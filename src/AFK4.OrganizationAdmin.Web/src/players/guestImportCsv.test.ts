import { describe, expect, it } from 'bun:test';
import { decodeGuestFile, parseGuestFile } from './guestImportCsv';

describe('parseGuestFile', () => {
  it('reads a Russian Excel export with a header and «;»', () => {
    const parsed = parseGuestFile('﻿Телефон;Имя;Баланс;Бонусы\r\n93 737 00 70;Фарход;1 250,50;10\n+992900000001;"Дилшод ""Боец""";0;\n');

    expect(parsed.unreadable).toEqual([]);
    expect(parsed.rows).toEqual([
      { phone: '93 737 00 70', name: 'Фарход', balanceMinorUnits: 125050, bonusMinorUnits: 1000 },
      { phone: '+992900000001', name: 'Дилшод "Боец"', balanceMinorUnits: 0, bonusMinorUnits: 0 }
    ]);
  });

  it('reads a headerless comma file and names the rows it cannot read', () => {
    const parsed = parseGuestFile('937370070,Фарход,15.5\n937370071,Зарина,пятьсот');

    expect(parsed.rows).toEqual([{ phone: '937370070', name: 'Фарход', balanceMinorUnits: 1550, bonusMinorUnits: 0 }]);
    expect(parsed.unreadable).toEqual([2]);
  });

  it('номера строк — как в файле: с заголовком и пустыми строками', () => {
    const parsed = parseGuestFile('Телефон;Имя;Баланс\n937370070;Фарход;10\n\n937370071;Зарина;пятьсот\n937370072;Али;5');

    expect(parsed.lineNumbers).toEqual([2, 5]);
    expect(parsed.unreadable).toEqual([4]);
  });

  it('файл без сумм и без заголовка не теряет первого гостя', () => {
    const parsed = parseGuestFile('937370070;Фарход\n937370071;Зарина');

    expect(parsed.rows.map((row) => row.name)).toEqual(['Фарход', 'Зарина']);
  });
});

describe('decodeGuestFile', () => {
  it('читает CSV из русского Excel в cp1251', () => {
    // «Фарход;Ёж» в cp1251.
    const bytes = new Uint8Array([0xd4, 0xe0, 0xf0, 0xf5, 0xee, 0xe4, 0x3b, 0xa8, 0xe6]);

    expect(decodeGuestFile(bytes)).toBe('Фарход;Ёж');
  });

  it('UTF-8 читает как UTF-8', () => {
    expect(decodeGuestFile(new TextEncoder().encode('Зарина;№1'))).toBe('Зарина;№1');
  });
});
