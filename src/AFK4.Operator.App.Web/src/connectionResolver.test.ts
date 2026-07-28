import { describe, expect, it, mock } from 'bun:test';
import {
  BridgeOperatorConnectionStorage,
  ConnectionResolutionError,
  ConnectionResolver,
  LocalStorageOperatorConnectionStorage,
  OperatorOrganizationStatus,
  clearStoredConnection,
  readStoredConnection,
  writeStoredConnection,
  type BridgeRequestSender,
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
    organizationStatus: OperatorOrganizationStatus.Active,
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
    const fetchImpl = mock(async () =>
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
    const fetchImpl = mock(async () =>
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
    const fetchImpl = mock(async () =>
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
    const fetchImpl = mock(async () => new Response('upstream-error', { status: 502 }));
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

describe('LocalStorageOperatorConnectionStorage', () => {
  it('round-trips save / load / loadSync / clear through the underlying StorageLike', async () => {
    const storage = buildStorage();
    const subject = new LocalStorageOperatorConnectionStorage(storage);

    expect(await subject.load()).toBeNull();
    expect(subject.loadSync()).toBeNull();

    const stored = await subject.save(buildResponse(), new Date('2026-05-23T12:00:00Z'));
    expect(stored.storedAtUtc).toBe('2026-05-23T12:00:00.000Z');
    expect(subject.loadSync()).toEqual(stored);
    expect(await subject.load()).toEqual(stored);

    await subject.clear();
    expect(await subject.load()).toBeNull();
    expect(subject.loadSync()).toBeNull();
  });
});

describe('BridgeOperatorConnectionStorage', () => {
  function recordingSender(initial: Record<string, unknown> | null = null) {
    const calls: Array<{ type: string; payload: unknown }> = [];
    let state: Record<string, unknown> | null = initial;
    const sender: BridgeRequestSender = async <T,>(type: string, payload?: unknown) => {
      calls.push({ type, payload });
      if (type === 'connection:loadConnection') {
        return state as unknown as T;
      }
      if (type === 'connection:saveConnection') {
        state = payload as Record<string, unknown>;
        return state as unknown as T;
      }
      if (type === 'connection:clearConnection') {
        state = null;
        return ({ cleared: true }) as unknown as T;
      }
      throw new Error(`Unexpected bridge request: ${type}`);
    };
    return { sender, calls, state: () => state };
  }

  it('loadSync always returns null because the bridge round-trip is asynchronous', () => {
    const { sender } = recordingSender();
    const subject = new BridgeOperatorConnectionStorage(sender);
    expect(subject.loadSync()).toBeNull();
  });

  it('load returns the bridge payload normalised to a ResolvedOperatorConnection', async () => {
    const { sender, calls } = recordingSender({
      organizationId: 'org-1',
      organizationSlug: 'org-slug',
      organizationName: 'Org Name',
      branchId: 'branch-1',
      branchSlug: 'branch-slug',
      branchName: 'Branch Name',
      branchCity: 'City',
      storedAtUtc: '2026-05-23T11:00:00Z'
    });
    const subject = new BridgeOperatorConnectionStorage(sender);
    const loaded = await subject.load();
    expect(loaded?.organizationId).toBe('org-1');
    expect(loaded?.branchSlug).toBe('branch-slug');
    expect(calls[0].type).toBe('connection:loadConnection');
  });

  it('load returns null when the bridge returns null or an empty id', async () => {
    const empty = recordingSender(null);
    expect(await new BridgeOperatorConnectionStorage(empty.sender).load()).toBeNull();

    const missingBranch = recordingSender({ organizationId: 'org-1', branchId: '', branchSlug: 'x' });
    expect(await new BridgeOperatorConnectionStorage(missingBranch.sender).load()).toBeNull();
  });

  it('save serializes the request, stamps storedAtUtc, and returns the normalised echo', async () => {
    const { sender, calls } = recordingSender();
    const subject = new BridgeOperatorConnectionStorage(sender);
    const stored = await subject.save(
      {
        organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
        organizationSlug: 'demo-club',
        organizationName: 'Demo Club',
        organizationStatus: OperatorOrganizationStatus.Active,
        organizationStatusReason: null,
        branchId: 'acfc0212-967f-4d84-94be-9003387b09c2',
        branchSlug: 'main',
        branchName: 'Main',
        branchCity: 'Dushanbe',
        source: 'slug'
      },
      new Date('2026-05-23T15:00:00Z')
    );

    expect(calls).toHaveLength(1);
    expect(calls[0].type).toBe('connection:saveConnection');
    const payload = calls[0].payload as Record<string, unknown>;
    expect(payload.storedAtUtc).toBe('2026-05-23T15:00:00.000Z');
    expect(payload.organizationSlug).toBe('demo-club');
    expect(stored.organizationId).toBe('0c04d6c0-bfa8-4e26-9263-fc0d307d0f08');
    expect(stored.storedAtUtc).toBe('2026-05-23T15:00:00.000Z');
  });

  it('clear posts connection:clearConnection and drops the stored state', async () => {
    const { sender, calls, state } = recordingSender({
      organizationId: 'org-1',
      organizationSlug: 'x',
      organizationName: 'x',
      branchId: 'branch-1',
      branchSlug: 'x',
      branchName: 'x',
      branchCity: 'x',
      storedAtUtc: 'x'
    });
    const subject = new BridgeOperatorConnectionStorage(sender);
    await subject.clear();
    expect(calls[0].type).toBe('connection:clearConnection');
    expect(state()).toBeNull();
  });
});
