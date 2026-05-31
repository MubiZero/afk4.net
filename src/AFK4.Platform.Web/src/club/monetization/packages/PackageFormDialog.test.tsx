import type { ComponentProps } from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PackageRow } from './packagesModel';
import { PackageFormDialog } from './PackageFormDialog';

type DialogProps = ComponentProps<typeof PackageFormDialog>;

function client(overrides: Record<string, unknown> = {}) {
  return {
    createPackageDefinition: mock(async () => ({ packageDefinitionId: 'pk1' })),
    updatePackageDefinition: mock(async () => ({ packageDefinitionId: 'pk1' })),
    ...overrides
  };
}

function renderDialog(props: Record<string, unknown>) {
  const merged = {
    open: true, branchId: 'b1', organizationId: 'org',
    onOpenChange: () => {}, onDone: () => {},
    ...props
  } as unknown as DialogProps;
  render(<I18nProvider><ToastProvider><PackageFormDialog {...merged} /></ToastProvider></I18nProvider>);
}

it('creates a package with minor-unit price and seconds', async () => {
  const c = client();
  renderDialog({ mode: 'create', client: c });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Старт' } });
  fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '500' } });
  fireEvent.change(screen.getByLabelText('Включено минут'), { target: { value: '60' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(c.createPackageDefinition).toHaveBeenCalledWith('b1', expect.objectContaining({
    organizationId: 'org', name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 50000 }, includedSeconds: 3600
  })));
});

it('updates a package in edit mode', async () => {
  const c = client();
  const initial: PackageRow = {
    packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB', price: 500,
    includedMinutes: 60, bonusMinutes: 0, expiresAfterDays: 30
  };
  renderDialog({ mode: 'edit', client: c, initial });
  fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '600' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(c.updatePackageDefinition).toHaveBeenCalledWith('b1', 'pk1', expect.objectContaining({
    name: 'Старт', price: { currencyCode: 'RUB', minorUnits: 60000 }, includedSeconds: 3600, expiresAfterDays: 30, isActive: true
  })));
});
