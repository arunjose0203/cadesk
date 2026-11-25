/*
 * GSTR-3B pre-filing check (Sprint 66).
 *
 * Since the July 2025 tax period the outward liability in GSTR-3B (Table 3) is
 * auto-filled from GSTR-1/1A and cannot be edited; mistakes are fixed through
 * GSTR-1A. So this is not an upload file. It works out what GSTR-3B should show,
 * so the firm can compare it with the portal's draft before filing:
 *
 * - Table 3.1 and 3.2 from the same sales register used for GSTR-1
 * - Table 4 ITC from the GSTR-2B matching batch (credit notes netted in 4A(5),
 *   negative values allowed, as on the portal since Sept 2022)
 * - Set-off of ITC against liability (s.49(5) CGST Act, Rule 88A) and cash payable
 * - Interest u/s 50(1) at 18% a year on tax paid in cash after the due date
 *
 * Pure functions; tests in web/tests/gstr3b.test.ts.
 */
import { STATE_CODES, type SalesLine } from './gstr1.ts'

export type Heads = { igst: number; cgst: number; sgst: number; cess: number }
export type Row = Heads & { txval: number }
export const HEADS = ['igst', 'cgst', 'sgst', 'cess'] as const
export type Head = (typeof HEADS)[number]

const r2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100
export const noTax = (): Heads => ({ igst: 0, cgst: 0, sgst: 0, cess: 0 })
const noRow = (): Row => ({ txval: 0, ...noTax() })
const roundHeads = <T extends Heads>(h: T): T => {
  const out = { ...h }
  for (const k of Object.keys(out) as (keyof T)[]) out[k] = r2(out[k] as number) as T[keyof T]
  return out
}
export const sumHeads = (...hs: Heads[]): Heads => {
  const t = noTax()
  for (const h of hs) for (const k of HEADS) t[k] += h[k]
  return roundHeads(t)
}
const minus = (a: Heads, b: Heads): Heads => roundHeads({ igst: a.igst - b.igst, cgst: a.cgst - b.cgst, sgst: a.sgst - b.sgst, cess: a.cess - b.cess })

// ---------------------------------------------------------------------------
// Table 3: from the sales register

export type Outward = {
  /** 3.1(a) outward taxable supplies other than zero-rated, nil and exempt */
  a: Row
  /** 3.1(b) zero-rated: exports and supplies to SEZ */
  b: Row
  /** 3.1(c) nil-rated and exempt */
  c: number
  /** 3.1(e) non-GST */
  e: number
  /** 3.2 inter-state supplies to unregistered persons, by place of supply */
  interUnregistered: { pos: string; state: string; txval: number; igst: number }[]
  /** Supplies where the customer pays tax under reverse charge: no tax for the client in 3.1(a). */
  reverseChargeOutward: { documents: number; txval: number }
  /** Amendments of earlier documents: only the change from the original belongs in 3.1, and the original is not in this register. */
  amendedDocuments: number
}

export function outwardFromSales(gstin: string, lines: SalesLine[]): Outward {
  const supplierState = gstin.slice(0, 2)
  const a = noRow(), b = noRow()
  let c = 0, e = 0
  const unreg = new Map<string, { txval: number; igst: number }>()
  const rcmDocs = new Set<string>()
  const amended = new Set<string>()
  let rcmTx = 0

  for (const l of lines) {
    const sign = l.docKind === 'CRN' ? -1 : 1
    if (l.originalDocNumber) { amended.add(`${l.docKind}|${l.docNumber.toUpperCase()}`); continue }
    if (l.category === 'NIL' || l.category === 'EXEMPT') { c += sign * l.taxableValue; continue }
    if (l.category === 'NONGST') { e += sign * l.taxableValue; continue }
    const isExport = l.exportType !== ''
    const pos = isExport ? '96' : (l.placeOfSupply || (l.customerGstin ? l.customerGstin.slice(0, 2) : supplierState))
    const add = (row: Row) => {
      row.txval += sign * l.taxableValue; row.igst += sign * l.igst; row.cgst += sign * l.cgst
      row.sgst += sign * l.sgst; row.cess += sign * l.cess
    }
    if (isExport || l.invoiceType === 'SEWP' || l.invoiceType === 'SEWOP') { add(b); continue }
    if (l.reverseCharge && l.customerGstin) {
      rcmDocs.add(`${l.docKind}|${l.docNumber.toUpperCase()}`)
      rcmTx += sign * l.taxableValue
      continue
    }
    add(a)
    if (!l.customerGstin && pos !== supplierState) {
      const u = unreg.get(pos) ?? { txval: 0, igst: 0 }
      u.txval += sign * l.taxableValue
      u.igst += sign * l.igst
      unreg.set(pos, u)
    }
  }
  return {
    a: roundHeadsRow(a), b: roundHeadsRow(b), c: r2(c), e: r2(e),
    interUnregistered: [...unreg.entries()]
      .sort((x, y) => x[0].localeCompare(y[0]))
      .map(([pos, u]) => ({ pos, state: STATE_CODES[pos] ?? pos, txval: r2(u.txval), igst: r2(u.igst) })),
    reverseChargeOutward: { documents: rcmDocs.size, txval: r2(rcmTx) },
    amendedDocuments: amended.size,
  }
}
const roundHeadsRow = (r: Row): Row => ({ ...roundHeads(r), txval: r2(r.txval) })

