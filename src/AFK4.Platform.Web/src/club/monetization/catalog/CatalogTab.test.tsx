import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { PosProduct } from '@/api/types';
import { CatalogTab } from './CatalogTab';

const product: PosProduct = {
  productId: 'p1', organizationId: 'org', branchId: 'b1', categoryId: 'c1', name: 'Кола', sku: 'SKU1',
  price: { currencyCode: 'RUB', minorUnits: 150 }, trackStock: false, allowNegativeStock: false,
  isActive: true, stockOnHand: 10, createdAtUtc: '2026-01-01T00:00:00.000Z'
};

function fakeClient() {
  return {
    getCatalog: mock(async () => [product]),
    createProductCategory: mock(async () => ({ categoryId: 'c9', organizationId: 'org', branchId: 'b1', name: 'Снеки', isActive: true, createdAtUtc: '' })),
    createProduct: mock(async () => ({ productId: 'p2' })),
    updateProduct: mock(async () => ({ productId: 'p1' }))
  };
}

function renderTab(canManage: boolean) {
  render(
    <I18nProvider><ToastProvider>
      <CatalogTab client={fakeClient() as never} branchId="b1" organizationId="org" canManage={canManage} />
    </ToastProvider></I18nProvider>
  );
}

it('renders product rows', async () => {
  renderTab(true);
  expect(await screen.findByText('Кола')).toBeInTheDocument();
});

it('opens the create-product dialog when managing', async () => {
  renderTab(true);
  await screen.findByText('Кола');
  fireEvent.click(screen.getByRole('button', { name: 'Создать товар' }));
  expect(await screen.findByRole('button', { name: 'Создать' })).toBeInTheDocument();
});

it('hides the create triggers when read-only', async () => {
  renderTab(false);
  await screen.findByText('Кола');
  expect(screen.queryByRole('button', { name: 'Создать товар' })).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: 'Создать категорию' })).not.toBeInTheDocument();
});
