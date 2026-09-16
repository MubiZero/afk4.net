import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationInvoicesSection } from './OrganizationInvoicesSection';

function fakeClient() {
  return {
    listOrganizationInvoices: mock().mockResolvedValue([]),
    generateInvoice: mock().mockResolvedValue({}),
    createInvoice: mock().mockResolvedValue({})
  } as never;
}

describe('OrganizationInvoicesSection', () => {
  it('shows empty state after load', async () => {
    render(<I18nProvider><ToastProvider><OrganizationInvoicesSection client={fakeClient()} organizationId="o" /></ToastProvider></I18nProvider>);
    await waitFor(() => expect(screen.getByText('Счетов пока нет.')).toBeInTheDocument());
  });

  // Две кнопки рядом делают разное: одна выставляет счёт подписки за период, другая — счёт
  // руками. Поэтому и называются они по делу, а не обе «Выставить счёт».
  it('обе кнопки выставления видны тому, кто ведёт биллинг', async () => {
    render(<I18nProvider><ToastProvider><OrganizationInvoicesSection client={fakeClient()} organizationId="o" /></ToastProvider></I18nProvider>);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Счёт по подписке' })).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Счёт вручную' })).toBeInTheDocument();
  });

  it('без права вести биллинг кнопок выставления нет', async () => {
    render(<I18nProvider><ToastProvider><OrganizationInvoicesSection client={fakeClient()} organizationId="o" canManage={false} /></ToastProvider></I18nProvider>);
    await waitFor(() => expect(screen.getByText('Счетов пока нет.')).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Счёт по подписке' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Счёт вручную' })).toBeNull();
  });

  it('кнопка «Счёт вручную» открывает форму', async () => {
    render(<I18nProvider><ToastProvider><OrganizationInvoicesSection client={fakeClient()} organizationId="o" /></ToastProvider></I18nProvider>);
    fireEvent.click(await screen.findByRole('button', { name: 'Счёт вручную' }));
    expect(screen.getByRole('dialog', { name: 'Счёт вне подписки' })).toBeInTheDocument();
  });
});
