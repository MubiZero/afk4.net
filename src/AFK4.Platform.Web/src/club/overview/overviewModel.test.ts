import { describe, expect, it } from 'bun:test';
import { buildOverview } from './overviewModel';
import type { DeviceInventoryItem, OperatorDashboardSummary } from '@/api/types';

const summary: OperatorDashboardSummary = {
  organizationId: 'o', branchId: 'b', fromUtc: '', toUtc: '', generatedAtUtc: '',
  utilization: { totalSeats: 30, activeSessions: 19, endingSessions: 0, onlineDevices: 28, offlineDevices: 2, sessionStarts: 40, utilizationPercent: 63 },
  alertPressure: { pendingCommands: 0, failedCommands: 1, offlineDevices: 2, endingSessions: 0, totalAlerts: 3 },
  revenue: {
    posNetSales: { amount: 1250, currencyCode: 'TJS' },
    gameplayRevenue: { amount: 3000, currencyCode: 'TJS' },
    totalRevenue: { amount: 4250, currencyCode: 'TJS' },
    posCheckCount: 12, newPlayerCount: 4
  }
};

function device(p: Partial<DeviceInventoryItem>): DeviceInventoryItem {
  return {
    organizationId: 'o', branchId: 'b', deviceId: 'd', machineName: 'PC', agentVersion: '1', shellVersion: '1',
    enrolledAtUtc: '', lastHeartbeatAtUtc: null, isOnline: true, isLocked: false, seatId: null, seatName: null,
    zoneId: null, zoneName: null, activeCredentialCount: 0, installedAppCount: 0, pendingCommandCount: 0,
    failedCommandCount: 0, displayName: 'PC', role: 'gaming_pc', enrollmentState: 'approved', ...p
  };
}

function summaryWith(over: Partial<OperatorDashboardSummary['utilization']>): OperatorDashboardSummary {
  return {
    organizationId: 'o', branchId: 'b', fromUtc: '', toUtc: '', generatedAtUtc: '',
    utilization: { totalSeats: 99, activeSessions: 0, endingSessions: 0, onlineDevices: 6, offlineDevices: 2, sessionStarts: 0, utilizationPercent: 0, ...over },
    alertPressure: { pendingCommands: 0, failedCommands: 0, offlineDevices: 0, endingSessions: 0, totalAlerts: 0 },
    revenue: { posNetSales: { amount: 0, currencyCode: 'TJS' }, gameplayRevenue: { amount: 0, currencyCode: 'TJS' }, totalRevenue: { amount: 0, currencyCode: 'TJS' }, posCheckCount: 0, newPlayerCount: 0 }
  };
}

describe('buildOverview', () => {
  it('uses online+offline devices as the online denominator, not totalSeats', () => {
    const vm = buildOverview(summaryWith({}), [], []);
    expect(vm.kpis.devicesOnline).toEqual({ online: 6, total: 8 });
  });

  it('maps KPI values from the summary', () => {
    const vm = buildOverview(summary, [], []);
    expect(vm.kpis.devicesOnline).toEqual({ online: 28, total: 30 });
    expect(vm.kpis.activeSessions).toBe(19);
    expect(vm.kpis.utilizationPercent).toBe(63);
    expect(vm.kpis.revenueToday).toEqual({ amount: 4250, currencyCode: 'TJS' });
    expect(vm.kpis.attention).toBe(3);
    expect(vm.revenueBreakdown).toEqual([
      { key: 'gameplay', amount: 3000 },
      { key: 'pos', amount: 1250 }
    ]);
  });

  it('builds attention rows from offline + failed devices and pending count', () => {
    const vm = buildOverview(
      summary,
      [device({ deviceId: 'd1', displayName: 'ПК-14', isOnline: false }),
       device({ deviceId: 'd2', displayName: 'ПК-07', failedCommandCount: 2 }),
       device({ deviceId: 'd3', displayName: 'OK', isOnline: true })],
      [device({ deviceId: 'd9', displayName: 'Новый', enrollmentState: 'pending' })]
    );
    const ids = vm.attention.map(a => a.deviceId);
    expect(ids).toEqual(expect.arrayContaining(['d1', 'd2', 'd9']));
    expect(ids).not.toContain('d3');
    expect(vm.attention.find(a => a.deviceId === 'd1')?.kind).toBe('offline');
    expect(vm.attention.find(a => a.deviceId === 'd9')?.kind).toBe('pending');
  });
});
