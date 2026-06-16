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
      <MapSidePanel seat={s} seats={[s]} currencyCode="TJS" backend={null} actionsEnabled={false} canUsePcControl={true} onSeatAction={async () => ({})} onPcControlAction={async () => ({ detail: '' })} />
    </I18nProvider>
  );
}

describe('MapSidePanel diagnostics (A3)', () => {
  it('surfaces the real device specifics on hand in the always-on status block', () => {
    const utils = renderPanel(seat({}));
    utils.getByText('Зал-1-ПК-07'); // device name, not a mashed string
    utils.getByText('Онлайн'); // connection
    utils.getByText('заблокирован'); // lock state
  });

  it('always shows software versions, online or offline', () => {
    // Версию ПО оператор должен видеть всегда — и для здоровой сессии, и для офлайн-ПК.
    const healthy = renderPanel(seat({}));
    expect(healthy.queryByText('Агент 0.4 · Оболочка 0.4')).not.toBeNull();
    cleanup();
    const offline = renderPanel(seat({ tone: 'offline', isDeviceOnline: false }));
    expect(offline.queryByText('Агент 0.4 · Оболочка 0.4')).not.toBeNull();
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
    const utils = renderPanel(seat({ isDeviceOnline: false }));
    const pill = utils.container.querySelector('.status-pill.bad');
    expect(pill).not.toBeNull();
    expect(pill?.textContent).toContain('Нет связи');
  });

  it('shows the unified status block under the controls, without a «Статус» button', () => {
    const utils = renderPanel(seat({}));
    // Статус — единый блок внизу, не действие: отдельной кнопки «Статус» больше нет.
    expect(utils.queryByRole('button', { name: /^Статус$/ })).toBeNull();
    utils.getByText('Статус ПК');
  });

  it('shows the real running total for an open tab in the host currency', () => {
    const { getByText } = renderPanel(seat({ remaining: '≈ 54 c.', remainingSeconds: null, accruedCostMinorUnits: 5400 }));
    getByText('Набежало');
  });

  it('offers no PC control or status block for a seat without a device', () => {
    const { queryByText } = renderPanel(seat({ deviceId: null, deviceName: null }));
    expect(queryByText('Управление ПК')).toBeNull();
    expect(queryByText('Статус ПК')).toBeNull();
  });
});
