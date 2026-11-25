import { useEffect, useMemo, useState } from 'react'
import { api, type Client, type ClientPage } from './api'
import {
  buildGstr1, isValidGstin, parseSalesRegister, parseSalesRegisterRows, templateCsv, STATE_CODES,
  type CancelledDoc, type Gstr1Result, type Issue, type ParsedRegister, type SalesLine,
} from './gst/gstr1'
import {
  buildGstr9Working, emptyAnnualEntries, gstr9Json, gstr9WorkingCsv,
  type AnnualEntries, type Gstr9Working,
} from './gst/gstr9'
import { readXlsxRows } from './gst/xlsx'
import type { Frequency } from './gst/gstr3b'
import { download, errorText, fyOfPeriod, saveGeneratedDocument } from './gst/vault'
import Gstr3bPanel from './Gstr3bPanel'

const inr = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const num = { textAlign: 'right' as const }

function previousMonth(): string {
  const d = new Date()
  d.setDate(1)
  d.setMonth(d.getMonth() - 1)
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`
}

/** The current financial year and the four before it. The default is the last completed year (the one GSTR-9 is filed for). */
function recentFys(): string[] {
  const now = new Date()
  const current = now.getMonth() >= 3 ? now.getFullYear() : now.getFullYear() - 1
  return [0, 1, 2, 3, 4].map(i => `${current - i}-${String((current - i + 1) % 100).padStart(2, '0')}`)
}

type Mode = 'monthly' | 'annual'
type SaveState = { busy: boolean; message: string | null; ok: boolean }

function IssuesTable({ issues }: { issues: Issue[] }) {
  const [showWarnings, setShowWarnings] = useState(false)
  const errorCount = issues.filter(i => i.severity === 'error').length
  const warningCount = issues.length - errorCount
  const shown = issues.filter(i => i.severity === 'error' || showWarnings)
  if (issues.length === 0) return null
  return (
    <div style={{ marginTop: 20 }}>
      <div className="row spread">
        <h3 style={{ margin: 0 }}>{showWarnings ? 'Errors and warnings' : 'Errors'}</h3>
        {warningCount > 0 && (
          <button className="link" onClick={() => setShowWarnings(s => !s)}>
            {showWarnings ? 'Hide warnings' : `Show ${warningCount} warning${warningCount === 1 ? '' : 's'}`}
          </button>
        )}
      </div>
      {shown.length === 0 && <p className="small muted">No errors. Review the warnings before you file.</p>}
      {shown.length > 0 && (
        <table style={{ marginTop: 8 }}>
          <thead><tr><th style={{ width: 90 }}>Type</th><th style={{ width: 70 }}>Row</th><th style={{ width: 160 }}>Document</th><th>What to fix</th></tr></thead>
          <tbody>
            {shown.slice(0, 300).map((i, n) => (
              <tr key={n}>
                <td><span className={`tag ${i.severity === 'error' ? 'overdue' : 'pending'}`}>{i.severity === 'error' ? 'Error' : 'Warning'}</span></td>
                <td>{i.row ?? '–'}</td>
                <td className="mono">{i.doc ?? ''}</td>
                <td>{i.message}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {shown.length > 300 && <p className="small muted">Showing the first 300 of {shown.length}.</p>}
    </div>
  )
}

/* Sprint 66: sales register (CSV) in. Monthly: GSTR-1 JSON for "Prepare offline > Upload", and the
   GSTR-3B check. Annual: GSTR-9 Tables 4, 5 and 17. The file is read and processed in the browser;
   only files the user chooses to save are sent to the client's document vault. */
export default function Gstr1ExportPage() {
  const [clients, setClients] = useState<Client[]>([])
  const [clientId, setClientId] = useState('')
  const [mode, setMode] = useState<Mode>('monthly')
  const [month, setMonth] = useState(previousMonth())
  const [fy, setFy] = useState(recentFys()[1])
  const [frequency, setFrequency] = useState<Frequency>('monthly')
  const [aato, setAato] = useState(false)
  const [fileName, setFileName] = useState('')
  const [parseIssues, setParseIssues] = useState<Issue[]>([])
  const [result, setResult] = useState<Gstr1Result | null>(null)
  const [lines, setLines] = useState<SalesLine[]>([])
  const [entries, setEntries] = useState<AnnualEntries>(emptyAnnualEntries())
  const [loadError, setLoadError] = useState<string | null>(null)
  const [save, setSave] = useState<SaveState>({ busy: false, message: null, ok: false })

  useEffect(() => {
    api<ClientPage>('/api/clients?take=500')
      .then(r => setClients(r.items.filter(c => c.isActive)))
      .catch(() => setLoadError('Clients could not be loaded. Refresh the page to try again.'))
  }, [])

  const client = clients.find(c => c.id === clientId)
  const gstin = (client?.taxIds?.GSTIN ?? '').toUpperCase()
  const period = month ? `${month.slice(5, 7)}${month.slice(0, 4)}` : ''
  const gstinOk = gstin !== '' && isValidGstin(gstin)

  const reset = () => {
    setResult(null); setLines([]); setParseIssues([]); setFileName('')
    setSave({ busy: false, message: null, ok: false })
  }

  const onFile = async (file: File | undefined) => {
    reset()
    if (!file) return
    setFileName(file.name)
    let parsed: ParsedRegister
    try {
      parsed = /\.(xlsx|xlsm|xls)$/i.test(file.name)
        ? parseSalesRegisterRows(await readXlsxRows(await file.arrayBuffer()))
        : parseSalesRegister(await file.text())
    } catch (e) {
      setParseIssues([{ severity: 'error', message: e instanceof Error ? e.message : 'The file could not be read.' }])
      return
    }
    setParseIssues(parsed.issues)
    if (parsed.lines.length === 0) return
    setLines(parsed.lines)
    if (mode === 'monthly') setResult(buildGstr1({ gstin, period, lines: parsed.lines, cancelled: parsed.cancelled as CancelledDoc[], aatoAbove5Cr: aato }))
  }

  const annual: Gstr9Working | null = useMemo(
    () => (mode === 'annual' && lines.length ? buildGstr9Working(fy, lines, entries) : null),
    [mode, lines, fy, entries],
  )
  const issues = useMemo(() => [...parseIssues, ...(result?.issues ?? []), ...(annual?.issues ?? [])], [parseIssues, result, annual])
  const errorCount = issues.filter(i => i.severity === 'error').length
  const warningCount = issues.length - errorCount
  const canDownload = Boolean(result?.json) && parseIssues.every(i => i.severity !== 'error')
  const annualOk = Boolean(annual) && errorCount === 0

  const gstr1Name = `GSTR1_${gstin}_${period}.json`
  const gstr9Name = `GSTR9-working_${gstin}_${fy}.csv`
  const gstr9JsonName = `GSTR9_${gstin}_${fy}.json`

  const saveToVault = async (fileName: string, content: string, typeCode: string, fyOfFile: string, name: string) => {
    setSave({ busy: true, message: null, ok: false })
    try {
      await saveGeneratedDocument(clientId, { fileName, content, typeCode, fy: fyOfFile, name })
      setSave({ busy: false, ok: true, message: `Saved to ${client?.name}'s documents (${name}, FY ${fyOfFile}).` })
    } catch (e) {
      setSave({ busy: false, ok: false, message: errorText(e) })
    }
  }

  return (
    <>
      <section className="card">
        <div className="row spread" style={{ alignItems: 'flex-start' }}>
          <h2>GST returns from a sales register</h2>
          <div className="tabs" role="tablist" style={{ margin: 0 }}>
            <button role="tab" aria-selected={mode === 'monthly'} className={mode === 'monthly' ? 'active' : ''} onClick={() => { setMode('monthly'); reset() }}>GSTR-1 &amp; 3B</button>
            <button role="tab" aria-selected={mode === 'annual'} className={mode === 'annual' ? 'active' : ''} onClick={() => { setMode('annual'); reset() }}>GSTR-9 (annual)</button>
          </div>
        </div>
        <p className="muted" style={{ maxWidth: 780 }}>
          {mode === 'monthly'
            ? <>Upload the client's sales register for the period as a CSV file. CaDesk checks every document, sorts it into the GSTR-1 tables and gives you a JSON file for Returns › GSTR-1 › Prepare offline › Upload. Once GSTR-1 is clean, the GSTR-3B check below works out Table 3, ITC from the 2B match and the tax to pay.</>
            : <>Upload the client's sales register for the whole financial year. CaDesk works out GSTR-9 Tables 4, 5 and 17 so you can check the portal's pre-filled figures before filing.</>}
          {' '}The file is processed in your browser; only files you choose to save are stored in the client's documents.
        </p>

        {loadError && <p className="notice small">{loadError}</p>}

        <div className="grid" style={{ gridTemplateColumns: 'minmax(240px, 2fr) minmax(150px, 1fr) minmax(150px, 1fr) minmax(200px, 1.3fr)', alignItems: 'end', marginTop: 16 }}>
          <label className="stack" style={{ gap: 6 }}>
            <span className="small muted">Client</span>
            <select value={clientId} onChange={e => { setClientId(e.target.value); reset() }}>
              <option value="">Choose a client…</option>
              {clients.map(c => (
                <option key={c.id} value={c.id}>{c.name}{c.taxIds?.GSTIN ? ` · ${c.taxIds.GSTIN}` : ' · no GSTIN'}</option>
              ))}
            </select>
          </label>
          {mode === 'monthly' ? (
            <>
              <label className="stack" style={{ gap: 6 }}>
                <span className="small muted">{frequency === 'qrmp' ? 'Quarter ending' : 'Return period'}</span>
                <input type="month" value={month} onChange={e => { setMonth(e.target.value); reset() }} />
              </label>
              <label className="stack" style={{ gap: 6 }}>
                <span className="small muted">Filing</span>
                <select value={frequency} onChange={e => { setFrequency(e.target.value as Frequency); reset() }}>
                  <option value="monthly">Monthly</option>
                  <option value="qrmp">Quarterly (QRMP)</option>
                </select>
              </label>
            </>
          ) : (
            <label className="stack" style={{ gap: 6 }}>
              <span className="small muted">Financial year</span>
              <select value={fy} onChange={e => { setFy(e.target.value); reset() }}>
                {recentFys().map((y, i) => <option key={y} value={y}>{y}{i === 0 ? ' (current year)' : ''}</option>)}
              </select>
            </label>
          )}
          {mode === 'monthly' && (
            <label className="row" style={{ gap: 8, paddingBottom: 10 }}>
              <input type="checkbox" checked={aato} onChange={e => { setAato(e.target.checked); reset() }} />
              <span className="small">Turnover above ₹5 crore last year (6-digit HSN)</span>
            </label>
          )}
        </div>

        {client && !gstin && <p className="notice small" style={{ marginTop: 12 }}>{client.name} has no GSTIN on its client record. Add it under Clients first.</p>}
        {client && gstin && !gstinOk && <p className="notice small" style={{ marginTop: 12 }}>The GSTIN on {client.name}'s record ({gstin}) is not valid. Correct it under Clients first.</p>}
        {gstinOk && (
          <p className="small muted" style={{ marginTop: 10 }}>
            Filing for <b>{gstin}</b> ({STATE_CODES[gstin.slice(0, 2)]}),{' '}
            {mode === 'monthly' ? <>period <b>{period.slice(0, 2)}/{period.slice(2)}</b>{frequency === 'qrmp' ? ' (QRMP quarter)' : ''}.</> : <>financial year <b>{fy}</b>.</>}
          </p>
        )}

        <div className="row" style={{ marginTop: 16 }}>
          <label className="button-like" style={{ cursor: gstinOk ? 'pointer' : 'not-allowed', opacity: gstinOk ? 1 : 0.55 }}>
            Upload sales register (Excel or CSV)
            <input type="file" accept=".xlsx,.xlsm,.xls,.csv,text/csv" disabled={!gstinOk} style={{ display: 'none' }}
              onChange={e => { void onFile(e.target.files?.[0]); e.target.value = '' }} />
          </label>
          <button className="ghost" onClick={() => download('CaDesk-sales-register-template.csv', templateCsv(), 'text/csv;charset=utf-8')}>
            Download template (opens in Excel)
          </button>
          {fileName && <span className="small muted">{fileName}</span>}
        </div>
        <p className="small muted" style={{ marginTop: 10, marginBottom: 0 }}>
          Excel (.xlsx) or CSV, one row per document and tax rate; title rows above the headings are fine. Mark cancelled numbers with Document Type "Cancelled" so Table 13 is right.
          For amendments of earlier documents, fill Original Document Number and Date.
        </p>
      </section>

      {mode === 'monthly' && (result || parseIssues.length > 0) && (
        <section className="card">
          <div className="row spread" style={{ marginBottom: 12 }}>
            <div>
              <h2 style={{ marginBottom: 4 }}>GSTR-1 check results</h2>
              <p className="small muted" style={{ margin: 0 }}>
                {result ? `${result.documentCount} documents. ` : ''}
                {errorCount === 0 ? 'No errors.' : `${errorCount} error${errorCount === 1 ? '' : 's'} to fix before the file can be made.`}
                {warningCount > 0 && ` ${warningCount} warning${warningCount === 1 ? '' : 's'} to review.`}
              </p>
            </div>
            <div className="row">
              <button className="ghost" disabled={!canDownload || save.busy}
                onClick={() => result?.json && saveToVault(gstr1Name, JSON.stringify(result.json), frequency === 'qrmp' ? 'GSTR1_Q' : 'GSTR1_M', fyOfPeriod(period), `GSTR-1 JSON ${period.slice(0, 2)}/${period.slice(2)}`)}>
                {save.busy ? 'Saving…' : 'Save to client documents'}
              </button>
              <button disabled={!canDownload} title={canDownload ? undefined : 'Fix the errors in the sales register and upload it again'}
                onClick={() => result?.json && download(gstr1Name, JSON.stringify(result.json), 'application/json')}>
                Download GSTR-1 JSON
              </button>
            </div>
          </div>
          {save.message && <p className={`small ${save.ok ? 'muted' : 'notice'}`}>{save.message}</p>}

          {result && result.sections.length > 0 && (
            <table>
              <thead>
                <tr>
                  <th>Section</th><th>Table</th><th style={num}>Documents</th><th style={num}>Taxable value</th>
                  <th style={num}>IGST</th><th style={num}>CGST</th><th style={num}>SGST</th><th style={num}>Cess</th>
                </tr>
              </thead>
              <tbody>
                {result.sections.map(s => (
                  <tr key={s.key}>
                    <td>{s.label}</td><td className="muted">{s.table}</td>
                    <td style={num}>{s.documents || '–'}</td><td style={num}>{inr.format(s.taxable)}</td>
                    <td style={num}>{inr.format(s.igst)}</td><td style={num}>{inr.format(s.cgst)}</td>
                    <td style={num}>{inr.format(s.sgst)}</td><td style={num}>{inr.format(s.cess)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          {result && result.documentsIssued.length > 0 && (
            <>
              <h3 style={{ marginTop: 24 }}>13 Documents issued</h3>
              <table>
                <thead><tr><th>Nature of document</th><th>From</th><th>To</th><th style={num}>Total</th><th style={num}>Cancelled</th><th style={num}>Net issued</th></tr></thead>
                <tbody>
                  {result.documentsIssued.map((d, i) => (
                    <tr key={i}>
                      <td>{d.natureLabel}</td><td className="mono">{d.from}</td><td className="mono">{d.to}</td>
                      <td style={num}>{d.total}</td><td style={num}>{d.cancelled}</td><td style={num}>{d.net}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}

          <IssuesTable issues={issues} />
        </section>
      )}

      {mode === 'monthly' && result?.json && canDownload && (
        <Gstr3bPanel clientId={clientId} clientName={client?.name ?? ''} gstin={gstin} period={period} frequency={frequency} lines={lines} />
      )}

      {mode === 'annual' && (annual || parseIssues.length > 0) && (
        <section className="card">
          <div className="row spread" style={{ marginBottom: 12 }}>
            <div>
              <h2 style={{ marginBottom: 4 }}>GSTR-9 working, FY {fy}</h2>
              <p className="small muted" style={{ margin: 0 }}>
                The JSON holds Tables 4, 5 and 17. Import it into the GSTR-9 offline tool (Open › Import JSON), complete Tables 6 to 19 from the portal's figures, then generate the file to upload.
                {errorCount > 0 && ` ${errorCount} error${errorCount === 1 ? '' : 's'} to fix first.`}
              </p>
            </div>
            <div className="row">
              <button className="ghost" disabled={!annualOk || save.busy}
                onClick={() => annual && saveToVault(gstr9JsonName, JSON.stringify(gstr9Json(annual, gstin, fy)), 'GSTR9', fy, `GSTR-9 JSON FY ${fy}`)}>
                {save.busy ? 'Saving…' : 'Save to client documents'}
              </button>
              <button className="ghost" disabled={!annualOk} onClick={() => annual && download(gstr9Name, gstr9WorkingCsv(annual, gstin, fy), 'text/csv;charset=utf-8')}>
                Working (CSV)
              </button>
              <button disabled={!annualOk} onClick={() => annual && download(gstr9JsonName, JSON.stringify(gstr9Json(annual, gstin, fy)), 'application/json')}>
                Download GSTR-9 JSON
              </button>
            </div>
          </div>
          {save.message && <p className={`small ${save.ok ? 'muted' : 'notice'}`}>{save.message}</p>}

          {annual && (
            <div className="row" style={{ gap: 24, marginTop: 8, alignItems: 'flex-start', flexWrap: 'wrap' }}>
              {([['advances', '4F Advances: tax paid, invoice not issued'], ['inwardRcm', '4G Inward supplies on reverse charge']] as const).map(([key, label]) => (
                <fieldset key={key} style={{ border: '1px solid var(--border)', borderRadius: 8, padding: '8px 12px' }}>
                  <legend className="small">{label}</legend>
                  <div className="row" style={{ gap: 8 }}>
                    {(['txval', 'igst', 'cgst', 'sgst', 'cess'] as const).map(h => (
                      <label key={h} className="stack" style={{ gap: 2 }}>
                        <span className="small muted">{{ txval: 'Taxable', igst: 'IGST', cgst: 'CGST', sgst: 'SGST', cess: 'Cess' }[h]}</span>
                        <input type="number" step="0.01" value={entries[key][h] || ''} placeholder="0" style={{ width: 110, textAlign: 'right' }}
                          onChange={e => setEntries({ ...entries, [key]: { ...entries[key], [h]: Number(e.target.value) || 0 } })} />
                      </label>
                    ))}
                  </div>
                </fieldset>
              ))}
            </div>
          )}

          {annual && annual.table4.length > 0 && (
            <>
              {[['4', 'Supplies on which tax is payable', annual.table4], ['5', 'Supplies on which tax is not payable', annual.table5]].map(([no, title, rows]) => (
                <div key={no as string} style={{ marginTop: 12 }}>
                  <h3>{no as string} {title as string}</h3>
                  <table>
                    <thead><tr><th style={{ width: 60 }}>Row</th><th>Details</th><th style={num}>Taxable value</th><th style={num}>IGST</th><th style={num}>CGST</th><th style={num}>SGST</th><th style={num}>Cess</th></tr></thead>
                    <tbody>
                      {(rows as Gstr9Working['table4']).map(r => (
                        <tr key={r.code} style={['4H', '4M', '4N', '5G', '5L', '5M', '5N'].includes(r.code) ? { fontWeight: 600 } : undefined}>
                          <td className="muted">{r.code}</td><td>{r.label}</td><td style={num}>{inr.format(r.txval)}</td>
                          <td style={num}>{inr.format(r.igst)}</td><td style={num}>{inr.format(r.cgst)}</td>
                          <td style={num}>{inr.format(r.sgst)}</td><td style={num}>{inr.format(r.cess)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ))}
              <h3 style={{ marginTop: 24 }}>17 HSN summary of outward supplies</h3>
              <table>
                <thead><tr><th>HSN/SAC</th><th>Description</th><th>UQC</th><th style={num}>Quantity</th><th style={num}>Rate</th><th style={num}>Taxable value</th><th style={num}>IGST</th><th style={num}>CGST</th><th style={num}>SGST</th><th style={num}>Cess</th></tr></thead>
                <tbody>
                  {annual.table17.map(h => (
                    <tr key={`${h.hsn}|${h.rate}|${h.uqc}`}>
                      <td className="mono">{h.hsn}</td><td>{h.description}</td><td>{h.uqc}</td><td style={num}>{h.quantity}</td><td style={num}>{h.rate}%</td>
                      <td style={num}>{inr.format(h.txval)}</td><td style={num}>{inr.format(h.igst)}</td><td style={num}>{inr.format(h.cgst)}</td>
                      <td style={num}>{inr.format(h.sgst)}</td><td style={num}>{inr.format(h.cess)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </>
          )}
          <IssuesTable issues={issues} />
        </section>
      )}
    </>
  )
}
