/*
 * GSTR-9 annual working from a year's sales register (Sprint 66).
 *
 * Works out Table 4 (supplies on which tax is payable), Table 5 (supplies on which
 * tax is not payable) and Table 17 (HSN summary of outward supplies) from the same
 * sales register format as the monthly GSTR-1 desk, for the whole financial year.
 * The portal pre-fills GSTR-9 from GSTR-1 and GSTR-3B; this is what the firm checks
 * those figures against before filing.
 *
 * gstr9Json() writes Tables 4, 5 and 17 in the GSTN GSTR-9 JSON format (the same keys as
 * the GSTR-9 save API: table4.b2c … sup_adv, table5.zero_rtd … total_tover, table17.items).
 * The firm imports it into the GSTR-9 offline tool, completes Tables 6–19 there from the
 * portal's figures, and files. 4F (advances) and 4G (inward reverse charge) are not in a
 * sales register, so they are entered on the desk.
 *
 * Pure functions; tests in web/tests/gstr9.test.ts.
 */
import { UQC_CODES, type Issue, type SalesLine } from './gstr1.ts'

export type Amount = { txval: number; igst: number; cgst: number; sgst: number; cess: number }
export type TableRow = { code: string; label: string } & Amount

const r2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100
const none = (): Amount => ({ txval: 0, igst: 0, cgst: 0, sgst: 0, cess: 0 })
const add = (a: Amount, l: SalesLine, sign = 1) => {
  a.txval += sign * l.taxableValue; a.igst += sign * l.igst; a.cgst += sign * l.cgst; a.sgst += sign * l.sgst; a.cess += sign * l.cess
}
const round = (a: Amount): Amount => ({ txval: r2(a.txval), igst: r2(a.igst), cgst: r2(a.cgst), sgst: r2(a.sgst), cess: r2(a.cess) })
const combine = (parts: [Amount, number][]): Amount => {
  const t = none()
  for (const [a, s] of parts) { t.txval += s * a.txval; t.igst += s * a.igst; t.cgst += s * a.cgst; t.sgst += s * a.sgst; t.cess += s * a.cess }
  return round(t)
}

/** "2025-26" → 1 Apr 2025 to 31 Mar 2026. */
export function fyBounds(fy: string): { start: string; end: string } | null {
  const m = /^(\d{4})-(\d{2})$/.exec(fy)
  if (!m) return null
  const y = Number(m[1])
  if ((y + 1) % 100 !== Number(m[2])) return null
  return { start: `${y}-04-01`, end: `${y + 1}-03-31` }
}

export type HsnRow = { hsn: string; description: string; uqc: string; quantity: number; rate: number } & Amount

/** Figures a sales register does not hold: 4F advances (tax paid, no invoice yet) and 4G inward reverse charge. */
export type AnnualEntries = { advances: Amount; inwardRcm: Amount }
export const emptyAnnualEntries = (): AnnualEntries => ({ advances: none(), inwardRcm: none() })

export type Gstr9Working = {
  table4: TableRow[]
  table5: TableRow[]
  table17: HsnRow[]
  totalTurnover: number // 5N, before 4G (entered on the portal)
  issues: Issue[]
}

