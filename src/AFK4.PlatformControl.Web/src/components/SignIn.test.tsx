import { describe, expect, it } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '../i18n/I18nProvider';
import { ThemeProvider } from '../theme/ThemeProvider';
import { SignIn } from './SignIn';
import { CLOCK_SKEW_TOLERANCE_MS } from './useChallengeExpiry';
import type { PlatformApiClient } from '../api/platformApi';

type FakeClient = Pick<PlatformApiClient, 'signIn'> & { twoFactor: { verify: (challengeToken: string, code: string) => Promise<unknown> } };

// Окно, которое доживёт до экрана кода и истечёт уже на нём: хук добавляет к сроку допуск на
// рассинхрон часов, поэтому его здесь заранее вычитают. Срок, мёртвый уже в момент выдачи, этот
// сценарий проверить не может — экран кода при нём не успевает появиться вовсе, и «вернулись с
// шага кода» становится гонкой, а не проверкой.
function expiresInAfterTolerance(remainingMs: number): string {
  return new Date(Date.now() + remainingMs - CLOCK_SKEW_TOLERANCE_MS).toISOString();
}

function buildClient(overrides: Partial<FakeClient> = {}): PlatformApiClient {
  const client: FakeClient = {
    signIn: overrides.signIn ?? (async () => { throw new Error('signIn not mocked'); }),
    twoFactor: overrides.twoFactor ?? { verify: async () => { throw new Error('verify not mocked'); } }
  };
  return client as unknown as PlatformApiClient;
}

describe('SignIn — истечение окна подтверждения (Находка 1)', () => {
  it('переключается на экран кода, когда 2FA уже настроена', async () => {
    const client = buildClient({
      signIn: async () => ({
        kind: 'challenge',
        challengeToken: 'chal-1',
        twoFactorConfigured: true,
        expiresAtUtc: new Date(Date.now() + 60_000).toISOString()
      })
    });

    render(<ThemeProvider><I18nProvider><SignIn client={client} onSignedIn={() => {}} /></I18nProvider></ThemeProvider>);

    await userEvent.type(screen.getByLabelText('Логин или email'), 'admin');
    await userEvent.type(screen.getByLabelText('Пароль'), 'password');
    await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

    expect(await screen.findByLabelText(/код/i)).toBeInTheDocument();
  });

  it('по истечении окна возвращает на шаг пароля с честным сообщением, а не «неверный код»', async () => {
    const client = buildClient({
      signIn: async () => ({
        kind: 'challenge',
        challengeToken: 'chal-1',
        twoFactorConfigured: true,
        // Человек успевает увидеть экран кода, и окно истекает под ним — ровно то, ради чего
        // хук написан. Двух настоящих минут тест не ждёт, но и полумиллисекундной щели не
        // оставляет: полсекунды переживают медленный раннер, а ожидание возврата ниже само
        // подстраивается под него запасом по таймауту.
        expiresAtUtc: expiresInAfterTolerance(500)
      })
    });

    render(<ThemeProvider><I18nProvider><SignIn client={client} onSignedIn={() => {}} /></I18nProvider></ThemeProvider>);

    await userEvent.type(screen.getByLabelText('Логин или email'), 'admin');
    await userEvent.type(screen.getByLabelText('Пароль'), 'password');
    await userEvent.click(screen.getByRole('button', { name: 'Войти' }));

    await screen.findByLabelText(/код/i);

    // Возврат на шаг пароля — с честной причиной, а не с «неверный код»: код человек, может, и
    // набрал правильный, просто окно под ним умерло.
    await waitFor(
      () => expect(screen.getByLabelText('Логин или email')).toBeInTheDocument(),
      { timeout: 3_000 });
    expect(screen.getByText('Время на подтверждение истекло. Войдите заново.')).toBeInTheDocument();
    expect(screen.queryByText(/неверный код/i)).not.toBeInTheDocument();
  });
});
