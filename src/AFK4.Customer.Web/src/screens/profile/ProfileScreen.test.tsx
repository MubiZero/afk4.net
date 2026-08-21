import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '@/components/ui/toast';
import { ProfileScreen } from './ProfileScreen';
import type { PlayerApiClient } from '@/api/playerApi';
import type { MePersonDto } from '@/api/types';
import { PlayerApiError } from '@/api/playerApi';

const person: MePersonDto = {
  platformPersonId: 'person1', phoneNumber: '+992900000001', displayName: 'Фёдор',
  preferredLocale: 'ru', phoneVerified: false, pinSet: false, networkBanned: false
};

const clubProfile = {
  playerAccountId: 'p1', displayName: 'Фёдор', phoneNumber: '+992900000001',
  phoneVerified: false, preferredLocale: 'ru', marketingOptIn: false
};

function renderScreen(
  api: PlayerApiClient,
  overrides: { person?: MePersonDto | null; onSignOut?: () => void; onPersonChanged?: () => void } = {}
) {
  return render(
    <I18nProvider>
      <ToastProvider autoDismissMs={1000}>
        <ProfileScreen
          api={api}
          person={overrides.person === undefined ? person : overrides.person}
          onPersonChanged={overrides.onPersonChanged ?? (() => {})}
          onSignOut={overrides.onSignOut ?? (() => {})}
          onLocaleChange={() => {}}
        />
      </ToastProvider>
    </I18nProvider>
  );
}

it('renders the profile and states whether the phone is confirmed', async () => {
  const api = { getProfile: mock().mockResolvedValue(clubProfile) } as unknown as PlayerApiClient;
  renderScreen(api);
  expect(await screen.findByText('Фёдор')).toBeInTheDocument();
  expect(screen.getByText(/Номер не подтверждён/i)).toBeInTheDocument();
});

it('PATCHes the marketing opt-in when toggled', async () => {
  const api = {
    getProfile: mock().mockResolvedValue(clubProfile),
    updateProfile: mock().mockResolvedValue({ ...clubProfile, marketingOptIn: true })
  } as unknown as PlayerApiClient;
  renderScreen(api);
  fireEvent.click(await screen.findByLabelText(/рассылк/i));
  await waitFor(() => expect(api.updateProfile).toHaveBeenCalledWith({ marketingOptIn: true }));
});

it('calls onSignOut when the sign-out button is pressed', async () => {
  const api = { getProfile: mock().mockResolvedValue(clubProfile) } as unknown as PlayerApiClient;
  const onSignOut = mock();
  renderScreen(api, { onSignOut });
  fireEvent.click(await screen.findByRole('button', { name: /выйти/i }));
  expect(onSignOut).toHaveBeenCalledTimes(1);
});

// PIN задаётся только в приложении — если панели нет в профиле, задать его человеку негде вообще.
it('даёт задать PIN прямо в профиле', async () => {
  const api = { getProfile: mock().mockResolvedValue(clubProfile) } as unknown as PlayerApiClient;
  renderScreen(api);
  expect(await screen.findByRole('button', { name: 'Задать PIN' })).toBeInTheDocument();
});

// Имя и язык принадлежат человеку, а не карточке в клубе: записанные в клуб, они разъехались бы
// от клуба к клубу.
it('сохраняет язык за человеком, а не за клубной карточкой', async () => {
  const api = {
    getProfile: mock().mockResolvedValue(clubProfile),
    updateMyProfile: mock().mockResolvedValue({ ...person, preferredLocale: 'en' })
  } as unknown as PlayerApiClient;
  renderScreen(api);
  fireEvent.click(await screen.findByRole('button', { name: 'English' }));
  await waitFor(() => expect(api.updateMyProfile).toHaveBeenCalledWith({ displayName: 'Фёдор', preferredLocale: 'en' }));
});

// Человек, зарегистрировавшийся дома, в этом клубе счёта ещё не открыл. Профиль обязан открыться
// и без него — иначе задать PIN до первого визита будет негде.
it('открывается у человека без счёта в этом клубе', async () => {
  const api = {
    getProfile: mock().mockRejectedValue(new PlayerApiError(409, 'club_not_selected'))
  } as unknown as PlayerApiClient;
  renderScreen(api);
  expect(await screen.findByText('Фёдор')).toBeInTheDocument();
  expect(screen.getByRole('button', { name: 'Задать PIN' })).toBeInTheDocument();
  await waitFor(() => expect(screen.queryByLabelText(/рассылк/i)).not.toBeInTheDocument());
});

it('ждёт личность, а не рисует пустой профиль', () => {
  const api = { getProfile: mock().mockResolvedValue(clubProfile) } as unknown as PlayerApiClient;
  renderScreen(api, { person: null });
  expect(screen.getByRole('status')).toBeInTheDocument();
});
