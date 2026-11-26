import fs from 'node:fs/promises'
import { expect, test } from '@playwright/test'
import { api, openDesk, randomPan, signUpFirm } from './helpers'

test('Competitive Parity Pack: Zerodha CG Importer -> ITR-2, ITR-7 Trust, Schedule III Pack & CMA, Windows Tally XML Bridge', async ({ page }) => {
  const firm = await signUpFirm(page, 'Cochin Statutory Suite')
  const me = await api(page.request, firm, 'GET', '/api/auth/me') as { firmId: string }
  const upg = await api(page.request, firm, 'POST', '/api/subscriptions/upgrade', {
    targetTier: 'ENTERPRISE',
    billingInterval: 'Annual',
  }) as { invoiceNumber: string }
  await api(page.request, firm, 'POST', '/api/subscriptions/admin/apply-upgrade', {
    firmId: me.firmId,
    targetTier: 'ENTERPRISE',
    invoiceNumber: upg.invoiceNumber,
  })

  const hniClient = await api(page.request, firm, 'POST', '/api/clients', {
    name: 'Meera Thomas Investor',
    entityType: 'Individual',
    taxIds: { PAN: randomPan('P') },
    isActive: true,
  }) as { id: string }

  const trustClient = await api(page.request, firm, 'POST', '/api/clients', {
    name: 'Malabar Educational Trust',
    entityType: 'Trust',
    taxIds: { PAN: `AAAT${randomPan('P').slice(4, 9)}E` },
    isActive: true,
  }) as { id: string }

  const coClient = await api(page.request, firm, 'POST', '/api/clients', {
    name: 'Vembanad Foods Pvt Ltd',
    entityType: 'Company',
    taxIds: { PAN: randomPan('C'), CIN: 'U15400KL2019PTC058123' },
    isActive: true,
  }) as { id: string }

  // Seed Schedule III assessment (FY 2024-25 matches default desk filter)
  await api(page.request, firm, 'POST', '/api/schedule3/assessments', {
    clientId: coClient.id,
    financialYear: '2024-25',
    precedingFinancialYear: '2023-24',
    accountingStandard: 'Division I - AS (Non-Ind AS)',
    roundingUnit: 'Lakhs',
    revenueFromOperationsCurrent: 800_000_000,
    revenueFromOperationsPreceding: 550_000_000,
    totalIncomeCurrent: 850_000_000,
    totalIncomePreceding: 600_000_000,
    shareholdersEquityCurrent: 450_000_000,
    shareholdersEquityPreceding: 380_000_000,
    currentAssetsCurrent: 320_000_000,
    currentAssetsPreceding: 240_000_000,
    currentLiabilitiesCurrent: 180_000_000,
    currentLiabilitiesPreceding: 150_000_000,
  })

  // Enable LedgerSync feature for Windows Tally Bridge verification
  await api(page.request, firm, 'POST', '/api/features/ledger-sync', { enabled: true })

  // =========================================================================
  // 1. Zerodha Broker CG CSV Import -> Granular ISIN Schedule 112A & ITR-2 JSON
  // =========================================================================
  await openDesk(page, 'tax')
  await page.getByRole('button', { name: 'CG (ITR-2) & Proprietor (ITR-3)' }).click()
  await page.locator('label', { hasText: 'Assessee Client' }).locator('..').locator('select').selectOption(hniClient.id)

  const zerodhaCsv = [
    'Symbol,ISIN,Entry Date,Exit Date,Quantity,Buy Value,Sell Value,Profit,FMV as on 31st Jan 2018,Brokerage',
    'INFY,INE009A01021,2017-05-10,2025-11-15,100,90000,290000,200000,160000,0',
    'TCS,INE467B01029,2025-01-12,2025-05-20,40,150000,220000,70000,0,500',
  ].join('\n')

  await page.locator('label', { hasText: /Import Zerodha \/ Groww \/ CAMS/ }).locator('input[type="file"]').setInputFiles({
    name: 'zerodha_taxpnl.csv',
    mimeType: 'text/csv',
    buffer: Buffer.from(zerodhaCsv, 'utf-8'),
  })

  await expect(page.getByText('INE009A01021')).toBeVisible()
  await expect(page.getByText('Clear Trades (2)')).toBeVisible()

  await page.getByRole('button', { name: 'Compute ITR-2 & Compare Regimes' }).click()
  await expect(page.getByText('Capital Gains + VDA (115BBH)')).toBeVisible()

  await page.getByRole('button', { name: 'Save ITR-2' }).click()
  await expect(page.getByText(/Official ITR-2 JSON Export/i)).toBeVisible()
  await page.getByPlaceholder('Verification Name').fill('Meera Thomas')
  await page.getByPlaceholder('Signer PAN').fill(randomPan('P'))
  await page.getByPlaceholder('City').fill('Kochi')
  await page.getByPlaceholder('PIN').fill('682011')
  await page.getByRole('button', { name: 'Generate & Validate Official JSON' }).click()
  await expect(page.getByText('CBDT Schema Validated (0 errors)')).toBeVisible()
  await expect(page.locator('pre')).toContainText('"ShareOnOrBefore": "BE"')
  await expect(page.locator('pre')).toContainText('"ISINCode": "INE009A01021"')

  // =========================================================================
  // 2. ITR-7 Trust / Sec 8 / Institution Tax & AY 2026-27 Official JSON + PDF
  // =========================================================================
  await page.getByRole('button', { name: 'Trust / NGO (ITR-7)' }).click()
  await page.locator('label', { hasText: 'Trust / Institution Client' }).locator('..').locator('select').selectOption(trustClient.id)

  await page.getByRole('button', { name: 'Calculate ITR-7 Trust Exemption' }).click()
  await expect(page.getByText('15% Statutory Accumulation u/s 11(1)(a)')).toBeVisible()

  const trustPdfDlPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Download PDF' }).click()
  const trustPdfDl = await trustPdfDlPromise
  expect(trustPdfDl.suggestedFilename()).toMatch(/ITR-7.*\.pdf$/i)

  await page.getByRole('button', { name: 'Save ITR-7' }).click()
  await expect(page.getByText(/Official ITR-7 \(Trust \/ Institution\) JSON Export/i)).toBeVisible()

  await page.getByPlaceholder('Trustee / MD Name').fill('Joseph Mathew')
  await page.getByPlaceholder('Signer PAN').fill(randomPan('P'))
  await page.getByPlaceholder('City').fill('Kochi')
  await page.getByPlaceholder('PIN').fill('682018')
  await page.getByRole('button', { name: 'Generate & Validate Official JSON' }).click()
  await expect(page.getByText('CBDT Schema Validated (0 errors)')).toBeVisible()
  await expect(page.locator('pre')).toContainText('"FormName": "ITR-7"')
  await expect(page.locator('pre')).toContainText('"Schedule115BBI"')

  // =========================================================================
  // 3. Statutory Schedule III Financial Statements Pack PDF & CMA Builder
  // =========================================================================
  await openDesk(page, 'schedule3')
  await page.getByText('Vembanad Foods Pvt Ltd').first().click()
  await expect(page.getByRole('button', { name: 'Regenerate Note' })).toBeVisible()

  const s3PdfPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Download PDF' }).first().click()
  const s3PdfDl = await s3PdfPromise
  expect(s3PdfDl.suggestedFilename()).toMatch(/Statutory[-_]Financial[-_]Pack.*\.pdf$/i)

  await page.getByRole('button', { name: 'CMA Data & Bank Projections' }).click()
  await expect(page.getByText(/CMA Data & Bank Loan Working Capital Assessment/i)).toBeVisible()
  await expect(page.getByText(/Tandon/i).first()).toBeVisible()

  const cmaPdfPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: 'Download PDF' }).last().click()
  const cmaPdfDl = await cmaPdfPromise
  expect(cmaPdfDl.suggestedFilename()).toMatch(/CMA[-_]Data[-_]Report.*\.pdf$/i)

  // =========================================================================
  // 4. Lightweight Local Windows Tally XML Bridge (.ps1 / appsettings / CLI)
  // =========================================================================
  await openDesk(page, 'ledger-sync')
  await page.getByRole('button', { name: /ERP Sync Agents & Live Daybook/i }).click()
  await page.getByRole('button', { name: /\+ Register Sync Agent/i }).first().click()

  await page.getByPlaceholder('e.g. Accounts Server Agent').fill('Vembanad Tally Bridge')
  await page.getByPlaceholder('Acme Corp Ltd (2026-27)').fill('Vembanad Foods (25-26)')
  await page.getByRole('button', { name: 'Register Agent & Generate Token' }).click()

  await expect(page.getByText('Vembanad Tally Bridge')).toBeVisible()

  const ps1DownloadPromise = page.waitForEvent('download')
  await page.getByRole('button', { name: /Windows Bridge \(\.ps1\)/i }).click()
  const ps1Download = await ps1DownloadPromise
  expect(ps1Download.suggestedFilename()).toMatch(/CaDesk_TallyBridge_.*\.ps1$/i)
  const ps1Path = await ps1Download.path()
  if (ps1Path) {
    const ps1Text = await fs.readFile(ps1Path, 'utf-8')
    expect(ps1Text).toContain('ca_sync_')
    expect(ps1Text).toContain('http://localhost:9000')
    expect(ps1Text).toContain('/api/ledger-sync/trial-balance/push')
  }

  await page.getByRole('button', { name: /\.NET CLI/i }).click()
  await expect(page.getByText(/dotnet run --project tools\/CaDesk\.TallyBridge/i)).toBeVisible()
})
