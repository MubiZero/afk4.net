import { it, expect, beforeEach } from 'bun:test';
import { readSupportSession, writeSupportSession, clearSupportSession, redeemSupportTicket } from './supportSession';

beforeEach(() => sessionStorage.clear());

it('хранит и очищает сессию поддержки', () => {
  expect(readSupportSession()).toBeNull();

  writeSupportSession({
    sessionToken: 's1',
    organizationId: 'o1',
    organizationName: 'Клуб',
    reason: 'Смена не открывается',
    expiresAtUtc: '2026-08-06T12:00:00Z',
    writableAreas: ['branch-settings']
  });

  expect(readSupportSession()?.organizationName).toBe('Клуб');

  clearSupportSession();
  expect(readSupportSession()).toBeNull();
});

it('игнорирует испорченное содержимое вместо падения', () => {
  sessionStorage.setItem('afk4.support.session', '{не json');
  expect(readSupportSession()).toBeNull();
});

it('обменивает билет на сессию поддержки через публичный эндпоинт', async () => {
  const calls: Array<[RequestInfo | URL, RequestInit | undefined]> = [];
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    calls.push([input, init]);
    return new Response(
      JSON.stringify({
        sessionToken: 'sess-1',
        organizationId: 'o1',
        organizationName: 'Клуб',
        reason: 'Смена не открывается',
        expiresAtUtc: '2026-08-06T12:00:00Z',
        writableAreas: ['branch-settings']
      }),
      { status: 200, headers: { 'Content-Type': 'application/json' } }
    );
  }) as unknown as typeof fetch;

  try {
    const session = await redeemSupportTicket('https://api.test/', 'ticket-1');
    expect(session.sessionToken).toBe('sess-1');
    expect(calls[0][0]).toBe('https://api.test/api/public/support-access/sessions');
    expect(calls[0][1]?.method).toBe('POST');
    expect(JSON.parse(String(calls[0][1]?.body))).toEqual({ ticket: 'ticket-1' });
  } finally {
    globalThis.fetch = originalFetch;
  }
});

it('бросает понятную ошибку, когда билет уже использован или истёк', async () => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async () => new Response(null, { status: 403 })) as unknown as typeof fetch;

  try {
    await expect(redeemSupportTicket('https://api.test/', 'ticket-1')).rejects.toThrow();
  } finally {
    globalThis.fetch = originalFetch;
  }
});
