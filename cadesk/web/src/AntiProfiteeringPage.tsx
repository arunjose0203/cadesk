import { useState, useEffect, useMemo } from 'react'
import {
  antiProfiteeringApi,
  api,
  type GstRateNotificationDto,
  type AntiProfiteeringAssessmentDto,
  type SkuProfiteeringItemDto,
  type ProfiteeringRefundRecordDto,
  type AntiProfiteeringSummaryDto,
  type SaveRateNotificationRequest,
  type SaveAssessmentRequest,
  type SaveSkuItemRequest,
  type SaveRefundRecordRequest,
  type CertifyAssessmentRequest,
  type Client
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

export function AntiProfiteeringPage() {
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [selectedFy, setSelectedFy] = useState<string>('2024-25')

  // Notifications & Assessments
  const [notifications, setNotifications] = useState<GstRateNotificationDto[]>([])
  const [assessments, setAssessments] = useState<AntiProfiteeringAssessmentDto[]>([])
  const [selectedAssessmentId, setSelectedAssessmentId] = useState<string>('')

  // Data states
  const [summary, setSummary] = useState<AntiProfiteeringSummaryDto | null>(null)
  const [currentAssessment, setCurrentAssessment] = useState<AntiProfiteeringAssessmentDto | null>(null)
  const [skus, setSkus] = useState<SkuProfiteeringItemDto[]>([])
  const [refunds, setRefunds] = useState<ProfiteeringRefundRecordDto[]>([])

  const [loading, setLoading] = useState(true)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)
  const [successMsg, setSuccessMsg] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState<'skus' | 'refunds' | 'notifications' | 'certificate'>('skus')

  // Filters
  const [statusFilter, setStatusFilter] = useState('All')
  const [recipientFilter, setRecipientFilter] = useState('All')
  const [searchQuery, setSearchQuery] = useState('')

  // Modals
  const [showAssessmentModal, setShowAssessmentModal] = useState(false)
  const [showSkuModal, setShowSkuModal] = useState(false)
  const [showRefundModal, setShowRefundModal] = useState(false)
  const [showNotificationModal, setShowNotificationModal] = useState(false)
  const [showCertifyModal, setShowCertifyModal] = useState(false)

  // Edit targets
  const [editingSku, setEditingSku] = useState<SkuProfiteeringItemDto | null>(null)
  const [editingRefund, setEditingRefund] = useState<ProfiteeringRefundRecordDto | null>(null)

  // Form states
  const [assessmentForm, setAssessmentForm] = useState<SaveAssessmentRequest>({
    clientId: '',
    assessmentReference: 'APA-2024-001',
    title: 'Section 171 Anti-Profiteering Audit Review',
    financialYear: '2024-25',
    auditPeriodFrom: '2024-07-01',
    auditPeriodTo: '2024-09-30',
    gstNotificationId: undefined,
    notificationReference: '',
    rateCutEffectiveDate: '2024-07-01',
    leadAuditorName: 'CA Sneha Mehra, Partner'
  })

  const [skuForm, setSkuForm] = useState<SaveSkuItemRequest>({
    productCode: '',
    hsnOrSacCode: '',
    productName: '',
    unitOfMeasurement: 'PCS',
    preCutBasePrice: 100,
    oldGstRate: 18,
    newGstRate: 12,
    postCutActualBasePrice: 100,
    quantitySoldPostCut: 1000,
    hasRawMaterialCostJustification: false,
    justificationRemarks: ''
  })

  const [refundForm, setRefundForm] = useState<SaveRefundRecordRequest>({
    recipientType: 'IdentifiedCustomer',
    customerNameOrEntity: '',
    customerGstin: '',
    invoiceReference: '',
    refundAmount: 0,
    refundMode: 'CreditNote',
    refundOrDepositDate: new Date().toISOString().substring(0, 10),
    challanOrCinNumber: '',
    interestAmountPaid: 0,
    notes: ''
  })

  const [notifForm, setNotifForm] = useState<SaveRateNotificationRequest>({
    notificationNumber: '',
    notificationDate: new Date().toISOString().substring(0, 10),
    effectiveDate: new Date().toISOString().substring(0, 10),
    hsnOrSacCode: '',
    description: '',
    previousRatePercentage: 18,
    revisedRatePercentage: 12,
    isActive: true,
    notes: ''
  })

  const [certifyForm, setCertifyForm] = useState<CertifyAssessmentRequest>({
    leadAuditorName: 'CA Sneha Mehra, FCA, Partner',
    udin: `24078512AAAAAP${Math.floor(1000 + Math.random() * 9000)}`
  })

  // Load clients & notifications
  useEffect(() => {
    const initData = async () => {
      try {
        const [cRes, notifRes] = await Promise.all([
          api<ClientPage>('/api/clients?pageSize=100').catch(() => ({ items: [], total: 0 })),
          antiProfiteeringApi.listNotifications().catch(() => [])
        ])
        setClients(cRes.items || [])
        setNotifications(notifRes)
        if (cRes.items && cRes.items.length > 0) {
          setSelectedClientId(prev => prev || cRes.items[0].id)
        }
      } catch (err) {
        console.error(err)
      }
    }
    initData()
  }, [])

  // Load assessments when client or FY changes
  useEffect(() => {
    if (!selectedClientId) return
    const fetchAssessments = async () => {
      try {
        setLoading(true)
        const aList = await antiProfiteeringApi.listAssessments(selectedClientId, selectedFy)
        setAssessments(aList)
        if (aList.length > 0) {
          setSelectedAssessmentId(prev => (prev && aList.some(a => a.id === prev) ? prev : aList[0].id))
        } else {
          setSelectedAssessmentId('')
          setCurrentAssessment(null)
          setSkus([])
          setRefunds([])
        }
      } catch (err: unknown) {
        setErrorMsg(err instanceof Error ? err.message : 'Failed to load assessments')
      } finally {
        setLoading(false)
      }
    }
    fetchAssessments()
  }, [selectedClientId, selectedFy])

  // Load details for selected assessment
  useEffect(() => {
    if (!selectedAssessmentId) return
    const loadDetails = async () => {
      try {
        setLoading(true)
        setErrorMsg(null)
        const [a, sum, sList, rList] = await Promise.all([
          antiProfiteeringApi.getAssessment(selectedAssessmentId),
          antiProfiteeringApi.getSummary(selectedClientId, selectedFy),
          antiProfiteeringApi.listSkus(selectedAssessmentId),
          antiProfiteeringApi.listRefunds(selectedAssessmentId)
        ])
        setCurrentAssessment(a)
        setSummary(sum)
        setSkus(sList)
        setRefunds(rList)
      } catch (err: unknown) {
        setErrorMsg(err instanceof Error ? err.message : 'Failed to load assessment details')
      } finally {
        setLoading(false)
      }
    }
    loadDetails()
  }, [selectedAssessmentId, selectedClientId, selectedFy])

  const notifySuccess = (msg: string) => {
    setSuccessMsg(msg)
    setTimeout(() => setSuccessMsg(null), 4000)
  }

  const refreshAssessment = async () => {
    if (!selectedAssessmentId) return
    try {
      const [a, sum, sList, rList] = await Promise.all([
        antiProfiteeringApi.getAssessment(selectedAssessmentId),
        antiProfiteeringApi.getSummary(selectedClientId, selectedFy),
        antiProfiteeringApi.listSkus(selectedAssessmentId),
        antiProfiteeringApi.listRefunds(selectedAssessmentId)
      ])
      setCurrentAssessment(a)
      setSummary(sum)
      setSkus(sList)
      setRefunds(rList)
    } catch (e) {
      console.error(e)
    }
  }

  // Handle Create Assessment
  const handleSaveAssessment = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setErrorMsg(null)
      const res = await antiProfiteeringApi.createAssessment({
        ...assessmentForm,
        clientId: selectedClientId,
        financialYear: selectedFy
      })
      setShowAssessmentModal(false)
      notifySuccess(`Assessment ${res.assessmentReference} created successfully!`)
      const aList = await antiProfiteeringApi.listAssessments(selectedClientId, selectedFy)
      setAssessments(aList)
      setSelectedAssessmentId(res.id)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error creating assessment')
    }
  }

  // Open SKU Modal
  const openSkuModal = (sku?: SkuProfiteeringItemDto) => {
    if (sku) {
      setEditingSku(sku)
      setSkuForm({
        productCode: sku.productCode,
        hsnOrSacCode: sku.hsnOrSacCode,
        productName: sku.productName,
        unitOfMeasurement: sku.unitOfMeasurement,
        preCutBasePrice: sku.preCutBasePrice,
        oldGstRate: sku.oldGstRate,
        newGstRate: sku.newGstRate,
        postCutActualBasePrice: sku.postCutActualBasePrice,
        quantitySoldPostCut: sku.quantitySoldPostCut,
        hasRawMaterialCostJustification: sku.hasRawMaterialCostJustification,
        justificationRemarks: sku.justificationRemarks
      })
    } else {
      setEditingSku(null)
      setSkuForm({
        productCode: `SKU-${(skus.length + 1).toString().padStart(3, '0')}`,
        hsnOrSacCode: currentAssessment?.gstNotificationId ? (notifications.find(n => n.id === currentAssessment.gstNotificationId)?.hsnOrSacCode || '8418') : '8418',
        productName: '',
        unitOfMeasurement: 'PCS',
        preCutBasePrice: 1000,
        oldGstRate: 18,
        newGstRate: 12,
        postCutActualBasePrice: 1000,
        quantitySoldPostCut: 100,
        hasRawMaterialCostJustification: false,
        justificationRemarks: ''
      })
    }
    setShowSkuModal(true)
  }

  // Handle Save SKU
  const handleSaveSku = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedAssessmentId) return
    try {
      setErrorMsg(null)
      if (editingSku) {
        await antiProfiteeringApi.updateSku(selectedAssessmentId, editingSku.id, skuForm)
        notifySuccess(`SKU ${skuForm.productCode} updated & re-evaluated`)
      } else {
        await antiProfiteeringApi.createSku(selectedAssessmentId, skuForm)
        notifySuccess(`SKU ${skuForm.productCode} evaluated for anti-profiteering`)
      }
      setShowSkuModal(false)
      await refreshAssessment()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error saving SKU')
    }
  }

  // Delete SKU
  const handleDeleteSku = async (id: string, code: string) => {
    if (!selectedAssessmentId) return
    if (!confirm(`Delete SKU ${code} from audit?`)) return
    try {
      await antiProfiteeringApi.deleteSku(selectedAssessmentId, id)
      notifySuccess(`SKU ${code} removed`)
      await refreshAssessment()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error deleting SKU')
    }
  }

  // Open Refund Modal
  const openRefundModal = (r?: ProfiteeringRefundRecordDto) => {
    if (r) {
      setEditingRefund(r)
      setRefundForm({
        recipientType: r.recipientType,
        customerNameOrEntity: r.customerNameOrEntity,
        customerGstin: r.customerGstin,
        invoiceReference: r.invoiceReference,
        refundAmount: r.refundAmount,
        refundMode: r.refundMode,
        refundOrDepositDate: r.refundOrDepositDate.substring(0, 10),
        challanOrCinNumber: r.challanOrCinNumber,
        interestAmountPaid: r.interestAmountPaid,
        notes: r.notes
      })
    } else {
      setEditingRefund(null)
      setRefundForm({
        recipientType: 'IdentifiedCustomer',
        customerNameOrEntity: '',
        customerGstin: '',
        invoiceReference: '',
        refundAmount: 0,
        refundMode: 'CreditNote',
        refundOrDepositDate: new Date().toISOString().substring(0, 10),
        challanOrCinNumber: '',
        interestAmountPaid: 0,
        notes: ''
      })
    }
    setShowRefundModal(true)
  }

  // Handle Save Refund
  const handleSaveRefund = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedAssessmentId) return
    try {
      setErrorMsg(null)
      if (editingRefund) {
        await antiProfiteeringApi.updateRefund(selectedAssessmentId, editingRefund.id, refundForm)
        notifySuccess('Refund/CWF record updated')
      } else {
        await antiProfiteeringApi.createRefund(selectedAssessmentId, refundForm)
        notifySuccess('Customer refund / CWF deposit recorded')
      }
      setShowRefundModal(false)
      await refreshAssessment()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error saving refund record')
    }
  }

  // Delete Refund
  const handleDeleteRefund = async (id: string) => {
    if (!selectedAssessmentId) return
    if (!confirm('Delete this refund record?')) return
    try {
      await antiProfiteeringApi.deleteRefund(selectedAssessmentId, id)
      notifySuccess('Record deleted')
      await refreshAssessment()
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error deleting record')
    }
  }

  // Save Rate Notification
  const handleSaveNotification = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      setErrorMsg(null)
      await antiProfiteeringApi.createNotification(notifForm)
      notifySuccess(`Rate notification ${notifForm.notificationNumber} added to registry`)
      setShowNotificationModal(false)
      const notifs = await antiProfiteeringApi.listNotifications()
      setNotifications(notifs)
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error saving notification')
    }
  }

  // Certify Assessment with UDIN
  const handleCertify = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedAssessmentId) return
    try {
      setErrorMsg(null)
      const res = await antiProfiteeringApi.certifyAssessment(selectedAssessmentId, certifyForm)
      setCurrentAssessment(res)
      setShowCertifyModal(false)
      notifySuccess(`Assessment certified with UDIN ${res.udin}!`)
      setActiveTab('certificate')
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : 'Error certifying assessment')
    }
  }

  // Filtered SKUs
  const filteredSkus = useMemo(() => {
    return skus.filter(s => {
      const matchesStatus = statusFilter === 'All' || s.complianceStatus === statusFilter
      const q = searchQuery.toLowerCase().trim()
      const matchesSearch =
        !q ||
        s.productCode.toLowerCase().includes(q) ||
        s.productName.toLowerCase().includes(q) ||
        s.hsnOrSacCode.toLowerCase().includes(q)
      return matchesStatus && matchesSearch
    })
  }, [skus, statusFilter, searchQuery])

  // Filtered Refunds
  const filteredRefunds = useMemo(() => {
    return refunds.filter(r => {
      const matchesRec = recipientFilter === 'All' || r.recipientType === recipientFilter
      return matchesRec
    })
  }, [refunds, recipientFilter])

  // Status Badge Helper
  const getStatusBadge = (st: string) => {
    switch (st) {
      case 'Compliant':
        return 'bg-emerald-100 text-emerald-800 border-emerald-300'
      case 'Deficient':
        return 'bg-amber-100 text-amber-800 border-amber-300'
      case 'Profiteered':
        return 'bg-rose-100 text-rose-800 border-rose-300 font-bold'
      default:
        return 'bg-slate-100 text-slate-700 border-slate-300'
    }
  }

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b border-slate-200 pb-5">
        <div className="flex items-center gap-3">
          <span className="p-2.5 bg-amber-50 border border-amber-200 rounded-xl text-amber-700">
            {/* Scales / Anti-profiteering Icon */}
            <svg className="w-6 h-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M3 6l3 1m0 0l-3 9a5.002 5.002 0 006.001 0M6 7l3 9M6 7l6-2m6 2l3-1m-3 1l-3 9a5.002 5.002 0 006.001 0M18 7l3 9m-3-9l-6-2m0-2v2m0 16V5m0 16H9m3 0h3" />
            </svg>
          </span>
          <div>
            <h1 className="text-2xl font-bold text-slate-900">
              GST Anti-Profiteering & Rate Notification Engine
            </h1>
            <p className="text-sm text-slate-500">
              CGST Act, 2017 Section 171 • Rules 122–137 • Commensurate Price Reduction • CCI / DGAP Compliance Desk
            </p>
          </div>
        </div>

        {/* Action Controls */}
        <div className="flex flex-wrap items-center gap-2">
          {/* Client picker */}
          <select
            value={selectedClientId}
            onChange={e => setSelectedClientId(e.target.value)}
            className="text-sm font-medium border border-slate-300 rounded-lg px-3 py-2 bg-white text-slate-700 shadow-sm focus:ring-2 focus:ring-amber-500 focus:outline-none"
          >
            {clients.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>

          {/* FY picker */}
          <select
            value={selectedFy}
            onChange={e => setSelectedFy(e.target.value)}
            className="text-sm font-medium border border-slate-300 rounded-lg px-3 py-2 bg-white text-slate-700 shadow-sm focus:ring-2 focus:ring-amber-500 focus:outline-none"
          >
            <option value="2025-26">FY 2025-26</option>
            <option value="2024-25">FY 2024-25</option>
            <option value="2023-24">FY 2023-24</option>
          </select>

          {/* Assessment Picker */}
          {assessments.length > 0 && (
            <select
              value={selectedAssessmentId}
              onChange={e => setSelectedAssessmentId(e.target.value)}
              className="text-sm font-medium border border-amber-300 bg-amber-50/50 rounded-lg px-3 py-2 text-amber-950 shadow-sm focus:ring-2 focus:ring-amber-500 focus:outline-none max-w-xs truncate"
            >
              {assessments.map(a => (
                <option key={a.id} value={a.id}>
                  {a.assessmentReference} - {a.title}
                </option>
              ))}
            </select>
          )}

          <button
            onClick={() => {
              setAssessmentForm({
                clientId: selectedClientId,
                assessmentReference: `APA-${selectedFy.substring(0, 4)}-${(assessments.length + 1).toString().padStart(3, '0')}`,
                title: 'Section 171 Anti-Profiteering Audit Review',
                financialYear: selectedFy,
                auditPeriodFrom: '2024-07-01',
                auditPeriodTo: '2024-09-30',
                gstNotificationId: notifications.length > 0 ? notifications[0].id : undefined,
                notificationReference: notifications.length > 0 ? notifications[0].notificationNumber : '',
                rateCutEffectiveDate: notifications.length > 0 ? notifications[0].effectiveDate.substring(0, 10) : '2024-07-01',
                leadAuditorName: 'CA Sneha Mehra, Partner'
              })
              setShowAssessmentModal(true)
            }}
            className="inline-flex items-center gap-1.5 px-3 py-2 text-sm font-medium text-white bg-amber-600 hover:bg-amber-700 rounded-lg shadow-sm transition"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
            </svg>
            New Assessment
          </button>
        </div>
      </div>

      {/* Loading & Notifications */}
      {loading && (
        <div className="flex items-center gap-2 text-xs text-amber-700 bg-amber-50/50 p-2.5 rounded-lg border border-amber-100">
          <svg className="animate-spin h-4 w-4 text-amber-600" fill="none" viewBox="0 0 24 24">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
          </svg>
          <span>Evaluating Section 171 commensurate price metrics and refund allocations...</span>
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
          {/* Post-Cut Audited Turnover */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">Post-Cut Turnover</p>
            <p className="text-2xl font-bold text-slate-900 mt-1">
              ₹{(summary.totalTurnoverAudited / 100000).toLocaleString('en-IN', { maximumFractionDigits: 1 })} L
            </p>
            <p className="text-xs text-slate-500 mt-1">Audited sales period</p>
          </div>

          {/* Undue Profiteering Identified */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-rose-600">Profiteered Amount</p>
            <p className="text-2xl font-bold text-rose-700 mt-1">
              ₹{summary.totalProfiteeredAmount.toLocaleString('en-IN', { maximumFractionDigits: 0 })}
            </p>
            <p className="text-xs text-rose-600 font-medium mt-1">
              {summary.totalProfiteeredSkus} of {summary.totalSkusAudited} SKUs affected
            </p>
          </div>

          {/* Refunded to Customers */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-emerald-600">Passed to Buyers</p>
            <p className="text-2xl font-bold text-emerald-700 mt-1">
              ₹{summary.totalRefundedToCustomers.toLocaleString('en-IN', { maximumFractionDigits: 0 })}
            </p>
            <p className="text-xs text-slate-500 mt-1">Credit notes verified</p>
          </div>

          {/* CWF Deposited */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">CWF Deposited</p>
            <p className="text-2xl font-bold text-blue-700 mt-1">
              ₹{summary.totalCwfDeposits.toLocaleString('en-IN', { maximumFractionDigits: 0 })}
            </p>
            <p className="text-xs text-slate-500 mt-1">50% Central + 50% State</p>
          </div>

          {/* Pending Remediation */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-amber-600">Remediation Gap</p>
            <p className="text-2xl font-bold text-amber-700 mt-1">
              ₹{summary.totalPendingRemediation.toLocaleString('en-IN', { maximumFractionDigits: 0 })}
            </p>
            <p className="text-xs text-amber-600 font-medium mt-1">
              {summary.totalPendingRemediation > 0 ? 'Requires immediate CWF deposit' : 'Fully remediated'}
            </p>
          </div>

          {/* Section 171(3A) Interest Risk */}
          <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <p className="text-xs font-semibold uppercase tracking-wider text-slate-500">Sec 171(3A) Interest (18%)</p>
            <p className="text-2xl font-bold text-slate-900 mt-1">
              ₹{(currentAssessment?.cwfInterestPayableAmount || 0).toLocaleString('en-IN', { maximumFractionDigits: 0 })}
            </p>
            <p className="text-xs text-slate-500 mt-1">Interest @ 18% p.a.</p>
          </div>
        </div>
      )}

      {/* Tabs Navigation */}
      <div className="border-b border-slate-200">
        <nav className="flex space-x-6">
          <button
            onClick={() => setActiveTab('skus')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'skus'
                ? 'border-amber-600 text-amber-700'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
            </svg>
            SKU Commensurate Price Matrix
            <span className="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-700 font-bold">{skus.length}</span>
          </button>

          <button
            onClick={() => setActiveTab('refunds')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'refunds'
                ? 'border-amber-600 text-amber-700'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M17 9V7a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2m2 4h10a2 2 0 002-2v-6a2 2 0 00-2-2H9a2 2 0 00-2 2v6a2 2 0 002 2zm7-5a2 2 0 11-4 0 2 2 0 014 0z" />
            </svg>
            Customer Refunds & Consumer Welfare Fund (CWF)
            <span className="text-xs px-2 py-0.5 rounded-full bg-amber-100 text-amber-800 font-bold">{refunds.length}</span>
          </button>

          <button
            onClick={() => setActiveTab('notifications')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'notifications'
                ? 'border-amber-600 text-amber-700'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M19 20H5a2 2 0 01-2-2V6a2 2 0 012-2h10a2 2 0 012 2v1m2 13a2 2 0 01-2-2V7m2 13a2 2 0 002-2V9a2 2 0 00-2-2h-2m-4-3H9M7 16h6M7 8h6v4H7V8z" />
            </svg>
            GST Rate Notifications Registry
            <span className="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-700 font-bold">{notifications.length}</span>
          </button>

          <button
            onClick={() => setActiveTab('certificate')}
            className={`py-3 px-1 border-b-2 font-medium text-sm flex items-center gap-2 ${
              activeTab === 'certificate'
                ? 'border-amber-600 text-amber-700'
                : 'border-transparent text-slate-500 hover:text-slate-700 hover:border-slate-300'
            }`}
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12l2 2 4-4m5.618-4.016A11.955 11.955 0 0112 2.944a11.955 11.955 0 01-8.618 3.04A12.02 12.02 0 003 9c0 5.591 3.824 10.29 9 11.622 5.176-1.332 9-6.03 9-11.622 0-1.042-.133-2.052-.382-3.016z" />
            </svg>
            Statutory CA Audit Certificate (UDIN)
          </button>
        </nav>
      </div>

      {/* TAB 1: SKU ANTI-PROFITEERING MATRIX */}
      {activeTab === 'skus' && (
        <div className="space-y-4">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <div className="flex flex-wrap items-center gap-3">
              <div className="flex items-center gap-2">
                <span className="text-xs font-semibold text-slate-500 uppercase">Status:</span>
                <select
                  value={statusFilter}
                  onChange={e => setStatusFilter(e.target.value)}
                  className="text-sm border border-slate-300 rounded-lg px-2.5 py-1.5 bg-white text-slate-700 focus:outline-none"
                >
                  <option value="All">All Compliance Statuses</option>
                  <option value="Compliant">Compliant (Passed On)</option>
                  <option value="Deficient">Deficient (Partial Reduction)</option>
                  <option value="Profiteered">Profiteered (Base Price Hiked)</option>
                </select>
              </div>

              <div className="relative">
                <input
                  type="text"
                  placeholder="Search SKU code, product, HSN..."
                  value={searchQuery}
                  onChange={e => setSearchQuery(e.target.value)}
                  className="text-sm pl-8 pr-3 py-1.5 border border-slate-300 rounded-lg focus:outline-none w-64"
                />
                <svg className="w-4 h-4 text-slate-400 absolute left-2.5 top-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => openSkuModal()}
                disabled={!selectedAssessmentId}
                className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-white bg-amber-600 hover:bg-amber-700 disabled:opacity-50 rounded-lg shadow-sm transition"
              >
                <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
                </svg>
                Add SKU for Evaluation
              </button>
            </div>
          </div>

          {/* Table */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-slate-200 text-left text-xs">
                <thead className="bg-slate-50 text-slate-600 font-semibold uppercase tracking-wider">
                  <tr>
                    <th className="px-4 py-3">SKU & HSN</th>
                    <th className="px-4 py-3">Product Name</th>
                    <th className="px-4 py-3 text-right">Pre-Cut Base (₹)</th>
                    <th className="px-4 py-3 text-center">GST Rates</th>
                    <th className="px-4 py-3 text-right">Commensurate Req.</th>
                    <th className="px-4 py-3 text-right">Actual Selling (₹)</th>
                    <th className="px-4 py-3 text-right">Base Hike %</th>
                    <th className="px-4 py-3 text-right">Profiteering (₹)</th>
                    <th className="px-4 py-3">Compliance Status</th>
                    <th className="px-4 py-3 text-right">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 text-slate-700">
                  {filteredSkus.length === 0 ? (
                    <tr>
                      <td colSpan={10} className="px-4 py-8 text-center text-slate-400">
                        No SKUs recorded. Click "Add SKU for Evaluation" to test commensurate price reductions.
                      </td>
                    </tr>
                  ) : (
                    filteredSkus.map(s => (
                      <tr key={s.id} className="hover:bg-slate-50 transition">
                        <td className="px-4 py-3 font-mono font-bold text-amber-900 whitespace-nowrap">
                          {s.productCode}
                          <div className="text-[10px] text-slate-400 font-normal">HSN: {s.hsnOrSacCode}</div>
                        </td>
                        <td className="px-4 py-3 max-w-xs">
                          <div className="font-semibold text-slate-800">{s.productName}</div>
                          <div className="text-[10px] text-slate-400">UOM: {s.unitOfMeasurement}</div>
                          {s.hasRawMaterialCostJustification && (
                            <span className="inline-block mt-0.5 text-[9px] px-1 py-0.2 rounded bg-blue-50 text-blue-700 border border-blue-200">
                              Cost Justified
                            </span>
                          )}
                        </td>
                        <td className="px-4 py-3 text-right font-medium text-slate-700">
                          ₹{s.preCutBasePrice.toFixed(2)}
                          <div className="text-[10px] text-slate-400">MRP: ₹{s.preCutSellingPrice.toFixed(2)}</div>
                        </td>
                        <td className="px-4 py-3 text-center whitespace-nowrap">
                          <span className="text-slate-500 line-through mr-1">{s.oldGstRate}%</span>
                          <span className="text-emerald-700 font-bold">&rarr; {s.newGstRate}%</span>
                        </td>
                        <td className="px-4 py-3 text-right font-bold text-indigo-700">
                          ₹{s.commensuratePriceRequired.toFixed(2)}
                        </td>
                        <td className="px-4 py-3 text-right font-semibold text-slate-900">
                          ₹{s.postCutActualSellingPrice.toFixed(2)}
                          <div className="text-[10px] text-slate-400">Base: ₹{s.postCutActualBasePrice.toFixed(2)}</div>
                        </td>
                        <td className="px-4 py-3 text-right font-medium">
                          <span className={s.basePriceIncreasePercentage > 0 ? 'text-rose-600 font-bold' : 'text-emerald-600'}>
                            {s.basePriceIncreasePercentage > 0 ? `+${s.basePriceIncreasePercentage.toFixed(2)}%` : '0%'}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right whitespace-nowrap">
                          <div className={`font-bold ${s.totalProfiteeredAmount > 0 ? 'text-rose-700' : 'text-slate-400'}`}>
                            ₹{s.totalProfiteeredAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                          </div>
                          {s.profiteeringPerUnit > 0 && (
                            <div className="text-[10px] text-rose-600">₹{s.profiteeringPerUnit.toFixed(2)} / unit</div>
                          )}
                        </td>
                        <td className="px-4 py-3 whitespace-nowrap">
                          <span className={`inline-block text-[11px] px-2 py-0.5 rounded border ${getStatusBadge(s.complianceStatus)}`}>
                            {s.complianceStatus}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right whitespace-nowrap">
                          <div className="flex items-center justify-end gap-1.5">
                            <button
                              onClick={() => openSkuModal(s)}
                              className="px-2 py-1 text-xs text-amber-700 hover:text-amber-900 bg-amber-50 hover:bg-amber-100 rounded transition font-medium"
                            >
                              Edit
                            </button>
                            <button
                              onClick={() => handleDeleteSku(s.id, s.productCode)}
                              className="px-2 py-1 text-xs text-rose-600 hover:text-rose-900 bg-rose-50 hover:bg-rose-100 rounded transition"
                            >
                              &times;
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      )}

      {/* TAB 2: CUSTOMER REFUNDS & CONSUMER WELFARE FUND */}
      {activeTab === 'refunds' && (
        <div className="space-y-4">
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-3 bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <div className="flex items-center gap-3">
              <span className="text-xs font-semibold text-slate-500 uppercase">Recipient Type:</span>
              <select
                value={recipientFilter}
                onChange={e => setRecipientFilter(e.target.value)}
                className="text-sm border border-slate-300 rounded-lg px-2.5 py-1.5 bg-white text-slate-700 focus:outline-none"
              >
                <option value="All">All Recipients</option>
                <option value="IdentifiedCustomer">Identified Customers (Credit Notes)</option>
                <option value="ConsumerWelfareFundCentral">Central Consumer Welfare Fund (CBIC)</option>
                <option value="ConsumerWelfareFundState">State Consumer Welfare Fund (SGST)</option>
              </select>
            </div>

            <button
              onClick={() => openRefundModal()}
              disabled={!selectedAssessmentId}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-white bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 rounded-lg shadow-sm transition"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
              </svg>
              Record Refund / CWF Deposit
            </button>
          </div>

          {/* Statutory Rule 133(3)(c) Note */}
          <div className="p-3 bg-blue-50 border border-blue-200 rounded-xl text-xs text-blue-900">
            <b>Rule 133(3)(c) Statutory Mandate:</b> Where the recipients of the benefit are not identifiable, the entire undue profiteered amount must be deposited in equal 50:50 proportion into the <b>Central Consumer Welfare Fund</b> and the relevant <b>State Consumer Welfare Fund</b>, along with interest at <b>18% p.a.</b> under Section 171(3A) from the date of collection to the date of deposit.
          </div>

          {/* Refunds List */}
          <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <table className="min-w-full divide-y divide-slate-200 text-left text-xs">
              <thead className="bg-slate-50 text-slate-600 font-semibold uppercase tracking-wider">
                <tr>
                  <th className="px-4 py-3">Recipient Type</th>
                  <th className="px-4 py-3">Beneficiary / Fund Name</th>
                  <th className="px-4 py-3">Reference / Challan</th>
                  <th className="px-4 py-3">Mode & Date</th>
                  <th className="px-4 py-3 text-right">Refund / Deposit (₹)</th>
                  <th className="px-4 py-3 text-right">Sec 171(3A) Interest</th>
                  <th className="px-4 py-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 text-slate-700">
                {filteredRefunds.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="px-4 py-8 text-center text-slate-400">
                      No refund or CWF deposits recorded yet. Click "Record Refund / CWF Deposit" to log remediation.
                    </td>
                  </tr>
                ) : (
                  filteredRefunds.map(r => (
                    <tr key={r.id} className="hover:bg-slate-50 transition">
                      <td className="px-4 py-3 whitespace-nowrap">
                        <span className={`inline-block text-[10px] font-bold px-2 py-0.5 rounded border ${
                          r.recipientType === 'IdentifiedCustomer'
                            ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
                            : 'bg-blue-50 text-blue-800 border-blue-200'
                        }`}>
                          {r.recipientType === 'IdentifiedCustomer' ? 'Customer Refund' : r.recipientType === 'ConsumerWelfareFundCentral' ? 'Central CWF' : 'State CWF'}
                        </span>
                      </td>
                      <td className="px-4 py-3 font-semibold text-slate-900">
                        {r.customerNameOrEntity}
                        {r.customerGstin && <div className="text-[10px] text-slate-400 font-mono">GSTIN: {r.customerGstin}</div>}
                      </td>
                      <td className="px-4 py-3 font-mono text-slate-600">
                        {r.invoiceReference || r.challanOrCinNumber || 'N/A'}
                      </td>
                      <td className="px-4 py-3 whitespace-nowrap">
                        <span className="font-semibold text-slate-700">{r.refundMode}</span>
                        <div className="text-[10px] text-slate-400">{r.refundOrDepositDate.substring(0, 10)}</div>
                      </td>
                      <td className="px-4 py-3 text-right font-bold text-emerald-700 text-sm whitespace-nowrap">
                        ₹{r.refundAmount.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td className="px-4 py-3 text-right font-semibold text-slate-700 whitespace-nowrap">
                        ₹{r.interestAmountPaid.toLocaleString('en-IN', { maximumFractionDigits: 2 })}
                      </td>
                      <td className="px-4 py-3 text-right whitespace-nowrap">
                        <div className="flex items-center justify-end gap-1.5">
                          <button
                            onClick={() => openRefundModal(r)}
                            className="px-2 py-1 text-xs text-indigo-600 hover:text-indigo-900 bg-indigo-50 rounded"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDeleteRefund(r.id)}
                            className="px-2 py-1 text-xs text-rose-600 hover:text-rose-900 bg-rose-50 rounded"
                          >
                            &times;
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 3: GST RATE NOTIFICATIONS REGISTRY */}
      {activeTab === 'notifications' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <div>
              <h3 className="text-base font-bold text-slate-900">Landmark GST Council Rate Reduction Notifications</h3>
              <p className="text-xs text-slate-500">Official tariff cuts triggering mandatory Section 171 price reduction audits</p>
            </div>
            <button
              onClick={() => {
                setNotifForm({
                  notificationNumber: '',
                  notificationDate: new Date().toISOString().substring(0, 10),
                  effectiveDate: new Date().toISOString().substring(0, 10),
                  hsnOrSacCode: '',
                  description: '',
                  previousRatePercentage: 18,
                  revisedRatePercentage: 12,
                  isActive: true,
                  notes: ''
                })
                setShowNotificationModal(true)
              }}
              className="inline-flex items-center gap-1.5 px-3 py-1.5 text-sm font-medium text-white bg-slate-800 hover:bg-slate-900 rounded-lg shadow-sm transition"
            >
              <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
              </svg>
              Add Notification
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {notifications.map(n => (
              <div key={n.id} className="bg-white p-5 rounded-xl border border-slate-200 shadow-sm flex flex-col justify-between">
                <div>
                  <div className="flex items-center justify-between">
                    <span className="font-mono text-xs font-bold px-2 py-0.5 rounded bg-amber-50 text-amber-800 border border-amber-200">
                      HSN {n.hsnOrSacCode}
                    </span>
                    <span className="text-xs px-2 py-0.5 rounded-full font-bold bg-emerald-100 text-emerald-800">
                      -{n.rateReductionPercentage}% Cut
                    </span>
                  </div>

                  <h4 className="text-sm font-bold text-slate-900 mt-2">{n.notificationNumber}</h4>
                  <p className="text-xs text-slate-600 mt-1 line-clamp-2">{n.description}</p>

                  <div className="mt-3 p-2.5 bg-slate-50 rounded-lg border text-xs grid grid-cols-2 gap-2">
                    <div>
                      <p className="text-slate-400 text-[10px] uppercase font-semibold">Previous Rate</p>
                      <p className="font-bold text-slate-800">{n.previousRatePercentage}%</p>
                    </div>
                    <div>
                      <p className="text-slate-400 text-[10px] uppercase font-semibold">Revised Rate</p>
                      <p className="font-bold text-emerald-700">{n.revisedRatePercentage}%</p>
                    </div>
                  </div>
                </div>

                <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between text-xs text-slate-400">
                  <span>Effective: <b>{n.effectiveDate.substring(0, 10)}</b></span>
                  <button
                    onClick={() => {
                      setAssessmentForm({
                        ...assessmentForm,
                        gstNotificationId: n.id,
                        notificationReference: n.notificationNumber,
                        rateCutEffectiveDate: n.effectiveDate.substring(0, 10)
                      })
                      setShowAssessmentModal(true)
                    }}
                    className="text-amber-700 hover:text-amber-900 font-semibold"
                  >
                    Create Audit &rarr;
                  </button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* TAB 4: STATUTORY AUDIT CERTIFICATE */}
      {activeTab === 'certificate' && (
        <div className="space-y-4">
          <div className="flex items-center justify-between bg-white p-4 rounded-xl border border-slate-200 shadow-sm">
            <div>
              <h3 className="text-base font-bold text-slate-900">
                Statutory Chartered Accountant's Anti-Profiteering Audit Certificate
              </h3>
              <p className="text-xs text-slate-500">
                Compliant with CGST Section 171 & Rules 122–137 for DGAP / CCI / GST Departmental Verification
              </p>
            </div>
            <div className="flex items-center gap-2">
              <button
                onClick={() => setShowCertifyModal(true)}
                disabled={!selectedAssessmentId}
                className="px-3 py-1.5 text-xs font-medium text-white bg-amber-600 hover:bg-amber-700 rounded-lg shadow-sm transition"
              >
                {currentAssessment?.udin ? 'Re-Certify / Update UDIN' : 'Certify Assessment (Generate UDIN)'}
              </button>
              {currentAssessment?.statutoryReportText && (
                <button
                  onClick={() => {
                    navigator.clipboard.writeText(currentAssessment.statutoryReportText || '')
                    notifySuccess('Statutory certificate copied to clipboard!')
                  }}
                  className="px-3 py-1.5 text-xs font-medium text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-lg transition"
                >
                  Copy Text
                </button>
              )}
            </div>
          </div>

          {currentAssessment?.statutoryReportText ? (
            <div className="bg-slate-900 text-slate-100 p-6 rounded-xl font-mono text-xs whitespace-pre-wrap leading-relaxed shadow-inner overflow-x-auto">
              {currentAssessment.statutoryReportText}
            </div>
          ) : (
            <div className="bg-white p-12 rounded-xl border border-slate-200 text-center space-y-3">
              <p className="text-sm text-slate-500">
                Audit certificate has not been compiled yet. Click "Certify Assessment" to generate the official Section 171 Certificate.
              </p>
              <button
                onClick={() => setShowCertifyModal(true)}
                className="px-4 py-2 text-sm font-medium text-white bg-amber-600 hover:bg-amber-700 rounded-lg shadow-sm transition"
              >
                Certify & Sign-off Now
              </button>
            </div>
          )}
        </div>
      )}

      {/* MODAL 1: NEW ASSESSMENT */}
      {showAssessmentModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-base font-bold text-slate-900">New Anti-Profiteering Assessment</h3>
              <button onClick={() => setShowAssessmentModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">&times;</button>
            </div>

            <form onSubmit={handleSaveAssessment} className="space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Assessment Reference</label>
                <input
                  type="text"
                  required
                  value={assessmentForm.assessmentReference}
                  onChange={e => setAssessmentForm({ ...assessmentForm, assessmentReference: e.target.value })}
                  className="w-full border border-slate-300 rounded px-3 py-2 font-mono uppercase"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Title</label>
                <input
                  type="text"
                  required
                  value={assessmentForm.title}
                  onChange={e => setAssessmentForm({ ...assessmentForm, title: e.target.value })}
                  className="w-full border border-slate-300 rounded px-3 py-2"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Audit Period From</label>
                  <input
                    type="date"
                    required
                    value={assessmentForm.auditPeriodFrom.substring(0, 10)}
                    onChange={e => setAssessmentForm({ ...assessmentForm, auditPeriodFrom: e.target.value })}
                    className="w-full border border-slate-300 rounded px-3 py-2"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Audit Period To</label>
                  <input
                    type="date"
                    required
                    value={assessmentForm.auditPeriodTo.substring(0, 10)}
                    onChange={e => setAssessmentForm({ ...assessmentForm, auditPeriodTo: e.target.value })}
                    className="w-full border border-slate-300 rounded px-3 py-2"
                  />
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Linked GST Rate Notification</label>
                <select
                  value={assessmentForm.gstNotificationId || ''}
                  onChange={e => {
                    const sel = notifications.find(n => n.id === e.target.value)
                    setAssessmentForm({
                      ...assessmentForm,
                      gstNotificationId: sel ? sel.id : undefined,
                      notificationReference: sel ? sel.notificationNumber : '',
                      rateCutEffectiveDate: sel ? sel.effectiveDate.substring(0, 10) : undefined
                    })
                  }}
                  className="w-full border border-slate-300 rounded px-3 py-2"
                >
                  <option value="">-- Manual Notification --</option>
                  {notifications.map(n => (
                    <option key={n.id} value={n.id}>
                      {n.notificationNumber} (HSN {n.hsnOrSacCode}: {n.previousRatePercentage}% &rarr; {n.revisedRatePercentage}%)
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Lead Auditor / CA Name</label>
                <input
                  type="text"
                  value={assessmentForm.leadAuditorName || ''}
                  onChange={e => setAssessmentForm({ ...assessmentForm, leadAuditorName: e.target.value })}
                  className="w-full border border-slate-300 rounded px-3 py-2"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowAssessmentModal(false)}
                  className="px-3 py-2 border rounded-lg text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-medium"
                >
                  Create Assessment
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: ADD / EDIT SKU */}
      {showSkuModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-xl w-full p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-base font-bold text-slate-900">
                {editingSku ? `Edit SKU: ${editingSku.productCode}` : 'Add SKU for Section 171 Evaluation'}
              </h3>
              <button onClick={() => setShowSkuModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">&times;</button>
            </div>

            <form onSubmit={handleSaveSku} className="space-y-4 text-xs">
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">SKU / Product Code</label>
                  <input
                    type="text"
                    required
                    value={skuForm.productCode}
                    onChange={e => setSkuForm({ ...skuForm, productCode: e.target.value })}
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5 font-mono uppercase"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">HSN / SAC</label>
                  <input
                    type="text"
                    required
                    value={skuForm.hsnOrSacCode}
                    onChange={e => setSkuForm({ ...skuForm, hsnOrSacCode: e.target.value })}
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5 font-mono"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Unit of Measure</label>
                  <input
                    type="text"
                    value={skuForm.unitOfMeasurement || 'PCS'}
                    onChange={e => setSkuForm({ ...skuForm, unitOfMeasurement: e.target.value })}
                    className="w-full border border-slate-300 rounded px-2.5 py-1.5 uppercase font-mono"
                  />
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Product Description</label>
                <input
                  type="text"
                  required
                  value={skuForm.productName}
                  onChange={e => setSkuForm({ ...skuForm, productName: e.target.value })}
                  placeholder="e.g. 55-inch 4K Smart LED Television"
                  className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                />
              </div>

              {/* Price & Tax Inputs */}
              <div className="p-3 bg-slate-50 rounded-lg border space-y-3">
                <p className="font-bold text-slate-900 uppercase tracking-wider text-[11px]">Pre-Reduction Benchmarks</p>
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block text-slate-600 mb-1">Pre-Cut Base Price (excl. GST) (₹)</label>
                    <input
                      type="number"
                      step="0.01"
                      required
                      value={skuForm.preCutBasePrice}
                      onChange={e => setSkuForm({ ...skuForm, preCutBasePrice: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-slate-300 rounded px-2.5 py-1.5 font-semibold"
                    />
                  </div>
                  <div>
                    <label className="block text-slate-600 mb-1">Old GST Rate (%)</label>
                    <input
                      type="number"
                      step="0.1"
                      required
                      value={skuForm.oldGstRate}
                      onChange={e => setSkuForm({ ...skuForm, oldGstRate: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-slate-300 rounded px-2.5 py-1.5"
                    />
                  </div>
                </div>
              </div>

              <div className="p-3 bg-amber-50/50 rounded-lg border border-amber-200 space-y-3">
                <p className="font-bold text-amber-950 uppercase tracking-wider text-[11px]">Post-Reduction Actuals</p>
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <label className="block text-slate-600 mb-1">New GST Rate (%)</label>
                    <input
                      type="number"
                      step="0.1"
                      required
                      value={skuForm.newGstRate}
                      onChange={e => setSkuForm({ ...skuForm, newGstRate: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-slate-300 rounded px-2.5 py-1.5 font-bold text-emerald-700"
                    />
                  </div>
                  <div>
                    <label className="block text-slate-600 mb-1">Post-Cut Base Price (₹)</label>
                    <input
                      type="number"
                      step="0.01"
                      required
                      value={skuForm.postCutActualBasePrice}
                      onChange={e => setSkuForm({ ...skuForm, postCutActualBasePrice: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-slate-300 rounded px-2.5 py-1.5 font-semibold"
                    />
                  </div>
                  <div>
                    <label className="block text-slate-600 mb-1">Units Sold Post-Cut</label>
                    <input
                      type="number"
                      min={0}
                      required
                      value={skuForm.quantitySoldPostCut}
                      onChange={e => setSkuForm({ ...skuForm, quantitySoldPostCut: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-slate-300 rounded px-2.5 py-1.5 font-semibold"
                    />
                  </div>
                </div>

                <div className="text-[11px] text-slate-600 flex justify-between pt-1">
                  <span>Commensurate Price Required: <b>₹{(skuForm.preCutBasePrice * (1 + skuForm.newGstRate / 100)).toFixed(2)}</b></span>
                  <span>Actual Selling Price: <b>₹{(skuForm.postCutActualBasePrice * (1 + skuForm.newGstRate / 100)).toFixed(2)}</b></span>
                </div>
              </div>

              <div className="p-3 bg-slate-50 rounded-lg border space-y-2">
                <div className="flex items-center gap-2">
                  <input
                    type="checkbox"
                    id="costJust"
                    checked={skuForm.hasRawMaterialCostJustification}
                    onChange={e => setSkuForm({ ...skuForm, hasRawMaterialCostJustification: e.target.checked })}
                    className="rounded text-amber-600 focus:ring-amber-500"
                  />
                  <label htmlFor="costJust" className="font-semibold text-slate-700">
                    Documented Raw Material / Input Cost Justification Exists
                  </label>
                </div>
                {skuForm.hasRawMaterialCostJustification && (
                  <textarea
                    rows={2}
                    value={skuForm.justificationRemarks || ''}
                    onChange={e => setSkuForm({ ...skuForm, justificationRemarks: e.target.value })}
                    placeholder="Reference procurement bills or commodity price indices demonstrating actual cost increase..."
                    className="w-full border border-slate-300 rounded px-2 py-1 text-xs"
                  />
                )}
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowSkuModal(false)}
                  className="px-3 py-2 border rounded-lg text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-medium"
                >
                  Save & Evaluate SKU
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: RECORD REFUND / CWF DEPOSIT */}
      {showRefundModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-base font-bold text-slate-900">
                {editingRefund ? 'Edit Refund Record' : 'Record Customer Refund / CWF Deposit'}
              </h3>
              <button onClick={() => setShowRefundModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">&times;</button>
            </div>

            <form onSubmit={handleSaveRefund} className="space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Recipient Category</label>
                <select
                  value={refundForm.recipientType}
                  onChange={e => {
                    const type = e.target.value
                    setRefundForm({
                      ...refundForm,
                      recipientType: type,
                      customerNameOrEntity: type === 'ConsumerWelfareFundCentral' ? 'Central Consumer Welfare Fund (CBIC)' : type === 'ConsumerWelfareFundState' ? 'State Consumer Welfare Fund (SGST)' : ''
                    })
                  }}
                  className="w-full border border-slate-300 rounded px-3 py-2 font-bold"
                >
                  <option value="IdentifiedCustomer">Identified Customer (Commercial Credit Note / Bank Transfer)</option>
                  <option value="ConsumerWelfareFundCentral">Central Consumer Welfare Fund (CBIC)</option>
                  <option value="ConsumerWelfareFundState">State Consumer Welfare Fund (SGST)</option>
                </select>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Recipient / Entity Name</label>
                <input
                  type="text"
                  required
                  value={refundForm.customerNameOrEntity}
                  onChange={e => setRefundForm({ ...refundForm, customerNameOrEntity: e.target.value })}
                  className="w-full border border-slate-300 rounded px-3 py-2"
                />
              </div>

              {refundForm.recipientType === 'IdentifiedCustomer' && (
                <div className="grid grid-cols-2 gap-3">
                  <div>
                    <label className="block font-semibold text-slate-700 mb-1">Customer GSTIN</label>
                    <input
                      type="text"
                      value={refundForm.customerGstin || ''}
                      onChange={e => setRefundForm({ ...refundForm, customerGstin: e.target.value })}
                      className="w-full border border-slate-300 rounded px-3 py-2 font-mono uppercase"
                    />
                  </div>
                  <div>
                    <label className="block font-semibold text-slate-700 mb-1">Invoice / CN Reference</label>
                    <input
                      type="text"
                      value={refundForm.invoiceReference || ''}
                      onChange={e => setRefundForm({ ...refundForm, invoiceReference: e.target.value })}
                      className="w-full border border-slate-300 rounded px-3 py-2 font-mono"
                    />
                  </div>
                </div>
              )}

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Refund / Deposit Amount (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    min={0}
                    required
                    value={refundForm.refundAmount}
                    onChange={e => setRefundForm({ ...refundForm, refundAmount: parseFloat(e.target.value) || 0 })}
                    className="w-full border border-slate-300 rounded px-3 py-2 font-bold text-emerald-700 text-sm"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Mode</label>
                  <select
                    value={refundForm.refundMode || 'CreditNote'}
                    onChange={e => setRefundForm({ ...refundForm, refundMode: e.target.value })}
                    className="w-full border border-slate-300 rounded px-3 py-2"
                  >
                    <option value="CreditNote">Commercial Credit Note</option>
                    <option value="BankTransfer">Direct Bank Transfer (NEFT/RTGS)</option>
                    <option value="ChallanGST_PMT_06">GST PMT-06 Electronic Cash Ledger</option>
                  </select>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Date of Refund / Deposit</label>
                  <input
                    type="date"
                    required
                    value={refundForm.refundOrDepositDate}
                    onChange={e => setRefundForm({ ...refundForm, refundOrDepositDate: e.target.value })}
                    className="w-full border border-slate-300 rounded px-3 py-2"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Sec 171(3A) Interest Paid (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    min={0}
                    value={refundForm.interestAmountPaid}
                    onChange={e => setRefundForm({ ...refundForm, interestAmountPaid: parseFloat(e.target.value) || 0 })}
                    className="w-full border border-slate-300 rounded px-3 py-2"
                  />
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">CIN / Bank UTR / Challan Number</label>
                <input
                  type="text"
                  value={refundForm.challanOrCinNumber || ''}
                  onChange={e => setRefundForm({ ...refundForm, challanOrCinNumber: e.target.value })}
                  placeholder="e.g. CPIN-9812450123 / UTR-HDFC0012345"
                  className="w-full border border-slate-300 rounded px-3 py-2 font-mono"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowRefundModal(false)}
                  className="px-3 py-2 border rounded-lg text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 font-medium"
                >
                  Save Record
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: ADD RATE NOTIFICATION */}
      {showNotificationModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-base font-bold text-slate-900">Add GST Rate Notification</h3>
              <button onClick={() => setShowNotificationModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">&times;</button>
            </div>

            <form onSubmit={handleSaveNotification} className="space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Notification Number</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. 18/2018-Central Tax (Rate)"
                  value={notifForm.notificationNumber}
                  onChange={e => setNotifForm({ ...notifForm, notificationNumber: e.target.value })}
                  className="w-full border border-slate-300 rounded px-3 py-2"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Notification Date</label>
                  <input
                    type="date"
                    required
                    value={notifForm.notificationDate}
                    onChange={e => setNotifForm({ ...notifForm, notificationDate: e.target.value })}
                    className="w-full border border-slate-300 rounded px-3 py-2"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Effective Date</label>
                  <input
                    type="date"
                    required
                    value={notifForm.effectiveDate}
                    onChange={e => setNotifForm({ ...notifForm, effectiveDate: e.target.value })}
                    className="w-full border border-slate-300 rounded px-3 py-2"
                  />
                </div>
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">HSN or SAC Code</label>
                <input
                  type="text"
                  required
                  placeholder="e.g. 8418"
                  value={notifForm.hsnOrSacCode}
                  onChange={e => setNotifForm({ ...notifForm, hsnOrSacCode: e.target.value })}
                  className="w-full border border-slate-300 rounded px-3 py-2 font-mono"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">Goods / Services Description</label>
                <textarea
                  rows={2}
                  required
                  placeholder="e.g. Refrigerators, freezers, and household appliances"
                  value={notifForm.description}
                  onChange={e => setNotifForm({ ...notifForm, description: e.target.value })}
                  className="w-full border border-slate-300 rounded px-3 py-2"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Previous Rate (%)</label>
                  <input
                    type="number"
                    step="0.1"
                    required
                    value={notifForm.previousRatePercentage}
                    onChange={e => setNotifForm({ ...notifForm, previousRatePercentage: parseFloat(e.target.value) || 0 })}
                    className="w-full border border-slate-300 rounded px-3 py-2"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-700 mb-1">Revised Rate (%)</label>
                  <input
                    type="number"
                    step="0.1"
                    required
                    value={notifForm.revisedRatePercentage}
                    onChange={e => setNotifForm({ ...notifForm, revisedRatePercentage: parseFloat(e.target.value) || 0 })}
                    className="w-full border border-slate-300 rounded px-3 py-2 font-bold text-emerald-700"
                  />
                </div>
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowNotificationModal(false)}
                  className="px-3 py-2 border rounded-lg text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-slate-800 text-white rounded-lg hover:bg-slate-900 font-medium"
                >
                  Save Notification
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 5: CERTIFY ASSESSMENT WITH UDIN */}
      {showCertifyModal && (
        <div className="fixed inset-0 z-50 bg-slate-900/40 backdrop-blur-sm flex items-center justify-center p-4">
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-base font-bold text-slate-900">Certify Anti-Profiteering Audit (UDIN)</h3>
              <button onClick={() => setShowCertifyModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">&times;</button>
            </div>

            <form onSubmit={handleCertify} className="space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-700 mb-1">Lead Signing Auditor / Partner</label>
                <input
                  type="text"
                  required
                  value={certifyForm.leadAuditorName}
                  onChange={e => setCertifyForm({ ...certifyForm, leadAuditorName: e.target.value })}
                  className="w-full border border-slate-300 rounded px-3 py-2"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-700 mb-1">18-Digit ICAI UDIN</label>
                <input
                  type="text"
                  required
                  maxLength={18}
                  placeholder="24078512AAAAAP7766"
                  value={certifyForm.udin || ''}
                  onChange={e => setCertifyForm({ ...certifyForm, udin: e.target.value.toUpperCase() })}
                  className="w-full border border-slate-300 rounded px-3 py-2 font-mono uppercase tracking-widest text-sm font-bold"
                />
                <p className="text-[10px] text-slate-400 mt-1">Unique Document Identification Number for statutory certificate</p>
              </div>

              <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg text-xs text-amber-900">
                Signing this certificate generates the official Section 171 Audit Report with commensurate price schedules and CWF deposit recommendations.
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyModal(false)}
                  className="px-3 py-2 border rounded-lg text-slate-600 hover:bg-slate-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-amber-600 text-white rounded-lg hover:bg-amber-700 font-medium"
                >
                  Certify & Issue Report
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
