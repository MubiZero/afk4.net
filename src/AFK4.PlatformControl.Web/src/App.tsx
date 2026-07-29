import { useCallback, useEffect, useMemo, useState } from 'react';
import { PlatformApiClient } from './api/platformApi';
import { AccountActivationApi } from './account-activation/accountActivationApi';
import type { CreateOrganizationResponse, OrganizationOwnerInvite } from './api/types';
import { readSession, type PlatformAdminSession } from './auth/tokenStore';
import { AppShell } from './components/shell/AppShell';
import { AccountActivation } from './account-activation/AccountActivation';
import { SignIn } from './components/SignIn';
import { useI18n, type MessageKey } from './i18n/I18nProvider';
import { BillingScreen as PlatformBillingScreen } from './platform/billing/BillingScreen';
import { useBillingMetrics } from './platform/billing/useBillingMetrics';
import { platformNav } from './platform/nav';
import { OverviewScreen as PlatformOverviewScreen } from './platform/overview/OverviewScreen';
import { useOrganizationMetrics } from './platform/overview/useOrganizationMetrics';
import { ProfileScreen as PlatformProfileScreen } from './platform/profile/ProfileScreen';
import { NewOrganizationScreen } from './platform/organizations/NewOrganizationScreen';
import { OrganizationsScreen } from './platform/organizations/OrganizationsScreen';
import { UpdatesScreen } from './platform/updates/UpdatesScreen';

export type AdminRoute =
  | { kind: 'adminOverview' }
  | { kind: 'adminBilling' }
  | { kind: 'adminUpdates' }
  | { kind: 'adminProfile' }
  | { kind: 'organizationList' }
  | { kind: 'newOrganization' }
  | { kind: 'organizationDetail'; organizationId: string; initialInvite: OrganizationOwnerInvite | null };

