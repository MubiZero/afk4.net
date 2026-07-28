import { useCallback, useEffect, useRef, useState } from 'react';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { OrganizationSummary } from '@/api/types';

export type OrganizationsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: OrganizationSummary[]; retry: () => void };

type Loadable = Pick<OrganizationsApi, 'listOrganizations'>;

export function useOrganizations(client: Loadable): OrganizationsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: OrganizationSummary[]; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listOrganizations()
      .then(organizations => { if (!cancelled) setState({ status: 'ready', data: organizations }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
