import type { Block } from '../pdf/pdf.ts'

export type CmaAssumptionInput = {
  clientName: string
  baseYear: string
  revenueGrowthPercent: number
  cogsPercentOfRevenue: number
  opexPercentOfRevenue: number
  interestRatePercent: number
  taxRatePercent: number
  proposedTermLoan: number
  proposedWcLimit: number
  annualTermLoanRepayment: number
  debtorDays: number
  inventoryDays: number
  creditorDays: number
}

export type CmaBaseActuals = {
  revenue: number
  cogs: number
  opex: number
  depreciation: number
  financeCost: number
  equityAndReserves: number
  termDebt: number
  wcBorrowings: number
  fixedAssetsNet: number
  inventories: number
  receivables: number
  cashAndBank: number
  payables: number
}

export type CmaProjectedYear = {
  label: string
  revenue: number
  ebitda: number
  depreciation: number
  ebit: number
  interest: number
  pbt: number
  tax: number
  pat: number
  cashAccrual: number
  netWorth: number
  termDebt: number
  wcBorrowings: number
  totalOutsideLiabilities: number
  currentAssets: number
  otherCurrentLiabilities: number
  netWorkingCapital: number
  mpbfMethod1: number
  mpbfMethod2: number
  nayakLimit20Pct: number
  currentRatio: number
  tolTnwRatio: number
  dscr: number
}

const fmtLakhs = (n: number) =>
  new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(n / 100_000)

function nextFyLabel(baseFy: string, offset: number): string {
  const m = /^(\d{4})/.exec(baseFy)
  if (!m) return `Year +${offset}`
  const start = Number(m[1]) + offset
  return `${start}-${String((start + 1) % 100).padStart(2, '0')}`
}

export function computeCmaProjections(base: CmaBaseActuals, assump: CmaAssumptionInput): CmaProjectedYear[] {
  const years: CmaProjectedYear[] = []
  let prevRev = base.revenue
  let prevNw = base.equityAndReserves
  let prevTermDebt = base.termDebt

  for (let i = 0; i < 5; i++) {
    const label = i === 0
      ? `${assump.baseYear} (Actual)`
      : i === 1
        ? `${nextFyLabel(assump.baseYear, i)} (Est)`
        : `${nextFyLabel(assump.baseYear, i)} (Proj)`

    const rev = i === 0 ? prevRev : prevRev * (1 + assump.revenueGrowthPercent / 100)
    const cogs = i === 0 ? base.cogs : rev * (assump.cogsPercentOfRevenue / 100)
    const opex = i === 0 ? base.opex : rev * (assump.opexPercentOfRevenue / 100)
    const dep = i === 0 ? base.depreciation : Math.round((base.fixedAssetsNet + (i === 1 ? assump.proposedTermLoan * 0.5 : 0)) * 0.12)
    const ebitda = rev - cogs - opex
    const ebit = ebitda - dep

    const termDebt = i === 0
      ? prevTermDebt
      : Math.max(0, (i === 1 ? prevTermDebt + assump.proposedTermLoan : prevTermDebt) - assump.annualTermLoanRepayment)
    const wcLimit = i === 0 ? base.wcBorrowings : assump.proposedWcLimit
    const interest = i === 0 ? base.financeCost : Math.round((termDebt + wcLimit) * (assump.interestRatePercent / 100))
    const pbt = ebit - interest
    const tax = pbt > 0 ? Math.round(pbt * (assump.taxRatePercent / 100)) : 0
    const pat = pbt - tax
    const netWorth = i === 0 ? prevNw : prevNw + pat

    const inv = i === 0 ? base.inventories : Math.round((rev * assump.inventoryDays) / 365)
    const rec = i === 0 ? base.receivables : Math.round((rev * assump.debtorDays) / 365)
    const pay = i === 0 ? base.payables : Math.round((cogs * assump.creditorDays) / 365)
    const cash = i === 0 ? base.cashAndBank : Math.max(0, Math.round(rev * 0.03))
    const currentAssets = inv + rec + cash
    const otherCl = pay
    const totalCl = otherCl + wcLimit
    const workingCapitalGap = currentAssets - otherCl
    const mpbf1 = Math.max(0, Math.round(0.75 * workingCapitalGap))
    const mpbf2 = Math.max(0, Math.round(0.75 * currentAssets - otherCl))
    const nayak20 = Math.max(0, Math.round(rev * 0.20))

    const cashAccrual = pat + dep
    const dscrDenom = interest + (i === 0 ? 0 : assump.annualTermLoanRepayment)
    const dscr = dscrDenom > 0 ? (cashAccrual + interest) / dscrDenom : 0
    const tol = termDebt + totalCl
    const tolTnw = netWorth > 0 ? tol / netWorth : 0
    const currRatio = totalCl > 0 ? currentAssets / totalCl : 0

    years.push({
      label,
      revenue: Math.round(rev),
      ebitda: Math.round(ebitda),
      depreciation: Math.round(dep),
      ebit: Math.round(ebit),
      interest: Math.round(interest),
      pbt: Math.round(pbt),
      tax: Math.round(tax),
      pat: Math.round(pat),
      cashAccrual: Math.round(cashAccrual),
      netWorth: Math.round(netWorth),
      termDebt: Math.round(termDebt),
      wcBorrowings: Math.round(wcLimit),
      totalOutsideLiabilities: Math.round(tol),
      currentAssets: Math.round(currentAssets),
      otherCurrentLiabilities: Math.round(otherCl),
      netWorkingCapital: Math.round(currentAssets - totalCl),
      mpbfMethod1: mpbf1,
      mpbfMethod2: mpbf2,
      nayakLimit20Pct: nayak20,
      currentRatio: Number(currRatio.toFixed(2)),
      tolTnwRatio: Number(tolTnw.toFixed(2)),
      dscr: Number(dscr.toFixed(2)),
    })

    prevRev = rev
    prevNw = netWorth
    prevTermDebt = termDebt
  }
  return years
}

