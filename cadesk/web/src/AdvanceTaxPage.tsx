import React, { useState, useEffect } from 'react'
import { toError,
  advanceTaxApi,
  api,
  type AdvanceTaxPlanDto,
  type AdvanceTaxRegime,
  type AdvanceTaxInstallmentDto,
  type TaxInterestComputationDto,
  type AdvanceTaxSummaryDto,
  type SaveAdvanceTaxPlanRequest,
  type RecordInstallmentPaymentRequest,
  type CalculateStatutoryInterestRequest,
  type Client,
  type ClientPage
} from './api'

interface AdvanceTaxPageProps {
  can?: (p: string) => boolean
}

const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(amount)
}

export const AdvanceTaxPage: React.FC<AdvanceTaxPageProps> = ({ can }) => {
  const [activeTab, setActiveTab] = useState<'plans' | 'installments' | 'interest'>('plans')
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Filters & State
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')
  const [summary, setSummary] = useState<AdvanceTaxSummaryDto | null>(null)
  const [plans, setPlans] = useState<AdvanceTaxPlanDto[]>([])
  const [selectedPlan, setSelectedPlan] = useState<AdvanceTaxPlanDto | null>(null)
  const [installments, setInstallments] = useState<AdvanceTaxInstallmentDto[]>([])
  const [interestComputations, setInterestComputations] = useState<TaxInterestComputationDto[]>([])

  // Modals
  const [showPlanModal, setShowPlanModal] = useState<boolean>(false)
  const [showPayModal, setShowPayModal] = useState<boolean>(false)
  const [showCalcModal, setShowCalcModal] = useState<boolean>(false)
  const [editingPlanId, setEditingPlanId] = useState<string | null>(null)
  const [selectedInstallment, setSelectedInstallment] = useState<AdvanceTaxInstallmentDto | null>(null)
  const [showAisModal, setShowAisModal] = useState<boolean>(false)
  const [aisPayload, setAisPayload] = useState<string>('')
  const [importingAis, setImportingAis] = useState<boolean>(false)

  // Plan Form State
  const [planForm, setPlanForm] = useState<SaveAdvanceTaxPlanRequest>({
    clientId: '',
    financialYear: '2024-25',
    assessmentYear: '2025-26',
    regime: 'NewRegime',
    isPresumptive: false,
    isSeniorCitizen: false,
    estimatedGrossIncome: 1200000,
    estimatedDeductions: 75000,
    estimatedTaxBeforeCredits: 140000,
    estimatedTdsTcs: 40000,
    otherReliefs: 0,
    notes: 'Advance Tax Assessment FY 2024-25'
  })

  // Payment Form State
  const [payForm, setPayForm] = useState<RecordInstallmentPaymentRequest>({
    amountPaid: 15000,
    challanBsrCode: '0210045',
    challanNumber: 'CH-2024-001',
    challanDate: new Date().toISOString().split('T')[0]
  })

  // Calculate Interest Form State
  const [calcForm, setCalcForm] = useState<CalculateStatutoryInterestRequest>({
    returnDueDate: '2025-07-31',
    actualReturnFilingDate: new Date().toISOString().split('T')[0],
    selfAssessmentTaxPaid: 0
  })

  useEffect(() => {
    loadClients()
  }, [])

  useEffect(() => {
    loadData(selectedClientId || undefined, selectedFy)
  }, [selectedClientId, selectedFy])

  const loadClients = async () => {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      if (res && res.items) {
        setClients(res.items)
        if (res.items.length > 0 && !selectedClientId) {
          setPlanForm(f => ({ ...f, clientId: res.items[0].id }))
        }
      }
    } catch {
      // Non-fatal
    }
  }

  const loadData = async (cId?: string, fy?: string) => {
    setLoading(true)
    setError(null)
    try {
      const [sum, planList] = await Promise.all([
        advanceTaxApi.getSummary(cId, fy),
        advanceTaxApi.getPlans(cId, fy)
      ])
      setSummary(sum)
      setPlans(planList)

      if (planList.length > 0) {
        const cur = selectedPlan ? planList.find(p => p.id === selectedPlan.id) || planList[0] : planList[0]
        setSelectedPlan(cur)
        await loadPlanDetails(cur.id)
      } else {
        setSelectedPlan(null)
        setInstallments([])
        setInterestComputations([])
      }
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to load advance tax data')
    } finally {
      setLoading(false)
    }
  }

  const loadPlanDetails = async (planId: string) => {
    try {
      const [instList, compList] = await Promise.all([
        advanceTaxApi.getInstallments(planId),
        advanceTaxApi.getInterestComputations(planId)
      ])
      setInstallments(instList)
      setInterestComputations(compList)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to load installment details')
    }
  }

  const handleSelectPlan = async (p: AdvanceTaxPlanDto) => {
    setSelectedPlan(p)
    await loadPlanDetails(p.id)
    setActiveTab('installments')
  }

  const handleOpenCreatePlan = () => {
    const curClient = clients.find(c => c.id === (selectedClientId || clients[0]?.id))
    setEditingPlanId(null)
    setPlanForm({
      clientId: curClient?.id || '',
      financialYear: selectedFy,
      assessmentYear: selectedFy === '2024-25' ? '2025-26' : selectedFy === '2023-24' ? '2024-25' : '2026-27',
      regime: 'NewRegime',
      isPresumptive: false,
      isSeniorCitizen: false,
      estimatedGrossIncome: 1200000,
      estimatedDeductions: 75000,
      estimatedTaxBeforeCredits: 140000,
      estimatedTdsTcs: 40000,
      otherReliefs: 0,
      notes: `Advance Tax Assessment for FY ${selectedFy}`
    })
    setShowPlanModal(true)
  }

  const handleOpenEditPlan = (p: AdvanceTaxPlanDto) => {
    setEditingPlanId(p.id)
    setPlanForm({
      clientId: p.clientId,
      financialYear: p.financialYear,
      assessmentYear: p.assessmentYear,
      regime: p.regime,
      isPresumptive: p.isPresumptive,
      isSeniorCitizen: p.isSeniorCitizen,
      estimatedGrossIncome: p.estimatedGrossIncome,
      estimatedDeductions: p.estimatedDeductions,
      estimatedTaxBeforeCredits: p.estimatedTaxBeforeCredits,
      estimatedTdsTcs: p.estimatedTdsTcs,
      otherReliefs: p.otherReliefs,
      notes: p.notes || ''
    })
    setShowPlanModal(true)
  }

  const handleSavePlan = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      const payload: SaveAdvanceTaxPlanRequest = {
        ...planForm,
        regime: (planForm.regime === 'OldRegime' || (planForm.regime as unknown) === 2 ? 2 : 1) as AdvanceTaxRegime
      }
      if (editingPlanId) {
        await advanceTaxApi.updatePlan(editingPlanId, payload)
        setSuccess('Advance tax plan updated successfully!')
      } else {
        const created = await advanceTaxApi.createPlan(payload)
        setSuccess(`Advance tax plan created! Assessed tax: ${formatCurrency(created.assessedTax)}`)
      }
      setShowPlanModal(false)
      loadData(selectedClientId || undefined, selectedFy)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to save advance tax plan')
    }
  }

  const handleOpenPay = (inst: AdvanceTaxInstallmentDto) => {
    setSelectedInstallment(inst)
    setPayForm({
      amountPaid: inst.amountPaid > 0 ? inst.amountPaid : inst.cumulativeTargetAmount,
      challanBsrCode: inst.challanBsrCode || '0210045',
      challanNumber: inst.challanNumber || 'CH-2024-' + Math.floor(100 + Math.random() * 900),
      challanDate: inst.challanDate || new Date().toISOString().split('T')[0]
    })
    setShowPayModal(true)
  }

  const handleSubmitPayment = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedInstallment || !selectedPlan) return
    setError(null)
    setSuccess(null)
    try {
      await advanceTaxApi.recordPayment(selectedInstallment.id, payForm)
      setSuccess(`Challan payment of ${formatCurrency(payForm.amountPaid)} recorded for Installment #${selectedInstallment.installmentNumber}!`)
      setShowPayModal(false)
      await loadData(selectedClientId || undefined, selectedFy)
      if (selectedPlan) await loadPlanDetails(selectedPlan.id)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to record installment payment')
    }
  }

  const handleImportAis = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedPlan) {
      setError('Please select an Advance Tax Plan first.')
      return
    }
    if (!aisPayload.trim()) {
      setError('Please provide AIS JSON content.')
      return
    }
    try {
      setImportingAis(true)
      setError(null)
      const updated = await advanceTaxApi.importAis(selectedPlan.id, aisPayload)
      setSuccess(`AIS successfully imported into plan! Assessed Tax: ${formatCurrency(updated.assessedTax)}, Credits: ${formatCurrency(updated.estimatedTdsTcs)}.`)
      setShowAisModal(false)
      setAisPayload('')
      await loadData(selectedClientId || undefined, selectedFy)
      await loadPlanDetails(updated.id)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to import AIS into Advance Tax Plan')
    } finally {
      setImportingAis(false)
    }
  }

  const handleOpenCalcInterest = () => {
    if (!selectedPlan) return
    setCalcForm({
      returnDueDate: selectedPlan.isPresumptive ? '2025-07-31' : '2025-07-31',
      actualReturnFilingDate: new Date().toISOString().split('T')[0],
      selfAssessmentTaxPaid: 0
    })
    setShowCalcModal(true)
  }

  const handleSubmitCalcInterest = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedPlan) return
    setError(null)
    setSuccess(null)
    try {
      const comp = await advanceTaxApi.calculateInterest(selectedPlan.id, calcForm)
      setSuccess(`Statutory interest evaluated: ₹${comp.totalStatutoryInterest} (234C: ₹${comp.interest234cTotal}, 234B: ₹${comp.interest234b}, 234A: ₹${comp.interest234a})`)
      setShowCalcModal(false)
      await loadData(selectedClientId || undefined, selectedFy)
      if (selectedPlan) await loadPlanDetails(selectedPlan.id)
      setActiveTab('interest')
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to calculate statutory interest')
    }
  }

  // Estimated assessed tax preview in plan form
  const estimatedTaxablePreview = Math.max(0, Number(planForm.estimatedGrossIncome) - Number(planForm.estimatedDeductions))
  const assessedTaxPreview = Math.max(0, Number(planForm.estimatedTaxBeforeCredits) - Number(planForm.estimatedTdsTcs) - Number(planForm.otherReliefs))

  return (
    <div style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto' }}>
      {/* Top Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '26px', fontWeight: 700, color: '#1a237e' }}>
            Advance Tax Planner & Section 234A/B/C Interest Desk
          </h1>
          <p style={{ margin: '4px 0 0 0', color: '#555', fontSize: '14px' }}>
            Quarterly statutory installments (June 15, Sept 15, Dec 15, March 15), Sec 208 threshold (₹10,000), safe harbors, and Sections 234A/B/C interest engine.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '12px', alignItems: 'center', flexWrap: 'wrap' }}>
          {/* Client Filter */}
          <select
            value={selectedClientId}
            onChange={e => setSelectedClientId(e.target.value)}
            style={{
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #ccc',
              fontSize: '14px',
              background: '#fff',
              minWidth: '200px'
            }}
          >
            <option value="">All Clients</option>
            {clients.map(c => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.code})
              </option>
            ))}
          </select>

          {/* FY Filter */}
          <select
            value={selectedFy}
            onChange={e => setSelectedFy(e.target.value)}
            style={{
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #ccc',
              fontSize: '14px',
              background: '#fff',
              fontWeight: 600
            }}
          >
            <option value="2024-25">FY 2024-25</option>
            <option value="2023-24">FY 2023-24</option>
            <option value="2025-26">FY 2025-26</option>
          </select>

          <button
            onClick={() => loadData(selectedClientId || undefined, selectedFy)}
            disabled={loading}
            style={{
              padding: '8px 14px',
              borderRadius: '6px',
              border: '1px solid #1a237e',
              background: '#fff',
              color: '#1a237e',
              fontWeight: 600,
              cursor: 'pointer'
            }}
          >
            {loading ? 'Refreshing...' : '↻ Refresh'}
          </button>

          {(!can || can('advancetax.manage')) && (
            <>
              <button
                onClick={() => setShowAisModal(true)}
                disabled={!selectedPlan}
                style={{
                  padding: '8px 16px',
                  borderRadius: '6px',
                  border: '1px solid #4338ca',
                  background: selectedPlan ? '#EEF2FF' : '#F3F4F6',
                  color: selectedPlan ? '#4338ca' : '#9CA3AF',
                  fontWeight: 600,
                  cursor: selectedPlan ? 'pointer' : 'not-allowed',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px'
                }}
              >
                <span>📥</span> Import AIS to Plan
              </button>
              <button
                onClick={handleOpenCreatePlan}
                style={{
                  padding: '8px 16px',
                  borderRadius: '6px',
                  border: 'none',
                  background: '#1a237e',
                  color: '#fff',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px'
                }}
              >
                <span>+</span> New Advance Tax Plan
              </button>
            </>
          )}

          {selectedPlan && (!can || can('advancetax.calculate')) && (
            <button
              onClick={handleOpenCalcInterest}
              style={{
                padding: '8px 16px',
                borderRadius: '6px',
                border: 'none',
                background: '#c62828',
                color: '#fff',
                fontWeight: 600,
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                gap: '6px'
              }}
            >
              <span>⚡</span> Run 234A/B/C Calculator
            </button>
          )}
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div style={{
          padding: '12px 16px',
          background: '#ffebee',
          color: '#c62828',
          borderRadius: '6px',
          marginBottom: '20px',
          border: '1px solid #ffcdd2',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span><strong>Error:</strong> {error}</span>
          <button onClick={() => setError(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#c62828', fontWeight: 'bold' }}>✕</button>
        </div>
      )}

      {success && (
        <div style={{
          padding: '12px 16px',
          background: '#e8f5e9',
          color: '#2e7d32',
          borderRadius: '6px',
          marginBottom: '20px',
          border: '1px solid #c8e6c9',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span><strong>Success:</strong> {success}</span>
          <button onClick={() => setSuccess(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#2e7d32', fontWeight: 'bold' }}>✕</button>
        </div>
      )}

      {/* Metric Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #1a237e' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Total Plans & Income</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#1a237e', marginTop: '4px' }}>{summary.totalPlans} Plans</div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Est Income: <strong>{formatCurrency(summary.totalEstimatedIncome)}</strong>
            </div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #00838f' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Total Assessed Tax</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#00838f', marginTop: '4px' }}>
              {formatCurrency(summary.totalAssessedTax)}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Net Advance Tax Liability
            </div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #2e7d32' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Advance Tax Collected</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#2e7d32', marginTop: '4px' }}>
              {formatCurrency(summary.totalAdvanceTaxCollected)}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Challan Payments Made
            </div>
          </div>

          <div style={{
            background: '#fff',
            padding: '16px',
            borderRadius: '8px',
            boxShadow: '0 2px 4px rgba(0,0,0,0.05)',
            borderLeft: summary.totalShortfall > 0 ? '4px solid #f57f17' : '4px solid #43a047'
          }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Installment Shortfall</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: summary.totalShortfall > 0 ? '#f57f17' : '#43a047', marginTop: '4px' }}>
              {formatCurrency(summary.totalShortfall)}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Cumulative Deficit
            </div>
          </div>

          <div style={{
            background: '#fff',
            padding: '16px',
            borderRadius: '8px',
            boxShadow: '0 2px 4px rgba(0,0,0,0.05)',
            borderLeft: summary.totalInterest234c > 0 ? '4px solid #c62828' : '4px solid #2e7d32'
          }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Sec 234C Interest</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: summary.totalInterest234c > 0 ? '#c62828' : '#2e7d32', marginTop: '4px' }}>
              {formatCurrency(summary.totalInterest234c)}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Installment Deferment Penalty
            </div>
          </div>

          <div style={{
            background: '#fff',
            padding: '16px',
            borderRadius: '8px',
            boxShadow: '0 2px 4px rgba(0,0,0,0.05)',
            borderLeft: summary.totalInterest234b > 0 ? '4px solid #d32f2f' : '4px solid #2e7d32'
          }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Sec 234B Interest</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: summary.totalInterest234b > 0 ? '#d32f2f' : '#2e7d32', marginTop: '4px' }}>
              {formatCurrency(summary.totalInterest234b)}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Shortfall (&lt;90%) Penalty
            </div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '2px solid #e0e0e0', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('plans')}
          style={{
            padding: '12px 24px',
            fontSize: '15px',
            fontWeight: 600,
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'plans' ? '3px solid #1a237e' : 'none',
            color: activeTab === 'plans' ? '#1a237e' : '#666',
            cursor: 'pointer'
          }}
        >
          📄 Advance Tax Plans & Estimates ({plans.length})
        </button>

        <button
          onClick={() => setActiveTab('installments')}
          style={{
            padding: '12px 24px',
            fontSize: '15px',
            fontWeight: 600,
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'installments' ? '3px solid #1a237e' : 'none',
            color: activeTab === 'installments' ? '#1a237e' : '#666',
            cursor: 'pointer'
          }}
        >
          📅 Quarterly Installments & ITNS 280 ({selectedPlan ? installments.length : 0})
        </button>

        <button
          onClick={() => setActiveTab('interest')}
          style={{
            padding: '12px 24px',
            fontSize: '15px',
            fontWeight: 600,
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'interest' ? '3px solid #1a237e' : 'none',
            color: activeTab === 'interest' ? '#1a237e' : '#666',
            cursor: 'pointer'
          }}
        >
          ⚖️ Section 234A/B/C Interest Assessment ({interestComputations.length})
        </button>
      </div>

      {/* TAB 1: Advance Tax Plans & Estimates */}
      {activeTab === 'plans' && (
        <div style={{ background: '#fff', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid #eee', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>Active Advance Tax Estimates</h3>
            <span style={{ fontSize: '12px', color: '#666' }}>Showing {plans.length} advance tax plans</span>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f8f9fa', color: '#555', borderBottom: '2px solid #eee' }}>
                  <th style={{ padding: '12px' }}>Client Details</th>
                  <th style={{ padding: '12px' }}>FY / AY</th>
                  <th style={{ padding: '12px' }}>Tax Regime</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Est. Gross Income</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>TDS/TCS Credit</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Assessed Tax</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Advance Tax Paid</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>234C Interest</th>
                  <th style={{ padding: '12px' }}>Status</th>
                  <th style={{ padding: '12px', textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {plans.length === 0 ? (
                  <tr>
                    <td colSpan={10} style={{ padding: '32px', textAlign: 'center', color: '#999' }}>
                      No advance tax plans found. Click "+ New Advance Tax Plan" to estimate tax liability and track quarterly challans.
                    </td>
                  </tr>
                ) : (
                  plans.map(p => {
                    const isSelected = selectedPlan?.id === p.id
                    const isPresumptive = p.isPresumptive

                    return (
                      <tr key={p.id} style={{ borderBottom: '1px solid #eee', background: isSelected ? '#f0f4ff' : '#fff' }}>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 700, color: '#1a237e' }}>{p.clientName}</div>
                          <div style={{ fontSize: '11px', color: '#666' }}>Code: {p.clientCode}</div>
                          {p.isSeniorCitizen && (
                            <span style={{ fontSize: '10px', background: '#e3f2fd', color: '#1565c0', padding: '1px 5px', borderRadius: '3px' }}>
                              Senior Citizen
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 600 }}>FY: {p.financialYear}</div>
                          <div style={{ fontSize: '11px', color: '#666' }}>AY: {p.assessmentYear}</div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          <span style={{
                            padding: '2px 6px',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 600,
                            background: p.regime === 'NewRegime' ? '#e8f5e9' : '#ede7f6',
                            color: p.regime === 'NewRegime' ? '#2e7d32' : '#4527a0'
                          }}>
                            {p.regime === 'NewRegime' ? 'New 115BAC' : 'Old Regime'}
                          </span>
                          {isPresumptive && (
                            <div style={{ fontSize: '10px', color: '#d84315', fontWeight: 600, marginTop: '2px' }}>
                              Sec 44AD/ADA (March 15)
                            </div>
                          )}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600 }}>
                          {formatCurrency(p.estimatedGrossIncome)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', color: '#00838f', fontWeight: 600 }}>
                          {formatCurrency(p.estimatedTdsTcs)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: '#1a237e' }}>
                          {formatCurrency(p.assessedTax)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#2e7d32' }}>
                          {formatCurrency(p.totalAdvanceTaxPaid)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: p.totalInterest234c > 0 ? '#c62828' : '#2e7d32' }}>
                          {formatCurrency(p.totalInterest234c)}
                        </td>
                        <td style={{ padding: '12px' }}>
                          <span style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 700,
                            background: p.status === 'Active' ? '#e8f5e9' : '#fff3e0',
                            color: p.status === 'Active' ? '#2e7d32' : '#e65100'
                          }}>
                            {p.status}
                          </span>
                        </td>
                        <td style={{ padding: '12px', textAlign: 'center' }}>
                          <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                            <button
                              onClick={() => handleSelectPlan(p)}
                              title="View Installments & Challans"
                              style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                border: '1px solid #1a237e',
                                background: '#e8eaf6',
                                color: '#1a237e',
                                fontSize: '11px',
                                fontWeight: 600,
                                cursor: 'pointer'
                              }}
                            >
                              Installments
                            </button>

                            {(!can || can('advancetax.manage')) && (
                              <button
                                onClick={() => handleOpenEditPlan(p)}
                                title="Edit Estimate"
                                style={{
                                  padding: '4px 8px',
                                  borderRadius: '4px',
                                  border: '1px solid #555',
                                  background: '#fff',
                                  color: '#333',
                                  fontSize: '11px',
                                  fontWeight: 600,
                                  cursor: 'pointer'
                                }}
                              >
                                Edit
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 2: Quarterly Installment Schedule & ITNS 280 */}
      {activeTab === 'installments' && (
        <div style={{ background: '#fff', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid #eee', display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '12px' }}>
            <div>
              <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>
                {selectedPlan ? `Quarterly Installments for ${selectedPlan.clientName} (FY ${selectedPlan.financialYear})` : 'Select a Plan'}
              </h3>
              <p style={{ margin: '2px 0 0 0', fontSize: '12px', color: '#666' }}>
                Section 211 statutory milestones. Section 234C interest applies at 1% per month for 3 months (1 month for March).
              </p>
            </div>

            {selectedPlan && (
              <div style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
                <span style={{ fontSize: '13px' }}>
                  Assessed Tax: <strong style={{ color: '#1a237e' }}>{formatCurrency(selectedPlan.assessedTax)}</strong>
                </span>
                <span style={{ fontSize: '13px' }}>
                  Paid: <strong style={{ color: '#2e7d32' }}>{formatCurrency(selectedPlan.totalAdvanceTaxPaid)}</strong>
                </span>
              </div>
            )}
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f8f9fa', color: '#555', borderBottom: '2px solid #eee' }}>
                  <th style={{ padding: '12px' }}>Installment</th>
                  <th style={{ padding: '12px' }}>Statutory Due Date</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Target %</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Cumulative Target</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Amount Paid</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Shortfall</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>234C Interest</th>
                  <th style={{ padding: '12px' }}>ITNS 280 Challan Details</th>
                  <th style={{ padding: '12px', textAlign: 'center' }}>Action</th>
                </tr>
              </thead>
              <tbody>
                {!selectedPlan || installments.length === 0 ? (
                  <tr>
                    <td colSpan={9} style={{ padding: '32px', textAlign: 'center', color: '#999' }}>
                      Please select an advance tax plan from the first tab to view its quarterly installment schedule.
                    </td>
                  </tr>
                ) : (
                  installments.map(inst => {
                    const isDeficit = inst.shortfallAmount > 0
                    const isPaid = inst.isPaid

                    return (
                      <tr key={inst.id} style={{ borderBottom: '1px solid #eee', background: isDeficit ? '#fffdf7' : '#fff' }}>
                        <td style={{ padding: '12px', fontWeight: 700, color: '#1a237e' }}>
                          Installment #{inst.installmentNumber}
                        </td>
                        <td style={{ padding: '12px', fontWeight: 600 }}>
                          {inst.dueDate}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600 }}>
                          {inst.targetPercentage}%
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#1a237e' }}>
                          {formatCurrency(inst.cumulativeTargetAmount)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: '#2e7d32' }}>
                          {formatCurrency(inst.amountPaid)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: isDeficit ? '#f57f17' : '#666' }}>
                          {formatCurrency(inst.shortfallAmount)}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: inst.interest234c > 0 ? '#c62828' : '#2e7d32' }}>
                          {formatCurrency(inst.interest234c)}
                        </td>
                        <td style={{ padding: '12px' }}>
                          {isPaid ? (
                            <div>
                              <div style={{ fontWeight: 600, fontFamily: 'monospace', fontSize: '12px' }}>
                                Challan: {inst.challanNumber || '-'}
                              </div>
                              <div style={{ fontSize: '11px', color: '#666' }}>
                                BSR: {inst.challanBsrCode || '-'} | Date: {inst.challanDate || '-'}
                              </div>
                            </div>
                          ) : (
                            <span style={{ color: '#999', fontSize: '11px' }}>Unpaid / Pending Challan</span>
                          )}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'center' }}>
                          {(!can || can('advancetax.manage')) && (
                            <button
                              onClick={() => handleOpenPay(inst)}
                              style={{
                                padding: '4px 10px',
                                borderRadius: '4px',
                                border: '1px solid #2e7d32',
                                background: '#e8f5e9',
                                color: '#2e7d32',
                                fontSize: '11px',
                                fontWeight: 600,
                                cursor: 'pointer'
                              }}
                            >
                              {isPaid ? 'Edit Challan' : '+ Record Challan'}
                            </button>
                          )}
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 3: Section 234A / 234B / 234C Interest Assessment */}
      {activeTab === 'interest' && (
        <div style={{ background: '#fff', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid #eee', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>
                Section 234A, 234B & 234C Statutory Interest Calculations
              </h3>
              <p style={{ margin: '2px 0 0 0', fontSize: '12px', color: '#666' }}>
                Evaluates delay in filing (234A), shortfall in advance tax &lt; 90% (234B), and installment deferment (234C).
              </p>
            </div>

            {selectedPlan && (!can || can('advancetax.calculate')) && (
              <button
                onClick={handleOpenCalcInterest}
                style={{
                  padding: '6px 14px',
                  borderRadius: '4px',
                  border: 'none',
                  background: '#c62828',
                  color: '#fff',
                  fontSize: '12px',
                  fontWeight: 600,
                  cursor: 'pointer'
                }}
              >
                ⚡ Calculate Interest
              </button>
            )}
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f8f9fa', color: '#555', borderBottom: '2px solid #eee' }}>
                  <th style={{ padding: '12px' }}>Assessment Year</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Assessed Tax</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Advance Tax Paid</th>
                  <th style={{ padding: '12px' }}>Return Dates</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Sec 234C (Deferment)</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Sec 234B (Shortfall &lt;90%)</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Sec 234A (Late Filing)</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Total Interest</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Net Payable</th>
                  <th style={{ padding: '12px' }}>Computed At</th>
                </tr>
              </thead>
              <tbody>
                {interestComputations.length === 0 ? (
                  <tr>
                    <td colSpan={10} style={{ padding: '32px', textAlign: 'center', color: '#999' }}>
                      No statutory interest calculations run yet. Select a plan and click "⚡ Calculate Interest".
                    </td>
                  </tr>
                ) : (
                  interestComputations.map(c => (
                    <tr key={c.id} style={{ borderBottom: '1px solid #eee' }}>
                      <td style={{ padding: '12px', fontWeight: 700, color: '#1a237e' }}>{c.assessmentYear}</td>
                      <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600 }}>{formatCurrency(c.assessedTax)}</td>
                      <td style={{ padding: '12px', textAlign: 'right', color: '#2e7d32', fontWeight: 600 }}>
                        {formatCurrency(c.advanceTaxPaid)}
                      </td>
                      <td style={{ padding: '12px' }}>
                        <div style={{ fontSize: '12px' }}>Due: {c.returnDueDate}</div>
                        <div style={{ fontSize: '11px', color: '#666' }}>Filed: {c.actualReturnFilingDate}</div>
                      </td>
                      <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#c62828' }}>
                        {formatCurrency(c.interest234cTotal)}
                      </td>
                      <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#c62828' }}>
                        {formatCurrency(c.interest234b)}
                        <div style={{ fontSize: '10px', color: '#777' }}>
                          {c.is234bApplicable ? `${c.monthsDelayed234b} mo @ 1%` : 'Exempt'}
                        </div>
                      </td>
                      <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600, color: '#c62828' }}>
                        {formatCurrency(c.interest234a)}
                        <div style={{ fontSize: '10px', color: '#777' }}>
                          {c.monthsDelayed234a > 0 ? `${c.monthsDelayed234a} mo @ 1%` : 'No delay'}
                        </div>
                      </td>
                      <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: '#c62828' }}>
                        {formatCurrency(c.totalStatutoryInterest)}
                      </td>
                      <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: '#1a237e' }}>
                        {formatCurrency(c.netTaxPayable)}
                      </td>
                      <td style={{ padding: '12px', fontSize: '11px', color: '#666' }}>
                        {new Date(c.computedAt).toLocaleString('en-IN')}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* MODAL 1: Create / Edit Advance Tax Plan */}
      {showPlanModal && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '640px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#1a237e' }}>
                  {editingPlanId ? 'Edit Advance Tax Plan' : 'Create Advance Tax Estimation Plan'}
                </h2>
                <div style={{ fontSize: '12px', color: '#666' }}>Income estimation & statutory installment setup</div>
              </div>
              <button onClick={() => setShowPlanModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSavePlan}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Client</label>
                  <select
                    value={planForm.clientId}
                    onChange={e => setPlanForm({ ...planForm, clientId: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Financial Year / AY</label>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <select
                      value={planForm.financialYear}
                      onChange={e => {
                        const fy = e.target.value
                        const ay = fy === '2024-25' ? '2025-26' : fy === '2023-24' ? '2024-25' : '2026-27'
                        setPlanForm({ ...planForm, financialYear: fy, assessmentYear: ay })
                      }}
                      style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                    >
                      <option value="2024-25">FY 2024-25</option>
                      <option value="2023-24">FY 2023-24</option>
                      <option value="2025-26">FY 2025-26</option>
                    </select>
                    <input
                      type="text"
                      readOnly
                      value={planForm.assessmentYear}
                      style={{ width: '90px', padding: '8px', borderRadius: '4px', border: '1px solid #eee', background: '#f5f5f5', textAlign: 'center' }}
                    />
                  </div>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Tax Regime</label>
                  <select
                    value={planForm.regime}
                    onChange={e => setPlanForm({ ...planForm, regime: e.target.value as (typeof planForm)['regime'] })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value="NewRegime">New Tax Regime (Section 115BAC)</option>
                    <option value="OldRegime">Old Tax Regime</option>
                  </select>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'center', gap: '6px' }}>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}>
                    <input
                      type="checkbox"
                      checked={planForm.isPresumptive}
                      onChange={e => setPlanForm({ ...planForm, isPresumptive: e.target.checked })}
                    />
                    Section 44AD / 44ADA (Single March 15 Installment)
                  </label>
                  <label style={{ display: 'flex', alignItems: 'center', gap: '6px', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}>
                    <input
                      type="checkbox"
                      checked={planForm.isSeniorCitizen}
                      onChange={e => setPlanForm({ ...planForm, isSeniorCitizen: e.target.checked })}
                    />
                    Senior Citizen u/s 207(2) (No Business Income)
                  </label>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Estimated Gross Total Income (₹)</label>
                  <input
                    type="number"
                    value={planForm.estimatedGrossIncome}
                    onChange={e => setPlanForm({ ...planForm, estimatedGrossIncome: parseFloat(e.target.value) || 0 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Estimated Deductions (₹)</label>
                  <input
                    type="number"
                    value={planForm.estimatedDeductions}
                    onChange={e => setPlanForm({ ...planForm, estimatedDeductions: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Estimated Tax Before Credits (₹)</label>
                  <input
                    type="number"
                    value={planForm.estimatedTaxBeforeCredits}
                    onChange={e => setPlanForm({ ...planForm, estimatedTaxBeforeCredits: parseFloat(e.target.value) || 0 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Estimated TDS / TCS Credit (₹)</label>
                  <input
                    type="number"
                    value={planForm.estimatedTdsTcs}
                    onChange={e => setPlanForm({ ...planForm, estimatedTdsTcs: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div style={{ gridColumn: 'span 2' }}>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Notes / Remarks</label>
                  <input
                    type="text"
                    value={planForm.notes || ''}
                    onChange={e => setPlanForm({ ...planForm, notes: e.target.value })}
                    placeholder="e.g. Projected turnover u/s 44AB, expected capital gains in Q3"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              {/* Assessed Tax Summary Banner */}
              <div style={{ background: '#f5f5f5', padding: '12px', borderRadius: '6px', marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px' }}>
                  <span>Estimated Taxable Income:</span>
                  <strong>{formatCurrency(estimatedTaxablePreview)}</strong>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '14px', fontWeight: 700, marginTop: '4px' }}>
                  <span>Net Assessed Advance Tax Liability:</span>
                  <span style={{ color: '#1a237e' }}>{formatCurrency(assessedTaxPreview)}</span>
                </div>
                {assessedTaxPreview < 10000 && (
                  <div style={{ fontSize: '11px', color: '#2e7d32', marginTop: '4px' }}>
                    ✓ Assessed tax is under ₹10,000 threshold (Section 208). Advance tax is optional / not mandatory.
                  </div>
                )}
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowPlanModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#1a237e', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Saving...' : editingPlanId ? 'Update Plan' : 'Generate Plan & Installments'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: Record Challan Payment */}
      {showPayModal && selectedInstallment && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '480px',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#2e7d32' }}>Record ITNS 280 Advance Tax Challan</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>Installment #{selectedInstallment.installmentNumber} (Due: {selectedInstallment.dueDate})</div>
              </div>
              <button onClick={() => setShowPayModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSubmitPayment}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Amount Paid (₹)</label>
                <input
                  type="number"
                  step="0.01"
                  value={payForm.amountPaid}
                  onChange={e => setPayForm({ ...payForm, amountPaid: parseFloat(e.target.value) || 0 })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontSize: '16px', fontWeight: 600 }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Challan BSR Code (7 Digits)</label>
                <input
                  type="text"
                  maxLength={7}
                  value={payForm.challanBsrCode || ''}
                  onChange={e => setPayForm({ ...payForm, challanBsrCode: e.target.value })}
                  placeholder="e.g. 0210045"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontFamily: 'monospace' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Challan Identification Number (CIN) / Serial No.</label>
                <input
                  type="text"
                  value={payForm.challanNumber || ''}
                  onChange={e => setPayForm({ ...payForm, challanNumber: e.target.value })}
                  placeholder="e.g. CH-2024-001"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontFamily: 'monospace' }}
                />
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Challan Payment Date</label>
                <input
                  type="date"
                  value={payForm.challanDate || ''}
                  onChange={e => setPayForm({ ...payForm, challanDate: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowPayModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#2e7d32', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Saving...' : 'Record Payment'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: Calculate Statutory Interest */}
      {showCalcModal && selectedPlan && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '520px',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#c62828' }}>Section 234A, 234B & 234C Calculator</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>{selectedPlan.clientName} ({selectedPlan.assessmentYear})</div>
              </div>
              <button onClick={() => setShowCalcModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSubmitCalcInterest}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>ITR Statutory Due Date (Section 139(1))</label>
                <input
                  type="date"
                  value={calcForm.returnDueDate}
                  onChange={e => setCalcForm({ ...calcForm, returnDueDate: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
                <div style={{ fontSize: '11px', color: '#777', marginTop: '2px' }}>
                  Default is 31st July (non-audit) or 31st October (tax audit)
                </div>
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Actual / Projected Return Filing Date</label>
                <input
                  type="date"
                  value={calcForm.actualReturnFilingDate}
                  onChange={e => setCalcForm({ ...calcForm, actualReturnFilingDate: e.target.value })}
                  required
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Self-Assessment Tax Paid Before Due Date (₹)</label>
                <input
                  type="number"
                  step="0.01"
                  value={calcForm.selfAssessmentTaxPaid}
                  onChange={e => setCalcForm({ ...calcForm, selfAssessmentTaxPaid: parseFloat(e.target.value) || 0 })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ background: '#f5f5f5', padding: '12px', borderRadius: '6px', marginBottom: '16px' }}>
                <div style={{ fontSize: '12px', color: '#333' }}>
                  Assessed Tax: <strong>{formatCurrency(selectedPlan.assessedTax)}</strong> | Advance Tax Paid: <strong>{formatCurrency(selectedPlan.totalAdvanceTaxPaid)}</strong>
                </div>
                <div style={{ fontSize: '11px', color: '#666', marginTop: '2px' }}>
                  Section 234B applies if advance tax is &lt; 90% ({formatCurrency(selectedPlan.assessedTax * 0.9)}).
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowCalcModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#c62828', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Evaluating...' : 'Compute Statutory Interest'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* AIS Import to Advance Tax Plan Modal */}
      {showAisModal && selectedPlan && (
        <div style={{ position: 'fixed', top: 0, left: 0, right: 0, bottom: 0, background: 'rgba(0,0,0,0.5)', display: 'flex', justifyContent: 'center', alignItems: 'center', zIndex: 1000 }}>
          <div style={{ background: '#FFFFFF', padding: '24px', borderRadius: '12px', width: '600px', maxWidth: '95%' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h3 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: '#111827' }}>
                Import AIS / 26AS JSON to Advance Tax Plan
              </h3>
              <button onClick={() => setShowAisModal(false)} style={{ background: 'transparent', border: 'none', fontSize: '18px', cursor: 'pointer' }}>✕</button>
            </div>

            <p style={{ margin: '0 0 16px 0', fontSize: '13px', color: '#555' }}>
              Target Plan: <strong>{selectedPlan.clientName} ({selectedPlan.financialYear})</strong>. Paste the CBDT AIS JSON to update estimated income, TDS credits, and advance tax challans directly from the income-tax portal.
            </p>

            <form onSubmit={handleImportAis}>
              <div style={{ marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: '#333' }}>AIS JSON Content</label>
                  <button
                    type="button"
                    onClick={() => setAisPayload(`{
  "financialYear": "${selectedPlan.financialYear}",
  "pan": "AAACB1234F",
  "salary": [{ "grossSalary": 2400000, "tds": 280000 }],
  "interest": [{ "amount": 75000, "tds": 5000 }],
  "taxPayments": [{ "minorHead": "Advance Tax (100)", "bsrCode": "0210045", "challanNumber": "88192", "tenderDate": "2024-06-14", "amount": 50000 }]
}`)}
                    style={{ background: 'transparent', border: 'none', color: '#1a237e', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
                  >
                    Load Sample JSON
                  </button>
                </div>
                <textarea
                  rows={8}
                  value={aisPayload}
                  onChange={e => setAisPayload(e.target.value)}
                  placeholder="Paste AIS JSON here..."
                  style={{ width: '100%', padding: '10px', borderRadius: '4px', border: '1px solid #ccc', fontSize: '12px', fontFamily: 'monospace' }}
                  required
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowAisModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={importingAis}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#1a237e', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {importingAis ? 'Importing...' : 'Import to Plan'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
