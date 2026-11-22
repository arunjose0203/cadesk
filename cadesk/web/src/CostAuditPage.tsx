import React, { useEffect, useState } from 'react'
import { toError,
  costAuditApi,
  type CostAuditSummaryDto,
  type CostAuditEngagementDto,
  type CostRecordHeadDto,
  type CreateCostAuditEngagementRequest,
  type UpdateRecordHeadRequest,
  type CreateProductCostStatementRequest,
  type CreateReconciliationItemRequest,
  type CostAuditApplicabilityRequest,
  type CostAuditApplicabilityResult,
  type MaterialVarianceRequest,
  type MaterialVarianceResult,
  api,
} from './api'

type Client = {
  id: string
  name: string
  legalName?: string | null
}

type ClientPage = {
  items: Client[]
  total: number
}

export function CostAuditPage() {
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')
  const [selectedStatus, setSelectedStatus] = useState<string>('')

  const [summary, setSummary] = useState<CostAuditSummaryDto | null>(null)
  const [engagements, setEngagements] = useState<CostAuditEngagementDto[]>([])
  const [selectedEngagement, setSelectedEngagement] = useState<CostAuditEngagementDto | null>(null)
  const [activeTab, setActiveTab] = useState<'filings' | 'records' | 'costsheet' | 'reconciliation' | 'report'>('filings')

  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)
  const [successMsg, setSuccessMsg] = useState<string | null>(null)

  // Modals
  const [showNewEngModal, setShowNewEngModal] = useState<boolean>(false)
  const [showAppCheckModal, setShowAppCheckModal] = useState<boolean>(false)
  const [showVarianceModal, setShowVarianceModal] = useState<boolean>(false)
  const [showHeadModal, setShowHeadModal] = useState<boolean>(false)
  const [showProductModal, setShowProductModal] = useState<boolean>(false)
  const [showRecModal, setShowRecModal] = useState<boolean>(false)
  const [showCra2Modal, setShowCra2Modal] = useState<boolean>(false)
  const [showCra4Modal, setShowCra4Modal] = useState<boolean>(false)
  const [showCertifyModal, setShowCertifyModal] = useState<boolean>(false)

  // Head edit state
  const [selectedHead, setSelectedHead] = useState<CostRecordHeadDto | null>(null)
  const [headStatus, setHeadStatus] = useState<string>('Compliant')
  const [headErp, setHeadErp] = useState<string>('SAP S/4HANA Material Ledger')
  const [headNotes, setHeadNotes] = useState<string>('')
  const [headDeficiency, setHeadDeficiency] = useState<string>('')
  const [headAction, setHeadAction] = useState<string>('')

  // Product edit state
  const [prodForm, setProdForm] = useState<CreateProductCostStatementRequest>({
    productCode: '',
    productName: '',
    ctaHeading: '3004',
    unitOfMeasurement: 'MT',
    installedCapacity: 1000,
    availableCapacity: 800,
    actualProduction: 650,
    directMaterialsCost: 3500000,
    directLaborCost: 1200000,
    utilitiesCost: 600000,
    otherDirectExpenses: 200000,
    worksOverheads: 900000,
    qualityControlCost: 300000,
    rndExpenses: 250000,
    administrationOverheads: 450000,
    openingWipCost: 300000,
    closingWipCost: 200000,
    openingFinishedGoodsCost: 500000,
    closingFinishedGoodsCost: 300000,
    sellingAndDistributionOverheads: 800000,
    netSalesRealization: 9500000,
  })

  // Reconciliation item state
  const [recForm, setRecForm] = useState<CreateReconciliationItemRequest>({
    itemType: 'Add_IncomeNotCosted',
    description: '',
    amountInr: 0,
    impact: 'Positive',
    financialScheduleRef: 'Note 22 Other Income',
    notes: '',
  })

  // CRA-2 state
  const [cra2Srn, setCra2Srn] = useState<string>('')
  const [cra2Date, setCra2Date] = useState<string>(new Date().toISOString().slice(0, 10))
  const [cra2Status, setCra2Status] = useState<string>('FiledOnTime')

  // CRA-4 state
  const [cra4Srn, setCra4Srn] = useState<string>('')
  const [cra4Date, setCra4Date] = useState<string>(new Date().toISOString().slice(0, 10))
  const [cra4Status, setCra4Status] = useState<string>('FiledOnTime')
  const [cra4Xbrl, setCra4Xbrl] = useState<boolean>(true)

  // UDIN Certify state
  const [certUdin, setCertUdin] = useState<string>('')
  const [certSigner, setCertSigner] = useState<string>('')

  // Applicability Calculator Modal state
  const [appReq, setAppReq] = useState<CostAuditApplicabilityRequest>({
    sectorType: 'Regulated',
    overallTurnoverInr: 1200000000,
    productTurnoverInr: 600000000,
    exportTurnoverInr: 150000000,
    isSezUnit: false,
    isCaptivePower: false,
  })
  const [appResult, setAppResult] = useState<CostAuditApplicabilityResult | null>(null)

  // Variance Calculator Modal state
  const [varReq, setVarReq] = useState<MaterialVarianceRequest>({
    standardQuantity: 500,
    actualQuantity: 520,
    standardPrice: 200,
    actualPrice: 195,
  })
  const [varResult, setVarResult] = useState<MaterialVarianceResult | null>(null)

  // New Engagement Form state
  const [newEngForm, setNewEngForm] = useState<CreateCostAuditEngagementRequest>({
    clientId: '',
    engagementReference: `CA-${new Date().getFullYear()}-${String(new Date().getFullYear() + 1).slice(2)}-001`,
    financialYear: '2024-25',
    periodStart: `${new Date().getFullYear()}-04-01`,
    periodEnd: `${new Date().getFullYear() + 1}-03-31`,
    sectorType: 'Regulated',
    sectorCategory: 'DrugsAndPharma',
    overallTurnoverInr: 1200000000,
    productTurnoverInr: 600000000,
    exportTurnoverInr: 150000000,
    isSezExempt: false,
    isCaptivePowerExempt: false,
    boardResolutionDate: `${new Date().getFullYear()}-05-20`,
    fyCommencementDate: `${new Date().getFullYear()}-04-01`,
    auditCommitteeRecommended: true,
    costAuditorName: 'CMA Rajesh Verma',
    costAuditorFirmName: 'Verma & Associates, Cost Accountants',
    costAuditorMembershipNo: 'FCMA 45678',
    costAuditorFirmRegNo: '000567',
    auditedFinancialProfitInr: 8500000,
    seedDefaultHeads: true,
  })

  // Load clients and engagements
  const loadData = async () => {
    try {
      setLoading(true)
      setError(null)
      const [cliData, sumData, engData] = await Promise.all([
        api<ClientPage>('/api/clients?pageSize=100'),
        costAuditApi.summary(selectedClientId || undefined, selectedFy || undefined),
        costAuditApi.listEngagements(
          selectedClientId || undefined,
          selectedFy || undefined,
          selectedStatus || undefined
        ),
      ])

      setClients(cliData.items || [])
      setSummary(sumData)
      setEngagements(engData)

      if (engData.length > 0) {
        if (!selectedEngagement || !engData.find((e) => e.id === selectedEngagement.id)) {
          setSelectedEngagement(engData[0])
        } else {
          const fresh = engData.find((e) => e.id === selectedEngagement.id)!
          setSelectedEngagement(fresh)
        }
      } else {
        setSelectedEngagement(null)
      }
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to load Cost Audit data.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadData()
  }, [selectedClientId, selectedFy, selectedStatus])

  const notify = (msg: string) => {
    setSuccessMsg(msg)
    setTimeout(() => setSuccessMsg(null), 4000)
  }

  // Handle Applicability check
  const handleCheckApplicability = async () => {
    try {
      const res = await costAuditApi.checkApplicability(appReq)
      setAppResult(res)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Error running applicability evaluation.')
    }
  }

  // Handle Variance calculation
  const handleCalculateVariance = async () => {
    try {
      const res = await costAuditApi.calculateVariance(varReq)
      setVarResult(res)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Error running variance computation.')
    }
  }

  // Create Engagement
  const handleCreateEngagement = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!newEngForm.clientId) {
      setError('Please select a client company.')
      return
    }
    try {
      setLoading(true)
      const created = await costAuditApi.createEngagement(newEngForm)
      setShowNewEngModal(false)
      notify(`Engagement ${created.engagementReference} created successfully!`)
      await loadData()
      setSelectedEngagement(created)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to create Cost Audit engagement.')
    } finally {
      setLoading(false)
    }
  }

  // Seed CRA-1 heads
  const handleSeedHeads = async () => {
    if (!selectedEngagement) return
    try {
      setLoading(true)
      const updated = await costAuditApi.seedHeads(selectedEngagement.id)
      setSelectedEngagement(updated)
      notify('Seeded standard 30 Form CRA-1 Cost Records heads.')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to seed cost record heads.')
    } finally {
      setLoading(false)
    }
  }

  // Open Head Edit
  const handleOpenHeadEdit = (h: CostRecordHeadDto) => {
    setSelectedHead(h)
    setHeadStatus(h.status)
    setHeadErp(h.recordMaintenanceSystem)
    setHeadNotes(h.verificationNotes || '')
    setHeadDeficiency(h.deficiencyOrAnomaly || '')
    setHeadAction(h.correctiveActionRequired || '')
    setShowHeadModal(true)
  }

  // Save Head
  const handleSaveHead = async () => {
    if (!selectedEngagement || !selectedHead) return
    try {
      const req: UpdateRecordHeadRequest = {
        status: headStatus,
        recordMaintenanceSystem: headErp,
        verificationNotes: headNotes,
        deficiencyOrAnomaly: headDeficiency,
        correctiveActionRequired: headAction,
      }
      const updated = await costAuditApi.updateHead(selectedEngagement.id, selectedHead.id, req)
      setSelectedEngagement(updated)
      setShowHeadModal(false)
      notify(`Updated CRA-1 Head: ${selectedHead.headName}`)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to update record head.')
    }
  }

  // Add Product Cost Statement
  const handleAddProduct = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedEngagement) return
    try {
      const updated = await costAuditApi.addProduct(selectedEngagement.id, prodForm)
      setSelectedEngagement(updated)
      setShowProductModal(false)
      notify(`Product ${prodForm.productName} added to Cost Statement.`)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to add product cost statement.')
    }
  }

  // Delete Product
  const handleDeleteProduct = async (prodId: string) => {
    if (!selectedEngagement) return
    if (!confirm('Are you sure you want to remove this product cost statement?')) return
    try {
      const updated = await costAuditApi.deleteProduct(selectedEngagement.id, prodId)
      setSelectedEngagement(updated)
      notify('Product cost statement removed.')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete product cost statement.')
    }
  }

  // Add Reconciliation Item
  const handleAddReconciliation = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedEngagement) return
    try {
      const updated = await costAuditApi.addReconciliation(selectedEngagement.id, recForm)
      setSelectedEngagement(updated)
      setShowRecModal(false)
      notify('Reconciliation item added successfully.')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to add reconciliation item.')
    }
  }

  // Delete Reconciliation Item
  const handleDeleteRecItem = async (itemId: string) => {
    if (!selectedEngagement) return
    if (!confirm('Are you sure you want to delete this reconciliation item?')) return
    try {
      const updated = await costAuditApi.deleteReconciliation(selectedEngagement.id, itemId)
      setSelectedEngagement(updated)
      notify('Reconciliation item removed.')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to remove reconciliation item.')
    }
  }

  // Record CRA-2
  const handleSaveCra2 = async () => {
    if (!selectedEngagement) return
    try {
      const updated = await costAuditApi.recordCra2(selectedEngagement.id, {
        cra2Srn,
        cra2FiledDate: cra2Date,
        cra2FilingStatus: cra2Status,
      })
      setSelectedEngagement(updated)
      setShowCra2Modal(false)
      notify('Form CRA-2 appointment filing details recorded.')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to record CRA-2 filing.')
    }
  }

  // Record CRA-4
  const handleSaveCra4 = async () => {
    if (!selectedEngagement) return
    try {
      const updated = await costAuditApi.recordCra4(selectedEngagement.id, {
        cra4Srn,
        cra4FiledDate: cra4Date,
        cra4FilingStatus: cra4Status,
        cra4XbrlValidated: cra4Xbrl,
      })
      setSelectedEngagement(updated)
      setShowCra4Modal(false)
      notify('Form CRA-4 submission to Central Government recorded.')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to record CRA-4 filing.')
    }
  }

  // Generate CRA-3 Report
  const handleGenerateReport = async () => {
    if (!selectedEngagement) return
    try {
      setLoading(true)
      const updated = await costAuditApi.generateCra3(selectedEngagement.id)
      setSelectedEngagement(updated)
      notify('Statutory Cost Audit Report (Form CRA-3) generated!')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to generate Form CRA-3.')
    } finally {
      setLoading(false)
    }
  }

  // Certify with UDIN
  const handleCertify = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedEngagement) return
    if (certUdin.trim().length !== 18) {
      setError('A valid 18-character ICMAI / ICAI UDIN is mandatory.')
      return
    }
    try {
      const updated = await costAuditApi.certify(selectedEngagement.id, {
        udin: certUdin.trim().toUpperCase(),
        certifiedBy: certSigner || selectedEngagement.costAuditorName,
      })
      setSelectedEngagement(updated)
      setShowCertifyModal(false)
      notify('Form CRA-3 signed & certified with UDIN!')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Certification failed.')
    }
  }

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat('en-IN', {
      style: 'currency',
      currency: 'INR',
      maximumFractionDigits: 2,
    }).format(val)
  }

  return (
    <div className="p-6 space-y-6 max-w-7xl mx-auto">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900 flex items-center gap-2">
            <span>🏭</span> Cost Audit & Form CRA-1 to CRA-4 Desk
          </h1>
          <p className="text-sm text-gray-600 mt-1">
            Companies Act, 2013, Section 148 & Companies (Cost Records and Audit) Rules, 2014
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          <button
            onClick={() => {
              setShowAppCheckModal(true)
              handleCheckApplicability()
            }}
            className="px-3 py-1.5 text-xs font-medium rounded-md border border-indigo-300 text-indigo-700 bg-indigo-50 hover:bg-indigo-100 flex items-center gap-1.5"
          >
            <span>⚖️</span> Rule 3 & 4 Applicability Check
          </button>
          <button
            onClick={() => {
              setShowVarianceModal(true)
              handleCalculateVariance()
            }}
            className="px-3 py-1.5 text-xs font-medium rounded-md border border-emerald-300 text-emerald-700 bg-emerald-50 hover:bg-emerald-100 flex items-center gap-1.5"
          >
            <span>📊</span> Material Variance Calculator
          </button>
          <button
            onClick={() => {
              if (clients.length > 0 && !newEngForm.clientId) {
                setNewEngForm((prev) => ({ ...prev, clientId: clients[0].id }))
              }
              setShowNewEngModal(true)
            }}
            className="px-3.5 py-1.5 text-xs font-medium rounded-md text-white bg-indigo-600 hover:bg-indigo-700 shadow-sm flex items-center gap-1.5"
          >
            <span>➕</span> New Cost Audit Engagement
          </button>
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div className="p-4 bg-red-50 border-l-4 border-red-500 text-red-700 text-sm flex justify-between items-center rounded">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="font-bold text-red-700">✕</button>
        </div>
      )}
      {successMsg && (
        <div className="p-4 bg-green-50 border-l-4 border-green-500 text-green-700 text-sm flex justify-between items-center rounded">
          <span>{successMsg}</span>
          <button onClick={() => setSuccessMsg(null)} className="font-bold text-green-700">✕</button>
        </div>
      )}
      {loading && (
        <div className="p-2 bg-indigo-50 border border-indigo-200 text-indigo-700 text-xs rounded flex items-center gap-2">
          <span className="animate-spin">⏳</span> Syncing Cost Audit records and engagements...
        </div>
      )}

      {/* Executive Metric Cards */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
        <div className="bg-white p-4 rounded-lg border shadow-sm">
          <span className="text-xs font-medium text-gray-500 uppercase tracking-wider">Total Engagements</span>
          <div className="mt-2 text-2xl font-bold text-gray-900">{summary?.totalEngagements ?? 0}</div>
          <div className="text-xs text-gray-500 mt-1">{summary?.activeAudits ?? 0} active | {summary?.certifiedAudits ?? 0} certified</div>
        </div>

        <div className="bg-white p-4 rounded-lg border shadow-sm">
          <span className="text-xs font-medium text-gray-500 uppercase tracking-wider">Sector Classification</span>
          <div className="mt-2 text-2xl font-bold text-indigo-700">
            {summary?.regulatedSectorsCount ?? 0} <span className="text-sm text-gray-500 font-normal">Table A</span>
          </div>
          <div className="text-xs text-gray-500 mt-1">{summary?.nonRegulatedSectorsCount ?? 0} Table B (Non-Regulated)</div>
        </div>

        <div className="bg-white p-4 rounded-lg border shadow-sm">
          <span className="text-xs font-medium text-gray-500 uppercase tracking-wider">Audited Production</span>
          <div className="mt-2 text-xl font-bold text-gray-900">{formatCurrency(summary?.totalAuditedProductionInr ?? 0)}</div>
          <div className="text-xs text-gray-500 mt-1">Cost of Production</div>
        </div>

        <div className="bg-white p-4 rounded-lg border shadow-sm">
          <span className="text-xs font-medium text-gray-500 uppercase tracking-wider">Total Cost Profit</span>
          <div className="mt-2 text-xl font-bold text-emerald-700">{formatCurrency(summary?.totalCostProfitInr ?? 0)}</div>
          <div className="text-xs text-gray-500 mt-1">Margin from Cost Sheets</div>
        </div>

        <div className="bg-white p-4 rounded-lg border shadow-sm">
          <span className="text-xs font-medium text-gray-500 uppercase tracking-wider">P&L Discrepancy</span>
          <div className={`mt-2 text-xl font-bold ${(summary?.totalUnreconciledDifferenceInr ?? 0) === 0 ? 'text-emerald-700' : 'text-amber-600'}`}>
            {formatCurrency(summary?.totalUnreconciledDifferenceInr ?? 0)}
          </div>
          <div className="text-xs text-gray-500 mt-1">
            {(summary?.totalUnreconciledDifferenceInr ?? 0) === 0 ? '✓ Balanced' : '⚠️ Pending Reconciliation'}
          </div>
        </div>

        <div className="bg-white p-4 rounded-lg border shadow-sm">
          <span className="text-xs font-medium text-gray-500 uppercase tracking-wider">CRA-1 Compliance</span>
          <div className="mt-2 text-2xl font-bold text-blue-700">{summary?.averageRecordsComplianceScorePct ?? 100}%</div>
          <div className="text-xs text-gray-500 mt-1">
            {summary?.overdueFilingsCount ? `🚨 ${summary.overdueFilingsCount} Overdue` : 'No Overdue Filings'}
          </div>
        </div>
      </div>

      {/* Filter Toolbar */}
      <div className="bg-white p-4 rounded-lg border shadow-sm flex flex-wrap gap-4 items-center justify-between">
        <div className="flex flex-wrap items-center gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Filter Client</label>
            <select
              value={selectedClientId}
              onChange={(e) => setSelectedClientId(e.target.value)}
              className="text-xs border rounded px-2.5 py-1.5 bg-gray-50 text-gray-800"
            >
              <option value="">All Clients</option>
              {clients.map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Financial Year</label>
            <select
              value={selectedFy}
              onChange={(e) => setSelectedFy(e.target.value)}
              className="text-xs border rounded px-2.5 py-1.5 bg-gray-50 text-gray-800"
            >
              <option value="">All FYs</option>
              <option value="2024-25">2024-25</option>
              <option value="2023-24">2023-24</option>
              <option value="2022-23">2022-23</option>
            </select>
          </div>

          <div>
            <label className="block text-xs font-medium text-gray-500 mb-1">Audit Status</label>
            <select
              value={selectedStatus}
              onChange={(e) => setSelectedStatus(e.target.value)}
              className="text-xs border rounded px-2.5 py-1.5 bg-gray-50 text-gray-800"
            >
              <option value="">All Statuses</option>
              <option value="Draft">Draft</option>
              <option value="AuditInProgress">Audit In Progress</option>
              <option value="UnderReview">Under Review</option>
              <option value="Certified">Certified (UDIN)</option>
              <option value="FiledCRA4">Filed CRA-4 (MCA)</option>
            </select>
          </div>
        </div>

        <div className="text-xs text-gray-500">
          Showing <span className="font-semibold text-gray-800">{engagements.length}</span> audit engagements
        </div>
      </div>

      {/* Main Workspace: Engagements List & Engagement Details */}
      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        {/* Left column: Engagement Cards */}
        <div className="lg:col-span-1 space-y-3">
          <h2 className="text-sm font-semibold text-gray-700 uppercase tracking-wider">Engagements</h2>
          {engagements.length === 0 ? (
            <div className="bg-white p-6 rounded-lg border text-center text-sm text-gray-500">
              No cost audit engagements found. Click "New Cost Audit Engagement" to start.
            </div>
          ) : (
            <div className="space-y-2.5 max-h-[750px] overflow-y-auto pr-1">
              {engagements.map((eng) => {
                const isSelected = selectedEngagement?.id === eng.id
                return (
                  <div
                    key={eng.id}
                    onClick={() => setSelectedEngagement(eng)}
                    className={`p-3.5 rounded-lg border cursor-pointer transition-all ${
                      isSelected
                        ? 'border-indigo-600 bg-indigo-50/40 shadow-sm ring-1 ring-indigo-600'
                        : 'border-gray-200 bg-white hover:border-indigo-300 hover:bg-gray-50'
                    }`}
                  >
                    <div className="flex justify-between items-start">
                      <span className="font-semibold text-xs text-indigo-700 bg-indigo-100/60 px-2 py-0.5 rounded">
                        {eng.engagementReference}
                      </span>
                      <span
                        className={`text-[10px] font-semibold px-2 py-0.5 rounded-full ${
                          eng.auditStatus === 'Certified' || eng.auditStatus === 'FiledCRA4'
                            ? 'bg-green-100 text-green-800'
                            : 'bg-amber-100 text-amber-800'
                        }`}
                      >
                        {eng.auditStatus}
                      </span>
                    </div>

                    <div className="mt-2 font-medium text-sm text-gray-900 truncate">
                      {eng.clientName}
                    </div>

                    <div className="mt-1 flex items-center justify-between text-xs text-gray-500">
                      <span>FY {eng.financialYear}</span>
                      <span className="font-semibold text-gray-700">{eng.sectorType} ({eng.sectorCategory})</span>
                    </div>

                    <div className="mt-2 pt-2 border-t border-gray-100 flex items-center justify-between text-[11px] text-gray-500">
                      <span>CRA-1: {eng.recordsComplianceScorePct}%</span>
                      <span>{eng.totalProductsCount} Products</span>
                    </div>
                  </div>
                )
              })}
            </div>
          )}
        </div>

        {/* Right column: Active Engagement Workspace */}
        <div className="lg:col-span-3 space-y-4">
          {selectedEngagement ? (
            <div className="bg-white rounded-lg border shadow-sm overflow-hidden">
              {/* Engagement Header */}
              <div className="p-5 border-b bg-gradient-to-r from-gray-50 to-white flex flex-col md:flex-row justify-between md:items-center gap-4">
                <div>
                  <div className="flex items-center gap-2">
                    <h3 className="text-lg font-bold text-gray-900">{selectedEngagement.clientName}</h3>
                    <span className="text-xs bg-indigo-100 text-indigo-800 font-semibold px-2 py-0.5 rounded">
                      {selectedEngagement.engagementReference}
                    </span>
                    <span className="text-xs bg-gray-100 text-gray-700 font-medium px-2 py-0.5 rounded">
                      FY {selectedEngagement.financialYear}
                    </span>
                  </div>
                  <div className="text-xs text-gray-500 mt-1 flex flex-wrap gap-4">
                    <span>Sector: <strong>{selectedEngagement.sectorType}</strong> ({selectedEngagement.sectorCategory})</span>
                    <span>Auditor: <strong>{selectedEngagement.costAuditorFirmName || selectedEngagement.costAuditorName}</strong></span>
                    {selectedEngagement.udin && (
                      <span className="text-emerald-700 font-mono font-semibold">UDIN: {selectedEngagement.udin}</span>
                    )}
                  </div>
                </div>

                <div className="flex items-center gap-2">
                  <button
                    onClick={handleGenerateReport}
                    className="px-3 py-1.5 text-xs font-medium rounded border border-gray-300 text-gray-700 bg-white hover:bg-gray-50 flex items-center gap-1 shadow-sm"
                  >
                    <span>📑</span> Generate CRA-3
                  </button>
                  {selectedEngagement.auditStatus !== 'Certified' && selectedEngagement.auditStatus !== 'FiledCRA4' && (
                    <button
                      onClick={() => {
                        setCertSigner(selectedEngagement.costAuditorName)
                        setShowCertifyModal(true)
                      }}
                      className="px-3.5 py-1.5 text-xs font-semibold rounded text-white bg-emerald-600 hover:bg-emerald-700 flex items-center gap-1 shadow-sm"
                    >
                      <span>✍️</span> Sign & Certify (UDIN)
                    </button>
                  )}
                </div>
              </div>

              {/* Navigation Tabs */}
              <div className="border-b px-5 flex space-x-6 text-xs font-medium">
                <button
                  onClick={() => setActiveTab('filings')}
                  className={`py-3 border-b-2 transition-colors ${
                    activeTab === 'filings'
                      ? 'border-indigo-600 text-indigo-600 font-semibold'
                      : 'border-transparent text-gray-500 hover:text-gray-700'
                  }`}
                >
                  🏛️ Filings (CRA-2 & CRA-4)
                </button>
                <button
                  onClick={() => setActiveTab('records')}
                  className={`py-3 border-b-2 transition-colors flex items-center gap-1.5 ${
                    activeTab === 'records'
                      ? 'border-indigo-600 text-indigo-600 font-semibold'
                      : 'border-transparent text-gray-500 hover:text-gray-700'
                  }`}
                >
                  <span>📋</span> Form CRA-1 Cost Records
                  <span className="ml-1 text-[10px] bg-gray-100 text-gray-600 px-1.5 py-0.2 rounded-full">
                    {selectedEngagement.recordHeads.length}
                  </span>
                </button>
                <button
                  onClick={() => setActiveTab('costsheet')}
                  className={`py-3 border-b-2 transition-colors flex items-center gap-1.5 ${
                    activeTab === 'costsheet'
                      ? 'border-indigo-600 text-indigo-600 font-semibold'
                      : 'border-transparent text-gray-500 hover:text-gray-700'
                  }`}
                >
                  <span>🏭</span> Cost Statements (Part B)
                  <span className="ml-1 text-[10px] bg-gray-100 text-gray-600 px-1.5 py-0.2 rounded-full">
                    {selectedEngagement.productStatements.length}
                  </span>
                </button>
                <button
                  onClick={() => setActiveTab('reconciliation')}
                  className={`py-3 border-b-2 transition-colors flex items-center gap-1.5 ${
                    activeTab === 'reconciliation'
                      ? 'border-indigo-600 text-indigo-600 font-semibold'
                      : 'border-transparent text-gray-500 hover:text-gray-700'
                  }`}
                >
                  <span>⚖️</span> P&L Reconciliation (Part D)
                  <span
                    className={`ml-1 text-[10px] px-1.5 py-0.2 rounded-full ${
                      selectedEngagement.netReconciliationDifferenceInr === 0
                        ? 'bg-emerald-100 text-emerald-700'
                        : 'bg-amber-100 text-amber-700'
                    }`}
                  >
                    {selectedEngagement.netReconciliationDifferenceInr === 0 ? 'Balanced' : 'Diff'}
                  </span>
                </button>
                <button
                  onClick={() => setActiveTab('report')}
                  className={`py-3 border-b-2 transition-colors ${
                    activeTab === 'report'
                      ? 'border-indigo-600 text-indigo-600 font-semibold'
                      : 'border-transparent text-gray-500 hover:text-gray-700'
                  }`}
                >
                  📜 Form CRA-3 Statutory Report
                </button>
              </div>

              {/* Tab Contents */}
              <div className="p-5">
                {/* TAB 1: FILINGS & STATUTORY TIMELINES */}
                {activeTab === 'filings' && (
                  <div className="space-y-6">
                    {/* Applicability summary note */}
                    <div className="bg-blue-50/50 p-4 rounded-lg border border-blue-200">
                      <h4 className="text-xs font-bold text-blue-900 uppercase tracking-wider">
                        Rule 3 & 4 Statutory Evaluation Verdict
                      </h4>
                      <div className="mt-2 text-xs text-blue-800 whitespace-pre-line font-mono leading-relaxed">
                        {selectedEngagement.applicabilityReasoning}
                      </div>
                      <div className="mt-3 flex flex-wrap gap-4 text-xs text-blue-900 pt-2 border-t border-blue-200/60">
                        <span>Overall Turnover: <strong>{formatCurrency(selectedEngagement.overallTurnoverInr)}</strong></span>
                        <span>Product Turnover: <strong>{formatCurrency(selectedEngagement.productTurnoverInr)}</strong></span>
                        <span>Export Turnover: <strong>{formatCurrency(selectedEngagement.exportTurnoverInr)}</strong></span>
                      </div>
                    </div>

                    <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                      {/* Form CRA-2 Card */}
                      <div className="border rounded-lg p-4 bg-gray-50/50 space-y-3">
                        <div className="flex justify-between items-start">
                          <div>
                            <span className="text-xs font-bold uppercase tracking-wider text-indigo-700">Form CRA-2</span>
                            <h4 className="font-semibold text-sm text-gray-900 mt-0.5">
                              Intimation of Cost Auditor Appointment
                            </h4>
                          </div>
                          <span
                            className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                              selectedEngagement.cra2FilingStatus === 'FiledOnTime'
                                ? 'bg-emerald-100 text-emerald-800'
                                : selectedEngagement.cra2FilingStatus === 'Overdue'
                                ? 'bg-red-100 text-red-800'
                                : 'bg-amber-100 text-amber-800'
                            }`}
                          >
                            {selectedEngagement.cra2FilingStatus}
                          </span>
                        </div>

                        <p className="text-xs text-gray-600">
                          Due within <strong>30 days</strong> of Board Meeting OR <strong>180 days</strong> of FY commencement (whichever is earlier).
                        </p>

                        <div className="text-xs space-y-1 bg-white p-3 rounded border text-gray-700">
                          <div className="flex justify-between">
                            <span className="text-gray-500">Board Resolution Date:</span>
                            <span>{selectedEngagement.boardResolutionDate ? new Date(selectedEngagement.boardResolutionDate).toLocaleDateString('en-IN') : 'N/A'}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-500">Statutory Due Date:</span>
                            <span className="font-semibold text-indigo-700">
                              {selectedEngagement.cra2FilingDueDate ? new Date(selectedEngagement.cra2FilingDueDate).toLocaleDateString('en-IN') : 'N/A'}
                            </span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-500">MCA Filing SRN:</span>
                            <span className="font-mono font-semibold">{selectedEngagement.cra2Srn || 'Pending'}</span>
                          </div>
                          {selectedEngagement.cra2FiledDate && (
                            <div className="flex justify-between">
                              <span className="text-gray-500">Filed On:</span>
                              <span>{new Date(selectedEngagement.cra2FiledDate).toLocaleDateString('en-IN')}</span>
                            </div>
                          )}
                        </div>

                        <button
                          onClick={() => {
                            setCra2Srn(selectedEngagement.cra2Srn || '')
                            setShowCra2Modal(true)
                          }}
                          className="w-full py-1.5 text-xs font-medium rounded border border-indigo-600 text-indigo-700 bg-white hover:bg-indigo-50"
                        >
                          Record / Update Form CRA-2 Filing
                        </button>
                      </div>

                      {/* Form CRA-4 Card */}
                      <div className="border rounded-lg p-4 bg-gray-50/50 space-y-3">
                        <div className="flex justify-between items-start">
                          <div>
                            <span className="text-xs font-bold uppercase tracking-wider text-emerald-700">Form CRA-4</span>
                            <h4 className="font-semibold text-sm text-gray-900 mt-0.5">
                              Filing Cost Audit Report with Central Govt
                            </h4>
                          </div>
                          <span
                            className={`text-[10px] font-bold px-2 py-0.5 rounded-full ${
                              selectedEngagement.cra4FilingStatus === 'FiledOnTime' || selectedEngagement.auditStatus === 'FiledCRA4'
                                ? 'bg-emerald-100 text-emerald-800'
                                : selectedEngagement.cra4FilingStatus === 'Overdue'
                                ? 'bg-red-100 text-red-800'
                                : 'bg-gray-100 text-gray-700'
                            }`}
                          >
                            {selectedEngagement.cra4FilingStatus}
                          </span>
                        </div>

                        <p className="text-xs text-gray-600">
                          Due within <strong>30 days</strong> from the date of receipt of the Cost Audit Report (Form CRA-3) in XBRL format.
                        </p>

                        <div className="text-xs space-y-1 bg-white p-3 rounded border text-gray-700">
                          <div className="flex justify-between">
                            <span className="text-gray-500">CRA-3 Handover Date:</span>
                            <span>{selectedEngagement.cra3HandoverDate ? new Date(selectedEngagement.cra3HandoverDate).toLocaleDateString('en-IN') : 'Pending Report'}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-500">Statutory Due Date:</span>
                            <span className="font-semibold text-emerald-700">
                              {selectedEngagement.cra4FilingDueDate ? new Date(selectedEngagement.cra4FilingDueDate).toLocaleDateString('en-IN') : 'N/A'}
                            </span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-500">MCA Filing SRN:</span>
                            <span className="font-mono font-semibold">{selectedEngagement.cra4Srn || 'Pending'}</span>
                          </div>
                          <div className="flex justify-between">
                            <span className="text-gray-500">XBRL Schema Validation:</span>
                            <span className={selectedEngagement.cra4XbrlValidated ? 'text-emerald-700 font-semibold' : 'text-gray-400'}>
                              {selectedEngagement.cra4XbrlValidated ? '✓ Validated' : 'Pending'}
                            </span>
                          </div>
                        </div>

                        <button
                          onClick={() => {
                            setCra4Srn(selectedEngagement.cra4Srn || '')
                            setShowCra4Modal(true)
                          }}
                          className="w-full py-1.5 text-xs font-medium rounded border border-emerald-600 text-emerald-700 bg-white hover:bg-emerald-50"
                        >
                          Record / Update Form CRA-4 Filing
                        </button>
                      </div>
                    </div>
                  </div>
                )}

                {/* TAB 2: FORM CRA-1 COST RECORDS MATRIX */}
                {activeTab === 'records' && (
                  <div className="space-y-4">
                    <div className="flex justify-between items-center">
                      <div>
                        <h4 className="text-sm font-bold text-gray-800">Form CRA-1: 30 Standard Heads of Cost Records</h4>
                        <p className="text-xs text-gray-500">Rule 3 verification matrix covering materials, wages, utilities, overheads, and inventory.</p>
                      </div>
                      <div className="flex items-center gap-3">
                        <span className="text-xs font-semibold text-gray-700">
                          Compliance Score: <strong className="text-indigo-600">{selectedEngagement.recordsComplianceScorePct}%</strong>
                        </span>
                        {selectedEngagement.recordHeads.length < 30 && (
                          <button
                            onClick={handleSeedHeads}
                            className="px-3 py-1 text-xs font-medium rounded border border-indigo-600 text-indigo-700 bg-indigo-50 hover:bg-indigo-100"
                          >
                            Seed Standard 30 Heads
                          </button>
                        )}
                      </div>
                    </div>

                    <div className="overflow-x-auto border rounded-lg">
                      <table className="w-full text-left text-xs text-gray-700">
                        <thead className="bg-gray-50 border-b text-[11px] uppercase font-semibold text-gray-500">
                          <tr>
                            <th className="py-2.5 px-3">Head Code</th>
                            <th className="py-2.5 px-3">Record Description</th>
                            <th className="py-2.5 px-3">Status</th>
                            <th className="py-2.5 px-3">ERP / System</th>
                            <th className="py-2.5 px-3">Audit Observations</th>
                            <th className="py-2.5 px-3 text-right">Action</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100">
                          {selectedEngagement.recordHeads.map((h) => (
                            <tr key={h.id} className="hover:bg-gray-50">
                              <td className="py-2.5 px-3 font-mono font-semibold text-indigo-700">{h.headCode}</td>
                              <td className="py-2.5 px-3 font-medium text-gray-900">{h.headName}</td>
                              <td className="py-2.5 px-3">
                                <span
                                  className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                                    h.status === 'Compliant'
                                      ? 'bg-emerald-100 text-emerald-800'
                                      : h.status === 'PartiallyCompliant'
                                      ? 'bg-amber-100 text-amber-800'
                                      : h.status === 'NonCompliant'
                                      ? 'bg-red-100 text-red-800'
                                      : 'bg-gray-100 text-gray-600'
                                  }`}
                                >
                                  {h.status}
                                </span>
                              </td>
                              <td className="py-2.5 px-3 text-gray-600">{h.recordMaintenanceSystem}</td>
                              <td className="py-2.5 px-3 text-gray-600 truncate max-w-[200px]">
                                {h.verificationNotes || <span className="text-gray-400 italic">None</span>}
                              </td>
                              <td className="py-2.5 px-3 text-right">
                                <button
                                  onClick={() => handleOpenHeadEdit(h)}
                                  className="text-indigo-600 hover:text-indigo-800 font-semibold"
                                >
                                  Edit
                                </button>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                )}

                {/* TAB 3: PRODUCT COST STATEMENTS (PART B) */}
                {activeTab === 'costsheet' && (
                  <div className="space-y-4">
                    <div className="flex justify-between items-center">
                      <div>
                        <h4 className="text-sm font-bold text-gray-800">Part B: Product-wise Cost Statements & Quantitative Info</h4>
                        <p className="text-xs text-gray-500">Breakdown of Prime Cost, Works Cost, Cost of Production, Cost of Sales, Realization & Margins.</p>
                      </div>
                      <button
                        onClick={() => setShowProductModal(true)}
                        className="px-3 py-1.5 text-xs font-semibold rounded text-white bg-indigo-600 hover:bg-indigo-700 shadow-sm flex items-center gap-1"
                      >
                        <span>➕</span> Add Product Statement
                      </button>
                    </div>

                    {selectedEngagement.productStatements.length === 0 ? (
                      <div className="text-center py-10 border rounded-lg bg-gray-50 text-sm text-gray-500">
                        No product cost statements added yet. Click "Add Product Statement" to create one.
                      </div>
                    ) : (
                      <div className="overflow-x-auto border rounded-lg">
                        <table className="w-full text-left text-xs text-gray-700 whitespace-nowrap">
                          <thead className="bg-gray-50 border-b text-[11px] uppercase font-semibold text-gray-500">
                            <tr>
                              <th className="py-2.5 px-3">Product</th>
                              <th className="py-2.5 px-3">CTA / HSN</th>
                              <th className="py-2.5 px-3 text-right">Production</th>
                              <th className="py-2.5 px-3 text-right">Capacity Util.</th>
                              <th className="py-2.5 px-3 text-right">Prime Cost</th>
                              <th className="py-2.5 px-3 text-right">Cost of Prod.</th>
                              <th className="py-2.5 px-3 text-right">Cost of Sales</th>
                              <th className="py-2.5 px-3 text-right">Realization</th>
                              <th className="py-2.5 px-3 text-right">Margin (₹)</th>
                              <th className="py-2.5 px-3 text-right">Margin %</th>
                              <th className="py-2.5 px-3 text-center">Actions</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-gray-100">
                            {selectedEngagement.productStatements.map((p) => (
                              <tr key={p.id} className="hover:bg-gray-50">
                                <td className="py-2.5 px-3">
                                  <div className="font-semibold text-gray-900">{p.productName}</div>
                                  <div className="text-[10px] text-gray-500 font-mono">{p.productCode}</div>
                                </td>
                                <td className="py-2.5 px-3 font-mono text-gray-600">{p.ctaHeading}</td>
                                <td className="py-2.5 px-3 text-right font-medium">
                                  {p.actualProduction.toLocaleString('en-IN')} {p.unitOfMeasurement}
                                </td>
                                <td className="py-2.5 px-3 text-right">
                                  <div className="flex items-center justify-end gap-1.5">
                                    <span className="font-semibold text-gray-800">{p.capacityUtilizationPct}%</span>
                                    <div className="w-12 bg-gray-200 rounded-full h-1.5 overflow-hidden">
                                      <div
                                        className="bg-indigo-600 h-1.5 rounded-full"
                                        style={{ width: `${Math.min(100, p.capacityUtilizationPct)}%` }}
                                      />
                                    </div>
                                  </div>
                                </td>
                                <td className="py-2.5 px-3 text-right">{formatCurrency(p.primeCost)}</td>
                                <td className="py-2.5 px-3 text-right font-medium text-gray-900">{formatCurrency(p.costOfProduction)}</td>
                                <td className="py-2.5 px-3 text-right text-gray-800">{formatCurrency(p.costOfSales)}</td>
                                <td className="py-2.5 px-3 text-right font-semibold text-gray-900">{formatCurrency(p.netSalesRealization)}</td>
                                <td className={`py-2.5 px-3 text-right font-bold ${p.productMarginInr >= 0 ? 'text-emerald-700' : 'text-red-600'}`}>
                                  {formatCurrency(p.productMarginInr)}
                                </td>
                                <td className={`py-2.5 px-3 text-right font-semibold ${p.productMarginPct >= 0 ? 'text-emerald-700' : 'text-red-600'}`}>
                                  {p.productMarginPct}%
                                </td>
                                <td className="py-2.5 px-3 text-center">
                                  <button
                                    onClick={() => handleDeleteProduct(p.id)}
                                    className="text-red-600 hover:text-red-800 text-[11px]"
                                  >
                                    Delete
                                  </button>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                          <tfoot className="bg-gray-50 border-t font-semibold text-gray-900">
                            <tr>
                              <td className="py-2.5 px-3" colSpan={4}>TOTAL ({selectedEngagement.totalProductsCount} Products)</td>
                              <td className="py-2.5 px-3 text-right"></td>
                              <td className="py-2.5 px-3 text-right">{formatCurrency(selectedEngagement.totalProductionValueInr)}</td>
                              <td className="py-2.5 px-3 text-right">{formatCurrency(selectedEngagement.totalCostOfSalesInr)}</td>
                              <td className="py-2.5 px-3 text-right">{formatCurrency(selectedEngagement.totalSalesRealizationInr)}</td>
                              <td className="py-2.5 px-3 text-right text-emerald-700 font-bold">{formatCurrency(selectedEngagement.totalCostProfitInr)}</td>
                              <td className="py-2.5 px-3 text-right" colSpan={2}></td>
                            </tr>
                          </tfoot>
                        </table>
                      </div>
                    )}
                  </div>
                )}

                {/* TAB 4: PROFIT RECONCILIATION WITH FINANCIAL P&L (PART D) */}
                {activeTab === 'reconciliation' && (
                  <div className="space-y-5">
                    <div className="flex justify-between items-center">
                      <div>
                        <h4 className="text-sm font-bold text-gray-800">Part D: Reconciliation of Cost Accounts with Audited Financial P&L</h4>
                        <p className="text-xs text-gray-500">Bridges Cost Profit to Audited Financial Profit through non-costed incomes and expenses.</p>
                      </div>
                      <button
                        onClick={() => setShowRecModal(true)}
                        className="px-3 py-1.5 text-xs font-semibold rounded text-white bg-indigo-600 hover:bg-indigo-700 shadow-sm flex items-center gap-1"
                      >
                        <span>➕</span> Add Reconciliation Item
                      </button>
                    </div>

                    {/* Reconciliation Math Banner */}
                    <div className="grid grid-cols-1 md:grid-cols-4 gap-4 p-4 bg-gray-50 rounded-lg border">
                      <div className="space-y-0.5">
                        <span className="text-[11px] text-gray-500 font-medium">1. Profit as per Cost Records</span>
                        <div className="text-base font-bold text-gray-900">{formatCurrency(selectedEngagement.totalCostProfitInr)}</div>
                      </div>
                      <div className="space-y-0.5">
                        <span className="text-[11px] text-gray-500 font-medium">2. Reconciled Financial Profit</span>
                        <div className="text-base font-bold text-indigo-700">{formatCurrency(selectedEngagement.reconciledFinancialProfitInr)}</div>
                      </div>
                      <div className="space-y-0.5">
                        <span className="text-[11px] text-gray-500 font-medium">3. Audited Financial Profit (P&L)</span>
                        <div className="text-base font-bold text-gray-900">{formatCurrency(selectedEngagement.auditedFinancialProfitInr)}</div>
                      </div>
                      <div className="space-y-0.5">
                        <span className="text-[11px] text-gray-500 font-medium">4. Unreconciled Variance</span>
                        <div
                          className={`text-base font-bold ${
                            selectedEngagement.netReconciliationDifferenceInr === 0 ? 'text-emerald-700' : 'text-red-600'
                          }`}
                        >
                          {formatCurrency(selectedEngagement.netReconciliationDifferenceInr)}
                          <span className="text-xs ml-1 font-normal">
                            {selectedEngagement.netReconciliationDifferenceInr === 0 ? '✓ Balanced' : '⚠️ Discrepancy'}
                          </span>
                        </div>
                      </div>
                    </div>

                    {/* Reconciliation Items List */}
                    <div className="border rounded-lg overflow-hidden">
                      <table className="w-full text-left text-xs text-gray-700">
                        <thead className="bg-gray-50 border-b text-[11px] uppercase font-semibold text-gray-500">
                          <tr>
                            <th className="py-2.5 px-3">Classification</th>
                            <th className="py-2.5 px-3">Particulars / Description</th>
                            <th className="py-2.5 px-3">P&L Schedule Reference</th>
                            <th className="py-2.5 px-3 text-right">Adjustment Amount</th>
                            <th className="py-2.5 px-3 text-center">Action</th>
                          </tr>
                        </thead>
                        <tbody className="divide-y divide-gray-100">
                          {selectedEngagement.reconciliationItems.length === 0 ? (
                            <tr>
                              <td colSpan={5} className="text-center py-6 text-gray-500 italic">
                                No reconciliation adjustments entered. Click "Add Reconciliation Item" to add non-cost items.
                              </td>
                            </tr>
                          ) : (
                            selectedEngagement.reconciliationItems.map((item) => (
                              <tr key={item.id} className="hover:bg-gray-50">
                                <td className="py-2.5 px-3">
                                  <span
                                    className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                                      item.impact === 'Positive' || item.itemType.startsWith('Add_')
                                        ? 'bg-emerald-100 text-emerald-800'
                                        : 'bg-amber-100 text-amber-800'
                                    }`}
                                  >
                                    {item.impact === 'Positive' ? '+ Add to Profit' : '- Deduct from Profit'}
                                  </span>
                                </td>
                                <td className="py-2.5 px-3 font-medium text-gray-900">{item.description}</td>
                                <td className="py-2.5 px-3 text-gray-600 font-mono">{item.financialScheduleRef || 'P&L'}</td>
                                <td className="py-2.5 px-3 text-right font-semibold text-gray-900">
                                  {formatCurrency(item.amountInr)}
                                </td>
                                <td className="py-2.5 px-3 text-center">
                                  <button
                                    onClick={() => handleDeleteRecItem(item.id)}
                                    className="text-red-600 hover:text-red-800"
                                  >
                                    Delete
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

                {/* TAB 5: FORM CRA-3 STATUTORY REPORT & UDIN */}
                {activeTab === 'report' && (
                  <div className="space-y-4">
                    <div className="flex justify-between items-center">
                      <div>
                        <h4 className="text-sm font-bold text-gray-800">Form CRA-3: Statutory Cost Audit Report</h4>
                        <p className="text-xs text-gray-500">Pursuant to Section 148(6) of Companies Act, 2013 & Rule 6(4).</p>
                      </div>
                      <div className="flex items-center gap-2">
                        <button
                          onClick={handleGenerateReport}
                          className="px-3 py-1.5 text-xs font-medium rounded border border-gray-300 text-gray-700 bg-white hover:bg-gray-50 shadow-sm"
                        >
                          Refresh Report
                        </button>
                        <button
                          onClick={() => {
                            if (selectedEngagement.costAuditReportText) {
                              navigator.clipboard.writeText(selectedEngagement.costAuditReportText)
                              notify('Report copied to clipboard!')
                            }
                          }}
                          className="px-3 py-1.5 text-xs font-medium rounded border border-gray-300 text-gray-700 bg-white hover:bg-gray-50 shadow-sm"
                        >
                          Copy Text
                        </button>
                      </div>
                    </div>

                    <div className="bg-gray-900 text-gray-100 p-4 rounded-lg font-mono text-xs overflow-x-auto max-h-[550px] leading-relaxed whitespace-pre-wrap border">
                      {selectedEngagement.costAuditReportText || 'Report not yet compiled. Click "Generate CRA-3" above.'}
                    </div>
                  </div>
                )}
              </div>
            </div>
          ) : (
            <div className="bg-white p-12 rounded-lg border text-center text-gray-500">
              Select an engagement from the left or create a new one to begin.
            </div>
          )}
        </div>
      </div>

      {/* MODAL 1: RULE 3 & 4 APPLICABILITY EVALUATOR */}
      {showAppCheckModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-lg w-full p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-base text-gray-900">Rule 3 & 4 Statutory Applicability Calculator</h3>
              <button onClick={() => setShowAppCheckModal(false)} className="text-gray-400 hover:text-gray-600 font-bold">✕</button>
            </div>

            <div className="space-y-3 text-xs">
              <div>
                <label className="block font-medium text-gray-700 mb-1">Sector Type</label>
                <select
                  value={appReq.sectorType}
                  onChange={(e) => setAppReq({ ...appReq, sectorType: e.target.value })}
                  className="w-full border rounded px-3 py-1.5 bg-gray-50"
                >
                  <option value="Regulated">Table A: Regulated Sector (Telecom, Power, Petroleum, Pharma, Fertilizers, Sugar)</option>
                  <option value="NonRegulated">Table B: Non-Regulated Sector (Steel, Cement, Chemicals, Textiles, Auto, etc.)</option>
                </select>
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Total Turnover from all Products & Services (₹)</label>
                <input
                  type="number"
                  value={appReq.overallTurnoverInr}
                  onChange={(e) => setAppReq({ ...appReq, overallTurnoverInr: parseFloat(e.target.value) || 0 })}
                  className="w-full border rounded px-3 py-1.5"
                />
                <span className="text-[10px] text-gray-500">Threshold: ≥ ₹35 Cr for records; Table A: ≥ ₹50 Cr; Table B: ≥ ₹100 Cr for audit.</span>
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Aggregate Turnover of Audited Product/Service (₹)</label>
                <input
                  type="number"
                  value={appReq.productTurnoverInr}
                  onChange={(e) => setAppReq({ ...appReq, productTurnoverInr: parseFloat(e.target.value) || 0 })}
                  className="w-full border rounded px-3 py-1.5"
                />
                <span className="text-[10px] text-gray-500">Threshold: Table A: ≥ ₹25 Cr; Table B: ≥ ₹35 Cr.</span>
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Export Turnover in Foreign Exchange (₹)</label>
                <input
                  type="number"
                  value={appReq.exportTurnoverInr}
                  onChange={(e) => setAppReq({ ...appReq, exportTurnoverInr: parseFloat(e.target.value) || 0 })}
                  className="w-full border rounded px-3 py-1.5"
                />
                <span className="text-[10px] text-gray-500">Exemption: Over 75% export revenue qualifies for Rule 4(3)(i) audit exemption.</span>
              </div>

              <div className="flex gap-4 pt-1">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={appReq.isSezUnit}
                    onChange={(e) => setAppReq({ ...appReq, isSezUnit: e.target.checked })}
                  />
                  <span>Operating from SEZ (Rule 4(3)(ii) Exemption)</span>
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={appReq.isCaptivePower}
                    onChange={(e) => setAppReq({ ...appReq, isCaptivePower: e.target.checked })}
                  />
                  <span>Captive Power Plant</span>
                </label>
              </div>

              <button
                type="button"
                onClick={handleCheckApplicability}
                className="w-full py-2 bg-indigo-600 text-white rounded font-medium hover:bg-indigo-700 mt-2"
              >
                Compute Applicability
              </button>

              {appResult && (
                <div className="mt-3 p-3 bg-gray-50 border rounded space-y-2">
                  <div className="flex justify-between font-semibold">
                    <span>Rule 3 Cost Records:</span>
                    <span className={appResult.isCostRecordsApplicable ? 'text-emerald-700' : 'text-gray-500'}>
                      {appResult.isCostRecordsApplicable ? '✓ Mandatory' : '✗ Not Applicable'}
                    </span>
                  </div>
                  <div className="flex justify-between font-semibold">
                    <span>Rule 4 Cost Audit:</span>
                    <span className={appResult.isCostAuditApplicable ? 'text-indigo-700' : 'text-gray-500'}>
                      {appResult.isCostAuditApplicable ? '✓ Mandatory' : '✗ Exempt / Not Applicable'}
                    </span>
                  </div>
                  <div className="text-[11px] text-gray-600 whitespace-pre-line pt-2 border-t font-mono">
                    {appResult.reasoning}
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: MATERIAL VARIANCE CALCULATOR */}
      {showVarianceModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-base text-gray-900">Material Cost Variance Calculator</h3>
              <button onClick={() => setShowVarianceModal(false)} className="text-gray-400 hover:text-gray-600 font-bold">✕</button>
            </div>

            <div className="space-y-3 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Standard Qty</label>
                  <input
                    type="number"
                    value={varReq.standardQuantity}
                    onChange={(e) => setVarReq({ ...varReq, standardQuantity: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Actual Qty</label>
                  <input
                    type="number"
                    value={varReq.actualQuantity}
                    onChange={(e) => setVarReq({ ...varReq, actualQuantity: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Standard Price (₹)</label>
                  <input
                    type="number"
                    value={varReq.standardPrice}
                    onChange={(e) => setVarReq({ ...varReq, standardPrice: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Actual Price (₹)</label>
                  <input
                    type="number"
                    value={varReq.actualPrice}
                    onChange={(e) => setVarReq({ ...varReq, actualPrice: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
              </div>

              <button
                type="button"
                onClick={handleCalculateVariance}
                className="w-full py-2 bg-emerald-600 text-white rounded font-medium hover:bg-emerald-700 mt-2"
              >
                Compute Variances
              </button>

              {varResult && (
                <div className="mt-3 p-3 bg-gray-50 border rounded space-y-2">
                  <div className="flex justify-between">
                    <span className="text-gray-600">Material Price Variance:</span>
                    <span className={`font-bold ${varResult.priceVariance >= 0 ? 'text-emerald-700' : 'text-red-600'}`}>
                      {formatCurrency(varResult.priceVariance)} ({varResult.priceVariance >= 0 ? 'Favorable' : 'Adverse'})
                    </span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-gray-600">Material Usage Variance:</span>
                    <span className={`font-bold ${varResult.usageVariance >= 0 ? 'text-emerald-700' : 'text-red-600'}`}>
                      {formatCurrency(varResult.usageVariance)} ({varResult.usageVariance >= 0 ? 'Favorable' : 'Adverse'})
                    </span>
                  </div>
                  <div className="flex justify-between pt-2 border-t font-bold">
                    <span>Total Cost Variance:</span>
                    <span className={varResult.totalCostVariance >= 0 ? 'text-emerald-700' : 'text-red-600'}>
                      {formatCurrency(varResult.totalCostVariance)}
                    </span>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* MODAL 3: NEW ENGAGEMENT */}
      {showNewEngModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-2xl w-full p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-base text-gray-900">Create Cost Audit Engagement (Form CRA-1 to CRA-4)</h3>
              <button onClick={() => setShowNewEngModal(false)} className="text-gray-400 hover:text-gray-600 font-bold">✕</button>
            </div>

            <form onSubmit={handleCreateEngagement} className="space-y-4 text-xs">
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Client Company *</label>
                  <select
                    value={newEngForm.clientId}
                    onChange={(e) => setNewEngForm({ ...newEngForm, clientId: e.target.value })}
                    className="w-full border rounded px-3 py-1.5 bg-gray-50"
                    required
                  >
                    <option value="">Select Company...</option>
                    {clients.map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Engagement Reference *</label>
                  <input
                    type="text"
                    value={newEngForm.engagementReference}
                    onChange={(e) => setNewEngForm({ ...newEngForm, engagementReference: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Financial Year</label>
                  <input
                    type="text"
                    value={newEngForm.financialYear}
                    onChange={(e) => setNewEngForm({ ...newEngForm, financialYear: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                    required
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Period Start</label>
                  <input
                    type="date"
                    value={newEngForm.periodStart}
                    onChange={(e) => setNewEngForm({ ...newEngForm, periodStart: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                    required
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Period End</label>
                  <input
                    type="date"
                    value={newEngForm.periodEnd}
                    onChange={(e) => setNewEngForm({ ...newEngForm, periodEnd: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Sector Type</label>
                  <select
                    value={newEngForm.sectorType}
                    onChange={(e) => setNewEngForm({ ...newEngForm, sectorType: e.target.value })}
                    className="w-full border rounded px-3 py-1.5 bg-gray-50"
                  >
                    <option value="Regulated">Table A: Regulated</option>
                    <option value="NonRegulated">Table B: Non-Regulated</option>
                  </select>
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Industry Category</label>
                  <input
                    type="text"
                    value={newEngForm.sectorCategory}
                    onChange={(e) => setNewEngForm({ ...newEngForm, sectorCategory: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                    placeholder="e.g. DrugsAndPharma, Steel, Cement, etc."
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Overall Turnover (₹)</label>
                  <input
                    type="number"
                    value={newEngForm.overallTurnoverInr}
                    onChange={(e) => setNewEngForm({ ...newEngForm, overallTurnoverInr: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Product Turnover (₹)</label>
                  <input
                    type="number"
                    value={newEngForm.productTurnoverInr}
                    onChange={(e) => setNewEngForm({ ...newEngForm, productTurnoverInr: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Export Turnover (₹)</label>
                  <input
                    type="number"
                    value={newEngForm.exportTurnoverInr}
                    onChange={(e) => setNewEngForm({ ...newEngForm, exportTurnoverInr: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Board Meeting Appointment Date (CRA-2)</label>
                  <input
                    type="date"
                    value={newEngForm.boardResolutionDate || ''}
                    onChange={(e) => setNewEngForm({ ...newEngForm, boardResolutionDate: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Audited Financial Profit (P&L) (₹)</label>
                  <input
                    type="number"
                    value={newEngForm.auditedFinancialProfitInr}
                    onChange={(e) => setNewEngForm({ ...newEngForm, auditedFinancialProfitInr: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Cost Auditor Name</label>
                  <input
                    type="text"
                    value={newEngForm.costAuditorName}
                    onChange={(e) => setNewEngForm({ ...newEngForm, costAuditorName: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Cost Auditor Firm Name</label>
                  <input
                    type="text"
                    value={newEngForm.costAuditorFirmName}
                    onChange={(e) => setNewEngForm({ ...newEngForm, costAuditorFirmName: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
              </div>

              <div className="flex items-center gap-2 pt-2">
                <input
                  type="checkbox"
                  id="seedHeadsCheck"
                  checked={newEngForm.seedDefaultHeads}
                  onChange={(e) => setNewEngForm({ ...newEngForm, seedDefaultHeads: e.target.checked })}
                />
                <label htmlFor="seedHeadsCheck" className="text-gray-700 cursor-pointer">
                  Automatically seed standard 30 Form CRA-1 Cost Records heads
                </label>
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowNewEngModal(false)}
                  className="px-4 py-2 border rounded text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded font-medium hover:bg-indigo-700"
                >
                  Create Engagement
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: EDIT CRA-1 HEAD */}
      {showHeadModal && selectedHead && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex justify-between items-center border-b pb-3">
              <div>
                <span className="text-xs font-mono font-bold text-indigo-700">{selectedHead.headCode}</span>
                <h3 className="font-bold text-base text-gray-900">{selectedHead.headName}</h3>
              </div>
              <button onClick={() => setShowHeadModal(false)} className="text-gray-400 hover:text-gray-600 font-bold">✕</button>
            </div>

            <div className="space-y-3 text-xs">
              <div>
                <label className="block font-medium text-gray-700 mb-1">Compliance Status</label>
                <select
                  value={headStatus}
                  onChange={(e) => setHeadStatus(e.target.value)}
                  className="w-full border rounded px-3 py-1.5 bg-gray-50"
                >
                  <option value="Compliant">Compliant</option>
                  <option value="PartiallyCompliant">Partially Compliant</option>
                  <option value="NonCompliant">Non-Compliant</option>
                  <option value="NotApplicable">Not Applicable</option>
                </select>
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">ERP / Record Maintenance System</label>
                <input
                  type="text"
                  value={headErp}
                  onChange={(e) => setHeadErp(e.target.value)}
                  className="w-full border rounded px-3 py-1.5"
                />
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Auditor's Verification Notes</label>
                <textarea
                  rows={2}
                  value={headNotes}
                  onChange={(e) => setHeadNotes(e.target.value)}
                  className="w-full border rounded px-3 py-1.5"
                  placeholder="Details of audit tests performed..."
                />
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Deficiency or Anomaly Discovered</label>
                <textarea
                  rows={2}
                  value={headDeficiency}
                  onChange={(e) => setHeadDeficiency(e.target.value)}
                  className="w-full border rounded px-3 py-1.5"
                  placeholder="Leave blank if fully compliant..."
                />
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Corrective Action Required</label>
                <input
                  type="text"
                  value={headAction}
                  onChange={(e) => setHeadAction(e.target.value)}
                  className="w-full border rounded px-3 py-1.5"
                  placeholder="Recommended remedial step..."
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowHeadModal(false)}
                  className="px-4 py-2 border rounded text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleSaveHead}
                  className="px-4 py-2 bg-indigo-600 text-white rounded font-medium hover:bg-indigo-700"
                >
                  Save Head Finding
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 5: ADD PRODUCT COST STATEMENT */}
      {showProductModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-2xl w-full p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-base text-gray-900">Add Product Cost Statement (Part B / C)</h3>
              <button onClick={() => setShowProductModal(false)} className="text-gray-400 hover:text-gray-600 font-bold">✕</button>
            </div>

            <form onSubmit={handleAddProduct} className="space-y-4 text-xs">
              <div className="grid grid-cols-4 gap-3">
                <div className="col-span-2">
                  <label className="block font-medium text-gray-700 mb-1">Product Name *</label>
                  <input
                    type="text"
                    value={prodForm.productName}
                    onChange={(e) => setProdForm({ ...prodForm, productName: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                    required
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Product Code</label>
                  <input
                    type="text"
                    value={prodForm.productCode}
                    onChange={(e) => setProdForm({ ...prodForm, productCode: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                    required
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">CTA / HSN Chapter</label>
                  <input
                    type="text"
                    value={prodForm.ctaHeading}
                    onChange={(e) => setProdForm({ ...prodForm, ctaHeading: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                    required
                  />
                </div>
              </div>

              <div className="grid grid-cols-4 gap-3">
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Unit of Measure</label>
                  <input
                    type="text"
                    value={prodForm.unitOfMeasurement}
                    onChange={(e) => setProdForm({ ...prodForm, unitOfMeasurement: e.target.value })}
                    className="w-full border rounded px-3 py-1.5"
                    required
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Installed Capacity</label>
                  <input
                    type="number"
                    value={prodForm.installedCapacity}
                    onChange={(e) => setProdForm({ ...prodForm, installedCapacity: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Available Capacity</label>
                  <input
                    type="number"
                    value={prodForm.availableCapacity}
                    onChange={(e) => setProdForm({ ...prodForm, availableCapacity: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
                <div>
                  <label className="block font-medium text-gray-700 mb-1">Actual Production</label>
                  <input
                    type="number"
                    value={prodForm.actualProduction}
                    onChange={(e) => setProdForm({ ...prodForm, actualProduction: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded px-3 py-1.5"
                  />
                </div>
              </div>

              <div className="border-t pt-3">
                <span className="font-bold text-gray-800 uppercase tracking-wider text-[11px]">Cost Elements (₹)</span>
                <div className="grid grid-cols-3 gap-3 mt-2">
                  <div>
                    <label className="block text-gray-600 mb-1">Direct Materials</label>
                    <input
                      type="number"
                      value={prodForm.directMaterialsCost}
                      onChange={(e) => setProdForm({ ...prodForm, directMaterialsCost: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-2.5 py-1.5"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-600 mb-1">Direct Wages / Labor</label>
                    <input
                      type="number"
                      value={prodForm.directLaborCost}
                      onChange={(e) => setProdForm({ ...prodForm, directLaborCost: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-2.5 py-1.5"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-600 mb-1">Utilities (Power/Fuel)</label>
                    <input
                      type="number"
                      value={prodForm.utilitiesCost}
                      onChange={(e) => setProdForm({ ...prodForm, utilitiesCost: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-2.5 py-1.5"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-3 gap-3 mt-2">
                  <div>
                    <label className="block text-gray-600 mb-1">Other Direct Costs</label>
                    <input
                      type="number"
                      value={prodForm.otherDirectExpenses}
                      onChange={(e) => setProdForm({ ...prodForm, otherDirectExpenses: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-2.5 py-1.5"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-600 mb-1">Works Overheads</label>
                    <input
                      type="number"
                      value={prodForm.worksOverheads}
                      onChange={(e) => setProdForm({ ...prodForm, worksOverheads: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-2.5 py-1.5"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-600 mb-1">Quality Control</label>
                    <input
                      type="number"
                      value={prodForm.qualityControlCost}
                      onChange={(e) => setProdForm({ ...prodForm, qualityControlCost: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-2.5 py-1.5"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-3 gap-3 mt-2">
                  <div>
                    <label className="block text-gray-600 mb-1">R&D Expenses</label>
                    <input
                      type="number"
                      value={prodForm.rndExpenses}
                      onChange={(e) => setProdForm({ ...prodForm, rndExpenses: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-2.5 py-1.5"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-600 mb-1">Administration Overheads</label>
                    <input
                      type="number"
                      value={prodForm.administrationOverheads}
                      onChange={(e) => setProdForm({ ...prodForm, administrationOverheads: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-2.5 py-1.5"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-600 mb-1">Selling & Dist. Overheads</label>
                    <input
                      type="number"
                      value={prodForm.sellingAndDistributionOverheads}
                      onChange={(e) => setProdForm({ ...prodForm, sellingAndDistributionOverheads: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-2.5 py-1.5"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-3 mt-3 pt-2 border-t">
                  <div>
                    <label className="block font-medium text-gray-700 mb-1">Net Sales Realization (Gross - GST) (₹)</label>
                    <input
                      type="number"
                      value={prodForm.netSalesRealization}
                      onChange={(e) => setProdForm({ ...prodForm, netSalesRealization: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded px-3 py-1.5 font-semibold text-gray-900"
                      required
                    />
                  </div>
                </div>
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowProductModal(false)}
                  className="px-4 py-2 border rounded text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded font-medium hover:bg-indigo-700"
                >
                  Save Product Statement
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 6: ADD RECONCILIATION ITEM */}
      {showRecModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-base text-gray-900">Add P&L Reconciliation Item (Part D)</h3>
              <button onClick={() => setShowRecModal(false)} className="text-gray-400 hover:text-gray-600 font-bold">✕</button>
            </div>

            <form onSubmit={handleAddReconciliation} className="space-y-3 text-xs">
              <div>
                <label className="block font-medium text-gray-700 mb-1">Adjustment Type</label>
                <select
                  value={recForm.itemType}
                  onChange={(e) => {
                    const t = e.target.value
                    setRecForm({
                      ...recForm,
                      itemType: t,
                      impact: t.startsWith('Add_') ? 'Positive' : 'Negative',
                    })
                  }}
                  className="w-full border rounded px-3 py-1.5 bg-gray-50"
                >
                  <option value="Add_IncomeNotCosted">Add: Income in Financials but not in Cost Records (Dividend, Interest, Asset Gain)</option>
                  <option value="Deduct_ExpenseNotCosted">Less: Expense in Financials but not in Cost Records (Fines, CSR, Goodwill, Tax)</option>
                  <option value="Add_CostExpenseOvercharged">Add: Cost Expenses overcharged in Cost Accounts</option>
                  <option value="Deduct_FinancialExpenseOvercharged">Less: Financial Expenses higher than Cost Accounts (Depreciation, etc.)</option>
                  <option value="ValuationDifference">Stock Valuation Difference (Cost of Production vs Lower of Cost & NRV)</option>
                </select>
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Impact on Profit</label>
                <select
                  value={recForm.impact}
                  onChange={(e) => setRecForm({ ...recForm, impact: e.target.value })}
                  className="w-full border rounded px-3 py-1.5 bg-gray-50"
                >
                  <option value="Positive">Positive (+ Add to Cost Profit)</option>
                  <option value="Negative">Negative (- Deduct from Cost Profit)</option>
                </select>
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Particulars / Description *</label>
                <input
                  type="text"
                  value={recForm.description}
                  onChange={(e) => setRecForm({ ...recForm, description: e.target.value })}
                  placeholder="e.g. Dividend from Subsidiaries, Penalties under GST, etc."
                  className="w-full border rounded px-3 py-1.5"
                  required
                />
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Adjustment Amount (₹) *</label>
                <input
                  type="number"
                  value={recForm.amountInr}
                  onChange={(e) => setRecForm({ ...recForm, amountInr: parseFloat(e.target.value) || 0 })}
                  className="w-full border rounded px-3 py-1.5 font-semibold text-gray-900"
                  required
                />
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Financial Accounts Schedule Reference</label>
                <input
                  type="text"
                  value={recForm.financialScheduleRef || ''}
                  onChange={(e) => setRecForm({ ...recForm, financialScheduleRef: e.target.value })}
                  placeholder="e.g. Note 22 Other Income / Note 25 Other Expenses"
                  className="w-full border rounded px-3 py-1.5"
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowRecModal(false)}
                  className="px-4 py-2 border rounded text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded font-medium hover:bg-indigo-700"
                >
                  Save Item
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 7: RECORD CRA-2 */}
      {showCra2Modal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-base text-gray-900">Record Form CRA-2 Intimation Filing</h3>
              <button onClick={() => setShowCra2Modal(false)} className="text-gray-400 hover:text-gray-600 font-bold">✕</button>
            </div>

            <div className="space-y-3 text-xs">
              <div>
                <label className="block font-medium text-gray-700 mb-1">MCA Service Request Number (SRN) *</label>
                <input
                  type="text"
                  value={cra2Srn}
                  onChange={(e) => setCra2Srn(e.target.value)}
                  placeholder="e.g. F12345678"
                  className="w-full border rounded px-3 py-1.5 font-mono"
                  required
                />
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Date of Filing with MCA</label>
                <input
                  type="date"
                  value={cra2Date}
                  onChange={(e) => setCra2Date(e.target.value)}
                  className="w-full border rounded px-3 py-1.5"
                  required
                />
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Filing Status</label>
                <select
                  value={cra2Status}
                  onChange={(e) => setCra2Status(e.target.value)}
                  className="w-full border rounded px-3 py-1.5 bg-gray-50"
                >
                  <option value="FiledOnTime">Filed On Time</option>
                  <option value="FiledDelayed">Filed Delayed (with additional MCA fee)</option>
                </select>
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCra2Modal(false)}
                  className="px-4 py-2 border rounded text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleSaveCra2}
                  className="px-4 py-2 bg-indigo-600 text-white rounded font-medium hover:bg-indigo-700"
                >
                  Save CRA-2 Details
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 8: RECORD CRA-4 */}
      {showCra4Modal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-base text-gray-900">Record Form CRA-4 MCA Submission</h3>
              <button onClick={() => setShowCra4Modal(false)} className="text-gray-400 hover:text-gray-600 font-bold">✕</button>
            </div>

            <div className="space-y-3 text-xs">
              <div>
                <label className="block font-medium text-gray-700 mb-1">MCA CRA-4 SRN *</label>
                <input
                  type="text"
                  value={cra4Srn}
                  onChange={(e) => setCra4Srn(e.target.value)}
                  placeholder="e.g. G98765432"
                  className="w-full border rounded px-3 py-1.5 font-mono"
                  required
                />
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Date of Submission</label>
                <input
                  type="date"
                  value={cra4Date}
                  onChange={(e) => setCra4Date(e.target.value)}
                  className="w-full border rounded px-3 py-1.5"
                  required
                />
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Filing Status</label>
                <select
                  value={cra4Status}
                  onChange={(e) => setCra4Status(e.target.value)}
                  className="w-full border rounded px-3 py-1.5 bg-gray-50"
                >
                  <option value="FiledOnTime">Filed On Time</option>
                  <option value="FiledDelayed">Filed Delayed</option>
                </select>
              </div>

              <div className="flex items-center gap-2 pt-1">
                <input
                  type="checkbox"
                  id="cra4XbrlCheck"
                  checked={cra4Xbrl}
                  onChange={(e) => setCra4Xbrl(e.target.checked)}
                />
                <label htmlFor="cra4XbrlCheck" className="text-gray-700 cursor-pointer">
                  Cost Audit XBRL Document validated against MCA taxonomy
                </label>
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCra4Modal(false)}
                  className="px-4 py-2 border rounded text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="button"
                  onClick={handleSaveCra4}
                  className="px-4 py-2 bg-emerald-600 text-white rounded font-medium hover:bg-emerald-700"
                >
                  Save CRA-4 Submission
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* MODAL 9: CERTIFY WITH UDIN */}
      {showCertifyModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-md w-full p-6 space-y-4">
            <div className="flex justify-between items-center border-b pb-3">
              <h3 className="font-bold text-base text-gray-900">Sign & Certify Cost Audit Report (Form CRA-3)</h3>
              <button onClick={() => setShowCertifyModal(false)} className="text-gray-400 hover:text-gray-600 font-bold">✕</button>
            </div>

            <form onSubmit={handleCertify} className="space-y-3 text-xs">
              <div className="bg-amber-50 p-3 rounded border border-amber-200 text-amber-800">
                <strong>Statutory UDIN Requirement</strong>: Under ICMAI / ICAI guidelines, every statutory Cost Audit Report in Form CRA-3 must carry an 18-digit Unique Document Identification Number (UDIN).
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">18-Digit UDIN *</label>
                <input
                  type="text"
                  maxLength={18}
                  value={certUdin}
                  onChange={(e) => setCertUdin(e.target.value.toUpperCase())}
                  placeholder="e.g. 24123456AAAAAA1234"
                  className="w-full border rounded px-3 py-1.5 font-mono text-sm uppercase tracking-wider font-semibold"
                  required
                />
                <span className="text-[10px] text-gray-500">{certUdin.length} / 18 characters entered</span>
              </div>

              <div>
                <label className="block font-medium text-gray-700 mb-1">Signing Cost Accountant *</label>
                <input
                  type="text"
                  value={certSigner}
                  onChange={(e) => setCertSigner(e.target.value)}
                  className="w-full border rounded px-3 py-1.5"
                  required
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyModal(false)}
                  className="px-4 py-2 border rounded text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={certUdin.trim().length !== 18}
                  className="px-4 py-2 bg-emerald-600 text-white rounded font-medium hover:bg-emerald-700 disabled:opacity-50"
                >
                  Certify with UDIN
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
export default CostAuditPage
