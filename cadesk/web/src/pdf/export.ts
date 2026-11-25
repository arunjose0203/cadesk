/* Downloading and filing PDFs made in the browser (Sprint 70). */
import { renderPdf, type Block, type PdfOptions } from './pdf.ts'
import { htmlToBlocks } from './html.ts'
import { saveGeneratedDocument, type SavedDocument } from '../gst/vault.ts'

export function toBase64(bytes: Uint8Array): string {
  let s = ''
  for (let i = 0; i < bytes.length; i += 0x8000) s += String.fromCharCode(...bytes.subarray(i, i + 0x8000))
  return btoa(s)
}

/** "Audit dossier: Kochi Spices / FY 2025-26" → "Audit-dossier-Kochi-Spices-FY-2025-26.pdf" */
export function pdfFileName(title: string): string {
  const base = title.replace(/[^A-Za-z0-9.-]+/g, '-').replace(/-+/g, '-').replace(/^-|-$/g, '').slice(0, 100)
  return (base || 'document') + '.pdf'
}

export function makePdf(source: string | Block[], options: PdfOptions): Uint8Array {
  return renderPdf(typeof source === 'string' ? htmlToBlocks(source) : source, options)
}

export function downloadPdf(bytes: Uint8Array, fileName: string) {
  const url = URL.createObjectURL(new Blob([bytes as BlobPart], { type: 'application/pdf' }))
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  document.body.appendChild(a)
  a.click()
  a.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export function savePdfToVault(clientId: string, bytes: Uint8Array, file: { fileName: string; name?: string; fy?: string; typeCode?: string; folder?: string }): Promise<SavedDocument> {
  return saveGeneratedDocument(clientId, { ...file, contentBase64: toBase64(bytes) })
}
