import type { OperatorFloorMapState } from './floorMapState';
import type {
  FloorMapBulkUpdateRequest,
  FloorMapBulkSeatRequest,
  FloorMapBulkZoneRequest
} from './api/clients/floorMap';

// A seat as the editor tracks it: identity + zone + layout. Placed seats have posX/posY; unplaced
// seats keep them null but STAY in the draft so the full-replace save never deletes them.
export interface DraftSeat {
  id: string; zoneId: string; name: string; sortOrder: number;
  tone: string; stateLabel: string; seatType: string; rotation: number;
  posX: number | null; posY: number | null;
}
export interface DraftZone {
  zoneId: string; name: string; sortOrder: number;
  geoX: number | null; geoY: number | null; geoWidth: number | null; geoHeight: number | null;
  color: string | null; zoneType: string | null;
}
export interface DraftWall { x1: number; y1: number; x2: number; y2: number; }
export interface PlanDraft {
  etag: string | null; seats: DraftSeat[]; zones: DraftZone[]; walls: DraftWall[]; isDirty: boolean;
}

export function createDraft(state: OperatorFloorMapState): PlanDraft {
  return {
    etag: state.etag,
    isDirty: false,
    seats: state.seats.map((seat) => ({
      id: seat.id,
      zoneId: seat.zoneId ?? '',
      name: seat.name,
      sortOrder: seat.sortOrder ?? 0,
      tone: seat.tone,
      stateLabel: seat.stateLabel,
      seatType: seat.seatType ?? 'pc',
      rotation: seat.rotation ?? 0,
      posX: seat.posX ?? null,
      posY: seat.posY ?? null
    })),
    zones: state.zones.map((zone) => ({
      zoneId: zone.zoneId,
      name: zone.name,
      sortOrder: zone.sortOrder,
      geoX: zone.geoX ?? null,
      geoY: zone.geoY ?? null,
      geoWidth: zone.geoWidth ?? null,
      geoHeight: zone.geoHeight ?? null,
      color: zone.color ?? null,
      zoneType: zone.zoneType ?? null
    })),
    walls: state.walls.map((wall) => ({ x1: wall.x1, y1: wall.y1, x2: wall.x2, y2: wall.y2 }))
  };
}

function mutateSeat(draft: PlanDraft, seatId: string, change: Partial<DraftSeat>): PlanDraft {
  return { ...draft, isDirty: true, seats: draft.seats.map((s) => (s.id === seatId ? { ...s, ...change } : s)) };
}
export function placeSeat(draft: PlanDraft, seatId: string, posX: number, posY: number): PlanDraft {
  return mutateSeat(draft, seatId, { posX, posY });
}
export function moveSeat(draft: PlanDraft, seatId: string, posX: number, posY: number): PlanDraft {
  return mutateSeat(draft, seatId, { posX, posY });
}
export function removeSeatFromPlan(draft: PlanDraft, seatId: string): PlanDraft {
  return mutateSeat(draft, seatId, { posX: null, posY: null });
}
export function rotateSeat(draft: PlanDraft, seatId: string, rotation: number): PlanDraft {
  return mutateSeat(draft, seatId, { rotation });
}
export function setSeatType(draft: PlanDraft, seatId: string, seatType: string): PlanDraft {
  return mutateSeat(draft, seatId, { seatType });
}

// Serialize the ENTIRE layout. clientId == existing id so the server maps 1:1 and deletes nothing.
// Unplaced seats go in with null coords (still owned); zones and walls are echoed back unchanged
// (zone geometry + walls are edited in B2-3b, not here).
export function toBulkUpdateRequest(draft: PlanDraft, organizationId: string): FloorMapBulkUpdateRequest {
  const zones: FloorMapBulkZoneRequest[] = draft.zones.map((zone) => ({
    zoneId: zone.zoneId, clientId: zone.zoneId, name: zone.name, sortOrder: zone.sortOrder,
    geoX: zone.geoX, geoY: zone.geoY, geoWidth: zone.geoWidth, geoHeight: zone.geoHeight,
    color: zone.color, zoneType: zone.zoneType
  }));
  const seats: FloorMapBulkSeatRequest[] = draft.seats.map((seat) => ({
    seatId: seat.id, clientId: seat.id, zoneClientId: seat.zoneId, name: seat.name, sortOrder: seat.sortOrder,
    posX: seat.posX, posY: seat.posY, rotation: seat.rotation, seatType: seat.seatType
  }));
  return { organizationId, zones, seats, walls: draft.walls.map((w) => ({ x1: w.x1, y1: w.y1, x2: w.x2, y2: w.y2 })) };
}