export function buildGstr9Working(fy: string, lines: SalesLine[], entries: AnnualEntries = emptyAnnualEntries()): Gstr9Working {
  const issues: Issue[] = []
  const bounds = fyBounds(fy)
  if (!bounds) return { table4: [], table5: [], table17: [], totalTurnover: 0, issues: [{ severity: 'error', message: `Financial year "${fy}" should look like 2025-26.` }] }
  const t4 = { A: none(), B: none(), C: none(), D: none(), E: none(), I: none(), J: none() }
  const t5 = { A: none(), B: none(), C: none(), D: none(), E: none(), F: none(), H: none(), I: none() }
  const hsn = new Map<string, HsnRow>()
  const amended = new Set<string>()
  const outside = new Set<string>()

  for (const l of lines) {
    const docKey = `${l.docKind}|${l.docNumber.toUpperCase()}`
    if (l.originalDocNumber) { amended.add(docKey); continue }
    if (l.docDate < bounds.start || l.docDate > bounds.end) {
      if (!outside.has(docKey)) issues.push({ severity: 'error', row: l.row, doc: l.docNumber, message: `Dated outside the financial year ${fy}.` })
      outside.add(docKey)
      continue
    }
    const note = l.docKind !== 'INV'
    const registered = l.customerGstin !== '' && l.exportType === ''
    const withoutTax = l.exportType === 'WOPAY' || l.invoiceType === 'SEWOP'
    const rcmOutward = registered && l.reverseCharge

    if (l.category !== 'TAXABLE' || withoutTax || rcmOutward) {
      // Table 5: tax not payable by the client
      if (note) add(l.docKind === 'CRN' ? t5.H : t5.I, l)
      else if (l.category === 'EXEMPT') add(t5.D, l)
      else if (l.category === 'NIL') add(t5.E, l)
      else if (l.category === 'NONGST') add(t5.F, l)
      else if (l.exportType === 'WOPAY') add(t5.A, l)
      else if (l.invoiceType === 'SEWOP') add(t5.B, l)
      else add(t5.C, l) // reverse charge: the recipient pays
    } else {
      // Table 4: tax payable by the client
      if (note) add(l.docKind === 'CRN' ? t4.I : t4.J, l)
      else if (l.exportType === 'WPAY') add(t4.C, l)
      else if (l.invoiceType === 'SEWP') add(t4.D, l)
      else if (l.invoiceType === 'DE') add(t4.E, l)
      else if (registered) add(t4.B, l)
      else add(t4.A, l)
    }

    if (l.hsn && l.category !== 'NONGST') {
      const service = l.hsn.startsWith('99')
      const uqc = service ? 'NA' : (UQC_CODES.has(l.uqc) && l.uqc !== 'NA' ? l.uqc : 'OTH')
      const k = `${l.hsn}|${l.rate}|${uqc}`
      const h = hsn.get(k) ?? { hsn: l.hsn, description: l.description.slice(0, 30), uqc, quantity: 0, rate: l.rate, ...none() }
      const sign = l.docKind === 'CRN' ? -1 : 1
      h.quantity += service ? 0 : sign * l.quantity
      add(h, l, sign)
      hsn.set(k, h)
    }
  }
  if (amended.size) {
    issues.push({ severity: 'warning', message: `${amended.size} amended document${amended.size === 1 ? ' is' : 's are'} left out. Report amendments of this year's supplies in Table 4K/4L and 5J/5K with the difference from the original.` })
  }

  const F4 = round(entries.advances), G4 = round(entries.inwardRcm)
  const zeroA = none()
  // 4H = A to G; 4M = J + K − I − L; 4N = H + M. Amendments (K, L) are not in a sales register.
  const H4 = combine([[t4.A, 1], [t4.B, 1], [t4.C, 1], [t4.D, 1], [t4.E, 1], [F4, 1], [G4, 1]])
  const M4 = combine([[t4.J, 1], [zeroA, 1], [t4.I, -1], [zeroA, -1]])
  const N4 = combine([[H4, 1], [M4, 1]])
  // 5G = A to F; 5L = I + J − H − K; 5M = G + L; 5N = 4N + 5M − 4G.
  const G5 = combine([[t5.A, 1], [t5.B, 1], [t5.C, 1], [t5.D, 1], [t5.E, 1], [t5.F, 1]])
  const L5 = combine([[t5.I, 1], [zeroA, 1], [t5.H, -1], [zeroA, -1]])
  const M5 = combine([[G5, 1], [L5, 1]])
  const N5 = combine([[N4, 1], [M5, 1], [G4, -1]])
  const row = (code: string, label: string, a: Amount): TableRow => ({ code, label, ...round(a) })

  const table4 = [
    row('4A', 'Supplies to unregistered persons (B2C)', t4.A),
    row('4B', 'Supplies to registered persons (B2B)', t4.B),
    row('4C', 'Zero-rated supply (export) on payment of tax', t4.C),
    row('4D', 'Supply to SEZs on payment of tax', t4.D),
    row('4E', 'Deemed exports', t4.E),
    row('4F', 'Advances on which tax was paid but invoice not issued', F4),
    row('4G', 'Inward supplies on which tax is paid on reverse charge', G4),
    row('4H', 'Sub-total (A to G)', H4),
    row('4I', 'Credit notes issued in respect of A to F', t4.I),
    row('4J', 'Debit notes issued in respect of A to F', t4.J),
    row('4K', 'Supplies declared through amendments (+)', zeroA),
    row('4L', 'Supplies reduced through amendments (−)', zeroA),
    row('4M', 'Sub-total (I to L)', M4),
    row('4N', 'Supplies and advances on which tax is to be paid (H + M)', N4),
  ]
  const table5 = [
    row('5A', 'Zero-rated supply (export) without payment of tax', t5.A),
    row('5B', 'Supply to SEZs without payment of tax', t5.B),
    row('5C', 'Supplies on which the recipient pays tax on reverse charge', t5.C),
    row('5D', 'Exempted', t5.D),
    row('5E', 'Nil-rated', t5.E),
    row('5F', 'Non-GST supply', t5.F),
    row('5G', 'Sub-total (A to F)', G5),
    row('5H', 'Credit notes issued in respect of A to F', t5.H),
    row('5I', 'Debit notes issued in respect of A to F', t5.I),
    row('5J', 'Supplies declared through amendments (+)', zeroA),
    row('5K', 'Supplies reduced through amendments (−)', zeroA),
    row('5L', 'Sub-total (H to K)', L5),
    row('5M', 'Supplies on which tax is not to be paid (G + L)', M5),
    row('5N', 'Total turnover including advances (4N + 5M − 4G)', N5),
  ]
  const table17 = [...hsn.values()]
    .map(h => ({ ...h, quantity: r2(h.quantity), ...round(h) }))
    .sort((a, b) => a.hsn.localeCompare(b.hsn) || a.rate - b.rate)

  issues.sort((a, b) => (a.severity === b.severity ? (a.row ?? 0) - (b.row ?? 0) : a.severity === 'error' ? -1 : 1))
  return { table4, table5, table17, totalTurnover: r2(N5.txval), issues }
}

