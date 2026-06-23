import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WalletSection } from './WalletSection';
import type { LedgerEntryDto } from '../operatorApiClients';

const recentEntry: LedgerEntryDto = {
  ledgerEntryId: 'le-1', organizationId: 'o', branchId: 'b', playerAccountId: 'p',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 50000 }, quantitySeconds: 0,
  description: '', reason: '', reversesLedgerEntryId: null,
  createdByStaffUserId: 's', createdAtUtc: '2026-06-23T04:00:00Z'
};

afterEach(cleanup);

const renderSection = (over: Partial<Parameters<typeof WalletSection>[0]> = {}) => {
  const onTopUp = mock(() => {});
  const onPayDebt = mock(() => {});
  const onCorrect = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <WalletSection
        debtMinorUnits={0}
        currencyCode="TJS"
        recentEntries={[]}
        onShowHistory={() => {}}
        topUpAmount="100.00"
        topUpReason="пополнение через кассу"
        debtAmount=""
        debtReason="оплата долга через кассу"
        canTopUp
        canPayDebt={false}
        canCorrect={false}
        onChangeTopUpAmount={() => {}}
        onChangeTopUpReason={() => {}}
        onChangeDebtAmount={() => {}}
        onChangeDebtReason={() => {}}
        onTopUp={onTopUp}
        onPayDebt={onPayDebt}
        onCorrect={onCorrect}
        {...over}
      />
    </I18nProvider>
  );
  return { onTopUp, onPayDebt, onCorrect };
};

describe('WalletSection', () => {
  it('renders the top-up form', () => {
    renderSection({ debtMinorUnits: 3500 });
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина пополнения')).toBeInTheDocument();
  });

  it('hides the debt form entirely when there is no debt', () => {
    renderSection({ debtMinorUnits: 0, canPayDebt: false });
    expect(screen.queryByRole('button', { name: /Списать долг/ })).toBeNull();
    expect(screen.queryByLabelText('Сумма долга')).toBeNull();
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

  it('fires onCorrect when the correction link is clicked', () => {
    const onCorrect = mock(() => {});
    renderSection({ canCorrect: true, onCorrect });
    fireEvent.click(screen.getByRole('button', { name: /Ручная корректировка/ }));
    expect(onCorrect).toHaveBeenCalled();
  });

  it('hides the correction link without permission', () => {
    renderSection({ canCorrect: false });
    expect(screen.queryByRole('button', { name: /Ручная корректировка/ })).toBeNull();
  });

  it('renders recent operations and fires onShowHistory', () => {
    const onShowHistory = mock(() => {});
    renderSection({ recentEntries: [recentEntry], onShowHistory });
    expect(screen.getByText('Последние операции')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Вся история/ }));
    expect(onShowHistory).toHaveBeenCalled();
  });

  it('shows empty recent state when there are no operations', () => {
    renderSection({ recentEntries: [] });
    expect(screen.getByText('Пока нет операций')).toBeInTheDocument();
  });
});
