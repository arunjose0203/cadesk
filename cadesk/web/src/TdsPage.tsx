import React, { useState, useEffect, useCallback } from 'react'
import {
  api,
  tdsApi,
  type Client,
  type ClientPage,
  type TdsSummaryDto,
  type TdsDeductionDto,
  type TdsChallanDto,
  type TdsLowerDeductionCertificateDto,
  type TdsInterestResultDto,
  type RecordTdsDeductionRequest,
  type CreateTdsChallanRequest,
  type CreateLdcRequest
} from './api'
import { TdsRpuExportTab } from './tds/TdsRpuExportTab'
import { TdsForm16ImportTab } from './tds/TdsForm16ImportTab'
import {
  COUNTRY_27Q_CODES,
  REMITTANCE_27Q_CODES,
  TDS_FINANCIAL_YEARS,
  TDS_SECTION_OPTIONS,
} from './tds/tdsHelpers'

export const TdsPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'deductions' | 'challans' | 'ldc' | 'calculator' | 'returns' | 'import'>('deductions')
  const [summary, setSummary] = useState<TdsSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [deductions, setDeductions] = useState<TdsDeductionDto[]>([])
  const [challans, setChallans] = useState<TdsChallanDto[]>([])
  const [ldcs, setLdcs] = useState<TdsLowerDeductionCertificateDto[]>([])
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Filters
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedFy, setSelectedFy] = useState<string>('2026-27')
  const [selectedQuarter, setSelectedQuarter] = useState<string>('Q1')

  // Modals
  const [showDeductionModal, setShowDeductionModal] = useState<boolean>(false)
  const [showChallanModal, setShowChallanModal] = useState<boolean>(false)
  const [showLdcModal, setShowLdcModal] = useState<boolean>(false)
  const [showMapModal, setShowMapModal] = useState<boolean>(false)

  // Deduction Form State
  const [deductionForm, setDeductionForm] = useState<RecordTdsDeductionRequest>({
    clientId: '',
    financialYear: '2026-27',
    quarter: 'Q1',
    formType: '26Q',
    section: '194C_company',
    deducteePan: '',
    deducteeName: '',
    forceHigherRate206Ab: false,
    certificateId: undefined,
    paymentDate: new Date().toISOString().split('T')[0],
    deductionDate: new Date().toISOString().split('T')[0],
    amountPaid: 100000,
    customRate: null,
    deducteeCode: '',
    remittanceCode: '21',
    countryCode: '267',
    taxRateSource: 'A',
    ackNumber: '',
    optedOutNewRegime: false,
  })

  // Challan Form State
  const [challanForm, setChallanForm] = useState<CreateTdsChallanRequest>({
    clientId: '',
    financialYear: '2026-27',
    quarter: 'Q1',
    bsrCode: '0210043',
    challanDate: new Date().toISOString().split('T')[0],
    challanSerialNumber: '00124',
    sectionCode: '194C',
    minorHead: '200',
    tdsAmount: 2000,
    surcharge: 0,
    cess: 0,
    interest: 0,
    lateFee234E: 0,
    notes: 'TDS deposit ITNS 281',
    isBookEntry: false,
    isNil: false,
  })

  // LDC Form State
  const [ldcForm, setLdcForm] = useState<CreateLdcRequest>({
    clientId: '',
    certificateNumber: 'LDC/2026/098',
    financialYear: '2026-27',
    section: '194C',
    validFrom: '2026-04-01',
    validTo: '2027-03-31',
    specifiedRate: 0.5,
    thresholdAmount: 5000000,
    deducteePan: 'AAACP1234D',
    deducteeName: 'Alpha Logistics Pvt Ltd'
  })

  // Map Modal State
  const [selectedChallanId, setSelectedChallanId] = useState<string>('')
  const [selectedDeductionIds, setSelectedDeductionIds] = useState<string[]>([])

  // Calculator State
  const [calcForm, setCalcForm] = useState({
    tdsAmount: 2000,
    paymentDate: '2024-10-10',
    deductionDate: '2024-11-15',
    depositDate: '2025-01-10'
  })
  const [calcResult, setCalcResult] = useState<TdsInterestResultDto | null>(null)

  const loadData = useCallback(async () => {
    try {
      setLoading(true)
      setError(null)
      const [sum, clientRes, deds, chs, certs] = await Promise.all([
        tdsApi.summary(),
        api<ClientPage>('/api/clients?take=100'),
        tdsApi.listDeductions(selectedClientId || undefined, selectedFy || undefined, selectedQuarter || undefined),
        tdsApi.listChallans(selectedClientId || undefined, selectedFy || undefined, selectedQuarter || undefined),
        tdsApi.listLdcs(selectedClientId || undefined, selectedFy || undefined)
      ])
      setSummary(sum)
      const clientList = clientRes.items || []
      setClients(clientList)
      setDeductions(deds)
      setChallans(chs)
      setLdcs(certs)
      const fallbackClientId = selectedClientId || clientList[0]?.id || ''
      if (fallbackClientId) {
        setDeductionForm(prev => ({ ...prev, clientId: prev.clientId || fallbackClientId, financialYear: selectedFy, quarter: selectedQuarter }))
        setChallanForm(prev => ({ ...prev, clientId: prev.clientId || fallbackClientId, financialYear: selectedFy, quarter: selectedQuarter }))
        setLdcForm(prev => ({ ...prev, clientId: prev.clientId || fallbackClientId, financialYear: selectedFy }))
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load TDS data')
    } finally {
      setLoading(false)
    }
  }, [selectedClientId, selectedFy, selectedQuarter])

  const handleNotify = useCallback(({ error: err, success: ok }: { error?: string | null; success?: string | null }) => {
    setError(err ?? null)
    setSuccess(ok ?? null)
  }, [])

  useEffect(() => {
    loadData()
  }, [loadData])

  const handleRecordDeduction = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setLoading(true)
      await tdsApi.recordDeduction(deductionForm)
      setSuccess('TDS deduction recorded successfully')
      setShowDeductionModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to record deduction')
    } finally {
      setLoading(false)
    }
  }

  const handleCreateChallan = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setLoading(true)
      await tdsApi.createChallan(challanForm)
      setSuccess('ITNS 281 Challan created successfully')
      setShowChallanModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create challan')
    } finally {
      setLoading(false)
    }
  }

  const handleCreateLdc = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setLoading(true)
      await tdsApi.createLdc(ldcForm)
      setSuccess('Lower Deduction Certificate registered successfully')
      setShowLdcModal(false)
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to create LDC')
    } finally {
      setLoading(false)
    }
  }

  const handleMapDeductions = async () => {
    if (!selectedChallanId || selectedDeductionIds.length === 0) {
      setError('Select a challan and at least one deduction entry to map.')
      return
    }
    try {
      setLoading(true)
      await tdsApi.mapToChallan({
        challanId: selectedChallanId,
        deductionIds: selectedDeductionIds
      })
      setSuccess('Deductions successfully linked to Challan')
      setShowMapModal(false)
      setSelectedDeductionIds([])
      await loadData()
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to map deductions')
    } finally {
      setLoading(false)
    }
  }

  const runInterestCalculator = async () => {
    try {
      const res = await tdsApi.calculateInterest({
        tdsAmount: Number(calcForm.tdsAmount),
        paymentDate: calcForm.paymentDate,
        deductionDate: calcForm.deductionDate,
        depositDate: calcForm.depositDate
      })
      setCalcResult(res)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Calculation error')
    }
  }

  return (
    <div style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto', color: 'var(--text)' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: 0, color: 'var(--text)' }}>
            TDS / TCS Compliance &amp; Form 24Q / 26Q / 27Q Studio
          </h1>
          <p style={{ margin: '4px 0 0', color: 'var(--muted)', fontSize: '14px' }}>
            Deductee register, ITNS 281 Challans, Sec 197 LDC, NSDL RPU .txt Export &amp; Form 16/16A Import (Act 1961 &amp; Act 2025)
          </p>
        </div>
        <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
          <button
            onClick={() => {
              setDeductionForm(prev => ({
                ...prev,
                clientId: selectedClientId || prev.clientId || clients[0]?.id || '',
                financialYear: selectedFy,
                quarter: selectedQuarter,
              }))
              setShowDeductionModal(true)
            }}
            style={{
              padding: '8px 16px',
              backgroundColor: 'var(--accent)',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 600,
              cursor: 'pointer'
            }}
          >
            + Record Deduction
          </button>
          <button
            onClick={() => {
              setChallanForm(prev => ({
                ...prev,
                clientId: selectedClientId || prev.clientId || clients[0]?.id || '',
                financialYear: selectedFy,
                quarter: selectedQuarter,
              }))
              setShowChallanModal(true)
            }}
            style={{
              padding: '8px 16px',
              backgroundColor: 'var(--card)',
              color: 'var(--text)',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              fontWeight: 500,
              cursor: 'pointer'
            }}
          >
            + New Challan (ITNS 281)
          </button>
          <button
            onClick={() => {
              setLdcForm(prev => ({
                ...prev,
                clientId: selectedClientId || prev.clientId || clients[0]?.id || '',
                financialYear: selectedFy,
              }))
              setShowLdcModal(true)
            }}
            style={{
              padding: '8px 16px',
              backgroundColor: 'var(--card)',
              color: 'var(--text)',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              fontWeight: 500,
              cursor: 'pointer'
            }}
          >
            + Register LDC
          </button>
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div style={{ padding: '12px 16px', backgroundColor: 'rgba(220, 38, 38, 0.08)', border: '1px solid rgba(220, 38, 38, 0.35)', borderRadius: '6px', color: 'var(--error)', marginBottom: '16px' }}>
          {error}
        </div>
      )}
      {success && (
        <div style={{ padding: '12px 16px', backgroundColor: 'rgba(22, 163, 74, 0.08)', border: '1px solid rgba(22, 163, 74, 0.35)', borderRadius: '6px', color: 'var(--success)', marginBottom: '16px' }}>
          {success}
        </div>
      )}

      {/* KPI Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', borderTop: '3px solid var(--accent)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--muted)', textTransform: 'uppercase' }}>Total Deductions</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text)', marginTop: '4px' }}>{summary.totalDeductionsCount}</div>
            <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '4px' }}>Booked: ₹{summary.totalTaxDeducted.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</div>
          </div>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', borderTop: '3px solid var(--success)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--success)', textTransform: 'uppercase' }}>Deposited in Challans</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--success)', marginTop: '4px' }}>₹{summary.totalDeposited.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</div>
            <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '4px' }}>{summary.totalChallansCount} Challans Registered</div>
          </div>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', borderTop: '3px solid var(--warning)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--warning)', textTransform: 'uppercase' }}>Pending Deposit</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--warning)', marginTop: '4px' }}>₹{summary.totalPendingDeposit.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</div>
            <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '4px' }}>Requires Challan mapping</div>
          </div>
          <div style={{ backgroundColor: 'var(--card)', padding: '16px', borderRadius: '8px', border: '1px solid var(--border)', borderTop: '3px solid #6366f1' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: '#6366f1', textTransform: 'uppercase' }}>Active LDC Certificates</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#6366f1', marginTop: '4px' }}>{summary.activeLdcCount}</div>
            <div style={{ fontSize: '13px', color: 'var(--muted)', marginTop: '4px' }}>Section 197 lower rate</div>
          </div>
        </div>
      )}

      {/* Global Filter Bar */}
      <div style={{ display: 'flex', gap: '12px', alignItems: 'center', backgroundColor: 'var(--card-subtle)', padding: '12px 16px', borderRadius: '8px', marginBottom: '20px', border: '1px solid var(--border)' }}>
        <div>
          <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client:</label>
          <select
            value={selectedClientId}
            onChange={(e) => setSelectedClientId(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '14px' }}
          >
            <option value="">All Clients</option>
            {clients.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>
        <div>
          <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Financial Year:</label>
          <select
            value={selectedFy}
            onChange={(e) => setSelectedFy(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '14px' }}
          >
            {TDS_FINANCIAL_YEARS.map(fy => (
              <option key={fy} value={fy}>
                {Number(fy.slice(0, 4)) >= 2026 ? `TY ${fy} (Act 2025)` : `FY ${fy}`}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Quarter:</label>
          <select
            value={selectedQuarter}
            onChange={(e) => setSelectedQuarter(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '14px' }}
          >
            <option value="Q1">Q1 (Apr - Jun)</option>
            <option value="Q2">Q2 (Jul - Sep)</option>
            <option value="Q3">Q3 (Oct - Dec)</option>
            <option value="Q4">Q4 (Jan - Mar)</option>
          </select>
        </div>
        <div style={{ marginLeft: 'auto', display: 'flex', gap: '8px' }}>
          {selectedDeductionIds.length > 0 && (
            <button
              onClick={() => setShowMapModal(true)}
              style={{
                padding: '6px 12px',
                backgroundColor: 'var(--success)',
                color: '#fff',
                border: 'none',
                borderRadius: '6px',
                fontSize: '13px',
                cursor: 'pointer'
              }}
            >
              Map {selectedDeductionIds.length} Deductions to Challan
            </button>
          )}
          <button
            onClick={loadData}
            style={{
              padding: '6px 12px',
              backgroundColor: 'var(--card)',
              color: 'var(--text)',
              border: '1px solid var(--border)',
              borderRadius: '6px',
              fontSize: '13px',
              cursor: 'pointer'
            }}
          >
            Refresh
          </button>
        </div>
      </div>

      {/* Tabs */}
      <div style={{ display: 'flex', flexWrap: 'wrap', borderBottom: '1px solid var(--border)', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('deductions')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'deductions' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'deductions' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'deductions' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Deductions Register ({deductions.length})
        </button>
        <button
          onClick={() => setActiveTab('challans')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'challans' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'challans' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'challans' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Challans ITNS 281 ({challans.length})
        </button>
        <button
          onClick={() => setActiveTab('ldc')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'ldc' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'ldc' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'ldc' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Lower Deduction Certificates (Sec 197) ({ldcs.length})
        </button>
        <button
          onClick={() => setActiveTab('calculator')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'calculator' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'calculator' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'calculator' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Sec 201(1A) Interest Calculator
        </button>
        <button
          onClick={() => setActiveTab('returns')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'returns' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'returns' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'returns' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          RPU .txt Export &amp; FVU
        </button>
        <button
          onClick={() => setActiveTab('import')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'import' ? '2px solid var(--accent)' : '2px solid transparent',
            color: activeTab === 'import' ? 'var(--accent)' : 'var(--muted)',
            fontWeight: activeTab === 'import' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Form 16 / 16A Import
        </button>
      </div>

      {/* Tab 1: Deductions Register */}
      {activeTab === 'deductions' && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                <tr>
                  <th style={{ padding: '12px' }}>
                    <input
                      type="checkbox"
                      checked={deductions.length > 0 && selectedDeductionIds.length === deductions.length}
                      onChange={(e) => {
                        if (e.target.checked) setSelectedDeductionIds(deductions.map(d => d.id))
                        else setSelectedDeductionIds([])
                      }}
                    />
                  </th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Deductee / PAN</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Section / Form</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Payment Date</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Amount Paid</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Rate</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>TDS Deducted</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Booking Status</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Challan Link</th>
                </tr>
              </thead>
              <tbody>
                {deductions.length === 0 ? (
                  <tr>
                    <td colSpan={9} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No TDS deduction entries found for this selection. Record a new deduction to begin.
                    </td>
                  </tr>
                ) : (
                  deductions.map((d) => (
                    <tr key={d.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px' }}>
                        <input
                          type="checkbox"
                          checked={selectedDeductionIds.includes(d.id)}
                          onChange={(e) => {
                            if (e.target.checked) {
                              setSelectedDeductionIds(prev => [...prev, d.id])
                            } else {
                              setSelectedDeductionIds(prev => prev.filter(id => id !== d.id))
                            }
                          }}
                        />
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 600, color: 'var(--text)' }}>{d.deducteeName}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)' }}>
                          {d.deducteePan || <span style={{ color: 'var(--error)' }}>PAN NOT AVAILABLE (206AA)</span>}
                        </div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ display: 'inline-block', padding: '2px 8px', borderRadius: '4px', backgroundColor: 'rgba(37, 99, 235, 0.12)', color: 'var(--accent)', fontWeight: 600, fontSize: '11px' }}>
                          {d.section}
                        </span>
                        <div style={{ fontSize: '11px', color: 'var(--muted)', marginTop: '2px' }}>Form {d.formType}</div>
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {d.paymentDate ? new Date(d.paymentDate).toLocaleDateString() : '-'}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: 'var(--text)' }}>
                        ₹{d.amountPaid.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ fontWeight: 600, color: d.deducteePan ? 'var(--accent)' : 'var(--error)' }}>
                          {d.tdsRate}%
                        </span>
                        {d.higherRateReason && (
                          <div style={{ fontSize: '10px', color: 'var(--error)' }}>{d.higherRateReason}</div>
                        )}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 700, color: 'var(--error)' }}>
                        ₹{d.totalTaxDeducted.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: ['deposited', 'mapped_to_challan'].includes(d.bookingStatus.toLowerCase()) ? 'rgba(22, 163, 74, 0.12)' : 'rgba(217, 119, 6, 0.12)',
                            color: ['deposited', 'mapped_to_challan'].includes(d.bookingStatus.toLowerCase()) ? 'var(--success)' : 'var(--warning)'
                          }}
                        >
                          {d.bookingStatus}
                        </span>
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {d.challanId ? (
                          <span style={{ color: 'var(--success)', fontSize: '11px', fontWeight: 500 }}>
                            Linked ({d.challanBsr || 'BSR'} - {d.challanSerial || 'Serial'})
                          </span>
                        ) : (
                          <span style={{ color: 'var(--muted)', fontSize: '11px' }}>Unassigned</span>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 2: Challans ITNS 281 */}
      {activeTab === 'challans' && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>BSR / Serial No</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Tender Date</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Section / Minor Head</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Total Deposit</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Allocated Tax</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Unallocated Balance</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Deductions Linked</th>
                </tr>
              </thead>
              <tbody>
                {challans.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No Challans recorded. Register an ITNS 281 challan receipt.
                    </td>
                  </tr>
                ) : (
                  challans.map((c) => (
                    <tr key={c.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 600, color: 'var(--text)' }}>BSR: {c.bsrCode}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Serial: {c.challanSerialNumber}</div>
                      </td>
                      <td style={{ padding: '12px', color: 'var(--text-secondary)' }}>
                        {c.challanDate ? new Date(c.challanDate).toLocaleDateString() : '-'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 500 }}>Sec {c.sectionCode}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)' }}>Minor Head {c.minorHead}</div>
                      </td>
                      <td style={{ padding: '12px', fontWeight: 700, color: 'var(--success)' }}>
                        ₹{c.totalChallanAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: 'var(--accent)' }}>
                        ₹{c.allocatedAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: c.unallocatedBalance > 0 ? 'var(--warning)' : 'var(--success)' }}>
                        ₹{c.unallocatedBalance.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: c.unallocatedBalance === 0 ? 'rgba(22, 163, 74, 0.12)' : 'rgba(217, 119, 6, 0.12)',
                            color: c.unallocatedBalance === 0 ? 'var(--success)' : 'var(--warning)'
                          }}
                        >
                          {c.deductionsCount} linked
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 3: Lower Deduction Certificates */}
      {activeTab === 'ldc' && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Certificate No</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Deductee / PAN</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Section</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Validity Period</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Lower Rate</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Max Limit</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Utilized Amount</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: 'var(--muted)' }}>Remaining</th>
                </tr>
              </thead>
              <tbody>
                {ldcs.length === 0 ? (
                  <tr>
                    <td colSpan={8} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No Section 197 / 197A Lower Deduction Certificates registered yet.
                    </td>
                  </tr>
                ) : (
                  ldcs.map((l) => (
                    <tr key={l.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '12px', fontWeight: 600, color: 'var(--text)' }}>{l.certificateNumber}</td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 500 }}>{l.deducteeName}</div>
                        <div style={{ fontSize: '11px', color: 'var(--muted)' }}>{l.deducteePan}</div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ padding: '2px 8px', borderRadius: '4px', backgroundColor: 'rgba(99, 102, 241, 0.12)', color: '#6366f1', fontWeight: 600, fontSize: '11px' }}>
                          Sec {l.section}
                        </span>
                      </td>
                      <td style={{ padding: '12px', fontSize: '12px', color: 'var(--text-secondary)' }}>
                        {l.validFrom ? new Date(l.validFrom).toLocaleDateString() : ''} to {l.validTo ? new Date(l.validTo).toLocaleDateString() : ''}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 700, color: 'var(--accent)' }}>
                        {l.specifiedRate}%
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: 'var(--text)' }}>
                        ₹{l.thresholdAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px', color: 'var(--muted)' }}>
                        ₹{l.utilizedAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: l.remainingAmount > 0 ? 'var(--success)' : 'var(--error)' }}>
                        ₹{l.remainingAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Tab 4: Sec 201(1A) Interest Calculator */}
      {activeTab === 'calculator' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '24px' }}>
          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', border: '1px solid var(--border)' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, marginTop: 0, color: 'var(--text)' }}>
              Statutory Interest Calculator u/s 201(1A)
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--muted)', marginBottom: '20px' }}>
              Computes delay in deduction (1% per month or part of a month) and delay in deposit (1.5% per month or part of a month) as per the Income Tax Act, 1961.
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>TDS Principal Amount (₹):</label>
                <input
                  type="number"
                  value={calcForm.tdsAmount}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, tdsAmount: Number(e.target.value) }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Date of Payment / Bill Credit:</label>
                <input
                  type="date"
                  value={calcForm.paymentDate}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, paymentDate: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Actual Date of Tax Deduction:</label>
                <input
                  type="date"
                  value={calcForm.deductionDate}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, deductionDate: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Actual / Projected Date of Deposit in Bank:</label>
                <input
                  type="date"
                  value={calcForm.depositDate}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, depositDate: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                />
              </div>

              <button
                onClick={runInterestCalculator}
                style={{
                  padding: '10px 16px',
                  backgroundColor: 'var(--accent)',
                  color: '#fff',
                  border: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  marginTop: '8px'
                }}
              >
                Compute Section 201(1A) Interest
              </button>
            </div>
          </div>

          <div style={{ backgroundColor: 'var(--card)', padding: '24px', borderRadius: '8px', border: '1px solid var(--border)' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, marginTop: 0, color: 'var(--text)' }}>
              Computation Breakdown
            </h3>
            {calcResult ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                <div style={{ padding: '16px', backgroundColor: 'rgba(217, 119, 6, 0.08)', borderRadius: '8px', border: '1px solid rgba(217, 119, 6, 0.35)' }}>
                  <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', color: 'var(--warning)' }}>1. Delay in Tax Deduction u/s 201(1A)(i)</h4>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px', fontSize: '13px' }}>
                    <span style={{ color: 'var(--muted)' }}>Rate: 1% per month</span>
                    <span style={{ fontWeight: 600 }}>{calcResult.delayDeductionMonths} months</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '14px', fontWeight: 700, color: 'var(--warning)' }}>
                    <span>Interest on Deduction Delay:</span>
                    <span>₹{calcResult.delayDeductionInterest.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span>
                  </div>
                </div>

                <div style={{ padding: '16px', backgroundColor: 'rgba(220, 38, 38, 0.08)', borderRadius: '8px', border: '1px solid rgba(220, 38, 38, 0.35)' }}>
                  <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', color: 'var(--error)' }}>2. Delay in Tax Deposit u/s 201(1A)(ii)</h4>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px', fontSize: '13px' }}>
                    <span style={{ color: 'var(--muted)' }}>Rate: 1.5% per month</span>
                    <span style={{ fontWeight: 600 }}>{calcResult.delayDepositMonths} months</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '14px', fontWeight: 700, color: 'var(--error)' }}>
                    <span>Interest on Deposit Delay:</span>
                    <span>₹{calcResult.delayDepositInterest.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span>
                  </div>
                </div>

                <div style={{ padding: '16px', backgroundColor: 'rgba(22, 163, 74, 0.08)', borderRadius: '8px', border: '1px solid rgba(22, 163, 74, 0.35)' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '16px', fontWeight: 700, color: 'var(--success)' }}>
                    <span>Total Statutory Interest:</span>
                    <span>₹{calcResult.totalInterest.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span>
                  </div>
                </div>
              </div>
            ) : (
              <div style={{ textAlign: 'center', padding: '40px 20px', color: 'var(--muted)' }}>
                Fill in the details on the left and click "Compute Section 201(1A) Interest" to preview interest calculations.
              </div>
            )}
          </div>
        </div>
      )}

      {/* Tab 5: NSDL RPU .txt Export & FVU */}
      {activeTab === 'returns' && (
        <TdsRpuExportTab
          clients={clients}
          selectedClientId={selectedClientId}
          onSelectClient={setSelectedClientId}
          financialYear={selectedFy}
          quarter={selectedQuarter}
          onNotify={handleNotify}
        />
      )}

      {/* Tab 6: Form 16 / 16A Import */}
      {activeTab === 'import' && (
        <TdsForm16ImportTab
          clients={clients}
          selectedClientId={selectedClientId}
          onSelectClient={setSelectedClientId}
          financialYear={selectedFy}
          quarter={selectedQuarter}
          onImported={loadData}
          onNotify={handleNotify}
        />
      )}

      {/* Modal: Record Deduction */}
      {showDeductionModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', borderRadius: '8px', width: '600px', maxWidth: '90%', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>Record New TDS Deduction</h3>
            <form onSubmit={handleRecordDeduction} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client:</label>
                <select
                  required
                  value={deductionForm.clientId}
                  onChange={(e) => setDeductionForm(prev => ({ ...prev, clientId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                >
                  {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Return Form:</label>
                  <select
                    value={deductionForm.formType || '26Q'}
                    onChange={(e) => {
                      const nextForm = e.target.value as '24Q' | '26Q' | '27Q'
                      const defaultSec = nextForm === '24Q' ? '192_salary' : nextForm === '27Q' ? '195' : '194C_company'
                      setDeductionForm(prev => ({ ...prev, formType: nextForm, section: defaultSec }))
                    }}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="24Q">24Q / 138 (Salary Employees)</option>
                    <option value="26Q">26Q / 140 (Domestic Resident)</option>
                    <option value="27Q">27Q / 144 (Non-Resident / Foreign Entity)</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>TDS Section:</label>
                  <select
                    value={deductionForm.section}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, section: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  >
                    {TDS_SECTION_OPTIONS.map(opt => (
                      <option key={opt.value} value={opt.value}>{opt.label}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Custom Rate % (Optional — overrides slab/24Q):</label>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    max="100"
                    value={deductionForm.customRate ?? ''}
                    placeholder="Auto from section / LDC"
                    onChange={(e) => setDeductionForm(prev => ({
                      ...prev,
                      customRate: e.target.value === '' ? null : Number(e.target.value)
                    }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Deductee Classification (FVU DD Col 8):</label>
                  <select
                    value={deductionForm.deducteeCode || ''}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, deducteeCode: e.target.value || null }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="">Auto (from 4th char of PAN)</option>
                    <option value="1">1 - Company</option>
                    <option value="2">2 - Non-Company</option>
                  </select>
                </div>
              </div>

              {deductionForm.formType === '27Q' && (
                <div style={{ padding: '12px', borderRadius: '6px', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border)', display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '10px' }}>
                  <div>
                    <label style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Nature of Remittance (27Q / 144):</label>
                    <select
                      value={deductionForm.remittanceCode || '21'}
                      onChange={(e) => setDeductionForm(prev => ({ ...prev, remittanceCode: e.target.value }))}
                      style={{ width: '100%', padding: '7px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    >
                      {REMITTANCE_27Q_CODES.map(r => <option key={r.code} value={r.code}>{r.name}</option>)}
                    </select>
                  </div>
                  <div>
                    <label style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Country of Residence:</label>
                    <select
                      value={deductionForm.countryCode || '267'}
                      onChange={(e) => setDeductionForm(prev => ({ ...prev, countryCode: e.target.value }))}
                      style={{ width: '100%', padding: '7px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    >
                      {COUNTRY_27Q_CODES.map(c => <option key={c.code} value={c.code}>{c.name}</option>)}
                    </select>
                  </div>
                  <div>
                    <label style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Tax Rate Source:</label>
                    <select
                      value={deductionForm.taxRateSource || 'A'}
                      onChange={(e) => setDeductionForm(prev => ({ ...prev, taxRateSource: e.target.value }))}
                      style={{ width: '100%', padding: '7px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px' }}
                    >
                      <option value="A">A - As per Income-tax Act</option>
                      <option value="B">B - As per DTAA Treaty</option>
                    </select>
                  </div>
                  <div>
                    <label style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Form 15CA Acknowledgement No.:</label>
                    <input
                      type="text"
                      maxLength={15}
                      value={deductionForm.ackNumber || ''}
                      placeholder="Optional 15CA Ack No."
                      onChange={(e) => setDeductionForm(prev => ({ ...prev, ackNumber: e.target.value }))}
                      style={{ width: '100%', padding: '7px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontSize: '12px', boxSizing: 'border-box' }}
                    />
                  </div>
                </div>
              )}

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Deductee Name:</label>
                  <input
                    type="text"
                    required
                    value={deductionForm.deducteeName}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, deducteeName: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Deductee PAN (Leave blank for 206AA 20%):</label>
                  <input
                    type="text"
                    value={deductionForm.deducteePan || ''}
                    placeholder="e.g. ABCDE1234F"
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, deducteePan: e.target.value.toUpperCase() }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Amount Paid / Credited (₹):</label>
                  <input
                    type="number"
                    required
                    value={deductionForm.amountPaid}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, amountPaid: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Payment / Bill Date:</label>
                  <input
                    type="date"
                    required
                    value={deductionForm.paymentDate}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, paymentDate: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ padding: '8px 0' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={deductionForm.forceHigherRate206Ab || false}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, forceHigherRate206Ab: e.target.checked }))}
                  />
                  <span>Section 206AB Non-Filer Higher Rate (Double rate or 5%) — only for deductions up to 31 Mar 2025; omitted from 1 Apr 2025</span>
                </label>
              </div>

              {ldcs.length > 0 && (
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Link Lower Deduction Certificate (Sec 197):</label>
                  <select
                    value={deductionForm.certificateId || ''}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, certificateId: e.target.value || null }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="">No Certificate (Use Standard Rate)</option>
                    {ldcs.map(l => (
                      <option key={l.id} value={l.id}>{l.certificateNumber} - Sec {l.section} ({l.specifiedRate}%)</option>
                    ))}
                  </select>
                </div>
              )}

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowDeductionModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card-subtle)', color: 'var(--text)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: 'var(--accent)', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save Deduction Entry
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: New Challan ITNS 281 */}
      {showChallanModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', borderRadius: '8px', width: '560px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>Register ITNS 281 Challan</h3>
            <form onSubmit={handleCreateChallan} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client:</label>
                <select
                  required
                  value={challanForm.clientId}
                  onChange={(e) => setChallanForm(prev => ({ ...prev, clientId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                >
                  {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>BSR Code (7 Digits):</label>
                  <input
                    type="text"
                    required={!challanForm.isNil}
                    disabled={!!challanForm.isNil}
                    value={challanForm.bsrCode}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, bsrCode: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Challan Serial No (5 Digits):</label>
                  <input
                    type="text"
                    required={!challanForm.isNil}
                    disabled={!!challanForm.isNil}
                    value={challanForm.challanSerialNumber}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, challanSerialNumber: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Challan Date:</label>
                  <input
                    type="date"
                    required
                    value={challanForm.challanDate}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, challanDate: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Section Code:</label>
                  <input
                    type="text"
                    required
                    value={challanForm.sectionCode}
                    placeholder="e.g. 194C"
                    onChange={(e) => setChallanForm(prev => ({ ...prev, sectionCode: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Minor Head:</label>
                  <select
                    value={challanForm.minorHead || '200'}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, minorHead: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  >
                    <option value="200">200 - Regular TDS/TCS</option>
                    <option value="400">400 - Assessment Demand</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'flex', gap: '16px', flexWrap: 'wrap' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text-secondary)', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={!!challanForm.isBookEntry}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, isBookEntry: e.target.checked }))}
                  />
                  <span>Government Book Entry (BIN / Form 24G)</span>
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', color: 'var(--text-secondary)', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={!!challanForm.isNil}
                    onChange={(e) => setChallanForm(prev => ({
                      ...prev,
                      isNil: e.target.checked,
                      tdsAmount: e.target.checked ? 0 : prev.tdsAmount,
                    }))}
                  />
                  <span>NIL Challan Return</span>
                </label>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>TDS Tax Amount (₹):</label>
                  <input
                    type="number"
                    required
                    value={challanForm.tdsAmount}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, tdsAmount: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Interest u/s 201(1A) (₹):</label>
                  <input
                    type="number"
                    value={challanForm.interest || 0}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, interest: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowChallanModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card-subtle)', color: 'var(--text)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: 'var(--accent)', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save Challan
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Register LDC */}
      {showLdcModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', borderRadius: '8px', width: '560px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>Register Lower Deduction Certificate (Sec 197)</h3>
            <form onSubmit={handleCreateLdc} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Certificate No:</label>
                  <input
                    type="text"
                    required
                    value={ldcForm.certificateNumber}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, certificateNumber: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Deductee PAN:</label>
                  <input
                    type="text"
                    required
                    value={ldcForm.deducteePan}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, deducteePan: e.target.value.toUpperCase() }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Deductee Name:</label>
                  <input
                    type="text"
                    required
                    value={ldcForm.deducteeName}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, deducteeName: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>TDS Section:</label>
                  <input
                    type="text"
                    required
                    value={ldcForm.section}
                    placeholder="e.g. 194C, 194J"
                    onChange={(e) => setLdcForm(prev => ({ ...prev, section: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Valid From:</label>
                  <input
                    type="date"
                    required
                    value={ldcForm.validFrom}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, validFrom: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Valid To:</label>
                  <input
                    type="date"
                    required
                    value={ldcForm.validTo}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, validTo: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Specified Lower Rate (%):</label>
                  <input
                    type="number"
                    step="0.01"
                    required
                    value={ldcForm.specifiedRate}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, specifiedRate: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Threshold Limit Amount (₹):</label>
                  <input
                    type="number"
                    required
                    value={ldcForm.thresholdAmount}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, thresholdAmount: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowLdcModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card-subtle)', color: 'var(--text)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: 'var(--accent)', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  Register Certificate
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Map Deductions to Challan */}
      {showMapModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', borderRadius: '8px', width: '500px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: 'var(--text)' }}>
              Link Deductions to Challan (ITNS 281)
            </h3>
            <p style={{ fontSize: '13px', color: 'var(--muted)', marginBottom: '16px' }}>
              You have selected {selectedDeductionIds.length} deduction entries to map against an ITNS 281 Challan receipt.
            </p>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Choose Challan:</label>
              <select
                value={selectedChallanId}
                onChange={(e) => setSelectedChallanId(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
              >
                <option value="">-- Select Challan --</option>
                {challans.map(c => (
                  <option key={c.id} value={c.id}>
                    BSR: {c.bsrCode} | Challan: {c.challanSerialNumber} | Balance: ₹{c.unallocatedBalance.toLocaleString('en-IN')}
                  </option>
                ))}
              </select>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={() => setShowMapModal(false)}
                style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--border)', background: 'var(--card-subtle)', color: 'var(--text)', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                type="button"
                disabled={loading || !selectedChallanId}
                onClick={handleMapDeductions}
                style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: 'var(--accent)', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
              >
                Confirm Allocation
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
export default TdsPage
