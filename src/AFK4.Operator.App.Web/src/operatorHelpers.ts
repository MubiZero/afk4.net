import { minorToMajor, majorToMinor } from '@afk4/money';
import { formatDateParts } from '@afk4/formatting';
import { formatMinorUnits } from './currencyFormat';
import { getOperatorConfig } from './operatorConfig';
import { projectOperatorError } from './apiErrors';
import { createOperatorApiClients, type BranchDiagnosticsDto, type OperatorDashboardSummaryDto, type PlayerPackageDto, type PosSaleDto, type ShiftDto } from './operatorApiClients';
import { PlatformApiClient, PlatformApiError } from './platformApi';
import { isHostBridgeUnavailableError } from './hostBridge';
import { signOutOperator, type OperatorAuthSession } from './authClient';
import { mapFloorMapDtoToState, seatStatusLabel, type FloorMapLoadStatus, type OperatorFloorMapState } from './floorMapState';
import { saveFloorMapCache } from './floorMapCache';
import { hasPermission, permissionNames } from './operatorPermissions';
import type { DeviceCommandResultDto, DeviceStatusChangedDto, OperatorRealtimeConnectionState, SessionLifecycleChangedDto } from './operatorRealtime';
import type { SeatSummary, SeatTone } from './operatorData';
import type {
  Feedback,
  FeedbackState,
  LoadStatus,
  MapFilterId,
  OperatorBackendContext,
  OperatorConfig,
  SessionBillingModeId,
  SessionBillingSelection
} from './operatorTypes';
import type { MessageKey } from '@afk4/i18n';

export type TFunc = (key: MessageKey, values?: Record<string, string | number>) => string;

