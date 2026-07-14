import { describe, expect, it } from 'bun:test';
import { ApiError, createShellApi, OfflineError } from './shellApi';
import type { PlayerLoyaltyDto } from './apiTypes';

function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

describe('shellApi', () => {
  it('lists tariffs for a branch', async () => {
    const api = createShellApi('https://api.test', async () => jsonResponse([{ name: 'Standard' }]));
    const tariffs = await api.listTariffs('branch-1');
    expect(tariffs[0].name).toBe('Standard');
  });

  it('creates a dcgate top-up intent', async () => {
    let captured: any;
    const api = createShellApi('https://api.test', async (url, init) => {
      captured = { url, body: JSON.parse(String(init?.body)) };
      return jsonResponse({ paymentIntentId: 'p1', state: 'pending', payUrl: 'pay.dc.tj/x' });
    });
    const intent = await api.createTopUpIntent(5000);
    expect(captured.url).toContain('/api/me/wallet/top-up-intent');
    expect(captured.body.method).toBe('dcgate');
    expect(intent.payUrl).toBe('pay.dc.tj/x');
  });

  it('throws OfflineError when fetch rejects', async () => {
    const api = createShellApi('https://api.test', async () => { throw new TypeError('Failed to fetch'); });
    await expect(api.listTariffs('b')).rejects.toBeInstanceOf(OfflineError);
  });

  it('surfaces a 409 as a typed conflict', async () => {
    const api = createShellApi('https://api.test', async () => jsonResponse({ error: 'conflict' }, 409));
    await expect(api.extendSession('s1', { additionalMinutes: 30, tariffRuleVersionId: 't', idempotencyKey: 'k' }))
      .rejects.toMatchObject({ status: 409 });
  });
});

describe('shellApi shop methods', () => {
  it('listShopCatalog GETs the catalog', async () => {
    let seenUrl = '';
    const api = createShellApi('https://api.test', async (url) => {
      seenUrl = String(url);
      return new Response(JSON.stringify([{ productId: 'p1', name: 'Cola', sku: 'COLA', price: { currencyCode: 'TJS', minorUnits: 500 }, stockOnHand: 10 }]), { status: 200, headers: { 'Content-Type': 'application/json' } });
    });
    const catalog = await api.listShopCatalog();
    expect(seenUrl).toBe('https://api.test/api/me/shop/catalog');
    expect(catalog[0].name).toBe('Cola');
  });

  it('posts a caller supplied shop order idempotency key', async () => {
    let seenBody = '';
    const api = createShellApi('https://api.test', async (_url, init) => {
      seenBody = String(init?.body ?? '');
      return new Response(JSON.stringify({ id: 'o1', status: 'placed' }), { status: 200, headers: { 'Content-Type': 'application/json' } });
    });
    await api.placeShopOrder([{ productId: 'p1', quantity: 2 }], 'shop-gesture-1');
    expect(JSON.parse(seenBody)).toEqual({
      lines: [{ productId: 'p1', quantity: 2 }],
      idempotencyKey: 'shop-gesture-1'
    });
  });

  it('surfaces the server error code on 409', async () => {
    const api = createShellApi('https://api.test', async () =>
      new Response(JSON.stringify({ error: 'insufficient_funds' }), { status: 409, headers: { 'Content-Type': 'application/json' } })
    );
    try {
      await api.placeShopOrder([{ productId: 'p1', quantity: 99 }]);
      throw new Error('should have thrown');
    } catch (e) {
      expect(e).toBeInstanceOf(ApiError);
      expect((e as ApiError).status).toBe(409);
      expect((e as ApiError).code).toBe('insufficient_funds');
    }
  });
});

describe('shellApi.getLoyalty', () => {
  it('GETs /api/me/loyalty and returns the dto', async () => {
    const dto: PlayerLoyaltyDto = {
      topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0,
      totalEarned: { currencyCode: 'TJS', minorUnits: 200 }, recent: []
    };
    let calledUrl = '';
    const api = createShellApi('http://x', async (url) => {
      calledUrl = url;
      return new Response(JSON.stringify(dto), { status: 200, headers: { 'Content-Type': 'application/json' } });
    });
    const result = await api.getLoyalty();
    expect(calledUrl).toBe('http://x/api/me/loyalty');
    expect(result.topUpPercentBasisPoints).toBe(500);
  });
});
