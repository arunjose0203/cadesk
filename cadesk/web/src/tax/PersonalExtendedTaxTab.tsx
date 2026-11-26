import React, { useMemo, useState } from 'react'
import {
  taxApi,
  type BusinessPrefillSourceNote,
  type CalculatePersonalExtendedTaxRequest,
  type Client,
  type ForeignAssetInput,
  type HousePropertyInput,
  type IncomeTaxJsonExportResult,
  type PersonalExtendedTaxResultDto,
  type SalaryEmployerInput,
  type TaxComputationDto,
} from '../api'
import { JsonExportPanel } from '../components/JsonExportPanel'
import { PdfActions } from '../pdf/PdfActions'
import { buildTaxComputationPreviewPdfBlocks } from './taxComputationPdf'

const inr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n)

const inputStyle: React.CSSProperties = {
  width: '100%',
  padding: '6px 8px',
  borderRadius: '4px',
  border: '1px solid var(--border)',
  background: 'var(--card)',
  color: 'var(--text)',
  fontSize: '13px',
}

const sectionCard: React.CSSProperties = {
  marginBottom: '16px',
  background: 'var(--card-subtle)',
  padding: '14px',
  borderRadius: '6px',
  border: '1px solid var(--border)',
}

const labelStyle: React.CSSProperties = {
  display: 'block',
  fontSize: '11px',
  color: 'var(--muted)',
  marginBottom: '3px',
  fontWeight: 600,
}

function emptyRequest(formType: 'ITR-2' | 'ITR-3' = 'ITR-2'): CalculatePersonalExtendedTaxRequest {
  return {
    clientId: '',
    assessmentYear: '2026-27',
    financialYear: '2025-26',
    returnFormType: formType,
    taxpayerCategory: 'individual',
    statusType: 'I',
    optedRegime: 'auto',
    notes: '',
    grossSalary: 1_500_000,
    hraReceived: 0,
    rentPaid: 0,
    isMetro: false,
    professionalTax: 2_500,
    employers: [],
    annualRentReceived: 0,
    municipalTaxesPaid: 0,
    housingLoanInterest: 0,
    isSelfOccupied: true,
    houseProperties: [],
    stcg111ASaleValue: 0,
    stcg111ACost: 0,
    stcg111A: 120_000,
    stcgNormal: 0,
    ltcg112ASaleValue: 0,
    ltcg112ACost: 0,
    ltcg112A: 250_000,
    ltcg112SaleValue: 0,
    ltcg112Cost: 0,
    ltcg112: 0,
    vdaConsideration: 0,
    vdaCostOfAcquisition: 0,
    vdaIncome115BBH: 0,
    savingsInterest: 15_000,
    fdInterest: 25_000,
    dividendIncome: 0,
    otherSourcesIncome: 0,
    section80C: 150_000,
    section80Ccd1B: 0,
    section80D: 25_000,
    section80E: 0,
    section80G: 0,
    section80Tta: 10_000,
    deduction10AA: 0,
    deduction35AD: 0,
    businessName: 'Proprietary Enterprise',
    natureOfBusinessCode: '16013',
    isTaxAuditApplicable: false,
    propCapital: 2_500_000,
    reservesAndSurplus: 0,
    securedLoans: 0,
    unsecuredLoans: 0,
    tradePayables: 450_000,
    otherCurrentLiabilities: 0,
    fixedAssetsGrossBlock: 1_200_000,
    fixedAssetsAccumulatedDep: 0,
    fixedAssetsNetBlock: 1_200_000,
    inventories: 650_000,
    tradeReceivables: 750_000,
    cashAndBankBalances: 350_000,
    loansAndAdvancesAsset: 0,
    revenueFromOperations: formType === 'ITR-3' ? 8_500_000 : 0,
    otherBusinessIncome: 0,
    purchasesAndDirectCost: formType === 'ITR-3' ? 5_400_000 : 0,
    changesInInventories: 0,
    employeeBenefitExpense: formType === 'ITR-3' ? 720_000 : 0,
    financeCost: 0,
    depreciationPerBooks: formType === 'ITR-3' ? 140_000 : 0,
    auditFee: 0,
    otherBusinessExpenses: formType === 'ITR-3' ? 420_000 : 0,
    disallowSec36: 0,
    disallowSec37: 0,
    disallowSec40a: 0,
    disallowSec40A3: 0,
    disallowSec43B: 0,
    disallowSec43BhMsme: 0,
    incomeTaxDepreciationSec32: formType === 'ITR-3' ? 180_000 : 0,
    broughtForwardBusinessLoss: 0,
    depreciationBlocks: [],
    tdsSalary: 150_000,
    tdsOtherThanSalary: 0,
    tcsPaid: 0,
    advanceTaxPaid: 50_000,
    selfAssessmentTaxPaid: 0,
    interest234A: 0,
    interest234B: 0,
    interest234C: 0,
    lateFee234F: 0,
    foreignAssets: [],
    scheduleAl: {
      immovablePropertyValue: 0,
      immovableDescription: 'Residential Property',
      bankDeposits: 0,
      sharesAndSecurities: 0,
      insurancePolicies: 0,
      loansAndAdvancesGiven: 0,
      cashInHand: 0,
      jewelleryAndBullion: 0,
      vehiclesAndOther: 0,
      liabilitiesIncurred: 0,
    },
  }
}

