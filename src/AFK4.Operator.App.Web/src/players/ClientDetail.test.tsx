import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientDetail } from './ClientDetail';
import type { PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto } from '../operatorApiClients';

afterEach(cleanup);

type DetailProps = Parameters<typeof ClientDetail>[0];

const client: PlayerClientItem = {
  playerAccountId: 'p1', name: 'Madina S.', status: 'active', balanceMinorUnits: 46000,
  debtMinorUnits: 0, last: '', tone: 'active', detail: '', phoneNumber: '+992 90 555 22 11', source: 'backend',
  createdAtUtc: null, lastActivityAtUtc: null, activePackageName: null, activePackageRemainingMinutes: 0
};

const baseProps: DetailProps = {
  client,
  isLoading: false,
  liveContext: { session: null, nextBooking: null },
  balanceMinorUnits: 46000,
  debtMinorUnits: 0,
  packageCount: 1,
  currencyCode: 'TJS',
  packages: [] as PlayerPackageDto[],
  options: [] as PackageOptionDto[],
  ledgerEntries: [] as LedgerEntryDto[],
  ledgerFilter: null,
  ledgerHasMore: false,
  ledgerLoading: false,
  onLedgerFilterChange: () => {},
  onLedgerLoadMore: () => {},
  selectedPackageDefinitionId: '',
  packageBusy: false,
  packagesLoading: false,
  topUpAmount: '',
  canTopUp: true,
  canPayDebt: true,
  canPurchase: true,
  canCreateReservation: true,
  canManageClient: false,
  onSetPin: () => {},
  onEditProfile: () => {},
  onToggleActive: () => {},
  canCorrect: false,
  onCorrect: () => {},
  canRefund: false,
  onRefund: () => {},
  onChangeTopUpAmount: () => {},
  onTopUp: () => {},
  onOpenPayDebt: () => {},
  onSelectOption: () => {},
  onBuy: () => {},
  onCreateReservation: () => {},
};

const renderDetail = (over: Partial<DetailProps> = {}) =>
  render(<I18nProvider initialLocale="ru"><ClientDetail {...baseProps} {...over} /></I18nProvider>);

describe('ClientDetail', () => {
  it('shows the empty state when no client is selected', () => {
    renderDetail({ client: null });
    expect(screen.getByText('Нет выбранного клиента')).toBeInTheDocument();
  });

  it('does NOT flash the empty state while the list is still loading', () => {
    renderDetail({ client: null, isLoading: true });
    expect(screen.queryByText('Нет выбранного клиента')).toBeNull();
  });

  it('renders the header, phone and reservation button for a selected client', () => {
    renderDetail();
    expect(screen.getByText('Madina S.')).toBeInTheDocument();
    expect(screen.getByText('+992 90 555 22 11', { exact: false })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Бронь/ })).toBeInTheDocument();
  });

  it('renders wallet zone, packages and history together — no tabs', () => {
    renderDetail();
    // нет табов вообще
    expect(screen.queryByRole('tab')).toBeNull();
    // все зоны видны одновременно
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();     // WalletZone
    expect(screen.getByText('История операций')).toBeInTheDocument();          // History panel heading
  });

  it('shows two money stat cards and the package count', () => {
    renderDetail({ balanceMinorUnits: 45000, debtMinorUnits: 3500, packageCount: 2 });
    expect(screen.getByText('450 с.')).toHaveClass('ui-money');
    expect(document.querySelectorAll('.clients-wallet-zone .ui-card--stat')).toHaveLength(2);
    // «Пакеты» — текстовый узел лейбла, счётчик «2» — соседний span; проверяем весь заголовок.
    expect(screen.getByText('Пакеты', { exact: false }).closest('.clients-subpanel-head')).toHaveTextContent('2');
  });

  it('marks the debt stat card as danger only when the client has debt', () => {
    const { rerender } = renderDetail({ debtMinorUnits: 0 });
    expect(document.querySelector('.ui-card--stat.is-danger')).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><ClientDetail {...baseProps} debtMinorUnits={3500} /></I18nProvider>);
    expect(document.querySelector('.ui-card--stat.is-danger')).not.toBeNull();
  });

  it('fires onOpenPayDebt from the pay-debt button when the client has debt', () => {
    const onOpenPayDebt = mock(() => {});
    renderDetail({ debtMinorUnits: 3500, onOpenPayDebt });
    fireEvent.click(screen.getByRole('button', { name: 'Списать долг' }));
    expect(onOpenPayDebt).toHaveBeenCalled();
  });

  it('fires onCreateReservation when the reservation button is clicked', () => {
    const onCreateReservation = mock(() => {});
    renderDetail({ onCreateReservation });
    fireEvent.click(screen.getByRole('button', { name: /Бронь/ }));
    expect(onCreateReservation).toHaveBeenCalled();
  });

  it('renders the actions menu only with manage permission', () => {
    const { rerender } = renderDetail({ canManageClient: false });
    expect(screen.queryByRole('button', { name: 'Действия с клиентом' })).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><ClientDetail {...baseProps} canManageClient /></I18nProvider>);
    expect(screen.getByRole('button', { name: 'Действия с клиентом' })).toBeInTheDocument();
  });

  it('shows the deactivated banner for an inactive client', () => {
    renderDetail({ client: { ...client, status: 'inactive' } });
    expect(screen.getByText(/Клиент деактивирован/)).toBeInTheDocument();
  });

  it('shows the live-context strip: playing now + next booking', () => {
    renderDetail({
      liveContext: {
        session: { seatName: 'PC-03', untilLabel: '14:30' },
        nextBooking: { timeLabel: '18:00', seatName: null }
      }
    });
    expect(screen.getByText(/PC-03/)).toBeInTheDocument();
    expect(screen.getByText(/18:00/)).toBeInTheDocument();
  });
});
