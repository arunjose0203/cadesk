import React, { useState, useEffect } from 'react'
import {
  trialBalanceApi,
  api,
  type TrialBalanceImportDto,
  type TrialBalanceLedgerDto,
  type ClientLedgerMappingDto,
  type SyncTrialBalanceResultDto,
  type Client
} from './api'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 2 }).format(n || 0)

const SCHEDULE3_GROUPS = [
  'RevenueFromOperations',
  'OtherIncome',
  'CostOfMaterialsConsumed',
  'EmployeeBenefitsExpense',
  'FinanceCosts',
  'DepreciationAndAmortization',
  'OtherExpenses',
  'ShareCapital',
  'ReservesAndSurplus',
  'LongTermBorrowings',
  'ShortTermBorrowings',
  'TradePayables',
  'OtherCurrentLiabilities',
  'ShortTermProvisions',
  'PropertyPlantEquipment',
  'IntangibleAssets',
  'CapitalWorkInProgress',
  'NonCurrentInvestments',
  'Inventories',
  'TradeReceivables',
  'CashAndBankBalances',
  'ShortTermLoansAndAdvances',
  'OtherCurrentAssets'
]

const FORM3CD_CATEGORIES = [
  { value: '', label: 'None' },
  { value: 'TurnoverGrossReceipts', label: 'Clause 8a / 12: Turnover & Gross Receipts' },
  { value: 'CashReceipts', label: 'Clause 8b: Cash Receipts' },
  { value: 'Sec43BDisallowance', label: 'Clause 26: Section 43B Disallowance' },
  { value: 'Sec43BhMsmDisallowance', label: 'Clause 22: Section 43B(h) MSME Disallowance' },
  { value: 'Sec40A3Disallowance', label: 'Clause 21d: Section 40A(3) Cash Payment Disallowance' }
]

const CARO_CATEGORIES = [
  { value: '', label: 'None' },
  { value: 'BorrowingsFromBanks', label: 'Clause (ii)(b): Working Capital > ₹5 Cr' },
  { value: 'WorkingCapitalSanctionLimits', label: 'Clause (ix)(a): Borrowing Defaults' }
]

