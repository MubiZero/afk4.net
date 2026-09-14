import type { PlatformTransport } from '../platformTransport';
import type { SupportAccessGrantIssue, SupportAccessGrantListItem } from '../types';

export class SupportAccessApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public issueGrant(
    organizationId: string,
    reason: string,
    lifetimeMinutes: number
  ): Promise<SupportAccessGrantIssue> {
    return this.transport.send<SupportAccessGrantIssue>('POST', '/api/platform/support-access-grants', {
      organizationId,
      reason,
      lifetimeMinutes
    });
  }

  public listGrants(organizationId: string): Promise<SupportAccessGrantListItem[]> {
    return this.transport.send<SupportAccessGrantListItem[]>(
      'GET', `/api/platform/support-access-grants?organizationId=${organizationId}`);
  }

  public revokeGrant(grantId: string): Promise<void> {
    return this.transport.send<void>('DELETE', `/api/platform/support-access-grants/${grantId}`);
  }
}
