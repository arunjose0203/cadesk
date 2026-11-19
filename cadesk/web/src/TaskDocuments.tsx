import { useCallback, useEffect, useState } from 'react'
import { api, download, errorList, fmtSize, SCAN_STATUS, uploadFile, uploadLinkUrl, type DocumentItem, type DocumentPage, type UploadLinkCreated } from './api'

type Props = {
  taskId: string
  clientId?: string | null
  /** Documents can be added while the task is open (including in review) and removed only while it's being worked on. */
  canAdd: boolean
  canRemove: boolean
  can: (p: string) => boolean
}

/** The documents a task uses: upload into the task's filing folder, link files already in the vault, ask the client for files. */
export default function TaskDocuments({ taskId, clientId, canAdd, canRemove, can }: Props) {
  const [docs, setDocs] = useState<DocumentItem[]>([])
  const [vault, setVault] = useState<DocumentItem[]>([])
  const [pick, setPick] = useState('')
  const [link, setLink] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [errors, setErrors] = useState<string[] | null>(null)
  const upload = can('documents.upload') && canAdd

  const load = useCallback(async () => setDocs(await api<DocumentItem[]>(`/api/tasks/${taskId}/documents`)), [taskId])
  useEffect(() => { load().catch(() => {}) }, [load])
  useEffect(() => {
    if (!clientId || !upload) return
    api<DocumentPage>(`/api/clients/${clientId}/documents?take=200`).then(p => setVault(p.items)).catch(() => {})
  }, [clientId, upload, docs.length])

  const run = async (fn: () => Promise<unknown>) => {
    setErrors(null); setBusy(true)
    try { await fn() } catch (e) { setErrors(errorList(e)) } finally { setBusy(false) }
  }
  const send = (files: FileList | null) => run(async () => {
    for (const f of Array.from(files ?? [])) await uploadFile(`/api/tasks/${taskId}/documents/upload`, f)
    await load()
  })
  const attach = () => run(async () => {
    setDocs(await api<DocumentItem[]>(`/api/tasks/${taskId}/documents`, { method: 'POST', body: JSON.stringify({ documentId: pick }) }))
    setPick('')
  })
  const detach = (id: string) => run(async () =>
    setDocs(await api<DocumentItem[]>(`/api/tasks/${taskId}/documents/${id}`, { method: 'DELETE' })))
  const askClient = () => run(async () => {
    const r = await api<UploadLinkCreated>(`/api/clients/${clientId}/upload-links`, { method: 'POST', body: JSON.stringify({ taskId }) })
    setLink(uploadLinkUrl(r.token))
  })

  if (!can('documents.read')) return null
  const linked = new Set(docs.map(d => d.id))
  const choices = vault.filter(d => !linked.has(d.id) && !d.purgedAt)

  return (
    <>
      <h3 className="small muted" style={{ marginTop: 16 }}>Documents {docs.length > 0 && `(${docs.length})`}</h3>
      <ul className="checklist">
        {docs.map(d => (
          <li key={d.id}>
            <span>
              {d.canDownload
                ? <button className="link" onClick={() => run(() => download(`/api/documents/${d.id}/download`, d.fileName))}>{d.name}</button>
                : <span className={d.purgedAt ? 'muted strike' : ''}>{d.name}</span>}
              <span className="muted small"> · {d.folderName}{d.fy ? ` · FY ${d.fy}` : ''} · {fmtSize(d.sizeBytes)}
                {d.source === 'portal' && ' · from client'}{d.scanStatus !== 'clean' && ` · ${SCAN_STATUS[d.scanStatus]}`}</span>
            </span>
            {canRemove && can('documents.upload') && <button className="ghost small" onClick={() => detach(d.id)}>Remove</button>}
          </li>
        ))}
        {docs.length === 0 && <li className="muted small">No documents linked.</li>}
      </ul>
      {upload && clientId && (
        <div className="row">
          <label className="button-like ghost-like">
            {busy ? 'Uploading…' : 'Upload'}
            <input type="file" multiple hidden onChange={e => { send(e.target.files); e.target.value = '' }} />
          </label>
          {choices.length > 0 && <>
            <select value={pick} onChange={e => setPick(e.target.value)} aria-label="Document from the vault">
              <option value="">Link a file from the vault…</option>
              {choices.map(d => <option key={d.id} value={d.id}>{d.name} ({d.folderName}{d.fy ? ` ${d.fy}` : ''})</option>)}
            </select>
            <button className="ghost" disabled={!pick || busy} onClick={attach}>Link</button>
          </>}
          <button className="ghost" disabled={busy} onClick={askClient}>Ask client for files</button>
        </div>
      )}
      {link && (
        <div className="preview row">
          <span className="mono break" style={{ flex: 1 }}>{link}</span>
          <button className="ghost" onClick={() => navigator.clipboard.writeText(link).catch(() => {})}>Copy</button>
          <span className="muted small">Send this to the client; what they upload is added here.</span>
        </div>
      )}
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
    </>
  )
}
