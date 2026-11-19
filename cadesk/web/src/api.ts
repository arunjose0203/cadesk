// API client. The access token lives only in memory (never in storage);
// the refresh token is an HttpOnly cookie the browser sends to /api/auth automatically.

let accessToken: string | null = null
let refreshing: Promise<boolean> | null = null
let onSignedOut: (() => void) | null = null

export function setAccessToken(token: string | null) { accessToken = token }
export function onSessionEnded(handler: () => void) { onSignedOut = handler }

export class ApiError extends Error {
  status: number
  errors?: Record<string, string[]>
  constructor(status: number, message: string, errors?: Record<string, string[]>) {
    super(message)
    this.status = status
    this.errors = errors
  }
}

async function raw(path: string, init: RequestInit, token: string | null) {
  const headers = new Headers(init.headers)
  headers.set('Accept', 'application/json')
  if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)
  return fetch(path, { ...init, headers, credentials: 'same-origin' })
}

/** Uses the refresh cookie to get a new access token. One refresh at a time. */
export function refreshSession(): Promise<boolean> {
  refreshing ??= (async () => {
    try {
      let res = await raw('/api/auth/refresh', { method: 'POST' }, null)
      if (res.status === 409) res = await raw('/api/auth/refresh', { method: 'POST' }, null) // another tab just refreshed
      if (!res.ok) return false
      const body = (await res.json()) as AuthResult
      accessToken = body.accessToken ?? null
      return accessToken !== null
    } catch {
      return false
    } finally {
      setTimeout(() => { refreshing = null }, 0)
    }
  })()
  return refreshing
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  let res = await raw(path, init, accessToken)
  if (res.status === 401 && accessToken && !path.startsWith('/api/auth/')) {
    if (await refreshSession()) res = await raw(path, init, accessToken)
    else { accessToken = null; onSignedOut?.() }
  }
  if (res.status === 204) return undefined as T
  const body = await res.json().catch(() => null)
  if (!res.ok) throw new ApiError(res.status, body?.title ?? res.statusText, body?.errors)
  return body as T
}

// ---------------------------------------------------------------- types

export type UserProfile = {
  id: string
  name: string
  email: string
  firmId: string
  firmName: string
  roles: string[]
  permissions: string[]
  mfaEnabled: boolean
  mfaSetupRequired?: boolean
  sessionTimeoutMinutes?: number
}

export type AuthResult = {
  status: 'ok' | 'mfa_required' | 'choose_firm'
  accessToken?: string
  expiresIn?: number
  user?: UserProfile
  mfaToken?: string
  firms?: { firmId: string; firmName: string }[]
}

export type Client = {
  id: string
  code: string
  name: string
  legalName?: string | null
  countryCode: string
  entityType: string
  taxIds: Record<string, string>
  email?: string | null
  phone?: string | null
  isActive: boolean
  version: number
  groupId?: string | null
  groupName?: string | null
  assignedUserId?: string | null
  assignedUserName?: string | null
  custom?: Record<string, string> | null
  warnings?: string[] | null
}

export type SaveClient = {
  code?: string | null
  name: string
  legalName?: string | null
  countryCode?: string | null
  entityType?: string | null
  taxIds?: Record<string, string>
  email?: string | null
  phone?: string | null
  isActive?: boolean
  version?: number | null
  groupId?: string | null
  assignedUserId?: string | null
  custom?: Record<string, string>
}

export type ClientPage = { items: Client[]; total: number }
export type TaxIdType = { countryCode: string; code: string; label: string; example?: string | null; isUnique: boolean }
export type CustomField = {
  id: string; key: string; label: string; fieldType: 'text' | 'number' | 'date' | 'dropdown' | 'bool'
  options?: string[] | null; isRequired: boolean; validationRegex?: string | null; sortOrder: number; isActive: boolean
}
export type ClientFormInfo = {
  firmCountry: string; entityTypes: string[]; requiredFields: string[]; codeFormat: string; nextCodeExample: string
  taxIdTypes: TaxIdType[]; customFields: CustomField[]
}
export type Contact = { id: string; name: string; role?: string | null; email?: string | null; phone?: string | null; isPrimary: boolean; receivesReminders: boolean }
export type Note = { id: string; body: string; createdBy: string; createdByName?: string | null; createdAt: string }
export type Group = { id: string; name: string; clientCount: number }
export type ImportRow = { row: number; name?: string | null; ok: boolean; errors: string[]; warnings: string[] }
export type ImportPreview = { totalRows: number; validRows: number; errorRows: number; ignoredColumns: string[]; newGroups: string[]; rows: ImportRow[] }
export type ImportResult = { created: number; skipped: number; preview: ImportPreview }

