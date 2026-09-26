import type { PlatformTransport } from '../platformTransport';
import type { BillingTermsDto, CreatePlanRequest, SubscriptionPlan, UpdateBillingTermsRequest, UpdatePlanRequest } from '../types';

export class PlansApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public listPlans(includeInactive = true): Promise<SubscriptionPlan[]> {
    return this.transport.send<SubscriptionPlan[]>('GET', `/api/platform/plans?includeInactive=${includeInactive ? 'true' : 'false'}`);
  }

  public createPlan(request: CreatePlanRequest): Promise<SubscriptionPlan> {
    return this.transport.send<SubscriptionPlan>('POST', '/api/platform/plans', request);
  }

  public updatePlanCatalog(planCode: string, request: UpdatePlanRequest): Promise<SubscriptionPlan> {
    return this.transport.send<SubscriptionPlan>('PATCH', `/api/platform/plans/${encodeURIComponent(planCode)}`, request);
  }

  // Условия оплаты для клубов: пробный период, обещанный платёж, льгота до бесплатного тарифа.
  public getTerms(): Promise<BillingTermsDto> {
    return this.transport.send<BillingTermsDto>('GET', '/api/platform/billing/terms');
  }

  public updateTerms(request: UpdateBillingTermsRequest): Promise<BillingTermsDto> {
    return this.transport.send<BillingTermsDto>('PUT', '/api/platform/billing/terms', request);
  }
}
