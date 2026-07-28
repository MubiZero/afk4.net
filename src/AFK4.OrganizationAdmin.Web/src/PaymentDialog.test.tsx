import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PaymentDialog } from './PaymentDialog';

afterEach(cleanup);

describe('PaymentDialog split method choices', () => {
  it('renders each method once and disables Add after all methods are selected', () => {
    render(
      <I18nProvider initialLocale="ru">
        <PaymentDialog
          lines={[{ label: 'Cola', amountMinorUnits: 10_000 }]}
          dueLabel="Итого"
          grandTotalMinorUnits={10_000}
          currencyCode="TJS"
          walletBalanceMinorUnits={20_000}
          allowSplit
          disabled={false}
          onCancel={() => undefined}
          onConfirm={() => undefined}
        />
      </I18nProvider>
    );

    fireEvent.click(screen.getByRole('tab', { name: 'Смешанно' }));
    const add = screen.getByRole('button', { name: /Ещё способ/ });
    fireEvent.click(add);
    fireEvent.click(add);

    const selects = screen.getAllByRole('combobox', { name: 'Способ оплаты' });
    expect(selects).toHaveLength(3);
    expect(selects.map((select) => (select as HTMLSelectElement).value)).toEqual(['cash', 'card_manual', 'wallet']);
    expect(add).toBeDisabled();
    expect(Array.from((selects[0] as HTMLSelectElement).options).map((option) => option.value)).toEqual(['cash']);
  });
});
