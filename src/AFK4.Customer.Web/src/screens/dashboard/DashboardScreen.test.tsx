import { it, expect, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '@/components/ui/toast';
import { DashboardScreen } from './DashboardScreen';
import { PlayerApiError } from '@/api/playerApi';
import { branchChoice } from '@/branch/branchChoice';

// У человека со счётом в клубе зал уже записан — дашборд ни о чём не спрашивает.
const settled = branchChoice([], null, 'b1');

function apiWith(dashboard: unknown) {
  return {
    getDashboard: mock().mockResolvedValue(dashboard),
    getTopUpIntents: mock().mockResolvedValue([])
  } as unknown as import('@/api/playerApi').PlayerApiClient;
}

it('renders the wallet balance and a no-session empty state', async () => {
  const api = apiWith({
    walletBalance: { currencyCode: 'TJS', minorUnits: 24500 },
    heldBalance: { currencyCode: 'TJS', minorUnits: 0 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: null
  });
  render(<I18nProvider><ToastProvider><DashboardScreen branch={settled} onChooseBranch={() => {}} api={api} displayName="Фёдор" phoneVerified={false} /></ToastProvider></I18nProvider>);
  expect(await screen.findByText('245,00 TJS')).toBeInTheDocument();
  expect(screen.getByText('Нет активной сессии')).toBeInTheDocument();
});

it('renders the active session seat and a running timer', async () => {
  const api = apiWith({
    walletBalance: { currencyCode: 'TJS', minorUnits: 24500 },
    heldBalance: { currencyCode: 'TJS', minorUnits: 0 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: {
      // A recent, relative start keeps the open-mode timer at two-digit hours forever — a fixed past
      // date drifts and eventually renders 3-digit hours, racing the first tick (flaky in CI).
      sessionId: 's1', seatId: 'seat1', seatName: 'PC-14 · VIP',
      startedAtUtc: new Date(Date.now() - 90_000).toISOString(), durationMode: 'open',
      remainingSeconds: null, accruedCostMinorUnits: 3850, currencyCode: 'TJS'
    }
  });
  render(<I18nProvider><ToastProvider><DashboardScreen branch={settled} onChooseBranch={() => {}} api={api} displayName="Фёдор" phoneVerified={false} /></ToastProvider></I18nProvider>);
  expect(await screen.findByText('PC-14 · VIP')).toBeInTheDocument();
  await waitFor(() => expect(screen.getByTestId('session-timer').textContent).toMatch(/^\d\d:\d\d:\d\d$/));
});

// Третье число — ответ на вопрос «а куда делись мои деньги»: сумма под бронь из остатка уже
// вычтена, и без строки о ней человек видит пропажу без объяснения.
it('показывает придержанное под брони отдельной строкой', async () => {
  const api = apiWith({
    walletBalance: { currencyCode: 'TJS', minorUnits: 24500 },
    heldBalance: { currencyCode: 'TJS', minorUnits: 5000 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: null
  });
  render(<I18nProvider><ToastProvider><DashboardScreen branch={settled} onChooseBranch={() => {}} api={api} displayName="Фёдор" phoneVerified={false} /></ToastProvider></I18nProvider>);
  expect(await screen.findByText(/Придержано под брони/)).toBeInTheDocument();
  expect(screen.getByText('50,00 TJS')).toBeInTheDocument();
});

it('не показывает придержанное, когда придерживать нечего', async () => {
  const api = apiWith({
    walletBalance: { currencyCode: 'TJS', minorUnits: 24500 },
    heldBalance: { currencyCode: 'TJS', minorUnits: 0 },
    debtBalance: { currencyCode: 'TJS', minorUnits: 0 },
    activeSession: null
  });
  render(<I18nProvider><ToastProvider><DashboardScreen branch={settled} onChooseBranch={() => {}} api={api} displayName="Фёдор" phoneVerified={false} /></ToastProvider></I18nProvider>);
  await screen.findByText('245,00 TJS');
  expect(screen.queryByText(/Придержано под брони/)).not.toBeInTheDocument();
});

// Человек, зарегистрировавшийся дома, в этом клубе ещё не начинал — счёта у него тут нет, и
// «не удалось загрузить» соврало бы про поломку там, где всё в порядке.
it('объясняет отсутствие счёта в клубе вместо ошибки загрузки', async () => {
  const api = {
    getDashboard: mock().mockRejectedValue(new PlayerApiError(409, 'club_not_selected')),
    getTopUpIntents: mock().mockResolvedValue([])
  } as unknown as import('@/api/playerApi').PlayerApiClient;
  render(<I18nProvider><ToastProvider><DashboardScreen branch={settled} onChooseBranch={() => {}} api={api} displayName="Фёдор" phoneVerified /></ToastProvider></I18nProvider>);
  expect(await screen.findByText('Вы здесь ещё не начинали')).toBeInTheDocument();
  expect(screen.queryByText(/Не удалось загрузить данные/)).not.toBeInTheDocument();
});
