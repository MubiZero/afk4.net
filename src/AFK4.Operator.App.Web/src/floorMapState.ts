import { minorToMajor } from '@afk4/money';
import { formatNumber as formatLocaleNumber } from '@afk4/formatting';
import { formatMinorUnits } from './currencyFormat';
import { type MessageKey } from '@afk4/i18n';
import type { FloorMapCacheEntry } from './floorMapCache';
import type { FloorMapDto, FloorMapZoneDto, FloorMapWallDto, SeatStatusDto } from './operatorApiClients';
import { seats as fixtureSeats, type SeatSummary, type SeatTone } from './operatorData';
import type { DeviceStatusChangedDto } from './operatorRealtime';

type TFn = (key: MessageKey, values?: Record<string, string | number>) => string;

export type FloorMapLoadStatus = 'idle' | 'loading' | 'ready' | 'failed';
export type FloorMapSource = 'fixture' | 'backend';

export interface OperatorFloorMapState {
  branchId?: string;
  branchName: string;
  seats: SeatSummary[];
  // Static layout geometry for the «План» view (B2). Realtime seat updates never touch these.
  zones: FloorMapZoneDto[];
  walls: FloorMapWallDto[];
  // Concurrency token from the floor-map GET; required as If-Match when saving layout (B2-3).
  // Null on fixtures and on the offline-cache mirror — the editor's save is disabled without it.
  etag: string | null;
  source: FloorMapSource;
  loadStatus: FloorMapLoadStatus;
  error: string | null;
  // Offline mirror (spec §6.5): degraded read-only flag and the timestamp of the snapshot on screen.
  isOffline: boolean;
  cachedAtMs: number | null;
}

export const fixtureBranchName = 'AFK4 Dushanbe';

export function createFixtureFloorMapState(): OperatorFloorMapState {
  return {
    branchName: fixtureBranchName,
    seats: fixtureSeats,
    zones: [],
    walls: [],
    etag: null,
    source: 'fixture',
    loadStatus: 'idle',
    error: null,
    isOffline: false,
    cachedAtMs: null
  };
}

export function mapFloorMapDtoToState(floorMap: FloorMapDto, t: TFn, etag: string | null, loadedAtMs = Date.now()): OperatorFloorMapState {
  return {
    branchId: floorMap.branchId,
    branchName: floorMap.branchName,
    seats: mapFloorMapSeats(floorMap.seats, t, loadedAtMs),
    zones: floorMap.zones ?? [],
    walls: floorMap.walls ?? [],
    etag,
    source: 'backend',
    loadStatus: 'ready',
    error: null,
    isOffline: false,
    cachedAtMs: loadedAtMs
  };
}

// Hydrate a degraded, read-only floor map from the last-known-good cache when the platform is unreachable
// (spec §6.5). Seats render from the cached snapshot; billing actions are gated off elsewhere (D2).
export function hydrateFloorMapStateFromCache(
  entry: FloorMapCacheEntry,
  branchId: string,
  t: TFn
): OperatorFloorMapState {
  return {
    branchId,
    branchName: entry.floorMap.branchName,
    seats: mapFloorMapSeats(entry.floorMap.seats, t, entry.cachedAtMs),
    zones: entry.floorMap.zones ?? [],
    walls: entry.floorMap.walls ?? [],
    etag: null,
    source: 'backend',
    loadStatus: 'ready',
    error: null,
    isOffline: true,
    cachedAtMs: entry.cachedAtMs
  };
}

// Баннер показываем ТОЛЬКО при настоящем обрыве связи: данные заморожены, режим «только
// просмотр» — это операционно важно. Просто устаревший снимок при живой связи админу не нужен
// (приложение тихо обновится) — раньше D8 показывал «снимок устарел», убрано как тех-шум.
export function offlineBannerText(state: OperatorFloorMapState, t: TFn): string | null {
  if (!state.isOffline || state.cachedAtMs === null) {
    return null;
  }

  const at = new Date(state.cachedAtMs);
  const hh = String(at.getHours()).padStart(2, '0');
  const mm = String(at.getMinutes()).padStart(2, '0');
  return t('op.floor.offlineBanner', { time: `${hh}:${mm}` });
}

export function mapFloorMapSeats(nextSeats: SeatStatusDto[], t: TFn, loadedAtMs = Date.now()): SeatSummary[] {
  return [...nextSeats]
    .sort((left, right) => left.sortOrder - right.sortOrder || left.seatName.localeCompare(right.seatName))
    .map((seat) => mapFloorMapSeat(seat, t, loadedAtMs));
}

