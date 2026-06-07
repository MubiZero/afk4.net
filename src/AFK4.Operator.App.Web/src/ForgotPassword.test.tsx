import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { HostBridgeRequestError } from './hostBridge';

const forgotPasswordByEmail = mock(async () => {});
const forgotPasswordByPhone = mock(async () => {});
const resetPasswordByPhone = mock(async () => {});

mock.module('./authClient', () => ({
  forgotPasswordByEmail,
  forgotPasswordByPhone,
  resetPasswordByPhone
}));

const { ForgotPassword } = await import('./ForgotPassword');

function renderScreen() {
  return render(
    <I18nProvider>
      <ForgotPassword onBackToSignIn={() => {}} onOpenReset={() => {}} />
    </I18nProvider>
  );
}

describe('ForgotPassword (operator)', () => {
  afterEach(() => { mock.restore(); forgotPasswordByEmail.mockClear(); forgotPasswordByPhone.mockClear(); resetPasswordByPhone.mockClear(); });

  it('requests an email reset and confirms it was sent', async () => {
    renderScreen();
    fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: 'owner@demo.test' } });
    fireEvent.click(screen.getByRole('button', { name: 'Отправить код' }));
    await waitFor(() => expect(forgotPasswordByEmail).toHaveBeenCalledWith('owner@demo.test'));
    expect(await screen.findByText(/мы отправили код/i)).toBeInTheDocument();
  });

  it('runs the SMS flow and shows remaining attempts on a bad code', async () => {
    resetPasswordByPhone.mockImplementationOnce(async () => {
      throw new HostBridgeRequestError('bad', 'invalid_code', 2);
    });
    renderScreen();
    fireEvent.click(screen.getByRole('button', { name: 'По SMS' }));
    fireEvent.change(screen.getByLabelText('Номер телефона'), { target: { value: '+992937380070' } });
    fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
    fireEvent.change(await screen.findByLabelText('Код из SMS'), { target: { value: '000000' } });
    fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
    expect(await screen.findByText(/осталось попыток: 2/i)).toBeInTheDocument();
  });
});
