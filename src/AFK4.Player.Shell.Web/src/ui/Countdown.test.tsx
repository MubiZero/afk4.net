import { describe, expect, it } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { Countdown } from './Countdown';

describe('отсчёт', () => {
  it('считает по времени платформы с поправкой на часы ПК', () => {
    const now = Date.now();
    // Платформа впереди часов ПК на две минуты, аренда кончается через десять минут по её часам.
    const observedAt = new Date(now + 120_000).toISOString();
    const until = new Date(now + 120_000 + 600_000).toISOString();

    render(<Countdown untilUtc={until} observedAtUtc={observedAt} receivedAtMs={now} />);

    expect(screen.getByTestId('countdown').textContent).toMatch(/^(10:00|09:59)$/);
  });

  it('без срока ничего не рисует', () => {
    const { container } = render(<Countdown untilUtc={null} observedAtUtc={null} receivedAtMs={null} />);
    expect(container).toBeEmptyDOMElement();
  });
});
