import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen } from '@testing-library/react';
import { ActiveSessionScreen } from './ActiveSessionScreen';
import type { PlayerShellState } from '../shellContracts';

const baseState: PlayerShellState = {
  organizationId: 'o',
  branchId: 'b',
  deviceId: 'd',
  state: 'active',
  sessionId: 's',
  leaseExpiresAtUtc: null,
  remainingSeconds: 3661,
  isOnline: true,
  isGraceMode: false,
  warningThresholdSeconds: 300,
  message: 'ok',
  launcherApps: [
    { appId: 'cs2', displayName: 'Counter-Strike 2', category: 'game', iconUri: null, isAvailable: true },
    { appId: 'valorant', displayName: 'Valorant', category: 'game', iconUri: null, isAvailable: false }
  ],
  locale: 'ru',
  warningKind: 'none'
};

describe('ActiveSessionScreen', () => {
  it('shows the formatted remaining time', () => {
    render(<ActiveSessionScreen state={baseState} onLaunch={mock(async () => ({ status: 'accepted' }))} onRequestOperator={mock(async () => ({ requested: true }))} />);
    expect(screen.getByText('1:01:01')).toBeInTheDocument();
  });

  it('launches an available app on click', () => {
    const onLaunch = mock(async () => ({ status: 'accepted' }));
    render(<ActiveSessionScreen state={baseState} onLaunch={onLaunch} onRequestOperator={mock(async () => ({ requested: true }))} />);

    fireEvent.click(screen.getByRole('button', { name: /Counter-Strike 2/ }));
    expect(onLaunch).toHaveBeenCalledWith('cs2');
  });

  it('disables unavailable apps', () => {
    render(<ActiveSessionScreen state={baseState} onLaunch={mock(async () => ({ status: 'accepted' }))} onRequestOperator={mock(async () => ({ requested: true }))} />);
    expect(screen.getByRole('button', { name: /Valorant/ })).toBeDisabled();
  });
});
