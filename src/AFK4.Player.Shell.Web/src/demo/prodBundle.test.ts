import { describe, expect, it } from 'bun:test';
import { mkdtempSync, readdirSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

/**
 * Учебный хост и полоса «Демо» не должны попасть в сборку для клубов: иначе ПК клуба показывал бы
 * выдуманный баланс и принимал ПИН-код 123456. Собираем оболочку по-настоящему, обе сборки, и ищем
 * в JS строки, которые есть только в учебном хосте. Вторая сборка — контроль: без неё проверка
 * «в боевой сборке пусто» прошла бы и тогда, когда искать нечего.
 */
const packageRoot = join(import.meta.dir, '..', '..');
const DEV_HOST_MARKERS = ['api.example.test', 'Ночь CS2 в пятницу', 'dev_host_has_no_route'];

function builtJs(demo: boolean): string {
  const outDir = mkdtempSync(join(tmpdir(), 'afk4-shell-build-'));
  try {
    // bun test выставляет NODE_ENV=test, а с ним Vite считает сборку dev-сборкой: нужен production.
    const env: Record<string, string | undefined> = { ...process.env, NODE_ENV: 'production', VITE_AFK4_DEMO: demo ? '1' : undefined };
    const build = Bun.spawnSync([process.execPath, 'x', 'vite', 'build', '--outDir', outDir, '--emptyOutDir'], {
      cwd: packageRoot,
      env,
      stdout: 'pipe',
      stderr: 'pipe'
    });
    if (build.exitCode !== 0) throw new Error(`vite build failed: ${build.stderr.toString()}${build.stdout.toString()}`);
    const assets = join(outDir, 'assets');
    return readdirSync(assets)
      .filter((file) => file.endsWith('.js'))
      .map((file) => readFileSync(join(assets, file), 'utf8'))
      .join('\n');
  } finally {
    rmSync(outDir, { recursive: true, force: true });
  }
}

describe('сборка оболочки', () => {
  it('боевая не содержит учебного хоста, демо — содержит', () => {
    const production = builtJs(false);
    for (const marker of DEV_HOST_MARKERS) expect(production).not.toContain(marker);

    const demo = builtJs(true);
    for (const marker of DEV_HOST_MARKERS) expect(demo).toContain(marker);
  }, 120_000);
});
