import { describe, expect, it } from 'bun:test';
import { createDraft, placeSeat, moveSeat, removeSeatFromPlan, rotateSeat, setSeatType, toBulkUpdateRequest, addZone, setZoneGeometry, renameZone, setZoneColor, removeZone, setSeatZone, zoneSeatCount, addWall, removeWall } from './floorPlanDraft';
import type { OperatorFloorMapState } from './floorMapState';

function state(): OperatorFloorMapState {
  return {
    branchId: 'b1', branchName: 'AFK4', source: 'backend', loadStatus: 'ready',
    error: null, isOffline: false, etag: 'W/"v1"',
    zones: [{ zoneId: 'z1', name: 'Зал A', sortOrder: 0, geoX: null, geoY: null, geoWidth: null, geoHeight: null, color: null, zoneType: null }],
    walls: [{ wallId: 'w1', x1: 0, y1: 0, x2: 4, y2: 0 }],
    seats: [
      { id: 's1', zoneId: 'z1', name: 'PC-01', tone: 'ready', stateLabel: 'Свободно', sortOrder: 0, posX: 2, posY: 1, rotation: 0, seatType: 'pc' },
      { id: 's2', zoneId: 'z1', name: 'PC-02', tone: 'ready', stateLabel: 'Свободно', sortOrder: 1, posX: null, posY: null, rotation: 0, seatType: 'pc' }
    ]
  } as unknown as OperatorFloorMapState;
}

describe('floorPlanDraft', () => {
  it('serializes ALL seats and ALL zones (placed and unplaced) — never drops any', () => {
    const draft = createDraft(state());
    const req = toBulkUpdateRequest(draft, 'org-1');
    expect(req.organizationId).toBe('org-1');
    expect(req.zones).toHaveLength(1);
    expect(req.zones[0]).toMatchObject({ zoneId: 'z1', clientId: 'z1', name: 'Зал A', sortOrder: 0 });
    expect(req.seats).toHaveLength(2);
    const s1 = req.seats.find((s) => s.seatId === 's1')!;
    expect(s1).toMatchObject({ clientId: 's1', zoneClientId: 'z1', posX: 2, posY: 1, seatType: 'pc' });
    const s2 = req.seats.find((s) => s.seatId === 's2')!;
    expect(s2.posX).toBeNull();
    expect(s2.posY).toBeNull();
    expect(req.walls).toEqual([{ x1: 0, y1: 0, x2: 4, y2: 0 }]);
  });

  it('placeSeat puts an unplaced seat at a cell; removeSeatFromPlan clears it', () => {
    let draft = createDraft(state());
    draft = placeSeat(draft, 's2', 5, 3);
    expect(draft.seats.find((s) => s.id === 's2')!.posX).toBe(5);
    draft = removeSeatFromPlan(draft, 's2');
    expect(draft.seats.find((s) => s.id === 's2')!.posX).toBeNull();
  });

  it('moveSeat / rotateSeat / setSeatType mutate only the target seat', () => {
    let draft = createDraft(state());
    draft = moveSeat(draft, 's1', 4, 4);
    draft = rotateSeat(draft, 's1', 90);
    draft = setSeatType(draft, 's1', 'console');
    const s1 = draft.seats.find((s) => s.id === 's1')!;
    expect(s1).toMatchObject({ posX: 4, posY: 4, rotation: 90, seatType: 'console' });
    expect(draft.seats.find((s) => s.id === 's2')!.rotation).toBe(0);
  });

  it('isDirty flips after a mutation', () => {
    const draft = createDraft(state());
    expect(draft.isDirty).toBe(false);
    expect(moveSeat(draft, 's1', 9, 9).isDirty).toBe(true);
  });
});

function stateWith(): OperatorFloorMapState {
  return {
    branchName: 'B', source: 'backend', loadStatus: 'ready', loadedAtMs: 0,
    error: null, isStale: false, etag: 'v1',
    seats: [
      { id: 'seat-1', zoneId: 'zone-1', name: 'PC-1', sortOrder: 0, tone: 'free', stateLabel: 'free', seatType: 'pc', rotation: 0, posX: 0, posY: 0 },
      { id: 'seat-2', zoneId: 'zone-1', name: 'PC-2', sortOrder: 1, tone: 'free', stateLabel: 'free', seatType: 'pc', rotation: 0, posX: null, posY: null }
    ] as unknown as OperatorFloorMapState['seats'],
    zones: [
      { zoneId: 'zone-1', name: 'Зал', sortOrder: 0, geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 3, color: null, zoneType: null }
    ] as OperatorFloorMapState['zones'],
    walls: [{ wallId: 'w1', x1: 0, y1: 0, x2: 5, y2: 0 }] as OperatorFloorMapState['walls']
  } as unknown as OperatorFloorMapState;
}

