import { useCallback, useEffect, useState } from 'react'
import { api } from './api'
import { show, type Json } from './format'

type AuditItem = {
  id: string; at: string; actorId?: string | null; actorName?: string | null; actorType: string
  entity: string; entityId?: string | null; action: string
  oldValues?: Record<string, Json> | null; newValues?: Record<string, Json> | null; ip?: string | null
}
type AuditPageResult = { items: AuditItem[]; nextBefore?: string | null }

const ENTITIES: Record<string, string> = {
  clients: 'Clients', users: 'Users', roles: 'Roles', user_roles: 'User roles', role_permissions: 'Role permissions',
  invitations: 'Invitations', config_values: 'Settings', secrets: 'Secrets', user_mfa: 'Two-step sign-in',
}
const HIDDEN = new Set(['Id', 'FirmId', 'CreatedAt', 'CreatedBy', 'UpdatedAt', 'UpdatedBy'])

function Changes({ a }: { a: AuditItem }) {
  const keys = [...new Set([...Object.keys(a.oldValues ?? {}), ...Object.keys(a.newValues ?? {})])].filter(k => !HIDDEN.has(k))
  if (keys.length === 0) return null
  return (
    <ul className="small changes">
      {keys.slice(0, 6).map(k => (
        <li key={k}>
          <strong>{k}</strong>:{' '}
          {a.action === 'update'
            ? <>{show(a.oldValues?.[k] ?? null)} → {show(a.newValues?.[k] ?? null)}</>
            : show((a.newValues ?? a.oldValues)?.[k] ?? null)}
        </li>
      ))}
      {keys.length > 6 && <li className="muted">+{keys.length - 6} more</li>}
    </ul>
  )
}

export default function AuditPage() {
  const [items, setItems] = useState<AuditItem[]>([])
  const [next, setNext] = useState<string | null>(null)
  const [entity, setEntity] = useState('')
  const [action, setAction] = useState('')

  const query = useCallback((before?: string) => {
    const p = new URLSearchParams({ take: '50' })
    if (entity) p.set('entity', entity)
    if (action) p.set('action', action)
    if (before) p.set('before', before)
    return api<AuditPageResult>(`/api/audit-logs?${p}`)
  }, [entity, action])

  useEffect(() => {
    query().then(r => { setItems(r.items); setNext(r.nextBefore ?? null) }).catch(() => {})
  }, [query])

  const more = async () => {
    if (!next) return
    const r = await query(next)
    setItems(i => [...i, ...r.items]); setNext(r.nextBefore ?? null)
  }

  return (
    <section className="card">
      <h2>Audit log</h2>
      <p className="muted small">Every change to your firm's data: who, when, from where, and what changed. Entries can't be edited or deleted.</p>
      <div className="row">
        <select value={entity} onChange={e => setEntity(e.target.value)}>
          <option value="">All records</option>
          {Object.entries(ENTITIES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        <select value={action} onChange={e => setAction(e.target.value)}>
          <option value="">All actions</option>
          {['create', 'update', 'delete', 'revoke'].map(a => <option key={a}>{a}</option>)}
        </select>
      </div>
      <table>
        <thead><tr><th>When</th><th>Who</th><th>What</th><th>Details</th></tr></thead>
        <tbody>
          {items.map(a => (
            <tr key={a.id}>
              <td className="small nowrap">{new Date(a.at).toLocaleString()}</td>
              <td className="small">{a.actorName ?? (a.actorType === 'system' ? 'System' : 'Unknown')}{a.ip && <div className="muted">{a.ip}</div>}</td>
              <td><span className={`tag ${a.action}`}>{a.action}</span> {ENTITIES[a.entity] ?? a.entity}</td>
              <td><Changes a={a} /></td>
            </tr>
          ))}
          {items.length === 0 && <tr><td colSpan={4} className="muted">Nothing recorded yet.</td></tr>}
        </tbody>
      </table>
      {next && <button className="ghost" onClick={more}>Load more</button>}
    </section>
  )
}
