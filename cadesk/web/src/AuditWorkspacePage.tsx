import { useEffect, useState } from 'react'
import {
  type AuditEngagementDto,
  type AuditWorkingPaperDto,
  type AuditSummaryDto,
  type Client,
  type UserProfile,
  auditApi,
  api
} from './api'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n)

export function AuditWorkspacePage({ me }: { me: UserProfile | null }) {
  const [engagements, setEngagements] = useState<AuditEngagementDto[]>([])
  const [summary, setSummary] = useState<AuditSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selectedEng, setSelectedEng] = useState<AuditEngagementDto | null>(null)
  const [workingPapers, setWorkingPapers] = useState<AuditWorkingPaperDto[]>([])
  const [loadingWps, setLoadingWps] = useState(false)

  // Modals
  const [showNewEngModal, setShowNewEngModal] = useState(false)
  const [showNewWpModal, setShowNewWpModal] = useState(false)
  const [reviewingWp, setReviewingWp] = useState<AuditWorkingPaperDto | null>(null)
  const [editingStatusEng, setEditingStatusEng] = useState<AuditEngagementDto | null>(null)

  // New Engagement form
  const [engForm, setEngForm] = useState({
    clientId: '',
    financialYear: '2024-25',
    auditType: 'statutory',
    title: 'Statutory Audit FY 2024-25',
    benchmarkType: 'revenue',
    benchmarkAmount: 10000000,
    notes: ''
  })

  // New WP form
  const [wpForm, setWpForm] = useState({
    refCode: 'WP/REV/02',
    area: 'Revenue & Receivables',
    title: 'Debtors Direct Balance Confirmation (SA 505)',
    assertion: 'existence',
    objective: 'Circularize debtor balances exceeding performance materiality to obtain independent external confirmations.',
    proceduresPerformed: 'Selected top 15 debtors accounting for 68% of outstanding trade receivables and issued balance confirmation letters.',
    samplingMethod: 'risk_targeted',
    populationValue: 12000000,
    sampleValue: 8160000,
    itemsTestedCount: 15,
    exceptionsNoted: '2 debtors reported minor reconciliation differences due to transit receipts.',
    conclusion: 'satisfactory'
  })

  // Review WP form
  const [reviewForm, setReviewForm] = useState({
    status: 'reviewed',
    reviewNotes: 'Verified supporting third-party balance confirmations. Reconciliations verified.'
  })

  // Status form
  const [statusForm, setStatusForm] = useState({
    status: 'fieldwork',
    auditOpinion: 'unmodified'
  })

  useEffect(() => {
    loadData()
  }, [])

  async function loadData() {
    try {
      setLoading(true)
      const [sumRes, engRes, clRes] = await Promise.all([
        auditApi.summary(),
        auditApi.listEngagements(),
        api<Client[]>('/api/clients')
      ])
      setSummary(sumRes)
      setEngagements(engRes || [])
      setClients(clRes || [])
      if (clRes?.length > 0 && !engForm.clientId) {
        setEngForm(prev => ({ ...prev, clientId: clRes[0].id }))
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load audit data.')
    } finally {
      setLoading(false)
    }
  }

  async function loadWorkingPapers(engagementId: string) {
    try {
      setLoadingWps(true)
      const res = await auditApi.listWorkingPapers(engagementId)
      setWorkingPapers(res || [])
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load working papers.')
    } finally {
      setLoadingWps(false)
    }
  }

  async function handleSelectEngagement(eng: AuditEngagementDto) {
    setSelectedEng(eng)
    await loadWorkingPapers(eng.id)
  }

  async function handleCreateEngagement() {
    if (!engForm.clientId) {
      setError('Please select a client.')
      return
    }
    try {
      const created = await auditApi.createEngagement({
        ...engForm,
        leadPartnerId: me?.id
      })
      setShowNewEngModal(false)
      await loadData()
      await handleSelectEngagement(created)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create engagement.')
    }
  }

  async function handleCreateWorkingPaper() {
    if (!selectedEng) return
    try {
      await auditApi.createWorkingPaper(selectedEng.id, wpForm)
      setShowNewWpModal(false)
      await loadWorkingPapers(selectedEng.id)
      const refreshed = await auditApi.getEngagement(selectedEng.id)
      setSelectedEng(refreshed)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create working paper.')
    }
  }

  async function handleReviewWorkingPaper() {
    if (!reviewingWp || !selectedEng) return
    try {
      await auditApi.reviewWorkingPaper(reviewingWp.id, reviewForm)
      setReviewingWp(null)
      await loadWorkingPapers(selectedEng.id)
      const refreshed = await auditApi.getEngagement(selectedEng.id)
      setSelectedEng(refreshed)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to review working paper.')
    }
  }

  async function handleUpdateEngagementStatus() {
    if (!editingStatusEng) return
    try {
      await auditApi.updateStatus(editingStatusEng.id, statusForm)
      setEditingStatusEng(null)
      await loadData()
      if (selectedEng?.id === editingStatusEng.id) {
        const refreshed = await auditApi.getEngagement(editingStatusEng.id)
        setSelectedEng(refreshed)
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update engagement status.')
    }
  }

  async function handleDeleteWorkingPaper(id: string) {
    if (!selectedEng || !confirm('Delete this working paper?')) return
    try {
      await auditApi.deleteWorkingPaper(id)
      await loadWorkingPapers(selectedEng.id)
      const refreshed = await auditApi.getEngagement(selectedEng.id)
      setSelectedEng(refreshed)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to delete working paper.')
    }
  }

  // Materiality preview calculation for modal
  const calcMaterialityPreview = () => {
    const amt = engForm.benchmarkAmount || 0
    const pct = engForm.benchmarkType === 'profit_before_tax' ? 0.05 : 0.01
    const overall = amt * pct
    const perf = overall * 0.75
    const trivial = overall * 0.05
    return { overall, perf, trivial }
  }
  const matPreview = calcMaterialityPreview()

  return (
    <div style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto', color: '#1e293b' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 'bold', margin: '0 0 4px 0', color: '#0f172a' }}>
            Statutory Audit Workspace (ICAI Standards)
          </h1>
          <p style={{ margin: 0, color: '#64748b', fontSize: '14px' }}>
            Auditing Standards SA 200/300/320/500/530 • Materiality Benchmarking • Multi-Tier Working Papers Review
          </p>
        </div>
        <div style={{ display: 'flex', gap: '12px' }}>
          {selectedEng && (
            <button
              onClick={() => setSelectedEng(null)}
              style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '6px', cursor: 'pointer', fontWeight: 500 }}
            >
              ← Back to Engagements
            </button>
          )}
          <button
            onClick={() => setShowNewEngModal(true)}
            style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
          >
            + New Audit Engagement
          </button>
        </div>
      </div>

      {error && (
        <div style={{ padding: '12px 16px', background: '#fef2f2', border: '1px solid #fecaca', borderRadius: '6px', color: '#dc2626', marginBottom: '16px' }}>
          {error}
        </div>
      )}

      {/* KPI Cards */}
      {summary && !selectedEng && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>TOTAL ENGAGEMENTS</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>{summary.totalEngagements}</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>PLANNING / FIELDWORK</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#0284c7', marginTop: '4px' }}>{summary.planningCount + summary.fieldworkCount}</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>UNDER REVIEW</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#eab308', marginTop: '4px' }}>{summary.reviewCount}</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>SIGNED OFF</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#16a34a', marginTop: '4px' }}>{summary.signedOffCount}</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600 }}>WORKING PAPERS (SA 500)</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#4f46e5', marginTop: '4px' }}>{summary.totalWorkingPapers}</div>
          </div>
        </div>
      )}

      {/* Main View: Engagements List OR Selected Engagement Workspace */}
      {!selectedEng ? (
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
          <h2 style={{ fontSize: '18px', fontWeight: 600, marginTop: 0, marginBottom: '16px' }}>Audit Engagements</h2>

          {loading ? (
            <div style={{ padding: '32px', textAlign: 'center', color: '#64748b' }}>Loading audit engagements...</div>
          ) : engagements.length === 0 ? (
            <div style={{ padding: '48px', textAlign: 'center', color: '#64748b' }}>
              No audit engagements found. Click "+ New Audit Engagement" to start planning an audit.
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0', textAlign: 'left' }}>
                  <th style={{ padding: '10px 12px' }}>Client &amp; Title</th>
                  <th style={{ padding: '10px 12px' }}>FY / Type</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Overall Materiality (SA 320)</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Performance Mat. (75%)</th>
                  <th style={{ padding: '10px 12px' }}>Status</th>
                  <th style={{ padding: '10px 12px' }}>Opinion</th>
                  <th style={{ padding: '10px 12px' }}>WPs Reviewed</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {engagements.map(e => (
                  <tr key={e.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                    <td style={{ padding: '10px 12px' }}>
                      <div style={{ fontWeight: 600, color: '#0f172a' }}>{e.clientName || 'Client ' + e.clientId.slice(0, 8)}</div>
                      <div style={{ fontSize: '12px', color: '#64748b' }}>{e.title}</div>
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{ fontWeight: 500 }}>{e.financialYear}</span>
                      <div style={{ fontSize: '11px', color: '#64748b' }}>{e.auditType.toUpperCase()}</div>
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600, color: '#0369a1' }}>
                      {fmtInr(e.overallMateriality)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', color: '#475569' }}>
                      {fmtInr(e.performanceMateriality)}
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{
                        padding: '2px 8px',
                        borderRadius: '12px',
                        fontSize: '11px',
                        fontWeight: 600,
                        background: e.status === 'partner_signoff' ? '#dcfce7' : e.status === 'review' ? '#fef3c7' : '#e0f2fe',
                        color: e.status === 'partner_signoff' ? '#15803d' : e.status === 'review' ? '#b45309' : '#0369a1'
                      }}>
                        {e.status.toUpperCase()}
                      </span>
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      {e.auditOpinion ? (
                        <span style={{
                          padding: '2px 8px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 600,
                          background: e.auditOpinion === 'unmodified' ? '#f0fdf4' : '#fef2f2',
                          color: e.auditOpinion === 'unmodified' ? '#16a34a' : '#dc2626'
                        }}>
                          {e.auditOpinion.toUpperCase()}
                        </span>
                      ) : (
                        <span style={{ color: '#94a3b8', fontSize: '12px' }}>Pending</span>
                      )}
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{ fontWeight: 600 }}>{e.reviewedWorkingPapers}</span> / {e.totalWorkingPapers}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                        <button
                          onClick={() => handleSelectEngagement(e)}
                          style={{ padding: '4px 10px', fontSize: '12px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: 500 }}
                        >
                          Workspace →
                        </button>
                        <button
                          onClick={() => {
                            setEditingStatusEng(e)
                            setStatusForm({ status: e.status, auditOpinion: e.auditOpinion || 'unmodified' })
                          }}
                          style={{ padding: '4px 8px', fontSize: '12px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}
                        >
                          Status
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ) : (
        /* Workspace Detail View */
        <div>
          {/* Engagement Overview Banner */}
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px', marginBottom: '24px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <div>
                <span style={{
                  padding: '3px 8px',
                  borderRadius: '4px',
                  background: '#e0f2fe',
                  color: '#0369a1',
                  fontWeight: 600,
                  fontSize: '12px'
                }}>
                  {selectedEng.auditType.toUpperCase()} AUDIT • FY {selectedEng.financialYear}
                </span>
                <h2 style={{ fontSize: '20px', fontWeight: 700, margin: '8px 0 4px 0', color: '#0f172a' }}>
                  {selectedEng.title}
                </h2>
                <div style={{ fontSize: '14px', color: '#64748b' }}>
                  Client: <strong>{selectedEng.clientName}</strong>
                </div>
              </div>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  onClick={() => setShowNewWpModal(true)}
                  style={{ padding: '8px 14px', background: '#16a34a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
                >
                  + Add Working Paper
                </button>
              </div>
            </div>

            {/* SA 320 Materiality Card */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', background: '#f8fafc', padding: '14px', borderRadius: '6px', marginTop: '16px', border: '1px solid #e2e8f0' }}>
              <div>
                <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>BENCHMARK ({selectedEng.benchmarkType.toUpperCase()})</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: '#334155', marginTop: '2px' }}>{fmtInr(selectedEng.benchmarkAmount)}</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>OVERALL MATERIALITY (SA 320)</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: '#0284c7', marginTop: '2px' }}>{fmtInr(selectedEng.overallMateriality)}</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>PERFORMANCE MATERIALITY (75%)</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: '#4f46e5', marginTop: '2px' }}>{fmtInr(selectedEng.performanceMateriality)}</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>CLEARLY TRIVIAL (SA 450, 5%)</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: '#64748b', marginTop: '2px' }}>{fmtInr(selectedEng.trivialThreshold)}</div>
              </div>
            </div>
          </div>

          {/* Working Papers Table */}
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h3 style={{ fontSize: '16px', fontWeight: 600, margin: 0 }}>
                Audit Working Papers &amp; Test Procedures ({workingPapers.length})
              </h3>
              <div style={{ fontSize: '12px', color: '#64748b' }}>
                Reviewed: {selectedEng.reviewedWorkingPapers} / {selectedEng.totalWorkingPapers}
              </div>
            </div>

            {loadingWps ? (
              <div style={{ padding: '32px', textAlign: 'center', color: '#64748b' }}>Loading working papers...</div>
            ) : workingPapers.length === 0 ? (
              <div style={{ padding: '32px', textAlign: 'center', color: '#64748b' }}>
                No working papers for this engagement yet.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                <thead>
                  <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0', textAlign: 'left' }}>
                    <th style={{ padding: '10px 12px' }}>Ref / Area</th>
                    <th style={{ padding: '10px 12px' }}>Working Paper Title &amp; Objective</th>
                    <th style={{ padding: '10px 12px' }}>Assertion</th>
                    <th style={{ padding: '10px 12px', textAlign: 'right' }}>Sample / Population</th>
                    <th style={{ padding: '10px 12px' }}>Conclusion</th>
                    <th style={{ padding: '10px 12px' }}>Review Status</th>
                    <th style={{ padding: '10px 12px', textAlign: 'right' }}>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {workingPapers.map(wp => (
                    <tr key={wp.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                      <td style={{ padding: '10px 12px' }}>
                        <span style={{ fontWeight: 700, color: '#0369a1' }}>{wp.refCode}</span>
                        <div style={{ fontSize: '11px', color: '#64748b' }}>{wp.area}</div>
                      </td>
                      <td style={{ padding: '10px 12px', maxWidth: '320px' }}>
                        <div style={{ fontWeight: 600, color: '#0f172a' }}>{wp.title}</div>
                        <div style={{ fontSize: '12px', color: '#64748b', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                          {wp.objective}
                        </div>
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        <span style={{
                          padding: '2px 6px',
                          borderRadius: '4px',
                          background: '#f1f5f9',
                          fontSize: '11px',
                          fontWeight: 500
                        }}>
                          {wp.assertion.toUpperCase()}
                        </span>
                      </td>
                      <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                        <div style={{ fontWeight: 500 }}>{wp.sampleValue > 0 ? fmtInr(wp.sampleValue) : '-'}</div>
                        <div style={{ fontSize: '11px', color: '#64748b' }}>
                          {wp.populationValue > 0 ? `of ${fmtInr(wp.populationValue)}` : ''}
                        </div>
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        <span style={{
                          padding: '2px 8px',
                          borderRadius: '12px',
                          fontSize: '11px',
                          fontWeight: 600,
                          background: wp.conclusion === 'satisfactory' ? '#dcfce7' : wp.conclusion === 'qualified_finding' ? '#fef3c7' : '#fee2e2',
                          color: wp.conclusion === 'satisfactory' ? '#15803d' : wp.conclusion === 'qualified_finding' ? '#b45309' : '#dc2626'
                        }}>
                          {wp.conclusion.replace('_', ' ').toUpperCase()}
                        </span>
                      </td>
                      <td style={{ padding: '10px 12px' }}>
                        <span style={{
                          padding: '2px 8px',
                          borderRadius: '12px',
                          fontSize: '11px',
                          fontWeight: 600,
                          background: wp.status === 'reviewed' || wp.status === 'partner_approved' ? '#dbeafe' : '#f1f5f9',
                          color: wp.status === 'reviewed' || wp.status === 'partner_approved' ? '#1d4ed8' : '#64748b'
                        }}>
                          {wp.status.replace('_', ' ').toUpperCase()}
                        </span>
                        {wp.reviewerName && (
                          <div style={{ fontSize: '10px', color: '#64748b' }}>by {wp.reviewerName}</div>
                        )}
                      </td>
                      <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '6px' }}>
                          <button
                            onClick={() => {
                              setReviewingWp(wp)
                              setReviewForm({ status: 'reviewed', reviewNotes: wp.reviewNotes || '' })
                            }}
                            style={{ padding: '4px 8px', fontSize: '11px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                          >
                            Review
                          </button>
                          <button
                            onClick={() => handleDeleteWorkingPaper(wp.id)}
                            style={{ padding: '4px 6px', fontSize: '11px', background: '#fee2e2', color: '#dc2626', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                          >
                            ✕
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* Modal: New Audit Engagement */}
      {showNewEngModal && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000
        }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '560px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Create Statutory Audit Engagement</h3>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '4px' }}>Client</label>
              <select
                value={engForm.clientId}
                onChange={e => setEngForm({ ...engForm, clientId: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                {clients.map(c => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '4px' }}>Financial Year</label>
                <input
                  type="text"
                  value={engForm.financialYear}
                  onChange={e => setEngForm({ ...engForm, financialYear: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '4px' }}>Audit Type</label>
                <select
                  value={engForm.auditType}
                  onChange={e => setEngForm({ ...engForm, auditType: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="statutory">Statutory Audit (Companies Act 2013)</option>
                  <option value="tax_audit_44ab">Tax Audit (Section 44AB)</option>
                  <option value="internal">Internal Audit</option>
                  <option value="bank_concurrent">Bank Concurrent Audit</option>
                  <option value="limited_review">Limited Review</option>
                </select>
              </div>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '4px' }}>Engagement Title</label>
              <input
                type="text"
                value={engForm.title}
                onChange={e => setEngForm({ ...engForm, title: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            {/* Materiality Benchmark Inputs */}
            <div style={{ background: '#f8fafc', padding: '12px', borderRadius: '6px', border: '1px solid #e2e8f0', marginBottom: '16px' }}>
              <div style={{ fontWeight: 600, fontSize: '13px', marginBottom: '8px', color: '#0369a1' }}>
                SA 320 Materiality Benchmark Setup
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Benchmark Metric</label>
                  <select
                    value={engForm.benchmarkType}
                    onChange={e => setEngForm({ ...engForm, benchmarkType: e.target.value })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  >
                    <option value="revenue">Gross Turnover / Revenue (1%)</option>
                    <option value="profit_before_tax">Profit Before Tax (PBT, 5%)</option>
                    <option value="total_assets">Total Assets (1%)</option>
                    <option value="equity">Net Worth / Equity (2%)</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Metric Amount (₹)</label>
                  <input
                    type="number"
                    value={engForm.benchmarkAmount}
                    onChange={e => setEngForm({ ...engForm, benchmarkAmount: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>
              <div style={{ fontSize: '12px', color: '#0284c7', marginTop: '8px' }}>
                Calculated Materiality: <strong>{fmtInr(matPreview.overall)}</strong> (Performance: {fmtInr(matPreview.perf)})
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button
                onClick={() => setShowNewEngModal(false)}
                style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleCreateEngagement}
                style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}
              >
                Create Engagement
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: New Working Paper */}
      {showNewWpModal && selectedEng && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000
        }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '600px', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Add Audit Working Paper</h3>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Ref Code</label>
                <input
                  type="text"
                  value={wpForm.refCode}
                  onChange={e => setWpForm({ ...wpForm, refCode: e.target.value })}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Area</label>
                <input
                  type="text"
                  value={wpForm.area}
                  onChange={e => setWpForm({ ...wpForm, area: e.target.value })}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Working Paper Title</label>
              <input
                type="text"
                value={wpForm.title}
                onChange={e => setWpForm({ ...wpForm, title: e.target.value })}
                style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Assertion</label>
                <select
                  value={wpForm.assertion}
                  onChange={e => setWpForm({ ...wpForm, assertion: e.target.value })}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="completeness">Completeness</option>
                  <option value="existence">Existence</option>
                  <option value="accuracy">Accuracy</option>
                  <option value="valuation">Valuation</option>
                  <option value="rights_obligations">Rights &amp; Obligations</option>
                  <option value="presentation">Presentation &amp; Disclosure</option>
                </select>
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Sampling Method</label>
                <select
                  value={wpForm.samplingMethod}
                  onChange={e => setWpForm({ ...wpForm, samplingMethod: e.target.value })}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="100_percent">100% Examination</option>
                  <option value="risk_targeted">Risk-Targeted (above Materiality)</option>
                  <option value="monetary_unit_sampling">Monetary Unit Sampling (MUS)</option>
                  <option value="stratified">Stratified Sampling</option>
                  <option value="haphazard">Haphazard Selection</option>
                </select>
              </div>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Objective (SA 500)</label>
              <textarea
                rows={2}
                value={wpForm.objective}
                onChange={e => setWpForm({ ...wpForm, objective: e.target.value })}
                style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Procedures Performed</label>
              <textarea
                rows={3}
                value={wpForm.proceduresPerformed}
                onChange={e => setWpForm({ ...wpForm, proceduresPerformed: e.target.value })}
                style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Population Value (₹)</label>
                <input
                  type="number"
                  value={wpForm.populationValue}
                  onChange={e => setWpForm({ ...wpForm, populationValue: Number(e.target.value) })}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Sample Value Tested (₹)</label>
                <input
                  type="number"
                  value={wpForm.sampleValue}
                  onChange={e => setWpForm({ ...wpForm, sampleValue: Number(e.target.value) })}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '16px' }}>
              <button
                onClick={() => setShowNewWpModal(false)}
                style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleCreateWorkingPaper}
                style={{ padding: '8px 16px', background: '#16a34a', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}
              >
                Save Working Paper
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Review Working Paper */}
      {reviewingWp && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000
        }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '500px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>
              Review Working Paper ({reviewingWp.refCode})
            </h3>
            <div style={{ marginBottom: '12px', fontSize: '13px' }}>
              <strong>Title:</strong> {reviewingWp.title}
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Review Status</label>
              <select
                value={reviewForm.status}
                onChange={e => setReviewForm({ ...reviewForm, status: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                <option value="ready_for_review">Mark Ready For Review</option>
                <option value="reviewed">Approve as Manager / Reviewer</option>
                <option value="partner_approved">Final Partner Sign-off</option>
              </select>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Review Notes &amp; Observations</label>
              <textarea
                rows={3}
                value={reviewForm.reviewNotes}
                onChange={e => setReviewForm({ ...reviewForm, reviewNotes: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button
                onClick={() => setReviewingWp(null)}
                style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleReviewWorkingPaper}
                style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}
              >
                Submit Review
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Status & Opinion */}
      {editingStatusEng && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000
        }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '460px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Update Engagement Status</h3>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Engagement Stage</label>
              <select
                value={statusForm.status}
                onChange={e => setStatusForm({ ...statusForm, status: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                <option value="planning">Planning &amp; Risk Assessment (SA 300/315)</option>
                <option value="fieldwork">Fieldwork &amp; Substantive Testing (SA 500)</option>
                <option value="review">Manager / Partner Review</option>
                <option value="partner_signoff">Partner Sign-off &amp; Audit Report</option>
                <option value="archived">Archived</option>
              </select>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Opinion (SA 700/705)</label>
              <select
                value={statusForm.auditOpinion}
                onChange={e => setStatusForm({ ...statusForm, auditOpinion: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                <option value="unmodified">Unmodified Opinion (Clean Report)</option>
                <option value="qualified">Qualified Opinion (Except For...)</option>
                <option value="adverse">Adverse Opinion</option>
                <option value="disclaimer">Disclaimer of Opinion</option>
              </select>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button
                onClick={() => setEditingStatusEng(null)}
                style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleUpdateEngagementStatus}
                style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}
              >
                Save Status
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
