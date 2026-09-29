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
    // The "+" is a geometrically-centred icon, not a glyph.
    expect(free?.querySelector('svg')).not.toBeNull();
    // A free seat invites with "+", it does not show a meaningless "Гость" / billing line.
    expect(container.querySelector('.seat-main')).toBeNull();
    expect(container.querySelector('.seat-billing')).toBeNull();
    expect(container.textContent).not.toContain('Гость');
  });

  it('leads an active prepaid session with the remaining time and no placeholder data', () => {
    const { container } = renderTile(
      seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 1800, remaining: '30 мин', app: 'Agent 0.4 · Shell 0.4' })
    );
    // The remaining time is the hero — it's the one real per-seat datum we have.
    const clock = container.querySelector('.seat-clock');
    expect(clock?.textContent).toContain('30 мин');
    // Billing mode is hardcoded server-side ('Wallet'), so it must NOT pose as real data on the tile;
    // the real player/tariff arrive with the B1 floor-map DTO extension.
    expect(container.querySelector('.seat-billing')).toBeNull();
    expect(container.textContent).not.toContain('Кошелёк');
    // Technical version data must not leak onto the tile (it lives in the side panel).
    expect(container.textContent).not.toContain('Agent');
    expect(container.textContent).not.toContain('Shell');
    expect(container.textContent).not.toContain('0.4');
  });

  // Сумма открытого счёта — герой тела, как время у предоплаты. В шапке она стояла рядом с именем
  // игрока и сжимала его до пары букв (аудит 29.09), поэтому в шапку она больше не попадает.
  it('leads with the rising amount in the body for an open tab, leaving the header to the player', () => {
    const { container } = renderTile(
      seat({ tone: 'active', hasActiveSession: true, remainingSeconds: null, accruedCostMinorUnits: 5400, remaining: '≈ 54 с.', playerDisplayName: 'Юсуф А.' })
    );
    const head = container.querySelector('.seat-head');
    const amount = container.querySelector('.seat-amount');
    expect(amount).not.toBeNull();
    expect(amount?.textContent).toContain('≈ 54 с.');
    expect(head?.contains(amount)).toBe(false);
    expect(container.querySelector('.seat-body')?.contains(amount)).toBe(true);
    expect(container.querySelector('.seat-client')?.textContent).toBe('Юсуф А.');
    // Игрок — своей строкой, не в шапке: там ему не хватало места рядом с колокольчиком и паузой.
    expect(head?.querySelector('.seat-client')).toBeNull();
    // No state-chip when the amount takes the lead slot.
    expect(container.querySelector('.seat-head .state-chip')).toBeNull();
  });

  it('shows a depleting time bar for a prepaid session, flagged low near the end', () => {
    const { container: calm } = renderTile(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 1800, remaining: '30 мин' }));
    const bar = calm.querySelector('.seat-timebar');
    expect(bar).not.toBeNull();
    expect(bar?.classList.contains('seat-timebar--low')).toBe(false);

    const { container: low } = renderTile(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 300, remaining: '5 мин' }));
    expect(low.querySelector('.seat-timebar--low')).not.toBeNull();
  });

  it('reads "Время вышло" instead of "осталось 0 с" when the countdown has run out', () => {
    const { container } = renderTile(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 0, remaining: 'Время вышло' }));
    // No "осталось" prefix and no "0 с" — the hero itself states the time is up.
    expect(container.textContent).not.toContain('осталось');
    expect(container.textContent).not.toContain('0 с');
    expect(container.querySelector('.seat-clock')?.textContent).toContain('Время вышло');
    // Bar drops to the expired variant (empty, depleted track), not a misleading full bar.
    expect(container.querySelector('.seat-timebar--expired')).not.toBeNull();
  });

  it('adds the alert modifier only for attention/problem tones', () => {
    const { container: loud } = renderTile(seat({ tone: 'offline', remaining: 'Нет heartbeat' }));
    expect(loud.querySelector('.seat-tile')?.classList.contains('seat-tile--alert')).toBe(true);

    // Аудит #4: сбой команды — своё проблемное состояние, тоже громкое, но не «нет связи».
    const { container: failed } = renderTile(seat({ tone: 'failed', remaining: 'Сбой команды' }));
    expect(failed.querySelector('.seat-tile')?.classList.contains('seat-tile--alert')).toBe(true);
    expect(failed.querySelector('.seat-tile')?.classList.contains('state-failed')).toBe(true);

    const { container: quiet } = renderTile(seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 1800 }));
    expect(quiet.querySelector('.seat-tile')?.classList.contains('seat-tile--alert')).toBe(false);
  });

  it('shows the real client name next to the PC name for a named session', () => {
    const { container } = renderTile(
      seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 1800, playerDisplayName: 'Сабрина М.' })
    );
    const client = container.querySelector('.seat-client');
    expect(client?.textContent).toBe('Сабрина М.');
  });

  it('shows «Гость» next to the PC name for a guest session without an account', () => {
    const { container } = renderTile(
      seat({ tone: 'active', hasActiveSession: true, remainingSeconds: 1800, playerDisplayName: null })
    );
    expect(container.querySelector('.seat-client')?.textContent).toBe('Гость');
  });

  it('shows no client line for a free seat (the "+" invite stands alone)', () => {
    const { container } = renderTile(seat({ tone: 'ready', hasActiveSession: false }));
    expect(container.querySelector('.seat-client')).toBeNull();
  });
});

// Из двух зовущих мест стойка должна видеть, к какому идти первым, — поэтому на плитке время
// ожидания, а не просто значок «зовут».
describe('SeatTile: вызов оператора', () => {
  it('показывает, сколько место уже ждёт', () => {
    const calledAt = new Date(Date.now() - 5 * 60_000).toISOString();
    const { getByLabelText } = renderTile(seat({ assistanceRequestedAtUtc: calledAt }));

    const badge = getByLabelText('Зовёт администратора');
    expect(badge.textContent).toContain('5');
  });

  it('молчащее место значка не несёт', () => {
    const { queryByLabelText } = renderTile(seat({}));
    expect(queryByLabelText('Зовёт администратора')).toBeNull();
  });
});

// Тон плитки огрубляет состояние: пауза и игра одинаково «занято». Оператору разница важна —
// на паузе счётчик стоит.
describe('SeatTile: пауза', () => {
  it('называет паузу словом, а не оставляет её выглядеть игрой', () => {
    const { getByText } = renderTile(seat({ tone: 'active', activeSessionId: 's1', sessionState: 'Paused' }));
    expect(getByText('на паузе')).toBeInTheDocument();
  });

  it('играющее место паузой не подписано', () => {
    const { queryByText } = renderTile(seat({ tone: 'active', activeSessionId: 's1', sessionState: 'Active' }));
    expect(queryByText('на паузе')).toBeNull();
  });
});

