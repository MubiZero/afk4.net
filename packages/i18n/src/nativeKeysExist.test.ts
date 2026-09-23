import { describe, expect, it } from 'bun:test';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

// Нативные окна (WPF-хосты) читают каталог тем же `locales/*.json`, но через C#:
// `localization.T("operator.host.loading")`. Проверка мёртвых ключей видит эти имена, а обратной
// проверки не было — и 2026-08-10 ключи окна панели удалили как осиротевшие. Полтора месяца окно
// при запуске показывало человеку сырое «operator.host.loading». Заслон: каждый ключ, который C#
// просит, в каталоге есть.
const repoRoot = join(import.meta.dir, '..', '..', '..');
const SKIP_DIRS = new Set(['node_modules', 'bin', 'obj', '.git']);

function csharpFiles(dir: string, found: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (SKIP_DIRS.has(entry)) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) csharpFiles(full, found);
    else if (entry.endsWith('.cs')) found.push(full);
  }
  return found;
}

describe('ключи нативных окон', () => {
  const catalog = JSON.parse(readFileSync(join(repoRoot, 'locales', 'ru.json'), 'utf8')) as Record<string, string>;
  const requested = new Set<string>();
  for (const file of csharpFiles(join(repoRoot, 'src'))) {
    for (const [, key] of readFileSync(file, 'utf8').matchAll(/\.T\("([a-zA-Z][a-zA-Z0-9_.]+)"\)/g)) requested.add(key);
  }

  it('находит, что проверять', () => {
    expect(requested.size).toBeGreaterThan(0);
  });

  it('есть в каталоге', () => {
    expect([...requested].filter((key) => !(key in catalog))).toEqual([]);
  });
});
