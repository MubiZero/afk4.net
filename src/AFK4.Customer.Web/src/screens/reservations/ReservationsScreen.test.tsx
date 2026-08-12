import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '@/components/ui/toast';
import { ReservationsScreen } from './ReservationsScreen';
import type { PlayerApiClient } from '@/api/playerApi';

function renderScreen(api: PlayerApiClient, phoneVerified: boolean) {
  return render(
    <I18nProvider>
      <ToastProvider autoDismissMs={1000}>
        <ReservationsScreen api={api} phoneVerified={phoneVerified} />
      </ToastProvider>
    </I18nProvider>
  );
}

it('lists reservations with a localized state', async () => {
  const api = { getReservations: mock().mockResolvedValue([
    { reservationId: 'r1', seatId: 's1', seatName: 'PC-7', startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'pending', note: null }
  ]) } as unknown as PlayerApiClient;
  renderScreen(api, true);
  expect(await screen.findByText('PC-7')).toBeInTheDocument();
  expect(screen.getByText('Ожидает подтверждения')).toBeInTheDocument();
});

it('hides the create form behind the D8 gate when the phone is unverified', async () => {
  const api = { getReservations: mock().mockResolvedValue([]) } as unknown as PlayerApiClient;
  renderScreen(api, false);
  expect(await screen.findByText(/подтвердите свой номер/i)).toBeInTheDocument();
  expect(screen.queryByLabelText('Начало')).not.toBeInTheDocument();
});

it('cancels a reservation after confirmation', async () => {
  const api = {
    getReservations: mock()
      .mockResolvedValueOnce([{ reservationId: 'r1', seatId: null, seatName: null, startsAtUtc: '2999-01-01T10:00:00Z', endsAtUtc: '2999-01-01T12:00:00Z', state: 'pending', note: null }])
      .mockResolvedValueOnce([]),
    cancelReservation: mock().mockResolvedValue({ reservationId: 'r1', state: 'cancelled' })
  } as unknown as PlayerApiClient;
  (globalThis as { confirm: () => boolean }).confirm = () => true;
  renderScreen(api, true);
  fireEvent.click(await screen.findByRole('button', { name: /отменить/i }));
  await waitFor(() => expect(api.cancelReservation).toHaveBeenCalledWith('r1'));
});
