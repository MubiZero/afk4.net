import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { PlatformApiError } from '@/api/platformApi';
import { ForgotPassword } from './ForgotPassword';

function fakeClient(overrides: Partial<{
  forgotPasswordByEmail: (login: string) => Promise<void>;
  resetPasswordByEmail: (login: string, code: string, password: string) => Promise<void>;
  forgotPasswordByPhone: (phone: string) => Promise<void>;
  resetPasswordByPhone: (phone: string, code: string, password: string) => Promise<void>;
}> = {}) {
  return {
    forgotPasswordByEmail: mock(overrides.forgotPasswordByEmail ?? (async () => {})),
    resetPasswordByEmail: mock(overrides.resetPasswordByEmail ?? (async () => {})),
    forgotPasswordByPhone: mock(overrides.forgotPasswordByPhone ?? (async () => {})),
    resetPasswordByPhone: mock(overrides.resetPasswordByPhone ?? (async () => {}))
  };
}

function renderScreen(client: ReturnType<typeof fakeClient>) {
  return render(
    <I18nProvider>
      <ForgotPassword client={client as never} onBackToSignIn={() => {}} />
    </I18nProvider>
  );
}

it('runs the email reset inline: request a code then set a new password', async () => {
  const client = fakeClient();
  renderScreen(client);
  fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: 'owner@demo.test' } });
  fireEvent.click(screen.getByRole('button', { name: 'Отправить код' }));
  await waitFor(() => expect(client.forgotPasswordByEmail).toHaveBeenCalledWith('owner@demo.test'));

  fireEvent.change(await screen.findByLabelText('Код из письма'), { target: { value: '123456' } });
  fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
  await waitFor(() => expect(client.resetPasswordByEmail)
    .toHaveBeenCalledWith('owner@demo.test', '123456', 'Passw0rd!New'));
  expect(await screen.findByText(/Пароль изменён/)).toBeInTheDocument();
});

it('runs the SMS reset flow: request code then set a new password', async () => {
  const client = fakeClient();
  renderScreen(client);
  fireEvent.click(screen.getByRole('button', { name: 'По SMS' }));
  fireEvent.change(screen.getByLabelText('Номер телефона'), { target: { value: '+992937380070' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  await waitFor(() => expect(client.forgotPasswordByPhone).toHaveBeenCalledWith('+992937380070'));

  fireEvent.change(await screen.findByLabelText('Код из SMS'), { target: { value: '123456' } });
  fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
  await waitFor(() => expect(client.resetPasswordByPhone)
    .toHaveBeenCalledWith('+992937380070', '123456', 'Passw0rd!New'));
  expect(await screen.findByText(/Пароль изменён/)).toBeInTheDocument();
});

it('shows remaining attempts on a bad SMS code', async () => {
  const client = fakeClient({
    resetPasswordByPhone: async () => { throw new PlatformApiError(400, 'invalid_code', 'invalid_code', 2); }
  });
  renderScreen(client);
  fireEvent.click(screen.getByRole('button', { name: 'По SMS' }));
  fireEvent.change(screen.getByLabelText('Номер телефона'), { target: { value: '+992937380070' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  fireEvent.change(await screen.findByLabelText('Код из SMS'), { target: { value: '000000' } });
  fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
  expect(await screen.findByText(/Осталось попыток: 2/)).toBeInTheDocument();
});
