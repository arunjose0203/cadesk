// Step 1: sign up FIRMS firms (load-firm-000 … load-firm-049).
// Re-running is safe: a firm that can already sign in is left as it is.
import http from 'k6/http'
import exec from 'k6/execution'
import { check } from 'k6'
import { BASE, FIRMS, PASSWORD, firmEmail, json, login } from './lib.js'

export const options = {
  scenarios: { signup: { executor: 'shared-iterations', vus: 5, iterations: FIRMS, maxDuration: '10m' } },
  thresholds: { checks: ['rate>0.99'] },
}

export default function () {
  const n = exec.scenario.iterationInTest
  if (login(firmEmail(n), { quiet: true })) return   // signed up on an earlier run
  const res = http.post(`${BASE}/api/auth/signup`, JSON.stringify({
    firmName: `Load ${String(n).padStart(3, '0')}`, countryCode: 'IN', name: `Load Partner ${n}`, email: firmEmail(n), password: PASSWORD,
  }), { ...json, tags: { name: 'signup' } })
  check(res, { 'signed up': r => r.status === 200 })
}
