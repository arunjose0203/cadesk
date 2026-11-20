import { useEffect, useState, useCallback } from 'react'
import { api, type Client, type Invoice, type TimeEntry, type BillingSummary, type TimesheetSummary } from './api'
import { fmtDate } from './format'

export default function BillingPage({ can, meId }: { can: (p: string) => boolean; meId: string }) {
  const canManageBilling = can('billing.manage')
  const [subTab, setSubTab] = useState<'invoices' | 'timesheets'>('invoices')
  const [clients, setClients] = useState<Client[]>([])
  
  // Invoices state
  const [invoices, setInvoices] = useState<Invoice[]>([])
  const [invoiceStatus, setInvoiceStatus] = useState<string>('all')
  const [billingSummary, setBillingSummary] = useState<BillingSummary | null>(null)
  const [showNewInvoice, setShowNewInvoice] = useState(false)
  const [payingInvoice, setPayingInvoice] = useState<Invoice | null>(null)

  // Timesheets state
  const [timesheets, setTimesheets] = useState<TimeEntry[]>([])
  const [timesheetSummary, setTimesheetSummary] = useState<TimesheetSummary | null>(null)
  const [unbilledOnly, setUnbilledOnly] = useState(false)
  const [showLogTime, setShowLogTime] = useState(false)

  // Form states: New Invoice
  const [invClient, setInvClient] = useState('')
  const [invIssueDate, setInvIssueDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [invDueDate, setInvDueDate] = useState(() => {
    const d = new Date()
    d.setDate(d.getDate() + 15)
    return d.toISOString().slice(0, 10)
  })
  const [invNotes, setInvNotes] = useState('Payment due within 15 days.')
  const [invItems, setInvItems] = useState<Array<{ description: string; sacCode: string; quantity: number; unitPrice: number; gstRate: number; timeEntryId?: string }>>([
    { description: 'Professional Accounting & Filing Services', sacCode: '9982', quantity: 1, unitPrice: 5000, gstRate: 18 }
  ])
  const [selectedTimeEntries, setSelectedTimeEntries] = useState<TimeEntry[]>([])
  const [availableTimeEntries, setAvailableTimeEntries] = useState<TimeEntry[]>([])

  // Form states: Payment
  const [payAmount, setPayAmount] = useState<number>(0)
  const [payDate, setPayDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [payMode, setPayMode] = useState<string>('bank_transfer')
  const [payRef, setPayRef] = useState('')
  const [payNotes, setPayNotes] = useState('')

  // Form states: Log Time
  const [timeClient, setTimeClient] = useState('')
  const [timeDate, setTimeDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [timeHours, setTimeHours] = useState(2.0)
  const [timeRate, setTimeRate] = useState(1500)
  const [timeDesc, setTimeDesc] = useState('')
  const [timeBillable, setTimeBillable] = useState(true)

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Load clients
  useEffect(() => {
    api<{ items: Client[] }>('/api/clients?take=200')
      .then(r => setClients(r.items))
      .catch(() => {})
  }, [])

  // Load Invoices and Billing Summary
  const loadInvoices = useCallback(async () => {
    try {
      const q = invoiceStatus !== 'all' ? `?status=${invoiceStatus}` : ''
      const [list, sum] = await Promise.all([
        api<Invoice[]>(`/api/invoices${q}`),
        api<BillingSummary>('/api/invoices/summary')
      ])
      setInvoices(list)
      setBillingSummary(sum)
    } catch (err: unknown) {
      if (err instanceof Error) setError(err.message)
    }
  }, [invoiceStatus])

  // Load Timesheets and Summary
  const loadTimesheets = useCallback(async () => {
    try {
      const q = unbilledOnly ? '?unbilledOnly=true' : ''
      const [list, sum] = await Promise.all([
        api<TimeEntry[]>(`/api/timesheets${q}`),
        api<TimesheetSummary>('/api/timesheets/summary')
      ])
      setTimesheets(list)
      setTimesheetSummary(sum)
    } catch (err: unknown) {
      if (err instanceof Error) setError(err.message)
    }
  }, [unbilledOnly])

  useEffect(() => {
    if (subTab === 'invoices') loadInvoices()
    else loadTimesheets()
  }, [subTab, loadInvoices, loadTimesheets])

  // When client changes in New Invoice, fetch unbilled timesheets
  useEffect(() => {
    if (invClient) {
      api<TimeEntry[]>(`/api/timesheets?clientId=${invClient}&unbilledOnly=true`)
        .then(res => setAvailableTimeEntries(res))
        .catch(() => setAvailableTimeEntries([]))
    } else {
      setAvailableTimeEntries([])
    }
  }, [invClient])

  // Add selected timesheets to invoice items
  const handleImportTimesheet = (te: TimeEntry) => {
    if (selectedTimeEntries.some(x => x.id === te.id)) return
    setSelectedTimeEntries(prev => [...prev, te])
    setInvItems(prev => [
      ...prev,
      {
        description: `${te.description} (${fmtDate(te.date)} - ${te.hours}h)`,
        sacCode: '9982',
        quantity: te.hours,
        unitPrice: te.hourlyRate,
        gstRate: 18,
        timeEntryId: te.id
      }
    ])
  }

  // Invoice line calculations
  const calculateTotals = () => {
    let subtotal = 0
    let taxTotal = 0
    invItems.forEach(i => {
      const amt = i.quantity * i.unitPrice
      const tax = (amt * i.gstRate) / 100
      subtotal += amt
      taxTotal += tax
    })
    return { subtotal, taxTotal, total: subtotal + taxTotal }
  }

  const { subtotal: invSubtotal, taxTotal: invTaxTotal, total: invGrandTotal } = calculateTotals()

  // Submit New Invoice
  const handleCreateInvoice = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!invClient) { setError('Please select a client.'); return }
    if (invItems.length === 0) { setError('Add at least one line item.'); return }

    setLoading(true)
    setError(null)
    try {
      await api('/api/invoices', {
        method: 'POST',
        body: JSON.stringify({
          clientId: invClient,
          issueDate: invIssueDate,
          dueDate: invDueDate,
          currency: 'INR',
          notes: invNotes,
          items: invItems,
          timeEntryIds: selectedTimeEntries.map(x => x.id)
        })
      })
      setShowNewInvoice(false)
      loadInvoices()
    } catch (err: unknown) {
      if (err instanceof Error) setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  // Submit Payment
  const handleRecordPayment = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!payingInvoice) return
    if (payAmount <= 0 || payAmount > payingInvoice.balanceDue) {
      setError(`Payment must be between 1 and ₹${payingInvoice.balanceDue.toFixed(2)}`)
      return
    }

    setLoading(true)
    setError(null)
    try {
      await api(`/api/invoices/${payingInvoice.id}/payments`, {
        method: 'POST',
        body: JSON.stringify({
          paymentDate: payDate,
          amount: payAmount,
          paymentMode: payMode,
          referenceNumber: payRef || null,
          notes: payNotes || null
        })
      })
      setPayingInvoice(null)
      loadInvoices()
    } catch (err: unknown) {
      if (err instanceof Error) setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  // Submit Log Time
  const handleLogTime = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!timeClient) { setError('Please select a client.'); return }
    if (!timeDesc.trim()) { setError('Description is required.'); return }

    setLoading(true)
    setError(null)
    try {
      await api('/api/timesheets', {
        method: 'POST',
        body: JSON.stringify({
          clientId: timeClient,
          date: timeDate,
          hours: Number(timeHours),
          hourlyRate: Number(timeRate),
          description: timeDesc,
          billable: timeBillable
        })
      })
      setShowLogTime(false)
      setTimeDesc('')
      loadTimesheets()
    } catch (err: unknown) {
      if (err instanceof Error) setError(err.message)
    } finally {
      setLoading(false)
    }
  }

  // Quick invoice status update
  const handleStatusChange = async (inv: Invoice, newStatus: string) => {
    try {
      await api(`/api/invoices/${inv.id}/status`, {
        method: 'PUT',
        body: JSON.stringify({ status: newStatus })
      })
      loadInvoices()
    } catch (err: unknown) {
      if (err instanceof Error) setError(err.message)
    }
  }

  return (
    <div className="billing-page">
      {/* Top Action Header */}
      <div className="row spread" style={{ alignItems: 'center', marginBottom: '1.25rem' }}>
        <div>
          <h1 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 700 }}>Billing, Invoices & Timesheets</h1>
          <p className="muted small" style={{ margin: '0.25rem 0 0' }}>
            CA practice invoicing, GST billing, payment receipts, and staff billable time management
          </p>
        </div>
        <div className="row" style={{ gap: '0.5rem' }}>
          {canManageBilling && (
            <button className="primary" onClick={() => setShowNewInvoice(true)}>
              <span>+</span> New Invoice
            </button>
          )}
          <button className="secondary" onClick={() => setShowLogTime(true)} title={`User: ${meId}`}>
            <span>⏱️</span> Log Time
          </button>
        </div>
      </div>

      {/* KPI Overview Tiles */}
      <div className="kpi-grid" style={{ marginBottom: '1.5rem' }}>
        <div className="card kpi-card" style={{ borderTop: '4px solid #ef4444' }}>
          <div className="kpi-label">Outstanding Receivables</div>
          <div className="kpi-value" style={{ color: '#ef4444' }}>
            ₹{(billingSummary?.totalOutstanding ?? 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
          </div>
          <div className="kpi-hint">
            Overdue: ₹{(billingSummary?.totalOverdue ?? 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
          </div>
        </div>

        <div className="card kpi-card" style={{ borderTop: '4px solid #2563eb' }}>
          <div className="kpi-label">Billed This Month</div>
          <div className="kpi-value" style={{ color: '#2563eb' }}>
            ₹{(billingSummary?.billedThisMonth ?? 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
          </div>
          <div className="kpi-hint">{billingSummary?.openInvoicesCount ?? 0} active / draft invoices</div>
        </div>

        <div className="card kpi-card" style={{ borderTop: '4px solid #10b981' }}>
          <div className="kpi-label">Collections This Month</div>
          <div className="kpi-value" style={{ color: '#10b981' }}>
            ₹{(billingSummary?.collectedThisMonth ?? 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
          </div>
          <div className="kpi-hint">Payments cleared</div>
        </div>

        <div className="card kpi-card" style={{ borderTop: '4px solid #f59e0b' }}>
          <div className="kpi-label">Unbilled Workload</div>
          <div className="kpi-value" style={{ color: '#f59e0b' }}>
            {(timesheetSummary?.unbilledHours ?? 0).toFixed(1)} hrs
          </div>
          <div className="kpi-hint">
            Est. Value: ₹{(timesheetSummary?.estimatedUnbilledValue ?? 0).toLocaleString('en-IN', { minimumFractionDigits: 2 })}
          </div>
        </div>
      </div>

      {/* Sub Tab Switcher */}
      <nav className="tabs" style={{ marginBottom: '1.25rem', width: 'fit-content' }}>
        <button
          className={subTab === 'invoices' ? 'active' : ''}
          onClick={() => setSubTab('invoices')}
        >
          📄 Invoices ({invoices.length})
        </button>
        <button
          className={subTab === 'timesheets' ? 'active' : ''}
          onClick={() => setSubTab('timesheets')}
        >
          ⏱️ Staff Timesheets ({timesheets.length})
        </button>
      </nav>

      {error && (
        <div className="card" style={{ backgroundColor: '#fef2f2', borderColor: '#fca5a5', color: '#991b1b', marginBottom: '1rem', padding: '0.75rem 1rem' }}>
          <strong>Error: </strong> {error}
          <button className="ghost small" onClick={() => setError(null)} style={{ float: 'right', marginTop: '-0.2rem' }}>Dismiss</button>
        </div>
      )}

      {/* -------------------- INVOICES VIEW -------------------- */}
      {subTab === 'invoices' && (
        <section className="card">
          <div className="row spread" style={{ alignItems: 'center', marginBottom: '1rem' }}>
            <h2 style={{ margin: 0, fontSize: '1.125rem' }}>Client Invoices</h2>
            <div className="row" style={{ gap: '0.5rem' }}>
              <select value={invoiceStatus} onChange={e => setInvoiceStatus(e.target.value)}>
                <option value="all">All Invoices</option>
                <option value="draft">Draft</option>
                <option value="sent">Sent (Unpaid)</option>
                <option value="partially_paid">Partially Paid</option>
                <option value="paid">Fully Paid</option>
                <option value="overdue">Overdue</option>
              </select>
            </div>
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>Invoice #</th>
                <th>Client</th>
                <th>Issue Date</th>
                <th>Due Date</th>
                <th>Subtotal</th>
                <th>GST (18%)</th>
                <th>Total</th>
                <th>Balance Due</th>
                <th>Status</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {invoices.length === 0 ? (
                <tr>
                  <td colSpan={10} style={{ textAlign: 'center', padding: '2rem' }} className="muted">
                    No invoices found. Click <strong>+ New Invoice</strong> to create your first client fee invoice.
                  </td>
                </tr>
              ) : (
                invoices.map(inv => {
                  const isOverdue = inv.status !== 'paid' && inv.status !== 'cancelled' && new Date(inv.dueDate) < new Date()
                  return (
                    <tr key={inv.id}>
                      <td style={{ fontWeight: 600 }}>{inv.invoiceNumber}</td>
                      <td>{inv.clientName || '—'}</td>
                      <td>{fmtDate(inv.issueDate)}</td>
                      <td style={{ color: isOverdue ? '#ef4444' : 'inherit', fontWeight: isOverdue ? 600 : 400 }}>
                        {fmtDate(inv.dueDate)}
                        {isOverdue && <span className="tag delete small" style={{ marginLeft: '0.25rem' }}>Overdue</span>}
                      </td>
                      <td>₹{inv.subtotal.toFixed(2)}</td>
                      <td>₹{inv.taxAmount.toFixed(2)}</td>
                      <td style={{ fontWeight: 600 }}>₹{inv.totalAmount.toFixed(2)}</td>
                      <td style={{ color: inv.balanceDue > 0 ? '#ef4444' : '#10b981', fontWeight: 600 }}>
                        ₹{inv.balanceDue.toFixed(2)}
                      </td>
                      <td>
                        <span className={`tag ${
                          inv.status === 'paid' ? 'create' :
                          inv.status === 'partially_paid' ? 'warning' :
                          inv.status === 'sent' ? 'info' :
                          inv.status === 'cancelled' ? 'muted' : ''
                        }`}>
                          {inv.status.replace('_', ' ')}
                        </span>
                      </td>
                      <td>
                        <div className="row" style={{ gap: '0.25rem' }}>
                          {inv.balanceDue > 0 && inv.status !== 'cancelled' && (
                            <button
                              className="small primary"
                              onClick={() => { setPayingInvoice(inv); setPayAmount(inv.balanceDue) }}
                              title="Record Payment"
                            >
                              ₹ Pay
                            </button>
                          )}
                          {inv.status === 'draft' && (
                            <button
                              className="small secondary"
                              onClick={() => handleStatusChange(inv, 'sent')}
                              title="Mark as Sent to Client"
                            >
                              Send
                            </button>
                          )}
                          {inv.status !== 'cancelled' && inv.status !== 'paid' && (
                            <button
                              className="small ghost"
                              onClick={() => handleStatusChange(inv, 'cancelled')}
                              title="Cancel Invoice"
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
        </section>
      )}

      {/* -------------------- TIMESHEETS VIEW -------------------- */}
      {subTab === 'timesheets' && (
        <section className="card">
          <div className="row spread" style={{ alignItems: 'center', marginBottom: '1rem' }}>
            <h2 style={{ margin: 0, fontSize: '1.125rem' }}>Staff Timesheets & Billable Hours</h2>
            <div className="row" style={{ gap: '0.5rem', alignItems: 'center' }}>
              <label style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.875rem' }}>
                <input
                  type="checkbox"
                  checked={unbilledOnly}
                  onChange={e => setUnbilledOnly(e.target.checked)}
                />
                Unbilled Only
              </label>
            </div>
          </div>

          <table className="data-table">
            <thead>
              <tr>
                <th>Date</th>
                <th>Staff</th>
                <th>Client</th>
                <th>Task</th>
                <th>Description</th>
                <th>Hours</th>
                <th>Hourly Rate</th>
                <th>Est. Value</th>
                <th>Billing Status</th>
              </tr>
            </thead>
            <tbody>
              {timesheets.length === 0 ? (
                <tr>
                  <td colSpan={9} style={{ textAlign: 'center', padding: '2rem' }} className="muted">
                    No timesheet entries recorded. Click <strong>⏱️ Log Time</strong> to track billable work.
                  </td>
                </tr>
              ) : (
                timesheets.map(te => (
                  <tr key={te.id}>
                    <td>{fmtDate(te.date)}</td>
                    <td style={{ fontWeight: 500 }}>{te.userName || 'Staff'}</td>
                    <td>{te.clientName || '—'}</td>
                    <td className="muted">{te.taskTitle || '—'}</td>
                    <td style={{ maxWidth: '280px' }}>{te.description}</td>
                    <td style={{ fontWeight: 600 }}>{te.hours.toFixed(2)}h</td>
                    <td>₹{te.hourlyRate.toFixed(2)}/h</td>
                    <td style={{ fontWeight: 600 }}>₹{(te.hours * te.hourlyRate).toFixed(2)}</td>
                    <td>
                      {te.isBilled ? (
                        <span className="tag create small">Billed</span>
                      ) : te.billable ? (
                        <span className="tag warning small">Unbilled</span>
                      ) : (
                        <span className="tag small">Non-billable</span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </section>
      )}

      {/* -------------------- NEW INVOICE MODAL -------------------- */}
      {showNewInvoice && (
        <div className="modal-backdrop" style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex',
          alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem'
        }}>
          <div className="card modal-content" style={{ maxWidth: '800px', width: '100%', maxHeight: '90vh', overflowY: 'auto' }}>
            <div className="row spread" style={{ alignItems: 'center', borderBottom: '1px solid #e2e8f0', paddingBottom: '0.75rem', marginBottom: '1rem' }}>
              <h2 style={{ margin: 0 }}>Create Client Fee Invoice</h2>
              <button className="ghost small" onClick={() => setShowNewInvoice(false)}>✕</button>
            </div>

            <form onSubmit={handleCreateInvoice}>
              <div className="grid" style={{ gridTemplateColumns: '1fr 1fr', gap: '1rem', marginBottom: '1rem' }}>
                <div>
                  <label>Client *</label>
                  <select value={invClient} onChange={e => setInvClient(e.target.value)} required>
                    <option value="">Select a Client…</option>
                    {clients.map(c => <option key={c.id} value={c.id}>{c.name} ({c.code})</option>)}
                  </select>
                </div>
                <div>
                  <label>Currency</label>
                  <input value="INR (₹) - Indian Rupee" disabled />
                </div>
                <div>
                  <label>Issue Date *</label>
                  <input type="date" value={invIssueDate} onChange={e => setInvIssueDate(e.target.value)} required />
                </div>
                <div>
                  <label>Due Date *</label>
                  <input type="date" value={invDueDate} onChange={e => setInvDueDate(e.target.value)} required />
                </div>
              </div>

              {/* Unbilled timesheet importer */}
              {availableTimeEntries.length > 0 && (
                <div style={{ backgroundColor: '#eff6ff', padding: '0.75rem 1rem', borderRadius: '8px', marginBottom: '1rem', border: '1px solid #bfdbfe' }}>
                  <div style={{ fontWeight: 600, color: '#1e40af', marginBottom: '0.5rem', fontSize: '0.875rem' }}>
                    💡 Unbilled Staff Timesheets Available for this Client:
                  </div>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
                    {availableTimeEntries.map(te => {
                      const imported = selectedTimeEntries.some(x => x.id === te.id)
                      return (
                        <div key={te.id} className="row spread" style={{ alignItems: 'center', fontSize: '0.85rem' }}>
                          <span>
                            {fmtDate(te.date)}: {te.description} — <strong>{te.hours}h @ ₹{te.hourlyRate}/h</strong> (₹{(te.hours * te.hourlyRate).toFixed(2)})
                          </span>
                          <button
                            type="button"
                            className="small secondary"
                            disabled={imported}
                            onClick={() => handleImportTimesheet(te)}
                          >
                            {imported ? '✓ Added' : '+ Add to Invoice'}
                          </button>
                        </div>
                      )
                    })}
                  </div>
                </div>
              )}

              {/* Line Items Table */}
              <div style={{ marginBottom: '1rem' }}>
                <div className="row spread" style={{ alignItems: 'center', marginBottom: '0.5rem' }}>
                  <label style={{ margin: 0, fontWeight: 600 }}>Invoice Line Items</label>
                  <button
                    type="button"
                    className="small ghost"
                    onClick={() => setInvItems(prev => [...prev, { description: '', sacCode: '9982', quantity: 1, unitPrice: 0, gstRate: 18 }])}
                  >
                    + Add Row
                  </button>
                </div>

                <table className="data-table" style={{ fontSize: '0.85rem' }}>
                  <thead>
                    <tr>
                      <th style={{ width: '40%' }}>Description</th>
                      <th>SAC</th>
                      <th>Qty</th>
                      <th>Rate (₹)</th>
                      <th>GST %</th>
                      <th>Total (₹)</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {invItems.map((item, idx) => {
                      const lineAmt = item.quantity * item.unitPrice
                      const lineTax = (lineAmt * item.gstRate) / 100
                      return (
                        <tr key={idx}>
                          <td>
                            <input
                              style={{ width: '100%', padding: '0.35rem' }}
                              placeholder="e.g. GST Audit or Filing"
                              value={item.description}
                              onChange={e => {
                                const val = e.target.value
                                setInvItems(prev => prev.map((it, i) => i === idx ? { ...it, description: val } : it))
                              }}
                              required
                            />
                          </td>
                          <td>
                            <input
                              style={{ width: '70px', padding: '0.35rem' }}
                              value={item.sacCode}
                              onChange={e => {
                                const val = e.target.value
                                setInvItems(prev => prev.map((it, i) => i === idx ? { ...it, sacCode: val } : it))
                              }}
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              step="0.01"
                              style={{ width: '60px', padding: '0.35rem' }}
                              value={item.quantity}
                              onChange={e => {
                                const val = Number(e.target.value)
                                setInvItems(prev => prev.map((it, i) => i === idx ? { ...it, quantity: val } : it))
                              }}
                            />
                          </td>
                          <td>
                            <input
                              type="number"
                              step="0.01"
                              style={{ width: '90px', padding: '0.35rem' }}
                              value={item.unitPrice}
                              onChange={e => {
                                const val = Number(e.target.value)
                                setInvItems(prev => prev.map((it, i) => i === idx ? { ...it, unitPrice: val } : it))
                              }}
                            />
                          </td>
                          <td>
                            <select
                              style={{ padding: '0.35rem' }}
                              value={item.gstRate}
                              onChange={e => {
                                const val = Number(e.target.value)
                                setInvItems(prev => prev.map((it, i) => i === idx ? { ...it, gstRate: val } : it))
                              }}
                            >
                              <option value="18">18%</option>
                              <option value="12">12%</option>
                              <option value="5">5%</option>
                              <option value="0">0%</option>
                            </select>
                          </td>
                          <td style={{ fontWeight: 600 }}>₹{(lineAmt + lineTax).toFixed(2)}</td>
                          <td>
                            {invItems.length > 1 && (
                              <button
                                type="button"
                                className="ghost small"
                                style={{ color: '#ef4444' }}
                                onClick={() => setInvItems(prev => prev.filter((_, i) => i !== idx))}
                              >
                                ✕
                              </button>
                            )}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>

              {/* Summary Calculation Box */}
              <div style={{
                backgroundColor: '#f8fafc', padding: '1rem', borderRadius: '8px',
                border: '1px solid #e2e8f0', marginBottom: '1rem', width: '280px', marginLeft: 'auto'
              }}>
                <div className="row spread" style={{ marginBottom: '0.25rem', fontSize: '0.875rem' }}>
                  <span>Taxable Subtotal:</span>
                  <strong>₹{invSubtotal.toFixed(2)}</strong>
                </div>
                <div className="row spread" style={{ marginBottom: '0.25rem', fontSize: '0.875rem', color: '#64748b' }}>
                  <span>CGST (9%) + SGST (9%):</span>
                  <span>₹{invTaxTotal.toFixed(2)}</span>
                </div>
                <div className="row spread" style={{ borderTop: '2px solid #cbd5e1', paddingTop: '0.5rem', fontSize: '1.1rem', color: '#1e293b' }}>
                  <span>Grand Total:</span>
                  <strong style={{ color: '#2563eb' }}>₹{invGrandTotal.toFixed(2)}</strong>
                </div>
              </div>

              <div>
                <label>Terms & Notes</label>
                <textarea
                  rows={2}
                  value={invNotes}
                  onChange={e => setInvNotes(e.target.value)}
                  placeholder="Payment instructions, bank account details, or terms..."
                />
              </div>

              <div className="row spread" style={{ marginTop: '1.25rem' }}>
                <button type="button" className="ghost" onClick={() => setShowNewInvoice(false)}>Cancel</button>
                <button type="submit" className="primary" disabled={loading}>
                  {loading ? 'Creating…' : 'Save Invoice'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* -------------------- RECORD PAYMENT MODAL -------------------- */}
      {payingInvoice && (
        <div className="modal-backdrop" style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex',
          alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem'
        }}>
          <div className="card modal-content" style={{ maxWidth: '480px', width: '100%' }}>
            <div className="row spread" style={{ alignItems: 'center', borderBottom: '1px solid #e2e8f0', paddingBottom: '0.75rem', marginBottom: '1rem' }}>
              <h2 style={{ margin: 0 }}>Record Client Payment</h2>
              <button className="ghost small" onClick={() => setPayingInvoice(null)}>✕</button>
            </div>

            <div style={{ backgroundColor: '#f1f5f9', padding: '0.75rem 1rem', borderRadius: '8px', marginBottom: '1rem' }}>
              <div className="row spread" style={{ fontSize: '0.875rem', marginBottom: '0.25rem' }}>
                <span>Invoice Number:</span>
                <strong>{payingInvoice.invoiceNumber}</strong>
              </div>
              <div className="row spread" style={{ fontSize: '0.875rem', marginBottom: '0.25rem' }}>
                <span>Client:</span>
                <strong>{payingInvoice.clientName}</strong>
              </div>
              <div className="row spread" style={{ fontSize: '0.875rem', color: '#ef4444', fontWeight: 600 }}>
                <span>Current Balance Due:</span>
                <span>₹{payingInvoice.balanceDue.toFixed(2)}</span>
              </div>
            </div>

            <form onSubmit={handleRecordPayment}>
              <div style={{ marginBottom: '0.75rem' }}>
                <label>Payment Amount (₹) *</label>
                <input
                  type="number"
                  step="0.01"
                  max={payingInvoice.balanceDue}
                  value={payAmount}
                  onChange={e => setPayAmount(Number(e.target.value))}
                  required
                />
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label>Payment Date *</label>
                <input type="date" value={payDate} onChange={e => setPayDate(e.target.value)} required />
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label>Payment Mode *</label>
                <select value={payMode} onChange={e => setPayMode(e.target.value)} required>
                  <option value="bank_transfer">Bank Transfer (NEFT/RTGS/IMPS)</option>
                  <option value="upi">UPI / QR Code</option>
                  <option value="cheque">Cheque</option>
                  <option value="cash">Cash</option>
                  <option value="card">Credit / Debit Card</option>
                  <option value="other">Other</option>
                </select>
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label>Reference / UTR / Cheque Number</label>
                <input
                  placeholder="e.g. UTR12345678 or Cheque # 00412"
                  value={payRef}
                  onChange={e => setPayRef(e.target.value)}
                />
              </div>

              <div style={{ marginBottom: '1.25rem' }}>
                <label>Notes</label>
                <input
                  placeholder="Remarks..."
                  value={payNotes}
                  onChange={e => setPayNotes(e.target.value)}
                />
              </div>

              <div className="row spread">
                <button type="button" className="ghost" onClick={() => setPayingInvoice(null)}>Cancel</button>
                <button type="submit" className="primary" disabled={loading}>
                  {loading ? 'Recording…' : 'Confirm Payment'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* -------------------- LOG TIME MODAL -------------------- */}
      {showLogTime && (
        <div className="modal-backdrop" style={{
          position: 'fixed', top: 0, left: 0, right: 0, bottom: 0,
          backgroundColor: 'rgba(15, 23, 42, 0.65)', display: 'flex',
          alignItems: 'center', justifyContent: 'center', zIndex: 1000, padding: '1rem'
        }}>
          <div className="card modal-content" style={{ maxWidth: '500px', width: '100%' }}>
            <div className="row spread" style={{ alignItems: 'center', borderBottom: '1px solid #e2e8f0', paddingBottom: '0.75rem', marginBottom: '1rem' }}>
              <h2 style={{ margin: 0 }}>Log Billable Time</h2>
              <button className="ghost small" onClick={() => setShowLogTime(false)}>✕</button>
            </div>

            <form onSubmit={handleLogTime}>
              <div style={{ marginBottom: '0.75rem' }}>
                <label>Client *</label>
                <select value={timeClient} onChange={e => setTimeClient(e.target.value)} required>
                  <option value="">Select a Client…</option>
                  {clients.map(c => <option key={c.id} value={c.id}>{c.name} ({c.code})</option>)}
                </select>
              </div>

              <div className="grid" style={{ gridTemplateColumns: '1fr 1fr', gap: '0.75rem', marginBottom: '0.75rem' }}>
                <div>
                  <label>Date *</label>
                  <input type="date" value={timeDate} onChange={e => setTimeDate(e.target.value)} required />
                </div>
                <div>
                  <label>Hours Spent *</label>
                  <input
                    type="number"
                    step="0.25"
                    min="0.25"
                    max="24"
                    value={timeHours}
                    onChange={e => setTimeHours(Number(e.target.value))}
                    required
                  />
                </div>
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label>Hourly Billing Rate (₹/h)</label>
                <input
                  type="number"
                  step="50"
                  value={timeRate}
                  onChange={e => setTimeRate(Number(e.target.value))}
                />
              </div>

              <div style={{ marginBottom: '0.75rem' }}>
                <label>Work Description *</label>
                <textarea
                  rows={3}
                  placeholder="e.g. Preparation of GSTR-3B monthly return and reconciliation of input tax credit..."
                  value={timeDesc}
                  onChange={e => setTimeDesc(e.target.value)}
                  required
                />
              </div>

              <div style={{ marginBottom: '1.25rem' }}>
                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                  <input
                    type="checkbox"
                    checked={timeBillable}
                    onChange={e => setTimeBillable(e.target.checked)}
                  />
                  <span>Billable to client</span>
                </label>
              </div>

              <div className="row spread">
                <button type="button" className="ghost" onClick={() => setShowLogTime(false)}>Cancel</button>
                <button type="submit" className="primary" disabled={loading}>
                  {loading ? 'Saving…' : 'Save Time Entry'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
