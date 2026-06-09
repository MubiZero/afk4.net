import { useEffect, useState } from 'react';
import type { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type { OperatorAuthSession } from './authClient';
import { type OperatorDashboardSummaryDto, type ShiftDto } from './operatorApiClients';
import type { AuthStatus, LoadStatus, OperatorConfig } from './operatorTypes';
import { permissionNames, hasPermission } from './operatorPermissions';
import {
  shellOperationalRefreshMs,
  toDateInputValue,
  resolveActiveBranchId,
  createAuthenticatedOperatorClients,
  dashboardRangeQuery
} from './operatorHelpers';

type Translate = ReturnType<typeof useI18n>['t'];

export interface ShellData {
  shellCurrentShift: ShiftDto | null;
  shellDashboardSummary: OperatorDashboardSummaryDto | null;
  shellLoadStatus: LoadStatus;
  shellLoadError: string | null;
}

// Operator shell KPI strip: current shift + today's dashboard summary, polled on a slow interval.
export function useShellData(
  authStatus: AuthStatus,
  authSession: OperatorAuthSession | null,
  config: OperatorConfig,
  t: Translate
): ShellData {
  const [shellCurrentShift, setShellCurrentShift] = useState<ShiftDto | null>(null);
  const [shellDashboardSummary, setShellDashboardSummary] = useState<OperatorDashboardSummaryDto | null>(null);
  const [shellLoadStatus, setShellLoadStatus] = useState<LoadStatus>('loading');
  const [shellLoadError, setShellLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      setShellCurrentShift(null);
      setShellDashboardSummary(null);
      setShellLoadStatus('loading');
      setShellLoadError(null);
      return undefined;
    }

    const branchId = resolveActiveBranchId(authSession, config.branchId);
    if (!branchId) {
      setShellCurrentShift(null);
      setShellDashboardSummary(null);
      setShellLoadStatus('failed');
      setShellLoadError(t('op.dashboard.noBranch'));
      return undefined;
    }

    const canLoadShift = hasPermission(authSession, permissionNames.viewShift);
    const canLoadSummary = hasPermission(authSession, permissionNames.viewReports);
    if (!canLoadShift && !canLoadSummary) {
      setShellCurrentShift(null);
      setShellDashboardSummary(null);
      setShellLoadStatus('failed');
      setShellLoadError(null);
      return undefined;
    }

    let disposed = false;
    const clients = createAuthenticatedOperatorClients(config, authSession);

    const loadShellStatus = async () => {
      setShellLoadStatus((current) => current === 'backend' ? current : 'loading');
      setShellLoadError(null);

      let nextShift: ShiftDto | null = null;
      let nextSummary: OperatorDashboardSummaryDto | null = null;
      const errors: string[] = [];
      const today = toDateInputValue(new Date());

      await Promise.all([
        canLoadShift
          ? clients.shifts.getCurrentShift(branchId)
            .then((shift) => {
              nextShift = shift;
            })
            .catch((error) => {
              errors.push(projectOperatorError(error, t).detail);
            })
          : Promise.resolve(),
        canLoadSummary
          ? clients.dashboard.getSummary(branchId, dashboardRangeQuery(today, today))
            .then((summary) => {
              nextSummary = summary;
            })
            .catch((error) => {
              errors.push(projectOperatorError(error, t).detail);
            })
          : Promise.resolve()
      ]);

      if (disposed) {
        return;
      }

      setShellCurrentShift(nextShift);
      setShellDashboardSummary(nextSummary);
      setShellLoadStatus(errors.length > 0 && nextShift === null && nextSummary === null ? 'failed' : 'backend');
      setShellLoadError(errors[0] ?? null);
    };

    void loadShellStatus();
    const intervalId = window.setInterval(() => void loadShellStatus(), shellOperationalRefreshMs);

    return () => {
      disposed = true;
      window.clearInterval(intervalId);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

  return { shellCurrentShift, shellDashboardSummary, shellLoadStatus, shellLoadError };
}
