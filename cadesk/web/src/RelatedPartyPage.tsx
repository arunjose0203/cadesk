import { useState, useEffect, useMemo, useCallback, type FormEvent } from 'react'
import {
  relatedPartyApi,
  api,
  type RelatedPartyEntityDto,
  type RelatedPartyTransactionDto,
  type RelatedPartyOmnibusApprovalDto,
  type RelatedPartySummaryDto,
  type SaveRelatedPartyEntityRequest,
  type SaveRelatedPartyTransactionRequest,
  type SaveOmnibusApprovalRequest,
  type Client
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

const RELATIONSHIP_TYPES = [
  'HoldingCompany',
  'Subsidiary',
  'FellowSubsidiary',
  'Associate',
  'JointVenture',
  'KeyManagementPersonnel',
  'RelativeOfKMP',
  'EnterpriseControlledByKMP'
]

const TRANSACTION_TYPES = [
  { type: 'SaleOfGoods', label: 'Sale of Goods / Materials (10% Turnover)', base: 'Turnover' },
  { type: 'PurchaseOfGoods', label: 'Purchase of Goods / Materials (10% Turnover)', base: 'Turnover' },
  { type: 'LeaseOfProperty', label: 'Leasing of Property (10% Turnover)', base: 'Turnover' },
  { type: 'RenderingOfServices', label: 'Rendering of Services (10% Turnover)', base: 'Turnover' },
  { type: 'AvailingOfServices', label: 'Availing of Services (10% Turnover)', base: 'Turnover' },
  { type: 'SellingBuyingProperty', label: 'Selling / Buying Property (10% Net Worth)', base: 'NetWorth' },
  { type: 'RemunerationOfficeOfProfit', label: 'Remuneration / Office of Profit (₹2.5L/mo limit)', base: 'Fixed' },
  { type: 'LoanGiven', label: 'Inter-Corporate Loan / Advance Given', base: 'Turnover' },
  { type: 'LoanReceived', label: 'Inter-Corporate Loan / Advance Received', base: 'Turnover' },
  { type: 'GuaranteeUnderwriting', label: 'Guarantee / Underwriting Securities (1% Net Worth)', base: 'NetWorth' }
]

const TP_METHODS = ['CUP', 'ResalePriceMethod', 'CostPlusMethod', 'TNMM', 'IndependentValuation', 'NotApplicable']

export function RelatedPartyPage() {
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')

  // Data states
  const [summary, setSummary] = useState<RelatedPartySummaryDto | null>(null)
  const [entities, setEntities] = useState<RelatedPartyEntityDto[]>([])
  const [transactions, setTransactions] = useState<RelatedPartyTransactionDto[]>([])
  const [omnibusList, setOmnibusList] = useState<RelatedPartyOmnibusApprovalDto[]>([])
  const [mbp4Text, setMbp4Text] = useState('')
  const [as18Text, setAs18Text] = useState('')

  const [loading, setLoading] = useState(true)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<'transactions' | 'entities' | 'omnibus' | 'reports'>('transactions')
  const [reportSubTab, setReportSubTab] = useState<'mbp4' | 'as18'>('mbp4')

  // Search filter
  const [searchQuery, setSearchQuery] = useState('')

  // Modals
  const [showEntityModal, setShowEntityModal] = useState(false)
  const [showTxModal, setShowTxModal] = useState(false)
  const [showOmnibusModal, setShowOmnibusModal] = useState(false)
  const [showApproveModal, setShowApproveModal] = useState(false)

  // Selected item for approve modal
  const [targetTx, setTargetTx] = useState<RelatedPartyTransactionDto | null>(null)

  // Form states
  const [entityForm, setEntityForm] = useState<Partial<SaveRelatedPartyEntityRequest>>({
    relationshipType: 'Subsidiary',
    isActive: true
  })

  const [txForm, setTxForm] = useState<Partial<SaveRelatedPartyTransactionRequest>>({
    financialYear: '2024-25',
    transactionDate: new Date().toISOString().substring(0, 10),
    transactionType: 'SaleOfGoods',
    currency: 'INR',
    annualTurnoverBenchmark: 100000000,
    netWorthBenchmark: 50000000,
    isOrdinaryCourseOfBusiness: true,
    armLengthMethod: 'CUP',
    auditCommitteeApprovalStatus: 'Approved',
    balanceType: 'Debit',
    outstandingBalanceAtYearEnd: 0
  })

  const [omniForm, setOmniForm] = useState<Partial<SaveOmnibusApprovalRequest>>({
    financialYear: '2024-25',
    auditCommitteeMeetingDate: new Date().toISOString().substring(0, 10),
    validityPeriodTo: new Date(new Date().getFullYear() + 1, 2, 31).toISOString().substring(0, 10),
    maximumApprovedLimit: 10000000
  })

  const [approveForm, setApproveForm] = useState({
    approvalStatus: 'Approved',
    meetingDate: new Date().toISOString().substring(0, 10),
    boardDate: new Date().toISOString().substring(0, 10),
    shareholdersDate: ''
  })

  // Load clients initially
  useEffect(() => {
    const initClients = async () => {
      try {
        const res = await api<ClientPage>('/api/clients?pageSize=100').catch(() => ({ items: [], total: 0 }))
        setClients(res.items || [])
        if (res.items && res.items.length > 0) {
          setSelectedClientId(prev => prev || res.items[0].id)
        }
      } catch (err: unknown) {
        console.error(err)
      }
    }
    initClients()
  }, [])

  // Load module data
  const fetchData = useCallback(async () => {
    if (!selectedClientId) return
    try {
      setLoading(true)
      setErrorMsg(null)

      const [sum, ents, txs, omns] = await Promise.all([
        relatedPartyApi.getSummary(selectedClientId, selectedFy),
        relatedPartyApi.listEntities(selectedClientId),
        relatedPartyApi.listTransactions(selectedClientId, selectedFy),
        relatedPartyApi.listOmnibus(selectedClientId, selectedFy)
      ])

      setSummary(sum)
      setEntities(ents)
      setTransactions(txs)
      setOmnibusList(omns)

      // Fetch reports text
      const reps = await relatedPartyApi.getReports(selectedClientId, selectedFy).catch(() => ({ mbp4RegisterText: '', as18DisclosureNoteText: '' }))
      setMbp4Text(reps.mbp4RegisterText)
      setAs18Text(reps.as18DisclosureNoteText)
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to load related party records.'
      setErrorMsg(msg)
    } finally {
      setLoading(false)
    }
  }, [selectedClientId, selectedFy])

  useEffect(() => {
    fetchData()
  }, [fetchData])

  // Filtered transactions
  const filteredTxs = useMemo(() => {
    return transactions.filter((t) => {
      const q = searchQuery.toLowerCase()
      return (
        !q ||
        t.contractReference.toLowerCase().includes(q) ||
        (t.relatedPartyName && t.relatedPartyName.toLowerCase().includes(q)) ||
        t.transactionType.toLowerCase().includes(q) ||
        t.description.toLowerCase().includes(q)
      )
    })
  }, [transactions, searchQuery])

  // Handle entity submit
  const handleEntitySubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedClientId || !entityForm.name) {
      alert('Please fill party name.')
      return
    }

    try {
      setLoading(true)
      await relatedPartyApi.createEntity({
        ...entityForm,
        clientId: selectedClientId,
        name: entityForm.name.trim(),
        relationshipType: entityForm.relationshipType || 'Subsidiary',
        isActive: entityForm.isActive ?? true
      } as SaveRelatedPartyEntityRequest)

      setShowEntityModal(false)
      setEntityForm({ relationshipType: 'Subsidiary', isActive: true })
      await fetchData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to save related party entity.'
      alert(msg)
    } finally {
      setLoading(false)
    }
  }

  // Handle transaction submit
  const handleTxSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedClientId || !txForm.relatedPartyId || !txForm.transactionAmount) {
      alert('Please select related party and enter transaction amount.')
      return
    }

    try {
      setLoading(true)
      await relatedPartyApi.createTransaction({
        ...txForm,
        clientId: selectedClientId,
        financialYear: selectedFy,
        transactionDate: new Date(txForm.transactionDate || new Date()).toISOString(),
        transactionType: txForm.transactionType || 'SaleOfGoods',
        transactionAmount: txForm.transactionAmount || 0,
        annualTurnoverBenchmark: txForm.annualTurnoverBenchmark || 0,
        netWorthBenchmark: txForm.netWorthBenchmark || 0,
        isOrdinaryCourseOfBusiness: txForm.isOrdinaryCourseOfBusiness ?? true,
        benchmarkArmLengthPrice: txForm.benchmarkArmLengthPrice || 0,
        outstandingBalanceAtYearEnd: txForm.outstandingBalanceAtYearEnd || 0
      } as SaveRelatedPartyTransactionRequest)

      setShowTxModal(false)
      await fetchData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to save transaction.'
      alert(msg)
    } finally {
      setLoading(false)
    }
  }

  // Handle Omnibus submit
  const handleOmnibusSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedClientId || !omniForm.title || !omniForm.maximumApprovedLimit) {
      alert('Please fill title and maximum approved limit.')
      return
    }

    try {
      setLoading(true)
      await relatedPartyApi.createOmnibus({
        ...omniForm,
        clientId: selectedClientId,
        financialYear: selectedFy,
        auditCommitteeMeetingDate: new Date(omniForm.auditCommitteeMeetingDate || new Date()).toISOString(),
        validityPeriodTo: new Date(omniForm.validityPeriodTo || new Date()).toISOString(),
        maximumApprovedLimit: omniForm.maximumApprovedLimit || 0
      } as SaveOmnibusApprovalRequest)

      setShowOmnibusModal(false)
      await fetchData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to save omnibus approval.'
      alert(msg)
    } finally {
      setLoading(false)
    }
  }

  // Handle Approve submit
  const handleApproveSubmit = async (e: FormEvent) => {
    e.preventDefault()
    if (!targetTx) return

    try {
      setLoading(true)
      await relatedPartyApi.approveTransaction(targetTx.id, {
        approvalStatus: approveForm.approvalStatus,
        meetingDate: approveForm.meetingDate ? new Date(approveForm.meetingDate).toISOString() : undefined,
        boardDate: approveForm.boardDate ? new Date(approveForm.boardDate).toISOString() : undefined,
        shareholdersDate: approveForm.shareholdersDate ? new Date(approveForm.shareholdersDate).toISOString() : undefined
      })

      setShowApproveModal(false)
      setTargetTx(null)
      await fetchData()
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Failed to approve transaction.'
      alert(msg)
    } finally {
      setLoading(false)
    }
  }

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
            <div className="p-2.5 bg-rose-50 border border-rose-200 rounded-lg text-rose-700">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
                <circle cx="9" cy="7" r="4"></circle>
                <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
                <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
              </svg>
            </div>
            <div>
              <h1 className="text-2xl font-bold text-gray-900 tracking-tight">
                Related Party Transactions & Section 188 / AS 18 Desk
              </h1>
              <p className="text-sm text-gray-600">
                Companies Act Sec 188 • Rule 15 Thresholds • Arm's Length Pricing • Form MBP-4 Register • Ind AS 24 Disclosures
              </p>
            </div>
          </div>
        </div>

        {/* Global Controls */}
        <div className="flex flex-wrap items-center gap-3">
          <select
            value={selectedClientId}
            onChange={(e) => setSelectedClientId(e.target.value)}
            className="px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white font-medium text-gray-700 shadow-sm focus:outline-none focus:ring-2 focus:ring-rose-500"
          >
            {clients.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.taxIds?.['PAN'] || 'No PAN'})
              </option>
            ))}
          </select>

          <select
            value={selectedFy}
            onChange={(e) => setSelectedFy(e.target.value)}
            className="px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white font-medium text-gray-700 shadow-sm"
          >
            <option value="2024-25">FY 2024-25</option>
            <option value="2023-24">FY 2023-24</option>
            <option value="2022-23">FY 2022-23</option>
          </select>

          <button
            onClick={() => setShowEntityModal(true)}
            className="inline-flex items-center gap-1.5 px-3.5 py-2 border border-gray-300 bg-white hover:bg-gray-50 text-gray-700 text-xs font-semibold rounded-lg shadow-sm"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="12" y1="5" x2="12" y2="19"></line>
              <line x1="5" y1="12" x2="19" y2="12"></line>
            </svg>
            Add Related Party
          </button>

          <button
            onClick={() => setShowTxModal(true)}
            className="inline-flex items-center gap-1.5 px-4 py-2 bg-rose-600 hover:bg-rose-700 text-white text-xs font-semibold rounded-lg shadow"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="12" y1="5" x2="12" y2="19"></line>
              <line x1="5" y1="12" x2="19" y2="12"></line>
            </svg>
            Record RPT
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
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Total RPT Volume</p>
              <h3 className="text-2xl font-bold text-gray-900 mt-1">₹{summary.totalVolumeInr.toLocaleString('en-IN')}</h3>
              <p className="text-xs text-gray-500 mt-1">{summary.totalTransactions} transactions • {summary.totalParties} parties</p>
            </div>
            <div className="p-3 bg-rose-50 text-rose-600 rounded-lg">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <line x1="12" y1="1" x2="12" y2="23"></line>
                <path d="M17 5H9.5a3.5 3.5 0 0 0 0 7h5a3.5 3.5 0 0 1 0 7H6"></path>
              </svg>
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Arm's Length Compliance</p>
              <h3 className="text-2xl font-bold text-emerald-700 mt-1">{summary.armLengthComplianceRate}%</h3>
              <p className="text-xs text-emerald-600 mt-1">Benchmarked under TP methods</p>
            </div>
            <div className="p-3 bg-emerald-50 text-emerald-600 rounded-lg">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
                <polyline points="22 4 12 14.01 9 11.01"></polyline>
              </svg>
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Shareholder Resolution</p>
              <h3 className={`text-2xl font-bold mt-1 ${summary.specialResolutionRequiredCount > 0 ? 'text-amber-600' : 'text-gray-900'}`}>
                {summary.specialResolutionRequiredCount} Required
              </h3>
              <p className="text-xs text-gray-500 mt-1">Rule 15 10% threshold alerts</p>
            </div>
            <div className="p-3 bg-amber-50 text-amber-600 rounded-lg">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
                <line x1="12" y1="9" x2="12" y2="13"></line>
                <line x1="12" y1="17" x2="12.01" y2="17"></line>
              </svg>
            </div>
          </div>

          <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm flex items-center justify-between">
            <div>
              <p className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Sec 40A(2)(b) Risk Flags</p>
              <h3 className={`text-2xl font-bold mt-1 ${summary.sec40A2bRiskCount > 0 ? 'text-red-600' : 'text-gray-900'}`}>
                {summary.sec40A2bRiskCount} Flagged
              </h3>
              <p className="text-xs text-gray-500 mt-1">Year-End Balances: ₹{summary.totalYearEndBalancesInr.toLocaleString('en-IN')}</p>
            </div>
            <div className="p-3 bg-red-50 text-red-600 rounded-lg">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                <circle cx="12" cy="12" r="10"></circle>
                <line x1="12" y1="8" x2="12" y2="12"></line>
                <line x1="12" y1="16" x2="12.01" y2="16"></line>
              </svg>
            </div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div className="border-b border-gray-200 bg-white rounded-t-xl px-4 flex items-center justify-between shadow-sm">
        <div className="flex space-x-2">
          <button
            onClick={() => setActiveTab('transactions')}
            className={`py-3.5 px-4 font-semibold text-xs border-b-2 transition-colors ${
              activeTab === 'transactions'
                ? 'border-rose-600 text-rose-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            Transactions & Sec 188 Workbench ({transactions.length})
          </button>
          <button
            onClick={() => setActiveTab('entities')}
            className={`py-3.5 px-4 font-semibold text-xs border-b-2 transition-colors ${
              activeTab === 'entities'
                ? 'border-rose-600 text-rose-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            Related Parties Master ({entities.length})
          </button>
          <button
            onClick={() => setActiveTab('omnibus')}
            className={`py-3.5 px-4 font-semibold text-xs border-b-2 transition-colors ${
              activeTab === 'omnibus'
                ? 'border-rose-600 text-rose-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            Sec 177 Omnibus Approvals ({omnibusList.length})
          </button>
          <button
            onClick={() => setActiveTab('reports')}
            className={`py-3.5 px-4 font-semibold text-xs border-b-2 transition-colors ${
              activeTab === 'reports'
                ? 'border-rose-600 text-rose-600'
                : 'border-transparent text-gray-500 hover:text-gray-700'
            }`}
          >
            Statutory Reports (MBP-4 / AS 18)
          </button>
        </div>

        {activeTab === 'transactions' && (
          <div className="relative w-64">
            <input
              type="text"
              placeholder="Search transactions..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full px-3 py-1.5 border border-gray-300 rounded-md text-xs"
            />
          </div>
        )}
      </div>

      {/* TAB 1: TRANSACTIONS & SECTION 188 WORKBENCH */}
      {activeTab === 'transactions' && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          {loading && transactions.length === 0 ? (
            <div className="p-8 text-center text-sm text-gray-500">Loading transactions...</div>
          ) : filteredTxs.length === 0 ? (
            <div className="p-8 text-center space-y-2">
              <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1" strokeLinecap="round" strokeLinejoin="round" className="text-gray-300 mx-auto">
                <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"></path>
                <circle cx="9" cy="7" r="4"></circle>
                <path d="M23 21v-2a4 4 0 0 0-3-3.87"></path>
                <path d="M16 3.13a4 4 0 0 1 0 7.75"></path>
              </svg>
              <p className="text-gray-600 font-medium text-sm">No related party transactions found for this financial year.</p>
              <p className="text-xs text-gray-400">Click "Record RPT" to evaluate contracts against Section 188 & transfer pricing.</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="bg-gray-50/75 border-b border-gray-200 text-gray-600 font-semibold uppercase tracking-wider">
                  <tr>
                    <th className="py-3 px-4">Date / Ref</th>
                    <th className="py-3 px-4">Related Party</th>
                    <th className="py-3 px-4">Type</th>
                    <th className="py-3 px-4 text-right">Amount (INR)</th>
                    <th className="py-3 px-4">Rule 15 Threshold</th>
                    <th className="py-3 px-4">Arm's Length (TP)</th>
                    <th className="py-3 px-4">Approvals</th>
                    <th className="py-3 px-4 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-100">
                  {filteredTxs.map((t) => (
                    <tr key={t.id} className="hover:bg-rose-50/30 transition-colors">
                      <td className="py-3 px-4">
                        <span className="font-semibold text-gray-900 block">{t.contractReference}</span>
                        <span className="text-[11px] text-gray-500">{new Date(t.transactionDate).toLocaleDateString()}</span>
                      </td>
                      <td className="py-3 px-4">
                        <span className="font-medium text-gray-800 block">{t.relatedPartyName}</span>
                        <span className="text-[10px] text-rose-700 bg-rose-50 px-1.5 py-0.2 rounded border border-rose-200">
                          {t.relationshipType}
                        </span>
                      </td>
                      <td className="py-3 px-4">
                        <span className="font-medium text-gray-700">{t.transactionType}</span>
                        {t.description && <p className="text-[11px] text-gray-500 italic truncate max-w-xs">{t.description}</p>}
                      </td>
                      <td className="py-3 px-4 text-right font-bold text-gray-900">
                        ₹{t.transactionAmount.toLocaleString('en-IN')}
                      </td>
                      <td className="py-3 px-4">
                        {t.exceedsStatutoryThreshold ? (
                          <div>
                            <span className="text-[10px] font-bold px-2 py-0.5 bg-amber-100 text-amber-800 rounded-full border border-amber-200">
                              Exceeds 10% ({t.thresholdPercentage}%)
                            </span>
                            <span className="text-[10px] text-gray-500 block mt-0.5">
                              Limit: ₹{t.thresholdLimitAmount.toLocaleString('en-IN')}
                            </span>
                          </div>
                        ) : (
                          <span className="text-[10px] font-semibold px-2 py-0.5 bg-gray-100 text-gray-700 rounded-full">
                            Within Limits
                          </span>
                        )}
                      </td>
                      <td className="py-3 px-4">
                        <div className="space-y-0.5">
                          {t.isArmLength ? (
                            <span className="text-[10px] font-bold px-2 py-0.5 bg-emerald-100 text-emerald-800 rounded-full">
                              ✓ Arm's Length ({t.armLengthMethod})
                            </span>
                          ) : (
                            <span className="text-[10px] font-bold px-2 py-0.5 bg-red-100 text-red-800 rounded-full">
                              ✗ Variance {t.priceVariancePercentage}%
                            </span>
                          )}
                          {t.isOrdinaryCourseOfBusiness && t.isArmLength && (
                            <span className="text-[9px] text-emerald-700 font-semibold block">
                              4th Proviso Exempt
                            </span>
                          )}
                          {t.section40A2bRisk && (
                            <span className="text-[9px] text-red-600 font-bold block">
                              ⚠ Sec 40A(2)(b) Risk
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="py-3 px-4">
                        <div className="space-y-1">
                          <span className={`text-[10px] font-semibold px-2 py-0.5 rounded-full ${
                            t.auditCommitteeApprovalStatus === 'Approved' || t.auditCommitteeApprovalStatus === 'OmnibusApproved'
                              ? 'bg-blue-100 text-blue-800'
                              : 'bg-amber-100 text-amber-800'
                          }`}>
                            AC: {t.auditCommitteeApprovalStatus}
                          </span>
                          {t.requiresShareholderApproval && (
                            <span className="text-[9px] font-bold text-amber-700 block">
                              Special Resolution Req.
                            </span>
                          )}
                        </div>
                      </td>
                      <td className="py-3 px-4 text-right">
                        <button
                          onClick={() => {
                            setTargetTx(t)
                            setShowApproveModal(true)
                          }}
                          className="px-2.5 py-1 bg-gray-100 hover:bg-gray-200 text-gray-700 rounded text-xs font-semibold"
                        >
                          Approve
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {/* TAB 2: RELATED PARTIES MASTER REGISTER */}
      {activeTab === 'entities' && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="p-4 border-b border-gray-200 flex justify-between items-center bg-gray-50/50">
            <div>
              <h3 className="text-sm font-bold text-gray-900">Related Parties & Directors' Interest Register</h3>
              <p className="text-xs text-gray-500">Master database of holding, subsidiaries, associates, KMP, and relatives</p>
            </div>
            <button
              onClick={() => setShowEntityModal(true)}
              className="px-3 py-1.5 bg-rose-600 hover:bg-rose-700 text-white rounded text-xs font-semibold shadow"
            >
              + Add Related Party
            </button>
          </div>

          <div className="divide-y divide-gray-100">
            {entities.map((e) => (
              <div key={e.id} className="p-4 flex items-center justify-between hover:bg-gray-50/50">
                <div className="space-y-1">
                  <div className="flex items-center gap-2">
                    <span className="font-bold text-sm text-gray-900">{e.name}</span>
                    <span className="text-[10px] font-semibold px-2 py-0.5 bg-rose-50 text-rose-700 rounded-full border border-rose-200">
                      {e.relationshipType}
                    </span>
                    {e.pan && <span className="text-[11px] font-mono text-gray-500">PAN: {e.pan}</span>}
                    {e.cin && <span className="text-[11px] font-mono text-gray-500">CIN: {e.cin}</span>}
                  </div>
                  <p className="text-xs text-gray-600">
                    Interested Director / KMP: <strong>{e.directorOrKmpName || 'N/A'}</strong> • Nature of Interest: {e.natureOfInterest || 'N/A'}
                  </p>
                </div>
                <div className="text-right">
                  <span className="text-xs font-semibold text-gray-700 block">
                    {e.transactionCount} transactions recorded
                  </span>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* TAB 3: OMNIBUS APPROVALS */}
      {activeTab === 'omnibus' && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
          <div className="p-4 border-b border-gray-200 flex justify-between items-center bg-gray-50/50">
            <div>
              <h3 className="text-sm font-bold text-gray-900">Audit Committee Omnibus Approvals (Section 177)</h3>
              <p className="text-xs text-gray-500">Pre-approved annual limits for repetitive ordinary-course related party transactions</p>
            </div>
            <button
              onClick={() => setShowOmnibusModal(true)}
              className="px-3 py-1.5 bg-rose-600 hover:bg-rose-700 text-white rounded text-xs font-semibold shadow"
            >
              + New Omnibus Approval
            </button>
          </div>

          <div className="divide-y divide-gray-100">
            {omnibusList.length === 0 ? (
              <div className="p-8 text-center text-sm text-gray-500">No omnibus approvals registered for this financial year.</div>
            ) : (
              omnibusList.map((o) => {
                const pct = o.maximumApprovedLimit > 0 ? Math.min(100, Math.round((o.utilizedAmount / o.maximumApprovedLimit) * 100)) : 0
                return (
                  <div key={o.id} className="p-4 space-y-2">
                    <div className="flex items-center justify-between">
                      <div>
                        <h4 className="text-sm font-bold text-gray-900">{o.title}</h4>
                        <p className="text-xs text-gray-500">
                          Approved in AC Meeting: {new Date(o.auditCommitteeMeetingDate).toLocaleDateString()} • Valid through {new Date(o.validityPeriodTo).toLocaleDateString()}
                        </p>
                      </div>
                      <div className="text-right">
                        <span className="text-xs font-bold text-gray-900 block">
                          ₹{o.utilizedAmount.toLocaleString('en-IN')} / ₹{o.maximumApprovedLimit.toLocaleString('en-IN')}
                        </span>
                        <span className="text-[10px] text-gray-500 font-semibold">{pct}% Utilized</span>
                      </div>
                    </div>

                    {/* Progress Bar */}
                    <div className="w-full bg-gray-100 h-2 rounded-full overflow-hidden">
                      <div
                        className={`h-full rounded-full ${pct > 90 ? 'bg-red-500' : 'bg-emerald-500'}`}
                        style={{ width: `${pct}%` }}
                      ></div>
                    </div>

                    {o.approvalCriteria && (
                      <p className="text-xs text-gray-600 bg-gray-50 p-2 rounded">
                        Criteria: {o.approvalCriteria}
                      </p>
                    )}
                  </div>
                )
              })
            )}
          </div>
        </div>
      )}

      {/* TAB 4: STATUTORY REPORTS (MBP-4 & AS 18) */}
      {activeTab === 'reports' && (
        <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
          <div className="flex items-center justify-between border-b pb-3">
            <div className="flex space-x-2">
              <button
                onClick={() => setReportSubTab('mbp4')}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-colors ${
                  reportSubTab === 'mbp4' ? 'bg-rose-600 text-white' : 'bg-gray-100 text-gray-700'
                }`}
              >
                Form MBP-4 Register (Section 189)
              </button>
              <button
                onClick={() => setReportSubTab('as18')}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold transition-colors ${
                  reportSubTab === 'as18' ? 'bg-rose-600 text-white' : 'bg-gray-100 text-gray-700'
                }`}
              >
                AS 18 / Ind AS 24 Notes to Accounts
              </button>
            </div>

            <div className="flex gap-2">
              <button
                onClick={() => copyToClipboard(reportSubTab === 'mbp4' ? mbp4Text : as18Text, reportSubTab.toUpperCase())}
                className="px-3 py-1.5 border border-gray-300 rounded text-xs font-semibold hover:bg-gray-50"
              >
                Copy
              </button>
              <a
                href={`data:text/plain;charset=utf-8,${encodeURIComponent(reportSubTab === 'mbp4' ? mbp4Text : as18Text)}`}
                download={`${reportSubTab.toUpperCase()}_Report_${selectedFy}.txt`}
                className="px-3 py-1.5 bg-gray-900 text-white rounded text-xs font-semibold hover:bg-gray-800"
              >
                Download
              </a>
            </div>
          </div>

          <div className="bg-slate-900 text-slate-100 p-5 rounded-xl font-mono text-xs overflow-x-auto whitespace-pre leading-relaxed shadow-inner max-h-[550px]">
            {reportSubTab === 'mbp4' ? mbp4Text || 'Form MBP-4 not yet generated.' : as18Text || 'AS 18 Disclosure note not yet generated.'}
          </div>
        </div>
      )}

      {/* MODAL 1: ADD RELATED PARTY ENTITY */}
      {showEntityModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-2xl border border-gray-200 space-y-4">
            <h3 className="text-base font-bold text-gray-900">Add Related Party Entity</h3>
            <form onSubmit={handleEntitySubmit} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Entity / Person Name *</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Apex Technologies Pvt Ltd"
                  value={entityForm.name || ''}
                  onChange={(e) => setEntityForm({ ...entityForm, name: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Relationship Category *</label>
                <select
                  value={entityForm.relationshipType || 'Subsidiary'}
                  onChange={(e) => setEntityForm({ ...entityForm, relationshipType: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                >
                  {RELATIONSHIP_TYPES.map((r) => (
                    <option key={r} value={r}>{r}</option>
                  ))}
                </select>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">PAN</label>
                  <input
                    type="text"
                    maxLength={10}
                    value={entityForm.pan || ''}
                    onChange={(e) => setEntityForm({ ...entityForm, pan: e.target.value.toUpperCase() })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm font-mono uppercase"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">CIN (If Company)</label>
                  <input
                    type="text"
                    value={entityForm.cin || ''}
                    onChange={(e) => setEntityForm({ ...entityForm, cin: e.target.value.toUpperCase() })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm font-mono uppercase"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Interested Director / KMP Name</label>
                <input
                  type="text"
                  placeholder="e.g. Sunil K. Deshmukh"
                  value={entityForm.directorOrKmpName || ''}
                  onChange={(e) => setEntityForm({ ...entityForm, directorOrKmpName: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Nature of Concern / Interest</label>
                <input
                  type="text"
                  placeholder="e.g. Director and 60% equity shareholder"
                  value={entityForm.natureOfInterest || ''}
                  onChange={(e) => setEntityForm({ ...entityForm, natureOfInterest: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowEntityModal(false)}
                  className="px-3 py-1.5 border border-gray-300 rounded text-xs text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-1.5 bg-rose-600 hover:bg-rose-700 text-white rounded text-xs font-semibold"
                >
                  Save Party
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: RECORD TRANSACTION */}
      {showTxModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm overflow-y-auto">
          <div className="bg-white rounded-xl max-w-2xl w-full max-h-[90vh] overflow-y-auto p-6 shadow-2xl border border-gray-200 space-y-4">
            <h3 className="text-base font-bold text-gray-900">Record Related Party Transaction</h3>
            <form onSubmit={handleTxSubmit} className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Related Party *</label>
                  <select
                    value={txForm.relatedPartyId || ''}
                    onChange={(e) => setTxForm({ ...txForm, relatedPartyId: e.target.value })}
                    required
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                  >
                    <option value="">-- Choose Related Party --</option>
                    {entities.map((ent) => (
                      <option key={ent.id} value={ent.id}>
                        {ent.name} ({ent.relationshipType})
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Transaction Type *</label>
                  <select
                    value={txForm.transactionType || 'SaleOfGoods'}
                    onChange={(e) => setTxForm({ ...txForm, transactionType: e.target.value })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                  >
                    {TRANSACTION_TYPES.map((t) => (
                      <option key={t.type} value={t.type}>{t.label}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Transaction Amount (INR) *</label>
                  <input
                    type="number"
                    step="0.01"
                    required
                    value={txForm.transactionAmount || ''}
                    onChange={(e) => setTxForm({ ...txForm, transactionAmount: parseFloat(e.target.value) || 0 })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm font-bold text-gray-900"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Annual Turnover Benchmark (INR)</label>
                  <input
                    type="number"
                    value={txForm.annualTurnoverBenchmark || ''}
                    onChange={(e) => setTxForm({ ...txForm, annualTurnoverBenchmark: parseFloat(e.target.value) || 0 })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Net Worth Benchmark (INR)</label>
                  <input
                    type="number"
                    value={txForm.netWorthBenchmark || ''}
                    onChange={(e) => setTxForm({ ...txForm, netWorthBenchmark: parseFloat(e.target.value) || 0 })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Transfer Pricing Method</label>
                  <select
                    value={txForm.armLengthMethod || 'CUP'}
                    onChange={(e) => setTxForm({ ...txForm, armLengthMethod: e.target.value })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                  >
                    {TP_METHODS.map((m) => (
                      <option key={m} value={m}>{m}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Benchmark Arm's Length Price</label>
                  <input
                    type="number"
                    step="0.01"
                    value={txForm.benchmarkArmLengthPrice || ''}
                    onChange={(e) => setTxForm({ ...txForm, benchmarkArmLengthPrice: parseFloat(e.target.value) || 0 })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="flex items-center gap-4 pt-2">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={txForm.isOrdinaryCourseOfBusiness ?? true}
                    onChange={(e) => setTxForm({ ...txForm, isOrdinaryCourseOfBusiness: e.target.checked })}
                    className="rounded text-rose-600"
                  />
                  <span className="text-xs font-medium text-gray-800">In Ordinary Course of Business</span>
                </label>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Year-End Balance (INR)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={txForm.outstandingBalanceAtYearEnd || ''}
                    onChange={(e) => setTxForm({ ...txForm, outstandingBalanceAtYearEnd: parseFloat(e.target.value) || 0 })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Balance Type</label>
                  <select
                    value={txForm.balanceType || 'Debit'}
                    onChange={(e) => setTxForm({ ...txForm, balanceType: e.target.value })}
                    className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                  >
                    <option value="Debit">Debit (Receivable)</option>
                    <option value="Credit">Credit (Payable)</option>
                    <option value="Nil">Nil</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Description / Particulars</label>
                <textarea
                  rows={2}
                  value={txForm.description || ''}
                  onChange={(e) => setTxForm({ ...txForm, description: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowTxModal(false)}
                  className="px-3 py-1.5 border border-gray-300 rounded text-xs text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-1.5 bg-rose-600 hover:bg-rose-700 text-white rounded text-xs font-semibold"
                >
                  Save & Evaluate
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: OMNIBUS APPROVAL */}
      {showOmnibusModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-2xl border border-gray-200 space-y-4">
            <h3 className="text-base font-bold text-gray-900">New Audit Committee Omnibus Approval</h3>
            <form onSubmit={handleOmnibusSubmit} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Approval Title *</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Component Purchases FY 2024-25"
                  value={omniForm.title || ''}
                  onChange={(e) => setOmniForm({ ...omniForm, title: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                />
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Maximum Approved Limit (INR) *</label>
                <input
                  type="number"
                  required
                  value={omniForm.maximumApprovedLimit || ''}
                  onChange={(e) => setOmniForm({ ...omniForm, maximumApprovedLimit: parseFloat(e.target.value) || 0 })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm font-bold"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">AC Meeting Date</label>
                  <input
                    type="date"
                    value={omniForm.auditCommitteeMeetingDate || ''}
                    onChange={(e) => setOmniForm({ ...omniForm, auditCommitteeMeetingDate: e.target.value })}
                    className="w-full px-3 py-1.5 border border-gray-300 rounded text-xs"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Valid Through</label>
                  <input
                    type="date"
                    value={omniForm.validityPeriodTo || ''}
                    onChange={(e) => setOmniForm({ ...omniForm, validityPeriodTo: e.target.value })}
                    className="w-full px-3 py-1.5 border border-gray-300 rounded text-xs"
                  />
                </div>
              </div>
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Approval Criteria & Conditions</label>
                <textarea
                  rows={2}
                  value={omniForm.approvalCriteria || ''}
                  onChange={(e) => setOmniForm({ ...omniForm, approvalCriteria: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm"
                />
              </div>
              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowOmnibusModal(false)}
                  className="px-3 py-1.5 border border-gray-300 rounded text-xs text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-1.5 bg-rose-600 hover:bg-rose-700 text-white rounded text-xs font-semibold"
                >
                  Save Omnibus Approval
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: APPROVE TRANSACTION */}
      {showApproveModal && targetTx && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4 backdrop-blur-sm">
          <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-2xl border border-gray-200 space-y-4">
            <h3 className="text-base font-bold text-gray-900">Approve Transaction {targetTx.contractReference}</h3>
            <p className="text-xs text-gray-500">
              Record Audit Committee, Board, and Shareholder approvals pursuant to Sections 177 and 188.
            </p>

            <form onSubmit={handleApproveSubmit} className="space-y-3">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Approval Status</label>
                <select
                  value={approveForm.approvalStatus}
                  onChange={(e) => setApproveForm({ ...approveForm, approvalStatus: e.target.value })}
                  className="w-full px-3 py-2 border border-gray-300 rounded-lg text-sm bg-white"
                >
                  <option value="Approved">Approved (Audit Committee)</option>
                  <option value="OmnibusApproved">Omnibus Approved</option>
                  <option value="Exempt">Exempt</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Audit Committee Meeting Date</label>
                <input
                  type="date"
                  value={approveForm.meetingDate}
                  onChange={(e) => setApproveForm({ ...approveForm, meetingDate: e.target.value })}
                  className="w-full px-3 py-1.5 border border-gray-300 rounded text-xs"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Board Meeting Resolution Date</label>
                <input
                  type="date"
                  value={approveForm.boardDate}
                  onChange={(e) => setApproveForm({ ...approveForm, boardDate: e.target.value })}
                  className="w-full px-3 py-1.5 border border-gray-300 rounded text-xs"
                />
              </div>

              {targetTx.requiresShareholderApproval && (
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">
                    Special Resolution Date (General Meeting) *
                  </label>
                  <input
                    type="date"
                    required
                    value={approveForm.shareholdersDate}
                    onChange={(e) => setApproveForm({ ...approveForm, shareholdersDate: e.target.value })}
                    className="w-full px-3 py-1.5 border border-amber-300 bg-amber-50 rounded text-xs"
                  />
                  <span className="text-[10px] text-amber-700">Mandatory under Rule 15 threshold.</span>
                </div>
              )}

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowApproveModal(false)}
                  className="px-3 py-1.5 border border-gray-300 rounded text-xs text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-1.5 bg-rose-600 hover:bg-rose-700 text-white rounded text-xs font-semibold"
                >
                  Save Approvals
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