// ---------------------------------------------------------------------------
// Table 4: from the GSTR-2B matching batch

/** The fields of a reconciliation item this check needs (GstReconciliationItemDto). */
export type ReconItem = {
  supplierGstin: string
  supplierName?: string | null
  invoiceNumber: string
  invoiceType: string // regular | debit_note | credit_note | sez
  matchStatus: string // exact_match | match_with_tolerance | missing_in_2b | missing_in_books | tax_mismatch | date_mismatch
  itcEligibility: string // eligible | ineligible_17_5 | pos_mismatch | supplier_cancelled
  prCgst: number; prSgst: number; prIgst: number; prCess: number
  gstr2BCgst: number; gstr2BSgst: number; gstr2BIgst: number; gstr2BCess: number
}

export type ManualItc = {
  /** 4A(1) import of goods (ICEGATE / Table 3 Part A Section IV of 2B) */
  importGoods: Heads
  /** 4A(2) import of services, tax paid under reverse charge */
  importServices: Heads
  /** 4A(3) other inward supplies under reverse charge */
  reverseCharge: Heads
  /** 4A(4) from an Input Service Distributor */
  isd: Heads
  /** 4B(1) reversals under Rules 38, 42, 43 (on top of the s.17(5) amounts found in the batch) */
  reversalRules: Heads
  /** 4B(2) other reversals */
  reversalOther: Heads
  /** 4D(1) ITC reclaimed that was reversed in 4B(2) earlier */
  reclaimed: Heads
}
export const emptyManualItc = (): ManualItc => ({
  importGoods: noTax(), importServices: noTax(), reverseCharge: noTax(), isd: noTax(),
  reversalRules: noTax(), reversalOther: noTax(), reclaimed: noTax(),
})

export type Itc = {
  available: { importGoods: Heads; importServices: Heads; reverseCharge: Heads; isd: Heads; allOther: Heads; total: Heads }
  reversed: { rules: Heads; blocked17_5: Heads; other: Heads; total: Heads }
  net: Heads // 4C
  other: { reclaimed: Heads; ineligiblePos: Heads } // 4D(1), 4D(2)
  /** Not claimable this month; listed so the firm can follow up with suppliers. */
  heldBack: {
    notIn2B: { count: number; tax: Heads }
    notInBooks: { count: number; tax: Heads }
    supplierCancelled: { count: number; tax: Heads }
    loweredToBooks: { count: number; tax: Heads }
  }
}

const tax2B = (i: ReconItem): Heads => ({ igst: i.gstr2BIgst, cgst: i.gstr2BCgst, sgst: i.gstr2BSgst, cess: i.gstr2BCess })
const taxPR = (i: ReconItem): Heads => ({ igst: i.prIgst, cgst: i.prCgst, sgst: i.prSgst, cess: i.prCess })
const scale = (h: Heads, s: number): Heads => ({ igst: h.igst * s, cgst: h.cgst * s, sgst: h.sgst * s, cess: h.cess * s })
const addInto = (t: Heads, h: Heads) => { for (const k of HEADS) t[k] += h[k] }

