import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

const getShiftReport = mock(async () => ({
  rows: [{ state: 'Closed', openedAtUtc: '2026-07-01T08:00:00Z', closedAtUtc: '2026-07-01T20:00:00Z',
    cashMovementsTotal: { minorUnits: 5000, currencyCode: 'TJS' }, expectedCash: { minorUnits: 12000, currencyCode: 'TJS' },
    countedCash: { minorUnits: 12000, currencyCode: 'TJS' }, difference: { minorUnits: 0, currencyCode: 'TJS' } }]
}));
const getCashOperationReport = mock(async () => ({ cashInTotal: { minorUnits: 0, currencyCode: 'TJS' }, cashOutTotal: { minorUnits: 0, currencyCode: 'TJS' }, netCashTotal: { minorUnits: 0, currencyCode: 'TJS' }, rows: [] }));
const exportShiftReportCsv = mock(async () => 'state,opened\nClosed,2026-07-01');

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    shifts: { getShiftReport, getCashOperationReport, getGameplayTimeReport: async () => ({ rows: [] }), getOperatorActionReport: async () => ({ rows: [] }),
      exportShiftReportCsv, exportCashOperationReportCsv: async () => '', exportGameplayTimeReportCsv: async () => '', exportOperatorActionReportCsv: async () => '' }
  }),
  downloadTextFile: mock(() => {}),
  formatMinorUnits: (minor: number, code: string) => `${(minor / 100).toFixed(2)} ${code}`,
  readArray: (rec: Record<string, unknown>, key: string) => (Array.isArray(rec?.[key]) ? (rec[key] as unknown[]) : []),
  readRecord: (v: unknown) => (v && typeof v === 'object' ? (v as Record<string, unknown>) : {}),
  readString: (rec: Record<string, unknown>, key: string) => (typeof rec?.[key] === 'string' ? (rec[key] as string) : ''),
  readNumber: (rec: Record<string, unknown>, key: string) => (typeof rec?.[key] === 'number' ? (rec[key] as number) : 0),
  readMoney: (rec: Record<string, unknown>, key: string) => (rec?.[key] && typeof rec[key] === 'object' ? (rec[key] as { minorUnits: number; currencyCode: string }) : null)
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('HistoryDestination', () => {
  it('loads the shifts report by default and renders a row', async () => {
    const { HistoryDestination } = await import('./HistoryDestination');
    render(<I18nProvider initialLocale="ru"><HistoryDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(getShiftReport).toHaveBeenCalled());
    expect(await screen.findByText('50.00 TJS')).toBeInTheDocument();
  });

  it('switches to the cash tab and loads that report', async () => {
    const { HistoryDestination } = await import('./HistoryDestination');
    render(<I18nProvider initialLocale="ru"><HistoryDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(getShiftReport).toHaveBeenCalled());
    fireEvent.click(screen.getByText('Кассовые операции'));
    await waitFor(() => expect(getCashOperationReport).toHaveBeenCalled());
  });
});
