import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '@/api/clubApi';
import { toPackageChoices, toPlayerPackageRows, type PackageChoice, type PlayerPackageRow } from './playerPackagesModel';

type Loadable = Pick<ClubApiClient, 'getPlayerPackages' | 'getPackageOptions'>;

export type PlayerPackagesState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: PlayerPackageRow[]; choices: PackageChoice[]; retry: () => void };

export function usePlayerPackages(client: Loadable, playerAccountId: string, branchId: string): PlayerPackagesState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<PlayerPackageRow[]>([]);
  const [choices, setChoices] = useState<PackageChoice[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    Promise.all([
      clientRef.current.getPlayerPackages(playerAccountId),
      clientRef.current.getPackageOptions(branchId)
    ])
      .then(([packages, options]) => {
        if (!cancelled) {
          setRows(toPlayerPackageRows(packages));
          setChoices(toPackageChoices(options));
          setPhase('ready');
        }
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [playerAccountId, branchId, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', rows, choices, retry };
}
