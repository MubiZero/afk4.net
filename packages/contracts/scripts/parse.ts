/**
 * Разбор `src/AFK4.Shared.Contracts` — единственного источника правды о том, что ходит по сети.
 *
 * Читается исходник, а не сборка: рефлексия по dll потребовала бы собранного проекта на каждой
 * машине и в каждой дорожке CI, а разбору текста не нужно ничего. Форма записей здесь регулярна:
 * 445 записей, ни одного наследования, один generic.
 *
 * Правило одно: неизвестное — отказ, а не `any`. Молчаливый `any` в сгенерированном типе хуже
 * отсутствия генератора: он выглядит как проверка и ничего не проверяет.
 */
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

export interface ContractField {
  /** Имя в том виде, в каком его отдаёт сериализатор: с маленькой буквы. */
  name: string;
  /** Тип как он записан в C#, без `?` и без пространства имён. */
  type: string;
  nullable: boolean;
  /** Есть значение по умолчанию — значит поле можно не присылать. */
  optional: boolean;
  doc: string[];
}

export interface ContractRecord {
  name: string;
  namespace: string;
  /** Путь файла относительно корня контрактов — ссылка для человека. */
  file: string;
  typeParameters: string[];
  doc: string[];
  fields: ContractField[];
}

function sourceFiles(dir: string, found: string[] = []): string[] {
  for (const entry of readdirSync(dir)) {
    if (entry === 'bin' || entry === 'obj') continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) {
      sourceFiles(full, found);
      continue;
    }
    if (entry.endsWith('.cs')) found.push(full);
  }
  return found.sort();
}

/** Индекс парной скобки. Считает вложенность, а не ищет первую закрывающую. */
function matching(source: string, open: number, opener: string, closer: string): number {
  let depth = 0;
  for (let index = open; index < source.length; index += 1) {
    if (source[index] === opener) depth += 1;
    else if (source[index] === closer) {
      depth -= 1;
      if (depth === 0) return index;
    }
  }
  throw new Error('Не найдена закрывающая скобка.');
}

/** Снимает разметку XML-дока: в TypeScript и Dart она не значит ничего, а человек читает текст. */
function cleanDoc(line: string): string {
  return line
    .replace(/^\/\/\/?/, '')
    .replace(/<\/?(summary|remarks|para)>/g, '')
    .replace(/<(?:paramref|see|typeparamref)\s+(?:name|cref)="([^"]*)"\s*\/?>/g, '$1')
    .replace(/<\/?c>/g, '`')
    .trim();
}

