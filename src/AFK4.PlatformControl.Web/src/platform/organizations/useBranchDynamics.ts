import { useCallback, useEffect, useRef, useState } from 'react';
import type { BranchDynamicsApi } from '@/api/platformClients/branchDynamics';
import type { BranchDynamics } from '@/api/types';

export type BranchDynamicsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: BranchDynamics; retry: () => void };

type Loadable = Pick<BranchDynamicsApi, 'getBranchDynamics'>;

export function useBranchDynamics(client: Loadable, organizationId: string, branchId: string, days = 30): BranchDynamicsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: BranchDynamics; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    // No branch to ask about (organization with zero branches) — the caller renders its own
    // empty state in that case and never reads this hook's result, but skipping the request
    // still avoids firing a call against a malformed URL.
    if (branchId === '') return;
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.getBranchDynamics(organizationId, branchId, days)
      .then(data => { if (!cancelled) setState({ status: 'ready', data }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick, organizationId, branchId, days]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
