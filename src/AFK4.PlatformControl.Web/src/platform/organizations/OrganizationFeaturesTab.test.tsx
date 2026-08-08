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

function renderTab(client: any, features: OrganizationFeatureState[] = [feature()], planCode = 'growth') {
  const listFeatures = client.listFeatures ?? mock().mockResolvedValue(features);
  const fullClient = { listFeatures, setOverride: mock(), clearOverride: mock(), ...client };
  render(
    <I18nProvider><ToastProvider>
      <OrganizationFeaturesTab client={fullClient} organizationId="org-1" planCode={planCode} />
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
});
