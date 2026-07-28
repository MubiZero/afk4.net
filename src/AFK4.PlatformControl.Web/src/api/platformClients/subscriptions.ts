import type { PlatformTransport } from '../platformTransport';
import type { SubscriptionListItem, OrganizationSubscription, UpdateSubscriptionRequest } from '../types';

export class SubscriptionsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getSubscription(organizationId: string): Promise<OrganizationSubscription> {
    return this.transport.send<OrganizationSubscription>('GET', `/api/platform/organizations/${organizationId}/subscription`);
  }

  public updateSubscription(organizationId: string, request: UpdateSubscriptionRequest): Promise<OrganizationSubscription> {
    return this.transport.send<OrganizationSubscription>('PATCH', `/api/platform/organizations/${organizationId}/subscription`, request);
  }

  public listSubscriptions(status?: string, planCode?: string): Promise<SubscriptionListItem[]> {
    const params = new URLSearchParams();
    if (status !== undefined && status.length > 0) params.set('status', status);
    if (planCode !== undefined && planCode.length > 0) params.set('planCode', planCode);
    const query = params.toString().length > 0 ? `?${params.toString()}` : '';
    return this.transport.send<SubscriptionListItem[]>('GET', `/api/platform/subscriptions${query}`);
  }
}
