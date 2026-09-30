import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterAll, afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';
import { playersSnapshotCache } from './players/playersSnapshot';

// Приёмка 30.09.2026: «Клиенты» открывались пустыми (сервер на пустом запросе отдавал []),
// итоги считались по первым пятидесяти, а после «Пополнить баланс» строка списка, шапка и окно
// «Посадить за ПК» держали старый баланс, и сумма оставалась в поле.
const person = (index: number) => ({
  playerAccountId: `p${index}`,
  displayName: `Клиент ${String(index).padStart(2, '0')}`,
  phoneNumber: `+99290000${String(index).padStart(4, '0')}`,
  walletBalanceMinorUnits: index === 1 ? 20800 : 1000,
  debtBalanceMinorUnits: 0,
  activePackageCount: 0,
  isActive: true,
  createdAtUtc: '2026-01-01T00:00:00Z',
  lastActivityAtUtc: null,
  activePackageName: null,
  activePackageRemainingMinutes: 0,
  platformPersonId: null,
  createdFromApp: false
});
const club = Array.from({ length: 60 }, (_, index) => person(index + 1));

let walletTotal = 34000;
// Кошелёк первого клиента, как его помнит сервер: пополнение меняет его, и перечитывание сводки после
// него обязано вернуть уже новую сумму, а не прежнюю.
let firstWallet = 20800;
let shift: { shiftId: string } | null = { shiftId: 'shift-1' };
const searchPlayers = mock(async (_branchId: string, _query: string, limit: number, _includeInactive?: boolean, page?: { segment?: string; offset?: number }) =>
  club.slice(page?.offset ?? 0, (page?.offset ?? 0) + limit));
