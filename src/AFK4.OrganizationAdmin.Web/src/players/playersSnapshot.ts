import type { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, projectPlayerClient, type PlayerClientItem } from '../operatorHelpers';
import type { OperatorBackendContext } from '../operatorTypes';

type Translate = ReturnType<typeof useI18n>['t'];

export interface PlayersSnapshot {
  clients: PlayerClientItem[];
  selectedId: string | null;
  // Есть ещё страницы: без этого вернувшийся в раздел не увидел бы кнопку «Показать ещё».
  hasMore: boolean;
}

// Клубу с тысячами клиентов нельзя отдать всех разом: справочник открывается страницей в 50 по имени.
export const PLAYERS_PAGE_SIZE = 50;

// Последний удачный список клиентов, переживающий навигацию (раздел размонтируется при уходе, в
// отличие от Карты/Броней, чьи данные живут в App-стейте). Пишется и App-преднагрузкой (прогрев при
// входе — чтобы первый заход не мигал), и самим воркспейсом (ревалидация). Ключ = branchId.
export const playersSnapshotCache = new Map<string, PlayersSnapshot>();

// Единый источник «как грузить клиентов» для воркспейса и App-преднагрузки: fetch + проекция списка.
// selectedId резолвит вызывающий (воркспейс сохраняет текущий выбор, преднагрузка берёт первого).
// Просим на одну запись больше страницы: лишняя говорит, что дальше есть ещё, и в список не идёт.
export async function fetchPlayersData(
  backend: OperatorBackendContext,
  t: Translate,
  search: string,
  page: { segment?: 'debt' | 'inactive'; offset?: number } = {}
): Promise<{ clients: PlayerClientItem[]; hasMore: boolean }> {
  const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
  const players = await apiClients.players.searchPlayers(backend.branchId, search, PLAYERS_PAGE_SIZE + 1, true, page);
  const rows = Array.isArray(players) ? players : [];
  return {
    clients: rows.slice(0, PLAYERS_PAGE_SIZE).map((p) => projectPlayerClient(p, t)),
    hasMore: rows.length > PLAYERS_PAGE_SIZE
  };
}
