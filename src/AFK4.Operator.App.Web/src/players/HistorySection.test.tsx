import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
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

const renderSection = (over: Partial<Parameters<typeof HistorySection>[0]> = {}) => {
  const onFilterChange = mock(() => {});
  const onLoadMore = mock(() => {});
  const { container } = render(
    <I18nProvider initialLocale="ru">
      <HistorySection
        entries={[entry({ ledgerEntryId: 'le-1', entryType: 'top_up', description: 'Пополнение кошелька', reason: 'Касса' })]}
        currencyCode="TJS"
        activeFilter={null}
        onFilterChange={onFilterChange}
        hasMore={false}
        onLoadMore={onLoadMore}
        loading={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onFilterChange, onLoadMore, container };
};

describe('HistorySection', () => {
  it('renders localized type, description, reason and amount sign class', () => {
    const { container } = renderSection({
      entries: [
        entry({ ledgerEntryId: 'le-1', entryType: 'top_up', description: 'Пополнение кошелька', reason: 'Касса' }),
        entry({ ledgerEntryId: 'le-2', entryType: 'gameplay_charge', amount: { currencyCode: 'TJS', minorUnits: -1200 }, description: 'Списание' })
      ]
    });
    expect(screen.getByText(/Пополнение кошелька/)).toBeInTheDocument();
    expect(screen.getByText(/Касса/)).toBeInTheDocument();
    expect(container.querySelector('.client-history-row.is-credit')).not.toBeNull();
    expect(container.querySelector('.client-history-row.is-debit')).not.toBeNull();
  });

  it('renders the filter chips including «Все» and fires onFilterChange', () => {
    const { onFilterChange } = renderSection();
    const allChip = screen.getByRole('button', { name: 'Все' });
    expect(allChip).toBeInTheDocument();
    // чип «Пополнение» (ledger.type.top_up) → entryType top_up
    fireEvent.click(screen.getByRole('button', { name: 'Пополнение' }));
    expect(onFilterChange).toHaveBeenCalledWith('top_up');
  });

  it('fires onFilterChange(null) when «Все» chip is clicked', () => {
    const { onFilterChange } = renderSection({ activeFilter: 'top_up' });
    fireEvent.click(screen.getByRole('button', { name: 'Все' }));
    expect(onFilterChange).toHaveBeenCalledWith(null);
  });

  it('shows «Показать ещё» only when hasMore and fires onLoadMore', () => {
    const { onLoadMore } = renderSection({ hasMore: true });
    const more = screen.getByRole('button', { name: /Показать ещё/ });
    fireEvent.click(more);
    expect(onLoadMore).toHaveBeenCalled();
  });

  it('hides «Показать ещё» when hasMore is false', () => {
    renderSection({ hasMore: false });
    expect(screen.queryByRole('button', { name: /Показать ещё/ })).not.toBeInTheDocument();
  });

  it('renders the EmptyState when there are no entries and not loading', () => {
    renderSection({ entries: [], loading: false });
    expect(screen.getByText('Операций нет')).toBeInTheDocument();
  });

  it('renders skeleton rows (not empty state) during the first load', () => {
    const { container } = renderSection({ entries: [], loading: true });
    expect(container.querySelector('.skeleton-block')).not.toBeNull();
    expect(screen.queryByText('Операций нет')).not.toBeInTheDocument();
  });
});
