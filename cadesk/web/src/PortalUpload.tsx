import { useCallback, useEffect, useState, type DragEvent } from 'react'
import { api, errorList, fmtDate, fmtSize, uploadFile, type PortalInfo } from './api'

/** The page a client opens from an upload link: no account, the link is the key. */
export default function PortalUpload({ token }: { token: string }) {
  const [info, setInfo] = useState<PortalInfo | null>(null)
  const [gone, setGone] = useState<string | null>(null)
  const [results, setResults] = useState<{ name: string; state: string; ok?: boolean }[]>([])
  const [busy, setBusy] = useState(false)
  const [over, setOver] = useState(false)
  const path = `/api/portal/uploads/${encodeURIComponent(token)}`

  const load = useCallback(async () => {
    try { setInfo(await api<PortalInfo>(path)) } catch (e) { setGone(errorList(e, 'This link could not be opened.').join(' ')) }
  }, [path])
  useEffect(() => { load() }, [load])

  const send = async (files: File[]) => {
    if (!files.length || busy) return
    setBusy(true)
    setResults(files.map(f => ({ name: f.name, state: 'waiting' })))
    for (let i = 0; i < files.length; i++) {
      setResults(r => r.map((x, j) => (j === i ? { ...x, state: 'sending…' } : x)))
      try {
        await uploadFile(path, files[i])
        setResults(r => r.map((x, j) => (j === i ? { ...x, state: 'received', ok: true } : x)))
      } catch (e) {
        setResults(r => r.map((x, j) => (j === i ? { ...x, state: errorList(e, 'Could not send.').join(' '), ok: false } : x)))
      }
    }
    setBusy(false)
    await load()
  }

  const drop = (e: DragEvent) => { e.preventDefault(); setOver(false); send(Array.from(e.dataTransfer.files)) }

  if (gone) return (
    <main className="page"><section className="card narrow"><h1>Upload documents</h1><p>{gone}</p></section></main>
  )
  if (!info) return <main className="page"><p className="muted">Loading…</p></main>

  const accept = info.allowedTypes.map(t => `.${t}${t === 'jpg' ? ',.jpeg' : ''}`).join(',')
  return (
    <main className="page">
      <section className="card narrow portal">
        <p className="muted small">{info.firmName}</p>
        <h1>Send documents</h1>
        <p>For <b>{info.clientName}</b> · {info.folderName}{info.fy ? ` · FY ${info.fy}` : ''}</p>
        {info.message && <p className="notice">{info.message}</p>}

        {info.remaining > 0 ? (
          <label className={`drop ${over ? 'over' : ''}`} onDragOver={e => { e.preventDefault(); setOver(true) }}
            onDragLeave={() => setOver(false)} onDrop={drop}>
            <b>{busy ? 'Sending…' : 'Choose files or drop them here'}</b>
            <span className="muted small">{info.allowedTypes.join(', ')} · up to {info.maxSizeMb} MB each · {info.remaining} more file{info.remaining === 1 ? '' : 's'}</span>
            <input type="file" multiple hidden accept={accept} disabled={busy}
              onChange={e => { send(Array.from(e.target.files ?? [])); e.target.value = '' }} />
          </label>
        ) : <p className="notice">This link has received all the files it allows. Ask {info.firmName} for a new link if you need to send more.</p>}

        {results.length > 0 && (
          <ul className="plain small">
            {results.map((r, i) => <li key={i} className={r.ok === false ? 'error' : r.ok ? 'ok' : 'muted'}>{r.name}: {r.state}</li>)}
          </ul>
        )}

        {info.received.length > 0 && (
          <>
            <h3 className="small muted" style={{ marginTop: 16 }}>Received so far</h3>
            <ul className="plain small">
              {info.received.map((f, i) => <li key={i}>{f.name} · {fmtSize(f.sizeBytes)} · {new Date(f.uploadedAt).toLocaleString()}
                {f.status !== 'received' && <span className="error"> · {f.status}</span>}</li>)}
            </ul>
          </>
        )}
        <p className="muted small" style={{ marginTop: 16 }}>
          This link works until {fmtDate(info.expiresAt.slice(0, 10))}. Files are checked for viruses and kept privately by {info.firmName}.
        </p>
        <p className="muted small">
          {info.firmName} decides how your documents are used; CaDesk stores them for the firm in India.
          {' '}<a href="/legal/privacy.html" target="_blank" rel="noopener noreferrer">Privacy notice</a>
        </p>
      </section>
    </main>
  )
}
