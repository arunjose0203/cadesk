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

// ---------------------------------------------------------------- Sprint 11: Billing & Timesheets

export type TimeEntry = {
  id: string
  clientId: string
  clientName?: string | null
  taskId?: string | null
  taskTitle?: string | null
  userId: string
  userName?: string | null
  date: string
  hours: number
  hourlyRate: number
  description: string
  billable: boolean
  isBilled: boolean
  invoiceItemId?: string | null
  createdAt: string
}

export type TimesheetSummary = {
  totalHours: number
  billableHours: number
  nonBillableHours: number
  unbilledHours: number
  estimatedUnbilledValue: number
}

export type InvoiceItem = {
  id: string
  description: string
  sacCode?: string | null
  quantity: number
  unitPrice: number
  gstRate: number
  amount: number
  taxAmount: number
  totalAmount: number
  timeEntryId?: string | null
}

export type Payment = {
  id: string
  invoiceId: string
  invoiceNumber?: string | null
  clientId: string
  clientName?: string | null
  paymentDate: string
  amount: number
  paymentMode: string
  referenceNumber?: string | null
  notes?: string | null
  createdAt: string
}

export type Invoice = {
  id: string
  clientId: string
  clientName?: string | null
  invoiceNumber: string
  issueDate: string
  dueDate: string
  status: 'draft' | 'sent' | 'partially_paid' | 'paid' | 'cancelled'
  currency: string
  subtotal: number
  taxAmount: number
  totalAmount: number
  paidAmount: number
  balanceDue: number
  notes?: string | null
  terms?: string | null
  items: InvoiceItem[]
  payments: Payment[]
  createdAt: string
}

export type BillingSummary = {
  totalOutstanding: number
  totalOverdue: number
  billedThisMonth: number
  collectedThisMonth: number
  openInvoicesCount: number
}

export type CreateInvoiceRequest = {
  clientId: string
  issueDate: string
  dueDate: string
  currency?: string | null
  notes?: string | null
  terms?: string | null
  items: {
    description: string
    sacCode?: string | null
    quantity: number
    unitPrice: number
    gstRate?: number | null
    timeEntryId?: string | null
  }[]
  timeEntryIds?: string[] | null
}

export type RecordPaymentRequest = {
  paymentDate: string
  amount: number
  paymentMode: string
  referenceNumber?: string | null
  notes?: string | null
}


export type DscMovementLog = {
  id: string
  movedAt: string
  fromLocation: string
  toLocation: string
  handlerUserId?: string | null
  handlerUserName?: string | null
  remarks?: string | null
}

export type DscRegister = {
  id: string
  clientId?: string | null
  clientName?: string | null
  holderName: string
  pan: string
  dscType: string
  issuingAuthority: string
  validFrom: string
  validTo: string
  daysRemaining: number
  serialNumber?: string | null
  usbTokenId?: string | null
  location: string
  custodianUserId?: string | null
  custodianUserName?: string | null
  status: string
  notes?: string | null
  movementLogs: DscMovementLog[]
  createdAt: string
}

export type DscSummary = {
  totalActive: number
  expiringSoon: number
  expired: number
  inVault: number
  checkedOut: number
}

export type CreateDscRequest = {
  clientId?: string | null
  holderName: string
  pan: string
  dscType?: string | null
  issuingAuthority?: string | null
  validFrom: string
  validTo: string
  serialNumber?: string | null
  usbTokenId?: string | null
  location?: string | null
  custodianUserId?: string | null
  notes?: string | null
}

export type TransferCustodyRequest = {
  toLocation: string
  custodianUserId?: string | null
  remarks?: string | null
}

export type PortalUser = {
  id: string
  clientId: string
  clientName?: string | null
  email: string
  name: string
  phone?: string | null
  role: string
  isActive: boolean
  lastLoginAt?: string | null
  createdAt: string
}

export type ServiceRequestMessage = {
  id: string
  senderType: 'client' | 'staff' | string
  senderName: string
  senderUserId?: string | null
  message: string
  attachmentDocId?: string | null
  createdAt: string
}

export type ServiceRequest = {
  id: string
  clientId: string
  clientName?: string | null
  portalUserId?: string | null
  portalUserName?: string | null
  requestNumber: string
  category: string
  subject: string
  description: string
  priority: 'low' | 'medium' | 'high' | 'urgent' | string
  status: 'submitted' | 'in_progress' | 'resolved' | 'closed' | string
  assignedUserId?: string | null
  assignedUserName?: string | null
  resolutionNotes?: string | null
  resolvedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  messages: ServiceRequestMessage[]
}

export type CreatePortalUserRequest = {
  clientId: string
  email: string
  name: string
  phone?: string | null
  password: string
  role?: string | null
}

export type CreateServiceRequestInput = {
  category: string
  subject: string
  description: string
  priority?: string | null
}

export type AddRequestMessageInput = {
  message: string
  attachmentDocId?: string | null
}

export type UpdateServiceRequestStatusInput = {
  status: string
  resolutionNotes?: string | null
  assignedUserId?: string | null
}

// -------------------------------------------------------------
// Sprint 14: Direct Tax Computation & Regime Comparison Types
// -------------------------------------------------------------

export type TaxSlabBreakdown = {
  slabRange: string
  ratePercent: number
  taxableInSlab: number
  taxAmount: number
}

export type RegimeTaxResult = {
  regime: string
  grossSalary: number
  standardDeduction: number
  netSalary: number
  housePropertyIncome: number
  businessIncome: number
  capitalGainsStcg: number
  capitalGainsLtcg: number
  otherSourcesIncome: number
  grossTotalIncome: number
  chapterViaDeductions: number
  taxableIncome: number
  baseTax: number
  specialRateTax: number
  rebate87A: number
  surcharge: number
  cess: number
  totalTaxLiability: number
  prepaidTaxes: number
  netPayableOrRefund: number
  slabBreakdown: TaxSlabBreakdown[]
}

export type RegimeComparisonDto = {
  oldRegime: RegimeTaxResult
  newRegime: RegimeTaxResult
  recommendedRegime: string
  taxSavings: number
  recommendationReason: string
}

export type CalculateTaxRequest = {
  clientId: string
  assessmentYear: string
  financialYear: string
  taxpayerCategory?: string | null
  optedRegime?: string | null
  // Salary
  grossSalary: number
  hraReceived: number
  rentPaid: number
  isMetro: boolean
  professionalTax: number
  // House property
  annualRentReceived: number
  municipalTaxesPaid: number
  housingLoanInterest: number
  isSelfOccupied: boolean
  // Business
  businessGrossTurnover: number
  netProfit: number
  presumptiveSection?: string | null
  // Capital gains
  stcg111A: number
  stcgNormal: number
  ltcg112A: number
  ltcg112: number
  // Other sources
  savingsInterest: number
  fdInterest: number
  dividendIncome: number
  otherIncome: number
  // Deductions
  section80C: number
  section80Ccd1B: number
  section80D: number
  section80E: number
  section80G: number
  section80Tta: number
  // Taxes paid
  tdsPaid: number
  tcsPaid: number
  advanceTaxPaid: number
  selfAssessmentTaxPaid: number
  notes?: string | null
}

export type TaxComputationDto = {
  id: string
  clientId: string
  clientName?: string | null
  assessmentYear: string
  financialYear: string
  taxpayerCategory: string
  optedRegime: string
  status: string
  grossSalary: number
  salaryDeductions: number
  netSalary: number
  housePropertyIncome: number
  businessIncome: number
  capitalGainsStcg: number
  capitalGainsLtcg: number
  otherSourcesIncome: number
  grossTotalIncome: number
  chapterViaDeductions: number
  totalTaxableIncome: number
  taxOnIncome: number
  rebate87A: number
  surcharge: number
  cess: number
  totalTaxLiability: number
  tdsTcsCredit: number
  advanceTaxPaid: number
  selfAssessmentTax: number
  netTaxPayableOrRefund: number
  comparison: RegimeComparisonDto
  notes?: string | null
  createdAt: string
  updatedAt?: string | null
}

export const taxApi = {
  preview: (req: CalculateTaxRequest) => api<RegimeComparisonDto>('/api/tax/preview', { method: 'POST', body: JSON.stringify(req) }),
  save: (req: CalculateTaxRequest) => api<TaxComputationDto>('/api/tax/computations', { method: 'POST', body: JSON.stringify(req) }),
  list: (clientId?: string, ay?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (ay) p.set('assessmentYear', ay)
    const qs = p.toString()
    return api<TaxComputationDto[]>(`/api/tax/computations${qs ? `?${qs}` : ''}`)
  },
  get: (id: string) => api<TaxComputationDto>(`/api/tax/computations/${id}`),
  updateStatus: (id: string, status: string) => api<TaxComputationDto>(`/api/tax/computations/${id}/status?status=${encodeURIComponent(status)}`, { method: 'PUT' }),
  delete: (id: string) => api<void>(`/api/tax/computations/${id}`, { method: 'DELETE' }),
}

// -------------------------------------------------------------
// Sprint 15: Statutory Audit & Working Papers Types (ICAI SA)
// -------------------------------------------------------------

export type AuditEngagementDto = {
  id: string
  clientId: string
  clientName?: string | null
  financialYear: string
  auditType: string
  title: string
  leadPartnerId?: string | null
  leadPartnerName?: string | null
  managerId?: string | null
  managerName?: string | null
  plannedStartDate?: string | null
  plannedEndDate?: string | null
  status: string
  benchmarkType: string
  benchmarkAmount: number
  overallMateriality: number
  performanceMateriality: number
  trivialThreshold: number
  auditOpinion?: string | null
  notes?: string | null
  auditReportDate?: string | null
  archivalDeadline?: string | null
  isArchived: boolean
  archivedAt?: string | null
  archivedBy?: string | null
  archivedByName?: string | null
  eqrRequired: boolean
  eqrPartnerId?: string | null
  eqrPartnerName?: string | null
  eqrStatus: string
  archivalStatus: string
  openReviewNotesCount: number
  canSignOff: boolean
  signOffValidationMessage: string
  totalWorkingPapers: number
  reviewedWorkingPapers: number
  createdAt: string
  updatedAt?: string | null
}

export type AuditWorkingPaperDto = {
  id: string
  engagementId: string
  refCode: string
  area: string
  title: string
  assertion: string
  objective: string
  proceduresPerformed: string
  samplingMethod: string
  populationValue: number
  sampleValue: number
  itemsTestedCount: number
  exceptionsNoted?: string | null
  conclusion: string
  status: string
  preparerId?: string | null
  preparerName?: string | null
  preparedAt?: string | null
  reviewerId?: string | null
  reviewerName?: string | null
  reviewedAt?: string | null
  reviewNotes?: string | null
  attachments: string[]
  reviewNotesCount: number
  openReviewNotesCount: number
  createdAt: string
  updatedAt?: string | null
}

export type PreIssuanceChecklistItemDto = {
  code: string
  description: string
  required: boolean
  isVerified: boolean
  comments?: string | null
  verifiedAt?: string | null
}

