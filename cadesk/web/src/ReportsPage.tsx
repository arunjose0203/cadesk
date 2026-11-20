import React, { useState, useEffect } from 'react'
import {
  reportsApi,
  type PracticeExecutiveSummaryDto,
  type ClientProfitabilityDto,
  type StaffUtilizationDto,
  type ReceivablesAgingDto,
  type ComplianceSlaReportDto,
  type RevenueBreakdownDto,
  type SetPracticeTargetRequest
} from './api'

interface ReportsPageProps {
  can?: (p: string) => boolean
}

export const ReportsPage: React.FC<ReportsPageProps> = ({ can }) => {
  const [activeTab, setActiveTab] = useState<'overview' | 'aging' | 'profitability' | 'utilization' | 'sla'>('overview')
  const [financialYear, setFinancialYear] = useState<string>('2026-27')
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Report Data
  const [summary, setSummary] = useState<PracticeExecutiveSummaryDto | null>(null)
  const [aging, setAging] = useState<ReceivablesAgingDto | null>(null)
  const [profitability, setProfitability] = useState<ClientProfitabilityDto[]>([])
  const [utilization, setUtilization] = useState<StaffUtilizationDto[]>([])
  const [slaReport, setSlaReport] = useState<ComplianceSlaReportDto | null>(null)
  const [revenueBreakdown, setRevenueBreakdown] = useState<RevenueBreakdownDto[]>([])

  // Modal
  const [showTargetModal, setShowTargetModal] = useState<boolean>(false)
  const [targetForm, setTargetForm] = useState<SetPracticeTargetRequest>({
    financialYear: '2026-27',
    targetAnnualRevenue: 5000000,
    targetBillableHours: 2400,
    targetRecoveryPercentage: 85,
    targetComplianceSlaPct: 95
  })

  useEffect(() => {
    loadAllReports()
  }, [financialYear])

  const loadAllReports = async () => {
    setLoading(true)
    setError(null)
    try {
      const [sum, ag, prof, util, sla, rev] = await Promise.all([
        reportsApi.getExecutiveSummary(financialYear),
        reportsApi.getReceivablesAging(),
        reportsApi.getClientProfitability(financialYear),
        reportsApi.getStaffUtilization(),
        reportsApi.getComplianceSla(financialYear),
        reportsApi.getRevenueBreakdown(financialYear)
      ])
      setSummary(sum)
      setAging(ag)
      setProfitability(prof)
      setUtilization(util)
      setSlaReport(sla)
      setRevenueBreakdown(rev)

      if (sum.targets) {
        setTargetForm({
          financialYear: sum.targets.financialYear,
          targetAnnualRevenue: sum.targets.targetAnnualRevenue,
          targetBillableHours: sum.targets.targetBillableHours,
          targetRecoveryPercentage: sum.targets.targetRecoveryPercentage,
          targetComplianceSlaPct: sum.targets.targetComplianceSlaPct
        })
      }
    } catch (e: any) {
      setError(e.message || 'Failed to load practice reports')
    } finally {
      setLoading(false)
    }
  }

  const handleSaveTargets = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      await reportsApi.setTarget({
        ...targetForm,
        financialYear
      })
      setSuccess('Practice targets updated successfully!')
      setShowTargetModal(false)
      loadAllReports()
    } catch (e: any) {
      setError(e.message || 'Failed to update targets')
    }
  }

  const formatCurrency = (val?: number | null) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 0
    }).format(val || 0)
  }

  return (
    <div style={{ padding: '20px', maxWidth: '1350px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px' }}>
        <div>
          <h1 style={{ margin: '0 0 6px 0', fontSize: '24px', fontWeight: 700 }}>
            Practice MIS & Executive Reporting Desk
          </h1>
          <p style={{ margin: 0, color: 'var(--muted, #666)', fontSize: '14px' }}>
            Firm Analytics, Client Margins, Staff Utilization, Receivables Aging & Statutory SLAs (Sprint 21)
          </p>
        </div>
        <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{ fontSize: '13px', fontWeight: 600 }}>FY:</span>
            <select
              value={financialYear}
              onChange={e => setFinancialYear(e.target.value)}
              style={{ padding: '6px 12px', borderRadius: '4px', border: '1px solid var(--border, #ccc)', fontWeight: 600 }}
            >
              <option value="2026-27">FY 2026-27 (Current)</option>
              <option value="2025-26">FY 2025-26</option>
              <option value="2024-25">FY 2024-25</option>
            </select>
          </div>
          {(!can || can('reports.manage')) && (
            <button className="secondary" onClick={() => setShowTargetModal(true)}>
              🎯 Annual Targets
            </button>
          )}
          <button className="ghost small" onClick={() => window.print()}>
            🖨️ Print MIS
          </button>
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div style={{ padding: '12px', background: '#ffebee', color: '#c62828', borderRadius: '6px', marginBottom: '16px' }}>
          {error}
        </div>
      )}
      {success && (
        <div style={{ padding: '12px', background: '#e8f5e9', color: '#2e7d32', borderRadius: '6px', marginBottom: '16px' }}>
          {success}
        </div>
      )}

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '2px solid var(--border, #e0e0e0)', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('overview')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'overview' ? 700 : 400,
            borderBottom: activeTab === 'overview' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'overview' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Executive Overview
        </button>
        <button
          onClick={() => setActiveTab('aging')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'aging' ? 700 : 400,
            borderBottom: activeTab === 'aging' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'aging' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Receivables Aging ({aging ? aging.agedInvoices.length : 0})
        </button>
        <button
          onClick={() => setActiveTab('profitability')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'profitability' ? 700 : 400,
            borderBottom: activeTab === 'profitability' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'profitability' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Client Profitability ({profitability.length})
        </button>
        <button
          onClick={() => setActiveTab('utilization')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'utilization' ? 700 : 400,
            borderBottom: activeTab === 'utilization' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'utilization' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Team Utilization ({utilization.length})
        </button>
        <button
          onClick={() => setActiveTab('sla')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'sla' ? 700 : 400,
            borderBottom: activeTab === 'sla' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'sla' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Compliance SLAs
        </button>
      </div>

      {loading && !summary ? (
        <div style={{ padding: '40px', textAlign: 'center', color: 'var(--muted, #666)' }}>Loading practice reports...</div>
      ) : null}

      {/* Tab 1: Executive Overview */}
      {activeTab === 'overview' && summary && (
        <div>
          {/* Executive KPI Grid */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '16px', marginBottom: '24px' }}>
            <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
              <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Total Billings (YTD)</div>
              <div style={{ fontSize: '26px', fontWeight: 700, color: '#1976d2', marginTop: '4px' }}>
                {formatCurrency(summary.totalBilledYtd)}
              </div>
              {summary.targets && summary.targets.targetAnnualRevenue > 0 && (
                <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--muted, #666)' }}>
                  Target: {formatCurrency(summary.targets.targetAnnualRevenue)} ({Math.min(100, Math.round((summary.totalBilledYtd / summary.targets.targetAnnualRevenue) * 100))}%)
                </div>
              )}
            </div>

            <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
              <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Collections & Recovery</div>
              <div style={{ fontSize: '26px', fontWeight: 700, color: '#2e7d32', marginTop: '4px' }}>
                {formatCurrency(summary.totalCollectedYtd)}
              </div>
              <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--muted, #666)' }}>
                Realization Rate: <strong>{summary.overallRealizationRatePct}%</strong> (Target: {summary.targets?.targetRecoveryPercentage || 85}%)
              </div>
            </div>

            <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
              <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Outstanding Receivables</div>
              <div style={{ fontSize: '26px', fontWeight: 700, color: summary.totalOutstandingReceivables > 0 ? '#d32f2f' : '#2e7d32', marginTop: '4px' }}>
                {formatCurrency(summary.totalOutstandingReceivables)}
              </div>
              <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--muted, #666)' }}>
                Aged Debtors: {aging ? aging.agedInvoices.length : 0} open invoices
              </div>
            </div>

            <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
              <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Staff Billable Utilization</div>
              <div style={{ fontSize: '26px', fontWeight: 700, color: '#e65100', marginTop: '4px' }}>
                {summary.staffUtilizationPct}%
              </div>
              <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--muted, #666)' }}>
                {summary.totalBillableHoursLogged}h billable / {summary.totalNonBillableHoursLogged}h admin
              </div>
            </div>

            <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
              <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Filing SLA Compliance</div>
              <div style={{ fontSize: '26px', fontWeight: 700, color: summary.slaCompliancePct >= 90 ? '#2e7d32' : '#d32f2f', marginTop: '4px' }}>
                {summary.slaCompliancePct}%
              </div>
              <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--muted, #666)' }}>
                {summary.filedOnTimeCount} on time | {summary.overdueCount} overdue
              </div>
            </div>

            <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
              <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Client Practice Base</div>
              <div style={{ fontSize: '26px', fontWeight: 700, marginTop: '4px' }}>
                {summary.activeClientsCount} Clients
              </div>
              <div style={{ marginTop: '8px', fontSize: '12px', color: 'var(--muted, #666)' }}>
                Avg Revenue / Client: <strong>{formatCurrency(summary.averageRevenuePerClient)}</strong>
              </div>
            </div>
          </div>

          {/* Revenue by Practice Domain */}
          <div className="card" style={{ padding: '24px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)', marginBottom: '24px' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Revenue Contribution by Practice Service Domain</h3>
            {revenueBreakdown.length === 0 ? (
              <p style={{ color: 'var(--muted, #666)' }}>No service invoices recorded in {financialYear}.</p>
            ) : (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '16px' }}>
                {revenueBreakdown.map((rev, idx) => (
                  <div key={idx} style={{ background: '#f8f9fa', padding: '16px', borderRadius: '6px', borderLeft: '4px solid #1976d2' }}>
                    <div style={{ fontWeight: 600, fontSize: '14px' }}>{rev.serviceCategory}</div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '8px' }}>
                      <span style={{ fontSize: '18px', fontWeight: 700 }}>{formatCurrency(rev.amount)}</span>
                      <span style={{ padding: '2px 8px', borderRadius: '12px', background: '#e3f2fd', color: '#1976d2', fontWeight: 600, fontSize: '12px' }}>
                        {rev.percentageOfTotal}%
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Tab 2: Receivables Aging */}
      {activeTab === 'aging' && aging && (
        <div>
          {/* Aging Buckets Banner */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '20px' }}>
            <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid #c8e6c9', background: '#f1f8e9' }}>
              <div style={{ fontSize: '13px', color: '#2e7d32', fontWeight: 600 }}>0 - 30 Days (Current)</div>
              <div style={{ fontSize: '22px', fontWeight: 700, color: '#1b5e20', marginTop: '4px' }}>
                {formatCurrency(aging.current0To30Days)}
              </div>
            </div>

            <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid #bbdefb', background: '#e3f2fd' }}>
              <div style={{ fontSize: '13px', color: '#1565c0', fontWeight: 600 }}>31 - 60 Days</div>
              <div style={{ fontSize: '22px', fontWeight: 700, color: '#0d47a1', marginTop: '4px' }}>
                {formatCurrency(aging.bucket31To60Days)}
              </div>
            </div>

            <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid #ffe0b2', background: '#fff3e0' }}>
              <div style={{ fontSize: '13px', color: '#e65100', fontWeight: 600 }}>61 - 90 Days</div>
              <div style={{ fontSize: '22px', fontWeight: 700, color: '#bf360c', marginTop: '4px' }}>
                {formatCurrency(aging.bucket61To90Days)}
              </div>
            </div>

            <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid #ffcdd2', background: '#ffebee' }}>
              <div style={{ fontSize: '13px', color: '#c62828', fontWeight: 600 }}>&gt; 90 Days (Overdue Alert)</div>
              <div style={{ fontSize: '22px', fontWeight: 700, color: '#b71c1c', marginTop: '4px' }}>
                {formatCurrency(aging.bucketAbove90Days)}
              </div>
            </div>
          </div>

          {/* Aged Invoices Table */}
          <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Open Receivables Schedule</h3>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: '#f4f5f7', borderBottom: '2px solid #ddd' }}>
                  <th style={{ padding: '10px' }}>Invoice #</th>
                  <th style={{ padding: '10px' }}>Client</th>
                  <th style={{ padding: '10px' }}>Issue Date</th>
                  <th style={{ padding: '10px' }}>Due Date</th>
                  <th style={{ padding: '10px' }}>Age (Days)</th>
                  <th style={{ padding: '10px', textAlign: 'right' }}>Total Amount</th>
                  <th style={{ padding: '10px', textAlign: 'right' }}>Balance Due</th>
                  <th style={{ padding: '10px' }}>Aging Bucket</th>
                </tr>
              </thead>
              <tbody>
                {aging.agedInvoices.length === 0 ? (
                  <tr>
                    <td colSpan={8} style={{ padding: '24px', textAlign: 'center', color: '#2e7d32', fontWeight: 600 }}>
                      ✓ All client invoices are fully paid! No outstanding receivables.
                    </td>
                  </tr>
                ) : (
                  aging.agedInvoices.map(inv => (
                    <tr key={inv.invoiceId} style={{ borderBottom: '1px solid #eee' }}>
                      <td style={{ padding: '10px', fontWeight: 600 }}>{inv.invoiceNumber}</td>
                      <td style={{ padding: '10px' }}>{inv.clientName}</td>
                      <td style={{ padding: '10px' }}>{inv.issueDate}</td>
                      <td style={{ padding: '10px' }}>{inv.dueDate}</td>
                      <td style={{ padding: '10px', fontWeight: 600 }}>{inv.ageDays} days</td>
                      <td style={{ padding: '10px', textAlign: 'right' }}>{formatCurrency(inv.totalAmount)}</td>
                      <td style={{ padding: '10px', textAlign: 'right', fontWeight: 700, color: '#d32f2f' }}>
                        {formatCurrency(inv.balanceDue)}
                      </td>
                      <td style={{ padding: '10px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 600,
                            background: inv.ageDays > 90 ? '#ffebee' : (inv.ageDays > 60 ? '#fff3e0' : (inv.ageDays > 30 ? '#e3f2fd' : '#e8f5e9')),
                            color: inv.ageDays > 90 ? '#c62828' : (inv.ageDays > 60 ? '#e65100' : (inv.ageDays > 30 ? '#1565c0' : '#2e7d32'))
                          }}
                        >
                          {inv.agingBucket}
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 3: Client Profitability */}
      {activeTab === 'profitability' && (
        <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
          <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Client Profitability & Effective Hourly Realization</h3>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
            <thead>
              <tr style={{ background: '#f4f5f7', borderBottom: '2px solid #ddd' }}>
                <th style={{ padding: '10px' }}>Client Name</th>
                <th style={{ padding: '10px' }}>Entity</th>
                <th style={{ padding: '10px', textAlign: 'right' }}>Total Invoiced</th>
                <th style={{ padding: '10px', textAlign: 'right' }}>Collected</th>
                <th style={{ padding: '10px', textAlign: 'right' }}>Outstanding</th>
                <th style={{ padding: '10px', textAlign: 'center' }}>Staff Hours</th>
                <th style={{ padding: '10px', textAlign: 'right' }}>Effective Rate (₹/hr)</th>
                <th style={{ padding: '10px' }}>Margin Tier</th>
              </tr>
            </thead>
            <tbody>
              {profitability.length === 0 ? (
                <tr>
                  <td colSpan={8} style={{ padding: '24px', textAlign: 'center', color: 'var(--muted, #666)' }}>
                    No client billing records found for {financialYear}.
                  </td>
                </tr>
              ) : (
                profitability.map(c => (
                  <tr key={c.clientId} style={{ borderBottom: '1px solid #eee' }}>
                    <td style={{ padding: '10px', fontWeight: 600 }}>{c.clientName}</td>
                    <td style={{ padding: '10px' }}>{c.entityType || 'General'}</td>
                    <td style={{ padding: '10px', textAlign: 'right', fontWeight: 600 }}>{formatCurrency(c.totalInvoiced)}</td>
                    <td style={{ padding: '10px', textAlign: 'right', color: '#2e7d32' }}>{formatCurrency(c.totalPaid)}</td>
                    <td style={{ padding: '10px', textAlign: 'right', color: c.balanceDue > 0 ? '#d32f2f' : 'inherit' }}>
                      {formatCurrency(c.balanceDue)}
                    </td>
                    <td style={{ padding: '10px', textAlign: 'center' }}>{c.totalHoursLogged} hrs</td>
                    <td style={{ padding: '10px', textAlign: 'right', fontWeight: 700 }}>{formatCurrency(c.effectiveHourlyRate)}/hr</td>
                    <td style={{ padding: '10px' }}>
                      <span
                        style={{
                          padding: '3px 8px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 600,
                          background: c.profitabilityRating === 'HighMargin' ? '#e8f5e9' : (c.profitabilityRating === 'MediumMargin' ? '#e3f2fd' : '#ffebee'),
                          color: c.profitabilityRating === 'HighMargin' ? '#2e7d32' : (c.profitabilityRating === 'MediumMargin' ? '#1565c0' : '#c62828')
                        }}
                      >
                        {c.profitabilityRating === 'HighMargin' ? '★ High Margin' : (c.profitabilityRating === 'MediumMargin' ? 'Standard' : '⚠️ At Risk')}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Tab 4: Team Utilization */}
      {activeTab === 'utilization' && (
        <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
          <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Staff Utilization & Billable Productivity</h3>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
            <thead>
              <tr style={{ background: '#f4f5f7', borderBottom: '2px solid #ddd' }}>
                <th style={{ padding: '10px' }}>Team Member</th>
                <th style={{ padding: '10px' }}>Role</th>
                <th style={{ padding: '10px', textAlign: 'center' }}>Billable Hours</th>
                <th style={{ padding: '10px', textAlign: 'center' }}>Admin / Non-Billable</th>
                <th style={{ padding: '10px', textAlign: 'center' }}>Total Hours</th>
                <th style={{ padding: '10px', width: '200px' }}>Utilization Rate (%)</th>
                <th style={{ padding: '10px', textAlign: 'right' }}>Billed Value Generated</th>
              </tr>
            </thead>
            <tbody>
              {utilization.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: '24px', textAlign: 'center', color: 'var(--muted, #666)' }}>
                    No staff timesheet records found.
                  </td>
                </tr>
              ) : (
                utilization.map(u => (
                  <tr key={u.userId} style={{ borderBottom: '1px solid #eee' }}>
                    <td style={{ padding: '10px', fontWeight: 600 }}>{u.staffName}</td>
                    <td style={{ padding: '10px' }}>
                      <span style={{ padding: '2px 8px', borderRadius: '4px', background: '#f0f4f8', fontSize: '11px' }}>
                        {u.roleName}
                      </span>
                    </td>
                    <td style={{ padding: '10px', textAlign: 'center', fontWeight: 600, color: '#1976d2' }}>
                      {u.billableHours} hrs
                    </td>
                    <td style={{ padding: '10px', textAlign: 'center', color: 'var(--muted, #666)' }}>
                      {u.nonBillableHours} hrs
                    </td>
                    <td style={{ padding: '10px', textAlign: 'center' }}>{u.totalHours} hrs</td>
                    <td style={{ padding: '10px' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <div style={{ flex: 1, background: '#eee', height: '8px', borderRadius: '4px', overflow: 'hidden' }}>
                          <div
                            style={{
                              width: `${Math.min(100, u.utilizationPct)}%`,
                              height: '100%',
                              background: u.utilizationPct >= 75 ? '#2e7d32' : (u.utilizationPct >= 50 ? '#1976d2' : '#e65100')
                            }}
                          />
                        </div>
                        <span style={{ fontWeight: 600, fontSize: '12px', minWidth: '40px' }}>{u.utilizationPct}%</span>
                      </div>
                    </td>
                    <td style={{ padding: '10px', textAlign: 'right', fontWeight: 700, color: '#2e7d32' }}>
                      {formatCurrency(u.billedValueGenerated)}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Tab 5: Compliance SLAs */}
      {activeTab === 'sla' && slaReport && (
        <div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '20px' }}>
            <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid #e0e0e0' }}>
              <div style={{ fontSize: '13px', color: 'var(--muted, #666)' }}>Total Statutory Filings</div>
              <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px' }}>{slaReport.totalStatutoryFilings}</div>
            </div>
            <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid #e0e0e0' }}>
              <div style={{ fontSize: '13px', color: '#2e7d32' }}>Filed on Time</div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: '#2e7d32', marginTop: '4px' }}>{slaReport.filedOnTime}</div>
            </div>
            <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid #e0e0e0' }}>
              <div style={{ fontSize: '13px', color: '#e65100' }}>Filed Late (Delayed)</div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: '#e65100', marginTop: '4px' }}>{slaReport.filedLate}</div>
            </div>
            <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid #e0e0e0' }}>
              <div style={{ fontSize: '13px', color: '#c62828' }}>Currently Overdue</div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: '#c62828', marginTop: '4px' }}>{slaReport.currentlyOverdue}</div>
            </div>
            <div className="card" style={{ padding: '16px', borderRadius: '8px', border: '1px solid #e0e0e0' }}>
              <div style={{ fontSize: '13px', color: '#1976d2' }}>Overall SLA Score</div>
              <div style={{ fontSize: '24px', fontWeight: 700, color: '#1976d2', marginTop: '4px' }}>{slaReport.overallSlaPercentage}%</div>
            </div>
          </div>

          <div className="card" style={{ padding: '20px', borderRadius: '8px', border: '1px solid var(--border, #e0e0e0)' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Filing SLA Performance by Regulatory Stream</h3>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: '#f4f5f7', borderBottom: '2px solid #ddd' }}>
                  <th style={{ padding: '10px' }}>Regulatory Stream</th>
                  <th style={{ padding: '10px', textAlign: 'center' }}>Total Filings</th>
                  <th style={{ padding: '10px', textAlign: 'center', color: '#2e7d32' }}>On Time</th>
                  <th style={{ padding: '10px', textAlign: 'center', color: '#e65100' }}>Delayed</th>
                  <th style={{ padding: '10px', textAlign: 'center', color: '#c62828' }}>Overdue</th>
                  <th style={{ padding: '10px', width: '220px' }}>SLA Performance (%)</th>
                </tr>
              </thead>
              <tbody>
                {slaReport.categoryBreakdown.length === 0 ? (
                  <tr>
                    <td colSpan={6} style={{ padding: '24px', textAlign: 'center', color: 'var(--muted, #666)' }}>
                      No statutory filings scheduled for {financialYear}.
                    </td>
                  </tr>
                ) : (
                  slaReport.categoryBreakdown.map(cat => (
                    <tr key={cat.category} style={{ borderBottom: '1px solid #eee' }}>
                      <td style={{ padding: '10px', fontWeight: 600 }}>{cat.category}</td>
                      <td style={{ padding: '10px', textAlign: 'center' }}>{cat.totalCount}</td>
                      <td style={{ padding: '10px', textAlign: 'center', color: '#2e7d32', fontWeight: 600 }}>{cat.filedOnTime}</td>
                      <td style={{ padding: '10px', textAlign: 'center', color: '#e65100' }}>{cat.filedLate}</td>
                      <td style={{ padding: '10px', textAlign: 'center', color: '#c62828', fontWeight: 600 }}>{cat.overdue}</td>
                      <td style={{ padding: '10px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                          <div style={{ flex: 1, background: '#eee', height: '8px', borderRadius: '4px', overflow: 'hidden' }}>
                            <div
                              style={{
                                width: `${Math.min(100, cat.slaPercentage)}%`,
                                height: '100%',
                                background: cat.slaPercentage >= 95 ? '#2e7d32' : (cat.slaPercentage >= 80 ? '#1976d2' : '#c62828')
                              }}
                            />
                          </div>
                          <span style={{ fontWeight: 600, fontSize: '12px' }}>{cat.slaPercentage}%</span>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Targets Modal */}
      {showTargetModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div className="card" style={{ background: '#fff', padding: '24px', borderRadius: '8px', width: '480px', maxWidth: '90%' }}>
            <h3 style={{ margin: '0 0 16px 0' }}>Configure Practice Annual Targets ({financialYear})</h3>
            <form onSubmit={handleSaveTargets}>
              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Target Annual Billings (₹)</label>
                <input
                  type="number"
                  value={targetForm.targetAnnualRevenue}
                  onChange={e => setTargetForm({ ...targetForm, targetAnnualRevenue: parseFloat(e.target.value) || 0 })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Target Billable Hours (Firm Total)</label>
                <input
                  type="number"
                  value={targetForm.targetBillableHours}
                  onChange={e => setTargetForm({ ...targetForm, targetBillableHours: parseFloat(e.target.value) || 0 })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Target Recovery %</label>
                  <input
                    type="number"
                    step="0.1"
                    value={targetForm.targetRecoveryPercentage}
                    onChange={e => setTargetForm({ ...targetForm, targetRecoveryPercentage: parseFloat(e.target.value) || 0 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: 600, marginBottom: '4px' }}>Target SLA %</label>
                  <input
                    type="number"
                    step="0.1"
                    value={targetForm.targetComplianceSlaPct}
                    onChange={e => setTargetForm({ ...targetForm, targetComplianceSlaPct: parseFloat(e.target.value) || 0 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button type="button" className="ghost" onClick={() => setShowTargetModal(false)}>Cancel</button>
                <button type="submit" className="primary">Save Targets</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
