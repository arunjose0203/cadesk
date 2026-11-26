import { useState } from 'react'
import {
  type BusinessPrefillResponseDto,
  type BusinessTaxComputationResultDto,
  type CalculateBusinessTaxRequest,
  type Client,
  type ExportItrJsonRequest,
  type IncomeTaxJsonExportResult,
  type PartnerShareInput,
  type TaxComputationDto,
  taxApi,
} from '../api'
import { JsonExportPanel } from '../components/JsonExportPanel'

type Props = {
  clients: Client[]
  onSaved: () => Promise<void>
  onError: (msg: string | null) => void
  onSuccess: (msg: string | null) => void
}

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n)

const inputStyle: React.CSSProperties = {
  width: '100%',
  padding: '7px 10px',
  borderRadius: '6px',
  border: '1px solid var(--border)',
  background: 'var(--card)',
  color: 'var(--text)',
  fontSize: '13px',
  boxSizing: 'border-box',
}

const labelStyle: React.CSSProperties = {
  display: 'block',
  fontSize: '12px',
  fontWeight: 600,
  marginBottom: '4px',
  color: 'var(--text-secondary)',
}

const defaultRequest = (clientId = ''): CalculateBusinessTaxRequest => ({
  clientId,
  assessmentYear: '2026-27',
  financialYear: '2025-26',
  entityType: 'company',
  subType: 'Private',
  corporateRegime: 'auto',
  isSmallTurnover25Percent: true,
  incorporationDate: '2021-04-01',
  isTaxAuditApplicable: true,
  isIndAsApplicable: false,
  natureOfBusinessCode: '16013',
  notes: '',
  partnerOrShareCapital: 1000000,
  authorisedShareCapital: 5000000,
  reservesAndSurplus: 2500000,
  longTermBorrowings: 1200000,
  shortTermBorrowings: 400000,
  deferredTaxLiability: 0,
  tradePayables: 650000,
  otherCurrentLiabilities: 250000,
  fixedAssetsGrossBlock: 3600000,
  fixedAssetsAccumulatedDep: 800000,
  fixedAssetsNetBlock: 2800000,
  capitalWip: 0,
  nonCurrentInvestments: 0,
  currentInvestments: 0,
  inventories: 900000,
  tradeReceivables: 1400000,
  cashAndBankBalances: 900000,
  loansAndAdvances: 0,
  otherCurrentAssets: 0,
  revenueFromOperations: 35000000,
  otherIncome: 150000,
  interestIncome: 0,
  dividendIncome: 0,
  rentIncome: 0,
  purchasesAndDirectCost: 22000000,
  changesInInventories: 0,
  employeeBenefitExpense: 4200000,
  partnerRemunerationDebited: 0,
  financeCost: 220000,
  partnerInterestDebited: 0,
  depreciationPerBooks: 550000,
  auditFee: 75000,
  otherExpenses: 1850000,
  taxProvisionCurrent: 1200000,
  taxProvisionDeferred: 0,
  balanceBroughtForwardPl: 0,
  disallowSec36: 0,
  disallowSec37: 0,
  disallowSec40a: 45000,
  disallowSec40A3: 0,
  disallowSec43B: 0,
  disallowSec43BhMsme: 85000,
  otherDisallowances: 0,
  allowableDeductionsUs32Etc: 0,
  incomeTaxDepreciationSec32: 680000,
  deductionSec10AA: 0,
  deductionSec35AD: 0,
  housePropertyIncome: 0,
  stcg111A: 0,
  stcgNormal: 0,
  ltcg112A: 0,
  ltcg112: 0,
  otherSourcesIncome: 0,
  deduction80G: 0,
  deduction80Jjaa: 0,
  deduction80M: 0,
  otherChapViaDeductions: 0,
  broughtForwardBusinessLoss: 0,
  unabsorbedDepreciation: 0,
  matCreditBroughtForward: 0,
  amtCreditBroughtForward: 0,
  matAdditionsOther: 0,
  matDeductionsOther: 0,
  advanceTaxPaid: 850000,
  tdsCredit: 180000,
  tcsCredit: 0,
  selfAssessmentTaxPaid: 0,
  interest234A: 0,
  interest234B: 0,
  interest234C: 0,
  lateFee234F: 0,
  partners: [
    { name: 'Managing Partner', pan: 'ABCPM1111K', status: 'IND_WORKING', sharePercent: 50, capitalBalance: 1000000, interestRatePercent: 12, interestPaid: 120000, remunerationPaid: 600000 },
    { name: 'Working Partner', pan: 'ABCPN2222L', status: 'IND_WORKING', sharePercent: 50, capitalBalance: 1000000, interestRatePercent: 12, interestPaid: 120000, remunerationPaid: 600000 },
  ],
  keyPersons: [
    { name: 'Managing Director', designation: 'MD', pan: 'ABCPD1234Q', din: '01234567' },
  ],
  shareholders: [],
  depreciationBlocks: [],
  advanceTaxChallans: [],
  tdsCreditEntries: [],
})

