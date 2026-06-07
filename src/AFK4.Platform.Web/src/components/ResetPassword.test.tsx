import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { PlatformApiError } from '@/api/platformApi';
import { ResetPassword } from './ResetPassword';

function fakeClient(reset: (token: string, password: string) => Promise<void> = async () => {}) {
  return { resetPasswordByToken: mock(reset) };
}

function renderScreen(client: ReturnType<typeof fakeClient>, initialToken: string | null = null) {
  return render(
    <I18nProvider>
      <ResetPassword client={client as never} initialToken={initialToken} onBackToSignIn={() => {}} />
    </I18nProvider>
  );
}

it('prefills the token from the URL and completes the reset', async () => {
  const client = fakeClient();
  renderScreen(client, 'tok.en');
  expect((screen.getByLabelText('Код из письма') as HTMLInputElement).value).toBe('tok.en');
  fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
  await waitFor(() => expect(client.resetPasswordByToken).toHaveBeenCalledWith('tok.en', 'Passw0rd!New'));
  expect(await screen.findByText(/Пароль изменён/)).toBeInTheDocument();
});

it('shows an invalid-link error when the token is rejected', async () => {
  const client = fakeClient(async () => { throw new PlatformApiError(400, 'invalid', 'invalid'); });
  renderScreen(client, 'bad-token');
  fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
  expect(await screen.findByText(/недействительна или устарела/)).toBeInTheDocument();
});
