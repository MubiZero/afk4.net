import type { NavGroup } from '@/components/shell/navModel';
import type { PlatformAdminSession } from '@/auth/tokenStore';
import { can, type PlatformCapability } from '@/auth/platformAccess';

type GatedNavItem = NavGroup['items'][number] & { capability?: PlatformCapability };
type GatedNavGroup = Omit<NavGroup, 'items'> & { items: GatedNavItem[] };

const platformNav: GatedNavGroup[] = [
  {
    key: 'controlPlane',
    labelKey: 'nav.group.controlPlane',
    items: [
      { key: 'clubs', labelKey: 'nav.platform.clubs', path: '/admin', ownerOnly: false, soon: false, capability: 'organizations.read' },
      { key: 'money', labelKey: 'nav.platform.money', path: '/admin/money', ownerOnly: false, soon: false, capability: 'billing.read' },
      { key: 'updates', labelKey: 'nav.platform.updates', path: '/admin/updates', ownerOnly: false, soon: false, capability: 'updates.read' },
      { key: 'journal', labelKey: 'nav.platform.journal', path: '/admin/journal', ownerOnly: false, soon: false, capability: 'audit.read' },
      { key: 'settings', labelKey: 'nav.platform.settings', path: '/admin/settings', ownerOnly: false, soon: false, capability: 'settings.manage' }
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

export function buildPlatformNav(session: PlatformAdminSession): NavGroup[] {
  return platformNav
    .map(group => ({
      ...group,
      items: group.items
        .filter(item => item.capability === undefined || can(session, item.capability))
        .map(({ capability: _capability, ...item }) => item)
    }))
    .filter(group => group.items.length > 0);
}
