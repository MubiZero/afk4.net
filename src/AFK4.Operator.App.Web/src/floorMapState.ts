import type { FloorMapDto, SeatStatusDto } from './operatorApiClients';
import { seats as fixtureSeats, type SeatSummary, type SeatTone } from './operatorData';
import type { DeviceStatusChangedDto } from './operatorRealtime';

export type FloorMapLoadStatus = 'idle' | 'loading' | 'ready' | 'failed';
export type FloorMapSource = 'fixture' | 'backend';

export interface OperatorFloorMapState {
  branchId?: string;
  branchName: string;
  seats: SeatSummary[];
  source: FloorMapSource;
  loadStatus: FloorMapLoadStatus;
  error: string | null;
}

export const fixtureBranchName = 'AFK4 Dushanbe · зал A';

export function createFixtureFloorMapState(): OperatorFloorMapState {
  return {
    branchName: fixtureBranchName,
    seats: fixtureSeats,
    source: 'fixture',
    loadStatus: 'idle',
    error: null
  };
}

export function mapFloorMapDtoToState(floorMap: FloorMapDto, loadedAtMs = Date.now()): OperatorFloorMapState {
  return {
    branchId: floorMap.branchId,
    branchName: floorMap.branchName,
    seats: mapFloorMapSeats(floorMap.seats, loadedAtMs),
    source: 'backend',
    loadStatus: 'ready',
    error: null
  };
}

export function mapFloorMapSeats(nextSeats: SeatStatusDto[], loadedAtMs = Date.now()): SeatSummary[] {
  return [...nextSeats]
    .sort((left, right) => left.sortOrder - right.sortOrder || left.seatName.localeCompare(right.seatName))
    .map((seat) => mapFloorMapSeat(seat, loadedAtMs));
}

export function refreshFloorMapRemaining(
  floorMap: OperatorFloorMapState,
  nowMs = Date.now()
): OperatorFloorMapState {
  let changed = false;
  const nextSeats = floorMap.seats.map((seat) => {
    const nextSeat = refreshSeatRemaining(seat, nowMs);
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
  status: DeviceStatusChangedDto
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
    return applyDeviceStatusToSeat(seat, status);
  });

  return applied ? nextSeats : currentSeats;
}

function mapFloorMapSeat(dto: SeatStatusDto, loadedAtMs: number): SeatSummary {
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

  return {
    id: dto.seatId,
    zone: dto.zoneName,
    name: dto.seatName,
    tone,
    stateLabel: displayState(dto.state),
    player: hasActiveSession ? 'Активный клиент' : tone === 'ready' ? 'Гость' : 'Нет игрока',
    remaining: remainingText(remainingSeconds, normalizedState, tone, hasActiveSession),
    billing: hasActiveSession ? 'Wallet' : tone === 'ready' ? 'Fast guest' : 'N/A',
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
    sortOrder: dto.sortOrder
  };
}

function applyDeviceStatusToSeat(seat: SeatSummary, status: DeviceStatusChangedDto): SeatSummary {
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
    stateLabel: displayState(nextRawState),
    remaining: hasActiveSession
      ? seat.remaining
      : remainingText(null, normalizedState, tone, hasActiveSession),
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

function refreshSeatRemaining(seat: SeatSummary, nowMs: number): SeatSummary {
  if (!seat.hasActiveSession || seat.remainingDeadlineMs === null || seat.remainingDeadlineMs === undefined) {
    return seat;
  }

  const remainingSeconds = Math.max(0, Math.ceil((seat.remainingDeadlineMs - nowMs) / 1000));
  const normalizedState = normalizeState(seat.rawState ?? '');
  const nextRemaining = remainingText(remainingSeconds, normalizedState, seat.tone, true);
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

function displayState(value: string): string {
  switch (normalizeState(value)) {
    case 'active':
      return 'В сессии';
    case 'free':
      return 'Свободно';
    case 'locked':
      return 'Готов';
    case 'requested':
      return 'Ожидание';
    case 'ending':
      return 'Команда';
    case 'paused':
      return 'Пауза';
    case 'failed':
      return 'Ошибка';
    case 'offline':
      return 'Офлайн';
    case 'maintenance':
    case 'service':
      return 'Сервис';
    default:
      return value.replaceAll('_', ' ');
  }
}

function remainingText(
  seconds: number | null | undefined,
  normalizedState: string,
  tone: SeatTone,
  hasActiveSession: boolean
): string {
  if (seconds !== null && seconds !== undefined) {
    return formatDuration(seconds);
  }

  if (hasActiveSession) {
    return tone === 'warning' ? 'ПК офлайн' : 'играет сейчас';
  }

  if (tone === 'ready') {
    return normalizedState === 'free' ? 'Свободно' : 'Готов';
  }

  if (tone === 'pending') {
    return 'Ожидает';
  }

  if (tone === 'offline') {
    return 'Нет heartbeat';
  }

  if (tone === 'service') {
    return 'Закрыт';
  }

  return 'Нужно действие';
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

function formatDuration(seconds: number): string {
  if (seconds < 60) {
    return `осталось ${seconds} с`;
  }

  const totalMinutes = Math.ceil(seconds / 60);
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;

  return hours === 0
    ? `осталось ${minutes} мин`
    : `осталось ${hours} ч ${String(minutes).padStart(2, '0')} мин`;
}

function normalizeState(value: string): string {
  return value.trim().toLowerCase();
}

function equalsIgnoreCase(left: string, right: string): boolean {
  return left.localeCompare(right, undefined, { sensitivity: 'accent' }) === 0;
}
