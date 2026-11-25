/* Pure client helpers for NSDL/Protean e-TDS RPU exports, FVU validation, and Form 16/16A imports.
   Kept free of DOM/fetch calls so node --experimental-strip-types tests can verify everything directly. */

export type TdsSectionOption = {
  value: string
  label: string
  forms: readonly ('24Q' | '26Q' | '27Q')[]
  rateHint: string
}

export const TDS_FINANCIAL_YEARS = ['2026-27', '2025-26', '2024-25', '2023-24'] as const

export const TDS_SECTION_OPTIONS: readonly TdsSectionOption[] = [
  { value: '192_salary', label: '192 - Salary (Non-Govt) [Actual / Slab]', forms: ['24Q'], rateHint: 'Slab' },
  { value: '194C_company', label: '194C - Contractor (Company / Firm) [2%]', forms: ['26Q'], rateHint: '2%' },
  { value: '194C_other', label: '194C - Contractor (Individual / HUF) [1%]', forms: ['26Q'], rateHint: '1%' },
  { value: '194J_tech', label: '194J(a) - Technical Services / Call Centre / Royalty [2%]', forms: ['26Q'], rateHint: '2%' },
  { value: '194J_fees', label: '194J(b) - Professional / Director Fees [10%]', forms: ['26Q'], rateHint: '10%' },
  { value: '194I_land', label: '194I(b) - Rent: Land / Building / Furniture [10%]', forms: ['26Q'], rateHint: '10%' },
  { value: '194I_plant', label: '194I(a) - Rent: Plant / Machinery / Equipment [2%]', forms: ['26Q'], rateHint: '2%' },
  { value: '194A', label: '194A - Interest other than securities [10%]', forms: ['26Q'], rateHint: '10%' },
  { value: '194H', label: '194H - Commission or Brokerage [2% / 5%]', forms: ['26Q'], rateHint: '2%' },
  { value: '194Q', label: '194Q - Purchase of Goods (> ₹50L) [0.1%]', forms: ['26Q'], rateHint: '0.1%' },
  { value: '194D', label: '194D - Insurance Commission [2% / 5%]', forms: ['26Q'], rateHint: '2%' },
  { value: '194DA', label: '194DA - Life Insurance Policy Maturity [2%]', forms: ['26Q'], rateHint: '2%' },
  { value: '194B', label: '194B - Lottery / Crossword / Gaming [30%]', forms: ['26Q'], rateHint: '30%' },
  { value: '194N', label: '194N - Cash Withdrawal above Threshold [2%]', forms: ['26Q'], rateHint: '2%' },
  { value: '194R', label: '194R - Business Benefit or Perquisite [10%]', forms: ['26Q'], rateHint: '10%' },
  { value: '194S', label: '194S - Virtual Digital Asset Transfer [1%]', forms: ['26Q'], rateHint: '1%' },
  { value: '194T', label: '194T - Partner Remuneration / Interest [10%]', forms: ['26Q'], rateHint: '10%' },
  { value: '195', label: '195 - Payment to Non-Resident / Foreign Co.', forms: ['27Q'], rateHint: '20% / DTAA' },
] as const

export const TDS_STATE_CODES: readonly { code: string; name: string }[] = [
  { code: '01', name: '01 - Andaman & Nicobar Islands' },
  { code: '02', name: '02 - Andhra Pradesh' },
  { code: '03', name: '03 - Arunachal Pradesh' },
  { code: '04', name: '04 - Assam' },
  { code: '05', name: '05 - Bihar' },
  { code: '06', name: '06 - Chandigarh' },
  { code: '07', name: '07 - Dadra & Nagar Haveli' },
  { code: '09', name: '09 - Delhi' },
  { code: '10', name: '10 - Goa' },
  { code: '11', name: '11 - Gujarat' },
  { code: '12', name: '12 - Haryana' },
  { code: '13', name: '13 - Himachal Pradesh' },
  { code: '14', name: '14 - Jammu & Kashmir' },
  { code: '15', name: '15 - Karnataka' },
  { code: '16', name: '16 - Kerala' },
  { code: '17', name: '17 - Lakshadweep' },
  { code: '18', name: '18 - Madhya Pradesh' },
  { code: '19', name: '19 - Maharashtra' },
  { code: '20', name: '20 - Manipur' },
  { code: '21', name: '21 - Meghalaya' },
  { code: '22', name: '22 - Mizoram' },
  { code: '23', name: '23 - Nagaland' },
  { code: '24', name: '24 - Odisha' },
  { code: '25', name: '25 - Puducherry' },
  { code: '26', name: '26 - Punjab' },
  { code: '27', name: '27 - Rajasthan' },
  { code: '28', name: '28 - Sikkim' },
  { code: '29', name: '29 - Tamil Nadu' },
  { code: '30', name: '30 - Tripura' },
  { code: '31', name: '31 - Uttar Pradesh' },
  { code: '32', name: '32 - West Bengal' },
  { code: '33', name: '33 - Chhattisgarh' },
  { code: '34', name: '34 - Uttarakhand' },
  { code: '35', name: '35 - Jharkhand' },
  { code: '36', name: '36 - Telangana' },
  { code: '37', name: '37 - Ladakh' },
] as const