/** Downloads a file from the API (with the access token) and saves it in the browser. */
export async function download(path: string, fileName: string) {
  let res = await raw(path, {}, accessToken)
  if (res.status === 401 && await refreshSession()) res = await raw(path, {}, accessToken)
  if (!res.ok) {
    const body = await res.json().catch(() => null)
    throw new ApiError(res.status, body?.title ?? 'Download failed')
  }
  saveBlob(await res.blob(), fileName)
}

export function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = fileName
  a.click()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

/** Field errors as one list of sentences. */
export function errorList(e: unknown, fallback = 'Could not save.'): string[] {
  if (e instanceof ApiError) return e.errors ? Object.values(e.errors).flat() : [e.message]
  return [fallback]
}

export type RoleRef = { id: string; name: string }
export type UserListItem = {
  id: string; name: string; email: string; isActive: boolean
  lastLoginAt?: string | null; roles: RoleRef[]; mfaEnabled: boolean
}
export type RoleItem = { id: string; name: string; isSystem: boolean; permissions: string[]; userCount: number }
export type InvitationItem = { id: string; email: string; name?: string | null; roleName: string; expiresAt: string }
export type InviteCreated = { id: string; email: string; expiresAt: string; inviteToken: string }

// ---------------------------------------------------------------- compliance

export type Deadline = {
  id: string; clientId: string; clientCode: string; clientName: string; typeCode: string; typeName: string
  periodKey: string; periodLabel: string; periodEnd: string; statutoryDue: string; dueDate: string; internalDue: string
  status: 'pending' | 'in_progress' | 'filed' | 'not_applicable'; isOverdue: boolean
  assignedUserId?: string | null; assignedUserName?: string | null; filedOn?: string | null; ackNo?: string | null
  remarks?: string | null; extended: boolean; version: number
}
export type DeadlineSummary = { overdue: number; dueThisWeek: number; open: number; filedThisMonth: number }
export type DeadlinePage = { items: Deadline[]; total: number; summary: DeadlineSummary }
export type ComplianceType = {
  code: string; countryCode: string; name: string; authority: string; description?: string | null
  frequencies: string[]; defaultFrequency: string; shiftOnHoliday: boolean; autoApply: boolean; appliesTo: string
}
export type ClientCompliance = {
  code: string; name: string; authority: string; description?: string | null; enabled: boolean; byRule: boolean; chosen: boolean
  frequency: string; frequencies: string[]; quarterEndMonth?: number | null; fyStartMonth?: number | null; appliesTo: string
}
export type HolidayItem = { id: string; date: string; name: string; firmOnly: boolean }

/** "2026-11-20" → "20 Nov 2026" without time-zone shifts. */
export function fmtDate(iso?: string | null) {
  if (!iso) return ''
  const [y, m, d] = iso.split('-').map(Number)
  return new Date(Date.UTC(y, m - 1, d)).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' })
}

/** Today's date as yyyy-mm-dd in the browser's time zone. */
export function todayIso() {
  const t = new Date()
  return `${t.getFullYear()}-${String(t.getMonth() + 1).padStart(2, '0')}-${String(t.getDate()).padStart(2, '0')}`
}

export function addDaysIso(iso: string, days: number) {
  const [y, m, d] = iso.split('-').map(Number)
  const t = new Date(Date.UTC(y, m - 1, d + days))
  return t.toISOString().slice(0, 10)
}

// ---------------------------------------------------------------- tasks

export type TaskStatus = 'open' | 'in_progress' | 'in_review' | 'changes_requested' | 'done' | 'cancelled'
export type TaskItem = {
  id: string; title: string; clientId?: string | null; clientCode?: string | null; clientName?: string | null; deadlineId?: string | null
  status: TaskStatus; priority: 'low' | 'normal' | 'high'; dueDate?: string | null; isOverdue: boolean
  assignedUserId?: string | null; assignedUserName?: string | null; reviewerUserId?: string | null; reviewerName?: string | null
  reviewRequired: boolean; checklistDone: number; checklistTotal: number; version: number
}
export type TaskPage = { items: TaskItem[]; total: number }
export type TaskDetail = {
  task: TaskItem; description?: string | null; submittedBy?: string | null; submittedByName?: string | null
  filedOn?: string | null; ackNo?: string | null; completedAt?: string | null; completedByName?: string | null
  deadline?: { id: string; typeCode: string; periodKey: string; periodLabel: string; dueDate: string; statutoryDue: string; status: string } | null
  checklist: { id: string; position: number; text: string; isDone: boolean; doneByName?: string | null; doneAt?: string | null }[]
  comments: { id: string; kind: 'comment' | 'status' | 'review'; body: string; createdBy?: string | null; createdByName?: string | null; createdAt: string }[]
}
export type WorkloadRow = { userId?: string | null; name: string; open: number; overdue: number; dueThisWeek: number; waitingReview: number; toReview: number }
export type TaskTemplate = { id: string; typeCode?: string | null; name: string; items: string[]; firmOwn: boolean }

