import { describe, expect, it } from 'bun:test';
import { createMockSession, devMockFetch } from './devMockBackend';

const playerId = 'pl-1';

describe('dev preview session', () => {
  it('contains the real role contract used by the system footer', () => {
    expect(createMockSession()).toMatchObject({
      displayName: 'Администратор смены',
      roleNames: ['operator']
    });
  });

  // `?nobranch` в адресе превью: сотрудник без единого назначения. Сервер строит филиалы и права
  // из назначений ролей, поэтому пустыми приходят и те, и другие.
  it('models a staff member without any branch assignment', () => {
    const session = createMockSession({ withoutBranch: true });
    expect(session).toMatchObject({ branchIds: [], roleNames: [], permissions: [] });
    expect(session).not.toHaveProperty('activeBranchId');
  });
});

// Preview sign-in is HTTP now (authClient.ts → StaffAuthApi), mocked here instead of over the
// old WebView2 auth bridge — any credentials succeed against these fixtures.
describe('devMockFetch staff auth', () => {
  it('signs in by login with a mock session', async () => {
    const res = await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/auth/staff/sign-in-by-login', {
      method: 'POST',
      body: JSON.stringify({ login: 'anyone', password: 'anything' })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body).toMatchObject({ displayName: 'Администратор смены', roleNames: ['operator'] });
  });

  it('signs in to a chosen club with a mock session', async () => {
    const res = await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/auth/staff/sign-in', {
      method: 'POST',
      body: JSON.stringify({ organizationId: 'org-1', userName: 'anyone', password: 'anything' })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.accessToken).toBeTruthy();
  });

  it('refreshes with a mock session', async () => {
    const res = await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/auth/staff/refresh', {
      method: 'POST',
      body: JSON.stringify({ organizationId: 'org-1', refreshToken: 'preview-refresh-token' })
    });
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(body.refreshToken).toBeTruthy();
  });
});

describe('devMockFetch player data', () => {
  it('returns a populated wallet summary with varied ledger entries', async () => {
    const res = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/wallet-summary`);
    const body = await res.json();
    expect(body.walletBalance.minorUnits).toBeGreaterThan(0);
    expect(Array.isArray(body.recentEntries)).toBe(true);
    expect(body.recentEntries.length).toBeGreaterThanOrEqual(3);
    const types = new Set(body.recentEntries.map((e: { entryType: string }) => e.entryType));
    expect(types.size).toBeGreaterThanOrEqual(3); // несколько разных типов операций
  });

  it('returns player packages with bonus seconds', async () => {
    const res = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/packages`);
    const body = await res.json();
    expect(body.length).toBeGreaterThanOrEqual(1);
    expect(body[0].bonusSeconds).toBeGreaterThan(0);
  });

  it('echoes a wallet summary when topping up', async () => {
    const res = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/wallet/top-ups`, { method: 'POST' });
    const body = await res.json();
    expect(body.walletBalance).toBeDefined();
  });
});

describe('devMockFetch device lifecycle', () => {
  it('persists rename and removes the device from the authoritative inventory', async () => {
    const before = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/devices')).json();
    const deviceId = before[0].deviceId as string;
    await devMockFetch(`https://x/api/devices/${deviceId}/rename`, { method: 'POST', body: JSON.stringify({ displayName: 'VIP-02' }) });
    const renamed = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/devices')).json();
    expect(renamed.find((device: { deviceId: string }) => device.deviceId === deviceId).machineName).toBe('VIP-02');
    await devMockFetch(`https://x/api/devices/${deviceId}/remove`, { method: 'POST', body: JSON.stringify({ reason: 'retired' }) });
    const after = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/devices')).json();
    expect(after.some((device: { deviceId: string }) => device.deviceId === deviceId)).toBe(false);
  });
});

