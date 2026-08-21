import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { RejectPanel } from './RejectPanel';

function renderPanel(onSend = mock(), onDismiss = mock()) {
  render(
    <I18nProvider>
      <RejectPanel busy={false} onSend={onSend} onDismiss={onDismiss} />
    </I18nProvider>
  );
  return { onSend, onDismiss };
}

it('отдаёт код причины, а не её текст', () => {
  const { onSend } = renderPanel();
  fireEvent.click(screen.getByLabelText('Зал закрыт на техработы'));
  fireEvent.click(screen.getByRole('button', { name: 'Отправить отказ' }));

  expect(onSend).toHaveBeenCalledWith('maintenance', null);
});

// «Своими словами» без слов — тот же пустой отказ, от которого уходили: игрок снова не узнаёт
// причину, а статистика получает мусорную корзину вместо ответа.
it('не даёт отказать «своими словами» молча', () => {
  const { onSend } = renderPanel();
  fireEvent.click(screen.getByLabelText('Своими словами'));

  const send = screen.getByRole('button', { name: 'Отправить отказ' });
  expect(send).toBeDisabled();
  expect(screen.getByText(/Напишите, что случилось/)).toBeInTheDocument();

  fireEvent.change(screen.getByLabelText('Пояснение для игрока'), { target: { value: 'Свет вырубили' } });
  fireEvent.click(send);
  expect(onSend).toHaveBeenCalledWith('other', 'Свет вырубили');
});

it('пояснение к готовой причине уезжает вместе с кодом', () => {
  const { onSend } = renderPanel();
  fireEvent.change(screen.getByLabelText('Пояснение для игрока'), { target: { value: 'Турнир до полуночи' } });
  fireEvent.click(screen.getByRole('button', { name: 'Отправить отказ' }));

  expect(onSend).toHaveBeenCalledWith('no_seats', 'Турнир до полуночи');
});
