import { useEffect, useMemo, useRef, useState } from 'react';
import type { MouseEvent as ReactMouseEvent } from 'react';
import { Clock, LockKeyhole, MonitorCheck, Power, ShieldAlert, TimerReset, UnlockKeyhole, Wifi, WifiOff, Wrench } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { useDeferredFlag } from './useDeferredFlag';
import { offlineBannerText, type OperatorFloorMapState } from './floorMapState';
import type { OperatorAuthSession } from './authClient';
import type { Feedback, MapFilterId, MapViewMode, PcControlActionId, PcControlActionResult, SeatActionRequest, SeatActionResult } from './operatorTypes';
import type { SeatSummary } from './operatorData';
import {
  billingLabel,
  commandLabel,
  countByMapFilter,
  deviceStatusLabel,
  emptyFeedback,
  guestBillingSelection,
  mapFilterOptions,
  matchesMapFilter,
  projectOperatorFacingError,
  toneLabel,
  zoneLabel
} from './operatorHelpers';
import { hasPermission, permissionNames } from './operatorPermissions';
import { buildSeatMenu, type SeatMenuItem } from './seatMenu';
import { EmptyState, FeedbackNotice, Skeleton } from './operatorPrimitives';
import { SeatContextMenu } from './SeatContextMenu';
import { SeatTile } from './SeatTile';

