import { describe, it, expect, mock } from 'bun:test';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useBilling } from './useBilling';
import type { BillingApi } from '../../api/clients/billing';
import type { Invoice, TenantSubscription } from '../../api/types';

function makeSubscription(): TenantSubscription {
  return {
    tenantSubscriptionId: 's1', organizationId: 'o1', planCode: 'starter', status: 'active',
    currentPeriodStartUtc: '2026-05-01T00:00:00Z', currentPeriodEndUtc: '2026-06-01T00:00:00Z',
    nextInvoiceUtc: '2026-06-01T00:00:00Z', amountMinorUnits: 290000, currencyCode: 'RUB',
    billingInterval: 'monthly', cancelAtPeriodEnd: false,
    createdAtUtc: '2026-05-01T00:00:00Z', updatedAtUtc: '2026-05-01T00:00:00Z'
  };
}

function makeClient(over: Partial<BillingApi>): BillingApi {
  return {
    getSubscription: mock().mockResolvedValue(makeSubscription()),
    listInvoices: mock().mockResolvedValue([] as Invoice[]),
    ...over
  } as unknown as BillingApi;
}

describe('useBilling', () => {
  it('reaches ready with subscription + invoices', async () => {
    const client = makeClient({});
    const { result } = renderHook(() => useBilling(client, 'o1'));
    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') {
      expect(result.current.subscription.planCode).toBe('starter');
      expect(result.current.invoices).toEqual([]);
    }
  });

  it('reaches error and can retry', async () => {
    const getSubscription = mock()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValue(makeSubscription());
    const client = makeClient({ getSubscription });
    const { result } = renderHook(() => useBilling(client, 'o1'));
    await waitFor(() => expect(result.current.status).toBe('error'));
    const errorState = result.current;
    if (errorState.status === 'error') {
      act(() => errorState.retry());
    }
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