export function buildCmaReportPdfBlocks(
  assump: CmaAssumptionInput,
  years: CmaProjectedYear[],
  firmName?: string | null,
): Block[] {
  const headers = ['Particulars (Rs. in Lakhs)', ...years.map(y => y.label)]
  const align: ('left' | 'right')[] = ['left', ...years.map(() => 'right' as const)]
  const avgDscr = years.slice(1).reduce((s, y) => s + y.dscr, 0) / Math.max(1, years.length - 1)

  return [
    { kind: 'heading', level: 1, text: `CREDIT MONITORING ARRANGEMENT (CMA) DATA — ${assump.clientName.toUpperCase()}` },
    {
      kind: 'paragraph',
      text: `Bank Loan & Working Capital Assessment Report • Prepared by ${firmName || 'CaDesk Practice Suite'} (Figures in Rs. Lakhs)`,
      size: 9.5,
      bold: true,
    },
    {
      kind: 'table',
      header: ['Key Bank Facility Proposal & Assumptions', 'Parameter'],
      align: ['left', 'right'],
      rows: [
        ['Proposed Working Capital Limit (CC / OD)', `${fmtLakhs(assump.proposedWcLimit)} Lakhs`],
        ['Proposed New Term Loan', `${fmtLakhs(assump.proposedTermLoan)} Lakhs`],
        ['Annual Revenue Growth Assumption', `${assump.revenueGrowthPercent.toFixed(1)}% p.a.`],
        ['Working Capital Cycle (Inventory / Debtors / Creditors Days)', `${assump.inventoryDays}d / ${assump.debtorDays}d / ${assump.creditorDays}d`],
        ['Average Projected DSCR (Y+1 to Y+4)', `${avgDscr.toFixed(2)}x`],
      ],
    },
    { kind: 'heading', level: 2, text: 'FORM II — OPERATING STATEMENT (5-YEAR PROFITABILITY PROJECTION)' },
    {
      kind: 'table',
      header: headers,
      align,
      rows: [
        ['Gross Sales / Revenue from Operations', ...years.map(y => fmtLakhs(y.revenue))],
        ['EBITDA (Operating Profit before Dep & Int)', ...years.map(y => fmtLakhs(y.ebitda))],
        ['Less: Depreciation', ...years.map(y => fmtLakhs(y.depreciation))],
        ['EBIT (Profit Before Interest & Tax)', ...years.map(y => fmtLakhs(y.ebit))],
        ['Less: Interest & Finance Charges', ...years.map(y => fmtLakhs(y.interest))],
        ['Profit Before Tax (PBT)', ...years.map(y => fmtLakhs(y.pbt))],
        ['Tax Provision', ...years.map(y => fmtLakhs(y.tax))],
        ['Profit After Tax (PAT)', ...years.map(y => fmtLakhs(y.pat))],
        ['Cash Accruals (PAT + Depreciation)', ...years.map(y => fmtLakhs(y.cashAccrual))],
      ],
    },
    { kind: 'heading', level: 2, text: 'FORM III & FORM V — BALANCE SHEET SUMMARY & MPBF ASSESSMENT (TANDON / NAYAK)' },
    {
      kind: 'table',
      header: headers,
      align,
      rows: [
        ['Tangible Net Worth (TNW)', ...years.map(y => fmtLakhs(y.netWorth))],
        ['Term Loans (LT Debt)', ...years.map(y => fmtLakhs(y.termDebt))],
        ['Working Capital Bank Borrowings', ...years.map(y => fmtLakhs(y.wcBorrowings))],
        ['Total Outside Liabilities (TOL)', ...years.map(y => fmtLakhs(y.totalOutsideLiabilities))],
        ['Total Current Assets (CA)', ...years.map(y => fmtLakhs(y.currentAssets))],
        ['Current Liabilities (Other than Bank Borrowings)', ...years.map(y => fmtLakhs(y.otherCurrentLiabilities))],
        ['Net Working Capital (NWC)', ...years.map(y => fmtLakhs(y.netWorkingCapital))],
        ['MPBF — Tandon Committee Method I (75% WCG)', ...years.map(y => fmtLakhs(y.mpbfMethod1))],
        ['MPBF — Tandon Committee Method II (Min 25% NWC)', ...years.map(y => fmtLakhs(y.mpbfMethod2))],
        ['Nayak Committee Benchmark (20% of Turnover)', ...years.map(y => fmtLakhs(y.nayakLimit20Pct))],
      ],
    },
    { kind: 'heading', level: 2, text: 'KEY BANKABILITY & COVENANT RATIOS' },
    {
      kind: 'table',
      header: headers,
      align,
      rows: [
        ['Current Ratio (CA / CL, Ideal >= 1.33x)', ...years.map(y => `${y.currentRatio.toFixed(2)}x`)],
        ['TOL / TNW Gearing Ratio (Ideal <= 3.00x)', ...years.map(y => `${y.tolTnwRatio.toFixed(2)}x`)],
        ['Debt Service Coverage Ratio (DSCR, Ideal >= 1.25x)', ...years.map(y => `${y.dscr.toFixed(2)}x`)],
      ],
    },
  ]
}
