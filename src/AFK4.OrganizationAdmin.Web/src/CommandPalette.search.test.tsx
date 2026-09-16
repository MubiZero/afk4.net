import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';

// bun's mock.module is not hoisted above static imports — register before importing the palette.
const found = [
  { kind: 'seat', id: 'seat-1', title: 'PC-12', subtitle: 'Главный зал', occursAtUtc: null, amountMinorUnits: null, currencyCode: null },
  { kind: 'player', id: 'p1', title: 'Фаррух Азизов', subtitle: '+992937380070', occursAtUtc: null, amountMinorUnits: null, currencyCode: null },
  {
    kind: 'reservation',
    id: 'r1',
    title: 'Далер Назаров',
    subtitle: '+992937380071',
    occursAtUtc: '2026-09-17T15:00:00Z',
    amountMinorUnits: null,
    currencyCode: null
  },
  {
    kind: 'receipt',
    id: 'rc1',
    title: 'POS-20260916-0007',
    subtitle: null,
    occursAtUtc: '2026-09-16T10:00:00Z',
    amountMinorUnits: 4500,
    currencyCode: 'TJS'
  }
];
const searchBranch = mock(async () => found);

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({ search: { searchBranch } })
}));

const { CommandPalette } = await import('./CommandPalette');

afterAll(() => {
  mock.module('./operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('./operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

const managerPerms = [
  'organization.floor_map.view',
  'organization.reservations.view',
  'organization.players.view',
  'organization.receipts.view',
  'organization.identity.branch_staff.manage'
];
// Кассир видит кассу, но не раздел клиентов — значит, и людей в палитре искать не может.
const cashierPerms = ['organization.pos.sales.create'];

const makeSession = (permissions: string[]) => ({ permissions } as unknown as OperatorAuthSession);
const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [] },
  branchId: 'b1'
};

type Handlers = {
  onOpenPerson?: (person: { playerAccountId: string; search: string }) => void;
  onOpenSeat?: (seatId: string) => void;
  onOpenReservation?: (target: { reservationId: string; startsAtUtc: string | null }) => void;
  onOpenReceipt?: (target: { receiptId: string }) => void;
};

function renderPalette(perms: string[], handlers: Handlers = {}) {
  const onClose = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <CommandPalette
        session={makeSession(perms)}
        backend={backend as never}
        onNavigate={mock(() => {}) as never}
        onOpenPerson={handlers.onOpenPerson ?? mock(() => {})}
        onOpenSeat={handlers.onOpenSeat ?? mock(() => {})}
        onOpenReservation={handlers.onOpenReservation ?? mock(() => {})}
        onOpenReceipt={handlers.onOpenReceipt ?? mock(() => {})}
        onClose={onClose}
      />
    </I18nProvider>
  );
  return { onClose };
}

function type(value: string) {
  fireEvent.change(screen.getByLabelText('Командная палитра'), { target: { value } });
}

