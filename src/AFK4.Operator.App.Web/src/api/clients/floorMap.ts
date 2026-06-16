import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export interface SeatStatusDto {
  seatId: Guid;
  seatName: string;
  zoneId: Guid;
  zoneName: string;
  sortOrder: number;
  state: string;
  deviceId?: Guid | null;
  deviceName?: string | null;
  isDeviceOnline?: boolean | null;
  isDeviceLocked?: boolean | null;
  lastHeartbeatAtUtc?: string | null;
  agentVersion?: string | null;
  shellVersion?: string | null;
  activeSessionId?: Guid | null;
  remainingSeconds?: number | null;
  // Live accrued cost for an open-tab session (count-up); null for fixed sessions.
  accruedCostMinorUnits?: number | null;
  currencyCode?: string | null;
  // Who is on the seat: the active session's player display name. Null for a guest session
  // with no account, or a free seat.
  playerDisplayName?: string | null;
  // The tariff the active session bills against. Null for guest/package sessions with no
  // named tariff, or a free seat.
  tariffName?: string | null;
  // When the active session started (UTC); lets the operator see real elapsed time.
  sessionStartedAtUtc?: string | null;
  // Floor-plan layout (B2): grid cell + orientation + host type. Null/default until the
  // branch is arranged in the «План» editor; the abstract grid view ignores these.
  posX?: number | null;
  posY?: number | null;
  rotation?: number;
  seatType?: string;
}

export interface FloorMapDto {
  branchId: Guid;
  branchName: string;
  zones?: FloorMapZoneDto[];
  walls?: FloorMapWallDto[];
  seats: SeatStatusDto[];
}

export interface FloorMapZoneDto {
  zoneId: Guid;
  name: string;
  sortOrder: number;
  // Floor-plan rectangle in grid cells; null until arranged in the «План» editor (B2).
  geoX?: number | null;
  geoY?: number | null;
  geoWidth?: number | null;
  geoHeight?: number | null;
  color?: string | null;
  zoneType?: string | null;
}

export interface FloorMapWallDto {
  wallId: Guid;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
}

export function createFloorMapClient(api: PlatformApiClient) {
  return {
    getFloorMap(branchId: Guid): Promise<FloorMapDto> {
      return api.get<FloorMapDto>(`/api/branches/${branchId}/floor-map`);
    }
  };
}
