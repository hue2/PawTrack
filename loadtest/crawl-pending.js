import http from 'k6/http';
import { check, sleep } from 'k6';
import { Counter, Gauge, Rate } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5064';

// A crawler requesting pages the cache has never seen, alongside a staff member working the pending appointments queue.
// Every crawler request is a cache miss, so each one needs a database connection. Pending is never cached, so it needs one
// too: if the crawler holds them all, the staff member's requests wait and fail.
//
// The test passes only if the staff member is unaffected and the app's peak usage stays within its limits; the
// crawler's own errors (429/503) don't count. The peaks come from /api/diagnostics/load (LoadDiagnostics:Enabled).
// Run it against an app with the deployed connection pool (the docker-compose web service, or Rider with the same
// connection string), since a large default pool may never fill: docker compose run --rm k6 run /scripts/crawl-pending.js
//
// To crawl from several addresses, run one container normally (it resets the peaks, runs the staff member and prints
// the report) and start the others a few seconds later with CRAWL_ONLY=1, so each adds a crawler from its own IP address.
const CRAWL_ONLY = __ENV.CRAWL_ONLY === '1';

export const options = {
  scenarios: {
    // CRAWLERS raises the crawl above the pool size when testing against a larger pool, like Npgsql's default of 100.
    crawler: { executor: 'constant-vus', exec: 'crawl', vus: Number(__ENV.CRAWLERS || 40), duration: '30s' },
    ...(CRAWL_ONLY ? {} : {
      staff: { executor: 'constant-arrival-rate', exec: 'pendingQueue', rate: 1, timeUnit: '1s', duration: '30s', preAllocatedVUs: 5 },
    }),
  },
  thresholds: CRAWL_ONLY ? {} : {
    'http_req_failed{scenario:staff}': ['rate==0'],
    'http_req_duration{scenario:staff}': ['p(95)<500'],
    limits_respected: ['rate==1'],
  },
};

// The limits the fix configures. Code without limits reports none, and is measured against these instead.
const TARGET_GLOBAL = 10;
const TARGET_PER_CLIENT = 5;

const PARAMS = { timeout: '10s' };

const crawler429 = new Counter('crawler_429');
const crawler503 = new Counter('crawler_503');
const limitsRespected = new Rate('limits_respected');
const reported = {
  poolSize: new Gauge('pool_size'),
  peakConnections: new Gauge('peak_connections'),
  globalLimit: new Gauge('global_limit'),
  peakMisses: new Gauge('peak_cache_miss_queries'),
  perClientLimit: new Gauge('per_client_limit'),
  peakClientMisses: new Gauge('peak_client_cache_miss_queries'),
};

export function setup() {
  // Wait until the app serves a cached page, which also means its Redis connection is up: right after a restart the
  // first requests can fail for reasons unrelated to the crawl.
  for (let i = 0; i < 15; i++) {
    if (http.get(`${BASE_URL}/api/visits?page=1&pageSize=100`, PARAMS).status === 200) break;
    sleep(2);
  }
  // Start the peaks from now, so traffic before the test doesn't count. Crawl-only runs leave them to the reporting run.
  if (!CRAWL_ONLY) http.post(`${BASE_URL}/api/diagnostics/load/reset`, null, PARAMS);
}

// Each crawler VU walks its own range of pages, so no two requests share a cache key.
export function crawl() {
  const page = 2 + __VU * 100000 + __ITER;
  const res = http.get(`${BASE_URL}/api/visits?page=${page}&pageSize=100`, { ...PARAMS, tags: { name: 'crawler' } });
  if (res.status === 429) crawler429.add(1);
  if (res.status === 503) crawler503.add(1);
}

export function pendingQueue() {
  const res = http.get(`${BASE_URL}/api/appointments?status=pending&pageSize=100`, { ...PARAMS, tags: { name: 'pending queue' } });
  check(res, { 'pending queue 200': (r) => r.status === 200 });
}

