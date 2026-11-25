import { expect, type APIRequestContext, type Page } from '@playwright/test'

/* Shared steps for the end-to-end tests. Every test works in a firm of its own, signed up for it. */

export const PASSWORD = 'Correct-Horse-42!'

export function uniq(): string {
  return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 6)}`
}

const LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
const pick = (s: string) => s[Math.floor(Math.random() * s.length)]

/** A PAN of the right shape; the 4th letter is the holder type (C company, P person, F firm, H HUF). */
export function randomPan(holder: 'C' | 'P' | 'F' | 'H' = 'C'): string {
  const digits = String(Math.floor(Math.random() * 10_000)).padStart(4, '0')
  return pick(LETTERS) + pick(LETTERS) + pick(LETTERS) + holder + pick(LETTERS) + digits + pick(LETTERS)
}

/** A GSTIN with a correct check character, for state code (e.g. "32" Kerala) and PAN. */
export function gstinFor(stateCode: string, pan: string, entity = '1'): string {
  const base = stateCode + pan + entity + 'Z'
  const chars = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'
  let sum = 0
  for (let i = 0; i < 14; i++) {
    const p = chars.indexOf(base[i]) * (i % 2 === 0 ? 1 : 2)
    sum += Math.floor(p / 36) + (p % 36)
  }
  return base + chars[(36 - (sum % 36)) % 36]
}

export type Firm = { firmName: string; email: string; accessToken: string }

/**
 * Signs up a firm through the API (quicker than the form, which has its own test) and accepts the legal
 * documents, so the page opens straight on Today. The refresh cookie lands in the page's browser context.
 */
export async function signUpFirm(page: Page, name = 'E2E'): Promise<Firm> {
  const id = uniq()
  const firmName = `${name} ${id}`.slice(0, 20)
  const email = `e2e-${id}@example.test`
  const res = await page.request.post('/api/auth/signup', {
    data: { firmName, countryCode: 'IN', name: 'Test Partner', email, password: PASSWORD },
  })
  expect(res.ok(), `sign-up failed: ${res.status()} ${await res.text()}`).toBeTruthy()
  const body = await res.json() as { accessToken: string }
  const accepted = await page.request.post('/api/legal/accept', {
    headers: { Authorization: `Bearer ${body.accessToken}` },
    data: { documents: ['terms', 'privacy', 'dpa'] },
  })
  expect(accepted.ok()).toBeTruthy()
  return { firmName, email, accessToken: body.accessToken }
}

export async function openDesk(page: Page, desk: string) {
  await page.goto(`/?page=${desk}`)
  await expect(page.locator('.sidebar-footer')).toBeVisible()
}

export async function api(request: APIRequestContext, firm: Firm, method: 'GET' | 'POST', path: string, data?: unknown) {
  const res = await request.fetch(path, { method, headers: { Authorization: `Bearer ${firm.accessToken}` }, data })
  expect(res.ok(), `${method} ${path}: ${res.status()} ${await res.text()}`).toBeTruthy()
  return res.status() === 204 ? null : res.json()
}

/** Adds a client through the API and returns its id and GSTIN. */
export async function addClient(request: APIRequestContext, firm: Firm, name: string, opts: { stateCode?: string } = {}) {
  const pan = randomPan('C')
  const gstin = gstinFor(opts.stateCode ?? '32', pan)
  const c = await api(request, firm, 'POST', '/api/clients', {
    name, entityType: 'Company', taxIds: { PAN: pan, GSTIN: gstin }, isActive: true,
  }) as { id: string; code: string }
  return { ...c, pan, gstin }
}
