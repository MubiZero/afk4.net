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

// The wizard opens in phone mode; the login/email field is behind "sign in with login or email".
function switchToCredentials() {
  fireEvent.click(screen.getByRole('button', { name: /вход по логину или почте/i }));
}

describe('PhoneLoginScreen', () => {
  beforeEach(() => {
    signInByPhone.mockClear();
    signInByLogin.mockClear();
    signInToClub.mockClear();
    signInByLogin.mockImplementation(async () => ({ displayName: 'Сотрудник', requiresClubChoice: false, clubs: [] }));
    discoverAuthenticated.mockClear();
  });

  it('signs in by phone in the default mode, then discovers branches', async () => {
    const { onDiscovered } = renderScreen();
    fireEvent.change(screen.getByLabelText(/номер телефона/i), {
      target: { value: '+992 93 738-00-70' },
    });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    await waitFor(() => expect(signInByPhone).toHaveBeenCalledTimes(1));
    // The fixed +992 prefix + 9 local digits are sent as the full dialable number.
    expect(signInByPhone).toHaveBeenCalledWith('992937380070', 'Passw0rd!');
    expect(signInByLogin).not.toHaveBeenCalled();
    expect(discoverAuthenticated).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('signs in by login/email after switching to the credentials mode', async () => {
    const { onDiscovered } = renderScreen();
    switchToCredentials();
    fireEvent.change(screen.getByLabelText(/логин или email/i), { target: { value: 'owner@club.tj' } });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    await waitFor(() => expect(signInByLogin).toHaveBeenCalledWith('owner@club.tj', 'Passw0rd!'));
    expect(signInByPhone).not.toHaveBeenCalled();
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('shows a club picker when the login matches several clubs', async () => {
    signInByLogin.mockImplementationOnce(async () => ({
      displayName: null,
      requiresClubChoice: true,
      clubs: [
        { organizationId: 'org-a', name: 'Клуб А' },
        { organizationId: 'org-b', name: 'Клуб Б' },
      ],
    }));
    const { onDiscovered } = renderScreen();
    switchToCredentials();
    fireEvent.change(screen.getByLabelText(/логин или email/i), { target: { value: 'owner@club.tj' } });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    fireEvent.click(await screen.findByRole('button', { name: 'Клуб Б' }));
    await waitFor(() => expect(signInToClub).toHaveBeenCalledWith('org-b', 'owner@club.tj', 'Passw0rd!'));
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('masks the local digits as the user types; +992 is a fixed affix, not in the value', () => {
    renderScreen();
    const field = screen.getByLabelText(/номер телефона/i) as HTMLInputElement;
    fireEvent.change(field, { target: { value: '937380070' } });
    // The editable value holds ONLY the masked local part — the +992 prefix lives outside the input.
    expect(field.value).toBe('93 738 00 70');
    expect(screen.getByText('+992')).toBeTruthy();
  });

  it('drops a pasted country code, keeping only the 9 local digits', () => {
    renderScreen();
    const field = screen.getByLabelText(/номер телефона/i) as HTMLInputElement;
    fireEvent.change(field, { target: { value: '+992 93 738 00 70' } });
    expect(field.value).toBe('93 738 00 70');
  });

  it('shortens cleanly when local digits are deleted', () => {
    renderScreen();
    const field = screen.getByLabelText(/номер телефона/i) as HTMLInputElement;
    fireEvent.change(field, { target: { value: '937' } });
    expect(field.value).toBe('93 7');
    fireEvent.change(field, { target: { value: '' } });
    expect(field.value).toBe('');
  });

  it('rejects a malformed email on submit without calling the backend', async () => {
    renderScreen();
    switchToCredentials();
    fireEvent.change(screen.getByLabelText(/логин или email/i), { target: { value: 'owner@club' } });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    expect(await screen.findByText(/проверьте адрес/i)).toBeTruthy();
    expect(signInByLogin).not.toHaveBeenCalled();
  });

  it('never scolds a login: no hint, and it routes to login sign-in', async () => {
    renderScreen();
    switchToCredentials();
    const field = screen.getByLabelText(/логин или email/i);
    fireEvent.change(field, { target: { value: 'ivan' } });
    fireEvent.blur(field);

    expect(screen.queryByText(/проверьте адрес/i)).toBeNull();

    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));
    await waitFor(() => expect(signInByLogin).toHaveBeenCalledWith('ivan', 'Passw0rd!'));
  });

  it('shows "forgot password" up front and routes to it', () => {
    const { onForgotPassword } = renderScreen();
    // Recovery is always offered now — no need to fail a sign-in first.
    const forgot = screen.getByRole('button', { name: /забыли пароль/i });
    fireEvent.click(forgot);
    expect(onForgotPassword).toHaveBeenCalledTimes(1);
  });

  it('opens in credentials mode when prefilled with an email (from a reset round-trip)', () => {
    renderScreen({ initialIdentity: 'owner@club.tj' });
    expect((screen.getByLabelText(/логин или email/i) as HTMLInputElement).value).toBe('owner@club.tj');
  });
});
