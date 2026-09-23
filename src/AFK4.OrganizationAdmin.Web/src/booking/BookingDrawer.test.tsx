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
    onCancelGroup: () => {}, onStart: () => {}, onMove: () => {}, onCancel: () => {}, onReject: () => {}, onMarkNoShow: () => {},
    onConfirm: () => {}, onOpenMap: () => {}, onSeat: () => {}
  };
  return render(<I18nProvider><BookingDrawer {...props} /></I18nProvider>);
}

function detail(
  state: string,
  onConfirm = () => {},
  onStart = () => {},
  onMarkNoShow = () => {},
  startMs = Date.now() + 60_000,
  onSeat = () => {},
  over: Partial<BookingDrawerProps> & { seatId?: string } = {}
) {
  const item = {
    reservationId: 'r1', reservationGroupId: '', version: 2, state, source: 'operator',
    startMs, endMs: startMs + 3_600_000, durationMinutes: 60,
    customerName: 'Мадина', phoneNumber: '+992900000000', note: '', playerAccountId: 'p1',
    platformPersonId: '', seatId: over.seatId ?? 'a1', seatName: over.seatId === '' ? '' : 'PC-01', zoneName: 'Зал A', tone: state as 'pending', startedSessionId: '',
    respondByMs: null
  };
  const { seatId: _seatId, ...propsOver } = over;
  const props: BookingDrawerProps = {
    mode: 'detail', selected: item, freeSeats: [], allSeats: [activeSeat], draft: draft(),
    busy: false, canManage: true, canStartSessions: true, currencyCode: 'TJS', conflict: null,
    seatConflict: false, groupConflicts: new Set(), groupSize: 0, searchClients: async () => [], reputation: idleReputation(),
    onClose: () => {}, onChangeDraft: () => {}, onCreate: () => {}, onCreateGroup: () => {},
    onRemoveSeat: () => {}, onCancelGroup: () => {}, onStart, onMove: () => {}, onCancel: () => {}, onReject: () => {}, onMarkNoShow,
    onConfirm, onOpenMap: () => {}, onSeat, ...propsOver
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
    onCancelGroup: () => {}, onStart: () => {}, onMove: () => {}, onCancel: () => {}, onReject: () => {}, onMarkNoShow: () => {},
    onConfirm: () => {}, onOpenMap: () => {}, onSeat: () => {}
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

  it('cancelled offers neither lifecycle action', () => {
    const result = detail('cancelled');
    expect(result.queryByRole('button', { name: 'Подтвердить' })).toBeNull();
    expect(result.queryByRole('button', { name: 'Начать сессию' })).toBeNull();
  });

  // Посаженная бронь без запущенной сессии — это отмеченный приход: человек у стойки, машина
  // ещё не запущена. Забрать у него «Начать сессию» значило бы рвать связь брони с сессией.
  it('посаженная бронь всё ещё предлагает запустить сессию', () => {
    const result = detail('seated');
    expect(result.getByRole('button', { name: 'Начать сессию' })).toBeTruthy();
    expect(result.queryByRole('button', { name: 'Подтвердить' })).toBeNull();
    expect(result.queryByRole('button', { name: 'Пришёл' })).toBeNull();
  });
});

// Отметить неявку было нельзя ниоткуда: серверный маршрут существовал, клиентского метода не было
// вовсе, а в панели «не приехал» оставалось только читать как состояние, поставленное таймером.
// Таймер же трогает лишь брони с замороженными деньгами — остальные висели «подтверждёнными»
// вечно, занимая место в полосе.
it('неявка отмечается у начавшейся подтверждённой брони и только после подтверждения', () => {
  const onMarkNoShow = mock(() => {});
  const result = detail('confirmed', () => {}, () => {}, onMarkNoShow, Date.now() - 60_000);

  fireEvent.click(result.getByRole('button', { name: 'Не приехал' }));
  expect(onMarkNoShow).not.toHaveBeenCalled();

  fireEvent.click(result.getByRole('button', { name: 'Отметить неявку' }));
  expect(onMarkNoShow).toHaveBeenCalledTimes(1);
});

