import { useState, useEffect, useMemo, type FormEvent } from 'react'
import {
  foreignRemittanceApi,
  api,
  type ForeignRemittanceAssessmentDto,
  type ForeignRemittanceSummaryDto,
  type CreateForeignRemittanceRequest,
  type FemaFirmsFilingDto,
  type FemaFirmsSummaryDto,
  type SaveFemaFirmsFilingRequest,
  type CertifyFemaFirmsRequest,
  type RbiAnnualFlaReturnDto,
  type RbiFlaSummaryDto,
  type SaveRbiAnnualFlaReturnRequest,
  type CertifyRbiFlaRequest,
  type DtaaMfnEvaluationDto,
  type PeExposureEvaluationDto,
  type FirmsFilingType,
  type CapitalInstrumentType,
  type FdiEntryRoute,
  type TransferDirectionType,
  type TransferModeType,
  type FlaAuditStatus,
  type Client
} from './api'

function formatINR(val?: number): string {
  if (val == null || isNaN(val)) return '₹0'
  return '₹' + Number(val).toLocaleString('en-IN', { maximumFractionDigits: 2 })
}

const STANDARD_CURRENCIES = ['USD', 'EUR', 'GBP', 'SGD', 'AED', 'JPY', 'AUD', 'CAD', 'CHF', 'INR']

const RBI_PURPOSE_CODES = [
  { code: 'P0802', desc: 'Software consultancy / implementation / data processing', exempt: false },
  { code: 'S1007', desc: 'Royalties / license fees for use of intellectual property / patents', exempt: false },
  { code: 'S1006', desc: 'Fees for technical services / architectural / engineering', exempt: false },
  { code: 'S1107', desc: 'Management consultancy, accounting, auditing, public relations', exempt: false },
  { code: 'S1009', desc: 'Information services / news agency / database rights', exempt: false },
  { code: 'P0103', desc: 'Advance payment against import of goods (Commercial)', exempt: true },
  { code: 'S0001', desc: 'Business travel / conference participation', exempt: true },
  { code: 'S0005', desc: 'Education abroad / tuition / university fees', exempt: true },
  { code: 'S0011', desc: 'Medical treatment abroad', exempt: true },
  { code: 'S1301', desc: 'Remittance by non-resident Indians (Family maintenance)', exempt: true }
]

type ClientPage = {
  items: Client[]
  total: number
}

