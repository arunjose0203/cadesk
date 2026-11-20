import { useEffect, useState } from 'react'
import {
  type CaroAssessmentDto,
  type CaroSummaryDto,
  type CaroClauseResponseDto,
  type Client,
  caroApi,
  api
} from './api'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0)

type ClientPage = {
  items: Client[]
  total: number
}

export function CaroAuditDeskPage({ can }: { can: (p: string) => boolean }) {
  const [assessments, setAssessments] = useState<CaroAssessmentDto[]>([])
  const [summary, setSummary] = useState<CaroSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Filters
  const [filterFy, setFilterFy] = useState('2024-25')
  const [filterStatus, setFilterStatus] = useState('all')

  // Selected assessment workbench
  const [selectedAssessment, setSelectedAssessment] = useState<CaroAssessmentDto | null>(null)
  const [activeTab, setActiveTab] = useState<'applicability' | 'clauses' | 'annexure'>('clauses')
  const [clauseCategoryFilter, setClauseCategoryFilter] = useState('all')
  const [editingClause, setEditingClause] = useState<CaroClauseResponseDto | null>(null)

  // Modals
  const [showNewModal, setShowNewModal] = useState(false)

  // New assessment form
  const [newForm, setNewForm] = useState({
    clientId: '',
    financialYear: '2024-25',
    companyType: 'Private',
    isHoldingOrSubsidiaryOfPublic: false,
    paidUpCapitalAndReserves: 5000000,
    totalBorrowings: 2000000,
    totalRevenue: 30000000,
    auditorConclusion: ''
  })

  useEffect(() => {
    loadData()
    loadClients()
  }, [filterFy, filterStatus])

  async function loadData() {
    setLoading(true)
    setError(null)
    try {
      const [list, sum] = await Promise.all([
        caroApi.getAssessments(undefined, filterFy, filterStatus),
        caroApi.getSummary()
      ])
      setAssessments(list)
      setSummary(sum)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load CARO assessments.')
    } finally {
      setLoading(false)
    }
  }

  async function loadClients() {
    try {
      const res = await api<ClientPage>('/api/clients?take=200')
      setClients(res.items || [])
    } catch {
      // Ignore
    }
  }

  async function handleOpenWorkbench(a: CaroAssessmentDto) {
    setSelectedAssessment(a)
    setActiveTab(a.isCaroApplicable ? 'clauses' : 'applicability')
    setEditingClause(null)
  }

  async function handleCreateAssessment(e: React.FormEvent) {
    e.preventDefault()
    if (!newForm.clientId) {
      alert('Please select a company client.')
      return
    }

    try {
      const created = await caroApi.createAssessment({
        clientId: newForm.clientId,
        financialYear: newForm.financialYear,
        companyType: newForm.companyType,
        isHoldingOrSubsidiaryOfPublic: newForm.isHoldingOrSubsidiaryOfPublic,
        paidUpCapitalAndReserves: Number(newForm.paidUpCapitalAndReserves),
        totalBorrowings: Number(newForm.totalBorrowings),
        totalRevenue: Number(newForm.totalRevenue),
        auditorConclusion: newForm.auditorConclusion
      })
      setShowNewModal(false)
      await loadData()
      handleOpenWorkbench(created)
      alert('CARO 2020 assessment created successfully!')
    } catch (err: unknown) {
      alert(`Creation failed: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleUpdateApplicability(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedAssessment) return

    try {
      const updated = await caroApi.updateAssessment(selectedAssessment.id, {
        clientId: selectedAssessment.clientId,
        engagementId: selectedAssessment.engagementId,
        financialYear: selectedAssessment.financialYear,
        companyType: selectedAssessment.companyType,
        isHoldingOrSubsidiaryOfPublic: selectedAssessment.isHoldingOrSubsidiaryOfPublic,
        paidUpCapitalAndReserves: Number(selectedAssessment.paidUpCapitalAndReserves),
        totalBorrowings: Number(selectedAssessment.totalBorrowings),
        totalRevenue: Number(selectedAssessment.totalRevenue),
        auditorConclusion: selectedAssessment.auditorConclusion
      })
      setSelectedAssessment(updated)
      await loadData()
      alert('Applicability assessment updated & re-evaluated!')
    } catch (err: unknown) {
      alert(`Update failed: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleSaveClause(clause: CaroClauseResponseDto) {
    if (!selectedAssessment) return
    try {
      const updated = await caroApi.updateClause(selectedAssessment.id, clause.id, {
        status: clause.status,
        auditorRemark: clause.auditorRemark,
        hasDiscrepancy: clause.hasDiscrepancy,
        discrepancyAmount: clause.discrepancyAmount,
        supportingWpsRef: clause.supportingWpsRef
      })
      setSelectedAssessment(updated)
      setEditingClause(null)
      await loadData()
      alert(`Clause ${clause.clauseNumber} updated successfully!`)
    } catch (err: unknown) {
      alert(`Clause save failed: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleFinalize(id: string) {
    const remarks = prompt('Enter final audit remarks / sign-off conclusion for CARO 2020 Annexure:', 'Reported in accordance with Companies (Auditor\'s Report) Order, 2020 and ICAI Guidance Note.')
    if (!remarks) return

    try {
      const updated = await caroApi.finalizeAssessment(id, remarks)
      if (selectedAssessment && selectedAssessment.id === id) setSelectedAssessment(updated)
      await loadData()
      alert('CARO 2020 assessment & reporting annexure finalized successfully!')
    } catch (err: unknown) {
      alert(`Finalization failed: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  // Filter clauses for display
  const displayedClauses = selectedAssessment?.clauseResponses?.filter(c => {
    if (clauseCategoryFilter === 'all') return true
    return c.category === clauseCategoryFilter
  }) || []

  return (
    <div style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px 0', color: '#0f172a' }}>
            CARO 2020 Automated Audit Checklist &amp; Disclosures Desk
          </h1>
          <p style={{ margin: 0, color: '#64748b', fontSize: '14px' }}>
            Section 143(11) Companies (Auditor&apos;s Report) Order, 2020: 3-pillar applicability evaluation, 21-clause statutory verification checklist, and auditor&apos;s report Annexure generator.
          </p>
        </div>
        {can('caro.manage') && (
          <button
            onClick={() => setShowNewModal(true)}
            style={{
              background: '#0284c7',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              padding: '10px 18px',
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '8px'
            }}
          >
            <span>+</span> New CARO Assessment
          </button>
        )}
      </div>

      {/* KPI Summary Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>TOTAL ASSESSMENTS</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>{summary.totalAssessments}</div>
            <div style={{ fontSize: '11px', color: '#64748b', marginTop: '4px' }}>
              Applicable: {summary.applicableCount} • Exempt: {summary.exemptCount}
            </div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>CARO APPLICABLE RATIO</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#0284c7', marginTop: '4px' }}>
              {summary.totalAssessments > 0 ? `${Math.round((summary.applicableCount / summary.totalAssessments) * 100)}%` : '0%'}
            </div>
            <div style={{ fontSize: '11px', color: '#64748b', marginTop: '4px' }}>Finalized: {summary.finalizedCount}</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>QUALIFICATIONS FLAGGED</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: summary.totalQualifications > 0 ? '#b45309' : '#15803d', marginTop: '4px' }}>
              {summary.totalQualifications}
            </div>
            <div style={{ fontSize: '11px', color: '#64748b', marginTop: '4px' }}>Qualified CARO reporting clauses</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>ADVERSE REMARKS</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: summary.totalAdverseRemarks > 0 ? '#dc2626' : '#15803d', marginTop: '4px' }}>
              {summary.totalAdverseRemarks}
            </div>
            <div style={{ fontSize: '11px', color: '#64748b', marginTop: '4px' }}>Severe statutory non-compliances</div>
          </div>
        </div>
      )}

      {/* Filter Bar */}
      <div style={{ display: 'flex', gap: '12px', background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '14px', marginBottom: '20px', alignItems: 'center', flexWrap: 'wrap' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <label style={{ fontSize: '13px', fontWeight: 600, color: '#475569' }}>FY:</label>
          <select value={filterFy} onChange={e => setFilterFy(e.target.value)} style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}>
            <option value="2024-25">2024-25</option>
            <option value="2023-24">2023-24</option>
            <option value="2025-26">2025-26</option>
            <option value="all">All FYs</option>
          </select>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <label style={{ fontSize: '13px', fontWeight: 600, color: '#475569' }}>Status:</label>
          <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)} style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}>
            <option value="all">All Statuses</option>
            <option value="draft">Draft</option>
            <option value="in_review">In Review</option>
            <option value="finalized">Finalized</option>
          </select>
        </div>
        <button onClick={loadData} style={{ marginLeft: 'auto', padding: '6px 14px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '6px', cursor: 'pointer', fontSize: '13px', fontWeight: 600 }}>
          Refresh
        </button>
      </div>

      {error && <div style={{ background: '#fef2f2', border: '1px solid #fecaca', color: '#b91c1c', padding: '12px 16px', borderRadius: '6px', marginBottom: '20px' }}>{error}</div>}

      {/* Assessments Table */}
      <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
          <thead style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0', color: '#475569' }}>
            <tr>
              <th style={{ padding: '12px 16px' }}>Company Client</th>
              <th style={{ padding: '12px 16px' }}>Type</th>
              <th style={{ padding: '12px 16px' }}>FY</th>
              <th style={{ padding: '12px 16px' }}>Paid-up Capital &amp; Reserves</th>
              <th style={{ padding: '12px 16px' }}>Borrowings</th>
              <th style={{ padding: '12px 16px' }}>Total Revenue</th>
              <th style={{ padding: '12px 16px' }}>CARO Status</th>
              <th style={{ padding: '12px 16px' }}>Qualifications</th>
              <th style={{ padding: '12px 16px' }}>Report Status</th>
              <th style={{ padding: '12px 16px', textAlign: 'right' }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={10} style={{ padding: '24px', textAlign: 'center', color: '#64748b' }}>Loading CARO assessments...</td>
              </tr>
            ) : assessments.length === 0 ? (
              <tr>
                <td colSpan={10} style={{ padding: '32px', textAlign: 'center', color: '#64748b' }}>
                  No CARO assessments found. Click &quot;+ New CARO Assessment&quot; to begin an order applicability assessment.
                </td>
              </tr>
            ) : (
              assessments.map(a => (
                <tr key={a.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                  <td style={{ padding: '12px 16px', fontWeight: 600, color: '#0f172a' }}>{a.clientName || 'Company'}</td>
                  <td style={{ padding: '12px 16px' }}>
                    <span style={{ fontSize: '11px', padding: '2px 6px', borderRadius: '4px', background: '#f1f5f9', color: '#334155' }}>
                      {a.companyType} {a.isHoldingOrSubsidiaryOfPublic ? '(Deemed Public)' : ''}
                    </span>
                  </td>
                  <td style={{ padding: '12px 16px' }}>{a.financialYear}</td>
                  <td style={{ padding: '12px 16px' }}>{fmtInr(a.paidUpCapitalAndReserves)}</td>
                  <td style={{ padding: '12px 16px' }}>{fmtInr(a.totalBorrowings)}</td>
                  <td style={{ padding: '12px 16px' }}>{fmtInr(a.totalRevenue)}</td>
                  <td style={{ padding: '12px 16px' }}>
                    {a.isCaroApplicable ? (
                      <span style={{ padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 700, background: '#dcfce7', color: '#15803d' }}>
                        ✓ Applicable
                      </span>
                    ) : (
                      <span style={{ padding: '3px 8px', borderRadius: '4px', fontSize: '11px', fontWeight: 700, background: '#f1f5f9', color: '#64748b' }} title={a.exemptionReason || ''}>
                        Exempt (1(2))
                      </span>
                    )}
                  </td>
                  <td style={{ padding: '12px 16px' }}>
                    {a.qualificationsCount > 0 || a.adverseRemarksCount > 0 ? (
                      <span style={{ padding: '2px 6px', borderRadius: '4px', fontSize: '11px', fontWeight: 600, background: '#fef3c7', color: '#b45309' }}>
                        ⚠️ {a.qualificationsCount} Qual • {a.adverseRemarksCount} Adv
                      </span>
                    ) : (
                      <span style={{ color: '#15803d', fontSize: '11px', fontWeight: 600 }}>0 Issues</span>
                    )}
                  </td>
                  <td style={{ padding: '12px 16px' }}>
                    <span style={{
                      padding: '2px 8px',
                      borderRadius: '12px',
                      fontSize: '11px',
                      fontWeight: 600,
                      textTransform: 'capitalize',
                      background: a.status === 'finalized' ? '#dcfce7' : a.status === 'in_review' ? '#fef3c7' : '#f1f5f9',
                      color: a.status === 'finalized' ? '#15803d' : a.status === 'in_review' ? '#b45309' : '#475569'
                    }}>
                      {a.status}
                    </span>
                  </td>
                  <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                    <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                      <button
                        onClick={() => handleOpenWorkbench(a)}
                        style={{ padding: '5px 10px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '4px', fontSize: '12px', cursor: 'pointer', fontWeight: 600 }}
                      >
                        Workbench
                      </button>
                      {can('caro.finalize') && a.status !== 'finalized' && a.isCaroApplicable && (
                        <button
                          onClick={() => handleFinalize(a.id)}
                          style={{ padding: '5px 10px', background: '#10b981', color: '#fff', border: 'none', borderRadius: '4px', fontSize: '12px', cursor: 'pointer', fontWeight: 600 }}
                        >
                          Finalize
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* CARO Assessment Workbench Drawer / Modal */}
      {selectedAssessment && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100, padding: '20px' }}>
          <div style={{ background: '#fff', borderRadius: '12px', width: '100%', maxWidth: '1100px', maxHeight: '90vh', display: 'flex', flexDirection: 'column', overflow: 'hidden', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            {/* Modal Header */}
            <div style={{ padding: '16px 24px', borderBottom: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#f8fafc' }}>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: '#0f172a' }}>
                    {selectedAssessment.clientName} • CARO 2020 Desk
                  </h2>
                  <span style={{ padding: '2px 8px', borderRadius: '4px', background: '#e0f2fe', color: '#0369a1', fontWeight: 600, fontSize: '11px' }}>
                    FY {selectedAssessment.financialYear}
                  </span>
                  <span style={{
                    padding: '2px 8px',
                    borderRadius: '12px',
                    fontSize: '11px',
                    fontWeight: 600,
                    background: selectedAssessment.status === 'finalized' ? '#dcfce7' : '#f1f5f9',
                    color: selectedAssessment.status === 'finalized' ? '#15803d' : '#475569'
                  }}>
                    {selectedAssessment.status.toUpperCase()}
                  </span>
                </div>
                <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>
                  {selectedAssessment.isCaroApplicable ? (
                    <span style={{ color: '#15803d', fontWeight: 600 }}>✓ CARO 2020 Applicable</span>
                  ) : (
                    <span style={{ color: '#64748b' }}>Exempt from CARO 2020</span>
                  )} • {selectedAssessment.qualificationsCount} Qualification(s) • {selectedAssessment.adverseRemarksCount} Adverse Remark(s)
                </div>
              </div>
              <button onClick={() => setSelectedAssessment(null)} style={{ background: 'transparent', border: 'none', fontSize: '20px', cursor: 'pointer', color: '#94a3b8' }}>✕</button>
            </div>

            {/* Sub-tab Navigation */}
            <div style={{ display: 'flex', borderBottom: '1px solid #e2e8f0', background: '#fff', padding: '0 24px' }}>
              <button
                onClick={() => setActiveTab('applicability')}
                style={{ padding: '12px 18px', border: 'none', background: 'transparent', borderBottom: activeTab === 'applicability' ? '2px solid #0284c7' : '2px solid transparent', color: activeTab === 'applicability' ? '#0284c7' : '#64748b', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
              >
                1. Applicability &amp; Thresholds
              </button>
              <button
                onClick={() => setActiveTab('clauses')}
                style={{ padding: '12px 18px', border: 'none', background: 'transparent', borderBottom: activeTab === 'clauses' ? '2px solid #0284c7' : '2px solid transparent', color: activeTab === 'clauses' ? '#0284c7' : '#64748b', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
              >
                2. 21-Clause Verification Checklist ({selectedAssessment.clauseResponses?.length || 0})
              </button>
              <button
                onClick={() => setActiveTab('annexure')}
                style={{ padding: '12px 18px', border: 'none', background: 'transparent', borderBottom: activeTab === 'annexure' ? '2px solid #0284c7' : '2px solid transparent', color: activeTab === 'annexure' ? '#0284c7' : '#64748b', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
              >
                3. Auditor&apos;s Report Annexure &lsquo;A&rsquo;
              </button>
            </div>

            {/* Modal Body */}
            <div style={{ padding: '20px 24px', overflowY: 'auto', flex: 1 }}>
              {/* TAB 1: Applicability & Thresholds */}
              {activeTab === 'applicability' && (
                <form onSubmit={handleUpdateApplicability}>
                  <div style={{
                    padding: '16px',
                    borderRadius: '8px',
                    marginBottom: '20px',
                    background: selectedAssessment.isCaroApplicable ? '#f0fdf4' : '#f8fafc',
                    border: `1px solid ${selectedAssessment.isCaroApplicable ? '#bbf7d0' : '#e2e8f0'}`
                  }}>
                    <h3 style={{ fontSize: '15px', fontWeight: 700, margin: '0 0 6px 0', color: selectedAssessment.isCaroApplicable ? '#15803d' : '#334155' }}>
                      {selectedAssessment.isCaroApplicable ? '✓ CARO 2020 Order Applies' : 'ℹ️ CARO 2020 Exemption Granted'}
                    </h3>
                    <p style={{ fontSize: '12px', margin: 0, color: '#475569' }}>
                      {selectedAssessment.exemptionReason}
                    </p>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px', marginBottom: '16px' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>Company Legal Type</label>
                      <select
                        value={selectedAssessment.companyType}
                        onChange={e => setSelectedAssessment({ ...selectedAssessment, companyType: e.target.value })}
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                        disabled={selectedAssessment.status === 'finalized'}
                      >
                        <option value="Private">Private Limited Company</option>
                        <option value="Public">Public Limited Company</option>
                        <option value="Section8">Section 8 Company (Charitable)</option>
                        <option value="SmallCompany">Small Company (Section 2(85))</option>
                        <option value="OPC">One Person Company (OPC)</option>
                        <option value="Banking">Banking Company</option>
                        <option value="Insurance">Insurance Company</option>
                        <option value="NBFC">Non-Banking Financial Company (NBFC)</option>
                      </select>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', paddingTop: '20px' }}>
                      <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', fontWeight: 600, color: '#334155', cursor: 'pointer' }}>
                        <input
                          type="checkbox"
                          checked={selectedAssessment.isHoldingOrSubsidiaryOfPublic}
                          onChange={e => setSelectedAssessment({ ...selectedAssessment, isHoldingOrSubsidiaryOfPublic: e.target.checked })}
                          disabled={selectedAssessment.status === 'finalized'}
                        />
                        Holding or Subsidiary Company of a Public Company
                      </label>
                    </div>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '20px' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>
                        Paid-Up Capital &amp; Reserves (₹)
                      </label>
                      <input
                        type="number"
                        value={selectedAssessment.paidUpCapitalAndReserves}
                        onChange={e => setSelectedAssessment({ ...selectedAssessment, paidUpCapitalAndReserves: Number(e.target.value) })}
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                        disabled={selectedAssessment.status === 'finalized'}
                      />
                      <span style={{ fontSize: '11px', color: '#64748b' }}>Exemption limit: &le; ₹1,00,00,000 (₹1 Cr)</span>
                    </div>

                    <div>
                      <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>
                        Total Borrowings from Banks/FIs (₹)
                      </label>
                      <input
                        type="number"
                        value={selectedAssessment.totalBorrowings}
                        onChange={e => setSelectedAssessment({ ...selectedAssessment, totalBorrowings: Number(e.target.value) })}
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                        disabled={selectedAssessment.status === 'finalized'}
                      />
                      <span style={{ fontSize: '11px', color: '#64748b' }}>Exemption limit: &le; ₹1,00,00,000 at any time</span>
                    </div>

                    <div>
                      <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>
                        Total Revenue as per Schedule III (₹)
                      </label>
                      <input
                        type="number"
                        value={selectedAssessment.totalRevenue}
                        onChange={e => setSelectedAssessment({ ...selectedAssessment, totalRevenue: Number(e.target.value) })}
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                        disabled={selectedAssessment.status === 'finalized'}
                      />
                      <span style={{ fontSize: '11px', color: '#64748b' }}>Exemption limit: &le; ₹10,00,00,000 (₹10 Cr)</span>
                    </div>
                  </div>

                  {can('caro.manage') && selectedAssessment.status !== 'finalized' && (
                    <button type="submit" style={{ padding: '8px 18px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}>
                      Re-Evaluate Applicability
                    </button>
                  )}
                </form>
              )}

              {/* TAB 2: 21-Clause Verification Checklist */}
              {activeTab === 'clauses' && (
                <div>
                  {/* Category Filter Pills */}
                  <div style={{ display: 'flex', gap: '8px', marginBottom: '16px', flexWrap: 'wrap' }}>
                    {['all', 'PPE & Intangibles', 'Inventory & Working Capital', 'Loans, Investments & Guarantees', 'Related Parties & Deposits', 'Statutory Dues', 'Borrowings & Going Concern', 'Fraud & Internal Controls', 'Regulatory & Group Reporting'].map(cat => (
                      <button
                        key={cat}
                        onClick={() => setClauseCategoryFilter(cat)}
                        style={{
                          padding: '5px 10px',
                          borderRadius: '20px',
                          border: '1px solid #cbd5e1',
                          fontSize: '11px',
                          fontWeight: 600,
                          cursor: 'pointer',
                          background: clauseCategoryFilter === cat ? '#0284c7' : '#fff',
                          color: clauseCategoryFilter === cat ? '#fff' : '#475569'
                        }}
                      >
                        {cat === 'all' ? 'All Clauses (21)' : cat}
                      </button>
                    ))}
                  </div>

                  {/* Clauses List */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                    {displayedClauses.map(c => {
                      const isEditing = editingClause?.id === c.id
                      const current = isEditing ? editingClause : c

                      return (
                        <div key={c.id} style={{
                          border: '1px solid #e2e8f0',
                          borderRadius: '8px',
                          padding: '14px 16px',
                          background: current.status === 'Qualified' ? '#fffbeb' : current.status === 'Adverse' ? '#fef2f2' : '#fff'
                        }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '8px' }}>
                            <div>
                              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                <span style={{ fontWeight: 700, fontSize: '13px', color: '#0369a1' }}>Clause 3({c.clauseNumber})</span>
                                <h4 style={{ margin: 0, fontSize: '14px', fontWeight: 600, color: '#0f172a' }}>{c.clauseTitle}</h4>
                                <span style={{ fontSize: '10px', padding: '1px 6px', borderRadius: '4px', background: '#f1f5f9', color: '#64748b' }}>{c.category}</span>
                              </div>
                            </div>

                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                              {isEditing ? (
                                <select
                                  value={current.status}
                                  onChange={e => setEditingClause({ ...current, status: e.target.value })}
                                  style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px', fontWeight: 700 }}
                                >
                                  <option value="Compliant">✓ Compliant</option>
                                  <option value="Qualified">⚠️ Qualified</option>
                                  <option value="Adverse">⛔ Adverse</option>
                                  <option value="NotApplicable">N/A</option>
                                </select>
                              ) : (
                                <span style={{
                                  padding: '3px 8px',
                                  borderRadius: '4px',
                                  fontWeight: 700,
                                  fontSize: '11px',
                                  background: c.status === 'Compliant' ? '#dcfce7' : c.status === 'Qualified' ? '#fef3c7' : c.status === 'Adverse' ? '#fee2e2' : '#f1f5f9',
                                  color: c.status === 'Compliant' ? '#15803d' : c.status === 'Qualified' ? '#b45309' : c.status === 'Adverse' ? '#dc2626' : '#64748b'
                                }}>
                                  {c.status}
                                </span>
                              )}

                              {can('caro.manage') && selectedAssessment.status !== 'finalized' && (
                                isEditing ? (
                                  <div style={{ display: 'flex', gap: '4px' }}>
                                    <button onClick={() => handleSaveClause(current)} style={{ padding: '4px 10px', background: '#10b981', color: '#fff', border: 'none', borderRadius: '4px', fontSize: '11px', cursor: 'pointer', fontWeight: 600 }}>Save</button>
                                    <button onClick={() => setEditingClause(null)} style={{ padding: '4px 8px', background: '#e2e8f0', color: '#475569', border: 'none', borderRadius: '4px', fontSize: '11px', cursor: 'pointer' }}>Cancel</button>
                                  </div>
                                ) : (
                                  <button onClick={() => setEditingClause(c)} style={{ padding: '4px 10px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', fontSize: '11px', cursor: 'pointer', fontWeight: 600 }}>Edit</button>
                                )
                              )}
                            </div>
                          </div>

                          {/* Remark & details */}
                          {isEditing ? (
                            <div style={{ marginTop: '8px' }}>
                              <label style={{ display: 'block', fontSize: '11px', color: '#475569', fontWeight: 600, marginBottom: '2px' }}>Auditor Observation / Reporting Remark:</label>
                              <textarea
                                value={current.auditorRemark}
                                onChange={e => setEditingClause({ ...current, auditorRemark: e.target.value })}
                                rows={2}
                                style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px' }}
                              />
                              <div style={{ display: 'flex', gap: '16px', marginTop: '8px', alignItems: 'center' }}>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: '#334155', cursor: 'pointer' }}>
                                  <input
                                    type="checkbox"
                                    checked={current.hasDiscrepancy}
                                    onChange={e => setEditingClause({ ...current, hasDiscrepancy: e.target.checked })}
                                  />
                                  Quantified Discrepancy Amount
                                </label>
                                {current.hasDiscrepancy && (
                                  <input
                                    type="number"
                                    placeholder="Discrepancy Amount (₹)"
                                    value={current.discrepancyAmount || 0}
                                    onChange={e => setEditingClause({ ...current, discrepancyAmount: Number(e.target.value) })}
                                    style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px', width: '180px' }}
                                  />
                                )}
                                <input
                                  type="text"
                                  placeholder="Supporting WP Ref (e.g. WP-PPE-02)"
                                  value={current.supportingWpsRef || ''}
                                  onChange={e => setEditingClause({ ...current, supportingWpsRef: e.target.value })}
                                  style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px', width: '180px' }}
                                />
                              </div>
                            </div>
                          ) : (
                            <div>
                              <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: '#334155' }}>
                                {c.auditorRemark || <span style={{ color: '#94a3b8' }}>No remark specified.</span>}
                              </p>
                              {c.hasDiscrepancy && c.discrepancyAmount && (
                                <div style={{ fontSize: '11px', color: '#b45309', fontWeight: 600, marginTop: '4px' }}>
                                  ⚠️ Discrepancy Amount: {fmtInr(c.discrepancyAmount)} {c.supportingWpsRef ? `(Ref: ${c.supportingWpsRef})` : ''}
                                </div>
                              )}
                            </div>
                          )}
                        </div>
                      )
                    })}
                  </div>
                </div>
              )}

              {/* TAB 3: Auditor's Report Annexure 'A' */}
              {activeTab === 'annexure' && (
                <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '24px' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '2px solid #0f172a', paddingBottom: '12px' }}>
                    <div>
                      <h3 style={{ fontSize: '16px', fontWeight: 700, margin: 0, color: '#0f172a' }}>
                        ANNEXURE &lsquo;A&rsquo; TO THE INDEPENDENT AUDITOR&apos;S REPORT (CARO 2020)
                      </h3>
                      <p style={{ fontSize: '12px', color: '#64748b', margin: '2px 0 0 0' }}>
                        Formulated under Section 143(11) of the Companies Act, 2013 and ICAI Guidance Note
                      </p>
                    </div>
                    <div style={{ display: 'flex', gap: '8px' }}>
                      <button
                        onClick={() => window.print()}
                        style={{ padding: '6px 14px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '6px', cursor: 'pointer', fontSize: '12px', fontWeight: 600 }}
                      >
                        🖨️ Print Annexure
                      </button>
                    </div>
                  </div>

                  <pre style={{
                    whiteSpace: 'pre-wrap',
                    fontFamily: 'Georgia, serif',
                    fontSize: '13px',
                    lineHeight: '1.6',
                    color: '#1e293b',
                    background: '#fcfcfc',
                    padding: '20px',
                    borderRadius: '6px',
                    border: '1px solid #e2e8f0'
                  }}>
                    {selectedAssessment.generatedAnnexureText || 'Annexure draft not yet generated.'}
                  </pre>

                  {selectedAssessment.auditorConclusion && (
                    <div style={{ marginTop: '16px', padding: '12px 16px', background: '#f8fafc', borderRadius: '6px', borderLeft: '4px solid #0284c7' }}>
                      <strong style={{ fontSize: '12px', color: '#0f172a' }}>Auditor Conclusion &amp; Final Remarks:</strong>
                      <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: '#475569' }}>{selectedAssessment.auditorConclusion}</p>
                    </div>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* New CARO Assessment Modal */}
      {showNewModal && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 120, padding: '20px' }}>
          <div style={{ background: '#fff', borderRadius: '12px', width: '100%', maxWidth: '600px', maxHeight: '90vh', overflowY: 'auto', padding: '24px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0 }}>Create CARO 2020 Assessment</h2>
              <button onClick={() => setShowNewModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '20px', cursor: 'pointer', color: '#94a3b8' }}>✕</button>
            </div>

            <form onSubmit={handleCreateAssessment}>
              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>Company Client *</label>
                <select
                  value={newForm.clientId}
                  onChange={e => setNewForm({ ...newForm, clientId: e.target.value })}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  required
                >
                  <option value="">-- Choose Company --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>Financial Year *</label>
                  <select value={newForm.financialYear} onChange={e => setNewForm({ ...newForm, financialYear: e.target.value })} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}>
                    <option value="2024-25">2024-25</option>
                    <option value="2023-24">2023-24</option>
                    <option value="2025-26">2025-26</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#475569', marginBottom: '4px' }}>Company Type *</label>
                  <select value={newForm.companyType} onChange={e => setNewForm({ ...newForm, companyType: e.target.value })} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #cbd5e1' }}>
                    <option value="Private">Private Limited Company</option>
                    <option value="Public">Public Limited Company</option>
                    <option value="Section8">Section 8 Company</option>
                    <option value="SmallCompany">Small Company</option>
                    <option value="OPC">One Person Company</option>
                    <option value="Banking">Banking Company</option>
                    <option value="Insurance">Insurance Company</option>
                  </select>
                </div>
              </div>

              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '12px', fontWeight: 600, color: '#334155', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={newForm.isHoldingOrSubsidiaryOfPublic}
                    onChange={e => setNewForm({ ...newForm, isHoldingOrSubsidiaryOfPublic: e.target.checked })}
                  />
                  Holding or Subsidiary of a Public Company (Deemed Public)
                </label>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: '#475569', marginBottom: '2px' }}>Capital &amp; Reserves (₹)</label>
                  <input
                    type="number"
                    value={newForm.paidUpCapitalAndReserves}
                    onChange={e => setNewForm({ ...newForm, paidUpCapitalAndReserves: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: '#475569', marginBottom: '2px' }}>Borrowings (₹)</label>
                  <input
                    type="number"
                    value={newForm.totalBorrowings}
                    onChange={e => setNewForm({ ...newForm, totalBorrowings: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: '#475569', marginBottom: '2px' }}>Total Revenue (₹)</label>
                  <input
                    type="number"
                    value={newForm.totalRevenue}
                    onChange={e => setNewForm({ ...newForm, totalRevenue: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                <button type="button" onClick={() => setShowNewModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '6px', cursor: 'pointer' }}>Cancel</button>
                <button type="submit" style={{ padding: '8px 18px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}>Create Assessment</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
