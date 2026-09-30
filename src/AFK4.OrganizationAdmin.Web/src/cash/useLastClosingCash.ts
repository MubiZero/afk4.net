import { useEffect, useState } from 'react';
import { createAuthenticatedOperatorClients, formatMoneyInputMinorUnits } from '../operatorHelpers';
import type { OperatorBackendContext } from '../operatorTypes';
import type { ShiftRevenueListDto } from '../operatorApiClients';

export interface ClosedShiftHistoryReader {
  history(branchId: string, limit?: number): Promise<ShiftRevenueListDto>;
}

/**
 * Сколько насчитали в кассе при закрытии прошлой смены: эта сумма и есть остаток, с которым
 * открывается следующая. Подсказка, не обязательное: нет права на отчёты, нет прошлой смены
 * или сервер не ответил — вернёт null, и поле остаётся как было.
 */
function useLastClosingCash(
  backend: OperatorBackendContext | null,
  enabled: boolean,
  injected?: ClosedShiftHistoryReader
): number | null {
  const [minorUnits, setMinorUnits] = useState<number | null>(null);

  useEffect(() => {
    if (!enabled || backend === null) return undefined;
    let active = true;
    (async () => {
      try {
        // Клиент строим здесь, внутри try: на невалидном конфиге (фейк-backend в тестах)
        // PlatformApiClient бросает при создании.
        const reader = injected ?? createAuthenticatedOperatorClients(backend.config, backend.session).shiftRevenue;
        const { shifts } = await reader.history(backend.branchId, 1);
        const last = shifts[0];
        if (active && last?.state === 'closed' && last.cash.counted) setMinorUnits(last.cash.counted.minorUnits);
      } catch {
        // Подсказка необязательна: кассир введёт сумму сам.
      }
    })();
    return () => { active = false; };
  }, [enabled, backend?.branchId, backend?.session.accessToken, injected]);

  return minorUnits;
}

/**
 * Подставляет в поле «Старт наличных» остаток прошлой смены. Только пока поле не тронуто
 * (стоит начальный «0»): то, что кассир уже ввёл сам, не затирается.
 */
export function usePrefillStartingCash(
  backend: OperatorBackendContext | null,
  enabled: boolean,
  setStartingCash: (update: (current: string) => string) => void,
  injected?: ClosedShiftHistoryReader
): void {
  const lastClosingCash = useLastClosingCash(backend, enabled, injected);
  useEffect(() => {
    if (lastClosingCash !== null) {
      setStartingCash((current) => current === '0' ? formatMoneyInputMinorUnits(lastClosingCash) : current);
    }
  }, [lastClosingCash]);
}
