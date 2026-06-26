import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';

// bun's mock.module is not hoisted above static imports — register before importing the component.
// Мокаем createAuthenticatedOperatorClients, чтобы отдать каталог с barcodes без сети.
const getCatalog = mock(async () => ([
  {
    productId: 'p1',
    name: 'Cola',
    sku: 'SKU-1',
    categoryName: 'Напитки',
    stockOnHand: 10,
    barcodes: ['111'],
    price: { currencyCode: 'TJS', minorUnits: 1200 }
  },
  {
    productId: 'p2',
    name: 'Water',
    sku: 'SKU-2',
    categoryName: 'Напитки',
    stockOnHand: 5,
    barcodes: ['222'],
    price: { currencyCode: 'TJS', minorUnits: 600 }
  }
]));
const getCurrentShift = mock(async () => ({ shiftId: 'shift-1' }));

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    pos: { getCatalog, createSale: mock(async () => ({})), paySaleManual: mock(async () => ({})) },
    shifts: { getCurrentShift },
    players: { searchPlayers: mock(async () => []) }
  })
}));

const { BackendPosWorkspace } = await import('./BackendPosWorkspace');

// Restore the real helpers after this file finishes to avoid leaking into App.test.tsx.
afterAll(() => {
  mock.module('./operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('./operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [] },
  branchId: 'b1'
};

function renderPos() {
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <BackendPosWorkspace currencyCode="TJS" backend={backend as never} embedded />
      </ToastProvider>
    </I18nProvider>
  );
}

// Отправляем символы быстро (gap≈0 < 50ms) — feedScanner детектирует как сканер.
// act() оборачивает диспатч, чтобы React обработал все setState-обновления синхронно.
function scan(code: string) {
  act(() => {
    for (const ch of code) {
      window.dispatchEvent(new KeyboardEvent('keydown', { key: ch, bubbles: true }));
    }
    window.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
  });
}

describe('BackendPosWorkspace — barcode scanner', () => {
  afterEach(() => {
    cleanup();
    mock.restore();
    getCatalog.mockClear();
  });

  it('бейдж «Сканер активен» отображается в метриках каталога', async () => {
    renderPos();
    await waitFor(() => expect(getCatalog).toHaveBeenCalled());
    expect(screen.getByLabelText('Сканер активен')).toBeInTheDocument();
  });

  it('скан известного штриха кладёт товар в чек', async () => {
    renderPos();
    await waitFor(() => expect(getCatalog).toHaveBeenCalled());
    // Ждём рендер каталога (товар в каталоге)
    await waitFor(() => expect(screen.getAllByText('Cola').length).toBeGreaterThan(0));

    scan('111');

    // Тост подтверждения
    await waitFor(() => screen.getByText('Cola — в чек'));
    // Cola присутствует как в каталоге, так и в корзине
    expect(screen.getAllByText('Cola').length).toBeGreaterThanOrEqual(2);
  });

  it('скан неизвестного штриха показывает тост «Штрих-код не привязан»', async () => {
    renderPos();
    // Ждём загрузки каталога (2 товара в каталоге)
    await waitFor(() => expect(getCatalog).toHaveBeenCalled());
    await waitFor(() => expect(screen.getAllByText('Cola').length).toBeGreaterThan(0));

    scan('999');

    await waitFor(() => screen.getByText('Штрих-код не привязан'));
  });

  it('скан одного штриха дважды увеличивает quantity до 2', async () => {
    renderPos();
    await waitFor(() => expect(getCatalog).toHaveBeenCalled());
    // Ждём каталога: Cola должна быть в каталоге
    await waitFor(() => expect(screen.getAllByText('Cola').length).toBeGreaterThan(0));

    // После загрузки каталога Cola уже в корзине с qty=1 (loadBackendPos инициализирует первым товаром).
    // Первый скан → qty=2
    scan('111');
    await waitFor(() => screen.getByText('2 шт.'));

    // Второй скан → qty=3
    scan('111');
    await waitFor(() => screen.getByText('3 шт.'));
  });
});
