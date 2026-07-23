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
      permissions: ['sessions.start', 'players.view', 'billing.view', 'tariffs.view']
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
    activePackageRemainingMinutes: 0
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

  it('flags a lost-connection PC with the danger status pill', () => {
    const utils = renderPanel(seat({ isDeviceOnline: false }));
    const pill = utils.container.querySelector('.status-pill.bad');
    expect(pill).not.toBeNull();
    expect(pill?.textContent).toContain('Нет связи');
  });

  it('shows the unified status block under the controls, without a «Статус» button', () => {
    const utils = renderPanel(seat({}));
    // Статус — единый блок внизу, не действие: отдельной кнопки «Статус» больше нет.
    expect(utils.queryByRole('button', { name: /^Статус$/ })).toBeNull();
    utils.getByText('Статус ПК');
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

  it('offers no PC control or status block for a seat without a device', () => {
    const { queryByText } = renderPanel(seat({ deviceId: null, deviceName: null }));
    expect(queryByText('Управление ПК')).toBeNull();
    expect(queryByText('Статус ПК')).toBeNull();
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
      fireEvent.click(within(dialog).getByRole('button', { name: /Старт/ }));
      await waitFor(() => expect(onSeatAction).toHaveBeenCalledTimes(1));
      expect(onSeatAction.mock.calls[0][0]).toMatchObject({
        type: 'start', durationMode: 'fixed', durationMinutes: 120,
        billing: { mode: 'guest', playerAccountId: null, tariffVersionId: null, playerPackageId: null }
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
          activePackageName: 'Night 5h', activePackageRemainingMinutes: 180
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

  it('показывает backend-ошибку поиска и оставляет запрос для повтора', async () => {
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

      expect(await within(dialog).findByRole('alert')).toHaveTextContent('503 Unavailable');
      expect(input).toHaveValue('Ma');
    } finally {
      globalThis.fetch = originalFetch;
    }
  });
});
