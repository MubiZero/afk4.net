import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { createOperatorApiClients, type ReservationSearchResultDto } from './operatorApiClients';
import type { OperatorFloorMapState } from './floorMapState';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  addDays,
  addMinutes,
  createAuthenticatedOperatorClients,
  emptyFeedback,
  projectPlayerClient,
  readArray,
  requireBackend,
  toDateInputValue,
  toDateTimeInputValue,
  type PlayerClientItem
} from './operatorHelpers';
import { localPhoneDigits } from './phoneFormat';

// Старт по умолчанию выравниваем по 15 мин, чтобы он совпадал с шагом дропдауна минут.
function roundToQuarter(date: Date): Date {
  const next = new Date(date);
  next.setMinutes(Math.round(next.getMinutes() / 15) * 15, 0, 0);
  return next;
}
import { FeedbackNotice, StateFlag } from './operatorPrimitives';
import { useDeferredFlag } from './useDeferredFlag';
import {
  mapReservationsToItems,
  computeAxis,
  buildSeatRows,
  unseatedOnlineRequests,
  onlineRequestCount
} from './booking/bookingModel';
import type { BookingDraft } from './booking/BookingDrawer';
import { BookingDrawer } from './booking/BookingDrawer';
import { BookingTimeline } from './booking/BookingTimeline';
import { BookingRequestsLane } from './booking/BookingRequestsLane';
import type { SeatSummary } from './operatorData';

