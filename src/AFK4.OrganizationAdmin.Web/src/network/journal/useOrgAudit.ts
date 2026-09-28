import { useRef } from 'react';
import type { OrgAuditQuery, OrgAuditRecordDto } from '../../api/clients/orgAudit';
import { useSection, type Section } from '../useSection';

export interface OrgAuditClient {
  searchOrganizationAudit(organizationId: string, query: OrgAuditQuery): Promise<{ records: OrgAuditRecordDto[] }>;
}

export function useOrgAudit(
  client: OrgAuditClient,
  organizationId: string,
  query: OrgAuditQuery
): Section<OrgAuditRecordDto[]> {
  const clientRef = useRef(client);
  clientRef.current = client;
  const key = organizationId === '' ? '' : `${organizationId}::${JSON.stringify(query)}`;
  return useSection(
    async () => (await clientRef.current.searchOrganizationAudit(organizationId, query)).records,
    key
  );
}
