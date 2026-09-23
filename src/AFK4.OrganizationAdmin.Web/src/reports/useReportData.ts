import { useCallback, useEffect, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError, type OperatorErrorProjection } from '../apiErrors';

export interface ReportData<T> {
  state: 'loading' | 'ready' | 'error';
  data: T | null;
  // Идёт запрос поверх уже показанных данных: они остаются на экране, пока не придут новые.
  refreshing: boolean;
  error?: OperatorErrorProjection;
  reload: () => void;
}

/**
 * Загрузка отчёта за выбранный период.
 *
 * Смена периода — тот же отчёт за другие даты, а не новый экран. Раньше каждый запрос ставил
 * экран в ожидание, и выбор даты подменял заглушкой всё сразу: цифры, с которыми человек
 * сравнивал, и само поле, в котором он только что выбрал дату. Теперь повтор поверх показанных
 * данных идёт тихо — данные стоят до прихода новых, а `refreshing` говорит, что они уже не за
 * выбранный период. Заглушка осталась там, где показывать нечего: первая загрузка и повтор после
 * отказа. Приём тот же, что у `useLoadable` в Platform Control.
 *
 * Ответ за период, который успели сменить ещё раз, отбрасывается: иначе медленный первый ответ,
 * пришедший после быстрого второго, оставил бы на экране отчёт не за те даты, что в полях.
 *
 * `load === null` — отчёт не у кого спросить (нет подключения к платформе); `deps` — то, от чего
 * зависит запрос: их смена загружает заново, как и `reload()`.
 */
export function useReportData<T>(load: (() => Promise<T>) | null, deps: readonly unknown[]): ReportData<T> {
  const { t } = useI18n();
  const [result, setResult] = useState<{ state: 'loading' | 'ready' | 'error'; data: T | null; error?: OperatorErrorProjection }>({ state: 'loading', data: null });
  const [refreshing, setRefreshing] = useState(false);
  const [nonce, setNonce] = useState(0);
  const request = useRef(0);
  // Запрос пересобирается на каждой отрисовке вместе с замыканием на период; перезапускать его
  // из-за этого нельзя — перезапуск решают `deps`.
  const loadRef = useRef(load);
  loadRef.current = load;
  const tRef = useRef(t);
  tRef.current = t;

  useEffect(() => {
    const id = ++request.current;
    const current = loadRef.current;
    if (current === null) {
      setRefreshing(false);
      setResult({ state: 'error', data: null, error: projectOperatorError(tRef.current('op.reports.backendRequired'), tRef.current) });
      return;
    }
    setResult((previous) => (previous.state === 'ready' ? previous : { state: 'loading', data: null }));
    setRefreshing(true);
    current()
      .then((data) => {
        if (id !== request.current) return;
        setResult({ state: 'ready', data });
        setRefreshing(false);
      })
      .catch((reason: unknown) => {
        if (id !== request.current) return;
        // Цифры за прошлый период под новыми датами были бы неправдой: отказ их убирает.
        setResult({ state: 'error', data: null, error: projectOperatorError(reason, tRef.current) });
        setRefreshing(false);
      });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nonce, ...deps]);

  const reload = useCallback(() => setNonce((value) => value + 1), []);
  return { ...result, refreshing: refreshing && result.state === 'ready', reload };
}
