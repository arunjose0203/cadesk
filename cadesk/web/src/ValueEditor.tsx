import type { Json, Schema } from './format'

/** Picks an input to match the setting's schema. */
export function ValueEditor({ schema, value, onChange }: { schema: Schema; value: Json; onChange: (v: Json) => void }) {
  if (schema.type === 'boolean')
    return <label className="row"><input type="checkbox" checked={value === true} onChange={e => onChange(e.target.checked)} /> {value ? 'Yes' : 'No'}</label>
  if (schema.enum)
    return <select value={String(value)} onChange={e => onChange(e.target.value)}>
      {schema.enum.map(o => <option key={String(o)} value={String(o)}>{String(o)}</option>)}
    </select>
  if (schema.type === 'integer' || schema.type === 'number')
    return <input type="number" min={schema.minimum} max={schema.maximum} step={schema.type === 'integer' ? 1 : 0.01}
      value={value as number} onChange={e => onChange(e.target.value === '' ? '' : Number(e.target.value))} />
  if (schema.type === 'array' && schema.items?.enum) {
    const list = (value as Json[]) ?? []
    return <div className="row">
      {schema.items.enum.map(o => (
        <label key={String(o)} className="row small">
          <input type="checkbox" checked={list.includes(o)}
            onChange={e => onChange(e.target.checked ? [...list, o] : list.filter(x => x !== o))} /> {String(o)}
        </label>
      ))}
    </div>
  }
  if (schema.type === 'array') {
    const numeric = schema.items?.type === 'integer' || schema.items?.type === 'number'
    return <input value={((value as Json[]) ?? []).join(', ')} placeholder="comma separated"
      onChange={e => onChange(e.target.value.split(',').map(s => s.trim()).filter(Boolean)
        .map(s => (numeric && !Number.isNaN(Number(s)) ? Number(s) : s)))} />
  }
  return <input value={(value as string) ?? ''} onChange={e => onChange(e.target.value)} />
}