export function BackendBookingWorkspace({
  floorMap,
  backend,
  currencyCode,
  onOpenSeat
}: {
  floorMap: OperatorFloorMapState;
  backend: OperatorBackendContext | null;
  currencyCode: string;
  onOpenSeat: (seatId: string) => void;
}) {
  const { t, locale } = useI18n();

  const [selectedDate, setSelectedDate] = useState(() => new Date());
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [reservationResult, setReservationResult] = useState<ReservationSearchResultDto | null>(null);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>('loading');
  const [loadError, setLoadError] = useState<string | null>(null);
  const [reloadVersion, setReloadVersion] = useState(0);

  const [drawerMode, setDrawerMode] = useState<'detail' | 'create' | null>(null);
  const [selectedReservationId, setSelectedReservationId] = useState('');
  const [draft, setDraft] = useState<BookingDraft>({
    customerName: '',
    phoneNumber: '',
    playerAccountId: '',
    clientBalanceMinorUnits: null,
    clientDebtMinorUnits: null,
    startsAt: toDateTimeInputValue(roundToQuarter(addMinutes(new Date(), 15))),
    durationMinutes: 60,
    seatId: ''
  });

  // Поиск клиента клуба для привязки брони к аккаунту (если есть право просмотра клиентов).
  const searchClients = useCallback(async (query: string): Promise<PlayerClientItem[]> => {
    if (backend === null || !hasPermission(backend.session, permissionNames.viewPlayers)) {
      return [];
    }
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    const raw = await clients.players.searchPlayers(backend.branchId, query, 8);
    return (Array.isArray(raw) ? raw : []).map((player) => projectPlayerClient(player, t));
  }, [backend, t]);

  const readySeats = floorMap.seats.filter((seat) => seat.tone === 'ready' && !seat.activeSessionId);
  const activeSeats = floorMap.seats.filter((seat) => seat.tone === 'active' || seat.activeSessionId);

  const bookingFromUtc = `${toDateInputValue(selectedDate)}T00:00:00.000Z`;
  const bookingToUtc = `${toDateInputValue(selectedDate)}T23:59:59.999Z`;

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
        if (disposed) return;
        setReservationResult(result);
        setLoadStatus('backend');
      })
      .catch((error) => {
        if (disposed) return;
        setReservationResult(null);
        setLoadStatus('failed');
        setLoadError(projectOperatorError(error, t).detail);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, bookingFromUtc, bookingToUtc, reloadVersion]);

  const items = mapReservationsToItems(
    readArray<Record<string, unknown>>(reservationResult, 'reservations'),
    t('op.booking.guest')
  );

  const dayStartMs = new Date(`${toDateInputValue(selectedDate)}T00:00:00`).getTime();
  const nowMs = Date.now();
  const axis = useMemo(() => computeAxis(items, dayStartMs, nowMs), [items, dayStartMs, nowMs]);
  const { groups, unplaced: _unplaced } = useMemo(() => buildSeatRows(floorMap.seats, items, axis), [floorMap.seats, items, axis]);
  const requests = unseatedOnlineRequests(items);
  const requestCount = onlineRequestCount(items);

  const showSkeleton = useDeferredFlag(loadStatus === 'loading');

  const reservationBusy = feedback.state === 'pending';
  const canManageReservations = backend !== null && hasPermission(backend.session, permissionNames.manageReservations);

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
      setReloadVersion((v) => v + 1);
      afterSuccess?.();
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const createReservation = () => runReservationAction(t('op.booking.create.submit'), async (clients) => {
    const nextBackend = requireBackend(backend, t);
    const seatId = draft.seatId;
    if (!seatId) {
      throw new Error(t('op.booking.error.noFreeSeat'));
    }

    const startsAt = new Date(draft.startsAt);
    if (Number.isNaN(startsAt.getTime())) {
      throw new Error(t('op.booking.error.invalidStart'));
    }

    if (startsAt.getTime() < Date.now() - 60000) {
      throw new Error(t('op.booking.error.startPassed'));
    }

    const seat = floorMap.seats.find((s) => s.id === seatId);
    const localDigits = localPhoneDigits(draft.phoneNumber);
    return await clients.reservations.create(nextBackend.branchId, {
      organizationId: nextBackend.session.organizationId,
      playerAccountId: draft.playerAccountId || null,
      seatId,
      customerName: draft.customerName.trim() || t('op.booking.guest'),
      phoneNumber: localDigits ? `+992${localDigits}` : null,
      startsAtUtc: startsAt.toISOString(),
      durationMinutes: Math.max(15, draft.durationMinutes),
      source: 'operator',
      note: t('op.booking.note.created', { seat: seat?.name ?? seatId })
    });
  }, () => setDrawerMode(null));

  const confirmReservation = (reservationId: string, label: string) => runReservationAction(label, async (clients) => {
    const nextBackend = requireBackend(backend, t);
    return await clients.reservations.confirm(reservationId, { organizationId: nextBackend.session.organizationId });
  });

  const seatReservation = () => runReservationAction(t('op.booking.action.seat'), async (clients) => {
    const nextBackend = requireBackend(backend, t);
    if (!selectedReservationId) throw new Error(t('op.booking.error.selectReservation'));
    return await clients.reservations.seat(selectedReservationId, { organizationId: nextBackend.session.organizationId });
  }, () => {
    const item = items.find((i) => i.reservationId === selectedReservationId);
    if (item?.seatId) onOpenSeat(item.seatId);
  });

  const moveReservation = (targetSeatId: string) => runReservationAction(t('op.booking.action.move'), async (clients) => {
    const nextBackend = requireBackend(backend, t);
    if (!selectedReservationId) throw new Error(t('op.booking.error.selectReservation'));
    const seat = floorMap.seats.find((s) => s.id === targetSeatId);
    return await clients.reservations.update(selectedReservationId, {
      organizationId: nextBackend.session.organizationId,
      seatId: targetSeatId,
      note: t('op.booking.note.moved', { seat: seat?.name ?? targetSeatId })
    });
  });

  const cancelReservation = () => runReservationAction(t('op.booking.action.cancel'), async (clients) => {
    const nextBackend = requireBackend(backend, t);
    if (!selectedReservationId) throw new Error(t('op.booking.error.selectReservation'));
    return await clients.reservations.cancel(selectedReservationId, {
      organizationId: nextBackend.session.organizationId,
      reason: t('op.booking.note.cancelReason')
    });
  });

  const openCreateDrawer = () => {
    setFeedback(emptyFeedback);
    setDraft({
      customerName: '',
      phoneNumber: '',
      playerAccountId: '',
      clientBalanceMinorUnits: null,
      clientDebtMinorUnits: null,
      startsAt: toDateTimeInputValue(roundToQuarter(addMinutes(new Date(), 15))),
      durationMinutes: 60,
      seatId: readySeats[0]?.id ?? ''
    });
    setDrawerMode('create');
  };

  const openCreateDrawerForCell = (seat: SeatSummary, startMs: number, durationMinutes = 60) => {
    setFeedback(emptyFeedback);
    setDraft({
      customerName: '',
      phoneNumber: '',
      playerAccountId: '',
      clientBalanceMinorUnits: null,
      clientDebtMinorUnits: null,
      startsAt: toDateTimeInputValue(new Date(startMs)),
      durationMinutes,
      seatId: seat.id
    });
    setDrawerMode('create');
  };

  const openDetailDrawer = (reservationId: string) => {
    setFeedback(emptyFeedback);
    setSelectedReservationId(reservationId);
    setDrawerMode('detail');
  };

  const selectedItem = items.find((i) => i.reservationId === selectedReservationId) ?? null;

  // Подсветка выбранного интервала на таймлайне, пока открыто окно создания — следует за формой.
  const previewStartMs = drawerMode === 'create' ? new Date(draft.startsAt).getTime() : Number.NaN;
  const draftEndMs = previewStartMs + Math.max(15, draft.durationMinutes) * 60_000;
  const previewBlock = drawerMode === 'create' && draft.seatId && Number.isFinite(previewStartMs)
    ? { seatId: draft.seatId, startMs: previewStartMs, endMs: draftEndMs }
    : null;

  // Конфликт: бронь на это же место, пересекающаяся по времени (пред-проверка до отправки).
  const conflict = drawerMode === 'create' && draft.seatId && Number.isFinite(previewStartMs)
    ? items.find((item) => item.seatId === draft.seatId && item.state !== 'cancelled' && item.startMs < draftEndMs && previewStartMs < item.endMs) ?? null
    : null;

  const dateLabel = toDateInputValue(selectedDate) === toDateInputValue(new Date())
    ? t('op.booking.dateNav.today')
    : new Date(selectedDate).toLocaleDateString(locale, { day: '2-digit', month: 'long' });

  return (
    <main className="workspace-screen booking-screen">
      <section className="screen-head booking-head">
        <div>
          <h1>{t('op.booking.title')}</h1>
        </div>
      </section>

      <section className="state-strip booking-state-strip">
        <StateFlag label={t('op.booking.strip.busy')} value={String(activeSeats.length)} />
        <StateFlag label={t('op.booking.strip.free')} value={String(readySeats.length)} />
        <StateFlag label={t('op.booking.strip.requests')} value={String(requestCount)} tone={requestCount > 0 ? 'warning' : undefined} />
      </section>

      {loadStatus === 'failed' && (
        <FeedbackNotice feedback={{ label: t('op.booking.eyebrow'), state: 'failed', detail: loadError ?? t('op.booking.load.failed') }} />
      )}

      {!drawerMode && <FeedbackNotice feedback={feedback} />}

      <BookingRequestsLane
        requests={requests}
        busy={reservationBusy}
        canManage={canManageReservations}
        onCreate={openCreateDrawer}
        onAccept={(item) => confirmReservation(item.reservationId, t('op.booking.requests.acceptLabel', { client: item.customerName }))}
        onClarify={(item) => openDetailDrawer(item.reservationId)}
      />

      <section className={`booking-layout${drawerMode ? ' with-drawer' : ''}`}>
        <BookingTimeline
          groups={groups}
          axis={axis}
          nowMs={nowMs}
          loading={loadStatus === 'loading'}
          showSkeleton={showSkeleton}
          selectedReservationId={selectedReservationId}
          branchName={floorMap.branchName}
          previewBlock={previewBlock}
          dateLabel={dateLabel}
          onPrevDay={() => setSelectedDate((d) => addDays(d, -1))}
          onNextDay={() => setSelectedDate((d) => addDays(d, 1))}
          onSelectBlock={(item) => openDetailDrawer(item.reservationId)}
          onCellCreate={openCreateDrawerForCell}
        />

        {drawerMode && (
          <BookingDrawer
            mode={drawerMode}
            selected={selectedItem}
            freeSeats={readySeats}
            allSeats={floorMap.seats}
            draft={draft}
            feedback={feedback}
            busy={reservationBusy}
            canManage={canManageReservations}
            currencyCode={currencyCode}
            conflict={conflict}
            searchClients={searchClients}
            onClose={() => setDrawerMode(null)}
            onChangeDraft={(patch) => setDraft((d) => ({ ...d, ...patch }))}
            onCreate={createReservation}
            onSeat={seatReservation}
            onMove={moveReservation}
            onCancel={cancelReservation}
            onConfirm={(item) => confirmReservation(item.reservationId, t('op.booking.requests.acceptLabel', { client: item.customerName }))}
            onOpenMap={onOpenSeat}
          />
        )}
      </section>
    </main>
  );
}
