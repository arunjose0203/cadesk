import { useCallback, useEffect, useMemo, useState } from 'react'
import { api, ApiError } from './api'

import { show, type Json, type Schema } from './format'
import CustomFieldsEditor from './CustomFieldsEditor'
import HolidaysEditor from './HolidaysEditor'
import TemplatesEditor from './TemplatesEditor'
import RemindersPanel from './RemindersPanel'
import RetentionPanel from './RetentionPanel'

export type SettingItem = {
  key: string; area: string; label: string; valueType: string; schema: Schema; sortOrder: number
  value: Json; source: string; firmValue: Json | null; inherited: Json; inheritedSource: string
  isLocked: boolean; lockedBy: string | null; editable: boolean; lockedForLowerLevels: boolean
  clientOverrideAllowed: boolean; userOverrideAllowed: boolean; capped: boolean
}
type Change = { id: string; key: string; level: string; oldValue: Json | null; newValue: Json | null; reason?: string | null; changedAt: string }
type SecretInfo = { name: string; createdAt: string; rotatedAt?: string | null }

const AREAS: Record<string, string> = {
  general: 'General', clients: 'Clients', documents: 'Documents', ai: 'AI extraction', compliance: 'Deadlines',
  reminders: 'Reminders', tasks: 'Tasks', tax: 'Tax', billing: 'Billing', einvoicing: 'E-invoicing',
  integrations: 'Integrations', security: 'Security',
}
const SOURCE: Record<string, string> = {
  default: 'Default', platform: 'Platform', country: 'Country default', plan: 'Plan', firm: 'Set by your firm',
}

function errorText(e: unknown) {
  if (e instanceof ApiError) return e.errors ? Object.values(e.errors).flat().join(' ') : e.message
  return 'Could not save.'
}

/** Picks an input to match the setting's schema. */
export function ValueEditor({ schema, value, onChange }: { schema: Schema; value: Json; onChange: (v: Json) => void }) {
  if (schema.type === 'boolean')
    return <label className="row"><input type="checkbox" checked={value === true} onChange={e => onChange(e.target.checked)} /> {value ? 'Yes' : 'No'}</label>
  if (schema.enum)
    return <select value={String(value)} onChange={e => onChange(e.target.value)}>
      {schema.enum.map(o => <option key={String(o)} value={String(o)}>{String(o)}</option>)}
    </select>
  if (schema.type === 'integer' || schema.type === 'number')
    return <input type="number" min={schema.minimum} max={schema.maximum} step={schema.type === 'integer' ? 1 : 0.01}
      value={value as number} onChange={e => onChange(e.target.value === '' ? '' : Number(e.target.value))} />
  if (schema.type === 'array' && schema.items?.enum) {
    const list = (value as Json[]) ?? []
    return <div className="row">
      {schema.items.enum.map(o => (
        <label key={String(o)} className="row small">
          <input type="checkbox" checked={list.includes(o)}
            onChange={e => onChange(e.target.checked ? [...list, o] : list.filter(x => x !== o))} /> {String(o)}
        </label>
      ))}
    </div>
  }
  if (schema.type === 'array') {
    const numeric = schema.items?.type === 'integer' || schema.items?.type === 'number'
    return <input value={((value as Json[]) ?? []).join(', ')} placeholder="comma separated"
      onChange={e => onChange(e.target.value.split(',').map(s => s.trim()).filter(Boolean)
        .map(s => (numeric && !Number.isNaN(Number(s)) ? Number(s) : s)))} />
  }
  return <input value={(value as string) ?? ''} onChange={e => onChange(e.target.value)} />
}

function SettingRow({ item, onSaved }: { item: SettingItem; onSaved: () => void }) {
  const [draft, setDraft] = useState<Json>(item.value)
  const [lock, setLock] = useState(item.lockedForLowerLevels)
  const [error, setError] = useState<string | null>(null)
  const dirty = JSON.stringify(draft) !== JSON.stringify(item.value) || lock !== item.lockedForLowerLevels

  useEffect(() => { setDraft(item.value); setLock(item.lockedForLowerLevels) }, [item])

  const save = async () => {
    setError(null)
    try {
      await api(`/api/settings/${item.key}`, {
        method: 'PUT', body: JSON.stringify({ value: draft, scope: 'firm', lockForLowerLevels: item.clientOverrideAllowed || item.lockedForLowerLevels ? lock : undefined }),
      })
      onSaved()
    } catch (e) { setError(errorText(e)) }
  }
  const reset = async () => {
    setError(null)
    try { await api(`/api/settings/${item.key}`, { method: 'DELETE' }); onSaved() } catch (e) { setError(errorText(e)) }
  }

  return (
    <tr>
      <td>
        <strong className="small">{item.label}</strong>
        <div className="muted small">
          {item.isLocked ? `Fixed by ${item.lockedBy}` : SOURCE[item.source] ?? item.source}
          {item.source === 'firm' && <> · was {show(item.inherited)} ({SOURCE[item.inheritedSource] ?? item.inheritedSource})</>}
          {item.capped && <> · limited by your plan</>}
        </div>
      </td>
      <td>
        {item.editable ? <ValueEditor schema={item.schema} value={draft} onChange={setDraft} /> : <span>{show(item.value)}</span>}
        {(item.clientOverrideAllowed || item.lockedForLowerLevels) && item.editable && (
          <label className="row small muted"><input type="checkbox" checked={lock} onChange={e => setLock(e.target.checked)} /> Same for every client</label>
        )}
        {error && <div className="error small">{error}</div>}
      </td>
      <td className="nowrap">
        {item.editable && <button disabled={!dirty} onClick={save}>Save</button>}
        {item.editable && item.source === 'firm' && <button className="ghost" onClick={reset}>Reset</button>}
      </td>
    </tr>
  )
}

