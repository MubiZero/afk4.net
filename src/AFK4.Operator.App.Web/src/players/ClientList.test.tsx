import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientList } from './ClientList';
import type { PlayerClientItem } from '../operatorHelpers';
import type { ClientSegment } from './playersModel';

afterEach(cleanup);

const client = (over: Partial<PlayerClientItem>): PlayerClientItem => ({
  playerAccountId: 'p1', name: 'Madina S.', status: 'active', balanceMinorUnits: 46000,
  debtMinorUnits: 0, last: '', tone: 'active', detail: '+992 90 555 22 11', phoneNumber: '+992 90 555 22 11',
  source: 'backend', ...over
});

const segments: ClientSegment[] = [
  { id: 'all', label: 'Все', count: 2 },
  { id: 'debt', label: 'Есть долг', count: 1 },
  { id: 'inactive', label: 'Неактивные', count: 0 }
];

const renderList = (over: Partial<Parameters<typeof ClientList>[0]> = {}) => {
  const onSearchChange = mock(() => {});
  const onSelectSegment = mock(() => {});
  const onSelectClient = mock(() => {});
  const onNewClient = mock(() => {});
  const { container } = render(
    <I18nProvider initialLocale="ru">
      <ClientList
        clients={[client({}), client({ playerAccountId: 'p2', name: 'Olim K.', status: 'debt', tone: 'debt', debtMinorUnits: 3500 })]}
        segments={segments}
        activeSegment="all"
        selectedClientId="p1"
        search=""
        showSkeleton={false}
        isLoading={false}
        emptyDescription="По текущему поиску клиентов нет."
        currencyCode="TJS"
        canCreatePlayer
        onNewClient={onNewClient}
        onSearchChange={onSearchChange}
        onSelectSegment={onSelectSegment}
        onSelectClient={onSelectClient}
        {...over}
      />
    </I18nProvider>
  );
  return { onSearchChange, onSelectSegment, onSelectClient, onNewClient, container };
};

describe('ClientList', () => {
  it('renders client rows with debt indicator', () => {
    const { container } = renderList();
    expect(screen.getByText('Madina S.')).toBeInTheDocument();
    expect(screen.getByText('Olim K.')).toBeInTheDocument();
    expect(container.querySelector('.client-row.debt')).not.toBeNull();
  });

  it('fires onSelectClient when a row is clicked', () => {
    const { onSelectClient } = renderList();
    fireEvent.click(screen.getByRole('button', { name: /Olim K\./ }));
    expect(onSelectClient).toHaveBeenCalledWith('p2');
  });

  it('fires onSelectSegment when a segment chip is clicked', () => {
    const { onSelectSegment } = renderList();
    fireEvent.click(screen.getByRole('button', { name: /Есть долг/ }));
    expect(onSelectSegment).toHaveBeenCalledWith('debt');
  });

  it('fires onSearchChange on input', () => {
    const { onSearchChange } = renderList();
    fireEvent.change(screen.getByPlaceholderText('Игрок, телефон, карта'), { target: { value: 'Mad' } });
    expect(onSearchChange).toHaveBeenCalledWith('Mad');
  });

  it('shows the EmptyState when there are no clients', () => {
    renderList({ clients: [] });
    expect(screen.getByText('Клиенты не найдены')).toBeInTheDocument();
  });

  it('does NOT flash the EmptyState while the list is still loading', () => {
    // На входе клиенты ещё пусты, а скелетон отложен на 180ms — раньше в этот зазор мигало
    // «Клиенты не найдены». Пока loadStatus==='loading' пустое состояние не показываем.
    renderList({ clients: [], isLoading: true });
    expect(screen.queryByText('Клиенты не найдены')).toBeNull();
  });

  it('shows skeleton rows when loading', () => {
    const { container } = renderList({ clients: [], showSkeleton: true });
    expect(container.querySelector('.skeleton-block')).not.toBeNull();
  });
});

describe('ClientList debtor row', () => {
  it('shows a single balance figure and a debt badge (no stacked second number)', () => {
    const debtor = client({
      playerAccountId: 'p1', name: 'Мадина Саидова', balanceMinorUnits: 0,
      debtMinorUnits: 3500, tone: 'debt', status: 'debt', detail: '+992 98 700 11 22 · 0 пакетов'
    });
    renderList({ clients: [debtor], segments: [], selectedClientId: null });

    const row = screen.getByRole('button', { name: /Мадина Саидова/ });
    // баланс — одно число справа (mono), без второй строки
    const figures = row.querySelector('.client-row-figures');
    const moneyFigures = figures?.querySelectorAll('.ui-money') ?? [];
    expect(moneyFigures).toHaveLength(1);
    expect(moneyFigures[0]).toHaveTextContent('0 с.');

    // долг — бейдж «Долг 35 с.» рядом с именем, не в колонке цифр; сумма — Money (mono)
    const debtBadge = row.querySelector('.ui-chip--status.is-danger');
    expect(debtBadge).not.toBeNull();
    expect(debtBadge).toHaveTextContent(/Долг\s+35\s+с\./);
    expect(debtBadge?.querySelector('.ui-money')).not.toBeNull();
  });

  it('shows the neutral status badge (not a debt badge) for an inactive, non-debt client', () => {
    const inactive = client({
      playerAccountId: 'p1', name: 'Неактивный Клиент', status: 'inactive', tone: 'regular',
      balanceMinorUnits: 0, debtMinorUnits: 0
    });
    renderList({ clients: [inactive], segments: [], selectedClientId: null });

    const row = screen.getByRole('button', { name: /Неактивный Клиент/ });
    const statusBadge = screen.getByText('Неактивен');
    expect(statusBadge).toHaveClass('ui-chip--status');
    expect(row.querySelector('.is-danger')).toBeNull();

    const moneyFigures = row.querySelectorAll('.ui-money');
    expect(moneyFigures).toHaveLength(1);
  });
});
