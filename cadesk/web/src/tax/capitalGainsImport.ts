import type { CapitalGainTradeEntry } from '../api.ts'
import { readXlsxAllSheets } from '../gst/xlsx.ts'

export type ImportedCapitalGainsSummary = {
  detectedBroker: string
  trades: CapitalGainTradeEntry[]
  stcg111ASaleValue: number
  stcg111ACost: number
  stcg111A: number
  stcgNormal: number
  ltcg112ASaleValue: number
  ltcg112ACost: number
  ltcg112A: number
  ltcg112SaleValue: number
  ltcg112Cost: number
  ltcg112: number
  vdaConsideration: number
  vdaCostOfAcquisition: number
  vdaIncome115BBH: number
  grandfatheredCount: number
  quarterlyTotals: { q1: number; q2: number; q3: number; q4: number; q5: number }
  warnings: string[]
}

function splitCsvLine(line: string, delim: string): string[] {
  const out: string[] = []
  let cur = ''
  let inQ = false
  for (let i = 0; i < line.length; i++) {
    const ch = line[i]
    if (ch === '"') {
      if (inQ && line[i + 1] === '"') { cur += '"'; i++ }
      else inQ = !inQ
    } else if (ch === delim && !inQ) {
      out.push(cur.trim())
      cur = ''
    } else {
      cur += ch
    }
  }
  out.push(cur.trim())
  return out
}

export function parseCsvOrTsvRows(text: string): string[][] {
  const lines = text.replace(/\r\n/g, '\n').replace(/\r/g, '\n').split('\n').filter(l => l.trim().length > 0)
  if (lines.length === 0) return []
  const sample = lines[0]
  const tabCount = (sample.match(/\t/g) ?? []).length
  const commaCount = (sample.match(/,/g) ?? []).length
  const delim = tabCount > commaCount ? '\t' : ','
  return lines.map(l => splitCsvLine(l, delim))
}

function toNum(v: string | undefined): number {
  if (!v) return 0
  const cleaned = v.replace(/[,₹\s]/g, '').replace(/^Rs\.?/i, '')
  const parenNeg = /^\((.+)\)$/.exec(cleaned)
  const raw = parenNeg ? `-${parenNeg[1]}` : cleaned
  const n = Number(raw)
  return Number.isFinite(n) ? n : 0
}

const MONTHS: Record<string, string> = {
  jan: '01', feb: '02', mar: '03', apr: '04', may: '05', jun: '06',
  jul: '07', aug: '08', sep: '09', sept: '09', oct: '10', nov: '11', dec: '12',
}

export function normalizeTradeDate(raw: string | undefined): string | null {
  if (!raw) return null
  const s = raw.trim().split(' ')[0] ?? ''
  if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return s
  const dmy = /^(\d{1,2})[/-](\d{1,2})[/-](\d{4})$/.exec(s)
  if (dmy) {
    const d = dmy[1].padStart(2, '0'), m = dmy[2].padStart(2, '0'), y = dmy[3]
    return `${y}-${m}-${d}`
  }
  const dMonY = /^(\d{1,2})[-\s]([A-Za-z]{3,4})[-\s](\d{2,4})$/.exec(s)
  if (dMonY) {
    const d = dMonY[1].padStart(2, '0')
    const m = MONTHS[dMonY[2].toLowerCase()]
    const y = dMonY[3].length === 2 ? `20${dMonY[3]}` : dMonY[3]
    if (m) return `${y}-${m}-${d}`
  }
  return null
}

function holdingDays(buyIso: string | null, sellIso: string | null): number | null {
  if (!buyIso || !sellIso) return null
  const b = Date.parse(buyIso), s = Date.parse(sellIso)
  if (Number.isNaN(b) || Number.isNaN(s)) return null
  return Math.round((s - b) / 86_400_000)
}

function quarterOfDate(iso: string | null): 'q1' | 'q2' | 'q3' | 'q4' | 'q5' {
  if (!iso) return 'q5'
  const m = Number(iso.slice(5, 7)), d = Number(iso.slice(8, 10))
  if (m === 4 || m === 5 || (m === 6 && d <= 15)) return 'q1'
  if (m === 6 || m === 7 || m === 8 || (m === 9 && d <= 15)) return 'q2'
  if (m === 9 || m === 10 || m === 11 || (m === 12 && d <= 15)) return 'q3'
  if (m === 12 || m === 1 || m === 2 || (m === 3 && d <= 15)) return 'q4'
  return 'q5'
}

type ColIdx = {
  symbol: number; isin: number; buyDate: number; sellDate: number; qty: number;
  buyVal: number; sellVal: number; expenses: number; fmv2018: number;
  termOrType: number; vdaFlag: number; stcgCol: number; ltcgCol: number; gainCol: number;
}

