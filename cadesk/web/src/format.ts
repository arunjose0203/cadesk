export type Schema = {
  type?: string; enum?: (string | number)[]; minimum?: number; maximum?: number
  items?: Schema; minItems?: number; maxItems?: number
}
export type Json = string | number | boolean | null | Json[] | { [k: string]: Json }

/** Human-readable form of a setting value. */
export function show(v: Json | null): string {
  if (v === null || v === undefined) return '—'
  if (Array.isArray(v)) return v.length ? v.join(', ') : '(none)'
  if (typeof v === 'boolean') return v ? 'Yes' : 'No'
  if (typeof v === 'object') {
    const entries = Object.entries(v)
    if (entries.length === 0) return '(none)'
    return entries.map(([k, val]) => `${k}: ${val}`).join(', ')
  }
  return String(v)
}

/** Formats ISO date 'yyyy-MM-dd' to 'dd/MM/yyyy'. */
export function fmtDate(d: string | null | undefined): string {
  if (!d) return '—'
  const parts = d.slice(0, 10).split('-')
  if (parts.length === 3) return `${parts[2]}/${parts[1]}/${parts[0]}`
  return d
}
