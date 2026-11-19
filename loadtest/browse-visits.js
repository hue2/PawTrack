import http from 'k6/http';
import { check, sleep } from 'k6';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:8080';

const SPECIES = ['', 'Dog', 'Cat', 'Rabbit', 'Bird', 'Reptile'];
const CITIES = ['', 'Austin', 'Denver', 'Seattle', 'Portland', 'Chicago', 'Boston'];

export const options = {
  stages: [
    { duration: '20s', target: 20 },
    { duration: '40s', target: 150 },
    { duration: '30s', target: 150 },
    { duration: '10s', target: 0 },
  ],
  thresholds: {
    http_req_failed: ['rate<0.001'],
    http_req_duration: ['p(95)<500', 'p(99)<1000'],
  },
};

export function setup() {
  for (let i = 0; i < 60; i++) {
    const res = http.get(`${BASE_URL}/health`);
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

  const visits = http.get(`${BASE_URL}/?p=${page}&species=${species}&city=${city}`, { tags: { name: 'visits page' } });
  check(visits, { 'visits page 200': (r) => r.status === 200 });

  const owners = http.get(`${BASE_URL}/Owners?p=${page}&city=${city}`, { tags: { name: 'owners page' } });
  check(owners, { 'owners page 200': (r) => r.status === 200 });

  const api = http.get(`${BASE_URL}/api/visits?page=${page}&pageSize=100&species=${species}`, { tags: { name: 'visits api' } });
  check(api, { 'visits api 200': (r) => r.status === 200 });

  sleep(Math.random() * 0.5);
}
