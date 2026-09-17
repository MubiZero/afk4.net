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
const purchasePackage = mock(async (_playerAccountId: string, _request: Record<string, unknown>) => ({ playerPackageId: 'pp1' }));
const getPackageOptions = mock(async () => [
  { packageDefinitionId: 'pkg-1', name: 'Ночной', priceMinorUnits: 12000, currencyCode: 'TJS', minutes: 300 }
]);
const getCurrentShift = mock(async () => ({ shiftId: 'shift-1' }));
const startGuestSession = mock(async (_branchId: string, _request: Record<string, unknown>) => ({ sessionId: 's1' }));
const getTariffOptions = mock(async () => [
  { tariffRuleVersionId: 'rule-1', tariffVersionId: 'ver-1', tariffId: 't1', name: 'Дневной', pricePerMinuteMinorUnits: 50, currencyCode: 'TJS', zoneId: null, zoneName: null }
]);
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
      getPlayerPackages: mock(async () => []),
      purchasePackage
    },
    settings: { getPackageOptions, getTariffOptions },
    shifts: { getCurrentShift },
    sessions: { startGuestSession },
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
  session: {
    accessToken: 't',
    organizationId: 'org',
    permissions: [
      'organization.reservations.manage',
      'organization.packages.purchase',
      'organization.sessions.start',
      'organization.tariffs.view'
    ]
  },
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

// Пакеты в карточке были только для просмотра, а продавались в Кассе — где того же человека
// приходилось искать заново. Один визит гостя превращался в два поиска на двух экранах.
describe('BackendPlayersWorkspace · продажа пакета из карточки', () => {
  afterEach(() => {
    cleanup();
    purchasePackage.mockClear();
    getPackageOptions.mockClear();
    playersSnapshotCache.clear();
  });

  it('продаёт пакет тому, чья карточка открыта', async () => {
    renderWorkspace();
    await screen.findByText('Фаррух Азизов', { selector: '.drawer-name' });
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    fireEvent.click(await screen.findByRole('menuitem', { name: 'Продать пакет' }));

    await waitFor(() => expect(getPackageOptions).toHaveBeenCalled());
    fireEvent.click(await screen.findByRole('button', { name: 'Купить пакет' }));

    await waitFor(() => expect(purchasePackage).toHaveBeenCalledTimes(1));
    expect(purchasePackage.mock.calls[0]![0]).toBe('p1');
    expect((purchasePackage.mock.calls[0]![1] as Record<string, unknown>).packageDefinitionId).toBe('pkg-1');
  });
});

// Третий поиск того же человека за визит: после кассы и после карточки оператор шёл на Карту и
// искал его снова, чтобы посадить за ПК.
describe('BackendPlayersWorkspace · посадить за ПК из карточки', () => {
  afterEach(() => {
    cleanup();
    startGuestSession.mockClear();
    getFloorMap.mockClear();
    playersSnapshotCache.clear();
  });

  it('предлагает только свободные места и сажает выбранного клиента', async () => {
    renderWorkspace();
    await screen.findByText('Фаррух Азизов', { selector: '.drawer-name' });
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    fireEvent.click(await screen.findByRole('menuitem', { name: 'Посадить за ПК' }));

    await waitFor(() => expect(getFloorMap).toHaveBeenCalled());
    const seat = await screen.findByLabelText('Место');
    expect([...seat.querySelectorAll('option')].map((option) => option.textContent)).toEqual(['Зал · PC-01']);

    // Кнопка оживает не сразу: форма ждёт список тарифов. Ждём именно его, а не отмеренную
    // секунду — на загруженном раннере секунды не хватало, и проверка падала на ровном месте.
    await waitFor(() => expect(getTariffOptions).toHaveBeenCalled());

    // Пункт меню и кнопка подтверждения называются одинаково — берём ту, что в диалоге.
    const submit = (await screen.findAllByRole('button', { name: 'Посадить за ПК' })).at(-1)!;
    await waitFor(() => expect(submit).not.toBeDisabled(), { timeout: 5000 });
    fireEvent.click(submit);

    await waitFor(() => expect(startGuestSession).toHaveBeenCalledTimes(1));
    const payload = startGuestSession.mock.calls[0]![1] as Record<string, unknown>;
    expect(payload.seatId).toBe('seat-1');
    expect(payload.playerAccountId).toBe('p1');
  });
});
