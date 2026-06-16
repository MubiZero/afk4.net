import { minorToMajor } from '@afk4/money';
import { formatNumber as formatLocaleNumber } from '@afk4/formatting';
import { formatMinorUnits } from './currencyFormat';
import { type MessageKey } from '@afk4/i18n';
import type { FloorMapCacheEntry } from './floorMapCache';
import type { FloorMapDto, SeatStatusDto } from './operatorApiClients';
import { seats as fixtureSeats, type SeatSummary, type SeatTone } from './operatorData';
import type { DeviceStatusChangedDto } from './operatorRealtime';

type TFn = (key: MessageKey, values?: Record<string, string | number>) => string;

export type FloorMapLoadStatus = 'idle' | 'loading' | 'ready' | 'failed';
export type FloorMapSource = 'fixture' | 'backend';

const staleAfterMs = 30_000;

export interface OperatorFloorMapState {
  branchId?: string;
  branchName: string;
  seats: SeatSummary[];
  source: FloorMapSource;
  loadStatus: FloorMapLoadStatus;
  error: string | null;
  // Offline mirror (spec §6.5): degraded read-only flag and the timestamp of the snapshot on screen.
  isOffline: boolean;
  cachedAtMs: number | null;
}

export const fixtureBranchName = 'AFK4 Dushanbe · зал A';

export function createFixtureFloorMapState(): OperatorFloorMapState {
  return {
    branchName: fixtureBranchName,
    seats: fixtureSeats,
    source: 'fixture',
    loadStatus: 'idle',
    error: null,
    isOffline: false,
    cachedAtMs: null
  };
}

export function mapFloorMapDtoToState(floorMap: FloorMapDto, t: TFn, loadedAtMs = Date.now()): OperatorFloorMapState {
  return {
    branchId: floorMap.branchId,
    branchName: floorMap.branchName,
    seats: mapFloorMapSeats(floorMap.seats, t, loadedAtMs),
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
    source: 'backend',
    loadStatus: 'ready',
    error: null,
    isOffline: true,
    cachedAtMs: entry.cachedAtMs
  };
}

// Operator-facing banner shown once the mirror is offline or the data has aged past 30s (D8):
// "Офлайн — данные от HH:MM, только просмотр". Null while live and fresh.
export function offlineBannerText(state: OperatorFloorMapState, t: TFn, nowMs = Date.now()): string | null {
  if (state.cachedAtMs === null) {
    return null;
  }

  const isStale = nowMs - state.cachedAtMs > staleAfterMs;
  if (!state.isOffline && !isStale) {
    return null;
  }

  const at = new Date(state.cachedAtMs);
  const hh = String(at.getHours()).padStart(2, '0');
  const mm = String(at.getMinutes()).padStart(2, '0');
  // Честно различаем причину: реальный обрыв связи (только просмотр) vs просто устаревший
  // снимок при живом соединении — последнее НЕ «офлайн», иначе врём против футера.
  return t(state.isOffline ? 'op.floor.offlineBanner' : 'op.floor.staleBanner', { time: `${hh}:${mm}` });
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

  return {
    id: dto.seatId,
    zone: dto.zoneName,
    name: dto.seatName,
    tone,
    stateLabel: displayState(dto.state, t),
    player: hasActiveSession ? t('op.floor.player.active') : tone === 'ready' ? t('op.floor.player.guest') : t('op.floor.player.none'),
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
    sortOrder: dto.sortOrder
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
    stateLabel: displayState(nextRawState, t),
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
  if (hasDevice && !isOnline && hasActiveSession) {
    return 'warning';
  }

  if (hasDevice && !isOnline) {
    return 'offline';
  }

  switch (normalizedState) {
    case 'active':
      return 'active';
    case 'free':
    case 'locked':
      return 'ready';
    case 'requested':
    case 'ending':
      return 'pending';
    case 'paused':
      return 'warning';
    case 'failed':
      return 'blocking';
    case 'offline':
      return 'offline';
    case 'maintenance':
    case 'service':
      return 'service';
    default:
      return 'ready';
  }
}

function displayState(value: string, t: TFn): string {
  switch (normalizeState(value)) {
    case 'active':
      return t('op.floor.state.active');
    case 'free':
      return t('op.floor.state.free');
    case 'locked':
      return t('op.floor.state.locked');
    case 'requested':
      return t('op.floor.state.requested');
    case 'ending':
      return t('op.floor.state.ending');
    case 'paused':
      return t('op.floor.state.paused');
    case 'failed':
      return t('op.floor.state.failed');
    case 'offline':
      return t('op.floor.state.offline');
    case 'maintenance':
    case 'service':
      return t('op.floor.state.service');
    default:
      return value.replaceAll('_', ' ');
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
    return tone === 'warning' ? t('op.floor.remaining.pcOffline') : t('op.floor.remaining.playing');
  }

  if (tone === 'ready') {
    return normalizedState === 'free' ? t('op.floor.state.free') : t('op.floor.state.locked');
  }

  if (tone === 'pending') {
    return t('op.floor.remaining.pending');
  }

  if (tone === 'offline') {
    return t('op.floor.remaining.noHeartbeat');
  }

  if (tone === 'service') {
    return t('op.floor.remaining.closed');
  }

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

  if (tone === 'service') {
    return 'Technician';
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
