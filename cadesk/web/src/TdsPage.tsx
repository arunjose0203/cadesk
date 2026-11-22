import React, { useState, useEffect } from 'react'
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

export const TdsPage: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'deductions' | 'challans' | 'ldc' | 'calculator'>('deductions')
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
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')
  const [selectedQuarter, setSelectedQuarter] = useState<string>('Q4')

  // Modals
  const [showDeductionModal, setShowDeductionModal] = useState<boolean>(false)
  const [showChallanModal, setShowChallanModal] = useState<boolean>(false)
  const [showLdcModal, setShowLdcModal] = useState<boolean>(false)
  const [showMapModal, setShowMapModal] = useState<boolean>(false)

  // Deduction Form State
  const [deductionForm, setDeductionForm] = useState<RecordTdsDeductionRequest>({
    clientId: '',
    financialYear: '2024-25',
    quarter: 'Q4',
    formType: '26Q',
    section: '194C_company',
    deducteePan: '',
    deducteeName: '',
    forceHigherRate206Ab: false,
    certificateId: undefined,
    paymentDate: new Date().toISOString().split('T')[0],
    deductionDate: new Date().toISOString().split('T')[0],
    amountPaid: 100000
  })

  // Challan Form State
  const [challanForm, setChallanForm] = useState<CreateTdsChallanRequest>({
    clientId: '',
    financialYear: '2024-25',
    quarter: 'Q4',
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
    notes: 'TDS deposit ITNS 281'
  })

  // LDC Form State
  const [ldcForm, setLdcForm] = useState<CreateLdcRequest>({
    clientId: '',
    certificateNumber: 'LDC/2024/098',
    financialYear: '2024-25',
    section: '194C',
    validFrom: '2024-04-01',
    validTo: '2025-03-31',
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

  const loadData = async () => {
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
      if (clientList.length > 0 && !deductionForm.clientId) {
        setDeductionForm(prev => ({ ...prev, clientId: clientList[0].id }))
        setChallanForm(prev => ({ ...prev, clientId: clientList[0].id }))
        setLdcForm(prev => ({ ...prev, clientId: clientList[0].id }))
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load TDS data')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadData()
  }, [selectedClientId, selectedFy, selectedQuarter])

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
    <div style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 700, margin: 0, color: '#1e293b' }}>
            TDS / TCS Compliance & Form 26Q / 27Q Studio
          </h1>
          <p style={{ margin: '4px 0 0', color: '#64748b', fontSize: '14px' }}>
            Statutory tax deduction accounting, Section 197 LDC tracking, ITNS 281 Challan mapping & Sec 201(1A) Interest
          </p>
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          <button
            onClick={() => setShowDeductionModal(true)}
            style={{
              padding: '8px 16px',
              backgroundColor: '#2563eb',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 500,
              cursor: 'pointer'
            }}
          >
            + Record Deduction
          </button>
          <button
            onClick={() => setShowChallanModal(true)}
            style={{
              padding: '8px 16px',
              backgroundColor: '#059669',
              color: '#fff',
              border: 'none',
              borderRadius: '6px',
              fontWeight: 500,
              cursor: 'pointer'
            }}
          >
            + New Challan (ITNS 281)
          </button>
          <button
            onClick={() => setShowLdcModal(true)}
            style={{
              padding: '8px 16px',
              backgroundColor: '#7c3aed',
              color: '#fff',
              border: 'none',
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
        <div style={{ padding: '12px 16px', backgroundColor: '#fee2e2', border: '1px solid #f87171', borderRadius: '6px', color: '#b91c1c', marginBottom: '16px' }}>
          {error}
        </div>
      )}
      {success && (
        <div style={{ padding: '12px 16px', backgroundColor: '#ecfdf5', border: '1px solid #34d399', borderRadius: '6px', color: '#065f46', marginBottom: '16px' }}>
          {success}
        </div>
      )}

      {/* KPI Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ backgroundColor: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>Total Deductions</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#0f172a', marginTop: '4px' }}>{summary.totalDeductionsCount}</div>
            <div style={{ fontSize: '13px', color: '#64748b', marginTop: '4px' }}>Booked: ₹{summary.totalTaxDeducted.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</div>
          </div>
          <div style={{ backgroundColor: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: '#059669', textTransform: 'uppercase' }}>Deposited in Challans</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#059669', marginTop: '4px' }}>₹{summary.totalDeposited.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</div>
            <div style={{ fontSize: '13px', color: '#64748b', marginTop: '4px' }}>{summary.totalChallansCount} Challans Registered</div>
          </div>
          <div style={{ backgroundColor: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: '#d97706', textTransform: 'uppercase' }}>Pending Deposit</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#d97706', marginTop: '4px' }}>₹{summary.totalPendingDeposit.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</div>
            <div style={{ fontSize: '13px', color: '#64748b', marginTop: '4px' }}>Requires Challan mapping</div>
          </div>
          <div style={{ backgroundColor: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #7c3aed', boxShadow: '0 1px 3px rgba(0,0,0,0.05)' }}>
            <span style={{ fontSize: '12px', fontWeight: 600, color: '#7c3aed', textTransform: 'uppercase' }}>Active LDC Certificates</span>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#7c3aed', marginTop: '4px' }}>{summary.activeLdcCount}</div>
            <div style={{ fontSize: '13px', color: '#64748b', marginTop: '4px' }}>Section 197 lower rate</div>
          </div>
        </div>
      )}

      {/* Global Filter Bar */}
      <div style={{ display: 'flex', gap: '12px', alignItems: 'center', backgroundColor: '#f8fafc', padding: '12px 16px', borderRadius: '8px', marginBottom: '20px', border: '1px solid #e2e8f0' }}>
        <div>
          <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Client:</label>
          <select
            value={selectedClientId}
            onChange={(e) => setSelectedClientId(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '14px' }}
          >
            <option value="">All Clients</option>
            {clients.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
        </div>
        <div>
          <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Financial Year:</label>
          <select
            value={selectedFy}
            onChange={(e) => setSelectedFy(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '14px' }}
          >
            <option value="2024-25">FY 2024-25</option>
            <option value="2023-24">FY 2023-24</option>
          </select>
        </div>
        <div>
          <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Quarter:</label>
          <select
            value={selectedQuarter}
            onChange={(e) => setSelectedQuarter(e.target.value)}
            style={{ padding: '6px 12px', borderRadius: '6px', border: '1px solid #cbd5e1', fontSize: '14px' }}
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
                backgroundColor: '#0f766e',
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
              backgroundColor: '#fff',
              border: '1px solid #cbd5e1',
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
      <div style={{ display: 'flex', borderBottom: '1px solid #e2e8f0', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('deductions')}
          style={{
            padding: '10px 20px',
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'deductions' ? '2px solid #2563eb' : '2px solid transparent',
            color: activeTab === 'deductions' ? '#2563eb' : '#64748b',
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
            borderBottom: activeTab === 'challans' ? '2px solid #2563eb' : '2px solid transparent',
            color: activeTab === 'challans' ? '#2563eb' : '#64748b',
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
            borderBottom: activeTab === 'ldc' ? '2px solid #2563eb' : '2px solid transparent',
            color: activeTab === 'ldc' ? '#2563eb' : '#64748b',
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
            borderBottom: activeTab === 'calculator' ? '2px solid #2563eb' : '2px solid transparent',
            color: activeTab === 'calculator' ? '#2563eb' : '#64748b',
            fontWeight: activeTab === 'calculator' ? 600 : 500,
            cursor: 'pointer'
          }}
        >
          Sec 201(1A) Interest Calculator
        </button>
      </div>

      {/* Tab 1: Deductions Register */}
      {activeTab === 'deductions' && (
        <div style={{ backgroundColor: '#fff', borderRadius: '8px', border: '1px solid #e2e8f0', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
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
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Deductee / PAN</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Section / Form</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Payment Date</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Amount Paid</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Rate</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>TDS Deducted</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Booking Status</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Challan Link</th>
                </tr>
              </thead>
              <tbody>
                {deductions.length === 0 ? (
                  <tr>
                    <td colSpan={9} style={{ padding: '32px', textAlign: 'center', color: '#94a3b8' }}>
                      No TDS deduction entries found for this selection. Record a new deduction to begin.
                    </td>
                  </tr>
                ) : (
                  deductions.map((d) => (
                    <tr key={d.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
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
                        <div style={{ fontWeight: 600, color: '#1e293b' }}>{d.deducteeName}</div>
                        <div style={{ fontSize: '11px', color: '#64748b' }}>
                          {d.deducteePan || <span style={{ color: '#ef4444' }}>PAN NOT AVAILABLE (206AA)</span>}
                        </div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ display: 'inline-block', padding: '2px 8px', borderRadius: '4px', backgroundColor: '#e0e7ff', color: '#3730a3', fontWeight: 600, fontSize: '11px' }}>
                          {d.section}
                        </span>
                        <div style={{ fontSize: '11px', color: '#64748b', marginTop: '2px' }}>Form {d.formType}</div>
                      </td>
                      <td style={{ padding: '12px', color: '#475569' }}>
                        {d.paymentDate ? new Date(d.paymentDate).toLocaleDateString() : '-'}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: '#0f172a' }}>
                        ₹{d.amountPaid.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ fontWeight: 600, color: d.deducteePan ? '#2563eb' : '#dc2626' }}>
                          {d.tdsRate}%
                        </span>
                        {d.higherRateReason && (
                          <div style={{ fontSize: '10px', color: '#dc2626' }}>{d.higherRateReason}</div>
                        )}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 700, color: '#dc2626' }}>
                        ₹{d.totalTaxDeducted.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: d.bookingStatus === 'Deposited' ? '#dcfce7' : '#fef3c7',
                            color: d.bookingStatus === 'Deposited' ? '#15803d' : '#b45309'
                          }}
                        >
                          {d.bookingStatus}
                        </span>
                      </td>
                      <td style={{ padding: '12px', color: '#475569' }}>
                        {d.challanId ? (
                          <span style={{ color: '#059669', fontSize: '11px', fontWeight: 500 }}>
                            Linked ({d.challanBsr || 'BSR'} - {d.challanSerial || 'Serial'})
                          </span>
                        ) : (
                          <span style={{ color: '#94a3b8', fontSize: '11px' }}>Unassigned</span>
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
        <div style={{ backgroundColor: '#fff', borderRadius: '8px', border: '1px solid #e2e8f0', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>BSR / Serial No</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Tender Date</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Section / Minor Head</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Total Deposit</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Allocated Tax</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Unallocated Balance</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Deductions Linked</th>
                </tr>
              </thead>
              <tbody>
                {challans.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: '#94a3b8' }}>
                      No Challans recorded. Register an ITNS 281 challan receipt.
                    </td>
                  </tr>
                ) : (
                  challans.map((c) => (
                    <tr key={c.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 600, color: '#1e293b' }}>BSR: {c.bsrCode}</div>
                        <div style={{ fontSize: '11px', color: '#64748b' }}>Serial: {c.challanSerialNumber}</div>
                      </td>
                      <td style={{ padding: '12px', color: '#475569' }}>
                        {c.challanDate ? new Date(c.challanDate).toLocaleDateString() : '-'}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 500 }}>Sec {c.sectionCode}</div>
                        <div style={{ fontSize: '11px', color: '#64748b' }}>Minor Head {c.minorHead}</div>
                      </td>
                      <td style={{ padding: '12px', fontWeight: 700, color: '#059669' }}>
                        ₹{c.totalChallanAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: '#2563eb' }}>
                        ₹{c.allocatedAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: c.unallocatedBalance > 0 ? '#d97706' : '#15803d' }}>
                        ₹{c.unallocatedBalance.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span
                          style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            backgroundColor: c.unallocatedBalance === 0 ? '#dcfce7' : '#fef3c7',
                            color: c.unallocatedBalance === 0 ? '#15803d' : '#b45309'
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
        <div style={{ backgroundColor: '#fff', borderRadius: '8px', border: '1px solid #e2e8f0', overflow: 'hidden' }}>
          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead style={{ backgroundColor: '#f8fafc', borderBottom: '1px solid #e2e8f0' }}>
                <tr>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Certificate No</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Deductee / PAN</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Section</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Validity Period</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Lower Rate</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Max Limit</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Utilized Amount</th>
                  <th style={{ padding: '12px', fontWeight: 600, color: '#475569' }}>Remaining</th>
                </tr>
              </thead>
              <tbody>
                {ldcs.length === 0 ? (
                  <tr>
                    <td colSpan={8} style={{ padding: '32px', textAlign: 'center', color: '#94a3b8' }}>
                      No Section 197 / 197A Lower Deduction Certificates registered yet.
                    </td>
                  </tr>
                ) : (
                  ldcs.map((l) => (
                    <tr key={l.id} style={{ borderBottom: '1px solid #f1f5f9' }}>
                      <td style={{ padding: '12px', fontWeight: 600, color: '#1e293b' }}>{l.certificateNumber}</td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontWeight: 500 }}>{l.deducteeName}</div>
                        <div style={{ fontSize: '11px', color: '#64748b' }}>{l.deducteePan}</div>
                      </td>
                      <td style={{ padding: '12px' }}>
                        <span style={{ padding: '2px 8px', borderRadius: '4px', backgroundColor: '#f3e8ff', color: '#6b21a8', fontWeight: 600, fontSize: '11px' }}>
                          Sec {l.section}
                        </span>
                      </td>
                      <td style={{ padding: '12px', fontSize: '12px', color: '#475569' }}>
                        {l.validFrom ? new Date(l.validFrom).toLocaleDateString() : ''} to {l.validTo ? new Date(l.validTo).toLocaleDateString() : ''}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 700, color: '#2563eb' }}>
                        {l.specifiedRate}%
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: '#0f172a' }}>
                        ₹{l.thresholdAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px', color: '#64748b' }}>
                        ₹{l.utilizedAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px', fontWeight: 600, color: l.remainingAmount > 0 ? '#15803d' : '#b91c1c' }}>
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
          <div style={{ backgroundColor: '#fff', padding: '24px', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, marginTop: 0, color: '#1e293b' }}>
              Statutory Interest Calculator u/s 201(1A)
            </h3>
            <p style={{ fontSize: '13px', color: '#64748b', marginBottom: '20px' }}>
              Computes delay in deduction (1% per month or part of a month) and delay in deposit (1.5% per month or part of a month) as per the Income Tax Act, 1961.
            </p>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: '#334155', display: 'block', marginBottom: '4px' }}>TDS Principal Amount (₹):</label>
                <input
                  type="number"
                  value={calcForm.tdsAmount}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, tdsAmount: Number(e.target.value) }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: '#334155', display: 'block', marginBottom: '4px' }}>Date of Payment / Bill Credit:</label>
                <input
                  type="date"
                  value={calcForm.paymentDate}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, paymentDate: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: '#334155', display: 'block', marginBottom: '4px' }}>Actual Date of Tax Deduction:</label>
                <input
                  type="date"
                  value={calcForm.deductionDate}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, deductionDate: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: '#334155', display: 'block', marginBottom: '4px' }}>Actual / Projected Date of Deposit in Bank:</label>
                <input
                  type="date"
                  value={calcForm.depositDate}
                  onChange={(e) => setCalcForm(prev => ({ ...prev, depositDate: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                />
              </div>

              <button
                onClick={runInterestCalculator}
                style={{
                  padding: '10px 16px',
                  backgroundColor: '#2563eb',
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

          <div style={{ backgroundColor: '#fff', padding: '24px', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
            <h3 style={{ fontSize: '16px', fontWeight: 700, marginTop: 0, color: '#1e293b' }}>
              Computation Breakdown
            </h3>
            {calcResult ? (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                <div style={{ padding: '16px', backgroundColor: '#fffbeb', borderRadius: '8px', border: '1px solid #fef3c7' }}>
                  <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', color: '#92400e' }}>1. Delay in Tax Deduction u/s 201(1A)(i)</h4>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px', fontSize: '13px' }}>
                    <span style={{ color: '#78350f' }}>Rate: 1% per month</span>
                    <span style={{ fontWeight: 600 }}>{calcResult.delayDeductionMonths} months</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '14px', fontWeight: 700, color: '#b45309' }}>
                    <span>Interest on Deduction Delay:</span>
                    <span>₹{calcResult.delayDeductionInterest.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span>
                  </div>
                </div>

                <div style={{ padding: '16px', backgroundColor: '#fef2f2', borderRadius: '8px', border: '1px solid #fecaca' }}>
                  <h4 style={{ margin: '0 0 8px 0', fontSize: '14px', color: '#991b1b' }}>2. Delay in Tax Deposit u/s 201(1A)(ii)</h4>
                  <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '4px', fontSize: '13px' }}>
                    <span style={{ color: '#7f1d1d' }}>Rate: 1.5% per month</span>
                    <span style={{ fontWeight: 600 }}>{calcResult.delayDepositMonths} months</span>
                  </div>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '14px', fontWeight: 700, color: '#dc2626' }}>
                    <span>Interest on Deposit Delay:</span>
                    <span>₹{calcResult.delayDepositInterest.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span>
                  </div>
                </div>

                <div style={{ padding: '16px', backgroundColor: '#ecfdf5', borderRadius: '8px', border: '1px solid #a7f3d0' }}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '16px', fontWeight: 700, color: '#065f46' }}>
                    <span>Total Statutory Interest:</span>
                    <span>₹{calcResult.totalInterest.toLocaleString('en-IN', { maximumFractionDigits: 2 })}</span>
                  </div>
                </div>
              </div>
            ) : (
              <div style={{ textAlign: 'center', padding: '40px 20px', color: '#94a3b8' }}>
                Fill in the details on the left and click "Compute Section 201(1A) Interest" to preview interest calculations.
              </div>
            )}
          </div>
        </div>
      )}

      {/* Modal: Record Deduction */}
      {showDeductionModal && (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: '#fff', borderRadius: '8px', width: '600px', maxWidth: '90%', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: '#1e293b' }}>Record New TDS Deduction</h3>
            <form onSubmit={handleRecordDeduction} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Client:</label>
                <select
                  required
                  value={deductionForm.clientId}
                  onChange={(e) => setDeductionForm(prev => ({ ...prev, clientId: e.target.value }))}
                  style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                >
                  {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Return Form:</label>
                  <select
                    value={deductionForm.formType || '26Q'}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, formType: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  >
                    <option value="26Q">26Q (Domestic Resident)</option>
                    <option value="27Q">27Q (Non-Resident / Foreign Entity)</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>TDS Section:</label>
                  <select
                    value={deductionForm.section}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, section: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  >
                    <option value="194C_company">194C (Contractor - Company / Firm) [2%]</option>
                    <option value="194C_individual">194C (Contractor - Individual / HUF) [1%]</option>
                    <option value="194J_technical">194J (Technical / Royalty / FTS) [2%]</option>
                    <option value="194J_professional">194J (Professional Services / Director) [10%]</option>
                    <option value="194I_rent_land">194I (Rent - Land / Building) [10%]</option>
                    <option value="194I_rent_plant">194I (Rent - Plant / Machinery) [2%]</option>
                    <option value="194Q">194Q (Purchase of Goods &gt; ₹50L) [0.1%]</option>
                    <option value="194A">194A (Interest other than securities) [10%]</option>
                    <option value="194H">194H (Commission or Brokerage) [5%]</option>
                    <option value="195">195 (Payment to Non-Resident)</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Deductee Name:</label>
                  <input
                    type="text"
                    required
                    value={deductionForm.deducteeName}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, deducteeName: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Deductee PAN (Leave blank for 206AA 20%):</label>
                  <input
                    type="text"
                    value={deductionForm.deducteePan || ''}
                    placeholder="e.g. ABCDE1234F"
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, deducteePan: e.target.value.toUpperCase() }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Amount Paid / Credited (₹):</label>
                  <input
                    type="number"
                    required
                    value={deductionForm.amountPaid}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, amountPaid: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Payment / Bill Date:</label>
                  <input
                    type="date"
                    required
                    value={deductionForm.paymentDate}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, paymentDate: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
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
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Link Lower Deduction Certificate (Sec 197):</label>
                  <select
                    value={deductionForm.certificateId || ''}
                    onChange={(e) => setDeductionForm(prev => ({ ...prev, certificateId: e.target.value || null }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
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
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#2563eb', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
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
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: '#fff', borderRadius: '8px', width: '560px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: '#1e293b' }}>Register ITNS 281 Challan</h3>
            <form onSubmit={handleCreateChallan} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>BSR Code (7 Digits):</label>
                  <input
                    type="text"
                    required
                    value={challanForm.bsrCode}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, bsrCode: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Challan Serial No (5 Digits):</label>
                  <input
                    type="text"
                    required
                    value={challanForm.challanSerialNumber}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, challanSerialNumber: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Challan Date:</label>
                  <input
                    type="date"
                    required
                    value={challanForm.challanDate}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, challanDate: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Section Code:</label>
                  <input
                    type="text"
                    required
                    value={challanForm.sectionCode}
                    placeholder="e.g. 194C"
                    onChange={(e) => setChallanForm(prev => ({ ...prev, sectionCode: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>TDS Tax Amount (₹):</label>
                  <input
                    type="number"
                    required
                    value={challanForm.tdsAmount}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, tdsAmount: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Interest u/s 201(1A) (₹):</label>
                  <input
                    type="number"
                    value={challanForm.interest || 0}
                    onChange={(e) => setChallanForm(prev => ({ ...prev, interest: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowChallanModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#059669', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
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
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: '#fff', borderRadius: '8px', width: '560px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: '#1e293b' }}>Register Lower Deduction Certificate (Sec 197)</h3>
            <form onSubmit={handleCreateLdc} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Certificate No:</label>
                  <input
                    type="text"
                    required
                    value={ldcForm.certificateNumber}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, certificateNumber: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Deductee PAN:</label>
                  <input
                    type="text"
                    required
                    value={ldcForm.deducteePan}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, deducteePan: e.target.value.toUpperCase() }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Deductee Name:</label>
                  <input
                    type="text"
                    required
                    value={ldcForm.deducteeName}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, deducteeName: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>TDS Section:</label>
                  <input
                    type="text"
                    required
                    value={ldcForm.section}
                    placeholder="e.g. 194C, 194J"
                    onChange={(e) => setLdcForm(prev => ({ ...prev, section: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Valid From:</label>
                  <input
                    type="date"
                    required
                    value={ldcForm.validFrom}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, validFrom: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Valid To:</label>
                  <input
                    type="date"
                    required
                    value={ldcForm.validTo}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, validTo: e.target.value }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Specified Lower Rate (%):</label>
                  <input
                    type="number"
                    step="0.01"
                    required
                    value={ldcForm.specifiedRate}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, specifiedRate: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Threshold Limit Amount (₹):</label>
                  <input
                    type="number"
                    required
                    value={ldcForm.thresholdAmount}
                    onChange={(e) => setLdcForm(prev => ({ ...prev, thresholdAmount: Number(e.target.value) }))}
                    style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowLdcModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#7c3aed', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
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
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ backgroundColor: '#fff', borderRadius: '8px', width: '500px', maxWidth: '90%', padding: '24px' }}>
            <h3 style={{ fontSize: '18px', fontWeight: 700, margin: '0 0 16px 0', color: '#1e293b' }}>
              Link Deductions to Challan (ITNS 281)
            </h3>
            <p style={{ fontSize: '13px', color: '#64748b', marginBottom: '16px' }}>
              You have selected {selectedDeductionIds.length} deduction entries to map against an ITNS 281 Challan receipt.
            </p>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ fontSize: '12px', fontWeight: 600, color: '#475569', display: 'block', marginBottom: '4px' }}>Choose Challan:</label>
              <select
                value={selectedChallanId}
                onChange={(e) => setSelectedChallanId(e.target.value)}
                style={{ width: '100%', padding: '8px', borderRadius: '6px', border: '1px solid #cbd5e1' }}
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
                style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid #cbd5e1', background: '#fff', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                type="button"
                disabled={loading || !selectedChallanId}
                onClick={handleMapDeductions}
                style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#0f766e', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
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
