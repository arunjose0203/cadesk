// Run: npm test   (Node 22+, uses node:test and built-in TypeScript type stripping)
import { test } from 'node:test'
import assert from 'node:assert/strict'
import {
  buildGstr1, documentSeries, gstinCheckDigitOk, isValidGstin, parseAmount, parseCsv, parseDate, parseSalesRegister,
  periodBounds, templateCsv, type SalesLine,
} from '../src/gst/gstr1.ts'

const B36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'
/** Adds the correct check digit to a 14-character GSTIN body. */
function withCheck(body14: string): string {
  for (const c of B36) if (gstinCheckDigitOk(body14 + c)) return body14 + c
  throw new Error('no check digit')
}
const CLIENT = withCheck('32AABCK1234M1Z') // Kerala
const KA_CUST = withCheck('29AAFCB5678N1Z') // Karnataka
const KL_CUST = withCheck('32AAGCM4321P1Z') // Kerala

function line(p: Partial<SalesLine>): SalesLine {
  return {
    row: 2, docKind: 'INV', docNumber: 'INV-1', docDate: '2026-10-05', customerGstin: '', customerName: '',
    placeOfSupply: '', reverseCharge: false, invoiceValue: 0, category: 'TAXABLE', invoiceType: 'R', exportType: '',
    rate: 18, taxableValue: 1000, igst: 0, cgst: 90, sgst: 90, cess: 0, hsn: '090411', description: 'Pepper',
    uqc: 'KGS', quantity: 1, shippingPort: '', shippingBillNo: '', shippingBillDate: '',
    originalDocNumber: '', originalDocDate: '', originalInvoiceValue: 0, ...p,
  }
}
const run = (lines: SalesLine[], aato = false) => buildGstr1({ gstin: CLIENT, period: '102026', lines, aatoAbove5Cr: aato })
const errors = (r: ReturnType<typeof run>) => r.issues.filter(i => i.severity === 'error').map(i => i.message)

test('GSTIN check digit: a known valid GSTIN passes, one changed character fails', () => {
  // 27AAPFU0939F1ZV is the sample GSTIN used in GSTN's own API documentation.
  assert.equal(gstinCheckDigitOk('27AAPFU0939F1ZV'), true)
  assert.equal(gstinCheckDigitOk('27AAPFU0939F1ZW'), false)
  assert.equal(isValidGstin(CLIENT), true)
  assert.equal(isValidGstin('99AAPFU0939F1ZV'), false) // no such state
})

test('period and date parsing', () => {
  assert.deepEqual(periodBounds('022028'), { start: '2028-02-01', end: '2028-02-29' })
  assert.equal(periodBounds('132026'), null)
  assert.equal(parseDate('05-10-2026'), '2026-10-05')
  assert.equal(parseDate('5/10/26'), '2026-10-05')
  assert.equal(parseDate('2026-10-05'), '2026-10-05')
  assert.equal(parseDate('5-Oct-2026'), '2026-10-05')
  assert.equal(parseDate('31-02-2026'), null)
  assert.equal(parseAmount('1,18,000.50'), 118000.5)
  assert.equal(parseAmount('₹ 1180'), 1180)
  assert.equal(parseAmount('(1,000)'), -1000)
  assert.ok(Number.isNaN(parseAmount('abc')))
})

test('CSV parser handles quotes, commas and blank lines', () => {
  const rows = parseCsv('﻿a,b\r\n"x, y","say ""hi"""\r\n\r\n1,2')
  assert.deepEqual(rows, [['a', 'b'], ['x, y', 'say "hi"'], ['1', '2']])
})

test('the downloadable template parses and builds without errors', () => {
  const { lines, cancelled, issues } = parseSalesRegister(templateCsv())
  assert.deepEqual(issues, [])
  assert.equal(lines.length, 3)
  assert.equal(cancelled.length, 1)
  const r = buildGstr1({ gstin: CLIENT, period: '102026', lines, cancelled, aatoAbove5Cr: false })
  assert.deepEqual(errors(r), [])
  assert.ok(r.json)
  // 0412 and 0413 issued, 0414 cancelled
  assert.deepEqual(r.documentsIssued.find(d => d.nature === 1), { nature: 1, natureLabel: 'Invoices for outward supply', from: 'KS/26-27/0412', to: 'KS/26-27/0414', total: 3, cancelled: 1, net: 2 })
})

