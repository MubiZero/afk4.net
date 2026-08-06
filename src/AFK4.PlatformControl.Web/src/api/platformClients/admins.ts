import type { PlatformTransport } from '../platformTransport';
import type { CreateInvitationResponse, PlatformAdminInvitation, PlatformAdminListItem } from '../types';

export class AdminsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listAdmins(): Promise<PlatformAdminListItem[]> {
    return this.transport.send<PlatformAdminListItem[]>('GET', '/api/platform/admins');
  }

  public listInvitations(): Promise<PlatformAdminInvitation[]> {
    return this.transport.send<PlatformAdminInvitation[]>('GET', '/api/platform/admins/invitations');
  }

  public invite(role: string, lifetimeHours: number): Promise<CreateInvitationResponse> {
    return this.transport.send<CreateInvitationResponse>('POST', '/api/platform/admins/invitations', {
      role,
      lifetimeHours
    });
  }

  public async revokeInvitation(invitationId: string): Promise<void> {
    await this.transport.send<void>('POST', `/api/platform/admins/invitations/${invitationId}/revoke`);
  }

  public updateAdmin(
    platformAdminUserId: string,
    patch: { role?: string; isActive?: boolean }
  ): Promise<PlatformAdminListItem> {
    return this.transport.send<PlatformAdminListItem>('PATCH', `/api/platform/admins/${platformAdminUserId}`, {
      role: patch.role,
      isActive: patch.isActive
    });
  }
}
