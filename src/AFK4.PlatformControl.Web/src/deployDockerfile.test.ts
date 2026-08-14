import { describe, expect, it } from 'bun:test';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

const repoRoot = join(import.meta.dir, '..', '..', '..');

// Образы ставят зависимости из корневого лок-файла, а тот описывает весь воркспейс целиком. Значит
// манифест каждого участника должен попасть в контекст сборки до `bun install --frozen-lockfile`.
// Пропущенный манифест ломает не тесты, а деплой: сборка падает на сервере, контейнер продолжает
// крутить старый образ, и панель тихо остаётся прошлой версии — именно так и случилось с
// `packages/ui`, из-за чего на staging неделю жила сборка без двухфакторки.
//
// Проверка идёт по всем веб-образам сразу: добавить второй Dockerfile и забыть в нём тот же
// манифест — ошибка ровно того же рода, и ловиться она должна тем же тестом.
const dockerfiles = ['platform-control.Dockerfile', 'organization-admin.Dockerfile'];

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

describe.each(dockerfiles)('%s', name => {
  const dockerfile = readFileSync(join(repoRoot, 'deploy/coolify', name), 'utf8');

  it.each(members)('copies the manifest of %s', member => {
    expect(dockerfile).toContain(`COPY ${member}/package.json`);
  });

  // Статику раздаёт nginx с историческим фолбэком, иначе прямая ссылка на внутренний экран
  // отвечает 404 вместо приложения.
  it('serves its own nginx config', () => {
    expect(dockerfile).toContain(`COPY deploy/coolify/${name.replace('.Dockerfile', '.nginx.conf')}`);
  });
});

describe('workspace discovery', () => {
  it('finds every workspace member (the list itself must not silently go empty)', () => {
    expect(members.length).toBeGreaterThanOrEqual(5);
  });
});
