import { useCallback, useEffect, useMemo, useState } from 'react';
import { ClubApiClient } from './api/clubApi';
import { PlatformApiClient } from './api/platformApi';
import { StaffAuthApiClient } from './api/staffAuthApi';
import type { OwnerInvite } from './api/types';
import { readStaffSession, type StaffSession } from './auth/staffTokenStore';
import { readSession, type PlatformAdminSession } from './auth/tokenStore';
import { AcceptInvite } from './components/AcceptInvite';
import { ClubDashboard } from './components/ClubDashboard';
import { SignIn } from './components/SignIn';
import { StaffSignIn } from './components/StaffSignIn';
import { TenantList } from './components/TenantList';
import { TenantDetailView } from './components/TenantDetail';
import { NewTenant } from './components/NewTenant';

export type AdminRoute =
  | { kind: 'tenantList' }
  | { kind: 'newTenant' }
  | { kind: 'tenantDetail'; organizationId: string; initialInvite: OwnerInvite | null };

export type AuthRoute =
  | { kind: 'acceptInvite'; code: string | null }
  | { kind: 'staffSignIn'; organizationId: string | null }
  | { kind: 'forgotPassword' }
  | { kind: 'resetPassword' };

export type ClubRoute =
  | { kind: 'clubDashboard' }
  | { kind: 'clubInstall' }
  | { kind: 'clubBranches' }
  | { kind: 'clubBranchDetail'; branchId: string }
  | { kind: 'clubBranchFloorMap'; branchId: string }
  | { kind: 'clubBranchDevices'; branchId: string }
  | { kind: 'clubBranchPendingDevices'; branchId: string }
  | { kind: 'clubBranchOperators'; branchId: string };

export type AppRoute =
  | AdminRoute
  | AuthRoute
  | ClubRoute
  | { kind: 'notFound'; path: string };

export interface RouteResolution {
  route: AppRoute;
  redirectTo?: string;
}

export interface AppProps {
  apiBaseUrl: string;
}

