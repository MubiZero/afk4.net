import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../PanelModal';
import { SessionStartForm, createSessionStartSelection, type SessionStartSelection } from '../session/SessionStartForm';
import { createAuthenticatedOperatorClients, createIdempotencyKey } from '../operatorHelpers';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { projectOperatorError } from '../apiErrors';
import type { PlayerClientItem } from '../operatorHelpers';
import type { OperatorBackendContext } from '../operatorTypes';

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

  useEffect(() => {
    let active = true;
    createAuthenticatedOperatorClients(backend.config, backend.session).floorMap
      .getFloorMap(backend.branchId)
      .then((map) => {
        if (!active) return;
        // Свободные места и без активной сессии — те же, что на Карте предлагают под запуск.
        const free = map.seats
          .filter((seat) => seat.activeSessionId === null && (seat.state === 'free' || seat.state === 'ready'))
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
        <p>{t('state.loading')}</p>
      ) : seats.length === 0 ? (
        <p>{error ?? t('op.players.session.noFreeSeats')}</p>
      ) : (
        <div className="clients-new-form">
          <label htmlFor="client-session-seat">{t('op.players.session.seatLabel')}</label>
          <select
            id="client-session-seat"
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
            loadTariffs={() => hasPermission(backend.session, permissionNames.viewTariffs)
              ? createAuthenticatedOperatorClients(backend.config, backend.session).settings.getTariffOptions(backend.branchId)
              : Promise.resolve([])}
            loadPackages={(playerAccountId) => hasPermission(backend.session, permissionNames.viewBilling)
              ? createAuthenticatedOperatorClients(backend.config, backend.session).players.getPlayerPackages(playerAccountId)
              : Promise.resolve([])}
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
