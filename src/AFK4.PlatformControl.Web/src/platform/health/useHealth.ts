import { useCallback, useEffect, useRef, useState } from 'react';
import type { HealthApi } from '@/api/platformClients/health';
import type { HealthOverview } from '@/api/types';

export type HealthState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: HealthOverview; retry: () => void };

type Loadable = Pick<HealthApi, 'getOverview'>;

export function useHealth(client: Loadable): HealthState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: HealthOverview; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.getOverview()
      .then(overview => { if (!cancelled) setState({ status: 'ready', data: overview }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
