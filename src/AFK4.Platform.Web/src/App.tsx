import { useCallback, useEffect, useMemo, useState } from 'react';
import { ClubApiClient } from './api/clubApi';
import { PlatformApiClient } from './api/platformApi';
import { StaffAuthApiClient } from './api/staffAuthApi';
import type { CreateTenantResponse, OwnerInvite } from './api/types';
import { readStaffSession, type StaffSession } from './auth/staffTokenStore';
import { readSession, type PlatformAdminSession } from './auth/tokenStore';
import { AcceptInvite } from './components/AcceptInvite';
import { AppShell } from './components/shell/AppShell';
import { OverviewScreen } from './club/overview/OverviewScreen';
import { useOverview } from './club/overview/useOverview';
import { VenueScreen } from './club/venue/VenueScreen';
import { SettingsScreen } from './club/settings/SettingsScreen';
import { MonetizationScreen } from './club/monetization/MonetizationScreen';
import { ReportsScreen } from './club/reports/ReportsScreen';
import { JournalScreen } from './club/journal/JournalScreen';
import { ClientsScreen } from './club/clients/ClientsScreen';
import { InstallScreen } from './club/install/InstallScreen';
import { ProfileScreen } from './club/profile/ProfileScreen';
import { resolveBranchNames } from './club/profile/profileModel';
import { useActiveBranch } from './club/branches/useActiveBranch';
import { useBranchDirectory } from './club/branches/useBranchDirectory';
import { BranchesScreen } from './club/branches/BranchesScreen';
import { roleFromPermissions, visibleNav } from './club/nav';
import { BranchSwitcher } from './components/shell/BranchSwitcher';
import { EmptyState } from './components/ui/states';
import { useI18n } from './i18n/I18nProvider';
import { SignIn } from './components/SignIn';
import { StaffSignIn } from './components/StaffSignIn';
import { NewTenant } from './components/NewTenant';
import { platformNav } from './platform/nav';
import { OverviewScreen as PlatformOverviewScreen } from './platform/overview/OverviewScreen';
import { BillingScreen as PlatformBillingScreen } from './platform/billing/BillingScreen';
import { useTenantMetrics } from './platform/overview/useTenantMetrics';
import { useBillingMetrics } from './platform/billing/useBillingMetrics';
import { TenantsScreen } from './platform/tenants/TenantsScreen';

export type PlatformWebAudience = 'all' | 'admin' | 'club';

export type AdminRoute =
  | { kind: 'adminOverview' }
  | { kind: 'adminBilling' }
  | { kind: 'tenantList' }
  | { kind: 'newTenant' }
  | { kind: 'tenantDetail'; organizationId: string; initialInvite: OwnerInvite | null };

export type AuthRoute =
  | { kind: 'acceptInvite'; code: string | null }
  | { kind: 'staffSignIn'; tenantKey: string | null }
  | { kind: 'forgotPassword' }
  | { kind: 'resetPassword' };

export type ClubRoute =
  | { kind: 'clubDashboard' }
  | { kind: 'clubVenue' }
  | { kind: 'clubClients' }
  | { kind: 'clubMonetization' }
  | { kind: 'clubReports' }
  | { kind: 'clubJournal' }
  | { kind: 'clubSettings' }
  | { kind: 'clubInstall' }
  | { kind: 'clubProfile' }
  | { kind: 'clubBranches' };

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
  audience?: PlatformWebAudience;
}

const defaultAudience = readPlatformWebAudience();

