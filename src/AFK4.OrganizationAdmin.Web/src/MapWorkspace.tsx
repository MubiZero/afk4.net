import { useCallback, useEffect, useMemo, useState } from 'react';
import type { MouseEvent as ReactMouseEvent } from 'react';
import { WifiOff } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { useDeferredFlag } from './useDeferredFlag';
import { offlineBannerText, type OperatorFloorMapState } from './floorMapState';
import type { OperatorAuthSession } from './authClient';
import type { Feedback, MapFilterId, PcControlActionId, PcControlActionOptions, PcControlActionResult, SeatActionRequest, SeatActionResult } from './operatorTypes';
import type { SeatSummary } from './operatorData';
import {
  countByMapFilter,
  emptyFeedback,
  guestBillingSelection,
  mapFilterOptions,
  matchesMapFilter,
  projectOperatorFacingError,
  zoneLabel
} from './operatorHelpers';
import { hasPermission, permissionNames } from './operatorPermissions';
import { buildBulkMenu, buildSeatMenu, type SeatMenuItem } from './seatMenu';
import { SeatSelectionBar } from './SeatSelectionBar';
import { PcCommandDialog } from './pc/PcCommandDialog';
import { planBulk, type BulkPlan } from './pc/pcBulk';
import { PC_COMMAND_CONFIRM, PC_COMMAND_LABELS, type PcCommandOrLock } from './pc/pcCommandCopy';
import { EmptyState, FeedbackNotice, Skeleton } from './operatorPrimitives';
import { SeatContextMenu } from './SeatContextMenu';
import { SeatTile } from './SeatTile';
import { useFeedbackToasts } from './useFeedbackToasts';