export default function SettingsPage() {
  const [items, setItems] = useState<SettingItem[]>([])
  const [area, setArea] = useState('general')
  const [changes, setChanges] = useState<Change[]>([])
  const [secrets, setSecrets] = useState<SecretInfo[]>([])
  const [secret, setSecret] = useState({ name: '', value: '' })
  const [msg, setMsg] = useState<string | null>(null)

  const load = useCallback(async () => {
    setItems(await api<SettingItem[]>('/api/settings'))
    setChanges(await api<Change[]>('/api/settings/changes?take=20'))
    setSecrets(await api<SecretInfo[]>('/api/settings/secrets'))
  }, [])
  useEffect(() => { load().catch(() => {}) }, [load])

  const areas = useMemo(() => [...new Set(items.map(i => i.area))], [items])
  const label = (key: string) => items.find(i => i.key === key)?.label ?? key

  const undo = async (id: string) => { await api(`/api/settings/changes/${id}/revert`, { method: 'POST' }); load() }
  const saveSecret = async () => {
    setMsg(null)
    try {
      await api(`/api/settings/secrets/${secret.name}`, { method: 'PUT', body: JSON.stringify({ value: secret.value }) })
      setSecret({ name: '', value: '' }); setMsg('Saved. The value is encrypted and can\'t be shown again.'); load()
    } catch (e) { setMsg(errorText(e)) }
  }

  return (
    <>
      <section className="card">
        <h2>Firm settings</h2>
        <nav className="tabs">
          {areas.map(a => <button key={a} className={a === area ? 'active' : ''} onClick={() => setArea(a)}>{AREAS[a] ?? a}</button>)}
        </nav>
        <table>
          <tbody>
            {items.filter(i => i.area === area).map(i => <SettingRow key={i.key} item={i} onSaved={load} />)}
          </tbody>
        </table>
      </section>

      {area === 'clients' && <CustomFieldsEditor />}
      {area === 'compliance' && <HolidaysEditor />}
      {area === 'tasks' && <TemplatesEditor />}
      {area === 'reminders' && <RemindersPanel />}
      {area === 'documents' && <RetentionPanel />}

      {area === 'integrations' || area === 'einvoicing' ? (
        <section className="card">
          <h2>Secrets</h2>
          <p className="muted small">API keys and passwords are encrypted. You can replace or delete them, but not view them.</p>
          <div className="grid">
            <input placeholder="name (e.g. wa_token)" value={secret.name} onChange={e => setSecret({ ...secret, name: e.target.value })} />
            <input type="password" placeholder="value" value={secret.value} onChange={e => setSecret({ ...secret, value: e.target.value })} />
            <button onClick={saveSecret}>Save secret</button>
          </div>
          {msg && <p className="small">{msg}</p>}
          <ul className="small">{secrets.map(s => <li key={s.name}><code>{s.name}</code> · saved {new Date(s.rotatedAt ?? s.createdAt).toLocaleDateString()}</li>)}</ul>
        </section>
      ) : null}

      <section className="card">
        <h2>Recent changes</h2>
        <table>
          <thead><tr><th>When</th><th>Setting</th><th>Level</th><th>From</th><th>To</th><th /></tr></thead>
          <tbody>
            {changes.map(c => (
              <tr key={c.id}>
                <td className="small">{new Date(c.changedAt).toLocaleString()}</td>
                <td>{label(c.key)}</td><td>{c.level}</td>
                <td>{c.oldValue === null ? 'inherited' : show(c.oldValue)}</td>
                <td>{c.newValue === null ? 'inherited' : show(c.newValue)}</td>
                <td><button className="ghost" onClick={() => undo(c.id)}>Undo</button></td>
              </tr>
            ))}
            {changes.length === 0 && <tr><td colSpan={6} className="muted">No changes yet.</td></tr>}
          </tbody>
        </table>
      </section>
    </>
  )
}
