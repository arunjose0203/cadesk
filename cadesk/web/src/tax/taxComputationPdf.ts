import type { Block } from '../pdf/pdf.ts'
import type {
  BusinessTaxComputationResultDto,
  PersonalExtendedTaxResultDto,
  RegimeComparisonDto,
  TaxComputationDto,
  TrustTaxResultDto,
} from '../api.ts'

const inr = (n: number): string => {
  const rounded = Math.round(n)
  const abs = new Intl.NumberFormat('en-IN', { maximumFractionDigits: 0 }).format(Math.abs(rounded))
  return rounded < 0 ? `(Rs. ${abs})` : `Rs. ${abs}`
}

export type TaxPdfPreviewArgs = {
  clientName?: string | null
  clientPan?: string | null
  assessmentYear: string
  financialYear: string
  taxpayerCategory: string
  optedRegime: string
  firmName?: string | null
  notes?: string | null
  comparison?: RegimeComparisonDto | null
  business?: BusinessTaxComputationResultDto | null
  extendedPersonal?: PersonalExtendedTaxResultDto | null
  trust?: TrustTaxResultDto | null
  tdsTcsCredit?: number
  advanceTaxPaid?: number
  selfAssessmentTax?: number
}

function inferFormLabel(args: TaxPdfPreviewArgs): string {
  if (args.trust) return 'ITR-7 (Trust / Institution / Sec 8 Co.)'
  if (args.business) return args.business.entityType === 'firm' ? 'ITR-5 (Firm / LLP / AOP)' : 'ITR-6 (Company)'
  if (args.extendedPersonal) return args.extendedPersonal.returnFormType === 'ITR-3' ? 'ITR-3 (Proprietor / Books u/s 44AB)' : 'ITR-2 (Capital Gains / Multi-HP / VDA / FA)'
  if (args.taxpayerCategory === 'trust') return 'ITR-7 (Trust / NGO)'
  if (args.taxpayerCategory === 'company') return 'ITR-6 (Company)'
  if (args.taxpayerCategory === 'firm') return 'ITR-5 (Firm / LLP)'
  if (args.comparison && (args.comparison.oldRegime.businessIncome > 0 || args.comparison.newRegime.businessIncome > 0)) {
    return 'ITR-4 / ITR-3 (Business & Profession)'
  }
  return 'ITR-1 / ITR-2 (Individual / HUF)'
}

export function buildTaxComputationPdfBlocks(
  comp: TaxComputationDto,
  opts?: { firmName?: string | null; clientPan?: string | null },
): Block[] {
  return buildTaxComputationPreviewPdfBlocks({
    clientName: comp.clientName,
    clientPan: opts?.clientPan ?? null,
    assessmentYear: comp.assessmentYear,
    financialYear: comp.financialYear,
    taxpayerCategory: comp.taxpayerCategory,
    optedRegime: comp.optedRegime,
    firmName: opts?.firmName ?? null,
    notes: comp.notes,
    comparison: comp.comparison,
    business: comp.businessComputation,
    extendedPersonal: comp.extendedPersonalComputation,
    trust: comp.trustComputation,
    tdsTcsCredit: comp.tdsTcsCredit,
    advanceTaxPaid: comp.advanceTaxPaid,
    selfAssessmentTax: comp.selfAssessmentTax,
  })
}

