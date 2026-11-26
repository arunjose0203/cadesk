import { useState, useEffect, useCallback } from 'react'
import {
  toError,
  taxCopilotApi,
  taxNoticesApi,
  api,
  type TaxAuthority,
  type StatutoryPrecedentDto,
  type ParsedNoticeResultDto,
  type TaxDefenseDraftDto,
  type TaxNoticeDto,
  type Client,
  type ClientPage,
  type FactualItem,
  type MeritGroundItem,
  type CitedPrecedentItem,
  type AnnexureItem,
  type CreateTaxNoticeRequest
} from './api'

interface TaxCopilotDeskProps {
  can?: (p: string) => boolean
  initialNoticeId?: string | null
  onOpenNoticeLedger?: () => void
}

const SAMPLE_NOTICES = {
  sec148a: `GOVERNMENT OF INDIA
MINISTRY OF FINANCE
INCOME TAX DEPARTMENT
OFFICE OF THE NATIONAL FACELESS ASSESSMENT CENTRE, DELHI

Notice under clause (b) of Section 148A of the Income-tax Act, 1961
DIN: ITBA/AST/S/148A/2023-24/1058291029(1)
Date of Issue: 15/03/2024

To:
PAN: AABCB1234F
M/S BHARAT INFRASTRUCTURES LTD
Assessment Year: 2021-22
Financial Year: 2020-21

Sub: Show Cause Notice under Section 148A(b) - Escapement of Income

Sir/Madam,
Information has been flagged in the Insight Portal under Risk Management Strategy (RMS) that you have made high-value cash deposits of Rs. 75,00,000 during FY 2020-21.
You are hereby required to show cause on or before 30/03/2024 why notice u/s 148 should not be issued.
`,
  gstDrc01c: `FORM GST DRC-01C
[See Rule 88D]
Reference No: GST/DRC01C/27AAAAA0000A1Z5/2023-24/098
Date: 10/11/2023

GSTIN: 27AAAAA0000A1Z5
Legal Name: VANGUARD TRADING CORP
Financial Year: 2022-23

Intimation of difference in input tax credit available in Form GSTR-2B and that availed in Form GSTR-3B
Part A (System Generated)
It is observed that the ITC availed in Form GSTR-3B exceeds the ITC available in Form GSTR-2B by Rs. 8,45,000.
You are directed to pay the excess ITC along with interest or furnish reasons within 7 days.
Due Date: 17/11/2023
`,
  sec143_1: `CENTRALIZED PROCESSING CENTRE, BENGALURU
INTIMATION UNDER SECTION 143(1) OF THE INCOME TAX ACT, 1961
DIN: 20230987654321
PAN: AABCP9999K
Assessment Year: 2023-24
Date: 05/12/2023

Adjustment made under Section 143(1)(a):
TDS credit claimed in return differs from Form 26AS.
Disallowance of Rs. 1,25,000 under Chapter VI-A.
Demand payable: Rs. 1,48,500.
Please submit rectification or payment within 30 days.
`,
  sec270a: `OFFICE OF THE ASSISTANT COMMISSIONER OF INCOME TAX, CIRCLE 1(1), MUMBAI
Notice for penalty under Section 270A of the Income-tax Act, 1961
DIN: ITBA/PNL/S/270A/2024-25/00112233
PAN: ABCDE5555M
Assessment Year: 2022-23
Date: 12/01/2025

Whereas in the course of assessment proceedings, it appears that you have under-reported your income.
Proposed penalty of Rs. 15,00,000 is initiated.
Show cause why penalty under section 270A should not be imposed.
`
}