test('B2B intra-state invoice: grouped under customer GSTIN, CGST/SGST items, one item per rate', () => {
  const r = run([
    line({ customerGstin: KL_CUST, invoiceValue: 1770, rate: 18, taxableValue: 1000, cgst: 90, sgst: 90 }),
    line({ row: 3, customerGstin: KL_CUST, invoiceValue: 1770, rate: 18, taxableValue: 200, cgst: 18, sgst: 18 }),
    line({ row: 4, customerGstin: KL_CUST, invoiceValue: 1770, rate: 5, taxableValue: 400, cgst: 10, sgst: 10, hsn: '090830' }),
  ])
  assert.deepEqual(errors(r), [])
  const b2b = r.json!.b2b as { ctin: string; inv: { pos: string; itms: { num: number; itm_det: Record<string, number> }[] }[] }[]
  assert.equal(b2b.length, 1)
  assert.equal(b2b[0].ctin, KL_CUST)
  const inv = b2b[0].inv[0]
  assert.equal(inv.pos, '32')
  assert.equal(inv.itms.length, 2)
  assert.deepEqual(inv.itms[1].itm_det, { txval: 1200, rt: 18, camt: 108, samt: 108, csamt: 0 })
  assert.equal(r.documentCount, 1)
})

test('B2C inter-state above Rs 1 lakh goes to B2CL; at or below goes to B2CS', () => {
  const big = line({ docNumber: 'B-1', placeOfSupply: '29', invoiceValue: 105000, rate: 5, taxableValue: 100000, cgst: 0, sgst: 0, igst: 5000 })
  const small = line({ row: 3, docNumber: 'B-2', placeOfSupply: '29', invoiceValue: 100000, rate: 5, taxableValue: 95238.1, cgst: 0, sgst: 0, igst: 4761.9 })
  const local = line({ row: 4, docNumber: 'B-3', invoiceValue: 500000, rate: 5, taxableValue: 476190.48, cgst: 11904.76, sgst: 11904.76 })
  const r = run([big, small, local])
  assert.deepEqual(errors(r), [])
  const b2cl = r.json!.b2cl as { pos: string; inv: { inum: string }[] }[]
  assert.equal(b2cl.length, 1)
  assert.equal(b2cl[0].pos, '29')
  assert.deepEqual(b2cl[0].inv.map(i => i.inum), ['B-1'])
  const b2cs = r.json!.b2cs as Record<string, unknown>[]
  assert.deepEqual(b2cs.find(x => x.sply_ty === 'INTER'), { sply_ty: 'INTER', pos: '29', typ: 'OE', rt: 5, txval: 95238.1, iamt: 4761.9, csamt: 0 })
  assert.deepEqual(b2cs.find(x => x.sply_ty === 'INTRA'), { sply_ty: 'INTRA', pos: '32', typ: 'OE', rt: 5, txval: 476190.48, camt: 11904.76, samt: 11904.76, csamt: 0 })
})

test('wrong tax head is an error; a rounding difference within Rs 1 is not', () => {
  const r1 = run([line({ placeOfSupply: '29', igst: 0, cgst: 90, sgst: 90 })])
  assert.ok(errors(r1).some(m => m.includes('must be IGST')))
  assert.equal(r1.json, null)
  const r2 = run([line({ igst: 180, cgst: 0, sgst: 0 })])
  assert.ok(errors(r2).some(m => m.includes('must be CGST + SGST')))
  const r3 = run([line({ cgst: 90.4, sgst: 90.4 })])
  assert.deepEqual(errors(r3), [])
  assert.equal(r3.issues.length, 0)
  const r4 = run([line({ cgst: 95, sgst: 95 })])
  assert.ok(r4.issues.some(i => i.severity === 'warning' && i.message.includes('differs')))
})

