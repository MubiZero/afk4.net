import type { OperatorDashboardSummaryDto } from '../../operatorApiClients';

export interface BranchKpis {
  devicesOnline: { online: number; total: number };
  activeSessions: number;
  revenue: { minorUnits: number; currencyCode: string };
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
  revenue: { minorUnits: number; currencyCode: string };
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
  summary: OperatorDashboardSummaryDto | null;
}

function toKpis(summary: OperatorDashboardSummaryDto): BranchKpis {
  const { utilization, revenue, alertPressure } = summary;
  const online = utilization.onlineDevices;
  return {
    devicesOnline: { online, total: online + utilization.offlineDevices },
    activeSessions: utilization.activeSessions,
    revenue: {
      minorUnits: revenue.totalRevenue.minorUnits,
      currencyCode: revenue.totalRevenue.currencyCode
    },
    attention: alertPressure.totalAlerts
  };
}

export function buildBranchRollup(entries: BranchRollupEntry[]): BranchRollupViewModel {
  const rows: BranchRollupRow[] = entries.map((e) => ({
    branchId: e.branchId,
    name: e.name,
    city: e.city,
    kpis: e.summary === null ? null : toKpis(e.summary)
  }));

  let online = 0;
  let total = 0;
  let activeSessions = 0;
  let attention = 0;
  let revenueMinorUnits = 0;
  let currencyCode = '';
  for (const row of rows) {
    if (row.kpis === null) continue;
    online += row.kpis.devicesOnline.online;
    total += row.kpis.devicesOnline.total;
    activeSessions += row.kpis.activeSessions;
    attention += row.kpis.attention;
    revenueMinorUnits += row.kpis.revenue.minorUnits;
    if (currencyCode === '' && row.kpis.revenue.currencyCode !== '') currencyCode = row.kpis.revenue.currencyCode;
  }

  return {
    rows,
    totals: {
      branches: rows.length,
      devicesOnline: { online, total },
      activeSessions,
      revenue: { minorUnits: revenueMinorUnits, currencyCode: currencyCode === '' ? 'TJS' : currencyCode },
      attention
    }
  };
}
