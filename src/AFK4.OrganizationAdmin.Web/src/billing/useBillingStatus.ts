import { useEffect, useState } from 'react';
import type { OperatorAuthSession } from '../authClient';
import type { OrganizationBillingStatusDto } from '../operatorApiClients';
import type { AuthStatus, OperatorConfig } from '../operatorTypes';
import { permissionNames, hasPermission } from '../operatorPermissions';
import { createAuthenticatedOperatorClients, shellOperationalRefreshMs } from '../operatorHelpers';

// Loads the club's own arrears summary for the shell banner (BillingStatusBanner). Gated on
// `viewSubscription` — a cashier without that permission has no business seeing what the club owes
// the platform, and there is no reason to spend a request on every page load fetching numbers
// nobody below the gate will ever see. Best-effort: a failed fetch just means no banner, not an
// error screen — this is a heads-up, not a critical read.
//
// Re-polled on `shellOperationalRefreshMs` (same slow safety-net interval as useShellData): the
// shell stays open for a whole cashier shift, or for days on a WebView2 kiosk host, so a single
// fetch on mount would leave a paid-off club's banner stuck red (and the day counter frozen) until
// the app restarts, and would leave a genuinely overdue club unbannered for the rest of the session
// if the very first request happened to hit a network blip.
export function useBillingStatus(
  authStatus: AuthStatus,
  authSession: OperatorAuthSession | null,
  config: OperatorConfig,
  // Overridable only for tests — real timers with a small interval beat mocking window.setInterval
  // under fake timers, which has repeatedly hung in CI (see useBillingStatus.test.ts). Production
  // always gets the default, since App.tsx never passes this argument.
  refreshMs: number = shellOperationalRefreshMs
): OrganizationBillingStatusDto | null {
  const [status, setStatus] = useState<OrganizationBillingStatusDto | null>(null);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null || !hasPermission(authSession, permissionNames.viewSubscription)) {
      setStatus(null);
      return undefined;
    }

    let disposed = false;
    const clients = createAuthenticatedOperatorClients(config, authSession);

    const loadStatus = () => {
      clients.orgBilling.getBillingStatus(authSession.organizationId)
        .then((result) => {
          if (!disposed) {
            setStatus(result);
          }
        })
        .catch(() => {
          // Best-effort — see comment above; a failed poll just keeps whatever was last shown.
        });
    };

    loadStatus();
    const intervalId = window.setInterval(loadStatus, refreshMs);

    return () => {
      disposed = true;
      window.clearInterval(intervalId);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authStatus, authSession, config.platformBaseUrl, refreshMs]);

  return status;
}
