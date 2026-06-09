import { useEffect, useState, type Dispatch, type MutableRefObject, type SetStateAction } from 'react';
import type { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type { OperatorAuthSession } from './authClient';
import { applyDeviceStatusToSeats, type OperatorFloorMapState } from './floorMapState';
import { createOperatorRealtimeClient, type OperatorRealtimeConnectionState } from './operatorRealtime';
import { seats } from './operatorData';
import type { AuthStatus, OperatorConfig } from './operatorTypes';
import {
  resolveActiveBranchId,
  matchesRealtimeScope,
  matchesCommandResultScope,
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
  setSelectedSeatId
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
              : nextState.seats[0]?.id ?? seats[0].id);
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

    const realtimeClient = createOperatorRealtimeClient({
      baseUrl: config.platformBaseUrl,
      getAccessToken: () => authSession.accessToken,
      onConnectionStateChanged: (state) => {
        if (!disposed) {
          setRealtimeState(state);
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
            seats: applyDeviceStatusToSeats(current.seats, status, t)
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
      }
    });

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
      void realtimeClient.stop();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

  return { realtimeState, realtimeError };
}
