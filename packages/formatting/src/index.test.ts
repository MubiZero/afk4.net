import { describe, it, expect } from 'bun:test';
import { formatCurrency, formatDateParts, formatLocal, formatNumber, fullPhoneDigits, isTajikLocale, localPhoneDigits } from './index';

it('formats numbers with locale grouping and options', () => {
  // ru-RU groups thousands with a non-breaking space (U+00A0); normalise any
  // whitespace to a regular space so the assertion does not hinge on the glyph.
  expect(formatNumber(1234.5, 'ru-RU', { maximumFractionDigits: 0 }).replace(/\s/g, ' ')).toBe('1 235');
  expect(formatNumber(1234.5, 'en-US', { maximumFractionDigits: 1 })).toBe('1,234.5');
});

it('formats currency with no fractional digits', () => {
  // ru-RU currency formatting uses a non-breaking space and a trailing code/symbol;
  // assert on stable substrings rather than exact glyphs.
  const out = formatCurrency(4250, 'TJS', 'ru-RU');
  expect(out).toContain('4');
  expect(out).toContain('250');
  expect(out).not.toContain(',00');
});

it('formats date parts from an ISO string', () => {
  const time = formatDateParts('2026-06-01T13:05:00.000Z', 'ru-RU', { hour: '2-digit', minute: '2-digit', timeZone: 'UTC' });
  expect(time).toBe('13:05');
});

it('accepts a Date instance', () => {
  const out = formatDateParts(new Date('2026-06-01T00:00:00.000Z'), 'ru-RU', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' });
  expect(out).toBe('01.06.2026');
});

// Маска телефона переехала сюда из двух приложений сразу: у админки клуба и у мастера установки
// лежали побайтно одинаковые копии. Проверки тоже были в двух местах — теперь одни.
describe('телефон Таджикистана', () => {
  it('оставляет девять локальных цифр и срезает код страны', () => {
    expect(localPhoneDigits('+992 93 738 00 70')).toBe('937380070');
    expect(localPhoneDigits('937380070')).toBe('937380070');
    expect(localPhoneDigits('9373800709999')).toBe('937380070');
    expect(localPhoneDigits('')).toBe('');
  });

  it('раскладывает маску 2-3-2-2 и не рисует пустые группы', () => {
    expect(formatLocal('937380070')).toBe('93 738 00 70');
    expect(formatLocal('93')).toBe('93');
    expect(formatLocal('')).toBe('');
  });

  it('на сервер уходит код страны и девять цифр', () => {
    expect(fullPhoneDigits('93 738 00 70')).toBe('992937380070');
    expect(fullPhoneDigits('+992937380070')).toBe('992937380070');
  });
});

// Браузеры не знают таджикского (tg-TJ → en-US): месяцы шли по-английски, время — с AM/PM.
describe('таджикские даты', () => {
  const at = new Date('2026-10-31T09:05:00.000Z');

  it('пишет месяц и день недели по-таджикски', () => {
    expect(formatDateParts(at, 'tg-TJ', { day: 'numeric', month: 'long', timeZone: 'UTC' })).toBe('31 октябр');
    expect(formatDateParts(at, 'tg', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' })).toBe('31 октябр 2026');
    expect(formatDateParts(at, 'tg-TJ', { weekday: 'short', day: 'numeric', month: 'long', timeZone: 'UTC' })).toBe('шанбе 31 октябр');
  });

  it('время — двадцать четыре часа, без AM/PM', () => {
    expect(formatDateParts('2026-10-31T21:40:00.000Z', 'tg-TJ', { hour: '2-digit', minute: '2-digit', timeZone: 'UTC' })).toBe('21:40');
    expect(formatDateParts(at, 'tg-TJ', { dateStyle: 'medium', timeStyle: 'short', timeZone: 'UTC' })).toBe('31 октябр 2026, 09:05');
  });

  it('месяц и день недели берутся в поясе, в котором показывается дата', () => {
    // 31 октября 22:00 UTC — в Душанбе уже 1 ноября, воскресенье.
    expect(formatDateParts('2026-10-31T22:00:00.000Z', 'tg-TJ', { weekday: 'long', day: 'numeric', month: 'long', timeZone: 'Asia/Dushanbe' }))
      .toBe('якшанбе 1 ноябр');
  });

  it('цифровые даты остаются цифрами, а русский и английский — за Intl', () => {
    expect(formatDateParts(at, 'tg-TJ', { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' })).toBe('31.10.2026');
    expect(formatDateParts(at, 'ru-RU', { day: 'numeric', month: 'long', timeZone: 'UTC' })).toBe('31 октября');
    expect(isTajikLocale('tg')).toBe(true);
    expect(isTajikLocale('ru-RU')).toBe(false);
  });
});
