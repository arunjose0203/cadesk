import { useState } from 'react'
import { api, errorList, type Client, type ClientFormInfo, type CustomField, type Group, type SaveClient, type UserListItem } from './api'

type Props = {
  info: ClientFormInfo
  groups: Group[]
  users: UserListItem[]
  client?: Client
  onSaved: (c: Client) => void
  onCancel: () => void
}

const empty = (info: ClientFormInfo): SaveClient => ({ code: '', name: '', entityType: info.entityTypes[0], taxIds: {}, custom: {} })

/** Add / edit a client. Fields, tax IDs and custom fields come from the firm's settings (GET /api/clients/form). */
export default function ClientForm({ info, groups, users, client, onSaved, onCancel }: Props) {
  const [f, setF] = useState<SaveClient>(() => client
    ? { ...client, taxIds: { ...client.taxIds }, custom: { ...(client.custom ?? {}) } }
    : empty(info))
  const [errors, setErrors] = useState<string[] | null>(null)
  const [warnings, setWarnings] = useState<string[] | null>(null)
  const [saving, setSaving] = useState(false)
  const required = new Set(info.requiredFields.map(r => r.toLowerCase()))
  const star = (k: string) => (required.has(k.toLowerCase()) ? ' *' : '')

  const set = (patch: Partial<SaveClient>) => setF({ ...f, ...patch })
  const setTax = (code: string, v: string) => set({ taxIds: { ...f.taxIds, [code]: v } })
  const setCustom = (key: string, v: string) => set({ custom: { ...f.custom, [key]: v } })

  const save = async () => {
    setSaving(true); setErrors(null); setWarnings(null)
    try {
      const body = JSON.stringify({ ...f, code: f.code || null, groupId: f.groupId || null, assignedUserId: f.assignedUserId || null })
      const saved = client
        ? await api<Client>(`/api/clients/${client.id}`, { method: 'PUT', body })
        : await api<Client>('/api/clients', { method: 'POST', body })
      if (saved.warnings?.length) setWarnings(saved.warnings)
      onSaved(saved)
      if (!client) setF(empty(info))
    } catch (e) {
      setErrors(errorList(e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="stack">
      <div className="grid">
        <label className="field">Code
          <input placeholder={`auto: ${info.nextCodeExample}`} value={f.code ?? ''} onChange={e => set({ code: e.target.value })} />
        </label>
        <label className="field">Name *
          <input value={f.name} onChange={e => set({ name: e.target.value })} />
        </label>
        <label className="field">Legal name
          <input value={f.legalName ?? ''} onChange={e => set({ legalName: e.target.value })} />
        </label>
        <label className="field">Entity type *
          <select value={f.entityType ?? ''} onChange={e => set({ entityType: e.target.value })}>
            {info.entityTypes.map(t => <option key={t}>{t}</option>)}
          </select>
        </label>
        {info.taxIdTypes.map(t => (
          <label className="field" key={t.code}>{t.label}{star(t.code)}
            <input placeholder={t.example ?? ''} value={f.taxIds?.[t.code] ?? ''} onChange={e => setTax(t.code, e.target.value)} />
          </label>
        ))}
        <label className="field">Email{star('email')}
          <input type="email" value={f.email ?? ''} onChange={e => set({ email: e.target.value })} />
        </label>
        <label className="field">Phone{star('phone')}
          <input value={f.phone ?? ''} onChange={e => set({ phone: e.target.value })} />
        </label>
        <label className="field">Group
          <select value={f.groupId ?? ''} onChange={e => set({ groupId: e.target.value })}>
            <option value="">— none —</option>
            {groups.map(g => <option key={g.id} value={g.id}>{g.name}</option>)}
          </select>
        </label>
        {users.length > 0 && (
          <label className="field">Assigned to
            <select value={f.assignedUserId ?? ''} onChange={e => set({ assignedUserId: e.target.value })}>
              <option value="">— nobody —</option>
              {users.filter(u => u.isActive).map(u => <option key={u.id} value={u.id}>{u.name}</option>)}
            </select>
          </label>
        )}
        {info.customFields.map(cf => (
          <CustomInput key={cf.key} field={cf} value={f.custom?.[cf.key] ?? ''} onChange={v => setCustom(cf.key, v)} />
        ))}
      </div>
      {client && (
        <label className="row small"><input type="checkbox" checked={f.isActive ?? true} onChange={e => set({ isActive: e.target.checked })} /> Active client</label>
      )}
      <div className="row">
        <button onClick={save} disabled={saving}>{client ? 'Save changes' : 'Add client'}</button>
        <button className="ghost" onClick={onCancel}>Cancel</button>
        <span className="muted small">* required by your firm's settings</span>
      </div>
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
      {warnings && <ul className="warn">{warnings.map(m => <li key={m}>{m}</li>)}</ul>}
    </div>
  )
}

function CustomInput({ field, value, onChange }: { field: CustomField; value: string; onChange: (v: string) => void }) {
  const label = `${field.label}${field.isRequired ? ' *' : ''}`
  if (field.fieldType === 'dropdown')
    return <label className="field">{label}
      <select value={value} onChange={e => onChange(e.target.value)}>
        <option value="">—</option>
        {(field.options ?? []).map(o => <option key={o}>{o}</option>)}
      </select>
    </label>
  if (field.fieldType === 'bool')
    return <label className="field">{label}
      <select value={value} onChange={e => onChange(e.target.value)}>
        <option value="">—</option><option value="true">Yes</option><option value="false">No</option>
      </select>
    </label>
  return <label className="field">{label}
    <input type={field.fieldType === 'date' ? 'date' : field.fieldType === 'number' ? 'number' : 'text'}
      value={value} onChange={e => onChange(e.target.value)} />
  </label>
}
