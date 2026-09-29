import { describe, expect, it } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '../i18n/I18nProvider';
import { PlatformApiError } from '../api/platformApi';
import { TwoFactorChallenge } from './TwoFactorChallenge';
import { CLOCK_SKEW_TOLERANCE_MS } from './useChallengeExpiry';

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

  // Раньше опечатка и мёртвая сессия входа отвечали одинаковым голым 401, и обе показывали
  // «неверный код» — хотя опечатку чинит повтор на этом же экране, а мёртвую сессию повтор не
  // починит никогда. Код `two_factor_code_invalid` держит эту ветку на месте.
  it('опечатка в коде остаётся на этом же экране', async () => {
    render(
      <I18nProvider>
        <TwoFactorChallenge
          onSubmit={async () => { throw new PlatformApiError(401, 'x', 'two_factor_code_invalid'); }}
          onCancel={() => {}}
        />
      </I18nProvider>
    );

    await userEvent.type(screen.getByLabelText(/код/i), '000000');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить/i }));

    expect(await screen.findByText(/неверный код/i)).toBeInTheDocument();
  });

  // Сервер отвечает тем же 401, что и на опечатку, но кодом two_factor_challenge_expired — окно
  // входа умерло, и повтор на этом экране ничего не даст. Экран уходит на пароль, а не показывает
  // «неверный код» на форме, которая больше никогда не сработает.
  it('истёкшая на сервере сессия ведёт назад, на пароль, а не показывает «неверный код»', async () => {
    let expiredCalls = 0;
    render(
      <I18nProvider>
        <TwoFactorChallenge
          onSubmit={async () => { throw new PlatformApiError(401, 'x', 'two_factor_challenge_expired'); }}
          onCancel={() => {}}
          onExpired={() => { expiredCalls += 1; }}
        />
      </I18nProvider>
    );

    await userEvent.type(screen.getByLabelText(/код/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить/i }));

    await waitFor(() => expect(expiredCalls).toBe(1));
    expect(screen.queryByText(/неверный код/i)).not.toBeInTheDocument();
  });

  it('блокировка называет время разблокировки, когда сервер его прислал', async () => {
    const lockedUntilUtc = new Date(Date.now() + 12 * 60_000).toISOString();
    render(
      <I18nProvider>
        <TwoFactorChallenge
          onSubmit={async () => {
            throw new PlatformApiError(429, 'x', 'two_factor_locked', null, JSON.stringify({ lockedUntilUtc }));
          }}
          onCancel={() => {}}
        />
      </I18nProvider>
    );

    await userEvent.type(screen.getByLabelText(/код/i), '000000');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить/i }));

    expect(await screen.findByText(/заблокирован до/i)).toBeInTheDocument();
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
          // Deep in the past, well beyond the clock-skew tolerance — fires almost immediately.
          expiresAtUtc={new Date(Date.now() - CLOCK_SKEW_TOLERANCE_MS - 60_000).toISOString()}
          onExpired={() => { expiredCalls += 1; }}
        />
      </I18nProvider>
    );

    await waitFor(() => expect(expiredCalls).toBe(1));
    expect(submitCalls).toBe(0);
  });
});
