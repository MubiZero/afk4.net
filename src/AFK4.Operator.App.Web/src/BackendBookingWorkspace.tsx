import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { createOperatorApiClients, type ReservationSearchResultDto, type SessionTimelineResult } from './operatorApiClients';
import { PlatformApiError } from './platformApi';
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
import { StateFlag } from './operatorPrimitives';
import { useDeferredFlag } from './useDeferredFlag';
import { useFeedbackToasts } from './useFeedbackToasts';
import {
  mapReservationsToItems,
  mapSessionDtosToItems,
  computeAxis,
  buildSeatRows,
  unseatedOnlineRequests,
  onlineRequestCount,
  type SessionDtoLike
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
  useFeedbackToasts(feedback);
  const [reservationResult, setReservationResult] = useState<ReservationSearchResultDto | null>(null);
  const [sessionResult, setSessionResult] = useState<SessionTimelineResult | null>(null);
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
    seatId: '',
    seatIds: []
  });
  const [groupConflicts, setGroupConflicts] = useState<Set<string>>(new Set());

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

  // Сессии за тот же день — отдельный best-effort fetch: их слой не должен валить экран броней,
  // поэтому при ошибке просто скрываем сессии, а заявки/брони продолжают грузиться своим путём.
  useEffect(() => {
    let disposed = false;
    if (backend === null) {
      setSessionResult(null);
      return undefined;
    }

    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    if (!hasPermission(backend.session, permissionNames.viewSessions)) {
      setSessionResult(null);
      return undefined;
    }

    clients.sessions.timeline(backend.branchId, { fromUtc: bookingFromUtc, toUtc: bookingToUtc, limit: null })
      .then((result) => { if (!disposed) setSessionResult(result); })
      .catch(() => { if (!disposed) setSessionResult(null); });

    return () => { disposed = true; };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, bookingFromUtc, bookingToUtc, reloadVersion]);

  const items = mapReservationsToItems(
    readArray<Record<string, unknown>>(reservationResult, 'reservations'),
    t('op.booking.guest')
  );

  const dayStartMs = new Date(`${toDateInputValue(selectedDate)}T00:00:00`).getTime();
  const nowMs = Date.now();
  const axis = useMemo(() => computeAxis(items, dayStartMs, nowMs), [items, dayStartMs, nowMs]);
  // Сессии (активные и завершённые) приходят отдельным эндпоинтом и привязываются к строкам мест
  // по seatId — единый источник, независимый от снимка floor-map.
  const sessionItems = useMemo(
    () => mapSessionDtosToItems(readArray<SessionDtoLike>(sessionResult, 'sessions')),
    [sessionResult]
  );
  const { groups, unplaced: _unplaced } = useMemo(() => buildSeatRows(floorMap.seats, items, axis, sessionItems), [floorMap.seats, items, axis, sessionItems]);
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

  // Массовая бронь: один групповой вызов на набор мест. На 409 (хотя бы один ПК занят) — сервер
  // ничего не пишет и возвращает список конфликтных мест; подсвечиваем их в окне, drawer не закрываем.
  const createGroupReservation = async () => {
    const label = t('op.booking.create.submitGroup', { count: draft.seatIds.length });
    setFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageReservations)) {
        throw new Error(t('op.booking.error.noPermission'));
      }
      if (draft.seatIds.length === 0) {
        throw new Error(t('op.booking.error.noFreeSeat'));
      }
      const startsAt = new Date(draft.startsAt);
      if (Number.isNaN(startsAt.getTime())) {
        throw new Error(t('op.booking.error.invalidStart'));
      }
      if (startsAt.getTime() < Date.now() - 60000) {
        throw new Error(t('op.booking.error.startPassed'));
      }
      const localDigits = localPhoneDigits(draft.phoneNumber);
      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await clients.reservations.createGroup(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        playerAccountId: draft.playerAccountId || null,
        seatIds: draft.seatIds,
        customerName: draft.customerName.trim() || t('op.booking.guest'),
        phoneNumber: localDigits ? `+992${localDigits}` : null,
        startsAtUtc: startsAt.toISOString(),
        durationMinutes: Math.max(15, draft.durationMinutes),
        source: 'operator',
        note: t('op.booking.note.group', { count: draft.seatIds.length })
      });
      setGroupConflicts(new Set());
      setFeedback({ label, state: 'confirmed' });
      setReloadVersion((v) => v + 1);
      setDrawerMode(null);
    } catch (error) {
      if (error instanceof PlatformApiError && error.status === 409) {
        try {
          const body = JSON.parse(error.body) as { conflicts?: Array<{ seatId: string }> };
          setGroupConflicts(new Set((body.conflicts ?? []).map((c) => c.seatId)));
          setFeedback({ label, state: 'failed', detail: t('op.booking.group.conflict') });
          return;
        } catch {
          // тело не разобрать — падаем в общий обработчик ниже
        }
      }
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

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

  // Отмена всей группы: отменяем каждую активную бронь с тем же ReservationGroupId по очереди.
  const cancelReservationGroup = () => runReservationAction(t('op.booking.group.cancelAll'), async (clients) => {
    const nextBackend = requireBackend(backend, t);
    const groupId = selectedItem?.reservationGroupId;
    if (!groupId) throw new Error(t('op.booking.error.selectReservation'));
    const members = items.filter((item) => item.reservationGroupId === groupId && item.state !== 'cancelled');
    for (const member of members) {
      await clients.reservations.cancel(member.reservationId, {
        organizationId: nextBackend.session.organizationId,
        reason: t('op.booking.note.cancelReason')
      });
    }
  }, () => setDrawerMode(null));

  const openCreateDrawer = () => {
    setFeedback(emptyFeedback);
    setGroupConflicts(new Set());
    setDraft({
      customerName: '',
      phoneNumber: '',
      playerAccountId: '',
      clientBalanceMinorUnits: null,
      clientDebtMinorUnits: null,
      startsAt: toDateTimeInputValue(roundToQuarter(addMinutes(new Date(), 15))),
      durationMinutes: 60,
      seatId: readySeats[0]?.id ?? '',
      seatIds: []
    });
    setDrawerMode('create');
  };

  const openCreateDrawerForCell = (seat: SeatSummary, startMs: number, durationMinutes = 60) => {
    setFeedback(emptyFeedback);
    setGroupConflicts(new Set());
    setDraft({
      customerName: '',
      phoneNumber: '',
      playerAccountId: '',
      clientBalanceMinorUnits: null,
      clientDebtMinorUnits: null,
      startsAt: toDateTimeInputValue(new Date(startMs)),
      durationMinutes,
      seatId: seat.id,
      seatIds: []
    });
    setDrawerMode('create');
  };

  // Протягивание по нескольким строкам → массовая (групповая) бронь: окно создания с набором мест.
  const openGroupDrawer = (seats: SeatSummary[], startMs: number, durationMinutes: number) => {
    setFeedback(emptyFeedback);
    setGroupConflicts(new Set());
    setDraft({
      customerName: '',
      phoneNumber: '',
      playerAccountId: '',
      clientBalanceMinorUnits: null,
      clientDebtMinorUnits: null,
      startsAt: toDateTimeInputValue(new Date(startMs)),
      durationMinutes,
      seatId: '',
      seatIds: seats.map((seat) => seat.id)
    });
    setDrawerMode('create');
  };

  // Ctrl/Command-клик дополняет текущий черновик произвольным местом, не меняя общий
  // интервал после первого выбора. Текущее состояние ПК не фильтруем: для будущего времени
  // авторитетны фактические пересечения, рассчитанные ниже и повторно проверяемые сервером.
  const toggleDraftSeat = (seat: SeatSummary, startMs: number) => {
    const continueCurrentDraft = drawerMode === 'create';
    setFeedback(emptyFeedback);
    setGroupConflicts(new Set());
    setDrawerMode('create');
    setDraft((current) => {
      if (!continueCurrentDraft) {
        return {
          customerName: '',
          phoneNumber: '',
          playerAccountId: '',
          clientBalanceMinorUnits: null,
          clientDebtMinorUnits: null,
          startsAt: toDateTimeInputValue(new Date(startMs)),
          durationMinutes: 60,
          seatId: '',
          seatIds: [seat.id]
        };
      }
      const basis = current.seatIds.length > 0 ? current.seatIds : current.seatId ? [current.seatId] : [];
      const seatIds = basis.includes(seat.id) ? basis.filter((id) => id !== seat.id) : [...basis, seat.id];
      return {
        ...current,
        seatId: '',
        seatIds,
        startsAt: basis.length === 0 ? toDateTimeInputValue(new Date(startMs)) : current.startsAt
      };
    });
  };

  const removeGroupSeat = (seatId: string) => {
    setDraft((d) => ({ ...d, seatIds: d.seatIds.filter((id) => id !== seatId) }));
    setGroupConflicts((prev) => {
      if (!prev.has(seatId)) return prev;
      const next = new Set(prev);
      next.delete(seatId);
      return next;
    });
  };

  const openDetailDrawer = (reservationId: string) => {
    setFeedback(emptyFeedback);
    setSelectedReservationId(reservationId);
    setDrawerMode('detail');
  };

  const selectedItem = items.find((i) => i.reservationId === selectedReservationId) ?? null;
  const selectedGroupSize = selectedItem?.reservationGroupId
    ? items.filter((i) => i.reservationGroupId === selectedItem.reservationGroupId && i.state !== 'cancelled').length
    : 0;

  // Подсветка выбранного интервала на таймлайне, пока открыто окно создания — следует за формой.
  const previewStartMs = drawerMode === 'create' ? new Date(draft.startsAt).getTime() : Number.NaN;
  const draftEndMs = previewStartMs + Math.max(15, draft.durationMinutes) * 60_000;
  // Места для персистентной подсветки: групповой режим — весь набор, одиночный — одно место.
  const previewSeatIds = draft.seatIds.length > 0 ? draft.seatIds : (draft.seatId ? [draft.seatId] : []);
  const previewBlock = drawerMode === 'create' && previewSeatIds.length > 0 && Number.isFinite(previewStartMs)
    ? { seatIds: previewSeatIds, startMs: previewStartMs, endMs: draftEndMs }
    : null;

  // Конфликт: бронь на это же место, пересекающаяся по времени (пред-проверка до отправки).
  const conflict = drawerMode === 'create' && draft.seatId && Number.isFinite(previewStartMs)
    ? items.find((item) => item.seatId === draft.seatId && item.state !== 'cancelled' && item.startMs < draftEndMs && previewStartMs < item.endMs) ?? null
    : null;

  // Пересекается ли место по времени с активной бронью ИЛИ идущей сессией (открытая = до бесконечности).
  const seatHasClash = (seatId: string): boolean =>
    Number.isFinite(previewStartMs) && (
      items.some((item) => item.seatId === seatId && item.state !== 'cancelled' && item.startMs < draftEndMs && previewStartMs < item.endMs)
      || sessionItems.some((s) => s.seatId === seatId && s.startMs < draftEndMs && previewStartMs < (s.endMs ?? Number.POSITIVE_INFINITY))
    );

  // Проактивные конфликты массовой брони: считаем на фронте (бронь + сессия) ещё до отправки и
  // объединяем с тем, что вернул бэк на 409 — конфликтные ПК подсвечиваются и блокируют создание.
  const predictedGroupConflicts = drawerMode === 'create' && draft.seatIds.length > 0
    ? new Set(draft.seatIds.filter(seatHasClash))
    : new Set<string>();
  const mergedGroupConflicts = predictedGroupConflicts.size > 0
    ? new Set<string>([...groupConflicts, ...predictedGroupConflicts])
    : groupConflicts;

  // Одиночное место: тот же критерий, что у массовой (бронь ИЛИ сессия). `conflict` выше — только
  // бронь (для детальной подписи); это покрывает ещё и сессии и блокирует создание.
  const singleSeatConflict = drawerMode === 'create' && draft.seatId !== '' && seatHasClash(draft.seatId);

  const todayValue = toDateInputValue(new Date());
  const dateValue = toDateInputValue(selectedDate);
  const isToday = dateValue === todayValue;
  const dateLabel = isToday
    ? t('op.booking.dateNav.today')
    : new Date(selectedDate).toLocaleDateString(locale, { day: '2-digit', month: 'long' });

  return (
    <main className="workspace-screen booking-screen">
      <section className="booking-header">
        <h1><strong className="booking-header-name">{t('op.booking.title')}</strong> · <span className="booking-header-tagline">{t('op.booking.tagline')}</span></h1>
        <div className="booking-header-metrics">
          <StateFlag label={t('op.booking.strip.busy')} value={String(activeSeats.length)} />
          <StateFlag label={t('op.booking.strip.free')} value={String(readySeats.length)} />
          <StateFlag label={t('op.booking.strip.requests')} value={String(requestCount)} tone={requestCount > 0 ? 'warning' : undefined} />
        </div>
      </section>

      {loadStatus === 'failed' && (
        <p className="workspace-error" role="alert">{loadError ?? t('op.booking.load.failed')}</p>
      )}

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
          dateValue={dateValue}
          isToday={isToday}
          onPrevDay={() => setSelectedDate((d) => addDays(d, -1))}
          onNextDay={() => setSelectedDate((d) => addDays(d, 1))}
          onToday={() => setSelectedDate(new Date())}
          onPickDate={(value) => { const picked = new Date(`${value}T00:00:00`); if (!Number.isNaN(picked.getTime())) setSelectedDate(picked); }}
          onSelectBlock={(item) => openDetailDrawer(item.reservationId)}
          onCellCreate={openCreateDrawerForCell}
          onSeatsCreate={openGroupDrawer}
          onSeatToggle={toggleDraftSeat}
        />

        {drawerMode && (
          <BookingDrawer
            mode={drawerMode}
            selected={selectedItem}
            freeSeats={readySeats}
            allSeats={floorMap.seats}
            draft={draft}
            busy={reservationBusy}
            canManage={canManageReservations}
            currencyCode={currencyCode}
            conflict={conflict}
            seatConflict={singleSeatConflict}
            groupConflicts={mergedGroupConflicts}
            groupSize={selectedGroupSize}
            searchClients={searchClients}
            onClose={() => setDrawerMode(null)}
            onChangeDraft={(patch) => setDraft((d) => ({ ...d, ...patch }))}
            onCreate={createReservation}
            onCreateGroup={createGroupReservation}
            onRemoveSeat={removeGroupSeat}
            onCancelGroup={cancelReservationGroup}
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
