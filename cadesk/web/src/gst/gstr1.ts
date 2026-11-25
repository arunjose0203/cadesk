/*
 * GSTR-1 from a sales register (Sprint 66).
 *
 * Pure functions: no browser APIs, no network. The client's sales data is read
 * and turned into the GSTR-1 JSON in the browser, so it never leaves the
 * firm's machine. Tests: web/tests/gstr1.test.ts (npm test).
 *
 * Rules used, as of Nov 2026:
 * - B2C Large: inter-state supply to an unregistered person with invoice value
 *   above Rs 1,00,000 (Notification 12/2024-CT, from 1 Aug 2024).
 * - Table 12 HSN summary is reported in two parts, B2B and B2C (Phase III,
 *   from the May 2025 tax period).
 * - Credit/debit notes to registered persons (CDNR) need no original invoice.
 *
 * BEFORE RELEASE: confirm the key names marked "SCHEMA" against the current
 * GSTN offline tool by importing a generated file into it.
 */

// SCHEMA: the offline tool writes these two fields; confirm the version string.
export const GSTR1_JSON_VERSION = 'GST3.2.3'
export const B2CL_THRESHOLD = 100000

export type Severity = 'error' | 'warning'
export type Issue = { severity: Severity; row?: number; doc?: string; message: string }

export type DocKind = 'INV' | 'CRN' | 'DBN'
export type SupplyCategory = 'TAXABLE' | 'NIL' | 'EXEMPT' | 'NONGST'
/** R regular, SEWP / SEWOP supply to SEZ with / without payment of tax, DE deemed export */
export type InvoiceType = 'R' | 'SEWP' | 'SEWOP' | 'DE'
export type ExportType = '' | 'WPAY' | 'WOPAY'

export type SalesLine = {
  row: number
  docKind: DocKind
  docNumber: string
  docDate: string // yyyy-mm-dd
  customerGstin: string // '' for unregistered customers
  customerName: string
  placeOfSupply: string // two-digit state code, '' to derive it
  reverseCharge: boolean
  invoiceValue: number
  category: SupplyCategory
  invoiceType: InvoiceType
  exportType: ExportType
  rate: number
  taxableValue: number
  igst: number
  cgst: number
  sgst: number
  cess: number
  hsn: string
  description: string
  uqc: string
  quantity: number
  shippingPort: string
  shippingBillNo: string
  shippingBillDate: string // yyyy-mm-dd or ''
  /** Amendments: the document being amended (filed in an earlier period). */
  originalDocNumber: string
  originalDocDate: string // yyyy-mm-dd, '' or 'invalid'
  /** Notes to unregistered customers: value of the invoice the note relates to (decides B2CL vs B2CS). */
  originalInvoiceValue: number
}

/** A document number marked Cancelled in the register: counted in Table 13 only. */
export type CancelledDoc = { row: number; kind: DocKind; docNumber: string }

export const GST_RATES = [0, 0.1, 0.25, 1, 1.5, 3, 5, 6, 7.5, 12, 18, 28, 40]

export const STATE_CODES: Record<string, string> = {
  '01': 'Jammu and Kashmir', '02': 'Himachal Pradesh', '03': 'Punjab', '04': 'Chandigarh', '05': 'Uttarakhand',
  '06': 'Haryana', '07': 'Delhi', '08': 'Rajasthan', '09': 'Uttar Pradesh', '10': 'Bihar', '11': 'Sikkim',
  '12': 'Arunachal Pradesh', '13': 'Nagaland', '14': 'Manipur', '15': 'Mizoram', '16': 'Tripura', '17': 'Meghalaya',
  '18': 'Assam', '19': 'West Bengal', '20': 'Jharkhand', '21': 'Odisha', '22': 'Chhattisgarh', '23': 'Madhya Pradesh',
  '24': 'Gujarat', '26': 'Dadra and Nagar Haveli and Daman and Diu', '27': 'Maharashtra', '29': 'Karnataka',
  '30': 'Goa', '31': 'Lakshadweep', '32': 'Kerala', '33': 'Tamil Nadu', '34': 'Puducherry',
  '35': 'Andaman and Nicobar Islands', '36': 'Telangana', '37': 'Andhra Pradesh', '38': 'Ladakh',
  '97': 'Other Territory',
}

export const UQC_CODES = new Set([
  'BAG', 'BAL', 'BDL', 'BKL', 'BOU', 'BOX', 'BTL', 'BUN', 'CAN', 'CBM', 'CCM', 'CMS', 'CTN', 'DOZ', 'DRM', 'GGK',
  'GMS', 'GRS', 'GYD', 'KGS', 'KLR', 'KME', 'LTR', 'MLT', 'MTR', 'MTS', 'NOS', 'OTH', 'PAC', 'PCS', 'PRS', 'QTL',
  'ROL', 'SET', 'SQF', 'SQM', 'SQY', 'TBS', 'TGM', 'THD', 'TON', 'TUB', 'UGS', 'UNT', 'YDS', 'NA',
])

const r2 = (n: number) => Math.round((n + Number.EPSILON) * 100) / 100

// ---------------------------------------------------------------------------
// GSTIN

const BASE36 = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ'

/** The 15th character of a GSTIN is a check digit over the first 14 (mod 36). */
export function gstinCheckDigitOk(gstin: string): boolean {
  if (!/^[0-9A-Z]{15}$/.test(gstin)) return false
  let sum = 0
  for (let i = 0; i < 14; i++) {
    const v = BASE36.indexOf(gstin[i])
    if (v < 0) return false
    const p = v * (i % 2 === 0 ? 1 : 2)
    sum += Math.floor(p / 36) + (p % 36)
  }
  const check = (36 - (sum % 36)) % 36
  return BASE36[check] === gstin[14]
}

export function isValidGstin(gstin: string): boolean {
  return /^[0-9]{2}[0-9A-Z]{13}$/.test(gstin) && STATE_CODES[gstin.slice(0, 2)] !== undefined && gstinCheckDigitOk(gstin)
}

// ---------------------------------------------------------------------------
// Period

/** "102026" for October 2026. */
export function periodBounds(period: string): { start: string; end: string } | null {
  const m = /^(0[1-9]|1[0-2])(\d{4})$/.exec(period)
  if (!m) return null
  const month = Number(m[1])
  const year = Number(m[2])
  const last = new Date(Date.UTC(year, month, 0)).getUTCDate()
  const mm = String(month).padStart(2, '0')
  return { start: `${year}-${mm}-01`, end: `${year}-${mm}-${String(last).padStart(2, '0')}` }
}

/** yyyy-mm-dd → dd-mm-yyyy, the date format in GSTN JSON. */
export const gstDate = (iso: string) => `${iso.slice(8, 10)}-${iso.slice(5, 7)}-${iso.slice(0, 4)}`

// ---------------------------------------------------------------------------
// CSV sales register

export const TEMPLATE_HEADERS = [
  'Document Type', 'Document Number', 'Document Date', 'Customer GSTIN', 'Customer Name', 'Place of Supply',
  'Reverse Charge', 'Invoice Value', 'Supply Category', 'Invoice Type', 'Export Type', 'Rate', 'Taxable Value',
  'IGST', 'CGST', 'SGST', 'Cess', 'HSN/SAC', 'Description', 'UQC', 'Quantity',
  'Shipping Bill Port', 'Shipping Bill Number', 'Shipping Bill Date',
  'Original Document Number', 'Original Document Date', 'Original Invoice Value',
]

