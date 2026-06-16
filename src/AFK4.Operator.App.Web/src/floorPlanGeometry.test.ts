import { describe, expect, it } from 'bun:test';
import { boundingBox, cellToPx, DEFAULT_CELL_SIZE } from './floorPlanGeometry';

describe('floor-plan geometry', () => {
  it('converts grid cells to pixels with the default cell size', () => {
    expect(cellToPx(0)).toBe(0);
    expect(cellToPx(3)).toBe(3 * DEFAULT_CELL_SIZE);
  });

  it('converts grid cells to pixels with a custom cell size', () => {
    expect(cellToPx(2, 40)).toBe(80);
  });

  it('returns null bounding box when there is nothing to draw', () => {
    expect(boundingBox({ seats: [], zones: [], walls: [] })).toBeNull();
  });

  it('computes a bounding box spanning seats, zones and walls (seats occupy one cell)', () => {
    const box = boundingBox({
      seats: [{ posX: 2, posY: 3 }],
      zones: [{ geoX: 0, geoY: 0, geoWidth: 4, geoHeight: 2 }],
      walls: [{ x1: 5, y1: 1, x2: 5, y2: 6 }]
    });
    expect(box).toEqual({ minX: 0, minY: 0, maxX: 5, maxY: 6 });
  });
});
