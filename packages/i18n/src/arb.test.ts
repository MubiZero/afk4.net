import { it, expect } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { messages } from './messages';

// Flutter не читает locales/*.json — ему нужны ARB. Чтобы источник строк остался один,
// ARB генерируются тем же скриптом (`bun run gen`), а эти тесты стерегут расхождение:
// забыл перегенерировать после правки каталога — красный тест, а не тихо устаревший перевод.
const arbDir = join(import.meta.dir, '..', '..', '..', 'src', 'afk4_customer_app', 'lib', 'l10n');
const readArb = (loc: string) => JSON.parse(readFileSync(join(arbDir, `app_${loc}.arb`), 'utf8')) as Record<string, string>;

const LOCALES = ['ru', 'en', 'tg'] as const;
// Те же префиксы, что в ARB_TARGETS генератора: строки приложения плюс общие подписи для
// экранных читалок.
const PREFIXES = ['customer.', 'a11y.'];
const appKeys = Object.keys(messages.ru).filter((key) => PREFIXES.some((p) => key.startsWith(p))).sort();

// Точки в ключе — не идентификатор Dart, поэтому ARB получает camelCase-имена.
// Дублируем правило здесь намеренно: тест обязан ловить и ошибку в самом преобразовании.
const toArbName = (key: string) =>
  key.split('.').map((part, index) => (index === 0 ? part : part[0].toUpperCase() + part.slice(1))).join('');

it('каждая локаль ARB несёт ровно те же строки приложения, что и каталог', () => {
  for (const locale of LOCALES) {
    const arb = readArb(locale);
    const names = Object.keys(arb).filter((name) => !name.startsWith('@')).sort();
    expect(names).toEqual(appKeys.map(toArbName).sort());

    for (const key of appKeys) {
      expect(arb[toArbName(key)]).toBe((messages[locale] as Record<string, string>)[key]);
    }
  }
});

it('ARB объявляет свою локаль — без @@locale Flutter не поймёт, какой это язык', () => {
  for (const locale of LOCALES) {
    expect(readArb(locale)['@@locale']).toBe(locale);
  }
});

// Без объявленных подстановок `gen-l10n` не соберёт метод с аргументами — строка с
// `{amount}` уедет в интерфейс как есть, с фигурными скобками.
it('строка с подстановками объявляет их в @ключе', () => {
  const arb = readArb('ru');
  const withPlaceholders = Object.entries(arb)
    .filter(([name, value]) => !name.startsWith('@') && typeof value === 'string' && value.includes('{'));

  expect(withPlaceholders.length).toBeGreaterThan(0);
  for (const [name] of withPlaceholders) {
    const meta = arb[`@${name}`] as unknown as { placeholders?: Record<string, { type: string }> };
    expect(meta?.placeholders, `нет @${name}.placeholders`).toBeDefined();
  }
});

it('обычная подстановка объявлена строкой', () => {
  const arb = readArb('ru') as unknown as Record<string, { placeholders?: Record<string, { type: string }> }>;
  expect(arb['@customerHistoryDurationHoursMinutes']?.placeholders).toEqual({
    hours: { type: 'String' },
    minutes: { type: 'String' },
  });
});

it('преобразование имени не схлопывает разные ключи в одно имя', () => {
  const names = appKeys.map(toArbName);
  expect(new Set(names).size).toBe(names.length);
});
