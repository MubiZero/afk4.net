import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { toProductRows, type ProductRow } from './catalogModel';

type Loadable = Pick<ClubApiClient, 'getCatalog'>;

export type CatalogState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: ProductRow[]; retry: () => void };

export function useCatalog(client: Loadable, branchId: string): CatalogState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<ProductRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.getCatalog(branchId)
      .then(products => { if (!cancelled) { setRows(toProductRows(products)); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', rows, retry };
}