// Человек не опоздал, пока его время не наступило.
it('у ещё не начавшейся брони кнопки неявки нет', () => {
  const result = detail('confirmed', () => {}, () => {}, () => {}, Date.now() + 60_000);
  expect(result.queryByRole('button', { name: 'Не приехал' })).toBeNull();
});

// Заявка, на которую клуб сам не ответил, — это молчание стойки, а не прогул игрока.
it('у неотвеченной заявки кнопки неявки нет', () => {
  const result = detail('pending', () => {}, () => {}, () => {}, Date.now() - 60_000);
  expect(result.queryByRole('button', { name: 'Не приехал' })).toBeNull();
});

// Отметить приход было нечем. Маршрут `reservations/{id}/seat` и клиентский метод существовали с
// самого начала и не вызывались ниоткуда, а ReservationNoShowRunner смотрит ровно на SeatedAtUtc:
// игрок, который приехал и стоит у стойки — платит, выбирает пакет, ждёт освобождения места, — по
// истечении grace-окна филиала получал удержанную предоплату и испорченную репутацию. Единственным
// способом снять этот взвод было «Начать сессию», то есть посадить человека за машину прямо сейчас.
it('приход отмечается и у подтверждённой брони, и у неотвеченной заявки', () => {
  for (const state of ['confirmed', 'pending']) {
    const onSeat = mock(() => {});
    const result = detail(state, () => {}, () => {}, () => {}, Date.now() - 60_000, onSeat);

    fireEvent.click(result.getByRole('button', { name: 'Пришёл' }));
    expect(onSeat).toHaveBeenCalledTimes(1);
    cleanup();
  }
});

// Уже посаженного сажать некуда, отменённую бронь — тем более: сервер откажет, и кнопка обещала бы
// несуществующее.
it('у посаженной и отменённой брони кнопки прихода нет', () => {
  for (const state of ['seated', 'cancelled', 'no_show']) {
    const result = detail(state);
    expect(result.queryByRole('button', { name: 'Пришёл' })).toBeNull();
    cleanup();
  }
});

// Серое «Перенести на место» с прочерком не говорило, что переносить некуда: список предлагает
// только места, свободные прямо сейчас.
describe('BookingDrawer · почему нельзя перенести', () => {
  it('называет причину, когда свободных мест нет', () => {
    const result = detail('confirmed');
    const move = result.getByRole('combobox', { name: 'Перенести на место' });
    expect(move).toBeDisabled();
    expect(move).toHaveTextContent('Сейчас свободных мест нет');
  });

  it('оставляет прочерк, когда переносить есть куда', () => {
    const result = detail('confirmed', () => {}, () => {}, () => {}, Date.now() + 60_000, () => {}, {
      freeSeats: [seat({ id: 'c7', name: 'PC-07' })]
    });
    const move = result.getByRole('combobox', { name: 'Перенести на место' });
    expect(move).not.toBeDisabled();
    expect(move).not.toHaveTextContent('Сейчас свободных мест нет');
  });
});

// Заявка из приложения может прийти без места. У такой брони «Открыть карту», «Начать сессию»
// и «Пришёл» были серыми молча.
describe('BookingDrawer · бронь без места', () => {
  it('говорит, что сначала нужно выбрать место', () => {
    const result = detail('confirmed', () => {}, () => {}, () => {}, Date.now() + 60_000, () => {}, { seatId: '' });
    const reason = result.getByText(/У брони нет места/);
    for (const name of ['Открыть карту', 'Начать сессию', 'Пришёл']) {
      const button = result.getByRole('button', { name });
      expect(button).toBeDisabled();
      expect(button.getAttribute('aria-describedby')).toBe(reason.id);
    }
  });

  it('молчит, когда место в брони есть', () => {
    const result = detail('confirmed');
    expect(result.queryByText(/У брони нет места/)).toBeNull();
    expect(result.getByRole('button', { name: 'Открыть карту' }).getAttribute('aria-describedby')).toBeNull();
  });
});
