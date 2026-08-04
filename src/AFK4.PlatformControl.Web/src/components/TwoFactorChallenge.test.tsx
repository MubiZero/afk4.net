import { describe, expect, it } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '../i18n/I18nProvider';
import { PlatformApiError } from '../api/platformApi';
import { TwoFactorChallenge } from './TwoFactorChallenge';

describe('TwoFactorChallenge', () => {
  it('отправляет введённый код и сообщает об успехе', async () => {
    let submitted = '';
    render(
      <I18nProvider>
        <TwoFactorChallenge
          onSubmit={async code => { submitted = code; }}
          onCancel={() => {}}
        />
      </I18nProvider>
    );

    await userEvent.type(screen.getByLabelText(/код/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить/i }));

    expect(submitted).toBe('123456');
  });

  it('показывает понятную ошибку при блокировке', async () => {
    render(
      <I18nProvider>
        <TwoFactorChallenge
          onSubmit={async () => { throw new PlatformApiError(429, 'locked'); }}
          onCancel={() => {}}
        />
      </I18nProvider>
    );

    await userEvent.type(screen.getByLabelText(/код/i), '000000');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить/i }));

    expect(await screen.findByText(/слишком много попыток/i)).toBeInTheDocument();
  });

  // Находка 1: expiry is a distinct outcome from a wrong code, never surfaced through onSubmit —
  // it fires on its own once the server-issued window elapses, even without any user action.
  it('вызывает onExpired по истечении окна, не дожидаясь отправки кода', async () => {
    let expiredCalls = 0;
    let submitCalls = 0;
    render(
      <I18nProvider>
        <TwoFactorChallenge
          onSubmit={async () => { submitCalls += 1; }}
          onCancel={() => {}}
          expiresAtUtc={new Date(Date.now() + 5).toISOString()}
          onExpired={() => { expiredCalls += 1; }}
        />
      </I18nProvider>
    );

    await waitFor(() => expect(expiredCalls).toBe(1));
    expect(submitCalls).toBe(0);
  });
});