export function teardown() {
  if (CRAWL_ONLY) return;
  const res = http.get(`${BASE_URL}/api/diagnostics/load`, PARAMS);
  if (res.status !== 200) {
    // Without the numbers the limits can't be shown to hold, so the run fails rather than passing silently.
    limitsRespected.add(false);
    return;
  }

  const d = res.json();
  reported.poolSize.add(d.poolSize);
  reported.peakConnections.add(d.peaks.peakConnections);
  reported.peakMisses.add(d.peaks.peakCacheMissQueries);
  reported.peakClientMisses.add(d.peaks.peakClientCacheMissQueries);
  if (d.globalLimit != null) reported.globalLimit.add(d.globalLimit);
  if (d.perClientLimit != null) reported.perClientLimit.add(d.perClientLimit);

  limitsRespected.add(d.peaks.peakConnections < d.poolSize);
  limitsRespected.add(d.peaks.peakCacheMissQueries <= (d.globalLimit ?? TARGET_GLOBAL));
  limitsRespected.add(d.peaks.peakClientCacheMissQueries <= (d.perClientLimit ?? TARGET_PER_CLIENT));
}

export function handleSummary(data) {
  if (CRAWL_ONLY) {
    const count = (name) => data.metrics[name]?.values.count ?? 0;
    return { stdout: `Crawl only: ${count('http_reqs')} requests, 429: ${count('crawler_429')}, 503: ${count('crawler_503')}\n` };
  }
  return { stdout: textSummary(data, { indent: ' ', enableColors: true }) + '\n' + limitsReport(data.metrics) };
}

// One row per limit, comparing the configured value with the peak the app reported during the run.
function limitsReport(m) {
  const value = (name) => m[name]?.values.value;
  const count = (name) => m[name]?.values.count ?? 0;
  if (value('pool_size') === undefined) {
    return 'Limits report: /api/diagnostics/load was unavailable (set LoadDiagnostics__Enabled=true).\n';
  }

  const pool = value('pool_size');
  const peakConnections = value('peak_connections');
  const limitRow = (label, limit, target, peak) => [
    label,
    limit === undefined ? `none (target ${target})` : String(limit),
    String(peak),
    peak <= (limit ?? target) ? 'ok' : 'EXCEEDED',
  ];

  const staffFailed = m['http_req_failed{scenario:staff}']?.values;
  const staffDuration = m['http_req_duration{scenario:staff}']?.values;
  const rows = [
    ['DB connections in use', `< ${pool} (pool)`, String(peakConnections),
      peakConnections < pool ? `ok, ${pool - peakConnections} always free` : 'EXCEEDED, none free'],
    limitRow('Cache-miss queries let through, all clients', value('global_limit'), TARGET_GLOBAL, value('peak_cache_miss_queries')),
    limitRow('Cache-miss queries let through, one client', value('per_client_limit'), TARGET_PER_CLIENT, value('peak_client_cache_miss_queries')),
    ['Pending queue failures', '0', String(staffFailed?.passes ?? 0), staffFailed?.passes ? 'FAILED' : 'ok'],
    ['Pending queue p95', '< 500 ms', `${Math.round(staffDuration?.['p(95)'] ?? 0)} ms`, (staffDuration?.['p(95)'] ?? 0) < 500 ? 'ok' : 'FAILED'],
    ['Crawler requests rejected', '-', `429: ${count('crawler_429')}, 503: ${count('crawler_503')}`, ''],
  ];

  const header = ['Limit', 'Allowed', 'Peak', 'Result'];
  const widths = header.map((h, i) => Math.max(h.length, ...rows.map((r) => r[i].length)));
  const line = (cells) => cells.map((c, i) => c.padEnd(widths[i])).join('  ').trimEnd();
  return ['Limits report', line(header), line(widths.map((w) => '-'.repeat(w))), ...rows.map(line), ''].join('\n');
}
