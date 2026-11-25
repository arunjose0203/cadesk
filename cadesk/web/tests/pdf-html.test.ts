import { test } from 'node:test'
import assert from 'node:assert/strict'
import { htmlToBlocks, decodeEntities } from '../src/pdf/html.ts'
import { renderPdf } from '../src/pdf/pdf.ts'

test('entities decode', () => {
  assert.equal(decodeEntities('A &amp; B &#8377;10 &#x2013; &nbsp;x &bogus;'), 'A & B ₹10 –  x &bogus;')
})

test('structure becomes blocks; styles and scripts are dropped', () => {
  const html = `<!DOCTYPE html><html><head><title>t</title><style>h1{color:red}</style></head><body>
    <h1>Independent Auditor&rsquo;s Report</h1><p>To the Members of <b>Kochi Spices</b></p>
    <script>alert(1)</script>
    <h4>Opinion</h4><p style="text-align:right">Kochi, 24 Nov 2026</p>
    <ul><li>Confirmations sent</li><li>Ageing <i>reviewed</i></li></ul>
    <ol><li>One<ul><li>nested</li></ul></li></ol>
    <table><thead><tr><th>Ledger</th><th>Balance</th></tr></thead>
      <tbody><tr><td>Debtors</td><td>1,000.00</td></tr><tr><td>Creditors</td><td>(250.50)</td></tr><tr><td></td><td></td></tr></tbody></table>
    <div style="page-break-before:always">Annexure</div></body></html>`
  const b = htmlToBlocks(html)
  assert.deepEqual(b[0], { kind: 'heading', level: 1, text: 'Independent Auditor’s Report' })
  assert.equal(b[1].kind === 'paragraph' && b[1].text, 'To the Members of Kochi Spices')
  assert.deepEqual(b[2], { kind: 'heading', level: 3, text: 'Opinion' })
  assert.deepEqual(b[3], { kind: 'paragraph', text: 'Kochi, 24 Nov 2026', align: 'right' })
  assert.deepEqual(b[4], { kind: 'list', items: ['Confirmations sent', 'Ageing reviewed'], ordered: false })
  assert.deepEqual(b[5], { kind: 'list', items: ['One', '– nested'], ordered: true })
  assert.deepEqual(b[6], { kind: 'table', header: ['Ledger', 'Balance'], rows: [['Debtors', '1,000.00'], ['Creditors', '(250.50)']], align: ['left', 'right'] })
  assert.deepEqual(b[7], { kind: 'pageBreak' })
  assert.equal(b[8].kind === 'paragraph' && b[8].text, 'Annexure')
  assert.ok(!JSON.stringify(b).includes('alert'))
  const pdf = renderPdf(b, { title: 'Report' })
  assert.equal(String.fromCharCode(...pdf.slice(0, 5)), '%PDF-')
})
