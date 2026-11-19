import { useCallback, useEffect, useState } from 'react'
import {
  api, download, errorList, fmtSize, SCAN_STATUS, uploadFile, uploadLinkUrl,
  type DocumentItem, type DocumentPage, type FolderChoice, type UploadLink, type UploadLinkCreated, type VaultInfo,
} from './api'

type Props = { clientId: string; can: (p: string) => boolean }

/** What the list shows: everything, one year, one folder in a year, or the trash. */
type View =
  | { kind: 'all' }
  | { kind: 'year'; fy: string | null }
  | { kind: 'folder'; fy: string | null; folder?: string | null; typeCode?: string | null }
  | { kind: 'trash' }

/** "f:bank" / "t:GSTR3B_M" ⇄ a folder choice. */
const choiceKey = (c: { folder?: string | null; typeCode?: string | null }) => (c.folder ? `f:${c.folder}` : `t:${c.typeCode}`)
const fromKey = (k: string) => (k.startsWith('f:') ? { folder: k.slice(2), typeCode: null } : { folder: null, typeCode: k.slice(2) })

function query(v: View, search: string) {
  const p = new URLSearchParams({ take: '200' })
  if (search.trim()) p.set('search', search.trim())
  if (v.kind === 'trash') p.set('trash', 'true')
  if (v.kind === 'year' || v.kind === 'folder') p.set('fy', v.fy ?? 'permanent')
  if (v.kind === 'folder' && v.folder) p.set('folder', v.folder)
  if (v.kind === 'folder' && v.typeCode) p.set('typeCode', v.typeCode)
  return p.toString()
}

