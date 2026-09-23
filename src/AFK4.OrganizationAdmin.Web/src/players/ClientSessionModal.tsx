import { useCallback, useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../PanelModal';
import { SessionStartForm, SessionStartSkeleton, createSessionStartSelection, type SessionStartSelection } from '../session/SessionStartForm';
import { DeferredSkeleton, SkeletonControl, SkeletonLine } from '../LoadingSkeleton';
import { createAuthenticatedOperatorClients, createIdempotencyKey } from '../operatorHelpers';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { projectOperatorError } from '../apiErrors';
import type { PlayerClientItem } from '../operatorHelpers';
import type { OperatorBackendContext } from '../operatorTypes';
import { isSeatReadyForGuest } from '../floorMapState';

interface FreeSeat {
  seatId: string;
  seatName: string;
  zoneName: string;
}

/**
 * Посадить клиента за ПК прямо из его карточки.
 *
 * Раньше за этим уходили на Карту и искали того же человека третий раз за визит — после кассы и
 * после карточки. Форма запуска здесь та же, что на Карте, но клиент уже известен: перепутать
 * тёзку негде.
 */
export function ClientSessionModal({ backend, player, currencyCode, onClose, onStarted }: {
  backend: OperatorBackendContext;
  player: PlayerClientItem & { playerAccountId: string };
  currencyCode: string;
  onClose: () => void;
  onStarted: (seatName: string) => void | Promise<void>;
}) {
  const { t } = useI18n();
  const [seats, setSeats] = useState<FreeSeat[] | null>(null);
  const [seatId, setSeatId] = useState('');
  const [selection, setSelection] = useState<SessionStartSelection>(() => createSessionStartSelection('prepaid_wallet'));
  const [formValid, setFormValid] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Форма перезапрашивает тарифы и пакеты, когда меняется загрузчик, и ждёт, что родитель его
  // запоминает. Здесь загрузчики были новыми стрелками на каждой отрисовке: ответ тарифов менял
  // выбор, выбор перерисовывал диалог, новая стрелка запрашивала тарифы снова — пять запросов на
  // одно открытие.
  const loadTariffs = useCallback(
    () => hasPermission(backend.session, permissionNames.viewTariffs)
      ? createAuthenticatedOperatorClients(backend.config, backend.session).settings.getTariffOptions(backend.branchId)
      : Promise.resolve([]),
    [backend.branchId, backend.config, backend.session]
  );
  const loadPackages = useCallback(
    (playerAccountId: string) => hasPermission(backend.session, permissionNames.viewBilling)
      ? createAuthenticatedOperatorClients(backend.config, backend.session).players.getPlayerPackages(playerAccountId)
      : Promise.resolve([]),
    [backend.config, backend.session]
  );

  useEffect(() => {
    let active = true;
    createAuthenticatedOperatorClients(backend.config, backend.session).floorMap
      .getFloorMap(backend.branchId)
      .then((map) => {
        if (!active) return;
        // Свободные места и без активной сессии — те же, что на Карте предлагают под запуск.
        const free = map.seats
          .filter(isSeatReadyForGuest)
          .map((seat) => ({ seatId: seat.seatId, seatName: seat.seatName, zoneName: seat.zoneName }));
        setSeats(free);
        setSeatId(free[0]?.seatId ?? '');
      })
      .catch((reason) => {
        if (!active) return;
        setSeats([]);
        setError(projectOperatorError(reason, t).detail);
      });
    return () => { active = false; };
  }, [backend.branchId, backend.config, backend.session, t]);

  const selectedSeat = seats?.find((seat) => seat.seatId === seatId) ?? null;

  const start = async () => {
    if (selectedSeat === null || busy) return;
    setBusy(true);
    setError(null);
    try {
      if (!hasPermission(backend.session, permissionNames.startSession)) {
        throw new Error(t('op.shell.err.noPermStart'));
      }
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      const isOpenTab = selection.durationMode === 'open';
      await clients.sessions.startGuestSession(backend.branchId, {
        organizationId: backend.session.organizationId,
        seatId: selectedSeat.seatId,
        durationMode: isOpenTab ? 'open' : 'fixed',
        durationMinutes: isOpenTab ? null : selection.durationMinutes,
        tariffRuleVersionId: selection.tariffRuleVersionId,
        idempotencyKey: createIdempotencyKey('session-start'),
        playerAccountId: player.playerAccountId,
        billingMode: selection.billingMode === 'guest' ? '' : selection.billingMode,
        tariffVersionId: selection.tariffVersionId,
        playerPackageId: selection.playerPackageId,
        isComp: selection.isComp,
        compReason: selection.compReason
      });
      await onStarted(selectedSeat.seatName);
    } catch (reason) {
      setError(projectOperatorError(reason, t).detail);
    } finally {
      setBusy(false);
    }
  };

  return (
    <PanelModal
      title={t('op.players.session.title')}
      subtitle={player.name}
      onClose={onClose}
      closeDisabled={busy}
    >
      {seats === null ? (
        <DeferredSkeleton>
          {/* Та же форма, что придёт: место, форма запуска и две кнопки внизу. */}
          <div className="clients-new-form" data-skeleton="form" aria-hidden="true">
            <label>{t('op.players.session.seatLabel')}</label>
            <SkeletonControl size="sm" />
            <SessionStartSkeleton />
            <div className="critical-confirmation-actions">
              {[0, 1].map((button) => <button key={button} type="button" tabIndex={-1}><SkeletonLine width="6em" /></button>)}
            </div>
          </div>
        </DeferredSkeleton>
      ) : seats.length === 0 ? (
        <p>{error ?? t('op.players.session.noFreeSeats')}</p>
      ) : (
        <div className="clients-new-form">
          <label htmlFor="client-session-seat">{t('op.players.session.seatLabel')}</label>
          <select
            id="client-session-seat"
          className="ui-select"
            value={seatId}
            disabled={busy}
            onChange={(event) => setSeatId(event.currentTarget.value)}
          >
            {seats.map((seat) => (
              <option key={seat.seatId} value={seat.seatId}>{seat.zoneName} · {seat.seatName}</option>
            ))}
          </select>

          <SessionStartForm
            seatName={selectedSeat?.seatName ?? ''}
            currencyCode={currencyCode}
            disabled={busy}
            value={selection}
            onChange={setSelection}
            fixedClient={{
              playerAccountId: player.playerAccountId,
              name: player.name,
              phoneNumber: player.phoneNumber,
              balanceMinorUnits: player.balanceMinorUnits ?? null,
              debtMinorUnits: player.debtMinorUnits
            }}
            loadTariffs={loadTariffs}
            loadPackages={loadPackages}
            onValidityChange={(valid) => setFormValid(valid)}
          />

          {error !== null && <p role="alert">{error}</p>}

          <div className="critical-confirmation-actions">
            <button type="button" disabled={busy} onClick={onClose}>{t('common.cancel')}</button>
            <button
              type="button"
              className="ui-btn ui-btn--primary"
              disabled={busy || !formValid || selectedSeat === null}
              onClick={() => void start()}
            >
              {t('op.players.session.start')}
            </button>
          </div>
        </div>
      )}
    </PanelModal>
  );
}
