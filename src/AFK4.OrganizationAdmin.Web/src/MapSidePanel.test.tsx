import { afterEach, describe, expect, it, mock } from 'bun:test';
import { act, cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import type { OperatorBackendContext, SeatActionRequest } from './operatorTypes';
import { MapSidePanel } from './MapSidePanel';

afterEach(cleanup);

function seat(overrides: Partial<SeatSummary>): SeatSummary {
  return {
    id: 'seat-1', zone: 'Зал A', name: 'PC-07', tone: 'active', stateLabel: 'В сессии',
    player: 'Активный клиент', remaining: '30 мин', device: 'PC-07 · Online · locked · Agent 0.4 · Shell 0.4',
    command: 'Lease fresh', app: 'Agent 0.4 · Shell 0.4', deviceId: 'dev-1', deviceName: 'Зал-1-ПК-07',
    isDeviceOnline: true, isDeviceLocked: true, activeSessionId: 'AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE', ...overrides
  };
}

function renderPanel(s: SeatSummary) {
  return render(
    <I18nProvider>
      <MapSidePanel seat={s} seats={[s]} currencyCode="TJS" backend={null} actionsEnabled={false} canUsePcControl={true} onSeatAction={async () => ({})} onPcControlAction={async () => ({ detail: '' })} />
    </I18nProvider>
  );
}

function backend(): OperatorBackendContext {
  return {
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
      permissions: ['organization.sessions.start', 'organization.players.view', 'organization.billing.view', 'organization.tariffs.view']
    }
  };
}

