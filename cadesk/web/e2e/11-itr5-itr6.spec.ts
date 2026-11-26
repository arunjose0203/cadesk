import { expect, test } from '@playwright/test'
import { addClient, api, openDesk, randomPan, signUpFirm } from './helpers'

test('Income-tax ITR-5 & ITR-6: Sec 40(b), 115BAA vs MAT 115JB computation, CBDT JSON export & Vault save', async ({ page }) => {
  const firm = await signUpFirm(page, 'Malabar Corporate Tax')

  const coClient = await addClient(page.request, firm, 'Vembanad Tech Pvt Ltd', { stateCode: '32' })
  const firmClient = await api(page.request, firm, 'POST', '/api/clients', {
    name: 'Alleppey Coir LLP',
    entityType: 'LLP',
    taxIds: { PAN: randomPan('F') },
    isActive: true,
  }) as { id: string }
  expect(coClient.id).toBeTruthy()
  expect(firmClient.id).toBeTruthy()

  // Navigate to Tax Desk -> Firm (ITR-5) & Company (ITR-6) tab
  await openDesk(page, 'tax')
  await page.getByRole('button', { name: 'Firm (ITR-5) & Company (ITR-6)' }).click()
  await expect(page.getByRole('heading', { name: /Corporate \(ITR-6\) & Firm \/ LLP \(ITR-5\)/i })).toBeVisible()

  // 1. Company (ITR-6): Auto-fill -> Compute -> Save -> Generate & Validate ITR-6 JSON
  await page.locator('label', { hasText: 'Client (Firm / Company)' }).locator('..').locator('select').selectOption(coClient.id)
  await page.getByRole('button', { name: /Auto-Fill from Trial Balance/i }).click()
  await expect(page.getByText(/Integrated Books & Register Sources/i)).toBeVisible()

  await page.locator('label', { hasText: 'Revenue from Operations' }).locator('..').locator('input').fill('40000000')
  await page.locator('label', { hasText: 'Purchases & Direct Cost' }).locator('..').locator('input').fill('25000000')
  await page.locator('label', { hasText: 'Employee Benefits Expense' }).locator('..').locator('input').fill('4000000')
  await page.locator('label', { hasText: 'Depreciation (Books / Co. Act)' }).locator('..').locator('input').fill('800000')
  await page.locator('label', { hasText: 'IT Act Sec 32 Depreciation' }).locator('..').locator('input').fill('950000')
  await page.locator('label', { hasText: 'Current Tax Provision' }).locator('..').locator('input').fill('1500000')

  await page.getByRole('button', { name: 'Calculate ITR-6 (Company)' }).click()
  await expect(page.getByText(/115BAA/i).first()).toBeVisible()
  await expect(page.getByText(/Net PGBP Income/i)).toBeVisible()

  await page.getByRole('button', { name: 'Save Computation & Unlock JSON' }).click()
  await expect(page.getByText('Official ITR-6 (Company) JSON Export (AY 2026-27 V1.0)')).toBeVisible()

  await page.getByPlaceholder('Signatory Name').fill('Rajan Varma')
  await page.getByPlaceholder('Signer Personal PAN').fill(randomPan('P'))
  await page.getByPlaceholder('City (e.g. Kochi)').fill('Kochi')
  await page.getByPlaceholder('PIN Code').fill('682031')

  await page.getByRole('button', { name: 'Generate & Validate Official JSON' }).click()
  await expect(page.getByText('CBDT Schema Validated (0 errors)')).toBeVisible()
  await expect(page.locator('pre')).toContainText('"FormName": "ITR-6"')

  const itr6DownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: /Download .*_ITR6_AY2026-27\.json/ }).click()
  const itr6Download = await itr6DownloadPromise
  expect(itr6Download.suggestedFilename()).toMatch(/_ITR6_AY2026-27\.json$/)

  await page.getByRole('button', { name: 'Save JSON to Vault' }).click()
  await expect(page.getByText(/to client document vault\./i)).toBeVisible()

  // 2. Partnership / LLP (ITR-5): Switch mode -> Select LLP -> Compute Sec 40(b) -> Save -> Export ITR-5
  await page.getByRole('button', { name: /Partnership Firm \/ LLP \/ AOP \(ITR-5/i }).click()
  await page.locator('label', { hasText: 'Client (Firm / Company)' }).locator('..').locator('select').selectOption(firmClient.id)

  await page.locator('label', { hasText: 'Revenue from Operations' }).locator('..').locator('input').fill('20000000')
  await page.locator('label', { hasText: 'Purchases & Direct Cost' }).locator('..').locator('input').fill('13000000')
  await page.locator('label', { hasText: 'Other Expenses' }).locator('..').locator('input').fill('1500000')

  await page.getByRole('button', { name: 'Calculate ITR-5 (Firm/LLP)' }).click()
  await expect(page.getByText('Section 40(b) Statutory Partner Ceilings (AY 2026-27)')).toBeVisible()

  await page.getByRole('button', { name: 'Save Computation & Unlock JSON' }).click()
  await expect(page.getByText('Official ITR-5 (Firm/LLP) JSON Export (AY 2026-27 V1.1)')).toBeVisible()

  await page.getByRole('button', { name: 'Generate & Validate Official JSON' }).click()
  await expect(page.getByText('CBDT Schema Validated (0 errors)')).toBeVisible()
  await expect(page.locator('pre')).toContainText('"FormName": "ITR-5"')

  const itr5DownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: /Download .*_ITR5_AY2026-27\.json/ }).click()
  const itr5Download = await itr5DownloadPromise
  expect(itr5Download.suggestedFilename()).toMatch(/_ITR5_AY2026-27\.json$/)
})
