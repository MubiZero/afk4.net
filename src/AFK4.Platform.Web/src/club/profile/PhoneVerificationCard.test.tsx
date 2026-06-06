import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformApi';
import { PhoneVerificationCard } from './PhoneVerificationCard';

function fakeClient(overrides: Partial<{
  getStaffPhone: () => Promise<{ phone: string | null; phoneVerifiedAtUtc: string | null }>;
  startPhoneVerification: (phone: string) => Promise<{ expiresInSeconds: number; resendAfterSeconds: number }>;
  confirmPhoneVerification: (code: string) => Promise<{ phone: string }>;
}> = {}) {
  return {
    getStaffPhone: mock(overrides.getStaffPhone ?? (async () => ({ phone: null, phoneVerifiedAtUtc: null }))),
    startPhoneVerification: mock(overrides.startPhoneVerification ?? (async () => ({ expiresInSeconds: 300, resendAfterSeconds: 60 }))),
    confirmPhoneVerification: mock(overrides.confirmPhoneVerification ?? (async () => ({ phone: '+992937380070' })))
  };
}

function renderCard(client: ReturnType<typeof fakeClient>) {
  return render(
    <I18nProvider><ToastProvider>
      <PhoneVerificationCard client={client as never} />
    </ToastProvider></I18nProvider>
  );
}

it('lets an unverified staff member send a code and confirm it', async () => {
  const client = fakeClient();
  renderCard(client);
  const input = await screen.findByLabelText('Номер телефона');
  fireEvent.change(input, { target: { value: '+992937380070' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  await waitFor(() => expect(client.startPhoneVerification).toHaveBeenCalledWith('+992937380070'));

  const codeInput = await screen.findByLabelText('Код из SMS');
  fireEvent.change(codeInput, { target: { value: '123456' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  await waitFor(() => expect(client.confirmPhoneVerification).toHaveBeenCalledWith('123456'));
  expect(await screen.findByText('подтверждён')).toBeInTheDocument();
});

it('shows a verified phone on load', async () => {
  const client = fakeClient({ getStaffPhone: async () => ({ phone: '+992937380070', phoneVerifiedAtUtc: '2026-06-06T00:00:00Z' }) });
  renderCard(client);
  expect(await screen.findByText('+992937380070')).toBeInTheDocument();
  expect(screen.getByText('подтверждён')).toBeInTheDocument();
});

it('maps a backend error code to a localized message', async () => {
  const client = fakeClient({
    startPhoneVerification: async () => { throw new PlatformApiError(400, 'invalid_phone', 'invalid_phone'); }
  });
  renderCard(client);
  const input = await screen.findByLabelText('Номер телефона');
  fireEvent.change(input, { target: { value: 'abc' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  expect(await screen.findByText(/Проверьте номер/)).toBeInTheDocument();
});

it('shows an error state with retry when loading fails', async () => {
  const client = fakeClient({ getStaffPhone: async () => { throw new PlatformApiError(500, 'boom', null); } });
  renderCard(client);
  expect(await screen.findByRole('button', { name: /Повторить/i })).toBeInTheDocument();
});

it('keeps the code field and shows an inline error on a bad confirm code', async () => {
  const client = fakeClient({
    confirmPhoneVerification: async () => { throw new PlatformApiError(400, 'invalid_code', 'invalid_code'); }
  });
  renderCard(client);
  const input = await screen.findByLabelText('Номер телефона');
  fireEvent.change(input, { target: { value: '+992937380070' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  const codeInput = await screen.findByLabelText('Код из SMS');
  fireEvent.change(codeInput, { target: { value: '000000' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  expect(await screen.findByText(/Неверный код/)).toBeInTheDocument();
  expect(screen.getByLabelText('Код из SMS')).toBeInTheDocument();
});

it('surfaces the remaining-attempts count on a bad confirm code', async () => {
  const client = fakeClient({
    confirmPhoneVerification: async () => { throw new PlatformApiError(400, 'invalid_code', 'invalid_code', 2); }
  });
  renderCard(client);
  const input = await screen.findByLabelText('Номер телефона');
  fireEvent.change(input, { target: { value: '+992937380070' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  const codeInput = await screen.findByLabelText('Код из SMS');
  fireEvent.change(codeInput, { target: { value: '000000' } });
  fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
  expect(await screen.findByText(/Осталось попыток: 2/)).toBeInTheDocument();
});
