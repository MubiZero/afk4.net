import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import {
  applyDeviceStatusToSeats,
  createFixtureFloorMapState,
  isSeatReadyForGuest,
  mapFloorMapDtoToState,
  refreshFloorMapRemaining
} from './floorMapState';
import type { FloorMapDto } from './operatorApiClients';

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';
const deviceId = '11111111-1111-1111-1111-111111111111';
const t = createTranslator('ru');

describe('floor-map state', () => {
  it('starts from an empty seat set so the map shows a skeleton, not placeholder PCs', () => {
    const state = createFixtureFloorMapState();

    expect(state.source).toBe('fixture');
    expect(state.loadStatus).toBe('idle');
    expect(state.seats).toEqual([]);
  });

  it('maps backend floor-map DTOs to sorted operator seat summaries', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
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
    }, t);

    expect(state.source).toBe('backend');
    expect(state.branchName).toBe('Demo Branch');
    expect(state.seats.map((seat) => seat.name)).toEqual(['PC-01', 'PC-02']);
    // Сессия идёт, но ПК без связи → серый «нет связи» (бывший красный blocking), а время сессии
    // на плитке сохраняется (без потери активной сессии).
    expect(state.seats[0]).toMatchObject({
      tone: 'offline',
      command: 'No route',
      remaining: 'осталось 1 ч 01 мин'
    });
    expect(state.seats[1]).toMatchObject({
      tone: 'ready',
      stateLabel: 'Свободно',
      command: 'Idle'
    });
  });

  it('shows live accrued cost for an open-tab session instead of a countdown', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [
        createSeat({
          state: 'Active',
          activeSessionId: '33333333-3333-3333-3333-333333333333',
          remainingSeconds: null,
          accruedCostMinorUnits: 2250,
          currencyCode: 'TJS'
        })
      ]
    }, t);

    expect(state.seats[0]).toMatchObject({
      tone: 'active',
      remaining: '≈ 22,5 с.',
      accruedCostMinorUnits: 2250,
      currencyCode: 'TJS'
    });
  });

  // У консоли нет агента: сердцебиения нет никогда, и место не должно гореть «нет связи».
  it('maps a console seat to «free», never to «нет связи»', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [createSeat({ state: 'Free', isDeviceOnline: null, isConsole: true, lastHeartbeatAtUtc: null })]
    }, t);

    expect(state.seats[0]).toMatchObject({ tone: 'ready', isConsole: true });
  });

  // ПК сверх бесплатного тарифа: свободный — серый «Вне тарифа», а не «готов»; идущая сессия
  // остаётся в своём цвете, пока не кончится.
  it('greys out a free PC outside the free plan and keeps a running session as it is', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [
        createSeat({ state: 'Free', isOutsidePlan: true }),
        createSeat({ seatId: 'seat-2', state: 'Active', activeSessionId: 'session-2', isOutsidePlan: true })
      ]
    }, t);

    expect(state.seats[0]).toMatchObject({ tone: 'service', stateLabel: 'Вне тарифа', isOutsidePlan: true });
    expect(state.seats[1]).toMatchObject({ tone: 'active', isOutsidePlan: true });
    expect(isSeatReadyForGuest(createSeat({ state: 'Free', isOutsidePlan: true }))).toBe(false);
  });

  it('maps a maintenance PC to the calm "service" tone, separate from the «нет связи» bucket', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [createSeat({ state: 'Maintenance', isDeviceOnline: false })]
    }, t);

    // Обслуживание побеждает офлайн-устройство: спокойный серый «service», тело «Обслуживание».
    expect(state.seats[0]).toMatchObject({
      tone: 'service',
      stateLabel: 'Обслуживание',
      remaining: 'Обслуживание'
    });
  });

  it('surfaces the real player name, tariff and session start from the backend DTO', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [
        createSeat({
          state: 'Active',
          activeSessionId: '33333333-3333-3333-3333-333333333333',
          remainingSeconds: 3600,
          playerDisplayName: 'Иван Петров',
          tariffName: 'VIP час',
          sessionStartedAtUtc: '2026-05-21T08:48:00Z'
        })
      ]
    }, t);

    expect(state.seats[0]).toMatchObject({
      player: 'Иван Петров',
      playerDisplayName: 'Иван Петров',
      tariffName: 'VIP час',
      sessionStartedAtUtc: '2026-05-21T08:48:00Z'
    });
  });

  it('falls back to the generic player placeholder for a guest session with no account name', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [
        createSeat({
          state: 'Active',
          activeSessionId: '33333333-3333-3333-3333-333333333333',
          remainingSeconds: 3600,
          playerDisplayName: null,
          tariffName: null
        })
      ]
    }, t);

    expect(state.seats[0].player).toBe(t('op.floor.player.active'));
    expect(state.seats[0].playerDisplayName ?? null).toBeNull();
    expect(state.seats[0].tariffName ?? null).toBeNull();
  });

  it('applies SignalR device status by device id and updates free seats', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [createSeat({ state: 'Locked', isDeviceLocked: true })]
    }, t);

    const nextSeats = applyDeviceStatusToSeats(state.seats, {
      organizationId,
      branchId,
      deviceId,
      machineName: 'ignored',
      isOnline: true,
      isLocked: false,
      observedAtUtc: '2026-05-21T10:00:00Z'
    }, t);

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
      zones: [],
      seats: [
        createSeat({
          state: 'Active',
          activeSessionId: '33333333-3333-3333-3333-333333333333',
          remainingSeconds: 900
        })
      ]
    }, t);

    const nextSeats = applyDeviceStatusToSeats(state.seats, {
      organizationId,
      branchId,
      deviceId,
      machineName: 'PC-01',
      isOnline: false,
      isLocked: false,
      observedAtUtc: '2026-05-21T10:00:00Z'
    }, t);

    // ПК без связи → серый «нет связи» (бывший красный blocking), но время сессии сохраняется.
    expect(nextSeats[0]).toMatchObject({
      tone: 'offline',
      stateLabel: 'Нет связи',
      remaining: 'осталось 15 мин',
      command: 'No route'
    });
  });

  it('ignores manager workstation realtime status even when the machine name matches a seat', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [createSeat({ state: 'Locked', isDeviceLocked: true })]
    }, t);

    const nextSeats = applyDeviceStatusToSeats(state.seats, {
      organizationId,
      branchId,
      deviceId: '22222222-2222-2222-2222-222222222222',
      machineName: 'PC-01',
      isOnline: true,
      isLocked: false,
      observedAtUtc: '2026-05-21T10:00:00Z',
      role: 'manager_workstation'
    }, t);

    expect(nextSeats).toBe(state.seats);
  });

  it('ticks active session remaining time from the backend snapshot deadline', () => {
    const loadedAtMs = 1000;
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [
        createSeat({
          state: 'Active',
          activeSessionId: '33333333-3333-3333-3333-333333333333',
          remainingSeconds: 3600
        })
      ]
    }, t, loadedAtMs);

    const nextState = refreshFloorMapRemaining(state, t, loadedAtMs + 121_000);

    expect(nextState.seats[0]).toMatchObject({
      remainingSeconds: 3479,
      remaining: 'осталось 58 мин'
    });
  });

  it('lets remaining time go negative past the deadline and reads it as "time up"', () => {
    const loadedAtMs = 1000;
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [
        createSeat({
          state: 'Active',
          activeSessionId: '33333333-3333-3333-3333-333333333333',
          remainingSeconds: 60
        })
      ]
    }, t, loadedAtMs);

    // 121s later the paid minute is long gone — overtime, not "0 left".
    const nextState = refreshFloorMapRemaining(state, t, loadedAtMs + 121_000);

    expect(nextState.seats[0].remainingSeconds).toBeLessThan(0);
    expect(nextState.seats[0].remaining).toBe('Время вышло');
  });

  it('defaults zones to an empty array for fixtures', () => {
    const state = createFixtureFloorMapState();
    expect(state.zones).toEqual([]);
  });

  // Зона у места приходит идентификатором, а не только именем: два зала могут называться
  // одинаково, а карта группирует места по залам.
  it('carries zoneId on each seat', () => {
    const state = mapFloorMapDtoToState({
      branchId,
      branchName: 'Demo Branch',
      zones: [],
      seats: [createSeat({ seatName: 'PC-01', sortOrder: 10, state: 'Locked' })]
    }, t, 0);

    expect(state.seats[0].zoneId).toBe('bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb');
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
