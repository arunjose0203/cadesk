// Step 3: the load test. USERS_PER_FIRM staff in each of FIRMS firms work at once, the way a firm does on a
// filing day: Today screen, deadlines, task list, client search, a client's record, billing summary.
// Pass/fail thresholds are at the bottom of options.
import http from 'k6/http'
import exec from 'k6/execution'
import { check, group, sleep } from 'k6'
import { BASE, FIRMS, auth, firmEmail, login } from './lib.js'

const USERS_PER_FIRM = Number(__ENV.USERS_PER_FIRM || 3)
const VUS = FIRMS * USERS_PER_FIRM

export const options = {
  scenarios: {
    filing_day: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: __ENV.RAMP || '2m', target: VUS },
        { duration: __ENV.HOLD || '10m', target: VUS },
        { duration: '1m', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],
    'http_req_duration{kind:read}': ['p(95)<800', 'p(99)<2000'],
    'http_req_duration{name:clients-search}': ['p(95)<800'],
    'http_req_duration{name:deadlines}': ['p(95)<1000'],
    checks: ['rate>0.99'],
  },
}

// one session per virtual user, renewed when the 15-minute access token runs out
const session = { token: null, at: 0 }

function token() {
  if (!session.token || Date.now() - session.at > 12 * 60 * 1000) {
    const firm = (exec.vu.idInTest - 1) % FIRMS
    session.token = login(firmEmail(firm))
    session.at = Date.now()
  }
  return session.token
}

function get(path, name) {
  const res = http.get(`${BASE}${path}`, { ...auth(token()), tags: { name, kind: 'read' } })
  if (res.status === 401) session.token = null
  check(res, { [`${name} 200`]: r => r.status === 200 })
  return res
}

const today = () => new Date().toISOString().slice(0, 10)
const inDays = d => new Date(Date.now() + d * 86400000).toISOString().slice(0, 10)

export default function () {
  if (!token()) { sleep(5); return }

  group('today', () => {
    get(`/api/deadlines?from=${inDays(-30)}&to=${inDays(14)}&status=open&take=50`, 'deadlines')
    get('/api/tasks?assignee=me&take=50', 'tasks')
    get('/api/invoices/summary', 'invoices-summary')
  })
  sleep(1 + Math.random() * 3)

  group('find a client', () => {
    const q = String(Math.floor(Math.random() * 500))
    const list = get(`/api/clients?search=${q}&take=25`, 'clients-search')
    const items = list.status === 200 ? list.json('items') : []
    if (items && items.length > 0) {
      const id = items[Math.floor(Math.random() * items.length)].id
      get(`/api/clients/${id}`, 'client')
      get(`/api/deadlines?clientId=${id}&status=open&take=8`, 'client-deadlines')
      get(`/api/clients/${id}/documents`, 'client-documents')
    }
  })
  sleep(2 + Math.random() * 5)

  group('deadlines desk', () => {
    get(`/api/deadlines?from=${today()}&to=${inDays(31)}&take=100`, 'deadlines-month')
  })
  sleep(2 + Math.random() * 5)
}