export function toDateInputValue(date: Date) {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

export function toDateTimeInputValue(date: Date) {
  const hours = String(date.getHours()).padStart(2, '0');
  const minutes = String(date.getMinutes()).padStart(2, '0');
  return `${toDateInputValue(date)}T${hours}:${minutes}`;
}

export function addDays(date: Date, days: number) {
  const next = new Date(date);
  next.setDate(next.getDate() + days);
  return next;
}

export function addMinutes(date: Date, minutes: number) {
  const next = new Date(date);
  next.setMinutes(next.getMinutes() + minutes);
  return next;
}

export function countPeriodDays(from: string, to: string) {
  const fromDate = new Date(`${from}T00:00:00`);
  const toDate = new Date(`${to}T00:00:00`);

  if (Number.isNaN(fromDate.getTime()) || Number.isNaN(toDate.getTime()) || toDate < fromDate) {
    return 1;
  }

  return Math.max(1, Math.round((toDate.getTime() - fromDate.getTime()) / 86_400_000) + 1);
}

export function formatCompactNumber(value: number) {
  if (value >= 1000) {
    return `${(value / 1000).toFixed(value >= 10000 ? 0 : 1)}k`;
  }

  return String(value);
}


export function parseMoney(value: string) {
  const parsed = Number(value.replace(/[^\d-]/g, ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

export function triggerFeedback(
  setFeedback: (feedback: Feedback) => void,
  label: string,
  finalState: Exclude<FeedbackState, 'idle' | 'pending'> = 'failed',
  detail?: string
) {
  if (finalState === 'failed') {
    setFeedback({ label, state: 'failed', detail: detail ?? '' });
    return;
  }

  setFeedback({ label, state: 'pending' });
  window.setTimeout(() => setFeedback({ label, state: finalState }), 620);
}

export function feedbackText(feedback: Feedback, t: TFunc) {
  if (feedback.state === 'pending') {
    return t('op.helper.feedback.pending', { label: feedback.label });
  }

  if (feedback.state === 'failed') {
    if (feedback.detail) {
      return feedback.detail;
    }

    return t('op.helper.feedback.failed', { label: feedback.label });
  }

  if (feedback.state === 'confirmed') {
    if (feedback.detail) {
      return `${feedback.label}: ${feedback.detail}`;
    }

    return t('op.helper.feedback.confirmed', { label: feedback.label });
  }

  return '';
}

export const defaultSessionDurationMinutes = 60;
export const defaultTariffRuleVersionId = 'manual-v1';

// Незабилленный гость по дефолтному правилу — самый частый старт и единственный режим,
// который можно посадить «в один тап» без выбора игрока/тарифа. Панель и контекст-меню
// должны брать его отсюда, чтобы быстрый старт и старт из панели не разъехались.
export const guestBillingSelection: SessionBillingSelection = {
  mode: 'guest',
  tariffRuleVersionId: defaultTariffRuleVersionId,
  playerAccountId: null,
  tariffVersionId: null,
  playerPackageId: null
};
// Dashboard KPIs are now event-driven: a sessionLifecycleChanged push (or a reconnect) reconciles
// them immediately, so this poll is only a slow safety net that corrects drift / covers a realtime
// outage, not the primary refresh path.
export const shellOperationalRefreshMs = 120_000;

export function billingModeOptions(t: TFunc): Array<{ id: SessionBillingModeId; label: string; detail: string }> {
  return [
    { id: 'guest', label: t('op.helper.billing.guest'), detail: t('op.helper.billing.guestDetail') },
    { id: 'prepaid_wallet', label: t('op.helper.billing.prepaid'), detail: t('op.helper.billing.prepaidDetail') },
    { id: 'package', label: t('op.helper.billing.package'), detail: t('op.helper.billing.packageDetail') },
    { id: 'postpaid_debt', label: t('op.helper.billing.postpaid'), detail: t('op.helper.billing.postpaidDetail') }
  ];
}

export function mapFilterOptions(t: TFunc): Array<{ id: MapFilterId; label: string }> {
  return [
    { id: 'all', label: t('op.helper.zone.filter.all') },
    { id: 'ready', label: t('op.helper.zone.filter.ready') },
    { id: 'active', label: t('op.helper.zone.filter.active') },
    { id: 'offline', label: t('op.helper.zone.filter.offline') }
  ];
}

// Единый словарь подписей состояния — делегируем seatStatusLabel (плитка/панель/таблица одинаково).
export function toneLabel(tone: SeatTone, t: TFunc): string {
  return seatStatusLabel(tone, t);
}

export const emptyFeedback: Feedback = { label: '', state: 'idle' };

export function countByTone(nextSeats: SeatSummary[], tone: SeatTone): number {
  return nextSeats.filter((seat) => seat.tone === tone).length;
}

export function isPendingSeatCommand(seat: SeatSummary): boolean {
  return seat.tone === 'pending' || seat.command.toLowerCase().includes('pending');
}

export function matchesMapFilter(seat: SeatSummary, filterId: MapFilterId): boolean {
  if (filterId === 'all') {
    return true;
  }

  if (filterId === 'ready') {
    return seat.tone === 'ready' && !seat.activeSessionId;
  }

  if (filterId === 'active') {
    // Только здоровые сессии (ПК на связи). Сессия на отвалившемся ПК — тон offline, её место
    // в бакете «нет связи», не здесь (иначе двойной учёт).
    return seat.tone === 'active';
  }

  // Обслуживание — намеренное отключение, это НЕ «нет связи», в бакет не считаем (только «Все»).
  if (seat.tone === 'service') {
    return false;
  }

  // «Нет связи» — единый серый бакет: сбой команды, мёртвый heartbeat, сессия без связи с ПК.
  return seat.tone === 'offline' || seat.isDeviceOnline === false;
}

export function countByMapFilter(nextSeats: SeatSummary[], filterId: MapFilterId): number {
  return nextSeats.filter((seat) => matchesMapFilter(seat, filterId)).length;
}

export function zoneClass(zone: string): string {
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

export function billingLabel(value: string, t: TFunc) {
  const normalized = value.toLowerCase();

  if (normalized.includes('wallet')) {
    return t('op.helper.billing.deposit');
  }

  if (normalized.includes('package')) {
    return t('op.helper.billing.package');
  }

  if (normalized.includes('postpaid') || normalized.includes('постоплата')) {
    return t('op.helper.billing.postpaidLabel');
  }

  if (normalized.includes('guest')) {
    return t('op.helper.billing.guestLabel');
  }

  if (normalized.includes('открытый') || normalized.includes('open')) {
    return t('op.helper.billing.openTab');
  }

  if (normalized.includes('cash') || normalized.includes('наличн')) {
    return t('op.helper.billing.cash');
  }

  return t('op.helper.billing.notSet');
}

export function zoneLabel(zone: string, t: TFunc): string {
  return zone
    .replace('Console Lounge', t('op.helper.zone.consoleLounge'))
    .replace('Main Hall', t('op.helper.zone.mainHall'))
    .replace('VIP Room', t('op.helper.zone.vipRoom'))
    .replace('Bootcamp', t('op.helper.zone.bootcamp'));
}

export function appVersionLabel(app: string, t: TFunc): string {
  return app
    .replaceAll('Agent', t('op.helper.appVer.agent'))
    .replaceAll('Shell', t('op.helper.appVer.shell'));
}

export function operatorDisplayNameLabel(displayName: string | null | undefined, t: TFunc): string {
  const normalized = displayName?.trim();

  if (!normalized) {
    return t('op.helper.staff.shiftOperator');
  }

  if (/^cashier one$/i.test(normalized)) {
    return t('op.helper.staff.shiftOperator');
  }

  if (/^demo owner$/i.test(normalized)) {
    return t('op.helper.staff.clubAdmin');
  }

  if (/^local branch manager$/i.test(normalized)) {
    return t('op.helper.staff.branchMgr');
  }

  return normalized;
}

export function staffRoleLabel(roleName: string, t: TFunc): string {
  switch (roleName) {
    case 'cashier_operator':
      return t('op.helper.staff.cashierOperator');
    case 'shift_supervisor':
      return t('op.helper.staff.shiftSupervisor');
    case 'branch_manager':
      return t('op.helper.staff.branchManager');
    case 'technician':
      return t('op.helper.staff.technician');
    case 'accountant_auditor':
      return t('op.helper.staff.accountant');
    default:
      return roleName || t('op.helper.staff.roleNotSet');
  }
}

export function updateComponentLabel(component: string, t: TFunc): string {
  switch (component) {
    case 'operator-app':
      return t('op.helper.update.component.operatorApp');
    case 'agent-service':
      return t('op.helper.update.component.agentService');
    case 'player-shell':
      return t('op.helper.update.component.playerShell');
    default:
      return t('op.helper.update.component.fallback');
  }
}

export function updateChannelLabel(channel: string, t: TFunc): string {
  switch (channel) {
    case 'internal':
      return t('op.helper.update.channel.internal');
    case 'beta':
      return t('op.helper.update.channel.beta');
    case 'stable':
      return t('op.helper.update.channel.stable');
    default:
      return t('op.helper.update.channel.fallback');
  }
}

export function updateTargetKindLabel(kind: string, t: TFunc): string {
  switch (kind) {
    case 'branch':
      return t('op.helper.update.target.branch');
    case 'device':
      return t('op.helper.update.target.device');
    default:
      return t('op.helper.update.target.fallback');
  }
}

export function updatePackageStateLabel(state: string, t: TFunc): string {
  switch (state) {
    case 'registered':
      return t('op.helper.update.pkgState.registered');
    case 'validated':
      return t('op.helper.update.pkgState.validated');
    case 'rejected':
      return t('op.helper.update.pkgState.rejected');
    case 'retired':
      return t('op.helper.update.pkgState.retired');
    default:
      return t('op.helper.update.pkgState.fallback');
  }
}

export function updateRolloutStateLabel(state: string, t: TFunc): string {
  switch (state) {
    case 'active':
      return t('op.helper.update.rollout.active');
    case 'paused':
      return t('op.helper.update.rollout.paused');
    case 'completed':
      return t('op.helper.update.rollout.completed');
    case 'rollback_requested':
      return t('op.helper.update.rollout.rollbackRequested');
    case 'rolled_back':
      return t('op.helper.update.rollout.rolledBack');
    case 'cancelled':
      return t('op.helper.update.rollout.cancelled');
    default:
      return t('op.helper.update.rollout.fallback');
  }
}

export function updateDeviceStatusLabel(status: string, t: TFunc): string {
  switch (status) {
    case 'installed':
      return t('op.helper.update.deviceStatus.installed');
    case 'target reached':
      return t('op.helper.update.deviceStatus.reached');
    case 'pending':
      return t('op.helper.update.deviceStatus.pending');
    case 'failed':
      return t('op.helper.update.deviceStatus.failed');
    default:
      return t('op.helper.update.deviceStatus.unknown');
  }
}

export function stockMovementTypeLabel(type: string, t: TFunc): string {
  switch (type) {
    case 'purchase':
      return t('op.helper.stock.purchase');
    case 'adjustment':
      return t('op.helper.stock.adjustment');
    case 'sale':
      return t('op.helper.stock.sale');
    case 'refund':
      return t('op.helper.stock.refund');
    case 'write_off':
      return t('op.helper.stock.writeOff');
    default:
      return t('op.helper.stock.fallback');
  }
}

export function updateDeviceMessageLabel(message: string, t: TFunc): string {
  if (message === 'target reached') {
    return t('op.helper.update.msg.targetReached');
  }

  if (message === 'installed') {
    return t('op.helper.update.msg.installed');
  }

  return commandStatusMessageLabel(message, t);
}

export function commandLabel(command: string, t: TFunc) {
  if (command.includes('Lease fresh')) {
    return t('op.helper.command.leased');
  }

  if (command.includes('Unlock pending')) {
    return t('op.helper.command.unlockPending');
  }

  if (command.includes('Start pending')) {
    return t('op.helper.command.startPending');
  }

  if (command.includes('Stop pending')) {
    return t('op.helper.command.stopPending');
  }

  if (command.includes('Payment check')) {
    return t('op.helper.command.paymentCheck');
  }

  if (command.includes('No route')) {
    return t('op.helper.command.noRoute');
  }

  if (command.includes('Idle')) {
    return t('op.helper.command.idle');
  }

  if (command.includes('Command failed')) {
    return t('op.helper.command.failed');
  }

  if (command.includes('Technician')) {
    return t('op.helper.command.tech');
  }

  if (command.includes('Low balance')) {
    return t('op.helper.command.lowBalance');
  }

  return command;
}

// Локализует только слова Agent/Shell в строке версий («Agent 0.4 · Shell 0.4»); номера версий
// оставляет как есть. Если версий нет (фолбэк-строка 'Shell') — честный прочерк, а не «Оболочка».
export function appVersionsLabel(app: string, t: TFunc): string {
  if (!app || app.trim() === 'Shell') {
    return '—';
  }

  return app
    .replaceAll('Agent', t('op.helper.appVer.agent'))
    .replaceAll('Shell', t('op.helper.appVer.shell'));
}

export function deviceStatusLabel(device: string, t: TFunc) {
  return device
    .replace('Device unassigned', t('op.helper.deviceStatus.unassigned'))
    .replace('Online', t('op.helper.deviceStatus.online'))
    .replace('Offline', t('op.helper.deviceStatus.offline'))
    .replaceAll('Agent', t('op.helper.appVer.agent'))
    .replaceAll('Shell', t('op.helper.appVer.shell'))
    .replace('unlocked', t('op.helper.deviceStatus.unlocked'))
    .replace('locked state unknown', t('op.helper.deviceStatus.lockUnknown'))
    .replace('locked', t('op.helper.deviceStatus.locked'));
}

export function mapSeatStatus(seat: SeatSummary, t: TFunc) {
  if (seat.tone === 'active') {
    return {
      label: t('op.helper.map.sessionActive'),
      value: seat.remaining
    };
  }

  if (seat.tone === 'ready') {
    return {
      label: t('op.helper.map.ready'),
      value: t('op.helper.map.readyValue')
    };
  }

  if (seat.tone === 'pending') {
    return {
      label: t('op.helper.map.commandEnRoute'),
      value: commandLabel(seat.command, t)
    };
  }

  if (seat.tone === 'offline') {
    return {
      label: t('op.helper.map.noSignal'),
      value: commandLabel(seat.command, t)
    };
  }

  // blocking — нужно действие.
  return {
    label: t('op.helper.map.needsCheck'),
    value: commandLabel(seat.command, t)
  };
}

export function floorMapLoadLabel(status: FloorMapLoadStatus, source: OperatorFloorMapState['source'], error: string | null, t: TFunc) {
  if (status === 'loading') {
    return source === 'backend'
      ? t('op.helper.shift.mapUpdating')
      : t('op.helper.shift.mapLoading');
  }

  if (status === 'failed') {
    return error
      ? t('op.helper.shift.platformError', { error })
      : t('op.helper.shift.platformErrorApi');
  }

  return source === 'backend'
    ? t('op.helper.shift.platformConnected')
    : t('op.helper.shift.localData');
}

export function workspaceLoadStatusLabel(status: LoadStatus, backendLabel: string, t: TFunc): string {
  if (status === 'backend') {
    return backendLabel;
  }

  if (status === 'loading') {
    return t('op.helper.shift.dataLoading');
  }

  if (status === 'failed') {
    return t('op.helper.shift.loadError');
  }

  return t('op.helper.shift.localData');
}

export function dataSourceLabel(source: string, t: TFunc): string {
  return source === 'backend'
    ? t('op.helper.shift.platformConnected')
    : t('op.helper.shift.localData');
}

export function shellShiftLabel(
  shift: ShiftDto | null,
  summary: OperatorDashboardSummaryDto | null,
  status: LoadStatus,
  error: string | null,
  t: TFunc
): string {
  const shiftSource = shift ?? readRecord(summary, 'shift');
  if (shiftSource !== null) {
    const state = readString(shiftSource, 'state').toLowerCase();
    if (state === 'open') {
      return t('op.helper.shift.open', { time: formatTime(readString(shiftSource, 'openedAtUtc')) });
    }

    if (state === 'closed') {
      return t('op.helper.shift.closed', { time: formatTime(readString(shiftSource, 'closedAtUtc')) });
    }

    const stateStr = state || t('op.helper.shift.stateUnknown');
    return t('op.helper.shift.state', { state: stateStr });
  }

  if (status === 'loading') {
    return t('op.helper.shift.loading');
  }

  if (status === 'failed') {
    return error
      ? t('op.helper.shift.error')
      : t('op.helper.shift.noAccess');
  }

  return t('op.helper.shift.notOpen');
}

export type ShellShiftTone = 'open' | 'closed' | 'idle';

export interface ShellShiftBadgeData {
  tone: ShellShiftTone;
  value: string;
  full: string;
}

// Компактный бейдж смены для подвала рейла. Отдаёт короткое стабильное `value` (время или символ),
// чтобы строка не меняла высоту между состояниями — высота скачет, когда длинный текст переносится.
// Полная человеческая фраза остаётся в `full` (идёт в title-подсказку).
export function shellShiftBadge(
  shift: ShiftDto | null,
  summary: OperatorDashboardSummaryDto | null,
  status: LoadStatus,
  error: string | null,
  t: TFunc
): ShellShiftBadgeData {
  const full = shellShiftLabel(shift, summary, status, error, t);
  const shiftSource = shift ?? readRecord(summary, 'shift');
  if (shiftSource !== null) {
    const state = readString(shiftSource, 'state').toLowerCase();
    if (state === 'open') {
      return { tone: 'open', value: formatTime(readString(shiftSource, 'openedAtUtc')), full };
    }
    if (state === 'closed') {
      return { tone: 'closed', value: formatTime(readString(shiftSource, 'closedAtUtc')), full };
    }
    return { tone: 'idle', value: '—', full };
  }

  return { tone: 'idle', value: status === 'loading' ? '…' : '—', full };
}

export function shellPosLabel(summary: OperatorDashboardSummaryDto | null, status: LoadStatus, t: TFunc): string {
  if (summary !== null) {
    const revenue = readRecord(summary, 'revenue');
    const posChecks = readNumber(revenue, 'posCheckCount', 0);
    return t('op.helper.shell.posChecks', { count: posChecks });
  }

  if (status === 'loading') {
    return t('op.helper.shell.posLoading');
  }

  return t('op.helper.shell.posNoData');
}

export function shellModeLabel(mode: string, t: TFunc): string {
  if (mode.includes('dev')) {
    return t('op.helper.shell.modeDev');
  }

  if (mode.includes('dist')) {
    return t('op.helper.shell.modeDist');
  }

  return mode;
}

export function describeTechModeResult(
  seat: SeatSummary,
  device: Record<string, unknown>,
  diagnostics: BranchDiagnosticsDto,
  t: TFunc
): string {
  const commandSummary = isRecord(diagnostics) ? diagnostics.commandSummary : null;
  const machineName = readString(device, 'machineName', seat.deviceName || seat.name);
  const agentVersion = readString(device, 'agentVersion', 'unknown');
  const shellVersion = readString(device, 'shellVersion', 'unknown');
  const pendingCommands = readNumber(commandSummary, 'pendingCommands', 0);
  const failedCommands = readNumber(commandSummary, 'failedCommands', 0);

  return t('op.shell.techMode', {
    name: machineName,
    agentVersion,
    shellVersion,
    pending: pendingCommands,
    failed: failedCommands
  });
}

export function projectAuthHostError(error: unknown, config: OperatorConfig, t: TFunc): string {
  if (isHostBridgeUnavailableError(error) && config.runtime !== 'browser-dev') {
    return t('op.shell.err.nativeAuthUnavailable');
  }

  return projectOperatorError(error, t).detail;
}

export function projectOperatorFacingError(error: unknown, t: TFunc): string {
  const detail = projectOperatorError(error, t).detail;
  const platformDetail = extractPlatformError(detail);

  if (detail.includes('Only active or paused sessions can be ended') ||
    platformDetail?.includes('Only active or paused sessions can be ended')) {
    return t('op.helper.error.sessionEnded');
  }

  if (detail.includes('401 Unauthorized')) {
    return t('op.helper.error.sessionExpired');
  }

  if (platformDetail) {
    return platformDetail;
  }

  const badRequest = t('op.helper.error.badRequest');
  const serverError = t('op.helper.error.serverError');
  const sessionExpired = t('op.helper.error.sessionExpired');

  return detail
    .replace('Platform API returned 400 Bad Request:', badRequest)
    .replace('Platform API returned 401 Unauthorized:', sessionExpired)
    .replace('Platform API returned 500 Internal Server Error:', serverError);
}

export function extractPlatformError(detail: string): string | null {
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

export function realtimeLabel(state: OperatorRealtimeConnectionState, error: string | null, t: TFunc): string {
  if (state === 'connected') {
    return t('op.helper.realtime.connected');
  }

  if (state === 'connecting') {
    return t('op.helper.realtime.connecting');
  }

  if (state === 'reconnecting') {
    return t('op.helper.realtime.reconnecting');
  }

  return error
    ? t('op.helper.realtime.lost')
    : t('op.helper.realtime.disconnected');
}

export function resolveActiveBranchId(session: OperatorAuthSession, configBranchId?: string): string | null {
  return session.activeBranchId ?? configBranchId ?? session.branchIds[0] ?? null;
}

export function matchesRealtimeScope(status: DeviceStatusChangedDto, session: OperatorAuthSession, branchId: string): boolean {
  return status.organizationId.toLowerCase() === session.organizationId.toLowerCase()
    && status.branchId.toLowerCase() === branchId.toLowerCase();
}

export function matchesCommandResultScope(result: DeviceCommandResultDto, session: OperatorAuthSession, branchId: string): boolean {
  return result.organizationId.toLowerCase() === session.organizationId.toLowerCase()
    && result.branchId.toLowerCase() === branchId.toLowerCase();
}

export function matchesLifecycleScope(change: SessionLifecycleChangedDto, session: OperatorAuthSession, branchId: string): boolean {
  return change.organizationId.toLowerCase() === session.organizationId.toLowerCase()
    && change.branchId.toLowerCase() === branchId.toLowerCase();
}

export function findSeatForDeviceStatus(nextSeats: SeatSummary[], status: DeviceStatusChangedDto): SeatSummary | null {
  const statusDeviceId = status.deviceId.toLowerCase();
  const statusMachineName = status.machineName.toLowerCase();
  return nextSeats.find((seat) =>
    (seat.deviceId ?? '').toLowerCase() === statusDeviceId ||
    seat.name.toLowerCase() === statusMachineName) ?? null;
}

export function shouldReloadFloorMapAfterDeviceStatus(seat: SeatSummary, status: DeviceStatusChangedDto): boolean {
  return status.isLocked && (Boolean(seat.activeSessionId) || seat.hasActiveSession === true || isPendingSeatCommand(seat));
}

export function createAuthenticatedOperatorClients(config: ReturnType<typeof getOperatorConfig>, session: OperatorAuthSession) {
  return createOperatorApiClients(new PlatformApiClient({
    baseUrl: config.platformBaseUrl,
    getAccessToken: () => session.accessToken
  }));
}

export function isUnauthorizedPlatformError(error: unknown): boolean {
  return error instanceof PlatformApiError && error.status === 401;
}

export function isUnauthorizedAuthError(error: unknown): boolean {
  return error instanceof Error && error.message.includes('401 Unauthorized');
}

export async function clearStoredOperatorSession(): Promise<void> {
  try {
    await signOutOperator();
  } catch {
    // Best-effort cleanup; the original auth failure is the actionable error.
  }
}

export async function loadBackendFloorMapState(
  config: ReturnType<typeof getOperatorConfig>,
  session: OperatorAuthSession,
  branchId: string,
  t: TFunc
): Promise<OperatorFloorMapState> {
  const clients = createAuthenticatedOperatorClients(config, session);
  const { value: floorMap, etag } = await clients.floorMap.getFloorMapWithEtag(branchId);
  // Persist the last-known-good snapshot so the workspace can degrade to a read-only mirror offline (§6.5).
  saveFloorMapCache(branchId, floorMap, Date.now());
  return mapFloorMapDtoToState(floorMap, t, etag);
}

export function createIdempotencyKey(operationName: string): string {
  const unique = window.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  return `${operationName}-${unique}`;
}

export function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

export function readString(value: unknown, name: string, fallback = ''): string {
  if (!isRecord(value)) {
    return fallback;
  }

  const nextValue = value[name];
  return typeof nextValue === 'string' && nextValue.length > 0 ? nextValue : fallback;
}

export function readNumber(value: unknown, name: string, fallback = 0): number {
  if (!isRecord(value)) {
    return fallback;
  }

  const nextValue = value[name];
  return typeof nextValue === 'number' && Number.isFinite(nextValue) ? nextValue : fallback;
}

export function readBoolean(value: unknown, name: string, fallback = false): boolean {
  if (!isRecord(value)) {
    return fallback;
  }

  const nextValue = value[name];
  return typeof nextValue === 'boolean' ? nextValue : fallback;
}

export function readArray<T = unknown>(value: unknown, name: string): T[] {
  if (!isRecord(value)) {
    return [];
  }

  const nextValue = value[name];
  return Array.isArray(nextValue) ? nextValue as T[] : [];
}

export function readMoney(value: unknown, name: string): { currencyCode: string; minorUnits: number } | null {
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

export function readRecord(value: unknown, name: string): Record<string, unknown> | null {
  if (!isRecord(value)) {
    return null;
  }

  const nextValue = value[name];
  return isRecord(nextValue) ? nextValue : null;
}

export { formatMinorUnits } from './currencyFormat';

export function formatMoney(value: unknown, fallbackCurrencyCode: string): string {
  if (isRecord(value)) {
    const currencyCode = readString(value, 'currencyCode', fallbackCurrencyCode);
    const minorUnits = readNumber(value, 'minorUnits', 0);
    return formatMinorUnits(minorUnits, currencyCode);
  }

  return formatMinorUnits(0, fallbackCurrencyCode);
}

export function parseMoneyInputMinorUnits(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) {
    return null;
  }

  const majorUnits = Number(normalized);
  return Number.isFinite(majorUnits) && majorUnits > 0 ? majorToMinor(majorUnits) : null;
}

export function parseNonNegativeMoneyInputMinorUnits(value: string): number | null {
  const normalized = value.trim().replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(normalized)) {
    return null;
  }

  const majorUnits = Number(normalized);
  return Number.isFinite(majorUnits) && majorUnits >= 0 ? majorToMinor(majorUnits) : null;
}

export function formatMoneyInputMinorUnits(minorUnits: number): string {
  return minorToMajor(minorUnits).toFixed(2);
}

// Reason inputs render empty with a placeholder (§7.5) so a quick top-up/debt-payment
// isn't blocked by a required free-text field — but the audit trail still needs a real
// string. A blank submit resolves to the same localized default the placeholder shows.
export function resolveReasonInput(rawReason: string, fallback: string): string {
  const trimmed = rawReason.trim();
  return trimmed === '' ? fallback : trimmed;
}

export function dashboardRangeQuery(from: string, to: string) {
  return {
    fromUtc: `${from}T00:00:00.000Z`,
    toUtc: `${to}T23:59:59.999Z`,
    limit: 8
  };
}

export function emptyDashboardSummary(currencyCode: string, from: string, to: string): OperatorDashboardSummaryDto {
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

export function formatTime(value: unknown): string {
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

export function formatDateTime(value: unknown): string {
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

export function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (character) => ({
    '&': '&amp;',
    '<': '&lt;',
    '>': '&gt;',
    '"': '&quot;',
    "'": '&#39;'
  })[character] ?? character);
}

export function safeReceiptFileName(value: string): string {
  return value.replace(/[^a-zA-Z0-9._-]+/g, '-').replace(/^-+|-+$/g, '') || 'receipt';
}

export function downloadTextFile(fileName: string, contents: string, mimeType = 'text/plain;charset=utf-8') {
  const url = window.URL.createObjectURL(new Blob([contents], { type: mimeType }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  window.URL.revokeObjectURL(url);
}

export function posSaleStateLabel(state: string, t: TFunc): string {
  switch (state.toLowerCase()) {
    case 'paid':
      return t('op.helper.pos.saleState.paid');
    case 'draft':
      return t('op.helper.pos.saleState.draft');
    case 'void':
    case 'voided':
      return t('op.helper.pos.saleState.voided');
    case 'refund':
    case 'refunded':
      return t('op.helper.pos.saleState.refund');
    case 'pending':
      return t('op.helper.pos.saleState.pending');
    case 'sale':
      return t('op.helper.pos.saleState.sale');
    default:
      return t('op.helper.pos.saleState.fallback');
  }
}

export function posReceiptTypeLabel(type: string, t: TFunc): string {
  switch (type.toLowerCase()) {
    case 'sale':
    case 'paid':
      return t('op.helper.pos.receiptType.sale');
    case 'refund':
    case 'refunded':
      return t('op.helper.pos.receiptType.refund');
    case 'void':
    case 'voided':
      return t('op.helper.pos.receiptType.void');
    default:
      return t('op.helper.pos.receiptType.fallback');
  }
}

export function posSaleLineSummary(row: unknown, t: TFunc): string {
  const lineCount = readNumber(row, 'lineCount', 0);
  const itemQuantity = readNumber(row, 'itemQuantity', 0);
  return t('op.helper.pos.lineSummary', { lines: lineCount, qty: itemQuantity });
}

export function shiftStateLabel(state: string, t: TFunc): string {
  switch (state.toLowerCase()) {
    case 'open':
      return t('op.status.open');
    case 'closed':
      return t('op.status.closed');
    case 'closing':
      return t('op.status.closing');
    case 'unknown':
      return t('op.status.unknown');
    case 'нет смены':
      return t('op.status.none');
    default:
      return state ? t('op.status.unknown') : t('op.status.none');
  }
}

export function cashOperationTypeLabel(type: string, t: TFunc): string {
  switch (type.toLowerCase()) {
    case 'opening':
      return t('op.helper.billing.cashOp.opening');
    case 'closing':
      return t('op.helper.billing.cashOp.closing');
    case 'cash_in':
      return t('op.helper.billing.cashOp.cashIn');
    case 'cash_out':
      return t('op.helper.billing.cashOp.cashOut');
    case 'refund':
      return t('op.helper.billing.cashOp.refund');
    default:
      return t('op.helper.billing.cashOp.fallback');
  }
}

export function paymentSourceLabel(source: string, t: TFunc): string {
  switch (source.toLowerCase()) {
    case 'shift':
      return t('op.helper.billing.paySource.shift');
    case 'cash':
      return t('op.helper.billing.paySource.cash');
    case 'pos':
    case 'sale':
      return t('op.helper.billing.paySource.sale');
    default:
      return t('op.helper.billing.paySource.fallback');
  }
}

export function buildPosReceiptText(sale: PosSaleDto, receipt: Record<string, unknown> | null, currencyCode: string, t: TFunc): string {
  const receiptNumber = readString(receipt, 'receiptNumber', t('op.pos.receipts.receiptFallback'));
  const receiptType = posReceiptTypeLabel(readString(receipt, 'receiptType', readString(sale, 'state', 'sale')), t);
  const createdAtUtc = readString(receipt, 'createdAtUtc', readString(sale, 'createdAtUtc'));
  const productFallback = t('op.helper.pos.productFallback');
  const unitSuffix = t('op.helper.pos.unitSuffix');
  const lines = readArray(sale, 'lines').map((line) => [
    readString(line, 'productName', productFallback),
    `${readNumber(line, 'quantity', 0)} ${unitSuffix}`,
    formatMoney(readMoney(line, 'unitPrice'), currencyCode),
    formatMoney(readMoney(line, 'lineTotal'), currencyCode)
  ].join(' | '));

  const header = t('op.helper.pos.receiptHeader');
  const numLine = t('op.helper.pos.receiptNumber', { number: receiptNumber });
  const typeLine = t('op.helper.pos.receiptType', { type: receiptType });
  const createdLine = t('op.helper.pos.receiptCreated', { date: createdAtUtc || '—' });
  const totalLine = t('op.helper.pos.receiptTotal', { total: formatMoney(readMoney(sale, 'total'), currencyCode) });

  return [header, numLine, typeLine, createdLine, '', ...lines, '', totalLine].join('\n');
}

export function requireBackend(backend: OperatorBackendContext | null, t: TFunc): OperatorBackendContext {
  if (backend === null) {
    throw new Error(t('op.helper.error.sessionUnavailable'));
  }

  return backend;
}

export function billingModeLabel(mode: SessionBillingModeId, t: TFunc) {
  return billingModeOptions(t).find((option) => option.id === mode)?.label ?? mode;
}

export function tariffOptionLabel(tariff: Record<string, unknown>, currencyCode: string, t: TFunc) {
  const name = readString(tariff, 'name', t('op.helper.player.tariffFallback'));
  const price = readNumber(tariff, 'pricePerMinuteMinorUnits', 0);
  const currency = readString(tariff, 'currencyCode', currencyCode);
  return t('op.helper.player.tariffPerMin', { name, price: formatMinorUnits(price, currency) });
}

export function playerPackageLabel(playerPackage: PlayerPackageDto, t: TFunc) {
  const remainingSeconds = playerPackage.remainingIncludedSeconds + playerPackage.remainingBonusSeconds;
  const name = playerPackage.name ?? t('op.helper.player.packageFallback');
  const minutes = Math.floor(remainingSeconds / 60);
  return t('op.helper.player.packageLabel', { name, minutes });
}

export function packageOptionLabel(packageOption: Record<string, unknown>, currencyCode: string, t: TFunc) {
  const name = readString(packageOption, 'name', t('op.helper.player.packageFallback'));
  const price = readNumber(packageOption, 'priceMinorUnits', 0);
  const currency = readString(packageOption, 'currencyCode', currencyCode);
  const totalMinutes = Math.floor((readNumber(packageOption, 'includedSeconds', 0) + readNumber(packageOption, 'bonusSeconds', 0)) / 60);
  return t('op.helper.player.packageOptionLabel', { name, price: formatMinorUnits(price, currency), minutes: totalMinutes });
}

export function describeDeviceCommandStatus(status: Record<string, unknown>, t: TFunc) {
  const type = readString(status, 'type', 'command');
  const state = readString(status, 'status', 'pending');
  const message = readString(status, 'message');
  const typeLabel = commandTypeLabel(type, t);
  const stateLabel = commandStatusLabel(state, t);
  const messageLabel = commandStatusMessageLabel(message, t);
  return messageLabel ? `${typeLabel}: ${stateLabel} · ${messageLabel}` : `${typeLabel}: ${stateLabel}`;
}

export function describeSessionCommandFallback(response: unknown, t: TFunc) {
  const commands = readArray<Record<string, unknown>>(response, 'deviceCommands');
  if (commands.length === 0) {
    return t('op.helper.command.platformConfirmed');
  }

  const command = commands[0];
  const sentLabel = t('op.helper.command.sentToPc');
  return `${commandTypeLabel(readString(command, 'type', 'command'), t)}: ${sentLabel}`;
}

export function commandTypeLabel(type: string, t: TFunc): string {
  switch (type.toLowerCase()) {
    case 'lock':
      return t('op.helper.command.type.lock');
    case 'unlock':
      return t('op.helper.command.type.unlock');
    case 'transfer':
      return t('op.helper.command.type.transfer');
    case 'reboot':
      return t('op.helper.command.type.reboot');
    case 'shutdown':
      return t('op.helper.command.type.shutdown');
    case 'refresh-session-lease':
      return t('op.helper.command.type.refreshSession');
    default:
      return t('op.helper.command.type.fallback');
  }
}

export function commandStatusLabel(status: string, t: TFunc): string {
  switch (status.toLowerCase()) {
    case 'pending':
      return t('op.helper.command.status.pending');
    case 'sent':
    case 'accepted':
    case 'in_progress':
      return t('op.helper.command.status.inProgress');
    case 'completed':
    case 'succeeded':
      return t('op.helper.command.status.done');
    case 'failed':
      return t('op.helper.command.status.failed');
    case 'cancelled':
    case 'canceled':
      return t('op.helper.command.status.cancelled');
    default:
      return status;
  }
}

export function commandStatusMessageLabel(message: string, t: TFunc): string {
  return message
    .replace('Agent accepted lock', t('op.helper.cmdMsg.acceptedLock'))
    .replace('Agent accepted unlock', t('op.helper.cmdMsg.acceptedUnlock'))
    .replace('Agent accepted transfer', t('op.helper.cmdMsg.acceptedTransfer'))
    .replace('Agent timeout', t('op.helper.cmdMsg.agentTimeout'))
    .replace('timeout waiting for Agent', t('op.helper.cmdMsg.agentTimeoutWait'))
    .replace('Queued for Agent', t('op.helper.cmdMsg.queued'))
    .replace('Agent did not confirm lock.', t('op.helper.cmdMsg.notConfirmedLock'));
}

export function dashboardFocusTextLabel(text: string, t: TFunc): string {
  return commandStatusMessageLabel(text, t)
    .replace('lock Failed', t('op.shell.focus.lockFailed'))
    .replace('unlock Failed', t('op.shell.focus.unlockFailed'))
    .replace('Failed', t('op.shell.focus.failed'))
    .replace('Agent', t('op.helper.appVer.agent'));
}

export async function describeSeatActionResult(
  clients: ReturnType<typeof createAuthenticatedOperatorClients>,
  session: OperatorAuthSession,
  seat: SeatSummary,
  response: unknown,
  t: TFunc
) {
  const fallback = describeSessionCommandFallback(response, t);
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
    return describeDeviceCommandStatus(await clients.devices.getDeviceCommandStatus(deviceId, commandId), t);
  } catch (error) {
    const statusUnavail = t('op.helper.command.statusUnavailable', { detail: projectOperatorError(error, t).detail });
    return `${fallback} · ${statusUnavail}`;
  }
}

export async function describeDispatchedDeviceCommand(
  clients: ReturnType<typeof createAuthenticatedOperatorClients>,
  session: OperatorAuthSession,
  seat: SeatSummary,
  command: Record<string, unknown>,
  t: TFunc
): Promise<string> {
  const commandId = readString(command, 'commandId');
  const deviceId = seat.deviceId || readString(command, 'deviceId');
  const sentLabel = t('op.helper.command.sentToPc');
  const fallback = `${commandTypeLabel(readString(command, 'type', 'command'), t)}: ${sentLabel}`;
  if (!commandId || !deviceId || !hasPermission(session, permissionNames.viewDeviceCommandStatus)) {
    return fallback;
  }

  try {
    return describeDeviceCommandStatus(await clients.devices.getDeviceCommandStatus(deviceId, commandId), t);
  } catch (error) {
    const statusUnavail = t('op.helper.command.statusUnavailable', { detail: projectOperatorError(error, t).detail });
    return `${fallback} · ${statusUnavail}`;
  }
}

export type PlayerClientItem = {
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

export function projectPlayerClient(player: unknown, t: TFunc): PlayerClientItem {
  const debt = readNumber(player, 'debtBalanceMinorUnits', 0);
  const packages = readNumber(player, 'activePackageCount', 0);
  const isActive = isRecord(player) && player.isActive !== false;
  const phone = readString(player, 'phoneNumber', t('op.helper.player.noPhone'));
  const lastLabel = packages > 0
    ? t('op.helper.player.packageCount', { count: packages })
    : t('op.helper.player.platform');
  const detail = t('op.helper.player.detail', { phone, packages });
  return {
    playerAccountId: readString(player, 'playerAccountId') || undefined,
    name: readString(player, 'displayName', t('op.helper.player.nameFallback')),
    // Stable status key — rendered via playerStatusLabel and filtered on in BackendPlayersWorkspace.
    // Бейдж подсвечивает только отклонения (долг/деактивация); активный без долга — без подписи.
    status: debt > 0 ? 'debt' : isActive ? 'active' : 'inactive',
    balanceMinorUnits: readNumber(player, 'walletBalanceMinorUnits', 0),
    debtMinorUnits: debt,
    last: lastLabel,
    tone: debt > 0 ? 'debt' : isActive ? 'active' : 'regular',
    detail,
    phoneNumber: readString(player, 'phoneNumber', ''),
    source: 'backend'
  };
}

export function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim());
}

export function auditActionLabel(action: string, t: TFunc): string {
  const normalized = action.toLowerCase();
  switch (normalized) {
    case 'pos.sale.create':
    case 'pos.sales.create':
      return t('op.helper.audit.saleCreated');
    case 'pos.sale.refund':
    case 'pos.sales.refund':
      return t('op.helper.audit.saleRefund');
    case 'pos.sale.void':
    case 'pos.sales.void':
      return t('op.helper.audit.saleVoid');
    case 'sessions.start':
    case 'session.start':
      return t('op.helper.audit.sessionStart');
    case 'sessions.extend':
    case 'session.extend':
      return t('op.helper.audit.sessionExtend');
    case 'sessions.end':
    case 'session.end':
      return t('op.helper.audit.sessionEnd');
    case 'identity.staff.create':
      return t('op.helper.audit.staffCreate');
    case 'identity.staff.roles.update':
      return t('op.helper.audit.staffRolesUpdate');
    case 'updates.rollouts.view':
      return t('op.helper.audit.updatesView');
    case 'updates.rollouts.state.change':
      return t('op.helper.audit.updatesStateChange');
    default:
      if (normalized.includes('pos')) {
        return t('op.helper.audit.opPos');
      }

      if (normalized.includes('session')) {
        return t('op.helper.audit.opSession');
      }

      if (normalized.includes('device')) {
        return t('op.helper.audit.opDevice');
      }

      if (normalized.includes('shift')) {
        return t('op.helper.audit.opShift');
      }

      if (normalized.includes('identity') || normalized.includes('staff')) {
        return t('op.helper.audit.opStaff');
      }

      if (normalized.includes('update')) {
        return t('op.helper.audit.opUpdate');
      }

      return action
        ? t('op.helper.audit.opPlatform')
        : t('op.helper.audit.record');
  }
}

export function auditActorLabel(record: Record<string, unknown>, backend: OperatorBackendContext | null, t: TFunc): string {
  const actorStaffUserId = readString(record, 'actorStaffUserId');
  if (!actorStaffUserId) {
    return t('op.helper.audit.system');
  }

  if (backend?.session.staffUserId.toLowerCase() === actorStaffUserId.toLowerCase()) {
    return operatorDisplayNameLabel(backend.session.displayName, t);
  }

  return t('op.helper.audit.staff');
}
