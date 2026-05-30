// src/club/settings/useSettings.ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { buildSettings, type SettingsViewModel } from './settingsModel';

export type SettingsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: SettingsViewModel; retry: () => void };

type Loadable = Pick<ClubApiClient, 'getBranchProfile' | 'getBranchSettings' | 'listStaff'>;

export function useSettings(client: Loadable, branchId: string): SettingsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: SettingsViewModel; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    const c = clientRef.current;
    Promise.all([c.getBranchProfile(branchId), c.getBranchSettings(branchId), c.listStaff(branchId)])
      .then(([profile, settings, staff]) => {
        if (cancelled) return;
        setState({ status: 'ready', data: buildSettings(profile, settings, staff) });
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setState({ status: 'error', message: err instanceof Error ? err.message : 'error' });
      });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