describe('zone editing', () => {
  it('createDraft mirrors zoneId into serverId for existing zones', () => {
    const draft = createDraft(stateWith());
    expect(draft.zones[0].zoneId).toBe('zone-1');
    expect(draft.zones[0].serverId).toBe('zone-1');
  });

  it('addZone appends a new zone with serverId null and next sortOrder, dirty', () => {
    const draft = addZone(createDraft(stateWith()), 'new-zone-0', 'Новая зона', 0, 3, 4, 3);
    const added = draft.zones.find((z) => z.zoneId === 'new-zone-0');
    expect(added).toBeTruthy();
    expect(added?.serverId).toBeNull();
    expect(added?.sortOrder).toBe(1);
    expect(added?.geoWidth).toBe(4);
    expect(draft.isDirty).toBe(true);
  });

  it('setZoneGeometry patches only given fields', () => {
    const draft = setZoneGeometry(createDraft(stateWith()), 'zone-1', { geoWidth: 9 });
    expect(draft.zones[0].geoWidth).toBe(9);
    expect(draft.zones[0].geoX).toBe(0);
    expect(draft.isDirty).toBe(true);
  });

  it('renameZone and setZoneColor update the zone', () => {
    let draft = renameZone(createDraft(stateWith()), 'zone-1', 'VIP');
    draft = setZoneColor(draft, 'zone-1', '#3b82f6');
    expect(draft.zones[0].name).toBe('VIP');
    expect(draft.zones[0].color).toBe('#3b82f6');
  });

  it('zoneSeatCount counts placed AND unplaced seats of a zone', () => {
    expect(zoneSeatCount(createDraft(stateWith()), 'zone-1')).toBe(2);
  });

  it('setSeatZone repoints a seat; removeZone drops an empty zone', () => {
    let draft = addZone(createDraft(stateWith()), 'new-zone-0', 'Новая зона', 0, 3, 4, 3);
    draft = setSeatZone(draft, 'seat-1', 'new-zone-0');
    draft = setSeatZone(draft, 'seat-2', 'new-zone-0');
    expect(zoneSeatCount(draft, 'zone-1')).toBe(0);
    draft = removeZone(draft, 'zone-1');
    expect(draft.zones.find((z) => z.zoneId === 'zone-1')).toBeUndefined();
  });

  it('toBulkUpdateRequest emits serverId as zoneId (null for new zones) and clientId as the ref key', () => {
    let draft = addZone(createDraft(stateWith()), 'new-zone-0', 'Новая зона', 0, 3, 4, 3);
    draft = setSeatZone(draft, 'seat-1', 'new-zone-0');
    const req = toBulkUpdateRequest(draft, 'org-1');

    const existing = req.zones.find((z) => z.clientId === 'zone-1');
    expect(existing?.zoneId).toBe('zone-1');

    const created = req.zones.find((z) => z.clientId === 'new-zone-0');
    expect(created?.zoneId).toBeNull();

    const moved = req.seats.find((s) => s.clientId === 'seat-1');
    expect(moved?.zoneClientId).toBe('new-zone-0');
  });
});

describe('wall editing', () => {
  it('addWall appends a segment and marks dirty', () => {
    const draft = addWall(createDraft(stateWith()), 1, 2, 4, 2);
    expect(draft.walls).toHaveLength(2);                 // the fixture already has one wall
    expect(draft.walls[1]).toEqual({ x1: 1, y1: 2, x2: 4, y2: 2 });
    expect(draft.isDirty).toBe(true);
  });

  it('removeWall drops the indexed segment and marks dirty', () => {
    let draft = addWall(createDraft(stateWith()), 1, 2, 4, 2);
    draft = removeWall(draft, 0);                         // drop the original fixture wall
    expect(draft.walls).toHaveLength(1);
    expect(draft.walls[0]).toEqual({ x1: 1, y1: 2, x2: 4, y2: 2 });
    expect(draft.isDirty).toBe(true);
  });

  it('serializes all walls in the bulk request', () => {
    const draft = addWall(createDraft(stateWith()), 1, 2, 4, 2);
    const req = toBulkUpdateRequest(draft, 'org-1');
    expect(req.walls).toHaveLength(2);
  });
});