const FIXED_ASSET_BLOCKS = [
  { value: '', label: 'None' },
  { value: 'PlantAndMachinery', label: 'Plant & Machinery (15%)' },
  { value: 'ComputersAndSoftware', label: 'Computers & Software (40%)' },
  { value: 'Buildings', label: 'Buildings (10%)' },
  { value: 'FurnitureAndFixtures', label: 'Furniture & Fixtures (10%)' },
  { value: 'Vehicles', label: 'Motor Vehicles (15% / 30%)' }
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

  // Upload Form
  const [uploadSourceType, setUploadSourceType] = useState<string>('TallyXml')
  const [uploadFileName, setUploadFileName] = useState<string>('trial_balance.xml')
  const [uploadPayload, setUploadPayload] = useState<string>('')

  // Inline Editing
  const [editingLedgerId, setEditingLedgerId] = useState<string | null>(null)
  const [editForm, setEditForm] = useState({
    schedule3Group: 'OtherExpenses',
    form3CdCategory: '',
    caroCategory: '',
    fixedAssetBlock: '',
    saveAsClientDefaultRule: true
  })

  useEffect(() => {
    loadClients()
  }, [])

  useEffect(() => {
    if (selectedClientId) {
      loadImports(selectedClientId, selectedFy)
    }
  }, [selectedClientId, selectedFy])

  async function loadClients() {
    try {
      const res = await api<Client[]>('/api/clients')
      setClients(res || [])
      if (res && res.length > 0 && !selectedClientId) {
        setSelectedClientId(res[0].id)
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load client list.')
    }
  }

  async function loadImports(clientId: string, fy: string) {
    try {
      setLoading(true)
      setError(null)
      const list = await trialBalanceApi.listImports(clientId, fy)
      setImports(list || [])
      if (list && list.length > 0) {
        // Load details of the latest import
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

  async function handleUpload(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedClientId) {
      setError('Please select a client first.')
      return
    }
    if (!uploadPayload.trim()) {
      setError('Please provide or paste file contents.')
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
      const res = await trialBalanceApi.syncAllDesks(activeImport.id)
      setSyncResult(res)
      setShowSyncModal(true)
      setSuccess('Trial Balance figures synchronized down to all 5 desks!')
      // Refresh active import status
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

      // Update state in active import
      setActiveImport(prev => {
        if (!prev) return null
        const updatedLedgers = prev.ledgers.map(l => (l.id === ledgerId ? updated : l))
        const mappedCount = updatedLedgers.filter(l => l.isMapped).length
        return {
          ...prev,
          mappedLedgersCount: mappedCount,
          ledgers: updatedLedgers
        }
      })
      setEditingLedgerId(null)
      setSuccess(`Mapping updated for ledger: ${updated.ledgerName}`)
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

  function handleLoadSampleTallyXml() {
    setUploadSourceType('TallyXml')
    setUploadFileName('Tally_TB_Sample.xml')
    setUploadPayload(`<?xml version="1.0" encoding="utf-8"?>
<ENVELOPE>
    <HEADER><TALLYREQUEST>Export Data</TALLYREQUEST></HEADER>
    <BODY>
        <DATA>
            <COLLECTION>
                <LEDGER NAME="Equity Share Capital">
                    <PARENT>Capital Account</PARENT>
                    <OPENINGBALANCE>-2500000.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>-2500000.00</CLOSINGBALANCE>
                </LEDGER>
                <LEDGER NAME="Domestic Sales of Goods">
                    <PARENT>Sales Accounts</PARENT>
                    <OPENINGBALANCE>0.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>-12500000.00</CLOSINGBALANCE>
                </LEDGER>
                <LEDGER NAME="Purchases - Raw Materials">
                    <PARENT>Purchase Accounts</PARENT>
                    <OPENINGBALANCE>0.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>7800000.00</CLOSINGBALANCE>
                </LEDGER>
                <LEDGER NAME="Salaries and Wages">
                    <PARENT>Direct Expenses</PARENT>
                    <OPENINGBALANCE>0.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>1650000.00</CLOSINGBALANCE>
                </LEDGER>
                <LEDGER NAME="Office Rent &amp; Maintenance">
                    <PARENT>Indirect Expenses</PARENT>
                    <OPENINGBALANCE>0.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>450000.00</CLOSINGBALANCE>
                </LEDGER>
                <LEDGER NAME="Plant and Machinery">
                    <PARENT>Fixed Assets</PARENT>
                    <OPENINGBALANCE>3200000.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>3200000.00</CLOSINGBALANCE>
                </LEDGER>
                <LEDGER NAME="Computers &amp; Laptops">
                    <PARENT>Fixed Assets</PARENT>
                    <OPENINGBALANCE>600000.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>600000.00</CLOSINGBALANCE>
                </LEDGER>
                <LEDGER NAME="Trade Debtors">
                    <PARENT>Sundry Debtors</PARENT>
                    <OPENINGBALANCE>1100000.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>1450000.00</CLOSINGBALANCE>
                </LEDGER>
                <LEDGER NAME="Trade Creditors">
                    <PARENT>Sundry Creditors</PARENT>
                    <OPENINGBALANCE>-950000.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>-1150000.00</CLOSINGBALANCE>
                </LEDGER>
                <LEDGER NAME="HDFC Current Account">
                    <PARENT>Bank Accounts</PARENT>
                    <OPENINGBALANCE>500000.00</OPENINGBALANCE>
                    <CLOSINGBALANCE>900000.00</CLOSINGBALANCE>
                </LEDGER>
            </COLLECTION>
        </DATA>
    </BODY>
</ENVELOPE>`)
  }

  function handleFileUploadChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0]
    if (!file) return
    setUploadFileName(file.name)
    if (file.name.endsWith('.csv')) {
      setUploadSourceType('Csv')
    } else {
      setUploadSourceType('TallyXml')
    }
    const reader = new FileReader()
    reader.onload = evt => {
      const content = evt.target?.result as string
      setUploadPayload(content || '')
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
            <h1 style={{ margin: 0, fontSize: '24px', fontWeight: 700, color: '#111827' }}>
              Trial Balance & Multi-Desk Hub
            </h1>
            <span style={{
              background: '#EEF2FF',
              color: '#4F46E5',
              fontSize: '12px',
              fontWeight: 600,
              padding: '2px 10px',
              borderRadius: '9999px',
              border: '1px solid #C7D2FE'
            }}>
              Cascading Engine
            </span>
          </div>
          <p style={{ margin: '4px 0 0 0', color: '#6B7280', fontSize: '14px' }}>
            Import Tally TB once per client. One sync automatically fills Statutory Audit, Form 3CD, Schedule III Ratios, CARO 2020, and Fixed Assets.
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
              border: '1px solid #D1D5DB',
              background: '#FFFFFF',
              color: '#374151',
              fontWeight: 500,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '6px'
            }}
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path><polyline points="14 2 14 8 20 8"></polyline><line x1="16" y1="13" x2="8" y2="13"></line><line x1="16" y1="17" x2="8" y2="17"></line><polyline points="10 9 9 9 8 9"></polyline></svg>
            Retained Rules
          </button>

          {(!can || can('trialbalance.manage')) && (
            <>
              <button
                onClick={() => setShowUploadModal(true)}
                style={{
                  padding: '8px 16px',
                  borderRadius: '6px',
                  border: '1px solid #D1D5DB',
                  background: '#F9FAFB',
                  color: '#111827',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px'
                }}
              >
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path><polyline points="17 8 12 3 7 8"></polyline><line x1="12" y1="3" x2="12" y2="15"></line></svg>
                + Import Tally / CSV
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
                Sync to All Desks
              </button>
            </>
          )}
        </div>
      </div>

      {/* Alerts */}
      {error && (
        <div style={{ background: '#FEF2F2', border: '1px solid #FCA5A5', color: '#991B1B', padding: '12px 16px', borderRadius: '8px', marginBottom: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span>{error}</span>
          <button onClick={() => setError(null)} style={{ background: 'transparent', border: 'none', color: '#991B1B', cursor: 'pointer', fontWeight: 600 }}>✕</button>
        </div>
      )}

      {success && (
        <div style={{ background: '#ECFDF5', border: '1px solid #6EE7B7', color: '#065F46', padding: '12px 16px', borderRadius: '8px', marginBottom: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <span>{success}</span>
          <button onClick={() => setSuccess(null)} style={{ background: 'transparent', border: 'none', color: '#065F46', cursor: 'pointer', fontWeight: 600 }}>✕</button>
        </div>
      )}

      {/* Selectors Bar */}
      <div style={{ background: '#FFFFFF', border: '1px solid #E5E7EB', borderRadius: '8px', padding: '16px', marginBottom: '20px', display: 'flex', alignItems: 'center', gap: '20px', flexWrap: 'wrap' }}>
        <div style={{ minWidth: '280px', flex: 1 }}>
          <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#4B5563', marginBottom: '6px' }}>Client</label>
          <select
            value={selectedClientId}
            onChange={e => setSelectedClientId(e.target.value)}
            style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #D1D5DB', fontSize: '14px', background: '#FFFFFF' }}
          >
            {clients.map(c => (
              <option key={c.id} value={c.id}>
                {c.name} {c.taxIds?.['PAN'] ? `(${c.taxIds['PAN']})` : ''}
              </option>
            ))}
          </select>
        </div>

        <div style={{ minWidth: '160px' }}>
          <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#4B5563', marginBottom: '6px' }}>Financial Year</label>
          <select
            value={selectedFy}
            onChange={e => setSelectedFy(e.target.value)}
            style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #D1D5DB', fontSize: '14px', background: '#FFFFFF' }}
          >
            <option value="2024-25">2024-25 (AY 2025-26)</option>
            <option value="2023-24">2023-24 (AY 2024-25)</option>
            <option value="2022-23">2022-23 (AY 2023-24)</option>
            <option value="2021-22">2021-22 (AY 2022-23)</option>
          </select>
        </div>

        {imports.length > 1 && (
          <div style={{ minWidth: '220px' }}>
            <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#4B5563', marginBottom: '6px' }}>Select Import Version</label>
            <select
              value={activeImport?.id || ''}
              onChange={e => handleSelectImport(e.target.value)}
              style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #D1D5DB', fontSize: '14px', background: '#FFFFFF' }}
            >
              {imports.map(imp => (
                <option key={imp.id} value={imp.id}>
                  {imp.fileName} ({new Date(imp.uploadedAt).toLocaleDateString()})
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* KPI Cards */}
      {activeImport ? (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          {/* Total Debit */}
          <div style={{ background: '#FFFFFF', border: '1px solid #E5E7EB', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 600, color: '#6B7280', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              Total Debits
            </div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#111827', marginTop: '6px' }}>
              {fmtInr(activeImport.totalDebit)}
            </div>
            <div style={{ fontSize: '12px', color: '#9CA3AF', marginTop: '4px' }}>
              From {activeImport.totalLedgersCount} ledgers
            </div>
          </div>

          {/* Total Credit */}
          <div style={{ background: '#FFFFFF', border: '1px solid #E5E7EB', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 600, color: '#6B7280', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              Total Credits
            </div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#111827', marginTop: '6px' }}>
              {fmtInr(activeImport.totalCredit)}
            </div>
            <div style={{ fontSize: '12px', color: '#9CA3AF', marginTop: '4px' }}>
              Closing balance net sum
            </div>
          </div>

          {/* Balanced Status */}
          <div style={{ background: '#FFFFFF', border: '1px solid #E5E7EB', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 600, color: '#6B7280', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              TB Health Status
            </div>
            <div style={{ marginTop: '8px' }}>
              {activeImport.isBalanced ? (
                <span style={{ background: '#DEF7EC', color: '#03543F', padding: '4px 10px', borderRadius: '6px', fontSize: '13px', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                  ✓ Balanced (₹0 Diff)
                </span>
              ) : (
                <span style={{ background: '#FDE8E8', color: '#9B1C1C', padding: '4px 10px', borderRadius: '6px', fontSize: '13px', fontWeight: 600 }}>
                  ✕ Out of Balance (Diff: {fmtInr(Math.abs(activeImport.totalDebit - activeImport.totalCredit))})
                </span>
              )}
            </div>
            <div style={{ fontSize: '12px', color: '#9CA3AF', marginTop: '6px' }}>
              {activeImport.sourceType} • {activeImport.fileName}
            </div>
          </div>

          {/* Mapping Progress */}
          <div style={{ background: '#FFFFFF', border: '1px solid #E5E7EB', borderRadius: '8px', padding: '16px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ fontSize: '12px', fontWeight: 600, color: '#6B7280', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Classification
              </span>
              <span style={{ fontSize: '12px', fontWeight: 700, color: '#4F46E5' }}>{mappedPercent}%</span>
            </div>
            <div style={{ fontSize: '20px', fontWeight: 700, color: '#111827', marginTop: '4px' }}>
              {activeImport.mappedLedgersCount} / {activeImport.totalLedgersCount}
            </div>
            <div style={{ width: '100%', height: '6px', background: '#E5E7EB', borderRadius: '9999px', marginTop: '8px', overflow: 'hidden' }}>
              <div style={{ width: `${mappedPercent}%`, height: '100%', background: '#4F46E5', borderRadius: '9999px' }} />
            </div>
          </div>

          {/* Sync Status */}
          <div style={{ background: '#FFFFFF', border: '1px solid #E5E7EB', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', fontWeight: 600, color: '#6B7280', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              Multi-Desk Sync
            </div>
            <div style={{ marginTop: '8px' }}>
              {activeImport.isSynced ? (
                <span style={{ background: '#E0E7FF', color: '#3730A3', padding: '4px 10px', borderRadius: '6px', fontSize: '13px', fontWeight: 600 }}>
                  ✓ Synced across 5 desks
                </span>
              ) : (
                <span style={{ background: '#FEF3C7', color: '#92400E', padding: '4px 10px', borderRadius: '6px', fontSize: '13px', fontWeight: 600 }}>
                  Pending Cascading Sync
                </span>
              )}
            </div>
            <div style={{ fontSize: '12px', color: '#9CA3AF', marginTop: '6px' }}>
              {activeImport.lastSyncedAt ? `Last: ${new Date(activeImport.lastSyncedAt).toLocaleString()}` : 'Never synced'}
            </div>
          </div>
        </div>
      ) : (
        <div style={{ background: '#FFFFFF', border: '1px dashed #D1D5DB', borderRadius: '12px', padding: '48px', textAlign: 'center', marginBottom: '24px' }}>
          <div style={{ width: '56px', height: '56px', borderRadius: '50%', background: '#EEF2FF', color: '#4F46E5', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 16px', fontSize: '24px' }}>
            📊
          </div>
          <h3 style={{ margin: '0 0 8px 0', fontSize: '18px', fontWeight: 600, color: '#111827' }}>
            No Trial Balance imported for {selectedFy}
          </h3>
          <p style={{ margin: '0 0 20px 0', color: '#6B7280', fontSize: '14px', maxWidth: '500px', marginLeft: 'auto', marginRight: 'auto' }}>
            Export the Trial Balance from TallyPrime (XML or CSV) and upload it here. CaDesk will automatically map standard ledgers and propagate data to all downstream audit and tax desks.
          </p>
          <button
            onClick={() => setShowUploadModal(true)}
            style={{
              padding: '10px 20px',
              borderRadius: '6px',
              border: 'none',
              background: '#4F46E5',
              color: '#FFFFFF',
              fontWeight: 600,
              cursor: 'pointer',
              fontSize: '14px'
            }}
          >
            + Upload Tally Trial Balance
          </button>
        </div>
      )}

      {/* Ledgers Workbench Table */}
      {activeImport && (
        <div style={{ background: '#FFFFFF', border: '1px solid #E5E7EB', borderRadius: '8px', overflow: 'hidden' }}>
          {/* Table Toolbar */}
          <div style={{ padding: '16px', borderBottom: '1px solid #E5E7EB', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '12px' }}>
            <div style={{ display: 'flex', gap: '12px', alignItems: 'center', flex: 1, minWidth: '300px' }}>
              <input
                type="text"
                placeholder="Search ledger name, group or Schedule III..."
                value={searchLedger}
                onChange={e => setSearchLedger(e.target.value)}
                style={{ padding: '8px 12px', borderRadius: '6px', border: '1px solid #D1D5DB', fontSize: '14px', width: '320px' }}
              />
              <div style={{ display: 'flex', background: '#F3F4F6', borderRadius: '6px', padding: '2px' }}>
                <button
                  onClick={() => setMappingFilter('all')}
                  style={{
                    padding: '6px 12px',
                    borderRadius: '4px',
                    border: 'none',
                    background: mappingFilter === 'all' ? '#FFFFFF' : 'transparent',
                    color: mappingFilter === 'all' ? '#111827' : '#6B7280',
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
                    background: mappingFilter === 'mapped' ? '#FFFFFF' : 'transparent',
                    color: mappingFilter === 'mapped' ? '#111827' : '#6B7280',
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
                    background: mappingFilter === 'unmapped' ? '#FFFFFF' : 'transparent',
                    color: mappingFilter === 'unmapped' ? '#111827' : '#6B7280',
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

            <div style={{ fontSize: '13px', color: '#6B7280' }}>
              Showing {filteredLedgers.length} of {activeImport.ledgers.length} ledgers
            </div>
          </div>

          {/* Table Content */}
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: '#F9FAFB', borderBottom: '1px solid #E5E7EB', color: '#4B5563', fontWeight: 600 }}>
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
                        background: isEditing ? '#F0FDF4' : 'transparent',
                        transition: 'background 0.15s ease'
                      }}
                    >
                      {/* Name & Parent Group */}
                      <td style={{ padding: '12px 16px' }}>
                        <div style={{ fontWeight: 600, color: '#111827' }}>{ledger.ledgerName}</div>
                        <div style={{ fontSize: '11px', color: '#6B7280', marginTop: '2px' }}>
                          Parent: <span style={{ fontWeight: 500 }}>{ledger.tallyGroup || 'Primary'}</span>
                          {ledger.isMapped ? (
                            <span style={{ marginLeft: '6px', color: '#059669', fontWeight: 600 }}>✓ Mapped</span>
                          ) : (
                            <span style={{ marginLeft: '6px', color: '#D97706', fontWeight: 500 }}>• Unmapped</span>
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
                      <td style={{ padding: '12px 16px', textAlign: 'right', fontFamily: 'monospace', fontWeight: 600, color: ledger.closingBalance >= 0 ? '#111827' : '#991B1B' }}>
                        {fmtInr(ledger.closingBalance)}
                      </td>

                      {/* Schedule III Group */}
                      <td style={{ padding: '12px 16px' }}>
                        {isEditing ? (
                          <select
                            value={editForm.schedule3Group}
                            onChange={e => setEditForm({ ...editForm, schedule3Group: e.target.value })}
                            style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid #D1D5DB', fontSize: '12px', width: '100%' }}
                          >
                            {SCHEDULE3_GROUPS.map(g => (
                              <option key={g} value={g}>{g}</option>
                            ))}
                          </select>
                        ) : (
                          <span style={{ background: '#F3F4F6', color: '#374151', padding: '2px 8px', borderRadius: '4px', fontSize: '12px' }}>
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
                            style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid #D1D5DB', fontSize: '12px', width: '100%' }}
                          >
                            {FORM3CD_CATEGORIES.map(c => (
                              <option key={c.value} value={c.value}>{c.label}</option>
                            ))}
                          </select>
                        ) : (
                          <span style={{ fontSize: '12px', color: ledger.form3CdCategory ? '#1E40AF' : '#9CA3AF' }}>
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
                              style={{ padding: '4px 6px', borderRadius: '4px', border: '1px solid #D1D5DB', fontSize: '11px' }}
                            >
                              {CARO_CATEGORIES.map(c => (
                                <option key={c.value} value={c.value}>CARO: {c.label}</option>
                              ))}
                            </select>
                            <select
                              value={editForm.fixedAssetBlock}
                              onChange={e => setEditForm({ ...editForm, fixedAssetBlock: e.target.value })}
                              style={{ padding: '4px 6px', borderRadius: '4px', border: '1px solid #D1D5DB', fontSize: '11px' }}
                            >
                              {FIXED_ASSET_BLOCKS.map(b => (
                                <option key={b.value} value={b.value}>Asset: {b.label}</option>
                              ))}
                            </select>
                            <label style={{ fontSize: '11px', display: 'flex', alignItems: 'center', gap: '4px', marginTop: '2px', color: '#065F46' }}>
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
                              <div style={{ color: '#047857', fontWeight: 500 }}>
                                Block: {ledger.fixedAssetBlock}
                              </div>
                            )}
                            {ledger.caroCategory && (
                              <div style={{ color: '#B45309', fontSize: '11px' }}>
                                CARO: {ledger.caroCategory}
                              </div>
                            )}
                            {!ledger.fixedAssetBlock && !ledger.caroCategory && <span style={{ color: '#9CA3AF' }}>—</span>}
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
                              style={{ padding: '4px 8px', borderRadius: '4px', background: '#E5E7EB', color: '#374151', border: 'none', fontSize: '11px', cursor: 'pointer' }}
                            >
                              ✕
                            </button>
                          </div>
                        ) : (
                          <button
                            onClick={() => startEdit(ledger)}
                            style={{ padding: '4px 10px', borderRadius: '4px', background: '#F3F4F6', color: '#374151', border: '1px solid #D1D5DB', fontSize: '11px', fontWeight: 500, cursor: 'pointer' }}
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

      {/* Upload Modal */}
      {showUploadModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div style={{ background: '#FFFFFF', padding: '24px', borderRadius: '12px', width: '650px', maxWidth: '95%', maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: '#111827' }}>
                Import Trial Balance from Tally
              </h3>
              <button onClick={() => setShowUploadModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '18px', cursor: 'pointer' }}>✕</button>
            </div>

            <p style={{ margin: '0 0 16px 0', fontSize: '13px', color: '#6B7280' }}>
              Upload an exported Tally XML file or CSV statement. CaDesk parses the opening, debit, credit and closing balance figures and automatically maps common ledgers.
            </p>

            <form onSubmit={handleUpload}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#374151', marginBottom: '4px' }}>Source Format</label>
                  <select
                    value={uploadSourceType}
                    onChange={e => setUploadSourceType(e.target.value)}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #D1D5DB', fontSize: '13px' }}
                  >
                    <option value="TallyXml">Tally XML Export (Collection / Envelope)</option>
                    <option value="Csv">CSV / Excel Export</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#374151', marginBottom: '4px' }}>File Name</label>
                  <input
                    type="text"
                    value={uploadFileName}
                    onChange={e => setUploadFileName(e.target.value)}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #D1D5DB', fontSize: '13px' }}
                    required
                  />
                </div>
              </div>

              {/* File Dropzone */}
              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#374151', marginBottom: '4px' }}>
                  Select File or Drag & Drop
                </label>
                <div style={{ border: '2px dashed #D1D5DB', borderRadius: '8px', padding: '16px', textAlign: 'center', background: '#F9FAFB' }}>
                  <input
                    type="file"
                    accept=".xml,.csv,.txt"
                    onChange={handleFileUploadChange}
                    style={{ display: 'none' }}
                    id="tb-file-input"
                  />
                  <label htmlFor="tb-file-input" style={{ cursor: 'pointer', color: '#4F46E5', fontWeight: 600, fontSize: '13px' }}>
                    Browse XML or CSV file
                  </label>
                  <span style={{ fontSize: '12px', color: '#6B7280', display: 'block', marginTop: '4px' }}>
                    or paste XML text in the field below
                  </span>
                </div>
              </div>

              {/* Text Payload */}
              <div style={{ marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#374151' }}>File Content / XML</label>
                  <button
                    type="button"
                    onClick={handleLoadSampleTallyXml}
                    style={{ background: 'transparent', border: 'none', color: '#4F46E5', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
                  >
                    Load Sample Tally XML
                  </button>
                </div>
                <textarea
                  rows={8}
                  value={uploadPayload}
                  onChange={e => setUploadPayload(e.target.value)}
                  placeholder="Paste Tally <ENVELOPE> or CSV contents here..."
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #D1D5DB', fontSize: '12px', fontFamily: 'monospace' }}
                  required
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowUploadModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid #D1D5DB', background: '#FFFFFF', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '6px', border: 'none', background: '#4F46E5', color: '#FFFFFF', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Processing...' : 'Upload & Parse'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Sync Success Modal */}
      {showSyncModal && syncResult && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div style={{ background: '#FFFFFF', padding: '24px', borderRadius: '12px', width: '560px', maxWidth: '95%' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '16px' }}>
              <div style={{ width: '40px', height: '40px', borderRadius: '50%', background: '#DEF7EC', color: '#03543F', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '20px' }}>
                ✓
              </div>
              <div>
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: '#111827' }}>
                  Multi-Desk Synchronization Complete!
                </h3>
                <span style={{ fontSize: '13px', color: '#6B7280' }}>
                  Financial Year: {syncResult.financialYear}
                </span>
              </div>
            </div>

            <p style={{ fontSize: '13px', color: '#374151', lineHeight: '1.5', background: '#F9FAFB', padding: '12px', borderRadius: '8px', border: '1px solid #E5E7EB' }}>
              {syncResult.summary}
            </p>

            <div style={{ marginTop: '16px', display: 'flex', flexDirection: 'column', gap: '10px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 12px', background: '#F3F4F6', borderRadius: '6px' }}>
                <span style={{ fontSize: '13px', fontWeight: 500 }}>Statutory Audit Desk (SA Materiality)</span>
                <span style={{ color: syncResult.statutoryAuditUpdated ? '#059669' : '#6B7280', fontWeight: 600, fontSize: '12px' }}>
                  {syncResult.statutoryAuditUpdated ? '✓ Benchmark Updated' : 'No Active Engagement'}
                </span>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 12px', background: '#F3F4F6', borderRadius: '6px' }}>
                <span style={{ fontSize: '13px', fontWeight: 500 }}>Schedule III Desk (11 Ratios)</span>
                <span style={{ color: syncResult.schedule3Updated ? '#059669' : '#6B7280', fontWeight: 600, fontSize: '12px' }}>
                  {syncResult.schedule3Updated ? '✓ 11 Ratios Calculated' : 'Updated'}
                </span>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 12px', background: '#F3F4F6', borderRadius: '6px' }}>
                <span style={{ fontSize: '13px', fontWeight: 500 }}>Form 3CD Tax Audit Desk</span>
                <span style={{ color: syncResult.form3CdUpdated ? '#059669' : '#6B7280', fontWeight: 600, fontSize: '12px' }}>
                  {syncResult.form3CdUpdated ? '✓ Turnover & 43B Disallowances Populated' : 'Updated'}
                </span>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 12px', background: '#F3F4F6', borderRadius: '6px' }}>
                <span style={{ fontSize: '13px', fontWeight: 500 }}>CARO 2020 Desk</span>
                <span style={{ color: syncResult.caroUpdated ? '#059669' : '#6B7280', fontWeight: 600, fontSize: '12px' }}>
                  {syncResult.caroUpdated ? '✓ Borrowings Evaluated' : 'Updated'}
                </span>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '8px 12px', background: '#F3F4F6', borderRadius: '6px' }}>
                <span style={{ fontSize: '13px', fontWeight: 500 }}>Fixed Assets Desk</span>
                <span style={{ color: '#059669', fontWeight: 600, fontSize: '12px' }}>
                  ✓ {syncResult.fixedAssetsCreatedCount} Asset Blocks Populated
                </span>
              </div>
            </div>

            <div style={{ marginTop: '20px', display: 'flex', justifyContent: 'flex-end' }}>
              <button
                onClick={() => setShowSyncModal(false)}
                style={{ padding: '8px 20px', borderRadius: '6px', border: 'none', background: '#4F46E5', color: '#FFFFFF', fontWeight: 600, cursor: 'pointer' }}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Retained Rules Modal */}
      {showRulesModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div style={{ background: '#FFFFFF', padding: '24px', borderRadius: '12px', width: '700px', maxWidth: '95%', maxHeight: '85vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div>
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: '#111827' }}>
                  Client Retained Mapping Rules
                </h3>
                <span style={{ fontSize: '12px', color: '#6B7280' }}>
                  These rules are automatically applied every year when a new Trial Balance is uploaded for this client.
                </span>
              </div>
              <button onClick={() => setShowRulesModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '18px', cursor: 'pointer' }}>✕</button>
            </div>

            {clientRules.length === 0 ? (
              <div style={{ padding: '32px', textAlign: 'center', color: '#6B7280' }}>
                No custom default rules retained yet. When you edit a ledger mapping and check "Remember rule for future FYs", it will appear here.
              </div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px' }}>
                <thead>
                  <tr style={{ background: '#F9FAFB', borderBottom: '1px solid #E5E7EB', textAlign: 'left' }}>
                    <th style={{ padding: '10px 12px' }}>Ledger Name</th>
                    <th style={{ padding: '10px 12px' }}>Schedule III</th>
                    <th style={{ padding: '10px 12px' }}>Form 3CD</th>
                    <th style={{ padding: '10px 12px' }}>CARO / Asset</th>
                  </tr>
                </thead>
                <tbody>
                  {clientRules.map(r => (
                    <tr key={r.id} style={{ borderBottom: '1px solid #F3F4F6' }}>
                      <td style={{ padding: '10px 12px', fontWeight: 600 }}>{r.ledgerName}</td>
                      <td style={{ padding: '10px 12px' }}>{r.schedule3Group}</td>
                      <td style={{ padding: '10px 12px', color: '#1E40AF' }}>{r.form3CdCategory || '—'}</td>
                      <td style={{ padding: '10px 12px', color: '#047857' }}>{r.fixedAssetBlock || r.caroCategory || '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            <div style={{ marginTop: '20px', display: 'flex', justifyContent: 'flex-end' }}>
              <button
                onClick={() => setShowRulesModal(false)}
                style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid #D1D5DB', background: '#FFFFFF', cursor: 'pointer' }}
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
