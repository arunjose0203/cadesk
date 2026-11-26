import { useState, useEffect, useMemo, useCallback, type FormEvent } from 'react'
import {
  uaeTaxApi,
  errorMessage,
  api,
  type UaeTaxProfileDto,
  type CreateOrUpdateTaxProfileRequest,
  type CalculateUaeTaxRequest,
  type UaeComputationResult,
  type SaveUaeCorporateTaxReturnRequest,
  type UaeCorporateTaxReturnDto,
  type EmaraTaxSubmissionExportDto,
  type UaeTaxpayerCategory,
  type SmallBusinessReliefStatus,
  type Client
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

const EMIRATES = [
  'Dubai',
  'Abu Dhabi',
  'Sharjah',
  'Ajman',
  'Ras Al Khaimah',
  'Fujairah',
  'Umm Al Quwain'
]

const FREE_ZONES = [
  'Mainland',
  'DIFC (Dubai International Financial Centre)',
  'ADGM (Abu Dhabi Global Market)',
  'DMCC (Dubai Multi Commodities Centre)',
  'JAFZA (Jebel Ali Free Zone)',
  'DAFZA (Dubai Airport Freezone)',
  'Dubai Silicon Oasis (DSO)',
  'Dubai Internet City / Media City (TECOM)',
  'Sharjah Airport International Free Zone (SAIF)',
  'Hamriyah Free Zone',
  'RAK Economic Zone (RAKEZ)',
  'Ajman Free Zone',
  'Fujairah Free Zone'
]

export function UaeCorporateTaxDesk() {
  const [activeTab, setActiveTab] = useState<'profiles' | 'calculator' | 'returns'>('profiles')
  const [profiles, setProfiles] = useState<UaeTaxProfileDto[]>([])
  const [clients, setClients] = useState<Client[]>([])
  const [selectedProfileId, setSelectedProfileId] = useState<string>('')
  const [returns, setReturns] = useState<UaeCorporateTaxReturnDto[]>([])
  const [selectedReturn, setSelectedReturn] = useState<UaeCorporateTaxReturnDto | null>(null)
  const [loading, setLoading] = useState(true)
  const [notice, setNotice] = useState<string | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  // Registration Modal State
  const [showProfileModal, setShowProfileModal] = useState(false)
  const [profileForm, setProfileForm] = useState<CreateOrUpdateTaxProfileRequest>({
    clientId: '',
    corporateTaxTrn: '100',
    emirate: 'Dubai',
    freeZoneName: 'Mainland',
    category: 'ResidentJuridicalPerson',
    freeZoneRegime: 'Mainland',
    financialYearStartMonth: 1,
    isMneGroupConstituent: false,
    accountingStandard: 'IFRS',
    auditedFinancialsAvailable: true
  })

  // Return Studio / Calculator State
  const [calcForm, setCalcForm] = useState<CalculateUaeTaxRequest>({
    category: 'ResidentJuridicalPerson',
    grossRevenue: 5000000,
    costOfSales: 2200000,
    operatingExpenses: 1100000,
    netAccountingProfitLoss: 1700000,
    smallBusinessReliefElection: 'NotEligible',
    priorYear1Revenue: 0,
    priorYear2Revenue: 0,
    isMneConstituent: false,
    qualifyingRevenue: 0,
    nonQualifyingRevenue: 0,
    exemptIncome: 0,
    unrealizedGainsLossesAdjustment: 0,
    entertainmentExpenditureTotal: 40000,
    finesAndPenaltiesDisallowed: 15000,
    recoverableVatDisallowed: 5000,
    donationsToUnapprovedEntitiesDisallowed: 10000,
    netInterestExpense: 250000,
    adjustedEbitda: 2000000,
    broughtForwardTaxLosses: 0,
    foreignTaxCreditReliefAed: 0,
    exchangeRateToInr: 22.85
  })

  const [returnMeta, setReturnMeta] = useState({
    returnYear: new Date().getFullYear(),
    taxPeriodStart: `${new Date().getFullYear()}-01-01`,
    taxPeriodEnd: `${new Date().getFullYear()}-12-31`
  })

  // Real-time calculation result
  const [liveCalcResult, setLiveCalcResult] = useState<UaeComputationResult | null>(null)
  const [calculating, setCalculating] = useState(false)

  // Partner Sign-Off Modal State
  const [showApproveModal, setShowApproveModal] = useState(false)
  const [partnerNotes, setPartnerNotes] = useState('')

  // EmaraTax Export Drawer / Modal State
  const [showExportModal, setShowExportModal] = useState(false)
  const [exportPackage, setExportPackage] = useState<EmaraTaxSubmissionExportDto | null>(null)
  const [exportFormatTab, setExportFormatTab] = useState<'xml' | 'json'>('xml')

  // Search and filter
  const [profileSearch, setProfileSearch] = useState('')

  const loadData = useCallback(async () => {
    setLoading(true)
    setErrorMsg(null)
    try {
      const [profilesData, clientsRes] = await Promise.all([
        uaeTaxApi.getProfiles(),
        api<ClientPage>('/api/clients?page=1&limit=200').catch(() => ({ items: [], total: 0 }))
      ])
      setProfiles(profilesData)
      setClients(clientsRes.items || [])
      if (profilesData.length > 0) {
        setSelectedProfileId(prev => prev || profilesData[0].id)
      }
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to load UAE Tax data.'))
    } finally {
      setLoading(false)
    }
  }, [])

  // Load Initial Data
  useEffect(() => {
    loadData()
  }, [loadData])

  // Load returns whenever selected profile changes
  useEffect(() => {
    if (selectedProfileId) {
      loadProfileReturns(selectedProfileId)
      const p = profiles.find(x => x.id === selectedProfileId)
      if (p) {
        setCalcForm(prev => ({
          ...prev,
          category: p.category,
          isMneConstituent: p.isMneGroupConstituent
        }))
      }
    }
  }, [selectedProfileId, profiles])

  const loadProfileReturns = async (profileId: string) => {
    try {
      const retList = await uaeTaxApi.getProfileReturns(profileId)
      setReturns(retList)
      if (retList.length > 0) {
        setSelectedReturn(retList[0])
      } else {
        setSelectedReturn(null)
      }
    } catch (err: unknown) {
      console.error(err)
    }
  }

  const recomputeTax = useCallback(async () => {
    try {
      setCalculating(true)
      const netProfit = calcForm.grossRevenue - calcForm.costOfSales - calcForm.operatingExpenses
      const updatedReq = {
        ...calcForm,
        netAccountingProfitLoss: netProfit
      }
      const res = await uaeTaxApi.calculateTax(updatedReq)
      setLiveCalcResult(res)
    } catch (err: unknown) {
      console.error('Calculation error', err)
    } finally {
      setCalculating(false)
    }
  }, [calcForm])

  // Trigger real-time calculation whenever calcForm changes
  useEffect(() => {
    const timer = setTimeout(() => {
      recomputeTax()
    }, 200)
    return () => clearTimeout(timer)
  }, [recomputeTax])

  // Handle Profile Save
  const handleSaveProfile = async (e: FormEvent) => {
    e.preventDefault()
    setErrorMsg(null)
    if (!profileForm.clientId) {
      setErrorMsg('Please select a client.')
      return
    }
    if (!/^100\d{12}$/.test(profileForm.corporateTaxTrn)) {
      setErrorMsg('FTA Corporate Tax TRN must be exactly 15 numeric digits starting with 100.')
      return
    }
    try {
      const created = await uaeTaxApi.saveProfile(profileForm)
      setNotice(`Tax Profile for ${created.clientName} saved successfully (TRN: ${created.corporateTaxTrn}).`)
      setShowProfileModal(false)
      await loadData()
      setSelectedProfileId(created.id)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to save UAE Tax Profile.'))
    }
  }

  // Handle Save Return
  const handleSaveReturn = async () => {
    if (!selectedProfileId) {
      setErrorMsg('Please select a Tax Profile first.')
      return
    }
    setErrorMsg(null)
    try {
      const req: SaveUaeCorporateTaxReturnRequest = {
        taxProfileId: selectedProfileId,
        returnYear: returnMeta.returnYear,
        taxPeriodStart: returnMeta.taxPeriodStart,
        taxPeriodEnd: returnMeta.taxPeriodEnd,
        grossRevenue: Number(calcForm.grossRevenue) || 0,
        costOfSales: Number(calcForm.costOfSales) || 0,
        operatingExpenses: Number(calcForm.operatingExpenses) || 0,
        netAccountingProfitLoss: Number(calcForm.grossRevenue - calcForm.costOfSales - calcForm.operatingExpenses) || 0,
        smallBusinessReliefElection: calcForm.smallBusinessReliefElection,
        priorYear1Revenue: Number(calcForm.priorYear1Revenue) || 0,
        priorYear2Revenue: Number(calcForm.priorYear2Revenue) || 0,
        qualifyingRevenue: Number(calcForm.qualifyingRevenue) || 0,
        nonQualifyingRevenue: Number(calcForm.nonQualifyingRevenue) || 0,
        exemptIncome: Number(calcForm.exemptIncome) || 0,
        unrealizedGainsLossesAdjustment: Number(calcForm.unrealizedGainsLossesAdjustment) || 0,
        entertainmentExpenditureTotal: Number(calcForm.entertainmentExpenditureTotal) || 0,
        finesAndPenaltiesDisallowed: Number(calcForm.finesAndPenaltiesDisallowed) || 0,
        recoverableVatDisallowed: Number(calcForm.recoverableVatDisallowed) || 0,
        donationsToUnapprovedEntitiesDisallowed: Number(calcForm.donationsToUnapprovedEntitiesDisallowed) || 0,
        netInterestExpense: Number(calcForm.netInterestExpense) || 0,
        adjustedEbitda: Number(calcForm.adjustedEbitda) || 0,
        broughtForwardTaxLosses: Number(calcForm.broughtForwardTaxLosses) || 0,
        foreignTaxCreditReliefAed: Number(calcForm.foreignTaxCreditReliefAed) || 0,
        exchangeRateToInr: Number(calcForm.exchangeRateToInr) || 22.85
      }
      const saved = await uaeTaxApi.saveReturn(req)
      setNotice(`UAE Corporate Tax Return (Form CT01 Ref: ${saved.returnReference}) created and saved!`)
      await loadProfileReturns(selectedProfileId)
      setSelectedReturn(saved)
      setActiveTab('returns')
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to save Corporate Tax Return.'))
    }
  }

  // Handle Partner Approval
  const handleApproveReturn = async (e: FormEvent) => {
    e.preventDefault()
    if (!selectedReturn) return
    setErrorMsg(null)
    try {
      const approved = await uaeTaxApi.approveReturn(selectedReturn.id, { partnerNotes })
      setNotice(`Return ${approved.returnReference} has been formally signed off and approved by Partner!`)
      setShowApproveModal(false)
      setPartnerNotes('')
      await loadProfileReturns(selectedProfileId)
      setSelectedReturn(approved)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Partner approval failed.'))
    }
  }

  // Handle EmaraTax Export
  const handleOpenExport = async (ret: UaeCorporateTaxReturnDto) => {
    setErrorMsg(null)
    try {
      const pkg = await uaeTaxApi.exportEmaraTax(ret.id)
      setExportPackage(pkg)
      setShowExportModal(true)
    } catch (err: unknown) {
      setErrorMsg(errorMessage(err, 'Failed to generate EmaraTax submission package.'))
    }
  }

  // Download XML or JSON
  const handleDownloadFile = (content: string, filename: string, mime: string) => {
    const blob = new Blob([content], { type: mime })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = filename
    document.body.appendChild(a)
    a.click()
    document.body.removeChild(a)
    URL.revokeObjectURL(url)
  }

  // Filtered Profiles
  const filteredProfiles = useMemo(() => {
    return profiles.filter(p =>
      p.clientName.toLowerCase().includes(profileSearch.toLowerCase()) ||
      p.corporateTaxTrn.includes(profileSearch) ||
      p.freeZoneName.toLowerCase().includes(profileSearch.toLowerCase())
    )
  }, [profiles, profileSearch])

  const selectedProfile = useMemo(() => {
    return profiles.find(p => p.id === selectedProfileId)
  }, [profiles, selectedProfileId])

  const isTrnValid = useMemo(() => {
    return /^100\d{12}$/.test(profileForm.corporateTaxTrn)
  }, [profileForm.corporateTaxTrn])

  return (
    <div className="page-container" style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto' }}>
      {/* Header Banner */}
      <div style={{
        background: 'linear-gradient(135deg, #0b2545 0%, #134074 50%, #1d2d44 100%)',
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
            <span style={{ fontSize: '32px' }}>🇦🇪</span>
            <h1 style={{ margin: 0, fontSize: '24px', fontWeight: 700, letterSpacing: '-0.02em' }}>
              UAE Corporate Tax &amp; EmaraTax Desk
            </h1>
            <span style={{
              background: '#00a86b',
              color: '#ffffff',
              fontSize: '12px',
              padding: '4px 10px',
              borderRadius: '20px',
              fontWeight: 600
            }}>
              Federal Decree-Law No. 47 of 2022
            </span>
          </div>
          <p style={{ margin: 0, opacity: 0.9, fontSize: '14px', maxWidth: '850px', lineHeight: 1.5 }}>
            Automated statutory 9% UAE Corporate Tax calculation engine, Qualifying Free Zone Person (QFZP) 0% vs 9% De Minimis allocation,
            MD 73/2023 Small Business Relief (AED 3M ceiling), Article 30 interest limitation, Article 37 loss offset, and 1-click FTA EmaraTax Form CT01 export.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '12px' }}>
          <button
            onClick={() => setShowProfileModal(true)}
            style={{
              backgroundColor: '#00a86b',
              color: '#ffffff',
              border: 'none',
              borderRadius: '8px',
              padding: '10px 18px',
              fontWeight: 600,
              cursor: 'pointer',
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              boxShadow: '0 2px 8px rgba(0, 168, 107, 0.4)'
            }}
          >
            <span>+</span> Register UAE Taxpayer
          </button>
        </div>
      </div>

      {/* Loading & Notifications */}
      {loading && (
        <div style={{ fontSize: '13px', color: 'var(--muted)', marginBottom: '12px', display: 'flex', alignItems: 'center', gap: '8px' }}>
          <span>⏳</span> Loading UAE tax profiles and client records...
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
          onClick={() => setActiveTab('profiles')}
          style={{
            padding: '12px 20px',
            fontWeight: 600,
            fontSize: '15px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            borderBottom: activeTab === 'profiles' ? '3px solid var(--accent)' : '3px solid transparent',
            color: activeTab === 'profiles' ? 'var(--accent)' : 'var(--muted)'
          }}
        >
          🏢 Taxpayer Profiles &amp; FTA TRN ({profiles.length})
        </button>
        <button
          onClick={() => setActiveTab('calculator')}
          style={{
            padding: '12px 20px',
            fontWeight: 600,
            fontSize: '15px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            borderBottom: activeTab === 'calculator' ? '3px solid var(--accent)' : '3px solid transparent',
            color: activeTab === 'calculator' ? 'var(--accent)' : 'var(--muted)'
          }}
        >
          🧮 Corporate Tax Studio &amp; Form CT01 Calculator
        </button>
        <button
          onClick={() => setActiveTab('returns')}
          style={{
            padding: '12px 20px',
            fontWeight: 600,
            fontSize: '15px',
            border: 'none',
            background: 'none',
            cursor: 'pointer',
            borderBottom: activeTab === 'returns' ? '3px solid var(--accent)' : '3px solid transparent',
            color: activeTab === 'returns' ? 'var(--accent)' : 'var(--muted)'
          }}
        >
          🏛️ Returns, Partner Attestation &amp; EmaraTax Export ({returns.length})
        </button>
      </div>

      {/* Profile Selector Toolbar */}
      <div style={{
        backgroundColor: 'var(--card-subtle)',
        border: '1px solid var(--border)',
        borderRadius: '8px',
        padding: '12px 18px',
        marginBottom: '20px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: '12px'
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <label style={{ fontWeight: 600, color: 'var(--text-secondary)', fontSize: '14px' }}>Active UAE Client:</label>
          <select
            value={selectedProfileId}
            onChange={(e) => setSelectedProfileId(e.target.value)}
            style={{
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid var(--border-subtle)',
              backgroundColor: 'var(--card)',
              fontSize: '14px',
              fontWeight: 500,
              minWidth: '280px'
            }}
          >
            {profiles.length === 0 && <option value="">No UAE Tax Profiles Registered</option>}
            {profiles.map(p => (
              <option key={p.id} value={p.id}>
                {p.clientName} (TRN: {p.corporateTaxTrn} - {p.category === 'QualifyingFreeZonePerson' ? 'QFZP' : 'Mainland'})
              </option>
            ))}
          </select>
        </div>

        {selectedProfile && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px', fontSize: '13px', color: 'var(--text-secondary)' }}>
            <span><strong>Emirate:</strong> {selectedProfile.emirate}</span>
            <span><strong>Zone:</strong> {selectedProfile.freeZoneName}</span>
            <span><strong>Accounting:</strong> {selectedProfile.accountingStandard}</span>
            <span style={{
              padding: '2px 8px',
              borderRadius: '12px',
              fontWeight: 600,
              backgroundColor: selectedProfile.category === 'QualifyingFreeZonePerson' ? 'var(--accent-subtle)' : 'var(--card-subtle)',
              color: selectedProfile.category === 'QualifyingFreeZonePerson' ? 'var(--accent)' : 'var(--text-secondary)'
            }}>
              {selectedProfile.category}
            </span>
          </div>
        )}
      </div>

      {/* TAB 1: TAXPAYER PROFILES */}
      {activeTab === 'profiles' && (
        <div>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <input
              type="text"
              placeholder="Search by client name, TRN, or free zone..."
              value={profileSearch}
              onChange={(e) => setProfileSearch(e.target.value)}
              style={{
                padding: '10px 14px',
                borderRadius: '6px',
                border: '1px solid var(--border-subtle)',
                width: '360px',
                fontSize: '14px'
              }}
            />
            <span style={{ fontSize: '13px', color: 'var(--muted)' }}>
              Showing {filteredProfiles.length} of {profiles.length} profiles
            </span>
          </div>

          <div style={{
            backgroundColor: 'var(--card)',
            borderRadius: '8px',
            border: '1px solid var(--border)',
            overflow: 'hidden',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
          }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '14px' }}>
              <thead>
                <tr style={{ backgroundColor: 'var(--card-subtle)', color: 'var(--text-secondary)', borderBottom: '1px solid var(--border)' }}>
                  <th style={{ padding: '12px 16px', fontWeight: 600 }}>Client Name &amp; Code</th>
                  <th style={{ padding: '12px 16px', fontWeight: 600 }}>FTA Corporate Tax TRN</th>
                  <th style={{ padding: '12px 16px', fontWeight: 600 }}>Taxpayer Category</th>
                  <th style={{ padding: '12px 16px', fontWeight: 600 }}>Emirate &amp; Jurisdiction</th>
                  <th style={{ padding: '12px 16px', fontWeight: 600 }}>Financial Year</th>
                  <th style={{ padding: '12px 16px', fontWeight: 600 }}>MNE Group</th>
                  <th style={{ padding: '12px 16px', fontWeight: 600 }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredProfiles.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                      No UAE Tax Profiles registered yet. Click "+ Register UAE Taxpayer" above to onboard a client.
                    </td>
                  </tr>
                ) : (
                  filteredProfiles.map((p) => (
                    <tr
                      key={p.id}
                      style={{
                        borderBottom: '1px solid var(--border)',
                        backgroundColor: selectedProfileId === p.id ? '#f0f9ff' : 'transparent',
                        cursor: 'pointer'
                      }}
                      onClick={() => setSelectedProfileId(p.id)}
                    >
                      <td style={{ padding: '12px 16px', fontWeight: 600, color: 'var(--text)' }}>
                        {p.clientName}
                        <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 400 }}>Code: {p.clientCode}</div>
                      </td>
                      <td style={{ padding: '12px 16px', fontFamily: 'monospace', fontWeight: 600, color: 'var(--accent)' }}>
                        {p.corporateTaxTrn}
                      </td>
                      <td style={{ padding: '12px 16px' }}>
                        <span style={{
                          display: 'inline-block',
                          padding: '3px 8px',
                          borderRadius: '12px',
                          fontSize: '12px',
                          fontWeight: 600,
                          backgroundColor: p.category === 'QualifyingFreeZonePerson' ? 'var(--success-bg)' : 'var(--card-subtle)',
                          color: p.category === 'QualifyingFreeZonePerson' ? 'var(--success-text)' : 'var(--text-secondary)',
                          border: p.category === 'QualifyingFreeZonePerson' ? '1px solid var(--success-border)' : '1px solid var(--border)'
                        }}>
                          {p.category}
                        </span>
                      </td>
                      <td style={{ padding: '12px 16px' }}>
                        <div style={{ fontWeight: 500 }}>{p.emirate}</div>
                        <div style={{ fontSize: '12px', color: 'var(--muted)' }}>{p.freeZoneName}</div>
                      </td>
                      <td style={{ padding: '12px 16px', color: 'var(--text-secondary)' }}>
                        Month {p.financialYearStartMonth} ({p.financialYearStartMonth === 1 ? 'Jan - Dec' : 'Apr - Mar'})
                      </td>
                      <td style={{ padding: '12px 16px' }}>
                        {p.isMneGroupConstituent ? (
                          <span style={{ color: 'var(--warning)', fontWeight: 600, fontSize: '12px' }}>Yes (≥ AED 3.15B)</span>
                        ) : (
                          <span style={{ color: 'var(--muted)', fontSize: '12px' }}>No</span>
                        )}
                      </td>
                      <td style={{ padding: '12px 16px' }}>
                        <button
                          onClick={(e) => {
                            e.stopPropagation()
                            setSelectedProfileId(p.id)
                            setActiveTab('calculator')
                          }}
                          style={{
                            padding: '6px 12px',
                            backgroundColor: '#134074',
                            color: '#ffffff',
                            border: 'none',
                            borderRadius: '4px',
                            fontSize: '12px',
                            fontWeight: 600,
                            cursor: 'pointer'
                          }}
                        >
                          Open Studio ➔
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 2: CORPORATE TAX STUDIO & RETURN STUDIO (FORM CT01) */}
      {activeTab === 'calculator' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1.2fr 0.8fr', gap: '24px' }}>
          {/* Left Column: Form & Adjustments */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
            {/* Return Period & Metadata */}
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <h3 style={{ margin: '0 0 16px 0', fontSize: '16px', color: 'var(--text)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span>📅</span> Tax Period &amp; Currency Settings (IAS 21)
              </h3>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Return Year</label>
                  <input
                    type="number"
                    value={returnMeta.returnYear}
                    onChange={(e) => setReturnMeta({ ...returnMeta, returnYear: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Period Start</label>
                  <input
                    type="date"
                    value={returnMeta.taxPeriodStart}
                    onChange={(e) => setReturnMeta({ ...returnMeta, taxPeriodStart: e.target.value })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Period End</label>
                  <input
                    type="date"
                    value={returnMeta.taxPeriodEnd}
                    onChange={(e) => setReturnMeta({ ...returnMeta, taxPeriodEnd: e.target.value })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>AED / INR Rate</label>
                  <input
                    type="number"
                    step="0.01"
                    value={calcForm.exchangeRateToInr}
                    onChange={(e) => setCalcForm({ ...calcForm, exchangeRateToInr: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
              </div>
            </div>

            {/* Section 1: Commercial P&L */}
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <h3 style={{ margin: '0 0 16px 0', fontSize: '16px', color: 'var(--text)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span>📊</span> 1. Commercial Profit &amp; Loss (IFRS Accounting Net Income)
              </h3>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Gross Revenue (AED)</label>
                  <input
                    type="number"
                    value={calcForm.grossRevenue}
                    onChange={(e) => setCalcForm({ ...calcForm, grossRevenue: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px', fontWeight: 600 }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Cost of Sales (AED)</label>
                  <input
                    type="number"
                    value={calcForm.costOfSales}
                    onChange={(e) => setCalcForm({ ...calcForm, costOfSales: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Gross Profit (AED)</label>
                  <div style={{ padding: '8px', backgroundColor: 'var(--card-subtle)', border: '1px solid var(--border)', borderRadius: '4px', fontWeight: 600, color: 'var(--text)' }}>
                    {(calcForm.grossRevenue - calcForm.costOfSales).toLocaleString()} AED
                  </div>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Operating Expenses (AED)</label>
                  <input
                    type="number"
                    value={calcForm.operatingExpenses}
                    onChange={(e) => setCalcForm({ ...calcForm, operatingExpenses: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Net Accounting Profit / Loss (AED)</label>
                  <div style={{
                    padding: '8px',
                    backgroundColor: (calcForm.grossRevenue - calcForm.costOfSales - calcForm.operatingExpenses) >= 0 ? 'var(--success-bg)' : 'var(--error-bg)',
                    border: '1px solid var(--border)',
                    borderRadius: '4px',
                    fontWeight: 700,
                    color: (calcForm.grossRevenue - calcForm.costOfSales - calcForm.operatingExpenses) >= 0 ? 'var(--success-text)' : 'var(--error-text)'
                  }}>
                    {(calcForm.grossRevenue - calcForm.costOfSales - calcForm.operatingExpenses).toLocaleString()} AED
                  </div>
                </div>
              </div>
            </div>

            {/* Section 2: Small Business Relief (MD 73/2023) */}
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                <h3 style={{ margin: 0, fontSize: '16px', color: 'var(--text)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <span>🛡️</span> 2. Small Business Relief (Ministerial Decision No. 73 of 2023)
                </h3>
                {liveCalcResult?.sbrEligible && (
                  <span style={{ backgroundColor: 'var(--success-bg)', color: 'var(--success-text)', border: '1px solid var(--success-border)', padding: '3px 8px', borderRadius: '12px', fontSize: '12px', fontWeight: 600 }}>
                    ✓ SBR Qualified (≤ AED 3M)
                  </span>
                )}
              </div>
              <p style={{ margin: '0 0 16px 0', fontSize: '13px', color: 'var(--muted)' }}>
                Eligible resident businesses with gross revenue not exceeding AED 3,000,000 in current and prior periods can elect for SBR to treat taxable income as AED 0.
              </p>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>SBR Election Status</label>
                  <select
                    value={calcForm.smallBusinessReliefElection}
                    onChange={(e) => setCalcForm({ ...calcForm, smallBusinessReliefElection: e.target.value as SmallBusinessReliefStatus })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px', backgroundColor: 'var(--card)' }}
                  >
                    <option value="NotEligible">Not Eligible</option>
                    <option value="EligibleNotElected">Eligible (Do Not Elect)</option>
                    <option value="Elected">Elected (0% CT &amp; No Taxable Income)</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Prior Year 1 Revenue (AED)</label>
                  <input
                    type="number"
                    value={calcForm.priorYear1Revenue}
                    onChange={(e) => setCalcForm({ ...calcForm, priorYear1Revenue: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Prior Year 2 Revenue (AED)</label>
                  <input
                    type="number"
                    value={calcForm.priorYear2Revenue}
                    onChange={(e) => setCalcForm({ ...calcForm, priorYear2Revenue: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
              </div>
            </div>

            {/* Section 3: Free Zone Persons (Cabinet Decision No. 55/2023) */}
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: calcForm.category === 'QualifyingFreeZonePerson' ? '2px solid var(--accent)' : '1px solid var(--border)',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                <h3 style={{ margin: 0, fontSize: '16px', color: 'var(--text)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <span>🏝️</span> 3. Free Zone Regime &amp; De Minimis Test (CD 55/2023 &amp; MD 139/2023)
                </h3>
                {calcForm.category === 'QualifyingFreeZonePerson' && (
                  <span style={{
                    backgroundColor: liveCalcResult?.qfzpDeMinimisPassed ? 'var(--success-bg)' : 'var(--error-bg)',
                    color: liveCalcResult?.qfzpDeMinimisPassed ? 'var(--success-text)' : 'var(--error-text)',
                    border: liveCalcResult?.qfzpDeMinimisPassed ? '1px solid var(--success-border)' : '1px solid var(--error-border)',
                    padding: '3px 8px',
                    borderRadius: '12px',
                    fontSize: '12px',
                    fontWeight: 600
                  }}>
                    {liveCalcResult?.qfzpDeMinimisPassed ? '✓ De Minimis Met (0% QFZP)' : '✕ De Minimis Failed (Taxed at 9%)'}
                  </span>
                )}
              </div>
              <p style={{ margin: '0 0 16px 0', fontSize: '13px', color: 'var(--muted)' }}>
                Non-qualifying revenue must not exceed the lower of 5% of total revenue or AED 5,000,000.
                {liveCalcResult && liveCalcResult.deMinimisThresholdAed > 0 && (
                  <strong style={{ color: 'var(--accent)' }}> Current Threshold: {liveCalcResult.deMinimisThresholdAed.toLocaleString()} AED.</strong>
                )}
              </p>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Taxpayer Category</label>
                  <select
                    value={calcForm.category}
                    onChange={(e) => setCalcForm({ ...calcForm, category: e.target.value as UaeTaxpayerCategory })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px', backgroundColor: 'var(--card)' }}
                  >
                    <option value="ResidentJuridicalPerson">Resident Mainland Juridical Person</option>
                    <option value="QualifyingFreeZonePerson">Qualifying Free Zone Person (QFZP)</option>
                    <option value="NonResidentJuridicalPerson">Non-Resident Juridical Person (PE in UAE)</option>
                    <option value="ResidentNaturalPerson">Resident Natural Person (Sole Estab)</option>
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Qualifying Revenue (0% Rate)</label>
                  <input
                    type="number"
                    value={calcForm.qualifyingRevenue}
                    onChange={(e) => setCalcForm({ ...calcForm, qualifyingRevenue: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                    disabled={calcForm.category !== 'QualifyingFreeZonePerson'}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Non-Qualifying Revenue (9% Rate)</label>
                  <input
                    type="number"
                    value={calcForm.nonQualifyingRevenue}
                    onChange={(e) => setCalcForm({ ...calcForm, nonQualifyingRevenue: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                    disabled={calcForm.category !== 'QualifyingFreeZonePerson'}
                  />
                </div>
              </div>
            </div>

            {/* Section 4: Statutory Tax Adjustments */}
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <h3 style={{ margin: '0 0 16px 0', fontSize: '16px', color: 'var(--text)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span>⚖️</span> 4. Statutory Tax Adjustments (Additions &amp; Deductions)
              </h3>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px', marginBottom: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Entertainment Expenses (Art. 32)
                  </label>
                  <input
                    type="number"
                    value={calcForm.entertainmentExpenditureTotal}
                    onChange={(e) => setCalcForm({ ...calcForm, entertainmentExpenditureTotal: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--warning)' }}>50% disallowed: {(calcForm.entertainmentExpenditureTotal * 0.5).toLocaleString()} AED</span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Fines &amp; Penalties (Art. 33 - 100%)
                  </label>
                  <input
                    type="number"
                    value={calcForm.finesAndPenaltiesDisallowed}
                    onChange={(e) => setCalcForm({ ...calcForm, finesAndPenaltiesDisallowed: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--warning)' }}>100% disallowed</span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Recoverable VAT Disallowed
                  </label>
                  <input
                    type="number"
                    value={calcForm.recoverableVatDisallowed}
                    onChange={(e) => setCalcForm({ ...calcForm, recoverableVatDisallowed: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--warning)' }}>100% disallowed</span>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Unapproved Donations (AED)
                  </label>
                  <input
                    type="number"
                    value={calcForm.donationsToUnapprovedEntitiesDisallowed}
                    onChange={(e) => setCalcForm({ ...calcForm, donationsToUnapprovedEntitiesDisallowed: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Exempt Income (Dividends/PE)
                  </label>
                  <input
                    type="number"
                    value={calcForm.exemptIncome}
                    onChange={(e) => setCalcForm({ ...calcForm, exemptIncome: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--success-text)' }}>Statutory deduction</span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>
                    Unrealized Gains/Losses Adj.
                  </label>
                  <input
                    type="number"
                    value={calcForm.unrealizedGainsLossesAdjustment}
                    onChange={(e) => setCalcForm({ ...calcForm, unrealizedGainsLossesAdjustment: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                </div>
              </div>
            </div>

            {/* Section 5: Interest Limitation & Losses */}
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '20px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <h3 style={{ margin: '0 0 16px 0', fontSize: '16px', color: 'var(--text)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: '8px' }}>
                <span>📉</span> 5. Interest Capping (Art. 30 GIDLR) &amp; Tax Losses (Art. 37)
              </h3>
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '16px' }}>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Net Interest Expense (AED)</label>
                  <input
                    type="number"
                    value={calcForm.netInterestExpense}
                    onChange={(e) => setCalcForm({ ...calcForm, netInterestExpense: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  {liveCalcResult && liveCalcResult.disallowedInterestAed > 0 && (
                    <span style={{ fontSize: '11px', color: 'var(--warning)' }}>Disallowed: {liveCalcResult.disallowedInterestAed.toLocaleString()} AED</span>
                  )}
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Adjusted EBITDA (AED)</label>
                  <input
                    type="number"
                    value={calcForm.adjustedEbitda}
                    onChange={(e) => setCalcForm({ ...calcForm, adjustedEbitda: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--muted)' }}>Cap: Max(AED 12M, 30% EBITDA)</span>
                </div>
                <div>
                  <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px', fontWeight: 500 }}>Brought Forward Tax Losses (AED)</label>
                  <input
                    type="number"
                    value={calcForm.broughtForwardTaxLosses}
                    onChange={(e) => setCalcForm({ ...calcForm, broughtForwardTaxLosses: Number(e.target.value) })}
                    style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                  />
                  <span style={{ fontSize: '11px', color: 'var(--muted)' }}>Max 75% offset under Art. 37</span>
                </div>
              </div>
            </div>
          </div>

          {/* Right Column: Live Tax Computation Summary Card */}
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
                <h3 style={{ margin: 0, fontSize: '18px', color: '#0b2545', fontWeight: 700 }}>
                  🇦🇪 Tax Computation Summary
                </h3>
                {calculating && <span style={{ fontSize: '12px', color: 'var(--accent)' }}>Recalculating...</span>}
              </div>

              {liveCalcResult ? (
                <div>
                  <div style={{ borderBottom: '1px solid var(--border)', paddingBottom: '12px', marginBottom: '12px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--muted)', marginBottom: '6px' }}>
                      <span>Net Accounting Profit (IFRS):</span>
                      <span style={{ fontWeight: 600, color: 'var(--text)' }}>
                        {(calcForm.grossRevenue - calcForm.costOfSales - calcForm.operatingExpenses).toLocaleString()} AED
                      </span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--muted)', marginBottom: '6px' }}>
                      <span>Total Statutory Additions:</span>
                      <span style={{ fontWeight: 600, color: 'var(--warning)' }}>
                        + {liveCalcResult.totalAdditionsAed.toLocaleString()} AED
                      </span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--muted)', marginBottom: '6px' }}>
                      <span>Total Statutory Deductions:</span>
                      <span style={{ fontWeight: 600, color: 'var(--success-text)' }}>
                        - {liveCalcResult.totalDeductionsAed.toLocaleString()} AED
                      </span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '14px', fontWeight: 600, color: 'var(--text)' }}>
                      <span>Adjusted Taxable Income:</span>
                      <span>{liveCalcResult.adjustedTaxableIncomeAed.toLocaleString()} AED</span>
                    </div>
                  </div>

                  {/* Losses and SBR */}
                  <div style={{ borderBottom: '1px solid var(--border)', paddingBottom: '12px', marginBottom: '12px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--muted)', marginBottom: '6px' }}>
                      <span>Tax Losses Utilized (Art. 37):</span>
                      <span style={{ fontWeight: 600, color: 'var(--success-text)' }}>
                        - {liveCalcResult.taxLossesUtilizedAed.toLocaleString()} AED
                      </span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', color: 'var(--muted)' }}>
                      <span>Remaining Losses Carried Forward:</span>
                      <span style={{ fontWeight: 500 }}>
                        {liveCalcResult.remainingTaxLossesAed.toLocaleString()} AED
                      </span>
                    </div>
                  </div>

                  {/* Tier Breakdown */}
                  <div style={{ backgroundColor: 'var(--card-subtle)', padding: '12px', borderRadius: '8px', marginBottom: '16px' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginBottom: '6px' }}>
                      <span style={{ color: 'var(--text-secondary)' }}>0% Tier (Exempt / Threshold / QFZP):</span>
                      <span style={{ fontWeight: 600, color: 'var(--success-text)' }}>
                        {liveCalcResult.incomeSubjectToZeroPctAed.toLocaleString()} AED
                      </span>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px' }}>
                      <span style={{ color: 'var(--text-secondary)' }}>9% Tier (Taxable Mainland/Non-Qualifying):</span>
                      <span style={{ fontWeight: 600, color: 'var(--accent)' }}>
                        {liveCalcResult.incomeSubjectToNinePctAed.toLocaleString()} AED
                      </span>
                    </div>
                  </div>

                  {/* Foreign Tax Credit */}
                  <div style={{ marginBottom: '16px' }}>
                    <label style={{ fontSize: '12px', color: 'var(--muted)', display: 'block', marginBottom: '4px' }}>
                      Foreign Tax Credit Relief (AED)
                    </label>
                    <input
                      type="number"
                      value={calcForm.foreignTaxCreditReliefAed}
                      onChange={(e) => setCalcForm({ ...calcForm, foreignTaxCreditReliefAed: Number(e.target.value) })}
                      style={{ width: '100%', padding: '8px', border: '1px solid var(--border-subtle)', borderRadius: '4px' }}
                    />
                  </div>

                  {/* Final Net Tax Banner */}
                  <div style={{
                    backgroundColor: '#0b2545',
                    color: '#ffffff',
                    padding: '16px',
                    borderRadius: '8px',
                    textAlign: 'center',
                    marginBottom: '20px'
                  }}>
                    <div style={{ fontSize: '12px', opacity: 0.8, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                      Net UAE Corporate Tax Payable
                    </div>
                    <div style={{ fontSize: '28px', fontWeight: 800, marginTop: '4px' }}>
                      {liveCalcResult.netTaxPayableAed.toLocaleString()} AED
                    </div>
                    <div style={{ fontSize: '13px', opacity: 0.9, marginTop: '4px', color: '#93c5fd' }}>
                      ≈ ₹ {liveCalcResult.netTaxPayableInr.toLocaleString()} INR (@ {calcForm.exchangeRateToInr} AED/INR)
                    </div>
                  </div>

                  <button
                    onClick={handleSaveReturn}
                    style={{
                      width: '100%',
                      backgroundColor: '#00a86b',
                      color: '#ffffff',
                      border: 'none',
                      padding: '12px',
                      borderRadius: '8px',
                      fontSize: '15px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      boxShadow: '0 2px 8px rgba(0,168,107,0.3)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      gap: '8px'
                    }}
                  >
                    <span>💾</span> Save &amp; Compile Form CT01 Return
                  </button>
                </div>
              ) : (
                <div style={{ textAlign: 'center', color: 'var(--muted)', padding: '32px 0' }}>
                  Calculating statutory tax matrix...
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* TAB 3: RETURNS, PARTNER SIGN-OFF & EMARATAX EXPORT */}
      {activeTab === 'returns' && (
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1.3fr', gap: '24px' }}>
          {/* Left Column: Returns List */}
          <div style={{
            backgroundColor: 'var(--card)',
            borderRadius: '8px',
            border: '1px solid var(--border)',
            overflow: 'hidden',
            boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
          }}>
            <div style={{ padding: '16px', borderBottom: '1px solid var(--border)', backgroundColor: 'var(--card-subtle)' }}>
              <h3 style={{ margin: 0, fontSize: '16px', color: 'var(--text)', fontWeight: 600 }}>
                Filed &amp; Draft Returns for Selected Profile ({returns.length})
              </h3>
            </div>

            {returns.length === 0 ? (
              <div style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>
                No returns compiled for this taxpayer yet. Go to the "Corporate Tax Studio" tab to calculate and save a return.
              </div>
            ) : (
              <div style={{ maxHeight: '600px', overflowY: 'auto' }}>
                {returns.map(ret => (
                  <div
                    key={ret.id}
                    onClick={() => setSelectedReturn(ret)}
                    style={{
                      padding: '16px',
                      borderBottom: '1px solid var(--border)',
                      cursor: 'pointer',
                      backgroundColor: selectedReturn?.id === ret.id ? '#f0f9ff' : 'transparent',
                      transition: 'background 0.2s'
                    }}
                  >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                      <span style={{ fontWeight: 700, color: 'var(--text)' }}>{ret.returnReference}</span>
                      <span style={{
                        padding: '2px 8px',
                        borderRadius: '12px',
                        fontSize: '11px',
                        fontWeight: 600,
                        backgroundColor: ret.status === 'Approved' ? 'var(--success-bg)' : ret.status === 'Exported' ? 'var(--accent-subtle)' : 'var(--warning-bg)',
                        color: ret.status === 'Approved' ? 'var(--success-text)' : ret.status === 'Exported' ? 'var(--accent)' : 'var(--warning-text)'
                      }}>
                        {ret.status}
                      </span>
                    </div>

                    <div style={{ fontSize: '13px', color: 'var(--text-secondary)', marginBottom: '4px' }}>
                      Period: {ret.taxPeriodStart} to {ret.taxPeriodEnd} (Due: {ret.dueDate})
                    </div>

                    <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginTop: '8px' }}>
                      <span style={{ color: 'var(--muted)' }}>Tax Payable:</span>
                      <span style={{ fontWeight: 700, color: '#0b2545' }}>
                        {ret.netTaxPayableAed.toLocaleString()} AED (₹ {ret.netTaxPayableInr.toLocaleString()})
                      </span>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Right Column: Selected Return Detail & Actions */}
          {selectedReturn ? (
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '24px',
              boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '20px' }}>
                <div>
                  <h2 style={{ margin: '0 0 4px 0', fontSize: '20px', color: 'var(--text)', fontWeight: 700 }}>
                    Form CT01: {selectedReturn.returnReference}
                  </h2>
                  <div style={{ fontSize: '13px', color: 'var(--muted)' }}>
                    Taxpayer: <strong>{selectedReturn.clientName}</strong> | TRN: <strong style={{ color: 'var(--accent)' }}>{selectedReturn.corporateTaxTrn}</strong>
                  </div>
                </div>

                <div style={{ display: 'flex', gap: '8px' }}>
                  {selectedReturn.status !== 'Approved' && selectedReturn.status !== 'Exported' && (
                    <button
                      onClick={() => setShowApproveModal(true)}
                      style={{
                        padding: '8px 16px',
                        backgroundColor: '#1e40af',
                        color: '#ffffff',
                        border: 'none',
                        borderRadius: '6px',
                        fontSize: '13px',
                        fontWeight: 600,
                        cursor: 'pointer'
                      }}
                    >
                      ✓ Partner Sign-Off
                    </button>
                  )}
                  <button
                    onClick={() => handleOpenExport(selectedReturn)}
                    style={{
                      padding: '8px 16px',
                      backgroundColor: '#00a86b',
                      color: '#ffffff',
                      border: 'none',
                      borderRadius: '6px',
                      fontSize: '13px',
                      fontWeight: 600,
                      cursor: 'pointer',
                      display: 'flex',
                      alignItems: 'center',
                      gap: '6px'
                    }}
                  >
                    <span>📤</span> EmaraTax Package
                  </button>
                </div>
              </div>

              {/* Status and Sign-off Banner */}
              {selectedReturn.approvedByName && (
                <div style={{
                  backgroundColor: 'var(--success-bg)',
                  border: '1px solid var(--success-border)',
                  borderRadius: '6px',
                  padding: '12px 16px',
                  marginBottom: '20px',
                  fontSize: '13px',
                  color: 'var(--success-text)'
                }}>
                  <div style={{ fontWeight: 600, marginBottom: '2px' }}>
                    ✓ Partner Approved by {selectedReturn.approvedByName} on {new Date(selectedReturn.approvedAt!).toLocaleString()}
                  </div>
                  {selectedReturn.partnerNotes && (
                    <div style={{ fontStyle: 'italic', marginTop: '4px' }}>
                      "{selectedReturn.partnerNotes}"
                    </div>
                  )}
                </div>
              )}

              {/* Grid Breakdown */}
              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '16px', marginBottom: '20px' }}>
                <div style={{ backgroundColor: 'var(--card-subtle)', padding: '12px', borderRadius: '6px' }}>
                  <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Gross Revenue</div>
                  <div style={{ fontSize: '16px', fontWeight: 600, color: 'var(--text)' }}>{selectedReturn.grossRevenue.toLocaleString()} AED</div>
                </div>
                <div style={{ backgroundColor: 'var(--card-subtle)', padding: '12px', borderRadius: '6px' }}>
                  <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Net Accounting Profit</div>
                  <div style={{ fontSize: '16px', fontWeight: 600, color: 'var(--text)' }}>{selectedReturn.netAccountingProfitLoss.toLocaleString()} AED</div>
                </div>
                <div style={{ backgroundColor: 'var(--card-subtle)', padding: '12px', borderRadius: '6px' }}>
                  <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Adjusted Taxable Income</div>
                  <div style={{ fontSize: '16px', fontWeight: 600, color: 'var(--text)' }}>{selectedReturn.adjustedTaxableIncome.toLocaleString()} AED</div>
                </div>
                <div style={{ backgroundColor: 'var(--card-subtle)', padding: '12px', borderRadius: '6px' }}>
                  <div style={{ fontSize: '12px', color: 'var(--muted)', marginBottom: '4px' }}>Tax Losses Utilized (Art. 37)</div>
                  <div style={{ fontSize: '16px', fontWeight: 600, color: 'var(--success-text)' }}>{selectedReturn.taxLossesUtilized.toLocaleString()} AED</div>
                </div>
              </div>

              {/* Tax Tiers & Net Due */}
              <div style={{ border: '1px solid var(--border)', borderRadius: '8px', padding: '16px', marginBottom: '20px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginBottom: '8px' }}>
                  <span>0% Income Tier:</span>
                  <span style={{ fontWeight: 600 }}>{selectedReturn.taxableIncomeSubjectToZeroPct.toLocaleString()} AED</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginBottom: '8px' }}>
                  <span>9% Income Tier:</span>
                  <span style={{ fontWeight: 600 }}>{selectedReturn.taxableIncomeSubjectToNinePct.toLocaleString()} AED</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginBottom: '8px' }}>
                  <span>Gross Corporate Tax (9%):</span>
                  <span style={{ fontWeight: 600 }}>{selectedReturn.corporateTaxPayableAed.toLocaleString()} AED</span>
                </div>
                <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '13px', marginBottom: '12px' }}>
                  <span>Foreign Tax Credit Relief:</span>
                  <span style={{ fontWeight: 600, color: 'var(--success-text)' }}>- {selectedReturn.foreignTaxCreditReliefAed.toLocaleString()} AED</span>
                </div>
                <div style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  fontSize: '16px',
                  fontWeight: 700,
                  borderTop: '2px solid var(--border)',
                  paddingTop: '12px',
                  color: '#0b2545'
                }}>
                  <span>Net Tax Payable:</span>
                  <span>{selectedReturn.netTaxPayableAed.toLocaleString()} AED (₹ {selectedReturn.netTaxPayableInr.toLocaleString()})</span>
                </div>
              </div>
            </div>
          ) : (
            <div style={{
              backgroundColor: 'var(--card)',
              borderRadius: '8px',
              border: '1px solid var(--border)',
              padding: '48px 24px',
              textAlign: 'center',
              color: 'var(--muted)'
            }}>
              Select a return from the list on the left to view detailed statutory breakdown and sign-off options.
            </div>
          )}
        </div>
      )}

      {/* MODAL 1: REGISTER UAE TAX PROFILE */}
      {showProfileModal && (
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
            width: '600px',
            maxWidth: '95vw',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h2 style={{ margin: 0, fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>
                🇦🇪 Register UAE Corporate Tax Profile
              </h2>
              <button onClick={() => setShowProfileModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            <form onSubmit={handleSaveProfile} style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Client</label>
                <select
                  value={profileForm.clientId}
                  onChange={(e) => setProfileForm({ ...profileForm, clientId: e.target.value })}
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  required
                >
                  <option value="">-- Select Client --</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>
                  FTA Corporate Tax TRN (15 Digits starting with 100)
                </label>
                <div style={{ position: 'relative' }}>
                  <input
                    type="text"
                    maxLength={15}
                    value={profileForm.corporateTaxTrn}
                    onChange={(e) => setProfileForm({ ...profileForm, corporateTaxTrn: e.target.value.replace(/\D/g, '') })}
                    placeholder="100123456789012"
                    style={{
                      width: '100%',
                      padding: '10px',
                      borderRadius: '6px',
                      border: isTrnValid ? '2px solid #00a86b' : '2px solid var(--border)',
                      fontFamily: 'monospace',
                      fontWeight: 600
                    }}
                    required
                  />
                  <span style={{
                    position: 'absolute',
                    right: '12px',
                    top: '12px',
                    fontSize: '12px',
                    fontWeight: 600,
                    color: isTrnValid ? '#00a86b' : 'var(--muted)'
                  }}>
                    {isTrnValid ? '✓ Valid FTA TRN' : `${profileForm.corporateTaxTrn.length}/15 digits`}
                  </span>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Emirate</label>
                  <select
                    value={profileForm.emirate}
                    onChange={(e) => setProfileForm({ ...profileForm, emirate: e.target.value })}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    {EMIRATES.map(em => (
                      <option key={em} value={em}>{em}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Taxpayer Category</label>
                  <select
                    value={profileForm.category}
                    onChange={(e) => setProfileForm({ ...profileForm, category: e.target.value as UaeTaxpayerCategory })}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="ResidentJuridicalPerson">Resident Mainland Juridical Person</option>
                    <option value="QualifyingFreeZonePerson">Qualifying Free Zone Person (QFZP)</option>
                    <option value="NonResidentJuridicalPerson">Non-Resident Juridical Person</option>
                    <option value="ResidentNaturalPerson">Resident Natural Person</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '12px' }}>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Free Zone Jurisdiction</label>
                  <select
                    value={profileForm.freeZoneName}
                    onChange={(e) => setProfileForm({ ...profileForm, freeZoneName: e.target.value })}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    {FREE_ZONES.map(fz => (
                      <option key={fz} value={fz}>{fz}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Accounting Standard</label>
                  <select
                    value={profileForm.accountingStandard}
                    onChange={(e) => setProfileForm({ ...profileForm, accountingStandard: e.target.value })}
                    style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)' }}
                  >
                    <option value="IFRS">Full IFRS</option>
                    <option value="IFRS for SMEs">IFRS for SMEs (Revenue &lt; AED 50M)</option>
                  </select>
                </div>
              </div>

              <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '4px' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-secondary)', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={profileForm.isMneGroupConstituent}
                    onChange={(e) => setProfileForm({ ...profileForm, isMneGroupConstituent: e.target.checked })}
                  />
                  <span>Constituent entity of an MNE Group with consolidated revenue ≥ AED 3.15 Billion</span>
                </label>
                <label style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '13px', color: 'var(--text-secondary)', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={profileForm.auditedFinancialsAvailable}
                    onChange={(e) => setProfileForm({ ...profileForm, auditedFinancialsAvailable: e.target.checked })}
                  />
                  <span>Audited Financial Statements available (Mandatory for QFZP &amp; Turnover &gt; AED 50M)</span>
                </label>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
                <button
                  type="button"
                  onClick={() => setShowProfileModal(false)}
                  style={{ padding: '10px 16px', border: '1px solid var(--border-subtle)', borderRadius: '6px', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={!isTrnValid}
                  style={{
                    padding: '10px 20px',
                    borderRadius: '6px',
                    border: 'none',
                    backgroundColor: isTrnValid ? '#134074' : '#94a3b8',
                    color: '#ffffff',
                    fontWeight: 600,
                    cursor: isTrnValid ? 'pointer' : 'not-allowed'
                  }}
                >
                  Save UAE Profile
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: PARTNER SIGN-OFF */}
      {showApproveModal && (
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
            width: '500px',
            padding: '24px',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <h2 style={{ margin: '0 0 12px 0', fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>
              🏛️ Partner Sign-Off &amp; EmaraTax Attestation
            </h2>
            <p style={{ margin: '0 0 16px 0', fontSize: '13px', color: 'var(--muted)' }}>
              You are certifying this UAE Corporate Tax Return for <strong>{selectedReturn?.clientName}</strong> (Ref: {selectedReturn?.returnReference}).
            </p>

            <form onSubmit={handleApproveReturn}>
              <div style={{ marginBottom: '16px' }}>
                <label style={{ fontSize: '13px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '6px' }}>
                  Partner Review Notes &amp; Statutory Representation
                </label>
                <textarea
                  rows={4}
                  value={partnerNotes}
                  onChange={(e) => setPartnerNotes(e.target.value)}
                  placeholder="Verified audited trial balance, QFZP de minimis calculation, and statutory add-backs under Federal Decree-Law No. 47 of 2022..."
                  style={{ width: '100%', padding: '10px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
                  required
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px' }}>
                <button
                  type="button"
                  onClick={() => setShowApproveModal(false)}
                  style={{ padding: '8px 14px', border: '1px solid var(--border-subtle)', borderRadius: '6px', background: 'var(--card)', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  style={{
                    padding: '8px 18px',
                    backgroundColor: '#1e40af',
                    color: '#ffffff',
                    border: 'none',
                    borderRadius: '6px',
                    fontWeight: 600,
                    cursor: 'pointer'
                  }}
                >
                  Attest &amp; Approve
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: EMARATAX SUBMISSION PACKAGE DRAWER */}
      {showExportModal && exportPackage && (
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
            width: '850px',
            maxWidth: '95vw',
            maxHeight: '90vh',
            display: 'flex',
            flexDirection: 'column',
            overflow: 'hidden',
            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)'
          }}>
            <div style={{ padding: '20px 24px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <h2 style={{ margin: '0 0 4px 0', fontSize: '18px', fontWeight: 700, color: 'var(--text)' }}>
                  📤 FTA EmaraTax Form CT01 Submission Package
                </h2>
                <div style={{ fontSize: '13px', color: 'var(--muted)' }}>
                  Reference: <strong>{exportPackage.returnReference}</strong> | TRN: <strong>{exportPackage.corporateTaxTrn}</strong> | Standard Version: <strong>{exportPackage.formVersion}</strong>
                </div>
              </div>
              <button onClick={() => setShowExportModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: 'var(--muted)' }}>✕</button>
            </div>

            <div style={{ padding: '12px 24px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', backgroundColor: 'var(--card-subtle)' }}>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  onClick={() => setExportFormatTab('xml')}
                  style={{
                    padding: '6px 14px',
                    borderRadius: '4px',
                    border: 'none',
                    fontWeight: 600,
                    fontSize: '13px',
                    backgroundColor: exportFormatTab === 'xml' ? '#134074' : 'var(--bg-subtle)',
                    color: exportFormatTab === 'xml' ? '#ffffff' : 'var(--text-secondary)',
                    cursor: 'pointer'
                  }}
                >
                  FTA Schema XML
                </button>
                <button
                  onClick={() => setExportFormatTab('json')}
                  style={{
                    padding: '6px 14px',
                    borderRadius: '4px',
                    border: 'none',
                    fontWeight: 600,
                    fontSize: '13px',
                    backgroundColor: exportFormatTab === 'json' ? '#134074' : 'var(--bg-subtle)',
                    color: exportFormatTab === 'json' ? '#ffffff' : 'var(--text-secondary)',
                    cursor: 'pointer'
                  }}
                >
                  EmaraTax REST JSON
                </button>
              </div>

              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  onClick={() => handleDownloadFile(
                    exportFormatTab === 'xml' ? exportPackage.submissionXml : exportPackage.submissionJson,
                    `${exportPackage.returnReference}_${exportPackage.corporateTaxTrn}.${exportFormatTab}`,
                    exportFormatTab === 'xml' ? 'application/xml' : 'application/json'
                  )}
                  style={{
                    padding: '6px 14px',
                    borderRadius: '4px',
                    backgroundColor: '#00a86b',
                    color: '#ffffff',
                    border: 'none',
                    fontWeight: 600,
                    fontSize: '13px',
                    cursor: 'pointer'
                  }}
                >
                  Download {exportFormatTab.toUpperCase()} File
                </button>
              </div>
            </div>

            <div style={{ padding: '16px 24px', flex: 1, overflowY: 'auto' }}>
              <pre style={{
                backgroundColor: '#0f172a',
                color: '#f8fafc',
                padding: '16px',
                borderRadius: '8px',
                fontSize: '12px',
                fontFamily: 'monospace',
                overflowX: 'auto',
                margin: 0
              }}>
                {exportFormatTab === 'xml' ? exportPackage.submissionXml : exportPackage.submissionJson}
              </pre>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
export default UaeCorporateTaxDesk
