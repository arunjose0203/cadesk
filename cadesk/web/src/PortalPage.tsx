import { useEffect, useState, useCallback } from 'react'
import {
  api,
  type Client,
  type ServiceRequest,
  type PortalUser,
  type CreatePortalUserRequest,
  type AddRequestMessageInput,
  type UpdateServiceRequestStatusInput
} from './api'

export default function PortalPage({ can, meId }: { can: (p: string) => boolean; meId: string }) {
  const canManagePortal = can('portal.manage')
  const canManageRequests = can('service_requests.manage')

  const [tab, setTab] = useState<'requests' | 'users'>('requests')
  const [clients, setClients] = useState<Client[]>([])
  const [staffUsers, setStaffUsers] = useState<Array<{ id: string; name: string }>>([])

  // Requests state
  const [requests, setRequests] = useState<ServiceRequest[]>([])
  const [selectedRequest, setSelectedRequest] = useState<ServiceRequest | null>(null)
  const [statusFilter, setStatusFilter] = useState<string>('all')
  const [categoryFilter, setCategoryFilter] = useState<string>('all')
  const [replyText, setReplyText] = useState('')
  const [statusUpdate, setStatusUpdate] = useState<string>('')
  const [assignedStaffId, setAssignedStaffId] = useState<string>('')
  const [resolutionNotes, setResolutionNotes] = useState<string>('')

  // Users state
  const [portalUsers, setPortalUsers] = useState<PortalUser[]>([])
  const [showInviteModal, setShowInviteModal] = useState(false)
  const [inviteClientId, setInviteClientId] = useState('')
  const [inviteEmail, setInviteEmail] = useState('')
  const [inviteName, setInviteName] = useState('')
  const [invitePhone, setInvitePhone] = useState('')
  const [invitePassword, setInvitePassword] = useState('Welcome#2026')
  const [inviteRole, setInviteRole] = useState('client_admin')

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // Load clients & staff
  useEffect(() => {
    api<{ items: Client[] }>('/api/clients?take=200')
      .then(r => setClients(r.items))
      .catch(() => {})

    api<Array<{ id: string; name: string }>>('/api/users')
      .then(r => setStaffUsers(r))
      .catch(() => {})
  }, [])

  const loadRequests = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const q = new URLSearchParams()
      if (statusFilter !== 'all') q.set('status', statusFilter)
      if (categoryFilter !== 'all') q.set('category', categoryFilter)

      const list = await api<ServiceRequest[]>(`/api/service-requests?${q.toString()}`)
      setRequests(list)
    } catch (e: any) {
      setError(e.message || 'Failed to load service requests')
    } finally {
      setLoading(false)
    }
  }, [statusFilter, categoryFilter])

  const loadUsers = useCallback(async () => {
    setLoading(true)
    setError(null)
    try {
      const list = await api<PortalUser[]>('/api/portal/users')
      setPortalUsers(list)
    } catch (e: any) {
      setError(e.message || 'Failed to load client portal users')
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    if (tab === 'requests') loadRequests()
    else loadUsers()
  }, [tab, loadRequests, loadUsers])

  const handleSendReply = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!selectedRequest || !replyText.trim()) return
    setError(null)
    try {
      const payload: AddRequestMessageInput = { message: replyText.trim() }
      const updated = await api<ServiceRequest>(`/api/service-requests/${selectedRequest.id}/messages`, {
        method: 'POST',
        body: JSON.stringify(payload)
      })
      setSelectedRequest(updated)
      setReplyText('')
      await loadRequests()
    } catch (err: any) {
      setError(err.message || 'Failed to post reply')
    }
  }

  const handleUpdateStatus = async () => {
    if (!selectedRequest || !statusUpdate) return
    setError(null)
    try {
      const payload: UpdateServiceRequestStatusInput = {
        status: statusUpdate,
        resolutionNotes: resolutionNotes || null,
        assignedUserId: assignedStaffId || null
      }
      const updated = await api<ServiceRequest>(`/api/service-requests/${selectedRequest.id}/status`, {
        method: 'PUT',
        body: JSON.stringify(payload)
      })
      setSelectedRequest(updated)
      await loadRequests()
    } catch (err: any) {
      setError(err.message || 'Failed to update ticket status')
    }
  }

  const handleCreateUser = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    try {
      const payload: CreatePortalUserRequest = {
        clientId: inviteClientId,
        email: inviteEmail.trim(),
        name: inviteName.trim(),
        phone: invitePhone.trim() || null,
        password: invitePassword,
        role: inviteRole
      }
      await api('/api/portal/users', {
        method: 'POST',
        body: JSON.stringify(payload)
      })
      setShowInviteModal(false)
      setInviteEmail('')
      setInviteName('')
      setInvitePhone('')
      await loadUsers()
    } catch (err: any) {
      setError(err.message || 'Failed to create client portal account')
    }
  }

  const handleToggleUserStatus = async (user: PortalUser) => {
    try {
      await api(`/api/portal/users/${user.id}/status`, {
        method: 'PUT',
        body: JSON.stringify(!user.isActive)
      })
      await loadUsers()
    } catch (err: any) {
      setError(err.message || 'Failed to update portal user status')
    }
  }

  const handleDeleteUser = async (id: string) => {
    if (!window.confirm('Revoke portal access for this user?')) return
    try {
      await api(`/api/portal/users/${id}`, { method: 'DELETE' })
      await loadUsers()
    } catch (err: any) {
      setError(err.message || 'Failed to delete user')
    }
  }

  // Stats
  const totalCount = requests.length
  const submittedCount = requests.filter(r => r.status === 'submitted').length
  const inProgressCount = requests.filter(r => r.status === 'in_progress').length
  const resolvedCount = requests.filter(r => r.status === 'resolved' || r.status === 'closed').length

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <div className="flex items-center gap-3">
            <h1 className="text-2xl font-bold tracking-tight text-slate-900">Client Portal & Desk</h1>
            <span className="inline-flex items-center rounded-md bg-purple-50 px-2.5 py-0.5 text-xs font-semibold text-purple-700 border border-purple-200">
              Sprint 13
            </span>
          </div>
          <p className="mt-1 text-sm text-slate-500">
            External client self-service portal, customer inquiries, filings & invoice viewer, and service desk tickets.
          </p>
        </div>

        {tab === 'users' && canManagePortal && (
          <button
            onClick={() => setShowInviteModal(true)}
            className="inline-flex items-center justify-center gap-2 px-4 py-2 text-sm font-semibold text-white bg-blue-600 rounded-lg hover:bg-blue-700 shadow-sm transition-all"
          >
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
            </svg>
            Invite Client User
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

      {/* Tabs */}
      <div className="flex border-b border-slate-200 gap-4">
        <button
          onClick={() => setTab('requests')}
          className={`pb-3 text-sm font-semibold transition-all relative ${
            tab === 'requests' ? 'text-blue-600 border-b-2 border-blue-600' : 'text-slate-500 hover:text-slate-800'
          }`}
        >
          Service Desk Tickets ({totalCount})
        </button>
        <button
          onClick={() => setTab('users')}
          className={`pb-3 text-sm font-semibold transition-all relative ${
            tab === 'users' ? 'text-blue-600 border-b-2 border-blue-600' : 'text-slate-500 hover:text-slate-800'
          }`}
        >
          Client Portal Users ({portalUsers.length})
        </button>
      </div>

      {tab === 'requests' ? (
        <div className="space-y-6">
          {/* KPI Cards */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-sm">
              <div className="text-xs font-semibold text-slate-500 uppercase">Total Tickets</div>
              <div className="mt-2 text-2xl font-bold text-slate-900">{totalCount}</div>
            </div>
            <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-sm">
              <div className="text-xs font-semibold text-slate-500 uppercase">New / Submitted</div>
              <div className="mt-2 text-2xl font-bold text-amber-600">{submittedCount}</div>
            </div>
            <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-sm">
              <div className="text-xs font-semibold text-slate-500 uppercase">In Progress</div>
              <div className="mt-2 text-2xl font-bold text-blue-600">{inProgressCount}</div>
            </div>
            <div className="p-4 rounded-xl bg-white border border-slate-200 shadow-sm">
              <div className="text-xs font-semibold text-slate-500 uppercase">Resolved / Closed</div>
              <div className="mt-2 text-2xl font-bold text-emerald-600">{resolvedCount}</div>
            </div>
          </div>

          {/* Filter Bar */}
          <div className="flex flex-wrap items-center gap-3 p-3 bg-white rounded-xl border border-slate-200 shadow-sm">
            <select
              value={statusFilter}
              onChange={e => setStatusFilter(e.target.value)}
              className="text-xs font-medium border border-slate-200 rounded-lg px-3 py-1.5 bg-slate-50"
            >
              <option value="all">All Statuses</option>
              <option value="submitted">New / Submitted</option>
              <option value="in_progress">In Progress</option>
              <option value="resolved">Resolved</option>
              <option value="closed">Closed</option>
            </select>

            <select
              value={categoryFilter}
              onChange={e => setCategoryFilter(e.target.value)}
              className="text-xs font-medium border border-slate-200 rounded-lg px-3 py-1.5 bg-slate-50"
            >
              <option value="all">All Categories</option>
              <option value="tax_filing">Tax Filing Query</option>
              <option value="notice_reply">Notice Reply</option>
              <option value="certification">Certificate Request</option>
              <option value="advisory">Advisory</option>
              <option value="billing_query">Billing & Fees</option>
              <option value="general">General Inquiry</option>
            </select>
          </div>

          {/* Requests Table */}
          <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
            <table className="w-full text-left text-sm text-slate-600">
              <thead className="bg-slate-50 border-b border-slate-200 text-xs font-semibold uppercase text-slate-500">
                <tr>
                  <th className="px-4 py-3">Ticket #</th>
                  <th className="px-4 py-3">Client Entity</th>
                  <th className="px-4 py-3">Subject & Category</th>
                  <th className="px-4 py-3">Priority</th>
                  <th className="px-4 py-3">Status</th>
                  <th className="px-4 py-3">Submitted</th>
                  <th className="px-4 py-3 text-right">Action</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {loading ? (
                  <tr>
                    <td colSpan={7} className="text-center py-10 text-slate-400">Loading tickets...</td>
                  </tr>
                ) : requests.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="text-center py-10 text-slate-400">No service requests found.</td>
                  </tr>
                ) : (
                  requests.map(r => (
                    <tr key={r.id} className="hover:bg-slate-50/70 transition-colors">
                      <td className="px-4 py-3 font-mono text-xs font-semibold text-blue-600">
                        {r.requestNumber}
                      </td>
                      <td className="px-4 py-3 text-xs font-medium text-slate-800">
                        {r.clientName}
                      </td>
                      <td className="px-4 py-3 text-xs">
                        <div className="font-medium text-slate-900">{r.subject}</div>
                        <span className="inline-block px-1.5 py-0.5 rounded text-[11px] font-medium bg-slate-100 text-slate-600 mt-0.5">
                          {r.category.replace('_', ' ')}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-xs">
                        {r.priority === 'urgent' ? (
                          <span className="px-2 py-0.5 rounded-full font-bold text-[11px] bg-red-100 text-red-800">Urgent</span>
                        ) : r.priority === 'high' ? (
                          <span className="px-2 py-0.5 rounded-full font-semibold text-[11px] bg-amber-100 text-amber-800">High</span>
                        ) : (
                          <span className="px-2 py-0.5 rounded-full font-medium text-[11px] bg-slate-100 text-slate-700">{r.priority}</span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-xs">
                        {r.status === 'submitted' ? (
                          <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-amber-50 text-amber-700 border border-amber-200">
                            New
                          </span>
                        ) : r.status === 'in_progress' ? (
                          <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-blue-50 text-blue-700 border border-blue-200">
                            In Progress
                          </span>
                        ) : (
                          <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
                            {r.status}
                          </span>
                        )}
                      </td>
                      <td className="px-4 py-3 text-xs text-slate-500">
                        {new Date(r.createdAt).toLocaleDateString()}
                      </td>
                      <td className="px-4 py-3 text-right text-xs">
                        <button
                          onClick={() => {
                            setSelectedRequest(r)
                            setStatusUpdate(r.status)
                            setAssignedStaffId(r.assignedUserId || meId)
                            setResolutionNotes(r.resolutionNotes || '')
                          }}
                          className="px-3 py-1 bg-blue-50 hover:bg-blue-100 text-blue-700 font-semibold rounded-lg"
                        >
                          View / Reply
                        </button>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        /* Client Portal Users */
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
          <table className="w-full text-left text-sm text-slate-600">
            <thead className="bg-slate-50 border-b border-slate-200 text-xs font-semibold uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3">Portal User</th>
                <th className="px-4 py-3">Client Organization</th>
                <th className="px-4 py-3">Role</th>
                <th className="px-4 py-3">Status</th>
                <th className="px-4 py-3">Last Login</th>
                <th className="px-4 py-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {loading ? (
                <tr>
                  <td colSpan={6} className="text-center py-10 text-slate-400">Loading portal users...</td>
                </tr>
              ) : portalUsers.length === 0 ? (
                <tr>
                  <td colSpan={6} className="text-center py-10 text-slate-400">No client portal users created yet.</td>
                </tr>
              ) : (
                portalUsers.map(u => (
                  <tr key={u.id} className="hover:bg-slate-50/70 transition-colors">
                    <td className="px-4 py-3">
                      <div className="font-medium text-slate-900">{u.name}</div>
                      <div className="text-xs text-slate-500 font-mono">{u.email}</div>
                    </td>
                    <td className="px-4 py-3 text-xs font-medium text-slate-800">
                      {u.clientName || 'Unassigned'}
                    </td>
                    <td className="px-4 py-3 text-xs">
                      <span className="capitalize font-medium text-slate-700">{u.role.replace('_', ' ')}</span>
                    </td>
                    <td className="px-4 py-3 text-xs">
                      {u.isActive ? (
                        <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
                          Active
                        </span>
                      ) : (
                        <span className="px-2 py-0.5 rounded-full text-xs font-semibold bg-rose-50 text-rose-700 border border-rose-200">
                          Deactivated
                        </span>
                      )}
                    </td>
                    <td className="px-4 py-3 text-xs text-slate-500">
                      {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : 'Never logged in'}
                    </td>
                    <td className="px-4 py-3 text-right text-xs space-x-2">
                      {canManagePortal && (
                        <>
                          <button
                            onClick={() => handleToggleUserStatus(u)}
                            className="px-2 py-1 text-slate-600 bg-slate-100 hover:bg-slate-200 rounded font-medium"
                          >
                            {u.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                          <button
                            onClick={() => handleDeleteUser(u.id)}
                            className="text-rose-600 hover:text-rose-800 font-medium px-1.5 py-1"
                          >
                            Delete
                          </button>
                        </>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Ticket Details & Messaging Modal */}
      {selectedRequest && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-2xl rounded-2xl bg-white p-6 shadow-xl border border-slate-100 max-h-[90vh] flex flex-col">
            <div className="flex items-start justify-between pb-3 border-b border-slate-100">
              <div>
                <div className="flex items-center gap-2">
                  <span className="font-mono text-sm font-bold text-blue-600">{selectedRequest.requestNumber}</span>
                  <span className="text-xs font-semibold px-2 py-0.5 rounded bg-slate-100 text-slate-700 uppercase">
                    {selectedRequest.category.replace('_', ' ')}
                  </span>
                  <span className="text-xs font-bold px-2 py-0.5 rounded bg-amber-100 text-amber-800 uppercase">
                    {selectedRequest.priority}
                  </span>
                </div>
                <h2 className="text-lg font-bold text-slate-900 mt-1">{selectedRequest.subject}</h2>
                <div className="text-xs text-slate-500">Client: {selectedRequest.clientName}</div>
              </div>
              <button onClick={() => setSelectedRequest(null)} className="text-slate-400 hover:text-slate-600 text-lg">✕</button>
            </div>

            {/* Status & Assignee Bar */}
            {canManageRequests && (
              <div className="mt-3 p-3 bg-slate-50 rounded-xl border border-slate-200 flex flex-wrap items-center justify-between gap-3 text-xs">
                <div className="flex items-center gap-2">
                  <span className="font-semibold text-slate-700">Status:</span>
                  <select
                    value={statusUpdate}
                    onChange={e => setStatusUpdate(e.target.value)}
                    className="border border-slate-300 rounded p-1 bg-white"
                  >
                    <option value="submitted">Submitted</option>
                    <option value="in_progress">In Progress</option>
                    <option value="resolved">Resolved</option>
                    <option value="closed">Closed</option>
                  </select>
                </div>

                <div className="flex items-center gap-2">
                  <span className="font-semibold text-slate-700">Assign Staff:</span>
                  <select
                    value={assignedStaffId}
                    onChange={e => setAssignedStaffId(e.target.value)}
                    className="border border-slate-300 rounded p-1 bg-white"
                  >
                    <option value="">(Unassigned)</option>
                    {staffUsers.map(u => (
                      <option key={u.id} value={u.id}>{u.name}</option>
                    ))}
                  </select>
                </div>

                <button
                  onClick={handleUpdateStatus}
                  className="px-3 py-1 bg-blue-600 text-white rounded font-semibold hover:bg-blue-700"
                >
                  Save Status
                </button>
              </div>
            )}

            {/* Message Thread */}
            <div className="my-4 flex-1 overflow-y-auto space-y-3 pr-2">
              {selectedRequest.messages.map((m, idx) => {
                const isStaff = m.senderType === 'staff'
                return (
                  <div
                    key={m.id || idx}
                    className={`p-3 rounded-xl max-w-[85%] text-xs ${
                      isStaff
                        ? 'ml-auto bg-blue-50 border border-blue-200 text-slate-800'
                        : 'mr-auto bg-slate-100 border border-slate-200 text-slate-800'
                    }`}
                  >
                    <div className="flex items-center justify-between gap-4 font-semibold text-[11px] mb-1">
                      <span>{isStaff ? '💼 Staff: ' : '👤 Client: '}{m.senderName}</span>
                      <span className="text-slate-400 font-normal">{new Date(m.createdAt).toLocaleString()}</span>
                    </div>
                    <div className="whitespace-pre-wrap">{m.message}</div>
                  </div>
                )
              })}
            </div>

            {/* Quick Reply Form */}
            {canManageRequests && (
              <form onSubmit={handleSendReply} className="pt-3 border-t border-slate-100 flex gap-2">
                <input
                  type="text"
                  required
                  placeholder="Type reply to client..."
                  value={replyText}
                  onChange={e => setReplyText(e.target.value)}
                  className="flex-1 text-xs border border-slate-300 rounded-lg p-2 focus:ring-1 focus:ring-blue-500"
                />
                <button
                  type="submit"
                  className="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg font-semibold text-xs shadow-sm"
                >
                  Send Reply
                </button>
              </form>
            )}
          </div>
        </div>
      )}

      {/* Invite Client User Modal */}
      {showInviteModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4">
          <div className="w-full max-w-md rounded-2xl bg-white p-6 shadow-xl border border-slate-100">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h2 className="text-base font-bold text-slate-900">Invite Client to Portal</h2>
              <button onClick={() => setShowInviteModal(false)} className="text-slate-400 hover:text-slate-600">✕</button>
            </div>

            <form onSubmit={handleCreateUser} className="mt-4 space-y-3">
              <div>
                <label className="block text-xs font-medium text-slate-700">Client Organization *</label>
                <select
                  required
                  value={inviteClientId}
                  onChange={e => setInviteClientId(e.target.value)}
                  className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2 bg-white"
                >
                  <option value="">Select Client</option>
                  {clients.map(c => (
                    <option key={c.id} value={c.id}>{c.name} ({c.code})</option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700">Contact Person Name *</label>
                <input
                  type="text"
                  required
                  value={inviteName}
                  onChange={e => setInviteName(e.target.value)}
                  placeholder="e.g. Ramesh Kumar"
                  className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700">Email Address (Login ID) *</label>
                <input
                  type="email"
                  required
                  value={inviteEmail}
                  onChange={e => setInviteEmail(e.target.value)}
                  placeholder="director@client.com"
                  className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700">Phone Number</label>
                <input
                  type="text"
                  value={invitePhone}
                  onChange={e => setInvitePhone(e.target.value)}
                  placeholder="+919876543210"
                  className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2"
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700">Portal Access Role *</label>
                <select
                  value={inviteRole}
                  onChange={e => setInviteRole(e.target.value)}
                  className="mt-1 w-full text-xs border border-slate-300 rounded-lg p-2 bg-white"
                >
                  <option value="client_admin">Client Admin (Submit & Manage All)</option>
                  <option value="client_viewer">Client Viewer (Read Only)</option>
                </select>
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-700">Initial Password *</label>
                <input
                  type="text"
                  required
                  value={invitePassword}
                  onChange={e => setInvitePassword(e.target.value)}
                  className="mt-1 w-full text-xs font-mono border border-slate-300 rounded-lg p-2"
                />
              </div>

              <div className="flex justify-end gap-2 pt-3 border-t border-slate-100">
                <button
                  type="button"
                  onClick={() => setShowInviteModal(false)}
                  className="px-4 py-2 text-xs font-medium text-slate-600 hover:bg-slate-100 rounded-lg"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 text-xs font-semibold text-white bg-blue-600 hover:bg-blue-700 rounded-lg shadow-sm"
                >
                  Create Portal Account
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  )
}
