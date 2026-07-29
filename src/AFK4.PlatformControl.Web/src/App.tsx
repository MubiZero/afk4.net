import { useCallback, useEffect, useMemo, useState } from 'react';
import { AccountActivation } from './account-activation/AccountActivation';
import { AccountActivationApi } from './account-activation/accountActivationApi';
import { PlatformApiClient } from './api/platformApi';
import type { CreateOrganizationResponse, OrganizationOwnerInvite } from './api/types';
import { can, type PlatformCapability } from './auth/platformAccess';
import { readSession, type PlatformAdminSession } from './auth/tokenStore';
import { SignIn } from './components/SignIn';
import { AppShell } from './components/shell/AppShell';
import { ForbiddenState } from './components/ui/states';
import { Workspace } from './components/layout/Workspace';
import { useI18n, type MessageKey } from './i18n/I18nProvider';
import { BillingScreen } from './platform/billing/BillingScreen';
import { useBillingMetrics } from './platform/billing/useBillingMetrics';
import { buildPlatformNav } from './platform/nav';
import { NewOrganizationScreen } from './platform/organizations/NewOrganizationScreen';
import { OrganizationPage } from './platform/organizations/OrganizationPage';
import { OrganizationsScreen } from './platform/organizations/OrganizationsScreen';
import { OverviewScreen } from './platform/overview/OverviewScreen';
import { useOrganizationMetrics } from './platform/overview/useOrganizationMetrics';
import { ProfileScreen } from './platform/profile/ProfileScreen';
import { UpdatesScreen } from './platform/updates/UpdatesScreen';
import {
  pathForPlatformRoute,
  resolvePlatformRoute,
  type PlatformRoute
} from './routing/platformRoute';

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
      setRoute({ kind: 'overview' });
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
  if (route.kind === 'notFound') return <NotFound path={route.path} onHome={() => navigate({ kind: 'overview' })} />;
  if (session === null) return <SignIn client={client} onSignedIn={() => setSession(client.getSession())} />;

  const requiredCapability = capabilityForRoute(route);
  if (requiredCapability !== null && !can(session, requiredCapability)) {
    return <Forbidden onHome={() => navigate({ kind: 'overview' })} />;
  }

  return <PlatformArea client={client} route={route} session={session} navigate={navigate} onSignOut={() => void client.signOut()} />;
}

const TITLE_KEYS: Record<Exclude<PlatformRoute['kind'], 'notFound'>, MessageKey> = {
  overview: 'nav.platform.overview',
  organizations: 'nav.platform.organizations',
  organization: 'platform.organization.title',
  organizationNew: 'platform.organizations.new',
  billing: 'nav.platform.billing',
  updates: 'nav.platform.updates',
  audit: 'nav.platform.audit',
  settings: 'nav.platform.settings',
  profile: 'nav.platform.profile'
};

