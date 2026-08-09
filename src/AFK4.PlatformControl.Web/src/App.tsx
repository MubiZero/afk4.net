import { lazy, Suspense, useCallback, useEffect, useMemo, useState } from 'react';
import { AccountActivation } from './account-activation/AccountActivation';
import { AccountActivationApi } from './account-activation/accountActivationApi';
import { PlatformApiClient } from './api/platformApi';
import type { CreateOrganizationResponse, OrganizationOwnerInvite } from './api/types';
import { can, type PlatformCapability } from './auth/platformAccess';
import { readSession, type PlatformAdminSession } from './auth/tokenStore';
import { SignIn } from './components/SignIn';
import { AppShell } from './components/shell/AppShell';
import { ForbiddenState, LoadingCards } from './components/ui/states';
import { Page } from './components/layout/Page';
import { useI18n } from './i18n/I18nProvider';
import { buildPlatformNav } from './platform/nav';
import { GlobalSearch } from './platform/search/GlobalSearch';
import {
  pathForPlatformRoute,
  resolvePlatformRoute,
  type PlatformRoute
} from './routing/platformRoute';

const BillingScreen = lazy(() => import('./platform/billing/BillingScreen').then(module => ({ default: module.BillingScreen })));
const ClubsScreen = lazy(() => import('./platform/clubs/ClubsScreen').then(module => ({ default: module.ClubsScreen })));
const NewOrganizationScreen = lazy(() => import('./platform/organizations/NewOrganizationScreen').then(module => ({ default: module.NewOrganizationScreen })));
const OrganizationPage = lazy(() => import('./platform/organizations/OrganizationPage').then(module => ({ default: module.OrganizationPage })));
const UpdatesScreen = lazy(() => import('./platform/updates/UpdatesScreen').then(module => ({ default: module.UpdatesScreen })));
const AuditScreen = lazy(() => import('./platform/audit/AuditScreen').then(module => ({ default: module.AuditScreen })));
const SettingsScreen = lazy(() => import('./platform/settings/SettingsScreen').then(module => ({ default: module.SettingsScreen })));
const HealthScreen = lazy(() => import('./platform/health/HealthScreen').then(module => ({ default: module.HealthScreen })));

type AppRoute = PlatformRoute | { kind: 'accountActivation'; code: string | null };

export interface AppProps { apiBaseUrl: string; }

export default function App({ apiBaseUrl }: AppProps) {
  const [session, setSession] = useState<PlatformAdminSession | null>(() => readSession());
  const [route, setRoute] = useState<AppRoute>(readCurrentRoute);
  const client = useMemo(() => new PlatformApiClient({
    baseUrl: apiBaseUrl,
    session,
    onSessionChanged: setSession
  // The client owns refresh state and reports authoritative session changes.
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }), [apiBaseUrl]);
  const activationClient = useMemo(() => new AccountActivationApi({ baseUrl: apiBaseUrl }), [apiBaseUrl]);

  useEffect(() => {
    const sync = () => setRoute(readCurrentRoute());
    if (window.location.pathname === '/') {
      window.history.replaceState(null, '', '/admin');
      setRoute({ kind: 'overview', view: 'now' });
    }
    window.addEventListener('popstate', sync);
    return () => window.removeEventListener('popstate', sync);
  }, []);

  const navigate = useCallback((next: PlatformRoute, state: unknown = null) => {
    window.history.pushState(state, '', pathForPlatformRoute(next));
    setRoute(next);
  }, []);

  if (route.kind === 'accountActivation') {
    return <AccountActivation client={activationClient} initialCode={route.code} />;
  }
  if (route.kind === 'notFound') return <NotFound path={route.path} onHome={() => navigate({ kind: 'overview', view: 'now' })} />;
  if (session === null) return <SignIn client={client} onSignedIn={() => setSession(client.getSession())} />;

  const requiredCapability = capabilityForRoute(route);
  if (requiredCapability !== null && !can(session, requiredCapability)) {
    return <Forbidden onHome={() => navigate({ kind: 'overview', view: 'now' })} />;
  }

  return <PlatformArea client={client} route={route} session={session} navigate={navigate} onSignOut={() => void client.signOut()} />;
}

