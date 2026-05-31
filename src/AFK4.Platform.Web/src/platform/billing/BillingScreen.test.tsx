import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { BillingScreen } from './BillingScreen';

function fakeClient() {
  return {
    listSubscriptions: vi.fn().mockResolvedValue([]),
    listInvoices: vi.fn().mockResolvedValue([]),
    listPlans: vi.fn().mockResolvedValue([])
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
