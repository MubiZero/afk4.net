import { describe, it, expect, mock } from 'bun:test';
import { HostBridgeRequestError } from './hostBridge';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { pressEnter } from './test/pressEnter';
import { TariffScreen, type TariffClient } from './TariffScreen';

function renderScreen(client: TariffClient, onContinue = mock()) {
  render(
    <I18nProvider>
      <TariffScreen
      stepNumber={1}
        client={client}
        ownerName="Владелец"
        branchName="Главный"
        onContinue={onContinue}
        onBack={mock()}
      />
    </I18nProvider>,
  );
  return onContinue;
}

describe('TariffScreen', () => {
  // Цена вводится в сомони, а уходит в дирамах: 12.5 сомони — это 1250, а не 12.5.
  it('sends the hourly price in minor units', async () => {
    const createTariff = mock().mockResolvedValue({ name: 'Дневной' });
    renderScreen({ createTariff });

    fireEvent.change(screen.getByLabelText('Название тарифа'), { target: { value: 'Дневной' } });
    fireEvent.change(screen.getByLabelText('Цена за час, сомони'), { target: { value: '12.5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));

    await waitFor(() => expect(createTariff).toHaveBeenCalledWith('Дневной', 1250));
    expect(screen.getByText(/Тариф «Дневной» создан/)).toBeTruthy();
  });

  // Свой `Math.round(x * 100)` округлял 1.005 вниз: в двоичной дроби это 1.00499999…, и мастер
  // расходился с остальными экранами на дирам. Общий перевод из @afk4/money округляет как человек.
  it('округляет цену так же, как остальные экраны', async () => {
    const createTariff = mock().mockResolvedValue({ name: 'Дневной' });
    renderScreen({ createTariff });

    fireEvent.change(screen.getByLabelText('Название тарифа'), { target: { value: 'Дневной' } });
    fireEvent.change(screen.getByLabelText('Цена за час, сомони'), { target: { value: '1.005' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));

    await waitFor(() => expect(createTariff).toHaveBeenCalledWith('Дневной', 101));
  });

  it('does not create a tariff without a price', () => {
    const createTariff = mock();
    renderScreen({ createTariff });

    fireEvent.change(screen.getByLabelText('Цена за час, сомони'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));

    expect(createTariff).not.toHaveBeenCalled();
  });

  it('can be passed without a tariff', () => {
    const onContinue = renderScreen({ createTariff: mock() });

    fireEvent.click(screen.getByRole('button', { name: 'Пропустить' }));

    expect(onContinue).toHaveBeenCalled();
  });

  it('explains a failure instead of pretending the tariff exists', async () => {
    renderScreen({ createTariff: mock().mockRejectedValue(new Error('network')) });

    fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));

    await waitFor(() => expect(screen.getByText(/Не удалось создать тариф/)).toBeTruthy());
  });

  // Самый частый отказ на этом шаге: клуб заводит «Стандарт» второй раз. Раньше человек читал
  // «не удалось создать тариф. Проверьте цену» и правил цену, которая была ни при чём.
  it('называет занятое имя тарифа, а не отправляет проверять цену', async () => {
    renderScreen({
      createTariff: mock().mockRejectedValue(
        new HostBridgeRequestError('Platform API returned 400', 'tariff_name_taken', null),
      ),
    });

    fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));

    await waitFor(() => expect(screen.getByText(/Тариф с таким именем в клубе уже есть/)).toBeTruthy());
  });

});

// Enter в поле формы отправляет её — так человек привык везде, и так уже работают вход и
// экран устройства. Здесь поля лежали вне формы, и Enter не делал ничего.
describe('TariffScreen · Enter', () => {
  for (const label of ['Название тарифа', 'Цена за час, сомони']) {
    it(`Enter в поле «${label}» создаёт тариф`, async () => {
      const createTariff = mock().mockResolvedValue({ name: 'Дневной' });
      renderScreen({ createTariff });
      fireEvent.change(screen.getByLabelText('Название тарифа'), { target: { value: 'Дневной' } });

      pressEnter(screen.getByLabelText(label));

      await waitFor(() => expect(createTariff).toHaveBeenCalledTimes(1));
      expect(createTariff).toHaveBeenCalledWith('Дневной', 1000);
    });
  }

  it('Enter при неактивной кнопке не создаёт тариф', () => {
    const createTariff = mock();
    renderScreen({ createTariff });
    fireEvent.change(screen.getByLabelText('Цена за час, сомони'), { target: { value: '' } });

    pressEnter(screen.getByLabelText('Цена за час, сомони'));
    // И мимо кнопки: сама отправка тоже не пускает тариф без цены.
    fireEvent.submit(screen.getByLabelText('Цена за час, сомони').closest('form') as HTMLFormElement);

    expect(createTariff).not.toHaveBeenCalled();
  });

  it('двойной Enter не создаёт тариф дважды', () => {
    const createTariff = mock(() => new Promise<never>(() => {}));
    renderScreen({ createTariff });

    pressEnter(screen.getByLabelText('Название тарифа'));
    pressEnter(screen.getByLabelText('Название тарифа'));
    fireEvent.submit(screen.getByLabelText('Название тарифа').closest('form') as HTMLFormElement);

    expect(createTariff).toHaveBeenCalledTimes(1);
  });
});
