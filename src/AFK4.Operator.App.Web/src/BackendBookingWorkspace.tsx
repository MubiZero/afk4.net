import { useEffect, useState } from 'react';
import { ArrowRightLeft, MonitorCheck, Plus, Square, UserRoundPlus } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
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
  const { t } = useI18n();
  const [selectedBookingIndex, setSelectedBookingIndex] = useState(0);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [reservationResult, setReservationResult] = useState<ReservationSearchResultDto | null>(null);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('loading');
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadVersion, setReloadVersion] = useState(0);
  const [draftCustomerName, setDraftCustomerName] = useState(() => t('op.booking.guest'));
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
      setLoadError(t('op.booking.error.noBranch'));
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
        setLoadError(projectOperatorError(error, t).detail);
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
    const zoneName = readString(reservation, 'zoneName', t('op.booking.zoneNone'));
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
      client: readString(reservation, 'customerName', t('op.booking.guest')),
      seats: seatName ? t('op.booking.seatsOne') : t('op.booking.seatsNone'),
      zone: seatName ? `${zoneName} · ${seatName}` : zoneName,
      duration: t('op.booking.durationMin', { count: durationMinutes }),
      status: reservationStateLabel(state, t),
      tone,
      note: readString(reservation, 'note', readString(reservation, 'phoneNumber', t('op.booking.noComment'))),
      seatId: readString(reservation, 'seatId'),
      source
    };
  });
  const selectedBooking = bookings[selectedBookingIndex] ?? bookings[0] ?? {
    reservationId: '',
    time: '—',
    client: loadStatus === 'failed' ? t('op.booking.fallback.failedClient') : t('op.booking.fallback.emptyClient'),
    seats: t('op.booking.fallback.zeroSeats'),
    zone: floorMap.branchName,
    duration: '—',
    status: loadStatus === 'loading' ? t('a11y.loading') : t('state.empty'),
    tone: 'pending',
    note: loadError ?? t('op.booking.fallback.note'),
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
    ? t('op.booking.load.backend')
    : loadStatus === 'loading'
      ? t('op.booking.load.loading')
      : t('op.booking.load.failed');
  const runReservationAction = async (
    label: string,
    operation: (clients: ReturnType<typeof createOperatorApiClients>) => Promise<unknown>,
    afterSuccess?: () => void
  ) => {
    setFeedback({ label, state: 'pending' });

    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageReservations)) {
        throw new Error(t('op.booking.error.noPermission'));
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await operation(clients);
      setFeedback({ label, state: 'confirmed' });
      setReloadVersion((value) => value + 1);
      afterSuccess?.();
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };
  const requireSelectedReservationId = () => {
    if (!selectedBooking.reservationId) {
      throw new Error(t('op.booking.error.selectReservation'));
    }

    return selectedBooking.reservationId;
  };
  const createReservation = () => runReservationAction(t('op.booking.create.submit'), async (clients) => {
    const nextBackend = requireBackend(backend, t);
    if (!selectedReadySeat) {
      throw new Error(t('op.booking.error.noFreeSeat'));
    }

    const startsAt = new Date(draftStartsAt);
    if (Number.isNaN(startsAt.getTime())) {
      throw new Error(t('op.booking.error.invalidStart'));
    }

    if (startsAt.getTime() < Date.now() - 60000) {
      throw new Error(t('op.booking.error.startPassed'));
    }

    return await clients.reservations.create(nextBackend.branchId, {
      organizationId: nextBackend.session.organizationId,
      seatId: selectedReadySeat.id,
      customerName: draftCustomerName.trim() || t('op.booking.guest'),
      phoneNumber: draftPhoneNumber.trim() || null,
      startsAtUtc: startsAt.toISOString(),
      durationMinutes: Math.max(15, draftDurationMinutes),
      source: 'operator',
      note: t('op.booking.note.created', { seat: selectedReadySeat.name })
    });
  });
  const confirmReservation = (reservationId: string, label: string) => runReservationAction(label, async (clients) => {
    const nextBackend = requireBackend(backend, t);
    return await clients.reservations.confirm(reservationId, { organizationId: nextBackend.session.organizationId });
  });
  const seatReservation = () => runReservationAction(t('op.booking.action.seat'), async (clients) => {
    const nextBackend = requireBackend(backend, t);
    return await clients.reservations.seat(requireSelectedReservationId(), { organizationId: nextBackend.session.organizationId });
  }, () => {
    if (selectedBooking.seatId) {
      onOpenSeat(selectedBooking.seatId);
    }
  });
  const moveReservation = () => runReservationAction(t('op.booking.action.move'), async (clients) => {
    const nextBackend = requireBackend(backend, t);
    const targetSeat = readySeats.find((seat) => seat.id !== selectedBooking.seatId);
    if (!targetSeat) {
      throw new Error(t('op.booking.error.noOtherSeat'));
    }

    return await clients.reservations.update(requireSelectedReservationId(), {
      organizationId: nextBackend.session.organizationId,
      seatId: targetSeat.id,
      note: t('op.booking.note.moved', { seat: targetSeat.name })
    });
  });
  const cancelReservation = () => runReservationAction(t('op.booking.action.cancel'), async (clients) => {
    const nextBackend = requireBackend(backend, t);
    return await clients.reservations.cancel(requireSelectedReservationId(), {
      organizationId: nextBackend.session.organizationId,
      reason: t('op.booking.note.cancelReason')
    });
  });

  return (
    <main className="workspace-screen booking-screen">
      <section className="screen-head booking-head">
        <div>
          <span>{t('op.booking.eyebrow')}</span>
          <h1>{t('op.booking.title')}</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{loadLabel}</span>
          <button type="button" className="booking-create-action" disabled={!canCreateReservation} onClick={createReservation}><Plus size={14} />{t('op.booking.createBtn')}</button>
        </div>
      </section>

      <section className="state-strip booking-state-strip">
        <StateFlag label={t('op.booking.strip.free')} value={String(readySeats.length)} />
        <StateFlag label={t('op.booking.strip.busy')} value={String(activeSeats.length)} />
        <StateFlag label={t('op.booking.strip.problems')} value={String(problemSeats.length)} critical={problemSeats.length > 0} />
        <StateFlag label={t('op.booking.strip.bookings')} value={String(bookings.length)} critical={loadStatus === 'failed'} />
        <StateFlag label={t('op.booking.strip.requests')} value={String(onlineRequests.length)} critical={onlineRequests.length > 0} />
      </section>

      <section className="booking-layout">
        <section className="booking-panel booking-timeline-panel">
          <header className="booking-panel-title">
            <span>{t('op.booking.timeline.title')}</span>
            <strong>{t('op.booking.timeline.subtitle')}</strong>
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
                  <strong>{loadStatus === 'loading' ? t('op.booking.load.loading') : t('op.booking.timeline.empty')}</strong>
                  <em>{loadError ?? t('op.booking.timeline.emptyDetail')}</em>
                </span>
                <span className="booking-meta">{floorMap.branchName}</span>
                <b>{loadStatus === 'failed' ? t('op.booking.timeline.errorTag') : t('state.empty')}</b>
              </article>
            )}
          </div>
        </section>

        <section className="booking-panel booking-selected-panel">
          <header className="booking-panel-title">
            <span>{t('op.booking.selected.title')}</span>
            <strong>{selectedBooking.client} · {selectedBooking.time}</strong>
          </header>
          <div className={`booking-status-card ${selectedBooking.tone}`}>
            <span>{selectedBooking.status}</span>
            <strong>{selectedBooking.time}</strong>
            <em>{selectedBooking.seats} · {selectedBooking.zone} · {selectedBooking.duration}</em>
          </div>
          <div className="booking-action-grid" aria-label={t('op.booking.actions.aria')}>
            <button type="button" disabled={!selectedBooking.seatId || reservationBusy} onClick={() => selectedBooking.seatId ? onOpenSeat(selectedBooking.seatId) : setFeedback({ label: t('op.booking.actions.openMap'), state: 'failed', detail: t('op.booking.actions.openMapNoSeat') })}><MonitorCheck size={15} />{t('op.booking.actions.openMap')}</button>
            <button type="button" disabled={!canSeatSelectedReservation} onClick={seatReservation}><UserRoundPlus size={15} />{t('op.booking.actions.seat')}</button>
            <button type="button" disabled={!canMoveSelectedReservation} onClick={moveReservation}><ArrowRightLeft size={15} />{t('op.booking.actions.move')}</button>
            <button type="button" className="danger" disabled={!canCancelSelectedReservation} onClick={cancelReservation}><Square size={15} />{t('op.booking.actions.cancel')}</button>
          </div>
          <FeedbackNotice feedback={feedback} />
          <div className="booking-detail-list">
            <div><span>{t('op.booking.client')}</span><strong>{selectedBooking.client}</strong></div>
            <div><span>{t('op.booking.detail.comment')}</span><strong>{selectedBooking.note}</strong></div>
            <div><span>{t('op.booking.detail.source')}</span><strong>{selectedBooking.source === 'online' ? t('op.booking.source.online') : t('op.booking.source.operator')}</strong></div>
          </div>
        </section>

        <section className="booking-panel booking-requests-panel">
          <header className="booking-panel-title">
            <span>{t('op.booking.requests.title')}</span>
            <strong>{t('op.booking.requests.subtitle')}</strong>
          </header>
          <div className="booking-request-list">
            {onlineRequests.map((request) => (
              <article key={request.reservationId} className="booking-request-card">
                <span>{request.time}</span>
                <strong>{request.client}</strong>
                <em>{request.note}</em>
                <div>
                  <button type="button" disabled={!canManageReservations || reservationBusy} onClick={() => confirmReservation(request.reservationId, t('op.booking.requests.acceptLabel', { client: request.client }))}>{t('op.booking.requests.accept')}</button>
                  <button type="button" onClick={() => {
                    const index = bookings.findIndex((booking) => booking.reservationId === request.reservationId);
                    if (index >= 0) {
                      setSelectedBookingIndex(index);
                    }
                  }}>{t('op.booking.requests.clarify')}</button>
                </div>
              </article>
            ))}
            {onlineRequests.length === 0 && (
              <article className="booking-request-card">
                <span>—</span>
                <strong>{t('op.booking.requests.empty')}</strong>
                <em>{loadStatus === 'failed' ? loadError ?? t('op.booking.requests.emptyFailed') : t('op.booking.requests.emptyDetail')}</em>
              </article>
            )}
          </div>
        </section>

        <section className="booking-panel booking-create-panel">
          <header className="booking-panel-title">
            <span>{t('op.booking.create.title')}</span>
            <strong>{selectedReadySeat ? `${selectedReadySeat.zone} · ${selectedReadySeat.name}` : t('op.booking.create.noSeat')}</strong>
          </header>
          <div className="booking-form-grid">
            <label>{t('op.booking.client')}<input value={draftCustomerName} disabled={reservationBusy} onChange={(event) => setDraftCustomerName(event.target.value)} /></label>
            <label>{t('clients.field.phone')}<input value={draftPhoneNumber} disabled={reservationBusy} onChange={(event) => setDraftPhoneNumber(event.target.value)} /></label>
            <label>{t('op.booking.create.start')}<input type="datetime-local" value={draftStartsAt} disabled={reservationBusy} onChange={(event) => setDraftStartsAt(event.target.value)} /></label>
            <label>{t('op.booking.create.duration')}<input type="number" min={15} step={15} value={draftDurationMinutes} disabled={reservationBusy} onChange={(event) => setDraftDurationMinutes(Number(event.target.value) || 60)} /></label>
          </div>
          <button type="button" className="booking-primary-action" disabled={!canCreateReservation} onClick={createReservation}>{t('op.booking.create.submit')}</button>
        </section>
      </section>
    </main>
  );
}

function reservationStateLabel(
  state: string,
  t: (key: MessageKey, values?: Record<string, string | number>) => string
) {
  switch (state) {
    case 'confirmed':
      return t('op.booking.state.confirmed');
    case 'pending':
      return t('op.booking.state.pending');
    case 'seated':
      return t('op.booking.state.seated');
    case 'cancelled':
      return t('op.booking.state.cancelled');
    default:
      return state || t('op.booking.state.unknown');
  }
}
