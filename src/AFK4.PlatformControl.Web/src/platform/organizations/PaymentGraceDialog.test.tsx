import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import { PaymentGraceDialog } from './PaymentGraceDialog';

afterEach(cleanup);

function setup(currentGraceUntilUtc: string | null, updateSubscription = mock().mockResolvedValue({})) {
  const client = { updateSubscription };
  const onUpdated = mock();
  render(
    <I18nProvider><ToastProvider>
      <PaymentGraceDialog
        client={client as never}
        organizationId="org-1"
        currentGraceUntilUtc={currentGraceUntilUtc}
        onClose={() => {}}
        onUpdated={onUpdated}
      />
    </ToastProvider></I18nProvider>
  );
  return { client, onUpdated };
}

describe('PaymentGraceDialog', () => {
  // Отсрочка «до 20 сентября» означает весь день двадцатого: если отправить полночь, клуб
  // отключится утром того дня, до которого его обещали не трогать.
  it('отсрочка действует до конца выбранного дня', async () => {
    const { client, onUpdated } = setup(null);

    fireEvent.change(screen.getByLabelText('Отсрочка до'), { target: { value: '2026-09-20' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateSubscription).toHaveBeenCalledTimes(1));
    const [organizationId, patch] = client.updateSubscription.mock.calls[0];
    expect(organizationId).toBe('org-1');
    expect(patch.paymentGraceUntilUtc).toBe('2026-09-20T23:59:59.000Z');
    expect(patch.clearPaymentGrace).toBeNull();
    expect(onUpdated).toHaveBeenCalledTimes(1);
  });

  // Остальные поля подписки — план, сумма, статус — эта форма не трогает: они уезжают null,
  // иначе диалог отсрочки молча переписывал бы клубу тариф.
  it('не трогает остальные поля подписки', async () => {
    const { client } = setup(null);

    fireEvent.change(screen.getByLabelText('Отсрочка до'), { target: { value: '2026-09-20' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateSubscription).toHaveBeenCalledTimes(1));
    const patch = client.updateSubscription.mock.calls[0][1];
    expect(patch.planCode).toBeNull();
    expect(patch.status).toBeNull();
    expect(patch.amountMinorUnits).toBeNull();
    expect(patch.currentPeriodEndUtc).toBeNull();
  });

  it('снимает отсрочку отдельным признаком, а не пустой датой', async () => {
    const { client } = setup('2026-09-20T23:59:59Z');

    fireEvent.click(screen.getByRole('button', { name: 'Снять отсрочку' }));

    await waitFor(() => expect(client.updateSubscription).toHaveBeenCalledTimes(1));
    const patch = client.updateSubscription.mock.calls[0][1];
    expect(patch.clearPaymentGrace).toBe(true);
    expect(patch.paymentGraceUntilUtc).toBeNull();
  });

  it('снимать нечего, пока отсрочки нет', () => {
    setup(null);

    expect(screen.queryByRole('button', { name: 'Снять отсрочку' })).toBeNull();
    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
  });

  // Прошедшая дата — самая частая ошибка в этой форме, и отказ должен называть именно её.
  it('называет причину отказа сервера', async () => {
    const { client } = setup(null, mock().mockRejectedValue(
      new PlatformApiError(400, 'Payment grace must be in the future.', 'subscription_grace_not_in_future')
    ));

    fireEvent.change(screen.getByLabelText('Отсрочка до'), { target: { value: '2020-01-01' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateSubscription).toHaveBeenCalled());
    await screen.findByText('Отсрочку ставят на будущее: прошедшая дата ничего не отсрочит.');
  });
});
