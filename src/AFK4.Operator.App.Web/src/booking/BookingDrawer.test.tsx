import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render } from '@testing-library/react';
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
    busy: false, canManage: true, currencyCode: 'TJS', conflict: null, seatConflict: false,
    groupConflicts, groupSize: 0, searchClients: async () => [], onClose: () => {},
    onChangeDraft: () => {}, onCreate: () => {}, onCreateGroup: () => {}, onRemoveSeat: () => {},
    onCancelGroup: () => {}, onSeat: () => {}, onMove: () => {}, onCancel: () => {},
    onConfirm: () => {}, onOpenMap: () => {}
  };
  return render(<I18nProvider><BookingDrawer {...props} /></I18nProvider>);
}

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
