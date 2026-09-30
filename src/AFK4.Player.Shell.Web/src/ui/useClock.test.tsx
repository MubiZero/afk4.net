import { afterEach, describe, expect, it, setSystemTime } from 'bun:test';
import { act, cleanup, render, screen } from '@testing-library/react';
import { useClock } from './useSecondTick';

afterEach(() => {
  setSystemTime();
  Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
  cleanup();
});

function Clock() {
  return <span data-testid="now">{useClock('second')}</span>;
}

function setVisibility(value: 'visible' | 'hidden') {
  Object.defineProperty(document, 'visibilityState', { value, configurable: true });
  document.dispatchEvent(new Event('visibilitychange'));
}

describe('часы экрана', () => {
  // Оболочка прячется и засыпает, пока впереди игра; таймеры скрытой страницы растягиваются до
  // минуты. Вернулась — остаток должен быть верным сразу, а не когда дойдёт запоздавший таймер.
  it('при возвращении страницы время берётся из часов сразу', () => {
    setSystemTime(new Date('2026-09-30T10:00:00Z'));
    render(<Clock />);
    const shown = Number(screen.getByTestId('now').textContent);

    setVisibility('hidden');
    // Стена времени ушла на 5 минут вперёд, таймер за это время не сработал ни разу.
    setSystemTime(new Date('2026-09-30T10:05:00Z'));
    expect(Number(screen.getByTestId('now').textContent)).toBe(shown);

    act(() => setVisibility('visible'));

    expect(Number(screen.getByTestId('now').textContent)).toBe(Date.parse('2026-09-30T10:05:00Z'));
  });

  it('пока страница скрыта, событие видимости время не трогает', () => {
    setSystemTime(new Date('2026-09-30T10:00:00Z'));
    render(<Clock />);
    const shown = Number(screen.getByTestId('now').textContent);

    setSystemTime(new Date('2026-09-30T10:05:00Z'));
    act(() => setVisibility('hidden'));

    expect(Number(screen.getByTestId('now').textContent)).toBe(shown);
  });
});
