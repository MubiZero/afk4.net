import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { BillingTermsCard } from './BillingTermsCard';

describe('BillingTermsCard', () => {
  it('saves the terms the platform set, zero turning an offer off', async () => {
    const client = {
      getTerms: mock().mockResolvedValue({ trialDays: 30, promisedPaymentDays: 7, fallbackAfterOverdueDays: 14, updatedAtUtc: null }),
      updateTerms: mock().mockResolvedValue({ trialDays: 14, promisedPaymentDays: 0, fallbackAfterOverdueDays: 14, updatedAtUtc: '2026-09-26T10:00:00Z' })
    };
    render(<I18nProvider><ToastProvider><BillingTermsCard client={client} canManage /></ToastProvider></I18nProvider>);

    fireEvent.change(await screen.findByLabelText('Пробный период, дней'), { target: { value: '14' } });
    fireEvent.change(screen.getByLabelText('Обещанный платёж, дней'), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить условия' }));

    await waitFor(() => expect(client.updateTerms).toHaveBeenCalledWith({ trialDays: 14, promisedPaymentDays: 0, fallbackAfterOverdueDays: 14 }));
    expect(await screen.findByText('Условия оплаты сохранены')).toBeInTheDocument();
  });
});
