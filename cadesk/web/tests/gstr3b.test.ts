import { test } from 'node:test'
import assert from 'node:assert/strict'
import { gstinCheckDigitOk, type SalesLine } from '../src/gst/gstr1.ts'
import {
  dueDate, emptyManualItc, isQuarterEnd, itcFrom2B, lateFee, lateInterest, monthlyDueDate, noTax, outwardFromSales, setOff, type ReconItem,
} from '../src/gst/gstr3b.ts'

const B36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'
const withCheck = (b: string) => { for (const c of B36) if (gstinCheckDigitOk(b + c)) return b + c; throw new Error() }
const CLIENT = withCheck('32AABCK1234M1Z')
const KA = withCheck('29AAFCB5678N1Z')

const line = (p: Partial<SalesLine>): SalesLine => ({
  row: 2, docKind: 'INV', docNumber: 'I-1', docDate: '2026-10-05', customerGstin: '', customerName: '', placeOfSupply: '',
  reverseCharge: false, invoiceValue: 0, category: 'TAXABLE', invoiceType: 'R', exportType: '', rate: 18,
  taxableValue: 1000, igst: 0, cgst: 90, sgst: 90, cess: 0, hsn: '090411', description: '', uqc: 'KGS', quantity: 1,
  shippingPort: '', shippingBillNo: '', shippingBillDate: '', originalDocNumber: '', originalDocDate: '', originalInvoiceValue: 0, ...p,
})

test('Table 3.1 and 3.2 from the sales register', () => {
  const o = outwardFromSales(CLIENT, [
    line({}), // local B2C 1000 + 90 + 90
    line({ docNumber: 'I-2', customerGstin: KA, igst: 180, cgst: 0, sgst: 0 }), // B2B inter-state
    line({ docNumber: 'I-3', placeOfSupply: '29', taxableValue: 2000, igst: 360, cgst: 0, sgst: 0 }), // B2C inter-state
    line({ docNumber: 'C-1', docKind: 'CRN', placeOfSupply: '29', taxableValue: 500, igst: 90, cgst: 0, sgst: 0 }), // note on it
    line({ docNumber: 'E-1', exportType: 'WPAY', taxableValue: 10000, igst: 1800, cgst: 0, sgst: 0 }),
    line({ docNumber: 'S-1', customerGstin: KA, invoiceType: 'SEWOP', taxableValue: 3000, igst: 0, cgst: 0, sgst: 0 }),
    line({ docNumber: 'N-1', category: 'EXEMPT', rate: 0, cgst: 0, sgst: 0, taxableValue: 700 }),
    line({ docNumber: 'N-2', category: 'NONGST', rate: 0, cgst: 0, sgst: 0, taxableValue: 300 }),
    line({ docNumber: 'R-1', customerGstin: KA, reverseCharge: true, igst: 0, cgst: 0, sgst: 0, taxableValue: 4000 }),
  ])
  assert.deepEqual(o.a, { txval: 3500, igst: 450, cgst: 90, sgst: 90, cess: 0 })
  assert.deepEqual(o.b, { txval: 13000, igst: 1800, cgst: 0, sgst: 0, cess: 0 })
  assert.equal(o.c, 700)
  assert.equal(o.e, 300)
  assert.deepEqual(o.interUnregistered, [{ pos: '29', state: 'Karnataka', txval: 1500, igst: 270 }])
  assert.deepEqual(o.reverseChargeOutward, { documents: 1, txval: 4000 })
})

const item = (p: Partial<ReconItem>): ReconItem => ({
  supplierGstin: KA, invoiceNumber: 'P-1', invoiceType: 'regular', matchStatus: 'exact_match', itcEligibility: 'eligible',
  prCgst: 0, prSgst: 0, prIgst: 100, prCess: 0, gstr2BCgst: 0, gstr2BSgst: 0, gstr2BIgst: 100, gstr2BCess: 0, ...p,
})

