import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { TariffOption } from '@/api/types';
import { TariffsTab } from './TariffsTab';

const option: TariffOption = {
  tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', tariffRuleVersionId: 'rv1', versionNumber: 1,
  currencyCode: 'RUB', pricePerMinuteMinorUnits: 250, minimumBillableMinutes: 1, roundingIncrementMinutes: 1,
  effectiveFromUtc: '2026-01-01T00:00:00.000Z'
};

function fakeClient() {
  return {
    getTariffOptions: vi.fn(async () => [option]),
    createTariff: vi.fn(async () => ({ tariffId: 't1' })),
    createTariffVersion: vi.fn(async () => ({ tariffVersionId: 'v1' })),
    updateTariff: vi.fn(async () => ({})),
    updateTariffVersion: vi.fn(async () => ({}))
  };
}

function renderTab(canManage: boolean) {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <TariffsTab client={client as never} branchId="b1" organizationId="org" canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
  return { client };
}

it('renders tariff rows', async () => {
  renderTab(true);
  expect(await screen.findByText('Дневной')).toBeInTheDocument();
});

it('opens the create dialog when managing', async () => {
  renderTab(true);
  await screen.findByText('Дневной');
  fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));
  expect(await screen.findByRole('button', { name: 'Создать' })).toBeInTheDocument();
});

it('hides the create trigger when read-only', async () => {
  renderTab(false);
  await screen.findByText('Дневной');
  expect(screen.queryByRole('button', { name: 'Создать тариф' })).not.toBeInTheDocument();
});