export default function App({ apiBaseUrl, audience = defaultAudience }: AppProps) {
  const [adminSession, setAdminSession] = useState<PlatformAdminSession | null>(() => readSession());
  const [staffSession, setStaffSession] = useState<StaffSession | null>(() => readStaffSession());
  const [route, setRoute] = useState<AppRoute>(() => readCurrentRoute(audience));

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
        window.location.search,
        audience
      );
      if (resolution.redirectTo !== undefined) {
        window.history.replaceState(window.history.state, '', resolution.redirectTo);
      }
      setRoute(resolution.route);
    }

    syncRouteFromLocation();
    window.addEventListener('popstate', syncRouteFromLocation);
    return () => window.removeEventListener('popstate', syncRouteFromLocation);
  }, [audience]);

  const navigate = useCallback((nextRoute: AppRoute, path: string, historyState: unknown = null) => {
    const allowedRoute = routeForAudience(nextRoute, path, audience);
    if (typeof window !== 'undefined') {
      window.history.pushState(historyState, '', path);
    }
    setRoute(allowedRoute);
  }, [audience]);

  const navigateToAudienceHome = useCallback(() => {
    const home = getAudienceHome(audience);
    navigate(home.route, home.path);
  }, [audience, navigate]);

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

  const navigateToAdminRoute = useCallback(
    (nextRoute: AdminRoute, path: string) => navigate(nextRoute, path),
    [navigate]
  );

  const navigateToStaffSignIn = useCallback(
    () => navigate({ kind: 'staffSignIn', tenantKey: null }, '/auth/sign-in'),
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
    return (
      <NotFound
        path={route.path}
        homeLabel={getAudienceHome(audience).label}
        onHome={navigateToAudienceHome}
      />
    );
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
        initialTenantKey={route.tenantKey}
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
          initialTenantKey={null}
          onSignedIn={navigateToClubInstall}
        />
      );
    }
    return (
      <ClubArea
        clubClient={clubClient}
        route={route}
        session={staffSession}
        onSignOut={() => staffClient.signOutLocal()}
        onNavigate={navigateToClubRoute}
      />
    );
  }

  if (!isAdminRoute(route)) {
    return (
      <NotFound
        path="/"
        homeLabel={getAudienceHome(audience).label}
        onHome={navigateToAudienceHome}
      />
    );
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
      onCreatedTenant={(response) => navigateToTenantDetail(response.tenant.organizationId, response.ownerInvite)}
      onCancelNewTenant={navigateToTenantList}
      onBackToTenants={navigateToTenantList}
      onSignOut={() => void adminClient.signOut()}
    />
  );
}

interface ClubAreaProps {
  clubClient: ClubApiClient;
  route: ClubRoute;
  session: StaffSession;
  onNavigate: (route: ClubRoute, path: string) => void;
  onSignOut: () => void;
}

const ROLE_LABEL: Record<'owner' | 'manager', string> = {
  owner: 'Владелец',
  manager: 'Менеджер'
};

const CLUB_SCREEN_TITLE: Partial<Record<ClubRoute['kind'], string>> = {
  clubDashboard: 'Обзор',
  clubVenue: 'Зал и ПК',
  clubClients: 'Клиенты',
  clubMonetization: 'Монетизация',
  clubReports: 'Отчёты',
  clubJournal: 'Журнал',
  clubSettings: 'Настройки',
  clubInstall: 'Установка',
  clubProfile: 'Профиль',
  clubBranches: 'Все филиалы',
};

/**
 * Maps a {@link ClubRoute} to the nav `path` used by the new AppShell sidebar
 * for highlighting the active item. Branch-scoped legacy routes do not have a
 * dedicated nav item yet, so they fall back to the overview path.
 */
export function pathForRoute(route: ClubRoute): string {
  switch (route.kind) {
    case 'clubDashboard':
      return '/club';
    case 'clubVenue':
      return '/club/venue';
    case 'clubClients':
      return '/club/clients';
    case 'clubMonetization':
      return '/club/monetization';
    case 'clubReports':
      return '/club/reports';
    case 'clubJournal':
      return '/club/journal';
    case 'clubSettings':
      return '/club/settings';
    case 'clubInstall':
      return '/club/install';
    case 'clubProfile':
      return '/club/profile';
    case 'clubBranches':
      return '/club/branches';
    default:
      return '/club';
  }
}

