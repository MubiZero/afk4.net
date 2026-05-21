import { describe, expect, it } from 'vitest';
import {
  applyDeviceStatusToSeats,
  createFixtureFloorMapState,
  mapFloorMapDtoToState
} from './floorMapState';
import type { FloorMapDto } from './operatorApiClients';

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';
const deviceId = '11111111-1111-1111-1111-111111111111';

describe('floor-map state', () => {
  it('creates a fixture fallback state for offline browser-dev runs', () => {
    const state = createFixtureFloorMapState();

    expect(state.source).toBe('fixture');
    expect(state.loadStatus).toBe('idle');
    expect(state.seats.length).toBeGreaterThan(0);
  });

  it('maps backend floor-map DTOs to sorted operator seat summaries', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      seats: [
        createSeat({ seatName: 'PC-02', sortOrder: 20, state: 'Locked' }),
        createSeat({
          seatId: '22222222-2222-2222-2222-222222222222',
          seatName: 'PC-01',
          sortOrder: 10,
          state: 'Active',
          isDeviceOnline: false,
          activeSessionId: '33333333-3333-3333-3333-333333333333',
          remainingSeconds: 3660
        })
      ]
    });

    expect(state.source).toBe('backend');
    expect(state.branchName).toBe('Demo Branch');
    expect(state.seats.map((seat) => seat.name)).toEqual(['PC-01', 'PC-02']);
    expect(state.seats[0]).toMatchObject({
      tone: 'warning',
      command: 'No route',
      remaining: 'осталось 1 ч 01 мин'
    });
    expect(state.seats[1]).toMatchObject({
      tone: 'ready',
      stateLabel: 'Готов',
      command: 'Idle'
    });
  });

  it('applies SignalR device status by device id and updates free seats', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      seats: [createSeat({ state: 'Locked', isDeviceLocked: true })]
    });

    const nextSeats = applyDeviceStatusToSeats(state.seats, {
      organizationId,
      branchId,
      deviceId,
      machineName: 'ignored',
      isOnline: true,
      isLocked: false,
      observedAtUtc: '2026-05-21T10:00:00Z'
    });

    expect(nextSeats).not.toBe(state.seats);
    expect(nextSeats[0]).toMatchObject({
      tone: 'ready',
      rawState: 'Free',
      device: 'PC-01 · Online · unlocked',
      command: 'Idle'
    });
  });

  it('keeps active sessions visible when their device heartbeat goes offline', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      seats: [
        createSeat({
          state: 'Active',
          activeSessionId: '33333333-3333-3333-3333-333333333333',
          remainingSeconds: 900
        })
      ]
    });

    const nextSeats = applyDeviceStatusToSeats(state.seats, {
      organizationId,
      branchId,
      deviceId,
      machineName: 'PC-01',
      isOnline: false,
      isLocked: false,
      observedAtUtc: '2026-05-21T10:00:00Z'
    });

    expect(nextSeats[0]).toMatchObject({
      tone: 'warning',
      stateLabel: 'В сессии',
      remaining: 'осталось 15 мин',
      command: 'No route'
    });
  });
});

function createSeat(overrides: Partial<FloorMapDto['seats'][number]> = {}): FloorMapDto['seats'][number] {
  return {
    seatId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    seatName: 'PC-01',
    zoneId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
    zoneName: 'Main Hall',
    sortOrder: 10,
    state: 'Locked',
    deviceId,
    deviceName: 'PC-01',
    isDeviceOnline: true,
    isDeviceLocked: true,
    lastHeartbeatAtUtc: '2026-05-21T09:55:00Z',
    agentVersion: null,
    shellVersion: null,
    activeSessionId: null,
    remainingSeconds: null,
    ...overrides
  };
}
