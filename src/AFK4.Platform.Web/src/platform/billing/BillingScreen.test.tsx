import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { BillingScreen } from './BillingScreen';

function fakeClient() {
  return {
    subscriptions: { listSubscriptions: mock().mockResolvedValue([]) },
    invoices: { listInvoices: mock().mockResolvedValue([]), getBillingMetrics: mock().mockResolvedValue({}) },
    plans: { listPlans: mock().mockResolvedValue([]) }
  } as never;
}

describe('BillingScreen', () => {
  it('renders the three tab triggers', async () => {
    render(<I18nProvider><ToastProvider><BillingScreen client={fakeClient()} /></ToastProvider></I18nProvider>);
    expect(screen.getByText('Подписки')).toBeInTheDocument();
    expect(screen.getByText('Инвойсы')).toBeInTheDocument();
    expect(screen.getByText('Тарифы')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('Подписок пока нет.')).toBeInTheDocument());
  });
});
