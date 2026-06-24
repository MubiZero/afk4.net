import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashShiftCommandBar, type CashShiftActionsClient } from './CashShiftCommandBar';
import type { OperatorAuthSession } from '../authClient';
import type { ShiftRevenueDto } from '../operatorApiClients';

afterEach(cleanup);

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't', organizationId: 'org1' }, branchId: 'b1' } as never;
const allPerms = ['shifts.open', 'shifts.close', 'shifts.cash.manage'];
const session = (perms: string[]) => ({ permissions: perms, organizationId: 'org1' } as unknown as OperatorAuthSession);

function fakeActions(): CashShiftActionsClient & { calls: Record<string, unknown[]> } {
  const calls: Record<string, unknown[]> = { open: [], movement: [], close: [] };
  return {
    calls,
    openShift: mock(async (branchId: string, request: unknown) => { calls.open.push({ branchId, request }); return {}; }),
    recordCashMovement: mock(async (shiftId: string, request: unknown) => { calls.movement.push({ shiftId, request }); return {}; }),
    closeShift: mock(async (shiftId: string, request: unknown) => { calls.close.push({ shiftId, request }); return {}; })
  };
}

function renderBar(opts: { isOpen: boolean; perms?: string[]; actions?: CashShiftActionsClient; onShiftChanged?: () => void; revenue?: ShiftRevenueDto | null }) {
  render(
    <I18nProvider initialLocale="ru">
      <CashShiftCommandBar
        backend={backend}
        session={session(opts.perms ?? allPerms)}
        shiftId={opts.isOpen ? 's1' : null}
        isOpen={opts.isOpen}
        expectedCash={{ currencyCode: 'TJS', minorUnits: 11500 }}
        currencyCode="TJS"
        revenue={opts.revenue ?? null}
        onShiftChanged={opts.onShiftChanged ?? (() => {})}
        actions={opts.actions}
      />
    </I18nProvider>
  );
}

describe('CashShiftCommandBar', () => {
  it('закрытая смена → только кнопка «Открыть смену»', () => {
    renderBar({ isOpen: false });
    expect(screen.getByRole('button', { name: 'Открыть смену' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Внести' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Закрыть смену' })).not.toBeInTheDocument();
  });

  it('открытая смена → Внести/Изъять/Закрыть, без «Открыть»', () => {
    renderBar({ isOpen: true });
    expect(screen.getByRole('button', { name: 'Внести' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Изъять' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Закрыть смену' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Открыть смену' })).not.toBeInTheDocument();
  });

  it('права гейтят кнопки: без shifts.open закрытая смена не даёт «Открыть»', () => {
    renderBar({ isOpen: false, perms: [] });
    expect(screen.queryByRole('button', { name: 'Открыть смену' })).not.toBeInTheDocument();
  });

  it('открытие смены: модалка → submit → openShift с payload + onShiftChanged', async () => {
    const actions = fakeActions();
    const onShiftChanged = mock(() => {});
    renderBar({ isOpen: false, actions, onShiftChanged });
    fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));
    // в модалке поля предзаполнены; меняем старт наличных
    fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '150.00' } });
    // После открытия модалки есть ДВЕ кнопки «Открыть смену» — ищем submit внутри диалога
    const dialog = screen.getByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Открыть смену' }));
    await waitFor(() => expect(actions.calls.open.length).toBe(1));
    const { branchId, request } = actions.calls.open[0] as { branchId: string; request: Record<string, unknown> };
    expect(branchId).toBe('b1');
    expect(request).toMatchObject({ organizationId: 'org1', startingCash: { currencyCode: 'TJS', minorUnits: 15000 } });
    expect(String(request.idempotencyKey)).toMatch(/^shift-open-/);
    await waitFor(() => expect(onShiftChanged).toHaveBeenCalledTimes(1));
  });

  it('закрытие смены: модалка → submit → closeShift с countedCash', async () => {
    const actions = fakeActions();
    renderBar({ isOpen: true, actions });
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть смену' }));
    fireEvent.change(screen.getByLabelText('Факт в кассе'), { target: { value: '115.00' } });
    // После открытия модалки есть ДВЕ кнопки «Закрыть смену» — ищем submit внутри диалога
    const dialog = screen.getByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Закрыть смену' }));
    await waitFor(() => expect(actions.calls.close.length).toBe(1));
    const { shiftId, request } = actions.calls.close[0] as { shiftId: string; request: Record<string, unknown> };
    expect(shiftId).toBe('s1');
    expect(request).toMatchObject({ countedCash: { currencyCode: 'TJS', minorUnits: 11500 } });
  });

  const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
  const makeRevenue = () => ({
    shiftId: 's1', organizationId: 'o', branchId: 'b1', openedByStaffUserId: 'u1', closedByStaffUserId: null,
    state: 'open',
    earned: { time: m(82000), goods: m(41000), total: m(123000) },
    inflow: { cash: m(90000), nonCash: m(33000), walletTopUps: m(15000), directTotal: m(123000) },
    cash: { starting: m(100000), expected: m(190000), counted: null, difference: null },
    openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null
  } as never);

  it('кнопка X-отчёт открывает форму отчёта по снимку выручки', () => {
    renderBar({ isOpen: true, perms: [...allPerms, 'reports.view'], revenue: makeRevenue() });
    fireEvent.click(screen.getByRole('button', { name: /X-отчёт/ }));
    const dialog = screen.getByRole('dialog');
    expect(within(dialog).getByText('X-отчёт')).toBeInTheDocument();
    expect(within(dialog).getByText('Выручка смены')).toBeInTheDocument();
  });

  it('успешное закрытие показывает Z-отчёт', async () => {
    const revenue = makeRevenue();
    const actions: CashShiftActionsClient = {
      openShift: mock(async () => ({})),
      recordCashMovement: mock(async () => ({})),
      closeShift: mock(async () => ({ countedCash: m(185000), difference: m(-5000), closedAtUtc: '2026-06-24T18:00:00Z' }))
    };
    renderBar({ isOpen: true, revenue, actions });
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть смену' }));
    const dialog = screen.getByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('Факт в кассе'), { target: { value: '1850.00' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Закрыть смену' }));
    expect(await screen.findByText('Z-отчёт')).toBeInTheDocument();
  });
});
