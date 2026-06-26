import { hasAnyPermission, permissionNames } from '../operatorPermissions';
import type { OperatorAuthSession } from '../authClient';

export type StockTab = 'levels' | 'receiving' | 'journal' | 'inventory';

export const STOCK_TAB_ORDER: readonly StockTab[] = ['levels', 'receiving', 'journal', 'inventory'];

// Права под-вкладок «Склада»: levels — просмотр инвентаря; receiving/inventory — управление складом (запись); journal — просмотр.
export const STOCK_TAB_PERMISSIONS: Record<StockTab, readonly string[]> = {
  levels: [permissionNames.viewInventory, permissionNames.manageInventoryStock],
  receiving: [permissionNames.manageInventoryStock],
  journal: [permissionNames.viewInventory, permissionNames.manageInventoryStock],
  inventory: [permissionNames.manageInventoryStock],
};

export function visibleStockTabs(session: OperatorAuthSession | null): StockTab[] {
  return STOCK_TAB_ORDER.filter((tab) => hasAnyPermission(session, STOCK_TAB_PERMISSIONS[tab]));
}
