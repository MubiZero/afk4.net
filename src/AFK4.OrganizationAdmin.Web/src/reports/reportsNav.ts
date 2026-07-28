import type { LucideIcon } from 'lucide-react';
import { Gauge, History, ScrollText } from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission } from '../operatorPermissions';
import { permissionNames } from '../permissionNames';

export type ReportsDestinationId = 'overview' | 'history' | 'journal';

export interface ReportsDestination {
  id: ReportsDestinationId;
  labelKey: MessageKey;
  subtitleKey: MessageKey;
  Icon: LucideIcon;
  permissions: readonly string[]; // видим, если у сессии есть ЛЮБОЕ из
}

export const reportsDestinations: readonly ReportsDestination[] = [
  {
    id: 'overview',
    labelKey: 'op.reports.dest.overview',
    subtitleKey: 'op.reports.dest.overview.subtitle',
    Icon: Gauge,
    permissions: [permissionNames.viewReports]
  },
  {
    id: 'history',
    labelKey: 'op.reports.dest.history',
    subtitleKey: 'op.reports.dest.history.subtitle',
    Icon: History,
    permissions: [permissionNames.viewReports]
  },
  {
    id: 'journal',
    labelKey: 'op.reports.dest.journal',
    subtitleKey: 'op.reports.dest.journal.subtitle',
    Icon: ScrollText,
    permissions: [permissionNames.viewAudit]
  }
];

export function allowedReportsDestinations(session: OperatorAuthSession | null): ReportsDestination[] {
  return reportsDestinations.filter((destination) => hasAnyPermission(session, destination.permissions));
}
