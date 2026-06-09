import { describe, expect, it } from 'bun:test';
import { createShellApi, OfflineError } from './shellApi';

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
