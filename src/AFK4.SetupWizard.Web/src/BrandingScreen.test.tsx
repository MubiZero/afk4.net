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

describe('BrandingScreen', () => {
  // На телефонном интернете блок логотипов стоял пустым, пока идёт запрос, — раздел читался как
  // сломанный. Спиннер с задержкой: на быстрой сети он не мелькнёт.
  it('пока грузятся готовые логотипы, говорит об этом', async () => {
    let release: (value: { presets: typeof PRESETS }) => void = () => {};
    const presets = mock(() => new Promise<{ presets: typeof PRESETS }>((resolve) => { release = resolve; }));
    renderScreen({ presets, save: mock(), uploadLogo: mock() });

    expect(await screen.findByText(/Загружаем готовые логотипы/i)).toBeTruthy();

    release({ presets: PRESETS });
    await waitFor(() => expect(screen.queryByText(/Загружаем готовые логотипы/i)).toBeNull());
  });

  // Пресетов может не быть вовсе — шаг всё равно рабочий, и это надо сказать, а не оставить
  // пустое место.
  it('отсутствие готовых логотипов объясняет словами', async () => {
    renderScreen({
      presets: mock().mockRejectedValue(new Error('network')),
      save: mock(),
      uploadLogo: mock(),
    });

    expect(await screen.findByText(/Готовые логотипы сейчас недоступны/i)).toBeTruthy();
  });

  it('saves the chosen preset and colour', async () => {
    const save = mock().mockResolvedValue({ saved: true });
    const onContinue = renderScreen({ presets: mock().mockResolvedValue({ presets: PRESETS }), save, uploadLogo: mock() });

    // Кнопка-переключатель, а не radio: нажатие по выбранному снимает выбор, и «без логотипа» —
    // законное состояние. Подпись человеческая: диктор читал служебное имя файла «bolt».
    fireEvent.click(await screen.findByRole('button', { name: 'Вариант 1' }));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить и дальше' }));

    await waitFor(() => expect(onContinue).toHaveBeenCalled());
    expect(save).toHaveBeenCalledWith(PRESETS[0].url, '#C8FF00');
  });

  // Клуб должен открыться и без логотипа: оформление ставится позже в панели управляющего.
  it('lets the step be skipped without saving anything', async () => {
    const save = mock();
    const onContinue = renderScreen({ presets: mock().mockResolvedValue({ presets: PRESETS }), save, uploadLogo: mock() });

    fireEvent.click(await screen.findByRole('button', { name: 'Пропустить' }));

    expect(onContinue).toHaveBeenCalled();
    expect(save).not.toHaveBeenCalled();
  });

  // Пресеты приходят с платформы: если их не отдали, шаг обязан остаться проходимым.
  it('still works when presets cannot be loaded', async () => {
    renderScreen({ presets: mock().mockRejectedValue(new Error('offline')), save: mock() , uploadLogo: mock()});

    expect(await screen.findByRole('button', { name: 'Пропустить' })).toBeTruthy();
  });

  // Отказ сохранения не должен выглядеть как «всё прошло»: шаг остаётся на месте с объяснением.
  it('keeps the step open when saving fails', async () => {
    const onContinue = renderScreen({
      presets: mock().mockResolvedValue({ presets: PRESETS }),
      save: mock().mockRejectedValue(new Error('network')), uploadLogo: mock(),
    });

    fireEvent.click(await screen.findByRole('button', { name: 'Сохранить и дальше' }));

    await waitFor(() => expect(screen.getByText(/Не удалось сохранить оформление/)).toBeTruthy());
    expect(onContinue).not.toHaveBeenCalled();
  });
});

// Свой логотип выбирается нативным окном: веб получает только адрес уже загруженного файла.
it('uses the uploaded logo when one is chosen', async () => {
  const save = mock().mockResolvedValue({ saved: true });
  const uploadLogo = mock().mockResolvedValue({ logoUrl: 'https://cdn.afk4.net/logo.png' });
  render(
    <I18nProvider>
      <BrandingScreen
      stepNumber={1}
        client={{ presets: mock().mockResolvedValue({ presets: PRESETS }), save, uploadLogo }}
        ownerName="Владелец"
        branchName="Главный"
        onContinue={mock()}
        onBack={mock()}
      />
    </I18nProvider>,
  );

  fireEvent.click(await screen.findByRole('button', { name: 'Загрузить свой логотип' }));
  await waitFor(() => expect(screen.getByAltText('Загруженный логотип')).toBeTruthy());

  fireEvent.click(screen.getByRole('button', { name: 'Сохранить и дальше' }));
  await waitFor(() => expect(save).toHaveBeenCalledWith('https://cdn.afk4.net/logo.png', expect.any(String)));
});

// Закрытое окно выбора — не ошибка: экран должен остаться как был.
it('stays unchanged when the file dialog is dismissed', async () => {
  const uploadLogo = mock().mockResolvedValue({ logoUrl: null });
  render(
    <I18nProvider>
      <BrandingScreen
      stepNumber={1}
        client={{ presets: mock().mockResolvedValue({ presets: PRESETS }), save: mock(), uploadLogo }}
        ownerName="Владелец"
        branchName="Главный"
        onContinue={mock()}
        onBack={mock()}
      />
    </I18nProvider>,
  );

  fireEvent.click(await screen.findByRole('button', { name: 'Загрузить свой логотип' }));

  await waitFor(() => expect(uploadLogo).toHaveBeenCalled());
  expect(screen.queryByAltText('Загруженный логотип')).toBeNull();
});
