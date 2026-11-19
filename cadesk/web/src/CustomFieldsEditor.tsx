import { useCallback, useEffect, useState } from 'react'
import { api, errorList, type CustomField } from './api'

const TYPES: Record<CustomField['fieldType'], string> = { text: 'Text', number: 'Number', date: 'Date', dropdown: 'Dropdown', bool: 'Yes / No' }
const blank = { key: '', label: '', fieldType: 'text' as CustomField['fieldType'], options: '', isRequired: false, validationRegex: '' }

/** Firm-defined extra client fields. Key and type are fixed once created; fields are switched off, never deleted. */
export default function CustomFieldsEditor() {
  const [fields, setFields] = useState<CustomField[]>([])
  const [f, setF] = useState(blank)
  const [errors, setErrors] = useState<string[] | null>(null)

  const load = useCallback(async () => setFields(await api<CustomField[]>('/api/custom-fields')), [])
  useEffect(() => { load().catch(() => {}) }, [load])

  const run = async (fn: () => Promise<unknown>) => {
    setErrors(null)
    try { await fn(); await load() } catch (e) { setErrors(errorList(e)) }
  }
  const body = (x: CustomField) => JSON.stringify(x)

  const add = () => run(async () => {
    await api('/api/custom-fields', {
      method: 'POST',
      body: JSON.stringify({
        ...f, options: f.fieldType === 'dropdown' ? f.options.split(',').map(s => s.trim()).filter(Boolean) : null,
        validationRegex: f.validationRegex || null, sortOrder: (fields.length + 1) * 10, isActive: true,
      }),
    })
    setF(blank)
  })

  return (
    <section className="card">
      <h2>Custom client fields</h2>
      <p className="muted small">Extra fields on every client (e.g. Udyam number, file location, service plan). They appear on the client form and in the import template.</p>
      <table>
        <thead><tr><th>Label</th><th>Key</th><th>Type</th><th>Required</th><th /></tr></thead>
        <tbody>
          {fields.map(x => (
            <tr key={x.id} className={x.isActive ? '' : 'muted'}>
              <td>{x.label}{x.options?.length ? <div className="muted small">{x.options.join(', ')}</div> : null}</td>
              <td><code>{x.key}</code></td>
              <td>{TYPES[x.fieldType]}</td>
              <td><input type="checkbox" checked={x.isRequired} disabled={!x.isActive}
                onChange={e => run(() => api(`/api/custom-fields/${x.id}`, { method: 'PUT', body: body({ ...x, isRequired: e.target.checked }) }))} /></td>
              <td><button className="ghost" onClick={() => run(() => api(`/api/custom-fields/${x.id}`, { method: 'PUT', body: body({ ...x, isActive: !x.isActive }) }))}>
                {x.isActive ? 'Switch off' : 'Switch on'}</button></td>
            </tr>
          ))}
          {fields.length === 0 && <tr><td colSpan={5} className="muted">No custom fields yet.</td></tr>}
        </tbody>
      </table>
      <div className="grid" style={{ marginTop: 12 }}>
        <input placeholder="Label (e.g. Udyam number)" value={f.label}
          onChange={e => setF({ ...f, label: e.target.value, key: f.key || '' })} />
        <input placeholder="key (e.g. udyam_no)" value={f.key} onChange={e => setF({ ...f, key: e.target.value.toLowerCase() })} />
        <select value={f.fieldType} onChange={e => setF({ ...f, fieldType: e.target.value as CustomField['fieldType'] })}>
          {Object.entries(TYPES).map(([k, v]) => <option key={k} value={k}>{v}</option>)}
        </select>
        {f.fieldType === 'dropdown' && <input placeholder="Choices, comma separated" value={f.options} onChange={e => setF({ ...f, options: e.target.value })} />}
        {f.fieldType === 'text' && <input placeholder="Format rule (optional regex)" value={f.validationRegex} onChange={e => setF({ ...f, validationRegex: e.target.value })} />}
        <label className="row small"><input type="checkbox" checked={f.isRequired} onChange={e => setF({ ...f, isRequired: e.target.checked })} /> Required</label>
        <button onClick={add} disabled={!f.label.trim() || !f.key.trim()}>Add field</button>
      </div>
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
    </section>
  )
}
