import { useCallback, useEffect, useRef, useState } from 'react';
import type { OrgAuditQuery, OrgAuditRecordDto } from '../../api/clients/orgAudit';

export interface OrgAuditClient {
  searchOrganizationAudit(organizationId: string, query: OrgAuditQuery): Promise<{ records: OrgAuditRecordDto[] }>;
}

export type OrgAuditState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; records: OrgAuditRecordDto[]; retry: () => void };

export function useOrgAudit(client: OrgAuditClient, organizationId: string, query: OrgAuditQuery): OrgAuditState {
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
    clientRef.current.searchOrganizationAudit(organizationId, query)
      .then((res) => {
        if (!cancelled) {
          setRecords(res.records);
          setPhase('ready');
        }
      })
      .catch(() => {
        if (!cancelled) setPhase('error');
      });
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [organizationId, queryKey, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', records, retry };
}
