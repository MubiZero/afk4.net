import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import { MapSidePanel } from './MapSidePanel';

afterEach(cleanup);

function seat(overrides: Partial<SeatSummary>): SeatSummary {
  return {
    id: 'seat-1', zone: 'Зал A', name: 'PC-07', tone: 'active', stateLabel: 'В сессии',
    player: 'Активный клиент', remaining: '30 мин', billing: 'Wallet', device: 'PC-07 · Online · locked · Agent 0.4 · Shell 0.4',
    command: 'Lease fresh', app: 'Agent 0.4 · Shell 0.4', deviceId: 'dev-1', deviceName: 'Зал-1-ПК-07',
    isDeviceOnline: true, isDeviceLocked: true, activeSessionId: 'AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE', ...overrides
  };
}

function renderPanel(s: SeatSummary) {
  return render(
    <I18nProvider>
      <MapSidePanel seat={s} seats={[s]} currencyCode="TJS" backend={null} actionsEnabled={false} onSeatAction={async () => ({})} />
    </I18nProvider>
  );
}

describe('MapSidePanel diagnostics (A3)', () => {
  it('surfaces the real device specifics on hand as distinct fields', () => {
    const { getByText } = renderPanel(seat({}));
    getByText('Зал-1-ПК-07'); // device name, not a mashed string
    getByText('Онлайн'); // connection
    getByText('заблокирован'); // lock state
  });

  it('reveals software versions only when the seat needs troubleshooting', () => {
    // Healthy active seat keeps the panel lean — no version noise.
    const { queryByText, rerender } = renderPanel(seat({}));
    expect(queryByText('Агент 0.4 · Оболочка 0.4')).toBeNull();
    // A seat in an attention state surfaces the versions for the operator diagnosing it.
    rerender(
      <I18nProvider>
        <MapSidePanel seat={seat({ tone: 'offline', isDeviceOnline: false })} seats={[]} currencyCode="TJS" backend={null} actionsEnabled={false} onSeatAction={async () => ({})} />
      </I18nProvider>
    );
    queryByText('Агент 0.4 · Оболочка 0.4');
    expect(queryByText('Агент 0.4 · Оболочка 0.4')).not.toBeNull();
  });

  it('does not present a fabricated session billing mode as if it were real', () => {
    const { queryByText } = renderPanel(seat({}));
    // The old hardcoded "Биллинг: Депозит" row is gone; real billing/tariff arrives with B1.
    expect(queryByText('Биллинг')).toBeNull();
  });

  it('never leaks the raw session GUID onto the operator path', () => {
    const { container } = renderPanel(seat({}));
    expect(container.textContent).not.toContain('AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE');
  });

  it('flags a lost-connection PC with the danger status pill', () => {
    const { container } = renderPanel(seat({ isDeviceOnline: false }));
    const pill = container.querySelector('.status-pill.bad');
    expect(pill).not.toBeNull();
    expect(pill?.textContent).toContain('Нет связи');
  });

  it('shows the real running total for an open tab in the host currency', () => {
    const { getByText } = renderPanel(seat({ remaining: '≈ 54 c.', remainingSeconds: null, accruedCostMinorUnits: 5400 }));
    getByText('Набежало');
  });

  it('shows a plain "unassigned" line and no PC fields when there is no device', () => {
    const { getByText, queryByText } = renderPanel(seat({ deviceId: null, deviceName: null }));
    getByText('Устройство не назначено');
    expect(queryByText('Связь')).toBeNull();
    expect(queryByText('Версии ПО')).toBeNull();
  });
});
