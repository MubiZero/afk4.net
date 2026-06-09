import { postShellRequest } from './shellBridge';
import type { PlayerShellState } from './shellContracts';

export function loadShellState(): Promise<PlayerShellState | null> {
  return postShellRequest<PlayerShellState | null>('shell:loadState').then((s) => s ?? null);
}

export function launchApp(appId: string): Promise<{ status: string }> {
  return postShellRequest<{ status: string }>('launcher:launch', { appId });
}

export function requestOperator(): Promise<{ requested: boolean }> {
  return postShellRequest<{ requested: boolean }>('shell:requestOperator');
}

export function pauseSession(): Promise<{ paused: boolean }> {
  return postShellRequest<{ paused: boolean }>('shell:pause');
}
