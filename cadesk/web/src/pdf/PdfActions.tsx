import { useState } from 'react'
import type { Block, PdfOptions } from './pdf.ts'
import { downloadPdf, makePdf, pdfFileName, savePdfToVault } from './export.ts'
import { errorText } from '../gst/vault.ts'

/** "Download PDF" and "Save to client documents" for anything the app can render as HTML or blocks. */
export function PdfActions(props: {
  title: string
  options: Omit<PdfOptions, 'title'>
  /** Called on click, so it can read the current page (e.g. a ref's innerHTML) or fetch HTML. */
  source: () => string | Block[] | Promise<string | Block[]>
  clientId?: string | null
  fy?: string
  canSave?: boolean
}) {
  const [busy, setBusy] = useState<'download' | 'save' | null>(null)
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null)

  async function build() {
    return makePdf(await props.source(), { ...props.options, title: props.title })
  }

  async function run(kind: 'download' | 'save') {
    setBusy(kind)
    setMessage(null)
    try {
      const bytes = await build()
      const fileName = pdfFileName(props.title)
      if (kind === 'download') downloadPdf(bytes, fileName)
      else if (props.clientId) {
        const saved = await savePdfToVault(props.clientId, bytes, { fileName, name: props.title, fy: props.fy })
        setMessage({ ok: true, text: `Saved to client documents as "${saved.name}".` })
      }
    } catch (e) {
      setMessage({ ok: false, text: kind === 'save' ? errorText(e) : 'The PDF could not be made. Try again.' })
    } finally {
      setBusy(null)
    }
  }

  return (
    <div className="pdf-actions">
      <button type="button" className="small secondary" disabled={busy !== null} onClick={() => run('download')}>
        {busy === 'download' ? 'Making PDF…' : 'Download PDF'}
      </button>
      {props.canSave && props.clientId && (
        <button type="button" className="small secondary" disabled={busy !== null} onClick={() => run('save')}>
          {busy === 'save' ? 'Saving…' : 'Save PDF to client documents'}
        </button>
      )}
      {message && <span role="status" className={message.ok ? 'pdf-actions-ok' : 'pdf-actions-error'}>{message.text}</span>}
    </div>
  )
}
