// API client. The access token lives only in memory (never in storage);
// the refresh token is an HttpOnly cookie the browser sends to /api/auth automatically.

let accessToken: string | null = null
let refreshing: Promise<boolean> | null = null
let onSignedOut: (() => void) | null = null

export function setAccessToken(token: string | null) { accessToken = token }
export function onSessionEnded(handler: () => void) { onSignedOut = handler }

export function errorMessage(err: unknown, fallback = 'Something went wrong.'): string {
  if (err instanceof Error && err.message) return err.message
  if (err && typeof err === 'object' && 'message' in err && typeof (err as { message?: unknown }).message === 'string') {
    return (err as { message: string }).message || fallback
  }
  return fallback
}

export class ApiError extends Error {
  status: number
  errors?: Record<string, string[]>
  /** Server error reference (5xx), e.g. "E-0000ABCD12", also written in the server log. */
  errorId?: string
  constructor(status: number, message: string, errors?: Record<string, string[]>, errorId?: string) {
    super(message)
    this.status = status
    this.errors = errors
    this.errorId = errorId
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
  if (!res.ok) {
    const errorId: string | undefined = typeof body?.errorId === 'string' ? body.errorId : undefined
    const title: string = body?.title ?? res.statusText
    if (res.status >= 500) {
      // the error monitor (shell/monitor.ts) keeps these for crash and problem reports
      window.dispatchEvent(new CustomEvent('cadesk:api-error', { detail: { status: res.status, path, title, errorId } }))
    }
    throw new ApiError(res.status, errorId ? `${title} (reference ${errorId})` : title, body?.errors, errorId)
  }
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
/** A caught value as an Error, so handlers can read .message (and .status / .errors from ApiError) without `any`. */
export type CaughtError = Error & { status?: number; errors?: Record<string, string[]> }
export function toError(e: unknown): CaughtError {
  if (e instanceof Error) return e
  return new Error(typeof e === 'string' ? e : 'Something went wrong.')
}

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
  businessComputation?: BusinessTaxComputationResultDto | null
}

export type PartnerShareInput = {
  name: string
  pan: string
  status: string
  sharePercent: number
  capitalBalance: number
  interestRatePercent: number
  interestPaid: number
  remunerationPaid: number
}

export type KeyPersonInput = {
  name: string
  designation: string
  pan?: string | null
  din?: string | null
}

export type ShareholderInput = {
  name: string
  pan: string
  shareType: string
  numberOfShares: number
  faceValuePerShare: number
  issuePricePerShare: number
  residentialStatus: string
}

export type DepreciationBlockInput = {
  blockKey: string
  label: string
  ratePercent: number
  openingWdv: number
  additionsMoreThan180Days: number
  additionsLessThan180Days: number
  deletions: number
  additionalDepreciation: number
  explicitTaxDepreciation?: number | null
}

export type ChallanTaxPaymentInput = {
  bsrCode: string
  depositDate: string
  challanSerialNo: string
  amount: number
}

export type TdsCreditLineInput = {
  tanOfDeductor: string
  deductorName: string
  tdsSection: string
  grossAmount: number
  tdsClaimed: number
}

export type CalculateBusinessTaxRequest = {
  clientId: string
  assessmentYear: string
  financialYear: string
  entityType: 'firm' | 'company'
  subType: string
  corporateRegime: 'auto' | 'normal' | '115BAA' | '115BAB'
  isSmallTurnover25Percent: boolean
  incorporationDate?: string | null
  isTaxAuditApplicable: boolean
  isIndAsApplicable: boolean
  natureOfBusinessCode: string
  notes?: string | null

  partnerOrShareCapital: number
  authorisedShareCapital: number
  reservesAndSurplus: number
  longTermBorrowings: number
  shortTermBorrowings: number
  deferredTaxLiability: number
  tradePayables: number
  otherCurrentLiabilities: number
  fixedAssetsGrossBlock: number
  fixedAssetsAccumulatedDep: number
  fixedAssetsNetBlock: number
  capitalWip: number
  nonCurrentInvestments: number
  currentInvestments: number
  inventories: number
  tradeReceivables: number
  cashAndBankBalances: number
  loansAndAdvances: number
  otherCurrentAssets: number

  revenueFromOperations: number
  otherIncome: number
  interestIncome: number
  dividendIncome: number
  rentIncome: number
  purchasesAndDirectCost: number
  changesInInventories: number
  employeeBenefitExpense: number
  partnerRemunerationDebited: number
  financeCost: number
  partnerInterestDebited: number
  depreciationPerBooks: number
  auditFee: number
  otherExpenses: number
  taxProvisionCurrent: number
  taxProvisionDeferred: number
  balanceBroughtForwardPl: number

  disallowSec36: number
  disallowSec37: number
  disallowSec40a: number
  disallowSec40A3: number
  disallowSec43B: number
  disallowSec43BhMsme: number
  otherDisallowances: number
  allowableDeductionsUs32Etc: number
  incomeTaxDepreciationSec32: number
  deductionSec10AA: number
  deductionSec35AD: number

  housePropertyIncome: number
  stcg111A: number
  stcgNormal: number
  ltcg112A: number
  ltcg112: number
  otherSourcesIncome: number
  deduction80G: number
  deduction80Jjaa: number
  deduction80M: number
  otherChapViaDeductions: number
  broughtForwardBusinessLoss: number
  unabsorbedDepreciation: number

  matCreditBroughtForward: number
  amtCreditBroughtForward: number
  matAdditionsOther: number
  matDeductionsOther: number

  advanceTaxPaid: number
  tdsCredit: number
  tcsCredit: number
  selfAssessmentTaxPaid: number
  interest234A: number
  interest234B: number
  interest234C: number
  lateFee234F: number

  partners?: PartnerShareInput[] | null
  keyPersons?: KeyPersonInput[] | null
  shareholders?: ShareholderInput[] | null
  depreciationBlocks?: DepreciationBlockInput[] | null
  advanceTaxChallans?: ChallanTaxPaymentInput[] | null
  tdsCreditEntries?: TdsCreditLineInput[] | null
}

export type BusinessRegimeSummaryDto = {
  regimeCode: string
  regimeLabel: string
  taxableIncome: number
  taxAtNormalRates: number
  taxAtSpecialRates: number
  baseTax: number
  surchargeBeforeMarginalRelief: number
  marginalRelief: number
  surcharge: number
  healthAndEducationCess: number
  grossTaxBeforeMatAmt: number
  matOrAmtBaseTax: number
  matOrAmtSurcharge: number
  matOrAmtCess: number
  matOrAmtTotalTax: number
  isMatOrAmtApplied: boolean
  matOrAmtCreditUtilized: number
  matOrAmtCreditGenerated: number
  interestAndFee: number
  totalTaxLiability: number
  prepaidTaxes: number
  netTaxPayableOrRefund: number
  isEligible: boolean
  ineligibilityOrNotes: string
}

export type BusinessTaxComputationResultDto = {
  entityType: string
  subType: string
  assessmentYear: string
  financialYear: string
  totalRevenue: number
  totalExpensesBeforeTax: number
  bookProfitBeforeTax: number
  bookProfitAfterTax: number
  totalDisallowancesAddedBack: number
  firmBookProfitUs40b: number
  maxAllowableRemuneration40bv: number
  allowableRemuneration40b: number
  disallowedRemuneration40b: number
  disallowedPartnerInterest40b: number
  depreciationPerBooks: number
  incomeTaxDepreciationSec32: number
  otherHeadIncomesExcludedFromPl: number
  computedBusinessIncomeBeforeLoss: number
  broughtForwardLossSetOff: number
  netPgbpIncome: number
  housePropertyIncome: number
  capitalGainsTotal: number
  otherSourcesIncome: number
  grossTotalIncome: number
  chapterViaAllowed: number
  totalTaxableIncome: number
  matBookProfit115JB: number
  amtAdjustedTotalIncome115JC: number
  selectedRegimeCode: string
  recommendedRegimeCode: string
  savingsVsAlternative: number
  activeSummary: BusinessRegimeSummaryDto
  alternatives: BusinessRegimeSummaryDto[]
  normalizedInput: CalculateBusinessTaxRequest
}

export type BusinessPrefillResponseDto = {
  prefilledRequest: CalculateBusinessTaxRequest
  sources: { module: string; found: boolean; summary: string }[]
}

export type IncomeTaxJsonExportIssue = {
  severity: 'error' | 'warning'
  path: string
  code: string
  message: string
}

export type IncomeTaxJsonExportResult = {
  formType: string
  schemaVersion: string
  assessmentYear: string
  financialYear: string
  fileName: string
  validAgainstSchema: boolean
  schemaErrorCount: number
  jsonContent: string
  issues: IncomeTaxJsonExportIssue[]
}

export type ExportItrJsonRequest = {
  fatherName?: string | null
  dob?: string | null
  residenceNo?: string | null
  localityOrArea?: string | null
  city?: string | null
  stateCode?: string | null
  pinCode?: string | null
  mobileNo?: string | null
  emailAddress?: string | null
  employerCategory?: string | null
  returnFileSec?: number | null
  place?: string | null
  ifscCode?: string | null
  bankName?: string | null
  bankAccountNo?: string | null
  bankAccountType?: string | null
  intermediaryCity?: string | null
  optOutNewRegime?: boolean | null
  salaryIncome?: number | null
  otherSourcesIncome?: number | null
  chapterVia80C?: number | null
  tdsPaid?: number | null
  advanceTaxPaid?: number | null
  capacity?: string | null
  verificationName?: string | null
  verificationPan?: string | null
  dateOfIncorporation?: string | null
  useIndAs?: boolean | null
  itrFilingDueDate?: string | null
}

export type ExportForm3CdJsonRequest = {
  auditorName?: string | null
  auditorMembershipNo?: string | null
  caFirmName?: string | null
  firmRegNum?: string | null
  auditorAddress?: string | null
  auditorCity?: string | null
  auditorStateCode?: string | null
  auditorPinCode?: string | null
  place?: string | null
  signingDate?: string | null
  auditReportDate?: string | null
  otherLawActCode?: string | null
  assesseeCity?: string | null
  assesseeStateCode?: string | null
  assesseePinCode?: string | null
  numberOfBranches?: number | null
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
  importAis: (req: { clientId: string; aisJson: string }) =>
    api<TaxComputationDto>('/api/tax/computations/import-ais', { method: 'POST', body: JSON.stringify(req) }),
  exportItr1: (id: string, req: ExportItrJsonRequest = {}) =>
    api<IncomeTaxJsonExportResult>(`/api/tax/computations/${id}/export-itr1`, { method: 'POST', body: JSON.stringify(req) }),
  prefillBusiness: (req: { clientId: string; financialYear?: string; assessmentYear?: string; entityType?: string }) =>
    api<BusinessPrefillResponseDto>('/api/tax/business/prefill', { method: 'POST', body: JSON.stringify(req) }),
  previewBusiness: (req: CalculateBusinessTaxRequest) =>
    api<BusinessTaxComputationResultDto>('/api/tax/business/preview', { method: 'POST', body: JSON.stringify(req) }),
  saveBusiness: (req: CalculateBusinessTaxRequest) =>
    api<TaxComputationDto>('/api/tax/business/computations', { method: 'POST', body: JSON.stringify(req) }),
  exportItr5: (id: string, req: ExportItrJsonRequest = {}) =>
    api<IncomeTaxJsonExportResult>(`/api/tax/computations/${id}/export-itr5`, { method: 'POST', body: JSON.stringify(req) }),
  exportItr6: (id: string, req: ExportItrJsonRequest = {}) =>
    api<IncomeTaxJsonExportResult>(`/api/tax/computations/${id}/export-itr6`, { method: 'POST', body: JSON.stringify(req) }),
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

export type LeadScheduleDto = {
  id: string
  engagementId: string
  leadCode: string
  leadTitle: string
  scheduleCategory: string
  unadjustedBookBalance: number
  auditAdjustmentsDebit: number
  auditAdjustmentsCredit: number
  auditedBalance: number
  priorYearBalance: number
  varianceAmount: number
  variancePercentage: number
  isMaterialVariance: boolean
  auditProceduresSummary?: string | null
  exceptionsNoted?: string | null
  status: string
  signedOffByUserId?: string | null
  signedOffByName?: string | null
  signedOffAt?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type UpdateLeadScheduleRequest = {
  auditAdjustmentsDebit: number
  auditAdjustmentsCredit: number
  priorYearBalance: number
  auditProceduresSummary?: string | null
  exceptionsNoted?: string | null
  status: string
  signOff: boolean
}

export type GenerateLeadSchedulesRequest = {
  trialBalanceImportId?: string | null
}

export type DossierIndexSectionDto = {
  indexCode: string
  sectionTitle: string
  saStandard: string
  mandatory: boolean
  status: string
  itemsCount: number
  summaryNotes: string
}

export type AuditDossierDto = {
  id: string
  engagementId: string
  dossierType: string
  version: number
  status: string
  integritySha256: string
  nfraInspectionReady: boolean
  totalWorkingPapersIncluded: number
  leadSchedulesCount: number
  totalExceptionsNoted: number
  totalOpenNotes: number
  partnerSignoffRemarks?: string | null
  indexSections: DossierIndexSectionDto[]
  compiledAt: string
  compiledBy?: string | null
  compiledByName?: string | null
  sealedAt?: string | null
  sealedBy?: string | null
  sealedByName?: string | null
  createdAt: string
}

export type CompileDossierRequest = {
  dossierType?: string
  notes?: string | null
}

export type SealDossierRequest = {
  partnerSignoffRemarks: string
}

export type DossierExportHtmlDto = {
  htmlContent: string
  integritySha256: string
  generatedAt: string
}

// -------------------------------------------------------------
// Sprint 45: SA 505 External Confirmation Types
// -------------------------------------------------------------

export type ConfirmationCampaignDto = {
  id: string
  auditEngagementId: string
  title: string
  description?: string | null
  asOfDate: string
  cutoffDate: string
  status: string
  totalRequestsCount: number
  confirmedAgreedCount: number
  disputedCount: number
  pendingCount: number
  totalBookBalanceAmount: number
  totalConfirmedAmount: number
  varianceAmountTotal: number
  createdAt: string
  updatedAt?: string | null
}

export type ExternalConfirmationRequestDto = {
  id: string
  campaignId: string
  auditEngagementId: string
  leadScheduleRef: string
  partyType: string
  partyName: string
  partyContactEmail: string
  partyContactPhone?: string | null
  partyPanGstin?: string | null
  confirmationType: string
  bookBalanceAmount: number
  currency: string
  asOfDate: string
  secureAccessToken: string
  tokenExpiresAt: string
  status: string
  sentAt?: string | null
  respondedAt?: string | null
  confirmedBalanceAmount?: number | null
  varianceAmount?: number | null
  disputeReason: string
  thirdPartyRemarks?: string | null
  thirdPartySignatoryName?: string | null
  thirdPartyDesignation?: string | null
  alternativeProceduresPerformed?: string | null
  auditorConclusion: string
  proposedAdjustmentAmount?: number | null
  auditorNotes?: string | null
  reviewedBy?: string | null
  reviewedAt?: string | null
  createdAt: string
}

export type CreateConfirmationCampaignRequest = {
  title: string
  description?: string | null
  asOfDate: string
  cutoffDate: string
}

export type CreateConfirmationRequest = {
  leadScheduleRef: string
  partyType: string
  partyName: string
  partyContactEmail: string
  partyContactPhone?: string | null
  partyPanGstin?: string | null
  confirmationType: string
  bookBalanceAmount: number
  asOfDate: string
}

export type AutoPopulateConfirmationsRequest = {
  trialBalanceImportId?: string | null
  minimumDebtorBalance?: number
  minimumCreditorBalance?: number
  topNCount?: number
}

export type SubmitConfirmationResponseRequest = {
  confirmedBalanceAmount: number
  disputeReason: string
  thirdPartyRemarks?: string | null
  thirdPartySignatoryName?: string | null
  thirdPartyDesignation?: string | null
}

export type UpdateConfirmationAuditorEvaluationRequest = {
  status: string
  alternativeProceduresPerformed?: string | null
  auditorConclusion: string
  proposedAdjustmentAmount?: number | null
  auditorNotes?: string | null
}

export type PublicConfirmationDetailsDto = {
  clientName: string
  auditorFirmName: string
  partyName: string
  partyType: string
  confirmationType: string
  bookBalanceAmount?: number | null
  asOfDate: string
  isExpired: boolean
  alreadyResponded: boolean
  currentStatus: string
}

export type ConfirmationSummaryExportHtmlDto = {
  htmlContent: string
  generatedAt: string
}

// -------------------------------------------------------------
// Sprint 46: 1-Click ICAI Independent Auditor's Report & Schedule III Notes
// -------------------------------------------------------------

export type KeyAuditMatterDto = {
  id: string
  reportId: string
  category: string
  title: string
  descriptionOfMatter: string
  whyConsideredSignificant: string
  howAddressedInAudit: string
  keyDocumentationRef?: string | null
}

export type IndependentAuditorReportDto = {
  id: string
  auditEngagementId: string
  reportTitle: string
  opinionType: string
  reportDate: string
  placeOfSignature: string
  auditorFirmName: string
  firmRegistrationNumber: string
  partnerName: string
  partnerMembershipNumber: string
  udin?: string | null
  opinionParagraph: string
  basisForOpinionParagraph: string
  materialUncertaintyGoingConcernParagraph?: string | null
  emphasisOfMatterParagraph?: string | null
  otherMatterParagraph?: string | null
  includeKeyAuditMatters: boolean
  keyAuditMattersIntro?: string | null
  responsibilitiesOfManagementParagraph: string
  auditorResponsibilitiesParagraph: string
  clause143_3_a_InformationObtained: boolean
  clause143_3_b_ProperBooksKept: boolean
  clause143_3_c_AgreementWithBooks: boolean
  clause143_3_d_ComplianceWithAS: boolean
  clause143_3_e_AdverseFunctioningRemarks?: string | null
  clause143_3_f_DirectorsDisqualified: boolean
  clause143_3_g_MaintenanceAccountsQualifications?: string | null
  clause143_3_h_InternalFinancialControlsAnnexure: boolean
  rule11_a_PendingLitigations: string
  rule11_b_ForeseeableLosses: string
  rule11_c_IepfTransfers: string
  rule11_e_IntermediaryFunds: string
  rule11_f_DividendCompliance: string
  rule11_g_AuditTrailStatus: string
  rule11_g_AuditTrailRemarks: string
  hasCaro2020Annexure: boolean
  status: string
  keyAuditMatters: KeyAuditMatterDto[]
  createdAt: string
  updatedAt?: string | null
}

export type GenerateAuditorReportRequest = {
  opinionType?: string
  placeOfSignature?: string
  qualificationDetails?: string | null
  includeKeyAuditMatters?: boolean
  emphasisOfMatter?: string | null
  materialUncertaintyGoingConcern?: string | null
}

export type UpdateAuditorReportRequest = {
  opinionType: string
  placeOfSignature: string
  opinionParagraph: string
  basisForOpinionParagraph: string
  materialUncertaintyGoingConcernParagraph?: string | null
  emphasisOfMatterParagraph?: string | null
  otherMatterParagraph?: string | null
  includeKeyAuditMatters: boolean
  keyAuditMattersIntro?: string | null
  responsibilitiesOfManagementParagraph: string
  auditorResponsibilitiesParagraph: string
  rule11_g_AuditTrailStatus: string
  rule11_g_AuditTrailRemarks: string
  keyAuditMatters?: KeyAuditMatterDto[]
}

export type IssueReportWithUdinRequest = {
  udin: string
  reportDate: string
}

export type AuditorReportHtmlExportDto = {
  htmlContent: string
  opinionType: string
  udin?: string | null
  generatedAt: string
}

export type NotesToAccountsDto = {
  id: string
  auditEngagementId: string
  financialYear: string
  note1CorporateInformation: string
  note2AccountingPolicies: string
  balanceSheetNotesJson: string
  profitAndLossNotesJson: string
  regulatoryRatiosJson: string
  additionalRegulatoryDisclosures: string
  status: string
  createdAt: string
  updatedAt?: string | null
}

export type GenerateNotesToAccountsRequest = {
  trialBalanceImportId?: string | null
}

export type UpdateNotesToAccountsRequest = {
  note1CorporateInformation: string
  note2AccountingPolicies: string
  additionalRegulatoryDisclosures: string
  status: string
}

export type NotesToAccountsHtmlExportDto = {
  htmlContent: string
  generatedAt: string
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

  // Lead Schedules (A to Q) & SA 230
  getLeadSchedules: (engagementId: string) => api<LeadScheduleDto[]>(`/api/audit/engagements/${engagementId}/lead-schedules`),
  generateLeadSchedules: (engagementId: string, req: GenerateLeadSchedulesRequest = {}) => api<LeadScheduleDto[]>(`/api/audit/engagements/${engagementId}/lead-schedules/generate`, { method: 'POST', body: JSON.stringify(req) }),
  updateLeadSchedule: (id: string, req: UpdateLeadScheduleRequest) => api<LeadScheduleDto>(`/api/audit/lead-schedules/${id}`, { method: 'PUT', body: JSON.stringify(req) }),

  // SA 230 / SQC 1 Digital Audit Dossier & NFRA Defense Vault
  getDossier: (engagementId: string) => api<AuditDossierDto>(`/api/audit/engagements/${engagementId}/dossier`),
  compileDossier: (engagementId: string, req: CompileDossierRequest = {}) => api<AuditDossierDto>(`/api/audit/engagements/${engagementId}/dossier/compile`, { method: 'POST', body: JSON.stringify(req) }),
  sealDossier: (dossierId: string, req: SealDossierRequest) => api<AuditDossierDto>(`/api/audit/dossier/${dossierId}/seal`, { method: 'POST', body: JSON.stringify(req) }),
  exportDossierHtml: (engagementId: string) => api<DossierExportHtmlDto>(`/api/audit/engagements/${engagementId}/dossier/export-html`),

  // SA 505 External Confirmations Desk
  getConfirmationCampaigns: (engagementId: string) =>
    api<ConfirmationCampaignDto[]>(`/api/audit/engagements/${engagementId}/confirmation-campaigns`),
  createConfirmationCampaign: (engagementId: string, req: CreateConfirmationCampaignRequest) =>
    api<ConfirmationCampaignDto>(`/api/audit/engagements/${engagementId}/confirmation-campaigns`, { method: 'POST', body: JSON.stringify(req) }),
  getConfirmationRequests: (campaignId: string) =>
    api<ExternalConfirmationRequestDto[]>(`/api/audit/confirmation-campaigns/${campaignId}/requests`),
  createConfirmationRequest: (campaignId: string, req: CreateConfirmationRequest) =>
    api<ExternalConfirmationRequestDto>(`/api/audit/confirmation-campaigns/${campaignId}/requests`, { method: 'POST', body: JSON.stringify(req) }),
  autoPopulateConfirmations: (campaignId: string, req: AutoPopulateConfirmationsRequest = {}) =>
    api<ExternalConfirmationRequestDto[]>(`/api/audit/confirmation-campaigns/${campaignId}/auto-populate`, { method: 'POST', body: JSON.stringify(req) }),
  sendConfirmationRequests: (campaignId: string, requestIds?: string[]) =>
    api<{ sentCount: number }>(`/api/audit/confirmation-campaigns/${campaignId}/send`, { method: 'POST', body: JSON.stringify(requestIds || null) }),
  updateConfirmationEvaluation: (requestId: string, req: UpdateConfirmationAuditorEvaluationRequest) =>
    api<ExternalConfirmationRequestDto>(`/api/audit/confirmations/${requestId}/evaluate`, { method: 'PUT', body: JSON.stringify(req) }),
  exportConfirmationSummaryHtml: (campaignId: string) =>
    api<ConfirmationSummaryExportHtmlDto>(`/api/audit/confirmation-campaigns/${campaignId}/export-summary`),
  getConfirmationLetterHtml: (requestId: string) =>
    fetch(`/api/audit/confirmations/${requestId}/letter-html`, { headers: { Authorization: `Bearer ${localStorage.getItem('token')}` } }).then(r => r.text()),
  getPublicConfirmation: (token: string) =>
    api<PublicConfirmationDetailsDto>(`/api/audit/confirmations/public/${token}`),
  submitPublicConfirmation: (token: string, req: SubmitConfirmationResponseRequest) =>
    api<ExternalConfirmationRequestDto>(`/api/audit/confirmations/public/${token}/respond`, { method: 'POST', body: JSON.stringify(req) }),

  // 1-Click ICAI Independent Auditor's Report (SA 700/701/705/706) & Schedule III Notes (Sprint 46)
  getAuditorReport: (engagementId: string) =>
    api<IndependentAuditorReportDto>(`/api/audit/engagements/${engagementId}/auditor-report`),
  generateAuditorReport: (engagementId: string, req: GenerateAuditorReportRequest = {}) =>
    api<IndependentAuditorReportDto>(`/api/audit/engagements/${engagementId}/auditor-report/generate`, { method: 'POST', body: JSON.stringify(req) }),
  updateAuditorReport: (id: string, req: UpdateAuditorReportRequest) =>
    api<IndependentAuditorReportDto>(`/api/audit/auditor-reports/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  issueReportWithUdin: (id: string, req: IssueReportWithUdinRequest) =>
    api<IndependentAuditorReportDto>(`/api/audit/auditor-reports/${id}/issue-udin`, { method: 'POST', body: JSON.stringify(req) }),
  exportAuditorReportHtml: (engagementId: string) =>
    api<AuditorReportHtmlExportDto>(`/api/audit/engagements/${engagementId}/auditor-report/export-html`),

  getNotesToAccounts: (engagementId: string) =>
    api<NotesToAccountsDto>(`/api/audit/engagements/${engagementId}/notes-to-accounts`),
  generateNotesToAccounts: (engagementId: string, req: GenerateNotesToAccountsRequest = {}) =>
    api<NotesToAccountsDto>(`/api/audit/engagements/${engagementId}/notes-to-accounts/generate`, { method: 'POST', body: JSON.stringify(req) }),
  updateNotesToAccounts: (id: string, req: UpdateNotesToAccountsRequest) =>
    api<NotesToAccountsDto>(`/api/audit/notes-to-accounts/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  exportNotesToAccountsHtml: (engagementId: string) =>
    api<NotesToAccountsHtmlExportDto>(`/api/audit/engagements/${engagementId}/notes-to-accounts/export-html`),
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
  reconcileJson: (req: {
    clientId: string
    returnPeriod: string
    financialYear: string
    gstr2bJson: string
    purchaseInvoices: PurchaseRegisterInvoice[]
    toleranceAmount?: number
    notes?: string
  }) => api<GstReconciliationBatchDto>('/api/gst/reconcile-json', { method: 'POST', body: JSON.stringify(req) }),
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
  isBookEntry?: boolean
  isNil?: boolean
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
  isBookEntry?: boolean
  isNil?: boolean
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
  deducteeCode?: string | null
  remarkCode?: string | null
  remittanceCode?: string | null
  countryCode?: string | null
  taxRateSource?: string | null
  ackNumber?: string | null
  optedOutNewRegime?: boolean | null
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
  customRate?: number | null
  deducteeCode?: string | null
  remarkCode?: string | null
  remittanceCode?: string | null
  countryCode?: string | null
  taxRateSource?: string | null
  ackNumber?: string | null
  optedOutNewRegime?: boolean | null
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

export type TdsDeductorProfileDto = {
  clientId: string
  tan: string
  pan: string
  deductorType: string
  deductorName: string
  branchDivision: string
  gstin?: string | null
  flatBlock: string
  premisesName?: string | null
  streetRoad?: string | null
  areaLocality?: string | null
  cityDistrict?: string | null
  stateCode: string
  pincode: string
  countryName: string
  email: string
  stdCode?: string | null
  phone: string
  mobile: string
  countryCallingCode: string
  respName: string
  respDesignation: string
  respPan: string
  respFlatBlock: string
  respPremisesName?: string | null
  respStreetRoad?: string | null
  respAreaLocality?: string | null
  respCityDistrict?: string | null
  respStateCode: string
  respPincode: string
  respCountryName: string
  respEmail: string
  respMobile: string
  respCountryCallingCode: string
  filedPreviousRegular: boolean
  previousTokenNo?: string | null
}

export type SaveTdsDeductorProfileRequest = {
  tan: string
  pan: string
  deductorType: string
  deductorName: string
  branchDivision?: string | null
  gstin?: string | null
  flatBlock: string
  premisesName?: string | null
  streetRoad?: string | null
  areaLocality?: string | null
  cityDistrict?: string | null
  stateCode: string
  pincode: string
  countryName?: string | null
  email: string
  stdCode?: string | null
  phone?: string | null
  mobile?: string | null
  countryCallingCode?: string | null
  respName: string
  respDesignation: string
  respPan: string
  respFlatBlock: string
  respPremisesName?: string | null
  respStreetRoad?: string | null
  respAreaLocality?: string | null
  respCityDistrict?: string | null
  respStateCode: string
  respPincode: string
  respCountryName?: string | null
  respEmail: string
  respMobile: string
  respCountryCallingCode?: string | null
  filedPreviousRegular?: boolean
  previousTokenNo?: string | null
}

export type ExportTdsReturnRequest = {
  clientId: string
  formType: string
  financialYear: string
  quarter: string
  fileCreationDate?: string | null
  fileSequenceNo?: number
}

export type FvuValidationIssue = {
  severity: 'error' | 'warning'
  recordType: string
  lineNumber?: number | null
  code?: string | null
  message: string
}

export type TdsReturnExportResult = {
  fileName: string
  formType: string
  officialFormNumber: string
  actLabel: string
  financialYear: string
  assessmentYear: string
  quarter: string
  validForFvu: boolean
  lineCount: number
  challanCount: number
  deducteeCount: number
  totalPaid: number
  totalDeducted: number
  totalDeposited: number
  content: string
  issues: FvuValidationIssue[]
}

export type ParsedFvuErrEntry = {
  lineNumber?: number | null
  recordType: string
  challanNo?: number | null
  deducteeNo?: number | null
  errorCode: string
  description: string
}

export type ImportForm16Request = {
  clientId: string
  content: string
  formType?: string | null
  financialYear?: string | null
  quarter?: string | null
  commit?: boolean
}

export type ImportedTdsChallanRow = {
  bsrCode: string
  challanSerialNumber: string
  challanDate: string
  minorHead: string
  taxDeposited: number
  interest: number
  lateFee: number
  totalAmount: number
  isBookEntry: boolean
  alreadyExists: boolean
}

export type ImportedTdsDeductionRow = {
  formType: string
  quarter: string
  financialYear: string
  deducteePan: string
  deducteeName: string
  section: string
  paymentDate: string
  deductionDate: string
  amountPaid: number
  rate: number
  tdsAmount: number
  certificateNo?: string | null
  challanBsr?: string | null
  challanSerial?: string | null
  challanDate?: string | null
}

export type ImportForm16Result = {
  detectedFormat: string
  deductorTan?: string | null
  deductorName?: string | null
  financialYear?: string | null
  quarter?: string | null
  formType?: string | null
  challans: ImportedTdsChallanRow[]
  deductions: ImportedTdsDeductionRow[]
  warnings: string[]
  errors: string[]
  importedChallansCount: number
  importedDeductionsCount: number
  committed: boolean
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

  getProfile: (clientId: string) => api<TdsDeductorProfileDto>(`/api/tds/profiles/${clientId}`),
  saveProfile: (clientId: string, req: SaveTdsDeductorProfileRequest) => api<TdsDeductorProfileDto>(`/api/tds/profiles/${clientId}`, { method: 'PUT', body: JSON.stringify(req) }),
  exportReturn: (req: ExportTdsReturnRequest) => api<TdsReturnExportResult>('/api/tds/returns/export', { method: 'POST', body: JSON.stringify(req) }),
  parseErr: (errContent: string) => api<ParsedFvuErrEntry[]>('/api/tds/returns/parse-err', { method: 'POST', body: JSON.stringify({ errContent }) }),
  importForm16: (req: ImportForm16Request) => api<ImportForm16Result>('/api/tds/import/form16', { method: 'POST', body: JSON.stringify(req) }),
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

export type ForensicAnomalyDto = {
  id: string
  bankAccountId: string
  transactionId?: string | null
  transactionDate: string
  anomalyType: string
  riskLevel: string
  amount: number
  exposureAmount: number
  counterparty: string
  narration: string
  statutorySection: string
  recommendedAction: string
  auditorNotes?: string | null
  status: string
  createdAt: string
}

export type ForensicScanSummaryDto = {
  bankAccountId: string
  accountName: string
  bankName: string
  accountNumber: string
  totalTransactionsScanned: number
  totalAnomaliesCount: number
  sec40A3ExposureAmount: number
  sec269StExposureAmount: number
  sec269SsTExposureAmount: number
  roundTrippingCount: number
  smurfingCount: number
  dormancySurgeCount: number
  highValueRoundSumCount: number
  criticalCount: number
  highCount: number
  mediumCount: number
  overallRiskScore: number
  riskCategory: string
  anomalies: ForensicAnomalyDto[]
  scannedAt: string
}

export type TrialBalanceBankMatchDto = {
  bankAccountId: string
  bankAccountName: string
  bankName: string
  accountNumber: string
  statementClosingBalance: number
  tbLedgerBalance?: number | null
  tbLedgerName?: string | null
  varianceAmount: number
  isUnrecordedAccount: boolean
  isMatched: boolean
}

export type ClientTbBankReconciliationResultDto = {
  clientId: string
  totalBankAccounts: number
  totalTbBankLedgers: number
  matchedAccountsCount: number
  unrecordedAccountsCount: number
  totalBankStatementsBalance: number
  totalTbBankLedgerBalance: number
  netDifference: number
  matches: TrialBalanceBankMatchDto[]
}

export type RunForensicScanRequest = {
  fromDate?: string | null
  toDate?: string | null
  overwriteExisting?: boolean
}

export type UpdateAnomalyStatusRequest = {
  status: string
  auditorNotes?: string | null
}

export type ForensicReportExportDto = {
  htmlContent: string
  overallRiskScore: number
  generatedAt: string
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

  // Forensic Fraud & Statutory Scanners
  runForensicScan: (accountId: string, req: RunForensicScanRequest = {}) =>
    api<ForensicScanSummaryDto>(`/api/bank/accounts/${accountId}/forensic-scan`, { method: 'POST', body: JSON.stringify(req) }),
  getForensicReport: (accountId: string) =>
    api<ForensicScanSummaryDto>(`/api/bank/accounts/${accountId}/forensic-report`),
  updateAnomalyStatus: (anomalyId: string, req: UpdateAnomalyStatusRequest) =>
    api<ForensicAnomalyDto>(`/api/bank/anomalies/${anomalyId}/status`, { method: 'PUT', body: JSON.stringify(req) }),
  reconcileTrialBalance: (clientId: string, trialBalanceImportId?: string) => {
    const p = new URLSearchParams()
    if (trialBalanceImportId) p.set('trialBalanceImportId', trialBalanceImportId)
    const qs = p.toString()
    return api<ClientTbBankReconciliationResultDto>(`/api/bank/clients/${clientId}/reconcile-tb${qs ? `?${qs}` : ''}`, { method: 'POST' })
  },
  exportForensicReportHtml: (accountId: string) =>
    api<ForensicReportExportDto>(`/api/bank/accounts/${accountId}/forensic-report/export-html`),
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
// Sprint 55: CA Copilot: Statutory Citation & Tax Notice Defense Drafter
// ---------------------------------------------------------------------------
export type PrecedentCategory =
  | 'JurisdictionAndLimitation'
  | 'NaturalJusticeAndFairHearing'
  | 'ClericalAndBonaFideError'
  | 'TdsCreditMismatch'
  | 'ReassessmentValiditySec148'
  | 'CashCreditsSec68'
  | 'GstItcBonaFideBuyer'
  | 'GstRule88Discrepancy'
  | 'PenaltyReliefSec270A'
  | 'GeneralPrinciples'

export type DefenseDraftStatus = 'Draft' | 'UnderReview' | 'PartnerApproved' | 'Finalized'

export type StatutoryPrecedentDto = {
  id: string
  firmId?: string | null
  authority: TaxAuthority
  category: PrecedentCategory
  caseTitle: string
  citation: string
  courtBench: string
  judgmentYear: number
  applicableSections: string
  keyRatioDecidendi: string
  defenseGroundTemplate: string
  isLandmark: boolean
  keywords: string
}

export type ParsedNoticeResultDto = {
  authority: TaxAuthority
  category: NoticeCategory
  section: string
  din?: string | null
  dinStatus: DinStatus
  panOrGstin?: string | null
  assessmentYear?: string | null
  financialYear?: string | null
  issueDate?: string | null
  dueDate?: string | null
  demandAmount: number
  officerJurisdiction: string
  extractedIssue: string
  summaryOfAllegations: string
  recommendedPreliminaryObjections: string[]
  recommendedGrounds: string[]
  matchedPrecedents: StatutoryPrecedentDto[]
  confidenceScore: number
}

export type FactualItem = {
  itemNo: number
  eventDate: string
  description: string
  annexureRef: string
}

export type MeritGroundItem = {
  groundNo: number
  title: string
  argument: string
  relevantSection: string
  citedPrecedent?: string | null
}

export type CitedPrecedentItem = {
  precedentId?: string | null
  caseTitle: string
  citation: string
  courtBench: string
  relevantHolding: string
}

export type AnnexureItem = {
  annexureNo: string
  title: string
  documentType: string
  pageCount: number
}

export type TaxDefenseDraftDto = {
  id: string
  noticeId: string
  clientId: string
  clientName: string
  draftNumber: string
  title: string
  subject: string
  addressedTo: string
  clientPanOrGstin: string
  assessmentYear: string
  financialYear: string
  noticeRefNumber: string
  noticeDin: string
  noticeIssueDate: string
  noticeDueDate: string
  preliminaryObjections: string[]
  factualMatrix: FactualItem[]
  meritGrounds: MeritGroundItem[]
  citedPrecedents: CitedPrecedentItem[]
  prayerForRelief: string
  annexureIndex: AnnexureItem[]
  status: DefenseDraftStatus
  preparedByUserId?: string | null
  preparedByName?: string | null
  approvedByUserId?: string | null
  approvedByName?: string | null
  approvedAt?: string | null
  partnerUdin?: string | null
  generatedHtml?: string | null
  createdAt: string
}

export type CreateDefenseDraftRequest = {
  noticeId: string
  title?: string
  subject?: string
  addressedTo?: string
  preliminaryObjections?: string[]
  factualMatrix?: FactualItem[]
  meritGrounds?: MeritGroundItem[]
  citedPrecedents?: CitedPrecedentItem[]
  prayerForRelief?: string
  annexureIndex?: AnnexureItem[]
}

export type UpdateDefenseDraftRequest = {
  title: string
  subject: string
  addressedTo: string
  preliminaryObjections: string[]
  factualMatrix: FactualItem[]
  meritGrounds: MeritGroundItem[]
  citedPrecedents: CitedPrecedentItem[]
  prayerForRelief: string
  annexureIndex: AnnexureItem[]
}

export type ApproveDefenseDraftRequest = {
  partnerUdin?: string
  partnerNotes?: string
}

export type CreatePrecedentRequest = {
  authority: TaxAuthority
  category: PrecedentCategory
  caseTitle: string
  citation: string
  courtBench: string
  judgmentYear: number
  applicableSections: string
  keyRatioDecidendi: string
  defenseGroundTemplate: string
  keywords: string
}

export const taxCopilotApi = {
  parseNotice: (req: { rawText: string; clientId?: string }) =>
    api<ParsedNoticeResultDto>('/api/tax-copilot/parse', { method: 'POST', body: JSON.stringify(req) }),

  getPrecedents: (params?: { authority?: string; category?: string; search?: string }) => {
    const p = new URLSearchParams()
    if (params?.authority) p.set('authority', params.authority)
    if (params?.category) p.set('category', params.category)
    if (params?.search) p.set('search', params.search)
    const qs = p.toString()
    return api<StatutoryPrecedentDto[]>(`/api/tax-copilot/precedents${qs ? `?${qs}` : ''}`)
  },

  createCustomPrecedent: (req: CreatePrecedentRequest) =>
    api<StatutoryPrecedentDto>('/api/tax-copilot/precedents', { method: 'POST', body: JSON.stringify(req) }),

  createDefenseDraft: (req: CreateDefenseDraftRequest) =>
    api<TaxDefenseDraftDto>('/api/tax-copilot/drafts', { method: 'POST', body: JSON.stringify(req) }),

  getDefenseDraft: (id: string) =>
    api<TaxDefenseDraftDto>(`/api/tax-copilot/drafts/${id}`),

  getNoticeDrafts: (noticeId: string) =>
    api<TaxDefenseDraftDto[]>(`/api/tax-copilot/notices/${noticeId}/drafts`),

  updateDefenseDraft: (id: string, req: UpdateDefenseDraftRequest) =>
    api<TaxDefenseDraftDto>(`/api/tax-copilot/drafts/${id}`, { method: 'PUT', body: JSON.stringify(req) }),

  approveDefenseDraft: (id: string, req: ApproveDefenseDraftRequest) =>
    api<TaxDefenseDraftDto>(`/api/tax-copilot/drafts/${id}/approve`, { method: 'POST', body: JSON.stringify(req) }),

  exportHtmlUrl: (id: string) => `/api/tax-copilot/drafts/${id}/export-html`
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
export type AdvanceTaxRegime = 'NewRegime' | 'OldRegime' | number
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
  importAis: (planId: string, aisJson: string) =>
    api<AdvanceTaxPlanDto>(`/api/advance-tax/plans/${planId}/import-ais`, { method: 'POST', body: JSON.stringify({ aisJson }) }),
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
  },
  importGstJson: (req: { clientId?: string; financialYear?: string; gstr9Id?: string; gstr1Json?: string; gstr2bJson?: string }) =>
    api<Gstr9ReturnDto>('/api/gstr9/import-gst-json', { method: 'POST', body: JSON.stringify(req) }),
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
  exportItr4: (id: string, req: ExportItrJsonRequest = {}) =>
    api<IncomeTaxJsonExportResult>(`/api/presumptive/computations/${id}/export-itr4`, { method: 'POST', body: JSON.stringify(req) }),
  getSummary: () =>
    api<PresumptiveSummaryDto>('/api/presumptive/summary')
}

// -------------------------------------------------------------
// Sprint 30: CARO 2020 Automated Audit Checklist & Disclosures Generator
// -------------------------------------------------------------

export type CaroClauseResponseDto = {
  id: string
  assessmentId: string
  clauseNumber: string
  clauseTitle: string
  category: string
  status: string
  auditorRemark: string
  hasDiscrepancy: boolean
  discrepancyAmount?: number | null
  supportingWpsRef?: string | null
  verifiedByUserId?: string | null
  verifiedByName?: string | null
  verifiedAt?: string | null
  updatedAt?: string | null
}

export type CaroAssessmentDto = {
  id: string
  clientId: string
  clientName?: string | null
  engagementId?: string | null
  engagementTitle?: string | null
  financialYear: string
  companyType: string
  isHoldingOrSubsidiaryOfPublic: boolean
  paidUpCapitalAndReserves: number
  totalBorrowings: number
  totalRevenue: number
  isCaroApplicable: boolean
  exemptionReason?: string | null
  status: string
  qualificationsCount: number
  adverseRemarksCount: number
  generatedAnnexureText?: string | null
  auditorConclusion?: string | null
  finalizedByUserId?: string | null
  finalizedByName?: string | null
  finalizedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  clauseResponses: CaroClauseResponseDto[]
}

export type SaveCaroAssessmentRequest = {
  clientId: string
  engagementId?: string | null
  financialYear: string
  companyType: string
  isHoldingOrSubsidiaryOfPublic: boolean
  paidUpCapitalAndReserves: number
  totalBorrowings: number
  totalRevenue: number
  auditorConclusion?: string | null
}

export type UpdateClauseResponseRequest = {
  status: string
  auditorRemark: string
  hasDiscrepancy: boolean
  discrepancyAmount?: number | null
  supportingWpsRef?: string | null
}

export type CaroSummaryDto = {
  totalAssessments: number
  applicableCount: number
  exemptCount: number
  finalizedCount: number
  totalQualifications: number
  totalAdverseRemarks: number
}

export type CaroClauseTemplateDto = {
  clauseNumber: string
  title: string
  category: string
  standardRemark: string
}

export const caroApi = {
  getAssessments: (clientId?: string, fy?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('fy', fy)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<CaroAssessmentDto[]>(`/api/caro/assessments${qs ? `?${qs}` : ''}`)
  },
  getAssessmentById: (id: string) => api<CaroAssessmentDto>(`/api/caro/assessments/${id}`),
  createAssessment: (req: SaveCaroAssessmentRequest) =>
    api<CaroAssessmentDto>('/api/caro/assessments', { method: 'POST', body: JSON.stringify(req) }),
  updateAssessment: (id: string, req: SaveCaroAssessmentRequest) =>
    api<CaroAssessmentDto>(`/api/caro/assessments/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  updateClause: (assessmentId: string, clauseId: string, req: UpdateClauseResponseRequest) =>
    api<CaroAssessmentDto>(`/api/caro/assessments/${assessmentId}/clauses/${clauseId}`, { method: 'PUT', body: JSON.stringify(req) }),
  generateAnnexure: (assessmentId: string) =>
    api<{ annexureText: string }>(`/api/caro/assessments/${assessmentId}/generate-annexure`, { method: 'POST' }),
  finalizeAssessment: (assessmentId: string, conclusion?: string) =>
    api<CaroAssessmentDto>(`/api/caro/assessments/${assessmentId}/finalize`, { method: 'POST', body: JSON.stringify({ conclusion }) }),
  getClauseTemplates: () =>
    api<CaroClauseTemplateDto[]>('/api/caro/clauses-template'),
  getSummary: () =>
    api<CaroSummaryDto>('/api/caro/summary')
}

// -------------------------------------------------------------
// Sprint 31: Form 3CD Tax Audit Statement & Clause Disclosures
// -------------------------------------------------------------

export type Form3CdClauseResponseDto = {
  id: string
  assessmentId: string
  clauseNumber: string
  clauseTitle: string
  category: string
  status: string // Clean, Disallowed, Qualified, NotApplicable
  reportedAmount: number
  auditorRemarks: string
  hasDiscrepancy: boolean
  particularsJson?: string | null
  workpaperRef?: string | null
  verifiedByUserId?: string | null
  verifiedByName?: string | null
  verifiedAt?: string | null
  updatedAt?: string | null
}

export type Form3CdAssessmentDto = {
  id: string
  clientId: string
  clientName?: string | null
  auditEngagementId?: string | null
  auditEngagementTitle?: string | null
  financialYear: string
  assessmentYear: string
  auditType: string // Form3CA, Form3CB
  section44AbClause: string
  totalTurnoverOrGrossReceipts: number
  totalCashReceipts: number
  totalPayments: number
  totalCashPayments: number
  cashReceiptPercentage: number
  cashPaymentPercentage: number
  isTaxAuditMandatory: boolean
  applicabilityReason?: string | null
  assesseeName: string
  pan: string
  address: string
  gstin?: string | null
  status: string
  methodOfAccounting: string
  optedSpecialTaxRegime: string
  totalDisallowanceSec40a: number
  totalDisallowanceSec40A3: number
  totalDisallowanceSec43B: number
  totalDisallowanceSec43Bh: number
  totalDisallowanceSec361va: number
  totalSec269Violations: number
  totalIcdsNetAdjustment: number
  totalNetIncomeAdjustment: number
  assessmentStatus: string // draft, in_review, completed, finalized
  qualificationsCount: number
  discrepanciesCount: number
  auditorObservations?: string | null
  generatedReportText?: string | null
  finalizedByUserId?: string | null
  finalizedByName?: string | null
  finalizedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  clauseResponses: Form3CdClauseResponseDto[]
}

export type SaveForm3CdAssessmentRequest = {
  clientId: string
  auditEngagementId?: string | null
  financialYear: string
  assessmentYear: string
  auditType: string
  section44AbClause?: string | null
  totalTurnoverOrGrossReceipts: number
  totalCashReceipts: number
  totalPayments: number
  totalCashPayments: number
  assesseeName: string
  pan: string
  address: string
  gstin?: string | null
  status: string
  methodOfAccounting: string
  optedSpecialTaxRegime: string
  auditorObservations?: string | null
  declaresLowerPresumptiveProfit: boolean
  is44AdLockout: boolean
  /** professional offering income under 44ADA (s.58 Sl. No. 3) at not less than the deemed 50% */
  optsPresumptive44Ada?: boolean
  isAuditedUnderOtherLaw: boolean
  isBusiness: boolean
}

export type UpdateForm3CdClauseRequest = {
  status: string
  reportedAmount: number
  auditorRemarks: string
  hasDiscrepancy: boolean
  particularsJson?: string | null
  workpaperRef?: string | null
}

export type EvaluateApplicabilityRequest = {
  entityType: string
  isBusiness: boolean
  turnoverOrGrossReceipts: number
  cashReceipts: number
  totalPayments: number
  cashPayments: number
  isAuditedUnderOtherLaw: boolean
  declaresLowerPresumptiveProfit: boolean
  is44AdLockout: boolean
  /** professional offering income under 44ADA (s.58 Sl. No. 3) at not less than the deemed 50% */
  optsPresumptive44Ada?: boolean
  /** "2026-27": decides whether the 1961 or the 2025 Act is cited */
  financialYear?: string
}

export type Form3CdApplicabilityEvaluation = {
  isMandatory: boolean
  recommendedAuditType: string
  applicableClause: string
  cashReceiptPercent: number
  cashPaymentPercent: number
  rationale: string
  statutoryTurnoverThreshold: number
}

export type Form3CdClauseTemplateDto = {
  clauseNumber: string
  clauseTitle: string
  category: string
  description: string
}

export type Form3CdSummaryDto = {
  totalAssessments: number
  mandatoryCount: number
  exemptCount: number
  finalizedCount: number
  totalNetDisallowances: number
  total43BhMsmeDisallowances: number
  total269Violations: number
}

export const taxAuditApi = {
  getSummary: () => api<Form3CdSummaryDto>('/api/taxaudit/summary'),
  getClauseTemplates: () => api<Form3CdClauseTemplateDto[]>('/api/taxaudit/clauses-template'),
  evaluateApplicability: (req: EvaluateApplicabilityRequest) =>
    api<Form3CdApplicabilityEvaluation>('/api/taxaudit/evaluate-applicability', { method: 'POST', body: JSON.stringify(req) }),
  getAssessments: (clientId?: string, fy?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('fy', fy)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<Form3CdAssessmentDto[]>(`/api/taxaudit/assessments${qs ? `?${qs}` : ''}`)
  },
  getAssessmentById: (id: string) => api<Form3CdAssessmentDto>(`/api/taxaudit/assessments/${id}`),
  createAssessment: (req: SaveForm3CdAssessmentRequest) =>
    api<Form3CdAssessmentDto>('/api/taxaudit/assessments', { method: 'POST', body: JSON.stringify(req) }),
  updateAssessment: (id: string, req: SaveForm3CdAssessmentRequest) =>
    api<Form3CdAssessmentDto>(`/api/taxaudit/assessments/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  updateClause: (assessmentId: string, clauseId: string, req: UpdateForm3CdClauseRequest) =>
    api<Form3CdAssessmentDto>(`/api/taxaudit/assessments/${assessmentId}/clauses/${clauseId}`, { method: 'PUT', body: JSON.stringify(req) }),
  generateReport: (id: string) =>
    api<{ reportText: string }>(`/api/taxaudit/assessments/${id}/generate-report`, { method: 'POST' }),
  finalizeAssessment: (id: string, auditorObservations?: string) =>
    api<Form3CdAssessmentDto>(`/api/taxaudit/assessments/${id}/finalize`, { method: 'POST', body: JSON.stringify({ auditorObservations }) }),
  exportJson: (id: string, req: ExportForm3CdJsonRequest = {}) =>
    api<IncomeTaxJsonExportResult>(`/api/taxaudit/assessments/${id}/export-json`, { method: 'POST', body: JSON.stringify(req) }),
}

// -------------------------------------------------------------
// Sprint 32: Schedule III 11 Analytical Ratios & Disclosures
// -------------------------------------------------------------

export type Schedule3RatioItemDto = {
  id: string
  assessmentId: string
  ratioCode: string
  ratioName: string
  numeratorDescription: string
  denominatorDescription: string
  currentYearValue: number
  precedingYearValue: number
  variancePercent: number
  isVarianceSignificant: boolean
  varianceExplanation?: string | null
  isExplanationProvided: boolean
}

export type Schedule3AgeingItemDto = {
  id: string
  assessmentId: string
  scheduleType: string
  particularsCategory: string
  less6MonthsOr1Year: number
  oneToTwoYears: number
  twoToThreeYears: number
  moreThanThreeYears: number
  unbilledDues: number
  total: number
}

export type Schedule3AssessmentDto = {
  id: string
  clientId: string
  clientName?: string | null
  auditEngagementId?: string | null
  auditEngagementTitle?: string | null
  financialYear: string
  precedingFinancialYear: string
  accountingStandard: string
  roundingUnit: string

  totalIncomeCurrent: number
  totalIncomePreceding: number
  currentAssetsCurrent: number
  currentAssetsPreceding: number
  currentLiabilitiesCurrent: number
  currentLiabilitiesPreceding: number
  totalDebtCurrent: number
  totalDebtPreceding: number
  shareholdersEquityCurrent: number
  shareholdersEquityPreceding: number
  netProfitAfterTaxesCurrent: number
  netProfitAfterTaxesPreceding: number
  ebitCurrent: number
  ebitPreceding: number
  debtServiceCurrent: number
  debtServicePreceding: number
  costOfGoodsSoldCurrent: number
  costOfGoodsSoldPreceding: number
  averageInventoryCurrent: number
  averageInventoryPreceding: number
  revenueFromOperationsCurrent: number
  revenueFromOperationsPreceding: number
  averageTradeReceivablesCurrent: number
  averageTradeReceivablesPreceding: number
  netCreditPurchasesCurrent: number
  netCreditPurchasesPreceding: number
  averageTradePayablesCurrent: number
  averageTradePayablesPreceding: number
  capitalEmployedCurrent: number
  capitalEmployedPreceding: number
  incomeFromInvestmentsCurrent: number
  incomeFromInvestmentsPreceding: number
  averageInvestmentsCurrent: number
  averageInvestmentsPreceding: number

  isRoundingRuleCompliant: boolean
  roundingRuleRecommendation?: string | null
  significantVariancesCount: number
  missingExplanationsCount: number
  assessmentStatus: string
  auditorRemarks?: string | null
  generatedDisclosureNoteText?: string | null
  finalizedByUserId?: string | null
  finalizedByName?: string | null
  finalizedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  ratios: Schedule3RatioItemDto[]
  ageingItems: Schedule3AgeingItemDto[]
}

export type SaveSchedule3AssessmentRequest = {
  clientId: string
  auditEngagementId?: string | null
  financialYear: string
  precedingFinancialYear: string
  accountingStandard: string
  roundingUnit: string

  totalIncomeCurrent: number
  totalIncomePreceding: number
  currentAssetsCurrent: number
  currentAssetsPreceding: number
  currentLiabilitiesCurrent: number
  currentLiabilitiesPreceding: number
  totalDebtCurrent: number
  totalDebtPreceding: number
  shareholdersEquityCurrent: number
  shareholdersEquityPreceding: number
  netProfitAfterTaxesCurrent: number
  netProfitAfterTaxesPreceding: number
  ebitCurrent: number
  ebitPreceding: number
  debtServiceCurrent: number
  debtServicePreceding: number
  costOfGoodsSoldCurrent: number
  costOfGoodsSoldPreceding: number
  averageInventoryCurrent: number
  averageInventoryPreceding: number
  revenueFromOperationsCurrent: number
  revenueFromOperationsPreceding: number
  averageTradeReceivablesCurrent: number
  averageTradeReceivablesPreceding: number
  netCreditPurchasesCurrent: number
  netCreditPurchasesPreceding: number
  averageTradePayablesCurrent: number
  averageTradePayablesPreceding: number
  capitalEmployedCurrent: number
  capitalEmployedPreceding: number
  incomeFromInvestmentsCurrent: number
  incomeFromInvestmentsPreceding: number
  averageInvestmentsCurrent: number
  averageInvestmentsPreceding: number
  auditorRemarks?: string | null
}

export type UpdateRatioExplanationRequest = {
  varianceExplanation?: string | null
}

export type UpdateAgeingItemRequest = {
  less6MonthsOr1Year: number
  oneToTwoYears: number
  twoToThreeYears: number
  moreThanThreeYears: number
  unbilledDues: number
}

export type Schedule3SummaryDto = {
  totalAssessments: number
  inReviewCount: number
  finalizedCount: number
  totalSignificantVariances: number
  totalMissingExplanations: number
  roundingCompliantCount: number
}

export const schedule3Api = {
  getSummary: () => api<Schedule3SummaryDto>('/api/schedule3/summary'),
  getAssessments: (clientId?: string, fy?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('fy', fy)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<Schedule3AssessmentDto[]>(`/api/schedule3/assessments${qs ? `?${qs}` : ''}`)
  },
  getAssessmentById: (id: string) => api<Schedule3AssessmentDto>(`/api/schedule3/assessments/${id}`),
  createAssessment: (req: SaveSchedule3AssessmentRequest) =>
    api<Schedule3AssessmentDto>('/api/schedule3/assessments', { method: 'POST', body: JSON.stringify(req) }),
  updateAssessment: (id: string, req: SaveSchedule3AssessmentRequest) =>
    api<Schedule3AssessmentDto>(`/api/schedule3/assessments/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  updateRatioExplanation: (assessmentId: string, ratioId: string, req: UpdateRatioExplanationRequest) =>
    api<Schedule3AssessmentDto>(`/api/schedule3/assessments/${assessmentId}/ratios/${ratioId}`, { method: 'PUT', body: JSON.stringify(req) }),
  updateAgeingItem: (assessmentId: string, ageingId: string, req: UpdateAgeingItemRequest) =>
    api<Schedule3AssessmentDto>(`/api/schedule3/assessments/${assessmentId}/ageing/${ageingId}`, { method: 'PUT', body: JSON.stringify(req) }),
  generateNote: (id: string) =>
    api<{ disclosureNoteText: string }>(`/api/schedule3/assessments/${id}/generate-note`, { method: 'POST' }),
  finalizeAssessment: (id: string, auditorRemarks?: string) =>
    api<Schedule3AssessmentDto>(`/api/schedule3/assessments/${id}/finalize`, { method: 'POST', body: JSON.stringify({ auditorRemarks }) })
}

export type BranchAuditOfficeDto = {
  id: string
  engagementId: string
  branchCode: string
  branchName: string
  branchLocation: string
  isForeignBranch: boolean
  auditorFirmName: string
  auditorFirmRegistrationNumber: string
  auditorName: string
  auditorMembershipNumber: string
  auditorEmail: string
  auditorPhone?: string | null
  hasActiveToken: boolean
  tokenExpiresAt?: string | null
  lastSubmittedAt?: string | null
  totalAssets: number
  totalLiabilities: number
  totalRevenue: number
  totalExpenses: number
  netProfit: number
  branchReportType: string
  hasQualifications: boolean
  qualificationsSummary?: string | null
  branchAuditReportRef?: string | null
  accountingPoliciesConsistent: boolean
  relatedPartyTransactionsDisclosed: boolean
  contingentLiabilitiesReported: boolean
  subsequentEventsReported: boolean
  fraudOrIrregularitiesReported: boolean
  internalControlsCommensurate: boolean
  caroApplicableToBranch: boolean
  caroReportSummary?: string | null
  status: string
  principalAuditorReviewNotes?: string | null
  reviewedByUserId?: string | null
  reviewedByUserName?: string | null
  reviewedAt?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type BranchAuditEngagementDto = {
  id: string
  clientId: string
  clientName?: string | null
  clientPan?: string | null
  auditEngagementId?: string | null
  auditEngagementTitle?: string | null
  financialYear: string
  title: string
  headOfficeAssets: number
  headOfficeLiabilities: number
  headOfficeRevenue: number
  headOfficeExpenses: number
  headOfficeNetProfit: number
  totalConsolidatedAssets: number
  totalConsolidatedLiabilities: number
  totalConsolidatedRevenue: number
  totalConsolidatedExpenses: number
  totalConsolidatedNetProfit: number
  branchesAuditedCount: number
  branchAuditorsCount: number
  totalBranchAssets: number
  branchAssetsPercentage: number
  totalBranchRevenue: number
  branchRevenuePercentage: number
  totalBranchNetProfit: number
  branchNetProfitPercentage: number
  consolidationStatus: string
  branchQualificationsCount: number
  principalAuditorNotes?: string | null
  generatedSa600ReportText?: string | null
  finalizedByUserId?: string | null
  finalizedByUserName?: string | null
  finalizedAt?: string | null
  createdAt: string
  updatedAt?: string | null
  branches: BranchAuditOfficeDto[]
}

export type SaveBranchEngagementRequest = {
  clientId: string
  auditEngagementId?: string | null
  financialYear: string
  title: string
  headOfficeAssets: number
  headOfficeLiabilities: number
  headOfficeRevenue: number
  headOfficeExpenses: number
  headOfficeNetProfit: number
  principalAuditorNotes?: string | null
}

export type SaveBranchOfficeRequest = {
  branchCode: string
  branchName: string
  branchLocation: string
  isForeignBranch: boolean
  auditorFirmName: string
  auditorFirmRegistrationNumber: string
  auditorName: string
  auditorMembershipNumber: string
  auditorEmail: string
  auditorPhone?: string | null
  totalAssets: number
  totalLiabilities: number
  totalRevenue: number
  totalExpenses: number
  netProfit: number
  branchReportType: string
  hasQualifications: boolean
  qualificationsSummary?: string | null
  branchAuditReportRef?: string | null
  accountingPoliciesConsistent: boolean
  relatedPartyTransactionsDisclosed: boolean
  contingentLiabilitiesReported: boolean
  subsequentEventsReported: boolean
  fraudOrIrregularitiesReported: boolean
  internalControlsCommensurate: boolean
  caroApplicableToBranch: boolean
  caroReportSummary?: string | null
}

export type SubmitBranchReturnsRequest = {
  token: string
  totalAssets: number
  totalLiabilities: number
  totalRevenue: number
  totalExpenses: number
  netProfit: number
  branchReportType: string
  hasQualifications: boolean
  qualificationsSummary?: string | null
  branchAuditReportRef?: string | null
  accountingPoliciesConsistent: boolean
  relatedPartyTransactionsDisclosed: boolean
  contingentLiabilitiesReported: boolean
  subsequentEventsReported: boolean
  fraudOrIrregularitiesReported: boolean
  internalControlsCommensurate: boolean
  caroApplicableToBranch: boolean
  caroReportSummary?: string | null
}

export type ReviewBranchOfficeRequest = {
  decision: string
  reviewNotes?: string | null
}

export type GenerateTokenResponse = {
  branchId: string
  rawToken: string
  expiresAt: string
  portalUrl: string
}

export type BranchAuditSummaryDto = {
  totalEngagements: number
  consolidatedCount: number
  finalizedCount: number
  totalBranches: number
  branchesSubmitted: number
  branchesAccepted: number
  totalBranchQualifications: number
  totalBranchAssetsAudited: number
  averageBranchAssetsPercentage: number
}

export const branchAuditApi = {
  getSummary: () => api<BranchAuditSummaryDto>('/api/branchaudit/summary'),
  getEngagements: (clientId?: string, fy?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('fy', fy)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<BranchAuditEngagementDto[]>(`/api/branchaudit/engagements${qs ? `?${qs}` : ''}`)
  },
  getEngagementById: (id: string) => api<BranchAuditEngagementDto>(`/api/branchaudit/engagements/${id}`),
  createEngagement: (req: SaveBranchEngagementRequest) =>
    api<BranchAuditEngagementDto>('/api/branchaudit/engagements', { method: 'POST', body: JSON.stringify(req) }),
  updateEngagement: (id: string, req: SaveBranchEngagementRequest) =>
    api<BranchAuditEngagementDto>(`/api/branchaudit/engagements/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteEngagement: (id: string) =>
    api<void>(`/api/branchaudit/engagements/${id}`, { method: 'DELETE' }),
  addBranch: (engagementId: string, req: SaveBranchOfficeRequest) =>
    api<BranchAuditOfficeDto>(`/api/branchaudit/engagements/${engagementId}/branches`, { method: 'POST', body: JSON.stringify(req) }),
  updateBranch: (engagementId: string, branchId: string, req: SaveBranchOfficeRequest) =>
    api<BranchAuditOfficeDto>(`/api/branchaudit/engagements/${engagementId}/branches/${branchId}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteBranch: (engagementId: string, branchId: string) =>
    api<void>(`/api/branchaudit/engagements/${engagementId}/branches/${branchId}`, { method: 'DELETE' }),
  generateToken: (engagementId: string, branchId: string) =>
    api<GenerateTokenResponse>(`/api/branchaudit/engagements/${engagementId}/branches/${branchId}/generate-token`, { method: 'POST' }),
  getPortalBranchInfo: (token: string) =>
    api<BranchAuditOfficeDto>(`/api/branchaudit/portal/branch?token=${encodeURIComponent(token)}`),
  submitBranchReturns: (req: SubmitBranchReturnsRequest) =>
    api<BranchAuditOfficeDto>('/api/branchaudit/portal/submit', { method: 'POST', body: JSON.stringify(req) }),
  reviewBranch: (engagementId: string, branchId: string, req: ReviewBranchOfficeRequest) =>
    api<BranchAuditOfficeDto>(`/api/branchaudit/engagements/${engagementId}/branches/${branchId}/review`, { method: 'POST', body: JSON.stringify(req) }),
  consolidateEngagement: (engagementId: string) =>
    api<BranchAuditEngagementDto>(`/api/branchaudit/engagements/${engagementId}/consolidate`, { method: 'POST' }),
  finalizeEngagement: (engagementId: string) =>
    api<BranchAuditEngagementDto>(`/api/branchaudit/engagements/${engagementId}/finalize`, { method: 'POST' })
}

export type InternalControlDeficiencyDto = {
  id: string
  committeeEngagementId: string
  deficiencyCode: string
  title: string
  processArea: string
  deficiencyType: string
  severity: string
  description: string
  potentialFinancialEffect: string
  estimatedRiskAmount?: number | null
  auditorRecommendation: string
  managementResponse?: string | null
  remediationTargetDate?: string | null
  status: string
  createdAt: string
  updatedAt?: string | null
}

export type SignificantAuditMatterDto = {
  id: string
  committeeEngagementId: string
  matterCode: string
  subject: string
  category: string
  details: string
  auditorAssessment: string
  auditCommitteeFeedback?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type UncorrectedMisstatementDto = {
  id: string
  committeeEngagementId: string
  accountDescription: string
  misstatementAmount: number
  nature: string
  isMaterial: boolean
  managementJustificationForNonAdjustment?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type AuditCommitteeEngagementDto = {
  id: string
  clientId: string
  clientName?: string | null
  clientPan?: string | null
  auditEngagementId?: string | null
  auditEngagementTitle?: string | null
  financialYear: string
  title: string
  meetingType: string
  meetingDate?: string | null
  meetingLocation?: string | null
  committeeChairName: string
  leadPartnerName: string
  status: string
  meetingMinutes?: string | null
  generatedGovernancePackText?: string | null
  generatedSa265LetterText?: string | null
  signedOffByUserId?: string | null
  signedOffByUserName?: string | null
  signedOffAt?: string | null
  createdAt: string
  updatedAt?: string | null
  totalDeficienciesCount: number
  significantDeficienciesCount: number
  totalEstimatedRiskAmount: number
  significantMattersCount: number
  uncorrectedMisstatementsCount: number
  totalUncorrectedMisstatementAmount: number
  deficiencies: InternalControlDeficiencyDto[]
  significantMatters: SignificantAuditMatterDto[]
  uncorrectedMisstatements: UncorrectedMisstatementDto[]
}

export type SaveCommitteeEngagementRequest = {
  clientId: string
  auditEngagementId?: string | null
  financialYear: string
  title: string
  meetingType: string
  meetingDate?: string | null
  meetingLocation?: string | null
  committeeChairName: string
  leadPartnerName: string
  meetingMinutes?: string | null
}

export type SaveDeficiencyRequest = {
  deficiencyCode: string
  title: string
  processArea: string
  deficiencyType: string
  severity: string
  description: string
  potentialFinancialEffect: string
  estimatedRiskAmount?: number | null
  auditorRecommendation: string
  managementResponse?: string | null
  remediationTargetDate?: string | null
  status: string
}

export type SaveSignificantMatterRequest = {
  matterCode: string
  subject: string
  category: string
  details: string
  auditorAssessment: string
  auditCommitteeFeedback?: string | null
}

export type SaveUncorrectedMisstatementRequest = {
  accountDescription: string
  misstatementAmount: number
  nature: string
  isMaterial: boolean
  managementJustificationForNonAdjustment?: string | null
}

export type SignOffCommitteeEngagementRequest = {
  minutes?: string | null
}

export type AuditCommitteeSummaryDto = {
  totalEngagements: number
  packGeneratedCount: number
  signedOffCount: number
  totalDeficiencies: number
  totalSignificantDeficiencies: number
  totalEstimatedRiskAmount: number
  totalSignificantMatters: number
  totalUncorrectedMisstatements: number
  totalUncorrectedMisstatementValue: number
}

export const auditCommitteeApi = {
  getSummary: () => api<AuditCommitteeSummaryDto>('/api/auditcommittee/summary'),
  getEngagements: (clientId?: string, fy?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('fy', fy)
    if (status) p.set('status', status)
    const qs = p.toString()
    return api<AuditCommitteeEngagementDto[]>(`/api/auditcommittee/engagements${qs ? `?${qs}` : ''}`)
  },
  getEngagementById: (id: string) => api<AuditCommitteeEngagementDto>(`/api/auditcommittee/engagements/${id}`),
  createEngagement: (req: SaveCommitteeEngagementRequest) =>
    api<AuditCommitteeEngagementDto>('/api/auditcommittee/engagements', { method: 'POST', body: JSON.stringify(req) }),
  updateEngagement: (id: string, req: SaveCommitteeEngagementRequest) =>
    api<AuditCommitteeEngagementDto>(`/api/auditcommittee/engagements/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteEngagement: (id: string) =>
    api<void>(`/api/auditcommittee/engagements/${id}`, { method: 'DELETE' }),
  addDeficiency: (engagementId: string, req: SaveDeficiencyRequest) =>
    api<InternalControlDeficiencyDto>(`/api/auditcommittee/engagements/${engagementId}/deficiencies`, { method: 'POST', body: JSON.stringify(req) }),
  updateDeficiency: (engagementId: string, deficiencyId: string, req: SaveDeficiencyRequest) =>
    api<InternalControlDeficiencyDto>(`/api/auditcommittee/engagements/${engagementId}/deficiencies/${deficiencyId}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteDeficiency: (engagementId: string, deficiencyId: string) =>
    api<void>(`/api/auditcommittee/engagements/${engagementId}/deficiencies/${deficiencyId}`, { method: 'DELETE' }),
  addMatter: (engagementId: string, req: SaveSignificantMatterRequest) =>
    api<SignificantAuditMatterDto>(`/api/auditcommittee/engagements/${engagementId}/matters`, { method: 'POST', body: JSON.stringify(req) }),
  updateMatter: (engagementId: string, matterId: string, req: SaveSignificantMatterRequest) =>
    api<SignificantAuditMatterDto>(`/api/auditcommittee/engagements/${engagementId}/matters/${matterId}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteMatter: (engagementId: string, matterId: string) =>
    api<void>(`/api/auditcommittee/engagements/${engagementId}/matters/${matterId}`, { method: 'DELETE' }),
  addMisstatement: (engagementId: string, req: SaveUncorrectedMisstatementRequest) =>
    api<UncorrectedMisstatementDto>(`/api/auditcommittee/engagements/${engagementId}/misstatements`, { method: 'POST', body: JSON.stringify(req) }),
  updateMisstatement: (engagementId: string, misstatementId: string, req: SaveUncorrectedMisstatementRequest) =>
    api<UncorrectedMisstatementDto>(`/api/auditcommittee/engagements/${engagementId}/misstatements/${misstatementId}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteMisstatement: (engagementId: string, misstatementId: string) =>
    api<void>(`/api/auditcommittee/engagements/${engagementId}/misstatements/${misstatementId}`, { method: 'DELETE' }),
  generatePack: (engagementId: string) =>
    api<AuditCommitteeEngagementDto>(`/api/auditcommittee/engagements/${engagementId}/generate-pack`, { method: 'POST' }),
  signOff: (engagementId: string, req: SignOffCommitteeEngagementRequest) =>
    api<AuditCommitteeEngagementDto>(`/api/auditcommittee/engagements/${engagementId}/signoff`, { method: 'POST', body: JSON.stringify(req) })
}

// ----------------------------------------------------------------------
// Foreign Remittance & Form 15CA / 15CB Certification Engine (Sprint 35)
// ----------------------------------------------------------------------

export interface ForeignRemittanceChecklistItemDto {
  id: string
  assessmentId: string
  itemCode: string
  itemDescription: string
  isMandatory: boolean
  status: 'Pending' | 'Verified' | 'Rejected' | 'Waived'
  verificationNotes: string
  verifiedAt?: string
  createdAt: string
  updatedAt?: string
}

export interface ForeignRemittanceAssessmentDto {
  id: string
  clientId: string
  clientName?: string
  clientPan?: string
  financialYear: string
  remittanceReference: string
  remitterPan: string
  remitterTan: string
  remitterName: string
  remitterStatus: string
  remitterAddress: string
  remitterEmail: string
  remitterPhone: string
  beneficiaryName: string
  beneficiaryCountry: string
  beneficiaryCountryCode: string
  beneficiaryAddress: string
  beneficiaryTaxId: string
  beneficiaryEntityType: string
  remittanceCurrency: string
  foreignAmount: number
  exchangeRate: number
  inrEquivalent: number
  proposedRemittanceDate?: string
  rbiPurposeCode: string
  purposeDescription: string
  natureOfRemittance: string
  contractDescription: string
  invoiceNumber: string
  invoiceDate?: string
  isChargeableToTax: boolean
  chargeabilityReason: string
  domesticSection: string
  domesticTaxRate: number
  surchargeRate: number
  cessRate: number
  effectiveDomesticRate: number
  treatyBenefitClaimed: boolean
  treatyCountry: string
  treatyArticle: string
  treatyTaxRate: number
  hasTrc: boolean
  trcPeriodFrom?: string
  trcPeriodTo?: string
  hasForm10F: boolean
  noPeDeclaration: boolean
  beneficialOwnershipConfirmed: boolean
  mliPptPassed: boolean
  applicableRateType: string
  finalWithholdingRate: number
  taxBorneByRemitter: boolean
  grossedUpAmountInr: number
  tdsAmountInr: number
  tdsAmountFc: number
  netRemittanceFc: number
  netRemittanceInr: number
  form15CaPart: 'PartA' | 'PartB' | 'PartC' | 'PartD' | 'ExemptRule37BB'
  form15CaRoutingReason: string
  aoOrderSection?: string
  aoOrderNumber?: string
  aoOrderDate?: string
  aoCertificateRate?: number
  requiresForm15Cb: boolean
  caMembershipNumber: string
  caName: string
  caFirmName: string
  caFrn: string
  udin: string
  certifiedAt?: string
  certifierUserId?: string
  generated15CbCertificateText?: string
  generated15CaXmlPayload?: string
  acknowledgementNumber?: string
  status: 'Draft' | 'Assessed' | 'Certified' | 'Filed'
  checklist: ForeignRemittanceChecklistItemDto[]
  createdAt: string
  updatedAt?: string
}

export interface ForeignRemittanceSummaryDto {
  totalAssessments: number
  totalRemittanceInr: number
  totalTdsInr: number
  draftCount: number
  certifiedCount: number
  filedCount: number
  partACount: number
  partBCount: number
  partCCount: number
  partDCount: number
  exemptCount: number
}

export interface CreateForeignRemittanceRequest {
  clientId: string
  financialYear: string
  remittanceReference?: string
  remitterPan: string
  remitterTan?: string
  remitterName: string
  remitterStatus?: string
  remitterAddress?: string
  remitterEmail?: string
  remitterPhone?: string
  beneficiaryName: string
  beneficiaryCountry: string
  beneficiaryCountryCode?: string
  beneficiaryAddress?: string
  beneficiaryTaxId?: string
  beneficiaryEntityType?: string
  remittanceCurrency: string
  foreignAmount: number
  exchangeRate: number
  proposedRemittanceDate?: string
  rbiPurposeCode?: string
  purposeDescription?: string
  natureOfRemittance?: string
  contractDescription?: string
  invoiceNumber?: string
  invoiceDate?: string
  isChargeableToTax: boolean
  chargeabilityReason?: string
  domesticSection?: string
  domesticTaxRate: number
  surchargeRate: number
  cessRate: number
  treatyBenefitClaimed: boolean
  treatyCountry?: string
  treatyArticle?: string
  treatyTaxRate: number
  hasTrc: boolean
  trcPeriodFrom?: string
  trcPeriodTo?: string
  hasForm10F: boolean
  noPeDeclaration: boolean
  beneficialOwnershipConfirmed: boolean
  mliPptPassed: boolean
  taxBorneByRemitter: boolean
  aoOrderSection?: string
  aoOrderNumber?: string
  aoOrderDate?: string
  aoCertificateRate?: number
}

export interface UpdateForeignRemittanceRequest {
  remittanceReference?: string
  remitterPan: string
  remitterTan?: string
  remitterName: string
  remitterStatus?: string
  remitterAddress?: string
  remitterEmail?: string
  remitterPhone?: string
  beneficiaryName: string
  beneficiaryCountry: string
  beneficiaryCountryCode?: string
  beneficiaryAddress?: string
  beneficiaryTaxId?: string
  beneficiaryEntityType?: string
  remittanceCurrency: string
  foreignAmount: number
  exchangeRate: number
  proposedRemittanceDate?: string
  rbiPurposeCode?: string
  purposeDescription?: string
  natureOfRemittance?: string
  contractDescription?: string
  invoiceNumber?: string
  invoiceDate?: string
  isChargeableToTax: boolean
  chargeabilityReason?: string
  domesticSection?: string
  domesticTaxRate: number
  surchargeRate: number
  cessRate: number
  treatyBenefitClaimed: boolean
  treatyCountry?: string
  treatyArticle?: string
  treatyTaxRate: number
  hasTrc: boolean
  trcPeriodFrom?: string
  trcPeriodTo?: string
  hasForm10F: boolean
  noPeDeclaration: boolean
  beneficialOwnershipConfirmed: boolean
  mliPptPassed: boolean
  taxBorneByRemitter: boolean
  aoOrderSection?: string
  aoOrderNumber?: string
  aoOrderDate?: string
  aoCertificateRate?: number
}

export interface UpdateChecklistItemRequest {
  status: string
  verificationNotes?: string
}

export interface CertifyRemittanceRequest {
  caMembershipNumber: string
  caName: string
  caFirmName: string
  caFrn: string
  udin: string
}

export interface FileRemittanceRequest {
  acknowledgementNumber: string
}

export const foreignRemittanceApi = {
  getSummary: (fy?: string) =>
    api<ForeignRemittanceSummaryDto>(`/api/foreign-remittance/summary${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`),
  list: (clientId?: string, fy?: string, status?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (fy) params.set('fy', fy)
    if (status) params.set('status', status)
    const qs = params.toString()
    return api<ForeignRemittanceAssessmentDto[]>(`/api/foreign-remittance/assessments${qs ? `?${qs}` : ''}`)
  },
  get: (id: string) =>
    api<ForeignRemittanceAssessmentDto>(`/api/foreign-remittance/assessments/${id}`),
  create: (req: CreateForeignRemittanceRequest) =>
    api<ForeignRemittanceAssessmentDto>('/api/foreign-remittance/assessments', { method: 'POST', body: JSON.stringify(req) }),
  update: (id: string, req: UpdateForeignRemittanceRequest) =>
    api<ForeignRemittanceAssessmentDto>(`/api/foreign-remittance/assessments/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  delete: (id: string) =>
    api<void>(`/api/foreign-remittance/assessments/${id}`, { method: 'DELETE' }),
  updateChecklist: (assessmentId: string, itemId: string, req: UpdateChecklistItemRequest) =>
    api<ForeignRemittanceChecklistItemDto>(`/api/foreign-remittance/assessments/${assessmentId}/checklist/${itemId}`, { method: 'PUT', body: JSON.stringify(req) }),
  generateArtifacts: (id: string) =>
    api<ForeignRemittanceAssessmentDto>(`/api/foreign-remittance/assessments/${id}/generate-artifacts`, { method: 'POST' }),
  certify: (id: string, req: CertifyRemittanceRequest) =>
    api<ForeignRemittanceAssessmentDto>(`/api/foreign-remittance/assessments/${id}/certify`, { method: 'POST', body: JSON.stringify(req) }),
  file: (id: string, req: FileRemittanceRequest) =>
    api<ForeignRemittanceAssessmentDto>(`/api/foreign-remittance/assessments/${id}/file`, { method: 'POST', body: JSON.stringify(req) }),

  // SPRINT 62: FEMA FIRMS PORTAL API
  getFirmsSummary: () =>
    api<FemaFirmsSummaryDto>('/api/foreign-remittance/firms/summary'),
  getFirmsFilings: (clientId?: string, type?: FirmsFilingType) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (type) params.set('type', type)
    const qs = params.toString()
    return api<FemaFirmsFilingDto[]>(`/api/foreign-remittance/firms/filings${qs ? `?${qs}` : ''}`)
  },
  getFirmsFiling: (id: string) =>
    api<FemaFirmsFilingDto>(`/api/foreign-remittance/firms/filings/${id}`),
  saveFirmsFiling: (req: SaveFemaFirmsFilingRequest) =>
    api<FemaFirmsFilingDto>('/api/foreign-remittance/firms/filings', { method: 'POST', body: JSON.stringify(req) }),
  certifyFirmsFiling: (id: string, req: CertifyFemaFirmsRequest) =>
    api<FemaFirmsFilingDto>(`/api/foreign-remittance/firms/filings/${id}/certify`, { method: 'POST', body: JSON.stringify(req) }),
  deleteFirmsFiling: (id: string) =>
    api<void>(`/api/foreign-remittance/firms/filings/${id}`, { method: 'DELETE' }),
  getFirmsCertificateHtmlUrl: (id: string) =>
    `/api/foreign-remittance/firms/filings/${id}/certificate-html`,

  // SPRINT 62: RBI ANNUAL FLA RETURN API
  getFlaSummary: (fy?: string) =>
    api<RbiFlaSummaryDto>(`/api/foreign-remittance/fla/summary${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`),
  getFlaReturns: (clientId?: string, fy?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (fy) params.set('fy', fy)
    const qs = params.toString()
    return api<RbiAnnualFlaReturnDto[]>(`/api/foreign-remittance/fla/returns${qs ? `?${qs}` : ''}`)
  },
  getFlaReturn: (id: string) =>
    api<RbiAnnualFlaReturnDto>(`/api/foreign-remittance/fla/returns/${id}`),
  saveFlaReturn: (req: SaveRbiAnnualFlaReturnRequest) =>
    api<RbiAnnualFlaReturnDto>('/api/foreign-remittance/fla/returns', { method: 'POST', body: JSON.stringify(req) }),
  certifyFlaReturn: (id: string, req: CertifyRbiFlaRequest) =>
    api<RbiAnnualFlaReturnDto>(`/api/foreign-remittance/fla/returns/${id}/certify`, { method: 'POST', body: JSON.stringify(req) }),
  fileFlaReturn: (id: string, req: FileRbiFlaRequest) =>
    api<RbiAnnualFlaReturnDto>(`/api/foreign-remittance/fla/returns/${id}/file`, { method: 'POST', body: JSON.stringify(req) }),
  deleteFlaReturn: (id: string) =>
    api<void>(`/api/foreign-remittance/fla/returns/${id}`, { method: 'DELETE' }),
  getFlaReportHtmlUrl: (id: string) =>
    `/api/foreign-remittance/fla/returns/${id}/report-html`,

  // SPRINT 62: DTAA MFN & PE EVALUATION API
  evaluateMfn: (req: DtaaMfnEvaluationRequest) =>
    api<DtaaMfnEvaluationDto>('/api/foreign-remittance/evaluate-mfn', { method: 'POST', body: JSON.stringify(req) }),
  evaluatePe: (req: PeExposureEvaluationRequest) =>
    api<PeExposureEvaluationDto>('/api/foreign-remittance/evaluate-pe', { method: 'POST', body: JSON.stringify(req) })
}

// -----------------------------------------------------------------------------
// SPRINT 62: FEMA FIRMS & RBI ANNUAL FLA TYPES
// -----------------------------------------------------------------------------

export type FirmsFilingType = 'FcGpr' | 'FcTrs'
export type CapitalInstrumentType = 'EquityShares' | 'CCDs' | 'CCPS' | 'ShareWarrants'
export type FdiEntryRoute = 'AutomaticRoute' | 'GovernmentApprovalRoute'
export type TransferDirectionType = 'ResidentToNonResident' | 'NonResidentToResident' | 'NonResidentToNonResident'
export type TransferModeType = 'Sale' | 'Gift' | 'Swap' | 'Merger'
export type FirmsFilingStatus = 'Draft' | 'Validated' | 'Certified' | 'SubmittedToAdBank' | 'ApprovedByRbi'
export type FlaAuditStatus = 'Audited' | 'UnauditedProvisional'
export type FlaReturnStatus = 'Draft' | 'Validated' | 'Certified' | 'SubmittedToRbiFlair'

export interface FemaFirmsFilingDto {
  id: string
  clientId: string
  clientName?: string
  clientPan?: string
  filingType: FirmsFilingType
  referenceNumber: string
  indianEntityCin: string
  indianEntityName: string
  pan: string
  nonResidentPartyName: string
  nonResidentCountry: string
  nonResidentCountryCode: string
  instrumentType: CapitalInstrumentType
  numberOfInstruments: number
  faceValuePerInstrument: number
  issueOrTransferPricePerInstrument: number
  fairValuePerInstrument: number
  totalConsiderationInr: number
  totalConsiderationFc: number
  currency: string
  exchangeRate: number
  valuationMethod: string
  valuerName: string
  valuationReportDate?: string
  dateOfAllotmentOrTransfer: string
  statutoryFilingDeadline: string
  daysElapsed: number
  daysRemaining: number
  isLateSubmission: boolean
  lateSubmissionFeeEstimated: number
  fdiRoute: FdiEntryRoute
  entryRouteApprovalRef?: string
  sectoralCapPct: number
  postAllotmentForeignShareholdingPct: number
  transferDirection: TransferDirectionType
  transferMode: TransferModeType
  buyerAdBankName?: string
  sellerAdBankName?: string
  pricingGuidelinesMet: boolean
  pricingComplianceRemarks: string
  generatedComplianceCertificate?: string
  status: FirmsFilingStatus
  udin?: string
  certifiedAt?: string
  certifiedByCaName?: string
  caMembershipNumber?: string
  caFirmName?: string
  rbiAcknowledgmentReference?: string
  createdAt: string
}

export interface SaveFemaFirmsFilingRequest {
  id?: string
  clientId: string
  filingType: FirmsFilingType
  referenceNumber: string
  indianEntityCin: string
  indianEntityName: string
  pan: string
  nonResidentPartyName: string
  nonResidentCountry: string
  nonResidentCountryCode?: string
  instrumentType: CapitalInstrumentType
  numberOfInstruments: number
  faceValuePerInstrument: number
  issueOrTransferPricePerInstrument: number
  fairValuePerInstrument: number
  totalConsiderationInr: number
  totalConsiderationFc: number
  currency: string
  exchangeRate: number
  valuationMethod: string
  valuerName: string
  valuationReportDate?: string
  dateOfAllotmentOrTransfer: string
  fdiRoute: FdiEntryRoute
  entryRouteApprovalRef?: string
  sectoralCapPct: number
  postAllotmentForeignShareholdingPct: number
  transferDirection: TransferDirectionType
  transferMode: TransferModeType
  buyerAdBankName?: string
  sellerAdBankName?: string
}

export interface CertifyFemaFirmsRequest {
  caName: string
  caMembershipNumber: string
  caFirmName: string
  udin: string
}

export interface FemaFirmsSummaryDto {
  totalFilings: number
  fcGprCount: number
  fcTrsCount: number
  totalConsiderationInr: number
  lateSubmissionsCount: number
}

export interface RbiAnnualFlaReturnDto {
  id: string
  clientId: string
  clientName?: string
  clientPan?: string
  financialYear: string
  companyCin: string
  companyName: string
  pan: string
  accountClosingDate: string
  statutoryFilingDeadline: string
  auditStatus: FlaAuditStatus
  paidUpCapitalInr: number
  reservesAndSurplusInr: number
  netWorthInr: number
  totalSalesTurnoverInr: number
  netProfitLossAfterTaxInr: number
  totalAssetsInr: number
  totalLiabilitiesInr: number
  foreignEquityHoldingPct: number
  fdiAtFaceValueInr: number
  fdiAtNetAssetValueInr: number
  fdiAtMarketOrDcfValueInr: number
  freshFdiInflowsDuringYearInr: number
  disinvestmentOutflowsDuringYearInr: number
  retainedEarningsAttributableToFdiInr: number
  hasOdi: boolean
  overseasEntityName?: string
  overseasCountry?: string
  odiEquityHoldingPct: number
  odiEquityInvestmentInr: number
  odiLoanOutstandingInr: number
  odiGuaranteeIssuedInr: number
  totalOdiFinancialCommitmentInr: number
  externalCommercialBorrowingsEcbInr: number
  foreignTradeCreditSuppliersInr: number
  foreignTradeCreditBuyersInr: number
  foreignBankBalancesInr: number
  otherForeignAssetsInr: number
  netForeignLiabilitiesPositionInr: number
  isMandatoryFiling: boolean
  daysRemainingToDeadline: number
  isDeadlineBreached: boolean
  filingStatusAlert: string
  status: FlaReturnStatus
  rbiFlairAckNumber?: string
  submittedAt?: string
  udin?: string
  certifiedAt?: string
  certifiedByCa?: string
  verificationNotes?: string
  createdAt: string
}

export interface SaveRbiAnnualFlaReturnRequest {
  id?: string
  clientId: string
  financialYear: string
  companyCin: string
  companyName: string
  pan: string
  accountClosingDate: string
  statutoryFilingDeadline: string
  auditStatus: FlaAuditStatus
  paidUpCapitalInr: number
  reservesAndSurplusInr: number
  totalSalesTurnoverInr: number
  netProfitLossAfterTaxInr: number
  totalAssetsInr: number
  totalLiabilitiesInr: number
  foreignEquityHoldingPct: number
  fdiAtFaceValueInr: number
  fdiAtMarketOrDcfValueInr: number
  freshFdiInflowsDuringYearInr: number
  disinvestmentOutflowsDuringYearInr: number
  retainedEarningsAttributableToFdiInr: number
  hasOdi: boolean
  overseasEntityName?: string
  overseasCountry?: string
  odiEquityHoldingPct: number
  odiEquityInvestmentInr: number
  odiLoanOutstandingInr: number
  odiGuaranteeIssuedInr: number
  externalCommercialBorrowingsEcbInr: number
  foreignTradeCreditSuppliersInr: number
  foreignTradeCreditBuyersInr: number
  foreignBankBalancesInr: number
  otherForeignAssetsInr: number
}

export interface CertifyRbiFlaRequest {
  certifiedByCa: string
  udin: string
  verificationNotes?: string
}

export interface FileRbiFlaRequest {
  rbiFlairAckNumber: string
}

export interface RbiFlaSummaryDto {
  totalReturns: number
  totalFdiNetAssetValueInr: number
  totalOdiFinancialCommitmentInr: number
  mandatoryReturnsCount: number
  overdueCount: number
}

export interface DtaaMfnEvaluationRequest {
  country: string
  baseTreatyRate: number
}

export interface DtaaMfnEvaluationDto {
  hasMfnClause: boolean
  mfnRate: number
  isCbdtNotificationMandatory: boolean
  guidanceNote: string
}

export interface PeExposureEvaluationRequest {
  hasPhysicalOffice: boolean
  personnelDaysInIndia: number
  hasAuthorityToConcludeContracts: boolean
  maintainsInventoryStock: boolean
  treatyCountry: string
}

export interface PeExposureEvaluationDto {
  isPeExposed: boolean
  peType: string
  assessmentSummary: string
}

// ----------------------------------------------------------------------
// Related Party Transactions & Section 188 / AS 18 / Ind AS 24 Desk (Sprint 36)
// ----------------------------------------------------------------------

export interface RelatedPartyEntityDto {
  id: string
  clientId: string
  clientName?: string
  name: string
  relationshipType: string
  pan: string
  cin: string
  directorOrKmpName: string
  natureOfInterest: string
  isActive: boolean
  transactionCount: number
  createdAt: string
  updatedAt?: string
}

export interface RelatedPartyTransactionDto {
  id: string
  clientId: string
  clientName?: string
  relatedPartyId: string
  relatedPartyName?: string
  relationshipType?: string
  financialYear: string
  transactionDate: string
  contractReference: string
  transactionType: string
  description: string
  transactionAmount: number
  currency: string
  annualTurnoverBenchmark: number
  netWorthBenchmark: number
  thresholdLimitAmount: number
  thresholdPercentage: number
  exceedsStatutoryThreshold: boolean
  isOrdinaryCourseOfBusiness: boolean
  isArmLength: boolean
  armLengthMethod: string
  benchmarkArmLengthPrice: number
  priceVariancePercentage: number
  auditCommitteeApprovalStatus: string
  auditCommitteeMeetingDate?: string
  omnibusApprovalId?: string
  boardResolutionDate?: string
  shareholdersResolutionDate?: string
  requiresShareholderApproval: boolean
  section40A2bRisk: boolean
  mbp4EnteredDate?: string
  outstandingBalanceAtYearEnd: number
  balanceType: string
  status: string
  createdAt: string
  updatedAt?: string
}

export interface RelatedPartyOmnibusApprovalDto {
  id: string
  clientId: string
  clientName?: string
  financialYear: string
  auditCommitteeMeetingDate: string
  title: string
  maximumApprovedLimit: number
  utilizedAmount: number
  approvalCriteria: string
  validityPeriodTo: string
  isActive: boolean
  createdAt: string
  updatedAt?: string
}

export interface RelatedPartySummaryDto {
  totalParties: number
  totalTransactions: number
  totalVolumeInr: number
  armLengthComplianceRate: number
  specialResolutionRequiredCount: number
  auditCommitteeApprovedCount: number
  sec40A2bRiskCount: number
  totalYearEndBalancesInr: number
}

export interface SaveRelatedPartyEntityRequest {
  clientId: string
  name: string
  relationshipType: string
  pan?: string
  cin?: string
  directorOrKmpName?: string
  natureOfInterest?: string
  isActive: boolean
}

export interface SaveRelatedPartyTransactionRequest {
  clientId: string
  relatedPartyId: string
  financialYear: string
  transactionDate: string
  contractReference?: string
  transactionType: string
  description?: string
  transactionAmount: number
  currency?: string
  annualTurnoverBenchmark: number
  netWorthBenchmark: number
  isOrdinaryCourseOfBusiness: boolean
  armLengthMethod?: string
  benchmarkArmLengthPrice: number
  auditCommitteeApprovalStatus?: string
  auditCommitteeMeetingDate?: string
  omnibusApprovalId?: string
  boardResolutionDate?: string
  shareholdersResolutionDate?: string
  mbp4EnteredDate?: string
  outstandingBalanceAtYearEnd: number
  balanceType?: string
}

export interface SaveOmnibusApprovalRequest {
  clientId: string
  financialYear: string
  auditCommitteeMeetingDate: string
  title: string
  maximumApprovedLimit: number
  approvalCriteria?: string
  validityPeriodTo: string
}

export interface ApproveTransactionRequest {
  approvalStatus: string
  meetingDate?: string
  omnibusApprovalId?: string
  boardDate?: string
  shareholdersDate?: string
}

export interface RelatedPartyReportsDto {
  mbp4RegisterText: string
  as18DisclosureNoteText: string
}

export const relatedPartyApi = {
  getSummary: (clientId?: string, fy?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (fy) params.set('fy', fy)
    const qs = params.toString()
    return api<RelatedPartySummaryDto>(`/api/related-party/summary${qs ? `?${qs}` : ''}`)
  },
  listEntities: (clientId?: string) =>
    api<RelatedPartyEntityDto[]>(`/api/related-party/entities${clientId ? `?clientId=${clientId}` : ''}`),
  getEntity: (id: string) =>
    api<RelatedPartyEntityDto>(`/api/related-party/entities/${id}`),
  createEntity: (req: SaveRelatedPartyEntityRequest) =>
    api<RelatedPartyEntityDto>('/api/related-party/entities', { method: 'POST', body: JSON.stringify(req) }),
  updateEntity: (id: string, req: SaveRelatedPartyEntityRequest) =>
    api<RelatedPartyEntityDto>(`/api/related-party/entities/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteEntity: (id: string) =>
    api<void>(`/api/related-party/entities/${id}`, { method: 'DELETE' }),
  listTransactions: (clientId?: string, fy?: string, relatedPartyId?: string, status?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (fy) params.set('fy', fy)
    if (relatedPartyId) params.set('relatedPartyId', relatedPartyId)
    if (status) params.set('status', status)
    const qs = params.toString()
    return api<RelatedPartyTransactionDto[]>(`/api/related-party/transactions${qs ? `?${qs}` : ''}`)
  },
  getTransaction: (id: string) =>
    api<RelatedPartyTransactionDto>(`/api/related-party/transactions/${id}`),
  createTransaction: (req: SaveRelatedPartyTransactionRequest) =>
    api<RelatedPartyTransactionDto>('/api/related-party/transactions', { method: 'POST', body: JSON.stringify(req) }),
  updateTransaction: (id: string, req: SaveRelatedPartyTransactionRequest) =>
    api<RelatedPartyTransactionDto>(`/api/related-party/transactions/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteTransaction: (id: string) =>
    api<void>(`/api/related-party/transactions/${id}`, { method: 'DELETE' }),
  approveTransaction: (id: string, req: ApproveTransactionRequest) =>
    api<RelatedPartyTransactionDto>(`/api/related-party/transactions/${id}/approve`, { method: 'POST', body: JSON.stringify(req) }),
  listOmnibus: (clientId?: string, fy?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (fy) params.set('fy', fy)
    const qs = params.toString()
    return api<RelatedPartyOmnibusApprovalDto[]>(`/api/related-party/omnibus${qs ? `?${qs}` : ''}`)
  },
  createOmnibus: (req: SaveOmnibusApprovalRequest) =>
    api<RelatedPartyOmnibusApprovalDto>('/api/related-party/omnibus', { method: 'POST', body: JSON.stringify(req) }),
  updateOmnibus: (id: string, req: SaveOmnibusApprovalRequest) =>
    api<RelatedPartyOmnibusApprovalDto>(`/api/related-party/omnibus/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteOmnibus: (id: string) =>
    api<void>(`/api/related-party/omnibus/${id}`, { method: 'DELETE' }),
  getReports: (clientId: string, fy: string) =>
    api<RelatedPartyReportsDto>(`/api/related-party/reports?clientId=${clientId}&fy=${encodeURIComponent(fy)}`)
}

// -------------------------------------------------------------
// Sprint 37: Internal Audit Risk & Control Matrix (RCM) & IFC Testing Desk
// -------------------------------------------------------------

export interface InternalAuditEngagementDto {
  id: string
  clientId: string
  clientName?: string | null
  financialYear: string
  auditPeriod: string
  title: string
  engagementLeadName: string
  auditScopeDescription: string
  status: string
  reportDate?: string | null
  healthScorePercentage: number
  totalControls: number
  effectiveControls: number
  totalFindings: number
  criticalFindings: number
  generatedReportText?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface RcmControlItemDto {
  id: string
  engagementId: string
  controlCode: string
  processCycle: string
  subProcess: string
  riskDescription: string
  riskCategory: string
  inherentRiskRating: string
  controlActivity: string
  controlNature: string
  controlFrequency: string
  controlType: string
  controlOwner: string
  assertions: string
  walkthroughPerformed: boolean
  walkthroughDate?: string | null
  walkthroughReference: string
  designEffectiveness: string
  designNotes: string
  testingMethod: string
  sampleSize: number
  exceptionsCount: number
  operatingEffectiveness: string
  testingNotes: string
  testedAt?: string | null
  testedBy: string
  residualRiskRating: string
  hasFinding: boolean
  createdAt: string
  updatedAt?: string | null
}

export interface InternalAuditFindingDto {
  id: string
  engagementId: string
  controlItemId?: string | null
  controlCode: string
  findingReference: string
  title: string
  processCycle: string
  severity: string
  detailedObservation: string
  rootCause: string
  estimatedRiskAmount?: number | null
  ifcClassification: string
  auditorRecommendation: string
  managementActionPlan: string
  actionOwner: string
  targetCompletionDate?: string | null
  implementationStatus: string
  createdAt: string
  updatedAt?: string | null
}

export interface InternalAuditSummaryDto {
  totalEngagements: number
  totalControlsTested: number
  overallHealthScore: number
  totalDeficiencies: number
  criticalDeficiencies: number
  materialWeaknesses: number
  totalFinancialExposure: number
  openActionsCount: number
}

export interface SaveInternalAuditEngagementRequest {
  clientId: string
  financialYear: string
  auditPeriod: string
  title: string
  engagementLeadName?: string | null
  auditScopeDescription?: string | null
  status?: string | null
}

export interface SaveRcmControlItemRequest {
  controlCode: string
  processCycle: string
  subProcess: string
  riskDescription: string
  riskCategory?: string | null
  inherentRiskRating: string
  controlActivity: string
  controlNature?: string | null
  controlFrequency?: string | null
  controlType?: string | null
  controlOwner?: string | null
  assertions?: string | null
  walkthroughPerformed: boolean
  walkthroughDate?: string | null
  walkthroughReference?: string | null
  designEffectiveness: string
  designNotes?: string | null
  testingMethod: string
  sampleSize: number
  exceptionsCount: number
  operatingEffectiveness: string
  testingNotes?: string | null
  testedAt?: string | null
  testedBy?: string | null
}

export interface SaveInternalAuditFindingRequest {
  controlItemId?: string | null
  controlCode?: string | null
  findingReference?: string | null
  title: string
  processCycle: string
  severity: string
  detailedObservation: string
  rootCause?: string | null
  estimatedRiskAmount?: number | null
  ifcClassification: string
  auditorRecommendation: string
  managementActionPlan?: string | null
  actionOwner?: string | null
  targetCompletionDate?: string | null
  implementationStatus?: string | null
}

export interface UpdateFindingStatusRequest {
  implementationStatus: string
  managementActionPlan?: string | null
  targetCompletionDate?: string | null
}

export const internalAuditApi = {
  getSummary: (clientId?: string, financialYear?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (financialYear) params.set('financialYear', financialYear)
    const qs = params.toString()
    return api<InternalAuditSummaryDto>(`/api/internal-audit/summary${qs ? `?${qs}` : ''}`)
  },
  listEngagements: (clientId?: string, financialYear?: string, status?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (financialYear) params.set('financialYear', financialYear)
    if (status) params.set('status', status)
    const qs = params.toString()
    return api<InternalAuditEngagementDto[]>(`/api/internal-audit/engagements${qs ? `?${qs}` : ''}`)
  },
  getEngagement: (id: string) =>
    api<InternalAuditEngagementDto>(`/api/internal-audit/engagements/${id}`),
  createEngagement: (req: SaveInternalAuditEngagementRequest, populateStandardRcm: boolean = false) =>
    api<InternalAuditEngagementDto>(`/api/internal-audit/engagements?populateStandardRcm=${populateStandardRcm}`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),
  updateEngagement: (id: string, req: SaveInternalAuditEngagementRequest) =>
    api<InternalAuditEngagementDto>(`/api/internal-audit/engagements/${id}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),
  deleteEngagement: (id: string) =>
    api<void>(`/api/internal-audit/engagements/${id}`, { method: 'DELETE' }),
  listControls: (engagementId: string, processCycle?: string) => {
    const params = new URLSearchParams()
    if (processCycle && processCycle !== 'All') params.set('processCycle', processCycle)
    const qs = params.toString()
    return api<RcmControlItemDto[]>(`/api/internal-audit/engagements/${engagementId}/controls${qs ? `?${qs}` : ''}`)
  },
  createControl: (engagementId: string, req: SaveRcmControlItemRequest) =>
    api<RcmControlItemDto>(`/api/internal-audit/engagements/${engagementId}/controls`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),
  updateControl: (engagementId: string, controlId: string, req: SaveRcmControlItemRequest) =>
    api<RcmControlItemDto>(`/api/internal-audit/engagements/${engagementId}/controls/${controlId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),
  deleteControl: (engagementId: string, controlId: string) =>
    api<void>(`/api/internal-audit/engagements/${engagementId}/controls/${controlId}`, { method: 'DELETE' }),
  listFindings: (engagementId: string, severity?: string, status?: string) => {
    const params = new URLSearchParams()
    if (severity && severity !== 'All') params.set('severity', severity)
    if (status && status !== 'All') params.set('status', status)
    const qs = params.toString()
    return api<InternalAuditFindingDto[]>(`/api/internal-audit/engagements/${engagementId}/findings${qs ? `?${qs}` : ''}`)
  },
  createFinding: (engagementId: string, req: SaveInternalAuditFindingRequest) =>
    api<InternalAuditFindingDto>(`/api/internal-audit/engagements/${engagementId}/findings`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),
  updateFinding: (engagementId: string, findingId: string, req: SaveInternalAuditFindingRequest) =>
    api<InternalAuditFindingDto>(`/api/internal-audit/engagements/${engagementId}/findings/${findingId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),
  updateFindingStatus: (engagementId: string, findingId: string, req: UpdateFindingStatusRequest) =>
    api<InternalAuditFindingDto>(`/api/internal-audit/engagements/${engagementId}/findings/${findingId}/status`, {
      method: 'PATCH',
      body: JSON.stringify(req),
    }),
  deleteFinding: (engagementId: string, findingId: string) =>
    api<void>(`/api/internal-audit/engagements/${engagementId}/findings/${findingId}`, { method: 'DELETE' }),
  generateReport: (engagementId: string) =>
    api<InternalAuditEngagementDto>(`/api/internal-audit/engagements/${engagementId}/generate-report`, {
      method: 'POST',
    }),
}

// -------------------------------------------------------------
// Sprint 38: GST Anti-Profiteering & Rate Notification Engine (Section 171)
// -------------------------------------------------------------

export interface GstRateNotificationDto {
  id: string
  notificationNumber: string
  notificationDate: string
  effectiveDate: string
  hsnOrSacCode: string
  description: string
  previousRatePercentage: number
  revisedRatePercentage: number
  rateReductionPercentage: number
  isActive: boolean
  notes: string
  createdAt: string
}

export interface AntiProfiteeringAssessmentDto {
  id: string
  clientId: string
  clientName?: string | null
  clientGstin?: string | null
  assessmentReference: string
  title: string
  financialYear: string
  auditPeriodFrom: string
  auditPeriodTo: string
  gstNotificationId?: string | null
  notificationReference: string
  rateCutEffectiveDate?: string | null
  auditStatus: string
  leadAuditorName: string
  udin?: string | null
  certificationDate?: string | null
  totalSkusAudited: number
  profiteeredSkusCount: number
  totalTurnoverPostCut: number
  totalUndueProfiteeredAmount: number
  totalCustomerRefundedAmount: number
  totalCwfDepositedAmount: number
  cwfInterestPayableAmount: number
  statutoryReportText?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface SkuProfiteeringItemDto {
  id: string
  assessmentId: string
  productCode: string
  hsnOrSacCode: string
  productName: string
  unitOfMeasurement: string
  preCutBasePrice: number
  oldGstRate: number
  preCutSellingPrice: number
  newGstRate: number
  commensuratePriceRequired: number
  postCutActualBasePrice: number
  postCutActualSellingPrice: number
  basePriceIncreasePercentage: number
  quantitySoldPostCut: number
  profiteeringPerUnit: number
  totalProfiteeredAmount: number
  complianceStatus: string
  hasRawMaterialCostJustification: boolean
  justificationRemarks: string
  createdAt: string
  updatedAt?: string | null
}

export interface ProfiteeringRefundRecordDto {
  id: string
  assessmentId: string
  recipientType: string
  customerNameOrEntity: string
  customerGstin: string
  invoiceReference: string
  refundAmount: number
  refundMode: string
  refundOrDepositDate: string
  challanOrCinNumber: string
  interestAmountPaid: number
  notes: string
  createdAt: string
}

export interface AntiProfiteeringSummaryDto {
  totalAssessments: number
  totalSkusAudited: number
  totalProfiteeredSkus: number
  totalTurnoverAudited: number
  totalProfiteeredAmount: number
  totalRefundedToCustomers: number
  totalCwfDeposits: number
  totalPendingRemediation: number
}

export interface SaveRateNotificationRequest {
  notificationNumber: string
  notificationDate: string
  effectiveDate: string
  hsnOrSacCode: string
  description: string
  previousRatePercentage: number
  revisedRatePercentage: number
  isActive: boolean
  notes?: string | null
}

export interface SaveAssessmentRequest {
  clientId: string
  assessmentReference: string
  title: string
  financialYear: string
  auditPeriodFrom: string
  auditPeriodTo: string
  gstNotificationId?: string | null
  notificationReference?: string | null
  rateCutEffectiveDate?: string | null
  leadAuditorName?: string | null
}

export interface SaveSkuItemRequest {
  productCode: string
  hsnOrSacCode: string
  productName: string
  unitOfMeasurement?: string | null
  preCutBasePrice: number
  oldGstRate: number
  newGstRate: number
  postCutActualBasePrice: number
  quantitySoldPostCut: number
  hasRawMaterialCostJustification: boolean
  justificationRemarks?: string | null
}

export interface SaveRefundRecordRequest {
  recipientType: string
  customerNameOrEntity: string
  customerGstin?: string | null
  invoiceReference?: string | null
  refundAmount: number
  refundMode?: string | null
  refundOrDepositDate: string
  challanOrCinNumber?: string | null
  interestAmountPaid: number
  notes?: string | null
}

export interface CertifyAssessmentRequest {
  leadAuditorName: string
  udin?: string | null
}

export const antiProfiteeringApi = {
  getSummary: (clientId?: string, financialYear?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (financialYear) params.set('financialYear', financialYear)
    const qs = params.toString()
    return api<AntiProfiteeringSummaryDto>(`/api/anti-profiteering/summary${qs ? `?${qs}` : ''}`)
  },
  listNotifications: (isActive?: boolean) => {
    const params = new URLSearchParams()
    if (isActive !== undefined) params.set('isActive', String(isActive))
    const qs = params.toString()
    return api<GstRateNotificationDto[]>(`/api/anti-profiteering/notifications${qs ? `?${qs}` : ''}`)
  },
  getNotification: (id: string) =>
    api<GstRateNotificationDto>(`/api/anti-profiteering/notifications/${id}`),
  createNotification: (req: SaveRateNotificationRequest) =>
    api<GstRateNotificationDto>('/api/anti-profiteering/notifications', { method: 'POST', body: JSON.stringify(req) }),
  updateNotification: (id: string, req: SaveRateNotificationRequest) =>
    api<GstRateNotificationDto>(`/api/anti-profiteering/notifications/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteNotification: (id: string) =>
    api<void>(`/api/anti-profiteering/notifications/${id}`, { method: 'DELETE' }),

  listAssessments: (clientId?: string, financialYear?: string, status?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (financialYear) params.set('financialYear', financialYear)
    if (status) params.set('status', status)
    const qs = params.toString()
    return api<AntiProfiteeringAssessmentDto[]>(`/api/anti-profiteering/assessments${qs ? `?${qs}` : ''}`)
  },
  getAssessment: (id: string) =>
    api<AntiProfiteeringAssessmentDto>(`/api/anti-profiteering/assessments/${id}`),
  createAssessment: (req: SaveAssessmentRequest) =>
    api<AntiProfiteeringAssessmentDto>('/api/anti-profiteering/assessments', { method: 'POST', body: JSON.stringify(req) }),
  updateAssessment: (id: string, req: SaveAssessmentRequest) =>
    api<AntiProfiteeringAssessmentDto>(`/api/anti-profiteering/assessments/${id}`, { method: 'PUT', body: JSON.stringify(req) }),
  deleteAssessment: (id: string) =>
    api<void>(`/api/anti-profiteering/assessments/${id}`, { method: 'DELETE' }),
  certifyAssessment: (id: string, req: CertifyAssessmentRequest) =>
    api<AntiProfiteeringAssessmentDto>(`/api/anti-profiteering/assessments/${id}/certify`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  listSkus: (assessmentId: string, complianceStatus?: string) => {
    const params = new URLSearchParams()
    if (complianceStatus && complianceStatus !== 'All') params.set('complianceStatus', complianceStatus)
    const qs = params.toString()
    return api<SkuProfiteeringItemDto[]>(`/api/anti-profiteering/assessments/${assessmentId}/skus${qs ? `?${qs}` : ''}`)
  },
  createSku: (assessmentId: string, req: SaveSkuItemRequest) =>
    api<SkuProfiteeringItemDto>(`/api/anti-profiteering/assessments/${assessmentId}/skus`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),
  updateSku: (assessmentId: string, skuId: string, req: SaveSkuItemRequest) =>
    api<SkuProfiteeringItemDto>(`/api/anti-profiteering/assessments/${assessmentId}/skus/${skuId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),
  deleteSku: (assessmentId: string, skuId: string) =>
    api<void>(`/api/anti-profiteering/assessments/${assessmentId}/skus/${skuId}`, { method: 'DELETE' }),

  listRefunds: (assessmentId: string, recipientType?: string) => {
    const params = new URLSearchParams()
    if (recipientType && recipientType !== 'All') params.set('recipientType', recipientType)
    const qs = params.toString()
    return api<ProfiteeringRefundRecordDto[]>(`/api/anti-profiteering/assessments/${assessmentId}/refunds${qs ? `?${qs}` : ''}`)
  },
  createRefund: (assessmentId: string, req: SaveRefundRecordRequest) =>
    api<ProfiteeringRefundRecordDto>(`/api/anti-profiteering/assessments/${assessmentId}/refunds`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),
  updateRefund: (assessmentId: string, refundId: string, req: SaveRefundRecordRequest) =>
    api<ProfiteeringRefundRecordDto>(`/api/anti-profiteering/assessments/${assessmentId}/refunds/${refundId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),
  deleteRefund: (assessmentId: string, refundId: string) =>
    api<void>(`/api/anti-profiteering/assessments/${assessmentId}/refunds/${refundId}`, { method: 'DELETE' }),
}

// -------------------------------------------------------------
// Sprint 39: Secretarial Audit & Form MR-3 Compliance Engine
// -------------------------------------------------------------

export interface SecretarialAuditSummaryDto {
  totalEngagements: number
  activeEngagements: number
  certifiedEngagements: number
  mandatoryApplicableCount: number
  totalQualificationsLogged: number
  delayedFilingsCount: number
  meetingIrregularitiesCount: number
  averageComplianceScore: number
}

export interface SecretarialAuditEngagementDto {
  id: string
  clientId: string
  clientName: string
  engagementReference: string
  title: string
  financialYear: string
  auditPeriodStart: string
  auditPeriodEnd: string
  cin: string
  companyType: string
  paidUpShareCapital: number
  turnover: number
  outstandingBorrowings: number
  isApplicable: boolean
  applicabilityReason: string
  auditStatus: string
  auditorName: string
  auditorFirmName: string
  membershipType: string
  membershipNumber: string
  certificateOfPracticeNumber: string
  peerReviewCertificateNumber: string
  udin?: string
  certifiedAt?: string
  certifiedBy?: string
  totalChecklistCount: number
  compliedCount: number
  notCompliedCount: number
  partiallyCompliedCount: number
  notApplicableCount: number
  qualificationsCount: number
  overallComplianceScore: number
  totalFilingsAudited: number
  filingsDelayedCount: number
  totalMeetingsAudited: number
  meetingDeficienciesCount: number
  formMR3ReportText?: string
  createdAt: string
  updatedAt?: string
}

export interface SecretarialChecklistItemDto {
  id: string
  engagementId: string
  statuteCategory: string
  clauseCode: string
  subjectArea: string
  statutoryRequirement: string
  complianceStatus: string
  auditorRemarks: string
  isQualification: boolean
  deviationRisk: string
  targetCompletionDate?: string
  actionOwner?: string
  createdAt: string
  updatedAt?: string
}

export interface McaFormFilingAuditDto {
  id: string
  engagementId: string
  formName: string
  description: string
  eventDate: string
  statutoryDaysAllowed: number
  statutoryDueDate: string
  actualFilingDate?: string
  srn: string
  filingStatus: string
  delayDays: number
  additionalFeePaid: number
  auditorRemarks: string
  createdAt: string
}

export interface BoardMeetingAuditRecordDto {
  id: string
  engagementId: string
  meetingType: string
  meetingDate: string
  previousMeetingDate?: string
  gapFromPreviousMeetingDays?: number
  isGapCompliant: boolean
  noticeDispatchDate?: string
  noticeDaysGiven?: number
  isNoticeCompliant: boolean
  totalMembersCount: number
  attendedMembersCount: number
  quorumRequiredCount: number
  isQuorumCompliant: boolean
  independentDirectorsAttended: number
  secretarialStandardComplied: boolean
  auditorRemarks: string
  createdAt: string
}

export interface SaveEngagementRequest {
  clientId: string
  engagementReference: string
  title: string
  financialYear: string
  auditPeriodStart: string
  auditPeriodEnd: string
  cin: string
  companyType: string
  paidUpShareCapital: number
  turnover: number
  outstandingBorrowings: number
  auditorName: string
  auditorFirmName: string
  membershipType: string
  membershipNumber: string
  certificateOfPracticeNumber: string
  peerReviewCertificateNumber: string
}

export interface CertifyEngagementRequest {
  udin: string
  certifiedBy: string
}

export interface SaveChecklistItemRequest {
  statuteCategory: string
  clauseCode: string
  subjectArea: string
  statutoryRequirement: string
  complianceStatus: string
  auditorRemarks: string
  isQualification: boolean
  deviationRisk: string
  targetCompletionDate?: string
  actionOwner?: string
}

export interface BatchChecklistUpdateItem {
  id: string
  complianceStatus: string
  auditorRemarks: string
  isQualification: boolean
  deviationRisk: string
}

export interface BatchUpdateChecklistRequest {
  items: BatchChecklistUpdateItem[]
}

export interface SaveMcaFilingRequest {
  formName: string
  description: string
  eventDate: string
  statutoryDaysAllowed: number
  actualFilingDate?: string
  srn: string
  additionalFeePaid: number
  auditorRemarks: string
}

export interface SaveBoardMeetingAuditRequest {
  meetingType: string
  meetingDate: string
  previousMeetingDate?: string
  noticeDispatchDate?: string
  totalMembersCount: number
  attendedMembersCount: number
  independentDirectorsAttended: number
  secretarialStandardComplied: boolean
  auditorRemarks: string
}

export interface ApplicabilityCheckRequest {
  companyType: string
  paidUpShareCapital: number
  turnover: number
  outstandingBorrowings: number
}

export interface ApplicabilityCheckResult {
  isApplicable: boolean
  reason: string
  thresholdsExceeded: string[]
}

export const secretarialAuditApi = {
  getSummary: (clientId?: string, financialYear?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (financialYear) params.set('financialYear', financialYear)
    const qs = params.toString()
    return api<SecretarialAuditSummaryDto>(`/api/secretarial-audit/summary${qs ? `?${qs}` : ''}`)
  },

  checkApplicability: (req: ApplicabilityCheckRequest) =>
    api<ApplicabilityCheckResult>('/api/secretarial-audit/check-applicability', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  listEngagements: (clientId?: string, financialYear?: string, status?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (financialYear) params.set('financialYear', financialYear)
    if (status && status !== 'All') params.set('status', status)
    const qs = params.toString()
    return api<SecretarialAuditEngagementDto[]>(`/api/secretarial-audit/engagements${qs ? `?${qs}` : ''}`)
  },

  getEngagement: (id: string) =>
    api<SecretarialAuditEngagementDto>(`/api/secretarial-audit/engagements/${id}`),

  createEngagement: (req: SaveEngagementRequest) =>
    api<SecretarialAuditEngagementDto>('/api/secretarial-audit/engagements', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateEngagement: (id: string, req: SaveEngagementRequest) =>
    api<SecretarialAuditEngagementDto>(`/api/secretarial-audit/engagements/${id}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteEngagement: (id: string) =>
    api<void>(`/api/secretarial-audit/engagements/${id}`, { method: 'DELETE' }),

  certifyEngagement: (id: string, req: CertifyEngagementRequest) =>
    api<SecretarialAuditEngagementDto>(`/api/secretarial-audit/engagements/${id}/certify`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  generateMR3: (id: string) =>
    api<{ reportText: string }>(`/api/secretarial-audit/engagements/${id}/generate-mr3`, {
      method: 'POST',
    }),

  listChecklist: (engagementId: string, category?: string, status?: string) => {
    const params = new URLSearchParams()
    if (category && category !== 'All') params.set('category', category)
    if (status && status !== 'All') params.set('status', status)
    const qs = params.toString()
    return api<SecretarialChecklistItemDto[]>(`/api/secretarial-audit/engagements/${engagementId}/checklist${qs ? `?${qs}` : ''}`)
  },

  createChecklistItem: (engagementId: string, req: SaveChecklistItemRequest) =>
    api<SecretarialChecklistItemDto>(`/api/secretarial-audit/engagements/${engagementId}/checklist`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateChecklistItem: (engagementId: string, itemId: string, req: SaveChecklistItemRequest) =>
    api<SecretarialChecklistItemDto>(`/api/secretarial-audit/engagements/${engagementId}/checklist/${itemId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteChecklistItem: (engagementId: string, itemId: string) =>
    api<void>(`/api/secretarial-audit/engagements/${engagementId}/checklist/${itemId}`, { method: 'DELETE' }),

  batchUpdateChecklist: (engagementId: string, req: BatchUpdateChecklistRequest) =>
    api<{ updatedCount: number }>(`/api/secretarial-audit/engagements/${engagementId}/checklist/batch`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  listFilings: (engagementId: string) =>
    api<McaFormFilingAuditDto[]>(`/api/secretarial-audit/engagements/${engagementId}/filings`),

  createFiling: (engagementId: string, req: SaveMcaFilingRequest) =>
    api<McaFormFilingAuditDto>(`/api/secretarial-audit/engagements/${engagementId}/filings`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateFiling: (engagementId: string, filingId: string, req: SaveMcaFilingRequest) =>
    api<McaFormFilingAuditDto>(`/api/secretarial-audit/engagements/${engagementId}/filings/${filingId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteFiling: (engagementId: string, filingId: string) =>
    api<void>(`/api/secretarial-audit/engagements/${engagementId}/filings/${filingId}`, { method: 'DELETE' }),

  listMeetings: (engagementId: string) =>
    api<BoardMeetingAuditRecordDto[]>(`/api/secretarial-audit/engagements/${engagementId}/meetings`),

  createMeeting: (engagementId: string, req: SaveBoardMeetingAuditRequest) =>
    api<BoardMeetingAuditRecordDto>(`/api/secretarial-audit/engagements/${engagementId}/meetings`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateMeeting: (engagementId: string, meetingId: string, req: SaveBoardMeetingAuditRequest) =>
    api<BoardMeetingAuditRecordDto>(`/api/secretarial-audit/engagements/${engagementId}/meetings/${meetingId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteMeeting: (engagementId: string, meetingId: string) =>
    api<void>(`/api/secretarial-audit/engagements/${engagementId}/meetings/${meetingId}`, { method: 'DELETE' }),
}

// -------------------------------------------------------------
// Sprint 40: Transfer Pricing Study & Form 3CEB Certification Desk
// -------------------------------------------------------------

export interface TransferPricingSummaryDto {
  totalStudies: number
  activeStudies: number
  certifiedStudies: number
  totalAssociatedEnterprises: number
  totalInternationalTransactions: number
  totalInternationalVolumeInr: number
  totalSdtTransactions: number
  totalSdtVolumeInr: number
  totalAlpAdjustmentsCount: number
  totalAlpAdjustmentAmount: number
}

export interface TransferPricingStudyDto {
  id: string
  clientId: string
  clientName: string
  studyReference: string
  title: string
  financialYear: string
  assessmentYear: string
  auditPeriodStart: string
  auditPeriodEnd: string
  rule10DDocumentationMaintained: boolean
  rule10DDocumentationPath: string
  studyStatus: string
  accountantName: string
  accountantFirmName: string
  membershipNumber: string
  firmRegistrationNumber: string
  udin?: string
  certifiedAt?: string
  certifiedBy?: string
  totalAssociatedEnterprisesCount: number
  totalInternationalTransactionsCount: number
  totalInternationalInrValue: number
  totalSdtTransactionsCount: number
  totalSdtInrValue: number
  totalAlpAdjustmentsCount: number
  totalAlpAdjustmentAmount: number
  transactionsWithinToleranceCount: number
  transactionsOutsideToleranceCount: number
  form3CebReportText?: string
  createdAt: string
  updatedAt?: string
}

export interface AssociatedEnterpriseDto {
  id: string
  studyId: string
  enterpriseName: string
  countryOfResidence: string
  countryCode: string
  taxIdentificationNumber: string
  registeredAddress: string
  relationshipSection: string
  ownershipPercentage: number
  briefActivityDescription: string
  createdAt: string
}

export interface TransferPricingTransactionDto {
  id: string
  studyId: string
  associatedEnterpriseId?: string
  associatedEnterpriseName?: string
  associatedEnterpriseCountry?: string
  transactionCategory: string
  clauseNumber: number
  descriptionOfTransaction: string
  currencyCode: string
  foreignCurrencyAmount: number
  exchangeRate: number
  inrValue: number
  mostAppropriateMethod: string
  profitLevelIndicator: string
  testedPartyCostBase: number
  testedPartyMargin: number
  comparablesCount: number
  benchmarkRangeMin: number
  benchmarkRangeMax: number
  benchmarkMedianOrMean: number
  toleranceBandPercentage: number
  isWithinArmLength: boolean
  alpAdjustmentAmount: number
  methodSelectionRationale: string
  documentationReference: string
  createdAt: string
  updatedAt?: string
}

export interface ComparableBenchmarkDto {
  id: string
  studyId: string
  transactionId?: string
  comparableCompanyName: string
  cinOrIdentifier: string
  financialYear: string
  operatingRevenue: number
  operatingCost: number
  operatingProfit: number
  calculatedMarginPercentage: number
  weight: number
  isAccepted: boolean
  rejectionReason?: string
  createdAt: string
}

export interface SaveStudyRequest {
  clientId: string
  studyReference: string
  title: string
  financialYear: string
  assessmentYear: string
  auditPeriodStart: string
  auditPeriodEnd: string
  rule10DDocumentationMaintained: boolean
  rule10DDocumentationPath: string
  accountantName: string
  accountantFirmName: string
  membershipNumber: string
  firmRegistrationNumber: string
}

export interface CertifyStudyRequest {
  udin: string
  certifiedBy: string
}

export interface SaveEnterpriseRequest {
  enterpriseName: string
  countryOfResidence: string
  countryCode: string
  taxIdentificationNumber: string
  registeredAddress: string
  relationshipSection: string
  ownershipPercentage: number
  briefActivityDescription: string
}

export interface SaveTransactionRequest {
  associatedEnterpriseId?: string
  transactionCategory: string
  clauseNumber: number
  descriptionOfTransaction: string
  currencyCode: string
  foreignCurrencyAmount: number
  exchangeRate: number
  mostAppropriateMethod: string
  profitLevelIndicator: string
  testedPartyCostBase: number
  testedPartyMargin: number
  toleranceBandPercentage: number
  methodSelectionRationale: string
  documentationReference: string
}

export interface SaveComparableRequest {
  transactionId?: string
  comparableCompanyName: string
  cinOrIdentifier: string
  financialYear: string
  operatingRevenue: number
  operatingCost: number
  operatingProfit: number
  weight: number
  isAccepted: boolean
  rejectionReason?: string
}

export interface BenchmarkCalculationRequest {
  comparableMargins: number[]
  toleranceBandPercentage: number
}

export interface BenchmarkCalculationResult {
  useRange: boolean
  dataCount: number
  rangeMin: number
  rangeMax: number
  medianOrMean: number
  explanation: string
}

export const transferPricingApi = {
  getSummary: (clientId?: string, financialYear?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (financialYear) params.set('financialYear', financialYear)
    const qs = params.toString()
    return api<TransferPricingSummaryDto>(`/api/transfer-pricing/summary${qs ? `?${qs}` : ''}`)
  },

  calculateBenchmark: (req: BenchmarkCalculationRequest) =>
    api<BenchmarkCalculationResult>('/api/transfer-pricing/calculate-benchmark', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  listStudies: (clientId?: string, financialYear?: string, status?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (financialYear) params.set('financialYear', financialYear)
    if (status && status !== 'All') params.set('status', status)
    const qs = params.toString()
    return api<TransferPricingStudyDto[]>(`/api/transfer-pricing/studies${qs ? `?${qs}` : ''}`)
  },

  getStudy: (id: string) =>
    api<TransferPricingStudyDto>(`/api/transfer-pricing/studies/${id}`),

  createStudy: (req: SaveStudyRequest) =>
    api<TransferPricingStudyDto>('/api/transfer-pricing/studies', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateStudy: (id: string, req: SaveStudyRequest) =>
    api<TransferPricingStudyDto>(`/api/transfer-pricing/studies/${id}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteStudy: (id: string) =>
    api<void>(`/api/transfer-pricing/studies/${id}`, { method: 'DELETE' }),

  certifyStudy: (id: string, req: CertifyStudyRequest) =>
    api<TransferPricingStudyDto>(`/api/transfer-pricing/studies/${id}/certify`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  generate3CEB: (id: string) =>
    api<{ reportText: string }>(`/api/transfer-pricing/studies/${id}/generate-3ceb`, {
      method: 'POST',
    }),

  listEnterprises: (studyId: string) =>
    api<AssociatedEnterpriseDto[]>(`/api/transfer-pricing/studies/${studyId}/enterprises`),

  createEnterprise: (studyId: string, req: SaveEnterpriseRequest) =>
    api<AssociatedEnterpriseDto>(`/api/transfer-pricing/studies/${studyId}/enterprises`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateEnterprise: (studyId: string, aeId: string, req: SaveEnterpriseRequest) =>
    api<AssociatedEnterpriseDto>(`/api/transfer-pricing/studies/${studyId}/enterprises/${aeId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteEnterprise: (studyId: string, aeId: string) =>
    api<void>(`/api/transfer-pricing/studies/${studyId}/enterprises/${aeId}`, { method: 'DELETE' }),

  listTransactions: (studyId: string) =>
    api<TransferPricingTransactionDto[]>(`/api/transfer-pricing/studies/${studyId}/transactions`),

  createTransaction: (studyId: string, req: SaveTransactionRequest) =>
    api<TransferPricingTransactionDto>(`/api/transfer-pricing/studies/${studyId}/transactions`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateTransaction: (studyId: string, txId: string, req: SaveTransactionRequest) =>
    api<TransferPricingTransactionDto>(`/api/transfer-pricing/studies/${studyId}/transactions/${txId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteTransaction: (studyId: string, txId: string) =>
    api<void>(`/api/transfer-pricing/studies/${studyId}/transactions/${txId}`, { method: 'DELETE' }),

  listComparables: (studyId: string) =>
    api<ComparableBenchmarkDto[]>(`/api/transfer-pricing/studies/${studyId}/comparables`),

  createComparable: (studyId: string, req: SaveComparableRequest) =>
    api<ComparableBenchmarkDto>(`/api/transfer-pricing/studies/${studyId}/comparables`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateComparable: (studyId: string, compId: string, req: SaveComparableRequest) =>
    api<ComparableBenchmarkDto>(`/api/transfer-pricing/studies/${studyId}/comparables/${compId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteComparable: (studyId: string, compId: string) =>
    api<void>(`/api/transfer-pricing/studies/${studyId}/comparables/${compId}`, { method: 'DELETE' }),
}

// -------------------------------------------------------------
// Sprint 41: Cost Audit & Form CRA-1 to CRA-4 Desk (Companies Act Sec 148 / Cost Records Rules 2014)
// -------------------------------------------------------------

export type CostAuditSummaryDto = {
  totalEngagements: number
  activeAudits: number
  certifiedAudits: number
  regulatedSectorsCount: number
  nonRegulatedSectorsCount: number
  overdueFilingsCount: number
  totalAuditedProductionInr: number
  totalCostProfitInr: number
  totalUnreconciledDifferenceInr: number
  averageRecordsComplianceScorePct: number
}

export type CostRecordHeadDto = {
  id: string
  engagementId: string
  headCode: string
  headName: string
  ruleReference: string
  status: string
  recordMaintenanceSystem: string
  verificationNotes?: string | null
  deficiencyOrAnomaly?: string | null
  correctiveActionRequired?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type ProductCostStatementDto = {
  id: string
  engagementId: string
  productCode: string
  productName: string
  ctaHeading: string
  unitOfMeasurement: string
  installedCapacity: number
  availableCapacity: number
  actualProduction: number
  capacityUtilizationPct: number
  directMaterialsCost: number
  directLaborCost: number
  utilitiesCost: number
  otherDirectExpenses: number
  primeCost: number
  worksOverheads: number
  worksCost: number
  qualityControlCost: number
  rndExpenses: number
  administrationOverheads: number
  openingWipCost: number
  closingWipCost: number
  costOfProduction: number
  openingFinishedGoodsCost: number
  closingFinishedGoodsCost: number
  sellingAndDistributionOverheads: number
  costOfSales: number
  netSalesRealization: number
  productMarginInr: number
  productMarginPct: number
  unitCostOfSales: number
  unitSalesRealization: number
  unitMarginInr: number
  createdAt: string
  updatedAt?: string | null
}

export type CostAuditReconciliationItemDto = {
  id: string
  engagementId: string
  itemType: string
  description: string
  amountInr: number
  impact: string
  financialScheduleRef?: string | null
  notes?: string | null
  createdAt: string
  updatedAt?: string | null
}

export type CostAuditEngagementDto = {
  id: string
  clientId: string
  clientName: string
  engagementReference: string
  financialYear: string
  periodStart: string
  periodEnd: string
  sectorType: string
  sectorCategory: string
  overallTurnoverInr: number
  productTurnoverInr: number
  exportTurnoverInr: number
  isExportExempt: boolean
  isSezExempt: boolean
  isCaptivePowerExempt: boolean
  isCostRecordsApplicable: boolean
  isCostAuditApplicable: boolean
  applicabilityReasoning: string
  boardResolutionDate?: string | null
  fyCommencementDate?: string | null
  auditCommitteeRecommended: boolean
  costAuditorName: string
  costAuditorFirmName: string
  costAuditorMembershipNo: string
  costAuditorFirmRegNo: string
  cra2FilingDueDate?: string | null
  cra2FilingStatus: string
  cra2Srn?: string | null
  cra2FiledDate?: string | null
  auditStatus: string
  auditorOpinion: string
  reservationsQualifications?: string | null
  observationsSuggestions?: string | null
  udin?: string | null
  certifiedAt?: string | null
  certifiedBy?: string | null
  costAuditReportText?: string | null
  cra3HandoverDate?: string | null
  cra4FilingDueDate?: string | null
  cra4FilingStatus: string
  cra4Srn?: string | null
  cra4FiledDate?: string | null
  cra4XbrlValidated: boolean
  totalProductsCount: number
  totalProductionValueInr: number
  totalCostOfSalesInr: number
  totalSalesRealizationInr: number
  totalCostProfitInr: number
  auditedFinancialProfitInr: number
  reconciledFinancialProfitInr: number
  netReconciliationDifferenceInr: number
  recordsComplianceScorePct: number
  createdAt: string
  updatedAt?: string | null
  recordHeads: CostRecordHeadDto[]
  productStatements: ProductCostStatementDto[]
  reconciliationItems: CostAuditReconciliationItemDto[]
}

export type CostAuditApplicabilityRequest = {
  sectorType: string
  overallTurnoverInr: number
  productTurnoverInr: number
  exportTurnoverInr: number
  isSezUnit: boolean
  isCaptivePower: boolean
}

export type CostAuditApplicabilityResult = {
  isCostRecordsApplicable: boolean
  isCostAuditApplicable: boolean
  isExportExempt: boolean
  reasoning: string
}

export type MaterialVarianceRequest = {
  standardQuantity: number
  actualQuantity: number
  standardPrice: number
  actualPrice: number
}

export type MaterialVarianceResult = {
  priceVariance: number
  usageVariance: number
  totalCostVariance: number
}

export type CreateCostAuditEngagementRequest = {
  clientId: string
  engagementReference: string
  financialYear: string
  periodStart: string
  periodEnd: string
  sectorType: string
  sectorCategory: string
  overallTurnoverInr: number
  productTurnoverInr: number
  exportTurnoverInr: number
  isSezExempt: boolean
  isCaptivePowerExempt: boolean
  boardResolutionDate?: string | null
  fyCommencementDate?: string | null
  auditCommitteeRecommended: boolean
  costAuditorName: string
  costAuditorFirmName: string
  costAuditorMembershipNo: string
  costAuditorFirmRegNo: string
  auditedFinancialProfitInr: number
  seedDefaultHeads?: boolean
}

export type UpdateCostAuditEngagementRequest = {
  engagementReference: string
  financialYear: string
  periodStart: string
  periodEnd: string
  sectorType: string
  sectorCategory: string
  overallTurnoverInr: number
  productTurnoverInr: number
  exportTurnoverInr: number
  isSezExempt: boolean
  isCaptivePowerExempt: boolean
  boardResolutionDate?: string | null
  fyCommencementDate?: string | null
  auditCommitteeRecommended: boolean
  costAuditorName: string
  costAuditorFirmName: string
  costAuditorMembershipNo: string
  costAuditorFirmRegNo: string
  auditedFinancialProfitInr: number
  auditorOpinion: string
  reservationsQualifications?: string | null
  observationsSuggestions?: string | null
  auditStatus: string
}

export type CreateProductCostStatementRequest = {
  productCode: string
  productName: string
  ctaHeading: string
  unitOfMeasurement: string
  installedCapacity: number
  availableCapacity: number
  actualProduction: number
  directMaterialsCost: number
  directLaborCost: number
  utilitiesCost: number
  otherDirectExpenses: number
  worksOverheads: number
  qualityControlCost: number
  rndExpenses: number
  administrationOverheads: number
  openingWipCost: number
  closingWipCost: number
  openingFinishedGoodsCost: number
  closingFinishedGoodsCost: number
  sellingAndDistributionOverheads: number
  netSalesRealization: number
}

export type UpdateProductCostStatementRequest = CreateProductCostStatementRequest

export type CreateReconciliationItemRequest = {
  itemType: string
  description: string
  amountInr: number
  impact: string
  financialScheduleRef?: string | null
  notes?: string | null
}

export type UpdateReconciliationItemRequest = CreateReconciliationItemRequest

export type UpdateRecordHeadRequest = {
  status: string
  recordMaintenanceSystem: string
  verificationNotes?: string | null
  deficiencyOrAnomaly?: string | null
  correctiveActionRequired?: string | null
}

export type RecordCra2FilingRequest = {
  cra2Srn: string
  cra2FiledDate: string
  cra2FilingStatus: string
}

export type RecordCra4FilingRequest = {
  cra4Srn: string
  cra4FiledDate: string
  cra4FilingStatus: string
  cra4XbrlValidated: boolean
}

export type CostAuditCertifyRequest = {
  udin: string
  certifiedBy: string
}

export const costAuditApi = {
  summary: (clientId?: string, fy?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('financialYear', fy)
    const qs = p.toString()
    return api<CostAuditSummaryDto>(`/api/cost-audit/summary${qs ? `?${qs}` : ''}`)
  },

  checkApplicability: (req: CostAuditApplicabilityRequest) =>
    api<CostAuditApplicabilityResult>('/api/cost-audit/applicability-check', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  calculateVariance: (req: MaterialVarianceRequest) =>
    api<MaterialVarianceResult>('/api/cost-audit/variance-calculate', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  listEngagements: (clientId?: string, fy?: string, status?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (fy) p.set('financialYear', fy)
    if (status) p.set('auditStatus', status)
    const qs = p.toString()
    return api<CostAuditEngagementDto[]>(`/api/cost-audit/engagements${qs ? `?${qs}` : ''}`)
  },

  getEngagement: (id: string) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}`),

  createEngagement: (req: CreateCostAuditEngagementRequest) =>
    api<CostAuditEngagementDto>('/api/cost-audit/engagements', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateEngagement: (id: string, req: UpdateCostAuditEngagementRequest) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteEngagement: (id: string) =>
    api<void>(`/api/cost-audit/engagements/${id}`, { method: 'DELETE' }),

  seedHeads: (id: string) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/seed-heads`, {
      method: 'POST',
    }),

  updateHead: (id: string, headId: string, req: UpdateRecordHeadRequest) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/heads/${headId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  addProduct: (id: string, req: CreateProductCostStatementRequest) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/products`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateProduct: (id: string, productId: string, req: UpdateProductCostStatementRequest) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/products/${productId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteProduct: (id: string, productId: string) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/products/${productId}`, {
      method: 'DELETE',
    }),

  addReconciliation: (id: string, req: CreateReconciliationItemRequest) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/reconciliation`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  updateReconciliation: (id: string, itemId: string, req: UpdateReconciliationItemRequest) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/reconciliation/${itemId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  deleteReconciliation: (id: string, itemId: string) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/reconciliation/${itemId}`, {
      method: 'DELETE',
    }),

  recordCra2: (id: string, req: RecordCra2FilingRequest) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/cra2-filing`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  recordCra4: (id: string, req: RecordCra4FilingRequest) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/cra4-filing`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  generateCra3: (id: string) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/generate-cra3`, {
      method: 'POST',
    }),

  certify: (id: string, req: CostAuditCertifyRequest) =>
    api<CostAuditEngagementDto>(`/api/cost-audit/engagements/${id}/certify`, {
      method: 'POST',
      body: JSON.stringify(req),
    }),
}

// -------------------------------------------------------------
// Trial Balance & Multi-Desk Synchronizer (Phase 1 Data In)
// -------------------------------------------------------------

export interface TrialBalanceImportDto {
  id: string
  clientId: string
  clientName?: string | null
  financialYear: string
  sourceType: string
  fileName: string
  totalDebit: number
  totalCredit: number
  isBalanced: boolean
  totalLedgersCount: number
  mappedLedgersCount: number
  status: string
  syncedAt?: string | null
  syncedDesks?: string | null
  createdAt: string
  uploadedAt?: string
  isSynced?: boolean
  lastSyncedAt?: string | null
  ledgers: TrialBalanceLedgerDto[]
}

export interface TrialBalanceLedgerDto {
  id: string
  importId: string
  ledgerName: string
  tallyGroup: string
  openingBalance: number
  debitAmount: number
  creditAmount: number
  closingBalance: number
  schedule3Group: string
  form3CdCategory?: string | null
  caroCategory?: string | null
  fixedAssetBlock?: string | null
  isMapped: boolean
}

export interface ClientLedgerMappingDto {
  id: string
  clientId: string
  ledgerName: string
  schedule3Group: string
  form3CdCategory?: string | null
  caroCategory?: string | null
  fixedAssetBlock?: string | null
  createdAt: string
  updatedAt?: string | null
}

export interface UpdateLedgerMappingRequest {
  schedule3Group: string
  form3CdCategory?: string | null
  caroCategory?: string | null
  fixedAssetBlock?: string | null
  saveAsClientDefaultRule: boolean
}

export interface UploadTrialBalanceRequest {
  clientId: string
  financialYear: string
  sourceType: string
  fileName: string
  contentPayload: string
}

export interface SyncTrialBalanceResultDto {
  importId: string
  financialYear: string
  auditSynced: boolean
  auditNotes?: string | null
  schedule3Synced: boolean
  schedule3Notes?: string | null
  form3CdSynced: boolean
  form3CdNotes?: string | null
  caroSynced: boolean
  caroNotes?: string | null
  fixedAssetsSynced: boolean
  fixedAssetsNotes?: string | null
  costAuditSynced?: boolean
  costAuditNotes?: string | null
  syncedAt: string
}

export interface FinancialKpisDto {
  grossRevenue: number
  otherIncome: number
  totalIncome: number
  totalExpenses: number
  operatingProfit: number
  ebitda: number
  profitBeforeTax: number
  profitAfterTax: number
  totalAssets: number
  totalLiabilities: number
  totalDebt: number
  shareholdersEquity: number
  currentRatio: number
}

export interface TrialBalancePreviewResultDto {
  detectedSourceType: string
  delimiterUsed: string
  headerRowIndex: number
  totalLedgersCount: number
  mappedLedgersCount: number
  totalDebit: number
  totalCredit: number
  discrepancy: number
  isBalanced: boolean
  ledgers: Array<{
    name: string
    parentGroup: string
    openingBalance: number
    debitAmount: number
    creditAmount: number
    closingBalance: number
  }>
  kpis?: FinancialKpisDto | null
  diagnostics: string[]
}

export interface PreviewTrialBalanceRequest {
  contentPayload: string
  sourceType?: string | null
}

export interface TrialBalanceSampleDto {
  key: string
  title: string
  description: string
  sourceType: string
  turnoverInr: number
  ledgersCount: number
  contentPayload: string
}

export const trialBalanceApi = {
  upload: (req: UploadTrialBalanceRequest) =>
    api<TrialBalanceImportDto>('/api/trial-balance/upload', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  preview: (req: PreviewTrialBalanceRequest) =>
    api<TrialBalancePreviewResultDto>('/api/trial-balance/preview', {
      method: 'POST',
      body: JSON.stringify(req),
    }),

  getSamples: () =>
    api<TrialBalanceSampleDto[]>('/api/trial-balance/samples'),

  listImports: (clientId?: string, financialYear?: string) => {
    const p = new URLSearchParams()
    if (clientId) p.set('clientId', clientId)
    if (financialYear) p.set('financialYear', financialYear)
    const qs = p.toString()
    return api<TrialBalanceImportDto[]>(`/api/trial-balance/imports${qs ? `?${qs}` : ''}`)
  },

  getImport: (id: string) =>
    api<TrialBalanceImportDto>(`/api/trial-balance/imports/${id}`),

  updateLedgerMapping: (importId: string, ledgerId: string, req: UpdateLedgerMappingRequest) =>
    api<TrialBalanceImportDto>(`/api/trial-balance/imports/${importId}/ledgers/${ledgerId}`, {
      method: 'PUT',
      body: JSON.stringify(req),
    }),

  syncAllDesks: (importId: string, req?: { syncAudit?: boolean; syncSchedule3?: boolean; syncForm3Cd?: boolean; syncCaro?: boolean; syncFixedAssets?: boolean; syncCostAudit?: boolean }) =>
    api<SyncTrialBalanceResultDto>(`/api/trial-balance/imports/${importId}/sync`, {
      method: 'POST',
      body: JSON.stringify(req || {}),
    }),

  getClientRules: (clientId: string) =>
    api<ClientLedgerMappingDto[]>(`/api/trial-balance/clients/${clientId}/rules`),
}

// -------------------------------------------------------------
// Sprint 47: Commercial SaaS Monetization, Subscriptions & Demo Seeder
// -------------------------------------------------------------

export type SubscriptionStatusDto = {
  planTier: string
  displayName: string
  status: string
  billingInterval: string
  trialStartsAt: string
  trialEndsAt: string
  daysRemainingInTrial: number
  isTrialActive: boolean
  currentPeriodEnd: string
  cancelAtPeriodEnd: boolean
  currentUsersCount: number
  userLimit: number
  currentClientsCount: number
  clientLimit: number
  activeAuditsCount: number
  activeAuditsLimit: number
  isDemoDataSeeded: boolean
  includedFeatures: string[]
  lastPaymentAmount: number
  lastPaymentDate?: string | null
  lastPaymentInvoiceNumber?: string | null
}

export type PlanPricingOptionDto = {
  tierCode: string
  displayName: string
  description: string
  monthlyPrice: number
  annualPrice: number
  userLimit: number
  clientLimit: number
  activeAuditsLimit: number
  includedFeatures: string[]
  isPopular: boolean
}

export type UpgradePlanRequest = {
  targetTier: string
  billingInterval?: string
  paymentMethod?: string
  firmGstin?: string | null
  transactionReference?: string | null
}

export type UpgradePlanResponseDto = {
  success: boolean
  planTier: string
  status: string
  invoiceNumber: string
  totalAmountPaid: number
  cgstAmount: number
  sgstAmount: number
  igstAmount: number
  message: string
}

export type SubscriptionInvoiceDto = {
  id: string
  invoiceNumber: string
  invoiceDate: string
  planTier: string
  billingInterval: string
  baseAmount: number
  cgstAmount: number
  sgstAmount: number
  igstAmount: number
  totalAmount: number
  sacCode: string
  paymentStatus: string
  paymentMethod: string
  transactionReference: string
  createdAt: string
}

export type SeedDemoDataResultDto = {
  success: boolean
  clientsCreated: number
  auditEngagementsCreated: number
  bankTransactionsCreated: number
  confirmationsCreated: number
  message: string
}

export type PurgeDemoDataResultDto = {
  success: boolean
  clientsPurged: number
  message: string
}

export const subscriptionApi = {
  getStatus: () => api<SubscriptionStatusDto>('/api/subscriptions/status'),
  getPlans: () => api<PlanPricingOptionDto[]>('/api/subscriptions/plans'),
  upgradePlan: (req: UpgradePlanRequest) =>
    api<UpgradePlanResponseDto>('/api/subscriptions/upgrade', { method: 'POST', body: JSON.stringify(req) }),
  getInvoices: () => api<SubscriptionInvoiceDto[]>('/api/subscriptions/invoices'),
  seedDemoData: () => api<SeedDemoDataResultDto>('/api/subscriptions/seed-demo', { method: 'POST' }),
  purgeDemoData: () => api<PurgeDemoDataResultDto>('/api/subscriptions/purge-demo', { method: 'POST' })
}

// -----------------------------------------------------------------------------
// ICAI AQMM v1.0 (Audit Quality Maturity Model) Desk
// -----------------------------------------------------------------------------
export type AqmmScoringCriterion = {
  points: number
  label: string
  description: string
}

export type AqmmAssessmentSummaryDto = {
  id: string
  financialYear: string
  status: string
  totalScore: number
  maxPossibleScore: number
  overallPercentage: number
  maturityLevel: string
  sectionAScore: number
  sectionBScore: number
  sectionCScore: number
  sectionAPercentage: number
  sectionBPercentage: number
  sectionCPercentage: number
  level2Gap: number
  level3Gap: number
  leadPartnerName?: string | null
  partnerMembershipNo?: string | null
  firmRegistrationNo?: string | null
  udin?: string | null
  certifiedAt?: string | null
  totalQuestionsCount: number
  answeredQuestionsCount: number
}

export type AqmmQuestionResponseDto = {
  responseId?: string | null
  questionCode: string
  section: string
  title: string
  description: string
  maxPoints: number
  awardedPoints: number
  criteria: AqmmScoringCriterion[]
  partnerRemarks?: string | null
  evidenceReference?: string | null
  isAutoEvaluated: boolean
  autoEvaluationKey?: string | null
}

export type UpdateAqmmResponseRequest = {
  questionCode: string
  awardedPoints: number
  partnerRemarks?: string | null
  evidenceReference?: string | null
}

export type CertifyAqmmAssessmentRequest = {
  leadPartnerName: string
  partnerMembershipNo: string
  firmRegistrationNo: string
  udin: string
}

export type AqmmRecommendationDto = {
  questionCode: string
  title: string
  section: string
  currentPoints: number
  maxPoints: number
  potentialGain: number
  remedialAction: string
  priority: string
}

export type AqmmGapAnalysisDto = {
  currentScore: number
  currentLevel: string
  pointsNeededForLevel2: number
  pointsNeededForLevel3: number
  topRecommendations: AqmmRecommendationDto[]
}

export type AutoScoreResultDto = {
  totalScoreBefore: number
  totalScoreAfter: number
  autoScoredCount: number
  maturityLevel: string
  message: string
}

export const aqmmApi = {
  getCurrent: (fy?: string) => api<AqmmAssessmentSummaryDto>(`/api/aqmm/current${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`),
  getQuestions: (fy?: string) => api<AqmmQuestionResponseDto[]>(`/api/aqmm/questions${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`),
  updateResponse: (req: UpdateAqmmResponseRequest, fy?: string) =>
    api<AqmmAssessmentSummaryDto>(`/api/aqmm/responses${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`, { method: 'POST', body: JSON.stringify(req) }),
  autoScore: (fy?: string) =>
    api<AutoScoreResultDto>(`/api/aqmm/auto-score${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`, { method: 'POST' }),
  getGapAnalysis: (fy?: string) => api<AqmmGapAnalysisDto>(`/api/aqmm/gap-analysis${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`),
  certify: (req: CertifyAqmmAssessmentRequest, fy?: string) =>
    api<AqmmAssessmentSummaryDto>(`/api/aqmm/certify${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`, { method: 'POST', body: JSON.stringify(req) }),
  getDossierHtml: async (fy?: string) => {
    const res = await fetch(`/api/aqmm/dossier${fy ? `?fy=${encodeURIComponent(fy)}` : ''}`, {
      headers: { Authorization: `Bearer ${localStorage.getItem('accessToken') || ''}` }
    })
    return res.text()
  }
}

// ---------------------------------------------------------------- Communications & Gateway Types (Sprint 51)

export type MetaWhatsAppGatewayStatus = {
  isConnected: boolean
  mode: string
  phoneNumberId: string
  displayPhoneNumber: string
  wabaId: string
  webhookUrl: string
  templateLanguage: string
  activeTemplatesCount: number
  sentCount: number
  deliveredCount: number
  failedCount: number
}

export type TransactionalEmailGatewayStatus = {
  provider: string
  fromAddress: string
  host: string
  port: number
  sslEnabled: boolean
  apiKeyConfigured: boolean
  sentCount: number
  failedCount: number
}

export type CommunicationsOverviewDto = {
  whatsAppStatus: MetaWhatsAppGatewayStatus
  emailStatus: TransactionalEmailGatewayStatus
  totalDispatched: number
  deliverySuccessRate: number
  recentDispatches: NotificationItem[]
}

export type StatutoryDispatchFilter = {
  regime: 'GST' | 'AdvanceTax' | 'ITR' | 'All'
  daysHorizon?: number
  sendWhatsApp: boolean
  sendEmail: boolean
}

export type StatutoryDispatchResult = {
  deadlinesEvaluated: number
  clientsNotified: number
  whatsAppMessagesQueued: number
  emailMessagesQueued: number
  dispatchedItems: string[]
}

export type Sa505DispatchFilter = {
  campaignId: string
  requestIds?: string[]
  sendWhatsApp: boolean
  sendEmail: boolean
}

export type Sa505DispatchResult = {
  totalProcessed: number
  whatsAppSent: number
  emailSent: number
  dispatchedParties: string[]
}

export type SendPortalOtpRequest = {
  destination: string
  channel: 'whatsapp' | 'email'
}

export type SendPortalOtpResult = {
  success: boolean
  channel: string
  maskedDestination: string
  message: string
  expiresAtUtc: string
}

export type DispatchInvoiceNotificationRequest = {
  invoiceId: string
  sendEmailWithAttachment?: boolean
  sendWhatsApp?: boolean
}

export type DispatchInvoiceNotificationResult = {
  success: boolean
  invoiceNumber: string
  totalAmount: number
  recipientEmail: string
  recipientPhone?: string
  message: string
}

export const communicationsApi = {
  getGatewayStatus: () => api<CommunicationsOverviewDto>('/api/communications/gateway/status'),
  testWhatsApp: (data: { toPhone: string; templateKey?: string; customMessage?: string }) =>
    api<{ success: boolean; providerMessageId?: string; normalizedPhone: string; message: string }>('/api/communications/gateway/test-whatsapp', {
      method: 'POST',
      body: JSON.stringify(data)
    }),
  testEmail: (data: { toEmail: string; subject?: string; body?: string }) =>
    api<{ success: boolean; providerMessageId?: string; message: string }>('/api/communications/gateway/test-email', {
      method: 'POST',
      body: JSON.stringify(data)
    }),
  dispatchStatutoryDeadlines: (filter: StatutoryDispatchFilter) =>
    api<StatutoryDispatchResult>('/api/communications/deadlines/dispatch', {
      method: 'POST',
      body: JSON.stringify(filter)
    }),
  dispatchSa505Confirmations: (filter: Sa505DispatchFilter) =>
    api<Sa505DispatchResult>('/api/communications/sa505/dispatch', {
      method: 'POST',
      body: JSON.stringify(filter)
    }),
  sendPortalOtp: (req: SendPortalOtpRequest) =>
    api<SendPortalOtpResult>('/api/communications/portal/otp', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  dispatchInvoice: (req: DispatchInvoiceNotificationRequest) =>
    api<DispatchInvoiceNotificationResult>('/api/communications/invoices/dispatch', {
      method: 'POST',
      body: JSON.stringify(req)
    })
}

// ---------------------------------------------------------------- Security & Compliance Vault Types (Sprint 52)

export type SecurityAssessmentSummaryDto = {
  id: string
  assessmentPeriod: string
  status: string
  overallScorePercentage: number
  totalControlsCount: number
  compliantControlsCount: number
  partiallyCompliantCount: number
  accessControlScore: number
  cryptographyScore: number
  operationsScore: number
  physicalScore: number
  privacyScore: number
  certifiedByPartner?: string
  partnerMembershipNo?: string
  lastEvaluatedAt?: string
  certifiedAt?: string
}

export type SecurityControlEvidenceDto = {
  id: string
  assessmentId: string
  controlCode: string
  title: string
  category: string
  frameworkMapping: string
  description: string
  verificationMethod: string
  status: string
  isAutomatedTelemetry: boolean
  telemetrySignal?: string
  evidenceNotes?: string
  lastTestedAt?: string
}

export type TelemetryScanResultDto = {
  overallScore: number
  compliantCount: number
  evaluatedSignals: string[]
  scanTimestamp: string
}

export const securityComplianceApi = {
  getSummary: (period?: string) =>
    api<SecurityAssessmentSummaryDto>(`/api/security-compliance/assessment${period ? `?period=${encodeURIComponent(period)}` : ''}`),
  getControls: (period?: string, category?: string) => {
    const params = new URLSearchParams()
    if (period) params.set('period', period)
    if (category) params.set('category', category)
    const qs = params.toString() ? `?${params.toString()}` : ''
    return api<SecurityControlEvidenceDto[]>(`/api/security-compliance/controls${qs}`)
  },
  runTelemetryAudit: (period?: string) =>
    api<TelemetryScanResultDto>(`/api/security-compliance/telemetry/evaluate${period ? `?period=${encodeURIComponent(period)}` : ''}`, {
      method: 'POST'
    }),
  updateEvidence: (controlCode: string, req: { status?: string; evidenceNotes?: string }) =>
    api<SecurityControlEvidenceDto>(`/api/security-compliance/controls/${controlCode}`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  certify: (req: { partnerName: string; membershipNumber: string; assessmentPeriod?: string }) =>
    api<SecurityAssessmentSummaryDto>('/api/security-compliance/certify', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  getWhitepaperHtml: async (period?: string) => {
    const res = await fetch(`/api/security-compliance/whitepaper${period ? `?period=${encodeURIComponent(period)}` : ''}`, {
      headers: { Authorization: `Bearer ${localStorage.getItem('accessToken') || ''}` }
    })
    return res.text()
  }
}

// Sprint 53: Partner Mobile Companion Desk & PWA
export type PartnerPulseSummary = {
  todayCollections: number
  monthCollections: number
  unbilledWipAmount: number
  billableHoursThisWeek: number
  activeStaffCount: number
  criticalDeadlinesCount: number
  pendingAuditSignoffsCount: number
  pendingTimesheetApprovalsCount: number
  pendingInvoiceApprovalsCount: number
  unresolvedForensicAlertsCount: number
}

export type PendingAuditItem = {
  id: string
  clientId: string
  clientName: string
  financialYear: string
  auditType: string
  overallMateriality: number
  auditOpinion: string
  completedSchedulesCount: number
  totalSchedulesCount: number
  status: string
  plannedEndDate?: string | null
}

export type PendingTimesheetItem = {
  id: string
  staffName: string
  clientName: string
  date: string
  hours: number
  hourlyRate: number
  amount: number
  description: string
}

export type PendingInvoiceItem = {
  id: string
  invoiceNumber: string
  clientName: string
  issueDate: string
  dueDate: string
  totalAmount: number
  status: string
}

export type ForensicAlertItem = {
  id: string
  clientName: string
  anomalyType: string
  riskLevel: string
  amount: number
  exposureAmount: number
  counterparty: string
  narration: string
  statutorySection: string
  status: string
  transactionDate: string
}

export type DeadlineCountdownItem = {
  id: string
  title: string
  dueDate: string
  daysRemaining: number
  category: string
  status: string
}

export type PartnerMobileSecurity = {
  hasQuickPin: boolean
  biometricEnabled: boolean
  autoLockMinutes: number
}

export type PartnerActionCenterData = {
  pulse: PartnerPulseSummary
  pendingAudits: PendingAuditItem[]
  pendingTimesheets: PendingTimesheetItem[]
  pendingInvoices: PendingInvoiceItem[]
  forensicAlerts: ForensicAlertItem[]
  deadlines: DeadlineCountdownItem[]
  security: PartnerMobileSecurity
}

export const partnerCompanionApi = {
  getActionCenter: () => api<PartnerActionCenterData>('/api/partner-companion/pulse'),
  quickSignAudit: (id: string, req: { partnerMembershipNo?: string; udin?: string; remarks?: string }) =>
    api<{ success: boolean; message: string }>(`/api/partner-companion/audit/${id}/quick-sign`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  quickApproveTimesheets: (timeEntryIds: string[]) =>
    api<{ success: boolean; approvedCount: number }>('/api/partner-companion/timesheet/quick-approve', {
      method: 'POST',
      body: JSON.stringify({ timeEntryIds })
    }),
  quickApproveInvoice: (id: string) =>
    api<{ success: boolean; message: string }>(`/api/partner-companion/invoice/${id}/quick-approve`, {
      method: 'POST'
    }),
  triageForensicAlert: (id: string, req: { status: string; auditorNotes?: string }) =>
    api<{ success: boolean; message: string }>(`/api/partner-companion/forensic-alert/${id}/triage`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  setupQuickPin: (pin: string, biometricEnabled: boolean = false) =>
    api<{ success: boolean; message: string }>('/api/partner-companion/quick-pin/setup', {
      method: 'POST',
      body: JSON.stringify({ pin, biometricEnabled })
    }),
  verifyQuickPin: (pin: string) =>
    api<{ success: boolean; message?: string }>('/api/partner-companion/quick-pin/verify', {
      method: 'POST',
      body: JSON.stringify({ pin })
    })
}

// Sprint 54: Connected Corporate Banking & Maker-Checker Payout Desk
export type ConnectedBankSummary = {
  id: string
  bankAccountId: string
  clientId: string
  clientName: string
  bankName: string
  accountNumber: string
  ifscCode: string
  accountType: string
  provider: string
  corporateId: string
  status: string
  lastSyncAt?: string
  lastSyncBalance?: number
  currentBalance: number
  syncFrequencyMinutes: number
}

export type ConnectBankRequest = {
  bankAccountId: string
  provider: string
  corporateId: string
  userId: string
  apiKey: string
  apiSecret: string
  webhookSecret?: string
}

export type LiveSyncResult = {
  bankAccountId: string
  provider: string
  syncedBalance: number
  syncedTransactionsCount: number
  syncTimestamp: string
  statementId?: string
  message: string
}

export type PayoutItem = {
  id: string
  batchId: string
  payoutType: string
  beneficiaryName: string
  beneficiaryAccountNumber: string
  beneficiaryIfsc: string
  beneficiaryPan?: string
  amount: number
  paymentMode: string
  narration: string
  invoiceOrChallanRef?: string
  status: string
  utrNumber?: string
  failureReason?: string
}

export type PayoutBatch = {
  id: string
  batchNumber: string
  bankAccountId: string
  bankName: string
  accountNumber: string
  clientId: string
  clientName: string
  title: string
  totalAmount: number
  totalCount: number
  status: string
  createdByUserId: string
  makerName: string
  makerRemarks?: string
  authorizedByUserId?: string
  checkerName?: string
  authorizedAt?: string
  authMethod?: string
  authReference?: string
  rejectionReason?: string
  createdAt: string
  items: PayoutItem[]
}

export type CreatePayoutItemInput = {
  payoutType: string
  beneficiaryName: string
  beneficiaryAccountNumber: string
  beneficiaryIfsc: string
  beneficiaryPan?: string
  amount: number
  paymentMode: string
  narration: string
  invoiceOrChallanRef?: string
}

export type CreatePayoutBatchRequest = {
  bankAccountId: string
  title: string
  makerRemarks?: string
  scheduledExecutionDate?: string
  items: CreatePayoutItemInput[]
}

export type AuthorizePayoutBatchRequest = {
  authMethod: string
  otpOrToken?: string
  partnerRemarks?: string
}

export type RejectPayoutBatchRequest = {
  reason: string
}

export type GenerateTaxChallanPayoutRequest = {
  bankAccountId: string
  challanType: string
  financialYear: string
  assessmentYear: string
  majorHeadCode: string
  minorHeadCode: string
  taxAmount: number
  surchargeAmount: number
  cessAmount: number
  interestAmount: number
  feePenaltyAmount: number
  panOrTanOrGstin: string
  scheduleDate?: string
}

export const connectedBankingApi = {
  getAccounts: (clientId?: string) =>
    api<ConnectedBankSummary[]>(`/api/connected-banking/accounts${clientId ? `?clientId=${clientId}` : ''}`),
  connectAccount: (req: ConnectBankRequest) =>
    api<ConnectedBankSummary>('/api/connected-banking/accounts/connect', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  syncFeed: (id: string) =>
    api<LiveSyncResult>(`/api/connected-banking/accounts/${id}/sync`, {
      method: 'POST'
    }),
  getPayouts: (clientId?: string, status?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.set('clientId', clientId)
    if (status) params.set('status', status)
    const qs = params.toString() ? `?${params.toString()}` : ''
    return api<PayoutBatch[]>(`/api/connected-banking/payouts${qs}`)
  },
  createPayoutBatch: (req: CreatePayoutBatchRequest) =>
    api<PayoutBatch>('/api/connected-banking/payouts', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  submitPayoutBatch: (id: string) =>
    api<{ success: boolean; message: string }>(`/api/connected-banking/payouts/${id}/submit`, {
      method: 'POST'
    }),
  authorizePayoutBatch: (id: string, req: AuthorizePayoutBatchRequest) =>
    api<PayoutBatch>(`/api/connected-banking/payouts/${id}/authorize`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  rejectPayoutBatch: (id: string, req: RejectPayoutBatchRequest) =>
    api<{ success: boolean; message: string }>(`/api/connected-banking/payouts/${id}/reject`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  generateTaxChallan: (req: GenerateTaxChallanPayoutRequest) =>
    api<PayoutBatch>('/api/connected-banking/payouts/tax-challan', {
      method: 'POST',
      body: JSON.stringify(req)
    })
}

// ---------------------------------------------------------------- UAE Corporate Tax & EmaraTax Desk
export type UaeTaxpayerCategory = 'ResidentJuridicalPerson' | 'NonResidentJuridicalPerson' | 'QualifyingFreeZonePerson' | 'ResidentNaturalPerson'
export type FreeZoneRegime = 'Mainland' | 'DesignatedFreeZone' | 'NonDesignatedFreeZone'
export type SmallBusinessReliefStatus = 'NotEligible' | 'EligibleNotElected' | 'Elected'
export type ReturnFilingStatus = 'Draft' | 'SubmittedForReview' | 'Approved' | 'Exported' | 'Filed'

export type UaeTaxProfileDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  corporateTaxTrn: string
  emirate: string
  freeZoneName: string
  category: UaeTaxpayerCategory
  freeZoneRegime: FreeZoneRegime
  financialYearStartMonth: number
  isMneGroupConstituent: boolean
  accountingStandard: string
  auditedFinancialsAvailable: boolean
  createdAt: string
}

export type CreateOrUpdateTaxProfileRequest = {
  clientId: string
  corporateTaxTrn: string
  emirate: string
  freeZoneName: string
  category: UaeTaxpayerCategory
  freeZoneRegime: FreeZoneRegime
  financialYearStartMonth: number
  isMneGroupConstituent: boolean
  accountingStandard: string
  auditedFinancialsAvailable: boolean
}

export type CalculateUaeTaxRequest = {
  category: UaeTaxpayerCategory
  grossRevenue: number
  costOfSales: number
  operatingExpenses: number
  netAccountingProfitLoss: number
  smallBusinessReliefElection: SmallBusinessReliefStatus
  priorYear1Revenue: number
  priorYear2Revenue: number
  isMneConstituent: boolean
  qualifyingRevenue: number
  nonQualifyingRevenue: number
  exemptIncome: number
  unrealizedGainsLossesAdjustment: number
  entertainmentExpenditureTotal: number
  finesAndPenaltiesDisallowed: number
  recoverableVatDisallowed: number
  donationsToUnapprovedEntitiesDisallowed: number
  netInterestExpense: number
  adjustedEbitda: number
  broughtForwardTaxLosses: number
  foreignTaxCreditReliefAed: number
  exchangeRateToInr: number
}

export type UaeComputationResult = {
  sbrEligible: boolean
  qfzpDeMinimisPassed: boolean
  deMinimisThresholdAed: number
  disallowedInterestAed: number
  disallowedEntertainmentAed: number
  totalAdditionsAed: number
  totalDeductionsAed: number
  adjustedTaxableIncomeAed: number
  taxLossesUtilizedAed: number
  remainingTaxLossesAed: number
  finalTaxableIncomeAed: number
  incomeSubjectToZeroPctAed: number
  incomeSubjectToNinePctAed: number
  grossTaxPayableAed: number
  netTaxPayableAed: number
  netTaxPayableInr: number
}

export type SaveUaeCorporateTaxReturnRequest = {
  taxProfileId: string
  returnYear: number
  taxPeriodStart: string
  taxPeriodEnd: string
  grossRevenue: number
  costOfSales: number
  operatingExpenses: number
  netAccountingProfitLoss: number
  smallBusinessReliefElection: SmallBusinessReliefStatus
  priorYear1Revenue: number
  priorYear2Revenue: number
  qualifyingRevenue: number
  nonQualifyingRevenue: number
  exemptIncome: number
  unrealizedGainsLossesAdjustment: number
  entertainmentExpenditureTotal: number
  finesAndPenaltiesDisallowed: number
  recoverableVatDisallowed: number
  donationsToUnapprovedEntitiesDisallowed: number
  netInterestExpense: number
  adjustedEbitda: number
  broughtForwardTaxLosses: number
  foreignTaxCreditReliefAed: number
  exchangeRateToInr: number
}

export type UaeCorporateTaxReturnDto = {
  id: string
  taxProfileId: string
  clientId: string
  clientName: string
  corporateTaxTrn: string
  returnReference: string
  returnYear: number
  taxPeriodStart: string
  taxPeriodEnd: string
  dueDate: string
  status: ReturnFilingStatus
  functionalCurrency: string
  exchangeRateToInr: number

  grossRevenue: number
  costOfSales: number
  grossProfit: number
  operatingExpenses: number
  netAccountingProfitLoss: number

  smallBusinessReliefElection: SmallBusinessReliefStatus
  sbrEligible: boolean
  priorYear1Revenue: number
  priorYear2Revenue: number

  isQfzp: boolean
  qualifyingRevenue: number
  nonQualifyingRevenue: number
  deMinimisThreshold: number
  deMinimisSatisfied: boolean
  qualifyingIncome: number
  taxableNonQualifyingIncome: number

  exemptIncome: number
  unrealizedGainsLossesAdjustment: number
  entertainmentExpenditureTotal: number
  entertainmentExpenditureDisallowed: number
  finesAndPenaltiesDisallowed: number
  recoverableVatDisallowed: number
  donationsToUnapprovedEntitiesDisallowed: number

  netInterestExpense: number
  adjustedEbitda: number
  interestCapThreshold: number
  disallowedInterestExpense: number

  broughtForwardTaxLosses: number
  taxLossesUtilized: number
  carriedForwardTaxLosses: number

  adjustedTaxableIncome: number
  taxableIncomeSubjectToZeroPct: number
  taxableIncomeSubjectToNinePct: number
  corporateTaxPayableAed: number
  foreignTaxCreditReliefAed: number
  netTaxPayableAed: number
  netTaxPayableInr: number

  ftaSubmissionReference?: string | null
  preparedByName?: string | null
  approvedByName?: string | null
  approvedAt?: string | null
  partnerNotes?: string | null
  createdAt: string
}

export type ApproveUaeReturnRequest = {
  partnerNotes?: string
}

export type EmaraTaxSubmissionExportDto = {
  returnReference: string
  corporateTaxTrn: string
  legalEntityName: string
  formVersion: string
  submissionXml: string
  submissionJson: string
  totalTaxPayableAed: number
  totalTaxPayableInr: number
}

export const uaeTaxApi = {
  getProfiles: (clientId?: string) =>
    api<UaeTaxProfileDto[]>(`/api/uae-tax/profiles${clientId ? `?clientId=${clientId}` : ''}`),
  getProfile: (id: string) =>
    api<UaeTaxProfileDto>(`/api/uae-tax/profiles/${id}`),
  saveProfile: (req: CreateOrUpdateTaxProfileRequest) =>
    api<UaeTaxProfileDto>('/api/uae-tax/profiles', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  calculateTax: (req: CalculateUaeTaxRequest) =>
    api<UaeComputationResult>('/api/uae-tax/calculate', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  saveReturn: (req: SaveUaeCorporateTaxReturnRequest) =>
    api<UaeCorporateTaxReturnDto>('/api/uae-tax/returns', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  getReturn: (id: string) =>
    api<UaeCorporateTaxReturnDto>(`/api/uae-tax/returns/${id}`),
  getProfileReturns: (profileId: string) =>
    api<UaeCorporateTaxReturnDto[]>(`/api/uae-tax/profiles/${profileId}/returns`),
  approveReturn: (id: string, req: ApproveUaeReturnRequest) =>
    api<UaeCorporateTaxReturnDto>(`/api/uae-tax/returns/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  exportEmaraTax: (id: string) =>
    api<EmaraTaxSubmissionExportDto>(`/api/uae-tax/returns/${id}/export-emaratax`)
}

// ---------------------------------------------------------------- CA Practice Health, Succession & Valuation Desk
export type ValuationMethod = 'BlendedWeight' | 'DiscountedCashFlow' | 'RevenueMultiple' | 'EbitdaMultiple'
export type ValuationStatus = 'Draft' | 'Finalized' | 'ApprovedByPartners'
export type PartnerVestingStatus = 'FullyVested' | 'VestingInProgress' | 'Retired'
export type PartnerTransitionRole = 'SeniorPartner' | 'ManagingPartner' | 'JuniorPartner' | 'SuccessorInTraining' | 'RetiredHonorary'
export type RetirementPlanStatus = 'Draft' | 'ApprovedByPartnership' | 'ActivePayout' | 'FullySettled'
export type TransitionStage = 'Identified' | 'IntroductoryMeetingDone' | 'ShadowingAudit' | 'JointSignOff' | 'FullyHandedOver'
export type ClientRetentionRisk = 'Low' | 'Medium' | 'High'
export type FirmTierBenchmark = 'Tier1MetroBig10' | 'Tier2MidSizedMultiPartner' | 'Tier3BoutiqueSmall'

export type FirmTelemetryDto = {
  annualGrossBillings: number
  annualCollections: number
  activeClientsCount: number
  activePartnersCount: number
  activeStaffCount: number
  totalOutstandingReceivables: number
  estimatedDsoDays: number
  estimatedRealizationRate: number
  staffLeverageRatio: number
}

export type ProjectedCashFlowItem = {
  year: number
  projectedRevenue: number
  projectedEbitda: number
  freeCashFlow: number
  presentValue: number
}

export type PracticeValuationResultDto = {
  dcfEnterpriseValue: number
  multipleEnterpriseValue: number
  blendedFirmFairMarketValue: number
  firmGoodwillValue: number
  valuePerEquityPoint: number
  effectiveRevenueMultiple: number
  effectiveEbitdaMultiple: number
  projectedCashFlows: ProjectedCashFlowItem[]
}

export type CalculateValuationRequest = {
  annualGrossRevenue: number
  recurringComplianceRevenue: number
  advisoryAndOneOffRevenue: number
  ebitda: number
  partnerNormalizedCompensation: number
  netWorkingCapital: number
  netTangibleAssets: number
  projectedRevenueGrowthRate: number
  discountRateWacc: number
  terminalGrowthRate: number
  forecastYears: number
  revenueMultipleBase: number
  ebitdaMultipleBase: number
  auditFranchiseStickinessPremiumPct: number
  partnerConcentrationDiscountPct: number
  recurringRevenuePremiumPct: number
  method: ValuationMethod
}

export type SaveValuationRequest = {
  valuationName: string
  valuationDate: string
  method: ValuationMethod
  annualGrossRevenue: number
  recurringComplianceRevenue: number
  advisoryAndOneOffRevenue: number
  ebitda: number
  partnerNormalizedCompensation: number
  netWorkingCapital: number
  netTangibleAssets: number
  projectedRevenueGrowthRate: number
  discountRateWacc: number
  terminalGrowthRate: number
  forecastYears: number
  revenueMultipleBase: number
  ebitdaMultipleBase: number
  auditFranchiseStickinessPremiumPct: number
  partnerConcentrationDiscountPct: number
  recurringRevenuePremiumPct: number
  notes?: string
}

export type PracticeValuationDto = {
  id: string
  valuationName: string
  valuationDate: string
  method: ValuationMethod
  status: ValuationStatus
  annualGrossRevenue: number
  recurringComplianceRevenue: number
  advisoryAndOneOffRevenue: number
  ebitda: number
  partnerNormalizedCompensation: number
  netWorkingCapital: number
  netTangibleAssets: number
  projectedRevenueGrowthRate: number
  discountRateWacc: number
  terminalGrowthRate: number
  forecastYears: number
  revenueMultipleBase: number
  ebitdaMultipleBase: number
  auditFranchiseStickinessPremiumPct: number
  partnerConcentrationDiscountPct: number
  recurringRevenuePremiumPct: number
  dcfEnterpriseValue: number
  multipleEnterpriseValue: number
  blendedFirmFairMarketValue: number
  firmGoodwillValue: number
  valuePerEquityPoint: number
  projectedCashFlows: ProjectedCashFlowItem[]
  preparedByName?: string | null
  approvedByName?: string | null
  approvedAt?: string | null
  notes?: string | null
  createdAt: string
}

export type PartnerEquityDto = {
  id: string
  userId: string
  partnerName: string
  partnerEmail: string
  partnerMembershipNo: string
  equityPercentage: number
  capitalAccountBalance: number
  joiningDate: string
  targetRetirementDate?: string | null
  vestingStatus: PartnerVestingStatus
  vestedPercentage: number
  annualDrawShare: number
  role: PartnerTransitionRole
  isActive: boolean
  createdAt: string
}

export type SavePartnerEquityRequest = {
  id?: string
  userId: string
  partnerName: string
  partnerEmail: string
  partnerMembershipNo: string
  equityPercentage: number
  capitalAccountBalance: number
  joiningDate: string
  targetRetirementDate?: string | null
  vestingStatus: PartnerVestingStatus
  vestedPercentage: number
  annualDrawShare: number
  role: PartnerTransitionRole
}

export type AmortizationInstallment = {
  year: number
  beginningBalance: number
  principalPaid: number
  interestPaid: number
  totalInstallment: number
  endingBalance: number
}

export type CreateRetirementPlanRequest = {
  partnerEquityId: string
  retirementDate: string
  agreedFirmValuationAtRetirement: number
  capitalAccountRefundAmount: number
  payoutPeriodYears: number
  annualInterestRatePct: number
  partnershipNotes?: string
}

export type PartnerRetirementPlanDto = {
  id: string
  partnerEquityId: string
  partnerName: string
  retirementDate: string
  agreedFirmValuationAtRetirement: number
  partnerEquityPct: number
  grossEquityValue: number
  capitalAccountRefundAmount: number
  goodwillBuyoutAmount: number
  payoutPeriodYears: number
  annualInterestRatePct: number
  annualInstallmentAmount: number
  status: RetirementPlanStatus
  amortizationSchedule: AmortizationInstallment[]
  partnershipNotes?: string | null
  createdAt: string
}

export type SaveClientTransitionRequest = {
  id?: string
  retiringPartnerId: string
  successorPartnerId: string
  clientId: string
  annualClientFees: number
  stage: TransitionStage
  risk: ClientRetentionRisk
  targetHandoverDate: string
  handoverNotes?: string
}

export type ClientSuccessionTransitionDto = {
  id: string
  retiringPartnerId: string
  retiringPartnerName: string
  successorPartnerId: string
  successorPartnerName: string
  clientId: string
  clientName: string
  annualClientFees: number
  stage: TransitionStage
  risk: ClientRetentionRisk
  targetHandoverDate: string
  handoverNotes?: string | null
  createdAt: string
}

export type GenerateBenchmarkRequest = {
  benchmarkYear: number
  firmTier: FirmTierBenchmark
  overrideActualRevenuePerPartner?: number
  overrideActualRealizationRate?: number
  overrideActualDsoDays?: number
  overrideActualStaffLeverage?: number
  overrideActualEbitdaMargin?: number
}

export type PracticeBenchmarkDto = {
  id: string
  benchmarkYear: number
  firmTier: FirmTierBenchmark
  actualRevenuePerPartner: number
  actualRevenuePerEmployee: number
  actualRealizationRate: number
  actualDsoDays: number
  actualStaffLeverageRatio: number
  actualWipLockupDays: number
  actualEbitdaMarginPct: number
  benchmarkRevenuePerPartner: number
  benchmarkRevenuePerEmployee: number
  benchmarkRealizationRate: number
  benchmarkDsoDays: number
  benchmarkStaffLeverageRatio: number
  benchmarkWipLockupDays: number
  benchmarkEbitdaMarginPct: number
  overallHealthScore: number
  healthGrade: string
  insightsChecklist: string[]
  createdAt: string
}

export const practiceHealthApi = {
  getTelemetry: () =>
    api<FirmTelemetryDto>('/api/practice-health/telemetry'),
  calculateValuation: (req: CalculateValuationRequest) =>
    api<PracticeValuationResultDto>('/api/practice-health/valuation/calculate', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  saveValuation: (req: SaveValuationRequest) =>
    api<PracticeValuationDto>('/api/practice-health/valuation', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  getValuations: () =>
    api<PracticeValuationDto[]>('/api/practice-health/valuation'),
  getValuation: (id: string) =>
    api<PracticeValuationDto>(`/api/practice-health/valuation/${id}`),
  approveValuation: (id: string) =>
    api<PracticeValuationDto>(`/api/practice-health/valuation/${id}/approve`, {
      method: 'POST'
    }),
  getEquities: () =>
    api<PartnerEquityDto[]>('/api/practice-health/equity'),
  saveEquity: (req: SavePartnerEquityRequest) =>
    api<PartnerEquityDto>('/api/practice-health/equity', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteEquity: (id: string) =>
    api<void>(`/api/practice-health/equity/${id}`, {
      method: 'DELETE'
    }),
  getRetirementPlans: () =>
    api<PartnerRetirementPlanDto[]>('/api/practice-health/retirement-plans'),
  createRetirementPlan: (req: CreateRetirementPlanRequest) =>
    api<PartnerRetirementPlanDto>('/api/practice-health/retirement-plans', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  updateRetirementStatus: (id: string, status: RetirementPlanStatus) =>
    api<PartnerRetirementPlanDto>(`/api/practice-health/retirement-plans/${id}/status?status=${status}`, {
      method: 'PATCH'
    }),
  getTransitions: () =>
    api<ClientSuccessionTransitionDto[]>('/api/practice-health/transitions'),
  saveTransition: (req: SaveClientTransitionRequest) =>
    api<ClientSuccessionTransitionDto>('/api/practice-health/transitions', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteTransition: (id: string) =>
    api<void>(`/api/practice-health/transitions/${id}`, {
      method: 'DELETE'
    }),
  generateBenchmark: (req: GenerateBenchmarkRequest) =>
    api<PracticeBenchmarkDto>('/api/practice-health/benchmarks/generate', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  getBenchmarks: () =>
    api<PracticeBenchmarkDto[]>('/api/practice-health/benchmarks')
}

// ==========================================
// Bank & Ledger Auto-Reconciliation Engine (Tally / Busy Sync)
// ==========================================

export type ErpType = 'TallyPrime' | 'BusyAccounting'
export type AgentSyncStatus = 'Offline' | 'Connected' | 'Syncing' | 'Error'
export type ErpVoucherType = 'Payment' | 'Receipt' | 'Journal' | 'Contra' | 'Sales' | 'Purchase'
export type VoucherReconciliationStatus = 'Unreconciled' | 'AutoMatched' | 'ManuallyReconciled' | 'Disputed' | 'Excluded'
export type OutgoingVoucherStatus = 'PendingApproval' | 'QueuedForAgent' | 'PostedToErp' | 'Failed' | 'Rejected'
export type AuditReasonCategory = 'DepreciationSec32' | 'TaxProvisionAy2526' | 'Sa700AuditAdjustment' | 'PrepaidAmortization' | 'BadDebtsWriteoff' | 'ForeignExchangeFxAdjustment' | 'OtherAuditEntry'
export type ReconciliationMatchRule = 'ExactAmountAndDate' | 'ChequeOrUtrMatch' | 'TdsNetDeductionMatch' | 'ToleranceRoundingMatch' | 'MultiInvoiceBundleMatch'
export type ReconciliationMatchStatus = 'Suggested' | 'Approved' | 'Rejected'

export type ErpSyncAgentConfigDto = {
  id: string
  clientId: string
  clientName: string
  clientCode: string
  agentName: string
  agentToken: string
  erpType: ErpType
  hostUrl: string
  companyName: string
  syncStatus: AgentSyncStatus
  lastHeartbeatAt?: string | null
  lastSyncAt?: string | null
  syncFrequencyMinutes: number
  syncDaybook: boolean
  syncMasters: boolean
  syncBankFeeds: boolean
  totalVouchersSynced: number
  totalMastersSynced: number
  isActive: boolean
  createdAt: string
}

export type CreateOrUpdateAgentConfigRequest = {
  id?: string
  clientId: string
  agentName: string
  erpType: ErpType
  hostUrl: string
  companyName: string
  syncFrequencyMinutes: number
  syncDaybook: boolean
  syncMasters: boolean
  syncBankFeeds: boolean
}

export type ErpLedgerVoucherDto = {
  id: string
  clientId: string
  clientName: string
  agentConfigId: string
  erpType: ErpType
  voucherNumber: string
  voucherGuid: string
  voucherType: ErpVoucherType
  voucherDate: string
  effectiveDate: string
  amount: number
  debitLedger: string
  creditLedger: string
  narration: string
  chequeNumber?: string | null
  instrumentDate?: string | null
  bankPartyName?: string | null
  bankClearanceDate?: string | null
  reconciliationStatus: VoucherReconciliationStatus
  createdAt: string
}

export type CreateOutgoingVoucherRequest = {
  clientId: string
  agentConfigId: string
  voucherType: ErpVoucherType
  voucherDate: string
  referenceNumber: string
  debitLedger: string
  creditLedger: string
  amount: number
  narration: string
  auditReason: AuditReasonCategory
}

export type ErpOutgoingVoucherDto = {
  id: string
  clientId: string
  clientName: string
  agentConfigId: string
  voucherType: ErpVoucherType
  voucherDate: string
  referenceNumber: string
  debitLedger: string
  creditLedger: string
  amount: number
  narration: string
  auditReason: AuditReasonCategory
  postingStatus: OutgoingVoucherStatus
  generatedXmlPayload?: string | null
  erpVoucherNumber?: string | null
  errorMessage?: string | null
  authorizedByName?: string | null
  authorizedAt?: string | null
  createdAt: string
}

export type RunAutoReconciliationRequest = {
  clientId: string
  bankAccountId: string
  fromDate?: string | null
  toDate?: string | null
}

export type BankLedgerReconciliationMatchDto = {
  id: string
  clientId: string
  clientName: string
  bankAccountId: string
  bankTransactionId: string
  transactionDate: string
  narration: string
  transactionType: string
  erpLedgerVoucherId?: string | null
  erpVoucherNumber?: string | null
  voucherDate?: string | null
  debitLedger?: string | null
  creditLedger?: string | null
  matchRule: ReconciliationMatchRule
  confidenceScore: number
  bankAmount: number
  ledgerAmount: number
  varianceAmount: number
  varianceExplanation?: string | null
  matchedVoucherNumbers: string
  status: ReconciliationMatchStatus
  reconciledByName?: string | null
  reconciledAt?: string | null
  createdAt: string
}

export type ReconcileMatchActionRequest = {
  status: ReconciliationMatchStatus
  notes?: string
}

export type ReconciliationSummaryDto = {
  totalBankTransactions: number
  reconciledCount: number
  pendingCount: number
  totalReconciledAmount: number
  totalUnreconciledAmount: number
  reconciledPercentage: number
  highConfidenceSuggestionsCount: number
}

export const ledgerSyncApi = {
  getAgentConfigs: (clientId?: string) =>
    api<ErpSyncAgentConfigDto[]>(`/api/ledger-sync/agents${clientId ? `?clientId=${clientId}` : ''}`),
  getAgentConfig: (id: string) =>
    api<ErpSyncAgentConfigDto>(`/api/ledger-sync/agents/${id}`),
  saveAgentConfig: (req: CreateOrUpdateAgentConfigRequest) =>
    api<ErpSyncAgentConfigDto>('/api/ledger-sync/agents', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteAgentConfig: (id: string) =>
    api<void>(`/api/ledger-sync/agents/${id}`, {
      method: 'DELETE'
    }),
  getVouchers: (clientId?: string, fromDate?: string, toDate?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.append('clientId', clientId)
    if (fromDate) params.append('fromDate', fromDate)
    if (toDate) params.append('toDate', toDate)
    const qs = params.toString()
    return api<ErpLedgerVoucherDto[]>(`/api/ledger-sync/vouchers${qs ? `?${qs}` : ''}`)
  },
  createOutgoingVoucher: (req: CreateOutgoingVoucherRequest) =>
    api<ErpOutgoingVoucherDto>('/api/ledger-sync/outgoing-vouchers', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  authorizeOutgoingVoucher: (id: string) =>
    api<ErpOutgoingVoucherDto>(`/api/ledger-sync/outgoing-vouchers/${id}/authorize`, {
      method: 'POST'
    }),
  getOutgoingVouchers: (clientId?: string) =>
    api<ErpOutgoingVoucherDto[]>(`/api/ledger-sync/outgoing-vouchers${clientId ? `?clientId=${clientId}` : ''}`),
  runAutoReconciliation: (req: RunAutoReconciliationRequest) =>
    api<BankLedgerReconciliationMatchDto[]>('/api/ledger-sync/reconciliation/auto-match', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  getReconciliationMatches: (clientId?: string, bankAccountId?: string) => {
    const params = new URLSearchParams()
    if (clientId) params.append('clientId', clientId)
    if (bankAccountId) params.append('bankAccountId', bankAccountId)
    const qs = params.toString()
    return api<BankLedgerReconciliationMatchDto[]>(`/api/ledger-sync/reconciliation/matches${qs ? `?${qs}` : ''}`)
  },
  processReconciliationAction: (id: string, req: ReconcileMatchActionRequest) =>
    api<BankLedgerReconciliationMatchDto>(`/api/ledger-sync/reconciliation/matches/${id}/action`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  getReconciliationSummary: (clientId: string, bankAccountId: string) =>
    api<ReconciliationSummaryDto>(`/api/ledger-sync/reconciliation/summary?clientId=${clientId}&bankAccountId=${bankAccountId}`)
}

// ==========================================
// Tax Appeals & Rule 18 Paper Book Desk
// ==========================================

export type AppealForum = 'CitAppeals' | 'Itat' | 'HighCourt' | 'SupremeCourt'
export type TaxAppealStatus = 'Drafting' | 'PartnerReviewed' | 'Certified' | 'Filed' | 'HearingScheduled' | 'HeardReserved' | 'DisposedAllowed' | 'DisposedPartlyAllowed' | 'DisposedDismissed'
export type GroundType = 'General' | 'JurisdictionOrLimitation' | 'NaturalJustice' | 'MeritsAddition' | 'Alternative' | 'WithoutPrejudice'
export type PaperBookEvidenceType = 'PartLowerAuthoritiesRecord' | 'Rule29AdditionalEvidence'

export type TaxAppealDto = {
  id: string
  clientId: string
  clientName: string
  clientPan: string
  taxNoticeId?: string | null
  noticeReferenceNumber?: string | null
  forum: AppealForum
  appealNumber?: string | null
  assessmentYear: string
  financialYear: string
  assessingOfficerWardOrCircle: string
  impugnedOrderSection: string
  impugnedOrderNumber: string
  impugnedOrderDate: string
  dateOfServiceOfOrder: string
  statutoryFilingDeadline: string
  daysRemaining: number
  delayCondonationRequired: boolean
  daysOfDelay: number
  filingFeeAmount: number
  assessedTotalIncome: number
  returnedTotalIncome: number
  totalDisputedAdditions: number
  disputedTaxDemand: number
  filingDate?: string | null
  dinOrAckNumber?: string | null
  status: TaxAppealStatus
  partnerMembershipNumber?: string | null
  partnerCertifiedByName?: string | null
  partnerCertifiedAt?: string | null
  groundsCount: number
  documentsCount: number
  citationsCount: number
  createdAt: string
}

export type TaxAppealDetailDto = TaxAppealDto & {
  condonationReason?: string | null
  statementOfFacts: string
  reliefSought: string
  grounds: AppealGroundItemDto[]
  paperBookDocuments: PaperBookDocumentDto[]
  citations: AppealJudicialCitationDto[]
}

export type AppealGroundItemDto = {
  id: string
  taxAppealId: string
  groundNumber: number
  groundType: GroundType
  title: string
  groundText: string
  statutorySection: string
  disputedAdditionAmount: number
  isWithoutPrejudice: boolean
  isAlternativeGround: boolean
  orderIndex: number
}

export type PaperBookDocumentDto = {
  id: string
  taxAppealId: string
  itemNumber: number
  documentDate: string
  descriptionOfDocument: string
  relevance: string
  evidenceType: PaperBookEvidenceType
  lowerAuthorityFilingDate?: string | null
  pageNumberFrom: number
  pageNumberTo: number
  totalPages: number
  isCertified: boolean
  orderIndex: number
}

export type AppealJudicialCitationDto = {
  id: string
  taxAppealId: string
  precedentId?: string | null
  caseTitle: string
  citation: string
  courtOrTribunal: string
  judgmentYear: number
  statutorySection: string
  keyRatioProposition: string
  paragraphReference: string
  relevanceToGround: string
}

export type CreateOrUpdateTaxAppealRequest = {
  id?: string
  clientId: string
  taxNoticeId?: string | null
  noticeReferenceNumber?: string | null
  forum: AppealForum
  appealNumber?: string | null
  assessmentYear: string
  financialYear: string
  assessingOfficerWardOrCircle: string
  impugnedOrderSection: string
  impugnedOrderNumber: string
  impugnedOrderDate: string
  dateOfServiceOfOrder: string
  assessedTotalIncome: number
  returnedTotalIncome: number
  totalDisputedAdditions: number
  disputedTaxDemand: number
  condonationReason?: string | null
  statementOfFacts: string
  reliefSought: string
}

export type SaveAppealGroundRequest = {
  id?: string
  taxAppealId: string
  groundNumber: number
  groundType: GroundType
  title: string
  groundText: string
  statutorySection: string
  disputedAdditionAmount: number
  isWithoutPrejudice: boolean
  isAlternativeGround: boolean
  orderIndex: number
}

export type SavePaperBookDocRequest = {
  id?: string
  taxAppealId: string
  itemNumber: number
  documentDate: string
  descriptionOfDocument: string
  relevance: string
  evidenceType: PaperBookEvidenceType
  lowerAuthorityFilingDate?: string | null
  pageCount: number
  isCertified: boolean
  orderIndex: number
}

export type AddJudicialCitationRequest = {
  taxAppealId: string
  precedentId?: string | null
  caseTitle: string
  citation: string
  courtOrTribunal: string
  judgmentYear: number
  statutorySection: string
  keyRatioProposition: string
  paragraphReference: string
  relevanceToGround: string
}

export type CertifyAppealRequest = {
  partnerMembershipNumber: string
  partnerName: string
  filingDate?: string | null
  dinOrAckNumber?: string | null
}

export type CalculateFilingFeeRequest = {
  forum: AppealForum
  assessedTotalIncome: number
  isOtherMatter: boolean
}

export type FilingFeeResultDto = {
  forum: AppealForum
  assessedTotalIncome: number
  statutoryFilingFee: number
  legalBasis: string
}

export const taxLitigationApi = {
  getAppeals: (clientId?: string) =>
    api<TaxAppealDto[]>(`/api/tax-litigation/appeals${clientId ? `?clientId=${clientId}` : ''}`),
  getAppeal: (id: string) =>
    api<TaxAppealDetailDto>(`/api/tax-litigation/appeals/${id}`),
  saveAppeal: (req: CreateOrUpdateTaxAppealRequest) =>
    api<TaxAppealDetailDto>('/api/tax-litigation/appeals', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteAppeal: (id: string) =>
    api<void>(`/api/tax-litigation/appeals/${id}`, {
      method: 'DELETE'
    }),
  saveGround: (req: SaveAppealGroundRequest) =>
    api<AppealGroundItemDto>('/api/tax-litigation/grounds', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteGround: (id: string) =>
    api<void>(`/api/tax-litigation/grounds/${id}`, {
      method: 'DELETE'
    }),
  savePaperBookDoc: (req: SavePaperBookDocRequest) =>
    api<PaperBookDocumentDto[]>('/api/tax-litigation/paper-book', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deletePaperBookDoc: (id: string) =>
    api<PaperBookDocumentDto[]>(`/api/tax-litigation/paper-book/${id}`, {
      method: 'DELETE'
    }),
  addCitation: (req: AddJudicialCitationRequest) =>
    api<AppealJudicialCitationDto>('/api/tax-litigation/citations', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteCitation: (id: string) =>
    api<void>(`/api/tax-litigation/citations/${id}`, {
      method: 'DELETE'
    }),
  certifyAppeal: (id: string, req: CertifyAppealRequest) =>
    api<TaxAppealDetailDto>(`/api/tax-litigation/appeals/${id}/certify`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  getDossierHtmlUrl: (id: string) => `/api/tax-litigation/appeals/${id}/dossier`,
  calculateFilingFee: (req: CalculateFilingFeeRequest) =>
    api<FilingFeeResultDto>('/api/tax-litigation/filing-fee/calculate', {
      method: 'POST',
      body: JSON.stringify(req)
    })
}

// -----------------------------------------------------------------------------
// SPRINT 60: SEBI BRSR CORE & COMPANIES ACT SEC 135 CSR AUDIT ENGINE
// -----------------------------------------------------------------------------

export type ScheduleViiCategory =
  | 'EradicatingHungerPovertyAndMalnutrition'
  | 'PromotingEducationAndSkillDevelopment'
  | 'GenderEqualityAndWomenEmpowerment'
  | 'EnvironmentalSustainabilityAndAnimalWelfare'
  | 'ProtectionOfNationalHeritageArtAndCulture'
  | 'MeasuresForArmedForcesVeteransAndWarWidows'
  | 'RuralAndNationallyRecognisedSports'
  | 'PrimeMinisterNationalReliefFundOrPmCares'
  | 'TechnologyIncubatorsAndResearchInstitutions'
  | 'RuralDevelopmentProjects'
  | 'SlumAreaDevelopment'
  | 'DisasterManagementAndRelief'

export type CsrProjectNature = 'OngoingProject' | 'OtherThanOngoingProject'

export type CsrComplianceStatus =
  | 'Draft'
  | 'InReview'
  | 'FullyCompliant'
  | 'UnspentTransferredToEscrow'
  | 'ExcessSpentCarriedForward'
  | 'NonCompliantDefault'
  | 'Certified'

export interface CsrAssessmentDto {
  id: string
  clientId: string
  clientName: string
  clientPan: string
  financialYear: string
  netWorth: number
  turnover: number
  netProfitPreviousYear: number
  isCsrApplicable: boolean
  applicabilityCriterion: string
  averageNetProfit: number
  mandatoryCsrObligation2Pct: number
  netCsrObligationForCurrentFy: number
  totalActualCsrSpent: number
  unspentOngoingProjects: number
  unspentOtherProjects: number
  excessSpentCarriedForward: number
  caroQualificationRequired: boolean
  caroClauseXxARemark: string
  caroClauseXxBRemark: string
  status: CsrComplianceStatus
  certifiedByName?: string
  certifiedMembershipNo?: string
  certifiedAt?: string
  udin?: string
  projectsCount: number
  createdAt: string
}

export interface CsrAssessmentDetailDto {
  id: string
  clientId: string
  clientName: string
  clientPan: string
  financialYear: string
  netWorth: number
  turnover: number
  netProfitPreviousYear: number
  isCsrApplicable: boolean
  applicabilityCriterion: string
  netProfitFyMinus1: number
  netProfitFyMinus2: number
  netProfitFyMinus3: number
  averageNetProfit: number
  mandatoryCsrObligation2Pct: number
  setOffAvailableFromPriorYears: number
  netCsrObligationForCurrentFy: number
  totalActualCsrSpent: number
  totalSpentOngoingProjects: number
  totalSpentOtherProjects: number
  administrativeOverheadsSpent: number
  impactAssessmentSpent: number
  unspentOngoingProjects: number
  unspentOtherProjects: number
  unspentOngoingTransferDate?: string
  scheduleViiFundTransferDate?: string
  excessSpentCarriedForward: number
  caroClauseXxARemark: string
  caroClauseXxBRemark: string
  caroQualificationRequired: boolean
  status: CsrComplianceStatus
  certifiedByName?: string
  certifiedMembershipNo?: string
  certifiedAt?: string
  udin?: string
  projects: CsrProjectItemDto[]
  createdAt: string
}

export interface CsrProjectItemDto {
  id: string
  csrAssessmentId: string
  projectTitle: string
  scheduleViiCategory: ScheduleViiCategory
  nature: CsrProjectNature
  stateOrDistrict: string
  isAspirationalDistrict: boolean
  totalProjectOutlay: number
  allocatedBudget: number
  actualSpent: number
  unspentAmount: number
  implementationMode: string
  implementingAgencyName?: string
  implementingAgencyCsr1Number?: string
  projectStatus: string
  createdAt: string
}

export interface BrsrCoreReportDto {
  id: string
  clientId: string
  clientName: string
  financialYear: string
  turnoverInCrores: number
  dieselConsumedLitres: number
  naturalGasConsumedSm3: number
  gridElectricityPurchasedMwh: number
  scope1DirectEmissionsMtCo2e: number
  scope2IndirectEmissionsMtCo2e: number
  totalScope1And2MtCo2e: number
  ghgIntensityPerCroreTurnover: number
  totalWaterWithdrawalKl: number
  totalWaterConsumptionKl: number
  waterIntensityPerCroreTurnover: number
  operatesZeroLiquidDischarge: boolean
  totalEnergyConsumptionGj: number
  energyIntensityPerCroreTurnover: number
  renewableEnergySharePct: number
  lostTimeInjuryFrequencyRateLtifr: number
  fatalitiesCount: number
  poshComplaintsReceived: number
  poshComplaintsResolved: number
  medianRemunerationLakhs: number
  totalPermanentEmployees: number
  femaleEmployeesCount: number
  femaleEmployeesPct: number
  totalBoardDirectors: number
  femaleBoardDirectors: number
  femaleBoardPct: number
  totalKmpCount: number
  femaleKmpCount: number
  femaleKmpPct: number
  overallBrsrCoreScore: number
  assuranceType: string
  assuranceAuditorName?: string
  assuranceAuditorMembershipNo?: string
  assuranceUdin?: string
  assuranceCertifiedAt?: string
  createdAt: string
}

export interface SaveCsrAssessmentRequest {
  id?: string
  clientId: string
  financialYear: string
  netWorth: number
  turnover: number
  netProfitPreviousYear: number
  netProfitFyMinus1: number
  netProfitFyMinus2: number
  netProfitFyMinus3: number
  setOffAvailableFromPriorYears: number
  administrativeOverheadsSpent: number
  impactAssessmentSpent: number
  unspentOngoingTransferDate?: string
  scheduleViiFundTransferDate?: string
}

export interface SaveCsrProjectRequest {
  id?: string
  csrAssessmentId: string
  projectTitle: string
  scheduleViiCategory: ScheduleViiCategory
  nature: CsrProjectNature
  stateOrDistrict: string
  isAspirationalDistrict: boolean
  totalProjectOutlay: number
  allocatedBudget: number
  actualSpent: number
  implementationMode: string
  implementingAgencyName?: string
  implementingAgencyCsr1Number?: string
  projectStatus: string
}

export interface SaveBrsrReportRequest {
  id?: string
  clientId: string
  financialYear: string
  turnoverInCrores: number
  dieselConsumedLitres: number
  naturalGasConsumedSm3: number
  gridElectricityPurchasedMwh: number
  totalWaterWithdrawalKl: number
  totalWaterConsumptionKl: number
  operatesZeroLiquidDischarge: boolean
  totalEnergyConsumptionGj: number
  renewableEnergySharePct: number
  lostTimeInjuryFrequencyRateLtifr: number
  fatalitiesCount: number
  poshComplaintsReceived: number
  poshComplaintsResolved: number
  medianRemunerationLakhs: number
  totalPermanentEmployees: number
  femaleEmployeesCount: number
  totalBoardDirectors: number
  femaleBoardDirectors: number
  totalKmpCount: number
  femaleKmpCount: number
  assuranceType: string
}

export interface CertifyCsrRequest {
  auditorName: string
  membershipNo: string
  udin: string
}

export interface CertifyBrsrRequest {
  auditorName: string
  membershipNo: string
  udin: string
  assuranceType: string
}

export interface CalculateCsrObligationRequest {
  netWorth: number
  turnover: number
  netProfitPreviousYear: number
  netProfitFyMinus1: number
  netProfitFyMinus2: number
  netProfitFyMinus3: number
  setOffAvailableFromPriorYears: number
  totalSpentOngoing: number
  totalSpentOther: number
  ongoingTransferDate?: string
  scheduleViiTransferDate?: string
}

export interface CsrObligationCalculationDto {
  isApplicable: boolean
  applicabilityReason: string
  averageNetProfit: number
  mandatoryCsrObligation2Pct: number
  netCsrObligationCurrentFy: number
  totalSpent: number
  unspentOngoing: number
  unspentOther: number
  excessCarriedForward: number
  caroQualificationRequired: boolean
  caroClauseXxARemark: string
  caroClauseXxBRemark: string
}

export const esgCsrApi = {
  getAssessments: (clientId?: string) =>
    api<CsrAssessmentDto[]>(`/api/esg-csr/assessments${clientId ? `?clientId=${clientId}` : ''}`),
  getAssessment: (id: string) =>
    api<CsrAssessmentDetailDto>(`/api/esg-csr/assessments/${id}`),
  saveAssessment: (req: SaveCsrAssessmentRequest) =>
    api<CsrAssessmentDetailDto>('/api/esg-csr/assessments', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteAssessment: (id: string) =>
    api<void>(`/api/esg-csr/assessments/${id}`, {
      method: 'DELETE'
    }),
  saveProject: (req: SaveCsrProjectRequest) =>
    api<CsrProjectItemDto>('/api/esg-csr/projects', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteProject: (id: string) =>
    api<void>(`/api/esg-csr/projects/${id}`, {
      method: 'DELETE'
    }),
  getBrsrReports: (clientId?: string) =>
    api<BrsrCoreReportDto[]>(`/api/esg-csr/brsr${clientId ? `?clientId=${clientId}` : ''}`),
  getBrsrReport: (id: string) =>
    api<BrsrCoreReportDto>(`/api/esg-csr/brsr/${id}`),
  getBrsrReportByClientAndYear: (clientId: string, financialYear: string) =>
    api<BrsrCoreReportDto>(`/api/esg-csr/brsr/client/${clientId}/${financialYear}`),
  saveBrsrReport: (req: SaveBrsrReportRequest) =>
    api<BrsrCoreReportDto>('/api/esg-csr/brsr', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteBrsrReport: (id: string) =>
    api<void>(`/api/esg-csr/brsr/${id}`, {
      method: 'DELETE'
    }),
  certifyCsr: (id: string, req: CertifyCsrRequest) =>
    api<CsrAssessmentDetailDto>(`/api/esg-csr/assessments/${id}/certify`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  certifyBrsr: (id: string, req: CertifyBrsrRequest) =>
    api<BrsrCoreReportDto>(`/api/esg-csr/brsr/${id}/certify`, {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  getDossierHtmlUrl: (id: string) => `/api/esg-csr/brsr/${id}/dossier`,
  calculateObligation: (req: CalculateCsrObligationRequest) =>
    api<CsrObligationCalculationDto>('/api/esg-csr/calculate-obligation', {
      method: 'POST',
      body: JSON.stringify(req)
    })
}

// -----------------------------------------------------------------------------
// SPRINT 61: IBC 2016 INSOLVENCY & CIRP STATUTORY DESK
// -----------------------------------------------------------------------------

export type CirpStage =
  | 'AdmissionAndMoratorium'
  | 'PublicAnnouncementAndClaims'
  | 'CocConstituted'
  | 'InformationMemorandumIssued'
  | 'FormGInvited'
  | 'ResolutionPlanReview'
  | 'ResolutionPlanApproved'
  | 'LiquidationInitiated'
  | 'Section12AWithdrawn'

export type CreditorCategory =
  | 'SecuredFinancialCreditor'
  | 'UnsecuredFinancialCreditor'
  | 'OperationalCreditorEmployees'
  | 'OperationalCreditorGovernment'
  | 'OperationalCreditorVendors'

export type PufeType =
  | 'PreferentialSec43'
  | 'UndervaluedSec45'
  | 'ExtortionateCreditSec50'
  | 'FraudulentTradingSec66'

export type AssetValuationClass =
  | 'LandAndBuilding'
  | 'PlantAndMachinery'
  | 'SecuritiesAndFinancialAssets'

export type VotingResolutionStatus = 'Pending' | 'Approved' | 'Rejected'

export interface CirpCaseDto {
  id: string
  clientId: string
  clientName: string
  clientPan: string
  corporateDebtorName: string
  cin: string
  ncltBench: string
  courtOrderNumber: string
  admissionDate: string
  irpName: string
  irpIpRegNumber: string
  rpName: string
  rpIpRegNumber: string
  moratoriumSection14Declared: boolean
  claimsLastDate: string
  initial180DaysDeadline: string
  extended270DaysDeadline: string
  final330DaysLimit: string
  daysElapsed: number
  daysRemainingTo180: number
  daysRemainingTo330: number
  is180DaysExpired: boolean
  is330DaysCapBreached: boolean
  timelineStatusAlert: string
  stage: CirpStage
  totalAdmittedFinancialDebt: number
  totalAdmittedOperationalDebt: number
  statusNote: string
  membersCount: number
  resolutionsCount: number
  pufeCount: number
  valuationsCount: number
  createdAt: string
}

export interface CirpCaseDetailDto {
  id: string
  clientId: string
  clientName: string
  clientPan: string
  corporateDebtorName: string
  cin: string
  ncltBench: string
  courtOrderNumber: string
  admissionDate: string
  irpName: string
  irpIpRegNumber: string
  rpName: string
  rpIpRegNumber: string
  moratoriumSection14Declared: boolean
  claimsLastDate: string
  initial180DaysDeadline: string
  extended270DaysDeadline: string
  final330DaysLimit: string
  daysElapsed: number
  daysRemainingTo180: number
  daysRemainingTo330: number
  is180DaysExpired: boolean
  is330DaysCapBreached: boolean
  timelineStatusAlert: string
  stage: CirpStage
  totalAdmittedFinancialDebt: number
  totalAdmittedOperationalDebt: number
  statusNote: string
  cocMembers: CocMemberDto[]
  resolutions: CocResolutionItemDto[]
  pufeTransactions: PufeTransactionItemDto[]
  valuations: AssetValuationItemDto[]
  createdAt: string
}

export interface CocMemberDto {
  id: string
  cirpCaseId: string
  creditorName: string
  category: CreditorCategory
  claimSubmittedAmount: number
  admittedClaimAmount: number
  votingSharePct: number
  securityInterestDescription: string
  isRelatedParty: boolean
  createdAt: string
}

export interface CocResolutionItemDto {
  id: string
  cirpCaseId: string
  meetingNumber: number
  resolutionNumber: string
  resolutionTitle: string
  statutorySection: string
  requiredThresholdPct: number
  votingShareInFavourPct: number
  votingShareAgainstPct: number
  votingShareAbstainedPct: number
  status: VotingResolutionStatus
  createdAt: string
}

export interface PufeTransactionItemDto {
  id: string
  cirpCaseId: string
  pufeType: PufeType
  transactionDate: string
  counterpartyName: string
  isRelatedParty: boolean
  transactionAmount: number
  estimatedAvoidanceRecoverableAmount: number
  lookbackPeriodYears: number
  isWithinLookbackPeriod: boolean
  forensicAuditorObservations: string
  ncltApplicationFiled: boolean
  createdAt: string
}

export interface AssetValuationItemDto {
  id: string
  cirpCaseId: string
  assetClass: AssetValuationClass
  assetDescription: string
  valuer1Name: string
  valuer1FairValue: number
  valuer1LiquidationValue: number
  valuer2Name: string
  valuer2FairValue: number
  valuer2LiquidationValue: number
  averageFairValue: number
  averageLiquidationValue: number
  thirdValuerRequired: boolean
  createdAt: string
}

export interface SaveCirpCaseRequest {
  id?: string
  clientId: string
  corporateDebtorName: string
  cin: string
  ncltBench: string
  courtOrderNumber: string
  admissionDate: string
  irpName: string
  irpIpRegNumber: string
  rpName: string
  rpIpRegNumber: string
  moratoriumSection14Declared: boolean
  stage: CirpStage
  statusNote: string
}

export interface SaveCocMemberRequest {
  id?: string
  cirpCaseId: string
  creditorName: string
  category: CreditorCategory
  claimSubmittedAmount: number
  admittedClaimAmount: number
  securityInterestDescription: string
  isRelatedParty: boolean
}

export interface SaveCocResolutionRequest {
  id?: string
  cirpCaseId: string
  meetingNumber: number
  resolutionNumber: string
  resolutionTitle: string
  statutorySection: string
  requiredThresholdPct: number
  votingShareInFavourPct: number
  votingShareAgainstPct: number
  votingShareAbstainedPct: number
}

export interface SavePufeTransactionRequest {
  id?: string
  cirpCaseId: string
  pufeType: PufeType
  transactionDate: string
  counterpartyName: string
  isRelatedParty: boolean
  transactionAmount: number
  estimatedAvoidanceRecoverableAmount: number
  forensicAuditorObservations: string
  ncltApplicationFiled: boolean
}

export interface SaveAssetValuationRequest {
  id?: string
  cirpCaseId: string
  assetClass: AssetValuationClass
  assetDescription: string
  valuer1Name: string
  valuer1FairValue: number
  valuer1LiquidationValue: number
  valuer2Name: string
  valuer2FairValue: number
  valuer2LiquidationValue: number
}

export interface CalculateCirpTimelineRequest {
  admissionDate: string
  asOfDate: string
}

export interface CirpTimelineCalculationDto {
  admissionDate: string
  claimsLastDate: string
  initial180Days: string
  extended270Days: string
  final330Days: string
  daysElapsed: number
  daysRemainingTo180: number
  daysRemainingTo330: number
  is180DaysExpired: boolean
  is330DaysCapBreached: boolean
  timelineStatusAlert: string
}

export const ibcCirpApi = {
  getCases: (clientId?: string) =>
    api<CirpCaseDto[]>(`/api/ibc-cirp/cases${clientId ? `?clientId=${clientId}` : ''}`),
  getCase: (id: string) =>
    api<CirpCaseDetailDto>(`/api/ibc-cirp/cases/${id}`),
  saveCase: (req: SaveCirpCaseRequest) =>
    api<CirpCaseDetailDto>('/api/ibc-cirp/cases', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteCase: (id: string) =>
    api<void>(`/api/ibc-cirp/cases/${id}`, {
      method: 'DELETE'
    }),
  saveCocMember: (req: SaveCocMemberRequest) =>
    api<CocMemberDto>('/api/ibc-cirp/coc-members', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteCocMember: (id: string) =>
    api<void>(`/api/ibc-cirp/coc-members/${id}`, {
      method: 'DELETE'
    }),
  saveResolution: (req: SaveCocResolutionRequest) =>
    api<CocResolutionItemDto>('/api/ibc-cirp/resolutions', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteResolution: (id: string) =>
    api<void>(`/api/ibc-cirp/resolutions/${id}`, {
      method: 'DELETE'
    }),
  savePufe: (req: SavePufeTransactionRequest) =>
    api<PufeTransactionItemDto>('/api/ibc-cirp/pufe', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deletePufe: (id: string) =>
    api<void>(`/api/ibc-cirp/pufe/${id}`, {
      method: 'DELETE'
    }),
  saveValuation: (req: SaveAssetValuationRequest) =>
    api<AssetValuationItemDto>('/api/ibc-cirp/valuations', {
      method: 'POST',
      body: JSON.stringify(req)
    }),
  deleteValuation: (id: string) =>
    api<void>(`/api/ibc-cirp/valuations/${id}`, {
      method: 'DELETE'
    }),
  getImDossierHtmlUrl: (id: string) => `/api/ibc-cirp/cases/${id}/im-dossier`,
  calculateTimeline: (req: CalculateCirpTimelineRequest) =>
    api<CirpTimelineCalculationDto>('/api/ibc-cirp/calculate-timeline', {
      method: 'POST',
      body: JSON.stringify(req)
    })
}

export const featuresApi = {
  getFeatures: () => api<Record<string, boolean>>('/api/features'),
  getFeature: (key: string) => api<{ key: string; isEnabled: boolean }>(`/api/features/${key}`),
  setFeature: (key: string, enabled: boolean) =>
    api<{ key: string; isEnabled: boolean }>(`/api/features/${key}`, {
      method: 'POST',
      body: JSON.stringify({ enabled })
    })
}






















