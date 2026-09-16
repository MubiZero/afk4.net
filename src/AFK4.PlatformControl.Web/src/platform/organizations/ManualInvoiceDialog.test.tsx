import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { ManualInvoiceDialog } from './ManualInvoiceDialog';

function renderDialog(createInvoice: ReturnType<typeof mock>) {
  const onCreated = mock();
  render(
    <I18nProvider>
      <ToastProvider>
        <ManualInvoiceDialog
          client={{ createInvoice } as never}
          organizationId="org-1"
          currencyCode="TJS"
          onClose={() => {}}
          onCreated={onCreated}
        />
      </ToastProvider>
    </I18nProvider>
  );
  return { onCreated };
}

function fill(amount: string, description: string) {
  fireEvent.change(screen.getByLabelText(/^Сумма/), { target: { value: amount } });
  fireEvent.change(screen.getByLabelText(/^За что/), { target: { value: description } });
}

describe('ManualInvoiceDialog', () => {
  it('разовый счёт уходит положительной суммой в минорных единицах', async () => {
    const createInvoice = mock().mockResolvedValue({ invoiceId: 'inv-1' });
    const { onCreated } = renderDialog(createInvoice);

    fill('120,50', 'Настройка второго зала');
    fireEvent.click(screen.getByRole('button', { name: 'Выставить' }));

    await waitFor(() => expect(createInvoice).toHaveBeenCalledTimes(1));
    expect(createInvoice.mock.calls[0][1]).toMatchObject({
      kind: 'one_off',
      amountMinorUnits: 12050,
      description: 'Настройка второго зала',
      dueAtUtc: null
    });
    expect(onCreated).toHaveBeenCalled();
  });

  // Знак ставит вид счёта, а не человек: минус, набранный руками, однажды сотрёт настоящий долг.
  it('кредит-нота уходит отрицательной суммой, хотя вводят положительную', async () => {
    const createInvoice = mock().mockResolvedValue({ invoiceId: 'inv-2' });
    renderDialog(createInvoice);

    fireEvent.change(screen.getByLabelText(/^Что выставляем/), { target: { value: 'credit' } });
    fill('40', 'Компенсация простоя');
    fireEvent.click(screen.getByRole('button', { name: 'Выставить' }));

    await waitFor(() => expect(createInvoice).toHaveBeenCalledTimes(1));
    expect(createInvoice.mock.calls[0][1].amountMinorUnits).toBe(-4000);
  });

  it('срок оплаты у кредит-ноты не спрашивают — платить по ней нечего', () => {
    renderDialog(mock().mockResolvedValue({}));

    expect(screen.getByLabelText(/^Оплатить до/)).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText(/^Что выставляем/), { target: { value: 'credit' } });
    expect(screen.queryByLabelText(/^Оплатить до/)).toBeNull();
  });

  it('без суммы и описания выставить нельзя', () => {
    renderDialog(mock().mockResolvedValue({}));

    expect(screen.getByRole('button', { name: 'Выставить' })).toBeDisabled();
    fill('50', '');
    expect(screen.getByRole('button', { name: 'Выставить' })).toBeDisabled();
    fill('50', 'Разовая услуга');
    expect(screen.getByRole('button', { name: 'Выставить' })).not.toBeDisabled();
  });
});
