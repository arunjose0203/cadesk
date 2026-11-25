// Run: npm test   (Node 22+, uses node:test and built-in TypeScript type stripping)
import { test } from 'node:test'
import assert from 'node:assert/strict'
import {
  TDS_FINANCIAL_YEARS,
  TDS_SECTION_OPTIONS,
  TDS_STATE_CODES,
  form16TemplateCsv,
  isAct2025Fy,
  isValidFvuFileName,
  isValidPan,
  isValidTan,
  officialFormBadge,
  parseFvuErrClient,
  suggestedRpuFileName,
} from '../src/tds/tdsHelpers.ts'

test('TAN and PAN format validators handle standard and exempt tokens', () => {
  assert.equal(isValidTan('COCH01234A'), true)
  assert.equal(isValidTan('coch01234a'), true)
  assert.equal(isValidTan('COCH1234A'), false)
  assert.equal(isValidPan('AAACP1234D'), true)
  assert.equal(isValidPan('PANNOTAVBL'), false)
  assert.equal(isValidPan('PANNOTAVBL', true), true)
  assert.equal(isValidPan('PANAPPLIED', true), true)
  assert.equal(isValidPan('BADPAN'), false)
})

test('Protean FVU filename validator rejects underscores, spaces, and >12 chars', () => {
  assert.equal(isValidFvuFileName('140Q1.txt'), true)
  assert.equal(isValidFvuFileName('26QQ4.txt'), true)
  assert.equal(isValidFvuFileName('138Q2.txt'), true)
  assert.equal(isValidFvuFileName('26Q_Q1.txt'), false)
  assert.equal(isValidFvuFileName('26Q Q1.txt'), false)
  assert.equal(isValidFvuFileName('140Q12345.txt'), false)
  assert.equal(isValidFvuFileName('140Q1.TXT'), false)
})

test('Official form mapping distinguishes Act 1961 (<=2025-26) and Act 2025 (>=2026-27)', () => {
  assert.equal(isAct2025Fy('2025-26'), false)
  assert.equal(isAct2025Fy('2026-27'), true)
  assert.deepEqual(officialFormBadge('24Q', '2025-26'), {
    formNo: '24Q',
    actLabel: 'Income-tax Act, 1961 (FVU 9.5)',
  })
  assert.deepEqual(officialFormBadge('24Q', '2026-27'), {
    formNo: '138',
    actLabel: 'Income-tax Act, 2025 (FVU 1.2)',
  })
  assert.equal(suggestedRpuFileName('26Q', 'q1', '2026-27'), '140Q1.txt')
  assert.equal(suggestedRpuFileName('27Q', 'Q4', '2024-25'), '27QQ4.txt')
})

test('State codes and canonical TdsSection keys cover Protean FVU requirements', () => {
  assert.equal(TDS_STATE_CODES.length, 36)
  assert.equal(TDS_STATE_CODES.some((s) => s.code === '16' && s.name.includes('Kerala')), true)
  assert.equal(TDS_STATE_CODES.some((s) => s.code === '08'), false)
  assert.equal(TDS_FINANCIAL_YEARS[0], '2026-27')

  const values = new Set(TDS_SECTION_OPTIONS.map((o) => o.value))
  for (const key of ['192_salary', '194C_company', '194C_other', '194J_tech', '194J_fees', '194I_land', '194I_plant', '195']) {
    assert.equal(values.has(key), true, `Missing canonical section key ${key}`)
  }
})

test('parseFvuErrClient parses caret error rows and skips HTML headers', () => {
  const errSample = [
    '<html><body>ignored</body></html>',
    'Line Number^Record Type^Batch No^Challan No^Deductee No^Field^Error Code^Description',
    '4^DD^1^1^2^PAN^T-FV-3141^Invalid PAN of deductee',
    '3^CD^1^1^^BSR^T-FV-4012^Invalid BSR Code',
  ].join('\r\n')
  const parsed = parseFvuErrClient(errSample)
  assert.equal(parsed.length, 2)
  assert.deepEqual(parsed[0], {
    lineNumber: 4,
    recordType: 'DD',
    challanNo: 1,
    deducteeNo: 2,
    errorCode: 'T-FV-3141',
    description: 'Invalid PAN of deductee',
  })
  assert.equal(parsed[1]?.lineNumber, 3)
  assert.equal(parsed[1]?.recordType, 'CD')
  assert.equal(parsed[1]?.errorCode, 'T-FV-4012')
})

test('form16TemplateCsv emits expected headers and valid sample CSV rows', () => {
  const lines = form16TemplateCsv().split('\n')
  assert.equal(lines.length, 3)
  assert.ok(lines[0].includes('Deductee PAN'))
  assert.ok(lines[0].includes('Amount Paid'))
  assert.ok(lines[0].includes('TDS Amount'))
  assert.ok(lines[1].includes('AAACP1234D'))
})
