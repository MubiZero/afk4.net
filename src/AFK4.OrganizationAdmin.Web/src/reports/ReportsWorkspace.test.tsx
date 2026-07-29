import { afterEach, beforeEach, describe, expect, it } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ReportsWorkspace } from './ReportsWorkspace';

const originalFetch = globalThis.fetch;

beforeEach(() => {
  globalThis.fetch = (async (input: RequestInfo | URL) => {
    const url = input instanceof Request ? input.url : String(input);
    const money = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });
    const period = { fromDate: '2026-07-29', toDate: '2026-07-29', timeZone: 'Asia/Dushanbe', fromUtc: '2026-07-28T19:00:00Z', toUtc: '2026-07-29T19:00:00Z' };
    const shift = { shiftId: 'shift-closed', state: 'closed', openedByStaffUserId: 'staff-1', expectedCash: money(10000), countedCash: money(9500), difference: money(-500), cashMovementsTotal: money(0), posCashPaymentsTotal: money(0), posRefundsTotal: money(0), billingCashImpactTotal: money(0), openedAtUtc: '2026-07-29T05:00:00Z', closedAtUtc: '2026-07-29T10:00:00Z' };
    const body = url.includes('/workspace/summary')
      ? { period, attentionTotalCount: 1, attentionItems: [{ kind: 'shift_discrepancy', title: 'Смена', detail: 'Расхождение', targetId: 'shift-closed', amount: money(-500) }], figures: { netRevenue: money(10000), gameplayRevenue: money(6000), posNetSales: money(4000), gameplaySeconds: 7200 }, trend: [{ date: '2026-07-29', netRevenue: money(10000) }], activeShift: { shiftId: 'shift-open', openedByStaffUserId: 'staff-1', openedAtUtc: '2026-07-29T05:00:00Z', expectedCash: money(10000), isProvisional: true } }
      : url.includes('/workspace/shifts-cash')
        ? { period, shifts: [shift], cashOperations: [], cashInTotal: money(0), cashOutTotal: money(0), netCashTotal: money(0) }
        : { period, grossRevenue: money(10500), refunds: money(-500), netRevenue: money(10000), gameplayRevenue: money(6000), gameplaySeconds: 7200, posNetSales: money(4000), comparison: { previousNetRevenue: money(8000), differenceMinorUnits: 2000, changePercent: 25 }, sources: [{ source: 'gameplay', revenue: money(6000) }, { source: 'pos', revenue: money(4000) }], paymentMethods: [{ key: 'cash', label: 'Наличные', revenue: money(4000) }], operators: [{ key: 'staff-1', label: 'Owner', revenue: money(10000) }] };
    return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } });
  }) as typeof fetch;
});

afterEach(() => { cleanup(); globalThis.fetch = originalFetch; });

function backendWith(permissions: string[]) {
  return {
    config: { platformBaseUrl: 'https://platform.test', currencyCode: 'TJS' },
    session: {
      staffUserId: 'staff-1', organizationId: 'org-1', displayName: 'Owner',
      permissions, accessToken: 'token', accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z',
      refreshToken: 'refresh', refreshTokenExpiresAtUtc: '2099-02-01T00:00:00Z', branchIds: ['branch-1']
    },
    branchId: 'branch-1'
  } as never;
}

describe('ReportsWorkspace', () => {
  it('shows exactly the approved owner report tabs with Summary selected', async () => {
    render(<I18nProvider initialLocale="ru"><ReportsWorkspace backend={backendWith(['organization.reports.view'])} currencyCode="TJS" onNavigate={() => {}} onOpenSeat={() => {}} /></I18nProvider>);

    const tabs = screen.getAllByRole('tab');
    expect(tabs.map((tab) => tab.textContent)).toEqual(['Сводка', 'Смены и касса', 'Выручка']);
    expect(tabs[0]).toHaveAttribute('aria-selected', 'true');
    expect(screen.queryByText('Журнал')).not.toBeInTheDocument();
    expect(screen.queryByText('Действия операторов')).not.toBeInTheDocument();
    expect(await screen.findByText('Требуют проверки: 1')).toBeInTheDocument();
    expect(screen.getAllByText('100 с.')).toHaveLength(2);
    expect(screen.getByText('7 дней')).toBeInTheDocument();
  });

  it('switches between the three report panels', async () => {
    render(<I18nProvider initialLocale="ru"><ReportsWorkspace backend={backendWith(['organization.reports.view'])} currencyCode="TJS" onNavigate={() => {}} onOpenSeat={() => {}} /></I18nProvider>);

    fireEvent.click(screen.getByRole('tab', { name: 'Смены и касса' }));
    expect(screen.getByRole('tabpanel')).toHaveAttribute('aria-labelledby', 'reports-tab-shiftsCash');
    fireEvent.click(screen.getByRole('tab', { name: 'Выручка' }));
    expect(screen.getByRole('tabpanel')).toHaveAttribute('aria-labelledby', 'reports-tab-revenue');
    expect(await screen.findByText('Игровые часы')).toBeInTheDocument();
    expect(screen.getByText('+25%')).toBeInTheDocument();
  });

  it('does not grant Reports from audit permission', () => {
    render(<I18nProvider initialLocale="ru"><ReportsWorkspace backend={backendWith(['organization.audit.view'])} currencyCode="TJS" onNavigate={() => {}} onOpenSeat={() => {}} /></I18nProvider>);
    expect(screen.queryByRole('tab')).not.toBeInTheDocument();
    expect(screen.getByText('Нет доступа к отчётам')).toBeInTheDocument();
  });
});
