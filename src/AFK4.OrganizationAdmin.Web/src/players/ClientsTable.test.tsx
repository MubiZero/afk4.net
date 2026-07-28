import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientsTable } from './ClientsTable';
import type { PlayerClientItem } from '../operatorHelpers';
import type { ClientLiveContext, ClientSegment } from './playersModel';

afterEach(cleanup);

// Совпадает с currentDate сессии (см. playersModel.test.ts) — детерминируемый nowMs.
const NOW = new Date('2026-07-06T12:00:00Z').getTime();
const daysAgoIso = (days: number) => new Date(NOW - days * 24 * 60 * 60 * 1000).toISOString();

const client = (over: Partial<PlayerClientItem>): PlayerClientItem => ({
  playerAccountId: 'p1', name: 'Фариза Назарова', isActive: true, status: 'active', balanceMinorUnits: 45000,
  debtMinorUnits: 0, last: '', tone: 'active', detail: '', phoneNumber: '+992 93 100 20 30',
  source: 'backend', createdAtUtc: null, lastActivityAtUtc: null,
  activePackageName: null, activePackageRemainingMinutes: 0, ...over
});

const segments: ClientSegment[] = [
  { id: 'all', label: 'Все', count: 2 },
  { id: 'debt', label: 'Есть долг', count: 1 },
  { id: 'inactive', label: 'Неактивные', count: 0 }
];

const renderTable = (over: Partial<Parameters<typeof ClientsTable>[0]> = {}) => {
  const onSearchChange = mock(() => {});
  const onSelectSegment = mock(() => {});
  const onSelectClient = mock(() => {});
  const onNewClient = mock(() => {});
  const { container } = render(
    <I18nProvider initialLocale="ru">
      <ClientsTable
        clients={[client({})]}
        segments={segments}
        activeSegment="all"
        selectedClientId={null}
        search=""
        showSkeleton={false}
        isLoading={false}
        emptyDescription="По текущему поиску клиентов нет."
        currencyCode="TJS"
        canCreatePlayer
        liveContextByClient={new Map<string, ClientLiveContext>()}
        nowMs={NOW}
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

describe('ClientsTable', () => {
  it('renders a row button with the client name (keyboard-focusable, not a <tr>)', () => {
    const { container } = renderTable();
    const row = screen.getByRole('button', { name: /Фариза Назарова/ });
    expect(row.tagName).toBe('BUTTON');
    expect(container.querySelector('tr')).toBeNull();
  });

  it('shows the balance figure', () => {
    renderTable();
    expect(screen.getByRole('button', { name: /Фариза Назарова/ })).toHaveTextContent('450 с.');
  });

  it('shows a debt figure and the debt row class only when the client owes money', () => {
    renderTable({ clients: [client({ debtMinorUnits: 28000 })] });
    const row = screen.getByRole('button', { name: /Фариза Назарова/ });
    expect(row).toHaveClass('debt');
    expect(row).toHaveTextContent('280 с.');
  });

  it('does not add the debt class or a debt figure for a client without debt', () => {
    renderTable({ clients: [client({ debtMinorUnits: 0 })] });
    const row = screen.getByRole('button', { name: /Фариза Назарова/ });
    expect(row).not.toHaveClass('debt');
    // единственная фигура денег — баланс; долг рисуется прочерком, не вторым Money
    expect(row.querySelectorAll('.ui-money')).toHaveLength(1);
  });

  it('shows a "Новый" tag for a client created within the last week', () => {
    renderTable({ clients: [client({ createdAtUtc: daysAgoIso(1) })] });
    expect(screen.getByRole('button', { name: /Фариза Назарова/ })).toHaveTextContent('Новый');
  });

  it('does not show the "Новый" tag for an older client', () => {
    renderTable({ clients: [client({ createdAtUtc: daysAgoIso(30) })] });
    expect(screen.getByRole('button', { name: /Фариза Назарова/ })).not.toHaveTextContent('Новый');
  });

  it('shows the "Неактивен" tag and row class for an inactive client', () => {
    renderTable({ clients: [client({ isActive: false, status: 'inactive' })] });
    const row = screen.getByRole('button', { name: /Фариза Назарова/ });
    expect(row).toHaveTextContent('Неактивен');
    expect(row).toHaveClass('inactive');
  });

  it('shows the live session from liveContextByClient when the client is playing right now', () => {
    const liveContextByClient = new Map<string, ClientLiveContext>([
      ['p1', { session: { seatName: 'PC-01', untilLabel: '11:20' }, nextBooking: null }]
    ]);
    renderTable({ liveContextByClient });
    const row = screen.getByRole('button', { name: /Фариза Назарова/ });
    expect(row).toHaveTextContent('PC-01');
    expect(row).toHaveTextContent('до 11:20');
    expect(row).toHaveTextContent('сейчас'); // визит = «сейчас», пока играет
  });

  it('shows the active package time bank when the client is not playing', () => {
    renderTable({ clients: [client({ activePackageName: 'Ночной 5ч', activePackageRemainingMinutes: 150 })] });
    const row = screen.getByRole('button', { name: /Фариза Назарова/ });
    expect(row).toHaveTextContent('Ночной 5ч');
    expect(row).toHaveTextContent('150 мин');
  });

  it('falls back to the relative visit label when the client is not playing and has no package', () => {
    renderTable({ clients: [client({ lastActivityAtUtc: daysAgoIso(1) })] });
    expect(screen.getByRole('button', { name: /Фариза Назарова/ })).toHaveTextContent('вчера');
  });

  it('fires onSelectClient with the playerAccountId when a row is clicked', () => {
    const { onSelectClient } = renderTable();
    fireEvent.click(screen.getByRole('button', { name: /Фариза Назарова/ }));
    expect(onSelectClient).toHaveBeenCalledWith('p1');
  });

  it('fires onSelectSegment when a segment chip is clicked', () => {
    const { onSelectSegment } = renderTable();
    fireEvent.click(screen.getByRole('button', { name: /Есть долг/ }));
    expect(onSelectSegment).toHaveBeenCalledWith('debt');
  });

  it('fires onSearchChange on input', () => {
    const { onSearchChange } = renderTable();
    fireEvent.change(screen.getByPlaceholderText('Игрок, телефон, карта'), { target: { value: 'Фар' } });
    expect(onSearchChange).toHaveBeenCalledWith('Фар');
  });

  it('shows skeleton rows when showSkeleton is true', () => {
    const { container } = renderTable({ clients: [], showSkeleton: true });
    expect(container.querySelector('.skeleton-block')).not.toBeNull();
  });

  it('shows the EmptyState when there are no clients and loading has finished', () => {
    renderTable({ clients: [], isLoading: false });
    expect(screen.getByText('Клиенты не найдены')).toBeInTheDocument();
  });

  it('does not flash the EmptyState while the list is still loading', () => {
    renderTable({ clients: [], isLoading: true });
    expect(screen.queryByText('Клиенты не найдены')).toBeNull();
  });

  it('hides the "Новый клиент" button when canCreatePlayer is false', () => {
    renderTable({ canCreatePlayer: false });
    expect(screen.queryByRole('button', { name: /Новый клиент/ })).toBeNull();
  });
});
