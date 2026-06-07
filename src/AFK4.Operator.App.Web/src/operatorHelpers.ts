import { minorToMajor, majorToMinor } from '@afk4/money';
import { formatNumber as formatLocaleNumber, formatDateParts } from '@afk4/formatting';
import { getOperatorConfig } from './operatorConfig';
import { projectOperatorError } from './apiErrors';
import { createOperatorApiClients, type BranchDiagnosticsDto, type OperatorDashboardSummaryDto, type PosSaleDto, type ShiftDto } from './operatorApiClients';
import { PlatformApiClient, PlatformApiError } from './platformApi';
import { isHostBridgeUnavailableError } from './hostBridge';
import { signOutOperator, type OperatorAuthSession } from './authClient';
import { mapFloorMapDtoToState, type FloorMapLoadStatus, type OperatorFloorMapState } from './floorMapState';
import { saveFloorMapCache } from './floorMapCache';
import { hasPermission, permissionNames } from './operatorPermissions';
import type { DeviceCommandResultDto, DeviceStatusChangedDto, OperatorRealtimeConnectionState } from './operatorRealtime';
import type { SeatSummary, SeatTone } from './operatorData';
import type {
  Feedback,
  FeedbackState,
  LoadStatus,
  MapFilterId,
  OperatorBackendContext,
  OperatorConfig,
  SessionBillingModeId
} from './operatorTypes';
import type { MessageKey } from '@afk4/i18n';

type TFunc = (key: MessageKey, values?: Record<string, string | number>) => string;

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

export function feedbackText(feedback: Feedback, t?: TFunc) {
  if (feedback.state === 'pending') {
    return t
      ? t('op.helper.feedback.pending', { label: feedback.label })
      : `${feedback.label}: ждём подтверждение платформы`;
  }

  if (feedback.state === 'failed') {
    if (feedback.detail) {
      return feedback.detail;
    }

    return t
      ? t('op.helper.feedback.failed', { label: feedback.label })
      : `${feedback.label}: нужен повтор или проверка`;
  }

  if (feedback.state === 'confirmed') {
    if (feedback.detail) {
      return `${feedback.label}: ${feedback.detail}`;
    }

    return t
      ? t('op.helper.feedback.confirmed', { label: feedback.label })
      : `${feedback.label}: подтверждено`;
  }

  return '';
}

export const defaultSessionDurationMinutes = 60;
export const defaultTariffRuleVersionId = 'manual-v1';
export const shellOperationalRefreshMs = 30_000;

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
    { id: 'attention', label: t('op.helper.zone.filter.attention') },
    { id: 'offline', label: t('op.helper.zone.filter.offline') }
  ];
}

export function toneLabel(tone: SeatTone, t: TFunc): string {
  switch (tone) {
    case 'ready': return t('op.helper.tone.ready');
    case 'active': return t('op.helper.tone.active');
    case 'pending': return t('op.helper.tone.pending');
    case 'warning': return t('op.helper.tone.warning');
    case 'blocking': return t('op.helper.tone.blocking');
    case 'offline': return t('op.helper.tone.offline');
    case 'service': return t('op.helper.tone.service');
    default: return tone;
  }
}

export const problemTones = new Set<SeatTone>(['pending', 'warning', 'blocking', 'offline', 'service']);
export const emptyFeedback: Feedback = { label: '', state: 'idle' };

export function countByTone(nextSeats: SeatSummary[], tone: SeatTone): number {
  return nextSeats.filter((seat) => seat.tone === tone).length;
}

export function countProblems(nextSeats: SeatSummary[]): number {
  return nextSeats.filter((seat) => problemTones.has(seat.tone)).length;
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
    return seat.tone === 'active' || seat.hasActiveSession === true || Boolean(seat.activeSessionId);
  }

  if (filterId === 'attention') {
    return problemTones.has(seat.tone);
  }

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

export function billingLabel(value: string, t?: TFunc) {
  const normalized = value.toLowerCase();

  if (normalized.includes('wallet')) {
    return t ? t('op.helper.billing.deposit') : 'Депозит';
  }

  if (normalized.includes('package')) {
    return t ? t('op.helper.billing.package') : 'Пакет';
  }

  if (normalized.includes('postpaid') || normalized.includes('постоплата')) {
    return t ? t('op.helper.billing.postpaidLabel') : 'Постоплата';
  }

  if (normalized.includes('guest')) {
    return t ? t('op.helper.billing.guestLabel') : 'Гость';
  }

  return t ? t('op.helper.billing.notSet') : 'Не задан';
}

export function zoneLabel(zone: string, t?: TFunc): string {
  return zone
    .replace('Console Lounge', t ? t('op.helper.zone.consoleLounge') : 'Консольная зона')
    .replace('Main Hall', t ? t('op.helper.zone.mainHall') : 'Основной зал')
    .replace('VIP Room', t ? t('op.helper.zone.vipRoom') : 'VIP-зал')
    .replace('Bootcamp', t ? t('op.helper.zone.bootcamp') : 'Буткемп');
}

