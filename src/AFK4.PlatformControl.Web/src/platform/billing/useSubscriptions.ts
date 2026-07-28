import { useCallback, useEffect, useRef, useState } from 'react';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { SubscriptionListItem } from '@/api/types';

export type SubscriptionsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: SubscriptionListItem[]; retry: () => void };

type Loadable = Pick<SubscriptionsApi, 'listSubscriptions'>;

export function useSubscriptions(client: Loadable): SubscriptionsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: SubscriptionListItem[]; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listSubscriptions()
      .then(rows => { if (!cancelled) setState({ status: 'ready', data: rows }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
