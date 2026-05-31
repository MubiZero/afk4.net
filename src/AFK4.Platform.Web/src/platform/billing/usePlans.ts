import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import type { SubscriptionPlan } from '@/api/types';

export type PlansState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: SubscriptionPlan[]; retry: () => void };

type Loadable = Pick<PlatformApiClient, 'listPlans'>;

export function usePlans(client: Loadable): PlansState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: SubscriptionPlan[]; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listPlans(true)
      .then(plans => { if (!cancelled) setState({ status: 'ready', data: plans }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
