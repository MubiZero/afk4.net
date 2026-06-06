import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { PlatformApiError } from './platformApi';

const getMyPhone = mock(async () => ({ phone: null, phoneVerifiedAtUtc: null }));
const startPhoneVerification = mock(async () => ({ expiresInSeconds: 300, resendAfterSeconds: 60 }));
const confirmPhoneVerification = mock(async () => ({ phone: '+992937380070' }));

const actualClients = await import('./operatorApiClients');
mock.module('./operatorApiClients', () => ({
  ...actualClients,
  createOperatorApiClients: () => ({
    account: { getMyPhone, startPhoneVerification, confirmPhoneVerification }
  })
}));

const { PhoneVerificationCard } = await import('./PhoneVerificationCard');

const backend = { config: { platformBaseUrl: 'http://test' }, session: { accessToken: 't' } };

describe('PhoneVerificationCard (operator)', () => {
  afterEach(() => { cleanup(); mock.restore(); });

  it('sends a code then confirms it', async () => {
    render(<I18nProvider><PhoneVerificationCard backend={backend} /></I18nProvider>);
    const input = await screen.findByLabelText('Номер телефона');
    fireEvent.change(input, { target: { value: '+992937380070' } });
    fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
    await waitFor(() => expect(startPhoneVerification).toHaveBeenCalledWith({ phone: '+992937380070' }));

    const codeInput = await screen.findByLabelText('Код из SMS');
    fireEvent.change(codeInput, { target: { value: '123456' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
    await waitFor(() => expect(confirmPhoneVerification).toHaveBeenCalledWith({ code: '123456' }));
    expect(await screen.findByText('подтверждён')).toBeInTheDocument();
  });

  it('shows a retry button when the initial load fails', async () => {
    getMyPhone.mockImplementationOnce(async () => { throw new PlatformApiError('boom', 500, 'Server Error', ''); });
    render(<I18nProvider><PhoneVerificationCard backend={backend} /></I18nProvider>);
    expect(await screen.findByRole('button', { name: /повторить/i })).toBeInTheDocument();
  });

  it('shows remaining attempts on invalid_code', async () => {
    confirmPhoneVerification.mockImplementationOnce(async () => {
      throw new PlatformApiError('bad', 400, 'Bad Request', '{"error":"invalid_code","remainingAttempts":2}');
    });
    render(<I18nProvider><PhoneVerificationCard backend={backend} /></I18nProvider>);
    const input = await screen.findByLabelText('Номер телефона');
    fireEvent.change(input, { target: { value: '+992937380070' } });
    fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
    const codeInput = await screen.findByLabelText('Код из SMS');
    fireEvent.change(codeInput, { target: { value: '000000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
    expect(await screen.findByText(/осталось попыток: 2/i)).toBeInTheDocument();
  });
});