export function appVersionLabel(app: string): string {
  // Agent/Shell are loanword replacements — no t() needed, they stay in all locales
  return app
    .replaceAll('Agent', 'Агент')
    .replaceAll('Shell', 'Оболочка');
}

export function operatorDisplayNameLabel(displayName: string | null | undefined, t?: TFunc): string {
  const normalized = displayName?.trim();

  if (!normalized) {
    return t ? t('op.helper.staff.shiftOperator') : 'Оператор смены';
  }

  if (/^cashier one$/i.test(normalized)) {
    return t ? t('op.helper.staff.shiftOperator') : 'Оператор смены';
  }

  if (/^demo owner$/i.test(normalized)) {
    return t ? t('op.helper.staff.clubAdmin') : 'Администратор клуба';
  }

  if (/^local branch manager$/i.test(normalized)) {
    return t ? t('op.helper.staff.branchMgr') : 'Менеджер филиала';
  }

  return normalized;
}

export function staffRoleLabel(roleName: string, t?: TFunc): string {
  switch (roleName) {
    case 'cashier_operator':
      return t ? t('op.helper.staff.cashierOperator') : 'Кассир-оператор';
    case 'shift_supervisor':
      return t ? t('op.helper.staff.shiftSupervisor') : 'Старший смены';
    case 'branch_manager':
      return t ? t('op.helper.staff.branchManager') : 'Управляющий';
    case 'technician':
      return t ? t('op.helper.staff.technician') : 'Техник';
    case 'accountant_auditor':
      return t ? t('op.helper.staff.accountant') : 'Бухгалтер';
    default:
      return roleName || (t ? t('op.helper.staff.roleNotSet') : 'Роль не задана');
  }
}

export function updateComponentLabel(component: string, t?: TFunc): string {
  switch (component) {
    case 'operator-app':
      return t ? t('op.helper.update.component.operatorApp') : 'Приложение оператора';
    case 'agent-service':
      return t ? t('op.helper.update.component.agentService') : 'Сервис агента';
    case 'player-shell':
      return t ? t('op.helper.update.component.playerShell') : 'Оболочка игрока';
    default:
      return t ? t('op.helper.update.component.fallback') : 'Приложение';
  }
}

export function updateChannelLabel(channel: string, t?: TFunc): string {
  switch (channel) {
    case 'internal':
      return t ? t('op.helper.update.channel.internal') : 'Внутренний';
    case 'beta':
      return t ? t('op.helper.update.channel.beta') : 'Бета';
    case 'stable':
      return t ? t('op.helper.update.channel.stable') : 'Стабильный';
    default:
      return t ? t('op.helper.update.channel.fallback') : 'Канал';
  }
}

export function updateTargetKindLabel(kind: string, t?: TFunc): string {
  switch (kind) {
    case 'branch':
      return t ? t('op.helper.update.target.branch') : 'Филиал';
    case 'device':
      return t ? t('op.helper.update.target.device') : 'Отдельные ПК';
    default:
      return t ? t('op.helper.update.target.fallback') : 'Цель';
  }
}

export function updatePackageStateLabel(state: string, t?: TFunc): string {
  switch (state) {
    case 'registered':
      return t ? t('op.helper.update.pkgState.registered') : 'Зарегистрирован';
    case 'validated':
      return t ? t('op.helper.update.pkgState.validated') : 'Проверен';
    case 'rejected':
      return t ? t('op.helper.update.pkgState.rejected') : 'Отклонён';
    case 'retired':
      return t ? t('op.helper.update.pkgState.retired') : 'Выведен';
    default:
      return t ? t('op.helper.update.pkgState.fallback') : 'Состояние';
  }
}

export function updateRolloutStateLabel(state: string, t?: TFunc): string {
  switch (state) {
    case 'active':
      return t ? t('op.helper.update.rollout.active') : 'Активна';
    case 'paused':
      return t ? t('op.helper.update.rollout.paused') : 'Пауза';
    case 'completed':
      return t ? t('op.helper.update.rollout.completed') : 'Завершена';
    case 'rollback_requested':
      return t ? t('op.helper.update.rollout.rollbackRequested') : 'Запрошен откат';
    case 'rolled_back':
      return t ? t('op.helper.update.rollout.rolledBack') : 'Откат выполнен';
    case 'cancelled':
      return t ? t('op.helper.update.rollout.cancelled') : 'Отменена';
    default:
      return t ? t('op.helper.update.rollout.fallback') : 'Состояние';
  }
}

