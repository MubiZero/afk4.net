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

  it('shows skeleton rows when loading', () => {
    const { container } = renderList({ clients: [], showSkeleton: true });
    expect(container.querySelector('.skeleton-block')).not.toBeNull();
  });
});
