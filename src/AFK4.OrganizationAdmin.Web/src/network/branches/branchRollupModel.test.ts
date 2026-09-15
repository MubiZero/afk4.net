import { describe, it, expect } from 'bun:test';
import { buildBranchRollup, type BranchRollupEntry } from './branchRollupModel';
import type { OperatorDashboardSummaryDto } from '../../operatorApiClients';

const zeroMoney = { currencyCode: 'TJS', minorUnits: 0 };

// Operator dashboard-summary money fields are MoneyDto over the wire: { currencyCode, minorUnits }
// (see AFK4.Shared.Contracts/Billing/MoneyDto.cs) — NOT amountMinorUnits.
const summary: OperatorDashboardSummaryDto = {
  organizationId: 'org-1',
  branchId: 'branch-1',
  fromUtc: '2026-01-01T00:00:00Z',
  toUtc: '2026-01-02T00:00:00Z',
  generatedAtUtc: '2026-01-02T00:00:00Z',
  shift: { shiftId: null, state: 'closed', openedAtUtc: null, openedByStaffUserId: null, expectedCash: zeroMoney },
  revenue: {
    posNetSales: zeroMoney,
    gameplayRevenue: zeroMoney,
    totalRevenue: { minorUnits: 15000, currencyCode: 'TJS' },
    posCheckCount: 0,
    newPlayerCount: 0
  },
  utilization: {
    totalSeats: 4, activeSessions: 2, endingSessions: 0, onlineDevices: 3,
    offlineDevices: 1, sessionStarts: 2, utilizationPercent: 50
  },
  alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: 1, endingSessions: 0, totalAlerts: 1 },
  reservations: { activeReservations: 0, availableSlots: 4, source: 'branch' },
  focusQueue: [],
  recentPayments: []
};

describe('buildBranchRollup', () => {
  it('aggregates KPIs across branches', () => {
    const entries: BranchRollupEntry[] = [
      { branchId: 'a', name: 'A', city: 'X', summary },
      { branchId: 'b', name: 'B', city: 'Y', summary }
    ];
    const vm = buildBranchRollup(entries);
    expect(vm.totals.branches).toBe(2);
    expect(vm.totals.devicesOnline).toEqual({ online: 6, total: 8 });
    expect(vm.totals.activeSessions).toBe(4);
    expect(vm.totals.revenue.minorUnits).toBe(30000);
    expect(vm.totals.attention).toBe(2);
  });

  it('keeps a failed branch as a row with null kpis and excludes it from totals', () => {
    const entries: BranchRollupEntry[] = [
      { branchId: 'a', name: 'A', city: 'X', summary },
      { branchId: 'b', name: 'B', city: 'Y', summary: null }
    ];
    const vm = buildBranchRollup(entries);
    expect(vm.rows.find((r) => r.branchId === 'b')!.kpis).toBeNull();
    expect(vm.totals.branches).toBe(2);
    expect(vm.totals.activeSessions).toBe(2); // only branch A counted
  });
});
