import {
  AlertTriangle,
  ArrowRightLeft,
  Banknote,
  CalendarClock,
  CircleDollarSign,
  ClipboardCheck,
  Clock3,
  LockKeyhole,
  Maximize2,
  Minus,
  MonitorCheck,
  Plus,
  Power,
  ReceiptText,
  Search,
  ShieldAlert,
  Square,
  TimerReset,
  UnlockKeyhole,
  UserRoundPlus,
  Wifi,
  Wrench,
  X
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useEffect, useMemo, useRef, useState, type CSSProperties, type FormEvent, type MouseEvent, type ReactNode } from 'react';
import { minorToMajor, majorToMinor } from '@afk4/money';
import { formatNumber as formatLocaleNumber, formatDateParts } from '@afk4/formatting';
import { I18nProvider } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { loadOperatorSession, refreshOperatorSession, signInOperator, signOutOperator, type OperatorAuthSession, type OperatorSignInRequest } from './authClient';
import {
  applyDeviceStatusToSeats,
  createFixtureFloorMapState,
  hydrateFloorMapStateFromCache,
  mapFloorMapDtoToState,
  offlineBannerText,
  refreshFloorMapRemaining,
  type FloorMapLoadStatus,
  type OperatorFloorMapState
} from './floorMapState';
import { loadFloorMapCache, saveFloorMapCache } from './floorMapCache';
import {
  acknowledgeAction,
  enqueueAction,
  loadActionOutbox,
  reconcileActionOutbox,
  type OperatorCommandType
} from './actionOutbox';
import { isHostBridgeUnavailableError, postHostRequest, postHostWindowCommand, postHostWindowResize, type HostWindowResizeEdge } from './hostBridge';
import {
  buildCheckoutPayments,
  checkoutMethods,
  checkoutMethodLabels,
  formatBilledDuration,
  formatCheckoutAmount,
  initialCheckoutDrafts,
  validateCheckoutPayments,
  type CheckoutMethod,
  type CheckoutPaymentDraft
} from './checkoutState';
import { ConnectionResolutionScreen, isOperatorTenantBlocked } from './ConnectionResolutionScreen';
import {
  BridgeOperatorConnectionStorage,
  ConnectionResolver,
  LocalStorageOperatorConnectionStorage,
  OperatorTenantStatus,
  type OperatorConnectionStorage,
  type ResolvedOperatorConnection,
  type ResolveOperatorConnectionResponse
} from './connectionResolver';
import {
  createOperatorApiClients,
  type AuditSearchResultDto,
  type BranchProfileDto,
  type BranchDiagnosticsDto,
  type CashMovementDto,
  type DeviceCommandStatusDto,
  type DeviceInventoryItemDto,
  type OperatorDashboardSummaryDto,
  type PackageOptionDto,
  type PlayerPackageDto,
  type PlayerSearchResultDto,
  type PaymentPartDto,
  type PosProductDto,
  type PosSaleDto,
  type ReceiptDto,
  type SessionCheckoutQuoteResponse,
  type ReservationSearchResultDto,
  type ReportResultDto,
  type ShiftDto,
  type StaffUserDto,
  type StockMovementDto,
  type TariffOptionDto,
  type UpdatePackageDto,
  type UpdateRolloutStatusDto,
  type MoneyActionRequestDto,
  type WalletSummaryDto,
  type ZoneDto
} from './operatorApiClients';
import { getOperatorConfig } from './operatorConfig';
import {
  createOperatorRealtimeClient,
  type DeviceCommandResultDto,
  type DeviceStatusChangedDto,
  type OperatorRealtimeConnectionState
} from './operatorRealtime';
import { navItems, seats, type SeatSummary, type SeatTone } from './operatorData';
import { PlatformApiClient, PlatformApiError } from './platformApi';
import { PaymentGatewaysWorkspace } from './PaymentGatewaysWorkspace';

type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'pos' | 'players' | 'payments' | 'payment_cards' | 'logs' | 'settings' | 'review';
type DashboardPeriod = 'today' | 'week' | 'month' | 'custom';
type AuthStatus = 'checking' | 'signed-out' | 'signed-in';
type FeedbackState = 'idle' | 'pending' | 'confirmed' | 'failed';
type Feedback = { label: string; state: FeedbackState; detail?: string };
type CriticalConfirmationTone = 'warning' | 'danger';
type LoadStatus = 'fixture' | 'loading' | 'backend' | 'failed';
type MapFilterId = 'all' | 'ready' | 'active' | 'attention' | 'offline';
type MapViewMode = 'grid' | 'table';
type OperatorConfig = ReturnType<typeof getOperatorConfig>;
type OperatorBackendContext = {
  config: OperatorConfig;
  session: OperatorAuthSession;
  branchId: string;
};
type SessionBillingModeId = 'guest' | 'prepaid_wallet' | 'package' | 'postpaid_debt';
type SessionBillingSelection = {
  mode: SessionBillingModeId;
  playerAccountId?: string | null;
  tariffRuleVersionId: string;
  tariffVersionId?: string | null;
  playerPackageId?: string | null;
};
type SeatActionResult = {
  detail?: string;
};
type SessionStartDurationMode = 'fixed' | 'open';
type SeatActionRequest =
  | { type: 'start'; seat: SeatSummary; billing: SessionBillingSelection; durationMode: SessionStartDurationMode }
  | { type: 'extend'; seat: SeatSummary; minutes: number; billing: SessionBillingSelection }
  | { type: 'transfer'; seat: SeatSummary; targetSeatId: string }
  | { type: 'end'; seat: SeatSummary }
  | { type: 'checkout'; seat: SeatSummary; payments: PaymentPartDto[] };
type PcControlActionId = 'status' | 'lock' | 'unlock' | 'reboot' | 'shutdown' | 'wake' | 'admin';
type PcControlActionResult = {
  detail: string;
};

const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'pos', 'players', 'payments', 'payment_cards', 'logs', 'settings', 'review'];
const defaultSessionDurationMinutes = 60;
const defaultTariffRuleVersionId = 'manual-v1';
const shellOperationalRefreshMs = 30_000;
const billingModeOptions: Array<{ id: SessionBillingModeId; label: string; detail: string }> = [
  { id: 'guest', label: 'Гость', detail: 'без списания' },
  { id: 'prepaid_wallet', label: 'Депозит', detail: 'списать с баланса' },
  { id: 'package', label: 'Пакет', detail: 'списать минуты' },
  { id: 'postpaid_debt', label: 'Постоплата', detail: 'долг игрока' }
];
const mapFilterOptions: Array<{ id: MapFilterId; label: string }> = [
  { id: 'all', label: 'Все' },
  { id: 'ready', label: 'Свободно' },
  { id: 'active', label: 'Сессии' },
  { id: 'attention', label: 'Проблемы' },
  { id: 'offline', label: 'Нет связи' }
];
const permissionNames = {
  viewFloorMap: 'floor_map.view',
  startSession: 'sessions.start',
  extendSession: 'sessions.extend',
  transferSession: 'sessions.transfer',
  endSession: 'sessions.end',
  viewPlayers: 'players.view',
  createPlayerAccount: 'players.create',
  viewBilling: 'billing.view',
  topUpWallet: 'billing.wallet.top_up',
  payDebt: 'billing.debt.pay',
  viewPackages: 'packages.view',
  managePackages: 'packages.manage',
  purchasePackage: 'packages.purchase',
  viewShift: 'shifts.view',
  openShift: 'shifts.open',
  closeShift: 'shifts.close',
  manageShiftCash: 'shifts.cash.manage',
  viewReports: 'reports.view',
  viewReservations: 'reservations.view',
  manageReservations: 'reservations.manage',
  createPosSale: 'pos.sales.create',
  payPosSale: 'pos.sales.pay',
  refundPosSale: 'pos.sales.refund',
  voidPosSale: 'pos.sales.void',
  viewInventory: 'inventory.view',
  manageInventoryStock: 'inventory.stock.manage',
  managePosCatalog: 'pos.catalog.manage',
  viewReceipt: 'receipts.view',
  viewDiagnostics: 'diagnostics.view',
  manageBranchStaff: 'identity.branch_staff.manage',
  manageRoles: 'identity.roles.manage',
  manageLayout: 'layout.manage',
  createDeviceEnrollmentCode: 'devices.enrollment_codes.create',
  assignDeviceSeat: 'devices.seat_assignment.assign',
  viewDeviceDetail: 'devices.detail.view',
  dispatchDeviceCommand: 'devices.commands.dispatch',
  rotateDeviceCredential: 'devices.credentials.rotate',
  revokeDeviceCredential: 'devices.credentials.revoke',
  manageTariffs: 'tariffs.manage',
  viewTariffs: 'tariffs.view',
  viewUpdateStatus: 'updates.status.view',
  manageUpdatePackages: 'updates.packages.manage',
  manageUpdateRollouts: 'updates.rollouts.manage',
  viewDeviceCommandStatus: 'devices.commands.status.view',
  viewAudit: 'audit.view',
  approveMoneyAction: 'billing.money_action.approve',
  managePaymentGateways: 'payments.gateways.manage'
} as const;

const staffRoleOptions = ['cashier_operator', 'shift_supervisor', 'branch_manager', 'technician', 'accountant_auditor'] as const;

const workspacePermissionRules: Record<WorkspaceId, readonly string[]> = {
  map: [permissionNames.viewFloorMap],
  dashboard: [permissionNames.viewReports],
  booking: [permissionNames.viewReservations],
  pos: [
    permissionNames.viewInventory,
    permissionNames.createPosSale,
    permissionNames.payPosSale,
    permissionNames.refundPosSale,
    permissionNames.voidPosSale,
    permissionNames.viewShift,
    permissionNames.viewReports
  ],
  players: [
    permissionNames.viewPlayers,
    permissionNames.createPlayerAccount,
    permissionNames.viewBilling,
    permissionNames.topUpWallet,
    permissionNames.payDebt,
    permissionNames.viewPackages,
    permissionNames.purchasePackage
  ],
  payments: [permissionNames.viewShift, permissionNames.openShift, permissionNames.viewReports],
  payment_cards: [permissionNames.managePaymentGateways],
  logs: [permissionNames.viewAudit, permissionNames.viewDiagnostics],
  settings: [
    permissionNames.manageBranchStaff,
    permissionNames.manageLayout,
    permissionNames.createDeviceEnrollmentCode,
    permissionNames.assignDeviceSeat,
    permissionNames.viewDeviceDetail,
    permissionNames.rotateDeviceCredential,
    permissionNames.revokeDeviceCredential,
    permissionNames.viewInventory,
    permissionNames.manageInventoryStock,
    permissionNames.managePosCatalog,
    permissionNames.managePackages,
    permissionNames.viewDiagnostics,
    permissionNames.viewUpdateStatus,
    permissionNames.manageUpdatePackages,
    permissionNames.manageUpdateRollouts,
    permissionNames.manageTariffs,
    permissionNames.viewTariffs
  ],
  review: [permissionNames.approveMoneyAction]
};

const toneLabels: Record<SeatTone, string> = {
  ready: 'Готов',
  active: 'Активно',
  pending: 'Команда',
  warning: 'Внимание',
  blocking: 'Блокер',
  offline: 'Офлайн',
  service: 'Сервис'
};

const problemTones = new Set<SeatTone>(['pending', 'warning', 'blocking', 'offline', 'service']);
const emptyFeedback: Feedback = { label: '', state: 'idle' };
const pcControlLabel = 'Управление ПК';
const pcControlTitle = 'Команды для выбранного ПК: статус, блокировка, питание и сервисный доступ';

function handleWindowDragStart(event: MouseEvent<HTMLElement>) {
  if (event.button !== 0) {
    return;
  }

  const target = event.target as HTMLElement;
  if (event.detail > 1 || target.closest('button, input, select, textarea, .command-search, .window-resize-handle')) {
    return;
  }

  postHostWindowCommand('drag');
}

function handleWindowTitleDoubleClick(event: MouseEvent<HTMLElement>) {
  const target = event.target as HTMLElement;
  if (target.closest('button, input, select, textarea, .command-search, .window-resize-handle')) {
    return;
  }

  postHostWindowCommand('maximize');
}

function toDateInputValue(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

function toDateTimeInputValue(date: Date) {
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${toDateInputValue(date)}T${hours}:${minutes}`;
}

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

function addMinutes(date: Date, minutes: number) {
  const next = new Date(date);
  next.setMinutes(next.getMinutes() + minutes);
  return next;
}

function countPeriodDays(from: string, to: string) {
  const fromDate = new Date(`${from}T00:00:00`);
  const toDate = new Date(`${to}T00:00:00`);

  if (Number.isNaN(fromDate.getTime()) || Number.isNaN(toDate.getTime()) || toDate < fromDate) {
    return 1;
  }

  return Math.max(1, Math.round((toDate.getTime() - fromDate.getTime()) / 86_400_000) + 1);
}

function formatCompactNumber(value: number) {
  if (value >= 1000) {
    return `${(value / 1000).toFixed(value >= 10000 ? 0 : 1)}k`;
  }

  return String(value);
}

function pluralRu(value: number, forms: [string, string, string]) {
  const absolute = Math.abs(value) % 100;
  const last = absolute % 10;

  if (absolute > 10 && absolute < 20) {
    return forms[2];
  }

  if (last === 1) {
    return forms[0];
  }

  if (last >= 2 && last <= 4) {
    return forms[1];
  }

  return forms[2];
}

function parseMoney(value: string) {
  const parsed = Number(value.replace(/[^\d-]/g, ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

function triggerFeedback(
  setFeedback: (feedback: Feedback) => void,
  label: string,
  finalState: Exclude<FeedbackState, 'idle' | 'pending'> = 'failed',
  detail = 'Для этого действия нет контракта платформы.'
) {
  if (finalState === 'failed') {
    setFeedback({ label, state: 'failed', detail });
    return;
  }

  setFeedback({ label, state: 'pending' });
  window.setTimeout(() => setFeedback({ label, state: finalState }), 620);
}

function feedbackText(feedback: Feedback) {
  if (feedback.state === 'pending') {
    return `${feedback.label}: ждём подтверждение платформы`;
  }

  if (feedback.state === 'failed') {
    return feedback.detail ?? `${feedback.label}: нужен повтор или проверка`;
  }

  if (feedback.state === 'confirmed') {
    if (feedback.detail) {
      return `${feedback.label}: ${feedback.detail}`;
    }

    return `${feedback.label}: подтверждено`;
  }

  return '';
}

function FeedbackNotice({ feedback }: { feedback: Feedback }) {
  if (feedback.state === 'idle') {
    return null;
  }

  return (
    <div className={`feedback-notice ${feedback.state}`} role="status" aria-live="polite">
      <span>{feedbackText(feedback)}</span>
    </div>
  );
}

function CriticalActionConfirmation({
  title,
  detail,
  impact,
  confirmLabel,
  cancelLabel = 'Отмена',
  tone = 'danger',
  disabled = false,
  children,
  onConfirm,
  onCancel
}: {
  title: string;
  detail: string;
  impact: string;
  confirmLabel: string;
  cancelLabel?: string;
  tone?: CriticalConfirmationTone;
  disabled?: boolean;
  children?: ReactNode;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  return (
    <section className={`critical-confirmation ${tone}`} role="alertdialog" aria-label={title}>
      <div>
        <strong>{title}</strong>
        <span>{detail}</span>
        <em>{impact}</em>
      </div>
      {children}
      <div className="critical-confirmation-actions">
        <button type="button" onClick={onCancel} disabled={disabled}>{cancelLabel}</button>
        <button type="button" className="danger" onClick={onConfirm} disabled={disabled}>{confirmLabel}</button>
      </div>
    </section>
  );
}

const checkoutMethodIcons: Record<CheckoutMethod, ReactNode> = {
  cash: <Banknote size={14} />,
  card_manual: <CircleDollarSign size={14} />,
  wallet: <ReceiptText size={14} />
};

/**
 * "Завершить и принять оплату": fetches the read-only checkout quote, shows the
 * unified bill (Наиграно • Время • Снеки • Итого), and lets the operator settle
 * it with one or more split-payment parts (cash / card / deposit) before the PC
 * is locked. Confirm routes the parts through the shared seat-action handler.
 */
function CheckoutDialog({
  seat,
  backend,
  disabled,
  onCancel,
  onConfirm
}: {
  seat: SeatSummary;
  backend: OperatorBackendContext;
  disabled: boolean;
  onCancel: () => void;
  onConfirm: (payments: PaymentPartDto[]) => void;
}) {
  const [quote, setQuote] = useState<SessionCheckoutQuoteResponse | null>(null);
  const [status, setStatus] = useState<'loading' | 'ready' | 'failed'>('loading');
  const [error, setError] = useState<string | null>(null);
  const [drafts, setDrafts] = useState<CheckoutPaymentDraft[]>([{ method: 'cash', amountText: '' }]);

  useEffect(() => {
    let disposed = false;
    const sessionId = seat.activeSessionId;
    if (!sessionId) {
      setStatus('failed');
      setError('На выбранном ПК нет активной сессии.');
      return undefined;
    }

    setStatus('loading');
    setError(null);
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.sessions.getCheckoutQuote(sessionId)
      .then((result) => {
        if (disposed) {
          return;
        }

        setQuote(result);
        setDrafts(initialCheckoutDrafts(result.grandTotal.minorUnits));
        setStatus('ready');
      })
      .catch((fetchError) => {
        if (disposed) {
          return;
        }

        setStatus('failed');
        setError(projectOperatorError(fetchError).detail);
      });

    return () => {
      disposed = true;
    };
  }, [seat.activeSessionId, backend.config.platformBaseUrl, backend.session.accessToken]);

  const currencyCode = quote?.grandTotal.currencyCode ?? '';
  const grandTotal = quote?.grandTotal.minorUnits ?? 0;
  const walletBalance = quote?.walletBalance?.minorUnits ?? null;
  const validation = validateCheckoutPayments(drafts, grandTotal, walletBalance);
  const canConfirm = status === 'ready' && !disabled && validation.canSubmit;

  const updateDraft = (index: number, patch: Partial<CheckoutPaymentDraft>) => {
    setDrafts((current) => current.map((draft, position) => (position === index ? { ...draft, ...patch } : draft)));
  };
  const addDraft = () => {
    setDrafts((current) => [...current, { method: 'cash', amountText: '' }]);
  };
  const removeDraft = (index: number) => {
    setDrafts((current) => (current.length <= 1 ? current : current.filter((_, position) => position !== index)));
  };
  const suggestWallet = () => {
    if (walletBalance === null) {
      return;
    }

    const fromWallet = Math.min(walletBalance, grandTotal);
    const remainder = grandTotal - fromWallet;
    const next: CheckoutPaymentDraft[] = [{ method: 'wallet', amountText: formatCheckoutAmount(fromWallet) }];
    if (remainder > 0) {
      next.push({ method: 'cash', amountText: formatCheckoutAmount(remainder) });
    }

    setDrafts(next);
  };

  return (
    <section className="critical-confirmation warning checkout-dialog" role="alertdialog" aria-label="Завершить и принять оплату">
      <div>
        <strong>Завершить и принять оплату</strong>
        <span>{seat.name} · {seat.player}</span>
        <em>После оплаты сессия закрывается, платформа блокирует ПК.</em>
      </div>

      {status === 'loading' && <p className="checkout-loading">Расчёт счёта…</p>}
      {status === 'failed' && <p className="checkout-error">{error ?? 'Не удалось получить счёт.'}</p>}

      {status === 'ready' && quote && (
        <>
          <dl className="checkout-breakdown">
            <div><dt>Наиграно</dt><dd>{formatBilledDuration(quote.billableSeconds)}</dd></div>
            <div><dt>Время</dt><dd>{formatMinorUnits(quote.timeCharge.minorUnits, currencyCode)}</dd></div>
            <div><dt>Снеки</dt><dd>{formatMinorUnits(quote.posTotal.minorUnits, currencyCode)}</dd></div>
            <div className="checkout-breakdown-total"><dt>Итого</dt><dd>{formatMinorUnits(grandTotal, currencyCode)}</dd></div>
          </dl>

          <div className="checkout-payments">
            {drafts.map((draft, index) => (
              <div className="checkout-payment-row" key={index}>
                <select
                  aria-label="Способ оплаты"
                  value={draft.method}
                  disabled={disabled}
                  onChange={(event) => updateDraft(index, { method: event.currentTarget.value as CheckoutMethod })}
                >
                  {checkoutMethods.map((method) => (
                    <option key={method} value={method}>{checkoutMethodLabels[method]}</option>
                  ))}
                </select>
                <span className="checkout-payment-icon">{checkoutMethodIcons[draft.method]}</span>
                <input
                  type="text"
                  inputMode="decimal"
                  aria-label="Сумма"
                  placeholder="0.00"
                  value={draft.amountText}
                  disabled={disabled}
                  onChange={(event) => updateDraft(index, { amountText: event.currentTarget.value })}
                />
                <button
                  type="button"
                  className="checkout-payment-remove"
                  aria-label="Убрать строку"
                  disabled={disabled || drafts.length <= 1}
                  onClick={() => removeDraft(index)}
                >
                  <X size={14} />
                </button>
              </div>
            ))}
            <div className="checkout-payment-controls">
              <button type="button" disabled={disabled} onClick={addDraft}><Plus size={14} />Ещё способ</button>
              {walletBalance !== null && walletBalance > 0 && (
                <button type="button" disabled={disabled} onClick={suggestWallet}>
                  <ReceiptText size={14} />Депозит ({formatMinorUnits(walletBalance, currencyCode)})
                </button>
              )}
            </div>
          </div>

          {validation.error
            ? <p className="checkout-error">{validation.error}</p>
            : <p className="checkout-balanced">Сумма совпадает</p>}
        </>
      )}

      <div className="critical-confirmation-actions">
        <button type="button" onClick={onCancel} disabled={disabled}>Отмена</button>
        <button
          type="button"
          className="danger"
          disabled={!canConfirm}
          onClick={() => onConfirm(buildCheckoutPayments(drafts, currencyCode))}
        >
          Принять оплату
        </button>
      </div>
    </section>
  );
}

function useAnimatedNumber(value: number, duration = 360) {
  const [displayValue, setDisplayValue] = useState(value);

  useEffect(() => {
    if (typeof window === 'undefined' || typeof window.requestAnimationFrame !== 'function') {
      setDisplayValue(value);
      return undefined;
    }

    const startValue = displayValue;
    const difference = value - startValue;

    if (difference === 0) {
      return undefined;
    }

    const startedAt = window.performance.now();
    let frame = 0;

    const tick = (now: number) => {
      const progress = Math.min(1, (now - startedAt) / duration);
      const eased = 1 - Math.pow(1 - progress, 3);
      setDisplayValue(Math.round(startValue + difference * eased));

      if (progress < 1) {
        frame = window.requestAnimationFrame(tick);
      }
    };

    frame = window.requestAnimationFrame(tick);
    return () => window.cancelAnimationFrame(frame);
  }, [value]);

  return displayValue;
}

function AnimatedNumber({
  value,
  formatter = (nextValue: number) => String(nextValue)
}: {
  value: number;
  formatter?: (nextValue: number) => string;
}) {
  return <>{formatter(useAnimatedNumber(value))}</>;
}

function countByTone(nextSeats: SeatSummary[], tone: SeatTone): number {
  return nextSeats.filter((seat) => seat.tone === tone).length;
}

function countProblems(nextSeats: SeatSummary[]): number {
  return nextSeats.filter((seat) => problemTones.has(seat.tone)).length;
}

function isPendingSeatCommand(seat: SeatSummary): boolean {
  return seat.tone === 'pending' || seat.command.toLowerCase().includes('pending');
}

function matchesMapFilter(seat: SeatSummary, filterId: MapFilterId): boolean {
  if (filterId === 'all') {
    return true;
  }

  if (filterId === 'ready') {
    return seat.tone === 'ready' && !seat.activeSessionId;
  }

  if (filterId === 'active') {
    return seat.tone === 'active' || seat.hasActiveSession === true || Boolean(seat.activeSessionId);
  }

  if (filterId === 'attention') {
    return problemTones.has(seat.tone);
  }

  return seat.tone === 'offline' || seat.isDeviceOnline === false;
}

function countByMapFilter(nextSeats: SeatSummary[], filterId: MapFilterId): number {
  return nextSeats.filter((seat) => matchesMapFilter(seat, filterId)).length;
}

function zoneClass(zone: string): string {
  if (zone.includes('VIP')) {
    return 'zone-vip';
  }

  if (zone.includes('Bootcamp')) {
    return 'zone-bootcamp';
  }

  if (zone.includes('C')) {
    return 'zone-c';
  }

  if (zone.includes('B')) {
    return 'zone-b';
  }

  return 'zone-a';
}

function SeatTile({
  seat,
  selected,
  onSelect
}: {
  seat: SeatSummary;
  selected?: boolean;
  onSelect: () => void;
}) {
  return (
    <article
      className={`seat-tile ${zoneClass(seat.zone)} state-${seat.tone}${selected ? ' selected' : ''}`}
      aria-label={`${seat.name} ${seat.stateLabel}`}
      aria-pressed={selected}
      onClick={onSelect}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onSelect();
        }
      }}
      role="button"
      tabIndex={0}
    >
      <header className="seat-head">
        <div>
          <strong>{seat.name}</strong>
          <span>{zoneLabel(seat.zone)}</span>
        </div>
        <span className="state-chip">{seat.stateLabel}</span>
      </header>
      <div className="seat-main">
        <span>{seat.player}</span>
        <span>{appVersionLabel(seat.app)}</span>
      </div>
      <footer>
        <strong>{seat.remaining}</strong>
        <span>{commandLabel(seat.command)}</span>
      </footer>
    </article>
  );
}

function StateFlag({ label, value, critical }: { label: string; value: string; critical?: boolean }) {
  return (
    <section className={`state-flag${critical ? ' critical' : ''}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </section>
  );
}

function billingLabel(value: string) {
  const normalized = value.toLowerCase();

  if (normalized.includes('wallet')) {
    return 'Депозит';
  }

  if (normalized.includes('package')) {
    return 'Пакет';
  }

  if (normalized.includes('postpaid') || normalized.includes('постоплата')) {
    return 'Постоплата';
  }

  if (normalized.includes('guest')) {
    return 'Гость';
  }

  return 'Не задан';
}

function zoneLabel(zone: string): string {
  return zone
    .replace('Console Lounge', 'Консольная зона')
    .replace('Main Hall', 'Основной зал')
    .replace('VIP Room', 'VIP-зал')
    .replace('Bootcamp', 'Буткемп');
}

function appVersionLabel(app: string): string {
  return app
    .replaceAll('Agent', 'Агент')
    .replaceAll('Shell', 'Оболочка');
}

function operatorDisplayNameLabel(displayName: string | null | undefined): string {
  const normalized = displayName?.trim();

  if (!normalized) {
    return 'Оператор смены';
  }

  if (/^cashier one$/i.test(normalized)) {
    return 'Оператор смены';
  }

  if (/^demo owner$/i.test(normalized)) {
    return 'Администратор клуба';
  }

  if (/^local branch manager$/i.test(normalized)) {
    return 'Менеджер филиала';
  }

  return normalized;
}

function staffRoleLabel(roleName: string): string {
  switch (roleName) {
    case 'cashier_operator':
      return 'Кассир-оператор';
    case 'shift_supervisor':
      return 'Старший смены';
    case 'branch_manager':
      return 'Управляющий';
    case 'technician':
      return 'Техник';
    case 'accountant_auditor':
      return 'Бухгалтер';
    default:
      return roleName || 'Роль не задана';
  }
}

function updateComponentLabel(component: string): string {
  switch (component) {
    case 'operator-app':
      return 'Приложение оператора';
    case 'agent-service':
      return 'Сервис агента';
    case 'player-shell':
      return 'Оболочка игрока';
    default:
      return 'Приложение';
  }
}

function updateChannelLabel(channel: string): string {
  switch (channel) {
    case 'internal':
      return 'Внутренний';
    case 'beta':
      return 'Бета';
    case 'stable':
      return 'Стабильный';
    default:
      return 'Канал';
  }
}

function updateTargetKindLabel(kind: string): string {
  switch (kind) {
    case 'branch':
      return 'Филиал';
    case 'device':
      return 'Отдельные ПК';
    default:
      return 'Цель';
  }
}

function updatePackageStateLabel(state: string): string {
  switch (state) {
    case 'registered':
      return 'Зарегистрирован';
    case 'validated':
      return 'Проверен';
    case 'rejected':
      return 'Отклонён';
    case 'retired':
      return 'Выведен';
    default:
      return 'Состояние';
  }
}

function updateRolloutStateLabel(state: string): string {
  switch (state) {
    case 'active':
      return 'Активна';
    case 'paused':
      return 'Пауза';
    case 'completed':
      return 'Завершена';
    case 'rollback_requested':
      return 'Запрошен откат';
    case 'rolled_back':
      return 'Откат выполнен';
    case 'cancelled':
      return 'Отменена';
    default:
      return 'Состояние';
  }
}

function updateDeviceStatusLabel(status: string): string {
  switch (status) {
    case 'installed':
      return 'Установлено';
    case 'target reached':
      return 'Цель достигнута';
    case 'pending':
      return 'Ожидает';
    case 'failed':
      return 'Ошибка';
    default:
      return 'Неизвестно';
  }
}

function stockMovementTypeLabel(type: string): string {
  switch (type) {
    case 'purchase':
      return 'Приход';
    case 'adjustment':
      return 'Коррекция';
    case 'sale':
      return 'Продажа';
    case 'write_off':
      return 'Списание';
    default:
      return 'Движение';
  }
}

function updateDeviceMessageLabel(message: string): string {
  if (message === 'target reached') {
    return 'цель достигнута';
  }

  if (message === 'installed') {
    return 'установлено';
  }

  return commandStatusMessageLabel(message);
}

function commandLabel(command: string) {
  if (command.includes('Lease fresh')) {
    return 'Сессия подтверждена';
  }

  if (command.includes('Unlock pending')) {
    return 'Разблокировка в процессе';
  }

  if (command.includes('Start pending')) {
    return 'Запуск в процессе';
  }

  if (command.includes('Stop pending')) {
    return 'Завершение в процессе';
  }

  if (command.includes('Payment check')) {
    return 'Проверить оплату';
  }

  if (command.includes('No route')) {
    return 'Нет связи с ПК';
  }

  if (command.includes('Idle')) {
    return 'Команд нет';
  }

  if (command.includes('Command failed')) {
    return 'Команда не выполнена';
  }

  if (command.includes('Technician')) {
    return 'Техобслуживание';
  }

  if (command.includes('Low balance')) {
    return 'Мало средств';
  }

  return command;
}

function deviceStatusLabel(device: string) {
  return device
    .replace('Device unassigned', 'Устройство не назначено')
    .replace('Online', 'Онлайн')
    .replace('Offline', 'Нет связи')
    .replaceAll('Agent', 'Агент')
    .replaceAll('Shell', 'Оболочка')
    .replace('unlocked', 'разблокирован')
    .replace('locked state unknown', 'статус блокировки неизвестен')
    .replace('locked', 'заблокирован');
}

function mapSeatStatus(seat: SeatSummary) {
  if (seat.tone === 'active') {
    return {
      label: 'Сессия активна',
      value: seat.remaining
    };
  }

  if (seat.tone === 'ready') {
    return {
      label: 'Свободен',
      value: 'готов к старту'
    };
  }

  if (seat.tone === 'pending') {
    return {
      label: 'Команда в пути',
      value: commandLabel(seat.command)
    };
  }

  if (seat.tone === 'warning') {
    return {
      label: 'Требует проверки',
      value: commandLabel(seat.command)
    };
  }

  if (seat.tone === 'offline') {
    return {
      label: 'Нет связи',
      value: commandLabel(seat.command)
    };
  }

  return {
    label: 'Сервис',
    value: commandLabel(seat.command)
  };
}

function floorMapLoadLabel(status: FloorMapLoadStatus, source: OperatorFloorMapState['source'], error: string | null) {
  if (status === 'loading') {
    return source === 'backend' ? 'Обновляем карту' : 'Загружаем карту';
  }

  if (status === 'failed') {
    return error ? `Ошибка платформы · ${error}` : 'Ошибка платформы · API недоступен';
  }

  return source === 'backend' ? 'Платформа подключена' : 'Локальные данные';
}

function workspaceLoadStatusLabel(status: LoadStatus, backendLabel: string): string {
  if (status === 'backend') {
    return backendLabel;
  }

  if (status === 'loading') {
    return 'Загружаем данные';
  }

  if (status === 'failed') {
    return 'Ошибка платформы';
  }

  return 'Локальные данные';
}

function dataSourceLabel(source: string): string {
  return source === 'backend' ? 'Платформа подключена' : 'Локальные данные';
}

function shellShiftLabel(
  shift: ShiftDto | null,
  summary: OperatorDashboardSummaryDto | null,
  status: LoadStatus,
  error: string | null
): string {
  const shiftSource = shift ?? readRecord(summary, 'shift');
  if (shiftSource !== null) {
    const state = readString(shiftSource, 'state').toLowerCase();
    if (state === 'open') {
      return `Смена открыта · с ${formatTime(readString(shiftSource, 'openedAtUtc'))}`;
    }

    if (state === 'closed') {
      return `Смена закрыта · ${formatTime(readString(shiftSource, 'closedAtUtc'))}`;
    }

    return `Смена · ${state || 'состояние неизвестно'}`;
  }

  if (status === 'loading') {
    return 'Смена · загрузка';
  }

  if (status === 'failed') {
    return error ? 'Смена · ошибка платформы' : 'Смена · нет доступа';
  }

  return 'Смена не открыта';
}

function shellPosLabel(summary: OperatorDashboardSummaryDto | null, status: LoadStatus): string {
  if (summary !== null) {
    const revenue = readRecord(summary, 'revenue');
    const posChecks = readNumber(revenue, 'posCheckCount', 0);
    return `Касса: ${posChecks} ${pluralRu(posChecks, ['чек', 'чека', 'чеков'])} сегодня`;
  }

  return status === 'loading' ? 'Касса: загрузка' : 'Касса: нет данных';
}

function shellModeLabel(mode: string): string {
  if (mode.includes('dev')) {
    return 'локальная сборка';
  }

  if (mode.includes('dist')) {
    return 'установленная сборка';
  }

  return mode;
}

function describeTechModeResult(
  seat: SeatSummary,
  device: Record<string, unknown>,
  diagnostics: BranchDiagnosticsDto
): string {
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const machineName = readString(device, 'machineName', seat.deviceName || seat.name);
  const agentVersion = readString(device, 'agentVersion', 'unknown');
  const shellVersion = readString(device, 'shellVersion', 'unknown');
  const pendingCommands = readNumber(commandSummary, 'pendingCommands', 0);
  const failedCommands = readNumber(commandSummary, 'failedCommands', 0);

  return `${machineName}: связь есть, Агент ${agentVersion}, оболочка ${shellVersion}; команд в работе: ${pendingCommands}, ошибок: ${failedCommands}`;
}

function projectAuthHostError(error: unknown, config: OperatorConfig): string {
  if (isHostBridgeUnavailableError(error) && config.runtime !== 'browser-dev') {
    return 'Нативный вход приложения оператора недоступен. Перезапустите приложение или проверьте WebView2.';
  }

  return projectOperatorError(error).detail;
}

function projectOperatorFacingError(error: unknown): string {
  const detail = projectOperatorError(error).detail;
  const platformDetail = extractPlatformError(detail);

  if (detail.includes('Only active or paused sessions can be ended') ||
    platformDetail?.includes('Only active or paused sessions can be ended')) {
    return 'Сеанс уже завершается или завершён. Дождитесь обновления карты.';
  }

  if (detail.includes('401 Unauthorized')) {
    return 'Сессия оператора устарела. Войдите снова.';
  }

  if (platformDetail) {
    return platformDetail;
  }

  return detail
    .replace('Platform API returned 400 Bad Request:', 'Платформа отклонила запрос:')
    .replace('Platform API returned 401 Unauthorized:', 'Сессия оператора устарела.')
    .replace('Platform API returned 500 Internal Server Error:', 'Платформа временно недоступна:');
}

function extractPlatformError(detail: string): string | null {
  const jsonStart = detail.indexOf('{');
  if (jsonStart < 0) {
    return null;
  }

  try {
    const parsed = JSON.parse(detail.slice(jsonStart)) as Record<string, unknown>;
    const error = parsed.error;
    return typeof error === 'string' && error.trim().length > 0 ? error : null;
  } catch {
    return null;
  }
}

function realtimeLabel(state: OperatorRealtimeConnectionState, error: string | null): string {
  if (state === 'connected') {
    return 'Связь подключена';
  }

  if (state === 'connecting') {
    return 'Связь устанавливается';
  }

  if (state === 'reconnecting') {
    return 'Связь восстанавливается';
  }

  return error ? 'Связь потеряна' : 'Связь отключена';
}

function resolveActiveBranchId(session: OperatorAuthSession, configBranchId?: string): string | null {
  return session.activeBranchId ?? configBranchId ?? session.branchIds[0] ?? null;
}

function matchesRealtimeScope(status: DeviceStatusChangedDto, session: OperatorAuthSession, branchId: string): boolean {
  return status.organizationId.toLowerCase() === session.organizationId.toLowerCase()
    && status.branchId.toLowerCase() === branchId.toLowerCase();
}

function matchesCommandResultScope(result: DeviceCommandResultDto, session: OperatorAuthSession, branchId: string): boolean {
  return result.organizationId.toLowerCase() === session.organizationId.toLowerCase()
    && result.branchId.toLowerCase() === branchId.toLowerCase();
}

function findSeatForDeviceStatus(nextSeats: SeatSummary[], status: DeviceStatusChangedDto): SeatSummary | null {
  const statusDeviceId = status.deviceId.toLowerCase();
  const statusMachineName = status.machineName.toLowerCase();
  return nextSeats.find((seat) =>
    (seat.deviceId ?? '').toLowerCase() === statusDeviceId ||
    seat.name.toLowerCase() === statusMachineName) ?? null;
}

function shouldReloadFloorMapAfterDeviceStatus(seat: SeatSummary, status: DeviceStatusChangedDto): boolean {
  return status.isLocked && (Boolean(seat.activeSessionId) || seat.hasActiveSession === true || isPendingSeatCommand(seat));
}

function createAuthenticatedOperatorClients(config: ReturnType<typeof getOperatorConfig>, session: OperatorAuthSession) {
  return createOperatorApiClients(new PlatformApiClient({
    baseUrl: config.platformBaseUrl,
    getAccessToken: () => session.accessToken
  }));
}

function isUnauthorizedPlatformError(error: unknown): boolean {
  return error instanceof PlatformApiError && error.status === 401;
}

function isUnauthorizedAuthError(error: unknown): boolean {
  return error instanceof Error && error.message.includes('401 Unauthorized');
}

async function clearStoredOperatorSession(): Promise<void> {
  try {
    await signOutOperator();
  } catch {
    // Best-effort cleanup; the original auth failure is the actionable error.
  }
}

async function loadBackendFloorMapState(
  config: ReturnType<typeof getOperatorConfig>,
  session: OperatorAuthSession,
  branchId: string
): Promise<OperatorFloorMapState> {
  const clients = createAuthenticatedOperatorClients(config, session);
  const floorMap = await clients.floorMap.getFloorMap(branchId);
  // Persist the last-known-good snapshot so the workspace can degrade to a read-only mirror offline (§6.5).
  saveFloorMapCache(branchId, floorMap, Date.now());
  return mapFloorMapDtoToState(floorMap);
}

function createIdempotencyKey(operationName: string): string {
  const unique = window.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return `${operationName}-${unique}`;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function readString(value: unknown, name: string, fallback = ''): string {
  if (!isRecord(value)) {
    return fallback;
  }

  const nextValue = value[name];
  return typeof nextValue === 'string' && nextValue.length > 0 ? nextValue : fallback;
}

function readNumber(value: unknown, name: string, fallback = 0): number {
  if (!isRecord(value)) {
    return fallback;
  }

  const nextValue = value[name];
  return typeof nextValue === 'number' && Number.isFinite(nextValue) ? nextValue : fallback;
}

function readBoolean(value: unknown, name: string, fallback = false): boolean {
  if (!isRecord(value)) {
    return fallback;
  }

  const nextValue = value[name];
  return typeof nextValue === 'boolean' ? nextValue : fallback;
}

function readArray<T = unknown>(value: unknown, name: string): T[] {
  if (!isRecord(value)) {
    return [];
  }

  const nextValue = value[name];
  return Array.isArray(nextValue) ? nextValue as T[] : [];
}

function readMoney(value: unknown, name: string): { currencyCode: string; minorUnits: number } | null {
  if (!isRecord(value)) {
    return null;
  }

  const money = value[name];
  if (!isRecord(money)) {
    return null;
  }

  const currencyCode = readString(money, 'currencyCode');
  const minorUnits = readNumber(money, 'minorUnits', Number.NaN);
  return currencyCode && Number.isFinite(minorUnits) ? { currencyCode, minorUnits } : null;
}

function readRecord(value: unknown, name: string): Record<string, unknown> | null {
  if (!isRecord(value)) {
    return null;
  }

  const nextValue = value[name];
  return isRecord(nextValue) ? nextValue : null;
}

function formatMinorUnits(minorUnits: number, currencyCode: string): string {
  const majorUnits = minorToMajor(minorUnits);
  const formatted = formatLocaleNumber(majorUnits, 'ru-RU', {
    maximumFractionDigits: Number.isInteger(majorUnits) ? 0 : 2,
    minimumFractionDigits: 0
  });

  return `${formatted} ${currencyCode}`;
}

function formatMoney(value: unknown, fallbackCurrencyCode: string): string {
  if (isRecord(value)) {
    const currencyCode = readString(value, 'currencyCode', fallbackCurrencyCode);
    const minorUnits = readNumber(value, 'minorUnits', 0);
    return formatMinorUnits(minorUnits, currencyCode);
  }

  return formatMinorUnits(0, fallbackCurrencyCode);
}

function parseMoneyInputMinorUnits(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) {
    return null;
  }

  const majorUnits = Number(normalized);
  return Number.isFinite(majorUnits) && majorUnits > 0 ? majorToMinor(majorUnits) : null;
}

function parseNonNegativeMoneyInputMinorUnits(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) {
    return null;
  }

  const majorUnits = Number(normalized);
  return Number.isFinite(majorUnits) && majorUnits >= 0 ? majorToMinor(majorUnits) : null;
}

function formatMoneyInputMinorUnits(minorUnits: number): string {
  return minorToMajor(minorUnits).toFixed(2);
}

function dashboardRangeQuery(from: string, to: string) {
  return {
    fromUtc: `${from}T00:00:00.000Z`,
    toUtc: `${to}T23:59:59.999Z`,
    limit: 8
  };
}

function emptyDashboardSummary(currencyCode: string, from: string, to: string): OperatorDashboardSummaryDto {
  const zeroMoney = { currencyCode, minorUnits: 0 };

  return {
    organizationId: '',
    branchId: '',
    fromUtc: `${from}T00:00:00.000Z`,
    toUtc: `${to}T23:59:59.999Z`,
    generatedAtUtc: new Date().toISOString(),
    shift: {
      shiftId: null,
      state: 'none',
      openedAtUtc: null,
      openedByStaffUserId: null,
      expectedCash: zeroMoney
    },
    revenue: {
      posNetSales: zeroMoney,
      gameplayRevenue: zeroMoney,
      totalRevenue: zeroMoney,
      posCheckCount: 0,
      newPlayerCount: 0
    },
    utilization: {
      totalSeats: 0,
      activeSessions: 0,
      endingSessions: 0,
      onlineDevices: 0,
      offlineDevices: 0,
      sessionStarts: 0,
      utilizationPercent: 0
    },
    alertPressure: {
      pendingCommands: 0,
      failedCommands: 0,
      offlineDevices: 0,
      endingSessions: 0,
      totalAlerts: 0
    },
    reservations: {
      activeReservations: 0,
      availableSlots: 0,
      source: 'none'
    },
    focusQueue: [],
    recentPayments: []
  };
}

function formatTime(value: unknown): string {
  if (typeof value !== 'string' || value.length === 0) {
    return '—';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return formatDateParts(date, 'ru-RU', {
    hour: '2-digit',
    minute: '2-digit'
  });
}

function formatDateTime(value: unknown): string {
  if (typeof value !== 'string' || value.length === 0) {
    return '—';
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return formatDateParts(date, 'ru-RU', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  });
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (character) => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;'
  })[character] ?? character);
}

function safeReceiptFileName(value: string): string {
  return value.replace(/[^a-zA-Z0-9._-]+/g, '-').replace(/^-+|-+$/g, '') || 'receipt';
}

function downloadTextFile(fileName: string, contents: string, mimeType = 'text/plain;charset=utf-8') {
  const url = window.URL.createObjectURL(new Blob([contents], { type: mimeType }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
}

function posSaleStateLabel(state: string): string {
  switch (state.toLowerCase()) {
    case 'paid':
      return 'Оплачен';
    case 'draft':
      return 'Черновик';
    case 'void':
    case 'voided':
      return 'Аннулирован';
    case 'refund':
    case 'refunded':
      return 'Возврат';
    case 'pending':
      return 'Ожидает оплаты';
    case 'sale':
      return 'Продажа';
    default:
      return 'Чек';
  }
}

function posReceiptTypeLabel(type: string): string {
  switch (type.toLowerCase()) {
    case 'sale':
    case 'paid':
      return 'Продажа';
    case 'refund':
    case 'refunded':
      return 'Возврат';
    case 'void':
    case 'voided':
      return 'Аннулирование';
    default:
      return 'Чек';
  }
}

function posSaleLineSummary(row: unknown): string {
  const lineCount = readNumber(row, 'lineCount', 0);
  const itemQuantity = readNumber(row, 'itemQuantity', 0);
  return `${lineCount} ${pluralRu(lineCount, ['строка', 'строки', 'строк'])} · ${itemQuantity} шт.`;
}

function shiftStateLabel(state: string): string {
  switch (state.toLowerCase()) {
    case 'open':
      return 'Открыта';
    case 'closed':
      return 'Закрыта';
    case 'closing':
      return 'Закрывается';
    case 'unknown':
      return 'Неизвестно';
    case 'нет смены':
      return 'Нет';
    default:
      return state ? 'Неизвестно' : 'Нет';
  }
}

function cashOperationTypeLabel(type: string): string {
  switch (type.toLowerCase()) {
    case 'opening':
      return 'Открытие смены';
    case 'closing':
      return 'Закрытие смены';
    case 'cash_in':
      return 'Внесение';
    case 'cash_out':
      return 'Изъятие';
    case 'refund':
      return 'Возврат';
    default:
      return 'Движение кассы';
  }
}

function paymentSourceLabel(source: string): string {
  switch (source.toLowerCase()) {
    case 'shift':
      return 'Смена';
    case 'cash':
      return 'Наличные';
    case 'pos':
    case 'sale':
      return 'Продажа';
    default:
      return 'Касса';
  }
}

function buildPosReceiptText(sale: PosSaleDto, receipt: Record<string, unknown> | null, currencyCode: string): string {
  const receiptNumber = readString(receipt, 'receiptNumber', 'чек');
  const receiptType = posReceiptTypeLabel(readString(receipt, 'receiptType', readString(sale, 'state', 'sale')));
  const createdAtUtc = readString(receipt, 'createdAtUtc', readString(sale, 'createdAtUtc'));
  const lines = readArray(sale, 'lines').map((line) => [
    readString(line, 'productName', 'Товар'),
    `${readNumber(line, 'quantity', 0)} шт.`,
    formatMoney(readMoney(line, 'unitPrice'), currencyCode),
    formatMoney(readMoney(line, 'lineTotal'), currencyCode)
  ].join(' | '));

  return [
    'AFK4 Касса',
    `Чек: ${receiptNumber}`,
    `Тип: ${receiptType}`,
    `Создан: ${createdAtUtc || '—'}`,
    '',
    ...lines,
    '',
    `Итого: ${formatMoney(readMoney(sale, 'total'), currencyCode)}`
  ].join('\n');
}

function requireBackend(backend: OperatorBackendContext | null): OperatorBackendContext {
  if (backend === null) {
    throw new Error('Сессия оператора недоступна.');
  }

  return backend;
}

function hasPermission(session: OperatorAuthSession | null, permission: string) {
  return session?.permissions.some((candidate) => candidate.toLowerCase() === permission.toLowerCase()) ?? false;
}

function hasAllPermissions(session: OperatorAuthSession | null, permissions: readonly string[]) {
  return permissions.every((permission) => hasPermission(session, permission));
}

function hasAnyPermission(session: OperatorAuthSession | null, permissions: readonly string[]) {
  return permissions.some((permission) => hasPermission(session, permission));
}

function canOpenWorkspace(session: OperatorAuthSession | null, workspaceId: WorkspaceId) {
  return hasAnyPermission(session, workspacePermissionRules[workspaceId]);
}

function firstAllowedWorkspace(session: OperatorAuthSession | null) {
  return workspaceIds.find((workspaceId) => canOpenWorkspace(session, workspaceId)) ?? 'map';
}

function billingModeLabel(mode: SessionBillingModeId) {
  return billingModeOptions.find((option) => option.id === mode)?.label ?? mode;
}

function tariffOptionLabel(tariff: Record<string, unknown>, currencyCode: string) {
  const name = readString(tariff, 'name', 'Тариф');
  const price = readNumber(tariff, 'pricePerMinuteMinorUnits', 0);
  const currency = readString(tariff, 'currencyCode', currencyCode);
  return `${name} · ${formatMinorUnits(price, currency)}/мин`;
}

function playerPackageLabel(playerPackage: Record<string, unknown>) {
  const remainingSeconds = readNumber(playerPackage, 'remainingIncludedSeconds', 0) +
    readNumber(playerPackage, 'remainingBonusSeconds', 0);
  return `${readString(playerPackage, 'name', 'Пакет')} · ${Math.floor(remainingSeconds / 60)} мин`;
}

function packageOptionLabel(packageOption: Record<string, unknown>, currencyCode: string) {
  const name = readString(packageOption, 'name', 'Пакет');
  const price = readNumber(packageOption, 'priceMinorUnits', 0);
  const currency = readString(packageOption, 'currencyCode', currencyCode);
  const totalMinutes = Math.floor((readNumber(packageOption, 'includedSeconds', 0) + readNumber(packageOption, 'bonusSeconds', 0)) / 60);
  return `${name} · ${formatMinorUnits(price, currency)} · ${totalMinutes} мин`;
}

function describeDeviceCommandStatus(status: Record<string, unknown>) {
  const type = readString(status, 'type', 'command');
  const state = readString(status, 'status', 'pending');
  const message = readString(status, 'message');
  const typeLabel = commandTypeLabel(type);
  const stateLabel = commandStatusLabel(state);
  const messageLabel = commandStatusMessageLabel(message);
  return messageLabel ? `${typeLabel}: ${stateLabel} · ${messageLabel}` : `${typeLabel}: ${stateLabel}`;
}

function describeSessionCommandFallback(response: unknown) {
  const commands = readArray<Record<string, unknown>>(response, 'deviceCommands');
  if (commands.length === 0) {
    return 'Платформа подтвердила действие';
  }

  const command = commands[0];
  return `${commandTypeLabel(readString(command, 'type', 'command'))}: отправлена на ПК`;
}

function commandTypeLabel(type: string): string {
  switch (type.toLowerCase()) {
    case 'lock':
      return 'Блокировка';
    case 'unlock':
      return 'Разблокировка';
    case 'transfer':
      return 'Перенос';
    case 'reboot':
      return 'Перезагрузка';
    case 'shutdown':
      return 'Выключение';
    case 'refresh-session-lease':
      return 'Обновление сессии';
    default:
      return 'Команда';
  }
}

function commandStatusLabel(status: string): string {
  switch (status.toLowerCase()) {
    case 'pending':
      return 'ожидает выполнения';
    case 'sent':
    case 'accepted':
    case 'in_progress':
      return 'выполняется';
    case 'completed':
    case 'succeeded':
      return 'выполнена';
    case 'failed':
      return 'не выполнена';
    case 'cancelled':
    case 'canceled':
      return 'отменена';
    default:
      return status;
  }
}

function commandStatusMessageLabel(message: string): string {
  return message
    .replace('Agent accepted lock', 'ПК принял команду блокировки')
    .replace('Agent accepted unlock', 'ПК принял команду разблокировки')
    .replace('Agent accepted transfer', 'ПК принял команду переноса')
    .replace('Agent timeout', 'Агент не ответил')
    .replace('timeout waiting for Agent', 'Агент не ответил вовремя')
    .replace('Queued for Agent', 'Поставлено в очередь агента')
    .replace('Agent did not confirm lock.', 'Агент не подтвердил блокировку.');
}

function dashboardFocusTextLabel(text: string): string {
  return commandStatusMessageLabel(text)
    .replace('lock Failed', 'Блокировка не выполнена')
    .replace('unlock Failed', 'Разблокировка не выполнена')
    .replace('Failed', 'не выполнена')
    .replace('Agent', 'Агент');
}

async function describeSeatActionResult(
  clients: ReturnType<typeof createAuthenticatedOperatorClients>,
  session: OperatorAuthSession,
  seat: SeatSummary,
  response: unknown
) {
  const fallback = describeSessionCommandFallback(response);
  const command = readArray<Record<string, unknown>>(response, 'deviceCommands')[0];
  if (!command || !hasPermission(session, permissionNames.viewDeviceCommandStatus)) {
    return fallback;
  }

  const commandId = readString(command, 'commandId');
  const responseSession = readRecord(response, 'session');
  const deviceId = readString(command, 'deviceId') || seat.deviceId || readString(responseSession, 'deviceId');
  if (!commandId || !deviceId) {
    return fallback;
  }

  try {
    return describeDeviceCommandStatus(await clients.devices.getDeviceCommandStatus(deviceId, commandId));
  } catch (error) {
    return `${fallback} · статус недоступен: ${projectOperatorError(error).detail}`;
  }
}

async function describeDispatchedDeviceCommand(
  clients: ReturnType<typeof createAuthenticatedOperatorClients>,
  session: OperatorAuthSession,
  seat: SeatSummary,
  command: Record<string, unknown>
): Promise<string> {
  const commandId = readString(command, 'commandId');
  const deviceId = seat.deviceId || readString(command, 'deviceId');
  const fallback = `${commandTypeLabel(readString(command, 'type', 'command'))}: отправлена на ПК`;
  if (!commandId || !deviceId || !hasPermission(session, permissionNames.viewDeviceCommandStatus)) {
    return fallback;
  }

  try {
    return describeDeviceCommandStatus(await clients.devices.getDeviceCommandStatus(deviceId, commandId));
  } catch (error) {
    return `${fallback} · статус недоступен: ${projectOperatorError(error).detail}`;
  }
}

function MapWorkspace({
  floorMap,
  canUsePcControl,
  selectedSeatId,
  offlineActionAudit,
  onSelectSeat,
  onPcControlAction
}: {
  floorMap: OperatorFloorMapState;
  canUsePcControl: boolean;
  selectedSeatId: string;
  offlineActionAudit: string[];
  onSelectSeat: (seatId: string) => void;
  onPcControlAction: (seat: SeatSummary, action: PcControlActionId) => Promise<PcControlActionResult>;
}) {
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [activeFilter, setActiveFilter] = useState<MapFilterId>('all');
  const [viewMode, setViewMode] = useState<MapViewMode>('grid');
  const [isPcControlOpen, setIsPcControlOpen] = useState(false);
  const pcControlButtonRef = useRef<HTMLButtonElement | null>(null);
  const pcControlPanelRef = useRef<HTMLElement | null>(null);
  const visibleSeats = useMemo(
    () => floorMap.seats.filter((seat) => matchesMapFilter(seat, activeFilter)),
    [activeFilter, floorMap.seats]
  );
  const selectedSeat = floorMap.seats.find((seat) => seat.id === selectedSeatId) ?? null;
  const offlineBanner = offlineBannerText(floorMap);
  const selectedSeatVisible = visibleSeats.some((seat) => seat.id === selectedSeatId);
  const selectedHasSession = selectedSeat !== null && (Boolean(selectedSeat.activeSessionId) || selectedSeat.hasActiveSession === true);

  const runPcControlAction = async (action: PcControlActionId, label: string) => {
    if (selectedSeat === null) {
      setFeedback({ label, state: 'failed', detail: 'Выберите ПК.' });
      return;
    }

    setFeedback({ label, state: 'pending' });
    try {
      const result = await onPcControlAction(selectedSeat, action);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const explainUnavailablePcControl = (label: string, detail: string) => {
    setFeedback({ label, state: 'failed', detail });
  };

  useEffect(() => {
    if (!isPcControlOpen) {
      return undefined;
    }

    const closeOnOutsidePointer = (event: PointerEvent) => {
      const target = event.target as Node | null;
      if (target !== null && (pcControlPanelRef.current?.contains(target) || pcControlButtonRef.current?.contains(target))) {
        return;
      }

      setIsPcControlOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsPcControlOpen(false);
      }
    };

    document.addEventListener('pointerdown', closeOnOutsidePointer, true);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePointer, true);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [isPcControlOpen]);

  useEffect(() => {
    if (visibleSeats.length === 0 || selectedSeatVisible) {
      return;
    }

    onSelectSeat(visibleSeats[0].id);
  }, [activeFilter, floorMap.seats, onSelectSeat, selectedSeatVisible, visibleSeats]);

  return (
    <main className="floor-workspace">
      <section className="map-toolbar">
        <div>
          <span>Карта</span>
          <h1>{floorMap.branchName}</h1>
        </div>
        <div className="screen-actions">
          <button
            ref={pcControlButtonRef}
            type="button"
            className="map-tool-action"
            aria-expanded={isPcControlOpen}
            disabled={!canUsePcControl || selectedSeat === null}
            onClick={() => setIsPcControlOpen((current) => !current)}
            title={pcControlTitle}
          >
            <Wrench size={14} />{pcControlLabel}
          </button>
        </div>
      </section>

      {isPcControlOpen && selectedSeat !== null && (
        <section ref={pcControlPanelRef} className="pc-control-panel" aria-label="Управление выбранным ПК">
          <header>
            <div>
              <span>Выбранный ПК</span>
              <strong>{selectedSeat.name}</strong>
            </div>
            <b className={`state-chip state-${selectedSeat.tone}`}>{toneLabels[selectedSeat.tone]}</b>
          </header>
          <div className="pc-control-summary">
            <span>{selectedSeat.device}</span>
            <span>{commandLabel(selectedSeat.command)}</span>
          </div>
          <span className="pc-control-section-title">Доступно сейчас</span>
          <div className="pc-control-actions">
            <button type="button" disabled={feedback.state === 'pending'} onClick={() => runPcControlAction('status', 'Статус ПК')}>
              <MonitorCheck size={14} /><span>Статус</span>
            </button>
            <button type="button" disabled={feedback.state === 'pending' || !selectedSeat.deviceId} onClick={() => runPcControlAction('lock', 'Блокировка ПК')}>
              <LockKeyhole size={14} /><span>Блокировать</span>
            </button>
            <button
              type="button"
              disabled={feedback.state === 'pending' || !selectedSeat.deviceId || !selectedHasSession}
              onClick={() => runPcControlAction('unlock', 'Разблокировка ПК')}
              title={selectedHasSession ? 'Повторно отправить unlock для активной сессии' : 'Разблокировка без сессии будет отдельным админ-режимом'}
            >
              <UnlockKeyhole size={14} /><span>Разблокировать</span>
            </button>
          </div>
          <span className="pc-control-section-title">Следующий слой</span>
          <div className="pc-control-actions future">
            <button type="button" onClick={() => explainUnavailablePcControl('Перезагрузка ПК', 'Нужен Agent-контракт reboot и подтверждение выполнения от ПК.')}>
              <TimerReset size={14} /><span><strong>Перезагрузить</strong><em>нужен Agent reboot</em></span>
            </button>
            <button type="button" onClick={() => explainUnavailablePcControl('Выключение ПК', 'Нужен Agent-контракт shutdown и правило запрета при активной сессии.')}>
              <Power size={14} /><span><strong>Выключить</strong><em>нужен Agent shutdown</em></span>
            </button>
            <button type="button" onClick={() => explainUnavailablePcControl('Разбудить ПК', 'Нужен Wake-on-LAN relay через онлайн Agent в этой клубной сети.')}>
              <Wifi size={14} /><span><strong>Разбудить</strong><em>нужен WoL relay</em></span>
            </button>
            <button type="button" onClick={() => explainUnavailablePcControl('Админ-режим', 'Нужен сервисный режим с таймером, audit и автоматическим возвратом защиты.')}>
              <ShieldAlert size={14} /><span><strong>Админ-режим</strong><em>нужен сервисный контракт</em></span>
            </button>
          </div>
        </section>
      )}

      <section className="map-controls-row" aria-label="Фильтры и вид карты">
        <div className="filter-row map-filter-row" aria-label="Фильтр ПК">
          {mapFilterOptions.map((option) => (
            <button
              key={option.id}
              type="button"
              className={activeFilter === option.id ? 'active' : undefined}
              onClick={() => setActiveFilter(option.id)}
            >
              {option.label}
              <strong>{countByMapFilter(floorMap.seats, option.id)}</strong>
            </button>
          ))}
        </div>
        <div className="filter-row map-view-switch" aria-label="Вид карты">
          <button type="button" className={viewMode === 'grid' ? 'active' : undefined} onClick={() => setViewMode('grid')}>Карта</button>
          <button type="button" className={viewMode === 'table' ? 'active' : undefined} onClick={() => setViewMode('table')}>Таблица</button>
        </div>
      </section>
      {floorMap.loadStatus === 'failed' && (
        <FeedbackNotice feedback={{ label: 'Карта', state: 'failed', detail: floorMap.error ?? 'Не удалось загрузить карту.' }} />
      )}
      {offlineBanner !== null && (
        <FeedbackNotice feedback={{ label: 'Офлайн', state: 'pending', detail: offlineBanner }} />
      )}
      {offlineActionAudit.map((note, index) => (
        <FeedbackNotice key={`offline-audit-${index}`} feedback={{ label: 'Очередь', state: 'failed', detail: note }} />
      ))}
      <FeedbackNotice feedback={feedback} />

      <section className={`map-board ${viewMode === 'table' ? 'table-mode' : ''}`} aria-label="ПК зала">
        {visibleSeats.length === 0 ? (
          <div className="map-empty-state">
            <strong>Нет ПК в выбранном фильтре</strong>
            <span>Смените фильтр или проверьте карту платформы.</span>
          </div>
        ) : viewMode === 'grid' ? (
          <div className="seat-grid">
            {visibleSeats.map((seat) => (
              <SeatTile
                key={seat.id}
                seat={seat}
                selected={seat.id === selectedSeatId}
                onSelect={() => onSelectSeat(seat.id)}
              />
            ))}
          </div>
        ) : (
          <div className="seat-table-wrap">
            <table className="seat-table" aria-label="Таблица ПК">
              <thead>
                <tr>
                  <th>ПК</th>
                  <th>Состояние</th>
                  <th>Игрок</th>
                  <th>Остаток</th>
                  <th>Устройство</th>
                  <th>Команда</th>
                  <th>Биллинг</th>
                </tr>
              </thead>
              <tbody>
                {visibleSeats.map((seat) => (
                  <tr key={seat.id} className={`state-${seat.tone}${seat.id === selectedSeatId ? ' selected' : ''}`}>
                    <td>
                      <button type="button" onClick={() => onSelectSeat(seat.id)}>{seat.name}</button>
                      <span>{zoneLabel(seat.zone)}</span>
                    </td>
                    <td><strong>{toneLabels[seat.tone]}</strong><span>{seat.stateLabel}</span></td>
                    <td>{seat.player}</td>
                    <td>{seat.remaining}</td>
                    <td>{deviceStatusLabel(seat.device)}</td>
                    <td>{commandLabel(seat.command)}</td>
                    <td>{billingLabel(seat.billing)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}

function DashboardWorkspace({
  currencyCode,
  backend,
  onNavigate,
  onOpenSeat
}: {
  currencyCode: string;
  backend: OperatorBackendContext | null;
  onNavigate: (workspace: WorkspaceId) => void;
  onOpenSeat: (seatId: string) => void;
}) {
  const today = new Date();
  const todayInput = toDateInputValue(today);
  const weekStartInput = toDateInputValue(addDays(today, -6));
  const monthStartInput = toDateInputValue(addDays(today, -29));
  const [period, setPeriod] = useState<DashboardPeriod>('today');
  const [customRange, setCustomRange] = useState({ from: weekStartInput, to: todayInput });
  const [selectedFocusIndex, setSelectedFocusIndex] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [dashboardSummary, setDashboardSummary] = useState<OperatorDashboardSummaryDto | null>(null);
  const [dashboardLoadStatus, setDashboardLoadStatus] = useState<LoadStatus>('loading');
  const [dashboardLoadError, setDashboardLoadError] = useState<string | null>(null);

  const presetRanges = {
    today: { from: todayInput, to: todayInput, label: 'сегодня', metricLabel: 'сегодня' },
    week: { from: weekStartInput, to: todayInput, label: 'за неделю', metricLabel: 'неделю' },
    month: { from: monthStartInput, to: todayInput, label: 'за месяц', metricLabel: 'месяц' }
  };

  const activeRange = period === 'custom'
    ? { ...customRange, label: 'за выбранный период', metricLabel: 'выбранный период' }
    : presetRanges[period];
  const activeDays = countPeriodDays(activeRange.from, activeRange.to);
  const activePeriodLabel = period === 'custom' ? `${activeDays} дн.` : activeRange.metricLabel;
  const periodDaysShort = `${activeDays} дн.`;
  const exportLabel = `${activeRange.from} - ${activeRange.to}`;
  const updateCustomRange = (field: 'from' | 'to', value: string) => {
    setCustomRange((range) => ({ ...range, [field]: value }));
    setPeriod('custom');
  };

  useEffect(() => {
    let disposed = false;

    if (backend === null) {
      setDashboardSummary(null);
      setDashboardLoadStatus('failed');
      setDashboardLoadError('Активный филиал не назначен.');
      return undefined;
    }

    setDashboardLoadStatus('loading');
    setDashboardLoadError(null);

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.dashboard.getSummary(backend.branchId, dashboardRangeQuery(activeRange.from, activeRange.to))
      .then((summary) => {
        if (disposed) {
          return;
        }

        setDashboardSummary(summary);
        setDashboardLoadStatus('backend');
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setDashboardSummary(null);
        setDashboardLoadStatus('failed');
        setDashboardLoadError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, activeRange.from, activeRange.to]);

  const summary = dashboardSummary ?? emptyDashboardSummary(currencyCode, activeRange.from, activeRange.to);
  const revenue = readRecord(summary, 'revenue');
  const utilization = readRecord(summary, 'utilization');
  const alertPressure = readRecord(summary, 'alertPressure');
  const reservations = readRecord(summary, 'reservations');
  const shift = readRecord(summary, 'shift');
  const totalRevenue = readMoney(revenue, 'totalRevenue') ?? { currencyCode, minorUnits: 0 };
  const expectedCash = readMoney(shift, 'expectedCash') ?? { currencyCode, minorUnits: 0 };
  const cashTargetMinorUnits = Math.max(expectedCash.minorUnits, totalRevenue.minorUnits);
  const cashPercent = cashTargetMinorUnits > 0
    ? Math.min(100, Math.round((totalRevenue.minorUnits / cashTargetMinorUnits) * 100))
    : 0;
  const attentionCount = readNumber(alertPressure, 'totalAlerts', 0);
  const bookingUsed = readNumber(reservations, 'activeReservations', 0);
  const bookingSlots = readNumber(reservations, 'availableSlots', 0);
  const posChecks = readNumber(revenue, 'posCheckCount', 0);
  const newClients = readNumber(revenue, 'newPlayerCount', 0);
  const activePcs = readNumber(utilization, 'activeSessions', 0);
  const totalPcs = Math.max(1, readNumber(utilization, 'totalSeats', 0));
  const focusQueue = readArray<Record<string, unknown>>(summary, 'focusQueue');
  const dashboardStatusText = dashboardLoadStatus === 'backend'
    ? 'Данные платформы'
    : dashboardLoadStatus === 'loading'
      ? 'Загрузка данных'
      : 'Ошибка данных';
  const focusItems = focusQueue.length > 0
    ? focusQueue.map((item) => [
      readString(item, 'tone', 'warning'),
      readString(item, 'target', '-'),
      dashboardFocusTextLabel(readString(item, 'title', 'Сигнал платформы')),
      dashboardFocusTextLabel(readString(item, 'detail', 'Проверьте состояние в рабочей карте.')),
      readString(item, 'seatId')
    ] as const)
    : [[
      'ready',
      '-',
      dashboardLoadStatus === 'failed' ? 'Данные не загружены' : 'Нет срочных сигналов',
      dashboardLoadStatus === 'failed' ? dashboardLoadError ?? 'Повторите загрузку обзора.' : 'Срочных задач за выбранный период нет.',
      ''
    ] as const];
  const selectedFocus = focusItems[selectedFocusIndex] ?? focusItems[0];

  const openSelectedFocusSeat = (label: string) => {
    if (selectedFocus[4]) {
      onOpenSeat(selectedFocus[4]);
      return;
    }

    setFeedback({
      label,
      state: 'failed',
      detail: selectedFocus[3]
    });
  };

  const exportDashboard = async () => {
    setFeedback({ label: 'Экспорт', state: 'pending' });

    try {
      const nextBackend = requireBackend(backend);
      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [, salesCsv] = await Promise.all([
        clients.dashboard.getSummary(nextBackend.branchId, dashboardRangeQuery(activeRange.from, activeRange.to)),
        clients.shifts.exportSalesReportCsv(nextBackend.branchId, dashboardRangeQuery(activeRange.from, activeRange.to))
      ]);
      const exportStamp = new Date().toISOString().replace(/[:.]/g, '-');
      downloadTextFile(`afk4-overview-sales-${exportStamp}.csv`, salesCsv, 'text/csv;charset=utf-8');
      setFeedback({ label: 'Экспорт', state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: 'Экспорт', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const pulseItems = [
    { label: 'Касса', value: formatMinorUnits(totalRevenue.minorUnits, totalRevenue.currencyCode), detail: `из ${formatMinorUnits(cashTargetMinorUnits, totalRevenue.currencyCode)}`, chartValue: cashPercent, chartLabel: <><AnimatedNumber value={cashPercent} />%</>, chartSubLabel: formatCompactNumber(Math.round(minorToMajor(totalRevenue.minorUnits))), tone: 'cash', icon: Banknote },
    { label: 'Активные ПК', value: `${activePcs} / ${totalPcs}`, detail: `за ${activePeriodLabel}`, chartValue: Math.round((activePcs / totalPcs) * 100), chartLabel: <><AnimatedNumber value={activePcs} />/{totalPcs}</>, chartSubLabel: 'сейчас', tone: 'devices', icon: MonitorCheck },
    { label: 'Внимание', value: String(attentionCount), detail: `${pluralRu(attentionCount, ['сигнал', 'сигнала', 'сигналов'])} за ${activePeriodLabel}`, chartValue: Math.min(100, Math.round((attentionCount / Math.max(1, totalPcs * activeDays)) * 100)), chartLabel: <AnimatedNumber value={attentionCount} />, chartSubLabel: 'сигн.', tone: 'attention', icon: ShieldAlert },
    { label: 'Брони', value: `${bookingUsed} / ${bookingSlots}`, detail: `слоты за ${activePeriodLabel}`, chartValue: bookingSlots > 0 ? Math.min(100, Math.round((bookingUsed / bookingSlots) * 100)) : 0, chartLabel: <><AnimatedNumber value={bookingUsed} />/{bookingSlots}</>, chartSubLabel: 'слоты', tone: 'booking', icon: CalendarClock }
  ];

  const controlCards: Array<[WorkspaceId, string, string, string, LucideIcon]> = [
    ['map', 'Карта', `${totalPcs} ПК`, `${attentionCount} ${pluralRu(attentionCount, ['сигнал', 'сигнала', 'сигналов'])}`, MonitorCheck],
    ['pos', 'Продажи', `${posChecks} ${pluralRu(posChecks, ['чек', 'чека', 'чеков'])}`, `за ${activePeriodLabel}`, ReceiptText],
    ['payments', 'Касса', formatMinorUnits(totalRevenue.minorUnits, totalRevenue.currencyCode), `за ${activePeriodLabel}`, CircleDollarSign],
    ['players', 'Клиент', `${newClients} ${pluralRu(newClients, ['новый', 'новых', 'новых'])}`, `за ${activePeriodLabel}`, UserRoundPlus]
  ];

  return (
    <main className="workspace-screen dashboard-screen">
      <section className="screen-head dashboard-head">
        <div>
          <span>Обзор</span>
          <h1>Что требует внимания · {activeRange.label}</h1>
        </div>
        <div className="filter-row dashboard-period-filter" aria-label="Период обзора">
          <div className="period-segment">
            <button type="button" className={period === 'today' ? 'active' : undefined} onClick={() => setPeriod('today')}>Сегодня</button>
            <button type="button" className={period === 'week' ? 'active' : undefined} onClick={() => setPeriod('week')}>Неделя</button>
            <button type="button" className={period === 'month' ? 'active' : undefined} onClick={() => setPeriod('month')}>Месяц</button>
          </div>
          <div className={`date-range-control ${period === 'custom' ? 'active' : ''}`}>
            <label>
              <span>с</span>
              <input
                type="date"
                aria-label="Начало периода"
                value={customRange.from}
                onChange={(event) => updateCustomRange('from', event.currentTarget.value)}
                onInput={(event) => updateCustomRange('from', event.currentTarget.value)}
                onFocus={() => setPeriod('custom')}
              />
            </label>
            <label>
              <span>по</span>
              <input
                type="date"
                aria-label="Конец периода"
                value={customRange.to}
                onChange={(event) => updateCustomRange('to', event.currentTarget.value)}
                onInput={(event) => updateCustomRange('to', event.currentTarget.value)}
                onFocus={() => setPeriod('custom')}
              />
            </label>
            <span className="date-range-days" aria-label={`Длина периода: ${periodDaysShort}`}>{periodDaysShort}</span>
          </div>
          <span className={`map-load-state ${dashboardLoadStatus === 'backend' ? 'ready' : dashboardLoadStatus}`}>{dashboardStatusText}</span>
          <button type="button" className="export-button" aria-label={`Скачать продажи за ${exportLabel}`} onClick={exportDashboard}>
            Экспорт
          </button>
        </div>
      </section>

      <section className="dashboard-layout">
        <article className="dashboard-now-panel">
          <header className="dashboard-panel-title">
            <span>Главный фокус</span>
            <strong>{selectedFocus[2]}</strong>
          </header>
          <p>{selectedFocus[3]}</p>
          <div className="dashboard-now-meta">
            <span><AlertTriangle size={15} /> {selectedFocus[0]}</span>
            <span>{selectedFocus[1]}</span>
            <span>{dashboardStatusText}</span>
          </div>
          <div className="dashboard-now-actions">
            <button type="button" onClick={() => openSelectedFocusSeat('Разобрать')}><AlertTriangle size={15} /> Разобрать</button>
            <button type="button" onClick={() => openSelectedFocusSeat(pcControlLabel)}><Wrench size={15} /> {pcControlLabel}</button>
          </div>
          {dashboardLoadStatus === 'failed' && <FeedbackNotice feedback={{ label: 'Обзор', state: 'failed', detail: dashboardLoadError ?? 'Данные обзора недоступны.' }} />}
          <FeedbackNotice feedback={feedback} />
        </article>

        <section className="dashboard-secondary-panel">
          <header className="dashboard-panel-title">
            <span>Дальше по очереди</span>
            <strong>разобрать после критичного</strong>
          </header>
          <div className="focus-list">
            {focusItems.map(([tone, target, title, detail], index) => (
              <button
                key={`${target}-${title}`}
                type="button"
                className={`focus-row ${tone}${index === selectedFocusIndex ? ' active' : ''}`}
                onClick={() => setSelectedFocusIndex(index)}
              >
                <div>
                  <span>{target}</span>
                  <strong>{title}</strong>
                  <em>{detail}</em>
                </div>
              </button>
            ))}
          </div>
          <div className="dashboard-selected-signal">
            <span>{selectedFocus[1]}</span>
            <strong>{selectedFocus[3]}</strong>
          </div>
        </section>

        <section className="dashboard-control-panel">
          <header className="dashboard-panel-title">
            <span>Управление</span>
            <strong>карта, чек, депозит, клиент</strong>
          </header>
          <div className="dashboard-control-grid">
            {controlCards.map(([targetWorkspace, label, value, detail, Icon]) => (
              <DashboardControlCard
                key={label}
                label={label}
                value={value}
                detail={detail}
                icon={Icon}
                onActivate={() => onNavigate(targetWorkspace)}
              />
            ))}
          </div>
        </section>

        <section className="dashboard-pulse-panel">
          <header className="dashboard-panel-title">
            <span>Пульс смены</span>
            <strong>касса, зал, сигналы, брони</strong>
          </header>
          <div className="dashboard-pulse-grid">
            {pulseItems.map((item) => (
              <DashboardPulseCard key={item.label} {...item} />
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}

function DashboardControlCard({
  label,
  value,
  detail,
  icon: Icon,
  onActivate
}: {
  label: string;
  value: string;
  detail: string;
  icon: LucideIcon;
  onActivate: () => void;
}) {
  return (
    <button type="button" className="dashboard-control-card" onClick={onActivate}>
      <span>
        <Icon size={16} />
        {label}
      </span>
      <strong>{value}</strong>
      <em>{detail}</em>
    </button>
  );
}

function DashboardPulseCard({
  label,
  value,
  detail,
  chartValue,
  chartLabel,
  chartSubLabel,
  tone,
  icon: Icon
}: {
  label: string;
  value: string;
  detail: string;
  chartValue: number;
  chartLabel: ReactNode;
  chartSubLabel: string;
  tone: string;
  icon: LucideIcon;
}) {
  return (
    <article className={`dashboard-pulse-card ${tone}`}>
      <header className="pulse-card-title">
        <Icon size={15} />
        <span>{label}</span>
      </header>
      <div
        className="donut-chart"
        style={{ '--chart-value': `${chartValue}%` } as CSSProperties}
        aria-hidden="true"
      >
        <strong>{chartLabel}</strong>
        <em>{chartSubLabel}</em>
      </div>
    </article>
  );
}

function BookingWorkspace() {
  const [selectedBookingIndex, setSelectedBookingIndex] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const bookings = [
    { time: '15:40', client: 'Aziz P.', seats: '2 ПК', zone: 'Зал C', duration: '90 мин', status: 'Подтверждена', tone: 'confirmed', note: 'прийти за 10 мин до старта' },
    { time: '16:00', client: 'Гость +998', seats: '1 ПК', zone: 'VIP', duration: '60 мин', status: 'Онлайн-заявка', tone: 'online', note: 'нужен звонок для подтверждения' },
    { time: '16:30', client: 'Команда CS2', seats: '5 ПК', zone: 'Буткемп', duration: '120 мин', status: 'Строгая', tone: 'strict', note: 'держать места вместе' },
    { time: '17:10', client: 'Madina S.', seats: '1 ПК', zone: 'Зал B', duration: '45 мин', status: 'Ожидает', tone: 'pending', note: 'нет депозита, уточнить оплату' }
  ];

  const requests = [
    ['15:55', 'Telegram · +992 90 555 11 22', '2 ПК · рядом · 90 мин'],
    ['16:20', 'Сайт · guest-1842', '1 VIP · 60 мин']
  ];
  const selectedBooking = bookings[selectedBookingIndex];

  return (
    <main className="workspace-screen booking-screen">
      <section className="screen-head booking-head">
        <div>
          <span>Брони</span>
          <h1>Брони сегодня · посадка гостей и онлайн-заявки</h1>
        </div>
        <div className="screen-actions">
          <button type="button" className="booking-create-action" onClick={() => triggerFeedback(setFeedback, 'Новая бронь')}><Plus size={14} />Создать</button>
        </div>
      </section>

      <section className="state-strip booking-state-strip">
        <StateFlag label="Ближайшая" value="15:40" />
        <StateFlag label="Активные" value="5" />
        <StateFlag label="Онлайн" value="2" critical />
        <StateFlag label="Конфликты" value="1" critical />
        <StateFlag label="Слоты" value="8" />
      </section>

      <section className="booking-layout">
        <section className="booking-panel booking-timeline-panel">
          <header className="booking-panel-title">
            <span>Лента броней</span>
            <strong>ближайшие посадки сегодня</strong>
          </header>
          <div className="booking-list">
            {bookings.map((booking) => (
              <button
                key={`${booking.time}-${booking.client}`}
                type="button"
                className={`booking-card ${booking.tone}${booking === selectedBooking ? ' active' : ''}`}
                onClick={() => setSelectedBookingIndex(bookings.indexOf(booking))}
              >
                <span className="booking-time">{booking.time}</span>
                <span className="booking-client">
                  <strong>{booking.client}</strong>
                  <em>{booking.note}</em>
                </span>
                <span className="booking-meta">{booking.seats} · {booking.zone} · {booking.duration}</span>
                <b>{booking.status}</b>
              </button>
            ))}
          </div>
        </section>

        <section className="booking-panel booking-selected-panel">
          <header className="booking-panel-title">
            <span>Выбранная бронь</span>
            <strong>{selectedBooking.client} · {selectedBooking.time}</strong>
          </header>
          <div className={`booking-status-card ${selectedBooking.tone}`}>
            <span>Готовить посадку</span>
            <strong>{selectedBooking.time}</strong>
            <em>{selectedBooking.seats} · {selectedBooking.zone} · {selectedBooking.duration}</em>
          </div>
          <div className="booking-action-grid" aria-label="Действия с бронью">
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Открыть карту')}><MonitorCheck size={15} />Открыть карту</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Посадить бронь')}><UserRoundPlus size={15} />Посадить</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Перенести бронь')}><ArrowRightLeft size={15} />Перенести</button>
            <button type="button" className="danger" onClick={() => triggerFeedback(setFeedback, 'Отменить бронь')}><Square size={15} />Отменить</button>
          </div>
          <FeedbackNotice feedback={feedback} />
          <div className="booking-detail-list">
            <div><span>Клиент</span><strong>{selectedBooking.client}</strong></div>
            <div><span>Комментарий</span><strong>{selectedBooking.note}</strong></div>
            <div><span>Подтверждение</span><strong>звонок не нужен</strong></div>
          </div>
        </section>

        <section className="booking-panel booking-requests-panel">
          <header className="booking-panel-title">
            <span>Онлайн-заявки</span>
            <strong>требуют ответа оператора</strong>
          </header>
          <div className="booking-request-list">
            {requests.map(([time, source, detail]) => (
              <article key={`${time}-${source}`} className="booking-request-card">
                <span>{time}</span>
                <strong>{source}</strong>
                <em>{detail}</em>
                <div>
                  <button type="button" onClick={() => triggerFeedback(setFeedback, `Принять ${time}`)}>Принять</button>
                  <button type="button" onClick={() => triggerFeedback(setFeedback, `Уточнить ${time}`)}>Уточнить</button>
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="booking-panel booking-create-panel">
          <header className="booking-panel-title">
            <span>Новая бронь</span>
            <strong>быстрый черновик</strong>
          </header>
          <div className="booking-form-grid">
            <label>Клиент<input value="телефон или имя" readOnly /></label>
            <label>Старт<input value="Сегодня · 16:00" readOnly /></label>
            <label>Длительность<input value="60 мин" readOnly /></label>
            <label>ПК<input value="2 · рядом" readOnly /></label>
          </div>
          <button type="button" className="booking-primary-action" onClick={() => triggerFeedback(setFeedback, 'Создать бронь')}>Создать бронь</button>
        </section>
      </section>
    </main>
  );
}

function PosWorkspace({ currencyCode }: { currencyCode: string }) {
  const [activeCategory, setActiveCategory] = useState('Популярное');
  const [productSearch, setProductSearch] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('Наличные');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const products = [
    { name: 'Cola 0.5', price: 12, group: 'напитки', note: 'холодильник', category: 'Напитки' },
    { name: 'Вода 0.5', price: 6, group: 'напитки', note: 'рядом с кассой', category: 'Напитки' },
    { name: 'Хот-дог', price: 28, group: 'кухня', note: '7 мин', category: 'Еда' },
    { name: 'Бургер', price: 42, group: 'кухня', note: '12 мин', category: 'Еда' },
    { name: 'Гостевой час', price: 25, group: 'игровое время', note: 'без клиента', category: 'Услуги' },
    { name: 'VIP час', price: 45, group: 'игровое время', note: 'VIP зона', category: 'Услуги' },
    { name: 'Аренда гарнитуры', price: 10, group: 'услуги', note: 'залог не нужен', category: 'Услуги' },
    { name: 'Gamer combo', price: 55, group: 'комбо', note: 'напиток + еда', category: 'Популярное' }
  ];
  const [cartItems, setCartItems] = useState([
    { name: 'Cola 0.5', quantity: 2, price: 12 },
    { name: 'Гостевой час', quantity: 1, price: 25 },
    { name: 'Аренда гарнитуры', quantity: 1, price: 10 }
  ]);
  const receipts = [
    ['15:08', 'PC-06 · Madina S.', `86 ${currencyCode}`, 'карта'],
    ['14:55', 'PC-04 · возврат', `-20 ${currencyCode}`, 'наличные'],
    ['14:42', 'Гость · стойка', `59 ${currencyCode}`, 'наличные']
  ];
  const quickOps: Array<[string, string, LucideIcon]> = [
    ['Пополнить депозит', 'клиент или телефон', CircleDollarSign],
    ['Возврат по чеку', 'поиск последней продажи', ReceiptText],
    ['Новый клиент', 'быстрая регистрация', UserRoundPlus],
    ['Внести наличные', 'кассовое движение', Banknote]
  ];
  const visibleProducts = products.filter((product) => {
    const categoryMatches = activeCategory === 'Популярное' || product.category === activeCategory || product.category === 'Популярное';
    const searchMatches = `${product.name} ${product.group} ${product.note}`.toLowerCase().includes(productSearch.trim().toLowerCase());
    return categoryMatches && searchMatches;
  });
  const cartTotal = cartItems.reduce((sum, item) => sum + item.price * item.quantity, 0);
  const acceptedCash = paymentMethod === 'Наличные' ? Math.ceil(cartTotal / 10) * 10 : cartTotal;
  const change = acceptedCash - cartTotal;
  const addProduct = (product: (typeof products)[number]) => {
    setCartItems((items) => {
      const existing = items.find((item) => item.name === product.name);

      if (existing) {
        return items.map((item) => item.name === product.name ? { ...item, quantity: item.quantity + 1 } : item);
      }

      return [...items, { name: product.name, quantity: 1, price: product.price }];
    });
    triggerFeedback(setFeedback, `${product.name} добавлен`, 'confirmed');
  };

  return (
    <main className="workspace-screen pos-screen">
      <section className="screen-head pos-head">
        <div>
          <span>Продажи</span>
          <h1>Продажи · чек и кассовые операции</h1>
        </div>
      </section>

      <section className="state-strip pos-state-strip" aria-label="Сводка продаж">
        <StateFlag label="Продажи" value={`2 чека · 145 ${currencyCode}`} />
        <StateFlag label="Возвраты" value={`1 · 20 ${currencyCode}`} critical />
        <StateFlag label="Наличные" value={`3 740 ${currencyCode}`} />
        <StateFlag label="Склад" value="2 позиции низко" critical />
        <StateFlag label="Смена" value="5ч 54м" />
      </section>

      <section className="pos-layout">
        <section className="pos-panel pos-catalog-panel">
          <header className="pos-panel-title">
            <span>Каталог</span>
            <strong>быстрый поиск товара или услуги</strong>
          </header>
          <label className="pos-search">
            <Search size={14} />
            <input
              placeholder="Товар, услуга, клиент, чек"
              value={productSearch}
              onChange={(event) => setProductSearch(event.currentTarget.value)}
            />
          </label>
          <div className="pos-category-row" aria-label="Категории товаров">
            {['Популярное', 'Еда', 'Напитки', 'Услуги'].map((category) => (
              <button
                key={category}
                type="button"
                className={activeCategory === category ? 'active' : undefined}
                onClick={() => setActiveCategory(category)}
              >
                {category}
              </button>
            ))}
          </div>
          <div className="pos-catalog-grid">
            {visibleProducts.map((product) => (
              <button key={product.name} type="button" className="pos-product-card" onClick={() => addProduct(product)}>
                <strong>{product.name}</strong>
                <span>{product.group}</span>
                <b>{product.price} {currencyCode}</b>
                <em>{product.note}</em>
              </button>
            ))}
          </div>
        </section>

        <section className="pos-panel pos-cart-panel">
          <header className="pos-panel-title">
            <span>Корзина</span>
            <strong>текущая продажа</strong>
          </header>
          <div className="pos-cart-client">
            <UserRoundPlus size={17} />
            <div>
              <span>Клиент</span>
              <strong>Гость · без карты</strong>
            </div>
            <button type="button">Выбрать</button>
          </div>
          <div className="pos-cart-list">
            {cartItems.map((item) => (
              <article key={item.name} className="pos-cart-row interactive-row">
                <div>
                  <strong>{item.name}</strong>
                  <span>{item.quantity} шт.</span>
                </div>
                <b>{item.price * item.quantity} {currencyCode}</b>
              </article>
            ))}
          </div>
          <div className="pos-total-card">
            <span>Итого к оплате</span>
            <strong><AnimatedNumber value={cartTotal} /> {currencyCode}</strong>
            <em>скидок нет · чек будет создан после подтверждения платформы</em>
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="pos-panel pos-payment-panel">
          <header className="pos-panel-title">
            <span>Оплата</span>
            <strong>способ и подтверждение</strong>
          </header>
          <div className="pos-payment-methods">
            {['Наличные', 'Карта', 'Депозит'].map((method) => (
              <button
                key={method}
                type="button"
                className={paymentMethod === method ? 'active' : undefined}
                onClick={() => setPaymentMethod(method)}
              >
                {method === 'Наличные' && <Banknote size={15} />}
                {method === 'Карта' && <CircleDollarSign size={15} />}
                {method === 'Депозит' && <ReceiptText size={15} />}
                {method}
              </button>
            ))}
          </div>
          <div className="pos-payment-summary">
            <div><span>Принято</span><strong><AnimatedNumber value={acceptedCash} /> {currencyCode}</strong></div>
            <div><span>Сдача</span><strong><AnimatedNumber value={change} /> {currencyCode}</strong></div>
            <div><span>Смена</span><strong>Открыта</strong></div>
          </div>
          <button type="button" className="pos-primary-action" onClick={() => triggerFeedback(setFeedback, 'Оплата')}>Принять оплату</button>
          <button type="button" className="pos-secondary-action" onClick={() => triggerFeedback(setFeedback, 'Чек отложен')}>Отложить чек</button>
        </section>

        <section className="pos-panel pos-receipts-panel">
          <header className="pos-panel-title">
            <span>Последние чеки</span>
            <strong>быстрый доступ к возврату и повтору</strong>
          </header>
          <div className="pos-receipt-list">
            {receipts.map(([time, source, total, method]) => (
              <article key={`${time}-${source}`} className="pos-receipt-row">
                <span>{time}</span>
                <strong>{source}</strong>
                <em>{method}</em>
                <b>{total}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="pos-panel pos-quick-panel">
          <header className="pos-panel-title">
            <span>Быстрые операции</span>
            <strong>касса без лишних переходов</strong>
          </header>
          <div className="pos-quick-grid">
            {quickOps.map(([label, detail, Icon]) => (
              <button key={label} type="button" className="pos-quick-card" onClick={() => triggerFeedback(setFeedback, label)}>
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}

function PlayersWorkspace({ currencyCode }: { currencyCode: string }) {
  const [clientSearch, setClientSearch] = useState('');
  const [activeSegment, setActiveSegment] = useState('Все');
  const [selectedClientName, setSelectedClientName] = useState('Madina S.');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const clients = [
    { name: 'Madina S.', status: 'VIP', balance: `460 ${currencyCode}`, debt: `0 ${currencyCode}`, last: 'вчера · Dota 2', tone: 'vip', detail: '+10% скидка · 42 визита' },
    { name: 'Amir K.', status: 'Активен', balance: `120 ${currencyCode}`, debt: `0 ${currencyCode}`, last: 'сейчас · PC-01', tone: 'active', detail: 'пакет до 18:40 · 18 визитов' },
    { name: 'Olim K.', status: 'Долг', balance: `0 ${currencyCode}`, debt: `35 ${currencyCode}`, last: 'сейчас · PC-04', tone: 'debt', detail: 'постоплата близко к лимиту' },
    { name: 'Yusuf A.', status: 'Обычный', balance: `0 ${currencyCode}`, debt: `0 ${currencyCode}`, last: '18 мая · CS2', tone: 'regular', detail: 'нет активного пакета' },
    { name: 'Aziz P.', status: 'Бронь', balance: `80 ${currencyCode}`, debt: `0 ${currencyCode}`, last: 'бронь 15:40', tone: 'booking', detail: '2 ПК · Зал C' }
  ];
  const visibleClients = clients.filter((client) => {
    const segmentMatches = activeSegment === 'Все'
      || (activeSegment === 'VIP' && client.status === 'VIP')
      || (activeSegment === 'Есть долг' && parseMoney(client.debt) > 0)
      || (activeSegment === 'Новые' && client.name === 'Yusuf A.')
      || (activeSegment === 'Спящие' && client.name === 'Yusuf A.');
    const searchMatches = `${client.name} ${client.status} ${client.detail} ${client.last}`
      .toLowerCase()
      .includes(clientSearch.trim().toLowerCase());
    return segmentMatches && searchMatches;
  });
  const selectedClient = clients.find((client) => client.name === selectedClientName) ?? visibleClients[0] ?? clients[0];
  const history = [
    ['Вчера 22:10', 'Пополнение депозита', `200 ${currencyCode}`],
    ['Вчера 20:42', 'VIP час · PC-06', `45 ${currencyCode}`],
    ['15 мая', 'Возврат по чеку', `-20 ${currencyCode}`]
  ];
  const quickOps: Array<[string, string, LucideIcon]> = [
    ['Пополнить депозит', 'наличные или карта', CircleDollarSign],
    ['Списать долг', 'после оплаты', ReceiptText],
    ['Создать бронь', 'сразу из карточки', CalendarClock],
    ['Новая карта', 'быстрая регистрация', UserRoundPlus]
  ];

  return (
    <main className="workspace-screen clients-screen">
      <section className="screen-head clients-head">
        <div>
          <span>Клиенты</span>
          <h1>Клиенты · поиск, депозит и долги</h1>
        </div>
      </section>

      <section className="state-strip clients-state-strip" aria-label="Сводка клиентов">
        <StateFlag label="Клиенты" value="12 480" />
        <StateFlag label="Онлайн" value="9" />
        <StateFlag label="Депозиты" value={`84 210 ${currencyCode}`} />
        <StateFlag label="Долги" value={`1 240 ${currencyCode}`} critical />
        <StateFlag label="VIP" value="314" />
      </section>

      <section className="clients-layout">
        <section className="clients-panel clients-list-panel">
          <header className="clients-panel-title">
            <span>Список клиентов</span>
            <strong>поиск по имени, телефону или карте</strong>
          </header>
          <label className="clients-search">
            <Search size={14} />
            <input
              placeholder="Игрок, телефон, карта"
              value={clientSearch}
              onChange={(event) => setClientSearch(event.currentTarget.value)}
            />
          </label>
          <div className="clients-list">
            {visibleClients.map((client) => (
              <button
                key={client.name}
                type="button"
                className={`client-row ${client.tone}${client.name === selectedClient.name ? ' selected' : ''}`}
                onClick={() => setSelectedClientName(client.name)}
              >
                <span>{client.status}</span>
                <div>
                  <strong>{client.name}</strong>
                  <em>{client.detail}</em>
                </div>
                <b>{client.balance}</b>
                <small>{client.last}</small>
              </button>
            ))}
          </div>
        </section>

        <section className="clients-panel clients-profile-panel">
          <header className="clients-panel-title">
            <span>Карточка клиента</span>
            <strong>выбранный игрок</strong>
          </header>
          <div className="client-profile-card">
            <div className="client-avatar">MS</div>
            <div>
              <span>{selectedClient.status}</span>
              <strong>{selectedClient.name}</strong>
              <em>+992 90 555 22 11 · карта 0482</em>
            </div>
          </div>
          <div className="client-metrics-grid">
            <div><span>Депозит</span><strong>{selectedClient.balance}</strong></div>
            <div><span>Долг</span><strong>{selectedClient.debt}</strong></div>
            <div><span>Скидка</span><strong>10%</strong></div>
            <div><span>Визиты</span><strong>42</strong></div>
          </div>
        </section>

        <section className="clients-panel clients-actions-panel">
          <header className="clients-panel-title">
            <span>Операции</span>
            <strong>деньги и быстрые действия</strong>
          </header>
          <div className="clients-action-grid">
            {quickOps.map(([label, detail, Icon]) => (
              <button key={label} type="button" className="clients-action-card" onClick={() => triggerFeedback(setFeedback, `${label}: ${selectedClient.name}`)}>
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="clients-panel clients-segments-panel">
          <header className="clients-panel-title">
            <span>Сегменты</span>
            <strong>контекст для оператора</strong>
          </header>
          <div className="clients-segment-grid">
            {[
              ['Все', '12 480 клиентов · вся база'],
              ['VIP', '314 клиентов · 10% скидка'],
              ['Есть долг', '18 клиентов · проверить до закрытия'],
              ['Спящие', '924 клиента · 30+ дней без визита'],
              ['Новые', '36 регистраций за неделю']
            ].map(([label, detail]) => (
              <button
                key={label}
                type="button"
                className={activeSegment === label ? 'active' : undefined}
                onClick={() => setActiveSegment(label)}
              >
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="clients-panel clients-history-panel">
          <header className="clients-panel-title">
            <span>История клиента</span>
            <strong>последние операции</strong>
          </header>
          <div className="clients-history-list">
            {history.map(([time, event, total]) => (
              <article key={`${time}-${event}`} className="client-history-row">
                <span>{time}</span>
                <strong>{event}</strong>
                <b>{total}</b>
              </article>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}

function PaymentsWorkspace({ currencyCode }: { currencyCode: string }) {
  const [paymentSearch, setPaymentSearch] = useState('');
  const [selectedOperationKey, setSelectedOperationKey] = useState('15:08-Продажа по чеку-PC-06 · Madina S.');
  const [selectedMethod, setSelectedMethod] = useState('Наличные');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const operations = [
    ['15:08', 'Продажа по чеку', 'PC-06 · Madina S.', 'карта', `86 ${currencyCode}`, 'sale'],
    ['14:55', 'Возврат', 'Yusuf A.', 'наличные', `-20 ${currencyCode}`, 'refund'],
    ['14:41', 'Пополнение', 'Madina S.', 'карта', `200 ${currencyCode}`, 'deposit'],
    ['14:30', 'Продажа по чеку', 'Гость · стойка', 'наличные', `59 ${currencyCode}`, 'sale'],
    ['14:22', 'Игровое время', 'Amir K.', 'депозит', `45 ${currencyCode}`, 'session']
  ];
  const methods = [
    ['Наличные', `2 310 ${currencyCode}`, '48%', '3 операции'],
    ['Карта', `1 940 ${currencyCode}`, '40%', '4 операции'],
    ['Депозит', `570 ${currencyCode}`, '12%', '2 операции'],
    ['Возвраты', `20 ${currencyCode}`, '1 чек', 'проверить']
  ];
  const cashMoves = [
    ['09:02', 'Открытие смены', `1 000 ${currencyCode}`],
    ['13:20', 'Внесение', `300 ${currencyCode}`],
    ['14:55', 'Возврат', `-20 ${currencyCode}`]
  ];
  const visibleOperations = operations.filter(([time, type, client, method, total]) => (
    `${time} ${type} ${client} ${method} ${total}`.toLowerCase().includes(paymentSearch.trim().toLowerCase())
  ));
  const selectedOperation = operations.find(([time, type, client]) => `${time}-${type}-${client}` === selectedOperationKey) ?? operations[0];

  return (
    <main className="workspace-screen payments-screen">
      <section className="screen-head payments-head">
        <div>
          <span>Платежи</span>
          <h1>Платежи · касса смены и сверка</h1>
        </div>
      </section>

      <section className="state-strip payments-state-strip" aria-label="Сводка платежей">
        <StateFlag label="Выручка" value={`4 820 ${currencyCode}`} />
        <StateFlag label="Наличные" value={`2 310 ${currencyCode}`} />
        <StateFlag label="Карта" value={`1 940 ${currencyCode}`} />
        <StateFlag label="Депозиты" value={`570 ${currencyCode}`} />
        <StateFlag label="К сверке" value={`20 ${currencyCode}`} critical />
      </section>

      <section className="payments-layout">
        <section className="payments-panel payments-ledger-panel">
          <header className="payments-panel-title">
            <span>Операции смены</span>
            <strong>продажи, пополнения и возвраты</strong>
          </header>
          <label className="payments-search">
            <Search size={14} />
            <input
              placeholder="Клиент, чек, ПК, сумма"
              value={paymentSearch}
              onChange={(event) => setPaymentSearch(event.currentTarget.value)}
            />
          </label>
          <div className="payments-ledger-list">
            {visibleOperations.map(([time, type, client, method, total, tone]) => (
              <button
                key={`${time}-${type}-${client}`}
                type="button"
                className={`payment-operation-row ${tone}${`${time}-${type}-${client}` === selectedOperationKey ? ' active' : ''}`}
                onClick={() => setSelectedOperationKey(`${time}-${type}-${client}`)}
              >
                <span>{time}</span>
                <div>
                  <strong>{type}</strong>
                  <em>{client}</em>
                </div>
                <small>{method}</small>
                <b>{total}</b>
              </button>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-summary-panel">
          <header className="payments-panel-title">
            <span>Итоги смены</span>
            <strong>оперативная выручка</strong>
          </header>
          <div className="payments-total-card">
            <span>Всего за смену · выбрано {selectedOperation[0]}</span>
            <strong>4 820 {currencyCode}</strong>
            <em>{selectedOperation[1]} · {selectedOperation[2]} · {selectedOperation[4]}</em>
          </div>
          <div className="payments-metric-grid">
            <div><span>Чеков</span><strong>9</strong></div>
            <div><span>Средний чек</span><strong>536 {currencyCode}</strong></div>
            <div><span>Возвраты</span><strong>1</strong></div>
            <div><span>Долги</span><strong>3</strong></div>
          </div>
        </section>

        <section className="payments-panel payments-reconcile-panel">
          <header className="payments-panel-title">
            <span>Сверка кассы</span>
            <strong>перед закрытием</strong>
          </header>
          <div className="payments-reconcile-list">
            <div><span>Ожидается</span><strong>3 740 {currencyCode}</strong></div>
            <div><span>Посчитано</span><strong>3 720 {currencyCode}</strong></div>
            <div className="attention"><span>Расхождение</span><strong>20 {currencyCode}</strong></div>
          </div>
          <button type="button" className="payments-primary-action" onClick={() => triggerFeedback(setFeedback, 'Подготовить закрытие')}>Подготовить закрытие</button>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="payments-panel payments-methods-panel">
          <header className="payments-panel-title">
            <span>Методы оплаты</span>
            <strong>структура выручки</strong>
          </header>
          <div className="payments-method-grid">
            {methods.map(([label, total, share, detail]) => (
              <button
                key={label}
                type="button"
                className={`payment-method-card${selectedMethod === label ? ' active' : ''}`}
                onClick={() => setSelectedMethod(label)}
              >
                <strong>{label}</strong>
                <b>{total}</b>
                <span>{share} · {detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-cash-panel">
          <header className="payments-panel-title">
            <span>Движение наличных</span>
            <strong>кассовые события</strong>
          </header>
          <div className="payments-cash-list">
            {cashMoves.map(([time, event, total]) => (
              <article key={`${time}-${event}`} className="payment-cash-row">
                <span>{time}</span>
                <strong>{event}</strong>
                <b>{total}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-export-panel">
          <header className="payments-panel-title">
            <span>Отчёты</span>
            <strong>экспорт и журнал</strong>
          </header>
          <div className="payments-export-grid">
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Сводка смены')}><ReceiptText size={16} />Сводка смены</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Движение кассы')}><Banknote size={16} />Движение кассы</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Список чеков')}><ArrowRightLeft size={16} />Список чеков</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Сверка смены')}><ShieldAlert size={16} />Сверка смены</button>
          </div>
        </section>
      </section>
    </main>
  );
}

function LogsWorkspace({ currencyCode }: { currencyCode: string }) {
  const [eventSearch, setEventSearch] = useState('');
  const [activeLogFilter, setActiveLogFilter] = useState('Все события');
  const [selectedEventKey, setSelectedEventKey] = useState('15:04-PC-23 не отвечает');
  const [selectedSource, setSelectedSource] = useState('Агент');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const events = [
    ['15:09', 'Настройки просмотрены', 'Настройки · техник', 'Платформа', 'audit'],
    ['15:06', 'Пополнение депозита', `Madina S. · 200 ${currencyCode}`, 'Касса', 'money'],
    ['15:04', 'PC-23 не отвечает', 'нет связи 2 мин', 'Агент', 'device'],
    ['15:01', 'PC-01 сессия продлена', 'оператор · +15 мин', 'Оператор', 'session'],
    ['14:55', 'Возврат по чеку', `Yusuf A. · -20 ${currencyCode}`, 'Касса', 'money']
  ];
  const auditRows = [
    ['15:09', 'техник', 'Просмотр настроек', 'разрешено'],
    ['15:06', 'кассир', 'Пополнение депозита', 'подтверждено'],
    ['15:01', 'оператор', 'Продление сессии', 'платформа OK']
  ];
  const sourceCards: Array<[string, string, LucideIcon]> = [
    ['Агент', '23 онлайн · 1 офлайн', MonitorCheck],
    ['Касса', '9 чеков · 1 возврат', ReceiptText],
    ['Оператор', '14 действий смены', UserRoundPlus],
    ['Платформа', '3 предупреждения', ShieldAlert]
  ];
  const visibleEvents = events.filter(([time, title, detail, source, tone]) => {
    const filterMatches = activeLogFilter === 'Все события'
      || (activeLogFilter === 'Только ошибки' && tone === 'warning')
      || (activeLogFilter === 'ПК и связь' && (source === 'Агент' || detail.includes('связь')))
      || (activeLogFilter === 'Касса' && tone === 'money')
      || (activeLogFilter === 'Оператор' && source === 'Оператор')
      || (activeLogFilter === 'Системные' && source === 'Платформа');
    const searchMatches = `${time} ${title} ${detail} ${source}`.toLowerCase().includes(eventSearch.trim().toLowerCase());
    return filterMatches && searchMatches;
  });
  const selectedEvent = events.find(([time, title]) => `${time}-${title}` === selectedEventKey) ?? visibleEvents[0] ?? events[0];

  return (
    <main className="workspace-screen logs-screen">
      <section className="screen-head logs-head">
        <div>
          <span>Журнал</span>
          <h1>Журнал · события смены</h1>
        </div>
      </section>

      <section className="state-strip logs-state-strip" aria-label="Сводка журнала">
        <StateFlag label="События" value="128" />
        <StateFlag label="Ошибки" value="3" critical />
        <StateFlag label="Команды ПК" value="12" />
        <StateFlag label="Касса" value="9" />
        <StateFlag label="Записи" value="6" />
      </section>

      <section className="logs-layout">
        <section className="logs-panel logs-events-panel">
          <header className="logs-panel-title">
            <span>Журнал событий</span>
            <strong>поиск по ПК, клиенту, оператору или событию</strong>
          </header>
          <label className="logs-search">
            <Search size={14} />
            <input
              placeholder="ПК, клиент, оператор, событие"
              value={eventSearch}
              onChange={(event) => setEventSearch(event.currentTarget.value)}
            />
          </label>
          <div className="logs-event-list">
            {visibleEvents.map(([time, title, detail, source, tone]) => (
              <button
                key={`${time}-${title}`}
                type="button"
                className={`log-event-row ${tone}${`${time}-${title}` === selectedEventKey ? ' active' : ''}`}
                onClick={() => setSelectedEventKey(`${time}-${title}`)}
              >
                <span>{time}</span>
                <div>
                  <strong>{title}</strong>
                  <em>{detail}</em>
                </div>
                <b>{source}</b>
              </button>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-detail-panel">
          <header className="logs-panel-title">
            <span>Детали события</span>
            <strong>без внутренних ID</strong>
          </header>
          <div className={`log-detail-card ${selectedEvent[4]}`}>
            <span>{selectedEvent[0]} · {selectedEvent[3]}</span>
            <strong>{selectedEvent[1]}</strong>
            <em>{selectedEvent[2]}</em>
          </div>
          <div className="log-detail-list">
            <div><span>Источник</span><strong>{selectedEvent[3]}</strong></div>
            <div><span>Раздел</span><strong>{selectedEvent[1].includes('PC-') ? 'ПК' : 'Операция смены'}</strong></div>
            <div><span>Оператор</span><strong>Оператор смены</strong></div>
            <div><span>Результат</span><strong>{selectedEvent[2]}</strong></div>
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="logs-panel logs-filter-panel">
          <header className="logs-panel-title">
            <span>Фильтры</span>
            <strong>найти нужные записи</strong>
          </header>
          <div className="logs-filter-grid">
            {['Все события', 'Только ошибки', 'ПК и связь', 'Касса', 'Оператор', 'Системные'].map((filter) => (
              <button
                key={filter}
                type="button"
                className={activeLogFilter === filter ? 'active' : undefined}
                onClick={() => setActiveLogFilter(filter)}
              >
                {filter}
              </button>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-audit-panel">
          <header className="logs-panel-title">
            <span>Операции смены</span>
            <strong>действия персонала</strong>
          </header>
          <div className="logs-audit-list">
            {auditRows.map(([time, actor, action, result]) => (
              <article key={`${time}-${actor}-${action}`} className="log-audit-row">
                <span>{time}</span>
                <strong>{actor}</strong>
                <em>{action}</em>
                <b>{result}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-sources-panel">
          <header className="logs-panel-title">
            <span>Источники</span>
            <strong>каналы событий</strong>
          </header>
          <div className="logs-source-grid">
            {sourceCards.map(([label, detail, Icon]) => (
              <button
                key={label}
                type="button"
                className={`log-source-card${selectedSource === label ? ' active' : ''}`}
                onClick={() => setSelectedSource(label)}
              >
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-export-panel">
          <header className="logs-panel-title">
            <span>Экспорт</span>
            <strong>для проверки и поддержки</strong>
          </header>
          <div className="logs-export-grid">
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Сводка смены')}><ReceiptText size={16} />Сводка смены</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Только проблемы')}><AlertTriangle size={16} />Только проблемы</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Список действий')}><ArrowRightLeft size={16} />Список действий</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Пакет для поддержки')}><ShieldAlert size={16} />Пакет для поддержки</button>
          </div>
        </section>
      </section>
    </main>
  );
}

function SettingsWorkspace() {
  const [selectedSection, setSelectedSection] = useState('Профиль клуба');
  const [clubName, setClubName] = useState('AFK4 Dushanbe');
  const [city, setCity] = useState('Dushanbe');
  const [settingsDirty, setSettingsDirty] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const sections = [
    ['Профиль клуба', 'название, город, валюта'],
    ['Залы и ПК', 'зоны, рабочие места, статусы'],
    ['Тарифы', 'пакеты, постоплата, VIP'],
    ['Персонал', 'операторы, роли, доступы'],
    ['Товары и склад', 'товары, остатки, чеки'],
    ['Интеграции', 'платежи, уведомления, экспорт']
  ];
  const rooms = [
    ['Зал A', '10 ПК', 'основной зал'],
    ['Зал B', '8 ПК', 'тихий зал'],
    ['VIP', '2 ПК', 'повышенный тариф'],
    ['Буткемп', '4 ПК', 'командные места']
  ];
  const tariffs = [
    ['Стандарт', '25 TJS / час', 'для гостей и обычных клиентов'],
    ['VIP', '45 TJS / час', 'для VIP-зоны'],
    ['Ночь', '120 TJS / пакет', 'после 23:00'],
    ['Постоплата', 'лимит 100 TJS', 'только для доверенных клиентов']
  ];
  const readiness = [
    ['Профиль клуба', 'заполнен'],
    ['Залы и ПК', '24 рабочих места'],
    ['Персонал', '4 роли'],
    ['Касса', 'TJS · смена открыта'],
    ['Устройства', '23 из 24 онлайн']
  ];
  const actions: Array<[string, string, LucideIcon]> = [
    ['Добавить ПК', 'новое рабочее место на карте', MonitorCheck],
    ['Создать тариф', 'пакет или почасовая цена', CircleDollarSign],
    ['Пригласить сотрудника', 'оператор или техник', UserRoundPlus],
    ['Проверить устройства', 'Агент и оболочка', Wifi]
  ];
  const selectedSectionDetail = sections.find(([name]) => name === selectedSection)?.[1] ?? '';
  const markDirty = () => setSettingsDirty(true);
  const saveSettings = () => {
    if (!clubName.trim() || !city.trim()) {
      triggerFeedback(setFeedback, 'Проверить обязательные поля', 'failed', 'Заполните обязательные поля.');
      return;
    }

    setSettingsDirty(false);
    triggerFeedback(setFeedback, 'Настройки сохранены');
  };

  const renderSettingsContent = () => {
    if (selectedSection === 'Залы и ПК') {
      return (
        <>
          <div className="settings-section-title">
            <span>Залы и рабочие места</span>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Добавить зал')}>Добавить зал</button>
          </div>
          <div className="settings-room-grid">
            {rooms.map(([name, count, detail]) => (
              <button key={name} type="button" className="settings-room-card" onClick={() => triggerFeedback(setFeedback, `Открыть ${name}`)}>
                <strong>{name}</strong>
                <b>{count}</b>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </>
      );
    }

    if (selectedSection === 'Тарифы') {
      return (
        <>
          <div className="settings-section-title">
            <span>Тарифы</span>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Создать тариф')}>Создать тариф</button>
          </div>
          <div className="settings-tariff-list">
            {tariffs.map(([name, price, detail]) => (
              <button key={name} type="button" className="settings-tariff-row" onClick={() => triggerFeedback(setFeedback, `Открыть тариф ${name}`)}>
                <strong>{name}</strong>
                <b>{price}</b>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </>
      );
    }

    if (selectedSection === 'Персонал') {
      return (
        <div className="settings-config-grid">
          {['Владелец · полный доступ', 'Менеджер · смены и отчёты', 'Оператор · касса и карта', 'Техник · устройства'].map((role) => (
            <button key={role} type="button" onClick={() => triggerFeedback(setFeedback, role)}>
              <strong>{role.split(' · ')[0]}</strong>
              <span>{role.split(' · ')[1]}</span>
            </button>
          ))}
        </div>
      );
    }

    if (selectedSection === 'Товары и склад') {
      return (
        <div className="settings-config-grid">
          {['Напитки · 18 позиций', 'Кухня · 7 позиций', 'Услуги · 4 позиции', 'Низкие остатки · 2 товара'].map((item) => (
            <button key={item} type="button" onClick={() => triggerFeedback(setFeedback, item)}>
              <strong>{item.split(' · ')[0]}</strong>
              <span>{item.split(' · ')[1]}</span>
            </button>
          ))}
        </div>
      );
    }

    if (selectedSection === 'Интеграции') {
      return (
        <div className="settings-config-grid">
          {['Платежи · ручное подтверждение', 'Уведомления · выключены', 'Экспорт · отчёты включены', 'Связь · локальные данные'].map((item) => (
            <button key={item} type="button" onClick={() => triggerFeedback(setFeedback, item)}>
              <strong>{item.split(' · ')[0]}</strong>
              <span>{item.split(' · ')[1]}</span>
            </button>
          ))}
        </div>
      );
    }

    return (
      <>
        <div className="settings-form-grid">
          <label>Название клуба<input value={clubName} onChange={(event) => { setClubName(event.currentTarget.value); markDirty(); }} /></label>
          <label>Город<input value={city} onChange={(event) => { setCity(event.currentTarget.value); markDirty(); }} /></label>
          <label>Валюта<input value="TJS" readOnly /></label>
          <label>Часовой пояс<input value="Asia/Dushanbe" readOnly /></label>
        </div>
        <div className="settings-save-row">
          <span>{settingsDirty ? 'есть несохранённые изменения' : 'изменений нет'}</span>
          <button type="button" onClick={saveSettings}>Сохранить</button>
        </div>
      </>
    );
  };

  return (
    <main className="workspace-screen settings-screen">
      <section className="screen-head settings-head">
        <div>
          <span>Настройки</span>
          <h1>Настройки · клуб и правила работы</h1>
        </div>
      </section>

      <section className="settings-layout">
        <aside className="settings-nav-panel">
          <span>Разделы</span>
          {sections.map(([name, detail]) => (
            <button
              key={name}
              type="button"
              className={selectedSection === name ? 'active' : undefined}
              onClick={() => setSelectedSection(name)}
            >
              <strong>{name}</strong>
              <em>{detail}</em>
            </button>
          ))}
        </aside>

        <section className="settings-main-panel">
          <header className="settings-panel-title">
            <span>{selectedSection}</span>
            <strong>{selectedSectionDetail}</strong>
          </header>
          {renderSettingsContent()}
          <FeedbackNotice feedback={feedback} />
        </section>

        <aside className="settings-side-panel">
          <section className="settings-card-panel">
            <header className="settings-panel-title">
              <span>Готовность клуба</span>
              <strong>что важно перед запуском</strong>
            </header>
            <div className="settings-readiness-list">
              {readiness.map(([name, detail]) => (
                <div key={name}>
                  <span>{name}</span>
                  <strong>{detail}</strong>
                </div>
              ))}
            </div>
          </section>

          <section className="settings-card-panel">
            <header className="settings-panel-title">
              <span>Быстрые настройки</span>
              <strong>частые действия администратора</strong>
            </header>
            <div className="settings-action-grid">
              {actions.map(([label, detail, Icon]) => (
                <button key={label} type="button" className="settings-action-card" onClick={() => triggerFeedback(setFeedback, label)}>
                  <Icon size={17} />
                  <strong>{label}</strong>
                  <span>{detail}</span>
                </button>
              ))}
            </div>
          </section>

        </aside>
      </section>
    </main>
  );
}

function MapSidePanel({
  seat,
  seats: floorSeats,
  currencyCode,
  backend,
  actionsEnabled,
  onSeatAction
}: {
  seat: SeatSummary;
  seats: SeatSummary[];
  currencyCode: string;
  backend: OperatorBackendContext | null;
  actionsEnabled: boolean;
  onSeatAction: (request: SeatActionRequest) => Promise<SeatActionResult>;
}) {
  const session = backend?.session ?? null;
  const status = mapSeatStatus(seat);
  const activeBilling = billingLabel(seat.billing);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [billingMode, setBillingMode] = useState<SessionBillingModeId>('guest');
  const [durationMode, setDurationMode] = useState<SessionStartDurationMode>('fixed');
  const [playerSearch, setPlayerSearch] = useState('');
  const [billingPlayers, setBillingPlayers] = useState<PlayerClientItem[]>([]);
  const [selectedPlayerId, setSelectedPlayerId] = useState('');
  const [tariffOptions, setTariffOptions] = useState<TariffOptionDto[]>([]);
  const [selectedTariffVersionId, setSelectedTariffVersionId] = useState('');
  const [playerPackages, setPlayerPackages] = useState<PlayerPackageDto[]>([]);
  const [selectedPlayerPackageId, setSelectedPlayerPackageId] = useState('');
  const [billingStatus, setBillingStatus] = useState<LoadStatus>('fixture');
  const [billingError, setBillingError] = useState<string | null>(null);
  const [criticalAction, setCriticalAction] = useState<'end-session' | 'checkout' | null>(null);
  const transferCandidates = floorSeats.filter((candidate) =>
    candidate.id !== seat.id &&
    candidate.tone === 'ready' &&
    !candidate.activeSessionId);
  const [targetSeatId, setTargetSeatId] = useState(transferCandidates[0]?.id ?? '');
  const selectedPlayer = billingPlayers.find((player) => player.playerAccountId === selectedPlayerId) ?? null;
  const selectedTariff = tariffOptions.find((tariff) => readString(tariff, 'tariffVersionId') === selectedTariffVersionId) ??
    tariffOptions[0] ??
    null;
  const selectedPlayerPackage = playerPackages.find((playerPackage) => readString(playerPackage, 'playerPackageId') === selectedPlayerPackageId) ??
    playerPackages[0] ??
    null;
  const hasStoredSession = Boolean(seat.activeSessionId);
  const hasPendingSessionCommand = hasStoredSession && isPendingSeatCommand(seat);
  const hasActionableSession = hasStoredSession && !hasPendingSessionCommand;
  const hasActiveSession = hasStoredSession || seat.hasActiveSession === true || seat.tone === 'active';
  const isBusy = feedback.state === 'pending';
  const canStartPermission = hasPermission(session, permissionNames.startSession);
  const canExtendPermission = hasPermission(session, permissionNames.extendSession);
  const canTransferPermission = hasPermission(session, permissionNames.transferSession);
  const canEndPermission = hasPermission(session, permissionNames.endSession);
  const hasAnySessionActionPermission = canStartPermission ||
    canExtendPermission ||
    canTransferPermission ||
    canEndPermission;
  const billingSelection: SessionBillingSelection = billingMode === 'guest'
    ? {
        mode: 'guest',
        tariffRuleVersionId: defaultTariffRuleVersionId,
        playerAccountId: null,
        tariffVersionId: null,
        playerPackageId: null
      }
    : billingMode === 'package'
      ? {
          mode: 'package',
          tariffRuleVersionId: defaultTariffRuleVersionId,
          playerAccountId: selectedPlayerId || null,
          playerPackageId: selectedPlayerPackage === null ? null : readString(selectedPlayerPackage, 'playerPackageId')
        }
      : {
          mode: billingMode,
          tariffRuleVersionId: selectedTariff === null
            ? defaultTariffRuleVersionId
            : readString(selectedTariff, 'tariffRuleVersionId', defaultTariffRuleVersionId),
          playerAccountId: selectedPlayerId || null,
          tariffVersionId: selectedTariff === null ? null : readString(selectedTariff, 'tariffVersionId')
        };
  const billingMissing = billingMode !== 'guest' && !selectedPlayerId
    ? 'выберите игрока'
    : (billingMode === 'prepaid_wallet' || billingMode === 'postpaid_debt') && !billingSelection.tariffVersionId
      ? 'выберите тариф'
      : billingMode === 'package' && !billingSelection.playerPackageId
        ? 'выберите пакет игрока'
        : null;
  const billingReady = billingMissing === null;
  // An open tab (no fixed duration, settled later at checkout) is only valid for
  // an unbilled guest or a postpaid-debt player; other modes must be fixed.
  const openTabAllowed = billingMode === 'guest' || billingMode === 'postpaid_debt';
  const effectiveDurationMode: SessionStartDurationMode = openTabAllowed ? durationMode : 'fixed';
  const canStartSession = actionsEnabled && canStartPermission && billingReady && !hasActionableSession && seat.tone === 'ready';
  const canExtendSession = actionsEnabled && canExtendPermission && billingReady && hasActionableSession;
  const canEndSession = actionsEnabled && canEndPermission && hasActionableSession;
  const canTransferSession = actionsEnabled && canTransferPermission && hasActionableSession && targetSeatId.length > 0;
  const confirmationText = !actionsEnabled
    ? 'Карта платформы недоступна'
    : !hasAnySessionActionPermission
      ? 'Нет прав на действия с сессией'
      : !billingReady
        ? `Биллинг: ${billingMissing}`
      : hasPendingSessionCommand
        ? 'Команда уже отправлена. Дождитесь подтверждения ПК.'
      : feedback.state === 'idle'
        ? 'Ждём платформу'
        : feedbackText(feedback);
  const billingLoadText = billingStatus === 'backend'
    ? 'данные платформы'
    : billingStatus === 'loading'
      ? 'загрузка'
      : billingStatus === 'failed'
        ? billingError ?? 'ошибка загрузки'
        : 'ожидает платформу';

  useEffect(() => {
    if (targetSeatId.length > 0 && transferCandidates.some((candidate) => candidate.id === targetSeatId)) {
      return;
    }

    setTargetSeatId(transferCandidates[0]?.id ?? '');
  }, [seat.id, floorSeats]);

  useEffect(() => {
    let disposed = false;

    if (backend === null || !hasPermission(session, permissionNames.viewTariffs)) {
      setTariffOptions([]);
      setSelectedTariffVersionId('');
      setBillingStatus(backend === null ? 'failed' : 'fixture');
      setBillingError(backend === null ? 'Сессия оператора платформы недоступна.' : null);
      return undefined;
    }

    setBillingStatus('loading');
    setBillingError(null);
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.settings.getTariffOptions(backend.branchId)
      .then((options) => {
        if (disposed) {
          return;
        }

        setTariffOptions(options);
        setSelectedTariffVersionId((current) => current || readString(options[0], 'tariffVersionId'));
        setBillingStatus('backend');
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setTariffOptions([]);
        setSelectedTariffVersionId('');
        setBillingStatus('failed');
        setBillingError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  useEffect(() => {
    setCriticalAction(null);
  }, [seat.id, seat.activeSessionId]);

  useEffect(() => {
    let disposed = false;
    const query = playerSearch.trim();

    if (backend === null || query.length < 2 || !hasPermission(session, permissionNames.viewPlayers)) {
      setBillingPlayers([]);
      setSelectedPlayerId('');
      return undefined;
    }

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.players.searchPlayers(backend.branchId, query, 8)
      .then((players: PlayerSearchResultDto[]) => {
        if (disposed) {
          return;
        }

        const projected = players.map(projectPlayerClient);
        setBillingPlayers(projected);
        setSelectedPlayerId((current) => current || (projected[0]?.playerAccountId ?? ''));
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setBillingPlayers([]);
        setSelectedPlayerId('');
        setBillingError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, playerSearch]);

  useEffect(() => {
    let disposed = false;

    if (backend === null || !selectedPlayerId || !hasPermission(session, permissionNames.viewBilling)) {
      setPlayerPackages([]);
      setSelectedPlayerPackageId('');
      return undefined;
    }

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.players.getPlayerPackages(selectedPlayerId)
      .then((packages: PlayerPackageDto[]) => {
        if (disposed) {
          return;
        }

        setPlayerPackages(packages);
        setSelectedPlayerPackageId((current) => current || readString(packages[0], 'playerPackageId'));
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setPlayerPackages([]);
        setSelectedPlayerPackageId('');
        setBillingError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedPlayerId]);

  const runSeatAction = async (label: string, request: SeatActionRequest) => {
    setCriticalAction(null);
    setFeedback({ label, state: 'pending' });

    try {
      const result = await onSeatAction(request);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({
        label,
        state: 'failed',
        detail: projectOperatorFacingError(error)
      });
    }
  };

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>{zoneLabel(seat.zone)}</span>
          <h2>{seat.name}</h2>
        </div>
        <span className={`state-chip state-${seat.tone}`}>{toneLabels[seat.tone]}</span>
      </header>

      <section className={`context-status-row state-${seat.tone}`}>
        <span>{status.label}</span>
        <strong>{status.value}</strong>
      </section>

      <section className="action-grid context-actions" aria-label="Быстрые действия">
        {hasActiveSession ? (
          <>
            <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction('+15 мин', { type: 'extend', seat, minutes: 15, billing: billingSelection })}><Plus size={15} />15 мин</button>
            <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction('+30 мин', { type: 'extend', seat, minutes: 30, billing: billingSelection })}><TimerReset size={15} />30 мин</button>
            <button type="button" disabled={!canTransferSession || isBusy} onClick={() => runSeatAction('Перенос', { type: 'transfer', seat, targetSeatId })}><ArrowRightLeft size={15} />Перенос</button>
            <button type="button" className="danger" disabled={!canEndSession || isBusy} onClick={() => setCriticalAction('end-session')}><Square size={15} />Стоп</button>
          </>
        ) : (
          <>
            <button type="button" className="start-action" disabled={!canStartSession || isBusy} onClick={() => runSeatAction(effectiveDurationMode === 'open' ? 'Старт (открытый счёт)' : 'Старт 60 мин', { type: 'start', seat, billing: billingSelection, durationMode: effectiveDurationMode })}><Plus size={15} />{effectiveDurationMode === 'open' ? 'Старт · открытый счёт' : 'Старт 60 мин'}</button>
            <button type="button" disabled><TimerReset size={15} />Нет сессии</button>
          </>
        )}
      </section>
      {hasActiveSession && backend !== null && (
        <button
          type="button"
          className="checkout-action"
          disabled={!canEndSession || isBusy}
          onClick={() => setCriticalAction('checkout')}
        >
          <ReceiptText size={15} />Завершить и принять оплату
        </button>
      )}
      {hasActiveSession && (
        <label className="context-transfer-target">
          <span>Перенести на</span>
          <select value={targetSeatId} disabled={!actionsEnabled || !canTransferPermission || hasPendingSessionCommand || isBusy || transferCandidates.length === 0} onChange={(event) => setTargetSeatId(event.currentTarget.value)}>
            {transferCandidates.length === 0 && <option value="">Нет свободных ПК</option>}
            {transferCandidates.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>{candidate.name}</option>
            ))}
          </select>
        </label>
      )}
      {criticalAction === 'end-session' && (
        <CriticalActionConfirmation
          title="Подтвердите остановку сессии"
          detail={`${seat.name} · ${seat.remaining} · ${activeBilling}`}
          impact="Сессия будет завершена, платформа отправит команду блокировки ПК."
          confirmLabel="Подтвердить стоп"
          disabled={isBusy}
          onCancel={() => setCriticalAction(null)}
          onConfirm={() => void runSeatAction('Стоп', { type: 'end', seat })}
        />
      )}
      {criticalAction === 'checkout' && backend !== null && (
        <CheckoutDialog
          seat={seat}
          backend={backend}
          disabled={isBusy}
          onCancel={() => setCriticalAction(null)}
          onConfirm={(payments) => void runSeatAction('Оплата', { type: 'checkout', seat, payments })}
        />
      )}
      <FeedbackNotice feedback={feedback} />

      <section className="context-section">
        <div className="session-timer">
          <Clock3 size={17} />
          <div>
            <span>Активная сессия</span>
            <strong>{seat.remaining}</strong>
          </div>
        </div>
        <div className="detail-row">
          <span>Игрок</span>
          <strong>{seat.player}</strong>
        </div>
        <div className="detail-row">
          <span>Биллинг</span>
          <strong>{activeBilling} · {currencyCode}</strong>
        </div>
      </section>

      <section className="context-section">
        <div className="detail-row">
          <span>Устройство</span>
          <strong>{deviceStatusLabel(seat.device)}</strong>
        </div>
        <div className="detail-row">
          <span>Команда</span>
          <strong>{commandLabel(seat.command)}</strong>
        </div>
        <div className="detail-row">
          <span>Подтверждение</span>
          <strong>{confirmationText}</strong>
        </div>
      </section>

      <section className="context-section billing-selection-panel" aria-label="Настройка биллинга">
        <div className="billing-panel-head">
          <span>Биллинг сессии</span>
          <strong>{billingModeLabel(billingMode)}</strong>
          <em>{billingLoadText}</em>
        </div>
        <div className="billing-mode" aria-label="Режим биллинга">
          {billingModeOptions.map((option) => {
            const isBilledMode = option.id !== 'guest';
            return (
              <button
                key={option.id}
                type="button"
                className={option.id === billingMode ? 'active' : undefined}
                disabled={!actionsEnabled || isBusy || (isBilledMode && !hasPermission(session, permissionNames.viewBilling))}
                title={option.detail}
                onClick={() => setBillingMode(option.id)}
              >
                <span>{option.label}</span>
                <small>{option.detail}</small>
              </button>
            );
          })}
        </div>
        {!hasActiveSession && (
          <div className="billing-mode duration-mode" aria-label="Длительность сессии">
            <button
              type="button"
              className={effectiveDurationMode === 'fixed' ? 'active' : undefined}
              disabled={!actionsEnabled || isBusy}
              title="Фиксированные 60 минут"
              onClick={() => setDurationMode('fixed')}
            >
              <span>60 мин</span>
              <small>фиксировано</small>
            </button>
            <button
              type="button"
              className={effectiveDurationMode === 'open' ? 'active' : undefined}
              disabled={!actionsEnabled || isBusy || !openTabAllowed}
              title={openTabAllowed ? 'Время идёт, оплата при завершении' : 'Доступно для гостя или постоплаты'}
              onClick={() => setDurationMode('open')}
            >
              <span>Открытый счёт</span>
              <small>{openTabAllowed ? 'оплата при завершении' : 'гость / постоплата'}</small>
            </button>
          </div>
        )}
        {billingMode !== 'guest' && (
          <>
            <label className="context-transfer-target billing-input-row">
              <span>Игрок</span>
              <input
                aria-label="Игрок для биллинга"
                value={playerSearch}
                disabled={!actionsEnabled || isBusy || !hasPermission(session, permissionNames.viewPlayers)}
                placeholder="имя или телефон"
                onChange={(event) => setPlayerSearch(event.currentTarget.value)}
              />
            </label>
            <div className="billing-candidate-list" aria-label="Найденные игроки">
              {billingPlayers.map((player) => (
                <button
                  key={player.playerAccountId ?? player.name}
                  type="button"
                  className={player.playerAccountId === selectedPlayerId ? 'active' : undefined}
                  disabled={!player.playerAccountId || isBusy}
                  onClick={() => setSelectedPlayerId(player.playerAccountId ?? '')}
                >
                  <strong>{player.name}</strong>
                  <span>{formatMinorUnits(player.balanceMinorUnits, currencyCode)} · долг {formatMinorUnits(player.debtMinorUnits, currencyCode)}</span>
                </button>
              ))}
              {playerSearch.trim().length > 1 && billingPlayers.length === 0 && (
                <p>Игрок не найден</p>
              )}
            </div>
            {(billingMode === 'prepaid_wallet' || billingMode === 'postpaid_debt') && (
              <label className="context-transfer-target billing-input-row">
                <span>Тариф</span>
                <select
                  aria-label="Тариф для сессии"
                  value={selectedTariffVersionId}
                  disabled={!actionsEnabled || isBusy || tariffOptions.length === 0}
                  onChange={(event) => setSelectedTariffVersionId(event.currentTarget.value)}
                >
                  {tariffOptions.length === 0 && <option value="">Нет тарифов</option>}
                  {tariffOptions.map((tariff) => (
                    <option key={readString(tariff, 'tariffVersionId')} value={readString(tariff, 'tariffVersionId')}>
                      {tariffOptionLabel(tariff, currencyCode)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            {billingMode === 'package' && (
              <label className="context-transfer-target billing-input-row">
                <span>Пакет</span>
                <select
                  aria-label="Пакет для сессии"
                  value={selectedPlayerPackageId}
                  disabled={!actionsEnabled || isBusy || !selectedPlayer || playerPackages.length === 0}
                  onChange={(event) => setSelectedPlayerPackageId(event.currentTarget.value)}
                >
                  {playerPackages.length === 0 && <option value="">Нет активных пакетов</option>}
                  {playerPackages.map((playerPackage) => (
                    <option key={readString(playerPackage, 'playerPackageId')} value={readString(playerPackage, 'playerPackageId')}>
                      {playerPackageLabel(playerPackage)}
                    </option>
                  ))}
                </select>
              </label>
            )}
            <div className="detail-row billing-meta">
              <span>Выбор</span>
              <strong>{billingMissing ?? `${billingModeLabel(billingMode)} готов`}</strong>
            </div>
          </>
        )}
      </section>
    </aside>
  );
}

function SummarySidePanel({ workspace, currencyCode }: { workspace: WorkspaceId; currencyCode: string }) {
  const title = {
    map: 'PC-01',
    dashboard: 'Смена',
    booking: 'Бронь 16:00',
    pos: 'Корзина',
    players: 'Amir K.',
    payments: 'Платеж 14:30',
    payment_cards: 'Приём платежей',
    logs: 'Событие журнала',
    settings: 'Настройки',
    review: 'Проверка'
  }[workspace];

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>Детали</span>
          <h2>{title}</h2>
        </div>
        <span className="state-chip state-active">Активно</span>
      </header>
      <section className="context-section">
        <div className="detail-row"><span>Выручка</span><strong>4 820 {currencyCode}</strong></div>
        <div className="detail-row"><span>В работе</span><strong>2 действия</strong></div>
        <div className="detail-row"><span>Источник</span><strong>Локальные данные</strong></div>
      </section>
      <button type="button" className="primary-wide">Открыть действие</button>
    </aside>
  );
}

type PosCatalogItem = {
  productId?: string;
  name: string;
  priceMinorUnits: number;
  category: string;
  note: string;
  stockOnHand: number;
  source: 'fixture' | 'backend';
};

type PosCartItem = PosCatalogItem & {
  quantity: number;
};

const fixturePosProducts: PosCatalogItem[] = [
  { name: 'Кола 0.5', priceMinorUnits: 1200, category: 'Напитки', note: 'локальный пример', stockOnHand: 0, source: 'fixture' },
  { name: 'Вода 0.5', priceMinorUnits: 600, category: 'Напитки', note: 'локальный пример', stockOnHand: 0, source: 'fixture' },
  { name: 'Хот-дог', priceMinorUnits: 2800, category: 'Еда', note: 'локальный пример', stockOnHand: 0, source: 'fixture' },
  { name: 'Гостевой час', priceMinorUnits: 2500, category: 'Услуги', note: 'локальный пример', stockOnHand: 0, source: 'fixture' }
];

function projectPosProduct(product: PosProductDto, currencyCode: string): PosCatalogItem {
  const price = readMoney(product, 'price');
  return {
    productId: readString(product, 'productId') || undefined,
    name: readString(product, 'name', 'Товар'),
    priceMinorUnits: price?.minorUnits ?? 0,
    category: readString(product, 'categoryName', readString(product, 'categoryId', 'Каталог')),
    note: `${readString(product, 'sku', 'SKU')} · ${readNumber(product, 'stockOnHand', 0)} шт.`,
    stockOnHand: readNumber(product, 'stockOnHand', 0),
    source: price?.currencyCode === currencyCode || price !== null ? 'backend' : 'backend'
  };
}

function BackendPosWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [activeCategory, setActiveCategory] = useState('Все');
  const [productSearch, setProductSearch] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('Наличные');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null);
  const [catalog, setCatalog] = useState<PosCatalogItem[]>(() => backend === null ? fixturePosProducts : []);
  const [salesReport, setSalesReport] = useState<ReportResultDto | null>(null);
  const [lastSale, setLastSale] = useState<PosSaleDto | null>(null);
  const [selectedSaleDetail, setSelectedSaleDetail] = useState<PosSaleDto | null>(null);
  const [selectedReceiptDetail, setSelectedReceiptDetail] = useState<ReceiptDto | null>(null);
  const [selectedRefundSaleId, setSelectedRefundSaleId] = useState('');
  const [playerSearch, setPlayerSearch] = useState('');
  const [posPlayers, setPosPlayers] = useState<PlayerClientItem[]>([]);
  const [selectedPlayerId, setSelectedPlayerId] = useState('');
  const [playerLoadStatus, setPlayerLoadStatus] = useState<LoadStatus>('fixture');
  const [newPlayerName, setNewPlayerName] = useState('');
  const [newPlayerPhone, setNewPlayerPhone] = useState('');
  const [stockWriteOffProductId, setStockWriteOffProductId] = useState('');
  const [stockWriteOffQuantity, setStockWriteOffQuantity] = useState('1');
  const [stockWriteOffReason, setStockWriteOffReason] = useState('операторское списание');
  const [criticalAction, setCriticalAction] = useState<'refund-sale' | 'void-draft' | null>(null);
  const [refundReason, setRefundReason] = useState('Возврат по запросу клиента');
  const [voidReason, setVoidReason] = useState('Ошибка в черновике чека');
  const [cartItems, setCartItems] = useState<PosCartItem[]>(() => backend === null
    ? [
        { ...fixturePosProducts[0], quantity: 1 },
        { ...fixturePosProducts[3], quantity: 1 }
      ]
    : []);

  const loadBackendPos = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      setCatalog(fixturePosProducts);
      setCartItems((items) => items.length > 0 ? items : [
        { ...fixturePosProducts[0], quantity: 1 },
        { ...fixturePosProducts[3], quantity: 1 }
      ]);
      return;
    }

    setLoadStatus('loading');
    try {
      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [nextCatalog, nextShift, nextSalesReport] = await Promise.all([
        clients.pos.getCatalog(nextBackend.branchId),
        clients.shifts.getCurrentShift(nextBackend.branchId),
        clients.shifts.getSalesReport(nextBackend.branchId, { limit: 8 })
      ]);

      const products = Array.isArray(nextCatalog)
        ? nextCatalog.map((product) => projectPosProduct(product, currencyCode))
        : [];

      const backendProducts = products.filter((product) => product.source === 'backend' && product.productId);
      setCatalog(products);
      setStockWriteOffProductId((current) => backendProducts.some((product) => product.productId === current)
        ? current
        : backendProducts[0]?.productId ?? '');
      setCurrentShift(nextShift);
      setSalesReport(nextSalesReport);
      const nextSalesRows = readArray(nextSalesReport, 'rows');
      setSelectedRefundSaleId((current) => nextSalesRows.some((row) => readString(row, 'posSaleId') === current)
        ? current
        : readString(nextSalesRows.find((row) => readString(row, 'state').toLowerCase() === 'paid'), 'posSaleId'));
      setCartItems((items) => {
        const productById = new Map(backendProducts.map((product) => [product.productId, product]));
        const validBackendItems = items
          .filter((item) => item.source === 'backend' && item.productId && productById.has(item.productId))
          .map((item) => ({
            ...productById.get(item.productId!)!,
            quantity: item.quantity
          }));
        if (validBackendItems.length > 0) {
          return validBackendItems;
        }

        return backendProducts[0] ? [{ ...backendProducts[0], quantity: 1 }] : [];
      });
      setLoadStatus('backend');
    } catch (error) {
      setLoadStatus('failed');
      setFeedback({
        label: 'Касса',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  useEffect(() => {
    void loadBackendPos();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  useEffect(() => {
    let disposed = false;
    const query = playerSearch.trim();

    if (backend === null || query.length < 2 || !hasPermission(backend.session, permissionNames.viewPlayers)) {
      setPosPlayers([]);
      setSelectedPlayerId('');
      setPlayerLoadStatus(backend === null ? 'fixture' : 'backend');
      return undefined;
    }

    setPlayerLoadStatus('loading');
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.players.searchPlayers(backend.branchId, query, 8)
      .then((players: PlayerSearchResultDto[]) => {
        if (disposed) {
          return;
        }

        const projected = Array.isArray(players) ? players.map(projectPlayerClient) : [];
        setPosPlayers(projected);
        setSelectedPlayerId((current) => current && projected.some((player) => player.playerAccountId === current)
          ? current
          : projected[0]?.playerAccountId ?? '');
        setPlayerLoadStatus('backend');
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setPosPlayers([]);
        setSelectedPlayerId('');
        setPlayerLoadStatus('failed');
        setFeedback({ label: 'Клиент', state: 'failed', detail: projectOperatorError(error).detail });
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, playerSearch]);

  const categories = ['Все', ...Array.from(new Set(catalog.map((product) => product.category))).slice(0, 5)];
  const visibleProducts = catalog.filter((product) => {
    const categoryMatches = activeCategory === 'Все' || product.category === activeCategory;
    const searchMatches = `${product.name} ${product.category} ${product.note}`.toLowerCase().includes(productSearch.trim().toLowerCase());
    return categoryMatches && searchMatches;
  });
  const selectedPosPlayer = posPlayers.find((player) => player.playerAccountId === selectedPlayerId) ?? null;
  const selectedPosPlayerId = selectedPosPlayer?.playerAccountId ?? null;
  const playerSearchQuery = playerSearch.trim();
  const paymentMethodName = paymentMethod === 'Карта' ? 'card_manual' : 'cash';
  const newPlayerDisplayName = newPlayerName.trim() || playerSearchQuery;
  const cartTotalMinorUnits = cartItems.reduce((sum, item) => sum + item.priceMinorUnits * item.quantity, 0);
  const acceptedCashMinorUnits = paymentMethod === 'Наличные'
    ? Math.ceil(cartTotalMinorUnits / 1000) * 1000
    : cartTotalMinorUnits;
  const changeMinorUnits = acceptedCashMinorUnits - cartTotalMinorUnits;
  const salesRows = readArray(salesReport, 'rows');
  const grossSales = readMoney(salesReport, 'grossSalesTotal');
  const refundsTotal = readMoney(salesReport, 'refundsTotal');
  const lowStockCount = catalog.filter((product) => product.source === 'backend' && product.stockOnHand <= 2).length;
  const shiftId = readString(currentShift, 'shiftId');
  const shiftState = readString(currentShift, 'state', currentShift === null ? 'нет смены' : 'unknown');
  const reportRefundableSales = salesRows.filter((row) => readString(row, 'state').toLowerCase() === 'paid');
  const selectedRefundableSale = reportRefundableSales.find((row) => readString(row, 'posSaleId') === selectedRefundSaleId)
    ?? reportRefundableSales[0]
    ?? salesRows[0]
    ?? lastSale;
  const selectedRefundableSaleId = readString(selectedRefundableSale, 'posSaleId');
  const canRefundSelectedSale = backend !== null
    && selectedRefundableSaleId.length > 0
    && hasPermission(backend.session, permissionNames.refundPosSale);
  const canViewSaleDetails = backend !== null && hasPermission(backend.session, permissionNames.viewReceipt);
  const canVoidDraftCart = backend !== null
    && shiftId.length > 0
    && cartItems.length > 0
    && cartItems.every((item) => Boolean(item.productId) && item.source === 'backend')
    && hasPermission(backend.session, permissionNames.createPosSale)
    && hasPermission(backend.session, permissionNames.voidPosSale);
  const canAcceptPayment = backend !== null
    && shiftId.length > 0
    && cartItems.length > 0
    && cartItems.every((item) => Boolean(item.productId) && item.source === 'backend')
    && hasPermission(backend.session, permissionNames.createPosSale)
    && hasPermission(backend.session, permissionNames.payPosSale);
  const canCreatePosPlayer = backend !== null
    && hasPermission(backend.session, permissionNames.createPlayerAccount)
    && newPlayerDisplayName.length > 0;
  const canTopUpPosWallet = backend !== null
    && selectedPosPlayerId !== null
    && cartTotalMinorUnits > 0
    && hasPermission(backend.session, permissionNames.topUpWallet);
  const backendCatalogProducts = catalog.filter((product) => product.source === 'backend' && product.productId);
  const selectedStockProduct = backendCatalogProducts.find((product) => product.productId === stockWriteOffProductId)
    ?? backendCatalogProducts[0]
    ?? null;
  const canWriteOffStock = backend !== null
    && selectedStockProduct !== null
    && hasPermission(backend.session, permissionNames.manageInventoryStock);

  const addProduct = (product: PosCatalogItem) => {
    setCartItems((items) => {
      const existing = items.find((item) => item.name === product.name);
      if (existing) {
        return items.map((item) => item.name === product.name ? { ...item, quantity: item.quantity + 1 } : item);
      }

      return [...items, { ...product, quantity: 1 }];
    });
    triggerFeedback(setFeedback, `${product.name} добавлен`, 'confirmed');
  };

  const createPosPlayer = async () => {
    setFeedback({ label: 'Новая карта', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
        throw new Error('Нет прав на создание карты клиента.');
      }

      const displayName = newPlayerDisplayName;
      if (!displayName) {
        throw new Error('Введите имя клиента для новой карты.');
      }

      const player = await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).players.createPlayer(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        displayName,
        phoneNumber: newPlayerPhone.trim() || null,
        idempotencyKey: createIdempotencyKey('player-create')
      });
      const projected = projectPlayerClient(player);
      setPosPlayers((players) => [
        projected,
        ...players.filter((candidate) => candidate.playerAccountId !== projected.playerAccountId)
      ]);
      setSelectedPlayerId(projected.playerAccountId ?? '');
      setNewPlayerName('');
      setNewPlayerPhone('');
      setFeedback({ label: 'Новая карта', state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: 'Новая карта',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const writeOffStock = async () => {
    setFeedback({ label: 'Списание склада', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.manageInventoryStock)) {
        throw new Error('Нет прав на управление остатками.');
      }

      const productId = selectedStockProduct?.productId;
      const quantity = Number(stockWriteOffQuantity);
      const reason = stockWriteOffReason.trim();
      if (!productId || !Number.isInteger(quantity) || quantity <= 0 || !reason) {
        throw new Error('Выберите товар платформы, целое количество больше нуля и причину списания.');
      }

      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).inventory.createStockMovement(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        productId,
        movementType: 'adjustment',
        quantityDelta: -quantity,
        unitCost: { currencyCode, minorUnits: 0 },
        reason,
        idempotencyKey: createIdempotencyKey('stock-write-off')
      });

      setFeedback({ label: 'Списание склада', state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: 'Списание склада',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const topUpSelectedPosPlayer = async () => {
    setFeedback({ label: 'Пополнить депозит', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.topUpWallet)) {
        throw new Error('Нет прав на пополнение депозита.');
      }

      if (!selectedPosPlayerId) {
        throw new Error('Выберите клиента платформы для пополнения депозита.');
      }

      if (cartTotalMinorUnits <= 0) {
        throw new Error('Добавьте сумму в корзину перед пополнением депозита.');
      }

      const wallet = await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).players.topUpWallet(selectedPosPlayerId, {
        organizationId: nextBackend.session.organizationId,
        amount: { currencyCode, minorUnits: cartTotalMinorUnits },
        reason: 'operator POS wallet top-up',
        idempotencyKey: createIdempotencyKey('wallet-top-up')
      });
      const walletBalance = readMoney(wallet, 'walletBalance')?.minorUnits;
      if (walletBalance !== undefined) {
        setPosPlayers((players) => players.map((player) => player.playerAccountId === selectedPosPlayerId
          ? { ...player, balanceMinorUnits: walletBalance }
          : player));
      }
      setFeedback({
        label: 'Пополнить депозит',
        state: 'confirmed',
        detail: formatMinorUnits(cartTotalMinorUnits, currencyCode)
      });
    } catch (error) {
      setFeedback({
        label: 'Пополнить депозит',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const acceptPayment = async () => {
    setFeedback({ label: 'Оплата', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.createPosSale) || !hasPermission(nextBackend.session, permissionNames.payPosSale)) {
        throw new Error('Нет прав на создание или оплату чека.');
      }

      if (!shiftId) {
        throw new Error('Откройте смену перед оплатой.');
      }

      if (cartItems.length === 0 || cartItems.some((item) => !item.productId || item.source !== 'backend')) {
        throw new Error('Каталог товаров не загружен для текущей корзины.');
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const sale = await clients.pos.createSale(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        shiftId,
        lines: cartItems.map((item) => ({
          productId: item.productId!,
          quantity: item.quantity,
          unitPrice: {
            currencyCode,
            minorUnits: item.priceMinorUnits
          }
        })),
        idempotencyKey: createIdempotencyKey('pos-sale'),
        playerAccountId: selectedPosPlayerId
      });
      const saleId = readString(sale, 'posSaleId');
      if (!saleId) {
        throw new Error('Платформа не подтвердила номер чека. Повторите операцию или обратитесь в поддержку.');
      }

      const paidSale = await clients.pos.paySaleManual(saleId, {
        organizationId: nextBackend.session.organizationId,
        paymentMethod: paymentMethodName,
        amount: {
          currencyCode,
          minorUnits: cartTotalMinorUnits
        },
        note: 'operator POS checkout',
        idempotencyKey: createIdempotencyKey('pos-payment')
      });

      setLastSale(paidSale);
      setCartItems([]);
      setFeedback({ label: 'Оплата', state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: 'Оплата',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const refundLatestSale = async () => {
    setCriticalAction(null);
    setFeedback({ label: 'Возврат по чеку', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.refundPosSale)) {
        throw new Error('Нет прав на возврат по чеку.');
      }

      if (!selectedRefundableSaleId) {
        throw new Error('Выберите чек для возврата.');
      }

      const reason = refundReason.trim();
      if (!reason) {
        throw new Error('Введите причину возврата.');
      }

      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).pos.refundSale(selectedRefundableSaleId, {
        organizationId: nextBackend.session.organizationId,
        reason,
        idempotencyKey: createIdempotencyKey('pos-refund')
      });
      setFeedback({ label: 'Возврат по чеку', state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: 'Возврат по чеку',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const loadSaleDetail = async (saleId: string) => {
    setSelectedReceiptDetail(null);
    setFeedback({ label: 'Детали чека', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.viewReceipt)) {
        throw new Error('Нет прав на просмотр чеков.');
      }

      if (!saleId) {
        throw new Error('Выберите чек из списка.');
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const sale = await clients.pos.getSale(saleId);
      const latestReceipt = readRecord(sale, 'latestReceipt');
      const receiptId = readString(latestReceipt, 'receiptId');
      const receipt = receiptId ? await clients.pos.getReceipt(receiptId) : null;
      setSelectedSaleDetail(sale);
      setSelectedReceiptDetail(receipt);
      setFeedback({ label: 'Детали чека', state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: 'Детали чека',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const selectedReceiptRecord = selectedReceiptDetail ?? readRecord(selectedSaleDetail, 'latestReceipt');

  const printSelectedReceipt = () => {
    setFeedback({ label: 'Печать чека', state: 'pending' });
    try {
      if (selectedSaleDetail === null) {
        throw new Error('Откройте чек платформы перед печатью.');
      }

      const receiptText = buildPosReceiptText(selectedSaleDetail, selectedReceiptRecord, currencyCode);
      const printWindow = window.open('', '_blank', 'width=360,height=640');
      if (printWindow === null) {
        throw new Error('Не удалось открыть окно печати чека.');
      }

      printWindow.document.write(`<pre style="font: 13px/1.45 monospace; white-space: pre-wrap;">${escapeHtml(receiptText)}</pre>`);
      printWindow.document.close();
      printWindow.focus();
      printWindow.print();
      setFeedback({ label: 'Печать чека', state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: 'Печать чека',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const exportSelectedReceipt = () => {
    setFeedback({ label: 'Экспорт чека', state: 'pending' });
    try {
      if (selectedSaleDetail === null) {
        throw new Error('Откройте чек платформы перед экспортом.');
      }

      const receiptText = buildPosReceiptText(selectedSaleDetail, selectedReceiptRecord, currencyCode);
      const receiptNumber = readString(selectedReceiptRecord, 'receiptNumber', 'receipt');
      downloadTextFile(`${safeReceiptFileName(receiptNumber)}.txt`, receiptText);
      setFeedback({ label: 'Экспорт чека', state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: 'Экспорт чека',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const voidDraftCart = async () => {
    setCriticalAction(null);
    setFeedback({ label: 'Аннулировать черновик', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.createPosSale) || !hasPermission(nextBackend.session, permissionNames.voidPosSale)) {
        throw new Error('Нет прав на создание или аннулирование чека.');
      }

      if (!shiftId) {
        throw new Error('Открытая смена обязательна для аннулирования черновика.');
      }

      if (cartItems.length === 0 || cartItems.some((item) => !item.productId || item.source !== 'backend')) {
        throw new Error('Каталог товаров не загружен для текущей корзины.');
      }

      const reason = voidReason.trim();
      if (!reason) {
        throw new Error('Введите причину аннулирования.');
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const draft = await clients.pos.createSale(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        shiftId,
        lines: cartItems.map((item) => ({
          productId: item.productId!,
          quantity: item.quantity,
          unitPrice: {
            currencyCode,
            minorUnits: item.priceMinorUnits
          }
        })),
        idempotencyKey: createIdempotencyKey('pos-sale-draft'),
        playerAccountId: selectedPosPlayerId
      });
      const saleId = readString(draft, 'posSaleId');
      if (!saleId) {
        throw new Error('Платформа не подтвердила черновик чека. Повторите операцию или обратитесь в поддержку.');
      }

      const voidedSale = await clients.pos.voidSale(saleId, {
        organizationId: nextBackend.session.organizationId,
        reason,
        idempotencyKey: createIdempotencyKey('pos-void')
      });

      setLastSale(voidedSale);
      setCartItems([]);
      setFeedback({ label: 'Аннулировать черновик', state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: 'Аннулировать черновик',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  return (
    <main className="workspace-screen pos-screen">
      <section className="screen-head pos-head">
        <div>
          <span>Продажи</span>
          <h1>Продажи · чек и кассовые операции</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Платформа подключена')}</span>
        </div>
      </section>

      <section className="state-strip pos-state-strip" aria-label="Сводка продаж">
        <StateFlag label="Продажи" value={`${salesRows.length} · ${grossSales ? formatMinorUnits(grossSales.minorUnits, grossSales.currencyCode) : `0 ${currencyCode}`}`} />
        <StateFlag label="Возвраты" value={refundsTotal ? formatMinorUnits(refundsTotal.minorUnits, refundsTotal.currencyCode) : `0 ${currencyCode}`} critical={(refundsTotal?.minorUnits ?? 0) > 0} />
        <StateFlag label="Товары" value={`${catalog.length} поз.`} />
        <StateFlag label="Склад" value={`${lowStockCount} низко`} critical={lowStockCount > 0} />
        <StateFlag label="Смена" value={shiftStateLabel(shiftState)} critical={!shiftId} />
      </section>

      <section className="pos-layout">
        <section className="pos-panel pos-catalog-panel">
          <header className="pos-panel-title">
            <span>Каталог</span>
            <strong>активные товары, остатки и поиск</strong>
          </header>
          <label className="pos-search">
            <Search size={14} />
            <input
              placeholder="Товар, услуга, SKU"
              value={productSearch}
              onChange={(event) => setProductSearch(event.currentTarget.value)}
            />
          </label>
          <div className="pos-category-row" aria-label="Категории товаров">
            {categories.map((category) => (
              <button
                key={category}
                type="button"
                className={activeCategory === category ? 'active' : undefined}
                onClick={() => setActiveCategory(category)}
              >
                {category}
              </button>
            ))}
          </div>
          <div className="pos-catalog-grid">
            {visibleProducts.length === 0 ? (
              <div className="pos-empty-state">
                <strong>Каталог пуст</strong>
                <span>{loadStatus === 'backend' ? 'Активных товаров для этого филиала нет.' : 'Загрузите каталог.'}</span>
              </div>
            ) : (
              visibleProducts.map((product) => (
                <button key={`${product.productId ?? product.name}-${product.name}`} type="button" className="pos-product-card" onClick={() => addProduct(product)}>
                  <strong>{product.name}</strong>
                  <span>{product.category}</span>
                  <b>{formatMinorUnits(product.priceMinorUnits, currencyCode)}</b>
                  <em>{product.note}</em>
                </button>
              ))
            )}
          </div>
        </section>

        <section className="pos-panel pos-cart-panel">
          <header className="pos-panel-title">
            <span>Корзина</span>
            <strong>{shiftId ? 'смена открыта' : 'откройте смену'}</strong>
          </header>
          <div className="pos-cart-client">
            <UserRoundPlus size={17} />
            <div>
              <span>Клиент</span>
              <strong>{selectedPosPlayer ? selectedPosPlayer.name : 'Гость · без карты'}</strong>
              <em>{selectedPosPlayer
                ? `${selectedPosPlayer.phoneNumber || 'без телефона'} · ${formatMinorUnits(selectedPosPlayer.balanceMinorUnits, currencyCode)}`
                : 'продажа без карты клиента'}</em>
            </div>
            <button
              type="button"
              onClick={() => {
                setPlayerSearch('');
                setSelectedPlayerId('');
                setPosPlayers([]);
              }}
            >
              Гость
            </button>
          </div>
          <label className="pos-search pos-client-search">
            <Search size={14} />
            <input
              aria-label="Клиент"
              value={playerSearch}
              disabled={backend !== null && !hasPermission(backend.session, permissionNames.viewPlayers)}
              placeholder="имя или телефон клиента"
              onChange={(event) => setPlayerSearch(event.currentTarget.value)}
            />
          </label>
          {playerSearchQuery.length > 1 && (
            <div className="pos-client-candidates" aria-label="Клиенты продажи">
              {posPlayers.map((player) => (
                <button
                  key={player.playerAccountId ?? player.name}
                  type="button"
                  className={player.playerAccountId === selectedPlayerId ? 'active' : undefined}
                  disabled={!player.playerAccountId || feedback.state === 'pending'}
                  onClick={() => setSelectedPlayerId(player.playerAccountId ?? '')}
                >
                  <strong>{player.name}</strong>
                  <span>{formatMinorUnits(player.balanceMinorUnits, currencyCode)} · долг {formatMinorUnits(player.debtMinorUnits, currencyCode)}</span>
                </button>
              ))}
              {playerLoadStatus === 'loading' && <p>Поиск клиента</p>}
              {playerLoadStatus !== 'loading' && posPlayers.length === 0 && <p>Клиент не найден</p>}
            </div>
          )}
          <div className="pos-new-client-form">
            <label>
              <span>Новая карта</span>
              <input
                aria-label="Имя клиента"
                value={newPlayerName}
                disabled={backend !== null && !hasPermission(backend.session, permissionNames.createPlayerAccount)}
                placeholder={playerSearchQuery || 'имя клиента'}
                onChange={(event) => setNewPlayerName(event.currentTarget.value)}
              />
            </label>
            <label>
              <span>Телефон</span>
              <input
                aria-label="Телефон клиента"
                value={newPlayerPhone}
                disabled={backend !== null && !hasPermission(backend.session, permissionNames.createPlayerAccount)}
                placeholder="+992..."
                onChange={(event) => setNewPlayerPhone(event.currentTarget.value)}
              />
            </label>
            <button type="button" disabled={!canCreatePosPlayer || feedback.state === 'pending'} onClick={createPosPlayer}>
              <UserRoundPlus size={14} />
              Создать
            </button>
          </div>
          <div className="pos-cart-list">
            {cartItems.length === 0 ? (
              <article className="pos-cart-row empty">
                <div>
                  <strong>Корзина пуста</strong>
                  <span>Добавьте товар из каталога.</span>
                </div>
                <b>{formatMinorUnits(0, currencyCode)}</b>
              </article>
            ) : (
              cartItems.map((item) => (
                <article key={`${item.productId ?? item.name}-${item.name}`} className="pos-cart-row interactive-row">
                  <div>
                    <strong>{item.name}</strong>
                    <span>{item.quantity} шт.</span>
                  </div>
                  <b>{formatMinorUnits(item.priceMinorUnits * item.quantity, currencyCode)}</b>
                </article>
              ))
            )}
          </div>
          <div className="pos-total-card">
            <span>Итого к оплате</span>
            <strong>{formatMinorUnits(cartTotalMinorUnits, currencyCode)}</strong>
            <em>{lastSale ? 'последний чек принят' : 'чек создаётся после подтверждения платформы'}</em>
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="pos-panel pos-payment-panel">
          <header className="pos-panel-title">
            <span>Оплата</span>
            <strong>чек и подтверждение оплаты</strong>
          </header>
          <div className="pos-payment-methods">
            {['Наличные', 'Карта', 'Депозит'].map((method) => (
              <button
                key={method}
                type="button"
                className={paymentMethod === method ? 'active' : undefined}
                disabled={method === 'Депозит' || feedback.state === 'pending'}
                title={method === 'Депозит' ? 'Оплата с депозита будет включена после подключения депозитного платежа.' : undefined}
                onClick={() => setPaymentMethod(method)}
              >
                {method === 'Наличные' && <Banknote size={15} />}
                {method === 'Карта' && <CircleDollarSign size={15} />}
                {method === 'Депозит' && <ReceiptText size={15} />}
                {method}
              </button>
            ))}
          </div>
          <div className="pos-payment-summary">
            <div><span>Принято</span><strong>{formatMinorUnits(acceptedCashMinorUnits, currencyCode)}</strong></div>
            <div><span>Сдача</span><strong>{formatMinorUnits(changeMinorUnits, currencyCode)}</strong></div>
            <div><span>Смена</span><strong>{shiftId ? 'Открыта' : 'Нет'}</strong></div>
          </div>
          <button type="button" className="pos-primary-action" disabled={!canAcceptPayment || feedback.state === 'pending'} onClick={acceptPayment}>Принять оплату</button>
          <button type="button" className="pos-secondary-action" onClick={() => setCartItems([])}>Очистить корзину</button>
        </section>

        <section className="pos-panel pos-receipts-panel">
          <header className="pos-panel-title">
            <span>Последние чеки</span>
            <strong>чеки за смену</strong>
          </header>
          <div className="pos-receipt-list">
            {salesRows.slice(0, 4).map((row) => (
              <button
                key={readString(row, 'posSaleId')}
                type="button"
                className={`pos-receipt-row ${readString(row, 'posSaleId') === selectedRefundableSaleId ? 'selected' : ''}`}
                disabled={!canViewSaleDetails || feedback.state === 'pending'}
                onClick={() => {
                  const saleId = readString(row, 'posSaleId');
                  setSelectedRefundSaleId(saleId);
                  void loadSaleDetail(saleId);
                }}
              >
                <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                <strong>{posSaleStateLabel(readString(row, 'state', 'sale'))}</strong>
                <em>{posSaleLineSummary(row)}</em>
                <b>{formatMoney(readMoney(row, 'total'), currencyCode)}</b>
              </button>
            ))}
            {salesRows.length === 0 && (
              <article className="pos-receipt-row">
                <span>—</span>
                <strong>Чеков нет</strong>
                <em>платформа</em>
                <b>0 {currencyCode}</b>
              </article>
            )}
          </div>
          {selectedSaleDetail !== null && (
            <div className="pos-sale-detail">
              <div>
                <span>Детали чека</span>
                <strong>{posSaleStateLabel(readString(selectedSaleDetail, 'state', 'sale'))}</strong>
                <b>{formatMoney(readMoney(selectedSaleDetail, 'total'), currencyCode)}</b>
              </div>
              {readArray(selectedSaleDetail, 'lines').slice(0, 3).map((line) => (
                <p key={`${readString(line, 'productId')}-${readNumber(line, 'quantity', 0)}`}>
                  {readString(line, 'productName', 'Товар')} · {readNumber(line, 'quantity', 0)} × {formatMoney(readMoney(line, 'unitPrice'), currencyCode)}
                </p>
              ))}
              {selectedReceiptDetail !== null && (
                <div className="pos-receipt-detail">
                  <span>Чек платформы</span>
                  <strong>{readString(selectedReceiptDetail, 'receiptNumber', 'чек')}</strong>
                  <b>{formatMoney(readMoney(selectedReceiptDetail, 'total'), currencyCode)}</b>
                  <p>{posReceiptTypeLabel(readString(selectedReceiptDetail, 'receiptType', 'sale'))}</p>
                </div>
              )}
              <div className="pos-receipt-actions">
                <button type="button" disabled={feedback.state === 'pending'} onClick={printSelectedReceipt}>
                  <ReceiptText size={13} />
                  Печать
                </button>
                <button type="button" disabled={feedback.state === 'pending'} onClick={exportSelectedReceipt}>
                  <ArrowRightLeft size={13} />
                  Экспорт
                </button>
              </div>
            </div>
          )}
        </section>

        <section className="pos-panel pos-quick-panel">
          <header className="pos-panel-title">
            <span>Быстрые операции</span>
            <strong>действия ждут подтверждения платформы</strong>
          </header>
          <div className="pos-stock-form">
            <label>
              <span>Списание</span>
              <select
                aria-label="Товар для списания"
                value={selectedStockProduct?.productId ?? ''}
                disabled={backendCatalogProducts.length === 0 || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffProductId(event.currentTarget.value)}
              >
                {backendCatalogProducts.length === 0 && <option value="">Нет товара платформы</option>}
                {backendCatalogProducts.map((product) => (
                  <option key={product.productId} value={product.productId}>
                    {product.name} · {product.stockOnHand} шт.
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>Кол-во</span>
              <input
                aria-label="Количество списания"
                inputMode="numeric"
                value={stockWriteOffQuantity}
                disabled={!canWriteOffStock || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffQuantity(event.currentTarget.value)}
              />
            </label>
            <label className="pos-stock-reason">
              <span>Причина</span>
              <input
                aria-label="Причина списания"
                value={stockWriteOffReason}
                disabled={!canWriteOffStock || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffReason(event.currentTarget.value)}
              />
            </label>
            <button type="button" disabled={!canWriteOffStock || feedback.state === 'pending'} onClick={writeOffStock}>
              <AlertTriangle size={14} />
              Списать
            </button>
          </div>
          <div className="pos-quick-grid">
            {[
              ['Пополнить депозит', selectedPosPlayer ? `корзина ${formatMinorUnits(cartTotalMinorUnits, currencyCode)}` : 'выберите клиента', CircleDollarSign],
              ['Возврат по чеку', 'требует выбранный чек', ReceiptText],
              ['Аннулировать черновик', 'создать и аннулировать чек', X],
              ['Списать склад', selectedStockProduct?.name ?? 'выберите товар', AlertTriangle],
              ['Новый клиент', newPlayerDisplayName || 'заполните имя', UserRoundPlus],
              ['Внести наличные', 'экран платежей', Banknote]
            ].map(([label, detail, Icon]) => (
              <button
                key={label as string}
                type="button"
                className="pos-quick-card"
                disabled={((label as string) === 'Пополнить депозит' && (!canTopUpPosWallet || feedback.state === 'pending'))
                  || ((label as string) === 'Возврат по чеку' && (!canRefundSelectedSale || feedback.state === 'pending'))
                  || ((label as string) === 'Аннулировать черновик' && (!canVoidDraftCart || feedback.state === 'pending'))
                  || ((label as string) === 'Списать склад' && (!canWriteOffStock || feedback.state === 'pending'))
                  || ((label as string) === 'Новый клиент' && (!canCreatePosPlayer || feedback.state === 'pending'))}
                onClick={() => {
                  if ((label as string) === 'Пополнить депозит') {
                    void topUpSelectedPosPlayer();
                  } else if ((label as string) === 'Возврат по чеку') {
                    setFeedback(emptyFeedback);
                    setCriticalAction('refund-sale');
                  } else if ((label as string) === 'Аннулировать черновик') {
                    setFeedback(emptyFeedback);
                    setCriticalAction('void-draft');
                  } else if ((label as string) === 'Списать склад') {
                    void writeOffStock();
                  } else if ((label as string) === 'Новый клиент') {
                    void createPosPlayer();
                  } else {
                    triggerFeedback(setFeedback, label as string);
                  }
                }}
              >
                <Icon size={17} />
                <strong>{label as string}</strong>
                <span>{detail as string}</span>
              </button>
            ))}
          </div>
          {criticalAction === 'refund-sale' && (
            <CriticalActionConfirmation
              title="Подтвердите возврат"
              detail={`Выбранный чек · ${formatMoney(readMoney(selectedRefundableSale, 'total'), currencyCode)}`}
              impact="Платформа создаст возврат по выбранному чеку и запишет причину в аудит."
              confirmLabel="Подтвердить возврат"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void refundLatestSale()}
            >
              <label className="critical-confirmation-field">
                <span>Причина возврата</span>
                <input
                  value={refundReason}
                  disabled={feedback.state === 'pending'}
                  onChange={(event) => setRefundReason(event.currentTarget.value)}
                />
              </label>
            </CriticalActionConfirmation>
          )}
          {criticalAction === 'void-draft' && (
            <CriticalActionConfirmation
              title="Подтвердите аннулирование"
              detail={`Корзина · ${cartItems.length} поз. · ${formatMinorUnits(cartTotalMinorUnits, currencyCode)}`}
              impact="Черновик чека будет создан и аннулирован после подтверждения платформы."
              confirmLabel="Подтвердить аннулирование"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void voidDraftCart()}
            >
              <label className="critical-confirmation-field">
                <span>Причина аннулирования</span>
                <input
                  value={voidReason}
                  disabled={feedback.state === 'pending'}
                  onChange={(event) => setVoidReason(event.currentTarget.value)}
                />
              </label>
            </CriticalActionConfirmation>
          )}
        </section>
      </section>
    </main>
  );
}

function BackendBookingWorkspace({
  floorMap,
  backend,
  onOpenSeat
}: {
  floorMap: OperatorFloorMapState;
  backend: OperatorBackendContext | null;
  onOpenSeat: (seatId: string) => void;
}) {
  const [selectedBookingIndex, setSelectedBookingIndex] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [reservationResult, setReservationResult] = useState<ReservationSearchResultDto | null>(null);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('loading');
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadVersion, setReloadVersion] = useState(0);
  const [draftCustomerName, setDraftCustomerName] = useState('Гость');
  const [draftPhoneNumber, setDraftPhoneNumber] = useState('');
  const [draftStartsAt, setDraftStartsAt] = useState(() => toDateTimeInputValue(addMinutes(new Date(), 15)));
  const [draftDurationMinutes, setDraftDurationMinutes] = useState(60);
  const readySeats = floorMap.seats.filter((seat) => seat.tone === 'ready' && !seat.activeSessionId);
  const activeSeats = floorMap.seats.filter((seat) => seat.tone === 'active' || seat.activeSessionId);
  const problemSeats = floorMap.seats.filter((seat) => problemTones.has(seat.tone));
  const today = new Date();
  const bookingFromUtc = `${toDateInputValue(today)}T00:00:00.000Z`;
  const bookingToUtc = `${toDateInputValue(today)}T23:59:59.999Z`;

  useEffect(() => {
    let disposed = false;

    if (backend === null) {
      setReservationResult(null);
      setLoadStatus('failed');
      setLoadError('Активный филиал не назначен.');
      return undefined;
    }

    setLoadStatus('loading');
    setLoadError(null);

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.reservations.search(backend.branchId, {
      fromUtc: bookingFromUtc,
      toUtc: bookingToUtc,
      limit: 40
    })
      .then((result) => {
        if (disposed) {
          return;
        }

        setReservationResult(result);
        setLoadStatus('backend');
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setReservationResult(null);
        setLoadStatus('failed');
        setLoadError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, bookingFromUtc, bookingToUtc, reloadVersion]);

  const reservations = readArray<Record<string, unknown>>(reservationResult, 'reservations');
  const bookings = reservations.map((reservation) => {
    const state = readString(reservation, 'state', 'pending');
    const source = readString(reservation, 'source', 'operator');
    const startsAtUtc = readString(reservation, 'startsAtUtc');
    const durationMinutes = readNumber(reservation, 'durationMinutes', 60);
    const seatName = readString(reservation, 'seatName', '');
    const zoneName = readString(reservation, 'zoneName', 'Без места');
    const tone = state === 'cancelled'
      ? 'blocking'
      : state === 'seated'
        ? 'confirmed'
        : source === 'online'
          ? 'online'
          : 'pending';

    return {
      reservationId: readString(reservation, 'reservationId'),
      state,
      time: formatTime(startsAtUtc),
      client: readString(reservation, 'customerName', 'Гость'),
      seats: seatName ? '1 ПК' : 'без ПК',
      zone: seatName ? `${zoneName} · ${seatName}` : zoneName,
      duration: `${durationMinutes} мин`,
      status: reservationStateLabel(state),
      tone,
      note: readString(reservation, 'note', readString(reservation, 'phoneNumber', 'без комментария')),
      seatId: readString(reservation, 'seatId'),
      source
    };
  });
  const selectedBooking = bookings[selectedBookingIndex] ?? bookings[0] ?? {
    reservationId: '',
    time: '—',
    client: loadStatus === 'failed' ? 'Брони не загружены' : 'Нет броней за сегодня',
    seats: '0 ПК',
    zone: floorMap.branchName,
    duration: '—',
    status: loadStatus === 'loading' ? 'Загрузка' : 'Пусто',
    tone: 'pending',
    note: loadError ?? 'Свободные места доступны на карте зала',
    seatId: '',
    source: 'operator',
    state: 'empty'
  };
  useEffect(() => {
    if (bookings.length > 0 && selectedBookingIndex >= bookings.length) {
      setSelectedBookingIndex(0);
    }
  }, [bookings.length, selectedBookingIndex]);

  const onlineRequests = bookings.filter((booking) => booking.source === 'online' && booking.state === 'pending');
  const selectedReadySeat = readySeats.find((seat) => seat.id === selectedBooking.seatId) ?? readySeats[0] ?? null;
  const reservationBusy = feedback.state === 'pending';
  const canManageReservations = backend !== null && hasPermission(backend.session, permissionNames.manageReservations);
  const hasSelectedReservation = selectedBooking.reservationId.length > 0;
  const selectedReservationActive = selectedBooking.state !== 'seated' && selectedBooking.state !== 'cancelled' && selectedBooking.state !== 'empty';
  const canCreateReservation = canManageReservations && loadStatus === 'backend' && selectedReadySeat !== null && !reservationBusy;
  const canSeatSelectedReservation = canManageReservations && hasSelectedReservation && selectedReservationActive && selectedBooking.seatId.length > 0 && !reservationBusy;
  const canMoveSelectedReservation = canManageReservations &&
    hasSelectedReservation &&
    selectedReservationActive &&
    readySeats.some((seat) => seat.id !== selectedBooking.seatId) &&
    !reservationBusy;
  const canCancelSelectedReservation = canManageReservations && hasSelectedReservation && selectedReservationActive && !reservationBusy;
  const loadLabel = loadStatus === 'backend'
    ? 'Данные платформы'
    : loadStatus === 'loading'
      ? 'Загрузка броней'
      : 'Ошибка броней';
  const runReservationAction = async (
    label: string,
    operation: (clients: ReturnType<typeof createOperatorApiClients>) => Promise<unknown>,
    afterSuccess?: () => void
  ) => {
    setFeedback({ label, state: 'pending' });

    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.manageReservations)) {
        throw new Error('Нет прав на управление бронями.');
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await operation(clients);
      setFeedback({ label, state: 'confirmed' });
      setReloadVersion((value) => value + 1);
      afterSuccess?.();
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };
  const requireSelectedReservationId = () => {
    if (!selectedBooking.reservationId) {
      throw new Error('Выберите бронь из данных платформы.');
    }

    return selectedBooking.reservationId;
  };
  const createReservation = () => runReservationAction('Создать бронь', async (clients) => {
    const nextBackend = requireBackend(backend);
    if (!selectedReadySeat) {
      throw new Error('Нет свободного места для новой брони.');
    }

    const startsAt = new Date(draftStartsAt);
    if (Number.isNaN(startsAt.getTime())) {
      throw new Error('Укажите корректное время старта брони.');
    }

    if (startsAt.getTime() < Date.now() - 60000) {
      throw new Error('Время старта брони уже прошло.');
    }

    return await clients.reservations.create(nextBackend.branchId, {
      organizationId: nextBackend.session.organizationId,
      seatId: selectedReadySeat.id,
      customerName: draftCustomerName.trim() || 'Гость',
      phoneNumber: draftPhoneNumber.trim() || null,
      startsAtUtc: startsAt.toISOString(),
      durationMinutes: Math.max(15, draftDurationMinutes),
      source: 'operator',
      note: `Создано оператором · ${selectedReadySeat.name}`
    });
  });
  const confirmReservation = (reservationId: string, label: string) => runReservationAction(label, async (clients) => {
    const nextBackend = requireBackend(backend);
    return await clients.reservations.confirm(reservationId, { organizationId: nextBackend.session.organizationId });
  });
  const seatReservation = () => runReservationAction('Посадить бронь', async (clients) => {
    const nextBackend = requireBackend(backend);
    return await clients.reservations.seat(requireSelectedReservationId(), { organizationId: nextBackend.session.organizationId });
  }, () => {
    if (selectedBooking.seatId) {
      onOpenSeat(selectedBooking.seatId);
    }
  });
  const moveReservation = () => runReservationAction('Перенести бронь', async (clients) => {
    const nextBackend = requireBackend(backend);
    const targetSeat = readySeats.find((seat) => seat.id !== selectedBooking.seatId);
    if (!targetSeat) {
      throw new Error('Нет другого свободного места для переноса.');
    }

    return await clients.reservations.update(requireSelectedReservationId(), {
      organizationId: nextBackend.session.organizationId,
      seatId: targetSeat.id,
      note: `Перенесено оператором · ${targetSeat.name}`
    });
  });
  const cancelReservation = () => runReservationAction('Отменить бронь', async (clients) => {
    const nextBackend = requireBackend(backend);
    return await clients.reservations.cancel(requireSelectedReservationId(), {
      organizationId: nextBackend.session.organizationId,
      reason: 'Отменено оператором'
    });
  });

  return (
    <main className="workspace-screen booking-screen">
      <section className="screen-head booking-head">
        <div>
          <span>Брони</span>
          <h1>Брони сегодня · посадка гостей и онлайн-заявки</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{loadLabel}</span>
          <button type="button" className="booking-create-action" disabled={!canCreateReservation} onClick={createReservation}><Plus size={14} />Создать</button>
        </div>
      </section>

      <section className="state-strip booking-state-strip">
        <StateFlag label="Свободно" value={String(readySeats.length)} />
        <StateFlag label="Занято" value={String(activeSeats.length)} />
        <StateFlag label="Проблемы" value={String(problemSeats.length)} critical={problemSeats.length > 0} />
        <StateFlag label="Брони" value={String(bookings.length)} critical={loadStatus === 'failed'} />
        <StateFlag label="Заявки" value={String(onlineRequests.length)} critical={onlineRequests.length > 0} />
      </section>

      <section className="booking-layout">
        <section className="booking-panel booking-timeline-panel">
          <header className="booking-panel-title">
            <span>Лента броней</span>
            <strong>активные брони из платформы</strong>
          </header>
          <div className="booking-list">
            {bookings.map((booking, index) => (
              <button
                key={`${booking.time}-${booking.seatId}`}
                type="button"
                className={`booking-card ${booking.tone}${index === selectedBookingIndex ? ' active' : ''}`}
                onClick={() => setSelectedBookingIndex(index)}
              >
                <span className="booking-time">{booking.time}</span>
                <span className="booking-client">
                  <strong>{booking.client}</strong>
                  <em>{booking.note}</em>
                </span>
                <span className="booking-meta">{booking.seats} · {booking.zone} · {booking.duration}</span>
                <b>{booking.status}</b>
              </button>
            ))}
            {bookings.length === 0 && (
              <article className="booking-card pending">
                <span className="booking-time">—</span>
                <span className="booking-client">
                  <strong>{loadStatus === 'loading' ? 'Загрузка броней' : 'Нет броней'}</strong>
                  <em>{loadError ?? 'На сегодня броней нет.'}</em>
                </span>
                <span className="booking-meta">{floorMap.branchName}</span>
                <b>{loadStatus === 'failed' ? 'Ошибка' : 'Пусто'}</b>
              </article>
            )}
          </div>
        </section>

        <section className="booking-panel booking-selected-panel">
          <header className="booking-panel-title">
            <span>Выбранная бронь</span>
            <strong>{selectedBooking.client} · {selectedBooking.time}</strong>
          </header>
          <div className={`booking-status-card ${selectedBooking.tone}`}>
            <span>{selectedBooking.status}</span>
            <strong>{selectedBooking.time}</strong>
            <em>{selectedBooking.seats} · {selectedBooking.zone} · {selectedBooking.duration}</em>
          </div>
          <div className="booking-action-grid" aria-label="Действия с бронью">
            <button type="button" disabled={!selectedBooking.seatId || reservationBusy} onClick={() => selectedBooking.seatId ? onOpenSeat(selectedBooking.seatId) : setFeedback({ label: 'Открыть карту', state: 'failed', detail: 'У выбранной брони нет места.' })}><MonitorCheck size={15} />Открыть карту</button>
            <button type="button" disabled={!canSeatSelectedReservation} onClick={seatReservation}><UserRoundPlus size={15} />Посадить</button>
            <button type="button" disabled={!canMoveSelectedReservation} onClick={moveReservation}><ArrowRightLeft size={15} />Перенести</button>
            <button type="button" className="danger" disabled={!canCancelSelectedReservation} onClick={cancelReservation}><Square size={15} />Отменить</button>
          </div>
          <FeedbackNotice feedback={feedback} />
          <div className="booking-detail-list">
            <div><span>Клиент</span><strong>{selectedBooking.client}</strong></div>
            <div><span>Комментарий</span><strong>{selectedBooking.note}</strong></div>
            <div><span>Источник</span><strong>{selectedBooking.source === 'online' ? 'онлайн-заявка' : 'оператор'}</strong></div>
          </div>
        </section>

        <section className="booking-panel booking-requests-panel">
          <header className="booking-panel-title">
            <span>Онлайн-заявки</span>
            <strong>заявки в ожидании подтверждения</strong>
          </header>
          <div className="booking-request-list">
            {onlineRequests.map((request) => (
              <article key={request.reservationId} className="booking-request-card">
                <span>{request.time}</span>
                <strong>{request.client}</strong>
                <em>{request.note}</em>
                <div>
                  <button type="button" disabled={!canManageReservations || reservationBusy} onClick={() => confirmReservation(request.reservationId, `Принять ${request.client}`)}>Принять</button>
                  <button type="button" onClick={() => {
                    const index = bookings.findIndex((booking) => booking.reservationId === request.reservationId);
                    if (index >= 0) {
                      setSelectedBookingIndex(index);
                    }
                  }}>Уточнить</button>
                </div>
              </article>
            ))}
            {onlineRequests.length === 0 && (
              <article className="booking-request-card">
                <span>—</span>
                <strong>Нет онлайн-заявок</strong>
                <em>{loadStatus === 'failed' ? loadError ?? 'Не удалось загрузить заявки.' : 'Платформа не вернула заявок в ожидании.'}</em>
              </article>
            )}
          </div>
        </section>

        <section className="booking-panel booking-create-panel">
          <header className="booking-panel-title">
            <span>Новая бронь</span>
            <strong>{selectedReadySeat ? `${selectedReadySeat.zone} · ${selectedReadySeat.name}` : 'нет свободного места'}</strong>
          </header>
          <div className="booking-form-grid">
            <label>Клиент<input value={draftCustomerName} disabled={reservationBusy} onChange={(event) => setDraftCustomerName(event.target.value)} /></label>
            <label>Телефон<input value={draftPhoneNumber} disabled={reservationBusy} onChange={(event) => setDraftPhoneNumber(event.target.value)} /></label>
            <label>Старт<input type="datetime-local" value={draftStartsAt} disabled={reservationBusy} onChange={(event) => setDraftStartsAt(event.target.value)} /></label>
            <label>Длительность<input type="number" min={15} step={15} value={draftDurationMinutes} disabled={reservationBusy} onChange={(event) => setDraftDurationMinutes(Number(event.target.value) || 60)} /></label>
          </div>
          <button type="button" className="booking-primary-action" disabled={!canCreateReservation} onClick={createReservation}>Создать бронь</button>
        </section>
      </section>
    </main>
  );
}

function reservationStateLabel(state: string) {
  switch (state) {
    case 'confirmed':
      return 'Подтверждена';
    case 'pending':
      return 'Ожидает';
    case 'seated':
      return 'Посажен';
    case 'cancelled':
      return 'Отменена';
    default:
      return state || 'Неизвестно';
  }
}

type PlayerClientItem = {
  playerAccountId?: string;
  name: string;
  status: string;
  balanceMinorUnits: number;
  debtMinorUnits: number;
  last: string;
  tone: string;
  detail: string;
  phoneNumber: string;
  source: 'fixture' | 'backend';
};

function fixturePlayers(currencyCode: string): PlayerClientItem[] {
  return [
    { name: 'Madina S.', status: 'VIP', balanceMinorUnits: 46000, debtMinorUnits: 0, last: 'пример', tone: 'vip', detail: 'локальная карточка', phoneNumber: '+992 90 555 22 11', source: 'fixture' },
    { name: 'Amir K.', status: 'Активен', balanceMinorUnits: 12000, debtMinorUnits: 0, last: 'пример', tone: 'active', detail: `120 ${currencyCode}`, phoneNumber: '', source: 'fixture' },
    { name: 'Olim K.', status: 'Долг', balanceMinorUnits: 0, debtMinorUnits: 3500, last: 'пример', tone: 'debt', detail: 'долг после сеанса', phoneNumber: '', source: 'fixture' }
  ];
}

function projectPlayerClient(player: unknown): PlayerClientItem {
  const debt = readNumber(player, 'debtBalanceMinorUnits', 0);
  const packages = readNumber(player, 'activePackageCount', 0);
  const isActive = isRecord(player) && player.isActive !== false;
  return {
    playerAccountId: readString(player, 'playerAccountId') || undefined,
    name: readString(player, 'displayName', 'Игрок'),
    status: debt > 0 ? 'Долг' : packages > 0 ? 'Пакет' : isActive ? 'Активен' : 'Неактивен',
    balanceMinorUnits: readNumber(player, 'walletBalanceMinorUnits', 0),
    debtMinorUnits: debt,
    last: packages > 0 ? `${packages} пак.` : 'платформа',
    tone: debt > 0 ? 'debt' : packages > 0 ? 'vip' : isActive ? 'active' : 'regular',
    detail: `${readString(player, 'phoneNumber', 'без телефона')} · ${packages} пакетов`,
    phoneNumber: readString(player, 'phoneNumber', ''),
    source: 'backend'
  };
}

function BackendPlayersWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [clientSearch, setClientSearch] = useState('');
  const [activeSegment, setActiveSegment] = useState('Все');
  const [selectedClientId, setSelectedClientId] = useState<string | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [clients, setClients] = useState<PlayerClientItem[]>(() => backend === null ? fixturePlayers(currencyCode) : []);
  const [walletSummary, setWalletSummary] = useState<WalletSummaryDto | null>(null);
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [selectedPackageDefinitionId, setSelectedPackageDefinitionId] = useState('');
  const [selectedClientPackages, setSelectedClientPackages] = useState<PlayerPackageDto[]>([]);
  const [walletTopUpAmount, setWalletTopUpAmount] = useState('100.00');
  const [walletTopUpReason, setWalletTopUpReason] = useState('пополнение через кассу');
  const [debtPaymentAmount, setDebtPaymentAmount] = useState('');
  const [debtPaymentReason, setDebtPaymentReason] = useState('оплата долга через кассу');
  const [newPlayerName, setNewPlayerName] = useState('');
  const [newPlayerPhone, setNewPlayerPhone] = useState('');

  useEffect(() => {
    if (backend === null) {
      setLoadStatus('fixture');
      setClients(fixturePlayers(currencyCode));
      setPackageOptions([]);
      setSelectedPackageDefinitionId('');
      return undefined;
    }

    let disposed = false;
    const loadPlayers = async () => {
      setLoadStatus('loading');
      try {
        const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
        const players = await apiClients.players.searchPlayers(backend.branchId, clientSearch, 25);
        const nextPackageOptions = hasPermission(backend.session, permissionNames.viewPackages) || hasPermission(backend.session, permissionNames.purchasePackage)
          ? await apiClients.settings.getPackageOptions(backend.branchId).catch(() => [])
          : [];
        if (disposed) {
          return;
        }

        const nextClients = Array.isArray(players) ? players.map(projectPlayerClient) : [];
        const nextOptions = Array.isArray(nextPackageOptions) ? nextPackageOptions : [];
        setClients(nextClients.length > 0 ? nextClients : []);
        setPackageOptions(nextOptions);
        setSelectedPackageDefinitionId((current) => current && nextOptions.some((option) => readString(option, 'packageDefinitionId') === current)
          ? current
          : readString(nextOptions[0], 'packageDefinitionId'));
        setSelectedClientId((current) => current && nextClients.some((client) => client.playerAccountId === current)
          ? current
          : nextClients[0]?.playerAccountId ?? null);
        setLoadStatus('backend');
      } catch (error) {
        if (!disposed) {
          setLoadStatus('failed');
          setFeedback({ label: 'Клиенты', state: 'failed', detail: projectOperatorError(error).detail });
        }
      }
    };

    const timer = window.setTimeout(() => void loadPlayers(), 180);
    return () => {
      disposed = true;
      window.clearTimeout(timer);
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, clientSearch, currencyCode]);

  const selectedClient = clients.find((client) => client.playerAccountId === selectedClientId)
    ?? clients[0]
    ?? null;

  useEffect(() => {
    if (backend === null || selectedClient === null || !selectedClient.playerAccountId || selectedClient.source !== 'backend') {
      setWalletSummary(null);
      setSelectedClientPackages([]);
      return undefined;
    }

    const client = selectedClient as PlayerClientItem & { playerAccountId: string; source: 'backend' };
    let disposed = false;
    const loadWallet = async () => {
      try {
        const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
        const [wallet, packages] = await Promise.all([
          apiClients.players.getWalletSummary(client.playerAccountId),
          hasPermission(backend.session, permissionNames.viewPackages) || hasPermission(backend.session, permissionNames.purchasePackage)
            ? apiClients.players.getPlayerPackages(client.playerAccountId).catch(() => [])
            : Promise.resolve([])
        ]);
        if (!disposed) {
          setWalletSummary(wallet);
          setSelectedClientPackages(Array.isArray(packages) ? packages : []);
        }
      } catch (error) {
        if (!disposed) {
          setFeedback({ label: client.name, state: 'failed', detail: projectOperatorError(error).detail });
          setSelectedClientPackages([]);
        }
      }
    };

    void loadWallet();
    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedClient?.playerAccountId, selectedClient?.source]);

  const visibleClients = clients.filter((client) => {
    const segmentMatches = activeSegment === 'Все'
      || (activeSegment === 'VIP' && client.tone === 'vip')
      || (activeSegment === 'Есть долг' && client.debtMinorUnits > 0)
      || (activeSegment === 'Новые' && client.source === 'backend')
      || (activeSegment === 'Спящие' && client.status === 'Неактивен');
    const searchMatches = `${client.name} ${client.status} ${client.detail} ${client.last}`.toLowerCase().includes(clientSearch.trim().toLowerCase());
    return segmentMatches && searchMatches;
  });
  const balance = readMoney(walletSummary, 'walletBalance')?.minorUnits ?? selectedClient?.balanceMinorUnits ?? 0;
  const debt = readMoney(walletSummary, 'debtBalance')?.minorUnits ?? selectedClient?.debtMinorUnits ?? 0;
  const recentEntries = readArray(walletSummary, 'recentEntries');
  const selectedClientPackageCount = selectedClientPackages.length || Number.parseInt(selectedClient?.last ?? '', 10) || 0;
  const selectedPackageOption = packageOptions.find((option) => readString(option, 'packageDefinitionId') === selectedPackageDefinitionId)
    ?? packageOptions[0]
    ?? null;
  const selectedPackagePriceMinorUnits = selectedPackageOption === null ? 0 : readNumber(selectedPackageOption, 'priceMinorUnits', 0);
  const selectedPackageCurrencyCode = selectedPackageOption === null ? currencyCode : readString(selectedPackageOption, 'currencyCode', currencyCode);
  const selectedPackageIncludedMinutes = selectedPackageOption === null ? 0 : Math.floor(readNumber(selectedPackageOption, 'includedSeconds', 0) / 60);
  const selectedPackageBonusMinutes = selectedPackageOption === null ? 0 : Math.floor(readNumber(selectedPackageOption, 'bonusSeconds', 0) / 60);
  const selectedPackageTotalMinutes = selectedPackageIncludedMinutes + selectedPackageBonusMinutes;
  const selectedPackageExpiresDays = selectedPackageOption === null ? 0 : readNumber(selectedPackageOption, 'expiresAfterDays', 0);
  const canAffordSelectedPackage = selectedPackageOption !== null && balance >= selectedPackagePriceMinorUnits;
  useEffect(() => {
    if (debt <= 0) {
      setDebtPaymentAmount('');
      return;
    }

    setDebtPaymentAmount((current) => {
      const parsed = parseMoneyInputMinorUnits(current);
      return parsed !== null && parsed > 0 && parsed <= debt ? current : formatMoneyInputMinorUnits(debt);
    });
  }, [debt, selectedClient?.playerAccountId]);

  const canPurchasePackage = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.purchasePackage);
  const canTopUpWallet = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.topUpWallet);
  const canPayDebt = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && debt > 0
    && hasPermission(backend.session, permissionNames.payDebt);
  const canCreatePlayer = backend !== null && hasPermission(backend.session, permissionNames.createPlayerAccount);
  const canCreateClientReservation = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.manageReservations);
  const requireSelectedBackendClient = (): PlayerClientItem & { playerAccountId: string; source: 'backend' } => {
    if (selectedClient === null || selectedClient.source !== 'backend' || !selectedClient.playerAccountId) {
      throw new Error('Выберите игрока платформы перед операцией.');
    }

    return selectedClient as PlayerClientItem & { playerAccountId: string; source: 'backend' };
  };

  const runClientAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);

      if (label === 'Пополнить депозит') {
        if (!hasPermission(nextBackend.session, permissionNames.topUpWallet)) {
          throw new Error('Нет прав на пополнение депозита.');
        }

        const backendClient = requireSelectedBackendClient();

        const topUpMinorUnits = parseMoneyInputMinorUnits(walletTopUpAmount);
        const reason = walletTopUpReason.trim();
        if (topUpMinorUnits === null || !reason) {
          throw new Error('Заполните сумму и причину пополнения депозита.');
        }

        const wallet = await apiClients.players.topUpWallet(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          amount: { currencyCode, minorUnits: topUpMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('wallet-top-up')
        });
        setWalletSummary(wallet);
      } else if (label === 'Списать долг') {
        if (!hasPermission(nextBackend.session, permissionNames.payDebt)) {
          throw new Error('Нет прав на списание долга.');
        }

        const backendClient = requireSelectedBackendClient();

        const debtPaymentMinorUnits = parseMoneyInputMinorUnits(debtPaymentAmount);
        const reason = debtPaymentReason.trim();
        if (debtPaymentMinorUnits === null || !reason || debtPaymentMinorUnits > debt) {
          throw new Error('Заполните сумму долга не больше текущего долга и причину оплаты.');
        }

        const wallet = await apiClients.players.payDebt(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          amount: { currencyCode, minorUnits: debtPaymentMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('debt-payment')
        });
        setWalletSummary(wallet);
      } else if (label === 'Новая карта') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error('Нет прав на создание игрока.');
        }

        const displayName = newPlayerName.trim() || clientSearch.trim();
        if (!displayName) {
          throw new Error('Заполните имя нового клиента.');
        }

        const created = await apiClients.players.createPlayer(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          displayName,
          phoneNumber: newPlayerPhone.trim() || null,
          idempotencyKey: createIdempotencyKey('player-create')
        });
        const createdClient = projectPlayerClient({
          playerAccountId: readString(created, 'playerAccountId'),
          displayName: readString(created, 'displayName', 'Новый клиент'),
          phoneNumber: readString(created, 'phoneNumber'),
          walletBalanceMinorUnits: 0,
          debtBalanceMinorUnits: 0,
          activePackageCount: 0,
          isActive: true
        });
        setClients((items) => [createdClient, ...items]);
        setSelectedClientId(createdClient.playerAccountId ?? null);
        setNewPlayerName('');
        setNewPlayerPhone('');
      } else if (label === 'Купить пакет') {
        if (!hasPermission(nextBackend.session, permissionNames.purchasePackage)) {
          throw new Error('Нет прав на покупку пакетов.');
        }

        const backendClient = requireSelectedBackendClient();

        let packageOption: PackageOptionDto | null = selectedPackageOption;
        if (packageOption === null) {
          const options = await apiClients.settings.getPackageOptions(nextBackend.branchId);
          const nextOptions = Array.isArray(options) ? options : [];
          setPackageOptions(nextOptions);
          setSelectedPackageDefinitionId(readString(nextOptions[0], 'packageDefinitionId'));
          packageOption = Array.isArray(options) ? options[0] ?? null : null;
        }

        const packageDefinitionId = readString(packageOption, 'packageDefinitionId');
        if (!packageDefinitionId) {
          throw new Error('Нет доступного пакета платформы для покупки.');
        }

        const packagePriceMinorUnits = readNumber(packageOption, 'priceMinorUnits', 0);
        if (packagePriceMinorUnits > balance) {
          throw new Error('Недостаточно депозита для выбранного пакета.');
        }

        const purchasedPackage = await apiClients.players.purchasePackage(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          packageDefinitionId,
          idempotencyKey: createIdempotencyKey('package-purchase')
        });
        const [wallet, packages] = await Promise.all([
          apiClients.players.getWalletSummary(backendClient.playerAccountId),
          apiClients.players.getPlayerPackages(backendClient.playerAccountId).catch(() => [purchasedPackage])
        ]);
        setWalletSummary(wallet);
        setSelectedClientPackages(Array.isArray(packages) ? packages : [purchasedPackage]);
      } else if (label === 'Создать бронь') {
        if (!hasPermission(nextBackend.session, permissionNames.manageReservations)) {
          throw new Error('Нет прав на создание брони.');
        }

        const backendClient = requireSelectedBackendClient();

        await apiClients.reservations.create(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          playerAccountId: backendClient.playerAccountId,
          seatId: null,
          customerName: backendClient.name,
          phoneNumber: backendClient.phoneNumber || null,
          startsAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
          durationMinutes: 60,
          source: 'operator',
          note: 'Создано из карточки клиента'
        });
      } else {
        throw new Error('Операция пока не подключена к платформе.');
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  return (
    <main className="workspace-screen clients-screen">
      <section className="screen-head clients-head">
        <div>
          <span>Клиенты</span>
          <h1>Клиенты · поиск, депозит и долги</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Платформа подключена')}</span>
        </div>
      </section>

      <section className="state-strip clients-state-strip" aria-label="Сводка клиентов">
        <StateFlag label="Клиенты" value={String(clients.length)} />
        <StateFlag label="Платформа" value={String(clients.filter((client) => client.source === 'backend').length)} critical={loadStatus !== 'backend'} />
        <StateFlag label="Депозит" value={formatMinorUnits(balance, currencyCode)} />
        <StateFlag label="Долг" value={formatMinorUnits(debt, currencyCode)} critical={debt > 0} />
        <StateFlag label="Пакеты" value={String(selectedClientPackageCount)} />
        <StateFlag label="Записи" value={String(recentEntries.length)} />
      </section>

      <section className="clients-layout">
        <section className="clients-panel clients-list-panel">
          <header className="clients-panel-title">
            <span>Список клиентов</span>
            <strong>поиск по имени, телефону или карте</strong>
          </header>
          <label className="clients-search">
            <Search size={14} />
            <input
              placeholder="Игрок, телефон, карта"
              value={clientSearch}
              onChange={(event) => setClientSearch(event.currentTarget.value)}
            />
          </label>
          <div className="clients-list">
            {visibleClients.length === 0 ? (
              <div className="clients-empty-state">
                <strong>Клиенты не найдены</strong>
                <span>{loadStatus === 'backend' ? 'По текущему поиску клиентов нет.' : 'Подключитесь к платформе, чтобы загрузить клиентов.'}</span>
              </div>
            ) : (
              visibleClients.map((client) => (
                <button
                  key={client.playerAccountId ?? client.name}
                  type="button"
                  className={`client-row ${client.tone}${client.playerAccountId === selectedClient?.playerAccountId ? ' selected' : ''}`}
                  onClick={() => setSelectedClientId(client.playerAccountId ?? null)}
                >
                  <span>{client.status}</span>
                  <div>
                    <strong>{client.name}</strong>
                    <em>{client.detail}</em>
                  </div>
                  <b>{formatMinorUnits(client.balanceMinorUnits, currencyCode)}</b>
                  <small>{client.last}</small>
                </button>
              ))
            )}
          </div>
        </section>

        <section className="clients-panel clients-profile-panel">
          <header className="clients-panel-title">
            <span>Карточка клиента</span>
            <strong>выбранный игрок</strong>
          </header>
          {selectedClient === null ? (
            <div className="client-profile-card empty">
              <div className="client-avatar">--</div>
              <div>
                <span>Нет выбранного клиента</span>
                <strong>Выберите клиента из списка</strong>
                <em>Пустой ответ платформы не подменяется локальной карточкой</em>
              </div>
            </div>
          ) : (
            <div className="client-profile-card">
              <div className="client-avatar">{selectedClient.name.split(' ').map((part) => part[0]).join('').slice(0, 2).toUpperCase()}</div>
              <div>
                <span>{selectedClient.status}</span>
                <strong>{selectedClient.name}</strong>
                <em>{selectedClient.phoneNumber || 'без телефона'} · {dataSourceLabel(selectedClient.source)}</em>
              </div>
            </div>
          )}
          <div className="client-metrics-grid">
            <div><span>Депозит</span><strong>{formatMinorUnits(balance, currencyCode)}</strong></div>
            <div><span>Долг</span><strong>{formatMinorUnits(debt, currencyCode)}</strong></div>
            <div><span>Пакеты</span><strong>{selectedClientPackageCount}</strong></div>
            <div><span>Источник</span><strong>{selectedClient === null ? 'нет клиента' : dataSourceLabel(selectedClient.source)}</strong></div>
          </div>
          <div className="client-package-list" aria-label="Пакеты клиента">
            {selectedClientPackages.slice(0, 3).map((playerPackage) => (
              <article key={readString(playerPackage, 'playerPackageId')} className="client-package-row">
                <strong>{readString(playerPackage, 'name', 'Пакет')}</strong>
                <span>{playerPackageLabel(playerPackage)}</span>
                <b>{readString(playerPackage, 'state', 'active')}</b>
              </article>
            ))}
            {selectedClientPackages.length === 0 && (
              <article className="client-package-row">
                <strong>Нет активных пакетов</strong>
                <span>платформа</span>
                <b>0</b>
              </article>
            )}
          </div>
        </section>

        <section className="clients-panel clients-actions-panel">
          <header className="clients-panel-title">
            <span>Операции</span>
            <strong>денежные операции выполняет платформа</strong>
          </header>
          <div className="clients-money-form">
            <label>Сумма пополнения<input inputMode="decimal" value={walletTopUpAmount} disabled={!canTopUpWallet} onChange={(event) => setWalletTopUpAmount(event.currentTarget.value)} /></label>
            <label>Причина пополнения<input value={walletTopUpReason} disabled={!canTopUpWallet} onChange={(event) => setWalletTopUpReason(event.currentTarget.value)} /></label>
            <label>Сумма долга<input inputMode="decimal" value={debtPaymentAmount} disabled={!canPayDebt} onChange={(event) => setDebtPaymentAmount(event.currentTarget.value)} /></label>
            <label>Причина долга<input value={debtPaymentReason} disabled={!canPayDebt} onChange={(event) => setDebtPaymentReason(event.currentTarget.value)} /></label>
            <label>Имя нового клиента<input value={newPlayerName} disabled={!canCreatePlayer} onChange={(event) => setNewPlayerName(event.currentTarget.value)} /></label>
            <label>Телефон нового клиента<input value={newPlayerPhone} disabled={!canCreatePlayer} onChange={(event) => setNewPlayerPhone(event.currentTarget.value)} /></label>
          </div>
          <div className="clients-package-form">
            <label>
              Пакет для покупки
              <select
                value={selectedPackageOption === null ? '' : readString(selectedPackageOption, 'packageDefinitionId')}
                disabled={!canPurchasePackage || packageOptions.length === 0}
                onChange={(event) => setSelectedPackageDefinitionId(event.currentTarget.value)}
              >
                {packageOptions.length === 0 && <option value="">Нет активных пакетов</option>}
                {packageOptions.map((option) => (
                  <option key={readString(option, 'packageDefinitionId')} value={readString(option, 'packageDefinitionId')}>
                    {packageOptionLabel(option, currencyCode)}
                  </option>
                ))}
              </select>
            </label>
            <div className="clients-package-preview" aria-label="Пакет к покупке">
              <span><strong>Цена</strong><b>{formatMinorUnits(selectedPackagePriceMinorUnits, selectedPackageCurrencyCode)}</b></span>
              <span><strong>Минуты</strong><b>{selectedPackageTotalMinutes}</b></span>
              <span><strong>Бонус</strong><b>{selectedPackageBonusMinutes}</b></span>
              <span><strong>Срок</strong><b>{selectedPackageExpiresDays > 0 ? `${selectedPackageExpiresDays} дн.` : 'без срока'}</b></span>
              <span className={canAffordSelectedPackage ? undefined : 'attention'}><strong>Депозит</strong><b>{canAffordSelectedPackage ? 'достаточно' : 'пополнить'}</b></span>
            </div>
          </div>
          <div className="clients-action-grid">
            {[
              ['Пополнить депозит', `${walletTopUpAmount || '0'} ${currencyCode}`, CircleDollarSign],
              ['Списать долг', debtPaymentAmount ? `${debtPaymentAmount} ${currencyCode}` : 'нет долга', ReceiptText],
              ['Купить пакет', selectedPackageOption ? packageOptionLabel(selectedPackageOption, currencyCode) : 'нет пакетов', TimerReset],
              ['Создать бронь', 'бронь из карточки', CalendarClock],
              ['Новая карта', newPlayerName || 'создать игрока', UserRoundPlus]
            ].map(([label, detail, Icon]) => (
              <button
                key={label as string}
                type="button"
                className="clients-action-card"
                disabled={((label as string) === 'Пополнить депозит' && !canTopUpWallet)
                  || ((label as string) === 'Списать долг' && !canPayDebt)
                  || ((label as string) === 'Купить пакет' && (!canPurchasePackage || packageOptions.length === 0 || !canAffordSelectedPackage))
                  || ((label as string) === 'Создать бронь' && !canCreateClientReservation)
                  || ((label as string) === 'Новая карта' && !canCreatePlayer)}
                onClick={() => runClientAction(label as string)}
              >
                <Icon size={17} />
                <strong>{label as string}</strong>
                <span>{detail as string}</span>
              </button>
            ))}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="clients-panel clients-segments-panel">
          <header className="clients-panel-title">
            <span>Сегменты</span>
            <strong>фильтр по клиентам платформы</strong>
          </header>
          <div className="clients-segment-grid">
            {[
              ['Все', `${clients.length} клиентов`],
              ['VIP', `${clients.filter((client) => client.tone === 'vip').length} клиентов`],
              ['Есть долг', `${clients.filter((client) => client.debtMinorUnits > 0).length} клиентов`],
              ['Спящие', 'неактивные'],
              ['Новые', 'из поиска платформы']
            ].map(([label, detail]) => (
              <button
                key={label}
                type="button"
                className={activeSegment === label ? 'active' : undefined}
                onClick={() => setActiveSegment(label)}
              >
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="clients-panel clients-history-panel">
          <header className="clients-panel-title">
            <span>История клиента</span>
            <strong>последние операции</strong>
          </header>
          <div className="clients-history-list">
            {recentEntries.slice(0, 4).map((entry) => (
              <article key={readString(entry, 'ledgerEntryId')} className="client-history-row">
                <span>{formatTime(readString(entry, 'createdAtUtc'))}</span>
                <strong>{readString(entry, 'entryType', 'ledger')}</strong>
                <b>{formatMoney(readMoney(entry, 'amount'), currencyCode)}</b>
              </article>
            ))}
            {recentEntries.length === 0 && (
              <article className="client-history-row">
                <span>—</span>
                <strong>Операций нет</strong>
                <b>0 {currencyCode}</b>
              </article>
            )}
          </div>
        </section>
      </section>
    </main>
  );
}

type PaymentOperationItem = [string, string, string, string, string, string, Record<string, unknown> | null];

function paymentOperationPlaceholder(
  loadStatus: LoadStatus,
  currencyCode: string,
  loadError: string | null,
  hasSearchMiss: boolean
): PaymentOperationItem {
  if (hasSearchMiss) {
    return ['—', 'Нет совпадений', 'измените поиск или период', 'Поиск', `0 ${currencyCode}`, 'session', null];
  }

  if (loadStatus === 'loading') {
    return ['—', 'Загружаем операции', 'ждём отчёты', 'Отчёты', `0 ${currencyCode}`, 'session', null];
  }

  if (loadStatus === 'failed') {
    return ['—', 'Операции недоступны', loadError ?? 'повторите загрузку или проверьте связь', 'Отчёты', `0 ${currencyCode}`, 'refund', null];
  }

  if (loadStatus === 'backend') {
    return ['—', 'Операций за период нет', 'в отчёте пусто', 'Отчёты', `0 ${currencyCode}`, 'session', null];
  }

  return ['—', 'Локально: операций нет', 'локальные данные без платформы', 'локально', `0 ${currencyCode}`, 'session', null];
}

function buildShiftReconciliationExportJson(report: ReportResultDto, currentShift: ShiftDto | null, currencyCode: string): string {
  const rows = readArray<Record<string, unknown>>(report, 'rows');
  const latestRow = rows[0];
  const stateSource = currentShift ?? latestRow;
  const expectedCash = readMoney(currentShift, 'expectedCash') ?? readMoney(latestRow, 'expectedCash');
  const countedCash = readMoney(currentShift, 'countedCash') ?? readMoney(latestRow, 'countedCash');
  const difference = readMoney(currentShift, 'difference') ?? readMoney(latestRow, 'difference');

  return JSON.stringify({
    summary: {
      generatedAtUtc: new Date().toISOString(),
      shiftState: shiftStateLabel(readString(stateSource, 'state', 'unknown')),
      expectedCash: formatMoney(expectedCash, currencyCode),
      countedCash: countedCash ? formatMoney(countedCash, currencyCode) : 'Не указано',
      difference: formatMoney(difference, currencyCode),
      shiftCount: rows.length
    },
    shifts: rows.map((row, index) => ({
      label: `Смена ${index + 1}`,
      state: shiftStateLabel(readString(row, 'state', 'unknown')),
      openedAt: formatDateTime(readString(row, 'openedAtUtc')),
      closedAt: formatDateTime(readString(row, 'closedAtUtc')),
      startingCash: formatMoney(readMoney(row, 'startingCash'), currencyCode),
      cashMovements: formatMoney(readMoney(row, 'cashMovementsTotal'), currencyCode),
      cashSales: formatMoney(readMoney(row, 'posCashPaymentsTotal'), currencyCode),
      refunds: formatMoney(readMoney(row, 'posRefundsTotal'), currencyCode),
      walletCashImpact: formatMoney(readMoney(row, 'billingCashImpactTotal'), currencyCode),
      expectedCash: formatMoney(readMoney(row, 'expectedCash'), currencyCode),
      countedCash: readMoney(row, 'countedCash') ? formatMoney(readMoney(row, 'countedCash'), currencyCode) : 'Не указано',
      difference: formatMoney(readMoney(row, 'difference'), currencyCode)
    }))
  }, null, 2);
}

function BackendPaymentsWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [paymentSearch, setPaymentSearch] = useState('');
  const [selectedOperationKey, setSelectedOperationKey] = useState('');
  const [selectedMethod, setSelectedMethod] = useState('Наличные');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null);
  const [salesReport, setSalesReport] = useState<ReportResultDto | null>(null);
  const [cashReport, setCashReport] = useState<ReportResultDto | null>(null);
  const [shiftReport, setShiftReport] = useState<ReportResultDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [openStartingCash, setOpenStartingCash] = useState('0.00');
  const [openingNote, setOpeningNote] = useState('Открытие смены');
  const [closeCountedCash, setCloseCountedCash] = useState('');
  const [closingNote, setClosingNote] = useState('Сверка оператором');
  const [cashMovementType, setCashMovementType] = useState('cash_in');
  const [cashMovementAmount, setCashMovementAmount] = useState('10.00');
  const [cashMovementReason, setCashMovementReason] = useState('Размен кассы');
  const [criticalAction, setCriticalAction] = useState<'close-shift' | null>(null);

  const loadPayments = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      setLoadError(null);
      return;
    }

    setLoadStatus('loading');
    setLoadError(null);
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [shift, sales, cash, shifts] = await Promise.all([
        apiClients.shifts.getCurrentShift(nextBackend.branchId),
        apiClients.shifts.getSalesReport(nextBackend.branchId, { limit: 12 }),
        apiClients.shifts.getCashOperationReport(nextBackend.branchId, { limit: 12 }),
        apiClients.shifts.getShiftReport(nextBackend.branchId, { limit: 6 })
      ]);
      setCurrentShift(shift);
      setSalesReport(sales);
      setCashReport(cash);
      setShiftReport(shifts);
      const closeSeed = readMoney(shift, 'countedCash') ?? readMoney(shift, 'expectedCash');
      if (closeSeed !== null) {
        setCloseCountedCash(formatMoneyInputMinorUnits(closeSeed.minorUnits));
      }
      const note = readString(shift, 'closingNote');
      if (note) {
        setClosingNote(note);
      }
      setLoadError(null);
      setLoadStatus('backend');
    } catch (error) {
      const detail = projectOperatorError(error).detail;
      setLoadStatus('failed');
      setLoadError(detail);
      setFeedback({ label: 'Платежи', state: 'failed', detail });
    }
  };

  useEffect(() => {
    void loadPayments();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  const salesRows = readArray<Record<string, unknown>>(salesReport, 'rows');
  const cashRows = readArray<Record<string, unknown>>(cashReport, 'rows');
  const shiftRows = readArray<Record<string, unknown>>(shiftReport, 'rows');
  const operations: PaymentOperationItem[] = [
    ...salesRows.map((row): PaymentOperationItem => [
      formatTime(readString(row, 'createdAtUtc')),
      posSaleStateLabel(readString(row, 'state', 'sale')),
      `Чек · ${posSaleLineSummary(row)}`,
      'Продажа',
      formatMoney(readMoney(row, 'total'), currencyCode),
      readString(row, 'state', 'sale').toLowerCase().includes('refund') ? 'refund' : 'sale',
      row
    ]),
    ...cashRows.map((row): PaymentOperationItem => [
      formatTime(readString(row, 'createdAtUtc')),
      cashOperationTypeLabel(readString(row, 'operationType', 'cash')),
      readString(row, 'reason', readString(row, 'sourceType', 'cash')),
      paymentSourceLabel(readString(row, 'sourceType', 'cash')),
      formatMoney(readMoney(row, 'cashImpact'), currencyCode),
      readNumber(readMoney(row, 'cashImpact'), 'minorUnits', 0) < 0 ? 'refund' : 'deposit',
      row
    ])
  ];
  const operationSearch = paymentSearch.trim().toLowerCase();
  const filteredOperations = operations.filter(([time, type, client, method, total]) => (
    `${time} ${type} ${client} ${method} ${total}`.toLowerCase().includes(paymentSearch.trim().toLowerCase())
  ));
  const visibleOperations = operations.length === 0
    ? [paymentOperationPlaceholder(loadStatus, currencyCode, loadError, false)]
    : filteredOperations.length > 0
      ? filteredOperations
      : [paymentOperationPlaceholder(loadStatus, currencyCode, loadError, operationSearch.length > 0)];
  const selectedOperation = visibleOperations.find(([time, type, client]) => `${time}-${type}-${client}` === selectedOperationKey) ?? visibleOperations[0];
  const selectedOperationSource = selectedOperation[6];
  const selectedOperationIsSale = selectedOperationSource !== null && readString(selectedOperationSource, 'posSaleId').length > 0;
  const selectedOperationScope = selectedOperationSource === null
    ? '—'
    : readString(selectedOperationSource, 'shiftId') ? 'В отчёте смены' : 'Без смены';
  const selectedOperationSourceLabel = selectedOperationSource === null
    ? selectedOperation[3]
    : selectedOperationIsSale
      ? 'Продажа'
      : 'Движение наличных';
  const selectedOperationDetail = selectedOperationIsSale
    ? posSaleLineSummary(selectedOperationSource)
    : readString(selectedOperationSource, 'reason', selectedOperation[2]);
  const grossSales = readMoney(salesReport, 'grossSalesTotal');
  const refunds = readMoney(salesReport, 'refundsTotal');
  const netSales = readMoney(salesReport, 'netSalesTotal');
  const cashIn = readMoney(cashReport, 'cashInTotal');
  const cashOut = readMoney(cashReport, 'cashOutTotal');
  const latestShiftRow = shiftRows[0];
  const expectedCash = readMoney(currentShift, 'expectedCash') ?? readMoney(latestShiftRow, 'expectedCash');
  const countedCash = readMoney(currentShift, 'countedCash') ?? readMoney(latestShiftRow, 'countedCash');
  const difference = readMoney(currentShift, 'difference') ?? readMoney(latestShiftRow, 'difference');
  const currentShiftId = readString(currentShift, 'shiftId');
  const currentShiftState = readString(currentShift, 'state');
  const canOpenShift = backend !== null
    && currentShiftId.length === 0
    && hasPermission(backend.session, permissionNames.openShift);
  const canCloseShift = backend !== null
    && currentShiftId.length > 0
    && currentShiftState === 'open'
    && hasPermission(backend.session, permissionNames.closeShift);
  const canRecordCashMovement = backend !== null
    && currentShiftId.length > 0
    && currentShiftState === 'open'
    && hasPermission(backend.session, permissionNames.manageShiftCash);
  const methods = [
    ['Наличные', cashIn ? formatMinorUnits(cashIn.minorUnits, cashIn.currencyCode) : `0 ${currencyCode}`, 'кассовый отчёт', `${cashRows.length} операций`],
    ['Карта', netSales ? formatMinorUnits(netSales.minorUnits, netSales.currencyCode) : `0 ${currencyCode}`, 'отчёт продаж', `${salesRows.length} чеков`],
    ['Возвраты', refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`, 'возвраты', 'по отчёту'],
    ['Расхождения', difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`, 'закрытие смены', 'по сверке']
  ];

  const runReportAction = async (label: string) => {
    setCriticalAction(null);
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const exportStamp = new Date().toISOString().replace(/[:.]/g, '-');
      if (label === 'Сводка смены') {
        const csv = await apiClients.shifts.exportShiftReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-shift-summary-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Движение кассы') {
        const csv = await apiClients.shifts.exportCashOperationReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-cash-movements-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Список чеков') {
        const csv = await apiClients.shifts.exportSalesReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-check-list-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Открыть смену') {
        if (!hasPermission(nextBackend.session, permissionNames.openShift)) {
          throw new Error('Нет прав на открытие смены.');
        }

        if (currentShiftId) {
          throw new Error('Смена уже открыта.');
        }

        const startingCashMinorUnits = parseNonNegativeMoneyInputMinorUnits(openStartingCash);
        if (startingCashMinorUnits === null) {
          throw new Error('Введите стартовую сумму наличных не ниже нуля.');
        }

        const openedShift = await apiClients.shifts.openShift(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          startingCash: { currencyCode, minorUnits: startingCashMinorUnits },
          openingNote: openingNote.trim(),
          idempotencyKey: createIdempotencyKey('shift-open')
        });
        setCurrentShift(openedShift);
        await loadPayments(nextBackend);
      } else if (label === 'Добавить движение') {
        if (!hasPermission(nextBackend.session, permissionNames.manageShiftCash)) {
          throw new Error('Нет прав на движение наличных.');
        }

        if (!currentShiftId) {
          throw new Error('Нет открытой смены для движения наличных.');
        }

        const cashMovementMinorUnits = parseMoneyInputMinorUnits(cashMovementAmount);
        const reason = cashMovementReason.trim();
        if (cashMovementMinorUnits === null || !reason) {
          throw new Error('Введите сумму больше нуля и причину движения.');
        }

        await apiClients.shifts.recordCashMovement(currentShiftId, {
          organizationId: nextBackend.session.organizationId,
          movementType: cashMovementType,
          amount: { currencyCode, minorUnits: cashMovementMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('shift-cash-movement')
        });
        setCashMovementAmount('10.00');
        setCashMovementReason('Размен кассы');
        await loadPayments(nextBackend);
      } else if (label === 'Подготовить закрытие') {
        if (!hasPermission(nextBackend.session, permissionNames.closeShift)) {
          throw new Error('Нет прав на закрытие смены.');
        }

        if (!currentShiftId) {
          throw new Error('Нет открытой смены для закрытия.');
        }

        const countedCashMinorUnits = parseMoneyInputMinorUnits(closeCountedCash);
        if (countedCashMinorUnits === null) {
          throw new Error('Введите фактическую сумму наличных больше нуля.');
        }

        const closedShift = await apiClients.shifts.closeShift(currentShiftId, {
          organizationId: nextBackend.session.organizationId,
          countedCash: { currencyCode, minorUnits: countedCashMinorUnits },
          closingNote: closingNote.trim(),
          idempotencyKey: createIdempotencyKey('shift-close')
        });
        setCurrentShift(closedShift);
      } else if (label === 'Сверка смены') {
        const report = await apiClients.shifts.getShiftReport(nextBackend.branchId, { limit: 20 });
        downloadTextFile(`afk4-shift-reconciliation-${exportStamp}.json`, buildShiftReconciliationExportJson(report, currentShift, currencyCode), 'application/json;charset=utf-8');
      } else {
        await apiClients.shifts.getShiftReport(nextBackend.branchId, { limit: 20 });
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  return (
    <main className="workspace-screen payments-screen">
      <section className="screen-head payments-head">
        <div>
          <span>Платежи</span>
          <h1>Платежи · касса смены и сверка</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Отчёты загружены')}</span>
        </div>
      </section>

      <section className="state-strip payments-state-strip" aria-label="Сводка платежей">
        <StateFlag label="Выручка" value={grossSales ? formatMinorUnits(grossSales.minorUnits, grossSales.currencyCode) : `0 ${currencyCode}`} />
        <StateFlag label="Наличные" value={cashIn ? formatMinorUnits(cashIn.minorUnits, cashIn.currencyCode) : `0 ${currencyCode}`} />
        <StateFlag label="Возвраты" value={refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`} critical={(refunds?.minorUnits ?? 0) > 0} />
        <StateFlag label="Смена" value={shiftStateLabel(readString(currentShift, 'state', 'нет смены'))} critical={currentShift === null} />
        <StateFlag label="К сверке" value={difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`} critical={(difference?.minorUnits ?? 0) !== 0} />
      </section>

      <section className="payments-layout">
        <section className="payments-panel payments-ledger-panel">
          <header className="payments-panel-title">
            <span>Операции смены</span>
            <strong>продажи, возвраты и наличные</strong>
          </header>
          <label className="payments-search">
            <Search size={14} />
            <input
              placeholder="Клиент, чек, ПК, сумма"
              value={paymentSearch}
              onChange={(event) => setPaymentSearch(event.currentTarget.value)}
            />
          </label>
          <div className="payments-ledger-list">
            {visibleOperations.map(([time, type, client, method, total, tone]) => (
              <button
                key={`${time}-${type}-${client}`}
                type="button"
                className={`payment-operation-row ${tone}${`${time}-${type}-${client}` === selectedOperationKey ? ' active' : ''}`}
                onClick={() => setSelectedOperationKey(`${time}-${type}-${client}`)}
              >
                <span>{time}</span>
                <div>
                  <strong>{type}</strong>
                  <em>{client}</em>
                </div>
                <small>{method}</small>
                <b>{total}</b>
              </button>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-summary-panel">
          <header className="payments-panel-title">
            <span>Итоги смены</span>
            <strong>выручка и выбранная операция</strong>
          </header>
          <div className="payments-total-card">
            <span>Всего · выбрано {selectedOperation[0]}</span>
            <strong>{netSales ? formatMinorUnits(netSales.minorUnits, netSales.currencyCode) : `0 ${currencyCode}`}</strong>
            <em>{selectedOperation[1]} · {selectedOperation[2]} · {selectedOperation[4]}</em>
          </div>
          <div className="payments-operation-detail" aria-label="Детали выбранной операции">
            <div><span>Операция</span><strong>{selectedOperation[1]}</strong></div>
            <div><span>Смена</span><strong>{selectedOperationScope}</strong></div>
            <div><span>Тип</span><strong>{selectedOperationSourceLabel}</strong></div>
            <div><span>Деталь</span><strong>{selectedOperationDetail}</strong></div>
          </div>
          <div className="payments-metric-grid">
            <div><span>Чеков</span><strong>{salesRows.length}</strong></div>
            <div><span>Наличные</span><strong>{cashRows.length}</strong></div>
            <div><span>Возвраты</span><strong>{refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`}</strong></div>
            <div><span>Смены</span><strong>{shiftRows.length}</strong></div>
          </div>
        </section>

        <section className="payments-panel payments-reconcile-panel">
          <header className="payments-panel-title">
            <span>Сверка кассы</span>
            <strong>проверка перед закрытием</strong>
          </header>
          <div className="payments-open-form">
            <label>Старт наличных<input inputMode="decimal" value={openStartingCash} disabled={!canOpenShift} onChange={(event) => setOpenStartingCash(event.currentTarget.value)} /></label>
            <label>Открытие<input value={openingNote} disabled={!canOpenShift} onChange={(event) => setOpeningNote(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canOpenShift} onClick={() => runReportAction('Открыть смену')}>Открыть смену</button>
          </div>
          <div className="payments-reconcile-list">
            <div><span>Ожидается</span><strong>{expectedCash ? formatMinorUnits(expectedCash.minorUnits, expectedCash.currencyCode) : `0 ${currencyCode}`}</strong></div>
            <div><span>Посчитано</span><strong>{countedCash ? formatMinorUnits(countedCash.minorUnits, countedCash.currencyCode) : 'не закрыта'}</strong></div>
            <div className={(difference?.minorUnits ?? 0) !== 0 ? 'attention' : undefined}><span>Расхождение</span><strong>{difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`}</strong></div>
          </div>
          <div className="payments-close-form">
            <label>Факт в кассе<input inputMode="decimal" value={closeCountedCash} disabled={!canCloseShift} onChange={(event) => setCloseCountedCash(event.currentTarget.value)} /></label>
            <label>Комментарий<input value={closingNote} disabled={!canCloseShift} onChange={(event) => setClosingNote(event.currentTarget.value)} /></label>
          </div>
          <button
            type="button"
            className="payments-primary-action"
            disabled={!canCloseShift || feedback.state === 'pending'}
            onClick={() => {
              setFeedback(emptyFeedback);
              setCriticalAction('close-shift');
            }}
          >
            Подготовить закрытие
          </button>
          {criticalAction === 'close-shift' && (
            <CriticalActionConfirmation
              title="Подтвердите закрытие смены"
              detail={`Факт ${closeCountedCash || '0'} ${currencyCode} · ожидается ${expectedCash ? formatMinorUnits(expectedCash.minorUnits, expectedCash.currencyCode) : `0 ${currencyCode}`}`}
              impact="После подтверждения смена будет закрыта, новые продажи потребуют открытия следующей смены."
              confirmLabel="Закрыть смену"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runReportAction('Подготовить закрытие')}
            />
          )}
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="payments-panel payments-methods-panel">
          <header className="payments-panel-title">
            <span>Методы оплаты</span>
            <strong>структура отчёта</strong>
          </header>
          <div className="payments-method-grid">
            {methods.map(([label, total, share, detail]) => (
              <button
                key={label}
                type="button"
                className={`payment-method-card${selectedMethod === label ? ' active' : ''}`}
                onClick={() => setSelectedMethod(label)}
              >
                <strong>{label}</strong>
                <b>{total}</b>
                <span>{share} · {detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="payments-panel payments-cash-panel">
          <header className="payments-panel-title">
            <span>Движение наличных</span>
            <strong>кассовые движения</strong>
          </header>
          <div className="payments-cash-list">
            {cashRows.slice(0, 4).map((row) => (
              <article key={readString(row, 'operationId')} className="payment-cash-row">
                <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                <strong>{cashOperationTypeLabel(readString(row, 'operationType', 'cash'))}</strong>
                <b>{formatMoney(readMoney(row, 'cashImpact'), currencyCode)}</b>
              </article>
            ))}
          </div>
          <div className="payments-cash-form">
            <label>Тип
              <select value={cashMovementType} disabled={!canRecordCashMovement} onChange={(event) => setCashMovementType(event.currentTarget.value)}>
                <option value="cash_in">Внесение</option>
                <option value="cash_out">Изъятие</option>
              </select>
            </label>
            <label>Сумма<input inputMode="decimal" value={cashMovementAmount} disabled={!canRecordCashMovement} onChange={(event) => setCashMovementAmount(event.currentTarget.value)} /></label>
            <label className="payments-cash-reason">Причина<input value={cashMovementReason} disabled={!canRecordCashMovement} onChange={(event) => setCashMovementReason(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canRecordCashMovement} onClick={() => runReportAction('Добавить движение')}>Добавить движение</button>
          </div>
        </section>

        <section className="payments-panel payments-export-panel">
          <header className="payments-panel-title">
            <span>Отчёты</span>
            <strong>файлы для сверки</strong>
          </header>
          <div className="payments-export-grid">
            {[
              ['Сводка смены', ReceiptText],
              ['Движение кассы', Banknote],
              ['Список чеков', ArrowRightLeft],
              ['Сверка смены', ShieldAlert]
            ].map(([label, Icon]) => (
              <button key={label as string} type="button" onClick={() => runReportAction(label as string)}><Icon size={16} />{label as string}</button>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}

type LogEventKind = 'audit' | 'commandFailure' | 'updateFailure' | 'staleDevice' | 'placeholder';
type LogEventTone = 'audit' | 'device' | 'money' | 'session' | 'warning';
type LogEventItem = [string, string, string, string, LogEventTone, LogEventKind, Record<string, unknown> | null];

function logEventPlaceholder(loadStatus: LoadStatus, loadError: string | null, hasSearchMiss: boolean): LogEventItem {
  if (hasSearchMiss) {
    return ['—', 'Нет совпадений', 'измените поиск или фильтр', 'Оператор', 'audit', 'placeholder', null];
  }

  if (loadStatus === 'loading') {
    return ['—', 'Загружаем события', 'ждём аудит и диагностику', 'Платформа', 'audit', 'placeholder', null];
  }

  if (loadStatus === 'failed') {
    return ['—', 'События недоступны', loadError ?? 'повторите загрузку или проверьте связь', 'Платформа', 'warning', 'placeholder', null];
  }

  if (loadStatus === 'backend') {
    return ['—', 'Событий за период нет', 'аудит и диагностика вернули пустой результат', 'Платформа', 'audit', 'placeholder', null];
  }

  return ['—', 'Локально: событий нет', 'локальные данные без платформы', 'Платформа', 'audit', 'placeholder', null];
}

function mapAuditRecordsToLogEvents(auditRecords: Record<string, unknown>[]): LogEventItem[] {
  return auditRecords.map((record): LogEventItem => {
    const outcome = readString(record, 'outcome');

    return [
      formatTime(readString(record, 'createdAtUtc')),
      auditActionLabel(readString(record, 'action')),
      `${auditTargetLabel(readString(record, 'targetType'))} · ${auditOutcomeLabel(outcome)}`,
      auditSourceLabel(readString(record, 'sourceApp')),
      outcome.toLowerCase().includes('denied') || outcome.toLowerCase().includes('failed') ? 'warning' : 'audit',
      'audit',
      record
    ];
  });
}

function auditActionLabel(action: string): string {
  const normalized = action.toLowerCase();
  switch (normalized) {
    case 'pos.sale.create':
    case 'pos.sales.create':
      return 'Продажа создана';
    case 'pos.sale.refund':
    case 'pos.sales.refund':
      return 'Возврат по чеку';
    case 'pos.sale.void':
    case 'pos.sales.void':
      return 'Чек аннулирован';
    case 'sessions.start':
    case 'session.start':
      return 'Сессия запущена';
    case 'sessions.extend':
    case 'session.extend':
      return 'Сессия продлена';
    case 'sessions.end':
    case 'session.end':
      return 'Сессия завершена';
    case 'identity.staff.create':
      return 'Сотрудник добавлен';
    case 'identity.staff.roles.update':
      return 'Роли сотрудника изменены';
    case 'updates.rollouts.view':
      return 'Проверка публикаций обновлений';
    case 'updates.rollouts.state.change':
      return 'Состояние публикации обновления изменено';
    default:
      if (normalized.includes('pos')) {
        return 'Операция кассы';
      }

      if (normalized.includes('session')) {
        return 'Операция сессии';
      }

      if (normalized.includes('device')) {
        return 'Операция ПК';
      }

      if (normalized.includes('shift')) {
        return 'Операция смены';
      }

      if (normalized.includes('identity') || normalized.includes('staff')) {
        return 'Операция сотрудника';
      }

      if (normalized.includes('update')) {
        return 'Операция обновления';
      }

      return action ? 'Операция платформы' : 'Запись аудита';
  }
}

function auditOutcomeLabel(outcome: string): string {
  switch (outcome.toLowerCase()) {
    case 'succeeded':
    case 'success':
    case 'ok':
      return 'успешно';
    case 'failed':
    case 'failure':
      return 'ошибка';
    case 'denied':
    case 'rejected':
      return 'отказ';
    case 'pending':
      return 'ожидает';
    default:
      return outcome ? 'состояние неизвестно' : 'неизвестно';
  }
}

function auditTargetLabel(targetType: string): string {
  const normalized = targetType.toLowerCase();
  if (normalized.includes('pos') || normalized.includes('sale') || normalized.includes('receipt')) {
    return 'Чек';
  }

  if (normalized.includes('session')) {
    return 'Сессия';
  }

  if (normalized.includes('device')) {
    return 'ПК';
  }

  if (normalized.includes('staff') || normalized.includes('identity') || normalized.includes('user')) {
    return 'Сотрудник';
  }

  if (normalized.includes('shift')) {
    return 'Смена';
  }

  if (normalized.includes('payment') || normalized.includes('ledger') || normalized.includes('wallet')) {
    return 'Платёж';
  }

  if (normalized.includes('tariff')) {
    return 'Тариф';
  }

  if (normalized.includes('package')) {
    return 'Пакет';
  }

  if (normalized.includes('update') || normalized.includes('rollout')) {
    return 'Обновление';
  }

  if (normalized.includes('branch')) {
    return 'Филиал';
  }

  return targetType ? 'Объект' : 'Объект';
}

function auditSourceLabel(sourceApp: string): string {
  const normalized = sourceApp.toLowerCase();
  if (!normalized || normalized === 'audit' || normalized.includes('platform')) {
    return 'Платформа';
  }

  if (normalized.includes('operator')) {
    return 'Приложение оператора';
  }

  if (normalized.includes('agent')) {
    return 'Агент';
  }

  if (normalized.includes('setup')) {
    return 'Мастер установки';
  }

  return 'Платформа';
}

function auditActorLabel(record: Record<string, unknown>, backend: OperatorBackendContext | null): string {
  const actorStaffUserId = readString(record, 'actorStaffUserId');
  if (!actorStaffUserId) {
    return 'Система';
  }

  if (backend?.session.staffUserId.toLowerCase() === actorStaffUserId.toLowerCase()) {
    return operatorDisplayNameLabel(backend.session.displayName);
  }

  return 'Сотрудник';
}

function auditDetailKeyLabel(key: string): string {
  const normalized = key.toLowerCase();
  if (normalized.includes('reason')) {
    return 'Причина';
  }

  if (normalized.includes('amount') || normalized.includes('money') || normalized.includes('price')) {
    return 'Сумма';
  }

  if (normalized.includes('currency')) {
    return 'Валюта';
  }

  if (normalized.includes('status') || normalized.includes('state') || normalized.includes('outcome')) {
    return 'Состояние';
  }

  if (normalized.includes('device') || normalized.includes('machine')) {
    return 'ПК';
  }

  if (normalized.includes('session')) {
    return 'Сессия';
  }

  if (normalized.includes('sale') || normalized.includes('receipt') || normalized.includes('pos')) {
    return 'Чек';
  }

  if (normalized.includes('shift')) {
    return 'Смена';
  }

  if (normalized.includes('tariff')) {
    return 'Тариф';
  }

  if (normalized.includes('package')) {
    return 'Пакет';
  }

  if (normalized.includes('staff') || normalized.includes('user')) {
    return 'Сотрудник';
  }

  if (normalized.includes('branch')) {
    return 'Филиал';
  }

  return 'Параметр';
}

function auditDetailValueLabel(value: unknown): string {
  if (value === null || value === undefined) {
    return 'не указано';
  }

  if (typeof value === 'boolean') {
    return value ? 'да' : 'нет';
  }

  if (typeof value === 'number') {
    return String(value);
  }

  if (Array.isArray(value)) {
    return `${value.length} ${pluralRu(value.length, ['значение', 'значения', 'значений'])}`;
  }

  if (isRecord(value)) {
    return 'заполнено';
  }

  const trimmed = String(value).trim();
  if (trimmed.length === 0) {
    return 'не указано';
  }

  if (isGuid(trimmed)) {
    return 'указано';
  }

  if (/^https?:\/\//i.test(trimmed)) {
    return 'ссылка';
  }

  if (/^\d{4}-\d{2}-\d{2}T/.test(trimmed)) {
    return formatTime(trimmed);
  }

  return trimmed.length > 80 ? `${trimmed.slice(0, 80)}…` : trimmed;
}

function mapDiagnosticsToLogEvents(diagnostics: BranchDiagnosticsDto | null): LogEventItem[] {
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const recentCommandFailures = readArray<Record<string, unknown>>(commandSummary, 'recentFailures');
  const recentUpdateFailures = readArray<Record<string, unknown>>(updateSummary, 'recentFailures');
  const staleDevices = readArray<Record<string, unknown>>(diagnostics, 'staleDevices');

  return [
    ...recentCommandFailures.map((failure): LogEventItem => [
      formatTime(readString(failure, 'updatedAtUtc')),
      `${readString(failure, 'machineName', 'Устройство')} ${commandTypeLabel(readString(failure, 'type', 'command'))}`,
      commandStatusMessageLabel(readString(failure, 'message', readString(failure, 'status', 'failed'))),
      'Агент',
      'device',
      'commandFailure',
      failure
    ]),
    ...recentUpdateFailures.map((failure): LogEventItem => [
      formatTime(readString(failure, 'updatedAtUtc')),
      `${updateComponentLabel(readString(failure, 'component', 'Обновление'))} ${readString(failure, 'targetVersion', '')}`.trim(),
      commandStatusMessageLabel(readString(failure, 'message', readString(failure, 'status', 'failed'))),
      'Обновления',
      'warning',
      'updateFailure',
      failure
    ]),
    ...staleDevices.map((device): LogEventItem => [
      formatTime(readString(device, 'lastHeartbeatAtUtc')),
      `${readString(device, 'machineName', 'Устройство')} не отвечает`,
      `${readNumber(device, 'lastHeartbeatAgeSeconds', 0)} сек. без сигнала`,
      'Агент',
      'warning',
      'staleDevice',
      device
    ])
  ];
}

function compactAuditDetails(detailsJson: string): string {
  const trimmed = detailsJson.trim();
  if (trimmed.length === 0 || trimmed === '{}') {
    return 'нет подробностей';
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    if (!isRecord(parsed)) {
      return auditDetailValueLabel(parsed);
    }

    const entries = Object.entries(parsed)
      .filter(([, value]) => value !== null && value !== undefined && String(value).trim().length > 0)
      .slice(0, 3)
      .map(([key, value]) => `${auditDetailKeyLabel(key)}: ${auditDetailValueLabel(value)}`);

    return entries.length > 0 ? entries.join(' · ') : 'нет подробностей';
  } catch {
    return 'подробности в свободном формате';
  }
}

function logEventKey(event: LogEventItem): string {
  const record = event[6];
  const recordId = readString(record, 'auditRecordId')
    || readString(record, 'commandId')
    || readString(record, 'updateRolloutId')
    || readString(record, 'deviceId')
    || `${event[0]}-${event[1]}`;

  return `${event[5]}-${recordId}-${event[0]}-${event[1]}`;
}

function buildLogEventDetailRows(event: LogEventItem, backend: OperatorBackendContext | null): Array<[string, string]> {
  const record = event[6];

  if (event[5] === 'audit' && record !== null) {
    const targetType = readString(record, 'targetType');

    return [
      ['Событие', auditActionLabel(readString(record, 'action'))],
      ['Результат', auditOutcomeLabel(readString(record, 'outcome'))],
      ['Раздел', auditTargetLabel(targetType)],
      ['Исполнитель', auditActorLabel(record, backend)],
      ['Источник', auditSourceLabel(readString(record, 'sourceApp'))],
      ['Подробности', compactAuditDetails(readString(record, 'detailsJson'))]
    ];
  }

  if (event[5] === 'commandFailure' && record !== null) {
    return [
      ['Устройство', readString(record, 'machineName', 'Устройство')],
      ['Команда', commandTypeLabel(readString(record, 'type', 'command'))],
      ['Статус', commandStatusLabel(readString(record, 'status', 'failed'))],
      ['Сообщение', commandStatusMessageLabel(readString(record, 'message')) || 'нет сообщения'],
      ['Обновлено', readString(record, 'updatedAtUtc', event[0])]
    ];
  }

  if (event[5] === 'updateFailure' && record !== null) {
    return [
      ['Устройство', readString(record, 'machineName', 'Устройство')],
      ['Компонент', `${updateComponentLabel(readString(record, 'component', 'Обновление'))} ${readString(record, 'targetVersion')}`.trim()],
      ['Статус', commandStatusLabel(readString(record, 'status', 'failed'))],
      ['Сообщение', commandStatusMessageLabel(readString(record, 'message')) || 'нет сообщения']
    ];
  }

  if (event[5] === 'staleDevice' && record !== null) {
    return [
      ['Устройство', readString(record, 'machineName', 'Устройство')],
      ['Версия агента', readString(record, 'agentVersion', 'неизвестно')],
      ['Версия оболочки', readString(record, 'shellVersion', 'неизвестно')],
      ['Последний сигнал', readString(record, 'lastHeartbeatAtUtc', event[0])],
      ['Пауза связи', `${readNumber(record, 'lastHeartbeatAgeSeconds', 0)} сек.`]
    ];
  }

  return [
    ['Источник', event[3]],
    ['Событие', event[1]],
    ['Оператор', operatorDisplayNameLabel(backend?.session.displayName ?? 'система')],
    ['Результат', event[2]]
  ];
}

function logToneLabel(tone: LogEventTone): string {
  switch (tone) {
    case 'warning':
      return 'требует внимания';
    case 'device':
      return 'ПК и связь';
    case 'money':
      return 'касса';
    case 'session':
      return 'сессия';
    default:
      return 'запись';
  }
}

function logKindLabel(kind: LogEventKind): string {
  switch (kind) {
    case 'commandFailure':
      return 'команда ПК';
    case 'updateFailure':
      return 'обновление';
    case 'staleDevice':
      return 'связь с ПК';
    case 'placeholder':
      return 'пустой результат';
    default:
      return 'аудит';
  }
}

function buildLogsExportJson(
  branchId: string,
  auditRecords: Record<string, unknown>[],
  diagnostics: BranchDiagnosticsDto | null,
  events: LogEventItem[],
  backend: OperatorBackendContext | null
): string {
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const deviceSummary = isRecord(diagnostics) ? diagnostics.deviceSummary : null;
  return JSON.stringify({
    exportedAtUtc: new Date().toISOString(),
    branch: branchId ? 'текущий филиал' : 'филиал не выбран',
    summary: {
      events: events.length,
      auditRecords: auditRecords.length,
      warnings: events.filter((event) => event[4] === 'warning').length,
      devices: {
        total: readNumber(deviceSummary, 'totalDevices', 0),
        online: readNumber(deviceSummary, 'onlineDevices', 0),
        stale: readNumber(deviceSummary, 'staleDevices', 0)
      },
      commands: {
        pending: readNumber(commandSummary, 'pendingCommands', 0),
        failed: readNumber(commandSummary, 'failedCommands', 0)
      },
      updates: {
        failedDevices: readNumber(updateSummary, 'failedDevices', 0),
        rollbackDevices: readNumber(updateSummary, 'rollbackDevices', 0)
      }
    },
    events: events.map(([time, title, detail, source, tone, kind, record]) => ({
      time,
      title,
      detail,
      source,
      result: logToneLabel(tone),
      section: logKindLabel(kind),
      details: Object.fromEntries(buildLogEventDetailRows([time, title, detail, source, tone, kind, record], backend))
    }))
  }, null, 2);
}

function matchesLogSource(event: LogEventItem, sourceFilter: string): boolean {
  if (sourceFilter === 'Все') {
    return true;
  }

  const [, title, detail, source, tone, kind, record] = event;
  const normalizedTitle = title.toLowerCase();
  const normalizedDetail = detail.toLowerCase();
  const normalizedSource = source.toLowerCase();
  const action = readString(record, 'action').toLowerCase();
  const targetType = readString(record, 'targetType').toLowerCase();
  const isAgent = source === 'Агент' || tone === 'device' || kind === 'commandFailure' || kind === 'staleDevice';
  const isPos = normalizedTitle.includes('касс') || normalizedTitle.includes('чек') || normalizedDetail.includes('касс') || normalizedDetail.includes('чек') || action.includes('pos') || targetType.includes('pos');
  const isOperator = normalizedSource.includes('operator') || normalizedSource.includes('оператор') || normalizedTitle.includes('identity') || normalizedDetail.includes('staff') || action.includes('identity') || targetType.includes('staff');
  const isPlatform = source === 'Обновления' || kind === 'updateFailure' || (!isAgent && !isPos && !isOperator);

  if (sourceFilter === 'Агент') {
    return isAgent;
  }

  if (sourceFilter === 'Касса') {
    return isPos;
  }

  if (sourceFilter === 'Оператор') {
    return isOperator;
  }

  if (sourceFilter === 'Платформа') {
    return isPlatform;
  }

  return true;
}

type AuditSearchOverrides = {
  action?: string | null;
  outcome?: string | null;
  targetType?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  limit?: number | null;
};
type AuditPeriodPreset = 'today' | 'last24h' | 'last7d';

const auditPeriodPresetOptions: Array<[string, AuditPeriodPreset]> = [
  ['Сегодня', 'today'],
  ['24 часа', 'last24h'],
  ['7 дней', 'last7d']
];

function auditPeriodPresetRange(preset: AuditPeriodPreset, now = new Date()): Pick<AuditSearchOverrides, 'fromUtc' | 'toUtc'> {
  const toUtc = now.toISOString();
  if (preset === 'today') {
    return {
      fromUtc: new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())).toISOString(),
      toUtc
    };
  }

  const hours = preset === 'last24h' ? 24 : 24 * 7;
  return {
    fromUtc: new Date(now.getTime() - hours * 60 * 60 * 1000).toISOString(),
    toUtc
  };
}

type ReviewSegment = 'queue' | 'audit';

function reviewActionTypeLabel(actionType: string): string {
  switch (actionType) {
    case 'refund':
      return 'Возврат';
    case 'manual_correction':
      return 'Коррекция';
    case 'debt_write_off':
      return 'Списание долга';
    default:
      return actionType;
  }
}

function reviewExpiryBadge(expiresAtUtc: string, nowMs: number): { label: string; tone: 'overdue' | 'soon' } | null {
  const expiresMs = Date.parse(expiresAtUtc);
  if (!Number.isFinite(expiresMs)) {
    return null;
  }
  const remainingMs = expiresMs - nowMs;
  if (remainingMs <= 0) {
    return { label: 'Просрочена', tone: 'overdue' };
  }
  if (remainingMs <= 2 * 60 * 60 * 1000) {
    return { label: 'Истекает скоро', tone: 'soon' };
  }
  return null;
}

function ReviewWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [activeSegment, setActiveSegment] = useState<ReviewSegment>('queue');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [loadError, setLoadError] = useState<string | null>(null);

  const [requests, setRequests] = useState<MoneyActionRequestDto[]>([]);
  const [staffNames, setStaffNames] = useState<Record<string, string>>({});
  const [rejectingId, setRejectingId] = useState('');
  const [decisionReason, setDecisionReason] = useState('');

  const [auditResult, setAuditResult] = useState<AuditSearchResultDto | null>(null);
  const [auditActor, setAuditActor] = useState('');
  const [auditMinAmount, setAuditMinAmount] = useState('');
  const [auditMaxAmount, setAuditMaxAmount] = useState('');

  const resolveStaffName = (staffUserId: string) =>
    staffNames[staffUserId.toLowerCase()] ?? `${staffUserId.slice(0, 8)}…`;

  const reviewAuditActorLabel = (record: Record<string, unknown>) => {
    const actorStaffUserId = readString(record, 'actorStaffUserId');
    const resolved = actorStaffUserId ? staffNames[actorStaffUserId.toLowerCase()] : '';
    return resolved || auditActorLabel(record, backend);
  };

  const loadQueue = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      setLoadError(null);
      return;
    }
    setLoadStatus('loading');
    setLoadError(null);
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [feed, staff] = await Promise.all([
        apiClients.moneyActions.listPending(nextBackend.branchId),
        apiClients.settings.getStaffUsers(nextBackend.branchId)
      ]);
      setRequests(readArray<MoneyActionRequestDto>(feed, 'requests'));
      const names: Record<string, string> = {};
      for (const user of staff) {
        names[readString(user, 'staffUserId').toLowerCase()] = operatorDisplayNameLabel(readString(user, 'displayName'));
      }
      setStaffNames(names);
      setLoadStatus('backend');
    } catch (error) {
      const detail = projectOperatorError(error).detail;
      setLoadStatus('failed');
      setLoadError(detail);
      setFeedback({ label: 'Проверка', state: 'failed', detail });
    }
  };

  useEffect(() => {
    void loadQueue();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const approveRequest = async (request: MoneyActionRequestDto) => {
    setFeedback({ label: 'Одобрение', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.moneyActions.approve(nextBackend.branchId, request.moneyActionRequestId, { decisionReason: null });
      setFeedback({ label: 'Одобрение', state: 'confirmed' });
      await loadQueue();
    } catch (error) {
      setFeedback({ label: 'Одобрение', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const confirmReject = async (request: MoneyActionRequestDto) => {
    const reason = decisionReason.trim();
    if (reason.length === 0) {
      setFeedback({ label: 'Отклонение', state: 'failed', detail: 'Укажите причину отклонения.' });
      return;
    }
    setFeedback({ label: 'Отклонение', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.moneyActions.reject(nextBackend.branchId, request.moneyActionRequestId, { decisionReason: reason });
      setRejectingId('');
      setDecisionReason('');
      setFeedback({ label: 'Отклонение', state: 'confirmed' });
      await loadQueue();
    } catch (error) {
      setFeedback({ label: 'Отклонение', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const applyAuditSearch = async () => {
    setFeedback({ label: 'Журнал', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const parsedMin = auditMinAmount.trim() === '' ? null : Number(auditMinAmount);
      const parsedMax = auditMaxAmount.trim() === '' ? null : Number(auditMaxAmount);
      const maxAmount = parsedMax !== null && Number.isFinite(parsedMax) ? parsedMax : null;
      // Default to amount-bearing (money / high-risk) records when no amount bound is set:
      // the audit query drops null-amount rows once a bound is present (§5.5).
      const minAmount = parsedMin !== null && Number.isFinite(parsedMin)
        ? parsedMin
        : (maxAmount === null ? 0 : null);
      const result = await apiClients.audit.search({
        branchId: nextBackend.branchId,
        actorStaffUserId: auditActor.trim() === '' ? null : auditActor.trim(),
        minAmount,
        maxAmount,
        limit: 50
      });
      setAuditResult(result);
      setFeedback({ label: 'Журнал', state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: 'Журнал', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const auditRecords = readArray<Record<string, unknown>>(auditResult, 'records');
  const staffOptions = Object.entries(staffNames);

  return (
    <main className="workspace-screen review-screen">
      <section className="screen-head review-head">
        <div>
          <span>Проверка</span>
          <h1>Проверка · заявки и журнал</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Заявки загружены')}</span>
        </div>
      </section>

      <section className="state-strip review-state-strip" aria-label="Сводка проверки">
        <StateFlag label="Заявки" value={String(requests.length)} critical={requests.length > 0} />
        <StateFlag label="Источник" value={workspaceLoadStatusLabel(loadStatus, 'Платформа')} critical={loadStatus !== 'backend'} />
      </section>

      <div className="review-segments" role="tablist">
        <button type="button" role="tab" aria-selected={activeSegment === 'queue'} className={activeSegment === 'queue' ? 'active' : undefined} onClick={() => setActiveSegment('queue')}>Заявки на одобрение</button>
        <button type="button" role="tab" aria-selected={activeSegment === 'audit'} className={activeSegment === 'audit' ? 'active' : undefined} onClick={() => setActiveSegment('audit')}>Журнал операций</button>
      </div>

      {activeSegment === 'queue' && (
        <section className="review-panel review-queue-panel">
          {requests.length === 0 ? (
            <p className="review-empty">{loadError ?? 'Нет заявок на одобрение'}</p>
          ) : (
            requests.map((request) => {
              const expiryBadge = reviewExpiryBadge(request.expiresAtUtc, Date.now());
              return (
              <article key={request.moneyActionRequestId} className="review-request-row">
                <div className="review-request-head">
                  <strong>{reviewActionTypeLabel(request.actionType)}</strong>
                  <b>{formatMinorUnits(request.amountMinorUnits, request.currencyCode || currencyCode)}</b>
                </div>
                <em>{request.reason}</em>
                <div className="review-request-meta">
                  <span>Запросил: {resolveStaffName(request.requestedByStaffUserId)}</span>
                  <span>Создано: {formatTime(request.createdAtUtc)}</span>
                  <span>Истекает: {formatTime(request.expiresAtUtc)}</span>
                  {expiryBadge && <span className={`review-expiry-badge ${expiryBadge.tone}`}>{expiryBadge.label}</span>}
                </div>
                {rejectingId === request.moneyActionRequestId ? (
                  <div className="review-reject-form">
                    <label>
                      Причина отклонения
                      <input value={decisionReason} onChange={(event) => setDecisionReason(event.currentTarget.value)} placeholder="почему отклонено" />
                    </label>
                    <div className="review-request-actions">
                      <button type="button" onClick={() => void confirmReject(request)}>Подтвердить отклонение</button>
                      <button type="button" onClick={() => { setRejectingId(''); setDecisionReason(''); }}>Отмена</button>
                    </div>
                  </div>
                ) : (
                  <div className="review-request-actions">
                    <button type="button" onClick={() => void approveRequest(request)}>Одобрить</button>
                    <button type="button" onClick={() => { setRejectingId(request.moneyActionRequestId); setDecisionReason(''); }}>Отклонить</button>
                  </div>
                )}
              </article>
              );
            })
          )}
          <FeedbackNotice feedback={feedback} />
        </section>
      )}

      {activeSegment === 'audit' && (
        <section className="review-panel review-audit-panel">
          <div className="review-audit-filters">
            <label>
              Сотрудник
              <select value={auditActor} onChange={(event) => setAuditActor(event.currentTarget.value)}>
                <option value="">Все сотрудники</option>
                {staffOptions.map(([staffUserId, name]) => (
                  <option key={staffUserId} value={staffUserId}>{name}</option>
                ))}
              </select>
            </label>
            <label>Сумма от<input inputMode="numeric" value={auditMinAmount} onChange={(event) => setAuditMinAmount(event.currentTarget.value)} placeholder="мин" /></label>
            <label>Сумма до<input inputMode="numeric" value={auditMaxAmount} onChange={(event) => setAuditMaxAmount(event.currentTarget.value)} placeholder="макс" /></label>
            <button type="button" onClick={() => void applyAuditSearch()}>Применить фильтр</button>
          </div>
          <div className="review-audit-list">
            {auditRecords.length === 0 ? (
              <p className="review-empty">Записей нет — задайте фильтр</p>
            ) : (
              auditRecords.map((record) => (
                <article key={readString(record, 'auditRecordId')} className="review-audit-row">
                  <span>{formatTime(readString(record, 'createdAtUtc'))}</span>
                  <strong>{reviewAuditActorLabel(record)}</strong>
                  <em>{auditActionLabel(readString(record, 'action'))}</em>
                  <b>{readNumber(record, 'amountMinorUnits', 0) > 0 ? formatMinorUnits(readNumber(record, 'amountMinorUnits', 0), currencyCode) : '—'}</b>
                </article>
              ))
            )}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>
      )}
    </main>
  );
}

function BackendLogsWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [eventSearch, setEventSearch] = useState('');
  const [activeLogFilter, setActiveLogFilter] = useState('Все события');
  const [selectedEventKey, setSelectedEventKey] = useState('');
  const [selectedSource, setSelectedSource] = useState('Все');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [auditResult, setAuditResult] = useState<AuditSearchResultDto | null>(null);
  const [diagnostics, setDiagnostics] = useState<BranchDiagnosticsDto | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [auditActionFilter, setAuditActionFilter] = useState('');
  const [auditOutcomeFilter, setAuditOutcomeFilter] = useState('');
  const [auditTargetTypeFilter, setAuditTargetTypeFilter] = useState('');
  const [auditFromUtcFilter, setAuditFromUtcFilter] = useState('');
  const [auditToUtcFilter, setAuditToUtcFilter] = useState('');
  const [auditLimit, setAuditLimit] = useState('30');

  const buildAuditSearchRequest = (nextBackend: OperatorBackendContext, overrides: AuditSearchOverrides = {}) => {
    const limitValue = overrides.limit ?? Number(auditLimit);
    const limit = Number.isInteger(limitValue) && limitValue > 0 ? Math.min(limitValue, 200) : 30;

    return {
      branchId: nextBackend.branchId,
      action: overrides.action !== undefined ? overrides.action : auditActionFilter.trim(),
      outcome: overrides.outcome !== undefined ? overrides.outcome : auditOutcomeFilter.trim(),
      targetType: overrides.targetType !== undefined ? overrides.targetType : auditTargetTypeFilter.trim(),
      fromUtc: overrides.fromUtc !== undefined ? overrides.fromUtc : auditFromUtcFilter.trim(),
      toUtc: overrides.toUtc !== undefined ? overrides.toUtc : auditToUtcFilter.trim(),
      limit
    };
  };

  const loadLogs = async (nextBackend = backend, auditOverrides: AuditSearchOverrides = {}) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      setLoadError(null);
      return;
    }

    setLoadStatus('loading');
    setLoadError(null);
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [audit, branchDiagnostics] = await Promise.all([
        apiClients.audit.search(buildAuditSearchRequest(nextBackend, auditOverrides)),
        apiClients.diagnostics.getDiagnostics(nextBackend.branchId)
      ]);
      setAuditResult(audit);
      setDiagnostics(branchDiagnostics);
      setSelectedEventKey('');
      setLoadError(null);
      setLoadStatus('backend');
    } catch (error) {
      const detail = projectOperatorError(error).detail;
      setLoadStatus('failed');
      setLoadError(detail);
      setFeedback({ label: 'Логи', state: 'failed', detail });
    }
  };

  useEffect(() => {
    void loadLogs();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const auditRecords = readArray<Record<string, unknown>>(auditResult, 'records');
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const deviceSummary = isRecord(diagnostics) ? diagnostics.deviceSummary : null;
  const auditEvents = mapAuditRecordsToLogEvents(auditRecords);
  const diagnosticEvents = mapDiagnosticsToLogEvents(diagnostics);
  const events = [...diagnosticEvents, ...auditEvents];
  const filteredEvents = events.filter((event) => {
    const [time, title, detail, source, tone] = event;
    const filterMatches = activeLogFilter === 'Все события'
      || (activeLogFilter === 'Только ошибки' && tone === 'warning')
      || (activeLogFilter === 'ПК и связь' && matchesLogSource(event, 'Агент'))
      || (activeLogFilter === 'Касса' && matchesLogSource(event, 'Касса'))
      || (activeLogFilter === 'Оператор' && matchesLogSource(event, 'Оператор'))
      || (activeLogFilter === 'Системные' && matchesLogSource(event, 'Платформа'));
    const sourceMatches = matchesLogSource(event, selectedSource);
    const searchMatches = `${time} ${title} ${detail} ${source}`.toLowerCase().includes(eventSearch.trim().toLowerCase());
    return filterMatches && sourceMatches && searchMatches;
  });
  const visibleEvents = events.length === 0
    ? [logEventPlaceholder(loadStatus, loadError, false)]
    : filteredEvents.length > 0
      ? filteredEvents
      : [logEventPlaceholder(loadStatus, loadError, eventSearch.trim().length > 0 || activeLogFilter !== 'Все события' || selectedSource !== 'Все')];
  const selectedEvent = visibleEvents.find((event) => logEventKey(event) === selectedEventKey) ?? visibleEvents[0];
  const selectedEventDetails = buildLogEventDetailRows(selectedEvent, backend);
  const sourceCards: Array<[string, string, LucideIcon]> = [
    ['Все', `${events.length} событий`, Search],
    ['Агент', `${events.filter((event) => matchesLogSource(event, 'Агент')).length} событий · зависших ${readNumber(deviceSummary, 'staleDevices', 0)}`, MonitorCheck],
    ['Касса', `${events.filter((event) => matchesLogSource(event, 'Касса')).length} записей`, ReceiptText],
    ['Оператор', `${events.filter((event) => matchesLogSource(event, 'Оператор')).length} действий`, UserRoundPlus],
    ['Платформа', `${events.filter((event) => matchesLogSource(event, 'Платформа')).length} событий`, ShieldAlert]
  ];

  const applyAuditSearch = async (label: string, overrides: AuditSearchOverrides = {}) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, overrides));
      setAuditResult(audit);
      setSelectedEventKey('');
      setLoadStatus('backend');
      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const applyAuditPeriodPreset = async (label: string, preset: AuditPeriodPreset) => {
    const range = auditPeriodPresetRange(preset);
    setAuditFromUtcFilter(range.fromUtc ?? '');
    setAuditToUtcFilter(range.toUtc ?? '');
    setAuditLimit('50');
    await applyAuditSearch(label, { ...range, limit: 50 });
  };

  const selectLogFilter = (filter: string) => {
    setActiveLogFilter(filter);
    const presets: Record<string, AuditSearchOverrides> = {
      'Все события': { action: '', outcome: '', targetType: '', limit: 30 },
      'Только ошибки': { action: '', outcome: 'denied', targetType: '', limit: 50 },
      'ПК и связь': { action: '', outcome: '', targetType: 'Device', limit: 50 },
      'Касса': { action: 'pos.sale.create', outcome: '', targetType: '', limit: 50 },
      'Оператор': { action: 'identity.staff.create', outcome: '', targetType: '', limit: 50 },
      'Системные': { action: 'updates.rollouts.view', outcome: '', targetType: '', limit: 50 }
    };
    const preset = presets[filter] ?? {};
    setAuditActionFilter(preset.action ?? '');
    setAuditOutcomeFilter(preset.outcome ?? '');
    setAuditTargetTypeFilter(preset.targetType ?? '');
    setAuditFromUtcFilter(preset.fromUtc ?? '');
    setAuditToUtcFilter(preset.toUtc ?? '');
    setAuditLimit(String(preset.limit ?? 30));
    void applyAuditSearch(filter, preset);
  };

  const runLogAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const exportStamp = new Date().toISOString().replace(/[:.]/g, '-');
      if (label === 'Пакет для поддержки') {
        const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, { action: '', outcome: '', targetType: '', fromUtc: '', toUtc: '', limit: 100 }));
        const nextAuditRecords = readArray<Record<string, unknown>>(audit, 'records');
        setAuditResult(audit);
        downloadTextFile(
          `afk4-support-journal-${exportStamp}.json`,
          buildLogsExportJson(nextBackend.branchId, nextAuditRecords, diagnostics, [...mapDiagnosticsToLogEvents(diagnostics), ...mapAuditRecordsToLogEvents(nextAuditRecords)], nextBackend),
          'application/json;charset=utf-8'
        );
      } else if (label === 'Список действий') {
        const csv = await apiClients.shifts.exportOperatorActionReportCsv(nextBackend.branchId, { limit: 100 });
        downloadTextFile(`afk4-operator-action-list-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Только проблемы') {
        const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, { action: '', outcome: 'denied', targetType: '', fromUtc: '', toUtc: '', limit: 50 }));
        const nextAuditRecords = readArray<Record<string, unknown>>(audit, 'records');
        setAuditResult(audit);
        const failureEvents = [...mapDiagnosticsToLogEvents(diagnostics), ...mapAuditRecordsToLogEvents(nextAuditRecords)]
          .filter((event) => event[4] === 'warning');
        downloadTextFile(
          `afk4-support-problems-${exportStamp}.json`,
          buildLogsExportJson(nextBackend.branchId, nextAuditRecords, diagnostics, failureEvents, nextBackend),
          'application/json;charset=utf-8'
        );
      } else {
        const csv = await apiClients.shifts.exportShiftReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-shift-summary-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      }
      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  return (
    <main className="workspace-screen logs-screen">
      <section className="screen-head logs-head">
        <div>
          <span>Журнал</span>
          <h1>Журнал · события смены</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Журнал загружен')}</span>
        </div>
      </section>

      <section className="state-strip logs-state-strip" aria-label="Сводка журнала">
        <StateFlag label="События" value={String(events.length)} />
        <StateFlag label="Ошибки" value={String(events.filter((event) => event[4] === 'warning').length)} critical={events.some((event) => event[4] === 'warning')} />
        <StateFlag label="Команды ПК" value={String(readNumber(commandSummary, 'pendingCommands', 0))} />
        <StateFlag label="Записи" value={String(auditRecords.length)} />
        <StateFlag label="Источник" value={workspaceLoadStatusLabel(loadStatus, 'Платформа')} critical={loadStatus !== 'backend'} />
      </section>

      <section className="logs-layout">
        <section className="logs-panel logs-events-panel">
          <header className="logs-panel-title">
            <span>Журнал событий</span>
            <strong>события платформы и ПК</strong>
          </header>
          <label className="logs-search">
            <Search size={14} />
            <input
              placeholder="ПК, клиент, оператор, событие"
              value={eventSearch}
              onChange={(event) => setEventSearch(event.currentTarget.value)}
            />
          </label>
          <div className="logs-event-list">
            {visibleEvents.map((event) => {
              const [time, title, detail, source, tone] = event;
              const eventKey = logEventKey(event);
              return (
                <button
                  key={eventKey}
                  type="button"
                  className={`log-event-row ${tone}${eventKey === selectedEventKey ? ' active' : ''}`}
                  onClick={() => setSelectedEventKey(eventKey)}
                >
                  <span>{time}</span>
                  <div>
                    <strong>{title}</strong>
                    <em>{detail}</em>
                  </div>
                  <b>{source}</b>
                </button>
              );
            })}
          </div>
        </section>

        <section className="logs-panel logs-detail-panel">
          <header className="logs-panel-title">
            <span>Детали события</span>
            <strong>без внутренних ID</strong>
          </header>
          <div className={`log-detail-card ${selectedEvent[4]}`}>
            <span>{selectedEvent[0]} · {selectedEvent[3]}</span>
            <strong>{selectedEvent[1]}</strong>
            <em>{selectedEvent[2]}</em>
          </div>
          <div className="log-detail-list">
            {selectedEventDetails.map(([label, value]) => (
              <div key={label}><span>{label}</span><strong>{value}</strong></div>
            ))}
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="logs-panel logs-filter-panel">
          <header className="logs-panel-title">
            <span>Фильтры</span>
            <strong>найти нужные записи</strong>
          </header>
          <div className="logs-filter-grid">
            {['Все события', 'Только ошибки', 'ПК и связь', 'Касса', 'Оператор', 'Системные'].map((filter) => (
              <button
                key={filter}
                type="button"
                className={activeLogFilter === filter ? 'active' : undefined}
                onClick={() => selectLogFilter(filter)}
              >
                {filter}
              </button>
            ))}
          </div>
          <div className="logs-audit-filter-form">
            <div className="logs-period-presets" aria-label="Период аудита">
              {auditPeriodPresetOptions.map(([label, preset]) => (
                <button key={preset} type="button" onClick={() => void applyAuditPeriodPreset(label, preset)}>{label}</button>
              ))}
            </div>
            <label>Событие<input value={auditActionFilter} onChange={(event) => setAuditActionFilter(event.currentTarget.value)} placeholder="продажа / сессия / ПК" /></label>
            <label>Результат<input value={auditOutcomeFilter} onChange={(event) => setAuditOutcomeFilter(event.currentTarget.value)} placeholder="успешно / отказ" /></label>
            <label>Раздел<input value={auditTargetTypeFilter} onChange={(event) => setAuditTargetTypeFilter(event.currentTarget.value)} placeholder="сессии / касса / ПК" /></label>
            <label>С<input value={auditFromUtcFilter} onChange={(event) => setAuditFromUtcFilter(event.currentTarget.value)} placeholder="2026-05-21 00:00" /></label>
            <label>До<input value={auditToUtcFilter} onChange={(event) => setAuditToUtcFilter(event.currentTarget.value)} placeholder="2026-05-21 23:59" /></label>
            <label>Записей<input inputMode="numeric" value={auditLimit} onChange={(event) => setAuditLimit(event.currentTarget.value)} /></label>
            <button type="button" onClick={() => applyAuditSearch('Применить фильтр')}>Применить фильтр</button>
          </div>
        </section>

        <section className="logs-panel logs-audit-panel">
          <header className="logs-panel-title">
            <span>Операции смены</span>
            <strong>последние действия</strong>
          </header>
          <div className="logs-audit-list">
            {auditRecords.slice(0, 4).map((record) => (
              <article key={readString(record, 'auditRecordId')} className="log-audit-row">
                <span>{formatTime(readString(record, 'createdAtUtc'))}</span>
                <strong>{auditActorLabel(record, backend)}</strong>
                <em>{auditActionLabel(readString(record, 'action'))}</em>
                <b>{auditOutcomeLabel(readString(record, 'outcome'))}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-sources-panel">
          <header className="logs-panel-title">
            <span>Источники</span>
            <strong>каналы событий</strong>
          </header>
          <div className="logs-source-grid">
            {sourceCards.map(([label, detail, Icon]) => (
              <button
                key={label}
                type="button"
                className={`log-source-card${selectedSource === label ? ' active' : ''}`}
                onClick={() => setSelectedSource(label)}
              >
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-export-panel">
          <header className="logs-panel-title">
            <span>Экспорт</span>
            <strong>для проверки и поддержки</strong>
          </header>
          <div className="logs-export-grid">
            {[
              ['Сводка смены', ReceiptText],
              ['Только проблемы', AlertTriangle],
              ['Список действий', ArrowRightLeft],
              ['Пакет для поддержки', ShieldAlert]
            ].map(([label, Icon]) => (
              <button key={label as string} type="button" onClick={() => runLogAction(label as string)}><Icon size={16} />{label as string}</button>
            ))}
          </div>
        </section>
      </section>
    </main>
  );
}

function BackendSettingsWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [selectedSection, setSelectedSection] = useState('Профиль клуба');
  const [clubName, setClubName] = useState('AFK4');
  const [city, setCity] = useState('Dushanbe');
  const [settingsDirty, setSettingsDirty] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [staffUsers, setStaffUsers] = useState<StaffUserDto[]>([]);
  const [zones, setZones] = useState<ZoneDto[]>([]);
  const [catalog, setCatalog] = useState<PosProductDto[]>([]);
  const [stockMovements, setStockMovements] = useState<StockMovementDto[]>([]);
  const [diagnostics, setDiagnostics] = useState<BranchDiagnosticsDto | null>(null);
  const [rollouts, setRollouts] = useState<UpdateRolloutStatusDto[]>([]);
  const [registeredUpdatePackages, setRegisteredUpdatePackages] = useState<UpdatePackageDto[]>([]);
  const [tariffs, setTariffs] = useState<TariffOptionDto[]>([]);
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [deviceInventory, setDeviceInventory] = useState<DeviceInventoryItemDto[]>([]);
  const [deviceAssignmentDeviceId, setDeviceAssignmentDeviceId] = useState('');
  const [deviceAssignmentSeatId, setDeviceAssignmentSeatId] = useState('');
  const [enrollmentExpiresMinutes, setEnrollmentExpiresMinutes] = useState('15');
  const [enrollmentCode, setEnrollmentCode] = useState<Record<string, unknown> | null>(null);
  const [deviceDetail, setDeviceDetail] = useState<Record<string, unknown> | null>(null);
  const [deviceCommandHistory, setDeviceCommandHistory] = useState<DeviceCommandStatusDto[]>([]);
  const [branchDeviceCommandHistory, setBranchDeviceCommandHistory] = useState<DeviceCommandStatusDto[]>([]);
  const [credentialIdToRevoke, setCredentialIdToRevoke] = useState('');
  const [rotatedCredential, setRotatedCredential] = useState<Record<string, unknown> | null>(null);
  const [criticalAction, setCriticalAction] = useState<
    'credential-revoke' |
    'layout-zone-delete' |
    'layout-seat-delete' |
    'package-state-change' |
    'rollout-state-change' |
    null
  >(null);
  const [deviceCommandType, setDeviceCommandType] = useState('lock');
  const [deviceCommandReason, setDeviceCommandReason] = useState('Проверка оператором');
  const [lastDeviceCommand, setLastDeviceCommand] = useState<Record<string, unknown> | null>(null);
  const [layoutZoneName, setLayoutZoneName] = useState('Основной зал');
  const [layoutZoneSortOrder, setLayoutZoneSortOrder] = useState('10');
  const [layoutSeatZoneId, setLayoutSeatZoneId] = useState('');
  const [layoutSeatName, setLayoutSeatName] = useState('PC-01');
  const [layoutSeatSortOrder, setLayoutSeatSortOrder] = useState('10');
  const [selectedLayoutZoneId, setSelectedLayoutZoneId] = useState('');
  const [selectedLayoutSeatId, setSelectedLayoutSeatId] = useState('');
  const [inviteUserName, setInviteUserName] = useState('operator');
  const [inviteDisplayName, setInviteDisplayName] = useState('Новый оператор');
  const [inviteEmail, setInviteEmail] = useState('');
  const [inviteCode, setInviteCode] = useState<string | null>(null);
  const [resetPassword, setResetPassword] = useState('');
  const [inviteRoleName, setInviteRoleName] = useState('cashier_operator');
  const [selectedStaffUserId, setSelectedStaffUserId] = useState('');
  const [staffProfileUserName, setStaffProfileUserName] = useState('');
  const [staffProfileDisplayName, setStaffProfileDisplayName] = useState('');
  const [staffRoleName, setStaffRoleName] = useState('cashier_operator');
  const [productCategoryName, setProductCategoryName] = useState('Категория 1');
  const [productName, setProductName] = useState('Товар 1');
  const [productSku, setProductSku] = useState('SKU-001');
  const [productPrice, setProductPrice] = useState('12.00');
  const [productTrackStock, setProductTrackStock] = useState(true);
  const [productAllowNegativeStock, setProductAllowNegativeStock] = useState(false);
  const [selectedProductId, setSelectedProductId] = useState('');
  const [stockProductId, setStockProductId] = useState('');
  const [stockMovementType, setStockMovementType] = useState('purchase');
  const [stockQuantityDelta, setStockQuantityDelta] = useState('10');
  const [stockUnitCost, setStockUnitCost] = useState('0.00');
  const [stockReason, setStockReason] = useState('Первичное поступление');
  const [tariffName, setTariffName] = useState('Дневной тариф');
  const [tariffPricePerHour, setTariffPricePerHour] = useState('90.00');
  const [tariffMinimumMinutes, setTariffMinimumMinutes] = useState('15');
  const [tariffRoundingMinutes, setTariffRoundingMinutes] = useState('5');
  const [tariffEffectiveFromUtc, setTariffEffectiveFromUtc] = useState(() => new Date().toISOString());
  const [selectedTariffVersionId, setSelectedTariffVersionId] = useState('');
  const [packageName, setPackageName] = useState('Ночной пакет 5ч');
  const [packagePrice, setPackagePrice] = useState('250.00');
  const [packageMinutes, setPackageMinutes] = useState('300');
  const [packageBonusMinutes, setPackageBonusMinutes] = useState('30');
  const [packageExpiresDays, setPackageExpiresDays] = useState('30');
  const [selectedPackageDefinitionId, setSelectedPackageDefinitionId] = useState('');
  const [updateComponent, setUpdateComponent] = useState('operator-app');
  const [updateVersion, setUpdateVersion] = useState('0.1.0');
  const [updateChannel, setUpdateChannel] = useState('internal');
  const [updateArtifactUri, setUpdateArtifactUri] = useState('https://updates.afk4.staging.mubi.dev/operator-app/0.1.0/operator-app.msi');
  const [updateSha256, setUpdateSha256] = useState('0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef');
  const [updateSignature, setUpdateSignature] = useState('signed-update-package');
  const [updateSignatureAlgorithm, setUpdateSignatureAlgorithm] = useState('ECDSA-P256-SHA256-IEEE-P1363');
  const [updateSizeKilobytes, setUpdateSizeKilobytes] = useState('1024');
  const [updateReleaseNotes, setUpdateReleaseNotes] = useState('Пакет обновления приложения оператора.');
  const [rolloutPackageId, setRolloutPackageId] = useState('');
  const [rolloutChannel, setRolloutChannel] = useState('internal');
  const [rolloutTargetKind, setRolloutTargetKind] = useState('branch');
  const [rolloutTargetDeviceIds, setRolloutTargetDeviceIds] = useState('');
  const [rolloutBatchPercent, setRolloutBatchPercent] = useState('100');
  const [rolloutStartsAtUtc, setRolloutStartsAtUtc] = useState(() => new Date(Date.now() + 60 * 60 * 1000).toISOString());
  const [rolloutReason, setRolloutReason] = useState('Публикация обновления оператором.');
  const [packageStatePackageId, setPackageStatePackageId] = useState('');
  const [packageState, setPackageState] = useState('validated');
  const [packageStateReason, setPackageStateReason] = useState('Подпись проверена.');
  const [rolloutStateRolloutId, setRolloutStateRolloutId] = useState('');
  const [rolloutState, setRolloutState] = useState('paused');
  const [rolloutStateReason, setRolloutStateReason] = useState('Изменение состояния оператором.');

  const loadSettings = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      return;
    }

    setLoadStatus('loading');
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [branchProfile, staff, layoutZones, products, stockMovementRows, branchDiagnostics, rolloutStatuses, tariffOptions, packageOptionRows, deviceRows, branchDeviceCommands] = await Promise.all([
        apiClients.settings.getBranchProfile(nextBackend.branchId),
        apiClients.settings.getStaffUsers(nextBackend.branchId),
        apiClients.settings.getLayoutZones(nextBackend.branchId),
        apiClients.pos.getCatalog(nextBackend.branchId),
        apiClients.inventory.getStockMovements(nextBackend.branchId, { limit: 8 }).catch(() => []),
        apiClients.diagnostics.getDiagnostics(nextBackend.branchId),
        apiClients.updates.getRolloutStatuses(nextBackend.branchId),
        apiClients.settings.getTariffOptions(nextBackend.branchId),
        apiClients.settings.getPackageOptions(nextBackend.branchId).catch(() => []),
        hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)
          ? apiClients.devices.listDevices(nextBackend.branchId).catch(() => [])
          : Promise.resolve([]),
        hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)
          ? apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 }).catch(() => [])
          : Promise.resolve([])
      ]);
      const productRows = Array.isArray(products) ? products : [];
      const staffRows = Array.isArray(staff) ? staff : [];
      const selectedStaff = staffRows.find((user) => readString(user, 'staffUserId') === selectedStaffUserId) ?? staffRows[0];
      const selectedStaffRole = readArray<string>(selectedStaff, 'roleNames')
        .find((role) => staffRoleOptions.includes(role as (typeof staffRoleOptions)[number]));
      setStaffUsers(staffRows);
      setSelectedStaffUserId(readString(selectedStaff, 'staffUserId'));
      setStaffProfileUserName(readString(selectedStaff, 'userName'));
      setStaffProfileDisplayName(operatorDisplayNameLabel(readString(selectedStaff, 'displayName')));
      setStaffRoleName(selectedStaffRole ?? 'cashier_operator');
      const zoneRows = Array.isArray(layoutZones) ? layoutZones : [];
      setZones(zoneRows);
      const firstZoneId = readString(zoneRows[0], 'zoneId');
      setLayoutSeatZoneId((current) => zoneRows.some((zone) => readString(zone, 'zoneId') === current) ? current : firstZoneId);
      setSelectedLayoutZoneId((current) => zoneRows.some((zone) => readString(zone, 'zoneId') === current) ? current : '');
      const firstSeatId = zoneRows.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats')).map((seat) => readString(seat, 'seatId')).find(Boolean) ?? '';
      setSelectedLayoutSeatId((current) => zoneRows.some((zone) => readArray<Record<string, unknown>>(zone, 'seats').some((seat) => readString(seat, 'seatId') === current)) ? current : '');
      setDeviceAssignmentSeatId((current) => isGuid(current) ? current : firstSeatId);
      setCatalog(productRows);
      setSelectedProductId((current) => productRows.some((product) => readString(product, 'productId') === current) ? current : '');
      setStockMovements(Array.isArray(stockMovementRows) ? stockMovementRows : []);
      setStockProductId((current) => productRows.some((product) => readString(product, 'productId') === current && readBoolean(product, 'trackStock'))
        ? current
        : readString(productRows.find((product) => readBoolean(product, 'trackStock')), 'productId'));
      setDiagnostics(branchDiagnostics);
      const rolloutRows = Array.isArray(rolloutStatuses) ? rolloutStatuses : [];
      setRollouts(rolloutRows);
      setRolloutPackageId((current) => rolloutRows.some((rollout) => readString(rollout, 'updatePackageId') === current)
        ? current
        : readString(rolloutRows[0], 'updatePackageId'));
      setPackageStatePackageId((current) => rolloutRows.some((rollout) => readString(rollout, 'updatePackageId') === current)
        ? current
        : readString(rolloutRows[0], 'updatePackageId'));
      setRolloutStateRolloutId((current) => rolloutRows.some((rollout) => readString(rollout, 'updateRolloutId') === current)
        ? current
        : readString(rolloutRows[0], 'updateRolloutId'));
      const tariffRows = Array.isArray(tariffOptions) ? tariffOptions : [];
      setTariffs(tariffRows);
      setSelectedTariffVersionId((current) => tariffRows.some((tariff) => readString(tariff, 'tariffVersionId') === current) ? current : '');
      const packageRows = Array.isArray(packageOptionRows) ? packageOptionRows : [];
      setPackageOptions(packageRows);
      setSelectedPackageDefinitionId((current) => packageRows.some((option) => readString(option, 'packageDefinitionId') === current) ? current : '');
      const nextDeviceInventory = Array.isArray(deviceRows) ? deviceRows : [];
      setDeviceInventory(nextDeviceInventory);
      setBranchDeviceCommandHistory(Array.isArray(branchDeviceCommands) ? branchDeviceCommands : []);
      setDeviceAssignmentDeviceId((current) => nextDeviceInventory.some((device) => readString(device, 'deviceId') === current)
        ? current
        : readString(nextDeviceInventory[0], 'deviceId'));
      setClubName(readString(branchProfile, 'name', 'AFK4'));
      setCity(readString(branchProfile, 'city', 'Dushanbe'));
      setSettingsDirty(false);
      setLoadStatus('backend');
    } catch (error) {
      setLoadStatus('failed');
      setFeedback({ label: 'Настройки', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  useEffect(() => {
    void loadSettings();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  useEffect(() => {
    setCriticalAction(null);
  }, [deviceAssignmentDeviceId]);

  const sections = [
    ['Профиль клуба', 'название, город, валюта'],
    ['Залы и ПК', 'зоны, рабочие места, статусы'],
    ['Тарифы', 'пакеты, постоплата, VIP'],
    ['Персонал', 'операторы, роли, доступы'],
    ['Товары и склад', 'товары, остатки, чеки'],
    ['Интеграции', 'платежи, обновления, экспорт']
  ];
  const selectedSectionDetail = sections.find(([name]) => name === selectedSection)?.[1] ?? '';
  const deviceSummary = isRecord(diagnostics) ? diagnostics.deviceSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const canManageLayout = backend !== null && hasPermission(backend.session, permissionNames.manageLayout);
  const canManageBranchStaff = backend !== null && hasPermission(backend.session, permissionNames.manageBranchStaff);
  const canManageRoles = backend !== null && hasPermission(backend.session, permissionNames.manageRoles);
  const canManagePosCatalog = backend !== null && hasPermission(backend.session, permissionNames.managePosCatalog);
  const canManageInventoryStock = backend !== null && hasPermission(backend.session, permissionNames.manageInventoryStock);
  const canManageTariffs = backend !== null && hasPermission(backend.session, permissionNames.manageTariffs);
  const canManagePackages = backend !== null && hasPermission(backend.session, permissionNames.managePackages);
  const canManageUpdatePackages = backend !== null && hasPermission(backend.session, permissionNames.manageUpdatePackages);
  const canManageUpdateRollouts = backend !== null && hasPermission(backend.session, permissionNames.manageUpdateRollouts);
  const canCreateDeviceEnrollmentCode = backend !== null && hasPermission(backend.session, permissionNames.createDeviceEnrollmentCode);
  const canAssignDeviceSeat = backend !== null && hasPermission(backend.session, permissionNames.assignDeviceSeat);
  const canViewDeviceDetail = backend !== null && hasPermission(backend.session, permissionNames.viewDeviceDetail);
  const canViewDeviceCommandStatus = backend !== null && hasPermission(backend.session, permissionNames.viewDeviceCommandStatus);
  const canDispatchDeviceCommand = backend !== null && hasPermission(backend.session, permissionNames.dispatchDeviceCommand);
  const canRotateDeviceCredential = backend !== null && hasPermission(backend.session, permissionNames.rotateDeviceCredential);
  const canRevokeDeviceCredential = backend !== null && hasPermission(backend.session, permissionNames.revokeDeviceCredential);
  const selectedStaffUser = staffUsers.find((user) => readString(user, 'staffUserId') === selectedStaffUserId);
  const selectedStaffIsActive = readBoolean(selectedStaffUser, 'isActive', true);
  const selectedLayoutZone = zones.find((zone) => readString(zone, 'zoneId') === selectedLayoutZoneId) ?? null;
  const selectedLayoutSeat = zones
    .flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats'))
    .find((seat) => readString(seat, 'seatId') === selectedLayoutSeatId) ?? null;
  const layoutSeatOptions = zones.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats').map((seat) => ({
    seatId: readString(seat, 'seatId'),
    label: `${readString(zone, 'name', 'Зал')} · ${readString(seat, 'name', 'ПК')}`
  }))).filter((seat) => isGuid(seat.seatId));
  const trackedCatalog = catalog.filter((product) => readBoolean(product, 'trackStock'));
  const deviceRecentCommands = deviceCommandHistory.length > 0
    ? deviceCommandHistory
    : readArray<Record<string, unknown>>(deviceDetail, 'recentCommands');
  const getDeviceInventoryName = (deviceId: string) =>
    readString(deviceInventory.find((device) => readString(device, 'deviceId') === deviceId), 'machineName', 'Устройство');
  const selectedRollout = rollouts.find((rollout) => readString(rollout, 'updateRolloutId') === rolloutStateRolloutId) ?? rollouts[0] ?? null;
  const selectedRolloutDeviceStatuses = readArray<Record<string, unknown>>(selectedRollout, 'deviceStatuses');
  const deviceOptions = deviceInventory
    .map((device) => ({
      id: readString(device, 'deviceId'),
      label: `${readString(device, 'machineName', 'Устройство')} · ${readString(device, 'zoneName', 'без зала')} · ${readString(device, 'seatName', 'без места')}`
    }))
    .filter((device) => isGuid(device.id));
  const selectedDeviceLabel = getDeviceInventoryName(deviceAssignmentDeviceId);
  const updatePackageOptions = Array.from(new Map([
    ...rollouts.map((rollout) => ({
      id: readString(rollout, 'updatePackageId'),
      label: `${updateComponentLabel(readString(rollout, 'component'))} ${readString(rollout, 'version', 'версия')} · ${updateChannelLabel(readString(rollout, 'channel'))}`
    })),
    ...registeredUpdatePackages.map((updatePackage) => ({
      id: readString(updatePackage, 'updatePackageId'),
      label: `${updateComponentLabel(readString(updatePackage, 'component'))} ${readString(updatePackage, 'version', 'версия')} · ${updateChannelLabel(readString(updatePackage, 'channel'))} · ${updatePackageStateLabel(readString(updatePackage, 'state', 'registered'))}`
    }))
  ].filter((option) => isGuid(option.id)).map((option) => [option.id, option])).values());
  const rolloutOptions = rollouts
    .map((rollout) => ({
      id: readString(rollout, 'updateRolloutId'),
      label: `${updateComponentLabel(readString(rollout, 'component'))} ${readString(rollout, 'version', 'версия')} · ${updateRolloutStateLabel(readString(rollout, 'state'))}`
    }))
    .filter((rollout) => isGuid(rollout.id));
  const rolloutTargetDeviceIdSet = new Set(rolloutTargetDeviceIds.split(/[\s,;]+/).map((value) => value.trim()).filter(Boolean));
  const rotatedCredentialId = readString(rotatedCredential, 'credentialId');
  const rotatedCredentialLabel = rotatedCredentialId ? `готов к отзыву для ${selectedDeviceLabel}` : 'сначала смените ключ';
  const selectLayoutZone = (zone: Record<string, unknown>) => {
    const zoneId = readString(zone, 'zoneId');
    setSelectedLayoutZoneId(zoneId);
    setLayoutSeatZoneId(zoneId);
    setLayoutZoneName(readString(zone, 'name', layoutZoneName));
    setLayoutZoneSortOrder(String(readNumber(zone, 'sortOrder', Number(layoutZoneSortOrder))));
    triggerFeedback(setFeedback, readString(zone, 'name', 'Зал'), 'confirmed');
  };
  const selectLayoutSeat = (zone: Record<string, unknown>, seat: Record<string, unknown>) => {
    setSelectedLayoutSeatId(readString(seat, 'seatId'));
    setLayoutSeatZoneId(readString(zone, 'zoneId'));
    setLayoutSeatName(readString(seat, 'name', layoutSeatName));
    setLayoutSeatSortOrder(String(readNumber(seat, 'sortOrder', Number(layoutSeatSortOrder))));
    triggerFeedback(setFeedback, readString(seat, 'name', 'ПК'), 'confirmed');
  };
  const selectCatalogProduct = (product: PosProductDto) => {
    const productId = readString(product, 'productId');
    const price = readMoney(product, 'price');
    setSelectedProductId(productId);
    setProductName(readString(product, 'name', productName));
    setProductSku(readString(product, 'sku', productSku));
    setProductPrice(price ? formatMoneyInputMinorUnits(price.minorUnits) : productPrice);
    setProductTrackStock(readBoolean(product, 'trackStock', true));
    setProductAllowNegativeStock(readBoolean(product, 'allowNegativeStock'));
    triggerFeedback(setFeedback, readString(product, 'name', 'Товар'), 'confirmed');
  };
  const selectTariffOption = (option: TariffOptionDto) => {
    setSelectedTariffVersionId(readString(option, 'tariffVersionId'));
    setTariffName(readString(option, 'name', tariffName));
    setTariffPricePerHour(formatMoneyInputMinorUnits(readNumber(option, 'pricePerMinuteMinorUnits', 0) * 60));
    setTariffMinimumMinutes(String(readNumber(option, 'minimumBillableMinutes', Number(tariffMinimumMinutes))));
    setTariffRoundingMinutes(String(readNumber(option, 'roundingIncrementMinutes', Number(tariffRoundingMinutes))));
    setTariffEffectiveFromUtc(readString(option, 'effectiveFromUtc', new Date().toISOString()));
    triggerFeedback(setFeedback, readString(option, 'name', 'Тариф'), 'confirmed');
  };
  const selectPackageOption = (option: PackageOptionDto) => {
    setSelectedPackageDefinitionId(readString(option, 'packageDefinitionId'));
    setPackageName(readString(option, 'name', packageName));
    setPackagePrice(formatMoneyInputMinorUnits(readNumber(option, 'priceMinorUnits', 0)));
    setPackageMinutes(String(Math.round(readNumber(option, 'includedSeconds', 0) / 60)));
    setPackageBonusMinutes(String(Math.round(readNumber(option, 'bonusSeconds', 0) / 60)));
    setPackageExpiresDays(String(readNumber(option, 'expiresAfterDays', 30)));
    triggerFeedback(setFeedback, readString(option, 'name', 'Пакет'), 'confirmed');
  };
  const selectDeviceInventoryItem = (device: DeviceInventoryItemDto) => {
    const deviceId = readString(device, 'deviceId');
    const seatId = readString(device, 'seatId');
    setDeviceAssignmentDeviceId(deviceId);
    if (isGuid(seatId)) {
      setDeviceAssignmentSeatId(seatId);
    }
    setDeviceDetail(null);
    setDeviceCommandHistory([]);
    setRotatedCredential(null);
    setCredentialIdToRevoke('');
    triggerFeedback(setFeedback, readString(device, 'machineName', 'Устройство'), 'confirmed');
  };
  const readiness = [
    ['Профиль клуба', `${clubName} · ${city}`],
    ['Залы и ПК', `${zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0)} рабочих мест`],
    ['Персонал', `${staffUsers.length} сотрудников`],
    ['Касса', `${currencyCode} · отчёты платформы`],
    ['Устройства', `${readNumber(deviceSummary, 'onlineDevices', 0)} из ${readNumber(deviceSummary, 'totalDevices', 0)} онлайн`]
  ];
  const actions: Array<[string, string, LucideIcon]> = [
    ['Добавить ПК', 'новое рабочее место', MonitorCheck],
    ['Создать тариф', 'цена и правила списания', CircleDollarSign],
    ['Пригласить сотрудника', canManageBranchStaff ? 'создать учётную запись' : 'нет прав доступа', UserRoundPlus],
    ['Обновить профиль сотрудника', canManageBranchStaff ? 'редактировать логин и имя' : 'нет прав доступа', Wrench],
    ['Проверить устройства', 'обновить диагностику', Wifi]
  ];

  const runSettingsAction = async (label: string) => {
    setCriticalAction(null);
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === 'Проверить устройства') {
        setDiagnostics(await apiClients.diagnostics.getDiagnostics(nextBackend.branchId));
      } else if (label === 'Обновить историю команд') {
        if (!hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)) {
          throw new Error('Нет прав на просмотр истории команд устройств.');
        }

        const commands = await apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 });
        setBranchDeviceCommandHistory(Array.isArray(commands) ? commands : []);
      } else if (label === 'Создать код подключения') {
        if (!hasPermission(nextBackend.session, permissionNames.createDeviceEnrollmentCode)) {
          throw new Error('Нет прав на создание кода подключения устройства.');
        }

        const expiresInMinutes = Number(enrollmentExpiresMinutes);
        if (!Number.isInteger(expiresInMinutes) || expiresInMinutes < 1 || expiresInMinutes > 1440) {
          throw new Error('Срок действия кода должен быть от 1 минуты до 24 часов.');
        }

        const expiresInSeconds = expiresInMinutes * 60;
        const code = await apiClients.devices.createEnrollmentCode(nextBackend.branchId, nextBackend.session.organizationId, expiresInSeconds);
        setEnrollmentCode(code);
      } else if (label === 'Назначить устройство') {
        if (!hasPermission(nextBackend.session, permissionNames.assignDeviceSeat)) {
          throw new Error('Нет прав на назначение устройства рабочему месту.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const seatId = deviceAssignmentSeatId.trim();
        if (!isGuid(deviceId) || !isGuid(seatId)) {
          throw new Error('Выберите устройство и рабочее место.');
        }

        await apiClients.settings.assignDeviceSeat(nextBackend.branchId, deviceId, {
          organizationId: nextBackend.session.organizationId,
          seatId
        });
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          const [detail, commands] = await Promise.all([
            apiClients.devices.getDeviceDetail(deviceId),
            hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)
              ? apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => [])
              : Promise.resolve([])
          ]);
          setDeviceDetail(detail);
          setDeviceCommandHistory(Array.isArray(commands) ? commands : []);
        }
        await loadSettings(nextBackend);
      } else if (label === 'Открыть карточку устройства') {
        if (!hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          throw new Error('Нет прав на просмотр карточки устройства.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        if (!isGuid(deviceId)) {
          throw new Error('Выберите устройство.');
        }

        const [detail, commands] = await Promise.all([
          apiClients.devices.getDeviceDetail(deviceId),
          hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)
            ? apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => [])
            : Promise.resolve([])
        ]);
        setDeviceDetail(detail);
        setDeviceCommandHistory(Array.isArray(commands) ? commands : []);
      } else if (label === 'Отправить команду') {
        if (!hasPermission(nextBackend.session, permissionNames.dispatchDeviceCommand)) {
          throw new Error('Нет прав на отправку команд устройствам.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const type = deviceCommandType.trim();
        const reason = deviceCommandReason.trim();
        if (!isGuid(deviceId) || !type || !reason) {
          throw new Error('Выберите устройство, команду и причину.');
        }

        const command = await apiClients.devices.dispatchDeviceCommand(deviceId, {
          type,
          payload: {
            reason,
            source: 'operator-settings'
          }
        });
        setLastDeviceCommand(command);
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          setDeviceInventory(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
        }
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceCommandStatus)) {
          const [selectedCommands, branchCommands] = await Promise.all([
            apiClients.devices.listDeviceCommands(deviceId, { limit: 25 }).catch(() => deviceCommandHistory),
            apiClients.devices.listBranchDeviceCommands(nextBackend.branchId, { limit: 50 }).catch(() => branchDeviceCommandHistory)
          ]);
          setDeviceCommandHistory(Array.isArray(selectedCommands) ? selectedCommands : []);
          setBranchDeviceCommandHistory(Array.isArray(branchCommands) ? branchCommands : []);
        }
      } else if (label === 'Выдать новый ключ') {
        if (!hasPermission(nextBackend.session, permissionNames.rotateDeviceCredential)) {
          throw new Error('Нет прав на смену ключа устройства.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        if (!isGuid(deviceId)) {
          throw new Error('Выберите устройство.');
        }

        const rotated = await apiClients.devices.rotateDeviceCredential(deviceId);
        setRotatedCredential(rotated);
        setCredentialIdToRevoke(readString(rotated, 'credentialId'));
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          setDeviceInventory(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
        }
      } else if (label === 'Отозвать ключ') {
        if (!hasPermission(nextBackend.session, permissionNames.revokeDeviceCredential)) {
          throw new Error('Нет прав на отзыв ключа устройства.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const credentialId = credentialIdToRevoke.trim();
        if (!isGuid(deviceId) || !isGuid(credentialId)) {
          throw new Error('Выберите устройство и ключ для отзыва.');
        }

        await apiClients.devices.revokeDeviceCredential(deviceId, credentialId);
        setRotatedCredential(null);
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          setDeviceInventory(await apiClients.devices.listDevices(nextBackend.branchId).catch(() => deviceInventory));
        }
      } else if (label === 'Добавить зал' || label === 'Обновить зал') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const name = layoutZoneName.trim();
        const sortOrder = Number(layoutZoneSortOrder);
        if (!name || !Number.isInteger(sortOrder)) {
          throw new Error('Заполните название зала и целый порядок сортировки.');
        }
        if (label === 'Обновить зал' && !isGuid(selectedLayoutZoneId)) {
          throw new Error('Выберите зал для обновления.');
        }

        const zone = label === 'Обновить зал'
          ? await apiClients.settings.updateZone(nextBackend.branchId, selectedLayoutZoneId, {
            organizationId: nextBackend.session.organizationId,
            name,
            sortOrder
          })
          : await apiClients.settings.createZone(nextBackend.branchId, {
            organizationId: nextBackend.session.organizationId,
            name,
            sortOrder
          });
        const zoneId = readString(zone, 'zoneId', selectedLayoutZoneId);
        setSelectedLayoutZoneId(zoneId);
        setLayoutSeatZoneId(zoneId);
        if (label === 'Добавить зал') {
          setLayoutZoneName(`Zone ${zones.length + 2}`);
          setLayoutZoneSortOrder(String(sortOrder + 10));
        }
        await loadSettings(nextBackend);
      } else if (label === 'Удалить зал') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const zoneId = selectedLayoutZoneId.trim();
        if (!isGuid(zoneId)) {
          throw new Error('Выберите зал для удаления.');
        }

        await apiClients.settings.deleteZone(nextBackend.branchId, zoneId, nextBackend.session.organizationId);
        setSelectedLayoutZoneId('');
        setLayoutSeatZoneId('');
        await loadSettings(nextBackend);
      } else if (label === 'Добавить ПК' || label === 'Обновить ПК') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const zoneId = layoutSeatZoneId.trim();
        if (!zoneId) {
          throw new Error('Сначала создайте зал для нового ПК.');
        }

        const name = layoutSeatName.trim();
        const sortOrder = Number(layoutSeatSortOrder);
        if (!name || !Number.isInteger(sortOrder)) {
          throw new Error('Заполните название ПК и целый порядок сортировки.');
        }
        if (label === 'Обновить ПК' && !isGuid(selectedLayoutSeatId)) {
          throw new Error('Выберите ПК для обновления.');
        }

        const seat = label === 'Обновить ПК'
          ? await apiClients.settings.updateSeat(nextBackend.branchId, selectedLayoutSeatId, {
            organizationId: nextBackend.session.organizationId,
            zoneId,
            name,
            sortOrder
          })
          : await apiClients.settings.createSeat(nextBackend.branchId, {
            organizationId: nextBackend.session.organizationId,
            zoneId,
            name,
            sortOrder
          });
        setSelectedLayoutSeatId(readString(seat, 'seatId', selectedLayoutSeatId));
        if (label === 'Добавить ПК') {
          setLayoutSeatName(`PC-${zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0) + 2}`);
          setLayoutSeatSortOrder(String(sortOrder + 10));
        }
        await loadSettings(nextBackend);
      } else if (label === 'Удалить ПК') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const seatId = selectedLayoutSeatId.trim();
        if (!isGuid(seatId)) {
          throw new Error('Выберите ПК для удаления.');
        }

        await apiClients.settings.deleteSeat(nextBackend.branchId, seatId, nextBackend.session.organizationId);
        setSelectedLayoutSeatId('');
        setDeviceAssignmentSeatId('');
        await loadSettings(nextBackend);
      } else if (label === 'Создать тариф') {
        if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
          throw new Error('Нет прав на управление тарифами.');
        }

        const name = tariffName.trim();
        const pricePerHourMinorUnits = parseMoneyInputMinorUnits(tariffPricePerHour);
        const minimumBillableMinutes = Number(tariffMinimumMinutes);
        const roundingIncrementMinutes = Number(tariffRoundingMinutes);
        if (!name || pricePerHourMinorUnits === null
          || !Number.isInteger(minimumBillableMinutes) || minimumBillableMinutes <= 0
          || !Number.isInteger(roundingIncrementMinutes) || roundingIncrementMinutes <= 0) {
          throw new Error('Заполните название тарифа, цену за час, минимум и округление.');
        }

        const tariff = await apiClients.settings.createTariff(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name,
          idempotencyKey: createIdempotencyKey('tariff-create')
        });
        const tariffId = readString(tariff, 'tariffId');
        if (tariffId) {
          await apiClients.settings.createTariffVersion(nextBackend.branchId, tariffId, {
            organizationId: nextBackend.session.organizationId,
            tariffId,
            currencyCode,
            pricePerMinuteMinorUnits: Math.max(1, Math.round(pricePerHourMinorUnits / 60)),
            minimumBillableMinutes,
            roundingIncrementMinutes,
            effectiveFromUtc: new Date().toISOString(),
            idempotencyKey: createIdempotencyKey('tariff-version-create')
          });
        }
        setTariffName(`Тариф ${tariffs.length + 2}`);
        setTariffEffectiveFromUtc(new Date().toISOString());
        await loadSettings(nextBackend);
      } else if (label === 'Обновить тариф' || label === 'Снять тариф') {
        if (!hasPermission(nextBackend.session, permissionNames.manageTariffs)) {
          throw new Error('Нет прав на управление тарифами.');
        }

        const tariffOption = tariffs.find((tariff) => readString(tariff, 'tariffVersionId') === selectedTariffVersionId);
        const tariffId = readString(tariffOption, 'tariffId');
        const tariffVersionId = readString(tariffOption, 'tariffVersionId');
        const name = tariffName.trim();
        const pricePerHourMinorUnits = parseMoneyInputMinorUnits(tariffPricePerHour);
        const minimumBillableMinutes = Number(tariffMinimumMinutes);
        const roundingIncrementMinutes = Number(tariffRoundingMinutes);
        if (!isGuid(tariffId) || !isGuid(tariffVersionId) || !name || pricePerHourMinorUnits === null
          || !Number.isInteger(minimumBillableMinutes) || minimumBillableMinutes <= 0
          || !Number.isInteger(roundingIncrementMinutes) || roundingIncrementMinutes <= 0) {
          throw new Error('Выберите тариф и заполните название, цену за час, минимум и округление.');
        }

        const isActive = label !== 'Снять тариф';
        await apiClients.settings.updateTariff(nextBackend.branchId, tariffId, {
          organizationId: nextBackend.session.organizationId,
          name,
          isActive
        });
        await apiClients.settings.updateTariffVersion(nextBackend.branchId, tariffId, tariffVersionId, {
          organizationId: nextBackend.session.organizationId,
          currencyCode,
          pricePerMinuteMinorUnits: Math.max(1, Math.round(pricePerHourMinorUnits / 60)),
          minimumBillableMinutes,
          roundingIncrementMinutes,
          effectiveFromUtc: tariffEffectiveFromUtc,
          isActive
        });
        if (!isActive) {
          setSelectedTariffVersionId('');
        }
        await loadSettings(nextBackend);
      } else if (label === 'Создать пакет') {
        if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
          throw new Error('Нет прав на управление пакетами.');
        }

        const name = packageName.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(packagePrice);
        const includedMinutes = Number(packageMinutes);
        const bonusMinutes = Number(packageBonusMinutes);
        const expiresDays = Number(packageExpiresDays);
        if (!name || priceMinorUnits === null || !Number.isInteger(includedMinutes) || includedMinutes <= 0
          || !Number.isInteger(bonusMinutes) || bonusMinutes < 0
          || !Number.isInteger(expiresDays) || expiresDays <= 0) {
          throw new Error('Заполните название, цену, минуты, бонус и срок действия пакета.');
        }

        await apiClients.settings.createPackageDefinition(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name,
          price: { currencyCode, minorUnits: priceMinorUnits },
          includedSeconds: includedMinutes * 60,
          bonusSeconds: bonusMinutes * 60,
          expiresAfterDays: expiresDays,
          idempotencyKey: createIdempotencyKey('package-definition-create')
        });
        await loadSettings(nextBackend);
      } else if (label === 'Обновить пакет' || label === 'Снять пакет') {
        if (!hasPermission(nextBackend.session, permissionNames.managePackages)) {
          throw new Error('Нет прав на управление пакетами.');
        }

        const packageDefinitionId = selectedPackageDefinitionId.trim();
        const name = packageName.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(packagePrice);
        const includedMinutes = Number(packageMinutes);
        const bonusMinutes = Number(packageBonusMinutes);
        const expiresDays = Number(packageExpiresDays);
        if (!isGuid(packageDefinitionId) || !name || priceMinorUnits === null || !Number.isInteger(includedMinutes) || includedMinutes <= 0
          || !Number.isInteger(bonusMinutes) || bonusMinutes < 0
          || !Number.isInteger(expiresDays) || expiresDays <= 0) {
          throw new Error('Выберите пакет и заполните название, цену, минуты, бонус и срок действия.');
        }

        await apiClients.settings.updatePackageDefinition(nextBackend.branchId, packageDefinitionId, {
          organizationId: nextBackend.session.organizationId,
          name,
          price: { currencyCode, minorUnits: priceMinorUnits },
          includedSeconds: includedMinutes * 60,
          bonusSeconds: bonusMinutes * 60,
          expiresAfterDays: expiresDays,
          isActive: label !== 'Снять пакет'
        });
        if (label === 'Снять пакет') {
          setSelectedPackageDefinitionId('');
        }
        await loadSettings(nextBackend);
      } else if (label === 'Пригласить сотрудника') {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error('Нет прав на управление сотрудниками.');
        }

        const userName = inviteUserName.trim();
        const displayName = inviteDisplayName.trim();
        const email = inviteEmail.trim();
        if (!userName || !displayName || !email) {
          throw new Error('Заполните имя пользователя, имя сотрудника и email.');
        }

        const invite = await apiClients.settings.createStaffInvite(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          userName,
          displayName,
          email,
          roleNames: [inviteRoleName || 'cashier_operator']
        });
        // The invitee appears in the staff list only after they accept and set a password.
        setInviteCode(invite.code);
        setInviteUserName(`operator${staffUsers.length + 2}`);
        setInviteDisplayName('Новый оператор');
        setInviteEmail('');
      } else if (label === 'Обновить профиль сотрудника') {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error('Нет прав на управление сотрудниками.');
        }

        const staffUserId = selectedStaffUserId.trim();
        const userName = staffProfileUserName.trim();
        const displayName = staffProfileDisplayName.trim();
        if (!isGuid(staffUserId) || !userName || !displayName) {
          throw new Error('Выберите сотрудника и заполните логин и имя профиля.');
        }

        const staffUser = await apiClients.settings.updateStaffUserProfile(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          userName,
          displayName
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffProfileUserName(readString(staffUser, 'userName'));
        setStaffProfileDisplayName(operatorDisplayNameLabel(readString(staffUser, 'displayName')));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? staffRoleName);
      } else if (label === 'Обновить роль') {
        if (!hasPermission(nextBackend.session, permissionNames.manageRoles)) {
          throw new Error('Нет прав на изменение ролей сотрудников.');
        }

        const staffUserId = selectedStaffUserId.trim();
        const roleName = staffRoleName.trim();
        if (!isGuid(staffUserId) || !staffRoleOptions.includes(roleName as (typeof staffRoleOptions)[number])) {
          throw new Error('Выберите сотрудника и роль.');
        }

        const staffUser = await apiClients.settings.updateStaffUserRoles(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          roleNames: [roleName]
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? roleName);
      } else if (label === 'Отключить сотрудника' || label === 'Включить сотрудника') {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error('Нет прав на управление сотрудниками.');
        }

        const staffUserId = selectedStaffUserId.trim();
        if (!isGuid(staffUserId)) {
          throw new Error('Выберите сотрудника.');
        }

        const staffUser = await apiClients.settings.updateStaffUserState(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          isActive: label === 'Включить сотрудника'
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? staffRoleName);
      } else if (label === 'Сбросить пароль') {
        if (!hasPermission(nextBackend.session, permissionNames.manageBranchStaff)) {
          throw new Error('Нет прав на управление сотрудниками.');
        }

        const staffUserId = selectedStaffUserId.trim();
        const nextPassword = resetPassword.trim();
        if (!isGuid(staffUserId) || nextPassword.length < 8) {
          throw new Error('Выберите сотрудника и задайте пароль не короче 8 символов.');
        }

        const staffUser = await apiClients.settings.resetStaffUserPassword(nextBackend.branchId, staffUserId, {
          organizationId: nextBackend.session.organizationId,
          newPassword: nextPassword
        });
        setStaffUsers((items) => items.map((item) => readString(item, 'staffUserId') === staffUserId ? staffUser : item));
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setResetPassword('');
      } else if (label === 'Создать товар') {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error('Нет прав на управление каталогом товаров.');
        }

        const categoryName = productCategoryName.trim();
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!categoryName || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error('Заполните категорию, товар, артикул и цену больше нуля.');
        }

        const category = await apiClients.settings.createProductCategory(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name: categoryName,
          idempotencyKey: createIdempotencyKey('pos-category-create')
        });
        const categoryId = readString(category, 'categoryId');
        if (!categoryId) {
          throw new Error('Категория создана, но платформа не подтвердила её. Повторите операцию или обратитесь в поддержку.');
        }

        const product = await apiClients.settings.createProduct(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          categoryId,
          name: nextProductName,
          sku,
          price: { currencyCode, minorUnits: priceMinorUnits },
          trackStock: productTrackStock,
          allowNegativeStock: productAllowNegativeStock,
          idempotencyKey: createIdempotencyKey('pos-product-create')
        });
        setCatalog((items) => [...items, product]);
        const nextIndex = catalog.length + 2;
        setProductCategoryName(`Категория ${nextIndex}`);
        setProductName(`Товар ${nextIndex}`);
        setProductSku(`SKU-${String(nextIndex).padStart(3, '0')}`);
        setProductPrice('12.00');
        setProductTrackStock(true);
        setProductAllowNegativeStock(false);
        setSelectedProductId(readString(product, 'productId'));
      } else if (label === 'Обновить товар' || label === 'Снять с продажи') {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error('Нет прав на управление каталогом товаров.');
        }

        const selectedProduct = catalog.find((product) => readString(product, 'productId') === selectedProductId);
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!selectedProduct || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error('Выберите товар и заполните товар, артикул и цену больше нуля.');
        }

        await apiClients.settings.updateProduct(nextBackend.branchId, readString(selectedProduct, 'productId'), {
          organizationId: nextBackend.session.organizationId,
          categoryId: readString(selectedProduct, 'categoryId'),
          name: nextProductName,
          sku,
          price: { currencyCode, minorUnits: priceMinorUnits },
          trackStock: productTrackStock,
          allowNegativeStock: productAllowNegativeStock,
          isActive: label !== 'Снять с продажи'
        });
        if (label === 'Снять с продажи') {
          setSelectedProductId('');
        }
        await loadSettings(nextBackend);
      } else if (label === 'Записать движение') {
        if (!hasPermission(nextBackend.session, permissionNames.manageInventoryStock)) {
          throw new Error('Нет прав на управление остатками.');
        }

        const selectedProduct = trackedCatalog.find((product) => readString(product, 'productId') === stockProductId);
        const quantityDelta = Number(stockQuantityDelta);
        const unitCostMinorUnits = parseNonNegativeMoneyInputMinorUnits(stockUnitCost);
        const reason = stockReason.trim();
        if (!selectedProduct || !Number.isInteger(quantityDelta) || quantityDelta === 0 || unitCostMinorUnits === null || !reason) {
          throw new Error('Выберите товар с учётом остатков, количество не равное нулю, себестоимость и причину.');
        }

        await apiClients.inventory.createStockMovement(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          productId: readString(selectedProduct, 'productId'),
          movementType: stockMovementType,
          quantityDelta,
          unitCost: { currencyCode, minorUnits: unitCostMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('stock-movement-create')
        });
        await loadSettings(nextBackend);
      } else if (label === 'Добавить пакет обновления') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdatePackages)) {
          throw new Error('Нет прав на управление пакетами обновлений.');
        }

        const component = updateComponent.trim();
        const version = updateVersion.trim();
        const channel = updateChannel.trim();
        const artifactUri = updateArtifactUri.trim();
        const sha256 = updateSha256.trim();
        const signature = updateSignature.trim();
        const signatureAlgorithm = updateSignatureAlgorithm.trim();
        const sizeKilobytes = Number(updateSizeKilobytes);
        const sizeBytes = sizeKilobytes * 1024;
        if (!component || !version || !channel || !artifactUri || !sha256 || !signature || !signatureAlgorithm
          || !Number.isInteger(sizeKilobytes) || sizeKilobytes <= 0) {
          throw new Error('Заполните приложение, версию, канал, ссылку на установщик, проверочную сумму, подпись, способ проверки подписи и размер файла.');
        }

        try {
          new URL(artifactUri);
        } catch {
          throw new Error('Ссылка на установщик должна быть полной.');
        }

        const createdPackage = await apiClients.updates.registerPackage(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          component,
          version,
          channel,
          artifactUri,
          sha256,
          signature,
          signatureAlgorithm,
          sizeBytes,
          releaseNotes: updateReleaseNotes.trim()
        });
        const updatePackageId = readString(createdPackage, 'updatePackageId');
        if (!isGuid(updatePackageId)) {
          throw new Error('Пакет обновления создан, но платформа не подтвердила его. Повторите операцию или обратитесь в поддержку.');
        }

        setRegisteredUpdatePackages((items) => [createdPackage, ...items.filter((item) => readString(item, 'updatePackageId') !== updatePackageId)]);
        setRolloutPackageId(updatePackageId);
        setPackageStatePackageId(updatePackageId);
      } else if (label === 'Создать публикацию обновления') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdateRollouts)) {
          throw new Error('Нет прав на управление публикациями обновлений.');
        }

        const updatePackageId = rolloutPackageId.trim();
        const channel = rolloutChannel.trim();
        const targetKind = rolloutTargetKind.trim();
        const batchPercent = Number(rolloutBatchPercent);
        const startsAtText = rolloutStartsAtUtc.trim();
        const startsAt = new Date(startsAtText);
        const targetDeviceIds = targetKind === 'device'
          ? rolloutTargetDeviceIds.split(/[\s,;]+/).map((value) => value.trim()).filter(Boolean)
          : [];
        if (!isGuid(updatePackageId) || !channel || (targetKind !== 'branch' && targetKind !== 'device')
          || !Number.isInteger(batchPercent) || batchPercent < 1 || batchPercent > 100
          || Number.isNaN(startsAt.getTime()) || !rolloutReason.trim()
          || targetDeviceIds.some((deviceId) => !isGuid(deviceId))) {
          throw new Error('Выберите пакет, канал, цель, долю, старт и причину публикации.');
        }

        const rollout = await apiClients.updates.createRollout(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          updatePackageId,
          channel,
          targetKind,
          targetDeviceIds,
          batchPercent,
          startsAtUtc: startsAt.toISOString(),
          reason: rolloutReason.trim()
        });
        const updateRolloutId = readString(rollout, 'updateRolloutId');
        if (updateRolloutId) {
          setRolloutStateRolloutId(updateRolloutId);
          setRollouts((items) => [rollout, ...items.filter((item) => readString(item, 'updateRolloutId') !== updateRolloutId)]);
        }
      } else if (label === 'Изменить состояние пакета') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdatePackages)) {
          throw new Error('Нет прав на управление пакетами обновлений.');
        }

        const updatePackageId = packageStatePackageId.trim();
        const state = packageState.trim();
        const reason = packageStateReason.trim();
        if (!isGuid(updatePackageId) || !state || !reason) {
          throw new Error('Выберите пакет, состояние и причину.');
        }

        const updatePackage = await apiClients.updates.changePackageState(nextBackend.branchId, updatePackageId, {
          organizationId: nextBackend.session.organizationId,
          state,
          reason
        });
        setRegisteredUpdatePackages((items) => [updatePackage, ...items.filter((item) => readString(item, 'updatePackageId') !== updatePackageId)]);
      } else if (label === 'Изменить состояние публикации') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdateRollouts)) {
          throw new Error('Нет прав на управление публикациями обновлений.');
        }

        const updateRolloutId = rolloutStateRolloutId.trim();
        const state = rolloutState.trim();
        const reason = rolloutStateReason.trim();
        if (!isGuid(updateRolloutId) || !state || !reason) {
          throw new Error('Выберите публикацию, состояние и причину.');
        }

        const rollout = await apiClients.updates.changeRolloutState(nextBackend.branchId, updateRolloutId, {
          organizationId: nextBackend.session.organizationId,
          state,
          reason
        });
        setRollouts((items) => items.map((item) => readString(item, 'updateRolloutId') === updateRolloutId ? rollout : item));
      } else {
        throw new Error('Действие настроек пока не подключено к платформе.');
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const saveSettings = async () => {
    if (!clubName.trim() || !city.trim()) {
      triggerFeedback(setFeedback, 'Проверить обязательные поля', 'failed', 'Заполните обязательные поля.');
      return;
    }

    setFeedback({ label: 'Профиль клуба', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const branchProfile: BranchProfileDto = await apiClients.settings.updateBranchProfile(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        name: clubName.trim(),
        city: city.trim()
      });
      setClubName(readString(branchProfile, 'name', clubName.trim()));
      setCity(readString(branchProfile, 'city', city.trim()));
      setSettingsDirty(false);
      setFeedback({ label: 'Профиль клуба', state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: 'Профиль клуба', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const renderSettingsContent = () => {
    if (selectedSection === 'Залы и ПК') {
      return (
        <>
          <div className="settings-section-title">
            <span>Залы и рабочие места</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManageLayout} onClick={() => runSettingsAction('Добавить зал')}>Создать зал</button>
              <button type="button" disabled={!canManageLayout || !selectedLayoutZoneId} onClick={() => runSettingsAction('Обновить зал')}>Обновить зал</button>
              <button
                type="button"
                disabled={!canManageLayout || !selectedLayoutZoneId}
                onClick={() => {
                  setFeedback(emptyFeedback);
                  setCriticalAction('layout-zone-delete');
                }}
              >
                Удалить зал
              </button>
            </div>
          </div>
          <div className="settings-form-grid settings-layout-form">
            <label>Название зала<input value={layoutZoneName} disabled={!canManageLayout} onChange={(event) => setLayoutZoneName(event.currentTarget.value)} /></label>
            <label>Порядок зала<input inputMode="numeric" value={layoutZoneSortOrder} disabled={!canManageLayout} onChange={(event) => setLayoutZoneSortOrder(event.currentTarget.value)} /></label>
            <label>Зона ПК
              <select value={layoutSeatZoneId} disabled={!canManageLayout || zones.length === 0} onChange={(event) => setLayoutSeatZoneId(event.currentTarget.value)}>
                {zones.length === 0 && <option value="">нет залов</option>}
                {zones.map((zone) => (
                  <option key={readString(zone, 'zoneId')} value={readString(zone, 'zoneId')}>{readString(zone, 'name', 'Зал')}</option>
                ))}
              </select>
            </label>
            <label>Название ПК<input value={layoutSeatName} disabled={!canManageLayout} onChange={(event) => setLayoutSeatName(event.currentTarget.value)} /></label>
            <label>Порядок ПК<input inputMode="numeric" value={layoutSeatSortOrder} disabled={!canManageLayout} onChange={(event) => setLayoutSeatSortOrder(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canManageLayout || !layoutSeatZoneId} onClick={() => runSettingsAction('Добавить ПК')}>Создать ПК</button>
            <button type="button" disabled={!canManageLayout || !selectedLayoutSeatId || !layoutSeatZoneId} onClick={() => runSettingsAction('Обновить ПК')}>Обновить ПК</button>
            <button
              type="button"
              disabled={!canManageLayout || !selectedLayoutSeatId}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('layout-seat-delete');
              }}
            >
              Удалить ПК
            </button>
          </div>
          {criticalAction === 'layout-zone-delete' && (
            <CriticalActionConfirmation
              title="Подтвердите удаление зала"
              detail={`${readString(selectedLayoutZone, 'name', layoutZoneName || 'Зал')} · ${readArray(selectedLayoutZone, 'seats').length} ПК`}
              impact="Зал будет удален из схемы клуба. Удаление доступно только для пустых залов."
              confirmLabel="Подтвердить удаление зала"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Удалить зал')}
            />
          )}
          {criticalAction === 'layout-seat-delete' && (
            <CriticalActionConfirmation
              title="Подтвердите удаление ПК"
              detail={readString(selectedLayoutSeat, 'name', layoutSeatName || 'ПК')}
              impact="Рабочее место будет удалено из схемы клуба. Проверьте активные сессии и привязку устройства до удаления."
              confirmLabel="Подтвердить удаление ПК"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Удалить ПК')}
            />
          )}
          <div className="settings-room-grid">
            {zones.map((zone) => (
              <button key={readString(zone, 'zoneId')} type="button" className={`settings-room-card ${readString(zone, 'zoneId') === selectedLayoutZoneId ? 'active' : ''}`} onClick={() => selectLayoutZone(zone)}>
                <strong>{readString(zone, 'name', 'Зал')}</strong>
                <b>{readArray(zone, 'seats').length} ПК</b>
                <span>порядок {readNumber(zone, 'sortOrder', 0)}</span>
              </button>
            ))}
          </div>
          <div className="settings-tariff-list">
            {zones.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats').map((seat) => (
              <button key={readString(seat, 'seatId')} type="button" className={`settings-tariff-row ${readString(seat, 'seatId') === selectedLayoutSeatId ? 'active' : ''}`} onClick={() => selectLayoutSeat(zone, seat)}>
                <strong>{readString(seat, 'name', 'ПК')}</strong>
                <b>{readString(zone, 'name', 'Зал')}</b>
                <span>порядок {readNumber(seat, 'sortOrder', 0)}</span>
              </button>
            )))}
          </div>
          <div className="settings-section-title">
            <span>Подключение устройств</span>
            <button type="button" disabled={!canCreateDeviceEnrollmentCode} onClick={() => runSettingsAction('Создать код подключения')}>Создать код подключения</button>
            <button type="button" disabled={!canAssignDeviceSeat || layoutSeatOptions.length === 0} onClick={() => runSettingsAction('Назначить устройство')}>Назначить устройство</button>
            <button type="button" disabled={!canViewDeviceCommandStatus} onClick={() => runSettingsAction('Обновить историю команд')}>Обновить историю команд</button>
          </div>
          <div className="settings-form-grid settings-device-form">
            <label>Срок действия кода, минут<input inputMode="numeric" value={enrollmentExpiresMinutes} disabled={!canCreateDeviceEnrollmentCode} onChange={(event) => setEnrollmentExpiresMinutes(event.currentTarget.value)} /></label>
            <label>Код подключения<input value={readString(enrollmentCode, 'code', '—')} readOnly /></label>
            <label>Устройство
              <select value={deviceAssignmentDeviceId} disabled={deviceOptions.length === 0 || (!canAssignDeviceSeat && !canViewDeviceDetail)} onChange={(event) => setDeviceAssignmentDeviceId(event.currentTarget.value)}>
                {deviceOptions.length === 0 && <option value="">нет подключенных устройств</option>}
                {deviceAssignmentDeviceId && !deviceOptions.some((device) => device.id === deviceAssignmentDeviceId) && (
                  <option value={deviceAssignmentDeviceId}>выбранное устройство</option>
                )}
                {deviceOptions.map((device) => (
                  <option key={device.id} value={device.id}>{device.label}</option>
                ))}
              </select>
            </label>
            <label>Рабочее место
              <select value={deviceAssignmentSeatId} disabled={!canAssignDeviceSeat || layoutSeatOptions.length === 0} onChange={(event) => setDeviceAssignmentSeatId(event.currentTarget.value)}>
                {layoutSeatOptions.length === 0 && <option value="">нет рабочих мест</option>}
                {layoutSeatOptions.map((seat) => (
                  <option key={seat.seatId} value={seat.seatId}>{seat.label}</option>
                ))}
              </select>
            </label>
            <label>Карточка устройства<input value={deviceDetail ? `${readString(deviceDetail, 'machineName', 'Устройство')} · ${readString(deviceDetail, 'seatName', 'без места')}` : 'не открыта'} readOnly /></label>
            <button type="button" disabled={!canViewDeviceDetail || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction('Открыть карточку устройства')}>Открыть карточку устройства</button>
            <label>Команда
              <select value={deviceCommandType} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandType(event.currentTarget.value)}>
                <option value="lock">блокировка</option>
                <option value="unlock">разблокировка</option>
              </select>
            </label>
            <label>Причина команды<input value={deviceCommandReason} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandReason(event.currentTarget.value)} /></label>
            <label>Последняя команда<input value={lastDeviceCommand ? `${commandTypeLabel(readString(lastDeviceCommand, 'type', 'command'))} · отправлена` : 'не отправлена'} readOnly /></label>
            <button type="button" disabled={!canDispatchDeviceCommand || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction('Отправить команду')}>Отправить команду</button>
            <label>Ключ для отзыва<input value={rotatedCredentialLabel} readOnly /></label>
            <label>Новый ключ устройства<input value={rotatedCredentialId ? 'создан' : '—'} readOnly /></label>
            <label className="settings-form-wide">Код нового подключения<input value={readString(rotatedCredential, 'credentialSecret', '—')} readOnly /></label>
            <button type="button" disabled={!canRotateDeviceCredential || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction('Выдать новый ключ')}>Выдать новый ключ</button>
            <button
              type="button"
              disabled={!canRevokeDeviceCredential || !isGuid(deviceAssignmentDeviceId) || !isGuid(credentialIdToRevoke) || feedback.state === 'pending'}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('credential-revoke');
              }}
            >
              Отозвать ключ
            </button>
          </div>
          {criticalAction === 'credential-revoke' && (
            <CriticalActionConfirmation
              title="Подтвердите отзыв ключа"
              detail={`${selectedDeviceLabel} · новый ключ устройства`}
              impact="После отзыва этот ключ больше нельзя использовать для подключения выбранного ПК."
              confirmLabel="Отозвать ключ"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Отозвать ключ')}
            />
          )}
          <div className="settings-device-inventory" aria-label="Устройства филиала">
            {deviceInventory.map((device) => {
              const pendingCommands = readNumber(device, 'pendingCommandCount', 0);
              const failedCommands = readNumber(device, 'failedCommandCount', 0);
              return (
                <button
                  key={readString(device, 'deviceId')}
                  type="button"
                  aria-label={readString(device, 'machineName', 'Устройство')}
                  className={`settings-device-row ${readString(device, 'deviceId') === deviceAssignmentDeviceId ? 'active' : ''}${failedCommands > 0 ? ' attention' : ''}`}
                  disabled={!canViewDeviceDetail}
                  onClick={() => selectDeviceInventoryItem(device)}
                >
                  <strong>{readString(device, 'machineName', 'Устройство')}</strong>
                  <b>{readBoolean(device, 'isOnline') ? 'онлайн' : 'офлайн'} · {readBoolean(device, 'isLocked') ? 'заблокирован' : 'разблокирован'}</b>
                  <span>{readString(device, 'zoneName', 'без зала')} · {readString(device, 'seatName', 'без места')}</span>
                  <em>Агент {readString(device, 'agentVersion', '—')} · приложений {readNumber(device, 'installedAppCount', 0)} · в работе {pendingCommands} · ошибок {failedCommands} · {formatTime(readString(device, 'lastHeartbeatAtUtc'))}</em>
                </button>
              );
            })}
            {deviceInventory.length === 0 && (
              <span className="settings-device-empty">Нет подключенных устройств в филиале</span>
            )}
          </div>
          {deviceDetail && (
            <div className="settings-device-detail-grid">
              <span><strong>Устройство</strong><b>{readString(deviceDetail, 'machineName', 'Устройство')}</b></span>
              <span><strong>Статус</strong><b>{readBoolean(deviceDetail, 'isOnline') ? 'онлайн' : 'офлайн'} · {readBoolean(deviceDetail, 'isLocked') ? 'заблокирован' : 'разблокирован'}</b></span>
              <span><strong>Место</strong><b>{readString(deviceDetail, 'zoneName', 'без зала')} · {readString(deviceDetail, 'seatName', 'без места')}</b></span>
              <span><strong>Пульс</strong><b>{formatTime(readString(deviceDetail, 'lastHeartbeatAtUtc'))}</b></span>
              <span><strong>Агент</strong><b>{readString(deviceDetail, 'agentVersion', '—')}</b></span>
              <span><strong>Оболочка</strong><b>{readString(deviceDetail, 'shellVersion', '—')}</b></span>
              <span><strong>Ключи</strong><b>{readNumber(deviceDetail, 'activeCredentialCount', 0)}</b></span>
              <span><strong>Приложения</strong><b>{readNumber(deviceDetail, 'installedAppCount', 0)}</b></span>
            </div>
          )}
          {deviceRecentCommands.length > 0 && (
            <div className="settings-command-history">
              {deviceRecentCommands.map((command) => (
                <span key={readString(command, 'commandId')}>
                  <strong>{commandTypeLabel(readString(command, 'type', 'command'))}</strong>
                  <b>{commandStatusLabel(readString(command, 'status', 'unknown'))}</b>
                  <em>{commandStatusMessageLabel(readString(command, 'message')) || formatTime(readString(command, 'updatedAtUtc'))}</em>
                </span>
              ))}
            </div>
          )}
          {branchDeviceCommandHistory.length > 0 && (
            <>
              <div className="settings-section-title">
                <span>История команд филиала</span>
                <strong>{branchDeviceCommandHistory.length} последних</strong>
              </div>
              <div className="settings-command-history" aria-label="История команд филиала">
                {branchDeviceCommandHistory.map((command) => {
                  const deviceId = readString(command, 'deviceId');
                  return (
                    <span key={`${deviceId}-${readString(command, 'commandId')}`}>
                      <strong>{getDeviceInventoryName(deviceId)}</strong>
                      <b>{commandTypeLabel(readString(command, 'type', 'command'))} · {commandStatusLabel(readString(command, 'status', 'unknown'))}</b>
                      <em>{commandStatusMessageLabel(readString(command, 'message')) || formatTime(readString(command, 'updatedAtUtc'))}</em>
                    </span>
                  );
                })}
              </div>
            </>
          )}
        </>
      );
    }

    if (selectedSection === 'Тарифы') {
      return (
        <>
          <div className="settings-section-title">
            <span>Тарифы</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManageTariffs} onClick={() => runSettingsAction('Создать тариф')}>Создать тариф</button>
              <button type="button" disabled={!canManageTariffs || !selectedTariffVersionId} onClick={() => runSettingsAction('Обновить тариф')}>Обновить тариф</button>
              <button type="button" disabled={!canManageTariffs || !selectedTariffVersionId} onClick={() => runSettingsAction('Снять тариф')}>Снять тариф</button>
            </div>
          </div>
          <div className="settings-form-grid settings-tariff-form">
            <label>Название тарифа<input value={tariffName} disabled={!canManageTariffs} onChange={(event) => setTariffName(event.currentTarget.value)} /></label>
            <label>Цена за час<input inputMode="decimal" value={tariffPricePerHour} disabled={!canManageTariffs} onChange={(event) => setTariffPricePerHour(event.currentTarget.value)} /></label>
            <label>Минимум, мин<input inputMode="numeric" value={tariffMinimumMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffMinimumMinutes(event.currentTarget.value)} /></label>
            <label>Шаг округления, мин<input inputMode="numeric" value={tariffRoundingMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffRoundingMinutes(event.currentTarget.value)} /></label>
          </div>
          <div className="settings-tariff-list">
            {tariffs.map((tariff) => (
              <button key={readString(tariff, 'tariffVersionId')} type="button" className={`settings-tariff-row ${readString(tariff, 'tariffVersionId') === selectedTariffVersionId ? 'active' : ''}`} onClick={() => selectTariffOption(tariff)}>
                <strong>{readString(tariff, 'name', 'Тариф')}</strong>
                <b>{formatMinorUnits(readNumber(tariff, 'pricePerMinuteMinorUnits', 0) * 60, readString(tariff, 'currencyCode', currencyCode))} / час</b>
                <span>минимум {readNumber(tariff, 'minimumBillableMinutes', 0)} мин · шаг {readNumber(tariff, 'roundingIncrementMinutes', 0)} мин · {readBoolean(tariff, 'isActive', true) ? 'активен' : 'снят'}</span>
              </button>
            ))}
          </div>
          <div className="settings-section-title">
            <span>Пакеты</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManagePackages} onClick={() => runSettingsAction('Создать пакет')}>Создать пакет</button>
              <button type="button" disabled={!canManagePackages || !selectedPackageDefinitionId} onClick={() => runSettingsAction('Обновить пакет')}>Обновить пакет</button>
              <button type="button" disabled={!canManagePackages || !selectedPackageDefinitionId} onClick={() => runSettingsAction('Снять пакет')}>Снять пакет</button>
            </div>
          </div>
          <div className="settings-tariff-list">
            {packageOptions.map((option) => (
              <button key={readString(option, 'packageDefinitionId')} type="button" className={`settings-tariff-row ${readString(option, 'packageDefinitionId') === selectedPackageDefinitionId ? 'active' : ''}`} onClick={() => selectPackageOption(option)}>
                <strong>{readString(option, 'name', 'Пакет')}</strong>
                <b>{formatMinorUnits(readNumber(option, 'priceMinorUnits', 0), readString(option, 'currencyCode', currencyCode))}</b>
                <span>{Math.round(readNumber(option, 'includedSeconds', 0) / 60)} мин · +{Math.round(readNumber(option, 'bonusSeconds', 0) / 60)} бонус · действует {readNumber(option, 'expiresAfterDays', 0)} дн.</span>
              </button>
            ))}
          </div>
          <div className="settings-form-grid settings-package-form">
            <label>Название пакета<input value={packageName} disabled={!canManagePackages} onChange={(event) => setPackageName(event.currentTarget.value)} /></label>
            <label>Цена<input inputMode="decimal" value={packagePrice} disabled={!canManagePackages} onChange={(event) => setPackagePrice(event.currentTarget.value)} /></label>
            <label>Включено, мин<input inputMode="numeric" value={packageMinutes} disabled={!canManagePackages} onChange={(event) => setPackageMinutes(event.currentTarget.value)} /></label>
            <label>Бонус, мин<input inputMode="numeric" value={packageBonusMinutes} disabled={!canManagePackages} onChange={(event) => setPackageBonusMinutes(event.currentTarget.value)} /></label>
            <label>Срок, дней<input inputMode="numeric" value={packageExpiresDays} disabled={!canManagePackages} onChange={(event) => setPackageExpiresDays(event.currentTarget.value)} /></label>
          </div>
        </>
      );
    }

    if (selectedSection === 'Персонал') {
      return (
        <>
          <div className="settings-section-title">
            <span>Сотрудники</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManageBranchStaff} onClick={() => runSettingsAction('Пригласить сотрудника')}>Пригласить сотрудника</button>
              <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runSettingsAction(selectedStaffIsActive ? 'Отключить сотрудника' : 'Включить сотрудника')}>
                {selectedStaffIsActive ? 'Отключить сотрудника' : 'Включить сотрудника'}
              </button>
              <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runSettingsAction('Сбросить пароль')}>Сбросить пароль</button>
            </div>
          </div>
          <div className="settings-staff-layout">
            <div className="settings-config-grid">
              {staffUsers.map((user) => (
                <button
                  key={readString(user, 'staffUserId')}
                  type="button"
                  className={readString(user, 'staffUserId') === selectedStaffUserId ? 'active' : ''}
                  onClick={() => {
                    const roleName = readArray<string>(user, 'roleNames').find((role) => staffRoleOptions.includes(role as (typeof staffRoleOptions)[number]));
                    setSelectedStaffUserId(readString(user, 'staffUserId'));
                    setStaffProfileUserName(readString(user, 'userName'));
                    setStaffProfileDisplayName(operatorDisplayNameLabel(readString(user, 'displayName')));
                    setStaffRoleName(roleName ?? 'cashier_operator');
                    triggerFeedback(setFeedback, operatorDisplayNameLabel(readString(user, 'displayName', 'Сотрудник')), 'confirmed');
                  }}
                >
                  <strong>{operatorDisplayNameLabel(readString(user, 'displayName', 'Сотрудник'))}</strong>
                  <span>{readString(user, 'userName', 'Пользователь')} · {readArray<string>(user, 'roleNames').map(staffRoleLabel).join(', ') || 'роль не задана'} · {readBoolean(user, 'isActive', true) ? 'активен' : 'отключен'}</span>
                </button>
              ))}
            </div>
            <div className="settings-form-grid settings-staff-form">
              <label>Логин для входа<input value={inviteUserName} disabled={!canManageBranchStaff} onChange={(event) => setInviteUserName(event.currentTarget.value)} /></label>
              <label>Имя в смене<input value={inviteDisplayName} disabled={!canManageBranchStaff} onChange={(event) => setInviteDisplayName(event.currentTarget.value)} /></label>
              <label>Email для приглашения<input type="email" value={inviteEmail} disabled={!canManageBranchStaff} onChange={(event) => setInviteEmail(event.currentTarget.value)} /></label>
              {inviteCode && <label>Код приглашения<input readOnly value={inviteCode} onFocus={(event) => event.currentTarget.select()} /></label>}
              <label>Роль доступа
                <select value={inviteRoleName} disabled={!canManageBranchStaff} onChange={(event) => setInviteRoleName(event.currentTarget.value)}>
                  {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{staffRoleLabel(roleName)}</option>)}
                </select>
              </label>
              <label>Логин профиля<input value={staffProfileUserName} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setStaffProfileUserName(event.currentTarget.value)} /></label>
              <label>Имя профиля<input value={staffProfileDisplayName} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setStaffProfileDisplayName(event.currentTarget.value)} /></label>
              <button type="button" disabled={!canManageBranchStaff || !selectedStaffUserId} onClick={() => runSettingsAction('Обновить профиль сотрудника')}>Обновить профиль</button>
              <label>Новая роль
                <select value={staffRoleName} disabled={!canManageRoles || !selectedStaffUserId} onChange={(event) => setStaffRoleName(event.currentTarget.value)}>
                  {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{staffRoleLabel(roleName)}</option>)}
                </select>
              </label>
              <label>Новый пароль для входа<input type="password" value={resetPassword} disabled={!canManageBranchStaff || !selectedStaffUserId} onChange={(event) => setResetPassword(event.currentTarget.value)} /></label>
              <button type="button" disabled={!canManageRoles || !selectedStaffUserId} onClick={() => runSettingsAction('Обновить роль')}>Обновить роль</button>
            </div>
          </div>
        </>
      );
    }

    if (selectedSection === 'Товары и склад') {
      return (
        <>
          <div className="settings-section-title">
            <span>Каталог товаров</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManagePosCatalog} onClick={() => runSettingsAction('Создать товар')}>Создать товар</button>
              <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runSettingsAction('Обновить товар')}>Обновить товар</button>
              <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runSettingsAction('Снять с продажи')}>Снять с продажи</button>
            </div>
          </div>
          <div className="settings-config-grid">
            {catalog.slice(0, 8).map((product) => (
              <button key={readString(product, 'productId')} type="button" className={readString(product, 'productId') === selectedProductId ? 'active' : undefined} onClick={() => selectCatalogProduct(product)}>
                <strong>{readString(product, 'name', 'Товар')}</strong>
                <span>{formatMoney(readMoney(product, 'price'), currencyCode)} · остаток {readNumber(product, 'stockOnHand', 0)}</span>
              </button>
            ))}
          </div>
          <div className="settings-form-grid settings-pos-form">
            <label>Категория<input value={productCategoryName} disabled={!canManagePosCatalog} onChange={(event) => setProductCategoryName(event.currentTarget.value)} /></label>
            <label>Товар<input value={productName} disabled={!canManagePosCatalog} onChange={(event) => setProductName(event.currentTarget.value)} /></label>
            <label>Артикул<input value={productSku} disabled={!canManagePosCatalog} onChange={(event) => setProductSku(event.currentTarget.value)} /></label>
            <label>Цена<input inputMode="decimal" value={productPrice} disabled={!canManagePosCatalog} onChange={(event) => setProductPrice(event.currentTarget.value)} /></label>
            <label>Учёт остатков
              <select value={productTrackStock ? 'yes' : 'no'} disabled={!canManagePosCatalog} onChange={(event) => setProductTrackStock(event.currentTarget.value === 'yes')}>
                <option value="yes">да</option>
                <option value="no">нет</option>
              </select>
            </label>
            <label>Минусовой остаток
              <select value={productAllowNegativeStock ? 'yes' : 'no'} disabled={!canManagePosCatalog} onChange={(event) => setProductAllowNegativeStock(event.currentTarget.value === 'yes')}>
                <option value="no">нет</option>
                <option value="yes">да</option>
              </select>
            </label>
          </div>
          <div className="settings-section-title">
            <span>Остатки</span>
            <button type="button" disabled={!canManageInventoryStock || trackedCatalog.length === 0} onClick={() => runSettingsAction('Записать движение')}>Записать движение</button>
          </div>
          <div className="settings-form-grid settings-stock-form">
            <label>Товар склада
              <select value={stockProductId} disabled={!canManageInventoryStock || trackedCatalog.length === 0} onChange={(event) => setStockProductId(event.currentTarget.value)}>
                {trackedCatalog.length === 0 && <option value="">нет товаров с остатками</option>}
                {trackedCatalog.map((product) => (
                  <option key={readString(product, 'productId')} value={readString(product, 'productId')}>{readString(product, 'name', 'Товар')} · остаток {readNumber(product, 'stockOnHand', 0)}</option>
                ))}
              </select>
            </label>
            <label>Тип
              <select value={stockMovementType} disabled={!canManageInventoryStock} onChange={(event) => setStockMovementType(event.currentTarget.value)}>
                <option value="purchase">Приход</option>
                <option value="adjustment">Коррекция</option>
              </select>
            </label>
            <label>Кол-во<input inputMode="numeric" value={stockQuantityDelta} disabled={!canManageInventoryStock} onChange={(event) => setStockQuantityDelta(event.currentTarget.value)} /></label>
            <label>Себестоимость<input inputMode="decimal" value={stockUnitCost} disabled={!canManageInventoryStock} onChange={(event) => setStockUnitCost(event.currentTarget.value)} /></label>
            <label>Причина<input value={stockReason} disabled={!canManageInventoryStock} onChange={(event) => setStockReason(event.currentTarget.value)} /></label>
          </div>
          <div className="settings-section-title">
            <span>История склада</span>
            <strong>{stockMovements.length} последних</strong>
          </div>
          <div className="settings-config-grid settings-stock-history">
            {stockMovements.length === 0 && (
              <button type="button" disabled>
                <strong>Нет движений</strong>
                <span>движений по складу пока нет</span>
              </button>
            )}
            {stockMovements.map((movement) => {
              const productId = readString(movement, 'productId');
              const productName = readString(
                catalog.find((product) => readString(product, 'productId') === productId),
                'name',
                'Товар');
              const quantityDelta = readNumber(movement, 'quantityDelta', 0);
              const reason = readString(movement, 'reason', 'причина не указана');
              return (
                <button key={readString(movement, 'stockMovementId')} type="button" onClick={() => triggerFeedback(setFeedback, productName, 'confirmed')}>
                  <strong>{productName} · {stockMovementTypeLabel(readString(movement, 'movementType'))}</strong>
                  <span>{quantityDelta > 0 ? '+' : ''}{quantityDelta} · {formatMoney(readMoney(movement, 'unitCost'), currencyCode)} · {reason}</span>
                </button>
              );
            })}
          </div>
        </>
      );
    }

    if (selectedSection === 'Интеграции') {
      return (
        <>
          <div className="settings-config-grid">
            {[
              ['Платежи', 'ручное подтверждение'],
              ['Обновления', `публикаций: ${rollouts.length}`],
              ['Ошибки обновлений', `ПК с ошибками: ${readNumber(updateSummary, 'failedDevices', 0)}`],
              ['Связь', backend ? 'подключена' : 'локальные данные']
            ].map(([name, detail]) => (
              <button key={name} type="button" onClick={() => triggerFeedback(setFeedback, name, 'confirmed')}>
                <strong>{name}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>

          <div className="settings-section-title">
            <span>Пакеты для обновления</span>
            <button type="button" disabled={!canManageUpdatePackages} onClick={() => runSettingsAction('Добавить пакет обновления')}>Добавить пакет</button>
          </div>
          <div className="settings-form-grid settings-update-form">
            <label>Приложение
              <select value={updateComponent} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateComponent(event.currentTarget.value)}>
                <option value="operator-app">{updateComponentLabel('operator-app')}</option>
                <option value="agent-service">{updateComponentLabel('agent-service')}</option>
                <option value="player-shell">{updateComponentLabel('player-shell')}</option>
              </select>
            </label>
            <label>Версия<input value={updateVersion} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateVersion(event.currentTarget.value)} /></label>
            <label>Канал
              <select value={updateChannel} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateChannel(event.currentTarget.value)}>
                <option value="internal">{updateChannelLabel('internal')}</option>
                <option value="beta">{updateChannelLabel('beta')}</option>
                <option value="stable">{updateChannelLabel('stable')}</option>
              </select>
            </label>
            <label className="settings-form-wide">Файл установщика<input value={updateArtifactUri} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateArtifactUri(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Проверочная сумма<input value={updateSha256} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSha256(event.currentTarget.value)} /></label>
            <label>Подпись пакета<input value={updateSignature} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSignature(event.currentTarget.value)} /></label>
            <label>Способ проверки подписи<input value={updateSignatureAlgorithm} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSignatureAlgorithm(event.currentTarget.value)} /></label>
            <label>Размер файла, КБ<input inputMode="numeric" value={updateSizeKilobytes} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSizeKilobytes(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Описание релиза<input value={updateReleaseNotes} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateReleaseNotes(event.currentTarget.value)} /></label>
          </div>

          <div className="settings-section-title">
            <span>Публикации обновлений</span>
            <button type="button" disabled={!canManageUpdateRollouts} onClick={() => runSettingsAction('Создать публикацию обновления')}>Создать публикацию</button>
          </div>
          <div className="settings-tariff-list">
            {rollouts.map((rollout) => (
              <button
                key={readString(rollout, 'updateRolloutId')}
                type="button"
                className={`settings-tariff-row ${readString(rollout, 'updateRolloutId') === readString(selectedRollout, 'updateRolloutId') ? 'active' : ''}`}
                onClick={() => {
                  setRolloutStateRolloutId(readString(rollout, 'updateRolloutId'));
                  setRolloutPackageId(readString(rollout, 'updatePackageId'));
                  setPackageStatePackageId(readString(rollout, 'updatePackageId'));
                }}
              >
                <strong>{updateComponentLabel(readString(rollout, 'component'))} {readString(rollout, 'version', 'версия')}</strong>
                <b>{updateRolloutStateLabel(readString(rollout, 'state'))}</b>
                <span>{updateTargetKindLabel(readString(rollout, 'targetKind'))} · {readNumber(rollout, 'batchPercent', 0)}% · ПК: {readArray(rollout, 'deviceStatuses').length}</span>
              </button>
            ))}
          </div>
          {selectedRollout && (
            <div className="settings-device-detail-grid">
              <span><strong>Публикация</strong><b>{updateComponentLabel(readString(selectedRollout, 'component'))} {readString(selectedRollout, 'version', 'версия')}</b></span>
              <span><strong>Состояние</strong><b>{updateRolloutStateLabel(readString(selectedRollout, 'state'))}</b></span>
              <span><strong>Цель</strong><b>{updateTargetKindLabel(readString(selectedRollout, 'targetKind'))} · {readNumber(selectedRollout, 'batchPercent', 0)}%</b></span>
              <span><strong>Канал</strong><b>{updateChannelLabel(readString(selectedRollout, 'channel'))}</b></span>
              <span><strong>Пакет</strong><b>{updateComponentLabel(readString(selectedRollout, 'component'))} {readString(selectedRollout, 'version', 'версия')}</b></span>
              <span><strong>Старт</strong><b>{formatTime(readString(selectedRollout, 'startsAtUtc'))}</b></span>
              <span><strong>Завершено</strong><b>{formatTime(readString(selectedRollout, 'completedAtUtc'))}</b></span>
              <span><strong>ПК</strong><b>{selectedRolloutDeviceStatuses.length}</b></span>
            </div>
          )}
          {selectedRolloutDeviceStatuses.length > 0 && (
            <div className="settings-command-history">
              {selectedRolloutDeviceStatuses.map((status) => (
                <span key={`${readString(status, 'deviceId')}-${readString(status, 'updatedAtUtc')}`}>
                  <strong>{getDeviceInventoryName(readString(status, 'deviceId'))}</strong>
                  <b>{updateDeviceStatusLabel(readString(status, 'status', 'unknown'))}</b>
                  <em>{updateDeviceMessageLabel(readString(status, 'message')) || `${readString(status, 'installedVersion')} → ${readString(status, 'targetVersion')}`}</em>
                </span>
              ))}
            </div>
          )}
          <div className="settings-form-grid settings-update-form">
            <label className="settings-form-wide">Пакет для публикации
              <select value={rolloutPackageId} disabled={!canManageUpdateRollouts || updatePackageOptions.length === 0} onChange={(event) => setRolloutPackageId(event.currentTarget.value)}>
                {updatePackageOptions.length === 0 && <option value="">сначала зарегистрируйте пакет</option>}
                {rolloutPackageId && !updatePackageOptions.some((option) => option.id === rolloutPackageId) && (
                  <option value={rolloutPackageId}>выбранный пакет</option>
                )}
                {updatePackageOptions.map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>Канал
              <select value={rolloutChannel} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutChannel(event.currentTarget.value)}>
                <option value="internal">{updateChannelLabel('internal')}</option>
                <option value="beta">{updateChannelLabel('beta')}</option>
                <option value="stable">{updateChannelLabel('stable')}</option>
              </select>
            </label>
            <label>Цель
              <select value={rolloutTargetKind} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutTargetKind(event.currentTarget.value)}>
                <option value="branch">{updateTargetKindLabel('branch')}</option>
                <option value="device">{updateTargetKindLabel('device')}</option>
              </select>
            </label>
            <label>Доля %<input inputMode="numeric" value={rolloutBatchPercent} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutBatchPercent(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Целевые ПК
              <select multiple value={Array.from(rolloutTargetDeviceIdSet)} disabled={!canManageUpdateRollouts || rolloutTargetKind !== 'device' || deviceOptions.length === 0} onChange={(event) => setRolloutTargetDeviceIds(Array.from(event.currentTarget.selectedOptions).map((option) => option.value).join(','))}>
                {deviceOptions.length === 0 && <option value="">нет подключенных устройств</option>}
                {deviceOptions.map((device) => (
                  <option key={device.id} value={device.id}>{device.label}</option>
                ))}
              </select>
            </label>
            <label className="settings-form-wide">Начало публикации<input value={rolloutStartsAtUtc} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStartsAtUtc(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Причина публикации<input value={rolloutReason} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutReason(event.currentTarget.value)} /></label>
          </div>

          <div className="settings-section-title">
            <span>Состояния обновлений</span>
            <button
              type="button"
              disabled={!canManageUpdatePackages}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('package-state-change');
              }}
            >
              Изменить состояние пакета
            </button>
            <button
              type="button"
              disabled={!canManageUpdateRollouts}
              onClick={() => {
                setFeedback(emptyFeedback);
                setCriticalAction('rollout-state-change');
              }}
            >
              Изменить состояние публикации
            </button>
          </div>
          <div className="settings-form-grid settings-update-form">
            <label className="settings-form-wide">Пакет обновления
              <select value={packageStatePackageId} disabled={!canManageUpdatePackages || updatePackageOptions.length === 0} onChange={(event) => setPackageStatePackageId(event.currentTarget.value)}>
                {updatePackageOptions.length === 0 && <option value="">нет пакетов</option>}
                {packageStatePackageId && !updatePackageOptions.some((option) => option.id === packageStatePackageId) && (
                  <option value={packageStatePackageId}>выбранный пакет</option>
                )}
                {updatePackageOptions.map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>Состояние пакета
              <select value={packageState} disabled={!canManageUpdatePackages} onChange={(event) => setPackageState(event.currentTarget.value)}>
                <option value="registered">{updatePackageStateLabel('registered')}</option>
                <option value="validated">{updatePackageStateLabel('validated')}</option>
                <option value="rejected">{updatePackageStateLabel('rejected')}</option>
                <option value="retired">{updatePackageStateLabel('retired')}</option>
              </select>
            </label>
            <label>Причина пакета<input value={packageStateReason} disabled={!canManageUpdatePackages} onChange={(event) => setPackageStateReason(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Публикация
              <select value={rolloutStateRolloutId} disabled={!canManageUpdateRollouts || rolloutOptions.length === 0} onChange={(event) => setRolloutStateRolloutId(event.currentTarget.value)}>
                {rolloutOptions.length === 0 && <option value="">нет публикаций</option>}
                {rolloutStateRolloutId && !rolloutOptions.some((option) => option.id === rolloutStateRolloutId) && (
                  <option value={rolloutStateRolloutId}>выбранная публикация</option>
                )}
                {rolloutOptions.map((option) => (
                  <option key={option.id} value={option.id}>{option.label}</option>
                ))}
              </select>
            </label>
            <label>Состояние публикации
              <select value={rolloutState} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutState(event.currentTarget.value)}>
                <option value="active">{updateRolloutStateLabel('active')}</option>
                <option value="paused">{updateRolloutStateLabel('paused')}</option>
                <option value="completed">{updateRolloutStateLabel('completed')}</option>
                <option value="rollback_requested">{updateRolloutStateLabel('rollback_requested')}</option>
                <option value="rolled_back">{updateRolloutStateLabel('rolled_back')}</option>
                <option value="cancelled">{updateRolloutStateLabel('cancelled')}</option>
              </select>
            </label>
            <label>Причина публикации<input value={rolloutStateReason} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStateReason(event.currentTarget.value)} /></label>
          </div>
          {criticalAction === 'package-state-change' && (
            <CriticalActionConfirmation
              title="Подтвердите состояние пакета"
              detail={`${updatePackageOptions.find((option) => option.id === packageStatePackageId)?.label ?? 'Пакет'} · ${updatePackageStateLabel(packageState)}`}
              impact={`Причина будет записана в журнал: ${packageStateReason.trim() || 'не указана'}`}
              confirmLabel="Подтвердить состояние пакета"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Изменить состояние пакета')}
            />
          )}
          {criticalAction === 'rollout-state-change' && (
            <CriticalActionConfirmation
              title="Подтвердите состояние публикации"
              detail={`${rolloutOptions.find((option) => option.id === rolloutStateRolloutId)?.label ?? 'Публикация'} · ${updateRolloutStateLabel(rolloutState)}`}
              impact={`Изменение повлияет на выдачу обновлений устройствам. Причина: ${rolloutStateReason.trim() || 'не указана'}`}
              confirmLabel="Подтвердить состояние публикации"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void runSettingsAction('Изменить состояние публикации')}
            />
          )}
        </>
      );
    }

    return (
      <>
        <div className="settings-form-grid">
          <label>Название клуба<input value={clubName} onChange={(event) => { setClubName(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>Город<input value={city} onChange={(event) => { setCity(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>Валюта<input value={currencyCode} readOnly /></label>
          <label>Филиал<input value={backend ? 'текущий филиал' : 'локальный режим'} readOnly /></label>
        </div>
        <div className="settings-save-row">
          <span>{settingsDirty ? 'есть несохранённые изменения' : 'изменений нет'}</span>
          <button type="button" onClick={saveSettings}>Сохранить</button>
        </div>
      </>
    );
  };

  return (
    <main className="workspace-screen settings-screen">
      <section className="screen-head settings-head">
        <div>
          <span>Настройки</span>
          <h1>Настройки · клуб и правила работы</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Настройки загружены')}</span>
        </div>
      </section>

      <section className="settings-layout">
        <aside className="settings-nav-panel">
          <span>Разделы</span>
          {sections.map(([name, detail]) => (
            <button
              key={name}
              type="button"
              className={selectedSection === name ? 'active' : undefined}
              onClick={() => setSelectedSection(name)}
            >
              <strong>{name}</strong>
              <em>{detail}</em>
            </button>
          ))}
        </aside>

        <section className="settings-main-panel">
          <header className="settings-panel-title">
            <span>{selectedSection}</span>
            <strong>{selectedSectionDetail}</strong>
          </header>
          {renderSettingsContent()}
          <FeedbackNotice feedback={feedback} />
        </section>

        <aside className="settings-side-panel">
          <section className="settings-card-panel">
            <header className="settings-panel-title">
              <span>Готовность клуба</span>
              <strong>срез настроек платформы</strong>
            </header>
            <div className="settings-readiness-list">
              {readiness.map(([name, detail]) => (
                <div key={name}>
                  <span>{name}</span>
                  <strong>{detail}</strong>
                </div>
              ))}
            </div>
          </section>

          <section className="settings-card-panel">
            <header className="settings-panel-title">
              <span>Быстрые настройки</span>
              <strong>частые действия администратора</strong>
            </header>
            <div className="settings-action-grid">
              {actions.map(([label, detail, Icon]) => (
                <button key={label} type="button" className="settings-action-card" onClick={() => runSettingsAction(label)}>
                  <Icon size={17} />
                  <strong>{label}</strong>
                  <span>{detail}</span>
                </button>
              ))}
            </div>
          </section>
        </aside>
      </section>
    </main>
  );
}

function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim());
}

function WindowControls() {
  return (
    <div className="window-controls" aria-label="Окно">
      <button type="button" title="Свернуть" aria-label="Свернуть" onClick={() => postHostWindowCommand('minimize')}>
        <Minus size={15} />
      </button>
      <button type="button" title="Развернуть" aria-label="Развернуть" onClick={() => postHostWindowCommand('maximize')}>
        <Maximize2 size={13} />
      </button>
      <button type="button" title="Закрыть" aria-label="Закрыть" onClick={() => postHostWindowCommand('close')}>
        <X size={15} />
      </button>
    </div>
  );
}

function WindowResizeHandles() {
  const edges: HostWindowResizeEdge[] = ['top', 'right', 'bottom', 'left', 'top-left', 'top-right', 'bottom-left', 'bottom-right'];

  return (
    <div className="window-resize-handles" aria-hidden="true">
      {edges.map((edge) => (
        <div
          key={edge}
          className={`window-resize-handle ${edge}`}
          onMouseDown={(event) => {
            if (event.button !== 0) {
              return;
            }

            event.preventDefault();
            event.stopPropagation();
            postHostWindowResize(edge);
          }}
        />
      ))}
    </div>
  );
}

function SignInScreen({
  config,
  authStatus,
  hostError,
  onSignIn
}: {
  config: ReturnType<typeof getOperatorConfig>;
  authStatus: AuthStatus;
  hostError: string | null;
  onSignIn: (request: OperatorSignInRequest) => Promise<void>;
}) {
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(hostError);
  const isChecking = authStatus === 'checking';

  useEffect(() => {
    setError(hostError);
  }, [hostError]);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    const organizationId = config.organizationId?.trim() ?? '';
    if (!isGuid(organizationId)) {
      setError('Подключение клуба не настроено. Смените подключение и повторите вход.');
      return;
    }

    if (!userName.trim()) {
      setError('Укажите имя пользователя.');
      return;
    }

    if (!password) {
      setError('Укажите пароль.');
      return;
    }

    setIsBusy(true);
    try {
      await onSignIn({
        organizationId: organizationId.trim(),
        userName: userName.trim(),
        password
      });
      setPassword('');
    } catch (nextError) {
      setError(projectAuthHostError(nextError, config));
    } finally {
      setIsBusy(false);
    }
  };

  return (
    <div className="operator-shell auth-shell">
      <WindowResizeHandles />
      <header className="top-command auth-top-command" onMouseDown={handleWindowDragStart} onDoubleClick={handleWindowTitleDoubleClick}>
        <div className="brand-block">
          <strong>AFK4</strong>
          <span>Оператор</span>
        </div>
        <div className="top-status">
          <span><Wifi size={14} />{isChecking ? 'Проверяем защищённый вход' : 'Защищённый вход'}</span>
          <span>{config.platformBaseUrl}</span>
          <span>{shellModeLabel(config.shellMode)}</span>
        </div>
        <WindowControls />
      </header>

      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>AFK4 Оператор</span>
            <h1>Вход оператора</h1>
            <p>Токены сохраняются только через нативное защищённое хранилище Windows.</p>
          </header>

          <form className="auth-form" onSubmit={submit}>
            <label>
              Пользователь
              <input
                value={userName}
                onChange={(event) => setUserName(event.currentTarget.value)}
                autoComplete="username"
                autoFocus
              />
            </label>
            <label>
              Пароль
              <input
                type="password"
                value={password}
                onChange={(event) => setPassword(event.currentTarget.value)}
                autoComplete="current-password"
              />
            </label>

            <button type="submit" className="primary-wide" disabled={isBusy || isChecking}>
              {isBusy ? 'Проверяем' : 'Войти'}
            </button>
          </form>

          {error && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} />
              <span>{error}</span>
            </div>
          )}
        </section>

        <aside className="auth-context-panel">
          <section>
            <span>Платформа</span>
            <strong>{config.platformBaseUrl}</strong>
          </section>
          <section>
            <span>Валюта</span>
            <strong>{config.currencyCode}</strong>
          </section>
          <section>
            <span>Хранилище</span>
            <strong>Защищённое хранилище Windows</strong>
          </section>
        </aside>
      </main>
    </div>
  );
}

function BlockedTenantScreen({
  resolution,
  onChangeConnection
}: {
  resolution: ResolveOperatorConnectionResponse;
  onChangeConnection: () => void;
}) {
  const isDeletionPending = resolution.organizationStatus === OperatorTenantStatus.DeletionPending;
  const headline = isDeletionPending ? 'Готовится удаление клуба' : 'Подписка приостановлена';
  const reason = resolution.organizationStatusReason?.trim();
  return (
    <div className="operator-shell auth-shell">
      <WindowResizeHandles />
      <header
        className="top-command auth-top-command"
        onMouseDown={handleWindowDragStart}
        onDoubleClick={handleWindowTitleDoubleClick}
      >
        <div className="brand-block">
          <strong>AFK4</strong>
          <span>Оператор</span>
        </div>
        <WindowControls />
      </header>

      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>{resolution.organizationName}</span>
            <h1>{headline}</h1>
            <p>
              {reason !== undefined && reason.length > 0
                ? reason
                : 'Свяжитесь с владельцем клуба или поддержкой AFK4 для возобновления работы.'}
            </p>
          </header>

          <div className="auth-error" role="alert">
            <AlertTriangle size={16} />
            <span>
              {isDeletionPending
                ? 'Этот клуб помечен на удаление. Вход через AFK4 Operator недоступен.'
                : 'Клуб приостановлен. Кассовые операции и приём сессий заблокированы платформой.'}
            </span>
          </div>

          <button type="button" className="primary-wide" onClick={onChangeConnection}>
            Сменить подключение
          </button>
        </section>
      </main>
    </div>
  );
}

function createOperatorConnectionStorage(): OperatorConnectionStorage {
  if (typeof window !== 'undefined' && window.chrome?.webview) {
    return new BridgeOperatorConnectionStorage(postHostRequest);
  }
  return new LocalStorageOperatorConnectionStorage();
}

export function App() {
  return (
    <I18nProvider>
      <AppInner />
    </I18nProvider>
  );
}

function AppInner() {
  const baseConfig = getOperatorConfig();
  const connectionStorage = useMemo(() => createOperatorConnectionStorage(), []);
  const [resolvedConnection, setResolvedConnection] = useState<ResolvedOperatorConnection | null>(
    () => connectionStorage.loadSync()
  );
  const [isConnectionLoading, setIsConnectionLoading] = useState<boolean>(
    () => connectionStorage.loadSync() === null
  );
  const [blockedResolution, setBlockedResolution] = useState<ResolveOperatorConnectionResponse | null>(null);
  const config = useMemo<OperatorConfig>(() => {
    if (resolvedConnection === null) {
      return baseConfig;
    }
    return {
      ...baseConfig,
      organizationId: baseConfig.organizationId ?? resolvedConnection.organizationId,
      branchId: baseConfig.branchId ?? resolvedConnection.branchId
    };
  }, [baseConfig, resolvedConnection]);
  const connectionResolver = useMemo(
    () => new ConnectionResolver({ baseUrl: baseConfig.platformBaseUrl }),
    [baseConfig.platformBaseUrl]
  );
  const needsConnectionResolution = !config.organizationId || !config.branchId;
  const [workspace, setWorkspace] = useState<WorkspaceId>('map');
  const [selectedSeatId, setSelectedSeatId] = useState(seats[0].id);
  const [authStatus, setAuthStatus] = useState<AuthStatus>('checking');
  const [authSession, setAuthSession] = useState<OperatorAuthSession | null>(null);
  const [authError, setAuthError] = useState<string | null>(null);
  const [workspaceFeedback, setWorkspaceFeedback] = useState<string | null>(null);
  const [floorMap, setFloorMap] = useState<OperatorFloorMapState>(() => createFixtureFloorMapState());
  const floorMapRef = useRef(floorMap);
  const [remainingNowMs, setRemainingNowMs] = useState(() => Date.now());
  const [realtimeState, setRealtimeState] = useState<OperatorRealtimeConnectionState>('disconnected');
  const [realtimeError, setRealtimeError] = useState<string | null>(null);
  const [shellCurrentShift, setShellCurrentShift] = useState<ShiftDto | null>(null);
  const [shellDashboardSummary, setShellDashboardSummary] = useState<OperatorDashboardSummaryDto | null>(null);
  const [shellLoadStatus, setShellLoadStatus] = useState<LoadStatus>('loading');
  const [shellLoadError, setShellLoadError] = useState<string | null>(null);
  const [offlineActionAudit, setOfflineActionAudit] = useState<string[]>([]);
  const displayedFloorMap = useMemo(
    () => refreshFloorMapRemaining(floorMap, remainingNowMs),
    [floorMap, remainingNowMs]
  );
  const selectedSeat = displayedFloorMap.seats.find((seat) => seat.id === selectedSeatId) ?? displayedFloorMap.seats[0] ?? null;
  const activeBranchId = authSession === null ? null : resolveActiveBranchId(authSession, config.branchId);
  const backendContext: OperatorBackendContext | null = authSession !== null && activeBranchId !== null
    ? { config, session: authSession, branchId: activeBranchId }
    : null;
  const canUsePcControl = (hasPermission(authSession, permissionNames.viewDiagnostics)
    && hasPermission(authSession, permissionNames.viewDeviceDetail))
    || hasPermission(authSession, permissionNames.dispatchDeviceCommand);
  const shellShiftText = shellShiftLabel(shellCurrentShift, shellDashboardSummary, shellLoadStatus, shellLoadError);
  const shellPosText = shellPosLabel(shellDashboardSummary, shellLoadStatus);

  useEffect(() => {
    floorMapRef.current = floorMap;
  }, [floorMap]);

  useEffect(() => {
    const intervalId = window.setInterval(() => setRemainingNowMs(Date.now()), 1000);
    return () => window.clearInterval(intervalId);
  }, []);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      setShellCurrentShift(null);
      setShellDashboardSummary(null);
      setShellLoadStatus('loading');
      setShellLoadError(null);
      return undefined;
    }

    const branchId = resolveActiveBranchId(authSession, config.branchId);
    if (!branchId) {
      setShellCurrentShift(null);
      setShellDashboardSummary(null);
      setShellLoadStatus('failed');
      setShellLoadError('Активный филиал не назначен.');
      return undefined;
    }

    const canLoadShift = hasPermission(authSession, permissionNames.viewShift);
    const canLoadSummary = hasPermission(authSession, permissionNames.viewReports);
    if (!canLoadShift && !canLoadSummary) {
      setShellCurrentShift(null);
      setShellDashboardSummary(null);
      setShellLoadStatus('failed');
      setShellLoadError(null);
      return undefined;
    }

    let disposed = false;
    const clients = createAuthenticatedOperatorClients(config, authSession);

    const loadShellStatus = async () => {
      setShellLoadStatus((current) => current === 'backend' ? current : 'loading');
      setShellLoadError(null);

      let nextShift: ShiftDto | null = null;
      let nextSummary: OperatorDashboardSummaryDto | null = null;
      const errors: string[] = [];
      const today = toDateInputValue(new Date());

      await Promise.all([
        canLoadShift
          ? clients.shifts.getCurrentShift(branchId)
            .then((shift) => {
              nextShift = shift;
            })
            .catch((error) => {
              errors.push(projectOperatorError(error).detail);
            })
          : Promise.resolve(),
        canLoadSummary
          ? clients.dashboard.getSummary(branchId, dashboardRangeQuery(today, today))
            .then((summary) => {
              nextSummary = summary;
            })
            .catch((error) => {
              errors.push(projectOperatorError(error).detail);
            })
          : Promise.resolve()
      ]);

      if (disposed) {
        return;
      }

      setShellCurrentShift(nextShift);
      setShellDashboardSummary(nextSummary);
      setShellLoadStatus(errors.length > 0 && nextShift === null && nextSummary === null ? 'failed' : 'backend');
      setShellLoadError(errors[0] ?? null);
    };

    void loadShellStatus();
    const intervalId = window.setInterval(() => void loadShellStatus(), shellOperationalRefreshMs);

    return () => {
      disposed = true;
      window.clearInterval(intervalId);
    };
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

  useEffect(() => {
    let disposed = false;

    connectionStorage.load()
      .then((connection) => {
        if (disposed) {
          return;
        }
        if (connection !== null) {
          setResolvedConnection(connection);
        }
        setIsConnectionLoading(false);
      })
      .catch(() => {
        if (disposed) {
          return;
        }
        setIsConnectionLoading(false);
      });

    return () => {
      disposed = true;
    };
  }, [connectionStorage]);

  useEffect(() => {
    let disposed = false;

    loadOperatorSession()
      .then(async (session) => {
        if (session === null) {
          return null;
        }

        try {
          return await refreshOperatorSession();
        } catch (error) {
          if (isUnauthorizedAuthError(error)) {
            await clearStoredOperatorSession();
            return null;
          }

          throw error;
        }
      })
      .then((session) => {
        if (disposed) {
          return;
        }

        setAuthSession(session);
        setAuthStatus(session ? 'signed-in' : 'signed-out');
        setAuthError(null);
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setAuthSession(null);
        setAuthStatus('signed-out');
        setAuthError(projectAuthHostError(error, config));
      });

    return () => {
      disposed = true;
    };
  }, []);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      return;
    }

    if (!canOpenWorkspace(authSession, workspace)) {
      setWorkspace(firstAllowedWorkspace(authSession));
    }
  }, [authStatus, authSession, workspace]);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      setFloorMap(createFixtureFloorMapState());
      setSelectedSeatId(seats[0].id);
      setRealtimeState('disconnected');
      setRealtimeError(null);
      return undefined;
    }

    const branchId = resolveActiveBranchId(authSession, config.branchId);
    if (!branchId) {
      setFloorMap((current) => ({
        ...current,
        loadStatus: 'failed',
        error: 'Активный филиал не назначен.'
      }));
      return undefined;
    }

    let disposed = false;

    setFloorMap((current) => ({
      ...current,
      branchId,
      loadStatus: 'loading',
      error: null
    }));

    void (async () => {
      try {
        const nextState = await loadBackendFloorMapState(config, authSession, branchId);
        if (disposed) {
          return;
        }

        setFloorMap(nextState);
        setSelectedSeatId(nextState.seats[0]?.id ?? seats[0].id);
        void drainQueuedActions(nextState.seats);
      } catch (error) {
        if (isUnauthorizedPlatformError(error)) {
          try {
            const nextSession = await refreshOperatorSession();
            const nextBranchId = resolveActiveBranchId(nextSession, config.branchId);
            if (!nextBranchId) {
              throw new Error('Активный филиал не назначен.');
            }

            const nextState = await loadBackendFloorMapState(config, nextSession, nextBranchId);
            if (disposed) {
              return;
            }

            setAuthSession(nextSession);
            setFloorMap(nextState);
            setSelectedSeatId(nextState.seats[0]?.id ?? seats[0].id);
            return;
          } catch (refreshError) {
            await clearStoredOperatorSession();
            if (disposed) {
              return;
            }

            setAuthSession(null);
            setAuthStatus('signed-out');
            setAuthError(projectAuthHostError(refreshError, config));
            setFloorMap(createFixtureFloorMapState());
            setSelectedSeatId(seats[0].id);
            return;
          }
        }

        if (disposed) {
          return;
        }

        // Platform unreachable: hydrate the last-known-good snapshot into a read-only mirror (§6.5).
        // Fall back to the error surface only when there is nothing cached for this branch.
        const cached = loadFloorMapCache(branchId);
        if (cached) {
          const degraded = hydrateFloorMapStateFromCache(cached, branchId);
          setFloorMap(degraded);
          setSelectedSeatId((current) => current ?? degraded.seats[0]?.id ?? seats[0].id);
          return;
        }

        setFloorMap((current) => ({
          ...current,
          branchId,
          loadStatus: 'failed',
          error: projectOperatorError(error).detail
        }));
      }
    })();

    return () => {
      disposed = true;
    };
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      return undefined;
    }

    const branchId = resolveActiveBranchId(authSession, config.branchId);
    if (!branchId) {
      return undefined;
    }

    let disposed = false;
    let reloadTimeoutId: number | null = null;
    let reloadInFlight = false;
    let reloadQueued = false;
    const scheduleAuthoritativeFloorMapReload = () => {
      if (reloadTimeoutId !== null) {
        window.clearTimeout(reloadTimeoutId);
      }

      reloadTimeoutId = window.setTimeout(() => {
        reloadTimeoutId = null;
        if (reloadInFlight) {
          reloadQueued = true;
          return;
        }

        reloadInFlight = true;
        void loadBackendFloorMapState(config, authSession, branchId)
          .then((nextState) => {
            if (disposed) {
              return;
            }

            floorMapRef.current = nextState;
            setFloorMap(nextState);
            setSelectedSeatId((currentSeatId) => nextState.seats.some((seat) => seat.id === currentSeatId)
              ? currentSeatId
              : nextState.seats[0]?.id ?? seats[0].id);
          })
          .catch((error) => {
            if (!disposed) {
              setRealtimeError(projectOperatorError(error).detail);
            }
          })
          .finally(() => {
            reloadInFlight = false;
            if (!disposed && reloadQueued) {
              reloadQueued = false;
              scheduleAuthoritativeFloorMapReload();
            }
          });
      }, 100);
    };

    const realtimeClient = createOperatorRealtimeClient({
      baseUrl: config.platformBaseUrl,
      getAccessToken: () => authSession.accessToken,
      onConnectionStateChanged: (state) => {
        if (!disposed) {
          setRealtimeState(state);
        }
      },
      onDeviceStatusChanged: (status) => {
        if (disposed || !matchesRealtimeScope(status, authSession, branchId)) {
          return;
        }

        const matchingSeat = findSeatForDeviceStatus(floorMapRef.current.seats, status);
        const shouldReload = matchingSeat !== null && shouldReloadFloorMapAfterDeviceStatus(matchingSeat, status);
        setFloorMap((current) => {
          const nextState = {
            ...current,
            seats: applyDeviceStatusToSeats(current.seats, status)
          };
          floorMapRef.current = nextState;
          return nextState;
        });
        if (shouldReload) {
          scheduleAuthoritativeFloorMapReload();
        }
      },
      onDeviceCommandResult: (result) => {
        if (disposed || !matchesCommandResultScope(result, authSession, branchId)) {
          return;
        }

        scheduleAuthoritativeFloorMapReload();
      }
    });

    setRealtimeError(null);
    realtimeClient.start()
      .catch((error) => {
        if (disposed) {
          return;
        }

        setRealtimeState('disconnected');
        setRealtimeError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
      if (reloadTimeoutId !== null) {
        window.clearTimeout(reloadTimeoutId);
      }
      void realtimeClient.stop();
    };
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

  const handleSignIn = async (request: OperatorSignInRequest) => {
    const session = await signInOperator(request);
    setAuthSession(session);
    setAuthStatus('signed-in');
    setAuthError(null);
    setWorkspaceFeedback(null);
  };

  const handleConnectionResolved = async (resolution: ResolveOperatorConnectionResponse) => {
    if (isOperatorTenantBlocked(resolution)) {
      await connectionStorage.clear();
      setResolvedConnection(null);
      setBlockedResolution(resolution);
      return;
    }

    const stored = await connectionStorage.save(resolution);
    setResolvedConnection(stored);
    setBlockedResolution(null);
  };

  const handleChangeConnection = async () => {
    await connectionStorage.clear();
    setResolvedConnection(null);
    setBlockedResolution(null);
  };

  const handleSignOut = async () => {
    try {
      await signOutOperator();
      setAuthError(null);
      setWorkspaceFeedback(null);
    } catch (error) {
      setAuthError(projectAuthHostError(error, config));
    } finally {
      setAuthSession(null);
      setAuthStatus('signed-out');
    }
  };

  const handleWorkspaceNavigation = async (
    workspaceId: WorkspaceId,
    label: string,
    isAllowed: boolean
  ) => {
    if (isAllowed) {
      setWorkspaceFeedback(null);
      setWorkspace(workspaceId);
      return;
    }

    try {
      const refreshedSession = await refreshOperatorSession();
      setAuthSession(refreshedSession);
      if (canOpenWorkspace(refreshedSession, workspaceId)) {
        setWorkspaceFeedback(null);
        setWorkspace(workspaceId);
        return;
      }

      setWorkspaceFeedback(`Нет прав на раздел "${label}" для текущего оператора.`);
    } catch (error) {
      setWorkspaceFeedback(`Не удалось обновить права для "${label}": ${projectOperatorError(error).detail}`);
    }
  };

  const handleSeatAction = async (request: SeatActionRequest): Promise<SeatActionResult> => {
    const session = authSession;
    if (session === null) {
      throw new Error('Оператор не вошёл в систему.');
    }

    const branchId = resolveActiveBranchId(session, config.branchId);
    if (!branchId) {
      throw new Error('Активный филиал не назначен.');
    }

    if (floorMap.source !== 'backend') {
      throw new Error('Карта платформы не загружена.');
    }

    if (isPendingSeatCommand(request.seat)) {
      throw new Error('Команда уже отправлена. Дождитесь подтверждения ПК.');
    }

    const clients = createAuthenticatedOperatorClients(config, session);
    let response: unknown;
    if (request.type === 'start') {
      if (!hasPermission(session, permissionNames.startSession)) {
        throw new Error('Нет прав на запуск сессий.');
      }

      if (request.seat.tone !== 'ready' || request.seat.activeSessionId) {
        throw new Error('ПК не готов к запуску сессии.');
      }

      const billing = request.billing;
      const isOpenTab = request.durationMode === 'open';
      response = await clients.sessions.startGuestSession(branchId, {
        organizationId: session.organizationId,
        seatId: request.seat.id,
        durationMode: isOpenTab ? 'open' : 'fixed',
        durationMinutes: isOpenTab ? null : defaultSessionDurationMinutes,
        tariffRuleVersionId: billing.tariffRuleVersionId,
        idempotencyKey: createIdempotencyKey('session-start'),
        playerAccountId: billing.playerAccountId ?? null,
        billingMode: billing.mode === 'guest' ? '' : billing.mode,
        tariffVersionId: billing.tariffVersionId ?? null,
        playerPackageId: billing.playerPackageId ?? null
      });
    } else if (request.type === 'extend') {
      if (!hasPermission(session, permissionNames.extendSession)) {
        throw new Error('Нет прав на продление сессий.');
      }

      if (!request.seat.activeSessionId) {
        throw new Error('На выбранном ПК нет активной сессии.');
      }

      const billing = request.billing;
      response = await clients.sessions.extendSession(request.seat.activeSessionId, {
        additionalMinutes: request.minutes,
        tariffRuleVersionId: billing.tariffRuleVersionId,
        idempotencyKey: createIdempotencyKey('session-extend'),
        playerAccountId: billing.playerAccountId ?? null,
        billingMode: billing.mode === 'guest' ? '' : billing.mode,
        tariffVersionId: billing.tariffVersionId ?? null,
        playerPackageId: billing.playerPackageId ?? null
      });
    } else if (request.type === 'transfer') {
      if (!hasPermission(session, permissionNames.transferSession)) {
        throw new Error('Нет прав на перенос сессий.');
      }

      if (!request.seat.activeSessionId) {
        throw new Error('На выбранном ПК нет активной сессии.');
      }

      response = await clients.sessions.transferSession(request.seat.activeSessionId, {
        targetSeatId: request.targetSeatId,
        idempotencyKey: createIdempotencyKey('session-transfer')
      });
    } else if (request.type === 'checkout') {
      if (!hasPermission(session, permissionNames.endSession)) {
        throw new Error('Нет прав на завершение сессий.');
      }

      if (!request.seat.activeSessionId) {
        throw new Error('На выбранном ПК нет активной сессии.');
      }

      response = await clients.sessions.checkoutSession(request.seat.activeSessionId, {
        organizationId: session.organizationId,
        payments: request.payments,
        idempotencyKey: createIdempotencyKey('session-checkout')
      });
    } else {
      if (!hasPermission(session, permissionNames.endSession)) {
        throw new Error('Нет прав на завершение сессий.');
      }

      if (!request.seat.activeSessionId) {
        throw new Error('На выбранном ПК нет активной сессии.');
      }

      response = await clients.sessions.endSession(request.seat.activeSessionId, {
        reason: 'operator',
        idempotencyKey: createIdempotencyKey('session-end')
      });
    }

    const detail = await describeSeatActionResult(clients, session, request.seat, response);
    const nextState = await loadBackendFloorMapState(config, session, branchId);
    const preferredSeatId = request.type === 'transfer' ? request.targetSeatId : request.seat.id;
    setFloorMap(nextState);
    setSelectedSeatId(nextState.seats.some((seat) => seat.id === preferredSeatId)
      ? preferredSeatId
      : nextState.seats[0]?.id ?? '');
    return { detail };
  };

  // On reconnect (§6.5): replay queued lock/unlock against the now-authoritative map; drop actions whose
  // seat or session moved on while offline, recording an operator-visible audit note (D9).
  const drainQueuedActions = async (liveSeats: SeatSummary[]): Promise<void> => {
    const pending = loadActionOutbox();
    if (pending.length === 0) {
      return;
    }

    const { replay, dropped } = reconcileActionOutbox(pending, liveSeats);
    for (const drop of dropped) {
      acknowledgeAction(drop.entry.idempotencyKey);
    }
    if (dropped.length > 0) {
      setOfflineActionAudit((current) => [...current, ...dropped.map((drop) => drop.note)]);
    }

    if (replay.length === 0) {
      return;
    }

    const nextBackend = backendContext;
    if (nextBackend === null || !hasPermission(nextBackend.session, permissionNames.dispatchDeviceCommand)) {
      return;
    }

    const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
    for (const entry of replay) {
      try {
        await clients.devices.dispatchDeviceCommand(entry.deviceId, {
          type: entry.commandType,
          payload: { reason: 'operator-offline-replay', source: 'operator-map', seatId: entry.seatId }
        });
        acknowledgeAction(entry.idempotencyKey);
      } catch {
        // Connectivity dropped again mid-drain; keep the rest queued for the next refresh.
        break;
      }
    }
  };

  const handlePcControlAction = async (seat: SeatSummary, action: PcControlActionId): Promise<PcControlActionResult> => {
    const nextBackend = requireBackend(backendContext);
    if (!seat.deviceId) {
      throw new Error('У выбранного ПК нет привязанного устройства.');
    }

    const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
    if (action === 'status') {
      if (!hasPermission(nextBackend.session, permissionNames.viewDiagnostics) ||
        !hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
        throw new Error('Нет прав на просмотр диагностики устройства.');
      }

      const [device, diagnostics] = await Promise.all([
        clients.devices.getDeviceDetail(seat.deviceId),
        clients.diagnostics.getDiagnostics(nextBackend.branchId)
      ]);

      return { detail: describeTechModeResult(seat, device, diagnostics) };
    }

    if (action === 'lock' || action === 'unlock') {
      if (!hasPermission(nextBackend.session, permissionNames.dispatchDeviceCommand)) {
        throw new Error('Нет прав на отправку команд ПК.');
      }

      // Offline (§6.5): queue the idempotent lock/unlock locally instead of dispatching; it replays (or is
      // dropped if superseded) on reconnect. Billing actions stay online-only (D2) and are gated elsewhere.
      if (floorMapRef.current.isOffline) {
        enqueueAction({
          idempotencyKey: createIdempotencyKey(`device-${action}`),
          deviceId: seat.deviceId,
          seatId: seat.id,
          seatName: seat.name,
          commandType: action as OperatorCommandType,
          expectedSessionId: seat.activeSessionId ?? null,
          queuedAtMs: Date.now()
        });
        return { detail: `Команда «${action}» поставлена в очередь — будет отправлена после восстановления связи.` };
      }

      const command = await clients.devices.dispatchDeviceCommand(seat.deviceId, {
        type: action,
        payload: {
          reason: 'operator-pc-control',
          source: 'operator-map',
          seatId: seat.id
        }
      });
      return { detail: await describeDispatchedDeviceCommand(clients, nextBackend.session, seat, command) };
    }

    throw new Error('Эта команда требует отдельного контракта агента и платформы и пока не включена.');
  };

  if (blockedResolution !== null) {
    return (
      <BlockedTenantScreen
        resolution={blockedResolution}
        onChangeConnection={handleChangeConnection}
      />
    );
  }

  if (authStatus === 'signed-out' && needsConnectionResolution && !isConnectionLoading) {
    return (
      <ConnectionResolutionScreen
        resolver={connectionResolver}
        onResolved={handleConnectionResolved}
      />
    );
  }

  if (authStatus !== 'signed-in' || authSession === null) {
    return (
      <SignInScreen
        config={config}
        authStatus={authStatus}
        hostError={authError}
        onSignIn={handleSignIn}
      />
    );
  }

  return (
    <div className="operator-shell">
      <WindowResizeHandles />
      <header className="top-command" onMouseDown={handleWindowDragStart} onDoubleClick={handleWindowTitleDoubleClick}>
        <div className="brand-block">
          <strong>AFK4</strong>
          <span>Оператор</span>
        </div>
        <label className="command-search">
          <Search size={16} />
          <input placeholder="Игрок, ПК, команда" aria-label="Поиск" />
        </label>
        <div className="top-status">
          <span>{shellShiftText}</span>
          <span>{operatorDisplayNameLabel(authSession.displayName)} · {shellModeLabel(config.shellMode)}</span>
        </div>
        <button type="button" className="sign-out-button" onClick={handleSignOut}>Выйти</button>
        <WindowControls />
      </header>

      <nav className="workspace-rail" aria-label="Рабочие места">
        {navItems.map((item, index) => {
          const Icon = item.icon;
          const id = workspaceIds[index];
          const isAllowed = canOpenWorkspace(authSession, id);
          return (
            <button
              key={item.label}
              type="button"
              className={[workspace === id ? 'active' : '', !isAllowed ? 'locked' : ''].filter(Boolean).join(' ')}
              aria-disabled={!isAllowed}
              title={item.label}
              onClick={() => void handleWorkspaceNavigation(id, item.label, isAllowed)}
            >
              <Icon size={22} />
              <span>{item.label}</span>
            </button>
          );
        })}
      </nav>

      {workspace === 'map' && (
        <MapWorkspace
          floorMap={displayedFloorMap}
          canUsePcControl={canUsePcControl}
          selectedSeatId={selectedSeat?.id ?? ''}
          offlineActionAudit={offlineActionAudit}
          onSelectSeat={setSelectedSeatId}
          onPcControlAction={handlePcControlAction}
        />
      )}
      {workspace === 'dashboard' && (
        <DashboardWorkspace
          currencyCode={config.currencyCode}
          backend={backendContext}
          onNavigate={setWorkspace}
          onOpenSeat={(seatId) => {
            setSelectedSeatId(seatId);
            setWorkspace('map');
          }}
        />
      )}
      {workspace === 'booking' && (
        <BackendBookingWorkspace
          floorMap={displayedFloorMap}
          backend={backendContext}
          onOpenSeat={(seatId) => {
            setSelectedSeatId(seatId);
            setWorkspace('map');
          }}
        />
      )}
      {workspace === 'pos' && <BackendPosWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'players' && <BackendPlayersWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'payments' && <BackendPaymentsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'payment_cards' && backendContext !== null && (
        <PaymentGatewaysWorkspace backend={backendContext} />
      )}
      {workspace === 'logs' && <BackendLogsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'settings' && <BackendSettingsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'review' && <ReviewWorkspace currencyCode={config.currencyCode} backend={backendContext} />}

      {workspace === 'map' && selectedSeat !== null && (
        <MapSidePanel
          seat={selectedSeat}
          seats={displayedFloorMap.seats}
          currencyCode={config.currencyCode}
          backend={backendContext}
          actionsEnabled={floorMap.source === 'backend' && floorMap.loadStatus === 'ready'}
          onSeatAction={handleSeatAction}
        />
      )}
      {workspace !== 'map' && workspace !== 'dashboard' && workspace !== 'booking' && workspace !== 'pos' && workspace !== 'players' && workspace !== 'payments' && workspace !== 'logs' && workspace !== 'settings' && workspace !== 'review'
        && <SummarySidePanel workspace={workspace} currencyCode={config.currencyCode} />}

      <footer className="signals-strip">
        <span><Wifi size={14} />{realtimeLabel(realtimeState, realtimeError)} · {dataSourceLabel(floorMap.source)}</span>
        <span><MonitorCheck size={14} />ПК без связи: {countByTone(displayedFloorMap.seats, 'offline')} · требуют внимания: {countProblems(displayedFloorMap.seats)}</span>
        <span><CircleDollarSign size={14} />{shellPosText}</span>
        {workspaceFeedback && (
          <span className="rail-feedback"><LockKeyhole size={14} />{workspaceFeedback}</span>
        )}
      </footer>
    </div>
  );
}
