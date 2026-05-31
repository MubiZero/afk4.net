import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
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

function fakeClient() {
  return {
    getWalletSummary: mock(async () => summary),
    topUpWallet: mock(async () => ({ ledgerEntryId: 'l9' })),
    payDebt: mock(async () => ({ ledgerEntryId: 'l9' })),
    createManualCorrection: mock(async () => ({ ledgerEntryId: 'l9' })),
    refundLedgerEntry: mock(async () => ({ ledgerEntryId: 'l9' }))
  };
}

function renderPanel(moneyPerms?: { topUp: boolean; payDebt: boolean; correct: boolean; refund: boolean }) {
  render(
    <I18nProvider><ToastProvider>
      <WalletPanel client={fakeClient() as never} playerAccountId="p1" organizationId="org" moneyPerms={moneyPerms} />
    </ToastProvider></I18nProvider>
  );
}

it('shows balances and a translated ledger entry type', async () => {
  renderPanel();
  expect(await screen.findByText('Пополнение')).toBeInTheDocument();
  expect(screen.getByText('Касса')).toBeInTheDocument();
  expect(screen.getByText('История операций')).toBeInTheDocument();
});

it('hides all action buttons when no permissions are given', async () => {
  renderPanel();
  await screen.findByText('Пополнение');
  expect(screen.queryByRole('button', { name: 'Пополнить' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Возврат' })).not.toBeInTheDocument();
});

it('opens the top-up dialog when permitted', async () => {
  renderPanel({ topUp: true, payDebt: false, correct: false, refund: false });
  await screen.findByText('Пополнение');
  fireEvent.click(screen.getByRole('button', { name: 'Пополнить' }));
  expect(await screen.findByText('Пополнение кошелька')).toBeInTheDocument();
});

it('shows a refund button per ledger row when permitted', async () => {
  renderPanel({ topUp: false, payDebt: false, correct: false, refund: true });
  await screen.findByText('Пополнение');
  expect(screen.getByRole('button', { name: 'Возврат' })).toBeInTheDocument();
});
