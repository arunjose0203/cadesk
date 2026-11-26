import type { Block } from '../pdf/pdf.ts'
import type { Schedule3AssessmentDto, Schedule3FinancialPackDto, Schedule3LineSnapshot } from '../api.ts'

function scaleFactor(roundingUnit: string): { div: number; label: string } {
  const u = (roundingUnit || '').toLowerCase()
  if (u.includes('crore')) return { div: 10_000_000, label: 'Rs. in Crores' }
  if (u.includes('lakh')) return { div: 100_000, label: 'Rs. in Lakhs' }
  if (u.includes('thousand')) return { div: 1_000, label: 'Rs. in Thousands' }
  return { div: 1, label: 'Rs.' }
}

function fmtAmt(n: number, div: number): string {
  const scaled = n / div
  const abs = new Intl.NumberFormat('en-IN', {
    minimumFractionDigits: div > 1 ? 2 : 0,
    maximumFractionDigits: div > 1 ? 2 : 0,
  }).format(Math.abs(scaled))
  return scaled < -0.005 ? `(${abs})` : abs
}

export function sumLineSnapshot(s: Schedule3LineSnapshot) {
  const equityTotal = s.shareCapital + s.reservesAndSurplus
  const nonCurrLiab = s.longTermBorrowings + s.deferredTaxLiabilities + s.otherLongTermLiabilities + s.longTermProvisions
  const payables = s.tradePayablesMsme + s.tradePayablesOthers
  const currLiab = s.shortTermBorrowings + payables + s.otherCurrentLiabilities + s.shortTermProvisions
  const eqAndLiab = equityTotal + nonCurrLiab + currLiab

  const nonCurrAssets = s.propertyPlantEquipment + s.capitalWip + s.nonCurrentInvestments + s.deferredTaxAssets + s.longTermLoansAndAdvances + s.otherNonCurrentAssets
  const currAssets = s.currentInvestments + s.inventories + s.tradeReceivables + s.cashAndBank + s.shortTermLoansAndAdvances + s.otherCurrentAssets
  const totalAssets = nonCurrAssets + currAssets

  const totalIncome = s.revenueFromOperations + s.otherIncome
  const totalExpenses = s.costOfMaterialsConsumed + s.purchasesOfStockInTrade + s.changesInInventories
    + s.employeeBenefitsExpense + s.financeCosts + s.depreciationAndAmortization + s.otherExpenses
  const pbt = totalIncome - totalExpenses
  const pat = pbt - s.taxExpense

  return {
    equityTotal, nonCurrLiab, payables, currLiab, eqAndLiab,
    nonCurrAssets, currAssets, totalAssets,
    totalIncome, totalExpenses, pbt, pat,
  }
}