export function TaxCopilotDesk({ can, initialNoticeId, onOpenNoticeLedger }: TaxCopilotDeskProps) {
  const [activeTab, setActiveTab] = useState<'scanner' | 'precedents' | 'drafter'>('scanner')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Scanner state
  const [rawNoticeText, setRawNoticeText] = useState(SAMPLE_NOTICES.sec148a)
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [clients, setClients] = useState<Client[]>([])
  const [parsedNotice, setParsedNotice] = useState<ParsedNoticeResultDto | null>(null)
  const [selectedObjections, setSelectedObjections] = useState<string[]>([])
  const [selectedGrounds, setSelectedGrounds] = useState<string[]>([])

  // Precedents Library state
  const [precedents, setPrecedents] = useState<StatutoryPrecedentDto[]>([])
  const [precedentAuthority, setPrecedentAuthority] = useState<TaxAuthority | ''>('')
  const [precedentSearch, setPrecedentSearch] = useState('')
  const [copiedId, setCopiedId] = useState<string | null>(null)

  // Drafter state
  const [notices, setNotices] = useState<TaxNoticeDto[]>([])
  const [activeNoticeId, setActiveNoticeId] = useState<string>(initialNoticeId || '')
  const [activeDraft, setActiveDraft] = useState<TaxDefenseDraftDto | null>(null)
  const [noticeDrafts, setNoticeDrafts] = useState<TaxDefenseDraftDto[]>([])

  // Draft editing state
  const [draftTitle, setDraftTitle] = useState('')
  const [draftSubject, setDraftSubject] = useState('')
  const [draftAddressedTo, setDraftAddressedTo] = useState('')
  const [draftObjections, setDraftObjections] = useState<string[]>([])
  const [draftFacts, setDraftFacts] = useState<FactualItem[]>([])
  const [draftGrounds, setDraftGrounds] = useState<MeritGroundItem[]>([])
  const [draftPrecedents, setDraftPrecedents] = useState<CitedPrecedentItem[]>([])
  const [draftPrayer, setDraftPrayer] = useState('')
  const [draftAnnexures, setDraftAnnexures] = useState<AnnexureItem[]>([])

  // Partner approval modal
  const [showApprovalModal, setShowApprovalModal] = useState(false)
  const [partnerUdin, setPartnerUdin] = useState('')
  const [partnerNotes, setPartnerNotes] = useState('')

  // Preview modal
  const [showPreviewModal, setShowPreviewModal] = useState(false)
  const [previewHtml, setPreviewHtml] = useState('')

  const loadClients = useCallback(async () => {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      setClients(res.items || [])
      if (res.items && res.items.length > 0) {
        setSelectedClientId(prev => prev || res.items[0].id)
      }
    } catch {
      // client load optional
    }
  }, [])

  const populateDraftFields = useCallback((draft: TaxDefenseDraftDto) => {
    setDraftTitle(draft.title)
    setDraftSubject(draft.subject)
    setDraftAddressedTo(draft.addressedTo)
    setDraftObjections(draft.preliminaryObjections || [])
    setDraftFacts(draft.factualMatrix || [])
    setDraftGrounds(draft.meritGrounds || [])
    setDraftPrecedents(draft.citedPrecedents || [])
    setDraftPrayer(draft.prayerForRelief)
    setDraftAnnexures(draft.annexureIndex || [])
  }, [])

  const loadDraftsForNotice = useCallback(async (noticeId: string) => {
    if (!noticeId) return
    setLoading(true)
    try {
      const drafts = await taxCopilotApi.getNoticeDrafts(noticeId)
      setNoticeDrafts(drafts)
      if (drafts.length > 0) {
        setActiveDraft(drafts[0])
        populateDraftFields(drafts[0])
      } else {
        setActiveDraft(null)
      }
    } catch (err) {
      setError(toError(err).message)
    } finally {
      setLoading(false)
    }
  }, [populateDraftFields])

  useEffect(() => {
    loadClients()
    loadNotices()
    loadPrecedents()
  }, [loadClients])

  useEffect(() => {
    if (initialNoticeId) {
      setActiveNoticeId(initialNoticeId)
      loadDraftsForNotice(initialNoticeId)
      setActiveTab('drafter')
    }
  }, [initialNoticeId, loadDraftsForNotice])

  async function loadNotices() {
    try {
      const res = await taxNoticesApi.getNotices()
      setNotices(res || [])
    } catch {
      // ignore
    }
  }

  async function loadPrecedents(auth?: string, search?: string) {
    try {
      const res = await taxCopilotApi.getPrecedents({
        authority: auth || undefined,
        search: search || undefined
      })
      setPrecedents(res || [])
    } catch (err) {
      console.error(err)
    }
  }

  async function handleAnalyzeNotice() {
    if (!rawNoticeText.trim()) {
      setError('Please paste or enter notice text to analyze.')
      return
    }

    setLoading(true)
    setError(null)
    setSuccess(null)

    try {
      const res = await taxCopilotApi.parseNotice({
        rawText: rawNoticeText,
        clientId: selectedClientId || undefined
      })
      setParsedNotice(res)
      setSelectedObjections(res.recommendedPreliminaryObjections || [])
      setSelectedGrounds(res.recommendedGrounds || [])
      setSuccess(`Notice categorized successfully as ${res.section} (${res.authority}) with ${res.confidenceScore}% confidence.`)
    } catch (err) {
      setError(toError(err).message)
    } finally {
      setLoading(false)
    }
  }

  async function handleCreateNoticeAndLaunchDraft() {
    if (!parsedNotice) return
    if (!selectedClientId) {
      setError('Please select a client to link this notice to.')
      return
    }

    setLoading(true)
    setError(null)

    try {
      // 1. Create notice in system
      const noticeReq: CreateTaxNoticeRequest = {
        clientId: selectedClientId,
        authority: parsedNotice.authority,
        category: parsedNotice.category,
        section: parsedNotice.section,
        noticeNumber: parsedNotice.din || `NOT-${Date.now().toString().slice(-6)}`,
        din: parsedNotice.din,
        assessmentYear: parsedNotice.assessmentYear,
        financialYear: parsedNotice.financialYear || '2023-24',
        issueDate: parsedNotice.issueDate || new Date().toISOString().split('T')[0],
        dueDate: parsedNotice.dueDate || new Date(Date.now() + 15 * 86400000).toISOString().split('T')[0],
        demandAmount: parsedNotice.demandAmount,
        refundDisallowed: 0,
        subject: parsedNotice.extractedIssue,
        description: parsedNotice.summaryOfAllegations,
        officerJurisdiction: parsedNotice.officerJurisdiction
      }

      const createdNotice = await taxNoticesApi.createNotice(noticeReq)

      // 2. Initialize defense draft with chosen objections & grounds
      const meritGrounds: MeritGroundItem[] = selectedGrounds.map((g: string, idx: number) => ({
        groundNo: idx + 1,
        title: g.length > 50 ? g.slice(0, 50) + '...' : g,
        argument: g,
        relevantSection: parsedNotice.section
      }))

      const citedPrecedents: CitedPrecedentItem[] = (parsedNotice.matchedPrecedents || []).map((p: StatutoryPrecedentDto) => ({
        precedentId: p.id,
        caseTitle: p.caseTitle,
        citation: p.citation,
        courtBench: p.courtBench,
        relevantHolding: p.keyRatioDecidendi
      }))

      const draft = await taxCopilotApi.createDefenseDraft({
        noticeId: createdNotice.id,
        preliminaryObjections: selectedObjections,
        meritGrounds: meritGrounds,
        citedPrecedents: citedPrecedents
      })

      // 3. Switch to Drafter tab
      await loadNotices()
      setActiveNoticeId(createdNotice.id)
      setActiveDraft(draft)
      populateDraftFields(draft)
      setActiveTab('drafter')
      setSuccess(`Notice ${createdNotice.noticeNumber} registered and Defense Draft ${draft.draftNumber} initialized!`)
    } catch (err) {
      setError(toError(err).message)
    } finally {
      setLoading(false)
    }
  }

  async function handleCreateNewDraftForNotice() {
    if (!activeNoticeId) return
    setLoading(true)
    setError(null)
    try {
      const draft = await taxCopilotApi.createDefenseDraft({
        noticeId: activeNoticeId
      })
      await loadDraftsForNotice(activeNoticeId)
      setActiveDraft(draft)
      populateDraftFields(draft)
      setSuccess(`New Defense Draft ${draft.draftNumber} created.`)
    } catch (err) {
      setError(toError(err).message)
    } finally {
      setLoading(false)
    }
  }

  async function handleSaveDraft() {
    if (!activeDraft) return
    setLoading(true)
    setError(null)
    try {
      const updated = await taxCopilotApi.updateDefenseDraft(activeDraft.id, {
        title: draftTitle,
        subject: draftSubject,
        addressedTo: draftAddressedTo,
        preliminaryObjections: draftObjections,
        factualMatrix: draftFacts,
        meritGrounds: draftGrounds,
        citedPrecedents: draftPrecedents,
        prayerForRelief: draftPrayer,
        annexureIndex: draftAnnexures
      })
      setActiveDraft(updated)
      setSuccess(`Defense Draft ${updated.draftNumber} saved successfully.`)
    } catch (err) {
      setError(toError(err).message)
    } finally {
      setLoading(false)
    }
  }

  async function handlePartnerApprove() {
    if (!activeDraft) return
    setLoading(true)
    setError(null)
    try {
      const approved = await taxCopilotApi.approveDefenseDraft(activeDraft.id, {
        partnerUdin: partnerUdin || undefined,
        partnerNotes: partnerNotes || undefined
      })
      setActiveDraft(approved)
      setShowApprovalModal(false)
      setSuccess(`Draft ${approved.draftNumber} officially approved by Partner with UDIN ${approved.partnerUdin || 'N/A'}!`)
      await loadNotices()
    } catch (err) {
      setError(toError(err).message)
    } finally {
      setLoading(false)
    }
  }

  async function handleViewExportHtml() {
    if (!activeDraft) return
    try {
      const html = activeDraft.generatedHtml || ''
      setPreviewHtml(html)
      setShowPreviewModal(true)
    } catch (err) {
      setError(toError(err).message)
    }
  }

  function copyToClipboard(text: string, id: string) {
    navigator.clipboard.writeText(text)
    setCopiedId(id)
    setTimeout(() => setCopiedId(null), 2500)
  }

  return (
    <div className="space-y-6">
      {/* Header Banner */}
      <div className="bg-gradient-to-r from-slate-900 via-indigo-950 to-blue-900 rounded-xl p-6 text-white shadow-lg border border-indigo-700/40">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-3">
              <span className="text-2xl">🤖</span>
              <h2 className="text-2xl font-bold tracking-tight">CA Copilot: Notice Defense &amp; Statutory Citation Engine</h2>
              <span className="bg-indigo-500/30 text-indigo-200 border border-indigo-400/30 text-xs px-2.5 py-0.5 rounded-full font-semibold uppercase tracking-wider">
                Income Tax &bull; GST &bull; Faceless
              </span>
            </div>
            <p className="text-indigo-200 text-sm mt-1">
              AI-assisted notice text/OCR categorization, 60+ landmark Supreme Court &amp; High Court precedents, and 1-click statutory legal reply drafter.
            </p>
          </div>
          {onOpenNoticeLedger && (
            <button
              onClick={onOpenNoticeLedger}
              className="px-4 py-2 bg-white/10 hover:bg-white/20 text-white rounded-lg text-sm font-medium transition border border-white/20 whitespace-nowrap"
            >
              📋 Open Notice Ledger
            </button>
          )}
        </div>

        {/* Tab Navigation */}
        <div className="flex border-b border-indigo-500/30 mt-6 gap-2">
          <button
            onClick={() => setActiveTab('scanner')}
            className={`pb-3 px-4 font-medium text-sm border-b-2 transition flex items-center gap-2 ${
              activeTab === 'scanner'
                ? 'border-cyan-400 text-cyan-300'
                : 'border-transparent text-indigo-300 hover:text-white'
            }`}
          >
            ⚡ Notice Ingestion &amp; OCR Scanner
          </button>
          <button
            onClick={() => setActiveTab('precedents')}
            className={`pb-3 px-4 font-medium text-sm border-b-2 transition flex items-center gap-2 ${
              activeTab === 'precedents'
                ? 'border-cyan-400 text-cyan-300'
                : 'border-transparent text-indigo-300 hover:text-white'
            }`}
          >
            ⚖️ Statutory Citations &amp; Landmark Precedents ({precedents.length})
          </button>
          <button
            onClick={() => setActiveTab('drafter')}
            className={`pb-3 px-4 font-medium text-sm border-b-2 transition flex items-center gap-2 ${
              activeTab === 'drafter'
                ? 'border-cyan-400 text-cyan-300'
                : 'border-transparent text-indigo-300 hover:text-white'
            }`}
          >
            📝 1-Click Legal Reply Studio {activeDraft ? `(${activeDraft.draftNumber})` : ''}
          </button>
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div className="p-4 bg-rose-50 border border-rose-200 text-rose-800 rounded-lg flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span>⚠️</span>
            <span className="text-sm font-medium">{error}</span>
          </div>
          <button onClick={() => setError(null)} className="text-rose-600 hover:text-rose-900 font-bold">&times;</button>
        </div>
      )}
      {success && (
        <div className="p-4 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span>✅</span>
            <span className="text-sm font-medium">{success}</span>
          </div>
          <button onClick={() => setSuccess(null)} className="text-emerald-600 hover:text-emerald-900 font-bold">&times;</button>
        </div>
      )}

      {/* TAB 1: SCANNER */}
      {activeTab === 'scanner' && (
        <div className="space-y-6">
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
            {/* Left: Input & Presets */}
            <div className="lg:col-span-7 bg-white rounded-xl border border-slate-200 shadow-sm p-5 space-y-4">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2 border-b border-slate-100 pb-3">
                <h3 className="font-semibold text-slate-800 text-base flex items-center gap-2">
                  <span>📄</span> Ingest Notice Text or OCR Scan
                </h3>
                <div className="flex items-center gap-2">
                  <span className="text-xs text-slate-500 font-medium">Quick Presets:</span>
                  <select
                    onChange={e => {
                      const val = e.target.value as keyof typeof SAMPLE_NOTICES
                      if (val && SAMPLE_NOTICES[val]) {
                        setRawNoticeText(SAMPLE_NOTICES[val])
                      }
                    }}
                    className="text-xs border border-slate-300 rounded px-2 py-1 bg-slate-50 font-medium text-slate-700"
                  >
                    <option value="sec148a">Sec 148A(b) Escaped Income (₹75L)</option>
                    <option value="gstDrc01c">GST DRC-01C Rule 88D (ITC ₹8.45L)</option>
                    <option value="sec143_1">Sec 143(1) Intimation (TDS ₹1.48L)</option>
                    <option value="sec270a">Sec 270A Penalty (₹15L)</option>
                  </select>
                </div>
              </div>

              {/* Client Selector */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Target Client Assessee:
                </label>
                <select
                  value={selectedClientId}
                  onChange={e => setSelectedClientId(e.target.value)}
                  className="w-full text-sm border border-slate-300 rounded-lg px-3 py-2 bg-white text-slate-800"
                >
                  <option value="">-- Choose Client (Optional) --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>
                      {c.name} ({c.code}) - {c.taxIds ? Object.values(c.taxIds).join(' / ') : c.code}
                    </option>
                  ))}
                </select>
              </div>

              {/* Notice Textarea */}
              <div>
                <label className="block text-xs font-semibold text-slate-700 mb-1">
                  Notice Content / OCR Extracted Text:
                </label>
                <textarea
                  rows={14}
                  value={rawNoticeText}
                  onChange={e => setRawNoticeText(e.target.value)}
                  placeholder="Paste text from ITD portal PDF, GSTN notice, or mobile photo OCR scan..."
                  className="w-full font-mono text-xs border border-slate-300 rounded-lg p-3 bg-slate-50 text-slate-800 leading-relaxed focus:bg-white transition"
                />
              </div>

              <div className="flex justify-end gap-3 pt-2">
                <button
                  type="button"
                  onClick={() => setRawNoticeText('')}
                  className="px-4 py-2 text-xs font-medium text-slate-600 hover:text-slate-800 transition"
                >
                  Clear
                </button>
                <button
                  type="button"
                  onClick={handleAnalyzeNotice}
                  disabled={loading}
                  className="px-5 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-semibold shadow-sm transition flex items-center gap-2"
                >
                  {loading ? '⚡ Analyzing...' : '⚡ Analyze Notice with Copilot'}
                </button>
              </div>
            </div>

            {/* Right: Extraction Results */}
            <div className="lg:col-span-5 space-y-4">
              {parsedNotice ? (
                <div className="bg-white rounded-xl border border-indigo-100 shadow-md p-5 space-y-5">
                  <div className="flex items-center justify-between border-b border-slate-100 pb-3">
                    <div className="flex items-center gap-2">
                      <span className="w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
                      <h4 className="font-bold text-slate-900">Extracted Notice Telemetry</h4>
                    </div>
                    <span className="text-xs bg-indigo-50 text-indigo-700 font-semibold px-2.5 py-0.5 rounded-full border border-indigo-200">
                      {parsedNotice.confidenceScore}% Confidence
                    </span>
                  </div>

                  {/* Badges Matrix */}
                  <div className="grid grid-cols-2 gap-3">
                    <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                      <span className="text-[10px] uppercase font-bold text-slate-400 block">Authority</span>
                      <span className="font-semibold text-slate-800 text-sm">{parsedNotice.authority}</span>
                    </div>
                    <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                      <span className="text-[10px] uppercase font-bold text-slate-400 block">Section</span>
                      <span className="font-bold text-indigo-700 text-sm">{parsedNotice.section}</span>
                    </div>
                    <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                      <span className="text-[10px] uppercase font-bold text-slate-400 block">DIN / Notice Ref</span>
                      <div className="flex items-center gap-1.5 mt-0.5">
                        <span className="font-mono text-xs text-slate-800 truncate" title={parsedNotice.din || 'None'}>
                          {parsedNotice.din || 'Missing DIN'}
                        </span>
                        {parsedNotice.dinStatus === 'Valid' ? (
                          <span className="text-xs text-emerald-600 font-bold" title="Valid CBDT/CBIC DIN">✓</span>
                        ) : parsedNotice.dinStatus === 'Invalid' ? (
                          <span className="text-xs bg-rose-100 text-rose-700 px-1 rounded font-bold" title="Invalid DIN per CBDT Cir 19/2019">Void ab initio</span>
                        ) : null}
                      </div>
                    </div>
                    <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                      <span className="text-[10px] uppercase font-bold text-slate-400 block">AY / FY</span>
                      <span className="font-semibold text-slate-800 text-xs">
                        AY {parsedNotice.assessmentYear || 'N/A'} (FY {parsedNotice.financialYear || 'N/A'})
                      </span>
                    </div>
                    <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                      <span className="text-[10px] uppercase font-bold text-slate-400 block">Demand / Dispute</span>
                      <span className="font-bold text-rose-600 text-sm">
                        ₹{parsedNotice.demandAmount.toLocaleString('en-IN')}
                      </span>
                    </div>
                    <div className="bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                      <span className="text-[10px] uppercase font-bold text-slate-400 block">Due Date</span>
                      <span className="font-semibold text-slate-800 text-xs">
                        {parsedNotice.dueDate || 'N/A'}
                      </span>
                    </div>
                  </div>

                  {/* Summary */}
                  <div className="bg-amber-50/60 border border-amber-200/80 rounded-lg p-3 text-xs text-amber-900">
                    <span className="font-bold block mb-1">📌 Allegation Summary:</span>
                    <p>{parsedNotice.summaryOfAllegations}</p>
                  </div>

                  {/* Recommended Preliminary Objections */}
                  <div>
                    <h5 className="text-xs font-bold uppercase tracking-wider text-slate-700 mb-2 flex items-center gap-1.5">
                      <span>🛡️</span> Recommended Preliminary Objections:
                    </h5>
                    <div className="space-y-2 max-h-40 overflow-y-auto pr-1">
                      {parsedNotice.recommendedPreliminaryObjections.map((obj: string, i: number) => (
                        <label key={i} className="flex items-start gap-2 text-xs text-slate-700 bg-slate-50 p-2 rounded border border-slate-200 cursor-pointer hover:bg-slate-100 transition">
                          <input
                            type="checkbox"
                            checked={selectedObjections.includes(obj)}
                            onChange={e => {
                              if (e.target.checked) setSelectedObjections([...selectedObjections, obj])
                              else setSelectedObjections(selectedObjections.filter(x => x !== obj))
                            }}
                            className="mt-0.5 rounded text-indigo-600 focus:ring-indigo-500"
                          />
                          <span>{obj}</span>
                        </label>
                      ))}
                    </div>
                  </div>

                  {/* Recommended Grounds */}
                  <div>
                    <h5 className="text-xs font-bold uppercase tracking-wider text-slate-700 mb-2 flex items-center gap-1.5">
                      <span>⚖️</span> Rebuttal Grounds on Merits:
                    </h5>
                    <div className="space-y-2 max-h-40 overflow-y-auto pr-1">
                      {parsedNotice.recommendedGrounds.map((g: string, i: number) => (
                        <label key={i} className="flex items-start gap-2 text-xs text-slate-700 bg-slate-50 p-2 rounded border border-slate-200 cursor-pointer hover:bg-slate-100 transition">
                          <input
                            type="checkbox"
                            checked={selectedGrounds.includes(g)}
                            onChange={e => {
                              if (e.target.checked) setSelectedGrounds([...selectedGrounds, g])
                              else setSelectedGrounds(selectedGrounds.filter(x => x !== g))
                            }}
                            className="mt-0.5 rounded text-indigo-600 focus:ring-indigo-500"
                          />
                          <span>{g}</span>
                        </label>
                      ))}
                    </div>
                  </div>

                  {/* Action Button */}
                  <button
                    onClick={handleCreateNoticeAndLaunchDraft}
                    disabled={loading}
                    className="w-full py-3 bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-700 hover:to-teal-700 text-white font-bold rounded-lg shadow-md transition flex items-center justify-center gap-2 text-sm"
                  >
                    <span>🚀</span> Register Notice &amp; Launch Legal Reply Drafter
                  </button>
                </div>
              ) : (
                <div className="bg-slate-50 rounded-xl border border-dashed border-slate-300 p-8 text-center text-slate-500 space-y-3">
                  <span className="text-4xl block">🔍</span>
                  <h4 className="font-semibold text-slate-700">Awaiting Notice Ingestion</h4>
                  <p className="text-xs text-slate-500 max-w-xs mx-auto">
                    Paste raw text or choose one of the official notice presets on the left and click <strong>Analyze Notice with Copilot</strong> to trigger statutory heuristics.
                  </p>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: PRECEDENTS */}
      {activeTab === 'precedents' && (
        <div className="space-y-6">
          {/* Controls */}
          <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-sm flex flex-col sm:flex-row items-center justify-between gap-4">
            <div className="flex items-center gap-3 w-full sm:w-auto">
              <span className="text-xs font-semibold text-slate-600 uppercase">Statute:</span>
              <div className="flex bg-slate-100 p-1 rounded-lg">
                <button
                  onClick={() => {
                    setPrecedentAuthority('')
                    loadPrecedents('', precedentSearch)
                  }}
                  className={`px-3 py-1 rounded-md text-xs font-semibold transition ${
                    precedentAuthority === '' ? 'bg-white shadow text-slate-900' : 'text-slate-600 hover:text-slate-900'
                  }`}
                >
                  All ({precedents.length})
                </button>
                <button
                  onClick={() => {
                    setPrecedentAuthority('IncomeTax')
                    loadPrecedents('IncomeTax', precedentSearch)
                  }}
                  className={`px-3 py-1 rounded-md text-xs font-semibold transition ${
                    precedentAuthority === 'IncomeTax' ? 'bg-white shadow text-slate-900' : 'text-slate-600 hover:text-slate-900'
                  }`}
                >
                  Income Tax
                </button>
                <button
                  onClick={() => {
                    setPrecedentAuthority('Gst')
                    loadPrecedents('Gst', precedentSearch)
                  }}
                  className={`px-3 py-1 rounded-md text-xs font-semibold transition ${
                    precedentAuthority === 'Gst' ? 'bg-white shadow text-slate-900' : 'text-slate-600 hover:text-slate-900'
                  }`}
                >
                  GST
                </button>
              </div>
            </div>

            <div className="w-full sm:w-72">
              <input
                type="text"
                placeholder="Search case, citation, section, or keyword..."
                value={precedentSearch}
                onChange={e => {
                  setPrecedentSearch(e.target.value)
                  loadPrecedents(precedentAuthority, e.target.value)
                }}
                className="w-full text-xs border border-slate-300 rounded-lg px-3 py-2 bg-slate-50 focus:bg-white transition"
              />
            </div>
          </div>

          {/* Precedent Cards Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {precedents.map(p => (
              <div
                key={p.id}
                className="bg-white rounded-xl border border-slate-200 hover:border-indigo-300 shadow-sm hover:shadow transition p-5 space-y-3 flex flex-col justify-between"
              >
                <div>
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <span className={`text-[10px] font-bold uppercase tracking-wider px-2 py-0.5 rounded-full ${
                        p.authority === 'IncomeTax'
                          ? 'bg-blue-50 text-blue-700 border border-blue-200'
                          : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                      }`}>
                        {p.authority} &bull; {p.category}
                      </span>
                      <h4 className="font-bold text-slate-900 text-base mt-2 flex items-center gap-1.5">
                        {p.isLandmark && <span className="text-amber-500" title="Landmark Ruling">★</span>}
                        {p.caseTitle}
                      </h4>
                    </div>
                  </div>

                  <div className="mt-1 flex flex-wrap items-center gap-2 text-xs">
                    <span className="font-mono font-semibold text-indigo-700 bg-indigo-50 px-2 py-0.5 rounded border border-indigo-100">
                      {p.citation}
                    </span>
                    <span className="text-slate-500 font-medium">{p.courtBench} ({p.judgmentYear})</span>
                  </div>

                  <div className="mt-2 text-xs text-slate-600 bg-slate-50 p-2.5 rounded-lg border border-slate-100">
                    <span className="font-semibold text-slate-700 block mb-0.5">Key Principle / Ratio Decidendi:</span>
                    <p className="leading-relaxed">{p.keyRatioDecidendi}</p>
                  </div>

                  <div className="mt-2 text-xs text-slate-700 bg-amber-50/40 p-2.5 rounded-lg border border-amber-100/70">
                    <span className="font-semibold text-amber-900 block mb-0.5">Template Defense Paragraph:</span>
                    <p className="italic text-slate-600 leading-relaxed font-serif text-[11px] line-clamp-3">
                      "{p.defenseGroundTemplate}"
                    </p>
                  </div>
                </div>

                <div className="flex items-center justify-between pt-3 border-t border-slate-100 text-xs">
                  <span className="text-slate-400 font-medium">Ref: {p.applicableSections}</span>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => copyToClipboard(p.citation, `${p.id}-cit`)}
                      className="px-2.5 py-1 bg-slate-100 hover:bg-slate-200 text-slate-700 font-medium rounded transition"
                    >
                      {copiedId === `${p.id}-cit` ? '✓ Copied Citation' : '📋 Copy Citation'}
                    </button>
                    <button
                      onClick={() => copyToClipboard(p.defenseGroundTemplate, `${p.id}-tpl`)}
                      className="px-2.5 py-1 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 font-medium rounded transition"
                    >
                      {copiedId === `${p.id}-tpl` ? '✓ Copied Template' : '📋 Copy Defense'}
                    </button>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* TAB 3: DRAFTER */}
      {activeTab === 'drafter' && (
        <div className="space-y-6">
          {/* Draft selector bar */}
          <div className="bg-white rounded-xl border border-slate-200 p-4 shadow-sm flex flex-col md:flex-row items-center justify-between gap-4">
            <div className="flex items-center gap-3 w-full md:w-auto">
              <span className="text-xs font-semibold text-slate-600 whitespace-nowrap">Select Notice:</span>
              <select
                value={activeNoticeId}
                onChange={e => {
                  setActiveNoticeId(e.target.value)
                  loadDraftsForNotice(e.target.value)
                }}
                className="text-xs border border-slate-300 rounded-lg px-3 py-2 bg-slate-50 text-slate-800 font-medium max-w-md"
              >
                <option value="">-- Choose Registered Notice --</option>
                {notices.map(n => (
                  <option key={n.id} value={n.id}>
                    {n.noticeNumber} ({n.section} - {n.clientName})
                  </option>
                ))}
              </select>
              {noticeDrafts.length > 0 && (
                <select
                  value={activeDraft?.id || ''}
                  onChange={e => {
                    const d = noticeDrafts.find(x => x.id === e.target.value)
                    if (d) {
                      setActiveDraft(d)
                      populateDraftFields(d)
                    }
                  }}
                  className="text-xs border border-slate-300 rounded-lg px-2.5 py-2 bg-slate-50 text-slate-800 font-semibold"
                >
                  {noticeDrafts.map(d => (
                    <option key={d.id} value={d.id}>
                      {d.draftNumber} ({d.status})
                    </option>
                  ))}
                </select>
              )}
            </div>

            <div className="flex items-center gap-3 w-full md:w-auto justify-end">
              {activeDraft && (
                <div className="flex items-center gap-2">
                  <span className={`text-xs px-2.5 py-1 rounded-full font-bold uppercase tracking-wider ${
                    activeDraft.status === 'PartnerApproved'
                      ? 'bg-emerald-100 text-emerald-800 border border-emerald-300'
                      : 'bg-amber-100 text-amber-800 border border-amber-300'
                  }`}>
                    {activeDraft.status === 'PartnerApproved' ? '✓ Partner Approved' : 'Draft in Progress'}
                  </span>
                  {activeDraft.partnerUdin && (
                    <span className="font-mono text-xs bg-slate-900 text-emerald-400 px-2.5 py-1 rounded font-bold">
                      UDIN: {activeDraft.partnerUdin}
                    </span>
                  )}
                </div>
              )}
              {activeNoticeId && (
                <button
                  onClick={handleCreateNewDraftForNotice}
                  disabled={loading}
                  className="px-3 py-1.5 bg-slate-100 hover:bg-slate-200 text-slate-800 text-xs font-semibold rounded-lg transition"
                >
                  ➕ New Version
                </button>
              )}
            </div>
          </div>

          {activeDraft ? (
            <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
              {/* Draft Editor Column */}
              <div className="lg:col-span-8 bg-white rounded-xl border border-slate-200 shadow-sm p-6 space-y-6">
                <div className="flex items-center justify-between border-b border-slate-100 pb-4">
                  <div>
                    <h3 className="font-bold text-slate-900 text-lg">
                      Legal Defense Reply: {activeDraft.draftNumber}
                    </h3>
                    <p className="text-xs text-slate-500">
                      Prepared for {activeDraft.clientName} ({activeDraft.clientPanOrGstin}) &bull; Notice Ref: {activeDraft.noticeRefNumber}
                    </p>
                  </div>
                  <div className="flex items-center gap-2">
                    <button
                      onClick={handleSaveDraft}
                      disabled={loading}
                      className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold shadow-sm transition"
                    >
                      💾 Save Changes
                    </button>
                    {can?.('notices.approve') && activeDraft.status !== 'PartnerApproved' && (
                      <button
                        onClick={() => setShowApprovalModal(true)}
                        className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-semibold shadow-sm transition"
                      >
                        🏛️ Partner Approve &amp; UDIN
                      </button>
                    )}
                    <button
                      onClick={handleViewExportHtml}
                      className="px-4 py-2 bg-slate-900 hover:bg-slate-800 text-white rounded-lg text-xs font-semibold shadow-sm transition"
                    >
                      🖨️ View &amp; Print HTML
                    </button>
                  </div>
                </div>

                {/* Section A: Case & Addressee */}
                <div className="space-y-4">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700 border-b border-slate-100 pb-1">
                    A. Letterhead &amp; Assessing Authority
                  </h4>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <label className="block text-xs font-semibold text-slate-700 mb-1">Submission Title:</label>
                      <input
                        type="text"
                        value={draftTitle}
                        onChange={e => setDraftTitle(e.target.value)}
                        className="w-full text-xs border border-slate-300 rounded px-3 py-2 text-slate-800"
                      />
                    </div>
                    <div>
                      <label className="block text-xs font-semibold text-slate-700 mb-1">Addressed To (Proper Officer / NFAC):</label>
                      <input
                        type="text"
                        value={draftAddressedTo}
                        onChange={e => setDraftAddressedTo(e.target.value)}
                        className="w-full text-xs border border-slate-300 rounded px-3 py-2 text-slate-800"
                      />
                    </div>
                  </div>
                  <div>
                    <label className="block text-xs font-semibold text-slate-700 mb-1">Formal In Re / Subject Line:</label>
                    <textarea
                      rows={2}
                      value={draftSubject}
                      onChange={e => setDraftSubject(e.target.value)}
                      className="w-full text-xs border border-slate-300 rounded px-3 py-2 text-slate-800 font-mono"
                    />
                  </div>
                </div>

                {/* Section B: Preliminary Objections */}
                <div className="space-y-3">
                  <div className="flex items-center justify-between border-b border-slate-100 pb-1">
                    <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700">
                      B. Preliminary Objections (Jurisdiction, Limitation, Natural Justice)
                    </h4>
                    <button
                      onClick={() => setDraftObjections([...draftObjections, 'New Preliminary Objection...'])}
                      className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold"
                    >
                      ➕ Add Objection
                    </button>
                  </div>
                  <div className="space-y-2">
                    {draftObjections.map((obj, idx) => (
                      <div key={idx} className="flex items-start gap-2">
                        <span className="font-bold text-xs text-slate-400 mt-2">{idx + 1}.</span>
                        <textarea
                          rows={2}
                          value={obj}
                          onChange={e => {
                            const copy = [...draftObjections]
                            copy[idx] = e.target.value
                            setDraftObjections(copy)
                          }}
                          className="w-full text-xs border border-slate-300 rounded p-2 text-slate-800 leading-relaxed"
                        />
                        <button
                          onClick={() => setDraftObjections(draftObjections.filter((_, i) => i !== idx))}
                          className="text-rose-500 hover:text-rose-700 p-1 text-xs font-bold mt-1"
                        >
                          ✕
                        </button>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Section C: Factual Matrix */}
                <div className="space-y-3">
                  <div className="flex items-center justify-between border-b border-slate-100 pb-1">
                    <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700">
                      C. Factual Chronology Matrix
                    </h4>
                    <button
                      onClick={() => setDraftFacts([...draftFacts, {
                        itemNo: draftFacts.length + 1,
                        eventDate: new Date().toLocaleDateString('en-GB'),
                        description: 'Factual event description...',
                        annexureRef: `Annexure A-${draftFacts.length + 1}`
                      }])}
                      className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold"
                    >
                      ➕ Add Factual Point
                    </button>
                  </div>
                  <div className="overflow-x-auto">
                    <table className="w-full text-xs text-left border border-slate-200 rounded">
                      <thead className="bg-slate-100 text-slate-700 font-semibold">
                        <tr>
                          <th className="p-2 w-12">#</th>
                          <th className="p-2 w-28">Event Date</th>
                          <th className="p-2">Description of Fact / Transaction</th>
                          <th className="p-2 w-40">Annexure Ref</th>
                          <th className="p-2 w-10"></th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-200">
                        {draftFacts.map((f, idx) => (
                          <tr key={idx}>
                            <td className="p-2 font-bold text-slate-400">{idx + 1}</td>
                            <td className="p-2">
                              <input
                                type="text"
                                value={f.eventDate}
                                onChange={e => {
                                  const copy = [...draftFacts]
                                  copy[idx].eventDate = e.target.value
                                  setDraftFacts(copy)
                                }}
                                className="w-full text-xs border border-slate-300 rounded px-1.5 py-1"
                              />
                            </td>
                            <td className="p-2">
                              <input
                                type="text"
                                value={f.description}
                                onChange={e => {
                                  const copy = [...draftFacts]
                                  copy[idx].description = e.target.value
                                  setDraftFacts(copy)
                                }}
                                className="w-full text-xs border border-slate-300 rounded px-1.5 py-1"
                              />
                            </td>
                            <td className="p-2">
                              <input
                                type="text"
                                value={f.annexureRef}
                                onChange={e => {
                                  const copy = [...draftFacts]
                                  copy[idx].annexureRef = e.target.value
                                  setDraftFacts(copy)
                                }}
                                className="w-full text-xs border border-slate-300 rounded px-1.5 py-1 font-mono text-[11px]"
                              />
                            </td>
                            <td className="p-2 text-center">
                              <button
                                onClick={() => setDraftFacts(draftFacts.filter((_, i) => i !== idx))}
                                className="text-rose-500 font-bold hover:text-rose-700"
                              >
                                ✕
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>

                {/* Section D: Submissions on Merits */}
                <div className="space-y-3">
                  <div className="flex items-center justify-between border-b border-slate-100 pb-1">
                    <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700">
                      D. Ground-by-Ground Legal Rebuttal on Merits
                    </h4>
                    <button
                      onClick={() => setDraftGrounds([...draftGrounds, {
                        groundNo: draftGrounds.length + 1,
                        title: 'New Substantive Ground',
                        argument: 'Detailed submission on why proposed addition is untenable...',
                        relevantSection: activeDraft.noticeRefNumber
                      }])}
                      className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold"
                    >
                      ➕ Add Ground
                    </button>
                  </div>
                  <div className="space-y-4">
                    {draftGrounds.map((g, idx) => (
                      <div key={idx} className="bg-slate-50 p-3 rounded-lg border border-slate-200 space-y-2">
                        <div className="flex items-center justify-between gap-2">
                          <span className="font-bold text-xs text-indigo-900">Ground {idx + 1}:</span>
                          <input
                            type="text"
                            value={g.title}
                            placeholder="Ground Title"
                            onChange={e => {
                              const copy = [...draftGrounds]
                              copy[idx].title = e.target.value
                              setDraftGrounds(copy)
                            }}
                            className="flex-1 text-xs font-semibold border border-slate-300 rounded px-2 py-1 bg-white"
                          />
                          <input
                            type="text"
                            value={g.relevantSection}
                            placeholder="Sec Ref"
                            onChange={e => {
                              const copy = [...draftGrounds]
                              copy[idx].relevantSection = e.target.value
                              setDraftGrounds(copy)
                            }}
                            className="w-28 text-xs font-mono border border-slate-300 rounded px-2 py-1 bg-white"
                          />
                          <button
                            onClick={() => setDraftGrounds(draftGrounds.filter((_, i) => i !== idx))}
                            className="text-rose-500 font-bold hover:text-rose-700 p-1"
                          >
                            ✕
                          </button>
                        </div>
                        <textarea
                          rows={3}
                          value={g.argument}
                          onChange={e => {
                            const copy = [...draftGrounds]
                            copy[idx].argument = e.target.value
                            setDraftGrounds(copy)
                          }}
                          className="w-full text-xs border border-slate-300 rounded p-2 text-slate-800 bg-white leading-relaxed"
                          placeholder="Detailed statutory arguments..."
                        />
                      </div>
                    ))}
                  </div>
                </div>

                {/* Section E: Formal Prayer */}
                <div className="space-y-2">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-slate-700 border-b border-slate-100 pb-1">
                    E. Prayer for Relief
                  </h4>
                  <textarea
                    rows={4}
                    value={draftPrayer}
                    onChange={e => setDraftPrayer(e.target.value)}
                    className="w-full text-xs border border-slate-300 rounded-lg p-3 text-slate-800 bg-slate-50 leading-relaxed font-serif text-[13px]"
                  />
                </div>
              </div>

              {/* Sidebar Precedents & Annexures Column */}
              <div className="lg:col-span-4 space-y-5">
                {/* Precedents in this draft */}
                <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-4 space-y-3">
                  <h4 className="text-xs font-bold uppercase tracking-wider text-slate-800 flex items-center justify-between border-b border-slate-100 pb-2">
                    <span>⚖️ Cited Precedents ({draftPrecedents.length})</span>
                  </h4>
                  <div className="space-y-2.5 max-h-72 overflow-y-auto pr-1">
                    {draftPrecedents.map((cp, idx) => (
                      <div key={idx} className="bg-slate-50 p-2.5 rounded border border-slate-200 text-xs space-y-1">
                        <div className="flex items-start justify-between">
                          <span className="font-bold text-slate-800">{cp.caseTitle}</span>
                          <button
                            onClick={() => setDraftPrecedents(draftPrecedents.filter((_, i) => i !== idx))}
                            className="text-rose-500 text-[10px] font-bold"
                          >
                            Remove
                          </button>
                        </div>
                        <span className="font-mono text-[11px] text-indigo-700 font-semibold block">{cp.citation}</span>
                        <p className="text-[11px] text-slate-600 line-clamp-2">{cp.relevantHolding}</p>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Annexures Index */}
                <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-4 space-y-3">
                  <div className="flex items-center justify-between border-b border-slate-100 pb-2">
                    <h4 className="text-xs font-bold uppercase tracking-wider text-slate-800">
                      📎 Annexures Index ({draftAnnexures.length})
                    </h4>
                    <button
                      onClick={() => setDraftAnnexures([...draftAnnexures, {
                        annexureNo: `A-${draftAnnexures.length + 1}`,
                        title: 'Supporting Document',
                        documentType: 'Ledger / Certificate',
                        pageCount: 2
                      }])}
                      className="text-xs text-indigo-600 font-semibold"
                    >
                      ➕ Add
                    </button>
                  </div>
                  <div className="space-y-2">
                    {draftAnnexures.map((ann, idx) => (
                      <div key={idx} className="flex items-center gap-1.5 text-xs bg-slate-50 p-2 rounded border border-slate-200">
                        <span className="font-mono font-bold text-slate-600 w-10">{ann.annexureNo}</span>
                        <input
                          type="text"
                          value={ann.title}
                          onChange={e => {
                            const copy = [...draftAnnexures]
                            copy[idx].title = e.target.value
                            setDraftAnnexures(copy)
                          }}
                          className="flex-1 text-[11px] border border-slate-300 rounded px-1.5 py-0.5"
                        />
                        <span className="text-slate-400 text-[10px]">{ann.pageCount} pgs</span>
                        <button
                          onClick={() => setDraftAnnexures(draftAnnexures.filter((_, i) => i !== idx))}
                          className="text-rose-500 font-bold text-xs"
                        >
                          ✕
                        </button>
                      </div>
                    ))}
                  </div>
                </div>

                {/* Notice Details Card */}
                <div className="bg-indigo-50/50 rounded-xl border border-indigo-100 p-4 text-xs space-y-2 text-indigo-900">
                  <span className="font-bold text-slate-900 block">📌 Linked Notice Reference</span>
                  <div><strong>Notice Ref:</strong> {activeDraft.noticeRefNumber}</div>
                  <div><strong>Notice DIN:</strong> {activeDraft.noticeDin}</div>
                  <div><strong>Issue Date:</strong> {activeDraft.noticeIssueDate}</div>
                  <div><strong>Statutory Due Date:</strong> {activeDraft.noticeDueDate}</div>
                </div>
              </div>
            </div>
          ) : (
            <div className="bg-slate-50 rounded-xl border border-dashed border-slate-300 p-12 text-center text-slate-500 space-y-3">
              <span className="text-4xl block">📝</span>
              <h4 className="font-semibold text-slate-700">No Active Defense Draft Selected</h4>
              <p className="text-xs text-slate-500 max-w-sm mx-auto">
                Select an existing registered notice from the dropdown above, or use the <strong>Notice Ingestion Scanner</strong> tab to ingest a notice and launch the drafter.
              </p>
            </div>
          )}
        </div>
      )}

      {/* MODAL 1: PARTNER APPROVAL & UDIN */}
      {showApprovalModal && activeDraft && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-2xl border border-slate-200 max-w-md w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-slate-100 pb-3">
              <h3 className="font-bold text-slate-900 text-base flex items-center gap-2">
                <span>🏛️</span> Partner Quality Review &amp; UDIN Attestation
              </h3>
              <button onClick={() => setShowApprovalModal(false)} className="text-slate-400 hover:text-slate-600 font-bold">&times;</button>
            </div>

            <p className="text-xs text-slate-600">
              As a Managing Partner, approve the formal legal submission for <strong>{activeDraft.clientName}</strong>. You may optionally bind an 18-digit ICAI UDIN for statutory certification.
            </p>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                18-Character ICAI UDIN (Optional):
              </label>
              <input
                type="text"
                placeholder="e.g. 24104523ABCD123456"
                value={partnerUdin}
                onChange={e => setPartnerUdin(e.target.value.toUpperCase())}
                maxLength={18}
                className="w-full font-mono text-sm border border-slate-300 rounded-lg px-3 py-2 text-slate-800"
              />
              <span className="text-[10px] text-slate-400 mt-1 block">Format: 2-digit FY + 6-digit M.No + 10-digit doc code</span>
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-700 mb-1">
                Partner Review Notes:
              </label>
              <textarea
                rows={3}
                placeholder="Verified against original books of account and relevant judicial precedents..."
                value={partnerNotes}
                onChange={e => setPartnerNotes(e.target.value)}
                className="w-full text-xs border border-slate-300 rounded-lg p-2 text-slate-800"
              />
            </div>

            <div className="flex justify-end gap-3 pt-3 border-t border-slate-100">
              <button
                type="button"
                onClick={() => setShowApprovalModal(false)}
                className="px-4 py-2 text-xs font-medium text-slate-600 hover:text-slate-800"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handlePartnerApprove}
                disabled={loading}
                className="px-5 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-bold transition shadow-sm"
              >
                {loading ? 'Attesting...' : 'Confirm & Authorize Draft'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: PRINT-READY HTML PREVIEW */}
      {showPreviewModal && (
        <div className="fixed inset-0 bg-slate-900/70 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-2xl border border-slate-200 max-w-4xl w-full h-[90vh] flex flex-col overflow-hidden">
            <div className="flex items-center justify-between p-4 border-b border-slate-200 bg-slate-50">
              <div className="flex items-center gap-2">
                <span className="text-lg">🖨️</span>
                <h3 className="font-bold text-slate-900 text-sm">Standalone Printable Written Submission</h3>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => {
                    const printWindow = window.open('', '_blank')
                    if (printWindow) {
                      printWindow.document.write(previewHtml)
                      printWindow.document.close()
                      printWindow.print()
                    }
                  }}
                  className="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded text-xs font-semibold"
                >
                  Print / Save to PDF
                </button>
                <button
                  onClick={() => setShowPreviewModal(false)}
                  className="px-3 py-1.5 bg-slate-200 hover:bg-slate-300 text-slate-800 rounded text-xs font-semibold"
                >
                  Close
                </button>
              </div>
            </div>

            <div className="flex-1 overflow-auto p-4 bg-slate-100">
              <iframe
                srcDoc={previewHtml}
                title="Legal Submission Preview"
                className="w-full h-full bg-white shadow-md rounded border border-slate-300"
              />
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
