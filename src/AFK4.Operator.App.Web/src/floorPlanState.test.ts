import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { mapFloorMapDtoToState } from './floorMapState';
import { toPlanModel } from './floorPlanState';

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const t = createTranslator('ru');

function seat(overrides: Record<string, unknown>) {
  return {
    seatId: '00000000-0000-0000-0000-000000000000',
    seatName: 'PC',
    zoneId: '44444444-4444-4444-4444-444444444444',
    zoneName: 'Зал A',
    sortOrder: 10,
    state: 'Locked',
    ...overrides
  };
}

describe('floor-plan model', () => {
  it('splits seats into placed and unplaced, keeps geometric zones and walls', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo',
      zones: [
        { zoneId: '44444444-4444-4444-4444-444444444444', name: 'Зал A', sortOrder: 10, geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 3, color: '#22d3ee', zoneType: 'hall' },
        { zoneId: '55555555-5555-5555-5555-555555555555', name: 'Без гео', sortOrder: 20 }
      ],
      walls: [{ wallId: '66666666-6666-6666-6666-666666666666', x1: 0, y1: 0, x2: 4, y2: 0 }],
      seats: [
        seat({ seatId: '11111111-1111-1111-1111-111111111111', seatName: 'PC-01', posX: 1, posY: 1, seatType: 'pc' }),
        seat({ seatId: '22222222-2222-2222-2222-222222222222', seatName: 'PC-02' })
      ]
    }, t);

    const model = toPlanModel(state);

    expect(model.placedSeats.map((s) => s.name)).toEqual(['PC-01']);
    expect(model.placedSeats[0]).toMatchObject({ posX: 1, posY: 1, seatType: 'pc' });
    expect(model.unplacedSeats.map((s) => s.name)).toEqual(['PC-02']);
    expect(model.zones).toHaveLength(1);
    expect(model.zones[0]).toMatchObject({ name: 'Зал A', geoWidth: 4, geoHeight: 3 });
    expect(model.walls).toHaveLength(1);
    expect(model.isEmpty).toBe(false);
    expect(model.bbox).not.toBeNull();
  });

  it('is empty when nothing has coordinates', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo',
      seats: [seat({ seatName: 'PC-01' })]
    }, t);

    const model = toPlanModel(state);

    expect(model.placedSeats).toEqual([]);
    expect(model.unplacedSeats).toHaveLength(1);
    expect(model.isEmpty).toBe(true);
    expect(model.bbox).toBeNull();
  });
});
