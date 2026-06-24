import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { CashTabBar, type CashTab } from './CashTabBar';

afterEach(cleanup);

const tabs: { id: CashTab; label: string }[] = [
  { id: 'sales', label: 'Продажи' },
  { id: 'shift', label: 'Смена' },
  { id: 'journal', label: 'Журнал кассы' }
];

describe('CashTabBar', () => {
  it('рендерит все вкладки, активная помечена aria-selected', () => {
    render(<CashTabBar tabs={tabs} activeTab="sales" onSelect={() => {}} />);
    expect(screen.getAllByRole('tab').map((t) => t.textContent)).toEqual(['Продажи', 'Смена', 'Журнал кассы']);
    expect(screen.getByRole('tab', { name: 'Продажи' })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByRole('tab', { name: 'Журнал кассы' })).toHaveAttribute('aria-selected', 'false');
  });

  it('клик по вкладке вызывает onSelect с её id', () => {
    const onSelect = mock(() => {});
    render(<CashTabBar tabs={tabs} activeTab="sales" onSelect={onSelect} />);
    fireEvent.click(screen.getByRole('tab', { name: 'Журнал кассы' }));
    expect(onSelect).toHaveBeenCalledWith('journal');
  });
});
