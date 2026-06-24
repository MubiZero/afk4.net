import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { CashTabBar, type CashTab } from './CashTabBar';

afterEach(cleanup);

const tabs: { id: CashTab; label: string }[] = [
  { id: 'sales', label: 'Продажи' },
  { id: 'orders', label: 'Заказы' },
  { id: 'shift', label: 'Смена' },
  { id: 'review', label: 'Проверка' }
];

describe('CashTabBar', () => {
  it('рендерит все вкладки, активная помечена aria-selected', () => {
    render(<CashTabBar tabs={tabs} activeTab="sales" onSelect={() => {}} />);
    expect(screen.getAllByRole('tab').map((t) => t.textContent)).toEqual(['Продажи', 'Заказы', 'Смена', 'Проверка']);
    expect(screen.getByRole('tab', { name: 'Продажи' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Проверка' })).toHaveAttribute('aria-selected', 'false');
  });

  it('клик по вкладке вызывает onSelect с её id', () => {
    const onSelect = mock(() => {});
    render(<CashTabBar tabs={tabs} activeTab="sales" onSelect={onSelect} />);
    fireEvent.click(screen.getByRole('tab', { name: 'Проверка' }));
    expect(onSelect).toHaveBeenCalledWith('review');
  });
});
