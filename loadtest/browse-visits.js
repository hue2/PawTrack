import http from 'k6/http';
import { check, sleep } from 'k6';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

const SPECIES = ['', 'Dog', 'Cat', 'Rabbit', 'Bird', 'Reptile'];
const CITIES = ['', 'Austin', 'Denver', 'Seattle', 'Portland', 'Chicago', 'Boston'];
// The page size picker offers a few fixed choices, so requests share cache keys the way real users do.
const PAGE_SIZES = [25, 50, 100];
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
    browse: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 400,
      stages: [
        // Warm-up at a low rate so the cache fills before load increases.
        { duration: '10s', target: 50 },
        { duration: '20s', target: 200 },
        { duration: '30s', target: 200 },
        { duration: '10s', target: 0 },
      ],
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
  const page = Math.floor(Math.random() * 10) + 1;
  const species = pick(SPECIES);
  const city = pick(CITIES);
  const pageSize = pick(PAGE_SIZES);

  // The React pages load their data from these API calls (500 rows per page).
  const visits = http.get(`${BASE_URL}/api/visits?page=${page}&pageSize=${pageSize}&species=${species}&city=${city}`, { ...PARAMS, tags: { name: 'visits page' } });
  check(visits, { 'visits page 200': (r) => r.status === 200 });

  const owners = http.get(`${BASE_URL}/api/owners?page=${page}&pageSize=${pageSize}&city=${city}`, { ...PARAMS, tags: { name: 'owners page' } });
  check(owners, { 'owners page 200': (r) => r.status === 200 });

  const api = http.get(`${BASE_URL}/api/visits?page=${page}&pageSize=${pageSize}&species=${species}`, { ...PARAMS, tags: { name: 'visits api' } });
  check(api, { 'visits api 200': (r) => r.status === 200 });

  const status = pick(APPOINTMENT_STATUSES);
  const appointments = http.get(`${BASE_URL}/api/appointments?status=${status}&page=${page}&pageSize=${pageSize}`, { ...PARAMS, tags: { name: `appointments ${status}` } });
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