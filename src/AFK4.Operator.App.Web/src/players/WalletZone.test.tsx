import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WalletZone } from './WalletZone';

afterEach(cleanup);

const base = {
  debtMinorUnits: 0,
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
  it('disables the top-up amount field together with the rest of the form when topUp is not allowed', () => {
    renderZone({ canTopUp: false });
    expect(screen.getByLabelText('Сумма пополнения')).toBeDisabled();
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
    expect(screen.queryByRole('button', { name: 'Списать долг' })).toBeNull();
  });

  it('shows the pay-debt button and fires onOpenPayDebt when debt is present', () => {
    const onOpenPayDebt = mock(() => {});
    renderZone({ debtMinorUnits: 3500, onOpenPayDebt });
    fireEvent.click(screen.getByRole('button', { name: 'Списать долг' }));
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
