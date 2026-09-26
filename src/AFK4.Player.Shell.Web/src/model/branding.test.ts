import { describe, expect, it } from 'bun:test';
import { clubAccent, contrast } from './branding';

describe('цвет клуба', () => {
  it('читаемый цвет остаётся цветом клуба', () => {
    expect(clubAccent('#2cc592')).toBe('#2cc592');
    expect(clubAccent('F5A623')).toBe('#f5a623');
    expect(clubAccent('#fc0')).toBe('#ffcc00');
  });

  it('тёмный цвет на тёмном фоне не читается — остаётся фирменный зелёный', () => {
    expect(clubAccent('#3a1d6e')).toBeNull();
    expect(clubAccent('#000000')).toBeNull();
  });

  it('мусор вместо цвета не ломает экран', () => {
    expect(clubAccent(null)).toBeNull();
    expect(clubAccent('зелёный')).toBeNull();
  });

  it('контраст считается по WCAG', () => {
    expect(contrast('#ffffff', '#000000')).toBeCloseTo(21, 0);
    expect(contrast('#777777', '#777777')).toBeCloseTo(1, 5);
  });
});
