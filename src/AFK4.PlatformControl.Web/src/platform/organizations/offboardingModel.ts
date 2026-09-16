import type { MessageKey } from '@afk4/i18n';
import type { OrganizationOffboarding } from '@/api/types';

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
