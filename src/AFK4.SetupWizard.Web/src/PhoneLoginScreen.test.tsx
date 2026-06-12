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

  it('routes a phone-shaped identity to phone sign-in, then discovers branches', async () => {
    const { onDiscovered } = renderScreen();
    fireEvent.change(screen.getByLabelText(/телефон, логин или email/i), {
      target: { value: '+992 93 738-00-70' },
    });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    await waitFor(() => expect(signInByPhone).toHaveBeenCalledTimes(1));
    expect(signInByLogin).not.toHaveBeenCalled();
    expect(discoverAuthenticated).toHaveBeenCalledTimes(1);
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('routes a login/email identity to login sign-in, then discovers branches', async () => {
    const { onDiscovered } = renderScreen();
    fireEvent.change(screen.getByLabelText(/телефон, логин или email/i), { target: { value: 'owner@club.tj' } });
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
    fireEvent.change(screen.getByLabelText(/телефон, логин или email/i), { target: { value: 'owner@club.tj' } });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    fireEvent.click(await screen.findByRole('button', { name: 'Клуб Б' }));
    await waitFor(() => expect(signInToClub).toHaveBeenCalledWith('org-b', 'owner@club.tj', 'Passw0rd!'));
    await waitFor(() => expect(onDiscovered).toHaveBeenCalledTimes(1));
  });

  it('auto-prefixes +992 and masks the local digits as the user types a phone', () => {
    renderScreen();
    const field = screen.getByLabelText(/телефон, логин или email/i) as HTMLInputElement;
    fireEvent.change(field, { target: { value: '937380070' } });
    expect(field.value).toBe('+992 93 738 00 70');
  });

  it('drops a typed country code instead of doubling it', () => {
    renderScreen();
    const field = screen.getByLabelText(/телефон, логин или email/i) as HTMLInputElement;
    fireEvent.change(field, { target: { value: '+992 93 738 00 70' } });
    expect(field.value).toBe('+992 93 738 00 70');
  });

  it('clears instead of resurrecting digits when backspacing through the +992 prefix', () => {
    renderScreen();
    const field = screen.getByLabelText(/телефон, логин или email/i) as HTMLInputElement;
    fireEvent.change(field, { target: { value: '9' } });
    expect(field.value).toBe('+992 9');
    // Backspacing the last local digit leaves "+992 "; it must collapse to empty, not survive as a
    // bare prefix that the next backspace would re-read as local digits ("+992 99" bug).
    fireEvent.change(field, { target: { value: '+992 ' } });
    expect(field.value).toBe('');
  });

  it('never expands a lone or half-deleted +992 prefix into fake local digits', () => {
    renderScreen();
    const field = screen.getByLabelText(/телефон, логин или email/i) as HTMLInputElement;
    fireEvent.change(field, { target: { value: '+992' } });
    expect(field.value).toBe('');
    fireEvent.change(field, { target: { value: '+99' } });
    expect(field.value).toBe('');
  });

  it('rejects a malformed email on submit without calling the backend', async () => {
    renderScreen();
    fireEvent.change(screen.getByLabelText(/телефон, логин или email/i), { target: { value: 'owner@club' } });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    expect(await screen.findByText(/проверьте адрес/i)).toBeTruthy();
    expect(signInByLogin).not.toHaveBeenCalled();
  });

  it('never scolds a login: no hint, and it routes to login sign-in', async () => {
    renderScreen();
    const field = screen.getByLabelText(/телефон, логин или email/i);
    fireEvent.change(field, { target: { value: 'ivan' } });
    fireEvent.blur(field);

    expect(screen.queryByText(/проверьте адрес/i)).toBeNull();
    expect(screen.queryByText(/введите номер/i)).toBeNull();

    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'Passw0rd!' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));
    await waitFor(() => expect(signInByLogin).toHaveBeenCalledWith('ivan', 'Passw0rd!'));
  });

  it('reveals "forgot password" only after a failed sign-in, then routes to it', async () => {
    signInByPhone.mockImplementationOnce(async () => {
      throw new Error('bad credentials');
    });
    const { onForgotPassword } = renderScreen();

    // Hidden until the user actually gets the password wrong — keeps the resting screen clean.
    expect(screen.queryByRole('button', { name: /забыли пароль/i })).toBeNull();

    fireEvent.change(screen.getByLabelText(/телефон, логин или email/i), {
      target: { value: '+992 93 738-00-70' },
    });
    fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: 'wrong' } });
    fireEvent.click(screen.getByRole('button', { name: /войти$/i }));

    const forgot = await screen.findByRole('button', { name: /забыли пароль/i });
    fireEvent.click(forgot);
    expect(onForgotPassword).toHaveBeenCalledTimes(1);
  });
});
