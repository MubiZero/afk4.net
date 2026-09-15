/**
 * `src/AFK4.Shared.Contracts` → `packages/contracts/src/contracts.ts`.
 *
 * Запускается как `bun run gen` из этого пакета. Сгенерированный файл коммитится и проверяется
 * воротами: перегенерация не должна ничего менять. Так же устроена локализация — см.
 * `packages/i18n/scripts/generate-messages.ts`.
 *
 * Зачем вообще: рукописные зеркала контрактов расходились молча. `ShopOrderDto` потерял
 * `posSaleId`, и связь «заказ в баре → чек» была невидима оператору, притом что сервер отдавал её
 * с самого начала. Проверка паритета ловила такое постфактум; генерация делает расхождение
 * невозможным.
 */
import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseContracts, type ContractField, type ContractRecord } from './parse.ts';

const scriptDir = dirname(fileURLToPath(import.meta.url));
const repoRoot = join(scriptDir, '..', '..', '..');
const contractsRoot = join(repoRoot, 'src', 'AFK4.Shared.Contracts');
const outDir = join(scriptDir, '..', 'src');

/**
 * Записи, которые лежат в общих контрактах, но по сети не ходят: это внутренний интерфейс
 * сервера, а не форма ответа. Каждая — с объяснением: список, который можно пополнить молча,
 * перестаёт что-либо значить.
 */
const NOT_ON_THE_WIRE: { record: string; why: string }[] = [
  {
    record: 'NotificationRequest',
    why: 'Заявка на отправку уведомления: её строит сервер для своего же INotificationService.'
  },
  {
    record: 'NotificationAttachment',
    why: 'Вложение уведомления живёт байтами в строке outbox и наружу не отдаётся.'
  }
];

/** Имена-псевдонимы: `string` в рантайме, но в типе видно, что именно за строка. */
const ALIASES = `/** Идентификатор. На проводе это строка, и сравнивать его надо как строку. */
export type Guid = string;

/** Момент времени, ISO-8601 с зоной: \`2026-09-15T10:00:00+05:00\`. */
export type IsoDateTime = string;

/** Дата без времени: \`2026-09-15\`. */
export type IsoDate = string;

/** Время без даты: \`22:30\`. */
export type IsoTime = string;

/** Длительность, ISO-8601: \`PT2H30M\`. */
export type IsoDuration = string;
`;

const PRIMITIVES = new Map<string, string>([
  ['string', 'string'],
  ['Guid', 'Guid'],
  ['int', 'number'],
  ['long', 'number'],
  ['short', 'number'],
  ['byte', 'number'],
  ['double', 'number'],
  ['float', 'number'],
  ['decimal', 'number'],
  ['bool', 'boolean'],
  ['DateTimeOffset', 'IsoDateTime'],
  ['DateTime', 'IsoDateTime'],
  ['DateOnly', 'IsoDate'],
  ['TimeOnly', 'IsoTime'],
  ['TimeSpan', 'IsoDuration']
]);

const LIST_TYPES = ['IReadOnlyList', 'IReadOnlyCollection', 'IEnumerable', 'List', 'ICollection'];
const MAP_TYPES = ['IReadOnlyDictionary', 'Dictionary', 'IDictionary'];

function tsType(type: string, known: Set<string>, where: string): string {
  const primitive = PRIMITIVES.get(type);
  if (primitive !== undefined) return primitive;
  if (known.has(type)) return type;

  const generic = /^([A-Za-z0-9_]+)<(.+)>$/.exec(type);
  if (generic !== null) {
    const [, outer, inner] = generic;
    if (LIST_TYPES.includes(outer)) return `${tsType(inner.trim(), known, where)}[]`;
    if (MAP_TYPES.includes(outer)) {
      const [key, value] = inner.split(',').map((part) => part.trim());
      return `Record<${tsType(key, known, where)}, ${tsType(value, known, where)}>`;
    }
    // Своя generic-запись: `CursorPage<PlayerVisitDto>`.
    if (known.has(outer)) return `${outer}<${tsType(inner.trim(), known, where)}>`;
  }

  // Молчаливый `any` выглядел бы как проверка и ничего бы не проверял.
  throw new Error(
    `Неизвестный тип «${type}» в ${where}. Добавь его в карту типов генератора либо, если запись ` +
    'по сети не ходит, — в NOT_ON_THE_WIRE с объяснением.'
  );
}

function docBlock(lines: string[], indent: string): string {
  if (lines.length === 0) return '';
  if (lines.length === 1) return `${indent}/** ${lines[0]} */\n`;
  // Пустая строка внутри блока — это `*`, а не `* ` с хвостовым пробелом.
  const body = lines.map((line) => (line.length === 0 ? `${indent} *` : `${indent} * ${line}`)).join('\n');
  return `${indent}/**\n${body}\n${indent} */\n`;
}

function fieldLine(field: ContractField, known: Set<string>, where: string): string {
  const type = tsType(field.type, known, where);
  const nullable = field.nullable ? `${type} | null` : type;
  return `${docBlock(field.doc, '  ')}  ${field.name}${field.optional ? '?' : ''}: ${nullable};\n`;
}

function emitTypeScript(records: ContractRecord[], known: Set<string>): string {
  const parts: string[] = [
    '// Сгенерировано из src/AFK4.Shared.Contracts. Руками не править:\n',
    '// правка живёт в записи C#, а сюда приезжает через `bun run gen` в packages/contracts.\n',
    '\n',
    ALIASES,
    '\n'
  ];

  for (const record of records) {
    const parameters = record.typeParameters.length > 0 ? `<${record.typeParameters.join(', ')}>` : '';
    const doc = [...record.doc, record.doc.length > 0 ? '' : null, `Контракт: ${record.file}`]
      .filter((line): line is string => line !== null);
    parts.push(docBlock(doc, ''));
    parts.push(`export interface ${record.name}${parameters} {\n`);
    const scoped = new Set([...known, ...record.typeParameters]);
    for (const field of record.fields) {
      parts.push(fieldLine(field, scoped, `${record.name} (${record.file})`));
    }
    parts.push('}\n\n');
  }

  return parts.join('').replace(/\n{3,}/g, '\n\n').trimEnd() + '\n';
}

const excluded = new Set(NOT_ON_THE_WIRE.map((entry) => entry.record));
const all = parseContracts(contractsRoot);
const records = all.filter((record) => !excluded.has(record.name));

const missing = NOT_ON_THE_WIRE.filter((entry) => !all.some((record) => record.name === entry.record));
if (missing.length > 0) {
  throw new Error(`NOT_ON_THE_WIRE называет записи, которых в контрактах нет: ${missing.map((entry) => entry.record).join(', ')}.`);
}

const known = new Set(records.map((record) => record.name));
mkdirSync(outDir, { recursive: true });
writeFileSync(join(outDir, 'contracts.ts'), emitTypeScript(records, known), 'utf8');
console.log(`сгенерировано ${records.length} типов из ${all.length} записей (${excluded.size} служебных пропущено)`);