describe('CommandPalette · поиск по клубу', () => {
  afterEach(() => {
    cleanup();
    searchBranch.mockClear();
  });

  it('находит место, человека, бронь и чек — каждого в своём разделе', async () => {
    renderPalette(managerPerms);
    type('Фаррух');

    expect(await screen.findByText('Фаррух Азизов')).toBeDefined();
    expect(screen.getByText('PC-12')).toBeDefined();
    expect(screen.getByText('POS-20260916-0007')).toBeDefined();
    expect(screen.getByText('Места')).toBeDefined();
    expect(screen.getByText('Клиенты')).toBeDefined();
    expect(screen.getByText('Брони')).toBeDefined();
    expect(screen.getByText('Чеки')).toBeDefined();
  });

  // Номер сам по себе ничего не говорит: из двух похожих чеков свой узнают по сумме и дате.
  it('чек подписан суммой и датой, бронь — телефоном и временем', async () => {
    renderPalette(managerPerms);
    type('Фаррух');

    await screen.findByText('POS-20260916-0007');
    const receiptHint = screen.getByText((text) => text.includes('45') && text.includes('16.09.2026'));
    const reservationHint = screen.getByText((text) => text.includes('+992937380071') && text.includes('17.09.2026'));

    expect(receiptHint).toBeDefined();
    expect(reservationHint).toBeDefined();
  });

  // Палитра — это «отвези меня туда»: выбор открывает саму сущность, а не просто раздел.
  it('выбор человека отдаёт наверх его id и строку поиска', async () => {
    const onOpenPerson = mock((_: { playerAccountId: string; search: string }) => {});
    const { onClose } = renderPalette(managerPerms, { onOpenPerson });
    type('Фаррух');

    fireEvent.click(await screen.findByText('Фаррух Азизов'));

    expect(onOpenPerson).toHaveBeenCalledTimes(1);
    expect(onOpenPerson.mock.calls[0]![0]).toEqual({ playerAccountId: 'p1', search: 'Фаррух' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('выбор места ведёт на карту к этому месту', async () => {
    const onOpenSeat = mock((_: string) => {});
    renderPalette(managerPerms, { onOpenSeat });
    type('PC-12');

    fireEvent.click(await screen.findByText('PC-12'));

    expect(onOpenSeat.mock.calls[0]![0]).toBe('seat-1');
  });

  // Экран броней показывает один день: без времени начала нужная бронь просто не загрузится.
  it('выбор брони отдаёт наверх её время, а не только id', async () => {
    const onOpenReservation = mock((_: { reservationId: string; startsAtUtc: string | null }) => {});
    renderPalette(managerPerms, { onOpenReservation });
    type('Далер');

    fireEvent.click(await screen.findByText('Далер Назаров'));

    expect(onOpenReservation.mock.calls[0]![0]).toEqual({ reservationId: 'r1', startsAtUtc: '2026-09-17T15:00:00Z' });
  });

  it('выбор чека ведёт в кассу к этому чеку', async () => {
    const onOpenReceipt = mock((_: { receiptId: string }) => {});
    renderPalette(managerPerms, { onOpenReceipt });
    type('POS-2026');

    fireEvent.click(await screen.findByText('POS-20260916-0007'));

    expect(onOpenReceipt.mock.calls[0]![0]).toEqual({ receiptId: 'rc1' });
  });

  // Стрелки ходят сквозь все разделы: для того, кто набирает, это один список.
  it('стрелка вниз доходит от экранов до находок', async () => {
    const onOpenSeat = mock((_: string) => {});
    renderPalette(managerPerms, { onOpenSeat });
    type('Кл'); // «Клиенты» в списке экранов + запрос длиннее минимального

    await screen.findByText('PC-12');
    const input = screen.getByLabelText('Командная палитра');
    const options = screen.getAllByRole('option');
    const seatIndex = options.findIndex((option) => option.textContent?.startsWith('PC-12'));
    for (let step = 0; step < seatIndex; step += 1) {
      fireEvent.keyDown(input, { key: 'ArrowDown' });
    }
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onOpenSeat).toHaveBeenCalledTimes(1);
  });

  // Одна буква совпала бы с половиной базы, и каждая следующая гоняла бы сеть впустую.
  it('по одной букве сеть не дёргается', async () => {
    renderPalette(managerPerms);
    type('Ф');

    await waitFor(() => expect(screen.queryByText('Клиенты')).toBeNull());
    expect(searchBranch).not.toHaveBeenCalled();
  });

  // Палитра не должна становиться обходом прав: кто не может открыть раздел — тот в нём и не ищет.
  it('без прав на разделы ничего не ищет', async () => {
    renderPalette(cashierPerms);
    type('Фаррух');

    await waitFor(() => expect(screen.queryByText('Фаррух Азизов')).toBeNull());
    expect(searchBranch).not.toHaveBeenCalled();
  });

  // Режим поддержки платформы сужает список экранов — вместе с ним сужается и палитра.
  it('в режиме поддержки показывает только разрешённые виды', async () => {
    render(
      <I18nProvider initialLocale="ru">
        <CommandPalette
          session={makeSession(managerPerms)}
          backend={backend as never}
          visibleWorkspaceIds={new Set(['map'] as const) as never}
          onNavigate={mock(() => {}) as never}
          onOpenPerson={mock(() => {})}
          onOpenSeat={mock(() => {})}
          onOpenReservation={mock(() => {})}
          onOpenReceipt={mock(() => {})}
          onClose={mock(() => {})}
        />
      </I18nProvider>
    );
    type('Фаррух');

    expect(await screen.findByText('PC-12')).toBeDefined();
    expect(screen.queryByText('Фаррух Азизов')).toBeNull();
    expect(screen.queryByText('POS-20260916-0007')).toBeNull();
  });

  // Сеть отвалилась — палитра говорит об этом, а не притворяется, что ничего не нашлось.
  it('сбой поиска не выдаёт себя за «ничего не нашлось»', async () => {
    searchBranch.mockImplementationOnce(async () => { throw new Error('network down'); });
    renderPalette(managerPerms);
    type('Фаррух');

    expect(await screen.findByText('Не удалось выполнить поиск')).toBeDefined();
  });
});
