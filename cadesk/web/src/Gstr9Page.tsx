import React, { useState, useEffect } from 'react'
import { toError,
  gstr9Api,
  api,
  type Gstr9ReturnDto,
  type Gstr9cReconciliationDto,
  type Gstr9SummaryDto,
  type Client,
  type ClientPage,
  type CompileGstr9ReturnRequest,
  type ReconcileGstr9cRequest,
  type AddDiscrepancyRequest,
  type CertifyGstr9cRequest,
  type RecordDrc03PaymentRequest
} from './api'

export const Gstr9Page: React.FC = () => {
  const [returns, setReturns] = useState<Gstr9ReturnDto[]>([])
  const [summary, setSummary] = useState<Gstr9SummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState<boolean>(true)
  const [error, setError] = useState<string | null>(null)

  // Filters
  const [selectedFy, setSelectedFy] = useState<string>('2023-24')
  const [selectedClientId, setSelectedClientId] = useState<string>('')

  // Modals & Active Viewers
  const [showCompileModal, setShowCompileModal] = useState<boolean>(false)
  const [viewingReturn, setViewingReturn] = useState<Gstr9ReturnDto | null>(null)
  const [activeGstr9Tab, setActiveGstr9Tab] = useState<'outward' | 'itc' | 'tax'>('outward')

  const [activeGstr9c, setActiveGstr9c] = useState<Gstr9cReconciliationDto | null>(null)
  const [showGstr9cModal, setShowGstr9cModal] = useState<boolean>(false)
  const [showAddDiscrepancyModal, setShowAddDiscrepancyModal] = useState<boolean>(false)
  const [showCertifyModal, setShowCertifyModal] = useState<boolean>(false)
  const [showDrc03Modal, setShowDrc03Modal] = useState<boolean>(false)
  const [showFileModal, setShowFileModal] = useState<boolean>(false)
  const [fileArn, setFileArn] = useState<string>('')
  const [fileNotes, setFileNotes] = useState<string>('')

  // JSON Import State
  const [showJsonImportModal, setShowJsonImportModal] = useState<boolean>(false)
  const [targetGstr9Id, setTargetGstr9Id] = useState<string>('')
  const [gstr1Payload, setGstr1Payload] = useState<string>('')
  const [gstr2bPayload, setGstr2bPayload] = useState<string>('')
  const [importingJson, setImportingJson] = useState<boolean>(false)

  // Compile Form State
  const [compileForm, setCompileForm] = useState<CompileGstr9ReturnRequest>({
    clientId: '',
    financialYear: '2023-24',
    aggregateTurnover: 0,
    outwardTaxableSupplies: 0,
    outwardExemptedSupplies: 0,
    rcmInwardSupplies: 0,
    itcAvailed3b: 0,
    itcCategorizedB2b: 0,
    itcReversed: 0,
    itcAsPer2a: 0,
    itcSubsequentFy: 0,
    totalTaxPayable: 0,
    taxPaidCash: 0,
    taxPaidItc: 0,
    previousFyAmendmentsTax: 0,
    notes: ''
  })

  // Reconcile Form State
  const [reconcileForm, setReconcileForm] = useState<ReconcileGstr9cRequest>({
    auditedGrossTurnover: 0,
    unbilledRevenueStart: 0,
    unadjustedAdvancesEnd: 0,
    deemedSupplies: 0,
    creditNotesAfterFy: 0,
    tradeDiscountsDisallowed: 0,
    unbilledRevenueEnd: 0,
    unadjustedAdvancesStart: 0,
    otherTurnoverAdjustments: 0,
    exemptedNilTurnover: 0,
    zeroRatedWopTurnover: 0,
    rcmOutwardTurnover: 0,
    additionalTaxPayable: 0,
    additionalInterestPayable: 0,
    additionalPenaltyPayable: 0,
    bookItcTotal: 0,
    auditorRemarks: ''
  })

  // Discrepancy Form State
  const [discrepancyForm, setDiscrepancyForm] = useState<AddDiscrepancyRequest>({
    category: 'TurnoverTimingDifference',
    tableRef: 'Table5',
    description: '',
    discrepancyAmount: 0,
    taxImpact: 0,
    auditorRecommendation: ''
  })

  // Certify Form State
  const [certifyForm, setCertifyForm] = useState<CertifyGstr9cRequest>({
    auditorName: '',
    auditorMembershipNo: '',
    auditorFirmRegNo: '',
    udin: '',
    auditorRemarks: ''
  })

  // DRC-03 Form State
  const [drc03Form, setDrc03Form] = useState<RecordDrc03PaymentRequest>({
    drc03Arn: '',
    drc03Date: new Date().toISOString().split('T')[0],
    drc03PaidAmount: 0,
    drc03PaymentMode: 'CashLedger'
  })

  useEffect(() => {
    loadClients()
  }, [])

  useEffect(() => {
    loadData()
  }, [selectedFy, selectedClientId])

  const loadClients = async () => {
    try {
      const data = await api<ClientPage>('/api/clients?take=200')
      setClients(data.items || [])
    } catch (caught) {
      const err = toError(caught)
      console.error('Failed to load clients:', err)
    }
  }

  const loadData = async () => {
    setLoading(true)
    setError(null)
    try {
      const [returnsData, summaryData] = await Promise.all([
        gstr9Api.getReturns(selectedClientId || undefined, selectedFy || undefined),
        gstr9Api.getSummary(selectedFy || undefined)
      ])
      setReturns(returnsData)
      setSummary(summaryData)
    } catch (caught) {
      const err = toError(caught)
      setError(err?.message || 'Failed to load GSTR-9 returns.')
    } finally {
      setLoading(false)
    }
  }

  const handleOpenCompileModal = () => {
    setCompileForm({
      clientId: clients[0]?.id || '',
      financialYear: selectedFy || '2023-24',
      aggregateTurnover: 0,
      outwardTaxableSupplies: 0,
      outwardExemptedSupplies: 0,
      rcmInwardSupplies: 0,
      itcAvailed3b: 0,
      itcCategorizedB2b: 0,
      itcReversed: 0,
      itcAsPer2a: 0,
      itcSubsequentFy: 0,
      totalTaxPayable: 0,
      taxPaidCash: 0,
      taxPaidItc: 0,
      previousFyAmendmentsTax: 0,
      notes: ''
    })
    setShowCompileModal(true)
  }

  const handleCompileSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!compileForm.clientId) {
      alert('Please select a client.')
      return
    }
    try {
      await gstr9Api.compileReturn(compileForm)
      setShowCompileModal(false)
      loadData()
    } catch (caught) {
      const err = toError(caught)
      alert(`Error compiling GSTR-9: ${err.message}`)
    }
  }

  const handleViewGstr9Tables = async (ret: Gstr9ReturnDto) => {
    try {
      const full = await gstr9Api.getReturnById(ret.id)
      setViewingReturn(full)
      // fetch entries if any
      setActiveGstr9Tab('outward')
    } catch (caught) {
      const err = toError(caught)
      alert(`Failed to load return details: ${err.message}`)
    }
  }

  const handleOpenGstr9cDesk = async (ret: Gstr9ReturnDto) => {
    try {
      setViewingReturn(ret)
      const rec = await gstr9Api.getGstr9c(ret.id)
      if (rec) {
        setActiveGstr9c(rec)
        setReconcileForm({
          auditedGrossTurnover: rec.auditedGrossTurnover,
          unbilledRevenueStart: rec.unbilledRevenueStart,
          unadjustedAdvancesEnd: rec.unadjustedAdvancesEnd,
          deemedSupplies: rec.deemedSupplies,
          creditNotesAfterFy: rec.creditNotesAfterFy,
          tradeDiscountsDisallowed: rec.tradeDiscountsDisallowed,
          unbilledRevenueEnd: rec.unbilledRevenueEnd,
          unadjustedAdvancesStart: rec.unadjustedAdvancesStart,
          otherTurnoverAdjustments: rec.otherTurnoverAdjustments,
          exemptedNilTurnover: rec.exemptedNilTurnover,
          zeroRatedWopTurnover: rec.zeroRatedWopTurnover,
          rcmOutwardTurnover: rec.rcmOutwardTurnover,
          additionalTaxPayable: rec.additionalTaxPayable,
          additionalInterestPayable: rec.additionalInterestPayable,
          additionalPenaltyPayable: rec.additionalPenaltyPayable,
          bookItcTotal: rec.bookItcTotal,
          auditorRemarks: rec.auditorRemarks || ''
        })
        setShowGstr9cModal(true)
      } else {
        alert('GSTR-9C statement is not yet initialized for this return.')
      }
    } catch (caught) {
      const err = toError(caught)
      alert(`Failed to open GSTR-9C: ${err.message}`)
    }
  }

  const handleReconcileSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!viewingReturn || !activeGstr9c) return
    try {
      const updated = await gstr9Api.reconcileGstr9c(viewingReturn.id, reconcileForm)
      setActiveGstr9c(updated)
      alert('GSTR-9C reconciliation calculations saved.')
      loadData()
    } catch (caught) {
      const err = toError(caught)
      alert(`Error reconciling GSTR-9C: ${err.message}`)
    }
  }

  const handleAddDiscrepancySubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!viewingReturn) return
    try {
      await gstr9Api.addDiscrepancy(viewingReturn.id, discrepancyForm)
      setShowAddDiscrepancyModal(false)
      const rec = await gstr9Api.getGstr9c(viewingReturn.id)
      if (rec) setActiveGstr9c(rec)
      setDiscrepancyForm({
        category: 'TurnoverTimingDifference',
        tableRef: 'Table5',
        description: '',
        discrepancyAmount: 0,
        taxImpact: 0,
        auditorRecommendation: ''
      })
    } catch (caught) {
      const err = toError(caught)
      alert(`Error adding discrepancy: ${err.message}`)
    }
  }

  const handleResolveDiscrepancy = async (id: string) => {
    const notes = prompt('Enter resolution notes:')
    if (!notes) return
    try {
      await gstr9Api.resolveDiscrepancy(id, { resolutionNotes: notes })
      if (viewingReturn) {
        const rec = await gstr9Api.getGstr9c(viewingReturn.id)
        if (rec) setActiveGstr9c(rec)
      }
    } catch (caught) {
      const err = toError(caught)
      alert(`Error resolving discrepancy: ${err.message}`)
    }
  }

  const handleImportGstJson = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!targetGstr9Id) {
      alert('Please select a target GSTR-9 annual return.')
      return
    }
    if (!gstr1Payload.trim() && !gstr2bPayload.trim()) {
      alert('Please provide at least GSTR-1 or GSTR-2B JSON.')
      return
    }
    try {
      setImportingJson(true)
      const targetReturn = returns.find(r => r.id === targetGstr9Id)
      const res = await gstr9Api.importGstJson({
        clientId: targetReturn?.clientId || (selectedClientId !== 'all' ? selectedClientId : undefined),
        financialYear: targetReturn?.financialYear || selectedFy,
        gstr9Id: targetGstr9Id,
        gstr1Json: gstr1Payload.trim() || undefined,
        gstr2bJson: gstr2bPayload.trim() || undefined
      })
      alert(`GST JSON successfully imported! Aggregate Turnover: ${formatCurrency(res.aggregateTurnover)}, Eligible ITC: ${formatCurrency(res.itcAsPer2a)}.`)
      setShowJsonImportModal(false)
      setGstr1Payload('')
      setGstr2bPayload('')
      await loadData()
      setViewingReturn(res)
    } catch (caught) {
      const err = toError(caught)
      alert(`Error importing GST JSON: ${err.message}`)
    } finally {
      setImportingJson(false)
    }
  }

  const handleCertifySubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!viewingReturn) return
    try {
      const certified = await gstr9Api.certifyGstr9c(viewingReturn.id, certifyForm)
      setActiveGstr9c(certified)
      setShowCertifyModal(false)
      alert('GSTR-9C has been successfully certified!')
      loadData()
    } catch (caught) {
      const err = toError(caught)
      alert(`Certification failed: ${err.message}`)
    }
  }

  const handleDrc03Submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!viewingReturn) return
    try {
      const updated = await gstr9Api.recordDrc03(viewingReturn.id, drc03Form)
      setActiveGstr9c(updated)
      setShowDrc03Modal(false)
      alert('DRC-03 payment recorded successfully!')
      loadData()
    } catch (caught) {
      const err = toError(caught)
      alert(`Failed to record DRC-03: ${err.message}`)
    }
  }

  const handleFileSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!viewingReturn) return
    try {
      await gstr9Api.fileReturn(viewingReturn.id, { arn: fileArn, notes: fileNotes })
      setShowFileModal(false)
      setViewingReturn(null)
      alert('GSTR-9 return marked as filed!')
      loadData()
    } catch (caught) {
      const err = toError(caught)
      alert(`Filing failed: ${err.message}`)
    }
  }

  const formatCurrency = (val: number) => {
    return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(val || 0)
  }

  return (
    <div className="p-6 max-w-7xl mx-auto space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 border-b pb-4">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">
            GSTR-9 & GSTR-9C Annual Return & Audit Reconciliation Desk
          </h1>
          <p className="text-sm text-gray-500 mt-1">
            Statutory annual return compilation (Sec 44), turnover threshold gating (₹2 Cr / ₹5 Cr), dual gross & taxable reconciliation, discrepancy logs, and DRC-03 voluntary payment tracking.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <button
            onClick={() => {
              if (returns.length > 0 && !targetGstr9Id) {
                setTargetGstr9Id(returns[0].id)
              }
              setShowJsonImportModal(true)
            }}
            className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white font-medium rounded-lg shadow-sm transition flex items-center gap-2 text-sm"
          >
            <span>📥</span> Import GSTR-1 / 2B JSON
          </button>
          <button
            onClick={handleOpenCompileModal}
            className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white font-medium rounded-lg shadow-sm transition flex items-center gap-2 text-sm"
          >
            <span>+</span> Compile Annual Return
          </button>
        </div>
      </div>

      {/* Summary Cards */}
      {summary && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <div className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Annual Returns</div>
            <div className="text-2xl font-bold text-gray-900 mt-1">{summary.totalReturns}</div>
            <div className="text-xs text-emerald-600 mt-1 font-medium">{summary.filedReturnsCount} Filed with ARN</div>
          </div>
          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <div className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Statutory Mandate</div>
            <div className="text-2xl font-bold text-indigo-600 mt-1">{summary.mandatoryGstr9Count} <span className="text-xs text-gray-500 font-normal">GSTR-9 (&gt;₹2 Cr)</span></div>
            <div className="text-xs text-purple-600 mt-1 font-medium">{summary.mandatoryGstr9cCount} GSTR-9C Required (&gt;₹5 Cr)</div>
          </div>
          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <div className="text-xs font-semibold text-gray-500 uppercase tracking-wider">Total Reconciled Turnover</div>
            <div className="text-2xl font-bold text-emerald-700 mt-1">{formatCurrency(summary.totalTurnoverReconciled)}</div>
            <div className="text-xs text-gray-500 mt-1">Total Tax: {formatCurrency(summary.totalTaxPayable)}</div>
          </div>
          <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm">
            <div className="text-xs font-semibold text-gray-500 uppercase tracking-wider">DRC-03 Tax Recovery</div>
            <div className="text-2xl font-bold text-rose-600 mt-1">{formatCurrency(summary.totalTaxShortfall)}</div>
            <div className="text-xs text-emerald-600 mt-1 font-medium">{formatCurrency(summary.totalDrc03Paid)} Paid via DRC-03</div>
          </div>
        </div>
      )}

      {/* Filters Bar */}
      <div className="bg-white p-4 rounded-xl border border-gray-200 shadow-sm flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-wrap items-center gap-4">
          <div>
            <label className="block text-xs font-medium text-gray-700 mb-1">Financial Year</label>
            <select
              value={selectedFy}
              onChange={(e) => setSelectedFy(e.target.value)}
              className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:ring-indigo-500 focus:border-indigo-500"
            >
              <option value="">All FYs</option>
              <option value="2024-25">FY 2024-25</option>
              <option value="2023-24">FY 2023-24</option>
              <option value="2022-23">FY 2022-23</option>
              <option value="2021-22">FY 2021-22</option>
            </select>
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-700 mb-1">Client</label>
            <select
              value={selectedClientId}
              onChange={(e) => setSelectedClientId(e.target.value)}
              className="border border-gray-300 rounded-lg px-3 py-1.5 text-sm focus:ring-indigo-500 focus:border-indigo-500"
            >
              <option value="">All Clients</option>
              {clients.map(c => (
                <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
              ))}
            </select>
          </div>
        </div>
        <div className="text-xs text-gray-500">
          Showing <span className="font-semibold text-gray-900">{returns.length}</span> annual returns
        </div>
      </div>

      {/* Returns Table */}
      <div className="bg-white rounded-xl border border-gray-200 shadow-sm overflow-hidden">
        {loading ? (
          <div className="p-8 text-center text-gray-500">Loading annual returns...</div>
        ) : error ? (
          <div className="p-8 text-center text-rose-500">{error}</div>
        ) : returns.length === 0 ? (
          <div className="p-12 text-center text-gray-500">
            <p className="text-base font-medium text-gray-900">No GSTR-9 annual returns found</p>
            <p className="text-sm mt-1">Compile your first annual return by clicking "+ Compile Annual Return".</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="min-w-full divide-y divide-gray-200 text-sm">
              <thead className="bg-gray-50">
                <tr>
                  <th className="px-4 py-3 text-left font-semibold text-gray-700">Client / GSTIN</th>
                  <th className="px-4 py-3 text-left font-semibold text-gray-700">FY</th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-700">Turnover</th>
                  <th className="px-4 py-3 text-left font-semibold text-gray-700">Threshold Mandate</th>
                  <th className="px-4 py-3 text-center font-semibold text-gray-700">GSTR-9</th>
                  <th className="px-4 py-3 text-center font-semibold text-gray-700">GSTR-9C</th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-700">ITC Variance (8D)</th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-700">Tax Shortfall</th>
                  <th className="px-4 py-3 text-center font-semibold text-gray-700">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-200">
                {returns.map((ret) => (
                  <tr key={ret.id} className="hover:bg-gray-50 transition">
                    <td className="px-4 py-3">
                      <div className="font-semibold text-gray-900">{ret.clientName}</div>
                      <div className="text-xs text-gray-500 font-mono">{ret.gstin}</div>
                    </td>
                    <td className="px-4 py-3 font-medium text-gray-800">{ret.financialYear}</td>
                    <td className="px-4 py-3 text-right font-medium text-gray-900">
                      {formatCurrency(ret.aggregateTurnover)}
                    </td>
                    <td className="px-4 py-3">
                      {ret.isGstr9cMandatory ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-purple-100 text-purple-800">
                          Mandatory 9 &amp; 9C (&gt;₹5 Cr)
                        </span>
                      ) : ret.isGstr9Mandatory ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-semibold bg-indigo-100 text-indigo-800">
                          Mandatory GSTR-9 (&gt;₹2 Cr)
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-0.5 rounded text-xs font-medium bg-gray-100 text-gray-700">
                          Exempt (Optional)
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <span className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${
                        ret.status === 'Filed' ? 'bg-emerald-100 text-emerald-800' :
                        ret.status === 'Certified' ? 'bg-blue-100 text-blue-800' :
                        ret.status === 'Reconciled' ? 'bg-indigo-100 text-indigo-800' :
                        'bg-amber-100 text-amber-800'
                      }`}>
                        {ret.status}
                      </span>
                      {ret.arn && (
                        <div className="text-[10px] text-gray-500 font-mono mt-0.5">{ret.arn}</div>
                      )}
                    </td>
                    <td className="px-4 py-3 text-center">
                      {ret.gstr9cStatus ? (
                        <span className={`inline-flex items-center px-2 py-0.5 rounded text-xs font-medium ${
                          ret.gstr9cStatus === 'Certified' ? 'bg-emerald-100 text-emerald-800 font-bold' :
                          ret.gstr9cStatus === 'UnderReview' ? 'bg-blue-100 text-blue-800' :
                          'bg-gray-100 text-gray-700'
                        }`}>
                          {ret.gstr9cStatus}
                        </span>
                      ) : (
                        <span className="text-xs text-gray-400">N/A</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <span className={`font-mono text-xs ${ret.itcReconciledVariance < 0 ? 'text-rose-600 font-bold' : 'text-gray-700'}`}>
                        {formatCurrency(ret.itcReconciledVariance)}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-right font-medium">
                      {ret.shortfallTax > 0 ? (
                        <span className="text-rose-600 font-bold">{formatCurrency(ret.shortfallTax)}</span>
                      ) : (
                        <span className="text-emerald-600 font-semibold">Nil</span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-center">
                      <div className="flex items-center justify-center gap-1.5">
                        <button
                          onClick={() => handleViewGstr9Tables(ret)}
                          title="View GSTR-9 Tables"
                          className="px-2 py-1 bg-gray-100 hover:bg-gray-200 text-gray-800 rounded text-xs font-medium"
                        >
                          Tables
                        </button>
                        <button
                          onClick={() => handleOpenGstr9cDesk(ret)}
                          title="GSTR-9C Reconciliation Desk"
                          className="px-2 py-1 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 rounded text-xs font-medium"
                        >
                          9C Desk
                        </button>
                        {ret.status !== 'Filed' && (
                          <button
                            onClick={() => {
                              setViewingReturn(ret)
                              setFileArn('')
                              setFileNotes('')
                              setShowFileModal(true)
                            }}
                            title="Mark as Filed"
                            className="px-2 py-1 bg-emerald-50 hover:bg-emerald-100 text-emerald-700 rounded text-xs font-medium"
                          >
                            File
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Compile Return Modal */}
      {showCompileModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4 overflow-y-auto">
          <div className="bg-white rounded-xl max-w-3xl w-full p-6 shadow-xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b pb-3">
              <h3 className="text-lg font-bold text-gray-900">Compile GSTR-9 Annual Return</h3>
              <button onClick={() => setShowCompileModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>
            <form onSubmit={handleCompileSubmit} className="space-y-4">
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Client *</label>
                  <select
                    value={compileForm.clientId}
                    onChange={(e) => setCompileForm({ ...compileForm, clientId: e.target.value })}
                    required
                    className="w-full border border-gray-300 rounded-lg p-2 text-sm focus:ring-indigo-500"
                  >
                    <option value="">Select a client...</option>
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                    ))}
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Financial Year *</label>
                  <input
                    type="text"
                    value={compileForm.financialYear}
                    onChange={(e) => setCompileForm({ ...compileForm, financialYear: e.target.value })}
                    required
                    className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                  />
                </div>
              </div>

              {/* Turnover & Statutory Gating */}
              <div className="bg-indigo-50/50 p-4 rounded-lg border border-indigo-100 space-y-2">
                <div className="font-semibold text-sm text-indigo-900">Statutory Threshold Evaluation</div>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Aggregate Turnover (All India PAN) *</label>
                    <input
                      type="number"
                      value={compileForm.aggregateTurnover || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, aggregateTurnover: parseFloat(e.target.value) || 0 })}
                      required
                      placeholder="e.g. 65000000"
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                  <div className="flex flex-col justify-center">
                    <span className="text-xs font-semibold text-gray-600">Applicable Statutory Requirement:</span>
                    <span className="text-sm font-bold text-indigo-800 mt-1">
                      {compileForm.aggregateTurnover > 50000000 ? 'Mandatory GSTR-9 + GSTR-9C (> ₹5 Cr)' :
                       compileForm.aggregateTurnover > 20000000 ? 'Mandatory GSTR-9 (> ₹2 Cr, 9C Exempt)' :
                       'Exempt from mandatory filing (Optional)'}
                    </span>
                  </div>
                </div>
              </div>

              {/* Outward Supplies */}
              <div>
                <h4 className="text-xs font-bold uppercase tracking-wider text-gray-500 mb-2">Part II: Outward Supplies</h4>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">Outward Taxable Supplies (4N)</label>
                    <input
                      type="number"
                      value={compileForm.outwardTaxableSupplies || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, outwardTaxableSupplies: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">Exempt / Nil / Non-GST (5M)</label>
                    <input
                      type="number"
                      value={compileForm.outwardExemptedSupplies || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, outwardExemptedSupplies: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">Inward RCM Supplies (4G)</label>
                    <input
                      type="number"
                      value={compileForm.rcmInwardSupplies || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, rcmInwardSupplies: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                </div>
              </div>

              {/* ITC Availed */}
              <div>
                <h4 className="text-xs font-bold uppercase tracking-wider text-gray-500 mb-2">Part III: Input Tax Credit (ITC)</h4>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">ITC Availed in GSTR-3B (6A)</label>
                    <input
                      type="number"
                      value={compileForm.itcAvailed3b || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, itcAvailed3b: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">B2B Inputs &amp; Services (6B)</label>
                    <input
                      type="number"
                      value={compileForm.itcCategorizedB2b || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, itcCategorizedB2b: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">ITC Reversed (7I)</label>
                    <input
                      type="number"
                      value={compileForm.itcReversed || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, itcReversed: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">ITC Auto-drafted in 2A/2B (8A)</label>
                    <input
                      type="number"
                      value={compileForm.itcAsPer2a || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, itcAsPer2a: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">ITC Availed in Subsequent FY (8C)</label>
                    <input
                      type="number"
                      value={compileForm.itcSubsequentFy || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, itcSubsequentFy: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                </div>
              </div>

              {/* Tax Paid */}
              <div>
                <h4 className="text-xs font-bold uppercase tracking-wider text-gray-500 mb-2">Part IV: Tax Payable &amp; Paid</h4>
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">Total Tax Payable (Table 9)</label>
                    <input
                      type="number"
                      value={compileForm.totalTaxPayable || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, totalTaxPayable: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">Tax Paid in Cash</label>
                    <input
                      type="number"
                      value={compileForm.taxPaidCash || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, taxPaidCash: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                  <div>
                    <label className="block text-xs text-gray-700 mb-1">Tax Paid via ITC</label>
                    <input
                      type="number"
                      value={compileForm.taxPaidItc || ''}
                      onChange={(e) => setCompileForm({ ...compileForm, taxPaidItc: parseFloat(e.target.value) || 0 })}
                      className="w-full border border-gray-300 rounded-lg p-2 text-sm"
                    />
                  </div>
                </div>
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCompileModal(false)}
                  className="px-4 py-2 border border-gray-300 rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-semibold shadow-sm"
                >
                  Compile Return
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* GSTR-9 Table Viewer Modal */}
      {viewingReturn && !showGstr9cModal && !showFileModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4 overflow-y-auto">
          <div className="bg-white rounded-xl max-w-4xl w-full p-6 shadow-xl space-y-4 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b pb-3">
              <div>
                <h3 className="text-lg font-bold text-gray-900">{viewingReturn.clientName} - GSTR-9 Annual Return</h3>
                <p className="text-xs text-gray-500 font-mono">GSTIN: {viewingReturn.gstin} | FY: {viewingReturn.financialYear} | Status: {viewingReturn.status}</p>
              </div>
              <button onClick={() => setViewingReturn(null)} className="text-gray-400 hover:text-gray-600 text-xl font-bold">&times;</button>
            </div>

            {/* Navigation Tabs */}
            <div className="flex border-b text-sm">
              <button
                onClick={() => setActiveGstr9Tab('outward')}
                className={`px-4 py-2 font-medium border-b-2 -mb-px transition ${
                  activeGstr9Tab === 'outward' ? 'border-indigo-600 text-indigo-600' : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                Part II: Outward Supplies (Table 4 &amp; 5)
              </button>
              <button
                onClick={() => setActiveGstr9Tab('itc')}
                className={`px-4 py-2 font-medium border-b-2 -mb-px transition ${
                  activeGstr9Tab === 'itc' ? 'border-indigo-600 text-indigo-600' : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                Part III: ITC Reconciliation (Table 6, 7 &amp; 8)
              </button>
              <button
                onClick={() => setActiveGstr9Tab('tax')}
                className={`px-4 py-2 font-medium border-b-2 -mb-px transition ${
                  activeGstr9Tab === 'tax' ? 'border-indigo-600 text-indigo-600' : 'border-transparent text-gray-500 hover:text-gray-700'
                }`}
              >
                Part IV: Tax Paid &amp; Shortfall (Table 9)
              </button>
            </div>

            {/* Tab 1: Outward */}
            {activeGstr9Tab === 'outward' && (
              <div className="space-y-4 text-sm">
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 bg-gray-50 p-3 rounded-lg">
                  <div>
                    <span className="text-xs text-gray-500 block">Outward Taxable Supplies (4N)</span>
                    <span className="font-bold text-gray-900">{formatCurrency(viewingReturn.outwardTaxableSupplies)}</span>
                  </div>
                  <div>
                    <span className="text-xs text-gray-500 block">Exempt / Nil / Non-GST (5M)</span>
                    <span className="font-bold text-gray-900">{formatCurrency(viewingReturn.outwardExemptedSupplies)}</span>
                  </div>
                  <div>
                    <span className="text-xs text-gray-500 block">Total Annual Turnover (5N)</span>
                    <span className="font-bold text-indigo-700">{formatCurrency(viewingReturn.totalTurnover)}</span>
                  </div>
                </div>
                <div className="border rounded-lg overflow-hidden">
                  <table className="min-w-full divide-y divide-gray-200 text-xs">
                    <thead className="bg-gray-50 font-semibold text-gray-700">
                      <tr>
                        <th className="px-3 py-2 text-left">Code</th>
                        <th className="px-3 py-2 text-left">Description</th>
                        <th className="px-3 py-2 text-right">Taxable Value</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-200">
                      <tr>
                        <td className="px-3 py-2 font-mono font-bold text-indigo-600">4A</td>
                        <td className="px-3 py-2">Supplies to unregistered persons (B2C)</td>
                        <td className="px-3 py-2 text-right">{formatCurrency(viewingReturn.outwardTaxableSupplies * 0.2)}</td>
                      </tr>
                      <tr>
                        <td className="px-3 py-2 font-mono font-bold text-indigo-600">4B</td>
                        <td className="px-3 py-2">Supplies to registered persons (B2B)</td>
                        <td className="px-3 py-2 text-right">{formatCurrency(viewingReturn.outwardTaxableSupplies * 0.8)}</td>
                      </tr>
                      <tr>
                        <td className="px-3 py-2 font-mono font-bold text-indigo-600">5A</td>
                        <td className="px-3 py-2">Zero-rated supply (Export) without payment of tax</td>
                        <td className="px-3 py-2 text-right">{formatCurrency(viewingReturn.outwardExemptedSupplies * 0.3)}</td>
                      </tr>
                      <tr>
                        <td className="px-3 py-2 font-mono font-bold text-indigo-600">5C</td>
                        <td className="px-3 py-2">Exempted supplies</td>
                        <td className="px-3 py-2 text-right">{formatCurrency(viewingReturn.outwardExemptedSupplies * 0.7)}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            )}

            {/* Tab 2: ITC */}
            {activeGstr9Tab === 'itc' && (
              <div className="space-y-4 text-sm">
                <div className="grid grid-cols-1 sm:grid-cols-4 gap-3 bg-gray-50 p-3 rounded-lg">
                  <div>
                    <span className="text-xs text-gray-500 block">ITC in GSTR-3B (6A)</span>
                    <span className="font-bold text-gray-900">{formatCurrency(viewingReturn.itcAvailed3b)}</span>
                  </div>
                  <div>
                    <span className="text-xs text-gray-500 block">ITC Reversed (7I)</span>
                    <span className="font-bold text-rose-600">{formatCurrency(viewingReturn.itcReversed)}</span>
                  </div>
                  <div>
                    <span className="text-xs text-gray-500 block">Net ITC (7J)</span>
                    <span className="font-bold text-emerald-700">{formatCurrency(viewingReturn.netItcAvailed)}</span>
                  </div>
                  <div>
                    <span className="text-xs text-gray-500 block">ITC as per 2A/2B (8A)</span>
                    <span className="font-bold text-purple-700">{formatCurrency(viewingReturn.itcAsPer2a)}</span>
                  </div>
                </div>

                {/* Table 8 Reconciliation Card */}
                <div className={`p-4 rounded-lg border ${
                  viewingReturn.itcReconciledVariance < 0 ? 'bg-rose-50 border-rose-200' : 'bg-emerald-50 border-emerald-200'
                }`}>
                  <div className="flex items-center justify-between">
                    <div>
                      <div className="font-bold text-sm text-gray-900">Table 8D Reconciliation Difference [8A - (8B + 8C)]</div>
                      <div className="text-xs text-gray-600 mt-1">
                        {viewingReturn.itcReconciledVariance < 0 ?
                          'Excess ITC claimed in GSTR-3B over GSTR-2A! Demands or DRC-03 recovery may apply.' :
                          'ITC available in 2A/2B but not claimed (credit lapsed or ineligible u/s 17(5)).'}
                      </div>
                    </div>
                    <div className="text-xl font-bold font-mono">
                      {formatCurrency(viewingReturn.itcReconciledVariance)}
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* Tab 3: Tax */}
            {activeGstr9Tab === 'tax' && (
              <div className="space-y-4 text-sm">
                <div className="grid grid-cols-1 sm:grid-cols-3 gap-4 bg-gray-50 p-3 rounded-lg">
                  <div>
                    <span className="text-xs text-gray-500 block">Total Tax Payable (Table 9)</span>
                    <span className="font-bold text-gray-900">{formatCurrency(viewingReturn.totalTaxPayable)}</span>
                  </div>
                  <div>
                    <span className="text-xs text-gray-500 block">Paid (Cash + ITC)</span>
                    <span className="font-bold text-emerald-700">{formatCurrency(viewingReturn.taxPaidCash + viewingReturn.taxPaidItc)}</span>
                  </div>
                  <div>
                    <span className="text-xs text-gray-500 block">Tax Shortfall (DRC-03 Payable)</span>
                    <span className={`font-bold ${viewingReturn.shortfallTax > 0 ? 'text-rose-600' : 'text-emerald-700'}`}>
                      {formatCurrency(viewingReturn.shortfallTax)}
                    </span>
                  </div>
                </div>
              </div>
            )}

            <div className="flex justify-end gap-3 pt-3 border-t">
              <button
                onClick={() => setViewingReturn(null)}
                className="px-4 py-2 border border-gray-300 rounded-lg text-sm text-gray-700 hover:bg-gray-50"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

      {/* GSTR-9C Reconciliation Desk Modal */}
      {showGstr9cModal && activeGstr9c && viewingReturn && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4 overflow-y-auto">
          <div className="bg-white rounded-xl max-w-5xl w-full p-6 shadow-xl space-y-6 max-h-[92vh] overflow-y-auto">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between border-b pb-3 gap-2">
              <div>
                <div className="flex items-center gap-2">
                  <h3 className="text-lg font-bold text-gray-900">Form GSTR-9C Reconciliation Statement</h3>
                  <span className={`px-2 py-0.5 rounded text-xs font-bold ${
                    activeGstr9c.certificationStatus === 'Certified' ? 'bg-emerald-100 text-emerald-800' :
                    activeGstr9c.certificationStatus === 'UnderReview' ? 'bg-blue-100 text-blue-800' :
                    'bg-amber-100 text-amber-800'
                  }`}>
                    {activeGstr9c.certificationStatus}
                  </span>
                </div>
                <p className="text-xs text-gray-500">{viewingReturn.clientName} | FY: {activeGstr9c.financialYear}</p>
              </div>
              <div className="flex items-center gap-2">
                <button
                  onClick={() => setShowAddDiscrepancyModal(true)}
                  className="px-3 py-1.5 bg-gray-100 hover:bg-gray-200 text-gray-700 font-medium rounded-lg text-xs"
                >
                  + Add Discrepancy
                </button>
                {activeGstr9c.certificationStatus !== 'Certified' && (
                  <button
                    onClick={() => {
                      setCertifyForm({
                        auditorName: activeGstr9c.auditorName || '',
                        auditorMembershipNo: activeGstr9c.auditorMembershipNo || '',
                        auditorFirmRegNo: activeGstr9c.auditorFirmRegNo || '',
                        udin: activeGstr9c.udin || '',
                        auditorRemarks: activeGstr9c.auditorRemarks || ''
                      })
                      setShowCertifyModal(true)
                    }}
                    className="px-3 py-1.5 bg-indigo-600 hover:bg-indigo-700 text-white font-medium rounded-lg text-xs shadow-sm"
                  >
                    Certify GSTR-9C
                  </button>
                )}
                <button
                  onClick={() => {
                    setDrc03Form({
                      drc03Arn: activeGstr9c.drc03Arn || '',
                      drc03Date: activeGstr9c.drc03Date || new Date().toISOString().split('T')[0],
                      drc03PaidAmount: activeGstr9c.drc03PaidAmount || activeGstr9c.totalAdditionalPayable,
                      drc03PaymentMode: activeGstr9c.drc03PaymentMode || 'CashLedger'
                    })
                    setShowDrc03Modal(true)
                  }}
                  className="px-3 py-1.5 bg-emerald-600 hover:bg-emerald-700 text-white font-medium rounded-lg text-xs shadow-sm"
                >
                  Record DRC-03
                </button>
                <button onClick={() => setShowGstr9cModal(false)} className="text-gray-400 hover:text-gray-600 text-xl font-bold ml-2">&times;</button>
              </div>
            </div>

            {/* Table 5 Gross Turnover Reconciliation */}
            <form onSubmit={handleReconcileSubmit} className="space-y-6">
              <div className="bg-gray-50 p-4 rounded-xl border border-gray-200 space-y-3">
                <div className="flex items-center justify-between border-b pb-2">
                  <h4 className="font-bold text-sm text-gray-900">Table 5: Reconciliation of Gross Turnover</h4>
                  <div className="text-xs">
                    Variance (5R): <span className={`font-mono font-bold ${activeGstr9c.unreconciledTurnoverVariance !== 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
                      {formatCurrency(activeGstr9c.unreconciledTurnoverVariance)}
                    </span>
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs">
                  <div>
                    <label className="block text-gray-700 mb-1">Audited Gross Turnover (5A)</label>
                    <input
                      type="number"
                      value={reconcileForm.auditedGrossTurnover || ''}
                      onChange={(e) => setReconcileForm({ ...reconcileForm, auditedGrossTurnover: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded p-1.5 font-mono"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-700 mb-1">Unbilled Revenue at Start (5B) (+)</label>
                    <input
                      type="number"
                      value={reconcileForm.unbilledRevenueStart || ''}
                      onChange={(e) => setReconcileForm({ ...reconcileForm, unbilledRevenueStart: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded p-1.5 font-mono"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-700 mb-1">Unadjusted Advances at End (5C) (+)</label>
                    <input
                      type="number"
                      value={reconcileForm.unadjustedAdvancesEnd || ''}
                      onChange={(e) => setReconcileForm({ ...reconcileForm, unadjustedAdvancesEnd: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded p-1.5 font-mono"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-700 mb-1">Credit Notes after FY (5E) (+)</label>
                    <input
                      type="number"
                      value={reconcileForm.creditNotesAfterFy || ''}
                      onChange={(e) => setReconcileForm({ ...reconcileForm, creditNotesAfterFy: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded p-1.5 font-mono"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-700 mb-1">Unbilled Revenue at End (5H) (-)</label>
                    <input
                      type="number"
                      value={reconcileForm.unbilledRevenueEnd || ''}
                      onChange={(e) => setReconcileForm({ ...reconcileForm, unbilledRevenueEnd: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded p-1.5 font-mono"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-700 mb-1">Unadjusted Advances at Start (5I) (-)</label>
                    <input
                      type="number"
                      value={reconcileForm.unadjustedAdvancesStart || ''}
                      onChange={(e) => setReconcileForm({ ...reconcileForm, unadjustedAdvancesStart: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded p-1.5 font-mono"
                    />
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-2 bg-white p-2.5 rounded border text-xs">
                  <div>
                    <span className="text-gray-500">Reconciled Turnover (5P):</span>
                    <span className="ml-2 font-bold font-mono text-gray-900">{formatCurrency(activeGstr9c.reconciledTurnover)}</span>
                  </div>
                  <div>
                    <span className="text-gray-500">Turnover as per GSTR-9 (5Q):</span>
                    <span className="ml-2 font-bold font-mono text-gray-900">{formatCurrency(activeGstr9c.turnoverAsPerGstr9)}</span>
                  </div>
                  <div>
                    <span className="text-gray-500">Unreconciled Variance (5R):</span>
                    <span className={`ml-2 font-bold font-mono ${activeGstr9c.unreconciledTurnoverVariance !== 0 ? 'text-rose-600' : 'text-emerald-600'}`}>
                      {formatCurrency(activeGstr9c.unreconciledTurnoverVariance)}
                    </span>
                  </div>
                </div>
              </div>

              {/* Table 7 Taxable Turnover & Table 9 Additional Tax */}
              <div className="bg-gray-50 p-4 rounded-xl border border-gray-200 space-y-3">
                <div className="flex items-center justify-between border-b pb-2">
                  <h4 className="font-bold text-sm text-gray-900">Table 7 &amp; 9: Taxable Turnover &amp; Additional Tax Payable</h4>
                  <div className="text-xs">
                    Total Additional Due: <span className="font-mono font-bold text-rose-600">{formatCurrency(activeGstr9c.totalAdditionalPayable)}</span>
                  </div>
                </div>

                <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-xs">
                  <div>
                    <label className="block text-gray-700 mb-1">Exempt / Nil Turnover (7B)</label>
                    <input
                      type="number"
                      value={reconcileForm.exemptedNilTurnover || ''}
                      onChange={(e) => setReconcileForm({ ...reconcileForm, exemptedNilTurnover: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded p-1.5 font-mono"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-700 mb-1">Additional Tax Payable</label>
                    <input
                      type="number"
                      value={reconcileForm.additionalTaxPayable || ''}
                      onChange={(e) => setReconcileForm({ ...reconcileForm, additionalTaxPayable: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded p-1.5 font-mono text-rose-600 font-bold"
                    />
                  </div>
                  <div>
                    <label className="block text-gray-700 mb-1">Additional Interest Payable</label>
                    <input
                      type="number"
                      value={reconcileForm.additionalInterestPayable || ''}
                      onChange={(e) => setReconcileForm({ ...reconcileForm, additionalInterestPayable: parseFloat(e.target.value) || 0 })}
                      className="w-full border rounded p-1.5 font-mono text-amber-700 font-bold"
                    />
                  </div>
                </div>

                <div>
                  <label className="block text-xs text-gray-700 mb-1">Auditor Remarks / Qualifications</label>
                  <input
                    type="text"
                    value={reconcileForm.auditorRemarks || ''}
                    onChange={(e) => setReconcileForm({ ...reconcileForm, auditorRemarks: e.target.value })}
                    placeholder="Enter audit reconciliation notes or qualification remarks..."
                    className="w-full border rounded p-1.5 text-xs"
                  />
                </div>
              </div>

              {/* Discrepancies Table */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <h4 className="font-bold text-sm text-gray-900">Audit Discrepancies Register ({activeGstr9c.discrepancies.length})</h4>
                </div>
                {activeGstr9c.discrepancies.length === 0 ? (
                  <div className="p-4 bg-gray-50 rounded-lg text-center text-xs text-gray-500">No discrepancies logged.</div>
                ) : (
                  <div className="border rounded-lg overflow-hidden">
                    <table className="min-w-full divide-y divide-gray-200 text-xs">
                      <thead className="bg-gray-50 font-semibold text-gray-700">
                        <tr>
                          <th className="px-3 py-2 text-left">Category</th>
                          <th className="px-3 py-2 text-left">Table</th>
                          <th className="px-3 py-2 text-left">Description</th>
                          <th className="px-3 py-2 text-right">Amount</th>
                          <th className="px-3 py-2 text-right">Tax Impact</th>
                          <th className="px-3 py-2 text-center">Status</th>
                          <th className="px-3 py-2 text-center">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-gray-200">
                        {activeGstr9c.discrepancies.map((d) => (
                          <tr key={d.id}>
                            <td className="px-3 py-2 font-medium text-gray-800">{d.category}</td>
                            <td className="px-3 py-2 text-gray-500">{d.tableRef}</td>
                            <td className="px-3 py-2 text-gray-900">{d.description}</td>
                            <td className="px-3 py-2 text-right font-mono">{formatCurrency(d.discrepancyAmount)}</td>
                            <td className="px-3 py-2 text-right font-mono font-bold text-rose-600">{formatCurrency(d.taxImpact)}</td>
                            <td className="px-3 py-2 text-center">
                              <span className={`px-1.5 py-0.5 rounded text-[10px] font-semibold ${
                                d.isResolved ? 'bg-emerald-100 text-emerald-800' : 'bg-rose-100 text-rose-800'
                              }`}>
                                {d.isResolved ? 'Resolved' : 'Pending'}
                              </span>
                            </td>
                            <td className="px-3 py-2 text-center">
                              {!d.isResolved && (
                                <button
                                  type="button"
                                  onClick={() => handleResolveDiscrepancy(d.id)}
                                  className="text-xs text-indigo-600 hover:text-indigo-800 font-medium"
                                >
                                  Resolve
                                </button>
                              )}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              {/* DRC-03 & Certification Info */}
              {(activeGstr9c.auditorName || activeGstr9c.drc03Arn) && (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 bg-indigo-50/50 p-3 rounded-lg border border-indigo-100 text-xs">
                  {activeGstr9c.auditorName && (
                    <div>
                      <span className="font-semibold text-indigo-900 block">Certified By:</span>
                      <span className="text-gray-800">{activeGstr9c.auditorName} (M.No: {activeGstr9c.auditorMembershipNo})</span>
                      {activeGstr9c.udin && <div className="text-gray-500 font-mono">UDIN: {activeGstr9c.udin}</div>}
                    </div>
                  )}
                  {activeGstr9c.drc03Arn && (
                    <div>
                      <span className="font-semibold text-emerald-900 block">DRC-03 Payment:</span>
                      <span className="text-gray-800">ARN: {activeGstr9c.drc03Arn} ({formatCurrency(activeGstr9c.drc03PaidAmount)})</span>
                      <div className="text-emerald-700 font-semibold">{activeGstr9c.isDrc03Completed ? 'Fully Paid' : 'Partial Payment'}</div>
                    </div>
                  )}
                </div>
              )}

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowGstr9cModal(false)}
                  className="px-4 py-2 border border-gray-300 rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Close
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-semibold shadow-sm"
                >
                  Save &amp; Recalculate
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Add Discrepancy Modal */}
      {showAddDiscrepancyModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl max-w-lg w-full p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-bold text-gray-900">Add Audit Discrepancy</h3>
            <form onSubmit={handleAddDiscrepancySubmit} className="space-y-3">
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Category</label>
                <select
                  value={discrepancyForm.category}
                  onChange={(e) => setDiscrepancyForm({ ...discrepancyForm, category: e.target.value as (typeof discrepancyForm)['category'] })}
                  className="w-full border rounded-lg p-2 text-sm"
                >
                  <option value="TurnoverTimingDifference">Turnover Timing Difference</option>
                  <option value="UnbilledRevenue">Unbilled Revenue Recognition</option>
                  <option value="AdvanceAdjustment">Advance Tax Adjustment</option>
                  <option value="CreditNoteAdjustment">Credit Note Adjustment</option>
                  <option value="RateMismatch">GST Rate Mismatch</option>
                  <option value="ItcBookVsReturnMismatch">ITC Books vs Return Mismatch</option>
                  <option value="ExpenseItcUnclaimed">Expense ITC Unclaimed</option>
                  <option value="Other">Other Discrepancy</option>
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Table Reference</label>
                <select
                  value={discrepancyForm.tableRef}
                  onChange={(e) => setDiscrepancyForm({ ...discrepancyForm, tableRef: e.target.value })}
                  className="w-full border rounded-lg p-2 text-sm"
                >
                  <option value="Table5">Table 5 (Gross Turnover)</option>
                  <option value="Table7">Table 7 (Taxable Turnover)</option>
                  <option value="Table9">Table 9 (Rate-wise Tax)</option>
                  <option value="Table12">Table 12 (ITC Reconciliation)</option>
                </select>
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Description *</label>
                <input
                  type="text"
                  value={discrepancyForm.description}
                  onChange={(e) => setDiscrepancyForm({ ...discrepancyForm, description: e.target.value })}
                  required
                  placeholder="e.g. Unbilled services recognized in Q4 books"
                  className="w-full border rounded-lg p-2 text-sm"
                />
              </div>
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Discrepancy Amount</label>
                  <input
                    type="number"
                    value={discrepancyForm.discrepancyAmount || ''}
                    onChange={(e) => setDiscrepancyForm({ ...discrepancyForm, discrepancyAmount: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded-lg p-2 text-sm font-mono"
                  />
                </div>
                <div>
                  <label className="block text-xs font-semibold text-gray-700 mb-1">Tax Impact</label>
                  <input
                    type="number"
                    value={discrepancyForm.taxImpact || ''}
                    onChange={(e) => setDiscrepancyForm({ ...discrepancyForm, taxImpact: parseFloat(e.target.value) || 0 })}
                    className="w-full border rounded-lg p-2 text-sm font-mono text-rose-600 font-bold"
                  />
                </div>
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Auditor Recommendation</label>
                <input
                  type="text"
                  value={discrepancyForm.auditorRecommendation || ''}
                  onChange={(e) => setDiscrepancyForm({ ...discrepancyForm, auditorRecommendation: e.target.value })}
                  placeholder="e.g. Pay tax through Form GST DRC-03"
                  className="w-full border rounded-lg p-2 text-sm"
                />
              </div>
              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowAddDiscrepancyModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-semibold"
                >
                  Add Discrepancy
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Certify Modal */}
      {showCertifyModal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-bold text-gray-900">Certify Form GSTR-9C</h3>
            <p className="text-xs text-gray-500">Provide the signing Chartered Accountant's credentials and statutory UDIN.</p>
            <form onSubmit={handleCertifySubmit} className="space-y-3">
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Auditor / CA Name *</label>
                <input
                  type="text"
                  value={certifyForm.auditorName}
                  onChange={(e) => setCertifyForm({ ...certifyForm, auditorName: e.target.value })}
                  required
                  placeholder="e.g. CA Ramesh Kumar"
                  className="w-full border rounded-lg p-2 text-sm"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">ICAI Membership Number *</label>
                <input
                  type="text"
                  value={certifyForm.auditorMembershipNo}
                  onChange={(e) => setCertifyForm({ ...certifyForm, auditorMembershipNo: e.target.value })}
                  required
                  placeholder="e.g. 098765"
                  className="w-full border rounded-lg p-2 text-sm font-mono"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Firm Registration Number (FRN)</label>
                <input
                  type="text"
                  value={certifyForm.auditorFirmRegNo || ''}
                  onChange={(e) => setCertifyForm({ ...certifyForm, auditorFirmRegNo: e.target.value })}
                  placeholder="e.g. 102030W"
                  className="w-full border rounded-lg p-2 text-sm font-mono"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Unique Document Identification Number (UDIN)</label>
                <input
                  type="text"
                  value={certifyForm.udin || ''}
                  onChange={(e) => setCertifyForm({ ...certifyForm, udin: e.target.value })}
                  placeholder="e.g. 24098765BK12345678"
                  className="w-full border rounded-lg p-2 text-sm font-mono"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Verification Remarks</label>
                <textarea
                  value={certifyForm.auditorRemarks || ''}
                  onChange={(e) => setCertifyForm({ ...certifyForm, auditorRemarks: e.target.value })}
                  rows={2}
                  placeholder="Certificate opinion and qualifications..."
                  className="w-full border rounded-lg p-2 text-sm"
                />
              </div>
              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowCertifyModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg text-sm font-semibold"
                >
                  Sign &amp; Certify
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Record DRC-03 Modal */}
      {showDrc03Modal && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-bold text-gray-900">Record Form GST DRC-03 Payment</h3>
            <p className="text-xs text-gray-500">Record voluntary tax payment through electronic cash ledger towards annual return shortfalls.</p>
            <form onSubmit={handleDrc03Submit} className="space-y-3">
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">DRC-03 ARN *</label>
                <input
                  type="text"
                  value={drc03Form.drc03Arn}
                  onChange={(e) => setDrc03Form({ ...drc03Form, drc03Arn: e.target.value })}
                  required
                  placeholder="e.g. AA270324123456C"
                  className="w-full border rounded-lg p-2 text-sm font-mono"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Payment Date *</label>
                <input
                  type="date"
                  value={drc03Form.drc03Date}
                  onChange={(e) => setDrc03Form({ ...drc03Form, drc03Date: e.target.value })}
                  required
                  className="w-full border rounded-lg p-2 text-sm"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Amount Paid (₹) *</label>
                <input
                  type="number"
                  value={drc03Form.drc03PaidAmount || ''}
                  onChange={(e) => setDrc03Form({ ...drc03Form, drc03PaidAmount: parseFloat(e.target.value) || 0 })}
                  required
                  className="w-full border rounded-lg p-2 text-sm font-mono text-emerald-700 font-bold"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Payment Mode</label>
                <input
                  type="text"
                  value={drc03Form.drc03PaymentMode || 'CashLedger'}
                  onChange={(e) => setDrc03Form({ ...drc03Form, drc03PaymentMode: e.target.value })}
                  className="w-full border rounded-lg p-2 text-sm"
                />
              </div>
              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowDrc03Modal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-sm font-semibold"
                >
                  Record Payment
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* File Return Modal */}
      {showFileModal && viewingReturn && (
        <div className="fixed inset-0 z-50 bg-black/50 flex items-center justify-center p-4">
          <div className="bg-white rounded-xl max-w-md w-full p-6 shadow-xl space-y-4">
            <h3 className="text-lg font-bold text-gray-900">Mark GSTR-9 as Filed</h3>
            <p className="text-xs text-gray-500">Record the official GST Portal Acknowledgement Reference Number (ARN).</p>
            <form onSubmit={handleFileSubmit} className="space-y-3">
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Filing ARN *</label>
                <input
                  type="text"
                  value={fileArn}
                  onChange={(e) => setFileArn(e.target.value)}
                  required
                  placeholder="e.g. AA2712249876543"
                  className="w-full border rounded-lg p-2 text-sm font-mono"
                />
              </div>
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Notes</label>
                <textarea
                  value={fileNotes}
                  onChange={(e) => setFileNotes(e.target.value)}
                  rows={2}
                  placeholder="Optional filing notes..."
                  className="w-full border rounded-lg p-2 text-sm"
                />
              </div>
              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowFileModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-sm font-semibold"
                >
                  Confirm Filing
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* GSTR-1 & GSTR-2B JSON Import Modal */}
      {showJsonImportModal && (
        <div className="fixed inset-0 bg-black/50 backdrop-blur-sm z-50 flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-2xl w-full p-6 shadow-2xl max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between border-b pb-3 mb-4">
              <div>
                <h3 className="text-lg font-bold text-gray-900">Import GSTR-1 &amp; GSTR-2B JSON</h3>
                <p className="text-xs text-gray-500 mt-0.5">
                  Direct JSON import from the GST portal to auto-fill Table 4, 5, 6, and 8 of GSTR-9.
                </p>
              </div>
              <button
                onClick={() => setShowJsonImportModal(false)}
                className="text-gray-400 hover:text-gray-600 text-lg font-bold"
              >
                ✕
              </button>
            </div>

            <form onSubmit={handleImportGstJson} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-gray-700 mb-1">Target GSTR-9 Return</label>
                <select
                  value={targetGstr9Id}
                  onChange={(e) => setTargetGstr9Id(e.target.value)}
                  className="w-full border rounded-lg p-2 text-sm"
                  required
                >
                  <option value="">-- Choose Return --</option>
                  {returns.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.clientName} (FY {r.financialYear}) - Status: {r.status}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <div className="flex justify-between items-center mb-1">
                  <label className="text-xs font-semibold text-gray-700">GSTR-1 JSON (Outward Supplies)</label>
                  <button
                    type="button"
                    onClick={() => setGstr1Payload(`{
  "gstin": "27AAACB1234F1Z9",
  "fp": "2024-25",
  "b2b": [
    {
      "inv": [
        {
          "val": 1500000.00,
          "itms": [{ "txval": 1500000.00, "iamt": 0, "camt": 135000.00, "samt": 135000.00, "csamt": 0 }]
        }
      ]
    }
  ]
}`)}
                    className="text-xs text-indigo-600 hover:text-indigo-800 font-semibold"
                  >
                    Load Sample GSTR-1
                  </button>
                </div>
                <textarea
                  value={gstr1Payload}
                  onChange={(e) => setGstr1Payload(e.target.value)}
                  rows={4}
                  placeholder="Paste GSTR-1 JSON (b2b, b2cl, exp, etc.)..."
                  className="w-full border rounded-lg p-2 text-xs font-mono"
                />
              </div>

              <div>
                <div className="flex justify-between items-center mb-1">
                  <label className="text-xs font-semibold text-gray-700">GSTR-2B JSON (Inward Supplies &amp; ITC)</label>
                  <button
                    type="button"
                    onClick={() => setGstr2bPayload(`{
  "data": {
    "gstin": "27AAACB1234F1Z9",
    "fp": "042024",
    "b2b": [
      {
        "ctin": "27BBBCB5678M1Z1",
        "inv": [
          {
            "inum": "INV-1001",
            "val": 236000.00,
            "itcavl": "Y",
            "items": [{ "txval": 200000.00, "iamt": 0, "camt": 18000.00, "samt": 18000.00 }]
          }
        ]
      }
    ]
  }
}`)}
                    className="text-xs text-emerald-600 hover:text-emerald-800 font-semibold"
                  >
                    Load Sample GSTR-2B
                  </button>
                </div>
                <textarea
                  value={gstr2bPayload}
                  onChange={(e) => setGstr2bPayload(e.target.value)}
                  rows={4}
                  placeholder="Paste GSTR-2B JSON (b2b, cdnr, etc.)..."
                  className="w-full border rounded-lg p-2 text-xs font-mono"
                />
              </div>

              <div className="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  onClick={() => setShowJsonImportModal(false)}
                  className="px-4 py-2 border rounded-lg text-sm text-gray-700 hover:bg-gray-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={importingJson}
                  className="px-4 py-2 bg-emerald-600 hover:bg-emerald-700 text-white rounded-lg text-sm font-semibold"
                >
                  {importingJson ? 'Processing JSON...' : 'Auto-Fill GSTR-9'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
