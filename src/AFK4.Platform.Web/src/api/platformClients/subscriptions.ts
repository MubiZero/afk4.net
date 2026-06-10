import type { PlatformTransport } from '../platformTransport';
import type { SubscriptionListItem, TenantSubscription, UpdateSubscriptionRequest } from '../types';

export class SubscriptionsApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getSubscription(organizationId: string): Promise<TenantSubscription> {
    return this.transport.send<TenantSubscription>('GET', `/api/platform/tenants/${organizationId}/subscription`);
  }

  public updateSubscription(organizationId: string, request: UpdateSubscriptionRequest): Promise<TenantSubscription> {
    return this.transport.send<TenantSubscription>('PATCH', `/api/platform/tenants/${organizationId}/subscription`, request);
  }

  public listSubscriptions(status?: string, planCode?: string): Promise<SubscriptionListItem[]> {
    const params = new URLSearchParams();
    if (status !== undefined && status.length > 0) params.set('status', status);
    if (planCode !== undefined && planCode.length > 0) params.set('planCode', planCode);
    const query = params.toString().length > 0 ? `?${params.toString()}` : '';
    return this.transport.send<SubscriptionListItem[]>('GET', `/api/platform/subscriptions${query}`);
  }
}