test('customer GSTIN state decides place of supply when the column is blank', () => {
  const r = run([line({ customerGstin: KA_CUST, igst: 180, cgst: 0, sgst: 0 })])
  assert.deepEqual(errors(r), [])
  const inv = (r.json!.b2b as { inv: { pos: string; itms: { itm_det: Record<string, number> }[] }[] }[])[0].inv[0]
  assert.equal(inv.pos, '29')
  assert.deepEqual(inv.itms[0].itm_det, { txval: 1000, rt: 18, iamt: 180, csamt: 0 })
})

test('credit note to a registered customer goes to CDNR with positive amounts', () => {
  const { lines } = parseSalesRegister([
    'Document Type,Document Number,Document Date,Customer GSTIN,Invoice Value,Rate,Taxable Value,IGST,CGST,SGST,Cess,HSN/SAC,UQC,Quantity',
    `Credit Note,CN-7,12-10-2026,${KL_CUST},-1180,18,-1000,0,-90,-90,0,090411,KGS,-2`,
  ].join('\n'))
  const r = run(lines)
  assert.deepEqual(errors(r), [])
  const nt = (r.json!.cdnr as { nt: Record<string, unknown>[] }[])[0].nt[0]
  assert.equal(nt.ntty, 'C')
  assert.equal(nt.val, 1180)
  // and the HSN summary is reduced, not increased
  const h = (r.json!.hsn as { hsn_b2b: Record<string, number>[] }).hsn_b2b[0]
  assert.equal(h.txval, -1000)
  assert.equal(h.qty, -2)
})

test('B2C small credit note is netted into Table 7', () => {
  const r = run([
    line({ docNumber: 'S-1', taxableValue: 1000, cgst: 90, sgst: 90 }),
    line({ row: 3, docKind: 'CRN', docNumber: 'C-1', taxableValue: 200, cgst: 18, sgst: 18 }),
  ])
  assert.deepEqual(errors(r), [])
  assert.deepEqual(r.json!.b2cs, [{ sply_ty: 'INTRA', pos: '32', typ: 'OE', rt: 18, txval: 800, camt: 72, samt: 72, csamt: 0 }])
})

test('exports: WOPAY needs no IGST, WPAY needs IGST; port code checked', () => {
  const r = run([
    line({ docNumber: 'E-1', exportType: 'WOPAY', cgst: 0, sgst: 0, igst: 0, shippingPort: 'INCOK1', shippingBillNo: '1234567', shippingBillDate: '2026-10-08' }),
    line({ row: 3, docNumber: 'E-2', exportType: 'WPAY', cgst: 0, sgst: 0, igst: 180 }),
  ])
  assert.deepEqual(errors(r), [])
  const exp = r.json!.exp as { exp_typ: string; inv: Record<string, unknown>[] }[]
  const wopay = exp.find(e => e.exp_typ === 'WOPAY')!.inv[0]
  assert.equal(wopay.sbpcode, 'INCOK1')
  assert.equal(wopay.sbdt, '08-10-2026')
  assert.ok((r.json!.hsn as Record<string, unknown>).hsn_b2c)
  const bad = run([line({ exportType: 'WOPAY', cgst: 0, sgst: 0, igst: 180, shippingPort: 'COCHIN' + 'X' })])
  assert.ok(errors(bad).some(m => m.includes('without payment')))
  assert.ok(errors(bad).some(m => m.includes('6-character port')))
})

