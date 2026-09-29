import { afterAll, describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

// MoneyDto over the wire is { currencyCode, minorUnits } — see branchRollupModel.test.ts note.
const summary = {
  utilization: { onlineDevices: 2, offlineDevices: 0, activeSessions: 1 },
  revenue: { totalRevenue: { minorUnits: 5000, currencyCode: 'TJS' } },
  alertPressure: { totalAlerts: 0 }
};

// Остальные помощники — настоящие: список филиалов собран на общем списке Управления.
const realHelpers = (globalThis as typeof globalThis & { __afk4RealOperatorHelpers: typeof import('../../operatorHelpers') }).__afk4RealOperatorHelpers;
mock.module('../../operatorHelpers', () => ({
  ...realHelpers,
  createAuthenticatedOperatorClients: () => ({
    orgBranches: { getOwnerBranches: mock(async () => [{ branchId: 'b1', name: 'Центр' }]) },
    settings: { getBranchProfile: mock(async () => ({ name: 'Центр', city: 'Душанбе' })) },
    dashboard: { getSummary: mock(async () => summary) }
  }),
  dashboardRangeQuery: (from: string, to: string) => ({ fromUtc: from, toUtc: to, limit: 8 }),
  toDateInputValue: () => '2026-07-24'
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

afterAll(() => {
  mock.module('../../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

describe('BranchesDestination', () => {
  // Одна главная цифра — выручка сети за сегодня; остальное — строками по филиалам, без пяти
  // плиток итогов, которые повторялись ещё раз в карточке каждого филиала.
  it('shows the network revenue once and each branch as a row', async () => {
    const { BranchesDestination } = await import('./BranchesDestination');
    const { container } = render(
      <I18nProvider initialLocale="ru">
        <BranchesDestination backend={backend as never} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Душанбе')).toBeInTheDocument());
    expect(screen.getByText('Выручка сети сегодня')).toBeInTheDocument();
    expect(screen.getByText('1 филиал · ПК на связи 2 из 2')).toBeInTheDocument();
    expect(container.querySelectorAll('.mgmt-row')).toHaveLength(1);
    expect(screen.getByText('2 из 2')).toBeInTheDocument();
  });

  it('renders a branch row with the branch name', async () => {
    const { BranchesDestination } = await import('./BranchesDestination');
    render(
      <I18nProvider initialLocale="ru">
        <BranchesDestination backend={backend as never} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Душанбе')).toBeInTheDocument());
    expect(screen.getByText('Центр')).toBeInTheDocument();
  });

  // Кнопка «Добавить филиал» стояла здесь вечно выключенной с подписью «пока недоступно».
  // Обещание, которое никто не собирался выполнять из этого приложения: новый филиал меняет
  // лимит ПК по тарифу и счёт клуба, и открывает его платформа.
  it('не показывает выключенную кнопку добавления филиала', async () => {
    const { BranchesDestination } = await import('./BranchesDestination');
    render(
      <I18nProvider initialLocale="ru">
        <BranchesDestination backend={backend as never} />
      </I18nProvider>
    );

    await waitFor(() => expect(screen.getByText('Душанбе')).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Добавить филиал' })).toBeNull();
    expect(screen.getByText(/Новый филиал открывает платформа/)).toBeInTheDocument();
  });
});
