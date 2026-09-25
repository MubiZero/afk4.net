import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { ShopOrderDto } from '@afk4/contracts';
import { BarTab } from './BarTab';

const catalog = [
  { productId: 'cola', name: 'Кола 0,5 л', sku: 'C', price: { currencyCode: 'TJS', minorUnits: 1_200 }, stockOnHand: 24 },
  { productId: 'energy', name: 'Энергетик', sku: 'E', price: { currencyCode: 'TJS', minorUnits: 1_800 }, stockOnHand: 2 }
];

function order(status: string): ShopOrderDto {
  return {
    id: 'order-1', branchId: 'b', seatId: 's', playerAccountId: 'p', playerDisplayName: 'Алишер', status,
    total: { currencyCode: 'TJS', minorUnits: 2_400 },
    lines: [{ productId: 'cola', name: 'Кола 0,5 л', unitPrice: { currencyCode: 'TJS', minorUnits: 1_200 }, quantity: 2, lineTotal: { currencyCode: 'TJS', minorUnits: 2_400 } }],
    placedAtUtc: '2026-09-25T10:00:00Z', acceptedAtUtc: null, deliveredAtUtc: null, cancelledAtUtc: null, version: 1
  };
}

const realFetch = globalThis.fetch;
let calls: { path: string; method: string; body: unknown }[] = [];

function serve(handler: (path: string, method: string) => { status: number; body: unknown }) {
  globalThis.fetch = mock(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(input instanceof URL ? input.href : String(input));
    const method = init?.method ?? 'GET';
    calls.push({ path: url.pathname, method, body: init?.body ? JSON.parse(String(init.body)) : null });
    const { status, body } = handler(url.pathname, method);
    return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
  }) as unknown as typeof fetch;
}

function renderBar() {
  render(
    <I18nProvider initialLocale="ru">
      <BarTab baseUrl="https://api.example.test/" />
    </I18nProvider>
  );
}

beforeEach(() => {
  calls = [];
});

afterEach(() => {
  globalThis.fetch = realFetch;
});

describe('бар к месту', () => {
  it('меню с ценами и предупреждение о последних штуках', async () => {
    serve((path) => path.endsWith('/catalog') ? { status: 200, body: catalog } : { status: 200, body: [] });
    renderBar();

    expect(await screen.findByText('Кола 0,5 л')).toBeInTheDocument();
    expect(screen.getByText('осталось 2 штуки')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Выберите, что принести' })).toBeDisabled();
  });

  it('заказ уходит строками и ключом, после — статус заказа и пустая корзина', async () => {
    serve((path, method) => {
      if (path.endsWith('/catalog')) return { status: 200, body: catalog };
      if (path.endsWith('/orders') && method === 'POST') return { status: 200, body: order('placed') };
      return { status: 200, body: [] };
    });
    renderBar();

    const add = await screen.findByRole('button', { name: 'Добавить: Кола 0,5 л' });
    fireEvent.click(add);
    fireEvent.click(add);
    await act(async () => fireEvent.click(screen.getByRole('button', { name: /Заказать за 24/ })));

    const posted = calls.find((call) => call.method === 'POST')!;
    expect(posted.path).toBe('/api/me/shop/orders');
    expect(posted.body).toMatchObject({ lines: [{ productId: 'cola', quantity: 2 }] });
    expect(await screen.findByText('Заказ принят, готовим')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Выберите, что принести' })).toBeDisabled();
  });

  it('не хватило денег — сказано словами, корзина на месте', async () => {
    serve((path, method) => {
      if (path.endsWith('/catalog')) return { status: 200, body: catalog };
      if (path.endsWith('/orders') && method === 'POST') return { status: 409, body: { error: 'insufficient_funds' } };
      return { status: 200, body: [] };
    });
    renderBar();

    fireEvent.click(await screen.findByRole('button', { name: 'Добавить: Энергетик' }));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: /Заказать за 18/ })));

    expect(await screen.findByText('На счёте не хватает денег на этот заказ.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Заказать за 18/ })).toBeEnabled();
  });

  it('заказ, который ещё не начали готовить, можно отменить', async () => {
    serve((path, method) => {
      if (path.endsWith('/catalog')) return { status: 200, body: catalog };
      if (path.endsWith('/cancel') && method === 'POST') return { status: 200, body: order('cancelled') };
      return { status: 200, body: [order('placed')] };
    });
    renderBar();

    // Ждать — снаружи act: внутри него React не применяет обновления, и ожидание запирает само себя.
    const cancel = await screen.findByRole('button', { name: 'Отменить заказ' });
    await act(async () => fireEvent.click(cancel));

    expect(calls.some((call) => call.path === '/api/me/shop/orders/order-1/cancel')).toBe(true);
    expect(await screen.findByText('Заказ отменён, деньги вернулись на счёт')).toBeInTheDocument();
  });

  it('заказ, который уже несут, отменить нельзя — кнопки нет', async () => {
    serve((path) => path.endsWith('/catalog') ? { status: 200, body: catalog } : { status: 200, body: [order('accepted')] });
    renderBar();

    expect(await screen.findByText('Администратор несёт заказ')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Отменить заказ' })).toBeNull();
  });

  it('пустое меню — объяснение, а не пустота', async () => {
    serve(() => ({ status: 200, body: [] }));
    renderBar();

    expect(await screen.findByText(/Клуб не выложил меню/)).toBeInTheDocument();
  });
});
