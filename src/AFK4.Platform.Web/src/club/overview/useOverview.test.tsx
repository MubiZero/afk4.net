import { describe, expect, it, mock } from 'bun:test';
import { renderHook, waitFor } from '@testing-library/react';
import { useOverview } from './useOverview';

const okSummary = {
  utilization: { totalSeats: 30, activeSessions: 19, endingSessions: 0, onlineDevices: 28, offlineDevices: 2, sessionStarts: 1, utilizationPercent: 63 },
  alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: 2, endingSessions: 0, totalAlerts: 2 },
  revenue: { posNetSales: { amount: 1, currencyCode: 'TJS' }, gameplayRevenue: { amount: 2, currencyCode: 'TJS' }, totalRevenue: { amount: 3, currencyCode: 'TJS' }, posCheckCount: 0, newPlayerCount: 0 }
};

function fakeClient(over: Partial<Record<'getDashboardSummary' | 'listDevices' | 'listPendingDevices', unknown>> = {}) {
  return {
    getDashboardSummary: mock().mockResolvedValue(okSummary),
    listDevices: mock().mockResolvedValue([]),
    listPendingDevices: mock().mockResolvedValue([]),
    ...over
  } as never;
}

describe('useOverview', () => {
  it('reaches ready with a view-model', async () => {
    const { result } = renderHook(() => useOverview(fakeClient(), 'b1'));
    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') {
      expect(result.current.data.kpis.activeSessions).toBe(19);
    }
  });

  it('surfaces an error state and supports retry', async () => {
    const failing = fakeClient({ getDashboardSummary: mock().mockRejectedValue(new Error('boom')) });
    const { result } = renderHook(() => useOverview(failing, 'b1'));
    await waitFor(() => expect(result.current.status).toBe('error'));
    (failing as { getDashboardSummary: ReturnType<typeof mock> }).getDashboardSummary.mockResolvedValue(okSummary);
    result.current.retry();
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
