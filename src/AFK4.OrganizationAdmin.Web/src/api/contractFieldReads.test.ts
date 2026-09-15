import { describe, expect, it } from 'bun:test';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Поле, которого сервер не отдаёт, читается тихо: `readString(product, 'categoryName')` вернёт
 * пустую строку, а не ошибку, — и экран будет выглядеть рабочим ровно до встречи с живым сервером.
 *
 * Проверка паритета (`contractParity.test.ts`) ловит это только у типизированных DTO. Здесь —
 * вторая половина: все чтения по строковому имени сверяются с именами полей из контрактов.
 *
 * Три случая, из-за которых она написана, жили в проде одновременно:
 * — `categoryName` у товара: подпись категории на складе не появлялась никогда, а стойка
 *   показывала кассиру ряд GUID'ов;
 * — `actorName` и `runningBalance` в карточке кассовой операции: обе строки не показывались;
 * — `createdByDisplayName` в списке движений наличных: экран подставлял туда имя того, кто
 *   смотрит, и каждое чужое движение выглядело его собственным.
 */
const appRoot = join(import.meta.dir, '..');
const appDir = join(appRoot, '..');
const contractsRoot = join(appRoot, '..', '..', 'AFK4.Shared.Contracts');
const apiRoot = join(appRoot, '..', '..', 'AFK4.Platform.Api');

/**
 * Имена, которые читаются по строке, но в контрактах их нет и быть не должно.
 *
 * Каждая запись — с объяснением: список, который можно пополнить молча, перестаёт что-либо
 * значить. Пустой список — самое сильное состояние этой проверки.
 */
const NOT_FROM_SERVER: { field: string; why: string }[] = [];

function camel(name: string): string {
  return name.charAt(0).toLowerCase() + name.slice(1);
}

function stripComments(text: string): string {
  return text.replace(/\/\/[^\n]*/g, '').replace(/\/\*[\s\S]*?\*\//g, '');
}

function matchingParen(source: string, open: number): number {
  let depth = 0;
  for (let index = open; index < source.length; index += 1) {
    if (source[index] === '(') depth += 1;
    if (source[index] === ')') {
      depth -= 1;
      if (depth === 0) return index;
    }
  }
  return -1;
}

/** Имена параметров записи: тип может нести запятую внутри скобок, поэтому режем по верхнему уровню. */
function recordParameters(parameters: string): string[] {
  const fields: string[] = [];
  let depth = 0;
  let current = '';
  for (const character of `${parameters},`) {
    if (character === '<' || character === '(') depth += 1;
    if (character === '>' || character === ')') depth -= 1;
    if (character === ',' && depth === 0) {
      const parameter = current.trim();
      current = '';
      if (parameter.length === 0) continue;
      fields.push(camel(parameter.split('=')[0].trim().split(/\s+/).pop()!));
      continue;
    }
    current += character;
  }
  return fields;
}

function sourceFiles(dir: string, extensions: string[], found: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (['node_modules', 'bin', 'obj', 'dist', 'Migrations'].includes(entry)) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      sourceFiles(full, extensions, found);
      continue;
    }
    if (extensions.some((extension) => entry.endsWith(extension))) found.push(full);
  }
  return found;
}

function serverFieldNames(): Set<string> {
  const names = new Set<string>();
  for (const file of sourceFiles(contractsRoot, ['.cs'])) {
    const source = readFileSync(file, 'utf8');
    for (const match of source.matchAll(/record\s+[A-Za-z0-9_]+(?:<[^>]*>)?\s*\(/g)) {
      const open = source.indexOf('(', match.index!);
      const close = matchingParen(source, open);
      if (close < 0) continue;
      for (const field of recordParameters(stripComments(source.slice(open + 1, close)))) names.add(field);
    }
    // Поле можно объявить не параметром, а свойством — сериализуется оно наравне.
    for (const match of source.matchAll(/public\s+[^\s]+(?:<[^>]*>)?\??\s+([A-Z][A-Za-z0-9_]*)\s*\{\s*get/g)) {
      names.add(camel(match[1]));
    }
    // Имена состояний и видов приходят строками и читаются как значения, но встречаются и как ключи.
    for (const match of source.matchAll(/public\s+const\s+string\s+([A-Z][A-Za-z0-9_]*)/g)) {
      names.add(camel(match[1]));
    }
  }
  // Ключи, которые сервер кладёт в JSON руками: сводки, детали аудита, полезная нагрузка команд.
  for (const file of sourceFiles(apiRoot, ['.cs'])) {
    for (const match of readFileSync(file, 'utf8').matchAll(/\["([a-z][A-Za-z0-9_]*)"\]/g)) {
      names.add(match[1]);
    }
  }
  return names;
}

interface FieldRead {
  field: string;
  file: string;
}

function fieldReads(): FieldRead[] {
  const reads: FieldRead[] = [];
  for (const file of sourceFiles(appDir, ['.ts', '.tsx'])) {
    // Наборы и dev-макет читают свои формы, а не ответы сервера.
    if (file.includes('.test.') || file.endsWith('devMockBackend.ts')) continue;
    const source = readFileSync(file, 'utf8');
    for (const match of source.matchAll(/read(?:String|Number|Money|Array|Record|Boolean)\s*\(\s*[^,]+,\s*'([a-zA-Z0-9_]+)'/g)) {
      reads.push({ field: match[1], file: file.slice(appDir.length + 1) });
    }
  }
  return reads;
}

describe('чтения полей по имени', () => {
  const names = serverFieldNames();
  const reads = fieldReads();
  const allowed = new Set(NOT_FROM_SERVER.map((entry) => entry.field));

  // Разбор контрактов умеет ошибаться молча: пустой набор имён сделал бы проверку бессмысленной,
  // а слишком жадный — разрешил бы что угодно.
  it('разбирает контракты и находит сами чтения', () => {
    expect(names.size).toBeGreaterThan(500);
    expect(names.has('categoryId')).toBe(true);
    expect(names.has('createdByDisplayName')).toBe(true);
    expect(names.has('categoryName')).toBe(false);
    expect(reads.length).toBeGreaterThan(300);
  });

  it('читает только те имена, которые сервер отдаёт', () => {
    const unknown = reads.filter((read) => !names.has(read.field) && !allowed.has(read.field));
    const report = [...new Set(unknown.map((read) => `${read.field} (${read.file})`))].sort();
    expect(report).toEqual([]);
  });

  it('не держит в списке исключений имена, которые сервер отдаёт', () => {
    expect(NOT_FROM_SERVER.filter((entry) => names.has(entry.field)).map((entry) => entry.field)).toEqual([]);
  });
});
