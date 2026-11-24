import { useState, useEffect, useMemo, type FormEvent } from 'react'
import {
  esgCsrApi,
  api,
  type CsrAssessmentDto,
  type CsrAssessmentDetailDto,
  type BrsrCoreReportDto,
  type SaveCsrAssessmentRequest,
  type SaveCsrProjectRequest,
  type SaveBrsrReportRequest,
  type CertifyCsrRequest,
  type CertifyBrsrRequest,
  type CalculateCsrObligationRequest,
  type CsrObligationCalculationDto,
  type ScheduleViiCategory,
  type CsrProjectNature,
  type Client
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

const SCHEDULE_VII_CATEGORIES: { key: ScheduleViiCategory; label: string }[] = [
  { key: 'EradicatingHungerPovertyAndMalnutrition', label: 'Hunger, Poverty & Healthcare' },
  { key: 'PromotingEducationAndSkillDevelopment', label: 'Education & Livelihood Enhancement' },
  { key: 'GenderEqualityAndWomenEmpowerment', label: 'Gender Equality & Women Empowerment' },
  { key: 'EnvironmentalSustainabilityAndAnimalWelfare', label: 'Environmental Sustainability & Ecological Balance' },
  { key: 'ProtectionOfNationalHeritageArtAndCulture', label: 'National Heritage, Art & Culture' },
  { key: 'MeasuresForArmedForcesVeteransAndWarWidows', label: 'Armed Forces Veterans & War Widows' },
  { key: 'RuralAndNationallyRecognisedSports', label: 'Rural & Paralympic Sports Training' },
  { key: 'PrimeMinisterNationalReliefFundOrPmCares', label: 'PM Cares / PM National Relief Fund' },
  { key: 'TechnologyIncubatorsAndResearchInstitutions', label: 'Technology Incubators & Autonomous R&D' },
  { key: 'RuralDevelopmentProjects', label: 'Rural Development Infrastructure' },
  { key: 'SlumAreaDevelopment', label: 'Slum Area Development & Sanitation' },
  { key: 'DisasterManagementAndRelief', label: 'Disaster Management, Relief & Rehabilitation' }
]

export function EsgCsrDeskPage({ can: _can }: { can?: (p: string) => boolean }) {
  const [activeTab, setActiveTab] = useState<'csr-assessments' | 'projects' | 'caro-clause-xx' | 'brsr-core'>('csr-assessments')
  const [loading, setLoading] = useState(false)
  const [notice, setNotice] = useState<string | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  // Context Data
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [assessments, setAssessments] = useState<CsrAssessmentDto[]>([])
  const [selectedAssessmentId, setSelectedAssessmentId] = useState<string>('')
  const [currentAssessment, setCurrentAssessment] = useState<CsrAssessmentDetailDto | null>(null)

  const [brsrReports, setBrsrReports] = useState<BrsrCoreReportDto[]>([])
  const [currentBrsrReport, setCurrentBrsrReport] = useState<BrsrCoreReportDto | null>(null)

  // Modals & Panels
  const [showAssessmentModal, setShowAssessmentModal] = useState(false)
  const [showProjectModal, setShowProjectModal] = useState(false)
  const [showBrsrModal, setShowBrsrModal] = useState(false)
  const [showCertifyCsrModal, setShowCertifyCsrModal] = useState(false)
  const [showCertifyBrsrModal, setShowCertifyBrsrModal] = useState(false)
  const [showDossierModal, setShowDossierModal] = useState(false)
  const [dossierHtml, setDossierHtml] = useState<string>('')

  // Interactive Calculator State
  const [calcNetWorth, setCalcNetWorth] = useState<number>(600)
  const [calcTurnover, setCalcTurnover] = useState<number>(1200)
  const [calcNetProfitPrevYear, setCalcNetProfitPrevYear] = useState<number>(25)
  const [calcNp1, setCalcNp1] = useState<number>(30)
  const [calcNp2, setCalcNp2] = useState<number>(25)
  const [calcNp3, setCalcNp3] = useState<number>(20)
  const [calcPriorSetOff, setCalcPriorSetOff] = useState<number>(0)
  const [calcSpentOngoing, setCalcSpentOngoing] = useState<number>(0.20)
  const [calcSpentOther, setCalcSpentOther] = useState<number>(0.10)
  const [calcOngoingTransferDate, setCalcOngoingTransferDate] = useState<string>('')
  const [calcScheduleViiTransferDate, setCalcScheduleViiTransferDate] = useState<string>('')
  const [calcResult, setCalcResult] = useState<CsrObligationCalculationDto | null>(null)

  // Assessment Form State
  const [formAssessmentClient, setFormAssessmentClient] = useState<string>('')
  const [formFy, setFormFy] = useState<string>('2024-25')
  const [formNetWorth, setFormNetWorth] = useState<number>(550)
  const [formTurnover, setFormTurnover] = useState<number>(1100)
  const [formNpPrev, setFormNpPrev] = useState<number>(18)
  const [formNp1, setFormNp1] = useState<number>(24)
  const [formNp2, setFormNp2] = useState<number>(20)
  const [formNp3, setFormNp3] = useState<number>(16)
  const [formSetOff, setFormSetOff] = useState<number>(0)
  const [formAdminSpent, setFormAdminSpent] = useState<number>(0)
  const [formImpactSpent, setFormImpactSpent] = useState<number>(0)
  const [formOngoingTransferDate, setFormOngoingTransferDate] = useState<string>('')
  const [formScheduleViiTransferDate, setFormScheduleViiTransferDate] = useState<string>('')

  // Project Form State
  const [projectTitle, setProjectTitle] = useState('')
  const [projectCat, setProjectCat] = useState<ScheduleViiCategory>('PromotingEducationAndSkillDevelopment')
  const [projectNature, setProjectNature] = useState<CsrProjectNature>('OngoingProject')
  const [projectStateDistrict, setProjectStateDistrict] = useState('Pune, Maharashtra')
  const [projectAspirational, setProjectAspirational] = useState(false)
  const [projectOutlay, setProjectOutlay] = useState<number>(1.5)
  const [projectAllocated, setProjectAllocated] = useState<number>(0.5)
  const [projectSpent, setProjectSpent] = useState<number>(0.4)
  const [projectMode, setProjectMode] = useState('Direct')
  const [projectAgencyName, setProjectAgencyName] = useState('')
  const [projectAgencyCsr1, setProjectAgencyCsr1] = useState('')
  const [projectStatus, setProjectStatus] = useState('OngoingYear1')

  // BRSR Form State
  const [brsrClient, setBrsrClient] = useState('')
  const [brsrFy, setBrsrFy] = useState('2024-25')
  const [brsrTurnoverCr, setBrsrTurnoverCr] = useState<number>(250)
  const [brsrDieselLitres, setBrsrDieselLitres] = useState<number>(60000)
  const [brsrGasSm3, setBrsrGasSm3] = useState<number>(25000)
  const [brsrGridMwh, setBrsrGridMwh] = useState<number>(1500)
  const [brsrWaterWithdrawalKl, setBrsrWaterWithdrawalKl] = useState<number>(30000)
  const [brsrWaterConsumptionKl, setBrsrWaterConsumptionKl] = useState<number>(25000)
  const [brsrZld, setBrsrZld] = useState(true)
  const [brsrEnergyGj, setBrsrEnergyGj] = useState<number>(18000)
  const [brsrRenewablePct, setBrsrRenewablePct] = useState<number>(35)
  const [brsrLtifr, setBrsrLtifr] = useState<number>(0.15)
  const [brsrFatalities, setBrsrFatalities] = useState<number>(0)
  const [brsrPoshReceived, setBrsrPoshReceived] = useState<number>(1)
  const [brsrPoshResolved, setBrsrPoshResolved] = useState<number>(1)
  const [brsrMedianRemuneration, setBrsrMedianRemuneration] = useState<number>(8.5)
  const [brsrTotalEmp, setBrsrTotalEmp] = useState<number>(600)
  const [brsrFemaleEmp, setBrsrFemaleEmp] = useState<number>(180)
  const [brsrTotalDir, setBrsrTotalDir] = useState<number>(10)
  const [brsrFemaleDir, setBrsrFemaleDir] = useState<number>(2)
  const [brsrTotalKmp, setBrsrTotalKmp] = useState<number>(6)
  const [brsrFemaleKmp, setBrsrFemaleKmp] = useState<number>(2)
  const [brsrAssuranceType, setBrsrAssuranceType] = useState('ReasonableAssurance')

  // Certify Form State
  const [certAuditorName, setCertAuditorName] = useState('CA Rameshwar Sharma, FCA')
  const [certMembershipNo, setCertMembershipNo] = useState('094832')
  const [certUdin, setCertUdin] = useState('25094832AAAAES2025')

  // Initial load
  useEffect(() => {
    loadClients()
    loadAssessments()
    loadBrsrReports()
  }, [])

  useEffect(() => {
    if (selectedAssessmentId) {
      loadAssessmentDetail(selectedAssessmentId)
    }
  }, [selectedAssessmentId])

  async function loadClients() {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      setClients(res.items || [])
      if (res.items && res.items.length > 0 && !formAssessmentClient) {
        setFormAssessmentClient(res.items[0].id)
        setBrsrClient(res.items[0].id)
      }
    } catch (err: any) {
      console.error('Error loading clients:', err)
    }
  }

  async function loadAssessments(clientId?: string) {
    setLoading(true)
    try {
      const res = await esgCsrApi.getAssessments(clientId)
      setAssessments(res)
      if (res.length > 0 && !selectedAssessmentId) {
        setSelectedAssessmentId(res[0].id)
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to load CSR assessments')
    } finally {
      setLoading(false)
    }
  }

  async function loadAssessmentDetail(id: string) {
    setLoading(true)
    try {
      const res = await esgCsrApi.getAssessment(id)
      setCurrentAssessment(res)
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to load assessment details')
    } finally {
      setLoading(false)
    }
  }

  async function loadBrsrReports(clientId?: string) {
    try {
      const res = await esgCsrApi.getBrsrReports(clientId)
      setBrsrReports(res)
      if (res.length > 0 && !currentBrsrReport) {
        setCurrentBrsrReport(res[0])
      }
    } catch (err: any) {
      console.error('Failed to load BRSR reports:', err)
    }
  }

  async function handleRunInteractiveCalc(e?: FormEvent) {
    if (e) e.preventDefault()
    try {
      const req: CalculateCsrObligationRequest = {
        netWorth: calcNetWorth,
        turnover: calcTurnover,
        netProfitPreviousYear: calcNetProfitPrevYear,
        netProfitFyMinus1: calcNp1,
        netProfitFyMinus2: calcNp2,
        netProfitFyMinus3: calcNp3,
        setOffAvailableFromPriorYears: calcPriorSetOff,
        totalSpentOngoing: calcSpentOngoing,
        totalSpentOther: calcSpentOther,
        ongoingTransferDate: calcOngoingTransferDate || undefined,
        scheduleViiTransferDate: calcScheduleViiTransferDate || undefined
      }
      const res = await esgCsrApi.calculateObligation(req)
      setCalcResult(res)
    } catch (err: any) {
      setErrorMsg(err.message || 'Calculation error')
    }
  }

  async function handleSaveAssessment(e: FormEvent) {
    e.preventDefault()
    if (!formAssessmentClient) {
      setErrorMsg('Please select a client')
      return
    }
    setLoading(true)
    try {
      const req: SaveCsrAssessmentRequest = {
        clientId: formAssessmentClient,
        financialYear: formFy,
        netWorth: formNetWorth,
        turnover: formTurnover,
        netProfitPreviousYear: formNpPrev,
        netProfitFyMinus1: formNp1,
        netProfitFyMinus2: formNp2,
        netProfitFyMinus3: formNp3,
        setOffAvailableFromPriorYears: formSetOff,
        administrativeOverheadsSpent: formAdminSpent,
        impactAssessmentSpent: formImpactSpent,
        unspentOngoingTransferDate: formOngoingTransferDate || undefined,
        scheduleViiFundTransferDate: formScheduleViiTransferDate || undefined
      }
      const res = await esgCsrApi.saveAssessment(req)
      setNotice(`CSR Assessment saved for FY ${res.financialYear}`)
      setShowAssessmentModal(false)
      loadAssessments()
      setSelectedAssessmentId(res.id)
      setCurrentAssessment(res)
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to save CSR assessment')
    } finally {
      setLoading(false)
    }
  }

  async function handleSaveProject(e: FormEvent) {
    e.preventDefault()
    if (!selectedAssessmentId) {
      setErrorMsg('Select a CSR assessment first')
      return
    }
    setLoading(true)
    try {
      const req: SaveCsrProjectRequest = {
        csrAssessmentId: selectedAssessmentId,
        projectTitle,
        scheduleViiCategory: projectCat,
        nature: projectNature,
        stateOrDistrict: projectStateDistrict,
        isAspirationalDistrict: projectAspirational,
        totalProjectOutlay: projectOutlay,
        allocatedBudget: projectAllocated,
        actualSpent: projectSpent,
        implementationMode: projectMode,
        implementingAgencyName: projectAgencyName || undefined,
        implementingAgencyCsr1Number: projectAgencyCsr1 || undefined,
        projectStatus
      }
      await esgCsrApi.saveProject(req)
      setNotice(`CSR Project '${projectTitle}' added to Schedule VII portfolio`)
      setShowProjectModal(false)
      loadAssessmentDetail(selectedAssessmentId)
      loadAssessments()
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to save project')
    } finally {
      setLoading(false)
    }
  }

  async function handleDeleteProject(id: string) {
    if (!confirm('Are you sure you want to remove this project?')) return
    setLoading(true)
    try {
      await esgCsrApi.deleteProject(id)
      setNotice('Project removed')
      if (selectedAssessmentId) {
        loadAssessmentDetail(selectedAssessmentId)
        loadAssessments()
      }
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to remove project')
    } finally {
      setLoading(false)
    }
  }

  async function handleSaveBrsrReport(e: FormEvent) {
    e.preventDefault()
    if (!brsrClient) {
      setErrorMsg('Select a client for BRSR report')
      return
    }
    setLoading(true)
    try {
      const req: SaveBrsrReportRequest = {
        clientId: brsrClient,
        financialYear: brsrFy,
        turnoverInCrores: brsrTurnoverCr,
        dieselConsumedLitres: brsrDieselLitres,
        naturalGasConsumedSm3: brsrGasSm3,
        gridElectricityPurchasedMwh: brsrGridMwh,
        totalWaterWithdrawalKl: brsrWaterWithdrawalKl,
        totalWaterConsumptionKl: brsrWaterConsumptionKl,
        operatesZeroLiquidDischarge: brsrZld,
        totalEnergyConsumptionGj: brsrEnergyGj,
        renewableEnergySharePct: brsrRenewablePct,
        lostTimeInjuryFrequencyRateLtifr: brsrLtifr,
        fatalitiesCount: brsrFatalities,
        poshComplaintsReceived: brsrPoshReceived,
        poshComplaintsResolved: brsrPoshResolved,
        medianRemunerationLakhs: brsrMedianRemuneration,
        totalPermanentEmployees: brsrTotalEmp,
        femaleEmployeesCount: brsrFemaleEmp,
        totalBoardDirectors: brsrTotalDir,
        femaleBoardDirectors: brsrFemaleDir,
        totalKmpCount: brsrTotalKmp,
        femaleKmpCount: brsrFemaleKmp,
        assuranceType: brsrAssuranceType
      }
      const res = await esgCsrApi.saveBrsrReport(req)
      setNotice(`SEBI BRSR Core report saved with ESG score: ${res.overallBrsrCoreScore.toFixed(1)}/100`)
      setShowBrsrModal(false)
      loadBrsrReports()
      setCurrentBrsrReport(res)
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to save BRSR report')
    } finally {
      setLoading(false)
    }
  }

  async function handleCertifyCsr(e: FormEvent) {
    e.preventDefault()
    if (!selectedAssessmentId) return
    setLoading(true)
    try {
      const req: CertifyCsrRequest = {
        auditorName: certAuditorName,
        membershipNo: certMembershipNo,
        udin: certUdin
      }
      const res = await esgCsrApi.certifyCsr(selectedAssessmentId, req)
      setNotice(`CSR Assessment certified with UDIN ${res.udin}`)
      setShowCertifyCsrModal(false)
      setCurrentAssessment(res)
      loadAssessments()
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to certify CSR assessment')
    } finally {
      setLoading(false)
    }
  }

  async function handleCertifyBrsr(e: FormEvent) {
    e.preventDefault()
    if (!currentBrsrReport) return
    setLoading(true)
    try {
      const req: CertifyBrsrRequest = {
        auditorName: certAuditorName,
        membershipNo: certMembershipNo,
        udin: certUdin,
        assuranceType: brsrAssuranceType
      }
      const res = await esgCsrApi.certifyBrsr(currentBrsrReport.id, req)
      setNotice(`SEBI BRSR Core Assurance Statement signed with UDIN ${res.assuranceUdin}`)
      setShowCertifyBrsrModal(false)
      setCurrentBrsrReport(res)
      loadBrsrReports()
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to certify BRSR report')
    } finally {
      setLoading(false)
    }
  }

  async function handleViewDossier(brsrId: string) {
    setLoading(true)
    try {
      const res = await fetch(esgCsrApi.getDossierHtmlUrl(brsrId))
      const html = await res.text()
      setDossierHtml(html)
      setShowDossierModal(true)
    } catch (err: any) {
      setErrorMsg(err.message || 'Failed to load assurance dossier')
    } finally {
      setLoading(false)
    }
  }

  // Summary Metrics
  const summaryMetrics = useMemo(() => {
    const totalObligation = assessments.reduce((acc, a) => acc + a.mandatoryCsrObligation2Pct, 0)
    const totalSpent = assessments.reduce((acc, a) => acc + a.totalActualCsrSpent, 0)
    const totalUnspent = assessments.reduce((acc, a) => acc + a.unspentOngoingProjects + a.unspentOtherProjects, 0)
    const caroQualifications = assessments.filter(a => a.caroQualificationRequired).length
    return { totalObligation, totalSpent, totalUnspent, caroQualifications }
  }, [assessments])

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header Banner */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between bg-gradient-to-r from-emerald-900 via-teal-900 to-slate-900 text-white p-6 rounded-2xl shadow-xl border border-emerald-700/40">
        <div>
          <div className="flex items-center space-x-3">
            <span className="text-3xl">🌱</span>
            <h1 className="text-2xl font-bold tracking-tight">Enterprise ESG & Section 135 CSR Statutory Audit Moat</h1>
            <span className="bg-emerald-500/20 text-emerald-300 text-xs px-2.5 py-1 rounded-full border border-emerald-400/30 font-semibold uppercase tracking-wider">
              SEBI BRSR Core • CARO 2020 Clause XX
            </span>
          </div>
          <p className="text-emerald-100/80 text-sm mt-2 max-w-3xl leading-relaxed">
            Statutory CSR assessment under Section 135 & Section 198 (2% obligation, 3-year escrow tracking u/s 135(6)), automated CARO 2020 Clause XX disclosures, and SEBI BRSR Core 9-principle GHG Scope 1/2 emission assurance.
          </p>
        </div>
        <div className="mt-4 md:mt-0 flex flex-wrap items-center gap-2">
          <select
            value={selectedClientId}
            onChange={e => {
              setSelectedClientId(e.target.value)
              loadAssessments(e.target.value || undefined)
              loadBrsrReports(e.target.value || undefined)
            }}
            className="px-3 py-2 bg-emerald-950/80 border border-emerald-500/40 rounded-lg text-xs text-white"
          >
            <option value="">All Corporate Clients</option>
            {clients.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
          <button
            onClick={() => setShowAssessmentModal(true)}
            className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-lg font-medium shadow-md transition flex items-center gap-2 text-sm"
          >
            <span>+</span> New CSR Assessment
          </button>
          <button
            onClick={() => setShowBrsrModal(true)}
            className="px-4 py-2 bg-teal-700 hover:bg-teal-600 text-white rounded-lg font-medium shadow-md transition flex items-center gap-2 text-sm"
          >
            <span>🌍</span> New BRSR Report
          </button>
        </div>
      </div>

      {/* Notifications */}
      {notice && (
        <div className="bg-emerald-50 border border-emerald-200 text-emerald-800 px-4 py-3 rounded-lg flex justify-between items-center text-sm shadow-sm">
          <span>{notice}</span>
          <button onClick={() => setNotice(null)} className="text-emerald-600 font-bold hover:text-emerald-800">×</button>
        </div>
      )}
      {errorMsg && (
        <div className="bg-rose-50 border border-rose-200 text-rose-800 px-4 py-3 rounded-lg flex justify-between items-center text-sm shadow-sm">
          <span>{errorMsg}</span>
          <button onClick={() => setErrorMsg(null)} className="text-rose-600 font-bold hover:text-rose-800">×</button>
        </div>
      )}

      {/* Practice KPI Telemetry Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-between">
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Total CSR Obligation (2%)</div>
          <div className="text-2xl font-bold text-slate-900 mt-2">₹{summaryMetrics.totalObligation.toFixed(2)} Cr</div>
          <div className="text-xs text-emerald-600 mt-1 font-medium">Preceding 3-yr Sec 198 average profit</div>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-between">
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Total CSR Deployed</div>
          <div className="text-2xl font-bold text-emerald-700 mt-2">₹{summaryMetrics.totalSpent.toFixed(2)} Cr</div>
          <div className="text-xs text-slate-500 mt-1">Schedule VII projects & overheads</div>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-between">
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Unspent Escrow / Fund Balances</div>
          <div className="text-2xl font-bold text-amber-600 mt-2">₹{summaryMetrics.totalUnspent.toFixed(2)} Cr</div>
          <div className="text-xs text-amber-700 mt-1 font-medium">Sec 135(5) 6-mo & Sec 135(6) 30-day rules</div>
        </div>
        <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-between">
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">CARO 2020 Clause XX Audit Flags</div>
          <div className="text-2xl font-bold text-rose-600 mt-2">{summaryMetrics.caroQualifications} Adverse Flags</div>
          <div className="text-xs text-rose-700 mt-1 font-medium">Requires qualification in Auditor's Report</div>
        </div>
      </div>

      {/* Navigation Tabs */}
      <div className="border-b border-slate-200 flex space-x-6">
        <button
          onClick={() => setActiveTab('csr-assessments')}
          className={`pb-3 font-medium text-sm border-b-2 transition flex items-center gap-2 ${
            activeTab === 'csr-assessments'
              ? 'border-emerald-600 text-emerald-600 font-semibold'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
        >
          <span>🌱</span> Section 135 CSR & 2% Calculator
        </button>
        <button
          onClick={() => setActiveTab('projects')}
          className={`pb-3 font-medium text-sm border-b-2 transition flex items-center gap-2 ${
            activeTab === 'projects'
              ? 'border-emerald-600 text-emerald-600 font-semibold'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
        >
          <span>📑</span> Schedule VII Project Portfolio ({currentAssessment?.projects.length || 0})
        </button>
        <button
          onClick={() => setActiveTab('caro-clause-xx')}
          className={`pb-3 font-medium text-sm border-b-2 transition flex items-center gap-2 ${
            activeTab === 'caro-clause-xx'
              ? 'border-emerald-600 text-emerald-600 font-semibold'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
        >
          <span>🔍</span> CARO 2020 Clause XX Audit Disclosures
        </button>
        <button
          onClick={() => setActiveTab('brsr-core')}
          className={`pb-3 font-medium text-sm border-b-2 transition flex items-center gap-2 ${
            activeTab === 'brsr-core'
              ? 'border-emerald-600 text-emerald-600 font-semibold'
              : 'border-transparent text-slate-500 hover:text-slate-800'
          }`}
        >
          <span>🌍</span> SEBI BRSR Core & GHG Assurance
        </button>
      </div>

      {/* TAB 1: SECTION 135 CSR ASSESSMENTS & CALCULATOR */}
      {activeTab === 'csr-assessments' && (
        <div className="space-y-6">
          {/* Interactive Gating & Obligation Calculator */}
          <div className="bg-slate-50 p-5 rounded-xl border border-slate-200">
            <div className="flex items-center justify-between mb-4">
              <div>
                <h3 className="font-semibold text-slate-800 text-base">Section 135(1) & Section 198 Fast Compliance Simulator</h3>
                <p className="text-xs text-slate-500">Test applicability criteria (₹500 Cr NW / ₹1,000 Cr Turnover / ₹5 Cr NP) and simulate CARO Clause XX consequences.</p>
              </div>
              <button
                type="button"
                onClick={() => handleRunInteractiveCalc()}
                className="px-3 py-1.5 bg-emerald-700 hover:bg-emerald-600 text-white rounded text-xs font-medium"
              >
                Run Simulation
              </button>
            </div>
            <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-3 text-xs">
              <div>
                <label className="text-slate-600 font-medium">Net Worth (₹ Cr)</label>
                <input
                  type="number"
                  value={calcNetWorth}
                  onChange={e => setCalcNetWorth(Number(e.target.value))}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
              <div>
                <label className="text-slate-600 font-medium">Turnover (₹ Cr)</label>
                <input
                  type="number"
                  value={calcTurnover}
                  onChange={e => setCalcTurnover(Number(e.target.value))}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
              <div>
                <label className="text-slate-600 font-medium">NP Preceding FY (₹ Cr)</label>
                <input
                  type="number"
                  value={calcNetProfitPrevYear}
                  onChange={e => setCalcNetProfitPrevYear(Number(e.target.value))}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
              <div>
                <label className="text-slate-600 font-medium">FY-1 NP (₹ Cr)</label>
                <input
                  type="number"
                  value={calcNp1}
                  onChange={e => setCalcNp1(Number(e.target.value))}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
              <div>
                <label className="text-slate-600 font-medium">FY-2 NP (₹ Cr)</label>
                <input
                  type="number"
                  value={calcNp2}
                  onChange={e => setCalcNp2(Number(e.target.value))}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
              <div>
                <label className="text-slate-600 font-medium">FY-3 NP (₹ Cr)</label>
                <input
                  type="number"
                  value={calcNp3}
                  onChange={e => setCalcNp3(Number(e.target.value))}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-5 gap-3 text-xs mt-3 pt-3 border-t border-slate-200">
              <div>
                <label className="text-slate-600 font-medium">Prior Set-Off (₹ Cr)</label>
                <input
                  type="number"
                  step="any"
                  value={calcPriorSetOff}
                  onChange={e => setCalcPriorSetOff(Number(e.target.value))}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
              <div>
                <label className="text-slate-600 font-medium">Ongoing Spent (₹ Cr)</label>
                <input
                  type="number"
                  step="any"
                  value={calcSpentOngoing}
                  onChange={e => setCalcSpentOngoing(Number(e.target.value))}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
              <div>
                <label className="text-slate-600 font-medium">Other Spent (₹ Cr)</label>
                <input
                  type="number"
                  step="any"
                  value={calcSpentOther}
                  onChange={e => setCalcSpentOther(Number(e.target.value))}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
              <div>
                <label className="text-slate-600 font-medium">Ongoing Transfer Date</label>
                <input
                  type="date"
                  value={calcOngoingTransferDate}
                  onChange={e => setCalcOngoingTransferDate(e.target.value)}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
              <div>
                <label className="text-slate-600 font-medium">Schedule VII Transfer Date</label>
                <input
                  type="date"
                  value={calcScheduleViiTransferDate}
                  onChange={e => setCalcScheduleViiTransferDate(e.target.value)}
                  className="w-full mt-1 p-1.5 border rounded bg-white text-slate-800"
                />
              </div>
            </div>

            {calcResult && (
              <div className="mt-4 p-4 bg-white rounded-lg border border-emerald-300 text-xs space-y-2">
                <div className="flex flex-wrap items-center gap-3">
                  <span className={`px-2 py-0.5 rounded font-bold ${calcResult.isApplicable ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-700'}`}>
                    {calcResult.isApplicable ? 'CSR MANDATORILY APPLICABLE' : 'EXEMPT FROM CSR'}
                  </span>
                  <span className="text-slate-600"><b>Basis:</b> {calcResult.applicabilityReason}</span>
                  <span className="text-slate-700">| <b>Avg Sec 198 NP:</b> ₹{calcResult.averageNetProfit.toFixed(2)} Cr</span>
                  <span className="text-emerald-700">| <b>Statutory 2% Obligation:</b> ₹{calcResult.mandatoryCsrObligation2Pct.toFixed(2)} Cr</span>
                </div>
                {calcResult.caroQualificationRequired ? (
                  <div className="bg-rose-50 text-rose-800 p-2 rounded border border-rose-200">
                    ⚠️ <b>CARO 2020 Qualification Triggered:</b> {calcResult.caroClauseXxARemark} {calcResult.caroClauseXxBRemark}
                  </div>
                ) : (
                  <div className="bg-emerald-50 text-emerald-800 p-2 rounded border border-emerald-200">
                    ✅ <b>CARO 2020 Clean Disclosures:</b> All obligations fulfilled or duly transferred within 30-day / 6-month statutory windows.
                  </div>
                )}
              </div>
            )}
          </div>

          {/* CSR Assessments Registry Table */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="p-4 border-b border-slate-100 flex justify-between items-center">
              <h3 className="font-bold text-slate-800 text-sm">Corporate CSR Engagements Registry</h3>
              <div className="text-xs text-slate-500">Showing {assessments.length} assessment records</div>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-50 text-slate-600 font-semibold border-b border-slate-200">
                  <tr>
                    <th className="p-3">Client & FY</th>
                    <th className="p-3">Sec 135 Gating</th>
                    <th className="p-3">3-Yr Avg Net Profit</th>
                    <th className="p-3">2% CSR Obligation</th>
                    <th className="p-3">Actual Spent</th>
                    <th className="p-3">Unspent Balances</th>
                    <th className="p-3">CARO Status</th>
                    <th className="p-3">Audit Sign-off</th>
                    <th className="p-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 text-slate-700">
                  {assessments.map(a => (
                    <tr
                      key={a.id}
                      onClick={() => setSelectedAssessmentId(a.id)}
                      className={`cursor-pointer hover:bg-slate-50 transition ${selectedAssessmentId === a.id ? 'bg-emerald-50/60' : ''}`}
                    >
                      <td className="p-3">
                        <div className="font-bold text-slate-900">{a.clientName}</div>
                        <div className="text-slate-400 text-[10px]">FY {a.financialYear} • {a.clientPan}</div>
                      </td>
                      <td className="p-3">
                        {a.isCsrApplicable ? (
                          <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-emerald-100 text-emerald-800">
                            Applicable
                          </span>
                        ) : (
                          <span className="px-2 py-0.5 rounded text-[10px] font-semibold bg-slate-100 text-slate-600">
                            Exempt
                          </span>
                        )}
                        <div className="text-[10px] text-slate-500 mt-0.5 truncate max-w-[120px]" title={a.applicabilityCriterion}>
                          {a.applicabilityCriterion}
                        </div>
                      </td>
                      <td className="p-3 font-medium">₹{a.averageNetProfit.toFixed(2)} Cr</td>
                      <td className="p-3 font-bold text-emerald-700">₹{a.mandatoryCsrObligation2Pct.toFixed(2)} Cr</td>
                      <td className="p-3">
                        <div className="font-semibold text-slate-900">₹{a.totalActualCsrSpent.toFixed(2)} Cr</div>
                        <div className="text-[10px] text-slate-500">{a.projectsCount} projects</div>
                      </td>
                      <td className="p-3">
                        {a.unspentOngoingProjects > 0 || a.unspentOtherProjects > 0 ? (
                          <div className="text-amber-700 font-medium">
                            ₹{(a.unspentOngoingProjects + a.unspentOtherProjects).toFixed(2)} Cr
                          </div>
                        ) : (
                          <span className="text-slate-400">Nil</span>
                        )}
                      </td>
                      <td className="p-3">
                        {a.caroQualificationRequired ? (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-rose-100 text-rose-800">
                            Qualified
                          </span>
                        ) : (
                          <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-100 text-emerald-800">
                            Clean
                          </span>
                        )}
                      </td>
                      <td className="p-3">
                        {a.status === 'Certified' ? (
                          <div>
                            <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-blue-100 text-blue-800">
                              UDIN Attested
                            </span>
                            <div className="text-[10px] text-slate-500 mt-0.5">{a.udin}</div>
                          </div>
                        ) : (
                          <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-amber-100 text-amber-800">
                            Draft / Pending
                          </span>
                        )}
                      </td>
                      <td className="p-3 text-right space-x-2">
                        {a.status !== 'Certified' && (
                          <button
                            onClick={e => {
                              e.stopPropagation()
                              setSelectedAssessmentId(a.id)
                              setShowCertifyCsrModal(true)
                            }}
                            className="px-2 py-1 bg-blue-600 hover:bg-blue-500 text-white rounded text-[10px] font-medium"
                          >
                            Sign-off UDIN
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                  {assessments.length === 0 && (
                    <tr>
                      <td colSpan={9} className="p-8 text-center text-slate-400">
                        No CSR assessments recorded. Click "+ New CSR Assessment" to initialize.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: SCHEDULE VII PROJECT PORTFOLIO */}
      {activeTab === 'projects' && (
        <div className="space-y-4">
          <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center bg-slate-50 p-4 rounded-xl border border-slate-200">
            <div>
              <div className="text-xs font-semibold text-slate-500 uppercase">Active CSR Assessment</div>
              <div className="text-base font-bold text-slate-900 mt-0.5">
                {currentAssessment ? `${currentAssessment.clientName} (FY ${currentAssessment.financialYear})` : 'No assessment selected'}
              </div>
              <div className="text-xs text-slate-600 mt-1">
                Net 2% CSR Obligation: <b>₹{currentAssessment?.netCsrObligationForCurrentFy.toFixed(2) || '0.00'} Cr</b> • Total Spent: <b>₹{currentAssessment?.totalActualCsrSpent.toFixed(2) || '0.00'} Cr</b>
              </div>
            </div>
            <button
              onClick={() => setShowProjectModal(true)}
              disabled={!selectedAssessmentId}
              className="mt-3 sm:mt-0 px-3.5 py-2 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white rounded-lg text-xs font-medium shadow-sm transition"
            >
              + Add Schedule VII Project
            </button>
          </div>

          <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 text-slate-600 font-semibold border-b border-slate-200">
                <tr>
                  <th className="p-3">Project Title & Location</th>
                  <th className="p-3">Schedule VII Category</th>
                  <th className="p-3">Nature</th>
                  <th className="p-3">Total Outlay</th>
                  <th className="p-3">Allocated Budget</th>
                  <th className="p-3">Actual Spent</th>
                  <th className="p-3">Unspent Escrow</th>
                  <th className="p-3">Implementation & CSR-1</th>
                  <th className="p-3 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-slate-700">
                {currentAssessment?.projects.map(p => (
                  <tr key={p.id} className="hover:bg-slate-50">
                    <td className="p-3">
                      <div className="font-bold text-slate-900">{p.projectTitle}</div>
                      <div className="text-slate-500 text-[10px]">
                        {p.stateOrDistrict} {p.isAspirationalDistrict && <span className="text-amber-600 font-semibold">[Aspirational District]</span>}
                      </div>
                    </td>
                    <td className="p-3">
                      <span className="px-2 py-0.5 rounded text-[10px] font-medium bg-slate-100 text-slate-700">
                        {p.scheduleViiCategory}
                      </span>
                    </td>
                    <td className="p-3">
                      <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                        p.nature === 'OngoingProject' ? 'bg-indigo-100 text-indigo-800' : 'bg-slate-100 text-slate-700'
                      }`}>
                        {p.nature === 'OngoingProject' ? 'Ongoing (3-Yr Escrow)' : 'Single Year'}
                      </span>
                    </td>
                    <td className="p-3 font-medium">₹{p.totalProjectOutlay.toFixed(2)} Cr</td>
                    <td className="p-3 font-medium text-slate-900">₹{p.allocatedBudget.toFixed(2)} Cr</td>
                    <td className="p-3 font-bold text-emerald-700">₹{p.actualSpent.toFixed(2)} Cr</td>
                    <td className="p-3 font-medium text-amber-700">₹{p.unspentAmount.toFixed(2)} Cr</td>
                    <td className="p-3">
                      <div className="text-slate-800 font-medium">{p.implementationMode}</div>
                      {p.implementingAgencyName && (
                        <div className="text-slate-500 text-[10px]">{p.implementingAgencyName} ({p.implementingAgencyCsr1Number})</div>
                      )}
                    </td>
                    <td className="p-3 text-right">
                      <button
                        onClick={() => handleDeleteProject(p.id)}
                        className="text-rose-600 hover:text-rose-800 font-medium text-xs"
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                ))}
                {(!currentAssessment || currentAssessment.projects.length === 0) && (
                  <tr>
                    <td colSpan={9} className="p-8 text-center text-slate-400">
                      No Schedule VII projects mapped to this CSR assessment. Click "+ Add Schedule VII Project".
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 3: CARO 2020 CLAUSE XX STATUTORY AUDIT REPORTING */}
      {activeTab === 'caro-clause-xx' && (
        <div className="space-y-6">
          <div className="bg-slate-50 p-4 rounded-xl border border-slate-200">
            <h3 className="font-bold text-slate-800 text-sm">CARO 2020 Clause XX Statutory Auditor Remark Generator</h3>
            <p className="text-xs text-slate-600 mt-1">
              Under the Companies (Auditor's Report) Order, 2020, statutory auditors must report on compliance with Section 135(5) (transfer of unspent non-ongoing funds within 6 months to a Schedule VII fund) and Section 135(6) (transfer of unspent ongoing funds within 30 days to a special Unspent CSR Account).
            </p>
          </div>

          {currentAssessment ? (
            <div className="space-y-4">
              <div className={`p-4 rounded-xl border ${currentAssessment.caroQualificationRequired ? 'bg-rose-50 border-rose-200' : 'bg-emerald-50 border-emerald-200'}`}>
                <div className="flex items-center gap-2 font-bold text-sm">
                  <span>{currentAssessment.caroQualificationRequired ? '🚨' : '✅'}</span>
                  <span className={currentAssessment.caroQualificationRequired ? 'text-rose-900' : 'text-emerald-900'}>
                    Audit Opinion Status: {currentAssessment.caroQualificationRequired ? 'QUALIFIED OPINION REQUIRED' : 'UNQUALIFIED / CLEAN OPINION'}
                  </span>
                </div>
                <div className="text-xs text-slate-600 mt-1">
                  Entity: <b>{currentAssessment.clientName}</b> • FY: <b>{currentAssessment.financialYear}</b>
                </div>
              </div>

              {/* Clause XX(a) */}
              <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm space-y-2">
                <div className="flex justify-between items-center">
                  <h4 className="font-bold text-slate-900 text-sm">Clause XX(a): Other than ongoing projects (Section 135(5))</h4>
                  <button
                    onClick={() => navigator.clipboard.writeText(currentAssessment.caroClauseXxARemark)}
                    className="text-xs text-emerald-700 hover:text-emerald-900 font-semibold"
                  >
                    Copy Text
                  </button>
                </div>
                <div className="text-xs text-slate-500">
                  Transfer deadline: 6 months from expiry of FY (September 30).
                  {currentAssessment.scheduleViiFundTransferDate && ` Actual transfer date: ${currentAssessment.scheduleViiFundTransferDate}`}
                </div>
                <div className="p-3 bg-slate-50 rounded border border-slate-200 text-xs font-mono text-slate-800 leading-relaxed">
                  {currentAssessment.caroClauseXxARemark}
                </div>
              </div>

              {/* Clause XX(b) */}
              <div className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm space-y-2">
                <div className="flex justify-between items-center">
                  <h4 className="font-bold text-slate-900 text-sm">Clause XX(b): Ongoing projects (Section 135(6))</h4>
                  <button
                    onClick={() => navigator.clipboard.writeText(currentAssessment.caroClauseXxBRemark)}
                    className="text-xs text-emerald-700 hover:text-emerald-900 font-semibold"
                  >
                    Copy Text
                  </button>
                </div>
                <div className="text-xs text-slate-500">
                  Transfer deadline: 30 days from expiry of FY (April 30).
                  {currentAssessment.unspentOngoingTransferDate && ` Actual transfer date: ${currentAssessment.unspentOngoingTransferDate}`}
                </div>
                <div className="p-3 bg-slate-50 rounded border border-slate-200 text-xs font-mono text-slate-800 leading-relaxed">
                  {currentAssessment.caroClauseXxBRemark}
                </div>
              </div>
            </div>
          ) : (
            <div className="p-8 bg-white rounded-xl border border-slate-200 text-center text-slate-400 text-xs">
              Select a CSR assessment from Tab 1 to preview its CARO 2020 Clause XX remarks.
            </div>
          )}
        </div>
      )}

      {/* TAB 4: SEBI BRSR CORE & 9-PRINCIPLE METRICS */}
      {activeTab === 'brsr-core' && (
        <div className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {brsrReports.map(b => (
              <div
                key={b.id}
                onClick={() => setCurrentBrsrReport(b)}
                className={`p-4 rounded-xl border cursor-pointer transition shadow-sm ${
                  currentBrsrReport?.id === b.id ? 'bg-teal-50/70 border-teal-500' : 'bg-white border-slate-200 hover:border-slate-300'
                }`}
              >
                <div className="flex justify-between items-start">
                  <div>
                    <div className="font-bold text-slate-900 text-sm">{b.clientName}</div>
                    <div className="text-xs text-slate-500">FY {b.financialYear} • Turnover: ₹{b.turnoverInCrores} Cr</div>
                  </div>
                  <span className="px-2 py-0.5 rounded text-xs font-bold bg-teal-100 text-teal-800">
                    {b.overallBrsrCoreScore.toFixed(1)}/100
                  </span>
                </div>
                <div className="mt-3 pt-3 border-t border-slate-100 grid grid-cols-2 gap-2 text-xs">
                  <div>
                    <div className="text-slate-400 text-[10px]">Total Scope 1+2</div>
                    <div className="font-semibold text-slate-800">{b.totalScope1And2MtCo2e.toFixed(1)} MT CO2e</div>
                  </div>
                  <div>
                    <div className="text-slate-400 text-[10px]">GHG Intensity</div>
                    <div className="font-semibold text-slate-800">{b.ghgIntensityPerCroreTurnover.toFixed(2)} MT/Cr</div>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {currentBrsrReport && (
            <div className="bg-white rounded-xl border border-slate-200 shadow-sm p-6 space-y-6">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 border-b border-slate-200">
                <div>
                  <h3 className="text-lg font-bold text-slate-900">
                    SEBI BRSR Core Telemetry: {currentBrsrReport.clientName} (FY {currentBrsrReport.financialYear})
                  </h3>
                  <p className="text-xs text-slate-500 mt-1">
                    Assurance Type: <b>{currentBrsrReport.assuranceType}</b> • Auditor: {currentBrsrReport.assuranceAuditorName || 'Pending'} {currentBrsrReport.assuranceUdin && `(UDIN: ${currentBrsrReport.assuranceUdin})`}
                  </p>
                </div>
                <div className="mt-3 sm:mt-0 flex gap-2">
                  <button
                    onClick={() => handleViewDossier(currentBrsrReport.id)}
                    className="px-3 py-1.5 bg-slate-800 hover:bg-slate-700 text-white rounded text-xs font-medium shadow-sm flex items-center gap-1.5"
                  >
                    <span>📄</span> View Assurance Dossier
                  </button>
                  <button
                    onClick={() => setShowCertifyBrsrModal(true)}
                    className="px-3 py-1.5 bg-teal-600 hover:bg-teal-500 text-white rounded text-xs font-medium shadow-sm"
                  >
                    Certify & Sign-off
                  </button>
                </div>
              </div>

              {/* 5 ESG Pillars Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 text-xs">
                {/* Pillar 1: GHG Scope 1 & 2 */}
                <div className="bg-slate-50 p-4 rounded-lg border border-slate-200 space-y-2">
                  <div className="font-bold text-slate-900 flex items-center gap-1.5">
                    <span>💨</span> Pillar 1: Greenhouse Gas Footprint
                  </div>
                  <div className="space-y-1 text-slate-600">
                    <div>Scope 1 Direct (Fuel Combustion): <b>{currentBrsrReport.scope1DirectEmissionsMtCo2e.toFixed(2)} MT CO2e</b></div>
                    <div className="text-[10px] text-slate-400">Diesel: {currentBrsrReport.dieselConsumedLitres.toLocaleString()} L • Gas: {currentBrsrReport.naturalGasConsumedSm3.toLocaleString()} Sm3</div>
                    <div>Scope 2 Indirect (Grid Electricity): <b>{currentBrsrReport.scope2IndirectEmissionsMtCo2e.toFixed(2)} MT CO2e</b></div>
                    <div className="text-[10px] text-slate-400">CEA Grid Factor: 0.82 tCO2/MWh ({currentBrsrReport.gridElectricityPurchasedMwh.toLocaleString()} MWh)</div>
                    <div className="pt-1 border-t text-slate-800 font-bold">
                      GHG Intensity: {currentBrsrReport.ghgIntensityPerCroreTurnover.toFixed(2)} MT / ₹ Cr Turnover
                    </div>
                  </div>
                </div>

                {/* Pillar 2: Water Stewardship */}
                <div className="bg-slate-50 p-4 rounded-lg border border-slate-200 space-y-2">
                  <div className="font-bold text-slate-900 flex items-center gap-1.5">
                    <span>💧</span> Pillar 2: Water Stewardship
                  </div>
                  <div className="space-y-1 text-slate-600">
                    <div>Total Water Withdrawal: <b>{currentBrsrReport.totalWaterWithdrawalKl.toLocaleString()} KL</b></div>
                    <div>Total Water Consumption: <b>{currentBrsrReport.totalWaterConsumptionKl.toLocaleString()} KL</b></div>
                    <div>Water Intensity: <b>{currentBrsrReport.waterIntensityPerCroreTurnover.toFixed(2)} KL / ₹ Cr</b></div>
                    <div className="pt-1 border-t flex items-center gap-2">
                      <span className="font-bold text-slate-800">Zero Liquid Discharge (ZLD):</span>
                      <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${currentBrsrReport.operatesZeroLiquidDischarge ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'}`}>
                        {currentBrsrReport.operatesZeroLiquidDischarge ? 'OPERATIONAL' : 'INACTIVE'}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Pillar 3: Energy Circularity */}
                <div className="bg-slate-50 p-4 rounded-lg border border-slate-200 space-y-2">
                  <div className="font-bold text-slate-900 flex items-center gap-1.5">
                    <span>⚡</span> Pillar 3: Energy & Circularity
                  </div>
                  <div className="space-y-1 text-slate-600">
                    <div>Total Energy Consumption: <b>{currentBrsrReport.totalEnergyConsumptionGj.toLocaleString()} GJ</b></div>
                    <div>Energy Intensity: <b>{currentBrsrReport.energyIntensityPerCroreTurnover.toFixed(2)} GJ / ₹ Cr</b></div>
                    <div className="pt-1 border-t text-slate-800 font-bold">
                      Renewable Energy Share: <span className="text-emerald-700">{currentBrsrReport.renewableEnergySharePct.toFixed(1)}%</span>
                    </div>
                  </div>
                </div>

                {/* Pillar 4: Employee Well-being */}
                <div className="bg-slate-50 p-4 rounded-lg border border-slate-200 space-y-2">
                  <div className="font-bold text-slate-900 flex items-center gap-1.5">
                    <span>👷</span> Pillar 4: Employee Health & Safety
                  </div>
                  <div className="space-y-1 text-slate-600">
                    <div>LTIFR (Lost Time Injury Rate): <b>{currentBrsrReport.lostTimeInjuryFrequencyRateLtifr.toFixed(2)}</b> / 1M hrs</div>
                    <div>Workplace Fatalities: <b>{currentBrsrReport.fatalitiesCount}</b></div>
                    <div>POSH Complaints (Sexual Harassment): <b>{currentBrsrReport.poshComplaintsReceived} rec / {currentBrsrReport.poshComplaintsResolved} res</b></div>
                    <div className="pt-1 border-t text-slate-800 font-bold">
                      Median Remuneration: ₹{currentBrsrReport.medianRemunerationLakhs.toFixed(2)} Lakhs
                    </div>
                  </div>
                </div>

                {/* Pillar 5: Gender Diversity */}
                <div className="bg-slate-50 p-4 rounded-lg border border-slate-200 space-y-2">
                  <div className="font-bold text-slate-900 flex items-center gap-1.5">
                    <span>⚖️</span> Pillar 5: Gender Diversity Ratios
                  </div>
                  <div className="space-y-1 text-slate-600">
                    <div>Board Directors: <b>{currentBrsrReport.femaleBoardPct.toFixed(1)}%</b> women ({currentBrsrReport.femaleBoardDirectors}/{currentBrsrReport.totalBoardDirectors})</div>
                    <div>Key Managerial Personnel: <b>{currentBrsrReport.femaleKmpPct.toFixed(1)}%</b> women ({currentBrsrReport.femaleKmpCount}/{currentBrsrReport.totalKmpCount})</div>
                    <div>Permanent Workforce: <b>{currentBrsrReport.femaleEmployeesPct.toFixed(1)}%</b> women ({currentBrsrReport.femaleEmployeesCount}/{currentBrsrReport.totalPermanentEmployees})</div>
                    <div className="pt-1 border-t text-slate-800 font-bold">
                      SEBI LODR Reg 17(1) Compliance: <span className="text-emerald-700">Satisfied</span>
                    </div>
                  </div>
                </div>

                {/* Pillar 6: Composite ESG Score */}
                <div className="bg-gradient-to-br from-teal-50 to-emerald-50 p-4 rounded-lg border border-teal-200 flex flex-col justify-between">
                  <div className="font-bold text-teal-950 flex items-center gap-1.5">
                    <span>🏆</span> Composite BRSR Core Health Index
                  </div>
                  <div className="my-auto py-2 text-center">
                    <div className="text-4xl font-extrabold text-teal-800">
                      {currentBrsrReport.overallBrsrCoreScore.toFixed(1)}
                    </div>
                    <div className="text-xs text-teal-600 font-medium mt-1">out of 100 maximum score</div>
                  </div>
                  <div className="text-[10px] text-teal-700 text-center font-medium">
                    Audited for Reasonable Assurance pursuant to SEBI Circular SEBI/HO/CFD/CFD-SEC-2/P/CIR/2023/122
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      {/* MODAL 1: CREATE CSR ASSESSMENT */}
      {showAssessmentModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">New Statutory CSR Assessment (Section 135)</h3>
              <button onClick={() => setShowAssessmentModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSaveAssessment} className="space-y-4 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Corporate Client</label>
                  <select
                    value={formAssessmentClient}
                    onChange={e => setFormAssessmentClient(e.target.value)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                    required
                  >
                    <option value="">Select corporate entity...</option>
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Financial Year</label>
                  <input
                    type="text"
                    value={formFy}
                    onChange={e => setFormFy(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="2024-25"
                    required
                  />
                </div>
              </div>

              <div className="border-t pt-3">
                <div className="font-semibold text-slate-800 mb-2">Section 135(1) Gating Criteria (Preceding Financial Year)</div>
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="text-slate-600">Net Worth (₹ Cr)</label>
                    <input
                      type="number"
                      step="any"
                      value={formNetWorth}
                      onChange={e => setFormNetWorth(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                      required
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Turnover (₹ Cr)</label>
                    <input
                      type="number"
                      step="any"
                      value={formTurnover}
                      onChange={e => setFormTurnover(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                      required
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Net Profit (₹ Cr)</label>
                    <input
                      type="number"
                      step="any"
                      value={formNpPrev}
                      onChange={e => setFormNpPrev(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                      required
                    />
                  </div>
                </div>
              </div>

              <div className="border-t pt-3">
                <div className="font-semibold text-slate-800 mb-2">Section 198 Net Profits for Preceding 3 FYs (for 2% obligation)</div>
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="text-slate-600">FY-1 NP (₹ Cr)</label>
                    <input
                      type="number"
                      step="any"
                      value={formNp1}
                      onChange={e => setFormNp1(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                      required
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">FY-2 NP (₹ Cr)</label>
                    <input
                      type="number"
                      step="any"
                      value={formNp2}
                      onChange={e => setFormNp2(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                      required
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">FY-3 NP (₹ Cr)</label>
                    <input
                      type="number"
                      step="any"
                      value={formNp3}
                      onChange={e => setFormNp3(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                      required
                    />
                  </div>
                </div>
              </div>

              <div className="border-t pt-3">
                <div className="font-semibold text-slate-800 mb-2">Statutory Set-offs & Transfer Dates</div>
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="text-slate-600">Prior Year Set-off (₹ Cr)</label>
                    <input
                      type="number"
                      step="any"
                      value={formSetOff}
                      onChange={e => setFormSetOff(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Ongoing Escrow Transfer Date</label>
                    <input
                      type="date"
                      value={formOngoingTransferDate}
                      onChange={e => setFormOngoingTransferDate(e.target.value)}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Schedule VII Transfer Date</label>
                    <input
                      type="date"
                      value={formScheduleViiTransferDate}
                      onChange={e => setFormScheduleViiTransferDate(e.target.value)}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3 mt-3">
                  <div>
                    <label className="text-slate-600">Admin Overheads Spent (₹ Cr)</label>
                    <input
                      type="number"
                      step="any"
                      value={formAdminSpent}
                      onChange={e => setFormAdminSpent(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Impact Assessment Spent (₹ Cr)</label>
                    <input
                      type="number"
                      step="any"
                      value={formImpactSpent}
                      onChange={e => setFormImpactSpent(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                </div>
              </div>

              <div className="border-t pt-3">
                <div className="font-semibold text-slate-800 mb-2">Safety, Compliance & Remuneration</div>
                <div className="grid grid-cols-4 gap-3">
                  <div>
                    <label className="text-slate-600">Water Withdrawal (KL)</label>
                    <input
                      type="number"
                      value={brsrWaterWithdrawalKl}
                      onChange={e => setBrsrWaterWithdrawalKl(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Workplace Fatalities</label>
                    <input
                      type="number"
                      value={brsrFatalities}
                      onChange={e => setBrsrFatalities(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">POSH Rec / Res</label>
                    <div className="flex gap-1 mt-1">
                      <input
                        type="number"
                        placeholder="Rec"
                        value={brsrPoshReceived}
                        onChange={e => setBrsrPoshReceived(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                      <input
                        type="number"
                        placeholder="Res"
                        value={brsrPoshResolved}
                        onChange={e => setBrsrPoshResolved(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                    </div>
                  </div>
                  <div>
                    <label className="text-slate-600">Median Pay (₹ Lakhs)</label>
                    <input
                      type="number"
                      step="any"
                      value={brsrMedianRemuneration}
                      onChange={e => setBrsrMedianRemuneration(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                </div>

                <div className="mt-3">
                  <label className="flex items-center gap-2 cursor-pointer font-medium text-slate-700">
                    <input
                      type="checkbox"
                      checked={brsrZld}
                      onChange={e => setBrsrZld(e.target.checked)}
                      className="rounded text-teal-600"
                    />
                    Operates Zero Liquid Discharge (ZLD) Treatment Facility
                  </label>
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowAssessmentModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-medium shadow-sm"
                >
                  Save & Compute CSR Obligation
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: ADD SCHEDULE VII PROJECT */}
      {showProjectModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Add Schedule VII CSR Project</h3>
              <button onClick={() => setShowProjectModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSaveProject} className="space-y-4 text-xs">
              <div>
                <label className="font-semibold text-slate-700">Project Title</label>
                <input
                  type="text"
                  value={projectTitle}
                  onChange={e => setProjectTitle(e.target.value)}
                  className="w-full mt-1 p-2 border rounded"
                  placeholder="e.g. Rural Clean Drinking Water & Skill Centre"
                  required
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Schedule VII Category</label>
                  <select
                    value={projectCat}
                    onChange={e => setProjectCat(e.target.value as ScheduleViiCategory)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    {SCHEDULE_VII_CATEGORIES.map(c => (
                      <option key={c.key} value={c.key}>{c.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Project Nature</label>
                  <select
                    value={projectNature}
                    onChange={e => setProjectNature(e.target.value as CsrProjectNature)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    <option value="OngoingProject">Ongoing Project (Sec 135(6) - 3 Years)</option>
                    <option value="OtherThanOngoingProject">Other Than Ongoing Project (Sec 135(5))</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Location (State / District)</label>
                  <input
                    type="text"
                    value={projectStateDistrict}
                    onChange={e => setProjectStateDistrict(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="Nagpur, Maharashtra"
                  />
                </div>
                <div className="flex items-center pt-6">
                  <label className="flex items-center gap-2 cursor-pointer font-medium text-slate-700">
                    <input
                      type="checkbox"
                      checked={projectAspirational}
                      onChange={e => setProjectAspirational(e.target.checked)}
                      className="rounded text-emerald-600"
                    />
                    Aspirational District (NITI Aayog Priority)
                  </label>
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3 border-t pt-3">
                <div>
                  <label className="font-semibold text-slate-700">Total Outlay (₹ Cr)</label>
                  <input
                    type="number"
                    step="any"
                    value={projectOutlay}
                    onChange={e => setProjectOutlay(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded"
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Allocated Budget (₹ Cr)</label>
                  <input
                    type="number"
                    step="any"
                    value={projectAllocated}
                    onChange={e => setProjectAllocated(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded"
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Actual Spent (₹ Cr)</label>
                  <input
                    type="number"
                    step="any"
                    value={projectSpent}
                    onChange={e => setProjectSpent(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded"
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3 border-t pt-3">
                <div>
                  <label className="font-semibold text-slate-700">Implementation Mode</label>
                  <select
                    value={projectMode}
                    onChange={e => setProjectMode(e.target.value)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    <option value="Direct">Directly by Company</option>
                    <option value="ImplementingAgency">Implementing Agency (CSR-1)</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Agency Name</label>
                  <input
                    type="text"
                    value={projectAgencyName}
                    onChange={e => setProjectAgencyName(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="e.g. Swades Foundation"
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">MCA CSR-1 Reg No.</label>
                  <input
                    type="text"
                    value={projectAgencyCsr1}
                    onChange={e => setProjectAgencyCsr1(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="CSR00012345"
                  />
                </div>
              </div>

              <div className="mt-3">
                <label className="font-semibold text-slate-700">Multi-Year Project Status</label>
                <select
                  value={projectStatus}
                  onChange={e => setProjectStatus(e.target.value)}
                  className="w-full mt-1 p-2 border rounded bg-white"
                >
                  <option value="OngoingYear1">Ongoing Project - Year 1</option>
                  <option value="OngoingYear2">Ongoing Project - Year 2</option>
                  <option value="OngoingYear3">Ongoing Project - Year 3</option>
                  <option value="Completed">Completed &amp; Handed Over</option>
                </select>
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowProjectModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-medium shadow-sm"
                >
                  Add Project
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: CREATE BRSR REPORT */}
      {showBrsrModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">New SEBI BRSR Core Report & KPI Telemetry</h3>
              <button onClick={() => setShowBrsrModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSaveBrsrReport} className="space-y-4 text-xs">
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Company</label>
                  <select
                    value={brsrClient}
                    onChange={e => setBrsrClient(e.target.value)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                    required
                  >
                    <option value="">Select entity...</option>
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Financial Year</label>
                  <input
                    type="text"
                    value={brsrFy}
                    onChange={e => setBrsrFy(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Turnover (₹ Cr)</label>
                  <input
                    type="number"
                    step="any"
                    value={brsrTurnoverCr}
                    onChange={e => setBrsrTurnoverCr(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded"
                    required
                  />
                </div>
              </div>

              <div className="border-t pt-3">
                <div className="font-semibold text-slate-800 mb-2">Scope 1 & 2 Energy & GHG Emissions</div>
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="text-slate-600">Diesel Consumed (L)</label>
                    <input
                      type="number"
                      value={brsrDieselLitres}
                      onChange={e => setBrsrDieselLitres(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Natural Gas (Sm3)</label>
                    <input
                      type="number"
                      value={brsrGasSm3}
                      onChange={e => setBrsrGasSm3(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Grid Electricity (MWh)</label>
                    <input
                      type="number"
                      value={brsrGridMwh}
                      onChange={e => setBrsrGridMwh(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                </div>
              </div>

              <div className="border-t pt-3">
                <div className="font-semibold text-slate-800 mb-2">Water, Energy & Safety</div>
                <div className="grid grid-cols-4 gap-3">
                  <div>
                    <label className="text-slate-600">Water Consumed (KL)</label>
                    <input
                      type="number"
                      value={brsrWaterConsumptionKl}
                      onChange={e => setBrsrWaterConsumptionKl(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Energy (GJ)</label>
                    <input
                      type="number"
                      value={brsrEnergyGj}
                      onChange={e => setBrsrEnergyGj(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Renewable Energy %</label>
                    <input
                      type="number"
                      step="any"
                      value={brsrRenewablePct}
                      onChange={e => setBrsrRenewablePct(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">LTIFR Rate</label>
                    <input
                      type="number"
                      step="any"
                      value={brsrLtifr}
                      onChange={e => setBrsrLtifr(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                </div>
              </div>

              <div className="border-t pt-3">
                <div className="font-semibold text-slate-800 mb-2">Gender Diversity (Board, KMP & Workforce)</div>
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="text-slate-600">Board (Women / Total)</label>
                    <div className="flex gap-1 mt-1">
                      <input
                        type="number"
                        placeholder="Women"
                        value={brsrFemaleDir}
                        onChange={e => setBrsrFemaleDir(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                      <input
                        type="number"
                        placeholder="Total"
                        value={brsrTotalDir}
                        onChange={e => setBrsrTotalDir(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                    </div>
                  </div>
                  <div>
                    <label className="text-slate-600">KMP (Women / Total)</label>
                    <div className="flex gap-1 mt-1">
                      <input
                        type="number"
                        placeholder="Women"
                        value={brsrFemaleKmp}
                        onChange={e => setBrsrFemaleKmp(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                      <input
                        type="number"
                        placeholder="Total"
                        value={brsrTotalKmp}
                        onChange={e => setBrsrTotalKmp(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                    </div>
                  </div>
                  <div>
                    <label className="text-slate-600">Workforce (Women / Total)</label>
                    <div className="flex gap-1 mt-1">
                      <input
                        type="number"
                        placeholder="Women"
                        value={brsrFemaleEmp}
                        onChange={e => setBrsrFemaleEmp(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                      <input
                        type="number"
                        placeholder="Total"
                        value={brsrTotalEmp}
                        onChange={e => setBrsrTotalEmp(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                    </div>
                  </div>
                </div>
              </div>

              <div className="border-t pt-3">
                <div className="font-semibold text-slate-800 mb-2">Safety, Compliance & Remuneration</div>
                <div className="grid grid-cols-4 gap-3">
                  <div>
                    <label className="text-slate-600">Water Withdrawal (KL)</label>
                    <input
                      type="number"
                      value={brsrWaterWithdrawalKl}
                      onChange={e => setBrsrWaterWithdrawalKl(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">Workplace Fatalities</label>
                    <input
                      type="number"
                      value={brsrFatalities}
                      onChange={e => setBrsrFatalities(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                  <div>
                    <label className="text-slate-600">POSH Rec / Res</label>
                    <div className="flex gap-1 mt-1">
                      <input
                        type="number"
                        placeholder="Rec"
                        value={brsrPoshReceived}
                        onChange={e => setBrsrPoshReceived(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                      <input
                        type="number"
                        placeholder="Res"
                        value={brsrPoshResolved}
                        onChange={e => setBrsrPoshResolved(Number(e.target.value))}
                        className="w-1/2 p-2 border rounded"
                      />
                    </div>
                  </div>
                  <div>
                    <label className="text-slate-600">Median Pay (₹ Lakhs)</label>
                    <input
                      type="number"
                      step="any"
                      value={brsrMedianRemuneration}
                      onChange={e => setBrsrMedianRemuneration(Number(e.target.value))}
                      className="w-full mt-1 p-2 border rounded"
                    />
                  </div>
                </div>

                <div className="mt-3">
                  <label className="flex items-center gap-2 cursor-pointer font-medium text-slate-700">
                    <input
                      type="checkbox"
                      checked={brsrZld}
                      onChange={e => setBrsrZld(e.target.checked)}
                      className="rounded text-teal-600"
                    />
                    Operates Zero Liquid Discharge (ZLD) Treatment Facility
                  </label>
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowBrsrModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded font-medium shadow-sm"
                >
                  Save & Compute BRSR Indicators
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: CERTIFY CSR WITH UDIN */}
      {showCertifyCsrModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">ICAI Partner CSR Attestation & UDIN Sign-off</h3>
              <button onClick={() => setShowCertifyCsrModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleCertifyCsr} className="space-y-4 text-xs">
              <div>
                <label className="font-semibold text-slate-700">Statutory Auditor / Partner Name</label>
                <input
                  type="text"
                  value={certAuditorName}
                  onChange={e => setCertAuditorName(e.target.value)}
                  className="w-full mt-1 p-2 border rounded"
                  required
                />
              </div>
              <div>
                <label className="font-semibold text-slate-700">ICAI Membership Number</label>
                <input
                  type="text"
                  value={certMembershipNo}
                  onChange={e => setCertMembershipNo(e.target.value)}
                  className="w-full mt-1 p-2 border rounded"
                  required
                />
              </div>
              <div>
                <label className="font-semibold text-slate-700">Unique Document Identification Number (UDIN)</label>
                <input
                  type="text"
                  value={certUdin}
                  onChange={e => setCertUdin(e.target.value)}
                  className="w-full mt-1 p-2 border rounded font-mono"
                  placeholder="24094832AAAAES2025"
                  required
                />
              </div>
              <p className="text-[11px] text-slate-500 italic">
                By generating this sign-off, the auditor confirms verification of Section 135(5) and Section 135(6) compliance under CARO 2020 Clause XX.
              </p>
              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyCsrModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-500 text-white rounded font-medium shadow-sm"
                >
                  Affix UDIN & Certify
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 5: CERTIFY BRSR CORE ASSURANCE */}
      {showCertifyBrsrModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">SEBI BRSR Core Reasonable Assurance Sign-off</h3>
              <button onClick={() => setShowCertifyBrsrModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleCertifyBrsr} className="space-y-4 text-xs">
              <div>
                <label className="font-semibold text-slate-700">Assurance Auditor Name</label>
                <input
                  type="text"
                  value={certAuditorName}
                  onChange={e => setCertAuditorName(e.target.value)}
                  className="w-full mt-1 p-2 border rounded"
                  required
                />
              </div>
              <div>
                <label className="font-semibold text-slate-700">ICAI Membership Number</label>
                <input
                  type="text"
                  value={certMembershipNo}
                  onChange={e => setCertMembershipNo(e.target.value)}
                  className="w-full mt-1 p-2 border rounded"
                  required
                />
              </div>
              <div>
                <label className="font-semibold text-slate-700">SEBI Assurance UDIN</label>
                <input
                  type="text"
                  value={certUdin}
                  onChange={e => setCertUdin(e.target.value)}
                  className="w-full mt-1 p-2 border rounded font-mono"
                  placeholder="24094832AAAAES9999"
                  required
                />
              </div>
              <div>
                <label className="font-semibold text-slate-700">Assurance Standard Level</label>
                <select
                  value={brsrAssuranceType}
                  onChange={e => setBrsrAssuranceType(e.target.value)}
                  className="w-full mt-1 p-2 border rounded bg-white"
                >
                  <option value="ReasonableAssurance">Reasonable Assurance (ISAE 3000 / SSAE 3410)</option>
                  <option value="LimitedAssurance">Limited Assurance</option>
                </select>
              </div>
              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyBrsrModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-teal-600 hover:bg-teal-500 text-white rounded font-medium shadow-sm"
                >
                  Sign Assurance Statement
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 6: HTML ASSURANCE DOSSIER VIEWER & PRINT */}
      {showDossierModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-4xl w-full p-6 shadow-2xl space-y-4 max-h-[92vh] flex flex-col">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Print-Ready Statutory Assurance Dossier</h3>
              <div className="flex gap-2">
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
                  className="px-3 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded text-xs font-medium"
                >
                  🖨️ Print Dossier
                </button>
                <button onClick={() => setShowDossierModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
              </div>
            </div>
            <div className="flex-1 overflow-auto border rounded p-4 bg-slate-50">
              <iframe
                title="Assurance Dossier"
                srcDoc={dossierHtml}
                className="w-full h-[600px] bg-white border-0"
              />
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
