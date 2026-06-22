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
