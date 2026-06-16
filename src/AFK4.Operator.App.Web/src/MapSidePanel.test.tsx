import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, fireEvent, render } from '@testing-library/react';
import type { RenderResult } from '@testing-library/react';
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

// Статус ПК раскрывается под кнопкой «Статус» — пилюля блокировки появляется только тогда.
async function revealStatus(utils: RenderResult) {
  fireEvent.click(utils.getByRole('button', { name: /^Статус$/ }));
  await utils.findByText(/блокирован/);
}

describe('MapSidePanel diagnostics (A3)', () => {
  it('surfaces the real device specifics on hand once status is requested', async () => {
    const utils = renderPanel(seat({}));
    await revealStatus(utils);
    utils.getByText('Зал-1-ПК-07'); // device name, not a mashed string
    utils.getByText('Онлайн'); // connection
    utils.getByText('заблокирован'); // lock state
  });

  it('reveals software versions only when the seat needs troubleshooting', async () => {
    // Healthy active seat keeps the panel lean — no version noise even when status is open.
    const healthy = renderPanel(seat({}));
    await revealStatus(healthy);
    expect(healthy.queryByText('Агент 0.4 · Оболочка 0.4')).toBeNull();
    cleanup();
    // A seat in an attention state surfaces the versions for the operator diagnosing it.
    const attention = renderPanel(seat({ tone: 'offline', isDeviceOnline: false }));
    await revealStatus(attention);
    expect(attention.queryByText('Агент 0.4 · Оболочка 0.4')).not.toBeNull();
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

  it('flags a lost-connection PC with the danger status pill once status is open', async () => {
    const utils = renderPanel(seat({ isDeviceOnline: false }));
    await revealStatus(utils);
    const pill = utils.container.querySelector('.status-pill.bad');
    expect(pill).not.toBeNull();
    expect(pill?.textContent).toContain('Нет связи');
  });

  it('keeps the PC status hidden until the operator presses «Статус»', () => {
    const { queryByText } = renderPanel(seat({}));
    expect(queryByText('Онлайн')).toBeNull();
    expect(queryByText(/^(за|раз)блокирован$/)).toBeNull();
  });

  it('toggles the PC status closed on a second «Статус» press', async () => {
    const utils = renderPanel(seat({}));
    await revealStatus(utils);
    fireEvent.click(utils.getByRole('button', { name: /^Статус$/ }));
    expect(utils.queryByText('Онлайн')).toBeNull();
  });

  it('shows the real running total for an open tab in the host currency', () => {
    const { getByText } = renderPanel(seat({ remaining: '≈ 54 c.', remainingSeconds: null, accruedCostMinorUnits: 5400 }));
    getByText('Набежало');
  });

  it('offers no PC control or status for a seat without a device', () => {
    const { queryByText, queryByRole } = renderPanel(seat({ deviceId: null, deviceName: null }));
    expect(queryByText('Управление ПК')).toBeNull();
    expect(queryByRole('button', { name: /^Статус$/ })).toBeNull();
  });
});