export function updateDeviceStatusLabel(status: string, t?: TFunc): string {
  switch (status) {
    case 'installed':
      return t ? t('op.helper.update.deviceStatus.installed') : 'Установлено';
    case 'target reached':
      return t ? t('op.helper.update.deviceStatus.reached') : 'Цель достигнута';
    case 'pending':
      return t ? t('op.helper.update.deviceStatus.pending') : 'Ожидает';
    case 'failed':
      return t ? t('op.helper.update.deviceStatus.failed') : 'Ошибка';
    default:
      return t ? t('op.helper.update.deviceStatus.unknown') : 'Неизвестно';
  }
}

export function stockMovementTypeLabel(type: string, t?: TFunc): string {
  switch (type) {
    case 'purchase':
      return t ? t('op.helper.stock.purchase') : 'Приход';
    case 'adjustment':
      return t ? t('op.helper.stock.adjustment') : 'Коррекция';
    case 'sale':
      return t ? t('op.helper.stock.sale') : 'Продажа';
    case 'write_off':
      return t ? t('op.helper.stock.writeOff') : 'Списание';
    default:
      return t ? t('op.helper.stock.fallback') : 'Движение';
  }
}

export function updateDeviceMessageLabel(message: string): string {
  if (message === 'target reached') {
    return 'цель достигнута';
  }

  if (message === 'installed') {
    return 'установлено';
  }

  return commandStatusMessageLabel(message);
}

export function commandLabel(command: string, t?: TFunc) {
  if (command.includes('Lease fresh')) {
    return t ? t('op.helper.command.leased') : 'Сессия подтверждена';
  }

  if (command.includes('Unlock pending')) {
    return t ? t('op.helper.command.unlockPending') : 'Разблокировка в процессе';
  }

  if (command.includes('Start pending')) {
    return t ? t('op.helper.command.startPending') : 'Запуск в процессе';
  }

  if (command.includes('Stop pending')) {
    return t ? t('op.helper.command.stopPending') : 'Завершение в процессе';
  }

  if (command.includes('Payment check')) {
    return t ? t('op.helper.command.paymentCheck') : 'Проверить оплату';
  }

  if (command.includes('No route')) {
    return t ? t('op.helper.command.noRoute') : 'Нет связи с ПК';
  }

  if (command.includes('Idle')) {
    return t ? t('op.helper.command.idle') : 'Команд нет';
  }

  if (command.includes('Command failed')) {
    return t ? t('op.helper.command.failed') : 'Команда не выполнена';
  }

  if (command.includes('Technician')) {
    return t ? t('op.helper.command.tech') : 'Техобслуживание';
  }

  if (command.includes('Low balance')) {
    return t ? t('op.helper.command.lowBalance') : 'Мало средств';
  }

  return command;
}

