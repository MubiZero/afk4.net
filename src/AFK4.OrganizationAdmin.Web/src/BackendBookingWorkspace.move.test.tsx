import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { OperatorFloorMapState } from './floorMapState';
import type { SeatSummary } from './operatorData';
import type { OperatorBackendContext } from './operatorTypes';
import { ToastProvider } from './operatorToast';
import { BackendBookingWorkspace } from './BackendBookingWorkspace';

const originalFetch = globalThis.fetch;

afterEach(() => {
  cleanup();
  globalThis.fetch = originalFetch;
});

function seat(id: string, name: string): SeatSummary {
  return {
    id, zone: 'Зал A', name, tone: 'ready', stateLabel: 'Свободен', player: '', remaining: '',
    device: name, command: '', app: '', activeSessionId: null
  };
}

const floorMap: OperatorFloorMapState = {
  branchId: 'branch-1', branchName: 'Тестовый клуб', seats: [seat('a', 'PC-01'), seat('b', 'PC-02')],
  zones: [], source: 'backend', loadStatus: 'ready', error: null,
  isOffline: false, cachedAtMs: null
};

function startBackend(): OperatorBackendContext {
  return {
    config: { runtime: 'browser-test', shellMode: 'test', platformBaseUrl: 'http://localhost:5074/', currencyCode: 'TJS' },
    branchId: 'branch-1',
    session: {
      staffUserId: 'staff-1', organizationId: 'org-1', displayName: 'Operator', accessToken: 'token',
      accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'refresh-token',
      refreshTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
      branchIds: ['branch-1'], activeBranchId: 'branch-1',
      permissions: ['organization.reservations.view', 'organization.reservations.manage', 'organization.sessions.view']
    }
  };
}

function confirmedReservation(overrides: Record<string, unknown> = {}) {
  return {
    reservationId: 'reservation-start', version: 3, state: 'confirmed', source: 'operator',
    startsAtUtc: new Date(Date.now() + 3_600_000).toISOString(), durationMinutes: 60, customerName: 'Reserved guest',
    phoneNumber: '+992900000000', playerAccountId: null, seatId: 'a', seatName: 'PC-01',
    zoneName: 'Зал A', note: '', startedSessionId: null, ...overrides
  };
}

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

// Перенос брони. Список мест раньше брался из текущего состояния зала, даже если бронь на завтра:
// место, занятое сейчас и свободное завтра, пряталось, а свободное сейчас и занятое завтра чужой
// бронью — предлагалось и отклонялось сервером. Теперь список спрашивается у сервера на окно брони.
describe('BackendBookingWorkspace · куда перенести бронь', () => {
  const hall: OperatorFloorMapState = {
    ...floorMap,
    seats: [
      seat('a', 'PC-01'),
      { ...seat('b', 'PC-02'), tone: 'active', stateLabel: 'В сессии', activeSessionId: 'session-b' },
      { ...seat('c', 'PC-03'), tone: 'offline', stateLabel: 'Нет связи' },
      seat('d', 'PC-04')
    ]
  };

  // Карточка перерисовывается, пока догружаются день и список мест, поэтому поле ищется заново на
  // каждой попытке, а не держится старым узлом. Ищется селектором, а не по роли: разбор ролей по
  // всему таймлайну на каждой попытке дорог, а на загруженной машине экран и без того рисует ответ
  // списка мест за секунды. Роль и подпись проверяются один раз — у найденного поля.
  async function enabledMoveSelect(): Promise<HTMLElement> {
    const find = () => document.querySelector<HTMLButtonElement>(
      '.booking-drawer button[role="combobox"][aria-label="Перенести на место"]');
    await waitFor(() => {
      const move = find();
      expect(move).not.toBeNull();
      expect(move).not.toBeDisabled();
    }, { timeout: 15_000 });
    return within(screen.getByRole('dialog', { name: 'Бронь' })).getByRole('combobox', { name: 'Перенести на место' });
  }

  function mount(startsAtUtc: Date, freeSeatIds: string[]) {
    const freeSeatQueries: URLSearchParams[] = [];
    const moves: Record<string, unknown>[] = [];
    const reservation = confirmedReservation({
      reservationId: 'reservation-move', seatId: 'a', seatName: 'PC-01', customerName: 'Переносимый гость',
      startsAtUtc: startsAtUtc.toISOString(), durationMinutes: 60
    });
    globalThis.fetch = (async (input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/reservations') && init?.method === 'GET') return json({ reservations: [reservation], limit: 40 });
      if (url.pathname.endsWith('/sessions')) return json({ sessions: [], limit: 40 });
      if (url.pathname.endsWith('/reservations/free-seats')) {
        freeSeatQueries.push(url.searchParams);
        return json({
          startsAtUtc: url.searchParams.get('startsAtUtc'),
          endsAtUtc: url.searchParams.get('endsAtUtc'),
          freeSeatIds
        });
      }
      if (url.pathname.endsWith('/reservations/reservation-move') && init?.method === 'PATCH') {
        moves.push(JSON.parse(String(init.body)) as Record<string, unknown>);
        return json({ ...reservation, seatId: 'b', version: 4 });
      }
      throw new Error(`Unexpected request: ${init?.method ?? 'GET'} ${url.pathname}`);
    }) as typeof fetch;
    render(
      <I18nProvider><ToastProvider>
        <BackendBookingWorkspace
          floorMap={hall}
          backend={startBackend()}
          currencyCode="TJS"
          onOpenSeat={() => {}}
          openReservation={{ reservationId: 'reservation-move', startsAtUtc: startsAtUtc.toISOString() }}
        />
      </ToastProvider></I18nProvider>
    );
    return { freeSeatQueries, moves };
  }

  it('завтрашнюю бронь переносит на место, занятое сейчас, но свободное завтра', async () => {
    const startsAtUtc = new Date();
    startsAtUtc.setDate(startsAtUtc.getDate() + 1);
    startsAtUtc.setHours(18, 0, 0, 0);
    // Сервер: PC-02 завтра свободно, PC-04 завтра занято чужой бронью.
    const { freeSeatQueries, moves } = mount(startsAtUtc, ['a', 'b', 'c']);

    const move = await enabledMoveSelect();

    const query = freeSeatQueries.at(-1)!;
    expect(query.get('startsAtUtc')).toBe(startsAtUtc.toISOString());
    expect(query.get('endsAtUtc')).toBe(new Date(startsAtUtc.getTime() + 3_600_000).toISOString());
    expect(query.get('excludeReservationId')).toBe('reservation-move');

    fireEvent.click(move);
    const options = screen.getAllByRole('option').map((option) => option.textContent);
    expect(options).toEqual(['Зал A · PC-02', 'Зал A · PC-03']);

    fireEvent.click(screen.getByRole('option', { name: 'Зал A · PC-02' }));
    await waitFor(() => expect(moves).toHaveLength(1), { timeout: 15_000 });
    expect(moves[0]).toMatchObject({ seatId: 'b', expectedVersion: 3 });
  });

  it('бронь, которая уже идёт, переносит только туда, где можно сесть сейчас', async () => {
    const startsAtUtc = new Date(Date.now() - 10 * 60_000);
    mount(startsAtUtc, ['a', 'b', 'c', 'd']);

    const move = await enabledMoveSelect();

    fireEvent.click(move);
    const options = screen.getAllByRole('option').map((option) => option.textContent);
    expect(options).toEqual(['Зал A · PC-04']);
  });
});
