import { Building2, DownloadCloud, ScrollText, Settings, Wallet } from 'lucide-react';
import type { NavItem } from '@/components/shell/navModel';
import type { PlatformAdminSession } from '@/auth/tokenStore';
import { can, type PlatformCapability } from '@/auth/platformAccess';

type GatedNavItem = NavItem & { capability?: PlatformCapability };

// Рейл панели. «Профиля» здесь больше нет: учётная запись, тема, язык и выход живут в меню
// аккаунта в подвале рейла — как в Organization Admin. Отдельный экран показывал только список
// собственных прав и надпись «редактирование недоступно».
const platformNav: GatedNavItem[] = [
  { key: 'clubs', labelKey: 'nav.platform.clubs', path: '/admin', icon: Building2, capability: 'organizations.read' },
  { key: 'money', labelKey: 'nav.platform.money', path: '/admin/money', icon: Wallet, capability: 'billing.read' },
  { key: 'updates', labelKey: 'nav.platform.updates', path: '/admin/updates', icon: DownloadCloud, capability: 'updates.read' },
  { key: 'journal', labelKey: 'nav.platform.journal', path: '/admin/journal', icon: ScrollText, capability: 'audit.read' },
  { key: 'settings', labelKey: 'nav.platform.settings', path: '/admin/settings', icon: Settings, capability: 'settings.manage' }
];

export function buildPlatformNav(session: PlatformAdminSession): NavItem[] {
  return platformNav
    .filter(item => item.capability === undefined || can(session, item.capability))
    .map(({ capability: _capability, ...item }) => item);
}
