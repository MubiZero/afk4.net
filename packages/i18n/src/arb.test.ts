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
const customerKeys = Object.keys(messages.ru).filter((key) => key.startsWith('customer.')).sort();

// Точки в ключе — не идентификатор Dart, поэтому ARB получает camelCase-имена.
// Дублируем правило здесь намеренно: тест обязан ловить и ошибку в самом преобразовании.
const toArbName = (key: string) =>
  key.split('.').map((part, index) => (index === 0 ? part : part[0].toUpperCase() + part.slice(1))).join('');

it('каждая локаль ARB несёт ровно те же customer-строки, что и каталог', () => {
  for (const locale of LOCALES) {
    const arb = readArb(locale);
    const names = Object.keys(arb).filter((name) => !name.startsWith('@')).sort();
    expect(names).toEqual(customerKeys.map(toArbName).sort());

    for (const key of customerKeys) {
      expect(arb[toArbName(key)]).toBe((messages[locale] as Record<string, string>)[key]);
    }
  }
});

it('ARB объявляет свою локаль — без @@locale Flutter не поймёт, какой это язык', () => {
  for (const locale of LOCALES) {
    expect(readArb(locale)['@@locale']).toBe(locale);
  }
});

it('преобразование имени не схлопывает разные ключи в одно имя', () => {
  const names = customerKeys.map(toArbName);
  expect(new Set(names).size).toBe(names.length);
});
