import React, { useState, useEffect } from 'react'
import {
  eInvoicingApi,
  api,
  type EInvoiceDto,
  type EWayBillDto,
  type EInvoicingSummaryDto,
  type GenerateEInvoiceRequest,
  type GenerateEWayBillRequest,
  type UpdateEWayBillPartBRequest,
  type CancelEInvoiceRequest,
  type CancelEWayBillRequest,
  type Client,
  type ClientPage
} from './api'

interface EInvoicingPageProps {
  can?: (p: string) => boolean
}

const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat('en-IN', {
    style: 'currency',
    currency: 'INR',
    maximumFractionDigits: 2
  }).format(amount)
}

const formatDocType = (docType: number | string) => {
  if (docType === 1 || docType === 'INV') return 'INV (Tax Invoice)'
  if (docType === 2 || docType === 'CRN') return 'CRN (Credit Note)'
  if (docType === 3 || docType === 'DBN') return 'DBN (Debit Note)'
  return String(docType)
}

const formatInvoiceStatus = (status: number | string) => {
  if (status === 1 || status === 'Draft') return 'Draft'
  if (status === 2 || status === 'Generated') return 'Generated'
  if (status === 3 || status === 'Cancelled') return 'Cancelled'
  if (status === 4 || status === 'Failed') return 'Failed'
  return String(status)
}

const formatEwbStatus = (status: number | string) => {
  if (status === 1 || status === 'Active') return 'Active'
  if (status === 2 || status === 'Extended') return 'Extended'
  if (status === 3 || status === 'Cancelled') return 'Cancelled'
  if (status === 4 || status === 'Expired') return 'Expired'
  return String(status)
}

