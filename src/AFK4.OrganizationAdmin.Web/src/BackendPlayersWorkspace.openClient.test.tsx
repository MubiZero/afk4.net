import { cleanup, render, screen } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';
import { playersSnapshotCache } from './players/playersSnapshot';

// bun's mock.module is not hoisted above static imports — register before importing the workspace.
const people = [
  {
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
    activePackageRemainingMinutes: 0
  },
  {
    playerAccountId: 'p2',
    displayName: 'Фаррух Одинаев',
    phoneNumber: '+992937380071',
    walletBalanceMinorUnits: 2000,
    debtBalanceMinorUnits: 0,
    activePackageCount: 0,
    isActive: true,
    createdAtUtc: '2026-08-02T00:00:00Z',
    lastActivityAtUtc: null,
    activePackageName: null,
    activePackageRemainingMinutes: 0
  }
];

// Сигнатуру повторяем не для красоты: без неё TypeScript не знает, что у вызова есть
// аргументы, и проверка «искали по этой строке» не типизируется.
const searchPlayers = mock(async (_branchId: string, _query: string, _limit?: number) => people);

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    players: {
      searchPlayers,
      getWalletSummary: mock(async () => ({
        walletBalance: { currencyCode: 'TJS', minorUnits: 2000 },
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

// Права намеренно пустые: раздел грузит список клиентов и без них, а журнал, смены и брони
// в этом сценарии только мешали бы сети.
const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [] },
  branchId: 'b1'
};

function renderWorkspace(openClient?: { playerAccountId: string; search: string }) {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <BackendPlayersWorkspace currencyCode="TJS" backend={backend as never} openClient={openClient} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('BackendPlayersWorkspace · заход из палитры', () => {
  afterEach(() => {
    cleanup();
    searchPlayers.mockClear();
    // Снимок списка переживает уход из раздела — между тестами это чужие данные.
    playersSnapshotCache.clear();
  });

  // Обещание палитры — «отвези меня к нему». Открыть раздел на первом попавшемся тёзке значит
  // это обещание нарушить.
  it('открывает карточку того, кого выбрали, а не первого в списке', async () => {
    renderWorkspace({ playerAccountId: 'p2', search: 'Фаррух' });

    const drawerName = await screen.findByText('Фаррух Одинаев', { selector: '.drawer-name' });
    expect(drawerName).toBeDefined();
    expect(searchPlayers.mock.calls[0]![1]).toBe('Фаррух');
  });

  // Обычный заход в раздел — как был: список без предвыбранного поиска.
  it('без выбора из палитры ищет по пустой строке', async () => {
    renderWorkspace();

    await screen.findByText('Фаррух Азизов', { selector: '.drawer-name' });
    expect(searchPlayers.mock.calls[0]![1]).toBe('');
  });
});
