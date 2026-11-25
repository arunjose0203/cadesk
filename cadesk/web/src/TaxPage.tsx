import { useEffect, useState } from 'react'
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

  useEffect(() => {
    loadClients()
    loadComputations()
  }, [filterAy])

  async function loadClients() {
    try {
      const res = await api<ClientPage | Client[]>('/api/clients?take=200')
      const clientList = Array.isArray(res) ? res : (res?.items || [])
      setClients(clientList)
      if (clientList.length > 0 && !form.clientId) {
        setForm(prev => ({ ...prev, clientId: clientList[0].id }))
      }
    } catch {
      // non fatal
    }
  }

  async function loadComputations() {
    try {
      setLoading(true)
      const list = await taxApi.list(undefined, filterAy === 'all' ? undefined : filterAy)
      setComputations(list || [])
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load computations')
    } finally {
      setLoading(false)
    }
  }

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
    <div style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto', color: '#1e293b' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 'bold', margin: '0 0 4px 0', color: '#0f172a' }}>
            Direct Tax Computation Engine
          </h1>
          <p style={{ margin: 0, color: '#64748b', fontSize: '14px' }}>
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
              background: '#4338ca',
              color: '#fff',
              fontWeight: 600,
              border: 'none',
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
              background: activeTab === 'calculator' ? '#2563eb' : '#f1f5f9',
              color: activeTab === 'calculator' ? '#fff' : '#475569',
              fontWeight: 500,
              border: 'none',
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
              background: activeTab === 'history' ? '#2563eb' : '#f1f5f9',
              color: activeTab === 'history' ? '#fff' : '#475569',
              fontWeight: 500,
              border: 'none',
              cursor: 'pointer'
            }}
          >
            Computation Records ({computations.length})
          </button>
        </div>
      </div>

      {error && (
        <div style={{ padding: '12px 16px', background: '#fef2f2', border: '1px solid #fecaca', borderRadius: '6px', color: '#dc2626', marginBottom: '16px' }}>
          {error}
        </div>
      )}

      {successMsg && (
        <div style={{ padding: '12px 16px', background: '#f0fdf4', border: '1px solid #bbf7d0', borderRadius: '6px', color: '#16a34a', marginBottom: '16px' }}>
          {successMsg}
        </div>
      )}

      {/* Tab 1: Calculator & Simulator */}
      {activeTab === 'calculator' && (
        <div style={{ display: 'grid', gridTemplateColumns: 'minmax(500px, 1.2fr) minmax(450px, 1fr)', gap: '24px' }}>
          {/* Inputs Section */}
          <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 600, marginTop: 0, marginBottom: '16px', borderBottom: '1px solid #f1f5f9', paddingBottom: '8px' }}>
              Taxpayer &amp; Assessment Setup
            </h2>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', marginBottom: '20px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '4px' }}>Select Client</label>
                <select
                  value={form.clientId}
                  onChange={e => setForm({ ...form, clientId: e.target.value })}
                  style={{ width: '100%', padding: '8px 10px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                >
                  <option value="">-- Choose Client --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} {c.taxIds?.['PAN'] ? `(${c.taxIds['PAN']})` : ''}</option>
                  ))}
                </select>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '13px', fontWeight: 500, marginBottom: '4px' }}>Assessment Year</label>
                <select
                  value={form.assessmentYear}
                  onChange={e => {
                    const ay = e.target.value
                    const fy = ay === '2026-27' ? '2025-26' : ay === '2025-26' ? '2024-25' : '2023-24'
                    setForm({ ...form, assessmentYear: ay, financialYear: fy })
                  }}
                  style={{ width: '100%', padding: '8px 10px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                >
                  <option value="2026-27">AY 2026-27 (FY 2025-26)</option>
                  <option value="2025-26">AY 2025-26 (FY 2024-25)</option>
                  <option value="2024-25">AY 2024-25 (FY 2023-24)</option>
                </select>
              </div>
            </div>

            {/* Head 1: Salary */}
            <div style={{ marginBottom: '20px', background: '#f8fafc', padding: '14px', borderRadius: '6px', border: '1px solid #e2e8f0' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: '#0369a1' }}>
                1. Income from Salary (Sec 15–17)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Gross Salary (₹)</label>
                  <input
                    type="number"
                    value={form.grossSalary}
                    onChange={e => setForm({ ...form, grossSalary: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>HRA Received (₹)</label>
                  <input
                    type="number"
                    value={form.hraReceived}
                    onChange={e => setForm({ ...form, hraReceived: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Actual Rent Paid (₹)</label>
                  <input
                    type="number"
                    value={form.rentPaid}
                    onChange={e => setForm({ ...form, rentPaid: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Professional Tax (₹)</label>
                  <input
                    type="number"
                    value={form.professionalTax}
                    onChange={e => setForm({ ...form, professionalTax: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>
            </div>

            {/* Head 2: House Property */}
            <div style={{ marginBottom: '20px', background: '#f8fafc', padding: '14px', borderRadius: '6px', border: '1px solid #e2e8f0' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: '#0369a1' }}>
                2. Income from House Property (Sec 22–27)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Annual Rent Received (₹)</label>
                  <input
                    type="number"
                    value={form.annualRentReceived}
                    onChange={e => setForm({ ...form, annualRentReceived: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Housing Loan Interest Sec 24(b) (₹)</label>
                  <input
                    type="number"
                    value={form.housingLoanInterest}
                    onChange={e => setForm({ ...form, housingLoanInterest: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>
            </div>

            {/* Head 3 & 4: Capital Gains & Other Sources */}
            <div style={{ marginBottom: '20px', background: '#f8fafc', padding: '14px', borderRadius: '6px', border: '1px solid #e2e8f0' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: '#0369a1' }}>
                3 &amp; 4. Capital Gains &amp; Other Sources
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>STCG 111A (Equity 15%/20%) (₹)</label>
                  <input
                    type="number"
                    value={form.stcg111A}
                    onChange={e => setForm({ ...form, stcg111A: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>LTCG 112A (Equity 10%/12.5%) (₹)</label>
                  <input
                    type="number"
                    value={form.ltcg112A}
                    onChange={e => setForm({ ...form, ltcg112A: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Bank Interest (Savings + FD) (₹)</label>
                  <input
                    type="number"
                    value={form.savingsInterest + form.fdInterest}
                    onChange={e => setForm({ ...form, fdInterest: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Dividend &amp; Other Income (₹)</label>
                  <input
                    type="number"
                    value={form.dividendIncome + form.otherIncome}
                    onChange={e => setForm({ ...form, dividendIncome: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>
            </div>

            {/* Chapter VI-A Deductions */}
            <div style={{ marginBottom: '20px', background: '#f8fafc', padding: '14px', borderRadius: '6px', border: '1px solid #e2e8f0' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: '#0369a1' }}>
                5. Chapter VI-A Deductions (Old Regime Only)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>80C (EPF/PPF/ELSS, max 1.5L) (₹)</label>
                  <input
                    type="number"
                    value={form.section80C}
                    onChange={e => setForm({ ...form, section80C: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>80CCD(1B) (NPS, max 50k) (₹)</label>
                  <input
                    type="number"
                    value={form.section80Ccd1B}
                    onChange={e => setForm({ ...form, section80Ccd1B: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>80D (Health Insurance) (₹)</label>
                  <input
                    type="number"
                    value={form.section80D}
                    onChange={e => setForm({ ...form, section80D: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>80TTA (Savings Interest) (₹)</label>
                  <input
                    type="number"
                    value={form.section80Tta}
                    onChange={e => setForm({ ...form, section80Tta: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>
            </div>

            {/* Prepaid Taxes */}
            <div style={{ marginBottom: '20px', background: '#f8fafc', padding: '14px', borderRadius: '6px', border: '1px solid #e2e8f0' }}>
              <div style={{ fontWeight: 600, fontSize: '14px', marginBottom: '10px', color: '#0369a1' }}>
                Prepaid Taxes (TDS / Advance Tax)
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>TDS / TCS Paid (₹)</label>
                  <input
                    type="number"
                    value={form.tdsPaid}
                    onChange={e => setForm({ ...form, tdsPaid: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: '#475569' }}>Advance Tax Paid (₹)</label>
                  <input
                    type="number"
                    value={form.advanceTaxPaid}
                    onChange={e => setForm({ ...form, advanceTaxPaid: Number(e.target.value) })}
                    style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1' }}
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
                  background: '#0284c7',
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
                    background: '#16a34a',
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
              <div style={{ background: '#fff', border: '2px dashed #cbd5e1', borderRadius: '8px', padding: '48px 24px', textAlign: 'center', color: '#64748b' }}>
                <div style={{ fontSize: '36px', marginBottom: '12px' }}>⚖️</div>
                <div style={{ fontWeight: 600, fontSize: '16px', marginBottom: '4px', color: '#334155' }}>
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
                  background: preview.recommendedRegime === 'new' ? '#eff6ff' : '#f0fdf4',
                  border: `1px solid ${preview.recommendedRegime === 'new' ? '#93c5fd' : '#86efac'}`
                }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div style={{ fontWeight: 700, fontSize: '16px', color: preview.recommendedRegime === 'new' ? '#1d4ed8' : '#15803d' }}>
                      Recommended: {preview.recommendedRegime.toUpperCase()} REGIME
                    </div>
                    <div style={{
                      background: preview.recommendedRegime === 'new' ? '#2563eb' : '#16a34a',
                      color: '#fff',
                      padding: '4px 10px',
                      borderRadius: '12px',
                      fontSize: '12px',
                      fontWeight: 600
                    }}>
                      Saves {fmtInr(preview.taxSavings)}
                    </div>
                  </div>
                  <div style={{ fontSize: '13px', marginTop: '6px', color: '#334155' }}>
                    {preview.recommendationReason}
                  </div>
                </div>

                {/* Side by side comparison table */}
                <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', overflow: 'hidden' }}>
                  <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                    <thead>
                      <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
                        <th style={{ textAlign: 'left', padding: '10px 14px', color: '#475569' }}>Tax Head / Step</th>
                        <th style={{ textAlign: 'right', padding: '10px 14px', color: '#475569' }}>Old Regime</th>
                        <th style={{ textAlign: 'right', padding: '10px 14px', color: '#475569' }}>New Regime (115BAC)</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>Gross Salary</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.grossSalary)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.grossSalary)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>Standard &amp; Salary Deductions</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: '#16a34a' }}>- {fmtInr(preview.oldRegime.standardDeduction)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: '#16a34a' }}>- {fmtInr(preview.newRegime.standardDeduction)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>Net Salary</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 500 }}>{fmtInr(preview.oldRegime.netSalary)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 500 }}>{fmtInr(preview.newRegime.netSalary)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>House Property Income</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.housePropertyIncome)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.housePropertyIncome)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>Capital Gains (STCG + LTCG)</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.capitalGainsStcg + preview.oldRegime.capitalGainsLtcg)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.capitalGainsStcg + preview.newRegime.capitalGainsLtcg)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>Other Sources (Interest/Div)</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.otherSourcesIncome)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.otherSourcesIncome)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #e2e8f0', background: '#f8fafc' }}>
                        <td style={{ padding: '8px 14px', fontWeight: 600 }}>Gross Total Income (GTI)</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 600 }}>{fmtInr(preview.oldRegime.grossTotalIncome)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 600 }}>{fmtInr(preview.newRegime.grossTotalIncome)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>Chapter VI-A Deductions</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: '#16a34a' }}>- {fmtInr(preview.oldRegime.chapterViaDeductions)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: '#64748b' }}>- {fmtInr(preview.newRegime.chapterViaDeductions)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #e2e8f0', background: '#f8fafc' }}>
                        <td style={{ padding: '8px 14px', fontWeight: 700 }}>Total Taxable Income</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 700 }}>{fmtInr(preview.oldRegime.taxableIncome)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', fontWeight: 700 }}>{fmtInr(preview.newRegime.taxableIncome)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>Base Tax on Slabs</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.baseTax)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.baseTax)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>Special Rate Tax (CG)</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.oldRegime.specialRateTax)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>{fmtInr(preview.newRegime.specialRateTax)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>Rebate u/s 87A</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: '#16a34a' }}>- {fmtInr(preview.oldRegime.rebate87A)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: '#16a34a' }}>- {fmtInr(preview.newRegime.rebate87A)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>4% Health &amp; Education Cess</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>+{fmtInr(preview.oldRegime.cess)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px' }}>+{fmtInr(preview.newRegime.cess)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #e2e8f0', background: '#fef3c7' }}>
                        <td style={{ padding: '10px 14px', fontWeight: 700, color: '#92400e' }}>Total Tax Liability</td>
                        <td style={{ textAlign: 'right', padding: '10px 14px', fontWeight: 700, color: '#92400e' }}>{fmtInr(preview.oldRegime.totalTaxLiability)}</td>
                        <td style={{ textAlign: 'right', padding: '10px 14px', fontWeight: 700, color: '#92400e' }}>{fmtInr(preview.newRegime.totalTaxLiability)}</td>
                      </tr>
                      <tr style={{ borderBottom: '1px solid #f1f5f9' }}>
                        <td style={{ padding: '8px 14px' }}>TDS / Advance Taxes Paid</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: '#2563eb' }}>- {fmtInr(preview.oldRegime.prepaidTaxes)}</td>
                        <td style={{ textAlign: 'right', padding: '8px 14px', color: '#2563eb' }}>- {fmtInr(preview.newRegime.prepaidTaxes)}</td>
                      </tr>
                      <tr style={{ background: '#f8fafc' }}>
                        <td style={{ padding: '10px 14px', fontWeight: 700 }}>Net Payable / (Refund)</td>
                        <td style={{
                          textAlign: 'right',
                          padding: '10px 14px',
                          fontWeight: 700,
                          color: preview.oldRegime.netPayableOrRefund > 0 ? '#dc2626' : '#16a34a'
                        }}>
                          {fmtInr(preview.oldRegime.netPayableOrRefund)}
                        </td>
                        <td style={{
                          textAlign: 'right',
                          padding: '10px 14px',
                          fontWeight: 700,
                          color: preview.newRegime.netPayableOrRefund > 0 ? '#dc2626' : '#16a34a'
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
        <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: '8px', padding: '20px' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <h2 style={{ fontSize: '18px', fontWeight: 600, margin: 0 }}>Saved Client Tax Computations</h2>
            <div style={{ display: 'flex', gap: '8px' }}>
              <select
                value={filterAy}
                onChange={e => setFilterAy(e.target.value)}
                style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '13px' }}
              >
                <option value="all">All AYs</option>
                <option value="2026-27">AY 2026-27</option>
                <option value="2025-26">AY 2025-26</option>
                <option value="2024-25">AY 2024-25</option>
              </select>
              <button
                onClick={loadComputations}
                style={{ padding: '6px 12px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '6px', cursor: 'pointer' }}
              >
                Refresh
              </button>
            </div>
          </div>

          {loading ? (
            <div style={{ padding: '32px', textAlign: 'center', color: '#64748b' }}>Loading saved computations...</div>
          ) : computations.length === 0 ? (
            <div style={{ padding: '48px', textAlign: 'center', color: '#64748b' }}>
              No tax computations found for the selected filter.
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: '#f8fafc', borderBottom: '1px solid #e2e8f0', textAlign: 'left' }}>
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
                  <tr key={c.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                    <td style={{ padding: '10px 12px', fontWeight: 500 }}>{c.clientName || 'Client ' + c.clientId.slice(0, 8)}</td>
                    <td style={{ padding: '10px 12px' }}>{c.assessmentYear}</td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{
                        padding: '2px 8px',
                        borderRadius: '4px',
                        background: c.optedRegime === 'new' ? '#eff6ff' : '#fef3c7',
                        color: c.optedRegime === 'new' ? '#1d4ed8' : '#b45309',
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
                      color: c.netTaxPayableOrRefund > 0 ? '#dc2626' : '#16a34a'
                    }}>
                      {fmtInr(c.netTaxPayableOrRefund)}
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{
                        padding: '2px 8px',
                        borderRadius: '12px',
                        fontSize: '11px',
                        fontWeight: 600,
                        background: c.status === 'approved' ? '#dcfce7' : c.status === 'filed' ? '#e0e7ff' : '#f1f5f9',
                        color: c.status === 'approved' ? '#15803d' : c.status === 'filed' ? '#4338ca' : '#475569'
                      }}>
                        {c.status.toUpperCase()}
                      </span>
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                        {c.status === 'computed' && (
                          <button
                            onClick={() => handleStatusChange(c.id, 'approved')}
                            style={{ padding: '4px 8px', fontSize: '11px', background: '#16a34a', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                          >
                            Approve
                          </button>
                        )}
                        {c.status === 'approved' && (
                          <button
                            onClick={() => handleStatusChange(c.id, 'filed')}
                            style={{ padding: '4px 8px', fontSize: '11px', background: '#4f46e5', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                          >
                            Mark Filed
                          </button>
                        )}
                        <button
                          onClick={() => {
                            setItr1Export(null)
                            setSelectedComputation(c)
                          }}
                          style={{ padding: '4px 8px', fontSize: '11px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '4px', cursor: 'pointer' }}
                        >
                          View / Export JSON
                        </button>
                        <button
                          onClick={() => handleDeleteComputation(c.id)}
                          style={{ padding: '4px 8px', fontSize: '11px', background: '#fee2e2', color: '#dc2626', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
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
              background: 'rgba(0,0,0,0.5)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              zIndex: 1000
            }}>
              <div style={{ background: '#fff', borderRadius: '8px', width: '700px', maxHeight: '85vh', overflowY: 'auto', padding: '24px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                  <h3 style={{ margin: 0, fontSize: '18px' }}>
                    Computation Details: {selectedComputation.clientName} ({selectedComputation.assessmentYear})
                  </h3>
                  <button
                    onClick={() => setSelectedComputation(null)}
                    style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer' }}
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
                  <p style={{ fontSize: '15px', fontWeight: 'bold', color: selectedComputation.netTaxPayableOrRefund > 0 ? '#dc2626' : '#16a34a' }}>
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
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      placeholder="City (e.g. Kochi)"
                      value={itr1Opts.city || ''}
                      onChange={e => setItr1Opts(p => ({ ...p, city: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      maxLength={6}
                      placeholder="PIN Code (e.g. 682001)"
                      value={itr1Opts.pinCode || ''}
                      onChange={e => setItr1Opts(p => ({ ...p, pinCode: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      maxLength={11}
                      placeholder="Refund IFSC (optional)"
                      value={itr1Opts.ifscCode || ''}
                      onChange={e => setItr1Opts(p => ({ ...p, ifscCode: e.target.value.toUpperCase() }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px' }}
                    />
                    <input
                      type="text"
                      placeholder="Bank A/c No. (optional)"
                      value={itr1Opts.bankAccountNo || ''}
                      onChange={e => setItr1Opts(p => ({ ...p, bankAccountNo: e.target.value }))}
                      style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid #cbd5e1', fontSize: '12px' }}
                    />
                  </div>
                </JsonExportPanel>

                <div style={{ marginTop: '20px', textAlign: 'right' }}>
                  <button
                    onClick={() => setSelectedComputation(null)}
                    style={{ padding: '8px 16px', background: '#f1f5f9', border: '1px solid #cbd5e1', borderRadius: '6px', cursor: 'pointer' }}
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
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div style={{ background: '#FFFFFF', padding: '24px', borderRadius: '12px', width: '650px', maxWidth: '95%', maxHeight: '90vh', overflowY: 'auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: '#111827' }}>
                Import Annual Information Statement (AIS / 26AS JSON)
              </h3>
              <button onClick={() => setShowAisModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '18px', cursor: 'pointer' }}>✕</button>
            </div>

            <p style={{ margin: '0 0 16px 0', fontSize: '13px', color: '#64748B' }}>
              Paste the official JSON exported from the Income Tax e-filing portal (AIS / TIS / Form 26AS). CaDesk automatically categorizes Salary, Savings Interest, FD Interest, Dividends, Capital Gains, and extracts Advance Tax challans.
            </p>

            <form onSubmit={handleImportAis}>
              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, color: '#374151', marginBottom: '4px' }}>Client</label>
                <select
                  value={aisClientId}
                  onChange={e => setAisClientId(e.target.value)}
                  style={{ width: '100%', padding: '8px 12px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '13px' }}
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
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#374151' }}>AIS JSON Content</label>
                  <button
                    type="button"
                    onClick={loadSampleAisJson}
                    style={{ background: 'transparent', border: 'none', color: '#4F46E5', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
                  >
                    Load Sample AIS JSON
                  </button>
                </div>
                <textarea
                  rows={9}
                  value={aisJsonPayload}
                  onChange={e => setAisJsonPayload(e.target.value)}
                  placeholder="Paste AIS JSON exported from income-tax portal here..."
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid #CBD5E1', fontSize: '12px', fontFamily: 'monospace' }}
                  required
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowAisModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid #CBD5E1', background: '#FFFFFF', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={importingAis}
                  style={{ padding: '8px 20px', borderRadius: '6px', border: 'none', background: '#4338CA', color: '#FFFFFF', fontWeight: 600, cursor: 'pointer' }}
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
