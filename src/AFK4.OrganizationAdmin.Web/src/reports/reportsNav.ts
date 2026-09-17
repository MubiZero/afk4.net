import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission } from '../operatorPermissions';
import { permissionNames } from '../permissionNames';

export type ReportsDestinationId = 'summary' | 'shiftsCash' | 'revenue' | 'gameplay' | 'operatorActions' | 'schedules';

export interface ReportsDestination {
  id: ReportsDestinationId;
  labelKey: MessageKey;
  permissions: readonly string[];
}

export const reportsDestinations: readonly ReportsDestination[] = [
  { id: 'summary', labelKey: 'op.reports.dest.overview', permissions: [permissionNames.viewReports] },
  { id: 'shiftsCash', labelKey: 'op.reports.dest.history', permissions: [permissionNames.viewReports] },
  { id: 'revenue', labelKey: 'op.reports.dest.journal', permissions: [permissionNames.viewReports] },
  // Эти два отчёта уже можно было заказать письмом, а открыть в кабинете — нет.
  { id: 'gameplay', labelKey: 'op.reports.dest.gameplay', permissions: [permissionNames.viewReports] },
  { id: 'operatorActions', labelKey: 'op.reports.dest.operatorActions', permissions: [permissionNames.viewReports] },
  // Расписания рассылок живут за тем же правом, что и сами отчёты: письмо не показывает ничего
  // такого, чего нельзя открыть здесь же руками.
  { id: 'schedules', labelKey: 'op.reports.dest.schedules', permissions: [permissionNames.viewReports] }
];

export function allowedReportsDestinations(session: OperatorAuthSession | null): ReportsDestination[] {
  return reportsDestinations.filter((destination) => hasAnyPermission(session, destination.permissions));
}
