import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { LoadingCards, ErrorState, EmptyState, ForbiddenState, PartialFailure } from './states';

it('renders the requested number of loading skeletons', async () => {
  render(<LoadingCards count={3} />);
  expect(await screen.findAllByTestId('loading-skeleton')).toHaveLength(3);
});

// Быстрый ответ не должен мигать ожиданием: пятая доля секунды — это граница, за которой
// человек замечает паузу, а до неё скелетон только дёргает экран.
it('holds the skeleton back for an instant so quick answers do not flash', () => {
  render(<LoadingCards count={3} />);
  expect(screen.queryAllByTestId('loading-skeleton')).toHaveLength(0);
});

it('renders an error message and calls retry', () => {
  const retry = mock();
  render(<ErrorState message="Не удалось загрузить" retryLabel="Повторить" onRetry={retry} />);
  fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
  expect(retry).toHaveBeenCalledTimes(1);
});

// Пустой список без следующего шага — это «Нет приглашений», и человек не знает, что делать
// дальше. Решение обязано быть принято: либо кнопка, либо названная причина молчания. Строка
// ниже — не тест поведения, а проверка типа: `tsc` в сборке падает, если голый EmptyState снова
// начнёт компилироваться.
// @ts-expect-error — `next` обязателен
void (<EmptyState message="Пусто" />);

it('renders a useful first-use empty state with its action', () => {
  const create = mock();
  render(<EmptyState title="Создайте первую организацию" message="После этого здесь появятся клубы." next={{ label: 'Создать организацию', onClick: create }} />);
  expect(screen.getByRole('heading', { name: 'Создайте первую организацию' })).toBeVisible();
  fireEvent.click(screen.getByRole('button', { name: 'Создать организацию' }));
  expect(create).toHaveBeenCalledTimes(1);
});

it('says who holds the right instead of drawing a button the viewer cannot use', () => {
  render(<EmptyState message="Тарифов пока нет." next={{ noPermission: 'Это может сотрудник платформы с правом «Заводить и менять тарифы».' }} />);
  expect(screen.getByText('Тарифов пока нет.')).toBeInTheDocument();
  expect(screen.getByText('Это может сотрудник платформы с правом «Заводить и менять тарифы».')).toBeInTheDocument();
  expect(screen.queryByRole('button')).toBeNull();
});

it('stays quiet on purpose when emptiness is good news', () => {
  render(<EmptyState message="Открытых проблем нет — платформа работает штатно." next="calm" />);
  expect(screen.getByText('Открытых проблем нет — платформа работает штатно.')).toBeInTheDocument();
  expect(screen.queryByRole('button')).toBeNull();
});

it('renders forbidden content as an alert with a safe destination', () => {
  const goBack = mock();
  render(<ForbiddenState title="Нет доступа" message="Эта страница недоступна вашей роли." actionLabel="Открыть обзор" onAction={goBack} />);
  fireEvent.click(screen.getByRole('button', { name: 'Открыть обзор' }));
  expect(screen.getByRole('alert')).toHaveTextContent('Эта страница недоступна вашей роли.');
  expect(goBack).toHaveBeenCalledTimes(1);
});

it('keeps partial failure non-blocking and retryable', () => {
  const retry = mock();
  render(<PartialFailure title="Счета временно недоступны" retryLabel="Повторить" onRetry={retry} />);
  expect(screen.getByRole('status')).toHaveTextContent('Счета временно недоступны');
  fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
  expect(retry).toHaveBeenCalledTimes(1);
});
