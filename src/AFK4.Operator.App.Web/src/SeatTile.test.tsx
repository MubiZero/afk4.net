import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import { SeatTile } from './SeatTile';

afterEach(cleanup);

function seat(overrides: Partial<SeatSummary>): SeatSummary {
  return {
    id: 's',
    zone: 'Зал A',
    name: 'PC-01',
    tone: 'ready',
    stateLabel: 'Свободно',
    player: 'Гость',
    remaining: 'Свободно',
    billing: 'Fast guest',
    device: 'Device',
    command: 'Idle',
    app: 'Shell',
    ...overrides
  };
}

function renderTile(s: SeatSummary) {
  return render(
    <I18nProvider>
      <SeatTile seat={s} onSelect={() => {}} />
    </I18nProvider>
  );
}

describe('SeatTile', () => {
  it('renders a "+" affordance for a free seat, without a player line or billing', () => {
    const { container } = renderTile(seat({ tone: 'ready', hasActiveSession: false, player: 'Гость' }));
    const free = container.querySelector('.seat-free');
    expect(free).not.toBeNull();
    expect(free?.textContent).toContain('+');
    // A free seat invites with "+", it does not show a meaningless "Гость" / billing line.
    expect(container.querySelector('.seat-main')).toBeNull();
    expect(container.querySelector('.seat-billing')).toBeNull();
    expect(container.textContent).not.toContain('Гость');
  });

  it('shows the billing mode (human label) for an active session and never the agent/shell version', () => {
    const { container } = renderTile(
      seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 1800, remaining: '30 мин', billing: 'Wallet', app: 'Agent 0.4 · Shell 0.4' })
    );
    const billing = container.querySelector('.seat-billing');
    expect(billing).not.toBeNull();
    expect(billing?.textContent?.trim().length).toBeGreaterThan(0);
    // Technical version data must not leak onto the tile (it lives in the side panel).
    expect(container.textContent).not.toContain('Agent');
    expect(container.textContent).not.toContain('Shell');
    expect(container.textContent).not.toContain('0.4');
  });

  it('leads with the rising amount up top for an open tab', () => {
    const { container } = renderTile(
      seat({ tone: 'active', hasActiveSession: true, remainingSeconds: null, accruedCostMinorUnits: 5400, remaining: '≈ 54 с.' })
    );
    const head = container.querySelector('.seat-head');
    const amount = container.querySelector('.seat-amount');
    expect(amount).not.toBeNull();
    expect(amount?.textContent).toContain('≈ 54 с.');
    expect(head?.contains(amount)).toBe(true);
    // No state-chip when the amount takes the lead slot.
    expect(container.querySelector('.seat-head .state-chip')).toBeNull();
  });

  it('shows a depleting time bar for a prepaid session, flagged low near the end', () => {
    const { container: calm } = renderTile(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 1800, remaining: '30 мин' }));
    const bar = calm.querySelector('.seat-timebar');
    expect(bar).not.toBeNull();
    expect(bar?.classList.contains('seat-timebar--low')).toBe(false);

    const { container: low } = renderTile(seat({ tone: 'warning', hasActiveSession: true, remainingSeconds: 300, remaining: '5 мин' }));
    expect(low.querySelector('.seat-timebar--low')).not.toBeNull();
  });

  it('adds the alert modifier only for attention/problem tones', () => {
    const { container: loud } = renderTile(seat({ tone: 'offline', remaining: 'Нет heartbeat' }));
    expect(loud.querySelector('.seat-tile')?.classList.contains('seat-tile--alert')).toBe(true);

    const { container: quiet } = renderTile(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 1800 }));
    expect(quiet.querySelector('.seat-tile')?.classList.contains('seat-tile--alert')).toBe(false);
  });
});
