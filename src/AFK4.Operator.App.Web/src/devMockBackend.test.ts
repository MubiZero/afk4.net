import { describe, expect, it } from 'bun:test';
import { devMockFetch } from './devMockBackend';

const playerId = 'pl-1';

describe('devMockFetch player data', () => {
  it('returns a populated wallet summary with varied ledger entries', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/wallet-summary`);
    const body = await res.json();
    expect(body.walletBalance.minorUnits).toBeGreaterThan(0);
    expect(Array.isArray(body.recentEntries)).toBe(true);
    expect(body.recentEntries.length).toBeGreaterThanOrEqual(3);
    const types = new Set(body.recentEntries.map((e: { entryType: string }) => e.entryType));
    expect(types.size).toBeGreaterThanOrEqual(3); // несколько разных типов операций
  });

  it('returns player packages with bonus seconds', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/packages`);
    const body = await res.json();
    expect(body.length).toBeGreaterThanOrEqual(1);
    expect(body[0].bonusSeconds).toBeGreaterThan(0);
  });

  it('echoes a wallet summary when topping up', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/wallet/top-ups`, { method: 'POST' });
    const body = await res.json();
    expect(body.walletBalance).toBeDefined();
  });
});

describe('devMockFetch /ledger keyset pagination', () => {
  it('returns first page with items and nextCursor', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/ledger?limit=10`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body.items)).toBe(true);
    expect(body.items.length).toBe(10);
    expect(typeof body.nextCursor).toBe('string');
    expect(body.nextCursor).not.toBeNull();
  });

  it('second page by cursor does not overlap with first page', async () => {
    const res1 = await devMockFetch(`https://x/api/players/${playerId}/ledger?limit=10`);
    const page1 = await res1.json();
    const cursor = page1.nextCursor as string;

    const res2 = await devMockFetch(`https://x/api/players/${playerId}/ledger?limit=10&before=${cursor}`);
    const page2 = await res2.json();

    const ids1 = new Set(page1.items.map((e: { ledgerEntryId: string }) => e.ledgerEntryId));
    const ids2 = new Set(page2.items.map((e: { ledgerEntryId: string }) => e.ledgerEntryId));
    const intersection = [...ids2].filter((id) => ids1.has(id));
    expect(intersection.length).toBe(0);
  });

  it('last page has nextCursor null', async () => {
    // 48 записей, limit=50 — влезает в одну страницу
    const res = await devMockFetch(`https://x/api/players/${playerId}/ledger?limit=50`);
    const body = await res.json();
    expect(body.items.length).toBeGreaterThanOrEqual(48);
    expect(body.nextCursor).toBeNull();
  });

  it('filter by entryType returns only matching records', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/ledger?entryType=top_up&limit=50`);
    const body = await res.json();
    expect(body.items.length).toBeGreaterThan(0);
    for (const item of body.items) {
      expect(item.entryType).toBe('top_up');
    }
  });

  it('filter by accountType returns only matching records', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/ledger?accountType=debt&limit=50`);
    const body = await res.json();
    expect(body.items.length).toBeGreaterThan(0);
    for (const item of body.items) {
      expect(item.accountType).toBe('debt');
    }
  });

  it('ledger route does not intercept /packages endpoint', async () => {
    const res = await devMockFetch(`https://x/api/players/${playerId}/packages`);
    const body = await res.json();
    // packages возвращает массив, а не { items, nextCursor }
    expect(Array.isArray(body)).toBe(true);
  });
});