const getPlayersSummary = mock(async (_branchId: string, _query?: string) => ({
  totalCount: 60, debtorCount: 4, inactiveCount: 2, walletTotalMinorUnits: walletTotal, debtTotalMinorUnits: 1200
}));
const topUpWallet = mock(async (_playerAccountId: string, _request: Record<string, unknown>) => {
  walletTotal += 5000;
  firstWallet += 5000;
  return {
    playerAccountId: 'p1',
    walletBalance: { currencyCode: 'TJS', minorUnits: firstWallet },
    heldBalance: { currencyCode: 'TJS', minorUnits: 0 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    recentEntries: []
  };
});
const getWalletSummary = mock(async (playerAccountId: string) => ({
  playerAccountId,
  walletBalance: { currencyCode: 'TJS', minorUnits: firstWallet },
  heldBalance: { currencyCode: 'TJS', minorUnits: 0 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
  recentEntries: []
}));

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    players: {
      searchPlayers,
      getPlayersSummary,
      topUpWallet,
      getWalletSummary,
      getPlayerPackages: mock(async () => []),
      getLedger: mock(async () => ({ items: [], nextCursor: null }))
    },
    shifts: { getCurrentShift: mock(async () => shift) }
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
    permissions: ['organization.players.view', 'organization.billing.wallet.top_up', 'organization.shifts.view']
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

const firstRow = () => document.querySelector('.ctable-row') as HTMLElement;

describe('BackendPlayersWorkspace · справочник клиентов', () => {
  beforeEach(() => {
    walletTotal = 34000;
    firstWallet = 20800;
    shift = { shiftId: 'shift-1' };
  });
  afterEach(() => {
    cleanup();
    searchPlayers.mockClear();
    getPlayersSummary.mockClear();
    topUpWallet.mockClear();
    playersSnapshotCache.clear();
  });

  it('открывается списком клиентов без всякого поиска, страницей в 50 с запасной записью', async () => {
    renderWorkspace();

    await screen.findByText('Клиент 01', { selector: '.nm' });
    expect(searchPlayers.mock.calls[0]![1]).toBe('');
    expect(searchPlayers.mock.calls[0]![2]).toBe(51);
    // Запасная 51-я запись — признак «дальше есть», на экран не идёт.
    expect(document.querySelectorAll('.ctable-row')).toHaveLength(50);
    expect(screen.queryByText('Клиент 51', { selector: '.nm' })).toBeNull();
  });

  it('итоги и счётчики отборов берёт у сервера: у клуба их больше, чем на странице', async () => {
    renderWorkspace();

    await screen.findByText('Клиент 01', { selector: '.nm' });
    const head = document.querySelector('.ui-section-header') as HTMLElement;
    await waitFor(() => expect(head).toHaveTextContent('340 с.'));
    expect(head).toHaveTextContent('12 с.');
    const segments = screen.getByRole('group', { name: 'Сегменты' });
    // 60 клиентов в клубе, 50 на странице: «Все» считает клуб, а не страницу.
    expect(within(segments).getByRole('button', { name: /Все/ })).toHaveTextContent('60');
    expect(within(segments).getByRole('button', { name: /Есть долг/ })).toHaveTextContent('4');
  });

  it('«Показать ещё» берёт следующую страницу с того места, где остановились', async () => {
    renderWorkspace();
    await screen.findByText('Клиент 01', { selector: '.nm' });

    fireEvent.click(screen.getByRole('button', { name: 'Показать ещё' }));

    await screen.findByText('Клиент 60', { selector: '.nm' });
    expect(searchPlayers.mock.calls[1]![4]).toEqual({ segment: undefined, offset: 50 });
    expect(document.querySelectorAll('.ctable-row')).toHaveLength(60);
    expect(screen.queryByRole('button', { name: 'Показать ещё' })).toBeNull();
  });

  it('отбор «Долги» уходит на сервер, а не режет загруженную страницу', async () => {
    renderWorkspace();
    await screen.findByText('Клиент 01', { selector: '.nm' });

    fireEvent.click(within(screen.getByRole('group', { name: 'Сегменты' })).getByRole('button', { name: /Есть долг/ }));

    await waitFor(() => expect(searchPlayers.mock.calls.some((call) => call[4]?.segment === 'debt')).toBe(true));
  });
});

describe('BackendPlayersWorkspace · пополнение баланса', () => {
  beforeEach(() => {
    walletTotal = 34000;
    firstWallet = 20800;
    shift = { shiftId: 'shift-1' };
  });
  afterEach(() => {
    cleanup();
    searchPlayers.mockClear();
    getPlayersSummary.mockClear();
    topUpWallet.mockClear();
    playersSnapshotCache.clear();
  });

  async function topUpFifty() {
    renderWorkspace();
    await screen.findByText('Клиент 01', { selector: '.ui-inspector-title' });
    const field = screen.getByLabelText('Сумма пополнения') as HTMLInputElement;
    fireEvent.change(field, { target: { value: '50' } });
    await waitFor(() => expect(screen.getByRole('button', { name: 'Пополнить баланс' })).toBeEnabled());
    fireEvent.click(screen.getByRole('button', { name: 'Пополнить баланс' }));
    return field;
  }

  it('после успеха одно свежее значение везде, а поле пустеет', async () => {
    expect(firstRow()).toBeNull();
    const field = await topUpFifty();

    await waitFor(() => expect(topUpWallet).toHaveBeenCalledTimes(1));
    // Строка списка — то, откуда окно «Посадить за ПК» берёт баланс.
    await waitFor(() => expect(firstRow()).toHaveTextContent('258 с.'));
    expect(firstRow()).not.toHaveTextContent('208 с.');
    // Итог в шапке пересчитан сервером, а не остался прежним.
    const head = document.querySelector('.ui-section-header') as HTMLElement;
    await waitFor(() => expect(head).toHaveTextContent('390 с.'));
    // Сумма не остаётся в поле: повторное нажатие пополнило бы ещё раз.
    await waitFor(() => expect(field.value).toBe(''));
  });

  it('при закрытой смене говорит об этом рядом с кнопкой заранее, а не после нажатия', async () => {
    shift = null;
    renderWorkspace();
    await screen.findByText('Клиент 01', { selector: '.ui-inspector-title' });

    const button = await screen.findByRole('button', { name: 'Пополнить баланс' });
    await waitFor(() => expect(button).toBeDisabled());
    expect(screen.getByText('Пополнение принимают в открытой смене. Откройте смену.')).toBeInTheDocument();
    expect(button).toHaveAccessibleDescription('Пополнение принимают в открытой смене. Откройте смену.');
  });

  it('при открытой смене кнопка активна и без лишних слов', async () => {
    renderWorkspace();
    await screen.findByText('Клиент 01', { selector: '.ui-inspector-title' });

    const button = await screen.findByRole('button', { name: 'Пополнить баланс' });
    expect(button).toBeEnabled();
    expect(screen.queryByText(/Откройте смену/)).toBeNull();
  });
});
