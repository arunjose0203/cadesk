/*
 * A small PDF writer (Sprint 70): A4 pages, Helvetica, headings with bookmarks, wrapped paragraphs,
 * tables that break across pages with their header repeated, lists, and a footer with
 * "Page X of Y". No dependencies, so it runs in the browser and in tests (web/tests/pdf.test.ts).
 *
 * Text uses the PDF standard Helvetica fonts with WinAnsi encoding. Characters outside it are
 * replaced: the rupee sign becomes "Rs.", others become "?". That keeps files small and every
 * viewer shows them the same way.
 */
import { HELVETICA, HELVETICA_BOLD, WINANSI_EXTRA } from './metrics.ts'

export type Align = 'left' | 'right' | 'center'
export type Block =
  | { kind: 'heading'; level: 1 | 2 | 3; text: string }
  | { kind: 'paragraph'; text: string; bold?: boolean; align?: Align; size?: number }
  | { kind: 'table'; header?: string[]; rows: string[][]; align?: Align[]; widths?: number[] }
  | { kind: 'list'; items: string[]; ordered?: boolean }
  | { kind: 'pageBreak' }
  | { kind: 'spacer'; height: number }

export type PdfOptions = {
  title: string
  author?: string
  /** Small text at the top of every page. */
  header?: string
  /** Small text at the bottom left of every page (e.g. firm name, or a SHA-256 seal). */
  footer?: string
  /** Fixed creation time for reproducible output (tests). */
  createdAt?: Date
}

const PAGE_W = 595.28, PAGE_H = 841.89
const MARGIN_X = 56, MARGIN_TOP = 64, MARGIN_BOTTOM = 64
const CONTENT_W = PAGE_W - 2 * MARGIN_X
const BODY = 10, LEADING = 1.35
const HEADING_SIZE = { 1: 16, 2: 13, 3: 11 } as const
const CELL_PAD = 4

// ---------------------------------------------------------------------------- text

/** Unicode → WinAnsi bytes as a JS string of char codes 0-255. */
export function toWinAnsi(text: string): string {
  let out = ''
  for (const ch of text.normalize('NFC')) {
    const c = ch.codePointAt(0)!
    if (ch === '₹') out += 'Rs.'
    else if (c === 0x09) out += ' '
    else if (c >= 0x20 && c < 0x7f) out += ch
    else if (c >= 0xa0 && c <= 0xff) out += ch
    else if (WINANSI_EXTRA[ch] !== undefined) out += String.fromCharCode(WINANSI_EXTRA[ch])
    else if (c === 0x2212) out += '-'
    else if (c === 0x2009 || c === 0x202f || c === 0x200b) out += ' '
    else if (c >= 0x20) out += '?'
  }
  return out
}

/** Width in points of WinAnsi text. */
export function textWidth(winAnsi: string, size: number, bold = false): number {
  const table = bold ? HELVETICA_BOLD : HELVETICA
  let w = 0
  for (let i = 0; i < winAnsi.length; i++) {
    const c = winAnsi.charCodeAt(i)
    w += c >= 32 && c <= 255 ? table[c - 32] || 556 : 0
  }
  return (w * size) / 1000
}

/** Splits text into lines no wider than maxWidth; words longer than a line are broken. */
export function wrap(text: string, maxWidth: number, size: number, bold = false): string[] {
  const out: string[] = []
  for (const para of toWinAnsi(text).split(/\r?\n/)) {
    const words = para.split(/ +/).filter(w => w.length > 0)
    let line = ''
    for (let word of words) {
      const candidate = line ? `${line} ${word}` : word
      if (textWidth(candidate, size, bold) <= maxWidth) { line = candidate; continue }
      if (line) { out.push(line); line = '' }
      while (textWidth(word, size, bold) > maxWidth) {
        let cut = word.length - 1
        while (cut > 1 && textWidth(word.slice(0, cut), size, bold) > maxWidth) cut--
        out.push(word.slice(0, cut))
        word = word.slice(cut)
      }
      line = word
    }
    out.push(line)
  }
  return out
}

const esc = (s: string) => s.replace(/\\/g, '\\\\').replace(/\(/g, '\\(').replace(/\)/g, '\\)')
const n = (v: number) => (Math.round(v * 100) / 100).toString()

// ---------------------------------------------------------------------------- layout

type Page = { ops: string[] }
type Outline = { title: string; level: 1 | 2 | 3; page: number; y: number }

class Layout {
  pages: Page[] = []
  outlines: Outline[] = []
  y = 0
  constructor() { this.newPage() }

