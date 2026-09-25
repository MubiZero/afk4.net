import { afterEach, describe, expect, it } from 'bun:test';
import { act, render, screen, waitFor } from '@testing-library/react';
import { ShellI18nProvider } from './i18n/ShellI18nProvider';
import { PlayerShellStateNames, ShellBridgeEventTypeNames, ShellBridgeRequestTypeNames } from '@afk4/contracts';
import { App } from './App';
import { devScenarioState } from './host/devHost';
import { installFakeHost } from './test/fakeHost';

function renderShell() {
  return render(
    <ShellI18nProvider initialLocale="ru">
      <App />
    </ShellI18nProvider>
  );
}

afterEach(() => {
  window.chrome = undefined;
  localStorage.clear();
});

describe('оболочка выбирает экран по состоянию ПК', () => {
  it('пока агент молчит — «Подключаемся к ПК»', async () => {
    installFakeHost({ state: null });
    renderShell();
    expect(await screen.findByText('Подключаемся к ПК…')).toBeInTheDocument();
  });

  it('свободный ПК показывает свой номер и что он свободен', async () => {
    installFakeHost({ state: devScenarioState('idle') });
    renderShell();
    expect(await screen.findByText('ПК 07')).toBeInTheDocument();
    expect(screen.getByText('Свободен')).toBeInTheDocument();
    expect(screen.getByText('Общий зал')).toBeInTheDocument();
  });

  it('без связи говорит, что сесть нельзя, и не зовёт к коду', async () => {
    installFakeHost({ state: devScenarioState('offline') });
    renderShell();
    expect(await screen.findByText('Нет связи с клубом')).toBeInTheDocument();
    expect(screen.queryByText('418207')).not.toBeInTheDocument();
    expect(screen.getByText('Нет связи')).toBeInTheDocument();
  });

  it('на обслуживании сжимается в полосу: какой ПК, кто и когда, и «Вернуть в зал»', async () => {
    installFakeHost({ state: devScenarioState('maintenance') });
    renderShell();
    expect(await screen.findByText('ПК 07 на обслуживании')).toBeInTheDocument();
    expect(screen.getByText(/^Включено из Панели AFK4\.net в \d\d:\d\d · Шерзод$/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Вернуть в зал' })).toBeInTheDocument();
    // Полоса — служебная строка над рабочим столом техника: системной строки гостя в ней нет.
    expect(screen.queryByText('Нет связи')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'RU' })).not.toBeInTheDocument();
  });

  it('в сессии показывает остаток и игры клуба', async () => {
    installFakeHost({ state: devScenarioState('session') });
    renderShell();
    expect(await screen.findByText('Counter-Strike 2')).toBeInTheDocument();
    expect(screen.getByTestId('countdown').textContent).toMatch(/^1:3[45]:\d\d$/);
    // Игры, которой на ПК нет, запустить нельзя — и это видно.
    expect(screen.getByText('Нет на этом ПК')).toBeInTheDocument();
  });

  it('без связи посреди сессии говорит, что сессия идёт, а продлить нельзя', async () => {
    installFakeHost({ state: devScenarioState('grace') });
    renderShell();
    expect(await screen.findByText(/Сессия продолжается, но продлить её сейчас нельзя/)).toBeInTheDocument();
  });

  it('в последнюю минуту предупреждает', async () => {
    installFakeHost({ state: devScenarioState('ending') });
    renderShell();
    expect(await screen.findByText('Осталось меньше минуты')).toBeInTheDocument();
  });

  it('новое состояние от агента меняет экран без перезагрузки', async () => {
    const host = installFakeHost({ state: devScenarioState('idle') });
    renderShell();
    await screen.findByText('Свободен');

    await act(async () => {
      host.send(ShellBridgeEventTypeNames.StateChanged, { ...devScenarioState('idle'), state: PlayerShellStateNames.Offline, isOnline: false });
    });

    expect(await screen.findByText('Нет связи с клубом')).toBeInTheDocument();
  });
});

describe('действия идут через хост', () => {
  it('«Играть» просит хост запустить игру', async () => {
    const host = installFakeHost({ state: devScenarioState('session') });
    renderShell();
    const play = await screen.findAllByRole('button', { name: 'Играть' });

    await act(async () => play[0].click());

    await waitFor(() => expect(host.requests).toContainEqual({ type: ShellBridgeRequestTypeNames.AppLaunch, payload: { appId: 'cs2' } }));
  });

  it('игра не запустилась — экран говорит об этом, а не молчит', async () => {
    installFakeHost({
      state: devScenarioState('session'),
      reply: (type) =>
        type === ShellBridgeRequestTypeNames.AppLaunch
          ? { ok: false, error: { code: 'launch_failed', message: 'failed' } }
          : { ok: true, payload: {} }
    });
    renderShell();
    const play = await screen.findAllByRole('button', { name: 'Играть' });

    await act(async () => play[0].click());

    expect(await screen.findByText('Игра не запустилась. Позовите администратора.')).toBeInTheDocument();
  });

  it('«Администратор идёт» — только когда стойка узнала', async () => {
    installFakeHost({
      state: devScenarioState('error'),
      reply: (type) =>
        type === ShellBridgeRequestTypeNames.AssistCall
          ? { ok: false, error: { code: 'platform_unreachable', message: 'unreachable' } }
          : { ok: true, payload: {} }
    });
    renderShell();
    const call = await screen.findByRole('button', { name: 'Позвать администратора' });

    await act(async () => call.click());

    expect(await screen.findByText('Не дозвались до стойки. Подойдите сами.')).toBeInTheDocument();
    expect(screen.queryByText('Администратор идёт')).not.toBeInTheDocument();
  });

  it('язык филиала, пришедший позже, не перебивает выбор человека', async () => {
    const host = installFakeHost({ state: null });
    renderShell();
    const tajik = await screen.findByRole('button', { name: 'Тоҷ' });

    await act(async () => tajik.click());
    await act(async () => {
      host.send(ShellBridgeEventTypeNames.StateChanged, { ...devScenarioState('idle'), locale: 'ru' });
    });

    expect(await screen.findByText('Озод')).toBeInTheDocument();
  });

  it('язык переключается и хост узнаёт выбор', async () => {
    const host = installFakeHost({ state: devScenarioState('idle') });
    renderShell();
    const tajik = await screen.findByRole('button', { name: 'Тоҷ' });

    await act(async () => tajik.click());

    expect(await screen.findByText('Озод')).toBeInTheDocument();
    await waitFor(() => expect(host.requests).toContainEqual({ type: ShellBridgeRequestTypeNames.UiSetLocale, payload: { locale: 'tg' } }));
  });
});

describe('итог после любого конца сессии', () => {
  it('время кончилось само — вошедший видит итог, а не сразу выбор времени', async () => {
    // Итог просит чек у сервера клуба — в тесте его нет, и в сеть тест не ходит.
    const realFetch = globalThis.fetch;
    globalThis.fetch = (async () => new Response('{}', { status: 404 })) as unknown as typeof fetch;
    try {
      const owner = { signedIn: true, displayName: 'Алишер', playerAccountId: '00000000-0000-4000-8000-000000000020' };
      const host = installFakeHost({ state: devScenarioState('session'), auth: owner });
      renderShell();
      await screen.findByRole('button', { name: 'Продлить' });

      await act(async () => host.send(ShellBridgeEventTypeNames.StateChanged, devScenarioState('idle')));

      expect(await screen.findByText('Сессия закончена')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Играть ещё' })).toBeInTheDocument();
    } finally {
      globalThis.fetch = realFetch;
    }
  });
});

