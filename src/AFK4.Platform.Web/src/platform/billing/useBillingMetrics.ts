import { useCallback, useEffect, useRef, useState } from 'react';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { PlatformBillingMetrics } from '@/api/types';

export type BillingMetricsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: PlatformBillingMetrics; retry: () => void };

type Loadable = Pick<InvoicesApi, 'getBillingMetrics'>;

export function useBillingMetrics(client: Loadable): BillingMetricsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: PlatformBillingMetrics; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.getBillingMetrics()
      .then(m => { if (!cancelled) setState({ status: 'ready', data: m }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
