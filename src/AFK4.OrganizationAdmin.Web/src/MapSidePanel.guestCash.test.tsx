import { afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import type { OperatorBackendContext, SeatActionRequest } from './operatorTypes';
import { MapSidePanel } from './MapSidePanel';

afterEach(cleanup);

// Решение владельца 30.09.2026: гость платит наличными у стойки. Здесь — то, что видит кассир у
// карточки места: продление как новая оплата с названной суммой и окно расчёта, которое говорит
// правду про предоплаченную сессию (приёмка 30.09.2026, P12/P17).

const SESSION_ID = 'AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE';

function seat(overrides: Partial<SeatSummary>): SeatSummary {
  return {
    id: 'seat-1', zone: 'Зал A', name: 'PC-07', tone: 'active', stateLabel: 'В сессии',
    player: 'Гость', remaining: '30 мин', remainingSeconds: 1800, device: 'PC-07 · Online', command: 'Lease fresh',
    app: 'Agent 0.4 · Shell 0.4', deviceId: 'dev-1', deviceName: 'PC-07', isDeviceOnline: true, isDeviceLocked: false,
    activeSessionId: SESSION_ID, hasActiveSession: true, sessionVersion: 4, tariffName: 'Standard',
    sessionStartedAtUtc: '2026-09-30T10:00:00Z', ...overrides
  };
}

function backend(): OperatorBackendContext {
  return {
    config: { runtime: 'browser-test', shellMode: 'test', platformBaseUrl: 'http://localhost:5074/', currencyCode: 'TJS' },
    branchId: 'branch-1',
    session: {
      staffUserId: 'staff-1', organizationId: 'org-1', displayName: 'Operator', accessToken: 'test-token',
      accessTokenExpiresAtUtc: '2999-01-01T00:00:00Z', refreshToken: 'refresh-token',
      refreshTokenExpiresAtUtc: '2026-07-16T00:00:00Z', branchIds: ['branch-1'], activeBranchId: 'branch-1',
      permissions: ['organization.sessions.extend', 'organization.sessions.end', 'organization.sessions.start']
    }
  };
}

function json(value: unknown, status = 200): Response {
  return new Response(JSON.stringify(value), { status, headers: { 'Content-Type': 'application/json' } });
}

const money = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

function renderPanel(s: SeatSummary, onSeatAction: (request: SeatActionRequest) => Promise<Record<string, never>>) {
  render(
    <I18nProvider>
      <MapSidePanel
        seat={s} seats={[s]} currencyCode="TJS" backend={backend()} actionsEnabled canUsePcControl
        onSeatAction={onSeatAction} onPcControlAction={async () => ({ detail: '' })}
      />
    </I18nProvider>
  );
}

const originalFetch = globalThis.fetch;
let requests: string[] = [];
function serve(handler: (pathname: string, search: string) => Response | null) {
  requests = [];
  globalThis.fetch = mock(async (input: RequestInfo | URL) => {
    const url = new URL(String(input));
    requests.push(url.pathname + url.search);
    const response = handler(url.pathname, url.search);
    if (response === null) throw new Error(`Unexpected request: ${url.pathname}`);
    return response;
  }) as unknown as typeof fetch;
}
beforeEach(() => { requests = []; });
afterEach(() => { globalThis.fetch = originalFetch; });

describe('«+15 / +30» у гостя, заплатившего наличными', () => {
  it('называет сумму по тарифу сессии и продлевает только после «Принять»', async () => {
    serve((pathname) => pathname.endsWith('/extend/quote') ? json({ sessionId: SESSION_ID, additionalMinutes: 15, charge: money(750) }) : null);
    const onSeatAction = mock(async (_request: SeatActionRequest) => ({}));
    renderPanel(seat({ sessionBillingMode: 'prepaid_cash' }), onSeatAction);

    fireEvent.click(screen.getByRole('button', { name: /15 мин/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Продлить сессию гостя' });
    // Сумма пришла с сервера — не прикидка в браузере.
    expect(await within(dialog).findByText(/Принять наличными: 7,5/)).toBeInTheDocument();
    expect(requests.some((path) => path.includes(`/sessions/${SESSION_ID}/extend/quote?additionalMinutes=15`))).toBe(true);
    expect(onSeatAction).not.toHaveBeenCalled();

    fireEvent.click(within(dialog).getByRole('button', { name: /^Принять 7,5/ }));
    await waitFor(() => expect(onSeatAction).toHaveBeenCalledTimes(1));
    expect(onSeatAction.mock.calls[0][0]).toMatchObject({ type: 'extend', minutes: 15, expectedChargeMinorUnits: 750 });
  });

  it('без открытой смены говорит причину сразу и не даёт принять деньги', async () => {
    serve(() => json({ error: 'open_shift_required', code: 'open_shift_required' }, 400));
    const onSeatAction = mock(async (_request: SeatActionRequest) => ({}));
    renderPanel(seat({ sessionBillingMode: 'prepaid_cash' }), onSeatAction);

    fireEvent.click(screen.getByRole('button', { name: /30 мин/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Продлить сессию гостя' });

    expect(await within(dialog).findByText('Чтобы принять оплату, сначала откройте смену.')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Завершить' })).toBeDisabled();
    expect(onSeatAction).not.toHaveBeenCalled();
  });

  it('у сессии без наличной оплаты продлевает сразу, как раньше', async () => {
    serve(() => null);
    const onSeatAction = mock(async (_request: SeatActionRequest) => ({}));
    renderPanel(seat({ sessionBillingMode: 'prepaid_wallet', playerDisplayName: 'Мадина' }), onSeatAction);

    fireEvent.click(screen.getByRole('button', { name: /15 мин/ }));

    await waitFor(() => expect(onSeatAction).toHaveBeenCalledTimes(1));
    expect(onSeatAction.mock.calls[0][0]).toMatchObject({ type: 'extend', minutes: 15 });
    expect(requests).toEqual([]);
  });
});

describe('окно «Завершить и рассчитать»', () => {
  const quote = (over: Record<string, unknown>) => ({
    sessionId: SESSION_ID, timeCharge: money(0), posTotal: money(0), grandTotal: money(0), billableSeconds: 0,
    playerAccountId: null, walletBalance: null, playedSeconds: 1200, prepaidCharged: null, prepaidRefund: null, ...over
  });

  it('у гостя, заплатившего вперёд, называет его «Гость» и говорит, что сыграно, заплачено и что возврата нет', async () => {
    serve((pathname) => pathname.endsWith('/checkout/quote') ? json(quote({ prepaidCharged: money(3000) })) : null);
    renderPanel(seat({ sessionBillingMode: 'prepaid_cash', player: 'Активный клиент' }), async () => ({}));

    fireEvent.click(screen.getByRole('button', { name: 'Завершить и рассчитать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Завершить и принять оплату' });

    expect(await within(dialog).findByText(/Сыграно 20м/)).toBeInTheDocument();
    expect(within(dialog).getByText(/Гость заплатил вперёд наличными: 30/)).toBeInTheDocument();
    expect(within(dialog).getByText('Возврата нет: гость платил вперёд наличными')).toBeInTheDocument();
    // Предоплаченное время — не строка счёта: «Время · 0м, 0 с.» выглядело, будто время не считалось.
    expect(within(dialog).queryByText(/^Время ·/)).toBeNull();
    expect(dialog.textContent).toContain('PC-07 · Гость');
    expect(dialog.textContent).not.toContain('Активный клиент');
  });

  it('у клиента с балансом называет его по имени, сколько списано вперёд и что вернётся на баланс', async () => {
    serve((pathname) => pathname.endsWith('/checkout/quote')
      ? json(quote({ playerAccountId: 'player-1', walletBalance: money(1000), prepaidCharged: money(6000), prepaidRefund: money(3750), playedSeconds: 2400 }))
      : null);
    renderPanel(seat({ sessionBillingMode: 'prepaid_wallet', playerDisplayName: 'Мадина С.' }), async () => ({}));

    fireEvent.click(screen.getByRole('button', { name: 'Завершить и рассчитать' }));
    const dialog = await screen.findByRole('dialog', { name: 'Завершить и принять оплату' });

    expect(await within(dialog).findByText(/Сыграно 40м/)).toBeInTheDocument();
    expect(within(dialog).getByText(/С баланса списано вперёд: 60/)).toBeInTheDocument();
    expect(within(dialog).getByText(/Вернётся на баланс: 37,5/)).toBeInTheDocument();
    expect(dialog.textContent).toContain('PC-07 · Мадина С.');
  });

  it('у открытого счёта оставляет строку «Время» с суммой и сколько сыграно', async () => {
    serve((pathname) => pathname.endsWith('/checkout/quote')
      ? json(quote({ timeCharge: money(1500), grandTotal: money(1500), billableSeconds: 1800, playedSeconds: 1500 }))
      : null);
    renderPanel(seat({ remainingSeconds: null, accruedCostMinorUnits: 1500 }), async () => ({}));

    fireEvent.click(screen.getByRole('button', { name: /Завершить и принять|Завершить и рассчитать/ }));
    const dialog = await screen.findByRole('dialog', { name: 'Завершить и принять оплату' });

    expect(await within(dialog).findByText(/Сыграно 25м/)).toBeInTheDocument();
    expect(within(dialog).getByText(/^Время · 30м/)).toBeInTheDocument();
  });
});
