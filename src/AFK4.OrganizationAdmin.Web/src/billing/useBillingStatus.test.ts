import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { renderHook, waitFor, cleanup } from '@testing-library/react';

afterEach(() => {
  cleanup();
});

const okStatus = {
  inArrears: true,
  outstandingMinorUnits: 450000,
  currencyCode: 'TJS',
  oldestOverdueInvoiceNumber: 42,
  daysOverdue: 5,
  graceUntilUtc: null
};

const getBillingStatus = mock(async () => okStatus);

// operatorHelpers exports ~40 things and is imported across the app; mock.module replaces the
// whole module for the rest of the bun process, so spread the real module and override only
// what this file needs, then restore it in afterAll — see ReviewWorkspace.test.tsx for the same
// pattern and src/test/setup.ts for the snapshot this restore reads from.
const actualHelpers = await import('../operatorHelpers');
mock.module('../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    orgBilling: { getBillingStatus }
  })
}));

afterAll(() => {
  mock.module('../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

const config = { platformBaseUrl: 'x', currencyCode: 'TJS' } as never;

function session(overrides: Record<string, unknown> = {}) {
  return {
    organizationId: 'org1',
    permissions: ['organization.billing.subscription.view'],
    ...overrides
  } as never;
}

describe('useBillingStatus', () => {
  it('загружает статус для авторизованного сотрудника с правом viewSubscription', async () => {
    getBillingStatus.mockClear();
    const { useBillingStatus } = await import('./useBillingStatus');
    // Same session reference across re-renders — a fresh object literal per render would change
    // the effect's dependency identity and re-trigger the fetch after the state update below.
    const authorizedSession = session();
    const { result } = renderHook(() => useBillingStatus('signed-in', authorizedSession, config));

    await waitFor(() => expect(result.current).toEqual(okStatus));
    expect(getBillingStatus).toHaveBeenCalledTimes(1);
  });

  it('не запрашивает статус без права viewSubscription', async () => {
    getBillingStatus.mockClear();
    const { useBillingStatus } = await import('./useBillingStatus');
    const unauthorizedSession = session({ permissions: [] });
    const { result } = renderHook(() => useBillingStatus('signed-in', unauthorizedSession, config));

    expect(result.current).toBeNull();
    expect(getBillingStatus).not.toHaveBeenCalled();
  });

  it('не запрашивает статус, пока пользователь не авторизован', async () => {
    getBillingStatus.mockClear();
    const { useBillingStatus } = await import('./useBillingStatus');
    const { result } = renderHook(() => useBillingStatus('signed-out', null, config));

    expect(result.current).toBeNull();
    expect(getBillingStatus).not.toHaveBeenCalled();
  });

  it('перезапрашивает статус по интервалу, а не только при монтировании', async () => {
    getBillingStatus.mockClear();
    let call = 0;
    getBillingStatus.mockImplementation(async () => {
      call += 1;
      // Клуб оплатил счёт между опросами — второй ответ платформы уже без долга.
      return call === 1 ? okStatus : { ...okStatus, inArrears: false };
    });
    const { useBillingStatus } = await import('./useBillingStatus');
    const authorizedSession = session();

    // Real timers with a short interval, not window.setInterval mocked via fake timers: fake
    // timers + React 19's act scheduling under happy-dom has hung in CI for 5000ms (see git log
    // for this file) — something in that combination needs a genuine timer tick that fake timers
    // never provide. 150ms (well above waitFor's 50ms default poll granularity, so the first,
    // briefly-true state below is never skipped by a poll) plus waitFor (the project's standard
    // async-condition wait, see useBranchDirectory.test.ts) is slower by ~150ms but deterministic.
    const { result, unmount } = renderHook(() =>
      useBillingStatus('signed-in', authorizedSession, config, 150));

    await waitFor(() => expect(result.current).toEqual(okStatus));
    await waitFor(() => expect(result.current?.inArrears).toBe(false));
    // Unmount before asserting the call count: the interval keeps firing in the background (every
    // 150ms, same okStatus each time), and waitFor's own polling can otherwise race an extra tick
    // in between the state settling and this assertion running.
    unmount();

    // >=2 rather than ===2: unmount's clearInterval races the in-flight poll that produced the
    // updated state above (see App.test.tsx for the same >= pattern with a real-timer poll).
    expect(getBillingStatus.mock.calls.length).toBeGreaterThanOrEqual(2);
  });
});
