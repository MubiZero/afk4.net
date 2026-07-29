export type OrganizationTab =
  | 'summary'
  | 'clubs'
  | 'access'
  | 'subscription'
  | 'invoices'
  | 'support'
  | 'history';

export type BillingTab = 'plans' | 'subscriptions' | 'invoices';

export type PlatformRoute =
  | { kind: 'overview' }
  | { kind: 'organizations'; query: string; status: string; plan: string; sort: string }
  | { kind: 'organization'; organizationId: string; tab: OrganizationTab }
  | { kind: 'organizationNew' }
  | { kind: 'billing'; tab: BillingTab }
  | { kind: 'updates' }
  | { kind: 'audit' }
  | { kind: 'settings' }
  | { kind: 'profile' }
  | { kind: 'notFound'; path: string };

const ORGANIZATION_TABS = new Set<OrganizationTab>([
  'summary', 'clubs', 'access', 'subscription', 'invoices', 'support', 'history'
]);
const BILLING_TABS = new Set<BillingTab>(['plans', 'subscriptions', 'invoices']);

export function resolvePlatformRoute(pathname: string, search = ''): PlatformRoute {
  const path = normalizePath(pathname);
  const query = new URLSearchParams(search.startsWith('?') ? search.slice(1) : search);

  if (path === '/' || path === '/admin') return { kind: 'overview' };
  if (path === '/admin/organizations') {
    return {
      kind: 'organizations',
      query: query.get('q') ?? '',
      status: query.get('status') ?? 'all',
      plan: query.get('plan') ?? 'all',
      sort: query.get('sort') ?? 'attention'
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
        : 'summary'
    };
  }

  if (path === '/admin/billing') {
    const requestedTab = query.get('tab');
    return {
      kind: 'billing',
      tab: requestedTab !== null && BILLING_TABS.has(requestedTab as BillingTab)
        ? requestedTab as BillingTab
        : 'plans'
    };
  }
  if (path === '/admin/updates') return { kind: 'updates' };
  if (path === '/admin/audit') return { kind: 'audit' };
  if (path === '/admin/settings') return { kind: 'settings' };
  if (path === '/admin/profile') return { kind: 'profile' };
  return { kind: 'notFound', path };
}

export function pathForPlatformRoute(route: PlatformRoute): string {
  switch (route.kind) {
    case 'overview': return '/admin';
    case 'organizations': {
      const query = new URLSearchParams();
      if (route.query !== '') query.set('q', route.query);
      if (route.status !== 'all') query.set('status', route.status);
      if (route.plan !== 'all') query.set('plan', route.plan);
      if (route.sort !== 'attention') query.set('sort', route.sort);
      const suffix = query.toString();
      return `/admin/organizations${suffix === '' ? '' : `?${suffix}`}`;
    }
    case 'organization':
      return `/admin/organizations/${encodeURIComponent(route.organizationId)}${route.tab === 'summary' ? '' : `?tab=${route.tab}`}`;
    case 'organizationNew': return '/admin/organizations/new';
    case 'billing': return `/admin/billing${route.tab === 'plans' ? '' : `?tab=${route.tab}`}`;
    case 'updates': return '/admin/updates';
    case 'audit': return '/admin/audit';
    case 'settings': return '/admin/settings';
    case 'profile': return '/admin/profile';
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
