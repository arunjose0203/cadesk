import { useState, useEffect, useMemo, type FormEvent } from 'react'
import {
  foreignRemittanceApi,
  api,
  type ForeignRemittanceAssessmentDto,
  type ForeignRemittanceSummaryDto,
  type CreateForeignRemittanceRequest,
  type Client
} from './api'

const STANDARD_CURRENCIES = ['USD', 'EUR', 'GBP', 'SGD', 'AED', 'JPY', 'AUD', 'CAD', 'CHF', 'INR']

const RBI_PURPOSE_CODES = [
  { code: 'P0802', desc: 'Software consultancy / implementation / data processing', exempt: false },
  { code: 'S1007', desc: 'Royalties / license fees for use of intellectual property / patents', exempt: false },
  { code: 'S1006', desc: 'Fees for technical services / architectural / engineering', exempt: false },
  { code: 'S1107', desc: 'Management consultancy, accounting, auditing, public relations', exempt: false },
  { code: 'S1009', desc: 'Information services / news agency / database rights', exempt: false },
  { code: 'P0103', desc: 'Advance payment against import of goods (Commercial)', exempt: true },
  { code: 'S0001', desc: 'Business travel / conference participation', exempt: true },
  { code: 'S0005', desc: 'Education abroad / tuition / university fees', exempt: true },
  { code: 'S0011', desc: 'Medical treatment abroad', exempt: true },
  { code: 'S1301', desc: 'Remittance by non-resident Indians (Family maintenance)', exempt: true }
]

type ClientPage = {
  items: Client[]
  total: number
}

