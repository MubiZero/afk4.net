import { useCallback, useEffect, useRef, useState } from 'react';
import type { PulseApi } from '@/api/platformClients/pulse';
import type { PlatformPulse } from '@/api/types';

export type PulseState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: PlatformPulse; retry: () => void };

type Loadable = Pick<PulseApi, 'getPulse'>;

export function usePulse(client: Loadable): PulseState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: PlatformPulse; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.getPulse()
      .then(pulse => { if (!cancelled) setState({ status: 'ready', data: pulse }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
