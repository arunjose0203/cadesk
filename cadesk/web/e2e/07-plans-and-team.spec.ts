import { expect, test } from '@playwright/test'
import { openDesk, signUpFirm, uniq } from './helpers'

test('a Starter firm sees Growth desks locked', async ({ page }) => {
  const firm = await signUpFirm(page)
  await openDesk(page, 'caro')
  await expect(page.getByRole('heading', { name: /is in the Growth plan/ })).toBeVisible()
  await expect(page.locator('.sidebar-plan-badge').first()).toBeVisible()
  // the API refuses too, not just the screen
  const res = await page.request.get('/api/caro/summary', { headers: { Authorization: `Bearer ${firm.accessToken}` } })
  expect(res.status()).toBe(403)
  expect((await res.json()).requiredPlan).toBe('GROWTH')
})

test('the Starter plan allows three users, open invitations included', async ({ page }) => {
  await signUpFirm(page)
  await openDesk(page, 'users')
  const invite = async (email: string) => {
    await page.getByPlaceholder('Email').fill(email)
    await page.getByRole('button', { name: 'Create invite' }).click()
  }
  await invite(`a-${uniq()}@example.test`)
  await invite(`b-${uniq()}@example.test`)
  await expect(page.getByRole('button', { name: 'Revoke' })).toHaveCount(2)
  await invite(`c-${uniq()}@example.test`)
  await expect(page.getByText(/Your plan allows 3 users/)).toBeVisible()
})