export function ForeignRemittancePage() {
  // Main Desk Tabs
  const [deskTab, setDeskTab] = useState<'remittances' | 'form15cb-vault' | 'dtaa-mfn-pe' | 'rbi-firms' | 'rbi-fla'>('remittances')

  // Global State
  const [loading, setLoading] = useState(true)
  const [notice, setNotice] = useState<string | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)
  const [clients, setClients] = useState<Client[]>([])

  // Filters
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')
  const [selectedStatus, setSelectedStatus] = useState<string>('')
  const [searchQuery, setSearchQuery] = useState('')

  // TAB 1 & 2: Form 15CA & 15CB Data
  const [assessments, setAssessments] = useState<ForeignRemittanceAssessmentDto[]>([])
  const [summary, setSummary] = useState<ForeignRemittanceSummaryDto | null>(null)
  const [activeItem, setActiveItem] = useState<ForeignRemittanceAssessmentDto | null>(null)
  const [drawerTab, setDrawerTab] = useState<'overview' | 'checklist' | 'form15cb' | 'form15ca'>('overview')

  // Modals for Remittances
  const [showCreateModal, setShowCreateModal] = useState(false)
  const [showCertifyModal, setShowCertifyModal] = useState(false)

  // TAB 3: DTAA MFN & PE State
  const [mfnCountry, setMfnCountry] = useState('Netherlands')
  const [mfnBaseRate, setMfnBaseRate] = useState<number>(10)
  const [mfnResult, setMfnResult] = useState<DtaaMfnEvaluationDto | null>(null)

  const [peOffice, setPeOffice] = useState(false)
  const [peDays, setPeDays] = useState<number>(45)
  const [peAuthority, setPeAuthority] = useState(false)
  const [peInventory, setPeInventory] = useState(false)
  const [peCountry, setPeCountry] = useState('United States')
  const [peResult, setPeResult] = useState<PeExposureEvaluationDto | null>(null)

  // TAB 4: RBI FIRMS (FC-GPR / FC-TRS) State
  const [firmsFilings, setFirmsFilings] = useState<FemaFirmsFilingDto[]>([])
  const [firmsSummary, setFirmsSummary] = useState<FemaFirmsSummaryDto | null>(null)
  const [selectedFirmsType, setSelectedFirmsType] = useState<FirmsFilingType | ''>('')
  const [showFirmsModal, setShowFirmsModal] = useState(false)
  const [showCertifyFirmsModal, setShowCertifyFirmsModal] = useState(false)
  const [certifyingFirmsId, setCertifyingFirmsId] = useState<string>('')
  const [showFirmsCertViewer, setShowFirmsCertViewer] = useState(false)
  const [firmsCertHtml, setFirmsCertHtml] = useState('')

  // Form states for FIRMS
  const [firmsClientId, setFirmsClientId] = useState('')
  const [firmsType, setFirmsType] = useState<FirmsFilingType>('FcGpr')
  const [firmsRefNo, setFirmsRefNo] = useState('FIRMS/FC-GPR/2025/001')
  const [firmsCin, setFirmsCin] = useState('U72900MH2021PTC123456')
  const [firmsEntityName, setFirmsEntityName] = useState('Zenith Robotics India Pvt Ltd')
  const [firmsPan, setFirmsPan] = useState('AABCZ1234F')
  const [firmsNrName, setFirmsNrName] = useState('Global Ventures LLC')
  const [firmsNrCountry, setFirmsNrCountry] = useState('United States')
  const [firmsInstrumentType, setFirmsInstrumentType] = useState<CapitalInstrumentType>('EquityShares')
  const [firmsCount, setFirmsCount] = useState<number>(50000)
  const firmsFaceValue = 10
  const [firmsIssuePrice, setFirmsIssuePrice] = useState<number>(200)
  const [firmsFairValue, setFirmsFairValue] = useState<number>(180)
  const firmsExchangeRate = 85.0
  const [firmsAllotmentDate, setFirmsAllotmentDate] = useState(new Date().toISOString().split('T')[0])
  const [firmsValMethod, setFirmsValMethod] = useState('DiscountedCashFlow')
  const [firmsValuer, setFirmsValuer] = useState('Apex Capital Advisors (SEBI Merchant Banker)')
  const firmsFdiRoute: FdiEntryRoute = 'AutomaticRoute'
  const firmsSectoralCap = 100
  const firmsPostPct = 25.5
  const [firmsDirection, setFirmsDirection] = useState<TransferDirectionType>('ResidentToNonResident')
  const [firmsTransferMode, setFirmsTransferMode] = useState<TransferModeType>('Sale')

  // TAB 5: RBI Annual FLA Return State
  const [flaReturns, setFlaReturns] = useState<RbiAnnualFlaReturnDto[]>([])
  const [flaSummary, setFlaSummary] = useState<RbiFlaSummaryDto | null>(null)
  const [showFlaModal, setShowFlaModal] = useState(false)
  const [showCertifyFlaModal, setShowCertifyFlaModal] = useState(false)
  const [certifyingFlaId, setCertifyingFlaId] = useState<string>('')
  const [showFileFlaModal, setShowFileFlaModal] = useState(false)
  const [filingFlaId, setFilingFlaId] = useState<string>('')
  const [showFlaReportViewer, setShowFlaReportViewer] = useState(false)
  const [flaReportHtml, setFlaReportHtml] = useState('')

  // Form states for FLA
  const [flaClientId, setFlaClientId] = useState('')
  const [flaFy, setFlaFy] = useState('2024-25')
  const [flaCin, setFlaCin] = useState('L72200DL2018PLC334455')
  const [flaCompanyName, setFlaCompanyName] = useState('Aura Tech Solutions Ltd')
  const [flaPan, setFlaPan] = useState('AABCA9988K')
  const [flaAuditStatus, setFlaAuditStatus] = useState<FlaAuditStatus>('Audited')
  const [flaPaidUpCapital, setFlaPaidUpCapital] = useState<number>(50000000)
  const [flaReserves, setFlaReserves] = useState<number>(150000000)
  const [flaSalesTurnover, setFlaSalesTurnover] = useState<number>(350000000)
  const flaPat = 45000000
  const flaTotalAssets = 280000000
  const flaTotalLiabilities = 80000000
  const [flaForeignEquityPct, setFlaForeignEquityPct] = useState<number>(30.0)
  const [flaFdiFaceValue, setFlaFdiFaceValue] = useState<number>(15000000)
  const flaFdiDcfValue = 75000000
  const [flaFreshFdi, setFlaFreshFdi] = useState<number>(20000000)
  const flaDisinvestment = 0
  const flaRetainedEarningsFdi = 13500000
  const flaHasOdi = true
  const flaOverseasEntity = 'Aura EMEA BV'
  const flaOverseasCountry = 'Netherlands'
  const flaOdiEquityPct = 100
  const [flaOdiEquityInv, setFlaOdiEquityInv] = useState<number>(25000000)
  const flaOdiLoan = 5000000
  const flaOdiGuarantee = 10000000
  const [flaEcb, setFlaEcb] = useState<number>(15000000)
  const flaTradeSuppliers = 8000000
  const flaTradeBuyers = 4000000
  const [flaBankBalances, setFlaBankBalances] = useState<number>(6000000)
  const flaOtherAssets = 2000000

  // Form 15CA/15CB create form state
  const [createReq, setCreateReq] = useState<Partial<CreateForeignRemittanceRequest>>({
    financialYear: '2024-25',
    remittanceCurrency: 'USD',
    exchangeRate: 85.0,
    rbiPurposeCode: 'P0802',
    purposeDescription: 'Software consultancy / implementation',
    natureOfRemittance: 'FeesForTechnicalServices',
    isChargeableToTax: true,
    domesticSection: 'Section 195 read with Section 115A',
    domesticTaxRate: 20.0,
    surchargeRate: 0.0,
    cessRate: 4.0,
    treatyBenefitClaimed: true,
    treatyCountry: 'United States',
    treatyArticle: 'Article 12 - Royalties and Fees for Included Services',
    treatyTaxRate: 15.0,
    hasTrc: true,
    hasForm10F: true,
    noPeDeclaration: true,
    beneficialOwnershipConfirmed: true,
    mliPptPassed: true,
    taxBorneByRemitter: false
  })

  // Signatory forms
  const [certifyForm, setCertifyForm] = useState({
    caName: 'CA Rajesh V. Sharma',
    caMembershipNumber: '214589',
    caFirmName: 'Sharma & Associates LLP',
    caFrn: '123456N',
    udin: '25214589BK99887766'
  })
  const [portalAck, setPortalAck] = useState('')

  // Load initial data
  useEffect(() => {
    fetchClients()
    fetchRemittances()
    fetchFirmsData()
    fetchFlaData()
    handleRunMfnEval()
    handleRunPeEval()
  }, [])

  useEffect(() => {
    fetchRemittances()
  }, [selectedFy, selectedStatus])

  async function fetchClients() {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      setClients(res.items || [])
      if (res.items && res.items.length > 0) {
        if (!firmsClientId) setFirmsClientId(res.items[0].id)
        if (!flaClientId) setFlaClientId(res.items[0].id)
      }
    } catch {
      // ignore
    }
  }

  async function fetchRemittances() {
    try {
      setLoading(true)
      const [sum, list] = await Promise.all([
        foreignRemittanceApi.getSummary(selectedFy),
        foreignRemittanceApi.list(undefined, selectedFy, selectedStatus || undefined)
      ])
      setSummary(sum)
      setAssessments(list)
      if (activeItem) {
        const fresh = list.find(x => x.id === activeItem.id)
        if (fresh) setActiveItem(fresh)
      }
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to load foreign remittances.')
    } finally {
      setLoading(false)
    }
  }

  async function fetchFirmsData() {
    try {
      const [s, list] = await Promise.all([
        foreignRemittanceApi.getFirmsSummary(),
        foreignRemittanceApi.getFirmsFilings(undefined, selectedFirmsType || undefined)
      ])
      setFirmsSummary(s)
      setFirmsFilings(list)
    } catch {
      // ignore
    }
  }

  async function fetchFlaData() {
    try {
      const [s, list] = await Promise.all([
        foreignRemittanceApi.getFlaSummary(selectedFy),
        foreignRemittanceApi.getFlaReturns(undefined, selectedFy)
      ])
      setFlaSummary(s)
      setFlaReturns(list)
    } catch {
      // ignore
    }
  }

  // Handle MFN Evaluation
  async function handleRunMfnEval() {
    try {
      const res = await foreignRemittanceApi.evaluateMfn({
        country: mfnCountry,
        baseTreatyRate: Number(mfnBaseRate)
      })
      setMfnResult(res)
    } catch {
      // ignore
    }
  }

  // Handle PE Risk Evaluation
  async function handleRunPeEval() {
    try {
      const res = await foreignRemittanceApi.evaluatePe({
        hasPhysicalOffice: peOffice,
        personnelDaysInIndia: Number(peDays),
        hasAuthorityToConcludeContracts: peAuthority,
        maintainsInventoryStock: peInventory,
        treatyCountry: peCountry
      })
      setPeResult(res)
    } catch {
      // ignore
    }
  }

  // Handle Save FIRMS Filing
  async function handleSaveFirms(e: FormEvent) {
    e.preventDefault()
    if (!firmsClientId || !firmsRefNo || !firmsEntityName) {
      setErrorMsg('Mandatory FIRMS fields are required.')
      return
    }

    setLoading(true)
    try {
      const totalInr = firmsCount * firmsIssuePrice
      const totalFc = firmsExchangeRate > 0 ? totalInr / firmsExchangeRate : 0

      const req: SaveFemaFirmsFilingRequest = {
        clientId: firmsClientId,
        filingType: firmsType,
        referenceNumber: firmsRefNo,
        indianEntityCin: firmsCin,
        indianEntityName: firmsEntityName,
        pan: firmsPan,
        nonResidentPartyName: firmsNrName,
        nonResidentCountry: firmsNrCountry,
        instrumentType: firmsInstrumentType,
        numberOfInstruments: Number(firmsCount),
        faceValuePerInstrument: Number(firmsFaceValue),
        issueOrTransferPricePerInstrument: Number(firmsIssuePrice),
        fairValuePerInstrument: Number(firmsFairValue),
        totalConsiderationInr: totalInr,
        totalConsiderationFc: totalFc,
        currency: 'USD',
        exchangeRate: Number(firmsExchangeRate),
        valuationMethod: firmsValMethod,
        valuerName: firmsValuer,
        dateOfAllotmentOrTransfer: firmsAllotmentDate,
        fdiRoute: firmsFdiRoute,
        sectoralCapPct: Number(firmsSectoralCap),
        postAllotmentForeignShareholdingPct: Number(firmsPostPct),
        transferDirection: firmsDirection,
        transferMode: firmsTransferMode
      }

      await foreignRemittanceApi.saveFirmsFiling(req)
      setNotice(`FEMA FIRMS Filing (${firmsType}) saved with pricing guidelines compliance check.`)
      setShowFirmsModal(false)
      await fetchFirmsData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to save FIRMS filing.')
    } finally {
      setLoading(false)
    }
  }

  // Handle Certify FIRMS Filing
  async function handleCertifyFirms(e: FormEvent) {
    e.preventDefault()
    if (!certifyingFirmsId) return

    setLoading(true)
    try {
      const req: CertifyFemaFirmsRequest = {
        caName: certifyForm.caName,
        caMembershipNumber: certifyForm.caMembershipNumber,
        caFirmName: certifyForm.caFirmName,
        udin: certifyForm.udin
      }
      await foreignRemittanceApi.certifyFirmsFiling(certifyingFirmsId, req)
      setNotice('FEMA FIRMS compliance certificate signed and certified with UDIN.')
      setShowCertifyFirmsModal(false)
      await fetchFirmsData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to certify FIRMS filing.')
    } finally {
      setLoading(false)
    }
  }

  // Handle Delete FIRMS Filing
  async function handleDeleteFirms(id: string) {
    if (!confirm('Permanently remove this FEMA FIRMS filing?')) return
    setLoading(true)
    try {
      await foreignRemittanceApi.deleteFirmsFiling(id)
      setNotice('FEMA FIRMS filing deleted.')
      await fetchFirmsData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete filing.')
    } finally {
      setLoading(false)
    }
  }

  // Handle View FIRMS Compliance Certificate HTML
  async function handleOpenFirmsCert(id: string) {
    setLoading(true)
    try {
      const url = foreignRemittanceApi.getFirmsCertificateHtmlUrl(id)
      const res = await fetch(url, { headers: { 'Authorization': `Bearer ${localStorage.getItem('token') || ''}` } })
      const html = await res.text()
      setFirmsCertHtml(html)
      setShowFirmsCertViewer(true)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to load certificate.')
    } finally {
      setLoading(false)
    }
  }

  // Handle Save Annual FLA Return
  async function handleSaveFla(e: FormEvent) {
    e.preventDefault()
    if (!flaClientId || !flaCompanyName || !flaCin) {
      setErrorMsg('Mandatory company details are required for FLA Return.')
      return
    }

    setLoading(true)
    try {
      const req: SaveRbiAnnualFlaReturnRequest = {
        clientId: flaClientId,
        financialYear: flaFy,
        companyCin: flaCin,
        companyName: flaCompanyName,
        pan: flaPan,
        accountClosingDate: new Date(2025, 2, 31).toISOString(),
        statutoryFilingDeadline: new Date(2025, 6, 15).toISOString(),
        auditStatus: flaAuditStatus,
        paidUpCapitalInr: Number(flaPaidUpCapital),
        reservesAndSurplusInr: Number(flaReserves),
        totalSalesTurnoverInr: Number(flaSalesTurnover),
        netProfitLossAfterTaxInr: Number(flaPat),
        totalAssetsInr: Number(flaTotalAssets),
        totalLiabilitiesInr: Number(flaTotalLiabilities),
        foreignEquityHoldingPct: Number(flaForeignEquityPct),
        fdiAtFaceValueInr: Number(flaFdiFaceValue),
        fdiAtMarketOrDcfValueInr: Number(flaFdiDcfValue),
        freshFdiInflowsDuringYearInr: Number(flaFreshFdi),
        disinvestmentOutflowsDuringYearInr: Number(flaDisinvestment),
        retainedEarningsAttributableToFdiInr: Number(flaRetainedEarningsFdi),
        hasOdi: flaHasOdi,
        overseasEntityName: flaOverseasEntity,
        overseasCountry: flaOverseasCountry,
        odiEquityHoldingPct: Number(flaOdiEquityPct),
        odiEquityInvestmentInr: Number(flaOdiEquityInv),
        odiLoanOutstandingInr: Number(flaOdiLoan),
        odiGuaranteeIssuedInr: Number(flaOdiGuarantee),
        externalCommercialBorrowingsEcbInr: Number(flaEcb),
        foreignTradeCreditSuppliersInr: Number(flaTradeSuppliers),
        foreignTradeCreditBuyersInr: Number(flaTradeBuyers),
        foreignBankBalancesInr: Number(flaBankBalances),
        otherForeignAssetsInr: Number(flaOtherAssets)
      }

      await foreignRemittanceApi.saveFlaReturn(req)
      setNotice(`RBI Annual FLA Return for FY ${flaFy} compiled with Net Asset Value (NAV) computations.`)
      setShowFlaModal(false)
      await fetchFlaData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to save FLA return.')
    } finally {
      setLoading(false)
    }
  }

  // Handle Certify FLA Return
  async function handleCertifyFla(e: FormEvent) {
    e.preventDefault()
    if (!certifyingFlaId) return

    setLoading(true)
    try {
      const req: CertifyRbiFlaRequest = {
        certifiedByCa: certifyForm.caName,
        udin: certifyForm.udin,
        verificationNotes: 'Certified in accordance with FEMA regulations and audited balance sheet metrics.'
      }
      await foreignRemittanceApi.certifyFlaReturn(certifyingFlaId, req)
      setNotice('RBI Annual FLA Return certified with UDIN.')
      setShowCertifyFlaModal(false)
      await fetchFlaData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to certify FLA return.')
    } finally {
      setLoading(false)
    }
  }

  // Handle File FLA Return
  async function handleFileFla(e: FormEvent) {
    e.preventDefault()
    if (!filingFlaId || !portalAck) return

    setLoading(true)
    try {
      await foreignRemittanceApi.fileFlaReturn(filingFlaId, { rbiFlairAckNumber: portalAck })
      setNotice(`RBI FLAIR acknowledgment ${portalAck} recorded. Return status updated to Submitted.`)
      setShowFileFlaModal(false)
      setPortalAck('')
      await fetchFlaData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to record filing.')
    } finally {
      setLoading(false)
    }
  }

  // Handle Delete FLA Return
  async function handleDeleteFla(id: string) {
    if (!confirm('Permanently delete this Annual FLA return record?')) return
    setLoading(true)
    try {
      await foreignRemittanceApi.deleteFlaReturn(id)
      setNotice('FLA return record deleted.')
      await fetchFlaData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to delete return.')
    } finally {
      setLoading(false)
    }
  }

  // Handle View FLA Return HTML
  async function handleOpenFlaReport(id: string) {
    setLoading(true)
    try {
      const url = foreignRemittanceApi.getFlaReportHtmlUrl(id)
      const res = await fetch(url, { headers: { 'Authorization': `Bearer ${localStorage.getItem('token') || ''}` } })
      const html = await res.text()
      setFlaReportHtml(html)
      setShowFlaReportViewer(true)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to load FLA report.')
    } finally {
      setLoading(false)
    }
  }

  // Handle Create Remittance (15CA/15CB)
  async function handleCreateRemittance(e: FormEvent) {
    e.preventDefault()
    if (!createReq.clientId || !createReq.beneficiaryName || !createReq.foreignAmount) {
      setErrorMsg('Mandatory remittance fields are required.')
      return
    }

    setLoading(true)
    try {
      await foreignRemittanceApi.create(createReq as CreateForeignRemittanceRequest)
      setNotice('Foreign remittance assessment created with automated 15CA Part routing and 15CB threshold analysis.')
      setShowCreateModal(false)
      await fetchRemittances()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to create remittance assessment.')
    } finally {
      setLoading(false)
    }
  }

  // Handle Certify Remittance (15CB)
  async function handleCertifyRemittance(e: FormEvent) {
    e.preventDefault()
    if (!activeItem) return

    setLoading(true)
    try {
      await foreignRemittanceApi.certify(activeItem.id, certifyForm)
      setNotice(`Form 15CB Certificate signed and sealed with UDIN: ${certifyForm.udin}`)
      setShowCertifyModal(false)
      await fetchRemittances()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Failed to certify Form 15CB.')
    } finally {
      setLoading(false)
    }
  }

  // Filtered Remittances
  const filteredAssessments = useMemo(() => {
    return assessments.filter(a => {
      const q = searchQuery.toLowerCase()
      const matchQ =
        !q ||
        a.remittanceReference.toLowerCase().includes(q) ||
        a.beneficiaryName.toLowerCase().includes(q) ||
        a.beneficiaryCountry.toLowerCase().includes(q) ||
        (a.clientName && a.clientName.toLowerCase().includes(q))
      return matchQ
    })
  }, [assessments, searchQuery])

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header Banner */}
      <div className="bg-gradient-to-r from-slate-900 via-sky-950 to-blue-950 text-white rounded-2xl p-6 shadow-xl relative overflow-hidden border border-sky-900/40">
        <div className="relative z-10 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <div className="flex items-center gap-3">
              <span className="text-3xl">🌐</span>
              <h1 className="text-2xl font-black tracking-tight text-white flex items-center gap-2">
                RBI FEMA Foreign Remittance &amp; Form 15CB Certification Desk
                <span className="text-xs bg-sky-500/30 text-sky-200 border border-sky-400/30 px-2 py-0.5 rounded-full font-semibold">
                  Sec 195(6) / FEMA NDI / FLAIR
                </span>
              </h1>
            </div>
            <p className="text-sm text-sky-200/80 mt-1 max-w-3xl">
              High-yield cross-border certification workspace: Form 15CB CA Certificate generator with DTAA treaty &amp; MFN optimization,
              Form 15CA Part A/B/C/D auto-routing, RBI FIRMS Single Master Form (FC-GPR / FC-TRS) pricing guidelines compliance,
              and July 15th RBI Annual FLA (Foreign Liabilities &amp; Assets) Return engine.
            </p>
          </div>
          <div className="flex items-center gap-2 self-start md:self-auto flex-wrap">
            <button
              onClick={() => setShowCreateModal(true)}
              className="bg-sky-600 hover:bg-sky-500 text-white font-medium text-xs px-3.5 py-2.5 rounded-lg transition-all shadow-md flex items-center gap-1.5"
            >
              <span>+</span> New 15CA/15CB Remittance
            </button>
            <button
              onClick={() => setShowFirmsModal(true)}
              className="bg-indigo-600 hover:bg-indigo-500 text-white font-medium text-xs px-3.5 py-2.5 rounded-lg transition-all shadow-md flex items-center gap-1.5"
            >
              <span>+</span> New FIRMS (FC-GPR/TRS)
            </button>
            <button
              onClick={() => setShowFlaModal(true)}
              className="bg-emerald-600 hover:bg-emerald-500 text-white font-medium text-xs px-3.5 py-2.5 rounded-lg transition-all shadow-md flex items-center gap-1.5"
            >
              <span>+</span> New Annual FLA Return
            </button>
          </div>
        </div>

        {/* Quick Statutory Reference Strip */}
        <div className="relative z-10 mt-5 pt-4 border-t border-sky-800/50 grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs">
          <div className="bg-black/20 backdrop-blur-sm rounded-lg p-2.5 border border-white/5">
            <div className="text-sky-300 font-semibold">Section 195(6) / Rule 37BB</div>
            <div className="text-slate-100 font-mono text-[11px] mt-0.5">Part C: &gt;₹5L requires 15CB CA Sign-off</div>
          </div>
          <div className="bg-black/20 backdrop-blur-sm rounded-lg p-2.5 border border-white/5">
            <div className="text-sky-300 font-semibold">Supreme Court Nestle MFN Rule</div>
            <div className="text-slate-100 font-mono text-[11px] mt-0.5">Sec 90(1) notification mandatory for MFN</div>
          </div>
          <div className="bg-black/20 backdrop-blur-sm rounded-lg p-2.5 border border-white/5">
            <div className="text-sky-300 font-semibold">RBI FIRMS (NDI Rule 21)</div>
            <div className="text-slate-100 font-mono text-[11px] mt-0.5">FC-GPR: 30d • FC-TRS: 60d • DCF floor</div>
          </div>
          <div className="bg-black/20 backdrop-blur-sm rounded-lg p-2.5 border border-white/5">
            <div className="text-sky-300 font-semibold">RBI Annual FLA Return</div>
            <div className="text-slate-100 font-mono text-[11px] mt-0.5">July 15th Deadline • FDI at NAV</div>
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

      {/* Navigation Desk Tabs */}
      <div className="flex border-b border-slate-200 gap-1 overflow-x-auto text-sm font-semibold">
        <button
          onClick={() => setDeskTab('remittances')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${deskTab === 'remittances' ? 'border-sky-600 text-sky-700 bg-sky-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>🌐</span> Form 15CA &amp; 15CB Remittance Studio
          {summary && (
            <span className="bg-sky-100 text-sky-800 text-[10px] px-1.5 py-0.5 rounded-full font-bold">
              {summary.totalAssessments}
            </span>
          )}
        </button>
        <button
          onClick={() => setDeskTab('form15cb-vault')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${deskTab === 'form15cb-vault' ? 'border-sky-600 text-sky-700 bg-sky-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>📜</span> Form 15CB CA Certification &amp; UDIN Vault
          {summary && summary.certifiedCount > 0 && (
            <span className="bg-emerald-100 text-emerald-800 text-[10px] px-1.5 py-0.5 rounded-full font-bold">
              {summary.certifiedCount} Certified
            </span>
          )}
        </button>
        <button
          onClick={() => setDeskTab('dtaa-mfn-pe')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${deskTab === 'dtaa-mfn-pe' ? 'border-sky-600 text-sky-700 bg-sky-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>⚖️</span> DTAA Treaty, MFN &amp; PE Exposure Analyzer
        </button>
        <button
          onClick={() => setDeskTab('rbi-firms')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${deskTab === 'rbi-firms' ? 'border-sky-600 text-sky-700 bg-sky-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>🏦</span> RBI FIRMS (FC-GPR / FC-TRS)
          {firmsSummary && (
            <span className="bg-indigo-100 text-indigo-800 text-[10px] px-1.5 py-0.5 rounded-full font-bold">
              {firmsSummary.totalFilings}
            </span>
          )}
        </button>
        <button
          onClick={() => setDeskTab('rbi-fla')}
          className={`px-4 py-2.5 border-b-2 flex items-center gap-2 transition-all whitespace-nowrap ${deskTab === 'rbi-fla' ? 'border-sky-600 text-sky-700 bg-sky-50/50 rounded-t-lg' : 'border-transparent text-slate-600 hover:text-slate-900'}`}
        >
          <span>📊</span> RBI Annual FLA Return (FLAIR)
          {flaSummary && (
            <span className="bg-purple-100 text-purple-800 text-[10px] px-1.5 py-0.5 rounded-full font-bold">
              {flaSummary.totalReturns}
            </span>
          )}
        </button>
      </div>

      {/* TAB 1: FORM 15CA & 15CB REMITTANCE STUDIO */}
      {deskTab === 'remittances' && (
        <div className="space-y-6">
          {/* Summary KPIs */}
          {summary && (
            <div className="grid grid-cols-2 sm:grid-cols-5 gap-4">
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Total Remittances</span>
                <div className="text-xl font-bold font-mono text-slate-900 mt-1">{formatINR(summary.totalRemittanceInr)}</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">{summary.totalAssessments} Assessed Items</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Withholding Tax (TDS)</span>
                <div className="text-xl font-bold font-mono text-sky-700 mt-1">{formatINR(summary.totalTdsInr)}</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">Deducted u/s 195</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Part C (&gt;₹5L Mandatory 15CB)</span>
                <div className="text-xl font-bold font-mono text-indigo-700 mt-1">{summary.partCCount} Remittances</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">Requires CA Certificate</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Part A (&le;₹5L) / Part D (Nil)</span>
                <div className="text-xl font-bold font-mono text-emerald-700 mt-1">{summary.partACount + summary.partDCount} Remittances</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">No 15CB Needed</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Certified / Filed</span>
                <div className="text-xl font-bold font-mono text-slate-800 mt-1">{summary.certifiedCount} Cert / {summary.filedCount} Filed</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">Income Tax Portal</span>
              </div>
            </div>
          )}

          {/* Filters & Table */}
          <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
              <div className="flex items-center gap-3">
                <input
                  type="text"
                  value={searchQuery}
                  onChange={e => setSearchQuery(e.target.value)}
                  placeholder="Search beneficiary, remitter, country..."
                  className="p-2 border rounded-lg text-xs w-64 bg-slate-50"
                />
                <select
                  value={selectedFy}
                  onChange={e => setSelectedFy(e.target.value)}
                  className="p-2 border rounded-lg text-xs bg-slate-50"
                >
                  <option value="2024-25">FY 2024-25</option>
                  <option value="2025-26">FY 2025-26</option>
                  <option value="2023-24">FY 2023-24</option>
                </select>
                <select
                  value={selectedStatus}
                  onChange={e => setSelectedStatus(e.target.value)}
                  className="p-2 border rounded-lg text-xs bg-slate-50"
                >
                  <option value="">All Statuses</option>
                  <option value="Draft">Draft</option>
                  <option value="Assessed">Assessed</option>
                  <option value="Certified">Certified</option>
                  <option value="Filed">Filed</option>
                </select>
              </div>

              <div className="text-xs text-slate-500">
                Showing <strong>{filteredAssessments.length}</strong> remittance assessments
              </div>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-xs text-left">
                <thead className="bg-slate-50 text-slate-600 font-semibold border-b">
                  <tr>
                    <th className="py-2.5 px-3">Reference / Payer</th>
                    <th className="py-2.5 px-3">Beneficiary (Country)</th>
                    <th className="py-2.5 px-3">Remittance Amount</th>
                    <th className="py-2.5 px-3">15CA Routing</th>
                    <th className="py-2.5 px-3">Governing Tax Rate</th>
                    <th className="py-2.5 px-3">TDS (INR)</th>
                    <th className="py-2.5 px-3">15CB Status</th>
                    <th className="py-2.5 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {filteredAssessments.map(a => (
                    <tr key={a.id} className="hover:bg-slate-50">
                      <td className="py-2.5 px-3 font-semibold text-slate-900">
                        {a.remittanceReference}
                        <span className="text-[10px] text-slate-400 block font-normal">{a.remitterName} ({a.remitterPan})</span>
                      </td>
                      <td className="py-2.5 px-3 text-slate-700">
                        {a.beneficiaryName}
                        <span className="text-[10px] text-slate-500 block">{a.beneficiaryCountry} ({a.beneficiaryCountryCode})</span>
                      </td>
                      <td className="py-2.5 px-3 font-mono font-medium text-slate-800">
                        {formatINR(a.inrEquivalent)}
                        <span className="text-[10px] text-slate-400 block">{a.foreignAmount.toLocaleString()} {a.remittanceCurrency}</span>
                      </td>
                      <td className="py-2.5 px-3">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${a.form15CaPart === 'PartC' ? 'bg-indigo-100 text-indigo-800' : a.form15CaPart === 'PartA' ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-100 text-slate-700'}`}>
                          {a.form15CaPart}
                        </span>
                      </td>
                      <td className="py-2.5 px-3 font-mono font-semibold text-sky-800">
                        {a.finalWithholdingRate.toFixed(2)}%
                        <span className="text-[10px] text-slate-400 block font-normal">{a.applicableRateType}</span>
                      </td>
                      <td className="py-2.5 px-3 font-mono font-bold text-slate-900">{formatINR(a.tdsAmountInr)}</td>
                      <td className="py-2.5 px-3">
                        {a.requiresForm15Cb ? (
                          a.status === 'Certified' || a.status === 'Filed' ? (
                            <span className="bg-emerald-100 text-emerald-800 text-[10px] px-2 py-0.5 rounded font-bold">
                              ✅ 15CB Certified
                            </span>
                          ) : (
                            <span className="bg-amber-100 text-amber-800 text-[10px] px-2 py-0.5 rounded font-bold">
                              ⏳ 15CB Pending
                            </span>
                          )
                        ) : (
                          <span className="text-slate-400 text-[10px]">Not Required</span>
                        )}
                      </td>
                      <td className="py-2.5 px-3 text-right">
                        <button
                          onClick={() => {
                            setActiveItem(a)
                            setDrawerTab('overview')
                          }}
                          className="bg-slate-100 hover:bg-slate-200 text-slate-800 px-2.5 py-1 rounded text-xs font-semibold"
                        >
                          Details →
                        </button>
                      </td>
                    </tr>
                  ))}
                  {filteredAssessments.length === 0 && (
                    <tr>
                      <td colSpan={8} className="py-6 text-center text-slate-400">No foreign remittance assessments found.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: FORM 15CB CA CERTIFICATION & UDIN VAULT */}
      {deskTab === 'form15cb-vault' && (
        <div className="space-y-6">
          <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
            <div className="border-b pb-3">
              <h3 className="font-bold text-slate-900 text-sm">Form 15CB Chartered Accountant Attestation Registry</h3>
              <p className="text-xs text-slate-500">Statutory certificate of an accountant under Rule 37BB(2) on payments made to non-residents.</p>
            </div>

            <div className="space-y-3">
              {assessments.filter(a => a.requiresForm15Cb).map(a => (
                <div key={a.id} className="border border-slate-200 rounded-xl p-4 bg-white shadow-xs hover:border-sky-300 transition">
                  <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 border-b pb-3">
                    <div>
                      <div className="flex items-center gap-2">
                        <span className="bg-sky-100 text-sky-800 text-[10px] font-bold px-2 py-0.5 rounded">Form 15CB Mandated</span>
                        <h4 className="font-bold text-slate-900 text-sm">{a.remittanceReference} — {a.beneficiaryName} ({a.beneficiaryCountry})</h4>
                      </div>
                      <p className="text-xs text-slate-500 mt-0.5">Remitter: {a.remitterName} ({a.remitterPan}) • Purpose: {a.purposeDescription}</p>
                    </div>
                    <div className="flex items-center gap-2">
                      {a.status === 'Certified' || a.status === 'Filed' ? (
                        <div className="text-right">
                          <span className="bg-emerald-100 text-emerald-800 text-xs px-2.5 py-1 rounded-full font-bold">
                            ✅ Certified by {a.caName}
                          </span>
                          <span className="text-[10px] text-slate-400 block font-mono mt-0.5">UDIN: {a.udin}</span>
                        </div>
                      ) : (
                        <button
                          onClick={() => {
                            setActiveItem(a)
                            setShowCertifyModal(true)
                          }}
                          className="bg-sky-600 hover:bg-sky-500 text-white px-3.5 py-1.5 rounded-lg text-xs font-semibold shadow-sm"
                        >
                          Sign &amp; Issue 15CB
                        </button>
                      )}
                    </div>
                  </div>

                  <div className="mt-3 grid grid-cols-2 sm:grid-cols-4 gap-3 text-xs">
                    <div>
                      <span className="text-slate-400 block">Foreign Consideration:</span>
                      <strong className="text-slate-800 font-mono text-sm">{a.foreignAmount.toLocaleString()} {a.remittanceCurrency}</strong>
                    </div>
                    <div>
                      <span className="text-slate-400 block">INR Equivalent:</span>
                      <strong className="text-slate-800 font-mono text-sm">{formatINR(a.inrEquivalent)}</strong>
                    </div>
                    <div>
                      <span className="text-slate-400 block">TDS Deducted ({a.finalWithholdingRate}%):</span>
                      <strong className="text-sky-700 font-mono text-sm">{formatINR(a.tdsAmountInr)}</strong>
                    </div>
                    <div>
                      <span className="text-slate-400 block">Net Payable to Non-Resident:</span>
                      <strong className="text-emerald-700 font-mono text-sm">{a.netRemittanceFc.toLocaleString()} {a.remittanceCurrency}</strong>
                    </div>
                  </div>

                  {a.generated15CbCertificateText && (
                    <div className="mt-3 pt-3 border-t">
                      <details className="text-xs">
                        <summary className="cursor-pointer font-semibold text-sky-700 hover:text-sky-900">
                          View Generated Form 15CB Statutory Certificate Verbiage ▼
                        </summary>
                        <pre className="mt-2 p-3 bg-slate-900 text-slate-100 rounded-lg text-[11px] font-mono overflow-x-auto whitespace-pre-wrap">
                          {a.generated15CbCertificateText}
                        </pre>
                      </details>
                    </div>
                  )}
                </div>
              ))}
              {assessments.filter(a => a.requiresForm15Cb).length === 0 && (
                <div className="p-8 text-center text-slate-400 text-xs">No Form 15CB certificates required for the selected financial year.</div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* TAB 3: DTAA TREATY, MFN & PE EXPOSURE ANALYZER */}
      {deskTab === 'dtaa-mfn-pe' && (
        <div className="space-y-6">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            {/* MFN Clause Interactive Evaluator */}
            <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
              <div className="border-b pb-3">
                <h3 className="font-bold text-slate-900 text-sm flex items-center gap-2">
                  <span>⚖️</span> Most Favoured Nation (MFN) Treaty Clause Analyzer
                </h3>
                <p className="text-xs text-slate-500">
                  Tests treaty eligibility under Supreme Court ruling in <em>AO v. M/s Nestle SA (2023)</em>.
                </p>
              </div>

              <div className="space-y-3 text-xs">
                <div>
                  <label className="font-semibold text-slate-700">Tax Treaty Partner Country</label>
                  <select
                    value={mfnCountry}
                    onChange={e => setMfnCountry(e.target.value)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    <option value="Netherlands">Netherlands (MFN Protocol)</option>
                    <option value="France">France (MFN Protocol)</option>
                    <option value="Switzerland">Switzerland (MFN Protocol)</option>
                    <option value="Sweden">Sweden (MFN Protocol)</option>
                    <option value="Spain">Spain (MFN Protocol)</option>
                    <option value="Hungary">Hungary (MFN Protocol)</option>
                    <option value="United States">United States (No MFN)</option>
                    <option value="Singapore">Singapore (No MFN)</option>
                    <option value="United Kingdom">United Kingdom (No MFN)</option>
                  </select>
                </div>

                <div>
                  <label className="font-semibold text-slate-700">Base Treaty Tax Rate (%)</label>
                  <input
                    type="number"
                    value={mfnBaseRate}
                    onChange={e => setMfnBaseRate(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                  />
                </div>

                <button
                  type="button"
                  onClick={handleRunMfnEval}
                  className="w-full py-2 bg-sky-600 hover:bg-sky-500 text-white font-semibold rounded-lg shadow-sm"
                >
                  Evaluate MFN Benefit &amp; Nestle Ratio →
                </button>

                {mfnResult && (
                  <div className={`p-4 rounded-xl border mt-3 space-y-2 ${mfnResult.hasMfnClause ? 'bg-amber-50 border-amber-300 text-amber-950' : 'bg-slate-50 border-slate-200 text-slate-800'}`}>
                    <div className="flex items-center justify-between font-bold">
                      <span>MFN Protocol Status:</span>
                      <span>{mfnResult.hasMfnClause ? '✅ MFN Protocol Present' : '❌ No MFN Protocol'}</span>
                    </div>
                    {mfnResult.hasMfnClause && (
                      <div className="text-[11px] space-y-1">
                        <div>Effective MFN Rate: <strong className="font-mono">{mfnResult.mfnRate}%</strong></div>
                        <div className="text-rose-700 font-semibold">⚠️ Supreme Court Mandate: Separate CBDT notification under Section 90(1) required!</div>
                      </div>
                    )}
                    <p className="text-[11px] leading-relaxed text-slate-700 bg-white/70 p-2 rounded">
                      {mfnResult.guidanceNote}
                    </p>
                  </div>
                )}
              </div>
            </div>

            {/* Permanent Establishment (PE) Risk Scorecard */}
            <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
              <div className="border-b pb-3">
                <h3 className="font-bold text-slate-900 text-sm flex items-center gap-2">
                  <span>🏢</span> Article 5 Permanent Establishment (PE) Exposure Test
                </h3>
                <p className="text-xs text-slate-500">
                  Assess Fixed Place PE, Service PE, and Dependent Agent PE (DAPE) exposure before remittance.
                </p>
              </div>

              <div className="space-y-3 text-xs">
                <div>
                  <label className="font-semibold text-slate-700">Treaty Country</label>
                  <select
                    value={peCountry}
                    onChange={e => setPeCountry(e.target.value)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    <option value="United States">United States (182-day Service PE)</option>
                    <option value="United Kingdom">United Kingdom (182-day Service PE)</option>
                    <option value="Singapore">Singapore (90-day Service PE)</option>
                    <option value="Germany">Germany (90-day Service PE)</option>
                    <option value="Japan">Japan (90-day Service PE)</option>
                  </select>
                </div>

                <div className="space-y-2 pt-1">
                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={peOffice}
                      onChange={e => setPeOffice(e.target.checked)}
                      className="rounded text-sky-600"
                    />
                    <span className="font-medium text-slate-700">Non-resident maintains fixed office / branch in India</span>
                  </label>

                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={peAuthority}
                      onChange={e => setPeAuthority(e.target.checked)}
                      className="rounded text-sky-600"
                    />
                    <span className="font-medium text-slate-700">Agent in India habitually concludes contracts on behalf of non-resident (DAPE)</span>
                  </label>

                  <label className="flex items-center gap-2 cursor-pointer">
                    <input
                      type="checkbox"
                      checked={peInventory}
                      onChange={e => setPeInventory(e.target.checked)}
                      className="rounded text-sky-600"
                    />
                    <span className="font-medium text-slate-700">Agent in India maintains regular inventory stock for deliveries</span>
                  </label>
                </div>

                <div>
                  <label className="font-semibold text-slate-700">Personnel Aggregate Stay in India (Days)</label>
                  <input
                    type="number"
                    value={peDays}
                    onChange={e => setPeDays(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                  />
                </div>

                <button
                  type="button"
                  onClick={handleRunPeEval}
                  className="w-full py-2 bg-indigo-600 hover:bg-indigo-500 text-white font-semibold rounded-lg shadow-sm"
                >
                  Run Article 5 PE Test →
                </button>

                {peResult && (
                  <div className={`p-4 rounded-xl border mt-3 space-y-2 ${peResult.isPeExposed ? 'bg-rose-50 border-rose-300 text-rose-950' : 'bg-emerald-50 border-emerald-300 text-emerald-950'}`}>
                    <div className="flex items-center justify-between font-bold">
                      <span>PE Exposure Outcome:</span>
                      <span>{peResult.isPeExposed ? `🚨 PE Exposed (${peResult.peType})` : '✅ No PE Exposure'}</span>
                    </div>
                    <p className="text-[11px] leading-relaxed bg-white/70 p-2 rounded">
                      {peResult.assessmentSummary}
                    </p>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* TAB 4: RBI FIRMS (FC-GPR / FC-TRS) PORTAL */}
      {deskTab === 'rbi-firms' && (
        <div className="space-y-6">
          {/* Statutory Banner */}
          <div className="bg-indigo-50 border border-indigo-200 rounded-xl p-4 text-xs text-indigo-950 flex items-start gap-3 shadow-sm">
            <span className="text-xl">🏦</span>
            <div className="space-y-1">
              <strong className="font-bold text-sm block">RBI FIRMS Single Master Form (SMF) Statutory Reporting Engine</strong>
              <p>
                Governed by the <strong>Foreign Exchange Management (Non-debt Instruments) Rules, 2019</strong>.
                Mandatory electronic reporting on the RBI FIRMS portal through the Authorized Dealer (AD) Bank:
              </p>
              <div className="flex gap-4 font-mono text-[11px] pt-1">
                <span>• <strong>Form FC-GPR:</strong> Allotment of capital instruments within <strong>30 days</strong></span>
                <span>• <strong>Form FC-TRS:</strong> Transfer of capital instruments within <strong>60 days</strong></span>
                <span>• <strong>Rule 21:</strong> Issue/Transfer price must adhere to certified DCF floor/ceiling</span>
              </div>
            </div>
          </div>

          {/* FIRMS Summary KPIs */}
          {firmsSummary && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Total FIRMS Filings</span>
                <div className="text-xl font-bold font-mono text-slate-900 mt-1">{firmsSummary.totalFilings} Records</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">{firmsSummary.fcGprCount} FC-GPR • {firmsSummary.fcTrsCount} FC-TRS</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Total Foreign Inflow / Outflow</span>
                <div className="text-xl font-bold font-mono text-indigo-700 mt-1">{formatINR(firmsSummary.totalConsiderationInr)}</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">Capital Instruments</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Late Submissions Alert</span>
                <div className={`text-xl font-bold font-mono mt-1 ${firmsSummary.lateSubmissionsCount > 0 ? 'text-rose-600' : 'text-emerald-700'}`}>
                  {firmsSummary.lateSubmissionsCount} Delayed
                </div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">Attracts RBI LSF Fees</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Pricing Guidelines</span>
                <div className="text-xl font-bold font-mono text-emerald-700 mt-1">100% Compliant</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">Rule 21 DCF Floor</span>
              </div>
            </div>
          )}

          {/* FIRMS Filings Ledger */}
          <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b pb-3">
              <div className="flex items-center gap-3">
                <h3 className="font-bold text-slate-900 text-sm">FEMA FIRMS Allotment &amp; Share Transfer Filings</h3>
                <select
                  value={selectedFirmsType}
                  onChange={e => {
                    const t = e.target.value as FirmsFilingType | ''
                    setSelectedFirmsType(t)
                    foreignRemittanceApi.getFirmsFilings(undefined, t || undefined).then(setFirmsFilings).catch(() => {})
                  }}
                  className="p-1.5 border rounded text-xs bg-slate-50"
                >
                  <option value="">All FIRMS Forms</option>
                  <option value="FcGpr">Form FC-GPR (Allotment)</option>
                  <option value="FcTrs">Form FC-TRS (Transfer)</option>
                </select>
              </div>
              <button
                onClick={() => setShowFirmsModal(true)}
                className="bg-indigo-600 hover:bg-indigo-500 text-white font-medium text-xs px-3 py-1.5 rounded-lg shadow-sm flex items-center gap-1"
              >
                <span>+</span> New FIRMS Filing
              </button>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-xs text-left">
                <thead className="bg-slate-50 text-slate-600 font-semibold border-b">
                  <tr>
                    <th className="py-2.5 px-3">Form / Reference</th>
                    <th className="py-2.5 px-3">Indian Entity</th>
                    <th className="py-2.5 px-3">Non-Resident Counterparty</th>
                    <th className="py-2.5 px-3">Instruments</th>
                    <th className="py-2.5 px-3">Price vs Fair Value</th>
                    <th className="py-2.5 px-3">Total Consideration</th>
                    <th className="py-2.5 px-3">Deadline Status</th>
                    <th className="py-2.5 px-3">Certification</th>
                    <th className="py-2.5 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {firmsFilings.map(f => (
                    <tr key={f.id} className="hover:bg-slate-50">
                      <td className="py-2.5 px-3 font-semibold text-slate-900">
                        <span className={`px-2 py-0.5 rounded text-[10px] font-bold mr-1 ${f.filingType === 'FcGpr' ? 'bg-indigo-100 text-indigo-800' : 'bg-purple-100 text-purple-800'}`}>
                          {f.filingType === 'FcGpr' ? 'FC-GPR' : 'FC-TRS'}
                        </span>
                        {f.referenceNumber}
                      </td>
                      <td className="py-2.5 px-3 text-slate-800">
                        {f.indianEntityName}
                        <span className="text-[10px] text-slate-400 block font-mono">{f.indianEntityCin}</span>
                      </td>
                      <td className="py-2.5 px-3 text-slate-700">
                        {f.nonResidentPartyName}
                        <span className="text-[10px] text-slate-500 block">{f.nonResidentCountry}</span>
                      </td>
                      <td className="py-2.5 px-3 font-mono">
                        {f.numberOfInstruments.toLocaleString()} {f.instrumentType}
                        <span className="text-[10px] text-slate-400 block">FV: ₹{f.faceValuePerInstrument}</span>
                      </td>
                      <td className="py-2.5 px-3 font-mono">
                        <span className={f.pricingGuidelinesMet ? 'text-emerald-700 font-bold' : 'text-rose-600 font-bold'}>
                          ₹{f.issueOrTransferPricePerInstrument.toFixed(2)} / ₹{f.fairValuePerInstrument.toFixed(2)}
                        </span>
                        <span className="text-[10px] text-slate-400 block">{f.valuationMethod}</span>
                      </td>
                      <td className="py-2.5 px-3 font-mono font-bold text-slate-900">
                        {formatINR(f.totalConsiderationInr)}
                        <span className="text-[10px] text-slate-400 block font-normal">{f.totalConsiderationFc.toLocaleString()} {f.currency}</span>
                      </td>
                      <td className="py-2.5 px-3">
                        {f.isLateSubmission ? (
                          <span className="bg-rose-100 text-rose-800 text-[10px] px-2 py-0.5 rounded font-bold">
                            ⚠️ Delayed ({f.daysElapsed}d) • LSF: {formatINR(f.lateSubmissionFeeEstimated)}
                          </span>
                        ) : (
                          <span className="bg-emerald-100 text-emerald-800 text-[10px] px-2 py-0.5 rounded font-medium">
                            {f.daysRemaining}d runway
                          </span>
                        )}
                      </td>
                      <td className="py-2.5 px-3">
                        {f.status === 'Certified' ? (
                          <span className="bg-emerald-100 text-emerald-800 text-[10px] px-2 py-0.5 rounded font-bold">
                            ✅ Certified (UDIN: {f.udin})
                          </span>
                        ) : (
                          <button
                            onClick={() => {
                              setCertifyingFirmsId(f.id)
                              setShowCertifyFirmsModal(true)
                            }}
                            className="text-indigo-600 hover:text-indigo-800 underline font-semibold text-[11px]"
                          >
                            Sign Certificate
                          </button>
                        )}
                      </td>
                      <td className="py-2.5 px-3 text-right">
                        <div className="flex items-center justify-end gap-1">
                          <button
                            onClick={() => handleOpenFirmsCert(f.id)}
                            className="bg-slate-100 hover:bg-slate-200 text-slate-700 px-2 py-1 rounded text-xs"
                            title="Preview Certificate HTML"
                          >
                            👁️
                          </button>
                          <button
                            onClick={() => handleDeleteFirms(f.id)}
                            className="text-rose-600 hover:text-rose-800 text-xs px-1"
                            title="Delete"
                          >
                            ✕
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {firmsFilings.length === 0 && (
                    <tr>
                      <td colSpan={9} className="py-6 text-center text-slate-400">No FEMA FIRMS filings recorded yet.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 5: RBI ANNUAL FLA RETURN (FLAIR PORTAL) */}
      {deskTab === 'rbi-fla' && (
        <div className="space-y-6">
          {/* Statutory Banner */}
          <div className="bg-purple-50 border border-purple-200 rounded-xl p-4 text-xs text-purple-950 flex items-start gap-3 shadow-sm">
            <span className="text-xl">📊</span>
            <div className="space-y-1">
              <strong className="font-bold text-sm block">RBI Annual Return on Foreign Liabilities and Assets (FLA Return)</strong>
              <p>
                Mandatory annual statutory return under <strong>FEMA Regulations, 1999</strong> for any Indian company having received Foreign Direct Investment (FDI)
                or made Overseas Direct Investment (ODI). Must be filed on the <strong>RBI FLAIR portal by July 15th</strong> of each year based on audited/provisional accounts.
              </p>
            </div>
          </div>

          {/* FLA Summary KPIs */}
          {flaSummary && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Total Returns Registered</span>
                <div className="text-xl font-bold font-mono text-slate-900 mt-1">{flaSummary.totalReturns} Entities</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">{flaSummary.mandatoryReturnsCount} Mandatory Filings</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Total Foreign Direct Investment (NAV)</span>
                <div className="text-xl font-bold font-mono text-emerald-700 mt-1">{formatINR(flaSummary.totalFdiNetAssetValueInr)}</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">Net Worth Proportion</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Total ODI Commitment Outside India</span>
                <div className="text-xl font-bold font-mono text-purple-700 mt-1">{formatINR(flaSummary.totalOdiFinancialCommitmentInr)}</div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">JV / WOS Assets</span>
              </div>
              <div className="bg-white rounded-xl p-4 shadow-sm border border-slate-200">
                <span className="text-slate-500 text-xs">Statutory July 15 Deadline</span>
                <div className={`text-xl font-bold font-mono mt-1 ${flaSummary.overdueCount > 0 ? 'text-rose-600' : 'text-slate-800'}`}>
                  {flaSummary.overdueCount > 0 ? `🚨 ${flaSummary.overdueCount} Overdue` : '✅ Within Time'}
                </div>
                <span className="text-[11px] text-slate-400 mt-0.5 block">FEMA Compounding Risk</span>
              </div>
            </div>
          )}

          {/* FLA Returns Table */}
          <div className="bg-white rounded-xl p-5 shadow-sm border border-slate-200 space-y-4">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b pb-3">
              <h3 className="font-bold text-slate-900 text-sm">Annual FLA Returns by Entity</h3>
              <button
                onClick={() => setShowFlaModal(true)}
                className="bg-emerald-600 hover:bg-emerald-500 text-white font-medium text-xs px-3 py-1.5 rounded-lg shadow-sm flex items-center gap-1"
              >
                <span>+</span> Compile Annual FLA Return
              </button>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-xs text-left">
                <thead className="bg-slate-50 text-slate-600 font-semibold border-b">
                  <tr>
                    <th className="py-2.5 px-3">Company Name / CIN</th>
                    <th className="py-2.5 px-3">Financial Year</th>
                    <th className="py-2.5 px-3">Net Worth</th>
                    <th className="py-2.5 px-3">Foreign Equity %</th>
                    <th className="py-2.5 px-3">FDI at NAV</th>
                    <th className="py-2.5 px-3">ODI Commitment</th>
                    <th className="py-2.5 px-3">July 15 Deadline Status</th>
                    <th className="py-2.5 px-3">Filing Status</th>
                    <th className="py-2.5 px-3 text-right">Action</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {flaReturns.map(r => (
                    <tr key={r.id} className="hover:bg-slate-50">
                      <td className="py-2.5 px-3 font-semibold text-slate-900">
                        {r.companyName}
                        <span className="text-[10px] text-slate-400 block font-mono">{r.companyCin}</span>
                      </td>
                      <td className="py-2.5 px-3 font-medium text-slate-700">{r.financialYear}</td>
                      <td className="py-2.5 px-3 font-mono font-medium text-slate-800">{formatINR(r.netWorthInr)}</td>
                      <td className="py-2.5 px-3 font-mono font-bold text-sky-800">{r.foreignEquityHoldingPct}%</td>
                      <td className="py-2.5 px-3 font-mono font-bold text-emerald-700">{formatINR(r.fdiAtNetAssetValueInr)}</td>
                      <td className="py-2.5 px-3 font-mono text-purple-700">{formatINR(r.totalOdiFinancialCommitmentInr)}</td>
                      <td className="py-2.5 px-3">
                        {r.isDeadlineBreached ? (
                          <span className="bg-rose-100 text-rose-800 text-[10px] px-2 py-0.5 rounded font-bold">
                            🚨 Overdue
                          </span>
                        ) : (
                          <span className="bg-emerald-100 text-emerald-800 text-[10px] px-2 py-0.5 rounded font-medium">
                            {r.daysRemainingToDeadline}d remaining
                          </span>
                        )}
                      </td>
                      <td className="py-2.5 px-3">
                        {r.status === 'SubmittedToRbiFlair' ? (
                          <span className="bg-emerald-100 text-emerald-800 text-[10px] px-2 py-0.5 rounded font-bold">
                            ✅ Filed ({r.rbiFlairAckNumber})
                          </span>
                        ) : r.status === 'Certified' ? (
                          <div className="flex items-center gap-1">
                            <span className="bg-indigo-100 text-indigo-800 text-[10px] px-2 py-0.5 rounded font-bold">Certified</span>
                            <button
                              onClick={() => {
                                setFilingFlaId(r.id)
                                setShowFileFlaModal(true)
                              }}
                              className="text-xs text-sky-700 underline font-semibold"
                            >
                              Record Ack
                            </button>
                          </div>
                        ) : (
                          <button
                            onClick={() => {
                              setCertifyingFlaId(r.id)
                              setShowCertifyFlaModal(true)
                            }}
                            className="text-indigo-600 hover:text-indigo-800 underline font-semibold text-[11px]"
                          >
                            Sign &amp; Certify
                          </button>
                        )}
                      </td>
                      <td className="py-2.5 px-3 text-right">
                        <div className="flex items-center justify-end gap-1">
                          <button
                            onClick={() => handleOpenFlaReport(r.id)}
                            className="bg-slate-100 hover:bg-slate-200 text-slate-700 px-2 py-1 rounded text-xs"
                            title="Preview Full FLA Dossier"
                          >
                            👁️
                          </button>
                          <button
                            onClick={() => handleDeleteFla(r.id)}
                            className="text-rose-600 hover:text-rose-800 text-xs px-1"
                            title="Delete"
                          >
                            ✕
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {flaReturns.length === 0 && (
                    <tr>
                      <td colSpan={9} className="py-6 text-center text-slate-400">No Annual FLA returns recorded yet.</td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* DRAWER FOR REMITTANCE DETAILS (15CA/15CB) */}
      {activeItem && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex justify-end">
          <div className="bg-white w-full max-w-2xl h-full shadow-2xl flex flex-col overflow-hidden">
            <div className="p-4 bg-slate-900 text-white flex justify-between items-center">
              <div>
                <h3 className="font-bold text-sm">{activeItem.remittanceReference} — {activeItem.beneficiaryName}</h3>
                <p className="text-xs text-slate-400">{activeItem.beneficiaryCountry} • {activeItem.foreignAmount} {activeItem.remittanceCurrency} ({formatINR(activeItem.inrEquivalent)})</p>
              </div>
              <button onClick={() => setActiveItem(null)} className="text-slate-400 hover:text-white font-bold text-xl px-2">×</button>
            </div>

            {/* Drawer Tabs */}
            <div className="flex border-b border-slate-200 text-xs font-semibold bg-slate-50">
              <button
                onClick={() => setDrawerTab('overview')}
                className={`flex-1 py-2.5 border-b-2 ${drawerTab === 'overview' ? 'border-sky-600 text-sky-700 bg-white' : 'border-transparent text-slate-500'}`}
              >
                Overview &amp; Taxability
              </button>
              <button
                onClick={() => setDrawerTab('checklist')}
                className={`flex-1 py-2.5 border-b-2 ${drawerTab === 'checklist' ? 'border-sky-600 text-sky-700 bg-white' : 'border-transparent text-slate-500'}`}
              >
                Document Checklist
              </button>
              <button
                onClick={() => setDrawerTab('form15cb')}
                className={`flex-1 py-2.5 border-b-2 ${drawerTab === 'form15cb' ? 'border-sky-600 text-sky-700 bg-white' : 'border-transparent text-slate-500'}`}
              >
                Form 15CB
              </button>
              <button
                onClick={() => setDrawerTab('form15ca')}
                className={`flex-1 py-2.5 border-b-2 ${drawerTab === 'form15ca' ? 'border-sky-600 text-sky-700 bg-white' : 'border-transparent text-slate-500'}`}
              >
                Form 15CA XML
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-4 space-y-4 text-xs">
              {drawerTab === 'overview' && (
                <div className="space-y-4">
                  <div className="bg-sky-50 border border-sky-200 rounded-lg p-3 space-y-1 text-sky-950">
                    <strong className="block font-bold">Rule 37BB Form 15CA Routing: {activeItem.form15CaPart}</strong>
                    <p>{activeItem.form15CaRoutingReason}</p>
                  </div>

                  <div className="grid grid-cols-2 gap-3">
                    <div className="bg-slate-50 p-3 rounded-lg border">
                      <span className="text-slate-400 block font-medium">Domestic Tax Rate (Sec 195):</span>
                      <strong className="text-slate-800 text-sm">{activeItem.effectiveDomesticRate}%</strong>
                      <span className="text-[10px] text-slate-500 block">Base: {activeItem.domesticTaxRate}% + Cess: {activeItem.cessRate}%</span>
                    </div>
                    <div className="bg-slate-50 p-3 rounded-lg border">
                      <span className="text-slate-400 block font-medium">DTAA Treaty Rate (Sec 90):</span>
                      <strong className="text-slate-800 text-sm">{activeItem.treatyTaxRate}%</strong>
                      <span className="text-[10px] text-slate-500 block">{activeItem.treatyCountry} ({activeItem.treatyArticle})</span>
                    </div>
                  </div>

                  <div className="border rounded-lg p-3 space-y-2 bg-slate-50">
                    <span className="font-bold text-slate-800 block">Governing Tax &amp; TDS Calculations</span>
                    <div className="grid grid-cols-2 gap-2">
                      <div>Final Withholding Rate: <strong className="font-mono text-sky-700">{activeItem.finalWithholdingRate}% ({activeItem.applicableRateType})</strong></div>
                      <div>Total TDS (INR): <strong className="font-mono text-slate-900">{formatINR(activeItem.tdsAmountInr)}</strong></div>
                      <div>Net Remittance (FC): <strong className="font-mono">{activeItem.netRemittanceFc.toLocaleString()} {activeItem.remittanceCurrency}</strong></div>
                      <div>Grossed-up Amount: <strong className="font-mono">{formatINR(activeItem.grossedUpAmountInr)}</strong></div>
                    </div>
                  </div>

                  <div className="border rounded-lg p-3 space-y-2 bg-slate-50">
                    <span className="font-bold text-slate-800 block">DTAA Eligibility Prerequisites</span>
                    <div className="grid grid-cols-2 gap-2">
                      <div>Tax Residency Certificate (TRC): <strong>{activeItem.hasTrc ? '✅ Verified' : '❌ Missing'}</strong></div>
                      <div>Form 10F Electronic: <strong>{activeItem.hasForm10F ? '✅ Verified' : '❌ Missing'}</strong></div>
                      <div>No-PE Declaration: <strong>{activeItem.noPeDeclaration ? '✅ Verified' : '❌ Missing'}</strong></div>
                      <div>Beneficial Ownership: <strong>{activeItem.beneficialOwnershipConfirmed ? '✅ Confirmed' : '❌ Missing'}</strong></div>
                    </div>
                  </div>
                </div>
              )}

              {drawerTab === 'checklist' && (
                <div className="space-y-3">
                  <h4 className="font-bold text-slate-800">Supporting Verification Checklist</h4>
                  {activeItem.checklist?.map(c => (
                    <div key={c.id} className="border rounded-lg p-3 flex justify-between items-center bg-slate-50">
                      <div>
                        <strong className="block text-slate-900">{c.itemCode} — {c.itemDescription}</strong>
                        <span className="text-[10px] text-slate-500">Status: {c.status} • {c.isMandatory ? 'Mandatory' : 'Optional'}</span>
                      </div>
                      <span className={`text-[10px] px-2 py-0.5 rounded font-bold ${c.status === 'Verified' ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'}`}>
                        {c.status}
                      </span>
                    </div>
                  ))}
                </div>
              )}

              {drawerTab === 'form15cb' && (
                <div className="space-y-3">
                  <div className="flex justify-between items-center">
                    <h4 className="font-bold text-slate-800">Official Form 15CB Statutory Certificate</h4>
                    {activeItem.status !== 'Certified' && activeItem.requiresForm15Cb && (
                      <button
                        onClick={() => setShowCertifyModal(true)}
                        className="bg-sky-600 hover:bg-sky-500 text-white px-3 py-1 rounded text-xs font-semibold shadow-sm"
                      >
                        Sign &amp; Certify Certificate
                      </button>
                    )}
                  </div>
                  <pre className="p-4 bg-slate-900 text-slate-100 rounded-lg text-[11px] font-mono overflow-x-auto whitespace-pre-wrap leading-relaxed">
                    {activeItem.generated15CbCertificateText || 'Certificate not yet generated.'}
                  </pre>
                </div>
              )}

              {drawerTab === 'form15ca' && (
                <div className="space-y-3">
                  <h4 className="font-bold text-slate-800">Form 15CA E-Filing XML Payload</h4>
                  <pre className="p-4 bg-slate-900 text-slate-100 rounded-lg text-[11px] font-mono overflow-x-auto whitespace-pre-wrap leading-relaxed">
                    {activeItem.generated15CaXmlPayload || 'XML not yet generated.'}
                  </pre>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* MODAL: CREATE REMITTANCE */}
      {showCreateModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto text-xs">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">New Foreign Remittance Assessment</h3>
              <button onClick={() => setShowCreateModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleCreateRemittance} className="space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Client / Payer Entity</label>
                  <select
                    value={createReq.clientId || ''}
                    onChange={e => {
                      const id = e.target.value
                      const clt = clients.find(c => c.id === id)
                      setCreateReq(prev => ({
                        ...prev,
                        clientId: id,
                        remitterName: clt?.name || '',
                        remitterPan: clt?.taxIds?.PAN || 'AABCZ1234F'
                      }))
                    }}
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
                  <label className="font-semibold text-slate-700">Remittance Reference</label>
                  <input
                    type="text"
                    value={createReq.remittanceReference || ''}
                    onChange={e => setCreateReq(prev => ({ ...prev, remittanceReference: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    placeholder="REM-2025-001"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Beneficiary Name</label>
                  <input
                    type="text"
                    value={createReq.beneficiaryName || ''}
                    onChange={e => setCreateReq(prev => ({ ...prev, beneficiaryName: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="Zenith LLC"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Beneficiary Country</label>
                  <input
                    type="text"
                    value={createReq.beneficiaryCountry || ''}
                    onChange={e => setCreateReq(prev => ({ ...prev, beneficiaryCountry: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="United States"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Country Code (2-digit)</label>
                  <input
                    type="text"
                    value={createReq.beneficiaryCountryCode || ''}
                    onChange={e => setCreateReq(prev => ({ ...prev, beneficiaryCountryCode: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded font-mono uppercase"
                    placeholder="US"
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Foreign Amount</label>
                  <input
                    type="number"
                    step="any"
                    value={createReq.foreignAmount || ''}
                    onChange={e => setCreateReq(prev => ({ ...prev, foreignAmount: Number(e.target.value) }))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Currency</label>
                  <select
                    value={createReq.remittanceCurrency || 'USD'}
                    onChange={e => setCreateReq(prev => ({ ...prev, remittanceCurrency: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    {STANDARD_CURRENCIES.map(c => <option key={c} value={c}>{c}</option>)}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Exchange Rate (₹)</label>
                  <input
                    type="number"
                    step="any"
                    value={createReq.exchangeRate || 85}
                    onChange={e => setCreateReq(prev => ({ ...prev, exchangeRate: Number(e.target.value) }))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">RBI Purpose Code</label>
                  <select
                    value={createReq.rbiPurposeCode || 'P0802'}
                    onChange={e => {
                      const code = e.target.value
                      const p = RBI_PURPOSE_CODES.find(x => x.code === code)
                      setCreateReq(prev => ({ ...prev, rbiPurposeCode: code, purposeDescription: p?.desc || '' }))
                    }}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    {RBI_PURPOSE_CODES.map(p => (
                      <option key={p.code} value={p.code}>{p.code} - {p.desc}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Nature of Remittance</label>
                  <select
                    value={createReq.natureOfRemittance || 'FeesForTechnicalServices'}
                    onChange={e => setCreateReq(prev => ({ ...prev, natureOfRemittance: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    <option value="FeesForTechnicalServices">Fees for Technical Services (FTS)</option>
                    <option value="Royalty">Royalty</option>
                    <option value="ProfessionalServices">Professional Services</option>
                    <option value="Interest">Interest</option>
                    <option value="Dividend">Dividend</option>
                    <option value="PurchaseOfGoods">Purchase of Goods (Commercial)</option>
                  </select>
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded font-medium shadow-sm"
                >
                  Save Remittance Assessment
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: CERTIFY 15CB */}
      {showCertifyModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 text-xs">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Sign &amp; Issue Form 15CB Certificate</h3>
              <button onClick={() => setShowCertifyModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleCertifyRemittance} className="space-y-3">
              <div>
                <label className="font-semibold text-slate-700">Chartered Accountant Name</label>
                <input
                  type="text"
                  value={certifyForm.caName}
                  onChange={e => setCertifyForm(prev => ({ ...prev, caName: e.target.value }))}
                  className="w-full mt-1 p-2 border rounded"
                  required
                />
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className="font-semibold text-slate-700">ICAI Membership No.</label>
                  <input
                    type="text"
                    value={certifyForm.caMembershipNumber}
                    onChange={e => setCertifyForm(prev => ({ ...prev, caMembershipNumber: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Firm Reg. No. (FRN)</label>
                  <input
                    type="text"
                    value={certifyForm.caFrn}
                    onChange={e => setCertifyForm(prev => ({ ...prev, caFrn: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                  />
                </div>
              </div>
              <div>
                <label className="font-semibold text-slate-700">Unique Document ID (UDIN - 18 Chars)</label>
                <input
                  type="text"
                  value={certifyForm.udin}
                  onChange={e => setCertifyForm(prev => ({ ...prev, udin: e.target.value }))}
                  className="w-full mt-1 p-2 border rounded font-mono uppercase font-bold text-sky-800"
                  required
                />
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded font-medium shadow-sm"
                >
                  Attest &amp; Bind UDIN
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: NEW FIRMS FILING (FC-GPR / FC-TRS) */}
      {showFirmsModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto text-xs">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">New RBI FIRMS Single Master Form Filing</h3>
              <button onClick={() => setShowFirmsModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSaveFirms} className="space-y-4">
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Filing Type</label>
                  <select
                    value={firmsType}
                    onChange={e => setFirmsType(e.target.value as FirmsFilingType)}
                    className="w-full mt-1 p-2 border rounded bg-white font-bold"
                  >
                    <option value="FcGpr">Form FC-GPR (Allotment of Shares)</option>
                    <option value="FcTrs">Form FC-TRS (Transfer of Shares)</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Reference Number</label>
                  <input
                    type="text"
                    value={firmsRefNo}
                    onChange={e => setFirmsRefNo(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Client Account</label>
                  <select
                    value={firmsClientId}
                    onChange={e => setFirmsClientId(e.target.value)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                    required
                  >
                    <option value="">Select client...</option>
                    {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Indian Entity Name</label>
                  <input
                    type="text"
                    value={firmsEntityName}
                    onChange={e => setFirmsEntityName(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">CIN</label>
                  <input
                    type="text"
                    value={firmsCin}
                    onChange={e => setFirmsCin(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono uppercase"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">PAN</label>
                  <input
                    type="text"
                    value={firmsPan}
                    onChange={e => setFirmsPan(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono uppercase"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Non-Resident Counterparty</label>
                  <input
                    type="text"
                    value={firmsNrName}
                    onChange={e => setFirmsNrName(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="Global Ventures LLC"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Country of Non-Resident</label>
                  <input
                    type="text"
                    value={firmsNrCountry}
                    onChange={e => setFirmsNrCountry(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    placeholder="United States"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-4 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Instrument</label>
                  <select
                    value={firmsInstrumentType}
                    onChange={e => setFirmsInstrumentType(e.target.value as CapitalInstrumentType)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    <option value="EquityShares">Equity Shares</option>
                    <option value="CCDs">CCDs (Debentures)</option>
                    <option value="CCPS">CCPS (Preference)</option>
                    <option value="ShareWarrants">Share Warrants</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Number of Units</label>
                  <input
                    type="number"
                    value={firmsCount}
                    onChange={e => setFirmsCount(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Issue/Transfer Price (₹)</label>
                  <input
                    type="number"
                    step="any"
                    value={firmsIssuePrice}
                    onChange={e => setFirmsIssuePrice(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono font-bold text-sky-800"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">DCF Fair Value Floor (₹)</label>
                  <input
                    type="number"
                    step="any"
                    value={firmsFairValue}
                    onChange={e => setFirmsFairValue(Number(e.target.value))}
                    className="w-full mt-1 p-2 border rounded font-mono font-bold text-slate-800"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Date of Allotment / Transfer</label>
                  <input
                    type="date"
                    value={firmsAllotmentDate}
                    onChange={e => setFirmsAllotmentDate(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Valuation Method</label>
                  <input
                    type="text"
                    value={firmsValMethod}
                    onChange={e => setFirmsValMethod(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Registered Valuer Name</label>
                  <input
                    type="text"
                    value={firmsValuer}
                    onChange={e => setFirmsValuer(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                  />
                </div>
              </div>

              {firmsType === 'FcTrs' && (
                <div className="grid grid-cols-2 gap-3 p-3 bg-purple-50 rounded-lg border border-purple-200">
                  <div>
                    <label className="font-semibold text-slate-700">Transfer Direction</label>
                    <select
                      value={firmsDirection}
                      onChange={e => setFirmsDirection(e.target.value as TransferDirectionType)}
                      className="w-full mt-1 p-2 border rounded bg-white"
                    >
                      <option value="ResidentToNonResident">Resident to Non-Resident (Floor: &ge; Fair Value)</option>
                      <option value="NonResidentToResident">Non-Resident to Resident (Ceiling: &le; Fair Value)</option>
                      <option value="NonResidentToNonResident">Non-Resident to Non-Resident (Exempt)</option>
                    </select>
                  </div>
                  <div>
                    <label className="font-semibold text-slate-700">Transfer Mode</label>
                    <select
                      value={firmsTransferMode}
                      onChange={e => setFirmsTransferMode(e.target.value as TransferModeType)}
                      className="w-full mt-1 p-2 border rounded bg-white"
                    >
                      <option value="Sale">Sale (Cash / AD Bank Wire)</option>
                      <option value="Gift">Gift (Prior RBI Approval)</option>
                      <option value="Swap">Swap of Shares</option>
                    </select>
                  </div>
                </div>
              )}

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowFirmsModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded font-medium shadow-sm"
                >
                  Save FIRMS Filing
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: CERTIFY FIRMS FILING */}
      {showCertifyFirmsModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 text-xs">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Certify FEMA FIRMS Compliance</h3>
              <button onClick={() => setShowCertifyFirmsModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleCertifyFirms} className="space-y-3">
              <div>
                <label className="font-semibold text-slate-700">Practitioner Name (CA / CS)</label>
                <input
                  type="text"
                  value={certifyForm.caName}
                  onChange={e => setCertifyForm(prev => ({ ...prev, caName: e.target.value }))}
                  className="w-full mt-1 p-2 border rounded"
                  required
                />
              </div>
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className="font-semibold text-slate-700">Membership Number</label>
                  <input
                    type="text"
                    value={certifyForm.caMembershipNumber}
                    onChange={e => setCertifyForm(prev => ({ ...prev, caMembershipNumber: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded font-mono"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Firm Name</label>
                  <input
                    type="text"
                    value={certifyForm.caFirmName}
                    onChange={e => setCertifyForm(prev => ({ ...prev, caFirmName: e.target.value }))}
                    className="w-full mt-1 p-2 border rounded"
                  />
                </div>
              </div>
              <div>
                <label className="font-semibold text-slate-700">Unique Document ID (UDIN)</label>
                <input
                  type="text"
                  value={certifyForm.udin}
                  onChange={e => setCertifyForm(prev => ({ ...prev, udin: e.target.value }))}
                  className="w-full mt-1 p-2 border rounded font-mono uppercase font-bold text-indigo-700"
                  required
                />
              </div>
              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyFirmsModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-500 text-white rounded font-medium shadow-sm"
                >
                  Certify FIRMS Filing
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: NEW ANNUAL FLA RETURN */}
      {showFlaModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl space-y-4 max-h-[90vh] overflow-y-auto text-xs">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">New RBI Annual FLA Return (FLAIR Portal)</h3>
              <button onClick={() => setShowFlaModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleSaveFla} className="space-y-4">
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Financial Year</label>
                  <select
                    value={flaFy}
                    onChange={e => setFlaFy(e.target.value)}
                    className="w-full mt-1 p-2 border rounded bg-white font-bold"
                  >
                    <option value="2024-25">FY 2024-25</option>
                    <option value="2025-26">FY 2025-26</option>
                    <option value="2023-24">FY 2023-24</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Client Account</label>
                  <select
                    value={flaClientId}
                    onChange={e => setFlaClientId(e.target.value)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                    required
                  >
                    <option value="">Select client...</option>
                    {clients.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-slate-700">Audit Status</label>
                  <select
                    value={flaAuditStatus}
                    onChange={e => setFlaAuditStatus(e.target.value as FlaAuditStatus)}
                    className="w-full mt-1 p-2 border rounded bg-white"
                  >
                    <option value="Audited">Audited Balance Sheet</option>
                    <option value="UnauditedProvisional">Unaudited / Provisional</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-slate-700">Company Name</label>
                  <input
                    type="text"
                    value={flaCompanyName}
                    onChange={e => setFlaCompanyName(e.target.value)}
                    className="w-full mt-1 p-2 border rounded"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">CIN</label>
                  <input
                    type="text"
                    value={flaCin}
                    onChange={e => setFlaCin(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono uppercase"
                    required
                  />
                </div>
                <div>
                  <label className="font-semibold text-slate-700">PAN</label>
                  <input
                    type="text"
                    value={flaPan}
                    onChange={e => setFlaPan(e.target.value)}
                    className="w-full mt-1 p-2 border rounded font-mono uppercase"
                    required
                  />
                </div>
              </div>

              <div className="border rounded-lg p-3 bg-slate-50 space-y-2">
                <span className="font-bold text-slate-800 block">Section I: Financial Particulars</span>
                <div className="grid grid-cols-3 gap-2">
                  <div>
                    <label className="text-slate-500 text-[11px]">Paid-up Capital (₹)</label>
                    <input
                      type="number"
                      value={flaPaidUpCapital}
                      onChange={e => setFlaPaidUpCapital(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                      required
                    />
                  </div>
                  <div>
                    <label className="text-slate-500 text-[11px]">Reserves &amp; Surplus (₹)</label>
                    <input
                      type="number"
                      value={flaReserves}
                      onChange={e => setFlaReserves(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                      required
                    />
                  </div>
                  <div>
                    <label className="text-slate-500 text-[11px]">Sales Turnover (₹)</label>
                    <input
                      type="number"
                      value={flaSalesTurnover}
                      onChange={e => setFlaSalesTurnover(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                      required
                    />
                  </div>
                </div>
              </div>

              <div className="border rounded-lg p-3 bg-slate-50 space-y-2">
                <span className="font-bold text-slate-800 block">Section II: Foreign Direct Investment (FDI in India)</span>
                <div className="grid grid-cols-3 gap-2">
                  <div>
                    <label className="text-slate-500 text-[11px]">Foreign Equity Holding (%)</label>
                    <input
                      type="number"
                      step="any"
                      value={flaForeignEquityPct}
                      onChange={e => setFlaForeignEquityPct(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white font-bold text-emerald-700"
                      required
                    />
                  </div>
                  <div>
                    <label className="text-slate-500 text-[11px]">FDI at Face Value (₹)</label>
                    <input
                      type="number"
                      value={flaFdiFaceValue}
                      onChange={e => setFlaFdiFaceValue(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                    />
                  </div>
                  <div>
                    <label className="text-slate-500 text-[11px]">Fresh FDI Inflows (₹)</label>
                    <input
                      type="number"
                      value={flaFreshFdi}
                      onChange={e => setFlaFreshFdi(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                    />
                  </div>
                </div>
              </div>

              <div className="border rounded-lg p-3 bg-slate-50 space-y-2">
                <span className="font-bold text-slate-800 block">Section III &amp; IV: Overseas Investment &amp; Other Foreign Liabilities</span>
                <div className="grid grid-cols-3 gap-2">
                  <div>
                    <label className="text-slate-500 text-[11px]">ODI Investment in WOS/JV (₹)</label>
                    <input
                      type="number"
                      value={flaOdiEquityInv}
                      onChange={e => setFlaOdiEquityInv(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white text-purple-700 font-bold"
                    />
                  </div>
                  <div>
                    <label className="text-slate-500 text-[11px]">ECB Outstanding (₹)</label>
                    <input
                      type="number"
                      value={flaEcb}
                      onChange={e => setFlaEcb(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                    />
                  </div>
                  <div>
                    <label className="text-slate-500 text-[11px]">Foreign Bank Balances (₹)</label>
                    <input
                      type="number"
                      value={flaBankBalances}
                      onChange={e => setFlaBankBalances(Number(e.target.value))}
                      className="w-full p-1.5 border rounded font-mono bg-white"
                    />
                  </div>
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowFlaModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-medium shadow-sm"
                >
                  Compile Annual FLA Return
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: CERTIFY ANNUAL FLA */}
      {showCertifyFlaModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 text-xs">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Certify Annual FLA Return</h3>
              <button onClick={() => setShowCertifyFlaModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleCertifyFla} className="space-y-3">
              <div>
                <label className="font-semibold text-slate-700">Certified by Chartered Accountant</label>
                <input
                  type="text"
                  value={certifyForm.caName}
                  onChange={e => setCertifyForm(prev => ({ ...prev, caName: e.target.value }))}
                  className="w-full mt-1 p-2 border rounded"
                  required
                />
              </div>
              <div>
                <label className="font-semibold text-slate-700">Unique Document ID (UDIN)</label>
                <input
                  type="text"
                  value={certifyForm.udin}
                  onChange={e => setCertifyForm(prev => ({ ...prev, udin: e.target.value }))}
                  className="w-full mt-1 p-2 border rounded font-mono uppercase font-bold text-emerald-800"
                  required
                />
              </div>
              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyFlaModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-medium shadow-sm"
                >
                  Attest &amp; Certify Return
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: RECORD FLA FILING ACK */}
      {showFileFlaModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4 text-xs">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-slate-900 text-base">Record RBI FLAIR Portal Acknowledgment</h3>
              <button onClick={() => setShowFileFlaModal(false)} className="text-slate-400 hover:text-slate-600 font-bold text-lg">×</button>
            </div>
            <form onSubmit={handleFileFla} className="space-y-3">
              <div>
                <label className="font-semibold text-slate-700">RBI FLAIR Portal Acknowledgment Reference Number</label>
                <input
                  type="text"
                  value={portalAck}
                  onChange={e => setPortalAck(e.target.value)}
                  className="w-full mt-1 p-2 border rounded font-mono uppercase font-bold text-slate-900"
                  placeholder="FLAIR/2024-25/104928"
                  required
                />
              </div>
              <div className="flex justify-end gap-2 pt-4 border-t">
                <button
                  type="button"
                  onClick={() => setShowFileFlaModal(false)}
                  className="px-4 py-2 border rounded text-slate-700 hover:bg-slate-50 font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  className="px-4 py-2 bg-sky-600 hover:bg-sky-500 text-white rounded font-medium shadow-sm"
                >
                  Save Acknowledgment
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL: FIRMS CERTIFICATE HTML VIEWER */}
      {showFirmsCertViewer && (
        <div className="fixed inset-0 bg-slate-900/70 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-4xl w-full h-[85vh] shadow-2xl flex flex-col overflow-hidden">
            <div className="p-4 bg-slate-900 text-white flex justify-between items-center">
              <h3 className="font-bold text-sm">FEMA FIRMS Statutory Compliance Certificate</h3>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => {
                    const win = window.open('', '_blank')
                    if (win) {
                      win.document.write(firmsCertHtml)
                      win.document.close()
                      win.focus()
                      win.print()
                    }
                  }}
                  className="bg-indigo-600 hover:bg-indigo-500 text-white px-3 py-1 rounded text-xs font-semibold"
                >
                  Print PDF
                </button>
                <button onClick={() => setShowFirmsCertViewer(false)} className="text-slate-400 hover:text-white font-bold text-xl px-2">×</button>
              </div>
            </div>
            <iframe title="FIRMS Cert Viewer" srcDoc={firmsCertHtml} className="w-full h-full bg-white" />
          </div>
        </div>
      )}

      {/* MODAL: FLA REPORT HTML VIEWER */}
      {showFlaReportViewer && (
        <div className="fixed inset-0 bg-slate-900/70 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-4xl w-full h-[85vh] shadow-2xl flex flex-col overflow-hidden">
            <div className="p-4 bg-slate-900 text-white flex justify-between items-center">
              <h3 className="font-bold text-sm">RBI Annual FLA Return Dossier (FLAIR)</h3>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => {
                    const win = window.open('', '_blank')
                    if (win) {
                      win.document.write(flaReportHtml)
                      win.document.close()
                      win.focus()
                      win.print()
                    }
                  }}
                  className="bg-emerald-600 hover:bg-emerald-500 text-white px-3 py-1 rounded text-xs font-semibold"
                >
                  Print PDF
                </button>
                <button onClick={() => setShowFlaReportViewer(false)} className="text-slate-400 hover:text-white font-bold text-xl px-2">×</button>
              </div>
            </div>
            <iframe title="FLA Report Viewer" srcDoc={flaReportHtml} className="w-full h-full bg-white" />
          </div>
        </div>
      )}
    </div>
  )
}
