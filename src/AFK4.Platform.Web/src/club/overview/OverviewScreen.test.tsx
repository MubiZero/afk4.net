import { describe, expect, it, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OverviewScreen } from './OverviewScreen';
import type { OverviewState } from './useOverview';

function wrap(state: OverviewState) {
  return render(<I18nProvider><OverviewScreen state={state} /></I18nProvider>);
}

const ready: OverviewState = {
  status: 'ready', retry: vi.fn(),
  data: {
    kpis: { devicesOnline: { online: 28, total: 30 }, activeSessions: 19, utilizationPercent: 63, revenueToday: { amount: 4250, currencyCode: 'TJS' }, attention: 3 },
    revenueBreakdown: [{ key: 'gameplay', amount: 3000 }, { key: 'pos', amount: 1250 }],
    attention: [{ deviceId: 'd1', name: 'ПК-14', kind: 'offline' }]
  }
};

describe('OverviewScreen', () => {
  it('renders KPI values when ready', () => {
    wrap(ready);
    expect(screen.getByText('Активные сессии')).toBeInTheDocument();
    expect(screen.getByText('19')).toBeInTheDocument();
    expect(screen.getByText('ПК-14')).toBeInTheDocument();
  });

  it('shows a loading skeleton', () => {
    wrap({ status: 'loading', retry: vi.fn() });
    expect(screen.getByTestId('overview-loading')).toBeInTheDocument();
  });

  it('shows an error with a working retry', () => {
    const retry = vi.fn();
    wrap({ status: 'error', message: 'x', retry });
    fireEvent.click(screen.getByText('Повторить'));
    expect(retry).toHaveBeenCalled();
  });

  it('shows the empty attention message when there is nothing to attend to', () => {
    wrap({ ...ready, data: { ...ready.data!, attention: [] } } as OverviewState);
    expect(screen.getByText('Всё в порядке — ничего не требует внимания.')).toBeInTheDocument();
  });
});
