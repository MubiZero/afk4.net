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

describe('devMockFetch receipt preview', () => {
  it('returns matching sale and receipt detail', async () => {
    const sale = await (await devMockFetch('https://x/api/pos/sales/ps-06')).json();
    expect(sale).toMatchObject({ posSaleId: 'ps-06', state: 'paid' });
    const receipt = await (await devMockFetch(`https://x/api/receipts/${sale.latestReceipt.receiptId}`)).json();
    expect(receipt.total).toEqual(sale.total);
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

describe('devMockFetch session preview', () => {
  it('reflects a confirmed open-tab start in the next floor-map read', async () => {
    const start = await devMockFetch('https://x/api/branches/branch/sessions/start', {
      method: 'POST',
      body: JSON.stringify({
        seatId: 'a2',
        durationMode: 'open',
        idempotencyKey: 'preview-start-a2'
      })
    });

    expect(start.status).toBe(200);
    const after = await (await devMockFetch('https://x/api/branches/branch/floor-map')).json();
    const seat = after.seats.find((item: { seatId: string }) => item.seatId === 'a2');

    expect(seat).toMatchObject({
      state: 'Active',
      activeSessionId: expect.any(String),
      remainingSeconds: null,
      accruedCostMinorUnits: 0
    });
  });

  it('uses the same preview fixtures for layout and staff readiness', async () => {
    const [map, zones, staff] = await Promise.all([
      devMockFetch('https://x/api/branches/branch/floor-map').then((response) => response.json()),
      devMockFetch('https://x/api/branches/branch/layout/zones').then((response) => response.json()),
      devMockFetch('https://x/api/branches/branch/staff').then((response) => response.json())
    ]);

    const layoutSeatCount = zones.reduce((total: number, zone: { seats: unknown[] }) => total + zone.seats.length, 0);
    expect(layoutSeatCount).toBe(map.seats.length);
    expect(staff.length).toBeGreaterThan(0);
  });
});

describe('devMockFetch reservation session start', () => {
  it('links one session, replays the same request, and rejects changed re-use', async () => {
    const before = await (await devMockFetch('https://x/api/branches/branch/reservations')).json();
    const reservation = before.reservations.find((item: { reservationId: string }) => item.reservationId === 'r5');
    expect(reservation).toMatchObject({ state: 'confirmed', version: 1, startedSessionId: null });

    const request = {
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      expectedVersion: 1,
      tariffRuleVersionId: 'preview-v1',
      idempotencyKey: 'reservation-r5-start',
      durationMode: 'fixed',
      durationMinutes: 60,
      billingMode: ''
    };
    const start = await devMockFetch('https://x/api/reservations/r5/start-session', {
      method: 'POST',
      body: JSON.stringify(request)
    });
    const started = await start.json();

    expect(start.status).toBe(200);
    expect(started.reservation).toMatchObject({
      reservationId: 'r5',
      state: 'seated',
      version: 2,
      startedSessionId: expect.any(String)
    });
    expect(started.session).toMatchObject({
      idempotencyKey: 'reservation-r5-start',
      session: { sessionId: started.reservation.startedSessionId, state: 'Active' }
    });

    const replay = await devMockFetch('https://x/api/reservations/r5/start-session', {
      method: 'POST',
      body: JSON.stringify(request)
    });
    expect(replay.status).toBe(200);
    expect(await replay.json()).toEqual(started);

    const independent = await devMockFetch('https://x/api/reservations/g2/start-session', {
      method: 'POST',
      body: JSON.stringify(request)
    });
    const independentlyStarted = await independent.json();
    expect(independent.status).toBe(200);
    expect(independentlyStarted.reservation).toMatchObject({
      reservationId: 'g2',
      state: 'seated',
      version: 2,
      startedSessionId: expect.any(String)
    });
    expect(independentlyStarted.reservation.startedSessionId).not.toBe(started.reservation.startedSessionId);

    const changedReuse = await devMockFetch('https://x/api/reservations/r5/start-session', {
      method: 'POST',
      body: JSON.stringify({ ...request, durationMinutes: 90 })
    });
    expect(changedReuse.status).toBe(409);
    expect(await changedReuse.json()).toMatchObject({ code: 'idempotency_conflict', currentVersion: 2 });

    const differentKey = await devMockFetch('https://x/api/reservations/r5/start-session', {
      method: 'POST',
      body: JSON.stringify({ ...request, expectedVersion: 2, idempotencyKey: 'reservation-r5-start-again' })
    });
    expect(differentKey.status).toBe(409);
    expect(await differentKey.json()).toMatchObject({ code: 'reservation_already_started', currentVersion: 2 });

    const after = await (await devMockFetch('https://x/api/branches/branch/reservations')).json();
    expect(after.reservations.find((item: { reservationId: string }) => item.reservationId === 'r5'))
      .toEqual(started.reservation);
  });
});
