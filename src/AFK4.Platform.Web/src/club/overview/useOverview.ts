import { useCallback, useEffect, useRef, useState } from 'react';
import type { BranchApi } from '@/api/clients/branches';
import type { VenueApi } from '@/api/clients/venue';
import { buildOverview, type OverviewViewModel } from './overviewModel';

export type OverviewState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: OverviewViewModel; retry: () => void };

type Loadable = {
  branches: Pick<BranchApi, 'getDashboardSummary'>;
  venue: Pick<VenueApi, 'listDevices' | 'listPendingDevices'>;
};

export function useOverview(client: Loadable, branchId: string): OverviewState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: OverviewViewModel; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    const c = clientRef.current;
    Promise.all([c.branches.getDashboardSummary(branchId), c.venue.listDevices(branchId), c.venue.listPendingDevices(branchId)])
      .then(([summary, devices, pending]) => {
        if (cancelled) return;
        setState({ status: 'ready', data: buildOverview(summary, devices, pending) });
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
