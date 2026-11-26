import { test } from 'node:test'
import assert from 'node:assert/strict'
import { renderPdf } from '../src/pdf/pdf.ts'
import { buildSchedule3FinancialPackBlocks } from '../src/schedule3/schedule3FinancialPackPdf.ts'
import { buildCmaReportPdfBlocks, computeCmaProjections } from '../src/schedule3/cmaPdf.ts'
import type { Schedule3AssessmentDto, Schedule3FinancialPackDto } from '../src/api.ts'

test('builds Statutory Schedule III Financial Statements Pack PDF (BS, P&L, AS-3 Cash Flow, Ratios & Ageing)', () => {
  const assessment: Schedule3AssessmentDto = {
    id: '33333333-3333-3333-3333-333333333333',
    clientId: '44444444-4444-4444-4444-444444444444',
    clientName: 'Malabar Spice Exports Pvt Ltd',
    financialYear: '2025-26',
    precedingFinancialYear: '2024-25',
    accountingStandard: 'Division I - AS (Non-Ind AS)',
    roundingUnit: 'Lakhs',
    totalIncomeCurrent: 120_000_000,
    totalIncomePreceding: 95_000_000,
    currentAssetsCurrent: 45_000_000,
    currentAssetsPreceding: 35_000_000,
    currentLiabilitiesCurrent: 20_000_000,
    currentLiabilitiesPreceding: 18_000_000,
    totalDebtCurrent: 22_000_000,
    totalDebtPreceding: 25_000_000,
    shareholdersEquityCurrent: 55_000_000,
    shareholdersEquityPreceding: 45_000_000,
    netProfitAfterTaxesCurrent: 10_000_000,
    netProfitAfterTaxesPreceding: 7_500_000,
    ebitCurrent: 15_500_000,
    ebitPreceding: 11_500_000,
    debtServiceCurrent: 5_000_000,
    debtServicePreceding: 5_000_000,
    costOfGoodsSoldCurrent: 72_000_000,
    costOfGoodsSoldPreceding: 58_000_000,
    averageInventoryCurrent: 14_000_000,
    averageInventoryPreceding: 11_000_000,
    revenueFromOperationsCurrent: 118_000_000,
    revenueFromOperationsPreceding: 94_000_000,
    averageTradeReceivablesCurrent: 18_000_000,
    averageTradeReceivablesPreceding: 14_000_000,
    netCreditPurchasesCurrent: 68_000_000,
    netCreditPurchasesPreceding: 55_000_000,
    averageTradePayablesCurrent: 11_000_000,
    averageTradePayablesPreceding: 9_000_000,
    capitalEmployedCurrent: 77_000_000,
    capitalEmployedPreceding: 70_000_000,
    incomeFromInvestmentsCurrent: 500_000,
    incomeFromInvestmentsPreceding: 400_000,
    averageInvestmentsCurrent: 5_000_000,
    averageInvestmentsPreceding: 4_000_000,
    isRoundingRuleCompliant: true,
    roundingRuleRecommendation: 'Compliant (Turnover < 100 Cr -> Lakhs)',
    significantVariancesCount: 1,
    missingExplanationsCount: 0,
    assessmentStatus: 'finalized',
    createdAt: '2026-09-01T10:00:00Z',
    ratios: [
      {
        id: '1', assessmentId: '33333333-3333-3333-3333-333333333333',
        ratioCode: 'CR', ratioName: 'Current Ratio',
        numeratorDescription: 'CA', denominatorDescription: 'CL',
        currentYearValue: 2.25, precedingYearValue: 1.94,
        variancePercent: 15.9, isVarianceSignificant: false, isExplanationProvided: false,
      },
    ],
    ageingItems: [
      {
        id: '2', assessmentId: '33333333-3333-3333-3333-333333333333',
        scheduleType: 'TradeReceivables', particularsCategory: 'Undisputed - Considered Good',
        less6MonthsOr1Year: 16_000_000, oneToTwoYears: 2_000_000, twoToThreeYears: 0, moreThanThreeYears: 0, unbilledDues: 0, total: 18_000_000,
      },
    ],
  }

  const pack: Schedule3FinancialPackDto = {
    assessmentId: assessment.id,
    clientId: assessment.clientId,
    clientName: 'Malabar Spice Exports Pvt Ltd',
    cin: 'U15400KL2018PTC054321',
    pan: 'AABCM1234E',
    financialYear: '2025-26',
    precedingFinancialYear: '2024-25',
    accountingStandard: 'Division I - AS (Non-Ind AS)',
    roundingUnit: 'Lakhs',
    derivedFromTrialBalance: true,
    sourceNote: 'Derived from synced Trial Balance',
    currentYear: {
      shareCapital: 10_000_000, reservesAndSurplus: 45_000_000,
      longTermBorrowings: 14_000_000, deferredTaxLiabilities: 1_000_000, otherLongTermLiabilities: 0, longTermProvisions: 0,
      shortTermBorrowings: 8_000_000, tradePayablesMsme: 2_000_000, tradePayablesOthers: 9_000_000, otherCurrentLiabilities: 1_000_000, shortTermProvisions: 0,
      propertyPlantEquipment: 40_000_000, capitalWip: 0, nonCurrentInvestments: 5_000_000, deferredTaxAssets: 0, longTermLoansAndAdvances: 0, otherNonCurrentAssets: 0,
      currentInvestments: 0, inventories: 14_000_000, tradeReceivables: 18_000_000, cashAndBank: 13_000_000, shortTermLoansAndAdvances: 0, otherCurrentAssets: 0,
      revenueFromOperations: 118_000_000, otherIncome: 2_000_000, costOfMaterialsConsumed: 72_000_000, purchasesOfStockInTrade: 0, changesInInventories: 0,
      employeeBenefitsExpense: 18_000_000, financeCosts: 2_200_000, depreciationAndAmortization: 3_500_000, otherExpenses: 11_000_000, taxExpense: 3_300_000,
    },
    precedingYear: {
      shareCapital: 10_000_000, reservesAndSurplus: 35_000_000,
      longTermBorrowings: 16_000_000, deferredTaxLiabilities: 1_000_000, otherLongTermLiabilities: 0, longTermProvisions: 0,
      shortTermBorrowings: 9_000_000, tradePayablesMsme: 1_500_000, tradePayablesOthers: 7_500_000, otherCurrentLiabilities: 1_000_000, shortTermProvisions: 0,
      propertyPlantEquipment: 42_000_000, capitalWip: 0, nonCurrentInvestments: 4_000_000, deferredTaxAssets: 0, longTermLoansAndAdvances: 0, otherNonCurrentAssets: 0,
      currentInvestments: 0, inventories: 11_000_000, tradeReceivables: 14_000_000, cashAndBank: 10_000_000, shortTermLoansAndAdvances: 0, otherCurrentAssets: 0,
      revenueFromOperations: 94_000_000, otherIncome: 1_000_000, costOfMaterialsConsumed: 58_000_000, purchasesOfStockInTrade: 0, changesInInventories: 0,
      employeeBenefitsExpense: 14_000_000, financeCosts: 2_500_000, depreciationAndAmortization: 3_200_000, otherExpenses: 7_300_000, taxExpense: 2_500_000,
    },
  }

  const blocks = buildSchedule3FinancialPackBlocks({ assessment, pack, firmName: 'Menon & Associates, CA', frn: '004321S' })
  const bytes = renderPdf(blocks, { title: 'Schedule III Financial Pack - Malabar Spice Exports' })
  assert.equal(String.fromCharCode(...bytes.slice(0, 8)), '%PDF-1.4')
  assert.ok(bytes.length > 4000)
})

