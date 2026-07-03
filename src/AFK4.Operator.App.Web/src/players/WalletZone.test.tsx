import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WalletZone } from './WalletZone';

afterEach(cleanup);

const base = {
  balanceMinorUnits: 45000,
  debtMinorUnits: 0,
  currencyCode: 'TJS',
  topUpAmount: '',
  canTopUp: true,
  onChangeTopUpAmount: () => {},
  onTopUp: () => {},
  canPayDebt: true,
  onOpenPayDebt: () => {},
  canCorrect: false,
  onCorrect: () => {},
};

const renderZone = (over: Partial<typeof base> = {}) =>
  render(<I18nProvider initialLocale="ru"><WalletZone {...base} {...over} /></I18nProvider>);

describe('WalletZone', () => {
  it('renders two money stat cards (balance + debt)', () => {
    renderZone({ balanceMinorUnits: 45000, debtMinorUnits: 3500 });
    expect(document.querySelectorAll('.ui-card--stat')).toHaveLength(2);
    expect(screen.getByText('450 с.')).toHaveClass('ui-money');
  });

  it('marks the debt card danger only when debt is present', () => {
    const { rerender } = renderZone({ debtMinorUnits: 0 });
    expect(document.querySelector('.ui-card--stat.is-danger')).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><WalletZone {...base} debtMinorUnits={3500} /></I18nProvider>);
    expect(document.querySelector('.ui-card--stat.is-danger')).not.toBeNull();
  });

  it('fires onTopUp when the inline top-up form is submitted', () => {
    const onTopUp = mock(() => {});
    renderZone({ onTopUp });
    fireEvent.click(screen.getByRole('button', { name: /Пополнить/ }));
    expect(onTopUp).toHaveBeenCalled();
  });

  it('exposes the amount field labelled "Сумма пополнения"', () => {
    renderZone();
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();
  });

  it('hides the pay-debt button when there is no debt', () => {
    renderZone({ debtMinorUnits: 0 });
    expect(screen.queryByRole('button', { name: /Погасить долг|Списать долг/ })).toBeNull();
  });

  it('shows the pay-debt button and fires onOpenPayDebt when debt is present', () => {
    const onOpenPayDebt = mock(() => {});
    renderZone({ debtMinorUnits: 3500, onOpenPayDebt });
    fireEvent.click(screen.getByRole('button', { name: /Погасить долг|Списать долг/ }));
    expect(onOpenPayDebt).toHaveBeenCalled();
  });

  it('hides the correction button without permission and fires onCorrect with it', () => {
    const onCorrect = mock(() => {});
    const { rerender } = renderZone({ canCorrect: false });
    expect(screen.queryByRole('button', { name: /корректировк/i })).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><WalletZone {...base} canCorrect onCorrect={onCorrect} /></I18nProvider>);
    fireEvent.click(screen.getByRole('button', { name: /корректировк/i }));
    expect(onCorrect).toHaveBeenCalled();
  });
});
