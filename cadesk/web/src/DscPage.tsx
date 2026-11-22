import { useEffect, useState, useCallback } from 'react'
import { toError, api, type Client, type DscRegister, type DscSummary, type CreateDscRequest, type TransferCustodyRequest } from './api'
import { fmtDate } from './format'

export default function DscPage({ can }: { can: (p: string) => boolean; meId: string }) {
  const canManage = can('dsc.manage')
  const [items, setItems] = useState<DscRegister[]>([])
  const [summary, setSummary] = useState<DscSummary | null>(null)
  const [clients, setClients] = useState<Client[]>([])
  const [users, setUsers] = useState<Array<{ id: string; name: string }>>([])

  // Filters
  const [statusFilter, setStatusFilter] = useState<string>('all')
  const [locationFilter, setLocationFilter] = useState<string>('all')
  const [search, setSearch] = useState('')

  // Modals
  const [showRegisterModal, setShowRegisterModal] = useState(false)
  const [transferItem, setTransferItem] = useState<DscRegister | null>(null)
  const [historyItem, setHistoryItem] = useState<DscRegister | null>(null)

  // Register Form
  const [holderName, setHolderName] = useState('')
  const [pan, setPan] = useState('')
  const [clientId, setClientId] = useState('')
  const [dscType, setDscType] = useState('Class 3')
  const [issuingAuthority, setIssuingAuthority] = useState('eMudhra')
  const [validFrom, setValidFrom] = useState(() => new Date().toISOString().slice(0, 10))
  const [validTo, setValidTo] = useState(() => {
    const d = new Date()
    d.setFullYear(d.getFullYear() + 2)
    return d.toISOString().slice(0, 10)
  })
  const [serialNumber, setSerialNumber] = useState('')
  const [usbTokenId, setUsbTokenId] = useState('')
  const [location, setLocation] = useState('vault')
  const [custodianUserId, setCustodianUserId] = useState('')
  const [notes, setNotes] = useState('')

  // Transfer Form
  const [toLocation, setToLocation] = useState('with_staff')
  const [transferCustodianId, setTransferCustodianId] = useState('')
  const [transferRemarks, setTransferRemarks] = useState('')

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Load clients and users
  useEffect(() => {
    api<{ items: Client[] }>('/api/clients?take=200')
      .then(r => setClients(r.items))
      .catch(() => {})

    api<Array<{ id: string; name: string }>>('/api/users')
      .then(r => setUsers(r))
      .catch(() => {})
  }, [])

  const loadData = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const q = new URLSearchParams()
      if (statusFilter !== 'all') q.set('status', statusFilter)
      if (locationFilter !== 'all') q.set('location', locationFilter)

      const [listRes, sumRes] = await Promise.all([
        api<DscRegister[]>(`/api/dsc?${q.toString()}`),
        api<DscSummary>('/api/dsc/summary')
      ])
      setItems(listRes)
      setSummary(sumRes)
    } catch (caught) {
      const e = toError(caught)
      setError(e.message || 'Failed to load DSC records')
    } finally {
      setLoading(false)
    }
  }, [statusFilter, locationFilter])

  useEffect(() => {
    loadData()
  }, [loadData])

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      const payload: CreateDscRequest = {
        holderName,
        pan,
        clientId: clientId || null,
        dscType,
        issuingAuthority,
        validFrom,
        validTo,
        serialNumber: serialNumber || null,
        usbTokenId: usbTokenId || null,
        location,
        custodianUserId: custodianUserId || null,
        notes: notes || null
      }
      await api('/api/dsc', {
        method: 'POST',
        body: JSON.stringify(payload)
      })
      setShowRegisterModal(false)
      // reset
      setHolderName('')
      setPan('')
      setSerialNumber('')
      setUsbTokenId('')
      setNotes('')
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err.message || 'Failed to register DSC token')
    }
  }

  const handleTransfer = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!transferItem) return
    setError(null)
    try {
      const payload: TransferCustodyRequest = {
        toLocation,
        custodianUserId: transferCustodianId || null,
        remarks: transferRemarks || null
      }
      await api(`/api/dsc/${transferItem.id}/transfer`, {
        method: 'POST',
        body: JSON.stringify(payload)
      })
      setTransferItem(null)
      setTransferRemarks('')
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err.message || 'Failed to transfer custody')
    }
  }

  const handleDelete = async (id: string) => {
    if (!window.confirm('Are you sure you want to delete this DSC record?')) return
    try {
      await api(`/api/dsc/${id}`, { method: 'DELETE' })
      await loadData()
    } catch (caught) {
      const err = toError(caught)
      setError(err.message || 'Failed to delete DSC')
    }
  }

  const filteredItems = items.filter(d => {
    if (!search.trim()) return true
    const s = search.toLowerCase()
    return (
      d.holderName.toLowerCase().includes(s) ||
      d.pan.toLowerCase().includes(s) ||
      (d.clientName && d.clientName.toLowerCase().includes(s)) ||
      (d.usbTokenId && d.usbTokenId.toLowerCase().includes(s))
    )
  })

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">DSC & Token Custody</h1>
            <span className="inline-flex items-center rounded-md bg-blue-50 px-2.5 py-0.5 text-xs font-semibold text-blue-700 border border-blue-200">
              Sprint 12
            </span>
          </div>
          <p className="mt-1 text-sm text-slate-500">
            Track hardware USB tokens, signers' PAN validity, real-time custody location, and audit movement logs.
          </p>
        </div>

        {canManage && (
          <button
            onClick={() => setShowRegisterModal(true)}
            className="inline-flex items-center justify-center gap-2 px-4 py-2 text-sm font-semibold text-white bg-blue-600 rounded-lg hover:bg-blue-700 shadow-sm transition-all focus:outline-none focus:ring-2 focus:ring-blue-500/20"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Register Token
          </button>
        )}
      </div>

      {error && (
        <div className="p-4 rounded-xl bg-red-50 border border-red-200 text-red-800 text-sm flex items-start gap-3">
          <svg className="w-5 h-5 text-red-500 flex-shrink-0 mt-0.5" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clipRule="evenodd" />
          </svg>
          <div className="flex-1">{error}</div>
          <button onClick={() => setError(null)} className="text-red-600 hover:text-red-800 font-semibold text-xs">Dismiss</button>
        </div>
      )}

      {/* KPI Cards */}
      <div className="grid grid-cols-2 md:grid-cols-5 gap-4">
        <div className="p-4 rounded-xl bg-white border border-slate-200/80 shadow-sm">
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Active Tokens</div>
          <div className="mt-2 text-2xl font-bold text-emerald-600">{summary?.totalActive ?? 0}</div>
          <div className="mt-1 text-xs text-slate-400">Valid &gt; 30 days</div>
        </div>

        <div className="p-4 rounded-xl bg-white border border-slate-200/80 shadow-sm">
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Expiring Soon</div>
          <div className="mt-2 text-2xl font-bold text-amber-600">{summary?.expiringSoon ?? 0}</div>
          <div className="mt-1 text-xs text-amber-600/90 font-medium">Within 30 days</div>
        </div>

        <div className="p-4 rounded-xl bg-white border border-slate-200/80 shadow-sm">
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Expired</div>
          <div className="mt-2 text-2xl font-bold text-rose-600">{summary?.expired ?? 0}</div>
          <div className="mt-1 text-xs text-slate-400">Needs renewal</div>
        </div>

        <div className="p-4 rounded-xl bg-white border border-slate-200/80 shadow-sm">
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">In Office Vault</div>
          <div className="mt-2 text-2xl font-bold text-blue-600">{summary?.inVault ?? 0}</div>
          <div className="mt-1 text-xs text-slate-400">Secure physical storage</div>
        </div>

        <div className="p-4 rounded-xl bg-white border border-slate-200/80 shadow-sm">
          <div className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Checked Out</div>
          <div className="mt-2 text-2xl font-bold text-indigo-600">{summary?.checkedOut ?? 0}</div>
          <div className="mt-1 text-xs text-slate-400">With staff or client</div>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 p-3 bg-white rounded-xl border border-slate-200/80 shadow-sm">
        <div className="flex flex-wrap items-center gap-2">
          {/* Status Filter */}
          <select
            value={statusFilter}
            onChange={e => setStatusFilter(e.target.value)}
            className="text-xs font-medium border border-slate-200 rounded-lg px-3 py-1.5 bg-slate-50 text-slate-700 hover:bg-slate-100 focus:outline-none"
          >
            <option value="all">All Statuses</option>
            <option value="active">Active Only</option>
            <option value="expiring_soon">Expiring Soon (≤30d)</option>
            <option value="expired">Expired</option>
          </select>

          {/* Location Filter */}
          <select
            value={locationFilter}
            onChange={e => setLocationFilter(e.target.value)}
            className="text-xs font-medium border border-slate-200 rounded-lg px-3 py-1.5 bg-slate-50 text-slate-700 hover:bg-slate-100 focus:outline-none"
          >
            <option value="all">All Locations</option>
            <option value="vault">Office Vault</option>
            <option value="with_staff">With Staff</option>
            <option value="with_client">With Client</option>
            <option value="in_transit">In Transit</option>
          </select>
        </div>

        <div className="relative min-w-[240px]">
          <input
            type="text"
            placeholder="Search holder, PAN, token ID..."
            value={search}
            onChange={e => setSearch(e.target.value)}
            className="w-full text-xs border border-slate-200 rounded-lg pl-8 pr-3 py-1.5 focus:outline-none focus:ring-1 focus:ring-blue-500"
          />
          <svg className="w-4 h-4 text-slate-400 absolute left-2.5 top-2" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
        </div>
      </div>

      {/* Main Table */}
      <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-left text-sm text-slate-600">
          <thead className="bg-slate-50 border-b border-slate-200 text-xs font-semibold uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3">Signer & PAN</th>
              <th className="px-4 py-3">Client Entity</th>
              <th className="px-4 py-3">Validity & Countdown</th>
              <th className="px-4 py-3">Token Custody</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100">
            {loading ? (
              <tr>
                <td colSpan={6} className="text-center py-10 text-slate-400">Loading DSC registers...</td>
              </tr>
            ) : filteredItems.length === 0 ? (
              <tr>
                <td colSpan={6} className="text-center py-10 text-slate-400">
                  No DSC tokens found matching the filter criteria.
                </td>
              </tr>
            ) : (
              filteredItems.map(d => {
                const isExpiring = d.status === 'expiring_soon'
                const isExpired = d.status === 'expired'

                return (
                  <tr key={d.id} className="hover:bg-slate-50/70 transition-colors">
                    <td className="px-4 py-3">
                      <div className="font-medium text-slate-900">{d.holderName}</div>
                      <div className="flex items-center gap-1.5 text-xs text-slate-500 mt-0.5">
                        <span className="font-mono bg-slate-100 px-1 rounded text-slate-700">{d.pan}</span>
                        <span>•</span>
                        <span>{d.dscType}</span>
                        <span>•</span>
                        <span className="text-slate-400">{d.issuingAuthority}</span>
                      </div>
                    </td>

                    <td className="px-4 py-3 text-xs">
                      {d.clientName ? (
                        <span className="font-medium text-slate-800">{d.clientName}</span>
                      ) : (
                        <span className="italic text-slate-400">Internal / Unassigned</span>
                      )}
                    </td>

                    <td className="px-4 py-3 text-xs">
                      <div className="text-slate-800 font-medium">
                        {fmtDate(d.validTo)}
                      </div>
                      <div className="mt-0.5">
                        {isExpired ? (
                          <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-rose-600">
                            Expired {Math.abs(d.daysRemaining)} days ago
                          </span>
                        ) : isExpiring ? (
                          <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-amber-600">
                            Expires in {d.daysRemaining} days!
                          </span>
                        ) : (
                          <span className="text-[11px] text-slate-500">
                            {d.daysRemaining} days left
                          </span>
                        )}
                      </div>
                    </td>

                    <td className="px-4 py-3 text-xs">
                      <div className="flex items-center gap-1.5">
                        {d.location === 'vault' ? (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
                            🔒 Vault
                          </span>
                        ) : d.location === 'with_staff' ? (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold bg-blue-50 text-blue-700 border border-blue-200">
                            👤 Staff: {d.custodianUserName || 'Assigned Staff'}
                          </span>
                        ) : d.location === 'with_client' ? (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold bg-purple-50 text-purple-700 border border-purple-200">
                            🏢 With Client
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded text-[11px] font-semibold bg-amber-50 text-amber-700 border border-amber-200">
                            🚚 In Transit
                          </span>
                        )}
                      </div>
                      {d.usbTokenId && (
                        <div className="text-[11px] text-slate-400 mt-0.5 font-mono">
                          ID: {d.usbTokenId}
                        </div>
                      )}
                    </td>

                    <td className="px-4 py-3 text-xs">
                      {isExpired ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-rose-100 text-rose-800">
                          Expired
                        </span>
                      ) : isExpiring ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-amber-100 text-amber-800">
                          Expiring Soon
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
                          Active
                        </span>
                      )}
                    </td>

                    <td className="px-4 py-3 text-right text-xs space-x-2">
                      <button
                        onClick={() => setHistoryItem(d)}
                        className="text-slate-600 hover:text-slate-900 font-medium px-2 py-1 rounded bg-slate-100 hover:bg-slate-200"
                        title="View Custody Log"
                      >
                        History
                      </button>

                      {canManage && (
                        <>
                          <button
                            onClick={() => {
                              setTransferItem(d)
                              setToLocation(d.location === 'vault' ? 'with_staff' : 'vault')
                            }}
                            className="text-blue-600 hover:text-blue-900 font-medium px-2 py-1 rounded bg-blue-50 hover:bg-blue-100"
                          >
                            Transfer
                          </button>

                          <button
                            onClick={() => handleDelete(d.id)}
                            className="text-rose-600 hover:text-rose-900 font-medium px-1.5 py-1 rounded hover:bg-rose-50"
                            title="Delete"
                          >
                            ✕
                          </button>
                        </>
                      )}
                    </td>
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Register Modal */}
      {showRegisterModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl border border-slate-100 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h2 className="text-lg font-bold text-slate-900">Register Digital Signature Certificate</h2>
              <button onClick={() => setShowRegisterModal(false)} className="text-slate-400 hover:text-slate-600 text-lg">✕</button>
            </div>

            <form onSubmit={handleRegister} className="mt-4 space-y-4">
              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-slate-700">Signer / Holder Name *</label>
                  <input
                    type="text"
                    required
                    value={holderName}
                    onChange={e => setHolderName(e.target.value)}
                    placeholder="e.g. Ramesh Kumar"
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2 focus:ring-1 focus:ring-blue-500"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-700">Signer PAN (10 chars) *</label>
                  <input
                    type="text"
                    required
                    maxLength={10}
                    value={pan}
                    onChange={e => setPan(e.target.value.toUpperCase())}
                    placeholder="ABCDE1234F"
                    className="mt-1 w-full text-xs font-mono uppercase border border-slate-300 rounded-lg p-2 focus:ring-1 focus:ring-blue-500"
                  />
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700">Client Organization</label>
                <select
                  value={clientId}
                  onChange={e => setClientId(e.target.value)}
                  className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2 bg-white focus:ring-1 focus:ring-blue-500"
                >
                  <option value="">(Internal / Individual / Unassigned)</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-slate-700">DSC Type</label>
                  <select
                    value={dscType}
                    onChange={e => setDscType(e.target.value)}
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2 bg-white"
                  >
                    <option value="Class 3">Class 3 (Signing & Encryption)</option>
                    <option value="Class 2">Class 2 (Legacy)</option>
                    <option value="DGFT">DGFT Special</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-700">Issuing Authority</label>
                  <input
                    type="text"
                    value={issuingAuthority}
                    onChange={e => setIssuingAuthority(e.target.value)}
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-slate-700">Valid From *</label>
                  <input
                    type="date"
                    required
                    value={validFrom}
                    onChange={e => setValidFrom(e.target.value)}
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-700">Valid To (Expiry Date) *</label>
                  <input
                    type="date"
                    required
                    value={validTo}
                    onChange={e => setValidTo(e.target.value)}
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-slate-700">USB Token Hardware ID</label>
                  <input
                    type="text"
                    value={usbTokenId}
                    onChange={e => setUsbTokenId(e.target.value)}
                    placeholder="e.g. PROXKEY-2024-91"
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-700">Serial Number</label>
                  <input
                    type="text"
                    value={serialNumber}
                    onChange={e => setSerialNumber(e.target.value)}
                    placeholder="e.g. 7A8B9C10D"
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-slate-700">Initial Custody Location</label>
                  <select
                    value={location}
                    onChange={e => setLocation(e.target.value)}
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2 bg-white"
                  >
                    <option value="vault">Office Vault</option>
                    <option value="with_staff">With Staff</option>
                    <option value="with_client">With Client</option>
                    <option value="in_transit">In Transit</option>
                  </select>
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-700">Assigned Staff / Custodian</label>
                  <select
                    value={custodianUserId}
                    onChange={e => setCustodianUserId(e.target.value)}
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2 bg-white"
                  >
                    <option value="">(None / Vault)</option>
                    {users.map(u => (
                      <option key={u.id} value={u.id}>{u.name}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700">Internal Storage Notes</label>
                <textarea
                  rows={2}
                  value={notes}
                  onChange={e => setNotes(e.target.value)}
                  placeholder="e.g. Drawer 3, Safe Box A; token PIN with partner"
                  className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setShowRegisterModal(false)}
                  className="px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 rounded-lg"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-sm"
                >
                  Save DSC Record
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Custody Transfer Modal */}
      {transferItem && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl border border-slate-100">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h2 className="text-base font-bold text-slate-900">Transfer Token Custody</h2>
              <button onClick={() => setTransferItem(null)} className="text-slate-400 hover:text-slate-600">✕</button>
            </div>

            <div className="my-3 p-3 bg-slate-50 rounded-lg text-xs space-y-1">
              <div><strong className="text-slate-700">Token Signer:</strong> {transferItem.holderName} ({transferItem.pan})</div>
              <div><strong className="text-slate-700">Current Location:</strong> <span className="font-semibold text-blue-700">{transferItem.location}</span></div>
            </div>

            <form onSubmit={handleTransfer} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-slate-700">New Custody Location *</label>
                <select
                  value={toLocation}
                  onChange={e => setToLocation(e.target.value)}
                  className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2 bg-white"
                >
                  <option value="vault">Office Vault (Returned)</option>
                  <option value="with_staff">Assigned to Staff Member</option>
                  <option value="with_client">Handed over to Client</option>
                  <option value="in_transit">In Transit / Courier</option>
                </select>
              </div>

              {toLocation === 'with_staff' && (
                <div>
                  <label className="block text-xs font-medium text-slate-700">Assign to Staff Member *</label>
                  <select
                    required
                    value={transferCustodianId}
                    onChange={e => setTransferCustodianId(e.target.value)}
                    className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2 bg-white"
                  >
                    <option value="">Select Staff User</option>
                    {users.map(u => (
                      <option key={u.id} value={u.id}>{u.name}</option>
                    ))}
                  </select>
                </div>
              )}

              <div>
                <label className="block text-xs font-medium text-slate-700">Movement Purpose / Remarks *</label>
                <textarea
                  required
                  rows={2}
                  value={transferRemarks}
                  onChange={e => setTransferRemarks(e.target.value)}
                  placeholder="e.g. Checked out for MCA Annual Return signing"
                  className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setTransferItem(null)}
                  className="px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 rounded-lg"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-sm"
                >
                  Confirm Transfer
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Movement History Drawer */}
      {historyItem && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-lg rounded-2xl bg-white p-6 shadow-xl border border-slate-100 max-h-[85vh] flex flex-col">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div>
                <h2 className="text-base font-bold text-slate-900">Custody Audit Trail</h2>
                <div className="text-xs text-slate-500">{historyItem.holderName} ({historyItem.pan})</div>
              </div>
              <button onClick={() => setHistoryItem(null)} className="text-slate-400 hover:text-slate-600 text-lg">✕</button>
            </div>

            <div className="mt-4 flex-1 overflow-y-auto space-y-4 pr-1">
              {historyItem.movementLogs && historyItem.movementLogs.length > 0 ? (
                <div className="relative pl-6 border-l-2 border-slate-200 space-y-6">
                  {historyItem.movementLogs.map((m, idx) => (
                    <div key={m.id || idx} className="relative">
                      <div className="absolute -left-[31px] top-1 w-3.5 h-3.5 rounded-full bg-blue-600 border-2 border-white ring-2 ring-blue-100" />
                      <div className="flex items-center justify-between text-xs">
                        <span className="font-semibold text-slate-800">
                          {m.fromLocation} → <span className="text-blue-700 font-bold">{m.toLocation}</span>
                        </span>
                        <span className="text-slate-400 text-[11px]">{new Date(m.movedAt).toLocaleString()}</span>
                      </div>
                      {m.handlerUserName && (
                        <div className="text-xs text-slate-600 mt-0.5">
                          By: <span className="font-medium text-slate-800">{m.handlerUserName}</span>
                        </div>
                      )}
                      {m.remarks && (
                        <div className="text-xs text-slate-500 bg-slate-50 p-2 rounded mt-1 border border-slate-100">
                          {m.remarks}
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              ) : (
                <div className="text-center py-8 text-slate-400 text-xs">No movement history recorded.</div>
              )}
            </div>

            <div className="pt-3 border-t border-slate-100 flex justify-end">
              <button
                onClick={() => setHistoryItem(null)}
                className="px-4 py-1.5 text-xs font-semibold text-slate-700 bg-slate-100 hover:bg-slate-200 rounded-lg"
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
