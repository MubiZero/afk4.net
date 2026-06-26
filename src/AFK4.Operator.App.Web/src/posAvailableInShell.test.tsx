import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';

// bun's mock.module is not hoisted above static imports, so register it before
// importing the component under test. The POS product editor lives in
// BackendSettingsWorkspace (Goods section); it builds clients via
// createAuthenticatedOperatorClients from ./operatorHelpers.
const createProductCategory = mock(async () => ({ categoryId: 'cat1' }));
const createProduct = mock(async () => ({ productId: 'p2', name: 'Item 1', availableInShell: true }));
const updateProduct = mock(async () => ({ productId: 'p1' }));
const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Existing', sku: 'SKU-001', price: { currencyCode: 'TJS', minorUnits: 1200 }, categoryId: 'cat1', trackStock: true, allowNegativeStock: false, availableInShell: true }
]));
const empty = mock(async () => []);
const obj = mock(async () => ({}));

const settingsClient = {
  getBranchProfile: obj,
  getStaffUsers: empty,
  getLayoutZones: empty,
  getTariffOptions: empty,
  getPackageOptions: empty,
  getProductBarcodes: empty,
  createProductCategory,
  createProduct,
  updateProduct
};

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: settingsClient,
    pos: { getCatalog },
    inventory: { getStockMovements: empty },
    diagnostics: { getDiagnostics: obj },
    updates: { getRolloutStatuses: empty },
    devices: { listDevices: empty, listBranchDeviceCommands: empty }
  })
}));

const { BackendSettingsWorkspace } = await import('./BackendSettingsWorkspace');

// Restore the real module (snapshotted in the preload) once this file is done — bun keeps
// mock.module registrations for the rest of the run and mutates the shared namespace, which
// would otherwise break App.test.tsx and the operator helper tests.
afterAll(() => {
  mock.module('./operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('./operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: ['pos.catalog.manage'] },
  branchId: 'b1'
};

async function openGoods() {
  render(<I18nProvider><ToastProvider><BackendSettingsWorkspace currencyCode="TJS" backend={backend as never} /></ToastProvider></I18nProvider>);
  await waitFor(() => expect(getCatalog).toHaveBeenCalled());
  fireEvent.click(await screen.findByRole('button', { name: /товары и склад|products & stock/i }));
}

describe('POS product editor availableInShell', () => {
  afterEach(() => {
    cleanup();
    createProduct.mockClear();
    updateProduct.mockClear();
  });

  it('sends availableInShell when creating a product', async () => {
    await openGoods();
    fireEvent.click(await screen.findByLabelText(/доступно в шелле|available in shell/i));
    fireEvent.click(screen.getByRole('button', { name: /создать товар|create product/i }));
    await waitFor(() => expect(createProduct).toHaveBeenCalledWith('b1', expect.objectContaining({ availableInShell: true })));
    await waitFor(() => expect((screen.getByLabelText(/доступно в шелле|available in shell/i) as HTMLInputElement).checked).toBe(false));
  });

  it('initialises the checkbox from the product and sends it on update', async () => {
    await openGoods();
    // select the existing product (availableInShell: true) → checkbox should turn on
    fireEvent.click(await screen.findByRole('button', { name: /existing/i }));
    const checkbox = await screen.findByLabelText(/доступно в шелле|available in shell/i);
    await waitFor(() => expect((checkbox as HTMLInputElement).checked).toBe(true));
    fireEvent.click(screen.getByRole('button', { name: /обновить товар|update product/i }));
    await waitFor(() => expect(updateProduct).toHaveBeenCalledWith('b1', 'p1', expect.objectContaining({ availableInShell: true })));
  });
});
