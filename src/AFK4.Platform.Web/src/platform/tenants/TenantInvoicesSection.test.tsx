import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantInvoicesSection } from './TenantInvoicesSection';

function fakeClient() {
  return {
    listTenantInvoices: mock().mockResolvedValue([]),
    generateInvoice: mock().mockResolvedValue({})
  } as never;
}

describe('TenantInvoicesSection', () => {
  it('shows empty state after load', async () => {
    render(<I18nProvider><ToastProvider><TenantInvoicesSection client={fakeClient()} organizationId="o" /></ToastProvider></I18nProvider>);
    await waitFor(() => expect(screen.getByText('Инвойсов пока нет.')).toBeInTheDocument());
  });
});
