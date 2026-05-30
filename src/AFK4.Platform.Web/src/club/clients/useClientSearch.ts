import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { toPlayerRows, type PlayerRow } from './clientsModel';

type Loadable = Pick<ClubApiClient, 'searchPlayers'>;

export type ClientSearchState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: PlayerRow[]; retry: () => void };

export function useClientSearch(client: Loadable, branchId: string, query: string): ClientSearchState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<PlayerRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.searchPlayers(branchId, query, 20)
      .then(results => { if (!cancelled) { setRows(toPlayerRows(results)); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, query, tick]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', rows, retry };
}
