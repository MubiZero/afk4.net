import { describe, expect, it } from 'bun:test';
import { parseGuestFile } from './guestImportCsv';

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
});