export function BusinessTaxDeskTab({ clients, onSaved, onError, onSuccess }: Props) {
  const [form, setForm] = useState<CalculateBusinessTaxRequest>(() => defaultRequest(clients[0]?.id || ''))
  const [prefillSources, setPrefillSources] = useState<BusinessPrefillResponseDto['sources']>([])
  const [prefilling, setPrefilling] = useState(false)
  const [calculating, setCalculating] = useState(false)
  const [saving, setSaving] = useState(false)
  const [preview, setPreview] = useState<BusinessTaxComputationResultDto | null>(null)
  const [savedComp, setSavedComp] = useState<TaxComputationDto | null>(null)
  const [showBsSection, setShowBsSection] = useState(false)

  const [exportOpts, setExportOpts] = useState<ExportItrJsonRequest>({
    verificationName: '',
    verificationPan: 'ABCPA1234A',
    fatherName: 'NA',
    capacity: 'MD',
    city: 'Kochi',
    pinCode: '682001',
    ifscCode: '',
    bankAccountNo: '',
    bankAccountType: 'CA',
  })
  const [exporting, setExporting] = useState(false)
  const [exportResult, setExportResult] = useState<IncomeTaxJsonExportResult | null>(null)

  const setNum = (field: keyof CalculateBusinessTaxRequest, val: string) => {
    setForm(prev => ({ ...prev, [field]: Number(val) || 0 }))
  }

  const handleEntitySwitch = (entityType: 'firm' | 'company') => {
    setPreview(null)
    setSavedComp(null)
    setExportResult(null)
    setForm(prev => ({
      ...prev,
      entityType,
      subType: entityType === 'firm' ? 'Partnership' : 'Private',
      corporateRegime: entityType === 'firm' ? 'normal' : 'auto',
      partnerRemunerationDebited: entityType === 'firm' ? (prev.partnerRemunerationDebited || 1200000) : 0,
      partnerInterestDebited: entityType === 'firm' ? (prev.partnerInterestDebited || 240000) : 0,
    }))
    setExportOpts(prev => ({ ...prev, capacity: entityType === 'firm' ? 'MP' : 'MD' }))
  }

  const handlePrefill = async () => {
    if (!form.clientId) {
      onError('Select a Firm / LLP / Company client first to auto-fill from Books.')
      return
    }
    onError(null)
    onSuccess(null)
    setPrefilling(true)
    try {
      const res = await taxApi.prefillBusiness({
        clientId: form.clientId,
        financialYear: form.financialYear,
        assessmentYear: form.assessmentYear,
        entityType: form.entityType,
      })
      const pre = res.prefilledRequest
      setForm(prev => ({
        ...prev,
        ...pre,
        partners: pre.partners?.length ? pre.partners : prev.partners,
        keyPersons: pre.keyPersons?.length ? pre.keyPersons : prev.keyPersons,
      }))
      setPrefillSources(res.sources)
      setExportOpts(prev => ({
        ...prev,
        capacity: pre.entityType === 'firm' ? 'MP' : 'MD',
        verificationName: pre.keyPersons?.[0]?.name || pre.partners?.[0]?.name || prev.verificationName,
        verificationPan: pre.keyPersons?.[0]?.pan || pre.partners?.[0]?.pan || prev.verificationPan,
      }))
      const calc = await taxApi.previewBusiness(pre)
      setPreview(calc)
      onSuccess('Auto-filled figures from Trial Balance, Fixed Assets, Form 3CD, Advance Tax, and MCA Master.')
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'Auto-prefill failed.')
    } finally {
      setPrefilling(false)
    }
  }

  const handlePreview = async () => {
    if (!form.clientId) {
      onError('Please select a client before computing Business Tax.')
      return
    }
    onError(null)
    setCalculating(true)
    try {
      const res = await taxApi.previewBusiness(form)
      setPreview(res)
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'Business tax calculation failed.')
    } finally {
      setCalculating(false)
    }
  }

  const handleSave = async () => {
    if (!form.clientId) {
      onError('Please select a client to save this computation.')
      return
    }
    onError(null)
    setSaving(true)
    try {
      const saved = await taxApi.saveBusiness(form)
      setSavedComp(saved)
      setPreview(saved.businessComputation || preview)
      setExportResult(null)
      await onSaved()
      onSuccess(`Saved ${form.entityType === 'firm' ? 'ITR-5' : 'ITR-6'} computation for ${saved.clientName || 'client'} (AY ${saved.assessmentYear}).`)
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'Failed to save business tax computation.')
    } finally {
      setSaving(false)
    }
  }

  const updatePartner = (idx: number, patch: Partial<PartnerShareInput>) => {
    setForm(prev => {
      const list = [...(prev.partners || [])]
      list[idx] = { ...list[idx], ...patch }
      const totRemun = list.reduce((s, p) => s + (Number(p.remunerationPaid) || 0), 0)
      const totInt = list.reduce((s, p) => s + (Number(p.interestPaid) || 0), 0)
      return { ...prev, partners: list, partnerRemunerationDebited: totRemun, partnerInterestDebited: totInt }
    })
  }

  const addPartner = () => {
    setForm(prev => ({
      ...prev,
      partners: [
        ...(prev.partners || []),
        { name: `Partner ${(prev.partners?.length || 0) + 1}`, pan: 'ABCPP3333M', status: 'IND_WORKING', sharePercent: 25, capitalBalance: 500000, interestRatePercent: 12, interestPaid: 60000, remunerationPaid: 300000 },
      ],
    }))
  }

  const handleGenerateExport = async () => {
    if (!savedComp) return
    onError(null)
    setExporting(true)
    try {
      const res = form.entityType === 'firm'
        ? await taxApi.exportItr5(savedComp.id, exportOpts)
        : await taxApi.exportItr6(savedComp.id, exportOpts)
      setExportResult(res)
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'JSON export failed.')
    } finally {
      setExporting(false)
    }
  }

  const isFirm = form.entityType === 'firm'

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'minmax(500px, 1.25fr) minmax(430px, 1fr)', gap: '24px' }}>
      {/* Left: Business Books, Adjustments & Statutory Entity Inputs */}
      <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '20px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', flexWrap: 'wrap', gap: '10px', borderBottom: '1px solid var(--border)', paddingBottom: '10px' }}>
          <div>
            <h2 style={{ fontSize: '18px', fontWeight: 700, margin: 0, color: 'var(--text)' }}>
              Corporate (ITR-6) &amp; Firm / LLP (ITR-5) Computation
            </h2>
            <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '2px' }}>
              Schedule BP, Sec 32 WDV, Sec 40(b) partner caps, Sec 115BAA/BAB vs MAT 115JB, AMT 115JC
            </div>
          </div>
          <button
            type="button"
            disabled={prefilling}
            onClick={handlePrefill}
            style={{
              padding: '7px 12px',
              borderRadius: '6px',
              background: 'var(--accent)',
              color: '#fff',
              border: 'none',
              fontSize: '12px',
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            {prefilling ? 'Pulling Books...' : '⚡ Auto-Fill from Trial Balance & 3CD'}
          </button>
        </div>

        {/* Entity Mode Toggle */}
        <div style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
          <button
            type="button"
            onClick={() => handleEntitySwitch('company')}
            style={{
              flex: 1,
              padding: '9px 12px',
              borderRadius: '6px',
              border: !isFirm ? '1px solid var(--accent)' : '1px solid var(--border)',
              background: !isFirm ? 'var(--accent-subtle)' : 'var(--card-subtle)',
              color: !isFirm ? 'var(--accent)' : 'var(--text)',
              fontWeight: 600,
              fontSize: '13px',
              cursor: 'pointer',
            }}
          >
            Company (ITR-6 • 115BAA / MAT)
          </button>
          <button
            type="button"
            onClick={() => handleEntitySwitch('firm')}
            style={{
              flex: 1,
              padding: '9px 12px',
              borderRadius: '6px',
              border: isFirm ? '1px solid var(--accent)' : '1px solid var(--border)',
              background: isFirm ? 'var(--accent-subtle)' : 'var(--card-subtle)',
              color: isFirm ? 'var(--accent)' : 'var(--text)',
              fontWeight: 600,
              fontSize: '13px',
              cursor: 'pointer',
            }}
          >
            Partnership Firm / LLP / AOP (ITR-5 • Sec 40(b))
          </button>
        </div>

        {/* Client, AY & Regime Setup */}
        <div style={{ display: 'grid', gridTemplateColumns: '1.3fr 1fr 1fr', gap: '12px', marginBottom: '16px' }}>
          <div>
            <label style={labelStyle}>Client (Firm / Company)</label>
            <select
              value={form.clientId}
              onChange={e => {
                const cid = e.target.value
                const cl = clients.find(c => c.id === cid)
                const et = (cl?.entityType || '').toLowerCase()
                const nextEntity: 'firm' | 'company' = et.includes('firm') || et.includes('llp') || et.includes('partner') ? 'firm' : 'company'
                setForm(prev => ({
                  ...prev,
                  clientId: cid,
                  entityType: cl ? nextEntity : prev.entityType,
                  subType: cl ? (et.includes('llp') ? 'LLP' : et.includes('public') ? 'Public' : nextEntity === 'firm' ? 'Partnership' : 'Private') : prev.subType,
                }))
              }}
              style={inputStyle}
            >
              <option value="">-- Choose Client --</option>
              {clients.map(c => (
                <option key={c.id} value={c.id}>
                  {c.name} ({c.entityType}) {c.taxIds?.['PAN'] ? `• ${c.taxIds['PAN']}` : ''}
                </option>
              ))}
            </select>
          </div>

          <div>
            <label style={labelStyle}>Assessment Year</label>
            <select
              value={form.assessmentYear}
              onChange={e => {
                const ay = e.target.value
                const fy = ay === '2026-27' ? '2025-26' : '2024-25'
                setForm(prev => ({ ...prev, assessmentYear: ay, financialYear: fy }))
              }}
              style={inputStyle}
            >
              <option value="2026-27">AY 2026-27 (FY 2025-26)</option>
              <option value="2025-26">AY 2025-26 (FY 2024-25)</option>
            </select>
          </div>

          {!isFirm ? (
            <div>
              <label style={labelStyle}>Corporate Tax Section</label>
              <select
                value={form.corporateRegime}
                onChange={e => setForm(prev => ({ ...prev, corporateRegime: e.target.value as CalculateBusinessTaxRequest['corporateRegime'] }))}
                style={inputStyle}
              >
                <option value="auto">Auto-Compare Optimal</option>
                <option value="115BAA">Sec 115BAA (22% + 10% SC)</option>
                <option value="normal">Normal (25%/30% + MAT)</option>
                <option value="115BAB">Sec 115BAB (15% Mfg)</option>
              </select>
            </div>
          ) : (
            <div>
              <label style={labelStyle}>Firm Constitution</label>
              <select
                value={form.subType}
                onChange={e => setForm(prev => ({ ...prev, subType: e.target.value }))}
                style={inputStyle}
              >
                <option value="Partnership">Partnership Firm</option>
                <option value="LLP">Limited Liability Partnership (LLP)</option>
                <option value="AOP">AOP / BOI</option>
              </select>
            </div>
          )}
        </div>

        {/* Auto-Prefill Diagnostics */}
        {prefillSources.length > 0 && (
          <div style={{ marginBottom: '16px', padding: '10px 12px', borderRadius: '6px', background: 'var(--card-subtle)', border: '1px solid var(--border)', fontSize: '12px' }}>
            <div style={{ fontWeight: 700, marginBottom: '6px', color: 'var(--text)' }}>Integrated Books &amp; Register Sources</div>
            {prefillSources.map(s => (
              <div key={s.module} style={{ display: 'flex', gap: '8px', alignItems: 'center', marginBottom: '3px', color: s.found ? 'var(--text-secondary)' : 'var(--muted)' }}>
                <span style={{ color: s.found ? 'var(--success)' : 'var(--muted)', fontWeight: 700 }}>{s.found ? '✓' : '○'}</span>
                <strong>{s.module}:</strong> <span>{s.summary}</span>
              </div>
            ))}
          </div>
        )}

        {/* 1. P&L Statement */}
        <h3 style={{ fontSize: '13px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--accent)', margin: '0 0 10px 0' }}>
          1. Profit &amp; Loss Statement (Part A-P&amp;L)
        </h3>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px', marginBottom: '16px' }}>
          <div>
            <label style={labelStyle}>Revenue from Operations</label>
            <input type="number" style={inputStyle} value={form.revenueFromOperations} onChange={e => setNum('revenueFromOperations', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Other Operating Income</label>
            <input type="number" style={inputStyle} value={form.otherIncome} onChange={e => setNum('otherIncome', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Purchases &amp; Direct Cost</label>
            <input type="number" style={inputStyle} value={form.purchasesAndDirectCost} onChange={e => setNum('purchasesAndDirectCost', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Employee Benefits Expense</label>
            <input type="number" style={inputStyle} value={form.employeeBenefitExpense} onChange={e => setNum('employeeBenefitExpense', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Finance Cost (Excl. Partners)</label>
            <input type="number" style={inputStyle} value={form.financeCost} onChange={e => setNum('financeCost', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Depreciation (Books / Co. Act)</label>
            <input type="number" style={inputStyle} value={form.depreciationPerBooks} onChange={e => setNum('depreciationPerBooks', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Other Expenses</label>
            <input type="number" style={inputStyle} value={form.otherExpenses} onChange={e => setNum('otherExpenses', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Current Tax Provision</label>
            <input type="number" style={inputStyle} value={form.taxProvisionCurrent} onChange={e => setNum('taxProvisionCurrent', e.target.value)} />
          </div>
          <div style={{ display: 'flex', alignItems: 'flex-end', gap: '8px', paddingBottom: '4px' }}>
            <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text)', cursor: 'pointer' }}>
              <input type="checkbox" checked={form.isTaxAuditApplicable} onChange={e => setForm(p => ({ ...p, isTaxAuditApplicable: e.target.checked }))} />
              44AB Tax Audit
            </label>
            {!isFirm && (
              <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text)', cursor: 'pointer' }}>
                <input type="checkbox" checked={form.isSmallTurnover25Percent} onChange={e => setForm(p => ({ ...p, isSmallTurnover25Percent: e.target.checked }))} />
                Turnover ≤ ₹400Cr (25%)
              </label>
            )}
          </div>
        </div>

        {/* 2. Firm Partners Sec 40(b) OR Company MAT/Shareholding */}
        {isFirm ? (
          <div style={{ marginBottom: '16px', padding: '12px', borderRadius: '6px', background: 'var(--card-subtle)', border: '1px solid var(--border)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
              <h3 style={{ fontSize: '13px', fontWeight: 700, margin: 0, color: 'var(--accent)' }}>
                2. Partners &amp; Sec 40(b) Remuneration / Interest (ITR-5 Gen-2)
              </h3>
              <button
                type="button"
                onClick={addPartner}
                style={{ padding: '4px 10px', borderRadius: '4px', background: 'var(--card)', border: '1px solid var(--border)', color: 'var(--text)', fontSize: '12px', cursor: 'pointer' }}
              >
                + Add Partner
              </button>
            </div>
            {(form.partners || []).map((p, idx) => (
              <div key={idx} style={{ display: 'grid', gridTemplateColumns: '1.2fr 1fr 0.7fr 1fr 1fr 1fr', gap: '6px', marginBottom: '6px' }}>
                <input placeholder="Partner Name" style={inputStyle} value={p.name} onChange={e => updatePartner(idx, { name: e.target.value })} />
                <input placeholder="PAN" maxLength={10} style={inputStyle} value={p.pan} onChange={e => updatePartner(idx, { pan: e.target.value.toUpperCase() })} />
                <input type="number" title="Share %" style={inputStyle} value={p.sharePercent} onChange={e => updatePartner(idx, { sharePercent: Number(e.target.value) || 0 })} />
                <input type="number" title="Capital Balance" placeholder="Capital" style={inputStyle} value={p.capitalBalance} onChange={e => updatePartner(idx, { capitalBalance: Number(e.target.value) || 0 })} />
                <input type="number" title="Interest Paid" placeholder="Interest" style={inputStyle} value={p.interestPaid} onChange={e => updatePartner(idx, { interestPaid: Number(e.target.value) || 0 })} />
                <input type="number" title="Remuneration Paid" placeholder="Remun." style={inputStyle} value={p.remunerationPaid} onChange={e => updatePartner(idx, { remunerationPaid: Number(e.target.value) || 0 })} />
              </div>
            ))}
          </div>
        ) : (
          <div style={{ marginBottom: '16px', padding: '12px', borderRadius: '6px', background: 'var(--card-subtle)', border: '1px solid var(--border)' }}>
            <h3 style={{ fontSize: '13px', fontWeight: 700, margin: '0 0 8px 0', color: 'var(--accent)' }}>
              2. Corporate Governance &amp; MAT / Tax Credits (ITR-6)
            </h3>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
              <div>
                <label style={labelStyle}>MD / Director Name</label>
                <input
                  style={inputStyle}
                  value={form.keyPersons?.[0]?.name || ''}
                  onChange={e => setForm(prev => ({
                    ...prev,
                    keyPersons: [{ name: e.target.value, designation: 'MD', pan: prev.keyPersons?.[0]?.pan || '', din: prev.keyPersons?.[0]?.din || '' }],
                  }))}
                />
              </div>
              <div>
                <label style={labelStyle}>Director DIN (8 digits)</label>
                <input
                  style={inputStyle}
                  maxLength={8}
                  value={form.keyPersons?.[0]?.din || ''}
                  onChange={e => setForm(prev => ({
                    ...prev,
                    keyPersons: [{ name: prev.keyPersons?.[0]?.name || 'Director', designation: 'MD', pan: prev.keyPersons?.[0]?.pan || '', din: e.target.value }],
                  }))}
                />
              </div>
              <div>
                <label style={labelStyle}>MAT Credit B/F (Sec 115JAA)</label>
                <input type="number" style={inputStyle} value={form.matCreditBroughtForward} onChange={e => setNum('matCreditBroughtForward', e.target.value)} />
              </div>
            </div>
          </div>
        )}

        {/* 3. Tax Adjustments & Disallowances */}
        <h3 style={{ fontSize: '13px', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--accent)', margin: '0 0 10px 0' }}>
          3. Schedule BP Disallowances, IT Depreciation &amp; Other Heads
        </h3>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px', marginBottom: '16px' }}>
          <div>
            <label style={labelStyle}>IT Act Sec 32 Depreciation</label>
            <input type="number" style={inputStyle} value={form.incomeTaxDepreciationSec32} onChange={e => setNum('incomeTaxDepreciationSec32', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Disallow u/s 40(a) (TDS)</label>
            <input type="number" style={inputStyle} value={form.disallowSec40a} onChange={e => setNum('disallowSec40a', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>MSME Overdue u/s 43B(h)</label>
            <input type="number" style={inputStyle} value={form.disallowSec43BhMsme} onChange={e => setNum('disallowSec43BhMsme', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Unpaid Statutory u/s 43B</label>
            <input type="number" style={inputStyle} value={form.disallowSec43B} onChange={e => setNum('disallowSec43B', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Cash &amp; Late PF (40A(3)/36)</label>
            <input type="number" style={inputStyle} value={form.disallowSec40A3} onChange={e => setNum('disallowSec40A3', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>B/F Loss / Unabs. Dep.</label>
            <input type="number" style={inputStyle} value={form.broughtForwardBusinessLoss} onChange={e => setNum('broughtForwardBusinessLoss', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>STCG 111A (20%) / LTCG 112A</label>
            <input type="number" style={inputStyle} value={form.stcg111A} onChange={e => setNum('stcg111A', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Sec 80JJAA / 80M / 80G</label>
            <input type="number" style={inputStyle} value={form.deduction80Jjaa} onChange={e => setNum('deduction80Jjaa', e.target.value)} />
          </div>
          <div>
            <label style={labelStyle}>Advance Tax + TDS Credits</label>
            <input type="number" style={inputStyle} value={form.advanceTaxPaid + form.tdsCredit} onChange={e => setForm(p => ({ ...p, advanceTaxPaid: Number(e.target.value) || 0, tdsCredit: 0 }))} />
          </div>
        </div>

        {/* Collapsible Balance Sheet Summary */}
        <div style={{ marginBottom: '16px' }}>
          <button
            type="button"
            onClick={() => setShowBsSection(s => !s)}
            style={{ background: 'var(--card-subtle)', border: '1px solid var(--border)', color: 'var(--text)', padding: '6px 12px', borderRadius: '6px', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
          >
            {showBsSection ? '▼ Hide Balance Sheet (Part A-BS)' : '▶ Edit Balance Sheet Figures (Part A-BS)'}
          </button>
          {showBsSection && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px', marginTop: '10px', padding: '12px', borderRadius: '6px', background: 'var(--card-subtle)', border: '1px solid var(--border)' }}>
              <div><label style={labelStyle}>Capital / Paid-Up Share Cap</label><input type="number" style={inputStyle} value={form.partnerOrShareCapital} onChange={e => setNum('partnerOrShareCapital', e.target.value)} /></div>
              <div><label style={labelStyle}>Reserves &amp; Surplus</label><input type="number" style={inputStyle} value={form.reservesAndSurplus} onChange={e => setNum('reservesAndSurplus', e.target.value)} /></div>
              <div><label style={labelStyle}>Long Term Borrowings</label><input type="number" style={inputStyle} value={form.longTermBorrowings} onChange={e => setNum('longTermBorrowings', e.target.value)} /></div>
              <div><label style={labelStyle}>Fixed Assets Net Block</label><input type="number" style={inputStyle} value={form.fixedAssetsNetBlock} onChange={e => setNum('fixedAssetsNetBlock', e.target.value)} /></div>
              <div><label style={labelStyle}>Inventories &amp; Debtors</label><input type="number" style={inputStyle} value={form.inventories + form.tradeReceivables} onChange={e => setForm(p => ({ ...p, inventories: Number(e.target.value) || 0, tradeReceivables: 0 }))} /></div>
              <div><label style={labelStyle}>Cash &amp; Bank Balances</label><input type="number" style={inputStyle} value={form.cashAndBankBalances} onChange={e => setNum('cashAndBankBalances', e.target.value)} /></div>
            </div>
          )}
        </div>

        <div style={{ display: 'flex', gap: '12px' }}>
          <button
            type="button"
            disabled={calculating}
            onClick={handlePreview}
            style={{ flex: 1, padding: '10px 16px', borderRadius: '6px', background: 'var(--accent)', color: '#fff', fontWeight: 600, border: 'none', cursor: 'pointer' }}
          >
            {calculating ? 'Computing...' : `Calculate ${isFirm ? 'ITR-5 (Firm/LLP)' : 'ITR-6 (Company)'}`}
          </button>
          <button
            type="button"
            disabled={saving}
            onClick={handleSave}
            style={{ flex: 1, padding: '10px 16px', borderRadius: '6px', background: 'var(--success)', color: '#fff', fontWeight: 600, border: 'none', cursor: 'pointer' }}
          >
            {saving ? 'Saving...' : 'Save Computation & Unlock JSON'}
          </button>
        </div>
      </div>

      {/* Right: Regime Comparison, Sec 40(b) / MAT Analysis & Official ITR-5/ITR-6 JSON Export */}
      <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
        {!preview ? (
          <div style={{ textAlign: 'center', padding: '48px 16px', color: 'var(--muted)' }}>
            <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text)', marginBottom: '6px' }}>
              {isFirm ? 'Partnership / LLP Computation & ITR-5 Builder Ready' : 'Corporate Tax & ITR-6 Builder Ready'}
            </div>
            <div style={{ fontSize: '13px' }}>
              Click <strong>Auto-Fill from Trial Balance &amp; 3CD</strong> or enter book figures and click <strong>Calculate</strong> to compare regimes and validate CBDT AY 2026-27 JSON.
            </div>
          </div>
        ) : (
          <>
            {/* Summary Banner */}
            <div style={{ padding: '14px', borderRadius: '8px', background: 'var(--accent-subtle)', border: '1px solid var(--accent)' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '8px', flexWrap: 'wrap' }}>
                <div style={{ fontWeight: 700, fontSize: '15px', color: 'var(--accent)' }}>
                  {preview.activeSummary.regimeLabel}
                </div>
                {preview.savingsVsAlternative > 0 && (
                  <span style={{ background: 'var(--success)', color: '#fff', padding: '3px 10px', borderRadius: '999px', fontSize: '12px', fontWeight: 700 }}>
                    Saves {fmtInr(preview.savingsVsAlternative)}
                  </span>
                )}
              </div>
              <div style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: '4px' }}>
                {preview.activeSummary.ineligibilityOrNotes}
              </div>
            </div>

            {/* Company Multi-Regime Cards */}
            {!isFirm && preview.alternatives.length > 1 && (
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '8px' }}>
                {preview.alternatives.map(alt => {
                  const isSel = alt.regimeCode === preview.selectedRegimeCode
                  const isRec = alt.regimeCode === preview.recommendedRegimeCode
                  return (
                    <div
                      key={alt.regimeCode}
                      style={{
                        padding: '10px',
                        borderRadius: '6px',
                        border: isSel ? '2px solid var(--accent)' : '1px solid var(--border)',
                        background: isSel ? 'var(--card-subtle)' : 'var(--card)',
                      }}
                    >
                      <div style={{ fontSize: '11px', fontWeight: 700, color: isRec ? 'var(--success)' : 'var(--muted)' }}>
                        {alt.regimeCode.replace('CORP_', '')} {isRec ? '★ OPTIMAL' : ''}
                      </div>
                      <div style={{ fontSize: '16px', fontWeight: 700, color: 'var(--text)', marginTop: '4px' }}>
                        {fmtInr(alt.totalTaxLiability)}
                      </div>
                      <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '2px' }}>
                        {alt.isMatOrAmtApplied ? `MAT: ${fmtInr(alt.matOrAmtTotalTax)}` : `Taxable: ${fmtInr(alt.taxableIncome)}`}
                      </div>
                    </div>
                  )
                })}
              </div>
            )}

            {/* Firm Sec 40(b) Highlight Card */}
            {isFirm && (
              <div style={{ padding: '12px', borderRadius: '6px', background: 'var(--card-subtle)', border: '1px solid var(--border)', fontSize: '12px' }}>
                <div style={{ fontWeight: 700, color: 'var(--text)', marginBottom: '6px' }}>Section 40(b) Statutory Partner Ceilings (AY 2026-27)</div>
                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '4px' }}>
                  <span>Book Profit u/s 40(b):</span><strong style={{ textAlign: 'right' }}>{fmtInr(preview.firmBookProfitUs40b)}</strong>
                  <span>Max Allowable 40(b)(v):</span><strong style={{ textAlign: 'right', color: 'var(--success)' }}>{fmtInr(preview.maxAllowableRemuneration40bv)}</strong>
                  <span>Disallowed Remun. Add-back:</span><strong style={{ textAlign: 'right', color: preview.disallowedRemuneration40b > 0 ? 'var(--error)' : 'var(--text)' }}>{fmtInr(preview.disallowedRemuneration40b)}</strong>
                  <span>Excess Partner Interest (&gt;12%):</span><strong style={{ textAlign: 'right', color: preview.disallowedPartnerInterest40b > 0 ? 'var(--error)' : 'var(--text)' }}>{fmtInr(preview.disallowedPartnerInterest40b)}</strong>
                </div>
              </div>
            )}

            {/* Computation Schedule Table */}
            <div style={{ border: '1px solid var(--border)', borderRadius: '8px', overflow: 'hidden' }}>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                <tbody>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}><td style={{ padding: '7px 12px' }}>Profit Before Tax (Books)</td><td style={{ padding: '7px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtInr(preview.bookProfitBeforeTax)}</td></tr>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}><td style={{ padding: '7px 12px' }}>Add: Book Dep &amp; Disallowances (3CD/40b)</td><td style={{ padding: '7px 12px', textAlign: 'right' }}>+{fmtInr(preview.depreciationPerBooks + preview.totalDisallowancesAddedBack)}</td></tr>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}><td style={{ padding: '7px 12px' }}>Less: IT Act Sec 32 Depreciation</td><td style={{ padding: '7px 12px', textAlign: 'right', color: 'var(--success)' }}>-{fmtInr(preview.incomeTaxDepreciationSec32)}</td></tr>
                  <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--card-subtle)' }}><td style={{ padding: '7px 12px', fontWeight: 600 }}>Net PGBP Income (Schedule BP)</td><td style={{ padding: '7px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtInr(preview.netPgbpIncome)}</td></tr>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}><td style={{ padding: '7px 12px' }}>Gross Total Income (GTI)</td><td style={{ padding: '7px 12px', textAlign: 'right' }}>{fmtInr(preview.grossTotalIncome)}</td></tr>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}><td style={{ padding: '7px 12px' }}>Total Taxable Income (u/s 288A)</td><td style={{ padding: '7px 12px', textAlign: 'right', fontWeight: 700 }}>{fmtInr(preview.totalTaxableIncome)}</td></tr>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}><td style={{ padding: '7px 12px' }}>Base Tax + Special Rate Tax</td><td style={{ padding: '7px 12px', textAlign: 'right' }}>{fmtInr(preview.activeSummary.baseTax)}</td></tr>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}><td style={{ padding: '7px 12px' }}>Surcharge (Net of Marginal Relief)</td><td style={{ padding: '7px 12px', textAlign: 'right' }}>{fmtInr(preview.activeSummary.surcharge)}</td></tr>
                  <tr style={{ borderBottom: '1px solid var(--border)' }}><td style={{ padding: '7px 12px' }}>4% Health &amp; Education Cess</td><td style={{ padding: '7px 12px', textAlign: 'right' }}>{fmtInr(preview.activeSummary.healthAndEducationCess)}</td></tr>
                  {preview.activeSummary.isMatOrAmtApplied && (
                    <tr style={{ borderBottom: '1px solid var(--border)', background: 'rgba(217, 119, 6, 0.08)' }}><td style={{ padding: '7px 12px', fontWeight: 600 }}>{isFirm ? 'AMT u/s 115JC Applied' : 'MAT u/s 115JB Applied'}</td><td style={{ padding: '7px 12px', textAlign: 'right', fontWeight: 600 }}>{fmtInr(preview.activeSummary.matOrAmtTotalTax)}</td></tr>
                  )}
                  <tr style={{ borderBottom: '1px solid var(--border)', background: 'var(--card-subtle)' }}><td style={{ padding: '8px 12px', fontWeight: 700 }}>Total Tax Liability</td><td style={{ padding: '8px 12px', textAlign: 'right', fontWeight: 700, color: 'var(--warning)' }}>{fmtInr(preview.activeSummary.totalTaxLiability)}</td></tr>
                  <tr><td style={{ padding: '8px 12px', fontWeight: 700 }}>Net Payable / (Refund)</td><td style={{ padding: '8px 12px', textAlign: 'right', fontWeight: 700, color: preview.activeSummary.netTaxPayableOrRefund > 0 ? 'var(--error)' : 'var(--success)' }}>{fmtInr(preview.activeSummary.netTaxPayableOrRefund)}</td></tr>
                </tbody>
              </table>
            </div>
          </>
        )}

        {/* Official ITR-5 / ITR-6 Schema-Validated Export Panel */}
        {savedComp && (
          <JsonExportPanel
            title={isFirm ? 'Official ITR-5 (Firm/LLP) JSON Export (AY 2026-27 V1.1)' : 'Official ITR-6 (Company) JSON Export (AY 2026-27 V1.0)'}
            subtitle="Validates PartA_GEN1/2, BS, PL, CorpScheduleBP, DPM/DOA, MAT/AMT & PartB_TTI against CBDT schema."
            clientId={savedComp.clientId}
            fy={savedComp.financialYear}
            vaultTypeCode="ITR"
            generating={exporting}
            result={exportResult}
            onGenerate={handleGenerateExport}
          >
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(145px, 1fr))', gap: '8px' }}>
              <input type="text" placeholder="Signatory Name" value={exportOpts.verificationName || ''} onChange={e => setExportOpts(p => ({ ...p, verificationName: e.target.value }))} style={inputStyle} />
              <input type="text" maxLength={10} placeholder="Signer Personal PAN" value={exportOpts.verificationPan || ''} onChange={e => setExportOpts(p => ({ ...p, verificationPan: e.target.value.toUpperCase() }))} style={inputStyle} />
              <input type="text" placeholder="City (e.g. Kochi)" value={exportOpts.city || ''} onChange={e => setExportOpts(p => ({ ...p, city: e.target.value }))} style={inputStyle} />
              <input type="text" maxLength={6} placeholder="PIN Code" value={exportOpts.pinCode || ''} onChange={e => setExportOpts(p => ({ ...p, pinCode: e.target.value }))} style={inputStyle} />
              <input type="text" maxLength={11} placeholder="Bank IFSC (opt)" value={exportOpts.ifscCode || ''} onChange={e => setExportOpts(p => ({ ...p, ifscCode: e.target.value.toUpperCase() }))} style={inputStyle} />
              <input type="text" placeholder="Bank A/c No (opt)" value={exportOpts.bankAccountNo || ''} onChange={e => setExportOpts(p => ({ ...p, bankAccountNo: e.target.value }))} style={inputStyle} />
            </div>
          </JsonExportPanel>
        )}
      </div>
    </div>
  )
}
