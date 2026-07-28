import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { LoadingCards, ErrorState, EmptyState } from './states';

it('renders the requested number of loading skeletons', () => {
  render(<LoadingCards count={3} />);
  expect(screen.getAllByTestId('loading-skeleton')).toHaveLength(3);
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
