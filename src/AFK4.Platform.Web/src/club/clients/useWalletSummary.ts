import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlayersApi } from '@/api/clients/players';
import { toBalanceView, toLedgerRows, type BalanceView, type LedgerRow } from './clientsModel';

type Loadable = Pick<PlayersApi, 'getWalletSummary'>;

export type WalletSummaryState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; balance: BalanceView; ledger: LedgerRow[]; retry: () => void };

export function useWalletSummary(client: Loadable, playerAccountId: string): WalletSummaryState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [balance, setBalance] = useState<BalanceView | null>(null);
  const [ledger, setLedger] = useState<LedgerRow[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.getWalletSummary(playerAccountId)
      .then(summary => {
        if (!cancelled) {
          setBalance(toBalanceView(summary));
          setLedger(toLedgerRows(summary.recentEntries));
          setPhase('ready');
        }
      })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [playerAccountId, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || balance === null) return { status: 'loading' };
  return { status: 'ready', balance, ledger, retry };
}
