import { test } from 'node:test'
import assert from 'node:assert/strict'
import { parseCapitalGainsSheetRows, parseCsvOrTsvRows } from '../src/tax/capitalGainsImport.ts'

test('imports Zerodha Tax P&L CSV with STCG 111A, LTCG 112A, and Sec 55(2)(ac) pre-31-Jan-2018 grandfathering', () => {
  const csv = [
    'Symbol,ISIN,Entry Date,Exit Date,Quantity,Buy Value,Sell Value,Profit,Fair Market Value,Term',
    'INFY,INE009A01021,15-05-2017,20-07-2025,100,90000,190000,100000,130000,Long Term',
    'TCS,INE467B01029,10-01-2025,18-10-2025,50,180000,225000,45000,0,Short Term',
  ].join('\n')

  const res = parseCapitalGainsSheetRows([{ name: 'Zerodha Equity', rows: parseCsvOrTsvRows(csv) }])
  assert.equal(res.trades.length, 2)
  assert.equal(res.grandfatheredCount, 1)
  // INFY grandfathered: effective cost = max(90000, min(130000, 190000)) = 130000 -> LTCG 60000 in Q2 (Jul 20)
  assert.equal(res.ltcg112A, 60000)
  assert.equal(res.stcg111A, 45000)
  assert.equal(res.quarterlyTotals.q2, 60000)
  assert.equal(res.quarterlyTotals.q3, 45000)
  assert.equal(res.trades[0].isinCode, 'INE009A01021')
  assert.equal(res.trades[0].buyDate, '2017-05-15')
})

test('imports CAMS Mutual Fund & Crypto VDA sheets into separate 112/112A and 115BBH buckets', () => {
  const camsCsv = [
    'Scheme Name,ISIN,Purchase Date,Redemption Date,Purchase Amount,Redemption Amount,Asset Category',
    'HDFC Corporate Bond Direct,INF179K01Z89,2022-02-10,2025-12-20,500000,620000,Debt Long Term',
  ].join('\n')
  const cryptoCsv = [
    'Coin,Date of Purchase,Date of Sale,Cost,INR Value,Head',
    'Bitcoin BTC,2024-06-01,2026-03-20,150000,350000,Crypto VDA 115BBH',
  ].join('\n')

  const res = parseCapitalGainsSheetRows([
    { name: 'CAMS MF Report', rows: parseCsvOrTsvRows(camsCsv) },
    { name: 'CoinDCX Crypto VDA', rows: parseCsvOrTsvRows(cryptoCsv) },
  ])
  assert.equal(res.ltcg112, 120000)
  assert.equal(res.vdaIncome115BBH, 200000)
  assert.equal(res.quarterlyTotals.q4, 120000)
  assert.equal(res.quarterlyTotals.q5, 200000)
})
