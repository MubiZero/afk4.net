import { useEffect, useMemo, useRef, useState } from 'react';
import { LockKeyhole, MonitorCheck, Power, ShieldAlert, TimerReset, UnlockKeyhole, Wifi, Wrench } from 'lucide-react';
import { projectOperatorError } from './apiErrors';
import { offlineBannerText, type OperatorFloorMapState } from './floorMapState';
import type { Feedback, MapFilterId, MapViewMode, PcControlActionId, PcControlActionResult } from './operatorTypes';
import type { SeatSummary } from './operatorData';
import {
  appVersionLabel,
  billingLabel,
  commandLabel,
  countByMapFilter,
  deviceStatusLabel,
  emptyFeedback,
  mapFilterOptions,
  matchesMapFilter,
  pcControlLabel,
  pcControlTitle,
  toneLabels,
  zoneClass,
  zoneLabel
} from './operatorHelpers';
import { FeedbackNotice } from './operatorPrimitives';

function SeatTile({
  seat,
  selected,
  onSelect
}: {
  seat: SeatSummary;
  selected?: boolean;
  onSelect: () => void;
}) {
  return (
    <article
      className={`seat-tile ${zoneClass(seat.zone)} state-${seat.tone}${selected ? ' selected' : ''}`}
      aria-label={`${seat.name} ${seat.stateLabel}`}
      aria-pressed={selected}
      onClick={onSelect}
      onKeyDown={(event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onSelect();
        }
      }}
      role="button"
      tabIndex={0}
    >
      <header className="seat-head">
        <div>
          <strong>{seat.name}</strong>
          <span>{zoneLabel(seat.zone)}</span>
        </div>
        <span className="state-chip">{seat.stateLabel}</span>
      </header>
      <div className="seat-main">
        <span>{seat.player}</span>
        <span>{appVersionLabel(seat.app)}</span>
      </div>
      <footer>
        <strong>{seat.remaining}</strong>
        <span>{commandLabel(seat.command)}</span>
      </footer>
    </article>
  );
}

