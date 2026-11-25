import { expect, test } from '@playwright/test'
import { addClient, openDesk, signUpFirm } from './helpers'

test('create a task with a checklist for a client', async ({ page }) => {
  const firm = await signUpFirm(page)
  await addClient(page.request, firm, 'St. Mary’s Hospital Trust')
  await openDesk(page, 'tasks')
  await page.getByRole('button', { name: 'New task' }).click()
  await page.getByLabel('Title').fill('Reply to GST notice ASMT-10')
  await page.locator('main').getByLabel('Client').selectOption({ label: 'St. Mary’s Hospital Trust' })
  await page.getByLabel('Checklist (one step per line)').fill('Collect purchase register\nDraft reply\nPartner review')
  await page.getByRole('button', { name: 'Create task' }).click()

  await page.getByRole('button', { name: 'All tasks' }).click()
  const row = page.locator('tr', { hasText: 'Reply to GST notice ASMT-10' })
  await expect(row).toBeVisible()
  await expect(row).toContainText('0/3')
})
