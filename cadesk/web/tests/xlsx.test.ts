import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { readXlsxRows } from '../src/gst/xlsx.ts'
import { buildGstr1, parseSalesRegisterRows } from '../src/gst/gstr1.ts'

const load = (name: string) => {
  const b = readFileSync(new URL(`./fixtures/${name}`, import.meta.url))
  return b.buffer.slice(b.byteOffset, b.byteOffset + b.byteLength) as ArrayBuffer
}

test('Tally-style workbook: title rows skipped, dates read as dates, row numbers match Excel', async () => {
  const rows = await readXlsxRows(load('sales-tally-style.xlsx'))
  assert.equal(rows[2][1], 'Document Number')
  assert.equal(rows[3][2], '2026-10-05') // a real Excel date cell
  assert.equal(rows[3][3], '27AAPFU0939F1ZV')
  assert.equal(rows[3][4], 'Mumbai Foods & Co')
  assert.equal(rows[7][7], '2100') // 0.1 + 0.2 + 2099.7 without float noise
  assert.equal(rows[29][1], 'a & b < c')
  const { lines, cancelled, issues } = parseSalesRegisterRows(rows.slice(0, 8))
  assert.deepEqual(issues, [])
  assert.equal(lines.length, 3)
  assert.deepEqual(lines.map(l => l.row), [4, 5, 8]) // the blank Excel row 6 is skipped, numbering kept
  assert.deepEqual(cancelled.map(c => [c.row, c.docNumber]), [[7, 'KS/0414']])
  const r = buildGstr1({ gstin: '32AAACK1234M1ZU', period: '102026', lines, cancelled, aatoAbove5Cr: false })
  assert.deepEqual(r.issues.filter(i => i.severity === 'error'), [])
  assert.deepEqual(r.documentsIssued.map(d => [d.from, d.to, d.total, d.cancelled]), [['KS/0412', 'KS/0415', 4, 1]])
})

test('1904 date system gives the same dates', async () => {
  const rows = await readXlsxRows(load('sales-1904.xlsx'))
  assert.equal(rows[3][2], '2026-10-05')
  assert.equal(rows[4][2], '2026-10-07')
})

test('Excel shared strings, a formula result and Tally column names', async () => {
  const rows = await readXlsxRows(load('sales-excel-shared.xlsx'))
  assert.equal(rows[0][1], 'Voucher No')
  assert.equal(rows[1][2], '2026-10-03')
  assert.equal(rows[3][0], 'Credit Note') // formula cell (t="str")
  const { lines, issues } = parseSalesRegisterRows(rows)
  assert.deepEqual(issues, [])
  assert.deepEqual(lines.map(l => [l.docKind, l.docNumber, l.docDate, l.placeOfSupply, l.taxableValue]), [
    ['INV', 'INV-001', '2026-10-03', '32', 1000],
    ['INV', 'INV-002', '2026-10-04', '32', 1000],
    ['CRN', 'CN-001', '2026-10-06', '32', 100],
  ])
  const r = buildGstr1({ gstin: '32AAACK1234M1ZU', period: '102026', lines, aatoAbove5Cr: false })
  assert.deepEqual(r.issues.filter(i => i.severity === 'error').map(i => i.message), [])
})

test('old .xls and non-zip files give a clear message', async () => {
  const xls = new Uint8Array([0xd0, 0xcf, 0x11, 0xe0, 0xa1, 0xb1, 0x1a, 0xe1, 0, 0]).buffer
  await assert.rejects(readXlsxRows(xls), /97-2003/)
  await assert.rejects(readXlsxRows(new TextEncoder().encode('not a workbook at all, just text').buffer), /not a valid Excel/)
})
