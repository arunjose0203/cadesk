import { expect, test } from '@playwright/test'
import { PASSWORD, uniq } from './helpers'

test('a partner creates a firm, signs out and signs in again', async ({ page }) => {
  const id = uniq()
  const email = `e2e-${id}@example.test`
  await page.goto('/')
  await page.getByRole('button', { name: 'Sign In', exact: true }).click()
  await page.getByRole('button', { name: 'Create a firm' }).click()
  await page.getByPlaceholder('e.g. Sharma & Associates').fill(`E2E ${id}`.slice(0, 20))
  await page.getByPlaceholder('e.g. Arun Sharma').fill('Test Partner')
  await page.getByPlaceholder('partner@firm.com').fill(email)
  await page.getByPlaceholder('Min. 10 characters').fill(PASSWORD)

  // the terms must be accepted before the firm can be created
  const create = page.getByRole('button', { name: 'Create Firm Account' })
  await expect(create).toBeDisabled()
  await page.locator('.legal-signup-check input').check()
  await create.click()

  // the sign-up checkbox counts as acceptance: no second prompt
  await expect(page.getByRole('heading', { name: /Good (morning|afternoon|evening), Test/ })).toBeVisible()
  await expect(page.locator('.legal-gate')).toHaveCount(0)

  await page.getByRole('button', { name: 'Sign out' }).last().click()
  await page.getByPlaceholder('name@firm.com').fill(email)
  await page.locator('input[type="password"]').fill(PASSWORD)
  await page.getByRole('button', { name: 'Sign in to CA Desk' }).click()
  await expect(page.getByRole('heading', { name: /Good (morning|afternoon|evening), Test/ })).toBeVisible()
})

test('a wrong password is refused', async ({ page }) => {
  await page.goto('/')
  await page.getByRole('button', { name: 'Sign In', exact: true }).click()
  await page.getByPlaceholder('name@firm.com').fill(`nobody-${uniq()}@example.test`)
  await page.locator('input[type="password"]').fill('not-the-password-1')
  await page.getByRole('button', { name: 'Sign in to CA Desk' }).click()
  await expect(page.locator('form .error')).toBeVisible()
})
