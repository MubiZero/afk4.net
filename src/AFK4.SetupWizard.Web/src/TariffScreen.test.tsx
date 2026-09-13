import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
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
});