type Props = {
  clients: Client[]
  firmName?: string | null
  onSaved: () => Promise<void> | void
  onError: (msg: string | null) => void
  onSuccess?: (msg: string | null) => void
}

export function PersonalExtendedTaxTab({ clients, firmName, onSaved, onError, onSuccess }: Props) {
  const [req, setReq] = useState<CalculatePersonalExtendedTaxRequest>(() => emptyRequest('ITR-2'))
  const [prefillSources, setPrefillSources] = useState<BusinessPrefillSourceNote[]>([])
  const [prefilling, setPrefilling] = useState(false)
  const [preview, setPreview] = useState<PersonalExtendedTaxResultDto | null>(null)
  const [calculating, setCalculating] = useState(false)
  const [saving, setSaving] = useState(false)
  const [savedComp, setSavedComp] = useState<TaxComputationDto | null>(null)
  const [jsonResult, setJsonResult] = useState<IncomeTaxJsonExportResult | null>(null)
  const [generatingJson, setGeneratingJson] = useState(false)
  const [verifName, setVerifName] = useState('')
  const [verifPan, setVerifPan] = useState('')
  const [city, setCity] = useState('Kochi')
  const [pinCode, setPinCode] = useState('682031')

  const selectedClient = useMemo(
    () => clients.find(c => c.id === req.clientId) ?? null,
    [clients, req.clientId],
  )

  const handleModeSwitch = (formType: 'ITR-2' | 'ITR-3') => {
    setPreview(null)
    setSavedComp(null)
    setJsonResult(null)
    setPrefillSources([])
    setReq(prev => ({
      ...emptyRequest(formType),
      clientId: prev.clientId,
      assessmentYear: prev.assessmentYear,
      financialYear: prev.financialYear,
      taxpayerCategory: prev.taxpayerCategory,
      statusType: prev.statusType,
    }))
  }

  const handlePrefillFromBooks = async () => {
    if (!req.clientId) {
      onError('Select a client before auto-filling from Trial Balance / Form 3CD.')
      return
    }
    try {
      setPrefilling(true)
      onError(null)
      const res = await taxApi.prefillPersonal(
        { clientId: req.clientId, financialYear: req.financialYear, assessmentYear: req.assessmentYear, entityType: 'firm' },
        req.returnFormType,
      )
      setReq(res.prefilledRequest)
      setPrefillSources(res.sources)
      setPreview(null)
      setSavedComp(null)
      setJsonResult(null)
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'Auto-prefill failed.')
    } finally {
      setPrefilling(false)
    }
  }

  const handlePreview = async () => {
    if (!req.clientId) {
      onError('Please select an assessee client first.')
      return
    }
    try {
      setCalculating(true)
      onError(null)
      const res = await taxApi.previewPersonal(req)
      setPreview(res)
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'Failed to compute tax preview.')
    } finally {
      setCalculating(false)
    }
  }

  const handleSave = async () => {
    if (!req.clientId) {
      onError('Please select an assessee client before saving.')
      return
    }
    try {
      setSaving(true)
      onError(null)
      const saved = await taxApi.savePersonal(req)
      setSavedComp(saved)
      if (saved.extendedPersonalComputation) setPreview(saved.extendedPersonalComputation)
      setJsonResult(null)
      onSuccess?.(`${req.returnFormType} computation saved successfully (${saved.assessmentYear}).`)
      await onSaved()
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'Failed to save ITR-2/ITR-3 computation.')
    } finally {
      setSaving(false)
    }
  }

  const handleExportJson = async () => {
    if (!savedComp) return
    try {
      setGeneratingJson(true)
      onError(null)
      const opts = {
        verificationName: verifName || selectedClient?.legalName || selectedClient?.name || 'Assessee',
        verificationPan: verifPan || selectedClient?.taxIds?.['PAN'] || undefined,
        capacity: req.statusType === 'H' ? 'K' : 'S',
        city: city || 'Kochi',
        pinCode: pinCode || '682031',
      }
      const res = req.returnFormType === 'ITR-3'
        ? await taxApi.exportItr3(savedComp.id, opts)
        : await taxApi.exportItr2(savedComp.id, opts)
      setJsonResult(res)
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'Official JSON export failed.')
    } finally {
      setGeneratingJson(false)
    }
  }

  const employers = req.employers ?? []
  const properties = req.houseProperties ?? []
  const foreignAssets = req.foreignAssets ?? []

  const addEmployer = () => {
    const next: SalaryEmployerInput[] = [
      ...employers,
      { employerName: `Employer ${employers.length + 1}`, natureOfEmployment: 'OTH', grossSalary: 600_000, tdsDeducted: 50_000 },
    ]
    const totalSal = next.reduce((s, e) => s + e.grossSalary, 0)
    const totalTds = next.reduce((s, e) => s + e.tdsDeducted, 0)
    setReq({ ...req, employers: next, grossSalary: totalSal, tdsSalary: totalTds })
  }

  const addProperty = () => {
    const next: HousePropertyInput[] = [
      ...properties,
      {
        address: `Property ${properties.length + 1}`,
        city: 'Kochi',
        stateCode: '16',
        pinCode: '682031',
        usageType: properties.length === 0 ? 'S' : 'L',
        annualRent: properties.length === 0 ? 0 : 240_000,
        municipalTaxes: 0,
        loanInterest: 120_000,
        isCoOwned: false,
        assesseeSharePercent: 100,
      },
    ]
    setReq({ ...req, houseProperties: next })
  }

  const addForeignAsset = () => {
    const next: ForeignAssetInput[] = [
      ...foreignAssets,
      {
        countryName: 'United States',
        countryCode: '2',
        institutionName: 'Charles Schwab',
        address: 'San Francisco',
        zipCode: '94105',
        accountNumber: `FA${foreignAssets.length + 101}`,
        openDate: '2023-04-01',
        peakBalanceInr: 1_000_000,
        closingBalanceInr: 850_000,
        incomeAccruedInr: 0,
      },
    ]
    setReq({ ...req, foreignAssets: next })
  }

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'minmax(520px, 1.25fr) minmax(440px, 1fr)', gap: '20px' }}>
      {/* Left Form Panel */}
      <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '18px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '10px', marginBottom: '14px' }}>
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              type="button"
              onClick={() => handleModeSwitch('ITR-2')}
              style={{
                padding: '7px 14px',
                borderRadius: '6px',
                fontWeight: 600,
                fontSize: '12px',
                cursor: 'pointer',
                border: req.returnFormType === 'ITR-2' ? '1px solid var(--accent)' : '1px solid var(--border)',
                background: req.returnFormType === 'ITR-2' ? 'var(--accent)' : 'var(--card-subtle)',
                color: req.returnFormType === 'ITR-2' ? '#fff' : 'var(--text)',
              }}
            >
              ITR-2 • Cap Gains / VDA / Multi-HP / FA
            </button>
            <button
              type="button"
              onClick={() => handleModeSwitch('ITR-3')}
              style={{
                padding: '7px 14px',
                borderRadius: '6px',
                fontWeight: 600,
                fontSize: '12px',
                cursor: 'pointer',
                border: req.returnFormType === 'ITR-3' ? '1px solid var(--accent)' : '1px solid var(--border)',
                background: req.returnFormType === 'ITR-3' ? 'var(--accent)' : 'var(--card-subtle)',
                color: req.returnFormType === 'ITR-3' ? '#fff' : 'var(--text)',
              }}
            >
              ITR-3 • Proprietor / Books u/s 44AB
            </button>
          </div>
          {req.returnFormType === 'ITR-3' && (
            <button
              type="button"
              onClick={handlePrefillFromBooks}
              disabled={prefilling}
              style={{
                padding: '7px 12px',
                borderRadius: '6px',
                border: '1px solid var(--border)',
                background: 'var(--card-subtle)',
                color: 'var(--text)',
                fontWeight: 600,
                fontSize: '12px',
                cursor: 'pointer',
              }}
            >
              {prefilling ? 'Prefilling...' : 'Auto-Fill Books & 3CD'}
            </button>
          )}
        </div>

        {prefillSources.length > 0 && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px', marginBottom: '12px' }}>
            {prefillSources.map(src => (
              <span
                key={src.module}
                title={src.summary}
                style={{
                  fontSize: '11px',
                  padding: '3px 8px',
                  borderRadius: '999px',
                  border: '1px solid var(--border)',
                  background: src.found ? 'var(--success-bg)' : 'var(--card-subtle)',
                  color: src.found ? 'var(--success)' : 'var(--muted)',
                  fontWeight: 600,
                }}
              >
                {src.module}: {src.found ? 'Synced' : 'Empty'}
              </span>
            ))}
          </div>
        )}

        {/* Assessee Header */}
        <div style={{ display: 'grid', gridTemplateColumns: '1.3fr 1fr 0.8fr', gap: '10px', marginBottom: '14px' }}>
          <div>
            <label style={labelStyle}>Assessee Client</label>
            <select
              value={req.clientId}
              onChange={e => {
                const cid = e.target.value
                const cl = clients.find(c => c.id === cid)
                const isHuf = cl?.entityType?.toLowerCase() === 'huf'
                setReq({
                  ...req,
                  clientId: cid,
                  statusType: isHuf ? 'H' : 'I',
                  taxpayerCategory: isHuf ? 'huf' : 'individual',
                  businessName: cl?.legalName || cl?.name || req.businessName,
                })
                setVerifName(cl?.legalName || cl?.name || '')
                setVerifPan(cl?.taxIds?.['PAN'] || '')
              }}
              style={inputStyle}
            >
              <option value="">-- Select Individual / HUF / Proprietor --</option>
              {clients.map(c => (
                <option key={c.id} value={c.id}>
                  {c.name} ({c.entityType}){c.taxIds?.['PAN'] ? ` — ${c.taxIds['PAN']}` : ''}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label style={labelStyle}>Assessment Year</label>
            <select
              value={req.assessmentYear}
              onChange={e => setReq({
                ...req,
                assessmentYear: e.target.value,
                financialYear: e.target.value === '2026-27' ? '2025-26' : '2024-25',
              })}
              style={inputStyle}
            >
              <option value="2026-27">AY 2026-27 (FY 2025-26)</option>
              <option value="2025-26">AY 2025-26 (FY 2024-25)</option>
            </select>
          </div>
          <div>
            <label style={labelStyle}>Regime Opted</label>
            <select
              value={req.optedRegime}
              onChange={e => setReq({ ...req, optedRegime: e.target.value as 'auto' | 'new' | 'old' })}
              style={inputStyle}
            >
              <option value="auto">Auto (Optimal)</option>
              <option value="new">New (115BAC)</option>
              <option value="old">Old Regime</option>
            </select>
          </div>
        </div>

        {/* Multi-Employer Salary */}
        <div style={sectionCard}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <span style={{ fontWeight: 700, fontSize: '13px', color: 'var(--accent)' }}>1. Salary (Schedule S — Single or Multiple Employers)</span>
            <button type="button" onClick={addEmployer} style={{ padding: '4px 10px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '11px', fontWeight: 600, cursor: 'pointer' }}>
              + Add Employer
            </button>
          </div>
          {employers.length === 0 ? (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '10px' }}>
              <div>
                <label style={labelStyle}>Aggregate Gross Salary (Rs.)</label>
                <input type="number" value={req.grossSalary} onChange={e => setReq({ ...req, grossSalary: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Professional Tax u/s 16(iii)</label>
                <input type="number" value={req.professionalTax} onChange={e => setReq({ ...req, professionalTax: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>TDS Deducted on Salary</label>
                <input type="number" value={req.tdsSalary} onChange={e => setReq({ ...req, tdsSalary: Number(e.target.value) })} style={inputStyle} />
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              {employers.map((emp, i) => (
                <div key={i} style={{ display: 'grid', gridTemplateColumns: '1.5fr 1fr 1fr auto', gap: '8px', alignItems: 'end' }}>
                  <div>
                    <label style={labelStyle}>Employer Name</label>
                    <input
                      value={emp.employerName}
                      onChange={e => {
                        const next = employers.map((x, idx) => idx === i ? { ...x, employerName: e.target.value } : x)
                        setReq({ ...req, employers: next })
                      }}
                      style={inputStyle}
                    />
                  </div>
                  <div>
                    <label style={labelStyle}>Gross Salary</label>
                    <input
                      type="number"
                      value={emp.grossSalary}
                      onChange={e => {
                        const next = employers.map((x, idx) => idx === i ? { ...x, grossSalary: Number(e.target.value) } : x)
                        setReq({ ...req, employers: next, grossSalary: next.reduce((s, r) => s + r.grossSalary, 0) })
                      }}
                      style={inputStyle}
                    />
                  </div>
                  <div>
                    <label style={labelStyle}>TDS u/s 192</label>
                    <input
                      type="number"
                      value={emp.tdsDeducted}
                      onChange={e => {
                        const next = employers.map((x, idx) => idx === i ? { ...x, tdsDeducted: Number(e.target.value) } : x)
                        setReq({ ...req, employers: next, tdsSalary: next.reduce((s, r) => s + r.tdsDeducted, 0) })
                      }}
                      style={inputStyle}
                    />
                  </div>
                  <button
                    type="button"
                    onClick={() => {
                      const next = employers.filter((_, idx) => idx !== i)
                      setReq({ ...req, employers: next })
                    }}
                    style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--error)', cursor: 'pointer', fontSize: '11px' }}
                  >
                    Remove
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* Multi House Property */}
        <div style={sectionCard}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <span style={{ fontWeight: 700, fontSize: '13px', color: 'var(--accent)' }}>2. House Properties (Schedule HP — Self-Occupied &amp; Let-Out)</span>
            <button type="button" onClick={addProperty} style={{ padding: '4px 10px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '11px', fontWeight: 600, cursor: 'pointer' }}>
              + Add Property
            </button>
          </div>
          {properties.length === 0 ? (
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '10px' }}>
              <div>
                <label style={labelStyle}>Annual Rent Received</label>
                <input type="number" value={req.annualRentReceived} onChange={e => setReq({ ...req, annualRentReceived: Number(e.target.value), isSelfOccupied: Number(e.target.value) === 0 })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Municipal Taxes</label>
                <input type="number" value={req.municipalTaxesPaid} onChange={e => setReq({ ...req, municipalTaxesPaid: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Loan Interest u/s 24(b)</label>
                <input type="number" value={req.housingLoanInterest} onChange={e => setReq({ ...req, housingLoanInterest: Number(e.target.value) })} style={inputStyle} />
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
              {properties.map((hp, i) => (
                <div key={i} style={{ display: 'grid', gridTemplateColumns: '1.3fr 0.7fr 0.9fr 0.9fr auto', gap: '8px', alignItems: 'end' }}>
                  <div>
                    <label style={labelStyle}>Property Address</label>
                    <input value={hp.address} onChange={e => setReq({ ...req, houseProperties: properties.map((x, idx) => idx === i ? { ...x, address: e.target.value } : x) })} style={inputStyle} />
                  </div>
                  <div>
                    <label style={labelStyle}>Usage</label>
                    <select value={hp.usageType} onChange={e => setReq({ ...req, houseProperties: properties.map((x, idx) => idx === i ? { ...x, usageType: e.target.value as 'S' | 'L' | 'D' } : x) })} style={inputStyle}>
                      <option value="S">Self (S)</option>
                      <option value="L">Let-Out (L)</option>
                      <option value="D">Deemed (D)</option>
                    </select>
                  </div>
                  <div>
                    <label style={labelStyle}>Annual Rent</label>
                    <input type="number" value={hp.annualRent} onChange={e => setReq({ ...req, houseProperties: properties.map((x, idx) => idx === i ? { ...x, annualRent: Number(e.target.value) } : x) })} style={inputStyle} />
                  </div>
                  <div>
                    <label style={labelStyle}>24(b) Interest</label>
                    <input type="number" value={hp.loanInterest} onChange={e => setReq({ ...req, houseProperties: properties.map((x, idx) => idx === i ? { ...x, loanInterest: Number(e.target.value) } : x) })} style={inputStyle} />
                  </div>
                  <button type="button" onClick={() => setReq({ ...req, houseProperties: properties.filter((_, idx) => idx !== i) })} style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--error)', cursor: 'pointer', fontSize: '11px' }}>
                    Remove
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* ITR-3 Business Books Section */}
        {req.returnFormType === 'ITR-3' && (
          <div style={sectionCard}>
            <div style={{ fontWeight: 700, fontSize: '13px', color: 'var(--accent)', marginBottom: '8px' }}>
              3. Proprietor / Professional Books u/s 44AA &amp; 44AB (PARTA_BS / PL / BP)
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: '1.3fr 1fr 0.7fr', gap: '10px', marginBottom: '8px', alignItems: 'end' }}>
              <div>
                <label style={labelStyle}>Business / Trade Name</label>
                <input value={req.businessName} onChange={e => setReq({ ...req, businessName: e.target.value })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>NIC Code (CBDT Nature)</label>
                <input value={req.natureOfBusinessCode} onChange={e => setReq({ ...req, natureOfBusinessCode: e.target.value })} style={inputStyle} />
              </div>
              <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text)', cursor: 'pointer', paddingBottom: '6px' }}>
                <input type="checkbox" checked={req.isTaxAuditApplicable} onChange={e => setReq({ ...req, isTaxAuditApplicable: e.target.checked })} />
                44AB Audit
              </label>
            </div>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
              <div>
                <label style={labelStyle}>Turnover / Receipts</label>
                <input type="number" value={req.revenueFromOperations} onChange={e => setReq({ ...req, revenueFromOperations: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Purchases &amp; Direct Cost</label>
                <input type="number" value={req.purchasesAndDirectCost} onChange={e => setReq({ ...req, purchasesAndDirectCost: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Employee Benefit Expense</label>
                <input type="number" value={req.employeeBenefitExpense} onChange={e => setReq({ ...req, employeeBenefitExpense: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Other Operating Expenses</label>
                <input type="number" value={req.otherBusinessExpenses} onChange={e => setReq({ ...req, otherBusinessExpenses: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Depreciation per Books</label>
                <input type="number" value={req.depreciationPerBooks} onChange={e => setReq({ ...req, depreciationPerBooks: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>IT Depreciation u/s 32</label>
                <input type="number" value={req.incomeTaxDepreciationSec32} onChange={e => setReq({ ...req, incomeTaxDepreciationSec32: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>43B(h) MSME Disallowance</label>
                <input type="number" value={req.disallowSec43BhMsme} onChange={e => setReq({ ...req, disallowSec43BhMsme: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Sec 40(a)/40A(3) Disallowances</label>
                <input type="number" value={req.disallowSec40a} onChange={e => setReq({ ...req, disallowSec40a: Number(e.target.value) })} style={inputStyle} />
              </div>
              <div>
                <label style={labelStyle}>Proprietor Capital (BS)</label>
                <input type="number" value={req.propCapital} onChange={e => setReq({ ...req, propCapital: Number(e.target.value) })} style={inputStyle} />
              </div>
            </div>
          </div>
        )}

        {/* Capital Gains & VDA */}
        <div style={sectionCard}>
          <div style={{ fontWeight: 700, fontSize: '13px', color: 'var(--accent)', marginBottom: '8px' }}>
            {req.returnFormType === 'ITR-3' ? '4' : '3'}. Capital Gains (111A / 112A / 112) &amp; Crypto VDA (Sec 115BBH @ 30%)
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
            <div>
              <label style={labelStyle}>STCG 111A (Equity @ 20%)</label>
              <input type="number" value={req.stcg111A} onChange={e => setReq({ ...req, stcg111A: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>STCG Normal (Slab Rate)</label>
              <input type="number" value={req.stcgNormal} onChange={e => setReq({ ...req, stcgNormal: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>LTCG 112A (Equity @ 12.5%)</label>
              <input type="number" value={req.ltcg112A} onChange={e => setReq({ ...req, ltcg112A: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>LTCG 112 (Property/Other 12.5%)</label>
              <input type="number" value={req.ltcg112} onChange={e => setReq({ ...req, ltcg112: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>VDA Sale Value (Sec 115BBH)</label>
              <input
                type="number"
                value={req.vdaConsideration}
                onChange={e => {
                  const sale = Number(e.target.value)
                  setReq({ ...req, vdaConsideration: sale, vdaIncome115BBH: Math.max(0, sale - req.vdaCostOfAcquisition) })
                }}
                style={inputStyle}
              />
            </div>
            <div>
              <label style={labelStyle}>VDA Net Taxable Gain (30%)</label>
              <input type="number" value={req.vdaIncome115BBH} onChange={e => setReq({ ...req, vdaIncome115BBH: Number(e.target.value) })} style={inputStyle} />
            </div>
          </div>
        </div>

        {/* Other Sources, VI-A & Schedule FA */}
        <div style={sectionCard}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
            <span style={{ fontWeight: 700, fontSize: '13px', color: 'var(--accent)' }}>
              {req.returnFormType === 'ITR-3' ? '5' : '4'}. Other Sources, Chapter VI-A, Foreign Assets (Schedule FA) &amp; Taxes
            </span>
            <button type="button" onClick={addForeignAsset} style={{ padding: '4px 10px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '11px', fontWeight: 600, cursor: 'pointer' }}>
              + Add Foreign Asset (FA)
            </button>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '10px', marginBottom: '10px' }}>
            <div>
              <label style={labelStyle}>Savings / FD Interest</label>
              <input type="number" value={req.savingsInterest + req.fdInterest} onChange={e => setReq({ ...req, savingsInterest: Number(e.target.value), fdInterest: 0 })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>80C Deduction</label>
              <input type="number" value={req.section80C} onChange={e => setReq({ ...req, section80C: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>80D Health Ins.</label>
              <input type="number" value={req.section80D} onChange={e => setReq({ ...req, section80D: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Advance Tax + TDS Other</label>
              <input type="number" value={req.advanceTaxPaid} onChange={e => setReq({ ...req, advanceTaxPaid: Number(e.target.value) })} style={inputStyle} />
            </div>
          </div>
          {foreignAssets.length > 0 && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
              {foreignAssets.map((fa, i) => (
                <div key={i} style={{ display: 'grid', gridTemplateColumns: '1fr 1.2fr 1fr 1fr auto', gap: '6px', alignItems: 'end' }}>
                  <div>
                    <label style={labelStyle}>Country</label>
                    <input value={fa.countryName} onChange={e => setReq({ ...req, foreignAssets: foreignAssets.map((x, idx) => idx === i ? { ...x, countryName: e.target.value } : x) })} style={inputStyle} />
                  </div>
                  <div>
                    <label style={labelStyle}>Institution</label>
                    <input value={fa.institutionName} onChange={e => setReq({ ...req, foreignAssets: foreignAssets.map((x, idx) => idx === i ? { ...x, institutionName: e.target.value } : x) })} style={inputStyle} />
                  </div>
                  <div>
                    <label style={labelStyle}>Peak Balance (INR)</label>
                    <input type="number" value={fa.peakBalanceInr} onChange={e => setReq({ ...req, foreignAssets: foreignAssets.map((x, idx) => idx === i ? { ...x, peakBalanceInr: Number(e.target.value) } : x) })} style={inputStyle} />
                  </div>
                  <div>
                    <label style={labelStyle}>Closing (INR)</label>
                    <input type="number" value={fa.closingBalanceInr} onChange={e => setReq({ ...req, foreignAssets: foreignAssets.map((x, idx) => idx === i ? { ...x, closingBalanceInr: Number(e.target.value) } : x) })} style={inputStyle} />
                  </div>
                  <button type="button" onClick={() => setReq({ ...req, foreignAssets: foreignAssets.filter((_, idx) => idx !== i) })} style={{ padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--error)', fontSize: '11px', cursor: 'pointer' }}>
                    Remove
                  </button>
                </div>
              ))}
            </div>
          )}
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            type="button"
            onClick={handlePreview}
            disabled={calculating}
            style={{ flex: 1, padding: '10px 14px', background: 'var(--accent)', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
          >
            {calculating ? `Evaluating ${req.returnFormType}...` : `Compute ${req.returnFormType} & Compare Regimes`}
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={saving}
            style={{ padding: '10px 16px', background: 'var(--success)', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
          >
            {saving ? 'Saving...' : `Save ${req.returnFormType}`}
          </button>
        </div>
      </div>

      {/* Right Result & Export Panel */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
        {!preview ? (
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '36px 24px', textAlign: 'center', color: 'var(--muted)' }}>
            <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text)', marginBottom: '6px' }}>
              {req.returnFormType} Live Assessment Desk
            </div>
            <p style={{ fontSize: '13px', margin: 0 }}>
              Select a client and click <strong>Compute {req.returnFormType} &amp; Compare Regimes</strong> to evaluate Old vs New Sec 115BAC, VDA 30%, Sec 44AB Proprietor Books, download the Official Computation Sheet PDF, or generate AY 2026-27 e-Filing JSON.
            </p>
          </div>
        ) : (
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '18px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '8px', marginBottom: '12px' }}>
              <div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>{preview.returnFormType} • AY {preview.assessmentYear}</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text)' }}>{preview.recommendationReason}</div>
              </div>
              <PdfActions
                title={`Tax Computation - ${selectedClient?.name ?? 'Assessee'} - ${preview.returnFormType} AY ${preview.assessmentYear}`}
                options={{
                  author: firmName || 'CaDesk Practice Suite',
                  header: `${selectedClient?.name ?? 'Assessee'} • ${preview.returnFormType} • AY ${preview.assessmentYear}`,
                  footer: `${firmName || 'CaDesk'} • Statement of Total Income`,
                }}
                source={() =>
                  buildTaxComputationPreviewPdfBlocks({
                    clientName: selectedClient?.name ?? 'Assessee',
                    clientPan: selectedClient?.taxIds?.['PAN'] ?? null,
                    assessmentYear: preview.assessmentYear,
                    financialYear: preview.financialYear,
                    taxpayerCategory: req.taxpayerCategory,
                    optedRegime: preview.selectedRegime,
                    firmName,
                    extendedPersonal: preview,
                  })
                }
                clientId={req.clientId || null}
                fy={preview.financialYear}
                canSave={Boolean(req.clientId)}
              />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px', marginBottom: '14px' }}>
              {[preview.oldRegime, preview.newRegime].map(reg => {
                const isWinner = reg.regime === preview.recommendedRegime
                return (
                  <div
                    key={reg.regime}
                    style={{
                      padding: '12px',
                      borderRadius: '8px',
                      border: isWinner ? '2px solid var(--success)' : '1px solid var(--border)',
                      background: isWinner ? 'var(--success-bg)' : 'var(--card-subtle)',
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '6px' }}>
                      <span style={{ fontSize: '11px', fontWeight: 700, textTransform: 'uppercase', color: 'var(--text)' }}>
                        {reg.regime === 'new' ? 'New Regime (115BAC)' : 'Old Regime'}
                      </span>
                      {isWinner && <span style={{ fontSize: '10px', fontWeight: 700, color: 'var(--success)' }}>OPTIMAL</span>}
                    </div>
                    <div style={{ fontSize: '20px', fontWeight: 800, color: 'var(--text)' }}>{inr(reg.totalTaxLiability)}</div>
                    <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '4px' }}>
                      Taxable: {inr(reg.taxableIncome)} • Special: {inr(reg.specialRateTax)}
                    </div>
                    <div style={{ fontSize: '11px', color: reg.netPayableOrRefund < 0 ? 'var(--success)' : 'var(--warning)', fontWeight: 600, marginTop: '2px' }}>
                      {reg.netPayableOrRefund < 0 ? `Refund ${inr(-reg.netPayableOrRefund)}` : `Net Payable ${inr(reg.netPayableOrRefund)}`}
                    </div>
                  </div>
                )
              })}
            </div>

            <div style={{ fontSize: '12px', borderTop: '1px solid var(--border)', paddingTop: '10px', display: 'grid', gridTemplateColumns: '1fr auto', gap: '4px 12px', color: 'var(--text-secondary)' }}>
              <span>Net Salary / Multi-Employer</span><strong>{inr(preview.activeSummary.netSalary)}</strong>
              <span>House Property Income</span><strong>{inr(preview.activeSummary.housePropertyIncome)}</strong>
              {preview.returnFormType === 'ITR-3' && (
                <>
                  <span>Proprietor Net PGBP (Sec 44AB / 32)</span>
                  <strong>{inr(preview.activeSummary.businessIncomeNetPgbp)}</strong>
                </>
              )}
              <span>Capital Gains + VDA (115BBH)</span><strong>{inr(preview.activeSummary.totalCapitalGainsAndVda)}</strong>
              <span>Chapter VI-A Deductions</span><strong>{inr(preview.activeSummary.chapterViaDeductions)}</strong>
              <span>Gross Total / Total Taxable Income</span>
              <strong>{inr(preview.activeSummary.grossTotalIncome)} / {inr(preview.activeSummary.taxableIncome)}</strong>
            </div>
          </div>
        )}

        {savedComp && (
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <JsonExportPanel
              title={`Official ${req.returnFormType} JSON Export (AY ${savedComp.assessmentYear})`}
              subtitle={req.returnFormType === 'ITR-3' ? 'ITR-3 V1.1 Schema (PARTA_BS, PARTA_PL, ITR3ScheduleBP, DPM/DOA/DEP)' : 'ITR-2 V1.2 Schema (ScheduleS, HP, CGFor23, 112A, VDA, FA, AL)'}
              clientId={savedComp.clientId}
              fy={savedComp.financialYear}
              vaultTypeCode="ITR"
              generating={generatingJson}
              result={jsonResult}
              onGenerate={handleExportJson}
            >
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: '8px' }}>
                <input value={verifName} onChange={e => setVerifName(e.target.value)} placeholder="Verification Name" style={inputStyle} />
                <input value={verifPan} onChange={e => setVerifPan(e.target.value.toUpperCase())} maxLength={10} placeholder="Signer PAN" style={inputStyle} />
                <input value={city} onChange={e => setCity(e.target.value)} placeholder="City" style={inputStyle} />
                <input value={pinCode} onChange={e => setPinCode(e.target.value)} maxLength={6} placeholder="PIN" style={inputStyle} />
              </div>
            </JsonExportPanel>
          </div>
        )}
      </div>
    </div>
  )
}
