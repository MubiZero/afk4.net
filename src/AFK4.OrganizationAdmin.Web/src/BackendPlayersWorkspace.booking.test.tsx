import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';
import { playersSnapshotCache } from './players/playersSnapshot';

const person = {
  playerAccountId: 'p1',
  displayName: 'Фаррух Азизов',
  phoneNumber: '+992937380070',
  walletBalanceMinorUnits: 1000,
  debtBalanceMinorUnits: 0,
  activePackageCount: 0,
  isActive: true,
  createdAtUtc: '2026-08-01T00:00:00Z',
  lastActivityAtUtc: null,
  activePackageName: null,
  activePackageRemainingMinutes: 0,
  platformPersonId: null,
  createdFromApp: false
};

const createReservation = mock(async (_branchId: string, _request: Record<string, unknown>) => ({ reservationId: 'r1' }));
const getFloorMap = mock(async () => ({
  branchId: 'b1',
  branchName: 'Главный',
  zones: [],
  seats: [
    { seatId: 'seat-1', seatName: 'PC-01', zoneId: 'z', zoneName: 'Зал', sortOrder: 1, state: 'free', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: null, remainingSeconds: null },
    { seatId: 'seat-2', seatName: 'PC-02', zoneId: 'z', zoneName: 'Зал', sortOrder: 2, state: 'active', deviceId: null, deviceName: null, isDeviceOnline: null, isDeviceLocked: null, lastHeartbeatAtUtc: null, agentVersion: null, shellVersion: null, activeSessionId: 's1', remainingSeconds: 600 }
  ]
}));

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    players: {
      searchPlayers: mock(async () => [person]),
      getWalletSummary: mock(async () => ({
        walletBalance: { currencyCode: 'TJS', minorUnits: 1000 },
        debtBalance: { currencyCode: 'TJS', minorUnits: 0 }
      })),
      getPlayerPackages: mock(async () => [])
    },
    reservations: { create: createReservation },
    floorMap: { getFloorMap }
  })
}));

const { BackendPlayersWorkspace } = await import('./BackendPlayersWorkspace');

afterAll(() => {
  mock.module('./operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('./operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: ['organization.reservations.manage'] },
  branchId: 'b1'
};

function renderWorkspace() {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <BackendPlayersWorkspace currencyCode="TJS" backend={backend as never} />
      </ToastProvider>
    </I18nProvider>
  );
}

async function openBookingDialog() {
  renderWorkspace();
  await screen.findByText('Фаррух Азизов', { selector: '.drawer-name' });
  fireEvent.click(screen.getByRole('button', { name: "Действия с клиентом" }));
  fireEvent.click(await screen.findByRole('menuitem', { name: 'Создать бронь' }));
}

describe('BackendPlayersWorkspace · бронь из карточки клиента', () => {
  afterEach(() => {
    cleanup();
    createReservation.mockClear();
    getFloorMap.mockClear();
    playersSnapshotCache.clear();
  });

  // Раньше этот пункт меню молча заводил бронь «через 30 минут, на час, без места»: оператор
  // не видел и не выбирал ничего, а потом искал эту запись в «Бронях», чтобы дать ей место.
  it('спрашивает время и место, а не создаёт бронь по клику', async () => {
    await openBookingDialog();

    expect(await screen.findByText('Бронь для клиента')).toBeInTheDocument();
    expect(createReservation).not.toHaveBeenCalled();
  });

  it('создаёт бронь на выбранное время и место', async () => {
    await openBookingDialog();
    await screen.findByText('Бронь для клиента');
    await waitFor(() => expect(getFloorMap).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Старт'), { target: { value: '2026-09-20T18:30' } });
    fireEvent.change(screen.getByLabelText('Длительность'), { target: { value: '90' } });
    fireEvent.change(screen.getByLabelText('Место'), { target: { value: 'seat-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать бронь' }));

    await waitFor(() => expect(createReservation).toHaveBeenCalledTimes(1));
    const request = createReservation.mock.calls[0]![1] as Record<string, unknown>;
    expect(request.seatId).toBe('seat-1');
    expect(request.durationMinutes).toBe(90);
    expect(String(request.startsAtUtc)).toContain('2026-09-20');
    expect(request.playerAccountId).toBe('p1');
  });

  // Занятые места в списке не предлагаем: бронь на занятый ПК сервер отклонит, а оператор
  // узнает об этом только после отправки.
  it('предлагает только свободные места', async () => {
    await openBookingDialog();
    await waitFor(() => expect(getFloorMap).toHaveBeenCalled());

    const seat = await screen.findByLabelText('Место');
    const options = [...seat.querySelectorAll('option')].map((option) => option.textContent);
    expect(options).toEqual(['Место выберем позже', 'PC-01']);
  });
});
