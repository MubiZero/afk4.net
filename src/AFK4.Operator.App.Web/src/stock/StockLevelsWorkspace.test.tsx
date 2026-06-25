import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Энергетик Red Bull', sku: 'ENERGY-RB', trackStock: true, stockOnHand: 8, reorderThreshold: 10, avgCostMinorUnits: 900, price: { currencyCode: 'TJS', minorUnits: 1800 } },
  { productId: 'p2', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true, stockOnHand: 12, reorderThreshold: 6, avgCostMinorUnits: 400, price: { currencyCode: 'TJS', minorUnits: 1000 } }
]));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: {} }) }));

const { StockLevelsWorkspace } = await import('./StockLevelsWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const session = { permissions: ['inventory.view'], organizationId: 'o' } as never;
const view = () => render(<I18nProvider initialLocale="ru"><StockLevelsWorkspace backend={backend} currencyCode="TJS" session={session} /></I18nProvider>);

afterEach(() => cleanup());
afterAll(() => mock.restore());

describe('StockLevelsWorkspace', () => {
  it('показывает товары и помечает «на исходе» по per-product порогу', async () => {
    view();
    expect(await screen.findByText('Энергетик Red Bull')).toBeInTheDocument();
    // Red Bull 8 при пороге 10 → low; Cola 12 при пороге 6 → ok
    const lowTags = await screen.findAllByText(/на исходе/i);
    expect(lowTags.length).toBe(1);
  });

  it('фильтр «На исходе» оставляет только low/out', async () => {
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.click(screen.getByRole('button', { name: /на исходе/i }));
    expect(screen.queryByText('Cola 0.5')).not.toBeInTheDocument();
    expect(screen.getByText('Энергетик Red Bull')).toBeInTheDocument();
  });
});
