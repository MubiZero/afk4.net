import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import type { OwnerCodeSummary } from '@/api/types';

type Loadable = Pick<ClubApiClient, 'getOwnerCode'>;

export type OwnerCodeState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; summary: OwnerCodeSummary | null; retry: () => void };

export function useOwnerCode(client: Loadable, enabled: boolean): OwnerCodeState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [summary, setSummary] = useState<OwnerCodeSummary | null>(null);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    if (!enabled) {
      setSummary(null);
      setPhase('ready');
      return;
    }
    setPhase('loading');
    clientRef.current.getOwnerCode()
      .then(result => { if (!cancelled) { setSummary(result); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [enabled, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', summary, retry };
}
