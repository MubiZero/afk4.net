// Floor-plan coordinate system: integer grid cells. Pure helpers so the geometry is unit-testable
// and shared between the read-only «План» view (B2-2) and the editor (B2-3).

export const DEFAULT_CELL_SIZE = 56;
export const CANVAS_PADDING = 32;

export interface BoundingBox {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

export function cellToPx(cell: number, cellSize: number = DEFAULT_CELL_SIZE): number {
  return cell * cellSize;
}

// Outer extent (in cells) of everything to draw. A seat occupies a single cell, so its right/bottom
// edge is +1. Returns null when there is nothing positioned — the caller shows an empty state.
export function boundingBox(inputs: {
  seats: { posX: number; posY: number }[];
  zones: { geoX: number; geoY: number; geoWidth: number; geoHeight: number }[];
  walls: { x1: number; y1: number; x2: number; y2: number }[];
}): BoundingBox | null {
  const xs: number[] = [];
  const ys: number[] = [];

  for (const seat of inputs.seats) {
    xs.push(seat.posX, seat.posX + 1);
    ys.push(seat.posY, seat.posY + 1);
  }
  for (const zone of inputs.zones) {
    xs.push(zone.geoX, zone.geoX + zone.geoWidth);
    ys.push(zone.geoY, zone.geoY + zone.geoHeight);
  }
  for (const wall of inputs.walls) {
    xs.push(wall.x1, wall.x2);
    ys.push(wall.y1, wall.y2);
  }

  if (xs.length === 0) {
    return null;
  }

  return {
    minX: Math.min(...xs),
    minY: Math.min(...ys),
    maxX: Math.max(...xs),
    maxY: Math.max(...ys)
  };
}
