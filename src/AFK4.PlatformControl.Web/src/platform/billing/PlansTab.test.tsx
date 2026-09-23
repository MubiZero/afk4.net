import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlansTab } from './PlansTab';

afterEach(cleanup);

function plan(overrides: Record<string, unknown> = {}) {
  return {
    planCode: 'starter',
    name: 'Стартовый',
    priceMinorUnits: 290000,
    currencyCode: 'TJS',
    billingInterval: 'monthly',
    maxBranches: 1,
    maxDevicesPerBranch: 20,
    maxConcurrentSessions: 20,
    maxStaffUsersPerBranch: 5,
    sortOrder: 1,
    isActive: true,
    ...overrides
  };
}

function setup(rows: unknown[] = [plan()], canManage = true) {
  const client = {
    listPlans: mock().mockResolvedValue(rows),
    createPlan: mock().mockResolvedValue({}),
    updatePlanCatalog: mock().mockResolvedValue({})
  };
  render(
    <I18nProvider><ToastProvider>
      <PlansTab client={client as never} canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
  return client;
}

describe('PlansTab', () => {
  // Скрытый тариф отличался от активного только цветом кружка: ни прочитать, ни зачитать вслух.
  it('называет состояние тарифа словом', async () => {
    setup([plan(), plan({ planCode: 'pro', name: 'Профи', isActive: false })]);

    await screen.findByText('Активен');
    await screen.findByText('Скрыт');
  });

  it('создаёт тариф тем, что ввели в форме', async () => {
    const client = setup([]);

    fireEvent.click(await screen.findByRole('button', { name: 'Новый тариф' }));
    fireEvent.change(screen.getByLabelText('Код тарифа'), { target: { value: 'pro' } });
    fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Профи' } });
    fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '590' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.createPlan).toHaveBeenCalledTimes(1));
    const request = client.createPlan.mock.calls[0][0];
    expect(request.planCode).toBe('pro');
    expect(request.name).toBe('Профи');
    expect(request.priceMinorUnits).toBe(59000);
    expect(request.billingInterval).toBe('monthly');
  });

  // Правка идёт по коду тарифа: перепутать его с названием значит создать второй тариф вместо
  // изменения первого.
  it('правит тариф по его коду', async () => {
    const client = setup();

    fireEvent.click(await screen.findByRole('button', { name: 'Изменить' }));
    fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Стартовый+' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updatePlanCatalog).toHaveBeenCalledTimes(1));
    expect(client.updatePlanCatalog.mock.calls[0][0]).toBe('starter');
    expect(client.updatePlanCatalog.mock.calls[0][1].name).toBe('Стартовый+');
    expect(client.createPlan).not.toHaveBeenCalled();
  });

  it('без права на деньги тарифы только читаются', async () => {
    setup([plan()], false);

    await screen.findByText('Стартовый');
    expect(screen.queryByRole('button', { name: 'Новый тариф' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Изменить' })).toBeNull();
  });

  // Пустой каталог раньше говорил только «Тарифов пока нет.» — и всё. Без тарифа организации не
  // назначить подписку, поэтому первый шаг назван прямо в пустом списке.
  it('пустой каталог зовёт завести первый тариф', async () => {
    setup([]);

    await screen.findByText('Тарифов пока нет. Без тарифа организации не назначить подписку.');
    fireEvent.click(screen.getByRole('button', { name: 'Завести первый тариф' }));

    expect(await screen.findByLabelText('Код тарифа')).toBeInTheDocument();
  });

  it('без права на тарифы пустой каталог называет, у кого оно есть', async () => {
    setup([], false);

    await screen.findByText('Это может сотрудник платформы с правом «Заводить и менять тарифы».');
    expect(screen.queryByRole('button', { name: 'Завести первый тариф' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Новый тариф' })).toBeNull();
  });
});
