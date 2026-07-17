import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import { permissionNames } from '../../operatorPermissions';
import type { PosProductDto } from '../../operatorApiClients';

const createProductCategory = mock(async () => ({ categoryId: 'cat-new' }));
const createProduct = mock(async () => ({
  productId: 'p-new',
  categoryId: 'cat-new',
  name: 'Chips',
  sku: 'CHIPS-01',
  price: { currencyCode: 'TJS', minorUnits: 500 },
  trackStock: true,
  isActive: true
}));
const updateProduct = mock(async () => ({ productId: 'p1' }));
const getProductBarcodes = mock(async () => []);

const actualHelpers = await import('../../operatorHelpers');
mock.module('../../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: { createProductCategory, createProduct, updateProduct, getProductBarcodes }
  })
}));

const { GoodsDestination } = await import('./GoodsDestination');

afterAll(() => {
  mock.module('../../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  createProductCategory.mockClear();
  createProduct.mockClear();
  updateProduct.mockClear();
  getProductBarcodes.mockClear();
});

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1', displayName: 'x' }) as never;

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'o1', permissions: [permissionNames.managePosCatalog] },
  branchId: 'b1'
} as never;

const productId = '11111111-1111-1111-1111-111111111111';

const cola: PosProductDto = {
  productId,
  categoryId: 'cat1',
  name: 'Cola 0.5',
  sku: 'COLA-05',
  price: { currencyCode: 'TJS', minorUnits: 1000 },
  trackStock: true,
  allowNegativeStock: false,
  availableInShell: false,
  isActive: true,
  stockOnHand: 5
} as never;

const manyProducts: PosProductDto[] = Array.from({ length: 12 }, (_, index) => ({
  productId: `p${index + 1}`,
  categoryId: 'cat1',
  name: `Product ${index + 1}`,
  sku: `SKU-${index + 1}`,
  price: { currencyCode: 'TJS', minorUnits: 100 },
  trackStock: true,
  isActive: true,
  stockOnHand: 1
} as never));

