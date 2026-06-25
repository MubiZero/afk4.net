import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA', trackStock: true, stockOnHand: 2 },
  { productId: 'p2', name: 'Вода', sku: 'WATER', trackStock: true, stockOnHand: 30 },
  { productId: 'p3', name: 'Гостевой час', sku: 'SVC', trackStock: false, stockOnHand: 0 }
]));
const createStockMovement = mock(async () => ({}));

const actualHelpers = await import('../operatorHelpers');
mock.module('../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { createStockMovement } })
}));

const { CashStockWorkspace } = await import('./CashStockWorkspace');

afterAll(() => {
  mock.module('../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

const backend = { config: { platformBaseUrl: 'http://test' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b1' };
const session = { permissions: ['inventory.stock.manage'], organizationId: 'o' };

function renderStock() {
  render(<I18nProvider initialLocale="ru"><CashStockWorkspace backend={backend as never} currencyCode="TJS" session={session as never} /></I18nProvider>);
}

describe('CashStockWorkspace', () => {
  afterEach(() => { cleanup(); mock.restore(); });

  it('показывает остатки (только trackStock), низкий — тегом', async () => {
    renderStock();
    expect(await screen.findByText('Cola 0.5')).toBeInTheDocument();
    expect(screen.getByText('Вода')).toBeInTheDocument();
    expect(screen.queryByText('Гостевой час')).toBeNull(); // не trackStock — не на складе
    expect(screen.getAllByText(/низкий остаток/i).length).toBeGreaterThan(0); // Cola stockOnHand=2
  });

  it('списание шлёт createStockMovement с отрицательной дельтой', async () => {
    renderStock();
    await screen.findByText('Cola 0.5');
    fireEvent.change(screen.getByLabelText('Количество списания'), { target: { value: '3' } });
    fireEvent.click(screen.getByRole('button', { name: 'Списать' }));
    await waitFor(() => expect(createStockMovement).toHaveBeenCalled());
    const body = (createStockMovement.mock.calls[0] as unknown[])?.[1];
    expect(body).toMatchObject({ productId: 'p1', movementType: 'adjustment', quantityDelta: -3 });
  });
});
