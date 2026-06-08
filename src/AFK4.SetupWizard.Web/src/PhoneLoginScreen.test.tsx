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
  const onUseOwnerCode = mock(() => {});
  const onForgotPassword = mock(() => {});
  render(
    <I18nProvider>
      <PhoneLoginScreen
        onDiscovered={onDiscovered}
        onUseOwnerCode={onUseOwnerCode}
        onForgotPassword={onForgotPassword}
        {...props}
      />
    </I18nProvider>,
  );
  return { onDiscovered, onUseOwnerCode, onForgotPassword };
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
    fireEvent.change(screen.getByLabelText(/пароль/i), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    await waitFor(() => expect(signInByPhone).toHaveBeenCalledTimes(1));
    expect(discoverAuthenticated).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('signs in by email and discovers branches', async () => {
    const { onDiscovered } = renderScreen();
    fireEvent.click(screen.getByRole('radio', { name: /по email/i }));
    fireEvent.change(screen.getByLabelText(/email или логин/i), { target: { value: 'owner@club.tj' } });
    fireEvent.change(screen.getByLabelText(/пароль/i), { target: { value: 'Passw0rd!' } });
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
    fireEvent.click(screen.getByRole('radio', { name: /по email/i }));
    fireEvent.change(screen.getByLabelText(/email или логин/i), { target: { value: 'owner@club.tj' } });
    fireEvent.change(screen.getByLabelText(/пароль/i), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    fireEvent.click(await screen.findByRole('button', { name: 'Клуб Б' }));
    await waitFor(() => expect(signInToClub).toHaveBeenCalledWith('org-b', 'owner@club.tj', 'Passw0rd!'));
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('routes to the owner-code fallback', () => {
    const { onUseOwnerCode } = renderScreen();
    fireEvent.click(screen.getByRole('button', { name: /коду владельца/i }));
    expect(onUseOwnerCode).toHaveBeenCalledTimes(1);
  });

  it('routes to the forgot-password screen', () => {
    const { onForgotPassword } = renderScreen();
    fireEvent.click(screen.getByRole('button', { name: /забыли пароль/i }));
    expect(onForgotPassword).toHaveBeenCalledTimes(1);
  });
});
