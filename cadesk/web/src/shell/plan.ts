import { api } from '../api'

/* Plan-based desk gating (Sprint 71). The server decides; this only shows it. */

export type PlanDesks = {
  planTier: string
  displayName: string
  deskPlans: Record<string, string>
  userLimit: number | null
  clientLimit: number | null
  users: number
  clients: number
}

export const getPlanDesks = () => api<PlanDesks>('/api/subscriptions/desks')

const RANK: Record<string, number> = { STARTER: 0, GROWTH: 1, ENTERPRISE: 2, DEMO: 3, DEV: 3, INTERNAL: 3 }
export const PLAN_NAMES: Record<string, string> = { STARTER: 'Starter', GROWTH: 'Growth', ENTERPRISE: 'Enterprise' }

/** The plan a desk needs when the firm's plan doesn't include it; null when it is open (or not known yet). */
export function lockedPlan(plan: PlanDesks | null, desk: string): string | null {
  if (!plan) return null
  const needs = plan.deskPlans[desk]
  if (!needs) return null
  return (RANK[plan.planTier.toUpperCase()] ?? 0) >= (RANK[needs.toUpperCase()] ?? 0) ? null : needs.toUpperCase()
}
