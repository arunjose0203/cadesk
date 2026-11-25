/*
 * Reads the first worksheet of an .xlsx file into rows of text, in the browser, with no library.
 * An .xlsx file is a zip of XML parts; DecompressionStream('deflate-raw') unzips them
 * (Chrome/Edge 103+, Firefox 113+, Safari 16.4+, Node 18+).
 *
 * Handles: shared and inline text, numbers, booleans, formula results, Excel dates
 * (1900 and 1904 systems; returned as yyyy-mm-dd) and sparse cells. Not handled: the old
 * binary .xls format (save as .xlsx), password-protected files.
 */

type ZipEntry = { name: string; method: number; compressedSize: number; offset: number }

function readZipDirectory(buf: Uint8Array): Map<string, ZipEntry> {
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength)
  let eocd = -1
  for (let i = buf.length - 22; i >= Math.max(0, buf.length - 65557); i--) {
    if (dv.getUint32(i, true) === 0x06054b50) { eocd = i; break }
  }
  if (eocd < 0) throw new Error('This file is not a valid Excel workbook (.xlsx).')
  const count = dv.getUint16(eocd + 10, true)
  let p = dv.getUint32(eocd + 16, true)
  const entries = new Map<string, ZipEntry>()
  const dec = new TextDecoder()
  for (let n = 0; n < count; n++) {
    if (dv.getUint32(p, true) !== 0x02014b50) throw new Error('This Excel file is damaged.')
    const method = dv.getUint16(p + 10, true)
    const compressedSize = dv.getUint32(p + 20, true)
    const nameLen = dv.getUint16(p + 28, true)
    const extraLen = dv.getUint16(p + 30, true)
    const commentLen = dv.getUint16(p + 32, true)
    const offset = dv.getUint32(p + 42, true)
    const name = dec.decode(buf.subarray(p + 46, p + 46 + nameLen))
    entries.set(name.replace(/^\//, ''), { name, method, compressedSize, offset })
    p += 46 + nameLen + extraLen + commentLen
  }
  return entries
}

async function inflateRaw(data: Uint8Array): Promise<Uint8Array> {
  const stream = new Blob([data]).stream().pipeThrough(new DecompressionStream('deflate-raw'))
  return new Uint8Array(await new Response(stream).arrayBuffer())
}

async function readPart(buf: Uint8Array, entries: Map<string, ZipEntry>, path: string): Promise<string | null> {
  const e = entries.get(path)
  if (!e) return null
  const dv = new DataView(buf.buffer, buf.byteOffset, buf.byteLength)
  if (dv.getUint32(e.offset, true) !== 0x04034b50) throw new Error('This Excel file is damaged.')
  const start = e.offset + 30 + dv.getUint16(e.offset + 26, true) + dv.getUint16(e.offset + 28, true)
  const raw = buf.subarray(start, start + e.compressedSize)
  const bytes = e.method === 0 ? raw : e.method === 8 ? await inflateRaw(raw) : null
  if (!bytes) throw new Error('This Excel file uses a compression CaDesk cannot read. Save it again in Excel as .xlsx.')
  return new TextDecoder().decode(bytes)
}

const ENTITIES: Record<string, string> = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'" }
function unescapeXml(s: string): string {
  return s.replace(/&(#x[0-9a-fA-F]+|#\d+|amp|lt|gt|quot|apos);/g, (_, e: string) =>
    e[0] === '#' ? String.fromCodePoint(e[1] === 'x' ? parseInt(e.slice(2), 16) : parseInt(e.slice(1), 10)) : ENTITIES[e])
}
const attr = (attrs: string, name: string) => {
  const m = new RegExp(`(?:^|\\s)${name}="([^"]*)"`).exec(attrs)
  return m ? unescapeXml(m[1]) : null
}
/** All <t> text in a piece of XML, joined (rich-text runs are several <t>s; phonetic <rPh> runs are skipped). */
const textOf = (xml: string) => {
  const noPhonetic = xml.replace(/<rPh\b[\s\S]*?<\/rPh>/g, '')
  let out = ''
  for (const m of noPhonetic.matchAll(/<t(?:\s[^>]*)?>([\s\S]*?)<\/t>|<t(?:\s[^>]*)?\/>/g)) out += unescapeXml(m[1] ?? '')
  return out
}

const BUILTIN_DATE_FORMATS = new Set([14, 15, 16, 17, 22, 27, 30, 36, 45, 46, 47, 50, 57])
function isDateFormat(code: string): boolean {
  const s = code.replace(/"[^"]*"/g, '').replace(/\[[^\]]*\]/g, '').replace(/\\./g, '')
  return /[dy]/i.test(s) || (/m/i.test(s) && !/[0#]/.test(s) && /[ds]/i.test(code))
}

function columnIndex(ref: string): number {
  let n = 0
  for (const ch of ref) {
    const c = ch.charCodeAt(0)
    if (c < 65 || c > 90) break
    n = n * 26 + (c - 64)
  }
  return n - 1
}

function excelDate(serial: number, date1904: boolean): string {
  const ms = Math.round((serial + (date1904 ? 1462 : 0)) * 86_400_000) + Date.UTC(1899, 11, 30)
  return new Date(ms).toISOString().slice(0, 10)
}

function formatNumber(v: string): string {
  const n = Number(v)
  if (!Number.isFinite(n)) return v
  return String(Math.round(n * 1e8) / 1e8) // removes binary noise such as 0.30000000000000004
}

export async function readXlsxRows(data: ArrayBuffer): Promise<string[][]> {
  const buf = new Uint8Array(data)
  if (buf.length >= 8 && buf[0] === 0xd0 && buf[1] === 0xcf) {
    throw new Error('This is an old Excel 97-2003 (.xls) file. Open it in Excel and save it as .xlsx, then upload that.')
  }
  const entries = readZipDirectory(buf)
  const workbook = await readPart(buf, entries, 'xl/workbook.xml')
  if (!workbook) throw new Error('This file is not an Excel workbook.')

  // first sheet in tab order → its part
  const first = /<sheet\b([^>]*?)\/?>/.exec(workbook)
  const relId = first ? attr(first[1], 'r:id') : null
  const rels = (await readPart(buf, entries, 'xl/_rels/workbook.xml.rels')) ?? ''
  let target = 'worksheets/sheet1.xml'
  for (const m of rels.matchAll(/<Relationship\b([^>]*?)\/?>/g)) {
    if (attr(m[1], 'Id') === relId) target = attr(m[1], 'Target') ?? target
  }
  const sheetPath = target.startsWith('/') ? target.slice(1) : `xl/${target.replace(/^\.\//, '')}`
  const sheet = await readPart(buf, entries, sheetPath)
  if (!sheet) throw new Error('The first worksheet could not be read.')
  const date1904 = /<workbookPr\b[^>]*date1904="(1|true)"/.test(workbook)

  const sharedXml = (await readPart(buf, entries, 'xl/sharedStrings.xml')) ?? ''
  const shared = [...sharedXml.matchAll(/<si\b[^>]*>([\s\S]*?)<\/si>|<si\b[^>]*\/>/g)].map(m => textOf(m[1] ?? ''))

  const styles = (await readPart(buf, entries, 'xl/styles.xml')) ?? ''
  const customFormats = new Map<number, string>()
  for (const m of styles.matchAll(/<numFmt\b([^>]*?)\/?>/g)) {
    const id = Number(attr(m[1], 'numFmtId')), code = attr(m[1], 'formatCode') ?? ''
    customFormats.set(id, code)
  }
  const cellXfs = /<cellXfs\b[^>]*>([\s\S]*?)<\/cellXfs>/.exec(styles)?.[1] ?? ''
  const styleIsDate = [...cellXfs.matchAll(/<xf\b([^>]*?)(?:\/>|>)/g)].map(m => {
    const id = Number(attr(m[1], 'numFmtId') ?? 0)
    return BUILTIN_DATE_FORMATS.has(id) || (customFormats.has(id) && isDateFormat(customFormats.get(id)!))
  })

  const rows: string[][] = []
  for (const rm of sheet.matchAll(/<row\b([^>]*?)(?:\/>|>([\s\S]*?)<\/row>)/g)) {
    const rowNo = Number(attr(rm[1], 'r') ?? rows.length + 1)
    const cells: string[] = []
    let next = 0
    for (const cm of (rm[2] ?? '').matchAll(/<c\b([^>]*?)(?:\/>|>([\s\S]*?)<\/c>)/g)) {
      const a = cm[1], inner = cm[2] ?? ''
      const ref = attr(a, 'r')
      const col = ref ? columnIndex(ref) : next
      next = col + 1
      const type = attr(a, 't') ?? 'n'
      const v = /<v>([\s\S]*?)<\/v>/.exec(inner)?.[1]
      let text = ''
      if (type === 's') text = v !== undefined ? shared[Number(v)] ?? '' : ''
      else if (type === 'inlineStr') text = textOf(inner)
      else if (type === 'str') text = v !== undefined ? unescapeXml(v) : ''
      else if (type === 'b') text = v === '1' ? 'TRUE' : 'FALSE'
      else if (type === 'e') text = ''
      else if (v !== undefined) {
        const s = Number(attr(a, 's') ?? 0)
        text = styleIsDate[s] && Number.isFinite(Number(v)) ? excelDate(Number(v), date1904) : formatNumber(v)
      }
      cells[col] = text
    }
    const dense = Array.from(cells, c => c ?? '')
    if (dense.some(c => c.trim() !== '')) rows[rowNo - 1] = dense
  }
  // Keep positions so row numbers in messages match Excel's; empty rows are [].
  return Array.from(rows, r => r ?? [])
}
