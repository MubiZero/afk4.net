import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { SignInScreen } from './SignInScreen';
import type { OperatorConfig } from './operatorTypes';

// CRIT-1: the production browser build never carries organizationId/branchId in config by design
// (browserConfigFromEnv in operatorConfig.ts) — the two-step sign-in (login → server resolves the
// club, 409 ChooseClubError → pick club) resolves org itself. Only the WPF/kiosk host needs a
// paired connection before it lets the operator try a login.
const browserConfig: OperatorConfig = {
  runtime: 'browser',
  shellMode: 'web',
  platformBaseUrl: 'https://platform.example/',
  currencyCode: 'TJS'
};

const webviewConfigWithoutConnection: OperatorConfig = {
  runtime: 'webview2',
  shellMode: 'vite-dist',
  platformBaseUrl: 'https://platform.example/',
  currencyCode: 'TJS'
};

function renderSignIn(config: OperatorConfig, onSignIn = mock(async () => {})) {
  render(
    <I18nProvider>
      <SignInScreen
        config={config}
        authStatus="signed-out"
        hostError={null}
        chooseClub={null}
        onSignIn={onSignIn}
        onChooseClub={mock(async () => {})}
        onCancelChooseClub={mock(() => {})}
        onForgotPassword={mock(() => {})}
        onAcceptInvite={mock(() => {})}
      />
    </I18nProvider>
  );
  return onSignIn;
}

async function submitCredentials(login: string, password: string) {
  fireEvent.click(screen.getByRole('button', { name: 'Вход по логину или почте' }));
  fireEvent.change(screen.getByLabelText(/логин или email/i), { target: { value: login } });
  fireEvent.change(screen.getByLabelText('Пароль'), { target: { value: password } });
  fireEvent.click(screen.getByRole('button', { name: 'Войти' }));
}

describe('SignInScreen', () => {
  afterEach(() => {
    cleanup();
  });

  it('submits sign-in in the browser runtime even without organizationId in config', async () => {
    const onSignIn = renderSignIn(browserConfig);

    await submitCredentials('cashier', 'password');

    await waitFor(() => expect(onSignIn).toHaveBeenCalledWith('cashier', 'password'));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('still blocks sign-in on the WPF/kiosk host when the device has no paired connection', async () => {
    const onSignIn = renderSignIn(webviewConfigWithoutConnection);

    await submitCredentials('cashier', 'password');

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(onSignIn).not.toHaveBeenCalled();
  });
});
