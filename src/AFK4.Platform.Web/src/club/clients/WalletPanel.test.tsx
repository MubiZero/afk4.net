import { render, screen } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { WalletSummary } from '@/api/types';
import { WalletPanel } from './WalletPanel';

const summary: WalletSummary = {
  playerAccountId: 'p1',
  walletBalance: { currencyCode: 'TJS', minorUnits: 50000 },
  debtBalance: { currencyCode: 'TJS', minorUnits: 1500 },
  recentEntries: [{
    ledgerEntryId: 'l1', organizationId: 'org', branchId: 'b1', playerAccountId: 'p1',
    sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
    amount: { currencyCode: 'TJS', minorUnits: 50000 }, quantitySeconds: 0,
    description: 'd', reason: 'Касса', reversesLedgerEntryId: null,
    createdByStaffUserId: 's1', createdAtUtc: '2026-05-30T10:00:00.000Z'
  }]
};

function renderPanel(client: { getWalletSummary: () => Promise<WalletSummary> }) {
  render(
    <I18nProvider>
      <WalletPanel client={client as never} playerAccountId="p1" />
    </I18nProvider>
  );
}

it('shows balances and a translated ledger entry type', async () => {
  renderPanel({ getWalletSummary: vi.fn(async () => summary) });
  expect(await screen.findByText('Пополнение')).toBeInTheDocument();
  expect(screen.getByText('Касса')).toBeInTheDocument();
  expect(screen.getByText('История операций')).toBeInTheDocument();
});

it('shows an empty message when there is no history', async () => {
  renderPanel({ getWalletSummary: vi.fn(async () => ({ ...summary, recentEntries: [] })) });
  expect(await screen.findByText('Операций пока нет.')).toBeInTheDocument();
});
