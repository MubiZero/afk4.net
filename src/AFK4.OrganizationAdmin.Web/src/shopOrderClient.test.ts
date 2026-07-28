import { describe, expect, it } from 'bun:test';
import { createOperatorApiClients } from './operatorApiClients';
import { PlatformApiClient } from './platformApi';

function clientCapturing(record: (method: string, path: string, body: unknown) => void): PlatformApiClient {
  return new PlatformApiClient({
    baseUrl: 'https://api.test',
    getAccessToken: async () => 'token',
    fetchImpl: async (url, init) => {
      record(init?.method ?? 'GET', new URL(String(url)).pathname, init?.body ? JSON.parse(String(init.body)) : undefined);
      return new Response(JSON.stringify([]), { status: 200, headers: { 'Content-Type': 'application/json' } });
    }
  });
}

describe('shopOrders client', () => {
  it('lists the branch queue', async () => {
    let seen = '';
    const clients = createOperatorApiClients(clientCapturing((_m, path) => { seen = path; }), 'org1');
    await clients.shopOrders.listQueue('b1');
    expect(seen).toBe('/api/organizations/org1/branches/b1/shop/orders');
  });

  it('accepts an order with expectedVersion', async () => {
    let captured: { method: string; path: string; body: unknown } | null = null as { method: string; path: string; body: unknown } | null;
    const clients = createOperatorApiClients(clientCapturing((method, path, body) => { captured = { method, path, body }; }), 'org1');
    await clients.shopOrders.accept('b1', 'o1', 2);
    expect(captured).toEqual({ method: 'POST', path: '/api/organizations/org1/branches/b1/shop/orders/o1/accept', body: { expectedVersion: 2 } });
  });

  it('delivers an order with expectedVersion', async () => {
    let captured: { method: string; path: string; body: unknown } | null = null as { method: string; path: string; body: unknown } | null;
    const clients = createOperatorApiClients(clientCapturing((method, path, body) => { captured = { method, path, body }; }), 'org1');
    await clients.shopOrders.deliver('b1', 'o1', 3);
    expect(captured).toEqual({ method: 'POST', path: '/api/organizations/org1/branches/b1/shop/orders/o1/deliver', body: { expectedVersion: 3 } });
  });

  it('cancels an order with expectedVersion', async () => {
    let captured: { method: string; path: string; body: unknown } | null = null as { method: string; path: string; body: unknown } | null;
    const clients = createOperatorApiClients(clientCapturing((method, path, body) => { captured = { method, path, body }; }), 'org1');
    await clients.shopOrders.cancel('b1', 'o1', 3);
    expect(captured).toEqual({ method: 'POST', path: '/api/organizations/org1/branches/b1/shop/orders/o1/cancel', body: { expectedVersion: 3 } });
  });
});
