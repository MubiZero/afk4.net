import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Поля ответа сервера и поля клиентского типа — одно и то же множество.
 *
 * Генератора TS из C# в проекте нет, поэтому типы написаны руками и расходятся молча: так из
 * `ShopOrderDto` пропал `posSaleId`, и связь «заказ в баре → чек» была невидима оператору,
 * притом что сервер отдавал её с самого начала. Сверять две рукописные копии друг с другом
 * бессмысленно — читается исходник контрактов, и он здесь единственный источник правды.
 *
 * Список проверяемых типов неполон: часть клиентских DTO всё ещё объявлена как
 * `Record<string, unknown>` — контракта у них нет вовсе, и сверять там нечего. Каждый, кто
 * доводит такой тип до настоящего, добавляет его сюда.
 */
const repoRoot = join(import.meta.dir, '..', '..', '..');
const contractsRoot = join(repoRoot, 'AFK4.Shared.Contracts');

interface Pair {
  /// Имя записи в C#.
  record: string;
  /// Файл контракта относительно AFK4.Shared.Contracts.
  contract: string;
  /// Файл клиентского типа относительно этой папки.
  client: string;
  /// Имя интерфейса в TS.
  type: string;
}

const PAIRS: Pair[] = [
  { record: 'ShopOrderDto', contract: 'Shop/ShopOrderDto.cs', client: 'clients/shopOrders.ts', type: 'ShopOrderDto' },
  { record: 'ShopOrderLineDto', contract: 'Shop/ShopOrderLineDto.cs', client: 'clients/shopOrders.ts', type: 'ShopOrderLineDto' },
  { record: 'ShiftDto', contract: 'Shifts/ShiftDto.cs', client: 'clients/shifts.ts', type: 'ShiftDto' },
  { record: 'CashMovementDto', contract: 'Shifts/CashMovementDto.cs', client: 'clients/shifts.ts', type: 'CashMovementDto' },
  {
    record: 'DeviceInventoryItemDto',
    contract: 'Devices/DeviceInventoryItemDto.cs',
    client: 'clients/devices.ts',
    type: 'DeviceInventoryItemDto'
  },
  {
    record: 'PosProductCategoryDto',
    contract: 'Pos/PosProductCategoryDto.cs',
    client: 'clients/pos.ts',
    type: 'PosProductCategoryDto'
  }
];

/** Имена параметров записи C# в том виде, в каком их отдаёт сериализатор: с маленькой буквы. */
function serverFields(source: string, record: string): string[] {
  const start = source.indexOf(`record ${record}(`);
  if (start < 0) throw new Error(`Запись ${record} не найдена в контракте.`);
  const open = source.indexOf('(', start);
  const close = source.indexOf(');', open);
  const body = source
    .slice(open + 1, close)
    // Комментарии внутри списка параметров — обычное дело в этих файлах.
    .replace(/\/\/[^\n]*/g, '')
    .replace(/\/\*[\s\S]*?\*\//g, '');

  const fields: string[] = [];
  // Тип может нести запятую внутри скобок (`IReadOnlyList<Guid>` не может, а `Dictionary<,>` да),
  // поэтому режем по запятым верхнего уровня.
  let depth = 0;
  let current = '';
  for (const character of `${body},`) {
    if (character === '<' || character === '(') depth += 1;
    if (character === '>' || character === ')') depth -= 1;
    if (character === ',' && depth === 0) {
      const parameter = current.trim();
      current = '';
      if (parameter.length === 0) continue;
      // «Тип Имя» или «Тип Имя = значение».
      const name = parameter.split('=')[0].trim().split(/\s+/).pop()!;
      fields.push(name.charAt(0).toLowerCase() + name.slice(1));
      continue;
    }
    current += character;
  }
  return fields;
}

/** Имена полей TS-интерфейса. */
function clientFields(source: string, type: string): string[] {
  const start = source.indexOf(`interface ${type} {`);
  if (start < 0) throw new Error(`Интерфейс ${type} не найден у клиента.`);
  const open = source.indexOf('{', start);
  let depth = 0;
  let end = open;
  for (let index = open; index < source.length; index += 1) {
    if (source[index] === '{') depth += 1;
    if (source[index] === '}') {
      depth -= 1;
      if (depth === 0) {
        end = index;
        break;
      }
    }
  }
  const body = source
    .slice(open + 1, end)
    .replace(/\/\/\/?[^\n]*/g, '')
    .replace(/\/\*[\s\S]*?\*\//g, '');
  return [...body.matchAll(/^\s*([a-zA-Z0-9_]+)\??\s*:/gm)].map((match) => match[1]);
}

describe('поля клиентских типов совпадают с контрактом сервера', () => {
  it('проверяет не пустой список типов', () => {
    expect(PAIRS.length).toBeGreaterThan(3);
  });

  for (const pair of PAIRS) {
    it(`${pair.type} несёт ровно те поля, что отдаёт ${pair.record}`, () => {
      const server = serverFields(readFileSync(join(contractsRoot, pair.contract), 'utf8'), pair.record);
      const client = clientFields(readFileSync(join(import.meta.dir, pair.client), 'utf8'), pair.type);

      expect(server.length).toBeGreaterThan(0);
      expect([...client].sort()).toEqual([...server].sort());
    });
  }
});
