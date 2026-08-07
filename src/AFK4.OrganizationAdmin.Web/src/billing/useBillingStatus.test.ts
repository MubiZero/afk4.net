import { describe, it, expect, mock, afterEach, afterAll, jest } from 'bun:test';
import { act, renderHook, waitFor, cleanup } from '@testing-library/react';
import { shellOperationalRefreshMs } from '../operatorHelpers';

afterEach(() => {
  cleanup();
  jest.useRealTimers();
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

    // Fake timers must be active BEFORE the hook mounts: the effect's window.setInterval has to
    // register as a fake timer from the start, or advancing fake time later never fires it.
    jest.useFakeTimers();
    const { result } = renderHook(() => useBillingStatus('signed-in', authorizedSession, config));

    await act(async () => { await Promise.resolve(); });
    expect(result.current).toEqual(okStatus);

    await act(async () => {
      jest.advanceTimersByTime(shellOperationalRefreshMs);
      await Promise.resolve();
    });

    expect(result.current?.inArrears).toBe(false);
    expect(getBillingStatus).toHaveBeenCalledTimes(2);
  });
});
