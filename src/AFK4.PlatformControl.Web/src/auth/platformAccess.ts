import type { PlatformAdminSession } from './tokenStore';

export type PlatformCapability =
  | 'organizations.read'
  | 'organizations.manage'
  | 'organizations.profile.manage'
  | 'organizations.update_channel.manage'
  | 'organizations.owner_transfer.manage'
  | 'support.manage'
  | 'billing.read'
  | 'billing.manage'
  | 'updates.read'
  | 'updates.manage'
  | 'audit.read'
  | 'admins.manage';

const CAPABILITY_PERMISSIONS: Record<PlatformCapability, readonly string[]> = {
  'organizations.read': ['platform.organizations.view'],
  'organizations.manage': [
    'platform.organizations.create',
    'platform.organizations.status.update',
    'platform.organizations.limits.update'
  ],
  'organizations.profile.manage': ['platform.organizations.profile.update'],
  'organizations.update_channel.manage': ['platform.organizations.update_channel.update'],
  'organizations.owner_transfer.manage': ['platform.organizations.owner.transfer'],
  'support.manage': [
    'platform.organizations.support_notes.manage',
    'platform.organizations.owner_invites.manage',
    'platform.support.access'
  ],
  'billing.read': ['platform.billing.view'],
  'billing.manage': [
    'platform.billing.plans.manage',
    'platform.billing.subscriptions.manage',
    'platform.billing.invoices.manage'
  ],
  'updates.read': ['platform.updates.view'],
  'updates.manage': [
    'platform.updates.packages.manage',
    'platform.updates.rollouts.manage'
  ],
  'audit.read': ['platform.audit.view'],
  'admins.manage': ['platform.admins.manage']
};

export function can(session: PlatformAdminSession, capability: PlatformCapability): boolean {
  const required = CAPABILITY_PERMISSIONS[capability];
  return required.length > 0 && required.some(permission => session.permissions.includes(permission));
}
