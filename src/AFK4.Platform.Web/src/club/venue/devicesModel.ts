import type { DeviceInventoryItem, FloorMap } from '@/api/types';

export type DeviceRowStatus = 'online' | 'offline' | 'pending';

export interface DeviceRow {
  deviceId: string;
  organizationId: string;
  displayName: string;
  machineName: string;
  seatId: string | null;
  seatLabel: string;
  status: DeviceRowStatus;
  lastHeartbeatAtUtc: string | null;
  failedCommandCount: number;
}

export interface SeatOption { seatId: string; label: string; }

export interface DevicesViewModel {
  active: DeviceRow[];
  pending: DeviceRow[];
  seatOptions: SeatOption[];
}

function seatLabel(device: DeviceInventoryItem): string {
  if (device.zoneName && device.seatName) return `${device.zoneName} · ${device.seatName}`;
  if (device.seatName) return device.seatName;
  return '—';
}

function toRow(device: DeviceInventoryItem, status: DeviceRowStatus): DeviceRow {
  return {
    deviceId: device.deviceId,
    organizationId: device.organizationId,
    displayName: device.displayName,
    machineName: device.machineName,
    seatId: device.seatId,
    seatLabel: seatLabel(device),
    status,
    lastHeartbeatAtUtc: device.lastHeartbeatAtUtc,
    failedCommandCount: device.failedCommandCount
  };
}

export function buildDevices(
  devices: DeviceInventoryItem[],
  pending: DeviceInventoryItem[],
  floorMap: FloorMap
): DevicesViewModel {
  return {
    active: devices.map(d => toRow(d, d.isOnline ? 'online' : 'offline')),
    pending: pending.map(d => toRow(d, 'pending')),
    seatOptions: floorMap.seats.map(s => ({ seatId: s.seatId, label: `${s.zoneName} · ${s.seatName}` }))
  };
}
