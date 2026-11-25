import { test } from 'node:test'
import assert from 'node:assert/strict'
import { gstinCheckDigitOk, type SalesLine } from '../src/gst/gstr1.ts'
import { buildGstr9Working, emptyAnnualEntries, fyBounds, gstr9Json, gstr9WorkingCsv } from '../src/gst/gstr9.ts'

const B36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'
const withCheck = (b: string) => { for (const c of B36) if (gstinCheckDigitOk(b + c)) return b + c; throw new Error() }
const KA = withCheck('29AAFCB5678N1Z')

const line = (p: Partial<SalesLine>): SalesLine => ({
  row: 2, docKind: 'INV', docNumber: 'I-1', docDate: '2025-06-05', customerGstin: '', customerName: '', placeOfSupply: '',
  reverseCharge: false, invoiceValue: 0, category: 'TAXABLE', invoiceType: 'R', exportType: '', rate: 18,
  taxableValue: 1000, igst: 0, cgst: 90, sgst: 90, cess: 0, hsn: '090411', description: 'Pepper', uqc: 'KGS', quantity: 2,
  shippingPort: '', shippingBillNo: '', shippingBillDate: '', originalDocNumber: '', originalDocDate: '', originalInvoiceValue: 0, ...p,
})
const get = (rows: { code: string }[], code: string) => rows.find(r => r.code === code) as Record<string, number>

test('financial year bounds', () => {
  assert.deepEqual(fyBounds('2025-26'), { start: '2025-04-01', end: '2026-03-31' })
  assert.equal(fyBounds('2025-27'), null)
})

test('Tables 4 and 5 from a year of sales', () => {
  const w = buildGstr9Working('2025-26', [
    line({}), // 4A
    line({ docNumber: 'B-1', customerGstin: KA, igst: 180, cgst: 0, sgst: 0 }), // 4B
    line({ docNumber: 'E-1', exportType: 'WPAY', taxableValue: 5000, igst: 900, cgst: 0, sgst: 0 }), // 4C
    line({ docNumber: 'S-1', customerGstin: KA, invoiceType: 'SEWP', taxableValue: 3000, igst: 540, cgst: 0, sgst: 0 }), // 4D
    line({ docNumber: 'D-1', customerGstin: KA, invoiceType: 'DE', taxableValue: 2000, igst: 360, cgst: 0, sgst: 0 }), // 4E
    line({ docNumber: 'C-1', docKind: 'CRN', customerGstin: KA, taxableValue: 100, igst: 18, cgst: 0, sgst: 0 }), // 4I
    line({ docNumber: 'E-2', exportType: 'WOPAY', taxableValue: 7000, igst: 0, cgst: 0, sgst: 0 }), // 5A
    line({ docNumber: 'S-2', customerGstin: KA, invoiceType: 'SEWOP', taxableValue: 4000, igst: 0, cgst: 0, sgst: 0 }), // 5B
    line({ docNumber: 'R-1', customerGstin: KA, reverseCharge: true, taxableValue: 6000, igst: 0, cgst: 0, sgst: 0 }), // 5C
    line({ docNumber: 'X-1', category: 'EXEMPT', rate: 0, cgst: 0, sgst: 0, taxableValue: 800 }), // 5D
    line({ docNumber: 'N-1', category: 'NIL', rate: 0, cgst: 0, sgst: 0, taxableValue: 300 }), // 5E
    line({ docNumber: 'G-1', category: 'NONGST', rate: 0, cgst: 0, sgst: 0, taxableValue: 200, hsn: '' }), // 5F
  ])
  assert.deepEqual(w.issues, [])
  assert.equal(get(w.table4, '4A').txval, 1000)
  assert.equal(get(w.table4, '4B').igst, 180)
  assert.equal(get(w.table4, '4C').txval, 5000)
  assert.equal(get(w.table4, '4D').txval, 3000)
  assert.equal(get(w.table4, '4E').txval, 2000)
  assert.equal(get(w.table4, '4H').txval, 12000)
  assert.equal(get(w.table4, '4I').txval, 100)
  assert.equal(get(w.table4, '4N').txval, 11900)
  assert.equal(get(w.table4, '4N').igst, 1980 - 18)
  assert.deepEqual([5, 'A', 'B', 'C', 'D', 'E', 'F'].slice(1).map(c => get(w.table5, `5${c}`).txval), [7000, 4000, 6000, 800, 300, 200])
  assert.equal(get(w.table5, '5M').txval, 18300)
  assert.equal(w.totalTurnover, 30200)
})

