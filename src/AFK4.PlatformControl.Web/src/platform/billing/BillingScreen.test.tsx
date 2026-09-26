import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { BillingScreen } from './BillingScreen';

function fakeClient() {
  return {
    debt: { listDebt: mock().mockResolvedValue([]) },
    organizations: { updateStatus: mock().mockResolvedValue({}) },
    supportNotes: { createSupportNote: mock().mockResolvedValue({}) },
    subscriptions: { listSubscriptions: mock().mockResolvedValue([]), updateSubscription: mock().mockResolvedValue({}) },
    invoices: { listInvoices: mock().mockResolvedValue([]), markInvoicePaid: mock().mockResolvedValue({}) },
    plans: {
      listPlans: mock().mockResolvedValue([]),
      getTerms: mock().mockResolvedValue({ trialDays: 30, promisedPaymentDays: 7, fallbackAfterOverdueDays: 14, updatedAtUtc: null })
    }
  } as never;
}

describe('BillingScreen', () => {
  it('renders the three tab triggers', async () => {
    render(<I18nProvider><ToastProvider><BillingScreen client={fakeClient()} tab="subscriptions" onTabChange={() => {}} canManageInvoices canManagePlans debtAccess={{ canMarkPaid: true, canGrantGrace: true, canToggleStatus: true, canAddNote: true }} /></ToastProvider></I18nProvider>);
    expect(screen.getByText('Подписки')).toBeInTheDocument();
    expect(screen.getByText('Счета')).toBeInTheDocument();
    expect(screen.getByText('Тарифы')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('Подписок пока нет. Подписка появляется вместе с организацией, а тариф ей меняют в карточке организации.')).toBeInTheDocument());
  });

  it('keeps plan mutations out of a read-only billing session', async () => {
    render(<I18nProvider><ToastProvider><BillingScreen client={fakeClient()} tab="plans" onTabChange={() => {}} canManageInvoices={false} canManagePlans={false} debtAccess={{ canMarkPaid: false, canGrantGrace: false, canToggleStatus: false, canAddNote: false }} /></ToastProvider></I18nProvider>);
    await waitFor(() => expect(screen.getByText('Тарифов пока нет. Без тарифа организации не назначить подписку.')).toBeVisible());
    expect(screen.queryByRole('button', { name: 'Создать тариф' })).not.toBeInTheDocument();
    // Условия оплаты видны, но без права менять тарифы — только для чтения.
    expect(await screen.findByLabelText('Пробный период, дней')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Сохранить условия' })).not.toBeInTheDocument();
  });
});