describe('GoodsDestination', () => {
  it('renders the ManagementScreen title and subtitle', () => {
    const { container } = wrap(
      <GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={[]} />
    );

    expect(screen.getByRole('heading', { name: 'Товары' })).toBeTruthy();
    // Subtitle describes the whole section (catalog + prices + barcodes), deliberately distinct
    // from the «Каталог товаров» POS-section title inside the body.
    expect(container.querySelector('.management-screen-head')?.textContent).toContain('Каталог, цены и штрихкоды');
  });

  it('renders the catalog passed in from ManagementWorkspace state', () => {
    wrap(<GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={[cola]} />);
    expect(screen.getByText('Cola 0.5')).toBeTruthy();
  });

  it('does not render a "Категория" column — PosProductDto carries no category name and there is no GET endpoint to look one up', () => {
    wrap(<GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={[cola]} />);
    expect(screen.queryByText('Категория')).toBeNull();
  });

  it('renders the whole catalog beyond 8 items — the old settings-section cap is gone', () => {
    wrap(<GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={manyProducts} />);
    for (const product of manyProducts) {
      expect(screen.getByText(product.name)).toBeTruthy();
    }
  });

  it('does not render the stock-movement control — that lives in Склад now', () => {
    wrap(<GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={[cola]} />);
    expect(screen.queryByRole('button', { name: 'Записать движение' })).toBeNull();
  });

  it('renders the price via the neutral Money primitive, not amber', () => {
    const { container } = wrap(<GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={[cola]} />);
    const priceEl = container.querySelector('.ui-money');
    expect(priceEl).toBeTruthy();
    expect(priceEl!.textContent).toContain('10');
    expect(priceEl!.className).toBe('ui-money');
  });

  it('calls onDirtyChange(false) on mount since the section saves per-action', () => {
    const onDirtyChange = mock(() => {});
    wrap(<GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={[]} onDirtyChange={onDirtyChange} />);
    expect(onDirtyChange).toHaveBeenCalledWith(false);
  });

  it('shows a loading skeleton instead of the catalog while loadStatus is loading', () => {
    const { container } = wrap(
      <GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={[cola]} loadStatus="loading" />
    );
    expect(container.querySelector('.management-skeleton')).toBeTruthy();
    expect(screen.queryByText('Cola 0.5')).toBeNull();
  });

  it('shows the concrete error detail and retries via onRetry when loadStatus is failed', () => {
    const onRetry = mock(() => {});
    wrap(
      <GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={[cola]} loadStatus="failed" errorDetail="boom" onRetry={onRetry} />
    );
    expect(screen.getByText('boom')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('clicking a row opens the drawer with the product form and the embedded barcodes widget', async () => {
    wrap(<GoodsDestination backend={backend} session={session([permissionNames.managePosCatalog])} currencyCode="TJS" catalog={[cola]} />);
    fireEvent.click(screen.getByText('Cola 0.5'));

    expect(screen.getByRole('textbox', { name: 'Товар' })).toHaveValue('Cola 0.5');
    expect(screen.getByRole('textbox', { name: 'Артикул' })).toHaveValue('COLA-05');
    await waitFor(() => expect(getProductBarcodes).toHaveBeenCalledWith('b1', productId));
    expect(await screen.findByText('Штрих-коды не привязаны')).toBeTruthy();
  });

  it('hides the "+ Товар" primary action and row menu without canManagePosCatalog', () => {
    wrap(<GoodsDestination backend={null} session={session([])} currencyCode="TJS" catalog={[cola]} />);
    expect(screen.queryByRole('button', { name: '+ Товар' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Действия' })).toBeNull();
  });

  it('shows a create-first-product empty state when there are no products', () => {
    wrap(<GoodsDestination backend={backend} session={session([permissionNames.managePosCatalog])} currencyCode="TJS" catalog={[]} />);
    expect(screen.getByText('Нет товаров')).toBeTruthy();
  });

  it('"+ Товар" opens the create modal and submits createProductCategory then createProduct', async () => {
    const onCatalogChange = mock(() => {});
    wrap(
      <GoodsDestination
        backend={backend}
        session={session([permissionNames.managePosCatalog])}
        currencyCode="TJS"
        catalog={[cola]}
        onCatalogChange={onCatalogChange}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: '+ Товар' }));
    expect(screen.getByRole('dialog', { name: 'Новый товар' })).toBeTruthy();

    fireEvent.change(screen.getByRole('textbox', { name: 'Категория' }), { target: { value: 'Снеки' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Товар' }), { target: { value: 'Chips' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Артикул' }), { target: { value: 'CHIPS-01' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Цена' }), { target: { value: '5.00' } });

    fireEvent.click(screen.getByRole('button', { name: 'Создать товар' }));

    await waitFor(() => expect(createProductCategory).toHaveBeenCalledTimes(1));
    expect(createProductCategory).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'o1', name: 'Снеки' }));
    await waitFor(() => expect(createProduct).toHaveBeenCalledTimes(1));
    expect(createProduct).toHaveBeenCalledWith('b1', expect.objectContaining({
      organizationId: 'o1',
      categoryId: 'cat-new',
      name: 'Chips',
      sku: 'CHIPS-01',
      price: { currencyCode: 'TJS', minorUnits: 500 }
    }));
    await waitFor(() => expect(onCatalogChange).toHaveBeenCalledWith([cola, expect.objectContaining({ productId: 'p-new' })]));
  });

  it('delisting a product via the row menu asks for confirmation before calling updateProduct with isActive:false', async () => {
    const onReload = mock(async () => {});
    const { container } = wrap(
      <GoodsDestination
        backend={backend}
        session={session([permissionNames.managePosCatalog])}
        currencyCode="TJS"
        catalog={[cola]}
        onReload={onReload}
      />
    );
    fireEvent.click(within(container).getAllByRole('button', { name: 'Действия' })[0]);
    fireEvent.click(screen.getByRole('menuitem', { name: 'Снять с продажи' }));

    expect(updateProduct).not.toHaveBeenCalled();
    expect(screen.getByRole('alertdialog')).toBeTruthy();

    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Снять с продажи' }));
    await waitFor(() => expect(updateProduct).toHaveBeenCalledWith('b1', productId, expect.objectContaining({ isActive: false, name: 'Cola 0.5', sku: 'COLA-05' })));
    await waitFor(() => expect(onReload).toHaveBeenCalled());
  });

  it('an active product shows "Снять с продажи" but not "Вернуть в продажу" in the row menu', () => {
    const { container } = wrap(
      <GoodsDestination backend={backend} session={session([permissionNames.managePosCatalog])} currencyCode="TJS" catalog={[cola]} />
    );
    fireEvent.click(within(container).getAllByRole('button', { name: 'Действия' })[0]);
    expect(screen.getByRole('menuitem', { name: 'Снять с продажи' })).toBeTruthy();
    expect(screen.queryByRole('menuitem', { name: 'Вернуть в продажу' })).toBeNull();
  });

  it('a delisted product shows "Вернуть в продажу" in the row menu and re-lists it without confirmation', async () => {
    const delisted: PosProductDto = { ...cola, isActive: false } as never;
    const onReload = mock(async () => {});
    const { container } = wrap(
      <GoodsDestination
        backend={backend}
        session={session([permissionNames.managePosCatalog])}
        currencyCode="TJS"
        catalog={[delisted]}
        onReload={onReload}
      />
    );
    fireEvent.click(within(container).getAllByRole('button', { name: 'Действия' })[0]);
    expect(screen.queryByRole('menuitem', { name: 'Снять с продажи' })).toBeNull();
    fireEvent.click(screen.getByRole('menuitem', { name: 'Вернуть в продажу' }));

    expect(screen.queryByRole('alertdialog')).toBeNull();
    await waitFor(() => expect(updateProduct).toHaveBeenCalledWith('b1', productId, expect.objectContaining({
      isActive: true,
      categoryId: 'cat1',
      name: 'Cola 0.5',
      sku: 'COLA-05',
      price: { currencyCode: 'TJS', minorUnits: 1000 }
    })));
    await waitFor(() => expect(onReload).toHaveBeenCalled());
  });

  it('saving an edit from the drawer keeps the product active and calls updateProduct', async () => {
    const onReload = mock(async () => {});
    wrap(
      <GoodsDestination
        backend={backend}
        session={session([permissionNames.managePosCatalog])}
        currencyCode="TJS"
        catalog={[cola]}
        onReload={onReload}
      />
    );
    fireEvent.click(screen.getByText('Cola 0.5'));
    fireEvent.change(screen.getByRole('textbox', { name: 'Цена' }), { target: { value: '12.00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateProduct).toHaveBeenCalledWith('b1', productId, expect.objectContaining({
      categoryId: 'cat1',
      price: { currencyCode: 'TJS', minorUnits: 1200 },
      isActive: true
    })));
    await waitFor(() => expect(onReload).toHaveBeenCalled());
  });
});
