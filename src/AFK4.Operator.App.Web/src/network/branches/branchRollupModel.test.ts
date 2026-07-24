import { describe, it, expect } from 'bun:test';
import { buildBranchRollup, type BranchRollupEntry } from './branchRollupModel';

// Operator dashboard-summary money fields are MoneyDto over the wire: { currencyCode, minorUnits }
// (see AFK4.Shared.Contracts/Billing/MoneyDto.cs) — NOT amountMinorUnits.
const summary = {
  utilization: { onlineDevices: 3, offlineDevices: 1, activeSessions: 2 },
  revenue: { totalRevenue: { minorUnits: 15000, currencyCode: 'TJS' } },
  alertPressure: { totalAlerts: 1 }
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
