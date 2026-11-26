import React, { useEffect, useMemo, useState } from 'react'
import {
  schedule3Api,
  type Schedule3AssessmentDto,
  type Schedule3FinancialPackDto,
} from '../api'
import { PdfActions } from '../pdf/PdfActions'
import {
  buildCmaReportPdfBlocks,
  computeCmaProjections,
  type CmaAssumptionInput,
  type CmaBaseActuals,
} from './cmaPdf'

const fmtLakhs = (n: number) =>
  new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(n / 100_000)

const inputStyle: React.CSSProperties = {
  width: '100%',
  padding: '6px 8px',
  borderRadius: '4px',
  border: '1px solid var(--border)',
  background: 'var(--card)',
  color: 'var(--text)',
  fontSize: '12px',
}

const labelStyle: React.CSSProperties = {
  display: 'block',
  fontSize: '11px',
  color: 'var(--muted)',
  marginBottom: '3px',
  fontWeight: 600,
}

type Props = {
  assessment: Schedule3AssessmentDto
  canSave?: boolean
}

export function CmaBuilderPanel({ assessment, canSave = true }: Props) {
  const [pack, setPack] = useState<Schedule3FinancialPackDto | null>(null)
  const [assump, setAssump] = useState<CmaAssumptionInput>({
    clientName: assessment.clientName || 'Assessee Company',
    baseYear: assessment.financialYear,
    revenueGrowthPercent: 18,
    cogsPercentOfRevenue: 62,
    opexPercentOfRevenue: 18,
    interestRatePercent: 10.5,
    taxRatePercent: 25.17,
    proposedTermLoan: 15_000_000,
    proposedWcLimit: 25_000_000,
    annualTermLoanRepayment: 3_000_000,
    debtorDays: 60,
    inventoryDays: 45,
    creditorDays: 35,
  })

  useEffect(() => {
    let active = true
    setAssump(prev => ({
      ...prev,
      clientName: assessment.clientName || 'Assessee Company',
      baseYear: assessment.financialYear,
    }))
    schedule3Api.getFinancialPackData(assessment.id)
      .then(res => { if (active) setPack(res) })
      .catch(() => { if (active) setPack(null) })
    return () => { active = false }
  }, [assessment.id, assessment.clientName, assessment.financialYear])

  const baseActuals: CmaBaseActuals = useMemo(() => {
    if (pack) {
      const c = pack.currentYear
      const rev = Math.max(1, c.revenueFromOperations || assessment.revenueFromOperationsCurrent)
      const cogs = c.costOfMaterialsConsumed + c.purchasesOfStockInTrade + c.changesInInventories
      const opex = c.employeeBenefitsExpense + c.otherExpenses
      return {
        revenue: rev,
        cogs: cogs > 0 ? cogs : Math.round(rev * 0.62),
        opex: opex > 0 ? opex : Math.round(rev * 0.18),
        depreciation: c.depreciationAndAmortization || Math.round(rev * 0.03),
        financeCost: c.financeCosts || Math.round(assessment.debtServiceCurrent * 0.5),
        equityAndReserves: (c.shareCapital + c.reservesAndSurplus) || assessment.shareholdersEquityCurrent,
        termDebt: c.longTermBorrowings || Math.round(assessment.totalDebtCurrent * 0.6),
        wcBorrowings: c.shortTermBorrowings || Math.round(assessment.totalDebtCurrent * 0.4),
        fixedAssetsNet: (c.propertyPlantEquipment + c.capitalWip) || Math.round(assessment.capitalEmployedCurrent * 0.45),
        inventories: c.inventories || assessment.averageInventoryCurrent,
        receivables: c.tradeReceivables || assessment.averageTradeReceivablesCurrent,
        cashAndBank: c.cashAndBank || Math.round(assessment.currentAssetsCurrent * 0.15),
        payables: (c.tradePayablesMsme + c.tradePayablesOthers) || assessment.averageTradePayablesCurrent,
      }
    }
    const rev = Math.max(1, assessment.revenueFromOperationsCurrent)
    return {
      revenue: rev,
      cogs: assessment.costOfGoodsSoldCurrent || Math.round(rev * 0.62),
      opex: Math.round(rev * 0.18),
      depreciation: Math.round(rev * 0.03),
      financeCost: Math.round(assessment.debtServiceCurrent * 0.5),
      equityAndReserves: assessment.shareholdersEquityCurrent,
      termDebt: Math.round(assessment.totalDebtCurrent * 0.6),
      wcBorrowings: Math.round(assessment.totalDebtCurrent * 0.4),
      fixedAssetsNet: Math.round(assessment.capitalEmployedCurrent * 0.45),
      inventories: assessment.averageInventoryCurrent,
      receivables: assessment.averageTradeReceivablesCurrent,
      cashAndBank: Math.round(assessment.currentAssetsCurrent * 0.15),
      payables: assessment.averageTradePayablesCurrent,
    }
  }, [pack, assessment])

  const projections = useMemo(() => computeCmaProjections(baseActuals, assump), [baseActuals, assump])
  const avgDscr = useMemo(
    () => projections.slice(1).reduce((s, y) => s + y.dscr, 0) / Math.max(1, projections.length - 1),
    [projections],
  )

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '10px', marginBottom: '14px' }}>
        <div>
          <h3 style={{ margin: 0, fontSize: '16px', color: 'var(--text)' }}>
            CMA Data &amp; Bank Loan Working Capital Assessment (Tandon / Nayak Committee)
          </h3>
          <p style={{ margin: '3px 0 0', fontSize: '12px', color: 'var(--muted)' }}>
            {pack?.sourceNote || 'Auto-seeded from Schedule III financial baseline.'} Figures displayed in Rs. Lakhs.
          </p>
        </div>
        <PdfActions
          title={`CMA Data Report - ${assump.clientName} - FY ${assessment.financialYear}`}
          options={{
            author: 'CaDesk Practice Suite',
            header: `${assump.clientName} • Bank CMA Projections`,
            footer: `Base FY ${assessment.financialYear} • Average DSCR ${avgDscr.toFixed(2)}x`,
          }}
          source={() => buildCmaReportPdfBlocks(assump, projections)}
          clientId={assessment.clientId}
          fy={assessment.financialYear}
          canSave={canSave}
        />
      </div>

      {/* Assumptions Bar */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '10px', padding: '12px', background: 'var(--card-subtle)', borderRadius: '6px', border: '1px solid var(--border)', marginBottom: '14px' }}>
        <div>
          <label style={labelStyle}>Revenue Growth (% p.a.)</label>
          <input type="number" step="0.5" value={assump.revenueGrowthPercent} onChange={e => setAssump({ ...assump, revenueGrowthPercent: Number(e.target.value) })} style={inputStyle} />
        </div>
        <div>
          <label style={labelStyle}>COGS (% of Sales)</label>
          <input type="number" step="0.5" value={assump.cogsPercentOfRevenue} onChange={e => setAssump({ ...assump, cogsPercentOfRevenue: Number(e.target.value) })} style={inputStyle} />
        </div>
        <div>
          <label style={labelStyle}>Proposed WC Limit (Rs.)</label>
          <input type="number" value={assump.proposedWcLimit} onChange={e => setAssump({ ...assump, proposedWcLimit: Number(e.target.value) })} style={inputStyle} />
        </div>
        <div>
          <label style={labelStyle}>Proposed Term Loan (Rs.)</label>
          <input type="number" value={assump.proposedTermLoan} onChange={e => setAssump({ ...assump, proposedTermLoan: Number(e.target.value) })} style={inputStyle} />
        </div>
        <div>
          <label style={labelStyle}>Interest Rate (% p.a.)</label>
          <input type="number" step="0.25" value={assump.interestRatePercent} onChange={e => setAssump({ ...assump, interestRatePercent: Number(e.target.value) })} style={inputStyle} />
        </div>
        <div>
          <label style={labelStyle}>Debtor Days / Inv Days</label>
          <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px' }}>
            <input type="number" value={assump.debtorDays} onChange={e => setAssump({ ...assump, debtorDays: Number(e.target.value) })} style={inputStyle} />
            <input type="number" value={assump.inventoryDays} onChange={e => setAssump({ ...assump, inventoryDays: Number(e.target.value) })} style={inputStyle} />
          </div>
        </div>
      </div>

      {/* 5-Year CMA Table */}
      <div style={{ overflowX: 'auto', border: '1px solid var(--border)', borderRadius: '6px' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
          <thead>
            <tr style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', color: 'var(--text-secondary)' }}>
              <th style={{ padding: '8px 10px', textAlign: 'left' }}>CMA Particular (Rs. in Lakhs)</th>
              {projections.map(y => (
                <th key={y.label} style={{ padding: '8px 10px', textAlign: 'right' }}>{y.label}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {[
              { label: 'Gross Sales / Revenue', vals: projections.map(y => fmtLakhs(y.revenue)), bold: true },
              { label: 'EBITDA', vals: projections.map(y => fmtLakhs(y.ebitda)) },
              { label: 'Profit After Tax (PAT)', vals: projections.map(y => fmtLakhs(y.pat)), bold: true },
              { label: 'Cash Accruals (PAT + Dep)', vals: projections.map(y => fmtLakhs(y.cashAccrual)) },
              { label: 'Tangible Net Worth (TNW)', vals: projections.map(y => fmtLakhs(y.netWorth)) },
              { label: 'Total Outside Liabilities (TOL)', vals: projections.map(y => fmtLakhs(y.totalOutsideLiabilities)) },
              { label: 'MPBF — Tandon Method I (75% WCG)', vals: projections.map(y => fmtLakhs(y.mpbfMethod1)), accent: true },
              { label: 'MPBF — Tandon Method II (25% NWC)', vals: projections.map(y => fmtLakhs(y.mpbfMethod2)), accent: true },
              { label: 'Nayak Committee Limit (20% Sales)', vals: projections.map(y => fmtLakhs(y.nayakLimit20Pct)) },
              { label: 'Current Ratio (CA / CL)', vals: projections.map(y => `${y.currentRatio.toFixed(2)}x`) },
              { label: 'TOL / TNW Ratio', vals: projections.map(y => `${y.tolTnwRatio.toFixed(2)}x`) },
              { label: 'DSCR (Debt Service Coverage)', vals: projections.map(y => `${y.dscr.toFixed(2)}x`), bold: true },
            ].map(row => (
              <tr key={row.label} style={{ borderBottom: '1px solid var(--border)', fontWeight: row.bold ? 700 : 400 }}>
                <td style={{ padding: '7px 10px', color: row.accent ? 'var(--accent)' : 'var(--text)' }}>{row.label}</td>
                {row.vals.map((v, i) => (
                  <td key={i} style={{ padding: '7px 10px', textAlign: 'right', color: 'var(--text)' }}>{v}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
