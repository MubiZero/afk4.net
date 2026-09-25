import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { StaffSignInStepName } from '@afk4/contracts';
import { SignInScreen, type SignInActions } from './SignInScreen';
import { StaffAuthApiError } from './auth/staffAuthApi';
import type { OperatorConfig } from './operatorTypes';

// Браузерная Панель клуба не знает по построению — его находит сервер по номеру или логину.
// Подключение к клубу нужно только Панели, стоящей в клубе (WPF).
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

const PHONE = '992937380070';

function fakeActions(step: StaffSignInStepName = 'pin', overrides: Partial<SignInActions> = {}) {
  return {
    nextStep: mock(async (_phone: string) => step),
    signInByPhone: mock(async (_phone: string, _pin: string) => {}),
    signInByLogin: mock(async (_login: string, _pin: string) => {}),
    checkInviteCode: mock(async (_phone: string, _code: string) => {}),
    acceptInvite: mock(async (_phone: string, _code: string, _pin: string) => {}),
    ...overrides
  };
}

function renderSignIn(actions: SignInActions, config: OperatorConfig = browserConfig) {
  const onForgotPassword = mock((_phone: string | null) => {});
  render(
    <I18nProvider>
      <SignInScreen
        config={config}
        authStatus="signed-out"
        hostError={null}
        chooseClub={null}
        actions={actions}
        onChooseClub={mock(async () => {})}
        onCancelChooseClub={mock(() => {})}
        onForgotPassword={onForgotPassword}
      />
    </I18nProvider>
  );
  return { onForgotPassword };
}

function enterPhone(local = '937380070') {
  fireEvent.change(screen.getByLabelText(/Номер телефона/), { target: { value: local } });
  fireEvent.click(screen.getByRole('button', { name: 'Дальше' }));
}

async function typeInto(label: string, digits: string) {
  const field = await screen.findByLabelText(label);
  fireEvent.change(field, { target: { value: digits } });
}

describe('SignInScreen', () => {
  afterEach(() => {
    cleanup();
  });

  it('asks the number first, then signs in with the PIN the moment the sixth digit lands', async () => {
    const actions = fakeActions('pin');
    renderSignIn(actions);

    enterPhone();
    await typeInto('Введите ПИН-код', '246813');

    await waitFor(() => expect(actions.signInByPhone).toHaveBeenCalledWith(PHONE, '246813'));
    expect(actions.nextStep).toHaveBeenCalledWith(PHONE);
    // Номер виден на втором шаге целиком — опечатка в нём видна сразу.
    expect(screen.getByRole('button', { name: /\+992 93 738 00 70/ })).toBeInTheDocument();
  });

  it('names a wrong PIN and clears the cells for the next try', async () => {
    const actions = fakeActions('pin', {
      signInByPhone: mock(async () => { throw new StaffAuthApiError(401, null); })
    });
    renderSignIn(actions);

    enterPhone();
    await typeInto('Введите ПИН-код', '111111');

    expect(await screen.findByText('Неверный ПИН-код.')).toBeInTheDocument();
    expect((screen.getByLabelText('Введите ПИН-код') as HTMLInputElement).value).toBe('');
  });

  it('tells a locked-out person to wait instead of retyping', async () => {
    const actions = fakeActions('pin', {
      signInByPhone: mock(async () => {
        throw new StaffAuthApiError(429, { code: 'too_many_password_attempts' });
      })
    });
    renderSignIn(actions);

    enterPhone();
    await typeInto('Введите ПИН-код', '111111');

    expect(await screen.findByText(/Подождите пятнадцать минут/)).toBeInTheDocument();
  });

  it('walks a new staff member from the manager’s code to their own PIN and signs them in', async () => {
    const actions = fakeActions('invite-code');
    renderSignIn(actions);

    enterPhone();
    await typeInto('Код первого входа', '123456');
    await waitFor(() => expect(actions.checkInviteCode).toHaveBeenCalledWith(PHONE, '123456'));
    await typeInto('Придумайте ПИН-код', '654321');
    await typeInto('Повторите ПИН-код', '654321');

    await waitFor(() => expect(actions.acceptInvite).toHaveBeenCalledWith(PHONE, '123456', '654321'));
    expect(actions.signInByPhone).not.toHaveBeenCalled();
  });

  it('sends a mistyped repeat back to choosing the PIN', async () => {
    const actions = fakeActions('invite-code');
    renderSignIn(actions);

    enterPhone();
    await typeInto('Код первого входа', '123456');
    await typeInto('Придумайте ПИН-код', '654321');
    await typeInto('Повторите ПИН-код', '654320');

    expect(await screen.findByText('ПИН-коды не совпали. Придумайте заново.')).toBeInTheDocument();
    expect(screen.getByLabelText('Придумайте ПИН-код')).toBeInTheDocument();
    expect(actions.acceptInvite).not.toHaveBeenCalled();
  });

  it('says how many tries a wrong first sign-in code leaves', async () => {
    const actions = fakeActions('invite-code', {
      checkInviteCode: mock(async () => {
        throw new StaffAuthApiError(400, { error: 'invalid_code', remainingAttempts: 2 });
      })
    });
    renderSignIn(actions);

    enterPhone();
    await typeInto('Код первого входа', '000000');

    expect(await screen.findByText('Код не подошёл. Осталось попыток: 2.')).toBeInTheDocument();
    expect(screen.queryByLabelText('Придумайте ПИН-код')).not.toBeInTheDocument();
  });

  it('sends a number no club added to the manager, without a PIN step', async () => {
    const actions = fakeActions('unknown');
    renderSignIn(actions);

    enterPhone();

    expect(await screen.findByText(/не заведён ни в одном клубе/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Номер телефона/)).toBeInTheDocument();
  });

  it('goes back to the number with it still typed', async () => {
    renderSignIn(fakeActions('pin'));

    enterPhone();
    fireEvent.click(await screen.findByRole('button', { name: /Изменить/ }));

    expect((screen.getByLabelText(/Номер телефона/) as HTMLInputElement).value).toBe('93 738 00 70');
  });

  it('opens PIN recovery on the number already typed', async () => {
    const { onForgotPassword } = renderSignIn(fakeActions('pin'));

    enterPhone();
    fireEvent.click(await screen.findByRole('button', { name: 'Забыли ПИН-код?' }));

    expect(onForgotPassword).toHaveBeenCalledWith('93 738 00 70');
  });

  it('signs in by login in the browser, where the Panel knows no club', async () => {
    const actions = fakeActions();
    renderSignIn(actions);

    fireEvent.click(screen.getByRole('button', { name: 'Вход по логину или почте' }));
    fireEvent.change(screen.getByLabelText(/логин или email/i), { target: { value: 'owner' } });
    fireEvent.click(screen.getByRole('button', { name: 'Дальше' }));
    await typeInto('Введите ПИН-код', '246813');

    await waitFor(() => expect(actions.signInByLogin).toHaveBeenCalledWith('owner', '246813'));
    expect(actions.nextStep).not.toHaveBeenCalled();
  });

  it('keeps a club PC without a club connection from trying to sign in', async () => {
    const actions = fakeActions();
    renderSignIn(actions, webviewConfigWithoutConnection);

    enterPhone();

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(actions.nextStep).not.toHaveBeenCalled();
  });
});
