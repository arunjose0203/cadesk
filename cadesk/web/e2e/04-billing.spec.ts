import { expect, test } from '@playwright/test'
import { addClient, openDesk, signUpFirm } from './helpers'

test('raise an invoice, send it and record the payment', async ({ page }) => {
  const firm = await signUpFirm(page)
  const client = await addClient(page.request, firm, 'Thomas & Sons Builders')
  await openDesk(page, 'billing')

  await page.getByRole('button', { name: /New Invoice/ }).click()
  const form = page.locator('form', { hasText: 'Invoice Line Items' })
  await form.locator('select').first().selectOption({ label: `Thomas & Sons Builders (${client.code})` })
  await form.getByPlaceholder('e.g. GST Audit or Filing').fill('GST returns, October 2026')
  await form.locator('input[type="number"]').nth(1).fill('5000')   // unit price
  await form.getByRole('button', { name: 'Save Invoice' }).click()

  const row = page.locator('tr', { hasText: 'Thomas & Sons Builders' }).first()
  await expect(row).toContainText('draft')
  await row.getByRole('button', { name: 'Send' }).click()
  await expect(row).toContainText('sent')

  await row.getByRole('button', { name: '₹ Pay' }).click()
  await page.getByRole('button', { name: 'Confirm Payment' }).click()
  await expect(row).toContainText('paid')
})
