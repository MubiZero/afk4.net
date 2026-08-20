import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientDrawer } from './ClientDrawer';
import type { PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto } from '../operatorApiClients';
import type { ClientLiveContext } from './playersModel';

afterEach(cleanup);

const client: PlayerClientItem = {
  playerAccountId: 'p1', name: 'Фариза Назарова', status: 'active', isActive: true, balanceMinorUnits: 45000,
  debtMinorUnits: 0, last: '', tone: 'active', detail: '', phoneNumber: '+992 93 100 20 30', source: 'backend',
  createdAtUtc: null, lastActivityAtUtc: null, activePackageName: null, activePackageRemainingMinutes: 0
};

const noLiveContext: ClientLiveContext = { session: null, nextBooking: null };

const entry = (over: Partial<LedgerEntryDto>): LedgerEntryDto => ({
  ledgerEntryId: 'le-x', organizationId: 'o', branchId: 'b', playerAccountId: 'p',
  sessionId: null, playerPackageId: null, entryType: 'top_up', accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 5000 }, quantitySeconds: 0,
  description: '', reason: '', reversesLedgerEntryId: null,
  createdByStaffUserId: 's', createdAtUtc: '2026-06-22T09:00:00Z', ...over
});

type DrawerProps = Parameters<typeof ClientDrawer>[0];

const baseProps: DrawerProps = {
  client,
  liveContext: noLiveContext,
  balanceMinorUnits: 45000,
  heldMinorUnits: 0,
  debtMinorUnits: 0,
  currencyCode: 'TJS',
  recentEntries: [],
  packages: [],
  packagesLoading: false,
  topUpAmount: '',
  canTopUp: true,
  onChangeTopUpAmount: () => {},
  onTopUp: () => {},
  onOpenDcTopUp: () => {},
  canPayDebt: true,
  onOpenPayDebt: () => {},
  canManageClient: true,
  canCorrect: false,
  canCreateReservation: true,
  onCorrect: () => {},
  onCreateReservation: () => {},
  onEditProfile: () => {},
  onToggleActive: () => {},
  onOpenFullHistory: () => {},
  onClose: () => {},
};

const renderDrawer = (over: Partial<DrawerProps> = {}) =>
  render(<I18nProvider initialLocale="ru"><ClientDrawer {...baseProps} {...over} /></I18nProvider>);

