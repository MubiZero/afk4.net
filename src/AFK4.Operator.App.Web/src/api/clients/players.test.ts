import { describe, expect, it } from 'bun:test';
import { createPlayerClient } from './players';

function fakeApi() {
  const calls: Array<{ method: string; path: string; body?: unknown; query?: unknown }> = [];
  const api = {
    get: async <T,>(path: string, query?: unknown) => {
      calls.push({ method: 'GET', path, query });
      return { items: [], nextCursor: null } as unknown as T;
    },
    post: async <T,>(path: string, body: unknown) => {
      calls.push({ method: 'POST', path, body });
      return body as T;
    },
    patch: async <T,>() => ({} as T)
  };
  return { api, calls };
}

const branchId = 'acfc0212-967f-4d84-94be-9003387b09c2';
const playerId = '12121212-1212-1212-1212-121212121212';
const organizationId = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';

describe('createPlayerClient', () => {
  it('maps wallet top-up and debt-payment routes with typed bodies', async () => {
    const { api, calls } = fakeApi();
    const client = createPlayerClient(api as never);

    await client.getWalletSummary(playerId);
    await client.topUpWallet(playerId, {
      organizationId,
      amount: { currencyCode: 'TJS', minorUnits: 10000 },
      reason: 'Касса',
      idempotencyKey: 'idem-top'
    });
    await client.payDebt(playerId, {
      organizationId,
      amount: { currencyCode: 'TJS', minorUnits: 3500 },
      reason: 'Возврат долга',
      idempotencyKey: 'idem-debt'
    });

    expect(calls.map((c) => `${c.method} ${c.path}`)).toEqual([
      `GET players/${playerId}/wallet-summary`,
      `POST players/${playerId}/wallet/top-ups`,
      `POST players/${playerId}/debts/payments`
    ]);
    expect(calls[1].body).toMatchObject({ reason: 'Касса', amount: { minorUnits: 10000 } });
  });

  it('builds the ledger route with entryType/accountType/before/limit query', async () => {
    const { api, calls } = fakeApi();
    const client = createPlayerClient(api as never);

    await client.getLedger(playerId, { entryType: 'top_up', cursor: 'cur-123', limit: 50 });

    expect(calls[0].method).toBe('GET');
    expect(calls[0].path).toBe(`players/${playerId}/ledger`);
    // cursor → query-параметр `before`; пустые поля не отправляются.
    expect(calls[0].query).toEqual({ entryType: 'top_up', before: 'cur-123', limit: 50 });
  });

  it('omits empty filter params from the ledger query', async () => {
    const { api, calls } = fakeApi();
    const client = createPlayerClient(api as never);

    await client.getLedger(playerId);

    expect(calls[0].query).toEqual({});
  });

  it('returns the cursor page shape', async () => {
    const { api } = fakeApi();
    const client = createPlayerClient(api as never);

    const page = await client.getLedger(playerId, { entryType: 'refund' });

    expect(page).toEqual({ items: [], nextCursor: null });
  });
});
