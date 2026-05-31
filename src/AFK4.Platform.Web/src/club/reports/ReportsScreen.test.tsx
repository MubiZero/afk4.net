import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { ReportsScreen } from './ReportsScreen';

function fakeClient() {
  return {
    getShiftReport: mock<() => Promise<object>>(async () => ({ rows: [], limit: 100 })),
    getSalesReport: mock<() => Promise<object>>(async () => ({
      rows: [], limit: 100,
      grossSalesTotal: { currencyCode: 'RUB', minorUnits: 0 },
      refundsTotal: { currencyCode: 'RUB', minorUnits: 0 },
      netSalesTotal: { currencyCode: 'RUB', minorUnits: 0 }
    })),
    getGameplayTimeReport: mock<() => Promise<object>>(async () => ({
      rows: [], limit: 100, totalDurationSeconds: 0, totalPackageSeconds: 0, totalBonusSeconds: 0,
      gameplayRevenueTotal: { currencyCode: 'RUB', minorUnits: 0 }
    })),
    getCashOperationReport: mock<() => Promise<object>>(async () => ({
      rows: [], limit: 100,
      cashInTotal: { currencyCode: 'RUB', minorUnits: 0 },
      cashOutTotal: { currencyCode: 'RUB', minorUnits: 0 },
      netCashTotal: { currencyCode: 'RUB', minorUnits: 0 }
    })),
    getOperatorActionReport: mock<() => Promise<object>>(async () => ({ rows: [], limit: 100, totalActionCount: 0 })),
    fetchReportCsv: mock<() => Promise<Blob>>(async () => new Blob())
  };
}

it('loads the default (shifts) tab', async () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <ReportsScreen client={client as never} branchId="b1" />
    </ToastProvider></I18nProvider>
  );
  await waitFor(() => expect(client.getShiftReport).toHaveBeenCalled());
});

it('switches to the sales tab and loads it', async () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <ReportsScreen client={client as never} branchId="b1" />
    </ToastProvider></I18nProvider>
  );
  const tab = screen.getByRole('tab', { name: 'Продажи' });
  fireEvent.mouseDown(tab);
  fireEvent.click(tab);
  await waitFor(() => expect(client.getSalesReport).toHaveBeenCalled());
});
