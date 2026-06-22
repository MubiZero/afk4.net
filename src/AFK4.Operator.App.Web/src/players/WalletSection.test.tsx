import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WalletSection } from './WalletSection';

afterEach(cleanup);

const renderSection = (over: Partial<Parameters<typeof WalletSection>[0]> = {}) => {
  const onTopUp = mock(() => {});
  const onPayDebt = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <WalletSection
        balanceMinorUnits={46000}
        debtMinorUnits={0}
        currencyCode="TJS"
        topUpAmount="100.00"
        topUpReason="пополнение через кассу"
        debtAmount=""
        debtReason="оплата долга через кассу"
        canTopUp
        canPayDebt={false}
        onChangeTopUpAmount={() => {}}
        onChangeTopUpReason={() => {}}
        onChangeDebtAmount={() => {}}
        onChangeDebtReason={() => {}}
        onTopUp={onTopUp}
        onPayDebt={onPayDebt}
        {...over}
      />
    </I18nProvider>
  );
  return { onTopUp, onPayDebt };
};

describe('WalletSection', () => {
  it('renders balance and debt amounts', () => {
    renderSection({ debtMinorUnits: 3500 });
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина пополнения')).toBeInTheDocument();
  });

  it('keeps the debt form disabled when there is no debt', () => {
    renderSection({ debtMinorUnits: 0, canPayDebt: false });
    expect(screen.getByRole('button', { name: /Списать долг/ })).toBeDisabled();
  });

  it('fires onTopUp when the top-up button is clicked', () => {
    const { onTopUp } = renderSection();
    fireEvent.click(screen.getByRole('button', { name: /Пополнить депозит/ }));
    expect(onTopUp).toHaveBeenCalled();
  });

  it('shows the debt form fields and fires onPayDebt when debt is present', () => {
    const { onPayDebt } = renderSection({ debtMinorUnits: 3500, canPayDebt: true, debtAmount: '35.00' });
    expect(screen.getByLabelText('Сумма долга')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина долга')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Списать долг/ }));
    expect(onPayDebt).toHaveBeenCalled();
  });
});
