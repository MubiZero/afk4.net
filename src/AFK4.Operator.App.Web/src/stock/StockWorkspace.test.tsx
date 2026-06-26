import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true, stockOnHand: 12, reorderThreshold: 6, avgCostMinorUnits: 400, price: { currencyCode: 'TJS', minorUnits: 1000 } },
]));
const createStockMovement = mock(async () => ({ stockMovementId: 'm1' }));
const getStockMovements = mock(async () => ([]));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { createStockMovement, getStockMovements } }) }));

const { StockWorkspace } = await import('./StockWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const manageSession = { permissions: ['inventory.view', 'inventory.stock.manage'], organizationId: 'o' } as never;
const viewOnlySession = { permissions: ['inventory.view'], organizationId: 'o' } as never;

const view = (session: unknown) =>
  render(<I18nProvider initialLocale="ru"><StockWorkspace backend={backend} currencyCode="TJS" session={session as never} /></I18nProvider>);

afterEach(() => cleanup());
afterAll(() => mock.restore());

describe('StockWorkspace — вкладки', () => {
  it('при праве на управление видны обе вкладки и можно переключиться на Приёмку', async () => {
    view(manageSession);
    expect(screen.getByRole('tab', { name: 'Остатки' })).toBeInTheDocument();
    const receivingTab = screen.getByRole('tab', { name: 'Приёмка' });
    fireEvent.click(receivingTab);
    expect(receivingTab).toHaveAttribute('aria-selected', 'true');
  });

  it('без права управления вкладка Приёмка скрыта (полоса вкладок не показывается)', () => {
    view(viewOnlySession);
    expect(screen.queryByRole('tab', { name: 'Приёмка' })).not.toBeInTheDocument();
  });

  it('вкладка «Журнал» видна и переключается', async () => {
    view(manageSession);
    const journalTab = screen.getByRole('tab', { name: 'Журнал' });
    fireEvent.click(journalTab);
    expect(journalTab).toHaveAttribute('aria-selected', 'true');
  });
});