test('computes 5-year CMA MPBF Tandon/Nayak Committee & DSCR and builds valid PDF', () => {
  const assump = {
    clientName: 'Malabar Spice Exports Pvt Ltd',
    baseYear: '2025-26',
    revenueGrowthPercent: 20,
    cogsPercentOfRevenue: 60,
    opexPercentOfRevenue: 18,
    interestRatePercent: 10,
    taxRatePercent: 25.17,
    proposedTermLoan: 15_000_000,
    proposedWcLimit: 25_000_000,
    annualTermLoanRepayment: 3_000_000,
    debtorDays: 55,
    inventoryDays: 45,
    creditorDays: 35,
  }
  const years = computeCmaProjections({
    revenue: 100_000_000, cogs: 60_000_000, opex: 18_000_000, depreciation: 3_000_000, financeCost: 2_000_000,
    equityAndReserves: 50_000_000, termDebt: 10_000_000, wcBorrowings: 15_000_000,
    fixedAssetsNet: 40_000_000, inventories: 12_000_000, receivables: 15_000_000, cashAndBank: 5_000_000, payables: 8_000_000,
  }, assump)
  assert.equal(years.length, 5)
  assert.equal(years[1].revenue, 120_000_000)
  assert.ok(years[1].mpbfMethod1 > 0)
  assert.ok(years[1].nayakLimit20Pct === 24_000_000)
  assert.ok(years[1].dscr > 1.0)

  const bytes = renderPdf(buildCmaReportPdfBlocks(assump, years), { title: 'CMA Report' })
  assert.equal(String.fromCharCode(...bytes.slice(0, 8)), '%PDF-1.4')
  assert.ok(bytes.length > 3000)
})
