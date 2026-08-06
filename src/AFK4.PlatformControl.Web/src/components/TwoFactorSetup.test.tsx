import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '../i18n/I18nProvider';
import { PlatformApiError } from '../api/platformApi';
import { TwoFactorSetup, type TwoFactorSetupClient } from './TwoFactorSetup';
import { CLOCK_SKEW_TOLERANCE_MS } from './useChallengeExpiry';
import type { PlatformAdminSession } from '../auth/tokenStore';

afterEach(cleanup);

function buildSession(): PlatformAdminSession {
  return {
    platformAdminId: 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee',
    userName: 'admin@platform.test',
    displayName: 'Platform Owner',
    roles: ['platform_admin'],
    permissions: [],
    accessToken: 'access-token',
    accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
    refreshToken: 'refresh-token',
    refreshTokenExpiresAtUtc: '2030-02-01T00:00:00Z'
  };
}

function buildClient(overrides: Partial<TwoFactorSetupClient> = {}): TwoFactorSetupClient {
  return {
    beginSetup: overrides.beginSetup ?? (async () => ({
      secret: 'ABCD1234EFGH5678',
      otpAuthUri: 'otpauth://totp/AFK4:admin@platform.test?secret=ABCD1234EFGH5678&issuer=AFK4'
    })),
    completeSetup: overrides.completeSetup ?? (async () => ({
      session: buildSession(),
      recoveryCodes: ['code-1', 'code-2', 'code-3']
    }))
  };
}

function setClipboard(writeText: (text: string) => Promise<void>) {
  Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
}

describe('TwoFactorSetup', () => {
  it('счастливый путь: QR + секрет, подтверждение кода, показ кодов восстановления', async () => {
    const completeSetup = mock(async () => ({ session: buildSession(), recoveryCodes: ['aaaa-1111', 'bbbb-2222'] }));
    const client = buildClient({ completeSetup });

    render(
      <I18nProvider>
        <TwoFactorSetup client={client} challengeToken="chal-1" onComplete={() => {}} onCancel={() => {}} />
      </I18nProvider>
    );

    // Secret is shown as text next to the QR — an authenticator on the same device can't scan its
    // own screen, so this is the only way to configure it there.
    expect(await screen.findByText('ABCD1234EFGH5678')).toBeInTheDocument();
    await screen.findByRole('img');

    await userEvent.type(screen.getByLabelText(/код из приложения/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить и включить/i }));

    expect(completeSetup).toHaveBeenCalledWith('chal-1', '123456');
    expect(await screen.findByText('aaaa-1111')).toBeInTheDocument();
    expect(screen.getByText('bbbb-2222')).toBeInTheDocument();
    expect(screen.getByText(/показываются только один раз/i)).toBeInTheDocument();
  });

  it('гейт подтверждения: нельзя продолжить, пока не отмечено «сохранил коды»', async () => {
    const client = buildClient();

    render(
      <I18nProvider>
        <TwoFactorSetup client={client} challengeToken="chal-1" onComplete={() => {}} onCancel={() => {}} />
      </I18nProvider>
    );

    await userEvent.type(await screen.findByLabelText(/код из приложения/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить и включить/i }));

    const continueButton = await screen.findByRole('button', { name: /продолжить/i });
    expect(continueButton).toBeDisabled();

    await userEvent.click(screen.getByRole('checkbox'));
    expect(continueButton).toBeEnabled();

    let completed: PlatformAdminSession | null = null;
    // onComplete only fires once the checkbox is confirmed — re-render with a spy to verify the
    // click actually reaches the caller once enabled.
    cleanup();
    render(
      <I18nProvider>
        <TwoFactorSetup
          client={client}
          challengeToken="chal-1"
          onComplete={session => { completed = session; }}
          onCancel={() => {}}
        />
      </I18nProvider>
    );
    await userEvent.type(await screen.findByLabelText(/код из приложения/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить и включить/i }));
    await userEvent.click(await screen.findByRole('checkbox'));
    await userEvent.click(screen.getByRole('button', { name: /продолжить/i }));

    expect(completed).not.toBeNull();
  });

  it('показывает понятную ошибку при блокировке (429) на подтверждении кода', async () => {
    const client = buildClient({
      completeSetup: async () => { throw new PlatformApiError(429, 'locked'); }
    });

    render(
      <I18nProvider>
        <TwoFactorSetup client={client} challengeToken="chal-1" onComplete={() => {}} onCancel={() => {}} />
      </I18nProvider>
    );

    await userEvent.type(await screen.findByLabelText(/код из приложения/i), '000000');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить и включить/i }));

    expect(await screen.findByText(/слишком много попыток/i)).toBeInTheDocument();
  });

  // Находка 1: the challenge window can die while still on the QR screen, before any code is
  // ever submitted — the countdown must bounce the person out on its own.
  it('вызывает onExpired по истечении окна ещё на экране QR', async () => {
    let expiredCalls = 0;
    const client = buildClient();

    render(
      <I18nProvider>
        <TwoFactorSetup
          client={client}
          challengeToken="chal-1"
          // Deep in the past, well beyond the clock-skew tolerance — fires almost immediately.
          expiresAtUtc={new Date(Date.now() - CLOCK_SKEW_TOLERANCE_MS - 60_000).toISOString()}
          onExpired={() => { expiredCalls += 1; }}
          onComplete={() => {}}
          onCancel={() => {}}
        />
      </I18nProvider>
    );

    await screen.findByText('ABCD1234EFGH5678');
    await waitFor(() => expect(expiredCalls).toBe(1));
  });

  // The countdown must stop once recovery codes are on screen (the challenge is already redeemed
  // by then) — that guarantee is exercised directly against the hook in
  // useChallengeExpiry.test.tsx, decoupled from this screen's own async network/typing timing.

  // Находка 2: a false "Copied" is worse than no button at all — the person would close this
  // screen believing the codes are saved when they aren't.
  it('копирование кодов: показывает успех только после реального успеха', async () => {
    const writeText = mock(async () => {});
    setClipboard(writeText);
    const client = buildClient();

    render(
      <I18nProvider>
        <TwoFactorSetup client={client} challengeToken="chal-1" onComplete={() => {}} onCancel={() => {}} />
      </I18nProvider>
    );

    await userEvent.type(await screen.findByLabelText(/код из приложения/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить и включить/i }));
    await screen.findByText('code-1');

    await userEvent.click(screen.getByRole('button', { name: /скопировать все коды/i }));

    expect(writeText).toHaveBeenCalledWith('code-1\ncode-2\ncode-3');
    expect(await screen.findByRole('button', { name: /скопировано/i })).toBeInTheDocument();
    expect(screen.queryByText(/скопировать не удалось/i)).not.toBeInTheDocument();
  });

  it('копирование кодов: при отказе буфера обмена честно сообщает об ошибке, а не «скопировано»', async () => {
    setClipboard(async () => { throw new Error('denied'); });
    const client = buildClient();

    render(
      <I18nProvider>
        <TwoFactorSetup client={client} challengeToken="chal-1" onComplete={() => {}} onCancel={() => {}} />
      </I18nProvider>
    );

    await userEvent.type(await screen.findByLabelText(/код из приложения/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить и включить/i }));
    await screen.findByText('code-1');

    await userEvent.click(screen.getByRole('button', { name: /скопировать все коды/i }));

    expect(await screen.findByText(/скопировать не удалось/i)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /скопировано/i })).not.toBeInTheDocument();
  });
});
