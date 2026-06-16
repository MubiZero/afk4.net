import { describe, expect, it } from 'bun:test';
import { createDraft, placeSeat, moveSeat, removeSeatFromPlan, rotateSeat, setSeatType, toBulkUpdateRequest } from './floorPlanDraft';
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
