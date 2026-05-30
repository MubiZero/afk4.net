import type { DeviceInventoryItem, Money, OperatorDashboardSummary } from '@/api/types';

export type AttentionKind = 'offline' | 'failed' | 'pending';
export interface AttentionRow { deviceId: string; name: string; kind: AttentionKind; }
export interface RevenueSlice { key: 'gameplay' | 'pos'; amount: number; }

export interface OverviewViewModel {
  kpis: {
    devicesOnline: { online: number; total: number };
    activeSessions: number;
    utilizationPercent: number;
    revenueToday: Money;
    attention: number;
  };
  revenueBreakdown: RevenueSlice[];
  attention: AttentionRow[];
}

export function buildOverview(
  summary: OperatorDashboardSummary,
  devices: DeviceInventoryItem[],
  pending: DeviceInventoryItem[]
): OverviewViewModel {
  const attention: AttentionRow[] = [];
  for (const d of devices) {
    if (!d.isOnline) attention.push({ deviceId: d.deviceId, name: d.displayName, kind: 'offline' });
    else if (d.failedCommandCount > 0) attention.push({ deviceId: d.deviceId, name: d.displayName, kind: 'failed' });
  }
  for (const p of pending) attention.push({ deviceId: p.deviceId, name: p.displayName, kind: 'pending' });

  return {
    kpis: {
      devicesOnline: { online: summary.utilization.onlineDevices, total: summary.utilization.onlineDevices + summary.utilization.offlineDevices },
      activeSessions: summary.utilization.activeSessions,
      utilizationPercent: summary.utilization.utilizationPercent,
      revenueToday: summary.revenue.totalRevenue,
      attention: summary.alertPressure.totalAlerts
    },
    revenueBreakdown: [
      { key: 'gameplay', amount: summary.revenue.gameplayRevenue.amount },
      { key: 'pos', amount: summary.revenue.posNetSales.amount }
    ],
    attention
  };
}
