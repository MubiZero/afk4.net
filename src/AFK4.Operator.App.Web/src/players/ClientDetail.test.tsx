import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientDetail, type ClientDetailTab } from './ClientDetail';
import type { PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto } from '../operatorApiClients';

afterEach(cleanup);

type DetailProps = {
  client: PlayerClientItem | null;
  activeTab: ClientDetailTab;
  balanceMinorUnits: number;
  debtMinorUnits: number;
  packageCount: number;
  currencyCode: string;
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  ledgerEntries: LedgerEntryDto[];
  recentEntries: LedgerEntryDto[];
  ledgerFilter: string | null;
  ledgerHasMore: boolean;
  ledgerLoading: boolean;
  onLedgerFilterChange: (entryType: string | null) => void;
  onLedgerLoadMore: () => void;
  selectedPackageDefinitionId: string;
  topUpAmount: string;
  topUpReason: string;
  debtAmount: string;
  debtReason: string;
  canTopUp: boolean;
  canPayDebt: boolean;
  canPurchase: boolean;
  canCreateReservation: boolean;
  canManageClient: boolean;
  onSetPin: () => void;
  onEditProfile: () => void;
  onToggleActive: () => void;
  canCorrect: boolean;
  onCorrect: () => void;
  canRefund: boolean;
  onRefund: (entry: LedgerEntryDto) => void;
  onSelectTab: (tab: ClientDetailTab) => void;
  onChangeTopUpAmount: (v: string) => void;
  onChangeTopUpReason: (v: string) => void;
  onChangeDebtAmount: (v: string) => void;
  onChangeDebtReason: (v: string) => void;
  onTopUp: () => void;
  onPayDebt: () => void;
  onSelectOption: (id: string) => void;
  onBuy: () => void;
  onCreateReservation: () => void;
};

const client: PlayerClientItem = {
  playerAccountId: 'p1', name: 'Madina S.', status: 'active', balanceMinorUnits: 46000,
  debtMinorUnits: 0, last: '', tone: 'active', detail: '', phoneNumber: '+992 90 555 22 11', source: 'backend'
};

const baseProps: DetailProps = {
  client,
  activeTab: 'wallet',
  balanceMinorUnits: 46000,
  debtMinorUnits: 0,
  packageCount: 1,
  currencyCode: 'TJS',
  packages: [],
  options: [],
  ledgerEntries: [],
  recentEntries: [],
  ledgerFilter: null,
  ledgerHasMore: false,
  ledgerLoading: false,
  onLedgerFilterChange: () => {},
  onLedgerLoadMore: () => {},
  selectedPackageDefinitionId: '',
  topUpAmount: '100.00', topUpReason: 'пополнение через кассу',
  debtAmount: '', debtReason: 'оплата долга через кассу',
  canTopUp: true, canPayDebt: false, canPurchase: true, canCreateReservation: true,
  canManageClient: false, onSetPin: () => {}, onEditProfile: () => {}, onToggleActive: () => {},
  canCorrect: false, onCorrect: () => {},
  canRefund: false, onRefund: () => {},
  onSelectTab: () => {}, onChangeTopUpAmount: () => {}, onChangeTopUpReason: () => {},
  onChangeDebtAmount: () => {}, onChangeDebtReason: () => {}, onTopUp: () => {}, onPayDebt: () => {},
  onSelectOption: () => {}, onBuy: () => {}, onCreateReservation: () => {}
};

const renderDetail = (over: Partial<DetailProps> = {}) =>
  render(<I18nProvider initialLocale="ru"><ClientDetail {...baseProps} {...over} /></I18nProvider>);

describe('ClientDetail', () => {
  it('shows the empty state when no client is selected', () => {
    renderDetail({ client: null });
    expect(screen.getByText('Нет выбранного клиента')).toBeInTheDocument();
  });

  it('renders the header, chips and reservation button for a selected client', () => {
    renderDetail();
    expect(screen.getByText('Madina S.')).toBeInTheDocument();
    expect(screen.getByText('+992 90 555 22 11', { exact: false })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Бронь/ })).toBeInTheDocument();
  });

  it('switches tab content when a tab is clicked', () => {
    const onSelectTab = mock(() => {});
    renderDetail({ onSelectTab });
    fireEvent.click(screen.getByRole('tab', { name: 'История' }));
    expect(onSelectTab).toHaveBeenCalledWith('history');
  });

  it('renders the wallet section on the wallet tab', () => {
    renderDetail({ activeTab: 'wallet' });
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();
  });

  it('renders the history section on the history tab', () => {
    renderDetail({ activeTab: 'history', ledgerEntries: [], ledgerLoading: false });
    expect(screen.getByText('Операций нет')).toBeInTheDocument();
  });

  it('fires onCreateReservation when the reservation button is clicked', () => {
    const onCreateReservation = mock(() => {});
    renderDetail({ onCreateReservation });
    fireEvent.click(screen.getByRole('button', { name: /Бронь/ }));
    expect(onCreateReservation).toHaveBeenCalled();
  });

  it('renders the actions menu when the staff can manage the client', () => {
    renderDetail({ canManageClient: true });
    expect(screen.getByRole('button', { name: 'Действия с клиентом' })).toBeInTheDocument();
  });

  it('hides the actions menu without manage permission', () => {
    renderDetail({ canManageClient: false });
    expect(screen.queryByRole('button', { name: 'Действия с клиентом' })).not.toBeInTheDocument();
  });

  it('shows the deactivated banner for an inactive client', () => {
    renderDetail({ client: { ...client, status: 'inactive' } });
    expect(screen.getByText(/Клиент деактивирован/)).toBeInTheDocument();
  });
});
