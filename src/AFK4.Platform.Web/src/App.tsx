import { useCallback, useEffect, useMemo, useState } from 'react';
import { PlatformApiClient } from './api/platformApi';
import { OwnerInviteAcceptanceApi } from './api/ownerInviteAcceptanceApi';
import type { CreateTenantResponse, OwnerInvite } from './api/types';
import { readSession, type PlatformAdminSession } from './auth/tokenStore';
import { AppShell } from './components/shell/AppShell';
import { AcceptOwnerInvite } from './components/AcceptOwnerInvite';
import { SignIn } from './components/SignIn';
import { useI18n, type MessageKey } from './i18n/I18nProvider';
import { BillingScreen as PlatformBillingScreen } from './platform/billing/BillingScreen';
import { useBillingMetrics } from './platform/billing/useBillingMetrics';
import { platformNav } from './platform/nav';
import { OverviewScreen as PlatformOverviewScreen } from './platform/overview/OverviewScreen';
import { useTenantMetrics } from './platform/overview/useTenantMetrics';
import { ProfileScreen as PlatformProfileScreen } from './platform/profile/ProfileScreen';
import { NewTenantScreen } from './platform/tenants/NewTenantScreen';
import { TenantsScreen } from './platform/tenants/TenantsScreen';

export type AdminRoute =
  | { kind: 'adminOverview' }
  | { kind: 'adminBilling' }
  | { kind: 'adminProfile' }
  | { kind: 'tenantList' }
  | { kind: 'newTenant' }
  | { kind: 'tenantDetail'; organizationId: string; initialInvite: OwnerInvite | null };

export type AppRoute =
  | AdminRoute
  | { kind: 'acceptInvite'; code: string | null }
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
  const [route, setRoute] = useState<AppRoute>(readCurrentRoute);

  const adminClient = useMemo(
    () => new PlatformApiClient({
      baseUrl: apiBaseUrl,
      session: adminSession,
      onSessionChanged: next => setAdminSession(next)
    }),
    // The client owns refresh state and reports session changes through the callback.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [apiBaseUrl]
  );
  const ownerInviteClient = useMemo(
    () => new OwnerInviteAcceptanceApi({ baseUrl: apiBaseUrl }),
    [apiBaseUrl]
  );

  useEffect(() => {
    if (typeof window === 'undefined') return;

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
    if (typeof window !== 'undefined') window.history.pushState(historyState, '', path);
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
    (organizationId: string, initialInvite: OwnerInvite | null = null) => navigate(
      { kind: 'tenantDetail', organizationId, initialInvite },
      `/admin/tenants/${encodeURIComponent(organizationId)}`,
      { initialInvite }
    ),
    [navigate]
  );
  const navigateToAdminRoute = useCallback(
    (nextRoute: AdminRoute, path: string) => navigate(nextRoute, path),
    [navigate]
  );

  if (route.kind === 'notFound') {
    return (
      <NotFound
        path={route.path}
        onHome={() => navigate({ kind: 'adminOverview' }, '/admin')}
      />
    );
  }

  if (route.kind === 'acceptInvite') {
    return <AcceptOwnerInvite client={ownerInviteClient} initialCode={route.code} />;
  }

  if (adminSession === null) {
    return <SignIn client={adminClient} onSignedIn={() => setAdminSession(adminClient.getSession())} />;
  }

  return (
    <PlatformArea
      adminClient={adminClient}
      route={route}
      session={adminSession}
      onNavigate={navigateToAdminRoute}
      onCreateTenant={navigateToNewTenant}
      onOpenTenant={navigateToTenantDetail}
      onCreatedTenant={response => navigateToTenantDetail(response.tenant.organizationId, response.ownerInvite)}
      onCancelNewTenant={navigateToTenantList}
      onBackToTenants={navigateToTenantList}
      onSignOut={() => void adminClient.signOut()}
    />
  );
}

interface PlatformAreaProps {
  adminClient: PlatformApiClient;
  route: AdminRoute;
  session: PlatformAdminSession;
  onNavigate: (route: AdminRoute, path: string) => void;
  onCreateTenant: () => void;
  onOpenTenant: (organizationId: string) => void;
  onCreatedTenant: (response: CreateTenantResponse) => void;
  onCancelNewTenant: () => void;
  onBackToTenants: () => void;
  onSignOut: () => void;
}

const PLATFORM_SCREEN_TITLE_KEY: Record<AdminRoute['kind'], MessageKey> = {
  adminOverview: 'nav.platform.overview',
  adminBilling: 'nav.platform.billing',
  adminProfile: 'nav.platform.profile',
  tenantList: 'nav.platform.tenants',
  newTenant: 'platform.tenants.new',
  tenantDetail: 'platform.tenant.title'
};

function pathForAdminRoute(route: AdminRoute): string {
  switch (route.kind) {
    case 'adminOverview': return '/admin';
    case 'adminBilling': return '/admin/billing';
    case 'adminProfile': return '/admin/profile';
    case 'tenantList':
    case 'newTenant':
    case 'tenantDetail': return '/admin/tenants';
  }
}

