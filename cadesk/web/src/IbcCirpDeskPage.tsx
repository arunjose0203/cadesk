import { useState, useEffect, useMemo, useCallback, type FormEvent } from 'react'
import {
  ibcCirpApi,
  api,
  type CirpCaseDto,
  type CirpCaseDetailDto,
  type SaveCirpCaseRequest,
  type SaveCocMemberRequest,
  type SaveCocResolutionRequest,
  type SavePufeTransactionRequest,
  type SaveAssetValuationRequest,
  type CirpTimelineCalculationDto,
  type CirpStage,
  type CreditorCategory,
  type PufeType,
  type AssetValuationClass,
  type Client
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

function formatINR(val?: number): string {
  if (val == null || isNaN(val)) return '₹0'
  return '₹' + Number(val).toLocaleString('en-IN', { maximumFractionDigits: 2 })
}

const CIRP_STAGES: { key: CirpStage; label: string; badge: string }[] = [
  { key: 'AdmissionAndMoratorium', label: 'Admission & Sec 14 Moratorium', badge: 'bg-blue-100 text-blue-800 border-blue-200' },
  { key: 'PublicAnnouncementAndClaims', label: 'Public Announcement & Claims Verification', badge: 'bg-indigo-100 text-indigo-800 border-indigo-200' },
  { key: 'CocConstituted', label: 'CoC Constituted', badge: 'bg-purple-100 text-purple-800 border-purple-200' },
  { key: 'InformationMemorandumIssued', label: 'Information Memorandum Issued (Reg 36)', badge: 'bg-teal-100 text-teal-800 border-teal-200' },
  { key: 'FormGInvited', label: 'Form G EOI Published', badge: 'bg-amber-100 text-amber-800 border-amber-200' },
  { key: 'ResolutionPlanReview', label: 'CoC Resolution Plan Review & Voting', badge: 'bg-orange-100 text-orange-800 border-orange-200' },
  { key: 'ResolutionPlanApproved', label: 'Resolution Plan Approved & Closed', badge: 'bg-emerald-100 text-emerald-800 border-emerald-200' },
  { key: 'LiquidationInitiated', label: 'Liquidation Initiated u/s 33', badge: 'bg-rose-100 text-rose-800 border-rose-200' },
  { key: 'Section12AWithdrawn', label: 'Withdrawn u/s 12A (90% CoC Vote)', badge: 'bg-slate-100 text-slate-800 border-slate-200' }
]

const CREDITOR_CATEGORIES: { key: CreditorCategory; label: string; isVoting: boolean }[] = [
  { key: 'SecuredFinancialCreditor', label: 'Financial Creditor (Secured)', isVoting: true },
  { key: 'UnsecuredFinancialCreditor', label: 'Financial Creditor (Unsecured)', isVoting: true },
  { key: 'OperationalCreditorGovernment', label: 'Operational Creditor (Govt / Statutory Dues)', isVoting: false },
  { key: 'OperationalCreditorEmployees', label: 'Operational Creditor (Workmen & Employees)', isVoting: false },
  { key: 'OperationalCreditorVendors', label: 'Operational Creditor (Trade Vendors & Suppliers)', isVoting: false }
]

const PUFE_TYPES: { key: PufeType; label: string; section: string; lookback: string; desc: string }[] = [
  {
    key: 'PreferentialSec43',
    label: 'Preferential Transaction',
    section: 'Section 43',
    lookback: '1 yr (unrelated) / 2 yrs (related)',
    desc: 'Transfer of property of corporate debtor putting creditor in a beneficial position.'
  },
  {
    key: 'UndervaluedSec45',
    label: 'Undervalued Transaction',
    section: 'Section 45',
    lookback: '1 yr (unrelated) / 2 yrs (related)',
    desc: 'Transaction for consideration significantly less than market value.'
  },
  {
    key: 'ExtortionateCreditSec50',
    label: 'Extortionate Credit',
    section: 'Section 50',
    lookback: '2 years preceding ICD',
    desc: 'Credit entered into with unconscionable interest terms.'
  },
  {
    key: 'FraudulentTradingSec66',
    label: 'Fraudulent / Wrongful Trading',
    section: 'Section 66',
    lookback: 'No limitation period',
    desc: 'Business carried on to defraud creditors; directors personally liable.'
  }
]

const ASSET_CLASSES: { key: AssetValuationClass; label: string }[] = [
  { key: 'LandAndBuilding', label: 'Class 1: Land & Building' },
  { key: 'PlantAndMachinery', label: 'Class 2: Plant & Machinery' },
  { key: 'SecuritiesAndFinancialAssets', label: 'Class 3: Securities & Financial Assets' }
]

export function IbcCirpDeskPage({ can }: { can?: (p: string) => boolean }) {
  const [activeTab, setActiveTab] = useState<'cirp-cases' | 'coc-resolutions' | 'pufe-forensics' | 'asset-valuations' | 'im-dossier'>('cirp-cases')
  const [loading, setLoading] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  // Context Data
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [cases, setCases] = useState<CirpCaseDto[]>([])
  const [selectedCaseId, setSelectedCaseId] = useState<string>('')
  const [currentCase, setCurrentCase] = useState<CirpCaseDetailDto | null>(null)

  // Timeline telemetry state
  const [timelineCalc, setTimelineCalc] = useState<CirpTimelineCalculationDto | null>(null)

  // Modals & Panels
  const [showCaseModal, setShowCaseModal] = useState(false)
  const [showMemberModal, setShowMemberModal] = useState(false)
  const [showResolutionModal, setShowResolutionModal] = useState(false)
  const [showPufeModal, setShowPufeModal] = useState(false)
  const [showValuationModal, setShowValuationModal] = useState(false)
  const [showDossierModal, setShowDossierModal] = useState(false)
  const [dossierHtml, setDossierHtml] = useState<string>('')

  // Case Form State
  const [formClientId, setFormClientId] = useState('')
  const [formDebtorName, setFormDebtorName] = useState('')
  const [formCin, setFormCin] = useState('')
  const [formBench, setFormBench] = useState('NCLT Mumbai Bench (Court-I)')
  const [formOrderNo, setFormOrderNo] = useState('CP (IB) No. 1042/MB/2024')
  const [formAdmissionDate, setFormAdmissionDate] = useState(new Date().toISOString().split('T')[0])
  const [formIrpName, setFormIrpName] = useState('CA Rajesh Sharma')
  const [formIrpRegNo, setFormIrpRegNo] = useState('IBBI/IPA-001/IP-P01234/2020-2021/11987')
  const [formRpName, setFormRpName] = useState('CA Rajesh Sharma')
  const [formRpRegNo, setFormRpRegNo] = useState('IBBI/IPA-001/IP-P01234/2020-2021/11987')
  const [formMoratorium, setFormMoratorium] = useState(true)
  const [formStage, setFormStage] = useState<CirpStage>('CocConstituted')
  const [formStatusNote, setFormStatusNote] = useState('Public announcement published in Financial Express & Navshakti.')

  // CoC Member Form State
  const [memberCreditorName, setMemberCreditorName] = useState('')
  const [memberCategory, setMemberCategory] = useState<CreditorCategory>('SecuredFinancialCreditor')
  const [memberClaimSubmitted, setMemberClaimSubmitted] = useState<number>(100000000)
  const [memberAdmittedClaim, setMemberAdmittedClaim] = useState<number>(95000000)
  const [memberSecurityDesc, setMemberSecurityDesc] = useState('First pari passu charge on immovable factory land & building.')
  const [memberIsRelatedParty, setMemberIsRelatedParty] = useState(false)

  // Resolution Form State
  const [resMeetingNo, setResMeetingNo] = useState<number>(3)
  const [resNumber, setResNumber] = useState('RES/CoC-03/01')
  const [resTitle, setResTitle] = useState('Approval of Resolution Plan submitted by Apex Consortium')
  const [resStatutorySection, setResStatutorySection] = useState('Section 30(4)')
  const [resRequiredPct, setResRequiredPct] = useState<number>(66)
  const [resInFavourPct, setResInFavourPct] = useState<number>(72.5)
  const [resAgainstPct, setResAgainstPct] = useState<number>(15.0)
  const [resAbstainedPct, setResAbstainedPct] = useState<number>(12.5)

  // PUFE Form State
  const [pufeType, setPufeType] = useState<PufeType>('PreferentialSec43')
  const [pufeDate, setPufeDate] = useState(new Date(Date.now() - 180 * 86400000).toISOString().split('T')[0])
  const [pufeCounterparty, setPufeCounterparty] = useState('Zenith Suppliers Pvt Ltd')
  const [pufeIsRelatedParty, setPufeIsRelatedParty] = useState(false)
  const [pufeAmount, setPufeAmount] = useState<number>(45000000)
  const [pufeRecoverable, setPufeRecoverable] = useState<number>(45000000)
  const [pufeObservations, setPufeObservations] = useState('Security interest created in favour of unsecured creditor 4 months prior to ICD outside ordinary course of business.')
  const [pufeNcltFiled, setPufeNcltFiled] = useState(true)

  // Valuation Form State
  const [valClass, setValClass] = useState<AssetValuationClass>('LandAndBuilding')
  const [valDesc, setValDesc] = useState('Manufacturing unit at Plot No. 44, MIDC Industrial Area, Pune')
  const [val1Name, setVal1Name] = useState('M/s Adarsh Valuers & Co.')
  const [val1Fair, setVal1Fair] = useState<number>(125000000)
  const [val1Liq, setVal1Liq] = useState<number>(85000000)
  const [val2Name, setVal2Name] = useState('M/s Apex Registered Valuers LLP')
  const [val2Fair, setVal2Fair] = useState<number>(120000000)
  const [val2Liq, setVal2Liq] = useState<number>(82000000)

  const loadClients = useCallback(async () => {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      setClients(res.items || [])
      if (res.items && res.items.length > 0) {
        setFormClientId(prev => prev || res.items[0].id)
      }
    } catch {
      // ignore
    }
  }, [])

  const loadCases = useCallback(async (clientId?: string) => {
    setLoading(true)
    try {
      const targetClientId = clientId !== undefined ? clientId : selectedClientId
      const data = await ibcCirpApi.getCases(targetClientId ? targetClientId : undefined)
      setCases(data)
      if (data.length > 0) {
        setSelectedCaseId(prev => prev || data[0].id)
      } else {
        setSelectedCaseId('')
        setCurrentCase(null)
      }
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to load CIRP cases')
    } finally {
      setLoading(false)
    }
  }, [selectedClientId])

  // Load clients & cases
  useEffect(() => {
    loadClients()
    loadCases()
  }, [loadClients, loadCases])

  // When selected case changes, fetch full details & compute timeline
  useEffect(() => {
    if (selectedCaseId) {
      loadCaseDetail(selectedCaseId)
    } else {
      setCurrentCase(null)
      setTimelineCalc(null)
    }
  }, [selectedCaseId])

  async function loadCaseDetail(id: string) {
    setLoading(true)
    try {
      const detail = await ibcCirpApi.getCase(id)
      setCurrentCase(detail)
      // compute timeline
      const t = await ibcCirpApi.calculateTimeline({
        admissionDate: detail.admissionDate.split('T')[0],
        asOfDate: new Date().toISOString().split('T')[0]
      })
      setTimelineCalc(t)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to load case detail')
    } finally {
      setLoading(false)
    }
  }

  // Handle Create/Edit Case
  async function handleSaveCase(e: FormEvent) {
    e.preventDefault()
    if (!formClientId || !formDebtorName || !formCin || !formAdmissionDate) {
      setErrorMsg('Please enter mandatory Corporate Debtor details.')
      return
    }

    setLoading(true)
    setErrorMsg(null)
    try {
      const req: SaveCirpCaseRequest = {
        id: currentCase && showCaseModal && currentCase.corporateDebtorName === formDebtorName ? currentCase.id : undefined,
        clientId: formClientId,
        corporateDebtorName: formDebtorName,
        cin: formCin,
        ncltBench: formBench,
        courtOrderNumber: formOrderNo,
        admissionDate: formAdmissionDate,
        irpName: formIrpName,
        irpIpRegNumber: formIrpRegNo,
        rpName: formRpName,
        rpIpRegNumber: formRpRegNo,
        moratoriumSection14Declared: formMoratorium,
        stage: formStage,
        statusNote: formStatusNote
      }
      const saved = await ibcCirpApi.saveCase(req)
      setNotice(`CIRP Case for "${saved.corporateDebtorName}" saved successfully.`)
      setShowCaseModal(false)
      await loadCases()
      setSelectedCaseId(saved.id)
      await loadCaseDetail(saved.id)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to save CIRP case')
    } finally {
      setLoading(false)
    }
  }

  // Handle Delete Case
  async function handleDeleteCase(id: string) {
    if (!confirm('Are you sure you want to permanently delete this CIRP case and all related records?')) return
    setLoading(true)
    try {
      await ibcCirpApi.deleteCase(id)
      setNotice('CIRP case deleted.')
      setSelectedCaseId('')
      setCurrentCase(null)
      await loadCases()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete case')
    } finally {
      setLoading(false)
    }
  }

  // Handle Save CoC Member
  async function handleSaveMember(e: FormEvent) {
    e.preventDefault()
    if (!selectedCaseId || !memberCreditorName) {
      setErrorMsg('Creditor Name is required.')
      return
    }

    setLoading(true)
    setErrorMsg(null)
    try {
      const req: SaveCocMemberRequest = {
        cirpCaseId: selectedCaseId,
        creditorName: memberCreditorName,
        category: memberCategory,
        claimSubmittedAmount: Number(memberClaimSubmitted),
        admittedClaimAmount: Number(memberAdmittedClaim),
        securityInterestDescription: memberSecurityDesc,
        isRelatedParty: memberIsRelatedParty
      }
      await ibcCirpApi.saveCocMember(req)
      setNotice(`Creditor claim for "${memberCreditorName}" recorded with automatic CoC voting share calculation.`)
      setShowMemberModal(false)
      setMemberCreditorName('')
      await loadCaseDetail(selectedCaseId)
      await loadCases()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to save CoC member')
    } finally {
      setLoading(false)
    }
  }

  async function handleDeleteMember(id: string) {
    if (!confirm('Remove this creditor from the claims & CoC ledger?')) return
    setLoading(true)
    try {
      await ibcCirpApi.deleteCocMember(id)
      setNotice('Creditor removed.')
      if (selectedCaseId) await loadCaseDetail(selectedCaseId)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to remove creditor')
    } finally {
      setLoading(false)
    }
  }

  // Handle Save Resolution
  async function handleSaveResolution(e: FormEvent) {
    e.preventDefault()
    if (!selectedCaseId || !resNumber || !resTitle) {
      setErrorMsg('Resolution number and title are required.')
      return
    }

    setLoading(true)
    setErrorMsg(null)
    try {
      const req: SaveCocResolutionRequest = {
        cirpCaseId: selectedCaseId,
        meetingNumber: Number(resMeetingNo),
        resolutionNumber: resNumber,
        resolutionTitle: resTitle,
        statutorySection: resStatutorySection,
        requiredThresholdPct: Number(resRequiredPct),
        votingShareInFavourPct: Number(resInFavourPct),
        votingShareAgainstPct: Number(resAgainstPct),
        votingShareAbstainedPct: Number(resAbstainedPct)
      }
      await ibcCirpApi.saveResolution(req)
      setNotice(`Resolution "${resNumber}" evaluated against IBC statutory thresholds.`)
      setShowResolutionModal(false)
      if (selectedCaseId) await loadCaseDetail(selectedCaseId)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to save resolution')
    } finally {
      setLoading(false)
    }
  }

  async function handleDeleteResolution(id: string) {
    if (!confirm('Delete this voting resolution record?')) return
    setLoading(true)
    try {
      await ibcCirpApi.deleteResolution(id)
      setNotice('Resolution removed.')
      if (selectedCaseId) await loadCaseDetail(selectedCaseId)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to remove resolution')
    } finally {
      setLoading(false)
    }
  }

  // Handle Save PUFE
  async function handleSavePufe(e: FormEvent) {
    e.preventDefault()
    if (!selectedCaseId || !pufeCounterparty || !pufeAmount) {
      setErrorMsg('Counterparty and transaction amount are required.')
      return
    }

    setLoading(true)
    setErrorMsg(null)
    try {
      const req: SavePufeTransactionRequest = {
        cirpCaseId: selectedCaseId,
        pufeType: pufeType,
        transactionDate: pufeDate,
        counterpartyName: pufeCounterparty,
        isRelatedParty: pufeIsRelatedParty,
        transactionAmount: Number(pufeAmount),
        estimatedAvoidanceRecoverableAmount: Number(pufeRecoverable),
        forensicAuditorObservations: pufeObservations,
        ncltApplicationFiled: pufeNcltFiled
      }
      await ibcCirpApi.savePufe(req)
      setNotice(`PUFE transaction recorded with statutory look-back evaluation.`)
      setShowPufeModal(false)
      if (selectedCaseId) await loadCaseDetail(selectedCaseId)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to record PUFE transaction')
    } finally {
      setLoading(false)
    }
  }

  async function handleDeletePufe(id: string) {
    if (!confirm('Delete this PUFE forensic audit finding?')) return
    setLoading(true)
    try {
      await ibcCirpApi.deletePufe(id)
      setNotice('PUFE record removed.')
      if (selectedCaseId) await loadCaseDetail(selectedCaseId)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete PUFE record')
    } finally {
      setLoading(false)
    }
  }

  // Handle Save Valuation
  async function handleSaveValuation(e: FormEvent) {
    e.preventDefault()
    if (!selectedCaseId || !valDesc || !val1Fair || !val2Fair) {
      setErrorMsg('Asset description and both valuer estimates are required.')
      return
    }

    setLoading(true)
    setErrorMsg(null)
    try {
      const req: SaveAssetValuationRequest = {
        cirpCaseId: selectedCaseId,
        assetClass: valClass,
        assetDescription: valDesc,
        valuer1Name: val1Name,
        valuer1FairValue: Number(val1Fair),
        valuer1LiquidationValue: Number(val1Liq),
        valuer2Name: val2Name,
        valuer2FairValue: Number(val2Fair),
        valuer2LiquidationValue: Number(val2Liq)
      }
      await ibcCirpApi.saveValuation(req)
      setNotice(`Regulation 35 valuation saved with average fair and liquidation values.`)
      setShowValuationModal(false)
      if (selectedCaseId) await loadCaseDetail(selectedCaseId)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to save valuation')
    } finally {
      setLoading(false)
    }
  }

  async function handleDeleteValuation(id: string) {
    if (!confirm('Delete this registered asset valuation?')) return
    setLoading(true)
    try {
      await ibcCirpApi.deleteValuation(id)
      setNotice('Asset valuation removed.')
      if (selectedCaseId) await loadCaseDetail(selectedCaseId)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete valuation')
    } finally {
      setLoading(false)
    }
  }

  // Load IM HTML Dossier
  async function handleOpenDossier() {
    if (!selectedCaseId) return
    setLoading(true)
    try {
      const url = ibcCirpApi.getImDossierHtmlUrl(selectedCaseId)
      const res = await fetch(url, { headers: { 'Authorization': `Bearer ${localStorage.getItem('token') || ''}` } })
      const html = await res.text()
      setDossierHtml(html)
      setShowDossierModal(true)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to load IM dossier')
    } finally {
      setLoading(false)
    }
  }

  // Calculated CoC Aggregates
  const cocSummary = useMemo(() => {
    if (!currentCase) return { totalAdmitted: 0, totalFinancialAdmitted: 0, totalVotingEligible: 0, relatedPartyDebt: 0 }
    const members = currentCase.cocMembers || []
    const totalAdmitted = members.reduce((sum, m) => sum + m.admittedClaimAmount, 0)
    const financialMembers = members.filter(m => m.category === 'SecuredFinancialCreditor' || m.category === 'UnsecuredFinancialCreditor')
    const totalFinancialAdmitted = financialMembers.reduce((sum, m) => sum + m.admittedClaimAmount, 0)
    const relatedPartyDebt = financialMembers.filter(m => m.isRelatedParty).reduce((sum, m) => sum + m.admittedClaimAmount, 0)
    const totalVotingEligible = totalFinancialAdmitted - relatedPartyDebt
    return { totalAdmitted, totalFinancialAdmitted, totalVotingEligible, relatedPartyDebt }
  }, [currentCase])

  // Calculated PUFE Aggregates
  const pufeSummary = useMemo(() => {
    if (!currentCase) return { totalAmount: 0, totalRecoverable: 0, countWithinLookback: 0, countApplications: 0 }
    const items = currentCase.pufeTransactions || []
    const totalAmount = items.reduce((sum, p) => sum + p.transactionAmount, 0)
    const totalRecoverable = items.reduce((sum, p) => sum + p.estimatedAvoidanceRecoverableAmount, 0)
    const countWithinLookback = items.filter(p => p.isWithinLookbackPeriod).length
    const countApplications = items.filter(p => p.ncltApplicationFiled).length
    return { totalAmount, totalRecoverable, countWithinLookback, countApplications }
  }, [currentCase])

  // Calculated Valuation Aggregates
  const valSummary = useMemo(() => {
    if (!currentCase) return { totalFairValue: 0, totalLiquidationValue: 0, hasThirdValuerRequired: false }
    const items = currentCase.valuations || []
    const totalFairValue = items.reduce((sum, v) => sum + v.averageFairValue, 0)
    const totalLiquidationValue = items.reduce((sum, v) => sum + v.averageLiquidationValue, 0)
    const hasThirdValuerRequired = items.some(v => v.thirdValuerRequired)
    return { totalFairValue, totalLiquidationValue, hasThirdValuerRequired }
  }, [currentCase])

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header Banner */}
      <div className="bg-gradient-to-r from-slate-900 via-indigo-950 to-blue-950 text-white rounded-2xl p-6 shadow-xl relative overflow-hidden border border-indigo-900/40">
        <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-3">
              <span className="text-3xl">⚖️</span>
              <h1 className="text-2xl font-black tracking-tight text-white flex items-center gap-2">
                IBC 2016 Insolvency &amp; CIRP Statutory Desk
                <span className="text-xs bg-indigo-500/30 text-indigo-200 border border-indigo-400/30 px-2 py-0.5 rounded-full font-semibold">
                  IBBI (CIRP) Reg. 2016
                </span>
              </h1>
            </div>
            <p className="text-sm text-indigo-200/80 mt-1 max-w-3xl">
              End-to-end statutory workspace for Resolution Professionals (RP) &amp; CA insolvency practices:
              330-day CIRP Section 12 timeline engine, CoC voting threshold validator (51% / 66% / 90% Section 12A),
              forensic PUFE avoidance tracking (Sec 43, 45, 50, 66), Regulation 35 Fair vs. Liquidation valuations,
              and 1-click Regulation 36 Information Memorandum (IM) dossier compilation.
            </p>
          </div>
          <div className="flex items-center gap-2 self-start md:self-auto">
            <button
              onClick={() => {
                setFormDebtorName('')
                setFormCin('')
                setShowCaseModal(true)
              }}
              className="bg-indigo-600 hover:bg-indigo-500 text-white font-medium text-xs px-3.5 py-2.5 rounded-lg transition-all shadow-md flex items-center gap-1.5"
            >
              <span>+</span> New CIRP Case
            </button>
            {currentCase && (
              <button
                onClick={handleOpenDossier}
                className="bg-emerald-600 hover:bg-emerald-500 text-white font-medium text-xs px-3.5 py-2.5 rounded-lg transition-all shadow-md flex items-center gap-1.5"
              >
                <span>📄</span> Reg. 36 IM Dossier
              </button>
            )}
          </div>
        </div>

        {/* Quick Statutory Reference Strip */}
        <div className="relative z-10 mt-5 pt-4 border-t border-indigo-800/50 grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs">
          <div className="bg-black/20 backdrop-blur-sm rounded-lg p-2.5 border border-white/5">
            <div className="text-indigo-300 font-semibold">Section 12 Timelines</div>
            <div className="text-slate-100 font-mono text-[11px] mt-0.5">180d initial • 270d ext. • 330d max cap</div>
          </div>
          <div className="bg-black/20 backdrop-blur-sm rounded-lg p-2.5 border border-white/5">
            <div className="text-indigo-300 font-semibold">CoC Voting Thresholds</div>
            <div className="text-slate-100 font-mono text-[11px] mt-0.5">51% Admin • 66% Plan/Ext. • 90% Sec 12A</div>
          </div>
          <div className="bg-black/20 backdrop-blur-sm rounded-lg p-2.5 border border-white/5">
            <div className="text-indigo-300 font-semibold">PUFE Forensic Scope</div>
            <div className="text-slate-100 font-mono text-[11px] mt-0.5">Sec 43 (Pref) • 45 (UV) • 50 (Ext) • 66 (Fraud)</div>
          </div>
          <div className="bg-black/20 backdrop-blur-sm rounded-lg p-2.5 border border-white/5">
            <div className="text-indigo-300 font-semibold">Reg. 35 Valuation</div>
            <div className="text-slate-100 font-mono text-[11px] mt-0.5">&gt;25% divergence requires 3rd Valuer</div>
          </div>
        </div>
      </div>

      {/* Notice / Alert banners */}
      {notice && (
        <div className="bg-emerald-50 border border-emerald-200 text-emerald-800 px-4 py-3 rounded-xl text-sm flex items-center justify-between shadow-sm">
          <div className="flex items-center gap-2">
            <span>✅</span>
            <span>{notice}</span>
          </div>
          <button onClick={() => setNotice(null)} className="text-emerald-600 hover:text-emerald-900 font-bold">×</button>
        </div>
      )}
      {errorMsg && (
        <div className="bg-rose-50 border border-rose-200 text-rose-800 px-4 py-3 rounded-xl text-sm flex items-center justify-between shadow-sm">
          <div className="flex items-center gap-2">
            <span>⚠️</span>
            <span>{errorMsg}</span>
          </div>
          <button onClick={() => setErrorMsg(null)} className="text-rose-600 hover:text-rose-900 font-bold">×</button>
        </div>
      )}

      {/* Case & Client Selector Ribbon */}
      <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200 flex flex-wrap items-center justify-between gap-4">
        <div className="flex items-center gap-3 flex-1 min-w-[320px]">
          {/* Client Filter */}
          <div className="w-48">
            <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-0.5">Filter by Client:</label>
            <select
              value={selectedClientId}
              onChange={e => {
                setSelectedClientId(e.target.value)
                loadCases(e.target.value)
              }}
              className="w-full bg-slate-50 border border-slate-300 text-slate-800 text-xs rounded-lg p-1.5 font-medium focus:ring-2 focus:ring-indigo-500 focus:outline-none"
            >
              <option value="">All Corporate Entities</option>
              {clients.map(c => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
          </div>

          {/* Corporate Debtor Selector */}
          <div className="flex-1">
            <label className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block mb-0.5">Corporate Debtor CIRP Case:</label>
            <select
              value={selectedCaseId}
              onChange={e => setSelectedCaseId(e.target.value)}
              className="w-full bg-slate-50 border border-slate-300 text-slate-800 text-xs rounded-lg p-1.5 font-medium focus:ring-2 focus:ring-indigo-500 focus:outline-none"
            >
              {cases.length === 0 && <option value="">No CIRP cases registered</option>}
              {cases.map(c => (
                <option key={c.id} value={c.id}>
                  {c.corporateDebtorName} ({c.cin}) — {c.ncltBench}
                </option>
              ))}
            </select>
          </div>
        </div>

        {currentCase && (
          <div className="flex items-center gap-2 text-xs flex-wrap">
            <span className={`px-2.5 py-1 rounded-full font-bold border ${CIRP_STAGES.find(s => s.key === currentCase.stage)?.badge || 'bg-slate-100 text-slate-800 border-slate-200'}`}>
              Stage: {CIRP_STAGES.find(s => s.key === currentCase.stage)?.label || currentCase.stage}
            </span>
            {currentCase.moratoriumSection14Declared && (
              <span className="bg-rose-100 text-rose-800 border border-rose-200 px-2 py-0.5 rounded-md font-semibold flex items-center gap-1">
                <span>🛡️</span> Section 14 Moratorium Active
              </span>
            )}
            <span className="text-slate-500">ICD: <strong className="text-slate-700">{new Date(currentCase.admissionDate).toLocaleDateString('en-IN')}</strong></span>
            {can && can('ibccirp.manage') && (
              <button
                onClick={() => handleDeleteCase(currentCase.id)}
                className="text-rose-600 hover:text-rose-800 font-medium ml-2 underline text-xs"
              >
                Delete Case
              </button>
            )}
          </div>
        )}
      </div>

      {/* Section 12 Statutory Timeline Monitor (Always visible when case selected) */}
      {currentCase && timelineCalc && (
        <div className={`rounded-xl p-4 border text-xs shadow-sm transition-all ${timelineCalc.is330DaysCapBreached ? 'bg-rose-50 border-rose-300 text-rose-950' : timelineCalc.is180DaysExpired ? 'bg-amber-50 border-amber-300 text-amber-950' : 'bg-slate-50 border-slate-200 text-slate-800'}`}>
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 pb-3 border-b border-black/10">
            <div className="flex items-center gap-2">
              <span className="text-lg">⏱️</span>
              <div>
                <span className="font-bold text-sm">CIRP Section 12 Statutory Timelines &amp; 330-Day Outer Cap</span>
                <span className="ml-2 font-mono text-[11px] font-semibold text-indigo-700">
                  {timelineCalc.timelineStatusAlert}
                </span>
              </div>
            </div>
            <div className="flex items-center gap-4 text-xs font-mono">
              <div>Days Elapsed: <strong className="text-indigo-900">{timelineCalc.daysElapsed} days</strong></div>
              <div>Remaining to 180d: <strong className={timelineCalc.daysRemainingTo180 < 0 ? 'text-rose-600' : 'text-emerald-700'}>{timelineCalc.daysRemainingTo180} days</strong></div>
              <div>Remaining to 330d Cap: <strong className={timelineCalc.daysRemainingTo330 < 30 ? 'text-rose-600' : 'text-slate-800'}>{timelineCalc.daysRemainingTo330} days</strong></div>
            </div>
          </div>

          {/* Progress bar representing 330 days */}
          <div className="mt-3">
            <div className="w-full bg-slate-200 h-2.5 rounded-full overflow-hidden flex">
              <div
                className={`h-full transition-all ${timelineCalc.is330DaysCapBreached ? 'bg-rose-600' : timelineCalc.is180DaysExpired ? 'bg-amber-500' : 'bg-indigo-600'}`}
                style={{ width: `${Math.min(100, Math.round((timelineCalc.daysElapsed / 330) * 100))}%` }}
              />
            </div>
            <div className="flex justify-between items-center text-[11px] text-slate-500 mt-1 font-mono">
              <span>ICD: {new Date(timelineCalc.admissionDate).toLocaleDateString('en-IN')}</span>
              <span>Claims Due (14d): {new Date(timelineCalc.claimsLastDate).toLocaleDateString('en-IN')}</span>
              <span>180 Days Limit: {new Date(timelineCalc.initial180Days).toLocaleDateString('en-IN')}</span>
              <span>270 Days Ext.: {new Date(timelineCalc.extended270Days).toLocaleDateString('en-IN')}</span>
              <span className="font-bold text-rose-700">330 Days Hard Stop: {new Date(timelineCalc.final330Days).toLocaleDateString('en-IN')}</span>
            </div>
          </div>
        </div>
      )}

      {/* Navigation Tabs */}
      <div className="flex border-b border-slate-200 gap-1 overflow-x-auto text-sm font-semibold">
        <button
          onClick={() => setActiveTab('cirp-cases')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${activeTab === 'cirp-cases' ? 'border-indigo-600 text-indigo-700 bg-indigo-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>🏢</span> Corporate Debtor Master
        </button>
        <button
          onClick={() => setActiveTab('coc-resolutions')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${activeTab === 'coc-resolutions' ? 'border-indigo-600 text-indigo-700 bg-indigo-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>🗳️</span> Committee of Creditors (CoC) &amp; Voting
          {currentCase && currentCase.cocMembers && (
            <span className="bg-indigo-100 text-indigo-800 text-[10px] px-1.5 py-0.5 rounded-full font-bold">
              {currentCase.cocMembers.length}
            </span>
          )}
        </button>
        <button
          onClick={() => setActiveTab('pufe-forensics')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${activeTab === 'pufe-forensics' ? 'border-indigo-600 text-indigo-700 bg-indigo-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>⚖️</span> PUFE Forensic Avoidance (Sec 43, 45, 50, 66)
          {currentCase && currentCase.pufeTransactions && currentCase.pufeTransactions.length > 0 && (
            <span className="bg-rose-100 text-rose-800 text-[10px] px-1.5 py-0.5 rounded-full font-bold">
              {currentCase.pufeTransactions.length}
            </span>
          )}
        </button>
        <button
          onClick={() => setActiveTab('asset-valuations')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${activeTab === 'asset-valuations' ? 'border-indigo-600 text-indigo-700 bg-indigo-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>💎</span> Regulation 35 Registered Valuations
          {currentCase && currentCase.valuations && (
            <span className="bg-purple-100 text-purple-800 text-[10px] px-1.5 py-0.5 rounded-full font-bold">
              {currentCase.valuations.length}
            </span>
          )}
        </button>
        <button
          onClick={() => setActiveTab('im-dossier')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${activeTab === 'im-dossier' ? 'border-indigo-600 text-indigo-700 bg-indigo-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>📄</span> Reg. 36 Information Memorandum
        </button>
      </div>

      {/* TAB 1: CORPORATE DEBTOR MASTER */}
      {activeTab === 'cirp-cases' && (
        <div className="space-y-6">
          {currentCase ? (
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
              {/* Primary Case Profile */}
              <div className="md:col-span-2 bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
                <div className="flex justify-between items-start border-b pb-3">
                  <div>
                    <h2 className="text-lg font-bold text-slate-900">{currentCase.corporateDebtorName}</h2>
                    <p className="text-xs text-slate-500 font-mono">CIN: {currentCase.cin} • Client: {currentCase.clientName || 'N/A'}</p>
                  </div>
                  <button
                    onClick={() => {
                      setFormDebtorName(currentCase.corporateDebtorName)
                      setFormCin(currentCase.cin)
                      setFormBench(currentCase.ncltBench)
                      setFormOrderNo(currentCase.courtOrderNumber)
                      setFormAdmissionDate(currentCase.admissionDate.split('T')[0])
                      setFormIrpName(currentCase.irpName)
                      setFormIrpRegNo(currentCase.irpIpRegNumber)
                      setFormRpName(currentCase.rpName)
                      setFormRpRegNo(currentCase.rpIpRegNumber)
                      setFormMoratorium(currentCase.moratoriumSection14Declared)
                      setFormStage(currentCase.stage)
                      setFormStatusNote(currentCase.statusNote)
                      setShowCaseModal(true)
                    }}
                    className="text-indigo-600 hover:text-indigo-800 text-xs font-semibold px-2 py-1 bg-indigo-50 rounded"
                  >
                    ✏️ Edit Case Profile
                  </button>
                </div>

                <div className="grid grid-cols-2 gap-4 text-xs">
                  <div className="bg-slate-50 p-3 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block font-medium">NCLT Adjudicating Authority:</span>
                    <strong className="text-slate-800 text-sm mt-0.5 block">{currentCase.ncltBench}</strong>
                    <span className="text-slate-500 font-mono mt-1 block">Order: {currentCase.courtOrderNumber}</span>
                  </div>
                  <div className="bg-slate-50 p-3 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block font-medium">Insolvency Commencement Date (ICD):</span>
                    <strong className="text-slate-800 text-sm mt-0.5 block">{new Date(currentCase.admissionDate).toLocaleDateString('en-IN', { dateStyle: 'long' })}</strong>
                    <span className="text-slate-500 font-mono mt-1 block">Created: {new Date(currentCase.createdAt).toLocaleDateString('en-IN')}</span>
                  </div>
                  <div className="bg-slate-50 p-3 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block font-medium">Interim Resolution Professional (IRP):</span>
                    <strong className="text-slate-800 text-sm mt-0.5 block">{currentCase.irpName}</strong>
                    <span className="text-slate-500 font-mono mt-1 block">Reg: {currentCase.irpIpRegNumber}</span>
                  </div>
                  <div className="bg-slate-50 p-3 rounded-lg border border-slate-100">
                    <span className="text-slate-400 block font-medium">Resolution Professional (RP):</span>
                    <strong className="text-slate-800 text-sm mt-0.5 block">{currentCase.rpName}</strong>
                    <span className="text-slate-500 font-mono mt-1 block">Reg: {currentCase.rpIpRegNumber}</span>
                  </div>
                </div>

                <div className="bg-slate-50 p-3 rounded-lg border border-slate-100 text-xs">
                  <span className="text-slate-400 block font-medium">RP Statutory Status Note:</span>
                  <p className="text-slate-700 mt-1 whitespace-pre-wrap">{currentCase.statusNote || 'No specific notes recorded.'}</p>
                </div>
              </div>

              {/* Claims & Exposure Summary Card */}
              <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4 flex flex-col justify-between">
                <div>
                  <h3 className="font-bold text-slate-800 text-sm border-b pb-2 flex items-center justify-between">
                    <span>Creditor Claims &amp; Exposure</span>
                    <span className="text-xs bg-slate-100 text-slate-600 px-2 py-0.5 rounded font-mono">Sec. 18 / 21</span>
                  </h3>
                  <div className="space-y-3 mt-4 text-xs">
                    <div className="flex justify-between items-center py-1 border-b border-slate-100">
                      <span className="text-slate-500">Total Admitted Claims:</span>
                      <strong className="text-slate-900 font-mono text-sm">{formatINR(cocSummary.totalAdmitted)}</strong>
                    </div>
                    <div className="flex justify-between items-center py-1 border-b border-slate-100">
                      <span className="text-slate-500">Financial Creditor Claims:</span>
                      <strong className="text-indigo-700 font-mono">{formatINR(cocSummary.totalFinancialAdmitted)}</strong>
                    </div>
                    <div className="flex justify-between items-center py-1 border-b border-slate-100">
                      <span className="text-slate-500">Voting-Eligible Financial Debt:</span>
                      <strong className="text-emerald-700 font-mono">{formatINR(cocSummary.totalVotingEligible)}</strong>
                    </div>
                    <div className="flex justify-between items-center py-1 border-b border-slate-100">
                      <span className="text-slate-500">Related Party Debt (Sec 21(2)):</span>
                      <strong className="text-rose-600 font-mono">{formatINR(cocSummary.relatedPartyDebt)}</strong>
                    </div>
                    <div className="flex justify-between items-center py-1 border-b border-slate-100">
                      <span className="text-slate-500">PUFE Forensic Exposure:</span>
                      <strong className="text-rose-700 font-mono">{formatINR(pufeSummary.totalRecoverable)}</strong>
                    </div>
                    <div className="flex justify-between items-center py-1">
                      <span className="text-slate-500">Est. Liquidation Value (Reg 35):</span>
                      <strong className="text-slate-800 font-mono">{formatINR(valSummary.totalLiquidationValue)}</strong>
                    </div>
                  </div>
                </div>

                <div className="pt-2">
                  <button
                    onClick={() => setActiveTab('coc-resolutions')}
                    className="w-full py-2 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 rounded-lg text-xs font-semibold transition"
                  >
                    View CoC Voting Matrix →
                  </button>
                </div>
              </div>
            </div>
          ) : (
            <div className="bg-white rounded-xl p-8 text-center border border-dashed border-slate-300">
              <span className="text-4xl">🏢</span>
              <h3 className="font-bold text-slate-800 mt-2">No Corporate Debtor Selected</h3>
              <p className="text-xs text-slate-500 max-w-md mx-auto mt-1">
                Select an existing CIRP proceeding from the dropdown above, or register a new Corporate Debtor under Section 7/9/10 admission order.
              </p>
              <button
                onClick={() => setShowCaseModal(true)}
                className="mt-4 px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold rounded-lg shadow-sm"
              >
                + Register First CIRP Case
              </button>
            </div>
          )}

          {/* All Registered CIRP Cases Grid */}
          <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200">
            <h3 className="font-bold text-slate-800 text-sm mb-3">All Active &amp; Archived CIRP Cases</h3>
            <div className="overflow-x-auto">
              <table className="w-full text-xs text-left">
                <thead className="bg-slate-50 text-slate-600 font-semibold border-b">
                  <tr>
                    <th className="py-2.5 px-3">Corporate Debtor</th>
                    <th className="py-2.5 px-3">CIN</th>
                    <th className="py-2.5 px-3">NCLT Bench</th>
                    <th className="py-2.5 px-3">ICD Date</th>
                    <th className="py-2.5 px-3">Resolution Professional</th>
                    <th className="py-2.5 px-3">Stage</th>
                    <th className="py-2.5 px-3">Total Claims</th>
                    <th className="py-2.5 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {cases.map(c => (
                    <tr key={c.id} className={c.id === selectedCaseId ? 'bg-indigo-50/50' : 'hover:bg-slate-50'}>
                      <td className="py-2.5 px-3 font-semibold text-slate-900">{c.corporateDebtorName}</td>
                      <td className="py-2.5 px-3 font-mono text-slate-500">{c.cin}</td>
                      <td className="py-2.5 px-3 text-slate-700">{c.ncltBench}</td>
                      <td className="py-2.5 px-3 text-slate-600">{new Date(c.admissionDate).toLocaleDateString('en-IN')}</td>
                      <td className="py-2.5 px-3 text-slate-700">{c.rpName || c.irpName}</td>
                      <td className="py-2.5 px-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold border ${CIRP_STAGES.find(s => s.key === c.stage)?.badge || 'bg-slate-100 text-slate-700'}`}>
                          {CIRP_STAGES.find(s => s.key === c.stage)?.label || c.stage}
                        </span>
                      </td>
                      <td className="py-2.5 px-3 font-mono font-medium text-slate-800">
                        {formatINR(c.totalAdmittedFinancialDebt + c.totalAdmittedOperationalDebt)}
                      </td>
                      <td className="py-2.5 px-3 text-right">
                        <button
                          onClick={() => setSelectedCaseId(c.id)}
                          className={`text-xs px-2.5 py-1 rounded font-medium ${c.id === selectedCaseId ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'}`}
                        >
                          {c.id === selectedCaseId ? 'Active' : 'Open'}
                        </button>
                      </td>
                    </tr>
                  ))}
                  {cases.length === 0 && (
                    <tr>
                      <td colSpan={8} className="py-6 text-center text-slate-400">No CIRP cases registered yet.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: COMMITTEE OF CREDITORS (CoC) & VOTING ENGINE */}
      {activeTab === 'coc-resolutions' && (
        <div className="space-y-6">
          {/* Statutory Rule Banner */}
          <div className="bg-indigo-50 border border-indigo-200 rounded-xl p-4 text-xs text-indigo-900 flex items-start gap-3 shadow-sm">
            <span className="text-xl">🗳️</span>
            <div className="space-y-1">
              <strong className="font-bold text-sm block">Section 21 &amp; Section 24 Committee of Creditors Statutory Engine</strong>
              <p>
                <strong>Section 21(2) First Proviso:</strong> Related party financial creditors shall have <em>NO right of representation, participation, or voting</em> in a meeting of the Committee of Creditors.
                Voting shares are computed strictly in proportion to non-related admitted financial debt. Operational creditors have right of notice if debt &gt;10% but carry no voting share.
              </p>
              <div className="flex gap-4 font-mono text-[11px] pt-1">
                <span>• Routine Decisions: <strong>51% Simple Majority</strong> (Sec 21(8))</span>
                <span>• Resolution Plan / Extension / Liquidate: <strong>66% Majority</strong> (Sec 30(4), 12(2), 33(2))</span>
                <span>• Section 12A Withdrawal: <strong>90% Supermajority</strong></span>
              </div>
            </div>
          </div>

          {/* Section A: Creditor Claims Ledger */}
          <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b pb-3">
              <div>
                <h3 className="font-bold text-slate-900 text-sm">Admitted Creditor Claims &amp; Proportional CoC Voting Share %</h3>
                <p className="text-xs text-slate-500">Automatic real-time re-allocation of voting shares upon claim admission.</p>
              </div>
              {currentCase && (
                <button
                  onClick={() => setShowMemberModal(true)}
                  className="bg-indigo-600 hover:bg-indigo-500 text-white font-medium text-xs px-3 py-1.5 rounded-lg shadow-sm flex items-center gap-1"
                >
                  <span>+</span> Admit Creditor Claim
                </button>
              )}
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-xs text-left">
                <thead className="bg-slate-50 text-slate-600 font-semibold border-b">
                  <tr>
                    <th className="py-2.5 px-3">Creditor Name</th>
                    <th className="py-2.5 px-3">Class</th>
                    <th className="py-2.5 px-3">Claim Submitted</th>
                    <th className="py-2.5 px-3">Admitted Amount</th>
                    <th className="py-2.5 px-3 text-center">Related Party?</th>
                    <th className="py-2.5 px-3">Security Interest</th>
                    <th className="py-2.5 px-3 text-right font-bold text-indigo-900">CoC Voting %</th>
                    <th className="py-2.5 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {currentCase?.cocMembers?.map(m => (
                    <tr key={m.id} className="hover:bg-slate-50">
                      <td className="py-2.5 px-3 font-semibold text-slate-900">{m.creditorName}</td>
                      <td className="py-2.5 px-3 text-slate-600">
                        {CREDITOR_CATEGORIES.find(c => c.key === m.category)?.label || m.category}
                      </td>
                      <td className="py-2.5 px-3 font-mono text-slate-500">{formatINR(m.claimSubmittedAmount)}</td>
                      <td className="py-2.5 px-3 font-mono font-semibold text-slate-800">{formatINR(m.admittedClaimAmount)}</td>
                      <td className="py-2.5 px-3 text-center">
                        {m.isRelatedParty ? (
                          <span className="bg-rose-100 text-rose-800 text-[10px] px-2 py-0.5 rounded font-bold">
                            Disqualified (Sec 21(2))
                          </span>
                        ) : (
                          <span className="text-emerald-700 font-semibold text-[11px]">No (Eligible)</span>
                        )}
                      </td>
                      <td className="py-2.5 px-3 text-slate-600 max-w-xs truncate" title={m.securityInterestDescription}>
                        {m.securityInterestDescription || 'Unsecured / None'}
                      </td>
                      <td className="py-2.5 px-3 text-right font-mono font-black text-sm">
                        {m.votingSharePct > 0 ? (
                          <span className="text-indigo-700 bg-indigo-50 px-2 py-0.5 rounded">
                            {m.votingSharePct.toFixed(2)}%
                          </span>
                        ) : (
                          <span className="text-slate-400">0.00%</span>
                        )}
                      </td>
                      <td className="py-2.5 px-3 text-right">
                        <button
                          onClick={() => handleDeleteMember(m.id)}
                          className="text-rose-600 hover:text-rose-800 font-medium"
                        >
                          Remove
                        </button>
                      </td>
                    </tr>
                  ))}
                  {(!currentCase?.cocMembers || currentCase.cocMembers.length === 0) && (
                    <tr>
                      <td colSpan={8} className="py-6 text-center text-slate-400">No creditors admitted for this corporate debtor yet.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          {/* Section B: CoC Voting Resolutions Studio */}
          <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b pb-3">
              <div>
                <h3 className="font-bold text-slate-900 text-sm">CoC Resolution Voting Studio &amp; Threshold Validator</h3>
                <p className="text-xs text-slate-500">Evaluates e-voting shares against mandatory statutory benchmarks under IBC 2016.</p>
              </div>
              {currentCase && (
                <button
                  onClick={() => setShowResolutionModal(true)}
                  className="bg-emerald-600 hover:bg-emerald-500 text-white font-medium text-xs px-3 py-1.5 rounded-lg shadow-sm flex items-center gap-1"
                >
                  <span>+</span> Propose CoC Resolution
                </button>
              )}
            </div>

            <div className="space-y-3">
              {currentCase?.resolutions?.map(r => (
                <div key={r.id} className="border border-slate-200 rounded-xl p-4 hover:border-indigo-300 transition-all bg-white shadow-xs">
                  <div className="flex flex-col md:flex-row md:items-center justify-between gap-2 border-b pb-2">
                    <div className="flex items-center gap-2">
                      <span className="bg-slate-100 text-slate-800 font-mono text-[11px] font-bold px-2 py-0.5 rounded">
                        CoC #{r.meetingNumber} • {r.resolutionNumber}
                      </span>
                      <h4 className="font-bold text-slate-900 text-sm">{r.resolutionTitle}</h4>
                      <span className="text-xs text-indigo-700 bg-indigo-50 font-semibold px-2 py-0.5 rounded">
                        {r.statutorySection}
                      </span>
                    </div>
                    <div className="flex items-center gap-2">
                      <span className={`text-xs px-2.5 py-0.5 rounded-full font-bold border ${r.status === 'Approved' ? 'bg-emerald-100 text-emerald-800 border-emerald-300' : r.status === 'Rejected' ? 'bg-rose-100 text-rose-800 border-rose-300' : 'bg-amber-100 text-amber-800 border-amber-300'}`}>
                        {r.status === 'Approved' ? '✅ Passed by CoC' : r.status === 'Rejected' ? '❌ Defeated' : '⏳ Pending Vote'}
                      </span>
                      <button
                        onClick={() => handleDeleteResolution(r.id)}
                        className="text-slate-400 hover:text-rose-600 text-xs px-1"
                      >
                        ✕
                      </button>
                    </div>
                  </div>

                  <div className="mt-3 grid grid-cols-1 sm:grid-cols-4 gap-3 text-xs">
                    <div>
                      <span className="text-slate-400 block">Required Threshold:</span>
                      <strong className="text-slate-800 font-mono text-sm">{r.requiredThresholdPct}% Majority</strong>
                    </div>
                    <div>
                      <span className="text-slate-400 block">In Favour:</span>
                      <strong className="text-emerald-700 font-mono text-sm">{r.votingShareInFavourPct.toFixed(2)}%</strong>
                    </div>
                    <div>
                      <span className="text-slate-400 block">Against:</span>
                      <strong className="text-rose-700 font-mono text-sm">{r.votingShareAgainstPct.toFixed(2)}%</strong>
                    </div>
                    <div>
                      <span className="text-slate-400 block">Abstained:</span>
                      <strong className="text-slate-500 font-mono text-sm">{r.votingShareAbstainedPct.toFixed(2)}%</strong>
                    </div>
                  </div>

                  {/* Visual Voting Bar */}
                  <div className="mt-3">
                    <div className="w-full bg-slate-100 h-2 rounded-full overflow-hidden flex">
                      <div className="bg-emerald-500 h-full" style={{ width: `${Math.min(100, r.votingShareInFavourPct)}%` }} title={`In Favour: ${r.votingShareInFavourPct}%`} />
                      <div className="bg-rose-500 h-full" style={{ width: `${Math.min(100, r.votingShareAgainstPct)}%` }} title={`Against: ${r.votingShareAgainstPct}%`} />
                      <div className="bg-slate-300 h-full" style={{ width: `${Math.min(100, r.votingShareAbstainedPct)}%` }} title={`Abstained: ${r.votingShareAbstainedPct}%`} />
                    </div>
                    <div className="flex justify-between items-center text-[10px] text-slate-400 mt-1 font-mono">
                      <span>0%</span>
                      <span className="font-bold text-indigo-700">Required: {r.requiredThresholdPct}%</span>
                      <span>100%</span>
                    </div>
                  </div>
                </div>
              ))}
              {(!currentCase?.resolutions || currentCase.resolutions.length === 0) && (
                <div className="p-6 text-center text-slate-400 text-xs">No CoC voting resolutions logged yet.</div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* TAB 3: PUFE FORENSIC AVOIDANCE STUDIO */}
      {activeTab === 'pufe-forensics' && (
        <div className="space-y-6">
          {/* Statutory Reference Strip */}
          <div className="bg-rose-50 border border-rose-200 rounded-xl p-4 text-xs text-rose-950 flex items-start gap-3 shadow-sm">
            <span className="text-xl">⚖️</span>
            <div className="space-y-1">
              <strong className="font-bold text-sm block">IBC 2016 Avoidance &amp; Vulnerable Transaction Forensic Framework</strong>
              <p>
                Resolution Professionals are statutorily mandated under <strong>Regulation 35A</strong> to form an opinion within 75 days, make a determination within 115 days, and apply to the Adjudicating Authority (NCLT) by the 135th day for avoidance of transactions under:
              </p>
              <div className="grid grid-cols-1 md:grid-cols-4 gap-2 pt-1 font-mono text-[11px]">
                <div className="bg-white/60 p-1.5 rounded"><strong>Sec 43 (Preferential):</strong> 1 yr (unrelated) / 2 yrs (related)</div>
                <div className="bg-white/60 p-1.5 rounded"><strong>Sec 45 (Undervalued):</strong> 1 yr (unrelated) / 2 yrs (related)</div>
                <div className="bg-white/60 p-1.5 rounded"><strong>Sec 50 (Extortionate):</strong> 2 years preceding ICD</div>
                <div className="bg-white/60 p-1.5 rounded"><strong>Sec 66 (Fraudulent):</strong> No lookback limitation</div>
              </div>
            </div>
          </div>

          {/* PUFE Exposure Summary Cards */}
          <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <span className="text-slate-500 text-xs">Total Avoidance Transactions</span>
              <div className="text-xl font-bold font-mono text-slate-900 mt-1">{currentCase?.pufeTransactions?.length || 0} Records</div>
            </div>
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <span className="text-slate-500 text-xs">Gross Avoidance Amount</span>
              <div className="text-xl font-bold font-mono text-slate-800 mt-1">{formatINR(pufeSummary.totalAmount)}</div>
            </div>
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <span className="text-slate-500 text-xs">Estimated Recoverable Amount</span>
              <div className="text-xl font-bold font-mono text-rose-700 mt-1">{formatINR(pufeSummary.totalRecoverable)}</div>
            </div>
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <span className="text-slate-500 text-xs">NCLT Applications Filed</span>
              <div className="text-xl font-bold font-mono text-indigo-700 mt-1">{pufeSummary.countApplications} Filed</div>
            </div>
          </div>

          {/* PUFE Transaction Ledger */}
          <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b pb-3">
              <div>
                <h3 className="font-bold text-slate-900 text-sm">Forensic Avoidance Ledger (PUFE)</h3>
                <p className="text-xs text-slate-500">Includes lookback calculation and recoverable clawback estimates.</p>
              </div>
              {currentCase && (
                <button
                  onClick={() => setShowPufeModal(true)}
                  className="bg-rose-600 hover:bg-rose-500 text-white font-medium text-xs px-3 py-1.5 rounded-lg shadow-sm flex items-center gap-1"
                >
                  <span>+</span> Record PUFE Avoidance Item
                </button>
              )}
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-xs text-left">
                <thead className="bg-slate-50 text-slate-600 font-semibold border-b">
                  <tr>
                    <th className="py-2.5 px-3">Type</th>
                    <th className="py-2.5 px-3">Date</th>
                    <th className="py-2.5 px-3">Counterparty</th>
                    <th className="py-2.5 px-3">Related Party?</th>
                    <th className="py-2.5 px-3">Look-back Status</th>
                    <th className="py-2.5 px-3">Transaction Amt</th>
                    <th className="py-2.5 px-3">Recoverable Amt</th>
                    <th className="py-2.5 px-3">NCLT Status</th>
                    <th className="py-2.5 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {currentCase?.pufeTransactions?.map(p => (
                    <tr key={p.id} className="hover:bg-slate-50">
                      <td className="py-2.5 px-3 font-semibold text-slate-900">
                        {PUFE_TYPES.find(t => t.key === p.pufeType)?.label || p.pufeType}
                        <span className="text-[10px] text-slate-400 block font-normal">{PUFE_TYPES.find(t => t.key === p.pufeType)?.section}</span>
                      </td>
                      <td className="py-2.5 px-3 text-slate-600 font-mono">{new Date(p.transactionDate).toLocaleDateString('en-IN')}</td>
                      <td className="py-2.5 px-3 font-medium text-slate-800">{p.counterpartyName}</td>
                      <td className="py-2.5 px-3">
                        {p.isRelatedParty ? (
                          <span className="bg-amber-100 text-amber-800 text-[10px] px-2 py-0.5 rounded font-bold">Related Party</span>
                        ) : (
                          <span className="text-slate-500 text-[11px]">Unrelated (3rd Party)</span>
                        )}
                      </td>
                      <td className="py-2.5 px-3">
                        {p.isWithinLookbackPeriod ? (
                          <span className="bg-rose-100 text-rose-800 text-[10px] px-2 py-0.5 rounded font-bold flex items-center gap-1 w-max">
                            <span>🎯</span> Within Statutory Lookback ({p.lookbackPeriodYears}y)
                          </span>
                        ) : (
                          <span className="bg-slate-100 text-slate-600 text-[10px] px-2 py-0.5 rounded font-medium">
                            Time-Barred
                          </span>
                        )}
                      </td>
                      <td className="py-2.5 px-3 font-mono font-medium text-slate-700">{formatINR(p.transactionAmount)}</td>
                      <td className="py-2.5 px-3 font-mono font-bold text-rose-700">{formatINR(p.estimatedAvoidanceRecoverableAmount)}</td>
                      <td className="py-2.5 px-3">
                        {p.ncltApplicationFiled ? (
                          <span className="bg-emerald-100 text-emerald-800 text-[10px] px-2 py-0.5 rounded font-bold">NCLT App Filed</span>
                        ) : (
                          <span className="bg-slate-100 text-slate-600 text-[10px] px-2 py-0.5 rounded">Pending Application</span>
                        )}
                      </td>
                      <td className="py-2.5 px-3 text-right">
                        <button
                          onClick={() => handleDeletePufe(p.id)}
                          className="text-rose-600 hover:text-rose-800 font-medium"
                        >
                          Delete
                        </button>
                      </td>
                    </tr>
                  ))}
                  {(!currentCase?.pufeTransactions || currentCase.pufeTransactions.length === 0) && (
                    <tr>
                      <td colSpan={9} className="py-6 text-center text-slate-400">No PUFE forensic audit items recorded.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 4: REGULATION 35 REGISTERED VALUATIONS */}
      {activeTab === 'asset-valuations' && (
        <div className="space-y-6">
          {/* Statutory Rule Banner */}
          <div className="bg-purple-50 border border-purple-200 rounded-xl p-4 text-xs text-purple-950 flex items-start gap-3 shadow-sm">
            <span className="text-xl">💎</span>
            <div className="space-y-1">
              <strong className="font-bold text-sm block">IBBI (CIRP) Regulation 35 Fair Value &amp; Liquidation Value Protocol</strong>
              <p>
                Two registered valuers must be appointed under Regulation 27 to submit independent estimates of Fair Value and Liquidation Value.
                The average of both estimates constitutes the official value.
                <strong>Regulation 35(1)(b):</strong> If the two estimates differ significantly (greater than 25% of the higher valuation), the RP must appoint a <em>third registered valuer</em>.
              </p>
            </div>
          </div>

          {/* Divergence Alert if >25% */}
          {valSummary.hasThirdValuerRequired && (
            <div className="bg-rose-100 border border-rose-300 text-rose-900 px-4 py-3 rounded-xl text-xs flex items-center gap-2 font-semibold">
              <span className="text-lg">🚨</span>
              <span>
                <strong>Statutory Action Required u/Reg 35(1)(b):</strong> One or more asset classes exhibit a valuation variance exceeding 25%.
                The Resolution Professional is mandated to appoint a third registered valuer!
              </span>
            </div>
          )}

          {/* Valuation Summary Totals */}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <span className="text-slate-500 text-xs">Total Enterprise Fair Value</span>
              <div className="text-2xl font-black font-mono text-purple-700 mt-1">{formatINR(valSummary.totalFairValue)}</div>
              <span className="text-[11px] text-slate-400 mt-0.5 block">Average of registered valuers</span>
            </div>
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <span className="text-slate-500 text-xs">Total Enterprise Liquidation Value</span>
              <div className="text-2xl font-black font-mono text-slate-800 mt-1">{formatINR(valSummary.totalLiquidationValue)}</div>
              <span className="text-[11px] text-slate-400 mt-0.5 block">Estimated realization in forced sale</span>
            </div>
            <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
              <span className="text-slate-500 text-xs">Statutory Valuer Variance Check</span>
              <div className={`text-lg font-bold mt-1 ${valSummary.hasThirdValuerRequired ? 'text-rose-700' : 'text-emerald-700'}`}>
                {valSummary.hasThirdValuerRequired ? '⚠️ >25% Disparity Detected' : '✅ Within Acceptable Limits (≤25%)'}
              </div>
              <span className="text-[11px] text-slate-400 mt-0.5 block">Reg. 35(1)(b) compliance</span>
            </div>
          </div>

          {/* Valuation Items Table */}
          <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b pb-3">
              <div>
                <h3 className="font-bold text-slate-900 text-sm">Class-wise Registered Valuer Estimates</h3>
                <p className="text-xs text-slate-500">Asset classes: Land &amp; Building, Plant &amp; Machinery, Securities.</p>
              </div>
              {currentCase && (
                <button
                  onClick={() => setShowValuationModal(true)}
                  className="bg-purple-600 hover:bg-purple-500 text-white font-medium text-xs px-3 py-1.5 rounded-lg shadow-sm flex items-center gap-1"
                >
                  <span>+</span> Add Asset Valuation
                </button>
              )}
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-xs text-left">
                <thead className="bg-slate-50 text-slate-600 font-semibold border-b">
                  <tr>
                    <th className="py-2.5 px-3">Asset Class &amp; Description</th>
                    <th className="py-2.5 px-3">Valuer 1 (Fair / Liq)</th>
                    <th className="py-2.5 px-3">Valuer 2 (Fair / Liq)</th>
                    <th className="py-2.5 px-3">Average Fair Value</th>
                    <th className="py-2.5 px-3">Average Liquidation Value</th>
                    <th className="py-2.5 px-3 text-center">3rd Valuer Required?</th>
                    <th className="py-2.5 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {currentCase?.valuations?.map(v => (
                    <tr key={v.id} className="hover:bg-slate-50">
                      <td className="py-2.5 px-3">
                        <strong className="text-slate-900 block">{ASSET_CLASSES.find(c => c.key === v.assetClass)?.label || v.assetClass}</strong>
                        <span className="text-slate-500 text-[11px] block mt-0.5">{v.assetDescription}</span>
                      </td>
                      <td className="py-2.5 px-3 text-slate-700">
                        <div className="font-medium text-slate-900">{v.valuer1Name}</div>
                        <div className="font-mono text-slate-500">Fair: {formatINR(v.valuer1FairValue)}</div>
                        <div className="font-mono text-slate-400">Liq: {formatINR(v.valuer1LiquidationValue)}</div>
                      </td>
                      <td className="py-2.5 px-3 text-slate-700">
                        <div className="font-medium text-slate-900">{v.valuer2Name}</div>
                        <div className="font-mono text-slate-500">Fair: {formatINR(v.valuer2FairValue)}</div>
                        <div className="font-mono text-slate-400">Liq: {formatINR(v.valuer2LiquidationValue)}</div>
                      </td>
                      <td className="py-2.5 px-3 font-mono font-bold text-purple-800 text-sm">{formatINR(v.averageFairValue)}</td>
                      <td className="py-2.5 px-3 font-mono font-bold text-slate-800">{formatINR(v.averageLiquidationValue)}</td>
                      <td className="py-2.5 px-3 text-center">
                        {v.thirdValuerRequired ? (
                          <span className="bg-rose-100 text-rose-800 text-[10px] px-2 py-0.5 rounded font-bold">
                            ⚠️ YES (&gt;25% variance)
                          </span>
                        ) : (
                          <span className="bg-emerald-100 text-emerald-800 text-[10px] px-2 py-0.5 rounded font-semibold">
                            No (≤25%)
                          </span>
                        )}
                      </td>
                      <td className="py-2.5 px-3 text-right">
                        <button
                          onClick={() => handleDeleteValuation(v.id)}
                          className="text-rose-600 hover:text-rose-800 font-medium"
                        >
                          Delete
                        </button>
                      </td>
                    </tr>
                  ))}
                  {(!currentCase?.valuations || currentCase.valuations.length === 0) && (
                    <tr>
                      <td colSpan={7} className="py-6 text-center text-slate-400">No registered asset valuations recorded yet.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 5: REGULATION 36 INFORMATION MEMORANDUM (IM) */}
      {activeTab === 'im-dossier' && (
        <div className="space-y-6">
          <div className="bg-white rounded-xl p-6 shadow-sm border border-slate-200 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b pb-4">
              <div>
                <h3 className="font-bold text-slate-900 text-base">Regulation 36 Information Memorandum (IM) Generator</h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Prepares the statutory Information Memorandum for Prospective Resolution Applicants (PRAs) under IBBI (CIRP) Regulations, 2016.
                </p>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={handleOpenDossier}
                  disabled={!currentCase || loading}
                  className="bg-emerald-600 hover:bg-emerald-500 text-white font-medium text-xs px-4 py-2 rounded-lg shadow-sm flex items-center gap-1.5"
                >
                  <span>👁️</span> Preview &amp; Print Full IM Dossier
                </button>
              </div>
            </div>

            {/* Checklist of Regulation 36 Statutory Inclusions */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
              <div className="border border-slate-200 rounded-xl p-4 bg-slate-50 space-y-2">
                <h4 className="font-bold text-slate-800">Mandatory Regulation 36(2) Contents</h4>
                <ul className="space-y-1.5 text-slate-600">
                  <li className="flex items-center gap-2"><span>✅</span> <strong>Clause (a):</strong> Assets &amp; Liabilities as on Insolvency Commencement Date</li>
                  <li className="flex items-center gap-2"><span>✅</span> <strong>Clause (b):</strong> Audited Financial Statements for preceding two financial years</li>
                  <li className="flex items-center gap-2"><span>✅</span> <strong>Clause (c):</strong> List of creditors containing admitted claims and voting shares</li>
                  <li className="flex items-center gap-2"><span>✅</span> <strong>Clause (d):</strong> Particulars of debts due to/from related parties</li>
                  <li className="flex items-center gap-2"><span>✅</span> <strong>Clause (e):</strong> Details of guarantees given by Corporate Debtor</li>
                </ul>
              </div>
              <div className="border border-slate-200 rounded-xl p-4 bg-slate-50 space-y-2">
                <h4 className="font-bold text-slate-800">Forensic &amp; Valuation Annexures</h4>
                <ul className="space-y-1.5 text-slate-600">
                  <li className="flex items-center gap-2"><span>✅</span> <strong>Clause (g):</strong> Ongoing litigations and statutory investigations</li>
                  <li className="flex items-center gap-2"><span>✅</span> <strong>Clause (h):</strong> Forensic findings on PUFE transactions (Sec 43, 45, 50, 66)</li>
                  <li className="flex items-center gap-2"><span>🔒</span> <strong>Regulation 35:</strong> Fair Value &amp; Liquidation Value (Confidential to CoC)</li>
                  <li className="flex items-center gap-2"><span>✅</span> <strong>Statutory Disclaimer:</strong> Regulation 36(4) confidentiality undertaking</li>
                  <li className="flex items-center gap-2"><span>✅</span> <strong>RP Certification:</strong> Sign-off with IBBI IP Registration number</li>
                </ul>
              </div>
            </div>

            {currentCase && (
              <div className="bg-indigo-50 border border-indigo-200 rounded-xl p-4 text-xs text-indigo-950 flex justify-between items-center">
                <div>
                  <strong className="block font-bold">Ready for Resolution Professional Verification</strong>
                  <span className="text-slate-600">Corporate Debtor: {currentCase.corporateDebtorName} • Admitted Claims: {formatINR(cocSummary.totalAdmitted)}</span>
                </div>
                <button
                  onClick={handleOpenDossier}
                  className="bg-indigo-600 hover:bg-indigo-500 text-white font-medium text-xs px-3.5 py-2 rounded-lg shadow-sm"
                >
                  Generate Regulation 36 Dossier →
                </button>
              </div>
            )}
          </div>
        </div>
      )}

      {/* MODAL 1: CREATE / EDIT CIRP CASE */}
      {showCaseModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Corporate Debtor Master &amp; CIRP Admission Order</h3>
              <button onClick={() => setShowCaseModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSaveCase} className="space-y-4 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Client Account</label>
                  <select
                    value={formClientId}
                    onChange={e => setFormClientId(e.target.value)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                    required
                  >
                    <option value="">Select client...</option>
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Corporate Debtor Name</label>
                  <input
                    type="text"
                    value={formDebtorName}
                    onChange={e => setFormDebtorName(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="e.g. Zenith Heavy Engineering Ltd"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">CIN (21 Chars)</label>
                  <input
                    type="text"
                    value={formCin}
                    onChange={e => setFormCin(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono uppercase"
                    placeholder="L28100MH2005PLC154890"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">NCLT Adjudicating Bench</label>
                  <input
                    type="text"
                    value={formBench}
                    onChange={e => setFormBench(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="NCLT Mumbai Bench (Court-I)"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Admission Order Number</label>
                  <input
                    type="text"
                    value={formOrderNo}
                    onChange={e => setFormOrderNo(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    placeholder="CP (IB) No. 1042/MB/2024"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Insolvency Commencement Date (ICD)</label>
                  <input
                    type="date"
                    value={formAdmissionDate}
                    onChange={e => setFormAdmissionDate(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Interim Resolution Professional (IRP)</label>
                  <input
                    type="text"
                    value={formIrpName}
                    onChange={e => setFormIrpName(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="CA Rajesh Sharma"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">IRP IBBI Reg. Number</label>
                  <input
                    type="text"
                    value={formIrpRegNo}
                    onChange={e => setFormIrpRegNo(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono text-[11px]"
                    placeholder="IBBI/IPA-001/IP-P01234/2020-2021/11987"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Resolution Professional (RP)</label>
                  <input
                    type="text"
                    value={formRpName}
                    onChange={e => setFormRpName(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="CA Rajesh Sharma"
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">RP IBBI Reg. Number</label>
                  <input
                    type="text"
                    value={formRpRegNo}
                    onChange={e => setFormRpRegNo(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono text-[11px]"
                    placeholder="IBBI/IPA-001/IP-P01234/2020-2021/11987"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">CIRP Current Stage</label>
                  <select
                    value={formStage}
                    onChange={e => setFormStage(e.target.value as CirpStage)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    {CIRP_STAGES.map(s => (
                      <option key={s.key} value={s.key}>{s.label}</option>
                    ))}
                  </select>
                </div>
                <div className="flex items-center pt-5">
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={formMoratorium}
                      onChange={e => setFormMoratorium(e.target.checked)}
                      className="rounded text-indigo-600 focus:ring-indigo-500"
                    />
                    <span className="font-semibold text-slate-800">Moratorium Declared u/s 14 of IBC 2016</span>
                  </label>
                </div>
              </div>

              <div>
                <label className="font-semibold text-slate-700">Status &amp; Public Announcement Note</label>
                <textarea
                  value={formStatusNote}
                  onChange={e => setFormStatusNote(e.target.value)}
                  className="w-full mt-1 p-2 border rounded h-16"
                  placeholder="Public announcement Form A published on..."
                />
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowCaseModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded font-medium shadow-sm"
                >
                  Save CIRP Case
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: ADD CREDITOR CLAIM (CoC MEMBER) */}
      {showMemberModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Admit Creditor Claim &amp; CoC Voting Share</h3>
              <button onClick={() => setShowMemberModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSaveMember} className="space-y-4 text-xs">
              <div>
                <label className="font-semibold text-slate-700">Creditor Name</label>
                <input
                  type="text"
                  value={memberCreditorName}
                  onChange={e => setMemberCreditorName(e.target.value)}
                  className="w-full mt-1 p-2 border rounded"
                  placeholder="e.g. State Bank of India / HDFC Bank"
                  required
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Creditor Class</label>
                  <select
                    value={memberCategory}
                    onChange={e => setMemberCategory(e.target.value as CreditorCategory)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    {CREDITOR_CATEGORIES.map(c => (
                      <option key={c.key} value={c.key}>{c.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Section 21(2) Related Party?</label>
                  <div className="mt-2">
                    <label className="flex items-center gap-2 cursor-pointer">
                      <input
                        type="checkbox"
                        checked={memberIsRelatedParty}
                        onChange={e => setMemberIsRelatedParty(e.target.checked)}
                        className="rounded text-rose-600 focus:ring-rose-500"
                      />
                      <span className="text-slate-700 font-semibold">Yes, Related Party (0% Voting Share)</span>
                    </label>
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Claim Submitted Amount (₹)</label>
                  <input
                    type="number"
                    step="any"
                    value={memberClaimSubmitted}
                    onChange={e => setMemberClaimSubmitted(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Admitted Claim Amount (₹)</label>
                  <input
                    type="number"
                    step="any"
                    value={memberAdmittedClaim}
                    onChange={e => setMemberAdmittedClaim(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono font-bold text-slate-900"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-slate-700">Security Interest / Collateral Description</label>
                <textarea
                  value={memberSecurityDesc}
                  onChange={e => setMemberSecurityDesc(e.target.value)}
                  className="w-full mt-1 p-2 border rounded h-16"
                  placeholder="First pari passu mortgage over factory premises..."
                />
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowMemberModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded font-medium shadow-sm"
                >
                  Save Creditor Claim
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: PROPOSE CoC RESOLUTION */}
      {showResolutionModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Propose CoC Resolution &amp; Record Voting Results</h3>
              <button onClick={() => setShowResolutionModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSaveResolution} className="space-y-4 text-xs">
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">CoC Meeting #</label>
                  <input
                    type="number"
                    value={resMeetingNo}
                    onChange={e => setResMeetingNo(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Resolution No.</label>
                  <input
                    type="text"
                    value={resNumber}
                    onChange={e => setResNumber(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    placeholder="RES/CoC-03/01"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Statutory Section</label>
                  <input
                    type="text"
                    value={resStatutorySection}
                    onChange={e => setResStatutorySection(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="Section 30(4)"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-slate-700">Resolution Title / Matter</label>
                <input
                  type="text"
                  value={resTitle}
                  onChange={e => setResTitle(e.target.value)}
                  className="w-full mt-1 p-2 border rounded"
                  placeholder="e.g. Approval of Resolution Plan of Apex Consortium"
                  required
                />
              </div>

              <div className="grid grid-cols-4 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Required %</label>
                  <select
                    value={resRequiredPct}
                    onChange={e => setResRequiredPct(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded bg-white font-mono"
                  >
                    <option value={51}>51% (Routine)</option>
                    <option value={66}>66% (Plan / Ext.)</option>
                    <option value={90}>90% (Sec 12A)</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">In Favour %</label>
                  <input
                    type="number"
                    step="0.01"
                    value={resInFavourPct}
                    onChange={e => setResInFavourPct(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono font-bold text-emerald-700"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Against %</label>
                  <input
                    type="number"
                    step="0.01"
                    value={resAgainstPct}
                    onChange={e => setResAgainstPct(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono text-rose-700"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Abstained %</label>
                  <input
                    type="number"
                    step="0.01"
                    value={resAbstainedPct}
                    onChange={e => setResAbstainedPct(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono text-slate-500"
                    required
                  />
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowResolutionModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-medium shadow-sm"
                >
                  Record Resolution Vote
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: RECORD PUFE TRANSACTION */}
      {showPufeModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Record PUFE Avoidance Transaction</h3>
              <button onClick={() => setShowPufeModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSavePufe} className="space-y-4 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Avoidance Transaction Category</label>
                  <select
                    value={pufeType}
                    onChange={e => setPufeType(e.target.value as PufeType)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    {PUFE_TYPES.map(t => (
                      <option key={t.key} value={t.key}>{t.label} ({t.section})</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Transaction Date</label>
                  <input
                    type="date"
                    value={pufeDate}
                    onChange={e => setPufeDate(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Counterparty Name</label>
                  <input
                    type="text"
                    value={pufeCounterparty}
                    onChange={e => setPufeCounterparty(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="e.g. Zenith Suppliers Pvt Ltd"
                    required
                  />
                </div>
                <div className="flex items-center pt-5">
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={pufeIsRelatedParty}
                      onChange={e => setPufeIsRelatedParty(e.target.checked)}
                      className="rounded text-rose-600 focus:ring-rose-500"
                    />
                    <span className="text-slate-800 font-semibold">Related Party (Extends Lookback to 2y)</span>
                  </label>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Transaction Amount (₹)</label>
                  <input
                    type="number"
                    step="any"
                    value={pufeAmount}
                    onChange={e => setPufeAmount(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Estimated Clawback Recoverable (₹)</label>
                  <input
                    type="number"
                    step="any"
                    value={pufeRecoverable}
                    onChange={e => setPufeRecoverable(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono font-bold text-rose-700"
                    required
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-slate-700">Forensic Auditor Observations</label>
                <textarea
                  value={pufeObservations}
                  onChange={e => setPufeObservations(e.target.value)}
                  className="w-full mt-1 p-2 border rounded h-16"
                  placeholder="Observations indicating preference, undervaluation, or fraudulent intent..."
                />
              </div>

              <div>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={pufeNcltFiled}
                    onChange={e => setPufeNcltFiled(e.target.checked)}
                    className="rounded text-indigo-600 focus:ring-indigo-500"
                  />
                  <span className="text-slate-800 font-semibold">Application under Regulation 35A filed before NCLT</span>
                </label>
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowPufeModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-rose-600 hover:bg-rose-500 text-white rounded font-medium shadow-sm"
                >
                  Save Avoidance Item
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 5: RECORD ASSET VALUATION */}
      {showValuationModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Record Regulation 35 Registered Valuation</h3>
              <button onClick={() => setShowValuationModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSaveValuation} className="space-y-4 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Asset Class</label>
                  <select
                    value={valClass}
                    onChange={e => setValClass(e.target.value as AssetValuationClass)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    {ASSET_CLASSES.map(c => (
                      <option key={c.key} value={c.key}>{c.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Asset Description</label>
                  <input
                    type="text"
                    value={valDesc}
                    onChange={e => setValDesc(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="e.g. Factory land at MIDC Pune"
                    required
                  />
                </div>
              </div>

              {/* Valuer 1 Details */}
              <div className="border border-slate-200 rounded-lg p-3 bg-slate-50 space-y-2">
                <span className="font-bold text-slate-800 block">Registered Valuer #1</span>
                <input
                  type="text"
                  value={val1Name}
                  onChange={e => setVal1Name(e.target.value)}
                  className="w-full p-1.5 border rounded text-xs bg-white"
                  placeholder="Valuer 1 Name & Entity"
                  required
                />
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <label className="text-slate-500 text-[11px]">Fair Value (₹)</label>
                    <input
                      type="number"
                      step="any"
                      value={val1Fair}
                      onChange={e => setVal1Fair(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                      required
                    />
                  </div>
                  <div>
                    <label className="text-slate-500 text-[11px]">Liquidation Value (₹)</label>
                    <input
                      type="number"
                      step="any"
                      value={val1Liq}
                      onChange={e => setVal1Liq(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                      required
                    />
                  </div>
                </div>
              </div>

              {/* Valuer 2 Details */}
              <div className="border border-slate-200 rounded-lg p-3 bg-slate-50 space-y-2">
                <span className="font-bold text-slate-800 block">Registered Valuer #2</span>
                <input
                  type="text"
                  value={val2Name}
                  onChange={e => setVal2Name(e.target.value)}
                  className="w-full p-1.5 border rounded text-xs bg-white"
                  placeholder="Valuer 2 Name & Entity"
                  required
                />
                <div className="grid grid-cols-2 gap-2">
                  <div>
                    <label className="text-slate-500 text-[11px]">Fair Value (₹)</label>
                    <input
                      type="number"
                      step="any"
                      value={val2Fair}
                      onChange={e => setVal2Fair(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                      required
                    />
                  </div>
                  <div>
                    <label className="text-slate-500 text-[11px]">Liquidation Value (₹)</label>
                    <input
                      type="number"
                      step="any"
                      value={val2Liq}
                      onChange={e => setVal2Liq(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                      required
                    />
                  </div>
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowValuationModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-purple-600 hover:bg-purple-500 text-white rounded font-medium shadow-sm"
                >
                  Save Valuation
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 6: REGULATION 36 INFORMATION MEMORANDUM (IM) HTML DOSSIER VIEWER */}
      {showDossierModal && (
        <div className="fixed inset-0 bg-slate-900/70 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-5xl w-full h-[90vh] shadow-2xl flex flex-col overflow-hidden">
            <div className="p-4 bg-slate-900 text-white flex justify-between items-center">
              <div>
                <h3 className="font-bold text-sm flex items-center gap-2">
                  <span>📄</span> Official Regulation 36 Information Memorandum (IM) Dossier
                </h3>
                <p className="text-xs text-slate-400">IBBI (Insolvency Resolution Process for Corporate Persons) Regulations, 2016</p>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => {
                    const printWin = window.open('', '_blank')
                    if (printWin) {
                      printWin.document.write(dossierHtml)
                      printWin.document.close()
                      printWin.focus()
                      printWin.print()
                    }
                  }}
                  className="bg-indigo-600 hover:bg-indigo-500 text-white px-3 py-1.5 rounded text-xs font-semibold flex items-center gap-1"
                >
                  <span>🖨️</span> Print / Export PDF
                </button>
                <button
                  onClick={() => setShowDossierModal(false)}
                  className="text-slate-400 hover:text-white font-bold text-xl px-2"
                >
                  ×
                </button>
              </div>
            </div>
            <div className="flex-1 overflow-auto bg-slate-100 p-4">
              <iframe
                title="IM Dossier Viewer"
                srcDoc={dossierHtml}
                className="w-full h-full bg-white rounded-lg shadow border border-slate-200"
              />
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
