import { useCallback, useEffect, useState } from 'react'
import {
  type GstReconciliationBatchDto,
  type GstReconciliationItemDto,
  type GstReconciliationSummaryDto,
  type Client,
  type PurchaseRegisterInvoice,
  type Gstr2BInvoice,
  type ClientPage,
  gstApi,
  api
} from './api'

const fmtInr = (n: number) =>
  new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', maximumFractionDigits: 0 }).format(n)

export function GstReconciliationPage() {
  const [batches, setBatches] = useState<GstReconciliationBatchDto[]>([])
  const [summary, setSummary] = useState<GstReconciliationSummaryDto | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [selectedBatch, setSelectedBatch] = useState<GstReconciliationBatchDto | null>(null)
  const [items, setItems] = useState<GstReconciliationItemDto[]>([])
  const [itemFilter, setItemFilter] = useState('all')
  const [loadingItems, setLoadingItems] = useState(false)

  // Modal
  const [showModal, setShowModal] = useState(false)
  const [running, setRunning] = useState(false)

  // Form state
  const [formClientId, setFormClientId] = useState('')
  const [formPeriod, setFormPeriod] = useState('042024')
  const [formFy, setFormFy] = useState('2024-25')
  const [formTolerance, setFormTolerance] = useState(1.0)
  const [formNotes, setFormNotes] = useState('April 2024 monthly GSTR-2B purchase reconciliation')

  const [prInvoices, setPrInvoices] = useState<PurchaseRegisterInvoice[]>([])
  const [twoBInvoices, setTwoBInvoices] = useState<Gstr2BInvoice[]>([])
  const [useGstr2bJson, setUseGstr2bJson] = useState(false)
  const [gstr2bRawJson, setGstr2bRawJson] = useState('')

  const loadData = useCallback(async () => {
    try {
      setLoading(true)
      const [sumRes, batchRes, clRes] = await Promise.all([
        gstApi.summary(),
        gstApi.listBatches(),
        api<ClientPage | Client[]>('/api/clients?take=200')
      ])
      const clientList = Array.isArray(clRes) ? clRes : (clRes?.items || [])
      setSummary(sumRes)
      setBatches(batchRes || [])
      setClients(clientList)
      if (clientList.length > 0) {
        setFormClientId(prev => prev || clientList[0].id)
      }
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load GST data.')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    loadData()
  }, [loadData])

  async function loadItems(batchId: string, filter: string = 'all') {
    try {
      setLoadingItems(true)
      const res = await gstApi.listItems(batchId, filter === 'all' ? undefined : filter)
      setItems(res || [])
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to load batch items.')
    } finally {
      setLoadingItems(false)
    }
  }

  async function handleSelectBatch(batch: GstReconciliationBatchDto) {
    setSelectedBatch(batch)
    setItemFilter('all')
    await loadItems(batch.id, 'all')
  }

  function handleFilterChange(filter: string) {
    setItemFilter(filter)
    if (selectedBatch) {
      loadItems(selectedBatch.id, filter)
    }
  }

  function loadSampleData() {
    setPrInvoices([
      { supplierGstin: '27AABCS1429B1ZB', supplierName: 'Siemens India Ltd', invoiceNumber: 'SIEM/24-25/001', invoiceDate: '2024-04-05', invoiceType: 'regular', taxableValue: 200000, cgst: 18000, sgst: 18000, igst: 0, cess: 0 },
      { supplierGstin: '27AABCS1429B1ZB', supplierName: 'Siemens India Ltd', invoiceNumber: 'SIEM/24-25/002', invoiceDate: '2024-04-12', invoiceType: 'regular', taxableValue: 100000, cgst: 9000, sgst: 9000, igst: 0, cess: 0 },
      { supplierGstin: '29AAACH7409R1ZX', supplierName: 'HCL Technologies', invoiceNumber: 'INV-8889', invoiceDate: '2024-04-18', invoiceType: 'regular', taxableValue: 150000, cgst: 0, sgst: 0, igst: 27000, cess: 0 },
      { supplierGstin: '06AAACD1111A1Z1', supplierName: 'Dell Global', invoiceNumber: 'DEL/099', invoiceDate: '2024-04-20', invoiceType: 'regular', taxableValue: 80000, cgst: 0, sgst: 0, igst: 14400, cess: 0 }
    ])

    setTwoBInvoices([
      // Exact match with different slash formatting
      { supplierGstin: '27AABCS1429B1ZB', supplierName: 'Siemens India Ltd', invoiceNumber: 'SIEM-24-25-1', invoiceDate: '2024-04-05', invoiceType: 'regular', taxableValue: 200000, cgst: 18000, sgst: 18000, igst: 0, cess: 0, itcAvailable: true, ineligibleReason: null },
      // Match with tolerance (0.50 round off)
      { supplierGstin: '27AABCS1429B1ZB', supplierName: 'Siemens India Ltd', invoiceNumber: 'SIEM/24-25/02', invoiceDate: '2024-04-12', invoiceType: 'regular', taxableValue: 100000, cgst: 9000.25, sgst: 9000.25, igst: 0, cess: 0, itcAvailable: true, ineligibleReason: null },
      // Dell with tax discrepancy
      { supplierGstin: '06AAACD1111A1Z1', supplierName: 'Dell Global', invoiceNumber: 'DEL-99', invoiceDate: '2024-04-20', invoiceType: 'regular', taxableValue: 80000, cgst: 0, sgst: 0, igst: 9600, cess: 0, itcAvailable: true, ineligibleReason: null },
      // Unbooked Tata Steel in 2B
      { supplierGstin: '27AAACT2727Q1ZW', supplierName: 'Tata Steel Ltd', invoiceNumber: 'TS/9000', invoiceDate: '2024-04-28', invoiceType: 'regular', taxableValue: 500000, cgst: 45000, sgst: 45000, igst: 0, cess: 0, itcAvailable: true, ineligibleReason: null }
    ])
  }

  async function handleRunReconcile() {
    if (!formClientId) {
      setError('Please select a client.')
      return
    }
    if (!useGstr2bJson && prInvoices.length === 0 && twoBInvoices.length === 0) {
      setError('Please add invoices or click "Load Demo Invoices".')
      return
    }
    if (useGstr2bJson && !gstr2bRawJson.trim()) {
      setError('Please paste GSTR-2B JSON.')
      return
    }

    setRunning(true)
    setError(null)
    try {
      let created: GstReconciliationBatchDto
      if (useGstr2bJson) {
        created = await gstApi.reconcileJson({
          clientId: formClientId,
          returnPeriod: formPeriod,
          financialYear: formFy,
          toleranceAmount: formTolerance,
          purchaseInvoices: prInvoices,
          gstr2bJson: gstr2bRawJson,
          notes: formNotes
        })
      } else {
        created = await gstApi.reconcile({
          clientId: formClientId,
          returnPeriod: formPeriod,
          financialYear: formFy,
          toleranceAmount: formTolerance,
          purchaseInvoices: prInvoices,
          gstr2BInvoices: twoBInvoices,
          notes: formNotes
        })
      }
      setShowModal(false)
      await loadData()
      await handleSelectBatch(created)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Reconciliation failed.')
    } finally {
      setRunning(false)
    }
  }

  async function handleDeleteBatch(id: string) {
    if (!confirm('Are you sure you want to delete this reconciliation batch?')) return
    try {
      await gstApi.deleteBatch(id)
      await loadData()
      if (selectedBatch?.id === id) setSelectedBatch(null)
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : 'Failed to delete batch.')
    }
  }

  return (
    <div style={{ padding: '24px', maxWidth: '1440px', margin: '0 auto', color: 'var(--text)' }}>
      {/* Header */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div>
          <h1 style={{ fontSize: '24px', fontWeight: 'bold', margin: '0 0 4px 0', color: 'var(--text)' }}>
            GST 2A / 2B Purchase Register Reconciliation
          </h1>
          <p style={{ margin: 0, color: 'var(--muted)', fontSize: '14px' }}>
            Section 16(2)(aa) &amp; Rule 36(4) Matching • Numbering Normalizer • Round-off Tolerance • Missing ITC Radar
          </p>
        </div>
        <div style={{ display: 'flex', gap: '12px' }}>
          {selectedBatch && (
            <button
              onClick={() => setSelectedBatch(null)}
              style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: '1px solid var(--border)', color: 'var(--text)', borderRadius: '6px', cursor: 'pointer', fontWeight: 500 }}
            >
              ← Back to Batches
            </button>
          )}
          <button
            onClick={() => {
              setShowModal(true)
              loadSampleData()
            }}
            style={{ padding: '8px 16px', background: 'var(--accent)', color: '#fff', border: 'none', borderRadius: '6px', fontWeight: 600, cursor: 'pointer' }}
          >
            + New Reconciliation Batch
          </button>
        </div>
      </div>

      {error && (
        <div style={{ padding: '12px 16px', background: 'rgba(220, 38, 38, 0.08)', border: '1px solid rgba(220, 38, 38, 0.35)', borderRadius: '6px', color: 'var(--error)', marginBottom: '16px' }}>
          {error}
        </div>
      )}

      {/* KPI Cards */}
      {summary && !selectedBatch && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', marginBottom: '24px' }}>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>RECONCILED BATCHES</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--text)', marginTop: '4px' }}>{summary.totalBatches}</div>
          </div>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>MATCHED INVOICES</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--success)', marginTop: '4px' }}>{summary.totalMatchedCount}</div>
          </div>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>MISSING IN 2B (ITC AT RISK)</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--error)', marginTop: '4px' }}>{summary.totalMissingIn2BCount}</div>
            <div style={{ fontSize: '12px', color: 'var(--error)', marginTop: '2px' }}>{fmtInr(summary.totalItcAtRisk)}</div>
          </div>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>UNBOOKED IN 2B</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--warning)', marginTop: '4px' }}>{summary.totalMissingInBooksCount}</div>
          </div>
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '16px' }}>
            <div style={{ fontSize: '12px', color: 'var(--muted)', fontWeight: 600 }}>ELIGIBLE ITC (GSTR-3B)</div>
            <div style={{ fontSize: '24px', fontWeight: 700, color: 'var(--accent)', marginTop: '4px' }}>{fmtInr(summary.totalItcEligible)}</div>
          </div>
        </div>
      )}

      {/* Main View: Batches Table OR Selected Batch Item Workspace */}
      {!selectedBatch ? (
        <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '20px' }}>
          <h2 style={{ fontSize: '18px', fontWeight: 600, marginTop: 0, marginBottom: '16px', color: 'var(--text)' }}>Reconciliation Runs</h2>

          {loading ? (
            <div style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>Loading GST reconciliation batches...</div>
          ) : batches.length === 0 ? (
            <div style={{ padding: '48px', textAlign: 'center', color: 'var(--muted)' }}>
              No reconciliation batches found. Click "+ New Reconciliation Batch" to compare client purchases against GSTR-2B.
            </div>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
              <thead>
                <tr style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', textAlign: 'left', color: 'var(--muted)' }}>
                  <th style={{ padding: '10px 12px' }}>Client Name</th>
                  <th style={{ padding: '10px 12px' }}>Period / FY</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>PR Invoices</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>2B Invoices</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Matched</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Missing in 2B</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Eligible ITC</th>
                  <th style={{ padding: '10px 12px', textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {batches.map(b => (
                  <tr key={b.id} style={{ borderBottom: '1px solid var(--border)' }}>
                    <td style={{ padding: '10px 12px', fontWeight: 600, color: 'var(--text)' }}>
                      {b.clientName || 'Client ' + b.clientId.slice(0, 8)}
                    </td>
                    <td style={{ padding: '10px 12px' }}>
                      <span style={{ fontWeight: 500 }}>{b.returnPeriod}</span>
                      <div style={{ fontSize: '11px', color: 'var(--muted)' }}>{b.financialYear}</div>
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{b.purchaseRegisterCount}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>{b.gstr2BCount}</td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600, color: 'var(--success)' }}>
                      {b.matchedCount + b.matchedWithToleranceCount}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600, color: b.missingIn2BCount > 0 ? 'var(--error)' : 'var(--muted)' }}>
                      {b.missingIn2BCount}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600, color: 'var(--accent)' }}>
                      {fmtInr(b.totalItcEligible)}
                    </td>
                    <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                      <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
                        <button
                          onClick={() => handleSelectBatch(b)}
                          style={{ padding: '4px 10px', fontSize: '12px', background: 'var(--accent)', color: '#fff', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: 500 }}
                        >
                          View Results →
                        </button>
                        <button
                          onClick={() => handleDeleteBatch(b.id)}
                          style={{ padding: '4px 8px', fontSize: '12px', background: 'rgba(220, 38, 38, 0.12)', color: 'var(--error)', border: 'none', borderRadius: '4px', cursor: 'pointer' }}
                        >
                          Delete
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ) : (
        /* Batch Detail View */
        <div>
          {/* Batch Overview Banner */}
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '20px', marginBottom: '24px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
              <div>
                <span style={{
                  padding: '3px 8px',
                  borderRadius: '4px',
                  background: 'rgba(37, 99, 235, 0.12)',
                  color: 'var(--accent)',
                  fontWeight: 600,
                  fontSize: '12px'
                }}>
                  PERIOD {selectedBatch.returnPeriod} • FY {selectedBatch.financialYear}
                </span>
                <h2 style={{ fontSize: '20px', fontWeight: 700, margin: '8px 0 4px 0', color: 'var(--text)' }}>
                  Reconciliation Results: {selectedBatch.clientName}
                </h2>
                <div style={{ fontSize: '13px', color: 'var(--muted)' }}>
                  Tolerance applied: ±₹{selectedBatch.toleranceAmount.toFixed(2)} • {selectedBatch.notes}
                </div>
              </div>
            </div>

            {/* Metrics Breakdown */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(5, 1fr)', gap: '16px', background: 'var(--card-subtle)', padding: '14px', borderRadius: '6px', marginTop: '16px', border: '1px solid var(--border)' }}>
              <div>
                <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>PURCHASE REGISTER (BOOKS)</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text)', marginTop: '2px' }}>{selectedBatch.purchaseRegisterCount} Invoices</div>
                <div style={{ fontSize: '12px', color: 'var(--muted)' }}>Tax: {fmtInr(selectedBatch.totalPrTax)}</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>GSTR-2B (PORTAL)</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text)', marginTop: '2px' }}>{selectedBatch.gstr2BCount} Invoices</div>
                <div style={{ fontSize: '12px', color: 'var(--muted)' }}>Tax: {fmtInr(selectedBatch.total2BTax)}</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>MATCHED INVOICES</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--success)', marginTop: '2px' }}>
                  {selectedBatch.matchedCount + selectedBatch.matchedWithToleranceCount}
                </div>
                <div style={{ fontSize: '12px', color: 'var(--success)' }}>Exact: {selectedBatch.matchedCount} • Tol: {selectedBatch.matchedWithToleranceCount}</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>MISSING IN 2B (RISK)</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--error)', marginTop: '2px' }}>{selectedBatch.missingIn2BCount} Invoices</div>
                <div style={{ fontSize: '12px', color: 'var(--error)' }}>Supplier did not file</div>
              </div>
              <div>
                <div style={{ fontSize: '11px', color: 'var(--muted)', fontWeight: 600 }}>UNBOOKED IN 2B</div>
                <div style={{ fontSize: '15px', fontWeight: 700, color: 'var(--warning)', marginTop: '2px' }}>{selectedBatch.missingInBooksCount} Invoices</div>
                <div style={{ fontSize: '12px', color: 'var(--warning)' }}>Avail in books</div>
              </div>
            </div>
          </div>

          {/* Filter Tabs & Items Table */}
          <div style={{ background: 'var(--card)', border: '1px solid var(--border)', borderRadius: '8px', padding: '20px' }}>
            <div style={{ display: 'flex', gap: '8px', marginBottom: '16px', borderBottom: '1px solid var(--border)', paddingBottom: '12px', flexWrap: 'wrap' }}>
              {[
                { id: 'all', label: `All Invoices (${items.length})` },
                { id: 'exact_match', label: `Exact Matches (${selectedBatch.matchedCount})` },
                { id: 'match_with_tolerance', label: `Tolerance Matches (${selectedBatch.matchedWithToleranceCount})` },
                { id: 'missing_in_2b', label: `Missing in 2B (${selectedBatch.missingIn2BCount})` },
                { id: 'missing_in_books', label: `Missing in Books (${selectedBatch.missingInBooksCount})` },
                { id: 'tax_mismatch', label: `Tax Mismatch (${selectedBatch.mismatchedTaxCount})` },
              ].map(tab => (
                <button
                  key={tab.id}
                  onClick={() => handleFilterChange(tab.id)}
                  style={{
                    padding: '6px 12px',
                    borderRadius: '4px',
                    border: itemFilter === tab.id ? '1px solid var(--accent)' : '1px solid var(--border)',
                    background: itemFilter === tab.id ? 'var(--accent)' : 'var(--card-subtle)',
                    color: itemFilter === tab.id ? '#fff' : 'var(--text-secondary)',
                    fontWeight: 500,
                    fontSize: '12px',
                    cursor: 'pointer'
                  }}
                >
                  {tab.label}
                </button>
              ))}
            </div>

            {loadingItems ? (
              <div style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>Loading items...</div>
            ) : items.length === 0 ? (
              <div style={{ padding: '32px', textAlign: 'center', color: 'var(--muted)' }}>No invoices found in this filter.</div>
            ) : (
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '13px' }}>
                <thead>
                  <tr style={{ background: 'var(--card-subtle)', borderBottom: '1px solid var(--border)', textAlign: 'left', color: 'var(--muted)' }}>
                    <th style={{ padding: '10px 12px' }}>Supplier GSTIN / Name</th>
                    <th style={{ padding: '10px 12px' }}>Invoice No / Date</th>
                    <th style={{ padding: '10px 12px', textAlign: 'right' }}>Books (Taxable / Tax)</th>
                    <th style={{ padding: '10px 12px', textAlign: 'right' }}>2B (Taxable / Tax)</th>
                    <th style={{ padding: '10px 12px' }}>Match Status</th>
                    <th style={{ padding: '10px 12px' }}>ITC Status</th>
                    <th style={{ padding: '10px 12px', textAlign: 'right' }}>Diff</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map(it => {
                    const prTax = it.prCgst + it.prSgst + it.prIgst + it.prCess
                    const twoBTax = it.gstr2BCgst + it.gstr2BSgst + it.gstr2BIgst + it.gstr2BCess

                    return (
                      <tr key={it.id} style={{ borderBottom: '1px solid var(--border)' }}>
                        <td style={{ padding: '10px 12px' }}>
                          <span style={{ fontWeight: 600, color: 'var(--text)' }}>{it.supplierGstin}</span>
                          <div style={{ fontSize: '11px', color: 'var(--muted)' }}>{it.supplierName || 'Unknown Vendor'}</div>
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          <span style={{ fontWeight: 500 }}>{it.invoiceNumber}</span>
                          <div style={{ fontSize: '11px', color: 'var(--muted)' }}>
                            {it.invoiceDate || 'Date missing'} • {it.invoiceType.toUpperCase()}
                          </div>
                        </td>
                        <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                          <div>{it.prTaxableValue > 0 ? fmtInr(it.prTaxableValue) : '-'}</div>
                          <div style={{ fontSize: '11px', color: 'var(--muted)' }}>
                            {prTax > 0 ? `Tax: ${fmtInr(prTax)}` : ''}
                          </div>
                        </td>
                        <td style={{ padding: '10px 12px', textAlign: 'right' }}>
                          <div>{it.gstr2BTaxableValue > 0 ? fmtInr(it.gstr2BTaxableValue) : '-'}</div>
                          <div style={{ fontSize: '11px', color: 'var(--muted)' }}>
                            {twoBTax > 0 ? `Tax: ${fmtInr(twoBTax)}` : ''}
                          </div>
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          <span style={{
                            padding: '2px 8px',
                            borderRadius: '12px',
                            fontSize: '11px',
                            fontWeight: 600,
                            background:
                              it.matchStatus === 'exact_match' ? 'rgba(22, 163, 74, 0.12)' :
                              it.matchStatus === 'match_with_tolerance' ? 'rgba(37, 99, 235, 0.12)' :
                              it.matchStatus === 'missing_in_2b' ? 'rgba(220, 38, 38, 0.12)' :
                              it.matchStatus === 'missing_in_books' ? 'rgba(217, 119, 6, 0.12)' : 'rgba(220, 38, 38, 0.12)',
                            color:
                              it.matchStatus === 'exact_match' ? 'var(--success)' :
                              it.matchStatus === 'match_with_tolerance' ? 'var(--accent)' :
                              it.matchStatus === 'missing_in_2b' ? 'var(--error)' :
                              it.matchStatus === 'missing_in_books' ? 'var(--warning)' : 'var(--error)',
                          }}>
                            {it.matchStatus.replace(/_/g, ' ').toUpperCase()}
                          </span>
                        </td>
                        <td style={{ padding: '10px 12px' }}>
                          <span style={{
                            padding: '2px 6px',
                            borderRadius: '4px',
                            fontSize: '11px',
                            fontWeight: 500,
                            background: it.itcEligibility === 'eligible' ? 'rgba(22, 163, 74, 0.12)' : 'rgba(220, 38, 38, 0.12)',
                            color: it.itcEligibility === 'eligible' ? 'var(--success)' : 'var(--error)'
                          }}>
                            {it.itcEligibility.toUpperCase()}
                          </span>
                        </td>
                        <td style={{ padding: '10px 12px', textAlign: 'right', fontWeight: 600, color: it.taxDifference > 0 ? 'var(--error)' : 'var(--success)' }}>
                          {it.taxDifference > 0 ? fmtInr(it.taxDifference) : '₹0'}
                        </td>
                      </tr>
                    )
                  })}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}

      {/* Modal: New Reconciliation */}
      {showModal && (
        <div style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          background: 'rgba(15, 23, 42, 0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000
        }}>
          <div style={{ background: 'var(--card)', color: 'var(--text)', border: '1px solid var(--border)', borderRadius: '8px', width: '650px', maxHeight: '90vh', overflowY: 'auto', padding: '24px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
              <h3 style={{ margin: 0, fontSize: '18px' }}>Run GST 2A/2B Reconciliation</h3>
              <button
                onClick={loadSampleData}
                style={{ padding: '4px 8px', fontSize: '12px', background: 'var(--card-subtle)', border: '1px solid var(--border)', color: 'var(--text)', borderRadius: '4px', cursor: 'pointer' }}
              >
                ↻ Load Demo Invoices
              </button>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: '12px', marginBottom: '12px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px', color: 'var(--text-secondary)' }}>Client</label>
                <select
                  value={formClientId}
                  onChange={e => setFormClientId(e.target.value)}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                >
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px', color: 'var(--text-secondary)' }}>Period</label>
                <input
                  type="text"
                  value={formPeriod}
                  onChange={e => setFormPeriod(e.target.value)}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  placeholder="042024"
                />
              </div>
              <div>
                <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px', color: 'var(--text-secondary)' }}>Financial Year</label>
                <input
                  type="text"
                  value={formFy}
                  onChange={e => setFormFy(e.target.value)}
                  style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
                  placeholder="2024-25"
                />
              </div>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px', color: 'var(--text-secondary)' }}>
                Rounding Tolerance Threshold (₹)
              </label>
              <input
                type="number"
                step="0.5"
                value={formTolerance}
                onChange={e => setFormTolerance(Number(e.target.value))}
                style={{ width: '120px', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
              />
              <span style={{ fontSize: '12px', color: 'var(--muted)', marginLeft: '8px' }}>
                Differences within this amount will be marked "Match with Tolerance".
              </span>
            </div>

            <div style={{ marginBottom: '16px' }}>
              <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, marginBottom: '4px', color: 'var(--text-secondary)' }}>
                Reconciliation Notes / Remarks
              </label>
              <input
                type="text"
                value={formNotes}
                onChange={e => setFormNotes(e.target.value)}
                style={{ width: '100%', padding: '6px 8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)' }}
              />
            </div>

            <div style={{ display: 'flex', gap: '8px', marginBottom: '16px' }}>
              <button
                type="button"
                onClick={() => setUseGstr2bJson(false)}
                style={{
                  padding: '6px 12px',
                  borderRadius: '4px',
                  border: !useGstr2bJson ? '1px solid var(--accent)' : '1px solid var(--border)',
                  background: !useGstr2bJson ? 'var(--accent)' : 'var(--card-subtle)',
                  color: !useGstr2bJson ? '#fff' : 'var(--text-secondary)',
                  fontSize: '12px',
                  fontWeight: 600,
                  cursor: 'pointer'
                }}
              >
                Preloaded Invoice Mode
              </button>
              <button
                type="button"
                onClick={() => setUseGstr2bJson(true)}
                style={{
                  padding: '6px 12px',
                  borderRadius: '4px',
                  border: useGstr2bJson ? '1px solid var(--accent)' : '1px solid var(--border)',
                  background: useGstr2bJson ? 'var(--accent)' : 'var(--card-subtle)',
                  color: useGstr2bJson ? '#fff' : 'var(--text-secondary)',
                  fontSize: '12px',
                  fontWeight: 600,
                  cursor: 'pointer'
                }}
              >
                Direct GSTR-2B JSON (Portal)
              </button>
            </div>

            {useGstr2bJson ? (
              <div style={{ marginBottom: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '4px' }}>
                  <label style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text-secondary)' }}>
                    GSTR-2B JSON (B2B, CDNR invoices)
                  </label>
                  <button
                    type="button"
                    onClick={() => setGstr2bRawJson(`{
  "data": {
    "gstin": "27AAACB1234F1Z9",
    "fp": "042024",
    "b2b": [
      {
        "ctin": "27BBBCB5678M1Z1",
        "cname": "Supplier Logistics Ltd",
        "inv": [
          {
            "inum": "INV-1001",
            "idt": "12-04-2024",
            "val": 236000.00,
            "itcavl": "Y",
            "items": [
              { "txval": 200000.00, "igst": 0, "cgst": 18000.00, "sgst": 18000.00, "cess": 0 }
            ]
          }
        ]
      }
    ]
  }
}`)}
                    style={{ background: 'transparent', border: 'none', color: 'var(--accent)', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
                  >
                    Load Sample GSTR-2B JSON
                  </button>
                </div>
                <textarea
                  rows={6}
                  value={gstr2bRawJson}
                  onChange={e => setGstr2bRawJson(e.target.value)}
                  placeholder="Paste official GSTR-2B JSON downloaded from GST portal..."
                  style={{ width: '100%', padding: '8px', borderRadius: '4px', border: '1px solid var(--border)', background: 'var(--card)', color: 'var(--text)', fontFamily: 'monospace', fontSize: '12px' }}
                />
              </div>
            ) : (
              /* Invoices Preview */
              <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '16px', background: 'var(--card-subtle)', padding: '12px', borderRadius: '6px', border: '1px solid var(--border)', marginBottom: '16px' }}>
                <div>
                  <div style={{ fontWeight: 600, fontSize: '12px', marginBottom: '6px' }}>
                    Purchase Register Invoices: {prInvoices.length}
                  </div>
                  <div style={{ fontSize: '11px', color: 'var(--muted)', maxHeight: '100px', overflowY: 'auto' }}>
                    {prInvoices.map((p, i) => (
                      <div key={i}>{p.invoiceNumber} - {p.supplierName} ({fmtInr(p.taxableValue)})</div>
                    ))}
                  </div>
                </div>
                <div>
                  <div style={{ fontWeight: 600, fontSize: '12px', marginBottom: '6px' }}>
                    GSTR-2B Portal Invoices: {twoBInvoices.length}
                  </div>
                  <div style={{ fontSize: '11px', color: 'var(--muted)', maxHeight: '100px', overflowY: 'auto' }}>
                    {twoBInvoices.map((b, i) => (
                      <div key={i}>{b.invoiceNumber} - {b.supplierName} ({fmtInr(b.taxableValue)})</div>
                    ))}
                  </div>
                </div>
              </div>
            )}

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
              <button
                onClick={() => setShowModal(false)}
                style={{ padding: '8px 16px', background: 'var(--card-subtle)', border: '1px solid var(--border)', color: 'var(--text)', borderRadius: '4px', cursor: 'pointer' }}
              >
                Cancel
              </button>
              <button
                onClick={handleRunReconcile}
                disabled={running}
                style={{ padding: '8px 16px', background: 'var(--accent)', color: '#fff', border: 'none', borderRadius: '4px', fontWeight: 600, cursor: 'pointer' }}
              >
                {running ? 'Reconciling...' : 'Run Reconciliation Engine'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
