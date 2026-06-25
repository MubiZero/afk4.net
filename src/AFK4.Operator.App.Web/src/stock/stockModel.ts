import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { OperatorAuthSession } from '../authClient';

export type StockTab = 'levels';

export const STOCK_TAB_ORDER: readonly StockTab[] = ['levels'];

// Права под-вкладок «Склада»: levels требует хотя бы одного из прав инвентаря.
export const STOCK_TAB_PERMISSIONS: Record<StockTab, readonly string[]> = {
  levels: [permissionNames.viewInventory, permissionNames.manageInventoryStock]
};

export function visibleStockTabs(session: OperatorAuthSession | null): StockTab[] {
  return STOCK_TAB_ORDER.filter((tab) => hasAnyPermission(session, STOCK_TAB_PERMISSIONS[tab]));
}
