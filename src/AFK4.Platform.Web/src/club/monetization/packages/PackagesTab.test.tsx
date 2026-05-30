import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PackageOption } from '@/api/types';
import { PackagesTab } from './PackagesTab';

const option: PackageOption = {
  packageDefinitionId: 'pk1', name: 'Старт', currencyCode: 'RUB', priceMinorUnits: 50000,
  includedSeconds: 3600, bonusSeconds: 600, expiresAfterDays: 30
};

function fakeClient() {
  return {
    getPackageOptions: vi.fn(async () => [option]),
    createPackageDefinition: vi.fn(async () => ({ packageDefinitionId: 'pk2' })),
    updatePackageDefinition: vi.fn(async () => ({ packageDefinitionId: 'pk1' }))
  };
}

function renderTab(canManage: boolean) {
  render(
    <I18nProvider><ToastProvider>
      <PackagesTab client={fakeClient() as never} branchId="b1" organizationId="org" canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
}

it('renders package rows', async () => {
  renderTab(true);
  expect(await screen.findByText('Старт')).toBeInTheDocument();
});

it('opens the create dialog when managing', async () => {
  renderTab(true);
  await screen.findByText('Старт');
  fireEvent.click(screen.getByRole('button', { name: 'Создать пакет' }));
  expect(await screen.findByRole('button', { name: 'Создать' })).toBeInTheDocument();
});

it('hides the create trigger when read-only', async () => {
  renderTab(false);
  await screen.findByText('Старт');
  expect(screen.queryByRole('button', { name: 'Создать пакет' })).not.toBeInTheDocument();
});
