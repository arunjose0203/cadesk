import { PLAN_NAMES } from './plan'

/** Shown in place of a desk the firm's plan doesn't include. */
export function PlanLocked({ desk, needs, current, canUpgrade, onUpgrade }: {
  desk: string; needs: string; current: string; canUpgrade: boolean; onUpgrade: () => void
}) {
  const name = PLAN_NAMES[needs] ?? needs
  return (
    <div className="feature-off plan-locked">
      <h2>{desk} is in the {name} plan</h2>
      <p>Your firm is on the {PLAN_NAMES[current] ?? current} plan. Upgrade to {name} to use this desk; your data and other desks stay as they are.</p>
      <p>
        {canUpgrade
          ? <button className="small" onClick={onUpgrade}>See plans</button>
          : <span className="muted small">Ask a partner who manages the firm's settings to upgrade.</span>}
      </p>
    </div>
  )
}
