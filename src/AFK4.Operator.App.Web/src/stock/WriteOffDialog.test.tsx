import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const createStockMovement = mock(async (_branchId: string, _request: Record<string, unknown>) => ({ stockMovementId: 'm1' }));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ inventory: { createStockMovement } }) }));

const { WriteOffDialog } = await import('./WriteOffDialog');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const item = { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', stockOnHand: 12, reorderThreshold: 6, priceMinorUnits: 1000, avgCostMinorUnits: 400, category: '' } as never;

const view = (onDone = () => {}, onClose = () => {}) =>
  render(<I18nProvider initialLocale="ru"><WriteOffDialog item={item} backend={backend} currencyCode="TJS" onClose={onClose} onDone={onDone} /></I18nProvider>);

afterEach(() => { createStockMovement.mockClear(); cleanup(); });
afterAll(() => mock.restore());

describe('WriteOffDialog', () => {
  it('шлёт adjustment с отрицательным кол-вом и себестоимостью из avgCost', async () => {
    const onDone = mock(() => {});
    view(onDone);
    fireEvent.change(screen.getByLabelText('Количество к списанию'), { target: { value: '3' } });
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'бой' } });
    fireEvent.click(screen.getByRole('button', { name: 'Списать' }));
    await waitFor(() => expect(createStockMovement).toHaveBeenCalledTimes(1));
    const [, req] = createStockMovement.mock.calls[0];
    expect(req).toMatchObject({ productId: 'p1', movementType: 'adjustment', quantityDelta: -3, reason: 'бой' });
    expect(req.unitCost).toMatchObject({ currencyCode: 'TJS', minorUnits: 400 });
    expect(onDone).toHaveBeenCalled();
  });

  it('не даёт списать больше остатка', () => {
    view();
    fireEvent.change(screen.getByLabelText('Количество к списанию'), { target: { value: '99' } });
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'бой' } });
    fireEvent.click(screen.getByRole('button', { name: 'Списать' }));
    expect(createStockMovement).not.toHaveBeenCalled();
    expect(screen.getByText('Количество должно быть от 1 до остатка')).toBeInTheDocument();
  });
});
