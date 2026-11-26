import { useCallback, useEffect, useState } from 'react'
import {
  type Schedule3AssessmentDto,
  type Schedule3SummaryDto,
  type Schedule3RatioItemDto,
  type Schedule3AgeingItemDto,
  type Client,
  schedule3Api,
  api
} from './api'
import { PdfActions } from './pdf/PdfActions'
import { buildSchedule3FinancialPackBlocks } from './schedule3/schedule3FinancialPackPdf'
import { CmaBuilderPanel } from './schedule3/CmaBuilderPanel'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0)

type ClientPage = {
  items: Client[]
  total: number
}

export function Schedule3DeskPage({ can }: { can: (p: string) => boolean }) {
  const [assessments, setAssessments] = useState<Schedule3AssessmentDto[]>([])
  const [summary, setSummary] = useState<Schedule3SummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Filters
  const [filterFy, setFilterFy] = useState('2024-25')
  const [filterStatus, setFilterStatus] = useState('all')

  // Selected assessment
  const [selectedAssessment, setSelectedAssessment] = useState<Schedule3AssessmentDto | null>(null)
  const [activeTab, setActiveTab] = useState<'ratios' | 'ageing' | 'note' | 'cma'>('ratios')
  const [ageingSubTab, setAgeingSubTab] = useState<'TradeReceivables' | 'TradePayables' | 'CWIP'>('TradeReceivables')

  // Modals
  const [showNewModal, setShowNewModal] = useState(false)
  const [showRoundingGuide, setShowRoundingGuide] = useState(false)
  const [editingRatio, setEditingRatio] = useState<Schedule3RatioItemDto | null>(null)
  const [editingAgeing, setEditingAgeing] = useState<Schedule3AgeingItemDto | null>(null)
  const [ratioExplanationInput, setRatioExplanationInput] = useState('')
  const [finalizeRemarks, setFinalizeRemarks] = useState('')
  const [showFinalizeModal, setShowFinalizeModal] = useState(false)

  // Ageing item edit form
  const [ageingForm, setAgeingForm] = useState({
    less6MonthsOr1Year: 0,
    oneToTwoYears: 0,
    twoToThreeYears: 0,
    moreThanThreeYears: 0,
    unbilledDues: 0
  })

  // New assessment input form
  const [newForm, setNewForm] = useState({
    clientId: '',
    financialYear: '2024-25',
    precedingFinancialYear: '2023-24',
    accountingStandard: 'Division I - AS (Non-Ind AS)',
    roundingUnit: 'Lakhs',
    totalIncomeCurrent: 850000000,
    totalIncomePreceding: 600000000,
    currentAssetsCurrent: 300000000,
    currentAssetsPreceding: 200000000,
    currentLiabilitiesCurrent: 150000000,
    currentLiabilitiesPreceding: 150000000,
    totalDebtCurrent: 250000000,
    totalDebtPreceding: 300000000,
    shareholdersEquityCurrent: 500000000,
    shareholdersEquityPreceding: 400000000,
    netProfitAfterTaxesCurrent: 100000000,
    netProfitAfterTaxesPreceding: 60000000,
    ebitCurrent: 150000000,
    ebitPreceding: 100000000,
    debtServiceCurrent: 50000000,
    debtServicePreceding: 50000000,
    costOfGoodsSoldCurrent: 480000000,
    costOfGoodsSoldPreceding: 350000000,
    averageInventoryCurrent: 120000000,
    averageInventoryPreceding: 100000000,
    revenueFromOperationsCurrent: 800000000,
    revenueFromOperationsPreceding: 550000000,
    averageTradeReceivablesCurrent: 160000000,
    averageTradeReceivablesPreceding: 120000000,
    netCreditPurchasesCurrent: 400000000,
    netCreditPurchasesPreceding: 300000000,
    averageTradePayablesCurrent: 80000000,
    averageTradePayablesPreceding: 70000000,
    capitalEmployedCurrent: 750000000,
    capitalEmployedPreceding: 700000000,
    incomeFromInvestmentsCurrent: 20000000,
    incomeFromInvestmentsPreceding: 15000000,
    averageInvestmentsCurrent: 200000000,
    averageInvestmentsPreceding: 180000000,
    auditorRemarks: ''
  })

  const loadData = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [list, sum] = await Promise.all([
        schedule3Api.getAssessments(undefined, filterFy, filterStatus === 'all' ? undefined : filterStatus),
        schedule3Api.getSummary()
      ])
      setAssessments(list)
      setSummary(sum)
      setSelectedAssessment(prev => (prev ? (list.find(a => a.id === prev.id) ?? prev) : null))
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load Schedule III assessments.')
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
      const data = await schedule3Api.getAssessmentById(id)
      setSelectedAssessment(data)
      setActiveTab('ratios')
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
      const created = await schedule3Api.createAssessment({
        ...newForm,
        auditEngagementId: null
      })
      setShowNewModal(false)
      await loadData()
      setSelectedAssessment(created)
      setActiveTab('ratios')
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to compute Schedule III ratios.')
    } finally {
      setLoading(false)
    }
  }

  async function handleSaveRatioExplanation(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedAssessment || !editingRatio) return

    setLoading(true)
    setError(null)
    try {
      const updated = await schedule3Api.updateRatioExplanation(selectedAssessment.id, editingRatio.id, {
        varianceExplanation: ratioExplanationInput
      })
      setSelectedAssessment(updated)
      setEditingRatio(null)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to save ratio variance explanation.')
    } finally {
      setLoading(false)
    }
  }

  function openEditAgeing(item: Schedule3AgeingItemDto) {
    setEditingAgeing(item)
    setAgeingForm({
      less6MonthsOr1Year: item.less6MonthsOr1Year,
      oneToTwoYears: item.oneToTwoYears,
      twoToThreeYears: item.twoToThreeYears,
      moreThanThreeYears: item.moreThanThreeYears,
      unbilledDues: item.unbilledDues
    })
  }

  async function handleSaveAgeing(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedAssessment || !editingAgeing) return

    setLoading(true)
    setError(null)
    try {
      const updated = await schedule3Api.updateAgeingItem(selectedAssessment.id, editingAgeing.id, {
        less6MonthsOr1Year: Number(ageingForm.less6MonthsOr1Year),
        oneToTwoYears: Number(ageingForm.oneToTwoYears),
        twoToThreeYears: Number(ageingForm.twoToThreeYears),
        moreThanThreeYears: Number(ageingForm.moreThanThreeYears),
        unbilledDues: Number(ageingForm.unbilledDues)
      })
      setSelectedAssessment(updated)
      setEditingAgeing(null)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to save ageing item.')
    } finally {
      setLoading(false)
    }
  }

  async function handleRegenerateNote() {
    if (!selectedAssessment) return
    setLoading(true)
    setError(null)
    try {
      const res = await schedule3Api.generateNote(selectedAssessment.id)
      setSelectedAssessment({
        ...selectedAssessment,
        generatedDisclosureNoteText: res.disclosureNoteText
      })
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to regenerate disclosure note.')
    } finally {
      setLoading(false)
    }
  }

  async function handleFinalize() {
    if (!selectedAssessment) return
    setLoading(true)
    setError(null)
    try {
      const finalized = await schedule3Api.finalizeAssessment(selectedAssessment.id, finalizeRemarks)
      setSelectedAssessment(finalized)
      setShowFinalizeModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to finalize Schedule III assessment.')
    } finally {
      setLoading(false)
    }
  }

  const activeAgeingItems = selectedAssessment?.ageingItems.filter(i => i.scheduleType === ageingSubTab) || []

  return (
    <div style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto', fontFamily: 'system-ui, -apple-system, sans-serif' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '26px', fontWeight: '700', color: 'var(--text)' }}>
            Schedule III Financial Ratios & Disclosures Engine
          </h1>
          <p style={{ margin: '4px 0 0', color: 'var(--muted)', fontSize: '14px' }}>
            Companies Act 2013 Schedule III mandatory 11 ratios, &gt; 25% variance alerts, ageing schedules, and rounding off compliance.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '12px' }}>
          <button
            onClick={() => setShowRoundingGuide(true)}
            style={{
              padding: '10px 16px',
              backgroundColor: 'var(--card-subtle)',
              color: 'var(--text-secondary)',
              border: '1px solid var(--border-subtle)',
              borderRadius: '6px',
              cursor: 'pointer',
              fontWeight: '600',
              fontSize: '14px'
            }}
          >
            Rounding Off Rules Guide
          </button>
          {can('schedule3.manage') && (
            <button
              onClick={() => setShowNewModal(true)}
              style={{
                padding: '10px 16px',
                backgroundColor: '#2563eb',
                color: '#fff',
                border: 'none',
                borderRadius: '6px',
                cursor: 'pointer',
                fontWeight: '600',
                fontSize: '14px',
                boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
              }}
            >
              + New Schedule III Ratios
            </button>
          )}
        </div>
      </div>

      {error && (
        <div style={{ padding: '14px', backgroundColor: 'var(--error-bg)', border: '1px solid #ef4444', color: 'var(--error-text)', borderRadius: '8px', marginBottom: '20px', fontSize: '14px' }}>
          {error}
        </div>
      )}

      {/* KPI Cards */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
        <div style={{ backgroundColor: 'var(--card)', padding: '18px', borderRadius: '8px', border: '1px solid var(--border)' }}>
          <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--muted)', textTransform: 'uppercase' }}>Assessments Total</div>
          <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--text)', marginTop: '6px' }}>{summary?.totalAssessments ?? 0}</div>
          <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px' }}>
            {summary?.inReviewCount ?? 0} In Review | {summary?.finalizedCount ?? 0} Finalized
          </div>
        </div>

        <div style={{ backgroundColor: 'var(--card)', padding: '18px', borderRadius: '8px', border: '1px solid var(--border)' }}>
          <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--muted)', textTransform: 'uppercase' }}>Significant Variances</div>
          <div style={{ fontSize: '24px', fontWeight: '700', color: 'var(--warning)', marginTop: '6px' }}>
            {summary?.totalSignificantVariances ?? 0}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--warning)', marginTop: '4px' }}>Variance &ge; 25% (Explanation required)</div>
        </div>

        <div style={{ backgroundColor: 'var(--card)', padding: '18px', borderRadius: '8px', border: '1px solid var(--border)' }}>
          <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--muted)', textTransform: 'uppercase' }}>Missing Explanations</div>
          <div style={{ fontSize: '24px', fontWeight: '700', color: (summary?.totalMissingExplanations ?? 0) > 0 ? 'var(--error)' : '#16a34a', marginTop: '6px' }}>
            {summary?.totalMissingExplanations ?? 0}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px' }}>Mandatory Schedule III compliance gaps</div>
        </div>

        <div style={{ backgroundColor: 'var(--card)', padding: '18px', borderRadius: '8px', border: '1px solid var(--border)' }}>
          <div style={{ fontSize: '12px', fontWeight: '600', color: 'var(--muted)', textTransform: 'uppercase' }}>Rounding Rule Compliant</div>
          <div style={{ fontSize: '24px', fontWeight: '700', color: '#16a34a', marginTop: '6px' }}>
            {summary?.roundingCompliantCount ?? 0} / {summary?.totalAssessments ?? 0}
          </div>
          <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px' }}>₹100 Cr threshold rules validated</div>
        </div>
      </div>

      {/* Main Grid: Left side Assessment Selector, Right side Workbench */}
      <div style={{ display: 'grid', gridTemplateColumns: selectedAssessment ? '360px 1fr' : '1fr', gap: '20px' }}>
        {/* Left Column: List */}
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '16px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
            <h2 style={{ margin: 0, fontSize: '16px', fontWeight: '700', color: 'var(--text)' }}>Ratio Assessments</h2>
            <div style={{ display: 'flex', gap: '8px' }}>
              <select
                value={filterFy}
                onChange={e => setFilterFy(e.target.value)}
                style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '12px' }}
              >
                <option value="2024-25">FY 2024-25</option>
                <option value="2023-24">FY 2023-24</option>
              </select>
              <select
                value={filterStatus}
                onChange={e => setFilterStatus(e.target.value)}
                style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '12px' }}
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
              No Schedule III assessments found. Create one to begin.
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
                      backgroundColor: isSelected ? 'var(--accent-subtle)' : 'var(--card-subtle)',
                      cursor: 'pointer',
                      transition: 'all 0.15s ease'
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                      <div style={{ fontWeight: '700', fontSize: '14px', color: 'var(--text)' }}>{a.clientName || 'Company'}</div>
                      <span
                        style={{
                          fontSize: '11px',
                          padding: '2px 6px',
                          borderRadius: '4px',
                          fontWeight: '600',
                          backgroundColor:
                            a.assessmentStatus === 'finalized'
                              ? '#dcfce7'
                              : a.assessmentStatus === 'in_review'
                              ? '#fef3c7'
                              : '#e2e8f0',
                          color:
                            a.assessmentStatus === 'finalized'
                              ? '#166534'
                              : a.assessmentStatus === 'in_review'
                              ? '#92400e'
                              : '#475569'
                        }}
                      >
                        {a.assessmentStatus.toUpperCase()}
                      </span>
                    </div>

                    <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>
                      {a.financialYear} (PY: {a.precedingFinancialYear}) | In {a.roundingUnit}
                    </div>

                    <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '2px' }}>
                      Income: {fmtInr(a.totalIncomeCurrent)} | {a.accountingStandard}
                    </div>

                    <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '8px', paddingTop: '6px', borderTop: '1px solid var(--border)', fontSize: '12px' }}>
                      <span style={{ color: a.isRoundingRuleCompliant ? '#16a34a' : 'var(--error)', fontWeight: '600' }}>
                        {a.isRoundingRuleCompliant ? '● Rounding OK' : '▲ Rounding Issue'}
                      </span>
                      <span style={{ fontWeight: '700', color: a.significantVariancesCount > 0 ? 'var(--warning)' : '#16a34a' }}>
                        {a.significantVariancesCount} &ge;25% Variances ({a.missingExplanationsCount} missing)
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
                    {selectedAssessment.clientName}
                  </h2>
                  <span
                    style={{
                      fontSize: '12px',
                      padding: '2px 8px',
                      borderRadius: '4px',
                      fontWeight: '700',
                      backgroundColor: 'var(--accent-subtle)',
                      color: 'var(--accent)'
                    }}
                  >
                    {selectedAssessment.accountingStandard}
                  </span>
                  <span
                    style={{
                      fontSize: '12px',
                      padding: '2px 8px',
                      borderRadius: '4px',
                      fontWeight: '600',
                      backgroundColor: selectedAssessment.isRoundingRuleCompliant ? 'var(--success-bg)' : 'var(--error-bg)',
                      color: selectedAssessment.isRoundingRuleCompliant ? 'var(--success-text)' : 'var(--error-text)'
                    }}
                  >
                    {selectedAssessment.isRoundingRuleCompliant ? 'Rounding Rule Compliant' : 'Rounding Non-Compliant'}
                  </span>
                </div>
                <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '6px' }}>
                  Financial Year: <strong>{selectedAssessment.financialYear}</strong> (Preceding: {selectedAssessment.precedingFinancialYear}) | Unit: <strong>In {selectedAssessment.roundingUnit}</strong>
                </div>
                <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '2px' }}>
                  Total Income: <strong>{fmtInr(selectedAssessment.totalIncomeCurrent)}</strong> ({selectedAssessment.roundingRuleRecommendation})
                </div>
              </div>

              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', alignItems: 'center' }}>
                <PdfActions
                  title={`Statutory Financial Pack - ${selectedAssessment.clientName ?? 'Company'} - FY ${selectedAssessment.financialYear}`}
                  options={{
                    author: 'CaDesk Practice Suite',
                    header: `${selectedAssessment.clientName ?? 'Company'} • Schedule III Financial Statements • FY ${selectedAssessment.financialYear}`,
                    footer: `CaDesk Companies Act 2013 Schedule III Pack • ${selectedAssessment.accountingStandard}`,
                  }}
                  source={async () => {
                    const pack = await schedule3Api.getFinancialPackData(selectedAssessment.id)
                    return buildSchedule3FinancialPackBlocks({ assessment: selectedAssessment, pack })
                  }}
                  clientId={selectedAssessment.clientId}
                  fy={selectedAssessment.financialYear}
                  canSave={true}
                />
                <button
                  onClick={handleRegenerateNote}
                  style={{
                    padding: '8px 12px',
                    backgroundColor: 'var(--card-subtle)',
                    color: 'var(--text)',
                    border: '1px solid var(--border-subtle)',
                    borderRadius: '6px',
                    fontSize: '13px',
                    cursor: 'pointer',
                    fontWeight: '600'
                  }}
                >
                  Regenerate Note
                </button>
                {selectedAssessment.assessmentStatus !== 'finalized' && can('schedule3.finalize') && (
                  <button
                    onClick={() => {
                      setFinalizeRemarks(selectedAssessment.auditorRemarks || '')
                      setShowFinalizeModal(true)
                    }}
                    style={{
                      padding: '8px 14px',
                      backgroundColor: '#16a34a',
                      color: '#fff',
                      border: 'none',
                      borderRadius: '6px',
                      fontSize: '13px',
                      cursor: 'pointer',
                      fontWeight: '600'
                    }}
                  >
                    Finalize & Sign Off
                  </button>
                )}
              </div>
            </div>

            {/* Quick Metrics Bar */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '12px', padding: '14px 0', borderBottom: '1px solid var(--border)', fontSize: '13px' }}>
              <div>
                <span style={{ color: 'var(--muted)' }}>Revenue: </span>
                <strong>{fmtInr(selectedAssessment.revenueFromOperationsCurrent)}</strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Net Profit: </span>
                <strong>{fmtInr(selectedAssessment.netProfitAfterTaxesCurrent)}</strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Current Assets: </span>
                <strong>{fmtInr(selectedAssessment.currentAssetsCurrent)}</strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Current Liab: </span>
                <strong>{fmtInr(selectedAssessment.currentLiabilitiesCurrent)}</strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Significant Variances: </span>
                <strong style={{ color: selectedAssessment.significantVariancesCount > 0 ? 'var(--warning)' : '#16a34a' }}>
                  {selectedAssessment.significantVariancesCount}
                </strong>
              </div>
              <div>
                <span style={{ color: 'var(--muted)' }}>Missing Notes: </span>
                <strong style={{ color: selectedAssessment.missingExplanationsCount > 0 ? 'var(--error)' : '#16a34a' }}>
                  {selectedAssessment.missingExplanationsCount}
                </strong>
              </div>
            </div>

            {/* Tabs */}
            <div style={{ display: 'flex', gap: '8px', borderBottom: '1px solid var(--border)', margin: '16px 0 20px' }}>
              <button
                onClick={() => setActiveTab('ratios')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: activeTab === 'ratios' ? '2px solid var(--accent)' : 'none',
                  backgroundColor: 'transparent',
                  color: activeTab === 'ratios' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: activeTab === 'ratios' ? '700' : '500',
                  cursor: 'pointer',
                  fontSize: '14px'
                }}
              >
                11 Analytical Ratios ({selectedAssessment.ratios.length})
              </button>
              <button
                onClick={() => setActiveTab('ageing')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: activeTab === 'ageing' ? '2px solid var(--accent)' : 'none',
                  backgroundColor: 'transparent',
                  color: activeTab === 'ageing' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: activeTab === 'ageing' ? '700' : '500',
                  cursor: 'pointer',
                  fontSize: '14px'
                }}
              >
                Statutory Ageing Schedules ({selectedAssessment.ageingItems.length})
              </button>
              <button
                onClick={() => setActiveTab('note')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: activeTab === 'note' ? '2px solid var(--accent)' : 'none',
                  backgroundColor: 'transparent',
                  color: activeTab === 'note' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: activeTab === 'note' ? '700' : '500',
                  cursor: 'pointer',
                  fontSize: '14px'
                }}
              >
                Schedule III Note Preview
              </button>
              <button
                onClick={() => setActiveTab('cma')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: activeTab === 'cma' ? '2px solid var(--accent)' : 'none',
                  backgroundColor: 'transparent',
                  color: activeTab === 'cma' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: activeTab === 'cma' ? '700' : '500',
                  cursor: 'pointer',
                  fontSize: '14px'
                }}
              >
                CMA Data &amp; Bank Projections
              </button>
            </div>

            {/* Tab 1: 11 Ratios Table */}
            {activeTab === 'ratios' && (
              <div>
                <div style={{ overflowX: 'auto', border: '1px solid var(--border)', borderRadius: '6px' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
                    <thead>
                      <tr style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', color: 'var(--text-secondary)', fontWeight: '600' }}>
                        <th style={{ padding: '10px 12px' }}>Code</th>
                        <th style={{ padding: '10px 12px' }}>Ratio Name & Formula</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>{selectedAssessment.financialYear}</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>{selectedAssessment.precedingFinancialYear}</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>Variance %</th>
                        <th style={{ padding: '10px 12px' }}>Status & Mandatory Note</th>
                        <th style={{ padding: '10px 12px', textAlign: 'center' }}>Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedAssessment.ratios.map(r => (
                        <tr
                          key={r.id}
                          style={{
                            borderBottom: '1px solid var(--border)',
                            backgroundColor: r.isVarianceSignificant ? 'var(--warning-bg)' : 'var(--card)'
                          }}
                        >
                          <td style={{ padding: '10px 12px', fontWeight: '700' }}>{r.ratioCode}</td>
                          <td style={{ padding: '10px 12px' }}>
                            <div style={{ fontWeight: '600', color: 'var(--text)' }}>{r.ratioName}</div>
                            <div style={{ fontSize: '11px', color: 'var(--muted)' }}>
                              [{r.numeratorDescription}] / [{r.denominatorDescription}]
                            </div>
                          </td>
                          <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: '700' }}>
                            {r.currentYearValue.toFixed(2)}
                          </td>
                          <td style={{ padding: '10px 12px', textAlign: 'right', color: 'var(--muted)' }}>
                            {r.precedingYearValue.toFixed(2)}
                          </td>
                          <td
                            style={{
                              padding: '10px 12px',
                              textAlign: 'right',
                              fontWeight: '700',
                              color: r.isVarianceSignificant ? 'var(--error)' : '#16a34a'
                            }}
                          >
                            {r.variancePercent > 0 ? `+${r.variancePercent.toFixed(2)}%` : `${r.variancePercent.toFixed(2)}%`}
                          </td>
                          <td style={{ padding: '10px 12px', maxWidth: '300px' }}>
                            {r.isVarianceSignificant ? (
                              r.isExplanationProvided ? (
                                <div style={{ fontSize: '12px', color: 'var(--success-text)' }}>
                                  <strong>Note: </strong> {r.varianceExplanation}
                                </div>
                              ) : (
                                <span style={{ fontSize: '11px', padding: '2px 6px', backgroundColor: 'var(--error-bg)', color: 'var(--error-text)', borderRadius: '4px', fontWeight: '700' }}>
                                  MANDATORY EXPLANATION MISSING (&ge; 25%)
                                </span>
                              )
                            ) : (
                              <span style={{ fontSize: '11px', color: 'var(--muted)' }}>Variance &lt; 25% (Normal)</span>
                            )}
                          </td>
                          <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                            {selectedAssessment.assessmentStatus !== 'finalized' && can('schedule3.manage') ? (
                              <button
                                onClick={() => {
                                  setEditingRatio(r)
                                  setRatioExplanationInput(r.varianceExplanation || '')
                                }}
                                style={{
                                  padding: '4px 10px',
                                  backgroundColor: 'var(--card-subtle)',
                                  border: '1px solid var(--border-subtle)',
                                  borderRadius: '4px',
                                  cursor: 'pointer',
                                  fontSize: '12px',
                                  fontWeight: '600'
                                }}
                              >
                                {r.isExplanationProvided ? 'Edit Note' : 'Add Note'}
                              </button>
                            ) : (
                              <span style={{ color: 'var(--muted)', fontSize: '12px' }}>Locked</span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Tab 2: Ageing Schedules */}
            {activeTab === 'ageing' && (
              <div>
                <div style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
                  <button
                    onClick={() => setAgeingSubTab('TradeReceivables')}
                    style={{
                      padding: '8px 14px',
                      borderRadius: '6px',
                      border: '1px solid var(--border-subtle)',
                      backgroundColor: ageingSubTab === 'TradeReceivables' ? '#2563eb' : 'var(--card-subtle)',
                      color: ageingSubTab === 'TradeReceivables' ? '#fff' : 'var(--text-secondary)',
                      fontWeight: '600',
                      fontSize: '13px',
                      cursor: 'pointer'
                    }}
                  >
                    Trade Receivables Ageing
                  </button>
                  <button
                    onClick={() => setAgeingSubTab('TradePayables')}
                    style={{
                      padding: '8px 14px',
                      borderRadius: '6px',
                      border: '1px solid var(--border-subtle)',
                      backgroundColor: ageingSubTab === 'TradePayables' ? '#2563eb' : 'var(--card-subtle)',
                      color: ageingSubTab === 'TradePayables' ? '#fff' : 'var(--text-secondary)',
                      fontWeight: '600',
                      fontSize: '13px',
                      cursor: 'pointer'
                    }}
                  >
                    Trade Payables Ageing
                  </button>
                  <button
                    onClick={() => setAgeingSubTab('CWIP')}
                    style={{
                      padding: '8px 14px',
                      borderRadius: '6px',
                      border: '1px solid var(--border-subtle)',
                      backgroundColor: ageingSubTab === 'CWIP' ? '#2563eb' : 'var(--card-subtle)',
                      color: ageingSubTab === 'CWIP' ? '#fff' : 'var(--text-secondary)',
                      fontWeight: '600',
                      fontSize: '13px',
                      cursor: 'pointer'
                    }}
                  >
                    CWIP & Intangibles
                  </button>
                </div>

                <div style={{ overflowX: 'auto', border: '1px solid var(--border)', borderRadius: '6px' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
                    <thead>
                      <tr style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', color: 'var(--text-secondary)', fontWeight: '600' }}>
                        <th style={{ padding: '10px 12px' }}>Category / Particulars</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>&lt; 6m / 1y</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>1 - 2 years</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>2 - 3 years</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>&gt; 3 years</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>Unbilled</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>Total</th>
                        <th style={{ padding: '10px 12px', textAlign: 'center' }}>Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {activeAgeingItems.map(item => (
                        <tr key={item.id} style={{ borderBottom: '1px solid var(--border)' }}>
                          <td style={{ padding: '10px 12px', fontWeight: '600', color: 'var(--text)' }}>
                            {item.particularsCategory}
                          </td>
                          <td style={{ padding: '10px 12px', textAlign: 'right' }}>{fmtInr(item.less6MonthsOr1Year)}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right' }}>{fmtInr(item.oneToTwoYears)}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right' }}>{fmtInr(item.twoToThreeYears)}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right' }}>{fmtInr(item.moreThanThreeYears)}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right' }}>{fmtInr(item.unbilledDues)}</td>
                          <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: '700', color: 'var(--text)' }}>
                            {fmtInr(item.total)}
                          </td>
                          <td style={{ padding: '10px 12px', textAlign: 'center' }}>
                            {selectedAssessment.assessmentStatus !== 'finalized' && can('schedule3.manage') ? (
                              <button
                                onClick={() => openEditAgeing(item)}
                                style={{
                                  padding: '4px 10px',
                                  backgroundColor: 'var(--card-subtle)',
                                  border: '1px solid var(--border-subtle)',
                                  borderRadius: '4px',
                                  cursor: 'pointer',
                                  fontSize: '12px',
                                  fontWeight: '600'
                                }}
                              >
                                Edit Buckets
                              </button>
                            ) : (
                              <span style={{ color: 'var(--muted)', fontSize: '12px' }}>Locked</span>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Tab 3: Note Preview */}
            {activeTab === 'note' && (
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
                  <h3 style={{ margin: 0, fontSize: '16px', color: 'var(--text)' }}>
                    Schedule III Financial Statement Note Draft
                  </h3>
                  <button
                    onClick={() => {
                      if (selectedAssessment.generatedDisclosureNoteText) {
                        navigator.clipboard.writeText(selectedAssessment.generatedDisclosureNoteText)
                        alert('Schedule III disclosure note copied to clipboard!')
                      }
                    }}
                    style={{
                      padding: '6px 12px',
                      backgroundColor: '#2563eb',
                      color: '#fff',
                      border: 'none',
                      borderRadius: '4px',
                      fontSize: '12px',
                      cursor: 'pointer',
                      fontWeight: '600'
                    }}
                  >
                    Copy Note to Clipboard
                  </button>
                </div>

                <textarea
                  readOnly
                  value={selectedAssessment.generatedDisclosureNoteText || 'No disclosure note text generated.'}
                  rows={20}
                  style={{
                    width: '100%',
                    boxSizing: 'border-box',
                    padding: '16px',
                    fontFamily: 'Consolas, Monaco, monospace',
                    fontSize: '12px',
                    lineHeight: '1.5',
                    backgroundColor: '#0f172a',
                    color: '#f8fafc',
                    borderRadius: '6px',
                    border: '1px solid #334155',
                    resize: 'vertical'
                  }}
                />
              </div>
            )}

            {/* Tab 4: CMA Data & Bank Projections */}
            {activeTab === 'cma' && (
              <CmaBuilderPanel assessment={selectedAssessment} />
            )}
          </div>
        )}
      </div>

      {/* Modal: New Schedule III Ratios */}
      {showNewModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', maxWidth: '780px', width: '90%', maxHeight: '90vh', overflowY: 'auto' }}>
            <h2 style={{ margin: '0 0 16px', fontSize: '18px', fontWeight: '700' }}>New Schedule III Financial Ratios Analysis</h2>
            <form onSubmit={handleCreateAssessment} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>Client Company</label>
                <select
                  required
                  value={newForm.clientId}
                  onChange={e => setNewForm({ ...newForm, clientId: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="">-- Select Client --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>
                      {c.name} ({c.code || 'No Code'})
                    </option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: '10px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Current FY</label>
                  <select
                    value={newForm.financialYear}
                    onChange={e => setNewForm({ ...newForm, financialYear: e.target.value })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="2024-25">2024-25</option>
                    <option value="2023-24">2023-24</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Preceding FY</label>
                  <select
                    value={newForm.precedingFinancialYear}
                    onChange={e => setNewForm({ ...newForm, precedingFinancialYear: e.target.value })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="2023-24">2023-24</option>
                    <option value="2022-23">2022-23</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Standard Division</label>
                  <select
                    value={newForm.accountingStandard}
                    onChange={e => setNewForm({ ...newForm, accountingStandard: e.target.value })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Division I - AS (Non-Ind AS)">Division I - AS</option>
                    <option value="Division II - Ind AS">Division II - Ind AS</option>
                    <option value="Division III - NBFC (Ind AS)">Division III - NBFC</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Rounding Unit</label>
                  <select
                    value={newForm.roundingUnit}
                    onChange={e => setNewForm({ ...newForm, roundingUnit: e.target.value })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Hundreds">Hundreds (&lt; ₹100 Cr)</option>
                    <option value="Thousands">Thousands (&lt; ₹100 Cr)</option>
                    <option value="Lakhs">Lakhs (All)</option>
                    <option value="Millions">Millions (All)</option>
                    <option value="Crores">Crores (&ge; ₹100 Cr)</option>
                  </select>
                </div>
              </div>

              {/* Key Financial Inputs Table */}
              <div style={{ backgroundColor: 'var(--card-subtle)', padding: '12px', borderRadius: '6px', border: '1px solid var(--border)' }}>
                <div style={{ fontWeight: '700', fontSize: '13px', color: 'var(--text)', marginBottom: '10px' }}>
                  Financial Statement Line Items (Current vs Preceding FY)
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: '8px', alignItems: 'center', fontSize: '12px' }}>
                  <div style={{ fontWeight: '600' }}>Line Item Particulars</div>
                  <div style={{ fontWeight: '600' }}>Current FY (₹)</div>
                  <div style={{ fontWeight: '600' }}>Preceding FY (₹)</div>

                  <div>Total Income (Turnover threshold check)</div>
                  <input
                    type="number"
                    value={newForm.totalIncomeCurrent}
                    onChange={e => setNewForm({ ...newForm, totalIncomeCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.totalIncomePreceding}
                    onChange={e => setNewForm({ ...newForm, totalIncomePreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Current Assets (Current Ratio)</div>
                  <input
                    type="number"
                    value={newForm.currentAssetsCurrent}
                    onChange={e => setNewForm({ ...newForm, currentAssetsCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.currentAssetsPreceding}
                    onChange={e => setNewForm({ ...newForm, currentAssetsPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Current Liabilities (Current Ratio)</div>
                  <input
                    type="number"
                    value={newForm.currentLiabilitiesCurrent}
                    onChange={e => setNewForm({ ...newForm, currentLiabilitiesCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.currentLiabilitiesPreceding}
                    onChange={e => setNewForm({ ...newForm, currentLiabilitiesPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Total Debt (Short + Long term borrowings)</div>
                  <input
                    type="number"
                    value={newForm.totalDebtCurrent}
                    onChange={e => setNewForm({ ...newForm, totalDebtCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.totalDebtPreceding}
                    onChange={e => setNewForm({ ...newForm, totalDebtPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Shareholder's Equity (Capital + Reserves)</div>
                  <input
                    type="number"
                    value={newForm.shareholdersEquityCurrent}
                    onChange={e => setNewForm({ ...newForm, shareholdersEquityCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.shareholdersEquityPreceding}
                    onChange={e => setNewForm({ ...newForm, shareholdersEquityPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Revenue from Operations</div>
                  <input
                    type="number"
                    value={newForm.revenueFromOperationsCurrent}
                    onChange={e => setNewForm({ ...newForm, revenueFromOperationsCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.revenueFromOperationsPreceding}
                    onChange={e => setNewForm({ ...newForm, revenueFromOperationsPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Net Profit after Taxes</div>
                  <input
                    type="number"
                    value={newForm.netProfitAfterTaxesCurrent}
                    onChange={e => setNewForm({ ...newForm, netProfitAfterTaxesCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.netProfitAfterTaxesPreceding}
                    onChange={e => setNewForm({ ...newForm, netProfitAfterTaxesPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Cost of Goods Sold / Materials Consumed</div>
                  <input
                    type="number"
                    value={newForm.costOfGoodsSoldCurrent}
                    onChange={e => setNewForm({ ...newForm, costOfGoodsSoldCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.costOfGoodsSoldPreceding}
                    onChange={e => setNewForm({ ...newForm, costOfGoodsSoldPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Average Inventory</div>
                  <input
                    type="number"
                    value={newForm.averageInventoryCurrent}
                    onChange={e => setNewForm({ ...newForm, averageInventoryCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.averageInventoryPreceding}
                    onChange={e => setNewForm({ ...newForm, averageInventoryPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Average Trade Receivables</div>
                  <input
                    type="number"
                    value={newForm.averageTradeReceivablesCurrent}
                    onChange={e => setNewForm({ ...newForm, averageTradeReceivablesCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.averageTradeReceivablesPreceding}
                    onChange={e => setNewForm({ ...newForm, averageTradeReceivablesPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Net Credit Purchases</div>
                  <input
                    type="number"
                    value={newForm.netCreditPurchasesCurrent}
                    onChange={e => setNewForm({ ...newForm, netCreditPurchasesCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.netCreditPurchasesPreceding}
                    onChange={e => setNewForm({ ...newForm, netCreditPurchasesPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Average Trade Payables</div>
                  <input
                    type="number"
                    value={newForm.averageTradePayablesCurrent}
                    onChange={e => setNewForm({ ...newForm, averageTradePayablesCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.averageTradePayablesPreceding}
                    onChange={e => setNewForm({ ...newForm, averageTradePayablesPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>EBIT (Operating Earnings)</div>
                  <input
                    type="number"
                    value={newForm.ebitCurrent}
                    onChange={e => setNewForm({ ...newForm, ebitCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.ebitPreceding}
                    onChange={e => setNewForm({ ...newForm, ebitPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Debt Service (Interest + Principal Repayment)</div>
                  <input
                    type="number"
                    value={newForm.debtServiceCurrent}
                    onChange={e => setNewForm({ ...newForm, debtServiceCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.debtServicePreceding}
                    onChange={e => setNewForm({ ...newForm, debtServicePreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />

                  <div>Capital Employed</div>
                  <input
                    type="number"
                    value={newForm.capitalEmployedCurrent}
                    onChange={e => setNewForm({ ...newForm, capitalEmployedCurrent: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <input
                    type="number"
                    value={newForm.capitalEmployedPreceding}
                    onChange={e => setNewForm({ ...newForm, capitalEmployedPreceding: Number(e.target.value) })}
                    style={{ padding: '4px 6px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowNewModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid var(--border-subtle)', backgroundColor: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: 'none', backgroundColor: '#2563eb', color: '#fff', fontWeight: '600', cursor: 'pointer' }}
                >
                  Compute 11 Ratios
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Edit Ratio Explanation */}
      {editingRatio && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', maxWidth: '580px', width: '90%' }}>
            <h2 style={{ margin: '0 0 8px', fontSize: '18px', fontWeight: '700' }}>
              {editingRatio.ratioCode}: {editingRatio.ratioName}
            </h2>
            <div style={{ fontSize: '13px', color: 'var(--muted)', marginBottom: '14px' }}>
              Variance: <strong style={{ color: editingRatio.isVarianceSignificant ? 'var(--error)' : '#16a34a' }}>{editingRatio.variancePercent.toFixed(2)}%</strong> (Formula: [{editingRatio.numeratorDescription}] / [{editingRatio.denominatorDescription}])
            </div>

            <form onSubmit={handleSaveRatioExplanation} style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>
                  Mandatory Explanation of Variance (&ge; 25% Change)
                </label>
                <textarea
                  rows={4}
                  required
                  placeholder="Explain the underlying business reasons for the variance as required under Schedule III..."
                  value={ratioExplanationInput}
                  onChange={e => setRatioExplanationInput(e.target.value)}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setEditingRatio(null)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid var(--border-subtle)', backgroundColor: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: 'none', backgroundColor: '#2563eb', color: '#fff', fontWeight: '600', cursor: 'pointer' }}
                >
                  Save Explanation
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Edit Ageing Buckets */}
      {editingAgeing && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', maxWidth: '580px', width: '90%' }}>
            <h2 style={{ margin: '0 0 8px', fontSize: '18px', fontWeight: '700' }}>
              Edit Ageing: {editingAgeing.particularsCategory}
            </h2>
            <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '14px' }}>
              Schedule Type: {editingAgeing.scheduleType}
            </div>

            <form onSubmit={handleSaveAgeing} style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>&lt; 6 months / 1 year (₹)</label>
                  <input
                    type="number"
                    value={ageingForm.less6MonthsOr1Year}
                    onChange={e => setAgeingForm({ ...ageingForm, less6MonthsOr1Year: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>1 - 2 years (₹)</label>
                  <input
                    type="number"
                    value={ageingForm.oneToTwoYears}
                    onChange={e => setAgeingForm({ ...ageingForm, oneToTwoYears: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>2 - 3 years (₹)</label>
                  <input
                    type="number"
                    value={ageingForm.twoToThreeYears}
                    onChange={e => setAgeingForm({ ...ageingForm, twoToThreeYears: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>&gt; 3 years (₹)</label>
                  <input
                    type="number"
                    value={ageingForm.moreThanThreeYears}
                    onChange={e => setAgeingForm({ ...ageingForm, moreThanThreeYears: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: '600', marginBottom: '4px' }}>Unbilled Dues (₹)</label>
                <input
                  type="number"
                  value={ageingForm.unbilledDues}
                  onChange={e => setAgeingForm({ ...ageingForm, unbilledDues: Number(e.target.value) })}
                  style={{ width: '100%', padding: '6px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '10px' }}>
                <button
                  type="button"
                  onClick={() => setEditingAgeing(null)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid var(--border-subtle)', backgroundColor: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: 'none', backgroundColor: '#2563eb', color: '#fff', fontWeight: '600', cursor: 'pointer' }}
                >
                  Update Ageing
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Rounding Off Rules Guide */}
      {showRoundingGuide && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', maxWidth: '600px', width: '90%' }}>
            <h2 style={{ margin: '0 0 12px', fontSize: '18px', fontWeight: '700' }}>Schedule III Rounding Off Instructions</h2>
            <div style={{ fontSize: '13px', color: 'var(--text-secondary)', lineHeight: '1.6' }}>
              <p>Under the General Instructions of Schedule III (Paragraph 4), rounding off is strictly governed by the company's <strong>Total Income</strong>:</p>
              <div style={{ backgroundColor: 'var(--card-subtle)', padding: '12px', borderRadius: '6px', border: '1px solid var(--border)', marginBottom: '12px' }}>
                <div style={{ fontWeight: '700', color: 'var(--text)' }}>1. Total Income &lt; ₹100 Crores:</div>
                <div>May round off to the nearest <strong>Hundreds, Thousands, Lakhs, or Millions</strong>, or decimals thereof.</div>
              </div>
              <div style={{ backgroundColor: 'var(--card-subtle)', padding: '12px', borderRadius: '6px', border: '1px solid var(--border)', marginBottom: '12px' }}>
                <div style={{ fontWeight: '700', color: 'var(--text)' }}>2. Total Income &ge; ₹100 Crores:</div>
                <div>May round off to the nearest <strong>Lakhs, Millions, or Crores</strong>, or decimals thereof. (Rounding in Hundreds or Thousands is prohibited!)</div>
              </div>
              <p style={{ fontSize: '12px', color: 'var(--muted)' }}>
                Note: Once a unit of measurement is chosen, it must be used consistently across all components of the financial statements and notes.
              </p>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '16px' }}>
              <button
                type="button"
                onClick={() => setShowRoundingGuide(false)}
                style={{ padding: '6px 14px', borderRadius: '4px', border: '1px solid var(--border-subtle)', backgroundColor: 'var(--card)', cursor: 'pointer' }}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Finalize Assessment */}
      {showFinalizeModal && selectedAssessment && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', maxWidth: '520px', width: '90%' }}>
            <h2 style={{ margin: '0 0 12px', fontSize: '18px', fontWeight: '700', color: 'var(--text)' }}>
              Finalize Schedule III Financial Ratios
            </h2>
            <p style={{ margin: '0 0 16px', fontSize: '13px', color: 'var(--muted)' }}>
              Finalizing will permanently lock all 11 analytical ratios, variance explanations, and ageing schedules. The disclosure note draft will be frozen for publication.
            </p>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: '600', marginBottom: '4px' }}>
                Auditor Concurrence Remarks
              </label>
              <textarea
                rows={4}
                value={finalizeRemarks}
                onChange={e => setFinalizeRemarks(e.target.value)}
                placeholder="Enter audit sign-off remarks on ratio variance reasonableness..."
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={() => setShowFinalizeModal(false)}
                style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid var(--border-subtle)', backgroundColor: 'var(--card)', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleFinalize}
                style={{ padding: '8px 16px', borderRadius: '4px', border: 'none', backgroundColor: '#16a34a', color: '#fff', fontWeight: '600', cursor: 'pointer' }}
              >
                Confirm Sign-Off
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
