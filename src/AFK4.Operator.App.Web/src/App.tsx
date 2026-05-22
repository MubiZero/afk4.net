import {
  AlertTriangle,
  ArrowRightLeft,
  Banknote,
  CalendarClock,
  CircleDollarSign,
  Clock3,
  LockKeyhole,
  Maximize2,
  Minus,
  MonitorCheck,
  Plus,
  ReceiptText,
  Search,
  ShieldAlert,
  Square,
  TimerReset,
  UserRoundPlus,
  Wifi,
  Wrench,
  X
} from 'lucide-react';
import type { LucideIcon } from 'lucide-react';
import { useEffect, useMemo, useState, type CSSProperties, type FormEvent, type MouseEvent, type ReactNode } from 'react';
import { projectOperatorError } from './apiErrors';
import { loadOperatorSession, signInOperator, signOutOperator, type OperatorAuthSession, type OperatorSignInRequest } from './authClient';
import {
  applyDeviceStatusToSeats,
  createFixtureFloorMapState,
  mapFloorMapDtoToState,
  type FloorMapLoadStatus,
  type OperatorFloorMapState
} from './floorMapState';
import { isHostBridgeUnavailableError, postHostWindowCommand } from './hostBridge';
import {
  createOperatorApiClients,
  type AuditSearchResultDto,
  type BranchProfileDto,
  type BranchDiagnosticsDto,
  type CashMovementDto,
  type OperatorDashboardSummaryDto,
  type PackageOptionDto,
  type PlayerPackageDto,
  type PlayerSearchResultDto,
  type PosProductDto,
  type PosSaleDto,
  type ReceiptDto,
  type ReservationSearchResultDto,
  type ReportResultDto,
  type ShiftDto,
  type StaffUserDto,
  type StockMovementDto,
  type TariffOptionDto,
  type UpdateRolloutStatusDto,
  type WalletSummaryDto,
  type ZoneDto
} from './operatorApiClients';
import { getOperatorConfig } from './operatorConfig';
import {
  createOperatorRealtimeClient,
  type DeviceStatusChangedDto,
  type OperatorRealtimeConnectionState
} from './operatorRealtime';
import { navItems, seats, type SeatSummary, type SeatTone } from './operatorData';
import { PlatformApiClient } from './platformApi';

type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'pos' | 'players' | 'payments' | 'logs' | 'settings';
type DashboardPeriod = 'today' | 'week' | 'month' | 'custom';
type AuthStatus = 'checking' | 'signed-out' | 'signed-in';
type FeedbackState = 'idle' | 'pending' | 'confirmed' | 'failed';
type Feedback = { label: string; state: FeedbackState; detail?: string };
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
type SeatActionRequest =
  | { type: 'start'; seat: SeatSummary; billing: SessionBillingSelection }
  | { type: 'extend'; seat: SeatSummary; minutes: number; billing: SessionBillingSelection }
  | { type: 'transfer'; seat: SeatSummary; targetSeatId: string }
  | { type: 'end'; seat: SeatSummary };

const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'pos', 'players', 'payments', 'logs', 'settings'];
const fallbackOrganizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';
const defaultSessionDurationMinutes = 60;
const defaultTariffRuleVersionId = 'manual-v1';
const billingModeOptions: Array<{ id: SessionBillingModeId; label: string; detail: string }> = [
  { id: 'guest', label: 'Гость', detail: 'без ledger' },
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
  viewAudit: 'audit.view'
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
  ]
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

function handleWindowDragStart(event: MouseEvent<HTMLElement>) {
  if (event.button !== 0) {
    return;
  }

  const target = event.target as HTMLElement;
  if (target.closest('button, input, .command-search')) {
    return;
  }

  postHostWindowCommand('drag');
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
  detail = 'Для этого действия нет backend-контракта.'
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
      {feedbackText(feedback)}
    </div>
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
          <span>{seat.zone}</span>
        </div>
        <span className="state-chip">{seat.stateLabel}</span>
      </header>
      <div className="seat-main">
        <span>{seat.player}</span>
        <span>{seat.app}</span>
      </div>
      <footer>
        <strong>{seat.remaining}</strong>
        <span>{seat.command}</span>
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

  if (normalized.includes('postpaid')) {
    return 'Постоплата';
  }

  if (normalized.includes('guest')) {
    return 'Гость';
  }

  return 'Не задан';
}

function commandLabel(command: string) {
  if (command.includes('Lease fresh')) {
    return 'Сессия подтверждена';
  }

  if (command.includes('Unlock pending')) {
    return 'Разблокировка в процессе';
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

  return command;
}

function deviceStatusLabel(device: string) {
  return device
    .replace('Online', 'Онлайн')
    .replace('Offline', 'Нет связи')
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
    label: 'Техрежим',
    value: commandLabel(seat.command)
  };
}

function floorMapLoadLabel(status: FloorMapLoadStatus, source: OperatorFloorMapState['source'], error: string | null) {
  if (status === 'loading') {
    return source === 'backend' ? 'Обновляем карту' : 'Загружаем карту';
  }

  if (status === 'failed') {
    return error ? `Backend error · ${error}` : 'Backend error · API unavailable';
  }

  return source === 'backend' ? 'Backend live' : 'Dev demo';
}

function workspaceLoadStatusLabel(status: LoadStatus, backendLabel: string): string {
  if (status === 'backend') {
    return backendLabel;
  }

  if (status === 'loading') {
    return 'Loading backend';
  }

  if (status === 'failed') {
    return 'Backend error';
  }

  return 'Dev demo';
}

function dataSourceLabel(source: string): string {
  return source === 'backend' ? 'backend' : 'dev demo';
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

  return `${machineName} · Agent ${agentVersion} / Shell ${shellVersion} · ${pendingCommands} pending, ${failedCommands} failed commands`;
}

function projectAuthHostError(error: unknown, config: OperatorConfig): string {
  if (isHostBridgeUnavailableError(error) && config.runtime !== 'browser-dev') {
    return 'Нативный вход Operator App недоступен. Перезапустите приложение или проверьте WebView2 host.';
  }

  return projectOperatorError(error).detail;
}

function realtimeLabel(state: OperatorRealtimeConnectionState, error: string | null): string {
  if (state === 'connected') {
    return 'Realtime connected';
  }

  if (state === 'connecting') {
    return 'Realtime connecting';
  }

  if (state === 'reconnecting') {
    return 'Realtime reconnecting';
  }

  return error ? 'Realtime offline' : 'Realtime disconnected';
}

function resolveActiveBranchId(session: OperatorAuthSession, configBranchId?: string): string | null {
  return session.activeBranchId ?? configBranchId ?? session.branchIds[0] ?? null;
}

function matchesRealtimeScope(status: DeviceStatusChangedDto, session: OperatorAuthSession, branchId: string): boolean {
  return status.organizationId.toLowerCase() === session.organizationId.toLowerCase()
    && status.branchId.toLowerCase() === branchId.toLowerCase();
}

function createAuthenticatedOperatorClients(config: ReturnType<typeof getOperatorConfig>, session: OperatorAuthSession) {
  return createOperatorApiClients(new PlatformApiClient({
    baseUrl: config.platformBaseUrl,
    getAccessToken: () => session.accessToken
  }));
}

async function loadBackendFloorMapState(
  config: ReturnType<typeof getOperatorConfig>,
  session: OperatorAuthSession,
  branchId: string
): Promise<OperatorFloorMapState> {
  const clients = createAuthenticatedOperatorClients(config, session);
  return mapFloorMapDtoToState(await clients.floorMap.getFloorMap(branchId));
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
  const majorUnits = minorUnits / 100;
  const formatter = new Intl.NumberFormat('ru-RU', {
    maximumFractionDigits: Number.isInteger(majorUnits) ? 0 : 2,
    minimumFractionDigits: 0
  });

  return `${formatter.format(majorUnits)} ${currencyCode}`;
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
  return Number.isFinite(majorUnits) && majorUnits > 0 ? Math.round(majorUnits * 100) : null;
}

function parseNonNegativeMoneyInputMinorUnits(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) {
    return null;
  }

  const majorUnits = Number(normalized);
  return Number.isFinite(majorUnits) && majorUnits >= 0 ? Math.round(majorUnits * 100) : null;
}

