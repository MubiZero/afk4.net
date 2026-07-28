import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { EmptyState, Skeleton } from './operatorPrimitives';

afterEach(cleanup);

describe('Skeleton', () => {
  it('renders a block placeholder hidden from a11y, keeping a custom class', () => {
    const { container } = render(<Skeleton className="seat-skeleton" />);
    const block = container.querySelector('.skeleton-block');
    expect(block).not.toBeNull();
    expect(block).toHaveClass('seat-skeleton');
    expect(block).toHaveAttribute('aria-hidden', 'true');
  });

  it('renders the requested number of text lines', () => {
    const { container } = render(<Skeleton variant="text" lines={3} />);
    expect(container.querySelectorAll('.skeleton-block')).toHaveLength(3);
  });

  it('renders a circle variant', () => {
    const { container } = render(<Skeleton variant="circle" />);
    expect(container.querySelector('.skeleton-circle')).not.toBeNull();
  });
});

describe('EmptyState', () => {
  it('renders title and description', () => {
    render(<EmptyState title="Нет ПК" description="Смените фильтр" />);
    expect(screen.getByText('Нет ПК')).toBeInTheDocument();
    expect(screen.getByText('Смените фильтр')).toBeInTheDocument();
  });

  it('renders an action button that fires onClick', () => {
    const onClick = mock(() => {});
    render(<EmptyState title="Пусто" action={{ label: 'Создать', onClick }} />);
    fireEvent.click(screen.getByText('Создать'));
    expect(onClick).toHaveBeenCalled();
  });

  it('omits description and action when not provided', () => {
    const { container } = render(<EmptyState title="Заказов нет" />);
    expect(screen.getByText('Заказов нет')).toBeInTheDocument();
    expect(container.querySelector('button')).toBeNull();
  });

  it('renders an icon when provided', () => {
    const { container } = render(<EmptyState title="Пусто" icon={<svg data-testid="ico" />} />);
    expect(container.querySelector('.empty-state-icon')).not.toBeNull();
    expect(container.querySelector('[data-testid="ico"]')).not.toBeNull();
  });
});
