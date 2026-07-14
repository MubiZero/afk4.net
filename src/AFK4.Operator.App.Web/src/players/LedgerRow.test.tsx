import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { LedgerRow } from './LedgerRow';
import type { LedgerEntryView } from './playersModel';

afterEach(cleanup);

const view: LedgerEntryView = {
  id: 'le-1',
  timeLabel: '04:00',
  typeLabel: 'Пополнение',
  description: 'Пополнение кошелька',
  reason: 'Касса',
  amountMinorUnits: 50000,
  currencyCode: 'TJS',
  isCredit: true,
  isReversal: false
};

const renderRow = (over: Partial<Parameters<typeof LedgerRow>[0]> = {}) =>
  render(
    <I18nProvider initialLocale="ru">
      <LedgerRow view={view} currencyCode="TJS" {...over} />
    </I18nProvider>
  );

describe('LedgerRow', () => {
  it('renders time, type, detail and a signed positive amount', () => {
    renderRow();
    expect(screen.getByText('04:00')).toBeInTheDocument();
    expect(screen.getByText('Пополнение')).toBeInTheDocument();
    expect(screen.getByText('Пополнение кошелька · Касса')).toBeInTheDocument();
    expect(screen.getByText('+500 с.')).toHaveClass('ui-money--pos');
  });

  it('hides detail and refund in compact variant', () => {
    renderRow({ compact: true, canRefund: true, onRefund: () => {} });
    expect(screen.queryByText('Пополнение кошелька · Касса')).toBeNull();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('fires onRefund from the row action when refundable', () => {
    const onRefund = mock(() => {});
    renderRow({ canRefund: true, onRefund });
    fireEvent.click(screen.getByRole('button', { name: /Вернуть/ }));
    expect(onRefund).toHaveBeenCalled();
  });

  it('never shows refund for a reversal entry', () => {
    renderRow({ view: { ...view, isReversal: true }, canRefund: true, onRefund: () => {} });
    expect(screen.queryByRole('button')).toBeNull();
  });
});
