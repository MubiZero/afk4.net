import { useCallback, useEffect, useRef, useState } from 'react';
import type { OrgAuditRecordDto } from '../../api/clients/orgAudit';

export interface BranchAuditQuery {
  fromUtc: string;
  toUtc: string;
  action?: string;
  outcome?: string;
  targetType?: string;
  limit?: number;
}

export interface BranchAuditClient {
  search(branchId: string, query: BranchAuditQuery): Promise<OrgAuditRecordDto[]>;
}

export type BranchAuditState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; records: OrgAuditRecordDto[]; retry: () => void };

export function useBranchAudit(client: BranchAuditClient, branchId: string, query: BranchAuditQuery): BranchAuditState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [records, setRecords] = useState<OrgAuditRecordDto[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;
  const retry = useCallback(() => setTick((t) => t + 1), []);
  const queryKey = JSON.stringify(query);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.search(branchId, query)
      .then((res) => { if (!cancelled) { setRecords(res); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId, queryKey, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', records, retry };
}
