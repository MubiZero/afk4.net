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
  zones: [], walls: [], etag: null, source: 'backend', loadStatus: 'ready', error: null,
  isOffline: false, cachedAtMs: null
};

describe('BackendBookingWorkspace modifier draft transitions', () => {
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
        accessTokenExpiresAtUtc: '2026-07-15T00:00:00Z',
        refreshTokenExpiresAtUtc: '2026-07-16T00:00:00Z',
        branchIds: ['branch-1'],
        activeBranchId: 'branch-1',
        permissions: ['reservations.view', 'reservations.manage']
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
});
