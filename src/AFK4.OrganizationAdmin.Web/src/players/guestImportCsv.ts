import type { GuestImportRowDto } from '@afk4/contracts';

/**
 * Выгрузка гостей из прежней программы: номер, имя, баланс, бонусы (бонусов может не быть).
 * Разделитель — точка с запятой, запятая или табуляция: Excel в русской локали сохраняет «;».
 * Первая строка — заголовок, если в ней нет ни одной суммы. Суммы — «150», «150,50», «150.50».
 */
export interface ParsedGuestFile {
  rows: GuestImportRowDto[];
  // Строки, которые не разобрались вовсе (номер с единицы, без заголовка).
  unreadable: number[];
}

export function parseGuestFile(text: string): ParsedGuestFile {
  const lines = text.replace(/^﻿/, '').split(/\r?\n/).map((line) => line.trim()).filter((line) => line.length > 0);
  if (lines.length === 0) return { rows: [], unreadable: [] };

  const delimiter = detectDelimiter(lines[0]);
  const cells = lines.map((line) => splitLine(line, delimiter));
  const hasHeader = cells[0].slice(2).every((cell) => money(cell) === null) && cells[0].length >= 2;
  const body = hasHeader ? cells.slice(1) : cells;

  const rows: GuestImportRowDto[] = [];
  const unreadable: number[] = [];
  body.forEach((row, index) => {
    const [phone = '', name = '', balance = '0', bonus = '0'] = row;
    const balanceMinor = money(balance === '' ? '0' : balance);
    const bonusMinor = money(bonus === '' ? '0' : bonus);
    if (balanceMinor === null || bonusMinor === null) {
      unreadable.push(index + 1);
      return;
    }
    rows.push({ phone: phone.trim(), name: name.trim(), balanceMinorUnits: balanceMinor, bonusMinorUnits: bonusMinor });
  });
  return { rows, unreadable };
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
