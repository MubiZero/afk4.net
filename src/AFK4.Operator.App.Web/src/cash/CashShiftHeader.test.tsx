// src/cash/CashShiftHeader.test.tsx
import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashShiftHeader } from './CashShiftHeader';
import type { ShiftRevenueDto } from '../operatorApiClients';
import type { CashShiftActionsClient } from './CashShiftCommandBar';
import { ToastProvider } from '../operatorToast';

afterEach(cleanup);

function m(minorUnits: number) {
  return { currencyCode: 'TJS', minorUnits };
}

function openShift(): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(1000), goods: m(500), total: m(1500) },
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
  it('открытая смена → статус «Смена открыта» + метрики кассы/выручки', async () => {
    renderHeader(openShift());
    await waitFor(() => expect(screen.getByText('Смена открыта')).toBeInTheDocument());
    expect(screen.getByText('В кассе')).toBeInTheDocument();
    expect(screen.getByText('Выручка')).toBeInTheDocument();
  });

  it('нет смены → статус «Смена не открыта», без метрик', async () => {
    renderHeader(null);
    await waitFor(() => expect(screen.getByText('Смена не открыта')).toBeInTheDocument());
    expect(screen.queryByText('В кассе')).not.toBeInTheDocument();
  });

  it('открытая смена + право shifts.close → кнопка «Закрыть смену» в шапке', async () => {
    const session = { permissions: ['shifts.close'], organizationId: 'o' } as never;
    const actions: CashShiftActionsClient = {
      openShift: async () => ({}),
      recordCashMovement: async () => ({}),
      closeShift: async () => ({})
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
    await waitFor(() => expect(screen.getByText('Смена открыта')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Закрыть смену' })).toBeInTheDocument();
  });
});