export function refreshFloorMapRemaining(
  floorMap: OperatorFloorMapState,
  t: TFn,
  nowMs = Date.now()
): OperatorFloorMapState {
  let changed = false;
  const nextSeats = floorMap.seats.map((seat) => {
    const nextSeat = refreshSeatRemaining(seat, t, nowMs);
    if (nextSeat !== seat) {
      changed = true;
    }

    return nextSeat;
  });

  return changed
    ? { ...floorMap, seats: nextSeats }
    : floorMap;
}

export function applyDeviceStatusToSeats(
  currentSeats: SeatSummary[],
  status: DeviceStatusChangedDto,
  t: TFn
): SeatSummary[] {
  if (!isGamingPcStatus(status)) {
    return currentSeats;
  }

  let applied = false;
  const nextSeats = currentSeats.map((seat) => {
    if (!matchesDeviceStatus(seat, status)) {
      return seat;
    }

    applied = true;
    return applyDeviceStatusToSeat(seat, status, t);
  });

  return applied ? nextSeats : currentSeats;
}

function mapFloorMapSeat(dto: SeatStatusDto, t: TFn, loadedAtMs: number): SeatSummary {
  const normalizedState = normalizeState(dto.state);
  const hasActiveSession = dto.activeSessionId !== null && dto.activeSessionId !== undefined;
  const hasDevice = dto.deviceId !== null && dto.deviceId !== undefined;
  const isDeviceOnline = dto.isDeviceOnline ?? false;
  const isDeviceLocked = dto.isDeviceLocked ?? true;
  const tone = resolveTone(normalizedState, hasDevice, isDeviceOnline, hasActiveSession);
  const remainingSeconds = dto.remainingSeconds ?? null;
  const remainingDeadlineMs = remainingSeconds === null
    ? null
    : loadedAtMs + remainingSeconds * 1000;
  const accruedCostMinorUnits = dto.accruedCostMinorUnits ?? null;
  const currencyCode = dto.currencyCode ?? null;
  // Open tabs (no countdown) show the live accruing cost where fixed sessions show time left.
  const isOpenTab = hasActiveSession && remainingSeconds === null && accruedCostMinorUnits !== null;
  // Prefer the real account name from the backend; fall back to the generic placeholder only
  // when the session has no named player (guest) or the seat is free.
  const playerDisplayName = dto.playerDisplayName?.trim() || null;
  const tariffName = dto.tariffName?.trim() || null;
  const sessionStartedAtUtc = dto.sessionStartedAtUtc ?? null;

  return {
    id: dto.seatId,
    zone: dto.zoneName,
    name: dto.seatName,
    tone,
    stateLabel: seatStatusLabel(tone, t),
    player: playerDisplayName ?? (hasActiveSession ? t('op.floor.player.active') : tone === 'ready' ? t('op.floor.player.guest') : t('op.floor.player.none')),
    remaining: isOpenTab
      ? accruedCostText(accruedCostMinorUnits, currencyCode, t)
      : remainingText(remainingSeconds, normalizedState, tone, hasActiveSession, t),
    billing: isOpenTab ? 'Открытый счёт' : hasActiveSession ? 'Wallet' : tone === 'ready' ? 'Fast guest' : 'N/A',
    device: formatDeviceSummary({
      deviceName: dto.deviceName,
      isOnline: isDeviceOnline,
      isLocked: isDeviceLocked,
      agentVersion: dto.agentVersion,
      shellVersion: dto.shellVersion
    }),
    command: commandText(normalizedState, tone, hasActiveSession, isDeviceOnline),
    app: versionSummary(dto.agentVersion, dto.shellVersion),
    deviceId: dto.deviceId,
    deviceName: dto.deviceName,
    isDeviceOnline,
    isDeviceLocked,
    hasActiveSession,
    activeSessionId: dto.activeSessionId,
    rawState: dto.state,
    remainingSeconds,
    remainingDeadlineMs,
    accruedCostMinorUnits,
    currencyCode,
    sortOrder: dto.sortOrder,
    playerDisplayName,
    tariffName,
    sessionStartedAtUtc,
    // Floor-plan geometry: null pos = not placed yet; 0° = no rotation; 'pc' = default host type.
    posX: dto.posX ?? null,
    posY: dto.posY ?? null,
    rotation: dto.rotation ?? 0,
    seatType: dto.seatType ?? 'pc',
    zoneId: dto.zoneId
  };
}

