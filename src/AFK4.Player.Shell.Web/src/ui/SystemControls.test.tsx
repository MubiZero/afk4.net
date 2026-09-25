import { afterEach, describe, expect, it } from 'bun:test';
import { act, cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { ShellI18nProvider } from '../i18n/ShellI18nProvider';
import { ShellBridgeRequestTypeNames, type ShellSystemStateDto } from '@afk4/contracts';
import { installFakeHost } from '../test/fakeHost';
import { SystemControls } from './SystemControls';

afterEach(cleanup);

const system: ShellSystemStateDto = { volume: 60, micMuted: false, layout: 'RU' };

function renderControls(value: ShellSystemStateDto | null) {
  return render(
    <ShellI18nProvider initialLocale="ru">
      <SystemControls system={value} />
    </ShellI18nProvider>
  );
}

describe('звук, микрофон и раскладка', () => {
  it('без слова хоста кнопок нет', () => {
    installFakeHost({ state: null });
    const { container } = renderControls(null);

    expect(container.querySelector('.system-controls')).toBeNull();
  });

  it('чего у ПК нет, того и не показывает', () => {
    installFakeHost({ state: null });
    renderControls({ volume: 40, micMuted: null, layout: null });

    expect(screen.getByRole('slider', { name: 'Громкость' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Микрофон/ })).toBeNull();
    expect(screen.queryByRole('button', { name: /Раскладка/ })).toBeNull();
  });

  it('микрофон выключается сразу, не дожидаясь хоста', async () => {
    const host = installFakeHost({ state: null });
    renderControls(system);

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Микрофон включён' })));

    expect(screen.getByRole('button', { name: 'Микрофон выключен' })).toHaveAttribute('aria-pressed', 'true');
    expect(host.requests).toContainEqual({ type: ShellBridgeRequestTypeNames.SystemSetMicMuted, payload: { micMuted: true } });
  });

  it('раскладка идёт по кругу: русская, английская, таджикская', async () => {
    const host = installFakeHost({ state: null });
    renderControls(system);

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Раскладка клавиатуры: RU' })));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Раскладка клавиатуры: EN' })));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Раскладка клавиатуры: TG' })));

    expect(host.requests.map((request) => request.payload)).toEqual([{ layout: 'EN' }, { layout: 'TG' }, { layout: 'RU' }]);
  });

  it('громкость уходит хосту числом', async () => {
    const host = installFakeHost({ state: null });
    renderControls(system);

    await act(async () => fireEvent.change(screen.getByRole('slider', { name: 'Громкость' }), { target: { value: '25' } }));

    expect(host.requests).toContainEqual({ type: ShellBridgeRequestTypeNames.SystemSetVolume, payload: { volume: 25 } });
  });

  it('отказ Windows возвращает как было и говорит об этом', async () => {
    installFakeHost({
      state: null,
      reply: () => ({ ok: false, error: { code: 'system_unavailable', message: 'no device' } })
    });
    renderControls(system);

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Микрофон включён' })));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Микрофон включён' })).toHaveAttribute('aria-pressed', 'false'));
    expect(screen.getByText('Windows не дала это поменять')).toBeInTheDocument();
  });
});
