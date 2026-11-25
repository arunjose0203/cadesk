import { useEffect, useMemo, useState } from 'react'
import { gstApi, todayIso, type GstReconciliationBatchDto, type GstReconciliationItemDto } from './api'
import type { SalesLine } from './gst/gstr1'
import {
  HEADS, dueDate, emptyManualItc, isQuarterEnd, itcFrom2B, lateFee, lateInterest, noTax, outwardFromSales, setOff, sumHeads,
  type Frequency, type Head, type Heads, type ManualItc, type TurnoverBand,
} from './gst/gstr3b'
import { download, errorText, fyOfPeriod, saveGeneratedDocument } from './gst/vault'

const inr = new Intl.NumberFormat('en-IN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const f = (n: number) => inr.format(n)
const HEAD_LABEL: Record<Head, string> = { igst: 'IGST', cgst: 'CGST', sgst: 'SGST', cess: 'Cess' }
const num = { textAlign: 'right' as const }

type Props = { clientId: string; clientName: string; gstin: string; period: string; frequency: Frequency; lines: SalesLine[] }

const MANUAL_ROWS: { key: keyof ManualItc; label: string; table: string }[] = [
  { key: 'importGoods', label: 'Import of goods', table: '4A(1)' },
  { key: 'importServices', label: 'Import of services', table: '4A(2)' },
  { key: 'reverseCharge', label: 'Other reverse charge', table: '4A(3)' },
  { key: 'isd', label: 'From ISD', table: '4A(4)' },
  { key: 'reversalRules', label: 'Reversal, Rules 38/42/43', table: '4B(1)' },
  { key: 'reversalOther', label: 'Other reversals', table: '4B(2)' },
  { key: 'reclaimed', label: 'ITC reclaimed', table: '4D(1)' },
]

function HeadInputs({ value, onChange }: { value: Heads; onChange: (h: Heads) => void }) {
  return (
    <>
      {HEADS.map(k => (
        <td key={k} style={{ padding: '4px 6px' }}>
          <input
            type="number"
            inputMode="decimal"
            step="0.01"
            aria-label={HEAD_LABEL[k]}
            value={value[k] === 0 ? '' : value[k]}
            placeholder="0"
            onChange={e => onChange({ ...value, [k]: Number(e.target.value) || 0 })}
            style={{ width: '100%', minWidth: 90, textAlign: 'right', padding: '6px 8px' }}
          />
        </td>
      ))}
    </>
  )
}

function TaxCells({ h, txval }: { h: Heads; txval?: number }) {
  return (
    <>
      {txval !== undefined && <td style={num}>{f(txval)}</td>}
      {HEADS.map(k => <td key={k} style={num}>{f(h[k])}</td>)}
    </>
  )
}

/* Sprint 66: what GSTR-3B should show for the month, ITC from the 2B match, and the cash to pay. */
export default function Gstr3bPanel({ clientId, clientName, gstin, period, frequency, lines }: Props) {
  const [batches, setBatches] = useState<GstReconciliationBatchDto[] | null>(null)
  const [batchId, setBatchId] = useState('')
  const [items, setItems] = useState<GstReconciliationItemDto[]>([])
  const [manual, setManual] = useState<ManualItc>(emptyManualItc())
  const [rcmLiability, setRcmLiability] = useState<Heads & { txval: number }>({ txval: 0, ...noTax() })
  const [paidOn, setPaidOn] = useState(todayIso())
  const [band, setBand] = useState<TurnoverBand>(frequency === 'qrmp' ? 'upto1.5cr' : '1.5to5cr')
  const [saveMsg, setSaveMsg] = useState<{ ok: boolean; text: string } | null>(null)
  const [saving, setSaving] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)

  useEffect(() => {
    let live = true
    setBatches(null); setBatchId(''); setItems([])
    gstApi.listBatches(clientId, undefined, period)
      .then(list => {
        if (!live) return
        const sorted = [...list].sort((a, b) => b.createdAt.localeCompare(a.createdAt))
        setBatches(sorted)
        if (sorted[0]) setBatchId(sorted[0].id)
      })
      .catch(() => { if (live) { setBatches([]); setLoadError('2B matching could not be loaded. ITC below uses only the amounts you enter.') } })
    return () => { live = false }
  }, [clientId, period])

  useEffect(() => {
    if (!batchId) { setItems([]); return }
    let live = true
    gstApi.listItems(batchId)
      .then(r => { if (live) setItems(r) })
      .catch(() => { if (live) setLoadError('The 2B matching rows could not be loaded.') })
    return () => { live = false }
  }, [batchId])

  const outward = useMemo(() => outwardFromSales(gstin, lines), [gstin, lines])
  const itc = useMemo(() => itcFrom2B(items, manual), [items, manual])
  const outwardTax = sumHeads(outward.a, outward.b)
  const pay = setOff(outwardTax, itc.net, rcmLiability)
  const due = dueDate(period, frequency, gstin.slice(0, 2))
  const late = lateInterest(pay.totalCash, due, paidOn)
  const nilReturn = HEADS.every(k => pay.liability[k] === 0 && pay.rcmCash[k] === 0)
  const fee = lateFee(due, paidOn, nilReturn, band)

  const workingCsv = () => {
    const rows: (string | number)[][] = [[`GSTR-3B working for ${gstin}, period ${period}`], [], ['Table', 'Details', 'Taxable value', 'IGST', 'CGST', 'SGST', 'Cess']]
    const r = (t: string, d: string, h: Heads, tx: number | string = '') => rows.push([t, d, tx, h.igst, h.cgst, h.sgst, h.cess])
    r('3.1(a)', 'Outward taxable supplies', outward.a, outward.a.txval)
    r('3.1(b)', 'Zero-rated', outward.b, outward.b.txval)
    rows.push(['3.1(c)', 'Nil-rated and exempt', outward.c])
    r('3.1(d)', 'Inward supplies under reverse charge', rcmLiability, rcmLiability.txval)
    rows.push(['3.1(e)', 'Non-GST', outward.e])
    for (const u of outward.interUnregistered) rows.push(['3.2', `Unregistered, ${u.pos} ${u.state}`, u.txval, u.igst])
    r('4A(1)', 'Import of goods', itc.available.importGoods); r('4A(2)', 'Import of services', itc.available.importServices)
    r('4A(3)', 'Other reverse charge', itc.available.reverseCharge); r('4A(4)', 'ISD', itc.available.isd)
    r('4A(5)', 'All other ITC', itc.available.allOther)
    r('4B(1)', 'Rules 38/42/43 and s.17(5)', sumHeads(itc.reversed.rules, itc.reversed.blocked17_5)); r('4B(2)', 'Others', itc.reversed.other)
    r('4C', 'Net ITC', itc.net); r('4D(1)', 'Reclaimed', itc.other.reclaimed); r('4D(2)', 'Place-of-supply rule', itc.other.ineligiblePos)
    r('6.1', 'Paid by ITC', pay.paidByCredit); r('6.1', 'Cash (incl. reverse charge)', pay.totalCash)
    r('6.1', `Interest, ${late.days} days`, late.interest)
    rows.push(['6.1', `Late fee, ${fee.days} days`, '', 0, fee.cgst, fee.sgst, 0])
    r('', 'ITC carried forward', pay.carryForward)
    const esc = (v: string | number) => { const t = String(v); return /[",\n]/.test(t) ? `"${t.replace(/"/g, '""')}"` : t }
    return rows.map(x => x.map(esc).join(',')).join('\r\n') + '\r\n'
  }
  const workingName = `GSTR3B-working_${gstin}_${period}.csv`
  const saveWorking = async () => {
    setSaving(true); setSaveMsg(null)
    try {
      await saveGeneratedDocument(clientId, {
        fileName: workingName, content: workingCsv(), typeCode: frequency === 'qrmp' ? 'GSTR3B_Q' : 'GSTR3B_M',
        fy: fyOfPeriod(period), name: `GSTR-3B working ${period.slice(0, 2)}/${period.slice(2)}`,
      })
      setSaveMsg({ ok: true, text: `Saved to ${clientName}'s documents.` })
    } catch (e) {
      setSaveMsg({ ok: false, text: errorText(e) })
    } finally {
      setSaving(false)
    }
  }
  const held = itc.heldBack
  const heldTotal = (h: Heads) => h.igst + h.cgst + h.sgst + h.cess

  return (
    <section className="card">
      <div className="row spread" style={{ alignItems: 'flex-start' }}>
        <div style={{ maxWidth: 760 }}>
          <h2 style={{ marginBottom: 4 }}>GSTR-3B check</h2>
          <p className="small muted" style={{ margin: 0 }}>
            Table 3 on the portal is filled from GSTR-1 and cannot be edited. Compare these figures with the portal's
            draft GSTR-3B; if Table 3 differs, correct it through GSTR-1A before filing. ITC comes from the 2B match below.
          </p>
        </div>
        <div className="row">
          <button className="ghost" disabled={saving} onClick={() => void saveWorking()}>{saving ? 'Saving…' : 'Save to client documents'}</button>
          <button onClick={() => download(workingName, workingCsv(), 'text/csv;charset=utf-8')}>Download working (CSV)</button>
        </div>
      </div>
      {saveMsg && <p className={`small ${saveMsg.ok ? 'muted' : 'notice'}`} style={{ marginTop: 8 }}>{saveMsg.text}</p>}
      {frequency === 'qrmp' && !isQuarterEnd(period) && (
        <p className="notice small" style={{ marginTop: 12 }}>
          QRMP GSTR-3B is filed once a quarter. Choose the last month of the quarter (June, September, December or March) and upload the whole quarter's sales.
        </p>
      )}

      {loadError && <p className="notice small" style={{ marginTop: 12 }}>{loadError}</p>}

      <div className="row" style={{ marginTop: 16, gap: 16, alignItems: 'end' }}>
        <label className="stack" style={{ gap: 6, minWidth: 320 }}>
          <span className="small muted">GSTR-2B match for {period.slice(0, 2)}/{period.slice(2)}</span>
          <select value={batchId} onChange={e => setBatchId(e.target.value)} disabled={!batches || batches.length === 0}>
            {batches === null && <option>Loading…</option>}
            {batches?.length === 0 && <option value="">None for this month</option>}
            {batches?.map(b => (
              <option key={b.id} value={b.id}>
                {new Date(b.createdAt).toLocaleDateString('en-IN')} · {b.gstr2BCount} in 2B · {b.purchaseRegisterCount} in books
              </option>
            ))}
          </select>
        </label>
        <label className="stack" style={{ gap: 6 }}>
          <span className="small muted">Filed and paid on (due {due.split('-').reverse().join('-')})</span>
          <input type="date" value={paidOn} onChange={e => setPaidOn(e.target.value)} />
        </label>
        <label className="stack" style={{ gap: 6 }}>
          <span className="small muted">Turnover last year (late fee cap)</span>
          <select value={band} onChange={e => setBand(e.target.value as TurnoverBand)}>
            <option value="upto1.5cr">Up to ₹1.5 crore</option>
            <option value="1.5to5cr">₹1.5 to 5 crore</option>
            <option value="above5cr">Above ₹5 crore</option>
          </select>
        </label>
      </div>
      {batches?.length === 0 && !loadError && (
        <p className="small muted" style={{ marginTop: 8 }}>
          No 2B match has been run for this client and month. Run one on the GST 2B Match desk, or enter ITC by hand below.
        </p>
      )}

      <h3 style={{ marginTop: 24 }}>3.1 Outward supplies and reverse charge</h3>
      <table>
        <thead><tr><th>Nature of supplies</th><th style={num}>Taxable value</th>{HEADS.map(k => <th key={k} style={num}>{HEAD_LABEL[k]}</th>)}</tr></thead>
        <tbody>
          <tr><td>(a) Taxable, other than zero-rated, nil and exempt</td><TaxCells h={outward.a} txval={outward.a.txval} /></tr>
          <tr><td>(b) Zero-rated: exports and SEZ</td><TaxCells h={outward.b} txval={outward.b.txval} /></tr>
          <tr><td>(c) Nil-rated and exempt</td><td style={num}>{f(outward.c)}</td><td colSpan={4} /></tr>
          <tr>
            <td>(d) Inward supplies under reverse charge <span className="small muted">(enter)</span></td>
            <td style={{ padding: '4px 6px' }}>
              <input type="number" step="0.01" aria-label="Reverse charge taxable value" value={rcmLiability.txval || ''} placeholder="0"
                onChange={e => setRcmLiability({ ...rcmLiability, txval: Number(e.target.value) || 0 })}
                style={{ width: '100%', minWidth: 110, textAlign: 'right', padding: '6px 8px' }} />
            </td>
            <HeadInputs value={rcmLiability} onChange={h => setRcmLiability({ ...rcmLiability, ...h })} />
          </tr>
          <tr><td>(e) Non-GST</td><td style={num}>{f(outward.e)}</td><td colSpan={4} /></tr>
        </tbody>
      </table>
      {outward.reverseChargeOutward.documents > 0 && (
        <p className="small muted" style={{ marginTop: 6 }}>
          {outward.reverseChargeOutward.documents} suppl{outward.reverseChargeOutward.documents === 1 ? 'y' : 'ies'} (₹{f(outward.reverseChargeOutward.txval)}) are taxed in the customer's hands under reverse charge and carry no tax for this client.
        </p>
      )}

      {outward.amendedDocuments > 0 && (
        <p className="small muted" style={{ marginTop: 6 }}>
          {outward.amendedDocuments} amended document{outward.amendedDocuments === 1 ? ' is' : 's are'} not in 3.1: only the change from the original belongs there, and the portal adds it from GSTR-1.
        </p>
      )}

      {outward.interUnregistered.length > 0 && (
        <>
          <h3 style={{ marginTop: 24 }}>3.2 Inter-state supplies to unregistered persons</h3>
          <table>
            <thead><tr><th>Place of supply</th><th style={num}>Taxable value</th><th style={num}>IGST</th></tr></thead>
            <tbody>
              {outward.interUnregistered.map(u => (
                <tr key={u.pos}><td>{u.pos} – {u.state}</td><td style={num}>{f(u.txval)}</td><td style={num}>{f(u.igst)}</td></tr>
              ))}
            </tbody>
          </table>
        </>
      )}

      <h3 style={{ marginTop: 24 }}>4 Eligible ITC</h3>
      <table>
        <thead><tr><th>Details</th><th style={{ width: 70 }}>Table</th>{HEADS.map(k => <th key={k} style={num}>{HEAD_LABEL[k]}</th>)}</tr></thead>
        <tbody>
          {MANUAL_ROWS.slice(0, 4).map(r => (
            <tr key={r.key}><td>{r.label} <span className="small muted">(enter)</span></td><td className="muted">{r.table}</td>
              <HeadInputs value={manual[r.key]} onChange={h => setManual({ ...manual, [r.key]: h })} /></tr>
          ))}
          <tr><td>All other ITC, from the 2B match</td><td className="muted">4A(5)</td><TaxCells h={itc.available.allOther} /></tr>
          <tr style={{ fontWeight: 600 }}><td>Total ITC available</td><td className="muted">4A</td><TaxCells h={itc.available.total} /></tr>
          <tr><td>Blocked under s.17(5), from the 2B match</td><td className="muted">4B(1)</td><TaxCells h={itc.reversed.blocked17_5} /></tr>
          {MANUAL_ROWS.slice(4, 6).map(r => (
            <tr key={r.key}><td>{r.label} <span className="small muted">(enter)</span></td><td className="muted">{r.table}</td>
              <HeadInputs value={manual[r.key]} onChange={h => setManual({ ...manual, [r.key]: h })} /></tr>
          ))}
          <tr style={{ fontWeight: 600 }}><td>Net ITC available</td><td className="muted">4C</td><TaxCells h={itc.net} /></tr>
          <tr><td>{MANUAL_ROWS[6].label} <span className="small muted">(enter)</span></td><td className="muted">4D(1)</td>
            <HeadInputs value={manual.reclaimed} onChange={h => setManual({ ...manual, reclaimed: h })} /></tr>
          <tr><td>Not available: place-of-supply rule, from the 2B match</td><td className="muted">4D(2)</td><TaxCells h={itc.other.ineligiblePos} /></tr>
        </tbody>
      </table>

      {(held.notIn2B.count + held.notInBooks.count + held.supplierCancelled.count + held.loweredToBooks.count) > 0 && (
        <div className="notice small" style={{ marginTop: 12 }}>
          <b>Not claimed this month</b>
          <ul style={{ margin: '6px 0 0', paddingLeft: 18 }}>
            {held.notIn2B.count > 0 && <li>{held.notIn2B.count} purchase invoice{held.notIn2B.count === 1 ? ' is' : 's are'} not in GSTR-2B yet (tax ₹{f(heldTotal(held.notIn2B.tax))}). Follow up with the suppliers; claim when they appear.</li>}
            {held.notInBooks.count > 0 && <li>{held.notInBooks.count} invoice{held.notInBooks.count === 1 ? '' : 's'} in GSTR-2B {held.notInBooks.count === 1 ? 'is' : 'are'} not in the books (tax ₹{f(heldTotal(held.notInBooks.tax))}). Record them first if they are genuine.</li>}
            {held.supplierCancelled.count > 0 && <li>{held.supplierCancelled.count} invoice{held.supplierCancelled.count === 1 ? ' is' : 's are'} from suppliers whose registration is cancelled (tax ₹{f(heldTotal(held.supplierCancelled.tax))}).</li>}
            {held.loweredToBooks.count > 0 && <li>{held.loweredToBooks.count} invoice{held.loweredToBooks.count === 1 ? ' shows' : 's show'} more tax in 2B than in the books; ₹{f(heldTotal(held.loweredToBooks.tax))} was left out, claiming the books' figure.</li>}
          </ul>
        </div>
      )}

      <h3 style={{ marginTop: 24 }}>6.1 Payment of tax</h3>
      <table>
        <thead><tr><th>Head</th><th style={num}>Liability</th><th style={num}>Paid by ITC</th><th style={num}>Cash: outward</th><th style={num}>Cash: reverse charge</th><th style={num}>Cash total</th><th style={num}>Interest</th><th style={num}>ITC carried forward</th></tr></thead>
        <tbody>
          {HEADS.map(k => (
            <tr key={k}>
              <td>{HEAD_LABEL[k]}</td>
              <td style={num}>{f(pay.liability[k])}</td>
              <td style={num}>{f(pay.paidByCredit[k])}</td>
              <td style={num}>{f(pay.cash[k])}</td>
              <td style={num}>{f(pay.rcmCash[k])}</td>
              <td style={{ ...num, fontWeight: 600 }}>{f(pay.totalCash[k])}</td>
              <td style={num}>{f(late.interest[k])}</td>
              <td style={num}>{f(pay.carryForward[k])}</td>
            </tr>
          ))}
          <tr style={{ fontWeight: 600 }}>
            <td>Total</td>
            {[pay.liability, pay.paidByCredit, pay.cash, pay.rcmCash, pay.totalCash, late.interest, pay.carryForward].map((h, i) => (
              <td key={i} style={num}>{f(HEADS.reduce((s, k) => s + h[k], 0))}</td>
            ))}
          </tr>
        </tbody>
      </table>
      {pay.steps.length > 0 && (
        <p className="small muted" style={{ marginTop: 8 }}>
          Credit used: {pay.steps.map(s => `${HEAD_LABEL[s.from]} credit → ${HEAD_LABEL[s.to]} ₹${f(s.amount)}`).join('; ')}.
          IGST credit is used up first (Rule 88A); CGST and SGST credit are never used against each other.
        </p>
      )}
      {late.days > 0 && (
        <p className="small muted">
          Filed {late.days} day{late.days === 1 ? '' : 's'} after the due date: interest at 18% a year on the cash portion (s.50(1)),
          and a late fee of ₹{f(fee.cgst)} CGST + ₹{f(fee.sgst)} SGST (s.47{nilReturn ? ', nil return' : ''}, capped by Notification 19/2021-CT). The late fee is paid in cash.
        </p>
      )}
    </section>
  )
}
