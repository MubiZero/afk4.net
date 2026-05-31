import { it, expect, mock } from 'bun:test';
import { waitFor, renderHook } from '@testing-library/react';
import type { OperatorDashboardSummary } from '@/api/types';
import { useBranchRollup } from './useBranchRollup';

function summary(id: string, online: number): OperatorDashboardSummary {
  return {
    organizationId: 'org', branchId: id, fromUtc: '', toUtc: '', generatedAtUtc: '',
    utilization: { totalSeats: 10, activeSessions: 1, endingSessions: 0, onlineDevices: online, offlineDevices: 0, sessionStarts: 0, utilizationPercent: 0 },
    alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: 0, endingSessions: 0, totalAlerts: 0 },
    revenue: { posNetSales: { amount: 0, currencyCode: 'RUB' }, gameplayRevenue: { amount: 0, currencyCode: 'RUB' }, totalRevenue: { amount: 0, currencyCode: 'RUB' }, posCheckCount: 0, newPlayerCount: 0 }
  };
}

function client() {
  return {
    getBranchProfile: mock(async (id: string) => ({ organizationId: 'org', branchId: id, name: id.toUpperCase(), city: 'Москва', createdAtUtc: '' })),
    getDashboardSummary: mock(async (id: string) => summary(id, id === 'a' ? 5 : 3))
  };
}

it('loads each branch and builds a rollup', async () => {
  const { result } = renderHook(() => useBranchRollup(client() as never, ['a', 'b'], 'Филиал'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  expect(result.current.data.rows.map(r => r.name)).toEqual(['A', 'B']);
  expect(result.current.data.totals.devicesOnline).toEqual({ online: 8, total: 8 });
});

it('marks a branch whose summary fails as kpis null and uses the unnamed fallback when its profile fails', async () => {
  const c = client();
  c.getDashboardSummary = mock(async (id: string) => {
    if (id === 'b') throw new Error('boom');
    return summary(id, 5);
  });
  c.getBranchProfile = mock(async (id: string) => {
    if (id === 'b') throw new Error('boom');
    return { organizationId: 'org', branchId: id, name: id.toUpperCase(), city: 'Москва', createdAtUtc: '' };
  });
  const { result } = renderHook(() => useBranchRollup(c as never, ['a', 'b'], 'Филиал'));
  await waitFor(() => expect(result.current.status).toBe('ready'));
  if (result.current.status !== 'ready') throw new Error('not ready');
  const b = result.current.data.rows.find(r => r.branchId === 'b');
  expect(b?.kpis).toBeNull();
  expect(b?.name).toBe('Филиал');
});
