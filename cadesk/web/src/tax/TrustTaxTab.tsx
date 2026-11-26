import React, { useMemo, useState } from 'react'
import {
  taxApi,
  type CalculateTrustTaxRequest,
  type Client,
  type IncomeTaxJsonExportResult,
  type TaxComputationDto,
  type TrustTaxResultDto,
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
  marginBottom: '14px',
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

function defaultTrustRequest(): CalculateTrustTaxRequest {
  return {
    clientId: '',
    assessmentYear: '2026-27',
    financialYear: '2025-26',
    returnFurnishedSection: '139-4A',
    sectionExemptionClaimed: '11',
    statusOrCompanyType: '5',
    registration12AbNo: 'AAABT1234AE20214',
    registration12AbDate: '2021-06-15',
    incorporationDate: '2018-04-01',
    notes: '',
    corpusDonations80G: 500_000,
    corpusDonationsOther: 0,
    grantsReceivedGovt: 1_200_000,
    grantsReceivedCorporate: 400_000,
    otherSpecificAndGeneralDonations: 2_000_000,
    foreignContributions: 0,
    anonymousDonationsReceived: 350_000,
    receiptsFromMainObjects: 650_000,
    receiptsIncidental: 0,
    rentIncome: 0,
    dividendIncome: 0,
    interestIncome: 140_000,
    revenueApplicationCharitable: 3_400_000,
    capitalApplicationCharitable: 450_000,
    activityCategory: 'Educational',
    disallowedApplication40aOr40A3: 0,
    form10AccumulationUs11_2: 0,
    form10Furnished: false,
    specifiedViolationIncome115BBI: 0,
    corpusFundClosing: 2_500_000,
    accumulatedFundClosing: 1_500_000,
    securedLoans: 0,
    unsecuredLoans: 0,
    fixedAssetsGrossBlock: 1_800_000,
    fixedAssetsDepreciation: 0,
    fixedAssetsNetBlock: 1_800_000,
    investmentsSec11_5: 1_500_000,
    sundryDebtors: 0,
    cashAndBankBalances: 700_000,
    loansAndAdvances: 0,
    sundryCreditors: 0,
    provisions: 0,
    tdsCredit: 15_000,
    tcsCredit: 0,
    advanceTaxPaid: 25_000,
    selfAssessmentTaxPaid: 0,
    interest234A: 0,
    interest234B: 0,
    interest234C: 0,
    lateFee234F: 0,
  }
}

type Props = {
  clients: Client[]
  onSaved: () => Promise<void> | void
  onError: (msg: string | null) => void
  onSuccess?: (msg: string | null) => void
}

export function TrustTaxTab({ clients, onSaved, onError, onSuccess }: Props) {
  const [req, setReq] = useState<CalculateTrustTaxRequest>(() => defaultTrustRequest())
  const [preview, setPreview] = useState<TrustTaxResultDto | null>(null)
  const [calculating, setCalculating] = useState(false)
  const [saving, setSaving] = useState(false)
  const [savedComp, setSavedComp] = useState<TaxComputationDto | null>(null)
  const [jsonResult, setJsonResult] = useState<IncomeTaxJsonExportResult | null>(null)
  const [generatingJson, setGeneratingJson] = useState(false)
  const [verifName, setVerifName] = useState('Managing Trustee')
  const [verifPan, setVerifPan] = useState('')
  const [city, setCity] = useState('Kochi')
  const [pinCode, setPinCode] = useState('682031')

  const selectedClient = useMemo(
    () => clients.find(c => c.id === req.clientId) ?? null,
    [clients, req.clientId],
  )

  const handlePreview = async () => {
    if (!req.clientId) {
      onError('Select a Trust / Institution / Sec 8 client first.')
      return
    }
    try {
      setCalculating(true)
      onError(null)
      const res = await taxApi.previewTrust(req)
      setPreview(res)
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'ITR-7 Trust evaluation failed.')
    } finally {
      setCalculating(false)
    }
  }

  const handleSave = async () => {
    if (!req.clientId) {
      onError('Select a Trust / Institution / Sec 8 client before saving.')
      return
    }
    try {
      setSaving(true)
      onError(null)
      const saved = await taxApi.saveTrust(req)
      setSavedComp(saved)
      if (saved.trustComputation) setPreview(saved.trustComputation)
      setJsonResult(null)
      onSuccess?.(`ITR-7 Trust computation saved successfully (${saved.assessmentYear}).`)
      await onSaved()
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'Failed to save ITR-7 computation.')
    } finally {
      setSaving(false)
    }
  }

  const handleExportItr7 = async () => {
    if (!savedComp) return
    try {
      setGeneratingJson(true)
      onError(null)
      const res = await taxApi.exportItr7(savedComp.id, {
        verificationName: verifName || 'Managing Trustee',
        verificationPan: verifPan || undefined,
        capacity: req.statusOrCompanyType === '6' ? 'MD' : 'DR',
        city: city || 'Kochi',
        pinCode: pinCode || '682031',
      })
      setJsonResult(res)
    } catch (err: unknown) {
      onError(err instanceof Error ? err.message : 'ITR-7 JSON generation failed.')
    } finally {
      setGeneratingJson(false)
    }
  }

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'minmax(500px, 1.2fr) minmax(440px, 1fr)', gap: '20px' }}>
      <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '18px' }}>
        <div style={{ marginBottom: '12px' }}>
          <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: 'var(--text)' }}>
            ITR-7 • Charitable Trusts, Sec 8 Companies &amp; Institutions (u/s 11, 12AB, 115BBC)
          </h3>
          <p style={{ margin: '3px 0 0', fontSize: '12px', color: 'var(--muted)' }}>
            Computes 15% statutory accumulation u/s 11(1)(a), 85% charitable application, Form 10 u/s 11(2), Anonymous Donations u/s 115BBC (@ 30%), and validates against AY 2026-27 ITR-7 V0.1 Schema.
          </p>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1.3fr 1fr 1fr', gap: '10px', marginBottom: '14px' }}>
          <div>
            <label style={labelStyle}>Trust / Institution Client</label>
            <select
              value={req.clientId}
              onChange={e => setReq({ ...req, clientId: e.target.value })}
              style={inputStyle}
            >
              <option value="">-- Select Trust / Sec 8 / Institution --</option>
              {clients.map(c => (
                <option key={c.id} value={c.id}>{c.name} ({c.entityType})</option>
              ))}
            </select>
          </div>
          <div>
            <label style={labelStyle}>Return Furnished u/s</label>
            <select
              value={req.returnFurnishedSection}
              onChange={e => setReq({ ...req, returnFurnishedSection: e.target.value as CalculateTrustTaxRequest['returnFurnishedSection'] })}
              style={inputStyle}
            >
              <option value="139-4A">Sec 139(4A) Charitable Trust</option>
              <option value="139-4C">Sec 139(4C) Institution / Univ</option>
              <option value="139-4D">Sec 139(4D) Scientific Research</option>
              <option value="139-4B">Sec 139(4B) Political Party</option>
            </select>
          </div>
          <div>
            <label style={labelStyle}>12AB Registration No.</label>
            <input value={req.registration12AbNo || ''} onChange={e => setReq({ ...req, registration12AbNo: e.target.value })} style={inputStyle} />
          </div>
        </div>

        <div style={sectionCard}>
          <div style={{ fontWeight: 700, fontSize: '13px', color: 'var(--accent)', marginBottom: '8px' }}>
            1. Voluntary Contributions (Schedule VC) &amp; Trust Receipts (Schedule AI)
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
            <div>
              <label style={labelStyle}>Corpus Donations 80G u/s 11(1)(d)</label>
              <input type="number" value={req.corpusDonations80G} onChange={e => setReq({ ...req, corpusDonations80G: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Grants (Govt + Corporate)</label>
              <input type="number" value={req.grantsReceivedGovt + req.grantsReceivedCorporate} onChange={e => setReq({ ...req, grantsReceivedGovt: Number(e.target.value), grantsReceivedCorporate: 0 })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Other Donations (Non-Corpus)</label>
              <input type="number" value={req.otherSpecificAndGeneralDonations} onChange={e => setReq({ ...req, otherSpecificAndGeneralDonations: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Anonymous Donations (Sec 115BBC)</label>
              <input type="number" value={req.anonymousDonationsReceived} onChange={e => setReq({ ...req, anonymousDonationsReceived: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Receipts from Objects (Sch AI)</label>
              <input type="number" value={req.receiptsFromMainObjects} onChange={e => setReq({ ...req, receiptsFromMainObjects: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Interest / Investment Income</label>
              <input type="number" value={req.interestIncome} onChange={e => setReq({ ...req, interestIncome: Number(e.target.value) })} style={inputStyle} />
            </div>
          </div>
        </div>

        <div style={sectionCard}>
          <div style={{ fontWeight: 700, fontSize: '13px', color: 'var(--accent)', marginBottom: '8px' }}>
            2. Application of Income (Schedule A), Form 10 &amp; Balance Sheet / TDS
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '10px' }}>
            <div>
              <label style={labelStyle}>Revenue Charitable Application</label>
              <input type="number" value={req.revenueApplicationCharitable} onChange={e => setReq({ ...req, revenueApplicationCharitable: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Capital Charitable Application</label>
              <input type="number" value={req.capitalApplicationCharitable} onChange={e => setReq({ ...req, capitalApplicationCharitable: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Form 10 Accumulation u/s 11(2)</label>
              <input type="number" value={req.form10AccumulationUs11_2} onChange={e => setReq({ ...req, form10AccumulationUs11_2: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Sec 11(5) Investments (BS)</label>
              <input type="number" value={req.investmentsSec11_5} onChange={e => setReq({ ...req, investmentsSec11_5: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>TDS Credit</label>
              <input type="number" value={req.tdsCredit} onChange={e => setReq({ ...req, tdsCredit: Number(e.target.value) })} style={inputStyle} />
            </div>
            <div>
              <label style={labelStyle}>Advance Tax Paid</label>
              <input type="number" value={req.advanceTaxPaid} onChange={e => setReq({ ...req, advanceTaxPaid: Number(e.target.value) })} style={inputStyle} />
            </div>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '10px' }}>
          <button
            type="button"
            onClick={handlePreview}
            disabled={calculating}
            style={{ flex: 1, padding: '10px 14px', background: 'var(--accent)', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
          >
            {calculating ? 'Evaluating ITR-7...' : 'Calculate ITR-7 Trust Exemption'}
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={saving}
            style={{ padding: '10px 16px', background: 'var(--success)', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
          >
            {saving ? 'Saving...' : 'Save ITR-7'}
          </button>
        </div>
      </div>

      {/* Right Summary & ITR-7 Export */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
        {!preview ? (
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '36px 24px', textAlign: 'center', color: 'var(--muted)' }}>
            <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text)', marginBottom: '6px' }}>ITR-7 Charitable Trust Desk</div>
            <p style={{ margin: 0, fontSize: '13px' }}>
              Click <strong>Calculate ITR-7 Trust Exemption</strong> to check 85% application compliance, Sec 115BBC anonymous donation tax, download the Computation PDF, or validate official ITR-7 JSON.
            </p>
          </div>
        ) : (
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '18px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '8px', marginBottom: '12px' }}>
              <div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>ITR-7 (u/s {preview.returnFurnishedSection}) • AY {preview.assessmentYear}</div>
                <div style={{ fontSize: '14px', fontWeight: 700, color: 'var(--text)' }}>{preview.summaryNote}</div>
              </div>
              <PdfActions
                title={`ITR-7 Computation - ${selectedClient?.name ?? 'Trust'} - AY ${preview.assessmentYear}`}
                options={{
                  author: 'CaDesk Practice Suite',
                  header: `${selectedClient?.name ?? 'Trust'} • ITR-7 AY ${preview.assessmentYear}`,
                  footer: 'CaDesk Charitable Trust & Sec 11 Computation',
                }}
                source={() =>
                  buildTaxComputationPreviewPdfBlocks({
                    clientName: selectedClient?.name ?? 'Trust',
                    clientPan: selectedClient?.taxIds?.['PAN'] ?? null,
                    assessmentYear: preview.assessmentYear,
                    financialYear: preview.financialYear,
                    taxpayerCategory: 'trust',
                    optedRegime: 'old',
                    trust: preview,
                  })
                }
                clientId={req.clientId || null}
                fy={preview.financialYear}
                canSave={Boolean(req.clientId)}
              />
            </div>

            <div style={{ fontSize: '12px', borderTop: '1px solid var(--border)', paddingTop: '10px', display: 'grid', gridTemplateColumns: '1fr auto', gap: '5px 12px', color: 'var(--text-secondary)' }}>
              <span>Corpus Donations Exempt u/s 11(1)(d)</span><strong>{inr(preview.corpusExempt11_1d)}</strong>
              <span>Total Eligible Receipts (VC + Schedule AI)</span><strong>{inr(preview.totalEligibleIncome)}</strong>
              <span>15% Statutory Accumulation u/s 11(1)(a)</span><strong>{inr(preview.statutoryAccumulation15Pct)}</strong>
              <span>Required 85% Application / Net Allowed</span><strong>{inr(preview.required85PctApplication)} / {inr(preview.netAllowedApplication)}</strong>
              <span>Excess Anonymous Donations (115BBC @ 30%)</span><strong>{inr(preview.anonymousDonationsTaxable115BBC)}</strong>
              <span>Total Taxable Income / Tax Liability</span><strong>{inr(preview.totalTaxableIncome)} / {inr(preview.totalTaxLiability)}</strong>
              <span>Net Payable / (Refundable)</span>
              <strong style={{ color: preview.netPayableOrRefund <= 0 ? 'var(--success)' : 'var(--warning)' }}>
                {preview.netPayableOrRefund < 0 ? `Refund ${inr(-preview.netPayableOrRefund)}` : inr(preview.netPayableOrRefund)}
              </strong>
            </div>
          </div>
        )}

        {savedComp && (
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <JsonExportPanel
              title={`Official ITR-7 (Trust / Institution) JSON Export (AY ${savedComp.assessmentYear})`}
              subtitle="CBDT ITR-7 V0.1 Schema (PartA_GEN1/2, PARTA_BS, ScheduleVC, ScheduleAI, ScheduleA, PartB_TI/TTI)"
              clientId={savedComp.clientId}
              fy={savedComp.financialYear}
              vaultTypeCode="ITR"
              generating={generatingJson}
              result={jsonResult}
              onGenerate={handleExportItr7}
            >
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr 1fr', gap: '8px' }}>
                <input value={verifName} onChange={e => setVerifName(e.target.value)} placeholder="Trustee / MD Name" style={inputStyle} />
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
