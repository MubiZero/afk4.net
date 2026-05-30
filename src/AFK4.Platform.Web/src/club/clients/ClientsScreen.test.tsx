import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PlayerSearchResult, WalletSummary } from '@/api/types';
import { ClientsScreen } from './ClientsScreen';

const result: PlayerSearchResult = {
  playerAccountId: 'p1', displayName: 'Иван', phoneNumber: '+992900',
  walletBalanceMinorUnits: 50000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true
};

const summary: WalletSummary = {
  playerAccountId: 'p1',
  walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
  recentEntries: []
};

function fakeClient() {
  return {
    searchPlayers: vi.fn(async () => [result]),
    getWalletSummary: vi.fn(async () => summary),
    createPlayer: vi.fn(async () => ({ playerAccountId: 'p2' }))
  };
}

function renderScreen(opts: { canCreate?: boolean; canViewBilling?: boolean } = {}) {
  render(
    <I18nProvider><ToastProvider>
      <ClientsScreen
        client={fakeClient() as never} branchId="b1" organizationId="org"
        canCreate={opts.canCreate ?? true} canViewBilling={opts.canViewBilling ?? true}
      />
    </ToastProvider></I18nProvider>
  );
}

it('lists search results', async () => {
  renderScreen();
  expect(await screen.findByText('Иван')).toBeInTheDocument();
});

it('selecting a row shows the client detail', async () => {
  renderScreen();
  fireEvent.click(await screen.findByText('Иван'));
  expect(await screen.findByText('Редактирование данных клиента недоступно.')).toBeInTheDocument();
});

it('shows the create trigger only when permitted', async () => {
  renderScreen({ canCreate: false });
  await screen.findByText('Иван');
  expect(screen.queryByRole('button', { name: 'Создать клиента' })).not.toBeInTheDocument();
});