function json(value: unknown): Response {
  return new Response(JSON.stringify(value), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((next) => { resolve = next; });
  return { promise, resolve };
}

function player(playerAccountId: string, displayName: string, walletBalanceMinorUnits: number) {
  return {
    playerAccountId,
    displayName,
    phoneNumber: '+992 90 777 88 99',
    walletBalanceMinorUnits,
    debtBalanceMinorUnits: 0,
    activePackageCount: 1,
    isActive: true,
    createdAtUtc: '2026-07-01T00:00:00Z',
    lastActivityAtUtc: null,
    activePackageName: null,
    activePackageRemainingMinutes: 0, platformPersonId: null, createdFromApp: false
  };
}

function playerPackage(playerAccountId: string, playerPackageId: string, name: string) {
  return {
    playerPackageId,
    packageDefinitionId: `definition-${playerAccountId}`,
    playerAccountId,
    name,
    purchasedPrice: { currencyCode: 'TJS', minorUnits: 10_000 },
    includedSeconds: 18_000,
    bonusSeconds: 0,
    remainingIncludedSeconds: 10_800,
    remainingBonusSeconds: 0,
    purchasedAtUtc: '2026-07-01T00:00:00Z',
    expiresAtUtc: null
  };
}

function tariffOptions() {
  return [{
    tariffId: 'tariff-1', tariffVersionId: 'tariff-version-1', tariffRuleVersionId: 'standard-v1',
    name: 'Standard', currencyCode: 'TJS', pricePerMinuteMinorUnits: 50,
    minimumBillableMinutes: 15, roundingIncrementMinutes: 5
  }];
}

function openClientStartDialog(onSeatAction: (request: SeatActionRequest) => Promise<Record<string, never>> = async () => ({})) {
  const readySeat = seat({ tone: 'ready', stateLabel: 'Свободен', activeSessionId: null, hasActiveSession: false });
  render(
    <I18nProvider>
      <MapSidePanel
        seat={readySeat}
        seats={[readySeat]}
        currencyCode="TJS"
        backend={backend()}
        actionsEnabled
        canUsePcControl={false}
        onSeatAction={onSeatAction}
        onPcControlAction={async () => ({ detail: '' })}
      />
    </I18nProvider>
  );
  fireEvent.click(screen.getByRole('button', { name: 'Посадить гостя' }));
  const dialog = screen.getByRole('dialog', { name: 'Новая сессия' });
  fireEvent.click(within(dialog).getByRole('tab', { name: 'Клиент клуба' }));
  return dialog;
}

describe('MapSidePanel diagnostics (A3)', () => {
  it('surfaces the real device specifics on hand in the always-on status block', () => {
    const utils = renderPanel(seat({}));
    utils.getByText('Зал-1-ПК-07'); // device name, not a mashed string
    utils.getByText('Онлайн'); // connection
    utils.getByText('заблокирован'); // lock state
  });

  it('always shows software versions, online or offline', () => {
    // Версию ПО оператор должен видеть всегда — и для здоровой сессии, и для офлайн-ПК.
    const healthy = renderPanel(seat({}));
    expect(healthy.queryByText('Агент 0.4 · Оболочка 0.4')).not.toBeNull();
    cleanup();
    const offline = renderPanel(seat({ tone: 'offline', isDeviceOnline: false }));
    expect(offline.queryByText('Агент 0.4 · Оболочка 0.4')).not.toBeNull();
  });

  it('does not present a fabricated session billing mode as if it were real', () => {
    const { queryByText } = renderPanel(seat({}));
    // The old hardcoded "Биллинг: Депозит" row is gone; real billing/tariff arrives with B1.
    expect(queryByText('Биллинг')).toBeNull();
  });

  it('never leaks the raw session GUID onto the operator path', () => {
    const { container } = renderPanel(seat({}));
    expect(container.textContent).not.toContain('AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE');
  });

  // Связь с ПК — первым словом строки о ПК, выделенным, если её нет.
  it('names a lost connection first in the PC line', () => {
    const utils = renderPanel(seat({ isDeviceOnline: false }));
    const offline = utils.container.querySelector('.seat-pc-line .is-offline');
    expect(offline?.textContent).toBe('Нет связи');
  });

  // Статус ПК — одна строка внизу панели (дизайн-проход 29.09), а не блок «Статус ПК» с пилюлями
  // и не кнопка «Статус».
  it('shows the PC status as one line, without a «Статус» button or section', () => {
    const utils = renderPanel(seat({}));
    expect(utils.queryByRole('button', { name: /^Статус$/ })).toBeNull();
    expect(utils.queryByText('Статус ПК')).toBeNull();
    expect(utils.container.querySelector('.seat-pc-line')?.textContent).toContain('Онлайн');
  });

  it('shows the real client and tariff from the backend session, not a placeholder', () => {
    const utils = renderPanel(seat({
      playerDisplayName: 'Иван Петров',
      tariffName: 'VIP час',
      sessionStartedAtUtc: '2026-05-21T08:48:00Z'
    }));
    utils.getByText('Иван Петров');
    utils.getByText('VIP час');
    utils.getByText('Начата');
  });

  it('omits the session-identity block for a guest session with no account or tariff', () => {
    const utils = renderPanel(seat({ playerDisplayName: null, tariffName: null, sessionStartedAtUtc: null }));
    expect(utils.queryByText('Начата')).toBeNull();
  });

  it('shows the real running total for an open tab in the host currency', () => {
    const { getByText } = renderPanel(seat({ remaining: '≈ 54 c.', remainingSeconds: null, accruedCostMinorUnits: 5400 }));
    getByText('Набежало');
  });

  it('offers no PC line for a seat without a device', () => {
    const { container } = renderPanel(seat({ deviceId: null, deviceName: null }));
    expect(container.querySelector('.seat-pc-line')).toBeNull();
  });
});

describe('MapSidePanel new session client picker', () => {
  it('keeps the extracted guest fixed-duration start payload', async () => {
    const originalFetch = globalThis.fetch;
    const onSeatAction = mock(async (_request: SeatActionRequest) => ({}));
    globalThis.fetch = mock(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/tariffs/options')) return json(tariffOptions());
      throw new Error(`Unexpected request: ${url.pathname}`);
    }) as unknown as typeof fetch;
    try {
      const dialog = openClientStartDialog(onSeatAction);
      fireEvent.click(within(dialog).getByRole('tab', { name: 'Гость' }));
      fireEvent.click(within(dialog).getByRole('button', { name: /2 ч/ }));
      // Гость платит наличными вперёд: кнопка называет сумму, а запрос несёт тариф и эту сумму.
      fireEvent.click(await within(dialog).findByRole('button', { name: /Принять .* и начать/ }));
      await waitFor(() => expect(onSeatAction).toHaveBeenCalledTimes(1));
      expect(onSeatAction.mock.calls[0][0]).toMatchObject({
        type: 'start', durationMode: 'fixed', durationMinutes: 120, expectedChargeMinorUnits: 6000,
        billing: { mode: 'guest', playerAccountId: null, tariffVersionId: expect.any(String), playerPackageId: null }
      });
    } finally {
      globalThis.fetch = originalFetch;
    }
  });
  it('привязывает клиента кликом, показывает баланс и пакеты, а гость сбрасывает связь', async () => {
    const originalFetch = globalThis.fetch;
    const fetchMock = mock(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/tariffs/options')) {
        return json([{
          tariffId: 'tariff-1', tariffVersionId: 'tariff-version-1', tariffRuleVersionId: 'standard-v1',
          name: 'Standard', currencyCode: 'TJS', pricePerMinuteMinorUnits: 50,
          minimumBillableMinutes: 15, roundingIncrementMinutes: 5
        }]);
      }
      if (url.pathname.endsWith('/players/player-1/packages')) {
        return json([{
          playerPackageId: 'package-1', packageDefinitionId: 'definition-1', playerAccountId: 'player-1',
          name: 'Night 5h', purchasedPrice: { currencyCode: 'TJS', minorUnits: 10_000 },
          includedSeconds: 18_000, bonusSeconds: 0, remainingIncludedSeconds: 10_800,
          remainingBonusSeconds: 0, purchasedAtUtc: '2026-07-01T00:00:00Z', expiresAtUtc: null
        }]);
      }
      if (url.pathname.endsWith('/players')) {
        return json([{
          playerAccountId: 'player-1', displayName: 'Мадина С.', phoneNumber: '+992 90 777 88 99',
          walletBalanceMinorUnits: 45_000, debtBalanceMinorUnits: 0, activePackageCount: 1,
          isActive: true, createdAtUtc: '2026-07-01T00:00:00Z', lastActivityAtUtc: null,
          activePackageName: 'Night 5h', activePackageRemainingMinutes: 180, platformPersonId: null, createdFromApp: false
        }]);
      }
      throw new Error(`Unexpected request: ${url.pathname}`);
    });
    globalThis.fetch = fetchMock as unknown as typeof fetch;

    try {
      const readySeat = seat({ tone: 'ready', stateLabel: 'Свободен', activeSessionId: null, hasActiveSession: false });
      render(
        <I18nProvider>
          <MapSidePanel
            seat={readySeat}
            seats={[readySeat]}
            currencyCode="TJS"
            backend={backend()}
            actionsEnabled
            canUsePcControl={false}
            onSeatAction={async () => ({})}
            onPcControlAction={async () => ({ detail: '' })}
          />
        </I18nProvider>
      );

      fireEvent.click(screen.getByRole('button', { name: 'Посадить гостя' }));
      const dialog = screen.getByRole('dialog', { name: 'Новая сессия' });
      fireEvent.click(within(dialog).getByRole('tab', { name: 'Клиент клуба' }));
      fireEvent.change(within(dialog).getByRole('combobox', { name: 'Игрок для биллинга' }), { target: { value: 'Ма' } });
      fireEvent.click(await within(dialog).findByRole('option', { name: /\u041c\u0430\u0434\u0438\u043d\u0430 \u0421\./ }));

      expect(within(dialog).getByRole('combobox', { name: 'Игрок для биллинга' })).toHaveValue('Мадина С.');
      expect(within(dialog).getByText('Клиент клуба', { selector: '.booking-client-badge' })).toBeInTheDocument();
      expect(await within(dialog).findByText(/450.*хватит/)).toBeInTheDocument();

      fireEvent.click(within(dialog).getByRole('tab', { name: 'Пакет' }));
      await waitFor(() => expect(within(dialog).getByRole('combobox', { name: 'Пакет для сессии' })).toHaveTextContent('Night 5h'));

      fireEvent.click(within(dialog).getByRole('tab', { name: 'Гость' }));
      fireEvent.click(within(dialog).getByRole('tab', { name: 'Клиент клуба' }));
      expect(within(dialog).getByRole('combobox', { name: 'Игрок для биллинга' })).toHaveValue('');
      expect(within(dialog).queryByText('Клиент клуба', { selector: '.booking-client-badge' })).toBeNull();
    } finally {
      globalThis.fetch = originalFetch;
    }
  });

  it('не даёт позднему ответу старого поиска подменить баланс нового клиента', async () => {
    const originalFetch = globalThis.fetch;
    const oldSearch = deferred<Response>();
    const newSearch = deferred<Response>();
    const fetchMock = mock(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/tariffs/options')) return json(tariffOptions());
      if (url.pathname.endsWith('/players')) {
        return url.searchParams.get('query') === 'Al' ? oldSearch.promise : newSearch.promise;
      }
      if (url.pathname.includes('/packages')) return json([]);
      throw new Error(`Unexpected request: ${url.pathname}`);
    });
    globalThis.fetch = fetchMock as unknown as typeof fetch;

    try {
      const dialog = openClientStartDialog();
      const input = within(dialog).getByRole('combobox', { name: 'Игрок для биллинга' });
      fireEvent.change(input, { target: { value: 'Al' } });
      await waitFor(() => expect(fetchMock.mock.calls.some(([request]) => new URL(String(request)).searchParams.get('query') === 'Al')).toBe(true));
      fireEvent.change(input, { target: { value: 'Bo' } });
      await waitFor(() => expect(fetchMock.mock.calls.some(([request]) => new URL(String(request)).searchParams.get('query') === 'Bo')).toBe(true));

      await act(async () => { newSearch.resolve(json([player('player-b', 'Bob B.', 90_000)])); });
      const bobOption = await within(dialog).findByRole('option', { name: /Bob B\./ });
      await act(async () => { oldSearch.resolve(json([player('player-a', 'Alice A.', 10_000)])); });
      fireEvent.click(bobOption);

      expect(await within(dialog).findByText(/900.*хватит/)).toBeInTheDocument();
      expect(within(dialog).getByText(/Bob B\./, { selector: '.start-plan strong' })).toBeInTheDocument();
    } finally {
      globalThis.fetch = originalFetch;
    }
  });

  it('очищает пакет A до загрузки пакетов B и не разрешает submit со старым packageId', async () => {
    const originalFetch = globalThis.fetch;
    const playerBPackages = deferred<Response>();
    const onSeatAction = mock(async (_request: SeatActionRequest) => ({}));
    globalThis.fetch = mock(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/tariffs/options')) return json(tariffOptions());
      if (url.pathname.endsWith('/players')) {
        const query = url.searchParams.get('query');
        return json(query === 'Al' ? [player('player-a', 'Alice A.', 10_000)] : [player('player-b', 'Bob B.', 90_000)]);
      }
      if (url.pathname.endsWith('/players/player-a/packages')) return json([playerPackage('player-a', 'package-a', 'Alice Night')]);
      if (url.pathname.endsWith('/players/player-b/packages')) return playerBPackages.promise;
      throw new Error(`Unexpected request: ${url.pathname}`);
    }) as unknown as typeof fetch;

    try {
      const dialog = openClientStartDialog(onSeatAction);
      const input = within(dialog).getByRole('combobox', { name: 'Игрок для биллинга' });
      fireEvent.change(input, { target: { value: 'Al' } });
      fireEvent.click(await within(dialog).findByRole('option', { name: /Alice A\./ }));
      fireEvent.click(within(dialog).getByRole('tab', { name: 'Пакет' }));
      await waitFor(() => expect(within(dialog).getByRole('combobox', { name: 'Пакет для сессии' })).toHaveTextContent('Alice Night'));

      fireEvent.change(input, { target: { value: 'Bo' } });
      fireEvent.click(await within(dialog).findByRole('option', { name: /Bob B\./ }));

      const packageSelect = within(dialog).getByRole('combobox', { name: 'Пакет для сессии' });
      expect(packageSelect).toBeDisabled();
      expect(packageSelect).not.toHaveTextContent('Alice Night');
      expect(within(dialog).getByRole('button', { name: /Старт/ })).toBeDisabled();

      await act(async () => { playerBPackages.resolve(json([playerPackage('player-b', 'package-b', 'Bob Night')])); });
      await waitFor(() => expect(packageSelect).toHaveTextContent('Bob Night'));
      expect(packageSelect).toBeEnabled();
      fireEvent.click(within(dialog).getByRole('button', { name: /Старт/ }));
      await waitFor(() => expect(onSeatAction).toHaveBeenCalledTimes(1));
      expect(onSeatAction.mock.calls[0][0]).toMatchObject({
        type: 'start',
        billing: { mode: 'package', playerAccountId: 'player-b', playerPackageId: 'package-b' }
      });
    } finally {
      globalThis.fetch = originalFetch;
    }
  });

  // Поиск клиента отвалился — оператор должен понять, что делать дальше, а не прочитать
  // «503 Unavailable» посреди русского экрана. Набранное при этом не стирается.
  it('называет причину сбоя поиска и оставляет запрос для повтора', async () => {
    const originalFetch = globalThis.fetch;
    globalThis.fetch = mock(async (input: RequestInfo | URL) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/tariffs/options')) return json(tariffOptions());
      if (url.pathname.endsWith('/players')) return new Response('players unavailable', { status: 503, statusText: 'Unavailable' });
      throw new Error(`Unexpected request: ${url.pathname}`);
    }) as unknown as typeof fetch;

    try {
      const dialog = openClientStartDialog();
      const input = within(dialog).getByRole('combobox', { name: 'Игрок для биллинга' });
      fireEvent.change(input, { target: { value: 'Ma' } });

      expect(await within(dialog).findByRole('alert')).toHaveTextContent('Сервер вернул ошибку. Повторите позже.');
      expect(input).toHaveValue('Ma');
    } finally {
      globalThis.fetch = originalFetch;
    }
  });
});

