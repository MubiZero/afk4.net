import type { PlatformTransport } from '../platformTransport';
import type { OrganizationOwnerInvite, OrganizationOwnerInviteSummary } from '../types';

export class OrganizationOwnerInvitesApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public createOrganizationOwnerInvite(
    organizationId: string,
    branchId: string,
    ownerUserName: string | null,
    ownerDisplayName: string | null,
    lifetime: string | null
  ): Promise<OrganizationOwnerInvite> {
    return this.transport.send<OrganizationOwnerInvite>(
      'POST',
      `/api/platform/tenants/${organizationId}/organization-owner-invitations`,
      { branchId, ownerUserName, ownerDisplayName, lifetime }
    );
  }

  public listOrganizationOwnerInvites(organizationId: string): Promise<OrganizationOwnerInviteSummary[]> {
    return this.transport.send<OrganizationOwnerInviteSummary[]>(
      'GET',
      `/api/platform/tenants/${organizationId}/organization-owner-invitations`
    );
  }

  public revokeOrganizationOwnerInvite(organizationOwnerInviteId: string, reason: string): Promise<OrganizationOwnerInvite> {
    return this.transport.send<OrganizationOwnerInvite>(
      'POST',
      `/api/platform/organization-owner-invitations/${organizationOwnerInviteId}/revoke`,
      { reason }
    );
  }
}
