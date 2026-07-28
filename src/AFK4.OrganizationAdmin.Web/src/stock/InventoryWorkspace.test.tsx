import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { act, render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true, stockOnHand: 12, avgCostMinorUnits: 400, barcodes: ['111'], price: { currencyCode: 'TJS', minorUnits: 1000 } },
  { productId: 'p2', name: 'Вода 0.5', sku: 'WATER-05', trackStock: true, stockOnHand: 30, avgCostMinorUnits: 200, barcodes: ['222'], price: { currencyCode: 'TJS', minorUnits: 600 } },
  { productId: 'p3', name: 'Время-услуга', sku: 'TIME', trackStock: false, stockOnHand: 0, avgCostMinorUnits: 0, barcodes: [], price: { currencyCode: 'TJS', minorUnits: 0 } },
]));
const createStockMovement = mock(async (_branchId: string, _request: Record<string, unknown>) => ({ stockMovementId: 'm1' }));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { createStockMovement } }) }));

const { InventoryWorkspace } = await import('./InventoryWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const manageSession = { permissions: ['organization.inventory.view', 'organization.inventory.stock.manage'], organizationId: 'o' } as never;

const view = (props: Record<string, unknown> = {}) =>
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <InventoryWorkspace backend={backend} currencyCode="TJS" session={manageSession} {...props} />
      </ToastProvider>
    </I18nProvider>
  );

function scan(code: string) {
  act(() => {
    for (const ch of code) window.dispatchEvent(new KeyboardEvent('keydown', { key: ch, bubbles: true }));
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  });
}

const factInput = (name: string) => screen.getByLabelText(new RegExp(`Факт по полке: ${name}`)) as HTMLInputElement;

afterEach(() => { createStockMovement.mockClear(); getCatalog.mockClear(); cleanup(); });
afterAll(() => mock.restore());

describe('InventoryWorkspace', () => {
  it('без права управления — экран отказа', () => {
    render(<I18nProvider initialLocale="ru"><ToastProvider><InventoryWorkspace backend={backend} currencyCode="TJS" session={{ permissions: ['organization.inventory.view'], organizationId: 'o' } as never} /></ToastProvider></I18nProvider>);
    expect(screen.getByText('Недостаточно прав для инвентаризации')).toBeInTheDocument();
  });

  it('рендерит отслеживаемые товары строками с учётным остатком; неотслеживаемые скрыты', async () => {
    view();
    expect(await screen.findByText('Cola 0.5')).toBeInTheDocument();
    expect(screen.getByText('Вода 0.5')).toBeInTheDocument();
    expect(screen.queryByText('Время-услуга')).not.toBeInTheDocument();
  });

  it('ввод факта считает расхождение; кнопка «Провести» включается', async () => {
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.change(factInput('Cola 0.5'), { target: { value: '10' } });
    expect(await screen.findByText('-2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Провести инвентаризацию' })).not.toBeDisabled();
  });

  it('«Провести» шлёт adjustment на каждое расхождение со знаковой дельтой и себест из avg', async () => {
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.change(factInput('Cola 0.5'), { target: { value: '10' } });   // -2
    fireEvent.change(factInput('Вода 0.5'), { target: { value: '33' } });   // +3
    fireEvent.click(screen.getByRole('button', { name: 'Провести инвентаризацию' }));
    await waitFor(() => expect(createStockMovement).toHaveBeenCalledTimes(2));
    const reqs = createStockMovement.mock.calls.map((c) => c[1]);
    const cola = reqs.find((r) => r.productId === 'p1')!;
    expect(cola).toMatchObject({ movementType: 'adjustment', quantityDelta: -2 });
    expect(cola.unitCost).toMatchObject({ currencyCode: 'TJS', minorUnits: 400 });
    expect(typeof cola.reason).toBe('string');
    expect((cola.reason as string).length).toBeGreaterThan(0);
    const water = reqs.find((r) => r.productId === 'p2')!;
    expect(water).toMatchObject({ movementType: 'adjustment', quantityDelta: 3, reason: 'Инвентаризация' });
  });

  it('строки без факта и с нулевым расхождением не проводятся', async () => {
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.change(factInput('Cola 0.5'), { target: { value: '12' } }); // совпало → пропуск
    // Вода не пересчитана → пропуск
    expect(screen.getByRole('button', { name: 'Провести инвентаризацию' })).toBeDisabled();
  });

  it('частичный сбой: «Проведено X из Y», ретрай проводит только оставшееся', async () => {
    createStockMovement.mockImplementation(async (_b: string, request: Record<string, unknown>) => {
      if (request.productId === 'p2') throw new Error('boom');
      return { stockMovementId: 'ok' };
    });
    view();
    await screen.findByText('Cola 0.5');
    fireEvent.change(factInput('Cola 0.5'), { target: { value: '10' } }); // -2 (успех)
    fireEvent.change(factInput('Вода 0.5'), { target: { value: '28' } }); // -2 (упадёт)
    fireEvent.click(screen.getByRole('button', { name: 'Провести инвентаризацию' }));
    await waitFor(() => expect(screen.getByText(/Проведено 1 из 2/)).toBeInTheDocument());
    // Cola теперь сошлась (учётный := факт), повторный прогон шлёт только Воду
    createStockMovement.mockImplementation(async () => ({ stockMovementId: 'ok2' }));
    createStockMovement.mockClear();
    fireEvent.click(screen.getByRole('button', { name: 'Провести инвентаризацию' }));
    await waitFor(() => expect(createStockMovement).toHaveBeenCalledTimes(1));
    expect(createStockMovement.mock.calls[0][1]).toMatchObject({ productId: 'p2' });
  });

  it('скан известного штриха фокусирует поле факта своей строки', async () => {
    view();
    await screen.findByText('Cola 0.5');
    await waitFor(() => expect(getCatalog).toHaveBeenCalled());
    scan('222'); // Вода
    await waitFor(() => expect(document.activeElement).toBe(factInput('Вода 0.5')));
  });

  it('скан неизвестного штриха показывает тост', async () => {
    view();
    await screen.findByText('Cola 0.5');
    await waitFor(() => expect(getCatalog).toHaveBeenCalled());
    scan('999');
    await waitFor(() => expect(screen.getByText('Штрих-код не привязан')).toBeInTheDocument());
  });

  it('каталог без учётных товаров → пустое состояние', async () => {
    getCatalog.mockImplementationOnce(async () => ([
      { productId: 'x', name: 'Время', sku: 'TIME', trackStock: false, stockOnHand: 0, avgCostMinorUnits: 0, barcodes: [], price: { currencyCode: 'TJS', minorUnits: 0 } },
    ]));
    view();
    expect(await screen.findByText('Нет товаров с учётом остатка')).toBeInTheDocument();
  });
});