export function MapWorkspace({
  floorMap,
  canUsePcControl,
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
  canUsePcControl: boolean;
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
  const [viewMode, setViewMode] = useState<MapViewMode>('grid');
  const [isPcControlOpen, setIsPcControlOpen] = useState(false);
  const [seatMenu, setSeatMenu] = useState<{ seat: SeatSummary; x: number; y: number } | null>(null);
  const seatMenuCaps = useMemo(() => ({
    actionsEnabled,
    canStart: hasPermission(session, permissionNames.startSession),
    canExtend: hasPermission(session, permissionNames.extendSession),
    canStatus: hasPermission(session, permissionNames.viewDiagnostics) && hasPermission(session, permissionNames.viewDeviceDetail),
    canLockUnlock: hasPermission(session, permissionNames.dispatchDeviceCommand)
  }), [actionsEnabled, session]);
  const pcControlButtonRef = useRef<HTMLButtonElement | null>(null);
  const pcControlPanelRef = useRef<HTMLElement | null>(null);
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
  const selectedHasSession = selectedSeat !== null && (Boolean(selectedSeat.activeSessionId) || selectedSeat.hasActiveSession === true);

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
    if (!isPcControlOpen) {
      return undefined;
    }

    const closeOnOutsidePointer = (event: PointerEvent) => {
      const target = event.target as Node | null;
      if (target !== null && (pcControlPanelRef.current?.contains(target) || pcControlButtonRef.current?.contains(target))) {
        return;
      }

      setIsPcControlOpen(false);
    };
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsPcControlOpen(false);
      }
    };

    document.addEventListener('pointerdown', closeOnOutsidePointer, true);
    document.addEventListener('keydown', closeOnEscape);
    return () => {
      document.removeEventListener('pointerdown', closeOnOutsidePointer, true);
      document.removeEventListener('keydown', closeOnEscape);
    };
  }, [isPcControlOpen]);

  useEffect(() => {
    if (visibleSeats.length === 0 || selectedSeatVisible) {
      return;
    }

    onSelectSeat(visibleSeats[0].id);
  }, [activeFilter, floorMap.seats, onSelectSeat, selectedSeatVisible, visibleSeats]);

  return (
    <main className="floor-workspace">
      <section className="map-toolbar">
        <div className="map-toolbar-title">
          <h1>{floorMap.branchName}</h1>
        </div>
        <div className="screen-actions">
          <button
            ref={pcControlButtonRef}
            type="button"
            className="map-tool-action"
            aria-expanded={isPcControlOpen}
            disabled={!canUsePcControl || selectedSeat === null}
            onClick={() => setIsPcControlOpen((current) => !current)}
            title={t('op.map.pcControlTitle')}
          >
            <Wrench size={14} />{t('op.map.pcControlLabel')}
          </button>
        </div>
      </section>

      {isPcControlOpen && selectedSeat !== null && (
        <section ref={pcControlPanelRef} className="pc-control-panel" aria-label={t('op.map.pcControlPanelLabel')}>
          <header>
            <div>
              <span>{t('op.map.selectedPc')}</span>
              <strong>{selectedSeat.name}</strong>
            </div>
            <b className={`state-chip state-${selectedSeat.tone}`}>{toneLabel(selectedSeat.tone, t)}</b>
          </header>
          <div className="pc-control-summary">
            <span>{deviceStatusLabel(selectedSeat.device, t)}</span>
            <span>{commandLabel(selectedSeat.command, t)}</span>
          </div>
          <span className="pc-control-section-title">{t('op.map.availableNow')}</span>
          <div className="pc-control-actions">
            <button type="button" disabled={feedback.state === 'pending'} onClick={() => runPcControlAction('status', t('op.map.actionStatus'))}>
              <MonitorCheck size={14} /><span>{t('op.map.actionStatusBtn')}</span>
            </button>
            <button type="button" disabled={feedback.state === 'pending' || !selectedSeat.deviceId} onClick={() => runPcControlAction('lock', t('op.map.actionLock'))}>
              <LockKeyhole size={14} /><span>{t('op.map.actionLockBtn')}</span>
            </button>
            <button
              type="button"
              disabled={feedback.state === 'pending' || !selectedSeat.deviceId || !selectedHasSession}
              onClick={() => runPcControlAction('unlock', t('op.map.actionUnlock'))}
              title={selectedHasSession ? t('op.map.unlockActiveTitle') : t('op.map.unlockNoSessionTitle')}
            >
              <UnlockKeyhole size={14} /><span>{t('op.map.actionUnlockBtn')}</span>
            </button>
          </div>
          <span className="pc-control-section-title">{t('op.map.nextLayer')}</span>
          <div className="pc-control-actions future">
            <button type="button" onClick={() => explainUnavailablePcControl(t('op.map.actionReboot'), t('op.map.rebootDetail'))}>
              <TimerReset size={14} /><span><strong>{t('op.map.rebootBtn')}</strong><em>{t('op.map.rebootHint')}</em></span>
            </button>
            <button type="button" onClick={() => explainUnavailablePcControl(t('op.map.actionShutdown'), t('op.map.shutdownDetail'))}>
              <Power size={14} /><span><strong>{t('op.map.shutdownBtn')}</strong><em>{t('op.map.shutdownHint')}</em></span>
            </button>
            <button type="button" onClick={() => explainUnavailablePcControl(t('op.map.actionWake'), t('op.map.wakeDetail'))}>
              <Wifi size={14} /><span><strong>{t('op.map.wakeBtn')}</strong><em>{t('op.map.wakeHint')}</em></span>
            </button>
            <button type="button" onClick={() => explainUnavailablePcControl(t('op.map.actionAdmin'), t('op.map.adminDetail'))}>
              <ShieldAlert size={14} /><span><strong>{t('op.map.actionAdmin')}</strong><em>{t('op.map.adminHint')}</em></span>
            </button>
          </div>
        </section>
      )}

      <section className="map-controls-row" aria-label={t('op.map.filtersAndViewLabel')}>
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
        <div className="filter-row map-view-switch" aria-label={t('op.map.viewLabel')}>
          <button type="button" className={viewMode === 'grid' ? 'active' : undefined} onClick={() => setViewMode('grid')}>{t('op.map.viewGrid')}</button>
          <button type="button" className={viewMode === 'table' ? 'active' : undefined} onClick={() => setViewMode('table')}>{t('op.map.viewTable')}</button>
        </div>
      </section>
      {floorMap.loadStatus === 'failed' && (
        <FeedbackNotice feedback={{ label: t('op.map.feedbackMap'), state: 'failed', detail: floorMap.error ?? t('op.map.loadError') }} />
      )}
      {/* Снимок зала: показываем РЕАЛЬНЫЙ текст (со временем кэша), а не генерик «ждём ответ
          сервера». Тон честный — «офлайн/только просмотр» лишь при настоящем обрыве, иначе
          спокойное «снимок на HH:MM», чтобы не противоречить футеру при живой связи. */}
      {offlineBanner !== null && (
        <div className={`floor-snapshot-banner ${floorMap.isOffline ? 'offline' : 'stale'}`} role="status" aria-live="polite">
          {floorMap.isOffline ? <WifiOff size={14} aria-hidden="true" /> : <Clock size={14} aria-hidden="true" />}
          <span>{offlineBanner}</span>
        </div>
      )}
      {offlineActionAudit.map((note, index) => (
        <FeedbackNotice key={`offline-audit-${index}`} feedback={{ label: t('op.map.feedbackQueue'), state: 'failed', detail: note }} />
      ))}
      <FeedbackNotice feedback={feedback} />

      <section className={`map-board ${viewMode === 'table' ? 'table-mode' : ''}`} aria-label={t('op.map.seatsLabel')}>
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
        ) : viewMode === 'grid' ? (
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
        ) : (
          <div className="seat-table-wrap">
            <table className="seat-table" aria-label={t('op.map.tableLabel')}>
              <thead>
                <tr>
                  <th>{t('op.map.colPc')}</th>
                  <th>{t('op.map.colState')}</th>
                  <th>{t('op.map.colPlayer')}</th>
                  <th>{t('op.map.colRemaining')}</th>
                  <th>{t('op.map.colDevice')}</th>
                  <th>{t('op.map.colCommand')}</th>
                  <th>{t('op.map.colBilling')}</th>
                </tr>
              </thead>
              <tbody>
                {visibleSeats.map((seat) => (
                  <tr
                    key={seat.id}
                    className={`state-${seat.tone}${seat.id === selectedSeatId ? ' selected' : ''}`}
                    onContextMenu={(event) => openSeatMenu(seat, event)}
                  >
                    <td>
                      <button type="button" onClick={() => onSelectSeat(seat.id)}>{seat.name}</button>
                      <span>{zoneLabel(seat.zone, t)}</span>
                    </td>
                    <td><strong>{toneLabel(seat.tone, t)}</strong><span>{seat.stateLabel}</span></td>
                    <td>{seat.player}</td>
                    <td>{seat.remaining}</td>
                    <td>{deviceStatusLabel(seat.device, t)}</td>
                    <td>{commandLabel(seat.command, t)}</td>
                    <td>{billingLabel(seat.billing, t)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
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
