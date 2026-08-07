import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { AnalyticsTab } from './AnalyticsTab';
import type { AnalyticsMonth, AnalyticsOverview } from '@/api/types';

function month(overrides: Partial<AnalyticsMonth> = {}): AnalyticsMonth {
  return {
    year: 2026,
    month: 1,
    recurringMinorUnits: 0,
    oneOffMinorUnits: 0,
    joined: 0,
    left: 0,
    payingAtMonthEnd: 0,
    ...overrides
  };
}

function overview(overrides: Partial<AnalyticsOverview> = {}): AnalyticsOverview {
  return {
    generatedAtUtc: '2026-08-07T00:00:00Z',
    currencyCode: 'TJS',
    months: [month()],
    currentMrrMinorUnits: 0,
    currentPayingClubs: 0,
    averageRevenuePerClubMinorUnits: 0,
    outstandingMinorUnits: 0,
    ...overrides
  };
}

describe('AnalyticsTab', () => {
  it('shows the summary amount and the revenue chart title when there is revenue', async () => {
    const client = {
      getOverview: mock().mockResolvedValue(overview({
        currentMrrMinorUnits: 450000,
        currentPayingClubs: 12,
        averageRevenuePerClubMinorUnits: 37500,
        outstandingMinorUnits: 10000,
        months: [month({ recurringMinorUnits: 400000, oneOffMinorUnits: 50000, payingAtMonthEnd: 12, joined: 2 })]
      }))
    };
    render(<I18nProvider><AnalyticsTab client={client} /></I18nProvider>);

    await waitFor(() => expect(screen.getByText('Выручка по месяцам')).toBeInTheDocument());
    // currentMrrMinorUnits: 450000 must render as MAJOR units (4500), not 450000.
    const mrrTile = screen.getByText('Выручка в месяц').closest('.pc-analytics-tile');
    const digits = (mrrTile?.textContent ?? '').replace(/\D/g, '');
    expect(digits).toContain('4500');
    expect(digits).not.toContain('450000');
  });

  it('shows the empty-data message when every month is zero', async () => {
    const client = { getOverview: mock().mockResolvedValue(overview()) };
    render(<I18nProvider><AnalyticsTab client={client} /></I18nProvider>);

    await waitFor(() => expect(screen.getByText('Данных пока нет — первые цифры появятся, когда пройдут сутки')).toBeInTheDocument());
  });

  it('shows an error state with retry — and never the empty-data text — when the request fails', async () => {
    const client = { getOverview: mock().mockRejectedValue(new Error('network down')) };
    render(<I18nProvider><AnalyticsTab client={client} /></I18nProvider>);

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Повторить' })).toBeInTheDocument();
    expect(screen.queryByText('Данных пока нет — первые цифры появятся, когда пройдут сутки')).not.toBeInTheDocument();
  });
});
