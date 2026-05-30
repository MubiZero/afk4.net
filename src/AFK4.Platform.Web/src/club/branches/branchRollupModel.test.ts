import { it, expect } from 'vitest';
import type { OperatorDashboardSummary } from '@/api/types';
import { buildBranchRollup, type BranchRollupEntry } from './branchRollupModel';

function summary(online: number, offline: number, sessions: number, alerts: number, revenue: number): OperatorDashboardSummary {
  return {
    organizationId: 'org', branchId: 'b', fromUtc: '', toUtc: '', generatedAtUtc: '',
    utilization: { totalSeats: 10, activeSessions: sessions, endingSessions: 0, onlineDevices: online, offlineDevices: offline, sessionStarts: 0, utilizationPercent: 0 },
    alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: offline, endingSessions: 0, totalAlerts: alerts },
    revenue: { posNetSales: { amount: 0, currencyCode: 'RUB' }, gameplayRevenue: { amount: 0, currencyCode: 'RUB' }, totalRevenue: { amount: revenue, currencyCode: 'RUB' }, posCheckCount: 0, newPlayerCount: 0 }
  };
}

it('maps a branch summary into KPI fields', () => {
  const entries: BranchRollupEntry[] = [{ branchId: 'a', name: 'Центр', city: 'Москва', summary: summary(5, 1, 2, 3, 1000) }];
  const vm = buildBranchRollup(entries);
  expect(vm.rows[0]).toEqual({
    branchId: 'a', name: 'Центр', city: 'Москва',
    kpis: { devicesOnline: { online: 5, total: 6 }, activeSessions: 2, revenueToday: { amount: 1000, currencyCode: 'RUB' }, attention: 3 }
  });
});

it('sums totals across loaded branches and counts all rows', () => {
  const entries: BranchRollupEntry[] = [
    { branchId: 'a', name: 'A', city: '', summary: summary(5, 1, 2, 3, 1000) },
    { branchId: 'b', name: 'B', city: '', summary: summary(2, 0, 1, 4, 500) }
  ];
  const vm = buildBranchRollup(entries);
  expect(vm.totals).toEqual({
    branches: 2,
    devicesOnline: { online: 7, total: 8 },
    activeSessions: 3,
    revenue: { amount: 1500, currencyCode: 'RUB' },
    attention: 7
  });
});

it('counts a failed branch in the count but excludes it from totals and marks its kpis null', () => {
  const entries: BranchRollupEntry[] = [
    { branchId: 'a', name: 'A', city: '', summary: summary(5, 1, 2, 3, 1000) },
    { branchId: 'b', name: 'B', city: '', summary: null }
  ];
  const vm = buildBranchRollup(entries);
  expect(vm.rows[1].kpis).toBeNull();
  expect(vm.totals.branches).toBe(2);
  expect(vm.totals.devicesOnline).toEqual({ online: 5, total: 6 });
  expect(vm.totals.revenue).toEqual({ amount: 1000, currencyCode: 'RUB' });
});

it('falls back to a valid currency code when no branch summary loads', () => {
  const vm = buildBranchRollup([]);
  expect(vm.totals.branches).toBe(0);
  expect(vm.totals.revenue).toEqual({ amount: 0, currencyCode: 'RUB' });
});
