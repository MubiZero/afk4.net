import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import { buildTenantMetrics, type PlatformMetricsViewModel } from './metricsModel';

export type TenantMetricsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: PlatformMetricsViewModel; retry: () => void };

type Loadable = Pick<PlatformApiClient, 'listTenants'>;

export function useTenantMetrics(client: Loadable): TenantMetricsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: PlatformMetricsViewModel; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listTenants()
      .then(tenants => {
        if (cancelled) return;
        setState({ status: 'ready', data: buildTenantMetrics(tenants, new Date().toISOString()) });
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setState({ status: 'error', message: err instanceof Error ? err.message : 'error' });
      });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
