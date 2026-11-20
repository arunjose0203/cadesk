import { useEffect, useState } from 'react'
import {
  type AuditEngagementDto,
  type AuditWorkingPaperDto,
  type AuditSummaryDto,
  type AuditEqrReviewDto,
  type AuditReviewNoteDto,
  type Client,
  type UserProfile,
  auditApi,
  api
} from './api'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0)

export function AuditWorkspacePage({ me }: { me: UserProfile | null }) {
  const [engagements, setEngagements] = useState<AuditEngagementDto[]>([])
  const [summary, setSummary] = useState<AuditSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selectedEng, setSelectedEng] = useState<AuditEngagementDto | null>(null)

  // Selected engagement sub-view: workpapers, eqr, review_notes
  const [activeTab, setActiveTab] = useState<'workpapers' | 'eqr' | 'review_notes'>('workpapers')

  // Data for active engagement
  const [workingPapers, setWorkingPapers] = useState<AuditWorkingPaperDto[]>([])
  const [eqrReview, setEqrReview] = useState<AuditEqrReviewDto | null>(null)
  const [reviewNotes, setReviewNotes] = useState<AuditReviewNoteDto[]>([])
  const [loadingWps, setLoadingWps] = useState(false)

  // Modals
  const [showNewEngModal, setShowNewEngModal] = useState(false)
  const [showNewWpModal, setShowNewWpModal] = useState(false)
  const [reviewingWp, setReviewingWp] = useState<AuditWorkingPaperDto | null>(null)
  const [showSignoffModal, setShowSignoffModal] = useState(false)
  const [showNewNoteModal, setShowNewNoteModal] = useState(false)
  const [respondingNote, setRespondingNote] = useState<AuditReviewNoteDto | null>(null)
  const [noteResponseText, setNoteResponseText] = useState('')

  // New Engagement form
  const [engForm, setEngForm] = useState({
    clientId: '',
    financialYear: '2024-25',
    auditType: 'statutory',
    title: 'Statutory Audit FY 2024-25',
    benchmarkType: 'revenue',
    benchmarkAmount: 50000000,
    eqrRequired: true,
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

  // Signoff form
  const [signoffForm, setSignoffForm] = useState({
    auditReportDate: new Date().toISOString().split('T')[0],
    auditOpinion: 'unmodified',
    notes: 'Unmodified true and fair audit opinion issued under SA 700.'
  })

  // New Review Note form
  const [noteForm, setNoteForm] = useState({
    workingPaperId: '',
    raisedByRole: 'manager',
    category: 'documentation_gap',
    priority: 'medium',
    query: ''
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

  async function loadEngagementDetails(engId: string) {
    try {
      setLoadingWps(true)
      const [wps, eqr, notes, eng] = await Promise.all([
        auditApi.listWorkingPapers(engId),
        auditApi.getEqrReview(engId).catch(() => null),
        auditApi.listReviewNotes(engId),
        auditApi.getEngagement(engId)
      ])
      setWorkingPapers(wps || [])
      setEqrReview(eqr)
      setReviewNotes(notes || [])
      setSelectedEng(eng)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load engagement details.')
    } finally {
      setLoadingWps(false)
    }
  }

  async function handleSelectEngagement(eng: AuditEngagementDto) {
    setSelectedEng(eng)
    setActiveTab('workpapers')
    await loadEngagementDetails(eng.id)
  }

  async function handleCreateEngagement() {
    if (!engForm.clientId) {
      setError('Please select a client.')
      return
    }
    try {
      const created = await auditApi.createEngagement({
        ...engForm,
        leadPartnerId: me?.id,
        eqrPartnerId: engForm.eqrRequired ? me?.id : null
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
      await loadEngagementDetails(selectedEng.id)
      await loadData()
    } catch (err: unknown) {
      alert(`Error creating working paper: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleReviewWorkingPaper() {
    if (!reviewingWp || !selectedEng) return
    try {
      await auditApi.reviewWorkingPaper(reviewingWp.id, reviewForm)
      setReviewingWp(null)
      await loadEngagementDetails(selectedEng.id)
      await loadData()
    } catch (err: unknown) {
      alert(`Error reviewing working paper: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleDeleteWorkingPaper(id: string) {
    if (!selectedEng || !confirm('Delete this working paper?')) return
    try {
      await auditApi.deleteWorkingPaper(id)
      await loadEngagementDetails(selectedEng.id)
      await loadData()
    } catch (err: unknown) {
      alert(`Error deleting working paper: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleSignOffAudit() {
    if (!selectedEng) return
    try {
      await auditApi.signOffAudit(selectedEng.id, signoffForm)
      setShowSignoffModal(false)
      await loadEngagementDetails(selectedEng.id)
      await loadData()
      alert('Audit report successfully signed off! Statutory 60-day archival countdown started.')
    } catch (err: unknown) {
      alert(`Sign-off blocked: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleArchiveEngagement() {
    if (!selectedEng || !confirm('Are you sure you want to lock and archive this audit file? Once archived, all workpapers become permanently immutable.')) return
    try {
      await auditApi.archiveEngagement(selectedEng.id, { notes: 'Sealed & archived under ICAI SQC 1.' })
      await loadEngagementDetails(selectedEng.id)
      await loadData()
      alert('Engagement file successfully locked and archived under ICAI SQC 1!')
    } catch (err: unknown) {
      alert(`Archival failed: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleToggleChecklistItem(code: string) {
    if (!eqrReview) return
    const updated = eqrReview.preIssuanceChecklist.map(c => {
      if (c.code === code) {
        const nextState = !c.isVerified
        return { ...c, isVerified: nextState, verifiedAt: nextState ? new Date().toISOString() : null }
      }
      return c
    })
    try {
      const res = await auditApi.updateEqr(eqrReview.id, { preIssuanceChecklist: updated })
      setEqrReview(res)
    } catch (err: unknown) {
      alert(`Failed to update checklist: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleClearEqr() {
    if (!eqrReview || !selectedEng) return
    const unverified = eqrReview.preIssuanceChecklist.filter(c => c.required && !c.isVerified)
    if (unverified.length > 0) {
      alert(`Cannot grant clearance: ${unverified.length} mandatory checklist item(s) are not yet verified.`)
      return
    }
    const remarks = prompt('Enter EQR Conclusion & Concurrence remarks:', 'Concurred with unmodified opinion. Evaluated significant risks, materiality, and going concern under SA 220.')
    if (!remarks) return
    try {
      const res = await auditApi.clearEqr(eqrReview.id, {
        eqrConclusionRemarks: remarks,
        preIssuanceChecklist: eqrReview.preIssuanceChecklist
      })
      setEqrReview(res)
      await loadEngagementDetails(selectedEng.id)
      alert('EQR Hot Review clearance granted successfully!')
    } catch (err: unknown) {
      alert(`EQR clearance failed: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleCreateReviewNote() {
    if (!selectedEng) return
    if (!noteForm.query.trim()) {
      alert('Please enter query description.')
      return
    }
    try {
      await auditApi.createReviewNote(selectedEng.id, {
        ...noteForm,
        workingPaperId: noteForm.workingPaperId || null
      })
      setShowNewNoteModal(false)
      setNoteForm({ workingPaperId: '', raisedByRole: 'manager', category: 'documentation_gap', priority: 'medium', query: '' })
      await loadEngagementDetails(selectedEng.id)
    } catch (err: unknown) {
      alert(`Failed to add review note: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleRespondReviewNote() {
    if (!respondingNote || !selectedEng) return
    if (!noteResponseText.trim()) {
      alert('Please enter response text.')
      return
    }
    try {
      await auditApi.respondReviewNote(respondingNote.id, { response: noteResponseText })
      setRespondingNote(null)
      setNoteResponseText('')
      await loadEngagementDetails(selectedEng.id)
    } catch (err: unknown) {
      alert(`Failed to submit response: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleClearReviewNote(id: string) {
    if (!selectedEng) return
    try {
      await auditApi.clearReviewNote(id)
      await loadEngagementDetails(selectedEng.id)
    } catch (err: unknown) {
      alert(`Failed to clear review note: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleReopenReviewNote(id: string) {
    if (!selectedEng) return
    const remarks = prompt('Enter reopening remarks:')
    if (!remarks) return
    try {
      await auditApi.reopenReviewNote(id, { remarks })
      await loadEngagementDetails(selectedEng.id)
    } catch (err: unknown) {
      alert(`Failed to reopen review note: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  return (
    <div style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto', color: '#1e293b' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 4px 0', color: '#0f172a' }}>
            Digital Workpapers &amp; Engagement Quality Review (EQR) Desk
          </h1>
          <p style={{ fontSize: '13px', color: '#64748b', margin: 0 }}>
            ICAI SQC 1 &amp; SA 220 compliant audit documentation, multi-tier review notes, Hot/Cold EQR reviews, and statutory 60-day archival lock.
          </p>
        </div>
        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            onClick={() => setShowNewEngModal(true)}
            style={{
              padding: '10px 18px',
              background: '#2563eb',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 600,
              fontSize: '13px',
              cursor: 'pointer'
            }}
          >
            + New Audit Engagement
          </button>
        </div>
      </div>

      {error && (
        <div style={{ padding: '12px 16px', background: '#fef2f2', border: '1px solid #fecaca', borderRadius: '6px', color: '#b91c1c', marginBottom: '20px', fontSize: '14px' }}>
          {error}
        </div>
      )}

      {/* Summary KPI Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: '16px', marginBottom: '24px' }}>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Total Engagements</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>{summary.totalEngagements}</div>
            <div style={{ fontSize: '12px', color: '#16a34a', marginTop: '2px' }}>{summary.signedOffCount} Signed Off</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>EQR Reviews (SA 220)</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#7c3aed', marginTop: '4px' }}>{summary.eqrPendingCount}</div>
            <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>Hot Reviews Pending</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Audit Queries / Notes</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: summary.openReviewNotesCount > 0 ? '#dc2626' : '#16a34a', marginTop: '4px' }}>
              {summary.openReviewNotesCount}
            </div>
            <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>Uncleared Gate Items</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Working Papers</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>{summary.totalWorkingPapers}</div>
            <div style={{ fontSize: '12px', color: '#eab308', marginTop: '2px' }}>{summary.exceptionsFlaggedCount} Exceptions Flagged</div>
          </div>
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>SQC 1 Archival (60d)</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#0369a1', marginTop: '4px' }}>{summary.archivedCount}</div>
            <div style={{ fontSize: '12px', color: summary.archivalOverdueCount > 0 ? '#dc2626' : '#16a34a', marginTop: '2px', fontWeight: 600 }}>
              {summary.archivalOverdueCount > 0 ? `${summary.archivalOverdueCount} Overdue!` : `${summary.archivalDueSoonCount} Due Soon`}
            </div>
          </div>
        </div>
      )}

      {/* Main Content Area */}
      {!selectedEng ? (
        /* Engagements List View */
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h2 style={{ fontSize: '16px', fontWeight: 600, margin: 0 }}>Statutory Engagements Directory</h2>
            <div style={{ fontSize: '12px', color: '#64748b' }}>Showing {engagements.length} engagements</div>
          </div>

          {loading ? (
            <div style={{ padding: '40px', textAlign: 'center', color: '#64748b' }}>Loading audit engagements...</div>
          ) : engagements.length === 0 ? (
            <div style={{ padding: '40px', textAlign: 'center', color: '#64748b' }}>No audit engagements found.</div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0', textAlign: 'left' }}>
                  <th style={{ padding: '10px 12px' }}>Client / Engagement</th>
                  <th style={{ padding: '10px 12px' }}>FY</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Materiality</th>
                  <th style={{ padding: '10px 12px' }}>Stage</th>
                  <th style={{ padding: '10px 12px' }}>EQR Status</th>
                  <th style={{ padding: '10px 12px' }}>SQC 1 Archival</th>
                  <th style={{ padding: '10px 12px' }}>Audit Queries</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {engagements.map(e => (
                  <tr key={e.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                    <td style={{ padding: '10px 12px' }}>
                      <div style={{ fontWeight: 600, color: '#0f172a' }}>{e.title}</div>
                      <div style={{ fontSize: '12px', color: '#64748b' }}>{e.clientName}</div>
                    </td>
                    <td style={{ padding: '10px 12px', fontWeight: 500 }}>{e.financialYear}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600, color: '#0284c7' }}>
                      {fmtInr(e.overallMateriality)}
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{
                        padding: '2px 8px',
                        borderRadius: '12px',
                        fontSize: '11px',
                        fontWeight: 600,
                        background: e.status === 'partner_signoff' ? '#dcfce7' : e.status === 'archived' ? '#f1f5f9' : e.status === 'review' ? '#fef3c7' : '#e0f2fe',
                        color: e.status === 'partner_signoff' ? '#15803d' : e.status === 'archived' ? '#475569' : e.status === 'review' ? '#b45309' : '#0369a1'
                      }}>
                        {e.status.toUpperCase()}
                      </span>
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      {e.eqrRequired ? (
                        <span style={{
                          padding: '2px 8px',
                          borderRadius: '12px',
                          fontSize: '11px',
                          fontWeight: 600,
                          background: e.eqrStatus === 'clearance_granted' ? '#dcfce7' : '#f3e8ff',
                          color: e.eqrStatus === 'clearance_granted' ? '#15803d' : '#7c3aed'
                        }}>
                          {e.eqrStatus === 'clearance_granted' ? 'EQR CLEARED' : 'HOT REVIEW'}
                        </span>
                      ) : (
                        <span style={{ color: '#94a3b8', fontSize: '11px' }}>N/A</span>
                      )}
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{
                        fontSize: '11px',
                        fontWeight: 600,
                        color: e.isArchived ? '#475569' : e.archivalStatus.includes('OVERDUE') ? '#dc2626' : '#0369a1'
                      }}>
                        {e.isArchived ? '🔒 Sealed' : e.archivalStatus}
                      </span>
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      {e.openReviewNotesCount > 0 ? (
                        <span style={{ color: '#dc2626', fontWeight: 600 }}>{e.openReviewNotesCount} Open</span>
                      ) : (
                        <span style={{ color: '#16a34a', fontWeight: 500 }}>Cleared</span>
                      )}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                      <button
                        onClick={() => handleSelectEngagement(e)}
                        style={{ padding: '5px 12px', fontSize: '12px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: 500 }}
                      >
                        Open Desk →
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ) : (
        /* Workspace Detail View with Tabs */
        <div>
          {/* Top Banner with SQC 1 Archival Lock & Sign-Off Actions */}
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px', marginBottom: '20px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <div>
                <button
                  onClick={() => setSelectedEng(null)}
                  style={{ background: 'none', border: 'none', color: '#2563eb', fontSize: '12px', cursor: 'pointer', padding: 0, marginBottom: '6px', fontWeight: 600 }}
                >
                  ← Back to Engagements
                </button>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <span style={{ padding: '3px 8px', borderRadius: '4px', background: '#e0f2fe', color: '#0369a1', fontWeight: 600, fontSize: '12px' }}>
                    {selectedEng.auditType.toUpperCase()} • FY {selectedEng.financialYear}
                  </span>
                  {selectedEng.isArchived && (
                    <span style={{ padding: '3px 8px', borderRadius: '4px', background: '#f1f5f9', color: '#334155', fontWeight: 700, fontSize: '12px' }}>
                      🔒 ARCHIVED &amp; SEALED (ICAI SQC 1)
                    </span>
                  )}
                  {selectedEng.status === 'partner_signoff' && (
                    <span style={{ padding: '3px 8px', borderRadius: '4px', background: '#dcfce7', color: '#15803d', fontWeight: 700, fontSize: '12px' }}>
                      SIGNED OFF • OPINION: {selectedEng.auditOpinion?.toUpperCase()}
                    </span>
                  )}
                </div>
                <h2 style={{ fontSize: '20px', fontWeight: 700, margin: '8px 0 4px 0', color: '#0f172a' }}>
                  {selectedEng.title}
                </h2>
                <div style={{ fontSize: '13px', color: '#64748b' }}>
                  Client: <strong>{selectedEng.clientName}</strong> | Lead Partner: <strong>{selectedEng.leadPartnerName || 'Unassigned'}</strong>
                </div>
              </div>

              {/* Action Buttons */}
              <div style={{ display: 'flex', gap: '8px' }}>
                {!selectedEng.isArchived && selectedEng.status !== 'partner_signoff' && (
                  <button
                    onClick={() => setShowSignoffModal(true)}
                    style={{
                      padding: '8px 14px',
                      background: selectedEng.canSignOff ? '#16a34a' : '#94a3b8',
                      color: '#fff',
                      border: 'none',
                      borderRadius: '6px',
                      fontWeight: 600,
                      cursor: selectedEng.canSignOff ? 'pointer' : 'not-allowed',
                      fontSize: '13px'
                    }}
                    title={selectedEng.signOffValidationMessage}
                  >
                    Sign Off Audit Report (SA 700)
                  </button>
                )}
                {!selectedEng.isArchived && selectedEng.status === 'partner_signoff' && (
                  <button
                    onClick={handleArchiveEngagement}
                    style={{ padding: '8px 14px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer', fontSize: '13px' }}
                  >
                    Seal &amp; Archive File (SQC 1)
                  </button>
                )}
              </div>
            </div>

            {/* Archival Countdown Meter */}
            <div style={{ marginTop: '16px', padding: '12px', background: '#f8fafc', borderRadius: '6px', border: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <span style={{ fontSize: '11px', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>SQC 1 Archival Status (Para 74 - 60 Days): </span>
                <span style={{ fontSize: '13px', fontWeight: 700, color: selectedEng.isArchived ? '#334155' : selectedEng.archivalStatus.includes('OVERDUE') ? '#dc2626' : '#0284c7' }}>
                  {selectedEng.archivalStatus}
                </span>
                {selectedEng.auditReportDate && (
                  <span style={{ fontSize: '12px', color: '#64748b', marginLeft: '12px' }}>
                    (Report Date: {selectedEng.auditReportDate} • Deadline: {selectedEng.archivalDeadline})
                  </span>
                )}
              </div>
              <div>
                <span style={{ fontSize: '11px', fontWeight: 600, color: '#64748b' }}>Open Queries: </span>
                <span style={{ fontSize: '12px', fontWeight: 700, color: selectedEng.openReviewNotesCount > 0 ? '#dc2626' : '#16a34a' }}>
                  {selectedEng.openReviewNotesCount}
                </span>
              </div>
            </div>

            {/* SA 320 Materiality Card */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', background: '#f8fafc', padding: '12px', borderRadius: '6px', marginTop: '12px', border: '1px solid #e2e8f0' }}>
              <div>
                <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>BENCHMARK ({selectedEng.benchmarkType.toUpperCase()})</div>
                <div style={{ fontSize: '14px', fontWeight: 700, color: '#334155' }}>{fmtInr(selectedEng.benchmarkAmount)}</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>OVERALL MATERIALITY</div>
                <div style={{ fontSize: '14px', fontWeight: 700, color: '#0284c7' }}>{fmtInr(selectedEng.overallMateriality)}</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>PERFORMANCE MATERIALITY</div>
                <div style={{ fontSize: '14px', fontWeight: 700, color: '#4f46e5' }}>{fmtInr(selectedEng.performanceMateriality)}</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>CLEARLY TRIVIAL (5%)</div>
                <div style={{ fontSize: '14px', fontWeight: 700, color: '#64748b' }}>{fmtInr(selectedEng.trivialThreshold)}</div>
              </div>
            </div>
          </div>

          {/* Sub Navigation Tabs */}
          <div style={{ display: 'flex', gap: '8px', borderBottom: '2px solid #e2e8f0', marginBottom: '20px' }}>
            <button
              onClick={() => setActiveTab('workpapers')}
              style={{
                padding: '10px 18px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'workpapers' ? '2px solid #2563eb' : '2px solid transparent',
                color: activeTab === 'workpapers' ? '#2563eb' : '#64748b',
                fontWeight: 600,
                fontSize: '14px',
                cursor: 'pointer',
                marginBottom: '-2px'
              }}
            >
              Digital Workpapers ({workingPapers.length})
            </button>
            <button
              onClick={() => setActiveTab('eqr')}
              style={{
                padding: '10px 18px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'eqr' ? '2px solid #2563eb' : '2px solid transparent',
                color: activeTab === 'eqr' ? '#2563eb' : '#64748b',
                fontWeight: 600,
                fontSize: '14px',
                cursor: 'pointer',
                marginBottom: '-2px',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>EQR Review Desk (SA 220)</span>
              {selectedEng.eqrRequired && (
                <span style={{
                  padding: '1px 6px',
                  borderRadius: '10px',
                  fontSize: '10px',
                  background: eqrReview?.reviewStatus === 'clearance_granted' ? '#dcfce7' : '#f3e8ff',
                  color: eqrReview?.reviewStatus === 'clearance_granted' ? '#15803d' : '#7c3aed'
                }}>
                  {eqrReview?.reviewStatus === 'clearance_granted' ? 'CLEARED' : 'PENDING'}
                </span>
              )}
            </button>
            <button
              onClick={() => setActiveTab('review_notes')}
              style={{
                padding: '10px 18px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'review_notes' ? '2px solid #2563eb' : '2px solid transparent',
                color: activeTab === 'review_notes' ? '#2563eb' : '#64748b',
                fontWeight: 600,
                fontSize: '14px',
                cursor: 'pointer',
                marginBottom: '-2px',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>Audit Queries &amp; Review Notes</span>
              <span style={{
                padding: '1px 6px',
                borderRadius: '10px',
                fontSize: '10px',
                background: selectedEng.openReviewNotesCount > 0 ? '#fee2e2' : '#f1f5f9',
                color: selectedEng.openReviewNotesCount > 0 ? '#dc2626' : '#64748b'
              }}>
                {reviewNotes.length} ({selectedEng.openReviewNotesCount} open)
              </span>
            </button>
          </div>

          {/* TAB 1: Digital Workpapers */}
          {activeTab === 'workpapers' && (
            <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <h3 style={{ fontSize: '16px', fontWeight: 600, margin: 0 }}>Working Papers Directory</h3>
                {!selectedEng.isArchived && (
                  <button
                    onClick={() => setShowNewWpModal(true)}
                    style={{ padding: '6px 12px', background: '#16a34a', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, fontSize: '12px', cursor: 'pointer' }}
                  >
                    + Add Workpaper
                  </button>
                )}
              </div>

              {loadingWps ? (
                <div style={{ padding: '32px', textAlign: 'center', color: '#64748b' }}>Loading working papers...</div>
              ) : workingPapers.length === 0 ? (
                <div style={{ padding: '32px', textAlign: 'center', color: '#64748b' }}>No working papers found.</div>
              ) : (
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                  <thead>
                    <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0', textAlign: 'left' }}>
                      <th style={{ padding: '10px 12px' }}>Ref / Area</th>
                      <th style={{ padding: '10px 12px' }}>Working Paper Title</th>
                      <th style={{ padding: '10px 12px' }}>Assertion</th>
                      <th style={{ padding: '10px 12px', textAlign: 'right' }}>Sample / Population</th>
                      <th style={{ padding: '10px 12px' }}>Conclusion</th>
                      <th style={{ padding: '10px 12px' }}>Queries</th>
                      <th style={{ padding: '10px 12px' }}>Status</th>
                      <th style={{ padding: '10px 12px', textAlign: 'right' }}>Action</th>
                    </tr>
                  </thead>
                  <tbody>
                    {workingPapers.map(wp => (
                      <tr key={wp.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '10px 12px' }}>
                          <span style={{ fontWeight: 700, color: '#0369a1' }}>{wp.refCode}</span>
                          <div style={{ fontSize: '11px', color: '#64748b' }}>{wp.area}</div>
                        </td>
                        <td style={{ padding: '10px 12px', maxWidth: '300px' }}>
                          <div style={{ fontWeight: 600, color: '#0f172a' }}>{wp.title}</div>
                          <div style={{ fontSize: '11px', color: '#64748b', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                            {wp.objective}
                          </div>
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          <span style={{ padding: '2px 6px', background: '#f1f5f9', borderRadius: '4px', fontSize: '11px', textTransform: 'uppercase' }}>
                            {wp.assertion}
                          </span>
                        </td>
                        <td style={{ padding: '10px 12px', textAlign: 'right', fontFamily: 'monospace' }}>
                          <div>{fmtInr(wp.sampleValue)}</div>
                          <div style={{ fontSize: '10px', color: '#94a3b8' }}>Pop: {fmtInr(wp.populationValue)}</div>
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          <span style={{
                            fontSize: '11px',
                            fontWeight: 600,
                            color: wp.conclusion === 'satisfactory' ? '#16a34a' : '#ea580c'
                          }}>
                            {wp.conclusion.toUpperCase()}
                          </span>
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          {wp.openReviewNotesCount > 0 ? (
                            <span style={{ color: '#dc2626', fontWeight: 700, fontSize: '11px' }}>{wp.openReviewNotesCount} Open</span>
                          ) : (
                            <span style={{ color: '#94a3b8', fontSize: '11px' }}>{wp.reviewNotesCount} Notes</span>
                          )}
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          <span style={{
                            padding: '2px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            background: wp.status === 'reviewed' ? '#dcfce7' : '#fef3c7',
                            color: wp.status === 'reviewed' ? '#15803d' : '#b45309'
                          }}>
                            {wp.status.toUpperCase()}
                          </span>
                        </td>
                        <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                          {!selectedEng.isArchived && (
                            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '6px' }}>
                              <button
                                onClick={() => {
                                  setNoteForm({ workingPaperId: wp.id, raisedByRole: 'manager', category: 'documentation_gap', priority: 'medium', query: '' })
                                  setShowNewNoteModal(true)
                                }}
                                style={{ padding: '3px 8px', fontSize: '11px', background: '#f8fafc', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}
                              >
                                + Query
                              </button>
                              <button
                                onClick={() => {
                                  setReviewingWp(wp)
                                  setReviewForm({ status: wp.status, reviewNotes: wp.reviewNotes || '' })
                                }}
                                style={{ padding: '3px 8px', fontSize: '11px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}
                              >
                                Review
                              </button>
                              <button
                                onClick={() => handleDeleteWorkingPaper(wp.id)}
                                style={{ padding: '3px 6px', fontSize: '11px', background: '#fff', border: '1px solid #fecaca', color: '#dc2626', borderRadius: '4px', cursor: 'pointer' }}
                              >
                                ✕
                              </button>
                            </div>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            </div>
          )}

          {/* TAB 2: EQR Review Desk */}
          {activeTab === 'eqr' && (
            <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #e2e8f0', paddingBottom: '12px' }}>
                <div>
                  <h3 style={{ fontSize: '16px', fontWeight: 600, margin: 0 }}>Engagement Quality Control Review (ICAI SA 220)</h3>
                  <p style={{ fontSize: '12px', color: '#64748b', margin: '4px 0 0 0' }}>
                    Reviewer: <strong>{eqrReview?.eqrPartnerName || 'Assigned EQR Partner'}</strong> • Mode: <strong>{eqrReview?.reviewType.toUpperCase()}</strong>
                  </p>
                </div>
                {!selectedEng.isArchived && eqrReview && eqrReview.reviewStatus !== 'clearance_granted' && (
                  <button
                    onClick={handleClearEqr}
                    style={{ padding: '8px 16px', background: '#7c3aed', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
                  >
                    Grant EQR Clearance (Hot Review)
                  </button>
                )}
              </div>

              {!eqrReview ? (
                <div style={{ padding: '32px', textAlign: 'center', color: '#64748b' }}>No EQR review initialized for this engagement.</div>
              ) : (
                <div style={{ display: 'grid', gridTemplateColumns: '1.4fr 1fr', gap: '24px' }}>
                  {/* Left: Pre-Issuance Quality Checklist */}
                  <div>
                    <h4 style={{ fontSize: '14px', fontWeight: 600, margin: '0 0 12px 0' }}>
                      Pre-Issuance Quality Checklist (10 Mandatory SA Controls)
                    </h4>
                    <div style={{ border: '1px solid #e2e8f0', borderRadius: '6px', overflow: 'hidden' }}>
                      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                        <thead>
                          <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0', textAlign: 'left' }}>
                            <th style={{ padding: '8px 10px', width: '32px' }}>Status</th>
                            <th style={{ padding: '8px 10px' }}>Standard</th>
                            <th style={{ padding: '8px 10px' }}>Requirement Description</th>
                          </tr>
                        </thead>
                        <tbody>
                          {eqrReview.preIssuanceChecklist.map(c => (
                            <tr key={c.code} style={{ borderBottom: '1px solid #f1f5f9' }}>
                              <td style={{ padding: '8px 10px', textAlign: 'center' }}>
                                <input
                                  type="checkbox"
                                  checked={c.isVerified}
                                  disabled={selectedEng.isArchived}
                                  onChange={() => handleToggleChecklistItem(c.code)}
                                  style={{ cursor: 'pointer' }}
                                />
                              </td>
                              <td style={{ padding: '8px 10px', fontWeight: 700, color: '#0369a1' }}>{c.code}</td>
                              <td style={{ padding: '8px 10px', color: c.isVerified ? '#15803d' : '#334155' }}>
                                {c.description}
                                {c.comments && <div style={{ fontSize: '11px', color: '#64748b' }}>Note: {c.comments}</div>}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>

                  {/* Right: Significant Risk & Opinion Evaluations */}
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
                    <div style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '6px', padding: '12px' }}>
                      <div style={{ fontSize: '12px', fontWeight: 600, color: '#334155' }}>SA 320 Materiality Concurrence</div>
                      <div style={{ fontSize: '12px', color: '#64748b', marginTop: '4px' }}>
                        Benchmark verified at {fmtInr(selectedEng.benchmarkAmount)} ({selectedEng.benchmarkType}). Overall materiality evaluated as appropriate.
                      </div>
                    </div>
                    <div style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '6px', padding: '12px' }}>
                      <div style={{ fontSize: '12px', fontWeight: 600, color: '#334155' }}>SA 570 Going Concern Evaluation</div>
                      <div style={{ fontSize: '12px', color: '#64748b', marginTop: '4px' }}>
                        Management assessment for 12 months reviewed. No material uncertainty identified.
                      </div>
                    </div>
                    <div style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '6px', padding: '12px' }}>
                      <div style={{ fontSize: '12px', fontWeight: 600, color: '#334155' }}>SA 700 Audit Opinion Appropriateness</div>
                      <div style={{ fontSize: '12px', color: '#64748b', marginTop: '4px' }}>
                        Draft opinion: <strong>{selectedEng.auditOpinion?.toUpperCase() || 'UNMODIFIED'}</strong>. Concurred with audit team conclusions.
                      </div>
                    </div>
                    {eqrReview.eqrConclusionRemarks && (
                      <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '6px', padding: '12px' }}>
                        <div style={{ fontSize: '12px', fontWeight: 700, color: '#166534' }}>EQR Sign-Off Remarks</div>
                        <div style={{ fontSize: '12px', color: '#15803d', marginTop: '4px' }}>{eqrReview.eqrConclusionRemarks}</div>
                        <div style={{ fontSize: '10px', color: '#166534', marginTop: '4px' }}>Signed: {eqrReview.eqrSignoffDate}</div>
                      </div>
                    )}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* TAB 3: Review Notes & Queries */}
          {activeTab === 'review_notes' && (
            <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <h3 style={{ fontSize: '16px', fontWeight: 600, margin: 0 }}>Audit Review Notes &amp; Query Resolution</h3>
                {!selectedEng.isArchived && (
                  <button
                    onClick={() => {
                      setNoteForm({ workingPaperId: '', raisedByRole: 'manager', category: 'documentation_gap', priority: 'medium', query: '' })
                      setShowNewNoteModal(true)
                    }}
                    style={{ padding: '6px 12px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, fontSize: '12px', cursor: 'pointer' }}
                  >
                    + Raise Audit Query
                  </button>
                )}
              </div>

              {reviewNotes.length === 0 ? (
                <div style={{ padding: '32px', textAlign: 'center', color: '#64748b' }}>No review notes or queries logged.</div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                  {reviewNotes.map(n => (
                    <div
                      key={n.id}
                      style={{
                        border: '1px solid #e2e8f0',
                        borderRadius: '6px',
                        padding: '14px',
                        background: n.status === 'cleared' ? '#f8fafc' : '#fff',
                        borderLeft: `4px solid ${
                          n.status === 'cleared' ? '#16a34a' :
                          n.priority === 'critical' ? '#dc2626' :
                          n.priority === 'high' ? '#ea580c' : '#2563eb'
                        }`
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                        <div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <span style={{
                              padding: '1px 6px',
                              borderRadius: '4px',
                              fontSize: '10px',
                              fontWeight: 700,
                              textTransform: 'uppercase',
                              background: n.priority === 'critical' ? '#fee2e2' : n.priority === 'high' ? '#ffedd5' : '#e0f2fe',
                              color: n.priority === 'critical' ? '#dc2626' : n.priority === 'high' ? '#ea580c' : '#0369a1'
                            }}>
                              {n.priority}
                            </span>
                            <span style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>{n.category.toUpperCase()}</span>
                            {n.workingPaperRef && (
                              <span style={{ fontSize: '11px', color: '#0369a1', fontWeight: 600 }}>• {n.workingPaperRef}</span>
                            )}
                          </div>
                          <div style={{ fontSize: '13px', fontWeight: 600, color: '#0f172a', marginTop: '6px' }}>{n.query}</div>
                          <div style={{ fontSize: '11px', color: '#94a3b8', marginTop: '2px' }}>
                            Raised by {n.raisedByName || 'Auditor'} ({n.raisedByRole.toUpperCase()}) on {n.createdAt}
                          </div>
                        </div>

                        {/* Note Actions */}
                        {!selectedEng.isArchived && (
                          <div style={{ display: 'flex', gap: '6px' }}>
                            {n.status !== 'cleared' && (
                              <button
                                onClick={() => {
                                  setRespondingNote(n)
                                  setNoteResponseText(n.response || '')
                                }}
                                style={{ padding: '4px 8px', fontSize: '11px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}
                              >
                                {n.response ? 'Edit Response' : 'Provide Response'}
                              </button>
                            )}
                            {n.status !== 'cleared' && (
                              <button
                                onClick={() => handleClearReviewNote(n.id)}
                                style={{ padding: '4px 8px', fontSize: '11px', background: '#dcfce7', color: '#15803d', border: '1px solid #bbf7d0', borderRadius: '4px', cursor: 'pointer', fontWeight: 600 }}
                              >
                                Clear Query ✓
                              </button>
                            )}
                            {n.status === 'cleared' && (
                              <button
                                onClick={() => handleReopenReviewNote(n.id)}
                                style={{ padding: '4px 8px', fontSize: '11px', background: '#fff', border: '1px solid #cbd5e1', color: '#64748b', borderRadius: '4px', cursor: 'pointer' }}
                              >
                                Reopen Query ↺
                              </button>
                            )}
                          </div>
                        )}
                      </div>

                      {/* Response section */}
                      {n.response && (
                        <div style={{ marginTop: '10px', padding: '10px', background: '#f0fdf4', borderRadius: '4px', border: '1px solid #bbf7d0', fontSize: '12px' }}>
                          <span style={{ fontWeight: 600, color: '#166534' }}>Response ({n.responseByName || 'Preparer'}): </span>
                          <span style={{ color: '#15803d' }}>{n.response}</span>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {/* Modal: New Audit Engagement */}
      {showNewEngModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '520px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Create Statutory Audit Engagement</h3>
            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Client *</label>
              <select
                value={engForm.clientId}
                onChange={e => setEngForm({ ...engForm, clientId: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Financial Year</label>
                <input
                  type="text"
                  value={engForm.financialYear}
                  onChange={e => setEngForm({ ...engForm, financialYear: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Type</label>
                <select
                  value={engForm.auditType}
                  onChange={e => setEngForm({ ...engForm, auditType: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="statutory">Statutory Audit (Companies Act)</option>
                  <option value="tax_audit_44ab">Tax Audit u/s 44AB</option>
                  <option value="internal">Internal Audit</option>
                  <option value="limited_review">Limited Review (SEBI LODR)</option>
                </select>
              </div>
            </div>
            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Engagement Title *</label>
              <input
                type="text"
                value={engForm.title}
                onChange={e => setEngForm({ ...engForm, title: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Benchmark Type</label>
                <select
                  value={engForm.benchmarkType}
                  onChange={e => setEngForm({ ...engForm, benchmarkType: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="revenue">Gross Revenue / Turnover (1%)</option>
                  <option value="profit_before_tax">Profit Before Tax (5%)</option>
                  <option value="total_assets">Total Assets (1%)</option>
                  <option value="equity">Net Worth / Equity (2%)</option>
                </select>
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Benchmark Amount (₹)</label>
                <input
                  type="number"
                  value={engForm.benchmarkAmount}
                  onChange={e => setEngForm({ ...engForm, benchmarkAmount: parseFloat(e.target.value) || 0 })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>
            <div style={{ marginBottom: '16px', display: 'flex', alignItems: 'center', gap: '8px' }}>
              <input
                type="checkbox"
                id="eqrCheck"
                checked={engForm.eqrRequired}
                onChange={e => setEngForm({ ...engForm, eqrRequired: e.target.checked })}
              />
              <label htmlFor="eqrCheck" style={{ fontSize: '13px', fontWeight: 500 }}>
                Engagement Quality Review (EQR) Mandatory under ICAI SA 220
              </label>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setShowNewEngModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleCreateEngagement} style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Create Engagement</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Add Working Paper */}
      {showNewWpModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '560px', padding: '24px', maxHeight: '90vh', overflowY: 'auto' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Add Audit Working Paper</h3>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Ref Code *</label>
                <input
                  type="text"
                  value={wpForm.refCode}
                  onChange={e => setWpForm({ ...wpForm, refCode: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Area *</label>
                <input
                  type="text"
                  value={wpForm.area}
                  onChange={e => setWpForm({ ...wpForm, area: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>
            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Title *</label>
              <input
                type="text"
                value={wpForm.title}
                onChange={e => setWpForm({ ...wpForm, title: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Objective</label>
              <textarea
                value={wpForm.objective}
                onChange={e => setWpForm({ ...wpForm, objective: e.target.value })}
                rows={2}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Sample Tested (₹)</label>
                <input
                  type="number"
                  value={wpForm.sampleValue}
                  onChange={e => setWpForm({ ...wpForm, sampleValue: parseFloat(e.target.value) || 0 })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Total Population (₹)</label>
                <input
                  type="number"
                  value={wpForm.populationValue}
                  onChange={e => setWpForm({ ...wpForm, populationValue: parseFloat(e.target.value) || 0 })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setShowNewWpModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleCreateWorkingPaper} style={{ padding: '8px 16px', background: '#16a34a', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Save Workpaper</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Review Working Paper */}
      {reviewingWp && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '480px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Review Working Paper: {reviewingWp.refCode}</h3>
            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Review Status</label>
              <select
                value={reviewForm.status}
                onChange={e => setReviewForm({ ...reviewForm, status: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                <option value="draft">Draft</option>
                <option value="ready_for_review">Ready for Review</option>
                <option value="reviewed">Reviewed by Senior / Manager</option>
                <option value="partner_approved">Partner Approved</option>
              </select>
            </div>
            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Review Comments</label>
              <textarea
                value={reviewForm.reviewNotes}
                onChange={e => setReviewForm({ ...reviewForm, reviewNotes: e.target.value })}
                rows={3}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setReviewingWp(null)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleReviewWorkingPaper} style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Submit Review</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Sign Off Audit Report (SA 700) */}
      {showSignoffModal && selectedEng && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '500px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 12px 0', fontSize: '18px', color: '#0f172a' }}>Sign Off Audit Report (SA 700 / SA 220)</h3>
            <p style={{ fontSize: '12px', color: '#64748b', margin: '0 0 16px 0' }}>
              This issues the auditor's report and triggers the mandatory statutory 60-day archival countdown under ICAI SQC 1 (Para 74).
            </p>

            {/* Gate check warning */}
            {!selectedEng.canSignOff && (
              <div style={{ padding: '10px 14px', background: '#fef2f2', border: '1px solid #fecaca', borderRadius: '6px', color: '#b91c1c', fontSize: '12px', marginBottom: '14px' }}>
                ⚠️ <strong>Gate-Check Blocking Sign-Off:</strong> {selectedEng.signOffValidationMessage}
              </div>
            )}

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Report Date *</label>
              <input
                type="date"
                value={signoffForm.auditReportDate}
                onChange={e => setSignoffForm({ ...signoffForm, auditReportDate: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Opinion (SA 700 / SA 705)</label>
              <select
                value={signoffForm.auditOpinion}
                onChange={e => setSignoffForm({ ...signoffForm, auditOpinion: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                <option value="unmodified">Unmodified (Clean Opinion)</option>
                <option value="qualified">Qualified Opinion (Except for...)</option>
                <option value="adverse">Adverse Opinion</option>
                <option value="disclaimer">Disclaimer of Opinion</option>
              </select>
            </div>
            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Signing Notes</label>
              <textarea
                value={signoffForm.notes}
                onChange={e => setSignoffForm({ ...signoffForm, notes: e.target.value })}
                rows={2}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setShowSignoffModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button
                onClick={handleSignOffAudit}
                disabled={!selectedEng.canSignOff}
                style={{
                  padding: '8px 16px',
                  background: selectedEng.canSignOff ? '#16a34a' : '#94a3b8',
                  color: '#fff',
                  border: 'none',
                  borderRadius: '4px',
                  fontWeight: 600,
                  cursor: selectedEng.canSignOff ? 'pointer' : 'not-allowed'
                }}
              >
                Confirm Sign-Off
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: New Review Note / Query */}
      {showNewNoteModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '480px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 16px 0', fontSize: '18px' }}>Raise Audit Review Query</h3>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Priority</label>
                <select
                  value={noteForm.priority}
                  onChange={e => setNoteForm({ ...noteForm, priority: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="low">Low</option>
                  <option value="medium">Medium</option>
                  <option value="high">High (Audit Gate)</option>
                  <option value="critical">Critical (Blocking)</option>
                </select>
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Category</label>
                <select
                  value={noteForm.category}
                  onChange={e => setNoteForm({ ...noteForm, category: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="documentation_gap">Documentation Gap</option>
                  <option value="insufficient_testing">Insufficient Testing</option>
                  <option value="assertion_unverified">Assertion Unverified</option>
                  <option value="accounting_policy_mismatch">Accounting Policy Mismatch</option>
                  <option value="caro_clause_omission">CARO 2020 Clause Omission</option>
                  <option value="other">Other</option>
                </select>
              </div>
            </div>
            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Link to Working Paper (Optional)</label>
              <select
                value={noteForm.workingPaperId}
                onChange={e => setNoteForm({ ...noteForm, workingPaperId: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                <option value="">General Engagement Query</option>
                {workingPapers.map(w => <option key={w.id} value={w.id}>{w.refCode} - {w.title}</option>)}
              </select>
            </div>
            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Query Description *</label>
              <textarea
                value={noteForm.query}
                onChange={e => setNoteForm({ ...noteForm, query: e.target.value })}
                rows={3}
                placeholder="State the audit finding, documentation requirement, or missing evidence..."
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setShowNewNoteModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleCreateReviewNote} style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Raise Query</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Respond to Review Note */}
      {respondingNote && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '480px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 12px 0', fontSize: '18px' }}>Respond to Audit Query</h3>
            <div style={{ padding: '10px', background: '#f8fafc', borderRadius: '6px', border: '1px solid #e2e8f0', marginBottom: '14px', fontSize: '12px' }}>
              <strong>Query:</strong> {respondingNote.query}
            </div>
            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Response / Corrective Action *</label>
              <textarea
                value={noteResponseText}
                onChange={e => setNoteResponseText(e.target.value)}
                rows={4}
                placeholder="Explain testing performed, attach evidence reference, or explain accounting treatment..."
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setRespondingNote(null)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleRespondReviewNote} style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Submit Response</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
