import type { Money, OperatorDashboardSummary } from '@/api/types';

export interface BranchKpis {
  devicesOnline: { online: number; total: number };
  activeSessions: number;
  revenueToday: Money;
  attention: number;
}

export interface BranchRollupRow {
  branchId: string;
  name: string;
  city: string;
  kpis: BranchKpis | null; // null => this branch failed to load
}

export interface BranchRollupTotals {
  branches: number;
  devicesOnline: { online: number; total: number };
  activeSessions: number;
  revenue: Money;
  attention: number;
}

export interface BranchRollupViewModel {
  rows: BranchRollupRow[];
  totals: BranchRollupTotals;
}

export interface BranchRollupEntry {
  branchId: string;
  name: string;
  city: string;
  summary: OperatorDashboardSummary | null;
}

function toKpis(summary: OperatorDashboardSummary): BranchKpis {
  return {
    devicesOnline: {
      online: summary.utilization.onlineDevices,
      total: summary.utilization.onlineDevices + summary.utilization.offlineDevices
    },
    activeSessions: summary.utilization.activeSessions,
    revenueToday: summary.revenue.totalRevenue,
    attention: summary.alertPressure.totalAlerts
  };
}

export function buildBranchRollup(entries: BranchRollupEntry[]): BranchRollupViewModel {
  const rows: BranchRollupRow[] = entries.map(e => ({
    branchId: e.branchId,
    name: e.name,
    city: e.city,
    kpis: e.summary === null ? null : toKpis(e.summary)
  }));

  let online = 0;
  let total = 0;
  let activeSessions = 0;
  let attention = 0;
  let revenueAmount = 0;
  let currencyCode = '';
  for (const row of rows) {
    if (row.kpis === null) continue;
    online += row.kpis.devicesOnline.online;
    total += row.kpis.devicesOnline.total;
    activeSessions += row.kpis.activeSessions;
    attention += row.kpis.attention;
    revenueAmount += row.kpis.revenueToday.amount;
    if (currencyCode === '') currencyCode = row.kpis.revenueToday.currencyCode;
  }

  return {
    rows,
    totals: {
      branches: rows.length,
      devicesOnline: { online, total },
      activeSessions,
      revenue: { amount: revenueAmount, currencyCode: currencyCode === '' ? 'RUB' : currencyCode },
      attention
    }
  };
}
