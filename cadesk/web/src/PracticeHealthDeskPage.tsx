import { useState, useEffect, useMemo, useCallback, type FormEvent } from 'react'
import {
  practiceHealthApi,
  errorMessage,
  api,
  type FirmTelemetryDto,
  type PracticeValuationDto,
  type CalculateValuationRequest,
  type PracticeValuationResultDto,
  type SaveValuationRequest,
  type PartnerEquityDto,
  type SavePartnerEquityRequest,
  type PartnerRetirementPlanDto,
  type CreateRetirementPlanRequest,
  type ClientSuccessionTransitionDto,
  type SaveClientTransitionRequest,
  type PracticeBenchmarkDto,
  type GenerateBenchmarkRequest,
  type ValuationMethod,
  type PartnerVestingStatus,
  type PartnerTransitionRole,
  type TransitionStage,
  type ClientRetentionRisk,
  type FirmTierBenchmark,
  type Client,
  type UserProfile
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

export function PracticeHealthDeskPage() {
  const [activeTab, setActiveTab] = useState<'valuation' | 'succession' | 'benchmarks'>('valuation')
  const [loading, setLoading] = useState(true)
  const [notice, setNotice] = useState<string | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  // Telemetry
  const [telemetry, setTelemetry] = useState<FirmTelemetryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [users, setUsers] = useState<UserProfile[]>([])

  // Valuation State
  const [valuations, setValuations] = useState<PracticeValuationDto[]>([])
  const [selectedValuation, setSelectedValuation] = useState<PracticeValuationDto | null>(null)
  const [calcForm, setCalcForm] = useState<CalculateValuationRequest>({
    annualGrossRevenue: 18000000, // ₹1.8 Cr
    recurringComplianceRevenue: 13000000, // 72%
    advisoryAndOneOffRevenue: 5000000,
    ebitda: 5200000, // ~29%
    partnerNormalizedCompensation: 3600000,
    netWorkingCapital: 2500000,
    netTangibleAssets: 3500000,
    projectedRevenueGrowthRate: 0.12,
    discountRateWacc: 0.14,
    terminalGrowthRate: 0.05,
    forecastYears: 5,
    revenueMultipleBase: 1.35,
    ebitdaMultipleBase: 5.50,
    auditFranchiseStickinessPremiumPct: 0.10,
    partnerConcentrationDiscountPct: 0.10,
    recurringRevenuePremiumPct: 0.15,
    method: 'BlendedWeight'
  })
  const [liveValuationResult, setLiveValuationResult] = useState<PracticeValuationResultDto | null>(null)
  const [valNotes, setValNotes] = useState('')
  const [valScenarioName, setValScenarioName] = useState('FY 2024-25 Strategic Valuation')

  // Partner Equity State
  const [equities, setEquities] = useState<PartnerEquityDto[]>([])
  const [showEquityModal, setShowEquityModal] = useState(false)
  const [equityForm, setEquityForm] = useState<SavePartnerEquityRequest>({
    userId: '',
    partnerName: '',
    partnerEmail: '',
    partnerMembershipNo: '',
    equityPercentage: 25.0,
    capitalAccountBalance: 3000000,
    joiningDate: '2018-04-01',
    targetRetirementDate: '2030-03-31',
    vestingStatus: 'FullyVested',
    vestedPercentage: 100.0,
    annualDrawShare: 2400000,
    role: 'SeniorPartner'
  })

  // Retirement State
  const [retirementPlans, setRetirementPlans] = useState<PartnerRetirementPlanDto[]>([])
  const [showRetirementModal, setShowRetirementModal] = useState(false)
  const [retirementForm, setRetirementForm] = useState<CreateRetirementPlanRequest>({
    partnerEquityId: '',
    retirementDate: '2026-03-31',
    agreedFirmValuationAtRetirement: 25000000,
    capitalAccountRefundAmount: 3500000,
    payoutPeriodYears: 5,
    annualInterestRatePct: 8.0,
    partnershipNotes: 'Goodwill buyout agreed by majority deed resolution'
  })

  // Client Transitions
  const [transitions, setTransitions] = useState<ClientSuccessionTransitionDto[]>([])
  const [showTransitionModal, setShowTransitionModal] = useState(false)
  const [transitionForm, setTransitionForm] = useState<SaveClientTransitionRequest>({
    retiringPartnerId: '',
    successorPartnerId: '',
    clientId: '',
    annualClientFees: 450000,
    stage: 'Identified',
    risk: 'Low',
    targetHandoverDate: '2025-12-31',
    handoverNotes: 'Statutory audit client for 6 years; introduction planned post-AGM'
  })

  // Benchmarking State
  const [benchmarks, setBenchmarks] = useState<PracticeBenchmarkDto[]>([])
  const [selectedTier, setSelectedTier] = useState<FirmTierBenchmark>('Tier2MidSizedMultiPartner')
  const [activeBenchmark, setActiveBenchmark] = useState<PracticeBenchmarkDto | null>(null)

  // Load Initial Data
  useEffect(() => {
    loadAllData()
  }, [])

  const loadAllData = async () => {
    setLoading(true)
    setErrorMsg(null)
    try {
      const [tele, valList, eqList, retList, transList, bmList, clientsRes, usersRes] = await Promise.all([
        practiceHealthApi.getTelemetry().catch(() => null),
        practiceHealthApi.getValuations().catch(() => []),
        practiceHealthApi.getEquities().catch(() => []),
        practiceHealthApi.getRetirementPlans().catch(() => []),
        practiceHealthApi.getTransitions().catch(() => []),
        practiceHealthApi.getBenchmarks().catch(() => []),
        api<ClientPage>('/api/clients?page=1&limit=200').catch(() => ({ items: [], total: 0 })),
        api<UserProfile[]>('/api/users').catch(() => [])
      ])

      setTelemetry(tele)
      setValuations(valList)
      if (valList.length > 0) setSelectedValuation(valList[0])
      setEquities(eqList)
      setRetirementPlans(retList)
      setTransitions(transList)
      setBenchmarks(bmList)
      if (bmList.length > 0) setActiveBenchmark(bmList[0])
      setClients(clientsRes.items || [])
      setUsers(usersRes || [])

      // Auto-populate calculation from telemetry if available
      if (tele && tele.annualGrossBillings > 0) {
        setCalcForm(prev => ({
          ...prev,
          annualGrossRevenue: tele.annualGrossBillings,
          recurringComplianceRevenue: Math.round(tele.annualGrossBillings * 0.72),
          advisoryAndOneOffRevenue: Math.round(tele.annualGrossBillings * 0.28),
          ebitda: Math.round(tele.annualGrossBillings * 0.28)
        }))
      }
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to load practice health data.'))
    } finally {
      setLoading(false)
    }
  }

  const recomputeValuation = useCallback(async () => {
    try {
      const res = await practiceHealthApi.calculateValuation(calcForm)
      setLiveValuationResult(res)
    } catch (err: unknown) {
      console.error(err)
    }
  }, [calcForm])

  // Recalculate valuation in real time
  useEffect(() => {
    const timer = setTimeout(() => {
      recomputeValuation()
    }, 200)
    return () => clearTimeout(timer)
  }, [recomputeValuation])

  // Save Valuation Scenario
  const handleSaveValuation = async (e: FormEvent) => {
    e.preventDefault()
    setErrorMsg(null)
    try {
      const req: SaveValuationRequest = {
        valuationName: valScenarioName,
        valuationDate: new Date().toISOString().split('T')[0],
        method: calcForm.method,
        annualGrossRevenue: Number(calcForm.annualGrossRevenue) || 0,
        recurringComplianceRevenue: Number(calcForm.recurringComplianceRevenue) || 0,
        advisoryAndOneOffRevenue: Number(calcForm.advisoryAndOneOffRevenue) || 0,
        ebitda: Number(calcForm.ebitda) || 0,
        partnerNormalizedCompensation: Number(calcForm.partnerNormalizedCompensation) || 0,
        netWorkingCapital: Number(calcForm.netWorkingCapital) || 0,
        netTangibleAssets: Number(calcForm.netTangibleAssets) || 0,
        projectedRevenueGrowthRate: Number(calcForm.projectedRevenueGrowthRate) || 0.12,
        discountRateWacc: Number(calcForm.discountRateWacc) || 0.14,
        terminalGrowthRate: Number(calcForm.terminalGrowthRate) || 0.05,
        forecastYears: Number(calcForm.forecastYears) || 5,
        revenueMultipleBase: Number(calcForm.revenueMultipleBase) || 1.35,
        ebitdaMultipleBase: Number(calcForm.ebitdaMultipleBase) || 5.50,
        auditFranchiseStickinessPremiumPct: Number(calcForm.auditFranchiseStickinessPremiumPct) || 0.10,
        partnerConcentrationDiscountPct: Number(calcForm.partnerConcentrationDiscountPct) || 0.10,
        recurringRevenuePremiumPct: Number(calcForm.recurringRevenuePremiumPct) || 0.15,
        notes: valNotes
      }
      const saved = await practiceHealthApi.saveValuation(req)
      setNotice(`Practice Valuation scenario "${saved.valuationName}" saved successfully (FMV: ₹${(saved.blendedFirmFairMarketValue / 10000000).toFixed(2)} Cr)!`)
      const list = await practiceHealthApi.getValuations()
      setValuations(list)
      setSelectedValuation(saved)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to save valuation scenario.'))
    }
  }

  // Approve Valuation
  const handleApproveValuation = async (id: string) => {
    try {
      const approved = await practiceHealthApi.approveValuation(id)
      setNotice(`Valuation "${approved.valuationName}" has been ratified and approved by the partnership.`)
      const list = await practiceHealthApi.getValuations()
      setValuations(list)
      setSelectedValuation(approved)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Approval failed.'))
    }
  }

  // Save Partner Equity
  const handleSaveEquity = async (e: FormEvent) => {
    e.preventDefault()
    setErrorMsg(null)
    try {
      const saved = await practiceHealthApi.saveEquity(equityForm)
      setNotice(`Partner equity for ${saved.partnerName} (${saved.equityPercentage}%) saved successfully!`)
      setShowEquityModal(false)
      const list = await practiceHealthApi.getEquities()
      setEquities(list)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to save partner equity.'))
    }
  }

  // Delete Partner Equity
  const handleDeleteEquity = async (id: string) => {
    if (!confirm('Are you sure you want to remove this partner from the equity cap table?')) return
    try {
      await practiceHealthApi.deleteEquity(id)
      setNotice('Partner removed from equity register.')
      const list = await practiceHealthApi.getEquities()
      setEquities(list)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Delete failed.'))
    }
  }

  // Create Retirement Plan
  const handleCreateRetirementPlan = async (e: FormEvent) => {
    e.preventDefault()
    setErrorMsg(null)
    try {
      const created = await practiceHealthApi.createRetirementPlan(retirementForm)
      setNotice(`Retirement buyout plan for ${created.partnerName} created (Installment: ₹${(created.annualInstallmentAmount / 100000).toFixed(2)}L/yr)!`)
      setShowRetirementModal(false)
      const list = await practiceHealthApi.getRetirementPlans()
      setRetirementPlans(list)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to create retirement plan.'))
    }
  }

  // Save Transition
  const handleSaveTransition = async (e: FormEvent) => {
    e.preventDefault()
    setErrorMsg(null)
    try {
      const created = await practiceHealthApi.saveTransition(transitionForm)
      setNotice(`Client succession for ${created.clientName} mapped to ${created.successorPartnerName}.`)
      setShowTransitionModal(false)
      const list = await practiceHealthApi.getTransitions()
      setTransitions(list)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to save client transition.'))
    }
  }

  // Generate Benchmark
  const handleGenerateBenchmark = async () => {
    setErrorMsg(null)
    try {
      const req: GenerateBenchmarkRequest = {
        benchmarkYear: new Date().getFullYear(),
        firmTier: selectedTier
      }
      const bm = await practiceHealthApi.generateBenchmark(req)
      setNotice(`ICAI Peer Benchmark for ${selectedTier} generated (Score: ${bm.overallHealthScore}/100 - Grade ${bm.healthGrade})!`)
      const list = await practiceHealthApi.getBenchmarks()
      setBenchmarks(list)
      setActiveBenchmark(bm)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to generate benchmark.'))
    }
  }

  // Cap table total equity % check
  const totalEquityPct = useMemo(() => {
    return equities.reduce((sum, e) => sum + e.equityPercentage, 0)
  }, [equities])

  return (
    <div className="page-container" style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto' }}>
      {/* Header Banner */}
      <div style={{
        background: 'linear-gradient(135deg, #09203f 0%, #1e3c72 50%, #2a5298 100%)',
        color: '#ffffff',
        padding: '24px 32px',
        borderRadius: '12px',
        marginBottom: '24px',
        boxShadow: '0 4px 20px rgba(0, 0, 0, 0.15)',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: '16px'
      }}>
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '8px' }}>
            <span style={{ fontSize: '32px' }}>💎</span>
            <h1 style={{ margin: 0, fontSize: '24px', fontWeight: 700, letterSpacing: '-0.02em' }}>
              Practice Health, Succession &amp; Firm Valuation Desk
            </h1>
            <span style={{
              background: '#00a86b',
              color: '#ffffff',
              fontSize: '12px',
              padding: '4px 10px',
              borderRadius: '20px',
              fontWeight: 600
            }}>
              ICAI Multi-Tier Benchmarked
            </span>
          </div>
          <p style={{ margin: 0, opacity: 0.9, fontSize: '14px', maxWidth: '850px', lineHeight: 1.5 }}>
            Automated DCF &amp; recurring multiple firm valuation, partner equity cap table with retirement buyout amortization schedules,
            client succession transition tracking, and real-time operational benchmarking against ICAI norms.
          </p>
        </div>

        {/* Quick Telemetry Cards */}
        {telemetry && (
          <div style={{ display: 'flex', gap: '12px', flexWrap: 'wrap' }}>
            <div style={{ backgroundColor: 'rgba(255,255,255,0.12)', padding: '10px 16px', borderRadius: '8px', textAlign: 'center' }}>
              <div style={{ fontSize: '11px', textTransform: 'uppercase', opacity: 0.8 }}>Annual Gross Billings</div>
              <div style={{ fontSize: '18px', fontWeight: 700 }}>₹{(telemetry.annualGrossBillings / 10000000).toFixed(2)} Cr</div>
            </div>
            <div style={{ backgroundColor: 'rgba(255,255,255,0.12)', padding: '10px 16px', borderRadius: '8px', textAlign: 'center' }}>
              <div style={{ fontSize: '11px', textTransform: 'uppercase', opacity: 0.8 }}>Est. Debtor DSO</div>
              <div style={{ fontSize: '18px', fontWeight: 700 }}>{telemetry.estimatedDsoDays.toFixed(0)} Days</div>
            </div>
            <div style={{ backgroundColor: 'rgba(255,255,255,0.12)', padding: '10px 16px', borderRadius: '8px', textAlign: 'center' }}>
              <div style={{ fontSize: '11px', textTransform: 'uppercase', opacity: 0.8 }}>Active Partners</div>
              <div style={{ fontSize: '18px', fontWeight: 700 }}>{telemetry.activePartnersCount} Partners</div>
            </div>
          </div>
        )}
      </div>

      {/* Loading & Notifications */}
      {loading && (
        <div style={{ fontSize: '13px', color: 'var(--muted)', marginBottom: '12px', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <span>⏳</span> Loading practice telemetry and partner records...
        </div>
      )}
      {notice && (
        <div style={{
          backgroundColor: 'var(--success-bg)',
          color: 'var(--success-text)',
          border: '1px solid var(--success-border)',
          borderRadius: '8px',
          padding: '12px 16px',
          marginBottom: '20px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span>✓ {notice}</span>
          <button onClick={() => setNotice(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--success-text)', fontWeight: 'bold' }}>✕</button>
        </div>
      )}
      {errorMsg && (
        <div style={{
          backgroundColor: 'var(--error-bg)',
          color: 'var(--error-text)',
          border: '1px solid var(--error-border)',
          borderRadius: '8px',
          padding: '12px 16px',
          marginBottom: '20px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span>⚠ {errorMsg}</span>
          <button onClick={() => setErrorMsg(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--error-text)', fontWeight: 'bold' }}>✕</button>
        </div>
      )}

      {/* Main Navigation Tabs */}
      <div style={{ display: 'flex', borderBottom: '2px solid var(--border)', marginBottom: '24px', gap: '8px' }}>
        <button
          onClick={() => setActiveTab('valuation')}
          style={{
            padding: '12px 20px',
            fontWeight: 600,
            fontSize: '15px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            borderBottom: activeTab === 'valuation' ? '3px solid var(--accent)' : '3px solid transparent',
            color: activeTab === 'valuation' ? 'var(--accent)' : 'var(--muted)'
          }}
        >
          💎 Firm Valuation Calculator (DCF &amp; Multiples)
        </button>
        <button
          onClick={() => setActiveTab('succession')}
          style={{
            padding: '12px 20px',
            fontWeight: 600,
            fontSize: '15px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            borderBottom: activeTab === 'succession' ? '3px solid var(--accent)' : '3px solid transparent',
            color: activeTab === 'succession' ? 'var(--accent)' : 'var(--muted)'
          }}
        >
          🤝 Partner Equity, Succession &amp; Retirement Buyout ({equities.length} Partners)
        </button>
        <button
          onClick={() => setActiveTab('benchmarks')}
          style={{
            padding: '12px 20px',
            fontWeight: 600,
            fontSize: '15px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            borderBottom: activeTab === 'benchmarks' ? '3px solid var(--accent)' : '3px solid transparent',
            color: activeTab === 'benchmarks' ? 'var(--accent)' : 'var(--muted)'
          }}
        >
          📊 ICAI Peer Benchmarking &amp; Health Scorecard
        </button>
      </div>

      {/* TAB 1: FIRM VALUATION CALCULATOR */}
      {activeTab === 'valuation' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 0.8fr', gap: '24px' }}>
          {/* Left Column: Valuation Inputs & Sensitivity */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <h3 style={{ margin: 0, fontSize: '16px', color: 'var(--text)', fontWeight: 600 }}>
                  1. Firm Earnings &amp; Financial Performance
                </h3>
                {telemetry && (
                  <button
                    onClick={() => {
                      setCalcForm(prev => ({
                        ...prev,
                        annualGrossRevenue: telemetry.annualGrossBillings,
                        recurringComplianceRevenue: Math.round(telemetry.annualGrossBillings * 0.72),
                        advisoryAndOneOffRevenue: Math.round(telemetry.annualGrossBillings * 0.28),
                        ebitda: Math.round(telemetry.annualGrossBillings * 0.28)
                      }))
                    }}
                    style={{
                      padding: '4px 10px',
                      backgroundColor: 'var(--card-subtle)',
                      border: '1px solid var(--border-subtle)',
                      borderRadius: '4px',
                      fontSize: '12px',
                      cursor: 'pointer',
                      color: 'var(--text-secondary)',
                      fontWeight: 500
                    }}
                  >
                    ↺ Sync from Live Telemetry
                  </button>
                )}
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Annual Gross Revenue (₹)
                  </label>
                  <input
                    type="number"
                    value={calcForm.annualGrossRevenue}
                    onChange={(e) => setCalcForm({ ...calcForm, annualGrossRevenue: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px', fontWeight: 600 }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--muted)' }}>₹{(calcForm.annualGrossRevenue / 10000000).toFixed(2)} Cr</span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Recurring Retainers (₹)
                  </label>
                  <input
                    type="number"
                    value={calcForm.recurringComplianceRevenue}
                    onChange={(e) => setCalcForm({ ...calcForm, recurringComplianceRevenue: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--success)' }}>
                    {calcForm.annualGrossRevenue > 0 ? ((calcForm.recurringComplianceRevenue / calcForm.annualGrossRevenue) * 100).toFixed(0) : 0}% recurring
                  </span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Advisory / One-Off Fees (₹)
                  </label>
                  <input
                    type="number"
                    value={calcForm.advisoryAndOneOffRevenue}
                    onChange={(e) => setCalcForm({ ...calcForm, advisoryAndOneOffRevenue: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Firm EBITDA (₹)
                  </label>
                  <input
                    type="number"
                    value={calcForm.ebitda}
                    onChange={(e) => setCalcForm({ ...calcForm, ebitda: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px', fontWeight: 600 }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--muted)' }}>
                    {calcForm.annualGrossRevenue > 0 ? ((calcForm.ebitda / calcForm.annualGrossRevenue) * 100).toFixed(1) : 0}% margin
                  </span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Partner Normalized Comp (₹)
                  </label>
                  <input
                    type="number"
                    value={calcForm.partnerNormalizedCompensation}
                    onChange={(e) => setCalcForm({ ...calcForm, partnerNormalizedCompensation: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Net Tangible Assets (₹)
                  </label>
                  <input
                    type="number"
                    value={calcForm.netTangibleAssets}
                    onChange={(e) => setCalcForm({ ...calcForm, netTangibleAssets: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--muted)' }}>Book value base</span>
                </div>
              </div>
            </div>

            {/* Valuation Methodology & Risk Premiums */}
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <h3 style={{ margin: '0 0 16px 0', fontSize: '16px', color: 'var(--text)', fontWeight: 600 }}>
                2. Valuation Method &amp; Quality Premium Drivers
              </h3>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Primary Valuation Method</label>
                  <select
                    value={calcForm.method}
                    onChange={(e) => setCalcForm({ ...calcForm, method: e.target.value as ValuationMethod })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px', backgroundColor: 'var(--card)' }}
                  >
                    <option value="BlendedWeight">Blended (50% DCF + 50% Multiples)</option>
                    <option value="DiscountedCashFlow">Pure DCF (Free Cash Flow)</option>
                    <option value="RevenueMultiple">Revenue Multiple Approach</option>
                    <option value="EbitdaMultiple">EBITDA Multiple Approach</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Base Revenue Multiple</label>
                  <input
                    type="number"
                    step="0.05"
                    value={calcForm.revenueMultipleBase}
                    onChange={(e) => setCalcForm({ ...calcForm, revenueMultipleBase: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--muted)' }}>ICAI norm: 1.1x - 1.5x</span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Base EBITDA Multiple</label>
                  <input
                    type="number"
                    step="0.1"
                    value={calcForm.ebitdaMultipleBase}
                    onChange={(e) => setCalcForm({ ...calcForm, ebitdaMultipleBase: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--muted)' }}>ICAI norm: 4.5x - 6.5x</span>
                </div>
              </div>

              {/* Sensitivity Sliders / Risk Controls */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Audit Stickiness Premium (%)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    value={calcForm.auditFranchiseStickinessPremiumPct}
                    onChange={(e) => setCalcForm({ ...calcForm, auditFranchiseStickinessPremiumPct: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--success)' }}>+{(calcForm.auditFranchiseStickinessPremiumPct * 100).toFixed(0)}% regulatory moat</span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Partner Key-Man Discount (%)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    value={calcForm.partnerConcentrationDiscountPct}
                    onChange={(e) => setCalcForm({ ...calcForm, partnerConcentrationDiscountPct: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--error)' }}>-{(calcForm.partnerConcentrationDiscountPct * 100).toFixed(0)}% concentration risk</span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Discount Rate / WACC (%)
                  </label>
                  <input
                    type="number"
                    step="0.01"
                    value={calcForm.discountRateWacc}
                    onChange={(e) => setCalcForm({ ...calcForm, discountRateWacc: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--muted)' }}>{(calcForm.discountRateWacc * 100).toFixed(0)}% cost of capital</span>
                </div>
              </div>
            </div>

            {/* Save Scenario Panel */}
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <h3 style={{ margin: '0 0 12px 0', fontSize: '16px', color: 'var(--text)', fontWeight: 600 }}>
                💾 Save Valuation Scenario for Partnership Review
              </h3>
              <form onSubmit={handleSaveValuation} style={{ display: 'flex', gap: '12px', alignItems: 'center' }}>
                <input
                  type="text"
                  placeholder="Scenario Name (e.g. FY25 Partner Buy-in Valuation)"
                  value={valScenarioName}
                  onChange={(e) => setValScenarioName(e.target.value)}
                  style={{ flex: 1, padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  required
                />
                <input
                  type="text"
                  placeholder="Notes / Partnership Resolution"
                  value={valNotes}
                  onChange={(e) => setValNotes(e.target.value)}
                  style={{ width: '220px', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                />
                <button
                  type="submit"
                  style={{
                    padding: '10px 20px',
                    backgroundColor: '#1e3c72',
                    color: '#ffffff',
                    border: 'none',
                    borderRadius: '6px',
                    fontWeight: 600,
                    cursor: 'pointer'
                  }}
                >
                  Save Scenario
                </button>
              </form>
            </div>
          </div>

          {/* Right Column: Live Valuation Results Card */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '12px',
              border: '2px solid var(--accent)',
              padding: '24px',
              boxShadow: '0 4px 16px rgba(0,0,0,0.08)',
              position: 'sticky',
              top: '24px'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
                <h3 style={{ margin: 0, fontSize: '18px', color: '#09203f', fontWeight: 700 }}>
                  💎 Fair Market Firm Valuation
                </h3>
                <span style={{ fontSize: '12px', padding: '3px 8px', borderRadius: '12px', backgroundColor: 'var(--success-bg)', color: 'var(--success-text)', fontWeight: 600 }}>
                  Live Model
                </span>
              </div>

              {liveValuationResult ? (
                <div>
                  <div style={{
                    backgroundColor: '#09203f',
                    color: '#ffffff',
                    padding: '20px',
                    borderRadius: '8px',
                    textAlign: 'center',
                    marginBottom: '20px'
                  }}>
                    <div style={{ fontSize: '12px', opacity: 0.8, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                      Blended Enterprise Valuation (FMV)
                    </div>
                    <div style={{ fontSize: '32px', fontWeight: 800, marginTop: '4px' }}>
                      ₹{(liveValuationResult.blendedFirmFairMarketValue / 10000000).toFixed(2)} Cr
                    </div>
                    <div style={{ fontSize: '13px', opacity: 0.9, marginTop: '6px', color: '#93c5fd' }}>
                      Value per 1.0% Equity Point: <strong>₹{(liveValuationResult.valuePerEquityPoint / 100000).toFixed(2)} Lakhs</strong>
                    </div>
                  </div>

                  <div style={{ borderBottom: '1px solid var(--border)', paddingBottom: '12px', marginBottom: '12px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--muted)', marginBottom: '8px' }}>
                      <span>DCF Model Enterprise Value:</span>
                      <span style={{ fontWeight: 600, color: 'var(--text)' }}>
                        ₹{(liveValuationResult.dcfEnterpriseValue / 10000000).toFixed(2)} Cr
                      </span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--muted)', marginBottom: '8px' }}>
                      <span>Multiples Model Value:</span>
                      <span style={{ fontWeight: 600, color: 'var(--text)' }}>
                        ₹{(liveValuationResult.multipleEnterpriseValue / 10000000).toFixed(2)} Cr
                      </span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--muted)', marginBottom: '8px' }}>
                      <span>Effective Revenue Multiple:</span>
                      <span style={{ fontWeight: 600, color: 'var(--accent)' }}>
                        {liveValuationResult.effectiveRevenueMultiple}x Revenue
                      </span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--muted)', marginBottom: '8px' }}>
                      <span>Effective EBITDA Multiple:</span>
                      <span style={{ fontWeight: 600, color: 'var(--accent)' }}>
                        {liveValuationResult.effectiveEbitdaMultiple}x EBITDA
                      </span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--success)', fontWeight: 600 }}>
                      <span>Intangible Goodwill Value:</span>
                      <span>₹{(liveValuationResult.firmGoodwillValue / 10000000).toFixed(2)} Cr</span>
                    </div>
                  </div>

                  {/* 5-Year DCF Cash Flows Table */}
                  <div style={{ marginTop: '16px' }}>
                    <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px' }}>
                      5-Year Cash Flow Projection (₹ in Lakhs)
                    </div>
                    <table style={{ width: '100%', fontSize: '11px', borderCollapse: 'collapse', textAlign: 'right' }}>
                      <thead>
                        <tr style={{ backgroundColor: 'var(--card-subtle)', color: 'var(--text-secondary)' }}>
                          <th style={{ padding: '4px', textAlign: 'left' }}>Yr</th>
                          <th style={{ padding: '4px' }}>Rev</th>
                          <th style={{ padding: '4px' }}>EBITDA</th>
                          <th style={{ padding: '4px' }}>FCF</th>
                          <th style={{ padding: '4px' }}>PV</th>
                        </tr>
                      </thead>
                      <tbody>
                        {liveValuationResult.projectedCashFlows.map(cf => (
                          <tr key={cf.year} style={{ borderBottom: '1px solid var(--border)' }}>
                            <td style={{ padding: '4px', textAlign: 'left', fontWeight: 600 }}>Y{cf.year}</td>
                            <td style={{ padding: '4px' }}>{(cf.projectedRevenue / 100000).toFixed(0)}L</td>
                            <td style={{ padding: '4px' }}>{(cf.projectedEbitda / 100000).toFixed(0)}L</td>
                            <td style={{ padding: '4px' }}>{(cf.freeCashFlow / 100000).toFixed(0)}L</td>
                            <td style={{ padding: '4px', fontWeight: 600, color: '#09203f' }}>{(cf.presentValue / 100000).toFixed(0)}L</td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ) : (
                <div style={{ padding: '32px 0', textAlign: 'center', color: 'var(--muted)' }}>Computing valuation...</div>
              )}
            </div>

            {/* Saved Valuations List */}
            {valuations.length > 0 && (
              <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '16px' }}>
                <h4 style={{ margin: '0 0 12px 0', fontSize: '14px', color: 'var(--text)' }}>Saved Scenarios ({valuations.length})</h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  {valuations.map(v => (
                    <div
                      key={v.id}
                      style={{
                        padding: '10px',
                        borderRadius: '6px',
                        border: '1px solid var(--border)',
                        backgroundColor: selectedValuation?.id === v.id ? '#f0f9ff' : 'var(--card-subtle)',
                        cursor: 'pointer'
                      }}
                      onClick={() => setSelectedValuation(v)}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <strong style={{ fontSize: '13px' }}>{v.valuationName}</strong>
                        <span style={{
                          fontSize: '11px',
                          padding: '2px 6px',
                          borderRadius: '8px',
                          backgroundColor: v.status === 'ApprovedByPartners' ? 'var(--success-bg)' : 'var(--warning-bg)',
                          color: v.status === 'ApprovedByPartners' ? 'var(--success-text)' : 'var(--warning-text)'
                        }}>
                          {v.status}
                        </span>
                      </div>
                      <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '4px', display: 'flex', justifyContent: 'space-between' }}>
                        <span>FMV: ₹{(v.blendedFirmFairMarketValue / 10000000).toFixed(2)} Cr</span>
                        {v.status !== 'ApprovedByPartners' && (
                          <button
                            onClick={(e) => {
                              e.stopPropagation()
                              handleApproveValuation(v.id)
                            }}
                            style={{
                              padding: '2px 8px',
                              backgroundColor: '#1e3c72',
                              color: '#ffffff',
                              border: 'none',
                              borderRadius: '4px',
                              fontSize: '11px',
                              cursor: 'pointer'
                            }}
                          >
                            Ratify Scenario
                          </button>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        </div>
      )}

      {/* TAB 2: PARTNER EQUITY, SUCCESSION & RETIREMENT */}
      {activeTab === 'succession' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
          {/* Equity Cap Table Card */}
          <div style={{
            backgroundColor: 'var(--card)',
            borderRadius: '8px',
            border: '1px solid var(--border)',
            padding: '20px',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div>
                <h3 style={{ margin: '0 0 4px 0', fontSize: '16px', color: 'var(--text)', fontWeight: 600 }}>
                  🏛️ Partnership Deed Equity Cap Table ({equities.length} Partners)
                </h3>
                <div style={{ fontSize: '13px', color: 'var(--muted)' }}>
                  Total Allocated Equity: <strong style={{ color: totalEquityPct === 100 ? 'var(--success)' : 'var(--error)' }}>{totalEquityPct.toFixed(2)}%</strong>
                  {totalEquityPct === 100 ? ' (Fully Balanced 100%)' : ' (Warning: Deed must equal 100%)'}
                </div>
              </div>
              <button
                onClick={() => setShowEquityModal(true)}
                style={{
                  padding: '8px 16px',
                  backgroundColor: '#1e3c72',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: 'pointer'
                }}
              >
                + Add Partner to Deed
              </button>
            </div>

            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ backgroundColor: 'var(--card-subtle)', color: 'var(--text-secondary)', borderBottom: '1px solid var(--border)' }}>
                  <th style={{ padding: '10px 14px' }}>Partner Name &amp; M.No</th>
                  <th style={{ padding: '10px 14px' }}>Role</th>
                  <th style={{ padding: '10px 14px' }}>Equity Share %</th>
                  <th style={{ padding: '10px 14px' }}>Capital Account</th>
                  <th style={{ padding: '10px 14px' }}>Est. Equity Value (FMV)</th>
                  <th style={{ padding: '10px 14px' }}>Vesting Status</th>
                  <th style={{ padding: '10px 14px' }}>Target Retirement</th>
                  <th style={{ padding: '10px 14px' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {equities.length === 0 ? (
                  <tr>
                    <td colSpan={8} style={{ padding: '24px', textAlign: 'center', color: 'var(--muted)' }}>
                      No partner equity records recorded yet. Click "+ Add Partner to Deed" above to seed your cap table.
                    </td>
                  </tr>
                ) : (
                  equities.map(e => {
                    const estValue = liveValuationResult ? (liveValuationResult.blendedFirmFairMarketValue * (e.equityPercentage / 100)) : 0
                    return (
                      <tr key={e.id} style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '10px 14px' }}>
                          <strong style={{ color: 'var(--text)' }}>{e.partnerName}</strong>
                          <div style={{ fontSize: '11px', color: 'var(--muted)' }}>ICAI M.No: {e.partnerMembershipNo || 'N/A'}</div>
                        </td>
                        <td style={{ padding: '10px 14px' }}>
                          <span style={{ fontSize: '11px', padding: '2px 6px', borderRadius: '4px', backgroundColor: 'var(--card-subtle)' }}>
                            {e.role}
                          </span>
                        </td>
                        <td style={{ padding: '10px 14px', fontWeight: 700, color: 'var(--accent)' }}>
                          {e.equityPercentage.toFixed(2)}%
                        </td>
                        <td style={{ padding: '10px 14px' }}>
                          ₹{(e.capitalAccountBalance / 100000).toFixed(2)} Lakhs
                        </td>
                        <td style={{ padding: '10px 14px', fontWeight: 600, color: 'var(--success)' }}>
                          ₹{(estValue / 10000000).toFixed(2)} Cr
                        </td>
                        <td style={{ padding: '10px 14px' }}>
                          <span style={{
                            fontSize: '11px',
                            padding: '2px 6px',
                            borderRadius: '10px',
                            backgroundColor: e.vestingStatus === 'FullyVested' ? 'var(--success-bg)' : 'var(--warning-bg)',
                            color: e.vestingStatus === 'FullyVested' ? 'var(--success-text)' : 'var(--warning-text)'
                          }}>
                            {e.vestingStatus} ({e.vestedPercentage}%)
                          </span>
                        </td>
                        <td style={{ padding: '10px 14px', color: 'var(--text-secondary)' }}>
                          {e.targetRetirementDate || 'Active Continuing'}
                        </td>
                        <td style={{ padding: '10px 14px' }}>
                          <button
                            onClick={() => handleDeleteEquity(e.id)}
                            style={{ background: 'none', border: 'none', color: 'var(--error)', cursor: 'pointer', fontSize: '12px' }}
                          >
                            Remove
                          </button>
                        </td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>

          {/* Retirement Buyout & Goodwill Amortization Planner */}
          <div style={{
            backgroundColor: 'var(--card)',
            borderRadius: '8px',
            border: '1px solid var(--border)',
            padding: '20px',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div>
                <h3 style={{ margin: '0 0 4px 0', fontSize: '16px', color: 'var(--text)', fontWeight: 600 }}>
                  ⏳ Partner Retirement Buyout &amp; Goodwill Amortization Plans ({retirementPlans.length})
                </h3>
                <div style={{ fontSize: '13px', color: 'var(--muted)' }}>
                  Institutional buyout engine: capital account refund + agreed goodwill amortization with statutory interest.
                </div>
              </div>
              <button
                onClick={() => setShowRetirementModal(true)}
                disabled={equities.length === 0}
                style={{
                  padding: '8px 16px',
                  backgroundColor: equities.length > 0 ? '#00a86b' : '#94a3b8',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: equities.length > 0 ? 'pointer' : 'not-allowed'
                }}
              >
                + Plan Partner Retirement
              </button>
            </div>

            {retirementPlans.length === 0 ? (
              <div style={{ padding: '24px', textAlign: 'center', color: 'var(--muted)' }}>
                No partner retirement plans created yet. Click "+ Plan Partner Retirement" to schedule a structured equity buyout.
              </div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                {retirementPlans.map(plan => (
                  <div key={plan.id} style={{ border: '1px solid var(--border)', borderRadius: '8px', padding: '16px', backgroundColor: 'var(--card-subtle)' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                      <div>
                        <strong style={{ fontSize: '15px', color: 'var(--text)' }}>Retirement Plan: {plan.partnerName}</strong>
                        <span style={{ fontSize: '12px', color: 'var(--muted)', marginLeft: '12px' }}>
                          Retirement Date: <strong>{plan.retirementDate}</strong> | Equity: <strong>{plan.partnerEquityPct}%</strong>
                        </span>
                      </div>
                      <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                        <span style={{ fontSize: '12px', padding: '2px 8px', borderRadius: '12px', backgroundColor: 'var(--accent-subtle)', color: 'var(--accent)', fontWeight: 600 }}>
                          {plan.status}
                        </span>
                        <span style={{ fontSize: '14px', fontWeight: 700, color: '#09203f' }}>
                          Installment: ₹{(plan.annualInstallmentAmount / 100000).toFixed(2)}L/yr ({plan.payoutPeriodYears} yrs @ {plan.annualInterestRatePct}%)
                        </span>
                      </div>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '12px', marginBottom: '16px', fontSize: '12px' }}>
                      <div style={{ backgroundColor: 'var(--card)', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)' }}>
                        <span style={{ color: 'var(--muted)' }}>Agreed Firm Valuation:</span>
                        <div style={{ fontWeight: 600 }}>₹{(plan.agreedFirmValuationAtRetirement / 10000000).toFixed(2)} Cr</div>
                      </div>
                      <div style={{ backgroundColor: 'var(--card)', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)' }}>
                        <span style={{ color: 'var(--muted)' }}>Gross Equity Value:</span>
                        <div style={{ fontWeight: 600 }}>₹{(plan.grossEquityValue / 100000).toFixed(2)} Lakhs</div>
                      </div>
                      <div style={{ backgroundColor: 'var(--card)', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)' }}>
                        <span style={{ color: 'var(--muted)' }}>Capital Account Refund:</span>
                        <div style={{ fontWeight: 600 }}>₹{(plan.capitalAccountRefundAmount / 100000).toFixed(2)} Lakhs</div>
                      </div>
                      <div style={{ backgroundColor: 'var(--card)', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)' }}>
                        <span style={{ color: 'var(--muted)' }}>Goodwill Buyout Share:</span>
                        <div style={{ fontWeight: 600, color: 'var(--success)' }}>₹{(plan.goodwillBuyoutAmount / 100000).toFixed(2)} Lakhs</div>
                      </div>
                    </div>

                    {/* Amortization Schedule Table */}
                    <div style={{ backgroundColor: 'var(--card)', borderRadius: '6px', border: '1px solid var(--border)', padding: '12px' }}>
                      <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px' }}>
                        Annual Buyout Payout Schedule
                      </div>
                      <table style={{ width: '100%', fontSize: '12px', borderCollapse: 'collapse', textAlign: 'right' }}>
                        <thead>
                          <tr style={{ backgroundColor: 'var(--card-subtle)', color: 'var(--text-secondary)' }}>
                            <th style={{ padding: '6px', textAlign: 'left' }}>Payout Year</th>
                            <th style={{ padding: '6px' }}>Beginning Balance</th>
                            <th style={{ padding: '6px' }}>Principal Paid</th>
                            <th style={{ padding: '6px' }}>Interest ({plan.annualInterestRatePct}%)</th>
                            <th style={{ padding: '6px' }}>Total Annual Installment</th>
                            <th style={{ padding: '6px' }}>Ending Balance</th>
                          </tr>
                        </thead>
                        <tbody>
                          {plan.amortizationSchedule.map(row => (
                            <tr key={row.year} style={{ borderBottom: '1px solid var(--border)' }}>
                              <td style={{ padding: '6px', textAlign: 'left', fontWeight: 600 }}>Year {row.year}</td>
                              <td style={{ padding: '6px' }}>₹{(row.beginningBalance / 100000).toFixed(2)}L</td>
                              <td style={{ padding: '6px', color: 'var(--success)' }}>₹{(row.principalPaid / 100000).toFixed(2)}L</td>
                              <td style={{ padding: '6px', color: 'var(--warning)' }}>₹{(row.interestPaid / 100000).toFixed(2)}L</td>
                              <td style={{ padding: '6px', fontWeight: 700, color: 'var(--text)' }}>₹{(row.totalInstallment / 100000).toFixed(2)}L</td>
                              <td style={{ padding: '6px' }}>₹{(row.endingBalance / 100000).toFixed(2)}L</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Client Succession Transition Matrix */}
          <div style={{
            backgroundColor: 'var(--card)',
            borderRadius: '8px',
            border: '1px solid var(--border)',
            padding: '20px',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <div>
                <h3 style={{ margin: '0 0 4px 0', fontSize: '16px', color: 'var(--text)', fontWeight: 600 }}>
                  🔄 Client Succession Transition Register ({transitions.length})
                </h3>
                <div style={{ fontSize: '13px', color: 'var(--muted)' }}>
                  Prevents key-man client churn during partner retirements by managing formal multi-stage handovers.
                </div>
              </div>
              <button
                onClick={() => setShowTransitionModal(true)}
                style={{
                  padding: '8px 16px',
                  backgroundColor: '#1e3c72',
                  color: '#ffffff',
                  border: 'none',
                  borderRadius: '6px',
                  fontWeight: 600,
                  fontSize: '13px',
                  cursor: 'pointer'
                }}
              >
                + Map Client Transition
              </button>
            </div>

            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '13px' }}>
              <thead>
                <tr style={{ backgroundColor: 'var(--card-subtle)', color: 'var(--text-secondary)', borderBottom: '1px solid var(--border)' }}>
                  <th style={{ padding: '10px 14px' }}>Client Name</th>
                  <th style={{ padding: '10px 14px' }}>Annual Fee</th>
                  <th style={{ padding: '10px 14px' }}>Retiring Partner</th>
                  <th style={{ padding: '10px 14px' }}>Successor Partner</th>
                  <th style={{ padding: '10px 14px' }}>Handover Stage</th>
                  <th style={{ padding: '10px 14px' }}>Retention Risk</th>
                  <th style={{ padding: '10px 14px' }}>Target Date</th>
                </tr>
              </thead>
              <tbody>
                {transitions.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '24px', textAlign: 'center', color: 'var(--muted)' }}>
                      No client handovers mapped. Click "+ Map Client Transition" to assign continuing partners to retiring clients.
                    </td>
                  </tr>
                ) : (
                  transitions.map(t => (
                    <tr key={t.id} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '10px 14px', fontWeight: 600 }}>{t.clientName}</td>
                      <td style={{ padding: '10px 14px' }}>₹{(t.annualClientFees / 100000).toFixed(2)}L</td>
                      <td style={{ padding: '10px 14px', color: 'var(--error)' }}>{t.retiringPartnerName}</td>
                      <td style={{ padding: '10px 14px', color: 'var(--success)', fontWeight: 500 }}>{t.successorPartnerName}</td>
                      <td style={{ padding: '10px 14px' }}>
                        <span style={{ fontSize: '11px', padding: '2px 8px', borderRadius: '10px', backgroundColor: 'var(--accent-subtle)', color: 'var(--accent)', fontWeight: 600 }}>
                          {t.stage}
                        </span>
                      </td>
                      <td style={{ padding: '10px 14px' }}>
                        <span style={{
                          fontSize: '11px',
                          padding: '2px 8px',
                          borderRadius: '10px',
                          backgroundColor: t.risk === 'Low' ? 'var(--success-bg)' : t.risk === 'Medium' ? 'var(--warning-bg)' : 'var(--error-bg)',
                          color: t.risk === 'Low' ? 'var(--success-text)' : t.risk === 'Medium' ? 'var(--warning-text)' : 'var(--error-text)',
                          fontWeight: 600
                        }}>
                          {t.risk} Risk
                        </span>
                      </td>
                      <td style={{ padding: '10px 14px', color: 'var(--text-secondary)' }}>{t.targetHandoverDate}</td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 3: ICAI PEER BENCHMARKING TELEMETRY */}
      {activeTab === 'benchmarks' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '24px' }}>
          {/* Controls & Tier Selector */}
          <div style={{
            backgroundColor: 'var(--card)',
            borderRadius: '8px',
            border: '1px solid var(--border)',
            padding: '20px',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: '16px'
          }}>
            <div>
              <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                Select ICAI Practice Comparison Tier: {benchmarks.length > 0 && <span style={{ fontWeight: 400, color: 'var(--muted)' }}>({benchmarks.length} historical scans saved)</span>}
              </label>
              <select
                value={selectedTier}
                onChange={(e) => setSelectedTier(e.target.value as FirmTierBenchmark)}
                style={{ padding: '8px 14px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '14px', fontWeight: 500, minWidth: '320px' }}
              >
                <option value="Tier1MetroBig10">Tier 1: Metro Top-10 Audit Firms (₹2.5 Cr/Partner)</option>
                <option value="Tier2MidSizedMultiPartner">Tier 2: Mid-Sized Multi-Partner Practices (₹1.2 Cr/Partner)</option>
                <option value="Tier3BoutiqueSmall">Tier 3: Small &amp; Boutique Practices (₹55L/Partner)</option>
              </select>
            </div>

            <button
              onClick={handleGenerateBenchmark}
              style={{
                padding: '10px 20px',
                backgroundColor: '#1e3c72',
                color: '#ffffff',
                border: 'none',
                borderRadius: '6px',
                fontWeight: 600,
                fontSize: '14px',
                cursor: 'pointer',
                boxShadow: '0 2px 8px rgba(30, 60, 114, 0.3)'
              }}
            >
              🔄 Run Telemetry Benchmark Scan
            </button>
          </div>

          {/* Health Score Banner */}
          {activeBenchmark && (
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '12px',
              border: '2px solid var(--accent)',
              padding: '24px',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              boxShadow: '0 4px 16px rgba(0,0,0,0.06)',
              flexWrap: 'wrap',
              gap: '20px'
            }}>
              <div>
                <div style={{ fontSize: '13px', color: 'var(--muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                  Practice Health &amp; Productivity Score ({activeBenchmark.firmTier})
                </div>
                <div style={{ fontSize: '36px', fontWeight: 800, color: '#09203f', marginTop: '4px' }}>
                  {activeBenchmark.overallHealthScore.toFixed(1)} / 100
                </div>
                <div style={{ fontSize: '14px', color: 'var(--success)', fontWeight: 600, marginTop: '2px' }}>
                  ✓ {activeBenchmark.healthGrade}
                </div>
              </div>

              <div style={{ flex: 1, maxWidth: '600px' }}>
                <div style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '6px' }}>
                  Automated Peer Recommendations:
                </div>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                  {activeBenchmark.insightsChecklist.map((ins, i) => (
                    <div key={i} style={{ fontSize: '13px', color: 'var(--text-secondary)', display: 'flex', alignItems: 'flex-start', gap: '8px' }}>
                      <span style={{ color: '#00a86b', fontWeight: 'bold' }}>•</span>
                      <span>{ins}</span>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* 6 Key Operational KPI Side-by-Side Comparison */}
          {activeBenchmark && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '20px' }}>
              {/* Card 1: Revenue Per Partner */}
              <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '16px' }}>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Revenue Per Partner</div>
                <div style={{ fontSize: '22px', fontWeight: 700, color: 'var(--text)' }}>
                  ₹{(activeBenchmark.actualRevenuePerPartner / 100000).toFixed(1)} Lakhs
                </div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '6px' }}>
                  Peer Benchmark: <strong>₹{(activeBenchmark.benchmarkRevenuePerPartner / 100000).toFixed(1)} Lakhs</strong>
                </div>
                <div style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  marginTop: '6px',
                  color: activeBenchmark.actualRevenuePerPartner >= activeBenchmark.benchmarkRevenuePerPartner ? 'var(--success)' : 'var(--error)'
                }}>
                  {activeBenchmark.actualRevenuePerPartner >= activeBenchmark.benchmarkRevenuePerPartner ? '▲ Top Quartile' : '▼ Below Peer Median'}
                </div>
              </div>

              {/* Card 2: Realization Rate */}
              <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '16px' }}>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Billing Realization Rate</div>
                <div style={{ fontSize: '22px', fontWeight: 700, color: 'var(--text)' }}>
                  {activeBenchmark.actualRealizationRate.toFixed(1)}%
                </div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '6px' }}>
                  Peer Benchmark: <strong>{activeBenchmark.benchmarkRealizationRate.toFixed(1)}%</strong>
                </div>
                <div style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  marginTop: '6px',
                  color: activeBenchmark.actualRealizationRate >= activeBenchmark.benchmarkRealizationRate ? 'var(--success)' : 'var(--error)'
                }}>
                  {activeBenchmark.actualRealizationRate >= activeBenchmark.benchmarkRealizationRate ? '▲ Efficient Billing' : '▼ Write-Off Pressure'}
                </div>
              </div>

              {/* Card 3: Debtor DSO Days */}
              <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '16px' }}>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Debtor Collection (DSO)</div>
                <div style={{ fontSize: '22px', fontWeight: 700, color: 'var(--text)' }}>
                  {activeBenchmark.actualDsoDays.toFixed(0)} Days
                </div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '6px' }}>
                  Peer Benchmark: <strong>{activeBenchmark.benchmarkDsoDays.toFixed(0)} Days</strong>
                </div>
                <div style={{
                  fontSize: '11px',
                  fontWeight: 600,
                  marginTop: '6px',
                  color: activeBenchmark.actualDsoDays <= activeBenchmark.benchmarkDsoDays ? 'var(--success)' : 'var(--error)'
                }}>
                  {activeBenchmark.actualDsoDays <= activeBenchmark.benchmarkDsoDays ? '▲ Fast Cash Conversion' : '▼ Extended Receivables'}
                </div>
              </div>

              {/* Card 4: Staff Leverage Ratio */}
              <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '16px' }}>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Staff to Partner Leverage</div>
                <div style={{ fontSize: '22px', fontWeight: 700, color: 'var(--text)' }}>
                  {activeBenchmark.actualStaffLeverageRatio.toFixed(1)}x
                </div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '6px' }}>
                  Peer Benchmark: <strong>{activeBenchmark.benchmarkStaffLeverageRatio.toFixed(1)}x</strong>
                </div>
                <div style={{ fontSize: '11px', color: 'var(--text-secondary)', marginTop: '6px' }}>
                  Qualified &amp; Articled staff per partner
                </div>
              </div>

              {/* Card 5: Total Lockup Days (WIP + DSO) */}
              <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '16px' }}>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Total Working Capital Lockup</div>
                <div style={{ fontSize: '22px', fontWeight: 700, color: 'var(--text)' }}>
                  {activeBenchmark.actualWipLockupDays.toFixed(0)} Days
                </div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '6px' }}>
                  Peer Benchmark: <strong>{activeBenchmark.benchmarkWipLockupDays.toFixed(0)} Days</strong>
                </div>
                <div style={{ fontSize: '11px', color: 'var(--text-secondary)', marginTop: '6px' }}>
                  Combined unbilled WIP + debtor delay
                </div>
              </div>

              {/* Card 6: Operating EBITDA Margin */}
              <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '16px' }}>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Operating EBITDA Margin</div>
                <div style={{ fontSize: '22px', fontWeight: 700, color: 'var(--text)' }}>
                  {activeBenchmark.actualEbitdaMarginPct.toFixed(1)}%
                </div>
                <div style={{ fontSize: '12px', color: 'var(--muted)', marginTop: '6px' }}>
                  Peer Benchmark: <strong>{activeBenchmark.benchmarkEbitdaMarginPct.toFixed(1)}%</strong>
                </div>
                <div style={{ fontSize: '11px', color: 'var(--success)', fontWeight: 600, marginTop: '6px' }}>
                  Healthy partner draw buffer
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* MODAL 1: ADD PARTNER TO CAP TABLE */}
      {showEquityModal && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: 'var(--card)',
            borderRadius: '12px',
            width: '550px',
            maxWidth: '95vw',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>
                🏛️ Add Partner to Equity Register
              </h2>
              <button onClick={() => setShowEquityModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            <form onSubmit={handleSaveEquity} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Linked User Account</label>
                <select
                  value={equityForm.userId}
                  onChange={(e) => {
                    const u = users.find(x => x.id === e.target.value)
                    setEquityForm({
                      ...equityForm,
                      userId: e.target.value,
                      partnerName: u?.name || equityForm.partnerName,
                      partnerEmail: u?.email || equityForm.partnerEmail
                    })
                  }}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  required
                >
                  <option value="">-- Select User --</option>
                  {users.map(u => (
                    <option key={u.id} value={u.id}>{u.name} ({u.email})</option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Partner Name</label>
                  <input
                    type="text"
                    value={equityForm.partnerName}
                    onChange={(e) => setEquityForm({ ...equityForm, partnerName: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>ICAI Membership No</label>
                  <input
                    type="text"
                    value={equityForm.partnerMembershipNo}
                    onChange={(e) => setEquityForm({ ...equityForm, partnerMembershipNo: e.target.value })}
                    placeholder="e.g. 054128"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Equity Share (%)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={equityForm.equityPercentage}
                    onChange={(e) => setEquityForm({ ...equityForm, equityPercentage: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Capital Account (₹)</label>
                  <input
                    type="number"
                    value={equityForm.capitalAccountBalance}
                    onChange={(e) => setEquityForm({ ...equityForm, capitalAccountBalance: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Role in Deed</label>
                  <select
                    value={equityForm.role}
                    onChange={(e) => setEquityForm({ ...equityForm, role: e.target.value as PartnerTransitionRole })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="SeniorPartner">Senior Partner</option>
                    <option value="ManagingPartner">Managing Partner</option>
                    <option value="JuniorPartner">Junior Partner</option>
                    <option value="SuccessorInTraining">Successor in Training</option>
                    <option value="RetiredHonorary">Retired Honorary</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Vesting Status</label>
                  <select
                    value={equityForm.vestingStatus}
                    onChange={(e) => setEquityForm({ ...equityForm, vestingStatus: e.target.value as PartnerVestingStatus })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="FullyVested">Fully Vested (100%)</option>
                    <option value="VestingInProgress">Vesting in Progress</option>
                    <option value="Retired">Retired</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Deed Joining Date</label>
                  <input
                    type="date"
                    value={equityForm.joiningDate}
                    onChange={(e) => setEquityForm({ ...equityForm, joiningDate: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Target Retirement Date</label>
                  <input
                    type="date"
                    value={equityForm.targetRetirementDate || ''}
                    onChange={(e) => setEquityForm({ ...equityForm, targetRetirementDate: e.target.value || null })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowEquityModal(false)}
                  style={{ padding: '8px 14px', border: '1px solid var(--border-subtle)', borderRadius: '4px', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  style={{ padding: '8px 18px', backgroundColor: '#1e3c72', color: '#ffffff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save to Cap Table
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: PLAN RETIREMENT BUYOUT */}
      {showRetirementModal && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: 'var(--card)',
            borderRadius: '12px',
            width: '550px',
            maxWidth: '95vw',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>
                ⏳ Plan Partner Retirement Buyout
              </h2>
              <button onClick={() => setShowRetirementModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            <form onSubmit={handleCreateRetirementPlan} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Retiring Partner</label>
                <select
                  value={retirementForm.partnerEquityId}
                  onChange={(e) => {
                    const eq = equities.find(x => x.id === e.target.value)
                    setRetirementForm({
                      ...retirementForm,
                      partnerEquityId: e.target.value,
                      capitalAccountRefundAmount: eq?.capitalAccountBalance || 2500000
                    })
                  }}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  required
                >
                  <option value="">-- Select Partner --</option>
                  {equities.map(eq => (
                    <option key={eq.id} value={eq.id}>{eq.partnerName} ({eq.equityPercentage}% Equity)</option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Retirement Date</label>
                  <input
                    type="date"
                    value={retirementForm.retirementDate}
                    onChange={(e) => setRetirementForm({ ...retirementForm, retirementDate: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Agreed Firm Valuation (₹)</label>
                  <input
                    type="number"
                    value={retirementForm.agreedFirmValuationAtRetirement}
                    onChange={(e) => setRetirementForm({ ...retirementForm, agreedFirmValuationAtRetirement: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Capital Account Refund (₹)</label>
                  <input
                    type="number"
                    value={retirementForm.capitalAccountRefundAmount}
                    onChange={(e) => setRetirementForm({ ...retirementForm, capitalAccountRefundAmount: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Payout Duration (Years)</label>
                  <select
                    value={retirementForm.payoutPeriodYears}
                    onChange={(e) => setRetirementForm({ ...retirementForm, payoutPeriodYears: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value={3}>3 Years (Short Term)</option>
                    <option value={5}>5 Years (Standard ICAI Norm)</option>
                    <option value={7}>7 Years (Extended)</option>
                  </select>
                </div>
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Annual Interest Rate on Amortization (%)</label>
                <input
                  type="number"
                  step="0.1"
                  value={retirementForm.annualInterestRatePct}
                  onChange={(e) => setRetirementForm({ ...retirementForm, annualInterestRatePct: Number(e.target.value) })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  required
                />
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Partnership Notes &amp; Resolutions</label>
                <textarea
                  rows={3}
                  value={retirementForm.partnershipNotes || ''}
                  onChange={(e) => setRetirementForm({ ...retirementForm, partnershipNotes: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowRetirementModal(false)}
                  style={{ padding: '8px 14px', border: '1px solid var(--border-subtle)', borderRadius: '4px', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  style={{ padding: '8px 18px', backgroundColor: '#00a86b', color: '#ffffff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}
                >
                  Generate Amortization Plan
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: MAP CLIENT SUCCESSION TRANSITION */}
      {showTransitionModal && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          backgroundColor: 'rgba(0,0,0,0.5)',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          zIndex: 1000
        }}>
          <div style={{
            backgroundColor: 'var(--card)',
            borderRadius: '12px',
            width: '550px',
            maxWidth: '95vw',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>
                🔄 Map Client Succession Transition
              </h2>
              <button onClick={() => setShowTransitionModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            <form onSubmit={handleSaveTransition} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client to Transition</label>
                <select
                  value={transitionForm.clientId}
                  onChange={(e) => setTransitionForm({ ...transitionForm, clientId: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  required
                >
                  <option value="">-- Select Client --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Retiring Partner</label>
                  <select
                    value={transitionForm.retiringPartnerId}
                    onChange={(e) => setTransitionForm({ ...transitionForm, retiringPartnerId: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  >
                    <option value="">-- Select Retiring Partner --</option>
                    {users.map(u => (
                      <option key={u.id} value={u.id}>{u.name}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Successor Partner</label>
                  <select
                    value={transitionForm.successorPartnerId}
                    onChange={(e) => setTransitionForm({ ...transitionForm, successorPartnerId: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  >
                    <option value="">-- Select Successor Partner --</option>
                    {users.map(u => (
                      <option key={u.id} value={u.id}>{u.name}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Annual Client Fee (₹)</label>
                  <input
                    type="number"
                    value={transitionForm.annualClientFees}
                    onChange={(e) => setTransitionForm({ ...transitionForm, annualClientFees: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Target Handover Date</label>
                  <input
                    type="date"
                    value={transitionForm.targetHandoverDate}
                    onChange={(e) => setTransitionForm({ ...transitionForm, targetHandoverDate: e.target.value })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                    required
                  />
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Transition Stage</label>
                  <select
                    value={transitionForm.stage}
                    onChange={(e) => setTransitionForm({ ...transitionForm, stage: e.target.value as TransitionStage })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Identified">1. Identified</option>
                    <option value="IntroductoryMeetingDone">2. Introductory Meeting Done</option>
                    <option value="ShadowingAudit">3. Shadowing Current Year Engagement</option>
                    <option value="JointSignOff">4. Joint Sign-Off Phase</option>
                    <option value="FullyHandedOver">5. Fully Handed Over</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client Churn Risk</label>
                  <select
                    value={transitionForm.risk}
                    onChange={(e) => setTransitionForm({ ...transitionForm, risk: e.target.value as ClientRetentionRisk })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="Low">Low Risk (Strong Institutional Tie)</option>
                    <option value="Medium">Medium Risk (Personal Relationship with Retiring Partner)</option>
                    <option value="High">High Risk (Key-man Risk)</option>
                  </select>
                </div>
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Handover Action Notes</label>
                <textarea
                  rows={2}
                  value={transitionForm.handoverNotes || ''}
                  onChange={(e) => setTransitionForm({ ...transitionForm, handoverNotes: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border-subtle)' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowTransitionModal(false)}
                  style={{ padding: '8px 14px', border: '1px solid var(--border-subtle)', borderRadius: '4px', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  style={{ padding: '8px 18px', backgroundColor: '#1e3c72', color: '#ffffff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}
                >
                  Save Transition
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
export default PracticeHealthDeskPage