test('Table 4 from 2B: credit notes net in 4A(5), 17(5) reversed in 4B(1), POS to 4D(2), the rest held back', () => {
  const manual = { ...emptyManualItc(), importGoods: { igst: 50, cgst: 0, sgst: 0, cess: 0 } }
  const itc = itcFrom2B([
    item({}),
    item({ invoiceType: 'credit_note', prIgst: 30, gstr2BIgst: 30 }),
    item({ itcEligibility: 'ineligible_17_5', prIgst: 40, gstr2BIgst: 40 }),
    item({ itcEligibility: 'pos_mismatch', prIgst: 25, gstr2BIgst: 25 }),
    item({ matchStatus: 'missing_in_2b', prIgst: 60, gstr2BIgst: 0 }),
    item({ matchStatus: 'missing_in_books', prIgst: 0, gstr2BIgst: 70 }),
    item({ itcEligibility: 'supplier_cancelled', prIgst: 15, gstr2BIgst: 15 }),
    item({ matchStatus: 'tax_mismatch', prIgst: 80, gstr2BIgst: 90 }), // claim the lower: 80
  ], manual)
  assert.equal(itc.available.allOther.igst, 100 - 30 + 40 + 80)
  assert.equal(itc.available.total.igst, 190 + 50)
  assert.equal(itc.reversed.blocked17_5.igst, 40)
  assert.equal(itc.net.igst, 240 - 40)
  assert.equal(itc.other.ineligiblePos.igst, 25)
  assert.equal(itc.heldBack.notIn2B.count, 1)
  assert.equal(itc.heldBack.notIn2B.tax.igst, 60)
  assert.equal(itc.heldBack.notInBooks.tax.igst, 70)
  assert.equal(itc.heldBack.supplierCancelled.tax.igst, 15)
  assert.equal(itc.heldBack.loweredToBooks.tax.igst, 10)
})

test('net ITC can go negative when credit notes exceed invoices', () => {
  const itc = itcFrom2B([item({ invoiceType: 'credit_note', prIgst: 300, gstr2BIgst: 300 }), item({})], emptyManualItc())
  assert.equal(itc.net.igst, -200)
})

test('set-off: IGST credit goes to the head with a shortfall, CGST never pays SGST', () => {
  // Liability: IGST 100, CGST 500, SGST 500. Credit: IGST 400, CGST 500, SGST 100.
  const s = setOff({ igst: 100, cgst: 500, sgst: 500, cess: 0 }, { igst: 400, cgst: 500, sgst: 100, cess: 0 }, noTax())
  // IGST: 100 on IGST, then the 300 left all to SGST (its shortfall is 400; CGST has none).
  assert.deepEqual(s.steps.filter(x => x.from === 'igst'), [{ from: 'igst', to: 'igst', amount: 100 }, { from: 'igst', to: 'sgst', amount: 300 }])
  assert.deepEqual(s.cash, { igst: 0, cgst: 0, sgst: 100, cess: 0 })
  assert.deepEqual(s.carryForward, noTax())
})

test('set-off: Rule 88A uses IGST credit up before CGST/SGST credit', () => {
  const s = setOff({ igst: 0, cgst: 300, sgst: 300, cess: 0 }, { igst: 200, cgst: 300, sgst: 300, cess: 0 }, noTax())
  assert.equal(s.credit.igst - s.carryForward.igst, 200) // all IGST credit used
  assert.deepEqual(s.cash, noTax())
  assert.deepEqual(s.carryForward, { igst: 0, cgst: 100, sgst: 100, cess: 0 })
})

test('set-off: CGST and SGST credit pay IGST; cess only pays cess', () => {
  const s = setOff({ igst: 1000, cgst: 0, sgst: 0, cess: 50 }, { igst: 0, cgst: 300, sgst: 400, cess: 80 }, noTax())
  assert.deepEqual(s.cash, { igst: 300, cgst: 0, sgst: 0, cess: 0 })
  assert.deepEqual(s.carryForward, { igst: 0, cgst: 0, sgst: 0, cess: 30 })
})

