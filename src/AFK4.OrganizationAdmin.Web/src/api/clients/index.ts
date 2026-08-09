import { PlatformApiClient } from '../../platformApi';
import { createFloorMapClient } from './floorMap';
import { createSessionClient } from './sessions';
import { createPosClient } from './pos';
import { createPlayerClient } from './players';
import { createDashboardClient } from './dashboard';
import { createReservationClient } from './reservations';
import { createShiftClient } from './shifts';
import { createShiftRevenueClient } from './shiftRevenue';
import { createSettingsClient } from './settings';
import { createOrgBranchesClient } from './orgBranches';
import { createOrgBillingClient } from './orgBilling';
import { createOrgAuditClient } from './orgAudit';
import { createInventoryClient } from './inventory';
import { createDeviceClient } from './devices';
import { createDiagnosticsClient } from './diagnostics';
import { createUpdateClient } from './updates';
import { createAuditClient } from './audit';
import { createMoneyActionClient } from './moneyActions';
import { createAccountClient } from './account';
import { createShopOrderClient } from './shopOrders';
import { createLoyaltySettingsClient } from './loyaltySettings';
import { createFeaturesClient } from './features';
import { createEskhataConfigClient } from './eskhataConfig';
import { createNewsClient } from './news';
import { createPlatformMessagesClient } from './platformMessages';
import { createMediaClient } from './media';
import { createDcTopUpClient } from './dcTopUps';
import { createDcConfigClient } from './dcConfig';
import { createReportsClient } from './reports';
import { withCriticalUpdateActivity } from '../../updateActivity';

export function createOperatorApiClients(api: PlatformApiClient, organizationId: string) {
  const organizationApi = withCriticalUpdateActivity(api.forOrganization(organizationId));
  return {
    floorMap: createFloorMapClient(organizationApi),
    sessions: createSessionClient(organizationApi),
    pos: createPosClient(organizationApi),
    players: createPlayerClient(organizationApi),
    dashboard: createDashboardClient(organizationApi),
    reservations: createReservationClient(organizationApi),
    shifts: createShiftClient(organizationApi),
    shiftRevenue: createShiftRevenueClient(organizationApi),
    settings: createSettingsClient(organizationApi),
    orgBranches: createOrgBranchesClient(organizationApi),
    orgBilling: createOrgBillingClient(organizationApi),
    orgAudit: createOrgAuditClient(organizationApi),
    inventory: createInventoryClient(organizationApi),
    devices: createDeviceClient(organizationApi),
    diagnostics: createDiagnosticsClient(organizationApi),
    updates: createUpdateClient(organizationApi),
    audit: createAuditClient(organizationApi),
    moneyActions: createMoneyActionClient(organizationApi),
    account: createAccountClient(organizationApi),
    shopOrders: createShopOrderClient(organizationApi),
    loyaltySettings: createLoyaltySettingsClient(organizationApi),
    features: createFeaturesClient(organizationApi),
    eskhataConfig: createEskhataConfigClient(organizationApi),
    news: createNewsClient(organizationApi),
    platformMessages: createPlatformMessagesClient(organizationApi),
    media: createMediaClient(organizationApi),
    dcTopUps: createDcTopUpClient(organizationApi),
    dcConfig: createDcConfigClient(organizationApi),
    reports: createReportsClient(organizationApi)
  };
}
