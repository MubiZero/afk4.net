import { describe, it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { StockHero } from './StockHero';

describe('StockHero', () => {
  it('рендерит подпись, значение и опциональный саб-текст', () => {
    render(<StockHero label="Стоимость склада" value="120 с." sub="73 ед · по средней закупочной" tone="neutral" />);
    expect(screen.getByText('Стоимость склада')).toBeInTheDocument();
    expect(screen.getByText('120 с.')).toBeInTheDocument();
    expect(screen.getByText('73 ед · по средней закупочной')).toBeInTheDocument();
  });

  it('тон определяет модификатор класса', () => {
    const { container } = render(<StockHero label="Нужно дозаказать" value={1} tone="attention" />);
    expect(container.querySelector('.stock-hero')).toHaveClass('stock-hero--attention');
  });

  it('без sub — подпись-примечание не рендерится', () => {
    const { container } = render(<StockHero label="X" value="1" tone="muted" />);
    expect(container.querySelector('.stock-hero-sub')).toBeNull();
  });
});