  get page() { return this.pages[this.pages.length - 1] }
  newPage() { this.pages.push({ ops: [] }); this.y = PAGE_H - MARGIN_TOP }
  ensure(height: number) { if (this.y - height < MARGIN_BOTTOM) this.newPage() }

  text(x: number, y: number, s: string, size: number, bold = false, gray = 0) {
    if (!s) return
    this.page.ops.push(`BT ${gray ? `${n(gray)} g ` : ''}/${bold ? 'F2' : 'F1'} ${n(size)} Tf ${n(x)} ${n(y)} Td (${esc(s)}) Tj ET${gray ? ' 0 g' : ''}`)
  }
  line(x1: number, y1: number, x2: number, y2: number, width = 0.5, gray = 0.6) {
    this.page.ops.push(`${n(gray)} G ${n(width)} w ${n(x1)} ${n(y1)} m ${n(x2)} ${n(y2)} l S 0 G`)
  }
  rect(x: number, y: number, w: number, h: number, gray: number) {
    this.page.ops.push(`${n(gray)} g ${n(x)} ${n(y)} ${n(w)} ${n(h)} re f 0 g`)
  }

  lines(texts: string[], size: number, bold: boolean, align: Align = 'left', x = MARGIN_X, width = CONTENT_W) {
    const lh = size * LEADING
    for (const t of texts) {
      this.ensure(lh)
      this.y -= lh
      const w = textWidth(t, size, bold)
      const tx = align === 'right' ? x + width - w : align === 'center' ? x + (width - w) / 2 : x
      this.text(tx, this.y + (lh - size) / 2 + 1, t, size, bold)
    }
  }

  heading(level: 1 | 2 | 3, text: string) {
    const size = HEADING_SIZE[level]
    const lines = wrap(text, CONTENT_W, size, true)
    this.ensure(size * LEADING * lines.length + 10 + BODY * LEADING * 2) // keep with what follows
    this.y -= level === 1 ? 8 : 6
    this.outlines.push({ title: text, level, page: this.pages.length - 1, y: this.y + size })
    this.lines(lines, size, true)
    if (level === 1) { this.y -= 3; this.line(MARGIN_X, this.y, MARGIN_X + CONTENT_W, this.y, 0.75, 0.3) }
    this.y -= 4
  }

  paragraph(text: string, bold = false, align: Align = 'left', size = BODY) {
    this.lines(wrap(text, CONTENT_W, size, bold), size, bold, align)
    this.y -= size * 0.5
  }

  list(items: string[], ordered: boolean) {
    const indent = 16
    items.forEach((item, i) => {
      const marker = ordered ? `${i + 1}.` : toWinAnsi('•')
      const lines = wrap(item, CONTENT_W - indent, BODY)
      const lh = BODY * LEADING
      lines.forEach((t, j) => {
        this.ensure(lh)
        this.y -= lh
        if (j === 0) this.text(MARGIN_X + 2, this.y + 3, marker, BODY)
        this.text(MARGIN_X + indent, this.y + 3, t, BODY)
      })
    })
    this.y -= BODY * 0.5
  }

