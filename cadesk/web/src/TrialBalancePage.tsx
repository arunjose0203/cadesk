import React, { useState, useEffect, useCallback } from 'react'
import {
  trialBalanceApi,
  api,
  type TrialBalanceImportDto,
  type TrialBalanceLedgerDto,
  type ClientLedgerMappingDto,
  type SyncTrialBalanceResultDto,
  type TrialBalancePreviewResultDto,
  type TrialBalanceSampleDto,
  type Client,
  type ClientPage
} from './api'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(n || 0)

const SCHEDULE3_GROUPS = [
  'RevenueFromOperations',
  'OtherIncome',
  'CostOfMaterialsConsumed',
  'PurchasesOfStockInTrade',
  'ChangesInInventories',
  'EmployeeBenefitsExpense',
  'FinanceCosts',
  'DepreciationAndAmortization',
  'OtherExpenses',
  'TaxExpense',
  'ShareCapital',
  'ReservesAndSurplus',
  'LongTermBorrowings',
  'DeferredTaxLiabilities',
  'OtherLongTermLiabilities',
  'LongTermProvisions',
  'ShortTermBorrowings',
  'TradePayablesMsme',
  'TradePayablesOthers',
  'OtherCurrentLiabilities',
  'ShortTermProvisions',
  'PropertyPlantEquipment',
  'CapitalWip',
  'NonCurrentInvestments',
  'DeferredTaxAssets',
  'LongTermLoansAndAdvances',
  'OtherNonCurrentAssets',
  'CurrentInvestments',
  'Inventories',
  'TradeReceivables',
  'CashAndBank',
  'ShortTermLoansAndAdvances',
  'OtherCurrentAssets'
]

const FORM3CD_CATEGORIES = [
  { value: '', label: 'None' },
  { value: 'Turnover', label: 'Clause 8a / 12: Turnover & Gross Receipts' },
  { value: 'CashReceipt', label: 'Clause 8b: Cash Receipts (5% limit)' },
  { value: 'CashPayment', label: 'Clause 8b: Cash Payments (5% limit)' },
  { value: 'Sec43BStatutoryDue', label: 'Clause 26: Section 43B Unpaid Statutory Dues' },
  { value: 'Sec43BhMsme', label: 'Clause 22: Section 43B(h) MSME Creditors' },
  { value: 'Sec40A3Disallowance', label: 'Clause 21d: Section 40A(3) Cash Payments' },
  { value: 'Sec361vaEmployeeContribution', label: 'Clause 20b: Section 36(1)(va) PF/ESI Contributions' },
  { value: 'TdsDeductibleExpense', label: 'Clause 34: TDS Deductible Payments' }
]

const CARO_CATEGORIES = [
  { value: '', label: 'None' },
  { value: 'Inventory', label: 'Clause (ii)(a): Inventory Physical Verification' },
  { value: 'WorkingCapitalBorrowing', label: 'Clause (ii)(b): Working Capital > ₹5 Cr' },
  { value: 'TermLoan', label: 'Clause (ix)(a): Term Loan Repayments' },
  { value: 'StatutoryDue', label: 'Clause (vii)(a): Undisputed Statutory Dues' },
  { value: 'RelatedPartyAdvance', label: 'Clause (iii): Loans to Related Parties' }
]

const FIXED_ASSET_BLOCKS = [
  { value: '', label: 'None' },
  { value: 'PlantAndMachinery', label: 'Plant & Machinery (15%)' },
  { value: 'Computers', label: 'Computers & Servers (40%)' },
  { value: 'Buildings', label: 'Buildings & Godowns (10%)' },
  { value: 'FurnitureAndFixtures', label: 'Furniture & Fixtures (10%)' },
  { value: 'Vehicles', label: 'Motor Vehicles & Trucks (15% / 30%)' },
  { value: 'Intangibles', label: 'Intangibles & Software (25%)' }
]

