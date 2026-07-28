import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import { permissionNames, hasPermission } from './operatorPermissions';

export type QuickActionId =
  | 'start_session'
  | 'sell_product'
  | 'create_player'
  | 'new_reservation'
  | 'top_up_wallet'
  | 'cash_in_order'
  | 'cash_out_order'
  | 'stock_intake';

export type QuickActionGroup = 'sessions_clients' | 'money_stock';

export interface QuickAction {
  id: QuickActionId;
  labelKey: MessageKey;
  group: QuickActionGroup;
  permission: string;
  hotkeyHint: string;
  stageKey: MessageKey;
}

export const quickActions: QuickAction[] = [
  {
    id: 'start_session',
    labelKey: 'op.command.action.start_session',
    group: 'sessions_clients',
    permission: permissionNames.startSession,
    hotkeyHint: 'Ctrl+Alt+Z',
    stageKey: 'op.command.stage.map'
  },
  {
    id: 'sell_product',
    labelKey: 'op.command.action.sell_product',
    group: 'sessions_clients',
    permission: permissionNames.createPosSale,
    hotkeyHint: 'Ctrl+Alt+S',
    stageKey: 'op.command.stage.cashier'
  },
  {
    id: 'create_player',
    labelKey: 'op.command.action.create_player',
    group: 'sessions_clients',
    permission: permissionNames.createPlayerAccount,
    hotkeyHint: 'Ctrl+Alt+N',
    stageKey: 'op.command.stage.players'
  },
  {
    id: 'new_reservation',
    labelKey: 'op.command.action.new_reservation',
    group: 'sessions_clients',
    permission: permissionNames.manageReservations,
    hotkeyHint: 'Ctrl+Alt+B',
    stageKey: 'op.command.stage.booking'
  },
  {
    id: 'top_up_wallet',
    labelKey: 'op.command.action.top_up_wallet',
    group: 'money_stock',
    permission: permissionNames.topUpWallet,
    hotkeyHint: 'Ctrl+Alt+D',
    stageKey: 'op.command.stage.players'
  },
  {
    id: 'cash_in_order',
    labelKey: 'op.command.action.cash_in_order',
    group: 'money_stock',
    permission: permissionNames.manageShiftCash,
    hotkeyHint: '',
    stageKey: 'op.command.stage.cashier'
  },
  {
    id: 'cash_out_order',
    labelKey: 'op.command.action.cash_out_order',
    group: 'money_stock',
    permission: permissionNames.manageShiftCash,
    hotkeyHint: '',
    stageKey: 'op.command.stage.cashier'
  },
  {
    id: 'stock_intake',
    labelKey: 'op.command.action.stock_intake',
    group: 'money_stock',
    permission: permissionNames.manageInventoryStock,
    hotkeyHint: '',
    stageKey: 'op.command.stage.management'
  }
];

export function getVisibleQuickActions(session: OperatorAuthSession | null): QuickAction[] {
  return quickActions.filter((action) => hasPermission(session, action.permission));
}

export type QuickActionHandler = () => void;

export interface CommandRegistry {
  register(id: QuickActionId, handler: QuickActionHandler): void;
  dispatch(id: QuickActionId): boolean;
}

export function createCommandRegistry(): CommandRegistry {
  const handlers = new Map<QuickActionId, QuickActionHandler>();
  return {
    register(id, handler) {
      handlers.set(id, handler);
    },
    dispatch(id) {
      const handler = handlers.get(id);
      if (!handler) {
        return false;
      }
      handler();
      return true;
    }
  };
}