test('Table 17 nets credit notes; documents outside the year are errors; amendments left out', () => {
  const w = buildGstr9Working('2025-26', [
    line({ quantity: 10 }),
    line({ docNumber: 'C-1', docKind: 'CRN', taxableValue: 200, cgst: 18, sgst: 18, quantity: 2 }),
    line({ docNumber: 'L-1', docDate: '2026-04-02' }),
    line({ docNumber: 'A-2', originalDocNumber: 'A-1', originalDocDate: '2025-05-01' }),
  ])
  assert.deepEqual(w.table17.map(h => [h.hsn, h.quantity, h.txval, h.cgst]), [['090411', 8, 800, 72]])
  assert.ok(w.issues.some(i => i.severity === 'error' && i.message.includes('outside the financial year')))
  assert.ok(w.issues.some(i => i.severity === 'warning' && i.message.includes('amended')))
  assert.match(gstr9WorkingCsv(w, '32X', '2025-26'), /4N,/)
})

test('4F and 4G entries flow into 4H, 4N and 5N (5N takes 4G back out)', () => {
  const entries = { ...emptyAnnualEntries(), advances: { txval: 500, igst: 0, cgst: 45, sgst: 45, cess: 0 }, inwardRcm: { txval: 2000, igst: 0, cgst: 180, sgst: 180, cess: 0 } }
  const w = buildGstr9Working('2025-26', [line({})], entries)
  assert.equal(get(w.table4, '4H').txval, 3500)
  assert.equal(get(w.table4, '4M').txval, 0)
  assert.equal(get(w.table4, '4N').txval, 3500)
  assert.equal(get(w.table4, '4N').cgst, 315)
  assert.equal(get(w.table5, '5N').txval, 1500)
})

test('GSTR-9 JSON: keys and shapes for Tables 4, 5 and 17', () => {
  const w = buildGstr9Working('2025-26', [
    line({}),
    line({ docNumber: 'E-1', exportType: 'WPAY', taxableValue: 5000, igst: 900, cgst: 0, sgst: 0 }),
    line({ docNumber: 'C-1', docKind: 'CRN', taxableValue: 100, cgst: 9, sgst: 9 }),
    line({ docNumber: 'X-1', category: 'EXEMPT', rate: 0, cgst: 0, sgst: 0, taxableValue: 800, hsn: '' }),
  ])
  const j = gstr9Json(w, '32AABCK1234M1ZU', '2025-26') as unknown as { fp: string; isnil: string; table4: Record<string, object>; table5: Record<string, object>; table17: { items: object[] } }
  assert.equal(j.fp, '032026')
  assert.equal(j.isnil, 'N')
  assert.deepEqual(Object.keys(j.table4), ['b2c', 'b2b', 'exp', 'sez', 'deemed', 'at', 'rchrg', 'cr_nt', 'dr_nt', 'amd_pos', 'amd_neg', 'sub_totalAG', 'sub_totalIL', 'sup_adv'])
  assert.deepEqual(j.table4.b2c, { txval: 1000, iamt: 0, camt: 90, samt: 90, csamt: 0 })
  assert.deepEqual(j.table4.exp, { txval: 5000, iamt: 900, csamt: 0 })
  assert.deepEqual(j.table4.cr_nt, { txval: 100, iamt: 0, camt: 9, samt: 9, csamt: 0 })
  assert.deepEqual(j.table4.sub_totalIL, { txval: -100, iamt: 0, camt: -9, samt: -9, csamt: 0 })
  assert.deepEqual(j.table4.sup_adv, { txval: 5900, iamt: 900, camt: 81, samt: 81, csamt: 0 })
  assert.deepEqual(Object.keys(j.table5), ['zero_rtd', 'sez', 'rchrg', 'exmt', 'nil', 'non_gst', 'cr_nt', 'dr_nt', 'amd_pos', 'amd_neg', 'sub_totalAF', 'sub_totalHK', 'tover_tax_np', 'total_tover'])
  assert.deepEqual(j.table5.exmt, { txval: 800 })
  assert.deepEqual(j.table5.total_tover, { txval: 6700 })
  assert.deepEqual(j.table17.items[0], { hsn_sc: '090411', uqc: 'KGS', desc: 'Pepper', isconcesstional: 'N', qty: 2, txval: 5900, rt: 18, iamt: 900, camt: 81, samt: 81, csamt: 0 })
})
