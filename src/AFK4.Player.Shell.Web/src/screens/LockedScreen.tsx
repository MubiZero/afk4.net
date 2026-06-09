import type { PlayerShellState } from '../shellContracts';

interface Props {
  state: PlayerShellState | null;
  onRequestOperator: () => Promise<{ requested: boolean }>;
}

export function LockedScreen({ state, onRequestOperator }: Props) {
  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', gap: 20 }}>
      <strong style={{ fontSize: 34 }}>
        AFK4<span style={{ color: '#2dd4a7' }}>.NET</span>
      </strong>
      <p style={{ color: '#9ca3af', fontSize: 20 }}>{state?.message ?? 'Экран заблокирован'}</p>
      <button type="button" onClick={() => onRequestOperator()} style={{ background: 'none', border: '1px solid #2b5b84', color: '#9ca3af', borderRadius: 8, padding: '10px 18px' }}>
        Позвать оператора
      </button>
    </div>
  );
}