// Панель места по таблице состояний (дизайн-проход 29.09): одна главная кнопка по положению
// места, до трёх второстепенных рядом, остальное — в «Ещё». Было 12 кнопок в сессии и 8 на
// свободном месте, все одного веса.
const ALL = [
  'organization.sessions.start', 'organization.sessions.extend', 'organization.sessions.transfer',
  'organization.sessions.end', 'organization.sessions.pause', 'organization.assistance.resolve',
  'organization.devices.commands.dispatch', 'organization.devices.maintenance'
];

function renderWith(s: SeatSummary, {
  permissions = ALL,
  actionsEnabled = true,
  seats = [s],
  onSeatAction = async () => ({}),
  onPcControlAction = async () => ({ detail: '' }),
  onResolveAssistance = async () => ({ detail: '' })
}: {
  permissions?: string[];
  actionsEnabled?: boolean;
  seats?: SeatSummary[];
  onSeatAction?: (request: SeatActionRequest) => Promise<Record<string, never>>;
  onPcControlAction?: (seat: SeatSummary, action: string, options?: { text?: string }) => Promise<{ detail: string }>;
  onResolveAssistance?: (seat: SeatSummary) => Promise<{ detail: string }>;
} = {}) {
  const context = backend();
  context.session.permissions = permissions;
  return render(
    <I18nProvider>
      <MapSidePanel
        seat={s}
        seats={seats}
        currencyCode="TJS"
        backend={context}
        actionsEnabled={actionsEnabled}
        canUsePcControl
        onSeatAction={onSeatAction}
        onPcControlAction={onPcControlAction as never}
        onResolveAssistance={onResolveAssistance}
      />
    </I18nProvider>
  );
}

