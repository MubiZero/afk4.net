import type { PlatformAdminSession } from './tokenStore';

export type PlatformCapability =
  | 'organizations.read'
  | 'organizations.manage'
  | 'organizations.status.manage'
  | 'organizations.profile.manage'
  | 'organizations.update_channel.manage'
  | 'organizations.owner_transfer.manage'
  | 'organizations.support_notes.manage'
  | 'support.manage'
  | 'billing.read'
  | 'billing.manage'
  | 'billing.invoices.manage'
  | 'billing.subscriptions.manage'
  | 'updates.read'
  | 'updates.manage'
  | 'audit.read'
  | 'admins.manage'
  | 'health.read';

const CAPABILITY_PERMISSIONS: Record<PlatformCapability, readonly string[]> = {
  'organizations.read': ['platform.organizations.view'],
  'organizations.manage': [
    'platform.organizations.create',
    'platform.organizations.status.update',
    'platform.organizations.limits.update'
  ],
  // Отдельно от `organizations.manage`: раздел «Задолженность» должен звать кнопку
  // «Приостановить» доступной ровно по тому праву, которое реально проверяет бэкенд
  // (`platform.organizations.status.update`) — не по любому из create/status/limits.
  'organizations.status.manage': ['platform.organizations.status.update'],
  'organizations.profile.manage': ['platform.organizations.profile.update'],
  'organizations.update_channel.manage': ['platform.organizations.update_channel.update'],
  'organizations.owner_transfer.manage': ['platform.organizations.owner.transfer'],
  // Отдельно от `support.manage`: заметка в разделе «Задолженность» проверяется бэкендом
  // ровно по `platform.organizations.support_notes.manage`, а не по любому из
  // support_notes/owner_invites/support.access.
  'organizations.support_notes.manage': ['platform.organizations.support_notes.manage'],
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
  // Отдельно от `billing.manage`: «Отметить оплаченным» проверяется бэкендом ровно по
  // `platform.billing.invoices.manage`, «Отсрочка» — ровно по
  // `platform.billing.subscriptions.manage`. Сотрудник только с правом на тарифы
  // (`billing.manage` даёт true и на такого) не должен видеть эти кнопки активными.
  'billing.invoices.manage': ['platform.billing.invoices.manage'],
  'billing.subscriptions.manage': ['platform.billing.subscriptions.manage'],
  'updates.read': ['platform.updates.view'],
  'updates.manage': [
    'platform.updates.packages.manage',
    'platform.updates.rollouts.manage'
  ],
  'audit.read': ['platform.audit.view'],
  'admins.manage': ['platform.admins.manage'],
  'health.read': ['platform.health.view']
};

export function can(session: PlatformAdminSession, capability: PlatformCapability): boolean {
  const required = CAPABILITY_PERMISSIONS[capability];
  return required.length > 0 && required.some(permission => session.permissions.includes(permission));
}
