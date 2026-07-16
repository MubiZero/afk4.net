import type { LucideIcon } from 'lucide-react';
import { Building2, MonitorCog, BadgeDollarSign, UsersRound, Boxes, CreditCard, Gift, Newspaper } from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { permissionNames, hasAnyPermission } from '../operatorPermissions';

export type ManagementDestinationId =
  | 'club' | 'halls' | 'tariffs' | 'staff' | 'goods'
  | 'payment' | 'loyalty' | 'news';

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
    id: 'halls',
    labelKey: 'op.management.dest.halls',
    subtitleKey: 'op.management.dest.halls.subtitle',
    Icon: MonitorCog,
    permissions: [permissionNames.manageLayout, permissionNames.viewDeviceDetail, permissionNames.viewDeviceCommandStatus]
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
    id: 'payment',
    labelKey: 'op.management.dest.payment',
    subtitleKey: 'op.management.dest.payment.subtitle',
    Icon: CreditCard,
    permissions: [permissionNames.managePaymentGateways]
  },
  {
    id: 'loyalty',
    labelKey: 'op.management.dest.loyalty',
    subtitleKey: 'op.management.dest.loyalty.subtitle',
    Icon: Gift,
    permissions: [permissionNames.manageLoyaltySettings]
  },
  {
    id: 'news',
    labelKey: 'op.management.dest.news',
    subtitleKey: 'op.management.dest.news.subtitle',
    Icon: Newspaper,
    permissions: [permissionNames.manageNews]
  }
];

export function allowedManagementDestinations(session: OperatorAuthSession | null): ManagementDestination[] {
  return managementDestinations.filter((destination) => hasAnyPermission(session, destination.permissions));
}