export default function App({ apiBaseUrl }: AppProps) {
  const [adminSession, setAdminSession] = useState<PlatformAdminSession | null>(() => readSession());
  const [staffSession, setStaffSession] = useState<StaffSession | null>(() => readStaffSession());
  const [route, setRoute] = useState<AppRoute>(() => readCurrentRoute());

  const adminClient = useMemo(
    () =>
      new PlatformApiClient({
        baseUrl: apiBaseUrl,
        session: adminSession,
        onSessionChanged: next => setAdminSession(next)
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [apiBaseUrl]
  );

  const staffClient = useMemo(
    () =>
      new StaffAuthApiClient({
        baseUrl: apiBaseUrl,
        session: staffSession,
        onSessionChanged: next => setStaffSession(next)
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [apiBaseUrl]
  );

  const clubClient = useMemo(
    () =>
      new ClubApiClient({
        baseUrl: apiBaseUrl,
        session: staffSession,
        onSessionChanged: next => setStaffSession(next)
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [apiBaseUrl, staffSession]
  );

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    function syncRouteFromLocation() {
      const resolution = resolvePlatformRoute(
        window.location.pathname,
        window.history.state,
        window.location.search
      );
      if (resolution.redirectTo !== undefined) {
        window.history.replaceState(window.history.state, '', resolution.redirectTo);
      }
      setRoute(resolution.route);
    }

    syncRouteFromLocation();
    window.addEventListener('popstate', syncRouteFromLocation);
    return () => window.removeEventListener('popstate', syncRouteFromLocation);
  }, []);

  const navigate = useCallback((nextRoute: AppRoute, path: string, historyState: unknown = null) => {
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

  const navigateToStaffSignIn = useCallback(
    () => navigate({ kind: 'staffSignIn', organizationId: null }, '/auth/sign-in'),
    [navigate]
  );

  const navigateToClubInstall = useCallback(
    () => navigate({ kind: 'clubInstall' }, '/club/install'),
    [navigate]
  );

  const navigateToClubRoute = useCallback(
    (nextRoute: ClubRoute, path: string) => navigate(nextRoute, path),
    [navigate]
  );

  if (route.kind === 'notFound') {
    return <NotFound path={route.path} onHome={navigateToTenantList} />;
  }

  if (route.kind === 'acceptInvite') {
    return (
      <AcceptInvite
        client={staffClient}
        initialCode={route.code}
        onAccepted={navigateToClubInstall}
        onOpenSignIn={navigateToStaffSignIn}
      />
    );
  }

  if (route.kind === 'staffSignIn') {
    return (
      <StaffSignIn
        client={staffClient}
        initialOrganizationId={route.organizationId}
        onSignedIn={navigateToClubInstall}
      />
    );
  }

  if (route.kind === 'forgotPassword' || route.kind === 'resetPassword') {
    return <ReservedAuthPage onSignIn={navigateToStaffSignIn} />;
  }

  if (isClubRoute(route)) {
    if (staffSession === null) {
      return (
        <StaffSignIn
          client={staffClient}
          initialOrganizationId={null}
          onSignedIn={navigateToClubInstall}
        />
      );
    }
    return (
      <ClubDashboard
        client={clubClient}
        route={route}
        session={staffSession}
        onSignOut={() => staffClient.signOutLocal()}
        onNavigate={navigateToClubRoute}
      />
    );
  }

  if (!isAdminRoute(route)) {
    return <NotFound path="/" onHome={navigateToTenantList} />;
  }

  if (adminSession === null) {
    return <SignIn client={adminClient} onSignedIn={() => setAdminSession(adminClient.getSession())} />;
  }

  return (
    <>
      <header className="app-header">
        <div className="app-title">AFK4 Control Plane</div>
        <div className="app-session">
          <button type="button" className="link" onClick={navigateToTenantList}>Tenants</button>
          <span className="muted">{adminSession.displayName} ({adminSession.userName})</span>
          <button type="button" onClick={() => void adminClient.signOut()}>Sign out</button>
        </div>
      </header>
      <main>
        {route.kind === 'tenantList' && (
          <TenantList
            client={adminClient}
            onOpenTenant={id => navigateToTenantDetail(id)}
            onCreateTenant={navigateToNewTenant}
          />
        )}
        {route.kind === 'newTenant' && (
          <NewTenant
            client={adminClient}
            onCreated={response => navigateToTenantDetail(response.tenant.organizationId, response.ownerInvite)}
            onCancel={navigateToTenantList}
          />
        )}
        {route.kind === 'tenantDetail' && (
          <TenantDetailView
            client={adminClient}
            organizationId={route.organizationId}
            initialInvite={route.initialInvite}
            onBack={navigateToTenantList}
          />
        )}
      </main>
    </>
  );
}

export function resolvePlatformRoute(
  pathname: string,
  historyState: unknown = null,
  search = ''
): RouteResolution {
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

  if (path === '/auth') {
    return { route: { kind: 'staffSignIn', organizationId: null }, redirectTo: '/auth/sign-in' };
  }
  if (path === '/auth/sign-in') {
    return { route: { kind: 'staffSignIn', organizationId: readQueryValue(search, 'organizationId') } };
  }
  if (path === '/auth/accept-invite') {
    return { route: { kind: 'acceptInvite', code: readQueryValue(search, 'code') } };
  }
  if (path === '/auth/forgot-password') {
    return { route: { kind: 'forgotPassword' } };
  }
  if (path === '/auth/reset-password') {
    return { route: { kind: 'resetPassword' } };
  }

  if (path === '/club') {
    return { route: { kind: 'clubDashboard' } };
  }
  if (path === '/club/install') {
    return { route: { kind: 'clubInstall' } };
  }
  if (path === '/club/branches') {
    return { route: { kind: 'clubBranches' } };
  }

  const pendingDevicesMatch = /^\/club\/branches\/([^/]+)\/devices\/pending$/u.exec(path);
  if (pendingDevicesMatch !== null) {
    return {
      route: {
        kind: 'clubBranchPendingDevices',
        branchId: decodePathSegment(pendingDevicesMatch[1])
      }
    };
  }

  const branchDevicesMatch = /^\/club\/branches\/([^/]+)\/devices$/u.exec(path);
  if (branchDevicesMatch !== null) {
    return {
      route: {
        kind: 'clubBranchDevices',
        branchId: decodePathSegment(branchDevicesMatch[1])
      }
    };
  }

  const floorMapMatch = /^\/club\/branches\/([^/]+)\/floor-map$/u.exec(path);
  if (floorMapMatch !== null) {
    return {
      route: {
        kind: 'clubBranchFloorMap',
        branchId: decodePathSegment(floorMapMatch[1])
      }
    };
  }

  const operatorsMatch = /^\/club\/branches\/([^/]+)\/operators$/u.exec(path);
  if (operatorsMatch !== null) {
    return {
      route: {
        kind: 'clubBranchOperators',
        branchId: decodePathSegment(operatorsMatch[1])
      }
    };
  }

  const branchDetailMatch = /^\/club\/branches\/([^/]+)$/u.exec(path);
  if (branchDetailMatch !== null) {
    return {
      route: {
        kind: 'clubBranchDetail',
        branchId: decodePathSegment(branchDetailMatch[1])
      }
    };
  }

  return { route: { kind: 'notFound', path } };
}

function readCurrentRoute(): AppRoute {
  if (typeof window === 'undefined') {
    return { kind: 'tenantList' };
  }
  return resolvePlatformRoute(
    window.location.pathname,
    window.history.state,
    window.location.search
  ).route;
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

function readQueryValue(search: string, key: string): string | null {
  try {
    const params = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search);
    const value = params.get(key);
    if (value === null || value.trim().length === 0) {
      return null;
    }
    return value;
  } catch {
    return null;
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

function isAdminRoute(route: AppRoute): route is AdminRoute {
  return route.kind === 'tenantList'
    || route.kind === 'newTenant'
    || route.kind === 'tenantDetail';
}

function isClubRoute(route: AppRoute): route is ClubRoute {
  return route.kind === 'clubDashboard'
    || route.kind === 'clubInstall'
    || route.kind === 'clubBranches'
    || route.kind === 'clubBranchDetail'
    || route.kind === 'clubBranchFloorMap'
    || route.kind === 'clubBranchDevices'
    || route.kind === 'clubBranchPendingDevices'
    || route.kind === 'clubBranchOperators';
}

function ReservedAuthPage({ onSignIn }: { onSignIn: () => void }) {
  return (
    <div className="page page-narrow">
      <h1>Password reset</h1>
      <section className="section">
        <p className="muted">Password reset is not available in this build.</p>
        <button type="button" className="primary" onClick={onSignIn}>Back to sign in</button>
      </section>
    </div>
  );
}

function NotFound({ path, onHome }: { path: string; onHome: () => void }) {
  return (
    <main>
      <div className="page page-narrow">
        <div className="page-header">
          <h1>Page not found</h1>
        </div>
        <section className="section">
          <p className="muted">No Platform Web route matches <code>{path}</code>.</p>
          <button type="button" className="primary" onClick={onHome}>Open admin tenants</button>
        </section>
      </div>
    </main>
  );
}
