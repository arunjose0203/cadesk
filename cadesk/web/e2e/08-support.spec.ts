import { expect, test } from '@playwright/test'
import { openDesk, signUpFirm } from './helpers'

test('report a problem and get a reference', async ({ page }) => {
  await signUpFirm(page)
  await openDesk(page, 'tasks')
  await page.getByRole('button', { name: 'Report a problem' }).click()
  await page.getByRole('textbox', { name: 'What went wrong?' }).fill('E2E: the task list did not refresh after saving')
  await page.getByRole('button', { name: 'Send report' }).click()
  await expect(page.getByText(/PR-[0-9A-F]{8}/)).toBeVisible()
})

test('help centre and legal pages are published', async ({ page }) => {
  await page.goto('/help/index.html')
  await expect(page.getByRole('heading', { name: 'CaDesk Help' })).toBeVisible()
  await expect(page.locator('.topics li')).toHaveCount(8)
  await page.getByRole('link', { name: 'GST returns: GSTR-1, GSTR-3B check and GSTR-9' }).click()
  await expect(page.getByRole('heading', { name: 'GSTR-1', exact: true })).toBeVisible()
  for (const doc of ['terms', 'privacy', 'dpa']) {
    const res = await page.request.get(`/legal/${doc}.html`)
    expect(res.status()).toBe(200)
    expect(await res.text()).toContain('Version 2026-11-24')
  }
})

test('a new version of the terms is asked for once', async ({ page }) => {
  // a user who has not accepted yet sees the gate
  const res = await page.request.post('/api/auth/signup', {
    data: { firmName: `Gate ${Date.now().toString(36)}`, countryCode: 'IN', name: 'Test Partner', email: `gate-${Date.now()}@example.test`, password: 'Correct-Horse-42!' },
  })
  expect(res.ok()).toBeTruthy()
  await openDesk(page, 'today')
  await expect(page.locator('.legal-gate')).toBeVisible()
  await page.getByLabel(/I have read and accept/).check()
  await page.getByRole('button', { name: 'Accept and continue' }).click()
  await expect(page.locator('.legal-gate')).toHaveCount(0)
  await page.reload()
  await expect(page.locator('.sidebar-footer')).toBeVisible()
  await expect(page.locator('.legal-gate')).toHaveCount(0)
})