/** A client's document vault: folders by year and filing, upload, download, trash, legal hold and client upload links. */
export default function DocumentsPanel({ clientId, can }: Props) {
  const [vault, setVault] = useState<VaultInfo | null>(null)
  const [view, setView] = useState<View>({ kind: 'all' })
  const [search, setSearch] = useState('')
  const [docs, setDocs] = useState<DocumentItem[]>([])
  const [total, setTotal] = useState(0)
  const [target, setTarget] = useState({ key: 'f:other', fy: '' })
  const [progress, setProgress] = useState<{ name: string; state: string; ok?: boolean }[]>([])
  const [editing, setEditing] = useState<{ id: string; name: string; key: string; fy: string } | null>(null)
  const [errors, setErrors] = useState<string[] | null>(null)
  const upload = can('documents.upload')
  const remove = can('documents.delete')

  const load = useCallback(async () => {
    const [v, p] = await Promise.all([
      api<VaultInfo>(`/api/clients/${clientId}/documents/vault`),
      api<DocumentPage>(`/api/clients/${clientId}/documents?${query(view, search)}`),
    ])
    setVault(v); setDocs(p.items); setTotal(p.total)
  }, [clientId, view, search])
  useEffect(() => { load().catch(() => {}) }, [load])

  // uploads go to the folder being looked at
  useEffect(() => {
    if (!vault) return
    if (view.kind === 'folder') setTarget({ key: choiceKey(view), fy: view.fy ?? vault.currentFy })
    else if (view.kind === 'year') setTarget(t => ({ key: view.fy === null ? 'f:permanent' : t.key === 'f:permanent' ? 'f:other' : t.key, fy: view.fy ?? vault.currentFy }))
    else setTarget(t => ({ ...t, fy: t.fy || vault.currentFy }))
  }, [view, vault])

  const run = async (fn: () => Promise<unknown>) => {
    setErrors(null)
    try { await fn(); await load() } catch (e) { setErrors(errorList(e)) }
  }

  const chosen: FolderChoice | undefined = vault?.folders.find(f => choiceKey(f) === target.key)
  const sendFiles = async (files: FileList | null) => {
    if (!files?.length || !vault) return
    const list = Array.from(files)
    setErrors(null)
    setProgress(list.map(f => ({ name: f.name, state: 'waiting' })))
    for (let i = 0; i < list.length; i++) {
      const f = list[i]
      setProgress(p => p.map((x, j) => (j === i ? { ...x, state: 'uploading…' } : x)))
      try {
        const { folder, typeCode } = fromKey(target.key)
        const d = await uploadFile<DocumentItem>(`/api/clients/${clientId}/documents`, f,
          { folder, typeCode, fy: chosen?.perYear === false ? null : target.fy })
        setProgress(p => p.map((x, j) => (j === i ? { ...x, state: d.scanStatus === 'pending' ? 'saved — waiting for virus scan' : 'saved', ok: true } : x)))
      } catch (e) {
        setProgress(p => p.map((x, j) => (j === i ? { ...x, state: errorList(e, 'Upload failed.').join(' '), ok: false } : x)))
      }
    }
    await load().catch(() => {})
  }

  const saveEdit = () => editing && run(async () => {
    const d = docs.find(x => x.id === editing.id)
    const { folder, typeCode } = fromKey(editing.key)
    const perYear = vault?.folders.find(f => choiceKey(f) === editing.key)?.perYear !== false
    await api(`/api/documents/${editing.id}`, {
      method: 'PUT', body: JSON.stringify({ name: editing.name, folder, typeCode, fy: perYear ? editing.fy : null, version: d?.version }),
    })
    setEditing(null)
  })

  if (!vault) return <section className="card"><h2>Documents</h2><p className="muted">Loading…</p></section>

  const isSel = (v: View) => JSON.stringify(v) === JSON.stringify(view)
  const viewTitle = view.kind === 'all' ? 'All documents' : view.kind === 'trash' ? 'Trash'
    : view.kind === 'year' ? (view.fy ? `FY ${view.fy}` : 'Permanent')
    : `${vault.folders.find(f => choiceKey(f) === choiceKey(view))?.name ?? ''}${view.fy ? ` · FY ${view.fy}` : ''}`

  return (
    <section className="card">
      <div className="row spread">
        <h2>Documents</h2>
        <span className="muted small">
          {vault.allowedTypes.join(', ')} · up to {vault.maxSizeMb} MB · {vault.scanEnabled ? 'virus scanned' : 'virus scan off'}
        </span>
      </div>
      <div className="vault">
        <ul className="tree">
          <li><button className={isSel({ kind: 'all' }) ? 'active' : ''} onClick={() => setView({ kind: 'all' })}>All documents</button></li>
          {vault.years.map(y => (
            <li key={y.fy ?? 'permanent'}>
              <button className={isSel({ kind: 'year', fy: y.fy ?? null }) ? 'active' : ''} onClick={() => setView({ kind: 'year', fy: y.fy ?? null })}>
                {y.label} <span className="muted">{y.count}</span>
              </button>
              <ul>
                {y.folders.map(f => {
                  const v: View = { kind: 'folder', fy: y.fy ?? null, folder: f.folder ?? null, typeCode: f.typeCode ?? null }
                  return (
                    <li key={choiceKey(f)}>
                      <button className={isSel(v) ? 'active' : ''} onClick={() => setView(v)}>{f.name} <span className="muted">{f.count}</span></button>
                    </li>
                  )
                })}
              </ul>
            </li>
          ))}
          {vault.years.length === 0 && <li className="muted small">No documents yet.</li>}
          <li><button className={isSel({ kind: 'trash' }) ? 'active' : ''} onClick={() => setView({ kind: 'trash' })}>
            Trash <span className="muted">{vault.trashCount}</span></button></li>
        </ul>

        <div>
          {upload && view.kind !== 'trash' && (
            <div className="notice">
              <div className="row">
                <select value={target.key} onChange={e => setTarget({ ...target, key: e.target.value })} aria-label="Folder">
                  <optgroup label="Folders">
                    {vault.folders.filter(f => f.folder).map(f => <option key={choiceKey(f)} value={choiceKey(f)}>{f.name}</option>)}
                  </optgroup>
                  <optgroup label="Filings">
                    {vault.folders.filter(f => f.typeCode).map(f => <option key={choiceKey(f)} value={choiceKey(f)}>{f.name}</option>)}
                  </optgroup>
                </select>
                {chosen?.perYear !== false && (
                  <select value={target.fy} onChange={e => setTarget({ ...target, fy: e.target.value })} aria-label="Year">
                    {[...new Set([target.fy, ...vault.yearChoices])].filter(Boolean).map(y => <option key={y} value={y}>FY {y}</option>)}
                  </select>
                )}
                <label className="button-like">
                  Upload files
                  <input type="file" multiple hidden accept={vault.allowedTypes.map(t => `.${t}${t === 'jpg' ? ',.jpeg' : ''}`).join(',')}
                    onChange={e => { sendFiles(e.target.files); e.target.value = '' }} />
                </label>
              </div>
              {progress.length > 0 && (
                <ul className="small plain">
                  {progress.map((p, i) => <li key={i} className={p.ok === false ? 'error' : p.ok ? 'ok' : 'muted'}>{p.name}: {p.state}</li>)}
                </ul>
              )}
            </div>
          )}

          <div className="row spread">
            <h3>{viewTitle} <span className="muted small">({total})</span></h3>
            <input placeholder="Search names" value={search} onChange={e => setSearch(e.target.value)} />
          </div>
          <table>
            <thead><tr><th>Name</th><th>Folder</th><th>Size</th><th>Added</th><th /></tr></thead>
            <tbody>
              {docs.map(d => editing?.id === d.id ? (
                <tr key={d.id}>
                  <td colSpan={5}>
                    <div className="row">
                      <input value={editing.name} onChange={e => setEditing({ ...editing, name: e.target.value })} aria-label="Name" />
                      <select value={editing.key} onChange={e => setEditing({ ...editing, key: e.target.value })} aria-label="Folder">
                        {vault.folders.map(f => <option key={choiceKey(f)} value={choiceKey(f)}>{f.name}</option>)}
                      </select>
                      {vault.folders.find(f => choiceKey(f) === editing.key)?.perYear !== false && (
                        <select value={editing.fy} onChange={e => setEditing({ ...editing, fy: e.target.value })} aria-label="Year">
                          {[...new Set([editing.fy, ...vault.yearChoices])].filter(Boolean).map(y => <option key={y} value={y}>FY {y}</option>)}
                        </select>
                      )}
                      <button onClick={saveEdit}>Save</button>
                      <button className="ghost" onClick={() => setEditing(null)}>Cancel</button>
                    </div>
                  </td>
                </tr>
              ) : (
                <tr key={d.id}>
                  <td>
                    {d.canDownload
                      ? <button className="link" onClick={() => run(() => download(`/api/documents/${d.id}/download`, d.fileName))}>{d.name}</button>
                      : <span className={d.purgedAt ? 'muted strike' : ''}>{d.name}</span>}
                    <div className="muted small">
                      {d.fileName}
                      {d.scanStatus !== 'clean' && <> · <span className={d.scanStatus === 'infected' ? 'error' : 'warn'}>{SCAN_STATUS[d.scanStatus]}</span></>}
                      {d.legalHold && <> · <span className="tag">legal hold</span></>}
                      {d.taskCount > 0 && <> · used by {d.taskCount} task{d.taskCount === 1 ? '' : 's'}</>}
                    </div>
                  </td>
                  <td className="small">{d.folderName}{d.fy && <div className="muted">FY {d.fy}</div>}</td>
                  <td className="small nowrap">{fmtSize(d.sizeBytes)}</td>
                  <td className="small">
                    {new Date(d.uploadedAt).toLocaleDateString()}
                    <div className="muted">{d.source === 'portal' ? <span className="tag create">from client</span> : d.uploadedByName}</div>
                  </td>
                  <td className="nowrap">
                    {view.kind === 'trash' ? (
                      remove && <button className="ghost" onClick={() => run(() => api(`/api/documents/${d.id}/restore`, { method: 'POST' }))}>Restore</button>
                    ) : !d.purgedAt && <>
                      {upload && d.scanStatus === 'pending' &&
                        <button className="ghost" onClick={() => run(() => api(`/api/documents/${d.id}/rescan`, { method: 'POST' }))}>Scan now</button>}
                      {upload && <button className="ghost" onClick={() => setEditing({ id: d.id, name: d.name, key: choiceKey(d), fy: d.fy ?? vault.currentFy })}>Edit</button>}
                      {remove && <button className="ghost" onClick={() => run(() => api(`/api/documents/${d.id}/hold`, { method: 'PUT', body: JSON.stringify({ hold: !d.legalHold }) }))}>
                        {d.legalHold ? 'Release hold' : 'Hold'}</button>}
                      {remove && !d.legalHold && <button className="ghost" onClick={() => run(() => api(`/api/documents/${d.id}`, { method: 'DELETE' }))}>Delete</button>}
                    </>}
                  </td>
                </tr>
              ))}
              {docs.length === 0 && <tr><td colSpan={5} className="muted">{view.kind === 'trash' ? 'The trash is empty.' : 'No documents here.'}</td></tr>}
            </tbody>
          </table>
          {view.kind === 'trash' && <p className="muted small">Files in the trash are removed for good after the number of days set in Settings → Documents.</p>}
        </div>
      </div>
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
      {upload && <UploadLinks clientId={clientId} vault={vault} />}
    </section>
  )
}