function formatMoneyInputMinorUnits(minorUnits: number): string {
  return (minorUnits / 100).toFixed(2);
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

  return new Intl.DateTimeFormat('ru-RU', {
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
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

function buildPosReceiptText(sale: PosSaleDto, receipt: Record<string, unknown> | null, currencyCode: string): string {
  const saleId = readString(sale, 'posSaleId');
  const receiptNumber = readString(receipt, 'receiptNumber', saleId.slice(0, 8) || 'receipt');
  const receiptType = readString(receipt, 'receiptType', readString(sale, 'state', 'sale'));
  const createdAtUtc = readString(receipt, 'createdAtUtc', readString(sale, 'createdAtUtc'));
  const lines = readArray(sale, 'lines').map((line) => [
    readString(line, 'productName', 'POS item'),
    `${readNumber(line, 'quantity', 0)} шт.`,
    formatMoney(readMoney(line, 'unitPrice'), currencyCode),
    formatMoney(readMoney(line, 'lineTotal'), currencyCode)
  ].join(' | '));

  return [
    'AFK4 POS',
    `Receipt: ${receiptNumber}`,
    `Type: ${receiptType}`,
    `Created: ${createdAtUtc || '—'}`,
    `Sale: ${saleId || '—'}`,
    '',
    ...lines,
    '',
    `Total: ${formatMoney(readMoney(sale, 'total'), currencyCode)}`
  ].join('\n');
}

function requireBackend(backend: OperatorBackendContext | null): OperatorBackendContext {
  if (backend === null) {
    throw new Error('Backend operator session is not available.');
  }

  return backend;
}

function hasPermission(session: OperatorAuthSession | null, permission: string) {
  return session?.permissions.some((candidate) => candidate.toLowerCase() === permission.toLowerCase()) ?? false;
}

function hasAllPermissions(session: OperatorAuthSession | null, permissions: readonly string[]) {
  return permissions.every((permission) => hasPermission(session, permission));
}

function canOpenWorkspace(session: OperatorAuthSession | null, workspaceId: WorkspaceId) {
  return hasAllPermissions(session, workspacePermissionRules[workspaceId]);
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

function describeDeviceCommandStatus(status: Record<string, unknown>) {
  const type = readString(status, 'type', 'command');
  const state = readString(status, 'status', 'pending');
  const message = readString(status, 'message');
  return message ? `${type}: ${state} · ${message}` : `${type}: ${state}`;
}

function describeSessionCommandFallback(response: unknown) {
  const commands = readArray<Record<string, unknown>>(response, 'deviceCommands');
  if (commands.length === 0) {
    return 'backend подтвердил, команда Agent не нужна';
  }

  const command = commands[0];
  return `${readString(command, 'type', 'command')}: создана, ждём Agent`;
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

function MapWorkspace({
  currencyCode,
  floorMap,
  canUseTechMode,
  selectedSeatId,
  onSelectSeat,
  onTechMode
}: {
  currencyCode: string;
  floorMap: OperatorFloorMapState;
  canUseTechMode: boolean;
  selectedSeatId: string;
  onSelectSeat: (seatId: string) => void;
  onTechMode: (seat: SeatSummary) => Promise<string>;
}) {
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [activeFilter, setActiveFilter] = useState<MapFilterId>('all');
  const [viewMode, setViewMode] = useState<MapViewMode>('grid');
  const activeCount = countByTone(floorMap.seats, 'active');
  const readyCount = countByTone(floorMap.seats, 'ready');
  const pendingCount = countByTone(floorMap.seats, 'pending');
  const offlineCount = countByTone(floorMap.seats, 'offline');
  const problemCount = countProblems(floorMap.seats);
  const loadLabel = floorMapLoadLabel(floorMap.loadStatus, floorMap.source, floorMap.error);
  const visibleSeats = useMemo(
    () => floorMap.seats.filter((seat) => matchesMapFilter(seat, activeFilter)),
    [activeFilter, floorMap.seats]
  );
  const selectedSeat = floorMap.seats.find((seat) => seat.id === selectedSeatId) ?? null;
  const selectedSeatVisible = visibleSeats.some((seat) => seat.id === selectedSeatId);

  const runTechMode = async () => {
    if (selectedSeat === null) {
      setFeedback({ label: 'Техрежим', state: 'failed', detail: 'Выберите ПК для диагностики.' });
      return;
    }

    setFeedback({ label: 'Техрежим', state: 'pending' });
    try {
      setFeedback({ label: 'Техрежим', state: 'confirmed', detail: await onTechMode(selectedSeat) });
    } catch (error) {
      setFeedback({ label: 'Техрежим', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

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
          <span className={`map-load-state ${floorMap.loadStatus}`}>{loadLabel}</span>
          <button
            type="button"
            className="map-tool-action"
            disabled={!canUseTechMode || selectedSeat === null || feedback.state === 'pending'}
            onClick={runTechMode}
          >
            <Wrench size={14} />Техрежим
          </button>
        </div>
      </section>

      <section className="state-strip" aria-label="Сводка">
        <StateFlag label="Сессии" value={String(activeCount)} />
        <StateFlag label="Свободно" value={String(readyCount)} />
        <StateFlag label="Команды" value={String(pendingCount)} critical={pendingCount > 0} />
        <StateFlag label="Нет связи" value={String(offlineCount)} critical={offlineCount > 0} />
        <StateFlag label="Проблемы" value={String(problemCount)} critical={problemCount > 0} />
        <StateFlag label="Касса" value={`4 820 ${currencyCode}`} />
      </section>
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
      <FeedbackNotice feedback={feedback} />

      <section className={`map-board ${viewMode === 'table' ? 'table-mode' : ''}`} aria-label="ПК зала">
        {visibleSeats.length === 0 ? (
          <div className="map-empty-state">
            <strong>Нет ПК в выбранном фильтре</strong>
            <span>Смените фильтр или проверьте backend карту.</span>
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
                      <span>{seat.zone}</span>
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
      setDashboardLoadError('Active branch is not assigned.');
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
      readString(item, 'title', 'Сигнал платформы'),
      readString(item, 'detail', 'Проверьте состояние в рабочей карте.'),
      readString(item, 'seatId')
    ] as const)
    : [[
      'ready',
      '-',
      dashboardLoadStatus === 'failed' ? 'Данные не загружены' : 'Нет срочных сигналов',
      dashboardLoadStatus === 'failed' ? dashboardLoadError ?? 'Повторите загрузку dashboard.' : 'Платформа не вернула срочных задач за выбранный период.',
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
      downloadTextFile(`afk4-dashboard-sales-${exportStamp}.csv`, salesCsv, 'text/csv;charset=utf-8');
      setFeedback({ label: 'Экспорт', state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: 'Экспорт', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const pulseItems = [
    { label: 'Касса', value: formatMinorUnits(totalRevenue.minorUnits, totalRevenue.currencyCode), detail: `из ${formatMinorUnits(cashTargetMinorUnits, totalRevenue.currencyCode)}`, chartValue: cashPercent, chartLabel: <><AnimatedNumber value={cashPercent} />%</>, chartSubLabel: formatCompactNumber(Math.round(totalRevenue.minorUnits / 100)), tone: 'cash', icon: Banknote },
    { label: 'Активные ПК', value: `${activePcs} / ${totalPcs}`, detail: `за ${activePeriodLabel}`, chartValue: Math.round((activePcs / totalPcs) * 100), chartLabel: <><AnimatedNumber value={activePcs} />/{totalPcs}</>, chartSubLabel: 'сейчас', tone: 'devices', icon: MonitorCheck },
    { label: 'Внимание', value: String(attentionCount), detail: `${pluralRu(attentionCount, ['сигнал', 'сигнала', 'сигналов'])} за ${activePeriodLabel}`, chartValue: Math.min(100, Math.round((attentionCount / Math.max(1, totalPcs * activeDays)) * 100)), chartLabel: <AnimatedNumber value={attentionCount} />, chartSubLabel: 'сигн.', tone: 'attention', icon: ShieldAlert },
    { label: 'Брони', value: `${bookingUsed} / ${bookingSlots}`, detail: `слоты за ${activePeriodLabel}`, chartValue: bookingSlots > 0 ? Math.min(100, Math.round((bookingUsed / bookingSlots) * 100)) : 0, chartLabel: <><AnimatedNumber value={bookingUsed} />/{bookingSlots}</>, chartSubLabel: 'слоты', tone: 'booking', icon: CalendarClock }
  ];

  const controlCards: Array<[WorkspaceId, string, string, string, LucideIcon]> = [
    ['map', 'Карта', `${totalPcs} ПК`, `${attentionCount} ${pluralRu(attentionCount, ['сигнал', 'сигнала', 'сигналов'])}`, MonitorCheck],
    ['pos', 'POS', `${posChecks} ${pluralRu(posChecks, ['чек', 'чека', 'чеков'])}`, `за ${activePeriodLabel}`, ReceiptText],
    ['payments', 'Касса', formatMinorUnits(totalRevenue.minorUnits, totalRevenue.currencyCode), `за ${activePeriodLabel}`, CircleDollarSign],
    ['players', 'Клиент', `${newClients} ${pluralRu(newClients, ['новый', 'новых', 'новых'])}`, `за ${activePeriodLabel}`, UserRoundPlus]
  ];

  return (
    <main className="workspace-screen dashboard-screen">
      <section className="screen-head dashboard-head">
        <div>
          <span>Dashboard</span>
          <h1>Что требует внимания · {activeRange.label}</h1>
        </div>
        <div className="filter-row dashboard-period-filter" aria-label="Период данных дашборда">
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
          <button type="button" className="export-button" aria-label={`Экспорт дашборда за ${exportLabel}`} onClick={exportDashboard}>
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
            <button type="button" onClick={() => openSelectedFocusSeat('Техрежим')}><Wrench size={15} /> Техрежим</button>
          </div>
          {dashboardLoadStatus === 'failed' && <FeedbackNotice feedback={{ label: 'Dashboard', state: 'failed', detail: dashboardLoadError ?? 'Dashboard data is unavailable.' }} />}
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
            <strong>карта, POS, депозит, клиент</strong>
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
    { time: '16:30', client: 'Team CS2', seats: '5 ПК', zone: 'Bootcamp', duration: '120 мин', status: 'Строгая', tone: 'strict', note: 'держать места вместе' },
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
          <span>POS</span>
          <h1>POS · продажа и кассовые операции</h1>
        </div>
      </section>

      <section className="state-strip pos-state-strip" aria-label="Сводка POS">
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
          <div className="pos-category-row" aria-label="Категории POS">
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
  const [selectedOperationKey, setSelectedOperationKey] = useState('15:08-POS продажа-PC-06 · Madina S.');
  const [selectedMethod, setSelectedMethod] = useState('Наличные');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const operations = [
    ['15:08', 'POS продажа', 'PC-06 · Madina S.', 'карта', `86 ${currencyCode}`, 'sale'],
    ['14:55', 'Возврат', 'Yusuf A.', 'наличные', `-20 ${currencyCode}`, 'refund'],
    ['14:41', 'Пополнение', 'Madina S.', 'карта', `200 ${currencyCode}`, 'deposit'],
    ['14:30', 'POS продажа', 'Гость · стойка', 'наличные', `59 ${currencyCode}`, 'sale'],
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
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Журнал смены')}><ReceiptText size={16} />Журнал смены</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Кассовый отчёт')}><Banknote size={16} />Кассовый отчёт</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Экспорт CSV')}><ArrowRightLeft size={16} />Экспорт CSV</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Расхождения')}><ShieldAlert size={16} />Расхождения</button>
          </div>
        </section>
      </section>
    </main>
  );
}

function LogsWorkspace({ currencyCode }: { currencyCode: string }) {
  const [eventSearch, setEventSearch] = useState('');
  const [activeLogFilter, setActiveLogFilter] = useState('Все события');
  const [selectedEventKey, setSelectedEventKey] = useState('15:04-PC-23 heartbeat missed');
  const [selectedSource, setSelectedSource] = useState('Agent');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const events = [
    ['15:09', 'Настройки просмотрены', 'Настройки · technician', 'audit', 'audit'],
    ['15:06', 'Пополнение депозита', `Madina S. · 200 ${currencyCode}`, 'cashier', 'money'],
    ['15:04', 'PC-23 heartbeat missed', 'Agent · нет связи 2 мин', 'warning', 'device'],
    ['15:01', 'PC-01 сессия продлена', 'operator · +15 мин', 'session', 'session'],
    ['14:55', 'Возврат по чеку', `Yusuf A. · -20 ${currencyCode}`, 'refund', 'money']
  ];
  const auditRows = [
    ['15:09', 'technician', 'Settings read', 'разрешено'],
    ['15:06', 'cashier', 'Deposit replenished', 'подтверждено'],
    ['15:01', 'operator', 'Session extend', 'платформа OK']
  ];
  const sourceCards: Array<[string, string, LucideIcon]> = [
    ['Agent', '23 онлайн · 1 офлайн', MonitorCheck],
    ['POS', '9 чеков · 1 возврат', ReceiptText],
    ['Operator', '14 действий смены', UserRoundPlus],
    ['Platform', '3 предупреждения', ShieldAlert]
  ];
  const visibleEvents = events.filter(([time, title, detail, source, tone]) => {
    const filterMatches = activeLogFilter === 'Все события'
      || (activeLogFilter === 'Только ошибки' && tone === 'warning')
      || (activeLogFilter === 'ПК и Agent' && (source === 'warning' || detail.includes('Agent')))
      || (activeLogFilter === 'Касса и POS' && tone === 'money')
      || (activeLogFilter === 'Оператор' && detail.includes('operator'))
      || (activeLogFilter === 'Системные' && source === 'audit');
    const searchMatches = `${time} ${title} ${detail} ${source}`.toLowerCase().includes(eventSearch.trim().toLowerCase());
    return filterMatches && searchMatches;
  });
  const selectedEvent = events.find(([time, title]) => `${time}-${title}` === selectedEventKey) ?? visibleEvents[0] ?? events[0];

  return (
    <main className="workspace-screen logs-screen">
      <section className="screen-head logs-head">
        <div>
          <span>Логи</span>
          <h1>Логи · аудит и события смены</h1>
        </div>
      </section>

      <section className="state-strip logs-state-strip" aria-label="Сводка логов">
        <StateFlag label="События" value="128" />
        <StateFlag label="Ошибки" value="3" critical />
        <StateFlag label="Команды" value="12" />
        <StateFlag label="Касса" value="9" />
        <StateFlag label="Аудит" value="6" />
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
            <strong>выбранная запись</strong>
          </header>
          <div className={`log-detail-card ${selectedEvent[4]}`}>
            <span>{selectedEvent[0]} · {selectedEvent[3]}</span>
            <strong>{selectedEvent[1]}</strong>
            <em>{selectedEvent[2]}</em>
          </div>
          <div className="log-detail-list">
            <div><span>Источник</span><strong>{selectedEvent[3]}</strong></div>
            <div><span>Объект</span><strong>{selectedEvent[1].includes('PC-') ? selectedEvent[1].split(' ')[0] : 'смена #24'}</strong></div>
            <div><span>Оператор</span><strong>system</strong></div>
            <div><span>Correlation</span><strong>evt-9f42</strong></div>
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="logs-panel logs-filter-panel">
          <header className="logs-panel-title">
            <span>Фильтры</span>
            <strong>сузить расследование</strong>
          </header>
          <div className="logs-filter-grid">
            {['Все события', 'Только ошибки', 'ПК и Agent', 'Касса и POS', 'Оператор', 'Системные'].map((filter) => (
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
            <span>Аудит смены</span>
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
            <strong>откуда пришли события</strong>
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
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Журнал смены')}><ReceiptText size={16} />Журнал смены</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Ошибки')}><AlertTriangle size={16} />Ошибки</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'CSV')}><ArrowRightLeft size={16} />CSV</button>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Audit trail')}><ShieldAlert size={16} />Audit trail</button>
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
    ['POS и склад', 'товары, остатки, чеки'],
    ['Интеграции', 'платежи, уведомления, экспорт']
  ];
  const rooms = [
    ['Зал A', '10 ПК', 'основной зал'],
    ['Зал B', '8 ПК', 'тихий зал'],
    ['VIP', '2 ПК', 'повышенный тариф'],
    ['Bootcamp', '4 ПК', 'командные места']
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
    ['Проверить устройства', 'Agent и Shell', Wifi]
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
          {['Owner · полный доступ', 'Manager · смены и отчёты', 'Operator · касса и карта', 'Technician · устройства'].map((role) => (
            <button key={role} type="button" onClick={() => triggerFeedback(setFeedback, role)}>
              <strong>{role.split(' · ')[0]}</strong>
              <span>{role.split(' · ')[1]}</span>
            </button>
          ))}
        </div>
      );
    }

    if (selectedSection === 'POS и склад') {
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
          {['Платежи · manual provider', 'Уведомления · выключены', 'Экспорт · CSV включён', 'API · staging'].map((item) => (
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
  const [playerSearch, setPlayerSearch] = useState('');
  const [billingPlayers, setBillingPlayers] = useState<PlayerClientItem[]>([]);
  const [selectedPlayerId, setSelectedPlayerId] = useState('');
  const [tariffOptions, setTariffOptions] = useState<TariffOptionDto[]>([]);
  const [selectedTariffVersionId, setSelectedTariffVersionId] = useState('');
  const [playerPackages, setPlayerPackages] = useState<PlayerPackageDto[]>([]);
  const [selectedPlayerPackageId, setSelectedPlayerPackageId] = useState('');
  const [billingStatus, setBillingStatus] = useState<LoadStatus>('fixture');
  const [billingError, setBillingError] = useState<string | null>(null);
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
  const hasActionableSession = Boolean(seat.activeSessionId);
  const hasActiveSession = hasActionableSession || seat.hasActiveSession === true || seat.tone === 'active';
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
  const canStartSession = actionsEnabled && canStartPermission && billingReady && !hasActionableSession && seat.tone === 'ready';
  const canExtendSession = actionsEnabled && canExtendPermission && billingReady && hasActionableSession;
  const canEndSession = actionsEnabled && canEndPermission && hasActionableSession;
  const canTransferSession = actionsEnabled && canTransferPermission && hasActionableSession && targetSeatId.length > 0;
  const confirmationText = !actionsEnabled
    ? 'Backend карта недоступна'
    : !hasAnySessionActionPermission
      ? 'Нет прав на действия с сессией'
      : !billingReady
        ? `Биллинг: ${billingMissing}`
      : feedback.state === 'idle'
        ? 'Ждём платформу'
        : feedbackText(feedback);
  const billingLoadText = billingStatus === 'backend'
    ? 'данные backend'
    : billingStatus === 'loading'
      ? 'загрузка'
      : billingStatus === 'failed'
        ? billingError ?? 'ошибка загрузки'
        : 'ожидает backend';

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
      setBillingError(backend === null ? 'Backend operator session is not available.' : null);
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
    setFeedback({ label, state: 'pending' });

    try {
      const result = await onSeatAction(request);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({
        label,
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>{seat.zone}</span>
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
            <button type="button" className="danger" disabled={!canEndSession || isBusy} onClick={() => runSeatAction('Стоп', { type: 'end', seat })}><Square size={15} />Стоп</button>
          </>
        ) : (
          <>
            <button type="button" className="start-action" disabled={!canStartSession || isBusy} onClick={() => runSeatAction('Старт 60 мин', { type: 'start', seat, billing: billingSelection })}><Plus size={15} />Старт 60 мин</button>
            <button type="button" disabled><TimerReset size={15} />Нет сессии</button>
          </>
        )}
      </section>
      {hasActiveSession && (
        <label className="context-transfer-target">
          <span>Перенести на</span>
          <select value={targetSeatId} disabled={!actionsEnabled || !canTransferPermission || isBusy || transferCandidates.length === 0} onChange={(event) => setTargetSeatId(event.currentTarget.value)}>
            {transferCandidates.length === 0 && <option value="">Нет свободных ПК</option>}
            {transferCandidates.map((candidate) => (
              <option key={candidate.id} value={candidate.id}>{candidate.name}</option>
            ))}
          </select>
        </label>
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
    dashboard: 'Смена #24',
    booking: 'Бронь 16:00',
    pos: 'Корзина',
    players: 'Amir K.',
    payments: 'Платеж 14:30',
    logs: 'Log event',
    settings: 'Настройки'
  }[workspace];

  return (
    <aside className="context-panel">
      <header className="context-head">
        <div>
          <span>Details</span>
          <h2>{title}</h2>
        </div>
        <span className="state-chip state-active">Active</span>
      </header>
      <section className="context-section">
        <div className="detail-row"><span>Revenue</span><strong>4 820 {currencyCode}</strong></div>
        <div className="detail-row"><span>Pending</span><strong>2 actions</strong></div>
        <div className="detail-row"><span>Source</span><strong>Dev demo</strong></div>
      </section>
      <button type="button" className="primary-wide">Open action</button>
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
  { name: 'Cola 0.5', priceMinorUnits: 1200, category: 'Напитки', note: 'dev demo', stockOnHand: 0, source: 'fixture' },
  { name: 'Вода 0.5', priceMinorUnits: 600, category: 'Напитки', note: 'dev demo', stockOnHand: 0, source: 'fixture' },
  { name: 'Хот-дог', priceMinorUnits: 2800, category: 'Еда', note: 'dev demo', stockOnHand: 0, source: 'fixture' },
  { name: 'Гостевой час', priceMinorUnits: 2500, category: 'Услуги', note: 'dev demo', stockOnHand: 0, source: 'fixture' }
];

function projectPosProduct(product: PosProductDto, currencyCode: string): PosCatalogItem {
  const price = readMoney(product, 'price');
  return {
    productId: readString(product, 'productId') || undefined,
    name: readString(product, 'name', 'POS item'),
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
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null);
  const [catalog, setCatalog] = useState<PosCatalogItem[]>(fixturePosProducts);
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
  const [stockWriteOffReason, setStockWriteOffReason] = useState('операторское списание POS');
  const [cartItems, setCartItems] = useState<PosCartItem[]>([
    { ...fixturePosProducts[0], quantity: 1 },
    { ...fixturePosProducts[3], quantity: 1 }
  ]);

  const loadBackendPos = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
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
      setCatalog(products.length > 0 ? products : fixturePosProducts);
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
        if (products.length === 0 || items.some((item) => item.source === 'backend')) {
          return items;
        }

        return [{ ...products[0], quantity: 1 }];
      });
      setLoadStatus('backend');
    } catch (error) {
      setLoadStatus('failed');
      setFeedback({
        label: 'POS',
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
        setFeedback({ label: 'Клиент POS', state: 'failed', detail: projectOperatorError(error).detail });
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
        throw new Error('Выберите backend товар, целое количество больше нуля и причину списания.');
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
        throw new Error('Выберите backend клиента для пополнения депозита.');
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
        throw new Error('Нет прав на создание или оплату POS продажи.');
      }

      if (!shiftId) {
        throw new Error('Open shift is required before POS payment.');
      }

      if (cartItems.length === 0 || cartItems.some((item) => !item.productId || item.source !== 'backend')) {
        throw new Error('Backend POS catalog is not loaded for the current cart.');
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
        throw new Error('Platform API returned a POS sale without sale id.');
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
    setFeedback({ label: 'Возврат по чеку', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.refundPosSale)) {
        throw new Error('Нет прав на возврат POS продажи.');
      }

      if (!selectedRefundableSaleId) {
        throw new Error('Нет backend POS продажи для возврата.');
      }

      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).pos.refundSale(selectedRefundableSaleId, {
        organizationId: nextBackend.session.organizationId,
        reason: 'operator POS refund',
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
        throw new Error('Backend POS sale id is required for receipt detail.');
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
        throw new Error('Откройте backend чек перед печатью.');
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
        throw new Error('Откройте backend чек перед экспортом.');
      }

      const receiptText = buildPosReceiptText(selectedSaleDetail, selectedReceiptRecord, currencyCode);
      const receiptNumber = readString(selectedReceiptRecord, 'receiptNumber', readString(selectedSaleDetail, 'posSaleId').slice(0, 8));
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
    setFeedback({ label: 'Аннулировать черновик', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.createPosSale) || !hasPermission(nextBackend.session, permissionNames.voidPosSale)) {
        throw new Error('Нет прав на создание или аннулирование POS продажи.');
      }

      if (!shiftId) {
        throw new Error('Открытая смена обязательна для аннулирования черновика.');
      }

      if (cartItems.length === 0 || cartItems.some((item) => !item.productId || item.source !== 'backend')) {
        throw new Error('Backend POS catalog is not loaded for the current cart.');
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
        throw new Error('Platform API returned a POS draft without sale id.');
      }

      const voidedSale = await clients.pos.voidSale(saleId, {
        organizationId: nextBackend.session.organizationId,
        reason: 'operator discarded draft cart',
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
          <span>POS</span>
          <h1>POS · продажа и кассовые операции</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Backend live')}</span>
        </div>
      </section>

      <section className="state-strip pos-state-strip" aria-label="Сводка POS">
        <StateFlag label="Продажи" value={`${salesRows.length} · ${grossSales ? formatMinorUnits(grossSales.minorUnits, grossSales.currencyCode) : `0 ${currencyCode}`}`} />
        <StateFlag label="Возвраты" value={refundsTotal ? formatMinorUnits(refundsTotal.minorUnits, refundsTotal.currencyCode) : `0 ${currencyCode}`} critical={(refundsTotal?.minorUnits ?? 0) > 0} />
        <StateFlag label="Товары" value={`${catalog.length} поз.`} />
        <StateFlag label="Склад" value={`${lowStockCount} низко`} critical={lowStockCount > 0} />
        <StateFlag label="Смена" value={shiftState} critical={!shiftId} />
      </section>

      <section className="pos-layout">
        <section className="pos-panel pos-catalog-panel">
          <header className="pos-panel-title">
            <span>Каталог</span>
            <strong>backend catalog, stock and search</strong>
          </header>
          <label className="pos-search">
            <Search size={14} />
            <input
              placeholder="Товар, услуга, SKU"
              value={productSearch}
              onChange={(event) => setProductSearch(event.currentTarget.value)}
            />
          </label>
          <div className="pos-category-row" aria-label="Категории POS">
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
            {visibleProducts.map((product) => (
              <button key={`${product.productId ?? product.name}-${product.name}`} type="button" className="pos-product-card" onClick={() => addProduct(product)}>
                <strong>{product.name}</strong>
                <span>{product.category}</span>
                <b>{formatMinorUnits(product.priceMinorUnits, currencyCode)}</b>
                <em>{product.note}</em>
              </button>
            ))}
          </div>
        </section>

        <section className="pos-panel pos-cart-panel">
          <header className="pos-panel-title">
            <span>Корзина</span>
            <strong>{shiftId ? `смена ${shiftId.slice(0, 8)}` : 'откройте смену'}</strong>
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
              aria-label="Клиент POS"
              value={playerSearch}
              disabled={backend !== null && !hasPermission(backend.session, permissionNames.viewPlayers)}
              placeholder="имя или телефон клиента"
              onChange={(event) => setPlayerSearch(event.currentTarget.value)}
            />
          </label>
          {playerSearchQuery.length > 1 && (
            <div className="pos-client-candidates" aria-label="Клиенты POS">
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
                aria-label="Имя клиента POS"
                value={newPlayerName}
                disabled={backend !== null && !hasPermission(backend.session, permissionNames.createPlayerAccount)}
                placeholder={playerSearchQuery || 'имя клиента'}
                onChange={(event) => setNewPlayerName(event.currentTarget.value)}
              />
            </label>
            <label>
              <span>Телефон</span>
              <input
                aria-label="Телефон клиента POS"
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
            {cartItems.map((item) => (
              <article key={`${item.productId ?? item.name}-${item.name}`} className="pos-cart-row interactive-row">
                <div>
                  <strong>{item.name}</strong>
                  <span>{item.quantity} шт.</span>
                </div>
                <b>{formatMinorUnits(item.priceMinorUnits * item.quantity, currencyCode)}</b>
              </article>
            ))}
          </div>
          <div className="pos-total-card">
            <span>Итого к оплате</span>
            <strong>{formatMinorUnits(cartTotalMinorUnits, currencyCode)}</strong>
            <em>{lastSale ? `последний чек ${readString(lastSale, 'posSaleId').slice(0, 8)}` : 'чек создаётся только после ответа backend'}</em>
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="pos-panel pos-payment-panel">
          <header className="pos-panel-title">
            <span>Оплата</span>
            <strong>backend sale + manual provider</strong>
          </header>
          <div className="pos-payment-methods">
            {['Наличные', 'Карта', 'Депозит'].map((method) => (
              <button
                key={method}
                type="button"
                className={paymentMethod === method ? 'active' : undefined}
                disabled={method === 'Депозит' || feedback.state === 'pending'}
                title={method === 'Депозит' ? 'Оплата с депозита требует backend ledger-контракт.' : undefined}
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
          <button type="button" className="pos-primary-action" disabled={feedback.state === 'pending'} onClick={acceptPayment}>Принять оплату</button>
          <button type="button" className="pos-secondary-action" onClick={() => setCartItems([])}>Очистить корзину</button>
        </section>

        <section className="pos-panel pos-receipts-panel">
          <header className="pos-panel-title">
            <span>Последние чеки</span>
            <strong>sales report from backend</strong>
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
                <strong>{readString(row, 'state', 'sale')}</strong>
                <em>{readNumber(row, 'lineCount', 0)} lines</em>
                <b>{formatMoney(readMoney(row, 'total'), currencyCode)}</b>
              </button>
            ))}
            {salesRows.length === 0 && (
              <article className="pos-receipt-row">
                <span>—</span>
                <strong>Чеков нет</strong>
                <em>backend</em>
                <b>0 {currencyCode}</b>
              </article>
            )}
          </div>
          {selectedSaleDetail !== null && (
            <div className="pos-sale-detail">
              <div>
                <span>Детали продажи</span>
                <strong>{readString(selectedSaleDetail, 'state', 'sale')} · {readString(selectedSaleDetail, 'posSaleId').slice(0, 8)}</strong>
                <b>{formatMoney(readMoney(selectedSaleDetail, 'total'), currencyCode)}</b>
              </div>
              {readArray(selectedSaleDetail, 'lines').slice(0, 3).map((line) => (
                <p key={`${readString(line, 'productId')}-${readNumber(line, 'quantity', 0)}`}>
                  {readString(line, 'productName', 'POS item')} · {readNumber(line, 'quantity', 0)} × {formatMoney(readMoney(line, 'unitPrice'), currencyCode)}
                </p>
              ))}
              {selectedReceiptDetail !== null && (
                <div className="pos-receipt-detail">
                  <span>Чек backend</span>
                  <strong>{readString(selectedReceiptDetail, 'receiptNumber', 'receipt')}</strong>
                  <b>{formatMoney(readMoney(selectedReceiptDetail, 'total'), currencyCode)}</b>
                  <p>{readString(selectedReceiptDetail, 'receiptType', 'sale')}</p>
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
            <strong>actions require backend confirmation</strong>
          </header>
          <div className="pos-stock-form">
            <label>
              <span>Списание</span>
              <select
                aria-label="Товар для списания POS"
                value={selectedStockProduct?.productId ?? ''}
                disabled={backendCatalogProducts.length === 0 || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffProductId(event.currentTarget.value)}
              >
                {backendCatalogProducts.length === 0 && <option value="">Нет backend товара</option>}
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
                aria-label="Количество списания POS"
                inputMode="numeric"
                value={stockWriteOffQuantity}
                disabled={!canWriteOffStock || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffQuantity(event.currentTarget.value)}
              />
            </label>
            <label className="pos-stock-reason">
              <span>Причина</span>
              <input
                aria-label="Причина списания POS"
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
              ['Возврат по чеку', 'требует выбранный backend sale', ReceiptText],
              ['Аннулировать черновик', 'создать и отменить draft', X],
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
                    void refundLatestSale();
                  } else if ((label as string) === 'Аннулировать черновик') {
                    void voidDraftCart();
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
      setLoadError('Active branch is not assigned.');
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
                  <em>{loadError ?? 'Платформа вернула пустой список за сегодня.'}</em>
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
    { name: 'Madina S.', status: 'VIP', balanceMinorUnits: 46000, debtMinorUnits: 0, last: 'dev demo', tone: 'vip', detail: 'dev demo client', phoneNumber: '+992 90 555 22 11', source: 'fixture' },
    { name: 'Amir K.', status: 'Активен', balanceMinorUnits: 12000, debtMinorUnits: 0, last: 'dev demo', tone: 'active', detail: `120 ${currencyCode}`, phoneNumber: '', source: 'fixture' },
    { name: 'Olim K.', status: 'Долг', balanceMinorUnits: 0, debtMinorUnits: 3500, last: 'dev demo', tone: 'debt', detail: 'postpaid debt', phoneNumber: '', source: 'fixture' }
  ];
}

function projectPlayerClient(player: unknown): PlayerClientItem {
  const debt = readNumber(player, 'debtBalanceMinorUnits', 0);
  const packages = readNumber(player, 'activePackageCount', 0);
  const isActive = isRecord(player) && player.isActive !== false;
  return {
    playerAccountId: readString(player, 'playerAccountId') || undefined,
    name: readString(player, 'displayName', 'Player'),
    status: debt > 0 ? 'Долг' : packages > 0 ? 'Пакет' : isActive ? 'Активен' : 'Неактивен',
    balanceMinorUnits: readNumber(player, 'walletBalanceMinorUnits', 0),
    debtMinorUnits: debt,
    last: packages > 0 ? `${packages} пак.` : 'backend',
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
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [clients, setClients] = useState<PlayerClientItem[]>(() => fixturePlayers(currencyCode));
  const [walletSummary, setWalletSummary] = useState<WalletSummaryDto | null>(null);
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [selectedClientPackages, setSelectedClientPackages] = useState<PlayerPackageDto[]>([]);
  const [walletTopUpAmount, setWalletTopUpAmount] = useState('100.00');
  const [walletTopUpReason, setWalletTopUpReason] = useState('operator wallet top-up');
  const [debtPaymentAmount, setDebtPaymentAmount] = useState('');
  const [debtPaymentReason, setDebtPaymentReason] = useState('operator debt payment');
  const [newPlayerName, setNewPlayerName] = useState('');
  const [newPlayerPhone, setNewPlayerPhone] = useState('');

  useEffect(() => {
    if (backend === null) {
      setLoadStatus('fixture');
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
        setClients(nextClients.length > 0 ? nextClients : []);
        setPackageOptions(Array.isArray(nextPackageOptions) ? nextPackageOptions : []);
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
    ?? fixturePlayers(currencyCode)[0];

  useEffect(() => {
    if (backend === null || !selectedClient.playerAccountId || selectedClient.source !== 'backend') {
      setWalletSummary(null);
      setSelectedClientPackages([]);
      return undefined;
    }

    let disposed = false;
    const loadWallet = async () => {
      try {
        const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
        const [wallet, packages] = await Promise.all([
          apiClients.players.getWalletSummary(selectedClient.playerAccountId!),
          hasPermission(backend.session, permissionNames.viewPackages) || hasPermission(backend.session, permissionNames.purchasePackage)
            ? apiClients.players.getPlayerPackages(selectedClient.playerAccountId!).catch(() => [])
            : Promise.resolve([])
        ]);
        if (!disposed) {
          setWalletSummary(wallet);
          setSelectedClientPackages(Array.isArray(packages) ? packages : []);
        }
      } catch (error) {
        if (!disposed) {
          setFeedback({ label: selectedClient.name, state: 'failed', detail: projectOperatorError(error).detail });
          setSelectedClientPackages([]);
        }
      }
    };

    void loadWallet();
    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedClient.playerAccountId, selectedClient.source]);

  const visibleClients = clients.filter((client) => {
    const segmentMatches = activeSegment === 'Все'
      || (activeSegment === 'VIP' && client.tone === 'vip')
      || (activeSegment === 'Есть долг' && client.debtMinorUnits > 0)
      || (activeSegment === 'Новые' && client.source === 'backend')
      || (activeSegment === 'Спящие' && client.status === 'Неактивен');
    const searchMatches = `${client.name} ${client.status} ${client.detail} ${client.last}`.toLowerCase().includes(clientSearch.trim().toLowerCase());
    return segmentMatches && searchMatches;
  });
  const balance = readMoney(walletSummary, 'walletBalance')?.minorUnits ?? selectedClient.balanceMinorUnits;
  const debt = readMoney(walletSummary, 'debtBalance')?.minorUnits ?? selectedClient.debtMinorUnits;
  const recentEntries = readArray(walletSummary, 'recentEntries');
  const selectedClientPackageCount = selectedClientPackages.length || Number.parseInt(selectedClient.last, 10) || 0;
  const selectedPackageOption = packageOptions[0] ?? null;
  useEffect(() => {
    if (debt <= 0) {
      setDebtPaymentAmount('');
      return;
    }

    setDebtPaymentAmount((current) => {
      const parsed = parseMoneyInputMinorUnits(current);
      return parsed !== null && parsed > 0 && parsed <= debt ? current : formatMoneyInputMinorUnits(debt);
    });
  }, [debt, selectedClient.playerAccountId]);

  const canPurchasePackage = backend !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.purchasePackage);
  const canTopUpWallet = backend !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.topUpWallet);
  const canPayDebt = backend !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && debt > 0
    && hasPermission(backend.session, permissionNames.payDebt);
  const canCreatePlayer = backend !== null && hasPermission(backend.session, permissionNames.createPlayerAccount);
  const canCreateClientReservation = backend !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.manageReservations);

  const runClientAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);

      if (label === 'Пополнить депозит') {
        if (!hasPermission(nextBackend.session, permissionNames.topUpWallet)) {
          throw new Error('Нет прав на пополнение депозита.');
        }

        if (!selectedClient.playerAccountId) {
          throw new Error('Select a backend player before wallet top-up.');
        }

        const topUpMinorUnits = parseMoneyInputMinorUnits(walletTopUpAmount);
        const reason = walletTopUpReason.trim();
        if (topUpMinorUnits === null || !reason) {
          throw new Error('Заполните сумму и причину пополнения депозита.');
        }

        const wallet = await apiClients.players.topUpWallet(selectedClient.playerAccountId, {
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

        if (!selectedClient.playerAccountId) {
          throw new Error('Select a backend player before debt payment.');
        }

        const debtPaymentMinorUnits = parseMoneyInputMinorUnits(debtPaymentAmount);
        const reason = debtPaymentReason.trim();
        if (debtPaymentMinorUnits === null || !reason || debtPaymentMinorUnits > debt) {
          throw new Error('Заполните сумму долга не больше текущего долга и причину оплаты.');
        }

        const wallet = await apiClients.players.payDebt(selectedClient.playerAccountId, {
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

        if (!selectedClient.playerAccountId || selectedClient.source !== 'backend') {
          throw new Error('Выберите backend игрока перед покупкой пакета.');
        }

        let packageOption: PackageOptionDto | null = selectedPackageOption;
        if (packageOption === null) {
          const options = await apiClients.settings.getPackageOptions(nextBackend.branchId);
          setPackageOptions(Array.isArray(options) ? options : []);
          packageOption = Array.isArray(options) ? options[0] ?? null : null;
        }

        const packageDefinitionId = readString(packageOption, 'packageDefinitionId');
        if (!packageDefinitionId) {
          throw new Error('Нет доступного backend пакета для покупки.');
        }

        await apiClients.players.purchasePackage(selectedClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          packageDefinitionId,
          idempotencyKey: createIdempotencyKey('package-purchase')
        });
        setWalletSummary(await apiClients.players.getWalletSummary(selectedClient.playerAccountId));
      } else if (label === 'Создать бронь') {
        if (!hasPermission(nextBackend.session, permissionNames.manageReservations)) {
          throw new Error('Нет прав на создание брони.');
        }

        if (!selectedClient.playerAccountId || selectedClient.source !== 'backend') {
          throw new Error('Выберите backend игрока перед созданием брони.');
        }

        await apiClients.reservations.create(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          playerAccountId: selectedClient.playerAccountId,
          seatId: null,
          customerName: selectedClient.name,
          phoneNumber: selectedClient.phoneNumber || null,
          startsAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
          durationMinutes: 60,
          source: 'operator',
          note: 'Создано из карточки клиента'
        });
      } else {
        throw new Error('Операция пока не подключена к backend.');
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
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Backend live')}</span>
        </div>
      </section>

      <section className="state-strip clients-state-strip" aria-label="Сводка клиентов">
        <StateFlag label="Клиенты" value={String(clients.length)} />
        <StateFlag label="Backend" value={String(clients.filter((client) => client.source === 'backend').length)} critical={loadStatus !== 'backend'} />
        <StateFlag label="Депозит" value={formatMinorUnits(balance, currencyCode)} />
        <StateFlag label="Долг" value={formatMinorUnits(debt, currencyCode)} critical={debt > 0} />
        <StateFlag label="Пакеты" value={String(selectedClientPackageCount)} />
        <StateFlag label="Записи" value={String(recentEntries.length)} />
      </section>

      <section className="clients-layout">
        <section className="clients-panel clients-list-panel">
          <header className="clients-panel-title">
            <span>Список клиентов</span>
            <strong>backend search by name, phone or card</strong>
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
                key={client.playerAccountId ?? client.name}
                type="button"
                className={`client-row ${client.tone}${client.playerAccountId === selectedClient.playerAccountId ? ' selected' : ''}`}
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
            ))}
          </div>
        </section>

        <section className="clients-panel clients-profile-panel">
          <header className="clients-panel-title">
            <span>Карточка клиента</span>
            <strong>выбранный игрок</strong>
          </header>
          <div className="client-profile-card">
            <div className="client-avatar">{selectedClient.name.split(' ').map((part) => part[0]).join('').slice(0, 2).toUpperCase()}</div>
            <div>
              <span>{selectedClient.status}</span>
              <strong>{selectedClient.name}</strong>
              <em>{selectedClient.phoneNumber || 'без телефона'} · {dataSourceLabel(selectedClient.source)}</em>
            </div>
          </div>
          <div className="client-metrics-grid">
            <div><span>Депозит</span><strong>{formatMinorUnits(balance, currencyCode)}</strong></div>
            <div><span>Долг</span><strong>{formatMinorUnits(debt, currencyCode)}</strong></div>
            <div><span>Пакеты</span><strong>{selectedClientPackageCount}</strong></div>
            <div><span>Источник</span><strong>{dataSourceLabel(selectedClient.source)}</strong></div>
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
                <span>backend</span>
                <b>0</b>
              </article>
            )}
          </div>
        </section>

        <section className="clients-panel clients-actions-panel">
          <header className="clients-panel-title">
            <span>Операции</span>
            <strong>money actions wait for backend</strong>
          </header>
          <div className="clients-money-form">
            <label>Сумма пополнения<input inputMode="decimal" value={walletTopUpAmount} disabled={!canTopUpWallet} onChange={(event) => setWalletTopUpAmount(event.currentTarget.value)} /></label>
            <label>Причина пополнения<input value={walletTopUpReason} disabled={!canTopUpWallet} onChange={(event) => setWalletTopUpReason(event.currentTarget.value)} /></label>
            <label>Сумма долга<input inputMode="decimal" value={debtPaymentAmount} disabled={!canPayDebt} onChange={(event) => setDebtPaymentAmount(event.currentTarget.value)} /></label>
            <label>Причина долга<input value={debtPaymentReason} disabled={!canPayDebt} onChange={(event) => setDebtPaymentReason(event.currentTarget.value)} /></label>
            <label>Имя нового клиента<input value={newPlayerName} disabled={!canCreatePlayer} onChange={(event) => setNewPlayerName(event.currentTarget.value)} /></label>
            <label>Телефон нового клиента<input value={newPlayerPhone} disabled={!canCreatePlayer} onChange={(event) => setNewPlayerPhone(event.currentTarget.value)} /></label>
          </div>
          <div className="clients-action-grid">
            {[
              ['Пополнить депозит', `${walletTopUpAmount || '0'} ${currencyCode}`, CircleDollarSign],
              ['Списать долг', debtPaymentAmount ? `${debtPaymentAmount} ${currencyCode}` : 'нет долга', ReceiptText],
              ['Купить пакет', selectedPackageOption ? readString(selectedPackageOption, 'name', 'backend package') : 'нет пакетов', TimerReset],
              ['Создать бронь', 'бронь из карточки', CalendarClock],
              ['Новая карта', newPlayerName || 'создать игрока', UserRoundPlus]
            ].map(([label, detail, Icon]) => (
              <button
                key={label as string}
                type="button"
                className="clients-action-card"
                disabled={((label as string) === 'Пополнить депозит' && !canTopUpWallet)
                  || ((label as string) === 'Списать долг' && !canPayDebt)
                  || ((label as string) === 'Купить пакет' && (!canPurchasePackage || packageOptions.length === 0))
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
            <strong>filtered backend results</strong>
          </header>
          <div className="clients-segment-grid">
            {[
              ['Все', `${clients.length} клиентов`],
              ['VIP', `${clients.filter((client) => client.tone === 'vip').length} клиентов`],
              ['Есть долг', `${clients.filter((client) => client.debtMinorUnits > 0).length} клиентов`],
              ['Спящие', 'неактивные'],
              ['Новые', 'из backend поиска']
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
            <strong>recent ledger entries</strong>
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
    return ['—', 'Нет совпадений', 'измените поиск или период', 'filter', `0 ${currencyCode}`, 'session', null];
  }

  if (loadStatus === 'loading') {
    return ['—', 'Загружаем операции', 'ждём отчёты платформы', 'backend', `0 ${currencyCode}`, 'session', null];
  }

  if (loadStatus === 'failed') {
    return ['—', 'Операции недоступны', loadError ?? 'повторите загрузку или проверьте связь', 'backend', `0 ${currencyCode}`, 'refund', null];
  }

  if (loadStatus === 'backend') {
    return ['—', 'Операций за период нет', 'платформа вернула пустой отчёт', 'backend', `0 ${currencyCode}`, 'session', null];
  }

  return ['—', 'Dev demo: операций нет', 'локальный fallback без платформы', 'dev demo', `0 ${currencyCode}`, 'session', null];
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
      readString(row, 'state', 'POS sale'),
      readString(row, 'posSaleId').slice(0, 8),
      'POS',
      formatMoney(readMoney(row, 'total'), currencyCode),
      readString(row, 'state', 'sale').toLowerCase().includes('refund') ? 'refund' : 'sale',
      row
    ]),
    ...cashRows.map((row): PaymentOperationItem => [
      formatTime(readString(row, 'createdAtUtc')),
      readString(row, 'operationType', 'Cash'),
      readString(row, 'reason', readString(row, 'sourceType', 'cash')),
      readString(row, 'sourceType', 'cash'),
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
  const selectedOperationId = selectedOperationSource === null
    ? '—'
    : (readString(selectedOperationSource, 'posSaleId') || readString(selectedOperationSource, 'operationId') || '—').slice(0, 8);
  const selectedOperationShiftId = selectedOperationSource === null
    ? '—'
    : readString(selectedOperationSource, 'shiftId', '—').slice(0, 8);
  const selectedOperationDetail = selectedOperation[3] === 'POS'
    ? `${readNumber(selectedOperationSource, 'lineCount', 0)} строк · ${readNumber(selectedOperationSource, 'itemQuantity', 0)} шт.`
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
    ['Наличные', cashIn ? formatMinorUnits(cashIn.minorUnits, cashIn.currencyCode) : `0 ${currencyCode}`, 'cash report', `${cashRows.length} операций`],
    ['Карта', netSales ? formatMinorUnits(netSales.minorUnits, netSales.currencyCode) : `0 ${currencyCode}`, 'sales report', `${salesRows.length} чеков`],
    ['Возвраты', refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`, 'refunds', 'backend'],
    ['Расхождения', difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`, 'shift close', 'backend']
  ];

  const runReportAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const exportStamp = new Date().toISOString().replace(/[:.]/g, '-');
      if (label === 'Журнал смены') {
        const csv = await apiClients.shifts.exportShiftReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-shift-report-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Кассовый отчёт') {
        const csv = await apiClients.shifts.exportCashOperationReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-cash-report-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Экспорт CSV') {
        const csv = await apiClients.shifts.exportSalesReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-sales-report-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
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
      } else if (label === 'Расхождения') {
        const report = await apiClients.shifts.getShiftReport(nextBackend.branchId, { limit: 20 });
        downloadTextFile(`afk4-shift-discrepancies-${exportStamp}.json`, JSON.stringify(report, null, 2), 'application/json;charset=utf-8');
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
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Backend reports')}</span>
        </div>
      </section>

      <section className="state-strip payments-state-strip" aria-label="Сводка платежей">
        <StateFlag label="Выручка" value={grossSales ? formatMinorUnits(grossSales.minorUnits, grossSales.currencyCode) : `0 ${currencyCode}`} />
        <StateFlag label="Наличные" value={cashIn ? formatMinorUnits(cashIn.minorUnits, cashIn.currencyCode) : `0 ${currencyCode}`} />
        <StateFlag label="Возвраты" value={refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`} critical={(refunds?.minorUnits ?? 0) > 0} />
        <StateFlag label="Смена" value={readString(currentShift, 'state', 'нет')} critical={currentShift === null} />
        <StateFlag label="К сверке" value={difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`} critical={(difference?.minorUnits ?? 0) !== 0} />
      </section>

      <section className="payments-layout">
        <section className="payments-panel payments-ledger-panel">
          <header className="payments-panel-title">
            <span>Операции смены</span>
            <strong>sales and cash report rows</strong>
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
            <strong>backend aggregates</strong>
          </header>
          <div className="payments-total-card">
            <span>Всего · выбрано {selectedOperation[0]}</span>
            <strong>{netSales ? formatMinorUnits(netSales.minorUnits, netSales.currencyCode) : `0 ${currencyCode}`}</strong>
            <em>{selectedOperation[1]} · {selectedOperation[2]} · {selectedOperation[4]}</em>
          </div>
          <div className="payments-operation-detail" aria-label="Детали выбранной операции">
            <div><span>ID</span><strong>{selectedOperationId}</strong></div>
            <div><span>Смена</span><strong>{selectedOperationShiftId}</strong></div>
            <div><span>Источник</span><strong>{selectedOperation[3]}</strong></div>
            <div><span>Деталь</span><strong>{selectedOperationDetail}</strong></div>
          </div>
          <div className="payments-metric-grid">
            <div><span>Чеков</span><strong>{salesRows.length}</strong></div>
            <div><span>Cash rows</span><strong>{cashRows.length}</strong></div>
            <div><span>Возвраты</span><strong>{refunds ? formatMinorUnits(refunds.minorUnits, refunds.currencyCode) : `0 ${currencyCode}`}</strong></div>
            <div><span>Смены</span><strong>{shiftRows.length}</strong></div>
          </div>
        </section>

        <section className="payments-panel payments-reconcile-panel">
          <header className="payments-panel-title">
            <span>Сверка кассы</span>
            <strong>read model before close</strong>
          </header>
          <div className="payments-open-form">
            <label>Старт cash<input inputMode="decimal" value={openStartingCash} disabled={!canOpenShift} onChange={(event) => setOpenStartingCash(event.currentTarget.value)} /></label>
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
          <button type="button" className="payments-primary-action" disabled={!canCloseShift} onClick={() => runReportAction('Подготовить закрытие')}>Подготовить закрытие</button>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="payments-panel payments-methods-panel">
          <header className="payments-panel-title">
            <span>Методы оплаты</span>
            <strong>backend report breakdown</strong>
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
            <strong>cash operation rows</strong>
          </header>
          <div className="payments-cash-list">
            {cashRows.slice(0, 4).map((row) => (
              <article key={readString(row, 'operationId')} className="payment-cash-row">
                <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                <strong>{readString(row, 'operationType', 'cash')}</strong>
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
            <strong>CSV from backend</strong>
          </header>
          <div className="payments-export-grid">
            {[
              ['Журнал смены', ReceiptText],
              ['Кассовый отчёт', Banknote],
              ['Экспорт CSV', ArrowRightLeft],
              ['Расхождения', ShieldAlert]
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
    return ['—', 'Нет совпадений', 'измените поиск или фильтр', 'Operator', 'audit', 'placeholder', null];
  }

  if (loadStatus === 'loading') {
    return ['—', 'Загружаем события', 'ждём audit и diagnostics', 'Platform', 'audit', 'placeholder', null];
  }

  if (loadStatus === 'failed') {
    return ['—', 'События недоступны', loadError ?? 'повторите загрузку или проверьте связь', 'Platform', 'warning', 'placeholder', null];
  }

  if (loadStatus === 'backend') {
    return ['—', 'Событий за период нет', 'audit и diagnostics вернули пустой результат', 'Platform', 'audit', 'placeholder', null];
  }

  return ['—', 'Dev demo: событий нет', 'локальный fallback без платформы', 'Platform', 'audit', 'placeholder', null];
}

function mapAuditRecordsToLogEvents(auditRecords: Record<string, unknown>[]): LogEventItem[] {
  return auditRecords.map((record): LogEventItem => [
    formatTime(readString(record, 'createdAtUtc')),
    readString(record, 'action', 'audit'),
    `${readString(record, 'targetType', 'target')} · ${readString(record, 'outcome', 'unknown')}`,
    readString(record, 'sourceApp', 'Audit'),
    readString(record, 'outcome').toLowerCase().includes('denied') || readString(record, 'outcome').toLowerCase().includes('failed') ? 'warning' : 'audit',
    'audit',
    record
  ]);
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
      `${readString(failure, 'machineName', 'Device')} ${readString(failure, 'type', 'command')}`,
      readString(failure, 'message', readString(failure, 'status', 'failed')),
      'Agent',
      'device',
      'commandFailure',
      failure
    ]),
    ...recentUpdateFailures.map((failure): LogEventItem => [
      formatTime(readString(failure, 'updatedAtUtc')),
      `${readString(failure, 'component', 'Update')} ${readString(failure, 'targetVersion', '')}`,
      readString(failure, 'message', readString(failure, 'status', 'failed')),
      'Updates',
      'warning',
      'updateFailure',
      failure
    ]),
    ...staleDevices.map((device): LogEventItem => [
      formatTime(readString(device, 'lastHeartbeatAtUtc')),
      `${readString(device, 'machineName', 'Device')} heartbeat`,
      `${readNumber(device, 'lastHeartbeatAgeSeconds', 0)} sec без heartbeat`,
      'Agent',
      'warning',
      'staleDevice',
      device
    ])
  ];
}

function shortLogValue(value: string, fallback = '—'): string {
  const trimmed = value.trim();
  if (trimmed.length === 0) {
    return fallback;
  }

  return trimmed.length > 8 ? trimmed.slice(0, 8) : trimmed;
}

function compactAuditDetails(detailsJson: string): string {
  const trimmed = detailsJson.trim();
  if (trimmed.length === 0 || trimmed === '{}') {
    return 'нет деталей';
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;
    if (!isRecord(parsed)) {
      return String(parsed).slice(0, 120);
    }

    const entries = Object.entries(parsed)
      .filter(([, value]) => value !== null && value !== undefined && String(value).trim().length > 0)
      .slice(0, 3)
      .map(([key, value]) => `${key}: ${String(value)}`);

    return entries.length > 0 ? entries.join(' · ') : 'нет деталей';
  } catch {
    return trimmed.slice(0, 120);
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
    const targetType = readString(record, 'targetType', 'target');
    const targetId = readString(record, 'targetId');

    return [
      ['Audit ID', shortLogValue(readString(record, 'auditRecordId'))],
      ['Action', readString(record, 'action', 'audit')],
      ['Outcome', readString(record, 'outcome', 'unknown')],
      ['Target', targetId ? `${targetType} ${shortLogValue(targetId)}` : targetType],
      ['Actor', shortLogValue(readString(record, 'actorStaffUserId'), 'system')],
      ['Source app', readString(record, 'sourceApp', 'Audit')],
      ['Details', compactAuditDetails(readString(record, 'detailsJson'))]
    ];
  }

  if (event[5] === 'commandFailure' && record !== null) {
    return [
      ['Device', `${readString(record, 'machineName', 'Device')} · ${shortLogValue(readString(record, 'deviceId'))}`],
      ['Command', `${readString(record, 'type', 'command')} · ${shortLogValue(readString(record, 'commandId'))}`],
      ['Status', readString(record, 'status', 'failed')],
      ['Message', readString(record, 'message', 'нет сообщения')],
      ['Updated', readString(record, 'updatedAtUtc', event[0])]
    ];
  }

  if (event[5] === 'updateFailure' && record !== null) {
    return [
      ['Device', `${readString(record, 'machineName', 'Device')} · ${shortLogValue(readString(record, 'deviceId'))}`],
      ['Rollout', shortLogValue(readString(record, 'updateRolloutId'))],
      ['Component', `${readString(record, 'component', 'Update')} ${readString(record, 'targetVersion')}`.trim()],
      ['Status', readString(record, 'status', 'failed')],
      ['Message', readString(record, 'message', 'нет сообщения')]
    ];
  }

  if (event[5] === 'staleDevice' && record !== null) {
    return [
      ['Device', `${readString(record, 'machineName', 'Device')} · ${shortLogValue(readString(record, 'deviceId'))}`],
      ['Agent', readString(record, 'agentVersion', 'unknown')],
      ['Shell', readString(record, 'shellVersion', 'unknown')],
      ['Last heartbeat', readString(record, 'lastHeartbeatAtUtc', event[0])],
      ['Age', `${readNumber(record, 'lastHeartbeatAgeSeconds', 0)} sec`]
    ];
  }

  return [
    ['Источник', event[3]],
    ['Объект', event[1].split(' ')[0]],
    ['Оператор', backend?.session.displayName ?? 'system'],
    ['Branch', backend?.branchId.slice(0, 8) ?? 'dev demo']
  ];
}

function buildLogsExportJson(
  branchId: string,
  auditRecords: Record<string, unknown>[],
  diagnostics: BranchDiagnosticsDto | null,
  events: LogEventItem[]
): string {
  return JSON.stringify({
    exportedAtUtc: new Date().toISOString(),
    branchId,
    auditRecords,
    diagnostics,
    events: events.map(([time, title, detail, source, tone, kind, record]) => ({
      time,
      title,
      detail,
      source,
      tone,
      kind,
      record
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
  const isAgent = source === 'Agent' || tone === 'device' || kind === 'commandFailure' || kind === 'staleDevice';
  const isPos = normalizedTitle.includes('pos') || normalizedDetail.includes('pos') || action.includes('pos') || targetType.includes('pos');
  const isOperator = normalizedSource.includes('operator') || normalizedTitle.includes('identity') || normalizedDetail.includes('staff') || action.includes('identity') || targetType.includes('staff');
  const isPlatform = source === 'Updates' || kind === 'updateFailure' || (!isAgent && !isPos && !isOperator);

  if (sourceFilter === 'Agent') {
    return isAgent;
  }

  if (sourceFilter === 'POS') {
    return isPos;
  }

  if (sourceFilter === 'Operator') {
    return isOperator;
  }

  if (sourceFilter === 'Platform') {
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
      || (activeLogFilter === 'ПК и Agent' && (source === 'Agent' || tone === 'device' || detail.toLowerCase().includes('device')))
      || (activeLogFilter === 'Касса и POS' && (title.toLowerCase().includes('pos') || detail.toLowerCase().includes('cash')))
      || (activeLogFilter === 'Оператор' && (source.toLowerCase().includes('operator') || title.toLowerCase().includes('identity') || detail.toLowerCase().includes('staff')))
      || (activeLogFilter === 'Системные' && (source !== 'Agent' || title.toLowerCase().includes('updates') || title.toLowerCase().includes('diagnostics')));
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
    ['Agent', `${events.filter((event) => matchesLogSource(event, 'Agent')).length} событий · ${readNumber(deviceSummary, 'staleDevices', 0)} stale`, MonitorCheck],
    ['POS', `${events.filter((event) => matchesLogSource(event, 'POS')).length} audit`, ReceiptText],
    ['Operator', `${events.filter((event) => matchesLogSource(event, 'Operator')).length} действий`, UserRoundPlus],
    ['Platform', `${events.filter((event) => matchesLogSource(event, 'Platform')).length} событий`, ShieldAlert]
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
      'ПК и Agent': { action: '', outcome: '', targetType: 'Device', limit: 50 },
      'Касса и POS': { action: 'pos.sales.create', outcome: '', targetType: '', limit: 50 },
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
      if (label === 'Audit trail') {
        const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, { action: '', outcome: '', targetType: '', fromUtc: '', toUtc: '', limit: 100 }));
        const nextAuditRecords = readArray<Record<string, unknown>>(audit, 'records');
        setAuditResult(audit);
        downloadTextFile(
          `afk4-audit-trail-${exportStamp}.json`,
          buildLogsExportJson(nextBackend.branchId, nextAuditRecords, diagnostics, [...mapDiagnosticsToLogEvents(diagnostics), ...mapAuditRecordsToLogEvents(nextAuditRecords)]),
          'application/json;charset=utf-8'
        );
      } else if (label === 'CSV') {
        const csv = await apiClients.shifts.exportOperatorActionReportCsv(nextBackend.branchId, { limit: 100 });
        downloadTextFile(`afk4-operator-actions-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
      } else if (label === 'Ошибки') {
        const audit = await apiClients.audit.search(buildAuditSearchRequest(nextBackend, { action: '', outcome: 'denied', targetType: '', fromUtc: '', toUtc: '', limit: 50 }));
        const nextAuditRecords = readArray<Record<string, unknown>>(audit, 'records');
        setAuditResult(audit);
        const failureEvents = [...mapDiagnosticsToLogEvents(diagnostics), ...mapAuditRecordsToLogEvents(nextAuditRecords)]
          .filter((event) => event[4] === 'warning');
        downloadTextFile(
          `afk4-log-errors-${exportStamp}.json`,
          buildLogsExportJson(nextBackend.branchId, nextAuditRecords, diagnostics, failureEvents),
          'application/json;charset=utf-8'
        );
      } else {
        const csv = await apiClients.shifts.exportShiftReportCsv(nextBackend.branchId, { limit: 50 });
        downloadTextFile(`afk4-shift-log-${exportStamp}.csv`, csv, 'text/csv;charset=utf-8');
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
          <span>Логи</span>
          <h1>Логи · аудит и события смены</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Backend audit')}</span>
        </div>
      </section>

      <section className="state-strip logs-state-strip" aria-label="Сводка логов">
        <StateFlag label="События" value={String(events.length)} />
        <StateFlag label="Ошибки" value={String(events.filter((event) => event[4] === 'warning').length)} critical={events.some((event) => event[4] === 'warning')} />
        <StateFlag label="Команды" value={String(readNumber(commandSummary, 'pendingCommands', 0))} />
        <StateFlag label="Audit" value={String(auditRecords.length)} />
        <StateFlag label="Источник" value={loadStatus} critical={loadStatus !== 'backend'} />
      </section>

      <section className="logs-layout">
        <section className="logs-panel logs-events-panel">
          <header className="logs-panel-title">
            <span>Журнал событий</span>
            <strong>audit search + diagnostics</strong>
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
            <strong>выбранная запись</strong>
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
            <strong>сузить расследование</strong>
          </header>
          <div className="logs-filter-grid">
            {['Все события', 'Только ошибки', 'ПК и Agent', 'Касса и POS', 'Оператор', 'Системные'].map((filter) => (
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
            <div className="logs-period-presets" aria-label="Период audit">
              {auditPeriodPresetOptions.map(([label, preset]) => (
                <button key={preset} type="button" onClick={() => void applyAuditPeriodPreset(label, preset)}>{label}</button>
              ))}
            </div>
            <label>Audit action<input value={auditActionFilter} onChange={(event) => setAuditActionFilter(event.currentTarget.value)} placeholder="sessions.start" /></label>
            <label>Outcome<input value={auditOutcomeFilter} onChange={(event) => setAuditOutcomeFilter(event.currentTarget.value)} placeholder="succeeded / denied" /></label>
            <label>Target type<input value={auditTargetTypeFilter} onChange={(event) => setAuditTargetTypeFilter(event.currentTarget.value)} placeholder="Session" /></label>
            <label>From UTC<input value={auditFromUtcFilter} onChange={(event) => setAuditFromUtcFilter(event.currentTarget.value)} placeholder="2026-05-21T00:00:00Z" /></label>
            <label>To UTC<input value={auditToUtcFilter} onChange={(event) => setAuditToUtcFilter(event.currentTarget.value)} placeholder="2026-05-21T23:59:59Z" /></label>
            <label>Limit<input inputMode="numeric" value={auditLimit} onChange={(event) => setAuditLimit(event.currentTarget.value)} /></label>
            <button type="button" onClick={() => applyAuditSearch('Применить audit')}>Применить audit</button>
          </div>
        </section>

        <section className="logs-panel logs-audit-panel">
          <header className="logs-panel-title">
            <span>Аудит смены</span>
            <strong>последние записи backend</strong>
          </header>
          <div className="logs-audit-list">
            {auditRecords.slice(0, 4).map((record) => (
              <article key={readString(record, 'auditRecordId')} className="log-audit-row">
                <span>{formatTime(readString(record, 'createdAtUtc'))}</span>
                <strong>{readString(record, 'actorStaffUserId', 'system').slice(0, 8)}</strong>
                <em>{readString(record, 'action', 'audit')}</em>
                <b>{readString(record, 'outcome', 'ok')}</b>
              </article>
            ))}
          </div>
        </section>

        <section className="logs-panel logs-sources-panel">
          <header className="logs-panel-title">
            <span>Источники</span>
            <strong>откуда пришли события</strong>
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
              ['Журнал смены', ReceiptText],
              ['Ошибки', AlertTriangle],
              ['CSV', ArrowRightLeft],
              ['Audit trail', ShieldAlert]
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
  const [tariffs, setTariffs] = useState<TariffOptionDto[]>([]);
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [deviceAssignmentDeviceId, setDeviceAssignmentDeviceId] = useState('');
  const [deviceAssignmentSeatId, setDeviceAssignmentSeatId] = useState('');
  const [enrollmentExpiresSeconds, setEnrollmentExpiresSeconds] = useState('900');
  const [enrollmentCode, setEnrollmentCode] = useState<Record<string, unknown> | null>(null);
  const [deviceDetail, setDeviceDetail] = useState<Record<string, unknown> | null>(null);
  const [credentialIdToRevoke, setCredentialIdToRevoke] = useState('');
  const [rotatedCredential, setRotatedCredential] = useState<Record<string, unknown> | null>(null);
  const [deviceCommandType, setDeviceCommandType] = useState('lock');
  const [deviceCommandReason, setDeviceCommandReason] = useState('operator device tool');
  const [lastDeviceCommand, setLastDeviceCommand] = useState<Record<string, unknown> | null>(null);
  const [layoutZoneName, setLayoutZoneName] = useState('Main Hall');
  const [layoutZoneSortOrder, setLayoutZoneSortOrder] = useState('10');
  const [layoutSeatZoneId, setLayoutSeatZoneId] = useState('');
  const [layoutSeatName, setLayoutSeatName] = useState('PC-01');
  const [layoutSeatSortOrder, setLayoutSeatSortOrder] = useState('10');
  const [inviteUserName, setInviteUserName] = useState('operator');
  const [inviteDisplayName, setInviteDisplayName] = useState('Новый оператор');
  const [invitePassword, setInvitePassword] = useState('ChangeMe123!');
  const [inviteRoleName, setInviteRoleName] = useState('cashier_operator');
  const [selectedStaffUserId, setSelectedStaffUserId] = useState('');
  const [staffRoleName, setStaffRoleName] = useState('cashier_operator');
  const [productCategoryName, setProductCategoryName] = useState('POS category 1');
  const [productName, setProductName] = useState('POS item 1');
  const [productSku, setProductSku] = useState('POS-001');
  const [productPrice, setProductPrice] = useState('12.00');
  const [productTrackStock, setProductTrackStock] = useState(true);
  const [productAllowNegativeStock, setProductAllowNegativeStock] = useState(false);
  const [selectedProductId, setSelectedProductId] = useState('');
  const [stockProductId, setStockProductId] = useState('');
  const [stockMovementType, setStockMovementType] = useState('purchase');
  const [stockQuantityDelta, setStockQuantityDelta] = useState('10');
  const [stockUnitCost, setStockUnitCost] = useState('0.00');
  const [stockReason, setStockReason] = useState('initial stock');
  const [tariffName, setTariffName] = useState('Day Pass');
  const [tariffPricePerHour, setTariffPricePerHour] = useState('90.00');
  const [tariffMinimumMinutes, setTariffMinimumMinutes] = useState('15');
  const [tariffRoundingMinutes, setTariffRoundingMinutes] = useState('5');
  const [packageName, setPackageName] = useState('Night 5h');
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
  const [updateSizeBytes, setUpdateSizeBytes] = useState('1048576');
  const [updateReleaseNotes, setUpdateReleaseNotes] = useState('Operator App update package.');
  const [rolloutPackageId, setRolloutPackageId] = useState('');
  const [rolloutChannel, setRolloutChannel] = useState('internal');
  const [rolloutTargetKind, setRolloutTargetKind] = useState('branch');
  const [rolloutTargetDeviceIds, setRolloutTargetDeviceIds] = useState('');
  const [rolloutBatchPercent, setRolloutBatchPercent] = useState('100');
  const [rolloutStartsAtUtc, setRolloutStartsAtUtc] = useState(() => new Date(Date.now() + 60 * 60 * 1000).toISOString());
  const [rolloutReason, setRolloutReason] = useState('Operator rollout.');
  const [packageStatePackageId, setPackageStatePackageId] = useState('');
  const [packageState, setPackageState] = useState('validated');
  const [packageStateReason, setPackageStateReason] = useState('Signature verified.');
  const [rolloutStateRolloutId, setRolloutStateRolloutId] = useState('');
  const [rolloutState, setRolloutState] = useState('paused');
  const [rolloutStateReason, setRolloutStateReason] = useState('Operator state change.');

  const loadSettings = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      return;
    }

    setLoadStatus('loading');
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [branchProfile, staff, layoutZones, products, stockMovementRows, branchDiagnostics, rolloutStatuses, tariffOptions, packageOptionRows] = await Promise.all([
        apiClients.settings.getBranchProfile(nextBackend.branchId),
        apiClients.settings.getStaffUsers(nextBackend.branchId),
        apiClients.settings.getLayoutZones(nextBackend.branchId),
        apiClients.pos.getCatalog(nextBackend.branchId),
        apiClients.inventory.getStockMovements(nextBackend.branchId, { limit: 8 }).catch(() => []),
        apiClients.diagnostics.getDiagnostics(nextBackend.branchId),
        apiClients.updates.getRolloutStatuses(nextBackend.branchId),
        apiClients.settings.getTariffOptions(nextBackend.branchId),
        apiClients.settings.getPackageOptions(nextBackend.branchId).catch(() => [])
      ]);
      const productRows = Array.isArray(products) ? products : [];
      const staffRows = Array.isArray(staff) ? staff : [];
      const selectedStaff = staffRows.find((user) => readString(user, 'staffUserId') === selectedStaffUserId) ?? staffRows[0];
      const selectedStaffRole = readArray<string>(selectedStaff, 'roleNames')
        .find((role) => staffRoleOptions.includes(role as (typeof staffRoleOptions)[number]));
      setStaffUsers(staffRows);
      setSelectedStaffUserId(readString(selectedStaff, 'staffUserId'));
      setStaffRoleName(selectedStaffRole ?? 'cashier_operator');
      const zoneRows = Array.isArray(layoutZones) ? layoutZones : [];
      setZones(zoneRows);
      const firstZoneId = readString(zoneRows[0], 'zoneId');
      setLayoutSeatZoneId((current) => zoneRows.some((zone) => readString(zone, 'zoneId') === current) ? current : firstZoneId);
      const firstSeatId = zoneRows.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats')).map((seat) => readString(seat, 'seatId')).find(Boolean) ?? '';
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
      setRolloutPackageId((current) => isGuid(current) ? current : readString(rolloutRows[0], 'updatePackageId'));
      setPackageStatePackageId((current) => isGuid(current) ? current : readString(rolloutRows[0], 'updatePackageId'));
      setRolloutStateRolloutId((current) => isGuid(current) ? current : readString(rolloutRows[0], 'updateRolloutId'));
      setTariffs(Array.isArray(tariffOptions) ? tariffOptions : []);
      const packageRows = Array.isArray(packageOptionRows) ? packageOptionRows : [];
      setPackageOptions(packageRows);
      setSelectedPackageDefinitionId((current) => packageRows.some((option) => readString(option, 'packageDefinitionId') === current) ? current : '');
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

  const sections = [
    ['Профиль клуба', 'название, город, валюта'],
    ['Залы и ПК', 'зоны, рабочие места, статусы'],
    ['Тарифы', 'пакеты, постоплата, VIP'],
    ['Персонал', 'операторы, роли, доступы'],
    ['POS и склад', 'товары, остатки, чеки'],
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
  const canDispatchDeviceCommand = backend !== null && hasPermission(backend.session, permissionNames.dispatchDeviceCommand);
  const canRotateDeviceCredential = backend !== null && hasPermission(backend.session, permissionNames.rotateDeviceCredential);
  const canRevokeDeviceCredential = backend !== null && hasPermission(backend.session, permissionNames.revokeDeviceCredential);
  const layoutSeatOptions = zones.flatMap((zone) => readArray<Record<string, unknown>>(zone, 'seats').map((seat) => ({
    seatId: readString(seat, 'seatId'),
    label: `${readString(zone, 'name', 'Zone')} · ${readString(seat, 'name', 'Seat')}`
  }))).filter((seat) => isGuid(seat.seatId));
  const trackedCatalog = catalog.filter((product) => readBoolean(product, 'trackStock'));
  const selectCatalogProduct = (product: PosProductDto) => {
    const productId = readString(product, 'productId');
    const price = readMoney(product, 'price');
    setSelectedProductId(productId);
    setProductName(readString(product, 'name', productName));
    setProductSku(readString(product, 'sku', productSku));
    setProductPrice(price ? formatMoneyInputMinorUnits(price.minorUnits) : productPrice);
    setProductTrackStock(readBoolean(product, 'trackStock', true));
    setProductAllowNegativeStock(readBoolean(product, 'allowNegativeStock'));
    triggerFeedback(setFeedback, readString(product, 'name', 'Product'), 'confirmed');
  };
  const selectPackageOption = (option: PackageOptionDto) => {
    setSelectedPackageDefinitionId(readString(option, 'packageDefinitionId'));
    setPackageName(readString(option, 'name', packageName));
    setPackagePrice(formatMoneyInputMinorUnits(readNumber(option, 'priceMinorUnits', 0)));
    setPackageMinutes(String(Math.round(readNumber(option, 'includedSeconds', 0) / 60)));
    setPackageBonusMinutes(String(Math.round(readNumber(option, 'bonusSeconds', 0) / 60)));
    setPackageExpiresDays(String(readNumber(option, 'expiresAfterDays', 30)));
    triggerFeedback(setFeedback, readString(option, 'name', 'Package'), 'confirmed');
  };
  const readiness = [
    ['Профиль клуба', `${clubName} · ${city}`],
    ['Залы и ПК', `${zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0)} рабочих мест`],
    ['Персонал', `${staffUsers.length} сотрудников`],
    ['Касса', `${currencyCode} · backend reports`],
    ['Устройства', `${readNumber(deviceSummary, 'onlineDevices', 0)} из ${readNumber(deviceSummary, 'totalDevices', 0)} онлайн`]
  ];
  const actions: Array<[string, string, LucideIcon]> = [
    ['Добавить ПК', 'новое рабочее место', MonitorCheck],
    ['Создать тариф', 'backend tariff/version', CircleDollarSign],
    ['Пригласить сотрудника', canManageBranchStaff ? 'создать staff user' : 'нет прав доступа', UserRoundPlus],
    ['Проверить устройства', 'diagnostics refresh', Wifi]
  ];

  const runSettingsAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === 'Проверить устройства') {
        setDiagnostics(await apiClients.diagnostics.getDiagnostics(nextBackend.branchId));
      } else if (label === 'Создать код подключения') {
        if (!hasPermission(nextBackend.session, permissionNames.createDeviceEnrollmentCode)) {
          throw new Error('Нет прав на создание кода подключения устройства.');
        }

        const expiresInSeconds = Number(enrollmentExpiresSeconds);
        if (!Number.isInteger(expiresInSeconds) || expiresInSeconds < 60 || expiresInSeconds > 86400) {
          throw new Error('Срок действия кода должен быть от 60 до 86400 секунд.');
        }

        const code = await apiClients.devices.createEnrollmentCode(nextBackend.branchId, nextBackend.session.organizationId, expiresInSeconds);
        setEnrollmentCode(code);
      } else if (label === 'Назначить устройство') {
        if (!hasPermission(nextBackend.session, permissionNames.assignDeviceSeat)) {
          throw new Error('Нет прав на назначение устройства рабочему месту.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const seatId = deviceAssignmentSeatId.trim();
        if (!isGuid(deviceId) || !isGuid(seatId)) {
          throw new Error('Укажите корректные device id и seat id.');
        }

        await apiClients.settings.assignDeviceSeat(nextBackend.branchId, deviceId, {
          organizationId: nextBackend.session.organizationId,
          seatId
        });
        if (hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          setDeviceDetail(await apiClients.devices.getDeviceDetail(deviceId));
        }
        await loadSettings(nextBackend);
      } else if (label === 'Открыть карточку устройства') {
        if (!hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
          throw new Error('Нет прав на просмотр карточки устройства.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        if (!isGuid(deviceId)) {
          throw new Error('Укажите корректный device id.');
        }

        setDeviceDetail(await apiClients.devices.getDeviceDetail(deviceId));
      } else if (label === 'Отправить команду') {
        if (!hasPermission(nextBackend.session, permissionNames.dispatchDeviceCommand)) {
          throw new Error('Нет прав на отправку команд устройствам.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const type = deviceCommandType.trim();
        const reason = deviceCommandReason.trim();
        if (!isGuid(deviceId) || !type || !reason) {
          throw new Error('Укажите device id, тип команды и причину.');
        }

        const command = await apiClients.devices.dispatchDeviceCommand(deviceId, {
          type,
          payload: {
            reason,
            source: 'operator-settings'
          }
        });
        setLastDeviceCommand(command);
      } else if (label === 'Сменить credential') {
        if (!hasPermission(nextBackend.session, permissionNames.rotateDeviceCredential)) {
          throw new Error('Нет прав на смену credential устройства.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        if (!isGuid(deviceId)) {
          throw new Error('Укажите корректный device id.');
        }

        const rotated = await apiClients.devices.rotateDeviceCredential(deviceId);
        setRotatedCredential(rotated);
        setCredentialIdToRevoke(readString(rotated, 'credentialId'));
      } else if (label === 'Отозвать credential') {
        if (!hasPermission(nextBackend.session, permissionNames.revokeDeviceCredential)) {
          throw new Error('Нет прав на отзыв credential устройства.');
        }

        const deviceId = deviceAssignmentDeviceId.trim();
        const credentialId = credentialIdToRevoke.trim();
        if (!isGuid(deviceId) || !isGuid(credentialId)) {
          throw new Error('Укажите корректные device id и credential id.');
        }

        await apiClients.devices.revokeDeviceCredential(deviceId, credentialId);
        setRotatedCredential(null);
      } else if (label === 'Добавить зал') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const name = layoutZoneName.trim();
        const sortOrder = Number(layoutZoneSortOrder);
        if (!name || !Number.isInteger(sortOrder)) {
          throw new Error('Заполните название зала и целый порядок сортировки.');
        }

        const zone = await apiClients.settings.createZone(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name,
          sortOrder
        });
        setZones((items) => [...items, zone]);
        setLayoutSeatZoneId(readString(zone, 'zoneId'));
        setLayoutZoneName(`Zone ${zones.length + 2}`);
        setLayoutZoneSortOrder(String(sortOrder + 10));
      } else if (label === 'Добавить ПК') {
        if (!hasPermission(nextBackend.session, permissionNames.manageLayout)) {
          throw new Error('Нет прав на управление залами и рабочими местами.');
        }

        const zoneId = layoutSeatZoneId.trim();
        if (!zoneId) {
          throw new Error('Create a layout zone before adding a seat.');
        }

        const name = layoutSeatName.trim();
        const sortOrder = Number(layoutSeatSortOrder);
        if (!name || !Number.isInteger(sortOrder)) {
          throw new Error('Заполните название ПК и целый порядок сортировки.');
        }

        await apiClients.settings.createSeat(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          zoneId,
          name,
          sortOrder
        });
        setLayoutSeatName(`PC-${zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0) + 2}`);
        setLayoutSeatSortOrder(String(sortOrder + 10));
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
        setTariffName(`Tariff ${tariffs.length + 2}`);
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
        if (!userName || !displayName || !invitePassword) {
          throw new Error('Заполните имя пользователя, имя сотрудника и временный пароль.');
        }

        const staffUser = await apiClients.settings.createStaffUser(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          userName,
          displayName,
          password: invitePassword,
          roleNames: [inviteRoleName || 'cashier_operator']
        });
        setStaffUsers((items) => [...items, staffUser]);
        setSelectedStaffUserId(readString(staffUser, 'staffUserId'));
        setStaffRoleName(readArray<string>(staffUser, 'roleNames')[0] ?? 'cashier_operator');
        setInviteUserName(`operator${staffUsers.length + 2}`);
        setInviteDisplayName('Новый оператор');
        setInvitePassword('ChangeMe123!');
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
      } else if (label === 'Создать товар') {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error('Нет прав на управление POS каталогом.');
        }

        const categoryName = productCategoryName.trim();
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!categoryName || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error('Заполните категорию, товар, SKU и цену больше нуля.');
        }

        const category = await apiClients.settings.createProductCategory(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name: categoryName,
          idempotencyKey: createIdempotencyKey('pos-category-create')
        });
        const categoryId = readString(category, 'categoryId');
        if (!categoryId) {
          throw new Error('Platform API returned a POS category without category id.');
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
        setProductCategoryName(`POS category ${nextIndex}`);
        setProductName(`POS item ${nextIndex}`);
        setProductSku(`POS-${String(nextIndex).padStart(3, '0')}`);
        setProductPrice('12.00');
        setProductTrackStock(true);
        setProductAllowNegativeStock(false);
        setSelectedProductId(readString(product, 'productId'));
      } else if (label === 'Обновить товар' || label === 'Снять с продажи') {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error('Нет прав на управление POS каталогом.');
        }

        const selectedProduct = catalog.find((product) => readString(product, 'productId') === selectedProductId);
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!selectedProduct || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error('Выберите товар и заполните товар, SKU и цену больше нуля.');
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
      } else if (label === 'Зарегистрировать пакет обновления') {
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
        const sizeBytes = Number(updateSizeBytes);
        if (!component || !version || !channel || !artifactUri || !sha256 || !signature || !signatureAlgorithm
          || !Number.isInteger(sizeBytes) || sizeBytes <= 0) {
          throw new Error('Заполните component, version, channel, artifact URL, sha256, signature, algorithm и size.');
        }

        try {
          new URL(artifactUri);
        } catch {
          throw new Error('Artifact URL должен быть абсолютным URL.');
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
        if (updatePackageId) {
          setRolloutPackageId(updatePackageId);
          setPackageStatePackageId(updatePackageId);
        }
      } else if (label === 'Создать rollout обновления') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdateRollouts)) {
          throw new Error('Нет прав на управление rollout обновлений.');
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
          throw new Error('Заполните package id, channel, target, batch, start UTC и reason для rollout.');
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
          throw new Error('Заполните package id, state и reason.');
        }

        await apiClients.updates.changePackageState(nextBackend.branchId, updatePackageId, {
          organizationId: nextBackend.session.organizationId,
          state,
          reason
        });
      } else if (label === 'Изменить состояние rollout') {
        if (!hasPermission(nextBackend.session, permissionNames.manageUpdateRollouts)) {
          throw new Error('Нет прав на управление rollout обновлений.');
        }

        const updateRolloutId = rolloutStateRolloutId.trim();
        const state = rolloutState.trim();
        const reason = rolloutStateReason.trim();
        if (!isGuid(updateRolloutId) || !state || !reason) {
          throw new Error('Заполните rollout id, state и reason.');
        }

        const rollout = await apiClients.updates.changeRolloutState(nextBackend.branchId, updateRolloutId, {
          organizationId: nextBackend.session.organizationId,
          state,
          reason
        });
        setRollouts((items) => items.map((item) => readString(item, 'updateRolloutId') === updateRolloutId ? rollout : item));
      } else {
        throw new Error('Backend endpoint for this settings action is not implemented yet.');
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
            <button type="button" onClick={() => runSettingsAction('Добавить зал')}>Добавить зал</button>
          </div>
          <div className="settings-form-grid settings-layout-form">
            <label>Название зала<input value={layoutZoneName} disabled={!canManageLayout} onChange={(event) => setLayoutZoneName(event.currentTarget.value)} /></label>
            <label>Сортировка зала<input inputMode="numeric" value={layoutZoneSortOrder} disabled={!canManageLayout} onChange={(event) => setLayoutZoneSortOrder(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canManageLayout} onClick={() => runSettingsAction('Добавить зал')}>Создать зал</button>
            <label>Зона ПК
              <select value={layoutSeatZoneId} disabled={!canManageLayout || zones.length === 0} onChange={(event) => setLayoutSeatZoneId(event.currentTarget.value)}>
                {zones.length === 0 && <option value="">нет залов</option>}
                {zones.map((zone) => (
                  <option key={readString(zone, 'zoneId')} value={readString(zone, 'zoneId')}>{readString(zone, 'name', 'Zone')}</option>
                ))}
              </select>
            </label>
            <label>Название ПК<input value={layoutSeatName} disabled={!canManageLayout} onChange={(event) => setLayoutSeatName(event.currentTarget.value)} /></label>
            <label>Сортировка ПК<input inputMode="numeric" value={layoutSeatSortOrder} disabled={!canManageLayout} onChange={(event) => setLayoutSeatSortOrder(event.currentTarget.value)} /></label>
            <button type="button" disabled={!canManageLayout || !layoutSeatZoneId} onClick={() => runSettingsAction('Добавить ПК')}>Создать ПК</button>
          </div>
          <div className="settings-room-grid">
            {zones.map((zone) => (
              <button key={readString(zone, 'zoneId')} type="button" className="settings-room-card" onClick={() => triggerFeedback(setFeedback, readString(zone, 'name', 'Zone'), 'confirmed')}>
                <strong>{readString(zone, 'name', 'Zone')}</strong>
                <b>{readArray(zone, 'seats').length} ПК</b>
                <span>sort {readNumber(zone, 'sortOrder', 0)}</span>
              </button>
            ))}
          </div>
          <div className="settings-section-title">
            <span>Подключение устройств</span>
            <button type="button" disabled={!canCreateDeviceEnrollmentCode} onClick={() => runSettingsAction('Создать код подключения')}>Создать код подключения</button>
            <button type="button" disabled={!canAssignDeviceSeat || layoutSeatOptions.length === 0} onClick={() => runSettingsAction('Назначить устройство')}>Назначить устройство</button>
          </div>
          <div className="settings-form-grid settings-device-form">
            <label>Срок кода, сек<input inputMode="numeric" value={enrollmentExpiresSeconds} disabled={!canCreateDeviceEnrollmentCode} onChange={(event) => setEnrollmentExpiresSeconds(event.currentTarget.value)} /></label>
            <label>Код подключения<input value={readString(enrollmentCode, 'code', '—')} readOnly /></label>
            <label>Device id<input value={deviceAssignmentDeviceId} disabled={!canAssignDeviceSeat && !canViewDeviceDetail} onChange={(event) => setDeviceAssignmentDeviceId(event.currentTarget.value)} /></label>
            <label>Рабочее место
              <select value={deviceAssignmentSeatId} disabled={!canAssignDeviceSeat || layoutSeatOptions.length === 0} onChange={(event) => setDeviceAssignmentSeatId(event.currentTarget.value)}>
                {layoutSeatOptions.length === 0 && <option value="">нет рабочих мест</option>}
                {layoutSeatOptions.map((seat) => (
                  <option key={seat.seatId} value={seat.seatId}>{seat.label}</option>
                ))}
              </select>
            </label>
            <label>Карточка устройства<input value={deviceDetail ? `${readString(deviceDetail, 'machineName', 'Device')} · ${readString(deviceDetail, 'seatName', 'без места')}` : 'не открыта'} readOnly /></label>
            <button type="button" disabled={!canViewDeviceDetail || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction('Открыть карточку устройства')}>Открыть карточку устройства</button>
            <label>Команда
              <select value={deviceCommandType} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandType(event.currentTarget.value)}>
                <option value="lock">lock</option>
                <option value="unlock">unlock</option>
              </select>
            </label>
            <label>Причина команды<input value={deviceCommandReason} disabled={!canDispatchDeviceCommand} onChange={(event) => setDeviceCommandReason(event.currentTarget.value)} /></label>
            <label>Последняя команда<input value={lastDeviceCommand ? `${readString(lastDeviceCommand, 'type', 'command')} · ${readString(lastDeviceCommand, 'commandId').slice(0, 8)}` : 'не отправлена'} readOnly /></label>
            <button type="button" disabled={!canDispatchDeviceCommand || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction('Отправить команду')}>Отправить команду</button>
            <label>Credential id<input value={credentialIdToRevoke} disabled={!canRevokeDeviceCredential} onChange={(event) => setCredentialIdToRevoke(event.currentTarget.value)} /></label>
            <label>Новый credential<input value={readString(rotatedCredential, 'credentialId', '—')} readOnly /></label>
            <label className="settings-form-wide">Секрет credential<input value={readString(rotatedCredential, 'credentialSecret', '—')} readOnly /></label>
            <button type="button" disabled={!canRotateDeviceCredential || !isGuid(deviceAssignmentDeviceId)} onClick={() => runSettingsAction('Сменить credential')}>Сменить credential</button>
            <button type="button" disabled={!canRevokeDeviceCredential || !isGuid(deviceAssignmentDeviceId) || !isGuid(credentialIdToRevoke)} onClick={() => runSettingsAction('Отозвать credential')}>Отозвать credential</button>
          </div>
        </>
      );
    }

    if (selectedSection === 'Тарифы') {
      return (
        <>
          <div className="settings-section-title">
            <span>Тарифы</span>
            <button type="button" disabled={!canManageTariffs} onClick={() => runSettingsAction('Создать тариф')}>Создать тариф</button>
          </div>
          <div className="settings-form-grid settings-tariff-form">
            <label>Название тарифа<input value={tariffName} disabled={!canManageTariffs} onChange={(event) => setTariffName(event.currentTarget.value)} /></label>
            <label>Цена/час<input inputMode="decimal" value={tariffPricePerHour} disabled={!canManageTariffs} onChange={(event) => setTariffPricePerHour(event.currentTarget.value)} /></label>
            <label>Минимум мин<input inputMode="numeric" value={tariffMinimumMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffMinimumMinutes(event.currentTarget.value)} /></label>
            <label>Округление мин<input inputMode="numeric" value={tariffRoundingMinutes} disabled={!canManageTariffs} onChange={(event) => setTariffRoundingMinutes(event.currentTarget.value)} /></label>
          </div>
          <div className="settings-tariff-list">
            {tariffs.map((tariff) => (
              <button key={readString(tariff, 'tariffVersionId')} type="button" className="settings-tariff-row" onClick={() => triggerFeedback(setFeedback, readString(tariff, 'name', 'Tariff'), 'confirmed')}>
                <strong>{readString(tariff, 'name', 'Tariff')}</strong>
                <b>{formatMinorUnits(readNumber(tariff, 'pricePerMinuteMinorUnits', 0) * 60, readString(tariff, 'currencyCode', currencyCode))} / час</b>
                <span>v{readNumber(tariff, 'versionNumber', 0)} · {readString(tariff, 'tariffRuleVersionId', 'rule')}</span>
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
                <strong>{readString(option, 'name', 'Package')}</strong>
                <b>{formatMinorUnits(readNumber(option, 'priceMinorUnits', 0), readString(option, 'currencyCode', currencyCode))}</b>
                <span>{Math.round(readNumber(option, 'includedSeconds', 0) / 60)} мин · +{Math.round(readNumber(option, 'bonusSeconds', 0) / 60)} бонус · {readNumber(option, 'expiresAfterDays', 0)} дн.</span>
              </button>
            ))}
          </div>
          <div className="settings-form-grid settings-package-form">
            <label>Пакет<input value={packageName} disabled={!canManagePackages} onChange={(event) => setPackageName(event.currentTarget.value)} /></label>
            <label>Цена<input inputMode="decimal" value={packagePrice} disabled={!canManagePackages} onChange={(event) => setPackagePrice(event.currentTarget.value)} /></label>
            <label>Минуты<input inputMode="numeric" value={packageMinutes} disabled={!canManagePackages} onChange={(event) => setPackageMinutes(event.currentTarget.value)} /></label>
            <label>Бонус<input inputMode="numeric" value={packageBonusMinutes} disabled={!canManagePackages} onChange={(event) => setPackageBonusMinutes(event.currentTarget.value)} /></label>
            <label>Дней<input inputMode="numeric" value={packageExpiresDays} disabled={!canManagePackages} onChange={(event) => setPackageExpiresDays(event.currentTarget.value)} /></label>
          </div>
        </>
      );
    }

    if (selectedSection === 'Персонал') {
      return (
        <>
          <div className="settings-section-title">
            <span>Сотрудники</span>
            <button type="button" disabled={!canManageBranchStaff} onClick={() => runSettingsAction('Пригласить сотрудника')}>Создать сотрудника</button>
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
                    setStaffRoleName(roleName ?? 'cashier_operator');
                    triggerFeedback(setFeedback, readString(user, 'displayName', 'Staff'), 'confirmed');
                  }}
                >
                  <strong>{readString(user, 'displayName', 'Staff')}</strong>
                  <span>{readString(user, 'userName', 'user')} · {readArray<string>(user, 'roleNames').join(', ') || 'roles'}</span>
                </button>
              ))}
            </div>
            <div className="settings-form-grid settings-staff-form">
              <label>Логин<input value={inviteUserName} disabled={!canManageBranchStaff} onChange={(event) => setInviteUserName(event.currentTarget.value)} /></label>
              <label>Имя<input value={inviteDisplayName} disabled={!canManageBranchStaff} onChange={(event) => setInviteDisplayName(event.currentTarget.value)} /></label>
              <label>Временный пароль<input type="password" value={invitePassword} disabled={!canManageBranchStaff} onChange={(event) => setInvitePassword(event.currentTarget.value)} /></label>
              <label>Роль
                <select value={inviteRoleName} disabled={!canManageBranchStaff} onChange={(event) => setInviteRoleName(event.currentTarget.value)}>
                  {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{roleName}</option>)}
                </select>
              </label>
              <label>Роль сотрудника
                <select value={staffRoleName} disabled={!canManageRoles || !selectedStaffUserId} onChange={(event) => setStaffRoleName(event.currentTarget.value)}>
                  {staffRoleOptions.map((roleName) => <option key={roleName} value={roleName}>{roleName}</option>)}
                </select>
              </label>
              <button type="button" disabled={!canManageRoles || !selectedStaffUserId} onClick={() => runSettingsAction('Обновить роль')}>Обновить роль</button>
            </div>
          </div>
        </>
      );
    }

    if (selectedSection === 'POS и склад') {
      return (
        <>
          <div className="settings-section-title">
            <span>POS каталог</span>
            <div className="settings-section-actions">
              <button type="button" disabled={!canManagePosCatalog} onClick={() => runSettingsAction('Создать товар')}>Создать товар</button>
              <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runSettingsAction('Обновить товар')}>Обновить товар</button>
              <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runSettingsAction('Снять с продажи')}>Снять с продажи</button>
            </div>
          </div>
          <div className="settings-config-grid">
            {catalog.slice(0, 8).map((product) => (
              <button key={readString(product, 'productId')} type="button" className={readString(product, 'productId') === selectedProductId ? 'active' : undefined} onClick={() => selectCatalogProduct(product)}>
                <strong>{readString(product, 'name', 'Product')}</strong>
                <span>{formatMoney(readMoney(product, 'price'), currencyCode)} · stock {readNumber(product, 'stockOnHand', 0)}</span>
              </button>
            ))}
          </div>
          <div className="settings-form-grid settings-pos-form">
            <label>Категория<input value={productCategoryName} disabled={!canManagePosCatalog} onChange={(event) => setProductCategoryName(event.currentTarget.value)} /></label>
            <label>Товар<input value={productName} disabled={!canManagePosCatalog} onChange={(event) => setProductName(event.currentTarget.value)} /></label>
            <label>SKU<input value={productSku} disabled={!canManagePosCatalog} onChange={(event) => setProductSku(event.currentTarget.value)} /></label>
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
                  <option key={readString(product, 'productId')} value={readString(product, 'productId')}>{readString(product, 'name', 'Product')} · stock {readNumber(product, 'stockOnHand', 0)}</option>
                ))}
              </select>
            </label>
            <label>Тип
              <select value={stockMovementType} disabled={!canManageInventoryStock} onChange={(event) => setStockMovementType(event.currentTarget.value)}>
                <option value="purchase">purchase</option>
                <option value="adjustment">adjustment</option>
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
                <span>backend вернул пустую историю</span>
              </button>
            )}
            {stockMovements.map((movement) => {
              const productId = readString(movement, 'productId');
              const productName = readString(
                catalog.find((product) => readString(product, 'productId') === productId),
                'name',
                productId.slice(0, 8) || 'Product');
              const quantityDelta = readNumber(movement, 'quantityDelta', 0);
              const reason = readString(movement, 'reason', 'movement');
              return (
                <button key={readString(movement, 'stockMovementId')} type="button" onClick={() => triggerFeedback(setFeedback, productName, 'confirmed')}>
                  <strong>{productName} · {readString(movement, 'movementType', 'movement')}</strong>
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
              ['Платежи', 'manual provider'],
              ['Обновления', `${rollouts.length} rollout`],
              ['Ошибки обновлений', `${readNumber(updateSummary, 'failedDevices', 0)} devices`],
              ['API', backend?.config.platformBaseUrl ?? 'dev demo']
            ].map(([name, detail]) => (
              <button key={name} type="button" onClick={() => triggerFeedback(setFeedback, name, 'confirmed')}>
                <strong>{name}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>

          <div className="settings-section-title">
            <span>Update packages</span>
            <button type="button" disabled={!canManageUpdatePackages} onClick={() => runSettingsAction('Зарегистрировать пакет обновления')}>Зарегистрировать пакет</button>
          </div>
          <div className="settings-form-grid settings-update-form">
            <label>Компонент
              <select value={updateComponent} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateComponent(event.currentTarget.value)}>
                <option value="operator-app">operator-app</option>
                <option value="agent-service">agent-service</option>
                <option value="player-shell">player-shell</option>
              </select>
            </label>
            <label>Версия<input value={updateVersion} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateVersion(event.currentTarget.value)} /></label>
            <label>Канал
              <select value={updateChannel} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateChannel(event.currentTarget.value)}>
                <option value="internal">internal</option>
                <option value="beta">beta</option>
                <option value="stable">stable</option>
              </select>
            </label>
            <label className="settings-form-wide">Artifact URL<input value={updateArtifactUri} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateArtifactUri(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">SHA-256<input value={updateSha256} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSha256(event.currentTarget.value)} /></label>
            <label>Signature<input value={updateSignature} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSignature(event.currentTarget.value)} /></label>
            <label>Алгоритм<input value={updateSignatureAlgorithm} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSignatureAlgorithm(event.currentTarget.value)} /></label>
            <label>Размер bytes<input inputMode="numeric" value={updateSizeBytes} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateSizeBytes(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Release notes<input value={updateReleaseNotes} disabled={!canManageUpdatePackages} onChange={(event) => setUpdateReleaseNotes(event.currentTarget.value)} /></label>
          </div>

          <div className="settings-section-title">
            <span>Rollouts</span>
            <button type="button" disabled={!canManageUpdateRollouts} onClick={() => runSettingsAction('Создать rollout обновления')}>Создать rollout</button>
          </div>
          <div className="settings-tariff-list">
            {rollouts.map((rollout) => (
              <button
                key={readString(rollout, 'updateRolloutId')}
                type="button"
                className="settings-tariff-row"
                onClick={() => {
                  setRolloutStateRolloutId(readString(rollout, 'updateRolloutId'));
                  setRolloutPackageId(readString(rollout, 'updatePackageId'));
                  setPackageStatePackageId(readString(rollout, 'updatePackageId'));
                }}
              >
                <strong>{readString(rollout, 'component', 'component')} {readString(rollout, 'version', 'version')}</strong>
                <b>{readString(rollout, 'state', 'state')}</b>
                <span>{readString(rollout, 'targetKind', 'target')} · {readNumber(rollout, 'batchPercent', 0)}% · {readArray(rollout, 'deviceStatuses').length} devices</span>
              </button>
            ))}
          </div>
          <div className="settings-form-grid settings-update-form">
            <label className="settings-form-wide">Rollout package<input value={rolloutPackageId} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutPackageId(event.currentTarget.value)} /></label>
            <label>Канал
              <select value={rolloutChannel} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutChannel(event.currentTarget.value)}>
                <option value="internal">internal</option>
                <option value="beta">beta</option>
                <option value="stable">stable</option>
              </select>
            </label>
            <label>Target
              <select value={rolloutTargetKind} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutTargetKind(event.currentTarget.value)}>
                <option value="branch">branch</option>
                <option value="device">device</option>
              </select>
            </label>
            <label>Batch %<input inputMode="numeric" value={rolloutBatchPercent} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutBatchPercent(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Target device ids<input value={rolloutTargetDeviceIds} disabled={!canManageUpdateRollouts || rolloutTargetKind !== 'device'} onChange={(event) => setRolloutTargetDeviceIds(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Start UTC<input value={rolloutStartsAtUtc} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStartsAtUtc(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Причина rollout<input value={rolloutReason} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutReason(event.currentTarget.value)} /></label>
          </div>

          <div className="settings-section-title">
            <span>Состояния обновлений</span>
            <button type="button" disabled={!canManageUpdatePackages} onClick={() => runSettingsAction('Изменить состояние пакета')}>Изменить состояние пакета</button>
            <button type="button" disabled={!canManageUpdateRollouts} onClick={() => runSettingsAction('Изменить состояние rollout')}>Изменить состояние rollout</button>
          </div>
          <div className="settings-form-grid settings-update-form">
            <label className="settings-form-wide">Package id<input value={packageStatePackageId} disabled={!canManageUpdatePackages} onChange={(event) => setPackageStatePackageId(event.currentTarget.value)} /></label>
            <label>Состояние пакета
              <select value={packageState} disabled={!canManageUpdatePackages} onChange={(event) => setPackageState(event.currentTarget.value)}>
                <option value="registered">registered</option>
                <option value="validated">validated</option>
                <option value="rejected">rejected</option>
                <option value="retired">retired</option>
              </select>
            </label>
            <label>Причина пакета<input value={packageStateReason} disabled={!canManageUpdatePackages} onChange={(event) => setPackageStateReason(event.currentTarget.value)} /></label>
            <label className="settings-form-wide">Rollout id<input value={rolloutStateRolloutId} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStateRolloutId(event.currentTarget.value)} /></label>
            <label>Состояние rollout
              <select value={rolloutState} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutState(event.currentTarget.value)}>
                <option value="active">active</option>
                <option value="paused">paused</option>
                <option value="completed">completed</option>
                <option value="rollback_requested">rollback_requested</option>
                <option value="rolled_back">rolled_back</option>
                <option value="cancelled">cancelled</option>
              </select>
            </label>
            <label>Причина rollout<input value={rolloutStateReason} disabled={!canManageUpdateRollouts} onChange={(event) => setRolloutStateReason(event.currentTarget.value)} /></label>
          </div>
        </>
      );
    }

    return (
      <>
        <div className="settings-form-grid">
          <label>Название клуба<input value={clubName} onChange={(event) => { setClubName(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>Город<input value={city} onChange={(event) => { setCity(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>Валюта<input value={currencyCode} readOnly /></label>
          <label>Филиал<input value={backend?.branchId ?? 'dev demo'} readOnly /></label>
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
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Backend settings')}</span>
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
              <strong>backend setup snapshot</strong>
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
  const [organizationId, setOrganizationId] = useState(config.organizationId ?? fallbackOrganizationId);
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

    if (!isGuid(organizationId)) {
      setError('OrganizationId должен быть корректным GUID.');
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
      <header className="top-command auth-top-command" onMouseDown={handleWindowDragStart}>
        <div className="brand-block">
          <strong>AFK4</strong>
          <span>Operator</span>
        </div>
        <div className="top-status">
          <span><Wifi size={14} />{isChecking ? 'Проверяем защищённый вход' : 'Защищённый вход'}</span>
          <span>{config.platformBaseUrl}</span>
          <span>{config.shellMode}</span>
        </div>
        <WindowControls />
      </header>

      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>AFK4 Operator</span>
            <h1>Вход оператора</h1>
            <p>Токены сохраняются только через нативное защищённое хранилище Windows.</p>
          </header>

          <form className="auth-form" onSubmit={submit}>
            <label>
              Организация
              <input
                value={organizationId}
                onChange={(event) => setOrganizationId(event.currentTarget.value)}
                autoComplete="off"
                spellCheck={false}
              />
            </label>
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
            <strong>Windows Protected Data</strong>
          </section>
        </aside>
      </main>
    </div>
  );
}

export function App() {
  const config = getOperatorConfig();
  const [workspace, setWorkspace] = useState<WorkspaceId>('map');
  const [selectedSeatId, setSelectedSeatId] = useState(seats[0].id);
  const [authStatus, setAuthStatus] = useState<AuthStatus>('checking');
  const [authSession, setAuthSession] = useState<OperatorAuthSession | null>(null);
  const [authError, setAuthError] = useState<string | null>(null);
  const [floorMap, setFloorMap] = useState<OperatorFloorMapState>(() => createFixtureFloorMapState());
  const [realtimeState, setRealtimeState] = useState<OperatorRealtimeConnectionState>('disconnected');
  const [realtimeError, setRealtimeError] = useState<string | null>(null);
  const selectedSeat = floorMap.seats.find((seat) => seat.id === selectedSeatId) ?? floorMap.seats[0] ?? null;
  const activeBranchId = authSession === null ? null : resolveActiveBranchId(authSession, config.branchId);
  const backendContext: OperatorBackendContext | null = authSession !== null && activeBranchId !== null
    ? { config, session: authSession, branchId: activeBranchId }
    : null;
  const canUseTechMode = hasPermission(authSession, permissionNames.viewDiagnostics)
    && hasPermission(authSession, permissionNames.viewDeviceDetail);

  useEffect(() => {
    let disposed = false;

    loadOperatorSession()
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
        error: 'Active branch is not assigned.'
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

    loadBackendFloorMapState(config, authSession, branchId)
      .then((nextState) => {
        if (disposed) {
          return;
        }

        setFloorMap(nextState);
        setSelectedSeatId(nextState.seats[0]?.id ?? seats[0].id);
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setFloorMap((current) => ({
          ...current,
          branchId,
          loadStatus: 'failed',
          error: projectOperatorError(error).detail
        }));
      });

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

        setFloorMap((current) => ({
          ...current,
          seats: applyDeviceStatusToSeats(current.seats, status)
        }));
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
      void realtimeClient.stop();
    };
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

  const handleSignIn = async (request: OperatorSignInRequest) => {
    const session = await signInOperator(request);
    setAuthSession(session);
    setAuthStatus('signed-in');
    setAuthError(null);
  };

  const handleSignOut = async () => {
    try {
      await signOutOperator();
      setAuthError(null);
    } catch (error) {
      setAuthError(projectAuthHostError(error, config));
    } finally {
      setAuthSession(null);
      setAuthStatus('signed-out');
    }
  };

  const handleSeatAction = async (request: SeatActionRequest): Promise<SeatActionResult> => {
    const session = authSession;
    if (session === null) {
      throw new Error('Operator is not signed in.');
    }

    const branchId = resolveActiveBranchId(session, config.branchId);
    if (!branchId) {
      throw new Error('Active branch is not assigned.');
    }

    if (floorMap.source !== 'backend') {
      throw new Error('Backend floor map is not loaded.');
    }

    const clients = createAuthenticatedOperatorClients(config, session);
    let response: unknown;
    if (request.type === 'start') {
      if (!hasPermission(session, permissionNames.startSession)) {
        throw new Error('Operator is not allowed to start sessions.');
      }

      if (request.seat.tone !== 'ready' || request.seat.activeSessionId) {
        throw new Error('Seat is not ready for session start.');
      }

      const billing = request.billing;
      response = await clients.sessions.startGuestSession(branchId, {
        organizationId: session.organizationId,
        seatId: request.seat.id,
        durationMinutes: defaultSessionDurationMinutes,
        tariffRuleVersionId: billing.tariffRuleVersionId,
        idempotencyKey: createIdempotencyKey('session-start'),
        playerAccountId: billing.playerAccountId ?? null,
        billingMode: billing.mode === 'guest' ? '' : billing.mode,
        tariffVersionId: billing.tariffVersionId ?? null,
        playerPackageId: billing.playerPackageId ?? null
      });
    } else if (request.type === 'extend') {
      if (!hasPermission(session, permissionNames.extendSession)) {
        throw new Error('Operator is not allowed to extend sessions.');
      }

      if (!request.seat.activeSessionId) {
        throw new Error('Selected seat has no active session.');
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
        throw new Error('Operator is not allowed to transfer sessions.');
      }

      if (!request.seat.activeSessionId) {
        throw new Error('Selected seat has no active session.');
      }

      response = await clients.sessions.transferSession(request.seat.activeSessionId, {
        targetSeatId: request.targetSeatId,
        idempotencyKey: createIdempotencyKey('session-transfer')
      });
    } else {
      if (!hasPermission(session, permissionNames.endSession)) {
        throw new Error('Operator is not allowed to end sessions.');
      }

      if (!request.seat.activeSessionId) {
        throw new Error('Selected seat has no active session.');
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

  const handleTechMode = async (seat: SeatSummary): Promise<string> => {
    const nextBackend = requireBackend(backendContext);
    if (!hasPermission(nextBackend.session, permissionNames.viewDiagnostics) ||
      !hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
      throw new Error('Нет прав на диагностику устройства.');
    }

    if (!seat.deviceId) {
      throw new Error('У выбранного ПК нет привязанного устройства.');
    }

    const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
    const [device, diagnostics] = await Promise.all([
      clients.devices.getDeviceDetail(seat.deviceId),
      clients.diagnostics.getDiagnostics(nextBackend.branchId)
    ]);

    return describeTechModeResult(seat, device, diagnostics);
  };

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
      <header className="top-command" onMouseDown={handleWindowDragStart}>
        <div className="brand-block">
          <strong>AFK4</strong>
          <span>Operator</span>
        </div>
        <label className="command-search">
          <Search size={16} />
          <input placeholder="Игрок, ПК, команда" aria-label="Поиск" />
        </label>
        <div className="top-status">
          <span><Wifi size={14} />{realtimeLabel(realtimeState, realtimeError)}</span>
          <span>Смена #24 · открыта</span>
          <span>{authSession.displayName} · {config.shellMode}</span>
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
              className={workspace === id ? 'active' : ''}
              disabled={!isAllowed}
              title={item.label}
              onClick={() => {
                if (isAllowed) {
                  setWorkspace(id);
                }
              }}
            >
              <Icon size={22} />
              <span>{item.label}</span>
            </button>
          );
        })}
      </nav>

      {workspace === 'map' && (
        <MapWorkspace
          currencyCode={config.currencyCode}
          floorMap={floorMap}
          canUseTechMode={canUseTechMode}
          selectedSeatId={selectedSeat?.id ?? ''}
          onSelectSeat={setSelectedSeatId}
          onTechMode={handleTechMode}
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
          floorMap={floorMap}
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
      {workspace === 'logs' && <BackendLogsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'settings' && <BackendSettingsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}

      {workspace === 'map' && selectedSeat !== null && (
        <MapSidePanel
          seat={selectedSeat}
          seats={floorMap.seats}
          currencyCode={config.currencyCode}
          backend={backendContext}
          actionsEnabled={floorMap.source === 'backend' && floorMap.loadStatus === 'ready'}
          onSeatAction={handleSeatAction}
        />
      )}
      {workspace !== 'map' && workspace !== 'dashboard' && workspace !== 'booking' && workspace !== 'pos' && workspace !== 'players' && workspace !== 'payments' && workspace !== 'logs' && workspace !== 'settings'
        && <SummarySidePanel workspace={workspace} currencyCode={config.currencyCode} />}

      <footer className="signals-strip">
        <span><Wifi size={14} />{realtimeLabel(realtimeState, realtimeError)} · {dataSourceLabel(floorMap.source)}</span>
        <span><MonitorCheck size={14} />Devices: {countByTone(floorMap.seats, 'offline')} offline, {countProblems(floorMap.seats)} attention</span>
        <span><CircleDollarSign size={14} />POS: 2 неоплаченных чека</span>
        <span><LockKeyhole size={14} />Критичные действия ждут подтверждения платформы</span>
      </footer>
    </div>
  );
}
