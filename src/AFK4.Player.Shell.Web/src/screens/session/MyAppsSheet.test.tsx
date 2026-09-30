import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ShellBridgeErrorCodeNames, ShellBridgeRequestTypeNames, type LaunchedAppDto } from '@afk4/contracts';
import { ShellI18nProvider } from '../../i18n/ShellI18nProvider';
import { installFakeHost } from '../../test/fakeHost';
import { MyAppsSheet } from './MyAppsSheet';

afterEach(cleanup);

const cs2: LaunchedAppDto = { launchId: '00000000-0000-4000-8000-000000000030', appId: 'cs2', displayName: 'Counter-Strike 2', processIds: [100, 101] };
const dota: LaunchedAppDto = { launchId: '00000000-0000-4000-8000-000000000031', appId: 'dota2', displayName: 'Dota 2', processIds: [200] };

function renderSheet(apps: LaunchedAppDto[], onClose = () => {}) {
  return render(
    <ShellI18nProvider initialLocale="ru">
      <MyAppsSheet apps={apps} library={[]} onClose={onClose} />
    </ShellI18nProvider>
  );
}

describe('«Мои приложения»', () => {
  it('показывает только запущенное игроком: по «Вернуться» и «Закрыть» на каждую игру', () => {
    installFakeHost({ state: null });
    renderSheet([cs2, dota]);

    expect(screen.getByText('Counter-Strike 2')).toBeInTheDocument();
    expect(screen.getByText('Dota 2')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /^Вернуться:/ })).toHaveLength(2);
    expect(screen.getAllByRole('button', { name: /^Закрыть:/ })).toHaveLength(2);
  });

  it('пустой список говорит словами, а не пустым листом', () => {
    installFakeHost({ state: null });
    renderSheet([]);

    expect(screen.getByText(/Пока ничего не запущено/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Вернуться:/ })).toBeNull();
  });

  it('«Вернуться» просит хост вывести окно именно этой копии и закрывает лист', async () => {
    const host = installFakeHost({ state: null });
    let closed = 0;
    renderSheet([cs2, dota], () => { closed += 1; });

    fireEvent.click(screen.getByRole('button', { name: 'Вернуться: Dota 2' }));

    await waitFor(() => expect(closed).toBe(1));
    expect(host.requests.find((request) => request.type === ShellBridgeRequestTypeNames.AppFocus)?.payload)
      .toEqual({ launchId: dota.launchId });
  });

  it('«Закрыть» просит агента закрыть эту копию; строка блокируется, пока идёт закрытие', async () => {
    const host = installFakeHost({ state: null });
    renderSheet([cs2, dota]);

    fireEvent.click(screen.getByRole('button', { name: 'Закрыть: Counter-Strike 2' }));

    await waitFor(() => expect(host.requests.some((request) => request.type === ShellBridgeRequestTypeNames.AppClose)).toBe(true));
    expect(host.requests.find((request) => request.type === ShellBridgeRequestTypeNames.AppClose)?.payload)
      .toEqual({ launchId: cs2.launchId });
    expect(screen.getByRole('button', { name: 'Закрыть: Counter-Strike 2' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Закрыть: Dota 2' })).toBeEnabled();
  });

  it('у игры ещё нет окна — так и говорит, лист остаётся', async () => {
    installFakeHost({
      state: null,
      reply: () => ({ ok: false, error: { code: ShellBridgeErrorCodeNames.AppWindowNotFound, message: 'no window' } })
    });
    let closed = 0;
    renderSheet([cs2], () => { closed += 1; });

    fireEvent.click(screen.getByRole('button', { name: 'Вернуться: Counter-Strike 2' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Окно игры ещё не открылось');
    expect(closed).toBe(0);
  });

  it('отказ закрытия возвращает кнопку и называет причину', async () => {
    installFakeHost({
      state: null,
      reply: () => ({ ok: false, error: { code: 'app_not_running', message: 'gone' } })
    });
    renderSheet([cs2]);

    fireEvent.click(screen.getByRole('button', { name: 'Закрыть: Counter-Strike 2' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('уже закрыто');
    expect(screen.getByRole('button', { name: 'Закрыть: Counter-Strike 2' })).toBeEnabled();
  });
});
