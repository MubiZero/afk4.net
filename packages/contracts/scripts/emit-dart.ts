/**
 * Записи контрактов → классы Dart с `fromJson`/`toJson`.
 *
 * Зачем отдельный вывод, а не общий с TypeScript: Dart не читает TS, а держать третью рукописную
 * копию контрактов — верный способ развести их. Источник остаётся один, как у каталога строк.
 */
import type { ContractField, ContractNames, ContractRecord } from './parse.ts';

const PRIMITIVES = new Map<string, { type: string; read: (expression: string) => string; write: (expression: string) => string }>([
  ['string', { type: 'String', read: (value) => `${value} as String`, write: (value) => value }],
  ['Guid', { type: 'String', read: (value) => `${value} as String`, write: (value) => value }],
  ['int', { type: 'int', read: (value) => `(${value} as num).toInt()`, write: (value) => value }],
  ['long', { type: 'int', read: (value) => `(${value} as num).toInt()`, write: (value) => value }],
  ['short', { type: 'int', read: (value) => `(${value} as num).toInt()`, write: (value) => value }],
  ['double', { type: 'double', read: (value) => `(${value} as num).toDouble()`, write: (value) => value }],
  ['float', { type: 'double', read: (value) => `(${value} as num).toDouble()`, write: (value) => value }],
  ['decimal', { type: 'double', read: (value) => `(${value} as num).toDouble()`, write: (value) => value }],
  ['bool', { type: 'bool', read: (value) => `${value} as bool`, write: (value) => value }],
  ['DateTimeOffset', { type: 'DateTime', read: (value) => `DateTime.parse(${value} as String)`, write: (value) => `${value}.toIso8601String()` }],
  ['DateTime', { type: 'DateTime', read: (value) => `DateTime.parse(${value} as String)`, write: (value) => `${value}.toIso8601String()` }],
  // Дата и время без зоны ходят строкой и строкой же показываются: `2026-09-15`, `22:30`.
  ['DateOnly', { type: 'String', read: (value) => `${value} as String`, write: (value) => value }],
  ['TimeOnly', { type: 'String', read: (value) => `${value} as String`, write: (value) => value }],
  ['TimeSpan', { type: 'String', read: (value) => `${value} as String`, write: (value) => value }]
]);

const LIST_TYPES = ['IReadOnlyList', 'IReadOnlyCollection', 'IEnumerable', 'List', 'ICollection'];
const MAP_TYPES = ['IReadOnlyDictionary', 'Dictionary', 'IDictionary'];

function generic(type: string): { outer: string; inner: string } | null {
  const match = /^([A-Za-z0-9_]+)<(.+)>$/.exec(type);
  return match === null ? null : { outer: match[1], inner: match[2].trim() };
}

function dartType(type: string, known: Set<string>, where: string): string {
  const primitive = PRIMITIVES.get(type);
  if (primitive !== undefined) return primitive.type;
  if (known.has(type)) return type;

  const parts = generic(type);
  if (parts !== null) {
    if (LIST_TYPES.includes(parts.outer)) return `List<${dartType(parts.inner, known, where)}>`;
    if (MAP_TYPES.includes(parts.outer)) {
      const [key, value] = parts.inner.split(',').map((part) => part.trim());
      return `Map<${dartType(key, known, where)}, ${dartType(value, known, where)}>`;
    }
    if (known.has(parts.outer)) return `${parts.outer}<${dartType(parts.inner, known, where)}>`;
  }

  throw new Error(`Неизвестный тип «${type}» в ${where} (вывод Dart).`);
}

/** Чтение одного значения из уже развёрнутого выражения (не-null). */
function readValue(type: string, expression: string, known: Set<string>, where: string): string {
  const primitive = PRIMITIVES.get(type);
  if (primitive !== undefined) return primitive.read(expression);
  if (known.has(type)) return `${type}.fromJson(${expression} as Map<String, dynamic>)`;

  const parts = generic(type);
  if (parts !== null) {
    if (LIST_TYPES.includes(parts.outer)) {
      const item = readValue(parts.inner, 'item', known, where);
      return `(${expression} as List<dynamic>).map((item) => ${item}).toList()`;
    }
    if (MAP_TYPES.includes(parts.outer)) {
      const [, value] = parts.inner.split(',').map((part) => part.trim());
      const read = readValue(value, 'value', known, where);
      return `(${expression} as Map<String, dynamic>).map((key, value) => MapEntry(key, ${read}))`;
    }
  }
  throw new Error(`Неизвестный тип «${type}» в ${where} (чтение Dart).`);
}

function writeValue(type: string, expression: string, known: Set<string>, nullable = false): string {
  const access = nullable ? '?.' : '.';
  const primitive = PRIMITIVES.get(type);
  if (primitive !== undefined) {
    return nullable ? primitive.write(expression).replace(/\.(?=[a-zA-Z])/, '?.') : primitive.write(expression);
  }
  if (known.has(type)) return `${expression}${access}toJson()`;

  const parts = generic(type);
  if (parts !== null) {
    if (LIST_TYPES.includes(parts.outer)) {
      return `${expression}${access}map((item) => ${writeValue(parts.inner, 'item', known)}).toList()`;
    }
    if (MAP_TYPES.includes(parts.outer)) {
      const [, value] = parts.inner.split(',').map((part) => part.trim());
      return `${expression}${access}map((key, value) => MapEntry(key, ${writeValue(value, 'value', known)}))`;
    }
  }
  return expression;
}

function doc(lines: string[], indent: string): string {
  return lines.map((line) => `${indent}/// ${line}`.trimEnd()).join('\n') + (lines.length > 0 ? '\n' : '');
}

