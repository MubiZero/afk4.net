import { cleanup, render, screen, fireEvent, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';

// bun's mock.module is not hoisted above static imports — register before importing the palette.
const searchPlayers = mock(async () => ([
  {
    playerAccountId: 'p1',
    displayName: 'Фаррух Азизов',
    phoneNumber: '+992937380070',
    walletBalanceMinorUnits: 0,
    debtBalanceMinorUnits: 0,
    activePackageCount: 0,
    isActive: true,
    createdAtUtc: '2026-08-01T00:00:00Z',
    lastActivityAtUtc: null,
    activePackageName: null,
    activePackageRemainingMinutes: 0, platformPersonId: null, createdFromApp: false
  }
]));

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({ players: { searchPlayers } })
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

function renderPalette(perms: string[], onOpenPerson = mock((_: { playerAccountId: string; search: string }) => {})) {
  const onClose = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <CommandPalette
        session={makeSession(perms)}
        backend={backend as never}
        onNavigate={mock(() => {}) as never}
        onOpenPerson={onOpenPerson}
        onClose={onClose}
      />
    </I18nProvider>
  );
  return { onOpenPerson, onClose };
}

function type(value: string) {
  fireEvent.change(screen.getByLabelText('Командная палитра'), { target: { value } });
}

describe('CommandPalette · люди', () => {
  afterEach(() => {
    cleanup();
    searchPlayers.mockClear();
  });

  it('находит человека по набранному и показывает его номер', async () => {
    renderPalette(managerPerms);
    type('Фаррух');

    expect(await screen.findByText('Фаррух Азизов')).toBeDefined();
    expect(screen.getByText('+992937380070')).toBeDefined();
    expect(screen.getByText('Клиенты')).toBeDefined();
  });

  // Палитра — это «отвези меня к нему»: выбор открывает карточку, а не просто раздел.
  it('выбор человека отдаёт наверх его id и строку поиска', async () => {
    const onOpenPerson = mock((_: { playerAccountId: string; search: string }) => {});
    const { onClose } = renderPalette(managerPerms, onOpenPerson);
    type('Фаррух');

    fireEvent.click(await screen.findByText('Фаррух Азизов'));

    expect(onOpenPerson).toHaveBeenCalledTimes(1);
    expect(onOpenPerson.mock.calls[0]![0]).toEqual({ playerAccountId: 'p1', search: 'Фаррух' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  // Стрелки ходят сквозь оба раздела: для того, кто набирает, это один список.
  it('стрелка вниз доходит от экранов до людей', async () => {
    const onOpenPerson = mock((_: { playerAccountId: string; search: string }) => {});
    renderPalette(managerPerms, onOpenPerson);
    type('Кл'); // «Клиенты» в списке экранов + запрос длиннее минимального

    await screen.findByText('Фаррух Азизов');
    const input = screen.getByLabelText('Командная палитра');
    const options = screen.getAllByRole('option');
    for (let step = 1; step < options.length; step += 1) {
      fireEvent.keyDown(input, { key: 'ArrowDown' });
    }
    fireEvent.keyDown(input, { key: 'Enter' });

    expect(onOpenPerson).toHaveBeenCalledTimes(1);
  });

  // Одна буква совпала бы с половиной базы, и каждая следующая гоняла бы сеть впустую.
  it('по одной букве сеть не дёргается', async () => {
    renderPalette(managerPerms);
    type('Ф');

    await waitFor(() => expect(screen.queryByText('Клиенты')).toBeNull());
    expect(searchPlayers).not.toHaveBeenCalled();
  });

  // Палитра не должна становиться обходом прав: кто не может открыть карточку клиента —
  // тот и не ищет людей.
  it('без права на раздел клиентов людей не ищет', async () => {
    renderPalette(cashierPerms);
    type('Фаррух');

    await waitFor(() => expect(screen.queryByText('Фаррух Азизов')).toBeNull());
    expect(searchPlayers).not.toHaveBeenCalled();
  });

  // Режим поддержки платформы сужает список экранов — вместе с ним сужается и палитра.
  it('в режиме поддержки без раздела клиентов людей не ищет', async () => {
    render(
      <I18nProvider initialLocale="ru">
        <CommandPalette
          session={makeSession(managerPerms)}
          backend={backend as never}
          visibleWorkspaceIds={new Set(['map'] as const) as never}
          onNavigate={mock(() => {}) as never}
          onOpenPerson={mock(() => {})}
          onClose={mock(() => {})}
        />
      </I18nProvider>
    );
    type('Фаррух');

    await waitFor(() => expect(screen.queryByText('Фаррух Азизов')).toBeNull());
    expect(searchPlayers).not.toHaveBeenCalled();
  });

  // Сеть отвалилась — палитра говорит об этом, а не притворяется, что таких людей нет.
  it('сбой поиска не выдаёт себя за «никого не нашли»', async () => {
    searchPlayers.mockImplementationOnce(async () => { throw new Error('network down'); });
    renderPalette(managerPerms);
    type('Фаррух');

    expect(await screen.findByText('Не удалось поискать клиентов')).toBeDefined();
  });
});
