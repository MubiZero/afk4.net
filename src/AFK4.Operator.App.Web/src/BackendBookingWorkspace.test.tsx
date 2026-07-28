import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { OperatorFloorMapState } from './floorMapState';
import type { SeatSummary } from './operatorData';
import type { OperatorBackendContext } from './operatorTypes';
import { ToastProvider } from './operatorToast';
import { BackendBookingWorkspace, buildReservationStartRequest, isReservationStartOutcomeAmbiguous } from './BackendBookingWorkspace';
import { PlatformApiError } from './platformApi';
import { createSessionStartSelection } from './session/SessionStartForm';

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
  zones: [], walls: [], etag: null, source: 'backend', loadStatus: 'ready', error: null,
  isOffline: false, cachedAtMs: null
};

function startBackend(): OperatorBackendContext {
  return {
    config: { runtime: 'browser-test', shellMode: 'test', platformBaseUrl: 'http://localhost:5074/', currencyCode: 'TJS' },
    branchId: 'branch-1',
    session: {
      staffUserId: 'staff-1', organizationId: 'org-1', displayName: 'Operator', accessToken: 'token',
      accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'refresh-token',
      refreshTokenExpiresAtUtc: '2026-07-16T00:00:00Z',
      branchIds: ['branch-1'], activeBranchId: 'branch-1',
      permissions: ['organization.reservations.view', 'organization.reservations.manage', 'organization.sessions.start', 'organization.sessions.view', 'organization.tariffs.view', 'organization.billing.view']
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

describe('BackendBookingWorkspace modifier draft transitions', () => {
  it('classifies transport and retryable HTTP failures as ambiguous, but domain 4xx as determined', () => {
    expect(isReservationStartOutcomeAmbiguous(new TypeError('network lost'))).toBe(true);
    for (const status of [408, 425, 429, 500, 502, 503, 504]) {
      expect(isReservationStartOutcomeAmbiguous(new PlatformApiError('retryable', status, 'Failure', '{}'))).toBe(true);
    }
    for (const status of [400, 401, 403, 404, 409, 422]) {
      expect(isReservationStartOutcomeAmbiguous(new PlatformApiError('domain', status, 'Failure', '{}'))).toBe(false);
    }
  });
  it('builds distinct wallet, package, postpaid and comp payloads without changing reservation identity', () => {
    const base = createSessionStartSelection('prepaid_wallet');
    expect(buildReservationStartRequest('org-1', 7, 'key-wallet', { ...base, tariffVersionId: 'tariff-1' })).toMatchObject({
      organizationId: 'org-1', expectedVersion: 7, idempotencyKey: 'key-wallet', billingMode: 'prepaid_wallet', tariffVersionId: 'tariff-1'
    });
    expect(buildReservationStartRequest('org-1', 7, 'key-package', { ...base, billingMode: 'package', tariffVersionId: null, playerPackageId: 'pkg-1' })).toMatchObject({
      billingMode: 'package', playerPackageId: 'pkg-1'
    });
    expect(buildReservationStartRequest('org-1', 7, 'key-postpaid', { ...base, billingMode: 'postpaid_debt', tariffVersionId: 'tariff-1' })).toMatchObject({
      billingMode: 'postpaid_debt', tariffVersionId: 'tariff-1'
    });
    expect(buildReservationStartRequest('org-1', 7, 'key-comp', {
      ...base, billingMode: 'guest', tariffVersionId: 'tariff-1', durationMode: 'fixed', durationMinutes: 60,
      isComp: true, compReason: 'manager courtesy'
    })).toMatchObject({ billingMode: '', isComp: true, compReason: 'manager courtesy', tariffVersionId: 'tariff-1' });
  });
  it('после закрытия старой формы Ctrl-click начинает чистый draft, а повторный click снимает место', async () => {
    const result = render(
      <I18nProvider>
        <ToastProvider>
          <BackendBookingWorkspace floorMap={floorMap} backend={null} currencyCode="TJS" onOpenSeat={() => {}} />
        </ToastProvider>
      </I18nProvider>
    );

    await waitFor(() => expect(result.container.querySelectorAll('.booking-row-track')).toHaveLength(2));
    const tracks = result.container.querySelectorAll<HTMLElement>('.booking-row-track');
    for (const track of tracks) {
      track.getBoundingClientRect = () => ({ left: 0, top: 0, width: 2400, height: 38, right: 2400, bottom: 38, x: 0, y: 0, toJSON: () => ({}) }) as DOMRect;
    }

    // Старый закрытый draft A@10:00 с заполненным гостевым контактом.
    fireEvent.click(tracks[0], { clientX: 1000 });
    const oldDrawer = await screen.findByRole('dialog', { name: 'Новая бронь' });
    fireEvent.change(within(oldDrawer).getByRole('combobox', { name: 'Поиск клиентов' }), { target: { value: 'Старый клиент' } });
    fireEvent.change(within(oldDrawer).getByRole('textbox', { name: 'Телефон' }), { target: { value: '93 111 22 33' } });
    fireEvent.click(within(oldDrawer).getByRole('button', { name: 'Отмена' }));

    // Ctrl-click B@15:00 вне create должен начать заново, не продолжить закрытый draft.
    fireEvent.click(tracks[1], { clientX: 1500, ctrlKey: true });
    const freshDrawer = await screen.findByRole('dialog', { name: 'Новая бронь' });
    const chips = freshDrawer.querySelectorAll('.booking-seat-chip');
    expect(chips).toHaveLength(1);
    expect(chips[0]).toHaveTextContent('PC-02');
    expect(freshDrawer).not.toHaveTextContent('PC-01');
    expect(within(freshDrawer).getByRole<HTMLInputElement>('combobox', { name: 'Поиск клиентов' }).value).toBe('');
    expect(within(freshDrawer).getByRole<HTMLInputElement>('textbox', { name: 'Телефон' }).value).toBe('');
    expect(freshDrawer).toHaveTextContent(/15:00–16:00/);

    // В уже активном create drawer тот же modifier-click — именно toggle-off.
    fireEvent.click(tracks[1], { clientX: 1500, metaKey: true });
    expect(freshDrawer.querySelector('.booking-seat-chip')).toBeNull();
    expect(within(freshDrawer).getByRole('button', { name: 'Создать бронь' })).toBeDisabled();
  });

  it('sends the selected version and refreshes authoritative reservations without closing details on conflict', async () => {
    let reservationReads = 0;
    const confirmBodies: Record<string, unknown>[] = [];
    const startsAtUtc = new Date();
    startsAtUtc.setHours(16, 0, 0, 0);
    const reservation = {
      reservationId: 'reservation-1',
      reservationGroupId: null,
      organizationId: 'org-1',
      branchId: 'branch-1',
      seatId: 'a',
      seatName: 'PC-01',
      zoneName: 'Зал A',
      customerName: 'Guest stale',
      phoneNumber: '+992900000001',
      startsAtUtc: startsAtUtc.toISOString(),
      durationMinutes: 60,
      state: 'pending',
      source: 'online',
      note: '',
      version: 7
    };
    globalThis.fetch = (async (input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/reservations') && init?.method === 'GET') {
        reservationReads += 1;
        const authoritative = {
          ...reservation,
          version: reservationReads === 1 ? 7 : reservationReads === 2 ? 8 : 9,
          state: reservationReads >= 3 ? 'confirmed' : 'pending'
        };
        return new Response(JSON.stringify({ reservations: [authoritative], limit: 40 }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        });
      }
      if (url.pathname.endsWith('/sessions/timeline')) {
        return new Response(JSON.stringify({ sessions: [], limit: 40 }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        });
      }
      if (url.pathname.endsWith('/reservations/reservation-1/confirm')) {
        confirmBodies.push(JSON.parse(String(init?.body)) as Record<string, unknown>);
        if (confirmBodies.length === 1) {
          return new Response(JSON.stringify({
            error: 'Reservation changed since it was loaded.',
            code: 'version_conflict',
            currentVersion: 8
          }), {
            status: 409,
            statusText: 'Conflict',
            headers: { 'Content-Type': 'application/json' }
          });
        }
        return new Response(JSON.stringify({ ...reservation, state: 'confirmed', version: 9 }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' }
        });
      }
      throw new Error(`Unexpected request: ${init?.method ?? 'GET'} ${url.pathname}`);
    }) as typeof fetch;
    const backend: OperatorBackendContext = {
      config: {
        runtime: 'browser-test',
        shellMode: 'test',
        platformBaseUrl: 'http://localhost:5074/',
        currencyCode: 'TJS'
      },
      branchId: 'branch-1',
      session: {
        staffUserId: 'staff-1',
        organizationId: 'org-1',
        displayName: 'Operator',
        accessToken: 'test-token',
        accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
        refreshToken: 'refresh-token',
        refreshTokenExpiresAtUtc: '2026-07-16T00:00:00Z',
        branchIds: ['branch-1'],
        activeBranchId: 'branch-1',
        permissions: ['organization.reservations.view', 'organization.reservations.manage']
      }
    };

    render(
      <I18nProvider>
        <ToastProvider>
          <BackendBookingWorkspace floorMap={floorMap} backend={backend} currencyCode="TJS" onOpenSeat={() => {}} />
        </ToastProvider>
      </I18nProvider>
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Guest stale' }));
    const details = await screen.findByRole('dialog', { name: 'Бронь' });
    fireEvent.click(within(details).getByRole('button', { name: 'Принять' }));

    await waitFor(() => expect(confirmBodies[0]).toEqual({ organizationId: 'org-1', expectedVersion: 7 }));
    await waitFor(() => expect(reservationReads).toBe(2));
    const refreshedDetails = screen.getByRole('dialog', { name: 'Бронь' });
    fireEvent.click(within(refreshedDetails).getByRole('button', { name: 'Принять' }));

    await waitFor(() => expect(confirmBodies[1]).toEqual({ organizationId: 'org-1', expectedVersion: 8 }));
    await waitFor(() => expect(reservationReads).toBe(3));
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(reservationReads).toBe(3);
    expect(screen.getByRole('dialog', { name: 'Бронь' })).toBeInTheDocument();
  });

  it('starts a confirmed reservation once, opens its authoritative seat, and refreshes reservations', async () => {
    const startCalls: Record<string, unknown>[] = [];
    let reservationReads = 0;
    const startsAtUtc = new Date();
    startsAtUtc.setHours(12, 0, 0, 0);
    const reservation = {
      reservationId: 'reservation-start', version: 3, state: 'confirmed', source: 'operator',
      startsAtUtc: startsAtUtc.toISOString(), durationMinutes: 60, customerName: 'Reserved guest',
      phoneNumber: '+992900000000', playerAccountId: null, seatId: 'a', seatName: 'PC-01',
      zoneName: 'Зал A', note: '', startedSessionId: null
    };
    globalThis.fetch = (async (input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/reservations') && init?.method === 'GET') {
        reservationReads += 1;
        return new Response(JSON.stringify({ reservations: [reservation], limit: 40 }), { status: 200, headers: { 'Content-Type': 'application/json' } });
      }
      if (url.pathname.endsWith('/sessions/timeline')) {
        return new Response(JSON.stringify({ sessions: [], limit: 40 }), { status: 200, headers: { 'Content-Type': 'application/json' } });
      }
      if (url.pathname.endsWith('/tariffs/options')) {
        return new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } });
      }
      if (url.pathname.endsWith('/reservations/reservation-start/start-session')) {
        startCalls.push(JSON.parse(String(init?.body)) as Record<string, unknown>);
        return new Response(JSON.stringify({
          reservation: { ...reservation, version: 4, state: 'seated', startedSessionId: 'session-1' },
          session: { idempotencyKey: 'same', session: { sessionId: 'session-1', seatId: 'a' }, deviceCommands: [] }
        }), { status: 200, headers: { 'Content-Type': 'application/json' } });
      }
      throw new Error(`Unexpected request: ${init?.method ?? 'GET'} ${url.pathname}`);
    }) as typeof fetch;
    const backend: OperatorBackendContext = {
      config: { runtime: 'browser-test', shellMode: 'test', platformBaseUrl: 'http://localhost:5074/', currencyCode: 'TJS' },
      branchId: 'branch-1',
      session: {
        staffUserId: 'staff-1', organizationId: 'org-1', displayName: 'Operator', accessToken: 'token',
        accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'refresh-token',
        refreshTokenExpiresAtUtc: '2026-07-16T00:00:00Z',
        branchIds: ['branch-1'], activeBranchId: 'branch-1',
        permissions: ['organization.reservations.view', 'organization.reservations.manage', 'organization.sessions.start', 'organization.sessions.view', 'organization.tariffs.view', 'organization.billing.view']
      }
    };
    const onOpenSeat = mock(() => {});
    render(<I18nProvider><ToastProvider><BackendBookingWorkspace floorMap={floorMap} backend={backend} currencyCode="TJS" onOpenSeat={onOpenSeat} /></ToastProvider></I18nProvider>);

    fireEvent.click(await screen.findByRole('button', { name: 'Reserved guest' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Бронь' })).getByRole('button', { name: 'Начать сессию' }));
    const startDialog = screen.getByRole('dialog', { name: 'Запуск забронированной сессии' });
    fireEvent.click(within(startDialog).getByRole('button', { name: 'Начать сессию' }));

    await waitFor(() => expect(onOpenSeat).toHaveBeenCalledWith('a'));
    expect(startCalls).toHaveLength(1);
    expect(startCalls[0]).toMatchObject({ expectedVersion: 3, durationMode: 'open' });
    await waitFor(() => expect(reservationReads).toBeGreaterThan(1));
  });

  it('keeps an ambiguous start attempt immutable, blocks close routes, and creates a new key only after New attempt', async () => {
    const bodies: Record<string, unknown>[] = [];
    const reservation = confirmedReservation();
    let startCall = 0;
    globalThis.fetch = (async (input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/reservations') && init?.method === 'GET') return json({ reservations: [reservation], limit: 40 });
      if (url.pathname.endsWith('/sessions/timeline')) return json({ sessions: [], limit: 40 });
      if (url.pathname.endsWith('/tariffs/options')) return json([]);
      if (url.pathname.endsWith('/start-session')) {
        bodies.push(JSON.parse(String(init?.body)) as Record<string, unknown>);
        startCall += 1;
        if (startCall <= 2) throw new TypeError('network lost after send');
        return json({ reservation: { ...reservation, version: 4, state: 'seated', startedSessionId: 'session-1' }, session: { session: { sessionId: 'session-1', seatId: 'a' } } });
      }
      throw new Error(`Unexpected request: ${init?.method ?? 'GET'} ${url.pathname}`);
    }) as typeof fetch;
    const onOpenSeat = mock(() => {});
    render(<I18nProvider><ToastProvider><BackendBookingWorkspace floorMap={floorMap} backend={startBackend()} currencyCode="TJS" onOpenSeat={onOpenSeat} /></ToastProvider></I18nProvider>);

    fireEvent.click(await screen.findByRole('button', { name: 'Reserved guest' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Бронь' })).getByRole('button', { name: 'Начать сессию' }));
    let modal = screen.getByRole('dialog', { name: 'Запуск забронированной сессии' });
    fireEvent.click(within(modal).getByRole('button', { name: 'Начать сессию' }));
    await waitFor(() => expect(bodies).toHaveLength(1));
    await waitFor(() => expect(screen.getByRole('dialog', { name: 'Запуск забронированной сессии' })).toBeInTheDocument());

    modal = screen.getByRole('dialog', { name: 'Запуск забронированной сессии' });
    expect(within(modal).getByRole('button', { name: /2 ч/ })).toBeDisabled();
    expect(within(modal).getAllByRole('button', { name: 'Отмена' }).every((button) => button.hasAttribute('disabled'))).toBe(true);
    fireEvent.keyDown(document, { key: 'Escape' });
    fireEvent.pointerDown(modal.parentElement!);
    expect(screen.getByRole('dialog', { name: 'Запуск забронированной сессии' })).toBeInTheDocument();

    fireEvent.click(within(modal).getByRole('button', { name: 'Начать сессию' }));
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[1]).toEqual(bodies[0]);

    fireEvent.click(within(modal).getByRole('button', { name: 'Новая попытка' }));
    fireEvent.click(within(modal).getByRole('button', { name: /2 ч/ }));
    fireEvent.click(within(modal).getByRole('button', { name: 'Начать сессию' }));
    await waitFor(() => expect(onOpenSeat).toHaveBeenCalledWith('a'));
    expect(bodies).toHaveLength(3);
    expect(bodies[2].idempotencyKey).not.toBe(bodies[0].idempotencyKey);
    expect(bodies[2]).toMatchObject({ durationMode: 'fixed', durationMinutes: 120 });
  });

  it('keeps an HTTP 500 start unresolved and retries the exact immutable request', async () => {
    const bodies: Record<string, unknown>[] = [];
    const reservation = confirmedReservation();
    globalThis.fetch = (async (input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/reservations') && init?.method === 'GET') return json({ reservations: [reservation], limit: 40 });
      if (url.pathname.endsWith('/sessions/timeline')) return json({ sessions: [], limit: 40 });
      if (url.pathname.endsWith('/tariffs/options')) return json([]);
      if (url.pathname.endsWith('/start-session')) {
        bodies.push(JSON.parse(String(init?.body)) as Record<string, unknown>);
        if (bodies.length === 1) return json({ code: 'internal_error' }, 500);
        return json({ reservation: { ...reservation, version: 4, state: 'seated', startedSessionId: 'session-500' }, session: { session: { sessionId: 'session-500', seatId: 'a' } } });
      }
      throw new Error(`Unexpected request: ${init?.method ?? 'GET'} ${url.pathname}`);
    }) as typeof fetch;
    const onOpenSeat = mock(() => {});
    render(<I18nProvider><ToastProvider><BackendBookingWorkspace floorMap={floorMap} backend={startBackend()} currencyCode="TJS" onOpenSeat={onOpenSeat} /></ToastProvider></I18nProvider>);
    fireEvent.click(await screen.findByRole('button', { name: 'Reserved guest' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Бронь' })).getByRole('button', { name: 'Начать сессию' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Запуск забронированной сессии' })).getByRole('button', { name: 'Начать сессию' }));
    await waitFor(() => expect(bodies).toHaveLength(1));
    const modal = screen.getByRole('dialog', { name: 'Запуск забронированной сессии' });
    expect(within(modal).getByRole('button', { name: /2 ч/ })).toBeDisabled();
    expect(within(modal).getAllByRole('button', { name: 'Отмена' }).every((button) => button.hasAttribute('disabled'))).toBe(true);
    fireEvent.click(within(modal).getByRole('button', { name: 'Начать сессию' }));
    await waitFor(() => expect(onOpenSeat).toHaveBeenCalledWith('a'));
    expect(bodies).toHaveLength(2);
    expect(bodies[1]).toEqual(bodies[0]);
  });

  it('recovers an ambiguously committed start from refreshed startedSessionId without a second start', async () => {
    const reservation = confirmedReservation();
    let reads = 0;
    let starts = 0;
    globalThis.fetch = (async (input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/reservations') && init?.method === 'GET') {
        reads += 1;
        return json({ reservations: [{ ...reservation, ...(reads > 1 ? { version: 4, state: 'seated', startedSessionId: 'session-recovered' } : {}) }], limit: 40 });
      }
      if (url.pathname.endsWith('/sessions/timeline')) return json({ sessions: [], limit: 40 });
      if (url.pathname.endsWith('/tariffs/options')) return json([]);
      if (url.pathname.endsWith('/start-session')) { starts += 1; throw new TypeError('response lost'); }
      throw new Error(`Unexpected request: ${init?.method ?? 'GET'} ${url.pathname}`);
    }) as typeof fetch;
    const onOpenSeat = mock(() => {});
    render(<I18nProvider><ToastProvider><BackendBookingWorkspace floorMap={floorMap} backend={startBackend()} currencyCode="TJS" onOpenSeat={onOpenSeat} /></ToastProvider></I18nProvider>);
    fireEvent.click(await screen.findByRole('button', { name: 'Reserved guest' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Бронь' })).getByRole('button', { name: 'Начать сессию' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Запуск забронированной сессии' })).getByRole('button', { name: 'Начать сессию' }));
    await waitFor(() => expect(onOpenSeat).toHaveBeenCalledWith('a'));
    expect(starts).toBe(1);
  });

  it('refreshes a 409 conflict and submits the current reservation version with a fresh key', async () => {
    const initial = confirmedReservation();
    const bodies: Record<string, unknown>[] = [];
    let reads = 0;
    globalThis.fetch = (async (input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/reservations') && init?.method === 'GET') {
        reads += 1;
        return json({ reservations: [{ ...initial, version: reads > 1 ? 4 : 3 }], limit: 40 });
      }
      if (url.pathname.endsWith('/sessions/timeline')) return json({ sessions: [], limit: 40 });
      if (url.pathname.endsWith('/tariffs/options')) return json([]);
      if (url.pathname.endsWith('/start-session')) {
        bodies.push(JSON.parse(String(init?.body)) as Record<string, unknown>);
        if (bodies.length === 1) return json({ code: 'version_conflict', currentVersion: 4 }, 409);
        return json({ reservation: { ...initial, version: 5, state: 'seated', startedSessionId: 'session-2' }, session: { session: { sessionId: 'session-2', seatId: 'a' } } });
      }
      throw new Error(`Unexpected request: ${init?.method ?? 'GET'} ${url.pathname}`);
    }) as typeof fetch;
    const onOpenSeat = mock(() => {});
    render(<I18nProvider><ToastProvider><BackendBookingWorkspace floorMap={floorMap} backend={startBackend()} currencyCode="TJS" onOpenSeat={onOpenSeat} /></ToastProvider></I18nProvider>);
    fireEvent.click(await screen.findByRole('button', { name: 'Reserved guest' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Бронь' })).getByRole('button', { name: 'Начать сессию' }));
    fireEvent.click(within(screen.getByRole('dialog', { name: 'Запуск забронированной сессии' })).getByRole('button', { name: 'Начать сессию' }));
    await waitFor(() => expect(reads).toBeGreaterThan(1));
    const modal = screen.getByRole('dialog', { name: 'Запуск забронированной сессии' });
    fireEvent.click(within(modal).getByRole('button', { name: 'Начать сессию' }));
    await waitFor(() => expect(onOpenSeat).toHaveBeenCalledWith('a'));
    expect(bodies[0]).toMatchObject({ expectedVersion: 3 });
    expect(bodies[1]).toMatchObject({ expectedVersion: 4 });
    expect(bodies[1].idempotencyKey).not.toBe(bodies[0].idempotencyKey);
  });
});
