import React, { useState, useEffect } from 'react'
import { toError,
  api,
  secretarialAuditApi,
  type Client,
  type SecretarialAuditSummaryDto,
  type SecretarialAuditEngagementDto,
  type SecretarialChecklistItemDto,
  type McaFormFilingAuditDto,
  type BoardMeetingAuditRecordDto,
  type SaveEngagementRequest,
  type CertifyEngagementRequest,
  type SaveChecklistItemRequest,
  type SaveMcaFilingRequest,
  type SaveBoardMeetingAuditRequest,
  type ApplicabilityCheckRequest,
  type ApplicabilityCheckResult,
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

export const SecretarialAuditPage: React.FC = () => {
  // State
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)
  const [successMsg, setSuccessMsg] = useState<string | null>(null)

  const [summary, setSummary] = useState<SecretarialAuditSummaryDto | null>(null)
  const [engagements, setEngagements] = useState<SecretarialAuditEngagementDto[]>([])
  const [clients, setClients] = useState<Client[]>([])
  const [selectedEngId, setSelectedEngId] = useState<string | null>(null)
  const [currentEng, setCurrentEng] = useState<SecretarialAuditEngagementDto | null>(null)

  // Sub-entity tabs
  const [activeTab, setActiveTab] = useState<'checklist' | 'filings' | 'meetings' | 'report'>('checklist')
  const [checklist, setChecklist] = useState<SecretarialChecklistItemDto[]>([])
  const [filings, setFilings] = useState<McaFormFilingAuditDto[]>([])
  const [meetings, setMeetings] = useState<BoardMeetingAuditRecordDto[]>([])

  // Filters
  const [statuteFilter, setStatuteFilter] = useState<string>('All')
  const [statusFilter, setStatusFilter] = useState<string>('All')

  // Modals
  const [showApplicabilityModal, setShowApplicabilityModal] = useState<boolean>(false)
  const [showEngagementModal, setShowEngagementModal] = useState<boolean>(false)
  const [showCertifyModal, setShowCertifyModal] = useState<boolean>(false)
  const [showChecklistModal, setShowChecklistModal] = useState<boolean>(false)
  const [showFilingModal, setShowFilingModal] = useState<boolean>(false)
  const [showMeetingModal, setShowMeetingModal] = useState<boolean>(false)

  // Applicability Calculator State
  const [appCalcReq, setAppCalcReq] = useState<ApplicabilityCheckRequest>({
    companyType: 'UnlistedPublic',
    paidUpShareCapital: 600000000,
    turnover: 1500000000,
    outstandingBorrowings: 500000000,
  })
  const [appCalcResult, setAppCalcResult] = useState<ApplicabilityCheckResult | null>(null)

  // Engagement Form State
  const [engForm, setEngForm] = useState<SaveEngagementRequest>({
    clientId: '',
    engagementReference: `SEC-${new Date().getFullYear()}-001`,
    title: `Statutory Secretarial Audit FY ${new Date().getFullYear() - 1}-${new Date().getFullYear().toString().slice(-2)}`,
    financialYear: `${new Date().getFullYear() - 1}-${new Date().getFullYear().toString().slice(-2)}`,
    auditPeriodStart: `${new Date().getFullYear() - 1}-04-01`,
    auditPeriodEnd: `${new Date().getFullYear()}-03-31`,
    cin: '',
    companyType: 'UnlistedPublic',
    paidUpShareCapital: 550000000,
    turnover: 2600000000,
    outstandingBorrowings: 1200000000,
    auditorName: '',
    auditorFirmName: '',
    membershipType: 'FCS',
    membershipNumber: '',
    certificateOfPracticeNumber: '',
    peerReviewCertificateNumber: '',
  })
  const [editingEngId, setEditingEngId] = useState<string | null>(null)

  // Certify Form State
  const [certifyForm, setCertifyForm] = useState<CertifyEngagementRequest>({
    udin: '',
    certifiedBy: '',
  })

  // Checklist Item Form State
  const [checkForm, setCheckForm] = useState<SaveChecklistItemRequest>({
    statuteCategory: 'CompaniesAct2013',
    clauseCode: '',
    subjectArea: '',
    statutoryRequirement: '',
    complianceStatus: 'Complied',
    auditorRemarks: '',
    isQualification: false,
    deviationRisk: 'None',
    actionOwner: '',
  })
  const [editingCheckId, setEditingCheckId] = useState<string | null>(null)

  // Filing Form State
  const [filingForm, setFilingForm] = useState<SaveMcaFilingRequest>({
    formName: 'AOC-4',
    description: 'Filing of Audited Financial Statements',
    eventDate: `${new Date().getFullYear() - 1}-09-30`,
    statutoryDaysAllowed: 30,
    actualFilingDate: '',
    srn: '',
    additionalFeePaid: 0,
    auditorRemarks: '',
  })
  const [editingFilingId, setEditingFilingId] = useState<string | null>(null)

  // Meeting Form State
  const [meetingForm, setMeetingForm] = useState<SaveBoardMeetingAuditRequest>({
    meetingType: 'BoardMeeting',
    meetingDate: `${new Date().getFullYear() - 1}-06-20`,
    previousMeetingDate: '',
    noticeDispatchDate: `${new Date().getFullYear() - 1}-06-12`,
    totalMembersCount: 6,
    attendedMembersCount: 5,
    independentDirectorsAttended: 2,
    secretarialStandardComplied: true,
    auditorRemarks: '',
  })
  const [editingMeetingId, setEditingMeetingId] = useState<string | null>(null)

  // Initial Load
  useEffect(() => {
    loadData()
  }, [])

  // When selected engagement changes, reload its tabs
  useEffect(() => {
    if (selectedEngId) {
      loadEngagementDetails(selectedEngId)
    }
  }, [selectedEngId, activeTab, statuteFilter, statusFilter])

  const loadData = async () => {
    setLoading(true)
    setError(null)
    try {
      const [sumData, engsData, clsData] = await Promise.all([
        secretarialAuditApi.getSummary(),
        secretarialAuditApi.listEngagements(),
        api<ClientPage>('/api/clients?pageSize=100').catch(() => ({ items: [], total: 0 })),
      ])
      setSummary(sumData)
      setEngagements(engsData)
      setClients(clsData.items || [])

      if (engsData.length > 0 && !selectedEngId) {
        setSelectedEngId(engsData[0].id)
      }
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to load secretarial audit data.')
    } finally {
      setLoading(false)
    }
  }

  const loadEngagementDetails = async (id: string) => {
    try {
      const eng = await secretarialAuditApi.getEngagement(id)
      setCurrentEng(eng)

      if (activeTab === 'checklist') {
        const items = await secretarialAuditApi.listChecklist(id, statuteFilter, statusFilter)
        setChecklist(items)
      } else if (activeTab === 'filings') {
        const fList = await secretarialAuditApi.listFilings(id)
        setFilings(fList)
      } else if (activeTab === 'meetings') {
        const mList = await secretarialAuditApi.listMeetings(id)
        setMeetings(mList)
      }
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to load engagement details.')
    }
  }

  const handleTestApplicability = async () => {
    try {
      const res = await secretarialAuditApi.checkApplicability(appCalcReq)
      setAppCalcResult(res)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Applicability calculation failed.')
    }
  }

  const handleSaveEngagement = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      if (editingEngId) {
        await secretarialAuditApi.updateEngagement(editingEngId, engForm)
        setSuccessMsg('Engagement updated successfully.')
      } else {
        const created = await secretarialAuditApi.createEngagement(engForm)
        setSelectedEngId(created.id)
        setSuccessMsg('Engagement and baseline checklist created successfully.')
      }
      setShowEngagementModal(false)
      setEditingEngId(null)
      await loadData()
      if (selectedEngId) loadEngagementDetails(selectedEngId)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save engagement.')
    }
  }

  const handleDeleteEngagement = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this secretarial audit engagement? All checklist items, filings and meeting logs will be removed.')) return
    try {
      await secretarialAuditApi.deleteEngagement(id)
      setSuccessMsg('Engagement deleted.')
      setSelectedEngId(null)
      setCurrentEng(null)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete engagement.')
    }
  }

  const handleCertify = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentEng) return
    try {
      const updated = await secretarialAuditApi.certifyEngagement(currentEng.id, certifyForm)
      setCurrentEng(updated)
      setShowCertifyModal(false)
      setSuccessMsg(`Form MR-3 successfully certified with UDIN ${updated.udin}`)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Certification failed. Ensure UDIN is at least 17 characters.')
    }
  }

  const handleRegenerateReport = async () => {
    if (!currentEng) return
    try {
      const res = await secretarialAuditApi.generateMR3(currentEng.id)
      setCurrentEng({ ...currentEng, formMR3ReportText: res.reportText })
      setSuccessMsg('Form MR-3 report text regenerated with latest findings.')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to regenerate report.')
    }
  }

  // Checklist updates
  const handleChecklistStatusChange = async (item: SecretarialChecklistItemDto, newStatus: string) => {
    if (!currentEng) return
    try {
      await secretarialAuditApi.updateChecklistItem(currentEng.id, item.id, {
        statuteCategory: item.statuteCategory,
        clauseCode: item.clauseCode,
        subjectArea: item.subjectArea,
        statutoryRequirement: item.statutoryRequirement,
        complianceStatus: newStatus,
        auditorRemarks: item.auditorRemarks,
        isQualification: newStatus === 'NotComplied' ? true : item.isQualification,
        deviationRisk: item.deviationRisk,
        targetCompletionDate: item.targetCompletionDate,
        actionOwner: item.actionOwner,
      })
      await loadEngagementDetails(currentEng.id)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to update checklist item.')
    }
  }

  const handleToggleQualification = async (item: SecretarialChecklistItemDto) => {
    if (!currentEng) return
    try {
      await secretarialAuditApi.updateChecklistItem(currentEng.id, item.id, {
        statuteCategory: item.statuteCategory,
        clauseCode: item.clauseCode,
        subjectArea: item.subjectArea,
        statutoryRequirement: item.statutoryRequirement,
        complianceStatus: item.complianceStatus,
        auditorRemarks: item.auditorRemarks,
        isQualification: !item.isQualification,
        deviationRisk: item.deviationRisk,
        targetCompletionDate: item.targetCompletionDate,
        actionOwner: item.actionOwner,
      })
      await loadEngagementDetails(currentEng.id)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to toggle qualification.')
    }
  }

  const handleSaveChecklistItem = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentEng) return
    try {
      if (editingCheckId) {
        await secretarialAuditApi.updateChecklistItem(currentEng.id, editingCheckId, checkForm)
        setSuccessMsg('Checklist item updated.')
      } else {
        await secretarialAuditApi.createChecklistItem(currentEng.id, checkForm)
        setSuccessMsg('New checklist item added.')
      }
      setShowChecklistModal(false)
      setEditingCheckId(null)
      await loadEngagementDetails(currentEng.id)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save checklist item.')
    }
  }

  const handleDeleteChecklistItem = async (itemId: string) => {
    if (!currentEng) return
    if (!window.confirm('Delete this checklist item?')) return
    try {
      await secretarialAuditApi.deleteChecklistItem(currentEng.id, itemId)
      await loadEngagementDetails(currentEng.id)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete checklist item.')
    }
  }

  // Filing saves
  const handleSaveFiling = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentEng) return
    try {
      if (editingFilingId) {
        await secretarialAuditApi.updateFiling(currentEng.id, editingFilingId, filingForm)
        setSuccessMsg('MCA form filing updated.')
      } else {
        await secretarialAuditApi.createFiling(currentEng.id, filingForm)
        setSuccessMsg('MCA form filing record added.')
      }
      setShowFilingModal(false)
      setEditingFilingId(null)
      await loadEngagementDetails(currentEng.id)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save MCA filing record.')
    }
  }

  const handleDeleteFiling = async (filingId: string) => {
    if (!currentEng) return
    if (!window.confirm('Delete this filing audit record?')) return
    try {
      await secretarialAuditApi.deleteFiling(currentEng.id, filingId)
      await loadEngagementDetails(currentEng.id)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete filing record.')
    }
  }

  // Meeting saves
  const handleSaveMeeting = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentEng) return
    try {
      if (editingMeetingId) {
        await secretarialAuditApi.updateMeeting(currentEng.id, editingMeetingId, meetingForm)
        setSuccessMsg('Meeting audit record updated.')
      } else {
        await secretarialAuditApi.createMeeting(currentEng.id, meetingForm)
        setSuccessMsg('Board/Committee meeting record added.')
      }
      setShowMeetingModal(false)
      setEditingMeetingId(null)
      await loadEngagementDetails(currentEng.id)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save meeting record.')
    }
  }

  const handleDeleteMeeting = async (meetingId: string) => {
    if (!currentEng) return
    if (!window.confirm('Delete this meeting record?')) return
    try {
      await secretarialAuditApi.deleteMeeting(currentEng.id, meetingId)
      await loadEngagementDetails(currentEng.id)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete meeting record.')
    }
  }

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Page Header */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-gray-200 pb-5">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold text-gray-900">
              Secretarial Audit & Form MR-3 Compliance Engine
            </h1>
            <span className="px-2.5 py-0.5 rounded-full text-xs font-medium bg-indigo-100 text-indigo-800 border border-indigo-200">
              Sec 204 / Rule 9
            </span>
            {loading && <span className="text-xs text-indigo-600 animate-pulse font-medium">Syncing...</span>}
          </div>
          <p className="text-sm text-gray-500 mt-1">
            Companies Act 2013, SEBI LODR/PIT/SAST, FEMA (FDI/ODI/ECB), SCRA & Depositories Multi-Statute Audit Desk
          </p>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={() => {
              handleTestApplicability()
              setShowApplicabilityModal(true)
            }}
            className="px-3.5 py-2 text-sm font-medium text-indigo-700 bg-indigo-50 border border-indigo-200 rounded-lg hover:bg-indigo-100 transition shadow-sm"
          >
            Applicability Calculator
          </button>
          <button
            onClick={() => {
              setEditingEngId(null)
              if (clients.length > 0) setEngForm((prev) => ({ ...prev, clientId: clients[0].id }))
              setShowEngagementModal(true)
            }}
            className="px-4 py-2 text-sm font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition shadow-sm"
          >
            + New Audit Engagement
          </button>
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div className="p-4 bg-red-50 border border-red-200 text-red-700 rounded-lg text-sm flex items-center justify-between">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-500 hover:text-red-700">&times;</button>
        </div>
      )}
      {successMsg && (
        <div className="p-4 bg-green-50 border border-green-200 text-green-700 rounded-lg text-sm flex items-center justify-between">
          <span>{successMsg}</span>
          <button onClick={() => setSuccessMsg(null)} className="text-green-500 hover:text-green-700">&times;</button>
        </div>
      )}

      {/* Top Executive KPI Cards */}
      {summary && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Engagements</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-gray-900">{summary.totalEngagements}</span>
              <span className="text-xs text-green-600 font-medium">({summary.certifiedEngagements} certified)</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">Section 204 audits</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Mandatory Entities</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-indigo-700">{summary.mandatoryApplicableCount}</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">Breached capital/loans gate</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Qualifications</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-amber-600">{summary.totalQualificationsLogged}</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">Adverse remarks in MR-3</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Delayed Filings</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-red-600">{summary.delayedFilingsCount}</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">MCA e-forms with late fees</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Meeting Lapses</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-rose-600">{summary.meetingIrregularitiesCount}</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">120-day gap / quorum issues</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Compliance Index</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-blue-700">{summary.averageComplianceScore}%</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">Average checklist score</p>
          </div>
        </div>
      )}

      {/* Engagement Selector & Metadata Banner */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <label className="text-sm font-semibold text-gray-700 whitespace-nowrap">Select Audit Engagement:</label>
            <select
              value={selectedEngId || ''}
              onChange={(e) => setSelectedEngId(e.target.value)}
              className="px-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:ring-2 focus:ring-indigo-500 focus:border-indigo-500 bg-gray-50 font-medium text-gray-800"
            >
              {engagements.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.engagementReference} - {e.clientName} ({e.financialYear}) [{e.auditStatus}]
                </option>
              ))}
              {engagements.length === 0 && <option value="">No engagements found</option>}
            </select>
          </div>

          {currentEng && (
            <div className="flex items-center gap-2">
              <span className={`px-2.5 py-1 text-xs font-semibold rounded-full border ${
                currentEng.auditStatus === 'Certified'
                  ? 'bg-green-100 text-green-800 border-green-300'
                  : 'bg-amber-100 text-amber-800 border-amber-300'
              }`}>
                {currentEng.auditStatus}
              </span>
              {currentEng.isApplicable ? (
                <span className="px-2.5 py-1 text-xs font-semibold rounded-full bg-blue-50 text-blue-800 border border-blue-200">
                  Mandatory Audit
                </span>
              ) : (
                <span className="px-2.5 py-1 text-xs font-semibold rounded-full bg-gray-100 text-gray-700 border border-gray-200">
                  Voluntary Audit
                </span>
              )}
              {currentEng.auditStatus !== 'Certified' && (
                <button
                  onClick={() => {
                    setCertifyForm({ udin: currentEng.udin || '', certifiedBy: currentEng.auditorName || '' })
                    setShowCertifyModal(true)
                  }}
                  className="px-3 py-1.5 text-xs font-semibold text-white bg-green-600 rounded-md hover:bg-green-700 transition shadow-sm"
                >
                  Certify with UDIN
                </button>
              )}
              <button
                onClick={() => {
                  setEditingEngId(currentEng.id)
                  setEngForm({
                    clientId: currentEng.clientId,
                    engagementReference: currentEng.engagementReference,
                    title: currentEng.title,
                    financialYear: currentEng.financialYear,
                    auditPeriodStart: currentEng.auditPeriodStart.slice(0, 10),
                    auditPeriodEnd: currentEng.auditPeriodEnd.slice(0, 10),
                    cin: currentEng.cin,
                    companyType: currentEng.companyType,
                    paidUpShareCapital: currentEng.paidUpShareCapital,
                    turnover: currentEng.turnover,
                    outstandingBorrowings: currentEng.outstandingBorrowings,
                    auditorName: currentEng.auditorName,
                    auditorFirmName: currentEng.auditorFirmName,
                    membershipType: currentEng.membershipType,
                    membershipNumber: currentEng.membershipNumber,
                    certificateOfPracticeNumber: currentEng.certificateOfPracticeNumber,
                    peerReviewCertificateNumber: currentEng.peerReviewCertificateNumber,
                  })
                  setShowEngagementModal(true)
                }}
                className="px-3 py-1.5 text-xs font-medium text-gray-700 bg-gray-100 rounded-md hover:bg-gray-200 border border-gray-300 transition"
              >
                Edit
              </button>
              <button
                onClick={() => handleDeleteEngagement(currentEng.id)}
                className="px-3 py-1.5 text-xs font-medium text-red-600 bg-red-50 rounded-md hover:bg-red-100 border border-red-200 transition"
              >
                Delete
              </button>
            </div>
          )}
        </div>

        {currentEng && (
          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-3 text-xs bg-gray-50 p-3 rounded-lg border border-gray-200">
            <div>
              <span className="text-gray-500 font-medium">Company Name:</span>
              <p className="font-semibold text-gray-900 truncate">{currentEng.clientName}</p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">CIN:</span>
              <p className="font-mono font-semibold text-gray-900">{currentEng.cin || 'N/A'}</p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">Company Type:</span>
              <p className="font-semibold text-gray-900">{currentEng.companyType}</p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">Capital / Turnover / Loans:</span>
              <p className="font-semibold text-gray-900">
                ₹{(currentEng.paidUpShareCapital / 10000000).toFixed(1)}Cr / ₹{(currentEng.turnover / 10000000).toFixed(1)}Cr / ₹{(currentEng.outstandingBorrowings / 10000000).toFixed(1)}Cr
              </p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">Auditor / CP No:</span>
              <p className="font-semibold text-gray-900 truncate">{currentEng.auditorName} ({currentEng.certificateOfPracticeNumber})</p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">UDIN:</span>
              <p className="font-mono font-semibold text-indigo-700 truncate">{currentEng.udin || 'Not Assigned'}</p>
            </div>
          </div>
        )}
      </div>

      {/* Sub-Entity Navigation Tabs */}
      {currentEng && (
        <div className="space-y-4">
          <div className="border-b border-gray-200">
            <nav className="flex space-x-8">
              <button
                onClick={() => setActiveTab('checklist')}
                className={`py-3 px-1 border-b-2 text-sm font-medium transition ${
                  activeTab === 'checklist'
                    ? 'border-indigo-600 text-indigo-600 font-semibold'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                Multi-Statute Checklist ({currentEng.totalChecklistCount})
              </button>
              <button
                onClick={() => setActiveTab('filings')}
                className={`py-3 px-1 border-b-2 text-sm font-medium transition ${
                  activeTab === 'filings'
                    ? 'border-indigo-600 text-indigo-600 font-semibold'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                MCA E-Filings Audit ({currentEng.totalFilingsAudited})
                {currentEng.filingsDelayedCount > 0 && (
                  <span className="ml-2 px-1.5 py-0.5 rounded-full text-xs bg-red-100 text-red-700 font-bold">
                    {currentEng.filingsDelayedCount} Late
                  </span>
                )}
              </button>
              <button
                onClick={() => setActiveTab('meetings')}
                className={`py-3 px-1 border-b-2 text-sm font-medium transition ${
                  activeTab === 'meetings'
                    ? 'border-indigo-600 text-indigo-600 font-semibold'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                Board & Committee Meetings ({currentEng.totalMeetingsAudited})
                {currentEng.meetingDeficienciesCount > 0 && (
                  <span className="ml-2 px-1.5 py-0.5 rounded-full text-xs bg-rose-100 text-rose-700 font-bold">
                    {currentEng.meetingDeficienciesCount} Alerts
                  </span>
                )}
              </button>
              <button
                onClick={() => setActiveTab('report')}
                className={`py-3 px-1 border-b-2 text-sm font-medium transition ${
                  activeTab === 'report'
                    ? 'border-indigo-600 text-indigo-600 font-semibold'
                    : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                }`}
              >
                Form MR-3 Statutory Report
              </button>
            </nav>
          </div>

          {/* TAB 1: Multi-Statute Checklist */}
          {activeTab === 'checklist' && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="text-xs font-semibold text-gray-500 uppercase mr-1">Statute:</span>
                  {['All', 'CompaniesAct2013', 'Fema1999', 'DepositoriesAct1996', 'SebiLodr2015', 'SebiPit2015', 'IndustrySpecificLaws'].map((cat) => (
                    <button
                      key={cat}
                      onClick={() => setStatuteFilter(cat)}
                      className={`px-2.5 py-1 text-xs rounded-lg font-medium transition ${
                        statuteFilter === cat
                          ? 'bg-indigo-600 text-white shadow-sm'
                          : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
                      }`}
                    >
                      {cat === 'All' ? 'All Statutes' : cat.replace('2013', ' 2013').replace('1999', ' 1999').replace('1996', ' 1996').replace('2015', ' 2015')}
                    </button>
                  ))}
                </div>

                <div className="flex items-center gap-2">
                  <select
                    value={statusFilter}
                    onChange={(e) => setStatusFilter(e.target.value)}
                    className="px-2.5 py-1 text-xs border border-gray-300 rounded-lg bg-gray-50"
                  >
                    <option value="All">All Statuses</option>
                    <option value="Complied">Complied</option>
                    <option value="NotComplied">Not Complied</option>
                    <option value="PartiallyComplied">Partially Complied</option>
                    <option value="NotApplicable">Not Applicable</option>
                  </select>

                  <button
                    onClick={() => {
                      setEditingCheckId(null)
                      setCheckForm({
                        statuteCategory: 'CompaniesAct2013',
                        clauseCode: '',
                        subjectArea: '',
                        statutoryRequirement: '',
                        complianceStatus: 'Complied',
                        auditorRemarks: '',
                        isQualification: false,
                        deviationRisk: 'None',
                        actionOwner: '',
                      })
                      setShowChecklistModal(true)
                    }}
                    className="px-3 py-1 text-xs font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition"
                  >
                    + Add Clause
                  </button>
                </div>
              </div>

              {/* Checklist Table */}
              <div className="overflow-x-auto border border-gray-200 rounded-lg">
                <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                  <thead className="bg-gray-50 text-gray-600 font-semibold uppercase tracking-wider">
                    <tr>
                      <th className="py-3 px-3">Statute & Clause</th>
                      <th className="py-3 px-3">Subject & Statutory Mandate</th>
                      <th className="py-3 px-3">Compliance Status</th>
                      <th className="py-3 px-3">Qualification</th>
                      <th className="py-3 px-3">Risk</th>
                      <th className="py-3 px-3">Auditor Remarks</th>
                      <th className="py-3 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200 bg-white">
                    {checklist.map((item) => (
                      <tr key={item.id} className="hover:bg-gray-50 transition">
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <span className="font-semibold text-indigo-700 font-mono">{item.clauseCode}</span>
                          <p className="text-[10px] text-gray-400">{item.statuteCategory}</p>
                        </td>
                        <td className="py-3 px-3 align-top">
                          <p className="font-semibold text-gray-900">{item.subjectArea}</p>
                          <p className="text-gray-500 text-[11px] mt-0.5 max-w-md">{item.statutoryRequirement}</p>
                        </td>
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <select
                            value={item.complianceStatus}
                            onChange={(e) => handleChecklistStatusChange(item, e.target.value)}
                            className={`px-2 py-1 rounded text-xs font-semibold border ${
                              item.complianceStatus === 'Complied'
                                ? 'bg-green-50 text-green-700 border-green-300'
                                : item.complianceStatus === 'NotComplied'
                                ? 'bg-red-50 text-red-700 border-red-300'
                                : item.complianceStatus === 'PartiallyComplied'
                                ? 'bg-amber-50 text-amber-700 border-amber-300'
                                : 'bg-gray-100 text-gray-600 border-gray-300'
                            }`}
                          >
                            <option value="Complied">Complied</option>
                            <option value="NotComplied">Not Complied</option>
                            <option value="PartiallyComplied">Partially Complied</option>
                            <option value="NotApplicable">Not Applicable</option>
                          </select>
                        </td>
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <button
                            onClick={() => handleToggleQualification(item)}
                            className={`px-2 py-0.5 rounded text-[11px] font-semibold border transition ${
                              item.isQualification
                                ? 'bg-amber-100 text-amber-800 border-amber-300'
                                : 'bg-gray-50 text-gray-400 border-gray-200 hover:text-gray-700'
                            }`}
                          >
                            {item.isQualification ? 'Qualification' : 'Clean'}
                          </button>
                        </td>
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                            item.deviationRisk === 'Critical' || item.deviationRisk === 'High'
                              ? 'bg-red-100 text-red-800'
                              : item.deviationRisk === 'Medium'
                              ? 'bg-amber-100 text-amber-800'
                              : 'bg-gray-100 text-gray-600'
                          }`}>
                            {item.deviationRisk}
                          </span>
                        </td>
                        <td className="py-3 px-3 align-top text-gray-600 text-xs max-w-xs">
                          {item.auditorRemarks || <span className="text-gray-300 italic">No remarks</span>}
                        </td>
                        <td className="py-3 px-3 align-top text-right whitespace-nowrap">
                          <button
                            onClick={() => {
                              setEditingCheckId(item.id)
                              setCheckForm({
                                statuteCategory: item.statuteCategory,
                                clauseCode: item.clauseCode,
                                subjectArea: item.subjectArea,
                                statutoryRequirement: item.statutoryRequirement,
                                complianceStatus: item.complianceStatus,
                                auditorRemarks: item.auditorRemarks,
                                isQualification: item.isQualification,
                                deviationRisk: item.deviationRisk,
                                targetCompletionDate: item.targetCompletionDate ? item.targetCompletionDate.slice(0, 10) : undefined,
                                actionOwner: item.actionOwner,
                              })
                              setShowChecklistModal(true)
                            }}
                            className="text-indigo-600 hover:text-indigo-800 font-medium mr-2"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDeleteChecklistItem(item.id)}
                            className="text-red-500 hover:text-red-700 font-medium"
                          >
                            Delete
                          </button>
                        </td>
                      </tr>
                    ))}
                    {checklist.length === 0 && (
                      <tr>
                        <td colSpan={7} className="py-6 text-center text-gray-400">
                          No checklist items matching filter.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* TAB 2: MCA E-Filings Audit */}
          {activeTab === 'filings' && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-base font-semibold text-gray-900">MCA V2/V3 Portal E-Filing Verification</h3>
                  <p className="text-xs text-gray-500 mt-0.5">
                    Audits statutory e-forms (AOC-4, MGT-7, DIR-12, CHG-1/4, DPT-3, BEN-2, MGT-14) against statutory deadlines and additional fees.
                  </p>
                </div>
                <button
                  onClick={() => {
                    setEditingFilingId(null)
                    setFilingForm({
                      formName: 'AOC-4',
                      description: 'Filing of Financial Statements',
                      eventDate: `${new Date().getFullYear() - 1}-09-30`,
                      statutoryDaysAllowed: 30,
                      actualFilingDate: '',
                      srn: '',
                      additionalFeePaid: 0,
                      auditorRemarks: '',
                    })
                    setShowFilingModal(true)
                  }}
                  className="px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition"
                >
                  + Add Form Filing
                </button>
              </div>

              <div className="overflow-x-auto border border-gray-200 rounded-lg">
                <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                  <thead className="bg-gray-50 text-gray-600 font-semibold uppercase tracking-wider">
                    <tr>
                      <th className="py-3 px-3">Form & Purpose</th>
                      <th className="py-3 px-3">Trigger Event Date</th>
                      <th className="py-3 px-3">Statutory Due Date</th>
                      <th className="py-3 px-3">Actual Filing Date / SRN</th>
                      <th className="py-3 px-3">Status & Delay</th>
                      <th className="py-3 px-3">Additional Fee</th>
                      <th className="py-3 px-3">Auditor Remarks</th>
                      <th className="py-3 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200 bg-white">
                    {filings.map((f) => (
                      <tr key={f.id} className="hover:bg-gray-50 transition">
                        <td className="py-3 px-3 font-semibold text-gray-900">
                          <span className="font-mono text-indigo-700 bg-indigo-50 px-1.5 py-0.5 rounded border border-indigo-200">
                            {f.formName}
                          </span>
                          <p className="text-[11px] text-gray-500 font-normal mt-0.5">{f.description}</p>
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap text-gray-600">
                          {new Date(f.eventDate).toLocaleDateString()}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap font-medium text-gray-700">
                          {new Date(f.statutoryDueDate).toLocaleDateString()}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap">
                          {f.actualFilingDate ? (
                            <div>
                              <p className="font-medium text-gray-900">{new Date(f.actualFilingDate).toLocaleDateString()}</p>
                              <p className="text-[10px] text-gray-400 font-mono">SRN: {f.srn || 'N/A'}</p>
                            </div>
                          ) : (
                            <span className="text-gray-400 italic">Not Filed</span>
                          )}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap">
                          <span className={`px-2 py-0.5 rounded text-xs font-semibold ${
                            f.filingStatus === 'FiledOnTime'
                              ? 'bg-green-100 text-green-800'
                              : f.filingStatus === 'FiledWithDelay'
                              ? 'bg-amber-100 text-amber-800'
                              : 'bg-red-100 text-red-800'
                          }`}>
                            {f.filingStatus}
                          </span>
                          {f.delayDays > 0 && (
                            <span className="ml-1 text-red-600 font-bold text-[11px]">
                              (+{f.delayDays}d)
                            </span>
                          )}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap text-gray-700 font-mono">
                          ₹{f.additionalFeePaid.toLocaleString()}
                        </td>
                        <td className="py-3 px-3 text-gray-500 text-xs max-w-xs truncate">
                          {f.auditorRemarks || '-'}
                        </td>
                        <td className="py-3 px-3 text-right whitespace-nowrap">
                          <button
                            onClick={() => {
                              setEditingFilingId(f.id)
                              setFilingForm({
                                formName: f.formName,
                                description: f.description,
                                eventDate: f.eventDate.slice(0, 10),
                                statutoryDaysAllowed: f.statutoryDaysAllowed,
                                actualFilingDate: f.actualFilingDate ? f.actualFilingDate.slice(0, 10) : '',
                                srn: f.srn,
                                additionalFeePaid: f.additionalFeePaid,
                                auditorRemarks: f.auditorRemarks,
                              })
                              setShowFilingModal(true)
                            }}
                            className="text-indigo-600 hover:text-indigo-800 font-medium mr-2"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDeleteFiling(f.id)}
                            className="text-red-500 hover:text-red-700 font-medium"
                          >
                            Delete
                          </button>
                        </td>
                      </tr>
                    ))}
                    {filings.length === 0 && (
                      <tr>
                        <td colSpan={8} className="py-6 text-center text-gray-400">
                          No MCA form filings logged. Click "+ Add Form Filing" to track statutory returns.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* TAB 3: Board & Committee Meetings Audit */}
          {activeTab === 'meetings' && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-base font-semibold text-gray-900">Board & Committee Meetings Governance Log</h3>
                  <p className="text-xs text-gray-500 mt-0.5">
                    Evaluates Section 173(1) 120-day maximum interval rule, Section 173(3) 7-day advance notice rule, and Section 174 quorum requirements.
                  </p>
                </div>
                <button
                  onClick={() => {
                    setEditingMeetingId(null)
                    setMeetingForm({
                      meetingType: 'BoardMeeting',
                      meetingDate: `${new Date().getFullYear() - 1}-06-20`,
                      previousMeetingDate: '',
                      noticeDispatchDate: `${new Date().getFullYear() - 1}-06-12`,
                      totalMembersCount: 6,
                      attendedMembersCount: 5,
                      independentDirectorsAttended: 2,
                      secretarialStandardComplied: true,
                      auditorRemarks: '',
                    })
                    setShowMeetingModal(true)
                  }}
                  className="px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 rounded-lg hover:bg-indigo-700 transition"
                >
                  + Add Meeting Record
                </button>
              </div>

              <div className="overflow-x-auto border border-gray-200 rounded-lg">
                <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                  <thead className="bg-gray-50 text-gray-600 font-semibold uppercase tracking-wider">
                    <tr>
                      <th className="py-3 px-3">Meeting Type & Date</th>
                      <th className="py-3 px-3">Interval Gap (&le; 120d)</th>
                      <th className="py-3 px-3">Notice Period (&ge; 7d)</th>
                      <th className="py-3 px-3">Attendance & Quorum</th>
                      <th className="py-3 px-3">Independent Directors</th>
                      <th className="py-3 px-3">ICSI SS-1 / SS-2</th>
                      <th className="py-3 px-3">Auditor Remarks</th>
                      <th className="py-3 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200 bg-white">
                    {meetings.map((m) => (
                      <tr key={m.id} className="hover:bg-gray-50 transition">
                        <td className="py-3 px-3 font-semibold text-gray-900 whitespace-nowrap">
                          <span>{m.meetingType}</span>
                          <p className="text-[11px] text-gray-500 font-normal mt-0.5">
                            {new Date(m.meetingDate).toLocaleDateString()}
                          </p>
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap">
                          {m.gapFromPreviousMeetingDays !== undefined && m.gapFromPreviousMeetingDays !== null ? (
                            <span className={`px-2 py-0.5 rounded text-xs font-semibold ${
                              m.isGapCompliant ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800 font-bold'
                            }`}>
                              {m.gapFromPreviousMeetingDays} days
                              {!m.isGapCompliant && ' (Breach > 120d)'}
                            </span>
                          ) : (
                            <span className="text-gray-400 italic">First Meeting</span>
                          )}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap">
                          {m.noticeDaysGiven !== undefined && m.noticeDaysGiven !== null ? (
                            <span className={`px-2 py-0.5 rounded text-xs font-semibold ${
                              m.isNoticeCompliant ? 'bg-green-100 text-green-800' : 'bg-amber-100 text-amber-800'
                            }`}>
                              {m.noticeDaysGiven} days notice
                              {!m.isNoticeCompliant && ' (< 7d)'}
                            </span>
                          ) : (
                            <span className="text-gray-400 italic">-</span>
                          )}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap">
                          <span className={`px-2 py-0.5 rounded text-xs font-semibold ${
                            m.isQuorumCompliant ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
                          }`}>
                            {m.attendedMembersCount} / {m.totalMembersCount} attended (Req: {m.quorumRequiredCount})
                          </span>
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap text-gray-700">
                          {m.independentDirectorsAttended} Present
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                            m.secretarialStandardComplied ? 'bg-blue-100 text-blue-800' : 'bg-red-100 text-red-800'
                          }`}>
                            {m.secretarialStandardComplied ? 'SS Compliant' : 'SS Non-Compliant'}
                          </span>
                        </td>
                        <td className="py-3 px-3 text-gray-500 text-xs max-w-xs truncate">
                          {m.auditorRemarks || '-'}
                        </td>
                        <td className="py-3 px-3 text-right whitespace-nowrap">
                          <button
                            onClick={() => {
                              setEditingMeetingId(m.id)
                              setMeetingForm({
                                meetingType: m.meetingType,
                                meetingDate: m.meetingDate.slice(0, 10),
                                previousMeetingDate: m.previousMeetingDate ? m.previousMeetingDate.slice(0, 10) : '',
                                noticeDispatchDate: m.noticeDispatchDate ? m.noticeDispatchDate.slice(0, 10) : '',
                                totalMembersCount: m.totalMembersCount,
                                attendedMembersCount: m.attendedMembersCount,
                                independentDirectorsAttended: m.independentDirectorsAttended,
                                secretarialStandardComplied: m.secretarialStandardComplied,
                                auditorRemarks: m.auditorRemarks,
                              })
                              setShowMeetingModal(true)
                            }}
                            className="text-indigo-600 hover:text-indigo-800 font-medium mr-2"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDeleteMeeting(m.id)}
                            className="text-red-500 hover:text-red-700 font-medium"
                          >
                            Delete
                          </button>
                        </td>
                      </tr>
                    ))}
                    {meetings.length === 0 && (
                      <tr>
                        <td colSpan={8} className="py-6 text-center text-gray-400">
                          No board meetings logged. Click "+ Add Meeting Record" to verify meeting frequency and quorum.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* TAB 4: Form MR-3 Statutory Report */}
          {activeTab === 'report' && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-gray-200 pb-3">
                <div>
                  <h3 className="text-base font-semibold text-gray-900">Form No. MR-3 Secretarial Audit Report</h3>
                  <p className="text-xs text-gray-500">
                    Annexure to the Board's Report pursuant to Section 204(1) of Companies Act, 2013 and Rule 9.
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    onClick={handleRegenerateReport}
                    className="px-3 py-1.5 text-xs font-medium text-indigo-700 bg-indigo-50 border border-indigo-200 rounded-md hover:bg-indigo-100 transition"
                  >
                    Regenerate Text
                  </button>
                  <button
                    onClick={() => {
                      if (currentEng.formMR3ReportText) {
                        navigator.clipboard.writeText(currentEng.formMR3ReportText)
                        setSuccessMsg('Report text copied to clipboard.')
                      }
                    }}
                    className="px-3 py-1.5 text-xs font-medium text-gray-700 bg-gray-100 border border-gray-300 rounded-md hover:bg-gray-200 transition"
                  >
                    Copy Text
                  </button>
                  <button
                    onClick={() => window.print()}
                    className="px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 rounded-md hover:bg-indigo-700 transition"
                  >
                    Print Report
                  </button>
                </div>
              </div>

              {/* Printable Monospaced Viewer */}
              <div className="p-4 bg-gray-900 text-gray-100 rounded-lg font-mono text-xs overflow-x-auto whitespace-pre leading-relaxed shadow-inner border border-gray-800">
                {currentEng.formMR3ReportText || 'Report text pending generation.'}
              </div>
            </div>
          )}
        </div>
      )}

      {/* MODAL 1: Applicability Calculator */}
      {showApplicabilityModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Section 204 & Rule 9 Applicability Calculator</h3>
              <button onClick={() => setShowApplicabilityModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <div className="space-y-3 text-xs">
              <div>
                <label className="font-semibold text-gray-700">Company Type</label>
                <select
                  value={appCalcReq.companyType}
                  onChange={(e) => setAppCalcReq({ ...appCalcReq, companyType: e.target.value })}
                  className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                >
                  <option value="ListedPublic">Listed Public Company</option>
                  <option value="UnlistedPublic">Unlisted Public Company</option>
                  <option value="PrivateCompany">Private Company</option>
                </select>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Paid-Up Share Capital (₹)</label>
                <input
                  type="number"
                  value={appCalcReq.paidUpShareCapital}
                  onChange={(e) => setAppCalcReq({ ...appCalcReq, paidUpShareCapital: parseFloat(e.target.value) || 0 })}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  placeholder="e.g. 500000000 (50 Cr)"
                />
                <span className="text-[11px] text-gray-400">Rule 9(1)(a) threshold: ₹50 Crore for Public Companies</span>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Annual Turnover (₹)</label>
                <input
                  type="number"
                  value={appCalcReq.turnover}
                  onChange={(e) => setAppCalcReq({ ...appCalcReq, turnover: parseFloat(e.target.value) || 0 })}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  placeholder="e.g. 2500000000 (250 Cr)"
                />
                <span className="text-[11px] text-gray-400">Rule 9(1)(b) threshold: ₹250 Crore for Public Companies</span>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Outstanding Bank / PFI Loans & Borrowings (₹)</label>
                <input
                  type="number"
                  value={appCalcReq.outstandingBorrowings}
                  onChange={(e) => setAppCalcReq({ ...appCalcReq, outstandingBorrowings: parseFloat(e.target.value) || 0 })}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  placeholder="e.g. 1000000000 (100 Cr)"
                />
                <span className="text-[11px] text-gray-400">Rule 9(1)(c) threshold: ₹100 Crore for ALL Companies (incl. Private)</span>
              </div>

              <button
                type="button"
                onClick={handleTestApplicability}
                className="w-full py-2 bg-indigo-600 text-white font-semibold rounded-lg hover:bg-indigo-700 transition"
              >
                Compute Applicability
              </button>

              {appCalcResult && (
                <div className={`p-4 rounded-lg border text-xs mt-3 ${
                  appCalcResult.isApplicable ? 'bg-blue-50 border-blue-200 text-blue-900' : 'bg-gray-50 border-gray-200 text-gray-700'
                }`}>
                  <div className="flex items-center gap-2 font-bold text-sm">
                    <span>{appCalcResult.isApplicable ? 'MANDATORY SECRETARIAL AUDIT' : 'EXEMPT FROM MANDATORY AUDIT'}</span>
                  </div>
                  <p className="mt-1 font-medium">{appCalcResult.reason}</p>
                  {appCalcResult.thresholdsExceeded.length > 0 && (
                    <ul className="list-disc list-inside mt-2 space-y-1 text-gray-600">
                      {appCalcResult.thresholdsExceeded.map((t, idx) => (
                        <li key={idx}>{t}</li>
                      ))}
                    </ul>
                  )}
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: New / Edit Engagement */}
      {showEngagementModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-2xl w-full p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">
                {editingEngId ? 'Edit Audit Engagement' : 'Create Secretarial Audit Engagement'}
              </h3>
              <button onClick={() => setShowEngagementModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleSaveEngagement} className="space-y-4 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Client / Company</label>
                  <select
                    value={engForm.clientId}
                    onChange={(e) => setEngForm({ ...engForm, clientId: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="">Select Client...</option>
                    {clients.map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="font-semibold text-gray-700">Engagement Reference</label>
                  <input
                    type="text"
                    value={engForm.engagementReference}
                    onChange={(e) => setEngForm({ ...engForm, engagementReference: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Engagement Title</label>
                <input
                  type="text"
                  value={engForm.title}
                  onChange={(e) => setEngForm({ ...engForm, title: e.target.value })}
                  required
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Financial Year</label>
                  <input
                    type="text"
                    value={engForm.financialYear}
                    onChange={(e) => setEngForm({ ...engForm, financialYear: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                    placeholder="2024-25"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Period Start</label>
                  <input
                    type="date"
                    value={engForm.auditPeriodStart}
                    onChange={(e) => setEngForm({ ...engForm, auditPeriodStart: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Period End</label>
                  <input
                    type="date"
                    value={engForm.auditPeriodEnd}
                    onChange={(e) => setEngForm({ ...engForm, auditPeriodEnd: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">CIN (21 characters)</label>
                  <input
                    type="text"
                    value={engForm.cin}
                    onChange={(e) => setEngForm({ ...engForm, cin: e.target.value.toUpperCase() })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg font-mono text-sm"
                    placeholder="L17110MH1973PLC019786"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Company Type</label>
                  <select
                    value={engForm.companyType}
                    onChange={(e) => setEngForm({ ...engForm, companyType: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="UnlistedPublic">Unlisted Public Company</option>
                    <option value="ListedPublic">Listed Public Company</option>
                    <option value="PrivateCompany">Private Company</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Paid-Up Capital (₹)</label>
                  <input
                    type="number"
                    value={engForm.paidUpShareCapital}
                    onChange={(e) => setEngForm({ ...engForm, paidUpShareCapital: parseFloat(e.target.value) || 0 })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Turnover (₹)</label>
                  <input
                    type="number"
                    value={engForm.turnover}
                    onChange={(e) => setEngForm({ ...engForm, turnover: parseFloat(e.target.value) || 0 })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Outstanding Loans (₹)</label>
                  <input
                    type="number"
                    value={engForm.outstandingBorrowings}
                    onChange={(e) => setEngForm({ ...engForm, outstandingBorrowings: parseFloat(e.target.value) || 0 })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="border-t pt-3">
                <span className="font-bold text-gray-800 text-sm">Practicing Company Secretary / Auditor Particulars</span>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Auditor Name</label>
                  <input
                    type="text"
                    value={engForm.auditorName}
                    onChange={(e) => setEngForm({ ...engForm, auditorName: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                    placeholder="CS Rajesh Gupta"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Auditor Firm Name</label>
                  <input
                    type="text"
                    value={engForm.auditorFirmName}
                    onChange={(e) => setEngForm({ ...engForm, auditorFirmName: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                    placeholder="R. Gupta & Associates"
                  />
                </div>
              </div>

              <div className="grid grid-cols-4 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Membership</label>
                  <select
                    value={engForm.membershipType}
                    onChange={(e) => setEngForm({ ...engForm, membershipType: e.target.value })}
                    className="mt-1 w-full px-2 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="FCS">FCS</option>
                    <option value="ACS">ACS</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Member No.</label>
                  <input
                    type="text"
                    value={engForm.membershipNumber}
                    onChange={(e) => setEngForm({ ...engForm, membershipNumber: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                    placeholder="8899"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">CP No.</label>
                  <input
                    type="text"
                    value={engForm.certificateOfPracticeNumber}
                    onChange={(e) => setEngForm({ ...engForm, certificateOfPracticeNumber: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                    placeholder="12345"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Peer Review No.</label>
                  <input
                    type="text"
                    value={engForm.peerReviewCertificateNumber}
                    onChange={(e) => setEngForm({ ...engForm, peerReviewCertificateNumber: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                    placeholder="PR-9988"
                  />
                </div>
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowEngagementModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 font-semibold"
                >
                  {editingEngId ? 'Save Changes' : 'Create & Seed Checklist'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: Certify with UDIN */}
      {showCertifyModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Certify Form MR-3 Report</h3>
              <button onClick={() => setShowCertifyModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleCertify} className="space-y-4 text-xs">
              <div>
                <label className="font-semibold text-gray-700">Unique Document Identification Number (UDIN)</label>
                <input
                  type="text"
                  value={certifyForm.udin}
                  onChange={(e) => setCertifyForm({ ...certifyForm, udin: e.target.value.toUpperCase() })}
                  required
                  placeholder="e.g. F007766B0009876543"
                  className="mt-1 w-full px-3 py-2 border rounded-lg font-mono text-sm uppercase"
                />
                <span className="text-[11px] text-gray-400">Must be at least 17 alphanumeric characters issued by ICSI.</span>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Certified By (Lead Partner / CS)</label>
                <input
                  type="text"
                  value={certifyForm.certifiedBy}
                  onChange={(e) => setCertifyForm({ ...certifyForm, certifiedBy: e.target.value })}
                  required
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  placeholder="CS Alok Singhi, FCS"
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={certifyForm.udin.trim().length < 17}
                  className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 font-semibold disabled:bg-gray-300"
                >
                  Confirm Certification
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: Checklist Item Add/Edit */}
      {showChecklistModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">
                {editingCheckId ? 'Edit Checklist Clause' : 'Add Custom Checklist Clause'}
              </h3>
              <button onClick={() => setShowChecklistModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleSaveChecklistItem} className="space-y-3 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Statute Category</label>
                  <select
                    value={checkForm.statuteCategory}
                    onChange={(e) => setCheckForm({ ...checkForm, statuteCategory: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="CompaniesAct2013">Companies Act 2013</option>
                    <option value="Fema1999">FEMA 1999</option>
                    <option value="DepositoriesAct1996">Depositories Act 1996</option>
                    <option value="SebiLodr2015">SEBI LODR 2015</option>
                    <option value="SebiPit2015">SEBI PIT 2015</option>
                    <option value="IndustrySpecificLaws">Industry Specific / Other</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Clause Code</label>
                  <input
                    type="text"
                    value={checkForm.clauseCode}
                    onChange={(e) => setCheckForm({ ...checkForm, clauseCode: e.target.value })}
                    required
                    placeholder="e.g. SEC-186"
                    className="mt-1 w-full px-3 py-2 border rounded-lg font-mono text-sm"
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Subject Area</label>
                <input
                  type="text"
                  value={checkForm.subjectArea}
                  onChange={(e) => setCheckForm({ ...checkForm, subjectArea: e.target.value })}
                  required
                  placeholder="e.g. Loans and Investments by Company"
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div>
                <label className="font-semibold text-gray-700">Statutory Requirement</label>
                <textarea
                  value={checkForm.statutoryRequirement}
                  onChange={(e) => setCheckForm({ ...checkForm, statutoryRequirement: e.target.value })}
                  required
                  rows={2}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Compliance Status</label>
                  <select
                    value={checkForm.complianceStatus}
                    onChange={(e) => setCheckForm({ ...checkForm, complianceStatus: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="Complied">Complied</option>
                    <option value="NotComplied">Not Complied</option>
                    <option value="PartiallyComplied">Partially Complied</option>
                    <option value="NotApplicable">Not Applicable</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Deviation Risk</label>
                  <select
                    value={checkForm.deviationRisk}
                    onChange={(e) => setCheckForm({ ...checkForm, deviationRisk: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="None">None</option>
                    <option value="Low">Low</option>
                    <option value="Medium">Medium</option>
                    <option value="High">High</option>
                    <option value="Critical">Critical</option>
                  </select>
                </div>
              </div>

              <div className="flex items-center gap-2 pt-1">
                <input
                  type="checkbox"
                  id="isQualCheck"
                  checked={checkForm.isQualification}
                  onChange={(e) => setCheckForm({ ...checkForm, isQualification: e.target.checked })}
                  className="h-4 w-4 text-indigo-600 rounded"
                />
                <label htmlFor="isQualCheck" className="font-semibold text-gray-700 cursor-pointer">
                  Highlight as formal Qualification / Reservation in Form MR-3
                </label>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Auditor Review Remarks</label>
                <textarea
                  value={checkForm.auditorRemarks}
                  onChange={(e) => setCheckForm({ ...checkForm, auditorRemarks: e.target.value })}
                  rows={2}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  placeholder="Records examined, minutes book cross-referenced..."
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowChecklistModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 font-semibold"
                >
                  Save Clause
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 5: Filing Audit Add/Edit */}
      {showFilingModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">
                {editingFilingId ? 'Edit MCA Form Filing Audit' : 'Add MCA Form Filing Record'}
              </h3>
              <button onClick={() => setShowFilingModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleSaveFiling} className="space-y-3 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Form Name</label>
                  <select
                    value={filingForm.formName}
                    onChange={(e) => setFilingForm({ ...filingForm, formName: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm font-semibold"
                  >
                    <option value="AOC-4">AOC-4 (Financial Statements)</option>
                    <option value="MGT-7">MGT-7 (Annual Return)</option>
                    <option value="DIR-12">DIR-12 (Directors/KMP Change)</option>
                    <option value="CHG-1">CHG-1 (Charge Creation)</option>
                    <option value="CHG-4">CHG-4 (Charge Satisfaction)</option>
                    <option value="DPT-3">DPT-3 (Return of Deposits)</option>
                    <option value="BEN-2">BEN-2 (SBO Declaration)</option>
                    <option value="MGT-14">MGT-14 (Filing of Resolutions)</option>
                    <option value="MSME-1">MSME-1 (Half-yearly MSME Overdue)</option>
                    <option value="PAS-3">PAS-3 (Allotment of Shares)</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Statutory Days Allowed</label>
                  <input
                    type="number"
                    value={filingForm.statutoryDaysAllowed}
                    onChange={(e) => setFilingForm({ ...filingForm, statutoryDaysAllowed: parseInt(e.target.value) || 0 })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                    placeholder="30"
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Description / Purpose</label>
                <input
                  type="text"
                  value={filingForm.description}
                  onChange={(e) => setFilingForm({ ...filingForm, description: e.target.value })}
                  required
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Trigger Event Date</label>
                  <input
                    type="date"
                    value={filingForm.eventDate}
                    onChange={(e) => setFilingForm({ ...filingForm, eventDate: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Actual Filing Date (Leave blank if pending)</label>
                  <input
                    type="date"
                    value={filingForm.actualFilingDate || ''}
                    onChange={(e) => setFilingForm({ ...filingForm, actualFilingDate: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">MCA SRN (Service Request Number)</label>
                  <input
                    type="text"
                    value={filingForm.srn}
                    onChange={(e) => setFilingForm({ ...filingForm, srn: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg font-mono text-sm"
                    placeholder="F12345678"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Additional Fee Paid (₹)</label>
                  <input
                    type="number"
                    value={filingForm.additionalFeePaid}
                    onChange={(e) => setFilingForm({ ...filingForm, additionalFeePaid: parseFloat(e.target.value) || 0 })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Auditor Remarks</label>
                <textarea
                  value={filingForm.auditorRemarks}
                  onChange={(e) => setFilingForm({ ...filingForm, auditorRemarks: e.target.value })}
                  rows={2}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  placeholder="Challan verified from MCA portal..."
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowFilingModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 font-semibold"
                >
                  Save Filing Record
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 6: Meeting Audit Add/Edit */}
      {showMeetingModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">
                {editingMeetingId ? 'Edit Meeting Record' : 'Add Board/Committee Meeting'}
              </h3>
              <button onClick={() => setShowMeetingModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleSaveMeeting} className="space-y-3 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Meeting Type</label>
                  <select
                    value={meetingForm.meetingType}
                    onChange={(e) => setMeetingForm({ ...meetingForm, meetingType: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm font-semibold"
                  >
                    <option value="BoardMeeting">Board Meeting</option>
                    <option value="AuditCommittee">Audit Committee</option>
                    <option value="NominationRemunerationCommittee">Nomination & Remuneration Committee</option>
                    <option value="StakeholdersRelationshipCommittee">Stakeholders Relationship Committee</option>
                    <option value="CsrCommittee">CSR Committee</option>
                    <option value="AnnualGeneralMeeting">Annual General Meeting (AGM)</option>
                    <option value="ExtraordinaryGeneralMeeting">Extraordinary General Meeting (EGM)</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Meeting Date</label>
                  <input
                    type="date"
                    value={meetingForm.meetingDate}
                    onChange={(e) => setMeetingForm({ ...meetingForm, meetingDate: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Previous Meeting Date (for 120-day gap)</label>
                  <input
                    type="date"
                    value={meetingForm.previousMeetingDate || ''}
                    onChange={(e) => setMeetingForm({ ...meetingForm, previousMeetingDate: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Notice Dispatch Date (for 7-day rule)</label>
                  <input
                    type="date"
                    value={meetingForm.noticeDispatchDate || ''}
                    onChange={(e) => setMeetingForm({ ...meetingForm, noticeDispatchDate: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Total Members</label>
                  <input
                    type="number"
                    value={meetingForm.totalMembersCount}
                    onChange={(e) => setMeetingForm({ ...meetingForm, totalMembersCount: parseInt(e.target.value) || 0 })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Attended Count</label>
                  <input
                    type="number"
                    value={meetingForm.attendedMembersCount}
                    onChange={(e) => setMeetingForm({ ...meetingForm, attendedMembersCount: parseInt(e.target.value) || 0 })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Independent Present</label>
                  <input
                    type="number"
                    value={meetingForm.independentDirectorsAttended}
                    onChange={(e) => setMeetingForm({ ...meetingForm, independentDirectorsAttended: parseInt(e.target.value) || 0 })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="flex items-center gap-2 pt-1">
                <input
                  type="checkbox"
                  id="ssCompCheck"
                  checked={meetingForm.secretarialStandardComplied}
                  onChange={(e) => setMeetingForm({ ...meetingForm, secretarialStandardComplied: e.target.checked })}
                  className="h-4 w-4 text-indigo-600 rounded"
                />
                <label htmlFor="ssCompCheck" className="font-semibold text-gray-700 cursor-pointer">
                  Secretarial Standard SS-1 / SS-2 Complied (Agenda notes, minutes timing)
                </label>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Auditor Remarks</label>
                <textarea
                  value={meetingForm.auditorRemarks}
                  onChange={(e) => setMeetingForm({ ...meetingForm, auditorRemarks: e.target.value })}
                  rows={2}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  placeholder="Quorum verified from signed attendance sheet..."
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowMeetingModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 font-semibold"
                >
                  Save Meeting Record
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
