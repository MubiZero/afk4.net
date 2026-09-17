import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { MAX_SEATS_PER_RUN } from './HallScreen';

// Предел «сколько мест за один раз» проверяет хост, а называет экран. Разъехаться им нельзя:
// экран либо отправит заведомо отвергнутый запрос и покажет общее «не удалось», либо запретит
// то, что хост разрешает. Поэтому число читается из самого хоста, а не переписывается сюда.
describe('предел мест за один прогон', () => {
  it('совпадает с тем, что проверяет хост', () => {
    const source = readFileSync(
      resolve(import.meta.dir, '../../AFK4.SetupWizard.Core/SetupWizardWebHostBridge.cs'),
      'utf8',
    );
    const match = source.match(/MaxSeatsPerRun\s*=\s*(?<limit>\d+)/);

    expect(match).not.toBeNull();
    expect(Number(match!.groups!.limit)).toBe(MAX_SEATS_PER_RUN);
  });
});
