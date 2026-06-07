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

export function pluralRu(value: number, forms: [string, string, string]) {
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

export function parseMoney(value: string) {
  const parsed = Number(value.replace(/[^\d-]/g, ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

export function triggerFeedback(
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

export function feedbackText(feedback: Feedback) {
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

export const defaultSessionDurationMinutes = 60;
export const defaultTariffRuleVersionId = 'manual-v1';
export const shellOperationalRefreshMs = 30_000;
export const billingModeOptions: Array<{ id: SessionBillingModeId; label: string; detail: string }> = [
  { id: 'guest', label: 'Гость', detail: 'без списания' },
  { id: 'prepaid_wallet', label: 'Депозит', detail: 'списать с баланса' },
  { id: 'package', label: 'Пакет', detail: 'списать минуты' },
  { id: 'postpaid_debt', label: 'Постоплата', detail: 'долг игрока' }
];
export const mapFilterOptions: Array<{ id: MapFilterId; label: string }> = [
  { id: 'all', label: 'Все' },
  { id: 'ready', label: 'Свободно' },
  { id: 'active', label: 'Сессии' },
  { id: 'attention', label: 'Проблемы' },
  { id: 'offline', label: 'Нет связи' }
];

export const toneLabels: Record<SeatTone, string> = {
  ready: 'Готов',
  active: 'Активно',
  pending: 'Команда',
  warning: 'Внимание',
  blocking: 'Блокер',
  offline: 'Офлайн',
  service: 'Сервис'
};

export const problemTones = new Set<SeatTone>(['pending', 'warning', 'blocking', 'offline', 'service']);
export const emptyFeedback: Feedback = { label: '', state: 'idle' };
export const pcControlLabel = 'Управление ПК';
export const pcControlTitle = 'Команды для выбранного ПК: статус, блокировка, питание и сервисный доступ';

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

export function billingLabel(value: string) {
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

export function zoneLabel(zone: string): string {
  return zone
    .replace('Console Lounge', 'Консольная зона')
    .replace('Main Hall', 'Основной зал')
    .replace('VIP Room', 'VIP-зал')
    .replace('Bootcamp', 'Буткемп');
}

export function appVersionLabel(app: string): string {
  return app
    .replaceAll('Agent', 'Агент')
    .replaceAll('Shell', 'Оболочка');
}

export function operatorDisplayNameLabel(displayName: string | null | undefined): string {
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

export function staffRoleLabel(roleName: string): string {
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

export function updateComponentLabel(component: string): string {
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

export function updateChannelLabel(channel: string): string {
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

export function updateTargetKindLabel(kind: string): string {
  switch (kind) {
    case 'branch':
      return 'Филиал';
    case 'device':
      return 'Отдельные ПК';
    default:
      return 'Цель';
  }
}

export function updatePackageStateLabel(state: string): string {
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

export function updateRolloutStateLabel(state: string): string {
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

export function updateDeviceStatusLabel(status: string): string {
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

export function stockMovementTypeLabel(type: string): string {
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

export function updateDeviceMessageLabel(message: string): string {
  if (message === 'target reached') {
    return 'цель достигнута';
  }

  if (message === 'installed') {
    return 'установлено';
  }

  return commandStatusMessageLabel(message);
}

export function commandLabel(command: string) {
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

export function mapSeatStatus(seat: SeatSummary) {
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

export function floorMapLoadLabel(status: FloorMapLoadStatus, source: OperatorFloorMapState['source'], error: string | null) {
  if (status === 'loading') {
    return source === 'backend' ? 'Обновляем карту' : 'Загружаем карту';
  }

  if (status === 'failed') {
    return error ? `Ошибка платформы · ${error}` : 'Ошибка платформы · API недоступен';
  }

  return source === 'backend' ? 'Платформа подключена' : 'Локальные данные';
}

export function workspaceLoadStatusLabel(status: LoadStatus, backendLabel: string): string {
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

export function dataSourceLabel(source: string): string {
  return source === 'backend' ? 'Платформа подключена' : 'Локальные данные';
}

export function shellShiftLabel(
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

export function shellPosLabel(summary: OperatorDashboardSummaryDto | null, status: LoadStatus): string {
  if (summary !== null) {
    const revenue = readRecord(summary, 'revenue');
    const posChecks = readNumber(revenue, 'posCheckCount', 0);
    return `Касса: ${posChecks} ${pluralRu(posChecks, ['чек', 'чека', 'чеков'])} сегодня`;
  }

  return status === 'loading' ? 'Касса: загрузка' : 'Касса: нет данных';
}

export function shellModeLabel(mode: string): string {
  if (mode.includes('dev')) {
    return 'локальная сборка';
  }

  if (mode.includes('dist')) {
    return 'установленная сборка';
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

export function projectOperatorFacingError(error: unknown): string {
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

export function realtimeLabel(state: OperatorRealtimeConnectionState, error: string | null): string {
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

export function posSaleStateLabel(state: string): string {
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

export function posReceiptTypeLabel(type: string): string {
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

export function posSaleLineSummary(row: unknown): string {
  const lineCount = readNumber(row, 'lineCount', 0);
  const itemQuantity = readNumber(row, 'itemQuantity', 0);
  return `${lineCount} ${pluralRu(lineCount, ['строка', 'строки', 'строк'])} · ${itemQuantity} шт.`;
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

export function cashOperationTypeLabel(type: string): string {
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

export function paymentSourceLabel(source: string): string {
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

export function buildPosReceiptText(sale: PosSaleDto, receipt: Record<string, unknown> | null, currencyCode: string): string {
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
    'AFK4.NET Касса',
    `Чек: ${receiptNumber}`,
    `Тип: ${receiptType}`,
    `Создан: ${createdAtUtc || '—'}`,
    '',
    ...lines,
    '',
    `Итого: ${formatMoney(readMoney(sale, 'total'), currencyCode)}`
  ].join('\n');
}

export function requireBackend(backend: OperatorBackendContext | null): OperatorBackendContext {
  if (backend === null) {
    throw new Error('Сессия оператора недоступна.');
  }

  return backend;
}

export function billingModeLabel(mode: SessionBillingModeId) {
  return billingModeOptions.find((option) => option.id === mode)?.label ?? mode;
}

export function tariffOptionLabel(tariff: Record<string, unknown>, currencyCode: string) {
  const name = readString(tariff, 'name', 'Тариф');
  const price = readNumber(tariff, 'pricePerMinuteMinorUnits', 0);
  const currency = readString(tariff, 'currencyCode', currencyCode);
  return `${name} · ${formatMinorUnits(price, currency)}/мин`;
}

export function playerPackageLabel(playerPackage: Record<string, unknown>) {
  const remainingSeconds = readNumber(playerPackage, 'remainingIncludedSeconds', 0) +
    readNumber(playerPackage, 'remainingBonusSeconds', 0);
  return `${readString(playerPackage, 'name', 'Пакет')} · ${Math.floor(remainingSeconds / 60)} мин`;
}

export function packageOptionLabel(packageOption: Record<string, unknown>, currencyCode: string) {
  const name = readString(packageOption, 'name', 'Пакет');
  const price = readNumber(packageOption, 'priceMinorUnits', 0);
  const currency = readString(packageOption, 'currencyCode', currencyCode);
  const totalMinutes = Math.floor((readNumber(packageOption, 'includedSeconds', 0) + readNumber(packageOption, 'bonusSeconds', 0)) / 60);
  return `${name} · ${formatMinorUnits(price, currency)} · ${totalMinutes} мин`;
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

export function describeSessionCommandFallback(response: unknown) {
  const commands = readArray<Record<string, unknown>>(response, 'deviceCommands');
  if (commands.length === 0) {
    return 'Платформа подтвердила действие';
  }

  const command = commands[0];
  return `${commandTypeLabel(readString(command, 'type', 'command'))}: отправлена на ПК`;
}

export function commandTypeLabel(type: string): string {
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

export function commandStatusLabel(status: string): string {
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

export async function describeDispatchedDeviceCommand(
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

export function fixturePlayers(currencyCode: string): PlayerClientItem[] {
  return [
    { name: 'Madina S.', status: 'VIP', balanceMinorUnits: 46000, debtMinorUnits: 0, last: 'пример', tone: 'vip', detail: 'локальная карточка', phoneNumber: '+992 90 555 22 11', source: 'fixture' },
    { name: 'Amir K.', status: 'Активен', balanceMinorUnits: 12000, debtMinorUnits: 0, last: 'пример', tone: 'active', detail: `120 ${currencyCode}`, phoneNumber: '', source: 'fixture' },
    { name: 'Olim K.', status: 'Долг', balanceMinorUnits: 0, debtMinorUnits: 3500, last: 'пример', tone: 'debt', detail: 'долг после сеанса', phoneNumber: '', source: 'fixture' }
  ];
}

export function projectPlayerClient(player: unknown): PlayerClientItem {
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

export function isGuid(value: string): boolean {
  return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value.trim());
}

export function auditActionLabel(action: string): string {
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

export function auditActorLabel(record: Record<string, unknown>, backend: OperatorBackendContext | null): string {
  const actorStaffUserId = readString(record, 'actorStaffUserId');
  if (!actorStaffUserId) {
    return 'Система';
  }

  if (backend?.session.staffUserId.toLowerCase() === actorStaffUserId.toLowerCase()) {
    return operatorDisplayNameLabel(backend.session.displayName);
  }

  return 'Сотрудник';
}
