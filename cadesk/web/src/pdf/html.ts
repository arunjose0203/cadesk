/*
 * HTML → PDF blocks (Sprint 70). Turns the server's printable HTML (audit dossier, auditor's
 * report, notes) into blocks for renderPdf. It reads structure only — headings, paragraphs,
 * lists and tables — and drops styling, scripts and images. Written without the DOM so the
 * same code runs in the browser and in node tests.
 */
import type { Align, Block } from './pdf.ts'

const ENTITIES: Record<string, string> = {
  amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ', rsquo: '’', lsquo: '‘', rdquo: '”', ldquo: '“',
  ndash: '–', mdash: '—', hellip: '…', bull: '•', middot: '·', copy: '©', reg: '®', trade: '™', rupee: '₹', times: '×',
}

export function decodeEntities(text: string): string {
  return text.replace(/&(#x[0-9a-f]+|#\d+|[a-z]+);/gi, (whole, name: string) => {
    if (name[0] === '#') {
      const code = name[1] === 'x' || name[1] === 'X' ? parseInt(name.slice(2), 16) : parseInt(name.slice(1), 10)
      return Number.isFinite(code) && code > 0 && code < 0x110000 ? String.fromCodePoint(code) : ''
    }
    return ENTITIES[name.toLowerCase()] ?? whole
  })
}

type Token = { type: 'open' | 'close'; tag: string; attrs: string } | { type: 'text'; text: string }

function tokenize(html: string): Token[] {
  const out: Token[] = []
  const src = html.replace(/<!--[\s\S]*?-->/g, '').replace(/<!doctype[^>]*>/gi, '')
  const re = /<(\/?)([a-zA-Z][a-zA-Z0-9]*)([^>]*)>/g
  let last = 0
  let m: RegExpExecArray | null
  while ((m = re.exec(src))) {
    if (m.index > last) out.push({ type: 'text', text: src.slice(last, m.index) })
    const tag = m[2].toLowerCase()
    last = re.lastIndex
    if (!m[1] && (tag === 'script' || tag === 'style' || tag === 'head' || tag === 'title' || tag === 'svg')) {
      const end = src.toLowerCase().indexOf(`</${tag}`, last)
      const close = end < 0 ? src.length : src.indexOf('>', end) + 1 || src.length
      re.lastIndex = last = close
      continue
    }
    out.push({ type: m[1] ? 'close' : 'open', tag, attrs: m[3] })
  }
  if (last < src.length) out.push({ type: 'text', text: src.slice(last) })
  return out
}

const BLOCK_TAGS = new Set(['p', 'div', 'section', 'article', 'header', 'footer', 'main', 'blockquote', 'pre', 'address', 'figure', 'figcaption', 'dd', 'dt', 'body', 'html', 'center', 'caption'])
const clean = (s: string) => s.replace(/\s+/g, ' ').trim()
const NUMERIC = /^[-(]?(rs\.?|₹)?\s?[-(]?[\d,]+(\.\d+)?\)?%?$/i

/** Converts printable HTML into PDF blocks. */
export function htmlToBlocks(html: string): Block[] {
  const blocks: Block[] = []
  let text = ''
  let bold = 0
  let paraBold = false
  let paraAlign: Align | undefined
  let heading: 1 | 2 | 3 | null = null
  const lists: { ordered: boolean; items: string[] }[] = []
  let table: { header?: string[]; rows: string[][] } | null = null
  let row: { cells: string[]; header: boolean } | null = null
  let cell: string | null = null

  const flush = () => {
    const t = clean(decodeEntities(text))
    text = ''
    if (!t) return
    if (cell !== null) { cell += (cell ? ' ' : '') + t; return }
    if (lists.length) { const l = lists[lists.length - 1]; if (l.items.length) l.items[l.items.length - 1] += (l.items[l.items.length - 1] ? ' ' : '') + t; else l.items.push(t); return }
    if (heading) blocks.push({ kind: 'heading', level: heading, text: t })
    else blocks.push({ kind: 'paragraph', text: t, ...(paraBold || bold > 0 ? { bold: true } : {}), ...(paraAlign ? { align: paraAlign } : {}) })
  }

  for (const tok of tokenize(html)) {
    if (tok.type === 'text') { text += tok.text; continue }
    const { tag } = tok
    if (tok.type === 'open') {
      if (/^h[1-6]$/.test(tag)) { flush(); heading = Math.min(3, Number(tag[1])) as 1 | 2 | 3 }
      else if (tag === 'b' || tag === 'strong') bold++
      else if (tag === 'br') { if (cell !== null || lists.length) text += ' '; else flush() }
      else if (tag === 'hr') { flush(); blocks.push({ kind: 'spacer', height: 8 }) }
      else if (tag === 'ul' || tag === 'ol') { flush(); lists.push({ ordered: tag === 'ol', items: [] }) }
      else if (tag === 'li') { flush(); lists[lists.length - 1]?.items.push('') }
      else if (tag === 'table') { flush(); table = { rows: [] } }
      else if (tag === 'tr') { flush(); row = { cells: [], header: false } }
      else if (tag === 'td' || tag === 'th') { flush(); cell = ''; if (tag === 'th' && row) row.header = true }
      else if (BLOCK_TAGS.has(tag)) {
        flush()
        const align = /text-align\s*:\s*(right|center)/i.exec(tok.attrs)?.[1]?.toLowerCase() as Align | undefined
        paraAlign = align
        paraBold = /font-weight\s*:\s*(bold|[6-9]00)/i.test(tok.attrs)
        if (/page-break-before\s*:\s*always|break-before\s*:\s*page/i.test(tok.attrs) && blocks.length) blocks.push({ kind: 'pageBreak' })
      }
    } else {
      if (/^h[1-6]$/.test(tag)) { flush(); heading = null }
      else if (tag === 'b' || tag === 'strong') bold = Math.max(0, bold - 1)
      else if (tag === 'li') flush()
      else if (tag === 'ul' || tag === 'ol') {
        flush()
        const l = lists.pop()
        const items = l?.items.map(clean).filter(Boolean) ?? []
        if (items.length) {
          if (lists.length) lists[lists.length - 1].items.push(...items.map(i => '– ' + i))
          else blocks.push({ kind: 'list', items, ordered: l!.ordered })
        }
      }
      else if (tag === 'td' || tag === 'th') { flush(); if (row && cell !== null) row.cells.push(clean(cell)); cell = null }
      else if (tag === 'tr') {
        flush()
        if (row && table && row.cells.some(Boolean)) {
          if (row.header && !table.header && table.rows.length === 0) table.header = row.cells
          else table.rows.push(row.cells)
        }
        row = null
      }
      else if (tag === 'table') {
        flush()
        if (table && (table.rows.length || table.header)) blocks.push(tableBlock(table))
        table = null
      }
      else if (BLOCK_TAGS.has(tag)) { flush(); paraAlign = undefined; paraBold = false }
    }
  }
  flush()
  return blocks
}

function tableBlock(t: { header?: string[]; rows: string[][] }): Block {
  const cols = Math.max(t.header?.length ?? 0, ...t.rows.map(r => r.length))
  const pad = (r: string[]) => [...r, ...Array(Math.max(0, cols - r.length)).fill('')]
  const rows = t.rows.map(pad)
  const align: Align[] = Array.from({ length: cols }, (_, c) => {
    const values = rows.map(r => r[c]).filter(Boolean)
    return values.length > 0 && values.every(v => NUMERIC.test(v)) ? 'right' : 'left'
  })
  return { kind: 'table', ...(t.header ? { header: pad(t.header) } : {}), rows, align }
}
