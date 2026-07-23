import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { StaffAuthApiError } from './authClient';

const forgotPasswordByEmail = mock(async () => {});
const resetPasswordByEmail = mock(async () => {});
const forgotPasswordByPhone = mock(async () => {});
const resetPasswordByPhone = mock(async () => {});

// bun's mock.module registrations are global for the whole run and survive mock.restore(). Snapshot
// the real module BEFORE mocking and keep the full surface in the override, so sibling files keep
// the real signIn/load/etc.; afterAll restores everything (otherwise the reset stubs leak into
// authClient.test.ts, whose bridge-behaviour assertions then see a no-op and fail by run order).
const realAuthClient = { ...(await import('./authClient')) };

mock.module('./authClient', () => ({
  ...realAuthClient,
  forgotPasswordByEmail,
  resetPasswordByEmail,
  forgotPasswordByPhone,
  resetPasswordByPhone
}));

const { ForgotPassword } = await import('./ForgotPassword');

afterAll(() => {
  mock.module('./authClient', () => realAuthClient);
});

function renderScreen() {
  return render(
    <I18nProvider>
      <ForgotPassword onBackToSignIn={() => {}} />
    </I18nProvider>
  );
}

describe('ForgotPassword (operator)', () => {
  afterEach(() => {
    mock.restore();
    forgotPasswordByEmail.mockClear();
    resetPasswordByEmail.mockClear();
    forgotPasswordByPhone.mockClear();
    resetPasswordByPhone.mockClear();
  });

  it('runs the email reset inline: request a code then set a new password', async () => {
    renderScreen();
    fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: 'owner@demo.test' } });
    fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
    await waitFor(() => expect(forgotPasswordByEmail).toHaveBeenCalledWith('owner@demo.test'));

    fireEvent.change(await screen.findByLabelText('Код из письма'), { target: { value: '123456' } });
    fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
    await waitFor(() => expect(resetPasswordByEmail).toHaveBeenCalledWith('owner@demo.test', '123456', 'Passw0rd!New'));
    expect(await screen.findByText(/пароль изменён/i)).toBeInTheDocument();
  });

  it('runs the SMS flow and shows remaining attempts on a bad code', async () => {
    resetPasswordByPhone.mockImplementationOnce(async () => {
      // Реальный прод-путь: StaffAuthApi разбирает JSON-тело ответа бэка (см. AuthEndpoints.cs),
      // а не bridge-специфичный код нативного моста.
      throw new StaffAuthApiError(400, { error: 'invalid_code', remainingAttempts: 2 });
    });
    renderScreen();
    fireEvent.click(screen.getByRole('button', { name: 'По SMS' }));
    fireEvent.change(screen.getByLabelText(/Номер телефона/i), { target: { value: '+992937380070' } });
    fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
    fireEvent.change(await screen.findByLabelText('Код из SMS'), { target: { value: '000000' } });
    fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
    expect(await screen.findByText(/осталось попыток: 2/i)).toBeInTheDocument();
  });
});
