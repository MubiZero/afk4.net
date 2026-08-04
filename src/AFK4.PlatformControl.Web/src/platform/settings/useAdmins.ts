import { useCallback, useEffect, useRef, useState } from 'react';
import type { AdminsApi } from '@/api/platformClients/admins';
import type { PlatformAdminInvitation, PlatformAdminListItem } from '@/api/types';

export type AdminsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; admins: PlatformAdminListItem[]; invitations: PlatformAdminInvitation[]; retry: () => void };

type Loadable = Pick<AdminsApi, 'listAdmins' | 'listInvitations'>;

export function useAdmins(client: Loadable): AdminsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{
    status: 'loading' | 'error' | 'ready';
    admins?: PlatformAdminListItem[];
    invitations?: PlatformAdminInvitation[];
    message?: string;
  }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    Promise.all([clientRef.current.listAdmins(), clientRef.current.listInvitations()])
      .then(([admins, invitations]) => { if (!cancelled) setState({ status: 'ready', admins, invitations }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', admins: state.admins!, invitations: state.invitations!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
