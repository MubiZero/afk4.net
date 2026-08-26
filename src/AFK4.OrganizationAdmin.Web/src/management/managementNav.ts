import type { LucideIcon } from 'lucide-react';
import { Building2, CalendarCheck, MonitorCog, BadgeDollarSign, UsersRound, Boxes, CreditCard, Newspaper, Trophy } from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission } from '../operatorPermissions';
import { permissionNames } from '../permissionNames';

export type ManagementDestinationId =
  | 'club' | 'booking' | 'halls' | 'tariffs' | 'staff' | 'goods'
  | 'payments' | 'news' | 'events';

export interface ManagementDestination {
  id: ManagementDestinationId;
  labelKey: MessageKey;
  subtitleKey: MessageKey;
  Icon: LucideIcon;
  permissions: readonly string[]; // visible if the session has ANY of these
}

export const managementDestinations: readonly ManagementDestination[] = [
  {
    id: 'club',
    labelKey: 'op.management.dest.club',
    subtitleKey: 'op.management.dest.club.subtitle',
    Icon: Building2,
    permissions: [permissionNames.manageBranchSettings]
  },
  {
    id: 'booking',
    labelKey: 'op.management.dest.booking',
    subtitleKey: 'op.management.dest.booking.subtitle',
    Icon: CalendarCheck,
    // Настройки приёма гостей ходят под тем же правом, что и остальные настройки филиала
    // (ManageBranchSettings на сервере) — своего права у них нет.
    permissions: [permissionNames.manageBranchSettings]
  },
  {
    id: 'halls',
    labelKey: 'op.management.dest.halls',
    subtitleKey: 'op.management.dest.halls.subtitle',
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
    id: 'tariffs',
    labelKey: 'op.management.dest.tariffs',
    subtitleKey: 'op.management.dest.tariffs.subtitle',
    Icon: BadgeDollarSign,
    permissions: [permissionNames.manageTariffs, permissionNames.managePackages]
  },
  {
    id: 'staff',
    labelKey: 'op.management.dest.staff',
    subtitleKey: 'op.management.dest.staff.subtitle',
    Icon: UsersRound,
    permissions: [permissionNames.manageBranchStaff, permissionNames.manageRoles]
  },
  {
    id: 'goods',
    labelKey: 'op.management.dest.goods',
    subtitleKey: 'op.management.dest.goods.subtitle',
    Icon: Boxes,
    permissions: [permissionNames.managePosCatalog, permissionNames.manageInventoryStock]
  },
  {
    id: 'payments',
    labelKey: 'op.management.dest.payments',
    subtitleKey: 'op.management.dest.payments.subtitle',
    Icon: CreditCard,
    // Union of both tabs' permissions — visible if the session can manage payment gateways OR
    // loyalty. Which tabs actually render is gated per-tab inside PaymentsLoyaltyDestination, so a
    // role holding only one permission sees only its tab (no empty second tab).
    permissions: [permissionNames.managePaymentGateways, permissionNames.manageLoyaltySettings]
  },
  {
    id: 'news',
    labelKey: 'op.management.dest.news',
    subtitleKey: 'op.management.dest.news.subtitle',
    Icon: Newspaper,
    permissions: [permissionNames.manageNews]
  },
  {
    id: 'events',
    labelKey: 'op.management.dest.events',
    subtitleKey: 'op.management.dest.events.subtitle',
    Icon: Trophy,
    permissions: [permissionNames.manageTournaments]
  }
];

export function allowedManagementDestinations(session: OperatorAuthSession | null): ManagementDestination[] {
  return managementDestinations.filter((destination) => hasAnyPermission(session, destination.permissions));
}
