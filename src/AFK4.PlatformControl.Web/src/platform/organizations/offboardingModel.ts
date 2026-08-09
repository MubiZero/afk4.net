import type { MessageKey } from '@afk4/i18n';
import { PlatformApiError } from '@/api/platformTransport';
import { describeApiError } from '@/api/describeApiError';
import type { OrganizationOffboarding } from '@/api/types';

type Translate = (key: MessageKey, values?: Record<string, string | number>) => string;

const ERROR_CODE_KEY: Record<string, MessageKey> = {
  not_deletion_pending: 'platform.offboarding.error.notLeaving',
  purge_not_due: 'platform.offboarding.error.notDue',
  slug_mismatch: 'platform.offboarding.error.slugMismatch',
  organization_purged: 'platform.offboarding.error.alreadyPurged'
};

export function describeOffboardingError(cause: unknown, t: Translate): string {
  if (cause instanceof PlatformApiError && cause.errorCode !== null) {
    const key = ERROR_CODE_KEY[cause.errorCode];
    if (key !== undefined) return t(key);
  }
  return describeApiError(cause, t);
}

/**
 * Почему стирание недоступно — или `null`, когда доступно. Выключенная кнопка без объяснения
 * оставляет человека гадать; здесь причина всегда конкретна.
 */
export function purgeBlockReasonKey(state: OrganizationOffboarding): MessageKey | null {
  if (state.status === 'purged') return 'platform.offboarding.blocked.alreadyPurged';
  if (state.status !== 'deletion_pending') return 'platform.offboarding.blocked.notLeaving';
  if (!state.canPurge) return 'platform.offboarding.blocked.notDue';
  return null;
}