/** The working as CSV, for the file and for review in Excel. */
export function gstr9WorkingCsv(w: Gstr9Working, gstin: string, fy: string): string {
  const esc = (v: string | number) => { const s = String(v); return /[",\n]/.test(s) ? `"${s.replace(/"/g, '""')}"` : s }
  const out: (string | number)[][] = [[`GSTR-9 working for ${gstin}, FY ${fy}`], []]
  out.push(['Table', 'Details', 'Taxable value', 'IGST', 'CGST', 'SGST', 'Cess'])
  for (const r of [...w.table4, ...w.table5]) out.push([r.code, r.label, r.txval, r.igst, r.cgst, r.sgst, r.cess])
  out.push([], ['Table 17: HSN summary of outward supplies'], ['HSN', 'Description', 'UQC', 'Quantity', 'Rate', 'Taxable value', 'IGST', 'CGST', 'SGST', 'Cess'])
  for (const h of w.table17) out.push([h.hsn, h.description, h.uqc, h.quantity, h.rate, h.txval, h.igst, h.cgst, h.sgst, h.cess])
  return out.map(r => r.map(esc).join(',')).join('\r\n') + '\r\n'
}

/**
 * GSTN GSTR-9 JSON for Tables 4, 5 and 17. fp is March of the year ("032026" for 2025-26).
 * Keys follow the GSTR-9 save API. Exports and SEZ supplies carry only IGST and cess;
 * every Table 5 entry carries only the taxable value.
 */
export function gstr9Json(w: Gstr9Working, gstin: string, fy: string): Record<string, unknown> {
  const endYear = Number(fy.slice(0, 4)) + 1
  const get = (rows: TableRow[], code: string) => rows.find(r => r.code === code)!
  const all = (r: TableRow) => ({ txval: r.txval, iamt: r.igst, camt: r.cgst, samt: r.sgst, csamt: r.cess })
  const igstOnly = (r: TableRow) => ({ txval: r.txval, iamt: r.igst, csamt: r.cess })
  const t4 = (code: string) => get(w.table4, code)
  const t5 = (code: string) => ({ txval: get(w.table5, code).txval })
  return {
    gstin,
    fp: `03${endYear}`,
    isnil: w.table4.every(r => r.txval === 0) && w.table5.every(r => r.txval === 0) ? 'Y' : 'N',
    table4: {
      b2c: all(t4('4A')), b2b: all(t4('4B')), exp: igstOnly(t4('4C')), sez: igstOnly(t4('4D')), deemed: all(t4('4E')),
      at: all(t4('4F')), rchrg: all(t4('4G')), cr_nt: all(t4('4I')), dr_nt: all(t4('4J')),
      amd_pos: all(t4('4K')), amd_neg: all(t4('4L')), sub_totalAG: all(t4('4H')), sub_totalIL: all(t4('4M')), sup_adv: all(t4('4N')),
    },
    table5: {
      zero_rtd: t5('5A'), sez: t5('5B'), rchrg: t5('5C'), exmt: t5('5D'), nil: t5('5E'), non_gst: t5('5F'),
      cr_nt: t5('5H'), dr_nt: t5('5I'), amd_pos: t5('5J'), amd_neg: t5('5K'),
      sub_totalAF: t5('5G'), sub_totalHK: t5('5L'), tover_tax_np: t5('5M'), total_tover: t5('5N'),
    },
    table17: {
      items: w.table17.map(h => ({
        hsn_sc: h.hsn, uqc: h.uqc, desc: h.description, isconcesstional: 'N', qty: h.quantity, txval: h.txval,
        rt: h.rate, iamt: h.igst, camt: h.cgst, samt: h.sgst, csamt: h.cess,
      })),
    },
  }
}