/** Начало строчного комментария, не считая того, что стоит внутри строкового литерала. */
function lineCommentAt(line: string): number {
  const index = line.indexOf('//');
  if (index < 0) return -1;
  const quotes = (line.slice(0, index).match(/"/g) ?? []).length;
  return quotes % 2 === 1 ? -1 : index;
}

/** Комментарий, стоящий над объявлением в файле: и `///`, и `//`. */
function docAbove(source: string, index: number): string[] {
  const lines = source.slice(0, index).split('\n');
  const collected: string[] = [];
  for (let cursor = lines.length - 2; cursor >= 0; cursor -= 1) {
    const line = lines[cursor].trim();
    if (line.length === 0 && collected.length === 0) continue;
    if (line.startsWith('//')) {
      collected.unshift(cleanDoc(line));
      continue;
    }
    // Атрибут над объявлением комментарий не разрывает.
    if (line.startsWith('[') && line.endsWith(']')) continue;
    break;
  }
  return collected.filter((line) => line.length > 0);
}

interface RawParameter {
  text: string;
  doc: string[];
}

/**
 * Список параметров записи → параметры вместе с их комментариями.
 *
 * Резать сразу по запятым нельзя: комментарий над параметром сам полон запятых, и список
 * разлетается на осколки фраз. Поэтому читается построчно — комментарий снимается со строки
 * до подсчёта скобок, а запятые считаются только в коде.
 */
function parseParameters(parameters: string): RawParameter[] {
  const found: RawParameter[] = [];
  let pendingDoc: string[] = [];
  let buffer = '';
  let depth = 0;

  for (const rawLine of parameters.split('\n')) {
    const trimmed = rawLine.trim();
    if (trimmed.startsWith('//')) {
      pendingDoc.push(cleanDoc(trimmed));
      continue;
    }

    const commentAt = lineCommentAt(rawLine);
    const code = commentAt < 0 ? rawLine : rawLine.slice(0, commentAt);
    const trailing = commentAt < 0 ? '' : cleanDoc(rawLine.slice(commentAt));
    let closedHere = false;

    for (const character of code) {
      if (character === '<' || character === '(' || character === '[') depth += 1;
      if (character === '>' || character === ')' || character === ']') depth -= 1;
      if (character === ',' && depth === 0) {
        if (buffer.trim().length > 0) found.push({ text: buffer.trim(), doc: pendingDoc });
        buffer = '';
        pendingDoc = [];
        closedHere = true;
        continue;
      }
      buffer += character;
    }

    // Хвостовой комментарий поясняет параметр, закончившийся на этой же строке.
    if (trailing.length > 0) {
      if (closedHere && found.length > 0) found[found.length - 1].doc.push(trailing);
      else pendingDoc.push(trailing);
    }
  }

  if (buffer.trim().length > 0) found.push({ text: buffer.trim(), doc: pendingDoc });
  return found;
}

function field(declaration: string, doc: string[]): ContractField {
  const [left, ...rest] = declaration.split('=');
  const optional = rest.length > 0;
  const words = left.trim().split(/\s+/);
  const rawName = words.pop()!;
  const qualified = words.join(' ').replace(/^(?:params|in|out|ref)\s+/, '');
  const nullable = qualified.endsWith('?');
  const bare = nullable ? qualified.slice(0, -1) : qualified;
  // `AFK4.Shared.Contracts.Billing.MoneyDto` и `MoneyDto` — одно имя.
  const type = bare.includes('<') ? bare : bare.split('.').pop()!;
  return {
    name: rawName.charAt(0).toLowerCase() + rawName.slice(1),
    type,
    nullable,
    optional,
    doc: doc.filter((line) => line.length > 0)
  };
}

/** Автосвойства из тела записи: поле можно объявить не параметром, и сериализуется оно наравне. */
function bodyFields(body: string): ContractField[] {
  const fields: ContractField[] = [];
  const property = /public\s+((?:[A-Za-z0-9_.]+(?:<[^>]*>)?\??)(?:\[\])?)\s+([A-Z][A-Za-z0-9_]*)\s*\{\s*get\s*;/g;
  for (const match of body.matchAll(property)) {
    fields.push(field(`${match[1]} ${match[2]}`, docAbove(body, match.index!)));
  }
  return fields;
}

export function parseContracts(contractsRoot: string): ContractRecord[] {
  const records: ContractRecord[] = [];
  for (const file of sourceFiles(contractsRoot)) {
    const source = readFileSync(file, 'utf8');
    const namespace = /namespace\s+([A-Za-z0-9_.]+)\s*;/.exec(source)?.[1] ?? '';
    const relative = file.slice(contractsRoot.length + 1);

    for (const match of source.matchAll(/\brecord\s+([A-Za-z0-9_]+)\s*(<[^>]*>)?\s*([({])/g)) {
      const name = match[1];
      const typeParameters = match[2] ? match[2].slice(1, -1).split(',').map((part) => part.trim()) : [];
      const doc = docAbove(source, match.index!);
      const fields: ContractField[] = [];

      let afterHeader = match.index! + match[0].length - 1;
      if (source[afterHeader] === '(') {
        const close = matching(source, afterHeader, '(', ')');
        for (const parameter of parseParameters(source.slice(afterHeader + 1, close))) {
          fields.push(field(parameter.text, parameter.doc));
        }
        afterHeader = close + 1;
      }

      const rest = source.slice(afterHeader);
      const brace = rest.indexOf('{');
      const semicolon = rest.indexOf(';');
      if (brace >= 0 && (semicolon < 0 || brace < semicolon)) {
        const body = rest.slice(brace + 1, matching(rest, brace, '{', '}'));
        for (const extra of bodyFields(body)) {
          // Свойство может повторять параметр — так нормализуют значение по умолчанию.
          if (!fields.some((existing) => existing.name === extra.name)) fields.push(extra);
        }
      }

      if (fields.length === 0) continue;
      records.push({ name, namespace, file: relative, typeParameters, doc, fields });
    }
  }
  records.sort((left, right) => left.name.localeCompare(right.name));
  return records;
}
