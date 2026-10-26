import { useMemo } from 'react';
import { useI18n } from '@afk4/i18n';
import { refreshSeatRemaining } from './floorMapState';
import type { SeatSummary } from './operatorData';
import { useSecondClock } from './useSecondClock';

/**
 * Место с остатком времени «на сейчас».
 *
 * Раньше остаток всем местам пересчитывался раз в секунду на уровне всего приложения, и каждую
 * секунду перерисовывалась вся Панель — на слабом кассовом ПК весь вечер. Теперь секундные часы
 * заводит только то место, у которого идёт отсчёт или висит вызов администратора.
 */
export function useLiveSeat(seat: SeatSummary): SeatSummary {
  const { t } = useI18n();
  const counting = seat.hasActiveSession === true && seat.sessionState !== 'Paused' && seat.remainingDeadlineMs != null;
  const nowMs = useSecondClock(counting || Boolean(seat.assistanceRequestedAtUtc));
  return useMemo(() => refreshSeatRemaining(seat, t, nowMs), [seat, t, nowMs]);
}
