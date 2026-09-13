import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { BrandingScreen, type BrandingClient } from './BrandingScreen';

const PRESETS = [
  { id: 'bolt', url: 'https://api.afk4.net/branding/presets/bolt.svg' },
  { id: 'flame', url: 'https://api.afk4.net/branding/presets/flame.svg' },
];

function renderScreen(client: BrandingClient, onContinue = mock()) {
  render(
    <I18nProvider>
      <BrandingScreen
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

describe('BrandingScreen', () => {
  it('saves the chosen preset and colour', async () => {
    const save = mock().mockResolvedValue({ saved: true });
    const onContinue = renderScreen({ presets: mock().mockResolvedValue({ presets: PRESETS }), save });

    fireEvent.click(await screen.findByRole('radio', { name: 'bolt' }));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить и дальше' }));

    await waitFor(() => expect(onContinue).toHaveBeenCalled());
    expect(save).toHaveBeenCalledWith(PRESETS[0].url, '#C8FF00');
  });

  // Клуб должен открыться и без логотипа: оформление ставится позже в панели управляющего.
  it('lets the step be skipped without saving anything', async () => {
    const save = mock();
    const onContinue = renderScreen({ presets: mock().mockResolvedValue({ presets: PRESETS }), save });

    fireEvent.click(await screen.findByRole('button', { name: 'Пропустить' }));

    expect(onContinue).toHaveBeenCalled();
    expect(save).not.toHaveBeenCalled();
  });

  // Пресеты приходят с платформы: если их не отдали, шаг обязан остаться проходимым.
  it('still works when presets cannot be loaded', async () => {
    renderScreen({ presets: mock().mockRejectedValue(new Error('offline')), save: mock() });

    expect(await screen.findByRole('button', { name: 'Пропустить' })).toBeTruthy();
  });

  // Отказ сохранения не должен выглядеть как «всё прошло»: шаг остаётся на месте с объяснением.
  it('keeps the step open when saving fails', async () => {
    const onContinue = renderScreen({
      presets: mock().mockResolvedValue({ presets: PRESETS }),
      save: mock().mockRejectedValue(new Error('network')),
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Сохранить и дальше' }));

    await waitFor(() => expect(screen.getByText(/Не удалось сохранить оформление/)).toBeTruthy());
    expect(onContinue).not.toHaveBeenCalled();
  });
});
