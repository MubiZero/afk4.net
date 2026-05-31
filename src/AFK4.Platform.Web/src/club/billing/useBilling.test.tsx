import { describe, it, expect, vi } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useBilling } from './useBilling';
import type { ClubApiClient } from '../../api/clubApi';
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

function makeClient(over: Partial<ClubApiClient>): ClubApiClient {
  return {
    getSubscription: vi.fn().mockResolvedValue(makeSubscription()),
    listInvoices: vi.fn().mockResolvedValue([] as Invoice[]),
    ...over
  } as unknown as ClubApiClient;
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
    const getSubscription = vi.fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValue(makeSubscription());
    const client = makeClient({ getSubscription });
    const { result } = renderHook(() => useBilling(client, 'o1'));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