const free = (overrides: Partial<SeatSummary> = {}) =>
  seat({ tone: 'ready', stateLabel: 'Свободно', activeSessionId: null, hasActiveSession: false, command: 'Idle', ...overrides });

// Главная кнопка — одна, в заливке и во всю ширину; её вид и подпись решает положение места.
function primaryButton() {
  const primaries = document.querySelectorAll('.ui-inspector .ui-btn--primary');
  expect(primaries.length).toBeLessThanOrEqual(1);
  return primaries[0] as HTMLButtonElement | undefined;
}

describe('MapSidePanel: одна главная кнопка по положению места', () => {
  it('свободное место — «Посадить гостя»', () => {
    renderWith(free());
    expect(primaryButton()?.textContent).toBe('Посадить гостя');
  });

  it('идёт сессия — «Завершить и рассчитать», рядом +15 · +30 · Перенести…', () => {
    const target = free({ id: 'seat-2', name: 'PC-08' });
    renderWith(seat({ remainingSeconds: 1800 }), { seats: [seat({ remainingSeconds: 1800 }), target] });
    expect(primaryButton()?.textContent).toBe('Завершить и рассчитать');
    const secondary = [...document.querySelectorAll('.ui-inspector-secondary .ui-btn')].map((button) => button.textContent);
    expect(secondary).toEqual(['15 мин', '30 мин', 'Перенести…']);
  });

  // Приёмка 30.09.2026: быстрое «+15 мин» у платной сессии клиента отвечало 400 «нужен тариф»,
  // потому что уходило с заглушками из формы старта. Продление не называет условий оплаты —
  // тариф, способ и клиента берёт сервер из сессии.
  it('«+15 мин» не несёт условий оплаты: тариф и клиента берёт сервер из сессии', async () => {
    const onSeatAction = mock(async (_request: SeatActionRequest) => ({}));
    renderWith(seat({ remainingSeconds: 1800 }), { onSeatAction });
    fireEvent.click(screen.getByRole('button', { name: '15 мин' }));
    await waitFor(() => expect(onSeatAction).toHaveBeenCalledTimes(1));
    expect(onSeatAction.mock.calls[0][0]).toMatchObject({
      type: 'extend',
      minutes: 15,
      billing: { mode: 'guest', playerAccountId: null, tariffVersionId: null, playerPackageId: null }
    });
  });

  it('открытый счёт — «Завершить и принять» с суммой, которая уже набежала', () => {
    renderWith(seat({ remaining: '≈ 54 с.', remainingSeconds: null, accruedCostMinorUnits: 5400 }));
    expect(primaryButton()?.textContent).toBe('Завершить и принять 54 с.');
  });

  it('пауза — «Снять с паузы» и продолжает сессию', async () => {
    const onSeatAction = mock(async (_request: SeatActionRequest) => ({}));
    renderWith(seat({ sessionState: 'Paused', remainingSeconds: 1800 }), { onSeatAction });
    expect(screen.getByText('Пауза')).toBeInTheDocument();
    fireEvent.click(primaryButton()!);
    await waitFor(() => expect(onSeatAction).toHaveBeenCalledTimes(1));
    expect(onSeatAction.mock.calls[0][0]).toMatchObject({ type: 'resume' });
  });

  // Пока ПК не ответил на прошлую команду, вторая либо продублирует её, либо ударит по
  // неподтверждённому состоянию — поэтому кнопки нет, есть строка, чего ждём.
  it('ожидает ответа ПК — главной кнопки нет, есть строка, что ушло', () => {
    renderWith(free({ tone: 'pending', command: 'Unlock pending' }));
    expect(primaryButton()).toBeUndefined();
    expect(screen.getByText('Разблокировка в процессе')).toBeInTheDocument();
  });

  it('выключенный ПК — «Разбудить», и команда уходит сразу', async () => {
    const onPcControlAction = mock(async () => ({ detail: '' }));
    renderWith(free({ tone: 'offline', isDeviceOnline: false }), { onPcControlAction });
    expect(screen.queryByRole('button', { name: 'Посадить гостя' })).toBeNull();
    fireEvent.click(primaryButton()!);
    await waitFor(() => expect(onPcControlAction).toHaveBeenCalledTimes(1));
    expect((onPcControlAction.mock.calls[0] as unknown[])[1]).toBe('wake');
  });

  it('с сессией без связи — всё равно «Завершить и рассчитать»: деньги считает сервер', () => {
    renderWith(seat({ tone: 'offline', isDeviceOnline: false, remainingSeconds: 1200 }));
    expect(primaryButton()?.textContent).toBe('Завершить и рассчитать');
  });

  it('обслуживание — «Вернуть в зал», без вопросов: это безопасно', async () => {
    const onPcControlAction = mock(async () => ({ detail: '' }));
    renderWith(free({ tone: 'service', maintenanceSinceUtc: '2026-05-21T08:00:00Z' }), { onPcControlAction });
    fireEvent.click(primaryButton()!);
    await waitFor(() => expect(onPcControlAction).toHaveBeenCalledTimes(1));
    expect((onPcControlAction.mock.calls[0] as unknown[])[1]).toBe('maintenance-off');
  });

  it('ПК сверх тарифа — кнопки нет, одна строка почему', () => {
    renderWith(free({ tone: 'service', isOutsidePlan: true, stateLabel: 'Вне тарифа' }));
    expect(primaryButton()).toBeUndefined();
    expect(screen.getByText(/ПК сверх тарифа/)).toBeInTheDocument();
  });

  // Киоск снят — ПК вышел из зала: кнопки посадки нет, одна строка почему и что делать.
  it('ПК без киоска — кнопки нет, строка «Не игровое место: киоск снят» и как вернуть', () => {
    renderWith(free({ tone: 'service', isKioskAbsent: true, stateLabel: 'Не игровое место' }));
    expect(primaryButton()).toBeUndefined();
    expect(screen.getByText(/Не игровое место: киоск снят/)).toBeInTheDocument();
    expect(screen.getByText(/мастер установки заново/)).toBeInTheDocument();
    expect(screen.queryByText(/ПК сверх тарифа/)).toBeNull();
  });

  // Серый «сервис» без решения клуба (ПК не одобрен) — это не «сверх тарифа» и не «вернуть в зал».
  it('неодобренный ПК — ни «Вернуть в зал», ни строки про тариф', () => {
    renderWith(free({ tone: 'service', stateLabel: 'Обслуживание' }));
    expect(primaryButton()).toBeUndefined();
    expect(screen.queryByText(/ПК сверх тарифа/)).toBeNull();
  });

  // Нет права — кнопки нет вовсе: серая кнопка с «попросите доступ» у каждой была шумом.
  it('без права на продление и завершение этих кнопок нет, перенос остаётся', () => {
    renderWith(seat({}), { permissions: ['organization.sessions.start', 'organization.sessions.transfer'] });
    expect(primaryButton()).toBeUndefined();
    expect(screen.queryByRole('button', { name: /15 мин/ })).toBeNull();
    expect(screen.getByRole('button', { name: 'Перенести…' })).toBeInTheDocument();
  });

  // Сервер недоступен — объясняет одна строка у главной кнопки, и кнопка ссылается на неё.
  it('без связи с сервером главная кнопка закрыта с причиной', () => {
    renderWith(free(), { actionsEnabled: false });
    const primary = primaryButton()!;
    expect(primary).toBeDisabled();
    expect(screen.getByText('Нет связи с сервером')).toBeInTheDocument();
  });

  it('игрок зовёт — полоса «Зовёт» и «Подошёл» снимает вызов', async () => {
    const onResolveAssistance = mock(async () => ({ detail: '' }));
    renderWith(seat({ assistanceRequestedAtUtc: new Date(Date.now() - 4 * 60_000).toISOString() }), { onResolveAssistance });
    expect(screen.getByText(/Зовёт 4 мин/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Подошёл' }));
    await waitFor(() => expect(onResolveAssistance).toHaveBeenCalledTimes(1));
  });
});

// Сбой команды — своё положение места: главное — повторить именно ту команду, что упала, тем же
// путём, что из «Ещё» (с ключом повтора и, для опасной, с «точно?»); гостя посадить тоже можно.
describe('MapSidePanel: «Повторить» упавшую команду', () => {
  const failed = (type: string) => free({ tone: 'failed', stateLabel: 'Сбой команды', lastFailedCommandType: type });

  it('упавшая разблокировка — «Повторить разблокировку» уходит сразу, рядом «Посадить гостя»', async () => {
    const onPcControlAction = mock(async () => ({ detail: '' }));
    renderWith(failed('unlock'), { onPcControlAction });
    expect(primaryButton()?.textContent).toBe('Повторить разблокировку');
    expect(screen.getByRole('button', { name: 'Посадить гостя' })).not.toHaveClass('ui-btn--primary');
    fireEvent.click(primaryButton()!);
    await waitFor(() => expect(onPcControlAction).toHaveBeenCalledTimes(1));
    expect((onPcControlAction.mock.calls[0] as unknown[])[1]).toBe('unlock');
  });

  it('упавшая перезагрузка повторяется только после «точно?»', async () => {
    const onPcControlAction = mock(async () => ({ detail: '' }));
    renderWith(failed('reboot'), { onPcControlAction });
    fireEvent.click(screen.getByRole('button', { name: 'Повторить перезагрузку' }));
    expect(onPcControlAction).not.toHaveBeenCalled();
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Перезагрузить' }));
    await waitFor(() => expect(onPcControlAction).toHaveBeenCalledTimes(1));
    expect((onPcControlAction.mock.calls[0] as unknown[])[1]).toBe('reboot');
  });

  it('без права на команды ПК повтора нет — остаётся «Посадить гостя»', () => {
    renderWith(failed('unlock'), { permissions: ['organization.sessions.start'] });
    expect(primaryButton()?.textContent).toBe('Посадить гостя');
  });

  // С игроком за ПК главная — сессия, а упавшая команда — строкой с «Повторить».
  it('в сессии упавшая команда — строкой с «Повторить», главная остаётся сессии', async () => {
    const onPcControlAction = mock(async () => ({ detail: '' }));
    renderWith(seat({ remainingSeconds: 1800, lastFailedCommandType: 'unlock' }), { onPcControlAction });
    expect(primaryButton()?.textContent).toBe('Завершить и рассчитать');
    expect(screen.getByText('Не прошла команда: Разблокировка')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    await waitFor(() => expect(onPcControlAction).toHaveBeenCalledTimes(1));
    expect((onPcControlAction.mock.calls[0] as unknown[])[1]).toBe('unlock');
  });
});

describe('MapSidePanel: «Ещё» — всё остальное, с причинами', () => {
  const openMore = () => fireEvent.click(screen.getByRole('button', { name: 'Ещё действия' }));

  it('перезагрузка свободного ПК спрашивает «точно?» и уходит только после подтверждения', async () => {
    const onPcControlAction = mock(async () => ({ detail: '' }));
    renderWith(free(), { onPcControlAction });
    openMore();
    fireEvent.click(screen.getByRole('menuitem', { name: 'Перезагрузить' }));
    expect(onPcControlAction).not.toHaveBeenCalled();
    const dialog = screen.getByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Перезагрузить' }));
    await waitFor(() => expect(onPcControlAction).toHaveBeenCalledTimes(1));
    expect((onPcControlAction.mock.calls[0] as unknown[])[1]).toBe('reboot');
  });

  it('сообщение нельзя отправить пустым, а с текстом оно уходит с текстом', async () => {
    const onPcControlAction = mock(async () => ({ detail: '' }));
    renderWith(seat({}), { onPcControlAction });
    openMore();
    fireEvent.click(screen.getByRole('menuitem', { name: 'Сообщение игроку' }));
    const dialog = screen.getByRole('dialog');
    const send = within(dialog).getAllByRole('button').at(-1)!;
    expect(send).toBeDisabled();
    fireEvent.change(within(dialog).getByRole('textbox'), { target: { value: 'Через 5 минут закрываемся' } });
    fireEvent.click(send);
    await waitFor(() => expect(onPcControlAction).toHaveBeenCalledTimes(1));
    expect(onPcControlAction.mock.calls[0] as unknown[]).toMatchObject([expect.anything(), 'message', { text: 'Через 5 минут закрываемся' }]);
  });

  it('за ПК играют — перезагрузка закрыта и говорит почему', () => {
    renderWith(seat({}));
    openMore();
    const reboot = screen.getByRole('menuitem', { name: 'Перезагрузить' });
    expect(reboot).toHaveAttribute('aria-disabled', 'true');
    expect(reboot).toHaveAccessibleDescription('идёт сессия');
  });

  // Блокировка по факту: запертому ПК — только «Разблокировать».
  it('запертому ПК предлагает только разблокировать — и без сессии', () => {
    renderWith(free({ isDeviceLocked: true }));
    openMore();
    expect(screen.getByRole('menuitem', { name: 'Разблокировать' })).not.toHaveAttribute('aria-disabled');
    expect(screen.queryByRole('menuitem', { name: 'Блокировать' })).toBeNull();
  });

  // Что уже стоит кнопкой на панели, в «Ещё» не дублируется.
  it('не повторяет в «Ещё» то, что уже стоит кнопкой', () => {
    renderWith(seat({ sessionState: 'Paused', remainingSeconds: 1800 }));
    openMore();
    expect(screen.queryByRole('menuitem', { name: 'Продолжить' })).toBeNull();
    expect(screen.queryByRole('menuitem', { name: /15 мин/ })).toBeNull();
  });

  it('отказ сервера виден словами', async () => {
    renderWith(free(), { onPcControlAction: async () => { throw new Error('ПК занят другой командой'); } });
    openMore();
    fireEvent.click(screen.getByRole('menuitem', { name: 'Разблокировать' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('ПК занят другой командой');
  });
});

