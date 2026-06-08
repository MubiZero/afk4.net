import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { billingLabel } from './operatorHelpers';

const t = createTranslator('ru');

describe('billingLabel', () => {
  // Every billing token the floor-map data layer can emit must localize to a real
  // label — none may silently fall through to "not set".
  it.each([
    ['Wallet', 'Депозит'],
    ['Package', 'Пакет'],
    ['Постоплата', 'Постоплата'],
    ['Fast guest', 'Гость'],
    ['Открытый счёт', 'Открытый счёт'],
    ['Cash', 'Наличные']
  ])('maps the %s token to its label', (token, expected) => {
    expect(billingLabel(token, t)).toBe(expected);
  });

  it('falls back to "not set" only for the explicit N/A token', () => {
    expect(billingLabel('N/A', t)).toBe('Не задан');
  });
});
