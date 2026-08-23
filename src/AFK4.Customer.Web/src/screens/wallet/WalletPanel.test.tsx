import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '@/components/ui/toast';
import { WalletPanel } from './WalletPanel';
import type { PlayerApiClient } from '@/api/playerApi';

function renderPanel(api: PlayerApiClient, phoneVerified: boolean, features: string[] | null = null) {
  return render(
    <I18nProvider>
      <ToastProvider autoDismissMs={1000}>
        <WalletPanel api={api} phoneVerified={phoneVerified} features={features} />
      </ToastProvider>
    </I18nProvider>
  );
}

it('closes the top-up form with an explanation when the phone is unverified', async () => {
  const api = { getTopUpIntents: mock().mockResolvedValue([]) } as unknown as PlayerApiClient;
  renderPanel(api, false);
  expect(await screen.findByText(/подтвердите свой номер/i)).toBeInTheDocument();
  expect(screen.queryByLabelText('Сумма')).not.toBeInTheDocument();
});

it('submits a top-up request and shows it in the intent list', async () => {
  const api = {
    getTopUpIntents: mock()
      .mockResolvedValueOnce([])
      .mockResolvedValueOnce([{ paymentIntentId: 'i1', amountMinorUnits: 5000, currencyCode: 'TJS', state: 'pending', purpose: 'wallet_topup', method: 'counter', createdAtUtc: '2026-06-03T10:00:00Z', fulfilledAtUtc: null, isExpired: false }]),
    createTopUpIntent: mock().mockResolvedValue({ paymentIntentId: 'i1' })
  } as unknown as PlayerApiClient;
  renderPanel(api, true);
  const amount = await screen.findByLabelText('Сумма');
  fireEvent.change(amount, { target: { value: '50' } });
  fireEvent.click(screen.getByRole('button', { name: /внести на стойке/i }));
  await waitFor(() => expect(api.createTopUpIntent).toHaveBeenCalledWith({ amountMinorUnits: 5000, currencyCode: 'TJS' }));
  expect(await screen.findByText('Ожидает')).toBeInTheDocument();
});

it('прячет пополнение кошелька, когда онлайн-пополнение выключено', async () => {
  const api = { getTopUpIntents: mock().mockResolvedValue([]) } as unknown as PlayerApiClient;
  renderPanel(api, true, ['loyalty']);
  await screen.findByText('Пополнить кошелёк');
  expect(screen.queryByLabelText('Сумма')).not.toBeInTheDocument();
  expect(screen.queryByRole('button', { name: /запросить/i })).not.toBeInTheDocument();
});
