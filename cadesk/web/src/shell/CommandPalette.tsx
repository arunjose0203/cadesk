import { useEffect, useMemo, useRef, useState } from 'react'
import type { NavCategory, NavItem, Page } from './nav'

type Entry = { item: NavItem; category: string }

type Props = {
  nav: NavCategory[]
  recents: Page[]
  onPick: (page: Page) => void
  onClose: () => void
}

/* Ctrl+K / Cmd+K: type part of a desk name, a form number or a section
   ("3cd", "gstr", "15ca", "audit") and press Enter. */
export function CommandPalette({ nav, recents, onPick, onClose }: Props) {
  const [query, setQuery] = useState('')
  const [index, setIndex] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)
  const listRef = useRef<HTMLUListElement>(null)

  const all = useMemo<Entry[]>(
    () => nav.flatMap(c => c.items.filter(i => i.visible).map(item => ({ item, category: c.title }))),
    [nav],
  )

  const groups = useMemo(() => {
    const q = query.trim().toLowerCase()
    if (!q) {
      const recent = recents
        .map(p => all.find(e => e.item.id === p))
        .filter((e): e is Entry => Boolean(e))
      return [
        ...(recent.length ? [{ title: 'Recent', entries: recent }] : []),
        { title: 'All desks', entries: all.filter(e => !recents.includes(e.item.id)) },
      ]
    }
    const words = q.split(/\s+/)
    const score = (e: Entry) => {
      const label = e.item.label.toLowerCase()
      const hay = `${label} ${e.category.toLowerCase()} ${e.item.id}`
      if (!words.every(w => hay.includes(w))) return -1
      if (label.startsWith(q)) return 3
      if (label.split(/[\s/(&-]+/).some(part => part.startsWith(words[0]))) return 2
      return 1
    }
    const hits = all
      .map(e => ({ e, s: score(e) }))
      .filter(x => x.s >= 0)
      .sort((a, b) => b.s - a.s)
      .map(x => x.e)
    return [{ title: 'Desks', entries: hits }]
  }, [query, all, recents])

  const flat = useMemo(() => groups.flatMap(g => g.entries), [groups])

  useEffect(() => { inputRef.current?.focus() }, [])
  useEffect(() => { setIndex(0) }, [query])

  useEffect(() => {
    const el = listRef.current?.querySelector<HTMLElement>(`[data-index="${index}"]`)
    el?.scrollIntoView({ block: 'nearest' })
  }, [index])

  const onKey = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setIndex(i => Math.min(i + 1, flat.length - 1)) }
    else if (e.key === 'ArrowUp') { e.preventDefault(); setIndex(i => Math.max(i - 1, 0)) }
    else if (e.key === 'Enter') { e.preventDefault(); const hit = flat[index]; if (hit) onPick(hit.item.id) }
    else if (e.key === 'Escape') { e.preventDefault(); onClose() }
  }

  let n = -1
  return (
    <div className="palette-backdrop" onMouseDown={e => { if (e.target === e.currentTarget) onClose() }}>
      <div className="palette" role="dialog" aria-modal="true" aria-label="Go to a desk" onKeyDown={onKey}>
        <div className="palette-input-row">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
            <circle cx="11" cy="11" r="8"></circle>
            <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
          </svg>
          <input
            ref={inputRef}
            className="palette-input"
            placeholder="Go to a desk: try 3CD, GSTR, TDS, CARO"
            value={query}
            onChange={e => setQuery(e.target.value)}
            role="combobox"
            aria-expanded="true"
            aria-controls="palette-list"
            aria-activedescendant={flat[index] ? `palette-${flat[index].item.id}` : undefined}
          />
        </div>
        <ul className="palette-list" id="palette-list" role="listbox" ref={listRef}>
          {flat.length === 0 && (
            <li className="palette-empty">No desk matches “{query}”. Try a form number such as 3CD or 26Q.</li>
          )}
          {groups.map(g => g.entries.length > 0 && (
            <li key={g.title} role="presentation">
              <div className="palette-group">{g.title}</div>
              <ul role="presentation" style={{ listStyle: 'none', margin: 0, padding: 0 }}>
                {g.entries.map(e => {
                  n += 1
                  const i = n
                  return (
                    <li
                      key={e.item.id}
                      id={`palette-${e.item.id}`}
                      data-index={i}
                      role="option"
                      aria-selected={i === index}
                      className="palette-item"
                      onMouseMove={() => setIndex(i)}
                      onClick={() => onPick(e.item.id)}
                    >
                      <span className="palette-icon">{e.item.icon}</span>
                      <span>{e.item.label}</span>
                      <span className="palette-cat">{e.category}</span>
                    </li>
                  )
                })}
              </ul>
            </li>
          ))}
        </ul>
        <div className="palette-foot">
          <span><span className="kbd">↑</span> <span className="kbd">↓</span> to move</span>
          <span><span className="kbd">Enter</span> to open</span>
          <span><span className="kbd">Esc</span> to close</span>
        </div>
      </div>
    </div>
  )
}
