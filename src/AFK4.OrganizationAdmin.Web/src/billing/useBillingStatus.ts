import { useEffect, useState } from 'react';
import type { OperatorAuthSession } from '../authClient';
import type { OrganizationBillingStatusDto } from '../operatorApiClients';
import type { AuthStatus, OperatorConfig } from '../operatorTypes';
import { permissionNames, hasPermission } from '../operatorPermissions';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';

// Loads the club's own arrears summary for the shell banner (BillingStatusBanner). Gated on
// `viewSubscription` — a cashier without that permission has no business seeing what the club owes
// the platform, and there is no reason to spend a request on every page load fetching numbers
// nobody below the gate will ever see. Best-effort: a failed fetch just means no banner, not an
// error screen — this is a heads-up, not a critical read.
export function useBillingStatus(
  authStatus: AuthStatus,
  authSession: OperatorAuthSession | null,
  config: OperatorConfig
): OrganizationBillingStatusDto | null {
  const [status, setStatus] = useState<OrganizationBillingStatusDto | null>(null);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null || !hasPermission(authSession, permissionNames.viewSubscription)) {
      setStatus(null);
      return undefined;
    }

    let disposed = false;
    const clients = createAuthenticatedOperatorClients(config, authSession);

    clients.orgBilling.getBillingStatus(authSession.organizationId)
      .then((result) => {
        if (!disposed) {
          setStatus(result);
        }
      })
      .catch(() => {
        // Best-effort — see comment above; the banner simply stays hidden.
      });

    return () => {
      disposed = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authStatus, authSession, config.platformBaseUrl]);

  return status;
}
