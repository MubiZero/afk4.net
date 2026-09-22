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

it('renders an empty message', () => {
  render(<EmptyState message="Пусто" />);
  expect(screen.getByText('Пусто')).toBeInTheDocument();
});

it('renders a useful first-use empty state with its action', () => {
  render(<EmptyState title="Создайте первую организацию" message="После этого здесь появятся клубы." action={<button>Создать организацию</button>} />);
  expect(screen.getByRole('heading', { name: 'Создайте первую организацию' })).toBeVisible();
  expect(screen.getByRole('button', { name: 'Создать организацию' })).toBeEnabled();
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