  table(header: string[] | undefined, rows: string[][], alignIn?: Align[], widthsIn?: number[]) {
    const cols = Math.max(header?.length ?? 0, ...rows.map(r => r.length), 1)
    const cell = (r: string[], i: number) => (r[i] ?? '').trim()
    const numeric = (s: string) => /^[-(]?\s*(Rs\.?|₹)?\s*[\d,]+(\.\d+)?\)?%?$/.test(s.trim())
    const align: Align[] = Array.from({ length: cols }, (_, i) =>
      alignIn?.[i] ?? (rows.length > 0 && rows.every(r => !cell(r, i) || numeric(cell(r, i))) && rows.some(r => cell(r, i)) ? 'right' : 'left'))

    // column widths: given fractions, or by content (longest word / natural width), within the page
    let widths: number[]
    if (widthsIn && widthsIn.length === cols) {
      const total = widthsIn.reduce((a, b) => a + b, 0)
      widths = widthsIn.map(w => (w / total) * CONTENT_W)
    } else {
      const sample = [header ?? [], ...rows.slice(0, 200)]
      const natural = Array.from({ length: cols }, (_, i) => Math.max(24, ...sample.map((r, k) => textWidth(toWinAnsi(cell(r, i)), BODY - 1, k === 0 && !!header) + 2 * CELL_PAD)))
      const minimum = Array.from({ length: cols }, (_, i) => Math.max(24, ...sample.map(r => Math.max(0, ...toWinAnsi(cell(r, i)).split(' ').map(w => textWidth(w, BODY - 1))) + 2 * CELL_PAD)))
      const sumNat = natural.reduce((a, b) => a + b, 0)
      if (sumNat <= CONTENT_W) widths = natural.map(w => (w / sumNat) * CONTENT_W)
      else {
        const sumMin = minimum.reduce((a, b) => a + b, 0)
        const spare = Math.max(0, CONTENT_W - sumMin)
        const extra = natural.map((w, i) => Math.max(0, w - minimum[i]))
        const sumExtra = extra.reduce((a, b) => a + b, 0) || 1
        widths = minimum.map((m, i) => m + (spare * extra[i]) / sumExtra)
        const s = widths.reduce((a, b) => a + b, 0)
        widths = widths.map(w => (w / s) * CONTENT_W)
      }
    }

    const size = BODY - 1, lh = size * LEADING
    const drawRow = (r: string[], bold: boolean, fill: boolean) => {
      const wrapped = Array.from({ length: cols }, (_, i) => wrap(cell(r, i), widths[i] - 2 * CELL_PAD, size, bold))
      const height = Math.max(1, ...wrapped.map(w => w.length)) * lh + 2 * CELL_PAD
      return { wrapped, height, bold, fill }
    }
    const paint = (row: ReturnType<typeof drawRow>) => {
      const top = this.y
      if (row.fill) this.rect(MARGIN_X, top - row.height, CONTENT_W, row.height, 0.93)
      let x = MARGIN_X
      row.wrapped.forEach((lines, i) => {
        lines.forEach((t, j) => {
          const w = textWidth(t, size, row.bold)
          const tx = align[i] === 'right' ? x + widths[i] - CELL_PAD - w : align[i] === 'center' ? x + (widths[i] - w) / 2 : x + CELL_PAD
          this.text(tx, top - CELL_PAD - (j + 1) * lh + (lh - size) / 2 + 1, t, size, row.bold)
        })
        x += widths[i]
      })
      this.line(MARGIN_X, top - row.height, MARGIN_X + CONTENT_W, top - row.height, 0.4, 0.75)
      this.y = top - row.height
    }

    const head = header ? drawRow(header, true, true) : null
    const start = () => { if (head) { this.line(MARGIN_X, this.y, MARGIN_X + CONTENT_W, this.y, 0.4, 0.75); paint(head) } }
    this.ensure((head?.height ?? 0) + lh + 2 * CELL_PAD)
    this.y -= 2
    start()
    for (const r of rows) {
      const row = drawRow(r, false, false)
      if (this.y - row.height < MARGIN_BOTTOM) { this.newPage(); start() }
      paint(row)
    }
    this.y -= BODY * 0.8
  }
}

// ---------------------------------------------------------------------------- file

