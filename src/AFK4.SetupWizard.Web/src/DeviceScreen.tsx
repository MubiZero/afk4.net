import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { ArrowLeft, ArrowRight, Loader2, Plus, X } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { isHostBridgeUnavailableError } from './hostBridge';
import {
  type WizardBranch,
  type WizardEnrollResult,
  type WizardInstallClient,
  type WizardRole,
  type WizardSeat,
} from './wizardApi';

interface DeviceScreenProps {
  installClient: WizardInstallClient;
  ownerName: string;
  branch: WizardBranch;
  role: WizardRole;
  defaultDisplayName: string;
  onEnrolled(result: WizardEnrollResult, selectedSeat: WizardSeat | null): void;
  onBusyChange?(installing: boolean): void;
  onBack(): void;
}

type RequestState =
  | { kind: 'idle' }
  | { kind: 'creating' }
  | { kind: 'enrolling' }
  | { kind: 'error'; message: string };

export function DeviceScreen({
  installClient,
  ownerName,
  branch,
  role,
  defaultDisplayName,
  onEnrolled,
  onBusyChange,
  onBack,
}: DeviceScreenProps) {
  const { t } = useI18n();
  const [displayName, setDisplayName] = useState(defaultDisplayName);
  const [seats, setSeats] = useState<WizardSeat[]>(branch.seats);
  const [freeSeatIds, setFreeSeatIds] = useState<Set<string>>(() => new Set(branch.freeSeatIds));
  const [selectedSeatId, setSelectedSeatId] = useState<string | null>(null);
  const [newSeatName, setNewSeatName] = useState('');
  const [request, setRequest] = useState<RequestState>({ kind: 'idle' });
  const [seatFilter, setSeatFilter] = useState('');
  const [onlyFree, setOnlyFree] = useState(true);
  const [pendingSeatIds, setPendingSeatIds] = useState<Set<string>>(() => new Set());
  // Создание нового места — свёрнуто по умолчанию, чтобы не съедало вертикаль.
  // Раскрывается кликом по «Создать новое место»; форма автофокусит инпут.
  const [createOpen, setCreateOpen] = useState(false);
  const newSeatInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (createOpen) {
      newSeatInputRef.current?.focus();
    }
  }, [createOpen]);

  // Сообщаем наверх, что идёт установка (enroll → msiexec на хосте), чтобы титлбар мог
  // защитить кнопку закрытия. На размонтировании снимаем флаг.
  const installing = request.kind === 'enrolling';
  useEffect(() => {
    onBusyChange?.(installing);
    return () => onBusyChange?.(false);
  }, [installing, onBusyChange]);

  const openCreateForm = useCallback(() => {
    setCreateOpen(true);
  }, []);

  const closeCreateForm = useCallback(() => {
    setCreateOpen(false);
    setNewSeatName('');
  }, []);

  const requiresSeat = role === 'gaming_pc';
  const selectedSeat = useMemo(
    () => (selectedSeatId ? seats.find((seat) => seat.seatId === selectedSeatId) ?? null : null),
    [seats, selectedSeatId],
  );
  const trimmedDisplayName = displayName.trim();
  const trimmedNewSeat = newSeatName.trim();
  const displayNameValid = trimmedDisplayName.length >= 3 && trimmedDisplayName.length <= 32;
  const canEnroll = displayNameValid && (!requiresSeat || selectedSeat !== null);

  const defaultZone = branch.zones[0] ?? null;
  const totalSeats = seats.length;
  const showSeatFilter = totalSeats > 8;

  const visibleSeats = useMemo(() => {
    const term = seatFilter.trim().toLowerCase();
    return seats.filter((seat) => {
      if (onlyFree && !freeSeatIds.has(seat.seatId) && seat.seatId !== selectedSeatId) {
        return false;
      }
      if (term.length > 0 && !seat.pcName.toLowerCase().includes(term)) {
        return false;
      }
      return true;
    });
  }, [freeSeatIds, onlyFree, seatFilter, seats, selectedSeatId]);

  const groupedSeats = useMemo(
    () => groupSeatsByZone(visibleSeats, branch),
    [visibleSeats, branch],
  );
  const hasFilterActive = onlyFree || seatFilter.trim().length > 0;

  const handleCreateSeat = useCallback(async () => {
    if (!defaultZone) {
      setRequest({ kind: 'error', message: t('setup.wizard.device.create.noZone') });
      return;
    }
    if (trimmedNewSeat.length === 0) {
      return;
    }

    // Правило 31 (восприятие скорости): optimistic-плитка появляется сразу,
    // селектится мгновенно. На success подменяем на серверный seat, на error — откатываем.
    const optimisticId = `optimistic-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
    const optimisticSeat: WizardSeat = {
      seatId: optimisticId,
      pcName: trimmedNewSeat,
      zoneId: defaultZone.zoneId,
      zoneName: defaultZone.name,
      sortOrder: seats.length + 99,
      status: 'Free',
      deviceId: null,
      deviceName: null,
      isOnline: null,
    };
    setSeats((prev) => [...prev, optimisticSeat]);
    setFreeSeatIds((prev) => {
      const next = new Set(prev);
      next.add(optimisticId);
      return next;
    });
    setPendingSeatIds((prev) => {
      const next = new Set(prev);
      next.add(optimisticId);
      return next;
    });
    setSelectedSeatId(optimisticId);
    const previousSeatName = newSeatName;
    setNewSeatName('');
    setRequest({ kind: 'creating' });

    try {
      const seat = await installClient.createSeat({
        branchId: branch.branchId,
        zoneId: defaultZone.zoneId,
        zoneName: defaultZone.name,
        name: trimmedNewSeat,
      });
      // Reconcile: подмена optimistic на серверный
      setSeats((prev) => prev.map((s) => (s.seatId === optimisticId ? seat : s)));
      setFreeSeatIds((prev) => {
        const next = new Set(prev);
        next.delete(optimisticId);
        next.add(seat.seatId);
        return next;
      });
      setPendingSeatIds((prev) => {
        const next = new Set(prev);
        next.delete(optimisticId);
        return next;
      });
      setSelectedSeatId(seat.seatId);
      setRequest({ kind: 'idle' });
      setCreateOpen(false);
    } catch (error) {
      // Rollback
      setSeats((prev) => prev.filter((s) => s.seatId !== optimisticId));
      setFreeSeatIds((prev) => {
        const next = new Set(prev);
        next.delete(optimisticId);
        return next;
      });
      setPendingSeatIds((prev) => {
        const next = new Set(prev);
        next.delete(optimisticId);
        return next;
      });
      setSelectedSeatId(null);
      setNewSeatName(previousSeatName);
      setRequest({ kind: 'error', message: messageForError(error, t) });
    }
  }, [branch.branchId, defaultZone, installClient, newSeatName, seats.length, trimmedNewSeat, t]);

  const handleSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (!canEnroll || request.kind === 'enrolling' || request.kind === 'creating') {
        return;
      }

      setRequest({ kind: 'enrolling' });
      try {
        const result = await installClient.enrollDevice({
          branchId: branch.branchId,
          seatId: requiresSeat ? selectedSeat!.seatId : null,
          role,
          displayName: trimmedDisplayName,
        });
        onEnrolled(result, requiresSeat ? selectedSeat : null);
      } catch (error) {
        setRequest({ kind: 'error', message: messageForError(error, t) });
      }
    },
    [
      branch.branchId,
      canEnroll,
      installClient,
      onEnrolled,
      request.kind,
      requiresSeat,
      role,
      selectedSeat,
      t,
      trimmedDisplayName,
    ],
  );

  const titleKey: MessageKey = requiresSeat
    ? 'setup.wizard.device.gaming.title'
    : 'setup.wizard.device.manager.title';
  const subtitleKey: MessageKey = requiresSeat
    ? 'setup.wizard.device.gaming.subtitle'
    : 'setup.wizard.device.manager.subtitle';

  return (
    <section className={`wizard-screen is-framed ${requiresSeat ? 'is-wide' : 'is-narrow'}`}>
      <div className="wizard-screen-head">
        <span className="wizard-screen-context">{ownerName} · {branch.branchName}</span>
        <div className="wizard-screen-title-row">
          <span className="wizard-screen-step" aria-hidden>4</span>
          <h1>{t(titleKey)}</h1>
        </div>
        <p>{t(subtitleKey)}</p>
      </div>

      <form className="wizard-form" onSubmit={handleSubmit} noValidate>
        <label className="wizard-field">
          <span className="wizard-field-label">{t('setup.wizard.device.field.name')}</span>
          <input
            type="text"
            value={displayName}
            autoComplete="off"
            spellCheck={false}
            minLength={3}
            maxLength={32}
            onChange={(event) => setDisplayName(event.target.value)}
            placeholder={defaultDisplayName}
            aria-invalid={trimmedDisplayName.length > 0 && !displayNameValid}
            aria-describedby="display-name-hint"
          />
          <span id="display-name-hint" className="wizard-field-hint">
            {t('setup.wizard.device.field.nameHint')}
          </span>
        </label>

        {requiresSeat && (
          <section className="wizard-seats-section" aria-label={t('setup.wizard.device.seats.aria')}>
            <div className="wizard-seats-head">
              <h2>{t('setup.wizard.device.seats.title')}</h2>
              {defaultZone && !createOpen && (
                <button
                  type="button"
                  className="wizard-link-action"
                  onClick={openCreateForm}
                >
                  <Plus size={14} aria-hidden />
                  <span>{t('setup.wizard.device.create.toggle')}</span>
                </button>
              )}
            </div>

            {showSeatFilter && (
              <div className="wizard-device-seats-toolbar">
                <input
                  type="text"
                  value={seatFilter}
                  onChange={(event) => setSeatFilter(event.target.value)}
                  placeholder={t('setup.wizard.device.seats.search')}
                  autoComplete="off"
                  spellCheck={false}
                />
                <label className="wizard-toggle">
                  <input
                    type="checkbox"
                    checked={onlyFree}
                    onChange={(event) => setOnlyFree(event.target.checked)}
                  />
                  <span className="wizard-toggle-track" aria-hidden />
                  <span>{t('setup.wizard.device.seats.onlyFree')}</span>
                </label>
              </div>
            )}

            {defaultZone && createOpen && (
              <div className="wizard-create-seat" role="group">
                <label className="wizard-field">
                  <span className="wizard-field-label">
                    {t('setup.wizard.device.create.label')}
                  </span>
                  <input
                    ref={newSeatInputRef}
                    type="text"
                    value={newSeatName}
                    onChange={(event) => setNewSeatName(event.target.value)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' && trimmedNewSeat.length > 0) {
                        event.preventDefault();
                        handleCreateSeat();
                      } else if (event.key === 'Escape') {
                        closeCreateForm();
                      }
                    }}
                    placeholder={`PC-${(seats.length + 1).toString().padStart(2, '0')}`}
                    autoComplete="off"
                    spellCheck={false}
                  />
                </label>
                <div className="wizard-create-seat-actions">
                  <button
                    type="button"
                    className="wizard-primary wizard-create-seat-submit"
                    onClick={handleCreateSeat}
                    disabled={trimmedNewSeat.length === 0 || request.kind === 'creating'}
                  >
                    {request.kind === 'creating' ? (
                      <>
                        <Loader2 className="wizard-spinner" aria-hidden />
                        <span>{t('setup.wizard.device.create.creating')}</span>
                      </>
                    ) : (
                      <>
                        <Plus aria-hidden />
                        <span>{t('setup.wizard.device.create.action')}</span>
                      </>
                    )}
                  </button>
                  <button
                    type="button"
                    className="wizard-icon-ghost"
                    onClick={closeCreateForm}
                    aria-label={t('setup.wizard.device.create.cancel')}
                  >
                    <X size={16} aria-hidden />
                  </button>
                </div>
              </div>
            )}

            <div className="wizard-seats-list">
              {groupedSeats.length === 0 ? (
                totalSeats === 0 ? (
                  <div className="wizard-empty">
                    <strong>{t('setup.wizard.device.seats.empty.noSeats.title')}</strong>
                    <span>{t('setup.wizard.device.seats.empty.noSeats.body')}</span>
                    {defaultZone && (
                      <button
                        type="button"
                        className="wizard-primary wizard-empty-cta"
                        onClick={openCreateForm}
                      >
                        <Plus aria-hidden />
                        <span>{t('setup.wizard.device.create.toggle')}</span>
                      </button>
                    )}
                  </div>
                ) : (
                  <div className="wizard-empty">
                    <strong>{t('setup.wizard.device.seats.empty.noMatch.title')}</strong>
                    <span>
                      {hasFilterActive
                        ? t('setup.wizard.device.seats.empty.noMatch.bodyFiltered')
                        : t('setup.wizard.device.seats.empty.noMatch.bodyDefault')}
                    </span>
                    {hasFilterActive && (
                      <button
                        type="button"
                        className="wizard-secondary wizard-empty-cta"
                        onClick={() => {
                          setOnlyFree(false);
                          setSeatFilter('');
                        }}
                      >
                        <span>{t('setup.wizard.device.seats.empty.noMatch.resetAction')}</span>
                      </button>
                    )}
                  </div>
                )
              ) : (
                groupedSeats.map((zoneGroup) => (
                  <div className="wizard-zone-group" key={zoneGroup.zoneId}>
                    <div className="wizard-zone-head">
                      <strong>{zoneGroup.zoneName}</strong>
                      <span className="wizard-zone-counter">
                        <strong>{zoneGroup.freeCount}</strong>
                        <span>
                          {t('setup.wizard.branch.meta.of')} {zoneGroup.seats.length}{' '}
                          {t('setup.wizard.branch.meta.free')}
                        </span>
                      </span>
                    </div>
                    <div className="wizard-seat-grid">
                      {zoneGroup.seats.map((seat) => {
                        const isFree = freeSeatIds.has(seat.seatId);
                        const isSelected = selectedSeatId === seat.seatId;
                        const statusLabel = isFree
                          ? t('setup.wizard.device.seats.label.free')
                          : t('setup.wizard.device.seats.label.occupied');
                        // Правило 34: если есть deviceName/онлайн-статус — покажи их,
                        // не оставляй пользователя с generic «занято». Онлайн/оффлайн ещё и
                        // подсвечивается точкой-статусом на карточке (см. .is-online/.is-offline).
                        const onlineLabel = seat.isOnline === true
                          ? t('devices.status.online')
                          : seat.isOnline === false
                            ? t('devices.status.offline')
                            : null;
                        const occupiedDetail = !isFree && seat.deviceName
                          ? `${seat.deviceName}${onlineLabel ? ` · ${onlineLabel}` : ''}`
                          : null;
                        const tooltip = isFree
                          ? `${seat.pcName} · ${statusLabel}`
                          : occupiedDetail
                            ? `${seat.pcName} · ${statusLabel} · ${occupiedDetail}`
                            : `${seat.pcName} · ${statusLabel}`;
                        return (
                          <button
                            key={seat.seatId}
                            type="button"
                            className={[
                              'wizard-seat-card',
                              isFree ? 'is-free' : 'is-occupied',
                              !isFree && seat.isOnline === true ? 'is-online' : '',
                              !isFree && seat.isOnline === false ? 'is-offline' : '',
                              pendingSeatIds.has(seat.seatId) ? 'is-pending' : '',
                            ]
                              .filter(Boolean)
                              .join(' ')}
                            aria-pressed={isSelected}
                            aria-label={tooltip}
                            title={tooltip}
                            disabled={!isFree}
                            onClick={() => setSelectedSeatId(seat.seatId)}
                          >
                            <strong>{seat.pcName}</strong>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                ))
              )}
            </div>
          </section>
        )}

        {request.kind === 'error' && (
          <div role="alert" className="wizard-alert">
            {request.message}
          </div>
        )}

        <div className="wizard-actions">
          <button
            type="button"
            className="wizard-secondary"
            onClick={onBack}
            disabled={request.kind === 'enrolling'}
          >
            <ArrowLeft aria-hidden />
            <span>{t('setup.wizard.common.back')}</span>
          </button>
          <button
            type="submit"
            className="wizard-primary"
            disabled={!canEnroll || request.kind === 'enrolling' || request.kind === 'creating'}
          >
            {request.kind === 'enrolling' ? (
              <>
                <Loader2 className="wizard-spinner" aria-hidden />
                <span>{t('setup.wizard.device.action.enrolling')}</span>
              </>
            ) : (
              <>
                <span>{t('setup.wizard.device.action.enroll')}</span>
                <ArrowRight aria-hidden />
              </>
            )}
          </button>
        </div>
      </form>
    </section>
  );
}

interface ZoneGroup {
  zoneId: string;
  zoneName: string;
  seats: WizardSeat[];
  freeCount: number;
}

function groupSeatsByZone(seats: WizardSeat[], branch: WizardBranch): ZoneGroup[] {
  const zoneOrder = new Map(branch.zones.map((zone, index) => [zone.zoneId, index] as const));
  const groups = new Map<string, ZoneGroup>();
  const freeIds = new Set(branch.freeSeatIds);

  for (const seat of seats) {
    let group = groups.get(seat.zoneId);
    if (!group) {
      group = { zoneId: seat.zoneId, zoneName: seat.zoneName, seats: [], freeCount: 0 };
      groups.set(seat.zoneId, group);
    }
    group.seats.push(seat);
    if (freeIds.has(seat.seatId)) {
      group.freeCount += 1;
    }
  }

  return [...groups.values()].sort((a, b) => {
    const orderA = zoneOrder.get(a.zoneId) ?? Number.MAX_SAFE_INTEGER;
    const orderB = zoneOrder.get(b.zoneId) ?? Number.MAX_SAFE_INTEGER;
    return orderA - orderB;
  });
}

function messageForError(error: unknown, t: (key: MessageKey) => string): string {
  if (isHostBridgeUnavailableError(error)) {
    return t('setup.wizard.device.error.bridgeMissing');
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return t('setup.wizard.device.error.generic');
}
