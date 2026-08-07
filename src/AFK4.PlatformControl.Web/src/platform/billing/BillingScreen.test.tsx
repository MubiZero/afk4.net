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
    invoices: { listInvoices: mock().mockResolvedValue([]), getBillingMetrics: mock().mockResolvedValue({}), markInvoicePaid: mock().mockResolvedValue({}) },
    plans: { listPlans: mock().mockResolvedValue([]) }
  } as never;
}

describe('BillingScreen', () => {
  it('renders the three tab triggers', async () => {
    render(<I18nProvider><ToastProvider><BillingScreen client={fakeClient()} tab="subscriptions" onTabChange={() => {}} canManage /></ToastProvider></I18nProvider>);
    expect(screen.getByText('Подписки')).toBeInTheDocument();
    expect(screen.getByText('Инвойсы')).toBeInTheDocument();
    expect(screen.getByText('Тарифы')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('Подписок пока нет.')).toBeInTheDocument());
  });

  it('keeps plan mutations out of a read-only billing session', async () => {
    render(<I18nProvider><ToastProvider><BillingScreen client={fakeClient()} tab="plans" onTabChange={() => {}} canManage={false} /></ToastProvider></I18nProvider>);
    await waitFor(() => expect(screen.getByText('Тарифов пока нет.')).toBeVisible());
    expect(screen.queryByRole('button', { name: 'Создать тариф' })).not.toBeInTheDocument();
  });
});
