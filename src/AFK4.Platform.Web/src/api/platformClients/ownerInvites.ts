import type { PlatformTransport } from '../platformTransport';
import type { OwnerInvite, OwnerInviteSummary } from '../types';

export class OwnerInvitesApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public createOwnerInvite(
    organizationId: string,
    branchId: string,
    ownerUserName: string | null,
    ownerDisplayName: string | null,
    lifetime: string | null
  ): Promise<OwnerInvite> {
    return this.transport.send<OwnerInvite>(
      'POST',
      `/api/platform/tenants/${organizationId}/owner-invites`,
      { branchId, ownerUserName, ownerDisplayName, lifetime }
    );
  }

  public listOwnerInvites(organizationId: string): Promise<OwnerInviteSummary[]> {
    return this.transport.send<OwnerInviteSummary[]>(
      'GET',
      `/api/platform/tenants/${organizationId}/owner-invites`
    );
  }

  public revokeOwnerInvite(ownerInviteId: string, reason: string): Promise<OwnerInvite> {
    return this.transport.send<OwnerInvite>(
      'POST',
      `/api/platform/owner-invites/${ownerInviteId}/revoke`,
      { reason }
    );
  }
}