export const EInvoicingPage: React.FC<EInvoicingPageProps> = ({ can }) => {
  const [activeTab, setActiveTab] = useState<'invoices' | 'ewaybills'>('invoices')
  const [loading, setLoading] = useState<boolean>(false)
  const [error, setError] = useState<string | null>(null)
  const [success, setSuccess] = useState<string | null>(null)

  // Filters & Data
  const [clients, setClients] = useState<Client[]>([])
  const [selectedClientId, setSelectedClientId] = useState<string>('')
  const [summary, setSummary] = useState<EInvoicingSummaryDto | null>(null)
  const [invoices, setInvoices] = useState<EInvoiceDto[]>([])
  const [ewaybills, setEwaybills] = useState<EWayBillDto[]>([])

  // Modals
  const [showGenInvoiceModal, setShowGenInvoiceModal] = useState<boolean>(false)
  const [showGenEwbModal, setShowGenEwbModal] = useState<boolean>(false)
  const [showUpdatePartBModal, setShowUpdatePartBModal] = useState<boolean>(false)
  const [showCancelInvoiceModal, setShowCancelInvoiceModal] = useState<boolean>(false)
  const [showCancelEwbModal, setShowCancelEwbModal] = useState<boolean>(false)
  const [showQrModal, setShowQrModal] = useState<boolean>(false)

  // Selected Records
  const [selectedInvoice, setSelectedInvoice] = useState<EInvoiceDto | null>(null)
  const [selectedEwb, setSelectedEwb] = useState<EWayBillDto | null>(null)

  // Generate E-Invoice Form State
  const [invoiceForm, setInvoiceForm] = useState<GenerateEInvoiceRequest>({
    clientId: '',
    invoiceId: null,
    docType: 1,
    docNumber: '',
    docDate: new Date().toISOString().split('T')[0],
    financialYear: '2024-25',
    supplierGstin: '',
    supplierTradeName: '',
    recipientGstin: '',
    recipientLegalName: '',
    supplyType: 1,
    totalTaxableValue: 100000,
    cgstAmount: 9000,
    sgstAmount: 9000,
    igstAmount: 0,
    cessAmount: 0,
    mainHsnCode: '998311',
    itemCount: 1
  })

  // Generate E-Way Bill Form State
  const [ewbForm, setEwbForm] = useState<GenerateEWayBillRequest>({
    clientId: '',
    eInvoiceId: null,
    supplyType: 1,
    subSupplyType: 1,
    docType: 'INV',
    docNumber: '',
    docDate: new Date().toISOString().split('T')[0],
    fromGstin: '',
    fromTradeName: '',
    fromPincode: '400001',
    fromStateCode: 27,
    toGstin: '',
    toTradeName: '',
    toPincode: '560001',
    toStateCode: 29,
    totalValue: 118000,
    hsnCode: '847130',
    distanceKm: 980,
    transportMode: 1,
    vehicleType: 1,
    vehicleNumber: 'MH02AB1234',
    transporterId: '',
    transporterName: ''
  })

  // Update Part B Form
  const [partBForm, setPartBForm] = useState<UpdateEWayBillPartBRequest>({
    vehicleNumber: '',
    transporterId: '',
    transporterName: '',
    reason: 'Transshipment'
  })

  // Cancel Invoice Form
  const [cancelInvoiceForm, setCancelInvoiceForm] = useState<CancelEInvoiceRequest>({
    reason: '1: Duplicate',
    remarks: ''
  })

  // Cancel EWB Form
  const [cancelEwbForm, setCancelEwbForm] = useState<CancelEWayBillRequest>({
    reason: 'Duplicate'
  })

  // Load clients
  useEffect(() => {
    loadClients()
  }, [])

  // Load desk data when client selection changes
  useEffect(() => {
    loadData(selectedClientId || undefined)
  }, [selectedClientId])

  const loadClients = async () => {
    try {
      const res = await api<ClientPage>('/api/clients?pageSize=100')
      if (res && res.items) {
        setClients(res.items)
        if (res.items.length > 0 && !selectedClientId) {
          // Pre-populate default clientId in forms
          setInvoiceForm(f => ({ ...f, clientId: res.items[0].id }))
          setEwbForm(f => ({ ...f, clientId: res.items[0].id }))
        }
      }
    } catch {
      // Non-fatal if clients cannot be loaded immediately
    }
  }

  const loadData = async (cId?: string) => {
    setLoading(true)
    setError(null)
    try {
      const [sum, invList, ewbList] = await Promise.all([
        eInvoicingApi.getSummary(cId),
        eInvoicingApi.getInvoices(cId),
        eInvoicingApi.getEWayBills(cId)
      ])
      setSummary(sum)
      setInvoices(invList)
      setEwaybills(ewbList)
    } catch (e: any) {
      setError(e.message || 'Failed to load E-Invoicing data')
    } finally {
      setLoading(false)
    }
  }

  const handleOpenGenInvoice = () => {
    const curClient = clients.find(c => c.id === (selectedClientId || clients[0]?.id))
    const randomDocNo = 'INV/' + new Date().getFullYear() + '/' + Math.floor(1000 + Math.random() * 9000)
    setInvoiceForm({
      clientId: curClient?.id || '',
      invoiceId: null,
      docType: 1,
      docNumber: randomDocNo,
      docDate: new Date().toISOString().split('T')[0],
      financialYear: '2024-25',
      supplierGstin: curClient?.taxIds?.['GSTIN'] || '27AAAAA0000A1Z5',
      supplierTradeName: curClient?.name || 'Firm Registered Supplier',
      recipientGstin: '29ABCDE1234F1Z5',
      recipientLegalName: 'Acme Enterprise Solutions Pvt Ltd',
      supplyType: 1,
      totalTaxableValue: 100000,
      cgstAmount: 9000,
      sgstAmount: 9000,
      igstAmount: 0,
      cessAmount: 0,
      mainHsnCode: '998311',
      itemCount: 1
    })
    setShowGenInvoiceModal(true)
  }

  const handleOpenGenEwb = (fromInv?: EInvoiceDto) => {
    const curClient = clients.find(c => c.id === (fromInv?.clientId || selectedClientId || clients[0]?.id))
    const docNo = fromInv ? fromInv.docNumber : 'INV/' + new Date().getFullYear() + '/' + Math.floor(1000 + Math.random() * 9000)
    setEwbForm({
      clientId: fromInv?.clientId || curClient?.id || '',
      eInvoiceId: fromInv?.id || null,
      supplyType: 1,
      subSupplyType: 1,
      docType: fromInv ? 'INV' : 'INV',
      docNumber: docNo,
      docDate: fromInv ? fromInv.docDate : new Date().toISOString().split('T')[0],
      fromGstin: fromInv ? fromInv.supplierGstin : (curClient?.taxIds?.['GSTIN'] || '27AAAAA0000A1Z5'),
      fromTradeName: fromInv ? fromInv.supplierTradeName : (curClient?.name || 'Firm Goods Depot'),
      fromPincode: '400001',
      fromStateCode: 27,
      toGstin: fromInv ? fromInv.recipientGstin : '29ABCDE1234F1Z5',
      toTradeName: fromInv ? fromInv.recipientLegalName : 'Destination Logistics Hub',
      toPincode: '560001',
      toStateCode: 29,
      totalValue: fromInv ? fromInv.totalInvoiceValue : 118000,
      hsnCode: '847130',
      distanceKm: 980,
      transportMode: 1,
      vehicleType: 1,
      vehicleNumber: 'MH02AB1234',
      transporterId: '27TRANS1234A1Z9',
      transporterName: 'Express Cargo Logistics'
    })
    setShowGenEwbModal(true)
  }

  const handleSubmitInvoice = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      const created = await eInvoicingApi.generateInvoice(invoiceForm)
      setSuccess(`E-Invoice generated successfully! IRN: ${created.irn?.substring(0, 16)}...`)
      setShowGenInvoiceModal(false)
      loadData(selectedClientId || undefined)
    } catch (e: any) {
      setError(e.message || 'Failed to generate E-Invoice')
    }
  }

  const handleSubmitEwb = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setSuccess(null)
    try {
      const created = await eInvoicingApi.generateEWayBill(ewbForm)
      setSuccess(`E-Way Bill #${created.ewbNumber} generated! Valid until: ${new Date(created.validUntil).toLocaleString('en-IN')}`)
      setShowGenEwbModal(false)
      loadData(selectedClientId || undefined)
    } catch (e: any) {
      setError(e.message || 'Failed to generate E-Way Bill')
    }
  }

  const handleOpenUpdatePartB = (ewb: EWayBillDto) => {
    setSelectedEwb(ewb)
    setPartBForm({
      vehicleNumber: ewb.vehicleNumber || '',
      transporterId: ewb.transporterId || '',
      transporterName: ewb.transporterName || '',
      reason: 'Transshipment'
    })
    setShowUpdatePartBModal(true)
  }

  const handleSubmitPartB = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedEwb) return
    setError(null)
    setSuccess(null)
    try {
      await eInvoicingApi.updatePartB(selectedEwb.id, partBForm)
      setSuccess(`Part-B vehicle updated to ${partBForm.vehicleNumber} for E-Way Bill #${selectedEwb.ewbNumber}`)
      setShowUpdatePartBModal(false)
      loadData(selectedClientId || undefined)
    } catch (e: any) {
      setError(e.message || 'Failed to update Part-B vehicle')
    }
  }

  const handleOpenCancelInvoice = (inv: EInvoiceDto) => {
    setSelectedInvoice(inv)
    setCancelInvoiceForm({
      reason: '1: Duplicate',
      remarks: ''
    })
    setShowCancelInvoiceModal(true)
  }

  const handleSubmitCancelInvoice = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedInvoice) return
    setError(null)
    setSuccess(null)
    try {
      await eInvoicingApi.cancelInvoice(selectedInvoice.id, cancelInvoiceForm)
      setSuccess(`IRN cancelled successfully for invoice ${selectedInvoice.docNumber}`)
      setShowCancelInvoiceModal(false)
      loadData(selectedClientId || undefined)
    } catch (e: any) {
      setError(e.message || 'Failed to cancel E-Invoice')
    }
  }

  const handleOpenCancelEwb = (ewb: EWayBillDto) => {
    setSelectedEwb(ewb)
    setCancelEwbForm({
      reason: 'Duplicate'
    })
    setShowCancelEwbModal(true)
  }

  const handleSubmitCancelEwb = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedEwb) return
    setError(null)
    setSuccess(null)
    try {
      await eInvoicingApi.cancelEWayBill(selectedEwb.id, cancelEwbForm)
      setSuccess(`E-Way Bill #${selectedEwb.ewbNumber} cancelled successfully`)
      setShowCancelEwbModal(false)
      loadData(selectedClientId || undefined)
    } catch (e: any) {
      setError(e.message || 'Failed to cancel E-Way Bill')
    }
  }

  const handleOpenQrModal = (inv: EInvoiceDto) => {
    setSelectedInvoice(inv)
    setShowQrModal(true)
  }

  const copyToClipboard = (text: string, label: string) => {
    navigator.clipboard.writeText(text)
    setSuccess(`${label} copied to clipboard!`)
    setTimeout(() => setSuccess(null), 3000)
  }

  // Calculate estimated EWB validity preview
  const estimatedDays = Math.max(1, Math.ceil(ewbForm.distanceKm / (ewbForm.vehicleType === 2 ? 20 : 200)))

  return (
    <div style={{ padding: '24px', maxWidth: '1400px', margin: '0 auto' }}>
      {/* Top Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '26px', fontWeight: 700, color: '#1a237e' }}>
            Automated E-Invoicing & E-Way Bill Desk
          </h1>
          <p style={{ margin: '4px 0 0 0', color: '#555', fontSize: '14px' }}>
            Direct IRP integration, 64-character SHA-256 IRN generation, signed QR codes & Rule 138(10) E-Way Bill validity engine.
          </p>
        </div>

        <div style={{ display: 'flex', gap: '12px', alignItems: 'center', flexWrap: 'wrap' }}>
          {/* Client Filter */}
          <select
            value={selectedClientId}
            onChange={e => setSelectedClientId(e.target.value)}
            style={{
              padding: '8px 12px',
              borderRadius: '6px',
              border: '1px solid #ccc',
              fontSize: '14px',
              background: '#fff',
              minWidth: '200px'
            }}
          >
            <option value="">All Clients</option>
            {clients.map(c => (
              <option key={c.id} value={c.id}>
                {c.name} ({c.code})
              </option>
            ))}
          </select>

          <button
            onClick={() => loadData(selectedClientId || undefined)}
            disabled={loading}
            style={{
              padding: '8px 14px',
              borderRadius: '6px',
              border: '1px solid #1a237e',
              background: '#fff',
              color: '#1a237e',
              fontWeight: 600,
              cursor: 'pointer'
            }}
          >
            {loading ? 'Refreshing...' : '↻ Refresh'}
          </button>

          {(!can || can('einvoice.generate')) && (
            <>
              <button
                onClick={handleOpenGenInvoice}
                style={{
                  padding: '8px 16px',
                  borderRadius: '6px',
                  border: 'none',
                  background: '#1a237e',
                  color: '#fff',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px'
                }}
              >
                <span>+</span> Generate E-Invoice (IRN)
              </button>

              <button
                onClick={() => handleOpenGenEwb()}
                style={{
                  padding: '8px 16px',
                  borderRadius: '6px',
                  border: 'none',
                  background: '#0d47a1',
                  color: '#fff',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  gap: '6px'
                }}
              >
                <span>+</span> Generate E-Way Bill
              </button>
            </>
          )}
        </div>
      </div>

      {/* Notifications */}
      {error && (
        <div style={{
          padding: '12px 16px',
          background: '#ffebee',
          color: '#c62828',
          borderRadius: '6px',
          marginBottom: '20px',
          border: '1px solid #ffcdd2',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span><strong>Error:</strong> {error}</span>
          <button onClick={() => setError(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#c62828', fontWeight: 'bold' }}>✕</button>
        </div>
      )}

      {success && (
        <div style={{
          padding: '12px 16px',
          background: '#e8f5e9',
          color: '#2e7d32',
          borderRadius: '6px',
          marginBottom: '20px',
          border: '1px solid #c8e6c9',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center'
        }}>
          <span><strong>Success:</strong> {success}</span>
          <button onClick={() => setSuccess(null)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#2e7d32', fontWeight: 'bold' }}>✕</button>
        </div>
      )}

      {/* Metric Cards */}
      {summary && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #1a237e' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Total E-Invoices</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#1a237e', marginTop: '4px' }}>{summary.totalEInvoices}</div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              Active IRN: <strong style={{ color: '#2e7d32' }}>{summary.activeIrnCount}</strong> | Cancelled: <strong style={{ color: '#c62828' }}>{summary.cancelledIrnCount}</strong>
            </div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #2e7d32' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Active IRNs</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#2e7d32', marginTop: '4px' }}>{summary.activeIrnCount}</div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>Signed IRP QR Verified</div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #00838f' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>E-Invoiced Value</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#00838f', marginTop: '4px' }}>{formatCurrency(summary.totalEInvoicedValue)}</div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>Taxable + Statutory GST</div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #0277bd' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Active E-Way Bills</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: '#0277bd', marginTop: '4px' }}>{summary.activeEWayBills}</div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>Movement In Transit</div>
          </div>

          <div style={{
            background: '#fff',
            padding: '16px',
            borderRadius: '8px',
            boxShadow: '0 2px 4px rgba(0,0,0,0.05)',
            borderLeft: summary.expiringSoonEwbCount > 0 ? '4px solid #f57f17' : '4px solid #43a047'
          }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Expiring Soon (&lt; 24h)</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: summary.expiringSoonEwbCount > 0 ? '#f57f17' : '#43a047', marginTop: '4px' }}>
              {summary.expiringSoonEwbCount}
            </div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>
              {summary.expiringSoonEwbCount > 0 ? 'Action required: extend or deliver' : 'All EWBs in validity'}
            </div>
          </div>

          <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', borderLeft: '4px solid #4527a0' }}>
            <div style={{ fontSize: '12px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Total Consignment Value</div>
            <div style={{ fontSize: '22px', fontWeight: 700, color: '#4527a0', marginTop: '4px' }}>{formatCurrency(summary.totalEwbValue)}</div>
            <div style={{ fontSize: '11px', color: '#666', marginTop: '4px' }}>Active E-Way Bill Consignments</div>
          </div>
        </div>
      )}

      {/* Tabs */}
      <div style={{ display: 'flex', borderBottom: '2px solid #e0e0e0', marginBottom: '20px' }}>
        <button
          onClick={() => setActiveTab('invoices')}
          style={{
            padding: '12px 24px',
            fontSize: '15px',
            fontWeight: 600,
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'invoices' ? '3px solid #1a237e' : 'none',
            color: activeTab === 'invoices' ? '#1a237e' : '#666',
            cursor: 'pointer'
          }}
        >
          📄 E-Invoices & IRN Register ({invoices.length})
        </button>
        <button
          onClick={() => setActiveTab('ewaybills')}
          style={{
            padding: '12px 24px',
            fontSize: '15px',
            fontWeight: 600,
            background: 'none',
            border: 'none',
            borderBottom: activeTab === 'ewaybills' ? '3px solid #1a237e' : 'none',
            color: activeTab === 'ewaybills' ? '#1a237e' : '#666',
            cursor: 'pointer'
          }}
        >
          🚚 E-Way Bills (Part A & B) ({ewaybills.length})
        </button>
      </div>

      {/* Tab 1: E-Invoices & IRN Register */}
      {activeTab === 'invoices' && (
        <div style={{ background: '#fff', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid #eee', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>Registered E-Invoices</h3>
            <span style={{ fontSize: '12px', color: '#666' }}>Showing {invoices.length} e-invoice records</span>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f8f9fa', color: '#555', borderBottom: '2px solid #eee' }}>
                  <th style={{ padding: '12px' }}>Doc Details</th>
                  <th style={{ padding: '12px' }}>Client & Supplier</th>
                  <th style={{ padding: '12px' }}>Recipient</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Taxable Value</th>
                  <th style={{ padding: '12px', textAlign: 'right' }}>Total Invoice</th>
                  <th style={{ padding: '12px' }}>IRN & Ack</th>
                  <th style={{ padding: '12px' }}>Status</th>
                  <th style={{ padding: '12px', textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {invoices.length === 0 ? (
                  <tr>
                    <td colSpan={8} style={{ padding: '32px', textAlign: 'center', color: '#999' }}>
                      No E-Invoices found. Click "+ Generate E-Invoice (IRN)" to register your first IRP invoice.
                    </td>
                  </tr>
                ) : (
                  invoices.map(inv => {
                    const statusStr = formatInvoiceStatus(inv.status)
                    const isGenerated = statusStr === 'Generated'
                    const isCancelled = statusStr === 'Cancelled'
                    return (
                      <tr key={inv.id} style={{ borderBottom: '1px solid #eee', background: isCancelled ? '#fff9f9' : '#fff' }}>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 600, color: '#1a237e' }}>{inv.docNumber}</div>
                          <div style={{ fontSize: '11px', color: '#666' }}>
                            {formatDocType(inv.docType)} | {inv.docDate}
                          </div>
                          <div style={{ fontSize: '10px', color: '#888' }}>FY: {inv.financialYear}</div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 600 }}>{inv.clientName}</div>
                          <div style={{ fontSize: '11px', fontFamily: 'monospace', color: '#555' }}>
                            GSTIN: {inv.supplierGstin}
                          </div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 600 }}>{inv.recipientLegalName}</div>
                          <div style={{ fontSize: '11px', fontFamily: 'monospace', color: '#555' }}>
                            GSTIN: {inv.recipientGstin}
                          </div>
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 600 }}>
                          {formatCurrency(inv.totalTaxableValue)}
                          <div style={{ fontSize: '11px', color: '#666' }}>
                            CGST+SGST: {formatCurrency(inv.cgstAmount + inv.sgstAmount)}
                          </div>
                        </td>
                        <td style={{ padding: '12px', textAlign: 'right', fontWeight: 700, color: '#1a237e' }}>
                          {formatCurrency(inv.totalInvoiceValue)}
                        </td>
                        <td style={{ padding: '12px' }}>
                          {inv.irn ? (
                            <div>
                              <div style={{ display: 'flex', alignItems: 'center', gap: '4px' }}>
                                <span style={{ fontFamily: 'monospace', fontSize: '11px', color: '#333' }}>
                                  {inv.irn.substring(0, 14)}...{inv.irn.substring(inv.irn.length - 6)}
                                </span>
                                <button
                                  onClick={() => copyToClipboard(inv.irn!, 'IRN Hash')}
                                  title="Copy 64-character IRN"
                                  style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: '11px', color: '#1a237e' }}
                                >
                                  📋
                                </button>
                              </div>
                              <div style={{ fontSize: '10px', color: '#666' }}>
                                Ack #{inv.ackNo || '-'}
                              </div>
                            </div>
                          ) : (
                            <span style={{ color: '#999', fontSize: '12px' }}>Pending</span>
                          )}
                        </td>
                        <td style={{ padding: '12px' }}>
                          <span style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 700,
                            background: isGenerated ? '#e8f5e9' : isCancelled ? '#ffebee' : '#fff8e1',
                            color: isGenerated ? '#2e7d32' : isCancelled ? '#c62828' : '#f57f17'
                          }}>
                            {statusStr.toUpperCase()}
                          </span>
                          {isCancelled && inv.cancelReason && (
                            <div style={{ fontSize: '10px', color: '#c62828', marginTop: '2px' }}>
                              {inv.cancelReason}
                            </div>
                          )}
                        </td>
                        <td style={{ padding: '12px', textAlign: 'center' }}>
                          <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                            <button
                              onClick={() => handleOpenQrModal(inv)}
                              title="View Signed QR & Payload Details"
                              style={{
                                padding: '4px 8px',
                                borderRadius: '4px',
                                border: '1px solid #1a237e',
                                background: '#e8eaf6',
                                color: '#1a237e',
                                fontSize: '11px',
                                fontWeight: 600,
                                cursor: 'pointer'
                              }}
                            >
                              QR Details
                            </button>

                            {isGenerated && (!can || can('einvoice.generate')) && (
                              <button
                                onClick={() => handleOpenGenEwb(inv)}
                                title="Create E-Way Bill for this E-Invoice"
                                style={{
                                  padding: '4px 8px',
                                  borderRadius: '4px',
                                  border: '1px solid #00796b',
                                  background: '#e0f2f1',
                                  color: '#00796b',
                                  fontSize: '11px',
                                  fontWeight: 600,
                                  cursor: 'pointer'
                                }}
                              >
                                + EWB
                              </button>
                            )}

                            {isGenerated && (!can || can('einvoice.cancel')) && (
                              <button
                                onClick={() => handleOpenCancelInvoice(inv)}
                                title="Cancel IRN within 24 hours statutory window"
                                style={{
                                  padding: '4px 8px',
                                  borderRadius: '4px',
                                  border: '1px solid #d32f2f',
                                  background: '#ffebee',
                                  color: '#d32f2f',
                                  fontSize: '11px',
                                  fontWeight: 600,
                                  cursor: 'pointer'
                                }}
                              >
                                Cancel
                              </button>
                            )}
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
      )}

      {/* Tab 2: E-Way Bills (Part A & B) */}
      {activeTab === 'ewaybills' && (
        <div style={{ background: '#fff', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)', overflow: 'hidden' }}>
          <div style={{ padding: '16px', borderBottom: '1px solid #eee', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 600 }}>E-Way Bill Transit Register</h3>
              <p style={{ margin: '2px 0 0 0', fontSize: '12px', color: '#666' }}>
                Rule 138(10): Validity is calculated dynamically (1 day per 200 km regular / 20 km ODC)
              </p>
            </div>
            <span style={{ fontSize: '12px', color: '#666' }}>Showing {ewaybills.length} e-way bill records</span>
          </div>

          <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px', textAlign: 'left' }}>
              <thead>
                <tr style={{ background: '#f8f9fa', color: '#555', borderBottom: '2px solid #eee' }}>
                  <th style={{ padding: '12px' }}>EWB Number</th>
                  <th style={{ padding: '12px' }}>Origin & Destination</th>
                  <th style={{ padding: '12px' }}>Distance & Goods</th>
                  <th style={{ padding: '12px' }}>Vehicle (Part-B)</th>
                  <th style={{ padding: '12px' }}>Validity & Countdown</th>
                  <th style={{ padding: '12px' }}>Status</th>
                  <th style={{ padding: '12px', textAlign: 'center' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {ewaybills.length === 0 ? (
                  <tr>
                    <td colSpan={7} style={{ padding: '32px', textAlign: 'center', color: '#999' }}>
                      No E-Way Bills found. Click "+ Generate E-Way Bill" to generate goods transit authorization.
                    </td>
                  </tr>
                ) : (
                  ewaybills.map(ewb => {
                    const statusStr = formatEwbStatus(ewb.status)
                    const isActive = statusStr === 'Active'
                    const isExpired = statusStr === 'Expired' || ewb.remainingHours <= 0
                    const isExpiringSoon = isActive && ewb.remainingHours > 0 && ewb.remainingHours <= 24

                    return (
                      <tr key={ewb.id} style={{ borderBottom: '1px solid #eee' }}>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 700, fontFamily: 'monospace', color: '#0d47a1', fontSize: '14px' }}>
                            {ewb.ewbNumber}
                          </div>
                          <div style={{ fontSize: '11px', color: '#666' }}>
                            Doc #{ewb.docNumber} ({ewb.docType})
                          </div>
                          <div style={{ fontSize: '10px', color: '#888' }}>
                            Generated: {new Date(ewb.ewbDate).toLocaleDateString('en-IN')}
                          </div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 600 }}>{ewb.fromTradeName} ({ewb.fromPincode})</div>
                          <div style={{ fontSize: '11px', color: '#555' }}>➔ {ewb.toTradeName} ({ewb.toPincode})</div>
                          <div style={{ fontSize: '10px', color: '#777' }}>
                            State {ewb.fromStateCode} ➔ State {ewb.toStateCode}
                          </div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontWeight: 600 }}>{ewb.distanceKm} km</div>
                          <div style={{ fontSize: '11px', color: '#666' }}>HSN: {ewb.hsnCode}</div>
                          <div style={{ fontSize: '11px', fontWeight: 600, color: '#1a237e' }}>
                            Value: {formatCurrency(ewb.totalValue)}
                          </div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          {ewb.vehicleNumber ? (
                            <div>
                              <span style={{
                                padding: '2px 6px',
                                borderRadius: '4px',
                                background: '#e8f5e9',
                                color: '#2e7d32',
                                fontWeight: 700,
                                fontFamily: 'monospace',
                                fontSize: '12px'
                              }}>
                                🚚 {ewb.vehicleNumber}
                              </span>
                              <div style={{ fontSize: '10px', color: '#666', marginTop: '2px' }}>
                                {ewb.transporterName || 'Self Transporter'}
                              </div>
                            </div>
                          ) : (
                            <span style={{
                              padding: '2px 6px',
                              borderRadius: '4px',
                              background: '#ffebee',
                              color: '#c62828',
                              fontWeight: 600,
                              fontSize: '11px'
                            }}>
                              Part-B Missing
                            </span>
                          )}
                        </td>
                        <td style={{ padding: '12px' }}>
                          <div style={{ fontSize: '12px', fontWeight: 600 }}>
                            {new Date(ewb.validUntil).toLocaleString('en-IN')}
                          </div>
                          <div>
                            {isExpired ? (
                              <span style={{
                                display: 'inline-block',
                                padding: '2px 6px',
                                borderRadius: '4px',
                                fontSize: '10px',
                                fontWeight: 700,
                                background: '#ffebee',
                                color: '#c62828',
                                marginTop: '2px'
                              }}>
                                ⚠️ EXPIRED
                              </span>
                            ) : isExpiringSoon ? (
                              <span style={{
                                display: 'inline-block',
                                padding: '2px 6px',
                                borderRadius: '4px',
                                fontSize: '10px',
                                fontWeight: 700,
                                background: '#fff3e0',
                                color: '#e65100',
                                marginTop: '2px'
                              }}>
                                ⏳ {ewb.remainingHours} hrs remaining
                              </span>
                            ) : (
                              <span style={{
                                display: 'inline-block',
                                padding: '2px 6px',
                                borderRadius: '4px',
                                fontSize: '10px',
                                fontWeight: 600,
                                background: '#e8f5e9',
                                color: '#2e7d32',
                                marginTop: '2px'
                              }}>
                                ✓ {ewb.remainingHours} hrs valid
                              </span>
                            )}
                          </div>
                        </td>
                        <td style={{ padding: '12px' }}>
                          <span style={{
                            padding: '3px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 700,
                            background: isActive ? '#e8f5e9' : isExpired ? '#ffebee' : '#f5f5f5',
                            color: isActive ? '#2e7d32' : isExpired ? '#c62828' : '#666'
                          }}>
                            {statusStr.toUpperCase()}
                          </span>
                        </td>
                        <td style={{ padding: '12px', textAlign: 'center' }}>
                          <div style={{ display: 'flex', gap: '6px', justifyContent: 'center' }}>
                            {isActive && (!can || can('einvoice.generate')) && (
                              <button
                                onClick={() => handleOpenUpdatePartB(ewb)}
                                title="Update vehicle number (breakdown or transshipment)"
                                style={{
                                  padding: '4px 8px',
                                  borderRadius: '4px',
                                  border: '1px solid #1a237e',
                                  background: '#e8eaf6',
                                  color: '#1a237e',
                                  fontSize: '11px',
                                  fontWeight: 600,
                                  cursor: 'pointer'
                                }}
                              >
                                Update Vehicle
                              </button>
                            )}

                            {isActive && (!can || can('einvoice.cancel')) && (
                              <button
                                onClick={() => handleOpenCancelEwb(ewb)}
                                title="Cancel E-Way Bill"
                                style={{
                                  padding: '4px 8px',
                                  borderRadius: '4px',
                                  border: '1px solid #d32f2f',
                                  background: '#ffebee',
                                  color: '#d32f2f',
                                  fontSize: '11px',
                                  fontWeight: 600,
                                  cursor: 'pointer'
                                }}
                              >
                                Cancel
                              </button>
                            )}
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
      )}

      {/* MODAL 1: Generate E-Invoice */}
      {showGenInvoiceModal && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '650px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <h2 style={{ margin: 0, fontSize: '18px', color: '#1a237e' }}>Generate IRP E-Invoice & IRN</h2>
              <button onClick={() => setShowGenInvoiceModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSubmitInvoice}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Client</label>
                  <select
                    value={invoiceForm.clientId}
                    onChange={e => {
                      const c = clients.find(item => item.id === e.target.value)
                      setInvoiceForm({
                        ...invoiceForm,
                        clientId: e.target.value,
                        supplierGstin: c?.taxIds?.['GSTIN'] || invoiceForm.supplierGstin,
                        supplierTradeName: c?.name || invoiceForm.supplierTradeName
                      })
                    }}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Doc Type</label>
                  <select
                    value={invoiceForm.docType}
                    onChange={e => setInvoiceForm({ ...invoiceForm, docType: Number(e.target.value) as any })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value={1}>INV - Tax Invoice</option>
                    <option value={2}>CRN - Credit Note</option>
                    <option value={3}>DBN - Debit Note</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Doc Number</label>
                  <input
                    type="text"
                    value={invoiceForm.docNumber}
                    onChange={e => setInvoiceForm({ ...invoiceForm, docNumber: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Doc Date</label>
                  <input
                    type="date"
                    value={invoiceForm.docDate}
                    onChange={e => setInvoiceForm({ ...invoiceForm, docDate: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Financial Year</label>
                  <input
                    type="text"
                    value={invoiceForm.financialYear}
                    onChange={e => setInvoiceForm({ ...invoiceForm, financialYear: e.target.value })}
                    placeholder="2024-25"
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Supply Type</label>
                  <select
                    value={invoiceForm.supplyType}
                    onChange={e => setInvoiceForm({ ...invoiceForm, supplyType: Number(e.target.value) as any })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value={1}>B2B (Regular)</option>
                    <option value={2}>SEZWP (SEZ with payment)</option>
                    <option value={3}>SEZWOP (SEZ without payment)</option>
                    <option value={4}>EXPWP (Export with payment)</option>
                    <option value={5}>EXPWOP (Export without payment)</option>
                    <option value={6}>DEXP (Deemed Export)</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Supplier GSTIN</label>
                  <input
                    type="text"
                    value={invoiceForm.supplierGstin}
                    onChange={e => setInvoiceForm({ ...invoiceForm, supplierGstin: e.target.value.toUpperCase() })}
                    required
                    maxLength={15}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontFamily: 'monospace' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Supplier Trade Name</label>
                  <input
                    type="text"
                    value={invoiceForm.supplierTradeName}
                    onChange={e => setInvoiceForm({ ...invoiceForm, supplierTradeName: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Recipient GSTIN</label>
                  <input
                    type="text"
                    value={invoiceForm.recipientGstin}
                    onChange={e => setInvoiceForm({ ...invoiceForm, recipientGstin: e.target.value.toUpperCase() })}
                    required
                    maxLength={15}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontFamily: 'monospace' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Recipient Legal Name</label>
                  <input
                    type="text"
                    value={invoiceForm.recipientLegalName}
                    onChange={e => setInvoiceForm({ ...invoiceForm, recipientLegalName: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Main HSN Code</label>
                  <input
                    type="text"
                    value={invoiceForm.mainHsnCode}
                    onChange={e => setInvoiceForm({ ...invoiceForm, mainHsnCode: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Item Count</label>
                  <input
                    type="number"
                    min={1}
                    value={invoiceForm.itemCount}
                    onChange={e => setInvoiceForm({ ...invoiceForm, itemCount: parseInt(e.target.value) || 1 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Taxable Value (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={invoiceForm.totalTaxableValue}
                    onChange={e => {
                      const val = parseFloat(e.target.value) || 0
                      // Auto populate 9% CGST and 9% SGST for convenience
                      setInvoiceForm({
                        ...invoiceForm,
                        totalTaxableValue: val,
                        cgstAmount: Math.round(val * 0.09 * 100) / 100,
                        sgstAmount: Math.round(val * 0.09 * 100) / 100,
                        igstAmount: 0
                      })
                    }}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>CGST Amount (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={invoiceForm.cgstAmount}
                    onChange={e => setInvoiceForm({ ...invoiceForm, cgstAmount: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>SGST Amount (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={invoiceForm.sgstAmount}
                    onChange={e => setInvoiceForm({ ...invoiceForm, sgstAmount: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>IGST Amount (₹)</label>
                  <input
                    type="number"
                    step="0.01"
                    value={invoiceForm.igstAmount}
                    onChange={e => setInvoiceForm({ ...invoiceForm, igstAmount: parseFloat(e.target.value) || 0 })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              {/* Total Summary */}
              <div style={{ background: '#f5f5f5', padding: '12px', borderRadius: '6px', marginBottom: '16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span style={{ fontSize: '14px', fontWeight: 600 }}>Calculated Total Invoice Value:</span>
                <span style={{ fontSize: '18px', fontWeight: 700, color: '#1a237e' }}>
                  {formatCurrency(
                    Number(invoiceForm.totalTaxableValue) +
                    Number(invoiceForm.cgstAmount) +
                    Number(invoiceForm.sgstAmount) +
                    Number(invoiceForm.igstAmount) +
                    Number(invoiceForm.cessAmount)
                  )}
                </span>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowGenInvoiceModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#1a237e', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Submitting to IRP...' : 'Generate IRN'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 2: Generate E-Way Bill */}
      {showGenEwbModal && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '700px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#0d47a1' }}>Generate E-Way Bill (Part A & B)</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>Automated validity based on Rule 138(10)</div>
              </div>
              <button onClick={() => setShowGenEwbModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSubmitEwb}>
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px', marginBottom: '14px' }}>
                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Client</label>
                  <select
                    value={ewbForm.clientId}
                    onChange={e => {
                      const c = clients.find(item => item.id === e.target.value)
                      setEwbForm({
                        ...ewbForm,
                        clientId: e.target.value,
                        fromGstin: c?.taxIds?.['GSTIN'] || ewbForm.fromGstin,
                        fromTradeName: c?.name || ewbForm.fromTradeName
                      })
                    }}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    {clients.map(c => (
                      <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                    ))}
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Sub-Supply Type</label>
                  <select
                    value={ewbForm.subSupplyType}
                    onChange={e => setEwbForm({ ...ewbForm, subSupplyType: Number(e.target.value) as any })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value={1}>Supply (Regular)</option>
                    <option value={2}>Export</option>
                    <option value={3}>Job Work</option>
                    <option value={4}>SKD / CKD</option>
                    <option value={5}>Line Sale</option>
                    <option value={6}>Exhibition</option>
                    <option value={99}>Others</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Doc Number</label>
                  <input
                    type="text"
                    value={ewbForm.docNumber}
                    onChange={e => setEwbForm({ ...ewbForm, docNumber: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Doc Date</label>
                  <input
                    type="date"
                    value={ewbForm.docDate}
                    onChange={e => setEwbForm({ ...ewbForm, docDate: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Origin Pincode & State</label>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <input
                      type="text"
                      placeholder="Pincode (e.g. 400001)"
                      value={ewbForm.fromPincode}
                      onChange={e => setEwbForm({ ...ewbForm, fromPincode: e.target.value })}
                      required
                      maxLength={6}
                      style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                    <input
                      type="number"
                      placeholder="State (27)"
                      value={ewbForm.fromStateCode}
                      onChange={e => setEwbForm({ ...ewbForm, fromStateCode: parseInt(e.target.value) || 27 })}
                      required
                      style={{ width: '80px', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                  </div>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Destination Pincode & State</label>
                  <div style={{ display: 'flex', gap: '8px' }}>
                    <input
                      type="text"
                      placeholder="Pincode (e.g. 560001)"
                      value={ewbForm.toPincode}
                      onChange={e => setEwbForm({ ...ewbForm, toPincode: e.target.value })}
                      required
                      maxLength={6}
                      style={{ flex: 1, padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                    <input
                      type="number"
                      placeholder="State (29)"
                      value={ewbForm.toStateCode}
                      onChange={e => setEwbForm({ ...ewbForm, toStateCode: parseInt(e.target.value) || 29 })}
                      required
                      style={{ width: '80px', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                    />
                  </div>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>From Trade Name</label>
                  <input
                    type="text"
                    value={ewbForm.fromTradeName}
                    onChange={e => setEwbForm({ ...ewbForm, fromTradeName: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>To Trade Name</label>
                  <input
                    type="text"
                    value={ewbForm.toTradeName}
                    onChange={e => setEwbForm({ ...ewbForm, toTradeName: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Consignment Total Value (₹)</label>
                  <input
                    type="number"
                    value={ewbForm.totalValue}
                    onChange={e => setEwbForm({ ...ewbForm, totalValue: parseFloat(e.target.value) || 0 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>HSN Code</label>
                  <input
                    type="text"
                    value={ewbForm.hsnCode}
                    onChange={e => setEwbForm({ ...ewbForm, hsnCode: e.target.value })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Distance in KM</label>
                  <input
                    type="number"
                    min={1}
                    value={ewbForm.distanceKm}
                    onChange={e => setEwbForm({ ...ewbForm, distanceKm: parseInt(e.target.value) || 1 })}
                    required
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Vehicle Type</label>
                  <select
                    value={ewbForm.vehicleType}
                    onChange={e => setEwbForm({ ...ewbForm, vehicleType: Number(e.target.value) as any })}
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  >
                    <option value={1}>Regular Cargo (1 day / 200 km)</option>
                    <option value={2}>Over Dimensional Cargo (ODC) (1 day / 20 km)</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Vehicle Number (Part-B)</label>
                  <input
                    type="text"
                    value={ewbForm.vehicleNumber || ''}
                    onChange={e => setEwbForm({ ...ewbForm, vehicleNumber: e.target.value.toUpperCase() })}
                    placeholder="MH02AB1234"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontFamily: 'monospace' }}
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Transporter Name / ID</label>
                  <input
                    type="text"
                    value={ewbForm.transporterName || ''}
                    onChange={e => setEwbForm({ ...ewbForm, transporterName: e.target.value })}
                    placeholder="Express Cargo Logistics"
                    style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                  />
                </div>
              </div>

              {/* Statutory Rule 138(10) Preview Alert */}
              <div style={{ background: '#e3f2fd', padding: '12px', borderRadius: '6px', marginBottom: '16px', borderLeft: '4px solid #1565c0' }}>
                <div style={{ fontSize: '13px', fontWeight: 600, color: '#0d47a1' }}>
                  Rule 138(10) Statutory Validity Preview:
                </div>
                <div style={{ fontSize: '12px', color: '#333', marginTop: '2px' }}>
                  Distance: <strong>{ewbForm.distanceKm} km</strong> | Vehicle: <strong>{ewbForm.vehicleType === 2 ? 'ODC' : 'Regular'}</strong> ➔
                  Validity will be <strong>{estimatedDays} day{estimatedDays > 1 ? 's' : ''}</strong> from issuance date.
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowGenEwbModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#0d47a1', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Generating EWB...' : 'Generate E-Way Bill'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 3: Update Part-B Vehicle */}
      {showUpdatePartBModal && selectedEwb && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '480px',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#1a237e' }}>Update Part-B Vehicle</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>EWB #{selectedEwb.ewbNumber}</div>
              </div>
              <button onClick={() => setShowUpdatePartBModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSubmitPartB}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>New Vehicle Number</label>
                <input
                  type="text"
                  value={partBForm.vehicleNumber}
                  onChange={e => setPartBForm({ ...partBForm, vehicleNumber: e.target.value.toUpperCase() })}
                  required
                  placeholder="e.g. DL01AB5678"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc', fontFamily: 'monospace' }}
                />
              </div>

              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Transporter Name / ID</label>
                <input
                  type="text"
                  value={partBForm.transporterName || ''}
                  onChange={e => setPartBForm({ ...partBForm, transporterName: e.target.value })}
                  placeholder="Transporter Name"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Reason for Change</label>
                <select
                  value={partBForm.reason}
                  onChange={e => setPartBForm({ ...partBForm, reason: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="Transshipment">Transshipment (Hub transfer)</option>
                  <option value="Vehicle Break down">Vehicle Breakdown</option>
                  <option value="Others">Others</option>
                </select>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowUpdatePartBModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#1a237e', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Updating...' : 'Update Part-B'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 4: Cancel E-Invoice Modal */}
      {showCancelInvoiceModal && selectedInvoice && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '500px',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#c62828' }}>Cancel E-Invoice (IRP)</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>Invoice #{selectedInvoice.docNumber}</div>
              </div>
              <button onClick={() => setShowCancelInvoiceModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            {/* Statutory Advisory Notice */}
            <div style={{ background: '#fff3e0', padding: '12px', borderRadius: '6px', marginBottom: '16px', borderLeft: '4px solid #ef6c00' }}>
              <div style={{ fontSize: '12px', fontWeight: 700, color: '#e65100' }}>
                ⚠️ Statutory 24-Hour Cancellation Limit
              </div>
              <div style={{ fontSize: '11px', color: '#555', marginTop: '2px' }}>
                According to GST E-Invoicing rules, an IRN can only be cancelled within 24 hours of generation on the IRP.
                After 24 hours, cancellation is blocked and a Credit Note (CRN) must be issued instead.
              </div>
            </div>

            <form onSubmit={handleSubmitCancelInvoice}>
              <div style={{ marginBottom: '14px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Cancellation Reason</label>
                <select
                  value={cancelInvoiceForm.reason}
                  onChange={e => setCancelInvoiceForm({ ...cancelInvoiceForm, reason: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="1: Duplicate">1: Duplicate</option>
                  <option value="2: Data Entry Mistake">2: Data Entry Mistake</option>
                  <option value="3: Order Cancelled">3: Order Cancelled</option>
                  <option value="4: Others">4: Others</option>
                </select>
              </div>

              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Remarks</label>
                <input
                  type="text"
                  value={cancelInvoiceForm.remarks || ''}
                  onChange={e => setCancelInvoiceForm({ ...cancelInvoiceForm, remarks: e.target.value })}
                  placeholder="Optional explanatory remarks"
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowCancelInvoiceModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Close
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#c62828', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Cancelling on IRP...' : 'Confirm Cancellation'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 5: Cancel E-Way Bill Modal */}
      {showCancelEwbModal && selectedEwb && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '480px',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#c62828' }}>Cancel E-Way Bill</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>EWB #{selectedEwb.ewbNumber}</div>
              </div>
              <button onClick={() => setShowCancelEwbModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <form onSubmit={handleSubmitCancelEwb}>
              <div style={{ marginBottom: '16px' }}>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 600, marginBottom: '4px' }}>Reason</label>
                <select
                  value={cancelEwbForm.reason}
                  onChange={e => setCancelEwbForm({ ...cancelEwbForm, reason: e.target.value })}
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                >
                  <option value="Duplicate">Duplicate E-Way Bill</option>
                  <option value="Order Cancelled">Order Cancelled</option>
                  <option value="Data Entry Mistake">Data Entry Mistake</option>
                </select>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                <button
                  type="button"
                  onClick={() => setShowCancelEwbModal(false)}
                  style={{ padding: '8px 16px', borderRadius: '4px', border: '1px solid #ccc', background: '#fff', cursor: 'pointer' }}
                >
                  Close
                </button>
                <button
                  type="submit"
                  disabled={loading}
                  style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#c62828', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
                >
                  {loading ? 'Cancelling...' : 'Cancel EWB'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* MODAL 6: View IRN & Signed QR Code Details */}
      {showQrModal && selectedInvoice && (
        <div style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(0,0,0,0.5)',
          display: 'flex',
          justifyContent: 'center',
          alignItems: 'center',
          zIndex: 1000,
          padding: '20px'
        }}>
          <div style={{
            background: '#fff',
            borderRadius: '8px',
            width: '100%',
            maxWidth: '650px',
            maxHeight: '90vh',
            overflowY: 'auto',
            padding: '24px',
            boxShadow: '0 4px 20px rgba(0,0,0,0.15)'
          }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px', borderBottom: '1px solid #eee', paddingBottom: '12px' }}>
              <div>
                <h2 style={{ margin: 0, fontSize: '18px', color: '#1a237e' }}>IRP Verification & Signed QR</h2>
                <div style={{ fontSize: '12px', color: '#666' }}>Invoice #{selectedInvoice.docNumber}</div>
              </div>
              <button onClick={() => setShowQrModal(false)} style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#666' }}>✕</button>
            </div>

            <div style={{ display: 'flex', gap: '24px', alignItems: 'flex-start', flexWrap: 'wrap' }}>
              {/* QR Visual */}
              <div style={{
                background: '#f8f9fa',
                border: '2px dashed #9fa8da',
                borderRadius: '8px',
                padding: '16px',
                textAlign: 'center',
                width: '180px',
                flexShrink: 0
              }}>
                <svg width="140" height="140" viewBox="0 0 100 100">
                  <rect width="100" height="100" fill="#ffffff" rx="4" />
                  {/* Outer corner squares */}
                  <rect x="10" y="10" width="25" height="25" fill="#1a237e" />
                  <rect x="14" y="14" width="17" height="17" fill="#ffffff" />
                  <rect x="17" y="17" width="11" height="11" fill="#1a237e" />

                  <rect x="65" y="10" width="25" height="25" fill="#1a237e" />
                  <rect x="69" y="14" width="17" height="17" fill="#ffffff" />
                  <rect x="72" y="17" width="11" height="11" fill="#1a237e" />

                  <rect x="10" y="65" width="25" height="25" fill="#1a237e" />
                  <rect x="14" y="69" width="17" height="17" fill="#ffffff" />
                  <rect x="17" y="72" width="11" height="11" fill="#1a237e" />

                  {/* Simulated QR bits */}
                  <rect x="40" y="12" width="6" height="6" fill="#1a237e" />
                  <rect x="52" y="12" width="6" height="6" fill="#1a237e" />
                  <rect x="42" y="24" width="12" height="6" fill="#1a237e" />
                  <rect x="42" y="40" width="16" height="16" fill="#1a237e" />
                  <rect x="12" y="42" width="6" height="14" fill="#1a237e" />
                  <rect x="24" y="46" width="10" height="6" fill="#1a237e" />
                  <rect x="66" y="42" width="10" height="14" fill="#1a237e" />
                  <rect x="80" y="46" width="8" height="8" fill="#1a237e" />
                  <rect x="42" y="66" width="8" height="18" fill="#1a237e" />
                  <rect x="66" y="66" width="18" height="8" fill="#1a237e" />
                  <rect x="74" y="78" width="14" height="10" fill="#1a237e" />
                </svg>
                <div style={{ fontSize: '11px', fontWeight: 600, color: '#1a237e', marginTop: '8px' }}>
                  Signed QR Code
                </div>
                <div style={{ fontSize: '10px', color: '#666' }}>
                  IRP Digital Signature Verified
                </div>
              </div>

              {/* Data Fields */}
              <div style={{ flex: 1, minWidth: '260px' }}>
                <div style={{ marginBottom: '12px' }}>
                  <label style={{ display: 'block', fontSize: '11px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>
                    Invoice Reference Number (IRN) (SHA-256):
                  </label>
                  <div style={{
                    background: '#f5f5f5',
                    padding: '8px',
                    borderRadius: '4px',
                    fontFamily: 'monospace',
                    fontSize: '11px',
                    wordBreak: 'break-all',
                    color: '#1a237e',
                    border: '1px solid #ddd',
                    marginTop: '4px'
                  }}>
                    {selectedInvoice.irn || 'None'}
                  </div>
                  {selectedInvoice.irn && (
                    <button
                      onClick={() => copyToClipboard(selectedInvoice.irn!, 'Full IRN Hash')}
                      style={{
                        marginTop: '6px',
                        padding: '4px 10px',
                        borderRadius: '4px',
                        border: '1px solid #1a237e',
                        background: '#fff',
                        color: '#1a237e',
                        fontSize: '11px',
                        fontWeight: 600,
                        cursor: 'pointer'
                      }}
                    >
                      📋 Copy Full 64-char IRN
                    </button>
                  )}
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', marginBottom: '12px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '10px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>IRP Ack Number</label>
                    <div style={{ fontFamily: 'monospace', fontWeight: 600 }}>{selectedInvoice.ackNo || '-'}</div>
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '10px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>IRP Ack Timestamp</label>
                    <div style={{ fontSize: '12px' }}>{selectedInvoice.ackDate ? new Date(selectedInvoice.ackDate).toLocaleString('en-IN') : '-'}</div>
                  </div>
                </div>

                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '8px', marginBottom: '12px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '10px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Supplier GSTIN</label>
                    <div style={{ fontFamily: 'monospace', fontSize: '12px' }}>{selectedInvoice.supplierGstin}</div>
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '10px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Recipient GSTIN</label>
                    <div style={{ fontFamily: 'monospace', fontSize: '12px' }}>{selectedInvoice.recipientGstin}</div>
                  </div>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '11px', color: '#666', textTransform: 'uppercase', fontWeight: 600 }}>Signed QR Payload (JWT)</label>
                  <textarea
                    readOnly
                    value={selectedInvoice.signedQrCode || ''}
                    rows={3}
                    style={{
                      width: '100%',
                      padding: '6px',
                      borderRadius: '4px',
                      border: '1px solid #ddd',
                      fontFamily: 'monospace',
                      fontSize: '10px',
                      background: '#fafafa',
                      resize: 'none'
                    }}
                  />
                </div>
              </div>
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '20px' }}>
              <button
                onClick={() => setShowQrModal(false)}
                style={{ padding: '8px 20px', borderRadius: '4px', border: 'none', background: '#1a237e', color: '#fff', fontWeight: 600, cursor: 'pointer' }}
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
