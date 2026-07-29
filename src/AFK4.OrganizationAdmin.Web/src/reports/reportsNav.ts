import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission } from '../operatorPermissions';
import { permissionNames } from '../permissionNames';

export type ReportsDestinationId = 'summary' | 'shiftsCash' | 'revenue';

export interface ReportsDestination {
  id: ReportsDestinationId;
  labelKey: MessageKey;
  permissions: readonly string[];
}

export const reportsDestinations: readonly ReportsDestination[] = [
  { id: 'summary', labelKey: 'op.reports.dest.overview', permissions: [permissionNames.viewReports] },
  { id: 'shiftsCash', labelKey: 'op.reports.dest.history', permissions: [permissionNames.viewReports] },
  { id: 'revenue', labelKey: 'op.reports.dest.journal', permissions: [permissionNames.viewReports] }
];

export function allowedReportsDestinations(session: OperatorAuthSession | null): ReportsDestination[] {
  return reportsDestinations.filter((destination) => hasAnyPermission(session, destination.permissions));
}
