import type { MessageKey } from '@afk4/i18n';
import { PlatformApiError } from '@/api/platformTransport';
import { describeApiError } from '@/api/describeApiError';

type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;

// Каждый машинный код отказа получает свою фразу. Общий текст «не удалось сохранить» на все
// случаи — это потеря конкретики, которая уже пришла с сервера.
const ERROR_CODE_KEY: Record<string, MessageKey> = {
  role_name_taken: 'platform.settings.roles.error.roleNameTaken',
  unknown_permission: 'platform.settings.roles.error.unknownPermission',
  permission_not_held_by_actor: 'platform.settings.roles.error.permissionNotHeld',
  self_lockout: 'platform.settings.roles.error.selfLockout',
  built_in_role: 'platform.settings.roles.error.builtIn',
  role_in_use: 'platform.settings.roles.error.roleInUse',
  conflict: 'platform.settings.roles.error.conflict',
  invalid_role: 'platform.settings.roles.error.invalidRole'
};

export function describeRoleActionError(cause: unknown, t: Translate): string {
  if (cause instanceof PlatformApiError && cause.errorCode !== null) {
    const key = ERROR_CODE_KEY[cause.errorCode];
    if (key !== undefined) return t(key);
  }
  return describeApiError(cause, t);
}

/**
 * Раздел права — второй сегмент ключа (`platform.billing.plans.manage` → `billing`).
 * Двух десятков переключателей в один список читать невозможно; группировка по разделу — это то,
 * как о правах думает человек, который их выдаёт.
 */
export function permissionGroup(permission: string): string {
  const parts = permission.split('.');
  return parts.length >= 2 ? parts[1] : permission;
}

/**
 * Ключ человеческого названия права. Машинный ключ (`platform.billing.plans.manage`) — это адрес
 * проверки в коде, а не то, что должен читать человек, раздающий доступы; общий префикс
 * `platform.` в ключе каталога не повторяется.
 */
export function permissionLabelKey(permission: string): MessageKey {
  return `platform.permission.${permission.replace(/^platform\./, '')}` as MessageKey;
}

export function permissionGroupLabelKey(group: string): MessageKey {
  return `platform.permission.group.${group}` as MessageKey;
}

/**
 * Право, появившееся на сервере раньше своего перевода, показывается машинным ключом: увидеть
 * непереведённый ключ неприятно, но это честнее, чем спрятать существующее право из списка или
 * нарисовать пустой переключатель.
 */
export function describePermission(permission: string, t: Translate): string {
  const key = permissionLabelKey(permission);
  const label = t(key);
  return label === key ? permission : label;
}

export function describePermissionGroup(group: string, t: Translate): string {
  const key = permissionGroupLabelKey(group);
  const label = t(key);
  return label === key ? group : label;
}

// Порядок групп и прав внутри них — тот, в котором их отдаёт сервер: он идёт от доступа к клубам
// к деньгам и администраторам. Алфавит по машинному ключу перемешал бы их без всякого смысла.
export function groupPermissions(permissions: readonly string[]): [string, string[]][] {
  const groups = new Map<string, string[]>();
  for (const permission of permissions) {
    const group = permissionGroup(permission);
    groups.set(group, [...(groups.get(group) ?? []), permission]);
  }
  return [...groups.entries()];
}
