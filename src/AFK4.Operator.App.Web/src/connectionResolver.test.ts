import { describe, expect, it, vi } from 'vitest';
import {
  ConnectionResolutionError,
  ConnectionResolver,
  OperatorTenantStatus,
  clearStoredConnection,
  readStoredConnection,
  writeStoredConnection,
  type ResolveOperatorConnectionResponse,
  type StorageLike
} from './connectionResolver';

function buildStorage(): StorageLike & { store: Map<string, string> } {
  const store = new Map<string, string>();
  return {
    store,
    getItem: key => (store.has(key) ? (store.get(key) as string) : null),
    setItem: (key, value) => {
      store.set(key, value);
    },
    removeItem: key => {
      store.delete(key);
    }
  };
}

function buildResponse(overrides: Partial<ResolveOperatorConnectionResponse> = {}): ResolveOperatorConnectionResponse {
  return {
    organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
    organizationSlug: 'demo-club',
    organizationName: 'Demo Club',
    organizationStatus: OperatorTenantStatus.Active,
    organizationStatusReason: null,
    branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
    branchSlug: 'main',
    branchName: 'Main Branch',
    branchCity: 'Dushanbe',
    source: 'slug',
    ...overrides
  };
}

describe('ConnectionResolver', () => {
  it('resolves by slug pair and returns parsed body', async () => {
    const expected = buildResponse();
    const fetchImpl = vi.fn(async () =>
      new Response(JSON.stringify(expected), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
    );
    const resolver = new ConnectionResolver({
      baseUrl: 'http://localhost:5000/',
      fetchImpl: fetchImpl as unknown as typeof fetch
    });

    const result = await resolver.resolveBySlugPair('demo-club', 'main');

    expect(result).toEqual(expected);
    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost:5000/api/operator-connections/resolve');
    expect(call[1].method).toBe('POST');
    expect(call[1].body).toBe(
      JSON.stringify({ organizationSlug: 'demo-club', branchSlug: 'main', setupCode: null })
    );
  });

  it('resolves by setup code and returns parsed body', async () => {
    const expected = buildResponse({ source: 'setup_code' });
    const fetchImpl = vi.fn(async () =>
      new Response(JSON.stringify(expected), {
        status: 200,
        headers: { 'Content-Type': 'application/json' }
      })
    );
    const resolver = new ConnectionResolver({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch
    });

    const result = await resolver.resolveBySetupCode('abcd1234abcd1234abcd1234abcd1234');

    expect(result.source).toBe('setup_code');
    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(JSON.parse(call[1].body as string)).toEqual({
      organizationSlug: null,
      branchSlug: null,
      setupCode: 'abcd1234abcd1234abcd1234abcd1234'
    });
  });

  it('throws ConnectionResolutionError with parsed error body on failure', async () => {
    const fetchImpl = vi.fn(async () =>
      new Response(JSON.stringify({ error: 'Setup code is no longer usable.' }), {
        status: 400,
        headers: { 'Content-Type': 'application/json' }
      })
    );
    const resolver = new ConnectionResolver({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch
    });

    await expect(resolver.resolveBySetupCode('xx')).rejects.toMatchObject({
      status: 400,
      message: 'Setup code is no longer usable.'
    });
    await expect(resolver.resolveBySetupCode('xx')).rejects.toBeInstanceOf(ConnectionResolutionError);
  });

  it('falls back to a generic message when the body is not JSON', async () => {
    const fetchImpl = vi.fn(async () => new Response('upstream-error', { status: 502 }));
    const resolver = new ConnectionResolver({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch
    });

    await expect(resolver.resolveBySlugPair('a', 'b')).rejects.toMatchObject({
      status: 502,
      message: 'Failed to resolve operator connection.'
    });
  });
});

describe('connection storage', () => {
  it('round-trips a written connection', () => {
    const storage = buildStorage();
    const stored = writeStoredConnection(buildResponse(), storage, new Date('2026-05-23T12:00:00Z'));
    expect(stored.storedAtUtc).toBe('2026-05-23T12:00:00.000Z');
    expect(readStoredConnection(storage)).toEqual(stored);
  });

  it('returns null when storage is empty', () => {
    const storage = buildStorage();
    expect(readStoredConnection(storage)).toBeNull();
  });

  it('returns null when payload is malformed', () => {
    const storage = buildStorage();
    storage.setItem('afk4.operator.connection', '{');
    expect(readStoredConnection(storage)).toBeNull();
  });

  it('returns null when organizationId is missing', () => {
    const storage = buildStorage();
    storage.setItem('afk4.operator.connection', JSON.stringify({ organizationId: '', branchId: 'b' }));
    expect(readStoredConnection(storage)).toBeNull();
  });

  it('clears stored connection', () => {
    const storage = buildStorage();
    writeStoredConnection(buildResponse(), storage);
    clearStoredConnection(storage);
    expect(readStoredConnection(storage)).toBeNull();
  });
});
