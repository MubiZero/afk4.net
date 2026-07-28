import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import { buildSeatMenu, type SeatMenuItem } from './seatMenu';
import { SeatContextMenu } from './SeatContextMenu';

afterEach(cleanup);

function seat(overrides: Partial<SeatSummary>): SeatSummary {
  return {
    id: 's', zone: 'Зал A', name: 'PC-07', tone: 'active', stateLabel: 'В сессии',
    player: 'Гость', remaining: '30 мин', device: 'Device',
    command: 'Idle', app: 'Shell', deviceId: 'dev-1', activeSessionId: 'sess-1', ...overrides
  };
}

const caps = { actionsEnabled: true, canStart: true, canExtend: true, canLockUnlock: true };

function renderMenu(onSelect: (item: SeatMenuItem) => void, onClose: () => void) {
  const s = seat({});
  return render(
    <I18nProvider>
      <SeatContextMenu seat={s} sections={buildSeatMenu(s, caps)} x={20} y={20} onSelect={onSelect} onClose={onClose} />
    </I18nProvider>
  );
}

describe('SeatContextMenu', () => {
  it('renders an accessible menu and dispatches the picked item', () => {
    const onSelect = mock((_item: SeatMenuItem) => {});
    const { getByRole, getByText } = renderMenu(onSelect, () => {});
    expect(getByRole('menu')).not.toBeNull();
    fireEvent.click(getByText('15 мин'));
    expect(onSelect).toHaveBeenCalledTimes(1);
    expect(onSelect.mock.calls[0][0].id).toBe('extend-15');
  });

  it('closes on Escape so the keyboard user is never trapped', () => {
    const onClose = mock(() => {});
    const { getByRole } = renderMenu(() => {}, onClose);
    fireEvent.keyDown(getByRole('menu').querySelector('button')!, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('tags coming-soon items so they never read as live', () => {
    const { getAllByText } = renderMenu(() => {}, () => {});
    // The "скоро" tag marks deferred actions (reboot/shutdown/wake/fine/notify).
    expect(getAllByText('скоро').length).toBeGreaterThan(0);
  });
});
