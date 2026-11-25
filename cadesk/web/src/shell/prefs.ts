import { useCallback, useEffect, useState } from 'react'
import type { Page } from './nav'

/* Small per-browser preferences: theme, pinned desks, recent desks, collapsed
   sidebar sections. Stored in localStorage; every access is guarded because
   storage can be full, blocked or wiped. Nothing here is security-relevant. */

function read<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key)
    return raw === null ? fallback : (JSON.parse(raw) as T)
  } catch {
    return fallback
  }
}

function write(key: string, value: unknown) {
  try {
    localStorage.setItem(key, JSON.stringify(value))
  } catch {
    /* storage unavailable: keep the in-memory value only */
  }
}

export function useStored<T>(key: string, fallback: T): [T, (next: T | ((prev: T) => T)) => void] {
  const [value, setValue] = useState<T>(() => read(key, fallback))
  const set = useCallback((next: T | ((prev: T) => T)) => {
    setValue(prev => {
      const v = typeof next === 'function' ? (next as (p: T) => T)(prev) : next
      write(key, v)
      return v
    })
  }, [key])
  return [value, set]
}

export type ThemeChoice = 'system' | 'light' | 'dark'

/* The theme key is a plain string (not JSON) because index.html reads it
   before React loads, to avoid a flash of the wrong theme. */
export function useTheme(): [ThemeChoice, () => void] {
  const [theme, setTheme] = useState<ThemeChoice>(() => {
    try {
      const t = localStorage.getItem('cadesk.theme')
      return t === 'light' || t === 'dark' ? t : 'system'
    } catch {
      return 'system'
    }
  })

  useEffect(() => {
    const root = document.documentElement
    if (theme === 'system') root.removeAttribute('data-theme')
    else root.setAttribute('data-theme', theme)
    try {
      if (theme === 'system') localStorage.removeItem('cadesk.theme')
      else localStorage.setItem('cadesk.theme', theme)
    } catch { /* ignore */ }
  }, [theme])

  const cycle = useCallback(() => {
    setTheme(t => (t === 'system' ? 'light' : t === 'light' ? 'dark' : 'system'))
  }, [])

  return [theme, cycle]
}

export function pushRecent(list: Page[], page: Page, max = 6): Page[] {
  if (page === 'today') return list
  return [page, ...list.filter(p => p !== page)].slice(0, max)
}

/* India's financial year runs April to March. */
export function currentFinancialYear(today = new Date()): { fy: string; ay: string } {
  const y = today.getMonth() >= 3 ? today.getFullYear() : today.getFullYear() - 1
  const short = (n: number) => String(n % 100).padStart(2, '0')
  return { fy: `${y}-${short(y + 1)}`, ay: `${y + 1}-${short(y + 2)}` }
}
