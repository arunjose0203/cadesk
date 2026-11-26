import { useState, useEffect, useMemo, useCallback, type FormEvent } from 'react'
import {
  taxLitigationApi,
  api,
  type TaxAppealDto,
  type TaxAppealDetailDto,
  type CreateOrUpdateTaxAppealRequest,
  type SaveAppealGroundRequest,
  type SavePaperBookDocRequest,
  type AddJudicialCitationRequest,
  type CertifyAppealRequest,
  type CalculateFilingFeeRequest,
  type FilingFeeResultDto,
  type AppealForum,
  type GroundType,
  type PaperBookEvidenceType,
  type Client
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

function formatINR(val: number): string {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 0
  }).format(val)
}

export function TaxLitigationDeskPage() {
  const [activeTab, setActiveTab] = useState<'appeals' | 'grounds' | 'paperbook' | 'citations'>('appeals')
  const [loading, setLoading] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  // Context Data
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')

  // Appeals Data
  const [appeals, setAppeals] = useState<TaxAppealDto[]>([])
  const [selectedAppealId, setSelectedAppealId] = useState<string | null>(null)
  const [appealDetail, setAppealDetail] = useState<TaxAppealDetailDto | null>(null)

  // Modals
  const [showAppealModal, setShowAppealModal] = useState(false)
  const [showGroundModal, setShowGroundModal] = useState(false)
  const [showDocModal, setShowDocModal] = useState(false)
  const [showCitationModal, setShowCitationModal] = useState(false)
  const [showCertifyModal, setShowCertifyModal] = useState(false)
  const [showFeeModal, setShowFeeModal] = useState(false)
  const [showDossierModal, setShowDossierModal] = useState(false)
  const [dossierHtml, setDossierHtml] = useState<string>('')

  // Fee Calculator State
  const [feeForm, setFeeForm] = useState<CalculateFilingFeeRequest>({
    forum: 'Itat',
    assessedTotalIncome: 5000000,
    isOtherMatter: false
  })
  const [feeResult, setFeeResult] = useState<FilingFeeResultDto | null>(null)

  // Appeal Form State
  const [appealForm, setAppealForm] = useState<CreateOrUpdateTaxAppealRequest>({
    clientId: '',
    forum: 'Itat',
    appealNumber: '',
    assessmentYear: '2024-25',
    financialYear: '2023-24',
    assessingOfficerWardOrCircle: 'Circle 1(1)(1), New Delhi',
    impugnedOrderSection: 'Sec 143(3) r.w.s. 144B',
    impugnedOrderNumber: `ITBA/AST/S/143(3)/2025-26/${Math.floor(1000000000 + Math.random() * 9000000000)}`,
    impugnedOrderDate: new Date().toISOString().split('T')[0],
    dateOfServiceOfOrder: new Date().toISOString().split('T')[0],
    assessedTotalIncome: 7500000,
    returnedTotalIncome: 2500000,
    totalDisputedAdditions: 5000000,
    disputedTaxDemand: 1650000,
    condonationReason: '',
    statementOfFacts: '1. The Appellant is an incorporated Indian entity filing regular returns of income.\n2. In the course of assessment under Section 143(3) r.w.s. 144B, the learned AO made additions without affording reasonable opportunity.',
    reliefSought: 'It is humbly prayed that the impugned addition made by the AO and confirmed by the CIT(A) be deleted in full with all consequential relief.'
  })

  // Ground Form State
  const [groundForm, setGroundForm] = useState<SaveAppealGroundRequest>({
    taxAppealId: '',
    groundNumber: 1,
    groundType: 'MeritsAddition',
    title: 'Disallowance of Business Expenditure u/s 37(1)',
    groundText: 'The learned AO erred in law and on facts in disallowing genuine business expenditure incurred wholly and exclusively for the purpose of business.',
    statutorySection: 'Sec 37(1)',
    disputedAdditionAmount: 2500000,
    isWithoutPrejudice: false,
    isAlternativeGround: false,
    orderIndex: 1
  })

  // Paper Book Doc Form State
  const [docForm, setDocForm] = useState<SavePaperBookDocRequest>({
    taxAppealId: '',
    itemNumber: 1,
    documentDate: new Date().toISOString().split('T')[0],
    descriptionOfDocument: 'Written submissions filed before AO in response to show cause notice',
    relevance: 'Proves that comprehensive vouchers and ledger accounts were submitted during assessment',
    evidenceType: 'PartLowerAuthoritiesRecord',
    pageCount: 15,
    isCertified: true,
    orderIndex: 1
  })

  // Citation Form State
  const [citationForm, setCitationForm] = useState<AddJudicialCitationRequest>({
    taxAppealId: '',
    caseTitle: 'CIT v. Excel Industries Ltd',
    citation: '[2013] 358 ITR 295 (SC)',
    courtOrTribunal: 'Supreme Court of India',
    judgmentYear: 2013,
    statutorySection: 'Sec 28(i) / Sec 4',
    keyRatioProposition: 'Real income theory: Income does not accrue until a legal right to receive the income arises. Book entries alone do not constitute taxable real income.',
    paragraphReference: 'Paras 28-32',
    relevanceToGround: 'Applies to Ground 1 regarding hypothetical / premature revenue recognition additions.'
  })

  // Certify Form State
  const [certifyForm, setCertifyForm] = useState<CertifyAppealRequest>({
    partnerMembershipNumber: '504932',
    partnerName: 'CA Rajesh Sharma',
    filingDate: new Date().toISOString().split('T')[0],
    dinOrAckNumber: `ACK/ITAT/2026/${Math.floor(100000 + Math.random() * 900000)}`
  })

  const loadClients = useCallback(async () => {
    try {
      const res = await api<ClientPage>('/api/clients?take=100')
      setClients(res.items || [])
      if (res.items && res.items.length > 0) {
        const firstId = res.items[0].id
        setSelectedClientId(prev => prev || firstId)
        setAppealForm(prev => (prev.clientId ? prev : { ...prev, clientId: firstId }))
      }
    } catch (err: unknown) {
      console.error('Failed to load clients', err)
    }
  }, [])

  const loadAppeals = useCallback(async () => {
    try {
      const res = await taxLitigationApi.getAppeals(selectedClientId || undefined)
      setAppeals(res || [])
      if (res && res.length > 0) {
        setSelectedAppealId(prev => prev || res[0].id)
      }
    } catch (err: unknown) {
      console.error('Failed to load appeals', err)
    }
  }, [selectedClientId])

  // Initial Load
  useEffect(() => {
    loadClients()
  }, [loadClients])

  useEffect(() => {
    loadAppeals()
  }, [loadAppeals])

  useEffect(() => {
    if (selectedAppealId) {
      loadAppealDetail(selectedAppealId)
    }
  }, [selectedAppealId])

  const loadAppealDetail = async (id: string) => {
    setLoading(true)
    try {
      const res = await taxLitigationApi.getAppeal(id)
      setAppealDetail(res)
      setGroundForm(prev => ({
        ...prev,
        taxAppealId: id,
        groundNumber: (res?.grounds.length || 0) + 1,
        orderIndex: (res?.grounds.length || 0) + 1
      }))
      setDocForm(prev => ({
        ...prev,
        taxAppealId: id,
        itemNumber: (res?.paperBookDocuments.length || 0) + 1,
        orderIndex: (res?.paperBookDocuments.length || 0) + 1
      }))
      setCitationForm(prev => ({ ...prev, taxAppealId: id }))
    } catch (err: unknown) {
      console.error('Failed to load appeal details', err)
    } finally {
      setLoading(false)
    }
  }

  const handleSaveAppeal = async (e: FormEvent) => {
    e.preventDefault()
    if (!appealForm.clientId || !appealForm.assessingOfficerWardOrCircle) {
      setErrorMsg('Please select a Client and enter the Assessing Officer Circle')
      return
    }
    setLoading(true)
    setErrorMsg(null)
    try {
      const res = await taxLitigationApi.saveAppeal(appealForm)
      setNotice(`Appeal for AY ${res.assessmentYear} saved (${res.forum}). Statutory fee: ${formatINR(res.filingFeeAmount)}.`)
      setShowAppealModal(false)
      await loadAppeals()
      setSelectedAppealId(res.id)
      await loadAppealDetail(res.id)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to save appeal')
    } finally {
      setLoading(false)
    }
  }

  const handleDeleteAppeal = async (id: string) => {
    if (!confirm('Are you sure you want to delete this appeal and all associated grounds/documents?')) return
    try {
      await taxLitigationApi.deleteAppeal(id)
      setNotice('Appeal deleted.')
      if (selectedAppealId === id) setSelectedAppealId(null)
      await loadAppeals()
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to delete appeal')
    }
  }

  const handleSaveGround = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedAppealId) return
    try {
      await taxLitigationApi.saveGround({ ...groundForm, taxAppealId: selectedAppealId })
      setNotice('Ground of Appeal added successfully.')
      setShowGroundModal(false)
      await loadAppealDetail(selectedAppealId)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to save ground')
    }
  }

  const handleDeleteGround = async (groundId: string) => {
    if (!confirm('Delete this ground of appeal?')) return
    try {
      await taxLitigationApi.deleteGround(groundId)
      setNotice('Ground deleted.')
      if (selectedAppealId) await loadAppealDetail(selectedAppealId)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to delete ground')
    }
  }

  const handleSaveDoc = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedAppealId) return
    try {
      await taxLitigationApi.savePaperBookDoc({ ...docForm, taxAppealId: selectedAppealId })
      setNotice('Document added and Paper Book re-paginated sequentially.')
      setShowDocModal(false)
      await loadAppealDetail(selectedAppealId)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to add document')
    }
  }

  const handleDeleteDoc = async (docId: string) => {
    if (!confirm('Delete this document from Paper Book? Pages will be re-numbered sequentially.')) return
    try {
      await taxLitigationApi.deletePaperBookDoc(docId)
      setNotice('Document removed and remaining Paper Book re-paginated.')
      if (selectedAppealId) await loadAppealDetail(selectedAppealId)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to delete document')
    }
  }

  const handleAddCitation = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedAppealId) return
    try {
      await taxLitigationApi.addCitation({ ...citationForm, taxAppealId: selectedAppealId })
      setNotice('Judicial landmark citation linked.')
      setShowCitationModal(false)
      await loadAppealDetail(selectedAppealId)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to link citation')
    }
  }

  const handleDeleteCitation = async (citationId: string) => {
    try {
      await taxLitigationApi.deleteCitation(citationId)
      setNotice('Citation unlinked.')
      if (selectedAppealId) await loadAppealDetail(selectedAppealId)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to unlink citation')
    }
  }

  const handleCertifyAppeal = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedAppealId) return
    try {
      await taxLitigationApi.certifyAppeal(selectedAppealId, certifyForm)
      setNotice(`Appeal certified by ${certifyForm.partnerName} (Membership #${certifyForm.partnerMembershipNumber}). Ready for filing.`)
      setShowCertifyModal(false)
      await loadAppeals()
      await loadAppealDetail(selectedAppealId)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to certify appeal')
    }
  }

  const handleCalculateFee = async (e: FormEvent) => {
    e.preventDefault()
    try {
      const res = await taxLitigationApi.calculateFilingFee(feeForm)
      setFeeResult(res)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to calculate fee')
    }
  }

  const handleOpenDossier = async () => {
    if (!selectedAppealId) return
    try {
      const url = taxLitigationApi.getDossierHtmlUrl(selectedAppealId)
      const res = await fetch(url)
      const html = await res.text()
      setDossierHtml(html)
      setShowDossierModal(true)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to generate dossier HTML')
    }
  }

  const selectedAppeal = useMemo(() => {
    return appeals.find(a => a.id === selectedAppealId) || null
  }, [appeals, selectedAppealId])

  const getForumBadge = (forum: AppealForum) => {
    switch (forum) {
      case 'CitAppeals':
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-bold bg-amber-100 text-amber-800">CIT(Appeals) • Form 35</span>
      case 'Itat':
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-bold bg-indigo-100 text-indigo-800">ITAT Tribunal • Form 36</span>
      case 'HighCourt':
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-bold bg-purple-100 text-purple-800">High Court • Sec 260A</span>
      case 'SupremeCourt':
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-bold bg-rose-100 text-rose-800">Supreme Court • SLP</span>
      default:
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-800">{forum}</span>
    }
  }

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Certified':
      case 'Filed':
      case 'DisposedAllowed':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">● {status}</span>
      case 'PartnerReviewed':
      case 'HearingScheduled':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">⏳ {status}</span>
      case 'Drafting':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-amber-100 text-amber-800">✎ {status}</span>
      case 'DisposedDismissed':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-red-100 text-red-800">✖ {status}</span>
      default:
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-800">{status}</span>
    }
  }

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Page Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-gray-200 pb-5">
        <div>
          <div className="flex items-center gap-3">
            <span className="text-3xl">🏛️</span>
            <div>
              <h1 className="text-2xl font-bold text-gray-900">ITAT & High Court Appeal Brief & Paper Book Desk</h1>
              <p className="text-sm text-gray-500 mt-1">
                Automated Form 35 (CIT(A)) & Form 36 (ITAT) Appeals, ITAT Rule 18 Paginated Paper Books, Judicial Ratio Extractor & 60-Day Limitation Countdown
              </p>
            </div>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => {
              setFeeResult(null)
              setShowFeeModal(true)
            }}
            className="inline-flex items-center px-3.5 py-2 border border-gray-300 text-sm font-medium rounded-lg text-gray-700 bg-white hover:bg-gray-50 shadow-sm"
          >
            ⚖️ Fee Calculator
          </button>
          <button
            onClick={() => setShowAppealModal(true)}
            className="inline-flex items-center px-3.5 py-2 border border-transparent text-sm font-medium rounded-lg text-white bg-indigo-600 hover:bg-indigo-700 shadow-sm"
          >
            + Draft New Appeal
          </button>
          {selectedAppealId && (
            <button
              onClick={handleOpenDossier}
              className="inline-flex items-center px-3.5 py-2 border border-emerald-600 text-sm font-medium rounded-lg text-emerald-700 bg-emerald-50 hover:bg-emerald-100 shadow-sm"
            >
              📄 View Print Dossier
            </button>
          )}
        </div>
      </div>

      {/* Notifications */}
      {notice && (
        <div className="p-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg flex items-center justify-between text-sm">
          <span>{notice}</span>
          <button onClick={() => setNotice(null)} className="text-emerald-600 font-bold">&times;</button>
        </div>
      )}
      {errorMsg && (
        <div className="p-4 bg-rose-50 border border-rose-200 text-rose-800 rounded-lg flex items-center justify-between text-sm">
          <span>{errorMsg}</span>
          <button onClick={() => setErrorMsg(null)} className="text-rose-600 font-bold">&times;</button>
        </div>
      )}

      {/* Client Filter Bar */}
      <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
        <div className="w-full md:w-1/3">
          <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1">Target Client</label>
          <select
            value={selectedClientId}
            onChange={e => {
              setSelectedClientId(e.target.value)
              setAppealForm(prev => ({ ...prev, clientId: e.target.value }))
            }}
            className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border focus:ring-indigo-500 focus:border-indigo-500"
          >
            <option value="">-- All Clients --</option>
            {clients.map(c => (
              <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
            ))}
          </select>
        </div>
        {selectedAppeal && (
          <div className="text-right">
            <span className="text-xs text-gray-500">Active Appeal Context:</span>
            <div className="text-sm font-bold text-gray-900">{selectedAppeal.clientName} (AY {selectedAppeal.assessmentYear})</div>
            <div className="text-xs text-indigo-600 font-mono">{selectedAppeal.appealNumber || 'Appeal In Draft'} • {getForumBadge(selectedAppeal.forum)}</div>
          </div>
        )}
      </div>

      {/* Tab Navigation */}
      <div className="border-b border-gray-200">
        <nav className="-mb-px flex space-x-8">
          <button
            onClick={() => setActiveTab('appeals')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'appeals'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            <span>🏛️</span> Appeals Registry &amp; Limitation
            <span className="ml-2 bg-gray-100 text-gray-600 text-xs px-2 py-0.5 rounded-full font-bold">
              {appeals.length}
            </span>
          </button>
          <button
            onClick={() => setActiveTab('grounds')}
            disabled={!selectedAppealId}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 disabled:opacity-40 ${
              activeTab === 'grounds'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            <span>📝</span> Grounds of Appeal Studio
            {appealDetail?.grounds && (
              <span className="ml-2 bg-indigo-100 text-indigo-700 text-xs px-2 py-0.5 rounded-full font-bold">
                {appealDetail.grounds.length}
              </span>
            )}
          </button>
          <button
            onClick={() => setActiveTab('paperbook')}
            disabled={!selectedAppealId}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 disabled:opacity-40 ${
              activeTab === 'paperbook'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            <span>📚</span> ITAT Rule 18 Paper Book Compiler
            {appealDetail?.paperBookDocuments && (
              <span className="ml-2 bg-emerald-100 text-emerald-800 text-xs px-2 py-0.5 rounded-full font-bold">
                {appealDetail.paperBookDocuments.length} docs
              </span>
            )}
          </button>
          <button
            onClick={() => setActiveTab('citations')}
            disabled={!selectedAppealId}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 disabled:opacity-40 ${
              activeTab === 'citations'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            <span>⚖️</span> Judicial Authorities &amp; Certification
            {appealDetail?.citations && (
              <span className="ml-2 bg-purple-100 text-purple-800 text-xs px-2 py-0.5 rounded-full font-bold">
                {appealDetail.citations.length}
              </span>
            )}
          </button>
        </nav>
      </div>

      {/* ========================================================================= */}
      {/* TAB 1: Appeals Registry & 60-Day Limitation Tracker */}
      {/* ========================================================================= */}
      {activeTab === 'appeals' && (
        <div className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
            {appeals.map(appeal => (
              <div
                key={appeal.id}
                onClick={() => setSelectedAppealId(appeal.id)}
                className={`bg-white rounded-xl border p-5 shadow-sm space-y-4 cursor-pointer transition-all ${
                  selectedAppealId === appeal.id
                    ? 'border-indigo-600 ring-2 ring-indigo-100'
                    : 'border-gray-200 hover:border-gray-300'
                }`}
              >
                <div className="flex items-start justify-between">
                  <div>
                    <span className="text-xs font-bold text-gray-500 uppercase tracking-wider">{appeal.clientName}</span>
                    <h4 className="font-bold text-gray-900 text-base mt-0.5">AY {appeal.assessmentYear}</h4>
                    <p className="text-xs text-gray-500 font-mono">{appeal.appealNumber || 'Appeal In Draft'}</p>
                  </div>
                  {getForumBadge(appeal.forum)}
                </div>

                {/* Limitation Countdown Card */}
                <div className={`p-3 rounded-lg border text-xs ${
                  appeal.delayCondonationRequired
                    ? 'bg-rose-50 border-rose-200 text-rose-800'
                    : appeal.daysRemaining <= 15
                    ? 'bg-amber-50 border-amber-200 text-amber-800'
                    : 'bg-emerald-50 border-emerald-200 text-emerald-800'
                }`}>
                  <div className="flex items-center justify-between font-semibold">
                    <span>Statutory Limitation:</span>
                    <span>Deadline: {appeal.statutoryFilingDeadline}</span>
                  </div>
                  <div className="mt-1 flex items-center justify-between">
                    <span>
                      {appeal.delayCondonationRequired
                        ? `⚠️ DELAYED by ${appeal.daysOfDelay} days (Condonation Req)`
                        : `⏳ ${appeal.daysRemaining} days remaining`}
                    </span>
                    <span className="font-mono">Service: {appeal.dateOfServiceOfOrder}</span>
                  </div>
                </div>

                <div className="space-y-1.5 text-xs text-gray-600 bg-gray-50 p-3 rounded-lg border border-gray-100">
                  <div className="flex justify-between">
                    <span className="text-gray-500">Impugned Section:</span>
                    <span className="font-semibold text-gray-900">{appeal.impugnedOrderSection}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-500">Disputed Tax Demand:</span>
                    <span className="font-bold text-rose-600">{formatINR(appeal.disputedTaxDemand)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-500">Total Additions:</span>
                    <span className="font-medium text-gray-800">{formatINR(appeal.totalDisputedAdditions)}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-500">Statutory Appeal Fee:</span>
                    <span className="font-bold text-indigo-700">{formatINR(appeal.filingFeeAmount)}</span>
                  </div>
                </div>

                <div className="pt-2 border-t border-gray-100 flex items-center justify-between text-xs">
                  <div className="flex items-center gap-3 text-gray-500">
                    <span>📝 {appeal.groundsCount} Grounds</span>
                    <span>📚 {appeal.documentsCount} Docs</span>
                    <span>⚖️ {appeal.citationsCount} Rulings</span>
                  </div>
                  {getStatusBadge(appeal.status)}
                </div>

                <div className="pt-2 border-t border-gray-100 flex items-center justify-between">
                  <button
                    onClick={(e) => {
                      e.stopPropagation()
                      setSelectedAppealId(appeal.id)
                      setActiveTab('grounds')
                    }}
                    className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold"
                  >
                    Open in Studio &rarr;
                  </button>
                  <button
                    onClick={(e) => {
                      e.stopPropagation()
                      handleDeleteAppeal(appeal.id)
                    }}
                    className="text-xs text-rose-600 hover:text-rose-800"
                  >
                    Delete
                  </button>
                </div>
              </div>
            ))}

            {appeals.length === 0 && (
              <div className="col-span-full bg-white p-12 rounded-xl border border-dashed border-gray-300 text-center">
                <span className="text-4xl">🏛️</span>
                <h4 className="mt-3 font-semibold text-gray-900 text-base">No Tax Appeals Found</h4>
                <p className="text-xs text-gray-500 mt-1 max-w-sm mx-auto">
                  Click &quot;+ Draft New Appeal&quot; to build a Form 35 (CIT(A)) or Form 36 (ITAT) memorandum of appeal with automated limitation countdowns and ITAT Rule 18 Paper Book.
                </p>
                <button
                  onClick={() => setShowAppealModal(true)}
                  className="mt-4 px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 shadow-sm"
                >
                  + Draft New Appeal
                </button>
              </div>
            )}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 2: Grounds of Appeal Studio */}
      {/* ========================================================================= */}
      {activeTab === 'grounds' && appealDetail && (
        <div className="space-y-6">
          {/* Statement of Facts & Relief Preview */}
          <div className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-bold text-gray-900 text-base">Statement of Facts &amp; Legal Framework</h3>
                <p className="text-xs text-gray-500 mt-0.5">Factual matrix presented before the Appellate Authority</p>
              </div>
              <button
                onClick={() => setShowGroundModal(true)}
                className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-medium shadow-sm"
              >
                + Add Ground of Appeal
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
              <div className="bg-gray-50 p-3.5 rounded-lg border border-gray-200 space-y-1">
                <span className="font-semibold text-gray-700 uppercase">Statement of Facts:</span>
                <p className="text-gray-800 whitespace-pre-line leading-relaxed">{appealDetail.statementOfFacts}</p>
              </div>
              <div className="bg-indigo-50/50 p-3.5 rounded-lg border border-indigo-100 space-y-1">
                <span className="font-semibold text-indigo-900 uppercase">Prayers / Relief Sought:</span>
                <p className="text-indigo-950 whitespace-pre-line leading-relaxed">{appealDetail.reliefSought}</p>
              </div>
            </div>
          </div>

          {/* Grounds List */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-gray-200 flex items-center justify-between">
              <h3 className="font-semibold text-gray-900 text-base">Structured Grounds of Appeal ({appealDetail.grounds.length})</h3>
              <span className="text-xs text-gray-500">Each ground is independently evaluated and legally autonomous</span>
            </div>

            <div className="divide-y divide-gray-200">
              {appealDetail.grounds.map(g => (
                <div key={g.id} className="p-5 hover:bg-gray-50 transition-colors">
                  <div className="flex items-start justify-between gap-4">
                    <div className="space-y-2 flex-1">
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-gray-900 text-sm">Ground #{g.groundNumber}:</span>
                        <span className="font-semibold text-gray-800 text-sm">{g.title}</span>
                        <span className="px-2 py-0.5 rounded text-xs font-semibold bg-gray-100 text-gray-700">
                          {g.groundType}
                        </span>
                        {g.isWithoutPrejudice && (
                          <span className="px-2 py-0.5 rounded text-xs font-semibold bg-amber-100 text-amber-800">
                            Without Prejudice
                          </span>
                        )}
                        {g.isAlternativeGround && (
                          <span className="px-2 py-0.5 rounded text-xs font-semibold bg-indigo-100 text-indigo-800">
                            Alternative Plea
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-gray-700 leading-relaxed text-justify bg-gray-50 p-3 rounded-lg border border-gray-100">
                        {g.groundText}
                      </p>
                      {g.disputedAdditionAmount > 0 && (
                        <div className="flex items-center gap-3 text-xs text-gray-500">
                          <span>Statutory Section: <b className="text-gray-800">{g.statutorySection}</b></span>
                          <span>•</span>
                          <span>Disputed Addition: <b className="text-rose-600">{formatINR(g.disputedAdditionAmount)}</b></span>
                        </div>
                      )}
                    </div>
                    <button
                      onClick={() => handleDeleteGround(g.id)}
                      className="text-xs text-rose-600 hover:text-rose-800"
                    >
                      Delete
                    </button>
                  </div>
                </div>
              ))}

              {appealDetail.grounds.length === 0 && (
                <div className="p-8 text-center text-gray-500 text-xs">
                  No grounds of appeal recorded yet. Click &quot;+ Add Ground of Appeal&quot; above.
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 3: ITAT Rule 18 Paper Book Compiler */}
      {/* ========================================================================= */}
      {activeTab === 'paperbook' && appealDetail && (
        <div className="space-y-6">
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-gray-200 flex flex-col md:flex-row md:items-center justify-between gap-3">
              <div>
                <h3 className="font-semibold text-gray-900 text-base">ITAT Rule 18 Paginated Paper Book Index</h3>
                <p className="text-xs text-gray-500 mt-0.5">
                  Conforming to ITAT Rule 18(1) consecutive pagination &amp; Rule 18(6) mandatory certificate of true copies
                </p>
              </div>
              <button
                onClick={() => setShowDocModal(true)}
                className="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-medium shadow-sm"
              >
                + Add Document to Paper Book
              </button>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                <thead className="bg-gray-50 text-gray-600 uppercase font-semibold">
                  <tr>
                    <th className="px-4 py-3 w-12 text-center">Item #</th>
                    <th className="px-4 py-3 w-28">Doc Date</th>
                    <th className="px-4 py-3">Description of Document &amp; Evidence</th>
                    <th className="px-4 py-3">Relevance to Ground</th>
                    <th className="px-4 py-3 w-36">Evidence Category</th>
                    <th className="px-4 py-3 w-28 text-center">Page Nos.</th>
                    <th className="px-4 py-3 w-20 text-center">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200 text-gray-700">
                  {appealDetail.paperBookDocuments.map(doc => (
                    <tr key={doc.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 text-center font-bold text-gray-900">{doc.itemNumber}</td>
                      <td className="px-4 py-3 font-mono">{doc.documentDate}</td>
                      <td className="px-4 py-3 font-medium text-gray-900">{doc.descriptionOfDocument}</td>
                      <td className="px-4 py-3 text-gray-500 max-w-xs">{doc.relevance}</td>
                      <td className="px-4 py-3">
                        <span className={`px-2 py-0.5 rounded text-xs font-semibold ${
                          doc.evidenceType === 'PartLowerAuthoritiesRecord'
                            ? 'bg-blue-100 text-blue-800'
                            : 'bg-amber-100 text-amber-800'
                        }`}>
                          {doc.evidenceType === 'PartLowerAuthoritiesRecord' ? 'Lower Record' : 'Rule 29 Addl. Evidence'}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-center font-mono font-bold text-indigo-700 bg-indigo-50/50">
                        {doc.pageNumberFrom} – {doc.pageNumberTo}
                        <div className="text-[10px] text-gray-400 font-normal">({doc.totalPages} pp)</div>
                      </td>
                      <td className="px-4 py-3 text-center">
                        <button
                          onClick={() => handleDeleteDoc(doc.id)}
                          className="text-xs text-rose-600 hover:text-rose-800"
                        >
                          Delete
                        </button>
                      </td>
                    </tr>
                  ))}
                  {appealDetail.paperBookDocuments.length === 0 && (
                    <tr>
                      <td colSpan={7} className="px-4 py-8 text-center text-gray-500">
                        No documents compiled in Paper Book yet. Click &quot;+ Add Document to Paper Book&quot;.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* Rule 18(6) True Copy Certificate Preview */}
          <div className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm space-y-3">
            <div className="flex items-center justify-between">
              <h3 className="font-bold text-gray-900 text-sm">Certificate under Rule 18(6) of ITAT Rules, 1963</h3>
              <span className="text-xs font-mono text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
                Statutory True Copy Attestation
              </span>
            </div>
            <p className="text-xs text-gray-600 leading-relaxed font-serif bg-gray-50 p-4 rounded-lg border border-gray-200">
              &quot;Certified that documents running from Page Nos. 1 to {appealDetail.paperBookDocuments.length > 0 ? appealDetail.paperBookDocuments[appealDetail.paperBookDocuments.length - 1].pageNumberTo : 1} are true, correct, and complete copies of the original documents and evidence that formed part of the record before the Assessing Officer and/or CIT(Appeals). Each document was duly filed before lower authorities, except as stated under Rule 29.&quot;
            </p>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 4: Judicial Authorities & Certification */}
      {/* ========================================================================= */}
      {activeTab === 'citations' && appealDetail && (
        <div className="space-y-6">
          {/* Annexed Judicial Authorities */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-gray-200 flex items-center justify-between">
              <div>
                <h3 className="font-semibold text-gray-900 text-base">Annexed Landmark Judicial Authorities</h3>
                <p className="text-xs text-gray-500 mt-0.5">Precedents and landmark ratios cited in support of Grounds of Appeal</p>
              </div>
              <button
                onClick={() => setShowCitationModal(true)}
                className="px-3.5 py-1.5 bg-purple-600 hover:bg-purple-700 text-white rounded-lg text-xs font-medium shadow-sm"
              >
                + Link Landmark Ruling
              </button>
            </div>

            <div className="divide-y divide-gray-200">
              {appealDetail.citations.map(c => (
                <div key={c.id} className="p-5 hover:bg-gray-50 transition-colors">
                  <div className="flex items-start justify-between gap-4">
                    <div className="space-y-1.5 flex-1">
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-gray-900 text-sm">{c.caseTitle}</span>
                        <span className="font-mono text-xs font-semibold text-indigo-700 bg-indigo-50 px-2 py-0.5 rounded border border-indigo-200">
                          {c.citation}
                        </span>
                        <span className="text-xs text-gray-500 font-medium">({c.courtOrTribunal}, {c.judgmentYear})</span>
                      </div>
                      <p className="text-xs text-gray-700 leading-relaxed bg-purple-50/50 p-3 rounded-lg border border-purple-100 font-serif">
                        <b className="text-purple-950">Ratio Decidendi:</b> {c.keyRatioProposition} {c.paragraphReference ? `[${c.paragraphReference}]` : ''}
                      </p>
                      <div className="text-xs text-gray-500">
                        <span>Relevance: <b>{c.relevanceToGround}</b></span>
                      </div>
                    </div>
                    <button
                      onClick={() => handleDeleteCitation(c.id)}
                      className="text-xs text-rose-600 hover:text-rose-800"
                    >
                      Delete
                    </button>
                  </div>
                </div>
              ))}

              {appealDetail.citations.length === 0 && (
                <div className="p-8 text-center text-gray-500 text-xs">
                  No judicial citations linked yet. Click &quot;+ Link Landmark Ruling&quot; to cite Supreme Court or High Court authorities.
                </div>
              )}
            </div>
          </div>

          {/* Partner Certification Panel */}
          <div className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-bold text-gray-900 text-base">Partner Sign-off &amp; e-Filing Verification</h3>
                <p className="text-xs text-gray-500 mt-0.5">Authorised Representative ICAI Membership Verification</p>
              </div>
              <button
                onClick={() => setShowCertifyModal(true)}
                className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-bold shadow-sm"
              >
                ✓ Partner Certify Appeal
              </button>
            </div>

            {appealDetail.partnerMembershipNumber ? (
              <div className="p-4 bg-emerald-50 rounded-xl border border-emerald-200 text-xs space-y-1 text-emerald-900">
                <div className="font-bold text-emerald-950 flex items-center gap-2">
                  <span>✓ Certified by Authorised Representative:</span>
                  <span>{appealDetail.partnerCertifiedByName} (ICAI Membership #{appealDetail.partnerMembershipNumber})</span>
                </div>
                <div>Attestation Timestamp: {appealDetail.partnerCertifiedAt ? new Date(appealDetail.partnerCertifiedAt).toLocaleString() : 'Recent'}</div>
                {appealDetail.dinOrAckNumber && <div>e-Filing Acknowledgement / DIN: <b className="font-mono">{appealDetail.dinOrAckNumber}</b></div>}
              </div>
            ) : (
              <div className="p-4 bg-gray-50 rounded-xl border border-gray-200 text-xs text-gray-600">
                This appeal is currently in Draft. Once grounds and Paper Book documents are finalized, the Partner should certify the dossier.
              </div>
            )}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 1: Create New Tax Appeal */}
      {/* ========================================================================= */}
      {showAppealModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-4 border border-gray-100 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Draft New Tax Appeal Memorandum</h3>
              <button onClick={() => setShowAppealModal(false)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
            </div>

            <form onSubmit={handleSaveAppeal} className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Target Client Entity</label>
                  <select
                    value={appealForm.clientId}
                    onChange={e => setAppealForm({ ...appealForm, clientId: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  >
                    <option value="">-- Select Client --</option>
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Appellate Forum</label>
                  <select
                    value={appealForm.forum}
                    onChange={e => setAppealForm({ ...appealForm, forum: e.target.value as AppealForum })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  >
                    <option value="CitAppeals">CIT(Appeals) • Form 35 (Sec 246A / 249)</option>
                    <option value="Itat">ITAT Tribunal • Form 36 (Sec 253)</option>
                    <option value="HighCourt">High Court • Sec 260A</option>
                    <option value="SupremeCourt">Supreme Court • SLP</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Assessment Year</label>
                  <input
                    type="text"
                    value={appealForm.assessmentYear}
                    onChange={e => setAppealForm({ ...appealForm, assessmentYear: e.target.value })}
                    placeholder="2024-25"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Financial Year</label>
                  <input
                    type="text"
                    value={appealForm.financialYear}
                    onChange={e => setAppealForm({ ...appealForm, financialYear: e.target.value })}
                    placeholder="2023-24"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Appeal Number (if known)</label>
                  <input
                    type="text"
                    value={appealForm.appealNumber || ''}
                    onChange={e => setAppealForm({ ...appealForm, appealNumber: e.target.value })}
                    placeholder="e.g. ITA No. 1045/DEL/2026"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Respondent Assessing Officer Ward/Circle</label>
                  <input
                    type="text"
                    value={appealForm.assessingOfficerWardOrCircle}
                    onChange={e => setAppealForm({ ...appealForm, assessingOfficerWardOrCircle: e.target.value })}
                    placeholder="Circle 1(1)(1), New Delhi"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Impugned Order Section</label>
                  <input
                    type="text"
                    value={appealForm.impugnedOrderSection}
                    onChange={e => setAppealForm({ ...appealForm, impugnedOrderSection: e.target.value })}
                    placeholder="Sec 143(3) r.w.s. 144B"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Order Number</label>
                  <input
                    type="text"
                    value={appealForm.impugnedOrderNumber}
                    onChange={e => setAppealForm({ ...appealForm, impugnedOrderNumber: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono text-xs"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Order Date</label>
                  <input
                    type="date"
                    value={appealForm.impugnedOrderDate}
                    onChange={e => setAppealForm({ ...appealForm, impugnedOrderDate: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Date of Service (Clock Start)</label>
                  <input
                    type="date"
                    value={appealForm.dateOfServiceOfOrder}
                    onChange={e => setAppealForm({ ...appealForm, dateOfServiceOfOrder: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-semibold text-indigo-700"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Assessed Total Income (₹)</label>
                  <input
                    type="number"
                    value={appealForm.assessedTotalIncome}
                    onChange={e => setAppealForm({ ...appealForm, assessedTotalIncome: parseFloat(e.target.value) || 0 })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Disputed Additions (₹)</label>
                  <input
                    type="number"
                    value={appealForm.totalDisputedAdditions}
                    onChange={e => setAppealForm({ ...appealForm, totalDisputedAdditions: parseFloat(e.target.value) || 0 })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Disputed Tax Demand (₹)</label>
                  <input
                    type="number"
                    value={appealForm.disputedTaxDemand}
                    onChange={e => setAppealForm({ ...appealForm, disputedTaxDemand: parseFloat(e.target.value) || 0 })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono text-rose-600 font-bold"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Statement of Facts</label>
                <textarea
                  rows={3}
                  value={appealForm.statementOfFacts}
                  onChange={e => setAppealForm({ ...appealForm, statementOfFacts: e.target.value })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Relief Sought / Prayers</label>
                <textarea
                  rows={2}
                  value={appealForm.reliefSought}
                  onChange={e => setAppealForm({ ...appealForm, reliefSought: e.target.value })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  required
                />
              </div>

              <div className="pt-3 border-t flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setShowAppealModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-medium shadow-sm disabled:opacity-50"
                >
                  {loading ? 'Saving...' : 'Save & Compute Limitations'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 2: Add Ground of Appeal */}
      {/* ========================================================================= */}
      {showGroundModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-4 border border-gray-100">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Add Ground of Appeal</h3>
              <button onClick={() => setShowGroundModal(false)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
            </div>

            <form onSubmit={handleSaveGround} className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Ground Number</label>
                  <input
                    type="number"
                    value={groundForm.groundNumber}
                    onChange={e => setGroundForm({ ...groundForm, groundNumber: parseInt(e.target.value) || 1 })}
                    min={1}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Ground Type</label>
                  <select
                    value={groundForm.groundType}
                    onChange={e => setGroundForm({ ...groundForm, groundType: e.target.value as GroundType })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  >
                    <option value="MeritsAddition">Merits: Addition/Disallowance</option>
                    <option value="JurisdictionOrLimitation">Jurisdiction &amp; Limitation</option>
                    <option value="NaturalJustice">Natural Justice Violation</option>
                    <option value="Alternative">Alternative Ground</option>
                    <option value="WithoutPrejudice">Without Prejudice Ground</option>
                    <option value="General">General Ground</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Title / Caption</label>
                <input
                  type="text"
                  value={groundForm.title}
                  onChange={e => setGroundForm({ ...groundForm, title: e.target.value })}
                  placeholder="e.g. Addition u/s 68 Unexplained Cash Credits"
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  required
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Statutory Section</label>
                  <input
                    type="text"
                    value={groundForm.statutorySection}
                    onChange={e => setGroundForm({ ...groundForm, statutorySection: e.target.value })}
                    placeholder="Sec 68"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Disputed Amount (₹)</label>
                  <input
                    type="number"
                    value={groundForm.disputedAdditionAmount}
                    onChange={e => setGroundForm({ ...groundForm, disputedAdditionAmount: parseFloat(e.target.value) || 0 })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Ground Text / Legal Argument</label>
                <textarea
                  rows={4}
                  value={groundForm.groundText}
                  onChange={e => setGroundForm({ ...groundForm, groundText: e.target.value })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  required
                />
              </div>

              <div className="flex items-center gap-4 text-xs text-gray-700 pt-1">
                <label className="flex items-center gap-1.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={groundForm.isWithoutPrejudice}
                    onChange={e => setGroundForm({ ...groundForm, isWithoutPrejudice: e.target.checked })}
                    className="rounded text-indigo-600"
                  />
                  Without Prejudice
                </label>
                <label className="flex items-center gap-1.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={groundForm.isAlternativeGround}
                    onChange={e => setGroundForm({ ...groundForm, isAlternativeGround: e.target.checked })}
                    className="rounded text-indigo-600"
                  />
                  Alternative Plea
                </label>
              </div>

              <div className="pt-3 border-t flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setShowGroundModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-medium shadow-sm"
                >
                  Add Ground
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 3: Add Paper Book Document */}
      {/* ========================================================================= */}
      {showDocModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-4 border border-gray-100">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Add Document to Paper Book (ITAT Rule 18)</h3>
              <button onClick={() => setShowDocModal(false)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
            </div>

            <form onSubmit={handleSaveDoc} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Description of Document / Evidence</label>
                <input
                  type="text"
                  value={docForm.descriptionOfDocument}
                  onChange={e => setDocForm({ ...docForm, descriptionOfDocument: e.target.value })}
                  placeholder="e.g. Bank Statements, Confirmations, Written Submissions"
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  required
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Document Date</label>
                  <input
                    type="date"
                    value={docForm.documentDate}
                    onChange={e => setDocForm({ ...docForm, documentDate: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Total Page Count</label>
                  <input
                    type="number"
                    value={docForm.pageCount}
                    onChange={e => setDocForm({ ...docForm, pageCount: parseInt(e.target.value) || 1 })}
                    min={1}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Evidence Category</label>
                <select
                  value={docForm.evidenceType}
                  onChange={e => setDocForm({ ...docForm, evidenceType: e.target.value as PaperBookEvidenceType })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                >
                  <option value="PartLowerAuthoritiesRecord">Part of Lower Authorities&apos; Record (AO / CIT(A))</option>
                  <option value="Rule29AdditionalEvidence">Rule 29 Additional Evidence (Requires separate application)</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Relevance to Ground of Appeal</label>
                <textarea
                  rows={2}
                  value={docForm.relevance}
                  onChange={e => setDocForm({ ...docForm, relevance: e.target.value })}
                  placeholder="Explains why this document substantiates the ground..."
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                />
              </div>

              <div className="pt-3 border-t flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setShowDocModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-sm font-medium shadow-sm"
                >
                  Add &amp; Re-Paginate
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 4: Link Landmark Judicial Authority */}
      {/* ========================================================================= */}
      {showCitationModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-4 border border-gray-100">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Link Landmark Judicial Authority</h3>
              <button onClick={() => setShowCitationModal(false)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
            </div>

            <form onSubmit={handleAddCitation} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Case Title</label>
                <input
                  type="text"
                  value={citationForm.caseTitle}
                  onChange={e => setCitationForm({ ...citationForm, caseTitle: e.target.value })}
                  placeholder="e.g. CIT v. Excel Industries Ltd"
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  required
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Citation</label>
                  <input
                    type="text"
                    value={citationForm.citation}
                    onChange={e => setCitationForm({ ...citationForm, citation: e.target.value })}
                    placeholder="[2013] 358 ITR 295 (SC)"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Court / Tribunal</label>
                  <input
                    type="text"
                    value={citationForm.courtOrTribunal}
                    onChange={e => setCitationForm({ ...citationForm, courtOrTribunal: e.target.value })}
                    placeholder="Supreme Court of India"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Key Ratio Decidendi / Proposition</label>
                <textarea
                  rows={3}
                  value={citationForm.keyRatioProposition}
                  onChange={e => setCitationForm({ ...citationForm, keyRatioProposition: e.target.value })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-serif"
                  required
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Paragraph Reference</label>
                  <input
                    type="text"
                    value={citationForm.paragraphReference}
                    onChange={e => setCitationForm({ ...citationForm, paragraphReference: e.target.value })}
                    placeholder="Paras 28-32"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Relevance to Ground</label>
                  <input
                    type="text"
                    value={citationForm.relevanceToGround}
                    onChange={e => setCitationForm({ ...citationForm, relevanceToGround: e.target.value })}
                    placeholder="Supports Ground 1"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  />
                </div>
              </div>

              <div className="pt-3 border-t flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setShowCitationModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg text-sm font-medium shadow-sm"
                >
                  Link Authority
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 5: Partner Certify Appeal */}
      {/* ========================================================================= */}
      {showCertifyModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-gray-100">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Partner Certification &amp; Sign-off</h3>
              <button onClick={() => setShowCertifyModal(false)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
            </div>

            <form onSubmit={handleCertifyAppeal} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Authorised Partner Name</label>
                <input
                  type="text"
                  value={certifyForm.partnerName}
                  onChange={e => setCertifyForm({ ...certifyForm, partnerName: e.target.value })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">ICAI Membership Number</label>
                <input
                  type="text"
                  value={certifyForm.partnerMembershipNumber}
                  onChange={e => setCertifyForm({ ...certifyForm, partnerMembershipNumber: e.target.value })}
                  placeholder="6 digits"
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">e-Filing Acknowledgement / DIN</label>
                <input
                  type="text"
                  value={certifyForm.dinOrAckNumber || ''}
                  onChange={e => setCertifyForm({ ...certifyForm, dinOrAckNumber: e.target.value })}
                  placeholder="ACK/ITAT/2026/..."
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                />
              </div>

              <div className="pt-3 border-t flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setShowCertifyModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-sm font-medium shadow-sm"
                >
                  Certify &amp; Stamp
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 6: Statutory Fee Calculator */}
      {/* ========================================================================= */}
      {showFeeModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 border border-gray-100">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Statutory Appeal Filing Fee Calculator</h3>
              <button onClick={() => setShowFeeModal(false)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
            </div>

            <form onSubmit={handleCalculateFee} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Appellate Forum</label>
                <select
                  value={feeForm.forum}
                  onChange={e => setFeeForm({ ...feeForm, forum: e.target.value as AppealForum })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                >
                  <option value="CitAppeals">CIT(Appeals) • Sec 249(1)</option>
                  <option value="Itat">ITAT Tribunal • Sec 253(6)</option>
                  <option value="HighCourt">High Court • Sec 260A</option>
                  <option value="SupremeCourt">Supreme Court • SLP</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Assessed Total Income (₹)</label>
                <input
                  type="number"
                  value={feeForm.assessedTotalIncome}
                  onChange={e => setFeeForm({ ...feeForm, assessedTotalIncome: parseFloat(e.target.value) || 0 })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                  required
                />
              </div>

              <div className="text-xs text-gray-700">
                <label className="flex items-center gap-1.5 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={feeForm.isOtherMatter}
                    onChange={e => setFeeForm({ ...feeForm, isOtherMatter: e.target.checked })}
                    className="rounded text-indigo-600"
                  />
                  Subject matter does not relate to assessed income (penalty, 12AA, stay)
                </label>
              </div>

              <button
                type="submit"
                className="w-full py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold shadow-sm"
              >
                Compute Statutory Fee
              </button>

              {feeResult && (
                <div className="p-4 bg-indigo-50 rounded-xl border border-indigo-200 text-xs space-y-1 mt-3">
                  <div className="flex justify-between items-baseline">
                    <span className="text-gray-600">Statutory Appeal Fee:</span>
                    <span className="text-xl font-extrabold text-indigo-900">{formatINR(feeResult.statutoryFilingFee)}</span>
                  </div>
                  <div className="text-gray-500 pt-1 border-t border-indigo-100">{feeResult.legalBasis}</div>
                </div>
              )}
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 7: View Full HTML Appeal Dossier */}
      {/* ========================================================================= */}
      {showDossierModal && (
        <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-4xl w-full h-[90vh] flex flex-col p-6 shadow-2xl border border-gray-100">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Print-Ready Statutory Appeal Dossier &amp; Paper Book</h3>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => {
                    const win = window.open('', '_blank')
                    win?.document.write(dossierHtml)
                    win?.document.close()
                    win?.print()
                  }}
                  className="px-3.5 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-medium shadow-sm"
                >
                  🖨️ Print / Save as PDF
                </button>
                <button onClick={() => setShowDossierModal(false)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
              </div>
            </div>

            <div className="flex-1 mt-4 overflow-hidden border border-gray-200 rounded-xl">
              <iframe
                title="Statutory Appeal Dossier"
                srcDoc={dossierHtml}
                className="w-full h-full"
              />
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
