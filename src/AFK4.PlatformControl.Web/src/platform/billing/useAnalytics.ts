import { useCallback, useEffect, useRef, useState } from 'react';
import type { AnalyticsApi } from '@/api/platformClients/analytics';
import type { AnalyticsOverview } from '@/api/types';

export type AnalyticsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: AnalyticsOverview; retry: () => void };

type Loadable = Pick<AnalyticsApi, 'getOverview'>;

export function useAnalytics(client: Loadable, months = 12): AnalyticsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: AnalyticsOverview; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.getOverview(months)
      .then(data => { if (!cancelled) setState({ status: 'ready', data }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick, months]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
