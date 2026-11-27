import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5064';

// Hot keys: most users land on the first few pages with the default page size and no filters, so traffic concentrates
// on a handful of cache keys. When one of them is missing (cold start, or expiry after the 1-minute TTL), every request
// that arrives during the ~1s database query misses the same key. Without stampede protection each runs its own query.
const PAGES = [1, 2, 3];
const PAGE_SIZE = 100;
// Upcoming is cached in Redis for 30s; pending always reads the database.
const APPOINTMENT_STATUSES = ['upcoming', 'pending'];

// Request tags reported per endpoint in the summary table.
const ENDPOINTS = ['visits page', 'owners page', 'visits api', ...APPOINTMENT_STATUSES.map((s) => `appointments ${s}`)];

// k6 only keeps per-tag numbers for metrics that have a threshold, so each endpoint gets ones that always pass.
const endpointThresholds = {};
for (const name of ENDPOINTS) {
  endpointThresholds[`http_reqs{name:${name}}`] = ['count>=0'];
  endpointThresholds[`http_req_failed{name:${name}}`] = ['rate>=0'];
  endpointThresholds[`http_req_duration{name:${name}}`] = ['max>=0'];
}

export const options = {
  summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)', 'p(99)'],
  scenarios: {
    // Arrival rate (iterations/s) keeps traffic coming when responses slow down, like real users, so a slow database piles up instead of throttling the test.
    // Full rate from the first second with no warm-up, and longer than the 1-minute TTL, so the run hits two stampedes:
    // the cold start and the expiry at ~60s. Flush Redis before each run (redis-cli flushall) to start cold.
    browse: {
      executor: 'constant-arrival-rate',
      rate: 40,
      timeUnit: '1s',
      duration: '90s',
      preAllocatedVUs: 100,
      maxVUs: 400,
    },
  },
  thresholds: {
    http_req_failed: [{ threshold: 'rate<0.001', abortOnFail: true, delayAbortEval: '5s' }],
    // Latency still fails the run but doesn't abort it, so the test continues until requests start erroring.
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
    ...endpointThresholds,
  },
};

const PARAMS = { timeout: '5s' };

export function setup() {
  for (let i = 0; i < 15; i++) {
    const res = http.get(`${BASE_URL}/health`, PARAMS);
    if (res.status === 200) return;
    sleep(2);
  }
  throw new Error(`App at ${BASE_URL} never became healthy`);
}

function pick(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

export default function () {
  const page = pick(PAGES);

  // The React pages load their data from these API calls.
  const visits = http.get(`${BASE_URL}/api/visits?page=${page}&pageSize=${PAGE_SIZE}`, { ...PARAMS, tags: { name: 'visits page' } });
  check(visits, { 'visits page 200': (r) => r.status === 200 });

  const owners = http.get(`${BASE_URL}/api/owners?page=${page}&pageSize=${PAGE_SIZE}`, { ...PARAMS, tags: { name: 'owners page' } });
  check(owners, { 'owners page 200': (r) => r.status === 200 });

  const api = http.get(`${BASE_URL}/api/visits?page=${page}&pageSize=${PAGE_SIZE}&species=Dog`, { ...PARAMS, tags: { name: 'visits api' } });
  check(api, { 'visits api 200': (r) => r.status === 200 });

  const status = pick(APPOINTMENT_STATUSES);
  const appointments = http.get(`${BASE_URL}/api/appointments?status=${status}&page=${page}&pageSize=${PAGE_SIZE}`, { ...PARAMS, tags: { name: `appointments ${status}` } });
  check(appointments, { 'appointments 200': (r) => r.status === 200 });

  sleep(Math.random() * 0.5);
}

// Runs at the end of every run, including one aborted by a threshold: k6's usual summary, then a table per endpoint.
export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: ' ', enableColors: true }) + '\n' + endpointReport(data),
  };
}

function endpointReport(data) {
  const seconds = data.state.testRunDurationMs / 1000;
  const header = ['Endpoint', 'Requests', 'Req/s', 'Failed', 'Avg', 'Median', 'p95', 'p99', 'Max'];
  const rows = [...ENDPOINTS.map((name) => endpointRow(data, name, `{name:${name}}`)), endpointRow(data, 'TOTAL', '')];

  const widths = header.map((h, i) => Math.max(h.length, ...rows.map((r) => r[i].length)));
  const line = (cells) => cells.map((c, i) => (i === 0 ? c.padEnd(widths[i]) : c.padStart(widths[i]))).join('  ');

  return [
    `Endpoint report (${seconds.toFixed(1)}s run)`,
    line(header),
    line(widths.map((w) => '-'.repeat(w))),
    ...rows.map(line),
    '',
  ].join('\n');
}

function endpointRow(data, label, tag) {
  const reqs = data.metrics[`http_reqs${tag}`];
  const failed = data.metrics[`http_req_failed${tag}`];
  const duration = data.metrics[`http_req_duration${tag}`];
  if (!reqs || reqs.values.count === 0) {
    return [label, '0', '-', '-', '-', '-', '-', '-', '-'];
  }

  const ms = (v) => (v >= 1000 ? `${(v / 1000).toFixed(2)}s` : `${v.toFixed(0)}ms`);
  return [
    label,
    String(reqs.values.count),
    reqs.values.rate.toFixed(1),
    `${failed.values.passes} (${(failed.values.rate * 100).toFixed(2)}%)`,
    ms(duration.values.avg),
    ms(duration.values.med),
    ms(duration.values['p(95)']),
    ms(duration.values['p(99)']),
    ms(duration.values.max),
  ];
}