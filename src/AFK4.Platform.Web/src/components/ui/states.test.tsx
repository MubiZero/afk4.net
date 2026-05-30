import { it, expect } from 'vitest';
import { vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LoadingCards, ErrorState, EmptyState } from './states';

it('renders the requested number of loading skeletons', () => {
  render(<LoadingCards count={3} />);
  expect(screen.getAllByTestId('loading-skeleton')).toHaveLength(3);
});

it('renders an error message and calls retry', () => {
  const retry = vi.fn();
  render(<ErrorState message="Не удалось загрузить" retryLabel="Повторить" onRetry={retry} />);
  fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
  expect(retry).toHaveBeenCalledOnce();
});

it('renders an empty message', () => {
  render(<EmptyState message="Пусто" />);
  expect(screen.getByText('Пусто')).toBeInTheDocument();
});