function matchHeaderColumns(header: string[]): ColIdx | null {
  const norm = header.map(h => h.toLowerCase().replace(/[^a-z0-9]/g, ''))
  const find = (...keys: string[]) => norm.findIndex(h => keys.some(k => h === k || h.includes(k)))

  const symbol = find('symbol', 'stockname', 'schemename', 'scripname', 'asset', 'coin', 'token', 'securityname')
  const isin = find('isin', 'isincode')
  const sellVal = find('sellvalue', 'redemptionamount', 'salevalue', 'saleconsideration', 'exitvalue', 'proceeds', 'inrvalue')
  const buyVal = find('buyvalue', 'purchaseamount', 'purchasevalue', 'costofacquisition', 'entryvalue', 'buycost', 'cost')
  if (symbol < 0 || (sellVal < 0 && buyVal < 0)) return null

  return {
    symbol,
    isin,
    buyDate: find('entrydate', 'buydate', 'purchasedate', 'acquisitiondate', 'dateofpurchase'),
    sellDate: find('exitdate', 'selldate', 'redemptiondate', 'transferdate', 'dateofsale'),
    qty: find('quantity', 'qty', 'units'),
    buyVal,
    sellVal,
    expenses: find('brokerage', 'charges', 'expenses', 'stt', 'transferexpenses'),
    fmv2018: find('fairmarketvalue', 'fmv2018', 'fmv', '31jan2018', 'grandfathered'),
    termOrType: find('gainterm', 'term', 'periodofholding', 'assettype', 'category', 'type', 'head'),
    vdaFlag: find('crypto', 'vda', '115bbh', 'coin', 'token'),
    stcgCol: find('stcg', 'shorttermcapitalgain'),
    ltcgCol: find('ltcg', 'longtermcapitalgain'),
    gainCol: find('profit', 'realisedpnl', 'realizedpnl', 'gain', 'pnl'),
  }
}

function detectBrokerLabel(rows: string[][], sheetName: string, cols: ColIdx): string {
  const flat = `${sheetName} ${rows.slice(0, 6).flat().join(' ')}`.toLowerCase()
  if (flat.includes('zerodha') || cols.termOrType >= 0 && flat.includes('entry date') && flat.includes('exit date')) return 'Zerodha'
  if (flat.includes('groww')) return 'Groww'
  if (flat.includes('cams') || flat.includes('kfintech') || flat.includes('redemption amount')) return 'CAMS'
  if (flat.includes('coindcx') || flat.includes('wazirx') || cols.vdaFlag >= 0) return 'Crypto VDA'
  return 'Broker Import'
}

