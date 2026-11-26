import React, { useState } from 'react'
import { tdsApi, type Client, type ImportForm16Result } from '../api'
import { download, errorText } from '../gst/vault'
import { form16TemplateCsv } from './tdsHelpers'

type Props = {
  clients: Client[]
  selectedClientId: string
  onSelectClient: (clientId: string) => void
  financialYear: string
  quarter: string
  onImported: () => Promise<void>
  onNotify: (msg: { error?: string; success?: string }) => void
}

export const TdsForm16ImportTab: React.FC<Props> = ({
  clients,
  selectedClientId,
  onSelectClient,
  financialYear,
  quarter,
  onImported,
  onNotify,
}) => {
  const activeClientId = selectedClientId || clients[0]?.id || ''
  const [defaultForm, setDefaultForm] = useState<'24Q' | '26Q' | '27Q'>('26Q')
  const [content, setContent] = useState('')
  const [result, setResult] = useState<ImportForm16Result | null>(null)
  const [running, setRunning] = useState(false)

  const handleFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (!file) return
    const text = await file.text()
    setContent(text)
    setResult(null)
  }

  const runImport = async (commit: boolean) => {
    if (!activeClientId) {
      onNotify({ error: 'Select a client before importing Form 16 / Form 16A records.' })
      return
    }
    if (!content.trim()) {
      onNotify({ error: 'Upload a file or paste NSDL .txt / CSV / TRACES certificate content first.' })
      return
    }
    try {
      setRunning(true)
      onNotify({})
      const res = await tdsApi.importForm16({
        clientId: activeClientId,
        content,
        formType: defaultForm,
        financialYear,
        quarter,
        commit,
      })
      setResult(res)
      if (commit && res.committed) {
        onNotify({
          success: `Imported ${res.importedChallansCount} challan(s) and ${res.importedDeductionsCount} deduction(s) into TDS register.`,
        })
        await onImported()
      } else if (res.errors.length > 0) {
        onNotify({ error: res.errors[0] })
      }
    } catch (err: unknown) {
      onNotify({ error: errorText(err) })
    } finally {
      setRunning(false)
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
      <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '20px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: '12px' }}>
          <div>
            <h3 style={{ margin: 0, fontSize: '16px', fontWeight: 700, color: 'var(--text)' }}>
              Form 16 / Form 16A & NSDL RPU `.txt` Import
            </h3>
            <p style={{ margin: '4px 0 0', fontSize: '13px', color: 'var(--muted)' }}>
              Supports (1) NSDL / Protean RPU `.txt` files (`24Q/26Q/27Q` & Forms `138/140/144`), (2) CaDesk CSV registers, or (3) pasted TRACES Form 16 / 16A certificate text.
            </p>
          </div>
          <button
            type="button"
            onClick={() => download('cadesk-form16a-template.csv', form16TemplateCsv(), 'text/csv')}
            style={{ padding: '7px 14px', borderRadius: '6px', border: '1px solid var(--border-subtle)', background: 'var(--card-subtle)', fontWeight: 600, fontSize: '12px', cursor: 'pointer' }}
          >
            Download CSV Template
          </button>
        </div>

        <div style={{ display: 'flex', gap: '12px', alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <div>
            <label style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Target Client</label>
            <select
              value={activeClientId}
              onChange={(e) => onSelectClient(e.target.value)}
              style={{ padding: '7px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
            >
              {clients.map((c) => (
                <option key={c.id} value={c.id}>{c.name}</option>
              ))}
            </select>
          </div>
          <div>
            <label style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Default Form (when omitted)</label>
            <select
              value={defaultForm}
              onChange={(e) => setDefaultForm(e.target.value as '24Q' | '26Q' | '27Q')}
              style={{ padding: '7px 12px', borderRadius: '6px', border: '1px solid var(--border-subtle)', fontSize: '13px' }}
            >
              <option value="24Q">24Q / 138 (Form 16 Salary)</option>
              <option value="26Q">26Q / 140 (Form 16A Domestic)</option>
              <option value="27Q">27Q / 144 (Form 16A NR)</option>
            </select>
          </div>
          <div>
            <label style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', display: 'block', marginBottom: '4px' }}>Upload `.txt` or `.csv`</label>
            <input type="file" accept=".txt,.csv,.text" onChange={handleFile} style={{ fontSize: '12px' }} />
          </div>
          <div style={{ marginLeft: 'auto', display: 'flex', gap: '8px' }}>
            <button
              type="button"
              disabled={running || !content.trim()}
              onClick={() => runImport(false)}
              style={{ padding: '8px 16px', borderRadius: '6px', border: '1px solid var(--accent)', background: 'var(--accent-subtle)', color: 'var(--accent)', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
            >
              {running ? 'Parsing...' : 'Preview Import'}
            </button>
            <button
              type="button"
              disabled={running || !result || result.errors.length > 0 || result.deductions.length === 0}
              onClick={() => runImport(true)}
              style={{ padding: '8px 16px', borderRadius: '6px', border: 'none', background: '#059669', color: '#fff', fontWeight: 600, fontSize: '13px', cursor: 'pointer' }}
            >
              Commit to TDS Register
            </button>
          </div>
        </div>

        <textarea
          rows={7}
          value={content}
          onChange={(e) => {
            setContent(e.target.value)
            setResult(null)
          }}
          placeholder="Paste NSDL RPU .txt content, Form 16/16A CSV rows, or copied TRACES certificate text here..."
          style={{
            width: '100%',
            padding: '10px',
            borderRadius: '6px',
            border: '1px solid var(--border-subtle)',
            fontFamily: 'monospace',
            fontSize: '12px',
            boxSizing: 'border-box',
          }}
        />
      </div>

      {result && (
        <div style={{ backgroundColor: 'var(--card)', borderRadius: '8px', border: '1px solid var(--border)', padding: '20px', display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <div style={{ display: 'flex', gap: '10px', alignItems: 'center', flexWrap: 'wrap' }}>
            <span style={{ padding: '3px 10px', borderRadius: '999px', backgroundColor: 'var(--accent-subtle)', color: 'var(--accent)', fontWeight: 700, fontSize: '12px' }}>
              Format: {result.detectedFormat}
            </span>
            {result.deductorTan && <span style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>TAN: <strong>{result.deductorTan}</strong></span>}
            {result.formType && <span style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>Form: <strong>{result.formType}</strong></span>}
            {result.financialYear && <span style={{ fontSize: '12px', color: 'var(--text-secondary)' }}>FY: <strong>{result.financialYear}</strong> {result.quarter}</span>}
            <span style={{ fontSize: '12px', color: 'var(--text)', marginLeft: 'auto', fontWeight: 600 }}>
              {result.challans.length} challan(s) • {result.deductions.length} deduction(s)
              {result.committed ? ' • COMMITTED' : ' • DRY RUN'}
            </span>
          </div>

          {result.errors.map((err, i) => (
            <div key={`e-${i}`} style={{ padding: '8px 12px', backgroundColor: 'var(--error-bg)', color: 'var(--error)', borderRadius: '6px', fontSize: '12px' }}>
              {err}
            </div>
          ))}
          {result.warnings.map((w, i) => (
            <div key={`w-${i}`} style={{ padding: '8px 12px', backgroundColor: 'var(--warning-bg)', color: 'var(--warning)', borderRadius: '6px', fontSize: '12px' }}>
              {w}
            </div>
          ))}

          {result.challans.length > 0 && (
            <div>
              <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', marginBottom: '6px' }}>Detected ITNS 281 Challans</div>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px', textAlign: 'left' }}>
                <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                  <tr>
                    <th style={{ padding: '8px' }}>BSR Code</th>
                    <th style={{ padding: '8px' }}>Serial</th>
                    <th style={{ padding: '8px' }}>Date</th>
                    <th style={{ padding: '8px' }}>Tax</th>
                    <th style={{ padding: '8px' }}>Total</th>
                    <th style={{ padding: '8px' }}>Match</th>
                  </tr>
                </thead>
                <tbody>
                  {result.challans.map((c, idx) => (
                    <tr key={`${c.bsrCode}-${c.challanSerialNumber}-${idx}`} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '8px', fontFamily: 'monospace' }}>{c.bsrCode}</td>
                      <td style={{ padding: '8px', fontFamily: 'monospace' }}>{c.challanSerialNumber}</td>
                      <td style={{ padding: '8px' }}>{c.challanDate}</td>
                      <td style={{ padding: '8px' }}>₹{c.taxDeposited.toLocaleString('en-IN')}</td>
                      <td style={{ padding: '8px', fontWeight: 600 }}>₹{c.totalAmount.toLocaleString('en-IN')}</td>
                      <td style={{ padding: '8px' }}>{c.alreadyExists ? 'Existing (will reuse)' : 'New challan'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}

          {result.deductions.length > 0 && (
            <div style={{ overflowX: 'auto' }}>
              <div style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text-secondary)', marginBottom: '6px' }}>Detected Deductions</div>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '12px', textAlign: 'left' }}>
                <thead style={{ backgroundColor: 'var(--card-subtle)', borderBottom: '1px solid var(--border)' }}>
                  <tr>
                    <th style={{ padding: '8px' }}>Form</th>
                    <th style={{ padding: '8px' }}>PAN</th>
                    <th style={{ padding: '8px' }}>Deductee Name</th>
                    <th style={{ padding: '8px' }}>Section</th>
                    <th style={{ padding: '8px' }}>Pay Date</th>
                    <th style={{ padding: '8px' }}>Amount Paid</th>
                    <th style={{ padding: '8px' }}>Rate</th>
                    <th style={{ padding: '8px' }}>TDS</th>
                    <th style={{ padding: '8px' }}>Challan Ref</th>
                  </tr>
                </thead>
                <tbody>
                  {result.deductions.map((d, idx) => (
                    <tr key={`${d.deducteePan}-${idx}`} style={{ borderBottom: '1px solid var(--border)' }}>
                      <td style={{ padding: '8px' }}>{d.formType} ({d.quarter})</td>
                      <td style={{ padding: '8px', fontFamily: 'monospace' }}>{d.deducteePan}</td>
                      <td style={{ padding: '8px', fontWeight: 600 }}>{d.deducteeName}</td>
                      <td style={{ padding: '8px' }}>{d.section}</td>
                      <td style={{ padding: '8px' }}>{d.paymentDate}</td>
                      <td style={{ padding: '8px' }}>₹{d.amountPaid.toLocaleString('en-IN')}</td>
                      <td style={{ padding: '8px' }}>{d.rate}%</td>
                      <td style={{ padding: '8px', fontWeight: 700, color: 'var(--error)' }}>₹{d.tdsAmount.toLocaleString('en-IN')}</td>
                      <td style={{ padding: '8px', fontFamily: 'monospace' }}>
                        {d.challanBsr && d.challanSerial ? `${d.challanBsr}/${d.challanSerial}` : 'Pending'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
