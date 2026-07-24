import type { LucideIcon } from 'lucide-react';
import { Building2, CreditCard, MonitorDown, ScrollText } from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission } from '../operatorPermissions';
import { permissionNames } from '../permissionNames';

export type NetworkDestinationId = 'branches' | 'billing' | 'install' | 'journal';

export interface NetworkDestination {
  id: NetworkDestinationId;
  labelKey: MessageKey;
  subtitleKey: MessageKey;
  Icon: LucideIcon;
  permissions: readonly string[]; // visible if the session has ANY of these
}

export const networkDestinations: readonly NetworkDestination[] = [
  {
    id: 'branches',
    labelKey: 'op.network.dest.branches',
    subtitleKey: 'op.network.dest.branches.subtitle',
    Icon: Building2,
    permissions: [permissionNames.viewBranches]
  },
  {
    id: 'billing',
    labelKey: 'op.network.dest.billing',
    subtitleKey: 'op.network.dest.billing.subtitle',
    Icon: CreditCard,
    permissions: [permissionNames.viewSubscription]
  },
  {
    id: 'install',
    labelKey: 'op.network.dest.install',
    subtitleKey: 'op.network.dest.install.subtitle',
    Icon: MonitorDown,
    permissions: [permissionNames.installDevice]
  },
  {
    id: 'journal',
    labelKey: 'op.network.dest.journal',
    subtitleKey: 'op.network.dest.journal.subtitle',
    Icon: ScrollText,
    permissions: [permissionNames.viewOrganizationAudit]
  }
];

export function allowedNetworkDestinations(session: OperatorAuthSession | null): NetworkDestination[] {
  return networkDestinations.filter((destination) => hasAnyPermission(session, destination.permissions));
}