test('nil-rated and exempt lines go to Table 8 by supply type', () => {
  const r = run([
    line({ docNumber: 'N-1', category: 'NIL', rate: 0, cgst: 0, sgst: 0, taxableValue: 5000, hsn: '07019000' }),
    line({ row: 3, docNumber: 'N-2', category: 'EXEMPT', customerGstin: KA_CUST, rate: 0, cgst: 0, sgst: 0, taxableValue: 2000, hsn: '9963' }),
  ])
  assert.deepEqual(errors(r), [])
  const nil = (r.json!.nil as { inv: Record<string, unknown>[] }).inv
  assert.deepEqual(nil.find(n => n.sply_ty === 'INTRAB2C'), { sply_ty: 'INTRAB2C', expt_amt: 0, nil_amt: 5000, ngsup_amt: 0 })
  assert.deepEqual(nil.find(n => n.sply_ty === 'INTRB2B'), { sply_ty: 'INTRB2B', expt_amt: 2000, nil_amt: 0, ngsup_amt: 0 })
})

test('HSN: required for B2B, 6 digits above Rs 5 crore, services use UQC NA', () => {
  assert.ok(errors(run([line({ customerGstin: KL_CUST, hsn: '' })])).some(m => m.includes('HSN/SAC is required')))
  assert.ok(errors(run([line({ hsn: '0904' })], true)).some(m => m.includes('at least 6 digits')))
  const r = run([line({ hsn: '998231', uqc: 'NOS', quantity: 3 })])
  const h = (r.json!.hsn as { hsn_b2c: Record<string, unknown>[] }).hsn_b2c[0]
  assert.equal(h.uqc, 'NA')
  assert.equal(h.qty, 0)
})

test('document checks: future date, bad number, same rate split across rows, disagreeing rows', () => {
  assert.ok(errors(run([line({ docDate: '2026-11-01' })])).some(m => m.includes('after the end')))
  assert.ok(run([line({ docDate: '2026-09-30' })]).issues.some(i => i.severity === 'warning' && i.message.includes('before the return period')))
  assert.ok(errors(run([line({ docNumber: 'INV#1' })])).some(m => m.includes('only letters')))
  assert.ok(errors(run([line({ docNumber: 'ABCDEFGHIJKLMNOPQ' })])).some(m => m.includes('16 characters')))
  const r = run([line({ customerGstin: KL_CUST }), line({ row: 3, customerGstin: KA_CUST, igst: 180, cgst: 0, sgst: 0 })])
  assert.ok(errors(r).some(m => m.includes('disagree')))
})

test('client GSTIN must be valid; output carries gstin and period', () => {
  const bad = buildGstr1({ gstin: '32AABCK1234M1Z0', period: '102026', lines: [], aatoAbove5Cr: false })
  if (!gstinCheckDigitOk('32AABCK1234M1Z0')) assert.ok(bad.issues.some(i => i.message.includes('not valid')))
  const ok = run([line({})])
  assert.equal(ok.json!.gstin, CLIENT)
  assert.equal(ok.json!.fp, '102026')
})

test('missing required columns gives one clear message', () => {
  const { issues } = parseSalesRegister('Invoice No,Amount\nA1,100')
  assert.equal(issues.length, 1)
  assert.match(issues[0].message, /Document Date, Rate, Taxable Value/)
})

test('Table 13: series by prefix, gaps counted as cancelled with a warning, notes separate', () => {
  const warnings: string[] = []
  const out = documentSeries([
    { kind: 'INV', number: 'A/001' }, { kind: 'INV', number: 'A/002' }, { kind: 'INV', number: 'A/005' },
    { kind: 'INV', number: 'EXP-12' }, { kind: 'CRN', number: 'CN-1' }, { kind: 'CRN', number: 'CN-2' },
  ], [{ row: 9, kind: 'INV', docNumber: 'A/004' }], m => warnings.push(m))
  assert.deepEqual(out.map(d => [d.nature, d.from, d.to, d.total, d.cancelled, d.net]), [
    [1, 'A/001', 'A/005', 5, 2, 3],
    [1, 'EXP-12', 'EXP-12', 1, 0, 1],
    [5, 'CN-1', 'CN-2', 2, 0, 2],
  ])
  assert.equal(warnings.length, 1)
  assert.match(warnings[0], /A\/003/)
})

