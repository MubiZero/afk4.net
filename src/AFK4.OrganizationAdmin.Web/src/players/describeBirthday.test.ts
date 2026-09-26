import { describe, expect, it } from 'bun:test';
import { describeBirthday } from '../operatorHelpers';

// День рождения гостя в карточке: дата, возраст и «сегодня» — по календарю компьютера в клубе.
describe('describeBirthday', () => {
  it('names the date and the full age', () => {
    const birthday = describeBirthday('2000-03-14', 'ru', new Date(2026, 2, 13));
    expect(birthday.label).toBe('14 марта');
    expect(birthday.age).toBe(25);
    expect(birthday.isToday).toBe(false);
  });

  it('marks the birthday itself', () => {
    const birthday = describeBirthday('2000-03-14', 'ru', new Date(2026, 2, 14));
    expect(birthday.age).toBe(26);
    expect(birthday.isToday).toBe(true);
  });

  // 29 февраля в невисокосный год поздравляют 28-го — так же считает сервер, дарящий подарок.
  it('celebrates a leap-day birthday on the 28th in ordinary years', () => {
    const birthday = describeBirthday('2008-02-29', 'ru', new Date(2026, 1, 28));
    expect(birthday.isToday).toBe(true);
    expect(birthday.age).toBe(18);
  });
});
