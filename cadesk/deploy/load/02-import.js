// Step 2: import CLIENTS clients into every load firm through the real Excel/CSV import (preview rules, numbering,
// deadline generation). Run seed-plans.sql first: the Starter plan stops at 100 clients.
import http from 'k6/http'
import exec from 'k6/execution'
import { check } from 'k6'
import { BASE, CLIENTS_PER_FIRM, FIRMS, firmEmail, gstin, login, pan } from './lib.js'

export const options = {
  scenarios: { import: { executor: 'shared-iterations', vus: 5, iterations: FIRMS, maxDuration: '60m' } },
  thresholds: { checks: ['rate>0.99'], 'http_req_duration{name:import}': ['p(95)<60000'] },
}

const TYPES = [['Company', 'C'], ['LLP', 'F'], ['Firm', 'F'], ['Individual', 'P'], ['HUF', 'H']]
const STATES = ['32', '32', '32', '33', '29', '27']

export default function () {
  const f = exec.scenario.iterationInTest
  const token = login(firmEmail(f))
  if (!token) return
  const rows = ['Name,Entity type,PAN,GSTIN,Email,Phone']
  for (let c = 0; c < CLIENTS_PER_FIRM; c++) {
    const [type, holder] = TYPES[c % TYPES.length]
    const p = pan(f, c, holder)
    const g = c % 3 === 0 ? '' : gstin(STATES[c % STATES.length], p)   // a third have no GST registration
    rows.push(`Load client ${f}-${c},${type},${p},${g},client${c}@firm${f}.load.test,98${String(f * 10000 + c).padStart(8, '0')}`)
  }
  const res = http.post(`${BASE}/api/clients/import/commit?skipInvalid=true`, {
    file: http.file(rows.join('\r\n'), 'clients.csv', 'text/csv'),
  }, { headers: { Authorization: `Bearer ${token}` }, tags: { name: 'import' }, timeout: '120s' })
  check(res, {
    'import 200': r => r.status === 200,
    'all rows created (or already there)': r => r.status === 200 && (r.json('created') + r.json('skipped')) === CLIENTS_PER_FIRM,
  })
}
