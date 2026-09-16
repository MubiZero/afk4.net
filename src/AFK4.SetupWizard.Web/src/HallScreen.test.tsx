import { describe, it, expect, mock } from 'bun:test';
import { HostBridgeRequestError } from './hostBridge';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { HallScreen, type HallClient } from './HallScreen';

const ZONES = [
  { zoneId: 'z-1', name: 'Общий зал', sortOrder: 0 },
  { zoneId: 'z-2', name: 'VIP', sortOrder: 1 },
];

function renderScreen(client: HallClient, onContinue = mock()) {
  render(
    <I18nProvider>
      <HallScreen
      stepNumber={1}
        client={client}
        zones={ZONES}
        ownerName="Владелец"
        branchName="Главный"
        onContinue={onContinue}
        onBack={mock()}
      />
    </I18nProvider>,
  );
  return onContinue;
}

describe('HallScreen', () => {
  it('creates the requested number of seats in the chosen zone', async () => {
    const createSeats = mock().mockResolvedValue({ names: ['ПК-1', 'ПК-2', 'ПК-3'] });
    renderScreen({ createSeats });

    fireEvent.change(screen.getByLabelText('Зона'), { target: { value: 'z-2' } });
    fireEvent.change(screen.getByLabelText('Сколько мест'), { target: { value: '3' } });
    fireEvent.click(screen.getByRole('button', { name: 'Завести места' }));

    await waitFor(() => expect(screen.getByText('Заведено 3 места')).toBeTruthy());
    expect(createSeats).toHaveBeenCalledWith('z-2', 'ПК', 3);
  });

  // Ноль и мусор в поле не должны уходить на сервер циклом создания.
  it('does not create anything for a non-positive count', () => {
    const createSeats = mock();
    renderScreen({ createSeats });

    fireEvent.change(screen.getByLabelText('Сколько мест'), { target: { value: '0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Завести места' }));

    expect(createSeats).not.toHaveBeenCalled();
  });

  it('can be passed without creating seats', () => {
    const onContinue = renderScreen({ createSeats: mock() });

    fireEvent.click(screen.getByRole('button', { name: 'Пропустить' }));

    expect(onContinue).toHaveBeenCalled();
  });

  it('explains a failure instead of pretending the seats exist', async () => {
    renderScreen({ createSeats: mock().mockRejectedValue(new Error('network')) });

    fireEvent.click(screen.getByRole('button', { name: 'Завести места' }));

    await waitFor(() => expect(screen.getByText(/Не удалось завести места/)).toBeTruthy());
  });

  // Обрыв связи с хостом — это не «сервер отказал завести места»: чинится перезапуском мастера.
  it('обрыв моста называет своими словами', async () => {
    renderScreen({
      createSeats: mock().mockRejectedValue(new HostBridgeRequestError('boom', 'host_timeout', null)),
    });

    fireEvent.click(screen.getByRole('button', { name: 'Завести места' }));

    await waitFor(() => expect(screen.getByText(/Локальный агент не ответил вовремя/)).toBeTruthy());
  });

});
