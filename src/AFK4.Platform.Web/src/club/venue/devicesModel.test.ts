import { describe, expect, it } from 'bun:test';
import { buildDevices } from './devicesModel';
import type { DeviceInventoryItem, FloorMap } from '@/api/types';

function device(over: Partial<DeviceInventoryItem>): DeviceInventoryItem {
  return {
    organizationId: 'org', branchId: 'b1', deviceId: 'd1', machineName: 'PC-RAW',
    agentVersion: '1', shellVersion: '1', enrolledAtUtc: '2026-05-01T00:00:00Z',
    lastHeartbeatAtUtc: null, isOnline: true, isLocked: false,
    seatId: null, seatName: null, zoneId: null, zoneName: null,
    activeCredentialCount: 0, installedAppCount: 0, pendingCommandCount: 0,
    failedCommandCount: 0, displayName: 'ПК-1', role: 'workstation', enrollmentState: 'active',
    ...over
  };
}

const floorMap: FloorMap = {
  branchId: 'b1', branchName: 'Главный', zones: [{ zoneId: 'z1', name: 'Зона A', sortOrder: 0 }],
  seats: [{
    seatId: 's1', seatName: 'Место 1', zoneId: 'z1', zoneName: 'Зона A', sortOrder: 0,
    state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null,
    lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null
  }]
};

describe('buildDevices', () => {
  it('maps online/offline status and seat label', () => {
    const vm = buildDevices(
      [device({ deviceId: 'on', isOnline: true, zoneName: 'Зона A', seatName: 'Место 1', seatId: 's1' }),
       device({ deviceId: 'off', isOnline: false })],
      [],
      floorMap
    );
    expect(vm.active.find(r => r.deviceId === 'on')!.status).toBe('online');
    expect(vm.active.find(r => r.deviceId === 'on')!.seatLabel).toBe('Зона A · Место 1');
    expect(vm.active.find(r => r.deviceId === 'off')!.status).toBe('offline');
    expect(vm.active.find(r => r.deviceId === 'off')!.seatLabel).toBe('—');
  });

  it('marks pending devices and builds seat options', () => {
    const vm = buildDevices([], [device({ deviceId: 'p', enrollmentState: 'pending' })], floorMap);
    expect(vm.pending[0].status).toBe('pending');
    expect(vm.seatOptions).toEqual([{ seatId: 's1', label: 'Зона A · Место 1' }]);
  });
});
