import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { SubscriptionDialog } from './SubscriptionDialog';
import type { OrganizationSubscription, SubscriptionPlan, UpdateSubscriptionRequest } from '@/api/types';

afterEach(() => localStorage.removeItem('afk4.locale'));

function subscription(overrides: Partial<OrganizationSubscription> = {}): OrganizationSubscription {
  return {
    organizationSubscriptionId: 's1',
    organizationId: 'o1',
    planCode: 'starter',
    status: 'active',
    currentPeriodStartUtc: '2026-09-01T00:00:00Z',
    currentPeriodEndUtc: '2026-10-01T00:00:00Z',
    nextInvoiceUtc: '2026-10-01T00:00:00Z',
    amountMinorUnits: 150000,
    currencyCode: 'TJS',
    billingInterval: 'monthly',
    cancelAtPeriodEnd: false,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-09-01T00:00:00Z',
    paymentGraceUntilUtc: null,
    discountPercent: null,
    discountAmountMinorUnits: null,
    discountUntilUtc: null,
    discountReason: null,
    ...overrides
  };
}

function plan(planCode: string, name: string): SubscriptionPlan {
  return {
    planCode,
    name,
    priceMinorUnits: 150000,
    currencyCode: 'TJS',
    billingInterval: 'monthly',
    isActive: true
  } as SubscriptionPlan;
}

function setup(current = subscription(), plans = [plan('starter', 'Старт'), plan('growth', 'Рост')]) {
  const updateSubscription = mock().mockResolvedValue(current);
  const listPlans = mock().mockResolvedValue(plans);
  const onUpdated = mock();
  const onClose = mock();
  render(
    <I18nProvider>
      <ToastProvider>
        <SubscriptionDialog
          client={{ updateSubscription }}
          plansClient={{ listPlans }}
          organizationId="o1"
          subscription={current}
          onClose={onClose}
          onUpdated={onUpdated}
        />
      </ToastProvider>
    </I18nProvider>
  );
  return { updateSubscription, listPlans, onUpdated, onClose };
}

function sentRequest(updateSubscription: ReturnType<typeof mock>): UpdateSubscriptionRequest {
  return (updateSubscription.mock.calls[0] as unknown[])[1] as UpdateSubscriptionRequest;
}

describe('SubscriptionDialog', () => {
  // Ради этого диалог и переписан: `planCode` уходил захардкоженным null, и перевести клуб
  // с «Старта» на «Рост» через панель было негде — только завести организацию заново.
  it('переводит клуб на другой тариф', async () => {
    const { updateSubscription, listPlans } = setup();
    await waitFor(() => expect(listPlans).toHaveBeenCalled());

    fireEvent.change(await screen.findByLabelText('Тариф'), { target: { value: 'growth' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateSubscription).toHaveBeenCalled());
    expect(sentRequest(updateSubscription).planCode).toBe('growth');
  });

  // Сервер трактует null как «не трогать»: поле, которого человек не касался, не должно уезжать
  // в запрос как правка.
  it('нетронутые поля оставляет серверу без изменений', async () => {
    const { updateSubscription, listPlans } = setup();
    await waitFor(() => expect(listPlans).toHaveBeenCalled());

    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateSubscription).toHaveBeenCalled());
    const request = sentRequest(updateSubscription);
    expect(request.planCode).toBeNull();
    expect(request.status).toBeNull();
    expect(request.amountMinorUnits).toBeNull();
    expect(request.currentPeriodEndUtc).toBeNull();
  });

  it('задаёт скидку процентом вместе с причиной', async () => {
    const { updateSubscription, listPlans } = setup();
    await waitFor(() => expect(listPlans).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Скидка'), { target: { value: 'percent' } });
    fireEvent.change(screen.getByLabelText('Сколько процентов'), { target: { value: '15' } });
    fireEvent.change(screen.getByLabelText('За что скидка'), { target: { value: 'пилотный клуб' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateSubscription).toHaveBeenCalled());
    const request = sentRequest(updateSubscription);
    expect(request.discountPercent).toBe(15);
    expect(request.discountAmountMinorUnits).toBeNull();
    expect(request.discountReason).toBe('пилотный клуб');
  });

  // Скидку нельзя снять пустыми полями: пустое для сервера значит «оставь как было».
  it('снимает скидку отдельным признаком', async () => {
    const { updateSubscription, listPlans } = setup(subscription({ discountPercent: 20, discountReason: 'старт' }));
    await waitFor(() => expect(listPlans).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Скидка'), { target: { value: 'none' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateSubscription).toHaveBeenCalled());
    expect(sentRequest(updateSubscription).clearDiscount).toBe(true);
  });

  // Сервер откажет словами о своих полях («DiscountPercent must be between 1 and 100») — человеку
  // за панелью это ничего не объясняет, поэтому говорим до отправки.
  it('о негодной скидке говорит до отправки', async () => {
    const { updateSubscription, listPlans } = setup();
    await waitFor(() => expect(listPlans).toHaveBeenCalled());

    fireEvent.change(screen.getByLabelText('Скидка'), { target: { value: 'percent' } });
    fireEvent.change(screen.getByLabelText('Сколько процентов'), { target: { value: '140' } });

    expect(screen.getByRole('alert').textContent).toContain('от 1 до 100');
    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
    expect(updateSubscription).not.toHaveBeenCalled();
  });

  // Клуб с персональной ценой не должен выглядеть как клуб по прайсу.
  it('показывает действующую скидку', async () => {
    setup(subscription({ discountPercent: 20 }));

    expect(await screen.findByText(/Сейчас действует скидка: 20%/)).toBeInTheDocument();
  });

  // Каталог тарифов не доехал — остальные поля правятся, а список показывает текущий тариф:
  // молча подставить клубу чужой план нельзя.
  it('без каталога тарифов оставляет текущий тариф', async () => {
    const listPlans = mock().mockRejectedValue(new Error('network'));
    const updateSubscription = mock().mockResolvedValue(subscription());
    render(
      <I18nProvider>
        <ToastProvider>
          <SubscriptionDialog
            client={{ updateSubscription }}
            plansClient={{ listPlans }}
            organizationId="o1"
            subscription={subscription()}
            onClose={mock()}
            onUpdated={mock()}
          />
        </ToastProvider>
      </I18nProvider>
    );

    await waitFor(() => expect(listPlans).toHaveBeenCalled());
    const select = screen.getByLabelText('Тариф') as HTMLSelectElement;
    expect(select.value).toBe('starter');
    expect(select.options).toHaveLength(1);
  });
});
