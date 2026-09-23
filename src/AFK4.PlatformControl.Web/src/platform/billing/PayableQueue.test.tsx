import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PayableQueue } from './PayableQueue';

afterEach(cleanup);

function invoice(overrides: Record<string, unknown> = {}) {
  return {
    invoiceId: 'inv-1',
    organizationId: 'org-1',
    organizationName: 'Клуб на Рудаки',
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

function setup(rows: unknown[], canManage = true) {
  const client = {
    listInvoices: mock().mockResolvedValue(rows),
    markInvoicePaid: mock().mockResolvedValue({}),
    voidInvoice: mock().mockResolvedValue({})
  };
  render(
    <I18nProvider><ToastProvider>
      <PayableQueue client={client as never} canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
  return client;
}

describe('PayableQueue', () => {
  // Ответ потерялся по дороге — человек нажимает «Отметить оплаченным» второй раз. Если бы ключ
  // попытки был новым, платёж отметился бы дважды: первый запрос до сервера дошёл.
  it('повтор после неудачи несёт тот же ключ попытки', async () => {
    const client = {
      listInvoices: mock().mockResolvedValue([invoice()]),
      markInvoicePaid: mock()
        .mockRejectedValueOnce(new Error('network'))
        .mockResolvedValue({}),
      voidInvoice: mock().mockResolvedValue({})
    };
    render(
      <I18nProvider><ToastProvider>
        <PayableQueue client={client as never} canManage />
      </ToastProvider></I18nProvider>
    );

    fireEvent.click(await screen.findByRole('button', { name: 'Отметить оплаченным' }));
    const confirm = screen.getAllByRole('button', { name: 'Отметить оплаченным' }).at(-1)!;
    fireEvent.click(confirm);
    await waitFor(() => expect(client.markInvoicePaid).toHaveBeenCalledTimes(1));
    fireEvent.click(confirm);
    await waitFor(() => expect(client.markInvoicePaid).toHaveBeenCalledTimes(2));

    expect(client.markInvoicePaid.mock.calls[1][2]).toBe(client.markInvoicePaid.mock.calls[0][2]);
  });

  // Референс платежа подписан «необязательно»: неактивная кнопка рядом с таким полем — обещание,
  // которого интерфейс не выполняет, и человек ищет несуществующее обязательное поле.
  it('отмечает оплаченным без референса', async () => {
    const client = setup([invoice()]);

    fireEvent.click(await screen.findByRole('button', { name: 'Отметить оплаченным' }));
    const confirm = screen.getAllByRole('button', { name: 'Отметить оплаченным' }).at(-1)!;
    expect(confirm).not.toBeDisabled();
    fireEvent.click(confirm);

    await waitFor(() => expect(client.markInvoicePaid).toHaveBeenCalledTimes(1));
    expect(client.markInvoicePaid.mock.calls[0]).toEqual(['inv-1', null, expect.any(String)]);
  });

  // Аннулирование уходит в журнал: без причины подтверждать нечего.
  it('аннулирование требует причины', async () => {
    const client = setup([invoice()]);

    fireEvent.click(await screen.findByRole('button', { name: 'Аннулировать' }));
    const confirm = screen.getAllByRole('button', { name: 'Аннулировать' }).at(-1)!;
    expect(confirm).toBeDisabled();

    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'дубль' } });
    fireEvent.click(confirm);

    await waitFor(() => expect(client.voidInvoice).toHaveBeenCalledTimes(1));
    expect(client.voidInvoice.mock.calls[0]).toEqual(['inv-1', 'дубль', expect.any(String)]);
  });

  // Очередь отвечает на вопрос «кто не заплатил»: просроченные сверху, оплаченным здесь не место.
  it('показывает просроченные первыми и не показывает закрытые счета', async () => {
    setup([
      invoice({ invoiceId: 'inv-paid', number: 5, status: 'paid' }),
      invoice({ invoiceId: 'inv-issued', number: 7, status: 'issued', dueAtUtc: '2026-09-08T00:00:00Z' }),
      invoice({ invoiceId: 'inv-overdue', number: 9, status: 'overdue', dueAtUtc: '2026-08-08T00:00:00Z' })
    ]);

    const rows = await screen.findAllByRole('listitem');
    expect(rows).toHaveLength(2);
    expect(rows[0]!.textContent).toContain('#9');
    expect(rows[1]!.textContent).toContain('#7');
  });

  it('без права на деньги очередь только читается', async () => {
    setup([invoice()], false);

    await screen.findByText(/#7/);
    expect(screen.queryByRole('button', { name: 'Отметить оплаченным' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Аннулировать' })).toBeNull();
  });

  it('пустая очередь так и говорит', async () => {
    setup([invoice({ status: 'paid' })]);

    await screen.findByText('Неоплаченных счетов нет — все выставленные счета закрыты. Новый счёт встанет сюда, как только его выставят.');
  });
});
