import { useCallback, useEffect, useState } from 'react'
import {
  type Form3CdAssessmentDto,
  type Form3CdSummaryDto,
  type Form3CdClauseResponseDto,
  type Form3CdApplicabilityEvaluation,
  type IncomeTaxJsonExportResult,
  type ExportForm3CdJsonRequest,
  type Client,
  taxAuditApi,
  api
} from './api'
import { JsonExportPanel } from './components/JsonExportPanel'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0)

type ClientPage = {
  items: Client[]
  total: number
}

export function TaxAuditDeskPage({ can }: { can: (p: string) => boolean }) {
  const [assessments, setAssessments] = useState<Form3CdAssessmentDto[]>([])
  const [summary, setSummary] = useState<Form3CdSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Filters
  const [filterFy, setFilterFy] = useState('2025-26')
  const [filterStatus, setFilterStatus] = useState('all')

  // Selected assessment workbench
  const [selectedAssessment, setSelectedAssessment] = useState<Form3CdAssessmentDto | null>(null)
  const [activeTab, setActiveTab] = useState<'applicability' | 'clauses' | 'report'>('clauses')
  const [clauseCategoryFilter, setClauseCategoryFilter] = useState('all')
  const [editingClause, setEditingClause] = useState<Form3CdClauseResponseDto | null>(null)
  const [form3CdExport, setForm3CdExport] = useState<IncomeTaxJsonExportResult | null>(null)
  const [exporting3Cd, setExporting3Cd] = useState(false)
  const [form3CdOpts, setForm3CdOpts] = useState<ExportForm3CdJsonRequest>({
    auditorName: 'Statutory Tax Auditor',
    auditorMembershipNo: '234567',
    caFirmName: 'CA Firm & Associates',
    firmRegNum: '004321S',
    auditorCity: 'Kochi',
    auditorPinCode: '682031',
  })

  // Modals
  const [showNewModal, setShowNewModal] = useState(false)
  const [showEvalModal, setShowEvalModal] = useState(false)
  const [showFinalizeModal, setShowFinalizeModal] = useState(false)
  const [finalizeNote, setFinalizeNote] = useState('')

  // Quick Applicability Evaluator state
  const [evalForm, setEvalForm] = useState({
    entityType: 'Company',
    isBusiness: true,
    turnoverOrGrossReceipts: 35000000,
    cashReceipts: 4000000,
    totalPayments: 25000000,
    cashPayments: 1000000,
    isAuditedUnderOtherLaw: true,
    declaresLowerPresumptiveProfit: false,
    is44AdLockout: false,
    optsPresumptive44Ada: false,
    financialYear: '2025-26'
  })
  const [evalResult, setEvalResult] = useState<Form3CdApplicabilityEvaluation | null>(null)

  // New assessment form
  const [newForm, setNewForm] = useState({
    clientId: '',
    financialYear: '2025-26',
    assessmentYear: '2026-27',
    auditType: 'Form3CA',
    status: 'Company',
    isBusiness: true,
    totalTurnoverOrGrossReceipts: 35000000,
    totalCashReceipts: 1500000,
    totalPayments: 28000000,
    totalCashPayments: 1000000,
    assesseeName: '',
    pan: '',
    address: '',
    gstin: '',
    methodOfAccounting: 'Mercantile',
    optedSpecialTaxRegime: '115BAA',
    declaresLowerPresumptiveProfit: false,
    is44AdLockout: false,
    optsPresumptive44Ada: false,
    isAuditedUnderOtherLaw: true
  })

  // Clause editing form
  const [clauseForm, setClauseForm] = useState({
    status: 'Clean',
    reportedAmount: 0,
    auditorRemarks: '',
    hasDiscrepancy: false,
    workpaperRef: '',
    particularsJson: ''
  })

  // Helper calculators for editing modal
  const [calcResidentTdsBase, setCalcResidentTdsBase] = useState(0)
  const [calcMsmeDays, setCalcMsmeDays] = useState(50)
  const [calcMsmeHasAgreement, setCalcMsmeHasAgreement] = useState(true)

  const loadData = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [list, sum] = await Promise.all([
        taxAuditApi.getAssessments(undefined, filterFy, filterStatus === 'all' ? undefined : filterStatus),
        taxAuditApi.getSummary()
      ])
      setAssessments(list)
      setSummary(sum)
      setSelectedAssessment(prev => (prev ? (list.find(a => a.id === prev.id) ?? prev) : null))
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load Form 3CD assessments.')
    } finally {
      setLoading(false)
    }
  }, [filterFy, filterStatus])

  const loadClients = useCallback(async () => {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      setClients(res.items || [])
    } catch {
      // ignore
    }
  }, [])

  useEffect(() => {
    loadData()
    loadClients()
  }, [loadData, loadClients])

  async function handleSelectAssessment(id: string) {
    setLoading(true)
    setError(null)
    try {
      const data = await taxAuditApi.getAssessmentById(id)
      setSelectedAssessment(data)
      setActiveTab('clauses')
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load assessment details.')
    } finally {
      setLoading(false)
    }
  }

  async function handleCreateAssessment(e: React.FormEvent) {
    e.preventDefault()
    if (!newForm.clientId) {
      setError('Please select a client.')
      return
    }

    setLoading(true)
    setError(null)
    try {
      const created = await taxAuditApi.createAssessment({
        ...newForm,
        section44AbClause: null
      })
      setShowNewModal(false)
      await loadData()
      setSelectedAssessment(created)
      setActiveTab('clauses')
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create Form 3CD assessment.')
    } finally {
      setLoading(false)
    }
  }

  async function handleEvaluateApplicability() {
    setLoading(true)
    setError(null)
    try {
      const res = await taxAuditApi.evaluateApplicability(evalForm)
      setEvalResult(res)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Applicability check failed.')
    } finally {
      setLoading(false)
    }
  }

  function openEditClause(c: Form3CdClauseResponseDto) {
    setEditingClause(c)
    setClauseForm({
      status: c.status,
      reportedAmount: c.reportedAmount,
      auditorRemarks: c.auditorRemarks,
      hasDiscrepancy: c.hasDiscrepancy,
      workpaperRef: c.workpaperRef || '',
      particularsJson: c.particularsJson || ''
    })
    setCalcResidentTdsBase(0)
  }

  async function handleSaveClause(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedAssessment || !editingClause) return

    setLoading(true)
    setError(null)
    try {
      const updated = await taxAuditApi.updateClause(selectedAssessment.id, editingClause.id, {
        status: clauseForm.status,
        reportedAmount: Number(clauseForm.reportedAmount),
        auditorRemarks: clauseForm.auditorRemarks,
        hasDiscrepancy: clauseForm.hasDiscrepancy,
        workpaperRef: clauseForm.workpaperRef,
        particularsJson: clauseForm.particularsJson
      })
      setSelectedAssessment(updated)
      setEditingClause(null)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update clause response.')
    } finally {
      setLoading(false)
    }
  }

  async function handleRegenerateReport() {
    if (!selectedAssessment) return
    setLoading(true)
    setError(null)
    try {
      const res = await taxAuditApi.generateReport(selectedAssessment.id)
      setSelectedAssessment({
        ...selectedAssessment,
        generatedReportText: res.reportText
      })
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to regenerate tax audit report.')
    } finally {
      setLoading(false)
    }
  }

  async function handleFinalizeAssessment() {
    if (!selectedAssessment) return
    setLoading(true)
    setError(null)
    try {
      const finalized = await taxAuditApi.finalizeAssessment(selectedAssessment.id, finalizeNote)
      setSelectedAssessment(finalized)
      setShowFinalizeModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to finalize assessment.')
    } finally {
      setLoading(false)
    }
  }

  function onClientChangeInNewForm(clientId: string) {
    const cl = clients.find(c => c.id === clientId)
    if (cl) {
      const pan = cl.taxIds?.['PAN'] || ''
      setNewForm(prev => ({
        ...prev,
        clientId,
        assesseeName: cl.name,
        pan: pan,
        status: cl.entityType === 'Individual' ? 'Individual' : 'Company',
        isAuditedUnderOtherLaw: cl.entityType !== 'Individual',
        auditType: cl.entityType === 'Individual' ? 'Form3CB' : 'Form3CA'
      }))
    } else {
      setNewForm(prev => ({ ...prev, clientId }))
    }
  }

  const filteredClauses = selectedAssessment?.clauseResponses.filter(c => {
    if (clauseCategoryFilter === 'all') return true
    if (clauseCategoryFilter === 'disallowed') return c.status === 'Disallowed' || c.status === 'Qualified'
    return c.category === clauseCategoryFilter
  }) || []

  return (
    <div style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto', color: 'var(--text)' }}>
      {/* Top Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '26px', fontWeight: '700', color: 'var(--text)' }}>
            Form 3CD Tax Audit Statement &amp; Disclosures Desk
          </h1>
          <p style={{ margin: '4px 0 0', color: 'var(--muted)', fontSize: '14px' }}>
            Section 44AB statutory limit engine, 5% cash ratio rules, Clause-by-Clause disclosures (40(a), 43B(h) MSME, 269SS/ST/T), &amp; Form 3CA/3CB reports.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '12px' }}>
          <button
            onClick={() => {
              setShowEvalModal(true)
              setEvalResult(null)
            }}
            style={{
              padding: '10px 16px',
              backgroundColor: 'var(--card-subtle)',
              color: 'var(--text)',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              cursor: 'pointer',
              fontWeight: '600',
              fontSize: '14px'
            }}
          >
            44AB Applicability Calculator
          </button>
          {can('taxaudit.manage') && (
            <button
              onClick={() => setShowNewModal(true)}
              style={{
                padding: '10px 16px',
                backgroundColor: 'var(--accent)',
                color: '#fff',
                border: 'none',
                borderRadius: '6px',
                cursor: 'pointer',
                fontWeight: '600',
                fontSize: '14px',
                boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
              }}
            >
              + New Tax Audit (3CD)
            </button>
          )}
        </div>
      </div>

      {error && (
        <div style={{ padding: '14px', backgroundColor: 'rgba(220, 38, 38, 0.08)', border: '1px solid rgba(220, 38, 38, 0.35)', color: 'var(--error)', borderRadius: '8px', marginBottom: '20px', fontSize: '14px' }}>
          {error}
        </div>
      )}

      {/* KPI Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
        <div style={{ backgroundColor: 'var(--card)', padding: '18px', borderRadius: '8px', border: '1px solid var(--border)', borderTop: '3px solid var(--accent)' }}>
          <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--muted)', textTransform: 'uppercase' }}>Assessments Total</div>
          <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--text)', marginTop: '6px' }}>{summary?.totalAssessments ?? 0}</div>
          <div style={{ fontSize: '12px', color: 'var(--success)', marginTop: '4px' }}>
            {summary?.mandatoryCount ?? 0} Mandatory | {summary?.exemptCount ?? 0} Exempt
          </div>
        </div>

        <div style={{ backgroundColor: 'var(--card)', padding: '18px', borderRadius: '8px', border: '1px solid var(--border)', borderTop: '3px solid var(--error)' }}>
          <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--muted)', textTransform: 'uppercase' }}>Net Profit Additions</div>
          <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--error)', marginTop: '6px' }}>
            {fmtInr(summary?.totalNetDisallowances ?? 0)}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px' }}>Sec 40(a), 40A(3), 43B, 36(1)(va)</div>
        </div>

        <div style={{ backgroundColor: 'var(--card)', padding: '18px', borderRadius: '8px', border: '1px solid var(--border)', borderTop: '3px solid var(--warning)' }}>
          <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--muted)', textTransform: 'uppercase' }}>Sec 43B(h) MSME Overdue</div>
          <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--warning)', marginTop: '6px' }}>
            {fmtInr(summary?.total43BhMsmeDisallowances ?? 0)}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px' }}>Delayed &gt; 15/45 days limit</div>
        </div>

        <div style={{ backgroundColor: 'var(--card)', padding: '18px', borderRadius: '8px', border: '1px solid var(--border)', borderTop: '3px solid var(--warning)' }}>
          <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--muted)', textTransform: 'uppercase' }}>Sec 269SS/ST/T Violations</div>
          <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--warning)', marginTop: '6px' }}>
            {fmtInr(summary?.total269Violations ?? 0)}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px' }}>Cash loans &gt; ₹20k / ₹2L receipts</div>
        </div>

        <div style={{ backgroundColor: 'var(--card)', padding: '18px', borderRadius: '8px', border: '1px solid var(--border)', borderTop: '3px solid var(--success)' }}>
          <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--muted)', textTransform: 'uppercase' }}>Finalized Reports</div>
          <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--accent)', marginTop: '6px' }}>
            {summary?.finalizedCount ?? 0}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px' }}>Signed off by Partners</div>
        </div>
      </div>

      {/* Main Grid: Left side Assessment Selector, Right side Workbench */}
      <div style={{ display: 'grid', gridTemplateColumns: selectedAssessment ? '360px 1fr' : '1fr', gap: '20px' }}>
        {/* Left Column: Assessment List */}
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '16px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
            <h2 style={{ margin: 0, fontSize: '16px', fontWeight: '700', color: 'var(--text)' }}>Tax Audit Files</h2>
            <div style={{ display: 'flex', gap: '8px' }}>
              <select
                value={filterFy}
                onChange={e => setFilterFy(e.target.value)}
                style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
              >
                <option value="2026-27">TY 2026-27</option>
                <option value="2025-26">FY 2025-26</option>
                <option value="2024-25">FY 2024-25</option>
                <option value="2023-24">FY 2023-24</option>
                <option value="2022-23">FY 2022-23</option>
              </select>
              <select
                value={filterStatus}
                onChange={e => setFilterStatus(e.target.value)}
                style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
              >
                <option value="all">All</option>
                <option value="draft">Draft</option>
                <option value="in_review">In Review</option>
                <option value="finalized">Finalized</option>
              </select>
            </div>
          </div>

          {loading && assessments.length === 0 ? (
            <div style={{ padding: '24px', textAlign: 'center', color: 'var(--muted)' }}>Loading assessments...</div>
          ) : assessments.length === 0 ? (
            <div style={{ padding: '24px', textAlign: 'center', color: 'var(--muted)', fontSize: '14px' }}>
              No tax audit assessments found for this filter. Create one to begin.
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', maxHeight: '720px', overflowY: 'auto' }}>
              {assessments.map(a => {
                const isSelected = selectedAssessment?.id === a.id
                return (
                  <div
                    key={a.id}
                    onClick={() => handleSelectAssessment(a.id)}
                    style={{
                      padding: '12px',
                      borderRadius: '6px',
                      border: isSelected ? '2px solid var(--accent)' : '1px solid var(--border)',
                      backgroundColor: isSelected ? 'rgba(37, 99, 235, 0.08)' : 'var(--card-subtle)',
                      cursor: 'pointer',
                      transition: 'all 0.15s ease'
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                      <div style={{ fontWeight: '700', fontSize: '14px', color: 'var(--text)' }}>{a.assesseeName || a.clientName || 'Assessee'}</div>
                      <span
                        style={{
                          fontSize: '11px',
                          padding: '2px 6px',
                          borderRadius: '4px',
                          fontWeight: '600',
                          backgroundColor:
                            a.assessmentStatus === 'finalized'
                              ? 'rgba(22, 163, 74, 0.12)'
                              : a.assessmentStatus === 'in_review'
                                ? 'rgba(217, 119, 6, 0.12)'
                                : 'var(--card)',
                          color:
                            a.assessmentStatus === 'finalized'
                              ? 'var(--success)'
                              : a.assessmentStatus === 'in_review'
                                ? 'var(--warning)'
                                : 'var(--muted)'
                        }}
                      >
                        {a.assessmentStatus.toUpperCase()}
                      </span>
                    </div>

                    <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>
                      PAN: <strong>{a.pan || 'N/A'}</strong> | {a.auditType}
                    </div>

                    <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '2px' }}>
                      Turnover: {fmtInr(a.totalTurnoverOrGrossReceipts)} (Cash: {a.cashReceiptPercentage}%)
                    </div>

                    <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '8px', paddingTop: '6px', borderTop: '1px solid var(--border)', fontSize: '12px' }}>
                      <span style={{ color: a.isTaxAuditMandatory ? 'var(--error)' : 'var(--success)', fontWeight: '600' }}>
                        {a.isTaxAuditMandatory ? '● 44AB Mandatory' : '○ 44AB Exempt'}
                      </span>
                      <span style={{ fontWeight: '700', color: a.totalNetIncomeAdjustment > 0 ? 'var(--error)' : 'var(--text)' }}>
                        +{fmtInr(a.totalNetIncomeAdjustment)}
                      </span>
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>

        {/* Right Column: Active Workbench */}
        {selectedAssessment && (
          <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '20px' }}>
            {/* Header Banner */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '16px', paddingBottom: '16px', borderBottom: '1px solid var(--border)' }}>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <h2 style={{ margin: 0, fontSize: '20px', fontWeight: '700', color: 'var(--text)' }}>
                    {selectedAssessment.assesseeName}
                  </h2>
                  <span
                    style={{
                      fontSize: '12px',
                      padding: '2px 8px',
                      borderRadius: '4px',
                      fontWeight: '700',
                      backgroundColor: selectedAssessment.auditType === 'Form3CA' ? 'rgba(37, 99, 235, 0.12)' : 'rgba(217, 119, 6, 0.12)',
                      color: selectedAssessment.auditType === 'Form3CA' ? 'var(--accent)' : 'var(--warning)'
                    }}
                  >
                    {selectedAssessment.auditType}
                  </span>
                  <span
                    style={{
                      fontSize: '12px',
                      padding: '2px 8px',
                      borderRadius: '4px',
                      fontWeight: '600',
                      backgroundColor: selectedAssessment.isTaxAuditMandatory ? 'rgba(220, 38, 38, 0.12)' : 'rgba(22, 163, 74, 0.12)',
                      color: selectedAssessment.isTaxAuditMandatory ? 'var(--error)' : 'var(--success)'
                    }}
                  >
                    {selectedAssessment.isTaxAuditMandatory ? 'Mandatory u/s 44AB' : 'Exempt u/s 44AB'}
                  </span>
                </div>
                <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '6px' }}>
                  PAN: <strong>{selectedAssessment.pan}</strong> | GSTIN: {selectedAssessment.gstin || 'N/A'} | Status: {selectedAssessment.status} | Regime: {selectedAssessment.optedSpecialTaxRegime}
                </div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '2px' }}>
                  Financial Year: <strong>{selectedAssessment.financialYear}</strong> (AY: {selectedAssessment.assessmentYear}) | Method: {selectedAssessment.methodOfAccounting}
                </div>
              </div>

              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  onClick={handleRegenerateReport}
                  style={{
                    padding: '8px 12px',
                    backgroundColor: 'var(--card-subtle)',
                    border: '1px solid var(--border)',
                    color: 'var(--text)',
                    borderRadius: '6px',
                    fontSize: '13px',
                    cursor: 'pointer',
                    fontWeight: '600'
                  }}
                >
                  Regenerate Report
                </button>
                {selectedAssessment.assessmentStatus !== 'finalized' && can('taxaudit.finalize') && (
                  <button
                    onClick={() => {
                      setFinalizeNote(selectedAssessment.auditorObservations || '')
                      setShowFinalizeModal(true)
                    }}
                    style={{
                      padding: '8px 14px',
                      backgroundColor: 'var(--success)',
                      color: '#fff',
                      border: 'none',
                      borderRadius: '6px',
                      fontSize: '13px',
                      cursor: 'pointer',
                      fontWeight: '600'
                    }}
                  >
                    Finalize &amp; Sign Off
                  </button>
                )}
              </div>
            </div>

            {/* Quick Metrics Bar */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '12px', padding: '14px 0', borderBottom: '1px solid var(--border)', fontSize: '13px' }}>
              <div>
                <span style={{ color: 'var(--muted)' }}>Turnover: </span>
                <strong>{fmtInr(selectedAssessment.totalTurnoverOrGrossReceipts)}</strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Cash Receipts: </span>
                <strong>{selectedAssessment.cashReceiptPercentage}%</strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Cash Payments: </span>
                <strong>{selectedAssessment.cashPaymentPercentage}%</strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Sec 40(a) TDS: </span>
                <strong style={{ color: selectedAssessment.totalDisallowanceSec40a > 0 ? 'var(--error)' : 'var(--text)' }}>
                  {fmtInr(selectedAssessment.totalDisallowanceSec40a)}
                </strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Sec 43B(h) MSME: </span>
                <strong style={{ color: selectedAssessment.totalDisallowanceSec43Bh > 0 ? 'var(--warning)' : 'var(--text)' }}>
                  {fmtInr(selectedAssessment.totalDisallowanceSec43Bh)}
                </strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Total Additions: </span>
                <strong style={{ color: 'var(--error)', fontSize: '14px' }}>{fmtInr(selectedAssessment.totalNetIncomeAdjustment)}</strong>
              </div>
            </div>

            {/* Navigation Tabs */}
            <div style={{ display: 'flex', gap: '8px', borderBottom: '1px solid var(--border)', margin: '16px 0 20px' }}>
              <button
                onClick={() => setActiveTab('clauses')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: activeTab === 'clauses' ? '2px solid var(--accent)' : 'none',
                  backgroundColor: 'transparent',
                  color: activeTab === 'clauses' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: activeTab === 'clauses' ? '700' : '500',
                  cursor: 'pointer',
                  fontSize: '14px'
                }}
              >
                Form 3CD Clauses ({selectedAssessment.clauseResponses.length})
              </button>
              <button
                onClick={() => setActiveTab('applicability')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: activeTab === 'applicability' ? '2px solid var(--accent)' : 'none',
                  backgroundColor: 'transparent',
                  color: activeTab === 'applicability' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: activeTab === 'applicability' ? '700' : '500',
                  cursor: 'pointer',
                  fontSize: '14px'
                }}
              >
                44AB Applicability Analysis
              </button>
              <button
                onClick={() => setActiveTab('report')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: activeTab === 'report' ? '2px solid var(--accent)' : 'none',
                  backgroundColor: 'transparent',
                  color: activeTab === 'report' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: activeTab === 'report' ? '700' : '500',
                  cursor: 'pointer',
                  fontSize: '14px'
                }}
              >
                Tax Audit Report Draft ({selectedAssessment.auditType})
              </button>
            </div>

            {/* Tab 1: Clauses Workbench */}
            {activeTab === 'clauses' && (
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                  <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                    <label style={{ fontSize: '13px', color: 'var(--text-secondary)', fontWeight: '600' }}>Filter Category:</label>
                    <select
                      value={clauseCategoryFilter}
                      onChange={e => setClauseCategoryFilter(e.target.value)}
                      style={{ padding: '6px 10px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '13px' }}
                    >
                      <option value="all">All Categories</option>
                      <option value="disallowed">Only Disallowances / Qualifications</option>
                      <option value="Part A - General Assessee Details">Part A - General Details</option>
                      <option value="Accounting Methods & ICDS">Accounting &amp; ICDS</option>
                      <option value="Depreciation & Capital Deductions">Depreciation &amp; Capital</option>
                      <option value="Disallowances & Additions (Sec 40, 40A, 36)">Disallowances (40, 40A, 36)</option>
                      <option value="Statutory Dues & Sec 43B / 43B(h)">Statutory Dues &amp; 43B / MSME</option>
                      <option value="Loans, Deposits & Cash (Sec 269SS/ST/T)">Loans &amp; Cash (269SS/ST/T)</option>
                      <option value="TDS / TCS Compliance (Chapter XVII-B)">TDS/TCS Compliance</option>
                      <option value="Analytical Ratios & GST Expenditure">Ratios &amp; GST Expenditure</option>
                    </select>
                  </div>
                  <div style={{ fontSize: '13px', color: 'var(--muted)' }}>
                    Showing {filteredClauses.length} of {selectedAssessment.clauseResponses.length} clauses
                  </div>
                </div>

                <div style={{ overflowX: 'auto', border: '1px solid var(--border)', borderRadius: '6px' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
                    <thead>
                      <tr style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', color: 'var(--muted)', fontWeight: '600' }}>
                        <th style={{ padding: '10px 12px' }}>Clause No.</th>
                        <th style={{ padding: '10px 12px' }}>Title &amp; Scope</th>
                        <th style={{ padding: '10px 12px' }}>Status</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>Disallowance / Amount</th>
                        <th style={{ padding: '10px 12px' }}>Auditor Remarks &amp; Finding</th>
                        <th style={{ padding: '10px 12px' }}>Ref</th>
                        <th style={{ padding: '10px 12px', textAlign: 'center' }}>Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredClauses.map(c => {
                        const isDisallowed = c.status === 'Disallowed' || c.status === 'Qualified'
                        return (
                          <tr key={c.id} style={{ borderBottom: '1px solid var(--border)', backgroundColor: isDisallowed ? 'rgba(220, 38, 38, 0.05)' : 'var(--card)' }}>
                            <td style={{ padding: '10px 12px', fontWeight: '700', whiteSpace: 'nowrap' }}>
                              {c.clauseNumber}
                            </td>
                            <td style={{ padding: '10px 12px' }}>
                              <div style={{ fontWeight: '600', color: 'var(--text)' }}>{c.clauseTitle}</div>
                              <div style={{ fontSize: '11px', color: 'var(--muted)' }}>{c.category}</div>
                            </td>
                            <td style={{ padding: '10px 12px' }}>
                              <span
                                style={{
                                  fontSize: '11px',
                                  padding: '2px 8px',
                                  borderRadius: '4px',
                                  fontWeight: '700',
                                  backgroundColor:
                                    c.status === 'Clean'
                                      ? 'rgba(22, 163, 74, 0.12)'
                                      : c.status === 'Disallowed'
                                        ? 'rgba(220, 38, 38, 0.12)'
                                        : c.status === 'Qualified'
                                          ? 'rgba(217, 119, 6, 0.12)'
                                          : 'var(--card-subtle)',
                                  color:
                                    c.status === 'Clean'
                                      ? 'var(--success)'
                                      : c.status === 'Disallowed'
                                        ? 'var(--error)'
                                        : c.status === 'Qualified'
                                          ? 'var(--warning)'
                                          : 'var(--muted)'
                                }}
                              >
                                {c.status}
                              </span>
                            </td>
                            <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: '700', color: c.reportedAmount > 0 ? 'var(--error)' : 'var(--muted)' }}>
                              {c.reportedAmount > 0 ? fmtInr(c.reportedAmount) : '-'}
                            </td>
                            <td style={{ padding: '10px 12px', maxWidth: '300px', color: 'var(--text-secondary)' }}>
                              <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={c.auditorRemarks}>
                                {c.auditorRemarks || 'Verified without discrepancy.'}
                              </div>
                            </td>
                            <td style={{ padding: '10px 12px', color: 'var(--muted)', fontSize: '11px' }}>
                              {c.workpaperRef || '-'}
                            </td>
                            <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                              {selectedAssessment.assessmentStatus !== 'finalized' && can('taxaudit.manage') ? (
                                <button
                                  onClick={() => openEditClause(c)}
                                  style={{
                                    padding: '4px 10px',
                                    backgroundColor: 'var(--card-subtle)',
                                    border: '1px solid var(--border)',
                                    color: 'var(--text)',
                                    borderRadius: '4px',
                                    cursor: 'pointer',
                                    fontSize: '12px',
                                    fontWeight: '600'
                                  }}
                                >
                                  Edit / Verify
                                </button>
                              ) : (
                                <span style={{ color: 'var(--muted)', fontSize: '12px' }}>Locked</span>
                              )}
                            </td>
                          </tr>
                        )
                      })}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Tab 2: 44AB Applicability Analysis */}
            {activeTab === 'applicability' && (
              <div style={{ backgroundColor: 'var(--card-subtle)', padding: '20px', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <h3 style={{ margin: '0 0 14px', fontSize: '16px', color: 'var(--text)' }}>
                  Section 44AB Statutory Audit Gating Rationale
                </h3>
                <div style={{ backgroundColor: selectedAssessment.isTaxAuditMandatory ? 'rgba(220, 38, 38, 0.08)' : 'rgba(22, 163, 74, 0.08)', padding: '14px', borderRadius: '6px', border: `1px solid ${selectedAssessment.isTaxAuditMandatory ? 'rgba(220, 38, 38, 0.35)' : 'rgba(22, 163, 74, 0.35)'}`, marginBottom: '16px' }}>
                  <div style={{ fontWeight: '700', fontSize: '14px', color: selectedAssessment.isTaxAuditMandatory ? 'var(--error)' : 'var(--success)' }}>
                    {selectedAssessment.isTaxAuditMandatory ? 'Mandatory Audit Required' : 'Audit Not Mandatory (Exempt)'}
                  </div>
                  <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginTop: '6px' }}>
                    {selectedAssessment.applicabilityReason}
                  </div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', fontSize: '13px' }}>
                  <div style={{ backgroundColor: 'var(--card)', padding: '14px', borderRadius: '6px', border: '1px solid var(--border)' }}>
                    <div style={{ fontWeight: '700', color: 'var(--text)', marginBottom: '8px' }}>5% Cash Receipts Test</div>
                    <div>Total Turnover/Receipts: <strong>{fmtInr(selectedAssessment.totalTurnoverOrGrossReceipts)}</strong></div>
                    <div>Total Cash Receipts: <strong>{fmtInr(selectedAssessment.totalCashReceipts)}</strong></div>
                    <div>Cash Receipt Ratio: <strong>{selectedAssessment.cashReceiptPercentage}%</strong></div>
                    <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '4px' }}>
                      {selectedAssessment.cashReceiptPercentage <= 5 ? 'Eligible for enhanced limit (<= 5%)' : 'Exceeds 5% safe harbor threshold'}
                    </div>
                  </div>

                  <div style={{ backgroundColor: 'var(--card)', padding: '14px', borderRadius: '6px', border: '1px solid var(--border)' }}>
                    <div style={{ fontWeight: '700', color: 'var(--text)', marginBottom: '8px' }}>5% Cash Payments Test</div>
                    <div>Total Payments Incurred: <strong>{fmtInr(selectedAssessment.totalPayments)}</strong></div>
                    <div>Total Cash Payments: <strong>{fmtInr(selectedAssessment.totalCashPayments)}</strong></div>
                    <div>Cash Payment Ratio: <strong>{selectedAssessment.cashPaymentPercentage}%</strong></div>
                    <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '4px' }}>
                      {selectedAssessment.cashPaymentPercentage <= 5 ? 'Eligible for enhanced limit (<= 5%)' : 'Exceeds 5% safe harbor threshold'}
                    </div>
                  </div>
                </div>

                <div style={{ marginTop: '16px', fontSize: '13px', color: 'var(--text-secondary)', backgroundColor: 'var(--card)', padding: '14px', borderRadius: '6px', border: '1px solid var(--border)' }}>
                  <div>Audit Report Selected: <strong>{selectedAssessment.auditType}</strong></div>
                  <div>Statutory Sub-Clause: <strong>{selectedAssessment.section44AbClause}</strong></div>
                </div>
              </div>
            )}

            {/* Tab 3: Report Draft */}
            {activeTab === 'report' && (
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
                  <h3 style={{ margin: 0, fontSize: '16px', color: 'var(--text)' }}>
                    Legal Audit Report ({selectedAssessment.auditType}) &amp; Observations
                  </h3>
                  <button
                    onClick={() => {
                      if (selectedAssessment.generatedReportText) {
                        navigator.clipboard.writeText(selectedAssessment.generatedReportText)
                        alert('Report text copied to clipboard!')
                      }
                    }}
                    style={{
                      padding: '6px 12px',
                      backgroundColor: 'var(--accent)',
                      color: '#fff',
                      border: 'none',
                      borderRadius: '4px',
                      fontSize: '12px',
                      cursor: 'pointer',
                      fontWeight: '600'
                    }}
                  >
                    Copy Report Text
                  </button>
                </div>

                <textarea
                  readOnly
                  value={selectedAssessment.generatedReportText || 'No report draft generated. Click Regenerate Report.'}
                  rows={20}
                  style={{
                    width: '100%',
                    boxSizing: 'border-box',
                    padding: '16px',
                    fontFamily: 'Consolas, Monaco, monospace',
                    fontSize: '12px',
                    lineHeight: '1.5',
                    backgroundColor: '#0b1528',
                    color: '#f8fafc',
                    borderRadius: '6px',
                    border: '1px solid var(--border)',
                    resize: 'vertical'
                  }}
                />

                <JsonExportPanel
                  title={`Official CBDT ${selectedAssessment.auditType} + 3CD JSON Export (V2.5)`}
                  subtitle="Builds schema-validated Form 3CA-3CD / 3CB-3CD V2.5 JSON with Part A, disallowances, and Clause 44."
                  clientId={selectedAssessment.clientId}
                  fy={selectedAssessment.financialYear}
                  vaultTypeCode="TAX_AUDIT"
                  generating={exporting3Cd}
                  result={form3CdExport}
                  onGenerate={async () => {
                    try {
                      setExporting3Cd(true)
                      setError(null)
                      const res = await taxAuditApi.exportJson(selectedAssessment.id, form3CdOpts)
                      setForm3CdExport(res)
                    } catch (err: unknown) {
                      setError(err instanceof Error ? err.message : 'Form 3CD JSON export failed.')
                    } finally {
                      setExporting3Cd(false)
                    }
                  }}
                >
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '8px' }}>
                    <input
                      type="text"
                      placeholder="CA Name"
                      value={form3CdOpts.auditorName || ''}
                      onChange={e => setForm3CdOpts(p => ({ ...p, auditorName: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      maxLength={6}
                      placeholder="Membership No. (6 digits)"
                      value={form3CdOpts.auditorMembershipNo || ''}
                      onChange={e => setForm3CdOpts(p => ({ ...p, auditorMembershipNo: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      maxLength={8}
                      placeholder="FRN (e.g. 004321S)"
                      value={form3CdOpts.firmRegNum || ''}
                      onChange={e => setForm3CdOpts(p => ({ ...p, firmRegNum: e.target.value.toUpperCase() }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      placeholder="CA Firm Name"
                      value={form3CdOpts.caFirmName || ''}
                      onChange={e => setForm3CdOpts(p => ({ ...p, caFirmName: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      placeholder="Place / City"
                      value={form3CdOpts.auditorCity || ''}
                      onChange={e => setForm3CdOpts(p => ({ ...p, auditorCity: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      maxLength={6}
                      placeholder="Auditor PIN Code"
                      value={form3CdOpts.auditorPinCode || ''}
                      onChange={e => setForm3CdOpts(p => ({ ...p, auditorPinCode: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                  </div>
                </JsonExportPanel>
              </div>
            )}
          </div>
        )}
      </div>

      {/* Modal: New Tax Audit Assessment */}
      {showNewModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', padding: '24px', borderRadius: '8px', maxWidth: '640px', width: '90%', maxHeight: '90vh', overflowY: 'auto' }}>
            <h2 style={{ margin: '0 0 16px', fontSize: '18px', fontWeight: '700' }}>Create Form 3CD Tax Audit Assessment</h2>
            <form onSubmit={handleCreateAssessment} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Client</label>
                <select
                  required
                  value={newForm.clientId}
                  onChange={e => onClientChangeInNewForm(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                >
                  <option value="">-- Select Client --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>
                      {c.name} ({c.code || 'No Code'})
                    </option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Financial Year</label>
                  <select
                    value={newForm.financialYear}
                    onChange={e => setNewForm({ ...newForm, financialYear: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="2026-27">Tax Year 2026-27 (Income-tax Act, 2025 · Form 26)</option>
                    <option value="2025-26">2025-26</option>
                    <option value="2024-25">2024-25</option>
                    <option value="2023-24">2023-24</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Assessment Year</label>
                  <input
                    type="text"
                    value={newForm.assessmentYear}
                    onChange={e => setNewForm({ ...newForm, assessmentYear: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Assessee Name</label>
                  <input
                    type="text"
                    required
                    value={newForm.assesseeName}
                    onChange={e => setNewForm({ ...newForm, assesseeName: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>PAN</label>
                  <input
                    type="text"
                    required
                    value={newForm.pan}
                    onChange={e => setNewForm({ ...newForm, pan: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Status / Entity Type</label>
                  <select
                    value={newForm.status}
                    onChange={e => setNewForm({ ...newForm, status: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="Company">Company</option>
                    <option value="Firm">Partnership Firm</option>
                    <option value="LLP">LLP</option>
                    <option value="Individual">Individual</option>
                    <option value="HUF">HUF</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Special Tax Regime</label>
                  <select
                    value={newForm.optedSpecialTaxRegime}
                    onChange={e => setNewForm({ ...newForm, optedSpecialTaxRegime: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="None">Normal Provisions</option>
                    <option value="115BAA">115BAA (22% Domestic Co)</option>
                    <option value="115BAB">115BAB (15% New Mfg Co)</option>
                    <option value="115BAC">115BAC (Individual/HUF New)</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Total Turnover / Receipts (₹)</label>
                  <input
                    type="number"
                    value={newForm.totalTurnoverOrGrossReceipts}
                    onChange={e => setNewForm({ ...newForm, totalTurnoverOrGrossReceipts: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Cash Receipts (₹)</label>
                  <input
                    type="number"
                    value={newForm.totalCashReceipts}
                    onChange={e => setNewForm({ ...newForm, totalCashReceipts: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Total Payments Incurred (₹)</label>
                  <input
                    type="number"
                    value={newForm.totalPayments}
                    onChange={e => setNewForm({ ...newForm, totalPayments: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Cash Payments (₹)</label>
                  <input
                    type="number"
                    value={newForm.totalCashPayments}
                    onChange={e => setNewForm({ ...newForm, totalCashPayments: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', gap: '20px', flexWrap: 'wrap', padding: '10px', backgroundColor: 'var(--card-subtle)', borderRadius: '6px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={newForm.isAuditedUnderOtherLaw}
                    onChange={e => setNewForm({ ...newForm, isAuditedUnderOtherLaw: e.target.checked })}
                  />
                  Audited under other law (Form 3CA)
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={newForm.is44AdLockout}
                    onChange={e => setNewForm({ ...newForm, is44AdLockout: e.target.checked })}
                  />
                  Sec 44AD(4) 5-Yr Lockout
                </label>
                {!newForm.isBusiness && (
                  <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                    <input
                      type="checkbox"
                      checked={newForm.optsPresumptive44Ada}
                      onChange={e => setNewForm({ ...newForm, optsPresumptive44Ada: e.target.checked })}
                    />
                    Income offered u/s 44ADA at 50% or more
                  </label>
                )}
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '12px' }}>
                <button
                  type="button"
                  onClick={() => setShowNewModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card-subtle)', color: 'var(--text)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: 'none', backgroundColor: 'var(--accent)', color: '#fff', fontWeight: '600', cursor: 'pointer' }}
                >
                  Create Assessment
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Clause Response Editor / Disallowance Entry */}
      {editingClause && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', padding: '24px', borderRadius: '8px', maxWidth: '640px', width: '90%', maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
              <h2 style={{ margin: 0, fontSize: '18px', fontWeight: '700' }}>
                {editingClause.clauseNumber}: {editingClause.clauseTitle}
              </h2>
              <button
                onClick={() => setEditingClause(null)}
                style={{ border: 'none', background: 'transparent', color: 'var(--muted)', fontSize: '18px', cursor: 'pointer' }}
              >
                &times;
              </button>
            </div>

            <form onSubmit={handleSaveClause} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Clause Status</label>
                <select
                  value={clauseForm.status}
                  onChange={e => setClauseForm({ ...clauseForm, status: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                >
                  <option value="Clean">Clean / Favorable (No Disallowance)</option>
                  <option value="Disallowed">Disallowance / Addition to Income</option>
                  <option value="Qualified">Qualified Remark / Observation</option>
                  <option value="NotApplicable">Not Applicable</option>
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>
                  Disallowance / Reported Amount (₹)
                </label>
                <input
                  type="number"
                  value={clauseForm.reportedAmount}
                  onChange={e => setClauseForm({ ...clauseForm, reportedAmount: Number(e.target.value) })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                />
              </div>

              {/* Special helper for Clause 21(b) (Sec 40(a)(ia) 30% rule) */}
              {editingClause.clauseNumber.includes('21(b)') && (
                <div style={{ backgroundColor: 'rgba(22, 163, 74, 0.08)', border: '1px solid rgba(22, 163, 74, 0.35)', padding: '12px', borderRadius: '6px', fontSize: '12px' }}>
                  <div style={{ fontWeight: '700', color: 'var(--success)', marginBottom: '6px' }}>Section 40(a)(ia) 30% TDS Calculator Helper</div>
                  <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                    <input
                      type="number"
                      placeholder="Resident payment without TDS"
                      value={calcResidentTdsBase || ''}
                      onChange={e => setCalcResidentTdsBase(Number(e.target.value))}
                      style={{ flex: 1, padding: '6px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', borderRadius: '4px', fontSize: '12px' }}
                    />
                    <button
                      type="button"
                      onClick={() => {
                        const dis = Math.round(calcResidentTdsBase * 0.30)
                        setClauseForm({
                          ...clauseForm,
                          status: 'Disallowed',
                          reportedAmount: dis,
                          hasDiscrepancy: true,
                          auditorRemarks: `30% disallowance u/s 40(a)(ia) on resident payment of ₹${calcResidentTdsBase.toLocaleString('en-IN')} for failure to deduct/deposit TDS.`
                        })
                      }}
                      style={{ padding: '6px 12px', backgroundColor: 'var(--success)', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: '600' }}
                    >
                      Apply 30%
                    </button>
                  </div>
                </div>
              )}

              {/* Special helper for Clause 26/43B(h) (MSME 45-day overdue rule) */}
              {editingClause.clauseNumber.includes('43B(h)') && (
                <div style={{ backgroundColor: 'rgba(217, 119, 6, 0.08)', border: '1px solid rgba(217, 119, 6, 0.35)', padding: '12px', borderRadius: '6px', fontSize: '12px' }}>
                  <div style={{ fontWeight: '700', color: 'var(--warning)', marginBottom: '6px' }}>Section 43B(h) MSMED Act 15/45-Day Overdue Check</div>
                  <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
                    <label style={{ display: 'flex', alignItems: 'center', gap: '4px', cursor: 'pointer' }}>
                      <input
                        type="checkbox"
                        checked={calcMsmeHasAgreement}
                        onChange={e => setCalcMsmeHasAgreement(e.target.checked)}
                      />
                      Written Agreement (45 days)
                    </label>
                    <input
                      type="number"
                      placeholder="Actual delay (days)"
                      value={calcMsmeDays || ''}
                      onChange={e => setCalcMsmeDays(Number(e.target.value))}
                      style={{ width: '120px', padding: '6px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', borderRadius: '4px', fontSize: '12px' }}
                    />
                    <span style={{ color: calcMsmeDays > (calcMsmeHasAgreement ? 45 : 15) ? 'var(--error)' : 'var(--success)', fontWeight: '700' }}>
                      {calcMsmeDays > (calcMsmeHasAgreement ? 45 : 15) ? 'Overdue -> Disallowed' : 'Within Limit'}
                    </span>
                  </div>
                </div>
              )}

              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Auditor Remarks &amp; Findings</label>
                <textarea
                  rows={4}
                  required
                  value={clauseForm.auditorRemarks}
                  onChange={e => setClauseForm({ ...clauseForm, auditorRemarks: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '13px' }}
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Audit Workpaper Reference</label>
                  <input
                    type="text"
                    placeholder="e.g. WP-40A-01"
                    value={clauseForm.workpaperRef}
                    onChange={e => setClauseForm({ ...clauseForm, workpaperRef: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div style={{ display: 'flex', alignItems: 'center', marginTop: '20px' }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '13px', cursor: 'pointer' }}>
                    <input
                      type="checkbox"
                      checked={clauseForm.hasDiscrepancy}
                      onChange={e => setClauseForm({ ...clauseForm, hasDiscrepancy: e.target.checked })}
                    />
                    Flag as Discrepancy
                  </label>
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '12px' }}>
                <button
                  type="button"
                  onClick={() => setEditingClause(null)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card-subtle)', color: 'var(--text)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: 'none', backgroundColor: 'var(--accent)', color: '#fff', fontWeight: '600', cursor: 'pointer' }}
                >
                  Save Clause Response
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Quick 44AB Applicability Calculator */}
      {showEvalModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', padding: '24px', borderRadius: '8px', maxWidth: '600px', width: '90%', maxHeight: '90vh', overflowY: 'auto' }}>
            <h2 style={{ margin: '0 0 14px', fontSize: '18px', fontWeight: '700' }}>Section 44AB Applicability Calculator</h2>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Activity Category</label>
                  <select
                    value={evalForm.isBusiness ? 'business' : 'profession'}
                    onChange={e => setEvalForm({ ...evalForm, isBusiness: e.target.value === 'business' })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="business">Business (Turnover limit: ₹1 Cr / ₹10 Cr)</option>
                    <option value="profession">Profession (Receipts limit: ₹50 L; ₹75 L only under 44ADA)</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Year</label>
                  <select
                    value={evalForm.financialYear}
                    onChange={e => setEvalForm({ ...evalForm, financialYear: e.target.value })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="2026-27">Tax Year 2026-27 (Income-tax Act, 2025)</option>
                    <option value="2025-26">FY 2025-26 (Income-tax Act, 1961)</option>
                    <option value="2024-25">FY 2024-25 (Income-tax Act, 1961)</option>
                  </select>
                </div>
                {!evalForm.isBusiness && (
                  <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', cursor: 'pointer', alignSelf: 'end' }}>
                    <input
                      type="checkbox"
                      checked={evalForm.optsPresumptive44Ada}
                      onChange={e => setEvalForm({ ...evalForm, optsPresumptive44Ada: e.target.checked })}
                    />
                    Income offered u/s 44ADA at 50% or more
                  </label>
                )}
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Entity Type</label>
                  <select
                    value={evalForm.entityType}
                    onChange={e => setEvalForm({ ...evalForm, entityType: e.target.value })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="Company">Company</option>
                    <option value="Partnership">Partnership</option>
                    <option value="LLP">LLP</option>
                    <option value="Individual">Individual</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Turnover / Gross Receipts (₹)</label>
                  <input
                    type="number"
                    value={evalForm.turnoverOrGrossReceipts}
                    onChange={e => setEvalForm({ ...evalForm, turnoverOrGrossReceipts: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Cash Receipts (₹)</label>
                  <input
                    type="number"
                    value={evalForm.cashReceipts}
                    onChange={e => setEvalForm({ ...evalForm, cashReceipts: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Total Payments (₹)</label>
                  <input
                    type="number"
                    value={evalForm.totalPayments}
                    onChange={e => setEvalForm({ ...evalForm, totalPayments: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Cash Payments (₹)</label>
                  <input
                    type="number"
                    value={evalForm.cashPayments}
                    onChange={e => setEvalForm({ ...evalForm, cashPayments: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <button
                type="button"
                onClick={handleEvaluateApplicability}
                style={{ padding: '8px', backgroundColor: 'var(--accent)', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: '600' }}
              >
                Compute Applicability
              </button>

              {evalResult && (
                <div style={{ padding: '12px', backgroundColor: evalResult.isMandatory ? 'rgba(220, 38, 38, 0.08)' : 'rgba(22, 163, 74, 0.08)', border: `1px solid ${evalResult.isMandatory ? 'rgba(220, 38, 38, 0.35)' : 'rgba(22, 163, 74, 0.35)'}`, borderRadius: '6px', fontSize: '13px' }}>
                  <div style={{ fontWeight: '700', color: evalResult.isMandatory ? 'var(--error)' : 'var(--success)' }}>
                    {evalResult.isMandatory ? 'Mandatory Tax Audit (Section 44AB Applies)' : 'Tax Audit Not Mandatory (Exempt)'}
                  </div>
                  <div style={{ marginTop: '4px', color: 'var(--text-secondary)' }}>{evalResult.rationale}</div>
                  <div style={{ marginTop: '6px', fontSize: '12px', color: 'var(--muted)' }}>
                    Audit Report: <strong>{evalResult.recommendedAuditType}</strong> | Cash Receipts: {evalResult.cashReceiptPercent}% | Cash Payments: {evalResult.cashPaymentPercent}%
                  </div>
                </div>
              )}

              <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowEvalModal(false)}
                  style={{ padding: '6px 14px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card-subtle)', color: 'var(--text)', cursor: 'pointer' }}
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Finalize Assessment */}
      {showFinalizeModal && selectedAssessment && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', padding: '24px', borderRadius: '8px', maxWidth: '520px', width: '90%' }}>
            <h2 style={{ margin: '0 0 12px', fontSize: '18px', fontWeight: '700', color: 'var(--text)' }}>
              Finalize &amp; Sign Off Tax Audit Report
            </h2>
            <p style={{ margin: '0 0 16px', fontSize: '13px', color: 'var(--muted)' }}>
              Finalizing will permanently lock this assessment and all 44 Form 3CD clauses. The legal {selectedAssessment.auditType} report will be frozen for filing.
            </p>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>
                Partner Audit Observation / Qualification Remarks
              </label>
              <textarea
                rows={4}
                value={finalizeNote}
                onChange={e => setFinalizeNote(e.target.value)}
                placeholder="Enter formal observations to be included in Paragraph 3 of Form 3CA / 3CB..."
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card)', color: 'var(--text)', fontSize: '13px' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={() => setShowFinalizeModal(false)}
                style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid var(--border)', backgroundColor: 'var(--card-subtle)', color: 'var(--text)', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleFinalizeAssessment}
                style={{ padding: '8px 16px', borderRadius: '4px', border: 'none', backgroundColor: 'var(--success)', color: '#fff', fontWeight: '600', cursor: 'pointer' }}
              >
                Confirm Final Sign-Off
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
