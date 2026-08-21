import { it, expect, mock } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '@/components/ui/toast';
import { PinPanel, PIN_LENGTH_BOUNDS } from './PinPanel';
import type { PlayerApiClient } from '@/api/playerApi';

function renderPanel(api: PlayerApiClient, pinSet: boolean, onPinSet = mock()) {
  render(
    <I18nProvider>
      <ToastProvider autoDismissMs={1000}>
        <PinPanel api={api} pinSet={pinSet} onPinSet={onPinSet} />
      </ToastProvider>
    </I18nProvider>
  );
  return onPinSet;
}

// PIN — не пароль от приложения, а способ сесть за ПК без администратора. Панель, которая этого
// не говорит, оставляет человека гадать, зачем от него хотят ещё четыре цифры.
it('объясняет, зачем нужен PIN, человеческим языком', () => {
  renderPanel({ setPin: mock() } as unknown as PlayerApiClient, false);
  expect(screen.getByText(/сядете сами, без администратора/)).toBeInTheDocument();
  expect(screen.getByText('PIN пока не задан')).toBeInTheDocument();
});

it('зовёт задать PIN, пока его нет', () => {
  renderPanel({ setPin: mock() } as unknown as PlayerApiClient, false);
  expect(screen.getByRole('button', { name: 'Задать PIN' })).toBeInTheDocument();
});

it('зовёт сменить PIN, когда он уже есть', () => {
  renderPanel({ setPin: mock() } as unknown as PlayerApiClient, true);
  expect(screen.getByRole('button', { name: 'Сменить PIN' })).toBeInTheDocument();
  expect(screen.getByText('PIN задан')).toBeInTheDocument();
});

it('сохраняет новый PIN и сообщает об этом', async () => {
  const setPin = mock().mockResolvedValue(undefined);
  const onPinSet = renderPanel({ setPin } as unknown as PlayerApiClient, false);
  fireEvent.click(screen.getByRole('button', { name: 'Задать PIN' }));
  fireEvent.change(await screen.findByLabelText('Новый PIN'), { target: { value: '4821' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  await waitFor(() => expect(setPin).toHaveBeenCalledWith('4821'));
  expect(await screen.findByText('PIN сохранён')).toBeInTheDocument();
  expect(onPinSet).toHaveBeenCalled();
});

// Форма PIN известна и клиенту, и серверу — гонять человека до сервера за отказом, который видно
// на месте, значит тратить его время впустую.
it('не отправляет на сервер слишком короткий PIN', async () => {
  const setPin = mock();
  renderPanel({ setPin } as unknown as PlayerApiClient, false);
  fireEvent.click(screen.getByRole('button', { name: 'Задать PIN' }));
  fireEvent.change(await screen.findByLabelText('Новый PIN'), { target: { value: '12' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('PIN — это от 4 до 8 цифр');
  expect(setPin).not.toHaveBeenCalled();
});

it('не пускает буквы в PIN', async () => {
  const setPin = mock();
  renderPanel({ setPin } as unknown as PlayerApiClient, false);
  fireEvent.click(screen.getByRole('button', { name: 'Задать PIN' }));
  fireEvent.change(await screen.findByLabelText('Новый PIN'), { target: { value: '12a4' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  expect(await screen.findByRole('alert')).toHaveTextContent('PIN — это от 4 до 8 цифр');
  expect(setPin).not.toHaveBeenCalled();
});

// Старый PIN не спрашивается намеренно: потребовать его значило бы запереть выход ровно тому,
// кто его забыл. Об этом надо сказать вслух, иначе человек ищет несуществующее поле.
it('обещает, что старый PIN не понадобится', async () => {
  renderPanel({ setPin: mock() } as unknown as PlayerApiClient, true);
  fireEvent.click(screen.getByRole('button', { name: 'Сменить PIN' }));
  expect(await screen.findByText(/Старый PIN не спросим/)).toBeInTheDocument();
});

it('сообщает о неудаче сохранения', async () => {
  const setPin = mock().mockRejectedValue(new Error('nope'));
  renderPanel({ setPin } as unknown as PlayerApiClient, false);
  fireEvent.click(screen.getByRole('button', { name: 'Задать PIN' }));
  fireEvent.change(await screen.findByLabelText('Новый PIN'), { target: { value: '4821' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
  expect(await screen.findByText('Не удалось сохранить PIN')).toBeInTheDocument();
});

// Форму PIN знают двое: этот экран и сервер. Разъехавшись, они дадут либо отказ там, где сервер
// принял бы, либо поход по сети за отказом, который был виден на месте. Источник — контракт.
it('форма PIN на экране совпадает с контрактом сервера', () => {
  const contract = readFileSync(
    join(import.meta.dir, '..', '..', '..', '..', 'AFK4.Shared.Contracts', 'Identity', 'PinContracts.cs'),
    'utf8'
  );
  const min = /MinLength\s*=\s*(\d+)/.exec(contract)?.[1];
  const max = /MaxLength\s*=\s*(\d+)/.exec(contract)?.[1];
  expect({ min, max }).toEqual({ min: '4', max: '8' });
  expect(PIN_LENGTH_BOUNDS).toEqual([Number(min), Number(max)]);
});
