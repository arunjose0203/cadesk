import { useCallback, useEffect, useState } from 'react'
import { api, errorList, type TaskTemplate } from './api'

/** The checklist each filing's task starts with. The firm can replace any default with its own. */
export default function TemplatesEditor() {
  const [items, setItems] = useState<TaskTemplate[]>([])
  const [edit, setEdit] = useState<{ code: string; name: string; text: string } | null>(null)
  const [errors, setErrors] = useState<string[] | null>(null)
  const load = useCallback(async () => setItems(await api<TaskTemplate[]>('/api/task-templates')), [])
  useEffect(() => { load().catch(() => {}) }, [load])

  const save = async () => {
    if (!edit) return
    setErrors(null)
    try {
      await api(`/api/task-templates/${edit.code}`, { method: 'PUT', body: JSON.stringify({ name: edit.name, items: edit.text.split('\n') }) })
      setEdit(null); load()
    } catch (e) { setErrors(errorList(e)) }
  }

  return (
    <section className="card">
      <h2>Task checklists</h2>
      <p className="muted small">Every filing's task starts with this checklist. Edit one to use your firm's own steps.</p>
      <table>
        <tbody>
          {items.filter(t => t.typeCode).map(t => (
            <tr key={t.id}>
              <td><b>{t.name}</b> {t.firmOwn ? <span className="tag">your firm</span> : <span className="tag create">default</span>}
                <div className="muted small">{t.items.join(' → ')}</div></td>
              <td className="nowrap">
                <button className="ghost" onClick={() => setEdit({ code: t.typeCode!, name: t.name, text: t.items.join('\n') })}>Edit</button>
                {t.firmOwn && <button className="ghost" onClick={async () => { await api(`/api/task-templates/${t.typeCode}`, { method: 'DELETE' }); load() }}>Use default</button>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {edit && (
        <div className="notice" style={{ marginTop: 12 }}>
          <label className="field">Name<input value={edit.name} onChange={e => setEdit({ ...edit, name: e.target.value })} /></label>
          <label className="field" style={{ marginTop: 8 }}>Steps (one per line)<textarea rows={7} value={edit.text} onChange={e => setEdit({ ...edit, text: e.target.value })} /></label>
          <div className="row" style={{ marginTop: 8 }}><button onClick={save}>Save for my firm</button><button className="ghost" onClick={() => setEdit(null)}>Cancel</button></div>
          {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
        </div>
      )}
    </section>
  )
}
