import { describe, expect, it } from 'bun:test';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Ключ, который не читается ниоткуда, — это перевод, оплаченный трижды и не показанный ни разу.
 *
 * Такие ключи накапливаются молча: экран переписали, строку убрали, а три её перевода остались в
 * каталоге навсегда. Найти их глазами нельзя — каталог на три с половиной тысячи строк, — и
 * единственный способ не копить их — проверять.
 */
const repoRoot = join(import.meta.dir, '..', '..', '..');

// Ключи, которые собираются из кусков и в исходниках целиком не встречаются. Каждый префикс
// назван вручную и с объяснением: список, который можно пополнить молча, перестаёт что-либо
// значить.
const COMPOSED_PREFIXES: { prefix: string; builtAt: string }[] = [
  // Имена прав приходят с сервера списком, и в коде их нет.
  { prefix: 'platform.permission.', builtAt: 'PlatformControl.Web/platform/settings/rolesModel.ts' },
  // Тип записи журнала приходит строкой с сервера: `ledger.type.${entry.entryType}`.
  { prefix: 'ledger.type.', builtAt: 'OrganizationAdmin.Web/players/playersModel.ts' },
  { prefix: 'ledger.account.', builtAt: 'OrganizationAdmin.Web/players/LedgerRow.tsx' },
  // Роль сотрудника — тоже строка с сервера: `roles.${roleName}`.
  { prefix: 'roles.', builtAt: 'OrganizationAdmin.Web/staffModel.ts' },
  // Глагол действия над заказом: `op.shopOrders.toast.${verb}`.
  { prefix: 'op.shopOrders.toast.', builtAt: 'OrganizationAdmin.Web/PosOrdersTicker.tsx' }
];

const SOURCE_DIRS = ['src', 'packages', 'brand'];
const SOURCE_EXTENSIONS = ['.ts', '.tsx', '.dart', '.cs', '.html'];
const SKIP_DIRS = new Set(['node_modules', 'bin', 'obj', 'dist', '.git', 'build', 'l10n', '.dart_tool']);

function sourceFiles(dir: string, found: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (SKIP_DIRS.has(entry)) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      sourceFiles(full, found);
      continue;
    }
    // Сгенерированные зеркала каталога содержат КАЖДЫЙ ключ и сделали бы проверку бессмысленной.
    if (/messages\.(ru|en|tg)\.ts$/.test(entry)) continue;
    if (SOURCE_EXTENSIONS.some((extension) => entry.endsWith(extension))) found.push(full);
  }
  return found;
}

describe('каталог переводов', () => {
  const catalog = JSON.parse(readFileSync(join(repoRoot, 'locales', 'ru.json'), 'utf8')) as Record<string, string>;
  const keys = Object.keys(catalog);
  const haystack = SOURCE_DIRS
    .flatMap((dir) => sourceFiles(join(repoRoot, dir)))
    .map((file) => readFileSync(file, 'utf8'))
    .join('\n');

  it('видит и каталог, и исходники', () => {
    expect(keys.length).toBeGreaterThan(1000);
    expect(haystack.length).toBeGreaterThan(1_000_000);
  });

  // Имена собираются один раз в множество: три с половиной тысячи поисков подстроки по
  // многомегабайтному тексту идут десяток секунд, а проверка по множеству — мгновение.
  const mentioned = new Set<string>();
  // Подчёркивания входят в имена: ключи вида `ledger.type.top_up` и `booking.reject.no_seats`
  // без них обрывались на середине и выглядели как мёртвые.
  for (const [name] of haystack.matchAll(/[a-zA-Z][a-zA-Z0-9_]*(?:\.[a-zA-Z0-9_]+)+/g)) mentioned.add(name);
  for (const [name] of haystack.matchAll(/\b[a-zA-Z][a-zA-Z0-9_]{3,}\b/g)) mentioned.add(name);

  it('не содержит ключей, которых никто не читает', () => {
    const dead = keys.filter((key) => {
      if (COMPOSED_PREFIXES.some((composed) => key.startsWith(composed.prefix))) return false;
      // Ключи игрока живут в исходниках приложения в camelCase: `customer.shop.title` →
      // `customerShopTitle`, — потому что Flutter генерирует из ARB именно такие имена.
      const camel = key
        .split('.')
        .map((part, index) => (index === 0 ? part : part.charAt(0).toUpperCase() + part.slice(1)))
        .join('');
      return !mentioned.has(key) && !mentioned.has(camel);
    });

    expect(dead).toEqual([]);
  });
});
