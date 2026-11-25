import { expect, test } from '@playwright/test'
import { addClient, openDesk, randomPan, signUpFirm } from './helpers'

test('TDS Sprint 68: Form 16A CSV import, Deductor Profile, NSDL RPU 140 .txt export & vault save', async ({ page }) => {
  const firm = await signUpFirm(page, 'Cochin TDS')
  const client = await addClient(page.request, firm, 'Malabar Engineering Works Pvt Ltd', { stateCode: '32' })
  const respPan = randomPan('P')
  const dedPan = randomPan('C')

  const csv = [
    'Form,Financial Year,Quarter,Deductee PAN,Deductee Name,Section,Payment Date,Deduction Date,Amount Paid,Rate,TDS Amount,Certificate No,BSR Code,Challan Serial No,Challan Date',
    `26Q,2026-27,Q1,${dedPan},Kochi Contractors Pvt Ltd,194C_company,2026-05-15,2026-05-15,250000,2,5000,CERT001,0510025,00412,2026-06-05`,
  ].join('\n')

  await openDesk(page, 'tds')
  await expect(page.getByRole('heading', { name: /TDS \/ TCS Compliance & Form 24Q/i })).toBeVisible()

  // 1. Import Form 16A CSV into 2026-27 Q1
  await page.getByRole('button', { name: 'Form 16 / 16A Import' }).click()
  await page.locator('textarea').fill(csv)
  await page.getByRole('button', { name: 'Preview Import' }).click()
  await expect(page.getByText('Format: form16a_csv')).toBeVisible()
  await expect(page.getByRole('cell', { name: 'Kochi Contractors Pvt Ltd' })).toBeVisible()

  await page.getByRole('button', { name: 'Commit to TDS Register' }).click()
  await expect(page.getByText(/Imported 1 challan\(s\) and 1 deduction\(s\)/i)).toBeVisible()

  // 2. Switch to RPU .txt Export & FVU tab, complete Deductor + Responsible Person profile
  await page.getByRole('button', { name: 'RPU .txt Export & FVU' }).click()
  await page.getByPlaceholder('e.g. COCH01234A').fill('COCH01234A')

  const profileForm = page.locator('form')
  await profileForm.locator('input[required][maxlength="25"]').nth(0).fill('14 Marine Drive')
  await profileForm.locator('input[required][maxlength="6"]').nth(0).fill('682031')
  await profileForm.locator('input[required][type="email"]').nth(0).fill('accounts@malabar.test')
  await profileForm.locator('input[required][maxlength="10"]').nth(2).fill('9847011223') // Deductor phone

  await profileForm.locator('input[required][maxlength="75"]').nth(1).fill('Meera Menon') // Resp Name
  await profileForm.locator('input[required][maxlength="10"]').nth(3).fill(respPan) // Resp PAN
  await profileForm.getByRole('button', { name: 'Copy Deductor Address to Responsible Person' }).click()
  await profileForm.locator('input[required][type="email"]').nth(1).fill('meera@malabar.test')
  await profileForm.locator('input[required][maxlength="10"]').nth(4).fill('9847099887') // Resp Mobile

  await page.getByRole('button', { name: 'Save Deductor Profile' }).click()
  await expect(page.getByText(/Deductor & Responsible Person profile saved/i)).toBeVisible()

  // 3. Validate & generate Form 140 (26Q under Act 2025) NSDL RPU .txt + Save to Vault
  await page.getByRole('button', { name: 'Validate & Generate RPU .txt' }).click()
  await expect(page.getByText('FVU Pre-Check Passed')).toBeVisible()
  await expect(page.getByText(/File: 140Q1\.txt • Form 140/)).toBeVisible()
  await expect(page.locator('pre')).toContainText('1^FH^NS1^R^')
  await expect(page.locator('pre')).toContainText('^CD^')
  await expect(page.locator('pre')).toContainText('^DD^')
  await expect(page.locator('pre')).toContainText('Kochi Contractors Pvt Ltd')
  await expect(page.locator('pre')).toContainText('^1023^')

  const dlPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Download 140Q1.txt' }).click()
  const dl = await dlPromise
  expect(dl.suggestedFilename()).toBe('140Q1.txt')

  await page.getByRole('button', { name: 'Save .txt to Client Vault' }).click()
  await expect(page.getByText(/to client document vault\./i)).toBeVisible()
  expect(client.id).toBeTruthy()
})