export function itcFrom2B(items: ReconItem[], manual: ManualItc): Itc {
  const allOther = noTax(), blocked = noTax(), pos = noTax()
  const held = {
    notIn2B: { count: 0, tax: noTax() }, notInBooks: { count: 0, tax: noTax() },
    supplierCancelled: { count: 0, tax: noTax() }, loweredToBooks: { count: 0, tax: noTax() },
  }
  for (const i of items) {
    const sign = i.invoiceType === 'credit_note' ? -1 : 1
    if (i.matchStatus === 'missing_in_2b') {
      // Rule 36(4): only ITC shown in GSTR-2B can be claimed.
      held.notIn2B.count++; addInto(held.notIn2B.tax, scale(taxPR(i), sign)); continue
    }
    if (i.matchStatus === 'missing_in_books') {
      // s.16(2): the invoice must be in the client's books before ITC is taken.
      held.notInBooks.count++; addInto(held.notInBooks.tax, scale(tax2B(i), sign)); continue
    }
    if (i.itcEligibility === 'supplier_cancelled') {
      held.supplierCancelled.count++; addInto(held.supplierCancelled.tax, scale(tax2B(i), sign)); continue
    }
    // Claim what 2B shows, but never more than the books, head by head.
    const b = tax2B(i), p = taxPR(i)
    const claim: Heads = { igst: Math.min(b.igst, p.igst), cgst: Math.min(b.cgst, p.cgst), sgst: Math.min(b.sgst, p.sgst), cess: Math.min(b.cess, p.cess) }
    const cut = minus(b, claim)
    if (HEADS.some(k => cut[k] > 0.005)) { held.loweredToBooks.count++; addInto(held.loweredToBooks.tax, scale(cut, sign)) }
    if (i.itcEligibility === 'pos_mismatch') { addInto(pos, scale(claim, sign)); continue }
    addInto(allOther, scale(claim, sign))
    if (i.itcEligibility === 'ineligible_17_5') addInto(blocked, scale(claim, sign))
  }

  const avlTotal = sumHeads(manual.importGoods, manual.importServices, manual.reverseCharge, manual.isd, allOther)
  const revTotal = sumHeads(manual.reversalRules, blocked, manual.reversalOther)
  return {
    available: {
      importGoods: roundHeads(manual.importGoods), importServices: roundHeads(manual.importServices),
      reverseCharge: roundHeads(manual.reverseCharge), isd: roundHeads(manual.isd), allOther: roundHeads(allOther), total: avlTotal,
    },
    // 4B(1) holds both Rule 38/42/43 and s.17(5) reversals; kept apart here for review.
    reversed: { rules: roundHeads(manual.reversalRules), blocked17_5: roundHeads(blocked), other: roundHeads(manual.reversalOther), total: revTotal },
    net: minus(avlTotal, revTotal),
    other: { reclaimed: roundHeads(manual.reclaimed), ineligiblePos: roundHeads(pos) },
    heldBack: {
      notIn2B: { count: held.notIn2B.count, tax: roundHeads(held.notIn2B.tax) },
      notInBooks: { count: held.notInBooks.count, tax: roundHeads(held.notInBooks.tax) },
      supplierCancelled: { count: held.supplierCancelled.count, tax: roundHeads(held.supplierCancelled.tax) },
      loweredToBooks: { count: held.loweredToBooks.count, tax: roundHeads(held.loweredToBooks.tax) },
    },
  }
}

// ---------------------------------------------------------------------------
// Set-off (Table 6.1)

export type SetOffStep = { from: Head; to: Head; amount: number }
export type SetOff = {
  liability: Heads
  credit: Heads
  steps: SetOffStep[]
  paidByCredit: Heads // by liability head
  cash: Heads // by liability head, before RCM
  rcmCash: Heads // 3.1(d): always in cash
  totalCash: Heads
  carryForward: Heads
}

/**
 * Order of use, s.49(5) CGST Act and Rule 88A:
 * 1. IGST credit on IGST, then on CGST and SGST in any proportion, and it must be
 *    used up before CGST or SGST credit is touched. We give it first to whichever
 *    head its own credit cannot cover, which keeps the cash payment lowest.
 * 2. CGST credit on CGST, then IGST. Never on SGST.
 * 3. SGST credit on SGST, then IGST (only once CGST credit for IGST is used up). Never on CGST.
 * 4. Cess credit on cess only.
 * A negative net ITC (Table 4C) is added to the liability of that head.
 */
