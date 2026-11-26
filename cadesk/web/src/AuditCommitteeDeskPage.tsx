import { useCallback, useEffect, useRef, useState } from 'react'
import { toError,
  type AuditCommitteeEngagementDto,
  type AuditCommitteeSummaryDto,
  type InternalControlDeficiencyDto,
  type SignificantAuditMatterDto,
  type UncorrectedMisstatementDto,
  type SaveCommitteeEngagementRequest,
  type SaveDeficiencyRequest,
  type SaveSignificantMatterRequest,
  type SaveUncorrectedMisstatementRequest,
  type Client,
  auditCommitteeApi,
  api
} from './api'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0)

type ClientPage = {
  items: Client[]
  total: number
}

export function AuditCommitteeDeskPage({ can }: { can: (p: string) => boolean }) {
  const [engagements, setEngagements] = useState<AuditCommitteeEngagementDto[]>([])
  const [summary, setSummary] = useState<AuditCommitteeSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [toastMsg, setToastMsg] = useState<string | null>(null)

  // Filters
  const [filterFy, setFilterFy] = useState('2024-25')
  const [filterStatus, setFilterStatus] = useState('all')

  // Selected engagement & tabs
  const [selectedEngagement, setSelectedEngagement] = useState<AuditCommitteeEngagementDto | null>(null)
  const [activeTab, setActiveTab] = useState<'deficiencies' | 'matters' | 'misstatements' | 'packs'>('deficiencies')
  const [packSubTab, setPackSubTab] = useState<'sa265' | 'sa260'>('sa265')

  // Modals
  const [showNewEngModal, setShowNewEngModal] = useState(false)
  const [showAddDefModal, setShowAddDefModal] = useState(false)
  const [editingDef, setEditingDef] = useState<InternalControlDeficiencyDto | null>(null)

  const [showAddMatterModal, setShowAddMatterModal] = useState(false)
  const [editingMatter, setEditingMatter] = useState<SignificantAuditMatterDto | null>(null)

  const [showAddMisModal, setShowAddMisModal] = useState(false)
  const [editingMis, setEditingMis] = useState<UncorrectedMisstatementDto | null>(null)

  const [showSignOffModal, setShowSignOffModal] = useState(false)
  const [signOffMinutes, setSignOffMinutes] = useState('')

  // Form states
  const [engForm, setEngForm] = useState<SaveCommitteeEngagementRequest>({
    clientId: '',
    auditEngagementId: null,
    financialYear: '2024-25',
    title: 'Audit Committee Statutory Governance Review Meeting FY 2024-25',
    meetingType: 'Pre-Issuance',
    meetingDate: undefined,
    meetingLocation: 'Board Room, Corporate Headquarters',
    committeeChairName: 'Dr. K. S. Rao (Independent Director & Audit Committee Chair)',
    leadPartnerName: 'CA Rajesh Verma',
    meetingMinutes: ''
  })

  const [defForm, setDefForm] = useState<SaveDeficiencyRequest>({
    deficiencyCode: '',
    title: '',
    processArea: 'Revenue Recognition',
    deficiencyType: 'DesignDeficiency',
    severity: 'SignificantDeficiency',
    description: '',
    potentialFinancialEffect: '',
    estimatedRiskAmount: 0,
    auditorRecommendation: '',
    managementResponse: '',
    remediationTargetDate: undefined,
    status: 'Identified'
  })

  const [matterForm, setMatterForm] = useState<SaveSignificantMatterRequest>({
    matterCode: '',
    subject: '',
    category: 'KeyAuditMatter',
    details: '',
    auditorAssessment: '',
    auditCommitteeFeedback: ''
  })

  const [misForm, setMisForm] = useState<SaveUncorrectedMisstatementRequest>({
    accountDescription: '',
    misstatementAmount: 0,
    nature: 'Factual',
    isMaterial: false,
    managementJustificationForNonAdjustment: ''
  })

  const selectedEngagementIdRef = useRef<string | null>(null)
  useEffect(() => {
    selectedEngagementIdRef.current = selectedEngagement?.id ?? null
  }, [selectedEngagement])

  const showToast = (msg: string) => {
    setToastMsg(msg)
    setTimeout(() => setToastMsg(null), 4000)
  }

  const loadClients = useCallback(async () => {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      setClients(res.items || [])
      if (res.items && res.items.length > 0) {
        setEngForm(f => (f.clientId ? f : { ...f, clientId: res.items[0].id }))
      }
    } catch (caught) {
      const err = toError(caught)
      console.error('Failed to load clients', err)
    }
  }, [])

  const loadData = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [sumRes, engRes] = await Promise.all([
        auditCommitteeApi.getSummary(),
        auditCommitteeApi.getEngagements(undefined, filterFy !== 'all' ? filterFy : undefined, filterStatus !== 'all' ? filterStatus : undefined)
      ])
      setSummary(sumRes)
      setEngagements(engRes)

      if (engRes.length > 0) {
        const prevId = selectedEngagementIdRef.current
        const next = (prevId && engRes.find(e => e.id === prevId)) || engRes[0]
        selectedEngagementIdRef.current = next.id
        setSelectedEngagement(next)
      } else {
        selectedEngagementIdRef.current = null
        setSelectedEngagement(null)
      }
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to load audit committee engagements.')
    } finally {
      setLoading(false)
    }
  }, [filterFy, filterStatus])

  useEffect(() => {
    loadData()
    loadClients()
  }, [loadData, loadClients])

  const handleCreateEngagement = async () => {
    try {
      const created = await auditCommitteeApi.createEngagement(engForm)
      showToast('Audit Committee engagement created successfully.')
      setShowNewEngModal(false)
      await loadData()
      setSelectedEngagement(created)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to create engagement.')
    }
  }

  const handleSaveDeficiency = async () => {
    if (!selectedEngagement) return
    try {
      if (editingDef) {
        await auditCommitteeApi.updateDeficiency(selectedEngagement.id, editingDef.id, defForm)
        showToast('Internal control deficiency updated.')
      } else {
        await auditCommitteeApi.addDeficiency(selectedEngagement.id, defForm)
        showToast('Internal control deficiency added.')
      }
      setShowAddDefModal(false)
      setEditingDef(null)
      await loadData()
      const fresh = await auditCommitteeApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save deficiency.')
    }
  }

  const handleDeleteDeficiency = async (defId: string) => {
    if (!selectedEngagement) return
    if (!confirm('Remove this internal control deficiency?')) return
    try {
      await auditCommitteeApi.deleteDeficiency(selectedEngagement.id, defId)
      showToast('Deficiency removed.')
      await loadData()
      const fresh = await auditCommitteeApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete deficiency.')
    }
  }

  const handleSaveMatter = async () => {
    if (!selectedEngagement) return
    try {
      if (editingMatter) {
        await auditCommitteeApi.updateMatter(selectedEngagement.id, editingMatter.id, matterForm)
        showToast('Significant audit matter updated.')
      } else {
        await auditCommitteeApi.addMatter(selectedEngagement.id, matterForm)
        showToast('Significant audit matter added.')
      }
      setShowAddMatterModal(false)
      setEditingMatter(null)
      await loadData()
      const fresh = await auditCommitteeApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save audit matter.')
    }
  }

  const handleDeleteMatter = async (matterId: string) => {
    if (!selectedEngagement) return
    if (!confirm('Remove this significant audit matter?')) return
    try {
      await auditCommitteeApi.deleteMatter(selectedEngagement.id, matterId)
      showToast('Significant audit matter removed.')
      await loadData()
      const fresh = await auditCommitteeApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete audit matter.')
    }
  }

  const handleSaveMisstatement = async () => {
    if (!selectedEngagement) return
    try {
      if (editingMis) {
        await auditCommitteeApi.updateMisstatement(selectedEngagement.id, editingMis.id, misForm)
        showToast('Uncorrected misstatement updated.')
      } else {
        await auditCommitteeApi.addMisstatement(selectedEngagement.id, misForm)
        showToast('Uncorrected misstatement recorded.')
      }
      setShowAddMisModal(false)
      setEditingMis(null)
      await loadData()
      const fresh = await auditCommitteeApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save misstatement.')
    }
  }

  const handleDeleteMisstatement = async (misId: string) => {
    if (!selectedEngagement) return
    if (!confirm('Remove this uncorrected misstatement?')) return
    try {
      await auditCommitteeApi.deleteMisstatement(selectedEngagement.id, misId)
      showToast('Uncorrected misstatement removed.')
      await loadData()
      const fresh = await auditCommitteeApi.getEngagementById(selectedEngagement.id)
      setSelectedEngagement(fresh)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete misstatement.')
    }
  }

  const handleGeneratePack = async () => {
    if (!selectedEngagement) return
    try {
      const res = await auditCommitteeApi.generatePack(selectedEngagement.id)
      setSelectedEngagement(res)
      setActiveTab('packs')
      showToast('SA 260 Governance Pack & SA 265 Internal Control Letter generated!')
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to generate pack.')
    }
  }

  const handleSignOff = async () => {
    if (!selectedEngagement) return
    try {
      const res = await auditCommitteeApi.signOff(selectedEngagement.id, { minutes: signOffMinutes })
      setSelectedEngagement(res)
      setShowSignOffModal(false)
      showToast('Audit Committee engagement signed off and formally archived pursuant to Section 177.')
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to sign off engagement.')
    }
  }

  const copyText = (text?: string | null, label: string = 'Document') => {
    if (!text) return
    navigator.clipboard.writeText(text)
    showToast(`${label} copied to clipboard!`)
  }

  const openNewDefModal = () => {
    setEditingDef(null)
    setDefForm({
      deficiencyCode: `ICD-0${(selectedEngagement?.deficiencies?.length || 0) + 1}`,
      title: '',
      processArea: 'Revenue Recognition',
      deficiencyType: 'DesignDeficiency',
      severity: 'SignificantDeficiency',
      description: '',
      potentialFinancialEffect: '',
      estimatedRiskAmount: 0,
      auditorRecommendation: '',
      managementResponse: '',
      remediationTargetDate: undefined,
      status: 'Identified'
    })
    setShowAddDefModal(true)
  }

  const openEditDefModal = (d: InternalControlDeficiencyDto) => {
    setEditingDef(d)
    setDefForm({
      deficiencyCode: d.deficiencyCode,
      title: d.title,
      processArea: d.processArea,
      deficiencyType: d.deficiencyType,
      severity: d.severity,
      description: d.description,
      potentialFinancialEffect: d.potentialFinancialEffect,
      estimatedRiskAmount: d.estimatedRiskAmount || 0,
      auditorRecommendation: d.auditorRecommendation,
      managementResponse: d.managementResponse || '',
      remediationTargetDate: d.remediationTargetDate ? d.remediationTargetDate.split('T')[0] : undefined,
      status: d.status
    })
    setShowAddDefModal(true)
  }

  const openNewMatterModal = () => {
    setEditingMatter(null)
    setMatterForm({
      matterCode: `SAM-0${(selectedEngagement?.significantMatters?.length || 0) + 1}`,
      subject: '',
      category: 'KeyAuditMatter',
      details: '',
      auditorAssessment: '',
      auditCommitteeFeedback: ''
    })
    setShowAddMatterModal(true)
  }

  const openEditMatterModal = (m: SignificantAuditMatterDto) => {
    setEditingMatter(m)
    setMatterForm({
      matterCode: m.matterCode,
      subject: m.subject,
      category: m.category,
      details: m.details,
      auditorAssessment: m.auditorAssessment,
      auditCommitteeFeedback: m.auditCommitteeFeedback || ''
    })
    setShowAddMatterModal(true)
  }

  const openNewMisModal = () => {
    setEditingMis(null)
    setMisForm({
      accountDescription: '',
      misstatementAmount: 0,
      nature: 'Factual',
      isMaterial: false,
      managementJustificationForNonAdjustment: ''
    })
    setShowAddMisModal(true)
  }

  const openEditMisModal = (m: UncorrectedMisstatementDto) => {
    setEditingMis(m)
    setMisForm({
      accountDescription: m.accountDescription,
      misstatementAmount: m.misstatementAmount,
      nature: m.nature,
      isMaterial: m.isMaterial,
      managementJustificationForNonAdjustment: m.managementJustificationForNonAdjustment || ''
    })
    setShowAddMisModal(true)
  }

  return (
    <div className="desk-container" style={{ padding: '24px 32px' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <h1 style={{ margin: 0, fontSize: '1.75rem', fontWeight: 700, letterSpacing: '-0.02em' }}>
              Audit Committee & Board Governance Desk
            </h1>
            <span style={{
              background: 'var(--purple-bg)',
              color: '#6d28d9',
              fontSize: '0.75rem',
              fontWeight: 700,
              padding: '3px 8px',
              borderRadius: '6px',
              border: '1px solid #ddd6fe'
            }}>
              Sprint 34
            </span>
            {loading && <span style={{ fontSize: '0.8rem', color: 'var(--muted)' }}>Refreshing...</span>}
          </div>
          <p className="muted" style={{ margin: '6px 0 0', fontSize: '0.95rem' }}>
            Statutory Governance & Two-Way Communication with Those Charged with Governance (TCWG) under <strong>Companies Act, 2013 Sec 177</strong> & <strong>ICAI SA 260 / SA 265</strong>
          </p>
          <div style={{ display: 'flex', gap: '8px', marginTop: '10px', flexWrap: 'wrap' }}>
            <span style={{ fontSize: '0.78rem', background: 'var(--card-subtle)', color: 'var(--text-secondary)', padding: '2px 8px', borderRadius: '4px' }}>
              ⚖️ Section 177 Audit Committee Mandate
            </span>
            <span style={{ fontSize: '0.78rem', background: 'var(--card-subtle)', color: 'var(--text-secondary)', padding: '2px 8px', borderRadius: '4px' }}>
              🛡️ SA 260 Auditor Independence Statement
            </span>
            <span style={{ fontSize: '0.78rem', background: 'var(--card-subtle)', color: 'var(--text-secondary)', padding: '2px 8px', borderRadius: '4px' }}>
              ⚠️ SA 265 Significant Internal Control Deficiencies
            </span>
            <span style={{ fontSize: '0.78rem', background: 'var(--card-subtle)', color: 'var(--text-secondary)', padding: '2px 8px', borderRadius: '4px' }}>
              📑 SA 450 Uncorrected Misstatements Evaluation
            </span>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          {can('auditcommittee.manage') && (
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
              <span>+</span> New Committee Engagement
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
          background: 'var(--error-bg)',
          border: '1px solid var(--error-border)',
          color: 'var(--error)',
          padding: '12px 16px',
          borderRadius: '6px',
          marginBottom: '18px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span>{error}</span>
          <button onClick={() => setError(null)} style={{ background: 'none', border: 'none', color: 'var(--error)', cursor: 'pointer', fontWeight: 700 }}>✕</button>
        </div>
      )}

      {/* KPI Cards Row */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))', gap: '16px', marginBottom: '24px' }}>
        <div style={{ background: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: 'var(--muted)', fontWeight: 600 }}>Committee Engagements</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--text)', marginTop: '4px' }}>
            {summary?.totalEngagements ?? 0}
          </div>
          <div style={{ fontSize: '0.75rem', color: '#10b981', marginTop: '4px' }}>
            {summary?.signedOffCount ?? 0} Signed Off · {summary?.packGeneratedCount ?? 0} Pack Ready
          </div>
        </div>

        <div style={{ background: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: 'var(--muted)', fontWeight: 600 }}>Internal Control Deficiencies</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--text)', marginTop: '4px' }}>
            {summary?.totalDeficiencies ?? 0}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--muted)', marginTop: '4px' }}>
            Across all financial process areas
          </div>
        </div>

        <div style={{ background: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: 'var(--muted)', fontWeight: 600 }}>Significant Deficiencies (SA 265)</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: (summary?.totalSignificantDeficiencies ?? 0) > 0 ? 'var(--error)' : 'var(--success)', marginTop: '4px' }}>
            {summary?.totalSignificantDeficiencies ?? 0}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--muted)', marginTop: '4px' }}>
            Mandatory written reporting to TCWG
          </div>
        </div>

        <div style={{ background: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: 'var(--muted)', fontWeight: 600 }}>Est. Control Risk Exposure</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--warning)', marginTop: '4px' }}>
            {fmtInr(summary?.totalEstimatedRiskAmount ?? 0)}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--muted)', marginTop: '4px' }}>
            Quantified control risk aggregate
          </div>
        </div>

        <div style={{ background: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
          <div style={{ fontSize: '0.8rem', color: 'var(--muted)', fontWeight: 600 }}>Uncorrected Misstatements (SA 450)</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: 'var(--accent)', marginTop: '4px' }}>
            {fmtInr(summary?.totalUncorrectedMisstatementValue ?? 0)}
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--muted)', marginTop: '4px' }}>
            {summary?.totalUncorrectedMisstatements ?? 0} unadjusted audit items
          </div>
        </div>
      </div>

      {/* Engagement Selection & Filters Strip */}
      <div style={{
        background: 'var(--card)',
        padding: '16px 20px',
        borderRadius: '8px',
        border: '1px solid var(--border)',
        marginBottom: '24px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: '12px'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px', flexWrap: 'wrap' }}>
          <div>
            <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>
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
                border: '1px solid var(--border-subtle)',
                fontSize: '0.88rem',
                minWidth: '340px',
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
            <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>
              Financial Year
            </label>
            <select
              value={filterFy}
              onChange={e => setFilterFy(e.target.value)}
              style={{ padding: '7px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '0.88rem' }}
            >
              <option value="all">All FYs</option>
              <option value="2024-25">2024-25</option>
              <option value="2023-24">2023-24</option>
            </select>
          </div>

          <div>
            <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>
              Status
            </label>
            <select
              value={filterStatus}
              onChange={e => setFilterStatus(e.target.value)}
              style={{ padding: '7px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '0.88rem' }}
            >
              <option value="all">All Statuses</option>
              <option value="Draft">Draft</option>
              <option value="PackGenerated">Pack Generated</option>
              <option value="SignedOff">Signed Off</option>
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
              background: selectedEngagement.status === 'SignedOff' ? 'var(--success-bg)' :
                          selectedEngagement.status === 'PackGenerated' ? '#ede9fe' : '#f1f5f9',
              color: selectedEngagement.status === 'SignedOff' ? 'var(--success-text)' :
                     selectedEngagement.status === 'PackGenerated' ? '#6d28d9' : '#475569'
            }}>
              {selectedEngagement.status}
            </span>

            {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
              <button
                onClick={handleGeneratePack}
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
                ⚡ Generate Governance Pack & SA 265 Letter
              </button>
            )}

            {can('auditcommittee.signoff') && selectedEngagement.status !== 'SignedOff' && (
              <button
                onClick={() => {
                  setSignOffMinutes(selectedEngagement.meetingMinutes || '')
                  setShowSignOffModal(true)
                }}
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
                ✓ Sign Off Meeting
              </button>
            )}
          </div>
        )}
      </div>

      {/* Main Workspace */}
      {selectedEngagement ? (
        <div style={{ background: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', boxShadow: '0 1px 3px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          {/* Tabs navigation */}
          <div style={{
            display: 'flex',
            borderBottom: '1px solid var(--border)',
            background: 'var(--card-subtle)',
            padding: '0 16px'
          }}>
            <button
              onClick={() => setActiveTab('deficiencies')}
              style={{
                padding: '14px 20px',
                border: 'none',
                background: 'none',
                fontWeight: 600,
                fontSize: '0.88rem',
                cursor: 'pointer',
                color: activeTab === 'deficiencies' ? 'var(--accent)' : 'var(--muted)',
                borderBottom: activeTab === 'deficiencies' ? '2px solid var(--accent)' : '2px solid transparent'
              }}
            >
              ⚠️ Internal Control Deficiencies (SA 265) ({selectedEngagement.deficiencies?.length || 0})
            </button>
            <button
              onClick={() => setActiveTab('matters')}
              style={{
                padding: '14px 20px',
                border: 'none',
                background: 'none',
                fontWeight: 600,
                fontSize: '0.88rem',
                cursor: 'pointer',
                color: activeTab === 'matters' ? 'var(--accent)' : 'var(--muted)',
                borderBottom: activeTab === 'matters' ? '2px solid var(--accent)' : '2px solid transparent'
              }}
            >
              📋 Significant Audit Matters & KAM (SA 260) ({selectedEngagement.significantMatters?.length || 0})
            </button>
            <button
              onClick={() => setActiveTab('misstatements')}
              style={{
                padding: '14px 20px',
                border: 'none',
                background: 'none',
                fontWeight: 600,
                fontSize: '0.88rem',
                cursor: 'pointer',
                color: activeTab === 'misstatements' ? 'var(--accent)' : 'var(--muted)',
                borderBottom: activeTab === 'misstatements' ? '2px solid var(--accent)' : '2px solid transparent'
              }}
            >
              ⚖️ Uncorrected Misstatements (SA 450) ({selectedEngagement.uncorrectedMisstatements?.length || 0})
            </button>
            <button
              onClick={() => setActiveTab('packs')}
              style={{
                padding: '14px 20px',
                border: 'none',
                background: 'none',
                fontWeight: 600,
                fontSize: '0.88rem',
                cursor: 'pointer',
                color: activeTab === 'packs' ? 'var(--accent)' : 'var(--muted)',
                borderBottom: activeTab === 'packs' ? '2px solid var(--accent)' : '2px solid transparent'
              }}
            >
              📑 Governance Pack & SA 265 Letter Drafts
            </button>
          </div>

          {/* TAB 1: DEFICIENCIES (SA 265) */}
          {activeTab === 'deficiencies' && (
            <div style={{ padding: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <div>
                  <h3 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>
                    Internal Control Deficiencies (ICAI SA 265 & Sec 177(4)(vii))
                  </h3>
                  <p className="muted" style={{ margin: '4px 0 0', fontSize: '0.82rem' }}>
                    Communicating significant deficiencies in the design or operating effectiveness of internal financial controls to TCWG and management.
                  </p>
                </div>

                {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                  <button
                    onClick={openNewDefModal}
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
                    + Add Deficiency
                  </button>
                )}
              </div>

              {(!selectedEngagement.deficiencies || selectedEngagement.deficiencies.length === 0) ? (
                <div style={{ textAlign: 'center', padding: '48px 24px', color: 'var(--muted)' }}>
                  <div style={{ fontSize: '2.5rem', marginBottom: '12px' }}>🛡️</div>
                  <div style={{ fontWeight: 600, fontSize: '1rem', color: 'var(--text)' }}>No Deficiencies Recorded Yet</div>
                  <p style={{ maxWidth: '420px', margin: '8px auto 16px', fontSize: '0.85rem' }}>
                    Record identified deficiencies in internal control, evaluate whether they constitute significant deficiencies, and draft auditor recommendations.
                  </p>
                  {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                    <button
                      onClick={openNewDefModal}
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
                      + Add First Deficiency
                    </button>
                  )}
                </div>
              ) : (
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.84rem' }}>
                    <thead>
                      <tr style={{ background: 'var(--card-subtle)', borderBottom: '2px solid var(--border)', textAlign: 'left', color: 'var(--text-secondary)' }}>
                        <th style={{ padding: '10px 12px' }}>Code & Title</th>
                        <th style={{ padding: '10px 12px' }}>Process Area</th>
                        <th style={{ padding: '10px 12px' }}>Nature</th>
                        <th style={{ padding: '10px 12px' }}>Severity</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>Est. Risk Amount</th>
                        <th style={{ padding: '10px 12px' }}>Status</th>
                        <th style={{ padding: '10px 12px', textAlign: 'center' }}>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedEngagement.deficiencies.map(d => (
                        <tr key={d.id} style={{ borderBottom: '1px solid var(--border)' }}>
                          <td style={{ padding: '12px' }}>
                            <div style={{ fontWeight: 600, color: 'var(--text)' }}>{d.title}</div>
                            <div style={{ fontSize: '0.75rem', color: 'var(--muted)' }}>Ref: {d.deficiencyCode}</div>
                          </td>
                          <td style={{ padding: '12px' }}>
                            <span style={{ background: 'var(--card-subtle)', color: 'var(--text-secondary)', padding: '2px 8px', borderRadius: '4px', fontSize: '0.75rem', fontWeight: 600 }}>
                              {d.processArea}
                            </span>
                          </td>
                          <td style={{ padding: '12px' }}>
                            <div style={{ fontSize: '0.78rem' }}>
                              {d.deficiencyType === 'DesignDeficiency' ? 'Design Defect' : 'Operating Ineffectiveness'}
                            </div>
                          </td>
                          <td style={{ padding: '12px' }}>
                            <span style={{
                              fontSize: '0.72rem',
                              fontWeight: 700,
                              padding: '2px 8px',
                              borderRadius: '4px',
                              background: d.severity === 'SignificantDeficiency' ? 'var(--error-bg)' : 'var(--warning-bg)',
                              color: d.severity === 'SignificantDeficiency' ? 'var(--error)' : 'var(--warning)'
                            }}>
                              {d.severity === 'SignificantDeficiency' ? '⚠️ SIGNIFICANT DEFICIENCY' : 'Other Deficiency'}
                            </span>
                          </td>
                          <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: 'var(--text)' }}>
                            {d.estimatedRiskAmount ? fmtInr(d.estimatedRiskAmount) : '—'}
                          </td>
                          <td style={{ padding: '12px' }}>
                            <span style={{
                              fontSize: '0.72rem',
                              fontWeight: 600,
                              padding: '2px 8px',
                              borderRadius: '4px',
                              background: d.status === 'Remediated' ? 'var(--success-bg)' : 'var(--card-subtle)',
                              color: d.status === 'Remediated' ? 'var(--success)' : 'var(--text-secondary)'
                            }}>
                              {d.status}
                            </span>
                          </td>
                          <td style={{ padding: '12px', textAlign: 'center' }}>
                            <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                              {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                                <button
                                  onClick={() => openEditDefModal(d)}
                                  title="Edit deficiency"
                                  style={{
                                    background: 'var(--card-subtle)',
                                    border: '1px solid var(--border-subtle)',
                                    borderRadius: '4px',
                                    padding: '4px 8px',
                                    fontSize: '0.72rem',
                                    cursor: 'pointer'
                                  }}
                                >
                                  ✏️ Edit
                                </button>
                              )}
                              {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                                <button
                                  onClick={() => handleDeleteDeficiency(d.id)}
                                  title="Remove deficiency"
                                  style={{
                                    background: 'var(--error-bg)',
                                    color: 'var(--error)',
                                    border: '1px solid var(--error-border)',
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

          {/* TAB 2: SIGNIFICANT MATTERS & INDEPENDENCE (SA 260) */}
          {activeTab === 'matters' && (
            <div style={{ padding: '24px' }}>
              {/* Auditor Independence Card */}
              <div style={{
                background: 'var(--success-bg)',
                padding: '20px',
                borderRadius: '8px',
                border: '1px solid var(--success-border)',
                marginBottom: '24px'
              }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <div>
                    <h3 style={{ margin: 0, fontSize: '1rem', color: 'var(--success-text)', fontWeight: 700 }}>
                      ✓ Statement of Auditor Independence (ICAI SA 260 & Companies Act Sec 177(4)(i))
                    </h3>
                    <p style={{ margin: '6px 0 0', fontSize: '0.84rem', color: 'var(--success-text)' }}>
                      Lead partner confirmation affirming firm independence, compliance with Section 144 non-audit service prohibitions, and elimination of threats to independence.
                    </p>
                  </div>
                  <span style={{ background: '#166534', color: '#fff', fontSize: '0.75rem', fontWeight: 700, padding: '4px 10px', borderRadius: '6px' }}>
                    Affirmed
                  </span>
                </div>
              </div>

              {/* Matters table */}
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <div>
                  <h3 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>
                    Significant Audit Findings & Key Audit Matters (SA 260)
                  </h3>
                  <p className="muted" style={{ margin: '4px 0 0', fontSize: '0.82rem' }}>
                    Qualitative accounting aspects, management consultations, audit difficulties, and preview of Key Audit Matters (KAM).
                  </p>
                </div>

                {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                  <button
                    onClick={openNewMatterModal}
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
                    + Add Significant Matter
                  </button>
                )}
              </div>

              {(!selectedEngagement.significantMatters || selectedEngagement.significantMatters.length === 0) ? (
                <div style={{ textAlign: 'center', padding: '40px 24px', background: 'var(--card-subtle)', borderRadius: '8px' }}>
                  <div style={{ fontSize: '2rem', marginBottom: '8px' }}>📋</div>
                  <div style={{ fontWeight: 600, color: 'var(--text)' }}>No Significant Audit Matters Logged</div>
                  <p style={{ margin: '6px 0 0', fontSize: '0.82rem', color: 'var(--muted)' }}>
                    Log accounting judgments, estimates, or KAM previews for Audit Committee discussion.
                  </p>
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
                  {selectedEngagement.significantMatters.map(m => (
                    <div key={m.id} style={{
                      background: 'var(--card)',
                      border: '1px solid var(--border)',
                      borderRadius: '8px',
                      padding: '16px',
                      boxShadow: '0 1px 2px rgba(0,0,0,0.03)'
                    }}>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '8px' }}>
                        <div>
                          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <span style={{ fontSize: '0.75rem', fontWeight: 700, color: 'var(--accent)', background: 'var(--purple-bg)', padding: '2px 6px', borderRadius: '4px' }}>
                              {m.matterCode}
                            </span>
                            <span style={{ fontSize: '0.75rem', background: 'var(--card-subtle)', color: 'var(--text-secondary)', padding: '2px 8px', borderRadius: '4px', fontWeight: 600 }}>
                              {m.category}
                            </span>
                            <h4 style={{ margin: 0, fontSize: '0.96rem', fontWeight: 600 }}>{m.subject}</h4>
                          </div>
                        </div>

                        {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                          <div style={{ display: 'flex', gap: '6px' }}>
                            <button
                              onClick={() => openEditMatterModal(m)}
                              style={{ background: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', borderRadius: '4px', padding: '3px 8px', fontSize: '0.72rem', cursor: 'pointer' }}
                            >
                              ✏️ Edit
                            </button>
                            <button
                              onClick={() => handleDeleteMatter(m.id)}
                              style={{ background: 'var(--error-bg)', color: 'var(--error)', border: '1px solid var(--error-border)', borderRadius: '4px', padding: '3px 8px', fontSize: '0.72rem', cursor: 'pointer' }}
                            >
                              🗑️
                            </button>
                          </div>
                        )}
                      </div>

                      <div style={{ fontSize: '0.84rem', color: 'var(--text-secondary)', marginBottom: '8px' }}>
                        <strong>Details:</strong> {m.details}
                      </div>
                      <div style={{ fontSize: '0.84rem', color: 'var(--success-text)', background: 'var(--success-bg)', padding: '8px', borderRadius: '4px', marginBottom: m.auditCommitteeFeedback ? '8px' : '0' }}>
                        <strong>Auditor Assessment:</strong> {m.auditorAssessment}
                      </div>
                      {m.auditCommitteeFeedback && (
                        <div style={{ fontSize: '0.84rem', color: 'var(--accent)', background: 'var(--accent-subtle)', padding: '8px', borderRadius: '4px' }}>
                          <strong>Committee Feedback:</strong> {m.auditCommitteeFeedback}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* TAB 3: UNCORRECTED MISSTATEMENTS (SA 450) */}
          {activeTab === 'misstatements' && (
            <div style={{ padding: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <div>
                  <h3 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>
                    Schedule of Uncorrected Misstatements (ICAI SA 450 / SA 260)
                  </h3>
                  <p className="muted" style={{ margin: '4px 0 0', fontSize: '0.82rem' }}>
                    Evaluation of aggregate unadjusted audit differences and management justifications for non-adjustment.
                  </p>
                </div>

                {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                  <button
                    onClick={openNewMisModal}
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
                    + Add Misstatement
                  </button>
                )}
              </div>

              {(!selectedEngagement.uncorrectedMisstatements || selectedEngagement.uncorrectedMisstatements.length === 0) ? (
                <div style={{ textAlign: 'center', padding: '40px 24px', background: 'var(--card-subtle)', borderRadius: '8px' }}>
                  <div style={{ fontSize: '2rem', marginBottom: '8px' }}>⚖️</div>
                  <div style={{ fontWeight: 600, color: 'var(--text)' }}>No Uncorrected Misstatements</div>
                  <p style={{ margin: '6px 0 0', fontSize: '0.82rem', color: 'var(--muted)' }}>
                    All identified audit adjustments have been passed by management in the statutory financial statements.
                  </p>
                </div>
              ) : (
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.84rem' }}>
                    <thead>
                      <tr style={{ background: 'var(--card-subtle)', borderBottom: '2px solid var(--border)', textAlign: 'left', color: 'var(--text-secondary)' }}>
                        <th style={{ padding: '10px 12px' }}>Account / Description</th>
                        <th style={{ padding: '10px 12px', textAlign: 'right' }}>Amount (INR)</th>
                        <th style={{ padding: '10px 12px' }}>Nature</th>
                        <th style={{ padding: '10px 12px' }}>Material?</th>
                        <th style={{ padding: '10px 12px' }}>Management Justification</th>
                        <th style={{ padding: '10px 12px', textAlign: 'center' }}>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {selectedEngagement.uncorrectedMisstatements.map(m => (
                        <tr key={m.id} style={{ borderBottom: '1px solid var(--border)' }}>
                          <td style={{ padding: '12px', fontWeight: 600 }}>{m.accountDescription}</td>
                          <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: 'var(--text)' }}>
                            {fmtInr(m.misstatementAmount)}
                          </td>
                          <td style={{ padding: '12px' }}>
                            <span style={{ background: 'var(--card-subtle)', padding: '2px 8px', borderRadius: '4px', fontSize: '0.75rem', fontWeight: 600 }}>
                              {m.nature}
                            </span>
                          </td>
                          <td style={{ padding: '12px' }}>
                            <span style={{
                              fontSize: '0.72rem',
                              fontWeight: 700,
                              padding: '2px 8px',
                              borderRadius: '4px',
                              background: m.isMaterial ? 'var(--error-bg)' : 'var(--success-bg)',
                              color: m.isMaterial ? 'var(--error)' : 'var(--success)'
                            }}>
                              {m.isMaterial ? 'YES (MATERIAL)' : 'No (Immaterial)'}
                            </span>
                          </td>
                          <td style={{ padding: '12px', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                            {m.managementJustificationForNonAdjustment || '—'}
                          </td>
                          <td style={{ padding: '12px', textAlign: 'center' }}>
                            <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                              {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                                <button
                                  onClick={() => openEditMisModal(m)}
                                  style={{ background: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', borderRadius: '4px', padding: '4px 8px', fontSize: '0.72rem', cursor: 'pointer' }}
                                >
                                  ✏️ Edit
                                </button>
                              )}
                              {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                                <button
                                  onClick={() => handleDeleteMisstatement(m.id)}
                                  style={{ background: 'var(--error-bg)', color: 'var(--error)', border: '1px solid var(--error-border)', borderRadius: '4px', padding: '4px 8px', fontSize: '0.72rem', cursor: 'pointer' }}
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

          {/* TAB 4: PACKS (SA 265 LETTER & SA 260 PRESENTATION) */}
          {activeTab === 'packs' && (
            <div style={{ padding: '24px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <div style={{ display: 'flex', gap: '10px' }}>
                  <button
                    onClick={() => setPackSubTab('sa265')}
                    style={{
                      padding: '7px 14px',
                      borderRadius: '6px',
                      border: 'none',
                      fontWeight: 600,
                      fontSize: '0.82rem',
                      cursor: 'pointer',
                      background: packSubTab === 'sa265' ? '#4f46e5' : 'var(--card-subtle)',
                      color: packSubTab === 'sa265' ? '#fff' : 'var(--text-secondary)'
                    }}
                  >
                    ✉️ SA 265 Internal Control Deficiency Letter
                  </button>
                  <button
                    onClick={() => setPackSubTab('sa260')}
                    style={{
                      padding: '7px 14px',
                      borderRadius: '6px',
                      border: 'none',
                      fontWeight: 600,
                      fontSize: '0.82rem',
                      cursor: 'pointer',
                      background: packSubTab === 'sa260' ? '#4f46e5' : 'var(--card-subtle)',
                      color: packSubTab === 'sa260' ? '#fff' : 'var(--text-secondary)'
                    }}
                  >
                    📑 SA 260 Audit Committee Governance Pack
                  </button>
                </div>

                <div style={{ display: 'flex', gap: '8px' }}>
                  <button
                    onClick={() => copyText(
                      packSubTab === 'sa265' ? selectedEngagement.generatedSa265LetterText : selectedEngagement.generatedGovernancePackText,
                      packSubTab === 'sa265' ? 'SA 265 Letter' : 'SA 260 Pack'
                    )}
                    style={{
                      background: 'var(--card-subtle)',
                      border: '1px solid var(--border-subtle)',
                      borderRadius: '6px',
                      padding: '7px 14px',
                      fontSize: '0.82rem',
                      fontWeight: 600,
                      cursor: 'pointer'
                    }}
                  >
                    📋 Copy Text
                  </button>
                  {can('auditcommittee.manage') && selectedEngagement.status !== 'SignedOff' && (
                    <button
                      onClick={handleGeneratePack}
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
                      🔄 Re-Generate Pack
                    </button>
                  )}
                </div>
              </div>

              {packSubTab === 'sa265' ? (
                selectedEngagement.generatedSa265LetterText ? (
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
                    {selectedEngagement.generatedSa265LetterText}
                  </div>
                ) : (
                  <div style={{ textAlign: 'center', padding: '48px 24px', background: 'var(--card-subtle)', borderRadius: '8px', border: '1px dashed var(--border-subtle)' }}>
                    <div style={{ fontSize: '2rem', marginBottom: '8px' }}>✉️</div>
                    <div style={{ fontWeight: 600, color: 'var(--text)' }}>SA 265 Letter Not Yet Generated</div>
                    <p style={{ margin: '8px auto 16px', maxWidth: '400px', fontSize: '0.84rem', color: 'var(--muted)' }}>
                      Click Generate below to draft the formal letter communicating significant deficiencies in internal control.
                    </p>
                    <button onClick={handleGeneratePack} style={{ background: '#4f46e5', color: '#fff', border: 'none', borderRadius: '6px', padding: '8px 16px', fontWeight: 600, cursor: 'pointer' }}>
                      ⚡ Generate SA 265 Letter
                    </button>
                  </div>
                )
              ) : (
                selectedEngagement.generatedGovernancePackText ? (
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
                    {selectedEngagement.generatedGovernancePackText}
                  </div>
                ) : (
                  <div style={{ textAlign: 'center', padding: '48px 24px', background: 'var(--card-subtle)', borderRadius: '8px', border: '1px dashed var(--border-subtle)' }}>
                    <div style={{ fontSize: '2rem', marginBottom: '8px' }}>📑</div>
                    <div style={{ fontWeight: 600, color: 'var(--text)' }}>SA 260 Governance Pack Not Yet Generated</div>
                    <p style={{ margin: '8px auto 16px', maxWidth: '400px', fontSize: '0.84rem', color: 'var(--muted)' }}>
                      Click Generate below to compile the statutory Audit Committee presentation docket.
                    </p>
                    <button onClick={handleGeneratePack} style={{ background: '#4f46e5', color: '#fff', border: 'none', borderRadius: '6px', padding: '8px 16px', fontWeight: 600, cursor: 'pointer' }}>
                      ⚡ Generate SA 260 Pack
                    </button>
                  </div>
                )
              )}
            </div>
          )}
        </div>
      ) : (
        <div style={{ textAlign: 'center', padding: '64px 24px', background: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)' }}>
          <div style={{ fontSize: '3rem', marginBottom: '12px' }}>🏛️</div>
          <h2 style={{ margin: 0, fontWeight: 700, fontSize: '1.25rem', color: 'var(--text)' }}>No Engagement Selected</h2>
          <p className="muted" style={{ maxWidth: '480px', margin: '8px auto 20px', fontSize: '0.9rem' }}>
            Select an existing Audit Committee engagement or register a new statutory governance meeting for your corporate clients.
          </p>
          {can('auditcommittee.manage') && (
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
              + Create Committee Engagement
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
            background: 'var(--card)',
            borderRadius: '10px',
            width: '100%',
            maxWidth: '640px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h2 style={{ margin: '0 0 16px', fontSize: '1.25rem', fontWeight: 700 }}>
              New Audit Committee Governance Engagement
            </h2>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Client Organization *
                </label>
                <select
                  value={engForm.clientId}
                  onChange={e => setEngForm(f => ({ ...f, clientId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Financial Year *
                </label>
                <input
                  type="text"
                  value={engForm.financialYear}
                  onChange={e => setEngForm(f => ({ ...f, financialYear: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Engagement Title *
              </label>
              <input
                type="text"
                value={engForm.title}
                onChange={e => setEngForm(f => ({ ...f, title: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Meeting Type
                </label>
                <select
                  value={engForm.meetingType}
                  onChange={e => setEngForm(f => ({ ...f, meetingType: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="Pre-Issuance">Pre-Issuance Meeting</option>
                  <option value="Quarterly">Quarterly Review Meeting</option>
                  <option value="Annual">Annual Governance Meeting</option>
                  <option value="Special">Special Committee Meeting</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Meeting Location / Mode
                </label>
                <input
                  type="text"
                  placeholder="Board Room / Hybrid VC"
                  value={engForm.meetingLocation || ''}
                  onChange={e => setEngForm(f => ({ ...f, meetingLocation: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '16px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Audit Committee Chair
                </label>
                <input
                  type="text"
                  placeholder="Dr. K. S. Rao (Independent Director)"
                  value={engForm.committeeChairName}
                  onChange={e => setEngForm(f => ({ ...f, committeeChairName: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Lead Audit Partner
                </label>
                <input
                  type="text"
                  placeholder="CA Rajesh Verma"
                  value={engForm.leadPartnerName}
                  onChange={e => setEngForm(f => ({ ...f, leadPartnerName: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                onClick={() => setShowNewEngModal(false)}
                style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
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

      {/* MODAL 2: ADD / EDIT DEFICIENCY */}
      {showAddDefModal && (
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
            background: 'var(--card)',
            borderRadius: '10px',
            width: '100%',
            maxWidth: '680px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h2 style={{ margin: '0 0 16px', fontSize: '1.25rem', fontWeight: 700 }}>
              {editingDef ? 'Edit Internal Control Deficiency' : 'Record Internal Control Deficiency (SA 265)'}
            </h2>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Code *
                </label>
                <input
                  type="text"
                  placeholder="ICD-01"
                  value={defForm.deficiencyCode}
                  onChange={e => setDefForm(f => ({ ...f, deficiencyCode: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Title *
                </label>
                <input
                  type="text"
                  placeholder="Deficiency in 3-way matching of procurement invoices"
                  value={defForm.title}
                  onChange={e => setDefForm(f => ({ ...f, title: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Process Area
                </label>
                <select
                  value={defForm.processArea}
                  onChange={e => setDefForm(f => ({ ...f, processArea: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="Revenue Recognition">Revenue Recognition</option>
                  <option value="Procurement & Payables">Procurement & Payables</option>
                  <option value="Inventory Management">Inventory Management</option>
                  <option value="IT General Controls (ITGC)">IT General Controls (ITGC)</option>
                  <option value="Financial Close & Consolidation">Financial Close & Consolidation</option>
                  <option value="Statutory Dues & Taxes">Statutory Dues & Taxes</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Deficiency Type
                </label>
                <select
                  value={defForm.deficiencyType}
                  onChange={e => setDefForm(f => ({ ...f, deficiencyType: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="DesignDeficiency">Design Deficiency</option>
                  <option value="OperatingDeficiency">Operating Deficiency</option>
                </select>
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Severity Level
                </label>
                <select
                  value={defForm.severity}
                  onChange={e => setDefForm(f => ({ ...f, severity: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="SignificantDeficiency">Significant Deficiency</option>
                  <option value="OtherDeficiency">Other Deficiency</option>
                </select>
              </div>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Description of the Deficiency
              </label>
              <textarea
                rows={2}
                value={defForm.description}
                onChange={e => setDefForm(f => ({ ...f, description: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Potential Financial / Operational Effect
                </label>
                <input
                  type="text"
                  placeholder="Risk of duplicate payments or unrecorded supplier liabilities"
                  value={defForm.potentialFinancialEffect}
                  onChange={e => setDefForm(f => ({ ...f, potentialFinancialEffect: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Estimated Risk (INR)
                </label>
                <input
                  type="number"
                  value={defForm.estimatedRiskAmount || 0}
                  onChange={e => setDefForm(f => ({ ...f, estimatedRiskAmount: parseFloat(e.target.value) || 0 }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Auditor Recommendation (SA 265)
              </label>
              <textarea
                rows={2}
                placeholder="Automated three-way match hard stop should be implemented in SAP..."
                value={defForm.auditorRecommendation}
                onChange={e => setDefForm(f => ({ ...f, auditorRecommendation: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '12px', marginBottom: '16px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Management Response / Remediation Plan
                </label>
                <input
                  type="text"
                  placeholder="IT workflow deployed effective April 1, 2025"
                  value={defForm.managementResponse || ''}
                  onChange={e => setDefForm(f => ({ ...f, managementResponse: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Status
                </label>
                <select
                  value={defForm.status}
                  onChange={e => setDefForm(f => ({ ...f, status: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="Identified">Identified</option>
                  <option value="CommunicatedToManagement">Communicated to Management</option>
                  <option value="Remediated">Remediated</option>
                  <option value="CarriedForward">Carried Forward</option>
                </select>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                onClick={() => { setShowAddDefModal(false); setEditingDef(null); }}
                style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleSaveDeficiency}
                style={{ padding: '8px 16px', background: '#4f46e5', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                {editingDef ? 'Update Deficiency' : 'Add Deficiency'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 3: ADD / EDIT SIGNIFICANT MATTER */}
      {showAddMatterModal && (
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
            background: 'var(--card)',
            borderRadius: '10px',
            width: '100%',
            maxWidth: '600px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h2 style={{ margin: '0 0 16px', fontSize: '1.25rem', fontWeight: 700 }}>
              {editingMatter ? 'Edit Significant Audit Matter' : 'Record Significant Audit Matter (SA 260)'}
            </h2>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 2fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Code *
                </label>
                <input
                  type="text"
                  placeholder="SAM-01"
                  value={matterForm.matterCode}
                  onChange={e => setMatterForm(f => ({ ...f, matterCode: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Category
                </label>
                <select
                  value={matterForm.category}
                  onChange={e => setMatterForm(f => ({ ...f, category: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="KeyAuditMatter">Key Audit Matter (KAM)</option>
                  <option value="AccountingPractices">Qualitative Accounting Practices</option>
                  <option value="AuditDifficulties">Audit Difficulties Encountered</option>
                  <option value="ManagementConsultation">Management Consultations</option>
                  <option value="WrittenRepresentations">Written Representations Requested</option>
                </select>
              </div>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Subject / Topic *
              </label>
              <input
                type="text"
                placeholder="Revenue Recognition on Long-Term Turnkey Contracts (Ind AS 115)"
                value={matterForm.subject}
                onChange={e => setMatterForm(f => ({ ...f, subject: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Matter Details & Background
              </label>
              <textarea
                rows={2}
                value={matterForm.details}
                onChange={e => setMatterForm(f => ({ ...f, details: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Auditor Assessment & Technical Conclusion
              </label>
              <textarea
                rows={2}
                value={matterForm.auditorAssessment}
                onChange={e => setMatterForm(f => ({ ...f, auditorAssessment: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Audit Committee Feedback / Remarks
              </label>
              <input
                type="text"
                placeholder="Committee noted and agreed with proposed presentation."
                value={matterForm.auditCommitteeFeedback || ''}
                onChange={e => setMatterForm(f => ({ ...f, auditCommitteeFeedback: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                onClick={() => { setShowAddMatterModal(false); setEditingMatter(null); }}
                style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleSaveMatter}
                style={{ padding: '8px 16px', background: '#4f46e5', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                {editingMatter ? 'Update Matter' : 'Add Matter'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 4: ADD / EDIT MISSTATEMENT */}
      {showAddMisModal && (
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
            background: 'var(--card)',
            borderRadius: '10px',
            width: '100%',
            maxWidth: '540px',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h2 style={{ margin: '0 0 16px', fontSize: '1.25rem', fontWeight: 700 }}>
              {editingMis ? 'Edit Uncorrected Misstatement' : 'Record Uncorrected Misstatement (SA 450)'}
            </h2>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Account / Particulars *
              </label>
              <input
                type="text"
                placeholder="Understatement of provision for warranties"
                value={misForm.accountDescription}
                onChange={e => setMisForm(f => ({ ...f, accountDescription: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Misstatement Amount (INR) *
                </label>
                <input
                  type="number"
                  value={misForm.misstatementAmount}
                  onChange={e => setMisForm(f => ({ ...f, misstatementAmount: parseFloat(e.target.value) || 0 }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  Nature of Misstatement
                </label>
                <select
                  value={misForm.nature}
                  onChange={e => setMisForm(f => ({ ...f, nature: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                >
                  <option value="Factual">Factual (Objective error)</option>
                  <option value="Judgmental">Judgmental (Estimate variance)</option>
                  <option value="Projected">Projected (Sampling projection)</option>
                </select>
              </div>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', cursor: 'pointer' }}>
                <input
                  type="checkbox"
                  checked={misForm.isMaterial}
                  onChange={e => setMisForm(f => ({ ...f, isMaterial: e.target.checked }))}
                />
                <span style={{ fontSize: '0.84rem', fontWeight: 600, color: misForm.isMaterial ? 'var(--error)' : 'var(--text-secondary)' }}>
                  Considered Material to the Financial Statements?
                </span>
              </label>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Management Justification for Non-Adjustment
              </label>
              <textarea
                rows={2}
                placeholder="Immaterial timing difference; will reverse in subsequent financial year..."
                value={misForm.managementJustificationForNonAdjustment || ''}
                onChange={e => setMisForm(f => ({ ...f, managementJustificationForNonAdjustment: e.target.value }))}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                onClick={() => { setShowAddMisModal(false); setEditingMis(null); }}
                style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleSaveMisstatement}
                style={{ padding: '8px 16px', background: '#4f46e5', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                {editingMis ? 'Update Misstatement' : 'Record Misstatement'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 5: SIGN OFF ENGAGEMENT */}
      {showSignOffModal && (
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
            background: 'var(--card)',
            borderRadius: '10px',
            width: '100%',
            maxWidth: '540px',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h2 style={{ margin: '0 0 12px', fontSize: '1.25rem', fontWeight: 700 }}>
              Formally Sign Off Audit Committee Engagement
            </h2>
            <p className="muted" style={{ margin: '0 0 16px', fontSize: '0.84rem' }}>
              Pursuant to Section 177 and ICAI SA 260, record meeting minutes and freeze communication records.
            </p>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ fontSize: '0.8rem', fontWeight: 600, display: 'block', marginBottom: '6px' }}>
                Audit Committee Minutes & Acknowledgment
              </label>
              <textarea
                rows={4}
                placeholder="Audit Committee reviewed SA 260 presentation, uncorrected misstatements, and SA 265 letter. Committee recommended financial statements for Board approval."
                value={signOffMinutes}
                onChange={e => setSignOffMinutes(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                onClick={() => setShowSignOffModal(false)}
                style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleSignOff}
                style={{ padding: '8px 16px', background: '#10b981', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
              >
                Sign Off & Archive
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