export function buildTaxComputationPreviewPdfBlocks(args: TaxPdfPreviewArgs): Block[] {
  const blocks: Block[] = []
  const formLabel = inferFormLabel(args)
  const preparedDate = new Intl.DateTimeFormat('en-IN', { dateStyle: 'medium' }).format(new Date())

  blocks.push({ kind: 'heading', level: 1, text: 'STATEMENT OF TOTAL INCOME & TAX COMPUTATION' })
  blocks.push({
    kind: 'paragraph',
    text: `Prepared in accordance with the Income-tax Act, 1961 • Assessment Year ${args.assessmentYear} (Previous Year ${args.financialYear})`,
    size: 9.5,
  })

  blocks.push({
    kind: 'table',
    header: ['Assessee Particulars', 'Details', 'Return & Filing Info', 'Details'],
    rows: [
      ['Assessee Name', args.clientName || 'Client Assessee', 'Applicable ITR Form', formLabel],
      ['PAN', args.clientPan || 'As per Client Master', 'Assessment Year / FY', `AY ${args.assessmentYear} / FY ${args.financialYear}`],
      ['Assessee Status', (args.taxpayerCategory || 'Individual').toUpperCase(), 'Opted / Selected Regime', (args.optedRegime || 'AUTO').toUpperCase()],
      ['Prepared By', args.firmName || 'CaDesk Practice Suite', 'Generated On', preparedDate],
    ],
  })

  if (args.trust) {
    appendTrustSections(blocks, args.trust)
  } else if (args.business) {
    appendBusinessSections(blocks, args.business)
  } else if (args.extendedPersonal) {
    appendPersonalExtendedSections(blocks, args.extendedPersonal)
  } else if (args.comparison) {
    appendBasicPersonalSections(blocks, args)
  }

  if (args.notes) {
    blocks.push({ kind: 'heading', level: 3, text: 'Notes & CA Working Remarks' })
    blocks.push({ kind: 'paragraph', text: args.notes, size: 9.5 })
  }

  blocks.push({ kind: 'spacer', height: 10 })
  blocks.push({
    kind: 'paragraph',
    text: 'Verification Note: Tax amounts are rounded u/s 288A/288B. Verify Form 26AS / AIS & Challan CIN prior to e-filing.',
    size: 8.5,
  })

  return blocks
}

