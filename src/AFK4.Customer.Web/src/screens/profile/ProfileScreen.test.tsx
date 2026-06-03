import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '@/components/ui/toast';
import { ProfileScreen } from './ProfileScreen';
import type { PlayerApiClient } from '@/api/playerApi';

function renderScreen(api: PlayerApiClient, onSignOut = () => {}) {
  return render(
    <I18nProvider>
      <ToastProvider autoDismissMs={1000}>
        <ProfileScreen api={api} onSignOut={onSignOut} onLocaleChange={() => {}} />
      </ToastProvider>
    </I18nProvider>
  );
}

const profile = {
  playerAccountId: 'p1', displayName: 'Фёдор', phoneNumber: '+992900000001',
  phoneVerified: false, preferredLocale: 'ru', marketingOptIn: false
};

it('renders the profile and a disabled OTP note for the phone', async () => {
  const api = { getProfile: mock().mockResolvedValue(profile) } as unknown as PlayerApiClient;
  renderScreen(api);
  expect(await screen.findByText('Фёдор')).toBeInTheDocument();
  expect(screen.getByText(/через OTP/i)).toBeInTheDocument();
});

it('PATCHes the marketing opt-in when toggled', async () => {
  const api = {
    getProfile: mock().mockResolvedValue(profile),
    updateProfile: mock().mockResolvedValue({ ...profile, marketingOptIn: true })
  } as unknown as PlayerApiClient;
  renderScreen(api);
  fireEvent.click(await screen.findByLabelText(/рассылк/i));
  await waitFor(() => expect(api.updateProfile).toHaveBeenCalledWith({ marketingOptIn: true }));
});

it('calls onSignOut when the sign-out button is pressed', async () => {
  const api = { getProfile: mock().mockResolvedValue(profile) } as unknown as PlayerApiClient;
  const onSignOut = mock();
  renderScreen(api, onSignOut);
  fireEvent.click(await screen.findByRole('button', { name: /выйти/i }));
  expect(onSignOut).toHaveBeenCalledTimes(1);
});
