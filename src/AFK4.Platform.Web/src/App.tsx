import { useCallback, useEffect, useMemo, useState } from 'react';
import { PlatformApiClient } from './api/platformApi';
import type { OwnerInvite } from './api/types';
import { readSession, type PlatformAdminSession } from './auth/tokenStore';
import { SignIn } from './components/SignIn';
import { TenantList } from './components/TenantList';
import { TenantDetailView } from './components/TenantDetail';
import { NewTenant } from './components/NewTenant';

export type AdminRoute =
  | { kind: 'tenantList' }
  | { kind: 'newTenant' }
  | { kind: 'tenantDetail'; organizationId: string; initialInvite: OwnerInvite | null }
  | { kind: 'notFound'; path: string };

export interface RouteResolution {
  route: AdminRoute;
  redirectTo?: string;
}

export interface AppProps {
  apiBaseUrl: string;
}

export default function App({ apiBaseUrl }: AppProps) {
  const [session, setSession] = useState<PlatformAdminSession | null>(() => readSession());
  const [route, setRoute] = useState<AdminRoute>(() => readCurrentRoute());

  const client = useMemo(
    () =>
      new PlatformApiClient({
        baseUrl: apiBaseUrl,
        session,
        onSessionChanged: next => setSession(next)
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [apiBaseUrl]
  );

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    function syncRouteFromLocation() {
      const resolution = resolvePlatformRoute(window.location.pathname, window.history.state);
      if (resolution.redirectTo !== undefined) {
        window.history.replaceState(window.history.state, '', resolution.redirectTo);
      }
      setRoute(resolution.route);
    }

    syncRouteFromLocation();
    window.addEventListener('popstate', syncRouteFromLocation);
    return () => window.removeEventListener('popstate', syncRouteFromLocation);
  }, []);

  const navigate = useCallback((nextRoute: AdminRoute, path: string, historyState: unknown = null) => {
    if (typeof window !== 'undefined') {
      window.history.pushState(historyState, '', path);
    }
    setRoute(nextRoute);
  }, []);

  const navigateToTenantList = useCallback(
    () => navigate({ kind: 'tenantList' }, '/admin/tenants'),
    [navigate]
  );

  const navigateToNewTenant = useCallback(
    () => navigate({ kind: 'newTenant' }, '/admin/tenants/new'),
    [navigate]
  );

  const navigateToTenantDetail = useCallback(
    (organizationId: string, initialInvite: OwnerInvite | null = null) => {
      navigate(
        { kind: 'tenantDetail', organizationId, initialInvite },
        `/admin/tenants/${encodeURIComponent(organizationId)}`,
        { initialInvite }
      );
    },
    [navigate]
  );

  if (route.kind === 'notFound') {
    return <NotFound path={route.path} onHome={navigateToTenantList} />;
  }

  if (session === null) {
    return <SignIn client={client} onSignedIn={() => setSession(client.getSession())} />;
  }

  return (
    <>
      <header className="app-header">
        <div className="app-title">AFK4 Control Plane</div>
        <div className="app-session">
          <button type="button" className="link" onClick={navigateToTenantList}>Tenants</button>
          <span className="muted">{session.displayName} ({session.userName})</span>
          <button type="button" onClick={() => void client.signOut()}>Sign out</button>
        </div>
      </header>
      <main>
        {route.kind === 'tenantList' && (
          <TenantList
            client={client}
            onOpenTenant={id => navigateToTenantDetail(id)}
            onCreateTenant={navigateToNewTenant}
          />
        )}
        {route.kind === 'newTenant' && (
          <NewTenant
            client={client}
            onCreated={response => navigateToTenantDetail(response.tenant.organizationId, response.ownerInvite)}
            onCancel={navigateToTenantList}
          />
        )}
        {route.kind === 'tenantDetail' && (
          <TenantDetailView
            client={client}
            organizationId={route.organizationId}
            initialInvite={route.initialInvite}
            onBack={navigateToTenantList}
          />
        )}
      </main>
    </>
  );
}

export function resolvePlatformRoute(pathname: string, historyState: unknown = null): RouteResolution {
  const path = normalizePath(pathname);

  if (path === '/') {
    return { route: { kind: 'tenantList' }, redirectTo: '/admin' };
  }
  if (path === '/tenants') {
    return { route: { kind: 'tenantList' }, redirectTo: '/admin/tenants' };
  }
  if (path === '/tenants/new') {
    return { route: { kind: 'newTenant' }, redirectTo: '/admin/tenants/new' };
  }

  const legacyTenantDetailMatch = /^\/tenants\/([^/]+)$/u.exec(path);
  if (legacyTenantDetailMatch !== null) {
    const organizationId = decodePathSegment(legacyTenantDetailMatch[1]);
    return {
      route: { kind: 'tenantDetail', organizationId, initialInvite: readInitialInvite(historyState) },
      redirectTo: `/admin/tenants/${encodeURIComponent(organizationId)}`
    };
  }

  if (path === '/admin' || path === '/admin/tenants') {
    return { route: { kind: 'tenantList' } };
  }
  if (path === '/admin/tenants/new') {
    return { route: { kind: 'newTenant' } };
  }

  const tenantDetailMatch = /^\/admin\/tenants\/([^/]+)$/u.exec(path);
  if (tenantDetailMatch !== null) {
    return {
      route: {
        kind: 'tenantDetail',
        organizationId: decodePathSegment(tenantDetailMatch[1]),
        initialInvite: readInitialInvite(historyState)
      }
    };
  }

  return { route: { kind: 'notFound', path } };
}

function readCurrentRoute(): AdminRoute {
  if (typeof window === 'undefined') {
    return { kind: 'tenantList' };
  }
  return resolvePlatformRoute(window.location.pathname, window.history.state).route;
}

function normalizePath(pathname: string): string {
  const withLeadingSlash = pathname.startsWith('/') ? pathname : `/${pathname}`;
  return withLeadingSlash.replace(/\/+$/u, '') || '/';
}

function decodePathSegment(segment: string): string {
  try {
    return decodeURIComponent(segment);
  } catch {
    return segment;
  }
}

function readInitialInvite(historyState: unknown): OwnerInvite | null {
  if (historyState === null || typeof historyState !== 'object') {
    return null;
  }
  const candidate = (historyState as { initialInvite?: unknown }).initialInvite;
  if (candidate === null || typeof candidate === 'object') {
    return candidate as OwnerInvite | null;
  }
  return null;
}

function NotFound({ path, onHome }: { path: string; onHome: () => void }) {
  return (
    <main>
      <div className="page page-narrow">
        <div className="page-header">
          <h1>Page not found</h1>
        </div>
        <section className="section">
          <p className="muted">No Platform Control Plane route matches <code>{path}</code>.</p>
          <button type="button" className="primary" onClick={onHome}>Open admin tenants</button>
        </section>
      </div>
    </main>
  );
}
