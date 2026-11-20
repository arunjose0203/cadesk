import { useEffect, useState } from 'react'
import {
  type BranchAuditEngagementDto,
  type BranchAuditOfficeDto,
  type BranchAuditSummaryDto,
  type SaveBranchEngagementRequest,
  type SaveBranchOfficeRequest,
  type SubmitBranchReturnsRequest,
  type Client,
  branchAuditApi,
  api
} from './api'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0)

type ClientPage = {
  items: Client[]
  total: number
}

export function BranchAuditDeskPage({ can }: { can: (p: string) => boolean }) {
  const [engagements, setEngagements] = useState<BranchAuditEngagementDto[]>([])
  const [summary, setSummary] = useState<BranchAuditSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [toastMsg, setToastMsg] = useState<string | null>(null)

  // Filters
  const [filterFy, setFilterFy] = useState('2024-25')
  const [filterStatus, setFilterStatus] = useState('all')

  // Selected engagement
  const [selectedEngagement, setSelectedEngagement] = useState<BranchAuditEngagementDto | null>(null)
  const [activeTab, setActiveTab] = useState<'branches' | 'comparison' | 'sa600'>('branches')

  // Modals & Drawers
  const [showNewEngModal, setShowNewEngModal] = useState(false)
  const [showAddBranchModal, setShowAddBranchModal] = useState(false)
  const [editingBranch, setEditingBranch] = useState<BranchAuditOfficeDto | null>(null)
  const [reviewingBranch, setReviewingBranch] = useState<BranchAuditOfficeDto | null>(null)
  const [reviewDecision, setReviewDecision] = useState<'accept' | 'reject'>('accept')
  const [reviewNotes, setReviewNotes] = useState('')
  const [showPortalSimModal, setShowPortalSimModal] = useState(false)
  const [simToken, setSimToken] = useState('')

  // New Engagement Form
  const [engForm, setEngForm] = useState<SaveBranchEngagementRequest>({
    clientId: '',
    auditEngagementId: null,
    financialYear: '2024-25',
    title: 'Statutory Consolidation & Branch Audit FY 2024-25',
    headOfficeAssets: 500000000,      // ₹50 Cr
    headOfficeLiabilities: 200000000,  // ₹20 Cr
    headOfficeRevenue: 1000000000,    // ₹100 Cr
    headOfficeExpenses: 800000000,    // ₹80 Cr
    headOfficeNetProfit: 200000000,   // ₹20 Cr
    principalAuditorNotes: 'Principal audit team covering Head Office standalone books and consolidated statements.'
  })

  // Branch Office Form
  const [branchForm, setBranchForm] = useState<SaveBranchOfficeRequest>({
    branchCode: '',
    branchName: '',
    branchLocation: '',
    isForeignBranch: false,
    auditorFirmName: '',
    auditorFirmRegistrationNumber: '',
    auditorName: '',
    auditorMembershipNumber: '',
    auditorEmail: '',
    auditorPhone: '',
    totalAssets: 0,
    totalLiabilities: 0,
    totalRevenue: 0,
    totalExpenses: 0,
    netProfit: 0,
    branchReportType: 'Clean',
    hasQualifications: false,
    qualificationsSummary: '',
    branchAuditReportRef: '',
    accountingPoliciesConsistent: true,
    relatedPartyTransactionsDisclosed: true,
    contingentLiabilitiesReported: false,
    subsequentEventsReported: false,
    fraudOrIrregularitiesReported: false,
    internalControlsCommensurate: true,
    caroApplicableToBranch: true,
    caroReportSummary: ''
  })

  // External Portal Simulator Form
  const [portalSimForm, setPortalSimForm] = useState<SubmitBranchReturnsRequest>({
    token: '',
    totalAssets: 150000000,
    totalLiabilities: 50000000,
    totalRevenue: 300000000,
    totalExpenses: 250000000,
    netProfit: 50000000,
    branchReportType: 'Clean',
    hasQualifications: false,
    qualificationsSummary: '',
    branchAuditReportRef: 'BR/AUD/2025/01',
    accountingPoliciesConsistent: true,
    relatedPartyTransactionsDisclosed: true,
    contingentLiabilitiesReported: false,
    subsequentEventsReported: false,
    fraudOrIrregularitiesReported: false,
    internalControlsCommensurate: true,
    caroApplicableToBranch: true,
    caroReportSummary: 'Clean CARO return submitted for branch assets and inventories.'
  })

  useEffect(() => {
    loadData()
    loadClients()
  }, [filterFy, filterStatus])

  const showToast = (msg: string) => {
    setToastMsg(msg)
    setTimeout(() => setToastMsg(null), 4000)
  }

  const loadClients = async () => {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      setClients(res.items || [])
      if (res.items && res.items.length > 0 && !engForm.clientId) {
        setEngForm(f => ({ ...f, clientId: res.items[0].id }))
      }
    } catch (err: any) {
      console.error('Failed to load clients', err)
    }
  }

  const loadData = async () => {
    setLoading(true)
    setError(null)
    try {
      const [sumRes, engRes] = await Promise.all([
        branchAuditApi.getSummary(),
        branchAuditApi.getEngagements(undefined, filterFy !== 'all' ? filterFy : undefined, filterStatus !== 'all' ? filterStatus : undefined)
      ])
      setSummary(sumRes)
      setEngagements(engRes)

      if (engRes.length > 0) {
        if (!selectedEngagement || !engRes.find(e => e.id === selectedEngagement.id)) {
          setSelectedEngagement(engRes[0])
        } else {
          // Refresh selected engagement
          const updated = engRes.find(e => e.id === selectedEngagement.id)
          if (updated) setSelectedEngagement(updated)
        }
      } else {
        setSelectedEngagement(null)
      }
    } catch (err: any) {
      setError(err?.message || 'Failed to load branch audit engagements.')
    } finally {
      setLoading(false)
    }
  }

  const handleCreateEngagement = async () => {
    try {
      const created = await branchAuditApi.createEngagement(engForm)
      showToast('Branch audit engagement created successfully.')
      setShowNewEngModal(false)
      await loadData()
      setSelectedEngagement(created)
    } catch (err: any) {
      setError(err?.message || 'Failed to create engagement.')
    }
  }

  const handleSaveBranch = async () => {
    if (!selectedEngagement) return
    try {
      if (editingBranch) {
        await branchAuditApi.updateBranch(selectedEngagement.id, editingBranch.id, branchForm)
        showToast('Branch office details updated.')
      } else {
        await branchAuditApi.addBranch(selectedEngagement.id, branchForm)
        showToast('Branch office added successfully.')
      }
      setShowAddBranchModal(false)
      setEditingBranch(null)
      await loadData()
      const fresh = await branchAuditApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (err: any) {
      setError(err?.message || 'Failed to save branch office.')
    }
  }

  const handleDeleteBranch = async (branchId: string) => {
    if (!selectedEngagement) return
    if (!confirm('Are you sure you want to remove this branch office from the consolidation scope?')) return
    try {
      await branchAuditApi.deleteBranch(selectedEngagement.id, branchId)
      showToast('Branch office removed.')
      await loadData()
      const fresh = await branchAuditApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (err: any) {
      setError(err?.message || 'Failed to delete branch office.')
    }
  }

  const handleGenerateToken = async (branch: BranchAuditOfficeDto) => {
    if (!selectedEngagement) return
    try {
      const res = await branchAuditApi.generateToken(selectedEngagement.id, branch.id)
      const fullUrl = `${window.location.origin}${res.portalUrl}`
      navigator.clipboard.writeText(fullUrl)
      showToast(`Delegated Access Token generated! Submission link copied to clipboard.`)
      const fresh = await branchAuditApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (err: any) {
      setError(err?.message || 'Failed to generate branch access token.')
    }
  }

  const handleOpenReview = (branch: BranchAuditOfficeDto) => {
    setReviewingBranch(branch)
    setReviewDecision('accept')
    setReviewNotes(branch.principalAuditorReviewNotes || '')
  }

  const handleSubmitReview = async () => {
    if (!selectedEngagement || !reviewingBranch) return
    try {
      await branchAuditApi.reviewBranch(selectedEngagement.id, reviewingBranch.id, {
        decision: reviewDecision,
        reviewNotes: reviewNotes
      })
      showToast(`Branch audit returns ${reviewDecision === 'accept' ? 'accepted' : 'rejected'}.`)
      setReviewingBranch(null)
      await loadData()
      const fresh = await branchAuditApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (err: any) {
      setError(err?.message || 'Failed to review branch.')
    }
  }

  const handleConsolidate = async () => {
    if (!selectedEngagement) return
    try {
      const res = await branchAuditApi.consolidateEngagement(selectedEngagement.id)
      setSelectedEngagement(res)
      setActiveTab('sa600')
      showToast('Consolidation math executed and SA 600 report paragraph generated.')
      await loadData()
    } catch (err: any) {
      setError(err?.message || 'Failed to consolidate.')
    }
  }

  const handleFinalize = async () => {
    if (!selectedEngagement) return
    if (!confirm('Are you sure you want to finalize this branch audit consolidation? Once finalized, figures cannot be modified.')) return
    try {
      const res = await branchAuditApi.finalizeEngagement(selectedEngagement.id)
      setSelectedEngagement(res)
      showToast('Engagement successfully finalized pursuant to Section 143(8) and SA 600.')
      await loadData()
    } catch (err: any) {
      setError(err?.message || 'Failed to finalize.')
    }
  }

  const handleSimulatePortalSubmission = async () => {
    try {
      await branchAuditApi.submitBranchReturns(portalSimForm)
      showToast('External branch auditor submission simulated successfully!')
      setShowPortalSimModal(false)
      await loadData()
      if (selectedEngagement) {
        const fresh = await branchAuditApi.getEngagementById(selectedEngagement.id)
        setSelectedEngagement(fresh)
      }
    } catch (err: any) {
      setError(err?.message || 'Portal submission simulation failed.')
    }
  }

  const copySa600Text = () => {
    if (!selectedEngagement?.generatedSa600ReportText) return
    navigator.clipboard.writeText(selectedEngagement.generatedSa600ReportText)
    showToast('SA 600 Other Matter paragraph copied to clipboard!')
  }

  const openNewBranchModal = () => {
    setEditingBranch(null)
    setBranchForm({
      branchCode: `BR-0${(selectedEngagement?.branches?.length || 0) + 1}`,
      branchName: '',
      branchLocation: '',
      isForeignBranch: false,
      auditorFirmName: '',
      auditorFirmRegistrationNumber: '',
      auditorName: '',
      auditorMembershipNumber: '',
      auditorEmail: '',
      auditorPhone: '',
      totalAssets: 0,
      totalLiabilities: 0,
      totalRevenue: 0,
      totalExpenses: 0,
      netProfit: 0,
      branchReportType: 'Clean',
      hasQualifications: false,
      qualificationsSummary: '',
      branchAuditReportRef: '',
      accountingPoliciesConsistent: true,
      relatedPartyTransactionsDisclosed: true,
      contingentLiabilitiesReported: false,
      subsequentEventsReported: false,
      fraudOrIrregularitiesReported: false,
      internalControlsCommensurate: true,
      caroApplicableToBranch: true,
      caroReportSummary: ''
    })
    setShowAddBranchModal(true)
  }

  const openEditBranchModal = (b: BranchAuditOfficeDto) => {
    setEditingBranch(b)
    setBranchForm({
      branchCode: b.branchCode,
      branchName: b.branchName,
      branchLocation: b.branchLocation || '',
      isForeignBranch: b.isForeignBranch ?? false,
      auditorFirmName: b.auditorFirmName || '',
      auditorFirmRegistrationNumber: b.auditorFirmRegistrationNumber || '',
      auditorName: b.auditorName || '',
      auditorMembershipNumber: b.auditorMembershipNumber || '',
      auditorEmail: b.auditorEmail || '',
      auditorPhone: b.auditorPhone || '',
      totalAssets: b.totalAssets || 0,
      totalLiabilities: b.totalLiabilities || 0,
      totalRevenue: b.totalRevenue || 0,
      totalExpenses: b.totalExpenses || 0,
      netProfit: b.netProfit || 0,
      branchReportType: b.branchReportType || 'Clean',
      hasQualifications: b.hasQualifications || false,
      qualificationsSummary: b.qualificationsSummary || '',
      branchAuditReportRef: b.branchAuditReportRef || '',
      accountingPoliciesConsistent: b.accountingPoliciesConsistent ?? true,
      relatedPartyTransactionsDisclosed: b.relatedPartyTransactionsDisclosed ?? true,
      contingentLiabilitiesReported: b.contingentLiabilitiesReported ?? false,
      subsequentEventsReported: b.subsequentEventsReported ?? false,
      fraudOrIrregularitiesReported: b.fraudOrIrregularitiesReported ?? false,
      internalControlsCommensurate: b.internalControlsCommensurate ?? true,
      caroApplicableToBranch: b.caroApplicableToBranch ?? true,
      caroReportSummary: b.caroReportSummary || ''
    })
    setShowAddBranchModal(true)
  }

  return (
    <div className="desk-container" style={{ padding: '24px 32px' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h1 style={{ margin: 0, fontSize: '1.75rem', fontWeight: 700, letterSpacing: '-0.02em' }}>
              Multi-Firm Collaborative Audit & Branch Consolidation Desk
            </h1>
            <span style={{
              background: '#ede9fe',
              color: '#6d28d9',
              fontSize: '0.75rem',
              fontWeight: 700,
              padding: '3px 8px',
              borderRadius: '6px',
              border: '1px solid #ddd6fe'
            }}>
              Sprint 33
            </span>
            {loading && <span style={{ fontSize: '0.8rem', color: '#64748b', fontWeight: 500 }}>Refreshing...</span>}
          </div>
          <p className="muted" style={{ margin: '6px 0 0', fontSize: '0.95rem' }}>
            Statutory Branch Audits under <strong>Companies Act, 2013 Sec 143(8)</strong> & <strong>ICAI SA 600</strong> ("Using the Work of Another Auditor")
          </p>
          <div style={{ display: 'flex', gap: '8px', marginTop: '10px', flexWrap: 'wrap' }}>
            <span style={{ fontSize: '0.78rem', background: '#f1f5f9', color: '#475569', padding: '2px 8px', borderRadius: '4px' }}>
              ⚖️ Sec 143(8) & Rule 12 Mandate
            </span>
            <span style={{ fontSize: '0.78rem', background: '#f1f5f9', color: '#475569', padding: '2px 8px', borderRadius: '4px' }}>
              📋 SA 600 Magnitude of Operations (% Assets / Revenues)
            </span>
            <span style={{ fontSize: '0.78rem', background: '#f1f5f9', color: '#475569', padding: '2px 8px', borderRadius: '4px' }}>
              🔐 Delegated Branch Auditor Portal
            </span>
            <span style={{ fontSize: '0.78rem', background: '#f1f5f9', color: '#475569', padding: '2px 8px', borderRadius: '4px' }}>
              📑 Statutory "Other Matter" Paragraph Draft
            </span>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          {can('branchaudit.manage') && (
            <button
              onClick={() => setShowNewEngModal(true)}
              style={{
                background: '#4f46e5',
                color: '#fff',
                border: 'none',
                padding: '9px 16px',
                borderRadius: '6px',
                fontWeight: 600,
                fontSize: '0.88rem',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>+</span> New Branch Audit Engagement
            </button>
          )}
        </div>
      </div>

      {toastMsg && (
        <div style={{
          background: '#10b981',
          color: '#fff',
          padding: '10px 18px',
          borderRadius: '6px',
          marginBottom: '18px',
          fontWeight: 500,
          boxShadow: '0 2px 8px rgba(16,185,129,0.25)'
        }}>
          ✓ {toastMsg}
        </div>
      )}

      {error && (
        <div style={{
          background: '#fee2e2',
          border: '1px solid #fca5a5',
          color: '#b91c1c',
          padding: '12px 16px',
          borderRadius: '6px',
          marginBottom: '18px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span>{error}</span>
          <button onClick={() => setError(null)} style={{ background: 'none', border: 'none', color: '#b91c1c', cursor: 'pointer', fontWeight: 700 }}>✕</button>
        </div>
      )}

      {/* KPI Cards Row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px', marginBottom: '24px' }}>
        <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: '#64748b', fontWeight: 600 }}>Total Engagements</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>
            {summary?.totalEngagements ?? 0}
          </div>
          <div style={{ fontSize: '0.75rem', color: '#10b981', marginTop: '4px' }}>
            {summary?.finalizedCount ?? 0} Finalized · {summary?.consolidatedCount ?? 0} Consolidated
          </div>
        </div>

        <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: '#64748b', fontWeight: 600 }}>Branch Offices Tracked</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>
            {summary?.totalBranches ?? 0}
          </div>
          <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '4px' }}>
            {summary?.branchesAccepted ?? 0} Accepted · {summary?.branchesSubmitted ?? 0} Submitted
          </div>
        </div>

        <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: '#64748b', fontWeight: 600 }}>Total Branch Assets Audited</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#059669', marginTop: '4px' }}>
            {fmtInr(summary?.totalBranchAssetsAudited ?? 0)}
          </div>
          <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '4px' }}>
            Audited by independent branch auditors
          </div>
        </div>

        <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: '#64748b', fontWeight: 600 }}>Avg. Branch Assets Magnitude</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#4f46e5', marginTop: '4px' }}>
            {summary?.averageBranchAssetsPercentage ?? 0}%
          </div>
          <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '4px' }}>
            SA 600 Magnitude of Operations
          </div>
        </div>

        <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: '#64748b', fontWeight: 600 }}>Branch Qualifications</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: (summary?.totalBranchQualifications ?? 0) > 0 ? '#b91c1c' : '#059669', marginTop: '4px' }}>
            {summary?.totalBranchQualifications ?? 0}
          </div>
          <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '4px' }}>
            Tracked for SA 600 Other Matter notes
          </div>
        </div>
      </div>

      {/* Engagement Selection & Filter Strip */}
      <div style={{
        background: '#fff',
        padding: '16px 20px',
        borderRadius: '8px',
        border: '1px solid #e2e8f0',
        marginBottom: '24px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: '12px'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px', flexWrap: 'wrap' }}>
          <div>
            <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#64748b', display: 'block', marginBottom: '4px' }}>
              Active Engagement
            </label>
            <select
              value={selectedEngagement?.id || ''}
              onChange={e => {
                const found = engagements.find(eng => eng.id === e.target.value)
                if (found) setSelectedEngagement(found)
              }}
              style={{
                padding: '7px 12px',
                borderRadius: '6px',
                border: '1px solid #cbd5e1',
                fontSize: '0.88rem',
                minWidth: '320px',
                fontWeight: 600
              }}
            >
              {engagements.length === 0 && <option value="">No engagements found</option>}
              {engagements.map(eng => (
                <option key={eng.id} value={eng.id}>
                  {eng.clientName || 'Client'} — {eng.title} ({eng.financialYear})
                </option>
              ))}
            </select>
          </div>

          <div>
            <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#64748b', display: 'block', marginBottom: '4px' }}>
              Financial Year
            </label>
            <select
              value={filterFy}
              onChange={e => setFilterFy(e.target.value)}
              style={{ padding: '7px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '0.88rem' }}
            >
              <option value="all">All FYs</option>
              <option value="2024-25">2024-25</option>
              <option value="2023-24">2023-24</option>
            </select>
          </div>

          <div>
            <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#64748b', display: 'block', marginBottom: '4px' }}>
              Status
            </label>
            <select
              value={filterStatus}
              onChange={e => setFilterStatus(e.target.value)}
              style={{ padding: '7px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '0.88rem' }}
            >
              <option value="all">All Statuses</option>
              <option value="draft">Draft</option>
              <option value="branches_in_progress">Branches in Progress</option>
              <option value="consolidated">Consolidated</option>
              <option value="finalized">Finalized</option>
            </select>
          </div>
        </div>

        {selectedEngagement && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <span style={{
              fontSize: '0.78rem',
              fontWeight: 700,
              padding: '4px 10px',
              borderRadius: '9999px',
              textTransform: 'uppercase',
              background: selectedEngagement.consolidationStatus === 'finalized' ? '#dcfce7' :
                          selectedEngagement.consolidationStatus === 'consolidated' ? '#ede9fe' :
                          selectedEngagement.consolidationStatus === 'branches_in_progress' ? '#fef3c7' : '#f1f5f9',
              color: selectedEngagement.consolidationStatus === 'finalized' ? '#166534' :
                     selectedEngagement.consolidationStatus === 'consolidated' ? '#6d28d9' :
                     selectedEngagement.consolidationStatus === 'branches_in_progress' ? '#92400e' : '#475569'
            }}>
              {selectedEngagement.consolidationStatus.replace('_', ' ')}
            </span>

            {can('branchaudit.consolidate') && selectedEngagement.consolidationStatus !== 'finalized' && (
              <button
                onClick={handleConsolidate}
                style={{
                  background: '#3b82f6',
                  color: '#fff',
                  border: 'none',
                  padding: '7px 14px',
                  borderRadius: '6px',
                  fontWeight: 600,
                  fontSize: '0.82rem',
                  cursor: 'pointer'
                }}
              >
                ⚡ Consolidate & Draft SA 600
              </button>
            )}

            {can('branchaudit.consolidate') && selectedEngagement.consolidationStatus === 'consolidated' && (
              <button
                onClick={handleFinalize}
                style={{
                  background: '#10b981',
                  color: '#fff',
                  border: 'none',
                  padding: '7px 14px',
                  borderRadius: '6px',
                  fontWeight: 600,
                  fontSize: '0.82rem',
                  cursor: 'pointer'
                }}
              >
                ✓ Finalize Report
              </button>
            )}
          </div>
        )}
      </div>

      {/* Main Content Area */}
      {selectedEngagement ? (
        <div style={{ background: '#fff', borderRadius: '8px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          {/* Tabs navigation */}
          <div style={{
            display: 'flex',
            borderBottom: '1px solid #e2e8f0',
            background: '#f8fafc',
            padding: '0 16px'
          }}>
            <button
              onClick={() => setActiveTab('branches')}
              style={{
                padding: '14px 20px',
                border: 'none',
                background: 'none',
                fontWeight: 600,
                fontSize: '0.88rem',
                cursor: 'pointer',
                color: activeTab === 'branches' ? '#4f46e5' : '#64748b',
                borderBottom: activeTab === 'branches' ? '2px solid #4f46e5' : '2px solid transparent'
              }}
            >
              🏢 Branch Offices & Delegated Portals ({selectedEngagement.branches?.length || 0})
            </button>
            <button
              onClick={() => setActiveTab('comparison')}
              style={{
                padding: '14px 20px',
                border: 'none',
                background: 'none',
                fontWeight: 600,
                fontSize: '0.88rem',
                cursor: 'pointer',
                color: activeTab === 'comparison' ? '#4f46e5' : '#64748b',
                borderBottom: activeTab === 'comparison' ? '2px solid #4f46e5' : '2px solid transparent'
              }}
            >
              📊 Financials Comparison & Magnitude (% Assets/Revenue)
            </button>
            <button
              onClick={() => setActiveTab('sa600')}
              style={{
                padding: '14px 20px',
                border: 'none',
                background: 'none',
                fontWeight: 600,
                fontSize: '0.88rem',
                cursor: 'pointer',
                color: activeTab === 'sa600' ? '#4f46e5' : '#64748b',
                borderBottom: activeTab === 'sa600' ? '2px solid #4f46e5' : '2px solid transparent'
              }}
            >
              ⚖️ ICAI SA 600 Other Matter Paragraph Draft
            </button>
          </div>

          {/* TAB 1: BRANCH OFFICES TABLE */}
          {activeTab === 'branches' && (
            <div style={{ padding: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <div>
                  <h3 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Branch Offices (Section 143(8))</h3>
                  <p className="muted" style={{ margin: '4px 0 0', fontSize: '0.82rem' }}>
                    Branch auditors appointed under Section 139 / 143(8) of the Companies Act submit certified branch returns and SA 600 confirmations.
                  </p>
                </div>

                {can('branchaudit.manage') && selectedEngagement.consolidationStatus !== 'finalized' && (
                  <button
                    onClick={openNewBranchModal}
                    style={{
                      background: '#4f46e5',
                      color: '#fff',
                      border: 'none',
                      padding: '7px 14px',
                      borderRadius: '6px',
                      fontWeight: 600,
                      fontSize: '0.82rem',
                      cursor: 'pointer'
                    }}
                  >
                    + Add Branch Office
                  </button>
                )}
              </div>

              {(!selectedEngagement.branches || selectedEngagement.branches.length === 0) ? (
                <div style={{ textAlign: 'center', padding: '48px 24px', color: '#64748b' }}>
                  <div style={{ fontSize: '2.5rem', marginBottom: '12px' }}>🏢</div>
                  <div style={{ fontWeight: 600, fontSize: '1rem', color: '#1e293b' }}>No Branch Offices Added Yet</div>
                  <p style={{ maxWidth: '420px', margin: '8px auto 16px', fontSize: '0.85rem' }}>
                    Add branch locations to track external branch auditors, generate secure access tokens, and consolidate figures under ICAI SA 600.
                  </p>
                  {can('branchaudit.manage') && (
                    <button
                      onClick={openNewBranchModal}
                      style={{
                        background: '#4f46e5',
                        color: '#fff',
                        border: 'none',
                        padding: '8px 16px',
                        borderRadius: '6px',
                        fontWeight: 600,
                        cursor: 'pointer'
                      }}
                    >
                      + Add First Branch Office
                    </button>
                  )}
                </div>
              ) : (
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.84rem' }}>
                    <thead>
                      <tr style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', textAlign: 'left', color: '#475569' }}>
                        <th style={{ padding: '10px 12px' }}>Code & Branch</th>
                        <th style={{ padding: '10px 12px' }}>Location / Scope</th>
                        <th style={{ padding: '10px 12px' }}>Branch Auditor Firm</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>Branch Assets</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>Branch Revenue</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>Net Profit</th>
                        <th style={{ padding: '10px 12px' }}>Report Type</th>
                        <th style={{ padding: '10px 12px' }}>Status</th>
                        <th style={{ padding: '10px 12px', textAlign: 'center' }}>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedEngagement.branches.map(b => (
                        <tr key={b.id} style={{ borderBottom: '1px solid #e2e8f0' }}>
                          <td style={{ padding: '12px' }}>
                            <div style={{ fontWeight: 600, color: '#0f172a' }}>{b.branchName}</div>
                            <div style={{ fontSize: '0.75rem', color: '#64748b' }}>Code: {b.branchCode}</div>
                          </td>
                          <td style={{ padding: '12px' }}>
                            <div>{b.branchLocation || '—'}</div>
                            <span style={{
                              fontSize: '0.7rem',
                              fontWeight: 600,
                              color: b.isForeignBranch ? '#9333ea' : '#0369a1',
                              background: b.isForeignBranch ? '#f3e8ff' : '#e0f2fe',
                              padding: '1px 6px',
                              borderRadius: '4px'
                            }}>
                              {b.isForeignBranch ? 'Foreign Branch' : 'Domestic Branch'}
                            </span>
                          </td>
                          <td style={{ padding: '12px' }}>
                            <div style={{ fontWeight: 500 }}>{b.auditorFirmName || 'Unassigned'}</div>
                            <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
                              {b.auditorFirmRegistrationNumber ? `FRN: ${b.auditorFirmRegistrationNumber}` : ''}
                              {b.auditorName ? ` · ${b.auditorName}` : ''}
                            </div>
                            {b.auditorEmail && <div style={{ fontSize: '0.72rem', color: '#3b82f6' }}>{b.auditorEmail}</div>}
                          </td>
                          <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#0f172a' }}>
                            {fmtInr(b.totalAssets)}
                          </td>
                          <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#0f172a' }}>
                            {fmtInr(b.totalRevenue)}
                          </td>
                          <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: b.netProfit >= 0 ? '#059669' : '#dc2626' }}>
                            {fmtInr(b.netProfit)}
                          </td>
                          <td style={{ padding: '12px' }}>
                            <span style={{
                              fontSize: '0.72rem',
                              fontWeight: 700,
                              padding: '2px 8px',
                              borderRadius: '4px',
                              background: b.branchReportType === 'Clean' ? '#dcfce7' : '#fee2e2',
                              color: b.branchReportType === 'Clean' ? '#15803d' : '#b91c1c'
                            }}>
                              {b.branchReportType.toUpperCase()}
                            </span>
                            {b.hasQualifications && (
                              <div style={{ fontSize: '0.7rem', color: '#b91c1c', marginTop: '3px' }}>
                                ⚠️ Qualified
                              </div>
                            )}
                          </td>
                          <td style={{ padding: '12px' }}>
                            <span style={{
                              fontSize: '0.72rem',
                              fontWeight: 600,
                              padding: '2px 8px',
                              borderRadius: '4px',
                              background: b.status === 'Accepted' ? '#dcfce7' :
                                          b.status === 'SubmittedByBranch' ? '#fef3c7' :
                                          b.status === 'PackageSent' ? '#e0f2fe' : '#f1f5f9',
                              color: b.status === 'Accepted' ? '#15803d' :
                                     b.status === 'SubmittedByBranch' ? '#92400e' :
                                     b.status === 'PackageSent' ? '#0369a1' : '#475569'
                            }}>
                              {b.status}
                            </span>
                          </td>
                          <td style={{ padding: '12px', textAlign: 'center' }}>
                            <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                              {can('branchaudit.manage') && selectedEngagement.consolidationStatus !== 'finalized' && (
                                <button
                                  onClick={() => handleGenerateToken(b)}
                                  title="Generate & copy delegated external submission token"
                                  style={{
                                    background: '#f1f5f9',
                                    border: '1px solid #cbd5e1',
                                    borderRadius: '4px',
                                    padding: '4px 8px',
                                    fontSize: '0.72rem',
                                    cursor: 'pointer'
                                  }}
                                >
                                  🔑 Token
                                </button>
                              )}

                              {can('branchaudit.manage') && selectedEngagement.consolidationStatus !== 'finalized' && (
                                <button
                                  onClick={() => {
                                    setSimToken(b.id)
                                    setPortalSimForm(f => ({ ...f, token: 'simulate_live' }))
                                    setShowPortalSimModal(true)
                                  }}
                                  title="Simulate external auditor submission"
                                  style={{
                                    background: '#e0f2fe',
                                    color: '#0369a1',
                                    border: '1px solid #bae6fd',
                                    borderRadius: '4px',
                                    padding: '4px 8px',
                                    fontSize: '0.72rem',
                                    cursor: 'pointer'
                                  }}
                                >
                                  📤 Test Portal
                                </button>
                              )}

                              {can('branchaudit.manage') && selectedEngagement.consolidationStatus !== 'finalized' && (
                                <button
                                  onClick={() => handleOpenReview(b)}
                                  title="Review branch audit returns"
                                  style={{
                                    background: '#ede9fe',
                                    color: '#6d28d9',
                                    border: '1px solid #ddd6fe',
                                    borderRadius: '4px',
                                    padding: '4px 8px',
                                    fontSize: '0.72rem',
                                    cursor: 'pointer'
                                  }}
                                >
                                  🔍 Review
                                </button>
                              )}

                              {can('branchaudit.manage') && selectedEngagement.consolidationStatus !== 'finalized' && (
                                <button
                                  onClick={() => openEditBranchModal(b)}
                                  title="Edit branch details"
                                  style={{
                                    background: '#f1f5f9',
                                    border: '1px solid #cbd5e1',
                                    borderRadius: '4px',
                                    padding: '4px 8px',
                                    fontSize: '0.72rem',
                                    cursor: 'pointer'
                                  }}
                                >
                                  ✏️
                                </button>
                              )}

                              {can('branchaudit.manage') && selectedEngagement.consolidationStatus !== 'finalized' && (
                                <button
                                  onClick={() => handleDeleteBranch(b.id)}
                                  title="Remove branch"
                                  style={{
                                    background: '#fee2e2',
                                    color: '#b91c1c',
                                    border: '1px solid #fecaca',
                                    borderRadius: '4px',
                                    padding: '4px 8px',
                                    fontSize: '0.72rem',
                                    cursor: 'pointer'
                                  }}
                                >
                                  🗑️
                                </button>
                              )}
                            </div>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {/* TAB 2: FINANCIALS COMPARISON & MAGNITUDE */}
          {activeTab === 'comparison' && (
            <div style={{ padding: '24px' }}>
              <div style={{ marginBottom: '20px' }}>
                <h3 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>Head Office vs Branch Consolidation Matrix</h3>
                <p className="muted" style={{ margin: '4px 0 0', fontSize: '0.85rem' }}>
                  Aggregation of Standalone Head Office books with Certified Branch Returns and quantification of operations audited by other auditors.
                </p>
              </div>

              {/* Magnitude Metrics Banner */}
              <div style={{
                background: 'linear-gradient(135deg, #f8fafc 0%, #f1f5f9 100%)',
                padding: '20px',
                borderRadius: '8px',
                border: '1px solid #cbd5e1',
                marginBottom: '24px'
              }}>
                <div style={{ fontWeight: 700, fontSize: '0.9rem', color: '#0f172a', marginBottom: '12px' }}>
                  ICAI SA 600 Magnitude of Operations Audited by Other Auditors
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
                  <div>
                    <div style={{ fontSize: '0.78rem', color: '#64748b' }}>Branch Assets Magnitude</div>
                    <div style={{ fontSize: '1.3rem', fontWeight: 700, color: '#4f46e5' }}>
                      {selectedEngagement.branchAssetsPercentage}%
                    </div>
                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
                      {fmtInr(selectedEngagement.totalBranchAssets)} of {fmtInr(selectedEngagement.totalConsolidatedAssets)}
                    </div>
                  </div>

                  <div>
                    <div style={{ fontSize: '0.78rem', color: '#64748b' }}>Branch Revenue Magnitude</div>
                    <div style={{ fontSize: '1.3rem', fontWeight: 700, color: '#0284c7' }}>
                      {selectedEngagement.branchRevenuePercentage}%
                    </div>
                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
                      {fmtInr(selectedEngagement.totalBranchRevenue)} of {fmtInr(selectedEngagement.totalConsolidatedRevenue)}
                    </div>
                  </div>

                  <div>
                    <div style={{ fontSize: '0.78rem', color: '#64748b' }}>Branch Net Profit Magnitude</div>
                    <div style={{ fontSize: '1.3rem', fontWeight: 700, color: '#10b981' }}>
                      {selectedEngagement.branchNetProfitPercentage}%
                    </div>
                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
                      {fmtInr(selectedEngagement.totalBranchNetProfit)} of {fmtInr(selectedEngagement.totalConsolidatedNetProfit)}
                    </div>
                  </div>

                  <div>
                    <div style={{ fontSize: '0.78rem', color: '#64748b' }}>Independent Branch Auditors</div>
                    <div style={{ fontSize: '1.3rem', fontWeight: 700, color: '#6d28d9' }}>
                      {selectedEngagement.branchAuditorsCount} Firms
                    </div>
                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
                      Across {selectedEngagement.branchesAuditedCount} audited branches
                    </div>
                  </div>
                </div>
              </div>

              {/* Consolidated Table */}
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.88rem' }}>
                  <thead>
                    <tr style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', textAlign: 'left', color: '#475569' }}>
                      <th style={{ padding: '12px 14px' }}>Financial Metric</th>
                      <th style={{ padding: '12px 14px', textAlign: 'right' }}>Head Office (Standalone)</th>
                      <th style={{ padding: '12px 14px', textAlign: 'right' }}>Branches Total (Audited)</th>
                      <th style={{ padding: '12px 14px', textAlign: 'right' }}>Consolidated Total</th>
                      <th style={{ padding: '12px 14px', textAlign: 'right' }}>Branch Share (%)</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr style={{ borderBottom: '1px solid #e2e8f0' }}>
                      <td style={{ padding: '12px 14px', fontWeight: 600 }}>Total Assets</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right' }}>{fmtInr(selectedEngagement.headOfficeAssets)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', color: '#4f46e5', fontWeight: 600 }}>{fmtInr(selectedEngagement.totalBranchAssets)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', fontWeight: 700 }}>{fmtInr(selectedEngagement.totalConsolidatedAssets)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', fontWeight: 600 }}>{selectedEngagement.branchAssetsPercentage}%</td>
                    </tr>
                    <tr style={{ borderBottom: '1px solid #e2e8f0' }}>
                      <td style={{ padding: '12px 14px', fontWeight: 600 }}>Total Liabilities</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right' }}>{fmtInr(selectedEngagement.headOfficeLiabilities)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', color: '#4f46e5', fontWeight: 600 }}>{fmtInr(selectedEngagement.totalConsolidatedLiabilities - selectedEngagement.headOfficeLiabilities)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', fontWeight: 700 }}>{fmtInr(selectedEngagement.totalConsolidatedLiabilities)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', fontWeight: 600 }}>
                        {selectedEngagement.totalConsolidatedLiabilities > 0
                          ? `${Math.round(((selectedEngagement.totalConsolidatedLiabilities - selectedEngagement.headOfficeLiabilities) / selectedEngagement.totalConsolidatedLiabilities) * 10000) / 100}%`
                          : '0%'}
                      </td>
                    </tr>
                    <tr style={{ borderBottom: '1px solid #e2e8f0' }}>
                      <td style={{ padding: '12px 14px', fontWeight: 600 }}>Total Revenues / Turnover</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right' }}>{fmtInr(selectedEngagement.headOfficeRevenue)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', color: '#0284c7', fontWeight: 600 }}>{fmtInr(selectedEngagement.totalBranchRevenue)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', fontWeight: 700 }}>{fmtInr(selectedEngagement.totalConsolidatedRevenue)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', fontWeight: 600 }}>{selectedEngagement.branchRevenuePercentage}%</td>
                    </tr>
                    <tr style={{ borderBottom: '1px solid #e2e8f0' }}>
                      <td style={{ padding: '12px 14px', fontWeight: 600 }}>Total Expenses</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right' }}>{fmtInr(selectedEngagement.headOfficeExpenses)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', color: '#4f46e5', fontWeight: 600 }}>{fmtInr(selectedEngagement.totalConsolidatedExpenses - selectedEngagement.headOfficeExpenses)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', fontWeight: 700 }}>{fmtInr(selectedEngagement.totalConsolidatedExpenses)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', fontWeight: 600 }}>
                        {selectedEngagement.totalConsolidatedExpenses > 0
                          ? `${Math.round(((selectedEngagement.totalConsolidatedExpenses - selectedEngagement.headOfficeExpenses) / selectedEngagement.totalConsolidatedExpenses) * 10000) / 100}%`
                          : '0%'}
                      </td>
                    </tr>
                    <tr style={{ background: '#f8fafc', fontWeight: 700 }}>
                      <td style={{ padding: '12px 14px' }}>Net Profit / (Loss)</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right' }}>{fmtInr(selectedEngagement.headOfficeNetProfit)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right', color: '#10b981' }}>{fmtInr(selectedEngagement.totalBranchNetProfit)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right' }}>{fmtInr(selectedEngagement.totalConsolidatedNetProfit)}</td>
                      <td style={{ padding: '12px 14px', textAlign: 'right' }}>{selectedEngagement.branchNetProfitPercentage}%</td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* TAB 3: SA 600 OTHER MATTER PARAGRAPH */}
          {activeTab === 'sa600' && (
            <div style={{ padding: '24px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <div>
                  <h3 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>
                    ICAI SA 600 "Other Matter" Paragraph for Independent Auditor's Report
                  </h3>
                  <p className="muted" style={{ margin: '4px 0 0', fontSize: '0.85rem' }}>
                    Mandatory statutory disclosure wording pursuant to Section 143(8) and Standard on Auditing 600 ("Using the Work of Another Auditor").
                  </p>
                </div>

                <div style={{ display: 'flex', gap: '8px' }}>
                  {selectedEngagement.generatedSa600ReportText && (
                    <button
                      onClick={copySa600Text}
                      style={{
                        background: '#f1f5f9',
                        border: '1px solid #cbd5e1',
                        borderRadius: '6px',
                        padding: '7px 14px',
                        fontSize: '0.82rem',
                        fontWeight: 600,
                        cursor: 'pointer'
                      }}
                    >
                      📋 Copy Legal Text
                    </button>
                  )}
                  {can('branchaudit.consolidate') && selectedEngagement.consolidationStatus !== 'finalized' && (
                    <button
                      onClick={handleConsolidate}
                      style={{
                        background: '#4f46e5',
                        color: '#fff',
                        border: 'none',
                        borderRadius: '6px',
                        padding: '7px 14px',
                        fontSize: '0.82rem',
                        fontWeight: 600,
                        cursor: 'pointer'
                      }}
                    >
                      🔄 Re-Generate Text
                    </button>
                  )}
                </div>
              </div>

              {selectedEngagement.generatedSa600ReportText ? (
                <div style={{
                  background: '#1e293b',
                  color: '#f8fafc',
                  padding: '24px',
                  borderRadius: '8px',
                  fontFamily: 'Consolas, Monaco, "Courier New", monospace',
                  fontSize: '0.86rem',
                  lineHeight: '1.6',
                  whiteSpace: 'pre-wrap',
                  overflowX: 'auto',
                  border: '1px solid #334155'
                }}>
                  {selectedEngagement.generatedSa600ReportText}
                </div>
              ) : (
                <div style={{ textAlign: 'center', padding: '48px 24px', background: '#f8fafc', borderRadius: '8px', border: '1px dashed #cbd5e1' }}>
                  <div style={{ fontSize: '2rem', marginBottom: '8px' }}>📑</div>
                  <div style={{ fontWeight: 600, color: '#1e293b' }}>SA 600 Report Text Not Yet Generated</div>
                  <p style={{ maxWidth: '400px', margin: '8px auto 16px', fontSize: '0.84rem', color: '#64748b' }}>
                    Click the button below to consolidate submitted branch returns and generate the official statutory disclosure paragraph.
                  </p>
                  <button
                    onClick={handleConsolidate}
                    style={{
                      background: '#4f46e5',
                      color: '#fff',
                      border: 'none',
                      borderRadius: '6px',
                      padding: '8px 16px',
                      fontWeight: 600,
                      cursor: 'pointer'
                    }}
                  >
                    ⚡ Consolidate & Generate SA 600 Report
                  </button>
                </div>
              )}
            </div>
          )}
        </div>
      ) : (
        <div style={{ textAlign: 'center', padding: '64px 24px', background: '#fff', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
          <div style={{ fontSize: '3rem', marginBottom: '12px' }}>🏢</div>
          <h2 style={{ margin: 0, fontWeight: 700, fontSize: '1.25rem', color: '#0f172a' }}>No Engagement Selected</h2>
          <p className="muted" style={{ maxWidth: '480px', margin: '8px auto 20px', fontSize: '0.9rem' }}>
            Select an existing branch audit engagement from the dropdown above, or create a new multi-branch consolidation workspace for your corporate audit clients.
          </p>
          {can('branchaudit.manage') && (
            <button
              onClick={() => setShowNewEngModal(true)}
              style={{
                background: '#4f46e5',
                color: '#fff',
                border: 'none',
                padding: '9px 18px',
                borderRadius: '6px',
                fontWeight: 600,
                cursor: 'pointer'
              }}
            >
              + Create Branch Audit Engagement
            </button>
          )}
        </div>
      )}

      {/* MODAL 1: CREATE ENGAGEMENT */}
      {showNewEngModal && (
        <div style={{
          position: 'fixed',
          inset: 0,
          background: 'rgba(15,23,42,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 9999,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '10px',
            width: '100%',
            maxWidth: '640px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h2 style={{ margin: '0 0 16px', fontSize: '1.25rem', fontWeight: 700 }}>
              New Branch Audit & Consolidation Engagement
            </h2>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>
                  Client Organization *
                </label>
                <select
                  value={engForm.clientId}
                  onChange={e => setEngForm(f => ({ ...f, clientId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                >
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>
                  Financial Year *
                </label>
                <input
                  type="text"
                  value={engForm.financialYear}
                  onChange={e => setEngForm(f => ({ ...f, financialYear: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>
                Engagement Title *
              </label>
              <input
                type="text"
                value={engForm.title}
                onChange={e => setEngForm(f => ({ ...f, title: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ background: '#f8fafc', padding: '14px', borderRadius: '6px', border: '1px solid #e2e8f0', marginBottom: '16px' }}>
              <div style={{ fontWeight: 600, fontSize: '0.85rem', marginBottom: '10px', color: '#0f172a' }}>
                Head Office Standalone Financials (INR)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Head Office Assets</label>
                  <input
                    type="number"
                    value={engForm.headOfficeAssets}
                    onChange={e => setEngForm(f => ({ ...f, headOfficeAssets: parseFloat(e.target.value) || 0 }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Head Office Liabilities</label>
                  <input
                    type="number"
                    value={engForm.headOfficeLiabilities}
                    onChange={e => setEngForm(f => ({ ...f, headOfficeLiabilities: parseFloat(e.target.value) || 0 }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Head Office Revenue</label>
                  <input
                    type="number"
                    value={engForm.headOfficeRevenue}
                    onChange={e => setEngForm(f => ({ ...f, headOfficeRevenue: parseFloat(e.target.value) || 0 }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Head Office Expenses</label>
                  <input
                    type="number"
                    value={engForm.headOfficeExpenses}
                    onChange={e => setEngForm(f => ({ ...f, headOfficeExpenses: parseFloat(e.target.value) || 0 }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Head Office Net Profit</label>
                  <input
                    type="number"
                    value={engForm.headOfficeNetProfit}
                    onChange={e => setEngForm(f => ({ ...f, headOfficeNetProfit: parseFloat(e.target.value) || 0 }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>
                Principal Auditor Notes
              </label>
              <textarea
                rows={2}
                value={engForm.principalAuditorNotes || ''}
                onChange={e => setEngForm(f => ({ ...f, principalAuditorNotes: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                onClick={() => setShowNewEngModal(false)}
                style={{ padding: '8px 16px', background: '#f1f5f9', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleCreateEngagement}
                style={{ padding: '8px 16px', background: '#4f46e5', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Create Engagement
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: ADD / EDIT BRANCH OFFICE */}
      {showAddBranchModal && (
        <div style={{
          position: 'fixed',
          inset: 0,
          background: 'rgba(15,23,42,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 9999,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '10px',
            width: '100%',
            maxWidth: '680px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h2 style={{ margin: '0 0 16px', fontSize: '1.25rem', fontWeight: 700 }}>
              {editingBranch ? 'Edit Branch Office' : 'Register New Branch Office (Sec 143(8))'}
            </h2>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '12px', marginBottom: '14px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>
                  Branch Code *
                </label>
                <input
                  type="text"
                  placeholder="BR-MUM"
                  value={branchForm.branchCode}
                  onChange={e => setBranchForm(f => ({ ...f, branchCode: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>
                  Branch Name *
                </label>
                <input
                  type="text"
                  placeholder="Mumbai Port Logistics Branch"
                  value={branchForm.branchName}
                  onChange={e => setBranchForm(f => ({ ...f, branchName: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '12px', marginBottom: '14px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>
                  Branch Location
                </label>
                <input
                  type="text"
                  placeholder="Navi Mumbai, Maharashtra"
                  value={branchForm.branchLocation}
                  onChange={e => setBranchForm(f => ({ ...f, branchLocation: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>
                  Foreign Branch?
                </label>
                <select
                  value={branchForm.isForeignBranch ? 'true' : 'false'}
                  onChange={e => setBranchForm(f => ({ ...f, isForeignBranch: e.target.value === 'true' }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                >
                  <option value="false">Domestic (India)</option>
                  <option value="true">Foreign Branch</option>
                </select>
              </div>
            </div>

            <div style={{ background: '#f8fafc', padding: '14px', borderRadius: '6px', border: '1px solid #e2e8f0', marginBottom: '16px' }}>
              <div style={{ fontWeight: 600, fontSize: '0.85rem', marginBottom: '10px', color: '#0f172a' }}>
                Independent Branch Auditor Details (Section 143(8))
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '12px', marginBottom: '10px' }}>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Branch Auditor Firm Name</label>
                  <input
                    type="text"
                    placeholder="Desai & Associates"
                    value={branchForm.auditorFirmName}
                    onChange={e => setBranchForm(f => ({ ...f, auditorFirmName: e.target.value }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Firm Reg. Number (FRN)</label>
                  <input
                    type="text"
                    placeholder="104562W"
                    value={branchForm.auditorFirmRegistrationNumber}
                    onChange={e => setBranchForm(f => ({ ...f, auditorFirmRegistrationNumber: e.target.value }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '12px', marginBottom: '10px' }}>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Auditor Partner Name</label>
                  <input
                    type="text"
                    placeholder="CA Sunil Desai"
                    value={branchForm.auditorName}
                    onChange={e => setBranchForm(f => ({ ...f, auditorName: e.target.value }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Membership Number</label>
                  <input
                    type="text"
                    placeholder="045612"
                    value={branchForm.auditorMembershipNumber}
                    onChange={e => setBranchForm(f => ({ ...f, auditorMembershipNumber: e.target.value }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Auditor Email</label>
                  <input
                    type="email"
                    placeholder="sunil@desai-ca.in"
                    value={branchForm.auditorEmail}
                    onChange={e => setBranchForm(f => ({ ...f, auditorEmail: e.target.value }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Auditor Phone</label>
                  <input
                    type="text"
                    placeholder="9820011223"
                    value={branchForm.auditorPhone || ''}
                    onChange={e => setBranchForm(f => ({ ...f, auditorPhone: e.target.value }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>
            </div>

            {/* Optional Initial Financial Figures */}
            <div style={{ background: '#f8fafc', padding: '14px', borderRadius: '6px', border: '1px solid #e2e8f0', marginBottom: '16px' }}>
              <div style={{ fontWeight: 600, fontSize: '0.85rem', marginBottom: '10px', color: '#0f172a' }}>
                Branch Financial Returns (Can also be submitted by branch auditor via portal)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px', marginBottom: '10px' }}>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Branch Assets (INR)</label>
                  <input
                    type="number"
                    value={branchForm.totalAssets}
                    onChange={e => setBranchForm(f => ({ ...f, totalAssets: parseFloat(e.target.value) || 0 }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Branch Revenue (INR)</label>
                  <input
                    type="number"
                    value={branchForm.totalRevenue}
                    onChange={e => setBranchForm(f => ({ ...f, totalRevenue: parseFloat(e.target.value) || 0 }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Net Profit (INR)</label>
                  <input
                    type="number"
                    value={branchForm.netProfit}
                    onChange={e => setBranchForm(f => ({ ...f, netProfit: parseFloat(e.target.value) || 0 }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Report Type</label>
                  <select
                    value={branchForm.branchReportType}
                    onChange={e => setBranchForm(f => ({ ...f, branchReportType: e.target.value }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  >
                    <option value="Clean">Clean (Unmodified)</option>
                    <option value="Qualified">Qualified</option>
                    <option value="Adverse">Adverse</option>
                    <option value="Disclaimer">Disclaimer</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Has Qualifications?</label>
                  <select
                    value={branchForm.hasQualifications ? 'true' : 'false'}
                    onChange={e => setBranchForm(f => ({ ...f, hasQualifications: e.target.value === 'true' }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  >
                    <option value="false">No (Unmodified)</option>
                    <option value="true">Yes (Disclose in SA 600)</option>
                  </select>
                </div>
              </div>

              {branchForm.hasQualifications && (
                <div style={{ marginTop: '10px' }}>
                  <label style={{ fontSize: '0.75rem', color: '#b91c1c', fontWeight: 600 }}>Qualification Summary (disclosed in SA 600 Other Matter)</label>
                  <textarea
                    rows={2}
                    value={branchForm.qualificationsSummary || ''}
                    onChange={e => setBranchForm(f => ({ ...f, qualificationsSummary: e.target.value }))}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #fca5a5' }}
                  />
                </div>
              )}
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                onClick={() => { setShowAddBranchModal(false); setEditingBranch(null); }}
                style={{ padding: '8px 16px', background: '#f1f5f9', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleSaveBranch}
                style={{ padding: '8px 16px', background: '#4f46e5', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                {editingBranch ? 'Update Branch' : 'Add Branch'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 3: REVIEW BRANCH AUDIT SUBMISSION */}
      {reviewingBranch && (
        <div style={{
          position: 'fixed',
          inset: 0,
          background: 'rgba(15,23,42,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 9999,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '10px',
            width: '100%',
            maxWidth: '540px',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h2 style={{ margin: '0 0 12px', fontSize: '1.25rem', fontWeight: 700 }}>
              Principal Auditor Review: {reviewingBranch.branchName}
            </h2>
            <p className="muted" style={{ margin: '0 0 16px', fontSize: '0.84rem' }}>
              Verify certified branch auditor report under SA 600 procedures before incorporating into consolidated numbers.
            </p>

            <div style={{ background: '#f8fafc', padding: '12px', borderRadius: '6px', marginBottom: '16px', fontSize: '0.84rem' }}>
              <div><strong>Auditor Firm:</strong> {reviewingBranch.auditorFirmName || '—'}</div>
              <div><strong>Assets:</strong> {fmtInr(reviewingBranch.totalAssets)} · <strong>Revenue:</strong> {fmtInr(reviewingBranch.totalRevenue)}</div>
              <div><strong>Report:</strong> {reviewingBranch.branchReportType} {reviewingBranch.hasQualifications ? '⚠️ Qualified' : '✓ Clean'}</div>
              {reviewingBranch.qualificationsSummary && (
                <div style={{ marginTop: '4px', color: '#b91c1c' }}><strong>Notes:</strong> {reviewingBranch.qualificationsSummary}</div>
              )}
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ fontSize: '0.8rem', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Review Decision
              </label>
              <div style={{ display: 'flex', gap: '12px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer' }}>
                  <input
                    type="radio"
                    name="decision"
                    value="accept"
                    checked={reviewDecision === 'accept'}
                    onChange={() => setReviewDecision('accept')}
                  />
                  <span style={{ color: '#15803d', fontWeight: 600 }}>Accept (Include in Consolidation)</span>
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', cursor: 'pointer' }}>
                  <input
                    type="radio"
                    name="decision"
                    value="reject"
                    checked={reviewDecision === 'reject'}
                    onChange={() => setReviewDecision('reject')}
                  />
                  <span style={{ color: '#b91c1c', fontWeight: 600 }}>Reject / Request Clarification</span>
                </label>
              </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ fontSize: '0.8rem', fontWeight: 600, display: 'block', marginBottom: '4px' }}>
                Principal Auditor Review Notes / Queries
              </label>
              <textarea
                rows={3}
                placeholder="Certified branch returns and CARO memo received and accepted without modification."
                value={reviewNotes}
                onChange={e => setReviewNotes(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                onClick={() => setReviewingBranch(null)}
                style={{ padding: '8px 16px', background: '#f1f5f9', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleSubmitReview}
                style={{
                  padding: '8px 16px',
                  background: reviewDecision === 'accept' ? '#10b981' : '#ef4444',
                  color: '#fff',
                  border: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  cursor: 'pointer'
                }}
              >
                Submit Review
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 4: SIMULATE EXTERNAL BRANCH AUDITOR PORTAL */}
      {showPortalSimModal && (
        <div style={{
          position: 'fixed',
          inset: 0,
          background: 'rgba(15,23,42,0.6)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 9999,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '10px',
            width: '100%',
            maxWidth: '600px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
              <h2 style={{ margin: 0, fontSize: '1.2rem', fontWeight: 700 }}>
                Delegated Branch Auditor Portal (Simulator)
              </h2>
              <span style={{ background: '#e0f2fe', color: '#0369a1', fontSize: '0.72rem', fontWeight: 700, padding: '2px 8px', borderRadius: '4px' }}>
                External Token Mode {simToken ? `(${simToken.slice(0, 8)}…)` : ''}
              </span>
            </div>
            <p className="muted" style={{ margin: '0 0 16px', fontSize: '0.82rem' }}>
              This dialog simulates what the external branch auditor accesses and submits via their temporary delegated access token without full CA firm credentials.
            </p>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Total Branch Assets (INR)</label>
                <input
                  type="number"
                  value={portalSimForm.totalAssets}
                  onChange={e => setPortalSimForm(f => ({ ...f, totalAssets: parseFloat(e.target.value) || 0 }))}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Total Branch Revenue (INR)</label>
                <input
                  type="number"
                  value={portalSimForm.totalRevenue}
                  onChange={e => setPortalSimForm(f => ({ ...f, totalRevenue: parseFloat(e.target.value) || 0 }))}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Total Liabilities (INR)</label>
                <input
                  type="number"
                  value={portalSimForm.totalLiabilities}
                  onChange={e => setPortalSimForm(f => ({ ...f, totalLiabilities: parseFloat(e.target.value) || 0 }))}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Net Profit (INR)</label>
                <input
                  type="number"
                  value={portalSimForm.netProfit}
                  onChange={e => setPortalSimForm(f => ({ ...f, netProfit: parseFloat(e.target.value) || 0 }))}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Branch Auditor Report Type</label>
                <select
                  value={portalSimForm.branchReportType}
                  onChange={e => setPortalSimForm(f => ({ ...f, branchReportType: e.target.value }))}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="Clean">Clean (Unmodified)</option>
                  <option value="Qualified">Qualified</option>
                  <option value="Adverse">Adverse</option>
                  <option value="Disclaimer">Disclaimer</option>
                </select>
              </div>
              <div>
                <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Branch Audit Report Ref</label>
                <input
                  type="text"
                  value={portalSimForm.branchAuditReportRef || ''}
                  onChange={e => setPortalSimForm(f => ({ ...f, branchAuditReportRef: e.target.value }))}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>

            <div style={{ background: '#f8fafc', padding: '12px', borderRadius: '6px', border: '1px solid #e2e8f0', marginBottom: '14px', fontSize: '0.78rem' }}>
              <div style={{ fontWeight: 600, color: '#0f172a', marginBottom: '6px' }}>SA 600 Questionnaire Confirmations:</div>
              <label style={{ display: 'flex', alignItems: 'center', gap: '6px', marginBottom: '4px' }}>
                <input
                  type="checkbox"
                  checked={portalSimForm.accountingPoliciesConsistent}
                  onChange={e => setPortalSimForm(f => ({ ...f, accountingPoliciesConsistent: e.target.checked }))}
                />
                Accounting policies applied consistently with Head Office
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: '6px', marginBottom: '4px' }}>
                <input
                  type="checkbox"
                  checked={portalSimForm.relatedPartyTransactionsDisclosed}
                  onChange={e => setPortalSimForm(f => ({ ...f, relatedPartyTransactionsDisclosed: e.target.checked }))}
                />
                All branch related party transactions identified and disclosed
              </label>
              <label style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                <input
                  type="checkbox"
                  checked={portalSimForm.internalControlsCommensurate}
                  onChange={e => setPortalSimForm(f => ({ ...f, internalControlsCommensurate: e.target.checked }))}
                />
                Internal financial controls are commensurate with branch size
              </label>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                onClick={() => setShowPortalSimModal(false)}
                style={{ padding: '8px 16px', background: '#f1f5f9', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Close
              </button>
              <button
                onClick={handleSimulatePortalSubmission}
                style={{ padding: '8px 16px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Submit Returns as Branch Auditor
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
