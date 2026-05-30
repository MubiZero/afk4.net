import { render, screen } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { ReportTab } from './ReportTab';
import type { ReportView, ReportFormatters } from './reportsModel';

function build(_data: { ok: boolean }, _fmt: ReportFormatters): ReportView {
  return {
    summaryCards: [{ labelKey: 'reports.sum.net', value: '10.00 RUB' }],
    columns: [{ key: 'state', labelKey: 'reports.col.state' }],
    rows: [{ state: 'Paid' }]
  };
}

it('renders summary cards and table rows from the built view', async () => {
  const load = vi.fn<() => Promise<{ ok: boolean }>>(async () => ({ ok: true }));
  render(
    <I18nProvider><ToastProvider>
      <ReportTab load={load} build={build} deps={['k']} onExport={async () => new Blob()} filename="x.csv" />
    </ToastProvider></I18nProvider>
  );
  expect(await screen.findByText('Paid')).toBeInTheDocument();
  expect(screen.getByText('10.00 RUB')).toBeInTheDocument();
});

it('shows the empty state when there are no rows', async () => {
  const load = vi.fn<() => Promise<{ ok: boolean }>>(async () => ({ ok: true }));
  const emptyBuild = (): ReportView => ({ summaryCards: [], columns: [{ key: 'state', labelKey: 'reports.col.state' }], rows: [] });
  render(
    <I18nProvider><ToastProvider>
      <ReportTab load={load} build={emptyBuild} deps={['k']} onExport={async () => new Blob()} filename="x.csv" />
    </ToastProvider></I18nProvider>
  );
  expect(await screen.findByText('Нет данных за выбранный период.')).toBeInTheDocument();
});
