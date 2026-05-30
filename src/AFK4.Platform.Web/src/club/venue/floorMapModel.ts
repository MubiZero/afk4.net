import type {
  FloorMap,
  FloorMapBulkSeatRequest,
  FloorMapBulkUpdateRequest,
  FloorMapBulkZoneRequest
} from '@/api/types';

export interface EditorSeat {
  clientId: string;
  seatId: string | null;
  name: string;
}

export interface EditorZone {
  clientId: string;
  zoneId: string | null;
  name: string;
  seats: EditorSeat[];
}

export function makeClientId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

export function moveByIndex<T>(items: T[], index: number, direction: -1 | 1): T[] {
  const target = index + direction;
  if (target < 0 || target >= items.length) return items;
  const next = [...items];
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}

export function toEditorZones(floorMap: FloorMap): EditorZone[] {
  const seatOrder = new Map<string, number>();
  const entries = new Map<string, { zone: EditorZone; sortOrder: number }>();

  for (const zone of floorMap.zones ?? []) {
    entries.set(zone.zoneId, {
      zone: { clientId: zone.zoneId, zoneId: zone.zoneId, name: zone.name, seats: [] },
      sortOrder: zone.sortOrder
    });
  }
  for (const seat of floorMap.seats) {
    let entry = entries.get(seat.zoneId);
    if (entry === undefined) {
      entry = {
        zone: { clientId: seat.zoneId, zoneId: seat.zoneId, name: seat.zoneName, seats: [] },
        sortOrder: entries.size + 1
      };
      entries.set(seat.zoneId, entry);
    }
    seatOrder.set(seat.seatId, seat.sortOrder);
    entry.zone.seats.push({ clientId: seat.seatId, seatId: seat.seatId, name: seat.seatName });
  }

  const ordered = Array.from(entries.values()).sort((a, b) => a.sortOrder - b.sortOrder);
  for (const e of ordered) {
    e.zone.seats.sort((s1, s2) => (seatOrder.get(s1.seatId ?? '') ?? 0) - (seatOrder.get(s2.seatId ?? '') ?? 0));
  }
  return ordered.map(e => e.zone);
}

export function buildBulkRequest(organizationId: string, zones: EditorZone[]): FloorMapBulkUpdateRequest {
  const compacted = zones.filter(zone => zone.name.trim().length > 0);
  return {
    organizationId,
    zones: compacted.map<FloorMapBulkZoneRequest>((zone, i) => ({
      zoneId: zone.zoneId,
      clientId: zone.clientId,
      name: zone.name.trim(),
      sortOrder: i + 1
    })),
    seats: compacted.flatMap<FloorMapBulkSeatRequest>(zone =>
      zone.seats
        .filter(seat => seat.name.trim().length > 0)
        .map((seat, j) => ({
          seatId: seat.seatId,
          clientId: seat.clientId,
          zoneClientId: zone.clientId,
          name: seat.name.trim(),
          sortOrder: j + 1
        }))
    )
  };
}
