import { test } from 'node:test'
import assert from 'node:assert/strict'
import { writeFileSync, mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { renderPdf, textWidth, toWinAnsi, wrap, type Block } from '../src/pdf/pdf.ts'

test('WinAnsi conversion: rupee sign, curly quotes, dashes', () => {
  assert.equal(toWinAnsi('₹1,180'), 'Rs.1,180')
  assert.equal(toWinAnsi('‘a’ “b” – —').split('').map(c => c.charCodeAt(0).toString(16)).join(' '), '91 61 92 20 93 62 94 20 96 20 97')
  assert.equal(toWinAnsi('Café'), 'Caf\xe9')
  assert.equal(toWinAnsi('मलयालम'), '??????')
})

test('widths and wrapping use the real Helvetica metrics', () => {
  assert.equal(textWidth('W', 1000), 944)
  assert.equal(textWidth('i', 1000), 222)
  assert.equal(textWidth('W', 1000, true), 944)
  const lines = wrap('The quick brown fox jumps over the lazy dog '.repeat(5), 200, 10)
  assert.ok(lines.length > 4)
  for (const l of lines) assert.ok(textWidth(l, 10) <= 200)
  assert.deepEqual(wrap('', 100, 10), [''])
  assert.ok(wrap('x'.repeat(200), 100, 10).every(l => textWidth(l, 10) <= 100))
})

test('a long document: valid PDF, bookmarks, page numbers, table across pages', () => {
  const rows = Array.from({ length: 120 }, (_, i) => [`${i + 1}`, `Ledger ${i + 1} – Sundry debtors, Kochi`, (1000 + i * 37.5).toFixed(2), '₹ 18%'])
  const blocks: Block[] = [
    { kind: 'heading', level: 1, text: 'SA 230 Audit Documentation Dossier' },
    { kind: 'paragraph', text: 'Menon & Associates, Chartered Accountants. Engagement: statutory audit for FY 2025-26 (AY 2026-27).' },
    { kind: 'heading', level: 2, text: 'A. Lead schedule: Trade receivables' },
    { kind: 'table', header: ['#', 'Ledger', 'Balance', 'Rate'], rows },
    { kind: 'heading', level: 2, text: 'B. Checklist' },
    { kind: 'list', items: ['Balance confirmations sent (SA 505)', 'Ageing reviewed', 'Provision for doubtful debts tested'] },
    { kind: 'pageBreak' },
    { kind: 'heading', level: 1, text: 'Seal' },
    { kind: 'paragraph', text: 'SHA-256: 9f2c…' },
  ]
  const pdf = renderPdf(blocks, { title: 'SA 230 dossier', author: 'CaDesk', header: 'Kochi Spices Exports Pvt Ltd · FY 2025-26', footer: 'Menon & Associates', createdAt: new Date(Date.UTC(2026, 10, 24)) })
  const dir = mkdtempSync(join(tmpdir(), 'pdf-'))
  const file = join(dir, 'dossier.pdf')
  writeFileSync(file, pdf)
  assert.equal(String.fromCharCode(...pdf.slice(0, 8)), '%PDF-1.4')
  assert.ok(pdf.length > 5000)
  // reproducible
  assert.deepEqual(renderPdf(blocks, { title: 'SA 230 dossier', author: 'CaDesk', header: 'Kochi Spices Exports Pvt Ltd · FY 2025-26', footer: 'Menon & Associates', createdAt: new Date(Date.UTC(2026, 10, 24)) }), pdf)
})