function appendBusinessSections(blocks: Block[], biz: BusinessTaxComputationResultDto): void {
  const act = biz.activeSummary
  const isFirm = biz.entityType === 'firm'

  blocks.push({ kind: 'heading', level: 2, text: 'A. Book-to-Tax Reconciliation (Profits & Gains of Business or Profession)' })
  const pgbpRows: string[][] = [
    ['Total Revenue from Operations & Other Income', '', inr(biz.totalRevenue)],
    ['Book Profit Before Tax (P&L)', '', inr(biz.bookProfitBeforeTax)],
    ['Less: Income under other heads credited to P&L (HP / CG / OS)', inr(biz.otherHeadIncomesExcludedFromPl), ''],
    ['Add: Statutory Disallowances u/s 36 / 37 / 40(a) / 40A(3) / 43B / 43B(h)', inr(biz.totalDisallowancesAddedBack), ''],
    ['Add: Depreciation per Books', inr(biz.depreciationPerBooks), ''],
  ]
  if (isFirm) {
    pgbpRows.push(['Firm Book Profit u/s 40(b) Explanation 3', '', inr(biz.firmBookProfitUs40b)])
    pgbpRows.push(['  Max Allowable Partner Remuneration u/s 40(b)(v)', inr(biz.maxAllowableRemuneration40bv), ''])
    pgbpRows.push(['  Allowable Partner Remuneration Debited & Allowed', inr(biz.allowableRemuneration40b), ''])
    if (biz.disallowedRemuneration40b > 0 || biz.disallowedPartnerInterest40b > 0) {
      pgbpRows.push([
        'Add: Excess Partner Remuneration / Interest (>12%) disallowed u/s 40(b)',
        inr(biz.disallowedRemuneration40b + biz.disallowedPartnerInterest40b),
        '',
      ])
    }
  }
  pgbpRows.push(['Less: Income-tax Depreciation allowable u/s 32', inr(-biz.incomeTaxDepreciationSec32), ''])
  if (biz.broughtForwardLossSetOff > 0) {
    pgbpRows.push(['Less: Brought Forward Business Loss / Unabsorbed Depreciation set off', inr(-biz.broughtForwardLossSetOff), ''])
  }
  pgbpRows.push(['Net Taxable Income from Business / Profession (Schedule BP)', '', inr(biz.netPgbpIncome)])

  blocks.push({
    kind: 'table',
    header: ['Particulars', 'Working (Rs.)', 'Net (Rs.)'],
    align: ['left', 'right', 'right'],
    rows: pgbpRows,
  })

  blocks.push({ kind: 'heading', level: 2, text: 'B. Head-wise Summary & Total Taxable Income' })
  blocks.push({
    kind: 'table',
    header: ['Head of Income / Deduction', 'Amount (Rs.)'],
    align: ['left', 'right'],
    rows: [
      ['1. Income from House Property', inr(biz.housePropertyIncome)],
      ['2. Profits & Gains of Business or Profession', inr(biz.netPgbpIncome)],
      ['3. Capital Gains (STCG / LTCG)', inr(biz.capitalGainsTotal)],
      ['4. Income from Other Sources', inr(biz.otherSourcesIncome)],
      ['Gross Total Income (GTI)', inr(biz.grossTotalIncome)],
      ['Less: Chapter VI-A Deductions (80G / 80JJAA / 80M / Others)', inr(-biz.chapterViaAllowed)],
      ['Total Taxable Income (Rounded u/s 288A)', inr(biz.totalTaxableIncome)],
    ],
  })

  blocks.push({ kind: 'heading', level: 2, text: `C. Tax Liability (${act.regimeLabel}) & MAT / AMT Check` })
  blocks.push({
    kind: 'table',
    header: ['Particulars', 'Amount (Rs.)'],
    align: ['left', 'right'],
    rows: [
      ['Tax at Normal Corporate / Firm Rates', inr(act.taxAtNormalRates)],
      ['Tax at Special Rates (Cap Gains 111A / 112A / 112)', inr(act.taxAtSpecialRates)],
      ['Base Tax', inr(act.baseTax)],
      ['Surcharge (Net of Marginal Relief where applicable)', inr(act.surcharge)],
      ['Health & Education Cess @ 4%', inr(act.healthAndEducationCess)],
      ['Regular Tax Payable', inr(act.grossTaxBeforeMatAmt)],
      [
        isFirm
          ? `AMT u/s 115JC on Adjusted Total Income (${inr(biz.amtAdjustedTotalIncome115JC)})`
          : `MAT u/s 115JB on Book Profit (${inr(biz.matBookProfit115JB)})`,
        act.matOrAmtTotalTax > 0 ? inr(act.matOrAmtTotalTax) : 'Exempt / NA',
      ],
      ['MAT / AMT Credit Utilized', inr(-act.matOrAmtCreditUtilized)],
      ['Interest (234A/B/C) & Fee (234F)', inr(act.interestAndFee)],
      ['Total Tax & Interest Liability', inr(act.totalTaxLiability)],
      ['Less: Prepaid Taxes (TDS + TCS + Advance Tax + Self-Assessment)', inr(-act.prepaidTaxes)],
      [act.netTaxPayableOrRefund < 0 ? 'Net Refund Claimable' : 'Net Self-Assessment Tax Payable', inr(act.netTaxPayableOrRefund)],
    ],
  })

  if (biz.alternatives.length > 0) {
    blocks.push({ kind: 'heading', level: 2, text: 'D. Regime Evaluation Matrix' })
    blocks.push({
      kind: 'table',
      header: ['Regime', 'Eligible', 'Taxable Income', 'Gross + MAT/AMT', 'Total Liability', 'Net Payable / (Refund)'],
      align: ['left', 'center', 'right', 'right', 'right', 'right'],
      rows: biz.alternatives.map(alt => [
        alt.regimeLabel + (alt.regimeCode === biz.recommendedRegimeCode ? ' [Recommended]' : ''),
        alt.isEligible ? 'Yes' : 'No',
        inr(alt.taxableIncome),
        inr(Math.max(alt.grossTaxBeforeMatAmt, alt.matOrAmtTotalTax)),
        inr(alt.totalTaxLiability),
        inr(alt.netTaxPayableOrRefund),
      ]),
    })
  }
}

