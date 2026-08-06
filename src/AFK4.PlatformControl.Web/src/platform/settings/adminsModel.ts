import { PlatformApiError } from '@/api/platformTransport';
import { describeApiError } from '@/api/describeApiError';
import type { PlatformAdminListItem } from '@/api/types';
import type { MessageKey } from '@/i18n/messages';

type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;

export const ROLE_PLATFORM_ADMIN = 'platform_admin';
export const ROLE_PLATFORM_SUPPORT = 'platform_support';

function activeFullAdminCount(items: PlatformAdminListItem[]): number {
  return items.filter(item => item.role === ROLE_PLATFORM_ADMIN && item.isActive).length;
}

// Removing the last active full admin would lock the platform out of its own admin panel —
// support accounts alone can't fix it. Both disabling and demoting the last full admin are
// blocked by the same rule.
function isLastActiveFullAdmin(item: PlatformAdminListItem, items: PlatformAdminListItem[]): boolean {
  return item.role === ROLE_PLATFORM_ADMIN && item.isActive && activeFullAdminCount(items) <= 1;
}

/** Why the "disable" action is unavailable for this row, or null when it's allowed. */
export function disableBlockReasonKey(
  item: PlatformAdminListItem,
  selfId: string,
  items: PlatformAdminListItem[]
): MessageKey | null {
  if (!item.isActive) return null;
  if (item.platformAdminUserId === selfId) return 'platform.settings.reason.self';
  if (isLastActiveFullAdmin(item, items)) return 'platform.settings.reason.lastFullAdmin';
  return null;
}

export function canDisable(item: PlatformAdminListItem, selfId: string, items: PlatformAdminListItem[]): boolean {
  return item.isActive && disableBlockReasonKey(item, selfId, items) === null;
}

/** Why the "change role" action is unavailable for this row, or null when it's allowed. */
export function changeRoleBlockReasonKey(
  item: PlatformAdminListItem,
  selfId: string,
  items: PlatformAdminListItem[]
): MessageKey | null {
  if (item.platformAdminUserId === selfId) return 'platform.settings.reason.self';
  if (isLastActiveFullAdmin(item, items)) return 'platform.settings.reason.lastFullAdmin';
  return null;
}

export function canChangeRole(item: PlatformAdminListItem, selfId: string, items: PlatformAdminListItem[]): boolean {
  return changeRoleBlockReasonKey(item, selfId, items) === null;
}

export function roleLabelKey(role: string): MessageKey {
  if (role === ROLE_PLATFORM_ADMIN) return 'platform.settings.role.admin';
  if (role === ROLE_PLATFORM_SUPPORT) return 'platform.settings.role.support';
  return 'platform.settings.role.unknown';
}

// The API reports specific 409/400/404 codes for admin management failures (races with another
// admin, the last-full-admin guard, self-demotion, an unknown role, a stale id). describeApiError
// only keys off the HTTP status, which can't tell three different 409s apart — so admin actions
// resolve their own message from the body's error code first, falling back to the generic mapping.
const ERROR_CODE_KEY: Record<string, MessageKey> = {
  last_full_admin: 'platform.settings.error.lastFullAdmin',
  self_demotion: 'platform.settings.error.selfAction',
  conflict: 'platform.settings.error.conflict',
  unknown_role: 'platform.settings.error.unknownRole',
  not_found: 'platform.settings.error.notFound'
};

export function describeAdminActionError(cause: unknown, t: Translate): string {
  if (cause instanceof PlatformApiError && cause.errorCode !== null) {
    const key = ERROR_CODE_KEY[cause.errorCode];
    if (key !== undefined) return t(key);
  }
  return describeApiError(cause, t);
}
