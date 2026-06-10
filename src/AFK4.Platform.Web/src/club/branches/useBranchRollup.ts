import { useCallback, useEffect, useRef, useState } from 'react';
import type { BranchApi } from '@/api/clients/branches';
import { buildBranchRollup, type BranchRollupEntry, type BranchRollupViewModel } from './branchRollupModel';

export type BranchRollupState =
  | { status: 'loading'; retry: () => void }
  | { status: 'ready'; data: BranchRollupViewModel; retry: () => void };

type Loadable = Pick<BranchApi, 'getDashboardSummary' | 'getBranchProfile'>;

export function useBranchRollup(client: Loadable, branchIds: readonly string[], unnamedLabel: string): BranchRollupState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'ready'; data?: BranchRollupViewModel }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;
  const key = branchIds.join(',');

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    const c = clientRef.current;
    const ids = key === '' ? [] : key.split(',');
    void Promise.all(ids.map(async (branchId): Promise<BranchRollupEntry> => {
      const [profile, summary] = await Promise.all([
        c.getBranchProfile(branchId).catch(() => null),
        c.getDashboardSummary(branchId).catch(() => null)
      ]);
      return {
        branchId,
        name: profile?.name ?? unnamedLabel,
        city: profile?.city ?? '',
        summary
      };
    })).then(entries => {
      if (cancelled) return;
      setState({ status: 'ready', data: buildBranchRollup(entries) });
    });
    return () => { cancelled = true; };
  }, [key, tick, unnamedLabel]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  return { status: 'loading', retry };
}
