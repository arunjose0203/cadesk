import React, { useState, useEffect } from 'react'
import { TaxCopilotDesk } from './TaxCopilotDesk'
import { toError,
  taxNoticesApi,
  api,
  type TaxNoticeDto,
  type NoticeHearingDto,
  type NoticeResponseDto,
  type TaxDemandDto,
  type NoticeDashboardSummaryDto,
  type TaxAuthority,
  type NoticeStatus,
  type CreateTaxNoticeRequest,
  type CreateHearingRequest,
  type DraftResponseRequest,
  type RecordSubmissionRequest,
  type RecordDemandPaymentRequest,
  type UpdateAppealRequest,
  type Client,
  type ClientPage
} from './api'

interface TaxNoticesPageProps {
  can?: (p: string) => boolean
}

export const TaxNoticesPage: React.FC<TaxNoticesPageProps> = ({ can }) => {
  const [activeTab, setActiveTab] = useState<'notices' | 'copilot' | 'hearings' | 'responses' | 'demands'>('notices')
  const [copilotNoticeId, setCopilotNoticeId] = useState<string | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Data
  const [summary, setSummary] = useState<NoticeDashboardSummaryDto | null>(null)
  const [notices, setNotices] = useState<TaxNoticeDto[]>([])
  const [hearings, setHearings] = useState<NoticeHearingDto[]>([])
  const [demands, setDemands] = useState<TaxDemandDto[]>([])
  const [clients, setClients] = useState<Client[]>([])

  // Filters
  const [filterAuthority, setFilterAuthority] = useState<TaxAuthority | ''>('')
  const [filterStatus, setFilterStatus] = useState<NoticeStatus | ''>('')
  const [noticeResponses, setNoticeResponses] = useState<NoticeResponseDto[]>([])

  // Modals
  const [showNoticeModal, setShowNoticeModal] = useState<boolean>(false)
  const [showHearingModal, setShowHearingModal] = useState<boolean>(false)
  const [showDraftModal, setShowDraftModal] = useState<boolean>(false)
  const [showSubmitModal, setShowSubmitModal] = useState<boolean>(false)
  const [showPaymentModal, setShowPaymentModal] = useState<boolean>(false)
  const [showAppealModal, setShowAppealModal] = useState<boolean>(false)

  // Selected item for modals
  const [activeNotice, setActiveNotice] = useState<TaxNoticeDto | null>(null)
  const [activeResponseId, setActiveResponseId] = useState<string | null>(null)
  const [activeDemand, setActiveDemand] = useState<TaxDemandDto | null>(null)

  // Forms
  const [noticeForm, setNoticeForm] = useState<CreateTaxNoticeRequest>({
    clientId: '',
    authority: 'IncomeTax',
    category: 'Inquiry142_1',
    section: 'Sec 142(1)',
    noticeNumber: '',
    din: '',
    assessmentYear: '2024-25',
    financialYear: '2023-24',
    issueDate: new Date().toISOString().split('T')[0],
    dueDate: new Date(Date.now() + 15 * 86400000).toISOString().split('T')[0],
    demandAmount: 0,
    refundDisallowed: 0,
    subject: '',
    description: '',
    officerName: '',
    officerJurisdiction: 'NFAC Delhi'
  })

  const [hearingForm, setHearingForm] = useState<CreateHearingRequest>({
    hearingDate: new Date(Date.now() + 5 * 86400000).toISOString().slice(0, 16),
    hearingType: 'VideoConference',
    officerName: '',
    officerDesignation: 'Assessment Unit Officer (NFAC)',
    meetingLink: '',
    summaryNotes: ''
  })

  const [draftForm, setDraftForm] = useState<DraftResponseRequest>({
    submissionTitle: 'Written Submission / Rebuttal',
    summaryOfSubmissions: '',
    legalGrounds: '',
    attachmentNames: ''
  })

  const [submitForm, setSubmitForm] = useState<RecordSubmissionRequest>({
    portalAckNumber: '',
    submittedAt: new Date().toISOString()
  })

  const [paymentForm, setPaymentForm] = useState<RecordDemandPaymentRequest>({
    amount: 0,
    isRefundAdjustment: false,
    remarks: ''
  })

  const [appealForm, setAppealForm] = useState<UpdateAppealRequest>({
    status: 'FiledCitA',
    appealAckNumber: '',
    stayedAmount: 0,
    remarks: ''
  })

  useEffect(() => {
    loadAll()
  }, [filterAuthority, filterStatus])

  const loadAll = async () => {
    setLoading(true)
    setError(null)
    try {
      const [sum, nList, hList, dList, cRes] = await Promise.all([
        taxNoticesApi.getSummary(),
        taxNoticesApi.getNotices(filterAuthority || undefined, filterStatus || undefined),
        taxNoticesApi.getHearings(),
        taxNoticesApi.getDemands(),
        api<ClientPage>('/api/clients?take=200')
      ])
      setSummary(sum)
      setNotices(nList)
      setHearings(hList)
      setDemands(dList)
      const cList = cRes.items || []
      setClients(cList)

      if (cList.length > 0 && !noticeForm.clientId) {
        setNoticeForm(prev => ({ ...prev, clientId: cList[0].id }))
      }
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to load tax notices')
    } finally {
      setLoading(false)
    }
  }

  const handleSelectNoticeForResponses = async (n: TaxNoticeDto) => {
    setActiveNotice(n)
    setActiveTab('responses')
    try {
      const list = await taxNoticesApi.getResponses(n.id)
      setNoticeResponses(list)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to load responses for notice')
    }
  }

  const handleVerifyDin = async (id: string) => {
    try {
      const updated = await taxNoticesApi.verifyDin(id)
      setNotices(prev => prev.map(n => n.id === id ? updated : n))
      setSuccess(`DIN status updated to: ${updated.dinStatus}`)
      const sum = await taxNoticesApi.getSummary()
      setSummary(sum)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to verify DIN')
    }
  }

  const handleCreateNotice = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      await taxNoticesApi.createNotice(noticeForm)
      setSuccess('Tax notice registered successfully!')
      setShowNoticeModal(false)
      loadAll()
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to register tax notice')
    }
  }

  const handleCreateHearing = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!activeNotice) return
    setError(null)
    setSuccess(null)
    try {
      await taxNoticesApi.addHearing(activeNotice.id, {
        ...hearingForm,
        hearingDate: new Date(hearingForm.hearingDate).toISOString()
      })
      setSuccess('Hearing scheduled successfully!')
      setShowHearingModal(false)
      loadAll()
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to schedule hearing')
    }
  }

  const handleDraftResponse = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!activeNotice) return
    setError(null)
    setSuccess(null)
    try {
      await taxNoticesApi.draftResponse(activeNotice.id, draftForm)
      setSuccess('Written response draft saved!')
      setShowDraftModal(false)
      const list = await taxNoticesApi.getResponses(activeNotice.id)
      setNoticeResponses(list)
      loadAll()
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to draft response')
    }
  }

  const handleApproveResponse = async (responseId: string) => {
    setError(null)
    setSuccess(null)
    try {
      await taxNoticesApi.approveResponse(responseId)
      setSuccess('Response approved by Partner sign-off!')
      if (activeNotice) {
        const list = await taxNoticesApi.getResponses(activeNotice.id)
        setNoticeResponses(list)
      }
      loadAll()
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to approve response')
    }
  }

  const handleSubmitResponse = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!activeResponseId) return
    setError(null)
    setSuccess(null)
    try {
      await taxNoticesApi.submitResponse(activeResponseId, submitForm)
      setSuccess('Response submitted and portal ACK recorded!')
      setShowSubmitModal(false)
      if (activeNotice) {
        const list = await taxNoticesApi.getResponses(activeNotice.id)
        setNoticeResponses(list)
      }
      loadAll()
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to record portal submission')
    }
  }

  const handleRecordPayment = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!activeDemand) return
    setError(null)
    setSuccess(null)
    try {
      await taxNoticesApi.recordDemandPayment(activeDemand.id, paymentForm)
      setSuccess('Demand payment/adjustment recorded!')
      setShowPaymentModal(false)
      loadAll()
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to record payment')
    }
  }

  const handleUpdateAppeal = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!activeDemand) return
    setError(null)
    setSuccess(null)
    try {
      await taxNoticesApi.updateAppeal(activeDemand.id, appealForm)
      setSuccess('Appeal status updated successfully!')
      setShowAppealModal(false)
      loadAll()
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to update appeal status')
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
            Tax Notice & Faceless Assessment Desk
          </h1>
          <p style={{ margin: 0, color: 'var(--muted, #666)', fontSize: '14px' }}>
            CBDT/CBIC DIN Verification, Faceless E-Hearings, Maker-Checker Submissions & 30-Day Appeal Tracker (Sprint 22)
          </p>
        </div>
        <div style={{ display: 'flex', gap: '12px' }}>
          {(!can || can('notices.manage')) && (
            <button className="primary" onClick={() => setShowNoticeModal(true)}>
              + Register Notice
            </button>
          )}
          <button className="ghost small" onClick={loadAll}>
            🔄 Refresh
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

      {/* KPI Summary Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>Active Notices</div>
            <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px' }}>{summary.totalActiveNotices}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>
              IT: {summary.incomeTaxNotices} | GST: {summary.gstNotices}
            </div>
          </div>

          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>VC Hearings</div>
            <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px', color: '#1976d2' }}>{summary.pendingHearings}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>Faceless appointments</div>
          </div>

          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>Partner Approvals</div>
            <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px', color: '#ed6c02' }}>{summary.pendingApprovals}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>Submissions awaiting sign-off</div>
          </div>

          <div style={{ padding: '16px', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted, #666)', textTransform: 'uppercase', fontWeight: 600 }}>Outstanding Demand</div>
            <div style={{ fontSize: '22px', fontWeight: 700, marginTop: '4px', color: '#d32f2f' }}>{formatCurrency(summary.totalOutstandingDemand)}</div>
            <div style={{ fontSize: '12px', color: '#555', marginTop: '4px' }}>Total: {formatCurrency(summary.totalDemandAmount)}</div>
          </div>

          <div style={{ padding: '16px', background: summary.appealsDueWithin30Days > 0 ? '#fff3e0' : 'var(--card-bg, #fff)', border: '1px solid #ffb74d', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: '#e65100', textTransform: 'uppercase', fontWeight: 600 }}>30-Day Appeal Alerts</div>
            <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px', color: '#e65100' }}>{summary.appealsDueWithin30Days}</div>
            <div style={{ fontSize: '12px', color: '#e65100', marginTop: '4px' }}>CIT(A) Form 35 countdown</div>
          </div>

          <div style={{ padding: '16px', background: summary.invalidDinCount > 0 ? '#ffebee' : 'var(--card-bg, #fff)', border: '1px solid #e57373', borderRadius: '8px' }}>
            <div style={{ fontSize: '12px', color: '#c62828', textTransform: 'uppercase', fontWeight: 600 }}>DIN Defects (Non-Est)</div>
            <div style={{ fontSize: '24px', fontWeight: 700, marginTop: '4px', color: '#c62828' }}>{summary.invalidDinCount}</div>
            <div style={{ fontSize: '12px', color: '#c62828', marginTop: '4px' }}>Missing/Invalid CBDT/CBIC DIN</div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '2px solid var(--border, #e0e0e0)', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('notices')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'notices' ? 700 : 400,
            borderBottom: activeTab === 'notices' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'notices' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Notices & Inquiries ({notices.length})
        </button>
        <button
          onClick={() => setActiveTab('copilot')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'copilot' ? 700 : 400,
            borderBottom: activeTab === 'copilot' ? '3px solid #6366f1' : 'none',
            color: activeTab === 'copilot' ? '#4f46e5' : 'inherit',
            display: 'flex',
            alignItems: 'center',
            gap: '6px'
          }}
        >
          <span>🤖</span> CA Copilot: Notice Defense Drafter
        </button>
        <button
          onClick={() => setActiveTab('hearings')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'hearings' ? 700 : 400,
            borderBottom: activeTab === 'hearings' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'hearings' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Faceless Hearings & VC ({hearings.length})
        </button>
        <button
          onClick={() => setActiveTab('responses')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'responses' ? 700 : 400,
            borderBottom: activeTab === 'responses' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'responses' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Written Submissions & Approvals {activeNotice ? `(${activeNotice.section} - ${activeNotice.clientName})` : ''}
        </button>
        <button
          onClick={() => setActiveTab('demands')}
          style={{
            padding: '10px 18px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            fontWeight: activeTab === 'demands' ? 700 : 400,
            borderBottom: activeTab === 'demands' ? '3px solid var(--primary, #1976d2)' : 'none',
            color: activeTab === 'demands' ? 'var(--primary, #1976d2)' : 'inherit'
          }}
        >
          Demands & CIT(A) Appeals ({demands.length})
        </button>
      </div>

      {/* Tab 1: Notices & Inquiries */}
      {activeTab === 'notices' && (
        <div>
          {/* Filter Bar */}
          <div style={{ display: 'flex', gap: '12px', alignItems: 'center', marginBottom: '16px' }}>
            <span style={{ fontSize: '13px', fontWeight: 600 }}>Authority:</span>
            <select
              value={filterAuthority}
              onChange={e => setFilterAuthority(e.target.value as TaxAuthority | "")}
              style={{ padding: '6px 12px', borderRadius: '4px', border: '1px solid var(--border, #ccc)' }}
            >
              <option value="">All Authorities</option>
              <option value="IncomeTax">Income Tax (ITBA)</option>
              <option value="Gst">GST (DRC/ASMT)</option>
              <option value="Customs">Customs</option>
            </select>

            <span style={{ fontSize: '13px', fontWeight: 600, marginLeft: '12px' }}>Status:</span>
            <select
              value={filterStatus}
              onChange={e => setFilterStatus(e.target.value as NoticeStatus | "")}
              style={{ padding: '6px 12px', borderRadius: '4px', border: '1px solid var(--border, #ccc)' }}
            >
              <option value="">All Statuses</option>
              <option value="Received">Received</option>
              <option value="InReview">In Review</option>
              <option value="DraftPrepared">Draft Prepared</option>
              <option value="PartnerApproved">Partner Approved</option>
              <option value="Submitted">Submitted to Portal</option>
              <option value="DisposedOrClosed">Disposed / Closed</option>
            </select>
          </div>

          <div style={{ overflowX: 'auto', background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: 'var(--table-header, #f9f9f9)', borderBottom: '1px solid var(--border, #e0e0e0)' }}>
                  <th style={{ padding: '12px' }}>Client</th>
                  <th style={{ padding: '12px' }}>Authority / Section</th>
                  <th style={{ padding: '12px' }}>Notice # & DIN</th>
                  <th style={{ padding: '12px' }}>AY / FY</th>
                  <th style={{ padding: '12px' }}>Due Date & Countdown</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Demand (₹)</th>
                  <th style={{ padding: '12px' }}>Status</th>
                  <th style={{ padding: '12px', textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {notices.map(n => {
                  const isOverdue = n.daysRemaining < 0
                  const isUrgent = n.daysRemaining >= 0 && n.daysRemaining <= 7
                  return (
                    <tr key={n.id} style={{ borderBottom: '1px solid var(--border, #f0f0f0)' }}>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 600 }}>{n.clientName}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted, #666)' }}>{n.clientCode}</div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{
                          padding: '3px 8px',
                          borderRadius: '4px',
                          fontSize: '11px',
                          fontWeight: 600,
                          background: n.authority === 'IncomeTax' ? '#e3f2fd' : '#f3e5f5',
                          color: n.authority === 'IncomeTax' ? '#1565c0' : '#7b1fa2'
                        }}>
                          {n.authority}
                        </span>
                        <div style={{ fontWeight: 600, marginTop: '4px' }}>{n.section}</div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontFamily: 'monospace', fontSize: '12px' }}>{n.noticeNumber}</div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px', marginTop: '4px' }}>
                          <span style={{
                            padding: '2px 6px',
                            borderRadius: '3px',
                            fontSize: '10px',
                            fontWeight: 700,
                            background: n.dinStatus === 'Valid' ? '#e8f5e9' : (n.dinStatus === 'Invalid' ? '#ffebee' : '#f5f5f5'),
                            color: n.dinStatus === 'Valid' ? '#2e7d32' : (n.dinStatus === 'Invalid' ? '#c62828' : '#616161')
                          }}>
                            DIN: {n.dinStatus}
                          </span>
                          {n.din && <span style={{ fontSize: '11px', color: '#666', fontFamily: 'monospace' }}>{n.din.slice(0, 15)}…</span>}
                        </div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div>AY {n.assessmentYear || '-'}</div>
                        <div style={{ fontSize: '11px', color: '#666' }}>FY {n.financialYear}</div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div>{n.dueDate}</div>
                        <div style={{
                          fontSize: '11px',
                          fontWeight: 600,
                          color: isOverdue ? '#d32f2f' : (isUrgent ? '#ed6c02' : '#2e7d32')
                        }}>
                          {isOverdue ? `⚠️ Overdue by ${Math.abs(n.daysRemaining)} days` : `${n.daysRemaining} days left`}
                        </div>
                      </td>
                      <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: n.demandAmount > 0 ? '#c62828' : 'inherit' }}>
                        {formatCurrency(n.demandAmount)}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{
                          padding: '3px 8px',
                          borderRadius: '12px',
                          fontSize: '11px',
                          fontWeight: 600,
                          background: n.status === 'Submitted' ? '#e8f5e9' : (n.status === 'PartnerApproved' ? '#fff3e0' : '#e0e0e0'),
                          color: n.status === 'Submitted' ? '#2e7d32' : (n.status === 'PartnerApproved' ? '#e65100' : '#333')
                        }}>
                          {n.status}
                        </span>
                      </td>
                      <td style={{ padding: '12px', textAlign: 'center' }}>
                        <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                          <button
                            className="primary small"
                            style={{ background: '#4f46e5', borderColor: '#4f46e5', color: '#fff', fontWeight: 600 }}
                            onClick={() => {
                              setCopilotNoticeId(n.id)
                              setActiveTab('copilot')
                            }}
                            title="Open CA Copilot Notice Defense Drafter"
                          >
                            🤖 Copilot
                          </button>
                          <button
                            className="secondary small"
                            onClick={() => handleSelectNoticeForResponses(n)}
                            title="Submissions & Rebuttal"
                          >
                            📝 Drafts ({n.responseCount})
                          </button>
                          <button
                            className="ghost small"
                            onClick={() => {
                              setActiveNotice(n)
                              setShowHearingModal(true)
                            }}
                            title="Schedule VC Hearing"
                          >
                            📹 VC
                          </button>
                          <button
                            className="ghost small"
                            onClick={() => handleVerifyDin(n.id)}
                            title="Re-verify DIN"
                          >
                            🔍 DIN
                          </button>
                        </div>
                      </td>
                    </tr>
                  )
                })}
                {notices.length === 0 && (
                  <tr>
                    <td colSpan={8} style={{ padding: '24px', textAlign: 'center', color: '#777' }}>
                      No tax notices registered. Click "+ Register Notice" to add one.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab: CA Copilot Notice Defense Drafter */}
      {activeTab === 'copilot' && (
        <TaxCopilotDesk
          can={can}
          initialNoticeId={copilotNoticeId}
          onOpenNoticeLedger={() => setActiveTab('notices')}
        />
      )}

      {/* Tab 2: Faceless Hearings & VC Schedule */}
      {activeTab === 'hearings' && (
        <div style={{ background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px', overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
            <thead>
              <tr style={{ background: 'var(--table-header, #f9f9f9)', borderBottom: '1px solid var(--border, #e0e0e0)' }}>
                <th style={{ padding: '12px' }}>Hearing Date & Time</th>
                <th style={{ padding: '12px' }}>Client & Section</th>
                <th style={{ padding: '12px' }}>Type</th>
                <th style={{ padding: '12px' }}>Officer & Jurisdiction</th>
                <th style={{ padding: '12px' }}>Meeting Link / Venue</th>
                <th style={{ padding: '12px' }}>Status</th>
                <th style={{ padding: '12px' }}>Notes / Adjournment</th>
              </tr>
            </thead>
            <tbody>
              {hearings.map(h => (
                <tr key={h.id} style={{ borderBottom: '1px solid var(--border, #f0f0f0)' }}>
                  <td style={{ padding: '12px', fontWeight: 600 }}>
                    {new Date(h.hearingDate).toLocaleString('en-IN', { dateStyle: 'medium', timeStyle: 'short' })}
                  </td>
                  <td style={{ padding: '12px' }}>
                    <div style={{ fontWeight: 600 }}>{h.clientName}</div>
                    <div style={{ fontSize: '11px', color: '#666' }}>{h.noticeSection} ({h.noticeNumber})</div>
                  </td>
                  <td style={{ padding: '12px' }}>
                    <span style={{
                      padding: '3px 8px',
                      borderRadius: '4px',
                      fontSize: '11px',
                      fontWeight: 600,
                      background: h.hearingType === 'VideoConference' ? '#e8eaf6' : '#ede7f6',
                      color: h.hearingType === 'VideoConference' ? '#283593' : '#4527a0'
                    }}>
                      {h.hearingType}
                    </span>
                  </td>
                  <td style={{ padding: '12px' }}>
                    <div>{h.officerName || 'Faceless Unit'}</div>
                    <div style={{ fontSize: '11px', color: '#666' }}>{h.officerDesignation || '-'}</div>
                  </td>
                  <td style={{ padding: '12px' }}>
                    {h.meetingLink ? (
                      <a href={h.meetingLink} target="_blank" rel="noopener noreferrer" style={{ color: '#1976d2', textDecoration: 'underline' }}>
                        🔗 Open VC Room
                      </a>
                    ) : (
                      <span style={{ color: '#999' }}>None</span>
                    )}
                  </td>
                  <td style={{ padding: '12px' }}>
                    <span style={{
                      padding: '3px 8px',
                      borderRadius: '12px',
                      fontSize: '11px',
                      fontWeight: 600,
                      background: h.status === 'Attended' ? '#e8f5e9' : (h.status === 'Scheduled' ? '#e3f2fd' : '#ffebee'),
                      color: h.status === 'Attended' ? '#2e7d32' : (h.status === 'Scheduled' ? '#1565c0' : '#c62828')
                    }}>
                      {h.status}
                    </span>
                  </td>
                  <td style={{ padding: '12px', fontSize: '12px', color: '#555' }}>
                    {h.adjournmentReason ? `⚠️ Adjourned: ${h.adjournmentReason}` : (h.summaryNotes || '-')}
                  </td>
                </tr>
              ))}
              {hearings.length === 0 && (
                <tr>
                  <td colSpan={7} style={{ padding: '24px', textAlign: 'center', color: '#777' }}>
                    No faceless hearings scheduled. Click "VC" on any notice to schedule one.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Tab 3: Written Submissions & Maker-Checker */}
      {activeTab === 'responses' && (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <div>
              <h3 style={{ margin: 0 }}>
                {activeNotice ? `Submissions for ${activeNotice.section} - ${activeNotice.clientName}` : 'Select a notice from the Notices tab to view or draft submissions'}
              </h3>
            </div>
            {activeNotice && (!can || can('notices.manage')) && (
              <button className="primary small" onClick={() => setShowDraftModal(true)}>
                + Draft Written Response
              </button>
            )}
          </div>

          <div style={{ background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px', overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: 'var(--table-header, #f9f9f9)', borderBottom: '1px solid var(--border, #e0e0e0)' }}>
                  <th style={{ padding: '12px' }}>Ver #</th>
                  <th style={{ padding: '12px' }}>Title & Legal Grounds</th>
                  <th style={{ padding: '12px' }}>Prepared By</th>
                  <th style={{ padding: '12px' }}>Partner Sign-off</th>
                  <th style={{ padding: '12px' }}>Portal ACK</th>
                  <th style={{ padding: '12px' }}>Status</th>
                  <th style={{ padding: '12px', textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {noticeResponses.map(r => (
                  <tr key={r.id} style={{ borderBottom: '1px solid var(--border, #f0f0f0)' }}>
                    <td style={{ padding: '12px', fontWeight: 700 }}>v{r.responseVersion}</td>
                    <td style={{ padding: '12px', maxWidth: '350px' }}>
                      <div style={{ fontWeight: 600 }}>{r.submissionTitle}</div>
                      <div style={{ fontSize: '12px', color: '#555', marginTop: '2px' }}>{r.summaryOfSubmissions}</div>
                      {r.legalGrounds && (
                        <div style={{ fontSize: '11px', color: '#1565c0', marginTop: '4px', fontStyle: 'italic' }}>
                          Grounds: {r.legalGrounds}
                        </div>
                      )}
                    </td>
                    <td style={{ padding: '12px' }}>
                      <div>{r.preparedByName || 'Staff'}</div>
                      <div style={{ fontSize: '11px', color: '#666' }}>{new Date(r.createdAt).toLocaleDateString()}</div>
                    </td>
                    <td style={{ padding: '12px' }}>
                      {r.approvedByName ? (
                        <div>
                          <div style={{ fontWeight: 600, color: '#2e7d32' }}>✓ {r.approvedByName}</div>
                          <div style={{ fontSize: '11px', color: '#666' }}>
                            {r.approvedAt ? new Date(r.approvedAt).toLocaleDateString() : ''}
                          </div>
                        </div>
                      ) : (
                        <span style={{ color: '#ed6c02', fontSize: '12px' }}>⏳ Awaiting Sign-off</span>
                      )}
                    </td>
                    <td style={{ padding: '12px' }}>
                      {r.portalAckNumber ? (
                        <div style={{ fontFamily: 'monospace', fontSize: '12px', color: '#1976d2' }}>
                          {r.portalAckNumber}
                        </div>
                      ) : (
                        <span style={{ color: '#999' }}>Not uploaded</span>
                      )}
                    </td>
                    <td style={{ padding: '12px' }}>
                      <span style={{
                        padding: '3px 8px',
                        borderRadius: '12px',
                        fontSize: '11px',
                        fontWeight: 600,
                        background: r.status === 'UploadedToPortal' ? '#e8f5e9' : (r.status === 'PartnerApproved' ? '#e3f2fd' : '#fff3e0'),
                        color: r.status === 'UploadedToPortal' ? '#2e7d32' : (r.status === 'PartnerApproved' ? '#1565c0' : '#e65100')
                      }}>
                        {r.status}
                      </span>
                    </td>
                    <td style={{ padding: '12px', textAlign: 'center' }}>
                      <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                        {r.status === 'Draft' && (!can || can('notices.approve')) && (
                          <button
                            className="primary small"
                            onClick={() => handleApproveResponse(r.id)}
                            title="Partner Sign-off"
                          >
                            ✓ Approve
                          </button>
                        )}
                        {r.status === 'PartnerApproved' && (!can || can('notices.manage')) && (
                          <button
                            className="secondary small"
                            onClick={() => {
                              setActiveResponseId(r.id)
                              setShowSubmitModal(true)
                            }}
                            title="Record E-filing Portal ACK"
                          >
                            📤 Record ACK
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
                {noticeResponses.length === 0 && (
                  <tr>
                    <td colSpan={7} style={{ padding: '24px', textAlign: 'center', color: '#777' }}>
                      {activeNotice ? 'No written submissions prepared yet. Click "+ Draft Written Response".' : 'Select a notice from the Notices tab first.'}
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 4: Demands & CIT(A) Appeals */}
      {activeTab === 'demands' && (
        <div style={{ background: 'var(--card-bg, #fff)', border: '1px solid var(--border, #e0e0e0)', borderRadius: '8px', overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
            <thead>
              <tr style={{ background: 'var(--table-header, #f9f9f9)', borderBottom: '1px solid var(--border, #e0e0e0)' }}>
                <th style={{ padding: '12px' }}>Client</th>
                <th style={{ padding: '12px' }}>Section / AY</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Original Demand</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Stayed (₹)</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Paid / Sec 245 Adj</th>
                <th style={{ padding: '12px', textAlign: 'right' }}>Outstanding</th>
                <th style={{ padding: '12px' }}>30-Day Appeal Due</th>
                <th style={{ padding: '12px' }}>Appeal Status</th>
                <th style={{ padding: '12px', textAlign: 'center' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {demands.map(d => {
                const isOverdue = d.daysUntilAppealDue < 0
                const isUrgent = d.daysUntilAppealDue >= 0 && d.daysUntilAppealDue <= 15
                return (
                  <tr key={d.id} style={{ borderBottom: '1px solid var(--border, #f0f0f0)' }}>
                    <td style={{ padding: '12px' }}>
                      <div style={{ fontWeight: 600 }}>{d.clientName}</div>
                      <div style={{ fontSize: '11px', color: '#666' }}>{d.clientCode}</div>
                    </td>
                    <td style={{ padding: '12px' }}>
                      <div style={{ fontWeight: 600 }}>{d.section}</div>
                      <div style={{ fontSize: '11px', color: '#666' }}>AY {d.assessmentYear || '-'} | FY {d.financialYear}</div>
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600 }}>
                      {formatCurrency(d.originalDemand)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', color: '#ed6c02' }}>
                      {formatCurrency(d.stayedAmount)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', color: '#2e7d32' }}>
                      {formatCurrency(d.paidAmount + d.adjustedRefund)}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: d.outstandingAmount > 0 ? '#c62828' : '#2e7d32' }}>
                      {formatCurrency(d.outstandingAmount)}
                    </td>
                    <td style={{ padding: '12px' }}>
                      <div>{d.appealDueDate}</div>
                      <div style={{
                        fontSize: '11px',
                        fontWeight: 700,
                        color: isOverdue ? '#d32f2f' : (isUrgent ? '#ed6c02' : '#2e7d32')
                      }}>
                        {isOverdue ? `⚠️ EXPIRED (${Math.abs(d.daysUntilAppealDue)}d ago)` : `${d.daysUntilAppealDue} days left`}
                      </div>
                    </td>
                    <td style={{ padding: '12px' }}>
                      <span style={{
                        padding: '3px 8px',
                        borderRadius: '12px',
                        fontSize: '11px',
                        fontWeight: 600,
                        background: d.appealStatus === 'Settled' ? '#e8f5e9' : (d.appealStatus === 'FiledCitA' ? '#e3f2fd' : '#fff3e0'),
                        color: d.appealStatus === 'Settled' ? '#2e7d32' : (d.appealStatus === 'FiledCitA' ? '#1565c0' : '#e65100')
                      }}>
                        {d.appealStatus}
                      </span>
                      {d.appealAckNumber && (
                        <div style={{ fontSize: '10px', fontFamily: 'monospace', color: '#555', marginTop: '2px' }}>
                          ACK: {d.appealAckNumber}
                        </div>
                      )}
                    </td>
                    <td style={{ padding: '12px', textAlign: 'center' }}>
                      <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                        <button
                          className="secondary small"
                          onClick={() => {
                            setActiveDemand(d)
                            setPaymentForm({ amount: 0, isRefundAdjustment: false, remarks: '' })
                            setShowPaymentModal(true)
                          }}
                          title="Record Payment or Sec 245 Refund Adj"
                        >
                          💳 Pay / Adj
                        </button>
                        <button
                          className="ghost small"
                          onClick={() => {
                            setActiveDemand(d)
                            setAppealForm({
                              status: d.appealStatus,
                              appealAckNumber: d.appealAckNumber || '',
                              stayedAmount: d.stayedAmount,
                              remarks: d.remarks || ''
                            })
                            setShowAppealModal(true)
                          }}
                          title="Update Appeal / Stay"
                        >
                          ⚖️ Appeal
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
              {demands.length === 0 && (
                <tr>
                  <td colSpan={9} style={{ padding: '24px', textAlign: 'center', color: '#777' }}>
                    No statutory tax demands registered.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Modal 1: Register Tax Notice */}
      {showNoticeModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', padding: '24px', borderRadius: '8px', maxWidth: '650px', width: '90%', maxHeight: '90vh', overflowY: 'auto' }}>
            <h2 style={{ marginTop: 0 }}>Register Statutory Tax Notice</h2>
            <form onSubmit={handleCreateNotice}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Client *</label>
                  <select
                    value={noticeForm.clientId}
                    onChange={e => setNoticeForm({ ...noticeForm, clientId: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Authority *</label>
                  <select
                    value={noticeForm.authority}
                    onChange={e => setNoticeForm({ ...noticeForm, authority: e.target.value as (typeof noticeForm)['authority'] })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value="IncomeTax">Income Tax (ITBA / NFAC)</option>
                    <option value="Gst">GST (DRC / ASMT)</option>
                    <option value="Customs">Customs</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Notice Section *</label>
                  <input
                    type="text"
                    value={noticeForm.section}
                    onChange={e => setNoticeForm({ ...noticeForm, section: e.target.value })}
                    placeholder="e.g. Sec 143(1), Sec 142(1), DRC-01"
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Notice Reference # *</label>
                  <input
                    type="text"
                    value={noticeForm.noticeNumber}
                    onChange={e => setNoticeForm({ ...noticeForm, noticeNumber: e.target.value })}
                    placeholder="e.g. ITBA/AST/S/142(1)/2024-25/..."
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>
                    Document Identification # (DIN)
                  </label>
                  <input
                    type="text"
                    value={noticeForm.din || ''}
                    onChange={e => setNoticeForm({ ...noticeForm, din: e.target.value })}
                    placeholder="Mandatory for CBDT/CBIC notices"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                  <span style={{ fontSize: '11px', color: '#666' }}>Post-2019 notices without DIN are non-est</span>
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>AY / FY *</label>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <input
                      type="text"
                      value={noticeForm.assessmentYear || ''}
                      onChange={e => setNoticeForm({ ...noticeForm, assessmentYear: e.target.value })}
                      placeholder="AY (e.g. 2024-25)"
                      style={{ width: '50%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                    <input
                      type="text"
                      value={noticeForm.financialYear}
                      onChange={e => setNoticeForm({ ...noticeForm, financialYear: e.target.value })}
                      placeholder="FY (e.g. 2023-24)"
                      required
                      style={{ width: '50%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                  </div>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Issue Date *</label>
                  <input
                    type="date"
                    value={noticeForm.issueDate}
                    onChange={e => setNoticeForm({ ...noticeForm, issueDate: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Compliance Due Date *</label>
                  <input
                    type="date"
                    value={noticeForm.dueDate}
                    onChange={e => setNoticeForm({ ...noticeForm, dueDate: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Demand Amount (₹)</label>
                  <input
                    type="number"
                    value={noticeForm.demandAmount}
                    onChange={e => setNoticeForm({ ...noticeForm, demandAmount: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Officer / Ward</label>
                  <input
                    type="text"
                    value={noticeForm.officerJurisdiction || ''}
                    onChange={e => setNoticeForm({ ...noticeForm, officerJurisdiction: e.target.value })}
                    placeholder="e.g. NFAC Delhi / Ward 1(1)"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Subject / Issue *</label>
                <input
                  type="text"
                  value={noticeForm.subject}
                  onChange={e => setNoticeForm({ ...noticeForm, subject: e.target.value })}
                  placeholder="e.g. Discrepancy in Chapter VI-A deductions vs AIS"
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="ghost" onClick={() => setShowNoticeModal(false)}>
                  Cancel
                </button>
                <button type="submit" className="primary" disabled={loading}>
                  {loading ? 'Saving…' : 'Register Notice'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 2: Schedule VC Hearing */}
      {showHearingModal && activeNotice && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', padding: '24px', borderRadius: '8px', maxWidth: '500px', width: '90%' }}>
            <h2 style={{ marginTop: 0 }}>Schedule Faceless Hearing / VC</h2>
            <p style={{ color: '#666', fontSize: '13px' }}>
              Notice: <strong>{activeNotice.section}</strong> ({activeNotice.clientName})
            </p>
            <form onSubmit={handleCreateHearing}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Hearing Date & Time *</label>
                <input
                  type="datetime-local"
                  value={hearingForm.hearingDate}
                  onChange={e => setHearingForm({ ...hearingForm, hearingDate: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Hearing Mode</label>
                <select
                  value={hearingForm.hearingType}
                  onChange={e => setHearingForm({ ...hearingForm, hearingType: e.target.value as (typeof hearingForm)['hearingType'] })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="VideoConference">Video Conference (WebEx / Portal VC)</option>
                  <option value="WrittenSubmission">Written Submission only</option>
                  <option value="PhysicalHearing">Physical Appearance</option>
                </select>
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Virtual Meeting Link</label>
                <input
                  type="url"
                  value={hearingForm.meetingLink || ''}
                  onChange={e => setHearingForm({ ...hearingForm, meetingLink: e.target.value })}
                  placeholder="https://webex.incometax.gov.in/m/..."
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Summary / Preparation Notes</label>
                <textarea
                  value={hearingForm.summaryNotes || ''}
                  onChange={e => setHearingForm({ ...hearingForm, summaryNotes: e.target.value })}
                  rows={3}
                  placeholder="Key points and documentation required for hearing"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="ghost" onClick={() => setShowHearingModal(false)}>Cancel</button>
                <button type="submit" className="primary">Schedule Hearing</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 3: Draft Written Submission */}
      {showDraftModal && activeNotice && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', padding: '24px', borderRadius: '8px', maxWidth: '600px', width: '90%' }}>
            <h2 style={{ marginTop: 0 }}>Draft Written Submission / Rebuttal</h2>
            <p style={{ color: '#666', fontSize: '13px' }}>
              For Notice: <strong>{activeNotice.section}</strong> ({activeNotice.clientName})
            </p>
            <form onSubmit={handleDraftResponse}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Submission Title *</label>
                <input
                  type="text"
                  value={draftForm.submissionTitle}
                  onChange={e => setDraftForm({ ...draftForm, submissionTitle: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Executive Summary of Submissions *</label>
                <textarea
                  value={draftForm.summaryOfSubmissions}
                  onChange={e => setDraftForm({ ...draftForm, summaryOfSubmissions: e.target.value })}
                  rows={4}
                  required
                  placeholder="Detailed explanation answering specific queries raised by assessing officer"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Legal Grounds & Case Laws</label>
                <textarea
                  value={draftForm.legalGrounds || ''}
                  onChange={e => setDraftForm({ ...draftForm, legalGrounds: e.target.value })}
                  rows={3}
                  placeholder="Statutory provisions, limitation periods, ITAT/High Court/Supreme Court citations"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Attachment Filenames</label>
                <input
                  type="text"
                  value={draftForm.attachmentNames || ''}
                  onChange={e => setDraftForm({ ...draftForm, attachmentNames: e.target.value })}
                  placeholder="e.g. Ledger_Copy.pdf, Bank_Certificate.pdf"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="ghost" onClick={() => setShowDraftModal(false)}>Cancel</button>
                <button type="submit" className="primary">Save Draft Submission</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 4: Record Portal Submission */}
      {showSubmitModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', padding: '24px', borderRadius: '8px', maxWidth: '450px', width: '90%' }}>
            <h2 style={{ marginTop: 0 }}>Record E-Filing Portal Submission</h2>
            <form onSubmit={handleSubmitResponse}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>
                  Portal Acknowledgement Number *
                </label>
                <input
                  type="text"
                  value={submitForm.portalAckNumber}
                  onChange={e => setSubmitForm({ ...submitForm, portalAckNumber: e.target.value })}
                  placeholder="e.g. ACK-ITBA-2024-XXXXXXXX"
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>
              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="ghost" onClick={() => setShowSubmitModal(false)}>Cancel</button>
                <button type="submit" className="primary">Confirm Submission</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 5: Record Demand Payment / Adjustment */}
      {showPaymentModal && activeDemand && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', padding: '24px', borderRadius: '8px', maxWidth: '450px', width: '90%' }}>
            <h2 style={{ marginTop: 0 }}>Record Demand Payment / Adjustment</h2>
            <p style={{ color: '#666', fontSize: '13px' }}>
              Outstanding: <strong>{formatCurrency(activeDemand.outstandingAmount)}</strong> ({activeDemand.clientName})
            </p>
            <form onSubmit={handleRecordPayment}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Payment / Adjustment Type</label>
                <select
                  value={paymentForm.isRefundAdjustment ? 'true' : 'false'}
                  onChange={e => setPaymentForm({ ...paymentForm, isRefundAdjustment: e.target.value === 'true' })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="false">Direct Tax Payment (Challan 280 / PMT-06)</option>
                  <option value="true">Sec 245 Refund Adjustment</option>
                </select>
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Amount (₹) *</label>
                <input
                  type="number"
                  value={paymentForm.amount}
                  onChange={e => setPaymentForm({ ...paymentForm, amount: parseFloat(e.target.value) || 0 })}
                  required
                  min={1}
                  max={activeDemand.outstandingAmount}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Remarks / BSR Challan #</label>
                <input
                  type="text"
                  value={paymentForm.remarks || ''}
                  onChange={e => setPaymentForm({ ...paymentForm, remarks: e.target.value })}
                  placeholder="e.g. Challan BSR 0210045 Dt 15/10/24"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="ghost" onClick={() => setShowPaymentModal(false)}>Cancel</button>
                <button type="submit" className="primary">Record</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal 6: Update Appeal Status */}
      {showAppealModal && activeDemand && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', padding: '24px', borderRadius: '8px', maxWidth: '450px', width: '90%' }}>
            <h2 style={{ marginTop: 0 }}>CIT(A) Appeal & Stay Status</h2>
            <form onSubmit={handleUpdateAppeal}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Appeal Status</label>
                <select
                  value={appealForm.status}
                  onChange={e => setAppealForm({ ...appealForm, status: e.target.value as (typeof appealForm)['status'] })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="NotAppealed">Not Appealed</option>
                  <option value="AppealDrafted">Appeal Drafted (Form 35 / APL-01)</option>
                  <option value="FiledCitA">Filed with CIT(A) / Appellate Authority</option>
                  <option value="StayGranted">Stay of Demand Granted (Sec 220(6))</option>
                  <option value="DemandDeleted">Demand Deleted / Annulled</option>
                  <option value="Settled">Settled / Closed</option>
                </select>
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Appeal Acknowledgement #</label>
                <input
                  type="text"
                  value={appealForm.appealAckNumber || ''}
                  onChange={e => setAppealForm({ ...appealForm, appealAckNumber: e.target.value })}
                  placeholder="Form 35 e-filing ACK number"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontWeight: 600, fontSize: '13px', marginBottom: '4px' }}>Stayed Amount (₹)</label>
                <input
                  type="number"
                  value={appealForm.stayedAmount || 0}
                  onChange={e => setAppealForm({ ...appealForm, stayedAmount: parseFloat(e.target.value) || 0 })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button type="button" className="ghost" onClick={() => setShowAppealModal(false)}>Cancel</button>
                <button type="submit" className="primary">Update Status</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
