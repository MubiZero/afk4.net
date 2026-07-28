import { useEffect } from 'react';
import type { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from './authClient';
import { canOpenWorkspace } from './operatorPermissions';
import type { AuthStatus, OperatorConfig } from './operatorTypes';
import { fetchPlayersData, playersSnapshotCache } from './players/playersSnapshot';

type Translate = ReturnType<typeof useI18n>['t'];

// Прогревает кэш клиентов при входе, чтобы ПЕРВЫЙ заход в «Клиенты» был мгновенным — так же, как зал
// (`useFloorMap`) грузится заранее и Карта не мигает. Best-effort: нет права `players.view` → не
// грузим; ошибку глотаем (реальную покажет сам воркспейс при открытии); кэш филиала уже прогрет →
// пропускаем. На результат экрана не влияет напрямую — только наполняет `playersSnapshotCache`.
export function usePlayersPreload(
  authStatus: AuthStatus,
  authSession: OperatorAuthSession | null,
  config: OperatorConfig,
  t: Translate,
  // Активный филиал (реактивный выбор из свитчера — Task 11); без него прогрев продолжал бы
  // греть кэш старого филиала после переключения, замороженный на входе в сессию.
  activeBranchId: string | null
): void {
  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null || !canOpenWorkspace(authSession, 'players')) {
      return undefined;
    }

    const branchId = activeBranchId;
    if (!branchId || playersSnapshotCache.has(branchId)) {
      return undefined;
    }

    let disposed = false;
    void fetchPlayersData({ config, session: authSession, branchId }, t, '')
      .then(({ clients }) => {
        if (!disposed) {
          playersSnapshotCache.set(branchId, { clients, selectedId: clients[0]?.playerAccountId ?? null });
        }
      })
      .catch(() => { /* прогрев best-effort — реальную ошибку покажет воркспейс при открытии раздела */ });

    return () => {
      disposed = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authStatus, authSession, activeBranchId, config.platformBaseUrl]);
}
