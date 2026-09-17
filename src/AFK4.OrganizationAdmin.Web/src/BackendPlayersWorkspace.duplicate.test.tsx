import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';
import { playersSnapshotCache } from './players/playersSnapshot';

const existing = {
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

const createPlayer = mock(async () => ({ playerAccountId: 'p2', displayName: 'Двойник', phoneNumber: '+992937380070' }));
const searchPlayers = mock(async () => [existing]);

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    players: {
      searchPlayers,
      createPlayer,
      getWalletSummary: mock(async () => ({
        walletBalance: { currencyCode: 'TJS', minorUnits: 1000 },
        debtBalance: { currencyCode: 'TJS', minorUnits: 0 }
      })),
      getPlayerPackages: mock(async () => [])
    }
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
  session: { accessToken: 't', organizationId: 'org', permissions: ['organization.players.create'] },
  branchId: 'b1'
};

async function fillNewClient(phone: string) {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <BackendPlayersWorkspace currencyCode="TJS" backend={backend as never} />
      </ToastProvider>
    </I18nProvider>
  );
  await screen.findByText('Фаррух Азизов', { selector: '.drawer-name' });
  fireEvent.click(screen.getByRole('button', { name: 'Новый клиент' }));
  fireEvent.change(await screen.findByLabelText('Имя нового клиента'), { target: { value: 'Фаррух А.' } });
  fireEvent.change(screen.getByLabelText('Телефон нового клиента'), { target: { value: phone } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
}

describe('BackendPlayersWorkspace · двойник по телефону', () => {
  afterEach(() => {
    cleanup();
    createPlayer.mockClear();
    searchPlayers.mockClear();
    playersSnapshotCache.clear();
  });

  // Тот же человек со второй карточкой — это разошедшиеся баланс, долг и история. Поиск мог не
  // найти его из-за другого формата номера, и оператор без предупреждения заводил двойника.
  it('не заводит вторую карточку на тот же номер', async () => {
    await fillNewClient('937380070');

    await waitFor(() => expect(searchPlayers).toHaveBeenCalled());
    expect(createPlayer).not.toHaveBeenCalled();
    expect(await screen.findByText(/Клиент с таким номером уже есть: Фаррух Азизов/)).toBeInTheDocument();
  });

  it('новый номер создаёт карточку как обычно', async () => {
    await fillNewClient('900111222');

    await waitFor(() => expect(createPlayer).toHaveBeenCalledTimes(1));
  });
});
