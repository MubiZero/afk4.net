import { useEffect, useMemo, useRef, useState, type Dispatch, type MutableRefObject, type SetStateAction } from 'react';
import type { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { refreshOperatorSession, type OperatorAuthSession } from './authClient';
import {
  createFixtureFloorMapState,
  hydrateFloorMapStateFromCache,
  refreshFloorMapRemaining,
  type OperatorFloorMapState
} from './floorMapState';
import { loadFloorMapCache } from './floorMapCache';
import {
  acknowledgeAction,
  enqueueAction,
  loadActionOutbox,
  reconcileActionOutbox,
  type OperatorCommandType
} from './actionOutbox';
import type { SeatSummary } from './operatorData';
import type {
  AuthStatus,
  OperatorConfig,
  OperatorBackendContext,
  SeatActionResult,
  SeatActionRequest,
  PcControlActionId,
  PcControlActionResult
} from './operatorTypes';
import { permissionNames, hasPermission } from './operatorPermissions';
import {
  defaultSessionDurationMinutes,
  isPendingSeatCommand,
  createAuthenticatedOperatorClients,
  isUnauthorizedPlatformError,
  clearStoredOperatorSession,
  loadBackendFloorMapState,
  createIdempotencyKey,
  requireBackend,
  describeSeatActionResult,
  describeDispatchedDeviceCommand,
  describeTechModeResult,
  projectAuthSignInError
} from './operatorHelpers';

type Translate = ReturnType<typeof useI18n>['t'];

export interface UseFloorMapOptions {
  config: OperatorConfig;
  t: Translate;
  authStatus: AuthStatus;
  authSession: OperatorAuthSession | null;
  // Активный филиал (реактивный выбор из свитчера — Task 11), а не выведенный внутри хука через
  // замороженный 2-арг resolveActiveBranchId; свитчер должен переносить сюда И загрузку карты,
  // И billing-действия (start/extend/transfer/checkout/end), иначе оператор видит новый филиал,
  // но оперирует старым.
  activeBranchId: string | null;
  backendContext: OperatorBackendContext | null;
  setAuthSession: Dispatch<SetStateAction<OperatorAuthSession | null>>;
  setAuthStatus: Dispatch<SetStateAction<AuthStatus>>;
  setAuthError: Dispatch<SetStateAction<string | null>>;
}

export interface FloorMap {
  floorMap: OperatorFloorMapState;
  floorMapRef: MutableRefObject<OperatorFloorMapState>;
  displayedFloorMap: OperatorFloorMapState;
  selectedSeat: SeatSummary | null;
  selectedSeatId: string;
  setSelectedSeatId: Dispatch<SetStateAction<string>>;
  setFloorMap: Dispatch<SetStateAction<OperatorFloorMapState>>;
  offlineActionAudit: string[];
  handleSeatAction: (request: SeatActionRequest) => Promise<SeatActionResult>;
  handlePcControlAction: (seat: SeatSummary, action: PcControlActionId) => Promise<PcControlActionResult>;
}

// Owns the operator floor map: authoritative load (with token-refresh + offline-cache fallback), the
// live 1s remaining-time tick, seat lifecycle actions, and offline lock/unlock queue draining.
export function useFloorMap({
  config,
  t,
  authStatus,
  authSession,
  activeBranchId,
  backendContext,
  setAuthSession,
  setAuthStatus,
  setAuthError
}: UseFloorMapOptions): FloorMap {
  const [floorMap, setFloorMap] = useState<OperatorFloorMapState>(() => createFixtureFloorMapState());
  const floorMapRef = useRef(floorMap);
  const [selectedSeatId, setSelectedSeatId] = useState('');
  const [remainingNowMs, setRemainingNowMs] = useState(() => Date.now());
  const [offlineActionAudit, setOfflineActionAudit] = useState<string[]>([]);

  const displayedFloorMap = useMemo(
    () => refreshFloorMapRemaining(floorMap, t, remainingNowMs),
    [floorMap, remainingNowMs, t]
  );
  const selectedSeat = displayedFloorMap.seats.find((seat) => seat.id === selectedSeatId) ?? displayedFloorMap.seats[0] ?? null;

  useEffect(() => {
    floorMapRef.current = floorMap;
  }, [floorMap]);

  useEffect(() => {
    const intervalId = window.setInterval(() => setRemainingNowMs(Date.now()), 1000);
    return () => window.clearInterval(intervalId);
  }, []);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      setFloorMap(createFixtureFloorMapState());
      setSelectedSeatId('');
      return undefined;
    }

    const branchId = activeBranchId;
    if (!branchId) {
      setFloorMap((current) => ({
        ...current,
        loadStatus: 'failed',
        error: t('op.dashboard.noBranch')
      }));
      return undefined;
    }

    let disposed = false;

    setFloorMap((current) => ({
      ...current,
      branchId,
      loadStatus: 'loading',
      error: null
    }));

    void (async () => {
      try {
        const nextState = await loadBackendFloorMapState(config, authSession, branchId, t);
        if (disposed) {
          return;
        }

        setFloorMap(nextState);
        setSelectedSeatId(nextState.seats[0]?.id ?? '');
        void drainQueuedActions(nextState.seats);
      } catch (error) {
        if (isUnauthorizedPlatformError(error)) {
          try {
            const nextSession = await refreshOperatorSession();
            // A token refresh doesn't move the operator's chosen branch — reuse the reactive
            // activeBranchId prop rather than re-deriving it from the refreshed session.
            const nextBranchId = activeBranchId;
            if (!nextBranchId) {
              throw new Error(t('op.dashboard.noBranch'));
            }

            const nextState = await loadBackendFloorMapState(config, nextSession, nextBranchId, t);
            if (disposed) {
              return;
            }

            setAuthSession(nextSession);
            setFloorMap(nextState);
            setSelectedSeatId(nextState.seats[0]?.id ?? '');
            return;
          } catch (refreshError) {
            await clearStoredOperatorSession();
            if (disposed) {
              return;
            }

            setAuthSession(null);
            setAuthStatus('signed-out');
            setAuthError(projectAuthSignInError(refreshError, t));
            setFloorMap(createFixtureFloorMapState());
            setSelectedSeatId('');
            return;
          }
        }

        if (disposed) {
          return;
        }

        // Platform unreachable: hydrate the last-known-good snapshot into a read-only mirror (§6.5).
        // Fall back to the error surface only when there is nothing cached for this branch.
        const cached = loadFloorMapCache(branchId);
        if (cached) {
          const degraded = hydrateFloorMapStateFromCache(cached, branchId, t);
          setFloorMap(degraded);
          setSelectedSeatId((current) => current || degraded.seats[0]?.id || '');
          return;
        }

        setFloorMap((current) => ({
          ...current,
          branchId,
          loadStatus: 'failed',
          error: projectOperatorError(error, t).detail
        }));
      }
    })();

    return () => {
      disposed = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authStatus, authSession, activeBranchId, config.platformBaseUrl]);

  const handleSeatAction = async (request: SeatActionRequest): Promise<SeatActionResult> => {
    const session = authSession;
    if (session === null) {
      throw new Error(t('op.shell.err.notSignedIn'));
    }

    const branchId = activeBranchId;
    if (!branchId) {
      throw new Error(t('op.dashboard.noBranch'));
    }

    if (floorMap.source !== 'backend') {
      throw new Error(t('op.shell.err.mapNotLoaded'));
    }

    if (isPendingSeatCommand(request.seat)) {
      throw new Error(t('op.map.panel.confirmStatusPending'));
    }

    const clients = createAuthenticatedOperatorClients(config, session);
    let response: unknown;
    if (request.type === 'start') {
      if (!hasPermission(session, permissionNames.startSession)) {
        throw new Error(t('op.shell.err.noPermStart'));
      }

      if (request.seat.tone !== 'ready' || request.seat.activeSessionId) {
        throw new Error(t('op.shell.err.pcNotReady'));
      }

      const billing = request.billing;
      const isOpenTab = request.durationMode === 'open';
      response = await clients.sessions.startGuestSession(branchId, {
        organizationId: session.organizationId,
        seatId: request.seat.id,
        durationMode: isOpenTab ? 'open' : 'fixed',
        durationMinutes: isOpenTab ? null : (request.durationMinutes ?? defaultSessionDurationMinutes),
        tariffRuleVersionId: billing.tariffRuleVersionId,
        idempotencyKey: createIdempotencyKey('session-start'),
        playerAccountId: billing.playerAccountId ?? null,
        billingMode: billing.mode === 'guest' ? '' : billing.mode,
        tariffVersionId: billing.tariffVersionId ?? null,
        playerPackageId: billing.playerPackageId ?? null,
        isComp: request.isComp ?? false,
        compReason: request.compReason ?? null
      });
    } else if (request.type === 'extend') {
      if (!hasPermission(session, permissionNames.extendSession)) {
        throw new Error(t('op.shell.err.noPermExtend'));
      }

      if (!request.seat.activeSessionId) {
        throw new Error(t('op.map.panel.noActiveSession'));
      }

      const billing = request.billing;
      response = await clients.sessions.extendSession(request.seat.activeSessionId, {
        additionalMinutes: request.minutes,
        tariffRuleVersionId: billing.tariffRuleVersionId,
        idempotencyKey: createIdempotencyKey('session-extend'),
        playerAccountId: billing.playerAccountId ?? null,
        billingMode: billing.mode === 'guest' ? '' : billing.mode,
        tariffVersionId: billing.tariffVersionId ?? null,
        playerPackageId: billing.playerPackageId ?? null
      });
    } else if (request.type === 'transfer') {
      if (!hasPermission(session, permissionNames.transferSession)) {
        throw new Error(t('op.shell.err.noPermTransfer'));
      }

      if (!request.seat.activeSessionId) {
        throw new Error(t('op.map.panel.noActiveSession'));
      }

      response = await clients.sessions.transferSession(request.seat.activeSessionId, {
        targetSeatId: request.targetSeatId,
        idempotencyKey: createIdempotencyKey('session-transfer')
      });
    } else if (request.type === 'checkout') {
      if (!hasPermission(session, permissionNames.endSession)) {
        throw new Error(t('op.shell.err.noPermCheckout'));
      }

      if (!request.seat.activeSessionId) {
        throw new Error(t('op.map.panel.noActiveSession'));
      }

      response = await clients.sessions.checkoutSession(request.seat.activeSessionId, {
        organizationId: session.organizationId,
        payments: request.payments,
        idempotencyKey: createIdempotencyKey('session-checkout')
      });
    } else {
      if (!hasPermission(session, permissionNames.endSession)) {
        throw new Error(t('op.shell.err.noPermEnd'));
      }

      if (!request.seat.activeSessionId) {
        throw new Error(t('op.map.panel.noActiveSession'));
      }

      response = await clients.sessions.endSession(request.seat.activeSessionId, {
        reason: 'operator',
        idempotencyKey: createIdempotencyKey('session-end')
      });
    }

    const detail = await describeSeatActionResult(clients, session, request.seat, response, t);
    const nextState = await loadBackendFloorMapState(config, session, branchId, t);
    const preferredSeatId = request.type === 'transfer' ? request.targetSeatId : request.seat.id;
    setFloorMap(nextState);
    setSelectedSeatId(nextState.seats.some((seat) => seat.id === preferredSeatId)
      ? preferredSeatId
      : nextState.seats[0]?.id ?? '');
    return { detail };
  };

  // On reconnect (§6.5): replay queued lock/unlock against the now-authoritative map; drop actions whose
  // seat or session moved on while offline, recording an operator-visible audit note (D9).
  const drainQueuedActions = async (liveSeats: SeatSummary[]): Promise<void> => {
    const pending = loadActionOutbox();
    if (pending.length === 0) {
      return;
    }

    const { replay, dropped } = reconcileActionOutbox(pending, liveSeats, t);
    for (const drop of dropped) {
      acknowledgeAction(drop.entry.idempotencyKey);
    }
    if (dropped.length > 0) {
      setOfflineActionAudit((current) => [...current, ...dropped.map((drop) => drop.note)]);
    }

    if (replay.length === 0) {
      return;
    }

    const nextBackend = backendContext;
    if (nextBackend === null || !hasPermission(nextBackend.session, permissionNames.dispatchDeviceCommand)) {
      return;
    }

    const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
    for (const entry of replay) {
      try {
        await clients.devices.dispatchDeviceCommand(entry.deviceId, {
          type: entry.commandType,
          payload: { reason: 'operator-offline-replay', source: 'operator-map', seatId: entry.seatId }
        });
        acknowledgeAction(entry.idempotencyKey);
      } catch {
        // Connectivity dropped again mid-drain; keep the rest queued for the next refresh.
        break;
      }
    }
  };

  const handlePcControlAction = async (seat: SeatSummary, action: PcControlActionId): Promise<PcControlActionResult> => {
    const nextBackend = requireBackend(backendContext, t);
    if (!seat.deviceId) {
      throw new Error(t('op.shell.err.noDevice'));
    }

    const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
    if (action === 'status') {
      if (!hasPermission(nextBackend.session, permissionNames.viewDiagnostics) ||
        !hasPermission(nextBackend.session, permissionNames.viewDeviceDetail)) {
        throw new Error(t('op.shell.err.noPermDiag'));
      }

      const [device, diagnostics] = await Promise.all([
        clients.devices.getDeviceDetail(seat.deviceId),
        clients.diagnostics.getDiagnostics(nextBackend.branchId)
      ]);

      return { detail: describeTechModeResult(seat, device, diagnostics, t) };
    }

    if (action === 'lock' || action === 'unlock') {
      if (!hasPermission(nextBackend.session, permissionNames.dispatchDeviceCommand)) {
        throw new Error(t('op.shell.err.noPermDispatch'));
      }

      // Offline (§6.5): queue the idempotent lock/unlock locally instead of dispatching; it replays (or is
      // dropped if superseded) on reconnect. Billing actions stay online-only (D2) and are gated elsewhere.
      if (floorMapRef.current.isOffline) {
        enqueueAction({
          idempotencyKey: createIdempotencyKey(`device-${action}`),
          deviceId: seat.deviceId,
          seatId: seat.id,
          seatName: seat.name,
          commandType: action as OperatorCommandType,
          expectedSessionId: seat.activeSessionId ?? null,
          queuedAtMs: Date.now()
        });
        return { detail: t('op.shell.queuedCommand', { action }) };
      }

      const command = await clients.devices.dispatchDeviceCommand(seat.deviceId, {
        type: action,
        payload: {
          reason: 'operator-pc-control',
          source: 'operator-map',
          seatId: seat.id
        }
      });
      return { detail: await describeDispatchedDeviceCommand(clients, nextBackend.session, seat, command, t) };
    }

    throw new Error(t('op.shell.err.commandUnsupported'));
  };

  return {
    floorMap,
    floorMapRef,
    displayedFloorMap,
    selectedSeat,
    selectedSeatId,
    setSelectedSeatId,
    setFloorMap,
    offlineActionAudit,
    handleSeatAction,
    handlePcControlAction
  };
}
