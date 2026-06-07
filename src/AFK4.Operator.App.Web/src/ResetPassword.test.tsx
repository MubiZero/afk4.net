import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { HostBridgeRequestError } from './hostBridge';

const resetPasswordByEmail = mock(async () => {});
mock.module('./authClient', () => ({ resetPasswordByEmail }));

const { ResetPassword } = await import('./ResetPassword');

describe('ResetPassword (operator)', () => {
  afterEach(() => { mock.restore(); resetPasswordByEmail.mockClear(); });

  it('submits the pasted code and new password', async () => {
    render(<I18nProvider><ResetPassword onBackToSignIn={() => {}} /></I18nProvider>);
    fireEvent.change(screen.getByLabelText('Код из письма'), { target: { value: 'tok.en' } });
    fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
    await waitFor(() => expect(resetPasswordByEmail).toHaveBeenCalledWith('tok.en', 'Passw0rd!New'));
    expect(await screen.findByText(/Пароль изменён/)).toBeInTheDocument();
  });

  it('shows an invalid-link error when the code is rejected', async () => {
    resetPasswordByEmail.mockImplementationOnce(async () => { throw new HostBridgeRequestError('bad', 'reset_failed', null); });
    render(<I18nProvider><ResetPassword onBackToSignIn={() => {}} /></I18nProvider>);
    fireEvent.change(screen.getByLabelText('Код из письма'), { target: { value: 'bad' } });
    fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
    expect(await screen.findByText(/недействительна или устарела/)).toBeInTheDocument();
  });
});
