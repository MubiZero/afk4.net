import type { PulseView } from '@/platform/clubs/pulseModel';

export type OrganizationTab =
  | 'clubs'
  | 'invoices'
  | 'limits'
  | 'updates'
  | 'access'
  | 'history'
  | 'dynamics'
  | 'features'
  | 'offboarding';

export type BillingTab = 'plans' | 'subscriptions' | 'invoices' | 'analytics';

export type PlatformRoute =
  | { kind: 'overview'; view: PulseView }
  | { kind: 'organization'; organizationId: string; tab: OrganizationTab }
  | { kind: 'organizationNew' }
  | { kind: 'billing'; tab: BillingTab }
  | { kind: 'updates' }
  | { kind: 'audit'; organizationId: string; action: string; outcome: string; from: string; to: string }
  | { kind: 'settings' }
  | { kind: 'announcements' }
  | { kind: 'people' }
  | { kind: 'health' }
  | { kind: 'notFound'; path: string };

const ORGANIZATION_TABS = new Set<OrganizationTab>([
  'clubs', 'invoices', 'limits', 'updates', 'access', 'history', 'dynamics', 'features', 'offboarding'
]);
const BILLING_TABS = new Set<BillingTab>(['plans', 'subscriptions', 'invoices', 'analytics']);
const PULSE_VIEWS = new Set<PulseView>(['now', 'all', 'debt']);

export function resolvePlatformRoute(pathname: string, search = ''): PlatformRoute {
  const path = normalizePath(pathname);
  const query = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search);

  // '/admin/organizations' is a retired registry-list bookmark; it now opens
  // the same fleet pulse screen as the root, defaulting to the "now" view.
  if (path === '/' || path === '/admin' || path === '/admin/organizations') {
    const requestedView = query.get('view');
    return {
      kind: 'overview',
      view: requestedView !== null && PULSE_VIEWS.has(requestedView as PulseView) ? requestedView as PulseView : 'now'
    };
  }
  if (path === '/admin/organizations/new') return { kind: 'organizationNew' };

  const organizationMatch = /^\/admin\/organizations\/([^/]+)$/u.exec(path);
  if (organizationMatch !== null) {
    const requestedTab = query.get('tab');
    return {
      kind: 'organization',
      organizationId: decodeSegment(organizationMatch[1]),
      tab: requestedTab !== null && ORGANIZATION_TABS.has(requestedTab as OrganizationTab)
        ? requestedTab as OrganizationTab
        : 'clubs'
    };
  }

  if (path === '/admin/money') {
    const requestedTab = query.get('tab');
    return {
      kind: 'billing',
      tab: requestedTab !== null && BILLING_TABS.has(requestedTab as BillingTab)
        ? requestedTab as BillingTab
        : 'plans'
    };
  }
  if (path === '/admin/updates') return { kind: 'updates' };
  if (path === '/admin/journal') return { kind: 'audit', organizationId: query.get('organizationId') ?? '', action: query.get('action') ?? '', outcome: query.get('outcome') ?? '', from: query.get('from') ?? '', to: query.get('to') ?? '' };
  if (path === '/admin/settings') return { kind: 'settings' };
  if (path === '/admin/announcements') return { kind: 'announcements' };
  if (path === '/admin/people') return { kind: 'people' };
  if (path === '/admin/health') return { kind: 'health' };
  // '/admin/profile' — закладка на удалённый экран профиля: учётная запись переехала в меню
  // аккаунта в подвале рейла, поэтому старая ссылка ведёт на главный экран, а не в 404.
  if (path === '/admin/profile') return { kind: 'overview', view: 'now' };
  return { kind: 'notFound', path };
}

export function pathForPlatformRoute(route: PlatformRoute): string {
  switch (route.kind) {
    case 'overview': return `/admin${route.view === 'now' ? '' : `?view=${route.view}`}`;
    case 'organization':
      return `/admin/organizations/${encodeURIComponent(route.organizationId)}${route.tab === 'clubs' ? '' : `?tab=${route.tab}`}`;
    case 'organizationNew': return '/admin/organizations/new';
    case 'billing': return `/admin/money${route.tab === 'plans' ? '' : `?tab=${route.tab}`}`;
    case 'updates': return '/admin/updates';
    case 'audit': {
      const query = new URLSearchParams();
      if (route.organizationId) query.set('organizationId', route.organizationId);
      if (route.action) query.set('action', route.action);
      if (route.outcome) query.set('outcome', route.outcome);
      if (route.from) query.set('from', route.from);
      if (route.to) query.set('to', route.to);
      return `/admin/journal${query.size === 0 ? '' : `?${query}`}`;
    }
    case 'settings': return '/admin/settings';
    case 'announcements': return '/admin/announcements';
    case 'people': return '/admin/people';
    case 'health': return '/admin/health';
    case 'notFound': return route.path;
  }
}

function normalizePath(pathname: string): string {
  const leading = pathname.startsWith('/') ? pathname : `/${pathname}`;
  return leading.replace(/\/+$/u, '') || '/';
}

function decodeSegment(segment: string): string {
  try { return decodeURIComponent(segment); } catch { return segment; }
}
