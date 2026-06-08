import { describe, it, expect, mock, beforeEach } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { HostBridgeRequestError } from './hostBridge';

const forgotPasswordByEmail = mock(async () => {});
const resetPasswordByEmail = mock(async () => {});
const forgotPasswordByPhone = mock(async () => {});
const resetPasswordByPhone = mock(async () => {});

// bun shares mock.module registrations across files in one run, so export the full wizardApi
// surface the SUTs import (see PhoneLoginScreen.test for the same guard).
mock.module('./wizardApi', () => ({
  signInByPhone: mock(async () => ({ displayName: 'Сотрудник' })),
  signInByLogin: mock(async () => ({ displayName: 'Сотрудник', requiresClubChoice: false, clubs: [] })),
  signInToClub: mock(async () => ({ displayName: 'Сотрудник' })),
  discoverAuthenticated: mock(async () => ({ ownerName: 'Сотрудник', branches: [] })),
  forgotPasswordByEmail,
  resetPasswordByEmail,
  forgotPasswordByPhone,
  resetPasswordByPhone,
}));

const { ForgotPasswordScreen } = await import('./ForgotPasswordScreen');

function renderScreen() {
  const onBack = mock(() => {});
  render(
    <I18nProvider>
      <ForgotPasswordScreen onBack={onBack} />
    </I18nProvider>,
  );
  return { onBack };
}

describe('ForgotPasswordScreen', () => {
  beforeEach(() => {
    forgotPasswordByEmail.mockClear();
    resetPasswordByEmail.mockClear();
    forgotPasswordByPhone.mockClear();
    resetPasswordByPhone.mockClear();
  });

  it('runs the SMS reset inline: request code, then set a new password', async () => {
    renderScreen();
    // Phone channel is the default for the wizard.
    fireEvent.change(screen.getByLabelText(/номер телефона/i), { target: { value: '+992937380070' } });
    fireEvent.click(screen.getByRole('button', { name: /получить код/i }));

    await waitFor(() => expect(forgotPasswordByPhone).toHaveBeenCalledTimes(1));
    expect(forgotPasswordByPhone).toHaveBeenCalledWith('+992937380070');

    // Step 2: code + new password appear after the code is sent.
    fireEvent.change(await screen.findByLabelText(/код из sms/i), { target: { value: '123456' } });
    fireEvent.change(screen.getByLabelText(/новый пароль/i), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: /сменить пароль/i }));

    await waitFor(() => expect(resetPasswordByPhone).toHaveBeenCalledTimes(1));
    expect(resetPasswordByPhone).toHaveBeenCalledWith('+992937380070', '123456', 'Passw0rd!New');
    expect(await screen.findByText(/пароль изменён/i)).toBeTruthy();
  });

  it('shows the remaining attempts when the SMS code is wrong', async () => {
    resetPasswordByPhone.mockImplementationOnce(async () => {
      throw new HostBridgeRequestError('bad code', 'invalid_code', 2);
    });
    renderScreen();
    fireEvent.change(screen.getByLabelText(/номер телефона/i), { target: { value: '+992937380070' } });
    fireEvent.click(screen.getByRole('button', { name: /получить код/i }));

    fireEvent.change(await screen.findByLabelText(/код из sms/i), { target: { value: '000000' } });
    fireEvent.change(screen.getByLabelText(/новый пароль/i), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: /сменить пароль/i }));

    const alert = await screen.findByRole('alert');
    expect(alert.textContent).toContain('Неверный код');
    expect(alert.textContent).toContain('2');
  });

  it('runs the email reset inline: request a code then set a new password', async () => {
    renderScreen();
    fireEvent.click(screen.getByRole('radio', { name: /по email/i }));
    fireEvent.change(screen.getByLabelText(/логин или email/i), { target: { value: 'owner@club.tj' } });
    fireEvent.click(screen.getByRole('button', { name: /отправить код/i }));

    await waitFor(() => expect(forgotPasswordByEmail).toHaveBeenCalledWith('owner@club.tj'));

    fireEvent.change(await screen.findByLabelText(/код из письма/i), { target: { value: '123456' } });
    fireEvent.change(screen.getByLabelText(/новый пароль/i), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: /сменить пароль/i }));

    await waitFor(() => expect(resetPasswordByEmail).toHaveBeenCalledWith('owner@club.tj', '123456', 'Passw0rd!New'));
    expect(await screen.findByText(/пароль изменён/i)).toBeTruthy();
  });

  it('returns to sign-in via the back link', () => {
    const { onBack } = renderScreen();
    fireEvent.click(screen.getByRole('button', { name: /вернуться ко входу/i }));
    expect(onBack).toHaveBeenCalledTimes(1);
  });
});
