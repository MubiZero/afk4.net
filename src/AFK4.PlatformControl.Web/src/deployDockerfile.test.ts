import { describe, expect, it } from 'bun:test';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const repoRoot = join(import.meta.dir, '..', '..', '..');

// Образ ставит зависимости из корневого лок-файла, а тот описывает весь воркспейс целиком. Значит
// манифест каждого участника должен попасть в контекст сборки до `bun install --frozen-lockfile`.
// Пропущенный манифест ломает не тесты, а деплой: сборка падает на сервере, контейнер продолжает
// крутить старый образ, и панель тихо остаётся прошлой версии — именно так и случилось с
// `packages/ui`, из-за чего на staging неделю жила сборка без двухфакторки.
describe('platform-control.Dockerfile', () => {
  const dockerfile = readFileSync(join(repoRoot, 'deploy/coolify/platform-control.Dockerfile'), 'utf8');
  const rootManifest = JSON.parse(readFileSync(join(repoRoot, 'package.json'), 'utf8')) as {
    workspaces: string[];
  };

  const members = rootManifest.workspaces.flatMap(pattern => {
    if (!pattern.endsWith('/*')) {
      return [pattern];
    }
    const directory = pattern.slice(0, -2);
    return readdirSync(join(repoRoot, directory), { withFileTypes: true })
      .filter(entry => entry.isDirectory())
      .map(entry => `${directory}/${entry.name}`);
  });

  it.each(members)('copies the manifest of %s', member => {
    expect(dockerfile).toContain(`COPY ${member}/package.json`);
  });

  it('finds every workspace member (the list itself must not silently go empty)', () => {
    expect(members.length).toBeGreaterThanOrEqual(5);
  });
});
