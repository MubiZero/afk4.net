import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationInvoicesSection } from './OrganizationInvoicesSection';

function invoice(overrides: Record<string, unknown> = {}) {
  return {
    invoiceId: 'inv-1',
    organizationId: 'o',
    number: 7,
    kind: 'subscription',
    issuedAtUtc: '2026-09-01T00:00:00Z',
    dueAtUtc: '2026-09-08T00:00:00Z',
    amountMinorUnits: 290000,
    currencyCode: 'TJS',
    status: 'issued',
    ...overrides
  };
}

function fakeClient(overrides: Record<string, unknown> = {}) {
  return {
    listOrganizationInvoices: mock().mockResolvedValue([]),
    generateInvoice: mock().mockResolvedValue({}),
    createInvoice: mock().mockResolvedValue({}),
    markInvoicePaid: mock().mockResolvedValue(invoice({ status: 'paid' })),
    voidInvoice: mock().mockResolvedValue(invoice({ status: 'void' })),
    ...overrides
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

  // Раньше карточка показывала список без единой кнопки: отметить счёт оплаченным можно было
  // только уйдя в «Деньги» и найдя там этот же клуб заново.
  it('отмечает счёт оплаченным прямо в карточке клиента', async () => {
    const markInvoicePaid = mock().mockResolvedValue(invoice({ status: 'paid' }));
    const client = fakeClient({
      listOrganizationInvoices: mock().mockResolvedValue([invoice()]),
      markInvoicePaid
    });
    render(
      <I18nProvider><ToastProvider>
        <OrganizationInvoicesSection client={client} organizationId="o" canManageInvoices />
      </ToastProvider></I18nProvider>
    );

    await userEvent.click(await screen.findByRole('button', { name: 'Отметить оплаченным' }));
    // Кнопку подтверждения ищем ВНУТРИ открывшегося окна и дожидаемся самого окна: иначе под
    // нагрузкой клик уходил в кнопку строки, диалог открывался заново, и запрос не случался.
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Отметить оплаченным' }));

    await waitFor(() => expect(markInvoicePaid).toHaveBeenCalledTimes(1));
    expect((markInvoicePaid.mock.calls[0] as unknown[])[0]).toBe('inv-1');
  });

  // Аннулирование уходит в журнал с причиной — без неё подтверждать нечего.
  it('аннулирование требует причины', async () => {
    const voidInvoice = mock().mockResolvedValue(invoice({ status: 'void' }));
    const client = fakeClient({
      listOrganizationInvoices: mock().mockResolvedValue([invoice()]),
      voidInvoice
    });
    render(
      <I18nProvider><ToastProvider>
        <OrganizationInvoicesSection client={client} organizationId="o" canManageInvoices />
      </ToastProvider></I18nProvider>
    );

    await userEvent.click(await screen.findByRole('button', { name: 'Аннулировать' }));
    const dialog = await screen.findByRole('dialog');
    const confirm = within(dialog).getByRole('button', { name: /Аннулировать/ });
    expect(confirm).toBeDisabled();

    await userEvent.type(within(dialog).getByLabelText('Причина'), 'дубль');
    await userEvent.click(confirm);

    await waitFor(() => expect(voidInvoice).toHaveBeenCalledTimes(1));
    expect((voidInvoice.mock.calls[0] as unknown[])[1]).toBe('дубль');
  });

  // Оплаченный счёт этими кнопками не трогают: они собрали бы только отказ сервера.
  it('у оплаченного счёта действий нет', async () => {
    const client = fakeClient({ listOrganizationInvoices: mock().mockResolvedValue([invoice({ status: 'paid' })]) });
    render(
      <I18nProvider><ToastProvider>
        <OrganizationInvoicesSection client={client} organizationId="o" canManageInvoices />
      </ToastProvider></I18nProvider>
    );

    await screen.findByText(/#7/);
    expect(screen.queryByRole('button', { name: 'Отметить оплаченным' })).toBeNull();
  });

  // Право на счета сервер спрашивает отдельно от общего «управления деньгами».
  it('без права на счета действий нет', async () => {
    const client = fakeClient({ listOrganizationInvoices: mock().mockResolvedValue([invoice()]) });
    render(
      <I18nProvider><ToastProvider>
        <OrganizationInvoicesSection client={client} organizationId="o" />
      </ToastProvider></I18nProvider>
    );

    await screen.findByText(/#7/);
    expect(screen.queryByRole('button', { name: 'Отметить оплаченным' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Аннулировать' })).toBeNull();
  });
});
