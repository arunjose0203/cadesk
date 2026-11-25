import { defineConfig, devices } from '@playwright/test'

/*
 * End-to-end tests for the top flows (Sprint 72). They run against a real CaDesk: API + database + web.
 *
 *   1. Start PostgreSQL and the API (dotnet run --project src/CaDesk.Api), with
 *      RateLimits__AuthPerMinute=1000 so the tests' sign-ups aren't throttled.
 *   2. Start the web app: npm run dev (http://localhost:5173, proxies /api to the API).
 *   3. npx playwright install chromium   (once)
 *   4. npm run e2e
 *
 * E2E_BASE_URL points the tests at another environment, e.g. a staging server. Every test signs up its own
 * firm, so the tests never touch existing data and can run again and again.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 60_000,
  expect: { timeout: 10_000 },
  fullyParallel: true,
  workers: process.env.CI ? 2 : 4,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    locale: 'en-IN',
    timezoneId: 'Asia/Kolkata',
    acceptDownloads: true,
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        // a Chrome already on the machine instead of Playwright's download
        ...(process.env.E2E_CHROME ? { launchOptions: { executablePath: process.env.E2E_CHROME } } : {}),
      },
    },
  ],
})
