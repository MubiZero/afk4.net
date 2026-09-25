import { describe, expect, it } from 'bun:test';
import { BANK_WAIT_MS, outcomeOf, parseMajorAmount } from './topUp';

describe('пополнение', () => {
  it('сумма — в основных единицах, с запятой или точкой', () => {
    expect(parseMajorAmount('35')).toBe(3_500);
    expect(parseMajorAmount('35,5')).toBe(3_550);
    expect(parseMajorAmount(' 1 000 ')).toBe(100_000);
    expect(parseMajorAmount('0')).toBeNull();
    expect(parseMajorAmount('abc')).toBeNull();
    expect(parseMajorAmount('1.234')).toBeNull();
  });

  it('оплачено — только по слову сервера', () => {
    expect(outcomeOf('paid', 1_000, 5_000)).toEqual({ kind: 'paid', minorUnits: 5_000 });
    expect(outcomeOf('failed', 1_000, 5_000)).toEqual({ kind: 'problem', key: 'playerShell.topUp.failed' });
    expect(outcomeOf('pending', 1_000, 5_000)).toBeNull();
  });

  it('не дождались банка — не ошибка, а «деньги придут сами»', () => {
    expect(outcomeOf('pending', BANK_WAIT_MS, 5_000)).toEqual({ kind: 'problem', key: 'playerShell.topUp.slow' });
  });
});
