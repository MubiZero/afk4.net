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
}

export interface FloorMapDto {
  branchId: Guid;
  branchName: string;
  zones?: FloorMapZoneDto[];
  seats: SeatStatusDto[];
}

export interface FloorMapZoneDto {
  zoneId: Guid;
  name: string;
  sortOrder: number;
}

export function createFloorMapClient(api: PlatformApiClient) {
  return {
    getFloorMap(branchId: Guid): Promise<FloorMapDto> {
      return api.get<FloorMapDto>(`/api/branches/${branchId}/floor-map`);
    }
  };
}
