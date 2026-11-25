import { expect, test } from '@playwright/test'
import { addClient, gstinFor, openDesk, randomPan, signUpFirm } from './helpers'

test('GSTR-1 JSON from a sales register', async ({ page }) => {
  const firm = await signUpFirm(page)
  const client = await addClient(page.request, firm, 'Kochi Spices Exports Pvt Ltd', { stateCode: '32' })
  const buyer = gstinFor('32', randomPan('C'))
  const csv = [
    'Document Type,Document Number,Document Date,Customer GSTIN,Customer Name,Place of Supply,Reverse Charge,Invoice Value,Rate,Taxable Value,IGST,CGST,SGST,Cess,HSN/SAC,Description,UQC,Quantity',
    `Invoice,KS/26-27/0412,05-10-2026,${buyer},Kochi Traders,32,N,11800,18,10000,0,900,900,0,090411,Black pepper,KGS,20`,
    'Invoice,KS/26-27/0413,07-10-2026,,Walk-in customer,32,N,1050,5,1000,0,25,25,0,090830,Cardamom,KGS,1',
  ].join('\r\n')

  await openDesk(page, 'gstr1-export')
  await page.locator('select').first().selectOption({ label: `Kochi Spices Exports Pvt Ltd · ${client.gstin}` })
  await page.locator('input[type="month"]').fill('2026-10')
  await page.locator('input[type="file"]').first().setInputFiles({ name: 'sales-oct.csv', mimeType: 'text/csv', buffer: Buffer.from(csv) })
  await expect(page.getByRole('heading', { name: 'GSTR-1 check results' })).toBeVisible()

  const download = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Download GSTR-1 JSON' }).click()
  const file = await (await download).path()
  const { readFileSync } = await import('node:fs')
  const json = JSON.parse(readFileSync(file, 'utf8')) as { gstin: string; fp: string; b2b?: { ctin: string }[]; b2cs?: unknown[] }
  expect(json.gstin).toBe(client.gstin)
  expect(json.fp).toBe('102026')
  expect(json.b2b?.[0].ctin).toBe(buyer)
  expect(json.b2cs?.length).toBe(1)
})
