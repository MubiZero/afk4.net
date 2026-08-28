import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { SeatSummary } from '../operatorData';
import { BookingDrawer, type BookingDraft, type BookingDrawerProps } from './BookingDrawer';

afterEach(cleanup);

function seat(overrides: Partial<SeatSummary>): SeatSummary {
  return {
    id: 'seat-1', zone: 'Зал A', name: 'PC-01', tone: 'ready', stateLabel: 'Свободен',
    player: '', remaining: '', device: '', command: '', app: '', activeSessionId: null,
    ...overrides
  };
}

const activeSeat = seat({ id: 'a1', zone: 'Зал A', name: 'PC-01', tone: 'active', stateLabel: 'В сессии', activeSessionId: 'session-1' });
const serviceSeat = seat({ id: 'b4', zone: 'Зал B', name: 'PC-04', tone: 'service', stateLabel: 'Обслуживание' });

// Заглушка контроллера репутации: панель только рисует ответ сети, спрашивает его оркестратор.
function idleReputation() {
  return { state: { status: 'idle' } as const, ask: () => {} };
}

function draft(): BookingDraft {
  return {
    customerName: '', phoneNumber: '', playerAccountId: '', clientBalanceMinorUnits: null,
    clientDebtMinorUnits: null, startsAt: '2026-07-15T18:00', durationMinutes: 60,
    seatId: '', seatIds: ['a1', 'b4']
  };
}

function renderDrawer(groupConflicts = new Set<string>()) {
  const props: BookingDrawerProps = {
    mode: 'create', selected: null, freeSeats: [], allSeats: [activeSeat, serviceSeat], draft: draft(),
    busy: false, canManage: true, canStartSessions: true, currencyCode: 'TJS', conflict: null, seatConflict: false,
    groupConflicts, groupSize: 0, searchClients: async () => [], reputation: idleReputation(), onClose: () => {},
    onChangeDraft: () => {}, onCreate: () => {}, onCreateGroup: () => {}, onRemoveSeat: () => {},
    onCancelGroup: () => {}, onStart: () => {}, onMove: () => {}, onCancel: () => {}, onReject: () => {},
    onConfirm: () => {}, onOpenMap: () => {}
  };
  return render(<I18nProvider><BookingDrawer {...props} /></I18nProvider>);
}

function detail(state: string, onConfirm = () => {}, onStart = () => {}) {
  const item = {
    reservationId: 'r1', reservationGroupId: '', version: 2, state, source: 'operator',
    startMs: Date.now() + 60_000, endMs: Date.now() + 3_660_000, durationMinutes: 60,
    customerName: 'Мадина', phoneNumber: '+992900000000', note: '', playerAccountId: 'p1',
    platformPersonId: '', seatId: 'a1', seatName: 'PC-01', zoneName: 'Зал A', tone: state as 'pending', startedSessionId: '',
    respondByMs: null
  };
  const props: BookingDrawerProps = {
    mode: 'detail', selected: item, freeSeats: [], allSeats: [activeSeat], draft: draft(),
    busy: false, canManage: true, canStartSessions: true, currencyCode: 'TJS', conflict: null,
    seatConflict: false, groupConflicts: new Set(), groupSize: 0, searchClients: async () => [], reputation: idleReputation(),
    onClose: () => {}, onChangeDraft: () => {}, onCreate: () => {}, onCreateGroup: () => {},
    onRemoveSeat: () => {}, onCancelGroup: () => {}, onStart, onMove: () => {}, onCancel: () => {}, onReject: () => {},
    onConfirm, onOpenMap: () => {}
  };
  return render(<I18nProvider><BookingDrawer {...props} /></I18nProvider>);
}

it('blocks drawer close while a reservation command is pending', () => {
  const onClose = mock(() => {});
  const props: BookingDrawerProps = {
    mode: 'detail', selected: null, freeSeats: [], allSeats: [], draft: draft(), busy: true,
    canManage: true, canStartSessions: true, currencyCode: 'TJS', conflict: null, seatConflict: false,
    groupConflicts: new Set(), groupSize: 0, searchClients: async () => [], reputation: idleReputation(), onClose,
    onChangeDraft: () => {}, onCreate: () => {}, onCreateGroup: () => {}, onRemoveSeat: () => {},
    onCancelGroup: () => {}, onStart: () => {}, onMove: () => {}, onCancel: () => {}, onReject: () => {},
    onConfirm: () => {}, onOpenMap: () => {}
  };
  const result = render(<I18nProvider><BookingDrawer {...props} /></I18nProvider>);
  const close = result.getByRole('button', { name: 'Отмена' });
  expect(close).toBeDisabled();
  fireEvent.click(close);
  expect(onClose).not.toHaveBeenCalled();
});

describe('BookingDrawer arbitrary group selection', () => {
  it('показывает текущий несвободный статус как предупреждение, но не блокирует будущую бронь без пересечения', () => {
    const result = renderDrawer();

    expect(result.getByText('В сессии')).toBeTruthy();
    expect(result.getByText('Обслуживание')).toBeTruthy();
    expect(result.container.querySelectorAll('.booking-seat-chip.is-unavailable')).toHaveLength(2);
    expect(result.container.querySelector<HTMLButtonElement>('.booking-primary-action')?.disabled).toBe(false);
  });

  it('оставляет фактическое пересечение видимым и блокирует групповое создание', () => {
    const result = renderDrawer(new Set(['a1']));

    expect(result.container.querySelector('.booking-seat-chip.is-conflict')).not.toBeNull();
    expect(result.container.querySelector<HTMLButtonElement>('.booking-primary-action')?.disabled).toBe(true);
    expect(result.getByRole('alert')).toBeTruthy();
  });
});

describe('BookingDrawer reservation lifecycle actions', () => {
  it('pending offers Confirm but not Start', () => {
    const result = detail('pending');
    expect(result.getByRole('button', { name: 'Подтвердить' })).toBeTruthy();
    expect(result.queryByRole('button', { name: 'Начать сессию' })).toBeNull();
  });

  it('confirmed offers Start but not Confirm', () => {
    const onStart = mock(() => {});
    const result = detail('confirmed', () => {}, onStart);
    const button = result.getByRole('button', { name: 'Начать сессию' });
    expect(result.queryByRole('button', { name: 'Подтвердить' })).toBeNull();
    fireEvent.click(button);
    expect(onStart).toHaveBeenCalledTimes(1);
  });

  it('seated and cancelled offer neither lifecycle action', () => {
    for (const state of ['seated', 'cancelled']) {
      const result = detail(state);
      expect(result.queryByRole('button', { name: 'Подтвердить' })).toBeNull();
      expect(result.queryByRole('button', { name: 'Начать сессию' })).toBeNull();
      cleanup();
    }
  });
});
