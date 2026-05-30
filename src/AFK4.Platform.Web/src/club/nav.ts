import type { MessageKey } from '@/i18n/messages';

export type ClubRole = 'owner' | 'manager';
export type NavGroupKey = 'branch' | 'account';

export interface NavItem {
  key: string;
  labelKey: MessageKey;
  path: string;
  ownerOnly: boolean;
  soon: boolean;
}
export interface NavGroup { key: NavGroupKey; labelKey: MessageKey; items: NavItem[]; }

export const clubNav: NavGroup[] = [
  {
    key: 'branch',
    labelKey: 'nav.group.branch',
    items: [
      { key: 'overview', labelKey: 'nav.overview', path: '/club', ownerOnly: false, soon: false },
      { key: 'venue', labelKey: 'nav.venue', path: '/club/venue', ownerOnly: false, soon: false },
      { key: 'clients', labelKey: 'nav.clients', path: '/club/clients', ownerOnly: false, soon: false },
      { key: 'monetization', labelKey: 'nav.monetization', path: '/club/monetization', ownerOnly: true, soon: false },
      { key: 'reports', labelKey: 'nav.reports', path: '/club/reports', ownerOnly: false, soon: true },
      { key: 'settings', labelKey: 'nav.settings', path: '/club/settings', ownerOnly: true, soon: false }
    ]
  },
  {
    key: 'account',
    labelKey: 'nav.group.account',
    items: [
      { key: 'branches', labelKey: 'nav.branches', path: '/club/branches', ownerOnly: false, soon: false },
      { key: 'install', labelKey: 'nav.install', path: '/club/install', ownerOnly: true, soon: false },
      { key: 'billing', labelKey: 'nav.billing', path: '/club/billing', ownerOnly: true, soon: true },
      { key: 'profile', labelKey: 'nav.profile', path: '/club/profile', ownerOnly: true, soon: true }
    ]
  }
];

const OWNER_PERMISSION = 'identity.branch_staff.manage';

export function roleFromPermissions(permissions: readonly string[]): ClubRole {
  return permissions.includes(OWNER_PERMISSION) ? 'owner' : 'manager';
}

export function visibleNav(role: ClubRole): NavGroup[] {
  return clubNav
    .map(group => ({ ...group, items: group.items.filter(i => role === 'owner' || !i.ownerOnly) }))
    .filter(group => group.items.length > 0);
}
