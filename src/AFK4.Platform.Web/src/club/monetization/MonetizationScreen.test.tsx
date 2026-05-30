import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { TariffOption } from '@/api/types';
import { MonetizationScreen } from './MonetizationScreen';

const option: TariffOption = {
  tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', tariffRuleVersionId: 'rv1', versionNumber: 1,
  currencyCode: 'RUB', pricePerMinuteMinorUnits: 250, minimumBillableMinutes: 1, roundingIncrementMinutes: 1,
  effectiveFromUtc: '2026-01-01T00:00:00.000Z'
};

function setup() {
  const client = { getTariffOptions: vi.fn(async () => [option]), getCatalog: vi.fn(async () => []) };
  render(
    <I18nProvider><ToastProvider>
      <MonetizationScreen client={client as never} branchId="b1" organizationId="org" canManageTariffs canManageCatalog />
    </ToastProvider></I18nProvider>
  );
}

it('shows tariffs in the first tab', async () => {
  setup();
  expect(await screen.findByText('Дневной')).toBeInTheDocument();
});

it('shows the catalog on the products tab', async () => {
  setup();
  await screen.findByText('Дневной');
  const tab = screen.getByRole('tab', { name: 'Товары' });
  fireEvent.mouseDown(tab);
  fireEvent.click(tab);
  expect(await screen.findByText('Товары ещё не созданы.')).toBeInTheDocument();
});