export function deviceStatusLabel(device: string) {
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

export function mapSeatStatus(seat: SeatSummary, t?: TFunc) {
  if (seat.tone === 'active') {
    return {
      label: t ? t('op.helper.map.sessionActive') : 'Сессия активна',
      value: seat.remaining
    };
  }

  if (seat.tone === 'ready') {
    return {
      label: t ? t('op.helper.map.ready') : 'Свободен',
      value: t ? t('op.helper.map.readyValue') : 'готов к старту'
    };
  }

  if (seat.tone === 'pending') {
    return {
      label: t ? t('op.helper.map.commandEnRoute') : 'Команда в пути',
      value: commandLabel(seat.command, t)
    };
  }

  if (seat.tone === 'warning') {
    return {
      label: t ? t('op.helper.map.needsCheck') : 'Требует проверки',
      value: commandLabel(seat.command, t)
    };
  }

  if (seat.tone === 'offline') {
    return {
      label: t ? t('op.helper.map.noSignal') : 'Нет связи',
      value: commandLabel(seat.command, t)
    };
  }

  return {
    label: t ? t('op.helper.map.service') : 'Сервис',
    value: commandLabel(seat.command, t)
  };
}

export function floorMapLoadLabel(status: FloorMapLoadStatus, source: OperatorFloorMapState['source'], error: string | null, t?: TFunc) {
  if (status === 'loading') {
    return source === 'backend'
      ? (t ? t('op.helper.shift.mapUpdating') : 'Обновляем карту')
      : (t ? t('op.helper.shift.mapLoading') : 'Загружаем карту');
  }

  if (status === 'failed') {
    return error
      ? (t ? t('op.helper.shift.platformError', { error }) : `Ошибка платформы · ${error}`)
      : (t ? t('op.helper.shift.platformErrorApi') : 'Ошибка платформы · API недоступен');
  }

  return source === 'backend'
    ? (t ? t('op.helper.shift.platformConnected') : 'Платформа подключена')
    : (t ? t('op.helper.shift.localData') : 'Локальные данные');
}

export function workspaceLoadStatusLabel(status: LoadStatus, backendLabel: string, t?: TFunc): string {
  if (status === 'backend') {
    return backendLabel;
  }

  if (status === 'loading') {
    return t ? t('op.helper.shift.dataLoading') : 'Загружаем данные';
  }

  if (status === 'failed') {
    return t ? t('op.helper.shift.loadError') : 'Ошибка платформы';
  }

  return t ? t('op.helper.shift.localData') : 'Локальные данные';
}

export function dataSourceLabel(source: string, t?: TFunc): string {
  return source === 'backend'
    ? (t ? t('op.helper.shift.platformConnected') : 'Платформа подключена')
    : (t ? t('op.helper.shift.localData') : 'Локальные данные');
}

export function shellShiftLabel(
  shift: ShiftDto | null,
  summary: OperatorDashboardSummaryDto | null,
  status: LoadStatus,
  error: string | null,
  t?: TFunc
): string {
  const shiftSource = shift ?? readRecord(summary, 'shift');
  if (shiftSource !== null) {
    const state = readString(shiftSource, 'state').toLowerCase();
    if (state === 'open') {
      return t
        ? t('op.helper.shift.open', { time: formatTime(readString(shiftSource, 'openedAtUtc')) })
        : `Смена открыта · с ${formatTime(readString(shiftSource, 'openedAtUtc'))}`;
    }

    if (state === 'closed') {
      return t
        ? t('op.helper.shift.closed', { time: formatTime(readString(shiftSource, 'closedAtUtc')) })
        : `Смена закрыта · ${formatTime(readString(shiftSource, 'closedAtUtc'))}`;
    }

    const stateStr = state || (t ? t('op.helper.shift.stateUnknown') : 'состояние неизвестно');
    return t ? t('op.helper.shift.state', { state: stateStr }) : `Смена · ${stateStr}`;
  }

  if (status === 'loading') {
    return t ? t('op.helper.shift.loading') : 'Смена · загрузка';
  }

  if (status === 'failed') {
    return error
      ? (t ? t('op.helper.shift.error') : 'Смена · ошибка платформы')
      : (t ? t('op.helper.shift.noAccess') : 'Смена · нет доступа');
  }

  return t ? t('op.helper.shift.notOpen') : 'Смена не открыта';
}

export function shellPosLabel(summary: OperatorDashboardSummaryDto | null, status: LoadStatus, t?: TFunc): string {
  if (summary !== null) {
    const revenue = readRecord(summary, 'revenue');
    const posChecks = readNumber(revenue, 'posCheckCount', 0);
    return t
      ? t('op.helper.shell.posChecks', { count: posChecks })
      : `Касса: ${posChecks} чеков сегодня`;
  }

  if (status === 'loading') {
    return t ? t('op.helper.shell.posLoading') : 'Касса: загрузка';
  }

  return t ? t('op.helper.shell.posNoData') : 'Касса: нет данных';
}

export function shellModeLabel(mode: string, t?: TFunc): string {
  if (mode.includes('dev')) {
    return t ? t('op.helper.shell.modeDev') : 'локальная сборка';
  }

  if (mode.includes('dist')) {
    return t ? t('op.helper.shell.modeDist') : 'установленная сборка';
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

export function projectAuthHostError(error: unknown, config: OperatorConfig, t?: TFunc): string {
  if (isHostBridgeUnavailableError(error) && config.runtime !== 'browser-dev') {
    return t
      ? t('op.shell.err.nativeAuthUnavailable')
      : 'Нативный вход приложения оператора недоступен. Перезапустите приложение или проверьте WebView2.';
  }

  return projectOperatorError(error).detail;
}

export function projectOperatorFacingError(error: unknown, t?: TFunc): string {
  const detail = projectOperatorError(error).detail;
  const platformDetail = extractPlatformError(detail);

  if (detail.includes('Only active or paused sessions can be ended') ||
    platformDetail?.includes('Only active or paused sessions can be ended')) {
    return t ? t('op.helper.error.sessionEnded') : 'Сеанс уже завершается или завершён. Дождитесь обновления карты.';
  }

  if (detail.includes('401 Unauthorized')) {
    return t ? t('op.helper.error.sessionExpired') : 'Сессия оператора устарела. Войдите снова.';
  }

  if (platformDetail) {
    return platformDetail;
  }

  const badRequest = t ? t('op.helper.error.badRequest') : 'Платформа отклонила запрос:';
  const serverError = t ? t('op.helper.error.serverError') : 'Платформа временно недоступна:';
  const sessionExpired = t ? t('op.helper.error.sessionExpired') : 'Сессия оператора устарела.';

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

export function realtimeLabel(state: OperatorRealtimeConnectionState, error: string | null, t?: TFunc): string {
  if (state === 'connected') {
    return t ? t('op.helper.realtime.connected') : 'Связь подключена';
  }

  if (state === 'connecting') {
    return t ? t('op.helper.realtime.connecting') : 'Связь устанавливается';
  }

  if (state === 'reconnecting') {
    return t ? t('op.helper.realtime.reconnecting') : 'Связь восстанавливается';
  }

  return error
    ? (t ? t('op.helper.realtime.lost') : 'Связь потеряна')
    : (t ? t('op.helper.realtime.disconnected') : 'Связь отключена');
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
  branchId: string
): Promise<OperatorFloorMapState> {
  const clients = createAuthenticatedOperatorClients(config, session);
  const floorMap = await clients.floorMap.getFloorMap(branchId);
  // Persist the last-known-good snapshot so the workspace can degrade to a read-only mirror offline (§6.5).
  saveFloorMapCache(branchId, floorMap, Date.now());
  return mapFloorMapDtoToState(floorMap);
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

export function formatMinorUnits(minorUnits: number, currencyCode: string): string {
  const majorUnits = minorToMajor(minorUnits);
  const formatted = formatLocaleNumber(majorUnits, 'ru-RU', {
    maximumFractionDigits: Number.isInteger(majorUnits) ? 0 : 2,
    minimumFractionDigits: 0
  });

  return `${formatted} ${currencyCode}`;
}

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

export function posSaleStateLabel(state: string, t?: TFunc): string {
  switch (state.toLowerCase()) {
    case 'paid':
      return t ? t('op.helper.pos.saleState.paid') : 'Оплачен';
    case 'draft':
      return t ? t('op.helper.pos.saleState.draft') : 'Черновик';
    case 'void':
    case 'voided':
      return t ? t('op.helper.pos.saleState.voided') : 'Аннулирован';
    case 'refund':
    case 'refunded':
      return t ? t('op.helper.pos.saleState.refund') : 'Возврат';
    case 'pending':
      return t ? t('op.helper.pos.saleState.pending') : 'Ожидает оплаты';
    case 'sale':
      return t ? t('op.helper.pos.saleState.sale') : 'Продажа';
    default:
      return t ? t('op.helper.pos.saleState.fallback') : 'Чек';
  }
}

export function posReceiptTypeLabel(type: string, t?: TFunc): string {
  switch (type.toLowerCase()) {
    case 'sale':
    case 'paid':
      return t ? t('op.helper.pos.receiptType.sale') : 'Продажа';
    case 'refund':
    case 'refunded':
      return t ? t('op.helper.pos.receiptType.refund') : 'Возврат';
    case 'void':
    case 'voided':
      return t ? t('op.helper.pos.receiptType.void') : 'Аннулирование';
    default:
      return t ? t('op.helper.pos.receiptType.fallback') : 'Чек';
  }
}

export function posSaleLineSummary(row: unknown, t?: TFunc): string {
  const lineCount = readNumber(row, 'lineCount', 0);
  const itemQuantity = readNumber(row, 'itemQuantity', 0);
  return t
    ? t('op.helper.pos.lineSummary', { lines: lineCount, qty: itemQuantity })
    : `${lineCount} строк · ${itemQuantity} шт.`;
}

export function shiftStateLabel(state: string, t?: TFunc): string {
  if (!t) {
    // Fallback for callers (e.g. export JSON) that don't pass t
    switch (state.toLowerCase()) {
      case 'open': return 'Открыта';
      case 'closed': return 'Закрыта';
      case 'closing': return 'Закрывается';
      case 'unknown': return 'Неизвестно';
      case 'нет смены': return 'Нет';
      default: return state ? 'Неизвестно' : 'Нет';
    }
  }

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

export function cashOperationTypeLabel(type: string, t?: TFunc): string {
  switch (type.toLowerCase()) {
    case 'opening':
      return t ? t('op.helper.billing.cashOp.opening') : 'Открытие смены';
    case 'closing':
      return t ? t('op.helper.billing.cashOp.closing') : 'Закрытие смены';
    case 'cash_in':
      return t ? t('op.helper.billing.cashOp.cashIn') : 'Внесение';
    case 'cash_out':
      return t ? t('op.helper.billing.cashOp.cashOut') : 'Изъятие';
    case 'refund':
      return t ? t('op.helper.billing.cashOp.refund') : 'Возврат';
    default:
      return t ? t('op.helper.billing.cashOp.fallback') : 'Движение кассы';
  }
}

export function paymentSourceLabel(source: string, t?: TFunc): string {
  switch (source.toLowerCase()) {
    case 'shift':
      return t ? t('op.helper.billing.paySource.shift') : 'Смена';
    case 'cash':
      return t ? t('op.helper.billing.paySource.cash') : 'Наличные';
    case 'pos':
    case 'sale':
      return t ? t('op.helper.billing.paySource.sale') : 'Продажа';
    default:
      return t ? t('op.helper.billing.paySource.fallback') : 'Касса';
  }
}

export function buildPosReceiptText(sale: PosSaleDto, receipt: Record<string, unknown> | null, currencyCode: string, t?: TFunc): string {
  const receiptNumber = readString(receipt, 'receiptNumber', 'чек');
  const receiptType = posReceiptTypeLabel(readString(receipt, 'receiptType', readString(sale, 'state', 'sale')), t);
  const createdAtUtc = readString(receipt, 'createdAtUtc', readString(sale, 'createdAtUtc'));
  const productFallback = t ? t('op.helper.pos.productFallback') : 'Товар';
  const unitSuffix = t ? t('op.helper.pos.unitSuffix') : 'шт.';
  const lines = readArray(sale, 'lines').map((line) => [
    readString(line, 'productName', productFallback),
    `${readNumber(line, 'quantity', 0)} ${unitSuffix}`,
    formatMoney(readMoney(line, 'unitPrice'), currencyCode),
    formatMoney(readMoney(line, 'lineTotal'), currencyCode)
  ].join(' | '));

  const header = t ? t('op.helper.pos.receiptHeader') : 'AFK4.NET Касса';
  const numLine = t ? t('op.helper.pos.receiptNumber', { number: receiptNumber }) : `Чек: ${receiptNumber}`;
  const typeLine = t ? t('op.helper.pos.receiptType', { type: receiptType }) : `Тип: ${receiptType}`;
  const createdLine = t ? t('op.helper.pos.receiptCreated', { date: createdAtUtc || '—' }) : `Создан: ${createdAtUtc || '—'}`;
  const totalLine = t
    ? t('op.helper.pos.receiptTotal', { total: formatMoney(readMoney(sale, 'total'), currencyCode) })
    : `Итого: ${formatMoney(readMoney(sale, 'total'), currencyCode)}`;

  return [header, numLine, typeLine, createdLine, '', ...lines, '', totalLine].join('\n');
}

export function requireBackend(backend: OperatorBackendContext | null, t?: TFunc): OperatorBackendContext {
  if (backend === null) {
    throw new Error(t ? t('op.helper.error.sessionUnavailable') : 'Сессия оператора недоступна.');
  }

  return backend;
}

export function billingModeLabel(mode: SessionBillingModeId, t?: TFunc) {
  return billingModeOptions(t ?? ((k: string) => k)).find((option) => option.id === mode)?.label ?? mode;
}

export function tariffOptionLabel(tariff: Record<string, unknown>, currencyCode: string, t?: TFunc) {
  const name = readString(tariff, 'name', t ? t('op.helper.player.tariffFallback') : 'Тариф');
  const price = readNumber(tariff, 'pricePerMinuteMinorUnits', 0);
  const currency = readString(tariff, 'currencyCode', currencyCode);
  return t
    ? t('op.helper.player.tariffPerMin', { name, price: formatMinorUnits(price, currency) })
    : `${name} · ${formatMinorUnits(price, currency)}/мин`;
}

export function playerPackageLabel(playerPackage: Record<string, unknown>, t?: TFunc) {
  const remainingSeconds = readNumber(playerPackage, 'remainingIncludedSeconds', 0) +
    readNumber(playerPackage, 'remainingBonusSeconds', 0);
  const name = readString(playerPackage, 'name', t ? t('op.helper.player.packageFallback') : 'Пакет');
  const minutes = Math.floor(remainingSeconds / 60);
  return t
    ? t('op.helper.player.packageLabel', { name, minutes })
    : `${name} · ${minutes} мин`;
}

export function packageOptionLabel(packageOption: Record<string, unknown>, currencyCode: string, t?: TFunc) {
  const name = readString(packageOption, 'name', t ? t('op.helper.player.packageFallback') : 'Пакет');
  const price = readNumber(packageOption, 'priceMinorUnits', 0);
  const currency = readString(packageOption, 'currencyCode', currencyCode);
  const totalMinutes = Math.floor((readNumber(packageOption, 'includedSeconds', 0) + readNumber(packageOption, 'bonusSeconds', 0)) / 60);
  return t
    ? t('op.helper.player.packageOptionLabel', { name, price: formatMinorUnits(price, currency), minutes: totalMinutes })
    : `${name} · ${formatMinorUnits(price, currency)} · ${totalMinutes} мин`;
}

export function describeDeviceCommandStatus(status: Record<string, unknown>) {
  const type = readString(status, 'type', 'command');
  const state = readString(status, 'status', 'pending');
  const message = readString(status, 'message');
  const typeLabel = commandTypeLabel(type);
  const stateLabel = commandStatusLabel(state);
  const messageLabel = commandStatusMessageLabel(message);
  return messageLabel ? `${typeLabel}: ${stateLabel} · ${messageLabel}` : `${typeLabel}: ${stateLabel}`;
}

export function describeSessionCommandFallback(response: unknown, t?: TFunc) {
  const commands = readArray<Record<string, unknown>>(response, 'deviceCommands');
  if (commands.length === 0) {
    return t ? t('op.helper.command.platformConfirmed') : 'Платформа подтвердила действие';
  }

  const command = commands[0];
  const sentLabel = t ? t('op.helper.command.sentToPc') : 'отправлена на ПК';
  return `${commandTypeLabel(readString(command, 'type', 'command'), t)}: ${sentLabel}`;
}

export function commandTypeLabel(type: string, t?: TFunc): string {
  switch (type.toLowerCase()) {
    case 'lock':
      return t ? t('op.helper.command.type.lock') : 'Блокировка';
    case 'unlock':
      return t ? t('op.helper.command.type.unlock') : 'Разблокировка';
    case 'transfer':
      return t ? t('op.helper.command.type.transfer') : 'Перенос';
    case 'reboot':
      return t ? t('op.helper.command.type.reboot') : 'Перезагрузка';
    case 'shutdown':
      return t ? t('op.helper.command.type.shutdown') : 'Выключение';
    case 'refresh-session-lease':
      return t ? t('op.helper.command.type.refreshSession') : 'Обновление сессии';
    default:
      return t ? t('op.helper.command.type.fallback') : 'Команда';
  }
}

export function commandStatusLabel(status: string, t?: TFunc): string {
  switch (status.toLowerCase()) {
    case 'pending':
      return t ? t('op.helper.command.status.pending') : 'ожидает выполнения';
    case 'sent':
    case 'accepted':
    case 'in_progress':
      return t ? t('op.helper.command.status.inProgress') : 'выполняется';
    case 'completed':
    case 'succeeded':
      return t ? t('op.helper.command.status.done') : 'выполнена';
    case 'failed':
      return t ? t('op.helper.command.status.failed') : 'не выполнена';
    case 'cancelled':
    case 'canceled':
      return t ? t('op.helper.command.status.cancelled') : 'отменена';
    default:
      return status;
  }
}

export function commandStatusMessageLabel(message: string): string {
  return message
    .replace('Agent accepted lock', 'ПК принял команду блокировки')
    .replace('Agent accepted unlock', 'ПК принял команду разблокировки')
    .replace('Agent accepted transfer', 'ПК принял команду переноса')
    .replace('Agent timeout', 'Агент не ответил')
    .replace('timeout waiting for Agent', 'Агент не ответил вовремя')
    .replace('Queued for Agent', 'Поставлено в очередь агента')
    .replace('Agent did not confirm lock.', 'Агент не подтвердил блокировку.');
}

export function dashboardFocusTextLabel(text: string, t: TFunc): string {
  return commandStatusMessageLabel(text)
    .replace('lock Failed', t('op.shell.focus.lockFailed'))
    .replace('unlock Failed', t('op.shell.focus.unlockFailed'))
    .replace('Failed', t('op.shell.focus.failed'))
    .replace('Agent', 'Агент');
}

export async function describeSeatActionResult(
  clients: ReturnType<typeof createAuthenticatedOperatorClients>,
  session: OperatorAuthSession,
  seat: SeatSummary,
  response: unknown,
  t?: TFunc
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
    return describeDeviceCommandStatus(await clients.devices.getDeviceCommandStatus(deviceId, commandId));
  } catch (error) {
    const statusUnavail = t
      ? t('op.helper.command.statusUnavailable', { detail: projectOperatorError(error).detail })
      : `статус недоступен: ${projectOperatorError(error).detail}`;
    return `${fallback} · ${statusUnavail}`;
  }
}

export async function describeDispatchedDeviceCommand(
  clients: ReturnType<typeof createAuthenticatedOperatorClients>,
  session: OperatorAuthSession,
  seat: SeatSummary,
  command: Record<string, unknown>,
  t?: TFunc
): Promise<string> {
  const commandId = readString(command, 'commandId');
  const deviceId = seat.deviceId || readString(command, 'deviceId');
  const sentLabel = t ? t('op.helper.command.sentToPc') : 'отправлена на ПК';
  const fallback = `${commandTypeLabel(readString(command, 'type', 'command'), t)}: ${sentLabel}`;
  if (!commandId || !deviceId || !hasPermission(session, permissionNames.viewDeviceCommandStatus)) {
    return fallback;
  }

  try {
    return describeDeviceCommandStatus(await clients.devices.getDeviceCommandStatus(deviceId, commandId));
  } catch (error) {
    const statusUnavail = t
      ? t('op.helper.command.statusUnavailable', { detail: projectOperatorError(error).detail })
      : `статус недоступен: ${projectOperatorError(error).detail}`;
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

export function fixturePlayers(currencyCode: string, t?: TFunc): PlayerClientItem[] {
  const example = t ? t('op.helper.player.fixture.example') : 'пример';
  return [
    { name: 'Madina S.', status: 'VIP', balanceMinorUnits: 46000, debtMinorUnits: 0, last: example, tone: 'vip', detail: t ? t('op.helper.player.fixture.localCard') : 'локальная карточка', phoneNumber: '+992 90 555 22 11', source: 'fixture' },
    { name: 'Amir K.', status: 'Активен', balanceMinorUnits: 12000, debtMinorUnits: 0, last: example, tone: 'active', detail: `120 ${currencyCode}`, phoneNumber: '', source: 'fixture' },
    { name: 'Olim K.', status: 'Долг', balanceMinorUnits: 0, debtMinorUnits: 3500, last: example, tone: 'debt', detail: t ? t('op.helper.player.fixture.debtDetail') : 'долг после сеанса', phoneNumber: '', source: 'fixture' }
  ];
}

export function projectPlayerClient(player: unknown, t?: TFunc): PlayerClientItem {
  const debt = readNumber(player, 'debtBalanceMinorUnits', 0);
  const packages = readNumber(player, 'activePackageCount', 0);
  const isActive = isRecord(player) && player.isActive !== false;
  const phone = readString(player, 'phoneNumber', t ? t('op.helper.player.noPhone') : 'без телефона');
  const lastLabel = packages > 0
    ? (t ? t('op.helper.player.packageCount', { count: packages }) : `${packages} пак.`)
    : (t ? t('op.helper.player.platform') : 'платформа');
  const detail = t
    ? t('op.helper.player.detail', { phone, packages })
    : `${phone} · ${packages} пакетов`;
  return {
    playerAccountId: readString(player, 'playerAccountId') || undefined,
    name: readString(player, 'displayName', t ? t('op.helper.player.nameFallback') : 'Игрок'),
    // Status values are sentinels compared in BackendPlayersWorkspace — keep raw Russian
    status: debt > 0 ? 'Долг' : packages > 0 ? 'Пакет' : isActive ? 'Активен' : 'Неактивен',
    balanceMinorUnits: readNumber(player, 'walletBalanceMinorUnits', 0),
    debtMinorUnits: debt,
    last: lastLabel,
    tone: debt > 0 ? 'debt' : packages > 0 ? 'vip' : isActive ? 'active' : 'regular',
    detail,
    phoneNumber: readString(player, 'phoneNumber', ''),
    source: 'backend'
  };
}

export function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim());
}

export function auditActionLabel(action: string, t?: TFunc): string {
  const normalized = action.toLowerCase();
  switch (normalized) {
    case 'pos.sale.create':
    case 'pos.sales.create':
      return t ? t('op.helper.audit.saleCreated') : 'Продажа создана';
    case 'pos.sale.refund':
    case 'pos.sales.refund':
      return t ? t('op.helper.audit.saleRefund') : 'Возврат по чеку';
    case 'pos.sale.void':
    case 'pos.sales.void':
      return t ? t('op.helper.audit.saleVoid') : 'Чек аннулирован';
    case 'sessions.start':
    case 'session.start':
      return t ? t('op.helper.audit.sessionStart') : 'Сессия запущена';
    case 'sessions.extend':
    case 'session.extend':
      return t ? t('op.helper.audit.sessionExtend') : 'Сессия продлена';
    case 'sessions.end':
    case 'session.end':
      return t ? t('op.helper.audit.sessionEnd') : 'Сессия завершена';
    case 'identity.staff.create':
      return t ? t('op.helper.audit.staffCreate') : 'Сотрудник добавлен';
    case 'identity.staff.roles.update':
      return t ? t('op.helper.audit.staffRolesUpdate') : 'Роли сотрудника изменены';
    case 'updates.rollouts.view':
      return t ? t('op.helper.audit.updatesView') : 'Проверка публикаций обновлений';
    case 'updates.rollouts.state.change':
      return t ? t('op.helper.audit.updatesStateChange') : 'Состояние публикации обновления изменено';
    default:
      if (normalized.includes('pos')) {
        return t ? t('op.helper.audit.opPos') : 'Операция кассы';
      }

      if (normalized.includes('session')) {
        return t ? t('op.helper.audit.opSession') : 'Операция сессии';
      }

      if (normalized.includes('device')) {
        return t ? t('op.helper.audit.opDevice') : 'Операция ПК';
      }

      if (normalized.includes('shift')) {
        return t ? t('op.helper.audit.opShift') : 'Операция смены';
      }

      if (normalized.includes('identity') || normalized.includes('staff')) {
        return t ? t('op.helper.audit.opStaff') : 'Операция сотрудника';
      }

      if (normalized.includes('update')) {
        return t ? t('op.helper.audit.opUpdate') : 'Операция обновления';
      }

      return action
        ? (t ? t('op.helper.audit.opPlatform') : 'Операция платформы')
        : (t ? t('op.helper.audit.record') : 'Запись аудита');
  }
}

export function auditActorLabel(record: Record<string, unknown>, backend: OperatorBackendContext | null, t?: TFunc): string {
  const actorStaffUserId = readString(record, 'actorStaffUserId');
  if (!actorStaffUserId) {
    return t ? t('op.helper.audit.system') : 'Система';
  }

  if (backend?.session.staffUserId.toLowerCase() === actorStaffUserId.toLowerCase()) {
    return operatorDisplayNameLabel(backend.session.displayName, t);
  }

  return t ? t('op.helper.audit.staff') : 'Сотрудник';
}