export function parseCapitalGainsSheetRows(sheets: { name: string; rows: string[][] }[]): ImportedCapitalGainsSummary {
  const trades: CapitalGainTradeEntry[] = []
  const warnings: string[] = []
  const brokers = new Set<string>()
  const qTotals = { q1: 0, q2: 0, q3: 0, q4: 0, q5: 0 }
  let grandfatheredCount = 0

  for (const sheet of sheets) {
    const rows = sheet.rows
    let headerIdx = -1
    let cols: ColIdx | null = null
    for (let i = 0; i < Math.min(rows.length, 30); i++) {
      cols = matchHeaderColumns(rows[i])
      if (cols) { headerIdx = i; break }
    }
    if (!cols || headerIdx < 0) continue

    const broker = detectBrokerLabel(rows, sheet.name, cols)
    brokers.add(broker)

    for (let r = headerIdx + 1; r < rows.length; r++) {
      const row = rows[r]
      const assetName = (row[cols.symbol] ?? '').trim()
      if (!assetName || /^(total|grand total|summary)/i.test(assetName)) continue

      const saleValue = Math.max(0, cols.sellVal >= 0 ? toNum(row[cols.sellVal]) : 0)
      const costVal = Math.max(0, cols.buyVal >= 0 ? toNum(row[cols.buyVal]) : 0)
      if (saleValue === 0 && costVal === 0) continue

      const buyDate = normalizeTradeDate(cols.buyDate >= 0 ? row[cols.buyDate] : undefined)
      const sellDate = normalizeTradeDate(cols.sellDate >= 0 ? row[cols.sellDate] : undefined)
      const qty = cols.qty >= 0 ? Math.max(0, toNum(row[cols.qty])) : 0
      const fmvJan2018 = cols.fmv2018 >= 0 ? Math.max(0, toNum(row[cols.fmv2018])) : 0
      const expenses = cols.expenses >= 0 ? Math.max(0, toNum(row[cols.expenses])) : 0
      const isinRaw = cols.isin >= 0 ? (row[cols.isin] ?? '').trim().toUpperCase() : ''

      const termRaw = (cols.termOrType >= 0 ? row[cols.termOrType] ?? '' : '').toLowerCase()
      const sheetLower = sheet.name.toLowerCase()
      const stcgVal = cols.stcgCol >= 0 ? toNum(row[cols.stcgCol]) : 0
      const ltcgVal = cols.ltcgCol >= 0 ? toNum(row[cols.ltcgCol]) : 0

      let assetCategory: CapitalGainTradeEntry['assetCategory'] = '112A'
      if (broker === 'Crypto VDA' || termRaw.includes('vda') || termRaw.includes('crypto') || termRaw.includes('115bbh') || sheetLower.includes('crypto') || sheetLower.includes('vda')) {
        assetCategory = 'VDA'
      } else if (termRaw.includes('112a') || termRaw.includes('ltcg') || termRaw.includes('long') || (ltcgVal !== 0 && stcgVal === 0)) {
        assetCategory = (termRaw.includes('debt') || termRaw.includes('gold') || termRaw.includes('property') || termRaw.includes('unlisted')) ? '112' : '112A'
      } else if (termRaw.includes('111a') || termRaw.includes('stcg') || termRaw.includes('short') || (stcgVal !== 0 && ltcgVal === 0)) {
        assetCategory = (termRaw.includes('debt') || termRaw.includes('other') || termRaw.includes('slab')) ? 'STCG_NORMAL' : '111A'
      } else {
        const days = holdingDays(buyDate, sellDate)
        if (days !== null) assetCategory = days > 365 ? '112A' : '111A'
      }

      const isGrandfathered = assetCategory === '112A' && buyDate !== null && buyDate <= '2018-01-31' && fmvJan2018 > 0
      if (isGrandfathered) grandfatheredCount++

      const effCost = isGrandfathered ? Math.max(costVal, Math.min(fmvJan2018, saleValue)) : costVal
      const netGain = saleValue - effCost - (assetCategory === 'VDA' ? 0 : expenses)
      if (netGain > 0) {
        qTotals[quarterOfDate(sellDate)] += netGain
      }

      trades.push({
        brokerSource: broker,
        assetCategory,
        isinCode: /^IN[0-9A-Z]{10}$/.test(isinRaw) ? isinRaw : null,
        assetName,
        buyDate,
        sellDate,
        quantity: qty,
        saleValue,
        costOfAcquisition: costVal,
        fairMarketValueJan2018: fmvJan2018,
        transferExpenses: expenses,
      })
    }
  }

  if (trades.length === 0) {
    warnings.push('No recognized Capital Gains rows found. Ensure headers contain Symbol/Scheme Name, Sell Value/Redemption, and Buy Value/Cost.')
  }

  let s111a = 0, c111a = 0, gNorm = 0, s112a = 0, c112a = 0, s112 = 0, c112 = 0, svda = 0, cvda = 0
  for (const t of trades) {
    const isGf = t.assetCategory === '112A' && !!t.buyDate && t.buyDate <= '2018-01-31' && t.fairMarketValueJan2018 > 0
    const effCost = isGf ? Math.max(t.costOfAcquisition, Math.min(t.fairMarketValueJan2018, t.saleValue)) : t.costOfAcquisition
    const exp = t.assetCategory === 'VDA' ? 0 : t.transferExpenses
    if (t.assetCategory === '111A') { s111a += t.saleValue; c111a += effCost + exp }
    else if (t.assetCategory === 'STCG_NORMAL') { gNorm += Math.max(0, t.saleValue - effCost - exp) }
    else if (t.assetCategory === '112A') { s112a += t.saleValue; c112a += effCost + exp }
    else if (t.assetCategory === '112') { s112 += t.saleValue; c112 += effCost + exp }
    else if (t.assetCategory === 'VDA') { svda += t.saleValue; cvda += t.costOfAcquisition }
  }

  return {
    detectedBroker: [...brokers].join(' + ') || 'Broker Import',
    trades,
    stcg111ASaleValue: Math.round(s111a),
    stcg111ACost: Math.round(c111a),
    stcg111A: Math.max(0, Math.round(s111a - c111a)),
    stcgNormal: Math.round(gNorm),
    ltcg112ASaleValue: Math.round(s112a),
    ltcg112ACost: Math.round(c112a),
    ltcg112A: Math.max(0, Math.round(s112a - c112a)),
    ltcg112SaleValue: Math.round(s112),
    ltcg112Cost: Math.round(c112),
    ltcg112: Math.max(0, Math.round(s112 - c112)),
    vdaConsideration: Math.round(svda),
    vdaCostOfAcquisition: Math.round(cvda),
    vdaIncome115BBH: Math.max(0, Math.round(svda - cvda)),
    grandfatheredCount,
    quarterlyTotals: {
      q1: Math.round(qTotals.q1),
      q2: Math.round(qTotals.q2),
      q3: Math.round(qTotals.q3),
      q4: Math.round(qTotals.q4),
      q5: Math.round(qTotals.q5),
    },
    warnings,
  }
}

export async function importCapitalGainsFile(file: { name: string; buffer: ArrayBuffer; text?: () => Promise<string> }): Promise<ImportedCapitalGainsSummary> {
  const lower = file.name.toLowerCase()
  if (lower.endsWith('.xlsx')) {
    const sheets = await readXlsxAllSheets(file.buffer)
    return parseCapitalGainsSheetRows(sheets)
  }
  const text = file.text ? await file.text() : new TextDecoder().decode(new Uint8Array(file.buffer))
  return parseCapitalGainsSheetRows([{ name: file.name, rows: parseCsvOrTsvRows(text) }])
}
