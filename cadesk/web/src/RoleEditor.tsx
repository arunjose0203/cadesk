import { useEffect, useMemo, useState } from 'react'
import { api, ApiError, type RoleItem } from './api'

type Permission = { code: string; area: string; description: string }

function errorText(e: unknown) {
  if (e instanceof ApiError) return e.errors ? Object.values(e.errors).flat().join(' ') : e.message
  return 'Could not save.'
}

/** Create, change and delete custom roles. Built-in roles are shown but fixed. */
export default function RoleEditor({ roles, myPermissions, onChanged }: {
  roles: RoleItem[]; myPermissions: string[]; onChanged: () => void
}) {
  const [perms, setPerms] = useState<Permission[]>([])
  const [editing, setEditing] = useState<RoleItem | null>(null)
  const [name, setName] = useState('')
  const [chosen, setChosen] = useState<string[]>([])
  const [error, setError] = useState<string | null>(null)

  useEffect(() => { api<Permission[]>('/api/permissions').then(setPerms).catch(() => {}) }, [])
  const byArea = useMemo(() => perms.reduce<Record<string, Permission[]>>((acc, p) => {
    (acc[p.area] ??= []).push(p); return acc
  }, {}), [perms])

  const start = (r: RoleItem | null) => {
    setEditing(r); setName(r?.name ?? ''); setChosen(r?.permissions ?? []); setError(null)
  }
  const save = async () => {
    setError(null)
    try {
      const body = JSON.stringify({ name, permissions: chosen })
      if (editing) await api(`/api/roles/${editing.id}`, { method: 'PUT', body })
      else await api('/api/roles', { method: 'POST', body })
      start(null); setName(''); onChanged()
    } catch (e) { setError(errorText(e)) }
  }
  const remove = async (r: RoleItem) => {
    setError(null)
    try { await api(`/api/roles/${r.id}`, { method: 'DELETE' }); onChanged() } catch (e) { setError(errorText(e)) }
  }
  const toggle = (code: string) => setChosen(c => c.includes(code) ? c.filter(x => x !== code) : [...c, code])

  return (
    <section className="card">
      <h2>{editing ? `Edit role: ${editing.name}` : 'New role'}</h2>
      <p className="muted small">You can only give permissions you have yourself.</p>
      <input placeholder="Role name (e.g. Senior Accountant)" value={name} onChange={e => setName(e.target.value)} />
      <div className="perm-grid">
        {Object.entries(byArea).map(([area, list]) => (
          <fieldset key={area}>
            <legend>{area}</legend>
            {list.map(p => (
              <label key={p.code} className="row small" title={p.code}>
                <input type="checkbox" checked={chosen.includes(p.code)} disabled={!myPermissions.includes(p.code)}
                  onChange={() => toggle(p.code)} /> {p.description}
              </label>
            ))}
          </fieldset>
        ))}
      </div>
      <div className="row">
        <button onClick={save} disabled={!name.trim()}>{editing ? 'Save role' : 'Create role'}</button>
        {editing && <button className="ghost" onClick={() => start(null)}>Cancel</button>}
      </div>
      {error && <p className="error small">{error}</p>}
      <h3 className="small">Custom roles</h3>
      <ul className="small">
        {roles.filter(r => !r.isSystem).map(r => (
          <li key={r.id}>
            {r.name} ({r.userCount} users)
            <button className="ghost" onClick={() => start(r)}>Edit</button>
            <button className="ghost" onClick={() => remove(r)}>Delete</button>
          </li>
        ))}
        {roles.every(r => r.isSystem) && <li className="muted">None yet.</li>}
      </ul>
    </section>
  )
}
