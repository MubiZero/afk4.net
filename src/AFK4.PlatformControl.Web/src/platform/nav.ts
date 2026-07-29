import type { NavGroup } from '@/components/shell/navModel';

export const platformNav: NavGroup[] = [
  {
    key: 'controlPlane',
    labelKey: 'nav.group.controlPlane',
    items: [
      { key: 'overview', labelKey: 'nav.platform.overview', path: '/admin', ownerOnly: false, soon: false },
      { key: 'organizations', labelKey: 'nav.platform.organizations', path: '/admin/organizations', ownerOnly: false, soon: false },
      { key: 'billing', labelKey: 'nav.platform.billing', path: '/admin/billing', ownerOnly: false, soon: false },
      { key: 'updates', labelKey: 'nav.platform.updates', path: '/admin/updates', ownerOnly: false, soon: false }
    ]
  },
  {
    key: 'platformAccount',
    labelKey: 'nav.group.platformAccount',
    items: [
      { key: 'profile', labelKey: 'nav.platform.profile', path: '/admin/profile', ownerOnly: false, soon: false }
    ]
  }
];
