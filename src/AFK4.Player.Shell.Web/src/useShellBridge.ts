import { useEffect, useState } from 'react';
import { onShellStateChanged } from './shellBridge';
import { launchApp, loadShellState, pauseSession, requestOperator } from './shellClient';
import type { PlayerShellState } from './shellContracts';

export interface ShellBridge {
  state: PlayerShellState | null;
  launch: (appId: string) => Promise<{ status: string }>;
  requestOperator: () => Promise<{ requested: boolean }>;
  pause: () => Promise<{ paused: boolean }>;
}

export function useShellBridge(): ShellBridge {
  const [state, setState] = useState<PlayerShellState | null>(null);

  useEffect(() => {
    let active = true;
    loadShellState()
      .then((initial) => {
        if (active && initial) {
          setState(initial);
        }
      })
      .catch(() => {});

    const unsubscribe = onShellStateChanged((next) => setState(next as PlayerShellState));
    return () => {
      active = false;
      unsubscribe();
    };
  }, []);

  return { state, launch: launchApp, requestOperator, pause: pauseSession };
}
