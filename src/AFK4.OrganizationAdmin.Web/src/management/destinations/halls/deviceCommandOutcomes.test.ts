import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { commandOutcomeLabelKey } from './deviceCommandOutcomes';

// Имена исходов заводит агент, а подписи к ним — этот экран. Разъехаться им нельзя: исход без
// подписи молча исчезает из журнала, и администратор снова остаётся без объяснения. Поэтому
// список читается из самого контракта, а не переписывается сюда руками.
function outcomeNamesFromContract(): string[] {
  const source = readFileSync(
    resolve(import.meta.dir, '../../../../../AFK4.Shared.Contracts/Devices/DeviceCommandOutcomeNames.cs'),
    'utf8'
  );

  return [...source.matchAll(/=\s*"(?<name>[a-z-]+)"/g)].map((match) => match.groups!.name).sort();
}

describe('исходы команд устройства', () => {
  it('у каждого исхода из контракта есть подпись', () => {
    const names = outcomeNamesFromContract();

    expect(names.length).toBeGreaterThan(0);
    expect(names.filter((name) => commandOutcomeLabelKey(name) === null)).toEqual([]);
  });

  it('нет подписи — нет и строки: неизвестный исход не выдаётся за перевод', () => {
    expect(commandOutcomeLabelKey('something-new')).toBeNull();
    expect(commandOutcomeLabelKey(null)).toBeNull();
    expect(commandOutcomeLabelKey(undefined)).toBeNull();
  });
});