export function ForeignRemittancePage() {
  const [assessments, setAssessments] = useState<ForeignRemittanceAssessmentDto[]>([])
  const [summary, setSummary] = useState<ForeignRemittanceSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(true)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  // Filters
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')
  const [selectedStatus, setSelectedStatus] = useState<string>('')
  const [searchQuery, setSearchQuery] = useState('')

  // Selected item for details drawer
  const [activeItem, setActiveItem] = useState<ForeignRemittanceAssessmentDto | null>(null)
  const [activeTab, setActiveTab] = useState<'overview' | 'checklist' | 'form15cb' | 'form15ca'>('overview')

  // Modals
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showCertifyModal, setShowCertifyModal] = useState(false)
  const [showFileModal, setShowFileModal] = useState(false)

  // Form states
  const [createReq, setCreateReq] = useState<Partial<CreateForeignRemittanceRequest>>({
    financialYear: '2024-25',
    remittanceCurrency: 'USD',
    exchangeRate: 85.0,
    rbiPurposeCode: 'P0802',
    purposeDescription: 'Software consultancy / implementation',
    natureOfRemittance: 'FeesForTechnicalServices',
    isChargeableToTax: true,
    domesticSection: 'Section 195 read with Section 115A',
    domesticTaxRate: 20.0,
    surchargeRate: 0.0,
    cessRate: 4.0,
    treatyBenefitClaimed: true,
    treatyCountry: 'United States',
    treatyArticle: 'Article 12 - Royalties and Fees for Included Services',
    treatyTaxRate: 15.0,
    hasTrc: true,
    hasForm10F: true,
    noPeDeclaration: true,
    beneficialOwnershipConfirmed: true,
    mliPptPassed: true,
    taxBorneByRemitter: false
  })

  const [certifyForm, setCertifyForm] = useState({
    caName: 'CA Rajesh V. Sharma',
    caMembershipNumber: '214589',
    caFirmName: 'Sharma & Associates LLP',
    caFrn: '123456N',
    udin: '24214589BK99887766'
  })

  const [portalAck, setPortalAck] = useState('')

  // Load data
  const fetchData = async () => {
    try {
      setLoading(true)
      setErrorMsg(null)
      const [sum, list, cltPage] = await Promise.all([
        foreignRemittanceApi.getSummary(selectedFy),
        foreignRemittanceApi.list(undefined, selectedFy, selectedStatus || undefined),
        api<ClientPage>('/api/clients?pageSize=100').catch(() => ({ items: [], total: 0 }))
      ])
      setSummary(sum)
      setAssessments(list)
      setClients(cltPage.items || [])

      if (activeItem) {
        const fresh = list.find((x: ForeignRemittanceAssessmentDto) => x.id === activeItem.id)
        if (fresh) setActiveItem(fresh)
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to load foreign remittance records.'
      setErrorMsg(msg)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchData()
  }, [selectedFy, selectedStatus])

  // Filtered assessments
  const filteredAssessments = useMemo(() => {
    return assessments.filter((a) => {
      const q = searchQuery.toLowerCase()
      const matchSearch =
        !q ||
        a.remittanceReference.toLowerCase().includes(q) ||
        a.beneficiaryName.toLowerCase().includes(q) ||
        a.beneficiaryCountry.toLowerCase().includes(q) ||
        a.remitterName.toLowerCase().includes(q) ||
        a.remitterPan.toLowerCase().includes(q)
      return matchSearch
    })
  }, [assessments, searchQuery])

  // Client change in Create Form
  const handleClientSelect = (clientId: string) => {
    const c = clients.find((x) => x.id === clientId)
    if (!c) return
    setCreateReq((prev) => ({
      ...prev,
      clientId: c.id,
      remitterName: c.name,
      remitterPan: c.taxIds?.['PAN'] || '',
      remitterTan: c.taxIds?.['TAN'] || '',
      remitterStatus: c.entityType || 'Company',
      remitterEmail: c.email || '',
      remitterPhone: c.phone || ''
    }))
  }

  // Handle Create
  const handleCreateSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!createReq.clientId || !createReq.remitterPan || !createReq.beneficiaryName || !createReq.foreignAmount) {
      alert('Please fill all mandatory fields: Client, Remitter PAN, Beneficiary Name, and Foreign Amount.')
      return
    }

    try {
      setLoading(true)
      const created = await foreignRemittanceApi.create(createReq as CreateForeignRemittanceRequest)
      setShowCreateModal(false)
      await fetchData()
      setActiveItem(created)
      setActiveTab('overview')
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to create foreign remittance assessment.'
      alert(msg)
    } finally {
      setLoading(false)
    }
  }

  // Handle Certification
  const handleCertify = async (e: FormEvent) => {
    e.preventDefault()
    if (!activeItem) return

    if (certifyForm.udin.trim().length !== 18) {
      alert('Statutory UDIN must be exactly 18 alphanumeric characters in accordance with ICAI standards.')
      return
    }

    try {
      setLoading(true)
      const updated = await foreignRemittanceApi.certify(activeItem.id, certifyForm)
      setActiveItem(updated)
      setShowCertifyModal(false)
      await fetchData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Certification failed. Ensure all mandatory checklist items are verified.'
      alert(msg)
    } finally {
      setLoading(false)
    }
  }

  // Handle Filing
  const handleFile = async (e: FormEvent) => {
    e.preventDefault()
    if (!activeItem || !portalAck.trim()) return

    try {
      setLoading(true)
      const updated = await foreignRemittanceApi.file(activeItem.id, { acknowledgementNumber: portalAck.trim() })
      setActiveItem(updated)
      setShowFileModal(false)
      setPortalAck('')
      await fetchData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Recording filing failed.'
      alert(msg)
    } finally {
      setLoading(false)
    }
  }

  // Checklist item status update
  const handleChecklistStatusChange = async (itemId: string, newStatus: string) => {
    if (!activeItem) return
    try {
      await foreignRemittanceApi.updateChecklist(activeItem.id, itemId, { status: newStatus })
      const refreshed = await foreignRemittanceApi.get(activeItem.id)
      setActiveItem(refreshed)
      setAssessments((prev) => prev.map((x) => (x.id === refreshed.id ? refreshed : x)))
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to update checklist item.'
      alert(msg)
    }
  }

  // Copy text helper
  const copyToClipboard = (text: string, label: string) => {
    navigator.clipboard.writeText(text)
    alert(`${label} copied to clipboard!`)
  }

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-gray-200 pb-5">
        <div>
          <div className="flex items-center gap-3">
            <div className="p-2.5 bg-indigo-50 border border-indigo-200 rounded-lg text-indigo-700">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10"></circle>
                <line x1="2" y1="12" x2="22" y2="12"></line>
                <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
              </svg>
            </div>
            <div>
              <h1 className="text-2xl font-bold text-gray-900 tracking-tight">
                Foreign Remittance & Form 15CA / 15CB Desk
              </h1>
              <p className="text-sm text-gray-600">
                Income Tax Rule 37BB • Section 195 Withholding • DTAA Treaty Relief • Form 15CB CA Certification with UDIN
              </p>
            </div>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <select
            value={selectedFy}
            onChange={(e) => setSelectedFy(e.target.value)}
            className="px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white font-medium text-gray-700 shadow-sm focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="2024-25">FY 2024-25</option>
            <option value="2023-24">FY 2023-24</option>
            <option value="2022-23">FY 2022-23</option>
          </select>
          <button
            onClick={() => setShowCreateModal(true)}
            className="inline-flex items-center gap-2 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white text-sm font-semibold rounded-lg shadow transition-colors"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="12" y1="5" x2="12" y2="19"></line>
              <line x1="5" y1="12" x2="19" y2="12"></line>
            </svg>
            New Foreign Remittance
          </button>
        </div>
      </div>

      {errorMsg && (
        <div className="p-4 bg-red-50 border border-red-200 rounded-lg text-sm text-red-700">
          {errorMsg}
        </div>
      )}

      {/* KPI Cards */}
      {summary && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Total Remittance</p>
              <h3 className="text-2xl font-bold text-gray-900 mt-1">₹{summary.totalRemittanceInr.toLocaleString('en-IN')}</h3>
              <p className="text-xs text-gray-500 mt-1">{summary.totalAssessments} foreign transactions</p>
            </div>
            <div className="p-3 bg-blue-50 text-blue-600 rounded-lg">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="12" y1="1" x2="12" y2="23"></line>
                <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
              </svg>
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">TDS Withheld (Sec 195)</p>
              <h3 className="text-2xl font-bold text-emerald-700 mt-1">₹{summary.totalTdsInr.toLocaleString('en-IN')}</h3>
              <p className="text-xs text-emerald-600 mt-1">Effective treaty rates applied</p>
            </div>
            <div className="p-3 bg-emerald-50 text-emerald-600 rounded-lg">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
              </svg>
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Form 15CB Required</p>
              <h3 className="text-2xl font-bold text-indigo-700 mt-1">{summary.partCCount}</h3>
              <p className="text-xs text-gray-500 mt-1">Part C (&gt; ₹5 Lakhs) CA signed</p>
            </div>
            <div className="p-3 bg-indigo-50 text-indigo-600 rounded-lg">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="8" r="7"></circle>
                <polyline points="8.21 13.89 7 23 12 20 17 23 15.79 13.88"></polyline>
              </svg>
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Portal Status</p>
              <div className="flex items-center gap-2 mt-1">
                <span className="text-lg font-bold text-emerald-600">{summary.filedCount} Filed</span>
                <span className="text-gray-300">/</span>
                <span className="text-lg font-bold text-blue-600">{summary.certifiedCount} Certified</span>
              </div>
              <p className="text-xs text-gray-500 mt-1">{summary.draftCount} in assessment/draft</p>
            </div>
            <div className="p-3 bg-amber-50 text-amber-600 rounded-lg">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="22" y1="2" x2="11" y2="13"></line>
                <polygon points="22 2 15 22 11 13 2 9 22 2"></polygon>
              </svg>
            </div>
          </div>
        </div>
      )}

      {/* Main Content Area */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
        {/* Left Column: List of Remittances */}
        <div className={`${activeItem ? 'lg:col-span-5' : 'lg:col-span-12'} space-y-4`}>
          {/* Filters Bar */}
          <div className="flex flex-col sm:flex-row gap-3 items-center justify-between bg-white p-3.5 rounded-lg border border-gray-200 shadow-sm">
            <div className="relative w-full sm:w-72">
              <input
                type="text"
                placeholder="Search by ref, beneficiary, remitter..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="w-full px-3 py-1.5 border border-gray-300 rounded-md text-sm focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            <div className="flex items-center gap-2 w-full sm:w-auto">
              <select
                value={selectedStatus}
                onChange={(e) => setSelectedStatus(e.target.value)}
                className="px-2.5 py-1.5 border border-gray-300 rounded-md text-xs font-medium text-gray-700 bg-white"
              >
                <option value="">All Statuses</option>
                <option value="Draft">Draft</option>
                <option value="Assessed">Assessed</option>
                <option value="Certified">Certified</option>
                <option value="Filed">Filed</option>
              </select>
              <button
                onClick={fetchData}
                className="p-1.5 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-md transition-colors"
                title="Refresh"
              >
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <polyline points="23 4 23 10 17 10"></polyline>
                  <polyline points="1 20 1 14 7 14"></polyline>
                  <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path>
                </svg>
              </button>
            </div>
          </div>

          {/* Table / List */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            {loading && assessments.length === 0 ? (
              <div className="p-8 text-center text-sm text-gray-500">Loading remittance records...</div>
            ) : filteredAssessments.length === 0 ? (
              <div className="p-8 text-center space-y-2">
                <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1" strokeLinecap="round" strokeLinejoin="round" className="text-gray-300 mx-auto">
                  <circle cx="12" cy="12" r="10"></circle>
                  <line x1="2" y1="12" x2="22" y2="12"></line>
                  <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
                </svg>
                <p className="text-gray-600 font-medium text-sm">No foreign remittance assessments found.</p>
                <p className="text-xs text-gray-400">Click "New Foreign Remittance" to begin Section 195 & DTAA evaluation.</p>
              </div>
            ) : (
              <div className="divide-y divide-gray-100 max-h-[700px] overflow-y-auto">
                {filteredAssessments.map((a) => {
                  const isSelected = activeItem?.id === a.id
                  return (
                    <div
                      key={a.id}
                      onClick={() => setActiveItem(a)}
                      className={`p-4 transition-all cursor-pointer hover:bg-indigo-50/40 ${
                        isSelected ? 'bg-indigo-50/80 border-l-4 border-indigo-600' : ''
                      }`}
                    >
                      <div className="flex items-start justify-between gap-2">
                        <div>
                          <div className="flex items-center gap-2">
                            <span className="font-semibold text-gray-900 text-sm">{a.remittanceReference}</span>
                            <span
                              className={`text-[11px] font-medium px-2 py-0.5 rounded-full ${
                                a.form15CaPart === 'PartC'
                                  ? 'bg-purple-100 text-purple-700 border border-purple-200'
                                  : a.form15CaPart === 'PartB'
                                  ? 'bg-amber-100 text-amber-700 border border-amber-200'
                                  : a.form15CaPart === 'PartA'
                                  ? 'bg-blue-100 text-blue-700 border border-blue-200'
                                  : a.form15CaPart === 'PartD'
                                  ? 'bg-gray-100 text-gray-700 border border-gray-200'
                                  : 'bg-emerald-100 text-emerald-700 border border-emerald-200'
                              }`}
                            >
                              15CA {a.form15CaPart}
                            </span>
                            {a.requiresForm15Cb && (
                              <span className="text-[10px] font-bold px-1.5 py-0.5 bg-indigo-100 text-indigo-700 rounded border border-indigo-200">
                                15CB
                              </span>
                            )}
                          </div>
                          <p className="text-xs font-medium text-gray-700 mt-1">
                            {a.beneficiaryName} <span className="text-gray-400">({a.beneficiaryCountry})</span>
                          </p>
                          <p className="text-[11px] text-gray-500">
                            Remitter: {a.remitterName} • PAN: {a.remitterPan}
                          </p>
                        </div>

                        <div className="text-right">
                          <p className="text-sm font-bold text-gray-900">
                            {a.foreignAmount.toLocaleString()} {a.remittanceCurrency}
                          </p>
                          <p className="text-xs text-gray-500 font-medium">₹{a.inrEquivalent.toLocaleString('en-IN')}</p>
                          <span
                            className={`inline-block mt-1 text-[10px] font-semibold px-2 py-0.5 rounded-full ${
                              a.status === 'Filed'
                                ? 'bg-emerald-100 text-emerald-800'
                                : a.status === 'Certified'
                                ? 'bg-blue-100 text-blue-800'
                                : 'bg-gray-100 text-gray-700'
                            }`}
                          >
                            {a.status}
                          </span>
                        </div>
                      </div>

                      <div className="mt-2.5 pt-2 border-t border-gray-100 flex items-center justify-between text-xs text-gray-500">
                        <div className="flex items-center gap-1.5">
                          <span className="font-medium text-gray-600">Withholding:</span>
                          <span className="font-semibold text-indigo-700">{a.finalWithholdingRate}%</span>
                          <span className="text-[11px] text-gray-400">({a.applicableRateType})</span>
                        </div>
                        <div className="flex items-center gap-1 font-medium text-emerald-700">
                          TDS: ₹{a.tdsAmountInr.toLocaleString('en-IN')}
                        </div>
                      </div>
                    </div>
                  )
                })}
              </div>
            )}
          </div>
        </div>

        {/* Right Column: Assessment Detail Drawer / Workbench */}
        {activeItem && (
          <div className="lg:col-span-7 bg-white rounded-xl border border-gray-200 shadow-sm flex flex-col overflow-hidden">
            {/* Header of Drawer */}
            <div className="p-5 border-b border-gray-200 bg-gray-50/50 flex flex-col md:flex-row md:items-center justify-between gap-3">
              <div>
                <div className="flex items-center gap-2.5">
                  <h2 className="text-xl font-bold text-gray-900">{activeItem.remittanceReference}</h2>
                  <span
                    className={`text-xs font-semibold px-2.5 py-0.5 rounded-full ${
                      activeItem.status === 'Filed'
                        ? 'bg-emerald-100 text-emerald-800 border border-emerald-300'
                        : activeItem.status === 'Certified'
                        ? 'bg-blue-100 text-blue-800 border border-blue-300'
                        : 'bg-amber-100 text-amber-800 border border-amber-300'
                    }`}
                  >
                    {activeItem.status}
                  </span>
                  {activeItem.acknowledgementNumber && (
                    <span className="text-xs bg-gray-100 text-gray-700 px-2 py-0.5 rounded border border-gray-200 font-mono">
                      Ack: {activeItem.acknowledgementNumber}
                    </span>
                  )}
                </div>
                <p className="text-xs text-gray-600 mt-1">
                  Beneficiary: <strong className="text-gray-900">{activeItem.beneficiaryName}</strong> ({activeItem.beneficiaryCountry}) • Purpose: {activeItem.rbiPurposeCode}
                </p>
              </div>

              {/* Action Buttons */}
              <div className="flex items-center gap-2">
                {activeItem.requiresForm15Cb && activeItem.status !== 'Certified' && activeItem.status !== 'Filed' && (
                  <button
                    onClick={() => setShowCertifyModal(true)}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-semibold shadow-sm transition-colors"
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <circle cx="12" cy="8" r="7"></circle>
                      <polyline points="8.21 13.89 7 23 12 20 17 23 15.79 13.88"></polyline>
                    </svg>
                    Certify 15CB (UDIN)
                  </button>
                )}
                {activeItem.status === 'Certified' && (
                  <button
                    onClick={() => setShowFileModal(true)}
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-semibold shadow-sm transition-colors"
                  >
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                      <line x1="22" y1="2" x2="11" y2="13"></line>
                      <polygon points="22 2 15 22 11 13 2 9 22 2"></polygon>
                    </svg>
                    Record Portal Filing
                  </button>
                )}
                <button
                  onClick={() => setActiveItem(null)}
                  className="p-1.5 text-gray-400 hover:text-gray-600 hover:bg-gray-100 rounded-md"
                >
                  <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <line x1="18" y1="6" x2="6" y2="18"></line>
                    <line x1="6" y1="6" x2="18" y2="18"></line>
                  </svg>
                </button>
              </div>
            </div>

            {/* Sub-Navigation Tabs */}
            <div className="flex border-b border-gray-200 px-5 bg-white">
              <button
                onClick={() => setActiveTab('overview')}
                className={`py-3 px-4 font-semibold text-xs border-b-2 transition-colors ${
                  activeTab === 'overview'
                    ? 'border-indigo-600 text-indigo-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                Overview & Tax Computation
              </button>
              <button
                onClick={() => setActiveTab('checklist')}
                className={`py-3 px-4 font-semibold text-xs border-b-2 transition-colors flex items-center gap-1.5 ${
                  activeTab === 'checklist'
                    ? 'border-indigo-600 text-indigo-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                DTAA Checklist
                <span className="px-1.5 py-0.2 bg-gray-100 text-gray-600 rounded-full text-[10px]">
                  {activeItem.checklist.filter((x) => x.status === 'Verified').length}/{activeItem.checklist.length}
                </span>
              </button>
              <button
                onClick={() => setActiveTab('form15cb')}
                className={`py-3 px-4 font-semibold text-xs border-b-2 transition-colors flex items-center gap-1.5 ${
                  activeTab === 'form15cb'
                    ? 'border-indigo-600 text-indigo-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                Form 15CB Certificate
                {activeItem.status === 'Certified' && (
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-blue-600">
                    <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                    <polyline points="22 4 12 14.01 9 11.01"></polyline>
                  </svg>
                )}
              </button>
              <button
                onClick={() => setActiveTab('form15ca')}
                className={`py-3 px-4 font-semibold text-xs border-b-2 transition-colors ${
                  activeTab === 'form15ca'
                    ? 'border-indigo-600 text-indigo-600'
                    : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                Form 15CA e-Filing XML
              </button>
            </div>

            {/* Tab Contents */}
            <div className="p-5 flex-1 overflow-y-auto space-y-6">
              {/* TAB 1: OVERVIEW & TAX COMPUTATION */}
              {activeTab === 'overview' && (
                <div className="space-y-6">
                  {/* Form 15CA Routing Banner */}
                  <div className="p-4 bg-indigo-50/70 border border-indigo-200 rounded-xl flex items-start gap-3">
                    <div className="text-indigo-600 shrink-0 mt-0.5">
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                        <circle cx="12" cy="12" r="10"></circle>
                        <line x1="12" y1="16" x2="12" y2="12"></line>
                        <line x1="12" y1="8" x2="12.01" y2="8"></line>
                      </svg>
                    </div>
                    <div>
                      <h4 className="text-sm font-bold text-indigo-900">
                        Statutory Form 15CA Classification: {activeItem.form15CaPart}
                      </h4>
                      <p className="text-xs text-indigo-800 mt-1 leading-relaxed">
                        {activeItem.form15CaRoutingReason}
                      </p>
                    </div>
                  </div>

                  {/* Financial Computation Cards */}
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                    <div className="p-3 bg-gray-50 rounded-lg border border-gray-200">
                      <span className="text-[11px] font-medium text-gray-500">Foreign Remittance</span>
                      <p className="text-base font-bold text-gray-900 mt-0.5">
                        {activeItem.foreignAmount.toLocaleString()} {activeItem.remittanceCurrency}
                      </p>
                      <span className="text-[10px] text-gray-400">@ ₹{activeItem.exchangeRate.toFixed(4)} TT</span>
                    </div>

                    <div className="p-3 bg-gray-50 rounded-lg border border-gray-200">
                      <span className="text-[11px] font-medium text-gray-500">INR Equivalent</span>
                      <p className="text-base font-bold text-gray-900 mt-0.5">
                        ₹{activeItem.inrEquivalent.toLocaleString('en-IN')}
                      </p>
                      {activeItem.taxBorneByRemitter && (
                        <span className="text-[10px] text-amber-600 font-semibold">195A Grossed: ₹{activeItem.grossedUpAmountInr.toLocaleString('en-IN')}</span>
                      )}
                    </div>

                    <div className="p-3 bg-indigo-50 rounded-lg border border-indigo-200">
                      <span className="text-[11px] font-medium text-indigo-700">TDS Rate (Sec 90(2))</span>
                      <p className="text-base font-bold text-indigo-900 mt-0.5">
                        {activeItem.finalWithholdingRate.toFixed(2)}%
                      </p>
                      <span className="text-[10px] text-indigo-600">{activeItem.applicableRateType}</span>
                    </div>

                    <div className="p-3 bg-emerald-50 rounded-lg border border-emerald-200">
                      <span className="text-[11px] font-medium text-emerald-700">Total TDS Withheld</span>
                      <p className="text-base font-bold text-emerald-900 mt-0.5">
                        ₹{activeItem.tdsAmountInr.toLocaleString('en-IN')}
                      </p>
                      <span className="text-[10px] text-emerald-600 font-medium">({activeItem.tdsAmountFc} {activeItem.remittanceCurrency})</span>
                    </div>
                  </div>

                  {/* Section 90(2) Comparison Matrix: Domestic vs Treaty */}
                  <div className="border border-gray-200 rounded-xl overflow-hidden shadow-sm">
                    <div className="bg-gray-100/70 px-4 py-2.5 border-b border-gray-200">
                      <h4 className="text-xs font-bold text-gray-700 uppercase tracking-wider">
                        Section 90(2) Rate Evaluation Matrix (Domestic Law vs. DTAA)
                      </h4>
                    </div>
                    <div className="p-4 grid grid-cols-1 md:grid-cols-2 gap-4">
                      {/* Domestic Pillar */}
                      <div className="p-3.5 bg-gray-50 rounded-lg border border-gray-200 space-y-2">
                        <div className="flex items-center justify-between">
                          <span className="text-xs font-bold text-gray-800">Domestic Income-tax Act</span>
                          <span className="text-xs px-2 py-0.5 bg-gray-200 text-gray-700 rounded font-semibold">
                            {activeItem.effectiveDomesticRate.toFixed(2)}%
                          </span>
                        </div>
                        <p className="text-[11px] text-gray-600">{activeItem.domesticSection}</p>
                        <div className="text-[11px] text-gray-500 space-y-1 pt-1 border-t border-gray-200">
                          <div>Base Rate: {activeItem.domesticTaxRate}%</div>
                          <div>Surcharge: {activeItem.surchargeRate}% • Cess: {activeItem.cessRate}%</div>
                        </div>
                      </div>

                      {/* Treaty Pillar */}
                      <div className={`p-3.5 rounded-lg border space-y-2 ${
                        activeItem.applicableRateType === 'TreatyRate'
                          ? 'bg-emerald-50/60 border-emerald-200'
                          : 'bg-gray-50 border-gray-200'
                      }`}>
                        <div className="flex items-center justify-between">
                          <span className="text-xs font-bold text-gray-800">DTAA ({activeItem.treatyCountry})</span>
                          <span className={`text-xs px-2 py-0.5 rounded font-semibold ${
                            activeItem.applicableRateType === 'TreatyRate'
                              ? 'bg-emerald-600 text-white'
                              : 'bg-gray-200 text-gray-700'
                          }`}>
                            {activeItem.treatyTaxRate.toFixed(2)}%
                          </span>
                        </div>
                        <p className="text-[11px] text-gray-600">{activeItem.treatyArticle}</p>
                        <div className="text-[11px] text-gray-500 space-y-1 pt-1 border-t border-gray-200">
                          <div>TRC & Form 10F: {activeItem.hasTrc && activeItem.hasForm10F ? '✓ Verified' : '✗ Missing'}</div>
                          <div>No-PE Declaration: {activeItem.noPeDeclaration ? '✓ Confirmed' : '✗ Unconfirmed'}</div>
                        </div>
                      </div>
                    </div>
                  </div>

                  {/* Transaction Particulars Details */}
                  <div className="border border-gray-200 rounded-xl p-4 space-y-3">
                    <h4 className="text-xs font-bold text-gray-700 uppercase tracking-wider">Transaction Records</h4>
                    <div className="grid grid-cols-2 sm:grid-cols-3 gap-3 text-xs">
                      <div>
                        <span className="text-gray-400">Nature of Remittance:</span>
                        <p className="font-semibold text-gray-800">{activeItem.natureOfRemittance}</p>
                      </div>
                      <div>
                        <span className="text-gray-400">Invoice Number:</span>
                        <p className="font-semibold text-gray-800">{activeItem.invoiceNumber || 'N/A'}</p>
                      </div>
                      <div>
                        <span className="text-gray-400">Proposed Date:</span>
                        <p className="font-semibold text-gray-800">
                          {activeItem.proposedRemittanceDate ? new Date(activeItem.proposedRemittanceDate).toLocaleDateString() : 'Pending'}
                        </p>
                      </div>
                      <div>
                        <span className="text-gray-400">Beneficiary Tax ID (TIN):</span>
                        <p className="font-semibold text-gray-800 font-mono">{activeItem.beneficiaryTaxId || 'N/A'}</p>
                      </div>
                      <div>
                        <span className="text-gray-400">Net Payable in FC:</span>
                        <p className="font-bold text-emerald-700">
                          {activeItem.netRemittanceFc.toLocaleString()} {activeItem.remittanceCurrency}
                        </p>
                      </div>
                      <div>
                        <span className="text-gray-400">Net Payable in INR:</span>
                        <p className="font-bold text-emerald-700">₹{activeItem.netRemittanceInr.toLocaleString('en-IN')}</p>
                      </div>
                    </div>
                  </div>
                </div>
              )}

              {/* TAB 2: DTAA CHECKLIST */}
              {activeTab === 'checklist' && (
                <div className="space-y-4">
                  <div className="p-3 bg-blue-50 border border-blue-200 rounded-lg text-xs text-blue-800 flex items-center justify-between">
                    <span>
                      Mandatory documents required to establish treaty eligibility under Section 90(4)/(5) and Rule 37BB.
                    </span>
                    <span className="font-semibold">
                      {activeItem.checklist.filter((x) => x.status === 'Verified').length} of {activeItem.checklist.length} Verified
                    </span>
                  </div>

                  <div className="border border-gray-200 rounded-xl overflow-hidden divide-y divide-gray-100">
                    {activeItem.checklist.map((item) => (
                      <div key={item.id} className="p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-3 hover:bg-gray-50/50">
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <span className="font-bold text-xs text-gray-900">{item.itemCode}</span>
                            {item.isMandatory && (
                              <span className="text-[10px] bg-red-50 text-red-700 px-1.5 py-0.2 rounded border border-red-200 font-medium">
                                Mandatory
                              </span>
                            )}
                          </div>
                          <p className="text-xs text-gray-600">{item.itemDescription}</p>
                          {item.verificationNotes && (
                            <p className="text-[11px] text-gray-500 italic">Notes: {item.verificationNotes}</p>
                          )}
                        </div>

                        <div className="flex items-center gap-2 shrink-0">
                          <select
                            value={item.status}
                            onChange={(e) => handleChecklistStatusChange(item.id, e.target.value)}
                            className={`text-xs font-semibold px-2.5 py-1 rounded-md border shadow-sm focus:outline-none ${
                              item.status === 'Verified'
                                ? 'bg-emerald-50 text-emerald-700 border-emerald-300'
                                : item.status === 'Rejected'
                                ? 'bg-red-50 text-red-700 border-red-300'
                                : item.status === 'Waived'
                                ? 'bg-amber-50 text-amber-700 border-amber-300'
                                : 'bg-gray-50 text-gray-700 border-gray-300'
                            }`}
                          >
                            <option value="Pending">Pending</option>
                            <option value="Verified">Verified</option>
                            <option value="Waived">Waived</option>
                            <option value="Rejected">Rejected</option>
                          </select>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* TAB 3: FORM 15CB CA CERTIFICATE */}
              {activeTab === 'form15cb' && (
                <div className="space-y-4">
                  {/* Status & UDIN Bar */}
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-3 bg-gray-50 border border-gray-200 rounded-lg">
                    <div className="flex items-center gap-2">
                      <div className={`p-1.5 rounded-md ${activeItem.status === 'Certified' ? 'bg-blue-100 text-blue-700' : 'bg-gray-200 text-gray-500'}`}>
                        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                          <circle cx="12" cy="8" r="7"></circle>
                          <polyline points="8.21 13.89 7 23 12 20 17 23 15.79 13.88"></polyline>
                        </svg>
                      </div>
                      <div>
                        <span className="text-xs font-bold text-gray-800">
                          {activeItem.status === 'Certified' ? 'Certified by Chartered Accountant' : 'Draft Form 15CB'}
                        </span>
                        {activeItem.udin && (
                          <p className="text-[11px] font-mono text-blue-700 font-semibold">UDIN: {activeItem.udin}</p>
                        )}
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      {activeItem.generated15CbCertificateText && (
                        <>
                          <button
                            onClick={() => copyToClipboard(activeItem.generated15CbCertificateText!, 'Form 15CB Text')}
                            className="inline-flex items-center gap-1 px-3 py-1.5 border border-gray-300 bg-white rounded-md text-xs font-medium text-gray-700 hover:bg-gray-50"
                          >
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                              <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                            </svg>
                            Copy
                          </button>
                          <a
                            href={`data:text/plain;charset=utf-8,${encodeURIComponent(activeItem.generated15CbCertificateText)}`}
                            download={`Form15CB_${activeItem.remittanceReference}.txt`}
                            className="inline-flex items-center gap-1 px-3 py-1.5 bg-gray-900 text-white rounded-md text-xs font-medium hover:bg-gray-800"
                          >
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                              <polyline points="7 10 12 15 17 10"></polyline>
                              <line x1="12" y1="15" x2="12" y2="3"></line>
                            </svg>
                            Download
                          </a>
                        </>
                      )}
                    </div>
                  </div>

                  {/* Preformatted Certificate Text View */}
                  <div className="bg-slate-900 text-slate-100 p-5 rounded-xl font-mono text-xs overflow-x-auto whitespace-pre leading-relaxed shadow-inner max-h-[500px]">
                    {activeItem.generated15CbCertificateText || 'Certificate not yet generated.'}
                  </div>
                </div>
              )}

              {/* TAB 4: FORM 15CA XML */}
              {activeTab === 'form15ca' && (
                <div className="space-y-4">
                  <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-3 bg-gray-50 border border-gray-200 rounded-lg">
                    <div>
                      <span className="text-xs font-bold text-gray-800">
                        Income Tax e-Filing Portal Schema ({activeItem.form15CaPart})
                      </span>
                      <p className="text-[11px] text-gray-500">
                        Compatible with the Directorate of Income Tax e-filing portal upload schema
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      {activeItem.generated15CaXmlPayload && (
                        <>
                          <button
                            onClick={() => copyToClipboard(activeItem.generated15CaXmlPayload!, 'Form 15CA XML')}
                            className="inline-flex items-center gap-1 px-3 py-1.5 border border-gray-300 bg-white rounded-md text-xs font-medium text-gray-700 hover:bg-gray-50"
                          >
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                              <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                            </svg>
                            Copy XML
                          </button>
                          <a
                            href={`data:application/xml;charset=utf-8,${encodeURIComponent(activeItem.generated15CaXmlPayload)}`}
                            download={`Form15CA_${activeItem.remittanceReference}.xml`}
                            className="inline-flex items-center gap-1 px-3 py-1.5 bg-indigo-600 text-white rounded-md text-xs font-medium hover:bg-indigo-700"
                          >
                            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                              <polyline points="7 10 12 15 17 10"></polyline>
                              <line x1="12" y1="15" x2="12" y2="3"></line>
                            </svg>
                            Download XML
                          </a>
                        </>
                      )}
                    </div>
                  </div>

                  <div className="bg-slate-950 text-emerald-400 p-5 rounded-xl font-mono text-xs overflow-x-auto whitespace-pre leading-relaxed shadow-inner max-h-[500px]">
                    {activeItem.generated15CaXmlPayload || 'XML not yet generated.'}
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>

      {/* CREATE MODAL */}
      {showCreateModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm overflow-y-auto">
          <div className="bg-white rounded-2xl max-w-4xl w-full max-h-[90vh] overflow-y-auto shadow-2xl border border-gray-200">
            <div className="sticky top-0 bg-white px-6 py-4 border-b border-gray-200 flex items-center justify-between z-10">
              <div className="flex items-center gap-2.5">
                <div className="p-1.5 bg-indigo-50 rounded-lg text-indigo-700">
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <circle cx="12" cy="12" r="10"></circle>
                    <line x1="2" y1="12" x2="22" y2="12"></line>
                    <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z"></path>
                  </svg>
                </div>
                <h3 className="text-lg font-bold text-gray-900">New Foreign Remittance Assessment</h3>
              </div>
              <button
                onClick={() => setShowCreateModal(false)}
                className="p-1 text-gray-400 hover:text-gray-600 rounded-md"
              >
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="18" y1="6" x2="6" y2="18"></line>
                  <line x1="6" y1="6" x2="18" y2="18"></line>
                </svg>
              </button>
            </div>

            <form onSubmit={handleCreateSubmit} className="p-6 space-y-6">
              {/* Step 1: Remitter (Payer) */}
              <div className="space-y-4">
                <h4 className="text-xs font-bold uppercase tracking-wider text-indigo-600 border-b pb-1">
                  1. Remitter (Indian Payer) Details
                </h4>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Select Client *</label>
                    <select
                      value={createReq.clientId || ''}
                      onChange={(e) => handleClientSelect(e.target.value)}
                      required
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                    >
                      <option value="">-- Choose Client --</option>
                      {clients.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.name} ({c.taxIds?.['PAN'] || 'No PAN'})
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Remitter PAN *</label>
                    <input
                      type="text"
                      maxLength={10}
                      value={createReq.remitterPan || ''}
                      onChange={(e) => setCreateReq({ ...createReq, remitterPan: e.target.value.toUpperCase() })}
                      required
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm font-mono uppercase"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Remitter TAN</label>
                    <input
                      type="text"
                      maxLength={10}
                      value={createReq.remitterTan || ''}
                      onChange={(e) => setCreateReq({ ...createReq, remitterTan: e.target.value.toUpperCase() })}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm font-mono uppercase"
                    />
                  </div>
                </div>
              </div>

              {/* Step 2: Beneficiary (Non-Resident Payee) */}
              <div className="space-y-4">
                <h4 className="text-xs font-bold uppercase tracking-wider text-indigo-600 border-b pb-1">
                  2. Non-Resident Beneficiary (Remittee) Details
                </h4>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Beneficiary Name *</label>
                    <input
                      type="text"
                      placeholder="e.g. Amazon Web Services Inc"
                      value={createReq.beneficiaryName || ''}
                      onChange={(e) => setCreateReq({ ...createReq, beneficiaryName: e.target.value })}
                      required
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Country of Residence *</label>
                    <input
                      type="text"
                      placeholder="e.g. United States"
                      value={createReq.beneficiaryCountry || ''}
                      onChange={(e) => setCreateReq({ ...createReq, beneficiaryCountry: e.target.value })}
                      required
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Tax ID in Country (TIN/EIN)</label>
                    <input
                      type="text"
                      placeholder="e.g. EIN-91-1646860"
                      value={createReq.beneficiaryTaxId || ''}
                      onChange={(e) => setCreateReq({ ...createReq, beneficiaryTaxId: e.target.value })}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                    />
                  </div>
                </div>
              </div>

              {/* Step 3: Remittance Particulars */}
              <div className="space-y-4">
                <h4 className="text-xs font-bold uppercase tracking-wider text-indigo-600 border-b pb-1">
                  3. Remittance Particulars & RBI Purpose Code
                </h4>
                <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Currency *</label>
                    <select
                      value={createReq.remittanceCurrency || 'USD'}
                      onChange={(e) => setCreateReq({ ...createReq, remittanceCurrency: e.target.value })}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                    >
                      {STANDARD_CURRENCIES.map((c) => (
                        <option key={c} value={c}>{c}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Foreign Amount *</label>
                    <input
                      type="number"
                      step="0.01"
                      placeholder="50000"
                      value={createReq.foreignAmount || ''}
                      onChange={(e) => setCreateReq({ ...createReq, foreignAmount: parseFloat(e.target.value) || 0 })}
                      required
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Exchange Rate (TT Selling) *</label>
                    <input
                      type="number"
                      step="0.0001"
                      value={createReq.exchangeRate || 85.0}
                      onChange={(e) => setCreateReq({ ...createReq, exchangeRate: parseFloat(e.target.value) || 0 })}
                      required
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Approx INR Value</label>
                    <div className="px-3 py-2 bg-gray-100 rounded-lg text-sm font-bold text-gray-800">
                      ₹{((createReq.foreignAmount || 0) * (createReq.exchangeRate || 0)).toLocaleString('en-IN')}
                    </div>
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">RBI Purpose Code *</label>
                    <select
                      value={createReq.rbiPurposeCode || 'P0802'}
                      onChange={(e) => {
                        const sel = RBI_PURPOSE_CODES.find((x) => x.code === e.target.value)
                        setCreateReq({
                          ...createReq,
                          rbiPurposeCode: e.target.value,
                          purposeDescription: sel?.desc || ''
                        })
                      }}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                    >
                      {RBI_PURPOSE_CODES.map((r) => (
                        <option key={r.code} value={r.code}>
                          {r.code} - {r.desc.substring(0, 45)}... {r.exempt ? '(Exempt Rule 37BB)' : ''}
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Nature of Remittance</label>
                    <select
                      value={createReq.natureOfRemittance || 'FeesForTechnicalServices'}
                      onChange={(e) => setCreateReq({ ...createReq, natureOfRemittance: e.target.value })}
                      className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                    >
                      <option value="FeesForTechnicalServices">Fees for Technical Services (FTS)</option>
                      <option value="Royalty">Royalty (IP / Software License)</option>
                      <option value="ProfessionalServices">Professional Services</option>
                      <option value="Interest">Interest</option>
                      <option value="Dividend">Dividend</option>
                      <option value="PurchaseOfGoods">Purchase of Commercial Goods</option>
                      <option value="Reimbursement">Reimbursement of Actual Expenses</option>
                      <option value="Other">Other Non-Resident Payment</option>
                    </select>
                  </div>
                </div>
              </div>

              {/* Step 4: Taxability & DTAA Relief */}
              <div className="space-y-4">
                <h4 className="text-xs font-bold uppercase tracking-wider text-indigo-600 border-b pb-1">
                  4. Section 195 Taxability & DTAA Treaty Relief
                </h4>

                <div className="flex items-center gap-6">
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={createReq.isChargeableToTax ?? true}
                      onChange={(e) => setCreateReq({ ...createReq, isChargeableToTax: e.target.checked })}
                      className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500 w-4 h-4"
                    />
                    <span className="text-xs font-medium text-gray-800">Chargeable to Tax in India (Sec 195)</span>
                  </label>

                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={createReq.treatyBenefitClaimed ?? true}
                      onChange={(e) => setCreateReq({ ...createReq, treatyBenefitClaimed: e.target.checked })}
                      className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500 w-4 h-4"
                    />
                    <span className="text-xs font-medium text-gray-800">Claim DTAA Treaty Benefit (Sec 90)</span>
                  </label>

                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={createReq.taxBorneByRemitter ?? false}
                      onChange={(e) => setCreateReq({ ...createReq, taxBorneByRemitter: e.target.checked })}
                      className="rounded border-gray-300 text-indigo-600 focus:ring-indigo-500 w-4 h-4"
                    />
                    <span className="text-xs font-medium text-gray-800">Tax Borne by Remitter (Sec 195A Grossing up)</span>
                  </label>
                </div>

                {createReq.treatyBenefitClaimed && (
                  <div className="p-4 bg-indigo-50/60 rounded-xl border border-indigo-200 space-y-3">
                    <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                      <div>
                        <label className="block text-xs font-medium text-gray-700 mb-1">Treaty Country</label>
                        <input
                          type="text"
                          value={createReq.treatyCountry || ''}
                          onChange={(e) => setCreateReq({ ...createReq, treatyCountry: e.target.value })}
                          className="w-full px-3 py-1.5 border border-gray-300 rounded text-xs bg-white"
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-medium text-gray-700 mb-1">Treaty Article</label>
                        <input
                          type="text"
                          value={createReq.treatyArticle || ''}
                          onChange={(e) => setCreateReq({ ...createReq, treatyArticle: e.target.value })}
                          className="w-full px-3 py-1.5 border border-gray-300 rounded text-xs bg-white"
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-medium text-gray-700 mb-1">Treaty Tax Rate (%)</label>
                        <input
                          type="number"
                          step="0.01"
                          value={createReq.treatyTaxRate || 10.0}
                          onChange={(e) => setCreateReq({ ...createReq, treatyTaxRate: parseFloat(e.target.value) || 0 })}
                          className="w-full px-3 py-1.5 border border-gray-300 rounded text-xs bg-white"
                        />
                      </div>
                    </div>

                    <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 pt-2 border-t border-indigo-100 text-xs">
                      <label className="flex items-center gap-1.5 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={createReq.hasTrc ?? true}
                          onChange={(e) => setCreateReq({ ...createReq, hasTrc: e.target.checked })}
                          className="rounded text-indigo-600"
                        />
                        <span>TRC Valid</span>
                      </label>
                      <label className="flex items-center gap-1.5 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={createReq.hasForm10F ?? true}
                          onChange={(e) => setCreateReq({ ...createReq, hasForm10F: e.target.checked })}
                          className="rounded text-indigo-600"
                        />
                        <span>Form 10F Verified</span>
                      </label>
                      <label className="flex items-center gap-1.5 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={createReq.noPeDeclaration ?? true}
                          onChange={(e) => setCreateReq({ ...createReq, noPeDeclaration: e.target.checked })}
                          className="rounded text-indigo-600"
                        />
                        <span>No-PE Declaration</span>
                      </label>
                      <label className="flex items-center gap-1.5 cursor-pointer">
                        <input
                          type="checkbox"
                          checked={createReq.beneficialOwnershipConfirmed ?? true}
                          onChange={(e) => setCreateReq({ ...createReq, beneficialOwnershipConfirmed: e.target.checked })}
                          className="rounded text-indigo-600"
                        />
                        <span>Beneficial Owner</span>
                      </label>
                    </div>
                  </div>
                )}
              </div>

              {/* Form Buttons */}
              <div className="flex items-center justify-end gap-3 pt-4 border-t border-gray-200">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="px-4 py-2 border border-gray-300 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-5 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-semibold shadow transition-colors"
                >
                  Create Assessment & Generate Draft 15CA/CB
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* CERTIFY MODAL */}
      {showCertifyModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-2xl border border-gray-200 space-y-4">
            <div className="flex items-center gap-2">
              <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="text-indigo-600">
                <circle cx="12" cy="8" r="7"></circle>
                <polyline points="8.21 13.89 7 23 12 20 17 23 15.79 13.88"></polyline>
              </svg>
              <h3 className="text-base font-bold text-gray-900">Certify Form 15CB (Chartered Accountant)</h3>
            </div>
            <p className="text-xs text-gray-600 leading-relaxed">
              In accordance with Rule 37BB(2) and ICAI UDIN guidelines, please enter your professional details and generated 18-digit UDIN.
            </p>

            <form onSubmit={handleCertify} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Chartered Accountant Name *</label>
                <input
                  type="text"
                  value={certifyForm.caName}
                  onChange={(e) => setCertifyForm({ ...certifyForm, caName: e.target.value })}
                  required
                  className="w-full px-3 py-1.5 border border-gray-300 rounded text-sm"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">ICAI Membership No *</label>
                  <input
                    type="text"
                    value={certifyForm.caMembershipNumber}
                    onChange={(e) => setCertifyForm({ ...certifyForm, caMembershipNumber: e.target.value })}
                    required
                    className="w-full px-3 py-1.5 border border-gray-300 rounded text-sm font-mono"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Firm FRN</label>
                  <input
                    type="text"
                    value={certifyForm.caFrn}
                    onChange={(e) => setCertifyForm({ ...certifyForm, caFrn: e.target.value })}
                    className="w-full px-3 py-1.5 border border-gray-300 rounded text-sm font-mono"
                  />
                </div>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Firm Name</label>
                <input
                  type="text"
                  value={certifyForm.caFirmName}
                  onChange={(e) => setCertifyForm({ ...certifyForm, caFirmName: e.target.value })}
                  className="w-full px-3 py-1.5 border border-gray-300 rounded text-sm"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Unique Document ID (UDIN) *</label>
                <input
                  type="text"
                  maxLength={18}
                  placeholder="24214589BK99887766"
                  value={certifyForm.udin}
                  onChange={(e) => setCertifyForm({ ...certifyForm, udin: e.target.value.toUpperCase() })}
                  required
                  className="w-full px-3 py-1.5 border border-gray-300 rounded text-sm font-mono tracking-wider uppercase text-blue-700 font-bold"
                />
                <span className="text-[10px] text-gray-400">Must be exactly 18 alphanumeric characters.</span>
              </div>

              <div className="flex items-center justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyModal(false)}
                  className="px-3 py-1.5 border border-gray-300 rounded text-xs text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded text-xs font-semibold shadow"
                >
                  Sign & Certify
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* RECORD FILING MODAL */}
      {showFileModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="bg-white rounded-xl max-w-sm w-full p-5 shadow-2xl border border-gray-200 space-y-3">
            <h3 className="text-sm font-bold text-gray-900">Record Income Tax Portal Filing</h3>
            <p className="text-xs text-gray-500">
              Enter the Acknowledgement Number issued upon successful Form 15CA submission on the e-filing portal.
            </p>
            <form onSubmit={handleFile} className="space-y-3">
              <input
                type="text"
                placeholder="e.g. ACK15CA20240019284"
                value={portalAck}
                onChange={(e) => setPortalAck(e.target.value)}
                required
                className="w-full px-3 py-2 border border-gray-300 rounded text-sm font-mono"
              />
              <div className="flex justify-end gap-2">
                <button
                  type="button"
                  onClick={() => setShowFileModal(false)}
                  className="px-3 py-1.5 border border-gray-300 rounded text-xs text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded text-xs font-semibold"
                >
                  Save Filing
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
