import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { AdImpressionRowDto } from '@/api/types';
import type { AdReportQuery } from '@/api/platformClients/ads';
import { ReportTab, type ReportClient } from './ReportTab';

const row: AdImpressionRowDto = {
  day: '2026-09-01',
  campaignId: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  campaignName: 'Campaign',
  creativeId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
  creativeTitle: 'Creative',
  organizationId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
  organizationName: 'Club',
  branchId: 'dddddddd-dddd-dddd-dddd-dddddddddddd',
  branchName: 'Branch',
  city: 'Dushanbe',
  impressions: 10,
  shownSeconds: 300
};

// Фильтры применяются на onChange, и повторный выбор той же даты не меняет её значение — событие
// не срабатывает, и «те же условия → report.retry()» внутри apply() было мёртвым кодом. Кнопка
// «Обновить» — единственный способ перечитать те же дни, пока свежие показы не пришли с ПК.
describe('ReportTab refresh', () => {
  it('«Обновить» перечитывает отчёт за тот же период без изменения фильтров', async () => {
    const report = mock(async (_query: AdReportQuery) => [row]);
    const client: ReportClient = { report, listCampaigns: async () => [] };
    render(<I18nProvider initialLocale="ru"><ReportTab client={client} now={new Date('2026-09-15T00:00:00Z')} /></I18nProvider>);

    await waitFor(() => expect(screen.getByText('Creative')).toBeInTheDocument());
    expect(report).toHaveBeenCalledTimes(1);
    const firstCall = report.mock.calls[0]?.[0];

    await userEvent.click(screen.getByRole('button', { name: 'Обновить' }));

    await waitFor(() => expect(report).toHaveBeenCalledTimes(2));
    expect(report.mock.calls[1]?.[0]).toEqual(firstCall);
  });
});
