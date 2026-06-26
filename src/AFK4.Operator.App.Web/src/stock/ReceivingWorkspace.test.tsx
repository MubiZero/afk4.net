import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true, stockOnHand: 12, reorderThreshold: 6, avgCostMinorUnits: 400, price: { currencyCode: 'TJS', minorUnits: 1000 } },
  { productId: 'p2', name: 'Чипсы Lays', sku: 'CHIPS-LAYS', trackStock: true, stockOnHand: 3, reorderThreshold: 5, avgCostMinorUnits: 600, price: { currencyCode: 'TJS', minorUnits: 1200 } },
  { productId: 'p3', name: 'Время-услуга', sku: 'TIME', trackStock: false, stockOnHand: 0, reorderThreshold: 0, avgCostMinorUnits: 0, price: { currencyCode: 'TJS', minorUnits: 0 } },
]));
const createStockMovement = mock(async () => ({ stockMovementId: 'm1' }));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: { createStockMovement } }) }));

const { ReceivingWorkspace } = await import('./ReceivingWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const session = { permissions: ['inventory.view', 'inventory.stock.manage'], organizationId: 'o' } as never;

const view = (props: Record<string, unknown> = {}) =>
  render(<I18nProvider initialLocale="ru"><ReceivingWorkspace backend={backend} currencyCode="TJS" session={session} preload={null} onConsumePreload={() => {}} {...props} /></I18nProvider>);

afterEach(() => { createStockMovement.mockClear(); getCatalog.mockClear(); cleanup(); });
afterAll(() => mock.restore());

describe('ReceivingWorkspace', () => {
  it('поиск добавляет товар строкой с преподставленной себестоимостью (avgCost)', async () => {
    view();
    fireEvent.change(await screen.findByLabelText('Добавить товар'), { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    // строка появилась; себестоимость преподставлена 4.00 (400 minor)
    const lines = await screen.findByLabelText('Позиции прихода');
    expect(within(lines).getByText('Cola 0.5')).toBeInTheDocument();
    const costInput = within(lines).getByLabelText('Себестоимость ед.') as HTMLInputElement;
    expect(costInput.value).toBe('4.00');
  });

  it('товары без учёта остатка (trackStock=false) в поиск не попадают', async () => {
    view();
    fireEvent.change(await screen.findByLabelText('Добавить товар'), { target: { value: 'врем' } });
    expect(await screen.findByText('Товары не найдены')).toBeInTheDocument();
  });

  it('повторное добавление того же товара → +1 к количеству, не новая строка', async () => {
    view();
    const input = await screen.findByLabelText('Добавить товар');
    fireEvent.change(input, { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    fireEvent.change(input, { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    const lines = screen.getByLabelText('Позиции прихода');
    const qty = within(lines).getByLabelText('Кол-во') as HTMLInputElement;
    expect(qty.value).toBe('2');
  });

  it('preload добавляет товар при загрузке и зовёт onConsumePreload', async () => {
    const onConsumePreload = mock(() => {});
    view({ preload: { productId: 'p2' }, onConsumePreload });
    expect(await screen.findByText('Чипсы Lays')).toBeInTheDocument();
    expect(onConsumePreload).toHaveBeenCalled();
  });

  it('«Провести» шлёт по purchase-движению на строку и очищает накладную', async () => {
    view();
    fireEvent.change(await screen.findByLabelText('Добавить товар'), { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Провести приёмку' }));
    await waitFor(() => expect(createStockMovement).toHaveBeenCalledTimes(1));
    const [, req] = createStockMovement.mock.calls[0];
    expect(req).toMatchObject({ productId: 'p1', movementType: 'purchase', quantityDelta: 1 });
    expect(req.unitCost).toMatchObject({ currencyCode: 'TJS', minorUnits: 400 });
    // успех: накладная очищена
    expect(await screen.findByText('Накладная пуста — добавьте товары сверху')).toBeInTheDocument();
  });

  it('частичный сбой оставляет непроведённые строки', async () => {
    // p1 успех, p2 — падение
    createStockMovement.mockImplementation(async (_branch: unknown, req: { productId: string }) => {
      if (req.productId === 'p2') throw new Error('boom');
      return { stockMovementId: 'ok' };
    });
    view();
    const input = await screen.findByLabelText('Добавить товар');
    fireEvent.change(input, { target: { value: 'cola' } });
    fireEvent.click(await screen.findByRole('button', { name: /Cola 0\.5/ }));
    fireEvent.change(input, { target: { value: 'чипсы' } });
    fireEvent.click(await screen.findByRole('button', { name: /Чипсы Lays/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Провести приёмку' }));
    // p1 ушёл, p2 остался строкой; показано предупреждение
    await waitFor(() => expect(screen.getByText(/Проведено 1 из 2/)).toBeInTheDocument());
    const lines = screen.getByLabelText('Позиции прихода');
    expect(within(lines).queryByText('Cola 0.5')).not.toBeInTheDocument();
    expect(within(lines).getByText('Чипсы Lays')).toBeInTheDocument();
  });

  it('без права управления — экран отказа', () => {
    render(<I18nProvider initialLocale="ru"><ReceivingWorkspace backend={backend} currencyCode="TJS" session={{ permissions: ['inventory.view'], organizationId: 'o' } as never} preload={null} onConsumePreload={() => {}} /></I18nProvider>);
    expect(screen.getByText('Недостаточно прав для приёмки')).toBeInTheDocument();
  });

  it('каталог без учётных товаров → пустое состояние «нет товаров с учётом остатка»', async () => {
    getCatalog.mockImplementationOnce(async () => ([
      { productId: 'x1', name: 'Время-услуга', sku: 'TIME', trackStock: false, stockOnHand: 0, reorderThreshold: 0, avgCostMinorUnits: 0, price: { currencyCode: 'TJS', minorUnits: 0 } },
    ]));
    view();
    expect(await screen.findByText('Нет товаров с учётом остатка')).toBeInTheDocument();
  });
});