test('Table 13 in the JSON', () => {
  const r = run([line({ docNumber: 'INV-1' }), line({ docNumber: 'INV-2' })])
  assert.deepEqual(r.json!.doc_issue, { doc_det: [{ doc_num: 1, docs: [{ num: 1, from: 'INV-1', to: 'INV-2', totnum: 2, cancel: 0, net_issue: 2 }] }] })
})

test('CDNUR: export notes and notes on B2C large invoices; small ones still net in B2CS', () => {
  const r = run([
    line({ docNumber: 'EC-1', docKind: 'CRN', exportType: 'WOPAY', cgst: 0, sgst: 0, igst: 0 }),
    line({ docNumber: 'BC-1', docKind: 'CRN', placeOfSupply: '29', originalInvoiceValue: 150000, taxableValue: 5000, cgst: 0, sgst: 0, igst: 900 }),
    line({ docNumber: 'S-1', placeOfSupply: '29', taxableValue: 2000, cgst: 0, sgst: 0, igst: 360 }),
    line({ docNumber: 'SC-1', docKind: 'CRN', placeOfSupply: '29', originalInvoiceValue: 2360, taxableValue: 500, cgst: 0, sgst: 0, igst: 90 }),
  ])
  assert.deepEqual(errors(r), [])
  const cdnur = r.json!.cdnur as Record<string, unknown>[]
  assert.deepEqual(cdnur.map(n => [n.typ, n.nt_num, n.pos]), [['EXPWOP', 'EC-1', undefined], ['B2CL', 'BC-1', '29']])
  assert.deepEqual(r.json!.b2cs, [{ sply_ty: 'INTER', pos: '29', typ: 'OE', rt: 18, txval: 1500, iamt: 270, csamt: 0 }])
})

test('amendments: B2BA and CDNRA carry the original number and date; kept out of HSN and Table 13', () => {
  const r = run([
    line({ docNumber: 'INV-9', customerGstin: KL_CUST, originalDocNumber: 'INV-7', originalDocDate: '2026-08-14' }),
    line({ docNumber: 'CN-9', docKind: 'CRN', customerGstin: KL_CUST, originalDocNumber: 'CN-3', originalDocDate: '2026-09-02' }),
  ])
  assert.deepEqual(errors(r), [])
  const b2ba = (r.json!.b2ba as { ctin: string; inv: Record<string, unknown>[] }[])[0]
  assert.equal(b2ba.inv[0].oinum, 'INV-7')
  assert.equal(b2ba.inv[0].oidt, '14-08-2026')
  const nt = (r.json!.cdnra as { nt: Record<string, unknown>[] }[])[0].nt[0]
  assert.equal(nt.ont_num, 'CN-3')
  assert.equal(r.json!.hsn, undefined)
  assert.equal(r.json!.doc_issue, undefined)
  assert.equal(r.json!.b2b, undefined)
})

test('amendment checks: original in this period, missing date, B2C small amendment', () => {
  assert.ok(errors(run([line({ customerGstin: KL_CUST, originalDocNumber: 'X-1', originalDocDate: '2026-10-01' })])).some(m => m.includes('Correct the document itself')))
  assert.ok(errors(run([line({ customerGstin: KL_CUST, originalDocNumber: 'X-1' })])).some(m => m.includes('Original Document Date')))
  assert.ok(errors(run([line({ originalDocNumber: 'X-1', originalDocDate: '2026-08-01' })])).some(m => m.includes('B2CSA')))
})

test('cancelled rows: "Cancelled Credit Note" goes to the credit note series', () => {
  const { lines, cancelled, issues } = parseSalesRegister([
    'Document Type,Document Number,Document Date,Rate,Taxable Value,CGST,SGST,HSN/SAC',
    'Invoice,T-1,05-10-2026,18,1000,90,90,090411',
    'Cancelled,T-2,,,,,,',
    'Cancelled Credit Note,C-5,,,,,,',
  ].join('\n'))
  assert.deepEqual(issues, [])
  assert.equal(lines.length, 1)
  assert.deepEqual(cancelled.map(c => [c.kind, c.docNumber]), [['INV', 'T-2'], ['CRN', 'C-5']])
})