function PlatformArea({ client, route, session, navigate, onSignOut }: {
  client: PlatformApiClient;
  route: Exclude<PlatformRoute, { kind: 'notFound' }>;
  session: PlatformAdminSession;
  navigate: (route: PlatformRoute, state?: unknown) => void;
  onSignOut: () => void;
}) {
  const { t } = useI18n();
  const organizationMetrics = useOrganizationMetrics(client.organizations);
  const billingMetrics = useBillingMetrics(client.invoices);
  const openOrganization = (organizationId: string, initialInvite: OrganizationOwnerInvite | null = null) =>
    navigate({ kind: 'organization', organizationId, tab: initialInvite === null ? 'summary' : 'access' }, { initialInvite });
  const organizationAccess = {
    canManageOrganization: can(session, 'organizations.manage'),
    canManageAccess: session.permissions.includes('platform.organizations.owner_invites.manage'),
    canViewSupport: session.permissions.some(permission => permission === 'platform.organizations.support_notes.view' || permission === 'platform.organizations.support_notes.manage'),
    canViewBilling: can(session, 'billing.read'),
    canViewAudit: can(session, 'audit.read')
  };

  return (
    <AppShell
      navGroups={buildPlatformNav(session)}
      sidebarHeader={<div className="m-3 flex items-center gap-3 rounded-lg border border-border bg-card px-3 py-2"><img src="/favicon.svg" alt="" className="size-7 rounded-md" /><span className="min-w-0"><span className="block truncate text-sm font-bold">Platform Control</span><span className="block truncate text-[11px] text-muted">{session.userName}</span></span></div>}
      activePath={activePath(route)}
      subtitle=""
      screenTitle={t(TITLE_KEYS[route.kind])}
      userName={session.displayName}
      roleLabel={t('platform.profile.roleLabel')}
      onNavigate={path => navigate(resolvePlatformRoute(new URL(path, window.location.origin).pathname, new URL(path, window.location.origin).search))}
      onSignOut={onSignOut}
    >
      {route.kind === 'overview' ? <OverviewScreen state={organizationMetrics} billing={billingMetrics} />
        : route.kind === 'billing' ? <BillingScreen client={client} />
        : route.kind === 'updates' ? <UpdatesScreen client={client.updates} />
        : route.kind === 'profile' ? <ProfileScreen session={session} onSignOut={onSignOut} />
        : route.kind === 'organizationNew' ? <NewOrganizationScreen client={client.organizations} onCreated={(response: CreateOrganizationResponse) => openOrganization(response.organization.organizationId, response.organizationOwnerInvite)} onCancel={() => navigate({ kind: 'organizations', query: '', status: 'all', plan: 'all', sort: 'attention' })} />
        : route.kind === 'organization' ? <OrganizationPage client={client} organizationId={route.organizationId} tab={route.tab} access={organizationAccess} initialInvite={readInitialInvite()} onTabChange={tab => navigate({ ...route, tab })} onChanged={() => {}} />
        : route.kind === 'organizations' ? <OrganizationsScreen client={client} selectedOrganizationId={null} initialInvite={null} query={route.query} statusFilter={route.status} planFilter={route.plan} sort={route.sort} onQueryChange={change => navigate({ kind: 'organizations', query: change.query ?? route.query, status: change.statusFilter ?? route.status, plan: change.planFilter ?? route.plan, sort: change.sort ?? route.sort })} onOpenOrganization={id => openOrganization(id)} onCloseOrganization={() => navigate({ kind: 'organizations', query: '', status: 'all', plan: 'all', sort: 'attention' })} onCreateOrganization={() => navigate({ kind: 'organizationNew' })} />
        : <UnavailableScreen />}
    </AppShell>
  );
}

function capabilityForRoute(route: Exclude<PlatformRoute, { kind: 'notFound' }>): PlatformCapability | null {
  switch (route.kind) {
    case 'organizations': case 'organization': return 'organizations.read';
    case 'organizationNew': return 'organizations.manage';
    case 'billing': return 'billing.read';
    case 'updates': return 'updates.read';
    case 'audit': return 'audit.read';
    case 'settings': return 'settings.manage';
    case 'overview': case 'profile': return null;
  }
}

function activePath(route: Exclude<PlatformRoute, { kind: 'notFound' }>): string {
  if (route.kind === 'organization' || route.kind === 'organizationNew') return '/admin/organizations';
  if (route.kind === 'billing') return '/admin/billing';
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
  return <main className="min-h-screen p-5"><Workspace width="narrow"><ForbiddenState title={t('state.notFound.title')} message={t('state.notFound.message')} actionLabel={t('state.openOverview')} onAction={onHome} /></Workspace></main>;
}

function Forbidden({ onHome }: { onHome: () => void }) {
  const { t } = useI18n();
  return <main className="min-h-screen p-5"><Workspace width="narrow"><ForbiddenState title={t('state.forbidden.title')} message={t('state.forbidden.message')} actionLabel={t('state.openOverview')} onAction={onHome} /></Workspace></main>;
}

function UnavailableScreen() {
  const { t } = useI18n();
  return <p className="text-sm text-muted-foreground">{t('state.unavailable')}</p>;
}
