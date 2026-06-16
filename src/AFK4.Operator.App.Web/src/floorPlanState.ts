import type { OperatorFloorMapState } from './floorMapState';
import { boundingBox, type BoundingBox } from './floorPlanGeometry';
import type { SeatSummary, SeatTone } from './operatorData';
import type { PlanDraft } from './floorPlanDraft';

// A seat positioned on the plan canvas. Carries just what the canvas needs to draw it.
export interface PlanSeat {
  id: string;
  name: string;
  tone: SeatTone;
  stateLabel: string;
  seatType: string;
  rotation: number;
  posX: number;
  posY: number;
}

export interface PlanZone {
  id: string;
  name: string;
  geoX: number;
  geoY: number;
  geoWidth: number;
  geoHeight: number;
  color: string | null;
  zoneType: string | null;
}

export interface Wall {
  id: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
}

export interface PlanModel {
  placedSeats: PlanSeat[];
  unplacedSeats: SeatSummary[];
  zones: PlanZone[];
  walls: Wall[];
  bbox: BoundingBox | null;
  isEmpty: boolean;
}

// Project the live floor-map state into a flat, render-ready plan model. Seats without coordinates
// fall into `unplacedSeats` (still visible in the grid view); zones without a full rectangle are
// dropped from the canvas. `isEmpty` drives the «зал ещё не размечен» empty state.
export function toPlanModel(state: OperatorFloorMapState): PlanModel {
  const placedSeats: PlanSeat[] = [];
  const unplacedSeats: SeatSummary[] = [];

  for (const seat of state.seats) {
    if (seat.posX != null && seat.posY != null) {
      placedSeats.push({
        id: seat.id,
        name: seat.name,
        tone: seat.tone,
        stateLabel: seat.stateLabel,
        seatType: seat.seatType ?? 'pc',
        rotation: seat.rotation ?? 0,
        posX: seat.posX,
        posY: seat.posY
      });
    } else {
      unplacedSeats.push(seat);
    }
  }

  const zones: PlanZone[] = state.zones
    .filter((zone) => zone.geoX != null && zone.geoY != null && zone.geoWidth != null && zone.geoHeight != null)
    .map((zone) => ({
      id: zone.zoneId,
      name: zone.name,
      geoX: zone.geoX as number,
      geoY: zone.geoY as number,
      geoWidth: zone.geoWidth as number,
      geoHeight: zone.geoHeight as number,
      color: zone.color ?? null,
      zoneType: zone.zoneType ?? null
    }));

  const walls: Wall[] = state.walls.map((wall) => ({
    id: wall.wallId,
    x1: wall.x1,
    y1: wall.y1,
    x2: wall.x2,
    y2: wall.y2
  }));

  const bbox = boundingBox({ seats: placedSeats, zones, walls });

  return {
    placedSeats,
    unplacedSeats,
    zones,
    walls,
    bbox,
    isEmpty: bbox === null
  };
}

// Project the editor's working draft onto the same canvas model `FloorPlan` consumes. The palette
// owns the unplaced stack, so `unplacedSeats` stays empty here — the canvas only draws placed seats,
// geometric zones and walls. Recomputed on every draft mutation so the canvas follows live edits.
export function planModelFromDraft(draft: PlanDraft): PlanModel {
  const placedSeats: PlanSeat[] = draft.seats
    .filter((seat) => seat.posX != null && seat.posY != null)
    .map((seat) => ({
      id: seat.id,
      name: seat.name,
      tone: seat.tone as SeatTone,
      stateLabel: seat.stateLabel,
      seatType: seat.seatType,
      rotation: seat.rotation,
      posX: seat.posX as number,
      posY: seat.posY as number
    }));

  const zones: PlanZone[] = draft.zones
    .filter((zone) => zone.geoX != null && zone.geoY != null && zone.geoWidth != null && zone.geoHeight != null)
    .map((zone) => ({
      id: zone.zoneId,
      name: zone.name,
      geoX: zone.geoX as number,
      geoY: zone.geoY as number,
      geoWidth: zone.geoWidth as number,
      geoHeight: zone.geoHeight as number,
      color: zone.color,
      zoneType: zone.zoneType
    }));

  const walls: Wall[] = draft.walls.map((wall, index) => ({
    id: `draft-wall-${index}`,
    x1: wall.x1,
    y1: wall.y1,
    x2: wall.x2,
    y2: wall.y2
  }));

  const bbox = boundingBox({ seats: placedSeats, zones, walls });

  return { placedSeats, unplacedSeats: [], zones, walls, bbox, isEmpty: bbox === null };
}
