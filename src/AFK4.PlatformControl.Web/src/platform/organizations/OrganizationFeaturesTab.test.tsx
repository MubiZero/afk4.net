import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { OrganizationFeaturesTab } from './OrganizationFeaturesTab';
import type { OrganizationFeatureState } from '@/api/types';

function feature(overrides: Partial<OrganizationFeatureState> = {}): OrganizationFeatureState {
  return {
    featureKey: 'shop',
    name: 'Магазин',
    description: 'Продажа товаров через кассу',
    isEnabled: false,
    decisionLevel: 'default',
    overrideValue: null,
    overrideReason: null,
    overrideSetAtUtc: null,
    planValue: null,
    defaultValue: false,
    ...overrides
  };
}

function renderTab(client: any, features: OrganizationFeatureState[] = [feature()], planCode = 'growth', canManage = true) {
  const listFeatures = client.listFeatures ?? mock().mockResolvedValue(features);
  const fullClient = { listFeatures, setOverride: mock(), clearOverride: mock(), ...client };
  render(
    <I18nProvider><ToastProvider>
      <OrganizationFeaturesTab client={fullClient} organizationId="org-1" planCode={planCode} canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
  return fullClient;
}

describe('OrganizationFeaturesTab', () => {
  it('показывает для каждой фичи, чем решено', async () => {
    const byDefault = feature({ featureKey: 'shop', name: 'Магазин', decisionLevel: 'default', defaultValue: false, isEnabled: false });
    const byPlan = feature({ featureKey: 'loyalty', name: 'Лояльность', decisionLevel: 'plan', planValue: true, isEnabled: true });
    const byOverride = feature({
      featureKey: 'booking',
      name: 'Онлайн-бронь',
      decisionLevel: 'override',
      isEnabled: true,
      overrideValue: true,
      overrideReason: 'Пилот для сети «Восток»',
      overrideSetAtUtc: '2026-08-01T10:00:00Z'
    });
    renderTab({ listFeatures: mock().mockResolvedValue([byDefault, byPlan, byOverride]) });

    await waitFor(() => expect(screen.getByText('Магазин')).toBeInTheDocument());
    expect(screen.getByText('по умолчанию')).toBeInTheDocument();
    expect(screen.getByText('тариф growth')).toBeInTheDocument();
    expect(screen.getByText('вручную')).toBeInTheDocument();
    expect(screen.getByText(/Пилот для сети «Восток»/)).toBeInTheDocument();
  });

  it('ставит исключение с причиной', async () => {
    const client = renderTab({
      listFeatures: mock().mockResolvedValue([feature({ isEnabled: false })]),
      setOverride: mock().mockResolvedValue([feature({ isEnabled: true, decisionLevel: 'override', overrideValue: true, overrideReason: 'Пилот' })])
    });

    await waitFor(() => expect(screen.getByText('Магазин')).toBeInTheDocument());

    fireEvent.click(screen.getByRole('switch', { name: 'Магазин' }));
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'Пилот' } });
    fireEvent.click(screen.getByRole('button', { name: 'Применить' }));

    await waitFor(() => expect(client.setOverride).toHaveBeenCalledWith('org-1', 'shop', { isEnabled: true, reason: 'Пилот' }));
  });

  it('не даёт поставить исключение без причины', async () => {
    const client = renderTab({ listFeatures: mock().mockResolvedValue([feature({ isEnabled: false })]) });

    await waitFor(() => expect(screen.getByText('Магазин')).toBeInTheDocument());

    fireEvent.click(screen.getByRole('switch', { name: 'Магазин' }));
    const applyButton = screen.getByRole('button', { name: 'Применить' });
    expect(applyButton).toBeDisabled();

    fireEvent.click(applyButton);
    expect(client.setOverride).not.toHaveBeenCalled();
  });

  it('снимает исключение и возвращает решение тарифу', async () => {
    const overridden = feature({
      decisionLevel: 'override',
      isEnabled: true,
      overrideValue: true,
      overrideReason: 'Пилот',
      overrideSetAtUtc: '2026-08-01T10:00:00Z'
    });
    const client = renderTab({
      listFeatures: mock().mockResolvedValue([overridden]),
      clearOverride: mock().mockResolvedValue([feature({ decisionLevel: 'default', isEnabled: false })])
    });

    await waitFor(() => expect(screen.getByText('Магазин')).toBeInTheDocument());

    fireEvent.click(screen.getByRole('button', { name: 'Вернуть как у тарифа' }));

    await waitFor(() => expect(client.clearOverride).toHaveBeenCalledWith('org-1', 'shop'));
    await waitFor(() => expect(screen.getByText('по умолчанию')).toBeInTheDocument());
  });

  it('без права на управление показывает состояние и решение, но не рычаги', async () => {
    const overridden = feature({
      decisionLevel: 'override',
      isEnabled: true,
      overrideValue: true,
      overrideReason: 'Пилот',
      overrideSetAtUtc: '2026-08-01T10:00:00Z'
    });
    renderTab({ listFeatures: mock().mockResolvedValue([overridden]) }, undefined, 'growth', false);

    await waitFor(() => expect(screen.getByText('Магазин')).toBeInTheDocument());

    // Состояние и «чем решено» видны поддержке — это ей полезно и разрешено.
    expect(screen.getByText('Включена')).toBeInTheDocument();
    expect(screen.getByText('вручную')).toBeInTheDocument();
    expect(screen.getByText(/Пилот/)).toBeInTheDocument();

    // Но ни один рычаг управления не отрисован: увидеть «настоящую» кнопку, которая тут же
    // ответит 403, хуже, чем не увидеть кнопку вовсе.
    expect(screen.queryByRole('switch')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Причина')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Применить' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Вернуть как у тарифа' })).not.toBeInTheDocument();
  });

  it('сбрасывает черновик причины после успешного применения', async () => {
    const client = renderTab({
      listFeatures: mock().mockResolvedValue([feature({ isEnabled: false })]),
      setOverride: mock().mockResolvedValue([feature({ isEnabled: true, decisionLevel: 'override', overrideValue: true, overrideReason: 'Пилот' })])
    });

    await waitFor(() => expect(screen.getByText('Магазин')).toBeInTheDocument());

    fireEvent.click(screen.getByRole('switch', { name: 'Магазин' }));
    const reasonField = screen.getByLabelText('Причина') as HTMLTextAreaElement;
    fireEvent.change(reasonField, { target: { value: 'Пилот' } });
    fireEvent.click(screen.getByRole('button', { name: 'Применить' }));

    await waitFor(() => expect(client.setOverride).toHaveBeenCalled());
    // Успешное применение подтягивает свежий ответ сервера — старый текст причины не должен
    // пережить собственное действие и переехать на следующую попытку.
    await waitFor(() => expect((screen.getByLabelText('Причина') as HTMLTextAreaElement).value).toBe(''));
  });

  it('блокирует повторную отправку, пока запрос не завершился', async () => {
    let resolveSetOverride: (value: OrganizationFeatureState[]) => void = () => {};
    const setOverride = mock(() => new Promise<OrganizationFeatureState[]>(resolve => { resolveSetOverride = resolve; }));
    renderTab({
      listFeatures: mock().mockResolvedValue([feature({ isEnabled: false })]),
      setOverride
    });

    await waitFor(() => expect(screen.getByText('Магазин')).toBeInTheDocument());

    fireEvent.click(screen.getByRole('switch', { name: 'Магазин' }));
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'Пилот' } });
    const applyButton = screen.getByRole('button', { name: 'Применить' });

    fireEvent.click(applyButton);
    await waitFor(() => expect(applyButton).toBeDisabled());
    // Второй клик, пока первый запрос ещё в полёте, не должен завести второй PUT.
    fireEvent.click(applyButton);

    resolveSetOverride([feature({ isEnabled: true, decisionLevel: 'override', overrideValue: true, overrideReason: 'Пилот' })]);
    await waitFor(() => expect(applyButton).not.toBeDisabled());

    expect(setOverride).toHaveBeenCalledTimes(1);
  });
});