function appendPersonalExtendedSections(blocks: Block[], ext: PersonalExtendedTaxResultDto): void {
  const act = ext.activeSummary
  const inp = ext.normalizedInput

  if (ext.returnFormType === 'ITR-3') {
    blocks.push({ kind: 'heading', level: 2, text: `A. Proprietor / Professional Books Reconciliation (${inp.businessName})` })
    blocks.push({
      kind: 'table',
      header: ['Particulars', 'Amount (Rs.)'],
      align: ['left', 'right'],
      rows: [
        ['Gross Turnover / Professional Receipts', inr(inp.revenueFromOperations + inp.otherBusinessIncome)],
        ['Net Profit as per P&L Account (Before Tax)', inr(act.businessBookProfit)],
        ['Add: Statutory Disallowances (36 / 37 / 40(a) / 40A(3) / 43B / 43B(h) MSME)', inr(act.businessDisallowances)],
        ['Add: Depreciation debited to P&L', inr(act.depreciationBooks)],
        ['Less: Depreciation allowable u/s 32', inr(-act.depreciationIncomeTax32)],
        ['Less: Brought Forward Business Loss Set Off', inr(-inp.broughtForwardBusinessLoss)],
        ['Net Taxable Profits & Gains of Business / Profession', inr(act.businessIncomeNetPgbp)],
      ],
    })
  }

  blocks.push({ kind: 'heading', level: 2, text: 'Computation of Total Income (Five Heads & VDA u/s 115BBH)' })
  const headRows: string[][] = [
    ['1. Gross Salary / Pension Receipts', inr(act.grossSalary), ''],
    ['   Less: Deductions u/s 16 (Standard Deduction / Prof. Tax)', inr(-act.salaryDeductions), inr(act.netSalary)],
    ['2. Income from House Property (Net after 30% & Sec 24(b))', '', inr(act.housePropertyIncome)],
  ]
  if (ext.returnFormType === 'ITR-3' || act.businessIncomeNetPgbp !== 0) {
    headRows.push(['3. Profits & Gains of Business or Profession', '', inr(act.businessIncomeNetPgbp)])
  }
  headRows.push(
    ['4. Capital Gains & Virtual Digital Assets (VDA)', '', ''],
    ['   - STCG u/s 111A (Listed Equity / MFs @ 20%)', inr(act.stcg111A), ''],
    ['   - STCG Other Assets (At Slab Rates)', inr(act.stcgNormal), ''],
    ['   - LTCG u/s 112A (@ 12.5% above Rs. 1,25,000)', inr(act.ltcg112A), ''],
    ['   - LTCG u/s 112 Other Assets (@ 12.5%)', inr(act.ltcg112), ''],
    ['   - VDA / Crypto Income u/s 115BBH (@ 30% flat, no Ch.VI-A)', inr(act.vdaIncome115BBH), inr(act.totalCapitalGainsAndVda)],
    ['5. Income from Other Sources (Interest / Dividends / Misc)', '', inr(act.otherSourcesIncome)],
    ['Gross Total Income (GTI)', '', inr(act.grossTotalIncome)],
    ['Less: Chapter VI-A Deductions (80C / 80CCD(1B) / 80D / 80E / 80G / 80TTA)', '', inr(-act.chapterViaDeductions)],
    ['Total Taxable Income (Rounded u/s 288A)', '', inr(act.taxableIncome)],
  )
  blocks.push({
    kind: 'table',
    header: ['Head of Income / Deduction', 'Sub-schedule (Rs.)', 'Head Total (Rs.)'],
    align: ['left', 'right', 'right'],
    rows: headRows,
  })

  if (act.slabBreakdown.length > 0) {
    blocks.push({ kind: 'heading', level: 2, text: `Normal Slab Working (${act.regime.toUpperCase()} Regime)` })
    blocks.push({
      kind: 'table',
      header: ['Income Slab Range', 'Rate %', 'Income in Slab (Rs.)', 'Slab Tax (Rs.)'],
      align: ['left', 'right', 'right', 'right'],
      rows: act.slabBreakdown.map(s => [s.slabRange, `${s.ratePercent}%`, inr(s.taxableInSlab), inr(s.taxAmount)]),
    })
  }

  blocks.push({ kind: 'heading', level: 2, text: 'Tax Liability, Special Rates, Rebate, Surcharge & Prepaid Credits' })
  blocks.push({
    kind: 'table',
    header: ['Particulars', 'Amount (Rs.)'],
    align: ['left', 'right'],
    rows: [
      ['Slab Tax on Normal Income', inr(act.slabTaxOnNormalIncome)],
      ['Special Rate Tax: STCG 111A (20%) + LTCG 112A/112 (12.5%) + VDA 115BBH (30%)', inr(act.specialRateTax)],
      ['Less: Rebate u/s 87A (Normal income only; excluded for CG 111A/112A/VDA)', inr(-act.rebate87A)],
      ['Surcharge (with Marginal Relief if applicable)', inr(act.surcharge)],
      ['Health & Education Cess @ 4%', inr(act.cess)],
      ...(act.amtTotalTax115JC > 0 ? [[`AMT u/s 115JC Check (${act.isAmtApplied ? 'Applied' : 'Regular higher'})`, inr(act.amtTotalTax115JC)]] : []),
      ['Interest u/s 234A/B/C & Fee u/s 234F', inr(act.interestAndFee)],
      ['Total Tax & Interest Liability', inr(act.totalTaxLiability)],
      ['Less: TDS (Salary + Other) + TCS + Advance Tax + Self-Assessment Tax', inr(-act.prepaidTaxes)],
      [act.netPayableOrRefund < 0 ? 'Net Income Tax Refund Due' : 'Net Self-Assessment Tax Payable', inr(act.netPayableOrRefund)],
    ],
  })

  blocks.push({ kind: 'heading', level: 2, text: 'Old vs New Regime (Sec 115BAC) Comparison' })
  blocks.push({
    kind: 'table',
    header: ['Metric', 'Old Regime', 'New Regime (115BAC)', 'Optimal Choice'],
    align: ['left', 'right', 'right', 'left'],
    rows: [
      ['Gross Total Income', inr(ext.oldRegime.grossTotalIncome), inr(ext.newRegime.grossTotalIncome), ext.recommendedRegime.toUpperCase()],
      ['Chapter VI-A & Standard Deductions', inr(ext.oldRegime.chapterViaDeductions + ext.oldRegime.salaryDeductions), inr(ext.newRegime.chapterViaDeductions + ext.newRegime.salaryDeductions), `Saves ${inr(ext.taxSavings)}`],
      ['Total Taxable Income', inr(ext.oldRegime.taxableIncome), inr(ext.newRegime.taxableIncome), ext.recommendationReason],
      ['Total Tax Liability', inr(ext.oldRegime.totalTaxLiability), inr(ext.newRegime.totalTaxLiability), ext.selectedRegime === 'new' ? 'New Filed' : 'Old Filed'],
    ],
  })

  const faCount = inp.foreignAssets?.length ?? 0
  if (faCount > 0 || (inp.scheduleAl && act.grossTotalIncome > 5_000_000)) {
    blocks.push({ kind: 'heading', level: 3, text: 'Statutory Schedules Included' })
    const items: string[] = []
    if (faCount > 0) items.push(`Schedule FA: ${faCount} Foreign Financial / Asset account(s) disclosed u/s 139(1) 4th Proviso.`)
    if (inp.scheduleAl && act.grossTotalIncome > 5_000_000) items.push('Schedule AL: Assets & Liabilities disclosed (Gross Total Income exceeds Rs. 50 Lakhs).')
    blocks.push({ kind: 'list', items })
  }
}

