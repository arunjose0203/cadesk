import { useState, useEffect, useMemo } from 'react'
import {
  internalAuditApi,
  api,
  type InternalAuditEngagementDto,
  type RcmControlItemDto,
  type InternalAuditFindingDto,
  type InternalAuditSummaryDto,
  type SaveInternalAuditEngagementRequest,
  type SaveRcmControlItemRequest,
  type SaveInternalAuditFindingRequest,
  type UpdateFindingStatusRequest,
  type Client
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

const PROCESS_CYCLES = [
  { code: 'P2P', label: 'Procure to Pay (P2P)' },
  { code: 'O2C', label: 'Order to Cash (O2C)' },
  { code: 'H2R', label: 'Hire to Retire / Payroll (H2R)' },
  { code: 'R2R', label: 'Record to Report (R2R)' },
  { code: 'Treasury', label: 'Treasury & Cash Management' },
  { code: 'ITGC', label: 'IT General Controls (ITGC)' },
  { code: 'Inventory', label: 'Inventory & Warehousing' },
  { code: 'FixedAssets', label: 'Fixed Assets & Capitalization' }
]

const IFC_CLASSIFICATIONS = [
  { code: 'MaterialWeakness', label: 'Material Weakness (Significant deficiency + material misstatement risk)' },
  { code: 'SignificantDeficiency', label: 'Significant Deficiency (Merits attention by Audit Committee)' },
  { code: 'OperationalDeficiency', label: 'Operational Deficiency (Internal control deficiency not impacting FS)' },
  { code: 'ProcessImprovement', label: 'Process Improvement Opportunity' }
]

const TESTING_METHODS = ['Inspection', 'Reperformance', 'Observation', 'Inquiry']

export function InternalAuditPage() {
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')

  // Engagements
  const [engagements, setEngagements] = useState<InternalAuditEngagementDto[]>([])
  const [selectedEngId, setSelectedEngId] = useState<string>('')

  // Data states
  const [summary, setSummary] = useState<InternalAuditSummaryDto | null>(null)
  const [controls, setControls] = useState<RcmControlItemDto[]>([])
  const [findings, setFindings] = useState<InternalAuditFindingDto[]>([])
  const [currentEngagement, setCurrentEngagement] = useState<InternalAuditEngagementDto | null>(null)

  const [loading, setLoading] = useState(true)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)
  const [successMsg, setSuccessMsg] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<'matrix' | 'findings' | 'heatmap' | 'report'>('matrix')

  // Filters
  const [cycleFilter, setCycleFilter] = useState('All')
  const [searchQuery, setSearchQuery] = useState('')
  const [severityFilter, setSeverityFilter] = useState('All')
  const [statusFilter, setStatusFilter] = useState('All')

  // Modals
  const [showEngagementModal, setShowEngagementModal] = useState(false)
  const [showControlModal, setShowControlModal] = useState(false)
  const [showFindingModal, setShowFindingModal] = useState(false)
  const [showStatusModal, setShowStatusModal] = useState(false)

  // Target items for editing
  const [editingControl, setEditingControl] = useState<RcmControlItemDto | null>(null)
  const [editingFinding, setEditingFinding] = useState<InternalAuditFindingDto | null>(null)
  const [targetFindingForStatus, setTargetFindingForStatus] = useState<InternalAuditFindingDto | null>(null)

  // Form states
  const [engForm, setEngForm] = useState<SaveInternalAuditEngagementRequest & { populateTemplate: boolean }>({
    clientId: '',
    financialYear: '2024-25',
    auditPeriod: 'H2 FY 2024-25',
    title: 'Risk Based Internal Audit & IFC-FR Assessment',
    engagementLeadName: 'CA Partner',
    auditScopeDescription: 'Review of design and operating effectiveness of Internal Financial Controls over Financial Reporting (IFC-FR) as per Companies Act Sec 138 & 143(3)(i).',
    status: 'InProgress',
    populateTemplate: true
  })

  const [controlForm, setControlForm] = useState<SaveRcmControlItemRequest>({
    controlCode: '',
    processCycle: 'P2P',
    subProcess: '',
    riskDescription: '',
    riskCategory: 'FinancialReporting',
    inherentRiskRating: 'High',
    controlActivity: '',
    controlNature: 'Preventive',
    controlFrequency: 'Transactional',
    controlType: 'Automated',
    controlOwner: '',
    assertions: 'Accuracy, Completeness',
    walkthroughPerformed: true,
    walkthroughDate: new Date().toISOString().substring(0, 10),
    walkthroughReference: 'WP-WT-01',
    designEffectiveness: 'Effective',
    designNotes: '',
    testingMethod: 'Inspection',
    sampleSize: 25,
    exceptionsCount: 0,
    operatingEffectiveness: 'OperatingEffectively',
    testingNotes: '',
    testedAt: new Date().toISOString().substring(0, 10),
    testedBy: ''
  })

  const [findingForm, setFindingForm] = useState<SaveInternalAuditFindingRequest>({
    controlItemId: undefined,
    controlCode: '',
    findingReference: '',
    title: '',
    processCycle: 'P2P',
    severity: 'High',
    detailedObservation: '',
    rootCause: '',
    estimatedRiskAmount: 0,
    ifcClassification: 'SignificantDeficiency',
    auditorRecommendation: '',
    managementActionPlan: '',
    actionOwner: '',
    targetCompletionDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().substring(0, 10),
    implementationStatus: 'Open'
  })

  const [statusForm, setStatusForm] = useState<UpdateFindingStatusRequest>({
    implementationStatus: 'InProgress',
    managementActionPlan: '',
    targetCompletionDate: undefined
  })

  // Load clients
  useEffect(() => {
    const initClients = async () => {
      try {
        const res = await api<ClientPage>('/api/clients?pageSize=100').catch(() => ({ items: [], total: 0 }))
        setClients(res.items || [])
        if (res.items && res.items.length > 0) {
          setSelectedClientId(prev => prev || res.items[0].id)
        }
      } catch (err) {
        console.error(err)
      }
    }
    initClients()
  }, [])

  // Load engagements when client/FY changes
  useEffect(() => {
    if (!selectedClientId) return
    const fetchEngagements = async () => {
      try {
        setLoading(true)
        const engList = await internalAuditApi.listEngagements(selectedClientId, selectedFy)
        setEngagements(engList)
        if (engList.length > 0) {
          // preserve selection if valid
          setSelectedEngId(prev => (prev && engList.some(e => e.id === prev) ? prev : engList[0].id))
        } else {
          setSelectedEngId('')
          setCurrentEngagement(null)
          setControls([])
          setFindings([])
        }
      } catch (err: unknown) {
        setErrorMsg(err instanceof Error ? err.message : 'Failed to load engagements')
      } finally {
        setLoading(false)
      }
    }
    fetchEngagements()
  }, [selectedClientId, selectedFy])

  // Load controls & findings for selected engagement
  useEffect(() => {
    if (!selectedEngId) return
    const loadEngagementDetails = async () => {
      try {
        setLoading(true)
        setErrorMsg(null)
        const [eng, sum, ctrlList, fList] = await Promise.all([
          internalAuditApi.getEngagement(selectedEngId),
          internalAuditApi.getSummary(selectedClientId, selectedFy),
          internalAuditApi.listControls(selectedEngId),
          internalAuditApi.listFindings(selectedEngId)
        ])
        setCurrentEngagement(eng)
        setSummary(sum)
        setControls(ctrlList)
        setFindings(fList)
      } catch (err: unknown) {
        setErrorMsg(err instanceof Error ? err.message : 'Failed to load engagement details')
      } finally {
        setLoading(false)
      }
    }
    loadEngagementDetails()
  }, [selectedEngId, selectedClientId, selectedFy])

  // Notification helper
  const notifySuccess = (msg: string) => {
    setSuccessMsg(msg)
    setTimeout(() => setSuccessMsg(null), 4000)
  }

  // Reload current data
  const refreshData = async () => {
    if (!selectedEngId) return
    try {
      const [eng, sum, ctrlList, fList] = await Promise.all([
        internalAuditApi.getEngagement(selectedEngId),
        internalAuditApi.getSummary(selectedClientId, selectedFy),
        internalAuditApi.listControls(selectedEngId),
        internalAuditApi.listFindings(selectedEngId)
      ])
      setCurrentEngagement(eng)
      setSummary(sum)
      setControls(ctrlList)
      setFindings(fList)
    } catch (e) {
      console.error(e)
    }
  }

  // Handle Save Engagement
  const handleSaveEngagement = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setErrorMsg(null)
      const res = await internalAuditApi.createEngagement(
        {
          clientId: selectedClientId,
          financialYear: engForm.financialYear,
          auditPeriod: engForm.auditPeriod,
          title: engForm.title,
          engagementLeadName: engForm.engagementLeadName,
          auditScopeDescription: engForm.auditScopeDescription,
          status: engForm.status
        },
        engForm.populateTemplate
      )
      setShowEngagementModal(false)
      notifySuccess('Internal Audit Engagement created successfully!')
      // Refresh engagements
      const engList = await internalAuditApi.listEngagements(selectedClientId, selectedFy)
      setEngagements(engList)
      setSelectedEngId(res.id)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error creating engagement')
    }
  }

  // Open Edit Control Modal
  const openEditControlModal = (c?: RcmControlItemDto) => {
    if (c) {
      setEditingControl(c)
      setControlForm({
        controlCode: c.controlCode,
        processCycle: c.processCycle,
        subProcess: c.subProcess,
        riskDescription: c.riskDescription,
        riskCategory: c.riskCategory,
        inherentRiskRating: c.inherentRiskRating,
        controlActivity: c.controlActivity,
        controlNature: c.controlNature,
        controlFrequency: c.controlFrequency,
        controlType: c.controlType,
        controlOwner: c.controlOwner,
        assertions: c.assertions,
        walkthroughPerformed: c.walkthroughPerformed,
        walkthroughDate: c.walkthroughDate ? c.walkthroughDate.substring(0, 10) : '',
        walkthroughReference: c.walkthroughReference,
        designEffectiveness: c.designEffectiveness,
        designNotes: c.designNotes,
        testingMethod: c.testingMethod,
        sampleSize: c.sampleSize,
        exceptionsCount: c.exceptionsCount,
        operatingEffectiveness: c.operatingEffectiveness,
        testingNotes: c.testingNotes,
        testedAt: c.testedAt ? c.testedAt.substring(0, 10) : '',
        testedBy: c.testedBy
      })
    } else {
      setEditingControl(null)
      setControlForm({
        controlCode: `CTL-${(controls.length + 1).toString().padStart(3, '0')}`,
        processCycle: 'P2P',
        subProcess: '',
        riskDescription: '',
        riskCategory: 'FinancialReporting',
        inherentRiskRating: 'High',
        controlActivity: '',
        controlNature: 'Preventive',
        controlFrequency: 'Transactional',
        controlType: 'Automated',
        controlOwner: '',
        assertions: 'Accuracy, Completeness',
        walkthroughPerformed: true,
        walkthroughDate: new Date().toISOString().substring(0, 10),
        walkthroughReference: 'WP-WT-01',
        designEffectiveness: 'Effective',
        designNotes: '',
        testingMethod: 'Inspection',
        sampleSize: 25,
        exceptionsCount: 0,
        operatingEffectiveness: 'OperatingEffectively',
        testingNotes: '',
        testedAt: new Date().toISOString().substring(0, 10),
        testedBy: ''
      })
    }
    setShowControlModal(true)
  }

  // Handle Save Control
  const handleSaveControl = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedEngId) return
    try {
      setErrorMsg(null)
      if (editingControl) {
        await internalAuditApi.updateControl(selectedEngId, editingControl.id, controlForm)
        notifySuccess(`Control ${controlForm.controlCode} updated successfully`)
      } else {
        await internalAuditApi.createControl(selectedEngId, controlForm)
        notifySuccess(`Control ${controlForm.controlCode} added to RCM`)
      }
      setShowControlModal(false)
      await refreshData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error saving control')
    }
  }

  // Handle Delete Control
  const handleDeleteControl = async (id: string, code: string) => {
    if (!selectedEngId) return
    if (!confirm(`Are you sure you want to remove control ${code} from RCM?`)) return
    try {
      await internalAuditApi.deleteControl(selectedEngId, id)
      notifySuccess(`Control ${code} removed`)
      await refreshData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error deleting control')
    }
  }

  // Open Log Finding Modal
  const openFindingModal = (f?: InternalAuditFindingDto, fromControl?: RcmControlItemDto) => {
    if (f) {
      setEditingFinding(f)
      setFindingForm({
        controlItemId: f.controlItemId,
        controlCode: f.controlCode,
        findingReference: f.findingReference,
        title: f.title,
        processCycle: f.processCycle,
        severity: f.severity,
        detailedObservation: f.detailedObservation,
        rootCause: f.rootCause,
        estimatedRiskAmount: f.estimatedRiskAmount || 0,
        ifcClassification: f.ifcClassification,
        auditorRecommendation: f.auditorRecommendation,
        managementActionPlan: f.managementActionPlan,
        actionOwner: f.actionOwner,
        targetCompletionDate: f.targetCompletionDate ? f.targetCompletionDate.substring(0, 10) : '',
        implementationStatus: f.implementationStatus
      })
    } else {
      setEditingFinding(null)
      setFindingForm({
        controlItemId: fromControl?.id,
        controlCode: fromControl?.controlCode || '',
        findingReference: `IA-FIND-${(findings.length + 1).toString().padStart(3, '0')}`,
        title: fromControl ? `Control Deficiency in ${fromControl.controlCode} (${fromControl.subProcess})` : '',
        processCycle: fromControl?.processCycle || 'P2P',
        severity: 'High',
        detailedObservation: fromControl?.testingNotes || '',
        rootCause: '',
        estimatedRiskAmount: 0,
        ifcClassification: 'SignificantDeficiency',
        auditorRecommendation: '',
        managementActionPlan: '',
        actionOwner: '',
        targetCompletionDate: new Date(Date.now() + 30 * 24 * 60 * 60 * 1000).toISOString().substring(0, 10),
        implementationStatus: 'Open'
      })
    }
    setShowFindingModal(true)
  }

  // Handle Save Finding
  const handleSaveFinding = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedEngId) return
    try {
      setErrorMsg(null)
      if (editingFinding) {
        await internalAuditApi.updateFinding(selectedEngId, editingFinding.id, findingForm)
        notifySuccess(`Finding ${findingForm.findingReference} updated`)
      } else {
        await internalAuditApi.createFinding(selectedEngId, findingForm)
        notifySuccess(`Finding ${findingForm.findingReference} logged successfully`)
      }
      setShowFindingModal(false)
      await refreshData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error saving finding')
    }
  }

  // Open Status Update Modal
  const openStatusModal = (f: InternalAuditFindingDto) => {
    setTargetFindingForStatus(f)
    setStatusForm({
      implementationStatus: f.implementationStatus,
      managementActionPlan: f.managementActionPlan,
      targetCompletionDate: f.targetCompletionDate ? f.targetCompletionDate.substring(0, 10) : undefined
    })
    setShowStatusModal(true)
  }

  // Handle Update Finding Status
  const handleUpdateFindingStatus = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedEngId || !targetFindingForStatus) return
    try {
      setErrorMsg(null)
      await internalAuditApi.updateFindingStatus(selectedEngId, targetFindingForStatus.id, statusForm)
      setShowStatusModal(false)
      notifySuccess(`Status for ${targetFindingForStatus.findingReference} updated to ${statusForm.implementationStatus}`)
      await refreshData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error updating finding status')
    }
  }

  // Delete Finding
  const handleDeleteFinding = async (id: string, ref: string) => {
    if (!selectedEngId) return
    if (!confirm(`Are you sure you want to delete finding ${ref}?`)) return
    try {
      await internalAuditApi.deleteFinding(selectedEngId, id)
      notifySuccess(`Finding ${ref} deleted`)
      await refreshData()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error deleting finding')
    }
  }

  // Generate Executive Report
  const handleGenerateReport = async () => {
    if (!selectedEngId) return
    try {
      setErrorMsg(null)
      const res = await internalAuditApi.generateReport(selectedEngId)
      setCurrentEngagement(res)
      notifySuccess('ICAI SIA & Sec 138/143(3)(i) Executive Report generated!')
      setActiveTab('report')
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error generating report')
    }
  }

  // Filtered Controls
  const filteredControls = useMemo(() => {
    return controls.filter(c => {
      const matchesCycle = cycleFilter === 'All' || c.processCycle === cycleFilter
      const q = searchQuery.toLowerCase().trim()
      const matchesSearch =
        !q ||
        c.controlCode.toLowerCase().includes(q) ||
        c.subProcess.toLowerCase().includes(q) ||
        c.riskDescription.toLowerCase().includes(q) ||
        c.controlActivity.toLowerCase().includes(q) ||
        c.controlOwner.toLowerCase().includes(q)
      return matchesCycle && matchesSearch
    })
  }, [controls, cycleFilter, searchQuery])

  // Filtered Findings
  const filteredFindings = useMemo(() => {
    return findings.filter(f => {
      const matchesSev = severityFilter === 'All' || f.severity === severityFilter
      const matchesStat = statusFilter === 'All' || f.implementationStatus === statusFilter
      const q = searchQuery.toLowerCase().trim()
      const matchesSearch =
        !q ||
        f.findingReference.toLowerCase().includes(q) ||
        f.title.toLowerCase().includes(q) ||
        f.controlCode.toLowerCase().includes(q) ||
        f.detailedObservation.toLowerCase().includes(q)
      return matchesSev && matchesStat && matchesSearch
    })
  }, [findings, severityFilter, statusFilter, searchQuery])

  // Cycle Metrics for Heatmap
  const cycleMetrics = useMemo(() => {
    return PROCESS_CYCLES.map(p => {
      const cycleControls = controls.filter(c => c.processCycle === p.code)
      const total = cycleControls.length
      const effective = cycleControls.filter(
        c => (c.operatingEffectiveness === 'OperatingEffectively' || c.operatingEffectiveness === 'Effective') && c.exceptionsCount === 0
      ).length
      const ineffective = cycleControls.filter(
        c => c.operatingEffectiveness === 'Ineffective' || c.exceptionsCount > 0
      ).length
      const untested = cycleControls.filter(c => c.operatingEffectiveness === 'NotTested').length
      const cycleScore = total > 0 ? Math.round((effective / total) * 100) : 0
      const cycleFindings = findings.filter(f => f.processCycle === p.code).length
      return {
        ...p,
        total,
        effective,
        ineffective,
        untested,
        cycleScore,
        cycleFindings
      }
    })
  }, [controls, findings])

  // Health Score Color
  const getHealthBadge = (score: number) => {
    if (score >= 80) return 'text-emerald-700 bg-emerald-50 border-emerald-200'
    if (score >= 60) return 'text-amber-700 bg-amber-50 border-amber-200'
    return 'text-rose-700 bg-rose-50 border-rose-200'
  }

  // Severity Badge
  const getSeverityBadge = (sev: string) => {
    switch (sev) {
      case 'Critical':
        return 'bg-red-100 text-red-800 border-red-300'
      case 'High':
        return 'bg-amber-100 text-amber-800 border-amber-300'
      case 'Medium':
        return 'bg-blue-100 text-blue-800 border-blue-300'
      default:
        return 'bg-slate-100 text-slate-800 border-slate-300'
    }
  }

  // Status Badge
  const getStatusBadge = (st: string) => {
    switch (st) {
      case 'Implemented':
        return 'bg-emerald-100 text-emerald-800'
      case 'InProgress':
        return 'bg-blue-100 text-blue-800'
      case 'Overdue':
        return 'bg-rose-100 text-rose-800'
      default:
        return 'bg-amber-100 text-amber-800'
    }
  }

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Page Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-200 pb-5">
        <div>
          <div className="flex items-center gap-3">
            <span className="p-2 bg-indigo-50 border border-indigo-200 rounded-lg text-indigo-700">
              {/* Shield / Audit Icon */}
              <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
              </svg>
            </span>
            <div>
              <h1 className="text-2xl font-bold text-slate-900">
                Internal Audit Risk & Control Matrix (RCM) & IFC Testing Desk
              </h1>
              <p className="text-sm text-slate-500">
                ICAI Standards on Internal Audit (SIA) • Companies Act Sec 138 & 143(3)(i) • Internal Financial Controls (IFC-FR)
              </p>
            </div>
          </div>
        </div>

        {/* Action Controls */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Client Selector */}
          <select
            value={selectedClientId}
            onChange={e => setSelectedClientId(e.target.value)}
            className="text-sm font-medium border border-slate-300 rounded-lg px-3 py-2 bg-white text-slate-700 shadow-sm focus:ring-2 focus:ring-indigo-500 focus:outline-none"
          >
            {clients.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>

          {/* FY Selector */}
          <select
            value={selectedFy}
            onChange={e => setSelectedFy(e.target.value)}
            className="text-sm font-medium border border-slate-300 rounded-lg px-3 py-2 bg-white text-slate-700 shadow-sm focus:ring-2 focus:ring-indigo-500 focus:outline-none"
          >
            <option value="2025-26">FY 2025-26</option>
            <option value="2024-25">FY 2024-25</option>
            <option value="2023-24">FY 2023-24</option>
          </select>

          {/* Engagement Picker */}
          {engagements.length > 0 && (
            <select
              value={selectedEngId}
              onChange={e => setSelectedEngId(e.target.value)}
              className="text-sm font-medium border border-indigo-300 bg-indigo-50/50 rounded-lg px-3 py-2 text-indigo-900 shadow-sm focus:ring-2 focus:ring-indigo-500 focus:outline-none max-w-xs truncate"
            >
              {engagements.map(eng => (
                <option key={eng.id} value={eng.id}>
                  {eng.auditPeriod} - {eng.title}
                </option>
              ))}
            </select>
          )}

          <button
            onClick={() => {
              setEngForm({
                clientId: selectedClientId,
                financialYear: selectedFy,
                auditPeriod: 'H2 FY 2024-25',
                title: 'Risk Based Internal Audit & IFC-FR Assessment',
                engagementLeadName: 'CA Lead Auditor',
                auditScopeDescription: 'Review of design and operating effectiveness of Internal Financial Controls over Financial Reporting (IFC-FR) as per Companies Act Sec 138 & 143(3)(i).',
                status: 'InProgress',
                populateTemplate: true
              })
              setShowEngagementModal(true)
            }}
            className="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-lg shadow-sm transition"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
            </svg>
            New Engagement
          </button>
        </div>
      </div>

      {/* Notifications */}
      {loading && (
        <div className="flex items-center gap-2 text-xs text-indigo-600 bg-indigo-50/50 p-2.5 rounded-lg border border-indigo-100">
          <svg className="animate-spin h-4 w-4 text-indigo-600" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <span>Syncing RCM control matrix and testing logs...</span>
        </div>
      )}
      {successMsg && (
        <div className="p-3 bg-emerald-50 border border-emerald-200 text-emerald-800 rounded-lg text-sm flex items-center justify-between">
          <span>{successMsg}</span>
          <button onClick={() => setSuccessMsg(null)} className="text-emerald-600 hover:text-emerald-900">&times;</button>
        </div>
      )}
      {errorMsg && (
        <div className="p-3 bg-rose-50 border border-rose-200 text-rose-800 rounded-lg text-sm flex items-center justify-between">
          <span>{errorMsg}</span>
          <button onClick={() => setErrorMsg(null)} className="text-rose-600 hover:text-rose-900">&times;</button>
        </div>
      )}

      {/* KPI Dashboard */}
      {summary && (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-6 gap-4">
          {/* Health Score Gauge */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm col-span-1 lg:col-span-2">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">Overall IFC Health Score</p>
                <div className="flex items-baseline gap-2 mt-1">
                  <span className="text-3xl font-extrabold text-slate-900">
                    {summary.overallHealthScore.toFixed(1)}%
                  </span>
                  <span className={`text-xs px-2 py-0.5 rounded-full border font-medium ${getHealthBadge(summary.overallHealthScore)}`}>
                    {summary.overallHealthScore >= 80 ? 'Operating Effectively' : summary.overallHealthScore >= 60 ? 'Moderate Gaps' : 'Material Weakness Risk'}
                  </span>
                </div>
                <p className="text-xs text-slate-400 mt-1">
                  {summary.totalControlsTested} Controls Tested across Cycles
                </p>
              </div>
              <div className="relative w-14 h-14 flex items-center justify-center">
                <svg className="w-14 h-14 transform -rotate-90">
                  <circle cx="28" cy="28" r="22" stroke="currentColor" strokeWidth="5" className="text-slate-100" fill="transparent" />
                  <circle
                    cx="28"
                    cy="28"
                    r="22"
                    stroke="currentColor"
                    strokeWidth="5"
                    strokeDasharray={138}
                    strokeDashoffset={138 - (138 * Math.min(summary.overallHealthScore, 100)) / 100}
                    className={summary.overallHealthScore >= 80 ? 'text-emerald-500' : summary.overallHealthScore >= 60 ? 'text-amber-500' : 'text-rose-500'}
                    fill="transparent"
                  />
                </svg>
                <span className="absolute text-xs font-bold text-slate-700">
                  {Math.round(summary.overallHealthScore)}%
                </span>
              </div>
            </div>
          </div>

          {/* Controls Tested */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">RCM Controls</p>
            <p className="text-2xl font-bold text-slate-900 mt-1">{controls.length}</p>
            <p className="text-xs text-slate-500 mt-1">
              <span className="text-emerald-600 font-semibold">{currentEngagement?.effectiveControls ?? 0}</span> Effective / <span className="text-rose-600 font-semibold">{(controls.length - (currentEngagement?.effectiveControls ?? 0))}</span> Deficient
            </p>
          </div>

          {/* Deficiencies */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">Total Findings</p>
            <p className="text-2xl font-bold text-slate-900 mt-1">{summary.totalDeficiencies}</p>
            <p className="text-xs text-slate-500 mt-1">
              <span className="text-rose-600 font-semibold">{summary.criticalDeficiencies}</span> Critical • <span className="text-amber-600 font-semibold">{summary.materialWeaknesses}</span> Mat. Weakness
            </p>
          </div>

          {/* Financial Exposure */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">Risk Exposure (₹)</p>
            <p className="text-2xl font-bold text-slate-900 mt-1">
              ₹{(summary.totalFinancialExposure / 100000).toLocaleString('en-IN', { maximumFractionDigits: 1 })} L
            </p>
            <p className="text-xs text-slate-500 mt-1">Quantified audit exposure</p>
          </div>

          {/* Open Action Plans */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">Remediation Status</p>
            <p className="text-2xl font-bold text-amber-600 mt-1">{summary.openActionsCount}</p>
            <p className="text-xs text-slate-500 mt-1">Open / In-Progress actions</p>
          </div>
        </div>
      )}

      {/* Main Tabs Navigation */}
      <div className="border-b border-slate-200">
        <nav className="flex space-x-6">
          <button
            onClick={() => setActiveTab('matrix')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'matrix'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M3 10h18M3 14h18m-9-4v8m-7 0h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v8a2 2 0 002 2z" />
            </svg>
            Risk & Control Matrix (RCM)
            <span className="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 font-semibold">{controls.length}</span>
          </button>

          <button
            onClick={() => setActiveTab('findings')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'findings'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            Deficiencies & Action Tracker
            <span className="text-xs px-2 py-0.5 rounded-full bg-amber-100 text-amber-700 font-semibold">{findings.length}</span>
          </button>

          <button
            onClick={() => setActiveTab('heatmap')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'heatmap'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
            </svg>
            Cycle-wise Heatmap & Sec 138 Scope
          </button>

          <button
            onClick={() => setActiveTab('report')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'report'
                ? 'border-indigo-600 text-indigo-600'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
            </svg>
            Executive IFC Report
          </button>
        </nav>
      </div>

      {/* TAB 1: RCM CONTROLS MATRIX */}
      {activeTab === 'matrix' && (
        <div className="space-y-4">
          {/* Filters & Action Bar */}
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <div className="flex flex-wrap items-center gap-3">
              {/* Process Cycle Filter */}
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold text-slate-500 uppercase">Cycle:</span>
                <select
                  value={cycleFilter}
                  onChange={e => setCycleFilter(e.target.value)}
                  className="text-sm border border-slate-300 rounded-lg px-2.5 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                >
                  <option value="All">All Business Cycles</option>
                  {PROCESS_CYCLES.map(p => (
                    <option key={p.code} value={p.code}>{p.label}</option>
                  ))}
                </select>
              </div>

              {/* Search input */}
              <div className="relative">
                <input
                  type="text"
                  placeholder="Search control code, process, risk..."
                  value={searchQuery}
                  onChange={e => setSearchQuery(e.target.value)}
                  className="text-sm pl-8 pr-3 py-1.5 border border-slate-300 rounded-lg focus:outline-none focus:ring-1 focus:ring-indigo-500 w-64"
                />
                <svg className="w-4 h-4 text-slate-400 absolute left-2.5 top-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => openEditControlModal()}
                disabled={!selectedEngId}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 rounded-lg shadow-sm transition"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
                </svg>
                Add Control to RCM
              </button>
            </div>
          </div>

          {/* RCM Controls Table */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-slate-200 text-left text-xs">
                <thead className="bg-slate-50 text-slate-600 font-semibold uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">Control Code & Cycle</th>
                    <th className="px-4 py-3">Sub-Process & Inherent Risk</th>
                    <th className="px-4 py-3">Control Activity & Nature</th>
                    <th className="px-4 py-3">Design Review (Walkthrough)</th>
                    <th className="px-4 py-3">Operating Effectiveness (ToC)</th>
                    <th className="px-4 py-3">Residual Risk</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 text-slate-700">
                  {filteredControls.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="px-4 py-8 text-center text-slate-400">
                        No controls found for this criteria. Click "Add Control to RCM" or select an engagement.
                      </td>
                    </tr>
                  ) : (
                    filteredControls.map(c => {
                      const isEffective = (c.operatingEffectiveness === 'OperatingEffectively' || c.operatingEffectiveness === 'Effective') && c.exceptionsCount === 0
                      return (
                        <tr key={c.id} className="hover:bg-slate-50 transition">
                          {/* Code & Cycle */}
                          <td className="px-4 py-3 align-top font-medium text-slate-900 whitespace-nowrap">
                            <span className="font-mono text-indigo-700 font-bold">{c.controlCode}</span>
                            <div className="text-xs text-slate-500">{c.processCycle}</div>
                            <span className="inline-block mt-1 text-[10px] px-1.5 py-0.5 rounded bg-slate-100 text-slate-600 border">
                              {c.controlType}
                            </span>
                          </td>

                          {/* Sub-process & Inherent Risk */}
                          <td className="px-4 py-3 align-top max-w-xs">
                            <div className="font-semibold text-slate-800">{c.subProcess}</div>
                            <p className="text-slate-500 line-clamp-2 mt-0.5 text-[11px]">{c.riskDescription}</p>
                            <div className="mt-1 flex items-center gap-1.5">
                              <span className={`text-[10px] font-bold px-1.5 py-0.2 rounded border ${getSeverityBadge(c.inherentRiskRating)}`}>
                                Inherent: {c.inherentRiskRating}
                              </span>
                              <span className="text-[10px] text-slate-400">Owner: {c.controlOwner || 'Unassigned'}</span>
                            </div>
                          </td>

                          {/* Control Activity */}
                          <td className="px-4 py-3 align-top max-w-sm">
                            <p className="text-slate-700 text-[11px] line-clamp-3">{c.controlActivity}</p>
                            <div className="mt-1 flex items-center gap-2 text-[10px] text-slate-400">
                              <span>Nature: <b>{c.controlNature}</b></span>
                              <span>•</span>
                              <span>Freq: <b>{c.controlFrequency}</b></span>
                            </div>
                          </td>

                          {/* Design Review */}
                          <td className="px-4 py-3 align-top whitespace-nowrap">
                            <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${
                              c.designEffectiveness === 'Effective' ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-rose-50 text-rose-700 border-rose-200'
                            }`}>
                              {c.designEffectiveness === 'Effective' ? '✓ Effective Design' : '✗ Deficient Design'}
                            </span>
                            <div className="text-[10px] text-slate-400 mt-1">
                              WT Ref: {c.walkthroughReference || 'N/A'}
                            </div>
                          </td>

                          {/* Operating Effectiveness (ToC) */}
                          <td className="px-4 py-3 align-top whitespace-nowrap">
                            <span className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold border ${
                              isEffective ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-rose-50 text-rose-700 border-rose-200'
                            }`}>
                              {isEffective ? '✓ Operating Effectively' : '✗ Ineffective / Exceptions'}
                            </span>
                            <div className="text-[10px] text-slate-500 mt-1">
                              <span>Method: {c.testingMethod}</span> • <span>Sample: {c.sampleSize}</span>
                            </div>
                            <div className={`text-[10px] font-semibold mt-0.5 ${c.exceptionsCount > 0 ? 'text-rose-600 font-bold' : 'text-slate-400'}`}>
                              Exceptions: {c.exceptionsCount}
                            </div>
                          </td>

                          {/* Residual Risk */}
                          <td className="px-4 py-3 align-top whitespace-nowrap">
                            <span className={`inline-block text-[11px] font-bold px-2 py-0.5 rounded border ${
                              c.residualRiskRating === 'Low' ? 'bg-emerald-100 text-emerald-800 border-emerald-300' : 'bg-rose-100 text-rose-800 border-rose-300'
                            }`}>
                              {c.residualRiskRating} Residual Risk
                            </span>
                            {c.hasFinding && (
                              <div className="mt-1">
                                <button
                                  onClick={() => openFindingModal(undefined, c)}
                                  className="text-[10px] text-rose-600 hover:text-rose-800 font-semibold underline flex items-center gap-1"
                                >
                                  <span>Log Finding</span>
                                  <svg className="w-3 h-3" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
                                  </svg>
                                </button>
                              </div>
                            )}
                          </td>

                          {/* Actions */}
                          <td className="px-4 py-3 align-top text-right whitespace-nowrap">
                            <div className="flex items-center justify-end gap-1.5">
                              <button
                                onClick={() => openEditControlModal(c)}
                                className="px-2 py-1 text-xs text-indigo-600 hover:text-indigo-900 bg-indigo-50 hover:bg-indigo-100 rounded transition font-medium"
                              >
                                Test / Edit
                              </button>
                              <button
                                onClick={() => handleDeleteControl(c.id, c.controlCode)}
                                className="px-2 py-1 text-xs text-rose-600 hover:text-rose-900 bg-rose-50 hover:bg-rose-100 rounded transition"
                              >
                                &times;
                              </button>
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
        </div>
      )}

      {/* TAB 2: FINDINGS & ACTION TRACKER */}
      {activeTab === 'findings' && (
        <div className="space-y-4">
          {/* Filter & Action Bar */}
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <div className="flex flex-wrap items-center gap-3">
              {/* Severity Filter */}
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold text-slate-500 uppercase">Severity:</span>
                <select
                  value={severityFilter}
                  onChange={e => setSeverityFilter(e.target.value)}
                  className="text-sm border border-slate-300 rounded-lg px-2.5 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                >
                  <option value="All">All Severities</option>
                  <option value="Critical">Critical</option>
                  <option value="High">High</option>
                  <option value="Medium">Medium</option>
                  <option value="Low">Low</option>
                </select>
              </div>

              {/* Status Filter */}
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold text-slate-500 uppercase">Status:</span>
                <select
                  value={statusFilter}
                  onChange={e => setStatusFilter(e.target.value)}
                  className="text-sm border border-slate-300 rounded-lg px-2.5 py-1.5 bg-white text-slate-700 focus:outline-none focus:ring-1 focus:ring-indigo-500"
                >
                  <option value="All">All Statuses</option>
                  <option value="Open">Open</option>
                  <option value="InProgress">InProgress</option>
                  <option value="Implemented">Implemented</option>
                  <option value="Overdue">Overdue</option>
                </select>
              </div>
            </div>

            <button
              onClick={() => openFindingModal()}
              disabled={!selectedEngId}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-white bg-rose-600 hover:bg-rose-700 disabled:opacity-50 rounded-lg shadow-sm transition"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
              </svg>
              Log New Finding
            </button>
          </div>

          {/* Findings List */}
          {filteredFindings.length === 0 ? (
            <div className="bg-white p-8 rounded-xl border border-slate-200 text-center text-slate-500">
              No audit findings recorded. If controls have exceptions, log them here for management remediation tracking.
            </div>
          ) : (
            <div className="grid grid-cols-1 gap-4">
              {filteredFindings.map(f => (
                <div key={f.id} className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 hover:border-slate-300 transition">
                  <div className="flex flex-col md:flex-row md:items-start justify-between gap-3">
                    <div className="flex-1">
                      <div className="flex flex-wrap items-center gap-2 mb-1.5">
                        <span className="font-mono text-xs font-bold px-2 py-0.5 rounded bg-slate-100 text-slate-700 border">
                          {f.findingReference}
                        </span>
                        {f.controlCode && (
                          <span className="font-mono text-xs font-semibold px-2 py-0.5 rounded bg-indigo-50 text-indigo-700 border border-indigo-200">
                            {f.controlCode}
                          </span>
                        )}
                        <span className="text-xs font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-700">
                          {f.processCycle}
                        </span>
                        <span className={`text-xs font-bold px-2.5 py-0.5 rounded-full border ${getSeverityBadge(f.severity)}`}>
                          {f.severity} Severity
                        </span>
                        <span className="text-xs font-medium px-2 py-0.5 rounded bg-purple-50 text-purple-700 border border-purple-200">
                          {f.ifcClassification}
                        </span>
                      </div>

                      <h3 className="text-base font-bold text-slate-900 mt-1">{f.title}</h3>

                      <div className="mt-3 grid grid-cols-1 md:grid-cols-2 gap-4 text-xs">
                        <div className="bg-slate-50 p-3 rounded-lg border border-slate-200/80">
                          <p className="font-semibold text-slate-700 uppercase tracking-wider text-[10px]">Observation & Root Cause</p>
                          <p className="text-slate-700 mt-1">{f.detailedObservation}</p>
                          {f.rootCause && (
                            <p className="text-slate-500 mt-2">
                              <span className="font-semibold text-slate-600">Root Cause:</span> {f.rootCause}
                            </p>
                          )}
                          {f.estimatedRiskAmount && f.estimatedRiskAmount > 0 ? (
                            <p className="text-rose-700 font-bold mt-2">
                              Financial Exposure: ₹{f.estimatedRiskAmount.toLocaleString('en-IN')}
                            </p>
                          ) : null}
                        </div>

                        <div className="bg-indigo-50/40 p-3 rounded-lg border border-indigo-100">
                          <p className="font-semibold text-indigo-900 uppercase tracking-wider text-[10px]">Auditor Recommendation</p>
                          <p className="text-slate-700 mt-1">{f.auditorRecommendation}</p>
                          <div className="mt-2 pt-2 border-t border-indigo-100">
                            <p className="font-semibold text-indigo-900 uppercase tracking-wider text-[10px]">Management Action Plan</p>
                            <p className="text-slate-700 mt-0.5">{f.managementActionPlan || 'Action plan pending submission.'}</p>
                            <div className="mt-2 flex items-center gap-3 text-slate-500 text-[11px]">
                              <span>Owner: <b>{f.actionOwner || 'Not Assigned'}</b></span>
                              <span>•</span>
                              <span>Target Date: <b>{f.targetCompletionDate ? f.targetCompletionDate.substring(0, 10) : 'TBD'}</b></span>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>

                    {/* Status & Action Buttons */}
                    <div className="flex flex-col items-end gap-2 shrink-0">
                      <span className={`text-xs font-bold px-3 py-1 rounded-full uppercase tracking-wider ${getStatusBadge(f.implementationStatus)}`}>
                        {f.implementationStatus}
                      </span>
                      <div className="flex items-center gap-1.5 mt-2">
                        <button
                          onClick={() => openStatusModal(f)}
                          className="px-2.5 py-1 text-xs font-medium text-indigo-700 bg-indigo-50 hover:bg-indigo-100 border border-indigo-200 rounded-lg transition"
                        >
                          Update Status
                        </button>
                        <button
                          onClick={() => openFindingModal(f)}
                          className="px-2.5 py-1 text-xs font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
                        >
                          Edit
                        </button>
                        <button
                          onClick={() => handleDeleteFinding(f.id, f.findingReference)}
                          className="px-2 py-1 text-xs font-medium text-rose-600 hover:bg-rose-50 rounded-lg transition"
                        >
                          &times;
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* TAB 3: CYCLE-WISE HEATMAP & STATUTORY SCOPE */}
      {activeTab === 'heatmap' && (
        <div className="space-y-6">
          {/* Statutory Scope Checklist */}
          <div className="bg-gradient-to-r from-indigo-50 to-white p-5 rounded-xl border border-indigo-100 shadow-sm">
            <h3 className="text-base font-bold text-indigo-950 flex items-center gap-2">
              <svg className="w-5 h-5 text-indigo-600" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
              </svg>
              Companies Act, 2013 Statutory Applicability Benchmark
            </h3>
            <p className="text-xs text-slate-600 mt-1">
              Internal Audit is mandatory under Section 138 read with Rule 13 of Companies (Accounts) Rules, 2014:
            </p>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-3 mt-3 text-xs">
              <div className="p-3 bg-white rounded-lg border border-slate-200">
                <p className="font-bold text-slate-800">Listed Companies</p>
                <p className="text-slate-500 mt-0.5">Mandatory for all listed companies regardless of turnover or capital thresholds.</p>
              </div>
              <div className="p-3 bg-white rounded-lg border border-slate-200">
                <p className="font-bold text-slate-800">Unlisted Public Companies</p>
                <p className="text-slate-500 mt-0.5">Paid-up capital $\ge$ ₹50 Cr, OR Turnover $\ge$ ₹200 Cr, OR Loans $\ge$ ₹100 Cr, OR Deposits $\ge$ ₹25 Cr.</p>
              </div>
              <div className="p-3 bg-white rounded-lg border border-slate-200">
                <p className="font-bold text-slate-800">Private Companies</p>
                <p className="text-slate-500 mt-0.5">Turnover $\ge$ ₹200 Cr, OR Outstanding Loans/Borrowings from Banks/PFIs $\ge$ ₹100 Cr.</p>
              </div>
            </div>
            <div className="mt-3 p-2.5 bg-amber-50 border border-amber-200 rounded-lg text-xs text-amber-900">
              <b>Section 143(3)(i) IFC-FR Reporting:</b> Statutory auditor must independently comment on whether the company has adequate internal financial controls with reference to financial statements in place and their operating effectiveness.
            </div>
          </div>

          {/* Cycle Heatmap Grid */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {cycleMetrics.map(cm => (
              <div key={cm.code} className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between">
                    <span className="text-xs font-mono font-bold px-2 py-0.5 rounded bg-indigo-50 text-indigo-700 border border-indigo-200">
                      {cm.code}
                    </span>
                    <span className={`text-xs font-bold px-2 py-0.5 rounded-full border ${getHealthBadge(cm.cycleScore)}`}>
                      {cm.cycleScore}% Health
                    </span>
                  </div>
                  <h4 className="text-sm font-bold text-slate-900 mt-2">{cm.label}</h4>

                  {/* Progress bar */}
                  <div className="w-full bg-slate-100 rounded-full h-2.5 mt-3 overflow-hidden flex">
                    <div className="bg-emerald-500 h-2.5" style={{ width: `${(cm.effective / Math.max(cm.total, 1)) * 100}%` }}></div>
                    <div className="bg-rose-500 h-2.5" style={{ width: `${(cm.ineffective / Math.max(cm.total, 1)) * 100}%` }}></div>
                  </div>

                  <div className="grid grid-cols-3 gap-2 mt-3 text-center text-xs">
                    <div className="p-2 bg-slate-50 rounded border">
                      <p className="text-slate-400 text-[10px] uppercase font-bold">Total</p>
                      <p className="font-bold text-slate-800">{cm.total}</p>
                    </div>
                    <div className="p-2 bg-emerald-50 rounded border border-emerald-100">
                      <p className="text-emerald-600 text-[10px] uppercase font-bold">Effective</p>
                      <p className="font-bold text-emerald-800">{cm.effective}</p>
                    </div>
                    <div className="p-2 bg-rose-50 rounded border border-rose-100">
                      <p className="text-rose-600 text-[10px] uppercase font-bold">Deficient</p>
                      <p className="font-bold text-rose-800">{cm.ineffective}</p>
                    </div>
                  </div>
                </div>

                <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-xs">
                  <span className="text-slate-500">
                    Deficiencies Logged: <b className={cm.cycleFindings > 0 ? 'text-rose-600' : 'text-slate-700'}>{cm.cycleFindings}</b>
                  </span>
                  <button
                    onClick={() => {
                      setCycleFilter(cm.code)
                      setActiveTab('matrix')
                    }}
                    className="text-indigo-600 hover:text-indigo-900 font-semibold text-xs"
                  >
                    View Controls &rarr;
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* TAB 4: EXECUTIVE REPORT */}
      {activeTab === 'report' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <div>
              <h3 className="text-base font-bold text-slate-900">
                Executive Internal Audit & IFC-FR Testing Report
              </h3>
              <p className="text-xs text-slate-500">
                Formatted as per ICAI Standards on Internal Audit (SIA) & Section 138 / 143(3)(i)
              </p>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={handleGenerateReport}
                disabled={!selectedEngId}
                className="px-3 py-1.5 text-xs font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-lg shadow-sm transition"
              >
                Refresh / Generate Report
              </button>
              {currentEngagement?.generatedReportText && (
                <button
                  onClick={() => {
                    navigator.clipboard.writeText(currentEngagement.generatedReportText || '')
                    notifySuccess('Report text copied to clipboard!')
                  }}
                  className="px-3 py-1.5 text-xs font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
                >
                  Copy Text
                </button>
              )}
            </div>
          </div>

          {currentEngagement?.generatedReportText ? (
            <div className="bg-slate-900 text-slate-100 p-6 rounded-xl font-mono text-xs whitespace-pre-wrap leading-relaxed shadow-inner overflow-x-auto">
              {currentEngagement.generatedReportText}
            </div>
          ) : (
            <div className="bg-white p-12 rounded-xl border border-slate-200 text-center space-y-3">
              <p className="text-sm text-slate-500">
                No executive report has been generated yet for this engagement.
              </p>
              <button
                onClick={handleGenerateReport}
                className="px-4 py-2 text-sm font-medium text-white bg-indigo-600 hover:bg-indigo-700 rounded-lg shadow-sm transition"
              >
                Generate Executive Report Now
              </button>
            </div>
          )}
        </div>
      )}

      {/* MODAL 1: NEW ENGAGEMENT */}
      {showEngagementModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-base font-bold text-slate-900">New Internal Audit Engagement</h3>
              <button onClick={() => setShowEngagementModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">&times;</button>
            </div>

            <form onSubmit={handleSaveEngagement} className="space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Engagement Title</label>
                <input
                  type="text"
                  required
                  value={engForm.title}
                  onChange={e => setEngForm({ ...engForm, title: e.target.value })}
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Financial Year</label>
                  <input
                    type="text"
                    required
                    value={engForm.financialYear}
                    onChange={e => setEngForm({ ...engForm, financialYear: e.target.value })}
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Audit Period</label>
                  <input
                    type="text"
                    required
                    placeholder="e.g. Q3 FY 2024-25"
                    value={engForm.auditPeriod}
                    onChange={e => setEngForm({ ...engForm, auditPeriod: e.target.value })}
                    className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                  />
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Engagement Lead / Partner</label>
                <input
                  type="text"
                  value={engForm.engagementLeadName || ''}
                  onChange={e => setEngForm({ ...engForm, engagementLeadName: e.target.value })}
                  placeholder="CA Anita Desai, FCA, DISA"
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Audit Scope & Objective</label>
                <textarea
                  rows={3}
                  value={engForm.auditScopeDescription || ''}
                  onChange={e => setEngForm({ ...engForm, auditScopeDescription: e.target.value })}
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 focus:ring-1 focus:ring-indigo-500 focus:outline-none"
                />
              </div>

              <div className="p-3 bg-indigo-50 rounded-lg border border-indigo-100 flex items-start gap-2.5">
                <input
                  type="checkbox"
                  id="popTemplate"
                  checked={engForm.populateTemplate}
                  onChange={e => setEngForm({ ...engForm, populateTemplate: e.target.checked })}
                  className="mt-0.5 rounded text-indigo-600 focus:ring-indigo-500"
                />
                <label htmlFor="popTemplate" className="text-slate-700">
                  <span className="font-bold text-indigo-950 block">Pre-populate Standard 6-Cycle RCM Template</span>
                  <span className="text-[11px] text-slate-500">Automatically adds verified baseline controls for P2P, O2C, H2R, R2R, Treasury and ITGC.</span>
                </label>
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowEngagementModal(false)}
                  className="px-3 py-2 border rounded-lg text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 font-medium"
                >
                  Create Engagement
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: ADD/EDIT RCM CONTROL & TEST */}
      {showControlModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-2xl w-full p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-base font-bold text-slate-900">
                {editingControl ? `Edit & Test Control: ${editingControl.controlCode}` : 'Add Control to RCM'}
              </h3>
              <button onClick={() => setShowControlModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">&times;</button>
            </div>

            <form onSubmit={handleSaveControl} className="space-y-4 text-xs">
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Control Code</label>
                  <input
                    type="text"
                    required
                    value={controlForm.controlCode}
                    onChange={e => setControlForm({ ...controlForm, controlCode: e.target.value })}
                    className="w-full border border-slate-300 rounded-lg px-2.5 py-1.5 font-mono uppercase"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Process Cycle</label>
                  <select
                    value={controlForm.processCycle}
                    onChange={e => setControlForm({ ...controlForm, processCycle: e.target.value })}
                    className="w-full border border-slate-300 rounded-lg px-2.5 py-1.5"
                  >
                    {PROCESS_CYCLES.map(p => (
                      <option key={p.code} value={p.code}>{p.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Inherent Risk</label>
                  <select
                    value={controlForm.inherentRiskRating}
                    onChange={e => setControlForm({ ...controlForm, inherentRiskRating: e.target.value })}
                    className="w-full border border-slate-300 rounded-lg px-2.5 py-1.5"
                  >
                    <option value="High">High</option>
                    <option value="Medium">Medium</option>
                    <option value="Low">Low</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Sub-Process Name</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. Purchase Order 3-Way Match"
                  value={controlForm.subProcess}
                  onChange={e => setControlForm({ ...controlForm, subProcess: e.target.value })}
                  className="w-full border border-slate-300 rounded-lg px-2.5 py-1.5"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Risk Description (What Could Go Wrong)</label>
                <textarea
                  rows={2}
                  required
                  value={controlForm.riskDescription}
                  onChange={e => setControlForm({ ...controlForm, riskDescription: e.target.value })}
                  className="w-full border border-slate-300 rounded-lg px-2.5 py-1.5"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Control Activity (How Risk is Mitigated)</label>
                <textarea
                  rows={2}
                  required
                  value={controlForm.controlActivity}
                  onChange={e => setControlForm({ ...controlForm, controlActivity: e.target.value })}
                  className="w-full border border-slate-300 rounded-lg px-2.5 py-1.5"
                />
              </div>

              <div className="grid grid-cols-4 gap-2">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Nature</label>
                  <select
                    value={controlForm.controlNature || 'Preventive'}
                    onChange={e => setControlForm({ ...controlForm, controlNature: e.target.value })}
                    className="w-full border border-slate-300 rounded-lg px-2 py-1.5"
                  >
                    <option value="Preventive">Preventive</option>
                    <option value="Detective">Detective</option>
                    <option value="Corrective">Corrective</option>
                  </select>
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Frequency</label>
                  <select
                    value={controlForm.controlFrequency || 'Monthly'}
                    onChange={e => setControlForm({ ...controlForm, controlFrequency: e.target.value })}
                    className="w-full border border-slate-300 rounded-lg px-2 py-1.5"
                  >
                    <option value="Transactional">Transactional</option>
                    <option value="Daily">Daily</option>
                    <option value="Weekly">Weekly</option>
                    <option value="Monthly">Monthly</option>
                    <option value="Quarterly">Quarterly</option>
                    <option value="Annual">Annual</option>
                  </select>
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Type</label>
                  <select
                    value={controlForm.controlType || 'Automated'}
                    onChange={e => setControlForm({ ...controlForm, controlType: e.target.value })}
                    className="w-full border border-slate-300 rounded-lg px-2 py-1.5"
                  >
                    <option value="Automated">Automated</option>
                    <option value="SemiAutomated">Semi-Automated</option>
                    <option value="Manual">Manual</option>
                  </select>
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Control Owner</label>
                  <input
                    type="text"
                    value={controlForm.controlOwner || ''}
                    onChange={e => setControlForm({ ...controlForm, controlOwner: e.target.value })}
                    className="w-full border border-slate-300 rounded-lg px-2 py-1.5"
                  />
                </div>
              </div>

              {/* WALKTHROUGH & DESIGN SECTION */}
              <div className="p-3 bg-slate-50 rounded-lg border border-slate-200 space-y-2">
                <p className="font-bold text-slate-900 uppercase tracking-wider text-[11px]">1. Walkthrough & Design Effectiveness Review</p>
                <div className="grid grid-cols-3 gap-2">
                  <div>
                    <label className="block text-slate-600 mb-1">Design Effectiveness</label>
                    <select
                      value={controlForm.designEffectiveness}
                      onChange={e => setControlForm({ ...controlForm, designEffectiveness: e.target.value })}
                      className="w-full border border-slate-300 rounded px-2 py-1"
                    >
                      <option value="Effective">Effective Design</option>
                      <option value="Deficient">Deficient Design</option>
                    </select>
                  </div>
                  <div>
                    <label className="block text-slate-600 mb-1">Walkthrough Ref</label>
                    <input
                      type="text"
                      value={controlForm.walkthroughReference || ''}
                      onChange={e => setControlForm({ ...controlForm, walkthroughReference: e.target.value })}
                      className="w-full border border-slate-300 rounded px-2 py-1"
                    />
                  </div>
                  <div>
                    <label className="block text-slate-600 mb-1">Walkthrough Date</label>
                    <input
                      type="date"
                      value={controlForm.walkthroughDate || ''}
                      onChange={e => setControlForm({ ...controlForm, walkthroughDate: e.target.value })}
                      className="w-full border border-slate-300 rounded px-2 py-1"
                    />
                  </div>
                </div>
              </div>

              {/* TEST OF OPERATING EFFECTIVENESS (ToC) */}
              <div className="p-3 bg-indigo-50/50 rounded-lg border border-indigo-100 space-y-2">
                <p className="font-bold text-indigo-950 uppercase tracking-wider text-[11px]">2. Test of Operating Effectiveness (ToC)</p>
                <div className="grid grid-cols-4 gap-2">
                  <div>
                    <label className="block text-slate-600 mb-1">Testing Method</label>
                    <select
                      value={controlForm.testingMethod}
                      onChange={e => setControlForm({ ...controlForm, testingMethod: e.target.value })}
                      className="w-full border border-slate-300 rounded px-2 py-1"
                    >
                      {TESTING_METHODS.map(m => (
                        <option key={m} value={m}>{m}</option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="block text-slate-600 mb-1">Sample Size</label>
                    <input
                      type="number"
                      min={0}
                      value={controlForm.sampleSize}
                      onChange={e => setControlForm({ ...controlForm, sampleSize: parseInt(e.target.value) || 0 })}
                      className="w-full border border-slate-300 rounded px-2 py-1"
                    />
                  </div>
                  <div>
                    <label className="block text-slate-600 mb-1">Exceptions Found</label>
                    <input
                      type="number"
                      min={0}
                      value={controlForm.exceptionsCount}
                      onChange={e => {
                        const count = parseInt(e.target.value) || 0
                        setControlForm({
                          ...controlForm,
                          exceptionsCount: count,
                          operatingEffectiveness: count > 0 ? 'Ineffective' : 'OperatingEffectively'
                        })
                      }}
                      className="w-full border border-slate-300 rounded px-2 py-1 font-bold text-rose-700"
                    />
                  </div>
                  <div>
                    <label className="block text-slate-600 mb-1">Conclusion</label>
                    <select
                      value={controlForm.operatingEffectiveness}
                      onChange={e => setControlForm({ ...controlForm, operatingEffectiveness: e.target.value })}
                      className="w-full border border-slate-300 rounded px-2 py-1 font-semibold"
                    >
                      <option value="OperatingEffectively">Operating Effectively</option>
                      <option value="Ineffective">Ineffective</option>
                      <option value="NotTested">Not Tested</option>
                    </select>
                  </div>
                </div>

                <div>
                  <label className="block text-slate-600 mb-1">Testing Notes / Sample References</label>
                  <textarea
                    rows={2}
                    value={controlForm.testingNotes || ''}
                    onChange={e => setControlForm({ ...controlForm, testingNotes: e.target.value })}
                    placeholder="Document sample invoice numbers, testing findings or reasons for exceptions..."
                    className="w-full border border-slate-300 rounded px-2 py-1"
                  />
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowControlModal(false)}
                  className="px-3 py-2 border rounded-lg text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 font-medium"
                >
                  Save Control & Evaluation
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: LOG / EDIT AUDIT FINDING */}
      {showFindingModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-2xl w-full p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-base font-bold text-slate-900">
                {editingFinding ? `Edit Finding: ${editingFinding.findingReference}` : 'Log Internal Audit Finding'}
              </h3>
              <button onClick={() => setShowFindingModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">&times;</button>
            </div>

            <form onSubmit={handleSaveFinding} className="space-y-4 text-xs">
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Finding Ref</label>
                  <input
                    type="text"
                    value={findingForm.findingReference || ''}
                    onChange={e => setFindingForm({ ...findingForm, findingReference: e.target.value })}
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5 font-mono"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Process Cycle</label>
                  <select
                    value={findingForm.processCycle}
                    onChange={e => setFindingForm({ ...findingForm, processCycle: e.target.value })}
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                  >
                    {PROCESS_CYCLES.map(p => (
                      <option key={p.code} value={p.code}>{p.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Severity</label>
                  <select
                    value={findingForm.severity}
                    onChange={e => setFindingForm({ ...findingForm, severity: e.target.value })}
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5 font-semibold text-rose-700"
                  >
                    <option value="Critical">Critical</option>
                    <option value="High">High</option>
                    <option value="Medium">Medium</option>
                    <option value="Low">Low</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Finding Title</label>
                <input
                  type="text"
                  required
                  value={findingForm.title}
                  onChange={e => setFindingForm({ ...findingForm, title: e.target.value })}
                  placeholder="e.g. Purchase Invoices Processed Without Linked Purchase Order"
                  className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">IFC-FR Classification</label>
                  <select
                    value={findingForm.ifcClassification}
                    onChange={e => setFindingForm({ ...findingForm, ifcClassification: e.target.value })}
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                  >
                    {IFC_CLASSIFICATIONS.map(c => (
                      <option key={c.code} value={c.code}>{c.label}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Estimated Financial Exposure (₹)</label>
                  <input
                    type="number"
                    min={0}
                    value={findingForm.estimatedRiskAmount || 0}
                    onChange={e => setFindingForm({ ...findingForm, estimatedRiskAmount: parseFloat(e.target.value) || 0 })}
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                  />
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Detailed Observation (Condition & Criteria)</label>
                <textarea
                  rows={3}
                  required
                  value={findingForm.detailedObservation}
                  onChange={e => setFindingForm({ ...findingForm, detailedObservation: e.target.value })}
                  className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Root Cause Analysis</label>
                <input
                  type="text"
                  value={findingForm.rootCause || ''}
                  onChange={e => setFindingForm({ ...findingForm, rootCause: e.target.value })}
                  placeholder="e.g. System bypass configured to handle emergency spares"
                  className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Auditor Recommendation</label>
                <textarea
                  rows={2}
                  required
                  value={findingForm.auditorRecommendation}
                  onChange={e => setFindingForm({ ...findingForm, auditorRecommendation: e.target.value })}
                  className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Agreed Management Action Plan</label>
                <textarea
                  rows={2}
                  value={findingForm.managementActionPlan || ''}
                  onChange={e => setFindingForm({ ...findingForm, managementActionPlan: e.target.value })}
                  placeholder="Steps to be taken by management to remediate the deficiency..."
                  className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Action Owner</label>
                  <input
                    type="text"
                    value={findingForm.actionOwner || ''}
                    onChange={e => setFindingForm({ ...findingForm, actionOwner: e.target.value })}
                    placeholder="e.g. Chief Financial Officer / AP Lead"
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Target Completion Date</label>
                  <input
                    type="date"
                    value={findingForm.targetCompletionDate || ''}
                    onChange={e => setFindingForm({ ...findingForm, targetCompletionDate: e.target.value })}
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                  />
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowFindingModal(false)}
                  className="px-3 py-2 border rounded-lg text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-rose-600 text-white rounded-lg hover:bg-rose-700 font-medium"
                >
                  Save Finding
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: UPDATE FINDING REMEDIATION STATUS */}
      {showStatusModal && targetFindingForStatus && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-base font-bold text-slate-900">
                Update Status: {targetFindingForStatus.findingReference}
              </h3>
              <button onClick={() => setShowStatusModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">&times;</button>
            </div>

            <form onSubmit={handleUpdateFindingStatus} className="space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Implementation Status</label>
                <select
                  value={statusForm.implementationStatus}
                  onChange={e => setStatusForm({ ...statusForm, implementationStatus: e.target.value })}
                  className="w-full border border-slate-300 rounded-lg px-3 py-2 font-bold"
                >
                  <option value="Open">Open</option>
                  <option value="InProgress">In Progress</option>
                  <option value="Implemented">Implemented</option>
                  <option value="Overdue">Overdue</option>
                </select>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Updated Management Action Plan / Remediation Notes</label>
                <textarea
                  rows={3}
                  value={statusForm.managementActionPlan || ''}
                  onChange={e => setStatusForm({ ...statusForm, managementActionPlan: e.target.value })}
                  placeholder="Describe progress made, controls deployed, or testing results..."
                  className="w-full border border-slate-300 rounded-lg px-3 py-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Target / Extended Completion Date</label>
                <input
                  type="date"
                  value={statusForm.targetCompletionDate || ''}
                  onChange={e => setStatusForm({ ...statusForm, targetCompletionDate: e.target.value })}
                  className="w-full border border-slate-300 rounded-lg px-3 py-2"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowStatusModal(false)}
                  className="px-3 py-2 border rounded-lg text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 text-white rounded-lg hover:bg-indigo-700 font-medium"
                >
                  Update Status
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
