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
import { createInventoryClient } from './inventory';
import { createDeviceClient } from './devices';
import { createDiagnosticsClient } from './diagnostics';
import { createUpdateClient } from './updates';
import { createAuditClient } from './audit';
import { createMoneyActionClient } from './moneyActions';
import { createAccountClient } from './account';
import { createShopOrderClient } from './shopOrders';
import { createLoyaltySettingsClient } from './loyaltySettings';
import { createEskhataConfigClient } from './eskhataConfig';
import { createNewsClient } from './news';
import { createMediaClient } from './media';
import { createDcTopUpClient } from './dcTopUps';
import { createDcConfigClient } from './dcConfig';

export function createOperatorApiClients(api: PlatformApiClient) {
  return {
    floorMap: createFloorMapClient(api),
    sessions: createSessionClient(api),
    pos: createPosClient(api),
    players: createPlayerClient(api),
    dashboard: createDashboardClient(api),
    reservations: createReservationClient(api),
    shifts: createShiftClient(api),
    shiftRevenue: createShiftRevenueClient(api),
    settings: createSettingsClient(api),
    inventory: createInventoryClient(api),
    devices: createDeviceClient(api),
    diagnostics: createDiagnosticsClient(api),
    updates: createUpdateClient(api),
    audit: createAuditClient(api),
    moneyActions: createMoneyActionClient(api),
    account: createAccountClient(api),
    shopOrders: createShopOrderClient(api),
    loyaltySettings: createLoyaltySettingsClient(api),
    eskhataConfig: createEskhataConfigClient(api),
    news: createNewsClient(api),
    media: createMediaClient(api),
    dcTopUps: createDcTopUpClient(api),
    dcConfig: createDcConfigClient(api)
  };
}
