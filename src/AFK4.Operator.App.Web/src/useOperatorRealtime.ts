import { useEffect, useState, type Dispatch, type MutableRefObject, type SetStateAction } from 'react';
import type { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type { OperatorAuthSession } from './authClient';
import { applyDeviceStatusToSeats, type OperatorFloorMapState } from './floorMapState';
import {
  createOperatorRealtimeClient,
  createPreviewOperatorRealtimeClient,
  type OperatorRealtimeConnectionState,
  type OperatorRealtimeOptions,
  type SessionLifecycleChangedDto
} from './operatorRealtime';
import type { AuthStatus, OperatorConfig } from './operatorTypes';
import {
  resolveActiveBranchId,
  matchesRealtimeScope,
  matchesCommandResultScope,
  matchesLifecycleScope,
  findSeatForDeviceStatus,
  shouldReloadFloorMapAfterDeviceStatus,
  loadBackendFloorMapState
} from './operatorHelpers';

type Translate = ReturnType<typeof useI18n>['t'];

export interface OperatorRealtime {
  realtimeState: OperatorRealtimeConnectionState;
  realtimeError: string | null;
}

export interface UseOperatorRealtimeOptions {
  authStatus: AuthStatus;
  authSession: OperatorAuthSession | null;
  config: OperatorConfig;
  t: Translate;
  floorMapRef: MutableRefObject<OperatorFloorMapState>;
  setFloorMap: Dispatch<SetStateAction<OperatorFloorMapState>>;
  setSelectedSeatId: Dispatch<SetStateAction<string>>;
  // Called (debounced) when a session-lifecycle push or a reconnect should reconcile the dashboard
  // KPIs; the shell hook re-fetches them on the bump.
  onShellReconcile?: () => void;
}

// Owns the operator realtime connection: floor-map device pushes patch seats optimistically and a
// debounced authoritative reload reconciles against the backend (the single source of truth).
export function useOperatorRealtime({
  authStatus,
  authSession,
  config,
  t,
  floorMapRef,
  setFloorMap,
  setSelectedSeatId,
  onShellReconcile
}: UseOperatorRealtimeOptions): OperatorRealtime {
  const [realtimeState, setRealtimeState] = useState<OperatorRealtimeConnectionState>('disconnected');
  const [realtimeError, setRealtimeError] = useState<string | null>(null);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      setRealtimeState('disconnected');
      setRealtimeError(null);
      return undefined;
    }

    const branchId = resolveActiveBranchId(authSession, config.branchId);
    if (!branchId) {
      return undefined;
    }

    let disposed = false;
    let reloadTimeoutId: number | null = null;
    let reloadInFlight = false;
    let reloadQueued = false;
    const scheduleAuthoritativeFloorMapReload = () => {
      if (reloadTimeoutId !== null) {
        window.clearTimeout(reloadTimeoutId);
      }

      reloadTimeoutId = window.setTimeout(() => {
        reloadTimeoutId = null;
        if (reloadInFlight) {
          reloadQueued = true;
          return;
        }

        reloadInFlight = true;
        void loadBackendFloorMapState(config, authSession, branchId, t)
          .then((nextState) => {
            if (disposed) {
              return;
            }

            floorMapRef.current = nextState;
            setFloorMap(nextState);
            setSelectedSeatId((currentSeatId) => nextState.seats.some((seat) => seat.id === currentSeatId)
              ? currentSeatId
              : nextState.seats[0]?.id ?? '');
          })
          .catch((error) => {
            if (!disposed) {
              setRealtimeError(projectOperatorError(error, t).detail);
            }
          })
          .finally(() => {
            reloadInFlight = false;
            if (!disposed && reloadQueued) {
              reloadQueued = false;
              scheduleAuthoritativeFloorMapReload();
            }
          });
      }, 100);
    };

    // Coalesce a burst of lifecycle pushes into a single dashboard reconcile.
    let shellReconcileTimeoutId: number | null = null;
    const scheduleShellReconcile = () => {
      if (!onShellReconcile) {
        return;
      }
      if (shellReconcileTimeoutId !== null) {
        window.clearTimeout(shellReconcileTimeoutId);
      }
      shellReconcileTimeoutId = window.setTimeout(() => {
        shellReconcileTimeoutId = null;
        if (!disposed) {
          onShellReconcile();
        }
      }, 100);
    };

    let wasReconnecting = false;
    const realtimeOptions: OperatorRealtimeOptions = {
      baseUrl: config.platformBaseUrl,
      getAccessToken: () => authSession.accessToken,
      onConnectionStateChanged: (state) => {
        if (disposed) {
          return;
        }
        setRealtimeState(state);
        if (state === 'reconnecting') {
          wasReconnecting = true;
        } else if (state === 'connected' && wasReconnecting) {
          // A reconnect may have missed events; force an authoritative reconcile of both surfaces.
          wasReconnecting = false;
          scheduleAuthoritativeFloorMapReload();
          scheduleShellReconcile();
        }
      },
      onDeviceStatusChanged: (status) => {
        if (disposed || !matchesRealtimeScope(status, authSession, branchId)) {
          return;
        }

        const matchingSeat = findSeatForDeviceStatus(floorMapRef.current.seats, status);
        const shouldReload = matchingSeat !== null && shouldReloadFloorMapAfterDeviceStatus(matchingSeat, status);
        setFloorMap((current) => {
          const nextState = {
            ...current,
            seats: applyDeviceStatusToSeats(current.seats, status, t),
            // A live device event means the on-screen data is fresh as of now. Advance the staleness
            // clock so the "offline — stale data" banner doesn't trip 30s after the last HTTP reload
            // on a floor map that realtime is actively keeping current. cachedAtMs previously only
            // moved on a full reload, so a live map went "offline" between reloads.
            cachedAtMs: Date.now()
          };
          floorMapRef.current = nextState;
          return nextState;
        });
        if (shouldReload) {
          scheduleAuthoritativeFloorMapReload();
        }
      },
      onDeviceCommandResult: (result) => {
        if (disposed || !matchesCommandResultScope(result, authSession, branchId)) {
          return;
        }

        scheduleAuthoritativeFloorMapReload();
      },
      onSessionLifecycleChanged: (change: SessionLifecycleChangedDto) => {
        if (disposed || !matchesLifecycleScope(change, authSession, branchId)) {
          return;
        }

        // The push is a hint: reconcile the floor map (authoritative seat + version) and the
        // dashboard KPIs against the backend rather than trusting the event's fields blindly.
        scheduleAuthoritativeFloorMapReload();
        scheduleShellReconcile();
      }
    };
    const realtimeClient = config.shellMode === 'vite-dev-preview'
      ? createPreviewOperatorRealtimeClient(realtimeOptions)
      : createOperatorRealtimeClient(realtimeOptions);

    setRealtimeError(null);
    realtimeClient.start()
      .catch((error) => {
        if (disposed) {
          return;
        }

        setRealtimeState('disconnected');
        setRealtimeError(projectOperatorError(error, t).detail);
      });

    return () => {
      disposed = true;
      if (reloadTimeoutId !== null) {
        window.clearTimeout(reloadTimeoutId);
      }
      if (shellReconcileTimeoutId !== null) {
        window.clearTimeout(shellReconcileTimeoutId);
      }
      void realtimeClient.stop();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

  return { realtimeState, realtimeError };
}
