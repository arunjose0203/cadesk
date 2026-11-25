import { expect, test } from '@playwright/test'
import { gstinFor, openDesk, randomPan, signUpFirm } from './helpers'

test('add a client with the form and find it', async ({ page }) => {
  await signUpFirm(page)
  await openDesk(page, 'clients')
  await page.getByRole('button', { name: 'Add client' }).first().click()
  await page.getByLabel('Name *').fill('Kochi Spices Exports Pvt Ltd')
  await page.getByLabel('Entity type *').selectOption('Company')
  const pan = randomPan('C')
  await page.getByLabel(/^PAN/).fill(pan)
  await page.getByLabel(/^GSTIN/).fill(gstinFor('32', pan))
  await page.locator('section.card').getByRole('button', { name: 'Add client' }).click()
  await expect(page.getByText(/Added Kochi Spices Exports Pvt Ltd as /)).toBeVisible()

  await page.getByPlaceholder('Search name, code, PAN, GSTIN…').fill(pan)
  await expect(page.locator('tr.clickable')).toHaveCount(1)
})

test('import clients from a CSV: check first, then import the good rows', async ({ page }) => {
  await signUpFirm(page)
  await openDesk(page, 'clients')
  await page.getByRole('button', { name: 'Import from Excel' }).click()

  const csv = [
    'Name,Entity type,PAN,Email',
    `Malabar Textiles LLP,LLP,${randomPan('F')},accounts@malabar.example`,
    `Periyar Agro Industries Ltd,Company,${randomPan('C')},`,
    'No PAN Traders,Company,,',
  ].join('\r\n')
  await page.locator('input[type="file"][accept=".xlsx,.csv"]').setInputFiles({ name: 'clients.csv', mimeType: 'text/csv', buffer: Buffer.from(csv) })
  await page.getByRole('button', { name: 'Check file' }).click()

  await expect(page.locator('.summary')).toContainText('3 rows')
  await expect(page.locator('.summary')).toContainText('2 ready')
  await expect(page.locator('.summary')).toContainText('1 with problems')

  await page.getByRole('button', { name: 'Import 2 clients, skip 1' }).click()
  await expect(page.getByText('Imported 2 clients.')).toBeVisible()
})
