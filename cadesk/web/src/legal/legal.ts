import { api } from '../api'

export type LegalDocument = { document: string; title: string; version: string; url: string; accepted: boolean; acceptedAt?: string | null }
export type LegalStatus = { documents: LegalDocument[]; pending: string[] }

export const legalApi = {
  status: () => api<LegalStatus>('/api/legal/status'),
  accept: (documents: string[]) => api<LegalStatus>('/api/legal/accept', { method: 'POST', body: JSON.stringify({ documents }) }),
}

/** Set by the sign-up form, whose checkbox already records the partner's acceptance. */
export const SIGNUP_ACCEPTED_KEY = 'cadesk.signupAcceptedLegal'
