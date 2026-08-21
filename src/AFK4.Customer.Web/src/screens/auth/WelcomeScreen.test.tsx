import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WelcomeScreen } from './WelcomeScreen';
import type { PlayerApiClient } from '@/api/playerApi';

function renderScreen(api: PlayerApiClient, onDone = mock()) {
  render(<I18nProvider><WelcomeScreen api={api} onDone={onDone} onLocaleChange={() => {}} /></I18nProvider>);
  return onDone;
}

it('спрашивает имя и язык и сохраняет их за человеком', async () => {
  const updateMyProfile = mock().mockResolvedValue({ displayName: 'Фаррух', preferredLocale: 'ru' });
  const onDone = renderScreen({ updateMyProfile } as unknown as PlayerApiClient);
  fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Фаррух' } });
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));
  await waitFor(() => expect(updateMyProfile).toHaveBeenCalledWith({ displayName: 'Фаррух', preferredLocale: 'ru' }));
  expect(onDone).toHaveBeenCalledWith('Фаррух');
});

it('не отправляет пустое имя на сервер', async () => {
  const updateMyProfile = mock();
  renderScreen({ updateMyProfile } as unknown as PlayerApiClient);
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Введите имя');
  expect(updateMyProfile).not.toHaveBeenCalled();
});

it('объясняет, зачем клубу имя', () => {
  renderScreen({ updateMyProfile: mock() } as unknown as PlayerApiClient);
  expect(screen.getByText(/сажать вас за ПК/)).toBeInTheDocument();
});

it('сообщает о неудаче сохранения, а не молча стоит', async () => {
  const updateMyProfile = mock().mockRejectedValue(new Error('nope'));
  renderScreen({ updateMyProfile } as unknown as PlayerApiClient);
  fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Фаррух' } });
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('Не удалось сохранить');
});
