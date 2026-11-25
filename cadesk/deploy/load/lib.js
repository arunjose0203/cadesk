// Shared helpers for the CaDesk load test (Sprint 72). k6 runs these; see README.md.
import http from 'k6/http'
import { check } from 'k6'

export const BASE = __ENV.BASE_URL || 'http://localhost:5080'
export const PASSWORD = __ENV.LOAD_PASSWORD || 'Load-Test-Password-42'
export const FIRMS = Number(__ENV.FIRMS || 50)
export const CLIENTS_PER_FIRM = Number(__ENV.CLIENTS || 500)

export const json = { headers: { 'Content-Type': 'application/json' } }
export const auth = token => ({ headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' } })

export function firmEmail(i) {
  return `load-firm-${String(i).padStart(3, '0')}@load.cadesk.test`
}

/** Signs in and returns an access token, or null. With the same email in several firms, takes the first firm. */
export function login(email, { quiet = false } = {}) {
  let res = http.post(`${BASE}/api/auth/login`, JSON.stringify({ email, password: PASSWORD }), { ...json, tags: { name: 'login' } })
  if (res.status === 200 && res.json('status') === 'choose_firm') {
    const firmId = res.json('firms.0.firmId')
    res = http.post(`${BASE}/api/auth/login`, JSON.stringify({ email, password: PASSWORD, firmId }), { ...json, tags: { name: 'login' } })
  }
  const ok = res.status === 200 && res.json('status') === 'ok'
  if (!quiet) check(res, { 'signed in': () => ok })
  return ok ? res.json('accessToken') : null
}

const LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
const CHARS = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'

/** Deterministic PAN for firm f, client c, so re-running the import reports duplicates rather than adding more. */
export function pan(f, c, holder) {
  const n = f * 10000 + c
  const a = LETTERS[n % 26], b = LETTERS[Math.floor(n / 26) % 26], d = LETTERS[Math.floor(n / 676) % 26]
  return a + b + d + holder + LETTERS[f % 26] + String(c).padStart(4, '0') + LETTERS[(f * 7 + c) % 26]
}

export function gstin(state, p) {
  const base = state + p + '1Z'
  let sum = 0
  for (let i = 0; i < 14; i++) {
    const v = CHARS.indexOf(base[i]) * (i % 2 === 0 ? 1 : 2)
    sum += Math.floor(v / 36) + (v % 36)
  }
  return base + CHARS[(36 - (sum % 36)) % 36]
}
