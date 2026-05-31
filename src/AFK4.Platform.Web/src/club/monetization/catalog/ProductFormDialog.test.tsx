import type { ComponentProps } from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { CategoryOption, ProductRow } from './catalogModel';
import { ProductFormDialog } from './ProductFormDialog';

type DialogProps = ComponentProps<typeof ProductFormDialog>;

const categories: CategoryOption[] = [{ categoryId: 'c1', name: 'Напитки' }];

function client(overrides: Record<string, unknown> = {}) {
  return {
    createProduct: mock(async () => ({ productId: 'p1' })),
    updateProduct: mock(async () => ({ productId: 'p1' })),
    ...overrides
  };
}

function renderDialog(props: Record<string, unknown>) {
  const merged = {
    open: true, branchId: 'b1', organizationId: 'org', categories,
    onOpenChange: () => {}, onDone: () => {},
    ...props
  } as unknown as DialogProps;
  render(<I18nProvider><ToastProvider><ProductFormDialog {...merged} /></ToastProvider></I18nProvider>);
}

it('creates a product with the default category and minor-unit price', async () => {
  const c = client();
  renderDialog({ mode: 'create', client: c });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Кола' } });
  fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '1.5' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(c.createProduct).toHaveBeenCalledWith('b1', expect.objectContaining({
    organizationId: 'org', categoryId: 'c1', name: 'Кола', price: { currencyCode: 'RUB', minorUnits: 150 }
  })));
});

it('updates a product in edit mode', async () => {
  const c = client();
  const initial: ProductRow = {
    productId: 'p1', categoryId: 'c1', name: 'Кола', sku: 'SKU1', price: 1.5, currencyCode: 'RUB',
    trackStock: false, allowNegativeStock: false, isActive: true, stockOnHand: 10
  };
  renderDialog({ mode: 'edit', client: c, initial });
  fireEvent.change(screen.getByLabelText('Цена'), { target: { value: '2' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(c.updateProduct).toHaveBeenCalledWith('b1', 'p1', expect.objectContaining({
    categoryId: 'c1', name: 'Кола', price: { currencyCode: 'RUB', minorUnits: 200 }, isActive: true
  })));
});