function ClubArea({ clubClient, route, session, onNavigate, onSignOut }: ClubAreaProps) {
  const role = roleFromPermissions(session.permissions);
  const { t } = useI18n();
  const { activeBranchId, select } = useActiveBranch(session.branchIds);
  const directory = useBranchDirectory(clubClient, session.branchIds);
  const branches = resolveBranchNames(session.branchIds, directory, t('branches.unnamed'));
  const overviewState = useOverview(clubClient, activeBranchId);

  const handleNavigate = (path: string) => {
    const resolution = resolvePlatformRoute(path, null, '');
    if (isClubRoute(resolution.route)) {
      onNavigate(resolution.route, resolution.redirectTo ?? path);
    }
    // Soon/unbuilt nav targets resolve to notFound and are ignored; live targets
    // (overview, venue, install, branches) navigate.
  };

  return (
    <AppShell
      navGroups={visibleNav(role)}
      sidebarHeader={
        <BranchSwitcher
          orgName={session.displayName}
          branches={branches}
          activeBranchId={activeBranchId}
          onSelect={select}
        />
      }
      activePath={pathForRoute(route)}
      subtitle={branches.find(b => b.branchId === activeBranchId)?.name ?? ''}
      screenTitle={CLUB_SCREEN_TITLE[route.kind] ?? ''}
      userName={session.displayName}
      roleLabel={ROLE_LABEL[role]}
      onNavigate={handleNavigate}
      onSignOut={onSignOut}
    >
      {route.kind === 'clubDashboard' ? (
        <OverviewScreen state={overviewState} />
      ) : route.kind === 'clubVenue' ? (
        <VenueScreen
          client={clubClient}
          branchId={activeBranchId}
          organizationId={session.organizationId}
          canManageLayout={session.permissions.includes('layout.manage')}
        />
      ) : route.kind === 'clubClients' ? (
        session.permissions.includes('players.view') ? (
          <ClientsScreen
            client={clubClient}
            branchId={activeBranchId}
            organizationId={session.organizationId}
            canCreate={session.permissions.includes('players.create')}
            canViewBilling={session.permissions.includes('billing.view')}
            moneyPerms={{
              topUp: session.permissions.includes('billing.wallet.top_up'),
              payDebt: session.permissions.includes('billing.debt.pay'),
              correct: session.permissions.includes('billing.manual_correction'),
              refund: session.permissions.includes('billing.refund')
            }}
            canPurchase={session.permissions.includes('packages.purchase')}
          />
        ) : (
          <EmptyState message={t('clients.noAccess')} />
        )
      ) : route.kind === 'clubMonetization' ? (
        role === 'owner' ? (
          <MonetizationScreen
            client={clubClient}
            branchId={activeBranchId}
            organizationId={session.organizationId}
            canManageTariffs={session.permissions.includes('tariffs.manage')}
            canManageCatalog={session.permissions.includes('pos.catalog.manage')}
            canManagePackages={session.permissions.includes('packages.manage')}
          />
        ) : (
          <EmptyState message={t('monetization.ownerOnly')} />
        )
      ) : route.kind === 'clubSettings' ? (
        role === 'owner' ? (
          <SettingsScreen
            client={clubClient}
            branchId={activeBranchId}
            organizationId={session.organizationId}
            currentStaffUserId={session.staffUserId}
          />
        ) : (
          <EmptyState message={t('settings.ownerOnly')} />
        )
      ) : route.kind === 'clubReports' ? (
        session.permissions.includes('reports.view') ? (
          <ReportsScreen client={clubClient} branchId={activeBranchId} />
        ) : (
          <EmptyState message={t('reports.noAccess')} />
        )
      ) : route.kind === 'clubJournal' ? (
        session.permissions.includes('audit.view') ? (
          <JournalScreen client={clubClient} branchId={activeBranchId} />
        ) : (
          <EmptyState message={t('journal.noAccess')} />
        )
      ) : route.kind === 'clubBranches' ? (
        <BranchesScreen
          client={clubClient}
          branchIds={session.branchIds}
          organizationId={session.organizationId}
          onOpenBranch={(id) => { select(id); onNavigate({ kind: 'clubDashboard' }, '/club'); }}
        />
      ) : route.kind === 'clubProfile' ? (
        <ProfileScreen
          session={session}
          branches={branches}
          roleLabel={ROLE_LABEL[role]}
          onSignOut={onSignOut}
        />
      ) : (
        <InstallScreen
          client={clubClient}
          canManage={session.permissions.includes('identity.owner_code.manage')}
          branches={session.branchIds.map(id => ({ branchId: id, name: directory[id]?.name ?? t('branches.unnamed'), city: directory[id]?.city }))}
        />
      )}
    </AppShell>
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

const PLATFORM_ROLE_LABEL = 'Администратор';

const PLATFORM_SCREEN_TITLE: Record<AdminRoute['kind'], string> = {
  adminOverview: 'Обзор',
  adminBilling: 'Биллинг',
  tenantList: 'Тенанты',
  newTenant: 'Новый тенант',
  tenantDetail: 'Тенант'
};

function pathForAdminRoute(route: AdminRoute): string {
  switch (route.kind) {
    case 'adminOverview':
      return '/admin';
    case 'adminBilling':
      return '/admin/billing';
    case 'tenantList':
    case 'newTenant':
    case 'tenantDetail':
      return '/admin/tenants';
    default:
      return '/admin';
  }
}

function PlatformArea({
  adminClient, route, session, onNavigate, onCreateTenant, onOpenTenant,
  onCreatedTenant, onCancelNewTenant, onBackToTenants, onSignOut
}: PlatformAreaProps) {
  const metricsState = useTenantMetrics(adminClient);
  const billingMetricsState = useBillingMetrics(adminClient);

  const handleNavigate = (path: string) => {
    const resolution = resolvePlatformRoute(path, null, '');
    if (isAdminRoute(resolution.route)) {
      onNavigate(resolution.route, resolution.redirectTo ?? path);
    }
    // Soon/unbuilt nav targets (profile) resolve to notFound and are ignored; live targets
    // (overview, billing, tenants) navigate.
  };

  return (
    <AppShell
      navGroups={platformNav}
      sidebarHeader={
        <div className="m-3 flex items-center gap-3 rounded-lg border border-border bg-card px-3 py-2 text-left">
          <span className="flex size-7 items-center justify-center rounded-md bg-primary text-xs font-bold text-primary-foreground">
            A
          </span>
          <span className="min-w-0">
            <span className="block truncate text-sm font-bold">AFK4 Control Plane</span>
            <span className="block truncate text-[11px] text-muted">{session.userName}</span>
          </span>
        </div>
      }
      activePath={pathForAdminRoute(route)}
      subtitle=""
      screenTitle={PLATFORM_SCREEN_TITLE[route.kind] ?? ''}
      userName={session.displayName}
      roleLabel={PLATFORM_ROLE_LABEL}
      onNavigate={handleNavigate}
      onSignOut={onSignOut}
    >
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} billing={billingMetricsState} />
      ) : route.kind === 'adminBilling' ? (
        <PlatformBillingScreen client={adminClient} />
      ) : route.kind === 'newTenant' ? (
        <NewTenant
          client={adminClient}
          onCreated={onCreatedTenant}
          onCancel={onCancelNewTenant}
        />
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
  search = '',
  audience: PlatformWebAudience = defaultAudience
): RouteResolution {
  const path = normalizePath(pathname);

  if (path === '/') {
    const home = getAudienceHome(audience);
    return { route: home.route, redirectTo: home.path };
  }

  if (allowsAdminRoutes(audience)) {
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

    if (path === '/admin') {
      return { route: { kind: 'adminOverview' } };
    }
    if (path === '/admin/billing') {
      return { route: { kind: 'adminBilling' } };
    }
    if (path === '/admin/tenants') {
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
  }

  if (path === '/auth') {
    return { route: { kind: 'staffSignIn', tenantKey: null }, redirectTo: '/auth/sign-in' };
  }
  if (path === '/auth/sign-in') {
    return { route: { kind: 'staffSignIn', tenantKey: readQueryValue(search, 'tenantKey') } };
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

  if (allowsClubRoutes(audience)) {
    if (path === '/club') {
      return { route: { kind: 'clubDashboard' } };
    }
    if (path === '/club/venue') {
      return { route: { kind: 'clubVenue' } };
    }
    if (path === '/club/clients') {
      return { route: { kind: 'clubClients' } };
    }
    if (path === '/club/monetization') {
      return { route: { kind: 'clubMonetization' } };
    }
    if (path === '/club/reports') {
      return { route: { kind: 'clubReports' } };
    }
    if (path === '/club/journal') {
      return { route: { kind: 'clubJournal' } };
    }
    if (path === '/club/settings') {
      return { route: { kind: 'clubSettings' } };
    }
    if (path === '/club/install') {
      return { route: { kind: 'clubInstall' } };
    }
    if (path === '/club/profile') {
      return { route: { kind: 'clubProfile' } };
    }
    if (path === '/club/branches') {
      return { route: { kind: 'clubBranches' } };
    }
  }

  return { route: { kind: 'notFound', path } };
}

export function readPlatformWebAudience(
  env: Record<string, string | undefined> | undefined = (
    import.meta as unknown as { env?: Record<string, string | undefined> }
  ).env
): PlatformWebAudience {
  const value = env?.VITE_AUDIENCE?.trim().toLowerCase();
  if (value === 'admin' || value === 'club' || value === 'all') {
    return value;
  }
  return 'all';
}

function readCurrentRoute(audience: PlatformWebAudience): AppRoute {
  if (typeof window === 'undefined') {
    return getAudienceHome(audience).route;
  }
  return resolvePlatformRoute(
    window.location.pathname,
    window.history.state,
    window.location.search,
    audience
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
  return route.kind === 'adminOverview'
    || route.kind === 'adminBilling'
    || route.kind === 'tenantList'
    || route.kind === 'newTenant'
    || route.kind === 'tenantDetail';
}

function isClubRoute(route: AppRoute): route is ClubRoute {
  return route.kind === 'clubDashboard'
    || route.kind === 'clubVenue'
    || route.kind === 'clubClients'
    || route.kind === 'clubMonetization'
    || route.kind === 'clubReports'
    || route.kind === 'clubJournal'
    || route.kind === 'clubSettings'
    || route.kind === 'clubInstall'
    || route.kind === 'clubProfile'
    || route.kind === 'clubBranches';
}

function routeForAudience(route: AppRoute, path: string, audience: PlatformWebAudience): AppRoute {
  if (isRouteAllowedForAudience(route, audience)) {
    return route;
  }
  return { kind: 'notFound', path: normalizePath(path) };
}

function isRouteAllowedForAudience(route: AppRoute, audience: PlatformWebAudience): boolean {
  if (route.kind === 'notFound' || isAuthRoute(route)) {
    return true;
  }
  if (isAdminRoute(route)) {
    return allowsAdminRoutes(audience);
  }
  if (isClubRoute(route)) {
    return allowsClubRoutes(audience);
  }
  return false;
}

function isAuthRoute(route: AppRoute): route is AuthRoute {
  return route.kind === 'acceptInvite'
    || route.kind === 'staffSignIn'
    || route.kind === 'forgotPassword'
    || route.kind === 'resetPassword';
}

function allowsAdminRoutes(audience: PlatformWebAudience): boolean {
  return audience === 'all' || audience === 'admin';
}

function allowsClubRoutes(audience: PlatformWebAudience): boolean {
  return audience === 'all' || audience === 'club';
}

function getAudienceHome(audience: PlatformWebAudience): { route: AppRoute; path: string; label: string } {
  if (audience === 'club') {
    return { route: { kind: 'clubInstall' }, path: '/club/install', label: 'Open club install' };
  }
  return { route: { kind: 'adminOverview' }, path: '/admin', label: 'Open admin overview' };
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

function NotFound({
  path,
  homeLabel,
  onHome
}: {
  path: string;
  homeLabel: string;
  onHome: () => void;
}) {
  return (
    <main>
      <div className="page page-narrow">
        <div className="page-header">
          <h1>Page not found</h1>
        </div>
        <section className="section">
          <p className="muted">No Platform Web route matches <code>{path}</code>.</p>
          <button type="button" className="primary" onClick={onHome}>{homeLabel}</button>
        </section>
      </div>
    </main>
  );
}
