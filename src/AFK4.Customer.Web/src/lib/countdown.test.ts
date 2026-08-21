import { it, expect } from 'bun:test';
import { secondsUntil, formatCountdown } from './countdown';

const now = new Date('2026-08-20T12:00:00Z');

it('считает, сколько осталось до срока', () => {
  expect(secondsUntil('2026-08-20T12:12:30Z', now)).toBe(750);
});

// Срок, который уже прошёл, — это ноль, а не отрицательное число: «осталось −40 секунд»
// человек читать не должен.
it('прошедший срок — это ноль, а не минус', () => {
  expect(secondsUntil('2026-08-20T11:59:20Z', now)).toBe(0);
});

it('нечитаемая дата не притворяется сроком', () => {
  expect(secondsUntil('не дата', now)).toBeNull();
  expect(secondsUntil(null, now)).toBeNull();
});

it('показывает минуты и секунды, пока счёт идёт на минуты', () => {
  expect(formatCountdown(750)).toBe('12:30');
  expect(formatCountdown(9)).toBe('00:09');
});

// Срок ответа настраивается филиалом вплоть до суток, поэтому час на табло обязан помещаться.
it('добавляет часы, когда срок длиннее часа', () => {
  expect(formatCountdown(3725)).toBe('1:02:05');
});
