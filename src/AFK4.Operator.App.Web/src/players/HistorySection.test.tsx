import { describe, expect, it } from 'bun:test';
import { render, screen, cleanup } from '@testing-library/react';
import { afterEach } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { HistorySection } from './HistorySection';
import type { LedgerEntryDto } from '../operatorApiClients';

afterEach(cleanup);

const entry = (over: Partial<LedgerEntryDto>): LedgerEntryDto => ({
  ledgerEntryId: 'le-x', organizationId: 'o', branchId: 'b', playerAccountId: 'p',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 5000 }, quantitySeconds: 0,
  description: '', reason: '', reversesLedgerEntryId: null,
  createdByStaffUserId: 's', createdAtUtc: '2026-06-22T09:00:00Z', ...over
});

const renderSection = (entries: LedgerEntryDto[]) =>
  render(
    <I18nProvider initialLocale="ru">
      <HistorySection entries={entries} currencyCode="TJS" />
    </I18nProvider>
  );

describe('HistorySection', () => {
  it('renders localized type, description, reason and reversal badge', () => {
    renderSection([
      entry({ ledgerEntryId: 'le-1', entryType: 'top_up', description: 'Пополнение кошелька', reason: 'Касса' }),
      entry({ ledgerEntryId: 'le-2', entryType: 'refund', amount: { currencyCode: 'TJS', minorUnits: -2500 }, reversesLedgerEntryId: 'le-1', description: '' })
    ]);
    expect(screen.getByText('Пополнение')).toBeInTheDocument();       // ledger.type.top_up
    expect(screen.getByText('Пополнение кошелька')).toBeInTheDocument();
    expect(screen.getByText(/Касса/)).toBeInTheDocument();
    expect(screen.getByText('Возврат')).toBeInTheDocument();          // ledger.type.refund
    expect(screen.getByText('сторно')).toBeInTheDocument();           // reversal badge
  });

  it('renders the EmptyState when there are no entries', () => {
    renderSection([]);
    expect(screen.getByText('Операций нет')).toBeInTheDocument();
  });

  it('applies credit/debit class by amount sign', () => {
    const { container } = renderSection([
      entry({ ledgerEntryId: 'c', amount: { currencyCode: 'TJS', minorUnits: 5000 } }),
      entry({ ledgerEntryId: 'd', amount: { currencyCode: 'TJS', minorUnits: -1200 } })
    ]);
    expect(container.querySelector('.client-history-row.is-credit')).not.toBeNull();
    expect(container.querySelector('.client-history-row.is-debit')).not.toBeNull();
  });
});
