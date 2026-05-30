import { render, screen } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { PlayerRow } from './clientsModel';
import { ClientDetail } from './ClientDetail';

const player: PlayerRow = {
  playerAccountId: 'p1', displayName: 'Иван', phone: '+992900',
  walletMajor: 500, debtMajor: 0, activePackageCount: 1, isActive: true
};

function fakeClient() {
  return {
    getWalletSummary: vi.fn(async () => ({
      playerAccountId: 'p1',
      walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
      debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
      recentEntries: []
    }))
  };
}

it('shows the header and the edit-unavailable note', () => {
  render(
    <I18nProvider>
      <ClientDetail client={fakeClient() as never} player={player} organizationId="org" canViewBilling />
    </I18nProvider>
  );
  expect(screen.getByText('Иван')).toBeInTheDocument();
  expect(screen.getByText('Редактирование данных клиента недоступно.')).toBeInTheDocument();
});

it('renders the wallet panel when billing is permitted', async () => {
  render(
    <I18nProvider>
      <ClientDetail client={fakeClient() as never} player={player} organizationId="org" canViewBilling />
    </I18nProvider>
  );
  expect(await screen.findByText('История операций')).toBeInTheDocument();
});

it('hides the wallet panel and shows a note when billing is not permitted', () => {
  const client = fakeClient();
  render(
    <I18nProvider>
      <ClientDetail client={client as never} player={player} organizationId="org" canViewBilling={false} />
    </I18nProvider>
  );
  expect(screen.getByText('Просмотр баланса недоступен.')).toBeInTheDocument();
  expect(client.getWalletSummary).not.toHaveBeenCalled();
});
