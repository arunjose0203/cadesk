import { Component, type ReactNode } from 'react'
import { reportCrash } from './monitor'

type Props = { deskName: string; resetKey: string; onHome: () => void; children: ReactNode }
type State = { error: Error | null }

/* One desk failing must not blank the whole app: the sidebar stays usable,
   and moving to another desk (a new resetKey) clears the error. */
export class DeskBoundary extends Component<Props, State> {
  state: State = { error: null }

  static getDerivedStateFromError(error: Error): State {
    return { error }
  }

  componentDidCatch(error: Error) {
    console.error(`CaDesk: the ${this.props.deskName} desk failed to render`, error)
    reportCrash(error, `${this.props.deskName} desk`)
  }

  componentDidUpdate(prev: Props) {
    if (prev.resetKey !== this.props.resetKey && this.state.error) this.setState({ error: null })
  }

  render() {
    if (!this.state.error) return this.props.children
    return (
      <div className="feature-off" role="alert">
        <h2>{this.props.deskName} couldn't open</h2>
        <p>Something in this desk stopped working. Your saved data is not affected. Reload the page to try again, or open another desk from the sidebar.</p>
        <p className="row" style={{ marginTop: 16 }}>
          <button className="small" onClick={() => window.location.reload()}>Reload page</button>
          <button className="ghost small" onClick={this.props.onHome}>Go to Today</button>
        </p>
      </div>
    )
  }
}
