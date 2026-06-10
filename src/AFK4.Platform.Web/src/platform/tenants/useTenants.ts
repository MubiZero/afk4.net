import { useCallback, useEffect, useRef, useState } from 'react';
import type { TenantsApi } from '@/api/platformClients/tenants';
import type { TenantSummary } from '@/api/types';

export type TenantsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: TenantSummary[]; retry: () => void };

type Loadable = Pick<TenantsApi, 'listTenants'>;

export function useTenants(client: Loadable): TenantsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: TenantSummary[]; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listTenants()
      .then(tenants => { if (!cancelled) setState({ status: 'ready', data: tenants }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
