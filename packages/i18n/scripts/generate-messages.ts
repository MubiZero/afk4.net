/** Generate `src/messages.ts` from the catalog source of truth `locales/*.json`.
 * Run with `bun run gen` from this package (or `bun packages/i18n/scripts/generate-messages.ts`).
 * The JSON files are authoritative; messages.ts is a committed generated mirror.
 *
 * Заодно генерирует ARB для Flutter-приложений (см. ARB_TARGETS ниже): Dart не читает наш JSON,
 * а держать второй каталог строк — верный способ развести переводы. Источник остаётся один. */
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const LOCALES = ['ru', 'en', 'tg'] as const;

const scriptDir = dirname(fileURLToPath(import.meta.url));
const localesDir = join(scriptDir, '..', '..', '..', 'locales');
const sourceDir = join(scriptDir, '..', 'src');
const outFile = join(sourceDir, 'messages.ts');
const repoRoot = join(scriptDir, '..', '..', '..');

/** Flutter-приложение получает свою часть каталога по префиксу ключа. По мере переезда
 *  остальных приложений сюда добавляются строки — например `op.` для админского. */
const ARB_TARGETS = [
  { prefix: 'customer.', dir: join(repoRoot, 'src', 'afk4_customer_app', 'lib', 'l10n') }
] as const;

/** `customer.common.back` → `customerCommonBack`: точка не годится в идентификатор Dart. */
function toArbName(key: string): string {
  return key
    .split('.')
    .map((part, index) => (index === 0 ? part : part[0].toUpperCase() + part.slice(1)))
    .join('');
}

for (const loc of LOCALES) {
  const obj = JSON.parse(readFileSync(join(localesDir, `${loc}.json`), 'utf8')) as Record<string, string>;
  writeFileSync(
    join(sourceDir, `messages.${loc}.ts`),
    `// AUTO-GENERATED from locales/${loc}.json. Do not edit by hand.\nexport const ${loc} = ${JSON.stringify(obj, null, 2)} as const;\n`
  );
}

const out = `// AUTO-GENERATED from locales/*.json by packages/i18n/scripts/generate-messages.ts.
// Do not edit by hand — edit locales/{ru,en,tg}.json then run \`bun run gen\` in packages/i18n.
${LOCALES.map((loc) => `import { ${loc} } from './messages.${loc}';`).join('\n')}

export type Locale = ${LOCALES.map((l) => `'${l}'`).join(' | ')};

export const messages = {
${LOCALES.map((loc) => `  ${loc},`).join('\n')}
} as const;

export type MessageKey = keyof (typeof messages)['ru'];
`;

writeFileSync(outFile, out);
console.log(`generated ${outFile} from ${LOCALES.length} locales`);

for (const target of ARB_TARGETS) {
  mkdirSync(target.dir, { recursive: true });
  for (const loc of LOCALES) {
    const catalog = JSON.parse(readFileSync(join(localesDir, `${loc}.json`), 'utf8')) as Record<string, string>;
    const arb: Record<string, string> = { '@@locale': loc };
    for (const key of Object.keys(catalog).filter((k) => k.startsWith(target.prefix)).sort()) {
      arb[toArbName(key)] = catalog[key];
    }
    writeFileSync(join(target.dir, `app_${loc}.arb`), `${JSON.stringify(arb, null, 2)}\n`);
  }
  console.log(`generated ARB for "${target.prefix}" in ${target.dir}`);
}
