import { useCallback, useEffect, useRef, useState } from 'react';
import type { TariffsApi } from '@/api/clients/tariffs';
import { toTariffRows, type TariffRow } from './tariffsModel';

type Loadable = Pick<TariffsApi, 'getTariffOptions'>;

export type TariffsState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; rows: TariffRow[]; retry: () => void };

export function useTariffs(client: Loadable, branchId: string): TariffsState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [rows, setRows] = useState<TariffRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.getTariffOptions(branchId)
      .then(options => { if (!cancelled) { setRows(toTariffRows(options)); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [branchId, tick]);

  if (phase === 'loading') return { status: 'loading' };
  if (phase === 'error') return { status: 'error', retry };
  return { status: 'ready', rows, retry };
}