export function buildSchedule3FinancialPackBlocks(args: {
  assessment: Schedule3AssessmentDto
  pack: Schedule3FinancialPackDto
  firmName?: string | null
  auditorMemNo?: string | null
  frn?: string | null
}): Block[] {
  const { assessment, pack } = args
  const c = pack.currentYear
  const p = pack.precedingYear
  const cSum = sumLineSnapshot(c)
  const pSum = sumLineSnapshot(p)
  const { div, label } = scaleFactor(pack.roundingUnit)
  const f = (n: number) => fmtAmt(n, div)

  const blocks: Block[] = []

  // Cover / Statutory Particulars
  blocks.push({ kind: 'heading', level: 1, text: pack.clientName.toUpperCase() })
  blocks.push({
    kind: 'paragraph',
    text: `Statutory Financial Statements Pack (Companies Act, 2013 — Schedule III ${pack.accountingStandard}) • FY ${pack.financialYear}`,
    bold: true,
    size: 10,
  })
  blocks.push({
    kind: 'table',
    header: ['Entity Particulars', 'Details', 'Reporting Basis', 'Details'],
    rows: [
      ['Company / Assessee', pack.clientName, 'Financial Year', `FY ${pack.financialYear} (vs ${pack.precedingFinancialYear})`],
      ['CIN / Registration', pack.cin || 'As per MCA Master', 'PAN', pack.pan || 'As per Income Tax Master'],
      ['Accounting Framework', pack.accountingStandard, 'Presentation Unit', label],
      ['Assessment Status', assessment.assessmentStatus.toUpperCase(), 'Data Source', pack.derivedFromTrialBalance ? 'Synced Trial Balance' : 'Schedule III Working'],
    ],
  })

  // PART I: BALANCE SHEET
  blocks.push({ kind: 'heading', level: 2, text: `PART I — BALANCE SHEET AS AT 31 MARCH (${label})` })
  blocks.push({
    kind: 'table',
    header: ['Particulars', 'Note', `FY ${pack.financialYear}`, `FY ${pack.precedingFinancialYear}`],
    align: ['left', 'center', 'right', 'right'],
    rows: [
      ['I. EQUITY AND LIABILITIES', '', '', ''],
      ['1. Shareholders\' Funds', '', '', ''],
      ['   (a) Share Capital', '1', f(c.shareCapital), f(p.shareCapital)],
      ['   (b) Reserves & Surplus', '2', f(c.reservesAndSurplus), f(p.reservesAndSurplus)],
      ['   Total Shareholders\' Funds', '', f(cSum.equityTotal), f(pSum.equityTotal)],
      ['2. Non-Current Liabilities', '', '', ''],
      ['   (a) Long-Term Borrowings', '3', f(c.longTermBorrowings), f(p.longTermBorrowings)],
      ['   (b) Deferred Tax Liabilities (Net)', '4', f(c.deferredTaxLiabilities), f(p.deferredTaxLiabilities)],
      ['   (c) Other Long-Term Liabilities & Provisions', '5', f(c.otherLongTermLiabilities + c.longTermProvisions), f(p.otherLongTermLiabilities + p.longTermProvisions)],
      ['3. Current Liabilities', '', '', ''],
      ['   (a) Short-Term Borrowings', '6', f(c.shortTermBorrowings), f(p.shortTermBorrowings)],
      ['   (b) Trade Payables — Micro & Small Enterprises (MSME)', '7', f(c.tradePayablesMsme), f(p.tradePayablesMsme)],
      ['       Trade Payables — Other than MSME', '7', f(c.tradePayablesOthers), f(p.tradePayablesOthers)],
      ['   (c) Other Current Liabilities & Short-Term Provisions', '8', f(c.otherCurrentLiabilities + c.shortTermProvisions), f(p.otherCurrentLiabilities + p.shortTermProvisions)],
      ['TOTAL EQUITY AND LIABILITIES', '', f(cSum.eqAndLiab), f(pSum.eqAndLiab)],
      ['II. ASSETS', '', '', ''],
      ['1. Non-Current Assets', '', '', ''],
      ['   (a) Property, Plant & Equipment and Intangible Assets', '9', f(c.propertyPlantEquipment), f(p.propertyPlantEquipment)],
      ['   (b) Capital Work-in-Progress (CWIP)', '9A', f(c.capitalWip), f(p.capitalWip)],
      ['   (c) Non-Current Investments', '10', f(c.nonCurrentInvestments), f(p.nonCurrentInvestments)],
      ['   (d) Deferred Tax Assets & Long-Term Loans/Other', '11', f(c.deferredTaxAssets + c.longTermLoansAndAdvances + c.otherNonCurrentAssets), f(p.deferredTaxAssets + p.longTermLoansAndAdvances + p.otherNonCurrentAssets)],
      ['2. Current Assets', '', '', ''],
      ['   (a) Current Investments', '12', f(c.currentInvestments), f(p.currentInvestments)],
      ['   (b) Inventories', '13', f(c.inventories), f(p.inventories)],
      ['   (c) Trade Receivables', '14', f(c.tradeReceivables), f(p.tradeReceivables)],
      ['   (d) Cash and Cash Equivalents', '15', f(c.cashAndBank), f(p.cashAndBank)],
      ['   (e) Short-Term Loans & Advances / Other Current Assets', '16', f(c.shortTermLoansAndAdvances + c.otherCurrentAssets), f(p.shortTermLoansAndAdvances + p.otherCurrentAssets)],
      ['TOTAL ASSETS', '', f(cSum.totalAssets), f(pSum.totalAssets)],
    ],
  })

  // PART II: STATEMENT OF PROFIT & LOSS
  blocks.push({ kind: 'pageBreak' })
  blocks.push({ kind: 'heading', level: 2, text: `PART II — STATEMENT OF PROFIT AND LOSS (${label})` })
  blocks.push({
    kind: 'table',
    header: ['Particulars', 'Note', `FY ${pack.financialYear}`, `FY ${pack.precedingFinancialYear}`],
    align: ['left', 'center', 'right', 'right'],
    rows: [
      ['I. Revenue from Operations', '17', f(c.revenueFromOperations), f(p.revenueFromOperations)],
      ['II. Other Income', '18', f(c.otherIncome), f(p.otherIncome)],
      ['III. Total Income (I + II)', '', f(cSum.totalIncome), f(pSum.totalIncome)],
      ['IV. Expenses:', '', '', ''],
      ['   Cost of Materials Consumed / Direct Cost', '19', f(c.costOfMaterialsConsumed), f(p.costOfMaterialsConsumed)],
      ['   Purchases of Stock-in-Trade', '', f(c.purchasesOfStockInTrade), f(p.purchasesOfStockInTrade)],
      ['   Changes in Inventories of FG / WIP / Stock-in-Trade', '', f(c.changesInInventories), f(p.changesInInventories)],
      ['   Employee Benefits Expense', '20', f(c.employeeBenefitsExpense), f(p.employeeBenefitsExpense)],
      ['   Finance Costs', '21', f(c.financeCosts), f(p.financeCosts)],
      ['   Depreciation and Amortisation Expense', '9', f(c.depreciationAndAmortization), f(p.depreciationAndAmortization)],
      ['   Other Expenses', '22', f(c.otherExpenses), f(p.otherExpenses)],
      ['Total Expenses (IV)', '', f(cSum.totalExpenses), f(pSum.totalExpenses)],
      ['V. Profit Before Exceptional Items and Tax (III - IV)', '', f(cSum.pbt), f(pSum.pbt)],
      ['VI. Tax Expense (Current Tax + Deferred Tax)', '', f(c.taxExpense), f(p.taxExpense)],
      ['VII. Profit / (Loss) for the Year (V - VI)', '', f(cSum.pat), f(pSum.pat)],
    ],
  })

  // CASH FLOW STATEMENT (AS-3 Indirect Method)
  const dInv = -(c.inventories - p.inventories)
  const dRec = -(c.tradeReceivables - p.tradeReceivables)
  const dPay = cSum.payables - pSum.payables
  const dOcl = (c.otherCurrentLiabilities + c.shortTermProvisions) - (p.otherCurrentLiabilities + p.shortTermProvisions)
  const dOca = -((c.shortTermLoansAndAdvances + c.otherCurrentAssets) - (p.shortTermLoansAndAdvances + p.otherCurrentAssets))
  const opBeforeWc = cSum.pbt + c.depreciationAndAmortization + c.financeCosts
  const cashFromOps = opBeforeWc + dInv + dRec + dPay + dOcl + dOca - c.taxExpense

  const capex = -((c.propertyPlantEquipment + c.capitalWip) - (p.propertyPlantEquipment + p.capitalWip) + c.depreciationAndAmortization)
  const invDelta = -((c.nonCurrentInvestments + c.currentInvestments) - (p.nonCurrentInvestments + p.currentInvestments))
  const cashFromInvesting = capex + invDelta

  const dEquity = c.shareCapital - p.shareCapital
  const dDebt = (c.longTermBorrowings + c.shortTermBorrowings) - (p.longTermBorrowings + p.shortTermBorrowings)
  const cashFromFinancing = dEquity + dDebt - c.financeCosts
  const netCashDelta = cashFromOps + cashFromInvesting + cashFromFinancing

  blocks.push({ kind: 'heading', level: 2, text: `CASH FLOW STATEMENT (AS-3 INDIRECT METHOD) (${label})` })
  blocks.push({
    kind: 'table',
    header: ['Particulars', `FY ${pack.financialYear}`],
    align: ['left', 'right'],
    rows: [
      ['A. Cash Flow from Operating Activities', ''],
      ['   Profit Before Tax', f(cSum.pbt)],
      ['   Add: Depreciation & Amortisation', f(c.depreciationAndAmortization)],
      ['   Add: Finance Costs', f(c.financeCosts)],
      ['   Operating Profit before Working Capital Changes', f(opBeforeWc)],
      ['   Movement in Inventories / Receivables / Other Current Assets', f(dInv + dRec + dOca)],
      ['   Movement in Trade Payables & Current Liabilities', f(dPay + dOcl)],
      ['   Less: Direct Taxes Paid', f(-c.taxExpense)],
      ['   Net Cash Generated from Operating Activities (A)', f(cashFromOps)],
      ['B. Cash Flow from Investing Activities', ''],
      ['   Purchase / Addition to PPE & CWIP (Net of Dep)', f(capex)],
      ['   Movement in Investments', f(invDelta)],
      ['   Net Cash Used in Investing Activities (B)', f(cashFromInvesting)],
      ['C. Cash Flow from Financing Activities', ''],
      ['   Issue of Share Capital & Net Proceeds of Borrowings', f(dEquity + dDebt)],
      ['   Less: Finance Costs Paid', f(-c.financeCosts)],
      ['   Net Cash from Financing Activities (C)', f(cashFromFinancing)],
      ['Net Increase / (Decrease) in Cash Equivalents (A + B + C)', f(netCashDelta)],
      ['Cash & Cash Equivalents at Beginning of Year', f(p.cashAndBank)],
      ['Cash & Cash Equivalents at End of Year (Per Balance Sheet)', f(c.cashAndBank)],
    ],
  })

  // NOTES TO ACCOUNTS: 11 SCHEDULE III RATIOS & AGEING
  blocks.push({ kind: 'pageBreak' })
  blocks.push({ kind: 'heading', level: 2, text: 'NOTES TO ACCOUNTS — STATUTORY SCHEDULE III DISCLOSURES' })
  blocks.push({
    kind: 'list',
    ordered: true,
    items: [
      `Basis of Preparation: Financial statements prepared on accrual basis under historical cost convention in accordance with ${pack.accountingStandard} and Schedule III of the Companies Act, 2013.`,
      `Rounding Off: Figures rounded off to nearest ${pack.roundingUnit} (${assessment.roundingRuleRecommendation || 'Compliant u/s Sec 129'}).`,
      `MSME Disclosures (Sec 22 of MSMED Act): MSME Trade Payables stand at ${f(c.tradePayablesMsme)} (${label}).`,
    ],
  })

  if (assessment.ratios.length > 0) {
    blocks.push({ kind: 'heading', level: 3, text: 'Analytical Ratios (Mandatory Variance > 25% Disclosures)' })
    blocks.push({
      kind: 'table',
      header: ['Ratio', `FY ${pack.financialYear}`, `FY ${pack.precedingFinancialYear}`, 'Var %', 'Management Explanation (>25%)'],
      align: ['left', 'right', 'right', 'right', 'left'],
      rows: assessment.ratios.map(r => [
        r.ratioName,
        r.currentYearValue.toFixed(2),
        r.precedingYearValue.toFixed(2),
        `${r.variancePercent.toFixed(1)}%`,
        r.isVarianceSignificant ? (r.varianceExplanation || 'Explanation pending') : 'Within 25% tolerance',
      ]),
    })
  }

  if (assessment.ageingItems.length > 0) {
    blocks.push({ kind: 'heading', level: 3, text: `Statutory Ageing Schedules (${label})` })
    blocks.push({
      kind: 'table',
      header: ['Schedule', 'Category', '< 6m/1y', '1-2 Yrs', '2-3 Yrs', '> 3 Yrs', 'Total'],
      align: ['left', 'left', 'right', 'right', 'right', 'right', 'right'],
      rows: assessment.ageingItems.map(i => [
        i.scheduleType,
        i.particularsCategory,
        f(i.less6MonthsOr1Year),
        f(i.oneToTwoYears),
        f(i.twoToThreeYears),
        f(i.moreThanThreeYears),
        f(i.total),
      ]),
    })
  }

  blocks.push({ kind: 'spacer', height: 14 })
  blocks.push({
    kind: 'paragraph',
    text: `As per our report of even date • For ${args.firmName || 'Chartered Accountants'} (FRN: ${args.frn || '000000S'}) • Partner M.No: ${args.auditorMemNo || 'XXXXXX'}`,
    size: 9,
    bold: true,
  })

  return blocks
}
