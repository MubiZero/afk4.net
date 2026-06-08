import { describe, it, expect, mock, beforeEach } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

interface LoginResult {
  displayName: string | null;
  requiresClubChoice: boolean;
  clubs: { organizationId: string; name: string }[];
}

const signInByPhone = mock(async () => ({ displayName: 'Сотрудник' }));
const signInByLogin = mock(
  async (): Promise<LoginResult> => ({ displayName: 'Сотрудник', requiresClubChoice: false, clubs: [] }),
);
const signInToClub = mock(async () => ({ displayName: 'Сотрудник' }));
const discoverAuthenticated = mock(async () => ({
  ownerName: 'Сотрудник',
  branches: [
    {
      branchId: '11111111-1111-1111-1111-111111111111',
      branchSlug: 'main',
      branchName: 'Главный',
      zones: [],
      seats: [],
      freeSeatIds: [],
    },
  ],
}));

// bun shares mock.module registrations across files in one run, so every wizardApi mock must
// export the full surface the SUTs import — otherwise a sibling test's partial mock wins and an
// unrelated export resolves as "not found".
mock.module('./wizardApi', () => ({
  signInByPhone,
  signInByLogin,
  signInToClub,
  discoverAuthenticated,
  forgotPasswordByEmail: mock(async () => {}),
  resetPasswordByEmail: mock(async () => {}),
  forgotPasswordByPhone: mock(async () => {}),
  resetPasswordByPhone: mock(async () => {}),
}));

const { PhoneLoginScreen } = await import('./PhoneLoginScreen');

function renderScreen(props: Partial<Parameters<typeof PhoneLoginScreen>[0]> = {}) {
  const onDiscovered = mock(() => {});
  const onForgotPassword = mock(() => {});
  render(
    <I18nProvider>
      <PhoneLoginScreen
        onDiscovered={onDiscovered}
        onForgotPassword={onForgotPassword}
        {...props}
      />
    </I18nProvider>,
  );
  return { onDiscovered, onForgotPassword };
}

describe('PhoneLoginScreen', () => {
  beforeEach(() => {
    signInByPhone.mockClear();
    signInByLogin.mockClear();
    signInToClub.mockClear();
    signInByLogin.mockImplementation(async () => ({ displayName: 'Сотрудник', requiresClubChoice: false, clubs: [] }));
    discoverAuthenticated.mockClear();
  });

  it('signs in then discovers and reports branches', async () => {
    const { onDiscovered } = renderScreen();
    fireEvent.change(screen.getByLabelText(/номер телефона/i), {
      target: { value: '+992 93 738-00-70' },
    });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    await waitFor(() => expect(signInByPhone).toHaveBeenCalledTimes(1));
    expect(discoverAuthenticated).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('signs in by email and discovers branches', async () => {
    const { onDiscovered } = renderScreen();
    fireEvent.click(screen.getByRole('button', { name: /войти по email/i }));
    fireEvent.change(screen.getByLabelText(/email или логин/i), { target: { value: 'owner@club.tj' } });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    await waitFor(() => expect(signInByLogin).toHaveBeenCalledWith('owner@club.tj', 'Passw0rd!'));
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('shows a club picker when the email matches several clubs', async () => {
    signInByLogin.mockImplementationOnce(async () => ({
      displayName: null,
      requiresClubChoice: true,
      clubs: [
        { organizationId: 'org-a', name: 'Клуб А' },
        { organizationId: 'org-b', name: 'Клуб Б' },
      ],
    }));
    const { onDiscovered } = renderScreen();
    fireEvent.click(screen.getByRole('button', { name: /войти по email/i }));
    fireEvent.change(screen.getByLabelText(/email или логин/i), { target: { value: 'owner@club.tj' } });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    fireEvent.click(await screen.findByRole('button', { name: 'Клуб Б' }));
    await waitFor(() => expect(signInToClub).toHaveBeenCalledWith('org-b', 'owner@club.tj', 'Passw0rd!'));
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('reveals "forgot password" only after a failed sign-in, then routes to it', async () => {
    signInByPhone.mockImplementationOnce(async () => {
      throw new Error('bad credentials');
    });
    const { onForgotPassword } = renderScreen();

    // Hidden until the user actually gets the password wrong — keeps the resting screen clean.
    expect(screen.queryByRole('button', { name: /забыли пароль/i })).toBeNull();

    fireEvent.change(screen.getByLabelText(/номер телефона/i), {
      target: { value: '+992 93 738-00-70' },
    });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'wrong' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    const forgot = await screen.findByRole('button', { name: /забыли пароль/i });
    fireEvent.click(forgot);
    expect(onForgotPassword).toHaveBeenCalledTimes(1);
  });
});
