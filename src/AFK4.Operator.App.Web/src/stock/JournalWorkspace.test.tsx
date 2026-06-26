import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async (_branchId: string) => ([
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true },
  { productId: 'p2', name: 'Чипсы Lays', sku: 'CHIPS-LAYS', trackStock: true },
]));
const getStockMovements = mock(async (_branchId: string, _query?: unknown) => ([
  { stockMovementId: 'm1', productId: 'p1', movementType: 'purchase', quantityDelta: 12, unitCost: { currencyCode: 'TJS', minorUnits: 400 }, reason: 'Приёмка · Напитки', createdByStaffUserId: 's1', createdByDisplayName: 'Олег С.', createdAtUtc: '2026-06-25T10:05:00Z' },
  { stockMovementId: 'm2', productId: 'p2', movementType: 'adjustment', quantityDelta: -2, unitCost: { currencyCode: 'TJS', minorUnits: 600 }, reason: 'брак', createdByStaffUserId: 's1', createdByDisplayName: 'Олег С.', createdAtUtc: '2026-06-25T13:30:00Z' },
  { stockMovementId: 'm3', productId: 'p1', movementType: 'sale', quantityDelta: -1, unitCost: { currencyCode: 'TJS', minorUnits: 400 }, reason: 'чек #1042', createdByStaffUserId: 's1', createdByDisplayName: 'Олег С.', createdAtUtc: '2026-06-25T13:42:00Z' },
]));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { getStockMovements } }) }));

const { JournalWorkspace } = await import('./JournalWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const session = { permissions: ['inventory.view'], organizationId: 'o' } as never;
const view = () => render(<I18nProvider initialLocale="ru"><JournalWorkspace backend={backend} currencyCode="TJS" session={session} /></I18nProvider>);

afterEach(() => { getCatalog.mockClear(); getStockMovements.mockClear(); cleanup(); });
afterAll(() => mock.restore());

describe('JournalWorkspace', () => {
  it('показывает движения с резолвом имени товара, типом и автором', async () => {
    view();
    await screen.findAllByText('Cola 0.5');
    expect(screen.getByText('Чипсы Lays')).toBeInTheDocument();
    // автор движения
    expect(screen.getAllByText('Олег С.').length).toBeGreaterThan(0);
  });

  it('фильтр по типу «Продажа» оставляет только sale', async () => {
    view();
    await screen.findAllByText('Cola 0.5');
    fireEvent.click(screen.getByRole('button', { name: /Продажа/ }));
    const list = screen.getByLabelText('Движения склада');
    expect(within(list).queryByText('Чипсы Lays')).not.toBeInTheDocument(); // adjustment скрыт
    expect(within(list).getByText('Cola 0.5')).toBeInTheDocument(); // sale остался
  });

  it('без права — экран отказа', () => {
    render(<I18nProvider initialLocale="ru"><JournalWorkspace backend={backend} currencyCode="TJS" session={{ permissions: [], organizationId: 'o' } as never} /></I18nProvider>);
    expect(screen.getByText('Недостаточно прав для просмотра журнала')).toBeInTheDocument();
  });

  it('кнопка «Экспорт CSV» доступна при наличии движений', async () => {
    view();
    await screen.findAllByText('Cola 0.5');
    expect(screen.getByRole('button', { name: 'Экспорт CSV' })).toBeEnabled();
  });
});