function PlatformArea({ client, route, session, navigate, onSignOut }: {
  client: PlatformApiClient;
  route: Exclude<PlatformRoute, { kind: 'notFound' }>;
  session: PlatformAdminSession;
  navigate: (route: PlatformRoute, state?: unknown) => void;
  onSignOut: () => void;
}) {
  const { t } = useI18n();
  const openOrganization = (organizationId: string, initialInvite: OrganizationOwnerInvite | null = null) =>
    navigate({ kind: 'organization', organizationId, tab: initialInvite === null ? 'clubs' : 'access' }, { initialInvite });
  const organizationAccess = {
    canManageOrganization: can(session, 'organizations.manage'),
    canManageAccess: session.permissions.includes('platform.organizations.owner_invites.manage'),
    canViewSupport: session.permissions.some(permission => permission === 'platform.organizations.support_notes.view' || permission === 'platform.organizations.support_notes.manage'),
    canViewBilling: can(session, 'billing.read'),
    canManageBilling: can(session, 'billing.manage'),
    canManageProfile: can(session, 'organizations.profile.manage'),
    canManageUpdateChannel: can(session, 'organizations.update_channel.manage'),
    canManageFeatures: can(session, 'organizations.features.manage'),
    canTransferOwner: can(session, 'organizations.owner_transfer.manage'),
    canViewAudit: can(session, 'audit.read')
  };

  return (
    <AppShell
      navItems={buildPlatformNav(session)}
      activePath={activePath(route)}
      userName={session.displayName}
      roleLabel={t('platform.profile.roleLabel')}
      permissions={session.permissions}
      search={can(session, 'organizations.read') ? <GlobalSearch client={client.search} onNavigate={path => { const url = new URL(path, window.location.origin); navigate(resolvePlatformRoute(url.pathname, url.search)); }} /> : null}
      onNavigate={path => navigate(resolvePlatformRoute(new URL(path, window.location.origin).pathname, new URL(path, window.location.origin).search))}
      onSignOut={onSignOut}
    >
      <Suspense fallback={<LoadingCards count={3} />}>{route.kind === 'overview' ? <ClubsScreen client={client} view={route.view} onViewChange={view => navigate({ kind: 'overview', view })} onOpenOrganization={id => openOrganization(id)} />
        : route.kind === 'billing' ? <BillingScreen
            client={client}
            tab={route.tab}
            onTabChange={tab => navigate({ ...route, tab })}
            canManage={can(session, 'billing.manage')}
            debtAccess={{
              canMarkPaid: can(session, 'billing.invoices.manage'),
              canGrantGrace: can(session, 'billing.subscriptions.manage'),
              canToggleStatus: can(session, 'organizations.status.manage'),
              canAddNote: can(session, 'organizations.support_notes.manage')
            }}
          />
        : route.kind === 'updates' ? <UpdatesScreen client={client.updates} organizationsClient={client.organizations} />
        : route.kind === 'audit' ? <AuditScreen client={client.audit} filters={route} onFiltersChange={filters => navigate({ kind: 'audit', ...filters })} />
        : route.kind === 'organizationNew' ? <NewOrganizationScreen client={client.organizations} onCreated={(response: CreateOrganizationResponse) => openOrganization(response.organization.organizationId, response.organizationOwnerInvite)} onCancel={() => navigate({ kind: 'overview', view: 'now' })} />
        : route.kind === 'organization' ? <OrganizationPage client={client} organizationId={route.organizationId} tab={route.tab} access={organizationAccess} initialInvite={readInitialInvite()} onTabChange={tab => navigate({ ...route, tab })} onBack={() => navigate({ kind: 'overview', view: 'now' })} onChanged={() => {}} />
        : route.kind === 'settings' ? <SettingsScreen client={client.admins} twoFactorClient={client.twoFactor} rolesClient={client.roles} session={session} />
        : route.kind === 'health' ? <HealthScreen client={client.health} />
        : <UnavailableScreen />}</Suspense>
    </AppShell>
  );
}

function capabilityForRoute(route: Exclude<PlatformRoute, { kind: 'notFound' }>): PlatformCapability | null {
  switch (route.kind) {
    case 'organization': return 'organizations.read';
    case 'organizationNew': return 'organizations.manage';
    case 'billing': return 'billing.read';
    case 'updates': return 'updates.read';
    case 'audit': return 'audit.read';
    case 'settings': return 'admins.manage';
    case 'health': return 'health.read';
    case 'overview': return null;
  }
}

function activePath(route: Exclude<PlatformRoute, { kind: 'notFound' }>): string {
  if (route.kind === 'organization' || route.kind === 'organizationNew') return '/admin';
  return pathForPlatformRoute(route).split('?')[0];
}

function readCurrentRoute(): AppRoute {
  if (window.location.pathname === '/account-activation') {
    return { kind: 'accountActivation', code: new URLSearchParams(window.location.search).get('code') };
  }
  return resolvePlatformRoute(window.location.pathname, window.location.search);
}

function readInitialInvite(): OrganizationOwnerInvite | null {
  const candidate = window.history.state?.initialInvite as unknown;
  return candidate !== null && typeof candidate === 'object' ? candidate as OrganizationOwnerInvite : null;
}

function NotFound({ path: _path, onHome }: { path: string; onHome: () => void }) {
  const { t } = useI18n();
  return <main className="pc-workspace"><Page width="form"><ForbiddenState title={t('state.notFound.title')} message={t('state.notFound.message')} actionLabel={t('state.openOverview')} onAction={onHome} /></Page></main>;
}

function Forbidden({ onHome }: { onHome: () => void }) {
  const { t } = useI18n();
  return <main className="pc-workspace"><Page width="form"><ForbiddenState title={t('state.forbidden.title')} message={t('state.forbidden.message')} actionLabel={t('state.openOverview')} onAction={onHome} /></Page></main>;
}

function UnavailableScreen() {
  const { t } = useI18n();
  return <p className="mgmt-drawer-hint">{t('state.unavailable')}</p>;
}