function accruedCostText(minorUnits: number | null, currencyCode: string | null, t: TFn): string {
  if (minorUnits === null) {
    return t('op.floor.remaining.playing');
  }

  return currencyCode
    ? `≈ ${formatMinorUnits(minorUnits, currencyCode)}`
    : `≈ ${formatLocaleNumber(minorToMajor(minorUnits), 'ru-RU', { maximumFractionDigits: 2, minimumFractionDigits: 0 })}`;
}

function applyDeviceStatusToSeat(seat: SeatSummary, status: DeviceStatusChangedDto, t: TFn): SeatSummary {
  const hasActiveSession = seat.hasActiveSession ?? seat.tone === 'active';
  const hasDevice = true;
  const nextRawState = hasActiveSession
    ? seat.rawState ?? seat.tone
    : status.isOnline
      ? status.isLocked ? 'Locked' : 'Free'
      : 'Offline';
  const normalizedState = normalizeState(nextRawState);
  const tone = resolveTone(normalizedState, hasDevice, status.isOnline, hasActiveSession);

  return {
    ...seat,
    tone,
    stateLabel: seatStatusLabel(tone, t),
    remaining: hasActiveSession
      ? seat.remaining
      : remainingText(null, normalizedState, tone, hasActiveSession, t),
    billing: hasActiveSession ? seat.billing : tone === 'ready' ? 'Fast guest' : 'N/A',
    device: formatDeviceSummary({
      deviceName: seat.deviceName ?? status.machineName,
      isOnline: status.isOnline,
      isLocked: status.isLocked,
      agentVersion: undefined,
      shellVersion: undefined
    }),
    command: commandText(normalizedState, tone, hasActiveSession, status.isOnline),
    app: seat.app,
    deviceId: seat.deviceId ?? status.deviceId,
    deviceName: seat.deviceName ?? status.machineName,
    isDeviceOnline: status.isOnline,
    isDeviceLocked: status.isLocked,
    hasActiveSession,
    activeSessionId: seat.activeSessionId,
    rawState: nextRawState,
    remainingSeconds: hasActiveSession ? seat.remainingSeconds : null,
    remainingDeadlineMs: hasActiveSession ? seat.remainingDeadlineMs : null
  };
}

function refreshSeatRemaining(seat: SeatSummary, t: TFn, nowMs: number): SeatSummary {
  if (!seat.hasActiveSession || seat.remainingDeadlineMs === null || seat.remainingDeadlineMs === undefined) {
    return seat;
  }

  const remainingSeconds = Math.max(0, Math.ceil((seat.remainingDeadlineMs - nowMs) / 1000));
  const normalizedState = normalizeState(seat.rawState ?? '');
  const nextRemaining = remainingText(remainingSeconds, normalizedState, seat.tone, true, t);
  return nextRemaining === seat.remaining && remainingSeconds === seat.remainingSeconds
    ? seat
    : {
        ...seat,
        remaining: nextRemaining,
        remainingSeconds
      };
}

function matchesDeviceStatus(seat: SeatSummary, status: DeviceStatusChangedDto): boolean {
  return equalsIgnoreCase(seat.id, status.seatId ?? '')
    || equalsIgnoreCase(seat.deviceId ?? '', status.deviceId)
    || equalsIgnoreCase(seat.name, status.machineName);
}

function isGamingPcStatus(status: DeviceStatusChangedDto): boolean {
  return !status.role || status.role.trim().toLowerCase() === 'gaming_pc';
}

function resolveTone(
  normalizedState: string,
  hasDevice: boolean,
  isOnline: boolean,
  hasActiveSession: boolean
): SeatTone {
  // Сессия идёт, но ПК потерял связь — авария: деньги капают, контроля нет → красный.
  if (hasDevice && !isOnline && hasActiveSession) {
    return 'blocking';
  }

  if (hasDevice && !isOnline) {
    return 'offline';
  }

  switch (normalizedState) {
    case 'active':
    case 'paused':
      return 'active';
    case 'free':
    case 'locked':
      return 'ready';
    case 'requested':
    case 'ending':
      return 'pending';
    case 'failed':
      return 'blocking';
    case 'offline':
    case 'maintenance':
    case 'service':
      return 'offline';
    default:
      return 'ready';
  }
}

