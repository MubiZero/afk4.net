import { it, expect } from 'bun:test';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { messages } from '@afk4/i18n';

// Guard against raw i18n keys leaking to the UI: every literal t('key') used in
// operator source must exist in the message catalog. The parity test in
// packages/i18n only checks ru/en/tg agree WITH EACH OTHER — a key referenced in
// code but never added to the locales passes parity yet renders as the bare key
// in production. This test closes that hole.
//
// Limitation: only static literals are checked. Dynamically built keys
// (t(`op.x.${verb}`)) can't be verified here — keep their value sets small and
// covered by component tests.
const SRC_ROOT = import.meta.dir;
const KEY_CALL = /\bt\(\s*["']([\w.-]+)["']/g;

function collectSourceFiles(dir: string): string[] {
  const found: string[] = [];
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      found.push(...collectSourceFiles(full));
    } else if (/\.(ts|tsx)$/.test(entry) && !/\.test\.(ts|tsx)$/.test(entry)) {
      found.push(full);
    }
  }
  return found;
}

it('every literal t() key used in operator source exists in the catalog', () => {
  const ru = messages.ru as Record<string, string>;
  const missing = new Map<string, string>(); // key -> first file that uses it

  for (const file of collectSourceFiles(SRC_ROOT)) {
    const text = readFileSync(file, 'utf8');
    let match: RegExpExecArray | null;
    while ((match = KEY_CALL.exec(text)) !== null) {
      const key = match[1];
      if (!(key in ru) && !missing.has(key)) {
        missing.set(key, file.replace(SRC_ROOT, 'src'));
      }
    }
  }

  expect([...missing].map(([key, file]) => `${key} (${file})`)).toEqual([]);
});
