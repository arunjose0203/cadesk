import { expect, test } from '@playwright/test'
import { api, openDesk, randomPan, signUpFirm } from './helpers'

test('Income-tax ITR-2, ITR-3 & Official Computation Sheet PDF: Multi-HP, CG, VDA, ScheduleFA, Proprietor 44AB, JSON & Vault Save', async ({ page }) => {
  const firm = await signUpFirm(page, 'Travancore Wealth & Tax')

  const hniClient = await api(page.request, firm, 'POST', '/api/clients', {
    name: 'Vikram Menon',
    entityType: 'Individual',
    taxIds: { PAN: randomPan('P') },
    isActive: true,
  }) as { id: string }

  const propClient = await api(page.request, firm, 'POST', '/api/clients', {
    name: 'Nair Hardware Trading',
    entityType: 'HUF',
    taxIds: { PAN: randomPan('H') },
    isActive: true,
  }) as { id: string }

  expect(hniClient.id).toBeTruthy()
  expect(propClient.id).toBeTruthy()

  // Navigate to Tax Desk -> CG (ITR-2) & Proprietor (ITR-3) tab
  await openDesk(page, 'tax')
  await page.getByRole('button', { name: 'CG (ITR-2) & Proprietor (ITR-3)' }).click()
  await expect(page.getByRole('button', { name: /ITR-2 • Cap Gains/i })).toBeVisible()

  // 1. ITR-2 Workflow: Multi-Employer Salary, Multi-HP, Cap Gains, VDA 30%, Foreign Asset, Printable PDF & Official JSON
  await page.locator('label', { hasText: 'Assessee Client' }).locator('..').locator('select').selectOption(hniClient.id)
  await page.getByRole('button', { name: '+ Add Employer' }).click()
  await expect(page.locator('input[value="Employer 1"]')).toBeVisible()

  await page.getByRole('button', { name: '+ Add Property' }).click()
  await expect(page.locator('input[value="Property 1"]')).toBeVisible()

  await page.locator('label', { hasText: 'VDA Sale Value (Sec 115BBH)' }).locator('..').locator('input').fill('450000')
  await page.getByRole('button', { name: '+ Add Foreign Asset (FA)' }).click()
  await expect(page.locator('input[value="United States"]')).toBeVisible()

  await page.getByRole('button', { name: 'Compute ITR-2 & Compare Regimes' }).click()
  await expect(page.getByText('Capital Gains + VDA (115BBH)')).toBeVisible()

  // Verify Official Computation Sheet PDF download + Vault Save
  const pdfDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Download PDF' }).click()
  const pdfDownload = await pdfDownloadPromise
  expect(pdfDownload.suggestedFilename()).toMatch(/\.pdf$/i)

  await page.getByRole('button', { name: 'Save PDF to client documents' }).click()
  await expect(page.getByText(/Saved to Documents|to client/i).first()).toBeVisible({ timeout: 10000 })

  // Save ITR-2 and generate schema-validated ITR-2 JSON
  await page.getByRole('button', { name: 'Save ITR-2' }).click()
  await expect(page.getByText(/Official ITR-2 JSON Export/i)).toBeVisible()

  await page.getByPlaceholder('Verification Name').fill('Vikram Menon')
  await page.getByPlaceholder('Signer PAN').fill(randomPan('P'))
  await page.getByPlaceholder('City').fill('Kochi')
  await page.getByPlaceholder('PIN').fill('682036')

  await page.getByRole('button', { name: 'Generate & Validate Official JSON' }).click()
  await expect(page.getByText('CBDT Schema Validated (0 errors)')).toBeVisible()
  await expect(page.locator('pre')).toContainText('"FormName": "ITR-2"')
  await expect(page.locator('pre')).toContainText('"ScheduleVDA"')
  await expect(page.locator('pre')).toContainText('"ScheduleFA"')

  const itr2DownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: /Download .*_ITR2_AY2026-27\.json/ }).click()
  const itr2Download = await itr2DownloadPromise
  expect(itr2Download.suggestedFilename()).toMatch(/_ITR2_AY2026-27\.json$/)

  // 2. ITR-3 Workflow: Switch mode -> Proprietor 44AB Books -> Compute -> Save -> Generate & Validate ITR-3 JSON
  await page.getByRole('button', { name: /ITR-3 • Proprietor \/ Books u\/s 44AB/i }).click()
  await page.locator('label', { hasText: 'Assessee Client' }).locator('..').locator('select').selectOption(propClient.id)

  await page.getByRole('button', { name: 'Auto-Fill Books & 3CD' }).click()
  await expect(page.getByText(/TrialBalance:/i)).toBeVisible()

  await page.locator('label', { hasText: 'Turnover / Receipts' }).locator('..').locator('input').fill('22000000')
  await page.locator('label', { hasText: 'Purchases & Direct Cost' }).locator('..').locator('input').fill('16500000')
  await page.locator('label', { hasText: '43B(h) MSME Disallowance' }).locator('..').locator('input').fill('120000')

  await page.getByRole('button', { name: 'Compute ITR-3 & Compare Regimes' }).click()
  await expect(page.getByText('Proprietor Net PGBP (Sec 44AB / 32)')).toBeVisible()

  await page.getByRole('button', { name: 'Save ITR-3' }).click()
  await expect(page.getByText(/Official ITR-3 JSON Export/i)).toBeVisible()

  await page.getByPlaceholder('Verification Name').fill('Anoop Nair')
  await page.getByPlaceholder('Signer PAN').fill(randomPan('P'))
  await page.getByRole('button', { name: 'Generate & Validate Official JSON' }).click()
  await expect(page.getByText('CBDT Schema Validated (0 errors)')).toBeVisible()
  await expect(page.locator('pre')).toContainText('"FormName": "ITR-3"')
  await expect(page.locator('pre')).toContainText('"ITR3ScheduleBP"')

  // 3. History Modal also shows Computation PDF & ITR-3 JSON Export
  await page.getByRole('button', { name: /Computation Records/i }).click()
  await page.getByRole('button', { name: 'View / Export JSON' }).first().click()
  await expect(page.getByText('Printable Statement of Total Income (PDF)')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Close' })).toBeVisible()
})
