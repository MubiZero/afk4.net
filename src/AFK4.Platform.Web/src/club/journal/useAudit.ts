import { useCallback, useEffect, useRef, useState } from 'react';
import type { AuditApi } from '@/api/clients/audit';
import type { AuditSearchQuery, AuditRecord } from '@/api/types';

type Loadable = Pick<AuditApi, 'searchAudit'>;

export type AuditState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; records: AuditRecord[]; retry: () => void };

export function useAudit(client: Loadable, branchId: string, query: AuditSearchQuery): AuditState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [records, setRecords] = useState<AuditRecord[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);
  const queryKey = JSON.stringify(query);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.searchAudit(branchId, query)
      .then(result => { if (!cancelled) { setRecords(result.records); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId, queryKey, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', records, retry };
}
