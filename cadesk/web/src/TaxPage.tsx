import { useCallback, useEffect, useState } from 'react'
import {
  type CalculateTaxRequest,
  type RegimeComparisonDto,
  type TaxComputationDto,
  type IncomeTaxJsonExportResult,
  type ExportItrJsonRequest,
  type Client,
  type ClientPage,
  taxApi,
  api
} from './api'
import { JsonExportPanel } from './components/JsonExportPanel'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n)

export function TaxPage() {
  const [activeTab, setActiveTab] = useState<'calculator' | 'history'>('calculator')
  const [clients, setClients] = useState<Client[]>([])
  const [computations, setComputations] = useState<TaxComputationDto[]>([])
  const [loading, setLoading] = useState(false)
  const [calculating, setCalculating] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successMsg, setSuccessMsg] = useState<string | null>(null)
  const [selectedComputation, setSelectedComputation] = useState<TaxComputationDto | null>(null)
  const [itr1Export, setItr1Export] = useState<IncomeTaxJsonExportResult | null>(null)
  const [exportingItr1, setExportingItr1] = useState(false)
  const [itr1Opts, setItr1Opts] = useState<ExportItrJsonRequest>({
    fatherName: '',
    pinCode: '682001',
    city: 'Kochi',
    ifscCode: '',
    bankAccountNo: '',
  })

  // Filter state
  const [filterAy, setFilterAy] = useState('2026-27')

  // AIS Import State
  const [showAisModal, setShowAisModal] = useState(false)
  const [aisClientId, setAisClientId] = useState('')
  const [aisJsonPayload, setAisJsonPayload] = useState('')
  const [importingAis, setImportingAis] = useState(false)

  // Calculator Form State
  const [form, setForm] = useState<CalculateTaxRequest>({
    clientId: '',
    assessmentYear: '2026-27',
    financialYear: '2025-26',
    taxpayerCategory: 'individual',
    optedRegime: null,
    grossSalary: 1200000,
    hraReceived: 120000,
    rentPaid: 150000,
    isMetro: true,
    professionalTax: 2500,
    annualRentReceived: 0,
    municipalTaxesPaid: 0,
    housingLoanInterest: 0,
    isSelfOccupied: false,
    businessGrossTurnover: 0,
    netProfit: 0,
    presumptiveSection: null,
    stcg111A: 50000,
    stcgNormal: 0,
    ltcg112A: 200000,
    ltcg112: 0,
    savingsInterest: 15000,
    fdInterest: 25000,
    dividendIncome: 10000,
    otherIncome: 0,
    section80C: 150000,
    section80Ccd1B: 50000,
    section80D: 25000,
    section80E: 0,
    section80G: 0,
    section80Tta: 10000,
    tdsPaid: 60000,
    tcsPaid: 0,
    advanceTaxPaid: 20000,
    selfAssessmentTaxPaid: 0,
    notes: 'Salaried IT professional with equity investments'
  })

  // Regime preview state
  const [preview, setPreview] = useState<RegimeComparisonDto | null>(null)

  const loadClients = useCallback(async () => {
    try {
      const res = await api<ClientPage | Client[]>('/api/clients?take=200')
      const clientList = Array.isArray(res) ? res : (res?.items || [])
      setClients(clientList)
      if (clientList.length > 0) {
        setForm(prev => (prev.clientId ? prev : { ...prev, clientId: clientList[0].id }))
      }
    } catch {
      // non fatal
    }
  }, [])

  const loadComputations = useCallback(async () => {
    try {
      setLoading(true)
      const list = await taxApi.list(undefined, filterAy === 'all' ? undefined : filterAy)
      setComputations(list || [])
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load computations')
    } finally {
      setLoading(false)
    }
  }, [filterAy])

  useEffect(() => {
    loadClients()
    loadComputations()
  }, [loadClients, loadComputations])

  async function handleImportAis(e: React.FormEvent) {
    e.preventDefault()
    const targetClient = aisClientId || form.clientId
    if (!targetClient) {
      setError('Please select a client for AIS import.')
      return
    }
    if (!aisJsonPayload.trim()) {
      setError('Please provide AIS JSON content.')
      return
    }
    try {
      setImportingAis(true)
      setError(null)
      const res = await taxApi.importAis({ clientId: targetClient, aisJson: aisJsonPayload })
      setSuccessMsg(`AIS successfully imported! AY ${res.assessmentYear} computed (Gross Salary: ${fmtInr(res.grossSalary)}, Other Sources: ${fmtInr(res.otherSourcesIncome)}).`)
      setShowAisModal(false)
      setAisJsonPayload('')
      await loadComputations()
      setSelectedComputation(res)
      setActiveTab('history')
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'AIS import failed.')
    } finally {
      setImportingAis(false)
    }
  }

  function loadSampleAisJson() {
    setAisJsonPayload(`{
  "financialYear": "2024-25",
  "assessmentYear": "2025-26",
  "pan": "AAACB1234F",
  "salary": [
    { "employerName": "Bharat Tech Solutions Ltd", "grossSalary": 2400000, "tds": 280000 }
  ],
  "interest": [
    { "bankName": "HDFC Bank", "accountType": "SAVINGS", "amount": 25000, "tds": 0 },
    { "bankName": "State Bank of India", "accountType": "TIME_DEPOSIT", "amount": 50000, "tds": 5000 }
  ],
  "dividend": [
    { "companyName": "Reliance Industries Ltd", "amount": 20000, "tds": 2000 }
  ],
  "capitalGains": [
    { "securityName": "Nifty Index Fund", "saleConsideration": 450000, "costOfAcquisition": 300000, "isLongTerm": true, "gain": 150000 }
  ],
  "taxPayments": [
    { "minorHead": "Advance Tax (100)", "bsrCode": "0210045", "challanNumber": "88192", "tenderDate": "2024-06-14", "amount": 50000 }
  ]
}`)
  }

  async function handleCalculatePreview() {
    if (!form.clientId) {
      setError('Please select a client to run tax computation.')
      return
    }
    setError(null)
    setCalculating(true)
    try {
      const res = await taxApi.preview(form)
      setPreview(res)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Calculation preview failed')
    } finally {
      setCalculating(false)
    }
  }

  async function handleSaveComputation() {
    if (!form.clientId) {
      setError('Please select a client to save computation.')
      return
    }
    setError(null)
    setSaving(true)
    try {
      const saved = await taxApi.save(form)
      setSuccessMsg(`Tax computation saved successfully (${saved.assessmentYear}).`)
      await loadComputations()
      setActiveTab('history')
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to save computation.')
    } finally {
      setSaving(false)
    }
  }

  async function handleStatusChange(id: string, newStatus: string) {
    try {
      await taxApi.updateStatus(id, newStatus)
      await loadComputations()
      if (selectedComputation && selectedComputation.id === id) {
        const refreshed = await taxApi.get(id)
        setSelectedComputation(refreshed)
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to update status.')
    }
  }

  async function handleDeleteComputation(id: string) {
    if (!confirm('Are you sure you want to delete this tax computation record?')) return
    try {
      await taxApi.delete(id)
      await loadComputations()
      if (selectedComputation?.id === id) setSelectedComputation(null)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to delete computation.')
    }
  }

  return (
    <div style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto', color: 'var(--text)' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 'bold', margin: '0 0 4px 0', color: 'var(--text)' }}>
            Direct Tax Computation Engine
          </h1>
          <p style={{ margin: 0, color: 'var(--muted)', fontSize: '14px' }}>
            AY 2025-26 &amp; FY 2024-25 Income Tax Calculator, 5 Heads Analysis &amp; Old vs New (Sec 115BAC) Comparison
          </p>
        </div>
        <div style={{ display: 'flex', gap: '12px' }}>
          <button
            onClick={() => {
              setAisClientId(form.clientId || (clients.length > 0 ? clients[0].id : ''))
              setShowAisModal(true)
            }}
            style={{
              padding: '8px 16px',
              borderRadius: '6px',
              background: 'var(--card)',
              color: 'var(--accent)',
              fontWeight: 600,
              border: '1px solid var(--accent)',
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '6px'
            }}
          >
            + Import AIS / 26AS JSON
          </button>
          <button
            onClick={() => { setActiveTab('calculator'); setPreview(null); }}
            style={{
              padding: '8px 16px',
              borderRadius: '6px',
              background: activeTab === 'calculator' ? 'var(--accent)' : 'var(--card-subtle)',
              color: activeTab === 'calculator' ? '#fff' : 'var(--text-secondary)',
              fontWeight: 500,
              border: activeTab === 'calculator' ? '1px solid var(--accent)' : '1px solid var(--border)',
              cursor: 'pointer'
            }}
          >
            Tax Calculator &amp; Simulator
          </button>
          <button
            onClick={() => setActiveTab('history')}
            style={{
              padding: '8px 16px',
              borderRadius: '6px',
              background: activeTab === 'history' ? 'var(--accent)' : 'var(--card-subtle)',
              color: activeTab === 'history' ? '#fff' : 'var(--text-secondary)',
              fontWeight: 500,
              border: activeTab === 'history' ? '1px solid var(--accent)' : '1px solid var(--border)',
              cursor: 'pointer'
            }}
          >
            Computation Records ({computations.length})
          </button>
        </div>
      </div>

      {error && (
        <div style={{ padding: '12px 16px', background: 'rgba(220, 38, 38, 0.08)', border: '1px solid rgba(220, 38, 38, 0.35)', borderRadius: '6px', color: 'var(--error)', marginBottom: '16px' }}>
          {error}
        </div>
      )}

      {successMsg && (
        <div style={{ padding: '12px 16px', background: 'rgba(22, 163, 74, 0.08)', border: '1px solid rgba(22, 163, 74, 0.35)', borderRadius: '6px', color: 'var(--success)', marginBottom: '16px' }}>
          {successMsg}
        </div>
      )}

      {/* Tab 1: Calculator & Simulator */}
      {activeTab === 'calculator' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(500px, 1.2fr) minmax(450px, 1fr)', gap: '24px' }}>
          {/* Inputs Section */}
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '20px' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 600, marginTop: 0, marginBottom: '16px', borderBottom: '1px solid var(--border)', paddingBottom: '8px', color: 'var(--text)' }}>
              Taxpayer &amp; Assessment Setup
            </h2>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '20px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '4px', color: 'var(--text-secondary)' }}>Select Client</label>
                <select
                  value={form.clientId}
                  onChange={e => setForm({ ...form, clientId: e.target.value })}
                  style={{ width: '100%', padding: '8px 10px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                >
                  <option value="">-- Choose Client --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} {c.taxIds?.['PAN'] ? `(${c.taxIds['PAN']})` : ''}</option>
                  ))}
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '4px', color: 'var(--text-secondary)' }}>Assessment Year</label>
                <select
                  value={form.assessmentYear}
                  onChange={e => {
                    const ay = e.target.value
                    const fy = ay === '2026-27' ? '2025-26' : ay === '2025-26' ? '2024-25' : '2023-24'
                    setForm({ ...form, assessmentYear: ay, financialYear: fy })
                  }}
                  style={{ width: '100%', padding: '8px 10px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                >
                  <option value="2026-27">AY 2026-27 (FY 2025-26)</option>
                  <option value="2025-26">AY 2025-26 (FY 2024-25)</option>
                  <option value="2024-25">AY 2024-25 (FY 2023-24)</option>
                </select>
              </div>
            </div>

            {/* Head 1: Salary */}
            <div style={{ marginBottom: '20px', background: 'var(--card-subtle)', padding: '14px', borderRadius: '6px', border: '1px solid var(--border)' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: 'var(--accent)' }}>
                1. Income from Salary (Sec 15–17)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>Gross Salary (₹)</label>
                  <input
                    type="number"
                    value={form.grossSalary}
                    onChange={e => setForm({ ...form, grossSalary: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>HRA Received (₹)</label>
                  <input
                    type="number"
                    value={form.hraReceived}
                    onChange={e => setForm({ ...form, hraReceived: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>Actual Rent Paid (₹)</label>
                  <input
                    type="number"
                    value={form.rentPaid}
                    onChange={e => setForm({ ...form, rentPaid: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>Professional Tax (₹)</label>
                  <input
                    type="number"
                    value={form.professionalTax}
                    onChange={e => setForm({ ...form, professionalTax: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>
            </div>

            {/* Head 2: House Property */}
            <div style={{ marginBottom: '20px', background: 'var(--card-subtle)', padding: '14px', borderRadius: '6px', border: '1px solid var(--border)' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: 'var(--accent)' }}>
                2. Income from House Property (Sec 22–27)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>Annual Rent Received (₹)</label>
                  <input
                    type="number"
                    value={form.annualRentReceived}
                    onChange={e => setForm({ ...form, annualRentReceived: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>Housing Loan Interest Sec 24(b) (₹)</label>
                  <input
                    type="number"
                    value={form.housingLoanInterest}
                    onChange={e => setForm({ ...form, housingLoanInterest: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>
            </div>

            {/* Head 3 & 4: Capital Gains & Other Sources */}
            <div style={{ marginBottom: '20px', background: 'var(--card-subtle)', padding: '14px', borderRadius: '6px', border: '1px solid var(--border)' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: 'var(--accent)' }}>
                3 &amp; 4. Capital Gains &amp; Other Sources
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>STCG 111A (Equity 15%/20%) (₹)</label>
                  <input
                    type="number"
                    value={form.stcg111A}
                    onChange={e => setForm({ ...form, stcg111A: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>LTCG 112A (Equity 10%/12.5%) (₹)</label>
                  <input
                    type="number"
                    value={form.ltcg112A}
                    onChange={e => setForm({ ...form, ltcg112A: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>Bank Interest (Savings + FD) (₹)</label>
                  <input
                    type="number"
                    value={form.savingsInterest + form.fdInterest}
                    onChange={e => setForm({ ...form, fdInterest: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>Dividend &amp; Other Income (₹)</label>
                  <input
                    type="number"
                    value={form.dividendIncome + form.otherIncome}
                    onChange={e => setForm({ ...form, dividendIncome: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>
            </div>

            {/* Chapter VI-A Deductions */}
            <div style={{ marginBottom: '20px', background: 'var(--card-subtle)', padding: '14px', borderRadius: '6px', border: '1px solid var(--border)' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: 'var(--accent)' }}>
                5. Chapter VI-A Deductions (Old Regime Only)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>80C (EPF/PPF/ELSS, max 1.5L) (₹)</label>
                  <input
                    type="number"
                    value={form.section80C}
                    onChange={e => setForm({ ...form, section80C: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>80CCD(1B) (NPS, max 50k) (₹)</label>
                  <input
                    type="number"
                    value={form.section80Ccd1B}
                    onChange={e => setForm({ ...form, section80Ccd1B: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>80D (Health Insurance) (₹)</label>
                  <input
                    type="number"
                    value={form.section80D}
                    onChange={e => setForm({ ...form, section80D: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>80TTA (Savings Interest) (₹)</label>
                  <input
                    type="number"
                    value={form.section80Tta}
                    onChange={e => setForm({ ...form, section80Tta: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>
            </div>

            {/* Prepaid Taxes */}
            <div style={{ marginBottom: '20px', background: 'var(--card-subtle)', padding: '14px', borderRadius: '6px', border: '1px solid var(--border)' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: 'var(--accent)' }}>
                Prepaid Taxes (TDS / Advance Tax)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>TDS / TCS Paid (₹)</label>
                  <input
                    type="number"
                    value={form.tdsPaid}
                    onChange={e => setForm({ ...form, tdsPaid: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)' }}>Advance Tax Paid (₹)</label>
                  <input
                    type="number"
                    value={form.advanceTaxPaid}
                    onChange={e => setForm({ ...form, advanceTaxPaid: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>
            </div>

            <div style={{ display: 'flex', gap: '12px' }}>
              <button
                onClick={handleCalculatePreview}
                disabled={calculating}
                style={{
                  flex: 1,
                  padding: '10px 16px',
                  background: 'var(--accent)',
                  color: '#fff',
                  border: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  cursor: 'pointer'
                }}
              >
                {calculating ? 'Evaluating Both Regimes...' : 'Calculate & Compare Regimes'}
              </button>
              {preview && (
                <button
                  onClick={handleSaveComputation}
                  disabled={saving}
                  style={{
                    padding: '10px 20px',
                    background: 'var(--success)',
                    color: '#fff',
                    border: 'none',
                    borderRadius: '6px',
                    fontWeight: 600,
                    cursor: 'pointer'
                  }}
                >
                  {saving ? 'Saving...' : 'Save Record'}
                </button>
              )}
            </div>
          </div>

          {/* Comparative Results Section */}
          <div>
            {!preview ? (
              <div style={{ background: 'var(--card)', border: '2px dashed var(--border)', borderRadius: '8px', padding: '48px 24px', textAlign: 'center', color: 'var(--muted)' }}>
                <div style={{ display: 'inline-flex', padding: '12px', borderRadius: '12px', background: 'var(--card-subtle)', border: '1px solid var(--border)', color: 'var(--accent)', marginBottom: '12px' }}>
                  <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                    <path d="m16 16 3-8 3 8c-.87.65-1.92 1-3 1s-2.13-.35-3-1Z" /><path d="m2 16 3-8 3 8c-.87.65-1.92 1-3 1s-2.13-.35-3-1Z" /><path d="M7 21h10" /><path d="M12 3v18" /><path d="M3 7h2c2 0 5-1 7-2 2 1 5 2 7 2h2" />
                  </svg>
                </div>
                <div style={{ fontWeight: 600, fontSize: '16px', marginBottom: '4px', color: 'var(--text)' }}>
                  Regime Comparative Simulator Ready
                </div>
                <div style={{ fontSize: '13px', maxWidth: '360px', margin: '0 auto' }}>
                  Fill in the income and deduction heads on the left and click <strong>Calculate &amp; Compare Regimes</strong> to see side-by-side Old vs New regime calculations.
                </div>
              </div>
            ) : (
              <div>
                {/* Recommendation Banner */}
                <div style={{
                  padding: '16px',
                  borderRadius: '8px',
                  marginBottom: '16px',
                  background: preview.recommendedRegime === 'new' ? 'rgba(37, 99, 235, 0.08)' : 'rgba(22, 163, 74, 0.08)',
                  border: `1px solid ${preview.recommendedRegime === 'new' ? 'rgba(37, 99, 235, 0.35)' : 'rgba(22, 163, 74, 0.35)'}`
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ fontWeight: 700, fontSize: '16px', color: preview.recommendedRegime === 'new' ? 'var(--accent)' : 'var(--success)' }}>
                      Recommended: {preview.recommendedRegime.toUpperCase()} REGIME
                    </div>
                    <div style={{
                      background: preview.recommendedRegime === 'new' ? 'var(--accent)' : 'var(--success)',
                      color: '#fff',
                      padding: '4px 10px',
                      borderRadius: '12px',
                      fontSize: '12px',
                      fontWeight: 600
                    }}>
                      Saves {fmtInr(preview.taxSavings)}
                    </div>
                  </div>
                  <div style={{ fontSize: '13px', marginTop: '6px', color: 'var(--text)' }}>
                    {preview.recommendationReason}
                  </div>
                </div>

                {/* Side by side comparison table */}
                <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                    <thead>
                      <tr style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                        <th style={{ textAlign: 'left', padding: '10px 14px', color: 'var(--muted)' }}>Tax Head / Step</th>
                        <th style={{ textAlign: 'right', padding: '10px 14px', color: 'var(--muted)' }}>Old Regime</th>
                        <th style={{ textAlign: 'right', padding: '10px 14px', color: 'var(--muted)' }}>New Regime (115BAC)</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>Gross Salary</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.grossSalary)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.grossSalary)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>Standard &amp; Salary Deductions</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: 'var(--success)' }}>- {fmtInr(preview.oldRegime.standardDeduction)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: 'var(--success)' }}>- {fmtInr(preview.newRegime.standardDeduction)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>Net Salary</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 500 }}>{fmtInr(preview.oldRegime.netSalary)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 500 }}>{fmtInr(preview.newRegime.netSalary)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>House Property Income</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.housePropertyIncome)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.housePropertyIncome)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>Capital Gains (STCG + LTCG)</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.capitalGainsStcg + preview.oldRegime.capitalGainsLtcg)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.capitalGainsStcg + preview.newRegime.capitalGainsLtcg)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>Other Sources (Interest/Div)</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.otherSourcesIncome)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.otherSourcesIncome)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--card-subtle)' }}>
                        <td style={{ padding: '8px 14px', fontWeight: 600 }}>Gross Total Income (GTI)</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 600 }}>{fmtInr(preview.oldRegime.grossTotalIncome)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 600 }}>{fmtInr(preview.newRegime.grossTotalIncome)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>Chapter VI-A Deductions</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: 'var(--success)' }}>- {fmtInr(preview.oldRegime.chapterViaDeductions)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: 'var(--muted)' }}>- {fmtInr(preview.newRegime.chapterViaDeductions)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--card-subtle)' }}>
                        <td style={{ padding: '8px 14px', fontWeight: 700 }}>Total Taxable Income</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 700 }}>{fmtInr(preview.oldRegime.taxableIncome)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 700 }}>{fmtInr(preview.newRegime.taxableIncome)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>Base Tax on Slabs</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.baseTax)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.baseTax)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>Special Rate Tax (CG)</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.specialRateTax)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.specialRateTax)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>Rebate u/s 87A</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: 'var(--success)' }}>- {fmtInr(preview.oldRegime.rebate87A)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: 'var(--success)' }}>- {fmtInr(preview.newRegime.rebate87A)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>4% Health &amp; Education Cess</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>+{fmtInr(preview.oldRegime.cess)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>+{fmtInr(preview.newRegime.cess)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)', background: 'rgba(217, 119, 6, 0.1)' }}>
                        <td style={{ padding: '10px 14px', fontWeight: 700, color: 'var(--warning)' }}>Total Tax Liability</td>
                        <td style={{ textAlign: 'right', padding: '10px 14px', fontWeight: 700, color: 'var(--warning)' }}>{fmtInr(preview.oldRegime.totalTaxLiability)}</td>
                        <td style={{ textAlign: 'right', padding: '10px 14px', fontWeight: 700, color: 'var(--warning)' }}>{fmtInr(preview.newRegime.totalTaxLiability)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '8px 14px' }}>TDS / Advance Taxes Paid</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: 'var(--accent)' }}>- {fmtInr(preview.oldRegime.prepaidTaxes)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: 'var(--accent)' }}>- {fmtInr(preview.newRegime.prepaidTaxes)}</td>
                      </tr>
                      <tr style={{ background: 'var(--card-subtle)' }}>
                        <td style={{ padding: '10px 14px', fontWeight: 700 }}>Net Payable / (Refund)</td>
                        <td style={{
                          textAlign: 'right',
                          padding: '10px 14px',
                          fontWeight: 700,
                          color: preview.oldRegime.netPayableOrRefund > 0 ? 'var(--error)' : 'var(--success)'
                        }}>
                          {fmtInr(preview.oldRegime.netPayableOrRefund)}
                        </td>
                        <td style={{
                          textAlign: 'right',
                          padding: '10px 14px',
                          fontWeight: 700,
                          color: preview.newRegime.netPayableOrRefund > 0 ? 'var(--error)' : 'var(--success)'
                        }}>
                          {fmtInr(preview.newRegime.netPayableOrRefund)}
                        </td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Tab 2: Saved Computations Records */}
      {activeTab === 'history' && (
        <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '20px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 600, margin: 0, color: 'var(--text)' }}>Saved Client Tax Computations</h2>
            <div style={{ display: 'flex', gap: '8px' }}>
              <select
                value={filterAy}
                onChange={e => setFilterAy(e.target.value)}
                style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '13px' }}
              >
                <option value="all">All AYs</option>
                <option value="2026-27">AY 2026-27</option>
                <option value="2025-26">AY 2025-26</option>
                <option value="2024-25">AY 2024-25</option>
              </select>
              <button
                onClick={loadComputations}
                style={{ padding: '6px 12px', background: 'var(--card-subtle)', border: '1px solid var(--border)', color: 'var(--text)', borderRadius: '6px', cursor: 'pointer' }}
              >
                Refresh
              </button>
            </div>
          </div>

          {loading ? (
            <div style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>Loading saved computations...</div>
          ) : computations.length === 0 ? (
            <div style={{ padding: '48px', textAlign: 'center', color: 'var(--muted)' }}>
              No tax computations found for the selected filter.
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', textAlign: 'left', color: 'var(--muted)' }}>
                  <th style={{ padding: '10px 12px' }}>Client Name</th>
                  <th style={{ padding: '10px 12px' }}>AY</th>
                  <th style={{ padding: '10px 12px' }}>Regime</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Taxable Income</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Total Tax</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Net Payable / (Refund)</th>
                  <th style={{ padding: '10px 12px' }}>Status</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {computations.map(c => (
                  <tr key={c.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '10px 12px', fontWeight: 500 }}>{c.clientName || 'Client ' + c.clientId.slice(0, 8)}</td>
                    <td style={{ padding: '10px 12px' }}>{c.assessmentYear}</td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{
                        padding: '2px 8px',
                        borderRadius: '4px',
                        background: c.optedRegime === 'new' ? 'rgba(37, 99, 235, 0.12)' : 'rgba(217, 119, 6, 0.12)',
                        color: c.optedRegime === 'new' ? 'var(--accent)' : 'var(--warning)',
                        fontWeight: 600,
                        fontSize: '11px'
                      }}>
                        {c.optedRegime.toUpperCase()}
                      </span>
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{fmtInr(c.totalTaxableIncome)}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 500 }}>{fmtInr(c.totalTaxLiability)}</td>
                    <td style={{
                      padding: '10px 12px',
                      textAlign: 'right',
                      fontWeight: 600,
                      color: c.netTaxPayableOrRefund > 0 ? 'var(--error)' : 'var(--success)'
                    }}>
                      {fmtInr(c.netTaxPayableOrRefund)}
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{
                        padding: '2px 8px',
                        borderRadius: '12px',
                        fontSize: '11px',
                        fontWeight: 600,
                        background: c.status === 'approved' ? 'rgba(22, 163, 74, 0.12)' : c.status === 'filed' ? 'rgba(37, 99, 235, 0.12)' : 'var(--card-subtle)',
                        color: c.status === 'approved' ? 'var(--success)' : c.status === 'filed' ? 'var(--accent)' : 'var(--muted)'
                      }}>
                        {c.status.toUpperCase()}
                      </span>
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                        {c.status === 'computed' && (
                          <button
                            onClick={() => handleStatusChange(c.id, 'approved')}
                            style={{ padding: '4px 8px', fontSize: '11px', background: 'var(--success)', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                          >
                            Approve
                          </button>
                        )}
                        {c.status === 'approved' && (
                          <button
                            onClick={() => handleStatusChange(c.id, 'filed')}
                            style={{ padding: '4px 8px', fontSize: '11px', background: 'var(--accent)', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                          >
                            Mark Filed
                          </button>
                        )}
                        <button
                          onClick={() => {
                            setItr1Export(null)
                            setSelectedComputation(c)
                          }}
                          style={{ padding: '4px 8px', fontSize: '11px', background: 'var(--card-subtle)', border: '1px solid var(--border)', color: 'var(--text)', borderRadius: '4px', cursor: 'pointer' }}
                        >
                          View / Export JSON
                        </button>
                        <button
                          onClick={() => handleDeleteComputation(c.id)}
                          style={{ padding: '4px 8px', fontSize: '11px', background: 'rgba(220, 38, 38, 0.12)', color: 'var(--error)', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {/* Modal / Overlay for viewing full breakdown */}
          {selectedComputation && (
            <div style={{
              position: 'fixed',
              top: 0, left: 0, right: 0, bottom: 0,
              background: 'rgba(15, 23, 42, 0.65)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              zIndex: 1000
            }}>
              <div style={{ background: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', borderRadius: '8px', width: '700px', maxHeight: '85vh', overflowY: 'auto', padding: '24px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                  <h3 style={{ margin: 0, fontSize: '18px' }}>
                    Computation Details: {selectedComputation.clientName} ({selectedComputation.assessmentYear})
                  </h3>
                  <button
                    onClick={() => setSelectedComputation(null)}
                    style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: 'var(--muted)' }}
                  >
                    ✕
                  </button>
                </div>
                <div style={{ fontSize: '13px', lineHeight: '1.6' }}>
                  <p><strong>Status:</strong> {selectedComputation.status.toUpperCase()}</p>
                  <p><strong>Opted Regime:</strong> {selectedComputation.optedRegime.toUpperCase()}</p>
                  <p><strong>Gross Total Income:</strong> {fmtInr(selectedComputation.grossTotalIncome)}</p>
                  <p><strong>Deductions:</strong> {fmtInr(selectedComputation.chapterViaDeductions + selectedComputation.salaryDeductions)}</p>
                  <p><strong>Total Taxable Income:</strong> {fmtInr(selectedComputation.totalTaxableIncome)}</p>
                  <p><strong>Tax on Income:</strong> {fmtInr(selectedComputation.taxOnIncome)}</p>
                  <p><strong>87A Rebate:</strong> {fmtInr(selectedComputation.rebate87A)}</p>
                  <p><strong>4% Cess:</strong> {fmtInr(selectedComputation.cess)}</p>
                  <p><strong>Total Tax Liability:</strong> {fmtInr(selectedComputation.totalTaxLiability)}</p>
                  <p><strong>TDS / Advance Tax:</strong> {fmtInr(selectedComputation.tdsTcsCredit + selectedComputation.advanceTaxPaid)}</p>
                  <p style={{ fontSize: '15px', fontWeight: 'bold', color: selectedComputation.netTaxPayableOrRefund > 0 ? 'var(--error)' : 'var(--success)' }}>
                    Net Payable / (Refund): {fmtInr(selectedComputation.netTaxPayableOrRefund)}
                  </p>
                </div>

                <JsonExportPanel
                  title="Official ITR-1 (Sahaj) JSON Export (AY 2026-27 V1.1)"
                  subtitle="Generates schema-validated Income Tax e-Filing JSON from this computation."
                  clientId={selectedComputation.clientId}
                  fy={selectedComputation.financialYear}
                  vaultTypeCode="ITR"
                  generating={exportingItr1}
                  result={itr1Export}
                  onGenerate={async () => {
                    try {
                      setExportingItr1(true)
                      setError(null)
                      const res = await taxApi.exportItr1(selectedComputation.id, itr1Opts)
                      setItr1Export(res)
                    } catch (err: unknown) {
                      setError(err instanceof Error ? err.message : 'ITR-1 JSON export failed.')
                    } finally {
                      setExportingItr1(false)
                    }
                  }}
                >
                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '8px' }}>
                    <input
                      type="text"
                      placeholder="Father's Name"
                      value={itr1Opts.fatherName || ''}
                      onChange={e => setItr1Opts(p => ({ ...p, fatherName: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      placeholder="City (e.g. Kochi)"
                      value={itr1Opts.city || ''}
                      onChange={e => setItr1Opts(p => ({ ...p, city: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      maxLength={6}
                      placeholder="PIN Code (e.g. 682001)"
                      value={itr1Opts.pinCode || ''}
                      onChange={e => setItr1Opts(p => ({ ...p, pinCode: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      maxLength={11}
                      placeholder="Refund IFSC (optional)"
                      value={itr1Opts.ifscCode || ''}
                      onChange={e => setItr1Opts(p => ({ ...p, ifscCode: e.target.value.toUpperCase() }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      placeholder="Bank A/c No. (optional)"
                      value={itr1Opts.bankAccountNo || ''}
                      onChange={e => setItr1Opts(p => ({ ...p, bankAccountNo: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    />
                  </div>
                </JsonExportPanel>

                <div style={{ marginTop: '20px', textAlign: 'right' }}>
                  <button
                    onClick={() => setSelectedComputation(null)}
                    style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: '1px solid var(--border)', color: 'var(--text)', borderRadius: '6px', cursor: 'pointer' }}
                  >
                    Close
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* AIS Import Modal */}
      {showAisModal && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(15, 23, 42, 0.65)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div style={{ background: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', padding: '24px', borderRadius: '12px', width: '650px', maxWidth: '95%', maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>
                Import Annual Information Statement (AIS / 26AS JSON)
              </h3>
              <button onClick={() => setShowAisModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '18px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            <p style={{ margin: '0 0 16px 0', fontSize: '13px', color: 'var(--muted)' }}>
              Paste the official JSON exported from the Income Tax e-filing portal (AIS / TIS / Form 26AS). CaDesk automatically categorizes Salary, Savings Interest, FD Interest, Dividends, Capital Gains, and extracts Advance Tax challans.
            </p>

            <form onSubmit={handleImportAis}>
              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '4px' }}>Client</label>
                <select
                  value={aisClientId}
                  onChange={e => setAisClientId(e.target.value)}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '13px' }}
                  required
                >
                  <option value="">-- Choose Client --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} {c.taxIds?.['PAN'] ? `(${c.taxIds['PAN']})` : ''}</option>
                  ))}
                </select>
              </div>

              <div style={{ marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>AIS JSON Content</label>
                  <button
                    type="button"
                    onClick={loadSampleAisJson}
                    style={{ background: 'transparent', border: 'none', color: 'var(--accent)', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
                  >
                    Load Sample AIS JSON
                  </button>
                </div>
                <textarea
                  rows={9}
                  value={aisJsonPayload}
                  onChange={e => setAisJsonPayload(e.target.value)}
                  placeholder="Paste AIS JSON exported from income-tax portal here..."
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px', fontFamily: 'monospace' }}
                  required
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowAisModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card-subtle)', color: 'var(--text)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={importingAis}
                  style={{ padding: '8px 20px', borderRadius: '6px', border: 'none', background: 'var(--accent)', color: '#FFFFFF', fontWeight: 600, cursor: 'pointer' }}
                >
                  {importingAis ? 'Importing & Computing...' : 'Import & Compute Tax'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
