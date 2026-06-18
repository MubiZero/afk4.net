import { useEffect, useMemo, useState } from 'react';
import type { MouseEvent as ReactMouseEvent } from 'react';
import { WifiOff } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { useDeferredFlag } from './useDeferredFlag';
import { offlineBannerText, type OperatorFloorMapState } from './floorMapState';
import type { OperatorAuthSession } from './authClient';
import type { Feedback, MapFilterId, PcControlActionId, PcControlActionResult, SeatActionRequest, SeatActionResult } from './operatorTypes';
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
import { buildSeatMenu, type SeatMenuItem } from './seatMenu';
import { EmptyState, FeedbackNotice, Skeleton } from './operatorPrimitives';
import { SeatContextMenu } from './SeatContextMenu';
import { SeatTile } from './SeatTile';

export function MapWorkspace({
  floorMap,
  session,
  actionsEnabled,
  selectedSeatId,
  activeFilter,
  offlineActionAudit,
  onSelectSeat,
  onFilterChange,
  onPcControlAction,
  onSeatAction
}: {
  floorMap: OperatorFloorMapState;
  session: OperatorAuthSession | null;
  actionsEnabled: boolean;
  selectedSeatId: string;
  activeFilter: MapFilterId;
  offlineActionAudit: string[];
  onSelectSeat: (seatId: string) => void;
  onFilterChange: (filter: MapFilterId) => void;
  onPcControlAction: (seat: SeatSummary, action: PcControlActionId) => Promise<PcControlActionResult>;
  onSeatAction: (request: SeatActionRequest) => Promise<SeatActionResult>;
}) {
  const { t } = useI18n();
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [seatMenu, setSeatMenu] = useState<{ seat: SeatSummary; x: number; y: number } | null>(null);
  const seatMenuCaps = useMemo(() => ({
    actionsEnabled,
    canStart: hasPermission(session, permissionNames.startSession),
    canExtend: hasPermission(session, permissionNames.extendSession),
    canLockUnlock: hasPermission(session, permissionNames.dispatchDeviceCommand)
  }), [actionsEnabled, session]);
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

  const runSeatAction = async (label: string, request: SeatActionRequest) => {
    setFeedback({ label, state: 'pending' });
    try {
      const result = await onSeatAction(request);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorFacingError(error, t) });
    }
  };

  const explainUnavailablePcControl = (label: string, detail: string) => {
    setFeedback({ label, state: 'failed', detail });
  };

  // Правый клик: выбираем место (раскрывает карточку справа) и открываем меню у курсора.
  // Клавиатурный вызов (Menu/Shift+F10) даёт coords 0 — тогда якоримся к самому элементу.
  const openSeatMenu = (seat: SeatSummary, event: ReactMouseEvent) => {
    event.preventDefault();
    onSelectSeat(seat.id);
    const fromKeyboard = event.clientX === 0 && event.clientY === 0;
    const rect = (event.currentTarget as HTMLElement).getBoundingClientRect();
    setSeatMenu({
      seat,
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
    } else if (run.kind === 'pc') {
      void runPcControlAction(run.action, label, seat);
    } else {
      explainUnavailablePcControl(label, t(run.detailKey));
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
        <FeedbackNotice feedback={{ label: t('op.map.feedbackMap'), state: 'failed', detail: floorMap.error ?? t('op.map.loadError') }} />
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
      <FeedbackNotice feedback={feedback} />

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
          <EmptyState title={t('op.map.emptyTitle')} description={t('op.map.emptyHint')} className="map-empty-state" />
        ) : (
          <div className="seat-zones">
            {zoneGroups.map((group) => (
              <section className="zone-group" key={group.zone}>
                <header className="zone-group-head">
                  <span>{zoneLabel(group.zone, t)}</span>
                  <strong>{group.seats.length}</strong>
                </header>
                <div className="seat-grid">
                  {group.seats.map((seat) => (
                    <SeatTile
                      key={seat.id}
                      seat={seat}
                      selected={seat.id === selectedSeatId}
                      onSelect={() => onSelectSeat(seat.id)}
                      onContextMenu={(event) => openSeatMenu(seat, event)}
                    />
                  ))}
                </div>
              </section>
            ))}
          </div>
        )}
      </section>

      {seatMenu !== null && (
        <SeatContextMenu
          seat={seatMenu.seat}
          sections={buildSeatMenu(seatMenu.seat, seatMenuCaps)}
          x={seatMenu.x}
          y={seatMenu.y}
          onClose={() => setSeatMenu(null)}
          onSelect={onSeatMenuSelect}
        />
      )}
    </main>
  );
}
