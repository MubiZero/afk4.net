import { it, expect, beforeEach } from 'bun:test';
import {
  readSupportSession,
  writeSupportSession,
  clearSupportSession,
  redeemSupportTicket,
  endSupportSession,
  isSupportSessionExpired,
  supportOperatorSession,
  SUPPORT_GRANT_HEADER_NAME,
  type SupportSession
} from './supportSession';
import { permissionNames } from '../permissionNames';

function sessionExpiringAt(expiresAtUtc: string): SupportSession {
  return {
    sessionToken: 's1',
    organizationId: 'o1',
    organizationName: 'Клуб',
    reason: 'Смена не открывается',
    expiresAtUtc,
    writableAreas: [],
    branches: []
  };
}

const twoBranchSession: SupportSession = {
  sessionToken: 's1',
  organizationId: 'o1',
  organizationName: 'Клуб',
  reason: 'Смена не открывается',
  expiresAtUtc: '2026-08-06T12:00:00Z',
  writableAreas: ['branch-settings'],
  branches: [
    { branchId: 'b1', name: 'Филиал на Рудаки' },
    { branchId: 'b2', name: 'Филиал на Айни' }
  ]
};

beforeEach(() => sessionStorage.clear());

it('хранит и очищает сессию поддержки', () => {
  expect(readSupportSession()).toBeNull();

  writeSupportSession(twoBranchSession);

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
        writableAreas: ['branch-settings'],
        branches: [{ branchId: 'b1', name: 'Филиал на Рудаки' }]
      }),
      { status: 200, headers: { 'Content-Type': 'application/json' } }
    );
  }) as unknown as typeof fetch;

  try {
    const session = await redeemSupportTicket('https://api.test/', 'ticket-1');
    expect(session.sessionToken).toBe('sess-1');
    expect(session.branches).toEqual([{ branchId: 'b1', name: 'Филиал на Рудаки' }]);
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

it('endSupportSession зовёт DELETE с грант-заголовком', async () => {
  const calls: Array<[RequestInfo | URL, RequestInit | undefined]> = [];
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async (input: RequestInfo | URL, init?: RequestInit) => {
    calls.push([input, init]);
    return new Response(null, { status: 204 });
  }) as unknown as typeof fetch;

  try {
    await endSupportSession('https://api.test/', 'sess-1');
    expect(calls.length).toBe(1);
    expect(calls[0][0]).toBe('https://api.test/api/support-access/session');
    expect(calls[0][1]?.method).toBe('DELETE');
    expect(new Headers(calls[0][1]?.headers).get(SUPPORT_GRANT_HEADER_NAME)).toBe('sess-1');
  } finally {
    globalThis.fetch = originalFetch;
  }
});

it('endSupportSession бросает на не-2xx ответ', async () => {
  const originalFetch = globalThis.fetch;
  globalThis.fetch = (async () => new Response(null, { status: 500 })) as unknown as typeof fetch;

  try {
    await expect(endSupportSession('https://api.test/', 'sess-1')).rejects.toThrow();
  } finally {
    globalThis.fetch = originalFetch;
  }
});

it('считает сессию истёкшей строго по времени и при битом expiresAtUtc (fail-safe)', () => {
  const now = Date.parse('2026-08-06T12:00:00Z');

  expect(isSupportSessionExpired(sessionExpiringAt('2026-08-06T12:00:01Z'), now)).toBe(false);
  expect(isSupportSessionExpired(sessionExpiringAt('2026-08-06T12:00:00Z'), now)).toBe(true);
  expect(isSupportSessionExpired(sessionExpiringAt('2026-08-06T11:59:59Z'), now)).toBe(true);
  expect(isSupportSessionExpired(sessionExpiringAt('not-a-date'), now)).toBe(true);
});

it('supportOperatorSession adapts a support grant into the shell session shape, branches and all', () => {
  const operatorSession = supportOperatorSession(twoBranchSession);

  expect(operatorSession.organizationId).toBe('o1');
  expect(operatorSession.branchIds).toEqual(['b1', 'b2']);
  expect(operatorSession.displayName).toBe('Поддержка платформы');
});

it('supportOperatorSession grants read permissions plus only the write permissions its writableAreas cover', () => {
  const operatorSession = supportOperatorSession(twoBranchSession); // writableAreas: ['branch-settings']

  // Read stays broad — the server opens read access regardless of writableAreas.
  expect(operatorSession.permissions).toContain(permissionNames.viewFloorMap);
  expect(operatorSession.permissions).toContain(permissionNames.viewReports);
  // branch-settings is granted — its write permission is present.
  expect(operatorSession.permissions).toContain(permissionNames.manageBranchSettings);
  // No other area was granted — their write permissions must be absent, not just unused.
  expect(operatorSession.permissions).not.toContain(permissionNames.manageLayout);
  expect(operatorSession.permissions).not.toContain(permissionNames.manageBranchStaff);
  expect(operatorSession.permissions).not.toContain(permissionNames.assignDeviceSeat);
  // Permissions with no writable-area equivalent at all (money, or simply unsupported) never appear.
  expect(operatorSession.permissions).not.toContain(permissionNames.manageNews);
  expect(operatorSession.permissions).not.toContain(permissionNames.managePosCatalog);
  expect(operatorSession.permissions).not.toContain(permissionNames.openShift);
});
