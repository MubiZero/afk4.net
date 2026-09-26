import { describe, expect, it } from 'bun:test';
import { PlayerApiError } from '../api/playerApi';
import { MAX_PER_ITEM, barErrorKey, cartLines, cartTotalMinor, changeQuantity, fewLeft } from './bar';

const cola = { productId: 'cola', name: 'Кола', sku: 'C', price: { currencyCode: 'TJS', minorUnits: 1_200 }, stockOnHand: 24 };
const chips = { productId: 'chips', name: 'Чипсы', sku: 'H', price: { currencyCode: 'TJS', minorUnits: 900 }, stockOnHand: 0 };

describe('корзина бара', () => {
  it('ноль убирает строку: пустая строка ничего не заказывает', () => {
    const one = changeQuantity({}, 'cola', 1);
    expect(one).toEqual({ cola: 1 });
    expect(changeQuantity(one, 'cola', -1)).toEqual({});
  });

  it('больше двадцати одной позиции — скорее опечатка', () => {
    let cart = {};
    for (let i = 0; i < 25; i++) cart = changeQuantity(cart, 'cola', 1);
    expect(cart).toEqual({ cola: MAX_PER_ITEM });
  });

  it('итог — по ценам меню, строки — для сервера', () => {
    const cart = { cola: 2, chips: 1 };
    expect(cartTotalMinor(cart, [cola, chips])).toBe(3_300);
    expect(cartLines(cart)).toEqual([{ productId: 'cola', quantity: 2 }, { productId: 'chips', quantity: 1 }]);
  });

  it('«осталось мало» — только когда их правда мало, ноль значит «не считают»', () => {
    expect(fewLeft({ ...cola, stockOnHand: 2 })).toBe(true);
    expect(fewLeft(cola)).toBe(false);
    expect(fewLeft(chips)).toBe(false);
  });

  it('отказы заказа — теми же словами, что в приложении', () => {
    expect(barErrorKey(new PlayerApiError(409, 'insufficient_funds'))).toBe('playerShell.bar.error.funds');
    expect(barErrorKey(new PlayerApiError(409, 'no_active_session'))).toBe('playerShell.bar.error.noSession');
    expect(barErrorKey(new PlayerApiError(409, 'open_shift_required'))).toBe('playerShell.bar.error.counterClosed');
    expect(barErrorKey(new TypeError('Failed to fetch'))).toBe('playerShell.chooseTime.error.offline');
  });
});