function appendBasicPersonalSections(blocks: Block[], args: TaxPdfPreviewArgs): void {
  const cmp = args.comparison
  if (!cmp) return
  const act = args.optedRegime.toLowerCase() === 'old' ? cmp.oldRegime : cmp.newRegime
  const prepaid = (args.tdsTcsCredit ?? act.prepaidTaxes) + (args.advanceTaxPaid ?? 0) + (args.selfAssessmentTax ?? 0) - act.prepaidTaxes
  const prepaidTotal = act.prepaidTaxes > 0 ? act.prepaidTaxes : Math.max(0, prepaid)

  blocks.push({ kind: 'heading', level: 2, text: `A. Computation of Total Income (${act.regime.toUpperCase()} Regime)` })
  blocks.push({
    kind: 'table',
    header: ['Head of Income / Deduction', 'Details (Rs.)', 'Head Total (Rs.)'],
    align: ['left', 'right', 'right'],
    rows: [
      ['1. Gross Salary', inr(act.grossSalary), ''],
      ['   Less: Standard Deduction / Sec 16', inr(-act.standardDeduction), inr(act.netSalary)],
      ['2. Income from House Property', '', inr(act.housePropertyIncome)],
      ['3. Business / Presumptive Income (44AD/ADA)', '', inr(act.businessIncome)],
      ['4. Capital Gains (STCG + LTCG)', '', inr(act.capitalGainsStcg + act.capitalGainsLtcg)],
      ['5. Income from Other Sources', '', inr(act.otherSourcesIncome)],
      ['Gross Total Income', '', inr(act.grossTotalIncome)],
      ['Less: Chapter VI-A Deductions', '', inr(-act.chapterViaDeductions)],
      ['Total Taxable Income (Rounded u/s 288A)', '', inr(act.taxableIncome)],
    ],
  })

  if (act.slabBreakdown.length > 0) {
    blocks.push({ kind: 'heading', level: 2, text: 'B. Slab Rate Working' })
    blocks.push({
      kind: 'table',
      header: ['Slab Range', 'Rate %', 'Income in Slab (Rs.)', 'Tax (Rs.)'],
      align: ['left', 'right', 'right', 'right'],
      rows: act.slabBreakdown.map(s => [s.slabRange, `${s.ratePercent}%`, inr(s.taxableInSlab), inr(s.taxAmount)]),
    })
  }

  blocks.push({ kind: 'heading', level: 2, text: 'C. Tax Liability & Old vs New Regime Summary' })
  blocks.push({
    kind: 'table',
    header: ['Metric', 'Old Regime', 'New Regime (115BAC)', 'Selected Working'],
    align: ['left', 'right', 'right', 'right'],
    rows: [
      ['Taxable Income', inr(cmp.oldRegime.taxableIncome), inr(cmp.newRegime.taxableIncome), inr(act.taxableIncome)],
      ['Base + Special Rate Tax', inr(cmp.oldRegime.baseTax + cmp.oldRegime.specialRateTax), inr(cmp.newRegime.baseTax + cmp.newRegime.specialRateTax), inr(act.baseTax + act.specialRateTax)],
      ['Less: Rebate u/s 87A', inr(-cmp.oldRegime.rebate87A), inr(-cmp.newRegime.rebate87A), inr(-act.rebate87A)],
      ['Surcharge & 4% Cess', inr(cmp.oldRegime.surcharge + cmp.oldRegime.cess), inr(cmp.newRegime.surcharge + cmp.newRegime.cess), inr(act.surcharge + act.cess)],
      ['Total Tax Liability', inr(cmp.oldRegime.totalTaxLiability), inr(cmp.newRegime.totalTaxLiability), inr(act.totalTaxLiability)],
      ['Prepaid Taxes (TDS + TCS + Adv)', inr(cmp.oldRegime.prepaidTaxes), inr(cmp.newRegime.prepaidTaxes), inr(prepaidTotal)],
      ['Net Payable / (Refundable)', inr(cmp.oldRegime.netPayableOrRefund), inr(cmp.newRegime.netPayableOrRefund), inr(act.netPayableOrRefund)],
    ],
  })
  blocks.push({
    kind: 'paragraph',
    text: `Advisory: Recommended ${cmp.recommendedRegime.toUpperCase()} Regime (Saves ${inr(cmp.taxSavings)}) — ${cmp.recommendationReason}`,
    bold: true,
    size: 9.5,
  })
}

