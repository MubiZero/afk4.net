import { afterEach, describe, expect, it } from 'bun:test';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ShellBridgeEventTypeNames, ShellBridgeRequestTypeNames } from '@afk4/contracts';
import { App } from '../App';
import { devScenarioState } from '../host/devHost';
import { installFakeHost } from '../test/fakeHost';

function renderShell() {
  return render(
    <I18nProvider initialLocale="ru">
      <App />
    </I18nProvider>
  );
}

afterEach(() => {
  window.chrome = undefined;
  localStorage.clear();
});

async function approach(options: Parameters<typeof installFakeHost>[0] = { state: devScenarioState('idle') }) {
  const host = installFakeHost(options);
  renderShell();
  await screen.findByText('Свободен');
  await act(async () => host.send(ShellBridgeEventTypeNames.InputActivity, {}));
  await screen.findByRole('dialog');
  return host;
}

function fillIn(phone: string, pin: string) {
  fireEvent.change(screen.getByLabelText('Номер телефона'), { target: { value: phone } });
  fireEvent.change(screen.getByLabelText('ПИН-код'), { target: { value: pin } });
}

describe('окно входа на свободном ПК', () => {
  it('открывается, когда к ПК подошли, и показывает QR с кодом посадки', async () => {
    await approach();

    expect(screen.getByRole('img', { name: 'Или с телефона' })).toBeInTheDocument();
    expect(screen.getByText(/введите код 418 207/)).toBeInTheDocument();
  });

  it('закрывается по Esc', async () => {
    await approach();

    await act(async () => fireEvent.keyDown(window, { key: 'Escape' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(screen.getByText('Подвиньте мышь или нажмите клавишу, чтобы сесть')).toBeInTheDocument();
  });

  it('закрывается, когда от ПК отошли', async () => {
    const host = await approach();

    await act(async () => host.send(ShellBridgeEventTypeNames.InputIdle, {}));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('«Войти» ждёт полного номера и шести цифр ПИН-кода', async () => {
    await approach();
    const submit = screen.getByRole('button', { name: 'Войти' });

    fillIn('93 555 12', '1234');
    expect(submit).toBeDisabled();

    fillIn('93 555 12 40', '12ab3456789');
    // Буквы и лишние цифры поле просто не принимает.
    expect(screen.getByLabelText('ПИН-код')).toHaveValue('123456');
    expect(submit).toBeEnabled();
  });

  it('вход идёт через хост, а экран меняется только по его подтверждению', async () => {
    const host = await approach();
    fillIn('93 555 12 40', '123456');

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Войти' })));

    await waitFor(() =>
      expect(host.requests).toContainEqual({
        type: ShellBridgeRequestTypeNames.AuthSignIn,
        payload: { phone: '+992935551240', pin: '123456' }
      }));
    // Ответ пришёл, а события о входе ещё нет — экран не делает вид, что уже вошли.
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await act(async () =>
      host.send(ShellBridgeEventTypeNames.AuthChanged, { signedIn: true, displayName: 'Алишер', playerAccountId: null }));

    expect(await screen.findByText('Здравствуйте, Алишер')).toBeInTheDocument();
  });

  it('неверный ПИН-код — понятный отказ, и ПИН-код стирается', async () => {
    await approach({
      state: devScenarioState('idle'),
      reply: (type) =>
        type === ShellBridgeRequestTypeNames.AuthSignIn
          ? { ok: false, error: { code: 'sign_in_refused', message: 'refused' } }
          : { ok: true, payload: {} }
    });
    fillIn('93 555 12 40', '000000');

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Войти' })));

    expect(await screen.findByText('Номер или ПИН-код не подошли')).toBeInTheDocument();
    expect(screen.getByLabelText('ПИН-код')).toHaveValue('');
  });

  it('много неудач с этого ПК — говорит, когда можно снова', async () => {
    await approach({
      state: devScenarioState('idle'),
      reply: (type) =>
        type === ShellBridgeRequestTypeNames.AuthSignIn
          ? { ok: false, error: { code: 'too_many_attempts', message: 'later' } }
          : { ok: true, payload: {} }
    });
    fillIn('93 555 12 40', '123456');

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Войти' })));

    expect(await screen.findByText(/Попробуйте через 15 минут/)).toBeInTheDocument();
  });

  it('агент молчит — просит позвать администратора', async () => {
    await approach({
      state: devScenarioState('idle'),
      reply: (type) =>
        type === ShellBridgeRequestTypeNames.AuthSignIn
          ? { ok: false, error: { code: 'agent_unavailable', message: 'down' } }
          : { ok: true, payload: {} }
    });
    fillIn('93 555 12 40', '123456');

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Войти' })));

    expect(await screen.findByText('ПК не отвечает. Позовите администратора.')).toBeInTheDocument();
  });

  // ПК на месте, оборвалась дорога до клуба: «ПК не отвечает» отправило бы администратора чинить не то.
  it('нет связи с клубом — так и говорит', async () => {
    await approach({
      state: devScenarioState('idle'),
      reply: (type) =>
        type === ShellBridgeRequestTypeNames.AuthSignIn
          ? { ok: false, error: { code: 'platform_unreachable', message: 'offline' } }
          : { ok: true, payload: {} }
    });
    fillIn('93 555 12 40', '123456');

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Войти' })));

    expect(await screen.findByText(/Нет связи с клубом/)).toBeInTheDocument();
  });
});

describe('вошедший', () => {
  it('может выйти', async () => {
    const host = installFakeHost({
      state: devScenarioState('idle'),
      auth: { signedIn: true, displayName: 'Алишер', playerAccountId: null }
    });
    renderShell();

    const signOut = await screen.findByRole('button', { name: 'Выйти' });
    await act(async () => fireEvent.click(signOut));

    await waitFor(() => expect(host.requests.map((request) => request.type)).toContain(ShellBridgeRequestTypeNames.AuthSignOut));
  });
});
