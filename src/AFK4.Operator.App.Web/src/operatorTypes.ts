import type { OperatorAuthSession } from './authClient';
import type { getOperatorConfig } from './operatorConfig';
import type { PaymentPartDto } from './operatorApiClients';
import type { SeatSummary } from './operatorData';

export type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'pos' | 'shop_orders' | 'players' | 'payments' | 'payment_cards' | 'logs' | 'settings' | 'review' | 'loyalty' | 'news' | 'shifts';
export type DashboardPeriod = 'today' | 'week' | 'month' | 'custom';
export type AuthStatus = 'checking' | 'signed-out' | 'signed-in';
export type FeedbackState = 'idle' | 'pending' | 'confirmed' | 'failed';
export type Feedback = { label: string; state: FeedbackState; detail?: string };
export type CriticalConfirmationTone = 'warning' | 'danger';
export type LoadStatus = 'fixture' | 'loading' | 'backend' | 'failed';
// Фильтры карты: всё / свободно / сессии / нет связи. Один серый бакет «нет связи» вместо
// прежнего зоопарка (проблемы/сбой/обслуживание) — см. SeatTone.
export type MapFilterId = 'all' | 'ready' | 'active' | 'offline';
export type MapViewMode = 'grid' | 'plan';
export type OperatorConfig = ReturnType<typeof getOperatorConfig>;
export type OperatorBackendContext = {
  config: OperatorConfig;
  session: OperatorAuthSession;
  branchId: string;
};
export type SessionBillingModeId = 'guest' | 'prepaid_wallet' | 'package' | 'postpaid_debt';
export type SessionBillingSelection = {
  mode: SessionBillingModeId;
  playerAccountId?: string | null;
  tariffRuleVersionId: string;
  tariffVersionId?: string | null;
  playerPackageId?: string | null;
};
export type SeatActionResult = {
  detail?: string;
};
export type SessionStartDurationMode = 'fixed' | 'open';
export type SeatActionRequest =
  | { type: 'start'; seat: SeatSummary; billing: SessionBillingSelection; durationMode: SessionStartDurationMode; durationMinutes?: number | null }
  | { type: 'extend'; seat: SeatSummary; minutes: number; billing: SessionBillingSelection }
  | { type: 'transfer'; seat: SeatSummary; targetSeatId: string }
  | { type: 'end'; seat: SeatSummary }
  | { type: 'checkout'; seat: SeatSummary; payments: PaymentPartDto[] };
export type PcControlActionId = 'status' | 'lock' | 'unlock' | 'reboot' | 'shutdown' | 'wake' | 'admin';
export type PcControlActionResult = {
  detail: string;
};
