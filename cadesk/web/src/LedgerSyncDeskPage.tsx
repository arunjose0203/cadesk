import { useState, useEffect, useMemo, type FormEvent } from 'react'
import {
  ledgerSyncApi,
  bankFeedApi,
  api,
  type ErpSyncAgentConfigDto,
  type CreateOrUpdateAgentConfigRequest,
  type ErpLedgerVoucherDto,
  type ErpOutgoingVoucherDto,
  type CreateOutgoingVoucherRequest,
  type BankLedgerReconciliationMatchDto,
  type ReconciliationSummaryDto,
  type ErpType,
  type ErpVoucherType,
  type AuditReasonCategory,
  type ReconciliationMatchRule,
  type BankAccountDto,
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
    maximumFractionDigits: 2
  }).format(val)
}

export function LedgerSyncDeskPage({ can: _can }: { can?: (p: string) => boolean }) {
  const [activeTab, setActiveTab] = useState<'agents' | 'reconciliation' | 'outgoing'>('reconciliation')
  const [loading, setLoading] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  // Context Data
  const [clients, setClients] = useState<Client[]>([])
  const [bankAccounts, setBankAccounts] = useState<BankAccountDto[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedBankAccountId, setSelectedBankAccountId] = useState<string>('')

  // Agents & Vouchers State
  const [agents, setAgents] = useState<ErpSyncAgentConfigDto[]>([])
  const [vouchers, setVouchers] = useState<ErpLedgerVoucherDto[]>([])
  const [outgoingVouchers, setOutgoingVouchers] = useState<ErpOutgoingVoucherDto[]>([])
  const [voucherSearch, setVoucherSearch] = useState<string>('')
  const [voucherTypeFilter, setVoucherTypeFilter] = useState<string>('All')

  // Auto-Reconciliation State
  const [matches, setMatches] = useState<BankLedgerReconciliationMatchDto[]>([])
  const [summary, setSummary] = useState<ReconciliationSummaryDto | null>(null)
  const [reconciling, setReconciling] = useState(false)
  const [matchStatusFilter, setMatchStatusFilter] = useState<string>('All')

  // Modals State
  const [showAgentModal, setShowAgentModal] = useState(false)
  const [showOutgoingModal, setShowOutgoingModal] = useState(false)
  const [selectedXmlPayload, setSelectedXmlPayload] = useState<string | null>(null)
  const [copiedToken, setCopiedToken] = useState<string | null>(null)

  // Agent Form
  const [agentForm, setAgentForm] = useState<CreateOrUpdateAgentConfigRequest>({
    clientId: '',
    agentName: '',
    erpType: 'TallyPrime',
    hostUrl: 'http://localhost:9000',
    companyName: '',
    syncFrequencyMinutes: 15,
    syncDaybook: true,
    syncMasters: true,
    syncBankFeeds: true
  })

  // Outgoing Voucher Form
  const [outgoingForm, setOutgoingForm] = useState<CreateOutgoingVoucherRequest>({
    clientId: '',
    agentConfigId: '',
    voucherType: 'Journal',
    voucherDate: new Date().toISOString().split('T')[0],
    referenceNumber: `AUD-ADJ-${new Date().getFullYear()}-001`,
    debitLedger: 'Depreciation on Plant & Machinery',
    creditLedger: 'Accumulated Depreciation',
    amount: 50000,
    narration: 'Statutory depreciation adjustment as per IT Act Sec 32',
    auditReason: 'DepreciationSec32'
  })

  // Initial Load
  useEffect(() => {
    loadClients()
    loadBankAccounts()
  }, [])

  // Reload tab data when client changes
  useEffect(() => {
    loadAgents()
    loadVouchers()
    loadOutgoingVouchers()
    if (selectedClientId && selectedBankAccountId) {
      loadReconciliation()
    }
  }, [selectedClientId, selectedBankAccountId])

  const loadClients = async () => {
    try {
      const res = await api<ClientPage>('/api/clients?take=100')
      setClients(res.items || [])
      if (res.items && res.items.length > 0 && !selectedClientId) {
        setSelectedClientId(res.items[0].id)
        setAgentForm(prev => ({ ...prev, clientId: res.items[0].id }))
        setOutgoingForm(prev => ({ ...prev, clientId: res.items[0].id }))
      }
    } catch (err: unknown) {
      console.error('Failed to load clients', err)
    }
  }

  const loadBankAccounts = async () => {
    try {
      const res = await bankFeedApi.getAccounts()
      setBankAccounts(res || [])
      if (res && res.length > 0 && !selectedBankAccountId) {
        setSelectedBankAccountId(res[0].id)
      }
    } catch (err: unknown) {
      console.error('Failed to load bank accounts', err)
    }
  }

  const loadAgents = async () => {
    try {
      const res = await ledgerSyncApi.getAgentConfigs(selectedClientId || undefined)
      setAgents(res || [])
      if (res && res.length > 0 && !outgoingForm.agentConfigId) {
        setOutgoingForm(prev => ({ ...prev, agentConfigId: res[0].id }))
      }
    } catch (err: unknown) {
      console.error('Failed to load agent configs', err)
    }
  }

  const loadVouchers = async () => {
    try {
      const res = await ledgerSyncApi.getVouchers(selectedClientId || undefined)
      setVouchers(res || [])
    } catch (err: unknown) {
      console.error('Failed to load vouchers', err)
    }
  }

  const loadOutgoingVouchers = async () => {
    try {
      const res = await ledgerSyncApi.getOutgoingVouchers(selectedClientId || undefined)
      setOutgoingVouchers(res || [])
    } catch (err: unknown) {
      console.error('Failed to load outgoing vouchers', err)
    }
  }

  const loadReconciliation = async () => {
    if (!selectedClientId || !selectedBankAccountId) return
    try {
      const [matchesRes, summaryRes] = await Promise.all([
        ledgerSyncApi.getReconciliationMatches(selectedClientId, selectedBankAccountId),
        ledgerSyncApi.getReconciliationSummary(selectedClientId, selectedBankAccountId)
      ])
      setMatches(matchesRes || [])
      setSummary(summaryRes)
    } catch (err: unknown) {
      console.error('Failed to load reconciliation matches', err)
    }
  }

  const handleRunReconciliation = async () => {
    if (!selectedClientId || !selectedBankAccountId) {
      setErrorMsg('Please select a Client and Bank Account first')
      return
    }
    setReconciling(true)
    setErrorMsg(null)
    setNotice(null)
    try {
      const res = await ledgerSyncApi.runAutoReconciliation({
        clientId: selectedClientId,
        bankAccountId: selectedBankAccountId
      })
      setMatches(res || [])
      await loadReconciliation()
      setNotice(`AI Matching Complete: Generated ${res.length} suggested auto-reconciliation links.`)
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to run auto-reconciliation')
    } finally {
      setReconciling(false)
    }
  }

  const handleReconciliationAction = async (matchId: string, status: 'Approved' | 'Rejected') => {
    try {
      await ledgerSyncApi.processReconciliationAction(matchId, { status })
      setNotice(`Match ${status.toLowerCase()} successfully.`)
      await loadReconciliation()
      await loadVouchers()
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Action failed')
    }
  }

  const handleSaveAgent = async (e: FormEvent) => {
    e.preventDefault()
    if (!agentForm.clientId || !agentForm.agentName || !agentForm.companyName) {
      setErrorMsg('Please provide Agent Name, Company Name and Client')
      return
    }
    setLoading(true)
    setErrorMsg(null)
    try {
      const res = await ledgerSyncApi.saveAgentConfig(agentForm)
      setNotice(`Sync Agent "${res.agentName}" registered with token ${res.agentToken}`)
      setShowAgentModal(false)
      await loadAgents()
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to save agent config')
    } finally {
      setLoading(false)
    }
  }

  const handleDeleteAgent = async (id: string) => {
    if (!confirm('Are you sure you want to delete this Sync Agent configuration?')) return
    try {
      await ledgerSyncApi.deleteAgentConfig(id)
      setNotice('Agent configuration deleted.')
      await loadAgents()
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to delete agent')
    }
  }

  const handleCreateOutgoingVoucher = async (e: FormEvent) => {
    e.preventDefault()
    if (!outgoingForm.agentConfigId || !outgoingForm.amount || !outgoingForm.debitLedger || !outgoingForm.creditLedger) {
      setErrorMsg('Please complete all required fields')
      return
    }
    setLoading(true)
    setErrorMsg(null)
    try {
      await ledgerSyncApi.createOutgoingVoucher(outgoingForm)
      setNotice(`Outgoing ${outgoingForm.voucherType} voucher queued for partner authorization.`)
      setShowOutgoingModal(false)
      await loadOutgoingVouchers()
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to create outgoing voucher')
    } finally {
      setLoading(false)
    }
  }

  const handleAuthorizeOutgoing = async (id: string) => {
    try {
      await ledgerSyncApi.authorizeOutgoingVoucher(id)
      setNotice('Voucher authorized and released to ERP sync agent polling queue.')
      await loadOutgoingVouchers()
    } catch (err: unknown) {
      setErrorMsg((err as Error).message || 'Failed to authorize voucher')
    }
  }

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text)
    setCopiedToken(text)
    setTimeout(() => setCopiedToken(null), 3000)
  }

  // Filtered Vouchers
  const filteredVouchers = useMemo(() => {
    return vouchers.filter(v => {
      if (voucherTypeFilter !== 'All' && v.voucherType !== voucherTypeFilter) return false
      if (voucherSearch) {
        const q = voucherSearch.toLowerCase()
        return (
          v.voucherNumber.toLowerCase().includes(q) ||
          v.narration.toLowerCase().includes(q) ||
          v.debitLedger.toLowerCase().includes(q) ||
          v.creditLedger.toLowerCase().includes(q) ||
          (v.chequeNumber && v.chequeNumber.toLowerCase().includes(q))
        )
      }
      return true
    })
  }, [vouchers, voucherTypeFilter, voucherSearch])

  // Filtered Matches
  const filteredMatches = useMemo(() => {
    return matches.filter(m => {
      if (matchStatusFilter !== 'All' && m.status !== matchStatusFilter) return false
      return true
    })
  }, [matches, matchStatusFilter])

  const getRuleBadge = (rule: ReconciliationMatchRule) => {
    switch (rule) {
      case 'ExactAmountAndDate':
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-emerald-100 text-emerald-800">Exact Amount & Date</span>
      case 'ChequeOrUtrMatch':
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-blue-100 text-blue-800">Cheque / UTR Narration</span>
      case 'TdsNetDeductionMatch':
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-purple-100 text-purple-800">TDS Net-of-Tax</span>
      case 'ToleranceRoundingMatch':
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-amber-100 text-amber-800">Minor Tolerance (+/- ₹10)</span>
      case 'MultiInvoiceBundleMatch':
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-indigo-100 text-indigo-800">Multi-Invoice Bundle</span>
      default:
        return <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-gray-100 text-gray-800">{rule}</span>
    }
  }

  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Connected':
      case 'Approved':
      case 'PostedToErp':
      case 'AutoMatched':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-800">● {status}</span>
      case 'Syncing':
      case 'QueuedForAgent':
      case 'Suggested':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-blue-100 text-blue-800">⏳ {status}</span>
      case 'PendingApproval':
        return <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium bg-amber-100 text-amber-800">⚠ {status}</span>
      case 'Offline':
      case 'Rejected':
      case 'Failed':
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
            <span className="text-3xl">🔄</span>
            <div>
              <h1 className="text-2xl font-bold text-gray-900">Bank & Ledger Auto-Reconciliation Engine</h1>
              <p className="text-sm text-gray-500 mt-1">
                Real-time Tally Prime & Busy Sync Agent with AI Probabilistic Discrepancy Matcher & 2-Way Audited Adjustment Posting
              </p>
            </div>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => setShowAgentModal(true)}
            className="inline-flex items-center px-3.5 py-2 border border-gray-300 text-sm font-medium rounded-lg text-gray-700 bg-white hover:bg-gray-50 shadow-sm"
          >
            🔌 + Register Sync Agent
          </button>
          <button
            onClick={() => setShowOutgoingModal(true)}
            className="inline-flex items-center px-3.5 py-2 border border-gray-300 text-sm font-medium rounded-lg text-gray-700 bg-white hover:bg-gray-50 shadow-sm"
          >
            📤 Push Audited Voucher
          </button>
          <button
            onClick={handleRunReconciliation}
            disabled={reconciling}
            className="inline-flex items-center px-4 py-2 border border-transparent text-sm font-medium rounded-lg shadow-sm text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50"
          >
            {reconciling ? 'Matching...' : '⚡ Run AI Auto-Reconciliation'}
          </button>
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

      {/* Context Selection Bar */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
        <div>
          <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1">Target Client Entity</label>
          <select
            value={selectedClientId}
            onChange={e => {
              setSelectedClientId(e.target.value)
              setAgentForm(prev => ({ ...prev, clientId: e.target.value }))
              setOutgoingForm(prev => ({ ...prev, clientId: e.target.value }))
            }}
            className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border focus:ring-indigo-500 focus:border-indigo-500"
          >
            <option value="">-- All Clients --</option>
            {clients.map(c => (
              <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-xs font-semibold text-gray-500 uppercase tracking-wider mb-1">Client Bank Account (for BRS Matching)</label>
          <select
            value={selectedBankAccountId}
            onChange={e => setSelectedBankAccountId(e.target.value)}
            className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border focus:ring-indigo-500 focus:border-indigo-500"
          >
            <option value="">-- Select Bank Account --</option>
            {bankAccounts.map(b => (
              <option key={b.id} value={b.id}>{b.bankName} - {b.accountName} ({b.accountNumber})</option>
            ))}
          </select>
        </div>
      </div>

      {/* Tab Navigation */}
      <div className="border-b border-gray-200">
        <nav className="-mb-px flex space-x-8">
          <button
            onClick={() => setActiveTab('reconciliation')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'reconciliation'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            <span>🤖</span> AI Bank Feed Auto-Reconciliation
            {matches.filter(m => m.status === 'Suggested').length > 0 && (
              <span className="ml-2 bg-indigo-100 text-indigo-700 text-xs px-2 py-0.5 rounded-full font-bold">
                {matches.filter(m => m.status === 'Suggested').length}
              </span>
            )}
          </button>
          <button
            onClick={() => setActiveTab('agents')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'agents'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            <span>🔌</span> ERP Sync Agents & Live Daybook
            <span className="ml-2 bg-gray-100 text-gray-600 text-xs px-2 py-0.5 rounded-full font-bold">
              {agents.length}
            </span>
          </button>
          <button
            onClick={() => setActiveTab('outgoing')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'outgoing'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
            }`}
          >
            <span>📤</span> 2-Way Outgoing Vouchers (Audit Adjustments)
            {outgoingVouchers.filter(o => o.postingStatus === 'PendingApproval').length > 0 && (
              <span className="ml-2 bg-amber-100 text-amber-700 text-xs px-2 py-0.5 rounded-full font-bold">
                {outgoingVouchers.filter(o => o.postingStatus === 'PendingApproval').length}
              </span>
            )}
          </button>
        </nav>
      </div>

      {/* ========================================================================= */}
      {/* TAB 1: AI Bank Feed Auto-Reconciliation Engine */}
      {/* ========================================================================= */}
      {activeTab === 'reconciliation' && (
        <div className="space-y-6">
          {/* KPI Summary Banner */}
          {summary && (
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm">
                <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Reconciled Percentage</span>
                <div className="mt-2 flex items-baseline gap-2">
                  <span className="text-3xl font-extrabold text-indigo-600">{summary.reconciledPercentage}%</span>
                  <span className="text-xs text-gray-500">({summary.reconciledCount} / {summary.totalBankTransactions} txs)</span>
                </div>
                <div className="w-full bg-gray-200 rounded-full h-2 mt-3">
                  <div className="bg-indigo-600 h-2 rounded-full" style={{ width: `${Math.min(summary.reconciledPercentage, 100)}%` }}></div>
                </div>
              </div>

              <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm">
                <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Reconciled Volume</span>
                <div className="mt-2 text-2xl font-bold text-emerald-600">
                  {formatINR(summary.totalReconciledAmount)}
                </div>
                <p className="text-xs text-gray-500 mt-1">Cleared against client ERP ledger</p>
              </div>

              <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm">
                <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Unreconciled Variance</span>
                <div className="mt-2 text-2xl font-bold text-amber-600">
                  {formatINR(summary.totalUnreconciledAmount)}
                </div>
                <p className="text-xs text-gray-500 mt-1">{summary.pendingCount} open statement entries</p>
              </div>

              <div className="bg-white p-5 rounded-xl border border-gray-200 shadow-sm">
                <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">High-Confidence Suggestions</span>
                <div className="mt-2 text-2xl font-bold text-indigo-700">
                  {summary.highConfidenceSuggestionsCount}
                </div>
                <p className="text-xs text-gray-500 mt-1">&ge; 90% AI heuristic match score</p>
              </div>
            </div>
          )}

          {/* Match Review Section */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-gray-200 flex flex-col md:flex-row md:items-center justify-between gap-3">
              <div>
                <h3 className="font-semibold text-gray-900 text-base">Probabilistic Bank-Ledger Discrepancy Matches</h3>
                <p className="text-xs text-gray-500 mt-0.5">
                  AI matches bank feeds with Tally / Busy vouchers across Exact Date/Amt, TDS Net Deductions, Cheque/UTR Narrations, Rounding & Multi-Invoice Bundles
                </p>
              </div>
              <div className="flex items-center gap-2">
                <select
                  value={matchStatusFilter}
                  onChange={e => setMatchStatusFilter(e.target.value)}
                  className="text-xs border border-gray-300 rounded-lg p-1.5 focus:ring-indigo-500 focus:border-indigo-500"
                >
                  <option value="All">All Match Statuses</option>
                  <option value="Suggested">Suggested (Pending Review)</option>
                  <option value="Approved">Approved / Reconciled</option>
                  <option value="Rejected">Rejected / Dismissed</option>
                </select>
              </div>
            </div>

            {filteredMatches.length === 0 ? (
              <div className="p-12 text-center">
                <span className="text-4xl">🤖</span>
                <h4 className="mt-3 font-medium text-gray-900 text-base">No Discrepancy Matches Found</h4>
                <p className="text-sm text-gray-500 max-w-md mx-auto mt-1">
                  Click &quot;Run AI Auto-Reconciliation&quot; to analyze unmatched bank transactions against active ERP ledger vouchers.
                </p>
                <button
                  onClick={handleRunReconciliation}
                  disabled={reconciling}
                  className="mt-4 inline-flex items-center px-4 py-2 bg-indigo-600 text-white rounded-lg text-sm font-medium hover:bg-indigo-700 shadow-sm"
                >
                  {reconciling ? 'Running AI Engine...' : '⚡ Trigger AI Reconciliation Engine'}
                </button>
              </div>
            ) : (
              <div className="divide-y divide-gray-200">
                {filteredMatches.map(match => (
                  <div key={match.id} className="p-5 hover:bg-gray-50 transition-colors">
                    <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-4">
                      {/* Left: Bank Transaction */}
                      <div className="flex-1 bg-gray-50 p-3 rounded-lg border border-gray-100">
                        <div className="flex items-center justify-between text-xs text-gray-500">
                          <span className="font-semibold text-gray-700">Bank Statement Entry</span>
                          <span>{match.transactionDate}</span>
                        </div>
                        <div className="mt-1 font-medium text-gray-900 text-sm">{match.narration}</div>
                        <div className="mt-2 flex items-center justify-between">
                          <span className={`text-xs px-2 py-0.5 rounded font-bold ${match.transactionType === 'Credit' ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'}`}>
                            {match.transactionType}
                          </span>
                          <span className="text-base font-bold text-gray-900">{formatINR(match.bankAmount)}</span>
                        </div>
                      </div>

                      {/* Center: AI Heuristic & Variance Details */}
                      <div className="flex flex-col items-center justify-center px-4 py-2 min-w-[280px] text-center">
                        <div className="flex items-center gap-2">
                          {getRuleBadge(match.matchRule)}
                          <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-bold bg-indigo-50 text-indigo-700 border border-indigo-200">
                            {match.confidenceScore}% Confidence
                          </span>
                        </div>
                        <div className="text-xs text-gray-600 mt-2 max-w-xs font-mono bg-amber-50 p-1.5 rounded border border-amber-200">
                          {match.varianceExplanation || `Variance: ${formatINR(match.varianceAmount)}`}
                        </div>
                        <div className="mt-2 text-xs text-gray-500">
                          Status: {getStatusBadge(match.status)}
                        </div>
                      </div>

                      {/* Right: Matched ERP Voucher */}
                      <div className="flex-1 bg-indigo-50/50 p-3 rounded-lg border border-indigo-100">
                        <div className="flex items-center justify-between text-xs text-indigo-800">
                          <span className="font-semibold">ERP Ledger Voucher #{match.erpVoucherNumber}</span>
                          <span>{match.voucherDate || 'Matched'}</span>
                        </div>
                        <div className="mt-1 text-xs text-gray-700">
                          <div><span className="text-gray-500 font-semibold">Dr:</span> {match.debitLedger || 'Ledger'}</div>
                          <div><span className="text-gray-500 font-semibold">Cr:</span> {match.creditLedger || 'Ledger'}</div>
                        </div>
                        <div className="mt-2 flex items-center justify-between">
                          <span className="text-xs text-gray-500">Ledger Gross Amt</span>
                          <span className="text-base font-bold text-indigo-900">{formatINR(match.ledgerAmount)}</span>
                        </div>
                      </div>

                      {/* Action Buttons */}
                      <div className="flex flex-row lg:flex-col items-center gap-2 justify-end">
                        {match.status === 'Suggested' ? (
                          <>
                            <button
                              onClick={() => handleReconciliationAction(match.id, 'Approved')}
                              className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-xs font-medium shadow-sm w-full"
                            >
                              ✓ Approve & Reconcile
                            </button>
                            <button
                              onClick={() => handleReconciliationAction(match.id, 'Rejected')}
                              className="px-3 py-1.5 bg-white hover:bg-gray-100 text-gray-700 border border-gray-300 rounded-lg text-xs font-medium shadow-sm w-full"
                            >
                              ✕ Reject / Unlink
                            </button>
                          </>
                        ) : (
                          <div className="text-xs text-gray-400 italic">
                            Actioned by {match.reconciledByName || 'Auditor'}
                          </div>
                        )}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 2: ERP Sync Agents & Live Daybook */}
      {/* ========================================================================= */}
      {activeTab === 'agents' && (
        <div className="space-y-6">
          {/* Active Agents Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
            {agents.map(agent => (
              <div key={agent.id} className="bg-white rounded-xl border border-gray-200 p-5 shadow-sm space-y-4">
                <div className="flex items-start justify-between">
                  <div className="flex items-center gap-3">
                    <span className="text-2xl">{agent.erpType === 'TallyPrime' ? '📊' : '💼'}</span>
                    <div>
                      <h4 className="font-bold text-gray-900 text-sm">{agent.agentName}</h4>
                      <p className="text-xs text-gray-500">{agent.companyName} ({agent.erpType})</p>
                    </div>
                  </div>
                  {getStatusBadge(agent.syncStatus)}
                </div>

                <div className="space-y-1.5 text-xs text-gray-600 bg-gray-50 p-3 rounded-lg border border-gray-100">
                  <div className="flex justify-between">
                    <span className="text-gray-500">Host URL:</span>
                    <span className="font-mono font-medium text-gray-800">{agent.hostUrl}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-500">Client Entity:</span>
                    <span className="font-medium text-gray-800">{agent.clientName}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-500">Frequency:</span>
                    <span className="text-gray-800">Every {agent.syncFrequencyMinutes} mins</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-500">Total Synced Vouchers:</span>
                    <span className="font-bold text-indigo-600">{agent.totalVouchersSynced.toLocaleString()}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-500">Last Sync:</span>
                    <span className="text-gray-700">{agent.lastSyncAt ? new Date(agent.lastSyncAt).toLocaleTimeString() : 'Never'}</span>
                  </div>
                </div>

                {/* Agent Secret Token & Actions */}
                <div className="pt-2 border-t border-gray-100 flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <button
                      onClick={() => copyToClipboard(agent.agentToken)}
                      className="inline-flex items-center gap-1 text-xs text-indigo-600 hover:text-indigo-800 font-medium"
                    >
                      📋 {copiedToken === agent.agentToken ? 'Copied Token!' : 'Copy Agent Token'}
                    </button>
                  </div>
                  <button
                    onClick={() => handleDeleteAgent(agent.id)}
                    className="text-xs text-rose-600 hover:text-rose-800"
                  >
                    Delete
                  </button>
                </div>
              </div>
            ))}

            {agents.length === 0 && (
              <div className="col-span-full bg-white p-8 rounded-xl border border-dashed border-gray-300 text-center">
                <span className="text-3xl">🔌</span>
                <h4 className="mt-2 text-sm font-semibold text-gray-900">No ERP Sync Agents Configured</h4>
                <p className="text-xs text-gray-500 mt-1 max-w-sm mx-auto">
                  Register a sync agent to connect local Tally Prime (port 9000) or Busy Accounting software with CA Desk.
                </p>
                <button
                  onClick={() => setShowAgentModal(true)}
                  className="mt-3 px-3 py-1.5 bg-indigo-600 text-white rounded-lg text-xs font-medium hover:bg-indigo-700"
                >
                  + Register Sync Agent
                </button>
              </div>
            )}
          </div>

          {/* Live Daybook Table */}
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-gray-200 flex flex-col md:flex-row md:items-center justify-between gap-3">
              <div>
                <h3 className="font-semibold text-gray-900 text-base">Inbound Synced Daybook Vouchers</h3>
                <p className="text-xs text-gray-500 mt-0.5">Live stream of Daybook entries ingested from client accounting software</p>
              </div>
              <div className="flex items-center gap-3">
                <select
                  value={voucherTypeFilter}
                  onChange={e => setVoucherTypeFilter(e.target.value)}
                  className="text-xs border border-gray-300 rounded-lg p-1.5 focus:ring-indigo-500 focus:border-indigo-500"
                >
                  <option value="All">All Voucher Types</option>
                  <option value="Payment">Payment</option>
                  <option value="Receipt">Receipt</option>
                  <option value="Journal">Journal</option>
                  <option value="Contra">Contra</option>
                  <option value="Sales">Sales</option>
                  <option value="Purchase">Purchase</option>
                </select>
                <input
                  type="text"
                  placeholder="Search Daybook..."
                  value={voucherSearch}
                  onChange={e => setVoucherSearch(e.target.value)}
                  className="text-xs border border-gray-300 rounded-lg p-1.5 w-48 focus:ring-indigo-500 focus:border-indigo-500"
                />
              </div>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                <thead className="bg-gray-50 text-gray-600 uppercase font-semibold">
                  <tr>
                    <th className="px-4 py-3">Date</th>
                    <th className="px-4 py-3">Voucher #</th>
                    <th className="px-4 py-3">Type</th>
                    <th className="px-4 py-3">Debit Ledger</th>
                    <th className="px-4 py-3">Credit Ledger</th>
                    <th className="px-4 py-3">Amount</th>
                    <th className="px-4 py-3">Narration & Cheque</th>
                    <th className="px-4 py-3">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200 text-gray-700">
                  {filteredVouchers.map(v => (
                    <tr key={v.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 font-mono">{v.voucherDate}</td>
                      <td className="px-4 py-3 font-bold text-gray-900">{v.voucherNumber}</td>
                      <td className="px-4 py-3">
                        <span className="px-2 py-0.5 rounded text-xs font-semibold bg-gray-100 text-gray-800">
                          {v.voucherType}
                        </span>
                      </td>
                      <td className="px-4 py-3 font-medium text-gray-900">{v.debitLedger}</td>
                      <td className="px-4 py-3 font-medium text-gray-900">{v.creditLedger}</td>
                      <td className="px-4 py-3 font-bold text-gray-900">{formatINR(v.amount)}</td>
                      <td className="px-4 py-3 max-w-xs truncate" title={v.narration}>
                        <div>{v.narration}</div>
                        {v.chequeNumber && (
                          <span className="text-[10px] text-indigo-600 font-mono">Chq/Ref: #{v.chequeNumber}</span>
                        )}
                      </td>
                      <td className="px-4 py-3">{getStatusBadge(v.reconciliationStatus)}</td>
                    </tr>
                  ))}
                  {filteredVouchers.length === 0 && (
                    <tr>
                      <td colSpan={8} className="px-4 py-8 text-center text-gray-500">
                        No Daybook vouchers found.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* TAB 3: 2-Way Outgoing Vouchers (Push Adjustments) */}
      {/* ========================================================================= */}
      {activeTab === 'outgoing' && (
        <div className="space-y-6">
          <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-gray-200 flex flex-col md:flex-row md:items-center justify-between gap-3">
              <div>
                <h3 className="font-semibold text-gray-900 text-base">2-Way Audited Adjustment Voucher Queue</h3>
                <p className="text-xs text-gray-500 mt-0.5">
                  Push audit adjustments, depreciation provisions & tax entries directly into client Tally/Busy ledgers
                </p>
              </div>
              <button
                onClick={() => setShowOutgoingModal(true)}
                className="px-3.5 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-xs font-medium shadow-sm"
              >
                + Push Audited Voucher
              </button>
            </div>

            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                <thead className="bg-gray-50 text-gray-600 uppercase font-semibold">
                  <tr>
                    <th className="px-4 py-3">Reference #</th>
                    <th className="px-4 py-3">Date</th>
                    <th className="px-4 py-3">Type</th>
                    <th className="px-4 py-3">Debit Ledger</th>
                    <th className="px-4 py-3">Credit Ledger</th>
                    <th className="px-4 py-3">Amount</th>
                    <th className="px-4 py-3">Audit Reason</th>
                    <th className="px-4 py-3">Posting Status</th>
                    <th className="px-4 py-3">ERP Voucher #</th>
                    <th className="px-4 py-3">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200 text-gray-700">
                  {outgoingVouchers.map(v => (
                    <tr key={v.id} className="hover:bg-gray-50">
                      <td className="px-4 py-3 font-mono font-bold text-gray-900">{v.referenceNumber}</td>
                      <td className="px-4 py-3 font-mono">{v.voucherDate}</td>
                      <td className="px-4 py-3">
                        <span className="px-2 py-0.5 rounded text-xs font-semibold bg-gray-100 text-gray-800">
                          {v.voucherType}
                        </span>
                      </td>
                      <td className="px-4 py-3 font-medium text-gray-900">{v.debitLedger}</td>
                      <td className="px-4 py-3 font-medium text-gray-900">{v.creditLedger}</td>
                      <td className="px-4 py-3 font-bold text-gray-900">{formatINR(v.amount)}</td>
                      <td className="px-4 py-3">
                        <span className="px-2 py-0.5 rounded text-xs font-semibold bg-indigo-50 text-indigo-700 border border-indigo-200">
                          {v.auditReason}
                        </span>
                      </td>
                      <td className="px-4 py-3">{getStatusBadge(v.postingStatus)}</td>
                      <td className="px-4 py-3 font-mono font-bold text-emerald-700">
                        {v.erpVoucherNumber || '-'}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex items-center gap-2">
                          {v.postingStatus === 'PendingApproval' && (
                            <button
                              onClick={() => handleAuthorizeOutgoing(v.id)}
                              className="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-700 text-white rounded text-xs font-medium shadow-sm"
                            >
                              Partner Authorize
                            </button>
                          )}
                          {v.generatedXmlPayload && (
                            <button
                              onClick={() => setSelectedXmlPayload(v.generatedXmlPayload ?? null)}
                              className="text-xs text-indigo-600 hover:underline"
                            >
                              View XML
                            </button>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                  {outgoingVouchers.length === 0 && (
                    <tr>
                      <td colSpan={10} className="px-4 py-8 text-center text-gray-500">
                        No outgoing vouchers created yet.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 1: Register ERP Sync Agent */}
      {/* ========================================================================= */}
      {showAgentModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-4 border border-gray-100">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Register ERP Live Sync Agent</h3>
              <button onClick={() => setShowAgentModal(false)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
            </div>

            <form onSubmit={handleSaveAgent} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Target Client Entity</label>
                <select
                  value={agentForm.clientId}
                  onChange={e => setAgentForm({ ...agentForm, clientId: e.target.value })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  required
                >
                  <option value="">-- Select Client --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Agent Identifier Name</label>
                  <input
                    type="text"
                    value={agentForm.agentName}
                    onChange={e => setAgentForm({ ...agentForm, agentName: e.target.value })}
                    placeholder="e.g. Accounts Server Agent"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">ERP Software</label>
                  <select
                    value={agentForm.erpType}
                    onChange={e => setAgentForm({ ...agentForm, erpType: e.target.value as ErpType })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  >
                    <option value="TallyPrime">Tally Prime (XML/HTTP)</option>
                    <option value="BusyAccounting">Busy Accounting (ODBC/XML)</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Local Host URL</label>
                  <input
                    type="text"
                    value={agentForm.hostUrl}
                    onChange={e => setAgentForm({ ...agentForm, hostUrl: e.target.value })}
                    placeholder="http://localhost:9000"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Company Name in ERP</label>
                  <input
                    type="text"
                    value={agentForm.companyName}
                    onChange={e => setAgentForm({ ...agentForm, companyName: e.target.value })}
                    placeholder="Acme Corp Ltd (2026-27)"
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Sync Frequency (Minutes)</label>
                <input
                  type="number"
                  value={agentForm.syncFrequencyMinutes}
                  onChange={e => setAgentForm({ ...agentForm, syncFrequencyMinutes: parseInt(e.target.value) || 15 })}
                  min={1}
                  max={1440}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                />
              </div>

              <div className="space-y-2 pt-2">
                <span className="block text-xs font-semibold text-gray-700">Sync Scope</span>
                <div className="flex items-center gap-4 text-xs text-gray-700">
                  <label className="flex items-center gap-1.5 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={agentForm.syncDaybook}
                      onChange={e => setAgentForm({ ...agentForm, syncDaybook: e.target.checked })}
                      className="rounded text-indigo-600"
                    />
                    Sync Daybook Vouchers
                  </label>
                  <label className="flex items-center gap-1.5 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={agentForm.syncMasters}
                      onChange={e => setAgentForm({ ...agentForm, syncMasters: e.target.checked })}
                      className="rounded text-indigo-600"
                    />
                    Sync Ledger Masters
                  </label>
                </div>
              </div>

              <div className="pt-4 border-t flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setShowAgentModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-medium shadow-sm disabled:opacity-50"
                >
                  {loading ? 'Registering...' : 'Register Agent & Generate Token'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 2: Create Outgoing Audited Adjustment Voucher */}
      {/* ========================================================================= */}
      {showOutgoingModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-4 border border-gray-100">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Push Audited Adjustment to ERP</h3>
              <button onClick={() => setShowOutgoingModal(false)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
            </div>

            <form onSubmit={handleCreateOutgoingVoucher} className="space-y-3">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Target ERP Agent</label>
                  <select
                    value={outgoingForm.agentConfigId}
                    onChange={e => setOutgoingForm({ ...outgoingForm, agentConfigId: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  >
                    <option value="">-- Select Agent --</option>
                    {agents.map(a => (
                      <option key={a.id} value={a.id}>{a.agentName} ({a.erpType})</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Audit Reason Category</label>
                  <select
                    value={outgoingForm.auditReason}
                    onChange={e => setOutgoingForm({ ...outgoingForm, auditReason: e.target.value as AuditReasonCategory })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  >
                    <option value="DepreciationSec32">Depreciation Sec 32</option>
                    <option value="TaxProvisionAy2526">Tax Provision AY 25-26</option>
                    <option value="Sa700AuditAdjustment">SA 700 Audit Adjustment</option>
                    <option value="PrepaidAmortization">Prepaid Amortization</option>
                    <option value="BadDebtsWriteoff">Bad Debts Write-off</option>
                    <option value="ForeignExchangeFxAdjustment">Forex (FX) Adjustment</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Voucher Type</label>
                  <select
                    value={outgoingForm.voucherType}
                    onChange={e => setOutgoingForm({ ...outgoingForm, voucherType: e.target.value as ErpVoucherType })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  >
                    <option value="Journal">Journal</option>
                    <option value="Contra">Contra</option>
                    <option value="Payment">Payment</option>
                    <option value="Receipt">Receipt</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Reference Number</label>
                  <input
                    type="text"
                    value={outgoingForm.referenceNumber}
                    onChange={e => setOutgoingForm({ ...outgoingForm, referenceNumber: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border font-mono"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Debit Ledger Name</label>
                  <input
                    type="text"
                    value={outgoingForm.debitLedger}
                    onChange={e => setOutgoingForm({ ...outgoingForm, debitLedger: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Credit Ledger Name</label>
                  <input
                    type="text"
                    value={outgoingForm.creditLedger}
                    onChange={e => setOutgoingForm({ ...outgoingForm, creditLedger: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Amount (₹)</label>
                  <input
                    type="number"
                    value={outgoingForm.amount}
                    onChange={e => setOutgoingForm({ ...outgoingForm, amount: parseFloat(e.target.value) || 0 })}
                    min={0.01}
                    step={0.01}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Voucher Date</label>
                  <input
                    type="date"
                    value={outgoingForm.voucherDate}
                    onChange={e => setOutgoingForm({ ...outgoingForm, voucherDate: e.target.value })}
                    className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-gray-700 mb-1">Narration</label>
                <textarea
                  rows={2}
                  value={outgoingForm.narration}
                  onChange={e => setOutgoingForm({ ...outgoingForm, narration: e.target.value })}
                  className="w-full border-gray-300 rounded-lg shadow-sm text-sm p-2 border"
                  required
                />
              </div>

              <div className="pt-3 border-t flex justify-end gap-3">
                <button
                  type="button"
                  onClick={() => setShowOutgoingModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-medium shadow-sm disabled:opacity-50"
                >
                  {loading ? 'Creating...' : 'Queue Voucher'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* ========================================================================= */}
      {/* MODAL 3: View Tally XML Payload */}
      {/* ========================================================================= */}
      {selectedXmlPayload && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-4 border border-gray-100">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Generated Tally XML Envelope</h3>
              <button onClick={() => setSelectedXmlPayload(null)} className="text-gray-400 hover:text-gray-600 font-bold text-xl">&times;</button>
            </div>
            <pre className="bg-gray-900 text-emerald-400 p-4 rounded-xl text-xs font-mono overflow-x-auto max-h-96">
              {selectedXmlPayload}
            </pre>
            <div className="flex justify-end gap-3">
              <button
                onClick={() => copyToClipboard(selectedXmlPayload)}
                className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-medium shadow-sm"
              >
                Copy XML Payload
              </button>
              <button
                onClick={() => setSelectedXmlPayload(null)}
                className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