export function setOff(outwardTax: Heads, netItc: Heads, rcm: Heads): SetOff {
  const L = roundHeads({ ...outwardTax })
  const C = noTax()
  for (const k of HEADS) {
    if (netItc[k] < 0) L[k] = r2(L[k] - netItc[k])
    else C[k] = r2(netItc[k])
  }
  const liability = { ...L }, credit = { ...C }
  const steps: SetOffStep[] = []
  const apply = (from: Head, to: Head, want: number) => {
    const amt = r2(Math.max(0, Math.min(want, C[from], L[to])))
    if (amt <= 0) return
    C[from] = r2(C[from] - amt); L[to] = r2(L[to] - amt)
    const last = steps.find(s => s.from === from && s.to === to)
    if (last) last.amount = r2(last.amount + amt)
    else steps.push({ from, to, amount: amt })
  }

  apply('igst', 'igst', Infinity)
  const shortC = Math.max(0, L.cgst - C.cgst), shortS = Math.max(0, L.sgst - C.sgst)
  const gap = shortC + shortS
  if (gap > 0 && C.igst > 0) {
    const pool = Math.min(C.igst, gap)
    const toC = r2(pool * shortC / gap)
    apply('igst', 'cgst', toC)
    apply('igst', 'sgst', r2(pool - toC))
  }
  // Rule 88A: IGST credit must be used up before CGST/SGST credit. Split what is left
  // in proportion to the remaining CGST and SGST liability, so the CGST and SGST
  // balances carried forward stay even (they can never be used against each other).
  const restC = L.cgst, restS = L.sgst
  if (C.igst > 0 && restC + restS > 0) {
    const pool = Math.min(C.igst, restC + restS)
    const toC = r2(pool * restC / (restC + restS))
    apply('igst', 'cgst', toC)
    apply('igst', 'sgst', r2(pool - toC))
    apply('igst', 'cgst', Infinity) // rounding remainder
  }
  apply('cgst', 'cgst', Infinity)
  apply('cgst', 'igst', Infinity)
  apply('sgst', 'sgst', Infinity)
  apply('sgst', 'igst', Infinity)
  apply('cess', 'cess', Infinity)

  const cash = roundHeads({ ...L })
  const rcmCash = roundHeads({ igst: Math.max(0, rcm.igst), cgst: Math.max(0, rcm.cgst), sgst: Math.max(0, rcm.sgst), cess: Math.max(0, rcm.cess) })
  return {
    liability, credit, steps,
    paidByCredit: minus(liability, cash),
    cash, rcmCash, totalCash: sumHeads(cash, rcmCash),
    carryForward: roundHeads({ ...C }),
  }
}

// ---------------------------------------------------------------------------
// Due date, interest and late fee

/** QRMP returns are due on the 22nd for these states/UTs (principal place of business), the 24th for the rest. */
export const QRMP_22ND_STATES = new Set(['22', '23', '24', '25', '26', '27', '29', '30', '31', '32', '33', '34', '35', '36', '37'])

export type Frequency = 'monthly' | 'qrmp'

/** Monthly filers: 20th of the next month. QRMP filers: 22nd or 24th of the month after the quarter, by state. */
export function dueDate(period: string, frequency: Frequency = 'monthly', stateCode = ''): string {
  const m = Number(period.slice(0, 2)), y = Number(period.slice(2))
  const nm = m === 12 ? 1 : m + 1, ny = m === 12 ? y + 1 : y
  const day = frequency === 'monthly' ? 20 : (QRMP_22ND_STATES.has(stateCode) ? 22 : 24)
  return `${ny}-${String(nm).padStart(2, '0')}-${day}`
}
export const monthlyDueDate = (period: string) => dueDate(period, 'monthly')
export const isQuarterEnd = (period: string) => ['03', '06', '09', '12'].includes(period.slice(0, 2))

/** s.50(1): 18% a year on tax paid in cash, for each day after the due date (on net cash liability since 1 Sept 2020). */
export function lateInterest(cashTax: Heads, dueDate: string, paidOn: string): { days: number; interest: Heads } {
  const days = Math.max(0, Math.round((Date.parse(paidOn) - Date.parse(dueDate)) / 86_400_000))
  const f = (x: number) => r2(Math.max(0, x) * 0.18 * days / 365)
  return { days, interest: { igst: f(cashTax.igst), cgst: f(cashTax.cgst), sgst: f(cashTax.sgst), cess: f(cashTax.cess) } }
}

export type TurnoverBand = 'upto1.5cr' | '1.5to5cr' | 'above5cr'

/**
 * s.47 late fee for GSTR-3B: Rs 25 CGST + Rs 25 SGST a day (Rs 10 + Rs 10 for a nil return),
 * capped per return by Notification 19/2021-CT: nil Rs 250 + 250; turnover up to Rs 1.5 cr
 * Rs 1,000 + 1,000; Rs 1.5–5 cr Rs 2,500 + 2,500; above Rs 5 cr Rs 5,000 + 5,000.
 */
export function lateFee(dueDate: string, filedOn: string, nilReturn: boolean, band: TurnoverBand): { days: number; cgst: number; sgst: number } {
  const days = Math.max(0, Math.round((Date.parse(filedOn) - Date.parse(dueDate)) / 86_400_000))
  const perDay = nilReturn ? 10 : 25
  const cap = nilReturn ? 250 : band === 'upto1.5cr' ? 1000 : band === '1.5to5cr' ? 2500 : 5000
  const each = Math.min(days * perDay, cap)
  return { days, cgst: each, sgst: each }
}
