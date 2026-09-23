import { describe, expect, it } from 'bun:test';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

/**
 * `mock.module` в bun действует на весь процесс: подмена, поставленная одним тест-файлом, остаётся
 * и для всех следующих, а ещё задним числом меняет уже импортированные пространства имён. Какой
 * файл окажется следующим, решает файловая система: на NTFS порядок другой, чем на Linux. Так
 * `stock/WriteOffDialog.test.tsx` оставлял свою подмену `operatorHelpers` включённой, и на Windows
 * настоящая оболочка в `support/supportShell.test.tsx` падала на `client.list` — а CI гоняет веб
 * на Linux и этого не видел.
 *
 * Правило: файл, который подменяет модуль, возвращает настоящий в `afterAll`. `mock.restore()`
 * не в счёт — он снимает шпионов и моки функций, а подмену модуля оставляет как была.
 * Лучше всего модуль не подменять вовсе и передать зависимость компоненту (см. `client` у
 * «Подписки» и «Обновлений»).
 */
const srcRoot = import.meta.dir;

// Эти два файла идут отдельным запуском bun (скрипт test в package.json): им чужие подмены не
// достаются, а свои они ставят на весь запуск намеренно.
const OWN_PROCESS = new Set(['App.test.tsx', 'support/supportShell.test.tsx']);

function testFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return testFiles(path);
    return /\.test\.tsx?$/.test(name) ? [path] : [];
  });
}

function unrestoredMocks(): string[] {
  return testFiles(srcRoot).flatMap((path) => {
    const file = relative(srcRoot, path).split(sep).join('/');
    if (OWN_PROCESS.has(file)) return [];
    // Комментарии, которые рассказывают про чужую подмену, подменой не считаются.
    const source = readFileSync(path, 'utf8')
      .split('\n')
      .filter((line) => !/^\s*(\/\/|\*)/.test(line))
      .join('\n');
    const afterAllAt = source.indexOf('afterAll(');
    const specifiers = new Set([...source.matchAll(/mock\.module\(\s*'([^']+)'/g)].map((match) => match[1]));
    return [...specifiers]
      .filter((specifier) => afterAllAt < 0 || !source.slice(afterAllAt).includes(`mock.module('${specifier}'`))
      .map((specifier) => `${file} ${specifier}`);
  });
}

describe('mock.module does not outlive its file', () => {
  it('every module a test file replaces is put back in afterAll', () => {
    expect(unrestoredMocks()).toEqual([]);
  });
});