describe('devMockFetch receipt preview', () => {
  it('returns matching sale and receipt detail', async () => {
    const sale = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/pos/sales/ps-06')).json();
    expect(sale).toMatchObject({ posSaleId: 'ps-06', state: 'paid' });
    const receipt = await (await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-cf0d307d0f08/receipts/${sale.latestReceipt.receiptId}`)).json();
    expect(receipt.total).toEqual(sale.total);
  });
});

describe('devMockFetch approval preview', () => {
  it('moves an approved request from pending into audit history', async () => {
    const pending = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/money-actions')).json();
    expect(pending.requests.length).toBeGreaterThan(0);
    const request = pending.requests[0];
    const decision = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/money-actions/${request.moneyActionRequestId}/approve`, {
      method: 'POST', body: JSON.stringify({ decisionReason: null })
    });
    expect(decision.status).toBe(200);
    const after = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/money-actions')).json();
    expect(after.requests.some((item: { moneyActionRequestId: string }) => item.moneyActionRequestId === request.moneyActionRequestId)).toBe(false);
    const audit = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/audit?limit=50')).json();
    expect(audit.records.some((record: { action: string }) => record.action === 'money_action.approved')).toBe(true);
  });
});

describe('devMockFetch /ledger keyset pagination', () => {
  it('returns first page with items and nextCursor', async () => {
    const res = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/ledger?limit=10`);
    expect(res.status).toBe(200);
    const body = await res.json();
    expect(Array.isArray(body.items)).toBe(true);
    expect(body.items.length).toBe(10);
    expect(typeof body.nextCursor).toBe('string');
    expect(body.nextCursor).not.toBeNull();
  });

  it('second page by cursor does not overlap with first page', async () => {
    const res1 = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/ledger?limit=10`);
    const page1 = await res1.json();
    const cursor = page1.nextCursor as string;

    const res2 = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/ledger?limit=10&before=${cursor}`);
    const page2 = await res2.json();

    const ids1 = new Set(page1.items.map((e: { ledgerEntryId: string }) => e.ledgerEntryId));
    const ids2 = new Set(page2.items.map((e: { ledgerEntryId: string }) => e.ledgerEntryId));
    const intersection = [...ids2].filter((id) => ids1.has(id));
    expect(intersection.length).toBe(0);
  });

  it('last page has nextCursor null', async () => {
    // 48 записей, limit=50 — влезает в одну страницу
    const res = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/ledger?limit=50`);
    const body = await res.json();
    expect(body.items.length).toBeGreaterThanOrEqual(48);
    expect(body.nextCursor).toBeNull();
  });

  it('filter by entryType returns only matching records', async () => {
    const res = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/ledger?entryType=top_up&limit=50`);
    const body = await res.json();
    expect(body.items.length).toBeGreaterThan(0);
    for (const item of body.items) {
      expect(item.entryType).toBe('top_up');
    }
  });

  it('filter by accountType returns only matching records', async () => {
    const res = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/ledger?accountType=debt&limit=50`);
    const body = await res.json();
    expect(body.items.length).toBeGreaterThan(0);
    for (const item of body.items) {
      expect(item.accountType).toBe('debt');
    }
  });

  it('ledger route does not intercept /packages endpoint', async () => {
    const res = await devMockFetch(`https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/players/${playerId}/packages`);
    const body = await res.json();
    // packages возвращает массив, а не { items, nextCursor }
    expect(Array.isArray(body)).toBe(true);
  });
});

describe('devMockFetch session preview', () => {
  it('reflects a confirmed open-tab start in the next floor-map read', async () => {
    const start = await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/sessions/start', {
      method: 'POST',
      body: JSON.stringify({
        seatId: 'a2',
        durationMode: 'open',
        idempotencyKey: 'preview-start-a2'
      })
    });

    expect(start.status).toBe(200);
    const after = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/floor-map')).json();
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
      devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/floor-map').then((response) => response.json()),
      devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/layout/zones').then((response) => response.json()),
      devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/staff').then((response) => response.json())
    ]);

    const layoutSeatCount = zones.reduce((total: number, zone: { seats: unknown[] }) => total + zone.seats.length, 0);
    expect(layoutSeatCount).toBe(map.seats.length);
    expect(staff.length).toBeGreaterThan(0);
  });
});