export const TASK_STATUS: Record<TaskStatus, string> = {
  open: 'To do', in_progress: 'In progress', in_review: 'In review', changes_requested: 'Sent back', done: 'Done', cancelled: 'Cancelled',
}

// ---------------------------------------------------------------- reminders

export type NotificationItem = {
  id: string; recipientType: string; recipientName?: string | null; clientId?: string | null; clientName?: string | null
  channel: 'email' | 'whatsapp' | 'sms'; toAddress: string; templateKey: string; subject?: string | null; body: string
  relatedType?: string | null; relatedId?: string | null
  status: 'queued' | 'sending' | 'sent' | 'failed' | 'skipped' | 'cancelled'; scheduledAt: string; attempts: number
  lastError?: string | null; sentAt?: string | null; createdAt: string
}
export type NotificationPage = { items: NotificationItem[]; nextBefore?: string | null }
export type MessageTemplate = {
  id: string; key: string; channel: 'email' | 'whatsapp'; subject?: string | null; body: string; waTemplate?: string | null
  waParams?: string[] | null; firmOwn: boolean; placeholders: string[]
}
export const MESSAGE_NAMES: Record<string, string> = {
  'client.deadline_reminder': 'Client: deadline reminder',
  'client.deadline_overdue': 'Client: deadline overdue',
  'staff.daily_digest': 'Staff: daily task summary',
  'staff.task_sent_back': 'Staff: work sent back',
  'staff.review_requested': 'Staff: please review',
  'staff.escalation': 'Partner: overdue escalation',
  test: 'Test email',
}

// ---------------------------------------------------------------- documents

export type ScanStatus = 'pending' | 'clean' | 'infected' | 'not_scanned'
export type DocumentItem = {
  id: string; clientId: string; folder?: string | null; typeCode?: string | null; folderName: string; fy?: string | null
  periodKey?: string | null; name: string; fileName: string; extension: string; sizeBytes: number; scanStatus: ScanStatus
  scanDetail?: string | null; source: 'staff' | 'portal'; uploadedByName?: string | null; uploadedAt: string; notes?: string | null
  legalHold: boolean; deletedAt?: string | null; purgedAt?: string | null; purgeReason?: string | null; canDownload: boolean
  taskCount: number; version: number
}
export type DocumentPage = { items: DocumentItem[]; total: number }
export type FolderNode = { folder?: string | null; typeCode?: string | null; name: string; count: number }
export type YearNode = { fy?: string | null; label: string; count: number; folders: FolderNode[] }
export type FolderChoice = { folder?: string | null; typeCode?: string | null; name: string; perYear: boolean }
export type VaultInfo = {
  years: YearNode[]; trashCount: number; folders: FolderChoice[]; yearChoices: string[]; currentFy: string
  allowedTypes: string[]; maxSizeMb: number; scanEnabled: boolean; clientUploadEnabled: boolean
}
export type UploadLink = {
  id: string; clientId: string; message?: string | null; folderName: string; fy?: string | null; taskId?: string | null
  taskTitle?: string | null; maxFiles: number; uploadedCount: number; expiresAt: string; revokedAt?: string | null
  lastUsedAt?: string | null; createdAt: string; createdByName?: string | null; status: 'active' | 'expired' | 'revoked' | 'full'
}
export type UploadLinkCreated = { link: UploadLink; token: string }
export type PortalInfo = {
  firmName: string; clientName: string; message?: string | null; folderName: string; fy?: string | null
  allowedTypes: string[]; maxSizeMb: number; expiresAt: string; remaining: number
  received: { name: string; sizeBytes: number; uploadedAt: string; status: string }[]
}
export type RetentionItem = {
  id: string; clientId: string; clientCode: string; clientName: string; name: string; folderName: string; fy?: string | null
  keepUntil: string; sizeBytes: number; clientDeleted: boolean
}
export type RetentionPage = { retentionYears: number; items: RetentionItem[]; total: number }

export const SCAN_STATUS: Record<ScanStatus, string> = {
  pending: 'waiting for virus scan', clean: 'scanned', infected: 'virus found — removed', not_scanned: 'not scanned',
}

/** 1536 → "1.5 KB". */
export function fmtSize(bytes: number) {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1).replace(/\.0$/, '')} KB`
  return `${(bytes / 1024 / 1024).toFixed(1).replace(/\.0$/, '')} MB`
}

/** Sends one file (and form fields) as multipart/form-data. */
export function uploadFile<T>(path: string, file: File, fields: Record<string, string | null | undefined> = {}) {
  const body = new FormData()
  body.append('file', file)
  for (const [k, v] of Object.entries(fields)) if (v) body.append(k, v)
  return api<T>(path, { method: 'POST', body })
}

/** The link a client opens to send files (the token is shown only once, when the link is made). */
export function uploadLinkUrl(token: string) {
  return `${location.origin}/upload/${encodeURIComponent(token)}`
}
