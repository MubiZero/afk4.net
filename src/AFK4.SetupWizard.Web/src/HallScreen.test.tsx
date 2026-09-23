import { describe, it, expect, mock } from 'bun:test';
import { HostBridgeRequestError } from './hostBridge';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { pressEnter } from './test/pressEnter';
import { HallScreen, type HallClient } from './HallScreen';

const ZONES = [
  { zoneId: 'z-1', name: 'Общий зал', sortOrder: 0 },
  { zoneId: 'z-2', name: 'VIP', sortOrder: 1 },
];

function renderScreen(client: HallClient, onContinue = mock(), zones = ZONES) {
  render(
    <I18nProvider>
      <HallScreen
      stepNumber={1}
        client={client}
        zones={zones}
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

  // Шаг можно пройти мимо — но чем это обернётся, человек должен узнать здесь, а не открыв
  // пустую карту зала перед первым гостем.
  it('говорит, чем обернётся пустой зал', () => {
    renderScreen({ createSeats: mock() });

    expect(screen.getByText(/карта зала будет пустой/i)).toBeTruthy();
  });

  // Предел проверяет хост; экран не должен отправлять заведомо отвергнутый запрос и показывать
  // на него общее «не удалось».
  it('не отправляет количество больше предела и называет предел', () => {
    const createSeats = mock();
    renderScreen({ createSeats });

    fireEvent.change(screen.getByLabelText(/сколько мест/i), { target: { value: '600' } });

    expect(screen.getByText(/до 60 мест/i)).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: /завести места/i }));
    expect(createSeats).not.toHaveBeenCalled();
  });

  // Места заводятся по одному: обрыв на середине оставляет часть заведёнными, и человек боится
  // нажать ещё раз. Сервер место с тем же именем не задваивает — об этом и говорим.
  it('после отказа говорит, что повтор безопасен', async () => {
    renderScreen({ createSeats: mock().mockRejectedValue(new Error('network')) });

    fireEvent.click(screen.getByRole('button', { name: /завести места/i }));

    await waitFor(() => expect(screen.getByText(/не задвоятся/i)).toBeTruthy());
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

  // Места заводятся в зале. Если зала нет, кнопка не нажимается — и раньше причину человек не
  // узнавал ниоткуда: та же ситуация на экране устройства объяснена словами, а здесь молчала.
  it('называет причину, когда заводить места негде, и оставляет выход', () => {
    renderScreen({ createSeat: mock() } as unknown as HallClient, mock(), []);

    const alert = screen.getByRole('alert');
    expect(alert.textContent).toContain('ещё нет ни одного зала');
    expect(alert.textContent).toContain('пропустите шаг');
  });

  // Работа этого экрана — завести места, а не уйти с него. Пока не заведено ни одного, вес
  // главного действия принадлежит «Завести места»; после — кнопке «Дальше».
  it('держит главное действие на работе экрана, а не на пропуске', async () => {
    const created: unknown[] = [];
    const client = { createSeat: mock(async (...args: unknown[]) => { created.push(args); return { seatId: 's-1' }; }) };
    renderScreen(client as unknown as HallClient);

    expect(screen.getByRole('button', { name: /Завести места/ }).className).toContain('ui-btn--primary');
    expect(screen.getByRole('button', { name: /Пропустить/ }).className).not.toContain('ui-btn--primary');
  });
});

// Enter в поле формы отправляет её — так человек привык везде, и так уже работают вход и
// экран устройства. Здесь поля лежали вне формы, и Enter не делал ничего.
describe('HallScreen · Enter', () => {
  for (const label of ['Как называть места', 'Сколько мест']) {
    it(`Enter в поле «${label}» заводит места`, async () => {
      const createSeats = mock().mockResolvedValue({ names: ['ПК-1', 'ПК-2'] });
      renderScreen({ createSeats });
      fireEvent.change(screen.getByLabelText('Сколько мест'), { target: { value: '2' } });

      pressEnter(screen.getByLabelText(label));

      await waitFor(() => expect(createSeats).toHaveBeenCalledTimes(1));
      expect(createSeats).toHaveBeenCalledWith('z-1', 'ПК', 2);
    });
  }

  it('Enter при неактивной кнопке не заводит мест', () => {
    const createSeats = mock();
    renderScreen({ createSeats });
    fireEvent.change(screen.getByLabelText('Сколько мест'), { target: { value: '61' } });

    pressEnter(screen.getByLabelText('Сколько мест'));
    // И мимо кнопки: сама отправка тоже не пускает число сверх предела.
    fireEvent.submit(screen.getByLabelText('Сколько мест').closest('form') as HTMLFormElement);

    expect(createSeats).not.toHaveBeenCalled();
  });

  it('двойной Enter не заводит места дважды', () => {
    const createSeats = mock(() => new Promise<never>(() => {}));
    renderScreen({ createSeats });

    pressEnter(screen.getByLabelText('Сколько мест'));
    pressEnter(screen.getByLabelText('Сколько мест'));
    fireEvent.submit(screen.getByLabelText('Сколько мест').closest('form') as HTMLFormElement);

    expect(createSeats).toHaveBeenCalledTimes(1);
  });
});