export function TrialBalancePage({ can }: { can?: (p: string) => boolean }) {
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')
  const [imports, setImports] = useState<TrialBalanceImportDto[]>([])
  const [activeImport, setActiveImport] = useState<TrialBalanceImportDto | null>(null)
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Filters
  const [searchLedger, setSearchLedger] = useState<string>('')
  const [mappingFilter, setMappingFilter] = useState<'all' | 'mapped' | 'unmapped'>('all')

  // Modals
  const [showUploadModal, setShowUploadModal] = useState<boolean>(false)
  const [showSyncModal, setShowSyncModal] = useState<boolean>(false)
  const [showRulesModal, setShowRulesModal] = useState<boolean>(false)
  const [syncResult, setSyncResult] = useState<SyncTrialBalanceResultDto | null>(null)
  const [clientRules, setClientRules] = useState<ClientLedgerMappingDto[]>([])

  // 3-in-1 Ingestion State
  const [importTab, setImportTab] = useState<'demo' | 'paste' | 'file'>('demo')
  const [samples, setSamples] = useState<TrialBalanceSampleDto[]>([])
  const [uploadSourceType, setUploadSourceType] = useState<string>('Auto')
  const [uploadFileName, setUploadFileName] = useState<string>('trial_balance.tsv')
  const [uploadPayload, setUploadPayload] = useState<string>('')
  const [previewResult, setPreviewResult] = useState<TrialBalancePreviewResultDto | null>(null)
  const [previewLoading, setPreviewLoading] = useState<boolean>(false)

  // Multi-desk sync options
  const syncFlags = {
    syncAudit: true,
    syncSchedule3: true,
    syncForm3Cd: true,
    syncCaro: true,
    syncFixedAssets: true,
    syncCostAudit: true
  }

  // Inline Editing
  const [editingLedgerId, setEditingLedgerId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState({
    schedule3Group: 'OtherExpenses',
    form3CdCategory: '',
    caroCategory: '',
    fixedAssetBlock: '',
    saveAsClientDefaultRule: true
  })

  const loadClients = useCallback(async () => {
    try {
      const res = await api<ClientPage | Client[]>('/api/clients?take=200')
      const items = Array.isArray(res) ? res : (res?.items || [])
      setClients(items)
      if (items && items.length > 0) {
        setSelectedClientId(prev => prev || items[0].id)
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load client list.')
    }
  }, [])

  useEffect(() => {
    loadClients()
    loadSamples()
  }, [loadClients])

  useEffect(() => {
    if (selectedClientId) {
      loadImports(selectedClientId, selectedFy)
    }
  }, [selectedClientId, selectedFy])

  async function loadSamples() {
    try {
      const list = await trialBalanceApi.getSamples()
      setSamples(list || [])
    } catch {
      // Samples optional fallback
    }
  }

  async function loadImports(clientId: string, fy: string) {
    try {
      setLoading(true)
      setError(null)
      const list = await trialBalanceApi.listImports(clientId, fy)
      setImports(list || [])
      if (list && list.length > 0) {
        const full = await trialBalanceApi.getImport(list[0].id)
        setActiveImport(full)
      } else {
        setActiveImport(null)
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load trial balance imports.')
    } finally {
      setLoading(false)
    }
  }

  async function handleSelectImport(id: string) {
    try {
      setLoading(true)
      const full = await trialBalanceApi.getImport(id)
      setActiveImport(full)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to retrieve trial balance import.')
    } finally {
      setLoading(false)
    }
  }

  async function handleRunPreview(payloadToTest?: string, typeToTest?: string) {
    const text = (payloadToTest ?? uploadPayload).trim()
    if (!text) {
      setPreviewResult(null)
      return
    }

    try {
      setPreviewLoading(true)
      const res = await trialBalanceApi.preview({
        contentPayload: text,
        sourceType: typeToTest ?? uploadSourceType
      })
      setPreviewResult(res)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Preview parsing failed.')
    } finally {
      setPreviewLoading(false)
    }
  }

  function handleSelectSample(s: TrialBalanceSampleDto) {
    setUploadSourceType(s.sourceType)
    setUploadFileName(`${s.key}_tb.${s.sourceType === 'TallyXml' ? 'xml' : 'tsv'}`)
    setUploadPayload(s.contentPayload)
    handleRunPreview(s.contentPayload, s.sourceType)
  }

  async function handleConfirmImport(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedClientId) {
      setError('Please select a client first.')
      return
    }
    if (!uploadPayload.trim()) {
      setError('Please provide or paste trial balance data.')
      return
    }

    try {
      setLoading(true)
      setError(null)
      const imported = await trialBalanceApi.upload({
        clientId: selectedClientId,
        financialYear: selectedFy,
        sourceType: uploadSourceType,
        fileName: uploadFileName,
        contentPayload: uploadPayload
      })
      setSuccess(`Trial Balance imported successfully! ${imported.totalLedgersCount} ledgers processed (${imported.mappedLedgersCount} mapped).`)
      setShowUploadModal(false)
      setUploadPayload('')
      setPreviewResult(null)
      await loadImports(selectedClientId, selectedFy)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Trial Balance upload failed.')
    } finally {
      setLoading(false)
    }
  }

  async function handleSyncAllDesks() {
    if (!activeImport) return
    try {
      setLoading(true)
      setError(null)
      const res = await trialBalanceApi.syncAllDesks(activeImport.id, syncFlags)
      setSyncResult(res)
      setShowSyncModal(true)
      setSuccess('Trial Balance figures synchronized down across statutory desks!')
      const refreshed = await trialBalanceApi.getImport(activeImport.id)
      setActiveImport(refreshed)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Multi-desk sync failed.')
    } finally {
      setLoading(false)
    }
  }

  function startEdit(ledger: TrialBalanceLedgerDto) {
    setEditingLedgerId(ledger.id)
    setEditForm({
      schedule3Group: ledger.schedule3Group || 'OtherExpenses',
      form3CdCategory: ledger.form3CdCategory || '',
      caroCategory: ledger.caroCategory || '',
      fixedAssetBlock: ledger.fixedAssetBlock || '',
      saveAsClientDefaultRule: true
    })
  }

  async function saveEdit(ledgerId: string) {
    if (!activeImport) return
    try {
      setLoading(true)
      setError(null)
      const updated = await trialBalanceApi.updateLedgerMapping(activeImport.id, ledgerId, {
        schedule3Group: editForm.schedule3Group,
        form3CdCategory: editForm.form3CdCategory || null,
        caroCategory: editForm.caroCategory || null,
        fixedAssetBlock: editForm.fixedAssetBlock || null,
        saveAsClientDefaultRule: editForm.saveAsClientDefaultRule
      })

      setActiveImport(updated)
      setEditingLedgerId(null)
      const thisLedger = updated.ledgers?.find(l => l.id === ledgerId)
      setSuccess(`Mapping updated for ledger: ${thisLedger?.ledgerName || 'Ledger'}`)
      if (editForm.saveAsClientDefaultRule && selectedClientId) {
        trialBalanceApi.getClientRules(selectedClientId).then(r => setClientRules(r || [])).catch(() => {})
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update ledger mapping.')
    } finally {
      setLoading(false)
    }
  }

  async function loadClientRules() {
    if (!selectedClientId) return
    try {
      setLoading(true)
      const rules = await trialBalanceApi.getClientRules(selectedClientId)
      setClientRules(rules || [])
      setShowRulesModal(true)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to fetch client mapping rules.')
    } finally {
      setLoading(false)
    }
  }

  function handleFileUploadChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setUploadFileName(file.name)
    const lower = file.name.toLowerCase()
    let type = 'Auto'
    if (lower.endsWith('.xml')) type = 'TallyXml'
    else if (lower.endsWith('.csv')) type = 'GenericCsv'
    else if (lower.endsWith('.tsv') || lower.endsWith('.txt')) type = 'ClipboardTsv'
    setUploadSourceType(type)

    const reader = new FileReader()
    reader.onload = evt => {
      const content = evt.target?.result as string
      setUploadPayload(content || '')
      handleRunPreview(content || '', type)
    }
    reader.readAsText(file)
  }

  // Filtered ledgers
  const filteredLedgers = (activeImport?.ledgers || []).filter(l => {
    const matchesSearch =
      l.ledgerName.toLowerCase().includes(searchLedger.toLowerCase()) ||
      l.tallyGroup.toLowerCase().includes(searchLedger.toLowerCase()) ||
      l.schedule3Group.toLowerCase().includes(searchLedger.toLowerCase())
    if (!matchesSearch) return false

    if (mappingFilter === 'mapped') return l.isMapped
    if (mappingFilter === 'unmapped') return !l.isMapped
    return true
  })

  const mappedPercent = activeImport
    ? Math.round((activeImport.mappedLedgersCount / (activeImport.totalLedgersCount || 1)) * 100)
    : 0

  return (
    <div style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto' }}>
      {/* Top Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <h1 style={{ margin: 0, fontSize: '24px', fontWeight: 700, color: 'var(--text)' }}>
              Universal Trial Balance & Financial Data Hub
            </h1>
            <span style={{
              background: 'var(--accent-subtle)',
              color: 'var(--accent)',
              fontSize: '12px',
              fontWeight: 600,
              padding: '2px 10px',
              borderRadius: '9999px',
              border: '1px solid var(--accent)'
            }}>
              Tally • Busy • SAP • Excel TSV
            </span>
          </div>
          <p style={{ margin: '4px 0 0 0', color: 'var(--muted)', fontSize: '14px' }}>
            Test-drive client numbers in under 60 seconds. One universal ingestion cascades into Statutory Audit, Schedule III, Form 3CD, CARO 2020, Fixed Assets, and Cost Audit.
          </p>
        </div>

        {/* Action Controls */}
        <div style={{ display: 'flex', gap: '10px', alignItems: 'center', flexWrap: 'wrap' }}>
          <button
            onClick={loadClientRules}
            disabled={!selectedClientId}
            style={{
              padding: '8px 16px',
              borderRadius: '6px',
              border: '1px solid var(--border-subtle)',
              background: 'var(--card)',
              color: 'var(--text-secondary)',
              fontWeight: 500,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '6px'
            }}
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line><polyline points="10 9 9 9 8 9"></polyline></svg>
            Retained Rules ({clientRules.length > 0 ? clientRules.length : '0'})
          </button>

          {(!can || can('trialbalance.manage')) && (
            <>
              <button
                onClick={() => {
                  setShowUploadModal(true)
                  setImportTab('demo')
                }}
                style={{
                  padding: '8px 18px',
                  borderRadius: '6px',
                  border: '1px solid var(--accent)',
                  background: 'var(--accent-subtle)',
                  color: 'var(--accent)',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px',
                  boxShadow: '0 1px 2px rgba(99, 102, 241, 0.1)'
                }}
              >
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon></svg>
                60-Sec Test Drive / Import
              </button>

              <button
                onClick={handleSyncAllDesks}
                disabled={!activeImport || loading}
                style={{
                  padding: '8px 18px',
                  borderRadius: '6px',
                  border: 'none',
                  background: activeImport ? '#4F46E5' : '#9CA3AF',
                  color: '#FFFFFF',
                  fontWeight: 600,
                  cursor: activeImport ? 'pointer' : 'not-allowed',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '8px',
                  boxShadow: activeImport ? '0 1px 3px rgba(79, 70, 229, 0.3)' : 'none'
                }}
              >
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2"/></svg>
                Sync to All 6 Desks
              </button>
            </>
          )}
        </div>
      </div>

      {/* Alerts */}
      {error && (
        <div style={{ background: 'var(--error-bg)', border: '1px solid var(--error-border)', color: 'var(--error-text)', padding: '12px 16px', borderRadius: '8px', marginBottom: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span>{error}</span>
          <button onClick={() => setError(null)} style={{ background: 'transparent', border: 'none', color: 'var(--error-text)', cursor: 'pointer', fontWeight: 600 }}>✕</button>
        </div>
      )}

      {success && (
        <div style={{ background: 'var(--success-bg)', border: '1px solid #6EE7B7', color: 'var(--success-text)', padding: '12px 16px', borderRadius: '8px', marginBottom: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span>{success}</span>
          <button onClick={() => setSuccess(null)} style={{ background: 'transparent', border: 'none', color: 'var(--success-text)', cursor: 'pointer', fontWeight: 600 }}>✕</button>
        </div>
      )}

      {/* Selectors Bar */}
      <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px', marginBottom: '20px', display: 'flex', alignItems: 'center', gap: '20px', flexWrap: 'wrap' }}>
        <div style={{ minWidth: '280px', flex: 1 }}>
          <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '6px' }}>Client Entity</label>
          <select
            value={selectedClientId}
            onChange={e => setSelectedClientId(e.target.value)}
            style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px', background: 'var(--card)' }}
          >
            {clients.map(c => (
              <option key={c.id} value={c.id}>
                {c.name} {c.taxIds?.['PAN'] ? `(${c.taxIds['PAN']})` : ''}
              </option>
            ))}
          </select>
        </div>

        <div style={{ minWidth: '160px' }}>
          <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '6px' }}>Financial Year</label>
          <select
            value={selectedFy}
            onChange={e => setSelectedFy(e.target.value)}
            style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px', background: 'var(--card)' }}
          >
            <option value="2024-25">2024-25 (AY 2025-26)</option>
            <option value="2023-24">2023-24 (AY 2024-25)</option>
            <option value="2022-23">2022-23 (AY 2023-24)</option>
            <option value="2021-22">2021-22 (AY 2022-23)</option>
          </select>
        </div>

        {imports.length > 1 && (
          <div style={{ minWidth: '220px' }}>
            <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '6px' }}>Import Version</label>
            <select
              value={activeImport?.id || ''}
              onChange={e => handleSelectImport(e.target.value)}
              style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px', background: 'var(--card)' }}
            >
              {imports.map(imp => (
                <option key={imp.id} value={imp.id}>
                  {imp.fileName} ({new Date(imp.createdAt).toLocaleDateString()}) [{imp.sourceType}]
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* KPI Cards */}
      {activeImport ? (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          {/* Total Debit */}
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              Total Debits
            </div>
            <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--text)', marginTop: '6px' }}>
              {fmtInr(activeImport.totalDebit)}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px' }}>
              From {activeImport.totalLedgersCount} ledgers
            </div>
          </div>

          {/* Total Credit */}
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              Total Credits
            </div>
            <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--text)', marginTop: '6px' }}>
              {fmtInr(activeImport.totalCredit)}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px' }}>
              Closing balance net sum
            </div>
          </div>

          {/* Balanced Status */}
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              Integrity & Health
            </div>
            <div style={{ marginTop: '6px' }}>
              {activeImport.isBalanced ? (
                <span style={{ background: '#DEF7EC', color: '#03543F', padding: '4px 10px', borderRadius: '6px', fontSize: '12px', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                  ✓ Balanced (₹0 Diff)
                </span>
              ) : (
                <span style={{ background: 'var(--error-bg)', color: 'var(--error-text)', padding: '4px 10px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
                  ✕ Diff: {fmtInr(Math.abs(activeImport.totalDebit - activeImport.totalCredit))}
                </span>
              )}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '6px' }}>
              Source: <span style={{ fontWeight: 600, color: 'var(--text-secondary)' }}>{activeImport.sourceType}</span>
            </div>
          </div>

          {/* Classification Progress */}
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Classification
              </span>
              <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--accent)' }}>{mappedPercent}%</span>
            </div>
            <div style={{ fontSize: '20px', fontWeight: 700, color: 'var(--text)', marginTop: '4px' }}>
              {activeImport.mappedLedgersCount} / {activeImport.totalLedgersCount}
            </div>
            <div style={{ width: '100%', height: '6px', background: 'var(--bg-subtle)', borderRadius: '9999px', marginTop: '8px', overflow: 'hidden' }}>
              <div style={{ width: `${mappedPercent}%`, height: '100%', background: '#4F46E5', borderRadius: '9999px' }} />
            </div>
          </div>

          {/* Multi-Desk Cascading Sync */}
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              Cascading Sync
            </div>
            <div style={{ marginTop: '6px' }}>
              {activeImport.syncedAt ? (
                <span style={{ background: 'var(--accent-subtle)', color: 'var(--accent)', padding: '4px 10px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
                  ✓ Synced ({activeImport.syncedDesks || 'All Desks'})
                </span>
              ) : (
                <span style={{ background: 'var(--warning-bg)', color: 'var(--warning-text)', padding: '4px 10px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
                  Ready to Sync (6 Desks)
                </span>
              )}
            </div>
            <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '6px' }}>
              {activeImport.syncedAt ? `At: ${new Date(activeImport.syncedAt).toLocaleString()}` : 'Awaiting one-click sync'}
            </div>
          </div>
        </div>
      ) : (
        <div style={{ background: 'var(--card)', border: '1px dashed var(--accent)', borderRadius: '12px', padding: '48px', textAlign: 'center', marginBottom: '24px' }}>
          <div style={{ width: '60px', height: '60px', borderRadius: '50%', background: 'var(--accent-subtle)', color: 'var(--accent)', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 16px', fontSize: '28px' }}>
            ⚡
          </div>
          <h3 style={{ margin: '0 0 8px 0', fontSize: '20px', fontWeight: 700, color: 'var(--text)' }}>
            Test Drive Real Financial Numbers in 60 Seconds
          </h3>
          <p style={{ margin: '0 0 20px 0', color: 'var(--muted)', fontSize: '14px', maxWidth: '620px', marginLeft: 'auto', marginRight: 'auto', lineHeight: '1.5' }}>
            No complicated migration needed! Ingest trial balances directly from <strong>Tally Prime (XML/CSV)</strong>, <strong>Busy Accounting (TSV)</strong>, <strong>SAP ERP (GL Statements)</strong>, or directly <strong>paste columns from Excel / Google Sheets</strong>.
          </p>
          <div style={{ display: 'flex', gap: '12px', justifyContent: 'center' }}>
            <button
              onClick={() => {
                setShowUploadModal(true)
                setImportTab('demo')
              }}
              style={{
                padding: '10px 24px',
                borderRadius: '6px',
                border: 'none',
                background: '#4F46E5',
                color: '#FFFFFF',
                fontWeight: 600,
                cursor: 'pointer',
                fontSize: '14px',
                display: 'flex',
                alignItems: 'center',
                gap: '8px',
                boxShadow: '0 2px 4px rgba(79, 70, 229, 0.25)'
              }}
            >
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon></svg>
              Launch 1-Click Instant Demo
            </button>
            <button
              onClick={() => {
                setShowUploadModal(true)
                setImportTab('paste')
              }}
              style={{
                padding: '10px 20px',
                borderRadius: '6px',
                border: '1px solid var(--border-subtle)',
                background: 'var(--card)',
                color: 'var(--text-secondary)',
                fontWeight: 600,
                cursor: 'pointer',
                fontSize: '14px',
                display: 'flex',
                alignItems: 'center',
                gap: '8px'
              }}
            >
              📋 Paste from Excel (Ctrl+V)
            </button>
          </div>
        </div>
      )}

      {/* Ledgers Workbench Table */}
      {activeImport && (
        <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
          {/* Table Toolbar */}
          <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '12px' }}>
            <div style={{ display: 'flex', gap: '12px', alignItems: 'center', flex: 1, minWidth: '300px' }}>
              <input
                type="text"
                placeholder="Search ledger name, parent group or Schedule III group..."
                value={searchLedger}
                onChange={e => setSearchLedger(e.target.value)}
                style={{ padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px', width: '340px' }}
              />
              <div style={{ display: 'flex', background: 'var(--card-subtle)', borderRadius: '6px', padding: '2px' }}>
                <button
                  onClick={() => setMappingFilter('all')}
                  style={{
                    padding: '6px 12px',
                    borderRadius: '4px',
                    border: 'none',
                    background: mappingFilter === 'all' ? 'var(--card)' : 'transparent',
                    color: mappingFilter === 'all' ? 'var(--text)' : 'var(--muted)',
                    fontWeight: 600,
                    fontSize: '12px',
                    cursor: 'pointer',
                    boxShadow: mappingFilter === 'all' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none'
                  }}
                >
                  All ({activeImport.ledgers.length})
                </button>
                <button
                  onClick={() => setMappingFilter('mapped')}
                  style={{
                    padding: '6px 12px',
                    borderRadius: '4px',
                    border: 'none',
                    background: mappingFilter === 'mapped' ? 'var(--card)' : 'transparent',
                    color: mappingFilter === 'mapped' ? 'var(--text)' : 'var(--muted)',
                    fontWeight: 600,
                    fontSize: '12px',
                    cursor: 'pointer',
                    boxShadow: mappingFilter === 'mapped' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none'
                  }}
                >
                  Mapped ({activeImport.mappedLedgersCount})
                </button>
                <button
                  onClick={() => setMappingFilter('unmapped')}
                  style={{
                    padding: '6px 12px',
                    borderRadius: '4px',
                    border: 'none',
                    background: mappingFilter === 'unmapped' ? 'var(--card)' : 'transparent',
                    color: mappingFilter === 'unmapped' ? 'var(--text)' : 'var(--muted)',
                    fontWeight: 600,
                    fontSize: '12px',
                    cursor: 'pointer',
                    boxShadow: mappingFilter === 'unmapped' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none'
                  }}
                >
                  Unmapped ({activeImport.totalLedgersCount - activeImport.mappedLedgersCount})
                </button>
              </div>
            </div>

            <div style={{ fontSize: '13px', color: 'var(--muted)' }}>
              Showing {filteredLedgers.length} of {activeImport.ledgers.length} ledgers
            </div>
          </div>

          {/* Table Content */}
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', color: 'var(--text-secondary)', fontWeight: 600 }}>
                  <th style={{ padding: '12px 16px' }}>Ledger & Group</th>
                  <th style={{ padding: '12px 16px', textAlign: 'right' }}>Debit (₹)</th>
                  <th style={{ padding: '12px 16px', textAlign: 'right' }}>Credit (₹)</th>
                  <th style={{ padding: '12px 16px', textAlign: 'right' }}>Closing (₹)</th>
                  <th style={{ padding: '12px 16px' }}>Schedule III Group</th>
                  <th style={{ padding: '12px 16px' }}>Form 3CD Desk</th>
                  <th style={{ padding: '12px 16px' }}>CARO / Asset Block</th>
                  <th style={{ padding: '12px 16px', textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredLedgers.map(ledger => {
                  const isEditing = editingLedgerId === ledger.id
                  return (
                    <tr
                      key={ledger.id}
                      style={{
                        borderBottom: '1px solid #F3F4F6',
                        background: isEditing ? 'var(--success-bg)' : 'transparent',
                        transition: 'background 0.15s ease'
                      }}
                    >
                      {/* Name & Parent Group */}
                      <td style={{ padding: '12px 16px' }}>
                        <div style={{ fontWeight: 600, color: 'var(--text)' }}>{ledger.ledgerName}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '2px' }}>
                          Parent: <span style={{ fontWeight: 500 }}>{ledger.tallyGroup || 'Direct / Primary'}</span>
                          {ledger.isMapped ? (
                            <span style={{ marginLeft: '6px', color: 'var(--success)', fontWeight: 600 }}>✓ Mapped</span>
                          ) : (
                            <span style={{ marginLeft: '6px', color: 'var(--warning)', fontWeight: 500 }}>• Unmapped</span>
                          )}
                        </div>
                      </td>

                      {/* Amounts */}
                      <td style={{ padding: '12px 16px', textAlign: 'right', fontFamily: 'monospace', fontSize: '13px' }}>
                        {ledger.debitAmount > 0 ? fmtInr(ledger.debitAmount) : '-'}
                      </td>
                      <td style={{ padding: '12px 16px', textAlign: 'right', fontFamily: 'monospace', fontSize: '13px' }}>
                        {ledger.creditAmount > 0 ? fmtInr(ledger.creditAmount) : '-'}
                      </td>
                      <td style={{ padding: '12px 16px', textAlign: 'right', fontFamily: 'monospace', fontWeight: 600, color: ledger.closingBalance >= 0 ? 'var(--text)' : 'var(--error-text)' }}>
                        {fmtInr(ledger.closingBalance)}
                      </td>

                      {/* Schedule III Group */}
                      <td style={{ padding: '12px 16px' }}>
                        {isEditing ? (
                          <select
                            value={editForm.schedule3Group}
                            onChange={e => setEditForm({ ...editForm, schedule3Group: e.target.value })}
                            style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '12px', width: '100%' }}
                          >
                            {SCHEDULE3_GROUPS.map(g => (
                              <option key={g} value={g}>{g}</option>
                            ))}
                          </select>
                        ) : (
                          <span style={{ background: 'var(--card-subtle)', color: 'var(--text-secondary)', padding: '2px 8px', borderRadius: '4px', fontSize: '12px' }}>
                            {ledger.schedule3Group}
                          </span>
                        )}
                      </td>

                      {/* Form 3CD Category */}
                      <td style={{ padding: '12px 16px' }}>
                        {isEditing ? (
                          <select
                            value={editForm.form3CdCategory}
                            onChange={e => setEditForm({ ...editForm, form3CdCategory: e.target.value })}
                            style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '12px', width: '100%' }}
                          >
                            {FORM3CD_CATEGORIES.map(c => (
                              <option key={c.value} value={c.value}>{c.label}</option>
                            ))}
                          </select>
                        ) : (
                          <span style={{ fontSize: '12px', color: ledger.form3CdCategory ? 'var(--accent)' : 'var(--muted)' }}>
                            {FORM3CD_CATEGORIES.find(c => c.value === ledger.form3CdCategory)?.label || '—'}
                          </span>
                        )}
                      </td>

                      {/* CARO / Asset Block */}
                      <td style={{ padding: '12px 16px' }}>
                        {isEditing ? (
                          <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                            <select
                              value={editForm.caroCategory}
                              onChange={e => setEditForm({ ...editForm, caroCategory: e.target.value })}
                              style={{ padding: '4px 6px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '11px' }}
                            >
                              {CARO_CATEGORIES.map(c => (
                                <option key={c.value} value={c.value}>{c.label}</option>
                              ))}
                            </select>
                            <select
                              value={editForm.fixedAssetBlock}
                              onChange={e => setEditForm({ ...editForm, fixedAssetBlock: e.target.value })}
                              style={{ padding: '4px 6px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '11px' }}
                            >
                              {FIXED_ASSET_BLOCKS.map(b => (
                                <option key={b.value} value={b.value}>{b.label}</option>
                              ))}
                            </select>
                            <label style={{ fontSize: '11px', display: 'flex', alignItems: 'center', gap: '4px', marginTop: '2px', color: 'var(--success-text)' }}>
                              <input
                                type="checkbox"
                                checked={editForm.saveAsClientDefaultRule}
                                onChange={e => setEditForm({ ...editForm, saveAsClientDefaultRule: e.target.checked })}
                              />
                              Remember rule for future FYs
                            </label>
                          </div>
                        ) : (
                          <div style={{ fontSize: '12px' }}>
                            {ledger.fixedAssetBlock && (
                              <div style={{ color: 'var(--success-text)', fontWeight: 500 }}>
                                Block: {ledger.fixedAssetBlock}
                              </div>
                            )}
                            {ledger.caroCategory && (
                              <div style={{ color: 'var(--warning)', fontSize: '11px' }}>
                                CARO: {ledger.caroCategory}
                              </div>
                            )}
                            {!ledger.fixedAssetBlock && !ledger.caroCategory && <span style={{ color: 'var(--muted)' }}>—</span>}
                          </div>
                        )}
                      </td>

                      {/* Action Button */}
                      <td style={{ padding: '12px 16px', textAlign: 'center' }}>
                        {isEditing ? (
                          <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                            <button
                              onClick={() => saveEdit(ledger.id)}
                              style={{ padding: '4px 10px', borderRadius: '4px', background: '#059669', color: '#FFFFFF', border: 'none', fontSize: '11px', fontWeight: 600, cursor: 'pointer' }}
                            >
                              Save
                            </button>
                            <button
                              onClick={() => setEditingLedgerId(null)}
                              style={{ padding: '4px 8px', borderRadius: '4px', background: 'var(--bg-subtle)', color: 'var(--text-secondary)', border: 'none', fontSize: '11px', cursor: 'pointer' }}
                            >
                              ✕
                            </button>
                          </div>
                        ) : (
                          <button
                            onClick={() => startEdit(ledger)}
                            style={{ padding: '4px 10px', borderRadius: '4px', background: 'var(--card-subtle)', color: 'var(--text-secondary)', border: '1px solid var(--border-subtle)', fontSize: '11px', fontWeight: 500, cursor: 'pointer' }}
                          >
                            Edit Map
                          </button>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* 3-in-1 Universal Ingestion Modal */}
      {showUploadModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.6)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(2px)' }}>
          <div style={{ background: 'var(--card)', padding: '24px', borderRadius: '12px', width: '820px', maxWidth: '95%', maxHeight: '92vh', overflowY: 'auto', boxShadow: '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)' }}>
            {/* Header */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <h3 style={{ margin: 0, fontSize: '20px', fontWeight: 700, color: 'var(--text)' }}>
                    Universal Financial Data Importer
                  </h3>
                  <span style={{ background: 'var(--success-bg)', color: 'var(--success-text)', fontSize: '11px', fontWeight: 600, padding: '2px 8px', borderRadius: '6px' }}>
                    60-Sec Test Drive
                  </span>
                </div>
                <span style={{ fontSize: '13px', color: 'var(--muted)' }}>
                  Intelligent column heuristic auto-mapper for Tally Prime, Busy Accounting, SAP GL, and Excel clipboard paste.
                </span>
              </div>
              <button onClick={() => setShowUploadModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '20px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            {/* Ingestion Mode Tabs */}
            <div style={{ display: 'flex', borderBottom: '1px solid var(--border)', marginBottom: '20px' }}>
              <button
                type="button"
                onClick={() => setImportTab('demo')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: importTab === 'demo' ? '2px solid var(--accent)' : '2px solid transparent',
                  background: 'transparent',
                  color: importTab === 'demo' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px'
                }}
              >
                ⚡ 1-Click Demo Datasets
              </button>

              <button
                type="button"
                onClick={() => setImportTab('paste')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: importTab === 'paste' ? '2px solid var(--accent)' : '2px solid transparent',
                  background: 'transparent',
                  color: importTab === 'paste' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px'
                }}
              >
                📋 Paste from Excel / Sheets
              </button>

              <button
                type="button"
                onClick={() => setImportTab('file')}
                style={{
                  padding: '10px 18px',
                  border: 'none',
                  borderBottom: importTab === 'file' ? '2px solid var(--accent)' : '2px solid transparent',
                  background: 'transparent',
                  color: importTab === 'file' ? 'var(--accent)' : 'var(--muted)',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px'
                }}
              >
                📁 File Upload (XML / CSV / TSV)
              </button>
            </div>

            {/* TAB 1: 1-Click Demo Datasets */}
            {importTab === 'demo' && (
              <div style={{ marginBottom: '20px' }}>
                <p style={{ margin: '0 0 12px 0', fontSize: '13px', color: 'var(--text-secondary)' }}>
                  Select one of the pre-loaded real-world datasets below to test the full CaDesk platform immediately without preparing client files:
                </p>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(230px, 1fr))', gap: '12px', marginBottom: '16px' }}>
                  {samples.map(s => {
                    const isSelected = uploadPayload === s.contentPayload
                    return (
                      <div
                        key={s.key}
                        onClick={() => handleSelectSample(s)}
                        style={{
                          border: isSelected ? '2px solid var(--accent)' : '1px solid var(--border)',
                          background: isSelected ? 'var(--accent-subtle)' : 'var(--card-subtle)',
                          borderRadius: '8px',
                          padding: '14px',
                          cursor: 'pointer',
                          transition: 'all 0.15s ease'
                        }}
                      >
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                          <span style={{ fontSize: '11px', fontWeight: 600, color: 'var(--accent)', background: 'var(--card)', padding: '2px 6px', borderRadius: '4px', border: '1px solid var(--accent)' }}>
                            {s.sourceType}
                          </span>
                          <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--success)' }}>
                            ₹{(s.turnoverInr / 10000000).toFixed(1)} Cr Rev
                          </span>
                        </div>
                        <div style={{ fontWeight: 700, fontSize: '14px', color: 'var(--text)', marginTop: '8px' }}>
                          {s.title}
                        </div>
                        <p style={{ margin: '6px 0 0 0', fontSize: '12px', color: 'var(--muted)', lineHeight: '1.4' }}>
                          {s.description}
                        </p>
                        <div style={{ marginTop: '10px', fontSize: '11px', fontWeight: 600, color: 'var(--accent)' }}>
                          {isSelected ? '✓ Selected & Previewing' : 'Click to Load →'}
                        </div>
                      </div>
                    )
                  })}
                </div>
              </div>
            )}

            {/* TAB 2: Direct Clipboard Paste from Excel / Sheets */}
            {importTab === 'paste' && (
              <div style={{ marginBottom: '20px' }}>
                <div style={{ background: 'var(--success-bg)', border: '1px solid var(--success-border)', padding: '12px 16px', borderRadius: '8px', marginBottom: '12px', fontSize: '13px', color: 'var(--success-text)' }}>
                  💡 <strong>How to paste:</strong> In Microsoft Excel or Google Sheets, highlight your Trial Balance columns (Account Name, Group, Debit, Credit, Closing) and copy (Ctrl+C). Paste directly into the box below (Ctrl+V). CaDesk automatically splits tabs and detects headers!
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>
                    Paste Excel / Google Sheets Columns (Tab-Delimited)
                  </label>
                  <button
                    type="button"
                    onClick={() => handleRunPreview()}
                    style={{ background: 'transparent', border: 'none', color: 'var(--accent)', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
                  >
                    Inspect & Preview Table →
                  </button>
                </div>
                <textarea
                  rows={8}
                  value={uploadPayload}
                  onChange={e => {
                    setUploadPayload(e.target.value)
                    setUploadSourceType('ClipboardTsv')
                  }}
                  onBlur={() => handleRunPreview()}
                  placeholder={"Account Name\tParent Group\tDebit (Rs.)\tCredit (Rs.)\tClosing Bal.\nDomestic Sales\tSales Accounts\t0.00\t15000000.00\t15000000.00 Cr\nRaw Material Consumed\tDirect Expenses\t8000000.00\t0.00\t8000000.00 Dr\nSBI Current A/c\tBank Accounts\t7000000.00\t0.00\t7000000.00 Dr"}
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '12px', fontFamily: 'monospace', whiteSpace: 'pre', overflowX: 'auto' }}
                />
              </div>
            )}

            {/* TAB 3: File Upload */}
            {importTab === 'file' && (
              <div style={{ marginBottom: '20px' }}>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Source Format</label>
                    <select
                      value={uploadSourceType}
                      onChange={e => setUploadSourceType(e.target.value)}
                      style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
                    >
                      <option value="Auto">Auto-Detect (Heuristic)</option>
                      <option value="TallyXml">Tally XML Export (Collection / Envelope)</option>
                      <option value="BusyCsv">Busy Accounting (TSV / CSV)</option>
                      <option value="SapGl">SAP ERP GL Trial Balance</option>
                      <option value="GenericCsv">Standard CSV / TSV</option>
                    </select>
                  </div>

                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>File Name</label>
                    <input
                      type="text"
                      value={uploadFileName}
                      onChange={e => setUploadFileName(e.target.value)}
                      style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
                      required
                    />
                  </div>
                </div>

                <div style={{ border: '2px dashed var(--border-subtle)', borderRadius: '8px', padding: '24px', textAlign: 'center', background: 'var(--card-subtle)' }}>
                  <input
                    type="file"
                    accept=".xml,.csv,.tsv,.txt"
                    onChange={handleFileUploadChange}
                    style={{ display: 'none' }}
                    id="tb-file-input-universal"
                  />
                  <label htmlFor="tb-file-input-universal" style={{ cursor: 'pointer', color: 'var(--accent)', fontWeight: 600, fontSize: '14px', display: 'block' }}>
                    Browse XML, CSV or TSV File
                  </label>
                  <span style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginTop: '6px' }}>
                    Supports files exported from Tally ERP9/Prime, Busy 18/21, SAP S/4HANA or custom ERPs
                  </span>
                </div>
              </div>
            )}

            {/* LIVE PREVIEW SECTION */}
            {previewLoading && (
              <div style={{ padding: '16px', textAlign: 'center', color: 'var(--accent)', fontSize: '13px' }}>
                Analyzing column headers and checking balance integrity...
              </div>
            )}

            {previewResult && !previewLoading && (
              <div style={{ background: 'var(--card-subtle)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px', marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px', flexWrap: 'wrap', gap: '8px' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                    <span style={{ fontWeight: 700, fontSize: '14px', color: 'var(--text)' }}>
                      Data Diagnostics
                    </span>
                    {previewResult.isBalanced ? (
                      <span style={{ background: '#DEF7EC', color: '#03543F', padding: '3px 10px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
                        ✓ Balanced (₹0.00 Discrepancy)
                      </span>
                    ) : (
                      <span style={{ background: 'var(--error-bg)', color: 'var(--error-text)', padding: '3px 10px', borderRadius: '6px', fontSize: '12px', fontWeight: 600 }}>
                        ⚠ Out of Balance: ₹{previewResult.discrepancy.toLocaleString('en-IN')}
                      </span>
                    )}
                    <span style={{ background: 'var(--accent-subtle)', color: 'var(--accent)', padding: '3px 8px', borderRadius: '6px', fontSize: '11px', fontWeight: 600 }}>
                      Format: {previewResult.detectedSourceType}
                    </span>
                  </div>

                  <div style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>
                    <strong>{previewResult.totalLedgersCount}</strong> Ledgers • <strong>{previewResult.mappedLedgersCount}</strong> Auto-Mapped
                  </div>
                </div>

                {/* Diagnostics bullets */}
                <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', marginBottom: '14px', fontSize: '12px', color: 'var(--text-secondary)' }}>
                  {previewResult.diagnostics.map((d, idx) => (
                    <div key={idx} style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span>{d}</span>
                    </div>
                  ))}
                </div>

                {/* Mini Preview Table */}
                <div style={{ overflowX: 'auto', maxHeight: '180px', overflowY: 'auto', background: 'var(--card)', borderRadius: '6px', border: '1px solid var(--border)' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                    <thead>
                      <tr style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', color: 'var(--text-secondary)' }}>
                        <th style={{ padding: '6px 10px', textAlign: 'left' }}>Ledger Account</th>
                        <th style={{ padding: '6px 10px', textAlign: 'left' }}>Parent Group</th>
                        <th style={{ padding: '6px 10px', textAlign: 'right' }}>Debit (₹)</th>
                        <th style={{ padding: '6px 10px', textAlign: 'right' }}>Credit (₹)</th>
                        <th style={{ padding: '6px 10px', textAlign: 'right' }}>Closing (₹)</th>
                      </tr>
                    </thead>
                    <tbody>
                      {previewResult.ledgers.slice(0, 6).map((l, idx) => (
                        <tr key={idx} style={{ borderBottom: '1px solid #F3F4F6' }}>
                          <td style={{ padding: '6px 10px', fontWeight: 500 }}>{l.name}</td>
                          <td style={{ padding: '6px 10px', color: 'var(--muted)' }}>{l.parentGroup || '—'}</td>
                          <td style={{ padding: '6px 10px', textAlign: 'right', fontFamily: 'monospace' }}>
                            {l.debitAmount > 0 ? fmtInr(l.debitAmount) : '-'}
                          </td>
                          <td style={{ padding: '6px 10px', textAlign: 'right', fontFamily: 'monospace' }}>
                            {l.creditAmount > 0 ? fmtInr(l.creditAmount) : '-'}
                          </td>
                          <td style={{ padding: '6px 10px', textAlign: 'right', fontFamily: 'monospace', fontWeight: 600 }}>
                            {fmtInr(l.closingBalance)}
                          </td>
                        </tr>
                      ))}
                      {previewResult.ledgers.length > 6 && (
                        <tr>
                          <td colSpan={5} style={{ padding: '6px 10px', textAlign: 'center', color: 'var(--muted)', fontSize: '11px', background: 'var(--card-subtle)' }}>
                            ... and {previewResult.ledgers.length - 6} more ledgers parsed successfully
                          </td>
                        </tr>
                      )}
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Action Buttons */}
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={() => setShowUploadModal(false)}
                style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', cursor: 'pointer', fontSize: '13px' }}
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleConfirmImport}
                disabled={loading || !uploadPayload.trim()}
                style={{
                  padding: '8px 24px',
                  borderRadius: '6px',
                  border: 'none',
                  background: uploadPayload.trim() ? '#4F46E5' : '#9CA3AF',
                  color: '#FFFFFF',
                  fontWeight: 600,
                  cursor: uploadPayload.trim() ? 'pointer' : 'not-allowed',
                  fontSize: '13px',
                  boxShadow: '0 1px 3px rgba(79, 70, 229, 0.3)'
                }}
              >
                {loading ? 'Ingesting...' : 'Confirm & Ingest Trial Balance'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Sync Success & Results Modal */}
      {showSyncModal && syncResult && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.6)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(2px)' }}>
          <div style={{ background: 'var(--card)', padding: '24px', borderRadius: '12px', width: '640px', maxWidth: '95%', maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '16px' }}>
              <div style={{ width: '44px', height: '44px', borderRadius: '50%', background: '#DEF7EC', color: '#03543F', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '24px' }}>
                ✓
              </div>
              <div>
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>
                  Multi-Desk Synchronization Complete!
                </h3>
                <span style={{ fontSize: '13px', color: 'var(--muted)' }}>
                  Financial Year: {syncResult.financialYear} • Synced at {new Date(syncResult.syncedAt).toLocaleTimeString()}
                </span>
              </div>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', marginTop: '16px' }}>
              {/* Desk 1: Statutory Audit */}
              <div style={{ padding: '10px 14px', background: 'var(--card-subtle)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600, fontSize: '13px', color: 'var(--text)' }}>1. Statutory Audit Desk (SA 320 Materiality)</span>
                  <span style={{ color: syncResult.auditSynced ? 'var(--success)' : 'var(--warning)', fontWeight: 600, fontSize: '12px' }}>
                    {syncResult.auditSynced ? '✓ Benchmark Updated' : 'Materiality Suggestion'}
                  </span>
                </div>
                {syncResult.auditNotes && (
                  <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: 'var(--text-secondary)' }}>{syncResult.auditNotes}</p>
                )}
              </div>

              {/* Desk 2: Schedule III */}
              <div style={{ padding: '10px 14px', background: 'var(--card-subtle)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600, fontSize: '13px', color: 'var(--text)' }}>2. Schedule III Financial Statements Desk</span>
                  <span style={{ color: syncResult.schedule3Synced ? 'var(--success)' : 'var(--muted)', fontWeight: 600, fontSize: '12px' }}>
                    {syncResult.schedule3Synced ? '✓ 11 Statutory Ratios Updated' : 'Skipped'}
                  </span>
                </div>
                {syncResult.schedule3Notes && (
                  <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: 'var(--text-secondary)' }}>{syncResult.schedule3Notes}</p>
                )}
              </div>

              {/* Desk 3: Form 3CD */}
              <div style={{ padding: '10px 14px', background: 'var(--card-subtle)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600, fontSize: '13px', color: 'var(--text)' }}>3. Form 3CD Tax Audit Desk</span>
                  <span style={{ color: syncResult.form3CdSynced ? 'var(--success)' : 'var(--muted)', fontWeight: 600, fontSize: '12px' }}>
                    {syncResult.form3CdSynced ? '✓ Turnover, 43B & MSME Populated' : 'Skipped'}
                  </span>
                </div>
                {syncResult.form3CdNotes && (
                  <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: 'var(--text-secondary)' }}>{syncResult.form3CdNotes}</p>
                )}
              </div>

              {/* Desk 4: CARO 2020 */}
              <div style={{ padding: '10px 14px', background: 'var(--card-subtle)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600, fontSize: '13px', color: 'var(--text)' }}>4. CARO 2020 Desk</span>
                  <span style={{ color: syncResult.caroSynced ? 'var(--success)' : 'var(--muted)', fontWeight: 600, fontSize: '12px' }}>
                    {syncResult.caroSynced ? '✓ Capital & Borrowing Limits Filled' : 'Skipped'}
                  </span>
                </div>
                {syncResult.caroNotes && (
                  <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: 'var(--text-secondary)' }}>{syncResult.caroNotes}</p>
                )}
              </div>

              {/* Desk 5: Fixed Assets */}
              <div style={{ padding: '10px 14px', background: 'var(--card-subtle)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600, fontSize: '13px', color: 'var(--text)' }}>5. Fixed Assets Register Desk</span>
                  <span style={{ color: syncResult.fixedAssetsSynced ? 'var(--success)' : 'var(--muted)', fontWeight: 600, fontSize: '12px' }}>
                    {syncResult.fixedAssetsSynced ? '✓ Reconciled with Gross Register' : 'Skipped'}
                  </span>
                </div>
                {syncResult.fixedAssetsNotes && (
                  <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: 'var(--text-secondary)' }}>{syncResult.fixedAssetsNotes}</p>
                )}
              </div>

              {/* Desk 6: Cost Audit */}
              <div style={{ padding: '10px 14px', background: 'var(--card-subtle)', borderRadius: '8px', border: '1px solid var(--border)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <span style={{ fontWeight: 600, fontSize: '13px', color: 'var(--text)' }}>6. Cost Audit Desk (Form CRA-1 to CRA-4)</span>
                  <span style={{ color: syncResult.costAuditSynced ? 'var(--success)' : 'var(--muted)', fontWeight: 600, fontSize: '12px' }}>
                    {syncResult.costAuditSynced ? '✓ Product Turnover & Costs Synchronized' : 'Optional / Ready'}
                  </span>
                </div>
                {syncResult.costAuditNotes && (
                  <p style={{ margin: '4px 0 0 0', fontSize: '12px', color: 'var(--text-secondary)' }}>{syncResult.costAuditNotes}</p>
                )}
              </div>
            </div>

            <div style={{ marginTop: '20px', display: 'flex', justifyContent: 'flex-end' }}>
              <button
                onClick={() => setShowSyncModal(false)}
                style={{ padding: '8px 20px', borderRadius: '6px', border: 'none', background: '#4F46E5', color: '#FFFFFF', fontWeight: 600, cursor: 'pointer' }}
              >
                Close Summary
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Retained Rules Modal */}
      {showRulesModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.6)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000, backdropFilter: 'blur(2px)' }}>
          <div style={{ background: 'var(--card)', padding: '24px', borderRadius: '12px', width: '720px', maxWidth: '95%', maxHeight: '85vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div>
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>
                  Client Retained Mapping Rules
                </h3>
                <span style={{ fontSize: '12px', color: 'var(--muted)' }}>
                  Rules mapped once for this client are remembered forever across all financial years.
                </span>
              </div>
              <button onClick={() => setShowRulesModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '18px', cursor: 'pointer' }}>✕</button>
            </div>

            {clientRules.length === 0 ? (
              <div style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)', background: 'var(--card-subtle)', borderRadius: '8px' }}>
                No custom default rules retained yet. When you edit a ledger mapping and check "Remember rule for future FYs", it will appear here.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                <thead>
                  <tr style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', textAlign: 'left' }}>
                    <th style={{ padding: '10px 12px' }}>Ledger Pattern</th>
                    <th style={{ padding: '10px 12px' }}>Schedule III Group</th>
                    <th style={{ padding: '10px 12px' }}>Form 3CD Category</th>
                    <th style={{ padding: '10px 12px' }}>CARO / Asset Block</th>
                  </tr>
                </thead>
                <tbody>
                  {clientRules.map(r => (
                    <tr key={r.id} style={{ borderBottom: '1px solid #F3F4F6' }}>
                      <td style={{ padding: '10px 12px', fontWeight: 600 }}>{r.ledgerName}</td>
                      <td style={{ padding: '10px 12px' }}>{r.schedule3Group}</td>
                      <td style={{ padding: '10px 12px', color: 'var(--accent)' }}>{r.form3CdCategory || '—'}</td>
                      <td style={{ padding: '10px 12px', color: 'var(--success-text)' }}>{r.fixedAssetBlock || r.caroCategory || '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            <div style={{ marginTop: '20px', display: 'flex', justifyContent: 'flex-end' }}>
              <button
                onClick={() => setShowRulesModal(false)}
                style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card)', cursor: 'pointer' }}
              >
                Done
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
export default TrialBalancePage
