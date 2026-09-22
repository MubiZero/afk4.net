import { useCallback, useEffect, useRef, useState } from 'react';
import { describeApiError, retryCanHelp } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';

export type Loadable<T> =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; canRetry: boolean; retry: () => void }
  | { status: 'ready'; data: T; refreshing: boolean; apply: (next: T) => void; retry: () => void };

/**
 * Загрузка данных раздела: ожидание, причина отказа, повтор.
 *
 * Причина — главное, ради чего это одно место вместо десяти похожих. Раньше каждый раздел ловил
 * ошибку сам и клал в состояние техническую английскую строку транспорта, а на экран выводил
 * общее «Не удалось загрузить данные»: сотрудник без права на биллинг, отключённый интернет и
 * упавший сервер выглядели одинаково, хотя первое чинится начальником, второе — проводом, а
 * третье не чинится вовсе и надо просто подождать. Сервер присылает код, `describeApiError` знает
 * все коды — здесь они наконец доезжают до глаз.
 *
 * `deps` — то, от чего зависит сам запрос (идентификатор организации, выбранный период): их смена
 * загружает заново, как и `retry()`.
 */
export interface LoadableOptions {
  /**
   * Как часто перечитывать данные самому, в миллисекундах. Для дежурных экранов вроде обзора
   * сети: их держат открытыми, и снимок часовой давности там опаснее пустого экрана — по нему
   * принимают решения, считая, что видят «сейчас».
   *
   * Фоновое обновление не показывает ожидания и не стирает то, что уже на экране: подмена
   * скелетоном раз в минуту сделала бы экран непригодным для чтения.
   */
  refreshMs?: number;
}

export function useLoadable<T>(
  load: () => Promise<T>,
  deps: readonly unknown[] = [],
  options: LoadableOptions = {}
): Loadable<T> {
  const { t } = useI18n();
  const [tick, setTick] = useState(0);
  const quiet = useRef(false);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: T; message?: string; canRetry?: boolean }>({ status: 'loading' });
  const [refreshing, setRefreshing] = useState(false);
  const statusRef = useRef(state.status);
  statusRef.current = state.status;
  // Повтор поверх уже показанных данных идёт тихо. Раздел зовёт retry() и после собственного
  // успешного действия («Отметить оплаченным», «Снять отключение»), и подмена содержимого
  // скелетоном на полсекунды читается как сбой: человек нажал — и всё, что он читал, исчезло.
  // Скелетон остаётся там, где показывать нечего: первая загрузка и повтор после ошибки.
  const retry = useCallback(() => {
    quiet.current = statusRef.current === 'ready';
    setTick(value => value + 1);
  }, []);
  const apply = useCallback((next: T) => setState({ status: 'ready', data: next }), []);
  // Запрос пересобирается на каждой отрисовке вместе с замыканием на клиента и параметры, но
  // перезапускать его из-за этого нельзя — иначе раздел грузился бы бесконечно.
  const loadRef = useRef(load);
  loadRef.current = load;

  const { refreshMs } = options;
  useEffect(() => {
    if (refreshMs === undefined) return;
    const timer = setInterval(() => {
      // Скрытая вкладка ничего не показывает, а запросы шлёт: браузер оставляет её открытой
      // сутками, и все эти сутки панель опрашивала бы сервер впустую.
      if (typeof document !== 'undefined' && document.hidden) return;
      quiet.current = true;
      setTick(value => value + 1);
    }, refreshMs);
    return () => clearInterval(timer);
  }, [refreshMs]);

  useEffect(() => {
    let cancelled = false;
    if (quiet.current) {
      quiet.current = false;
      setRefreshing(true);
    } else {
      setState({ status: 'loading' });
    }
    loadRef.current()
      .then(data => { if (!cancelled) { setState({ status: 'ready', data }); setRefreshing(false); } })
      .catch((cause: unknown) => {
        if (!cancelled) {
          setState({ status: 'error', message: describeApiError(cause, t), canRetry: retryCanHelp(cause) });
          setRefreshing(false);
        }
      });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tick, ...deps]);

  if (state.status === 'ready') return { status: 'ready', data: state.data as T, refreshing, apply, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? '', canRetry: state.canRetry !== false, retry };
  return { status: 'loading', retry };
}