export const DEDUCTOR_TYPES: readonly { code: string; name: string }[] = [
  { code: 'K', name: 'K - Company' },
  { code: 'P', name: 'P - Individual / Sole Proprietor' },
  { code: 'F', name: 'F - Partnership Firm' },
  { code: 'H', name: 'H - Hindu Undivided Family (HUF)' },
  { code: 'B', name: 'B - Association of Persons / BOI' },
  { code: 'L', name: 'L - Local Authority' },
  { code: 'J', name: 'J - Artificial Juridical Person' },
  { code: 'S', name: 'S - Central Government' },
  { code: 'T', name: 'T - State Government' },
  { code: 'D', name: 'D - Statutory Body (Central)' },
  { code: 'E', name: 'E - Statutory Body (State)' },
  { code: 'G', name: 'G - Autonomous Body (Central)' },
  { code: 'N', name: 'N - Autonomous Body (State)' },
] as const

export const REMITTANCE_27Q_CODES: readonly { code: string; name: string }[] = [
  { code: '21', name: '21 - Fees for technical / included services' },
  { code: '49', name: '49 - Royalty' },
  { code: '16', name: '16 - Dividend' },
  { code: '27', name: '27 - Interest (u/s 195 / bonds)' },
  { code: '6', name: '6 - Commission / Brokerage' },
  { code: '99', name: '99 - Other Income / General u/s 195' },
] as const

export const COUNTRY_27Q_CODES: readonly { code: string; name: string }[] = [
  { code: '267', name: '267 - UNITED STATES' },
  { code: '44', name: '44 - UNITED KINGDOM' },
  { code: '231', name: '231 - SINGAPORE' },
  { code: '259', name: '259 - UNITED ARAB EMIRATES' },
  { code: '2', name: '2 - AUSTRALIA' },
  { code: '110', name: '110 - GERMANY' },
  { code: '39', name: '39 - CANADA' },
  { code: '126', name: '126 - JAPAN' },
  { code: '9999', name: '9999 - OTHERS' },
] as const

export function isValidTan(tan: string): boolean {
  return /^[A-Z]{4}[0-9]{5}[A-Z]$/.test(tan.trim().toUpperCase())
}

export function isValidPan(pan: string, allowExempt = false): boolean {
  const up = pan.trim().toUpperCase()
  if (/^[A-Z]{5}[0-9]{4}[A-Z]$/.test(up)) return true
  return allowExempt && (up === 'PANNOTAVBL' || up === 'PANAPPLIED' || up === 'PANINVALID')
}

export function isValidFvuFileName(name: string): boolean {
  const trimmed = name.trim()
  if (!trimmed || trimmed.length > 12 || !trimmed.endsWith('.txt')) return false
  return !/[\s:/?><*"|\\_^]/.test(trimmed)
}

export function isAct2025Fy(fy: string): boolean {
  const m = /^(\d{4})-(\d{2})$/.exec(fy.trim())
  return m ? Number(m[1]) >= 2026 : false
}

export function officialFormBadge(formType: string, fy: string): { formNo: string; actLabel: string } {
  const act2025 = isAct2025Fy(fy)
  const ft = formType.trim().toUpperCase()
  if (act2025) {
    const formNo = ft === '24Q' ? '138' : ft === '26Q' ? '140' : ft === '27Q' ? '144' : ft === '27EQ' ? '143' : ft
    return { formNo, actLabel: 'Income-tax Act, 2025 (FVU 1.2)' }
  }
  return { formNo: ft, actLabel: 'Income-tax Act, 1961 (FVU 9.5)' }
}

export function suggestedRpuFileName(formType: string, quarter: string, fy: string): string {
  const { formNo } = officialFormBadge(formType, fy)
  const q = quarter.trim().toUpperCase()
  return `${(formNo + q).slice(0, 8)}.txt`
}

export type ClientParsedFvuErr = {
  lineNumber: number | null
  recordType: string
  challanNo: number | null
  deducteeNo: number | null
  errorCode: string
  description: string
}

export function parseFvuErrClient(content: string): ClientParsedFvuErr[] {
  if (!content.trim()) return []
  const out: ClientParsedFvuErr[] = []
  for (const raw of content.split(/\r\n|\n|\r/)) {
    const line = raw.trim()
    if (!line || !line.includes('^')) continue
    const cols = line.split('^')
    if (cols[0].startsWith('<') || cols[0].toLowerCase() === 'line number') continue
    const ln = Number.parseInt(cols[0] ?? '', 10)
    const cn = Number.parseInt(cols[3] ?? '', 10)
    const dn = Number.parseInt(cols[4] ?? '', 10)
    const desc = [...cols].reverse().find(c => c.trim().length > 0)?.trim() ?? line
    out.push({
      lineNumber: Number.isFinite(ln) ? ln : null,
      recordType: (cols[1] ?? '').trim(),
      challanNo: Number.isFinite(cn) ? cn : null,
      deducteeNo: Number.isFinite(dn) ? dn : null,
      errorCode: (cols[6] ?? '').trim() || (cols[7] ?? '').trim() || 'FVU',
      description: desc,
    })
  }
  return out
}

export function form16TemplateCsv(): string {
  return [
    'Form,Financial Year,Quarter,Deductee PAN,Deductee Name,Section,Payment Date,Deduction Date,Amount Paid,Rate,TDS Amount,Certificate No,BSR Code,Challan Serial No,Challan Date',
    '26Q,2026-27,Q1,AAACP1234D,Alpha Logistics Pvt Ltd,194C_company,2026-05-15,2026-05-15,250000,2,5000,TRACES16A001,0510025,00412,2026-06-05',
    '26Q,2026-27,Q1,ABCDE1234F,Kiran Advisory LLP,194J_fees,2026-06-10,2026-06-10,120000,10,12000,TRACES16A002,0510025,00412,2026-06-05',
  ].join('\n')
}
