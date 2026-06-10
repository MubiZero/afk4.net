import { useCallback, useEffect, useRef, useState } from 'react';
import type { VenueApi } from '@/api/clients/venue';
import { buildDevices, type DevicesViewModel } from './devicesModel';

export type DevicesState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: DevicesViewModel; retry: () => void };

type Loadable = Pick<VenueApi, 'listDevices' | 'listPendingDevices' | 'getFloorMap'>;

export function useDevices(client: Loadable, branchId: string): DevicesState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: DevicesViewModel; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    const c = clientRef.current;
    Promise.all([c.listDevices(branchId), c.listPendingDevices(branchId), c.getFloorMap(branchId)])
      .then(([devices, pending, floor]) => {
        if (cancelled) return;
        setState({ status: 'ready', data: buildDevices(devices, pending, floor.floorMap) });
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        setState({ status: 'error', message: err instanceof Error ? err.message : 'error' });
      });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