export function renderPdf(blocks: Block[], options: PdfOptions): Uint8Array {
  const L = new Layout()
  for (const b of blocks) {
    switch (b.kind) {
      case 'heading': L.heading(b.level, b.text); break
      case 'paragraph': L.paragraph(b.text, b.bold, b.align, b.size); break
      case 'table': if (b.rows.length || b.header) L.table(b.header, b.rows, b.align, b.widths); break
      case 'list': L.list(b.items, Boolean(b.ordered)); break
      case 'pageBreak': if (L.y < PAGE_H - MARGIN_TOP) L.newPage(); break
      case 'spacer': L.y -= b.height; break
    }
  }

  // header and footer, now that the page count is known
  const total = L.pages.length
  L.pages.forEach((p, i) => {
    const small = 8
    if (options.header) {
      const h = wrap(options.header, CONTENT_W, small)[0]
      p.ops.push(`BT 0.45 g /F1 ${small} Tf ${n(MARGIN_X)} ${n(PAGE_H - 36)} Td (${esc(h)}) Tj ET 0 g`)
    }
    const pageText = `Page ${i + 1} of ${total}`
    const pw = textWidth(pageText, small)
    p.ops.push(`0.75 G 0.4 w ${n(MARGIN_X)} ${n(MARGIN_BOTTOM - 22)} m ${n(PAGE_W - MARGIN_X)} ${n(MARGIN_BOTTOM - 22)} l S 0 G`)
    p.ops.push(`BT 0.45 g /F1 ${small} Tf ${n(PAGE_W - MARGIN_X - pw)} ${n(MARGIN_BOTTOM - 34)} Td (${pageText}) Tj ET 0 g`)
    if (options.footer) {
      const f = wrap(options.footer, CONTENT_W - pw - 16, small)[0]
      p.ops.push(`BT 0.45 g /F1 ${small} Tf ${n(MARGIN_X)} ${n(MARGIN_BOTTOM - 34)} Td (${esc(f)}) Tj ET 0 g`)
    }
  })

  // objects: 1 catalog, 2 pages, 3 F1, 4 F2, 5 info, then per page (page, contents), then outlines
  const objects: string[] = []
  const add = (body: string) => { objects.push(body); return objects.length }
  const catalog = add('')
  const pagesObj = add('')
  const f1 = add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>')
  const f2 = add('<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>')
  const date = options.createdAt ?? new Date()
  const pdfDate = `D:${date.getUTCFullYear()}${String(date.getUTCMonth() + 1).padStart(2, '0')}${String(date.getUTCDate()).padStart(2, '0')}${String(date.getUTCHours()).padStart(2, '0')}${String(date.getUTCMinutes()).padStart(2, '0')}${String(date.getUTCSeconds()).padStart(2, '0')}Z`
  const info = add(`<< /Title (${esc(toWinAnsi(options.title))}) ${options.author ? `/Author (${esc(toWinAnsi(options.author))}) ` : ''}/Producer (CaDesk) /CreationDate (${pdfDate}) >>`)
  const pageIds: number[] = []
  for (const p of L.pages) {
    const content = p.ops.join('\n')
    const contentId = add(`<< /Length ${content.length} >>\nstream\n${content}\nendstream`)
    pageIds.push(add(`<< /Type /Page /Parent ${pagesObj} 0 R /MediaBox [0 0 ${n(PAGE_W)} ${n(PAGE_H)}] /Resources << /Font << /F1 ${f1} 0 R /F2 ${f2} 0 R >> >> /Contents ${contentId} 0 R >>`))
  }
  objects[pagesObj - 1] = `<< /Type /Pages /Kids [${pageIds.map(id => `${id} 0 R`).join(' ')}] /Count ${pageIds.length} >>`

  // bookmarks: level-1 headings at the top, level 2 and 3 under the nearest heading above them
  let outlinesRoot = 0
  if (L.outlines.length) {
    type Node = { o: Outline; id: number; children: Node[]; parent: number }
    outlinesRoot = add('')
    const roots: Node[] = []
    const stack: Node[] = []
    for (const o of L.outlines) {
      const node: Node = { o, id: add(''), children: [], parent: outlinesRoot }
      while (stack.length && stack[stack.length - 1].o.level >= o.level) stack.pop()
      if (stack.length) { node.parent = stack[stack.length - 1].id; stack[stack.length - 1].children.push(node) } else roots.push(node)
      stack.push(node)
    }
    const countAll = (list: Node[]): number => list.reduce((s, x) => s + 1 + countAll(x.children), 0)
    const write = (list: Node[]) => list.forEach((node, i) => {
      const parts = [`/Title (${esc(toWinAnsi(node.o.title))})`, `/Parent ${node.parent} 0 R`,
        `/Dest [${pageIds[node.o.page]} 0 R /XYZ ${n(MARGIN_X)} ${n(node.o.y)} null]`]
      if (i > 0) parts.push(`/Prev ${list[i - 1].id} 0 R`)
      if (i < list.length - 1) parts.push(`/Next ${list[i + 1].id} 0 R`)
      if (node.children.length) parts.push(`/First ${node.children[0].id} 0 R`, `/Last ${node.children[node.children.length - 1].id} 0 R`, `/Count ${countAll(node.children)}`)
      objects[node.id - 1] = `<< ${parts.join(' ')} >>`
      write(node.children)
    })
    write(roots)
    objects[outlinesRoot - 1] = `<< /Type /Outlines /First ${roots[0].id} 0 R /Last ${roots[roots.length - 1].id} 0 R /Count ${countAll(roots)} >>`
  }
  objects[catalog - 1] = `<< /Type /Catalog /Pages ${pagesObj} 0 R${outlinesRoot ? ` /Outlines ${outlinesRoot} 0 R /PageMode /UseOutlines` : ''} >>`

  // serialise: every byte is 0-255, so string offsets are byte offsets
  let out = '%PDF-1.4\n%\xe2\xe3\xcf\xd3\n'
  const offsets: number[] = []
  objects.forEach((body, i) => { offsets.push(out.length); out += `${i + 1} 0 obj\n${body}\nendobj\n` })
  const xref = out.length
  out += `xref\n0 ${objects.length + 1}\n0000000000 65535 f \n${offsets.map(o => `${String(o).padStart(10, '0')} 00000 n \n`).join('')}`
  out += `trailer\n<< /Size ${objects.length + 1} /Root ${catalog} 0 R /Info ${info} 0 R >>\nstartxref\n${xref}\n%%EOF\n`
  const bytes = new Uint8Array(out.length)
  for (let i = 0; i < out.length; i++) bytes[i] = out.charCodeAt(i) & 0xff
  return bytes
}
