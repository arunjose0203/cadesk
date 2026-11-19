import { Fragment, useCallback, useEffect, useState } from 'react'
import { api, errorList, MESSAGE_NAMES, type MessageTemplate, type NotificationItem, type NotificationPage } from './api'

const STATUS_CLASS: Record<string, string> = { sent: 'create', failed: 'delete', skipped: 'delete', cancelled: '', queued: '', sending: '' }

/** Settings → Reminders: run now, test email, message texts, and the log of everything sent. */
export default function RemindersPanel() {
  const [log, setLog] = useState<NotificationItem[]>([])
  const [status, setStatus] = useState('')
  const [templates, setTemplates] = useState<MessageTemplate[]>([])
  const [edit, setEdit] = useState<MessageTemplate | null>(null)
  const [preview, setPreview] = useState<{ subject?: string | null; body: string } | null>(null)
  const [open, setOpen] = useState<string | null>(null)
  const [msg, setMsg] = useState<string[] | null>(null)

  const load = useCallback(async () => {
    setLog((await api<NotificationPage>(`/api/notifications?take=100${status ? `&status=${status}` : ''}`)).items)
    setTemplates(await api<MessageTemplate[]>('/api/notification-templates'))
  }, [status])
  useEffect(() => { load().catch(() => {}) }, [load])

  const run = async (fn: () => Promise<string | void>) => {
    setMsg(null)
    try { const m = await fn(); if (m) setMsg([m]); await load() } catch (e) { setMsg(errorList(e)) }
  }

  return (
    <>
      <section className="card">
        <div className="row spread">
          <h2>Reminders</h2>
          <div className="row">
            <button className="ghost" onClick={() => run(async () => { await api('/api/notifications/test-email', { method: 'POST' }); return 'Test email sent to you. Check the log below.' })}>Send me a test email</button>
            <button onClick={() => run(async () => {
              const r = await api<{ queued: number; sent: number; failed: number; waiting: number }>('/api/reminders/run', { method: 'POST' })
              return `${r.queued} new message(s): ${r.sent} sent, ${r.waiting} waiting for quiet hours or retry, ${r.failed} failed.`
            })}>Send due reminders now</button>
          </div>
        </div>
        <p className="muted small">Reminders go out automatically every few minutes, outside your quiet hours. Each one is sent once.</p>
        {msg && <ul className="warn small">{msg.map(m => <li key={m}>{m}</li>)}</ul>}
      </section>

      <section className="card">
        <h2>Messages</h2>
        <table>
          <tbody>
            {templates.map(t => (
              <tr key={`${t.key}-${t.channel}`}>
                <td><b>{MESSAGE_NAMES[t.key] ?? t.key}</b> <span className="tag">{t.channel}</span> {t.firmOwn && <span className="tag">your firm</span>}
                  <div className="muted small">{t.subject ?? t.waTemplate}</div></td>
                <td className="nowrap">
                  <button className="ghost" onClick={() => { setEdit({ ...t }); setPreview(null) }}>Edit</button>
                  {t.firmOwn && <button className="ghost" onClick={() => run(() => api(`/api/notification-templates/${t.key}/${t.channel}`, { method: 'DELETE' }).then(() => undefined))}>Use default</button>}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {edit && (
          <div className="notice" style={{ marginTop: 12 }}>
            <h3>{MESSAGE_NAMES[edit.key]} · {edit.channel}</h3>
            <p className="muted small">You can use: {edit.placeholders.map(p => `{{${p}}}`).join(' ')}</p>
            {edit.channel === 'email'
              ? <label className="field">Subject<input value={edit.subject ?? ''} onChange={e => setEdit({ ...edit, subject: e.target.value })} /></label>
              : <div className="grid">
                  <label className="field">Approved WhatsApp template name<input value={edit.waTemplate ?? ''} onChange={e => setEdit({ ...edit, waTemplate: e.target.value })} /></label>
                  <label className="field">Its variables in order ({'{{1}}'}, {'{{2}}'} …)<input value={(edit.waParams ?? []).join(', ')}
                    onChange={e => setEdit({ ...edit, waParams: e.target.value.split(',').map(s => s.trim()).filter(Boolean) })} /></label>
                </div>}
            <label className="field" style={{ marginTop: 8 }}>{edit.channel === 'email' ? 'Message' : 'Text (for the log; WhatsApp sends the approved template)'}
              <textarea rows={8} value={edit.body} onChange={e => setEdit({ ...edit, body: e.target.value })} /></label>
            <div className="row" style={{ marginTop: 8 }}>
              <button onClick={() => run(async () => {
                await api(`/api/notification-templates/${edit.key}/${edit.channel}`, { method: 'PUT',
                  body: JSON.stringify({ subject: edit.subject, body: edit.body, waTemplate: edit.waTemplate, waParams: edit.waParams }) })
                setEdit(null)
                return 'Saved for your firm.'
              })}>Save</button>
              <button className="ghost" onClick={() => run(async () => {
                setPreview(await api(`/api/notification-templates/${edit.key}/preview`, { method: 'POST', body: JSON.stringify({ subject: edit.subject, body: edit.body }) }))
              })}>Preview</button>
              <button className="ghost" onClick={() => setEdit(null)}>Cancel</button>
            </div>
            {preview && <div className="preview">{preview.subject && <b>{preview.subject}</b>}<pre>{preview.body}</pre></div>}
          </div>
        )}
      </section>

      <section className="card">
        <div className="row spread">
          <h2>Sent and waiting</h2>
          <select value={status} onChange={e => setStatus(e.target.value)}>
            <option value="">All</option><option value="queued">Waiting</option><option value="sent">Sent</option>
            <option value="failed">Failed</option><option value="skipped">Skipped</option>
          </select>
        </div>
        <table>
          <thead><tr><th>When</th><th>To</th><th>Message</th><th>Status</th><th /></tr></thead>
          <tbody>
            {log.map(n => (
              <Fragment key={n.id}>
                <tr className="clickable" onClick={() => setOpen(open === n.id ? null : n.id)}>
                  <td className="nowrap small">{new Date(n.sentAt ?? n.scheduledAt).toLocaleString()}</td>
                  <td>{n.recipientName}<div className="muted small">{n.channel} · {n.toAddress}{n.clientName && n.clientName !== n.recipientName ? ` · ${n.clientName}` : ''}</div></td>
                  <td>{MESSAGE_NAMES[n.templateKey] ?? n.templateKey}<div className="muted small">{n.subject}</div></td>
                  <td><span className={`tag ${STATUS_CLASS[n.status]}`}>{n.status}</span>
                    {n.lastError && <div className="error small">{n.lastError}</div>}</td>
                  <td className="nowrap" onClick={e => e.stopPropagation()}>
                    {(n.status === 'failed' || n.status === 'skipped' || n.status === 'cancelled') &&
                      <button className="ghost" onClick={() => run(() => api(`/api/notifications/${n.id}/retry`, { method: 'POST' }).then(() => undefined))}>Send again</button>}
                    {n.status === 'queued' &&
                      <button className="ghost" onClick={() => run(() => api(`/api/notifications/${n.id}/cancel`, { method: 'POST' }).then(() => undefined))}>Cancel</button>}
                  </td>
                </tr>
                {open === n.id && <tr><td colSpan={5}><pre className="preview">{n.body}</pre></td></tr>}
              </Fragment>
            ))}
            {log.length === 0 && <tr><td colSpan={5} className="muted">Nothing yet.</td></tr>}
          </tbody>
        </table>
      </section>
    </>
  )
}
