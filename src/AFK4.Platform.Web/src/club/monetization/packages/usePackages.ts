import { useCallback, useEffect, useRef, useState } from 'react';
import type { PackagesApi } from '@/api/clients/packages';
import { toPackageRows, type PackageRow } from './packagesModel';

type Loadable = Pick<PackagesApi, 'getPackageOptions'>;

export type PackagesState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: PackageRow[]; retry: () => void };

export function usePackages(client: Loadable, branchId: string): PackagesState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<PackageRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.getPackageOptions(branchId)
      .then(options => { if (!cancelled) { setRows(toPackageRows(options)); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', rows, retry };
}