export function MapWorkspace({
  floorMap,
  canUsePcControl,
  selectedSeatId,
  offlineActionAudit,
  onSelectSeat,
  onPcControlAction
}: {
  floorMap: OperatorFloorMapState;
  canUsePcControl: boolean;
  selectedSeatId: string;
  offlineActionAudit: string[];
  onSelectSeat: (seatId: string) => void;
  onPcControlAction: (seat: SeatSummary, action: PcControlActionId) => Promise<PcControlActionResult>;
}) {
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [activeFilter, setActiveFilter] = useState<MapFilterId>('all');
  const [viewMode, setViewMode] = useState<MapViewMode>('grid');
  const [isPcControlOpen, setIsPcControlOpen] = useState(false);
  const pcControlButtonRef = useRef<HTMLButtonElement | null>(null);
  const pcControlPanelRef = useRef<HTMLElement | null>(null);
  const visibleSeats = useMemo(
    () => floorMap.seats.filter((seat) => matchesMapFilter(seat, activeFilter)),
    [activeFilter, floorMap.seats]
  );
  const selectedSeat = floorMap.seats.find((seat) => seat.id === selectedSeatId) ?? null;
  const offlineBanner = offlineBannerText(floorMap);
  const selectedSeatVisible = visibleSeats.some((seat) => seat.id === selectedSeatId);
  const selectedHasSession = selectedSeat !== null && (Boolean(selectedSeat.activeSessionId) || selectedSeat.hasActiveSession === true);

  const runPcControlAction = async (action: PcControlActionId, label: string) => {
    if (selectedSeat === null) {
      setFeedback({ label, state: 'failed', detail: 'Выберите ПК.' });
      return;
    }

    setFeedback({ label, state: 'pending' });
    try {
      const result = await onPcControlAction(selectedSeat, action);
      setFeedback({ label, state: 'confirmed', detail: result.detail });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error).detail });
    }
  };

  const explainUnavailablePcControl = (label: string, detail: string) => {
    setFeedback({ label, state: 'failed', detail });
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
        <div>
          <span>Карта</span>
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
            title={pcControlTitle}
          >
            <Wrench size={14} />{pcControlLabel}
          </button>
        </div>
      </section>

      {isPcControlOpen && selectedSeat !== null && (
        <section ref={pcControlPanelRef} className="pc-control-panel" aria-label="Управление выбранным ПК">
          <header>
            <div>
              <span>Выбранный ПК</span>
              <strong>{selectedSeat.name}</strong>
            </div>
            <b className={`state-chip state-${selectedSeat.tone}`}>{toneLabels[selectedSeat.tone]}</b>
          </header>
          <div className="pc-control-summary">
            <span>{selectedSeat.device}</span>
            <span>{commandLabel(selectedSeat.command)}</span>
          </div>
          <span className="pc-control-section-title">Доступно сейчас</span>
          <div className="pc-control-actions">
            <button type="button" disabled={feedback.state === 'pending'} onClick={() => runPcControlAction('status', 'Статус ПК')}>
              <MonitorCheck size={14} /><span>Статус</span>
            </button>
            <button type="button" disabled={feedback.state === 'pending' || !selectedSeat.deviceId} onClick={() => runPcControlAction('lock', 'Блокировка ПК')}>
              <LockKeyhole size={14} /><span>Блокировать</span>
            </button>
            <button
              type="button"
              disabled={feedback.state === 'pending' || !selectedSeat.deviceId || !selectedHasSession}
              onClick={() => runPcControlAction('unlock', 'Разблокировка ПК')}
              title={selectedHasSession ? 'Повторно отправить unlock для активной сессии' : 'Разблокировка без сессии будет отдельным админ-режимом'}
            >
              <UnlockKeyhole size={14} /><span>Разблокировать</span>
            </button>
          </div>
          <span className="pc-control-section-title">Следующий слой</span>
          <div className="pc-control-actions future">
            <button type="button" onClick={() => explainUnavailablePcControl('Перезагрузка ПК', 'Нужен Agent-контракт reboot и подтверждение выполнения от ПК.')}>
              <TimerReset size={14} /><span><strong>Перезагрузить</strong><em>нужен Agent reboot</em></span>
            </button>
            <button type="button" onClick={() => explainUnavailablePcControl('Выключение ПК', 'Нужен Agent-контракт shutdown и правило запрета при активной сессии.')}>
              <Power size={14} /><span><strong>Выключить</strong><em>нужен Agent shutdown</em></span>
            </button>
            <button type="button" onClick={() => explainUnavailablePcControl('Разбудить ПК', 'Нужен Wake-on-LAN relay через онлайн Agent в этой клубной сети.')}>
              <Wifi size={14} /><span><strong>Разбудить</strong><em>нужен WoL relay</em></span>
            </button>
            <button type="button" onClick={() => explainUnavailablePcControl('Админ-режим', 'Нужен сервисный режим с таймером, audit и автоматическим возвратом защиты.')}>
              <ShieldAlert size={14} /><span><strong>Админ-режим</strong><em>нужен сервисный контракт</em></span>
            </button>
          </div>
        </section>
      )}

      <section className="map-controls-row" aria-label="Фильтры и вид карты">
        <div className="filter-row map-filter-row" aria-label="Фильтр ПК">
          {mapFilterOptions.map((option) => (
            <button
              key={option.id}
              type="button"
              className={activeFilter === option.id ? 'active' : undefined}
              onClick={() => setActiveFilter(option.id)}
            >
              {option.label}
              <strong>{countByMapFilter(floorMap.seats, option.id)}</strong>
            </button>
          ))}
        </div>
        <div className="filter-row map-view-switch" aria-label="Вид карты">
          <button type="button" className={viewMode === 'grid' ? 'active' : undefined} onClick={() => setViewMode('grid')}>Карта</button>
          <button type="button" className={viewMode === 'table' ? 'active' : undefined} onClick={() => setViewMode('table')}>Таблица</button>
        </div>
      </section>
      {floorMap.loadStatus === 'failed' && (
        <FeedbackNotice feedback={{ label: 'Карта', state: 'failed', detail: floorMap.error ?? 'Не удалось загрузить карту.' }} />
      )}
      {offlineBanner !== null && (
        <FeedbackNotice feedback={{ label: 'Офлайн', state: 'pending', detail: offlineBanner }} />
      )}
      {offlineActionAudit.map((note, index) => (
        <FeedbackNotice key={`offline-audit-${index}`} feedback={{ label: 'Очередь', state: 'failed', detail: note }} />
      ))}
      <FeedbackNotice feedback={feedback} />

      <section className={`map-board ${viewMode === 'table' ? 'table-mode' : ''}`} aria-label="ПК зала">
        {visibleSeats.length === 0 ? (
          <div className="map-empty-state">
            <strong>Нет ПК в выбранном фильтре</strong>
            <span>Смените фильтр или проверьте карту платформы.</span>
          </div>
        ) : viewMode === 'grid' ? (
          <div className="seat-grid">
            {visibleSeats.map((seat) => (
              <SeatTile
                key={seat.id}
                seat={seat}
                selected={seat.id === selectedSeatId}
                onSelect={() => onSelectSeat(seat.id)}
              />
            ))}
          </div>
        ) : (
          <div className="seat-table-wrap">
            <table className="seat-table" aria-label="Таблица ПК">
              <thead>
                <tr>
                  <th>ПК</th>
                  <th>Состояние</th>
                  <th>Игрок</th>
                  <th>Остаток</th>
                  <th>Устройство</th>
                  <th>Команда</th>
                  <th>Биллинг</th>
                </tr>
              </thead>
              <tbody>
                {visibleSeats.map((seat) => (
                  <tr key={seat.id} className={`state-${seat.tone}${seat.id === selectedSeatId ? ' selected' : ''}`}>
                    <td>
                      <button type="button" onClick={() => onSelectSeat(seat.id)}>{seat.name}</button>
                      <span>{zoneLabel(seat.zone)}</span>
                    </td>
                    <td><strong>{toneLabels[seat.tone]}</strong><span>{seat.stateLabel}</span></td>
                    <td>{seat.player}</td>
                    <td>{seat.remaining}</td>
                    <td>{deviceStatusLabel(seat.device)}</td>
                    <td>{commandLabel(seat.command)}</td>
                    <td>{billingLabel(seat.billing)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </main>
  );
}
