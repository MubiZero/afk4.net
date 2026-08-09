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
 * Раздел права — первые два сегмента ключа (`platform.billing.plans.manage` → `platform.billing`).
 * Двух десятков переключателей в один список читать невозможно; группировка по разделу — это то,
 * как о правах думает человек, который их выдаёт.
 */
export function permissionGroup(permission: string): string {
  const parts = permission.split('.');
  return parts.length >= 2 ? `${parts[0]}.${parts[1]}` : permission;
}

export function groupPermissions(permissions: readonly string[]): [string, string[]][] {
  const groups = new Map<string, string[]>();
  for (const permission of permissions) {
    const group = permissionGroup(permission);
    groups.set(group, [...(groups.get(group) ?? []), permission]);
  }
  return [...groups.entries()].sort(([left], [right]) => left.localeCompare(right));
}