/** Links a client opens to send files without an account. */
function UploadLinks({ clientId, vault }: { clientId: string; vault: VaultInfo }) {
  const [links, setLinks] = useState<UploadLink[]>([])
  const [form, setForm] = useState({ message: '', key: 'f:other', fy: vault.currentFy, days: '', maxFiles: '20' })
  const [made, setMade] = useState<string | null>(null)
  const [copied, setCopied] = useState(false)
  const [errors, setErrors] = useState<string[] | null>(null)

  const load = useCallback(async () => setLinks(await api<UploadLink[]>(`/api/clients/${clientId}/upload-links`)), [clientId])
  useEffect(() => { load().catch(() => {}) }, [load])

  const create = async () => {
    setErrors(null); setMade(null); setCopied(false)
    try {
      const { folder, typeCode } = fromKey(form.key)
      const perYear = vault.folders.find(f => choiceKey(f) === form.key)?.perYear !== false
      const r = await api<UploadLinkCreated>(`/api/clients/${clientId}/upload-links`, {
        method: 'POST',
        body: JSON.stringify({
          message: form.message || null, folder, typeCode, fy: perYear ? form.fy : null,
          days: form.days ? Number(form.days) : null, maxFiles: Number(form.maxFiles) || null,
        }),
      })
      setMade(uploadLinkUrl(r.token)); setForm({ ...form, message: '' }); await load()
    } catch (e) { setErrors(errorList(e)) }
  }
  const revoke = async (id: string) => {
    setErrors(null)
    try { await api(`/api/upload-links/${id}/revoke`, { method: 'POST' }); await load() } catch (e) { setErrors(errorList(e)) }
  }
  const copy = async () => { if (made) { await navigator.clipboard.writeText(made).catch(() => {}); setCopied(true) } }

  return (
    <div style={{ marginTop: 16 }}>
      <h3>Ask the client for files</h3>
      {!vault.clientUploadEnabled ? <p className="muted small">Client uploads are switched off for this client.</p> : (
        <>
          <p className="muted small">Make a link and send it to the client (email or WhatsApp). They can upload without an account until it expires.</p>
          <div className="grid">
            <input placeholder="Message to the client (optional)" value={form.message} onChange={e => setForm({ ...form, message: e.target.value })} />
            <select value={form.key} onChange={e => setForm({ ...form, key: e.target.value })} aria-label="Folder">
              {vault.folders.map(f => <option key={choiceKey(f)} value={choiceKey(f)}>{f.name}</option>)}
            </select>
            {vault.folders.find(f => choiceKey(f) === form.key)?.perYear !== false && (
              <select value={form.fy} onChange={e => setForm({ ...form, fy: e.target.value })} aria-label="Year">
                {vault.yearChoices.map(y => <option key={y} value={y}>FY {y}</option>)}
              </select>
            )}
            <input type="number" min={1} max={90} placeholder="Days (default)" value={form.days} onChange={e => setForm({ ...form, days: e.target.value })} />
            <input type="number" min={1} max={100} placeholder="Max files" value={form.maxFiles} onChange={e => setForm({ ...form, maxFiles: e.target.value })} />
            <button onClick={create}>Make link</button>
          </div>
          {made && (
            <div className="preview row">
              <span className="mono break" style={{ flex: 1 }}>{made}</span>
              <button className="ghost" onClick={copy}>{copied ? 'Copied' : 'Copy'}</button>
              <span className="muted small">Shown only now — copy it before leaving this page.</span>
            </div>
          )}
        </>
      )}
      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
      {links.length > 0 && (
        <table>
          <thead><tr><th>For</th><th>Files</th><th>Until</th><th>Status</th><th /></tr></thead>
          <tbody>
            {links.map(l => (
              <tr key={l.id}>
                <td className="small">{l.folderName}{l.fy && ` · FY ${l.fy}`}{l.taskTitle && <div className="muted">{l.taskTitle}</div>}
                  {l.message && <div className="muted">“{l.message}”</div>}</td>
                <td className="small">{l.uploadedCount} / {l.maxFiles}</td>
                <td className="small">{new Date(l.expiresAt).toLocaleDateString()}</td>
                <td><span className={`tag ${l.status === 'active' ? 'create' : ''}`}>{l.status}</span></td>
                <td>{l.status === 'active' && <button className="ghost" onClick={() => revoke(l.id)}>Revoke</button>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}
