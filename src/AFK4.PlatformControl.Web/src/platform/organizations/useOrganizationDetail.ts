import { useCallback, useEffect, useRef, useState } from 'react';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { OrganizationDetail } from '@/api/types';

export type OrganizationDetailState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: OrganizationDetail; apply: (next: OrganizationDetail) => void; retry: () => void };

type Loadable = Pick<OrganizationsApi, 'getOrganization'>;

export function useOrganizationDetail(client: Loadable, organizationId: string): OrganizationDetailState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: OrganizationDetail; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const apply = useCallback((next: OrganizationDetail) => setState({ status: 'ready', data: next }), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.getOrganization(organizationId)
      .then(d => { if (!cancelled) setState({ status: 'ready', data: d }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [organizationId, tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, apply, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
