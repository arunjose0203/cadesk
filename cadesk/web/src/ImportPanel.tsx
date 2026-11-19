import { useState } from 'react'
import { api, download, errorList, saveBlob, type ImportPreview, type ImportResult } from './api'

const csvCell = (v: string) => (/[",\r\n]/.test(v) ? `"${v.replace(/"/g, '""')}"` : v)

/** Upload .xlsx / .csv → see every row's problems → import the good rows. Nothing is saved until you confirm. */
export default function ImportPanel({ onDone, onClose }: { onDone: () => void; onClose: () => void }) {
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<ImportPreview | null>(null)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [errors, setErrors] = useState<string[] | null>(null)
  const [busy, setBusy] = useState(false)
  const [onlyProblems, setOnlyProblems] = useState(true)

  const send = async <T,>(path: string) => {
    const body = new FormData()
    body.append('file', file!)
    return api<T>(path, { method: 'POST', body })
  }

  const check = async () => {
    if (!file) return
    setBusy(true); setErrors(null); setResult(null)
    try { setPreview(await send<ImportPreview>('/api/clients/import/preview')) }
    catch (e) { setPreview(null); setErrors(errorList(e, 'Could not read the file.')) }
    finally { setBusy(false) }
  }

  const commit = async () => {
    if (!preview) return
    setBusy(true); setErrors(null)
    try {
      const r = await send<ImportResult>(`/api/clients/import/commit?skipInvalid=${preview.errorRows > 0}`)
      setResult(r); setPreview(null); setFile(null); onDone()
    } catch (e) { setErrors(errorList(e, 'Import failed. Nothing was saved.')) }
    finally { setBusy(false) }
  }

  const errorReport = () => {
    if (!preview) return
    const lines = [['Row', 'Name', 'Status', 'Problems', 'Warnings'].join(',')]
    for (const r of preview.rows.filter(r => !r.ok || r.warnings.length))
      lines.push([String(r.row), r.name ?? '', r.ok ? 'OK' : 'Error', r.errors.join(' | '), r.warnings.join(' | ')].map(csvCell).join(','))
    saveBlob(new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv' }), 'client-import-errors.csv')
  }

  const rows = preview ? preview.rows.filter(r => !onlyProblems || !r.ok || r.warnings.length > 0) : []

  return (
    <section className="card">
      <div className="row spread">
        <h2>Import clients from Excel</h2>
        <button className="ghost" onClick={onClose}>Close</button>
      </div>
      <ol className="small steps">
        <li>Download the template:{' '}
          <button className="ghost" onClick={() => download('/api/clients/import/template', 'client-import-template.xlsx')}>Excel</button>
          <button className="ghost" onClick={() => download('/api/clients/import/template?format=csv', 'client-import-template.csv')}>CSV</button>
          <span className="muted"> — the Help sheet lists your entity types and required fields.</span>
        </li>
        <li>Fill one client per row (up to 2,000). Leave Code empty to number automatically.</li>
        <li>Upload and check. Nothing is saved until you press Import.</li>
      </ol>
      <div className="row">
        <input type="file" accept=".xlsx,.csv" onChange={e => { setFile(e.target.files?.[0] ?? null); setPreview(null); setResult(null) }} />
        <button onClick={check} disabled={!file || busy}>{busy && !preview ? 'Checking…' : 'Check file'}</button>
      </div>

      {errors && <ul className="error">{errors.map(m => <li key={m}>{m}</li>)}</ul>}
      {result && <p className="ok">Imported {result.created} client{result.created === 1 ? '' : 's'}.{result.skipped > 0 && ` Skipped ${result.skipped} row(s) with problems.`}</p>}

      {preview && (
        <>
          <div className="summary">
            <span><b>{preview.totalRows}</b> rows</span>
            <span className="ok"><b>{preview.validRows}</b> ready</span>
            <span className={preview.errorRows ? 'error' : 'muted'}><b>{preview.errorRows}</b> with problems</span>
            {preview.newGroups.length > 0 && <span className="muted">New groups: {preview.newGroups.join(', ')}</span>}
          </div>
          {preview.ignoredColumns.length > 0 && <p className="muted small">Columns not used: {preview.ignoredColumns.join(', ')}</p>}
          <div className="row">
            <button onClick={commit} disabled={busy || preview.validRows === 0}>
              Import {preview.validRows} client{preview.validRows === 1 ? '' : 's'}{preview.errorRows > 0 ? `, skip ${preview.errorRows}` : ''}
            </button>
            {(preview.errorRows > 0 || preview.rows.some(r => r.warnings.length)) &&
              <button className="ghost" onClick={errorReport}>Download error report</button>}
            <label className="row small"><input type="checkbox" checked={onlyProblems} onChange={e => setOnlyProblems(e.target.checked)} /> Only rows with problems</label>
          </div>
          <table>
            <thead><tr><th>Row</th><th>Name</th><th>Status</th><th>Problems</th></tr></thead>
            <tbody>
              {rows.map(r => (
                <tr key={r.row}>
                  <td>{r.row}</td>
                  <td>{r.name ?? <span className="muted">(no name)</span>}</td>
                  <td>{r.ok ? <span className="tag create">ready</span> : <span className="tag delete">error</span>}</td>
                  <td>
                    {r.errors.map(m => <div key={m} className="error small">{m}</div>)}
                    {r.warnings.map(m => <div key={m} className="warn small">{m}</div>)}
                  </td>
                </tr>
              ))}
              {rows.length === 0 && <tr><td colSpan={4} className="muted">Every row is ready.</td></tr>}
            </tbody>
          </table>
        </>
      )}
    </section>
  )
}
