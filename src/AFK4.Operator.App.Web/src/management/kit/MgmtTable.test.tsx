import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { MgmtTable } from './MgmtTable';
import type { MgmtColumn } from './types';

const wrap = (ui: React.ReactNode) => render(<I18nProvider initialLocale="ru">{ui}</I18nProvider>);

afterEach(cleanup);

interface Zone { id: string; name: string; seats: number }
const columns: MgmtColumn<Zone>[] = [
  { key: 'name', header: 'Зал', render: (z) => z.name },
  { key: 'seats', header: 'ПК', align: 'end', render: (z) => String(z.seats) }
];
const zones: Zone[] = [
  { id: 'z1', name: 'Зал А', seats: 6 },
  { id: 'z2', name: 'VIP', seats: 3 }
];

describe('MgmtTable', () => {
  it('renders headers and a row per item', () => {
    wrap(<MgmtTable columns={columns} rows={zones} rowKey={(z) => z.id} gridTemplate="1fr 80px" empty={{ title: 'Пусто' }} />);
    expect(screen.getByText('Зал')).toBeTruthy();
    expect(screen.getByText('Зал А')).toBeTruthy();
    expect(screen.getByText('VIP')).toBeTruthy();
  });

  it('calls onSelectRow when a row is clicked', () => {
    const onSelectRow = mock((_z: Zone) => {});
    wrap(<MgmtTable columns={columns} rows={zones} rowKey={(z) => z.id} gridTemplate="1fr 80px" onSelectRow={onSelectRow} empty={{ title: 'Пусто' }} />);
    fireEvent.click(screen.getByText('Зал А'));
    expect(onSelectRow).toHaveBeenCalledTimes(1);
    expect(onSelectRow.mock.calls[0][0].id).toBe('z1');
  });

  it('shows the empty state when there are no rows and not loading', () => {
    wrap(<MgmtTable columns={columns} rows={[]} rowKey={(z) => z.id} gridTemplate="1fr 80px" empty={{ title: 'Залов нет' }} />);
    expect(screen.getByText('Залов нет')).toBeTruthy();
  });

  it('renders a skeleton and no rows while loading', () => {
    const { container } = wrap(<MgmtTable columns={columns} rows={zones} rowKey={(z) => z.id} gridTemplate="1fr 80px" isLoading empty={{ title: 'Пусто' }} />);
    expect(container.querySelector('.ctable-skeleton')).toBeTruthy();
    expect(screen.queryByText('Зал А')).toBeNull();
  });

  it('fires the primary toolbar action', () => {
    const onClick = mock(() => {});
    wrap(
      <MgmtTable
        columns={columns}
        rows={zones}
        rowKey={(z) => z.id}
        gridTemplate="1fr 80px"
        toolbar={{ title: 'Залы', primary: { label: '+ Зал', onClick } }}
        empty={{ title: 'Пусто' }}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: '+ Зал' }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('renders a row ⋯ menu when rowActions are provided', () => {
    wrap(
      <MgmtTable
        columns={columns}
        rows={zones}
        rowKey={(z) => z.id}
        gridTemplate="1fr 80px"
        rowActions={(z) => [{ id: 'del', label: `Удалить ${z.name}`, onSelect: () => {}, danger: true }]}
        empty={{ title: 'Пусто' }}
      />
    );
    // одно ⋯-меню на строку
    expect(screen.getAllByRole('button', { name: 'Действия' })).toHaveLength(zones.length);
  });
});
