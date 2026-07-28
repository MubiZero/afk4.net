import { describe, expect, it, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import { useBillingMetrics } from './useBillingMetrics';

const metrics = {
  mrrMinorUnits: 580000, currencyCode: 'RUB', activeSubscriptions: 2,
  outstandingMinorUnits: 290000, outstandingCount: 1, overdueMinorUnits: 0, overdueCount: 0
};

describe('useBillingMetrics', () => {
  it('reaches ready with metrics', async () => {
    const client = { getBillingMetrics: mock().mockResolvedValue(metrics) } as never;
    const { result } = renderHook(() => useBillingMetrics(client));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') expect(result.current.data.mrrMinorUnits).toBe(580000);
  });
});
