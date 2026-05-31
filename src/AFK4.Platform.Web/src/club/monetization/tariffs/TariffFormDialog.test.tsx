import type { ComponentProps } from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { TariffRow } from './tariffsModel';
import { TariffFormDialog } from './TariffFormDialog';

type DialogProps = ComponentProps<typeof TariffFormDialog>;

function client(overrides: Record<string, unknown> = {}) {
  return {
    createTariff: mock(async () => ({ tariffId: 't1' })),
    createTariffVersion: mock(async () => ({ tariffVersionId: 'v1' })),
    updateTariff: mock(async () => ({})),
    updateTariffVersion: mock(async () => ({})),
    ...overrides
  };
}

function renderDialog(props: Record<string, unknown>) {
  const merged = {
    open: true, branchId: 'b1', organizationId: 'org',
    onOpenChange: () => {}, onDone: () => {},
    ...props
  } as unknown as DialogProps;
  render(
    <I18nProvider><ToastProvider>
      <TariffFormDialog {...merged} />
    </ToastProvider></I18nProvider>
  );
}

it('creates a tariff then its first version', async () => {
  const c = client();
  renderDialog({ mode: 'create', client: c });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Дневной' } });
  fireEvent.change(screen.getByLabelText('Цена за минуту'), { target: { value: '2.5' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(c.createTariff).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org', name: 'Дневной' })));
  await waitFor(() => expect(c.createTariffVersion).toHaveBeenCalledWith('b1', 't1', expect.objectContaining({ pricePerMinuteMinorUnits: 250, organizationId: 'org', tariffId: 't1' })));
});

it('updates the tariff and its version in edit mode', async () => {
  const c = client();
  const initial: TariffRow = {
    tariffId: 't1', tariffVersionId: 'v1', name: 'Дневной', currencyCode: 'RUB',
    pricePerMinute: 2.5, minimumBillableMinutes: 1, roundingIncrementMinutes: 1,
    effectiveFromUtc: '2026-01-01T00:00:00.000Z', versionNumber: 1
  };
  renderDialog({ mode: 'edit', client: c, initial });
  fireEvent.change(screen.getByLabelText('Цена за минуту'), { target: { value: '3' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(c.updateTariff).toHaveBeenCalledWith('b1', 't1', expect.objectContaining({ name: 'Дневной', isActive: true })));
  await waitFor(() => expect(c.updateTariffVersion).toHaveBeenCalledWith('b1', 't1', 'v1', expect.objectContaining({ pricePerMinuteMinorUnits: 300 })));
});
