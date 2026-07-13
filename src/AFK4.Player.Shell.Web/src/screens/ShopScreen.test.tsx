import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, mock } from 'bun:test';
import { ShopScreen } from './ShopScreen';
import { ApiError, type ShellApi } from '../shellApi';
import type { ShopOrderDto } from '../apiTypes';

const cola = { productId: 'p1', name: 'Cola', sku: 'COLA', price: { currencyCode: 'TJS', minorUnits: 500 }, stockOnHand: 10 };

function api(over: Partial<ShellApi>): ShellApi {
  return {
    listTariffs: async () => [], listPackages: async () => [], createTopUpIntent: async () => ({} as any),
    getTopUpIntents: async () => [], extendSession: async () => ({}),
    listShopCatalog: async () => [cola], placeShopOrder: async () => ({} as any),
    listShopOrders: async () => [], cancelShopOrder: async () => ({} as any),
    ...over
  } as unknown as ShellApi;
}

describe('ShopScreen', () => {
  it('lists catalog and places an order with the cart contents', async () => {
    let placedLines: unknown;
    render(<ShopScreen api={api({ placeShopOrder: async (lines) => { placedLines = lines; return { id: 'o1', status: 'placed', lines: [], total: { currencyCode: 'TJS', minorUnits: 1000 }, version: 1 } as any; } })}
      onNeedTopUp={() => {}} onDone={() => {}} pollIntervalMs={5} />);

    await waitFor(() => screen.getByText('Cola'));
    fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
    fireEvent.click(screen.getByRole('button', { name: /заказать/i }));
    await waitFor(() => expect(placedLines).toEqual([{ productId: 'p1', quantity: 1 }]));
  });

  it('keeps one request and key while the same submit gesture is in flight', async () => {
    let resolveOrder!: (value: ShopOrderDto) => void;
    const pending = new Promise<ShopOrderDto>((resolve) => { resolveOrder = resolve; });
    const placeShopOrder = mock(async () => pending);
    render(<ShopScreen api={api({ placeShopOrder })}
      onNeedTopUp={() => {}} onDone={() => {}} pollIntervalMs={5000} />);

    await waitFor(() => screen.getByText('Cola'));
    fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
    const submit = screen.getByRole('button', { name: /заказать/i });
    fireEvent.click(submit);
    fireEvent.click(submit);

    expect(placeShopOrder).toHaveBeenCalledTimes(1);
    expect(placeShopOrder.mock.calls[0][1]).toMatch(/^shop-/);

    resolveOrder({
      id: 'o1', posSaleId: 's1', status: 'placed', lines: [],
      total: { currencyCode: 'TJS', minorUnits: 500 }, version: 1
    } as ShopOrderDto);
    await waitFor(() => screen.getByText(/заказ принят/i));
  });

  it('unlocks after failure and uses a new key for a later submit', async () => {
    const placedOrder = {
      id: 'o1', posSaleId: 's1', status: 'placed', lines: [],
      total: { currencyCode: 'TJS', minorUnits: 500 }, version: 1
    } as ShopOrderDto;
    const placeShopOrder = mock()
      .mockRejectedValueOnce(new ApiError(500, 'failed'))
      .mockResolvedValueOnce(placedOrder);
    render(<ShopScreen api={api({ placeShopOrder })}
      onNeedTopUp={() => {}} onDone={() => {}} pollIntervalMs={5000} />);

    await waitFor(() => screen.getByText('Cola'));
    fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
    fireEvent.click(screen.getByRole('button', { name: /заказать/i }));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Не удалось оформить заказ'));

    fireEvent.click(screen.getByRole('button', { name: /заказать/i }));
    await waitFor(() => screen.getByText(/заказ принят/i));

    expect(placeShopOrder).toHaveBeenCalledTimes(2);
    expect(placeShopOrder.mock.calls[0][1]).toMatch(/^shop-/);
    expect(placeShopOrder.mock.calls[1][1]).toMatch(/^shop-/);
    expect(placeShopOrder.mock.calls[1][1]).not.toBe(placeShopOrder.mock.calls[0][1]);
  });

  it('calls onNeedTopUp when the server says insufficient_funds', async () => {
    let asked = false;
    render(<ShopScreen api={api({ placeShopOrder: async () => { throw new ApiError(409, 'x', 'insufficient_funds'); } })}
      onNeedTopUp={() => { asked = true; }} onDone={() => {}} pollIntervalMs={5} />);
    await waitFor(() => screen.getByText('Cola'));
    fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
    fireEvent.click(screen.getByRole('button', { name: /заказать/i }));
    await waitFor(() => expect(asked).toBe(true));
  });

  it('shows "Товар закончился" and does NOT call onNeedTopUp when out_of_stock', async () => {
    let asked = false;
    render(<ShopScreen api={api({ placeShopOrder: async () => { throw new ApiError(409, 'x', 'out_of_stock'); } })}
      onNeedTopUp={() => { asked = true; }} onDone={() => {}} pollIntervalMs={5} />);
    await waitFor(() => screen.getByText('Cola'));
    fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
    fireEvent.click(screen.getByRole('button', { name: /заказать/i }));
    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent('Товар закончился'));
    expect(asked).toBe(false);
  });

  it('shows order status after placing and polls for updates', async () => {
    let polls = 0;
    render(<ShopScreen api={api({
      placeShopOrder: async () => ({ id: 'o1', status: 'placed', lines: [], total: { currencyCode: 'TJS', minorUnits: 500 }, version: 1 } as any),
      listShopOrders: async () => { polls++; return [{ id: 'o1', status: polls >= 2 ? 'delivered' : 'placed', lines: [], total: { currencyCode: 'TJS', minorUnits: 500 }, version: polls >= 2 ? 3 : 1 } as any]; }
    })} onNeedTopUp={() => {}} onDone={() => {}} pollIntervalMs={5} />);
    await waitFor(() => screen.getByText('Cola'));
    fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
    fireEvent.click(screen.getByRole('button', { name: /заказать/i }));
    await waitFor(() => expect(screen.getByText(/доставлен/i)).toBeInTheDocument(), { timeout: 2000 });
  });
});
