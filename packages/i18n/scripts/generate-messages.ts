/** Generate `src/messages.ts` from the catalog source of truth `locales/*.json`.
 * Run with `bun run gen` from this package (or `bun packages/i18n/scripts/generate-messages.ts`).
 * The JSON files are authoritative; messages.ts is a committed generated mirror. */
import { readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const LOCALES = ['ru', 'en', 'tg'] as const;

const scriptDir = dirname(fileURLToPath(import.meta.url));
const localesDir = join(scriptDir, '..', '..', '..', 'locales');
const outFile = join(scriptDir, '..', 'src', 'messages.ts');

function indent(text: string, spaces: number): string {
  const pad = ' '.repeat(spaces);
  return text
    .split('\n')
    .map((line) => (line.length > 0 ? pad + line : line))
    .join('\n');
}

const blocks = LOCALES.map((loc) => {
  const obj = JSON.parse(readFileSync(join(localesDir, `${loc}.json`), 'utf8')) as Record<string, string>;
  return `  ${loc}: ${indent(JSON.stringify(obj, null, 2), 2).trimStart()},`;
}).join('\n');

const out = `// AUTO-GENERATED from locales/*.json by packages/i18n/scripts/generate-messages.ts.
// Do not edit by hand — edit locales/{ru,en,tg}.json then run \`bun run gen\` in packages/i18n.
export type Locale = ${LOCALES.map((l) => `'${l}'`).join(' | ')};

export const messages = {
${blocks}
} as const;

export type MessageKey = keyof (typeof messages)['ru'];
`;

writeFileSync(outFile, out);
console.log(`generated ${outFile} from ${LOCALES.length} locales`);