function PlatformArea({
  adminClient,
  route,
  session,
  onNavigate,
  onCreateTenant,
  onOpenTenant,
  onCreatedTenant,
  onCancelNewTenant,
  onBackToTenants,
  onSignOut
}: PlatformAreaProps) {
  const { t } = useI18n();
  const metricsState = useTenantMetrics(adminClient.tenants);
  const billingMetricsState = useBillingMetrics(adminClient.invoices);

  const handleNavigate = (path: string) => {
    const resolution = resolvePlatformRoute(path);
    if (isAdminRoute(resolution.route)) onNavigate(resolution.route, resolution.redirectTo ?? path);
  };

  return (
    <AppShell
      navGroups={platformNav}
      sidebarHeader={
        <div className="m-3 flex items-center gap-3 rounded-lg border border-border bg-card px-3 py-2 text-left">
          <img src="/favicon.svg" alt="" className="size-7 rounded-md" />
          <span className="min-w-0">
            <span className="block truncate text-sm font-bold">AFK4.NET Control Plane</span>
            <span className="block truncate text-[11px] text-muted">{session.userName}</span>
          </span>
        </div>
      }
      activePath={pathForAdminRoute(route)}
      subtitle=""
      screenTitle={t(PLATFORM_SCREEN_TITLE_KEY[route.kind])}
      userName={session.displayName}
      roleLabel={t('platform.profile.roleLabel')}
      onNavigate={handleNavigate}
      onSignOut={onSignOut}
    >
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} billing={billingMetricsState} />
      ) : route.kind === 'adminBilling' ? (
        <PlatformBillingScreen client={adminClient} />
      ) : route.kind === 'adminProfile' ? (
        <PlatformProfileScreen session={session} onSignOut={onSignOut} />
      ) : route.kind === 'newTenant' ? (
        <NewTenantScreen client={adminClient.tenants} onCreated={onCreatedTenant} onCancel={onCancelNewTenant} />
      ) : (
        <TenantsScreen
          client={adminClient}
          selectedTenantId={route.kind === 'tenantDetail' ? route.organizationId : null}
          initialInvite={route.kind === 'tenantDetail' ? route.initialInvite : null}
          onOpenTenant={onOpenTenant}
          onCloseTenant={onBackToTenants}
          onCreateTenant={onCreateTenant}
        />
      )}
    </AppShell>
  );
}

export function resolvePlatformRoute(
  pathname: string,
  historyState: unknown = null,
  _search = ''
): RouteResolution {
  const path = normalizePath(pathname);

  if (path === '/') return { route: { kind: 'adminOverview' }, redirectTo: '/admin' };
  if (path === '/tenants') return { route: { kind: 'tenantList' }, redirectTo: '/admin/tenants' };
  if (path === '/tenants/new') return { route: { kind: 'newTenant' }, redirectTo: '/admin/tenants/new' };

  const legacyTenantDetailMatch = /^\/tenants\/([^/]+)$/u.exec(path);
  if (legacyTenantDetailMatch !== null) {
    const organizationId = decodePathSegment(legacyTenantDetailMatch[1]);
    return {
      route: { kind: 'tenantDetail', organizationId, initialInvite: readInitialInvite(historyState) },
      redirectTo: `/admin/tenants/${encodeURIComponent(organizationId)}`
    };
  }

  if (path === '/admin') return { route: { kind: 'adminOverview' } };
  if (path === '/admin/billing') return { route: { kind: 'adminBilling' } };
  if (path === '/admin/profile') return { route: { kind: 'adminProfile' } };
  if (path === '/admin/tenants') return { route: { kind: 'tenantList' } };
  if (path === '/admin/tenants/new') return { route: { kind: 'newTenant' } };
  if (path === '/auth/accept-invite') {
    return { route: { kind: 'acceptInvite', code: readQueryValue(_search, 'code') } };
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

function readCurrentRoute(): AppRoute {
  if (typeof window === 'undefined') return { kind: 'adminOverview' };
  return resolvePlatformRoute(window.location.pathname, window.history.state, window.location.search).route;
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
    const value = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search).get(key);
    return value === null || value.trim().length === 0 ? null : value;
  } catch {
    return null;
  }
}

function readInitialInvite(historyState: unknown): OwnerInvite | null {
  if (historyState === null || typeof historyState !== 'object') return null;
  const candidate = (historyState as { initialInvite?: unknown }).initialInvite;
  return candidate === null || typeof candidate === 'object' ? candidate as OwnerInvite | null : null;
}

function isAdminRoute(route: AppRoute): route is AdminRoute {
  return route.kind === 'adminOverview'
    || route.kind === 'adminBilling'
    || route.kind === 'adminProfile'
    || route.kind === 'tenantList'
    || route.kind === 'newTenant'
    || route.kind === 'tenantDetail';
}

function NotFound({ path, onHome }: { path: string; onHome: () => void }) {
  return (
    <main>
      <div className="page page-narrow">
        <div className="page-header"><h1>Page not found</h1></div>
        <section className="section">
          <p className="muted">No Platform Web route matches <code>{path}</code>.</p>
          <button type="button" className="primary" onClick={onHome}>Open admin overview</button>
        </section>
      </div>
    </main>
  );
}
