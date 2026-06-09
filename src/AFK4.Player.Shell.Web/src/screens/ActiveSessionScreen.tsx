import { formatRemaining } from '../formatRemaining';
import type { PlayerShellState } from '../shellContracts';

interface Props {
  state: PlayerShellState;
  onLaunch: (appId: string) => Promise<{ status: string }>;
  onRequestOperator: () => Promise<{ requested: boolean }>;
}

export function ActiveSessionScreen({ state, onLaunch, onRequestOperator }: Props) {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <header style={{ display: 'flex', justifyContent: 'space-between', padding: '20px 28px', borderBottom: '1px solid #1f3a5f' }}>
        <strong style={{ fontSize: 24 }}>
          AFK4<span style={{ color: '#2dd4a7' }}>.NET</span>
        </strong>
        <span style={{ fontSize: 28, fontWeight: 600 }}>{formatRemaining(state.remainingSeconds)}</span>
      </header>

      <main style={{ flex: 1, padding: 42 }}>
        {state.warningKind !== 'None' && (
          <p style={{ color: '#fde68a' }}>{state.message}</p>
        )}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: 16 }}>
          {state.launcherApps.map((app) => (
            <button
              key={app.appId}
              type="button"
              disabled={!app.isAvailable}
              onClick={() => onLaunch(app.appId)}
              style={{
                minHeight: 120,
                background: '#10233a',
                border: '1px solid #2b5b84',
                color: '#fff',
                borderRadius: 10,
                opacity: app.isAvailable ? 1 : 0.45
              }}
            >
              <span style={{ fontSize: 18, fontWeight: 600 }}>{app.displayName}</span>
            </button>
          ))}
        </div>
      </main>

      <footer style={{ padding: '14px 24px', borderTop: '1px solid #1f3a5f' }}>
        <button type="button" onClick={() => onRequestOperator()} style={{ background: 'none', border: '1px solid #2b5b84', color: '#9ca3af', borderRadius: 8, padding: '8px 14px' }}>
          Позвать оператора
        </button>
      </footer>
    </div>
  );
}
