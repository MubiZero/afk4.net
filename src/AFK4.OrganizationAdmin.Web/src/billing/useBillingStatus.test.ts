import { describe, it, expect, mock, afterEach } from 'bun:test';
import { renderHook, waitFor, cleanup } from '@testing-library/react';

afterEach(() => cleanup());

const okStatus = {
  inArrears: true,
  outstandingMinorUnits: 450000,
  currencyCode: 'TJS',
  oldestOverdueInvoiceNumber: 42,
  daysOverdue: 5,
  graceUntilUtc: null
};

const getBillingStatus = mock(async () => okStatus);

mock.module('../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgBilling: { getBillingStatus }
  })
}));

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
});
