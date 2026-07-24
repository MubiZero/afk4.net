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
  summary: Record<string, unknown> | null;
}

function num(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}

function obj(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
}

// Operator dashboard-summary DTO is loosely typed (Record<string, unknown>) on the client, so every
// field is read defensively. Money fields are MoneyDto over the wire — { currencyCode, minorUnits }
// (see AFK4.Shared.Contracts/Billing/MoneyDto.cs) — not { amount } and not amountMinorUnits.
function toKpis(summary: Record<string, unknown>): BranchKpis {
  const utilization = obj(summary.utilization);
  const revenue = obj(summary.revenue);
  const totalRevenue = obj(revenue.totalRevenue);
  const alertPressure = obj(summary.alertPressure);
  const online = num(utilization.onlineDevices);
  const offline = num(utilization.offlineDevices);
  return {
    devicesOnline: { online, total: online + offline },
    activeSessions: num(utilization.activeSessions),
    revenue: {
      minorUnits: num(totalRevenue.minorUnits),
      currencyCode: typeof totalRevenue.currencyCode === 'string' ? totalRevenue.currencyCode : ''
    },
    attention: num(alertPressure.totalAlerts)
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