describe('ClientDrawer', () => {
  it('renders the client name and phone', () => {
    renderDrawer();
    expect(screen.getByText('Фариза Назарова')).toBeInTheDocument();
    expect(screen.getByText('+992 93 100 20 30')).toBeInTheDocument();
  });

  it('shows an .ok context pill while a session is live', () => {
    renderDrawer({ liveContext: { session: { seatName: 'PC-01', untilLabel: '11:20' }, nextBooking: null } });
    const pill = document.querySelector('.status-pill.ok');
    expect(pill).not.toBeNull();
    expect(pill).toHaveTextContent('PC-01');
    expect(screen.getByText('Нет брони')).toBeInTheDocument();
  });

  it('shows a .neutral context pill for an upcoming booking', () => {
    renderDrawer({ liveContext: { session: null, nextBooking: { timeLabel: '19:00', seatName: 'VIP-03' } } });
    expect(screen.getByText('Не играет')).toBeInTheDocument();
    const pills = document.querySelectorAll('.status-pill.neutral');
    expect([...pills].some((pill) => pill.textContent?.includes('VIP-03'))).toBe(true);
  });

  it('shows explicit placeholders when there is neither a session nor a booking', () => {
    renderDrawer();
    expect(document.querySelector('.drawer-context')).not.toBeNull();
    expect(screen.getByText('Не играет')).toBeInTheDocument();
    expect(screen.getByText('Нет брони')).toBeInTheDocument();
  });

  it('renders the wallet balance', () => {
    renderDrawer({ balanceMinorUnits: 45000 });
    expect(document.querySelector('.wallet-balance')).toHaveTextContent('450 с.');
  });

  it('shows what is held for bookings only when something is actually held', () => {
    const { rerender } = renderDrawer({ heldMinorUnits: 0 });
    expect(document.querySelector('.wallet-held')).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><ClientDrawer {...baseProps} heldMinorUnits={12000} /></I18nProvider>);
    const held = document.querySelector('.wallet-held');
    expect(held).toHaveTextContent('Придержано под бронь');
    expect(held).toHaveTextContent('120 с.');
    // Остаток не пересчитываем: холд из него уже вычтен сервером.
    expect(document.querySelector('.wallet-balance')).toHaveTextContent('450 с.');
  });

  it('shows the debt callout only when the client has debt', () => {
    const { rerender } = renderDrawer({ debtMinorUnits: 0 });
    expect(document.querySelector('.wallet-debt')).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><ClientDrawer {...baseProps} debtMinorUnits={28000} /></I18nProvider>);
    expect(document.querySelector('.wallet-debt')).toHaveTextContent('280 с.');
  });

  it('shows inactive lifecycle together with debt, offers activation and hides money actions', () => {
    renderDrawer({
      client: { ...client, isActive: false, status: 'inactive', tone: 'regular', debtMinorUnits: 28000 },
      debtMinorUnits: 28000,
    });

    expect(screen.getByText('Неактивен')).toBeInTheDocument();
    expect(document.querySelector('.wallet-debt')).toHaveTextContent('280 с.');
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    expect(screen.getByRole('menuitem', { name: 'Активировать' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Пополнить/ })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Списать долг' })).toBeNull();
  });

  it('fires onTopUp from the top-up form', () => {
    const onTopUp = mock(() => {});
    renderDrawer({ onTopUp });
    fireEvent.click(screen.getByRole('button', { name: /Пополнить/ }));
    expect(onTopUp).toHaveBeenCalled();
  });

  it('shows «Списать долг» when the client has debt and fires onOpenPayDebt', () => {
    const onOpenPayDebt = mock(() => {});
    renderDrawer({ debtMinorUnits: 28000, onOpenPayDebt });
    fireEvent.click(screen.getByRole('button', { name: 'Списать долг' }));
    expect(onOpenPayDebt).toHaveBeenCalled();
  });

  it('renders exactly the limited number of recent entries, not the full list', () => {
    const entries = Array.from({ length: 6 }, (_, index) => entry({ ledgerEntryId: `le-${index}` }));
    const { container } = renderDrawer({ recentEntries: entries });
    expect(container.querySelectorAll('.ui-ledger-row')).toHaveLength(4);
  });

  it('fires onOpenFullHistory from the «вся история →» link', () => {
    const onOpenFullHistory = mock(() => {});
    renderDrawer({ recentEntries: [entry({})], onOpenFullHistory });
    fireEvent.click(screen.getByRole('button', { name: /Вся история/ }));
    expect(onOpenFullHistory).toHaveBeenCalled();
  });

  it('fires onClose from the close button', () => {
    const onClose = mock(() => {});
    renderDrawer({ onClose });
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть' }));
    expect(onClose).toHaveBeenCalled();
  });

  describe('«⋯» actions menu', () => {
    it('opens with items gated by permission: reservation, edit, deactivate — no correction', () => {
      renderDrawer({ canCreateReservation: true, canManageClient: true, canCorrect: false });
      fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
      const items = screen.getAllByRole('menuitem');
      expect(items.map((item) => item.textContent)).toEqual([
        'Создать бронь',
        'Править профиль',
        'Деактивировать',
      ]);
    });

    it('adds the correction item when canCorrect is granted', () => {
      renderDrawer({ canCorrect: true });
      fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
      expect(screen.getByRole('menuitem', { name: /Ручная корректировка/ })).toBeInTheDocument();
    });

    it('fires onCreateReservation/onCorrect from their menu items', () => {
      const onCreateReservation = mock(() => {});
      const onCorrect = mock(() => {});
      renderDrawer({ canCorrect: true, onCreateReservation, onCorrect });
      fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
      fireEvent.click(screen.getByRole('menuitem', { name: /Создать бронь/ }));
      expect(onCreateReservation).toHaveBeenCalled();

      fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
      fireEvent.click(screen.getByRole('menuitem', { name: /Ручная корректировка/ }));
      expect(onCorrect).toHaveBeenCalled();
    });

    it('hides the trigger entirely when the operator has none of the three permissions', () => {
      renderDrawer({ canManageClient: false, canCreateReservation: false, canCorrect: false });
      expect(screen.queryByRole('button', { name: 'Действия с клиентом' })).toBeNull();
    });
  });
});
