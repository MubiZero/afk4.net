import { act, cleanup, renderHook, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import type { OperatorBackendContext } from './operatorTypes';
import {
  usePostAuthShiftGate,
  type PostAuthShiftClient
} from './usePostAuthShiftGate';

afterEach(cleanup);

const t = ((key: string) => key) as never;

function createBackend(
  permissions: string[],
  branchId = 'branch-a'
): OperatorBackendContext {
  return {
    branchId,
    config: {
      platformBaseUrl: 'https://platform.test/',
      currencyCode: 'TJS',
      organizationId: 'org-1',
      branchId,
      runtime: 'browser-dev',
      shellMode: 'vite-dev'
    },
    session: {
      staffUserId: 'staff-1',
      organizationId: 'org-1',
      displayName: 'Operator',
      accessToken: 'token',
      accessTokenExpiresAtUtc: '2026-07-15T10:00:00Z',
      refreshTokenExpiresAtUtc: '2026-07-16T10:00:00Z',
      branchIds: [branchId],
      activeBranchId: branchId,
      permissions
    }
  } as OperatorBackendContext;
}

function createClient(overrides: Partial<PostAuthShiftClient> = {}) {
  return {
    getCurrentShift: mock(async () => ({ shiftId: 'shift-1' })),
    openShift: mock(async () => ({ shiftId: 'shift-1' })),
    ...overrides
  } satisfies PostAuthShiftClient;
}

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

describe('usePostAuthShiftGate', () => {
  it('skips staff without shifts.open', () => {
    const client = createClient();
    const { result } = renderHook(() => usePostAuthShiftGate({
      authStatus: 'signed-in',
      backend: createBackend(['floor_map.view']),
      t,
      client
    }));

    expect(result.current.status).toBe('not-required');
    expect(client.getCurrentShift).not.toHaveBeenCalled();
  });

  it('exposes checking synchronously and requires an empty shift', async () => {
    const client = createClient({ getCurrentShift: mock(async () => null) });
    const { result } = renderHook(() => usePostAuthShiftGate({
      authStatus: 'signed-in',
      backend: createBackend(['shifts.open', 'shifts.view']),
      t,
      client
    }));

    expect(result.current.status).toBe('checking');
    await waitFor(() => expect(result.current.status).toBe('required'));
  });

  it('releases an existing shift', async () => {
    const client = createClient();
    const { result } = renderHook(() => usePostAuthShiftGate({
      authStatus: 'signed-in',
      backend: createBackend(['shifts.open', 'shifts.view']),
      t,
      client
    }));

    await waitFor(() => expect(result.current.status).toBe('ready'));
  });

  it('retries a failed authoritative check', async () => {
    const getCurrentShift = mock(async () => null as unknown | null);
    getCurrentShift.mockRejectedValueOnce(new Error('network down'));
    const client = createClient({ getCurrentShift });
    const { result } = renderHook(() => usePostAuthShiftGate({
      authStatus: 'signed-in',
      backend: createBackend(['shifts.open']),
      t,
      client
    }));

    await waitFor(() => expect(result.current.status).toBe('failed'));
    expect(result.current.error).toContain('network down');
    expect(result.current.failureKind).toBe('check');

    act(() => result.current.retry());

    await waitFor(() => expect(result.current.status).toBe('required'));
    expect(getCurrentShift).toHaveBeenCalledTimes(2);
  });

  it('ignores a stale response after the active branch changes', async () => {
    const branchA = deferred<unknown | null>();
    const getCurrentShift = mock((branchId: string) =>
      branchId === 'branch-a'
        ? branchA.promise
        : Promise.resolve({ shiftId: 'shift-b' })
    );
    const client = createClient({ getCurrentShift });
    const { result, rerender } = renderHook(
      ({ backend }) => usePostAuthShiftGate({ authStatus: 'signed-in', backend, t, client }),
      { initialProps: { backend: createBackend(['shifts.open'], 'branch-a') } }
    );

    expect(result.current.status).toBe('checking');
    rerender({ backend: createBackend(['shifts.open'], 'branch-b') });
    await waitFor(() => expect(result.current.status).toBe('ready'));

    await act(async () => branchA.resolve(null));

    expect(result.current.status).toBe('ready');
  });

  it('opens a shift with the supplied idempotent request', async () => {
    const client = createClient({ getCurrentShift: mock(async () => null) });
    const { result } = renderHook(() => usePostAuthShiftGate({
      authStatus: 'signed-in',
      backend: createBackend(['shifts.open']),
      t,
      client
    }));
    await waitFor(() => expect(result.current.status).toBe('required'));

    const request = {
      organizationId: 'org-1',
      startingCash: { currencyCode: 'TJS', minorUnits: 10000 },
      openingNote: 'Утренняя смена',
      idempotencyKey: 'shift-open:test'
    };
    await act(async () => result.current.openShift(request));

    expect(client.openShift).toHaveBeenCalledWith('branch-a', request);
    expect(result.current.status).toBe('ready');
  });

  it('releases the gate when another employee opened the shift first', async () => {
    const getCurrentShift = mock(async () => null as unknown | null);
    getCurrentShift.mockResolvedValueOnce(null);
    getCurrentShift.mockResolvedValueOnce({ shiftId: 'shift-concurrent' });
    const client = createClient({
      getCurrentShift,
      openShift: mock(async () => { throw new Error('already open'); })
    });
    const { result } = renderHook(() => usePostAuthShiftGate({
      authStatus: 'signed-in',
      backend: createBackend(['shifts.open']),
      t,
      client
    }));
    await waitFor(() => expect(result.current.status).toBe('required'));

    await act(async () => result.current.openShift({
      organizationId: 'org-1',
      startingCash: { currencyCode: 'TJS', minorUnits: 0 },
      openingNote: '',
      idempotencyKey: 'shift-open:race'
    }));

    expect(result.current.status).toBe('ready');
    expect(result.current.error).toBeNull();
  });

  it('marks an unreconciled open failure without losing the required workflow', async () => {
    const getCurrentShift = mock(async () => null);
    const client = createClient({
      getCurrentShift,
      openShift: mock(async () => { throw new Error('open failed'); })
    });
    const { result } = renderHook(() => usePostAuthShiftGate({
      authStatus: 'signed-in',
      backend: createBackend(['shifts.open']),
      t,
      client
    }));
    await waitFor(() => expect(result.current.status).toBe('required'));

    await act(async () => result.current.openShift({
      organizationId: 'org-1',
      startingCash: { currencyCode: 'TJS', minorUnits: 0 },
      openingNote: '',
      idempotencyKey: 'shift-open:failed'
    }));

    expect(result.current.status).toBe('failed');
    expect(result.current.failureKind).toBe('open');
    expect(result.current.error).toContain('open failed');
  });
});
