import { useRef } from 'react';

/**
 * Ключ одной попытки сделать необратимое.
 *
 * Пока попытка не удалась, повторы несут тот же ключ, и сервер выполняет операцию один раз —
 * даже если первый запрос всё-таки дошёл, а ответ потерялся по дороге. Это важно ровно там, где
 * второй раз стоит денег: счёт, отметка об оплате, заведённый клуб.
 *
 * Ключ привязан к тому, о чём попытка (`subject` — обычно само тело запроса). Исправил человек
 * сумму и нажал снова — это уже другая попытка, и ключ обязан смениться: сервер отвергает тот же
 * ключ с другим телом, и без этой привязки форма после правки просто перестала бы отправляться.
 */
export interface AttemptKey {
  /// Ключ попытки об этом предмете. Тот же самый, пока предмет не изменился и не вызван `done()`.
  forSubject(subject: unknown): string;
  /// Попытка завершилась — дальше начинается новая.
  done(): void;
}

export function useAttemptKey(): AttemptKey {
  const key = useRef<string | null>(null);
  const subject = useRef<string | null>(null);
  const handle = useRef<AttemptKey>({
    forSubject: (next: unknown) => {
      const serialized = JSON.stringify(next ?? null);
      if (subject.current !== serialized) {
        subject.current = serialized;
        key.current = null;
      }
      key.current ??= crypto.randomUUID();
      return key.current;
    },
    done: () => { key.current = null; subject.current = null; }
  });
  return handle.current;
}