export function MapWorkspace({
  floorMap,
  session,
  actionsEnabled,
  selectedSeatId,
  activeFilter,
  offlineActionAudit,
  onSelectSeat,
  onStartSeat,
  onFilterChange,
  onPcControlAction,
  onResolveAssistance,
  onSeatAction
}: {
  floorMap: OperatorFloorMapState;
  session: OperatorAuthSession | null;
  actionsEnabled: boolean;
  selectedSeatId: string;
  activeFilter: MapFilterId;
  offlineActionAudit: string[];
  onSelectSeat: (seatId: string) => void;
  onStartSeat?: (seatId: string) => void;
  onFilterChange: (filter: MapFilterId) => void;
  onPcControlAction: (seat: SeatSummary, action: PcControlActionId, options?: PcControlActionOptions) => Promise<PcControlActionResult>;
  onResolveAssistance: (seat: SeatSummary) => Promise<PcControlActionResult>;
  onSeatAction: (request: SeatActionRequest) => Promise<SeatActionResult>;
}) {
  const { t } = useI18n();
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);
  const [seatMenu, setSeatMenu] = useState<{ seat: SeatSummary; x: number; y: number; bulk: boolean } | null>(null);
  // Выбранные для общей команды места — отдельно от места, открытого в карточке: карточка одна,
  // а перезагружают сразу ряд.
  const [picked, setPicked] = useState<string[]>([]);
  const [pickAnchor, setPickAnchor] = useState<string | null>(null);
  const [commandPlan, setCommandPlan] = useState<BulkPlan | null>(null);
  const [bulkBusy, setBulkBusy] = useState(false);
  const seatMenuCaps = useMemo(() => ({
    actionsEnabled,
    canStart: hasPermission(session, permissionNames.startSession),
    canExtend: hasPermission(session, permissionNames.extendSession),
    canLockUnlock: hasPermission(session, permissionNames.dispatchDeviceCommand),
    canResolveAssistance: hasPermission(session, permissionNames.resolveAssistanceRequest),
    canPause: hasPermission(session, permissionNames.pauseSession),
    canMaintain: hasPermission(session, permissionNames.maintainDevice)
  }), [actionsEnabled, session]);
  const pcAccess = useMemo(
    () => ({ canDispatch: seatMenuCaps.canLockUnlock, canMaintain: seatMenuCaps.canMaintain }),
    [seatMenuCaps]
  );
  const visibleSeats = useMemo(
    () => floorMap.seats.filter((seat) => matchesMapFilter(seat, activeFilter)),
    [activeFilter, floorMap.seats]
  );
  // Группируем места по залам в порядке появления (сорт уже задан floorMapState).
  const zoneGroups = useMemo(() => {
    const groups: { zone: string; seats: SeatSummary[] }[] = [];
    const indexByZone = new Map<string, number>();
    for (const seat of visibleSeats) {
      const at = indexByZone.get(seat.zone);
      if (at === undefined) {
        indexByZone.set(seat.zone, groups.length);
        groups.push({ zone: seat.zone, seats: [seat] });
      } else {
        groups[at].seats.push(seat);
      }
    }
    return groups;
  }, [visibleSeats]);
  const selectedSeat = floorMap.seats.find((seat) => seat.id === selectedSeatId) ?? null;
  const isLoadingSeats = floorMap.seats.length === 0 && (floorMap.loadStatus === 'loading' || floorMap.loadStatus === 'idle');
  const showSeatSkeleton = useDeferredFlag(isLoadingSeats);
  const offlineBanner = offlineBannerText(floorMap, t);
  const selectedSeatVisible = visibleSeats.some((seat) => seat.id === selectedSeatId);
  const pickedSeats = useMemo(
    () => picked.map((id) => floorMap.seats.find((seat) => seat.id === id)).filter((seat): seat is SeatSummary => seat !== undefined),
    [floorMap.seats, picked]
  );

  const clearPicks = useCallback(() => {
    setPicked([]);
    setPickAnchor(null);
  }, []);

  // Ctrl/⌘-клик добавляет место или убирает его; Shift-клик — все места подряд от прошлого выбора
  // в том порядке, в каком они видны на карте.
  const pickSeat = (seat: SeatSummary, mode: 'toggle' | 'range') => {
    if (mode === 'range' && pickAnchor !== null) {
      const order = visibleSeats.map((candidate) => candidate.id);
      const from = order.indexOf(pickAnchor);
      const to = order.indexOf(seat.id);
      if (from >= 0 && to >= 0) {
        const range = order.slice(Math.min(from, to), Math.max(from, to) + 1);
        setPicked((current) => [...current, ...range.filter((id) => !current.includes(id))]);
        return;
      }
    }

    setPicked((current) => (current.includes(seat.id) ? current.filter((id) => id !== seat.id) : [...current, seat.id]));
    setPickAnchor(seat.id);
  };

  const toggleZone = (seats: SeatSummary[]) => {
    const ids = seats.map((seat) => seat.id);
    const allIn = ids.every((id) => picked.includes(id));
    setPicked((current) => (allIn ? current.filter((id) => !ids.includes(id)) : [...current, ...ids.filter((id) => !current.includes(id))]));
  };

  // Esc снимает выбор — если над картой не открыто окно или меню: их Esc закрывает первыми.
  useEffect(() => {
    if (picked.length === 0 || commandPlan !== null || seatMenu !== null) return undefined;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') clearPicks();
    };
    document.addEventListener('keydown', onKey);
    return () => document.removeEventListener('keydown', onKey);
  }, [clearPicks, commandPlan, picked.length, seatMenu]);

  /**
   * Команда одному месту или нескольким. Одному и безопасной — сразу, как кнопкой карточки;
   * остальное — через подтверждение со списком.
   */
  const askCommand = (seats: SeatSummary[], command: PcCommandOrLock) => {
    const plan = planBulk(seats, command, pcAccess);
    if (seats.length === 1 && plan.targets.length === 1 && PC_COMMAND_CONFIRM[command] === undefined) {
      void runPlan(plan);
      return;
    }

    setCommandPlan(plan);
  };

  // По четыре запроса разом: зал в сорок ПК не должен ждать сорок запросов по очереди, а сервер —
  // получать их все в одну миллисекунду.
  const runPlan = async (plan: BulkPlan, text?: string) => {
    setCommandPlan(null);
    const label = t(PC_COMMAND_LABELS[plan.command]);
    if (plan.targets.length === 0) return;

    setBulkBusy(true);
    setFeedback({ label, state: 'pending' });
    const failed: string[] = [];
    let lastDetail: string | undefined;
    const queue = [...plan.targets];
    const worker = async () => {
      for (let seat = queue.shift(); seat !== undefined; seat = queue.shift()) {
        try {
          const result = await onPcControlAction(seat, plan.command, plan.command === 'message' ? { text } : undefined);
          lastDetail = result.detail;
        } catch (error) {
          failed.push(`${seat.name} — ${projectOperatorError(error, t).detail}`);
        }
      }
    };
    await Promise.all(Array.from({ length: Math.min(4, queue.length) }, worker));
    setBulkBusy(false);

    const sent = plan.targets.length - failed.length;
    if (plan.targets.length === 1) {
      setFeedback(failed.length === 0
        ? { label, state: 'confirmed', detail: lastDetail }
        : { label, state: 'failed', detail: failed[0] });
      return;
    }

    setFeedback(failed.length === 0
      ? { label, state: 'confirmed', detail: t('op.pc.bulk.done', { sent }) }
      : { label, state: sent > 0 ? 'confirmed' : 'failed', detail: t('op.pc.bulk.partial', { sent, total: plan.targets.length, failed: failed.join('; ') }) });
  };

  const runPcControlAction = async (action: PcControlActionId, label: string, seat: SeatSummary | null = selectedSeat) => {
    if (seat === null) {
      setFeedback({ label, state: 'failed', detail: t('op.map.selectPcDetail') });
      return;
    }

    setFeedback({ label, state: 'pending' });
    try {
      const result = await onPcControlAction(seat, action);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const runResolveAssistance = async (label: string, seat: SeatSummary) => {
    setFeedback({ label, state: 'pending' });
    try {
      const result = await onResolveAssistance(seat);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const runSeatAction = async (label: string, request: SeatActionRequest) => {
    setFeedback({ label, state: 'pending' });
    try {
      const result = await onSeatAction(request);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorFacingError(error, t) });
    }
  };

  // Правый клик: выбираем место (раскрывает карточку справа) и открываем меню у курсора.
  // Клавиатурный вызов (Menu/Shift+F10) даёт coords 0 — тогда якоримся к самому элементу.
  // Правый клик по одному из выбранных мест — меню для всех выбранных, по любому другому — для него.
  const openSeatMenu = (seat: SeatSummary, event: ReactMouseEvent) => {
    event.preventDefault();
    const bulk = picked.length > 1 && picked.includes(seat.id);
    if (!bulk) onSelectSeat(seat.id);
    const fromKeyboard = event.clientX === 0 && event.clientY === 0;
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    setSeatMenu({
      seat,
      bulk,
      x: fromKeyboard ? rect.left + 12 : event.clientX,
      y: fromKeyboard ? rect.top + 12 : event.clientY
    });
  };

  const onSeatMenuSelect = (item: SeatMenuItem) => {
    const seat = seatMenu?.seat ?? null;
    setSeatMenu(null);
    if (seat === null) {
      return;
    }

    const label = t(item.feedbackKey);
    const run = item.run;
    if (run.kind === 'start-guest') {
      void runSeatAction(label, { type: 'start', seat, billing: guestBillingSelection, durationMode: 'fixed' });
    } else if (run.kind === 'extend') {
      void runSeatAction(label, { type: 'extend', seat, minutes: run.minutes, billing: guestBillingSelection });
    } else if (run.kind === 'resolve-assistance') {
      void runResolveAssistance(label, seat);
    } else if (run.kind === 'pause' || run.kind === 'resume') {
      void runSeatAction(label, { type: run.kind, seat });
    } else if (run.kind === 'pc-command') {
      askCommand([seat], run.command);
    } else if (run.kind === 'bulk') {
      askCommand(pickedSeats, run.command);
    } else {
      void runPcControlAction(run.action, label, seat);
    }
  };

  useEffect(() => {
    if (visibleSeats.length === 0 || selectedSeatVisible) {
      return;
    }

    onSelectSeat(visibleSeats[0].id);
  }, [activeFilter, floorMap.seats, onSelectSeat, selectedSeatVisible, visibleSeats]);

  return (
    <main className="floor-workspace">
      {/* Шапка экрана на мягкой подложке: заголовок зала + фильтры-статусы в одной строке —
          единый блок (как на «Бронях»), а не голый заголовок над россыпью фильтров. */}
      <section className="map-header">
        <h1>
          <strong className="map-header-name">{floorMap.branchName}</strong>
          {' · '}
          <span className="map-header-tagline">{t('op.map.tagline')}</span>
        </h1>
        <div className="filter-row map-filter-row" aria-label={t('op.map.filterLabel')}>
          {mapFilterOptions(t).map((option) => (
            <button
              key={option.id}
              type="button"
              className={activeFilter === option.id ? 'active' : undefined}
              onClick={() => onFilterChange(option.id)}
            >
              {option.label}
              <strong>{countByMapFilter(floorMap.seats, option.id)}</strong>
            </button>
          ))}
        </div>
      </section>
      {floorMap.loadStatus === 'failed' && (
        <p className="ui-alert ui-alert--spaced" role="alert">{floorMap.error ?? t('op.map.loadError')}</p>
      )}
      {/* Только настоящий обрыв связи: данные заморожены, «только просмотр». Устаревший снимок
          при живой связи не показываем — это тех-шум для админа. */}
      {offlineBanner !== null && (
        <div className="floor-snapshot-banner offline" role="status" aria-live="polite">
          <WifiOff size={14} aria-hidden="true" />
          <span>{offlineBanner}</span>
        </div>
      )}
      {offlineActionAudit.map((note, index) => (
        <FeedbackNotice key={`offline-audit-${index}`} feedback={{ label: t('op.map.feedbackQueue'), state: 'failed', detail: note }} />
      ))}

      <section className="map-board" aria-label={t('op.map.seatsLabel')}>
        {isLoadingSeats ? (
          showSeatSkeleton ? (
            <div className="seat-grid" role="status" aria-label={t('op.map.loading')}>
              {Array.from({ length: 10 }).map((_, index) => (
                <Skeleton key={index} className="seat-skeleton" />
              ))}
            </div>
          ) : null
        ) : visibleSeats.length === 0 ? (
          // Отбор, под который ничего не подошло, и пустой филиал — разные беды: первое снимается
          // одной кнопкой, второе чинится в Управлении. Сбой загрузки уже назван строкой выше.
          activeFilter !== 'all' ? (
            <EmptyState
              title={t('op.map.emptyTitle')}
              next={{ kind: 'action', label: t('op.empty.resetFilter'), onClick: () => onFilterChange('all') }}
              className="map-empty-state"
            />
          ) : floorMap.loadStatus === 'failed' ? null : (
            <EmptyState
              title={t('op.map.noSeats.emptyTitle')}
              next={{ kind: 'elsewhere', hint: t('op.map.noSeats.emptyHint') }}
              className="map-empty-state"
            />
          )
        ) : (
          <div className="seat-zones">
            {zoneGroups.map((group) => (
              <section className="zone-group" key={group.zone}>
                <header className="zone-group-head">
                  <span>{zoneLabel(group.zone, t)}</span>
                  <strong>{group.seats.length}</strong>
                  {pcAccess.canDispatch && group.seats.some((seat) => seat.deviceId) && (
                    <button
                      type="button"
                      className="zone-group-pick"
                      aria-pressed={group.seats.every((seat) => picked.includes(seat.id))}
                      onClick={() => toggleZone(group.seats)}
                    >
                      {t(group.seats.every((seat) => picked.includes(seat.id)) ? 'op.map.pick.zoneClear' : 'op.map.pick.zone')}
                    </button>
                  )}
                </header>
                <div className="seat-grid">
                  {group.seats.map((seat) => (
                    <SeatTile
                      key={seat.id}
                      seat={seat}
                      selected={seat.id === selectedSeatId}
                      picked={picked.includes(seat.id)}
                      onPick={pcAccess.canDispatch || pcAccess.canMaintain ? (mode) => pickSeat(seat, mode) : undefined}
                      onSelect={() => onSelectSeat(seat.id)}
                      onStartSession={onStartSeat ? () => onStartSeat(seat.id) : undefined}
                      onContextMenu={(event) => openSeatMenu(seat, event)}
                    />
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </section>

      {pickedSeats.length > 0 && (
        <SeatSelectionBar
          seats={pickedSeats}
          access={pcAccess}
          busy={bulkBusy}
          onCommand={(command) => askCommand(pickedSeats, command)}
          onClear={clearPicks}
        />
      )}

      {commandPlan !== null && (
        <PcCommandDialog plan={commandPlan} onCancel={() => setCommandPlan(null)} onConfirm={(text) => void runPlan(commandPlan, text)} />
      )}

      {seatMenu !== null && (
        <SeatContextMenu
          seat={seatMenu.seat}
          heading={seatMenu.bulk
            ? { title: t('op.map.bulk.count', { count: pickedSeats.length }), subtitle: pickedSeats.map((seat) => seat.name).join(', ') }
            : undefined}
          sections={seatMenu.bulk ? buildBulkMenu(pickedSeats, seatMenuCaps) : buildSeatMenu(seatMenu.seat, seatMenuCaps)}
          x={seatMenu.x}
          y={seatMenu.y}
          onClose={() => setSeatMenu(null)}
          onSelect={onSeatMenuSelect}
        />
      )}
    </main>
  );
}
