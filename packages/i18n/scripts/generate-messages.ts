/** Generate `src/messages.ts` from the catalog source of truth `locales/*.json`.
 * Run with `bun run gen` from this package (or `bun packages/i18n/scripts/generate-messages.ts`).
 * The JSON files are authoritative; messages.ts is a committed generated mirror. */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const LOCALES = ['ru', 'en', 'tg'] as const;

const scriptDir = dirname(fileURLToPath(import.meta.url));
const localesDir = join(scriptDir, '..', '..', '..', 'locales');
const sourceDir = join(scriptDir, '..', 'src');
const outFile = join(sourceDir, 'messages.ts');

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
