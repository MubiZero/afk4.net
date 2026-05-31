import { it, expect } from 'bun:test';
import type { FloorMap } from '@/api/types';
import { toEditorZones, buildBulkRequest, moveByIndex, type EditorZone } from './floorMapModel';

const floorMap: FloorMap = {
  branchId: 'b1',
  branchName: 'Центр',
  zones: [
    { zoneId: 'z2', name: 'Zone B', sortOrder: 2 },
    { zoneId: 'z1', name: 'Zone A', sortOrder: 1 }
  ],
  seats: [
    { seatId: 's2', seatName: 'PC-2', zoneId: 'z1', zoneName: 'Zone A', sortOrder: 2, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null },
    { seatId: 's1', seatName: 'PC-1', zoneId: 'z1', zoneName: 'Zone A', sortOrder: 1, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null }
  ]
};

it('maps the read model to zones and seats ordered by sortOrder', () => {
  const zones = toEditorZones(floorMap);
  expect(zones.map(z => z.name)).toEqual(['Zone A', 'Zone B']);
  expect(zones[0].zoneId).toBe('z1');
  expect(zones[0].seats.map(s => s.name)).toEqual(['PC-1', 'PC-2']);
  expect(zones[1].seats).toEqual([]);
});

it('builds a bulk request: drops empty names, indexes sortOrder, links seats to their zone clientId', () => {
  const zones: EditorZone[] = [
    { clientId: 'cz1', zoneId: 'z1', name: ' Hall ', seats: [
      { clientId: 'cs1', seatId: 's1', name: 'PC-1' },
      { clientId: 'cs2', seatId: null, name: '  ' } // empty -> dropped
    ] },
    { clientId: 'cz2', zoneId: null, name: '  ' } as EditorZone // empty zone -> dropped
  ];
  const req = buildBulkRequest('org', zones);
  expect(req.organizationId).toBe('org');
  expect(req.zones).toEqual([{ zoneId: 'z1', clientId: 'cz1', name: 'Hall', sortOrder: 1 }]);
  expect(req.seats).toEqual([{ seatId: 's1', clientId: 'cs1', zoneClientId: 'cz1', name: 'PC-1', sortOrder: 1 }]);
});

it('moveByIndex swaps adjacent items and is a no-op at the edges', () => {
  expect(moveByIndex(['a', 'b', 'c'], 0, 1)).toEqual(['b', 'a', 'c']);
  expect(moveByIndex(['a', 'b', 'c'], 2, 1)).toEqual(['a', 'b', 'c']);
  expect(moveByIndex(['a', 'b', 'c'], 0, -1)).toEqual(['a', 'b', 'c']);
});
