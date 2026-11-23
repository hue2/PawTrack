import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

const SPECIES = ['', 'Dog', 'Cat', 'Rabbit', 'Bird', 'Reptile'];
const CITIES = ['', 'Austin', 'Denver', 'Seattle', 'Portland', 'Chicago', 'Boston'];

export const options = {
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
        { duration: '20s', target: 10 },
        { duration: '40s', target: 200 },
        { duration: '30s', target: 200 },
        { duration: '10s', target: 0 },
      ],
    },
  },
  thresholds: {
    http_req_failed: [{ threshold: 'rate<0.001', abortOnFail: true, delayAbortEval: '5s' }],
    // Latency still fails the run but doesn't abort it, so the test continues until requests start erroring.
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
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
  const pageSize = Math.floor(Math.random() * 100) + 1;

  // The React pages load their data from these API calls (500 rows per page).
  const visits = http.get(`${BASE_URL}/api/visits?page=${page}&pageSize=${pageSize}&species=${species}&city=${city}`, { ...PARAMS, tags: { name: 'visits page' } });
  check(visits, { 'visits page 200': (r) => r.status === 200 });

  const owners = http.get(`${BASE_URL}/api/owners?page=${page}&pageSize=${pageSize}&city=${city}`, { ...PARAMS, tags: { name: 'owners page' } });
  check(owners, { 'owners page 200': (r) => r.status === 200 });

  const api = http.get(`${BASE_URL}/api/visits?page=${page}&pageSize=${pageSize}&species=${species}`, { ...PARAMS, tags: { name: 'visits api' } });
  check(api, { 'visits api 200': (r) => r.status === 200 });

  sleep(Math.random() * 0.5);
}