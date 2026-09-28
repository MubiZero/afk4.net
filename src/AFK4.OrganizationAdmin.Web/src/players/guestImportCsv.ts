import type { GuestImportRowDto } from '@afk4/contracts';

/**
 * Выгрузка гостей из прежней программы: номер, имя, баланс, бонусы (бонусов может не быть).
 * Разделитель — точка с запятой, запятая или табуляция: Excel в русской локали сохраняет «;».
 * Первая строка — заголовок, если в первой ячейке нет ни одной цифры: у номера телефона они есть
 * всегда, а у «Телефон» — нет. Суммы — «150», «150,50», «150.50».
 */
export interface ParsedGuestFile {
  rows: GuestImportRowDto[];
  // Номер строки в файле для каждой из rows — как его показывает Excel, с заголовком и пустыми
  // строками. Сервер называет строки по порядку в rows, а человеку нужен номер из файла.
  lineNumbers: number[];
  // Строки, которые не разобрались вовсе, — тоже номерами из файла.
  unreadable: number[];
}

export function parseGuestFile(text: string): ParsedGuestFile {
  const lines = text
    .replace(/^﻿/, '')
    .split(/\r?\n/)
    .map((line, index) => ({ text: line.trim(), number: index + 1 }))
    .filter((line) => line.text.length > 0);
  if (lines.length === 0) return { rows: [], lineNumbers: [], unreadable: [] };

  const delimiter = detectDelimiter(lines[0].text);
  const cells = lines.map((line) => ({ cells: splitLine(line.text, delimiter), number: line.number }));
  const hasHeader = !/\d/.test(cells[0].cells[0] ?? '');
  const body = hasHeader ? cells.slice(1) : cells;

  const rows: GuestImportRowDto[] = [];
  const lineNumbers: number[] = [];
  const unreadable: number[] = [];
  body.forEach(({ cells: row, number }) => {
    const [phone = '', name = '', balance = '0', bonus = '0'] = row;
    const balanceMinor = money(balance === '' ? '0' : balance);
    const bonusMinor = money(bonus === '' ? '0' : bonus);
    if (balanceMinor === null || bonusMinor === null) {
      unreadable.push(number);
      return;
    }
    rows.push({ phone: phone.trim(), name: name.trim(), balanceMinorUnits: balanceMinor, bonusMinorUnits: bonusMinor });
    lineNumbers.push(number);
  });
  return { rows, lineNumbers, unreadable };
}

/**
 * Байты файла — в текст. Excel в русской Windows сохраняет CSV в cp1251, а не в UTF-8: прочитай
 * такой файл как UTF-8 — и вместо имён будут «�», которые повторный импорт уже не исправит.
 * Сначала строгий UTF-8; не вышло — cp1251. Своя таблица, а не TextDecoder('windows-1251'):
 * не всякая среда эту кодировку знает.
 */
export function decodeGuestFile(bytes: ArrayBuffer | Uint8Array): string {
  const view = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(view);
  } catch {
    let text = '';
    for (const byte of view) text += String.fromCharCode(cp1251(byte));
    return text;
  }
}

// cp1251: 0xC0–0xFF — А–я подряд, 0x80–0xBF — по таблице (Ё, ё, №, кавычки, тире).
const CP1251_HIGH = [
  0x0402, 0x0403, 0x201a, 0x0453, 0x201e, 0x2026, 0x2020, 0x2021, 0x20ac, 0x2030, 0x0409, 0x2039, 0x040a, 0x040c, 0x040b, 0x040f,
  0x0452, 0x2018, 0x2019, 0x201c, 0x201d, 0x2022, 0x2013, 0x2014, 0x0098, 0x2122, 0x0459, 0x203a, 0x045a, 0x045c, 0x045b, 0x045f,
  0x00a0, 0x040e, 0x045e, 0x0408, 0x00a4, 0x0490, 0x00a6, 0x00a7, 0x0401, 0x00a9, 0x0404, 0x00ab, 0x00ac, 0x00ad, 0x00ae, 0x0407,
  0x00b0, 0x00b1, 0x0406, 0x0456, 0x0491, 0x00b5, 0x00b6, 0x00b7, 0x0451, 0x2116, 0x0454, 0x00bb, 0x0458, 0x0405, 0x0455, 0x0457
];

function cp1251(byte: number): number {
  if (byte < 0x80) return byte;
  if (byte >= 0xc0) return 0x0410 + (byte - 0xc0);
  return CP1251_HIGH[byte - 0x80];
}

function detectDelimiter(line: string): string {
  const counts = [';', '\t', ','].map((candidate) => ({ candidate, count: line.split(candidate).length - 1 }));
  return counts.sort((a, b) => b.count - a.count)[0].count > 0 ? counts[0].candidate : ';';
}

function splitLine(line: string, delimiter: string): string[] {
  const result: string[] = [];
  let current = '';
  let quoted = false;
  for (let index = 0; index < line.length; index++) {
    const character = line[index];
    if (character === '"') {
      if (quoted && line[index + 1] === '"') { current += '"'; index++; }
      else quoted = !quoted;
    } else if (character === delimiter && !quoted) {
      result.push(current);
      current = '';
    } else {
      current += character;
    }
  }
  result.push(current);
  return result.map((cell) => cell.trim());
}

// «1 250,50» → 125050. null — не сумма.
function money(raw: string): number | null {
  const cleaned = raw.replace(/[\s ]/g, '').replace(/(с\.|сомони|TJS)$/i, '');
  if (!/^\d+([.,]\d{1,2})?$/.test(cleaned)) return null;
  const [whole, fraction = ''] = cleaned.replace(',', '.').split('.');
  return Number(whole) * 100 + Number(fraction.padEnd(2, '0'));
}
