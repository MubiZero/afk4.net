import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ShiftsWorkspace } from './ShiftsWorkspace';
import type { ShiftRevenueDto } from './operatorApiClients';

function money(minorUnits: number) {
  return { currencyCode: 'TJS', minorUnits };
}

function shift(overrides: Partial<ShiftRevenueDto> = {}): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o1', branchId: 'b1',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: money(310000), goods: money(115000), total: money(425000) },
    inflow: { cash: money(200000), nonCash: money(180000), walletTopUps: money(90000), directTotal: money(380000) },
    cash: { starting: money(1000000), expected: money(1380000), counted: null, difference: null },
    openedAtUtc: '2026-06-10T09:00:00Z', closedAtUtc: null,
    ...overrides
  };
}

function client(current: ShiftRevenueDto | null, history: ShiftRevenueDto[] = []) {
  return {
    current: async () => current,
    history: async () => ({ shifts: history, limit: 20 })
  };
}

describe('ShiftsWorkspace', () => {
  it('renders earned and inflow breakdown for the current shift', async () => {
    render(
      <I18nProvider>
        <ShiftsWorkspace backend={null} branchId="b1" client={client(shift()) as never} />
      </I18nProvider>
    );

    await waitFor(() => screen.getByText(/4\s?250/)); // earned total 425000 minor → 4 250
    expect(screen.getByText(/3\s?100/)).toBeTruthy(); // earned time
    expect(screen.getByText(/1\s?150/)).toBeTruthy(); // earned goods
  });

  it('shows an empty state when no shift is open', async () => {
    render(
      <I18nProvider>
        <ShiftsWorkspace backend={null} branchId="b1" client={client(null) as never} />
      </I18nProvider>
    );

    await waitFor(() => screen.getByText(/нет открытых смен/i));
  });

  it('surfaces an error instead of masking a failed load as empty', async () => {
    const failing = {
      current: async () => { throw new Error('network'); },
      history: async () => ({ shifts: [], limit: 20 })
    };
    render(
      <I18nProvider>
        <ShiftsWorkspace backend={null} branchId="b1" client={failing as never} />
      </I18nProvider>
    );

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.queryByText(/нет открытых смен/i)).toBeNull();
  });
});
