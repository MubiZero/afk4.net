import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import type {
  ClubAdsDto,
  ClubPlanDevicesDto,
  ClubPlanDto,
  SetClubPlanDevicesRequest,
  InvoiceDto,
  OrganizationBillingStatusDto,
  OrganizationSubscriptionDto,
} from '@afk4/contracts';
export type {
  ClubPlanDto,
  InvoiceDto,
  OrganizationBillingStatusDto,
  OrganizationSubscriptionDto,
} from '@afk4/contracts';

// «Сеть → Подписка»: подписка и счета — чтение; тариф клуба — словами и с тем, что клуб делает сам
// (пробный период, тариф за ПК, обещанный платёж; спека тарифов клуба, §6).
export function createOrgBillingClient(api: PlatformApiClient) {
  return {
    getSubscription(_organizationId: Guid): Promise<OrganizationSubscriptionDto> {
      return api.get<OrganizationSubscriptionDto>('subscription');
    },
    listInvoices(_organizationId: Guid): Promise<InvoiceDto[]> {
      return api.get<InvoiceDto[]>('invoices');
    },
    getBillingStatus(_organizationId: Guid): Promise<OrganizationBillingStatusDto> {
      return api.get<OrganizationBillingStatusDto>('billing/status');
    },
    getPlan(): Promise<ClubPlanDto> {
      return api.get<ClubPlanDto>('plan');
    },
    startTrial(): Promise<ClubPlanDto> {
      return api.post<ClubPlanDto, Record<string, never>>('plan/trial', {});
    },
    switchToPerPc(): Promise<ClubPlanDto> {
      return api.post<ClubPlanDto, Record<string, never>>('plan/per-pc', {});
    },
    promisePayment(): Promise<ClubPlanDto> {
      return api.post<ClubPlanDto, Record<string, never>>('plan/promised-payment', {});
    },
    getDevices(): Promise<ClubPlanDevicesDto> {
      return api.get<ClubPlanDevicesDto>('plan/devices');
    },
    setDevices(deviceIds: string[]): Promise<ClubPlanDevicesDto> {
      return api.put<ClubPlanDevicesDto, SetClubPlanDevicesRequest>('plan/devices', { deviceIds });
    },
    // «Сеть → Реклама»: что из рекламы платформы идёт и шло на ПК клуба (клуб — тоже распространитель).
    listPlatformAds(): Promise<ClubAdsDto> {
      return api.get<ClubAdsDto>('platform-ads');
    }
  };
}
