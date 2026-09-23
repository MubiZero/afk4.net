import type { PlatformAdminSession } from './tokenStore';

export type PlatformCapability =
  | 'organizations.read'
  | 'organizations.create'
  | 'organizations.manage'
  | 'organizations.status.manage'
  | 'organizations.limits.manage'
  | 'organizations.profile.manage'
  | 'organizations.update_channel.manage'
  | 'organizations.owner_transfer.manage'
  | 'organizations.features.manage'
  | 'organizations.support_notes.view'
  | 'organizations.support_notes.manage'
  | 'support.manage'
  | 'support.access'
  | 'billing.read'
  | 'billing.manage'
  | 'billing.invoices.manage'
  | 'billing.subscriptions.manage'
  | 'billing.plans.manage'
  | 'updates.read'
  | 'updates.manage'
  | 'updates.packages.manage'
  | 'updates.rollouts.manage'
  | 'audit.read'
  | 'admins.manage'
  | 'announcements.manage'
  | 'offboarding.manage'
  | 'people.network_ban.manage'
  | 'health.read'
  | 'health.test_email.send';

const CAPABILITY_PERMISSIONS: Record<PlatformCapability, readonly string[]> = {
  'organizations.read': ['platform.organizations.view'],
  // Отдельно от `organizations.manage`: заведение клуба бэкенд спрашивает ровно по
  // `platform.organizations.create`. Сотрудник, которому можно только менять статус или лимиты,
  // не должен видеть кнопку «Новый клуб» — единственным ответом на неё был бы отказ.
  'organizations.create': ['platform.organizations.create'],
  'organizations.manage': [
    'platform.organizations.create',
    'platform.organizations.status.update',
    'platform.organizations.limits.update'
  ],
  // Отдельно от `organizations.manage`: раздел «Задолженность» должен звать кнопку
  // «Приостановить» доступной ровно по тому праву, которое реально проверяет бэкенд
  // (`platform.organizations.status.update`) — не по любому из create/status/limits.
  'organizations.status.manage': ['platform.organizations.status.update'],
  // Вкладка «Лимиты» открывалась по любому из create/status/limits, а сохраняет их сервер ровно по
  // `platform.organizations.limits.update`: сотрудник с правом только на статус видел форму, и
  // ответом на сохранение был отказ.
  'organizations.limits.manage': ['platform.organizations.limits.update'],
  'organizations.profile.manage': ['platform.organizations.profile.update'],
  'organizations.update_channel.manage': ['platform.organizations.update_channel.update'],
  'organizations.owner_transfer.manage': ['platform.organizations.owner.transfer'],
  'organizations.features.manage': ['platform.organizations.features.manage'],
  // Отдельно от `support.manage`: заметка в разделе «Задолженность» проверяется бэкендом
  // ровно по `platform.organizations.support_notes.manage`, а не по любому из
  // support_notes/owner_invites/support.access.
  // Смотреть заметки можно по праву на просмотр или на правку; писать — только по праву на правку.
  'organizations.support_notes.view': [
    'platform.organizations.support_notes.view',
    'platform.organizations.support_notes.manage'
  ],
  'organizations.support_notes.manage': ['platform.organizations.support_notes.manage'],
  'support.manage': [
    'platform.organizations.support_notes.manage',
    'platform.organizations.owner_invites.manage',
    'platform.support.access'
  ],
  // Выданные доступы поддержки сервер показывает и выдаёт только по `platform.support.access`;
  // раздел открывался по праву смотреть заметки, и у такого сотрудника он встречал отказом.
  'support.access': ['platform.support.access'],
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
  // Отдельно от `billing.manage`: тариф заводится и правится ровно по
  // `platform.billing.plans.manage`. Сотрудник с правом только на счета видел «Новый тариф», и
  // единственным ответом на нажатие был отказ.
  'billing.plans.manage': ['platform.billing.plans.manage'],
  'updates.read': ['platform.updates.view'],
  'updates.manage': [
    'platform.updates.packages.manage',
    'platform.updates.rollouts.manage'
  ],
  // Отдельно от `updates.read`: раздел открыт по праву на просмотр, а регистрацию пакета бэкенд
  // спрашивает ровно по `platform.updates.packages.manage`.
  'updates.packages.manage': ['platform.updates.packages.manage'],
  // «Опубликовать» создаёт раскатку, а пауза, возобновление и откат меняют её — сервер спрашивает
  // на это `platform.updates.rollouts.manage`, а не право на пакеты.
  'updates.rollouts.manage': ['platform.updates.rollouts.manage'],
  'audit.read': ['platform.audit.view'],
  'admins.manage': ['platform.admins.manage'],
  'announcements.manage': ['platform.announcements.manage'],
  'offboarding.manage': ['platform.organizations.offboarding.manage'],
  'people.network_ban.manage': ['platform.people.network_ban.manage'],
  'health.read': ['platform.health.view'],
  // Отдельно от `health.read`: экран здоровья открыт по `platform.health.view`, а отправку
  // проверочного письма бэкенд спрашивает ровно по `platform.health.test_email.send`. Пока
  // карточка отправки не имела своего гейта, кнопка была активна у тех, кому нельзя, и
  // единственным ответом на нажатие был отказ.
  'health.test_email.send': ['platform.health.test_email.send']
};

export function can(session: PlatformAdminSession, capability: PlatformCapability): boolean {
  const required = CAPABILITY_PERMISSIONS[capability];
  return required.length > 0 && required.some(permission => session.permissions.includes(permission));
}
