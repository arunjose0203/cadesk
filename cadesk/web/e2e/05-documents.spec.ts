import { expect, test } from '@playwright/test'
import { renderPdf } from '../src/pdf/pdf.ts'
import { addClient, openDesk, signUpFirm } from './helpers'

test('upload a PDF to a client’s documents', async ({ page }) => {
  const firm = await signUpFirm(page)
  await addClient(page.request, firm, 'Vembanad Resorts Pvt Ltd')
  await openDesk(page, 'clients')
  await page.locator('tr.clickable', { hasText: 'Vembanad Resorts Pvt Ltd' }).click()

  const pdf = renderPdf([{ kind: 'heading', level: 1, text: 'Board resolution' }, { kind: 'paragraph', text: 'Resolved that…' }], { title: 'Board resolution' })
  await page.locator('label.button-like', { hasText: 'Upload files' }).locator('input[type="file"]')
    .setInputFiles({ name: 'board-resolution.pdf', mimeType: 'application/pdf', buffer: Buffer.from(pdf) })
  await expect(page.getByText(/board-resolution\.pdf: saved/)).toBeVisible()
  await expect(page.getByRole('button', { name: /board-resolution/ })).toBeVisible()
})
