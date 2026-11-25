import { api } from '../api'

/* Error monitoring (Sprint 72). Crashes in the browser are sent to /api/problem-reports so they can be
   fixed without waiting for someone to complain; the last few errors are also attached when a user
   reports a problem. Reports never include form contents or client data, only messages and stack lines. */

export type RecentError = { at: string; message: string; page: string }

const MAX_SENT_PER_LOAD = 5
const recent: RecentError[] = []
const sent = new Set<string>()
let started = false

/** The build's asset hash, e.g. "D8xk2b1a", so a report says which version was running. */
export function appVersion(): string {
  const src = document.querySelector<HTMLScriptElement>('script[type="module"][src*="/assets/"]')?.src ?? ''
  return /index-([\w-]+)\.js/.exec(src)?.[1] ?? 'dev'
}

export function currentPage(): string {
  return new URLSearchParams(window.location.search).get('page') ?? 'today'
}

export function recentErrors(): RecentError[] {
  return [...recent]
}

function remember(message: string) {
  recent.push({ at: new Date().toISOString(), message: message.slice(0, 500), page: currentPage() })
  if (recent.length > 10) recent.shift()
}

/** Keeps only the first lines of a stack and drops query strings, which can carry tokens. */
export function cleanStack(stack: string | undefined): string | undefined {
  if (!stack) return undefined
  return stack.split('\n').slice(0, 12).map(l => l.replace(/\?[^\s):]*/g, '')).join('\n').slice(0, 4000)
}

export function reportCrash(error: unknown, context?: string) {
  const err = error instanceof Error ? error : new Error(String(error))
  const message = (context ? `${context}: ` : '') + (err.message || err.name || 'Unknown error')
  remember(message)
  const key = message + '|' + currentPage()
  if (sent.has(key) || sent.size >= MAX_SENT_PER_LOAD) return
  sent.add(key)
  api('/api/problem-reports', {
    method: 'POST',
    body: JSON.stringify({ kind: 'crash', message: message.slice(0, 2000), page: currentPage(), detail: cleanStack(err.stack), appVersion: appVersion() }),
  }).catch(() => { /* never let reporting cause more errors */ })
}

export function startMonitoring() {
  if (started) return
  started = true
  window.addEventListener('error', e => {
    // resource load errors (an image) have no error object; script errors do
    if (e.error) reportCrash(e.error)
  })
  window.addEventListener('unhandledrejection', e => {
    const reason = e.reason
    // failed API calls are handled by the desks and arrive through cadesk:api-error when they matter
    if (reason && typeof reason === 'object' && 'status' in reason) return
    reportCrash(reason, 'Unhandled promise')
  })
  window.addEventListener('cadesk:api-error', e => {
    const d = (e as CustomEvent<{ status: number; path: string; title: string; errorId?: string }>).detail
    remember(`${d.status} ${d.path.split('?')[0]}: ${d.title}${d.errorId ? ` (${d.errorId})` : ''}`)
  })
}
