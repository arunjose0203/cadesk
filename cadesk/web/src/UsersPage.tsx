import { useCallback, useEffect, useState } from 'react'
import { api, ApiError, type InvitationItem, type InviteCreated, type RoleItem, type UserListItem, type UserProfile } from './api'
import RoleEditor from './RoleEditor'

type Props = { can: (p: string) => boolean; me: UserProfile }

export default function UsersPage({ can, me }: Props) {
  const [users, setUsers] = useState<UserListItem[]>([])
  const [roles, setRoles] = useState<RoleItem[]>([])
  const [invites, setInvites] = useState<InvitationItem[]>([])
  const [invite, setInvite] = useState({ email: '', name: '', roleId: '' })
  const [link, setLink] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const manage = can('users.manage')

  const load = useCallback(async () => {
    setUsers(await api<UserListItem[]>('/api/users'))
    const r = await api<RoleItem[]>('/api/roles')
    setRoles(r)
    setInvite(i => i.roleId ? i : { ...i, roleId: r.find(x => x.name === 'Staff')?.id ?? r[0]?.id ?? '' })
    if (manage) setInvites(await api<InvitationItem[]>('/api/invitations'))
  }, [manage])

  useEffect(() => { load().catch(() => {}) }, [load])

  const act = async (fn: () => Promise<unknown>) => {
    setError(null)
    try { await fn(); await load() } catch (e) {
      setError(e instanceof ApiError ? (e.errors ? Object.values(e.errors).flat().join(' ') : e.message) : 'Failed')
    }
  }

  const sendInvite = () => act(async () => {
    const r = await api<InviteCreated>('/api/invitations', { method: 'POST', body: JSON.stringify(invite) })
    setLink(`${location.origin}/?invite=${r.inviteToken}`)
    setInvite({ ...invite, email: '', name: '' })
  })

  const setActive = (u: UserListItem, isActive: boolean) =>
    act(() => api(`/api/users/${u.id}`, { method: 'PUT', body: JSON.stringify({ isActive }) }))

  const setRole = (u: UserListItem, roleId: string) =>
    act(() => api(`/api/users/${u.id}`, { method: 'PUT', body: JSON.stringify({ roleIds: [roleId] }) }))

  return (
    <>
      <section className="card">
        <h2>Users</h2>
        {error && <p className="error">{error}</p>}
        <table>
          <thead><tr><th>Name</th><th>Email</th><th>Role</th><th>2-step</th><th>Status</th></tr></thead>
          <tbody>
            {users.map(u => (
              <tr key={u.id}>
                <td>{u.name}{u.id === me.id && <span className="muted"> (you)</span>}</td>
                <td>{u.email}</td>
                <td>
                  {manage ? (
                    <select value={u.roles[0]?.id ?? ''} onChange={e => setRole(u, e.target.value)}>
                      {roles.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
                    </select>
                  ) : u.roles.map(r => r.name).join(', ')}
                </td>
                <td>{u.mfaEnabled ? 'On' : 'Off'}</td>
                <td>
                  {u.isActive ? 'Active' : 'Inactive'}
                  {manage && u.id !== me.id && (
                    <button className="ghost" onClick={() => setActive(u, !u.isActive)}>
                      {u.isActive ? 'Deactivate' : 'Activate'}
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {manage && (
        <section className="card">
          <h2>Invite a team member</h2>
          <div className="grid">
            <input type="email" placeholder="Email" value={invite.email} onChange={e => setInvite({ ...invite, email: e.target.value })} />
            <input placeholder="Name (optional)" value={invite.name} onChange={e => setInvite({ ...invite, name: e.target.value })} />
            <select value={invite.roleId} onChange={e => setInvite({ ...invite, roleId: e.target.value })}>
              {roles.map(r => <option key={r.id} value={r.id}>{r.name}</option>)}
            </select>
            <button onClick={sendInvite}>Create invite</button>
          </div>
          {link && (
            <p className="small">
              Send this link to them (valid 7 days; email sending comes later):<br />
              <code className="break">{link}</code>
            </p>
          )}
          {invites.length > 0 && (
            <table>
              <thead><tr><th>Pending</th><th>Role</th><th>Expires</th><th /></tr></thead>
              <tbody>
                {invites.map(i => (
                  <tr key={i.id}>
                    <td>{i.email}</td><td>{i.roleName}</td><td>{new Date(i.expiresAt).toLocaleDateString()}</td>
                    <td><button className="ghost" onClick={() => act(() => api(`/api/invitations/${i.id}`, { method: 'DELETE' }))}>Revoke</button></td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      )}

      <section className="card">
        <h2>Roles</h2>
        <table>
          <thead><tr><th>Role</th><th>Users</th><th>Permissions</th></tr></thead>
          <tbody>
            {roles.map(r => (
              <tr key={r.id}>
                <td>{r.name}{r.isSystem && <span className="muted"> (built-in)</span>}</td>
                <td>{r.userCount}</td>
                <td className="small">{r.permissions.join(', ')}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {can('roles.manage') && <RoleEditor roles={roles} myPermissions={me.permissions} onChanged={() => load()} />}
    </>
  )
}
