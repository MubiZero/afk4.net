import type { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, projectPlayerClient, type PlayerClientItem } from '../operatorHelpers';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { PackageOptionDto } from '../operatorApiClients';
import type { OperatorBackendContext } from '../operatorTypes';

type Translate = ReturnType<typeof useI18n>['t'];

export interface PlayersSnapshot {
  clients: PlayerClientItem[];
  options: PackageOptionDto[];
  selectedId: string | null;
}

// Последний удачный список клиентов, переживающий навигацию (раздел размонтируется при уходе, в
// отличие от Карты/Броней, чьи данные живут в App-стейте). Пишется и App-преднагрузкой (прогрев при
// входе — чтобы первый заход не мигал), и самим воркспейсом (ревалидация). Ключ = branchId.
export const playersSnapshotCache = new Map<string, PlayersSnapshot>();

// Единый источник «как грузить клиентов» для воркспейса и App-преднагрузки: fetch + проекция списка
// и опций пакетов. selectedId резолвит вызывающий (воркспейс сохраняет текущий выбор, преднагрузка
// берёт первого).
export async function fetchPlayersData(
  backend: OperatorBackendContext,
  t: Translate,
  search: string
): Promise<{ clients: PlayerClientItem[]; options: PackageOptionDto[] }> {
  const apiClients = createAuthenticatedOperatorClients(backend.config, backend.session);
  const players = await apiClients.players.searchPlayers(backend.branchId, search, 25, true);
  const nextPackageOptions = hasPermission(backend.session, permissionNames.viewPackages) || hasPermission(backend.session, permissionNames.purchasePackage)
    ? await apiClients.settings.getPackageOptions(backend.branchId).catch(() => [])
    : [];
  const clients = Array.isArray(players) ? players.map((p) => projectPlayerClient(p, t)) : [];
  const options = Array.isArray(nextPackageOptions) ? nextPackageOptions : [];
  return { clients, options };
}