export const TEMPLATE_EXAMPLE_ROWS = [
  ['Invoice', 'KS/26-27/0412', '05-10-2026', '32AAACK1234M1ZU', 'Kochi Traders', '32', 'N', '11800', 'Taxable', 'Regular', '', '18', '10000', '0', '900', '900', '0', '090411', 'Black pepper', 'KGS', '20', '', '', '', '', '', ''],
  ['Invoice', 'KS/26-27/0413', '07-10-2026', '', 'Walk-in customer', '29', 'N', '125000', 'Taxable', 'Regular', '', '5', '119047.62', '5952.38', '0', '0', '0', '090830', 'Cardamom', 'KGS', '40', '', '', '', '', '', ''],
  ['Credit Note', 'CN/26-27/018', '12-10-2026', '32AAACK1234M1ZU', 'Kochi Traders', '32', 'N', '1180', 'Taxable', 'Regular', '', '18', '1000', '0', '90', '90', '0', '090411', 'Black pepper (return)', 'KGS', '2', '', '', '', '', '', ''],
  ['Cancelled Invoice', 'KS/26-27/0414', '08-10-2026', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', '', ''],
]

const HEADER_ALIASES: Record<string, keyof SalesLine | 'skip'> = {
  'document type': 'docKind', 'doc type': 'docKind', 'voucher type': 'docKind', type: 'docKind',
  'document number': 'docNumber', 'document no': 'docNumber', 'invoice number': 'docNumber', 'invoice no': 'docNumber', 'voucher no': 'docNumber', 'doc no': 'docNumber',
  'document date': 'docDate', 'invoice date': 'docDate', date: 'docDate', 'voucher date': 'docDate',
  'customer gstin': 'customerGstin', gstin: 'customerGstin', 'gstin/uin': 'customerGstin', 'recipient gstin': 'customerGstin', 'party gstin': 'customerGstin',
  'customer name': 'customerName', 'party name': 'customerName', 'recipient name': 'customerName', 'receiver name': 'customerName',
  'place of supply': 'placeOfSupply', pos: 'placeOfSupply',
  'reverse charge': 'reverseCharge', rcm: 'reverseCharge',
  'invoice value': 'invoiceValue', 'document value': 'invoiceValue', 'total value': 'invoiceValue', 'note value': 'invoiceValue',
  'supply category': 'category', category: 'category',
  'invoice type': 'invoiceType',
  'export type': 'exportType',
  rate: 'rate', 'gst rate': 'rate', 'tax rate': 'rate', 'rate %': 'rate',
  'taxable value': 'taxableValue', 'taxable amount': 'taxableValue',
  igst: 'igst', 'igst amount': 'igst', 'integrated tax': 'igst',
  cgst: 'cgst', 'cgst amount': 'cgst', 'central tax': 'cgst',
  sgst: 'sgst', 'sgst amount': 'sgst', 'state tax': 'sgst', 'sgst/utgst': 'sgst', utgst: 'sgst',
  cess: 'cess', 'cess amount': 'cess',
  'hsn/sac': 'hsn', hsn: 'hsn', sac: 'hsn', 'hsn code': 'hsn',
  description: 'description', 'item name': 'description', 'item description': 'description',
  uqc: 'uqc', unit: 'uqc',
  quantity: 'quantity', qty: 'quantity',
  'shipping bill port': 'shippingPort', 'port code': 'shippingPort',
  'shipping bill number': 'shippingBillNo', 'shipping bill no': 'shippingBillNo',
  'shipping bill date': 'shippingBillDate',
  'original document number': 'originalDocNumber', 'original document no': 'originalDocNumber', 'original invoice number': 'originalDocNumber', 'original invoice no': 'originalDocNumber',
  'original document date': 'originalDocDate', 'original invoice date': 'originalDocDate',
  'original invoice value': 'originalInvoiceValue',
}

/** RFC 4180 CSV: quoted fields, doubled quotes, commas and newlines in quotes. */
export function parseCsv(text: string): string[][] {
  const rows: string[][] = []
  let row: string[] = []
  let field = ''
  let quoted = false
  const src = text.replace(/^\uFEFF/, '')
  for (let i = 0; i < src.length; i++) {
    const c = src[i]
    if (quoted) {
      if (c === '"') {
        if (src[i + 1] === '"') { field += '"'; i++ } else quoted = false
      } else field += c
    } else if (c === '"') quoted = true
    else if (c === ',') { row.push(field); field = '' }
    else if (c === '\n' || c === '\r') {
      if (c === '\r' && src[i + 1] === '\n') i++
      row.push(field); field = ''
      if (row.some(v => v.trim() !== '')) rows.push(row)
      row = []
    } else field += c
  }
  row.push(field)
  if (row.some(v => v.trim() !== '')) rows.push(row)
  return rows
}

const MONTHS: Record<string, number> = { jan: 1, feb: 2, mar: 3, apr: 4, may: 5, jun: 6, jul: 7, aug: 8, sep: 9, oct: 10, nov: 11, dec: 12 }

/** Accepts dd-mm-yyyy, dd/mm/yyyy, dd.mm.yyyy, yyyy-mm-dd and dd-Mon-yyyy (Tally). Returns yyyy-mm-dd or null. */
export function parseDate(raw: string): string | null {
  const s = raw.trim()
  let d: number, m: number, y: number
  let match = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(s)
  if (match) { y = +match[1]; m = +match[2]; d = +match[3] }
  else if ((match = /^(\d{1,2})[-/.](\d{1,2})[-/.](\d{2}|\d{4})$/.exec(s))) { d = +match[1]; m = +match[2]; y = +match[3] }
  else if ((match = /^(\d{1,2})[-\s/]([A-Za-z]{3})[A-Za-z]*[-\s/](\d{2}|\d{4})$/.exec(s))) {
    d = +match[1]; m = MONTHS[match[2].toLowerCase()] ?? 0; y = +match[3]
  } else return null
  if (y < 100) y += 2000
  const dt = new Date(Date.UTC(y, m - 1, d))
  if (m < 1 || m > 12 || dt.getUTCDate() !== d) return null
  return `${y}-${String(m).padStart(2, '0')}-${String(d).padStart(2, '0')}`
}

/** "1,18,000.50", "₹ 1180", "(1,000)" → number. Blank → 0. NaN when unreadable. */
export function parseAmount(raw: string): number {
  let s = raw.trim().replace(/[₹,\s]/g, '').replace(/^Rs\.?/i, '')
  if (s === '' || s === '-') return 0
  let neg = false
  if (/^\(.*\)$/.test(s)) { neg = true; s = s.slice(1, -1) }
  if (!/^-?\d+(\.\d+)?$/.test(s)) return NaN
  const n = Number(s)
  return neg ? -n : n
}

function stateCodeFrom(raw: string): string {
  const s = raw.trim()
  if (s === '') return ''
  const m = /^(\d{1,2})\b/.exec(s) // "32", "32-Kerala", "32 Kerala"
  if (m) return m[1].padStart(2, '0')
  const byName = Object.entries(STATE_CODES).find(([, name]) => name.toLowerCase() === s.toLowerCase())
  return byName ? byName[0] : 'XX'
}

function docKindFrom(raw: string): DocKind | null {
  const s = raw.trim().toLowerCase().replace(/[^a-z]/g, '')
  if (['invoice', 'inv', 'taxinvoice', 'sales', 'sale', 'billofsupply'].includes(s)) return 'INV'
  if (['creditnote', 'crn', 'cn', 'credit', 'salesreturn'].includes(s)) return 'CRN'
  if (['debitnote', 'dbn', 'dn', 'debit'].includes(s)) return 'DBN'
  return null
}

function categoryFrom(raw: string): SupplyCategory | null {
  const s = raw.trim().toLowerCase().replace(/[^a-z]/g, '')
  if (s === '' || s === 'taxable' || s === 'regular') return 'TAXABLE'
  if (s === 'nil' || s === 'nilrated') return 'NIL'
  if (s === 'exempt' || s === 'exempted') return 'EXEMPT'
  if (s === 'nongst' || s === 'nongstsupply') return 'NONGST'
  return null
}

function invoiceTypeFrom(raw: string): InvoiceType | null {
  const s = raw.trim().toLowerCase().replace(/[^a-z]/g, '')
  if (s === '' || s === 'regular' || s === 'r' || s === 'regularb2b') return 'R'
  if (s === 'sezwithpayment' || s === 'sewp' || s === 'sezwp') return 'SEWP'
  if (s === 'sezwithoutpayment' || s === 'sewop' || s === 'sezwop') return 'SEWOP'
  if (s === 'deemedexport' || s === 'deemedexports' || s === 'de') return 'DE'
  return null
}

function exportTypeFrom(raw: string): ExportType | null {
  const s = raw.trim().toLowerCase().replace(/[^a-z]/g, '')
  if (s === '') return ''
  if (s === 'wpay' || s === 'withpayment' || s === 'exportwithpayment') return 'WPAY'
  if (s === 'wopay' || s === 'withoutpayment' || s === 'exportwithoutpayment' || s === 'lut') return 'WOPAY'
  return null
}

const NUM_LABELS: Record<string, string> = {
  invoiceValue: 'Invoice Value', rate: 'Rate', taxableValue: 'Taxable Value', igst: 'IGST', cgst: 'CGST', sgst: 'SGST',
  cess: 'Cess', quantity: 'Quantity', originalInvoiceValue: 'Original Invoice Value',
}

export type ParsedRegister = { lines: SalesLine[]; cancelled: CancelledDoc[]; issues: Issue[] }

/** A CSV sales register. */
export function parseSalesRegister(text: string): ParsedRegister {
  return parseSalesRegisterRows(parseCsv(text))
}

const REQUIRED: (keyof SalesLine)[] = ['docNumber', 'docDate', 'rate', 'taxableValue']

function columnsOf(row: string[]): Partial<Record<keyof SalesLine, number>> {
  const col: Partial<Record<keyof SalesLine, number>> = {}
  row.forEach((h, i) => {
    const key = HEADER_ALIASES[(h ?? '').trim().toLowerCase().replace(/\s+/g, ' ').replace(/\s*\(.*\)$/, '')]
    if (key && key !== 'skip' && col[key] === undefined) col[key] = i
  })
  return col
}

/**
 * Rows from a CSV or a worksheet (row i is spreadsheet row i + 1). The heading row is the first
 * of the top 20 rows that has all required columns, so title lines above it (firm name,
 * period, as Tally and Busy exports have) are skipped.
 */
export function parseSalesRegisterRows(rows: string[][]): ParsedRegister {
  const issues: Issue[] = []
  if (!rows.some(r => r.some(c => (c ?? '').trim() !== ''))) return { lines: [], cancelled: [], issues: [{ severity: 'error', message: 'The file is empty.' }] }

  let headerAt = rows.findIndex(r => r.some(c => (c ?? '').trim() !== ''))
  for (let i = 0; i < Math.min(rows.length, 20); i++) {
    const c = columnsOf(rows[i])
    if (REQUIRED.every(k => c[k] !== undefined)) { headerAt = i; break }
  }
  const col = columnsOf(rows[headerAt])
  const required = REQUIRED
  const missing = required.filter(k => col[k] === undefined)
  if (missing.length) {
    const names: Record<string, string> = { docNumber: 'Document Number', docDate: 'Document Date', rate: 'Rate', taxableValue: 'Taxable Value' }
    return { lines: [], cancelled: [], issues: [{ severity: 'error', message: `Columns not found: ${missing.map(m => names[m]).join(', ')}. Download the template to see the expected headings.` }] }
  }

  const lines: SalesLine[] = []
  const cancelled: CancelledDoc[] = []
  for (let r = headerAt + 1; r < rows.length; r++) {
    const cells = rows[r]
    if (!cells.some(c => (c ?? '').trim() !== '')) continue
    const rowNo = r + 1 // as the user sees it in Excel
    const get = (k: keyof SalesLine) => (col[k] === undefined ? '' : (cells[col[k]!] ?? '').trim())
    const bad = (message: string) => issues.push({ severity: 'error', row: rowNo, doc: get('docNumber') || undefined, message })

    if (/cancel/i.test(get('docKind'))) {
      // "Cancelled", "Cancelled Invoice", "Cancelled Credit Note": only the number matters (Table 13).
      const kind = docKindFrom(get('docKind').replace(/cancel(l)?ed/i, '').trim() || 'invoice') ?? 'INV'
      if (!get('docNumber')) { bad('Cancelled row without a Document Number.'); continue }
      cancelled.push({ row: rowNo, kind, docNumber: get('docNumber') })
      continue
    }
    const docKind = col.docKind === undefined ? 'INV' : docKindFrom(get('docKind') || 'invoice')
    if (!docKind) { bad(`Document Type "${get('docKind')}" is not Invoice, Credit Note, Debit Note or Cancelled.`); continue }
    const docDate = parseDate(get('docDate'))
    if (!docDate) { bad(get('docDate') ? `Document Date "${get('docDate')}" is not a date. Use dd-mm-yyyy.` : 'Document Date is empty.'); continue }
    const category = categoryFrom(get('category'))
    if (!category) { bad(`Supply Category "${get('category')}" should be Taxable, Nil, Exempt or Non-GST.`); continue }
    const invoiceType = invoiceTypeFrom(get('invoiceType'))
    if (!invoiceType) { bad(`Invoice Type "${get('invoiceType')}" should be Regular, SEZ with payment, SEZ without payment or Deemed export.`); continue }
    const exportType = exportTypeFrom(get('exportType'))
    if (exportType === null) { bad(`Export Type "${get('exportType')}" should be WPAY (with payment), WOPAY (under LUT) or blank.`); continue }

    const nums = { invoiceValue: 0, rate: 0, taxableValue: 0, igst: 0, cgst: 0, sgst: 0, cess: 0, quantity: 0, originalInvoiceValue: 0 }
    let numbersOk = true
    for (const k of Object.keys(nums) as (keyof typeof nums)[]) {
      const raw = get(k)
      const v = parseAmount(raw.replace(/%$/, ''))
      if (Number.isNaN(v)) { bad(`${NUM_LABELS[k]} "${raw}" is not a number.`); numbersOk = false; break }
      nums[k] = v
    }
    if (!numbersOk) continue

    const sb = get('shippingBillDate')
    const od = get('originalDocDate')
    lines.push({
      row: rowNo,
      docKind,
      docNumber: get('docNumber'),
      docDate,
      customerGstin: get('customerGstin').toUpperCase().replace(/\s/g, '').replace(/^URP$|^UNREGISTERED$|^NA$/, ''),
      customerName: get('customerName'),
      placeOfSupply: stateCodeFrom(get('placeOfSupply')),
      reverseCharge: /^(y|yes|true|1)$/i.test(get('reverseCharge')),
      category,
      invoiceType,
      exportType,
      ...nums,
      // Amounts in a credit note register are often negative; GSTR-1 wants them positive.
      ...(docKind !== 'INV' ? {
        invoiceValue: Math.abs(nums.invoiceValue), taxableValue: Math.abs(nums.taxableValue), igst: Math.abs(nums.igst),
        cgst: Math.abs(nums.cgst), sgst: Math.abs(nums.sgst), cess: Math.abs(nums.cess), quantity: Math.abs(nums.quantity),
      } : {}),
      hsn: get('hsn').replace(/\s/g, ''),
      description: get('description'),
      uqc: get('uqc').toUpperCase(),
      shippingPort: get('shippingPort').toUpperCase(),
      shippingBillNo: get('shippingBillNo'),
      shippingBillDate: sb ? (parseDate(sb) ?? 'invalid') : '',
      originalDocNumber: get('originalDocNumber'),
      originalDocDate: od ? (parseDate(od) ?? 'invalid') : '',
      originalInvoiceValue: Math.abs(nums.originalInvoiceValue),
    })
  }
  if (lines.length === 0 && issues.length === 0) issues.push({ severity: 'error', message: 'The file has headings but no rows.' })
  return { lines, cancelled, issues }
}

// ---------------------------------------------------------------------------
// Building GSTR-1

export type Gstr1Input = {
  gstin: string
  period: string // MMYYYY
  lines: SalesLine[]
  /** Numbers marked Cancelled in the register (Table 13). */
  cancelled?: CancelledDoc[]
  /** Aggregate turnover above Rs 5 crore last year: 6-digit HSN is then compulsory. */
  aatoAbove5Cr: boolean
}

export type SectionKey = 'b2b' | 'b2cl' | 'b2cs' | 'cdnr' | 'cdnur' | 'exp' | 'nil' | 'b2ba' | 'b2cla' | 'cdnra' | 'expa' | 'hsn_b2b' | 'hsn_b2c'
export type SectionTotal = { key: SectionKey; label: string; table: string; documents: number; taxable: number; igst: number; cgst: number; sgst: number; cess: number }

export const SECTION_LABELS: Record<SectionKey, { label: string; table: string }> = {
  b2b: { label: 'B2B invoices', table: '4A, 4B, 6B, 6C' },
  b2cl: { label: 'B2C large invoices', table: '5' },
  b2cs: { label: 'B2C small (summary)', table: '7' },
  cdnr: { label: 'Credit / debit notes (registered)', table: '9B' },
  cdnur: { label: 'Credit / debit notes (unregistered)', table: '9B' },
  exp: { label: 'Exports', table: '6A' },
  nil: { label: 'Nil, exempt and non-GST', table: '8' },
  b2ba: { label: 'Amended B2B invoices', table: '9A' },
  b2cla: { label: 'Amended B2C large invoices', table: '9A' },
  cdnra: { label: 'Amended notes (registered)', table: '9C' },
  expa: { label: 'Amended exports', table: '9A' },
  hsn_b2b: { label: 'HSN summary, B2B', table: '12' },
  hsn_b2c: { label: 'HSN summary, B2C', table: '12' },
}

/** Table 13: one row per document series. */
export type DocSeries = { nature: 1 | 4 | 5; natureLabel: string; from: string; to: string; total: number; cancelled: number; net: number }

export type Gstr1Result = {
  json: Record<string, unknown> | null
  sections: SectionTotal[]
  documentsIssued: DocSeries[]
  issues: Issue[]
  documentCount: number
}

type Doc = {
  key: string
  kind: DocKind
  number: string
  date: string
  gstin: string
  name: string
  pos: string
  reverseCharge: boolean
  value: number
  invoiceType: InvoiceType
  exportType: ExportType
  shippingPort: string
  shippingBillNo: string
  shippingBillDate: string
  interState: boolean
  originalNumber: string
  originalDate: string
  originalValue: number
  lines: SalesLine[]
}

type Amounts = { txval: number; iamt: number; camt: number; samt: number; csamt: number }
const zero = (): Amounts => ({ txval: 0, iamt: 0, camt: 0, samt: 0, csamt: 0 })
const addTo = (a: Amounts, l: { taxableValue: number; igst: number; cgst: number; sgst: number; cess: number }, sign = 1) => {
  a.txval += sign * l.taxableValue; a.iamt += sign * l.igst; a.camt += sign * l.cgst; a.samt += sign * l.sgst; a.csamt += sign * l.cess
}

/** One item per tax rate per document: the portal rejects a document that repeats a rate. */
function itemsByRate(lines: SalesLine[]): { rt: number; amt: Amounts }[] {
  const map = new Map<number, Amounts>()
  for (const l of lines) {
    if (!map.has(l.rate)) map.set(l.rate, zero())
    addTo(map.get(l.rate)!, l)
  }
  return [...map.entries()].sort((a, b) => a[0] - b[0]).map(([rt, amt]) => ({ rt, amt }))
}

const itmDet = (rt: number, a: Amounts, inter: boolean) => inter
  ? { txval: r2(a.txval), rt, iamt: r2(a.iamt), csamt: r2(a.csamt) }
  : { txval: r2(a.txval), rt, camt: r2(a.camt), samt: r2(a.samt), csamt: r2(a.csamt) }

const NATURE: Record<DocKind, { code: 1 | 4 | 5; label: string }> = {
  INV: { code: 1, label: 'Invoices for outward supply' },
  DBN: { code: 4, label: 'Debit notes' },
  CRN: { code: 5, label: 'Credit notes' },
}

/**
 * Table 13. A series is the document number with its last run of digits taken out
 * ("KS/26-27/0412" → "KS/26-27/" + 412). Numbers between the lowest and highest that are
 * neither in the register nor marked Cancelled are counted as cancelled, with a warning
 * so the firm can confirm them.
 */
export function documentSeries(
  issued: { kind: DocKind; number: string }[],
  cancelled: CancelledDoc[],
  warn: (message: string) => void = () => {},
): DocSeries[] {
  type S = { kind: DocKind; prefix: string; suffix: string; width: number; nums: Set<number>; cancelled: Set<number>; loose: string[] }
  const series = new Map<string, S>()
  const add = (kind: DocKind, number: string, isCancelled: boolean) => {
    const m = /^(.*?)(\d+)(\D*)$/.exec(number.trim())
    if (!m) {
      const key = `${kind}|${number}|x`
      const s = series.get(key) ?? { kind, prefix: number, suffix: '', width: 0, nums: new Set(), cancelled: new Set(), loose: [] }
      s.loose.push(number); if (isCancelled) s.cancelled.add(-1)
      series.set(key, s); return
    }
    const [, prefix, digits, suffix] = m
    const key = `${kind}|${prefix.toUpperCase()}|${suffix.toUpperCase()}`
    const s = series.get(key) ?? { kind, prefix, suffix, width: digits.length, nums: new Set(), cancelled: new Set(), loose: [] }
    s.width = Math.max(s.width, digits.length)
    ;(isCancelled ? s.cancelled : s.nums).add(Number(digits))
    series.set(key, s)
  }
  for (const d of issued) add(d.kind, d.number, false)
  for (const c of cancelled) add(c.kind, c.docNumber, true)

  const out: DocSeries[] = []
  for (const s of series.values()) {
    const nature = NATURE[s.kind]
    if (s.loose.length) {
      for (const n of s.loose) out.push({ nature: nature.code, natureLabel: nature.label, from: n, to: n, total: 1, cancelled: s.cancelled.size ? 1 : 0, net: s.cancelled.size ? 0 : 1 })
      continue
    }
    const all = [...s.nums, ...s.cancelled]
    const lo = Math.min(...all), hi = Math.max(...all)
    const fmt = (n: number) => `${s.prefix}${String(n).padStart(s.width, '0')}${s.suffix}`
    const total = hi - lo + 1
    if (total > 100000) { warn(`Series ${s.prefix}…${s.suffix} spans ${total} numbers; check the document numbers.`); continue }
    const gaps: number[] = []
    for (let n = lo; n <= hi; n++) if (!s.nums.has(n) && !s.cancelled.has(n)) gaps.push(n)
    if (gaps.length) {
      const shown = gaps.slice(0, 8).map(fmt).join(', ')
      warn(`${nature.label}: ${gaps.length} number${gaps.length === 1 ? '' : 's'} missing from the register in series ${fmt(lo)} to ${fmt(hi)} (${shown}${gaps.length > 8 ? ', …' : ''}). They are counted as cancelled in Table 13; add them if they were issued.`)
    }
    const cancelledCount = [...s.cancelled].filter(n => !s.nums.has(n)).length + gaps.length
    out.push({ nature: nature.code, natureLabel: nature.label, from: fmt(lo), to: fmt(hi), total, cancelled: cancelledCount, net: total - cancelledCount })
  }
  return out.sort((a, b) => a.nature - b.nature || a.from.localeCompare(b.from))
}

export function buildGstr1(input: Gstr1Input): Gstr1Result {
  const issues: Issue[] = []
  const err = (message: string, d?: { row?: number; doc?: string }) => issues.push({ severity: 'error', message, ...d })
  const warn = (message: string, d?: { row?: number; doc?: string }) => issues.push({ severity: 'warning', message, ...d })

  const gstin = input.gstin.trim().toUpperCase()
  if (!isValidGstin(gstin)) err(`The client's GSTIN "${input.gstin}" is not valid. Correct it on the client record.`)
  const bounds = periodBounds(input.period)
  if (!bounds) err(`Return period "${input.period}" should be MMYYYY, e.g. 102026.`)
  if (issues.length) return { json: null, sections: [], documentsIssued: [], issues, documentCount: 0 }
  const supplierState = gstin.slice(0, 2)

  // --- group rows into documents -------------------------------------------
  const docs = new Map<string, Doc>()
  for (const l of input.lines) {
    const number = l.docNumber.trim()
    const key = `${l.docKind}|${number.toUpperCase()}`
    const isExport = l.exportType !== ''
    const pos = isExport ? '96' : (l.placeOfSupply || (l.customerGstin ? l.customerGstin.slice(0, 2) : supplierState))
    const existing = docs.get(key)
    if (existing) {
      const differs = existing.date !== l.docDate || existing.gstin !== l.customerGstin || existing.pos !== pos
        || existing.invoiceType !== l.invoiceType || existing.exportType !== l.exportType
        || existing.originalNumber !== (l.originalDocNumber ?? '').trim()
      if (differs) err('Rows of this document disagree on date, customer GSTIN, place of supply, type or original document. Each document can have only one of each.', { row: l.row, doc: number })
      if (l.invoiceValue && existing.value && Math.abs(l.invoiceValue - existing.value) > 0.01) {
        err(`Invoice Value differs between rows of this document (${existing.value} and ${l.invoiceValue}). Repeat the full document value on every row.`, { row: l.row, doc: number })
      }
      if (!existing.value) existing.value = l.invoiceValue
      existing.lines.push(l)
      continue
    }
    docs.set(key, {
      key, kind: l.docKind, number, date: l.docDate, gstin: l.customerGstin, name: l.customerName, pos,
      reverseCharge: l.reverseCharge, value: l.invoiceValue, invoiceType: l.invoiceType, exportType: l.exportType,
      shippingPort: l.shippingPort, shippingBillNo: l.shippingBillNo, shippingBillDate: l.shippingBillDate,
      // SEZ supplies and exports are always inter-state (IGST Act s.7(5)).
      interState: isExport || l.invoiceType === 'SEWP' || l.invoiceType === 'SEWOP' || pos !== supplierState,
      originalNumber: (l.originalDocNumber ?? '').trim(),
      originalDate: l.originalDocDate ?? '',
      originalValue: l.originalInvoiceValue ?? 0,
      lines: [l],
    })
  }

  // --- check each document ---------------------------------------------------
  for (const d of docs.values()) {
    const ref = { row: d.lines[0].row, doc: d.number }
    if (!d.number) err('Document Number is empty.', ref)
    else if (d.number.length > 16) err('Document Number is longer than 16 characters, which the portal does not accept.', ref)
    else if (!/^[A-Za-z0-9/-]+$/.test(d.number)) err('Document Number may contain only letters, digits, "/" and "-".', ref)
    else if (/^[0/-]+$/.test(d.number)) err('Document Number cannot be only zeros.', ref)

    if (d.date > bounds!.end) err(`Dated ${gstDate(d.date)}, after the end of the return period.`, ref)
    else if (d.date < bounds!.start) warn(`Dated ${gstDate(d.date)}, before the return period. Keep it only if it was left out of an earlier GSTR-1.`, ref)

    if (d.pos !== '96' && !STATE_CODES[d.pos]) err(`Place of Supply "${d.pos}" is not a state code (01–38 or 97).`, ref)

    if (d.gstin) {
      if (!isValidGstin(d.gstin)) err(`Customer GSTIN "${d.gstin}" is not valid (format or check digit).`, ref)
      else if (d.gstin === gstin) err('Customer GSTIN is the same as the client\'s own GSTIN.', ref)
    } else {
      if (d.invoiceType !== 'R') err('SEZ and deemed-export supplies need the customer\'s GSTIN.', ref)
      if (d.reverseCharge) err('Reverse charge applies only to supplies to registered customers; add the GSTIN or set Reverse Charge to N.', ref)
    }
    if (d.originalNumber) {
      if (d.originalDate === 'invalid') err('Original Document Date is not a date. Use dd-mm-yyyy.', ref)
      else if (!d.originalDate) err('An amendment needs the Original Document Date as well as the number.', ref)
      else if (d.originalDate >= bounds!.start) err('The original document is dated in this return period. Correct the document itself instead of amending it.', ref)
      if (d.originalNumber.length > 16 || !/^[A-Za-z0-9/-]+$/.test(d.originalNumber)) err('Original Document Number may have up to 16 letters, digits, "/" or "-".', ref)
    }
    if (d.exportType && d.gstin) warn('Export invoices do not carry a customer GSTIN; it is ignored.', ref)
    if (d.exportType && d.shippingBillDate === 'invalid') err('Shipping Bill Date is not a date. Use dd-mm-yyyy.', ref)
    if (d.exportType && d.shippingPort && !/^[A-Z0-9]{6}$/.test(d.shippingPort)) err(`Shipping Bill Port "${d.shippingPort}" should be a 6-character port code, e.g. INCOK1.`, ref)

    let sum = 0
    for (const l of d.lines) {
      const at = { row: l.row, doc: d.number }
      sum += l.taxableValue + l.igst + l.cgst + l.sgst + l.cess
      if (!GST_RATES.includes(l.rate)) { err(`Rate ${l.rate}% is not a GST rate.`, at); continue }
      if (l.taxableValue < 0) err('Taxable Value is negative. Enter credit notes as Credit Note rows with positive amounts.', at)
      if (l.category !== 'TAXABLE') {
        if (l.rate !== 0 || l.igst || l.cgst || l.sgst || l.cess) err(`${l.category === 'NONGST' ? 'Non-GST' : l.category.toLowerCase()} supplies carry no tax; set Rate and tax to 0.`, at)
        if (d.exportType) err('Mark exports as Taxable with Export Type WPAY or WOPAY.', at)
        continue
      }

      const withoutTax = d.exportType === 'WOPAY' || d.invoiceType === 'SEWOP'
      if (d.interState) {
        if (l.cgst || l.sgst) err('Inter-state supply: tax must be IGST, not CGST/SGST.', at)
        if (withoutTax && l.igst) err('Supply without payment of tax (LUT/bond) cannot have IGST.', at)
        const expected = withoutTax ? 0 : l.taxableValue * l.rate / 100
        if (!withoutTax && Math.abs(l.igst - expected) > 1) warn(`IGST ${l.igst} differs from ${r2(expected)} (${l.rate}% of ${l.taxableValue}).`, at)
      } else {
        if (l.igst) err('Intra-state supply: tax must be CGST + SGST, not IGST.', at)
        if (Math.abs(l.cgst - l.sgst) > 0.01) err(`CGST (${l.cgst}) and SGST (${l.sgst}) should be equal.`, at)
        const expected = l.taxableValue * l.rate / 100
        if (Math.abs(l.cgst + l.sgst - expected) > 1) warn(`CGST + SGST ${r2(l.cgst + l.sgst)} differs from ${r2(expected)} (${l.rate}% of ${l.taxableValue}).`, at)
      }

      // Table 12 needs HSN for every taxable line.
      if (!l.hsn) {
        if (d.gstin && !d.exportType) err('HSN/SAC is required for supplies to registered customers.', at)
        else warn('HSN/SAC is missing; the line is left out of the HSN summary.', at)
      } else if (!/^\d{4}$|^\d{6}$|^\d{8}$/.test(l.hsn)) err(`HSN/SAC "${l.hsn}" should have 4, 6 or 8 digits.`, at)
      else if (input.aatoAbove5Cr && l.hsn.length < 6) err('Turnover above Rs 5 crore: HSN/SAC must have at least 6 digits.', at)
      if (l.uqc && !UQC_CODES.has(l.uqc)) warn(`Unit "${l.uqc}" is not a GST unit (UQC); reported as OTH.`, at)
    }
    if (d.value && d.value + 1 < sum) warn(`Invoice Value ${d.value} is less than taxable value plus tax (${r2(sum)}).`, ref)
    if (!d.value) d.value = r2(sum)
  }

  // --- place each document in its table -----------------------------------------
  const b2b = new Map<string, Record<string, unknown>[]>()
  const cdnr = new Map<string, Record<string, unknown>[]>()
  const b2cl = new Map<string, Record<string, unknown>[]>()
  const b2cs = new Map<string, { sply_ty: string; pos: string; rt: number; amt: Amounts }>()
  const expo = new Map<string, Record<string, unknown>[]>()
  const cdnur: Record<string, unknown>[] = []
  const b2ba = new Map<string, Record<string, unknown>[]>()
  const b2cla = new Map<string, Record<string, unknown>[]>()
  const cdnra = new Map<string, Record<string, unknown>[]>()
  const expa = new Map<string, Record<string, unknown>[]>()
  const nil = new Map<string, { expt_amt: number; nil_amt: number; ngsup_amt: number }>()
  const hsn = { b2b: new Map<string, Record<string, number | string>>(), b2c: new Map<string, Record<string, number | string>>() }
  const totals = new Map<SectionKey, SectionTotal>()
  const tally = (key: SectionKey, a: Amounts, newDoc: boolean, sign = 1) => {
    const t = totals.get(key) ?? { key, ...SECTION_LABELS[key], documents: 0, taxable: 0, igst: 0, cgst: 0, sgst: 0, cess: 0 }
    t.documents += newDoc ? 1 : 0
    t.taxable += sign * a.txval; t.igst += sign * a.iamt; t.cgst += sign * a.camt; t.sgst += sign * a.samt; t.cess += sign * a.csamt
    totals.set(key, t)
  }

  for (const d of docs.values()) {
    const taxable = d.lines.filter(l => l.category === 'TAXABLE')
    const other = d.lines.filter(l => l.category !== 'TAXABLE')
    const registered = d.gstin !== '' && !d.exportType
    const inter = d.interState
    const docAmt = zero()
    taxable.forEach(l => addTo(docAmt, l))

    // Table 8: nil, exempt, non-GST (aggregated, never invoice-wise)
    if (d.originalNumber && other.length) err('Nil, exempt and non-GST lines cannot be amended in this way. Amend Table 8 on the portal.', { row: other[0].row, doc: d.number })
    for (const l of d.originalNumber ? [] : other) {
      const ty = `${inter ? 'INTR' : 'INTRA'}${registered ? 'B2B' : 'B2C'}`
      const n = nil.get(ty) ?? { expt_amt: 0, nil_amt: 0, ngsup_amt: 0 }
      const sign = d.kind === 'CRN' ? -1 : 1
      if (l.category === 'EXEMPT') n.expt_amt += sign * l.taxableValue
      else if (l.category === 'NIL') n.nil_amt += sign * l.taxableValue
      else n.ngsup_amt += sign * l.taxableValue
      nil.set(ty, n)
      tally('nil', { txval: l.taxableValue, iamt: 0, camt: 0, samt: 0, csamt: 0 }, false, sign)
    }

    if (taxable.length && d.originalNumber) {
      // Amendments (Table 9A / 9C) of documents reported in an earlier period.
      const items = itemsByRate(taxable)
      const ref = { row: d.lines[0].row, doc: d.number }
      const orig = { oinum: d.originalNumber, oidt: d.originalDate && d.originalDate !== 'invalid' ? gstDate(d.originalDate) : '' }
      if (d.exportType && d.kind === 'INV') {
        const list = expa.get(d.exportType) ?? []
        list.push({ ...orig, inum: d.number, idt: gstDate(d.date), val: r2(d.value),
          itms: items.map(i => ({ txval: r2(i.amt.txval), rt: i.rt, iamt: r2(i.amt.iamt), csamt: r2(i.amt.csamt) })) })
        expa.set(d.exportType, list)
        tally('expa', docAmt, true)
      } else if (registered && d.kind === 'INV') {
        const list = b2ba.get(d.gstin) ?? []
        list.push({ ...orig, inum: d.number, idt: gstDate(d.date), val: r2(d.value), pos: d.pos, rchrg: d.reverseCharge ? 'Y' : 'N',
          inv_typ: d.invoiceType, itms: items.map((i, n) => ({ num: n + 1, itm_det: itmDet(i.rt, i.amt, inter) })) })
        b2ba.set(d.gstin, list)
        tally('b2ba', docAmt, true)
      } else if (registered) {
        const list = cdnra.get(d.gstin) ?? []
        list.push({ ont_num: orig.oinum, ont_dt: orig.oidt, ntty: d.kind === 'CRN' ? 'C' : 'D', nt_num: d.number, nt_dt: gstDate(d.date),
          val: r2(d.value), pos: d.pos, rchrg: d.reverseCharge ? 'Y' : 'N', inv_typ: d.invoiceType,
          itms: items.map((i, n) => ({ num: n + 1, itm_det: itmDet(i.rt, i.amt, inter) })) })
        cdnra.set(d.gstin, list)
        tally('cdnra', docAmt, true, d.kind === 'CRN' ? -1 : 1)
      } else if (d.kind === 'INV' && inter && d.value > B2CL_THRESHOLD) {
        const list = b2cla.get(d.pos) ?? []
        list.push({ ...orig, inum: d.number, idt: gstDate(d.date), val: r2(d.value),
          itms: items.map((i, n) => ({ num: n + 1, itm_det: { txval: r2(i.amt.txval), rt: i.rt, iamt: r2(i.amt.iamt), csamt: r2(i.amt.csamt) } })) })
        b2cla.set(d.pos, list)
        tally('b2cla', docAmt, true)
      } else {
        err('Amendments of B2C small supplies (B2CSA), of notes to unregistered customers (CDNURA) and of export notes need the original month and are not supported yet. Amend them on the portal.', ref)
      }
      warn('Amendments are not added to the HSN summary (Table 12) or documents issued (Table 13). Adjust Table 12 for any change in value.', ref)
    } else if (taxable.length) {
      const items = itemsByRate(taxable)
      // Notes to unregistered customers go to CDNUR when they relate to an export or a B2C large invoice.
      const noteOnB2cl = d.kind !== 'INV' && !registered && !d.exportType && inter
        && (d.originalValue > 0 ? d.originalValue > B2CL_THRESHOLD : d.value > B2CL_THRESHOLD)
      if (d.exportType && d.kind !== 'INV') {
        cdnur.push({ typ: d.exportType === 'WPAY' ? 'EXPWP' : 'EXPWOP', ntty: d.kind === 'CRN' ? 'C' : 'D', nt_num: d.number, nt_dt: gstDate(d.date), val: r2(d.value),
          itms: items.map((i, n) => ({ num: n + 1, itm_det: { txval: r2(i.amt.txval), rt: i.rt, iamt: r2(i.amt.iamt), csamt: r2(i.amt.csamt) } })) })
        tally('cdnur', docAmt, true, d.kind === 'CRN' ? -1 : 1)
      } else if (noteOnB2cl) {
        if (!d.originalValue) warn('Note to an unregistered customer above Rs 1 lakh: reported as a note on a B2C large invoice (CDNUR). Add the Original Invoice Value to confirm.', { row: d.lines[0].row, doc: d.number })
        cdnur.push({ typ: 'B2CL', ntty: d.kind === 'CRN' ? 'C' : 'D', nt_num: d.number, nt_dt: gstDate(d.date), val: r2(d.value), pos: d.pos,
          itms: items.map((i, n) => ({ num: n + 1, itm_det: { txval: r2(i.amt.txval), rt: i.rt, iamt: r2(i.amt.iamt), csamt: r2(i.amt.csamt) } })) })
        tally('cdnur', docAmt, true, d.kind === 'CRN' ? -1 : 1)
      } else if (d.exportType) {
        const list = expo.get(d.exportType) ?? []
        const inv: Record<string, unknown> = {
          inum: d.number, idt: gstDate(d.date), val: r2(d.value),
          itms: items.map(i => ({ txval: r2(i.amt.txval), rt: i.rt, iamt: r2(i.amt.iamt), csamt: r2(i.amt.csamt) })),
        }
        if (d.shippingPort) inv.sbpcode = d.shippingPort
        if (d.shippingBillNo) inv.sbnum = d.shippingBillNo
        if (d.shippingBillDate && d.shippingBillDate !== 'invalid') inv.sbdt = gstDate(d.shippingBillDate)
        list.push(inv); expo.set(d.exportType, list)
        tally('exp', docAmt, true)
      } else if (registered && d.kind === 'INV') {
        const list = b2b.get(d.gstin) ?? []
        list.push({
          inum: d.number, idt: gstDate(d.date), val: r2(d.value), pos: d.pos, rchrg: d.reverseCharge ? 'Y' : 'N',
          inv_typ: d.invoiceType, itms: items.map((i, n) => ({ num: n + 1, itm_det: itmDet(i.rt, i.amt, inter) })),
        })
        b2b.set(d.gstin, list)
        tally('b2b', docAmt, true)
      } else if (registered) {
        const list = cdnr.get(d.gstin) ?? []
        list.push({
          ntty: d.kind === 'CRN' ? 'C' : 'D', nt_num: d.number, nt_dt: gstDate(d.date), val: r2(d.value), pos: d.pos,
          rchrg: d.reverseCharge ? 'Y' : 'N', inv_typ: d.invoiceType,
          itms: items.map((i, n) => ({ num: n + 1, itm_det: itmDet(i.rt, i.amt, inter) })),
        })
        cdnr.set(d.gstin, list)
        tally('cdnr', docAmt, true, d.kind === 'CRN' ? -1 : 1)
      } else if (d.kind === 'INV' && inter && d.value > B2CL_THRESHOLD) {
        const list = b2cl.get(d.pos) ?? []
        list.push({
          inum: d.number, idt: gstDate(d.date), val: r2(d.value),
          itms: items.map((i, n) => ({ num: n + 1, itm_det: { txval: r2(i.amt.txval), rt: i.rt, iamt: r2(i.amt.iamt), csamt: r2(i.amt.csamt) } })),
        })
        b2cl.set(d.pos, list)
        tally('b2cl', docAmt, true)
      } else {
        // Table 7: summed by place of supply and rate; notes reduce (or add to) the totals.
        const sign = d.kind === 'CRN' ? -1 : 1
        for (const i of items) {
          const k = `${inter ? 'INTER' : 'INTRA'}|${d.pos}|${i.rt}`
          const row = b2cs.get(k) ?? { sply_ty: inter ? 'INTER' : 'INTRA', pos: d.pos, rt: i.rt, amt: zero() }
          row.amt.txval += sign * i.amt.txval; row.amt.iamt += sign * i.amt.iamt; row.amt.camt += sign * i.amt.camt
          row.amt.samt += sign * i.amt.samt; row.amt.csamt += sign * i.amt.csamt
          b2cs.set(k, row)
        }
        tally('b2cs', docAmt, false, sign)
      }
    }

    // Table 12: every line with an HSN, split by customer type. Exports go to B2C (no recipient GSTIN).
    for (const l of d.lines) {
      if (d.originalNumber || !l.hsn || l.category === 'NONGST') continue
      const tab = registered ? hsn.b2b : hsn.b2c
      const isService = l.hsn.startsWith('99')
      const uqc = isService ? 'NA' : (UQC_CODES.has(l.uqc) && l.uqc !== 'NA' ? l.uqc : 'OTH')
      const k = `${l.hsn}|${l.rate}|${uqc}`
      const sign = d.kind === 'CRN' ? -1 : 1
      const h = tab.get(k) ?? { hsn_sc: l.hsn, desc: l.description.slice(0, 30), uqc, qty: 0, rt: l.rate, txval: 0, iamt: 0, camt: 0, samt: 0, csamt: 0 }
      h.qty = (h.qty as number) + (isService ? 0 : sign * l.quantity)
      h.txval = (h.txval as number) + sign * l.taxableValue
      h.iamt = (h.iamt as number) + sign * l.igst
      h.camt = (h.camt as number) + sign * l.cgst
      h.samt = (h.samt as number) + sign * l.sgst
      h.csamt = (h.csamt as number) + sign * l.cess
      tab.set(k, h)
    }
  }

  // --- Table 13: documents issued ----------------------------------------------------
  const documentsIssued = documentSeries(
    [...docs.values()].filter(d => !d.originalNumber).map(d => ({ kind: d.kind, number: d.number })),
    input.cancelled ?? [],
    warn,
  )

  // --- assemble ---------------------------------------------------------------
  const json: Record<string, unknown> = { gstin, fp: input.period, version: GSTR1_JSON_VERSION, hash: 'hash' }
  if (b2b.size) json.b2b = [...b2b.entries()].map(([ctin, inv]) => ({ ctin, inv }))
  if (b2cl.size) json.b2cl = [...b2cl.entries()].map(([pos, inv]) => ({ pos, inv }))
  if (b2cs.size) {
    json.b2cs = [...b2cs.values()].map(r => r.sply_ty === 'INTER'
      ? { sply_ty: 'INTER', pos: r.pos, typ: 'OE', rt: r.rt, txval: r2(r.amt.txval), iamt: r2(r.amt.iamt), csamt: r2(r.amt.csamt) }
      : { sply_ty: 'INTRA', pos: r.pos, typ: 'OE', rt: r.rt, txval: r2(r.amt.txval), camt: r2(r.amt.camt), samt: r2(r.amt.samt), csamt: r2(r.amt.csamt) })
    for (const r of b2cs.values()) {
      if (r.amt.txval < -0.005) warn(`B2C small for ${STATE_CODES[r.pos] ?? r.pos} at ${r.rt}% is negative after credit notes (${r2(r.amt.txval)}). The portal accepts this only if it is adjusted against the same state and rate.`)
    }
  }
  if (cdnr.size) json.cdnr = [...cdnr.entries()].map(([ctin, nt]) => ({ ctin, nt }))
  if (cdnur.length) json.cdnur = cdnur
  if (expo.size) json.exp = [...expo.entries()].map(([exp_typ, inv]) => ({ exp_typ, inv }))
  // SCHEMA: amendment sections (oinum/oidt, ont_num/ont_dt).
  if (b2ba.size) json.b2ba = [...b2ba.entries()].map(([ctin, inv]) => ({ ctin, inv }))
  if (b2cla.size) json.b2cla = [...b2cla.entries()].map(([pos, inv]) => ({ pos, inv }))
  if (cdnra.size) json.cdnra = [...cdnra.entries()].map(([ctin, nt]) => ({ ctin, nt }))
  if (expa.size) json.expa = [...expa.entries()].map(([exp_typ, inv]) => ({ exp_typ, inv }))
  // SCHEMA: Table 13 key names.
  if (documentsIssued.length) {
    const byNature = new Map<number, DocSeries[]>()
    for (const d of documentsIssued) byNature.set(d.nature, [...(byNature.get(d.nature) ?? []), d])
    json.doc_issue = {
      doc_det: [...byNature.entries()].sort((a, b) => a[0] - b[0]).map(([doc_num, list]) => ({
        doc_num,
        docs: list.map((d, i) => ({ num: i + 1, from: d.from, to: d.to, totnum: d.total, cancel: d.cancelled, net_issue: d.net })),
      })),
    }
  }
  if (nil.size) {
    json.nil = { inv: [...nil.entries()].map(([sply_ty, n]) => ({ sply_ty, expt_amt: r2(n.expt_amt), nil_amt: r2(n.nil_amt), ngsup_amt: r2(n.ngsup_amt) })) }
  }
  const hsnList = (m: Map<string, Record<string, number | string>>) => [...m.values()].map((h, i) => ({
    num: i + 1, hsn_sc: h.hsn_sc, desc: h.desc, uqc: h.uqc, qty: r2(h.qty as number), rt: h.rt,
    txval: r2(h.txval as number), iamt: r2(h.iamt as number), camt: r2(h.camt as number), samt: r2(h.samt as number), csamt: r2(h.csamt as number),
  }))
  // SCHEMA: Phase III key names for the split HSN table.
  if (hsn.b2b.size || hsn.b2c.size) {
    const h: Record<string, unknown> = {}
    if (hsn.b2b.size) h.hsn_b2b = hsnList(hsn.b2b)
    if (hsn.b2c.size) h.hsn_b2c = hsnList(hsn.b2c)
    json.hsn = h
  }
  for (const [key, m] of [['hsn_b2b', hsn.b2b], ['hsn_b2c', hsn.b2c]] as const) {
    for (const h of m.values()) tally(key, { txval: h.txval as number, iamt: h.iamt as number, camt: h.camt as number, samt: h.samt as number, csamt: h.csamt as number }, false)
  }

  const order: SectionKey[] = ['b2b', 'b2cl', 'b2cs', 'cdnr', 'cdnur', 'exp', 'nil', 'b2ba', 'b2cla', 'cdnra', 'expa', 'hsn_b2b', 'hsn_b2c']
  const sections = order.filter(k => totals.has(k)).map(k => {
    const t = totals.get(k)!
    return { ...t, taxable: r2(t.taxable), igst: r2(t.igst), cgst: r2(t.cgst), sgst: r2(t.sgst), cess: r2(t.cess) }
  })

  issues.sort((a, b) => (a.severity === b.severity ? (a.row ?? 0) - (b.row ?? 0) : a.severity === 'error' ? -1 : 1))
  const hasErrors = issues.some(i => i.severity === 'error')
  return { json: hasErrors ? null : json, sections, documentsIssued, issues, documentCount: docs.size }
}

export function templateCsv(): string {
  const esc = (v: string) => (/[",\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v)
  return [TEMPLATE_HEADERS, ...TEMPLATE_EXAMPLE_ROWS].map(r => r.map(esc).join(',')).join('\r\n') + '\r\n'
}