// Демо-Панель стоит на этой заглушке: «+15 мин», перенос и завершение должны менять карту, иначе
// посетитель жмёт кнопку, получает «готово» и видит, что ничего не случилось.
describe('devMockFetch session lifecycle on the map', () => {
  const org = 'https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';
  const post = (path: string, body: Record<string, unknown>) =>
    devMockFetch(`${org}${path}`, { method: 'POST', body: JSON.stringify(body) });
  const seatById = async (seatId: string) => {
    const map = await (await devMockFetch(`${org}/branches/branch/floor-map`)).json();
    return map.seats.find((item: { seatId: string }) => item.seatId === seatId);
  };

  it('adds the bought minutes to the remaining time', async () => {
    const before = (await seatById('a1')).remainingSeconds as number;
    expect((await post('/sessions/s1/extend', { additionalMinutes: 15, idempotencyKey: 'k-extend' })).status).toBe(200);
    expect((await seatById('a1')).remainingSeconds).toBe(before + 15 * 60);
  });

  // Свободные ПК заглушки заняты бронями соседних тестов — место под перенос освобождает конец сессии.
  it('frees the seat when the session ends', async () => {
    expect((await post('/sessions/s9/end', { reason: 'operator', idempotencyKey: 'k-end' })).status).toBe(200);
    expect(await seatById('c3')).toMatchObject({ state: 'Free', activeSessionId: null, isDeviceLocked: true });
    expect((await post('/sessions/s9/end', { reason: 'operator', idempotencyKey: 'k-end-again' })).status).toBe(409);
  });

  it('moves the session to a free seat and frees the old one', async () => {
    expect((await post('/sessions/s5/transfer', { targetSeatId: 'c3', idempotencyKey: 'k-transfer' })).status).toBe(200);
    expect(await seatById('c3')).toMatchObject({ state: 'Active', activeSessionId: 's5', playerDisplayName: 'Мадина С.' });
    expect(await seatById('b1')).toMatchObject({ state: 'Free', activeSessionId: null });
  });

  it('refuses to move a session onto a busy seat', async () => {
    const response = await post('/sessions/s5/transfer', { targetSeatId: 'a1', idempotencyKey: 'k-transfer-busy' });
    expect(response.status).toBe(409);
    expect(await seatById('c3')).toMatchObject({ state: 'Active', activeSessionId: 's5' });
  });
});

describe('devMockFetch reservation session start', () => {
  it('links one session, replays the same request, and rejects changed re-use', async () => {
    const before = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/reservations')).json();
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
    const start = await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/reservations/r5/start-session', {
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

    const replay = await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/reservations/r5/start-session', {
      method: 'POST',
      body: JSON.stringify(request)
    });
    expect(replay.status).toBe(200);
    expect(await replay.json()).toEqual(started);

    const independent = await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/reservations/g2/start-session', {
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

    const changedReuse = await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/reservations/r5/start-session', {
      method: 'POST',
      body: JSON.stringify({ ...request, durationMinutes: 90 })
    });
    expect(changedReuse.status).toBe(409);
    expect(await changedReuse.json()).toMatchObject({ code: 'idempotency_conflict', currentVersion: 2 });

    const differentKey = await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/reservations/r5/start-session', {
      method: 'POST',
      body: JSON.stringify({ ...request, expectedVersion: 2, idempotencyKey: 'reservation-r5-start-again' })
    });
    expect(differentKey.status).toBe(409);
    expect(await differentKey.json()).toMatchObject({ code: 'reservation_already_started', currentVersion: 2 });

    const after = await (await devMockFetch('https://x/api/organizations/0c04d6c0-bfa8-4e26-9263-fc0d307d0f08/branches/branch/reservations')).json();
    expect(after.reservations.find((item: { reservationId: string }) => item.reservationId === 'r5'))
      .toEqual(started.reservation);
  });
});
