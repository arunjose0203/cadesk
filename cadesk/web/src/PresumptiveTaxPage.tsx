import { useCallback, useEffect, useState } from 'react'
import {
  type PresumptiveTaxComputationDto,
  type PresumptiveSummaryDto,
  type MinimalFinancialsStatementDto,
  type IncomeTaxJsonExportResult,
  type ExportItrJsonRequest,
  type Client,
  presumptiveApi,
  api
} from './api'
import { JsonExportPanel } from './components/JsonExportPanel'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n || 0)

type ClientPage = {
  items: Client[]
  total: number
}

export function PresumptiveTaxPage({ can }: { can: (p: string) => boolean }) {
  const [computations, setComputations] = useState<PresumptiveTaxComputationDto[]>([])
  const [summary, setSummary] = useState<PresumptiveSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Filters
  const [filterFy, setFilterFy] = useState('2025-26')
  const [filterSection, setFilterSection] = useState('all')
  const [filterStatus, setFilterStatus] = useState('all')

  // Selected for editing / details
  const [selectedComp, setSelectedComp] = useState<PresumptiveTaxComputationDto | null>(null)
  const [activeTab, setActiveTab] = useState<'computation' | 'minimal_financials' | 'statement'>('computation')
  const [statement, setStatement] = useState<MinimalFinancialsStatementDto | null>(null)
  const [itr4Export, setItr4Export] = useState<IncomeTaxJsonExportResult | null>(null)
  const [exportingItr4, setExportingItr4] = useState(false)
  const [itr4Opts, setItr4Opts] = useState<ExportItrJsonRequest>({
    fatherName: '',
    city: 'Kochi',
    pinCode: '682001',
    optOutNewRegime: false,
  })

  // Modals
  const [showNewModal, setShowNewModal] = useState(false)
  const [showAddVehicleModal, setShowAddVehicleModal] = useState(false)

  // New Computation Form
  const [form, setForm] = useState({
    clientId: '',
    financialYear: '2025-26',
    section: '44AD',
    entityType: 'Individual',
    businessNatureCode: '01001',
    tradeName: '',
    digitalTurnover: 0,
    cashTurnover: 0,
    declaredProfit: 0,
    optedOutPriorYear: false,
    optOutAssessmentYear: '2022-23',
    sundryDebtors: 0,
    sundryCreditors: 0,
    stockInTrade: 0,
    cashBalance: 0,
    bankBalance: 0,
    fixedAssets: 0,
    otherAssets: 0,
    partnerOrProprietorCapital: 0,
    loansAndBorrowings: 0,
    notes: ''
  })

  // Vehicle form (44AE)
  const [vehicleForm, setVehicleForm] = useState({
    registrationNumber: '',
    vehicleType: 'other',
    grossVehicleWeightTons: 0,
    monthsOwned: 12,
    declaredProfit: 0
  })

  const loadData = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [list, sum] = await Promise.all([
        presumptiveApi.getComputations(undefined, filterFy, filterSection, filterStatus),
        presumptiveApi.getSummary()
      ])
      setComputations(list)
      setSummary(sum)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load presumptive tax computations.')
    } finally {
      setLoading(false)
    }
  }, [filterFy, filterSection, filterStatus])

  const loadClients = useCallback(async () => {
    try {
      const res = await api<ClientPage>('/api/clients?take=200')
      setClients(res.items || [])
    } catch {
      // Ignore
    }
  }, [])

  useEffect(() => {
    loadData()
    loadClients()
  }, [loadData, loadClients])

  async function handleOpenDetail(comp: PresumptiveTaxComputationDto) {
    setSelectedComp(comp)
    setActiveTab('computation')
    try {
      const stmt = await presumptiveApi.getStatement(comp.id)
      setStatement(stmt)
    } catch {
      // Ignore
    }
  }

  async function handleCreateComputation(e: React.FormEvent) {
    e.preventDefault()
    if (!form.clientId) {
      alert('Please select a client.')
      return
    }

    try {
      const created = await presumptiveApi.createComputation({
        clientId: form.clientId,
        financialYear: form.financialYear,
        section: form.section,
        entityType: form.entityType,
        businessNatureCode: form.businessNatureCode,
        tradeName: form.tradeName,
        grossTurnover: Number(form.digitalTurnover) + Number(form.cashTurnover),
        digitalTurnover: Number(form.digitalTurnover),
        cashTurnover: Number(form.cashTurnover),
        declaredProfit: Number(form.declaredProfit),
        optedOutPriorYear: form.optedOutPriorYear,
        optOutAssessmentYear: form.optedOutPriorYear ? form.optOutAssessmentYear : null,
        sundryDebtors: Number(form.sundryDebtors),
        sundryCreditors: Number(form.sundryCreditors),
        stockInTrade: Number(form.stockInTrade),
        cashBalance: Number(form.cashBalance),
        bankBalance: Number(form.bankBalance),
        fixedAssets: Number(form.fixedAssets),
        otherAssets: Number(form.otherAssets),
        partnerOrProprietorCapital: Number(form.partnerOrProprietorCapital),
        loansAndBorrowings: Number(form.loansAndBorrowings),
        notes: form.notes
      })
      setShowNewModal(false)
      await loadData()
      handleOpenDetail(created)
      alert('Presumptive computation created successfully!')
    } catch (err: unknown) {
      alert(`Failed to create computation: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleUpdateSelected(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedComp) return

    try {
      const updated = await presumptiveApi.updateComputation(selectedComp.id, {
        clientId: selectedComp.clientId,
        financialYear: selectedComp.financialYear,
        section: selectedComp.section,
        entityType: selectedComp.entityType,
        businessNatureCode: selectedComp.businessNatureCode,
        tradeName: selectedComp.tradeName,
        grossTurnover: Number(selectedComp.digitalTurnover) + Number(selectedComp.cashTurnover),
        digitalTurnover: Number(selectedComp.digitalTurnover),
        cashTurnover: Number(selectedComp.cashTurnover),
        declaredProfit: Number(selectedComp.declaredProfit),
        optedOutPriorYear: selectedComp.optedOutPriorYear,
        optOutAssessmentYear: selectedComp.optedOutPriorYear ? selectedComp.optOutAssessmentYear : null,
        sundryDebtors: Number(selectedComp.sundryDebtors),
        sundryCreditors: Number(selectedComp.sundryCreditors),
        stockInTrade: Number(selectedComp.stockInTrade),
        cashBalance: Number(selectedComp.cashBalance),
        bankBalance: Number(selectedComp.bankBalance),
        fixedAssets: Number(selectedComp.fixedAssets),
        otherAssets: Number(selectedComp.otherAssets),
        partnerOrProprietorCapital: Number(selectedComp.partnerOrProprietorCapital),
        loansAndBorrowings: Number(selectedComp.loansAndBorrowings),
        notes: selectedComp.notes
      })
      setSelectedComp(updated)
      const stmt = await presumptiveApi.getStatement(updated.id)
      setStatement(stmt)
      await loadData()
      alert('Computation updated & recalculated successfully!')
    } catch (err: unknown) {
      alert(`Update failed: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleAddVehicle(e: React.FormEvent) {
    e.preventDefault()
    if (!selectedComp) return
    if (!vehicleForm.registrationNumber) {
      alert('Vehicle registration number is required.')
      return
    }

    try {
      const updated = await presumptiveApi.addVehicle(selectedComp.id, {
        registrationNumber: vehicleForm.registrationNumber,
        vehicleType: vehicleForm.vehicleType,
        grossVehicleWeightTons: Number(vehicleForm.grossVehicleWeightTons),
        monthsOwned: Number(vehicleForm.monthsOwned),
        declaredProfit: Number(vehicleForm.declaredProfit)
      })
      setSelectedComp(updated)
      setShowAddVehicleModal(false)
      setVehicleForm({
        registrationNumber: '',
        vehicleType: 'other',
        grossVehicleWeightTons: 0,
        monthsOwned: 12,
        declaredProfit: 0
      })
      await loadData()
      alert('Goods carriage vehicle added successfully!')
    } catch (err: unknown) {
      alert(`Failed to add vehicle: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleRemoveVehicle(vehicleId: string) {
    if (!selectedComp || !confirm('Remove this vehicle from computation?')) return
    try {
      const updated = await presumptiveApi.removeVehicle(selectedComp.id, vehicleId)
      setSelectedComp(updated)
      await loadData()
    } catch (err: unknown) {
      alert(`Failed to remove vehicle: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  async function handleApprove(id: string) {
    if (!confirm('Approve this presumptive computation? It will be marked ready for ITR-4 Sugam filing.')) return
    try {
      const res = await presumptiveApi.approve(id)
      if (selectedComp && selectedComp.id === id) setSelectedComp(res)
      await loadData()
      alert('Presumptive computation approved successfully!')
    } catch (err: unknown) {
      alert(`Approval failed: ${err instanceof Error ? err.message : 'Failed.'}`)
    }
  }

  return (
    <div style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: '0 0 6px 0', color: 'var(--text)' }}>
            Presumptive Taxation & Minimal Financials Desk
          </h1>
          <p style={{ margin: 0, color: 'var(--muted)', fontSize: '14px' }}>
            ITR-4 Sugam presumptive regime (Sections 44AD / 44ADA / 44AE), enhanced 5% cash threshold gating, Section 44AD(4) 5-year lockout tracker, and Section 44AA minimal balance sheet particulars.
          </p>
        </div>
        {can('presumptive.manage') && (
          <button
            onClick={() => setShowNewModal(true)}
            style={{
              background: '#0284c7',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              padding: '10px 18px',
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '8px'
            }}
          >
            <span>+</span> New Presumptive Computation
          </button>
        )}
      </div>

      {/* KPI Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>TOTAL COMPUTATIONS</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text)', marginTop: '4px' }}>{summary.totalComputations}</div>
            <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '4px' }}>
              44AD: {summary.sec44AdCount} • 44ADA: {summary.sec44AdaCount} • 44AE: {summary.sec44AeCount}
            </div>
          </div>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>PRESUMPTIVE TURNOVER</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--accent)', marginTop: '4px' }}>{fmtInr(summary.totalPresumptiveTurnover)}</div>
            <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '4px' }}>Declared: {fmtInr(summary.totalDeclaredProfit)}</div>
          </div>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>AUDIT MANDATORY (SEC 44AB)</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: summary.auditMandatoryCount > 0 ? 'var(--error)' : 'var(--success)', marginTop: '4px' }}>
              {summary.auditMandatoryCount}
            </div>
            <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '4px' }}>Below deemed profit or ceiling breach</div>
          </div>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>ACTIVE 44AD(4) LOCKOUTS</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: summary.lockoutActiveCount > 0 ? '#ea580c' : 'var(--text)', marginTop: '4px' }}>
              {summary.lockoutActiveCount}
            </div>
            <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '4px' }}>5-year penalty lockouts active</div>
          </div>
        </div>
      )}

      {/* Filters Bar */}
      <div style={{ display: 'flex', gap: '12px', background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '14px', marginBottom: '20px', alignItems: 'center', flexWrap: 'wrap' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)' }}>FY:</label>
          <select value={filterFy} onChange={e => setFilterFy(e.target.value)} style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}>
            <option value="2025-26">2025-26 (AY 2026-27)</option>
            <option value="2024-25">2024-25 (AY 2025-26)</option>
            <option value="2023-24">2023-24 (AY 2024-25)</option>
            <option value="all">All Financial Years</option>
          </select>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)' }}>Section:</label>
          <select value={filterSection} onChange={e => setFilterSection(e.target.value)} style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}>
            <option value="all">All Sections</option>
            <option value="44AD">Section 44AD (Eligible Business)</option>
            <option value="44ADA">Section 44ADA (Professionals)</option>
            <option value="44AE">Section 44AE (Goods Carriages)</option>
          </select>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
          <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)' }}>Status:</label>
          <select value={filterStatus} onChange={e => setFilterStatus(e.target.value)} style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}>
            <option value="all">All Statuses</option>
            <option value="draft">Draft</option>
            <option value="computed">Computed</option>
            <option value="approved">Approved</option>
          </select>
        </div>
        <button onClick={loadData} style={{ marginLeft: 'auto', padding: '6px 14px', background: 'var(--card-subtle)', color: 'var(--text)', border: '1px solid var(--border-subtle)', borderRadius: '6px', cursor: 'pointer', fontSize: '13px', fontWeight: 600 }}>
          Refresh
        </button>
      </div>

      {error && <div style={{ background: 'var(--error-bg)', border: '1px solid var(--error-border)', color: 'var(--error)', padding: '12px 16px', borderRadius: '6px', marginBottom: '20px' }}>{error}</div>}

      {/* Main Computations Table */}
      <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
          <thead style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', color: 'var(--text-secondary)' }}>
            <tr>
              <th style={{ padding: '12px 16px' }}>Client / Trade Name</th>
              <th style={{ padding: '12px 16px' }}>Section</th>
              <th style={{ padding: '12px 16px' }}>FY / AY</th>
              <th style={{ padding: '12px 16px' }}>Turnover / Receipts</th>
              <th style={{ padding: '12px 16px' }}>Digital %</th>
              <th style={{ padding: '12px 16px' }}>Min Deemed Profit</th>
              <th style={{ padding: '12px 16px' }}>Declared Profit</th>
              <th style={{ padding: '12px 16px' }}>Audit Status</th>
              <th style={{ padding: '12px 16px' }}>Status</th>
              <th style={{ padding: '12px 16px', textAlign: 'right' }}>Actions</th>
            </tr>
          </thead>
          <tbody>
            {loading ? (
              <tr>
                <td colSpan={10} style={{ padding: '24px', textAlign: 'center', color: 'var(--muted)' }}>Loading presumptive tax records...</td>
              </tr>
            ) : computations.length === 0 ? (
              <tr>
                <td colSpan={10} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                  No presumptive tax computations found. Click "+ New Presumptive Computation" above to create one.
                </td>
              </tr>
            ) : (
              computations.map(c => (
                <tr key={c.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  <td style={{ padding: '12px 16px' }}>
                    <div style={{ fontWeight: 600, color: 'var(--text)' }}>{c.clientName || 'Client'}</div>
                    <div style={{ fontSize: '11px', color: 'var(--muted)' }}>{c.tradeName} {c.clientPan ? `• PAN: ${c.clientPan}` : ''}</div>
                  </td>
                  <td style={{ padding: '12px 16px' }}>
                    <span style={{
                      padding: '3px 8px',
                      borderRadius: '4px',
                      fontWeight: 700,
                      fontSize: '11px',
                      background: c.section === '44AD' ? 'var(--accent-subtle)' : c.section === '44ADA' ? 'var(--purple-bg)' : 'var(--warning-bg)',
                      color: c.section === '44AD' ? 'var(--accent)' : c.section === '44ADA' ? '#7e22ce' : 'var(--warning)'
                    }}>
                      Section {c.section}
                    </span>
                  </td>
                  <td style={{ padding: '12px 16px' }}>
                    <div>FY {c.financialYear}</div>
                    <div style={{ fontSize: '11px', color: 'var(--muted)' }}>AY {c.assessmentYear}</div>
                  </td>
                  <td style={{ padding: '12px 16px', fontWeight: 600 }}>
                    {c.section === '44AE' ? `${c.vehicles?.length || 0} Vehicle(s)` : fmtInr(c.grossTurnover)}
                  </td>
                  <td style={{ padding: '12px 16px' }}>
                    {c.section === '44AE' ? (
                      <span style={{ color: 'var(--muted)' }}>N/A</span>
                    ) : (
                      <span style={{
                        padding: '2px 6px',
                        borderRadius: '4px',
                        fontSize: '11px',
                        fontWeight: 600,
                        background: c.isDigitalThresholdEligible ? 'var(--success-bg)' : 'var(--error-bg)',
                        color: c.isDigitalThresholdEligible ? 'var(--success)' : 'var(--error-text)'
                      }}>
                        {c.digitalPercentage}% {c.isDigitalThresholdEligible ? '(≤5% Cash)' : '(>5% Cash)'}
                      </span>
                    )}
                  </td>
                  <td style={{ padding: '12px 16px', color: 'var(--accent)', fontWeight: 600 }}>{fmtInr(c.minimumDeemedProfit)}</td>
                  <td style={{ padding: '12px 16px', color: 'var(--success)', fontWeight: 600 }}>{fmtInr(c.declaredProfit)}</td>
                  <td style={{ padding: '12px 16px' }}>
                    {c.isAuditMandatory ? (
                      <span style={{ padding: '2px 8px', borderRadius: '4px', background: 'var(--error-bg)', color: 'var(--error)', fontWeight: 600, fontSize: '11px' }} title={c.auditTriggerReason || 'Audit mandatory'}>
                        ⚠️ Audit Req (44AB)
                      </span>
                    ) : (
                      <span style={{ padding: '2px 8px', borderRadius: '4px', background: 'var(--success-bg)', color: 'var(--success)', fontWeight: 600, fontSize: '11px' }}>
                        ✓ Exempt (44AD/ADA/AE)
                      </span>
                    )}
                  </td>
                  <td style={{ padding: '12px 16px' }}>
                    <span style={{
                      padding: '2px 8px',
                      borderRadius: '12px',
                      fontSize: '11px',
                      fontWeight: 600,
                      textTransform: 'capitalize',
                      background: c.status === 'approved' ? 'var(--success-bg)' : c.status === 'computed' ? 'var(--accent-subtle)' : 'var(--card-subtle)',
                      color: c.status === 'approved' ? 'var(--success)' : c.status === 'computed' ? 'var(--accent)' : 'var(--text-secondary)'
                    }}>
                      {c.status}
                    </span>
                  </td>
                  <td style={{ padding: '12px 16px', textAlign: 'right' }}>
                    <div style={{ display: 'flex', gap: '6px', justifyContent: 'flex-end' }}>
                      <button
                        onClick={() => handleOpenDetail(c)}
                        style={{ padding: '5px 10px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '4px', fontSize: '12px', cursor: 'pointer', fontWeight: 600 }}
                      >
                        Open Desk
                      </button>
                      {can('presumptive.approve') && c.status !== 'approved' && (
                        <button
                          onClick={() => handleApprove(c.id)}
                          style={{ padding: '5px 10px', background: '#10b981', color: '#fff', border: 'none', borderRadius: '4px', fontSize: '12px', cursor: 'pointer', fontWeight: 600 }}
                        >
                          Approve
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* Detail / Workspace Modal */}
      {selectedComp && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 100, padding: '20px' }}>
          <div style={{ background: 'var(--card)', borderRadius: '12px', width: '100%', maxWidth: '1000px', maxHeight: '90vh', display: 'flex', flexDirection: 'column', overflow: 'hidden', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            {/* Modal Header */}
            <div style={{ padding: '16px 24px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: 'var(--card-subtle)' }}>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: 'var(--text)' }}>
                    {selectedComp.clientName} • Section {selectedComp.section}
                  </h2>
                  <span style={{ padding: '2px 8px', borderRadius: '4px', background: 'var(--accent-subtle)', color: 'var(--accent)', fontWeight: 600, fontSize: '11px' }}>
                    FY {selectedComp.financialYear} (AY {selectedComp.assessmentYear})
                  </span>
                  <span style={{ padding: '2px 8px', borderRadius: '12px', background: selectedComp.status === 'approved' ? 'var(--success-bg)' : 'var(--card-subtle)', color: selectedComp.status === 'approved' ? 'var(--success)' : 'var(--text-secondary)', fontWeight: 600, fontSize: '11px' }}>
                    {selectedComp.status.toUpperCase()}
                  </span>
                </div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '2px' }}>
                  Trade Name: <strong>{selectedComp.tradeName}</strong> • Entity: {selectedComp.entityType} • Code: {selectedComp.businessNatureCode}
                </div>
              </div>
              <button onClick={() => setSelectedComp(null)} style={{ background: 'transparent', border: 'none', fontSize: '20px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            {/* Warning Banner if Audit Mandatory */}
            {selectedComp.isAuditMandatory && (
              <div style={{ background: 'var(--error-bg)', borderBottom: '1px solid var(--error-border)', padding: '10px 24px', color: 'var(--error-text)', fontSize: '12px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span style={{ fontSize: '16px' }}>⚠️</span>
                <div>
                  <strong>Mandatory Tax Audit under Section 44AB Triggered:</strong> {selectedComp.auditTriggerReason}
                </div>
              </div>
            )}

            {/* Sub-tab Navigation */}
            <div style={{ display: 'flex', borderBottom: '1px solid var(--border)', background: 'var(--card)', padding: '0 24px' }}>
              <button
                onClick={() => setActiveTab('computation')}
                style={{ padding: '12px 18px', border: 'none', background: 'transparent', borderBottom: activeTab === 'computation' ? '2px solid var(--accent)' : '2px solid transparent', color: activeTab === 'computation' ? 'var(--accent)' : 'var(--muted)', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
              >
                1. Presumptive Income Computation
              </button>
              <button
                onClick={() => setActiveTab('minimal_financials')}
                style={{ padding: '12px 18px', border: 'none', background: 'transparent', borderBottom: activeTab === 'minimal_financials' ? '2px solid var(--accent)' : '2px solid transparent', color: activeTab === 'minimal_financials' ? 'var(--accent)' : 'var(--muted)', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
              >
                2. Section 44AA Minimal Financials (ITR-4)
              </button>
              <button
                onClick={() => setActiveTab('statement')}
                style={{ padding: '12px 18px', border: 'none', background: 'transparent', borderBottom: activeTab === 'statement' ? '2px solid var(--accent)' : '2px solid transparent', color: activeTab === 'statement' ? 'var(--accent)' : 'var(--muted)', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
              >
                3. Statement & Disclosures
              </button>
            </div>

            {/* Modal Body */}
            <div style={{ padding: '20px 24px', overflowY: 'auto', flex: 1 }}>
              {activeTab === 'computation' && (
                <div>
                  {selectedComp.section === '44AE' ? (
                    <div>
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                        <div>
                          <h3 style={{ fontSize: '15px', fontWeight: 600, margin: 0 }}>Goods Carriage Vehicles Register (Section 44AE)</h3>
                          <p style={{ fontSize: '12px', color: 'var(--muted)', margin: '2px 0 0 0' }}>
                            Max 10 vehicles owned at any time. Heavy (&gt;12 MT): ₹1,000/ton/month • Other: ₹7,500/month.
                          </p>
                        </div>
                        {can('presumptive.manage') && selectedComp.status !== 'approved' && (
                          <button
                            onClick={() => setShowAddVehicleModal(true)}
                            style={{ padding: '8px 14px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
                          >
                            + Add Vehicle
                          </button>
                        )}
                      </div>

                      <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px', marginBottom: '20px' }}>
                        <thead style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                          <tr>
                            <th style={{ padding: '8px 12px' }}>Registration No</th>
                            <th style={{ padding: '8px 12px' }}>Type</th>
                            <th style={{ padding: '8px 12px' }}>GVW (Tons)</th>
                            <th style={{ padding: '8px 12px' }}>Months</th>
                            <th style={{ padding: '8px 12px' }}>Rate / Month</th>
                            <th style={{ padding: '8px 12px' }}>Deemed Annual Profit</th>
                            <th style={{ padding: '8px 12px' }}>Declared Profit</th>
                            <th style={{ padding: '8px 12px', textAlign: 'right' }}>Action</th>
                          </tr>
                        </thead>
                        <tbody>
                          {selectedComp.vehicles && selectedComp.vehicles.length > 0 ? (
                            selectedComp.vehicles.map(v => (
                              <tr key={v.id} style={{ borderBottom: '1px solid var(--border)' }}>
                                <td style={{ padding: '8px 12px', fontWeight: 600 }}>{v.registrationNumber}</td>
                                <td style={{ padding: '8px 12px', textTransform: 'capitalize' }}>{v.vehicleType}</td>
                                <td style={{ padding: '8px 12px' }}>{v.grossVehicleWeightTons > 0 ? `${v.grossVehicleWeightTons} MT` : '-'}</td>
                                <td style={{ padding: '8px 12px' }}>{v.monthsOwned}</td>
                                <td style={{ padding: '8px 12px' }}>{fmtInr(v.ratePerMonth)}</td>
                                <td style={{ padding: '8px 12px', color: 'var(--accent)', fontWeight: 600 }}>{fmtInr(v.deemedAnnualProfit)}</td>
                                <td style={{ padding: '8px 12px', color: 'var(--success)', fontWeight: 600 }}>{fmtInr(v.declaredProfit)}</td>
                                <td style={{ padding: '8px 12px', textAlign: 'right' }}>
                                  {can('presumptive.manage') && selectedComp.status !== 'approved' && (
                                    <button
                                      onClick={() => handleRemoveVehicle(v.id)}
                                      style={{ padding: '4px 8px', background: 'var(--error-bg)', color: 'var(--error)', border: 'none', borderRadius: '4px', cursor: 'pointer', fontSize: '11px' }}
                                    >
                                      Remove
                                    </button>
                                  )}
                                </td>
                              </tr>
                            ))
                          ) : (
                            <tr>
                              <td colSpan={8} style={{ padding: '16px', textAlign: 'center', color: 'var(--muted)' }}>No vehicles registered yet. Click "+ Add Vehicle" above.</td>
                            </tr>
                          )}
                        </tbody>
                      </table>
                    </div>
                  ) : null}

                  {/* Editable figures for 44AD / 44ADA */}
                  {selectedComp.section !== '44AE' && (
                    <form onSubmit={handleUpdateSelected}>
                      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '16px' }}>
                        <div>
                          <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>
                            Digital Turnover / Receipts (Account Payee / ECS / UPI)
                          </label>
                          <input
                            type="number"
                            value={selectedComp.digitalTurnover}
                            onChange={e => setSelectedComp({ ...selectedComp, digitalTurnover: Number(e.target.value) })}
                            style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                            disabled={selectedComp.status === 'approved'}
                          />
                          <span style={{ fontSize: '11px', color: 'var(--muted)' }}>Deemed rate: {selectedComp.section === '44AD' ? '6%' : '50%'}</span>
                        </div>
                        <div>
                          <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>
                            Cash / Non-Digital Turnover
                          </label>
                          <input
                            type="number"
                            value={selectedComp.cashTurnover}
                            onChange={e => setSelectedComp({ ...selectedComp, cashTurnover: Number(e.target.value) })}
                            style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                            disabled={selectedComp.status === 'approved'}
                          />
                          <span style={{ fontSize: '11px', color: 'var(--muted)' }}>Deemed rate: {selectedComp.section === '44AD' ? '8%' : '50%'}</span>
                        </div>
                      </div>

                      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', background: 'var(--card-subtle)', padding: '16px', borderRadius: '8px', marginBottom: '16px' }}>
                        <div>
                          <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>GROSS TURNOVER</div>
                          <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>{fmtInr(selectedComp.grossTurnover)}</div>
                          <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Statutory Limit: {fmtInr(selectedComp.turnoverLimit)}</div>
                        </div>
                        <div>
                          <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>MINIMUM DEEMED PROFIT</div>
                          <div style={{ fontSize: '18px', fontWeight: 700, color: 'var(--accent)' }}>{fmtInr(selectedComp.minimumDeemedProfit)}</div>
                          <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Statutory Threshold</div>
                        </div>
                        <div>
                          <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '2px' }}>
                            DECLARED NET PROFIT (ITR-4)
                          </label>
                          <input
                            type="number"
                            value={selectedComp.declaredProfit}
                            onChange={e => setSelectedComp({ ...selectedComp, declaredProfit: Number(e.target.value) })}
                            style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontWeight: 700, fontSize: '14px', color: 'var(--success)' }}
                            disabled={selectedComp.status === 'approved'}
                          />
                        </div>
                      </div>

                      {/* Section 44AD(4) Lockout Options */}
                      {selectedComp.section === '44AD' && (
                        <div style={{ border: '1px solid var(--border)', borderRadius: '8px', padding: '14px', marginBottom: '16px' }}>
                          <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', cursor: 'pointer' }}>
                            <input
                              type="checkbox"
                              checked={selectedComp.optedOutPriorYear}
                              onChange={e => setSelectedComp({ ...selectedComp, optedOutPriorYear: e.target.checked })}
                              disabled={selectedComp.status === 'approved'}
                            />
                            Assessee opted out of Section 44AD in an earlier Assessment Year (declared profit &lt; 6%/8%)
                          </label>
                          {selectedComp.optedOutPriorYear && (
                            <div style={{ marginTop: '8px', display: 'flex', alignItems: 'center', gap: '12px' }}>
                              <span style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>Opt-out Assessment Year:</span>
                              <input
                                type="text"
                                value={selectedComp.optOutAssessmentYear || ''}
                                onChange={e => setSelectedComp({ ...selectedComp, optOutAssessmentYear: e.target.value })}
                                style={{ padding: '4px 8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', width: '120px' }}
                                placeholder="2022-23"
                                disabled={selectedComp.status === 'approved'}
                              />
                              {selectedComp.isLockoutActive && (
                                <span style={{ color: 'var(--error)', fontSize: '12px', fontWeight: 600 }}>
                                  🔒 5-Year Lockout Active until AY {selectedComp.lockoutExpiryAy}! Tax audit mandatory u/s 44AB(e).
                                </span>
                              )}
                            </div>
                          )}
                        </div>
                      )}

                      {can('presumptive.manage') && selectedComp.status !== 'approved' && (
                        <button type="submit" style={{ padding: '8px 18px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}>
                          Save & Recalculate
                        </button>
                      )}
                    </form>
                  )}
                </div>
              )}

              {/* TAB 2: Section 44AA Minimal Financials (ITR-4 Particulars) */}
              {activeTab === 'minimal_financials' && (
                <form onSubmit={handleUpdateSelected}>
                  <div style={{ marginBottom: '16px' }}>
                    <h3 style={{ fontSize: '15px', fontWeight: 600, margin: '0 0 4px 0' }}>
                      Mandatory Particulars of the Business (Section 44AA / ITR-4 Sugam)
                    </h3>
                    <p style={{ fontSize: '12px', color: 'var(--muted)', margin: 0 }}>
                      Assessees opting for presumptive taxation are exempt from maintaining regular books of accounts u/s 44AA, but must declare these 4 key balance sheet particulars.
                    </p>
                  </div>

                  {/* 4 Core ITR-4 Particulars */}
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px', background: '#f0f9ff', padding: '16px', borderRadius: '8px', border: '1px solid #bae6fd', marginBottom: '20px' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: 'var(--accent)', marginBottom: '4px' }}>
                        1. Amount of Sundry Debtors (₹) *
                      </label>
                      <input
                        type="number"
                        value={selectedComp.sundryDebtors}
                        onChange={e => setSelectedComp({ ...selectedComp, sundryDebtors: Number(e.target.value) })}
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #7dd3fc', fontWeight: 600 }}
                        disabled={selectedComp.status === 'approved'}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: 'var(--accent)', marginBottom: '4px' }}>
                        2. Amount of Sundry Creditors (₹) *
                      </label>
                      <input
                        type="number"
                        value={selectedComp.sundryCreditors}
                        onChange={e => setSelectedComp({ ...selectedComp, sundryCreditors: Number(e.target.value) })}
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #7dd3fc', fontWeight: 600 }}
                        disabled={selectedComp.status === 'approved'}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: 'var(--accent)', marginBottom: '4px' }}>
                        3. Amount of Stock-in-Trade (₹) *
                      </label>
                      <input
                        type="number"
                        value={selectedComp.stockInTrade}
                        onChange={e => setSelectedComp({ ...selectedComp, stockInTrade: Number(e.target.value) })}
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #7dd3fc', fontWeight: 600 }}
                        disabled={selectedComp.status === 'approved'}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '12px', fontWeight: 700, color: 'var(--accent)', marginBottom: '4px' }}>
                        4. Amount of Cash Balance (₹) *
                      </label>
                      <input
                        type="number"
                        value={selectedComp.cashBalance}
                        onChange={e => setSelectedComp({ ...selectedComp, cashBalance: Number(e.target.value) })}
                        style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #7dd3fc', fontWeight: 600 }}
                        disabled={selectedComp.status === 'approved'}
                      />
                    </div>
                  </div>

                  {/* Supplementary Balance Sheet Items */}
                  <h4 style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '10px' }}>
                    Supplementary Balance Sheet Assets & Liabilities (for Statement Coherence)
                  </h4>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '14px', marginBottom: '16px' }}>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--muted)', marginBottom: '4px' }}>Bank Balance</label>
                      <input
                        type="number"
                        value={selectedComp.bankBalance}
                        onChange={e => setSelectedComp({ ...selectedComp, bankBalance: Number(e.target.value) })}
                        style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                        disabled={selectedComp.status === 'approved'}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--muted)', marginBottom: '4px' }}>Fixed Assets</label>
                      <input
                        type="number"
                        value={selectedComp.fixedAssets}
                        onChange={e => setSelectedComp({ ...selectedComp, fixedAssets: Number(e.target.value) })}
                        style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                        disabled={selectedComp.status === 'approved'}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--muted)', marginBottom: '4px' }}>Other Assets</label>
                      <input
                        type="number"
                        value={selectedComp.otherAssets}
                        onChange={e => setSelectedComp({ ...selectedComp, otherAssets: Number(e.target.value) })}
                        style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                        disabled={selectedComp.status === 'approved'}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--muted)', marginBottom: '4px' }}>Partner / Proprietor Capital</label>
                      <input
                        type="number"
                        value={selectedComp.partnerOrProprietorCapital}
                        onChange={e => setSelectedComp({ ...selectedComp, partnerOrProprietorCapital: Number(e.target.value) })}
                        style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                        disabled={selectedComp.status === 'approved'}
                      />
                    </div>
                    <div>
                      <label style={{ display: 'block', fontSize: '11px', fontWeight: 600, color: 'var(--muted)', marginBottom: '4px' }}>Loans & Borrowings</label>
                      <input
                        type="number"
                        value={selectedComp.loansAndBorrowings}
                        onChange={e => setSelectedComp({ ...selectedComp, loansAndBorrowings: Number(e.target.value) })}
                        style={{ width: '100%', padding: '6px 10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                        disabled={selectedComp.status === 'approved'}
                      />
                    </div>
                  </div>

                  {/* Real-time Balance Sheet Sanity Meter */}
                  {statement && (
                    <div style={{
                      padding: '12px 16px',
                      borderRadius: '8px',
                      marginBottom: '16px',
                      background: statement.isBalanced ? 'var(--success-bg)' : 'var(--warning-bg)',
                      border: `1px solid ${statement.isBalanced ? '#86efac' : 'var(--warning-border)'}`,
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'center'
                    }}>
                      <div>
                        <div style={{ fontWeight: 600, fontSize: '13px', color: statement.isBalanced ? 'var(--success)' : 'var(--warning)' }}>
                          {statement.isBalanced ? '✓ Minimal Balance Sheet is Balanced' : `⚠️ Balance Sheet Variance: ${fmtInr(statement.difference)}`}
                        </div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)' }}>
                          Total Assets: {fmtInr(statement.totalAssets)} • Total Liabilities & Capital: {fmtInr(statement.totalLiabilitiesAndCapital)}
                        </div>
                      </div>
                      <span style={{
                        padding: '4px 10px',
                        borderRadius: '12px',
                        fontWeight: 700,
                        fontSize: '11px',
                        background: statement.isBalanced ? 'var(--success-bg)' : 'var(--warning-bg)',
                        color: statement.isBalanced ? 'var(--success)' : 'var(--warning)'
                      }}>
                        {statement.isBalanced ? 'BALANCED' : 'IMBALANCED'}
                      </span>
                    </div>
                  )}

                  {can('presumptive.manage') && selectedComp.status !== 'approved' && (
                    <button type="submit" style={{ padding: '8px 18px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}>
                      Update Minimal Financials
                    </button>
                  )}
                </form>
              )}

              {/* TAB 3: Statement & Disclosures (Printable Form) */}
              {activeTab === 'statement' && (
                <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '24px' }}>
                  <div style={{ borderBottom: '2px solid #0f172a', paddingBottom: '12px', marginBottom: '20px', display: 'flex', justifyContent: 'space-between' }}>
                    <div>
                      <h2 style={{ fontSize: '16px', fontWeight: 700, margin: 0, color: 'var(--text)' }}>
                        STATEMENT OF PRESUMPTIVE INCOME & MINIMAL FINANCIAL PARTICULARS
                      </h2>
                      <p style={{ fontSize: '12px', color: 'var(--muted)', margin: '4px 0 0 0' }}>
                        Prepared in compliance with Sections 44AD / 44ADA / 44AE &amp; Section 44AA of the Income Tax Act, 1961
                      </p>
                    </div>
                    <button
                      onClick={() => window.print()}
                      style={{ padding: '6px 14px', background: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', borderRadius: '6px', cursor: 'pointer', fontSize: '12px', fontWeight: 600 }}
                    >
                      🖨️ Print Statement
                    </button>
                  </div>

                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px', fontSize: '12px', marginBottom: '20px' }}>
                    <div><strong>Assessee Name:</strong> {selectedComp.clientName}</div>
                    <div><strong>PAN:</strong> {selectedComp.clientPan || 'N/A'}</div>
                    <div><strong>Trade / Business Name:</strong> {selectedComp.tradeName}</div>
                    <div><strong>Assessment Year:</strong> {selectedComp.assessmentYear} (FY {selectedComp.financialYear})</div>
                    <div><strong>Presumptive Section:</strong> Section {selectedComp.section}</div>
                    <div><strong>Entity Classification:</strong> {selectedComp.entityType}</div>
                  </div>

                  {/* Particulars Table */}
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px', marginBottom: '20px', border: '1px solid var(--border)' }}>
                    <thead style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border-subtle)' }}>
                      <tr>
                        <th style={{ padding: '8px 12px', textAlign: 'left' }}>Particulars (ITR-4 Schedule BP)</th>
                        <th style={{ padding: '8px 12px', textAlign: 'right' }}>Amount (₹)</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 12px' }}>Gross Turnover / Gross Professional Receipts</td>
                        <td style={{ padding: '8px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtInr(selectedComp.grossTurnover)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 12px' }}>Turnover received through Digital / Banking Modes ({selectedComp.digitalPercentage}%)</td>
                        <td style={{ padding: '8px 12px', textAlign: 'right' }}>{fmtInr(selectedComp.digitalTurnover)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 12px' }}>Turnover received in Cash</td>
                        <td style={{ padding: '8px 12px', textAlign: 'right' }}>{fmtInr(selectedComp.cashTurnover)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--card-subtle)' }}>
                        <td style={{ padding: '8px 12px', fontWeight: 600 }}>Statutory Minimum Deemed Profit</td>
                        <td style={{ padding: '8px 12px', textAlign: 'right', fontWeight: 600, color: 'var(--accent)' }}>{fmtInr(selectedComp.minimumDeemedProfit)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border-subtle)', background: 'var(--success-bg)' }}>
                        <td style={{ padding: '8px 12px', fontWeight: 700, color: 'var(--success)' }}>Declared Presumptive Net Profit (Taxable)</td>
                        <td style={{ padding: '8px 12px', textAlign: 'right', fontWeight: 700, color: 'var(--success)' }}>{fmtInr(selectedComp.declaredProfit)}</td>
                      </tr>
                    </tbody>
                  </table>

                  {/* Minimal Balance Sheet Particulars */}
                  <h4 style={{ fontSize: '13px', fontWeight: 700, marginBottom: '8px', color: 'var(--text)' }}>
                    Financial Particulars of the Business under Section 44AA (Part B of ITR-4)
                  </h4>
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '12px', fontSize: '12px', marginBottom: '24px' }}>
                    <div style={{ padding: '10px', background: 'var(--card-subtle)', borderRadius: '6px' }}>
                      <span style={{ color: 'var(--muted)' }}>Sundry Debtors:</span> <strong>{fmtInr(selectedComp.sundryDebtors)}</strong>
                    </div>
                    <div style={{ padding: '10px', background: 'var(--card-subtle)', borderRadius: '6px' }}>
                      <span style={{ color: 'var(--muted)' }}>Sundry Creditors:</span> <strong>{fmtInr(selectedComp.sundryCreditors)}</strong>
                    </div>
                    <div style={{ padding: '10px', background: 'var(--card-subtle)', borderRadius: '6px' }}>
                      <span style={{ color: 'var(--muted)' }}>Stock-in-trade:</span> <strong>{fmtInr(selectedComp.stockInTrade)}</strong>
                    </div>
                    <div style={{ padding: '10px', background: 'var(--card-subtle)', borderRadius: '6px' }}>
                      <span style={{ color: 'var(--muted)' }}>Cash in Hand:</span> <strong>{fmtInr(selectedComp.cashBalance)}</strong>
                    </div>
                  </div>

                  <div style={{ borderTop: '1px solid var(--border)', paddingTop: '16px', display: 'flex', justifyContent: 'space-between', fontSize: '11px', color: 'var(--muted)' }}>
                    <div>Prepared by: {selectedComp.createdByName || 'Staff'} • Status: {selectedComp.status.toUpperCase()}</div>
                    <div>Authorized Signatory / Assessee Signature: _________________________</div>
                  </div>

                  <JsonExportPanel
                    title="Official ITR-4 (Sugam) JSON Export (AY 2026-27 V1.1)"
                    subtitle="Exports Presumptive 44AD/ADA/AE + Sec 44AA Minimal Balance Sheet validated against official CBDT schema."
                    clientId={selectedComp.clientId}
                    fy={selectedComp.financialYear}
                    vaultTypeCode="ITR_BUS"
                    generating={exportingItr4}
                    result={itr4Export}
                    onGenerate={async () => {
                      try {
                        setExportingItr4(true)
                        setError(null)
                        const res = await presumptiveApi.exportItr4(selectedComp.id, itr4Opts)
                        setItr4Export(res)
                      } catch (err: unknown) {
                        setError(err instanceof Error ? err.message : 'ITR-4 JSON export failed.')
                      } finally {
                        setExportingItr4(false)
                      }
                    }}
                  >
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '8px', alignItems: 'center' }}>
                      <input
                        type="text"
                        placeholder="Father's Name"
                        value={itr4Opts.fatherName || ''}
                        onChange={e => setItr4Opts(p => ({ ...p, fatherName: e.target.value }))}
                        style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '12px' }}
                      />
                      <input
                        type="text"
                        placeholder="City (e.g. Kochi)"
                        value={itr4Opts.city || ''}
                        onChange={e => setItr4Opts(p => ({ ...p, city: e.target.value }))}
                        style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '12px' }}
                      />
                      <input
                        type="text"
                        maxLength={6}
                        placeholder="PIN Code"
                        value={itr4Opts.pinCode || ''}
                        onChange={e => setItr4Opts(p => ({ ...p, pinCode: e.target.value }))}
                        style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border-subtle)', fontSize: '12px' }}
                      />
                      <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text-secondary)', cursor: 'pointer' }}>
                        <input
                          type="checkbox"
                          checked={!!itr4Opts.optOutNewRegime}
                          onChange={e => setItr4Opts(p => ({ ...p, optOutNewRegime: e.target.checked }))}
                        />
                        <span>Opt Out New Regime (10-IEA)</span>
                      </label>
                    </div>
                  </JsonExportPanel>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* New Computation Modal */}
      {showNewModal && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 110, padding: '20px' }}>
          <div style={{ background: 'var(--card)', borderRadius: '12px', width: '100%', maxWidth: '600px', maxHeight: '90vh', overflowY: 'auto', padding: '24px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0 }}>Create Presumptive Tax Computation</h2>
              <button onClick={() => setShowNewModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '20px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            <form onSubmit={handleCreateComputation}>
              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Select Client *</label>
                <select
                  value={form.clientId}
                  onChange={e => {
                    const c = clients.find(cl => cl.id === e.target.value)
                    setForm({ ...form, clientId: e.target.value, tradeName: c ? c.name : '' })
                  }}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  required
                >
                  <option value="">-- Choose a Client --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Financial Year *</label>
                  <select value={form.financialYear} onChange={e => setForm({ ...form, financialYear: e.target.value })} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}>
                    <option value="2024-25">2024-25 (AY 2025-26)</option>
                    <option value="2023-24">2023-24 (AY 2024-25)</option>
                    <option value="2025-26">2025-26 (AY 2026-27)</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Section *</label>
                  <select value={form.section} onChange={e => setForm({ ...form, section: e.target.value })} style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}>
                    <option value="44AD">Section 44AD (Eligible Business)</option>
                    <option value="44ADA">Section 44ADA (Specified Professionals)</option>
                    <option value="44AE">Section 44AE (Goods Carriages Transporter)</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Trade / Business Name</label>
                  <input
                    type="text"
                    value={form.tradeName}
                    onChange={e => setForm({ ...form, tradeName: e.target.value })}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                    placeholder="e.g. Acme Stores"
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Business Nature Code</label>
                  <input
                    type="text"
                    value={form.businessNatureCode}
                    onChange={e => setForm({ ...form, businessNatureCode: e.target.value })}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                    placeholder="e.g. 01001, 07001"
                  />
                </div>
              </div>

              {form.section !== '44AE' && (
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Digital Turnover (₹)</label>
                    <input
                      type="number"
                      value={form.digitalTurnover}
                      onChange={e => setForm({ ...form, digitalTurnover: Number(e.target.value) })}
                      style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                    />
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Cash Turnover (₹)</label>
                    <input
                      type="number"
                      value={form.cashTurnover}
                      onChange={e => setForm({ ...form, cashTurnover: Number(e.target.value) })}
                      style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                    />
                  </div>
                </div>
              )}

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Declared Net Profit (₹)</label>
                <input
                  type="number"
                  value={form.declaredProfit}
                  onChange={e => setForm({ ...form, declaredProfit: Number(e.target.value) })}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  placeholder="Declared profit in ITR-4"
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                <button type="button" onClick={() => setShowNewModal(false)} style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', borderRadius: '6px', cursor: 'pointer' }}>Cancel</button>
                <button type="submit" style={{ padding: '8px 18px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}>Create Computation</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Add Vehicle Modal (Section 44AE) */}
      {showAddVehicleModal && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(15, 23, 42, 0.6)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 120, padding: '20px' }}>
          <div style={{ background: 'var(--card)', borderRadius: '12px', width: '100%', maxWidth: '480px', padding: '24px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h2 style={{ fontSize: '16px', fontWeight: 700, margin: 0 }}>Add Goods Carriage Vehicle (Sec 44AE)</h2>
              <button onClick={() => setShowAddVehicleModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '20px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            <form onSubmit={handleAddVehicle}>
              <div style={{ marginBottom: '12px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Registration Number *</label>
                <input
                  type="text"
                  value={vehicleForm.registrationNumber}
                  onChange={e => setVehicleForm({ ...vehicleForm, registrationNumber: e.target.value })}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  placeholder="e.g. DL-01-AB-1234"
                  required
                />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px', marginBottom: '12px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Vehicle Category</label>
                  <select
                    value={vehicleForm.vehicleType}
                    onChange={e => setVehicleForm({ ...vehicleForm, vehicleType: e.target.value })}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="other">Other Goods Vehicle (≤12 MT)</option>
                    <option value="heavy">Heavy Goods Vehicle (&gt;12 MT)</option>
                  </select>
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Months Owned (1-12)</label>
                  <input
                    type="number"
                    min="1"
                    max="12"
                    value={vehicleForm.monthsOwned}
                    onChange={e => setVehicleForm({ ...vehicleForm, monthsOwned: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              {vehicleForm.vehicleType === 'heavy' && (
                <div style={{ marginBottom: '16px' }}>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Gross Vehicle Weight in Metric Tonnes (MT)</label>
                  <input
                    type="number"
                    step="0.1"
                    value={vehicleForm.grossVehicleWeightTons}
                    onChange={e => setVehicleForm({ ...vehicleForm, grossVehicleWeightTons: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                    placeholder="e.g. 16.5 MT"
                  />
                  <span style={{ fontSize: '11px', color: 'var(--muted)' }}>Rate: ₹1,000 per MT per month</span>
                </div>
              )}

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                <button type="button" onClick={() => setShowAddVehicleModal(false)} style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: '1px solid var(--border-subtle)', borderRadius: '6px', cursor: 'pointer' }}>Cancel</button>
                <button type="submit" style={{ padding: '8px 18px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}>Add Vehicle</button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