test('set-off: shortfalls on both heads share IGST credit in proportion', () => {
  const s = setOff({ igst: 0, cgst: 400, sgst: 400, cess: 0 }, { igst: 300, cgst: 100, sgst: 300, cess: 0 }, noTax())
  // shortfalls: CGST 300, SGST 100 → IGST 300 split 225 / 75
  assert.deepEqual(s.steps.filter(x => x.from === 'igst'), [{ from: 'igst', to: 'cgst', amount: 225 }, { from: 'igst', to: 'sgst', amount: 75 }])
  assert.deepEqual(s.cash, { igst: 0, cgst: 75, sgst: 25, cess: 0 })
})

test('set-off: negative net ITC becomes liability; RCM is always cash', () => {
  const s = setOff({ igst: 100, cgst: 0, sgst: 0, cess: 0 }, { igst: -50, cgst: 0, sgst: 0, cess: 0 }, { igst: 0, cgst: 20, sgst: 20, cess: 0 })
  assert.equal(s.liability.igst, 150)
  assert.deepEqual(s.totalCash, { igst: 150, cgst: 20, sgst: 20, cess: 0 })
})

test('due date and interest', () => {
  assert.equal(monthlyDueDate('102026'), '2026-11-20')
  assert.equal(monthlyDueDate('122026'), '2027-01-20')
  const r = lateInterest({ igst: 36500, cgst: 0, sgst: 0, cess: 0 }, '2026-11-20', '2026-11-30')
  assert.equal(r.days, 10)
  assert.equal(r.interest.igst, 180) // 36,500 × 18% × 10 / 365
  assert.equal(lateInterest({ igst: 1000, cgst: 0, sgst: 0, cess: 0 }, '2026-11-20', '2026-11-19').days, 0)
})

test('QRMP due dates: 22nd for Kerala, 24th for Delhi; monthly stays on the 20th', () => {
  assert.equal(dueDate('122026', 'qrmp', '32'), '2027-01-22')
  assert.equal(dueDate('092026', 'qrmp', '07'), '2026-10-24')
  assert.equal(dueDate('092026', 'monthly', '07'), '2026-10-20')
  assert.equal(isQuarterEnd('092026'), true)
  assert.equal(isQuarterEnd('102026'), false)
})

test('late fee: Rs 25 + 25 a day, capped by turnover; nil returns Rs 10 + 10 capped at 250 + 250', () => {
  assert.deepEqual(lateFee('2026-11-20', '2026-11-30', false, 'upto1.5cr'), { days: 10, cgst: 250, sgst: 250 })
  assert.deepEqual(lateFee('2026-11-20', '2027-03-20', false, 'upto1.5cr'), { days: 120, cgst: 1000, sgst: 1000 })
  assert.deepEqual(lateFee('2026-11-20', '2027-03-20', false, '1.5to5cr'), { days: 120, cgst: 2500, sgst: 2500 })
  assert.deepEqual(lateFee('2026-11-20', '2027-11-20', false, 'above5cr'), { days: 365, cgst: 5000, sgst: 5000 })
  assert.deepEqual(lateFee('2026-11-20', '2026-12-20', true, 'above5cr'), { days: 30, cgst: 250, sgst: 250 })
  assert.deepEqual(lateFee('2026-11-20', '2026-11-20', false, 'above5cr'), { days: 0, cgst: 0, sgst: 0 })
})

test('amended documents are counted, not added to Table 3.1', () => {
  const o = outwardFromSales(CLIENT, [line({}), line({ docNumber: 'A-2', customerGstin: KA, igst: 180, cgst: 0, sgst: 0, originalDocNumber: 'A-1', originalDocDate: '2026-08-01' })])
  assert.equal(o.a.txval, 1000)
  assert.equal(o.amendedDocuments, 1)
})
