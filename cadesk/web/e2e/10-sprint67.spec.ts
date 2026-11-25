import { expect, test } from '@playwright/test'
import { addClient, openDesk, randomPan, signUpFirm } from './helpers'

test('Income-tax Sprint 67: ITR-1, ITR-4 & Form 3CA-3CD official JSON export, schema validation & vault save', async ({ page }) => {
  const firm = await signUpFirm(page, 'Cochin Direct Tax')
  const client = await addClient(page.request, firm, 'Periyar Valley Enterprises Pvt Ltd', { stateCode: '32' })
  expect(client.id).toBeTruthy()

  // 1. ITR-1 (Sahaj) JSON export from Income Tax & Regime Desk
  await openDesk(page, 'tax')
  await page.locator('label', { hasText: 'Gross Salary' }).locator('..').locator('input').fill('1250000')
  await page.locator('label', { hasText: 'STCG 111A' }).locator('..').locator('input').fill('0')
  await page.locator('label', { hasText: 'LTCG 112A' }).locator('..').locator('input').fill('0')
  await page.getByRole('button', { name: 'Calculate & Compare Regimes' }).click()
  await page.getByRole('button', { name: 'Save Record' }).click()

  await expect(page.getByRole('heading', { name: 'Saved Client Tax Computations' })).toBeVisible()
  await page.getByRole('button', { name: 'View / Export JSON' }).first().click()
  await expect(page.getByText('Official ITR-1 (Sahaj) JSON Export (AY 2026-27 V1.1)')).toBeVisible()

  await page.getByPlaceholder("Father's Name").fill('Raghavan Nair')
  await page.getByPlaceholder('City (e.g. Kochi)').fill('Kochi')
  await page.getByPlaceholder('PIN Code (e.g. 682001)').fill('682031')

  await page.getByRole('button', { name: 'Generate & Validate Official JSON' }).click()
  await expect(page.getByText('CBDT Schema Validated (0 errors)')).toBeVisible()
  await expect(page.locator('pre')).toContainText('"FormName": "ITR-1"')

  const itr1DownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: /Download .*_ITR1_202627\.json/ }).click()
  const itr1Download = await itr1DownloadPromise
  expect(itr1Download.suggestedFilename()).toMatch(/_ITR1_202627\.json$/)

  await page.getByRole('button', { name: 'Save JSON to Vault' }).click()
  await expect(page.getByText(/to client document vault\./i)).toBeVisible()

  await page.getByRole('button', { name: 'Close' }).click()

  // 2. ITR-4 (Sugam) JSON export from Presumptive Tax Desk
  await openDesk(page, 'presumptive')
  await page.getByRole('button', { name: /New Presumptive Computation/i }).click()
  await expect(page.getByRole('heading', { name: 'Create Presumptive Tax Computation' })).toBeVisible()

  await page.locator('form select').first().selectOption(client.id)
  await page.locator('label', { hasText: 'Digital Turnover' }).locator('..').locator('input').fill('4000000')
  await page.locator('label', { hasText: 'Cash Turnover' }).locator('..').locator('input').fill('500000')
  await page.locator('label', { hasText: 'Declared Net Profit' }).locator('..').locator('input').fill('400000')
  await page.getByRole('button', { name: 'Create Computation' }).click()

  // Create Computation automatically opens the Desk modal cleanly
  await page.getByRole('button', { name: '3. Statement & Disclosures' }).click()
  await expect(page.getByText('Official ITR-4 (Sugam) JSON Export (AY 2026-27 V1.1)')).toBeVisible()

  await page.getByPlaceholder("Father's Name").fill('Raghavan Nair')
  await page.getByPlaceholder('City (e.g. Kochi)').fill('Kochi')
  await page.getByPlaceholder('PIN Code').fill('682031')

  await page.getByRole('button', { name: 'Generate & Validate Official JSON' }).click()
  await expect(page.getByText('CBDT Schema Validated (0 errors)')).toBeVisible()
  await expect(page.locator('pre')).toContainText('"FormName": "ITR-4"')

  const itr4DownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: /Download .*_ITR4_202627\.json/ }).click()
  const itr4Download = await itr4DownloadPromise
  expect(itr4Download.suggestedFilename()).toMatch(/_ITR4_202627\.json$/)

  await page.getByRole('button', { name: 'Save JSON to Vault' }).click()
  await expect(page.getByText(/to client document vault\./i)).toBeVisible()

  // Close Presumptive Desk modal before navigating via sidebar
  await page.getByRole('button', { name: '✕' }).click()

  // 3. Form 3CA-3CD JSON export from Tax Audit Desk
  await openDesk(page, 'taxaudit')
  await page.getByRole('button', { name: '+ New Tax Audit (3CD)' }).click()
  await expect(page.getByRole('heading', { name: 'Create Form 3CD Tax Audit Assessment' })).toBeVisible()

  await page.locator('form select').first().selectOption(client.id)
  const panField = page.locator('label', { hasText: /^PAN$/ }).locator('..').locator('input')
  if (!(await panField.inputValue())) {
    await panField.fill(randomPan('C'))
  }
  await page.locator('label', { hasText: 'Total Turnover / Receipts' }).locator('..').locator('input').fill('25000000')
  await page.getByLabel('Audited under other law (Form 3CA)').check()
  await page.getByRole('button', { name: 'Create Assessment' }).click()

  await page.getByRole('button', { name: /Tax Audit Report Draft \(Form3CA\)/i }).click()
  await expect(page.getByText('Official CBDT Form3CA + 3CD JSON Export (V2.5)')).toBeVisible()

  await page.getByPlaceholder('FRN (e.g. 004321S)').fill('004321S')
  await page.getByPlaceholder('CA Firm Name').fill('Menon & Associates')
  await page.getByRole('button', { name: 'Generate & Validate Official JSON' }).click()

  await expect(page.getByText('CBDT Schema Validated (0 errors)')).toBeVisible()
  await expect(page.locator('pre')).toContainText('"FORM3CA"')

  const form3CdDownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: /Download .*_FORM3CA_202627\.json/ }).click()
  const form3CdDownload = await form3CdDownloadPromise
  expect(form3CdDownload.suggestedFilename()).toMatch(/_FORM3CA_202627\.json$/)

  await page.getByRole('button', { name: 'Save JSON to Vault' }).click()
  await expect(page.getByText(/to client document vault\./i)).toBeVisible()
})
