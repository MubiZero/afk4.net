import type { MessageKey } from './messages';

/**
 * Код (действия, типа объекта, исхода, источника) → подпись из словаря сообщений, без учёта
 * регистра; `null`, если словаря код не знает. Журналы Панели и Пульта платформы держали свою
 * копию ровно этого поиска — по словарю на действие, тип объекта, исход, источник — раздельно на
 * каждый словарь. Один поиск на все словари: выдумывать подпись для незнакомого кода нельзя,
 * `null` — сигнал показать код как есть, а не подменить его чем-то похожим на ответ.
 */
export function dictionaryLabel(dict: Readonly<Record<string, MessageKey>>, code: string, t: (key: MessageKey) => string): string | null {
  const key = dict[code.toLowerCase()];
  return key === undefined ? null : t(key);
}
