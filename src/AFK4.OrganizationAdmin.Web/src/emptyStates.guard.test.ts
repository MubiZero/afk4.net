import { describe, expect, it } from 'bun:test';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

/**
 * Пустое состояние без следующего шага — это «Нет товаров», перед которым кассир не знает, что
 * делать. `EmptyState` не собирается без решения (`next`), но самописная строчка
 * `<p>{t('….empty')}</p>` обходит его молча — так и набралось 33 немых места из 47.
 *
 * Проверка ищет именно этот обход: ключ пустоты, выведенный текстом прямо в разметку. Такое место
 * либо переходит на `EmptyState`, либо попадает в список ниже — с причиной, почему списком
 * оно не является. Список, который можно пополнить молча, перестаёт что-либо значить.
 */
const srcRoot = import.meta.dir;

const EXEMPT: { file: string; key: string; why: string }[] = [
  {
    file: 'CommandPalette.tsx',
    key: 'op.command.palette.empty',
    why: 'Результат набора в палитре: следующий шаг — исправить набранное в поле над строкой.'
  },
  {
    file: 'CommandPalette.tsx',
    key: 'op.command.palette.entityEmpty',
    why: 'То же для поиска клиентов и броней внутри палитры.'
  },
  {
    file: 'SeatContextMenu.tsx',
    key: 'op.map.menu.empty',
    why: 'Пункт контекстного меню, а не список: действий над этим ПК у человека нет.'
  },
  {
    file: 'PosOrdersTicker.tsx',
    key: 'op.shopOrders.empty',
    why: 'Строка состояния в ленте заказов: «активных заказов нет» — это и есть спокойное состояние, места там на одну фразу.'
  },
  {
    file: 'BackendPosWorkspace.tsx',
    key: 'op.pos.cart.emptyTitle',
    why: 'Пустая корзина — строка ленты чека со своей вёрсткой; следующий шаг назван словами рядом (op.pos.cart.emptyHint).'
  },
  {
    file: 'BackendPosWorkspace.tsx',
    key: 'op.pos.cart.emptyHint',
    why: '«Добавьте товар из каталога» — это и есть следующий шаг пустой корзины; каталог в соседней колонке.'
  },
  {
    file: 'cash/CashShiftWorkspace.tsx',
    key: 'op.cash.shift.empty',
    why: 'Подпись героя «нет открытой смены»: под ней кнопка «Открыть смену», а без права — к кому идти.'
  },
  {
    file: 'cash/CashShiftWorkspace.tsx',
    key: 'op.cash.shift.historyEmpty',
    why: 'Та же строка героя без открытой смены, когда закрытых смен ещё не было; действие — кнопка рядом.'
  },
  {
    file: 'stock/ReceivingWorkspace.tsx',
    key: 'op.stock.receiving.noResults',
    why: 'Строка выпадающих результатов поиска: следующий шаг — исправить набранное в поле над ней.'
  }
];

// Ключ пустоты, выведенный текстом прямо в элемент: `>{t('….empty…')}` или
// `>{cond ? t('….empty') : …}`. Ключи, где пустота в имени (`empty`, `noMatch`, `noResults`).
const RAW_EMPTY = />\s*\{[^{}]*?\bt\('([^']*(?:[Ee]mpty|noMatch|noResults)[^']*)'\)/g;

function sourceFiles(dir: string, found: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      if (entry !== 'test') sourceFiles(full, found);
      continue;
    }
    if (entry.endsWith('.tsx') && !entry.includes('.test.')) found.push(full);
  }
  return found;
}

function rawEmptyUsages(): { file: string; key: string }[] {
  const hits: { file: string; key: string }[] = [];
  for (const path of sourceFiles(srcRoot)) {
    const source = readFileSync(path, 'utf8');
    for (const match of source.matchAll(RAW_EMPTY)) {
      hits.push({ file: relative(srcRoot, path), key: match[1] });
    }
  }
  return hits;
}

describe('empty states decide the next step', () => {
  it('no list prints its emptiness as raw text past EmptyState', () => {
    const exempt = new Set(EXEMPT.map((entry) => `${entry.file} ${entry.key}`));
    const offenders = rawEmptyUsages()
      .map((hit) => `${hit.file} ${hit.key}`)
      .filter((hit) => !exempt.has(hit));
    expect(offenders).toEqual([]);
  });

  // Исключение, которого в коде уже нет, — это дверь, оставленная открытой для следующего.
  it('every exemption still points at a real usage', () => {
    const found = new Set(rawEmptyUsages().map((hit) => `${hit.file} ${hit.key}`));
    const stale = EXEMPT.map((entry) => `${entry.file} ${entry.key}`).filter((entry) => !found.has(entry));
    expect(stale).toEqual([]);
  });

  it('every exemption says why', () => {
    expect(EXEMPT.filter((entry) => entry.why.trim().length < 20)).toEqual([]);
  });
});
