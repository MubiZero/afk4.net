import { describe, expect, it, mock } from 'bun:test';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import type { PcControlActionId, PcControlActionOptions } from '../operatorTypes';
import { PcCommands } from './PcCommands';

function seat(overrides: Partial<SeatSummary> = {}): SeatSummary {
  return {
    id: 'seat-1', zone: 'Зал A', name: 'ПК 07', tone: 'ready', stateLabel: 'Свободен', player: '', remaining: '',
    device: '', command: '', app: '', deviceId: 'dev-1', deviceName: 'PC-007', isDeviceOnline: true, isDeviceLocked: true,
    activeSessionId: null, hasActiveSession: false, ...overrides
  };
}

function renderCommands(
  value: SeatSummary,
  onPcControlAction = mock(async (_seat: SeatSummary, _action: PcControlActionId, _options?: PcControlActionOptions) => ({ detail: 'Перезагрузка: отдана ПК' }))
) {
  render(
    <I18nProvider>
      <PcCommands seat={value} access={{ canDispatch: true, canMaintain: true }} onPcControlAction={onPcControlAction} />
    </I18nProvider>
  );
  return onPcControlAction;
}

describe('команды ПК', () => {
  it('перезагрузка спрашивает «точно?» и уходит только после подтверждения', async () => {
    const action = renderCommands(seat());

    fireEvent.click(screen.getByRole('button', { name: 'Перезагрузить' }));
    expect(action).not.toHaveBeenCalled();
    const dialog = screen.getByRole('alertdialog', { name: 'Перезагрузить ПК 07?' });
    expect(dialog).toHaveTextContent('через 10 секунд');

    await act(async () => fireEvent.click(screen.getAllByRole('button', { name: 'Перезагрузить' }).at(-1)!));

    expect(action).toHaveBeenCalledWith(expect.objectContaining({ id: 'seat-1' }), 'reboot', undefined);
    expect(await screen.findByText('Перезагрузка: отдана ПК')).toBeInTheDocument();
  });

  it('отмена ничего не отправляет', () => {
    const action = renderCommands(seat());

    fireEvent.click(screen.getByRole('button', { name: 'Выключить' }));
    fireEvent.click(screen.getByRole('button', { name: 'Отмена' }));

    expect(action).not.toHaveBeenCalled();
    expect(screen.queryByRole('alertdialog')).toBeNull();
  });

  it('сообщение нельзя отправить пустым, а с текстом оно уходит с текстом', async () => {
    const action = renderCommands(seat());

    fireEvent.click(screen.getByRole('button', { name: 'Сообщение игроку' }));
    const send = screen.getByRole('button', { name: 'Отправить' });
    expect(send).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/Текст сообщения/), { target: { value: '  Через пять минут закрываемся  ' } });
    await act(async () => fireEvent.click(send));

    expect(action).toHaveBeenCalledWith(expect.anything(), 'message', { text: 'Через пять минут закрываемся' });
  });

  it('вернуть в зал — сразу, без вопросов: это безопасно', async () => {
    const action = renderCommands(seat({ tone: 'service', maintenanceSinceUtc: '2026-09-25T10:00:00Z' }));

    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Вернуть в зал' })));

    expect(action).toHaveBeenCalledWith(expect.anything(), 'maintenance-off', undefined);
  });

  it('за ПК играют — закрытые кнопки объясняют почему', () => {
    renderCommands(seat({ tone: 'active', activeSessionId: 's-1', hasActiveSession: true }));

    expect(screen.getByRole('button', { name: 'Перезагрузить' })).toBeDisabled();
    expect(screen.getByText(/можно только свободный ПК/)).toBeInTheDocument();
  });

  it('отказ сервера виден словами', async () => {
    const action = mock(async () => {
      throw new Error('ПК не отвечает');
    });
    renderCommands(seat(), action);

    fireEvent.click(screen.getByRole('button', { name: 'Выйти из аккаунта игрока' }));
    await act(async () => fireEvent.click(screen.getByRole('button', { name: 'Выйти из аккаунта' })));

    expect(await screen.findByRole('alert')).toHaveTextContent('ПК не отвечает');
  });
});
