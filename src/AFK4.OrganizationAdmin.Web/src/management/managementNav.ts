import type { LucideIcon } from 'lucide-react';
import { Building2, CalendarCheck, MonitorCog, ShieldCheck, BadgeDollarSign, UsersRound, Boxes, CreditCard, Newspaper, Trophy, Gamepad2, MessageSquareText } from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission } from '../operatorPermissions';
import { permissionNames } from '../permissionNames';

export type ManagementDestinationId =
  | 'club' | 'booking' | 'halls' | 'protection' | 'games' | 'tariffs' | 'staff' | 'goods'
  | 'payments' | 'news' | 'events' | 'reviews';

// Группы меню — решение владельца 29.09: двенадцать разделов подряд не читались, а четыре группы
// отвечают на вопрос «про что это» раньше, чем глаз дойдёт до пункта.
export type ManagementGroupId = 'club' | 'pcs' | 'money' | 'people';

export const managementGroups: readonly { id: ManagementGroupId; labelKey: MessageKey }[] = [
  { id: 'club', labelKey: 'op.management.group.club' },
  { id: 'pcs', labelKey: 'op.management.group.pcs' },
  { id: 'money', labelKey: 'op.management.group.money' },
  { id: 'people', labelKey: 'op.management.group.people' }
];

export interface ManagementDestination {
  id: ManagementDestinationId;
  group: ManagementGroupId;
  labelKey: MessageKey;
  Icon: LucideIcon;
  permissions: readonly string[]; // visible if the session has ANY of these
}

// Порядок массива — порядок меню внутри группы; группы идут в порядке managementGroups.
export const managementDestinations: readonly ManagementDestination[] = [
  {
    id: 'club',
    group: 'club',
    labelKey: 'op.management.dest.club',
    Icon: Building2,
    permissions: [permissionNames.manageBranchSettings]
  },
  {
    id: 'booking',
    group: 'club',
    labelKey: 'op.management.dest.booking',
    Icon: CalendarCheck,
    // Настройки приёма гостей ходят под тем же правом, что и остальные настройки филиала
    // (ManageBranchSettings на сервере) — своего права у них нет.
    permissions: [permissionNames.manageBranchSettings]
  },
  {
    id: 'news',
    group: 'club',
    labelKey: 'op.management.dest.news',
    Icon: Newspaper,
    permissions: [permissionNames.manageNews]
  },
  {
    id: 'events',
    group: 'club',
    labelKey: 'op.management.dest.events',
    Icon: Trophy,
    permissions: [permissionNames.manageTournaments]
  },
  {
    id: 'reviews',
    group: 'club',
    labelKey: 'op.management.dest.reviews',
    Icon: MessageSquareText,
    permissions: [permissionNames.viewReviews]
  },
  {
    id: 'halls',
    group: 'pcs',
    labelKey: 'op.management.dest.halls',
    Icon: MonitorCog,
    // Gated on the ability to actually DO work in the reworked halls screen: manage the
    // floor layout (zones/seats) or manage a device (assign to a seat, rotate/revoke its
    // credential). Enrollment codes and lock/unlock commands were dropped from this screen
    // (provisioning is the Setup Wizard's job; lock/unlock lives on the Map), so those perms
    // no longer unlock anything here and must not grant section visibility on their own —
    // otherwise a role holding only one of them lands on an empty screen (semi-presence).
    permissions: [
      permissionNames.manageLayout,
      permissionNames.assignDeviceSeat,
      permissionNames.rotateDeviceCredential,
      permissionNames.revokeDeviceCredential
    ]
  },
  {
    id: 'protection',
    group: 'pcs',
    labelKey: 'op.management.dest.protection',
    Icon: ShieldCheck,
    // Правила клуба для всех ПК филиала — настройка филиала, как приём броней: её задают владелец
    // и управляющий, а не техник, который чинит ПК.
    permissions: [permissionNames.manageBranchSettings]
  },
  {
    id: 'games',
    group: 'pcs',
    labelKey: 'op.management.dest.games',
    Icon: Gamepad2,
    // Библиотеку собирает тот, кто ставит ПК и игры: владелец, управляющий и техник.
    permissions: [permissionNames.manageGameLibrary]
  },
  {
    id: 'tariffs',
    group: 'money',
    labelKey: 'op.management.dest.tariffs',
    Icon: BadgeDollarSign,
    permissions: [permissionNames.manageTariffs, permissionNames.managePackages]
  },
  {
    id: 'goods',
    group: 'money',
    labelKey: 'op.management.dest.goods',
    Icon: Boxes,
    permissions: [permissionNames.managePosCatalog, permissionNames.manageInventoryStock]
  },
  {
    id: 'payments',
    group: 'money',
    labelKey: 'op.management.dest.payments',
    Icon: CreditCard,
    // Union of the sections' permissions — visible if the session can manage payment gateways,
    // loyalty OR tips. Which sections render is gated inside PaymentsLoyaltyDestination, so a role
    // holding only one permission sees only its part. Чаевые — у управляющего, у которого нет ни
    // шлюзов, ни лояльности: без manageTips он не видел своего выключателя.
    permissions: [permissionNames.managePaymentGateways, permissionNames.manageLoyaltySettings, permissionNames.manageTips]
  },
  {
    id: 'staff',
    group: 'people',
    labelKey: 'op.management.dest.staff',
    Icon: UsersRound,
    permissions: [permissionNames.manageBranchStaff, permissionNames.manageRoles]
  }
];

export function allowedManagementDestinations(session: OperatorAuthSession | null): ManagementDestination[] {
  return managementDestinations.filter((destination) => hasAnyPermission(session, destination.permissions));
}