function fieldRead(field: ContractField, known: Set<string>, where: string): string {
  const source = `json['${field.name}']`;
  // Поле с умолчанием может не прийти вовсе, обнуляемое приходит с null — оба читаются как null.
  const optional = field.nullable || field.optional;
  if (!optional) return `        ${field.name}: ${readValue(field.type, source, known, where)},`;
  return `        ${field.name}: ${source} == null ? null : ${readValue(field.type, source, known, where)},`;
}

// Зарезервированные слова Dart именем поля быть не могут (встроенные вроде `operator` — могут).
const DART_RESERVED = new Set(['assert', 'break', 'case', 'catch', 'class', 'const', 'continue', 'default', 'do', 'else', 'enum',
  'extends', 'false', 'final', 'finally', 'for', 'if', 'in', 'is', 'new', 'null', 'rethrow', 'return', 'super', 'switch', 'this',
  'throw', 'true', 'try', 'var', 'void', 'while', 'with']);

/** `NoShow` → `noShow`; зарезервированное слово получает подчёркивание: `Void` → `void_`. */
function constantName(name: string): string {
  const camel = name.charAt(0).toLowerCase() + name.slice(1);
  return DART_RESERVED.has(camel) ? `${camel}_` : camel;
}

/**
 * Словарь кодов → класс констант: `SeatStateNames.free`. Тип поля в Dart остаётся `String` — у
 * языка нет объединений строк, а enum сломал бы разбор кода, которого старое приложение не знает.
 * Зато сравнение идёт с константой, и опечатка в имени — ошибка компиляции, а не тихое «никогда».
 */
function emitDartNames(names: ContractNames): string {
  const docLines = [...names.doc, names.doc.length > 0 ? '' : null, `Словарь: ${names.file}`]
    .filter((line): line is string => line !== null);
  const values = names.values
    .map((value) => `${value.doc.length > 0 ? doc(value.doc, '  ') : ''}  static const String ${constantName(value.name)} = '${value.value}';\n`)
    .join('');
  return `${doc(docLines, '')}abstract final class ${names.name} {\n${values}}\n\n`;
}

export function emitDart(records: ContractRecord[], known: Set<string>, dictionaries: ContractNames[] = []): string {
  const parts: string[] = [
    '// Сгенерировано из src/AFK4.Shared.Contracts. Руками не править:\n',
    '// правка живёт в записи C#, а сюда приезжает через `bun run gen` в packages/contracts.\n',
    '//\n',
    '// ignore_for_file: lines_longer_than_80_chars\n',
    'library;\n\n'
  ];

  for (const names of dictionaries) parts.push(emitDartNames(names));

  for (const record of records) {
    const scoped = new Set([...known, ...record.typeParameters]);
    const where = `${record.name} (${record.file})`;
    const parameters = record.typeParameters.length > 0 ? `<${record.typeParameters.join(', ')}>` : '';
    const docLines = [...record.doc, record.doc.length > 0 ? '' : null, `Контракт: ${record.file}`]
      .filter((line): line is string => line !== null);

    parts.push(doc(docLines, ''));
    parts.push(`class ${record.name}${parameters} {\n`);
    parts.push(`  const ${record.name}({\n`);
    for (const field of record.fields) {
      const optional = field.nullable || field.optional;
      parts.push(`    ${optional ? '' : 'required '}this.${field.name},\n`);
    }
    parts.push('  });\n\n');

    for (const field of record.fields) {
      if (field.doc.length > 0) parts.push(`\n${doc(field.doc, '  ')}`);
      const optional = field.nullable || field.optional;
      parts.push(`  final ${dartType(field.type, scoped, where)}${optional ? '?' : ''} ${field.name};\n`);
    }
    parts.push('\n');

    if (record.typeParameters.length === 0) {
      parts.push(`  factory ${record.name}.fromJson(Map<String, dynamic> json) => ${record.name}(\n`);
      for (const field of record.fields) parts.push(`${fieldRead(field, scoped, where)}\n`);
      parts.push('      );\n\n');
    } else {
      // Страница курсора несёт чужие элементы, и разбирать их умеет только вызывающий.
      const [parameter] = record.typeParameters;
      parts.push(`  factory ${record.name}.fromJson(\n`);
      parts.push('    Map<String, dynamic> json,\n');
      parts.push(`    ${parameter} Function(Map<String, dynamic>) item,\n`);
      parts.push(`  ) => ${record.name}${parameters}(\n`);
      for (const field of record.fields) {
        const parts2 = generic(field.type);
        if (parts2 !== null && LIST_TYPES.includes(parts2.outer) && parts2.inner === parameter) {
          parts.push(`        ${field.name}: (json['${field.name}'] as List<dynamic>).map((entry) => item(entry as Map<String, dynamic>)).toList(),\n`);
          continue;
        }
        parts.push(`${fieldRead(field, scoped, where)}\n`);
      }
      parts.push('      );\n\n');
    }

    if (record.typeParameters.length > 0) {
      parts.push('}\n\n');
      continue;
    }

    parts.push('  Map<String, dynamic> toJson() => {\n');
    for (const field of record.fields) {
      const optional = field.nullable || field.optional;
      const written = writeValue(field.type, field.name, scoped);
      // У обнуляемого поля превращение значения пишется через `?.`, иначе анализатор Dart
      // справедливо ругается на `== null ? null :` там, где язык умеет короче.
      parts.push(optional && written !== field.name
        ? `        '${field.name}': ${writeValue(field.type, field.name, scoped, true)},\n`
        : `        '${field.name}': ${written},\n`);
    }
    parts.push('      };\n');
    parts.push('}\n\n');
  }

  return parts.join('').trimEnd() + '\n';
}
