import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

const getCatalog = mock(async () => ([
  { productId: 'p1', name: 'Энергетик Red Bull', sku: 'ENERGY-RB', trackStock: true, stockOnHand: 8, reorderThreshold: 10, avgCostMinorUnits: 900, price: { currencyCode: 'TJS', minorUnits: 1800 } },
  { productId: 'p2', name: 'Cola 0.5', sku: 'COLA-05', trackStock: true, stockOnHand: 12, reorderThreshold: 6, avgCostMinorUnits: 400, price: { currencyCode: 'TJS', minorUnits: 1000 } },
  { productId: 'p3', name: 'Вода 0.5', sku: 'WATER-05', trackStock: true, stockOnHand: 0, reorderThreshold: 5, avgCostMinorUnits: 100, price: { currencyCode: 'TJS', minorUnits: 300 } },
]));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({ ...actual, createAuthenticatedOperatorClients: () => ({ pos: { getCatalog }, inventory: {} }) }));

const { StockLevelsWorkspace } = await import('./StockLevelsWorkspace');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o' }, branchId: 'b' } as never;
const session = { permissions: ['organization.inventory.view'], organizationId: 'o' } as never;
const view = () => render(<I18nProvider initialLocale="ru"><StockLevelsWorkspace backend={backend} currencyCode="TJS" session={session} /></I18nProvider>);

afterEach(() => cleanup());
afterAll(() => mock.restore());

describe('StockLevelsWorkspace', () => {
  it('показывает товары и помечает «на исходе» по per-product порогу', async () => {
    const { container } = view();
    // дождёмся загрузки
    await screen.findByText('Cola 0.5');
    // Red Bull 8 при пороге 10 → low; Cola 12 при пороге 6 → ok; Вода 0 → out
    // Тег-бейдж только у «На исходе» (low) — «Нет в наличии» (out) читается по левой
    // полосе/иконке/цвету остатка в строке, отдельного бейджа у него больше нет.
    expect(container.querySelectorAll('.ui-chip--status.is-warning')).toHaveLength(1);
    expect(container.querySelectorAll('.ui-chip--status.is-danger')).toHaveLength(0);
    // фильтр-кнопка тоже видна как реальный текст в DOM
    expect(screen.getByRole('button', { name: /на исходе/i })).toBeInTheDocument();
  });

  it('два героя сводки: «Стоимость склада» нейтральный, «Нужно дозаказать» тонирован по худшему статусу', async () => {
    const { container } = view();
    await screen.findByText('Cola 0.5');
    const heroes = container.querySelectorAll('.stock-hero');
    expect(heroes).toHaveLength(2);
    expect(heroes[0]).toHaveClass('stock-hero--neutral');
    expect(within(heroes[0] as HTMLElement).getByText('Стоимость склада')).toBeInTheDocument();
    // Red Bull (low, 8/10) + Вода (out, 0) → худший статус out → tone attention, счётчик = 2
    expect(heroes[1]).toHaveClass('stock-hero--attention');
    expect(within(heroes[1] as HTMLElement).getByText('2')).toBeInTheDocument();
  });

  it('фильтр «На исходе» оставляет low И out, скрывает ok', async () => {
    const { container } = view();
    await screen.findByText('Cola 0.5');
    // кнопка теперь содержит реальный текст «На исходе · N»
    fireEvent.click(screen.getByRole('button', { name: /на исходе/i }));
    // ok-товар скрыт
    expect(screen.queryByText('Cola 0.5')).not.toBeInTheDocument();
    // low и out — видны в списке
    const list = container.querySelector('.cash-stock-list') as HTMLElement;
    expect(within(list).getAllByText('Энергетик Red Bull').length).toBeGreaterThan(0);
    expect(within(list).getAllByText('Вода 0.5').length).toBeGreaterThan(0);
  });

  it('фильтр «Нет» оставляет только out', async () => {
    const { container } = view();
    await screen.findByText('Cola 0.5');
    fireEvent.click(screen.getByRole('button', { name: /^нет/i }));
    expect(screen.queryByText('Cola 0.5')).not.toBeInTheDocument();
    // Вода 0 → out, должна быть в списке
    const list = container.querySelector('.cash-stock-list') as HTMLElement;
    expect(within(list).getAllByText('Вода 0.5').length).toBeGreaterThan(0);
    // Red Bull low → НЕ должен быть в списке при фильтре 'out'
    expect(within(list).queryAllByText('Энергетик Red Bull').length).toBe(0);
  });

  it('кнопка ＋ на строке и «Оформить приёмку» зовут onReceive', async () => {
    const onReceive = mock((_id?: string) => {});
    render(<I18nProvider initialLocale="ru"><StockLevelsWorkspace backend={backend} currencyCode="TJS" session={session} onReceive={onReceive} /></I18nProvider>);
    await screen.findByText('Cola 0.5');
    // ＋ на первой строке (aria-label «Приёмка товара»)
    fireEvent.click(screen.getAllByRole('button', { name: 'Приёмка товара' })[0]);
    expect(onReceive).toHaveBeenCalledWith(expect.any(String));
    // «Оформить приёмку» (есть товары «на исходе» → блок виден)
    fireEvent.click(screen.getByRole('button', { name: 'Оформить приёмку' }));
    expect(onReceive).toHaveBeenCalledWith();
  });
});
