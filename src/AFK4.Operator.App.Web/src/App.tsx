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
import { useEffect, useState, type CSSProperties, type FormEvent, type MouseEvent, type ReactNode } from 'react';
import { projectOperatorError } from './apiErrors';
import { loadOperatorSession, signInOperator, signOutOperator, type OperatorAuthSession, type OperatorSignInRequest } from './authClient';
import {
  applyDeviceStatusToSeats,
  createFixtureFloorMapState,
  mapFloorMapDtoToState,
  type FloorMapLoadStatus,
  type OperatorFloorMapState
} from './floorMapState';
import { postHostWindowCommand } from './hostBridge';
import {
  createOperatorApiClients,
  type AuditSearchResultDto,
  type BranchDiagnosticsDto,
  type CashMovementDto,
  type OperatorDashboardSummaryDto,
  type PosProductDto,
  type PosSaleDto,
  type ReservationSearchResultDto,
  type ReportResultDto,
  type ShiftDto,
  type StaffUserDto,
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
type OperatorConfig = ReturnType<typeof getOperatorConfig>;
type OperatorBackendContext = {
  config: OperatorConfig;
  session: OperatorAuthSession;
  branchId: string;
};
type SeatActionRequest =
  | { type: 'start'; seat: SeatSummary }
  | { type: 'extend'; seat: SeatSummary; minutes: number }
  | { type: 'transfer'; seat: SeatSummary; targetSeatId: string }
  | { type: 'end'; seat: SeatSummary };

const workspaceIds: WorkspaceId[] = ['map', 'dashboard', 'booking', 'pos', 'players', 'payments', 'logs', 'settings'];
const fallbackOrganizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';
const defaultSessionDurationMinutes = 60;
const defaultTariffRuleVersionId = 'manual-v1';
const permissionNames = {
  viewFloorMap: 'floor_map.view',
  startSession: 'sessions.start',
  extendSession: 'sessions.extend',
  transferSession: 'sessions.transfer',
  endSession: 'sessions.end',
  viewPlayers: 'players.view',
  viewBilling: 'billing.view',
  viewShift: 'shifts.view',
  viewReports: 'reports.view',
  viewReservations: 'reservations.view',
  manageReservations: 'reservations.manage',
  createPosSale: 'pos.sales.create',
  payPosSale: 'pos.sales.pay',
  viewInventory: 'inventory.view',
  viewReceipt: 'receipts.view',
  viewDiagnostics: 'diagnostics.view',
  manageBranchStaff: 'identity.branch_staff.manage',
  manageLayout: 'layout.manage',
  viewTariffs: 'tariffs.view',
  viewUpdateStatus: 'updates.status.view',
  viewAudit: 'audit.view'
} as const;

const workspacePermissionRules: Record<WorkspaceId, readonly string[]> = {
  map: [permissionNames.viewFloorMap],
  dashboard: [permissionNames.viewReports],
  booking: [permissionNames.viewReservations],
  pos: [
    permissionNames.viewInventory,
    permissionNames.createPosSale,
    permissionNames.payPosSale,
    permissionNames.viewShift,
    permissionNames.viewReports
  ],
  players: [permissionNames.viewPlayers, permissionNames.viewBilling],
  payments: [permissionNames.viewShift, permissionNames.viewReports],
  logs: [permissionNames.viewAudit, permissionNames.viewDiagnostics],
  settings: [
    permissionNames.manageBranchStaff,
    permissionNames.manageLayout,
    permissionNames.viewInventory,
    permissionNames.viewDiagnostics,
    permissionNames.viewUpdateStatus,
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

function dateTimeInputToIso(value: string) {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? new Date().toISOString() : date.toISOString();
}

function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
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
  detail = 'Функция пока не подключена к backend.'
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
    return error ? `Fixture · ${error}` : 'Fixture · API недоступен';
  }

  return source === 'backend' ? 'Backend live' : 'Fixture';
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

function moneyDto(currencyCode: string, majorUnits: number) {
  return {
    currencyCode,
    minorUnits: Math.round(majorUnits * 100)
  };
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

function MapWorkspace({
  currencyCode,
  floorMap,
  canUseTechMode,
  selectedSeatId,
  onSelectSeat
}: {
  currencyCode: string;
  floorMap: OperatorFloorMapState;
  canUseTechMode: boolean;
  selectedSeatId: string;
  onSelectSeat: (seatId: string) => void;
}) {
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const activeCount = countByTone(floorMap.seats, 'active');
  const readyCount = countByTone(floorMap.seats, 'ready');
  const pendingCount = countByTone(floorMap.seats, 'pending');
  const offlineCount = countByTone(floorMap.seats, 'offline');
  const problemCount = countProblems(floorMap.seats);
  const loadLabel = floorMapLoadLabel(floorMap.loadStatus, floorMap.source, floorMap.error);

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
            disabled={!canUseTechMode}
            onClick={() => triggerFeedback(setFeedback, 'Техрежим')}
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
      <FeedbackNotice feedback={feedback} />

      <section className="map-board" aria-label="ПК зала">
        <div className="seat-grid">
          {floorMap.seats.map((seat) => (
            <SeatTile
              key={seat.id}
              seat={seat}
              selected={seat.id === selectedSeatId}
              onSelect={() => onSelectSeat(seat.id)}
            />
          ))}
        </div>
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
      await Promise.all([
        clients.dashboard.getSummary(nextBackend.branchId, dashboardRangeQuery(activeRange.from, activeRange.to)),
        clients.shifts.exportSalesReportCsv(nextBackend.branchId, dashboardRangeQuery(activeRange.from, activeRange.to))
      ]);
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
  session,
  actionsEnabled,
  onSeatAction
}: {
  seat: SeatSummary;
  seats: SeatSummary[];
  currencyCode: string;
  session: OperatorAuthSession | null;
  actionsEnabled: boolean;
  onSeatAction: (request: SeatActionRequest) => Promise<void>;
}) {
  const status = mapSeatStatus(seat);
  const activeBilling = billingLabel(seat.billing);
  const billingModes = ['Гость', 'Депозит', 'Пакет', 'Постоплата'];
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const transferCandidates = floorSeats.filter((candidate) =>
    candidate.id !== seat.id &&
    candidate.tone === 'ready' &&
    !candidate.activeSessionId);
  const [targetSeatId, setTargetSeatId] = useState(transferCandidates[0]?.id ?? '');
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
  const canStartSession = actionsEnabled && canStartPermission && !hasActionableSession && seat.tone === 'ready';
  const canExtendSession = actionsEnabled && canExtendPermission && hasActionableSession;
  const canEndSession = actionsEnabled && canEndPermission && hasActionableSession;
  const canTransferSession = actionsEnabled && canTransferPermission && hasActionableSession && targetSeatId.length > 0;
  const confirmationText = !actionsEnabled
    ? 'Backend карта недоступна'
    : !hasAnySessionActionPermission
      ? 'Нет прав на действия с сессией'
      : feedback.state === 'idle'
        ? 'Ждём платформу'
        : feedbackText(feedback);

  useEffect(() => {
    if (targetSeatId.length > 0 && transferCandidates.some((candidate) => candidate.id === targetSeatId)) {
      return;
    }

    setTargetSeatId(transferCandidates[0]?.id ?? '');
  }, [seat.id, floorSeats]);

  const runSeatAction = async (label: string, request: SeatActionRequest) => {
    setFeedback({ label, state: 'pending' });

    try {
      await onSeatAction(request);
      setFeedback({ label, state: 'confirmed' });
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
            <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction('+15 мин', { type: 'extend', seat, minutes: 15 })}><Plus size={15} />15 мин</button>
            <button type="button" disabled={!canExtendSession || isBusy} onClick={() => runSeatAction('+30 мин', { type: 'extend', seat, minutes: 30 })}><TimerReset size={15} />30 мин</button>
            <button type="button" disabled={!canTransferSession || isBusy} onClick={() => runSeatAction('Перенос', { type: 'transfer', seat, targetSeatId })}><ArrowRightLeft size={15} />Перенос</button>
            <button type="button" className="danger" disabled={!canEndSession || isBusy} onClick={() => runSeatAction('Стоп', { type: 'end', seat })}><Square size={15} />Стоп</button>
          </>
        ) : (
          <>
            <button type="button" className="start-action" disabled={!canStartSession || isBusy} onClick={() => runSeatAction('Старт 60 мин', { type: 'start', seat })}><Plus size={15} />Старт 60 мин</button>
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

      <section className="billing-mode" aria-label="Режим биллинга">
        {billingModes.map((mode) => (
          <button key={mode} type="button" className={mode === activeBilling ? 'active' : undefined}>
            {mode}
          </button>
        ))}
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
        <div className="detail-row"><span>Source</span><strong>SmartShell-like fixture</strong></div>
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
  { name: 'Cola 0.5', priceMinorUnits: 1200, category: 'Напитки', note: 'fixture', stockOnHand: 0, source: 'fixture' },
  { name: 'Вода 0.5', priceMinorUnits: 600, category: 'Напитки', note: 'fixture', stockOnHand: 0, source: 'fixture' },
  { name: 'Хот-дог', priceMinorUnits: 2800, category: 'Еда', note: 'fixture', stockOnHand: 0, source: 'fixture' },
  { name: 'Гостевой час', priceMinorUnits: 2500, category: 'Услуги', note: 'fixture', stockOnHand: 0, source: 'fixture' }
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

      setCatalog(products.length > 0 ? products : fixturePosProducts);
      setCurrentShift(nextShift);
      setSalesReport(nextSalesReport);
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

  const categories = ['Все', ...Array.from(new Set(catalog.map((product) => product.category))).slice(0, 5)];
  const visibleProducts = catalog.filter((product) => {
    const categoryMatches = activeCategory === 'Все' || product.category === activeCategory;
    const searchMatches = `${product.name} ${product.category} ${product.note}`.toLowerCase().includes(productSearch.trim().toLowerCase());
    return categoryMatches && searchMatches;
  });
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

  const acceptPayment = async () => {
    setFeedback({ label: 'Оплата', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
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
        idempotencyKey: createIdempotencyKey('pos-sale')
      });
      const saleId = readString(sale, 'posSaleId');
      if (!saleId) {
        throw new Error('Platform API returned a POS sale without sale id.');
      }

      const paidSale = await clients.pos.paySaleManual(saleId, {
        organizationId: nextBackend.session.organizationId,
        paymentMethod: paymentMethod === 'Карта' ? 'card' : paymentMethod === 'Депозит' ? 'wallet' : 'cash',
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

  return (
    <main className="workspace-screen pos-screen">
      <section className="screen-head pos-head">
        <div>
          <span>POS</span>
          <h1>POS · продажа и кассовые операции</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{loadStatus === 'backend' ? 'Backend live' : loadStatus === 'loading' ? 'Loading backend' : loadStatus === 'failed' ? 'Fixture fallback' : 'Fixture'}</span>
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
              <strong>Гость · без карты</strong>
            </div>
            <button type="button" onClick={() => triggerFeedback(setFeedback, 'Выбрать клиента')}>Выбрать</button>
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
              <article key={readString(row, 'posSaleId')} className="pos-receipt-row">
                <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                <strong>{readString(row, 'state', 'sale')}</strong>
                <em>{readNumber(row, 'lineCount', 0)} lines</em>
                <b>{formatMoney(readMoney(row, 'total'), currencyCode)}</b>
              </article>
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
        </section>

        <section className="pos-panel pos-quick-panel">
          <header className="pos-panel-title">
            <span>Быстрые операции</span>
            <strong>actions require backend confirmation</strong>
          </header>
          <div className="pos-quick-grid">
            {[
              ['Пополнить депозит', 'откройте экран клиентов', CircleDollarSign],
              ['Возврат по чеку', 'требует выбранный backend sale', ReceiptText],
              ['Новый клиент', 'экран клиентов', UserRoundPlus],
              ['Внести наличные', 'экран платежей', Banknote]
            ].map(([label, detail, Icon]) => (
              <button key={label as string} type="button" className="pos-quick-card" onClick={() => triggerFeedback(setFeedback, label as string)}>
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
  const [draftStartsAt, setDraftStartsAt] = useState(() => toDateTimeInputValue(addDays(new Date(), 0)));
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
    source: 'operator'
  };
  const onlineRequests = bookings.filter((booking) => booking.source === 'online' && booking.status === 'Ожидает');
  const selectedReadySeat = readySeats.find((seat) => seat.id === selectedBooking.seatId) ?? readySeats[0] ?? null;
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

    return await clients.reservations.create(nextBackend.branchId, {
      organizationId: nextBackend.session.organizationId,
      seatId: selectedReadySeat.id,
      customerName: draftCustomerName.trim() || 'Гость',
      phoneNumber: draftPhoneNumber.trim() || null,
      startsAtUtc: dateTimeInputToIso(draftStartsAt),
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
          <button type="button" className="booking-create-action" onClick={createReservation}><Plus size={14} />Создать</button>
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
            <button type="button" onClick={() => selectedBooking.seatId ? onOpenSeat(selectedBooking.seatId) : setFeedback({ label: 'Открыть карту', state: 'failed', detail: 'У выбранной брони нет места.' })}><MonitorCheck size={15} />Открыть карту</button>
            <button type="button" onClick={seatReservation}><UserRoundPlus size={15} />Посадить</button>
            <button type="button" onClick={moveReservation}><ArrowRightLeft size={15} />Перенести</button>
            <button type="button" className="danger" onClick={cancelReservation}><Square size={15} />Отменить</button>
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
                  <button type="button" onClick={() => confirmReservation(request.reservationId, `Принять ${request.client}`)}>Принять</button>
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
            <label>Клиент<input value={draftCustomerName} onChange={(event) => setDraftCustomerName(event.target.value)} /></label>
            <label>Телефон<input value={draftPhoneNumber} onChange={(event) => setDraftPhoneNumber(event.target.value)} /></label>
            <label>Старт<input type="datetime-local" value={draftStartsAt} onChange={(event) => setDraftStartsAt(event.target.value)} /></label>
            <label>Длительность<input type="number" min={15} step={15} value={draftDurationMinutes} onChange={(event) => setDraftDurationMinutes(Number(event.target.value) || 60)} /></label>
          </div>
          <button type="button" className="booking-primary-action" onClick={createReservation}>Создать бронь</button>
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
    { name: 'Madina S.', status: 'VIP', balanceMinorUnits: 46000, debtMinorUnits: 0, last: 'fixture', tone: 'vip', detail: 'fixture client', phoneNumber: '+992 90 555 22 11', source: 'fixture' },
    { name: 'Amir K.', status: 'Активен', balanceMinorUnits: 12000, debtMinorUnits: 0, last: 'fixture', tone: 'active', detail: `120 ${currencyCode}`, phoneNumber: '', source: 'fixture' },
    { name: 'Olim K.', status: 'Долг', balanceMinorUnits: 0, debtMinorUnits: 3500, last: 'fixture', tone: 'debt', detail: 'postpaid debt', phoneNumber: '', source: 'fixture' }
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
        if (disposed) {
          return;
        }

        const nextClients = Array.isArray(players) ? players.map(projectPlayerClient) : [];
        setClients(nextClients.length > 0 ? nextClients : []);
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
      return undefined;
    }

    let disposed = false;
    const loadWallet = async () => {
      try {
        const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
        const wallet = await apiClients.players.getWalletSummary(selectedClient.playerAccountId!);
        if (!disposed) {
          setWalletSummary(wallet);
        }
      } catch (error) {
        if (!disposed) {
          setFeedback({ label: selectedClient.name, state: 'failed', detail: projectOperatorError(error).detail });
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

  const runClientAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);

      if (label === 'Пополнить депозит') {
        if (!selectedClient.playerAccountId) {
          throw new Error('Select a backend player before wallet top-up.');
        }

        const wallet = await apiClients.players.topUpWallet(selectedClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          amount: moneyDto(currencyCode, 100),
          reason: 'operator quick top-up',
          idempotencyKey: createIdempotencyKey('wallet-top-up')
        });
        setWalletSummary(wallet);
      } else if (label === 'Списать долг') {
        if (!selectedClient.playerAccountId) {
          throw new Error('Select a backend player before debt payment.');
        }

        const wallet = await apiClients.players.payDebt(selectedClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          amount: {
            currencyCode,
            minorUnits: Math.max(debt, 1000)
          },
          reason: 'operator debt payment',
          idempotencyKey: createIdempotencyKey('debt-payment')
        });
        setWalletSummary(wallet);
      } else if (label === 'Новая карта') {
        const created = await apiClients.players.createPlayer(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          displayName: clientSearch.trim() || `Новый клиент ${new Date().toLocaleTimeString('ru-RU', { hour: '2-digit', minute: '2-digit' })}`,
          phoneNumber: null,
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
      } else if (label === 'Создать бронь') {
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
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{loadStatus === 'backend' ? 'Backend live' : loadStatus === 'loading' ? 'Loading backend' : loadStatus === 'failed' ? 'Fixture fallback' : 'Fixture'}</span>
        </div>
      </section>

      <section className="state-strip clients-state-strip" aria-label="Сводка клиентов">
        <StateFlag label="Клиенты" value={String(clients.length)} />
        <StateFlag label="Backend" value={String(clients.filter((client) => client.source === 'backend').length)} critical={loadStatus !== 'backend'} />
        <StateFlag label="Депозит" value={formatMinorUnits(balance, currencyCode)} />
        <StateFlag label="Долг" value={formatMinorUnits(debt, currencyCode)} critical={debt > 0} />
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
              <em>{selectedClient.phoneNumber || 'без телефона'} · {selectedClient.source}</em>
            </div>
          </div>
          <div className="client-metrics-grid">
            <div><span>Депозит</span><strong>{formatMinorUnits(balance, currencyCode)}</strong></div>
            <div><span>Долг</span><strong>{formatMinorUnits(debt, currencyCode)}</strong></div>
            <div><span>Пакеты</span><strong>{selectedClient.detail.includes('пакетов') ? selectedClient.detail.split(' · ')[1] : '0'}</strong></div>
            <div><span>Источник</span><strong>{selectedClient.source}</strong></div>
          </div>
        </section>

        <section className="clients-panel clients-actions-panel">
          <header className="clients-panel-title">
            <span>Операции</span>
            <strong>money actions wait for backend</strong>
          </header>
          <div className="clients-action-grid">
            {[
              ['Пополнить депозит', '100 к депозиту', CircleDollarSign],
              ['Списать долг', 'после оплаты', ReceiptText],
              ['Создать бронь', 'бронь из карточки', CalendarClock],
              ['Новая карта', 'создать игрока', UserRoundPlus]
            ].map(([label, detail, Icon]) => (
              <button key={label as string} type="button" className="clients-action-card" onClick={() => runClientAction(label as string)}>
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

type PaymentOperationItem = [string, string, string, string, string, string];

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

  const loadPayments = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      return;
    }

    setLoadStatus('loading');
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
      setLoadStatus('backend');
    } catch (error) {
      setLoadStatus('failed');
      setFeedback({ label: 'Платежи', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  useEffect(() => {
    void loadPayments();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  const salesRows = readArray(salesReport, 'rows');
  const cashRows = readArray(cashReport, 'rows');
  const shiftRows = readArray(shiftReport, 'rows');
  const operations: PaymentOperationItem[] = [
    ...salesRows.map((row): PaymentOperationItem => [
      formatTime(readString(row, 'createdAtUtc')),
      readString(row, 'state', 'POS sale'),
      readString(row, 'posSaleId').slice(0, 8),
      'POS',
      formatMoney(readMoney(row, 'total'), currencyCode),
      readString(row, 'state', 'sale').toLowerCase().includes('refund') ? 'refund' : 'sale'
    ]),
    ...cashRows.map((row): PaymentOperationItem => [
      formatTime(readString(row, 'createdAtUtc')),
      readString(row, 'operationType', 'Cash'),
      readString(row, 'reason', readString(row, 'sourceType', 'cash')),
      readString(row, 'sourceType', 'cash'),
      formatMoney(readMoney(row, 'cashImpact'), currencyCode),
      readNumber(readMoney(row, 'cashImpact'), 'minorUnits', 0) < 0 ? 'refund' : 'deposit'
    ])
  ];
  const fallbackOperations: PaymentOperationItem[] = [
    ['—', 'Нет backend операций', 'отчёты пустые', 'backend', `0 ${currencyCode}`, 'session']
  ];
  const visibleOperations = (operations.length > 0 ? operations : fallbackOperations).filter(([time, type, client, method, total]) => (
    `${time} ${type} ${client} ${method} ${total}`.toLowerCase().includes(paymentSearch.trim().toLowerCase())
  ));
  const selectedOperation = visibleOperations.find(([time, type, client]) => `${time}-${type}-${client}` === selectedOperationKey) ?? visibleOperations[0];
  const grossSales = readMoney(salesReport, 'grossSalesTotal');
  const refunds = readMoney(salesReport, 'refundsTotal');
  const netSales = readMoney(salesReport, 'netSalesTotal');
  const cashIn = readMoney(cashReport, 'cashInTotal');
  const cashOut = readMoney(cashReport, 'cashOutTotal');
  const latestShiftRow = shiftRows[0];
  const expectedCash = readMoney(currentShift, 'expectedCash') ?? readMoney(latestShiftRow, 'expectedCash');
  const countedCash = readMoney(currentShift, 'countedCash') ?? readMoney(latestShiftRow, 'countedCash');
  const difference = readMoney(currentShift, 'difference') ?? readMoney(latestShiftRow, 'difference');
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
      if (label === 'Журнал смены') {
        await apiClients.shifts.exportShiftReportCsv(nextBackend.branchId, { limit: 50 });
      } else if (label === 'Кассовый отчёт') {
        await apiClients.shifts.exportCashOperationReportCsv(nextBackend.branchId, { limit: 50 });
      } else if (label === 'Экспорт CSV') {
        await apiClients.shifts.exportSalesReportCsv(nextBackend.branchId, { limit: 50 });
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
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{loadStatus === 'backend' ? 'Backend reports' : loadStatus === 'loading' ? 'Loading backend' : loadStatus === 'failed' ? 'Fixture fallback' : 'Fixture'}</span>
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
          <div className="payments-reconcile-list">
            <div><span>Ожидается</span><strong>{expectedCash ? formatMinorUnits(expectedCash.minorUnits, expectedCash.currencyCode) : `0 ${currencyCode}`}</strong></div>
            <div><span>Посчитано</span><strong>{countedCash ? formatMinorUnits(countedCash.minorUnits, countedCash.currencyCode) : 'не закрыта'}</strong></div>
            <div className={(difference?.minorUnits ?? 0) !== 0 ? 'attention' : undefined}><span>Расхождение</span><strong>{difference ? formatMinorUnits(difference.minorUnits, difference.currencyCode) : `0 ${currencyCode}`}</strong></div>
          </div>
          <button type="button" className="payments-primary-action" onClick={() => runReportAction('Подготовить закрытие')}>Подготовить закрытие</button>
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

type LogEventItem = [string, string, string, string, string];

function BackendLogsWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [eventSearch, setEventSearch] = useState('');
  const [activeLogFilter, setActiveLogFilter] = useState('Все события');
  const [selectedEventKey, setSelectedEventKey] = useState('');
  const [selectedSource, setSelectedSource] = useState('Audit');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('fixture');
  const [auditResult, setAuditResult] = useState<AuditSearchResultDto | null>(null);
  const [diagnostics, setDiagnostics] = useState<BranchDiagnosticsDto | null>(null);

  const loadLogs = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      return;
    }

    setLoadStatus('loading');
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [audit, branchDiagnostics] = await Promise.all([
        apiClients.audit.search({ branchId: nextBackend.branchId, limit: 30 }),
        apiClients.diagnostics.getDiagnostics(nextBackend.branchId)
      ]);
      setAuditResult(audit);
      setDiagnostics(branchDiagnostics);
      setLoadStatus('backend');
    } catch (error) {
      setLoadStatus('failed');
      setFeedback({ label: 'Логи', state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  useEffect(() => {
    void loadLogs();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const auditRecords = readArray(auditResult, 'records');
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const updateSummary = isRecord(diagnostics) ? diagnostics.updateSummary : null;
  const deviceSummary = isRecord(diagnostics) ? diagnostics.deviceSummary : null;
  const recentCommandFailures = readArray(commandSummary, 'recentFailures');
  const recentUpdateFailures = readArray(updateSummary, 'recentFailures');
  const auditEvents: LogEventItem[] = auditRecords.map((record): LogEventItem => [
    formatTime(readString(record, 'createdAtUtc')),
    readString(record, 'action', 'audit'),
    `${readString(record, 'targetType', 'target')} · ${readString(record, 'outcome', 'unknown')}`,
    readString(record, 'sourceApp', 'Audit'),
    readString(record, 'outcome').toLowerCase().includes('denied') || readString(record, 'outcome').toLowerCase().includes('failed') ? 'warning' : 'audit'
  ]);
  const diagnosticEvents: LogEventItem[] = [
    ...recentCommandFailures.map((failure): LogEventItem => [
      formatTime(readString(failure, 'updatedAtUtc')),
      `${readString(failure, 'machineName', 'Device')} ${readString(failure, 'type', 'command')}`,
      readString(failure, 'message', readString(failure, 'status', 'failed')),
      'Agent',
      'device'
    ]),
    ...recentUpdateFailures.map((failure): LogEventItem => [
      formatTime(readString(failure, 'updatedAtUtc')),
      `${readString(failure, 'component', 'Update')} ${readString(failure, 'targetVersion', '')}`,
      readString(failure, 'message', readString(failure, 'status', 'failed')),
      'Updates',
      'warning'
    ])
  ];
  const events = [...diagnosticEvents, ...auditEvents];
  const fallbackEvents: LogEventItem[] = [
    ['—', 'Нет backend событий', 'audit/diagnostics пустые', 'Platform', 'audit']
  ];
  const visibleEvents = (events.length > 0 ? events : fallbackEvents).filter(([time, title, detail, source, tone]) => {
    const filterMatches = activeLogFilter === 'Все события'
      || (activeLogFilter === 'Только ошибки' && tone === 'warning')
      || (activeLogFilter === 'ПК и Agent' && (source === 'Agent' || tone === 'device'))
      || (activeLogFilter === 'Касса и POS' && (title.toLowerCase().includes('pos') || detail.toLowerCase().includes('cash')))
      || (activeLogFilter === 'Оператор' && source.toLowerCase().includes('operator'))
      || (activeLogFilter === 'Системные' && source !== 'Agent');
    const searchMatches = `${time} ${title} ${detail} ${source}`.toLowerCase().includes(eventSearch.trim().toLowerCase());
    return filterMatches && searchMatches;
  });
  const selectedEvent = visibleEvents.find(([time, title]) => `${time}-${title}` === selectedEventKey) ?? visibleEvents[0];
  const sourceCards: Array<[string, string, LucideIcon]> = [
    ['Agent', `${readNumber(deviceSummary, 'onlineDevices', 0)} онлайн · ${readNumber(deviceSummary, 'staleDevices', 0)} stale`, MonitorCheck],
    ['POS', `${auditRecords.filter((record) => readString(record, 'action').toLowerCase().includes('pos')).length} audit`, ReceiptText],
    ['Operator', `${auditRecords.length} действий`, UserRoundPlus],
    ['Platform', `${readNumber(commandSummary, 'failedCommands', 0) + readNumber(updateSummary, 'failedDevices', 0)} ошибок`, ShieldAlert]
  ];

  const runLogAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === 'Audit trail') {
        await apiClients.audit.search({ branchId: nextBackend.branchId, limit: 100 });
      } else if (label === 'CSV') {
        await apiClients.shifts.exportOperatorActionReportCsv(nextBackend.branchId, { limit: 100 });
      } else if (label === 'Ошибки') {
        await apiClients.audit.search({ branchId: nextBackend.branchId, outcome: 'denied', limit: 50 });
      } else {
        await apiClients.shifts.exportShiftReportCsv(nextBackend.branchId, { limit: 50 });
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
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{loadStatus === 'backend' ? 'Backend audit' : loadStatus === 'loading' ? 'Loading backend' : loadStatus === 'failed' ? 'Fixture fallback' : 'Fixture'}</span>
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
            <div><span>Объект</span><strong>{selectedEvent[1].split(' ')[0]}</strong></div>
            <div><span>Оператор</span><strong>{backend?.session.displayName ?? 'system'}</strong></div>
            <div><span>Branch</span><strong>{backend?.branchId.slice(0, 8) ?? 'fixture'}</strong></div>
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
  const [diagnostics, setDiagnostics] = useState<BranchDiagnosticsDto | null>(null);
  const [rollouts, setRollouts] = useState<UpdateRolloutStatusDto[]>([]);
  const [tariffs, setTariffs] = useState<TariffOptionDto[]>([]);

  const loadSettings = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      return;
    }

    setLoadStatus('loading');
    try {
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [staff, layoutZones, products, branchDiagnostics, rolloutStatuses, tariffOptions] = await Promise.all([
        apiClients.settings.getStaffUsers(nextBackend.branchId),
        apiClients.settings.getLayoutZones(nextBackend.branchId),
        apiClients.pos.getCatalog(nextBackend.branchId),
        apiClients.diagnostics.getDiagnostics(nextBackend.branchId),
        apiClients.updates.getRolloutStatuses(nextBackend.branchId),
        apiClients.settings.getTariffOptions(nextBackend.branchId)
      ]);
      setStaffUsers(Array.isArray(staff) ? staff : []);
      setZones(Array.isArray(layoutZones) ? layoutZones : []);
      setCatalog(Array.isArray(products) ? products : []);
      setDiagnostics(branchDiagnostics);
      setRollouts(Array.isArray(rolloutStatuses) ? rolloutStatuses : []);
      setTariffs(Array.isArray(tariffOptions) ? tariffOptions : []);
      setClubName(readString(nextBackend.session, 'organizationName', 'AFK4'));
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
    ['Пригласить сотрудника', 'нужна форма доступа', UserRoundPlus],
    ['Проверить устройства', 'diagnostics refresh', Wifi]
  ];

  const runSettingsAction = async (label: string) => {
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === 'Проверить устройства') {
        setDiagnostics(await apiClients.diagnostics.getDiagnostics(nextBackend.branchId));
      } else if (label === 'Добавить зал') {
        const zone = await apiClients.settings.createZone(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name: `Zone ${zones.length + 1}`,
          sortOrder: (zones.length + 1) * 10
        });
        setZones((items) => [...items, zone]);
      } else if (label === 'Добавить ПК') {
        const zoneId = readString(zones[0], 'zoneId');
        if (!zoneId) {
          throw new Error('Create a layout zone before adding a seat.');
        }

        await apiClients.settings.createSeat(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          zoneId,
          name: `PC-${zones.reduce((sum, zone) => sum + readArray(zone, 'seats').length, 0) + 1}`,
          sortOrder: 1000
        });
        await loadSettings(nextBackend);
      } else if (label === 'Создать тариф') {
        const tariff = await apiClients.settings.createTariff(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name: `Tariff ${tariffs.length + 1}`,
          idempotencyKey: createIdempotencyKey('tariff-create')
        });
        const tariffId = readString(tariff, 'tariffId');
        if (tariffId) {
          await apiClients.settings.createTariffVersion(nextBackend.branchId, tariffId, {
            organizationId: nextBackend.session.organizationId,
            tariffId,
            currencyCode,
            pricePerMinuteMinorUnits: 50,
            minimumBillableMinutes: 15,
            roundingIncrementMinutes: 5,
            effectiveFromUtc: new Date().toISOString(),
            idempotencyKey: createIdempotencyKey('tariff-version-create')
          });
        }
        await loadSettings(nextBackend);
      } else {
        throw new Error('General staff invite form is not implemented in the React operator yet.');
      }

      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const saveSettings = () => {
    if (!clubName.trim() || !city.trim()) {
      triggerFeedback(setFeedback, 'Проверить обязательные поля', 'failed', 'Заполните обязательные поля.');
      return;
    }

    triggerFeedback(setFeedback, 'Профиль клуба', 'failed', 'Backend endpoint for branch profile settings is not implemented yet.');
  };

  const renderSettingsContent = () => {
    if (selectedSection === 'Залы и ПК') {
      return (
        <>
          <div className="settings-section-title">
            <span>Залы и рабочие места</span>
            <button type="button" onClick={() => runSettingsAction('Добавить зал')}>Добавить зал</button>
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
        </>
      );
    }

    if (selectedSection === 'Тарифы') {
      return (
        <>
          <div className="settings-section-title">
            <span>Тарифы</span>
            <button type="button" onClick={() => runSettingsAction('Создать тариф')}>Создать тариф</button>
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
        </>
      );
    }

    if (selectedSection === 'Персонал') {
      return (
        <div className="settings-config-grid">
          {staffUsers.map((user) => (
            <button key={readString(user, 'staffUserId')} type="button" onClick={() => triggerFeedback(setFeedback, readString(user, 'displayName', 'Staff'), 'confirmed')}>
              <strong>{readString(user, 'displayName', 'Staff')}</strong>
              <span>{readString(user, 'userName', 'user')} · {readArray<string>(user, 'roleNames').join(', ') || 'roles'}</span>
            </button>
          ))}
        </div>
      );
    }

    if (selectedSection === 'POS и склад') {
      return (
        <div className="settings-config-grid">
          {catalog.slice(0, 8).map((product) => (
            <button key={readString(product, 'productId')} type="button" onClick={() => triggerFeedback(setFeedback, readString(product, 'name', 'Product'), 'confirmed')}>
              <strong>{readString(product, 'name', 'Product')}</strong>
              <span>{formatMoney(readMoney(product, 'price'), currencyCode)} · stock {readNumber(product, 'stockOnHand', 0)}</span>
            </button>
          ))}
        </div>
      );
    }

    if (selectedSection === 'Интеграции') {
      return (
        <div className="settings-config-grid">
          {[
            ['Платежи', 'manual provider'],
            ['Обновления', `${rollouts.length} rollout`],
            ['Ошибки обновлений', `${readNumber(updateSummary, 'failedDevices', 0)} devices`],
            ['API', backend?.config.platformBaseUrl ?? 'fixture']
          ].map(([name, detail]) => (
            <button key={name} type="button" onClick={() => triggerFeedback(setFeedback, name, 'confirmed')}>
              <strong>{name}</strong>
              <span>{detail}</span>
            </button>
          ))}
        </div>
      );
    }

    return (
      <>
        <div className="settings-form-grid">
          <label>Название клуба<input value={clubName} onChange={(event) => { setClubName(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>Город<input value={city} onChange={(event) => { setCity(event.currentTarget.value); setSettingsDirty(true); }} /></label>
          <label>Валюта<input value={currencyCode} readOnly /></label>
          <label>Филиал<input value={backend?.branchId ?? 'fixture'} readOnly /></label>
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
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{loadStatus === 'backend' ? 'Backend settings' : loadStatus === 'loading' ? 'Loading backend' : loadStatus === 'failed' ? 'Fixture fallback' : 'Fixture'}</span>
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
      setError(projectOperatorError(nextError).detail);
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
  const canUseTechMode = hasPermission(authSession, permissionNames.viewDiagnostics);

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
        setAuthError(projectOperatorError(error).detail);
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
      setAuthError(projectOperatorError(error).detail);
    } finally {
      setAuthSession(null);
      setAuthStatus('signed-out');
    }
  };

  const handleSeatAction = async (request: SeatActionRequest) => {
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
    if (request.type === 'start') {
      if (!hasPermission(session, permissionNames.startSession)) {
        throw new Error('Operator is not allowed to start sessions.');
      }

      if (request.seat.tone !== 'ready' || request.seat.activeSessionId) {
        throw new Error('Seat is not ready for session start.');
      }

      await clients.sessions.startGuestSession(branchId, {
        organizationId: session.organizationId,
        seatId: request.seat.id,
        durationMinutes: defaultSessionDurationMinutes,
        tariffRuleVersionId: defaultTariffRuleVersionId,
        idempotencyKey: createIdempotencyKey('session-start'),
        billingMode: ''
      });
    } else if (request.type === 'extend') {
      if (!hasPermission(session, permissionNames.extendSession)) {
        throw new Error('Operator is not allowed to extend sessions.');
      }

      if (!request.seat.activeSessionId) {
        throw new Error('Selected seat has no active session.');
      }

      await clients.sessions.extendSession(request.seat.activeSessionId, {
        additionalMinutes: request.minutes,
        tariffRuleVersionId: defaultTariffRuleVersionId,
        idempotencyKey: createIdempotencyKey('session-extend')
      });
    } else if (request.type === 'transfer') {
      if (!hasPermission(session, permissionNames.transferSession)) {
        throw new Error('Operator is not allowed to transfer sessions.');
      }

      if (!request.seat.activeSessionId) {
        throw new Error('Selected seat has no active session.');
      }

      await clients.sessions.transferSession(request.seat.activeSessionId, {
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

      await clients.sessions.endSession(request.seat.activeSessionId, {
        reason: 'operator',
        idempotencyKey: createIdempotencyKey('session-end')
      });
    }

    const nextState = await loadBackendFloorMapState(config, session, branchId);
    const preferredSeatId = request.type === 'transfer' ? request.targetSeatId : request.seat.id;
    setFloorMap(nextState);
    setSelectedSeatId(nextState.seats.some((seat) => seat.id === preferredSeatId)
      ? preferredSeatId
      : nextState.seats[0]?.id ?? '');
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
          session={authSession}
          actionsEnabled={floorMap.source === 'backend' && floorMap.loadStatus === 'ready'}
          onSeatAction={handleSeatAction}
        />
      )}
      {workspace !== 'map' && workspace !== 'dashboard' && workspace !== 'booking' && workspace !== 'pos' && workspace !== 'players' && workspace !== 'payments' && workspace !== 'logs' && workspace !== 'settings'
        && <SummarySidePanel workspace={workspace} currencyCode={config.currencyCode} />}

      <footer className="signals-strip">
        <span><Wifi size={14} />{realtimeLabel(realtimeState, realtimeError)} · {floorMap.source}</span>
        <span><MonitorCheck size={14} />Devices: {countByTone(floorMap.seats, 'offline')} offline, {countProblems(floorMap.seats)} attention</span>
        <span><CircleDollarSign size={14} />POS: 2 неоплаченных чека</span>
        <span><LockKeyhole size={14} />Критичные действия ждут подтверждения платформы</span>
      </footer>
    </div>
  );
}
