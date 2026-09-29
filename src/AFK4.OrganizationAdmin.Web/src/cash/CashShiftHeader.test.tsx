// src/cash/CashShiftHeader.test.tsx
import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashShiftHeader } from './CashShiftHeader';
import type { ShiftRevenueDto } from '../operatorApiClients';
import type { CashShiftActionsClient } from './CashShiftCommandBar';
import { ToastProvider } from '../operatorToast';
import { shiftDto } from './cashFixtures';

afterEach(cleanup);

function m(minorUnits: number) {
  return { currencyCode: 'TJS', minorUnits };
}

function openShift(): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(1000), goods: m(500), noShow: m(0), total: m(1500) },
    inflow: { cash: m(0), nonCash: m(0), walletTopUps: m(0), directTotal: m(0) },
    cash: { starting: m(10000), expected: m(11500), counted: null, difference: null },
    openedAtUtc: '2026-06-24T08:00:00Z', closedAtUtc: null
  };
}

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't' }, branchId: 'b1' } as never;

function renderHeader(current: ShiftRevenueDto | null) {
  return render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <CashShiftHeader backend={backend} currencyCode="TJS" client={{ current: async () => current }} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('CashShiftHeader', () => {
  // Открытую смену говорят деньги в ней: отдельная строка «Смена открыта» рядом с «Касса»
  // повторяла вкладку «Смена» и корзину — три раза одно и то же.
  it('открытая смена → «Касса» с суммой в кассе и выручкой, без строки «Смена открыта»', async () => {
    renderHeader(openShift());
    await waitFor(() => expect(screen.getByText('В кассе')).toBeInTheDocument());
    expect(screen.getByRole('heading', { level: 1, name: 'Касса' })).toBeInTheDocument();
    expect(screen.getByText('115 с.')).toBeInTheDocument();
    expect(screen.getByText('Выручка')).toBeInTheDocument();
    expect(screen.queryByText('Смена открыта')).toBeNull();
  });

  it('нет смены → счётчик «Смена · не открыта», без денег', async () => {
    renderHeader(null);
    await waitFor(() => expect(screen.getByText('не открыта')).toBeInTheDocument());
    expect(screen.getByText('Смена')).toBeInTheDocument();
    expect(screen.queryByText('В кассе')).not.toBeInTheDocument();
  });

  // До ответа шапка не знает, открыта ли смена, и не утверждает ни того, ни другого: раньше она
  // успевала мигнуть «не открыта» и кнопкой «Открыть смену» у кассира с идущей сменой.
  it('до ответа — без счётчиков и команд смены', () => {
    render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <CashShiftHeader backend={backend} currencyCode="TJS" client={{ current: () => new Promise(() => {}) }} />
        </ToastProvider>
      </I18nProvider>
    );
    expect(screen.getByRole('heading', { level: 1, name: 'Касса' })).toBeInTheDocument();
    expect(screen.queryByText('не открыта')).toBeNull();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('открытая смена + право shifts.close → кнопка «Закрыть смену» в шапке', async () => {
    const session = { permissions: ['organization.shifts.close'], organizationId: 'o' } as never;
    const actions: CashShiftActionsClient = {
      openShift: async () => ({}),
      recordCashMovement: async () => ({}),
      closeShift: async () => shiftDto({ state: 'closed' })
    };
    render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <CashShiftHeader
            backend={backend}
            currencyCode="TJS"
            session={session}
            client={{ current: async () => openShift() }}
            actions={actions}
          />
        </ToastProvider>
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByRole('button', { name: 'Закрыть смену' })).toBeInTheDocument());
  });

  // Узкое право «закрыть свою смену»: шапка знает, кто открыл смену, и показывает кнопку тому,
  // кто её открыл. Раньше этот человек находил «Закрыть смену» только на вкладке «Смена».
  it('кассир с правом закрыть свою смену видит кнопку в шапке', async () => {
    const session = { permissions: ['organization.shifts.close_own'], organizationId: 'o', staffUserId: 'u1' } as never;
    render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <CashShiftHeader backend={backend} currencyCode="TJS" session={session} client={{ current: async () => openShift() }} />
        </ToastProvider>
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByRole('button', { name: 'Закрыть смену' })).toBeInTheDocument());
  });
});
