import { it, expect, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '@/components/ui/toast';
import { DashboardScreen } from './DashboardScreen';

function apiWith(dashboard: unknown) {
  return {
    getDashboard: mock().mockResolvedValue(dashboard),
    getTopUpIntents: mock().mockResolvedValue([])
  } as unknown as import('@/api/playerApi').PlayerApiClient;
}

it('renders the wallet balance and a no-session empty state', async () => {
  const api = apiWith({
    walletBalance: { currencyCode: 'TJS', minorUnits: 24500 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: null
  });
  render(<I18nProvider><ToastProvider><DashboardScreen api={api} displayName="Фёдор" phoneVerified={false} /></ToastProvider></I18nProvider>);
  expect(await screen.findByText('245,00 TJS')).toBeInTheDocument();
  expect(screen.getByText('Нет активной сессии')).toBeInTheDocument();
});

it('renders the active session seat and a running timer', async () => {
  const api = apiWith({
    walletBalance: { currencyCode: 'TJS', minorUnits: 24500 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: {
      sessionId: 's1', seatId: 'seat1', seatName: 'PC-14 · VIP',
      startedAtUtc: '2026-06-03T20:00:00Z', durationMode: 'open',
      remainingSeconds: null, accruedCostMinorUnits: 3850, currencyCode: 'TJS'
    }
  });
  render(<I18nProvider><ToastProvider><DashboardScreen api={api} displayName="Фёдор" phoneVerified={false} /></ToastProvider></I18nProvider>);
  expect(await screen.findByText('PC-14 · VIP')).toBeInTheDocument();
  await waitFor(() => expect(screen.getByTestId('session-timer').textContent).toMatch(/^\d\d:\d\d:\d\d$/));
});