// Единая подпись состояния по тону — один словарь для плитки, панели и таблицы
// (раньше плитка подписывалась по сырому статусу, панель по тону → рассинхрон).
export function seatStatusLabel(tone: SeatTone, t: TFn): string {
  switch (tone) {
    case 'ready': return t('op.helper.tone.ready');
    case 'active': return t('op.helper.tone.active');
    case 'pending': return t('op.helper.tone.pending');
    case 'blocking': return t('op.helper.tone.blocking');
    case 'offline': return t('op.helper.tone.offline');
    default: return tone;
  }
}

function remainingText(
  seconds: number | null | undefined,
  normalizedState: string,
  tone: SeatTone,
  hasActiveSession: boolean,
  t: TFn
): string {
  if (seconds !== null && seconds !== undefined) {
    return formatDuration(seconds, t);
  }

  if (hasActiveSession) {
    // Сессия без обратного отсчёта: открытый счёт «играет» или потеря связи (blocking).
    return tone === 'blocking' ? t('op.floor.remaining.pcOffline') : t('op.floor.remaining.playing');
  }

  if (tone === 'ready') {
    return normalizedState === 'free' ? t('op.floor.state.free') : t('op.floor.state.locked');
  }

  if (tone === 'pending') {
    return t('op.floor.remaining.pending');
  }

  if (tone === 'offline') {
    // Обслуживание свёрнуто в «нет связи» по цвету, но причину в строке-теле сохраняем.
    return normalizedState === 'maintenance' || normalizedState === 'service'
      ? t('op.floor.remaining.closed')
      : t('op.floor.remaining.noHeartbeat');
  }

  // blocking без сессии — сбой команды.
  return t('op.floor.remaining.action');
}

function commandText(
  normalizedState: string,
  tone: SeatTone,
  hasActiveSession: boolean,
  isOnline: boolean
): string {
  if (hasActiveSession && !isOnline) {
    return 'No route';
  }

  if (tone === 'active') {
    return 'Lease fresh';
  }

  if (tone === 'ready') {
    return 'Idle';
  }

  if (tone === 'pending') {
    return normalizedState === 'ending' ? 'Stop pending' : 'Unlock pending';
  }

  if (tone === 'blocking') {
    return 'Command failed';
  }

  if (tone === 'offline') {
    return 'No route';
  }

  return 'Payment check';
}

function formatDeviceSummary({
  deviceName,
  isOnline,
  isLocked,
  agentVersion,
  shellVersion
}: {
  deviceName?: string | null;
  isOnline: boolean;
  isLocked: boolean;
  agentVersion?: string | null;
  shellVersion?: string | null;
}): string {
  if (!deviceName) {
    return 'Device unassigned';
  }

  const versions = [versionPart('Agent', agentVersion), versionPart('Shell', shellVersion)].filter(Boolean);
  return [
    deviceName,
    isOnline ? 'Online' : 'Offline',
    isLocked ? 'locked' : 'unlocked',
    ...versions
  ].join(' · ');
}

function versionSummary(agentVersion?: string | null, shellVersion?: string | null): string {
  return [versionPart('Agent', agentVersion), versionPart('Shell', shellVersion)].filter(Boolean).join(' · ') || 'Shell';
}

function versionPart(label: string, version?: string | null): string {
  return version && version.trim().length > 0 ? `${label} ${version}` : '';
}

function formatDuration(seconds: number, t: TFn): string {
  if (seconds < 60) {
    return t('op.floor.duration.sec', { count: seconds });
  }

  const totalMinutes = Math.ceil(seconds / 60);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  return hours === 0
    ? t('op.floor.duration.min', { count: minutes })
    : t('op.floor.duration.hourMin', { hours, minutes: String(minutes).padStart(2, '0') });
}

// Компактная длительность без слова «осталось» — для плитки, где время идёт «героем», а сам
// предлог «осталось» вынесен в отдельную мелкую подпись (иначе строка не влезает и режется).
export function formatDurationCompact(seconds: number, t: TFn): string {
  if (seconds < 60) {
    return t('op.floor.duration.secShort', { count: seconds });
  }

  const totalMinutes = Math.ceil(seconds / 60);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  return hours === 0
    ? t('op.floor.duration.minShort', { count: minutes })
    : t('op.floor.duration.hourMinShort', { hours, minutes: String(minutes).padStart(2, '0') });
}

function normalizeState(value: string): string {
  return value.trim().toLowerCase();
}

function equalsIgnoreCase(left: string, right: string): boolean {
  return left.localeCompare(right, undefined, { sensitivity: 'accent' }) === 0;
}
