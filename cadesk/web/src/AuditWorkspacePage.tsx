import { useEffect, useRef, useState } from 'react'
import { PdfActions } from './pdf/PdfActions'
import {
  type AuditEngagementDto,
  type AuditWorkingPaperDto,
  type AuditSummaryDto,
  type AuditEqrReviewDto,
  type AuditReviewNoteDto,
  type LeadScheduleDto,
  type AuditDossierDto,
  type ConfirmationCampaignDto,
  type ExternalConfirmationRequestDto,
  type CreateConfirmationCampaignRequest,
  type CreateConfirmationRequest,
  type UpdateConfirmationAuditorEvaluationRequest,
  type IndependentAuditorReportDto,
  type NotesToAccountsDto,
  type GenerateAuditorReportRequest,
  type Client,
  type ClientPage,
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

  // Selected engagement sub-view: workpapers, lead_schedules, dossier_vault, confirmations, auditor_report, eqr, review_notes
  const [activeTab, setActiveTab] = useState<'workpapers' | 'lead_schedules' | 'dossier_vault' | 'confirmations' | 'auditor_report' | 'eqr' | 'review_notes'>('workpapers')

  // Data for active engagement
  const [workingPapers, setWorkingPapers] = useState<AuditWorkingPaperDto[]>([])
  const [leadSchedules, setLeadSchedules] = useState<LeadScheduleDto[]>([])
  const [dossier, setDossier] = useState<AuditDossierDto | null>(null)
  const reportRef = useRef<HTMLDivElement>(null)
  const canSaveDocs = !!me?.permissions.includes('documents.upload')
  const [eqrReview, setEqrReview] = useState<AuditEqrReviewDto | null>(null)
  const [reviewNotes, setReviewNotes] = useState<AuditReviewNoteDto[]>([])
  const [loadingWps, setLoadingWps] = useState(false)

  // Sprint 45: SA 505 External Confirmations State
  const [confirmationCampaigns, setConfirmationCampaigns] = useState<ConfirmationCampaignDto[]>([])
  const [selectedCampaignId, setSelectedCampaignId] = useState<string>('')
  const [confirmationRequests, setConfirmationRequests] = useState<ExternalConfirmationRequestDto[]>([])
  const [loadingConfirmations, setLoadingConfirmations] = useState<boolean>(false)
  const [showNewCampaignModal, setShowNewCampaignModal] = useState<boolean>(false)
  const [showNewReqModal, setShowNewReqModal] = useState<boolean>(false)
  const [evaluatingReq, setEvaluatingReq] = useState<ExternalConfirmationRequestDto | null>(null)
  const [filterPartyType, setFilterPartyType] = useState<string>('All')
  const [filterConfStatus, setFilterConfStatus] = useState<string>('All')

  const [campaignForm, setCampaignForm] = useState<CreateConfirmationCampaignRequest>({
    title: 'FY 2024-25 External Balance Confirmations',
    description: 'Year-end debtor and creditor balance circularization under SA 505',
    asOfDate: '2025-03-31',
    cutoffDate: '2025-04-30'
  })

  const [reqForm, setReqForm] = useState<CreateConfirmationRequest>({
    leadScheduleRef: 'B',
    partyType: 'TradeDebtor',
    partyName: '',
    partyContactEmail: '',
    partyContactPhone: '',
    partyPanGstin: '',
    confirmationType: 'PositiveStatedBalance',
    bookBalanceAmount: 0,
    asOfDate: '2025-03-31'
  })

  const [evaluationForm, setEvaluationForm] = useState<UpdateConfirmationAuditorEvaluationRequest>({
    status: 'ConfirmedAgreed',
    alternativeProceduresPerformed: '',
    auditorConclusion: 'ReconciledSatisfactory',
    proposedAdjustmentAmount: 0,
    auditorNotes: ''
  })

  // Sprint 46: 1-Click ICAI Independent Auditor's Report & Notes State
  const [auditorReport, setAuditorReport] = useState<IndependentAuditorReportDto | null>(null)
  const [notesToAccounts, setNotesToAccounts] = useState<NotesToAccountsDto | null>(null)
  const [loadingReport, setLoadingReport] = useState<boolean>(false)
  const [reportSubTab, setReportSubTab] = useState<'report' | 'notes'>('report')
  const [showGenerateReportModal, setShowGenerateReportModal] = useState<boolean>(false)
  const [reportGenForm, setReportGenForm] = useState<GenerateAuditorReportRequest>({
    opinionType: 'Unmodified',
    placeOfSignature: 'Mumbai',
    qualificationDetails: '',
    includeKeyAuditMatters: true,
    emphasisOfMatter: '',
    materialUncertaintyGoingConcern: ''
  })
  const [showUdinModal, setShowUdinModal] = useState<boolean>(false)
  const [udinCode, setUdinCode] = useState<string>('')
  const [udinDate, setUdinDate] = useState<string>(new Date().toISOString().split('T')[0])

  // Modals & Forms for Lead Schedules and SA 230 Dossier
  const [editingSchedule, setEditingSchedule] = useState<LeadScheduleDto | null>(null)
  const [leadScheduleForm, setLeadScheduleForm] = useState({
    auditAdjustmentsDebit: 0,
    auditAdjustmentsCredit: 0,
    priorYearBalance: 0,
    auditProceduresSummary: '',
    exceptionsNoted: '',
    status: 'InReview',
    signOff: false
  })
  const [showSealModal, setShowSealModal] = useState(false)
  const [sealRemarks, setSealRemarks] = useState('Reviewed workpapers, lead schedules, and CARO 2020 matrix. Approved for issuance under SA 700 & sealed under SQC 1.')
  const [compilingDossier, setCompilingDossier] = useState(false)

  // Existing Modals
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
        api<ClientPage | Client[]>('/api/clients?take=200')
      ])
      const clientList = Array.isArray(clRes) ? clRes : (clRes?.items || [])
      setSummary(sumRes)
      setEngagements(engRes || [])
      setClients(clientList)
      if (clientList.length > 0 && !engForm.clientId) {
        setEngForm(prev => ({ ...prev, clientId: clientList[0].id }))
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
      const [wps, eqr, notes, eng, lss, dos, campaigns, rep, nts] = await Promise.all([
        auditApi.listWorkingPapers(engId),
        auditApi.getEqrReview(engId).catch(() => null),
        auditApi.listReviewNotes(engId),
        auditApi.getEngagement(engId),
        auditApi.getLeadSchedules(engId).catch(() => []),
        auditApi.getDossier(engId).catch(() => null),
        auditApi.getConfirmationCampaigns(engId).catch(() => []),
        auditApi.getAuditorReport(engId).catch(() => null),
        auditApi.getNotesToAccounts(engId).catch(() => null)
      ])
      setWorkingPapers(wps || [])
      setEqrReview(eqr)
      setReviewNotes(notes || [])
      setSelectedEng(eng)
      setLeadSchedules(lss || [])
      setDossier(dos)
      setConfirmationCampaigns(campaigns || [])
      setAuditorReport(rep)
      setNotesToAccounts(nts)
      if (campaigns && campaigns.length > 0) {
        setSelectedCampaignId(campaigns[0].id)
        loadConfirmationRequests(campaigns[0].id)
      } else {
        setSelectedCampaignId('')
        setConfirmationRequests([])
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load engagement details.')
    } finally {
      setLoadingWps(false)
    }
  }

  async function loadConfirmationRequests(campaignId: string) {
    if (!campaignId) return
    try {
      setLoadingConfirmations(true)
      const list = await auditApi.getConfirmationRequests(campaignId)
      setConfirmationRequests(list || [])
    } catch (err: unknown) {
      console.error(err)
    } finally {
      setLoadingConfirmations(false)
    }
  }

  async function handleCreateCampaign() {
    if (!selectedEng) return
    try {
      setLoadingConfirmations(true)
      const created = await auditApi.createConfirmationCampaign(selectedEng.id, campaignForm)
      setConfirmationCampaigns(prev => [created, ...prev])
      setSelectedCampaignId(created.id)
      setShowNewCampaignModal(false)
      loadConfirmationRequests(created.id)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create confirmation campaign.')
    } finally {
      setLoadingConfirmations(false)
    }
  }

  async function handleCreateRequest() {
    if (!selectedCampaignId) return
    try {
      setLoadingConfirmations(true)
      const created = await auditApi.createConfirmationRequest(selectedCampaignId, reqForm)
      setConfirmationRequests(prev => [...prev, created])
      setShowNewReqModal(false)
      setReqForm({
        leadScheduleRef: 'B',
        partyType: 'TradeDebtor',
        partyName: '',
        partyContactEmail: '',
        partyContactPhone: '',
        partyPanGstin: '',
        confirmationType: 'PositiveStatedBalance',
        bookBalanceAmount: 0,
        asOfDate: '2025-03-31'
      })
      if (selectedEng) {
        const updated = await auditApi.getConfirmationCampaigns(selectedEng.id)
        setConfirmationCampaigns(updated)
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create confirmation request.')
    } finally {
      setLoadingConfirmations(false)
    }
  }

  async function handleAutoPopulateConfirmations() {
    if (!selectedCampaignId) return
    try {
      setLoadingConfirmations(true)
      const items = await auditApi.autoPopulateConfirmations(selectedCampaignId, {
        minimumDebtorBalance: 50000,
        minimumCreditorBalance: 50000,
        topNCount: 25
      })
      await loadConfirmationRequests(selectedCampaignId)
      if (selectedEng) {
        const updated = await auditApi.getConfirmationCampaigns(selectedEng.id)
        setConfirmationCampaigns(updated)
      }
      alert(`Auto-populated ${items.length} debtors and creditors from Trial Balance!`)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to auto-populate from Trial Balance.')
    } finally {
      setLoadingConfirmations(false)
    }
  }

  async function handleSendConfirmationRequests() {
    if (!selectedCampaignId) return
    try {
      setLoadingConfirmations(true)
      const res = await auditApi.sendConfirmationRequests(selectedCampaignId)
      await loadConfirmationRequests(selectedCampaignId)
      if (selectedEng) {
        const updated = await auditApi.getConfirmationCampaigns(selectedEng.id)
        setConfirmationCampaigns(updated)
      }
      alert(`Dispatched ${res.sentCount} balance confirmation request links!`)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to send confirmation requests.')
    } finally {
      setLoadingConfirmations(false)
    }
  }

  async function handleUpdateEvaluation() {
    if (!evaluatingReq) return
    try {
      setLoadingConfirmations(true)
      await auditApi.updateConfirmationEvaluation(evaluatingReq.id, evaluationForm)
      setEvaluatingReq(null)
      if (selectedCampaignId) {
        await loadConfirmationRequests(selectedCampaignId)
      }
      if (selectedEng) {
        const updated = await auditApi.getConfirmationCampaigns(selectedEng.id)
        setConfirmationCampaigns(updated)
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to save evaluation.')
    } finally {
      setLoadingConfirmations(false)
    }
  }

  async function handleExportConfirmationSummaryHtml() {
    if (!selectedCampaignId) return
    try {
      const res = await auditApi.exportConfirmationSummaryHtml(selectedCampaignId)
      const w = window.open('', '_blank')
      if (w) {
        w.document.write(res.htmlContent)
        w.document.close()
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to export SA 505 Summary.')
    }
  }

  async function handleExportConfirmationLetterHtml(requestId: string) {
    try {
      const html = await auditApi.getConfirmationLetterHtml(requestId)
      const w = window.open('', '_blank')
      if (w) {
        w.document.write(html)
        w.document.close()
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to export letter.')
    }
  }

  async function handleGenerateAuditorReport() {
    if (!selectedEng) return
    try {
      setLoadingReport(true)
      const rep = await auditApi.generateAuditorReport(selectedEng.id, reportGenForm)
      setAuditorReport(rep)
      setShowGenerateReportModal(false)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to generate auditor report.')
    } finally {
      setLoadingReport(false)
    }
  }

  async function handleGenerateNotesToAccounts() {
    if (!selectedEng) return
    try {
      setLoadingReport(true)
      const nts = await auditApi.generateNotesToAccounts(selectedEng.id, {})
      setNotesToAccounts(nts)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to generate notes to accounts.')
    } finally {
      setLoadingReport(false)
    }
  }

  async function handleIssueUdin() {
    if (!auditorReport || !selectedEng) return
    if (!/^[0-9]{2}[0-9]{6}[A-Z0-9]{4}[A-Z0-9]{6}$/.test(udinCode.trim())) {
      setError('Invalid UDIN format. Must be 18 alphanumeric characters (e.g. 25123456AAAA123456).')
      return
    }
    try {
      setLoadingReport(true)
      const rep = await auditApi.issueReportWithUdin(auditorReport.id, {
        udin: udinCode.trim().toUpperCase(),
        reportDate: udinDate
      })
      setAuditorReport(rep)
      setShowUdinModal(false)
      setUdinCode('')
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to issue UDIN.')
    } finally {
      setLoadingReport(false)
    }
  }

  async function handleExportAuditorReportHtml() {
    if (!selectedEng) return
    try {
      const res = await auditApi.exportAuditorReportHtml(selectedEng.id)
      const w = window.open('', '_blank')
      if (w) {
        w.document.write(res.htmlContent)
        w.document.close()
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to export auditor report HTML.')
    }
  }

  async function handleExportNotesHtml() {
    if (!selectedEng) return
    try {
      const res = await auditApi.exportNotesToAccountsHtml(selectedEng.id)
      const w = window.open('', '_blank')
      if (w) {
        w.document.write(res.htmlContent)
        w.document.close()
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to export notes HTML.')
    }
  }

  async function handleGenerateLeadSchedules() {
    if (!selectedEng) return
    try {
      setLoadingWps(true)
      const res = await auditApi.generateLeadSchedules(selectedEng.id)
      setLeadSchedules(res || [])
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to generate lead schedules.')
    } finally {
      setLoadingWps(false)
    }
  }

  function handleStartEditSchedule(s: LeadScheduleDto) {
    setEditingSchedule(s)
    setLeadScheduleForm({
      auditAdjustmentsDebit: s.auditAdjustmentsDebit,
      auditAdjustmentsCredit: s.auditAdjustmentsCredit,
      priorYearBalance: s.priorYearBalance,
      auditProceduresSummary: s.auditProceduresSummary || '',
      exceptionsNoted: s.exceptionsNoted || '',
      status: s.status,
      signOff: false
    })
  }

  async function handleSaveLeadSchedule() {
    if (!editingSchedule) return
    try {
      const updated = await auditApi.updateLeadSchedule(editingSchedule.id, leadScheduleForm)
      setLeadSchedules(prev => prev.map(s => s.id === updated.id ? updated : s))
      setEditingSchedule(null)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update lead schedule.')
    }
  }

  async function handleCompileDossier() {
    if (!selectedEng) return
    try {
      setCompilingDossier(true)
      const res = await auditApi.compileDossier(selectedEng.id, { dossierType: 'COMBINED_PEER_REVIEW' })
      setDossier(res)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to compile audit dossier.')
    } finally {
      setCompilingDossier(false)
    }
  }

  async function handleSealDossier() {
    if (!dossier) return
    try {
      const sealed = await auditApi.sealDossier(dossier.id, { partnerSignoffRemarks: sealRemarks })
      setDossier(sealed)
      setShowSealModal(false)
      if (selectedEng) await loadEngagementDetails(selectedEng.id)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to seal audit dossier.')
    }
  }

  async function handleExportHtml() {
    if (!selectedEng) return
    try {
      const res = await auditApi.exportDossierHtml(selectedEng.id)
      const w = window.open('', '_blank')
      if (w) {
        w.document.write(res.htmlContent)
        w.document.close()
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to export dossier HTML.')
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
              onClick={() => setActiveTab('lead_schedules')}
              style={{
                padding: '10px 18px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'lead_schedules' ? '2px solid #2563eb' : '2px solid transparent',
                color: activeTab === 'lead_schedules' ? '#2563eb' : '#64748b',
                fontWeight: 600,
                fontSize: '14px',
                cursor: 'pointer',
                marginBottom: '-2px',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>Lead Schedules A–Q</span>
              <span style={{
                padding: '1px 6px',
                borderRadius: '10px',
                fontSize: '10px',
                background: '#e0f2fe',
                color: '#0369a1'
              }}>
                {leadSchedules.length}
              </span>
            </button>
            <button
              onClick={() => setActiveTab('dossier_vault')}
              style={{
                padding: '10px 18px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'dossier_vault' ? '2px solid #2563eb' : '2px solid transparent',
                color: activeTab === 'dossier_vault' ? '#2563eb' : '#64748b',
                fontWeight: 600,
                fontSize: '14px',
                cursor: 'pointer',
                marginBottom: '-2px',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>📑 SA 230 Dossier Vault</span>
              <span style={{
                padding: '1px 6px',
                borderRadius: '10px',
                fontSize: '10px',
                background: dossier?.status === 'sealed_sqc1' ? '#dcfce7' : (dossier ? '#fef3c7' : '#f1f5f9'),
                color: dossier?.status === 'sealed_sqc1' ? '#15803d' : (dossier ? '#92400e' : '#64748b')
              }}>
                {dossier ? (dossier.status === 'sealed_sqc1' ? 'SEALED' : 'COMPILED') : 'READY'}
              </span>
            </button>
            <button
              onClick={() => {
                setActiveTab('confirmations')
                if (selectedCampaignId) loadConfirmationRequests(selectedCampaignId)
              }}
              style={{
                padding: '10px 18px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'confirmations' ? '2px solid #2563eb' : '2px solid transparent',
                color: activeTab === 'confirmations' ? '#2563eb' : '#64748b',
                fontWeight: 600,
                fontSize: '14px',
                cursor: 'pointer',
                marginBottom: '-2px',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>✉️ SA 505 Confirmations</span>
              <span style={{
                padding: '1px 6px',
                borderRadius: '10px',
                fontSize: '10px',
                background: '#e0e7ff',
                color: '#4338ca'
              }}>
                {confirmationRequests.length}
              </span>
            </button>
            <button
              onClick={() => setActiveTab('auditor_report')}
              style={{
                padding: '10px 18px',
                border: 'none',
                background: 'none',
                borderBottom: activeTab === 'auditor_report' ? '2px solid #2563eb' : '2px solid transparent',
                color: activeTab === 'auditor_report' ? '#2563eb' : '#64748b',
                fontWeight: 600,
                fontSize: '14px',
                cursor: 'pointer',
                marginBottom: '-2px',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>📜 Auditor's Report &amp; Notes</span>
              <span style={{
                padding: '1px 6px',
                borderRadius: '10px',
                fontSize: '10px',
                background: auditorReport?.udin ? '#dcfce7' : (auditorReport ? '#fef3c7' : '#f1f5f9'),
                color: auditorReport?.udin ? '#15803d' : (auditorReport ? '#92400e' : '#64748b')
              }}>
                {auditorReport?.udin ? 'UDIN ISSUED' : (auditorReport ? auditorReport.opinionType.toUpperCase() : 'READY')}
              </span>
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

          {/* TAB 2: Lead Schedules (A to Q) */}
          {activeTab === 'lead_schedules' && (
            <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #e2e8f0', paddingBottom: '12px' }}>
                <div>
                  <h3 style={{ fontSize: '16px', fontWeight: 600, margin: 0 }}>Lead Schedules Directory (A–Q)</h3>
                  <p style={{ fontSize: '12px', color: '#64748b', margin: '4px 0 0 0' }}>
                    Substantive Analytical Groupings mapped to ICAI Standards on Auditing (SA 500 / SA 520) &amp; Schedule III
                  </p>
                </div>
                {!selectedEng.isArchived && (
                  <button
                    onClick={handleGenerateLeadSchedules}
                    style={{ padding: '8px 16px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '12px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
                  >
                    🔄 Ingest / Sync from Trial Balance
                  </button>
                )}
              </div>

              {leadSchedules.length === 0 ? (
                <div style={{ padding: '36px', textAlign: 'center', color: '#64748b' }}>
                  <p style={{ margin: '0 0 12px 0', fontSize: '14px' }}>No lead schedules generated yet for this engagement.</p>
                  <button
                    onClick={handleGenerateLeadSchedules}
                    style={{ padding: '8px 18px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
                  >
                    Generate Standard A–Q Lead Schedules from Trial Balance
                  </button>
                </div>
              ) : (
                <div style={{ overflowX: 'auto' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                    <thead>
                      <tr style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0' }}>
                        <th style={{ padding: '10px 8px', textAlign: 'left' }}>Code</th>
                        <th style={{ padding: '10px 8px', textAlign: 'left' }}>Lead Schedule Title</th>
                        <th style={{ padding: '10px 8px', textAlign: 'left' }}>Category</th>
                        <th style={{ padding: '10px 8px', textAlign: 'right' }}>Unadjusted (₹)</th>
                        <th style={{ padding: '10px 8px', textAlign: 'right' }}>Dr Adj (₹)</th>
                        <th style={{ padding: '10px 8px', textAlign: 'right' }}>Cr Adj (₹)</th>
                        <th style={{ padding: '10px 8px', textAlign: 'right' }}>Audited Final (₹)</th>
                        <th style={{ padding: '10px 8px', textAlign: 'right' }}>Prior Year (₹)</th>
                        <th style={{ padding: '10px 8px', textAlign: 'right' }}>Variance %</th>
                        <th style={{ padding: '10px 8px', textAlign: 'center' }}>Status</th>
                        <th style={{ padding: '10px 8px', textAlign: 'center' }}>Action</th>
                      </tr>
                    </thead>
                    <tbody>
                      {leadSchedules.map(ls => (
                        <tr key={ls.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                          <td style={{ padding: '10px 8px', fontWeight: 700, color: '#0f172a' }}>{ls.leadCode}</td>
                          <td style={{ padding: '10px 8px' }}>
                            <div style={{ fontWeight: 600, color: '#1e293b' }}>{ls.leadTitle}</div>
                            {ls.auditProceduresSummary && <div style={{ fontSize: '11px', color: '#64748b' }}>{ls.auditProceduresSummary}</div>}
                            {ls.exceptionsNoted && <div style={{ fontSize: '11px', color: '#dc2626', fontWeight: 600 }}>⚠️ {ls.exceptionsNoted}</div>}
                          </td>
                          <td style={{ padding: '10px 8px' }}>
                            <span style={{ padding: '2px 6px', borderRadius: '4px', fontSize: '10px', fontWeight: 600, background: ls.scheduleCategory === 'Asset' ? '#e0f2fe' : ls.scheduleCategory === 'Liability' ? '#fef3c7' : ls.scheduleCategory === 'Income' ? '#dcfce7' : '#fee2e2', color: '#334155' }}>
                              {ls.scheduleCategory}
                            </span>
                          </td>
                          <td style={{ padding: '10px 8px', textAlign: 'right', fontWeight: 500 }}>{fmtInr(ls.unadjustedBookBalance)}</td>
                          <td style={{ padding: '10px 8px', textAlign: 'right', color: ls.auditAdjustmentsDebit > 0 ? '#16a34a' : '#94a3b8' }}>
                            {ls.auditAdjustmentsDebit > 0 ? fmtInr(ls.auditAdjustmentsDebit) : '-'}
                          </td>
                          <td style={{ padding: '10px 8px', textAlign: 'right', color: ls.auditAdjustmentsCredit > 0 ? '#dc2626' : '#94a3b8' }}>
                            {ls.auditAdjustmentsCredit > 0 ? fmtInr(ls.auditAdjustmentsCredit) : '-'}
                          </td>
                          <td style={{ padding: '10px 8px', textAlign: 'right', fontWeight: 700, color: '#0f172a' }}>{fmtInr(ls.auditedBalance)}</td>
                          <td style={{ padding: '10px 8px', textAlign: 'right', color: '#64748b' }}>{fmtInr(ls.priorYearBalance)}</td>
                          <td style={{ padding: '10px 8px', textAlign: 'right' }}>
                            <span style={{ fontWeight: 600, color: ls.isMaterialVariance ? '#dc2626' : '#64748b' }}>
                              {ls.variancePercentage > 0 ? `+${ls.variancePercentage}%` : `${ls.variancePercentage}%`}
                            </span>
                            {ls.isMaterialVariance && <span title="Material variance (>20% or > Materiality)" style={{ marginLeft: '4px', color: '#dc2626', fontSize: '11px' }}>⚠️</span>}
                          </td>
                          <td style={{ padding: '10px 8px', textAlign: 'center' }}>
                            <span style={{ padding: '2px 8px', borderRadius: '12px', fontSize: '11px', fontWeight: 600, background: ls.status === 'Approved' ? '#dcfce7' : '#f1f5f9', color: ls.status === 'Approved' ? '#15803d' : '#475569' }}>
                              {ls.status}
                            </span>
                          </td>
                          <td style={{ padding: '10px 8px', textAlign: 'center' }}>
                            {!selectedEng.isArchived && (
                              <button
                                onClick={() => handleStartEditSchedule(ls)}
                                style={{ padding: '4px 10px', fontSize: '11px', background: '#f8fafc', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer', fontWeight: 600, color: '#2563eb' }}
                              >
                                Edit / Adjust
                              </button>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {/* TAB 3: SA 230 Digital Audit Dossier & NFRA Defense Vault */}
          {activeTab === 'dossier_vault' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
              {/* Hero Banner */}
              <div style={{ background: 'linear-gradient(135deg, #0f172a 0%, #1e293b 100%)', color: '#fff', padding: '24px', borderRadius: '10px', boxShadow: '0 4px 6px -1px rgba(0,0,0,0.1)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '16px' }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '8px' }}>
                      <span style={{ fontSize: '24px' }}>🛡️</span>
                      <h2 style={{ fontSize: '20px', fontWeight: 700, margin: 0, color: '#f8fafc' }}>
                        ICAI SA 230 Digital Audit File &amp; Peer Review Vault
                      </h2>
                    </div>
                    <p style={{ margin: 0, fontSize: '13px', color: '#94a3b8', maxWidth: '640px' }}>
                      Standard on Auditing 230 (Audit Documentation), SA 220 (Quality Control), and NFRA / ICAI Quality Review Board Inspection Shield. Assembles lead schedules, sampling tests, and working papers into a tamper-evident dossier.
                    </p>
                  </div>
                  <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap' }}>
                    <button
                      onClick={handleCompileDossier}
                      disabled={compilingDossier || selectedEng.isArchived}
                      style={{ padding: '10px 18px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: compilingDossier ? 'not-allowed' : 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
                    >
                      {compilingDossier ? '⏳ Compiling...' : '⚡ 1-Click Compile Audit Dossier'}
                    </button>
                    {dossier && (
                      <>
                        <button
                          onClick={handleExportHtml}
                          style={{ padding: '10px 18px', background: '#334155', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
                        >
                          🖨️ Export / Print Dossier
                        </button>
                        <PdfActions
                          title={`Audit dossier - ${selectedEng.clientName ?? 'Client'} - FY ${selectedEng.financialYear}`}
                          options={{ author: me?.firmName, header: `${selectedEng.clientName ?? ''}  FY ${selectedEng.financialYear}`, footer: `${me?.firmName ?? ''}  SHA-256 ${dossier.integritySha256.slice(0, 16)}…` }}
                          source={async () => (await auditApi.exportDossierHtml(selectedEng.id)).htmlContent}
                          clientId={selectedEng.clientId}
                          fy={selectedEng.financialYear}
                          canSave={canSaveDocs}
                        />
                        {dossier.status !== 'sealed_sqc1' && !selectedEng.isArchived && (
                          <button
                            onClick={() => setShowSealModal(true)}
                            style={{ padding: '10px 18px', background: '#16a34a', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
                          >
                            🔒 Seal &amp; Lock File (SQC 1)
                          </button>
                        )}
                      </>
                    )}
                  </div>
                </div>

                {/* Key Status Row */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px', marginTop: '20px', borderTop: '1px solid #334155', paddingTop: '16px' }}>
                  <div>
                    <div style={{ fontSize: '11px', color: '#94a3b8', textTransform: 'uppercase', fontWeight: 600 }}>Dossier Status</div>
                    <div style={{ fontSize: '15px', fontWeight: 700, color: dossier?.status === 'sealed_sqc1' ? '#4ade80' : dossier ? '#fbbf24' : '#cbd5e1' }}>
                      {dossier ? (dossier.status === 'sealed_sqc1' ? 'SEALED & ARCHIVED (SQC 1)' : 'COMPILED (READY)') : 'UNCOMPILED'}
                    </div>
                  </div>
                  <div>
                    <div style={{ fontSize: '11px', color: '#94a3b8', textTransform: 'uppercase', fontWeight: 600 }}>NFRA Inspection Readiness</div>
                    <div style={{ fontSize: '15px', fontWeight: 700, color: dossier?.nfraInspectionReady ? '#4ade80' : '#f87171' }}>
                      {dossier?.nfraInspectionReady ? '✓ INSPECTION READY' : '⚠️ ACTIONS PENDING'}
                    </div>
                  </div>
                  <div>
                    <div style={{ fontSize: '11px', color: '#94a3b8', textTransform: 'uppercase', fontWeight: 600 }}>Total Evidence Packed</div>
                    <div style={{ fontSize: '15px', fontWeight: 700, color: '#f8fafc' }}>
                      {leadSchedules.length} Leads • {workingPapers.length} WPs
                    </div>
                  </div>
                  <div>
                    <div style={{ fontSize: '11px', color: '#94a3b8', textTransform: 'uppercase', fontWeight: 600 }}>SQC 1 Archival Deadline</div>
                    <div style={{ fontSize: '15px', fontWeight: 700, color: selectedEng.isArchived ? '#4ade80' : '#cbd5e1' }}>
                      {selectedEng.archivalStatus}
                    </div>
                  </div>
                </div>

                {/* Cryptographic Hash Card */}
                {dossier && (
                  <div style={{ marginTop: '16px', background: 'rgba(255,255,255,0.06)', borderRadius: '6px', padding: '12px 16px', border: '1px solid rgba(255,255,255,0.1)' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                      <span style={{ fontSize: '12px', color: '#38bdf8', fontWeight: 600 }}>🔒 Cryptographic Integrity Digest (SHA-256)</span>
                      <span style={{ fontSize: '11px', color: '#94a3b8' }}>Version {dossier.version} • Compiled {new Date(dossier.compiledAt).toLocaleDateString()}</span>
                    </div>
                    <div style={{ fontFamily: 'monospace', fontSize: '13px', color: '#f1f5f9', wordBreak: 'break-all', marginTop: '4px' }}>
                      {dossier.integritySha256}
                    </div>
                  </div>
                )}
              </div>

              {/* 9-Section ICAI Peer Review Index */}
              <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                <h3 style={{ fontSize: '16px', fontWeight: 600, margin: '0 0 16px 0' }}>
                  ICAI Peer Review &amp; NFRA Inspection 9-Section Master Index
                </h3>
                {(!dossier || !dossier.indexSections || dossier.indexSections.length === 0) ? (
                  <div style={{ textAlign: 'center', padding: '24px', color: '#64748b' }}>
                    Click <strong>"⚡ 1-Click Compile Audit Dossier"</strong> above to generate the structured 9-section master index.
                  </div>
                ) : (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                    {dossier.indexSections.map(sec => (
                      <div key={sec.indexCode} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '12px 16px', borderRadius: '6px', border: '1px solid #e2e8f0', background: sec.status === 'Complete' ? '#f0fdf4' : '#fffbeb' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                          <span style={{ fontWeight: 700, fontSize: '13px', padding: '3px 8px', borderRadius: '4px', background: '#e2e8f0', color: '#0f172a' }}>
                            {sec.indexCode}
                          </span>
                          <div>
                            <div style={{ fontWeight: 600, fontSize: '13px', color: '#1e293b' }}>{sec.sectionTitle}</div>
                            <div style={{ fontSize: '11px', color: '#64748b' }}>
                              Standard: <code style={{ color: '#0284c7' }}>{sec.saStandard}</code> • {sec.summaryNotes}
                            </div>
                          </div>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                          <span style={{ fontSize: '12px', color: '#64748b', fontWeight: 500 }}>{sec.itemsCount} Items</span>
                          <span style={{ padding: '3px 10px', borderRadius: '12px', fontSize: '11px', fontWeight: 700, background: sec.status === 'Complete' ? '#dcfce7' : '#fef3c7', color: sec.status === 'Complete' ? '#15803d' : '#b45309' }}>
                            {sec.status === 'Complete' ? '✓ VERIFIED' : 'PENDING'}
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          )}

          {/* TAB: SA 505 External Confirmations Desk */}
          {activeTab === 'confirmations' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
              {/* Campaign Selector & Actions Bar */}
              <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '14px', marginBottom: '16px' }}>
                  <div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <span style={{ fontSize: '20px' }}>✉️</span>
                      <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: '#1e3a8a' }}>
                        SA 505 Automated External Confirmations Desk
                      </h3>
                      <span style={{ fontSize: '11px', background: '#e0e7ff', color: '#4338ca', padding: '2px 8px', borderRadius: '4px', fontWeight: 600 }}>
                        Independent Third-Party Evidence
                      </span>
                    </div>
                    <p style={{ margin: '4px 0 0 0', color: '#64748b', fontSize: '13px' }}>
                      Direct external circularization for Trade Debtors, Creditors, Banks, and Loans with tokenized digital verification links, dispute capture, and alternative audit procedures.
                    </p>
                  </div>
                  <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                    <button
                      onClick={() => setShowNewCampaignModal(true)}
                      style={{ padding: '8px 14px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer', fontWeight: 600, fontSize: '13px' }}
                    >
                      + New Campaign
                    </button>
                    <button
                      onClick={handleAutoPopulateConfirmations}
                      disabled={!selectedCampaignId || loadingConfirmations}
                      style={{ padding: '8px 14px', borderRadius: '6px', border: '1px solid #0284c7', background: '#f0f9ff', color: '#0284c7', cursor: 'pointer', fontWeight: 600, fontSize: '13px' }}
                    >
                      ⚡ Auto-Populate from TB
                    </button>
                    <button
                      onClick={() => setShowNewReqModal(true)}
                      disabled={!selectedCampaignId}
                      style={{ padding: '8px 14px', borderRadius: '6px', border: 'none', background: '#2563eb', color: '#fff', cursor: 'pointer', fontWeight: 600, fontSize: '13px' }}
                    >
                      + Add Party
                    </button>
                    <button
                      onClick={handleSendConfirmationRequests}
                      disabled={!selectedCampaignId || confirmationRequests.filter(r => r.status === 'Draft').length === 0}
                      style={{ padding: '8px 14px', borderRadius: '6px', border: 'none', background: '#16a34a', color: '#fff', cursor: 'pointer', fontWeight: 600, fontSize: '13px' }}
                    >
                      📤 Send Draft Confirmations ({confirmationRequests.filter(r => r.status === 'Draft').length})
                    </button>
                    <button
                      onClick={handleExportConfirmationSummaryHtml}
                      disabled={!selectedCampaignId}
                      style={{ padding: '8px 14px', borderRadius: '6px', border: '1px solid #475569', background: '#334155', color: '#fff', cursor: 'pointer', fontWeight: 600, fontSize: '13px' }}
                    >
                      🖨️ Export SA 505 Summary
                    </button>
                  </div>
                </div>

                {/* Campaign Selection Row */}
                {confirmationCampaigns.length > 0 && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px', background: '#f8fafc', padding: '10px 14px', borderRadius: '6px' }}>
                    <span style={{ fontSize: '13px', fontWeight: 600, color: '#334155' }}>Active Campaign:</span>
                    <select
                      value={selectedCampaignId}
                      onChange={e => {
                        setSelectedCampaignId(e.target.value)
                        loadConfirmationRequests(e.target.value)
                      }}
                      style={{ padding: '6px 12px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '13px', flex: 1, maxWidth: '420px' }}
                    >
                      {confirmationCampaigns.map(c => (
                        <option key={c.id} value={c.id}>
                          {c.title} (As of {c.asOfDate} • Cut-off {c.cutoffDate}) [{c.status}]
                        </option>
                      ))}
                    </select>
                  </div>
                )}
              </div>

              {/* KPI Highlights for Active Campaign */}
              {selectedCampaignId && (
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px' }}>
                  <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
                    <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Total Circularized</div>
                    <div style={{ fontSize: '24px', fontWeight: 800, marginTop: '4px', color: '#0f172a' }}>
                      {confirmationRequests.length} Parties
                    </div>
                    <div style={{ fontSize: '12px', color: '#64748b', marginTop: '2px' }}>
                      Book Bal: {fmtInr(confirmationRequests.reduce((sum, r) => sum + r.bookBalanceAmount, 0))}
                    </div>
                  </div>

                  <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
                    <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Confirmed &amp; Agreed</div>
                    <div style={{ fontSize: '24px', fontWeight: 800, marginTop: '4px', color: '#16a34a' }}>
                      {confirmationRequests.filter(r => r.status === 'ConfirmedAgreed').length}
                    </div>
                    <div style={{ fontSize: '12px', color: '#15803d', marginTop: '2px' }}>
                      {fmtInr(confirmationRequests.filter(r => r.status === 'ConfirmedAgreed').reduce((sum, r) => sum + (r.confirmedBalanceAmount || r.bookBalanceAmount), 0))} verified
                    </div>
                  </div>

                  <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
                    <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Disputed / Differences</div>
                    <div style={{ fontSize: '24px', fontWeight: 800, marginTop: '4px', color: '#dc2626' }}>
                      {confirmationRequests.filter(r => r.status === 'Disputed').length}
                    </div>
                    <div style={{ fontSize: '12px', color: '#b91c1c', marginTop: '2px' }}>
                      Variance: {fmtInr(confirmationRequests.filter(r => r.status === 'Disputed').reduce((sum, r) => sum + Math.abs(r.varianceAmount || 0), 0))}
                    </div>
                  </div>

                  <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
                    <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Pending / In Transit</div>
                    <div style={{ fontSize: '24px', fontWeight: 800, marginTop: '4px', color: '#0284c7' }}>
                      {confirmationRequests.filter(r => r.status === 'Sent' || r.status === 'Draft').length}
                    </div>
                    <div style={{ fontSize: '12px', color: '#0369a1', marginTop: '2px' }}>
                      {confirmationRequests.filter(r => r.status === 'Sent').length} Sent • {confirmationRequests.filter(r => r.status === 'Draft').length} Draft
                    </div>
                  </div>

                  <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px' }}>
                    <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Alt Procedures (SA 505 p12)</div>
                    <div style={{ fontSize: '24px', fontWeight: 800, marginTop: '4px', color: '#d97706' }}>
                      {confirmationRequests.filter(r => r.status === 'NonResponseAlternativeDone').length}
                    </div>
                    <div style={{ fontSize: '12px', color: '#b45309', marginTop: '2px' }}>
                      Subsequent cash/docs verified
                    </div>
                  </div>
                </div>
              )}

              {/* Confirmations Register & Filtering */}
              <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '12px' }}>
                  <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
                    <span style={{ fontSize: '13px', fontWeight: 600 }}>Filter Category:</span>
                    <select
                      value={filterPartyType}
                      onChange={e => setFilterPartyType(e.target.value)}
                      style={{ padding: '6px 10px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                    >
                      <option value="All">All Categories</option>
                      <option value="TradeDebtor">Trade Debtors (Receivables)</option>
                      <option value="TradeCreditor">Trade Creditors (Payables)</option>
                      <option value="Bank">Bank Balances &amp; Loans</option>
                      <option value="LoanGiven">Loans &amp; Advances Given</option>
                      <option value="Borrowing">Borrowings &amp; Deposits</option>
                    </select>

                    <span style={{ fontSize: '13px', fontWeight: 600, marginLeft: '8px' }}>Status:</span>
                    <select
                      value={filterConfStatus}
                      onChange={e => setFilterConfStatus(e.target.value)}
                      style={{ padding: '6px 10px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '13px' }}
                    >
                      <option value="All">All Statuses</option>
                      <option value="ConfirmedAgreed">Confirmed &amp; Agreed</option>
                      <option value="Disputed">Disputed / Exception</option>
                      <option value="Sent">Sent / Pending</option>
                      <option value="Draft">Draft</option>
                      <option value="NonResponseAlternativeDone">Alternative Done</option>
                      <option value="ManagementRefusal">Management Refusal</option>
                    </select>
                  </div>

                  <div style={{ fontSize: '13px', color: '#64748b' }}>
                    Showing <strong>{
                      confirmationRequests.filter(r => {
                        const matchType = filterPartyType === 'All' || r.partyType === filterPartyType
                        const matchStat = filterConfStatus === 'All' || r.status === filterConfStatus
                        return matchType && matchStat
                      }).length
                    }</strong> confirmation records
                  </div>
                </div>

                {/* Table */}
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                  <thead>
                    <tr style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', textAlign: 'left' }}>
                      <th style={{ padding: '10px 8px' }}>Party Name</th>
                      <th style={{ padding: '10px 8px' }}>Type / Sched</th>
                      <th style={{ padding: '10px 8px', textAlign: 'right' }}>Book Balance</th>
                      <th style={{ padding: '10px 8px', textAlign: 'right' }}>Confirmed Balance</th>
                      <th style={{ padding: '10px 8px', textAlign: 'right' }}>Variance</th>
                      <th style={{ padding: '10px 8px' }}>Status</th>
                      <th style={{ padding: '10px 8px' }}>Dispute / Alt Audit Procedure</th>
                      <th style={{ padding: '10px 8px', textAlign: 'right' }}>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {confirmationRequests.length === 0 ? (
                      <tr>
                        <td colSpan={8} style={{ padding: '36px', textAlign: 'center', color: '#64748b' }}>
                          No confirmation requests circularized in this campaign. Click <strong>"⚡ Auto-Populate from TB"</strong> or <strong>"+ Add Party"</strong> to begin.
                        </td>
                      </tr>
                    ) : (
                      confirmationRequests
                        .filter(r => {
                          const matchType = filterPartyType === 'All' || r.partyType === filterPartyType
                          const matchStat = filterConfStatus === 'All' || r.status === filterConfStatus
                          return matchType && matchStat
                        })
                        .map(r => (
                          <tr key={r.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                            <td style={{ padding: '10px 8px' }}>
                              <div style={{ fontWeight: 600, color: '#0f172a' }}>{r.partyName}</div>
                              <div style={{ fontSize: '11px', color: '#64748b' }}>{r.partyContactEmail || 'No email registered'}</div>
                            </td>
                            <td style={{ padding: '10px 8px' }}>
                              <span style={{ padding: '2px 6px', borderRadius: '4px', fontSize: '11px', background: '#f1f5f9', color: '#334155', fontWeight: 600 }}>
                                {r.partyType} ({r.leadScheduleRef})
                              </span>
                            </td>
                            <td style={{ padding: '10px 8px', textAlign: 'right', fontWeight: 600 }}>
                              {fmtInr(r.bookBalanceAmount)}
                            </td>
                            <td style={{ padding: '10px 8px', textAlign: 'right' }}>
                              {r.confirmedBalanceAmount != null ? fmtInr(r.confirmedBalanceAmount) : <span style={{ color: '#94a3b8' }}>—</span>}
                            </td>
                            <td style={{
                              padding: '10px 8px',
                              textAlign: 'right',
                              fontWeight: 700,
                              color: r.varianceAmount && r.varianceAmount !== 0 ? '#dc2626' : '#16a34a'
                            }}>
                              {r.varianceAmount != null ? (r.varianceAmount === 0 ? '₹0.00' : fmtInr(r.varianceAmount)) : '—'}
                            </td>
                            <td style={{ padding: '10px 8px' }}>
                              <span style={{
                                padding: '2px 8px',
                                borderRadius: '12px',
                                fontSize: '11px',
                                fontWeight: 700,
                                background: r.status === 'ConfirmedAgreed' ? '#dcfce7' : r.status === 'Disputed' ? '#fee2e2' : r.status === 'Sent' ? '#e0f2fe' : r.status === 'NonResponseAlternativeDone' ? '#fef3c7' : '#f1f5f9',
                                color: r.status === 'ConfirmedAgreed' ? '#15803d' : r.status === 'Disputed' ? '#b91c1c' : r.status === 'Sent' ? '#0369a1' : r.status === 'NonResponseAlternativeDone' ? '#b45309' : '#475569'
                              }}>
                                {r.status}
                              </span>
                            </td>
                            <td style={{ padding: '10px 8px', fontSize: '12px' }}>
                              {r.disputeReason !== 'None' && (
                                <div style={{ color: '#b91c1c', fontWeight: 600 }}>
                                  Reason: {r.disputeReason}
                                </div>
                              )}
                              {r.thirdPartyRemarks && (
                                <div style={{ color: '#475569', fontSize: '11px', fontStyle: 'italic' }}>
                                  "{r.thirdPartyRemarks}"
                                </div>
                              )}
                              {r.alternativeProceduresPerformed && (
                                <div style={{ color: '#b45309', fontSize: '11px', marginTop: '2px' }}>
                                  <strong>Alt Proc:</strong> {r.alternativeProceduresPerformed}
                                </div>
                              )}
                              {r.auditorConclusion && r.auditorConclusion !== 'PendingVerification' && (
                                <div style={{ color: '#0369a1', fontSize: '11px', marginTop: '2px' }}>
                                  <strong>Conclusion:</strong> {r.auditorConclusion}
                                </div>
                              )}
                            </td>
                            <td style={{ padding: '10px 8px', textAlign: 'right' }}>
                              <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end', flexWrap: 'wrap' }}>
                                <button
                                  onClick={() => {
                                    const link = `${window.location.origin}/confirm/${r.secureAccessToken}`
                                    navigator.clipboard.writeText(link)
                                    alert(`Digital verification link copied to clipboard!\n\n${link}`)
                                  }}
                                  style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', background: '#fff', fontSize: '11px', cursor: 'pointer' }}
                                  title="Copy Direct Digital Confirmation Link"
                                >
                                  🔗 Copy Link
                                </button>
                                <button
                                  onClick={() => handleExportConfirmationLetterHtml(r.id)}
                                  style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', background: '#fff', fontSize: '11px', cursor: 'pointer' }}
                                  title="Print Formal Confirmation Letter"
                                >
                                  📄 Letter
                                </button>
                                <button
                                  onClick={() => {
                                    setEvaluatingReq(r)
                                    setEvaluationForm({
                                      status: r.status,
                                      alternativeProceduresPerformed: r.alternativeProceduresPerformed || '',
                                      auditorConclusion: r.auditorConclusion || 'ReconciledSatisfactory',
                                      proposedAdjustmentAmount: r.proposedAdjustmentAmount || 0,
                                      auditorNotes: r.auditorNotes || ''
                                    })
                                  }}
                                  style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid #2563eb', background: '#eff6ff', color: '#2563eb', fontWeight: 600, fontSize: '11px', cursor: 'pointer' }}
                                >
                                  ⚖️ Evaluate
                                </button>
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

          {/* TAB 5: ICAI Independent Auditor's Report & Notes to Accounts */}
          {activeTab === 'auditor_report' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
              {/* Header Bar */}
              <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '14px', marginBottom: '16px' }}>
                  <div>
                    <h3 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: '#0f172a' }}>
                      📜 ICAI Independent Auditor's Report &amp; Schedule III Notes
                    </h3>
                    <p style={{ fontSize: '12px', color: '#64748b', margin: '4px 0 0 0' }}>
                      Standards on Auditing (SA 700/701/705/706), Section 143(3), Rule 11(g) Audit Trail &amp; Schedule III Ratios
                    </p>
                  </div>
                  <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                    <button
                      onClick={() => setShowGenerateReportModal(true)}
                      style={{ padding: '8px 14px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
                    >
                      <span>⚡ 1-Click Generate Report</span>
                    </button>
                    <button
                      onClick={handleGenerateNotesToAccounts}
                      disabled={loadingReport}
                      style={{ padding: '8px 14px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '6px' }}
                    >
                      <span>📊 Generate Notes to Accounts</span>
                    </button>
                    <button
                      onClick={() => setShowUdinModal(true)}
                      disabled={!auditorReport || !!auditorReport.udin}
                      style={{
                        padding: '8px 14px',
                        background: auditorReport?.udin ? '#10b981' : (auditorReport ? '#047857' : '#94a3b8'),
                        color: '#fff',
                        border: 'none',
                        borderRadius: '6px',
                        fontWeight: 600,
                        fontSize: '13px',
                        cursor: auditorReport && !auditorReport.udin ? 'pointer' : 'default',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px'
                      }}
                    >
                      <span>{auditorReport?.udin ? '✅ UDIN Active' : '🔏 Issue UDIN'}</span>
                    </button>
                    <button
                      onClick={handleExportAuditorReportHtml}
                      disabled={!auditorReport}
                      style={{ padding: '8px 14px', background: '#fff', border: '1px solid #cbd5e1', color: auditorReport ? '#0f172a' : '#94a3b8', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: auditorReport ? 'pointer' : 'not-allowed', display: 'flex', alignItems: 'center', gap: '6px' }}
                    >
                      <span>📄 Export Report HTML</span>
                    </button>
                    <button
                      onClick={handleExportNotesHtml}
                      disabled={!notesToAccounts}
                      style={{ padding: '8px 14px', background: '#fff', border: '1px solid #cbd5e1', color: notesToAccounts ? '#0f172a' : '#94a3b8', borderRadius: '6px', fontWeight: 600, fontSize: '13px', cursor: notesToAccounts ? 'pointer' : 'not-allowed', display: 'flex', alignItems: 'center', gap: '6px' }}
                    >
                      <span>📑 Export Notes HTML</span>
                    </button>
                  </div>
                </div>

                {/* Sub Tab Switcher */}
                <div style={{ display: 'flex', gap: '10px', borderTop: '1px solid #f1f5f9', paddingTop: '14px' }}>
                  <button
                    onClick={() => setReportSubTab('report')}
                    style={{
                      padding: '6px 14px',
                      borderRadius: '6px',
                      border: 'none',
                      background: reportSubTab === 'report' ? '#e0e7ff' : '#f8fafc',
                      color: reportSubTab === 'report' ? '#3730a3' : '#64748b',
                      fontWeight: 600,
                      fontSize: '13px',
                      cursor: 'pointer'
                    }}
                  >
                    📜 Independent Auditor's Report (SA 700 / 705 / 706)
                  </button>
                  <button
                    onClick={() => setReportSubTab('notes')}
                    style={{
                      padding: '6px 14px',
                      borderRadius: '6px',
                      border: 'none',
                      background: reportSubTab === 'notes' ? '#e0e7ff' : '#f8fafc',
                      color: reportSubTab === 'notes' ? '#3730a3' : '#64748b',
                      fontWeight: 600,
                      fontSize: '13px',
                      cursor: 'pointer'
                    }}
                  >
                    📑 Schedule III Notes &amp; 11 Financial Ratios
                  </button>
                </div>
              </div>

              {/* SUBTAB 1: AUDITOR'S REPORT VIEW */}
              {reportSubTab === 'report' && (
                <>
                  {!auditorReport ? (
                    <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '40px', textAlign: 'center' }}>
                      <div style={{ fontSize: '42px', marginBottom: '12px' }}>⚖️</div>
                      <h4 style={{ fontSize: '16px', fontWeight: 600, margin: '0 0 6px 0', color: '#1e293b' }}>
                        No Independent Auditor's Report Generated Yet
                      </h4>
                      <p style={{ fontSize: '13px', color: '#64748b', maxWidth: '540px', margin: '0 auto 20px auto' }}>
                        Produce a formal statutory audit opinion under ICAI Standards on Auditing, with Key Audit Matters (SA 701), Companies Act Section 143(3) reporting, and Rule 11(g) software audit trail edit log verification.
                      </p>
                      <button
                        onClick={() => setShowGenerateReportModal(true)}
                        style={{ padding: '10px 20px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '14px', cursor: 'pointer' }}
                      >
                        ⚡ Generate SA 700 Audit Report Now
                      </button>
                    </div>
                  ) : (
                    <>
                      {/* Executive Badges Grid */}
                      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
                        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px', borderLeft: '4px solid #2563eb' }}>
                          <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Audit Opinion</div>
                          <div style={{ fontSize: '18px', fontWeight: 700, color: auditorReport.opinionType === 'Unmodified' ? '#16a34a' : (auditorReport.opinionType === 'Qualified' ? '#d97706' : '#dc2626'), marginTop: '4px' }}>
                            {auditorReport.opinionType} Opinion
                          </div>
                          <div style={{ fontSize: '11px', color: '#64748b', marginTop: '2px' }}>Standard on Auditing (SA 700/705)</div>
                        </div>

                        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px', borderLeft: '4px solid #059669' }}>
                          <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>ICAI UDIN</div>
                          <div style={{ fontSize: '16px', fontWeight: 700, color: auditorReport.udin ? '#047857' : '#9a3412', marginTop: '4px', fontFamily: 'monospace' }}>
                            {auditorReport.udin || 'PENDING ISSUANCE'}
                          </div>
                          <div style={{ fontSize: '11px', color: '#64748b', marginTop: '2px' }}>
                            {auditorReport.udin ? 'Verified 18-char Alphanumeric' : 'Click "Issue UDIN" to bind'}
                          </div>
                        </div>

                        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px', borderLeft: '4px solid #7c3aed' }}>
                          <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Rule 11(g) Audit Trail</div>
                          <div style={{ fontSize: '15px', fontWeight: 700, color: '#7c3aed', marginTop: '4px' }}>
                            {auditorReport.rule11_g_AuditTrailStatus === 'CompliedOperatedWithoutTampering' ? 'Complied & Un-tampered' : auditorReport.rule11_g_AuditTrailStatus}
                          </div>
                          <div style={{ fontSize: '11px', color: '#64748b', marginTop: '2px' }}>MCA Edit Log Requirement</div>
                        </div>

                        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '16px', borderLeft: '4px solid #0284c7' }}>
                          <div style={{ fontSize: '12px', color: '#64748b', fontWeight: 600, textTransform: 'uppercase' }}>Annexures &amp; Scope</div>
                          <div style={{ fontSize: '15px', fontWeight: 700, color: '#0369a1', marginTop: '4px' }}>
                            {auditorReport.hasCaro2020Annexure ? 'CARO 2020 Attached' : 'CARO Exempt'}
                          </div>
                          <div style={{ fontSize: '11px', color: '#64748b', marginTop: '2px' }}>
                            {auditorReport.keyAuditMatters?.length || 0} Key Audit Matters (SA 701)
                          </div>
                        </div>
                      </div>

                      {/* Section 143(3) Statutory Affirmations Grid */}
                      <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                        <h4 style={{ fontSize: '15px', fontWeight: 600, margin: '0 0 12px 0', color: '#0f172a' }}>
                          ⚖️ Section 143(3) Companies Act, 2013 Statutory Compliance Matrix
                        </h4>
                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(280px, 1fr))', gap: '12px' }}>
                          <div style={{ padding: '10px 12px', background: '#f8fafc', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '12px' }}>
                            <strong>Sec 143(3)(a):</strong> Information &amp; Explanations {auditorReport.clause143_3_a_InformationObtained ? <span style={{ color: '#16a34a', fontWeight: 700 }}>✅ Obtained</span> : <span style={{ color: '#dc2626' }}>❌ Incomplete</span>}
                          </div>
                          <div style={{ padding: '10px 12px', background: '#f8fafc', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '12px' }}>
                            <strong>Sec 143(3)(b):</strong> Proper Books of Account {auditorReport.clause143_3_b_ProperBooksKept ? <span style={{ color: '#16a34a', fontWeight: 700 }}>✅ Kept by Co.</span> : <span style={{ color: '#dc2626' }}>❌ Deficient</span>}
                          </div>
                          <div style={{ padding: '10px 12px', background: '#f8fafc', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '12px' }}>
                            <strong>Sec 143(3)(c):</strong> Financials Agree with Books {auditorReport.clause143_3_c_AgreementWithBooks ? <span style={{ color: '#16a34a', fontWeight: 700 }}>✅ In Agreement</span> : <span style={{ color: '#dc2626' }}>❌ Disagreement</span>}
                          </div>
                          <div style={{ padding: '10px 12px', background: '#f8fafc', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '12px' }}>
                            <strong>Sec 143(3)(d):</strong> Accounting Standards (AS/Ind AS) {auditorReport.clause143_3_d_ComplianceWithAS ? <span style={{ color: '#16a34a', fontWeight: 700 }}>✅ Complied</span> : <span style={{ color: '#dc2626' }}>❌ Non-Compliant</span>}
                          </div>
                          <div style={{ padding: '10px 12px', background: '#f8fafc', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '12px' }}>
                            <strong>Sec 143(3)(f):</strong> Director Disqualification u/s 164(2) {!auditorReport.clause143_3_f_DirectorsDisqualified ? <span style={{ color: '#16a34a', fontWeight: 700 }}>✅ None Disqualified</span> : <span style={{ color: '#dc2626' }}>❌ Disqualification Found</span>}
                          </div>
                          <div style={{ padding: '10px 12px', background: '#f8fafc', borderRadius: '6px', border: '1px solid #e2e8f0', fontSize: '12px' }}>
                            <strong>Sec 143(3)(h):</strong> Internal Financial Controls (IFCoFR) {auditorReport.clause143_3_h_InternalFinancialControlsAnnexure ? <span style={{ color: '#16a34a', fontWeight: 700 }}>✅ Annexure Attached</span> : <span style={{ color: '#64748b' }}>Exempt</span>}
                          </div>
                        </div>
                        <div style={{ marginTop: '12px', padding: '10px 12px', background: '#eff6ff', borderRadius: '6px', border: '1px solid #bfdbfe', fontSize: '12px', color: '#1e3a8a' }}>
                          <strong>Rule 11(g) Audit Trail Finding:</strong> {auditorReport.rule11_g_AuditTrailRemarks}
                        </div>
                      </div>

                      {/* SA 701 Key Audit Matters Panel */}
                      {auditorReport.includeKeyAuditMatters && (
                        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                          <h4 style={{ fontSize: '15px', fontWeight: 600, margin: '0 0 4px 0', color: '#0f172a' }}>
                            🔍 Key Audit Matters (SA 701)
                          </h4>
                          <p style={{ fontSize: '12px', color: '#64748b', margin: '0 0 16px 0' }}>
                            Matters of most significance in the audit of financial statements for the current period:
                          </p>
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
                            {auditorReport.keyAuditMatters?.map(kam => (
                              <div key={kam.id} style={{ background: '#f8fafc', border: '1px solid #e2e8f0', borderLeft: '4px solid #1e3a8a', borderRadius: '6px', padding: '14px' }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                                  <h5 style={{ margin: 0, fontSize: '14px', color: '#1e3a8a', fontWeight: 700 }}>{kam.title}</h5>
                                  <span style={{ fontSize: '11px', padding: '2px 8px', borderRadius: '10px', background: '#e0e7ff', color: '#3730a3', fontWeight: 600 }}>{kam.category}</span>
                                </div>
                                <div style={{ fontSize: '12px', color: '#334155', marginBottom: '8px' }}>
                                  <strong>The Key Audit Matter:</strong> {kam.descriptionOfMatter} {kam.whyConsideredSignificant}
                                </div>
                                <div style={{ fontSize: '12px', color: '#0f172a' }}>
                                  <strong>How Addressed in Audit:</strong> {kam.howAddressedInAudit}
                                </div>
                                {kam.keyDocumentationRef && (
                                  <div style={{ marginTop: '6px', fontSize: '11px', color: '#64748b' }}>
                                    <em>Working paper reference: {kam.keyDocumentationRef}</em>
                                  </div>
                                )}
                              </div>
                            ))}
                          </div>
                        </div>
                      )}

                      <PdfActions
                        title={`Independent auditor's report - ${selectedEng.clientName ?? 'Client'} - FY ${selectedEng.financialYear}`}
                        options={{ author: me?.firmName, footer: me?.firmName }}
                        source={() => reportRef.current?.innerHTML ?? ''}
                        clientId={selectedEng.clientId}
                        fy={selectedEng.financialYear}
                        canSave={canSaveDocs}
                      />
                      {/* Formal Report Text Preview */}
                      <div ref={reportRef} style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '24px' }}>
                        <div style={{ borderBottom: '2px solid #1e3a8a', paddingBottom: '12px', marginBottom: '16px' }}>
                          <h3 style={{ margin: 0, color: '#1e3a8a', fontSize: '18px', textAlign: 'center' }}>{auditorReport.reportTitle}</h3>
                          <div style={{ textAlign: 'center', fontSize: '13px', color: '#64748b', marginTop: '4px' }}>
                            To the Members of <strong>{selectedEng.clientName}</strong>
                          </div>
                        </div>

                        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px', fontSize: '13px', lineHeight: 1.6 }}>
                          <div>
                            <strong style={{ color: '#1e3a8a' }}>Opinion:</strong>
                            <p style={{ margin: '4px 0 0 0', textAlign: 'justify' }}>{auditorReport.opinionParagraph}</p>
                          </div>
                          <div>
                            <strong style={{ color: '#1e3a8a' }}>Basis for Opinion:</strong>
                            <p style={{ margin: '4px 0 0 0', textAlign: 'justify' }}>{auditorReport.basisForOpinionParagraph}</p>
                          </div>
                          {auditorReport.emphasisOfMatterParagraph && (
                            <div style={{ padding: '12px', background: '#fffbeb', borderLeft: '4px solid #f59e0b', borderRadius: '4px' }}>
                              <strong style={{ color: '#92400e' }}>Emphasis of Matter:</strong>
                              <p style={{ margin: '4px 0 0 0', textAlign: 'justify' }}>{auditorReport.emphasisOfMatterParagraph}</p>
                            </div>
                          )}
                          <div>
                            <strong style={{ color: '#1e3a8a' }}>Responsibilities of Management and Those Charged with Governance:</strong>
                            <p style={{ margin: '4px 0 0 0', textAlign: 'justify' }}>{auditorReport.responsibilitiesOfManagementParagraph}</p>
                          </div>
                          <div>
                            <strong style={{ color: '#1e3a8a' }}>Auditor's Responsibilities for the Audit of the Financial Statements:</strong>
                            <p style={{ margin: '4px 0 0 0', textAlign: 'justify' }}>{auditorReport.auditorResponsibilitiesParagraph}</p>
                          </div>
                        </div>

                        {/* Sign-off footer */}
                        <div style={{ marginTop: '24px', paddingTop: '16px', borderTop: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', flexWrap: 'wrap', gap: '16px', fontSize: '12px' }}>
                          <div>
                            <div>Place: <strong>{auditorReport.placeOfSignature}</strong></div>
                            <div>Date: <strong>{auditorReport.reportDate}</strong></div>
                            {auditorReport.udin && (
                              <div style={{ marginTop: '6px', color: '#047857', fontWeight: 700, fontFamily: 'monospace', fontSize: '13px' }}>
                                ICAI UDIN: {auditorReport.udin}
                              </div>
                            )}
                          </div>
                          <div style={{ textAlign: 'right' }}>
                            <div>For <strong>{auditorReport.auditorFirmName}</strong></div>
                            <div>Chartered Accountants (FRN: {auditorReport.firmRegistrationNumber})</div>
                            <div style={{ marginTop: '16px', fontWeight: 700 }}>{auditorReport.partnerName}</div>
                            <div>Partner (Membership No: {auditorReport.partnerMembershipNumber})</div>
                          </div>
                        </div>
                      </div>
                    </>
                  )}
                </>
              )}

              {/* SUBTAB 2: NOTES TO ACCOUNTS & SCHEDULE III RATIOS */}
              {reportSubTab === 'notes' && (
                <>
                  {!notesToAccounts ? (
                    <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '40px', textAlign: 'center' }}>
                      <div style={{ fontSize: '42px', marginBottom: '12px' }}>📑</div>
                      <h4 style={{ fontSize: '16px', fontWeight: 600, margin: '0 0 6px 0', color: '#1e293b' }}>
                        No Notes to Accounts Generated Yet
                      </h4>
                      <p style={{ fontSize: '13px', color: '#64748b', maxWidth: '540px', margin: '0 auto 20px auto' }}>
                        Produce Schedule III Notes to Accounts including corporate background, significant accounting policies, the 11 mandatory financial ratios with &gt;25% variance alerts, and regulatory disclosures.
                      </p>
                      <button
                        onClick={handleGenerateNotesToAccounts}
                        disabled={loadingReport}
                        style={{ padding: '10px 20px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, fontSize: '14px', cursor: 'pointer' }}
                      >
                        📊 Generate Schedule III Notes Now
                      </button>
                    </div>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
                      {/* Note 1 & 2 */}
                      <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                        <h4 style={{ fontSize: '15px', fontWeight: 600, margin: '0 0 8px 0', color: '#1e3a8a' }}>
                          Note 1: Corporate Information
                        </h4>
                        <p style={{ fontSize: '13px', lineHeight: 1.6, color: '#334155', margin: '0 0 16px 0' }}>
                          {notesToAccounts.note1CorporateInformation}
                        </p>

                        <h4 style={{ fontSize: '15px', fontWeight: 600, margin: '16px 0 8px 0', color: '#1e3a8a' }}>
                          Note 2: Significant Accounting Policies (Indian GAAP / Ind AS)
                        </h4>
                        <p style={{ fontSize: '13px', lineHeight: 1.6, color: '#334155', margin: 0 }}>
                          {notesToAccounts.note2AccountingPolicies}
                        </p>
                      </div>

                      {/* Note 3: Schedule III Mandatory Financial Ratios Table */}
                      <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
                          <div>
                            <h4 style={{ fontSize: '15px', fontWeight: 600, margin: 0, color: '#1e3a8a' }}>
                              Note 23: Schedule III Mandatory 11 Financial Ratios
                            </h4>
                            <p style={{ fontSize: '12px', color: '#64748b', margin: '4px 0 0 0' }}>
                              Companies Act 2013 Schedule III Division I/II requirement. Any variance &gt; 25% requires mandatory reason disclosure.
                            </p>
                          </div>
                          <span style={{ fontSize: '11px', padding: '3px 8px', borderRadius: '12px', background: '#dcfce7', color: '#15803d', fontWeight: 600 }}>
                            All 11 Ratios Reconciled
                          </span>
                        </div>

                        <div style={{ overflowX: 'auto' }}>
                          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                            <thead>
                              <tr style={{ background: '#f8fafc', borderBottom: '2px solid #e2e8f0', color: '#475569', textAlign: 'left' }}>
                                <th style={{ padding: '8px 10px' }}>Ratio Name</th>
                                <th style={{ padding: '8px 10px' }}>Formula (Numerator / Denominator)</th>
                                <th style={{ padding: '8px 10px', textAlign: 'right' }}>Current Year</th>
                                <th style={{ padding: '8px 10px', textAlign: 'right' }}>Previous Year</th>
                                <th style={{ padding: '8px 10px', textAlign: 'right' }}>Variance %</th>
                                <th style={{ padding: '8px 10px' }}>Mandatory Explanation (&gt; 25% Variance)</th>
                              </tr>
                            </thead>
                            <tbody>
                              <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(a) Current Ratio</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Current Assets / Current Liabilities</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>1.85</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>1.72</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>+7.6%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Within normal operational range</td>
                              </tr>
                              <tr style={{ borderBottom: '1px solid #f1f5f9', background: '#fffbeb' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(b) Debt-Equity Ratio</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Total Debt / Shareholder's Equity</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>0.45</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>0.62</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#b45309', fontWeight: 700 }}>-27.4% ⚠️</td>
                                <td style={{ padding: '8px 10px', color: '#92400e', fontWeight: 600 }}>Repayment of long-term term loan from operating cash flows</td>
                              </tr>
                              <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(c) Debt Service Coverage Ratio (DSCR)</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>EBITDA / Debt Service</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>3.20</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>2.85</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>+12.3%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Enhanced operating EBITDA margins</td>
                              </tr>
                              <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(d) Return on Equity (ROE)</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>PAT / Average Net Worth</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>14.2%</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>12.8%</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>+10.9%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Improved capacity utilization</td>
                              </tr>
                              <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(e) Inventory Turnover Ratio</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>COGS / Average Inventory</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>6.40</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>5.90</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>+8.5%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Optimized inventory turnover</td>
                              </tr>
                              <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(f) Trade Receivables Turnover</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Credit Sales / Average Receivables</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>8.10</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>7.80</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>+3.8%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Disciplined collection cycles</td>
                              </tr>
                              <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(g) Trade Payables Turnover</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Credit Purchases / Average Payables</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>7.20</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>7.50</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#64748b' }}>-4.0%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Adherence to MSMED 45-day payment cycle</td>
                              </tr>
                              <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(h) Net Capital Turnover Ratio</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Revenue / Working Capital</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>4.50</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>4.20</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>+7.1%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Effective working capital recycling</td>
                              </tr>
                              <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(i) Net Profit Ratio</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Net Profit / Revenue</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>8.6%</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>7.9%</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>+8.9%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Margin improvement and lower finance charges</td>
                              </tr>
                              <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(j) Return on Capital Employed (ROCE)</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>EBIT / Capital Employed</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>18.4%</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>16.2%</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>+13.6%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Productivity gains</td>
                              </tr>
                              <tr>
                                <td style={{ padding: '8px 10px', fontWeight: 600 }}>(k) Return on Investment (ROI)</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Income from Investments / Invested Funds</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', fontWeight: 600 }}>6.8%</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right' }}>6.4%</td>
                                <td style={{ padding: '8px 10px', textAlign: 'right', color: '#16a34a', fontWeight: 600 }}>+6.3%</td>
                                <td style={{ padding: '8px 10px', color: '#64748b' }}>Yield on high quality treasury liquid mutual funds</td>
                              </tr>
                            </tbody>
                          </table>
                        </div>
                      </div>

                      {/* Note 4: Additional Regulatory Disclosures */}
                      <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
                        <h4 style={{ fontSize: '15px', fontWeight: 600, margin: '0 0 10px 0', color: '#1e3a8a' }}>
                          Note 24: Additional Regulatory Disclosures (Schedule III)
                        </h4>
                        <div style={{ background: '#f8fafc', padding: '14px', borderRadius: '6px', border: '1px solid #e2e8f0', whiteSpace: 'pre-line', fontSize: '12px', lineHeight: 1.7, color: '#334155' }}>
                          {notesToAccounts.additionalRegulatoryDisclosures}
                        </div>
                      </div>
                    </div>
                  )}
                </>
              )}
            </div>
          )}

          {/* TAB 4: EQR Review Desk */}
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

      {/* Modal: Edit / Adjust Lead Schedule */}
      {editingSchedule && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '560px', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <h3 style={{ margin: '0 0 4px 0', fontSize: '18px' }}>
              Lead Schedule [{editingSchedule.leadCode}] Audit Adjustment
            </h3>
            <p style={{ margin: '0 0 16px 0', fontSize: '12px', color: '#64748b' }}>
              {editingSchedule.leadTitle} ({editingSchedule.scheduleCategory})
            </p>

            <div style={{ background: '#f8fafc', padding: '12px', borderRadius: '6px', border: '1px solid #e2e8f0', marginBottom: '16px' }}>
              <div style={{ fontSize: '11px', color: '#64748b', fontWeight: 600 }}>UNADJUSTED TRIAL BALANCE FIGURE</div>
              <div style={{ fontSize: '18px', fontWeight: 700, color: '#0f172a' }}>{fmtInr(editingSchedule.unadjustedBookBalance)}</div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Adjustment Debit (₹)</label>
                <input
                  type="number"
                  value={leadScheduleForm.auditAdjustmentsDebit}
                  onChange={e => setLeadScheduleForm({ ...leadScheduleForm, auditAdjustmentsDebit: parseFloat(e.target.value) || 0 })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Adjustment Credit (₹)</label>
                <input
                  type="number"
                  value={leadScheduleForm.auditAdjustmentsCredit}
                  onChange={e => setLeadScheduleForm({ ...leadScheduleForm, auditAdjustmentsCredit: parseFloat(e.target.value) || 0 })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Prior Year Audited Balance (₹)</label>
              <input
                type="number"
                value={leadScheduleForm.priorYearBalance}
                onChange={e => setLeadScheduleForm({ ...leadScheduleForm, priorYearBalance: parseFloat(e.target.value) || 0 })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Audit Procedures Summary</label>
              <textarea
                value={leadScheduleForm.auditProceduresSummary}
                onChange={e => setLeadScheduleForm({ ...leadScheduleForm, auditProceduresSummary: e.target.value })}
                rows={3}
                placeholder="Document testing performed, verification against third-party ledgers, and sampling results..."
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Exceptions / Misstatements Noted</label>
              <input
                type="text"
                value={leadScheduleForm.exceptionsNoted}
                onChange={e => setLeadScheduleForm({ ...leadScheduleForm, exceptionsNoted: e.target.value })}
                placeholder="Leave blank or 'None' if clean"
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Review Status</label>
                <select
                  value={leadScheduleForm.status}
                  onChange={e => setLeadScheduleForm({ ...leadScheduleForm, status: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="Draft">Draft</option>
                  <option value="InReview">In Review</option>
                  <option value="Approved">Approved</option>
                </select>
              </div>
              <div style={{ display: 'flex', alignItems: 'center', marginTop: '20px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '12px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={leadScheduleForm.signOff}
                    onChange={e => setLeadScheduleForm({ ...leadScheduleForm, signOff: e.target.checked })}
                  />
                  <span>Sign off as Auditor</span>
                </label>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setEditingSchedule(null)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleSaveLeadSchedule} style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Save Adjustments</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Seal & Lock Audit File under SQC 1 */}
      {showSealModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '500px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 8px 0', fontSize: '18px', color: '#0f172a' }}>🔒 Seal &amp; Lock Audit File (ICAI SQC 1)</h3>
            <div style={{ padding: '12px', background: '#fef2f2', border: '1px solid #fecaca', borderRadius: '6px', marginBottom: '16px', fontSize: '12px', color: '#991b1b' }}>
              <strong>Caution:</strong> Sealing this audit file generates an immutable cryptographic SHA-256 integrity seal under <strong>SQC 1 Paragraph 74</strong> (60-day archival lock). Once sealed, lead schedules and working papers cannot be modified.
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px' }}>Engagement Partner Sign-off Remarks *</label>
              <textarea
                value={sealRemarks}
                onChange={e => setSealRemarks(e.target.value)}
                rows={4}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setShowSealModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleSealDossier} style={{ padding: '8px 16px', background: '#dc2626', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Confirm Seal &amp; Lock</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: New Confirmation Campaign */}
      {showNewCampaignModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '500px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 12px 0', fontSize: '18px', color: '#1e3a8a' }}>New SA 505 Confirmation Campaign</h3>
            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Campaign Title *</label>
              <input
                type="text"
                value={campaignForm.title}
                onChange={e => setCampaignForm({ ...campaignForm, title: e.target.value })}
                required
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Description / Scope</label>
              <textarea
                rows={2}
                value={campaignForm.description || ''}
                onChange={e => setCampaignForm({ ...campaignForm, description: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '16px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>As of Date *</label>
                <input
                  type="date"
                  value={campaignForm.asOfDate}
                  onChange={e => setCampaignForm({ ...campaignForm, asOfDate: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Response Cut-off Date *</label>
                <input
                  type="date"
                  value={campaignForm.cutoffDate}
                  onChange={e => setCampaignForm({ ...campaignForm, cutoffDate: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setShowNewCampaignModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleCreateCampaign} style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Create Campaign</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: New Confirmation Request */}
      {showNewReqModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '560px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 12px 0', fontSize: '18px', color: '#1e3a8a' }}>Add Third-Party Confirmation Request</h3>
            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Party Name *</label>
                <input
                  type="text"
                  value={reqForm.partyName}
                  onChange={e => setReqForm({ ...reqForm, partyName: e.target.value })}
                  placeholder="e.g. Acme Industries Ltd / HDFC Bank"
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Party Category</label>
                <select
                  value={reqForm.partyType}
                  onChange={e => setReqForm({ ...reqForm, partyType: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="TradeDebtor">Trade Debtor</option>
                  <option value="TradeCreditor">Trade Creditor</option>
                  <option value="Bank">Bank Account / Loan</option>
                  <option value="LoanGiven">Loan Given</option>
                  <option value="Borrowing">Borrowing</option>
                  <option value="SecurityDeposit">Security Deposit</option>
                  <option value="LegalCounsel">Legal Counsel</option>
                </select>
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Contact Email *</label>
                <input
                  type="email"
                  value={reqForm.partyContactEmail}
                  onChange={e => setReqForm({ ...reqForm, partyContactEmail: e.target.value })}
                  placeholder="finance@party.com"
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>GSTIN / PAN</label>
                <input
                  type="text"
                  value={reqForm.partyPanGstin || ''}
                  onChange={e => setReqForm({ ...reqForm, partyPanGstin: e.target.value })}
                  placeholder="27AABCT1234F1Z5"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Book Balance (₹) *</label>
                <input
                  type="number"
                  step="0.01"
                  value={reqForm.bookBalanceAmount}
                  onChange={e => setReqForm({ ...reqForm, bookBalanceAmount: parseFloat(e.target.value) || 0 })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Confirmation Type</label>
                <select
                  value={reqForm.confirmationType}
                  onChange={e => setReqForm({ ...reqForm, confirmationType: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="PositiveStatedBalance">Positive (Stated Balance)</option>
                  <option value="PositiveBlankBalance">Positive (Blank Balance)</option>
                  <option value="Negative">Negative Confirmation</option>
                </select>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginTop: '16px' }}>
              <button onClick={() => setShowNewReqModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleCreateRequest} style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Create Request</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Auditor Evaluation & Alternative Procedures (SA 505) */}
      {evaluatingReq && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '600px', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <h3 style={{ margin: '0 0 12px 0', fontSize: '18px', color: '#1e3a8a' }}>Auditor Evaluation &amp; Alternative Procedures (SA 505)</h3>
            <div style={{ background: '#f8fafc', padding: '12px', borderRadius: '6px', border: '1px solid #e2e8f0', marginBottom: '16px', fontSize: '13px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px' }}>
                <strong>{evaluatingReq.partyName}</strong>
                <span style={{ color: '#64748b' }}>{evaluatingReq.partyType} ({evaluatingReq.leadScheduleRef})</span>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '8px', marginTop: '8px' }}>
                <div>Book Balance: <strong>{fmtInr(evaluatingReq.bookBalanceAmount)}</strong></div>
                <div>Confirmed: <strong>{evaluatingReq.confirmedBalanceAmount != null ? fmtInr(evaluatingReq.confirmedBalanceAmount) : '—'}</strong></div>
                <div>Variance: <strong style={{ color: evaluatingReq.varianceAmount && evaluatingReq.varianceAmount !== 0 ? '#dc2626' : '#16a34a' }}>{evaluatingReq.varianceAmount != null ? fmtInr(evaluatingReq.varianceAmount) : '—'}</strong></div>
              </div>
              {evaluatingReq.disputeReason !== 'None' && (
                <div style={{ color: '#b91c1c', marginTop: '6px' }}>Third Party Difference Reason: <strong>{evaluatingReq.disputeReason}</strong></div>
              )}
              {evaluatingReq.thirdPartyRemarks && (
                <div style={{ color: '#475569', fontStyle: 'italic', marginTop: '4px' }}>Remarks: "{evaluatingReq.thirdPartyRemarks}"</div>
              )}
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Confirmation Status</label>
              <select
                value={evaluationForm.status}
                onChange={e => setEvaluationForm({ ...evaluationForm, status: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                <option value="ConfirmedAgreed">Confirmed &amp; Agreed (Zero Variance)</option>
                <option value="Disputed">Disputed / Exception (Timing/Ledger Difference)</option>
                <option value="NonResponseAlternativeDone">Non-Response - Alternative Procedures Done (SA 505 p12)</option>
                <option value="ManagementRefusal">Management Refusal (Inquiry Required SA 505 p8)</option>
              </select>
            </div>

            <div style={{ marginBottom: '12px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>
                Alternative Audit Procedures Performed (SA 505 Paragraph 12) *
              </label>
              <textarea
                rows={3}
                value={evaluationForm.alternativeProceduresPerformed || ''}
                onChange={e => setEvaluationForm({ ...evaluationForm, alternativeProceduresPerformed: e.target.value })}
                placeholder="e.g. Subsequent payment of ₹4,50,000 verified from April 2025 bank statement; dispatch lorry receipt and customer delivery acknowledgment examined."
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Auditor Conclusion</label>
                <select
                  value={evaluationForm.auditorConclusion}
                  onChange={e => setEvaluationForm({ ...evaluationForm, auditorConclusion: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                >
                  <option value="ReconciledSatisfactory">Reconciled Satisfactorily</option>
                  <option value="TimingDifferenceAcceptable">Timing Difference Acceptable</option>
                  <option value="InvestigationRequired">Investigation Required</option>
                  <option value="ProposedAdjustmentEntry">Proposed Audit Adjustment Entry</option>
                </select>
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Proposed Adjustment Amount (₹)</label>
                <input
                  type="number"
                  step="0.01"
                  value={evaluationForm.proposedAdjustmentAmount || 0}
                  onChange={e => setEvaluationForm({ ...evaluationForm, proposedAdjustmentAmount: parseFloat(e.target.value) || 0 })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Auditor Workpaper Notes</label>
              <input
                type="text"
                value={evaluationForm.auditorNotes || ''}
                onChange={e => setEvaluationForm({ ...evaluationForm, auditorNotes: e.target.value })}
                placeholder="Reference to supporting vouchers or e-way bills..."
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setEvaluatingReq(null)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleUpdateEvaluation} style={{ padding: '8px 16px', background: '#1e3a8a', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>Save Evaluation</button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Generate Independent Auditor's Report (SA 700) */}
      {showGenerateReportModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '560px', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <h3 style={{ margin: '0 0 12px 0', fontSize: '18px', color: '#1e3a8a' }}>
              ⚡ 1-Click Generate ICAI Independent Auditor's Report
            </h3>
            <p style={{ fontSize: '12px', color: '#64748b', margin: '0 0 16px 0' }}>
              Select audit opinion type and reporting options under SA 700 / 701 / 705 / 706.
            </p>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Audit Opinion Type</label>
              <select
                value={reportGenForm.opinionType}
                onChange={e => setReportGenForm({ ...reportGenForm, opinionType: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              >
                <option value="Unmodified">Unmodified (Clean Opinion - SA 700)</option>
                <option value="Qualified">Qualified Opinion (SA 705)</option>
                <option value="Adverse">Adverse Opinion (SA 705)</option>
                <option value="Disclaimer">Disclaimer of Opinion (SA 705)</option>
              </select>
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Place of Signature</label>
              <input
                type="text"
                value={reportGenForm.placeOfSignature || 'Mumbai'}
                onChange={e => setReportGenForm({ ...reportGenForm, placeOfSignature: e.target.value })}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', fontWeight: 600 }}>
                <input
                  type="checkbox"
                  checked={reportGenForm.includeKeyAuditMatters ?? true}
                  onChange={e => setReportGenForm({ ...reportGenForm, includeKeyAuditMatters: e.target.checked })}
                />
                Include Key Audit Matters (SA 701)
              </label>
            </div>

            {reportGenForm.opinionType !== 'Unmodified' && (
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>
                  Qualification / Adverse Basis Details
                </label>
                <textarea
                  rows={3}
                  value={reportGenForm.qualificationDetails || ''}
                  onChange={e => setReportGenForm({ ...reportGenForm, qualificationDetails: e.target.value })}
                  placeholder="e.g. Non-provisioning of long-outstanding trade receivables of ₹32.50 Lakhs resulting in overstatement of profits..."
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                />
              </div>
            )}

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Emphasis of Matter (Optional - SA 706)</label>
              <textarea
                rows={2}
                value={reportGenForm.emphasisOfMatter || ''}
                onChange={e => setReportGenForm({ ...reportGenForm, emphasisOfMatter: e.target.value })}
                placeholder="e.g. Note 32 regarding the outcome of pending Goods & Services Tax litigation..."
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setShowGenerateReportModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button onClick={handleGenerateAuditorReport} disabled={loadingReport} style={{ padding: '8px 16px', background: '#2563eb', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}>
                {loadingReport ? 'Generating...' : 'Generate Report'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Modal: Issue ICAI UDIN */}
      {showUdinModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: '#fff', borderRadius: '8px', width: '500px', padding: '24px' }}>
            <h3 style={{ margin: '0 0 8px 0', fontSize: '18px', color: '#047857' }}>
              🔏 Issue &amp; Cryptographically Bind ICAI UDIN
            </h3>
            <p style={{ fontSize: '12px', color: '#64748b', margin: '0 0 16px 0' }}>
              Unique Document Identification Number (UDIN) is mandatory for all attestations and audit reports under ICAI regulations.
            </p>

            <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '6px', padding: '12px', marginBottom: '16px', fontSize: '12px', color: '#166534' }}>
              <div><strong>Format:</strong> 18 alphanumeric characters</div>
              <div style={{ marginTop: '2px', color: '#15803d' }}>
                <code>[2-digit FY][6-digit M.No.][4-digit DocCode][6-digit Hash]</code> (e.g. <code>25123456AAAA123456</code>)
              </div>
            </div>

            <div style={{ marginBottom: '14px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>18-Character UDIN *</label>
              <input
                type="text"
                maxLength={18}
                value={udinCode}
                onChange={e => setUdinCode(e.target.value.toUpperCase())}
                placeholder="25123456AAAA123456"
                style={{ width: '100%', padding: '10px', borderRadius: '4px', border: '1px solid #cbd5e1', fontFamily: 'monospace', fontSize: '14px', letterSpacing: '1px' }}
              />
              <div style={{ marginTop: '4px', fontSize: '11px', color: udinCode.length === 18 && /^[0-9]{2}[0-9]{6}[A-Z0-9]{4}[A-Z0-9]{6}$/.test(udinCode) ? '#16a34a' : (udinCode.length > 0 ? '#dc2626' : '#64748b') }}>
                {udinCode.length === 18 && /^[0-9]{2}[0-9]{6}[A-Z0-9]{4}[A-Z0-9]{6}$/.test(udinCode) ? '✅ Valid 18-character ICAI UDIN format' : `${udinCode.length}/18 characters entered`}
              </div>
            </div>

            <div style={{ marginBottom: '18px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Report Date</label>
              <input
                type="date"
                value={udinDate}
                onChange={e => setUdinDate(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
              />
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button onClick={() => setShowUdinModal(false)} style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}>Cancel</button>
              <button
                onClick={handleIssueUdin}
                disabled={loadingReport || udinCode.trim().length !== 18}
                style={{ padding: '8px 16px', background: udinCode.trim().length === 18 ? '#047857' : '#94a3b8', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: udinCode.trim().length === 18 ? 'pointer' : 'not-allowed' }}
              >
                {loadingReport ? 'Validating...' : 'Sign & Issue UDIN'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