function appendTrustSections(blocks: Block[], trust: TrustTaxResultDto): void {
  blocks.push({
    kind: 'heading',
    level: 2,
    text: `A. Computation of Charitable Trust / Institution Income (u/s ${trust.returnFurnishedSection}, Exemption u/s ${trust.sectionExemptionClaimed})`,
  })
  blocks.push({
    kind: 'table',
    header: ['Particulars', 'Amount (Rs.)'],
    align: ['left', 'right'],
    rows: [
      ['Corpus Donations Exempt u/s 11(1)(d) (Excluded from Eligible Receipts)', inr(trust.corpusExempt11_1d)],
      ['Voluntary Contributions (Non-Corpus Govt/Corp/General + Foreign)', inr(trust.grossVoluntaryNonCorpus)],
      ['Aggregate Income from Trust Objects / Interest / Rent (Schedule AI)', inr(trust.otherObjectAndInvestmentReceipts)],
      ['Total Eligible Receipts u/s 11 / 12AB', inr(trust.totalEligibleIncome)],
      ['Less: 15% Unconditional Statutory Accumulation u/s 11(1)(a)', inr(-trust.statutoryAccumulation15Pct)],
      ['Minimum 85% Income Required to be Applied', inr(trust.required85PctApplication)],
      ['Less: Revenue & Capital Charitable Application Allowed (Schedule A)', inr(-trust.netAllowedApplication)],
      ['Less: Specific Accumulation u/s 11(2) (Form 10)', inr(-trust.form10Accumulation)],
      ['Net Taxable Shortfall at Normal / Slab Rates', inr(trust.shortfallTaxableNormalIncome)],
      ['Add: Taxable Anonymous Donations u/s 115BBC (@ 30%)', inr(trust.anonymousDonationsTaxable115BBC)],
      ['Add: Specified Violations Income u/s 115BBI (@ 30%)', inr(trust.specifiedIncome115BBI)],
      ['Total Taxable Income (ITR-7 Part B-TI)', inr(trust.totalTaxableIncome)],
    ],
  })

  blocks.push({ kind: 'heading', level: 2, text: 'B. Tax Computation (Part B-TTI)' })
  blocks.push({
    kind: 'table',
    header: ['Particulars', 'Amount (Rs.)'],
    align: ['left', 'right'],
    rows: [
      ['Tax at Normal Rates on Application Shortfall', inr(trust.taxOnNormalShortfall)],
      ['Tax @ 30% on Excess Anonymous Donations u/s 115BBC', inr(trust.taxOnAnonymous115BBC)],
      ['Tax @ 30% on Specified Violations u/s 115BBI', inr(trust.taxOn115BBI)],
      ['Surcharge + 4% Health & Education Cess', inr(trust.surcharge + trust.cess)],
      ['Interest u/s 234A/B/C & Fee u/s 234F', inr(trust.interestAndFee)],
      ['Total Tax & Interest Liability', inr(trust.totalTaxLiability)],
      ['Less: Prepaid Taxes (TDS + TCS + Advance Tax)', inr(-trust.prepaidTaxes)],
      [trust.netPayableOrRefund < 0 ? 'Net Refund Due' : 'Net Self-Assessment Tax Payable', inr(trust.netPayableOrRefund)],
    ],
  })
}
