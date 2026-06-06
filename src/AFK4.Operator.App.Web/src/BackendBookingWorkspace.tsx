import { useEffect, useState } from 'react';
import { ArrowRightLeft, MonitorCheck, Plus, Square, UserRoundPlus } from 'lucide-react';
import { projectOperatorError } from './apiErrors';
import { createOperatorApiClients, type ReservationSearchResultDto } from './operatorApiClients';
import type { OperatorFloorMapState } from './floorMapState';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  addMinutes,
  createAuthenticatedOperatorClients,
  emptyFeedback,
  formatTime,
  problemTones,
  readArray,
  readNumber,
  readString,
  requireBackend,
  toDateInputValue,
  toDateTimeInputValue
} from './operatorHelpers';
import { FeedbackNotice, StateFlag } from './operatorPrimitives';

export function BackendBookingWorkspace({
  floorMap,
  backend,
  onOpenSeat
}: {
  floorMap: OperatorFloorMapState;
  backend: OperatorBackendContext | null;
  onOpenSeat: (seatId: string) => void;
}) {
  const [selectedBookingIndex, setSelectedBookingIndex] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [reservationResult, setReservationResult] = useState<ReservationSearchResultDto | null>(null);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('loading');
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadVersion, setReloadVersion] = useState(0);
  const [draftCustomerName, setDraftCustomerName] = useState('Гость');
  const [draftPhoneNumber, setDraftPhoneNumber] = useState('');
  const [draftStartsAt, setDraftStartsAt] = useState(() => toDateTimeInputValue(addMinutes(new Date(), 15)));
  const [draftDurationMinutes, setDraftDurationMinutes] = useState(60);
  const readySeats = floorMap.seats.filter((seat) => seat.tone === 'ready' && !seat.activeSessionId);
  const activeSeats = floorMap.seats.filter((seat) => seat.tone === 'active' || seat.activeSessionId);
  const problemSeats = floorMap.seats.filter((seat) => problemTones.has(seat.tone));
  const today = new Date();
  const bookingFromUtc = `${toDateInputValue(today)}T00:00:00.000Z`;
  const bookingToUtc = `${toDateInputValue(today)}T23:59:59.999Z`;

  useEffect(() => {
    let disposed = false;

    if (backend === null) {
      setReservationResult(null);
      setLoadStatus('failed');
      setLoadError('Активный филиал не назначен.');
      return undefined;
    }

    setLoadStatus('loading');
    setLoadError(null);

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.reservations.search(backend.branchId, {
      fromUtc: bookingFromUtc,
      toUtc: bookingToUtc,
      limit: 40
    })
      .then((result) => {
        if (disposed) {
          return;
        }

        setReservationResult(result);
        setLoadStatus('backend');
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setReservationResult(null);
        setLoadStatus('failed');
        setLoadError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, bookingFromUtc, bookingToUtc, reloadVersion]);

  const reservations = readArray<Record<string, unknown>>(reservationResult, 'reservations');
  const bookings = reservations.map((reservation) => {
    const state = readString(reservation, 'state', 'pending');
    const source = readString(reservation, 'source', 'operator');
    const startsAtUtc = readString(reservation, 'startsAtUtc');
    const durationMinutes = readNumber(reservation, 'durationMinutes', 60);
    const seatName = readString(reservation, 'seatName', '');
    const zoneName = readString(reservation, 'zoneName', 'Без места');
    const tone = state === 'cancelled'
      ? 'blocking'
      : state === 'seated'
        ? 'confirmed'
        : source === 'online'
          ? 'online'
          : 'pending';

    return {
      reservationId: readString(reservation, 'reservationId'),
      state,
      time: formatTime(startsAtUtc),
      client: readString(reservation, 'customerName', 'Гость'),
      seats: seatName ? '1 ПК' : 'без ПК',
      zone: seatName ? `${zoneName} · ${seatName}` : zoneName,
      duration: `${durationMinutes} мин`,
      status: reservationStateLabel(state),
      tone,
      note: readString(reservation, 'note', readString(reservation, 'phoneNumber', 'без комментария')),
      seatId: readString(reservation, 'seatId'),
      source
    };
  });
  const selectedBooking = bookings[selectedBookingIndex] ?? bookings[0] ?? {
    reservationId: '',
    time: '—',
    client: loadStatus === 'failed' ? 'Брони не загружены' : 'Нет броней за сегодня',
    seats: '0 ПК',
    zone: floorMap.branchName,
    duration: '—',
    status: loadStatus === 'loading' ? 'Загрузка' : 'Пусто',
    tone: 'pending',
    note: loadError ?? 'Свободные места доступны на карте зала',
    seatId: '',
    source: 'operator',
    state: 'empty'
  };
  useEffect(() => {
    if (bookings.length > 0 && selectedBookingIndex >= bookings.length) {
      setSelectedBookingIndex(0);
    }
  }, [bookings.length, selectedBookingIndex]);

  const onlineRequests = bookings.filter((booking) => booking.source === 'online' && booking.state === 'pending');
  const selectedReadySeat = readySeats.find((seat) => seat.id === selectedBooking.seatId) ?? readySeats[0] ?? null;
  const reservationBusy = feedback.state === 'pending';
  const canManageReservations = backend !== null && hasPermission(backend.session, permissionNames.manageReservations);
  const hasSelectedReservation = selectedBooking.reservationId.length > 0;
  const selectedReservationActive = selectedBooking.state !== 'seated' && selectedBooking.state !== 'cancelled' && selectedBooking.state !== 'empty';
  const canCreateReservation = canManageReservations && loadStatus === 'backend' && selectedReadySeat !== null && !reservationBusy;
  const canSeatSelectedReservation = canManageReservations && hasSelectedReservation && selectedReservationActive && selectedBooking.seatId.length > 0 && !reservationBusy;
  const canMoveSelectedReservation = canManageReservations &&
    hasSelectedReservation &&
    selectedReservationActive &&
    readySeats.some((seat) => seat.id !== selectedBooking.seatId) &&
    !reservationBusy;
  const canCancelSelectedReservation = canManageReservations && hasSelectedReservation && selectedReservationActive && !reservationBusy;
  const loadLabel = loadStatus === 'backend'
    ? 'Данные платформы'
    : loadStatus === 'loading'
      ? 'Загрузка броней'
      : 'Ошибка броней';
  const runReservationAction = async (
    label: string,
    operation: (clients: ReturnType<typeof createOperatorApiClients>) => Promise<unknown>,
    afterSuccess?: () => void
  ) => {
    setFeedback({ label, state: 'pending' });

    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.manageReservations)) {
        throw new Error('Нет прав на управление бронями.');
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await operation(clients);
      setFeedback({ label, state: 'confirmed' });
      setReloadVersion((value) => value + 1);
      afterSuccess?.();
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };
  const requireSelectedReservationId = () => {
    if (!selectedBooking.reservationId) {
      throw new Error('Выберите бронь из данных платформы.');
    }

    return selectedBooking.reservationId;
  };
  const createReservation = () => runReservationAction('Создать бронь', async (clients) => {
    const nextBackend = requireBackend(backend);
    if (!selectedReadySeat) {
      throw new Error('Нет свободного места для новой брони.');
    }

    const startsAt = new Date(draftStartsAt);
    if (Number.isNaN(startsAt.getTime())) {
      throw new Error('Укажите корректное время старта брони.');
    }

    if (startsAt.getTime() < Date.now() - 60000) {
      throw new Error('Время старта брони уже прошло.');
    }

    return await clients.reservations.create(nextBackend.branchId, {
      organizationId: nextBackend.session.organizationId,
      seatId: selectedReadySeat.id,
      customerName: draftCustomerName.trim() || 'Гость',
      phoneNumber: draftPhoneNumber.trim() || null,
      startsAtUtc: startsAt.toISOString(),
      durationMinutes: Math.max(15, draftDurationMinutes),
      source: 'operator',
      note: `Создано оператором · ${selectedReadySeat.name}`
    });
  });
  const confirmReservation = (reservationId: string, label: string) => runReservationAction(label, async (clients) => {
    const nextBackend = requireBackend(backend);
    return await clients.reservations.confirm(reservationId, { organizationId: nextBackend.session.organizationId });
  });
  const seatReservation = () => runReservationAction('Посадить бронь', async (clients) => {
    const nextBackend = requireBackend(backend);
    return await clients.reservations.seat(requireSelectedReservationId(), { organizationId: nextBackend.session.organizationId });
  }, () => {
    if (selectedBooking.seatId) {
      onOpenSeat(selectedBooking.seatId);
    }
  });
  const moveReservation = () => runReservationAction('Перенести бронь', async (clients) => {
    const nextBackend = requireBackend(backend);
    const targetSeat = readySeats.find((seat) => seat.id !== selectedBooking.seatId);
    if (!targetSeat) {
      throw new Error('Нет другого свободного места для переноса.');
    }

    return await clients.reservations.update(requireSelectedReservationId(), {
      organizationId: nextBackend.session.organizationId,
      seatId: targetSeat.id,
      note: `Перенесено оператором · ${targetSeat.name}`
    });
  });
  const cancelReservation = () => runReservationAction('Отменить бронь', async (clients) => {
    const nextBackend = requireBackend(backend);
    return await clients.reservations.cancel(requireSelectedReservationId(), {
      organizationId: nextBackend.session.organizationId,
      reason: 'Отменено оператором'
    });
  });

  return (
    <main className="workspace-screen booking-screen">
      <section className="screen-head booking-head">
        <div>
          <span>Брони</span>
          <h1>Брони сегодня · посадка гостей и онлайн-заявки</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{loadLabel}</span>
          <button type="button" className="booking-create-action" disabled={!canCreateReservation} onClick={createReservation}><Plus size={14} />Создать</button>
        </div>
      </section>

      <section className="state-strip booking-state-strip">
        <StateFlag label="Свободно" value={String(readySeats.length)} />
        <StateFlag label="Занято" value={String(activeSeats.length)} />
        <StateFlag label="Проблемы" value={String(problemSeats.length)} critical={problemSeats.length > 0} />
        <StateFlag label="Брони" value={String(bookings.length)} critical={loadStatus === 'failed'} />
        <StateFlag label="Заявки" value={String(onlineRequests.length)} critical={onlineRequests.length > 0} />
      </section>

      <section className="booking-layout">
        <section className="booking-panel booking-timeline-panel">
          <header className="booking-panel-title">
            <span>Лента броней</span>
            <strong>активные брони из платформы</strong>
          </header>
          <div className="booking-list">
            {bookings.map((booking, index) => (
              <button
                key={`${booking.time}-${booking.seatId}`}
                type="button"
                className={`booking-card ${booking.tone}${index === selectedBookingIndex ? ' active' : ''}`}
                onClick={() => setSelectedBookingIndex(index)}
              >
                <span className="booking-time">{booking.time}</span>
                <span className="booking-client">
                  <strong>{booking.client}</strong>
                  <em>{booking.note}</em>
                </span>
                <span className="booking-meta">{booking.seats} · {booking.zone} · {booking.duration}</span>
                <b>{booking.status}</b>
              </button>
            ))}
            {bookings.length === 0 && (
              <article className="booking-card pending">
                <span className="booking-time">—</span>
                <span className="booking-client">
                  <strong>{loadStatus === 'loading' ? 'Загрузка броней' : 'Нет броней'}</strong>
                  <em>{loadError ?? 'На сегодня броней нет.'}</em>
                </span>
                <span className="booking-meta">{floorMap.branchName}</span>
                <b>{loadStatus === 'failed' ? 'Ошибка' : 'Пусто'}</b>
              </article>
            )}
          </div>
        </section>

        <section className="booking-panel booking-selected-panel">
          <header className="booking-panel-title">
            <span>Выбранная бронь</span>
            <strong>{selectedBooking.client} · {selectedBooking.time}</strong>
          </header>
          <div className={`booking-status-card ${selectedBooking.tone}`}>
            <span>{selectedBooking.status}</span>
            <strong>{selectedBooking.time}</strong>
            <em>{selectedBooking.seats} · {selectedBooking.zone} · {selectedBooking.duration}</em>
          </div>
          <div className="booking-action-grid" aria-label="Действия с бронью">
            <button type="button" disabled={!selectedBooking.seatId || reservationBusy} onClick={() => selectedBooking.seatId ? onOpenSeat(selectedBooking.seatId) : setFeedback({ label: 'Открыть карту', state: 'failed', detail: 'У выбранной брони нет места.' })}><MonitorCheck size={15} />Открыть карту</button>
            <button type="button" disabled={!canSeatSelectedReservation} onClick={seatReservation}><UserRoundPlus size={15} />Посадить</button>
            <button type="button" disabled={!canMoveSelectedReservation} onClick={moveReservation}><ArrowRightLeft size={15} />Перенести</button>
            <button type="button" className="danger" disabled={!canCancelSelectedReservation} onClick={cancelReservation}><Square size={15} />Отменить</button>
          </div>
          <FeedbackNotice feedback={feedback} />
          <div className="booking-detail-list">
            <div><span>Клиент</span><strong>{selectedBooking.client}</strong></div>
            <div><span>Комментарий</span><strong>{selectedBooking.note}</strong></div>
            <div><span>Источник</span><strong>{selectedBooking.source === 'online' ? 'онлайн-заявка' : 'оператор'}</strong></div>
          </div>
        </section>

        <section className="booking-panel booking-requests-panel">
          <header className="booking-panel-title">
            <span>Онлайн-заявки</span>
            <strong>заявки в ожидании подтверждения</strong>
          </header>
          <div className="booking-request-list">
            {onlineRequests.map((request) => (
              <article key={request.reservationId} className="booking-request-card">
                <span>{request.time}</span>
                <strong>{request.client}</strong>
                <em>{request.note}</em>
                <div>
                  <button type="button" disabled={!canManageReservations || reservationBusy} onClick={() => confirmReservation(request.reservationId, `Принять ${request.client}`)}>Принять</button>
                  <button type="button" onClick={() => {
                    const index = bookings.findIndex((booking) => booking.reservationId === request.reservationId);
                    if (index >= 0) {
                      setSelectedBookingIndex(index);
                    }
                  }}>Уточнить</button>
                </div>
              </article>
            ))}
            {onlineRequests.length === 0 && (
              <article className="booking-request-card">
                <span>—</span>
                <strong>Нет онлайн-заявок</strong>
                <em>{loadStatus === 'failed' ? loadError ?? 'Не удалось загрузить заявки.' : 'Платформа не вернула заявок в ожидании.'}</em>
              </article>
            )}
          </div>
        </section>

        <section className="booking-panel booking-create-panel">
          <header className="booking-panel-title">
            <span>Новая бронь</span>
            <strong>{selectedReadySeat ? `${selectedReadySeat.zone} · ${selectedReadySeat.name}` : 'нет свободного места'}</strong>
          </header>
          <div className="booking-form-grid">
            <label>Клиент<input value={draftCustomerName} disabled={reservationBusy} onChange={(event) => setDraftCustomerName(event.target.value)} /></label>
            <label>Телефон<input value={draftPhoneNumber} disabled={reservationBusy} onChange={(event) => setDraftPhoneNumber(event.target.value)} /></label>
            <label>Старт<input type="datetime-local" value={draftStartsAt} disabled={reservationBusy} onChange={(event) => setDraftStartsAt(event.target.value)} /></label>
            <label>Длительность<input type="number" min={15} step={15} value={draftDurationMinutes} disabled={reservationBusy} onChange={(event) => setDraftDurationMinutes(Number(event.target.value) || 60)} /></label>
          </div>
          <button type="button" className="booking-primary-action" disabled={!canCreateReservation} onClick={createReservation}>Создать бронь</button>
        </section>
      </section>
    </main>
  );
}

function reservationStateLabel(state: string) {
  switch (state) {
    case 'confirmed':
      return 'Подтверждена';
    case 'pending':
      return 'Ожидает';
    case 'seated':
      return 'Посажен';
    case 'cancelled':
      return 'Отменена';
    default:
      return state || 'Неизвестно';
  }
}
