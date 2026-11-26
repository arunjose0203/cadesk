import React, { useState, useEffect, useCallback } from 'react'
import {
  toError,
  api,
  transferPricingApi,
  type Client,
  type TransferPricingSummaryDto,
  type TransferPricingStudyDto,
  type AssociatedEnterpriseDto,
  type TransferPricingTransactionDto,
  type ComparableBenchmarkDto,
  type SaveStudyRequest,
  type CertifyStudyRequest,
  type SaveEnterpriseRequest,
  type SaveTransactionRequest,
  type SaveComparableRequest,
  type BenchmarkCalculationResult,
} from './api'

type ClientPage = {
  items: Client[]
  total: number
}

export const TransferPricingPage: React.FC = () => {
  // State
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)
  const [successMsg, setSuccessMsg] = useState<string | null>(null)

  const [summary, setSummary] = useState<TransferPricingSummaryDto | null>(null)
  const [studies, setStudies] = useState<TransferPricingStudyDto[]>([])
  const [clients, setClients] = useState<Client[]>([])
  const [selectedStudyId, setSelectedStudyId] = useState<string | null>(null)
  const [currentStudy, setCurrentStudy] = useState<TransferPricingStudyDto | null>(null)

  // Sub-entity tabs
  const [activeTab, setActiveTab] = useState<'enterprises' | 'transactions' | 'comparables' | 'report'>('transactions')
  const [enterprises, setEnterprises] = useState<AssociatedEnterpriseDto[]>([])
  const [transactions, setTransactions] = useState<TransferPricingTransactionDto[]>([])
  const [comparables, setComparables] = useState<ComparableBenchmarkDto[]>([])

  // Modals
  const [showBenchmarkCalcModal, setShowBenchmarkCalcModal] = useState<boolean>(false)
  const [showStudyModal, setShowStudyModal] = useState<boolean>(false)
  const [showCertifyModal, setShowCertifyModal] = useState<boolean>(false)
  const [showAeModal, setShowAeModal] = useState<boolean>(false)
  const [showTxModal, setShowTxModal] = useState<boolean>(false)
  const [showCompModal, setShowCompModal] = useState<boolean>(false)

  // Benchmark Calculator State
  const [calcInputMargins, setCalcInputMargins] = useState<string>('12.5, 14.0, 15.2, 16.8, 18.0, 20.5, 22.1')
  const [calcToleranceBand, setCalcToleranceBand] = useState<number>(3.0)
  const [calcResult, setCalcResult] = useState<BenchmarkCalculationResult | null>(null)

  // Study Form State
  const [studyForm, setStudyForm] = useState<SaveStudyRequest>({
    clientId: '',
    studyReference: `TP-${new Date().getFullYear()}-001`,
    title: `Transfer Pricing Study & Form 3CEB FY ${new Date().getFullYear() - 1}-${new Date().getFullYear().toString().slice(-2)}`,
    financialYear: `${new Date().getFullYear() - 1}-${new Date().getFullYear().toString().slice(-2)}`,
    assessmentYear: `${new Date().getFullYear()}-${(new Date().getFullYear() + 1).toString().slice(-2)}`,
    auditPeriodStart: `${new Date().getFullYear() - 1}-04-01`,
    auditPeriodEnd: `${new Date().getFullYear()}-03-31`,
    rule10DDocumentationMaintained: true,
    rule10DDocumentationPath: '/docs/tp/LocalFile_TP_Study.pdf',
    accountantName: '',
    accountantFirmName: '',
    membershipNumber: '',
    firmRegistrationNumber: '',
  })
  const [editingStudyId, setEditingStudyId] = useState<string | null>(null)

  // Certify Form State
  const [certifyForm, setCertifyForm] = useState<CertifyStudyRequest>({
    udin: '',
    certifiedBy: '',
  })

  // AE Form State
  const [aeForm, setAeForm] = useState<SaveEnterpriseRequest>({
    enterpriseName: '',
    countryOfResidence: 'USA',
    countryCode: 'US',
    taxIdentificationNumber: '',
    registeredAddress: '',
    relationshipSection: 'Sec 92A(2)(a) - Holding >= 26% Voting Power',
    ownershipPercentage: 100,
    briefActivityDescription: 'Marketing and overseas distribution of parent products',
  })
  const [editingAeId, setEditingAeId] = useState<string | null>(null)

  // Transaction Form State
  const [txForm, setTxForm] = useState<SaveTransactionRequest>({
    associatedEnterpriseId: undefined,
    transactionCategory: 'InternationalTransaction',
    clauseNumber: 13,
    descriptionOfTransaction: 'Provision of Software Development & Engineering Services',
    currencyCode: 'USD',
    foreignCurrencyAmount: 1000000,
    exchangeRate: 83.50,
    mostAppropriateMethod: 'TNMM',
    profitLevelIndicator: 'OP_TC',
    testedPartyCostBase: 70000000,
    testedPartyMargin: 15.0,
    toleranceBandPercentage: 3.0,
    methodSelectionRationale: 'TNMM selected as most appropriate method given captive services profile.',
    documentationReference: 'Rule 10D Study Report Chapter 5',
  })
  const [editingTxId, setEditingTxId] = useState<string | null>(null)

  // Comparable Form State
  const [compForm, setCompForm] = useState<SaveComparableRequest>({
    transactionId: undefined,
    comparableCompanyName: '',
    cinOrIdentifier: '',
    financialYear: `${new Date().getFullYear() - 1}-${new Date().getFullYear().toString().slice(-2)}`,
    operatingRevenue: 100000000,
    operatingCost: 85000000,
    operatingProfit: 15000000,
    weight: 1.0,
    isAccepted: true,
    rejectionReason: '',
  })
  const [editingCompId, setEditingCompId] = useState<string | null>(null)

  const loadData = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const [sumData, studiesData, clsData] = await Promise.all([
        transferPricingApi.getSummary(),
        transferPricingApi.listStudies(),
        api<ClientPage>('/api/clients?pageSize=100').catch(() => ({ items: [], total: 0 })),
      ])
      setSummary(sumData)
      setStudies(studiesData)
      setClients(clsData.items || [])

      if (studiesData.length > 0) {
        setSelectedStudyId(prev => prev || studiesData[0].id)
      }
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to load transfer pricing data.')
    } finally {
      setLoading(false)
    }
  }, [])

  const loadStudyDetails = useCallback(async (id: string) => {
    try {
      const study = await transferPricingApi.getStudy(id)
      setCurrentStudy(study)

      if (activeTab === 'enterprises') {
        const aeList = await transferPricingApi.listEnterprises(id)
        setEnterprises(aeList)
      } else if (activeTab === 'transactions') {
        const [txList, aeList] = await Promise.all([
          transferPricingApi.listTransactions(id),
          transferPricingApi.listEnterprises(id),
        ])
        setTransactions(txList)
        setEnterprises(aeList)
      } else if (activeTab === 'comparables') {
        const cList = await transferPricingApi.listComparables(id)
        setComparables(cList)
      }
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to load study details.')
    }
  }, [activeTab])

  // Initial Load
  useEffect(() => {
    loadData()
  }, [loadData])

  // When selected study changes, reload its tabs
  useEffect(() => {
    if (selectedStudyId) {
      loadStudyDetails(selectedStudyId)
    }
  }, [selectedStudyId, loadStudyDetails])

  const handleComputeBenchmark = async () => {
    try {
      const margins = calcInputMargins
        .split(',')
        .map((s) => parseFloat(s.trim()))
        .filter((n) => !isNaN(n))

      if (margins.length === 0) {
        setError('Please enter at least one valid numerical margin.')
        return
      }

      const res = await transferPricingApi.calculateBenchmark({
        comparableMargins: margins,
        toleranceBandPercentage: calcToleranceBand,
      })
      setCalcResult(res)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Benchmark calculation failed.')
    }
  }

  const handleSaveStudy = async (e: React.FormEvent) => {
    e.preventDefault()
    try {
      if (editingStudyId) {
        await transferPricingApi.updateStudy(editingStudyId, studyForm)
        setSuccessMsg('Transfer Pricing Study updated successfully.')
      } else {
        const created = await transferPricingApi.createStudy(studyForm)
        setSelectedStudyId(created.id)
        setSuccessMsg('Transfer Pricing Study created successfully.')
      }
      setShowStudyModal(false)
      setEditingStudyId(null)
      await loadData()
      if (selectedStudyId) loadStudyDetails(selectedStudyId)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save transfer pricing study.')
    }
  }

  const handleDeleteStudy = async (id: string) => {
    if (!window.confirm('Delete this Transfer Pricing Study? All linked AEs, transactions and comparables will be removed.')) return
    try {
      await transferPricingApi.deleteStudy(id)
      setSuccessMsg('Study deleted.')
      setSelectedStudyId(null)
      setCurrentStudy(null)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete study.')
    }
  }

  const handleCertify = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentStudy) return
    try {
      const updated = await transferPricingApi.certifyStudy(currentStudy.id, certifyForm)
      setCurrentStudy(updated)
      setShowCertifyModal(false)
      setSuccessMsg(`Form 3CEB successfully certified with UDIN ${updated.udin}`)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Certification failed. Ensure UDIN is 18 characters.')
    }
  }

  const handleRegenerateReport = async () => {
    if (!currentStudy) return
    try {
      const res = await transferPricingApi.generate3CEB(currentStudy.id)
      setCurrentStudy({ ...currentStudy, form3CebReportText: res.reportText })
      setSuccessMsg('Form 3CEB report text regenerated.')
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to regenerate report.')
    }
  }

  // AE saves
  const handleSaveAe = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentStudy) return
    try {
      if (editingAeId) {
        await transferPricingApi.updateEnterprise(currentStudy.id, editingAeId, aeForm)
        setSuccessMsg('Associated Enterprise updated.')
      } else {
        await transferPricingApi.createEnterprise(currentStudy.id, aeForm)
        setSuccessMsg('Associated Enterprise added.')
      }
      setShowAeModal(false)
      setEditingAeId(null)
      await loadStudyDetails(currentStudy.id)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save Associated Enterprise.')
    }
  }

  const handleDeleteAe = async (aeId: string) => {
    if (!currentStudy) return
    if (!window.confirm('Delete this Associated Enterprise?')) return
    try {
      await transferPricingApi.deleteEnterprise(currentStudy.id, aeId)
      await loadStudyDetails(currentStudy.id)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete Associated Enterprise.')
    }
  }

  // Transaction saves
  const handleSaveTx = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentStudy) return
    try {
      if (editingTxId) {
        await transferPricingApi.updateTransaction(currentStudy.id, editingTxId, txForm)
        setSuccessMsg('Transaction updated and arm\'s length status recalculated.')
      } else {
        await transferPricingApi.createTransaction(currentStudy.id, txForm)
        setSuccessMsg('Transaction added and arm\'s length evaluated.')
      }
      setShowTxModal(false)
      setEditingTxId(null)
      await loadStudyDetails(currentStudy.id)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save transaction.')
    }
  }

  const handleDeleteTx = async (txId: string) => {
    if (!currentStudy) return
    if (!window.confirm('Delete this transfer pricing transaction?')) return
    try {
      await transferPricingApi.deleteTransaction(currentStudy.id, txId)
      await loadStudyDetails(currentStudy.id)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete transaction.')
    }
  }

  // Comparable saves
  const handleSaveComp = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!currentStudy) return
    try {
      if (editingCompId) {
        await transferPricingApi.updateComparable(currentStudy.id, editingCompId, compForm)
        setSuccessMsg('Comparable benchmark updated and study ranges refreshed.')
      } else {
        await transferPricingApi.createComparable(currentStudy.id, compForm)
        setSuccessMsg('Comparable benchmark added and study ranges recalculated.')
      }
      setShowCompModal(false)
      setEditingCompId(null)
      await loadStudyDetails(currentStudy.id)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to save comparable.')
    }
  }

  const handleDeleteComp = async (compId: string) => {
    if (!currentStudy) return
    if (!window.confirm('Delete this comparable benchmark?')) return
    try {
      await transferPricingApi.deleteComparable(currentStudy.id, compId)
      await loadStudyDetails(currentStudy.id)
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to delete comparable.')
    }
  }

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Page Header */}
      <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 border-b border-gray-200 pb-5">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold text-gray-900">
              Transfer Pricing Study & Form 3CEB Certification Desk
            </h1>
            <span className="px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-100 text-emerald-800 border border-emerald-200">
              Sec 92E / Rule 10D & 10E
            </span>
            {loading && <span className="text-xs text-emerald-600 animate-pulse font-medium">Syncing...</span>}
          </div>
          <p className="text-sm text-gray-500 mt-1">
            Associated Enterprises (AEs), Most Appropriate Method (MAM) benchmarking, Rule 10CA 35th–65th percentile range, and Form 3CEB reporting.
          </p>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={() => {
              handleComputeBenchmark()
              setShowBenchmarkCalcModal(true)
            }}
            className="px-3.5 py-2 text-sm font-medium text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-lg hover:bg-emerald-100 transition shadow-sm"
          >
            Range & Tolerance Calculator
          </button>
          <button
            onClick={() => {
              setEditingStudyId(null)
              if (clients.length > 0) setStudyForm((prev) => ({ ...prev, clientId: clients[0].id }))
              setShowStudyModal(true)
            }}
            className="px-4 py-2 text-sm font-medium text-white bg-emerald-600 rounded-lg hover:bg-emerald-700 transition shadow-sm"
          >
            + New Transfer Pricing Study
          </button>
        </div>
      </div>

      {/* Alerts */}
      {error && (
        <div className="p-4 bg-red-50 border border-red-200 text-red-700 rounded-lg text-sm flex items-center justify-between">
          <span>{error}</span>
          <button onClick={() => setError(null)} className="text-red-500 hover:text-red-700">&times;</button>
        </div>
      )}
      {successMsg && (
        <div className="p-4 bg-green-50 border border-green-200 text-green-700 rounded-lg text-sm flex items-center justify-between">
          <span>{successMsg}</span>
          <button onClick={() => setSuccessMsg(null)} className="text-green-500 hover:text-green-700">&times;</button>
        </div>
      )}

      {/* Top Executive KPI Cards */}
      {summary && (
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Studies</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-gray-900">{summary.totalStudies}</span>
              <span className="text-xs text-green-600 font-medium">({summary.certifiedStudies} certified)</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">Section 92E studies</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Associated Enterprises</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-emerald-700">{summary.totalAssociatedEnterprises}</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">Section 92A cross-border AEs</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">International Txs</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-gray-900">
                ₹{(summary.totalInternationalVolumeInr / 10000000).toFixed(1)}Cr
              </span>
            </div>
            <p className="text-xs text-gray-400 mt-1">{summary.totalInternationalTransactions} cross-border items</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Domestic Txs (SDT)</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-blue-700">
                ₹{(summary.totalSdtVolumeInr / 10000000).toFixed(1)}Cr
              </span>
            </div>
            <p className="text-xs text-gray-400 mt-1">{summary.totalSdtTransactions} Section 92BA items</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">TP Adjustments</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-2xl font-bold text-rose-600">
                ₹{(summary.totalAlpAdjustmentAmount / 10000000).toFixed(2)}Cr
              </span>
            </div>
            <p className="text-xs text-gray-400 mt-1">{summary.totalAlpAdjustmentsCount} deficient txs (Sec 92CA)</p>
          </div>

          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <span className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Rule 10D Local File</span>
            <div className="mt-2 flex items-baseline gap-2">
              <span className="text-xl font-bold text-green-700">Active</span>
            </div>
            <p className="text-xs text-gray-400 mt-1">Documentation maintained</p>
          </div>
        </div>
      )}

      {/* Study Selector & Overview Banner */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <label className="text-sm font-semibold text-gray-700 whitespace-nowrap">Select TP Study:</label>
            <select
              value={selectedStudyId || ''}
              onChange={(e) => setSelectedStudyId(e.target.value)}
              className="px-3 py-1.5 text-sm border border-gray-300 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 bg-gray-50 font-medium text-gray-800"
            >
              {studies.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.studyReference} - {s.clientName} (FY {s.financialYear}) [{s.studyStatus}]
                </option>
              ))}
              {studies.length === 0 && <option value="">No studies found</option>}
            </select>
          </div>

          {currentStudy && (
            <div className="flex items-center gap-2">
              <span className={`px-2.5 py-1 text-xs font-semibold rounded-full border ${currentStudy.studyStatus === 'Certified'
                ? 'bg-green-100 text-green-800 border-green-300'
                : 'bg-amber-100 text-amber-800 border-amber-300'
                }`}>
                {currentStudy.studyStatus}
              </span>
              {currentStudy.totalAlpAdjustmentAmount > 0 ? (
                <span className="px-2.5 py-1 text-xs font-semibold rounded-full bg-red-50 text-red-700 border border-red-200">
                  Adjustment: ₹{(currentStudy.totalAlpAdjustmentAmount / 100000).toFixed(2)}L
                </span>
              ) : (
                <span className="px-2.5 py-1 text-xs font-semibold rounded-full bg-green-50 text-green-700 border border-green-200">
                  Arm's Length Compliant
                </span>
              )}
              {currentStudy.studyStatus !== 'Certified' && (
                <button
                  onClick={() => {
                    setCertifyForm({ udin: currentStudy.udin || '', certifiedBy: currentStudy.accountantName || '' })
                    setShowCertifyModal(true)
                  }}
                  className="px-3 py-1.5 text-xs font-semibold text-white bg-green-600 rounded-md hover:bg-green-700 transition shadow-sm"
                >
                  Certify with UDIN
                </button>
              )}
              <button
                onClick={() => {
                  setEditingStudyId(currentStudy.id)
                  setStudyForm({
                    clientId: currentStudy.clientId,
                    studyReference: currentStudy.studyReference,
                    title: currentStudy.title,
                    financialYear: currentStudy.financialYear,
                    assessmentYear: currentStudy.assessmentYear,
                    auditPeriodStart: currentStudy.auditPeriodStart.slice(0, 10),
                    auditPeriodEnd: currentStudy.auditPeriodEnd.slice(0, 10),
                    rule10DDocumentationMaintained: currentStudy.rule10DDocumentationMaintained,
                    rule10DDocumentationPath: currentStudy.rule10DDocumentationPath,
                    accountantName: currentStudy.accountantName,
                    accountantFirmName: currentStudy.accountantFirmName,
                    membershipNumber: currentStudy.membershipNumber,
                    firmRegistrationNumber: currentStudy.firmRegistrationNumber,
                  })
                  setShowStudyModal(true)
                }}
                className="px-3 py-1.5 text-xs font-medium text-gray-700 bg-gray-100 rounded-md hover:bg-gray-200 border border-gray-300 transition"
              >
                Edit
              </button>
              <button
                onClick={() => handleDeleteStudy(currentStudy.id)}
                className="px-3 py-1.5 text-xs font-medium text-red-600 bg-red-50 rounded-md hover:bg-red-100 border border-red-200 transition"
              >
                Delete
              </button>
            </div>
          )}
        </div>

        {currentStudy && (
          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-3 text-xs bg-gray-50 p-3 rounded-lg border border-gray-200">
            <div>
              <span className="text-gray-500 font-medium">Assessee Client:</span>
              <p className="font-semibold text-gray-900 truncate">{currentStudy.clientName}</p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">Financial / Assessment Year:</span>
              <p className="font-semibold text-gray-900">FY {currentStudy.financialYear} / AY {currentStudy.assessmentYear}</p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">Associated Enterprises:</span>
              <p className="font-semibold text-gray-900">{currentStudy.totalAssociatedEnterprisesCount} entities</p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">International / SDT Volume:</span>
              <p className="font-semibold text-gray-900">
                ₹{(currentStudy.totalInternationalInrValue / 10000000).toFixed(1)}Cr / ₹{(currentStudy.totalSdtInrValue / 10000000).toFixed(1)}Cr
              </p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">Accountant / Membership:</span>
              <p className="font-semibold text-gray-900 truncate">{currentStudy.accountantName} (M: {currentStudy.membershipNumber})</p>
            </div>
            <div>
              <span className="text-gray-500 font-medium">UDIN:</span>
              <p className="font-mono font-semibold text-emerald-700 truncate">{currentStudy.udin || 'Not Certified'}</p>
            </div>
          </div>
        )}
      </div>

      {/* Sub-Entity Navigation Tabs */}
      {currentStudy && (
        <div className="space-y-4">
          <div className="border-b border-gray-200">
            <nav className="flex space-x-8">
              <button
                onClick={() => setActiveTab('transactions')}
                className={`py-3 px-1 border-b-2 text-sm font-medium transition ${activeTab === 'transactions'
                  ? 'border-emerald-600 text-emerald-600 font-semibold'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                  }`}
              >
                Transactions & Form 3CEB Clauses ({currentStudy.totalInternationalTransactionsCount + currentStudy.totalSdtTransactionsCount})
              </button>
              <button
                onClick={() => setActiveTab('enterprises')}
                className={`py-3 px-1 border-b-2 text-sm font-medium transition ${activeTab === 'enterprises'
                  ? 'border-emerald-600 text-emerald-600 font-semibold'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                  }`}
              >
                Associated Enterprises ({currentStudy.totalAssociatedEnterprisesCount})
              </button>
              <button
                onClick={() => setActiveTab('comparables')}
                className={`py-3 px-1 border-b-2 text-sm font-medium transition ${activeTab === 'comparables'
                  ? 'border-emerald-600 text-emerald-600 font-semibold'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                  }`}
              >
                Comparable Benchmarks ({comparables.length})
              </button>
              <button
                onClick={() => setActiveTab('report')}
                className={`py-3 px-1 border-b-2 text-sm font-medium transition ${activeTab === 'report'
                  ? 'border-emerald-600 text-emerald-600 font-semibold'
                  : 'border-transparent text-gray-500 hover:text-gray-700 hover:border-gray-300'
                  }`}
              >
                Form No. 3CEB Statutory Report
              </button>
            </nav>
          </div>

          {/* TAB 1: Transactions & Form 3CEB Clauses */}
          {activeTab === 'transactions' && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
              <div className="flex flex-col md:flex-row md:items-center justify-between gap-3">
                <div>
                  <h3 className="text-base font-semibold text-gray-900">International & Specified Domestic Transactions</h3>
                  <p className="text-xs text-gray-500">
                    Clauses 11 to 20 (International) and Clauses 21 to 25 (SDT) with automated arm's length evaluation against Rule 10CA benchmark range.
                  </p>
                </div>
                <button
                  onClick={() => {
                    setEditingTxId(null)
                    if (enterprises.length > 0) {
                      setTxForm((prev) => ({ ...prev, associatedEnterpriseId: enterprises[0].id }))
                    }
                    setShowTxModal(true)
                  }}
                  className="px-3.5 py-1.5 text-xs font-medium text-white bg-emerald-600 rounded-lg hover:bg-emerald-700 transition"
                >
                  + Add Transaction
                </button>
              </div>

              {/* Transactions Table */}
              <div className="overflow-x-auto border border-gray-200 rounded-lg">
                <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                  <thead className="bg-gray-50 text-gray-600 font-semibold uppercase tracking-wider">
                    <tr>
                      <th className="py-3 px-3">Clause & Category</th>
                      <th className="py-3 px-3">Associated Enterprise</th>
                      <th className="py-3 px-3">Transaction Value</th>
                      <th className="py-3 px-3">MAM & Tested Margin</th>
                      <th className="py-3 px-3">Benchmark Range / Mean</th>
                      <th className="py-3 px-3">Arm's Length Status</th>
                      <th className="py-3 px-3">Primary TP Adjustment</th>
                      <th className="py-3 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200 bg-white">
                    {transactions.map((t) => (
                      <tr key={t.id} className="hover:bg-gray-50 transition">
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <span className="font-mono text-emerald-800 bg-emerald-50 px-1.5 py-0.5 rounded font-bold border border-emerald-200">
                            Clause {t.clauseNumber}
                          </span>
                          <p className="text-[10px] text-gray-400 mt-1">{t.transactionCategory}</p>
                          <p className="text-gray-900 font-semibold text-[11px] mt-0.5 max-w-xs">{t.descriptionOfTransaction}</p>
                        </td>
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <p className="font-medium text-gray-900">{t.associatedEnterpriseName || 'N/A'}</p>
                          <p className="text-gray-400 text-[10px]">{t.associatedEnterpriseCountry || 'Overseas'}</p>
                        </td>
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <p className="font-semibold text-gray-900 font-mono">₹{t.inrValue.toLocaleString()}</p>
                          {t.currencyCode !== 'INR' && (
                            <p className="text-gray-400 text-[10px] font-mono">
                              {t.currencyCode} {t.foreignCurrencyAmount.toLocaleString()} @ ₹{t.exchangeRate}
                            </p>
                          )}
                        </td>
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <span className="font-semibold text-gray-800">{t.mostAppropriateMethod}</span>
                          <span className="text-[10px] text-gray-400 ml-1">({t.profitLevelIndicator})</span>
                          <p className="text-emerald-700 font-bold font-mono mt-0.5">{t.testedPartyMargin}%</p>
                        </td>
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <p className="font-mono text-gray-700">
                            {t.benchmarkRangeMin}% to {t.benchmarkRangeMax}%
                          </p>
                          <p className="text-[10px] text-gray-400">
                            Median/Mean: {t.benchmarkMedianOrMean}% ({t.comparablesCount} comps)
                          </p>
                        </td>
                        <td className="py-3 px-3 align-top whitespace-nowrap">
                          <span className={`px-2 py-0.5 rounded text-xs font-semibold ${t.isWithinArmLength
                            ? 'bg-green-100 text-green-800'
                            : 'bg-red-100 text-red-800 font-bold'
                            }`}>
                            {t.isWithinArmLength ? 'At Arm\'s Length' : 'Adjustment Required'}
                          </span>
                        </td>
                        <td className="py-3 px-3 align-top whitespace-nowrap font-mono">
                          {t.alpAdjustmentAmount > 0 ? (
                            <span className="text-red-600 font-bold">
                              ₹{t.alpAdjustmentAmount.toLocaleString()}
                            </span>
                          ) : (
                            <span className="text-gray-400">₹0.00</span>
                          )}
                        </td>
                        <td className="py-3 px-3 align-top text-right whitespace-nowrap">
                          <button
                            onClick={() => {
                              setEditingTxId(t.id)
                              setTxForm({
                                associatedEnterpriseId: t.associatedEnterpriseId,
                                transactionCategory: t.transactionCategory,
                                clauseNumber: t.clauseNumber,
                                descriptionOfTransaction: t.descriptionOfTransaction,
                                currencyCode: t.currencyCode,
                                foreignCurrencyAmount: t.foreignCurrencyAmount,
                                exchangeRate: t.exchangeRate,
                                mostAppropriateMethod: t.mostAppropriateMethod,
                                profitLevelIndicator: t.profitLevelIndicator,
                                testedPartyCostBase: t.testedPartyCostBase,
                                testedPartyMargin: t.testedPartyMargin,
                                toleranceBandPercentage: t.toleranceBandPercentage,
                                methodSelectionRationale: t.methodSelectionRationale,
                                documentationReference: t.documentationReference,
                              })
                              setShowTxModal(true)
                            }}
                            className="text-emerald-600 hover:text-emerald-800 font-medium mr-2"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDeleteTx(t.id)}
                            className="text-red-500 hover:text-red-700 font-medium"
                          >
                            Delete
                          </button>
                        </td>
                      </tr>
                    ))}
                    {transactions.length === 0 && (
                      <tr>
                        <td colSpan={8} className="py-6 text-center text-gray-400">
                          No transactions recorded. Click "+ Add Transaction" to document international transactions or SDT.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* TAB 2: Associated Enterprises */}
          {activeTab === 'enterprises' && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-base font-semibold text-gray-900">Associated Enterprises (Section 92A)</h3>
                  <p className="text-xs text-gray-500 mt-0.5">
                    Direct/indirect control or common management, capital, voting power ($\ge$ 26%), loans ($\ge$ 51% assets), or operational dependence.
                  </p>
                </div>
                <button
                  onClick={() => {
                    setEditingAeId(null)
                    setAeForm({
                      enterpriseName: '',
                      countryOfResidence: 'USA',
                      countryCode: 'US',
                      taxIdentificationNumber: '',
                      registeredAddress: '',
                      relationshipSection: 'Sec 92A(2)(a) - Holding >= 26% Voting Power',
                      ownershipPercentage: 100,
                      briefActivityDescription: 'Marketing and overseas distribution of parent products',
                    })
                    setShowAeModal(true)
                  }}
                  className="px-3 py-1.5 text-xs font-medium text-white bg-emerald-600 rounded-lg hover:bg-emerald-700 transition"
                >
                  + Add Associated Enterprise
                </button>
              </div>

              <div className="overflow-x-auto border border-gray-200 rounded-lg">
                <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                  <thead className="bg-gray-50 text-gray-600 font-semibold uppercase tracking-wider">
                    <tr>
                      <th className="py-3 px-3">Enterprise Name</th>
                      <th className="py-3 px-3">Country & Tax ID (TIN)</th>
                      <th className="py-3 px-3">Section 92A Basis</th>
                      <th className="py-3 px-3">Ownership %</th>
                      <th className="py-3 px-3">Business Activity</th>
                      <th className="py-3 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200 bg-white">
                    {enterprises.map((ae) => (
                      <tr key={ae.id} className="hover:bg-gray-50 transition">
                        <td className="py-3 px-3 font-semibold text-gray-900 whitespace-nowrap">
                          {ae.enterpriseName}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap">
                          <p className="text-gray-900 font-medium">{ae.countryOfResidence} ({ae.countryCode})</p>
                          <p className="text-[10px] text-gray-400 font-mono">TIN: {ae.taxIdentificationNumber || 'N/A'}</p>
                        </td>
                        <td className="py-3 px-3 text-gray-700 font-medium">
                          {ae.relationshipSection}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap font-mono font-semibold text-emerald-700">
                          {ae.ownershipPercentage}%
                        </td>
                        <td className="py-3 px-3 text-gray-500 text-xs max-w-xs truncate">
                          {ae.briefActivityDescription || '-'}
                        </td>
                        <td className="py-3 px-3 text-right whitespace-nowrap">
                          <button
                            onClick={() => {
                              setEditingAeId(ae.id)
                              setAeForm({
                                enterpriseName: ae.enterpriseName,
                                countryOfResidence: ae.countryOfResidence,
                                countryCode: ae.countryCode,
                                taxIdentificationNumber: ae.taxIdentificationNumber,
                                registeredAddress: ae.registeredAddress,
                                relationshipSection: ae.relationshipSection,
                                ownershipPercentage: ae.ownershipPercentage,
                                briefActivityDescription: ae.briefActivityDescription,
                              })
                              setShowAeModal(true)
                            }}
                            className="text-emerald-600 hover:text-emerald-800 font-medium mr-2"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDeleteAe(ae.id)}
                            className="text-red-500 hover:text-red-700 font-medium"
                          >
                            Delete
                          </button>
                        </td>
                      </tr>
                    ))}
                    {enterprises.length === 0 && (
                      <tr>
                        <td colSpan={6} className="py-6 text-center text-gray-400">
                          No Associated Enterprises added yet.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* TAB 3: Comparable Benchmarks */}
          {activeTab === 'comparables' && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
              <div className="flex items-center justify-between">
                <div>
                  <h3 className="text-base font-semibold text-gray-900">Comparable Benchmark Database</h3>
                  <p className="text-xs text-gray-500 mt-0.5">
                    Peer uncontrolled company financial data and margins used to construct Rule 10CA percentiles (35th to 65th range).
                  </p>
                </div>
                <button
                  onClick={() => {
                    setEditingCompId(null)
                    setCompForm({
                      transactionId: undefined,
                      comparableCompanyName: '',
                      cinOrIdentifier: '',
                      financialYear: currentStudy.financialYear,
                      operatingRevenue: 100000000,
                      operatingCost: 85000000,
                      operatingProfit: 15000000,
                      weight: 1.0,
                      isAccepted: true,
                      rejectionReason: '',
                    })
                    setShowCompModal(true)
                  }}
                  className="px-3 py-1.5 text-xs font-medium text-white bg-emerald-600 rounded-lg hover:bg-emerald-700 transition"
                >
                  + Add Comparable Company
                </button>
              </div>

              <div className="overflow-x-auto border border-gray-200 rounded-lg">
                <table className="min-w-full divide-y divide-gray-200 text-left text-xs">
                  <thead className="bg-gray-50 text-gray-600 font-semibold uppercase tracking-wider">
                    <tr>
                      <th className="py-3 px-3">Company Name & CIN</th>
                      <th className="py-3 px-3">Operating Revenue</th>
                      <th className="py-3 px-3">Operating Cost</th>
                      <th className="py-3 px-3">Operating Profit</th>
                      <th className="py-3 px-3">Margin % (OP/TC)</th>
                      <th className="py-3 px-3">Status</th>
                      <th className="py-3 px-3 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-200 bg-white">
                    {comparables.map((c) => (
                      <tr key={c.id} className="hover:bg-gray-50 transition">
                        <td className="py-3 px-3 font-semibold text-gray-900 whitespace-nowrap">
                          {c.comparableCompanyName}
                          <p className="text-[10px] text-gray-400 font-mono font-normal">{c.cinOrIdentifier || 'Unlisted'}</p>
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap font-mono text-gray-700">
                          ₹{c.operatingRevenue.toLocaleString()}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap font-mono text-gray-700">
                          ₹{c.operatingCost.toLocaleString()}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap font-mono text-gray-700">
                          ₹{c.operatingProfit.toLocaleString()}
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap font-mono font-bold text-emerald-700">
                          {c.calculatedMarginPercentage}%
                        </td>
                        <td className="py-3 px-3 whitespace-nowrap">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${c.isAccepted ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
                            }`}>
                            {c.isAccepted ? 'Accepted' : 'Rejected'}
                          </span>
                          {!c.isAccepted && c.rejectionReason && (
                            <p className="text-[10px] text-red-600 truncate max-w-xs">{c.rejectionReason}</p>
                          )}
                        </td>
                        <td className="py-3 px-3 text-right whitespace-nowrap">
                          <button
                            onClick={() => {
                              setEditingCompId(c.id)
                              setCompForm({
                                transactionId: c.transactionId,
                                comparableCompanyName: c.comparableCompanyName,
                                cinOrIdentifier: c.cinOrIdentifier,
                                financialYear: c.financialYear,
                                operatingRevenue: c.operatingRevenue,
                                operatingCost: c.operatingCost,
                                operatingProfit: c.operatingProfit,
                                weight: c.weight,
                                isAccepted: c.isAccepted,
                                rejectionReason: c.rejectionReason || '',
                              })
                              setShowCompModal(true)
                            }}
                            className="text-emerald-600 hover:text-emerald-800 font-medium mr-2"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDeleteComp(c.id)}
                            className="text-red-500 hover:text-red-700 font-medium"
                          >
                            Delete
                          </button>
                        </td>
                      </tr>
                    ))}
                    {comparables.length === 0 && (
                      <tr>
                        <td colSpan={7} className="py-6 text-center text-gray-400">
                          No comparable entities added yet.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* TAB 4: Form No. 3CEB Statutory Report */}
          {activeTab === 'report' && (
            <div className="bg-white rounded-xl border border-gray-200 shadow-sm p-5 space-y-4">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 border-b border-gray-200 pb-3">
                <div>
                  <h3 className="text-base font-semibold text-gray-900">Form No. 3CEB Accountant's Report</h3>
                  <p className="text-xs text-gray-500">
                    Statutory report under Section 92E and Rule 10E relating to international and specified domestic transactions.
                  </p>
                </div>
                <div className="flex items-center gap-2">
                  <button
                    onClick={handleRegenerateReport}
                    className="px-3 py-1.5 text-xs font-medium text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-md hover:bg-emerald-100 transition"
                  >
                    Regenerate Text
                  </button>
                  <button
                    onClick={() => {
                      if (currentStudy.form3CebReportText) {
                        navigator.clipboard.writeText(currentStudy.form3CebReportText)
                        setSuccessMsg('Form 3CEB report text copied to clipboard.')
                      }
                    }}
                    className="px-3 py-1.5 text-xs font-medium text-gray-700 bg-gray-100 border border-gray-300 rounded-md hover:bg-gray-200 transition"
                  >
                    Copy Text
                  </button>
                  <button
                    onClick={() => window.print()}
                    className="px-3 py-1.5 text-xs font-medium text-white bg-emerald-600 rounded-md hover:bg-emerald-700 transition"
                  >
                    Print Report
                  </button>
                </div>
              </div>

              {/* Monospaced Form 3CEB Viewer */}
              <div className="p-4 bg-gray-900 text-gray-100 rounded-lg font-mono text-xs overflow-x-auto whitespace-pre leading-relaxed shadow-inner border border-gray-800">
                {currentStudy.form3CebReportText || 'Form 3CEB report text pending generation.'}
              </div>
            </div>
          )}
        </div>
      )}

      {/* MODAL 1: Rule 10CA Range & Tolerance Calculator */}
      {showBenchmarkCalcModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Rule 10CA Range & Tolerance Band Calculator</h3>
              <button onClick={() => setShowBenchmarkCalcModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <div className="space-y-3 text-xs">
              <div>
                <label className="font-semibold text-gray-700">Comparable Margins (Comma Separated %)</label>
                <textarea
                  value={calcInputMargins}
                  onChange={(e) => setCalcInputMargins(e.target.value)}
                  rows={3}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm font-mono"
                  placeholder="e.g. 10.5, 12.0, 14.5, 16.0, 18.2, 21.0"
                />
                <span className="text-[11px] text-gray-400">If &ge; 6 comparables, 35th–65th percentile range applies. If &lt; 6, arithmetic mean applies.</span>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Tolerance Band Percentage (%)</label>
                <select
                  value={calcToleranceBand}
                  onChange={(e) => setCalcToleranceBand(parseFloat(e.target.value))}
                  className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                >
                  <option value={3.0}>3.0% (General Services / Manufacturing)</option>
                  <option value={1.0}>1.0% (Wholesale Trading)</option>
                </select>
              </div>

              <button
                type="button"
                onClick={handleComputeBenchmark}
                className="w-full py-2 bg-emerald-600 text-white font-semibold rounded-lg hover:bg-emerald-700 transition"
              >
                Calculate Arm's Length Range
              </button>

              {calcResult && (
                <div className="p-4 rounded-lg border text-xs mt-3 bg-emerald-50 border-emerald-200 text-emerald-950 space-y-2">
                  <div className="flex items-center justify-between font-bold text-sm">
                    <span>{calcResult.useRange ? 'RULE 10CA RANGE APPLICABLE' : 'ARITHMETIC MEAN APPLICABLE'}</span>
                    <span className="px-2 py-0.5 rounded text-xs bg-emerald-200 text-emerald-800">
                      {calcResult.dataCount} Data Points
                    </span>
                  </div>
                  <p className="text-gray-600 font-medium">{calcResult.explanation}</p>
                  <div className="grid grid-cols-3 gap-2 bg-white p-2.5 rounded border border-emerald-200 text-center">
                    <div>
                      <span className="text-[10px] text-gray-400 block uppercase">Min / 35th Pct</span>
                      <span className="font-mono font-bold text-gray-900 text-sm">{calcResult.rangeMin}%</span>
                    </div>
                    <div>
                      <span className="text-[10px] text-gray-400 block uppercase">Median / Mean</span>
                      <span className="font-mono font-bold text-emerald-700 text-sm">{calcResult.medianOrMean}%</span>
                    </div>
                    <div>
                      <span className="text-[10px] text-gray-400 block uppercase">Max / 65th Pct</span>
                      <span className="font-mono font-bold text-gray-900 text-sm">{calcResult.rangeMax}%</span>
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      )}

      {/* MODAL 2: New / Edit Study */}
      {showStudyModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-2xl w-full p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">
                {editingStudyId ? 'Edit Transfer Pricing Study' : 'Create Transfer Pricing Study & Form 3CEB'}
              </h3>
              <button onClick={() => setShowStudyModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleSaveStudy} className="space-y-4 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Client / Assessee</label>
                  <select
                    value={studyForm.clientId}
                    onChange={(e) => setStudyForm({ ...studyForm, clientId: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="">Select Assessee...</option>
                    {clients.map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Study Reference</label>
                  <input
                    type="text"
                    value={studyForm.studyReference}
                    onChange={(e) => setStudyForm({ ...studyForm, studyReference: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Title of TP Documentation</label>
                <input
                  type="text"
                  value={studyForm.title}
                  onChange={(e) => setStudyForm({ ...studyForm, title: e.target.value })}
                  required
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div className="grid grid-cols-4 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Financial Year</label>
                  <input
                    type="text"
                    value={studyForm.financialYear}
                    onChange={(e) => setStudyForm({ ...studyForm, financialYear: e.target.value })}
                    required
                    placeholder="2024-25"
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Assessment Year</label>
                  <input
                    type="text"
                    value={studyForm.assessmentYear}
                    onChange={(e) => setStudyForm({ ...studyForm, assessmentYear: e.target.value })}
                    required
                    placeholder="2025-26"
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Period Start</label>
                  <input
                    type="date"
                    value={studyForm.auditPeriodStart}
                    onChange={(e) => setStudyForm({ ...studyForm, auditPeriodStart: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Period End</label>
                  <input
                    type="date"
                    value={studyForm.auditPeriodEnd}
                    onChange={(e) => setStudyForm({ ...studyForm, auditPeriodEnd: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="flex items-center gap-2 pt-1">
                <input
                  type="checkbox"
                  id="rule10dCheck"
                  checked={studyForm.rule10DDocumentationMaintained}
                  onChange={(e) => setStudyForm({ ...studyForm, rule10DDocumentationMaintained: e.target.checked })}
                  className="h-4 w-4 text-emerald-600 rounded"
                />
                <label htmlFor="rule10dCheck" className="font-semibold text-gray-700 cursor-pointer">
                  Mandatory Rule 10D Transfer Pricing Documentation (Local File) Maintained
                </label>
              </div>

              <div className="border-t pt-3">
                <span className="font-bold text-gray-800 text-sm">Accountant / Signatory Details</span>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Accountant Name (Chartered Accountant)</label>
                  <input
                    type="text"
                    value={studyForm.accountantName}
                    onChange={(e) => setStudyForm({ ...studyForm, accountantName: e.target.value })}
                    required
                    placeholder="CA Amit Kapur"
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Accountant Firm Name</label>
                  <input
                    type="text"
                    value={studyForm.accountantFirmName}
                    onChange={(e) => setStudyForm({ ...studyForm, accountantFirmName: e.target.value })}
                    required
                    placeholder="Kapur & Associates LLP"
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Membership Number</label>
                  <input
                    type="text"
                    value={studyForm.membershipNumber}
                    onChange={(e) => setStudyForm({ ...studyForm, membershipNumber: e.target.value })}
                    placeholder="098765"
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Firm Registration Number (FRN)</label>
                  <input
                    type="text"
                    value={studyForm.firmRegistrationNumber}
                    onChange={(e) => setStudyForm({ ...studyForm, firmRegistrationNumber: e.target.value })}
                    placeholder="101234W"
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowStudyModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 font-semibold"
                >
                  {editingStudyId ? 'Save Changes' : 'Create Study'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: Certify with UDIN */}
      {showCertifyModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-md w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Certify Form 3CEB Report</h3>
              <button onClick={() => setShowCertifyModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleCertify} className="space-y-4 text-xs">
              <div>
                <label className="font-semibold text-gray-700">Unique Document Identification Number (UDIN)</label>
                <input
                  type="text"
                  value={certifyForm.udin}
                  onChange={(e) => setCertifyForm({ ...certifyForm, udin: e.target.value.toUpperCase() })}
                  required
                  placeholder="e.g. 25098765B000123456"
                  className="mt-1 w-full px-3 py-2 border rounded-lg font-mono text-sm uppercase"
                />
                <span className="text-[11px] text-gray-400">Must be exactly 18 alphanumeric characters issued by ICAI.</span>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Certified By (Signing Partner)</label>
                <input
                  type="text"
                  value={certifyForm.certifiedBy}
                  onChange={(e) => setCertifyForm({ ...certifyForm, certifiedBy: e.target.value })}
                  required
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  placeholder="CA Amit Kapur, Partner"
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={certifyForm.udin.trim().length < 18}
                  className="px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 font-semibold disabled:bg-gray-300"
                >
                  Confirm Certification
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: Associated Enterprise Add/Edit */}
      {showAeModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">
                {editingAeId ? 'Edit Associated Enterprise' : 'Add Associated Enterprise'}
              </h3>
              <button onClick={() => setShowAeModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleSaveAe} className="space-y-3 text-xs">
              <div>
                <label className="font-semibold text-gray-700">Enterprise Name</label>
                <input
                  type="text"
                  value={aeForm.enterpriseName}
                  onChange={(e) => setAeForm({ ...aeForm, enterpriseName: e.target.value })}
                  required
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Country of Residence</label>
                  <input
                    type="text"
                    value={aeForm.countryOfResidence}
                    onChange={(e) => setAeForm({ ...aeForm, countryOfResidence: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Country Code (ISO-2)</label>
                  <input
                    type="text"
                    value={aeForm.countryCode}
                    onChange={(e) => setAeForm({ ...aeForm, countryCode: e.target.value.toUpperCase() })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm uppercase"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Tax ID Number (TIN / EIN)</label>
                  <input
                    type="text"
                    value={aeForm.taxIdentificationNumber}
                    onChange={(e) => setAeForm({ ...aeForm, taxIdentificationNumber: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm font-mono"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Ownership %</label>
                  <input
                    type="number"
                    value={aeForm.ownershipPercentage}
                    onChange={(e) => setAeForm({ ...aeForm, ownershipPercentage: parseFloat(e.target.value) || 0 })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Section 92A Relationship Criterion</label>
                <select
                  value={aeForm.relationshipSection}
                  onChange={(e) => setAeForm({ ...aeForm, relationshipSection: e.target.value })}
                  className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                >
                  <option value="Sec 92A(2)(a) - Holding >= 26% Voting Power">Sec 92A(2)(a) - Holding &ge; 26% Voting Power</option>
                  <option value="Sec 92A(2)(b) - Mutual Holding >= 26% Voting Power">Sec 92A(2)(b) - Mutual Holding &ge; 26% Voting Power</option>
                  <option value="Sec 92A(2)(c) - Loan >= 51% of Total Assets">Sec 92A(2)(c) - Loan &ge; 51% of Total Assets</option>
                  <option value="Sec 92A(2)(d) - Guarantee >= 10% of Total Debt">Sec 92A(2)(d) - Guarantee &ge; 10% of Total Debt</option>
                  <option value="Sec 92A(2)(e) - Board Appointment Control">Sec 92A(2)(e) - Board Appointment Control</option>
                  <option value="Sec 92A(2)(g) - 100% Dependent on Know-how/IP">Sec 92A(2)(g) - 100% Dependent on Know-how/IP</option>
                  <option value="Sec 92A(2)(h) - Raw Materials Supply Control >= 90%">Sec 92A(2)(h) - Raw Materials Supply Control &ge; 90%</option>
                </select>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Brief Activity Description</label>
                <textarea
                  value={aeForm.briefActivityDescription}
                  onChange={(e) => setAeForm({ ...aeForm, briefActivityDescription: e.target.value })}
                  rows={2}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowAeModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 font-semibold"
                >
                  Save Enterprise
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 5: Transaction Add/Edit */}
      {showTxModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-xl w-full p-6 space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">
                {editingTxId ? 'Edit Transaction' : 'Add Transfer Pricing Transaction'}
              </h3>
              <button onClick={() => setShowTxModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleSaveTx} className="space-y-3 text-xs">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Associated Enterprise</label>
                  <select
                    value={txForm.associatedEnterpriseId || ''}
                    onChange={(e) => setTxForm({ ...txForm, associatedEnterpriseId: e.target.value || undefined })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="">Select AE...</option>
                    {enterprises.map((a) => (
                      <option key={a.id} value={a.id}>{a.enterpriseName} ({a.countryCode})</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Form 3CEB Clause Number</label>
                  <select
                    value={txForm.clauseNumber}
                    onChange={(e) => setTxForm({ ...txForm, clauseNumber: parseInt(e.target.value) || 11 })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm font-semibold"
                  >
                    <option value={11}>Clause 11: Tangible Property (Raw Materials/Capital Goods)</option>
                    <option value={12}>Clause 12: Intangible Property (Patents/IP/Software)</option>
                    <option value={13}>Clause 13: Provision of Services (IT/ITeS/Management)</option>
                    <option value={14}>Clause 14: Lending or Borrowing Money (Loans/ECB)</option>
                    <option value={15}>Clause 15: Cost Allocation / Sharing</option>
                    <option value={17}>Clause 17: Guarantees (Corporate/Financial)</option>
                    <option value={18}>Clause 18: Marketable Securities & Share Allotments</option>
                    <option value={21}>Clause 21: Specified Domestic Transaction (Sec 80A/80-IA)</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Description of Transaction</label>
                <input
                  type="text"
                  value={txForm.descriptionOfTransaction}
                  onChange={(e) => setTxForm({ ...txForm, descriptionOfTransaction: e.target.value })}
                  required
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Currency</label>
                  <select
                    value={txForm.currencyCode}
                    onChange={(e) => setTxForm({ ...txForm, currencyCode: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="USD">USD ($)</option>
                    <option value="EUR">EUR (€)</option>
                    <option value="GBP">GBP (£)</option>
                    <option value="SGD">SGD (S$)</option>
                    <option value="JPY">JPY (¥)</option>
                    <option value="INR">INR (₹)</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Foreign Currency Value</label>
                  <input
                    type="number"
                    value={txForm.foreignCurrencyAmount}
                    onChange={(e) => setTxForm({ ...txForm, foreignCurrencyAmount: parseFloat(e.target.value) || 0 })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm font-mono"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Exchange Rate (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={txForm.exchangeRate}
                    onChange={(e) => setTxForm({ ...txForm, exchangeRate: parseFloat(e.target.value) || 1.0 })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm font-mono"
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Most Appropriate Method (MAM)</label>
                  <select
                    value={txForm.mostAppropriateMethod}
                    onChange={(e) => setTxForm({ ...txForm, mostAppropriateMethod: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm font-semibold"
                  >
                    <option value="TNMM">TNMM (Transactional Net Margin Method)</option>
                    <option value="CUP">CUP (Comparable Uncontrolled Price)</option>
                    <option value="RPM">RPM (Resale Price Method)</option>
                    <option value="CPM">CPM (Cost Plus Method)</option>
                    <option value="PSM">PSM (Profit Split Method)</option>
                    <option value="OtherMethod">Other Method (Rule 10AB)</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Profit Level Indicator (PLI)</label>
                  <select
                    value={txForm.profitLevelIndicator}
                    onChange={(e) => setTxForm({ ...txForm, profitLevelIndicator: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg bg-gray-50 text-sm"
                  >
                    <option value="OP_TC">OP / Total Cost (Mark-up)</option>
                    <option value="OP_OR">OP / Operating Revenue</option>
                    <option value="GP_Sales">GP / Sales (RPM)</option>
                    <option value="GP_Cost">GP / Cost (CPM)</option>
                    <option value="UnitPrice">Unit Price (CUP)</option>
                  </select>
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Tolerance Band %</label>
                  <input
                    type="number"
                    step="0.1"
                    value={txForm.toleranceBandPercentage}
                    onChange={(e) => setTxForm({ ...txForm, toleranceBandPercentage: parseFloat(e.target.value) || 3.0 })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Tested Party Cost Base (₹)</label>
                  <input
                    type="number"
                    value={txForm.testedPartyCostBase}
                    onChange={(e) => setTxForm({ ...txForm, testedPartyCostBase: parseFloat(e.target.value) || 0 })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm font-mono"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Tested Party Margin %</label>
                  <input
                    type="number"
                    step="0.01"
                    value={txForm.testedPartyMargin}
                    onChange={(e) => setTxForm({ ...txForm, testedPartyMargin: parseFloat(e.target.value) || 0 })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm font-mono"
                  />
                </div>
              </div>

              <div>
                <label className="font-semibold text-gray-700">Rationale for MAM Selection</label>
                <textarea
                  value={txForm.methodSelectionRationale}
                  onChange={(e) => setTxForm({ ...txForm, methodSelectionRationale: e.target.value })}
                  rows={2}
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowTxModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 font-semibold"
                >
                  Save Transaction
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 6: Comparable Benchmark Add/Edit */}
      {showCompModal && (
        <div className="fixed inset-0 bg-black bg-opacity-40 flex items-center justify-center p-4 z-50">
          <div className="bg-white rounded-xl shadow-xl max-w-lg w-full p-6 space-y-4">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">
                {editingCompId ? 'Edit Comparable Entity' : 'Add Comparable Benchmark'}
              </h3>
              <button onClick={() => setShowCompModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            <form onSubmit={handleSaveComp} className="space-y-3 text-xs">
              <div>
                <label className="font-semibold text-gray-700">Comparable Company Name</label>
                <input
                  type="text"
                  value={compForm.comparableCompanyName}
                  onChange={(e) => setCompForm({ ...compForm, comparableCompanyName: e.target.value })}
                  required
                  className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                />
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">CIN or Company Identifier</label>
                  <input
                    type="text"
                    value={compForm.cinOrIdentifier}
                    onChange={(e) => setCompForm({ ...compForm, cinOrIdentifier: e.target.value })}
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm font-mono"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Financial Year</label>
                  <input
                    type="text"
                    value={compForm.financialYear}
                    onChange={(e) => setCompForm({ ...compForm, financialYear: e.target.value })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="font-semibold text-gray-700">Operating Revenue (₹)</label>
                  <input
                    type="number"
                    value={compForm.operatingRevenue}
                    onChange={(e) => setCompForm({ ...compForm, operatingRevenue: parseFloat(e.target.value) || 0 })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Operating Cost (₹)</label>
                  <input
                    type="number"
                    value={compForm.operatingCost}
                    onChange={(e) => setCompForm({ ...compForm, operatingCost: parseFloat(e.target.value) || 0 })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
                <div>
                  <label className="font-semibold text-gray-700">Operating Profit (₹)</label>
                  <input
                    type="number"
                    value={compForm.operatingProfit}
                    onChange={(e) => setCompForm({ ...compForm, operatingProfit: parseFloat(e.target.value) || 0 })}
                    required
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm"
                  />
                </div>
              </div>

              <div className="flex items-center gap-2 pt-1">
                <input
                  type="checkbox"
                  id="isAcceptedCheck"
                  checked={compForm.isAccepted}
                  onChange={(e) => setCompForm({ ...compForm, isAccepted: e.target.checked })}
                  className="h-4 w-4 text-emerald-600 rounded"
                />
                <label htmlFor="isAcceptedCheck" className="font-semibold text-gray-700 cursor-pointer">
                  Accept into Benchmark Dataset (Qualitative & Quantitative filters passed)
                </label>
              </div>

              {!compForm.isAccepted && (
                <div>
                  <label className="font-semibold text-gray-700">Rejection Reason</label>
                  <input
                    type="text"
                    value={compForm.rejectionReason}
                    onChange={(e) => setCompForm({ ...compForm, rejectionReason: e.target.value })}
                    placeholder="e.g. Significant related party transactions > 25%"
                    className="mt-1 w-full px-3 py-2 border rounded-lg text-sm text-red-600"
                  />
                </div>
              )}

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCompModal(false)}
                  className="px-4 py-2 border rounded-lg text-gray-700 hover:bg-gray-100"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 text-white rounded-lg hover:bg-emerald-700 font-semibold"
                >
                  Save Comparable
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