export type AppRoute =
  | AdminRoute
  | { kind: 'accountActivation'; code: string | null }
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
  const organizationOwnerInviteClient = useMemo(
    () => new AccountActivationApi({ baseUrl: apiBaseUrl }),
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

  const navigateToOrganizationList = useCallback(
    () => navigate({ kind: 'organizationList' }, '/admin/organizations'),
    [navigate]
  );
  const navigateToNewOrganization = useCallback(
    () => navigate({ kind: 'newOrganization' }, '/admin/organizations/new'),
    [navigate]
  );
  const navigateToOrganizationDetail = useCallback(
    (organizationId: string, initialInvite: OrganizationOwnerInvite | null = null) => navigate(
      { kind: 'organizationDetail', organizationId, initialInvite },
      `/admin/organizations/${encodeURIComponent(organizationId)}`,
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

  if (route.kind === 'accountActivation') {
    return <AccountActivation client={organizationOwnerInviteClient} initialCode={route.code} />;
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
      onCreateOrganization={navigateToNewOrganization}
      onOpenOrganization={navigateToOrganizationDetail}
      onCreatedOrganization={response => navigateToOrganizationDetail(response.organization.organizationId, response.organizationOwnerInvite)}
      onCancelNewOrganization={navigateToOrganizationList}
      onBackToOrganizations={navigateToOrganizationList}
      onSignOut={() => void adminClient.signOut()}
    />
  );
}

interface PlatformAreaProps {
  adminClient: PlatformApiClient;
  route: AdminRoute;
  session: PlatformAdminSession;
  onNavigate: (route: AdminRoute, path: string) => void;
  onCreateOrganization: () => void;
  onOpenOrganization: (organizationId: string) => void;
  onCreatedOrganization: (response: CreateOrganizationResponse) => void;
  onCancelNewOrganization: () => void;
  onBackToOrganizations: () => void;
  onSignOut: () => void;
}

const PLATFORM_SCREEN_TITLE_KEY: Record<AdminRoute['kind'], MessageKey> = {
  adminOverview: 'nav.platform.overview',
  adminBilling: 'nav.platform.billing',
  adminUpdates: 'nav.platform.updates',
  adminProfile: 'nav.platform.profile',
  organizationList: 'nav.platform.organizations',
  newOrganization: 'platform.organizations.new',
  organizationDetail: 'platform.organization.title'
};

function pathForAdminRoute(route: AdminRoute): string {
  switch (route.kind) {
    case 'adminOverview': return '/admin';
    case 'adminBilling': return '/admin/billing';
    case 'adminUpdates': return '/admin/updates';
    case 'adminProfile': return '/admin/profile';
    case 'organizationList':
    case 'newOrganization':
    case 'organizationDetail': return '/admin/organizations';
  }
}

function PlatformArea({
  adminClient,
  route,
  session,
  onNavigate,
  onCreateOrganization,
  onOpenOrganization,
  onCreatedOrganization,
  onCancelNewOrganization,
  onBackToOrganizations,
  onSignOut
}: PlatformAreaProps) {
  const { t } = useI18n();
  const metricsState = useOrganizationMetrics(adminClient.organizations);
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
            <span className="block truncate text-sm font-bold">Platform Control</span>
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
      ) : route.kind === 'adminUpdates' ? (
        <UpdatesScreen client={adminClient.updates} />
      ) : route.kind === 'adminProfile' ? (
        <PlatformProfileScreen session={session} onSignOut={onSignOut} />
      ) : route.kind === 'newOrganization' ? (
        <NewOrganizationScreen client={adminClient.organizations} onCreated={onCreatedOrganization} onCancel={onCancelNewOrganization} />
      ) : (
        <OrganizationsScreen
          client={adminClient}
          selectedOrganizationId={route.kind === 'organizationDetail' ? route.organizationId : null}
          initialInvite={route.kind === 'organizationDetail' ? route.initialInvite : null}
          onOpenOrganization={onOpenOrganization}
          onCloseOrganization={onBackToOrganizations}
          onCreateOrganization={onCreateOrganization}
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
  if (path === '/organizations') return { route: { kind: 'organizationList' }, redirectTo: '/admin/organizations' };
  if (path === '/organizations/new') return { route: { kind: 'newOrganization' }, redirectTo: '/admin/organizations/new' };

  const legacyOrganizationDetailMatch = /^\/organizations\/([^/]+)$/u.exec(path);
  if (legacyOrganizationDetailMatch !== null) {
    const organizationId = decodePathSegment(legacyOrganizationDetailMatch[1]);
    return {
      route: { kind: 'organizationDetail', organizationId, initialInvite: readInitialInvite(historyState) },
      redirectTo: `/admin/organizations/${encodeURIComponent(organizationId)}`
    };
  }

  if (path === '/admin') return { route: { kind: 'adminOverview' } };
  if (path === '/admin/billing') return { route: { kind: 'adminBilling' } };
  if (path === '/admin/updates') return { route: { kind: 'adminUpdates' } };
  if (path === '/admin/profile') return { route: { kind: 'adminProfile' } };
  if (path === '/admin/organizations') return { route: { kind: 'organizationList' } };
  if (path === '/admin/organizations/new') return { route: { kind: 'newOrganization' } };
  if (path === '/account-activation') {
    return { route: { kind: 'accountActivation', code: readQueryValue(_search, 'code') } };
  }

  const organizationDetailMatch = /^\/admin\/organizations\/([^/]+)$/u.exec(path);
  if (organizationDetailMatch !== null) {
    return {
      route: {
        kind: 'organizationDetail',
        organizationId: decodePathSegment(organizationDetailMatch[1]),
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

function readInitialInvite(historyState: unknown): OrganizationOwnerInvite | null {
  if (historyState === null || typeof historyState !== 'object') return null;
  const candidate = (historyState as { initialInvite?: unknown }).initialInvite;
  return candidate === null || typeof candidate === 'object' ? candidate as OrganizationOwnerInvite | null : null;
}

function isAdminRoute(route: AppRoute): route is AdminRoute {
  return route.kind === 'adminOverview'
    || route.kind === 'adminBilling'
    || route.kind === 'adminUpdates'
    || route.kind === 'adminProfile'
    || route.kind === 'organizationList'
    || route.kind === 'newOrganization'
    || route.kind === 'organizationDetail';
}

function NotFound({ path, onHome }: { path: string; onHome: () => void }) {
  return (
    <main>
      <div className="page page-narrow">
        <div className="page-header"><h1>Page not found</h1></div>
        <section className="section">
          <p className="muted">No Platform Control route matches <code>{path}</code>.</p>
          <button type="button" className="primary" onClick={onHome}>Open admin overview</button>
        </section>
      </div>
    </main>
  );
}
