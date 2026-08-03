import type { PulseAlertLevel, PulseClub, PulseOrganization } from '@/api/types';

export type PulseView = 'now' | 'all' | 'debt';
export type PulseDensity = 'roomy' | 'dense';

const RANK: Record<PulseAlertLevel, number> = { normal: 0, attention: 1, critical: 2 };

export function alertRank(level: PulseAlertLevel): number {
  return RANK[level];
}

export function resolveDensity(clientCount: number): PulseDensity {
  return clientCount > 5 ? 'dense' : 'roomy';
}

export function selectView(
  organizations: readonly PulseOrganization[],
  view: PulseView
): PulseOrganization[] {
  const byName = (left: PulseOrganization, right: PulseOrganization) =>
    left.name.localeCompare(right.name, 'ru');

  if (view === 'all') {
    return [...organizations].sort(byName);
  }

  if (view === 'debt') {
    return organizations
      .filter(item => item.outstandingMinorUnits > 0)
      .sort((left, right) => right.outstandingMinorUnits - left.outstandingMinorUnits);
  }

  return [...organizations].sort((left, right) => {
    const delta = alertRank(right.alertLevel) - alertRank(left.alertLevel);
    return delta !== 0 ? delta : byName(left, right);
  });
}

export interface ClubOccupancy {
  devicesOnline: number;
  devicesTotal: number;
  seatsOccupied: number;
  seatsTotal: number;
}

/** Sums per-club device/seat counters into one occupancy figure for the network row. */
export function aggregateOccupancy(clubs: readonly PulseClub[]): ClubOccupancy {
  return clubs.reduce<ClubOccupancy>(
    (acc, club) => ({
      devicesOnline: acc.devicesOnline + club.devicesOnline,
      devicesTotal: acc.devicesTotal + club.devicesTotal,
      seatsOccupied: acc.seatsOccupied + club.seatsOccupied,
      seatsTotal: acc.seatsTotal + club.seatsTotal
    }),
    { devicesOnline: 0, devicesTotal: 0, seatsOccupied: 0, seatsTotal: 0 }
  );
}

export type ClubAlertSummary =
  | { kind: 'silent'; affected: number; total: number }
  | { kind: 'attention'; affected: number; total: number };

/**
 * Picks the concrete story behind a network-level alert: how many of its clubs are
 * silent (no agent heartbeat) versus how many merely carry some other flagged issue.
 * `agent_silent` is called out by name because it is the dominant, most actionable
 * signal; anything else collapses into a generic "needs attention" count.
 */
export function summarizeClubAlerts(clubs: readonly PulseClub[]): ClubAlertSummary | null {
  if (clubs.length === 0) return null;
  const silent = clubs.filter(club => club.alerts.some(alert => alert.kind === 'agent_silent')).length;
  if (silent > 0) return { kind: 'silent', affected: silent, total: clubs.length };
  const flagged = clubs.filter(club => club.alerts.length > 0).length;
  if (flagged > 0) return { kind: 'attention', affected: flagged, total: clubs.length };
  return null;
}