export type AuditEqrReviewDto = {
  id: string
  engagementId: string
  reviewType: string
  eqrPartnerId: string
  eqrPartnerName?: string | null
  reviewStatus: string
  evaluationMateriality?: string | null
  evaluationSignificantRisks?: string | null
  evaluationGoingConcern?: string | null
  evaluationAuditOpinion?: string | null
  preIssuanceChecklist: PreIssuanceChecklistItemDto[]
  eqrSignoffDate?: string | null
  eqrConclusionRemarks?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type AuditReviewNoteDto = {
  id: string
  engagementId: string
  workingPaperId?: string | null
  workingPaperRef?: string | null
  workingPaperTitle?: string | null
  raisedByUserId: string
  raisedByName?: string | null
  raisedByRole: string
  category: string
  priority: string
  query: string
  status: string
  response?: string | null
  responseByUserId?: string | null
  responseByName?: string | null
  responseAt?: string | null
  clearedByUserId?: string | null
  clearedByName?: string | null
  clearedAt?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type CreateAuditEngagementRequest = {
  clientId: string
  financialYear: string
  auditType?: string | null
  title: string
  leadPartnerId?: string | null
  managerId?: string | null
  plannedStartDate?: string | null
  plannedEndDate?: string | null
  benchmarkType?: string | null
  benchmarkAmount: number
  eqrRequired: boolean
  eqrPartnerId?: string | null
  notes?: string | null
}

export type SignOffAuditReportRequest = {
  auditReportDate: string
  auditOpinion: string
  notes?: string | null
}

export type ArchiveEngagementRequest = {
  notes?: string | null
}

export type CreateEqrReviewRequest = {
  reviewType: string
  eqrPartnerId: string
}

export type UpdateEqrReviewRequest = {
  evaluationMateriality?: string | null
  evaluationSignificantRisks?: string | null
  evaluationGoingConcern?: string | null
  evaluationAuditOpinion?: string | null
  preIssuanceChecklist?: PreIssuanceChecklistItemDto[] | null
  eqrConclusionRemarks?: string | null
}

export type ClearEqrReviewRequest = {
  eqrConclusionRemarks: string
  preIssuanceChecklist: PreIssuanceChecklistItemDto[]
}

export type CreateReviewNoteRequest = {
  workingPaperId?: string | null
  raisedByRole: string
  category: string
  priority: string
  query: string
}

export type RespondReviewNoteRequest = {
  response: string
}

export type ReopenReviewNoteRequest = {
  remarks: string
}

export type UpdateMaterialityRequest = {
  benchmarkType: string
  benchmarkAmount: number
  overallMaterialityPercent?: number | null
  performanceMaterialityPercent?: number | null
  trivialThresholdPercent?: number | null
}

export type SaveWorkingPaperRequest = {
  refCode: string
  area: string
  title: string
  assertion?: string | null
  objective: string
  proceduresPerformed?: string | null
  samplingMethod?: string | null
  populationValue: number
  sampleValue: number
  itemsTestedCount: number
  exceptionsNoted?: string | null
  conclusion?: string | null
}

export type ReviewWorkingPaperRequest = {
  status: string
  reviewNotes?: string | null
}

export type UpdateEngagementStatusRequest = {
  status: string
  auditOpinion?: string | null
}

export type AuditSummaryDto = {
  totalEngagements: number
  planningCount: number
  fieldworkCount: number
  reviewCount: number
  signedOffCount: number
  archivedCount: number
  archivalDueSoonCount: number
  archivalOverdueCount: number
  eqrPendingCount: number
  totalWorkingPapers: number
  openReviewNotesCount: number
  exceptionsFlaggedCount: number
}

export const auditApi = {
  summary: () => api<AuditSummaryDto>('/api/audit/summary'),
  listEngagements: (clientId?: string, fy?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('financialYear', fy)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<AuditEngagementDto[]>(`/api/audit/engagements${qs ? `?${qs}` : ''}`)
  },
  getEngagement: (id: string) => api<AuditEngagementDto>(`/api/audit/engagements/${id}`),
  createEngagement: (req: CreateAuditEngagementRequest) => api<AuditEngagementDto>('/api/audit/engagements', { method: 'POST', body: JSON.stringify(req) }),
  updateMateriality: (id: string, req: UpdateMaterialityRequest) => api<AuditEngagementDto>(`/api/audit/engagements/${id}/materiality`, { method: 'PUT', body: JSON.stringify(req) }),
  updateStatus: (id: string, req: UpdateEngagementStatusRequest) => api<AuditEngagementDto>(`/api/audit/engagements/${id}/status`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteEngagement: (id: string) => api<void>(`/api/audit/engagements/${id}`, { method: 'DELETE' }),

  signOffAudit: (id: string, req: SignOffAuditReportRequest) => api<AuditEngagementDto>(`/api/audit/engagements/${id}/signoff`, { method: 'POST', body: JSON.stringify(req) }),
  archiveEngagement: (id: string, req: ArchiveEngagementRequest) => api<AuditEngagementDto>(`/api/audit/engagements/${id}/archive`, { method: 'POST', body: JSON.stringify(req) }),

  getEqrReview: (engagementId: string) => api<AuditEqrReviewDto>(`/api/audit/engagements/${engagementId}/eqr`),
  assignEqr: (engagementId: string, req: CreateEqrReviewRequest) => api<AuditEqrReviewDto>(`/api/audit/engagements/${engagementId}/eqr/assign`, { method: 'POST', body: JSON.stringify(req) }),
  updateEqr: (id: string, req: UpdateEqrReviewRequest) => api<AuditEqrReviewDto>(`/api/audit/eqr/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  clearEqr: (id: string, req: ClearEqrReviewRequest) => api<AuditEqrReviewDto>(`/api/audit/eqr/${id}/clear`, { method: 'POST', body: JSON.stringify(req) }),

  listReviewNotes: (engagementId: string, workingPaperId?: string, status?: string, priority?: string) => {
    const p = new URLSearchParams()
    if (workingPaperId) p.set('workingPaperId', workingPaperId)
    if (status) p.set('status', status)
    if (priority) p.set('priority', priority)
    const qs = p.toString()
    return api<AuditReviewNoteDto[]>(`/api/audit/engagements/${engagementId}/review-notes${qs ? `?${qs}` : ''}`)
  },
  createReviewNote: (engagementId: string, req: CreateReviewNoteRequest) => api<AuditReviewNoteDto>(`/api/audit/engagements/${engagementId}/review-notes`, { method: 'POST', body: JSON.stringify(req) }),
  respondReviewNote: (id: string, req: RespondReviewNoteRequest) => api<AuditReviewNoteDto>(`/api/audit/review-notes/${id}/respond`, { method: 'POST', body: JSON.stringify(req) }),
  clearReviewNote: (id: string) => api<AuditReviewNoteDto>(`/api/audit/review-notes/${id}/clear`, { method: 'POST', body: JSON.stringify({}) }),
  reopenReviewNote: (id: string, req: ReopenReviewNoteRequest) => api<AuditReviewNoteDto>(`/api/audit/review-notes/${id}/reopen`, { method: 'POST', body: JSON.stringify(req) }),
  getChecklistTemplate: () => api<PreIssuanceChecklistItemDto[]>('/api/audit/checklist-template'),

  listWorkingPapers: (engagementId: string, area?: string, status?: string) => {
    const p = new URLSearchParams()
    if (area) p.set('area', area)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<AuditWorkingPaperDto[]>(`/api/audit/engagements/${engagementId}/working-papers${qs ? `?${qs}` : ''}`)
  },
  getWorkingPaper: (id: string) => api<AuditWorkingPaperDto>(`/api/audit/working-papers/${id}`),
  createWorkingPaper: (engagementId: string, req: SaveWorkingPaperRequest) => api<AuditWorkingPaperDto>(`/api/audit/engagements/${engagementId}/working-papers`, { method: 'POST', body: JSON.stringify(req) }),
  updateWorkingPaper: (id: string, req: SaveWorkingPaperRequest) => api<AuditWorkingPaperDto>(`/api/audit/working-papers/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  reviewWorkingPaper: (id: string, req: ReviewWorkingPaperRequest) => api<AuditWorkingPaperDto>(`/api/audit/working-papers/${id}/review`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteWorkingPaper: (id: string) => api<void>(`/api/audit/working-papers/${id}`, { method: 'DELETE' }),
}

// -------------------------------------------------------------
// Sprint 16: GST 2A/2B Reconciliation Types
// -------------------------------------------------------------

export type PurchaseRegisterInvoice = {
  supplierGstin: string
  supplierName?: string | null
  invoiceNumber: string
  invoiceDate?: string | null
  invoiceType?: string | null
  taxableValue: number
  cgst: number
  sgst: number
  igst: number
  cess: number
}

export type Gstr2BInvoice = {
  supplierGstin: string
  supplierName?: string | null
  invoiceNumber: string
  invoiceDate?: string | null
  invoiceType?: string | null
  taxableValue: number
  cgst: number
  sgst: number
  igst: number
  cess: number
  itcAvailable?: boolean | null
  ineligibleReason?: string | null
}

export type RunReconciliationRequest = {
  clientId: string
  returnPeriod: string
  financialYear: string
  toleranceAmount?: number | null
  purchaseInvoices: PurchaseRegisterInvoice[]
  gstr2BInvoices: Gstr2BInvoice[]
  notes?: string | null
}

export type GstReconciliationBatchDto = {
  id: string
  clientId: string
  clientName?: string | null
  returnPeriod: string
  financialYear: string
  status: string
  toleranceAmount: number
  purchaseRegisterCount: number
  gstr2BCount: number
  matchedCount: number
  matchedWithToleranceCount: number
  missingIn2BCount: number
  missingInBooksCount: number
  mismatchedTaxCount: number
  totalPrTaxable: number
  totalPrTax: number
  total2BTaxable: number
  total2BTax: number
  totalItcEligible: number
  totalItcIneligible: number
  notes?: string | null
  createdAt: string
}

export type GstReconciliationItemDto = {
  id: string
  batchId: string
  supplierGstin: string
  supplierName?: string | null
  invoiceNumber: string
  normalizedInvoiceNumber: string
  invoiceDate?: string | null
  invoiceType: string
  prTaxableValue: number
  prCgst: number
  prSgst: number
  prIgst: number
  prCess: number
  gstr2BTaxableValue: number
  gstr2BCgst: number
  gstr2BSgst: number
  gstr2BIgst: number
  gstr2BCess: number
  matchStatus: string
  itcEligibility: string
  taxDifference: number
  notes?: string | null
  createdAt: string
}

export type GstReconciliationSummaryDto = {
  totalBatches: number
  totalInvoicesReconciled: number
  totalMatchedCount: number
  totalMissingIn2BCount: number
  totalMissingInBooksCount: number
  totalItcEligible: number
  totalItcAtRisk: number
}

export const gstApi = {
  summary: () => api<GstReconciliationSummaryDto>('/api/gst/summary'),
  listBatches: (clientId?: string, fy?: string, rp?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('financialYear', fy)
    if (rp) p.set('returnPeriod', rp)
    const qs = p.toString()
    return api<GstReconciliationBatchDto[]>(`/api/gst/batches${qs ? `?${qs}` : ''}`)
  },
  getBatch: (id: string) => api<GstReconciliationBatchDto>(`/api/gst/batches/${id}`),
  reconcile: (req: RunReconciliationRequest) => api<GstReconciliationBatchDto>('/api/gst/reconcile', { method: 'POST', body: JSON.stringify(req) }),
  listItems: (batchId: string, matchStatus?: string) => {
    const p = new URLSearchParams()
    if (matchStatus) p.set('matchStatus', matchStatus)
    const qs = p.toString()
    return api<GstReconciliationItemDto[]>(`/api/gst/batches/${batchId}/items${qs ? `?${qs}` : ''}`)
  },
  deleteBatch: (id: string) => api<void>(`/api/gst/batches/${id}`, { method: 'DELETE' }),
}

// -------------------------------------------------------------
// Sprint 17: TDS Management & Form 26Q/27Q Types
// -------------------------------------------------------------

export type TdsLowerDeductionCertificateDto = {
  id: string
  clientId: string
  clientName?: string | null
  deducteePan: string
  deducteeName: string
  section: string
  certificateNumber: string
  financialYear: string
  validFrom: string
  validTo: string
  specifiedRate: number
  thresholdAmount: number
  utilizedAmount: number
  remainingAmount: number
  createdAt: string
}

export type CreateLdcRequest = {
  clientId: string
  deducteePan: string
  deducteeName: string
  section: string
  certificateNumber: string
  financialYear: string
  validFrom: string
  validTo: string
  specifiedRate: number
  thresholdAmount: number
}

export type TdsChallanDto = {
  id: string
  clientId: string
  clientName?: string | null
  quarter: string
  financialYear: string
  bsrCode: string
  challanDate: string
  challanSerialNumber: string
  sectionCode: string
  minorHead: string
  tdsAmount: number
  surcharge: number
  cess: number
  interest: number
  lateFee234E: number
  totalChallanAmount: number
  allocatedAmount: number
  unallocatedBalance: number
  deductionsCount: number
  notes?: string | null
  createdAt: string
}

export type CreateTdsChallanRequest = {
  clientId: string
  quarter: string
  financialYear: string
  bsrCode: string
  challanDate: string
  challanSerialNumber: string
  sectionCode: string
  minorHead?: string | null
  tdsAmount: number
  surcharge: number
  cess: number
  interest: number
  lateFee234E: number
  notes?: string | null
}

export type TdsDeductionDto = {
  id: string
  clientId: string
  clientName?: string | null
  challanId?: string | null
  challanBsr?: string | null
  challanDate?: string | null
  challanSerial?: string | null
  formType: string
  quarter: string
  financialYear: string
  deducteePan: string
  deducteeName: string
  section: string
  paymentDate: string
  amountPaid: number
  tdsRate: number
  tdsAmount: number
  surcharge: number
  cess: number
  totalTaxDeducted: number
  deductionDate: string
  certificateId?: string | null
  certificateNumber?: string | null
  higherRateReason: string
  bookingStatus: string
  createdAt: string
}

export type RecordTdsDeductionRequest = {
  clientId: string
  formType?: string | null
  quarter: string
  financialYear: string
  deducteePan: string
  deducteeName: string
  section: string
  paymentDate: string
  amountPaid: number
  deductionDate?: string | null
  certificateId?: string | null
  forceHigherRate206Ab?: boolean | null
}

export type MapDeductionsToChallanRequest = {
  challanId: string
  deductionIds: string[]
}

export type CalculateTdsInterestRequest = {
  deductionDate: string
  paymentDate?: string | null
  depositDate: string
  tdsAmount: number
}

export type TdsInterestResultDto = {
  delayDeductionMonths: number
  delayDeductionInterest: number
  delayDepositMonths: number
  delayDepositInterest: number
  totalInterest: number
}

export type TdsSummaryDto = {
  totalDeductionsCount: number
  totalTaxDeducted: number
  totalDeposited: number
  totalPendingDeposit: number
  activeLdcCount: number
  totalChallansCount: number
}

export const tdsApi = {
  summary: () => api<TdsSummaryDto>('/api/tds/summary'),
  listLdcs: (clientId?: string, fy?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('financialYear', fy)
    const qs = p.toString()
    return api<TdsLowerDeductionCertificateDto[]>(`/api/tds/ldc${qs ? `?${qs}` : ''}`)
  },
  createLdc: (req: CreateLdcRequest) => api<TdsLowerDeductionCertificateDto>('/api/tds/ldc', { method: 'POST', body: JSON.stringify(req) }),

  listChallans: (clientId?: string, fy?: string, q?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('financialYear', fy)
    if (q) p.set('quarter', q)
    const qs = p.toString()
    return api<TdsChallanDto[]>(`/api/tds/challans${qs ? `?${qs}` : ''}`)
  },
  createChallan: (req: CreateTdsChallanRequest) => api<TdsChallanDto>('/api/tds/challans', { method: 'POST', body: JSON.stringify(req) }),

  listDeductions: (clientId?: string, fy?: string, q?: string, formType?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('financialYear', fy)
    if (q) p.set('quarter', q)
    if (formType) p.set('formType', formType)
    if (status) p.set('bookingStatus', status)
    const qs = p.toString()
    return api<TdsDeductionDto[]>(`/api/tds/deductions${qs ? `?${qs}` : ''}`)
  },
  recordDeduction: (req: RecordTdsDeductionRequest) => api<TdsDeductionDto>('/api/tds/deductions', { method: 'POST', body: JSON.stringify(req) }),

  mapToChallan: (req: MapDeductionsToChallanRequest) => api<TdsChallanDto>('/api/tds/challans/map', { method: 'POST', body: JSON.stringify(req) }),

  calculateInterest: (req: CalculateTdsInterestRequest) => api<TdsInterestResultDto>('/api/tds/interest/calculate', { method: 'POST', body: JSON.stringify(req) }),
}

// -------------------------------------------------------------
// Sprint 18: ROC / MCA Company Law & Secretarial Compliance Types
// -------------------------------------------------------------

export type CompanyMasterDto = {
  id: string
  clientId: string
  clientName?: string | null
  cin: string
  companyName: string
  companyClass: string
  authorizedCapital: number
  paidUpCapital: number
  registeredOfficeAddress: string
  rocJurisdiction: string
  incorporationDate: string
  companyStatus: string
  lastAgmDate?: string | null
  financialYearEndMonth: number
  directorsCount: number
  pendingFilingsCount: number
  createdAt: string
}

export type CreateCompanyRequest = {
  clientId: string
  cin: string
  companyName: string
  companyClass?: string
  authorizedCapital: number
  paidUpCapital: number
  registeredOfficeAddress: string
  rocJurisdiction: string
  incorporationDate: string
  companyStatus?: string
  lastAgmDate?: string | null
  financialYearEndMonth?: number
}

export type DirectorDinDto = {
  id: string
  companyId: string
  companyName?: string | null
  din: string
  directorName: string
  pan?: string | null
  designation: string
  appointmentDate: string
  cessationDate?: string | null
  dscExpiryDate?: string | null
  dir3KycStatus: string
  dir3KycLastFiledDate?: string | null
  isSignatory: boolean
  createdAt: string
}

export type CreateDirectorRequest = {
  companyId: string
  din: string
  directorName: string
  pan?: string | null
  designation?: string
  appointmentDate: string
  cessationDate?: string | null
  dscExpiryDate?: string | null
  dir3KycStatus?: string
  dir3KycLastFiledDate?: string | null
  isSignatory?: boolean
}

export type McaFilingDto = {
  id: string
  companyId: string
  companyName?: string | null
  financialYear: string
  formType: string
  dueDate: string
  filingDate?: string | null
  srn?: string | null
  status: string
  normalFee: number
  lateFee: number
  totalFee: number
  remarks?: string | null
  daysDelayed: number
  createdAt: string
}

export type CreateFilingRequest = {
  companyId: string
  financialYear: string
  formType: string
  dueDate: string
  filingDate?: string | null
  srn?: string | null
  status?: string
  normalFee?: number
  remarks?: string | null
}

export type BoardResolutionDto = {
  id: string
  companyId: string
  companyName?: string | null
  resolutionNumber: string
  meetingType: string
  meetingDate: string
  category: string
  subject: string
  verbiage: string
  passedUnanimously: boolean
  signatoryDin?: string | null
  signatoryName?: string | null
  createdAt: string
}

export type CreateResolutionRequest = {
  companyId: string
  resolutionNumber: string
  meetingType: string
  meetingDate: string
  category?: string
  subject: string
  verbiage: string
  passedUnanimously?: boolean
  signatoryDin?: string | null
  signatoryName?: string | null
}

export type McaComplianceSummaryDto = {
  totalCompaniesCount: number
  activeCompaniesCount: number
  totalDirectorsCount: number
  pendingKycCount: number
  pendingFilingsCount: number
  approvedFilingsCount: number
  totalLateFeesAccrued: number
}

export type CalculateMcaLateFeeRequest = {
  formType: string
  dueDate: string
  filingDate: string
  paidUpCapital?: number
}

export type McaLateFeeCalculationResult = {
  formType: string
  dueDate: string
  filingDate: string
  daysDelayed: number
  normalFee: number
  lateFee: number
  totalFee: number
  legalProvisionDescription: string
}

export const mcaApi = {
  summary: () => api<McaComplianceSummaryDto>('/api/mca/summary'),

  listCompanies: (clientId?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<CompanyMasterDto[]>(`/api/mca/companies${qs ? `?${qs}` : ''}`)
  },

  getCompany: (id: string) => api<CompanyMasterDto>(`/api/mca/companies/${id}`),

  createCompany: (req: CreateCompanyRequest) =>
    api<CompanyMasterDto>('/api/mca/companies', { method: 'POST', body: JSON.stringify(req) }),

  listDirectors: (companyId?: string, kycStatus?: string) => {
    const p = new URLSearchParams()
    if (companyId) p.set('companyId', companyId)
    if (kycStatus) p.set('kycStatus', kycStatus)
    const qs = p.toString()
    return api<DirectorDinDto[]>(`/api/mca/directors${qs ? `?${qs}` : ''}`)
  },

  createDirector: (req: CreateDirectorRequest) =>
    api<DirectorDinDto>('/api/mca/directors', { method: 'POST', body: JSON.stringify(req) }),

  listFilings: (companyId?: string, fy?: string, formType?: string, status?: string) => {
    const p = new URLSearchParams()
    if (companyId) p.set('companyId', companyId)
    if (fy) p.set('financialYear', fy)
    if (formType) p.set('formType', formType)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<McaFilingDto[]>(`/api/mca/filings${qs ? `?${qs}` : ''}`)
  },

  createFiling: (req: CreateFilingRequest) =>
    api<McaFilingDto>('/api/mca/filings', { method: 'POST', body: JSON.stringify(req) }),

  listResolutions: (companyId?: string, meetingType?: string) => {
    const p = new URLSearchParams()
    if (companyId) p.set('companyId', companyId)
    if (meetingType) p.set('meetingType', meetingType)
    const qs = p.toString()
    return api<BoardResolutionDto[]>(`/api/mca/resolutions${qs ? `?${qs}` : ''}`)
  },

  createResolution: (req: CreateResolutionRequest) =>
    api<BoardResolutionDto>('/api/mca/resolutions', { method: 'POST', body: JSON.stringify(req) }),

  calculateLateFee: (req: CalculateMcaLateFeeRequest) =>
    api<McaLateFeeCalculationResult>('/api/mca/late-fee/calculate', { method: 'POST', body: JSON.stringify(req) }),
}

// -------------------------------------------------------------
// Sprint 19: Digital Engagement Letters & KYC Compliance Types
// -------------------------------------------------------------

export type EngagementLetterDto = {
  id: string
  clientId: string
  clientName?: string | null
  letterNumber: string
  engagementType: string
  financialYear: string
  scopeOfWork: string
  managementResponsibilities: string
  auditorResponsibilities: string
  feeStructure: string
  agreedFeeAmount: number
  outOfPocketIncluded: boolean
  status: string
  sentDate?: string | null
  acceptedDate?: string | null
  clientSignatoryName?: string | null
  clientSignatoryDesignation?: string | null
  acceptanceIp?: string | null
  letterContent: string
  createdAt: string
}

export type CreateEngagementLetterRequest = {
  clientId: string
  engagementType: string
  financialYear: string
  scopeOfWork?: string
  managementResponsibilities?: string
  auditorResponsibilities?: string
  feeStructure?: string
  agreedFeeAmount: number
  outOfPocketIncluded?: boolean
  letterContent?: string
}

export type AcceptEngagementLetterRequest = {
  signatoryName: string
  signatoryDesignation: string
  ipAddress?: string
}

export type ClientKycRecordDto = {
  id: string
  clientId: string
  clientName?: string | null
  riskRating: string
  verificationStatus: string
  constitutionDocumentType: string
  constitutionDocumentNumber?: string | null
  panVerified: boolean
  gstinVerified: boolean
  addressProofType?: string | null
  addressProofNumber?: string | null
  uboName?: string | null
  uboShareholdingPercentage?: number | null
  uboPan?: string | null
  sanctionsCheckCleared: boolean
  adverseMediaCheckCleared: boolean
  riskRationale?: string | null
  lastReviewedDate: string
  nextReviewDueDate: string
  createdAt: string
}

export type SaveClientKycRequest = {
  clientId: string
  riskRating: string
  verificationStatus?: string
  constitutionDocumentType: string
  constitutionDocumentNumber?: string | null
  panVerified?: boolean
  gstinVerified?: boolean
  addressProofType?: string | null
  addressProofNumber?: string | null
  uboName?: string | null
  uboShareholdingPercentage?: number | null
  uboPan?: string | null
  sanctionsCheckCleared?: boolean
  adverseMediaCheckCleared?: boolean
  riskRationale?: string | null
}

export type EngagementKycSummaryDto = {
  totalLettersCount: number
  acceptedLettersCount: number
  pendingLettersCount: number
  totalKycRecordsCount: number
  verifiedKycCount: number
  highRiskKycCount: number
  totalContractedFeeValue: number
}

export type GenerateTemplateRequest = {
  engagementType: string
  clientName: string
  financialYear: string
  feeStructure?: string
  agreedFeeAmount: number
}

export type GeneratedEngagementTemplate = {
  engagementType: string
  defaultScope: string
  defaultManagementResponsibilities: string
  defaultAuditorResponsibilities: string
  fullLetterContent: string
}

export const engagementApi = {
  summary: () => api<EngagementKycSummaryDto>('/api/engagements/summary'),

  listLetters: (clientId?: string, type?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (type) p.set('type', type)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<EngagementLetterDto[]>(`/api/engagements${qs ? `?${qs}` : ''}`)
  },

  getLetter: (id: string) => api<EngagementLetterDto>(`/api/engagements/${id}`),

  createLetter: (req: CreateEngagementLetterRequest) =>
    api<EngagementLetterDto>('/api/engagements', { method: 'POST', body: JSON.stringify(req) }),

  sendLetter: (id: string) =>
    api<EngagementLetterDto>(`/api/engagements/${id}/send`, { method: 'POST' }),

  acceptLetter: (id: string, req: AcceptEngagementLetterRequest) =>
    api<EngagementLetterDto>(`/api/engagements/${id}/accept`, { method: 'POST', body: JSON.stringify(req) }),

  generateTemplate: (req: GenerateTemplateRequest) =>
    api<GeneratedEngagementTemplate>('/api/engagements/template', { method: 'POST', body: JSON.stringify(req) }),

  listKyc: (clientId?: string, riskRating?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (riskRating) p.set('riskRating', riskRating)
    const qs = p.toString()
    return api<ClientKycRecordDto[]>(`/api/kyc${qs ? `?${qs}` : ''}`)
  },

  getKyc: (clientId: string) => api<ClientKycRecordDto>(`/api/kyc/${clientId}`),

  saveKyc: (req: SaveClientKycRequest) =>
    api<ClientKycRecordDto>('/api/kyc', { method: 'POST', body: JSON.stringify(req) }),
}

// -------------------------------------------------------------
// Sprint 20: Bank Feeds & Automated BRS Auto-Matcher Types
// -------------------------------------------------------------

export type BankAccountDto = {
  id: string
  clientId: string
  clientName: string
  accountName: string
  bankName: string
  branchName?: string | null
  accountNumber: string
  ifscCode: string
  accountType: string
  openingBalance: number
  currentBalance: number
  currency: string
  isActive: boolean
  createdAt: string
}

export type CreateBankAccountRequest = {
  clientId: string
  accountName: string
  bankName: string
  branchName?: string | null
  accountNumber: string
  ifscCode: string
  accountType: string
  openingBalance: number
  currency?: string | null
}

export type UpdateBankAccountRequest = {
  accountName: string
  bankName: string
  branchName?: string | null
  accountNumber: string
  ifscCode: string
  accountType: string
  isActive: boolean
}

export type BankStatementDto = {
  id: string
  bankAccountId: string
  statementDate: string
  startDate: string
  endDate: string
  fileName: string
  format: string
  openingBalance: number
  closingBalance: number
  totalDebits: number
  totalCredits: number
  totalTransactions: number
  status: string
  createdAt: string
}

export type UploadStatementRequest = {
  fileName: string
  csvContent: string
  statementDate?: string | null
}

export type BankTransactionDto = {
  id: string
  statementId: string
  bankAccountId: string
  transactionDate: string
  valueDate?: string | null
  narration: string
  referenceNumber?: string | null
  transactionType: string
  amount: number
  runningBalance?: number | null
  category?: string | null
  matchStatus: string
  matchedEntityType?: string | null
  matchedEntityId?: string | null
  notes?: string | null
}

export type MatchTransactionRequest = {
  matchStatus: string
  matchedEntityType?: string | null
  matchedEntityId?: string | null
  notes?: string | null
}

export type BankReconciliationSummaryDto = {
  bankAccountId: string
  accountName: string
  bankName: string
  accountNumber: string
  asOfDate: string
  bankStatementBalance: number
  totalUnclearedDebits: number
  totalUncreditedCredits: number
  directDebitsNotBooked: number
  directCreditsNotBooked: number
  balanceAsPerBooks: number
  reconciledDifference: number
  unmatchedCount: number
  matchedCount: number
}

export type BankReconciliationRuleDto = {
  id: string
  ruleName: string
  matchKeyword: string
  transactionType: string
  assignCategory: string
  priority: number
  isActive: boolean
}

export type CreateBankReconciliationRuleRequest = {
  ruleName: string
  matchKeyword: string
  transactionType: string
  assignCategory: string
  priority: number
}

export const bankFeedApi = {
  getAccounts: (clientId?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    const qs = p.toString()
    return api<BankAccountDto[]>(`/api/bank/accounts${qs ? `?${qs}` : ''}`)
  },
  getAccount: (id: string) => api<BankAccountDto>(`/api/bank/accounts/${id}`),
  createAccount: (req: CreateBankAccountRequest) =>
    api<BankAccountDto>('/api/bank/accounts', { method: 'POST', body: JSON.stringify(req) }),
  updateAccount: (id: string, req: UpdateBankAccountRequest) =>
    api<BankAccountDto>(`/api/bank/accounts/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteAccount: (id: string) => api<void>(`/api/bank/accounts/${id}`, { method: 'DELETE' }),

  getStatements: (accountId: string) => api<BankStatementDto[]>(`/api/bank/accounts/${accountId}/statements`),
  uploadStatement: (accountId: string, req: UploadStatementRequest) =>
    api<BankStatementDto>(`/api/bank/accounts/${accountId}/statements`, { method: 'POST', body: JSON.stringify(req) }),

  getTransactions: (
    accountId: string,
    statementId?: string,
    matchStatus?: string,
    fromDate?: string,
    toDate?: string
  ) => {
    const p = new URLSearchParams()
    if (statementId) p.set('statementId', statementId)
    if (matchStatus) p.set('matchStatus', matchStatus)
    if (fromDate) p.set('fromDate', fromDate)
    if (toDate) p.set('toDate', toDate)
    const qs = p.toString()
    return api<BankTransactionDto[]>(`/api/bank/accounts/${accountId}/transactions${qs ? `?${qs}` : ''}`)
  },
  matchTransaction: (transactionId: string, req: MatchTransactionRequest) =>
    api<BankTransactionDto>(`/api/bank/transactions/${transactionId}/match`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  getBrsSummary: (accountId: string, asOfDate?: string) => {
    const p = new URLSearchParams()
    if (asOfDate) p.set('asOfDate', asOfDate)
    const qs = p.toString()
    return api<BankReconciliationSummaryDto>(`/api/bank/accounts/${accountId}/brs${qs ? `?${qs}` : ''}`)
  },

  getRules: () => api<BankReconciliationRuleDto[]>('/api/bank/rules'),
  createRule: (req: CreateBankReconciliationRuleRequest) =>
    api<BankReconciliationRuleDto>('/api/bank/rules', { method: 'POST', body: JSON.stringify(req) }),
  deleteRule: (id: string) => api<void>(`/api/bank/rules/${id}`, { method: 'DELETE' }),
}

// -------------------------------------------------------------
// Sprint 21: Practice MIS & Executive Reporting Desk Types
// -------------------------------------------------------------

export type PracticeTargetDto = {
  id: string
  financialYear: string
  targetAnnualRevenue: number
  targetBillableHours: number
  targetRecoveryPercentage: number
  targetComplianceSlaPct: number
  createdAt: string
  updatedAt?: string | null
}

export type SetPracticeTargetRequest = {
  financialYear: string
  targetAnnualRevenue: number
  targetBillableHours: number
  targetRecoveryPercentage: number
  targetComplianceSlaPct: number
}

export type PracticeExecutiveSummaryDto = {
  financialYear: string
  totalBilledYtd: number
  totalCollectedYtd: number
  totalOutstandingReceivables: number
  overallRealizationRatePct: number
  totalBillableHoursLogged: number
  totalNonBillableHoursLogged: number
  staffUtilizationPct: number
  totalDeadlinesCount: number
  filedOnTimeCount: number
  filedLateCount: number
  overdueCount: number
  slaCompliancePct: number
  activeClientsCount: number
  averageRevenuePerClient: number
  targets?: PracticeTargetDto | null
}

export type ClientProfitabilityDto = {
  clientId: string
  clientName: string
  entityType?: string | null
  totalInvoiced: number
  totalPaid: number
  balanceDue: number
  totalHoursLogged: number
  effectiveHourlyRate: number
  profitabilityRating: string
}

export type StaffUtilizationDto = {
  userId: string
  staffName: string
  roleName: string
  billableHours: number
  nonBillableHours: number
  totalHours: number
  utilizationPct: number
  billedValueGenerated: number
}

export type AgedInvoiceItemDto = {
  invoiceId: string
  invoiceNumber: string
  clientId: string
  clientName: string
  issueDate: string
  dueDate: string
  ageDays: number
  totalAmount: number
  balanceDue: number
  agingBucket: string
}

export type ReceivablesAgingDto = {
  totalReceivables: number
  current0To30Days: number
  bucket31To60Days: number
  bucket61To90Days: number
  bucketAbove90Days: number
  agedInvoices: AgedInvoiceItemDto[]
}

export type ComplianceTypeSlaDto = {
  category: string
  totalCount: number
  filedOnTime: number
  filedLate: number
  overdue: number
  slaPercentage: number
}

export type ComplianceSlaReportDto = {
  financialYear: string
  totalStatutoryFilings: number
  filedOnTime: number
  filedLate: number
  currentlyOverdue: number
  overallSlaPercentage: number
  categoryBreakdown: ComplianceTypeSlaDto[]
}

export type RevenueBreakdownDto = {
  serviceCategory: string
  amount: number
  percentageOfTotal: number
}

export const reportsApi = {
  getExecutiveSummary: (fy?: string) => {
    const p = new URLSearchParams()
    if (fy) p.set('financialYear', fy)
    const qs = p.toString()
    return api<PracticeExecutiveSummaryDto>(`/api/reports/executive-summary${qs ? `?${qs}` : ''}`)
  },
  getClientProfitability: (fy?: string) => {
    const p = new URLSearchParams()
    if (fy) p.set('financialYear', fy)
    const qs = p.toString()
    return api<ClientProfitabilityDto[]>(`/api/reports/client-profitability${qs ? `?${qs}` : ''}`)
  },
  getStaffUtilization: (from?: string, to?: string) => {
    const p = new URLSearchParams()
    if (from) p.set('from', from)
    if (to) p.set('to', to)
    const qs = p.toString()
    return api<StaffUtilizationDto[]>(`/api/reports/staff-utilization${qs ? `?${qs}` : ''}`)
  },
  getReceivablesAging: () => api<ReceivablesAgingDto>('/api/reports/receivables-aging'),
  getComplianceSla: (fy?: string) => {
    const p = new URLSearchParams()
    if (fy) p.set('financialYear', fy)
    const qs = p.toString()
    return api<ComplianceSlaReportDto>(`/api/reports/compliance-sla${qs ? `?${qs}` : ''}`)
  },
  getRevenueBreakdown: (fy?: string) => {
    const p = new URLSearchParams()
    if (fy) p.set('financialYear', fy)
    const qs = p.toString()
    return api<RevenueBreakdownDto[]>(`/api/reports/revenue-breakdown${qs ? `?${qs}` : ''}`)
  },
  getTarget: (fy: string) => api<PracticeTargetDto>(`/api/reports/targets/${fy}`),
  setTarget: (req: SetPracticeTargetRequest) =>
    api<PracticeTargetDto>('/api/reports/targets', { method: 'POST', body: JSON.stringify(req) }),
}

// ---------------------------------------------------------------------------
// Sprint 22: Tax Notice & Faceless Assessment Management Desk
// ---------------------------------------------------------------------------
export type TaxAuthority = 'IncomeTax' | 'Gst' | 'Customs' | 'Other'
export type NoticeCategory =
  | 'Intimation143_1'
  | 'DefectiveReturn139_9'
  | 'Inquiry142_1'
  | 'Reassessment148_148A'
  | 'DemandNotice156'
  | 'RefundAdjustment245'
  | 'PenaltyNotice270A'
  | 'GstDrc01A'
  | 'GstDrc01'
  | 'GstAsmt10'
  | 'GstReg17'
  | 'GstDrc07'
  | 'GeneralNotice'
export type NoticeStatus =
  | 'Received'
  | 'InReview'
  | 'DraftPrepared'
  | 'PartnerApproved'
  | 'Submitted'
  | 'DisposedOrClosed'
  | 'Appealed'
export type DinStatus = 'Unverified' | 'Valid' | 'Invalid' | 'NotApplicable'
export type HearingType = 'WrittenSubmission' | 'VideoConference' | 'PhysicalHearing'
export type HearingStatus = 'Scheduled' | 'Attended' | 'AdjournmentRequested' | 'Concluded' | 'Cancelled'
export type ResponseStatus = 'Draft' | 'UnderReview' | 'PartnerApproved' | 'UploadedToPortal'
export type AppealStatus = 'NotAppealed' | 'AppealDrafted' | 'FiledCitA' | 'StayGranted' | 'DemandDeleted' | 'Settled'

export type TaxNoticeDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  authority: TaxAuthority
  category: NoticeCategory
  section: string
  noticeNumber: string
  din?: string | null
  dinStatus: DinStatus
  assessmentYear?: string | null
  financialYear: string
  issueDate: string
  dueDate: string
  daysRemaining: number
  demandAmount: number
  refundDisallowed: number
  status: NoticeStatus
  subject: string
  description?: string | null
  assignedToUserId?: string | null
  assignedToName?: string | null
  officerName?: string | null
  officerJurisdiction?: string | null
  hearingCount: number
  responseCount: number
  createdAt: string
}

export type NoticeHearingDto = {
  id: string
  noticeId: string
  noticeSection: string
  noticeNumber: string
  clientName: string
  hearingDate: string
  hearingType: HearingType
  officerName?: string | null
  officerDesignation?: string | null
  meetingLink?: string | null
  status: HearingStatus
  adjournmentReason?: string | null
  summaryNotes?: string | null
  attendedByUserId?: string | null
  attendedByName?: string | null
  createdAt: string
}

export type NoticeResponseDto = {
  id: string
  noticeId: string
  responseVersion: number
  submissionTitle: string
  summaryOfSubmissions: string
  legalGrounds?: string | null
  status: ResponseStatus
  preparedByUserId?: string | null
  preparedByName?: string | null
  approvedByUserId?: string | null
  approvedByName?: string | null
  approvedAt?: string | null
  portalAckNumber?: string | null
  submittedAt?: string | null
  attachmentNames?: string | null
  createdAt: string
}

export type TaxDemandDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  noticeId?: string | null
  assessmentYear?: string | null
  financialYear: string
  section: string
  originalDemand: number
  stayedAmount: number
  paidAmount: number
  adjustedRefund: number
  outstandingAmount: number
  demandDate: string
  appealDueDate: string
  daysUntilAppealDue: number
  appealStatus: AppealStatus
  appealAckNumber?: string | null
  remarks?: string | null
}

export type NoticeDashboardSummaryDto = {
  totalActiveNotices: number
  incomeTaxNotices: number
  gstNotices: number
  pendingHearings: number
  pendingApprovals: number
  totalDemandAmount: number
  totalOutstandingDemand: number
  appealsDueWithin30Days: number
  invalidDinCount: number
}

export type CreateTaxNoticeRequest = {
  clientId: string
  authority: TaxAuthority
  category: NoticeCategory
  section: string
  noticeNumber: string
  din?: string | null
  assessmentYear?: string | null
  financialYear: string
  issueDate: string
  dueDate: string
  demandAmount: number
  refundDisallowed: number
  subject: string
  description?: string | null
  assignedToUserId?: string | null
  officerName?: string | null
  officerJurisdiction?: string | null
}

export type UpdateTaxNoticeRequest = {
  status: NoticeStatus
  din?: string | null
  dueDate: string
  demandAmount: number
  refundDisallowed: number
  subject: string
  description?: string | null
  assignedToUserId?: string | null
  officerName?: string | null
  officerJurisdiction?: string | null
}

export type CreateHearingRequest = {
  hearingDate: string
  hearingType: HearingType
  officerName?: string | null
  officerDesignation?: string | null
  meetingLink?: string | null
  summaryNotes?: string | null
}

export type UpdateHearingStatusRequest = {
  status: HearingStatus
  adjournmentReason?: string | null
  summaryNotes?: string | null
  attendedByUserId?: string | null
}

export type DraftResponseRequest = {
  submissionTitle: string
  summaryOfSubmissions: string
  legalGrounds?: string | null
  attachmentNames?: string | null
}

export type RecordSubmissionRequest = {
  portalAckNumber: string
  submittedAt?: string | null
}

export type CreateTaxDemandRequest = {
  clientId: string
  noticeId?: string | null
  assessmentYear?: string | null
  financialYear: string
  section: string
  originalDemand: number
  demandDate: string
  appealDueDate?: string | null
  remarks?: string | null
}

export type RecordDemandPaymentRequest = {
  amount: number
  isRefundAdjustment: boolean
  remarks?: string | null
}

export type UpdateAppealRequest = {
  status: AppealStatus
  appealAckNumber?: string | null
  stayedAmount?: number | null
  remarks?: string | null
}

export const taxNoticesApi = {
  getSummary: () => api<NoticeDashboardSummaryDto>('/api/tax-notices/summary'),
  getNotices: (authority?: TaxAuthority, status?: NoticeStatus, clientId?: string) => {
    const p = new URLSearchParams()
    if (authority) p.set('authority', authority)
    if (status) p.set('status', status)
    if (clientId) p.set('clientId', clientId)
    const qs = p.toString()
    return api<TaxNoticeDto[]>(`/api/tax-notices${qs ? `?${qs}` : ''}`)
  },
  getNoticeById: (id: string) => api<TaxNoticeDto>(`/api/tax-notices/${id}`),
  createNotice: (req: CreateTaxNoticeRequest) =>
    api<TaxNoticeDto>('/api/tax-notices', { method: 'POST', body: JSON.stringify(req) }),
  updateNotice: (id: string, req: UpdateTaxNoticeRequest) =>
    api<TaxNoticeDto>(`/api/tax-notices/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  verifyDin: (id: string) => api<TaxNoticeDto>(`/api/tax-notices/${id}/verify-din`, { method: 'POST' }),

  getHearings: (noticeId?: string, status?: HearingStatus) => {
    const p = new URLSearchParams()
    if (noticeId) p.set('noticeId', noticeId)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<NoticeHearingDto[]>(`/api/tax-notices/hearings${qs ? `?${qs}` : ''}`)
  },
  addHearing: (noticeId: string, req: CreateHearingRequest) =>
    api<NoticeHearingDto>(`/api/tax-notices/${noticeId}/hearings`, { method: 'POST', body: JSON.stringify(req) }),
  updateHearingStatus: (hearingId: string, req: UpdateHearingStatusRequest) =>
    api<NoticeHearingDto>(`/api/tax-notices/hearings/${hearingId}/status`, { method: 'PUT', body: JSON.stringify(req) }),

  getResponses: (noticeId: string) => api<NoticeResponseDto[]>(`/api/tax-notices/${noticeId}/responses`),
  draftResponse: (noticeId: string, req: DraftResponseRequest) =>
    api<NoticeResponseDto>(`/api/tax-notices/${noticeId}/responses`, { method: 'POST', body: JSON.stringify(req) }),
  approveResponse: (responseId: string) =>
    api<NoticeResponseDto>(`/api/tax-notices/responses/${responseId}/approve`, { method: 'POST' }),
  submitResponse: (responseId: string, req: RecordSubmissionRequest) =>
    api<NoticeResponseDto>(`/api/tax-notices/responses/${responseId}/submit`, { method: 'POST', body: JSON.stringify(req) }),

  getDemands: (clientId?: string, overdueOnly?: boolean) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (overdueOnly) p.set('overdueOnly', 'true')
    const qs = p.toString()
    return api<TaxDemandDto[]>(`/api/tax-notices/demands${qs ? `?${qs}` : ''}`)
  },
  createDemand: (req: CreateTaxDemandRequest) =>
    api<TaxDemandDto>('/api/tax-notices/demands', { method: 'POST', body: JSON.stringify(req) }),
  recordDemandPayment: (demandId: string, req: RecordDemandPaymentRequest) =>
    api<TaxDemandDto>(`/api/tax-notices/demands/${demandId}/payment`, { method: 'POST', body: JSON.stringify(req) }),
  updateAppeal: (demandId: string, req: UpdateAppealRequest) =>
    api<TaxDemandDto>(`/api/tax-notices/demands/${demandId}/appeal`, { method: 'PUT', body: JSON.stringify(req) }),
}

// ---------------------------------------------------------------------------
// Sprint 23: Payroll, Salary TDS & Statutory PF / ESIC Engine
// ---------------------------------------------------------------------------
export type PayrollTaxRegime = 'NewRegime' | 'OldRegime'
export type PayrollRunStatus = 'Draft' | 'Approved' | 'Disbursed' | 'Cancelled'

export type PayrollEmployeeDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  employeeCode: string
  name: string
  pan?: string | null
  uan?: string | null
  esicNumber?: string | null
  dateOfJoining: string
  designation: string
  department?: string | null
  state: string
  taxRegime: PayrollTaxRegime
  monthlyCtc: number
  basicSalary: number
  hra: number
  specialAllowance: number
  otherAllowances: number
  grossSalary: number
  isPfEligible: boolean
  isEsiEligible: boolean
  isPtEligible: boolean
  isActive: boolean
  estimatedPf: number
  estimatedEsi: number
  estimatedPt: number
  estimatedTds: number
  estimatedNet: number
}

export type PayrollRunDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  payrollPeriod: string
  month: number
  year: number
  status: PayrollRunStatus
  totalEmployees: number
  totalGrossPay: number
  totalNetPay: number
  totalEmployeePf: number
  totalEmployerPf: number
  totalEmployeeEsi: number
  totalEmployerEsi: number
  totalPt: number
  totalTds: number
  processedAt: string
  notes?: string | null
}

export type PayrollPayslipDto = {
  id: string
  payrollRunId: string
  employeeId: string
  employeeCode: string
  employeeName: string
  pan?: string | null
  uan?: string | null
  daysWorked: number
  daysInMonth: number
  basicSalary: number
  hra: number
  specialAllowance: number
  bonusIncentive: number
  grossSalary: number
  employeePf: number
  employeeEsi: number
  professionalTax: number
  salaryTds: number
  otherDeductions: number
  totalDeductions: number
  employerEps: number
  employerEpf: number
  employerEsi: number
  netSalary: number
}

export type PayrollSummaryDto = {
  totalEmployees: number
  activeEmployees: number
  totalMonthlyGross: number
  totalMonthlyPfLiability: number
  totalMonthlyEsiLiability: number
  totalMonthlyPt: number
  totalMonthlyTds: number
  latestPayrollPeriod?: string | null
}

export type EpfoEcrExportDto = {
  payrollRunId: string
  payrollPeriod: string
  clientName: string
  employeeCount: number
  totalWages: number
  totalEmployeePf: number
  totalEmployerEps: number
  totalEmployerEpf: number
  ecrText: string
}

export type SavePayrollEmployeeRequest = {
  clientId: string
  employeeCode: string
  name: string
  pan?: string | null
  uan?: string | null
  esicNumber?: string | null
  dateOfJoining: string
  designation: string
  department?: string | null
  state: string
  taxRegime: PayrollTaxRegime
  monthlyCtc: number
  basicSalary: number
  hra: number
  specialAllowance: number
  otherAllowances: number
  isPfEligible: boolean
  isEsiEligible: boolean
  isPtEligible: boolean
  isActive: boolean
}

export type EmployeeBonusOverride = {
  employeeId: string
  bonusIncentive: number
  daysWorked?: number | null
}

export type ProcessPayrollRunRequest = {
  clientId: string
  year: number
  month: number
  notes?: string | null
  overrides?: EmployeeBonusOverride[] | null
}

export type UpdatePayrollRunStatusRequest = {
  status: PayrollRunStatus
  notes?: string | null
}

export const payrollApi = {
  getSummary: (clientId?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    const qs = p.toString()
    return api<PayrollSummaryDto>(`/api/payroll/summary${qs ? `?${qs}` : ''}`)
  },
  getEmployees: (clientId?: string, activeOnly?: boolean) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (activeOnly) p.set('activeOnly', 'true')
    const qs = p.toString()
    return api<PayrollEmployeeDto[]>(`/api/payroll/employees${qs ? `?${qs}` : ''}`)
  },
  getEmployeeById: (id: string) => api<PayrollEmployeeDto>(`/api/payroll/employees/${id}`),
  createEmployee: (req: SavePayrollEmployeeRequest) =>
    api<PayrollEmployeeDto>('/api/payroll/employees', { method: 'POST', body: JSON.stringify(req) }),
  updateEmployee: (id: string, req: SavePayrollEmployeeRequest) =>
    api<PayrollEmployeeDto>(`/api/payroll/employees/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteEmployee: (id: string) => api(`/api/payroll/employees/${id}`, { method: 'DELETE' }),

  getRuns: (clientId?: string, year?: number) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (year) p.set('year', year.toString())
    const qs = p.toString()
    return api<PayrollRunDto[]>(`/api/payroll/runs${qs ? `?${qs}` : ''}`)
  },
  getRunById: (id: string) => api<PayrollRunDto>(`/api/payroll/runs/${id}`),
  processRun: (req: ProcessPayrollRunRequest) =>
    api<PayrollRunDto>('/api/payroll/runs', { method: 'POST', body: JSON.stringify(req) }),
  updateRunStatus: (id: string, req: UpdatePayrollRunStatusRequest) =>
    api<PayrollRunDto>(`/api/payroll/runs/${id}/status`, { method: 'PUT', body: JSON.stringify(req) }),
  getPayslips: (runId: string) => api<PayrollPayslipDto[]>(`/api/payroll/runs/${runId}/payslips`),
  exportEcr: (runId: string) => api<EpfoEcrExportDto>(`/api/payroll/runs/${runId}/ecr`),
}

// ---------------------------------------------------------------------------
// Sprint 24: Automated E-Invoicing & E-Way Bill Integration Desk
// ---------------------------------------------------------------------------
export type EInvoiceDocType = 1 | 2 | 3 | 'INV' | 'CRN' | 'DBN'
export type EInvoiceSupplyType = 1 | 2 | 3 | 4 | 5 | 6 | 'B2B' | 'SEZWP' | 'SEZWOP' | 'EXPWP' | 'EXPWOP' | 'DEXP'
export type EInvoiceStatus = 1 | 2 | 3 | 4 | 'Draft' | 'Generated' | 'Cancelled' | 'Failed'

export type EwbSupplyType = 1 | 2 | 'Outward' | 'Inward'
export type EwbSubSupplyType = 1 | 2 | 3 | 4 | 5 | 6 | 99 | 'Supply' | 'Export' | 'JobWork' | 'SKD_CKD' | 'LineSale' | 'Exhibition' | 'Others'
export type EwbTransportMode = 1 | 2 | 3 | 4 | 'Road' | 'Rail' | 'Air' | 'Ship'
export type EwbVehicleType = 1 | 2 | 'Regular' | 'OverDimensionalCargo'
export type EwbStatus = 1 | 2 | 3 | 4 | 'Active' | 'Extended' | 'Cancelled' | 'Expired'

export type EInvoiceDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  invoiceId?: string | null
  docType: EInvoiceDocType
  docNumber: string
  docDate: string
  financialYear: string
  supplierGstin: string
  supplierTradeName: string
  recipientGstin: string
  recipientLegalName: string
  supplyType: EInvoiceSupplyType
  totalTaxableValue: number
  cgstAmount: number
  sgstAmount: number
  igstAmount: number
  cessAmount: number
  totalInvoiceValue: number
  irn?: string | null
  signedQrCode?: string | null
  ackNo?: string | null
  ackDate?: string | null
  status: EInvoiceStatus
  cancelReason?: string | null
  cancelRemarks?: string | null
  cancelledAt?: string | null
  createdAt: string
}

export type EWayBillDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  eInvoiceId?: string | null
  ewbNumber: string
  ewbDate: string
  validUntil: string
  remainingHours: number
  supplyType: EwbSupplyType
  subSupplyType: EwbSubSupplyType
  docType: string
  docNumber: string
  docDate: string
  fromGstin: string
  fromTradeName: string
  fromPincode: string
  fromStateCode: number
  toGstin: string
  toTradeName: string
  toPincode: string
  toStateCode: number
  totalValue: number
  hsnCode: string
  distanceKm: number
  transportMode: EwbTransportMode
  vehicleType: EwbVehicleType
  vehicleNumber?: string | null
  transporterId?: string | null
  transporterName?: string | null
  status: EwbStatus
  cancelReason?: string | null
  cancelledAt?: string | null
  createdAt: string
}

export type EInvoicingSummaryDto = {
  totalEInvoices: number
  activeIrnCount: number
  cancelledIrnCount: number
  totalEInvoicedValue: number
  activeEWayBills: number
  expiringSoonEwbCount: number
  totalEwbValue: number
}

export type GenerateEInvoiceRequest = {
  clientId: string
  invoiceId?: string | null
  docType: EInvoiceDocType
  docNumber: string
  docDate: string
  financialYear: string
  supplierGstin: string
  supplierTradeName: string
  recipientGstin: string
  recipientLegalName: string
  supplyType: EInvoiceSupplyType
  totalTaxableValue: number
  cgstAmount: number
  sgstAmount: number
  igstAmount: number
  cessAmount: number
  mainHsnCode: string
  itemCount: number
}

export type CancelEInvoiceRequest = {
  reason: string
  remarks?: string | null
}

export type GenerateEWayBillRequest = {
  clientId: string
  eInvoiceId?: string | null
  supplyType: EwbSupplyType
  subSupplyType: EwbSubSupplyType
  docType: string
  docNumber: string
  docDate: string
  fromGstin: string
  fromTradeName: string
  fromPincode: string
  fromStateCode: number
  toGstin: string
  toTradeName: string
  toPincode: string
  toStateCode: number
  totalValue: number
  hsnCode: string
  distanceKm: number
  transportMode: EwbTransportMode
  vehicleType: EwbVehicleType
  vehicleNumber?: string | null
  transporterId?: string | null
  transporterName?: string | null
}

export type UpdateEWayBillPartBRequest = {
  vehicleNumber: string
  transporterId?: string | null
  transporterName?: string | null
  reason: string
}

export type CancelEWayBillRequest = {
  reason: string
}

export const eInvoicingApi = {
  getSummary: (clientId?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    const qs = p.toString()
    return api<EInvoicingSummaryDto>(`/api/einvoicing/summary${qs ? `?${qs}` : ''}`)
  },
  getInvoices: (clientId?: string, status?: EInvoiceStatus) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (status !== undefined) p.set('status', status.toString())
    const qs = p.toString()
    return api<EInvoiceDto[]>(`/api/einvoicing/invoices${qs ? `?${qs}` : ''}`)
  },
  getInvoiceById: (id: string) => api<EInvoiceDto>(`/api/einvoicing/invoices/${id}`),
  generateInvoice: (req: GenerateEInvoiceRequest) =>
    api<EInvoiceDto>('/api/einvoicing/invoices', { method: 'POST', body: JSON.stringify(req) }),
  cancelInvoice: (id: string, req: CancelEInvoiceRequest) =>
    api<EInvoiceDto>(`/api/einvoicing/invoices/${id}/cancel`, { method: 'POST', body: JSON.stringify(req) }),

  getEWayBills: (clientId?: string, status?: EwbStatus) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (status !== undefined) p.set('status', status.toString())
    const qs = p.toString()
    return api<EWayBillDto[]>(`/api/einvoicing/ewaybills${qs ? `?${qs}` : ''}`)
  },
  getEWayBillById: (id: string) => api<EWayBillDto>(`/api/einvoicing/ewaybills/${id}`),
  generateEWayBill: (req: GenerateEWayBillRequest) =>
    api<EWayBillDto>('/api/einvoicing/ewaybills', { method: 'POST', body: JSON.stringify(req) }),
  updatePartB: (id: string, req: UpdateEWayBillPartBRequest) =>
    api<EWayBillDto>(`/api/einvoicing/ewaybills/${id}/part-b`, { method: 'PUT', body: JSON.stringify(req) }),
  cancelEWayBill: (id: string, req: CancelEWayBillRequest) =>
    api<EWayBillDto>(`/api/einvoicing/ewaybills/${id}/cancel`, { method: 'POST', body: JSON.stringify(req) }),
}

// ---------------------------------------------------------------------------
// Sprint 25: Fixed Asset Register & Dual Depreciation Engine
// ---------------------------------------------------------------------------
export type AssetClass =
  | 'Building'
  | 'PlantAndMachinery'
  | 'ComputersAndSoftware'
  | 'FurnitureAndFixtures'
  | 'OfficeEquipment'
  | 'Vehicles'
  | 'Intangibles'

export type CompanyDepreciationMethod = 'SLM' | 'WDV'
export type ShiftType = 'SingleShift' | 'DoubleShift' | 'TripleShift'
export type AssetStatus = 'Active' | 'Disposed' | 'Scrapped' | 'WrittenOff'
export type PhysicalVerificationStatus = 'FoundAndGood' | 'Damaged' | 'Missing' | 'NeedsMaintenance'

export type FixedAssetDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  assetTag: string
  name: string
  assetClass: AssetClass
  purchaseDate: string
  capitalizationDate: string
  purchaseCost: number
  inwardFreight: number
  installationCost: number
  preOperatingCost: number
  gstItcClaimed: number
  grossCost: number
  salvageValue: number
  usefulLifeYears: number
  companyDepreciationMethod: CompanyDepreciationMethod
  shiftType: ShiftType
  incomeTaxBlockRatePercent: number
  isEligibleForAdditionalDepreciation: boolean
  location?: string | null
  department?: string | null
  custodianName?: string | null
  status: AssetStatus
  disposalDate?: string | null
  disposalProceeds?: number | null
  disposalRemarks?: string | null
  latestBookValue: number
  latestTaxWdv: number
  createdAt: string
}

export type AssetDepreciationEntryDto = {
  id: string
  fixedAssetId: string
  assetTag: string
  assetName: string
  assetClass: AssetClass
  financialYear: string
  openingBookValue: number
  bookDepreciation: number
  closingBookValue: number
  openingTaxWdv: number
  taxDepreciation: number
  closingTaxWdv: number
  timingDifference: number
  deferredTaxImpact: number
  daysPutToUseInYear: number
  isUnder180Days: boolean
  calculatedAt: string
}

export type AssetPhysicalVerificationDto = {
  id: string
  fixedAssetId: string
  assetTag: string
  assetName: string
  verificationDate: string
  verifiedBy: string
  status: PhysicalVerificationStatus
  remarks?: string | null
  locationVerified?: string | null
  createdAt: string
}

export type FixedAssetSummaryDto = {
  totalAssets: number
  activeAssets: number
  disposedAssets: number
  totalGrossBlock: number
  totalBookDepreciation: number
  totalNetBookValue: number
  totalTaxDepreciation: number
  totalTaxWdv: number
  totalTimingDifference: number
  netDeferredTaxLiability: number
  totalVerifications: number
  verifiedInLast12Months: number
  verificationCompliancePercent: number
}

export type SaveFixedAssetRequest = {
  clientId: string
  assetTag: string
  name: string
  assetClass: AssetClass
  purchaseDate: string
  capitalizationDate: string
  purchaseCost: number
  inwardFreight: number
  installationCost: number
  preOperatingCost: number
  gstItcClaimed: number
  salvageValue?: number | null
  usefulLifeYears: number
  companyDepreciationMethod: CompanyDepreciationMethod
  shiftType: ShiftType
  incomeTaxBlockRatePercent: number
  isEligibleForAdditionalDepreciation: boolean
  location?: string | null
  department?: string | null
  custodianName?: string | null
}

export type DisposeAssetRequest = {
  disposalDate: string
  disposalProceeds: number
  remarks?: string | null
}

export type RunDualDepreciationRequest = {
  clientId: string
  financialYear: string
  assetId?: string | null
  corporateTaxRate?: number | null
}

export type RecordPhysicalVerificationRequest = {
  verificationDate: string
  verifiedBy: string
  status: PhysicalVerificationStatus
  remarks?: string | null
  locationVerified?: string | null
}

export const fixedAssetApi = {
  getSummary: (clientId?: string, financialYear?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (financialYear) p.set('financialYear', financialYear)
    const qs = p.toString()
    return api<FixedAssetSummaryDto>(`/api/fixed-assets/summary${qs ? `?${qs}` : ''}`)
  },
  getAssets: (clientId?: string, assetClass?: AssetClass, status?: AssetStatus) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (assetClass) p.set('assetClass', assetClass)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<FixedAssetDto[]>(`/api/fixed-assets${qs ? `?${qs}` : ''}`)
  },
  getAssetById: (id: string) => api<FixedAssetDto>(`/api/fixed-assets/${id}`),
  createAsset: (req: SaveFixedAssetRequest) =>
    api<FixedAssetDto>('/api/fixed-assets', { method: 'POST', body: JSON.stringify(req) }),
  updateAsset: (id: string, req: SaveFixedAssetRequest) =>
    api<FixedAssetDto>(`/api/fixed-assets/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  disposeAsset: (id: string, req: DisposeAssetRequest) =>
    api<FixedAssetDto>(`/api/fixed-assets/${id}/dispose`, { method: 'POST', body: JSON.stringify(req) }),
  deleteAsset: (id: string) => api(`/api/fixed-assets/${id}`, { method: 'DELETE' }),

  runDepreciation: (req: RunDualDepreciationRequest) =>
    api<AssetDepreciationEntryDto[]>('/api/fixed-assets/depreciation', { method: 'POST', body: JSON.stringify(req) }),
  getDepreciationEntries: (assetId?: string, clientId?: string, financialYear?: string) => {
    const p = new URLSearchParams()
    if (assetId) p.set('assetId', assetId)
    if (clientId) p.set('clientId', clientId)
    if (financialYear) p.set('financialYear', financialYear)
    const qs = p.toString()
    return api<AssetDepreciationEntryDto[]>(`/api/fixed-assets/depreciation${qs ? `?${qs}` : ''}`)
  },

  recordVerification: (assetId: string, req: RecordPhysicalVerificationRequest) =>
    api<AssetPhysicalVerificationDto>(`/api/fixed-assets/${assetId}/verifications`, { method: 'POST', body: JSON.stringify(req) }),
  getVerifications: (assetId?: string, clientId?: string) => {
    const p = new URLSearchParams()
    if (assetId) p.set('assetId', assetId)
    if (clientId) p.set('clientId', clientId)
    const qs = p.toString()
    return api<AssetPhysicalVerificationDto[]>(`/api/fixed-assets/verifications${qs ? `?${qs}` : ''}`)
  }
}

// ---------------------------------------------------------------------------
// Sprint 26: Advance Tax Planner & Section 234A/B/C Interest Engine
// ---------------------------------------------------------------------------
export type AdvanceTaxRegime = 'NewRegime' | 'OldRegime'
export type AdvanceTaxPlanStatus = 'Draft' | 'Active' | 'Completed'

export type AdvanceTaxPlanDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  financialYear: string
  assessmentYear: string
  regime: AdvanceTaxRegime
  isPresumptive: boolean
  isSeniorCitizen: boolean
  estimatedGrossIncome: number
  estimatedDeductions: number
  estimatedTaxableIncome: number
  estimatedTaxBeforeCredits: number
  estimatedTdsTcs: number
  otherReliefs: number
  assessedTax: number
  status: AdvanceTaxPlanStatus
  notes?: string | null
  totalAdvanceTaxPaid: number
  totalInterest234c: number
  createdAt: string
}

export type AdvanceTaxInstallmentDto = {
  id: string
  planId: string
  installmentNumber: number
  dueDate: string
  targetPercentage: number
  cumulativeTargetAmount: number
  amountPaid: number
  shortfallAmount: number
  interest234c: number
  challanBsrCode?: string | null
  challanNumber?: string | null
  challanDate?: string | null
  isPaid: boolean
  createdAt: string
}

export type TaxInterestComputationDto = {
  id: string
  planId: string
  assessmentYear: string
  assessedTax: number
  advanceTaxPaid: number
  tdsTcsCredit: number
  selfAssessmentTaxPaid: number
  returnDueDate: string
  actualReturnFilingDate: string
  monthsDelayed234a: number
  interest234a: number
  is234bApplicable: boolean
  monthsDelayed234b: number
  interest234b: number
  interest234cTotal: number
  totalStatutoryInterest: number
  netTaxPayable: number
  computedAt: string
}

export type AdvanceTaxSummaryDto = {
  totalPlans: number
  activePlans: number
  totalEstimatedIncome: number
  totalAssessedTax: number
  totalAdvanceTaxCollected: number
  totalShortfall: number
  totalInterest234c: number
  totalInterest234b: number
  totalInterest234a: number
}

export type SaveAdvanceTaxPlanRequest = {
  clientId: string
  financialYear: string
  assessmentYear: string
  regime: AdvanceTaxRegime
  isPresumptive: boolean
  isSeniorCitizen: boolean
  estimatedGrossIncome: number
  estimatedDeductions: number
  estimatedTaxBeforeCredits: number
  estimatedTdsTcs: number
  otherReliefs: number
  notes?: string | null
}

export type RecordInstallmentPaymentRequest = {
  amountPaid: number
  challanBsrCode?: string | null
  challanNumber?: string | null
  challanDate?: string | null
}

export type CalculateStatutoryInterestRequest = {
  returnDueDate: string
  actualReturnFilingDate: string
  selfAssessmentTaxPaid: number
}

export const advanceTaxApi = {
  getSummary: (clientId?: string, financialYear?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (financialYear) p.set('financialYear', financialYear)
    const qs = p.toString()
    return api<AdvanceTaxSummaryDto>(`/api/advance-tax/summary${qs ? `?${qs}` : ''}`)
  },
  getPlans: (clientId?: string, financialYear?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (financialYear) p.set('financialYear', financialYear)
    const qs = p.toString()
    return api<AdvanceTaxPlanDto[]>(`/api/advance-tax/plans${qs ? `?${qs}` : ''}`)
  },
  getPlanById: (id: string) => api<AdvanceTaxPlanDto>(`/api/advance-tax/plans/${id}`),
  createPlan: (req: SaveAdvanceTaxPlanRequest) =>
    api<AdvanceTaxPlanDto>('/api/advance-tax/plans', { method: 'POST', body: JSON.stringify(req) }),
  updatePlan: (id: string, req: SaveAdvanceTaxPlanRequest) =>
    api<AdvanceTaxPlanDto>(`/api/advance-tax/plans/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deletePlan: (id: string) => api(`/api/advance-tax/plans/${id}`, { method: 'DELETE' }),

  getInstallments: (planId: string) =>
    api<AdvanceTaxInstallmentDto[]>(`/api/advance-tax/plans/${planId}/installments`),
  recordPayment: (installmentId: string, req: RecordInstallmentPaymentRequest) =>
    api<AdvanceTaxInstallmentDto>(`/api/advance-tax/installments/${installmentId}/pay`, { method: 'POST', body: JSON.stringify(req) }),

  calculateInterest: (planId: string, req: CalculateStatutoryInterestRequest) =>
    api<TaxInterestComputationDto>(`/api/advance-tax/plans/${planId}/calculate-interest`, { method: 'POST', body: JSON.stringify(req) }),
  getInterestComputations: (planId: string) =>
    api<TaxInterestComputationDto[]>(`/api/advance-tax/plans/${planId}/interest-computations`),
}

export type Gstr9ReturnDto = {
  id: string
  clientId: string
  clientName: string
  financialYear: string
  gstin: string
  legalName: string
  tradeName?: string | null
  aggregateTurnover: number
  isGstr9Mandatory: boolean
  isGstr9cMandatory: boolean
  exemptionStatus: string
  status: 'Draft' | 'Reconciled' | 'Certified' | 'Filed'
  outwardTaxableSupplies: number
  outwardExemptedSupplies: number
  totalTurnover: number
  itcAvailed3b: number
  itcCategorizedB2b: number
  itcReversed: number
  netItcAvailed: number
  itcAsPer2a: number
  itcReconciledVariance: number
  totalTaxPayable: number
  taxPaidCash: number
  taxPaidItc: number
  shortfallTax: number
  previousFyAmendmentsTax: number
  arn?: string | null
  filedAt?: string | null
  notes?: string | null
  tableEntriesCount: number
  hasGstr9c: boolean
  gstr9cStatus?: 'Draft' | 'UnderReview' | 'Certified' | null
  drc03Pending: boolean
  createdAt: string
}

export type Gstr9TableEntryDto = {
  id: string
  gstr9ReturnId: string
  tableSection: string
  rowCode: string
  description: string
  taxableValue: number
  igstAmount: number
  cgstAmount: number
  sgstAmount: number
  cessAmount: number
  totalTax: number
}

export type Gstr9cDiscrepancyDto = {
  id: string
  gstr9cId: string
  category: string
  tableRef: string
  description: string
  discrepancyAmount: number
  taxImpact: number
  auditorRecommendation?: string | null
  isResolved: boolean
  resolutionNotes?: string | null
  createdAt: string
}

export type Gstr9cReconciliationDto = {
  id: string
  gstr9ReturnId: string
  clientId: string
  financialYear: string
  auditedGrossTurnover: number
  unbilledRevenueStart: number
  unadjustedAdvancesEnd: number
  deemedSupplies: number
  creditNotesAfterFy: number
  tradeDiscountsDisallowed: number
  unbilledRevenueEnd: number
  unadjustedAdvancesStart: number
  otherTurnoverAdjustments: number
  reconciledTurnover: number
  turnoverAsPerGstr9: number
  unreconciledTurnoverVariance: number
  exemptedNilTurnover: number
  zeroRatedWopTurnover: number
  rcmOutwardTurnover: number
  reconciledTaxableTurnover: number
  taxableTurnoverGstr9: number
  unreconciledTaxableVariance: number
  additionalTaxPayable: number
  additionalInterestPayable: number
  additionalPenaltyPayable: number
  totalAdditionalPayable: number
  bookItcTotal: number
  gstr9ItcTotal: number
  unreconciledItcVariance: number
  certificationStatus: 'Draft' | 'UnderReview' | 'Certified'
  auditorName?: string | null
  auditorMembershipNo?: string | null
  auditorFirmRegNo?: string | null
  udin?: string | null
  certifiedAt?: string | null
  auditorRemarks?: string | null
  drc03Arn?: string | null
  drc03Date?: string | null
  drc03PaidAmount: number
  drc03PaymentMode?: string | null
  isDrc03Completed: boolean
  discrepancies: Gstr9cDiscrepancyDto[]
}

export type CompileGstr9ReturnRequest = {
  clientId: string
  financialYear: string
  aggregateTurnover: number
  outwardTaxableSupplies: number
  outwardExemptedSupplies: number
  rcmInwardSupplies: number
  itcAvailed3b: number
  itcCategorizedB2b: number
  itcReversed: number
  itcAsPer2a: number
  itcSubsequentFy: number
  totalTaxPayable: number
  taxPaidCash: number
  taxPaidItc: number
  previousFyAmendmentsTax: number
  notes?: string | null
}

export type UpdateGstr9ReturnRequest = {
  aggregateTurnover: number
  outwardTaxableSupplies: number
  outwardExemptedSupplies: number
  rcmInwardSupplies: number
  itcAvailed3b: number
  itcCategorizedB2b: number
  itcReversed: number
  itcAsPer2a: number
  itcSubsequentFy: number
  totalTaxPayable: number
  taxPaidCash: number
  taxPaidItc: number
  previousFyAmendmentsTax: number
  notes?: string | null
}

export type ReconcileGstr9cRequest = {
  auditedGrossTurnover: number
  unbilledRevenueStart: number
  unadjustedAdvancesEnd: number
  deemedSupplies: number
  creditNotesAfterFy: number
  tradeDiscountsDisallowed: number
  unbilledRevenueEnd: number
  unadjustedAdvancesStart: number
  otherTurnoverAdjustments: number
  exemptedNilTurnover: number
  zeroRatedWopTurnover: number
  rcmOutwardTurnover: number
  additionalTaxPayable: number
  additionalInterestPayable: number
  additionalPenaltyPayable: number
  bookItcTotal: number
  auditorRemarks?: string | null
}

export type AddDiscrepancyRequest = {
  category: string
  tableRef: string
  description: string
  discrepancyAmount: number
  taxImpact: number
  auditorRecommendation?: string | null
}

export type ResolveDiscrepancyRequest = {
  resolutionNotes: string
}

export type CertifyGstr9cRequest = {
  auditorName: string
  auditorMembershipNo: string
  auditorFirmRegNo: string
  udin?: string | null
  auditorRemarks?: string | null
}

export type RecordDrc03PaymentRequest = {
  drc03Arn: string
  drc03Date: string
  drc03PaidAmount: number
  drc03PaymentMode?: string | null
}

export type FileGstr9ReturnRequest = {
  arn: string
  notes?: string | null
}

export type Gstr9SummaryDto = {
  totalReturns: number
  mandatoryGstr9Count: number
  mandatoryGstr9cCount: number
  certifiedGstr9cCount: number
  filedReturnsCount: number
  totalTurnoverReconciled: number
  totalTaxPayable: number
  totalTaxShortfall: number
  totalDrc03Paid: number
}

export const gstr9Api = {
  getReturns: (clientId?: string, financialYear?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (financialYear) p.set('financialYear', financialYear)
    const qs = p.toString()
    return api<Gstr9ReturnDto[]>(`/api/gstr9/returns${qs ? `?${qs}` : ''}`)
  },
  getReturnById: (id: string) => api<Gstr9ReturnDto>(`/api/gstr9/returns/${id}`),
  compileReturn: (req: CompileGstr9ReturnRequest) =>
    api<Gstr9ReturnDto>('/api/gstr9/returns/compile', { method: 'POST', body: JSON.stringify(req) }),
  updateReturn: (id: string, req: UpdateGstr9ReturnRequest) =>
    api<Gstr9ReturnDto>(`/api/gstr9/returns/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  fileReturn: (id: string, req: FileGstr9ReturnRequest) =>
    api<Gstr9ReturnDto>(`/api/gstr9/returns/${id}/file`, { method: 'POST', body: JSON.stringify(req) }),

  getGstr9c: (returnId: string) =>
    api<Gstr9cReconciliationDto>(`/api/gstr9/returns/${returnId}/gstr9c`),
  reconcileGstr9c: (returnId: string, req: ReconcileGstr9cRequest) =>
    api<Gstr9cReconciliationDto>(`/api/gstr9/returns/${returnId}/gstr9c/reconcile`, { method: 'POST', body: JSON.stringify(req) }),
  addDiscrepancy: (returnId: string, req: AddDiscrepancyRequest) =>
    api<Gstr9cDiscrepancyDto>(`/api/gstr9/returns/${returnId}/gstr9c/discrepancies`, { method: 'POST', body: JSON.stringify(req) }),
  resolveDiscrepancy: (id: string, req: ResolveDiscrepancyRequest) =>
    api<Gstr9cDiscrepancyDto>(`/api/gstr9/discrepancies/${id}/resolve`, { method: 'POST', body: JSON.stringify(req) }),
  certifyGstr9c: (returnId: string, req: CertifyGstr9cRequest) =>
    api<Gstr9cReconciliationDto>(`/api/gstr9/returns/${returnId}/gstr9c/certify`, { method: 'POST', body: JSON.stringify(req) }),
  recordDrc03: (returnId: string, req: RecordDrc03PaymentRequest) =>
    api<Gstr9cReconciliationDto>(`/api/gstr9/returns/${returnId}/gstr9c/drc03`, { method: 'POST', body: JSON.stringify(req) }),

  getSummary: (financialYear?: string) => {
    const p = new URLSearchParams()
    if (financialYear) p.set('financialYear', financialYear)
    const qs = p.toString()
    return api<Gstr9SummaryDto>(`/api/gstr9/summary${qs ? `?${qs}` : ''}`)
  }
}

// -------------------------------------------------------------
// Sprint 29: Presumptive Taxation & Minimal Financials (44AD/44ADA/44AE)
// -------------------------------------------------------------

export type Presumptive44AeVehicleDto = {
  id: string
  computationId: string
  registrationNumber: string
  vehicleType: string
  grossVehicleWeightTons: number
  monthsOwned: number
  ratePerMonth: number
  deemedMonthlyProfit: number
  deemedAnnualProfit: number
  declaredProfit: number
  createdAt: string
}

export type PresumptiveTaxComputationDto = {
  id: string
  clientId: string
  clientName?: string | null
  clientPan?: string | null
  financialYear: string
  assessmentYear: string
  section: string
  entityType: string
  businessNatureCode: string
  tradeName: string
  grossTurnover: number
  digitalTurnover: number
  cashTurnover: number
  digitalPercentage: number
  isDigitalThresholdEligible: boolean
  turnoverLimit: number
  isTurnoverWithinLimit: boolean
  deemedProfitDigitalRate: number
  deemedProfitCashRate: number
  minimumDeemedProfit: number
  declaredProfit: number
  optedOutPriorYear: boolean
  optOutAssessmentYear?: string | null
  isLockoutActive: boolean
  lockoutExpiryAy?: string | null
  isAuditMandatory: boolean
  auditTriggerReason?: string | null
  sundryDebtors: number
  sundryCreditors: number
  stockInTrade: number
  cashBalance: number
  bankBalance: number
  fixedAssets: number
  otherAssets: number
  partnerOrProprietorCapital: number
  loansAndBorrowings: number
  status: string
  notes?: string | null
  createdByUserId?: string | null
  createdByName?: string | null
  approvedByUserId?: string | null
  approvedByName?: string | null
  approvedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  vehicles: Presumptive44AeVehicleDto[]
}

export type SavePresumptiveComputationRequest = {
  clientId: string
  financialYear: string
  section: string
  entityType?: string | null
  businessNatureCode?: string | null
  tradeName?: string | null
  grossTurnover: number
  digitalTurnover: number
  cashTurnover: number
  declaredProfit: number
  optedOutPriorYear: boolean
  optOutAssessmentYear?: string | null
  sundryDebtors: number
  sundryCreditors: number
  stockInTrade: number
  cashBalance: number
  bankBalance: number
  fixedAssets: number
  otherAssets: number
  partnerOrProprietorCapital: number
  loansAndBorrowings: number
  notes?: string | null
}

export type SaveVehicleRequest = {
  registrationNumber: string
  vehicleType: string
  grossVehicleWeightTons: number
  monthsOwned: number
  declaredProfit: number
}

export type MinimalFinancialsStatementDto = {
  computationId: string
  clientName: string
  clientPan?: string | null
  financialYear: string
  assessmentYear: string
  section: string
  tradeName: string
  grossTurnover: number
  declaredProfit: number
  totalAssets: number
  totalLiabilitiesAndCapital: number
  difference: number
  isBalanced: boolean
  validationAlerts: string[]
  sundryDebtors: number
  sundryCreditors: number
  stockInTrade: number
  cashBalance: number
  bankBalance: number
  fixedAssets: number
  otherAssets: number
  partnerOrProprietorCapital: number
  loansAndBorrowings: number
}

export type PresumptiveSummaryDto = {
  totalComputations: number
  sec44AdCount: number
  sec44AdaCount: number
  sec44AeCount: number
  auditMandatoryCount: number
  lockoutActiveCount: number
  totalPresumptiveTurnover: number
  totalDeclaredProfit: number
}

export const presumptiveApi = {
  getComputations: (clientId?: string, fy?: string, section?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('fy', fy)
    if (section) p.set('section', section)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<PresumptiveTaxComputationDto[]>(`/api/presumptive/computations${qs ? `?${qs}` : ''}`)
  },
  getComputationById: (id: string) => api<PresumptiveTaxComputationDto>(`/api/presumptive/computations/${id}`),
  createComputation: (req: SavePresumptiveComputationRequest) =>
    api<PresumptiveTaxComputationDto>('/api/presumptive/computations', { method: 'POST', body: JSON.stringify(req) }),
  updateComputation: (id: string, req: SavePresumptiveComputationRequest) =>
    api<PresumptiveTaxComputationDto>(`/api/presumptive/computations/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  recalculate: (id: string) =>
    api<PresumptiveTaxComputationDto>(`/api/presumptive/computations/${id}/recalculate`, { method: 'POST' }),
  addVehicle: (id: string, req: SaveVehicleRequest) =>
    api<PresumptiveTaxComputationDto>(`/api/presumptive/computations/${id}/vehicles`, { method: 'POST', body: JSON.stringify(req) }),
  removeVehicle: (id: string, vehicleId: string) =>
    api<PresumptiveTaxComputationDto>(`/api/presumptive/computations/${id}/vehicles/${vehicleId}`, { method: 'DELETE' }),
  approve: (id: string) =>
    api<PresumptiveTaxComputationDto>(`/api/presumptive/computations/${id}/approve`, { method: 'POST' }),
  getStatement: (id: string) =>
    api<MinimalFinancialsStatementDto>(`/api/presumptive/computations/${id}/statement`),
  getSummary: () =>
    api<PresumptiveSummaryDto>('/api/presumptive/summary')
}







