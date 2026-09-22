import { useCallback, useEffect, useRef, useState } from 'react';

// Одна секция экрана, которая грузится своим запросом. Экраны «Сети» собраны из независимых
// панелей (подписка и счета, состояние обновления и окно обслуживания), и одним ожиданием на
// все их грузить нельзя: отказ одной панели стирал бы соседнюю, которая уже пришла.
//
// Отказ хранится как есть: словами его называет экран (projectOperatorError), хуку переводчик
// не нужен.
export type Section<T> =
  | { status: 'loading' }
  | { status: 'error'; error: unknown; retry: () => void }
  // `apply` кладёт в секцию свежую запись, которую вернуло сохранение, — без повторной загрузки.
  | { status: 'ready'; data: T; retry: () => void; apply: (next: T) => void };

type Phase<T> = { status: 'loading' } | { status: 'error'; error: unknown } | { status: 'ready'; data: T };

/**
 * `key` — то, от чего зависит сам запрос (организация, филиал): его смена загружает заново, как
 * и `retry()`. Пустой ключ значит «спрашивать пока не о чем» — секция остаётся в ожидании.
 */
export function useSection<T>(load: () => Promise<T>, key: string): Section<T> {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<Phase<T>>({ status: 'loading' });
  // Запрос пересобирается на каждой отрисовке вместе с замыканием на клиента, но перезапускать
  // его из-за этого нельзя — иначе секция грузилась бы бесконечно.
  const loadRef = useRef(load);
  loadRef.current = load;
  const retry = useCallback(() => setTick((value) => value + 1), []);
  const apply = useCallback((next: T) => setPhase({ status: 'ready', data: next }), []);

  useEffect(() => {
    if (key === '') return undefined;
    let cancelled = false;
    setPhase({ status: 'loading' });
    loadRef.current()
      .then((data) => { if (!cancelled) setPhase({ status: 'ready', data }); })
      .catch((error: unknown) => { if (!cancelled) setPhase({ status: 'error', error }); });
    return () => {
      cancelled = true;
    };
  }, [key, tick]);

  if (phase.status === 'error') return { status: 'error', error: phase.error, retry };
  if (phase.status === 'ready') return { status: 'ready', data: phase.data, retry, apply };
  return { status: 'loading' };
}
