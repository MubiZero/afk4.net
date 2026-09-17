import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
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
  branchId: 'branch-1', branchName: 'Тестовый клуб', seats: [seat('a', 'PC-01')],
  zones: [], source: 'backend', loadStatus: 'ready', error: null, isOffline: false, cachedAtMs: null
};

const backend: OperatorBackendContext = {
  config: { runtime: 'browser-test', shellMode: 'test', platformBaseUrl: 'http://localhost:5074/', currencyCode: 'TJS' },
  branchId: 'branch-1',
  session: {
    staffUserId: 'staff-1', organizationId: 'org-1', displayName: 'Operator', accessToken: 'token',
    accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'refresh-token',
    refreshTokenExpiresAtUtc: '2999-01-01T00:00:00Z',
    branchIds: ['branch-1'], activeBranchId: 'branch-1',
    permissions: ['organization.reservations.view', 'organization.reservations.manage']
  }
} as never;

function request(index: number) {
  return {
    reservationId: `request-${index}`, version: 1, state: 'pending', source: 'online',
    startsAtUtc: new Date(Date.now() + 3_600_000).toISOString(), durationMinutes: 60,
    customerName: `Гость ${index}`, phoneNumber: '+992937380070', playerAccountId: null,
    seatId: null, seatName: '', zoneName: '', note: 'просит место у окна', startedSessionId: null
  };
}

function json(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

function mockReservations(reservations: unknown[], limit: number) {
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input));
    if (url.pathname.endsWith('/reservations') && (init?.method ?? 'GET') === 'GET') {
      return json({ reservations, limit });
    }
    if (url.pathname.endsWith('/sessions/timeline')) return json({ sessions: [], limit });
    return json({});
  }) as typeof fetch;
}

function renderWorkspace() {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <BackendBookingWorkspace floorMap={floorMap} backend={backend} currencyCode="TJS" onOpenSeat={() => {}} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('BackendBookingWorkspace · лента заявок', () => {
  // По заявке перезванивают. Номер жил только в подсказке при наведении, и заметка брони
  // вытесняла его оттуда целиком: «просит место у окна» вместо телефона.
  it('показывает телефон заявки текстом, а не только подсказкой', async () => {
    mockReservations([request(1)], 200);
    renderWorkspace();

    expect(await screen.findByText('+992 93 738 00 70')).toBeInTheDocument();
  });

  // Сорока броней не хватает загруженному дню: остальные не доезжали до экрана молча, и день
  // выглядел свободнее, чем он есть.
  it('предупреждает, когда день не поместился в выдачу', async () => {
    mockReservations(Array.from({ length: 200 }, (_, index) => request(index)), 200);
    renderWorkspace();

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(/Показаны первые 200 броней/));
  });

  it('обычный день обходится без предупреждения', async () => {
    mockReservations([request(1)], 200);
    renderWorkspace();

    await screen.findByText('+992 93 738 00 70');
    expect(screen.queryByText(/Показаны первые/)).toBeNull();
  });
});
