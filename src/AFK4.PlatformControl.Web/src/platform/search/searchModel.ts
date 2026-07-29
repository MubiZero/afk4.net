import type { PlatformSearchResult } from '@/api/platformClients/search';
import type { MessageKey } from '@/i18n/I18nProvider';

export const SEARCH_KIND_LABEL: Record<PlatformSearchResult['kind'], MessageKey> = {
  organization: 'platform.search.kind.organization',
  club: 'platform.search.kind.club',
  owner: 'platform.search.kind.owner'
};

export function nextSearchIndex(current: number, direction: 1 | -1, count: number): number {
  if (count === 0) return -1;
  if (current < 0) return direction === 1 ? 0 : count - 1;
  return (current + direction + count) % count;
}
