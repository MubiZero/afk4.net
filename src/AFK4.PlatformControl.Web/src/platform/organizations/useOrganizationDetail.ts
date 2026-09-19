import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { OrganizationDetail } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type OrganizationDetailState = Loadable<OrganizationDetail>;

type Client = Pick<OrganizationsApi, 'getOrganization'>;

export function useOrganizationDetail(client: Client, organizationId: string): OrganizationDetailState {
  return useLoadable(() => client.getOrganization(organizationId), [organizationId]);
}
