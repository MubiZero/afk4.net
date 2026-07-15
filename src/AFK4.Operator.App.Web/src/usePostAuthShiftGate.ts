import { useCallback, useEffect, useRef, useState } from 'react';
import type { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type { OpenShiftRequest } from './api/clients/shifts';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import { hasPermission, permissionNames } from './operatorPermissions';
import type { AuthStatus, OperatorBackendContext } from './operatorTypes';

type Translate = ReturnType<typeof useI18n>['t'];

export type PostAuthShiftGateStatus =
  | 'not-required'
  | 'checking'
  | 'required'
  | 'opening'
  | 'failed'
  | 'ready';

export interface PostAuthShiftClient {
  getCurrentShift(branchId: string): Promise<unknown | null>;
  openShift(branchId: string, request: OpenShiftRequest): Promise<unknown>;
}

export interface PostAuthShiftGateController {
  status: PostAuthShiftGateStatus;
  error: string | null;
  retry(): void;
  openShift(request: OpenShiftRequest): Promise<void>;
}

interface GateSnapshot {
  key: string | null;
  status: PostAuthShiftGateStatus;
  error: string | null;
}

function resolveClient(
  backend: OperatorBackendContext,
  injected: PostAuthShiftClient | undefined
): PostAuthShiftClient {
  return injected ?? createAuthenticatedOperatorClients(backend.config, backend.session).shifts;
}

export function usePostAuthShiftGate({
  authStatus,
  backend,
  t,
  client
}: {
  authStatus: AuthStatus;
  backend: OperatorBackendContext | null;
  t: Translate;
  client?: PostAuthShiftClient;
}): PostAuthShiftGateController {
  const eligible = authStatus === 'signed-in'
    && backend !== null
    && hasPermission(backend.session, permissionNames.openShift);
  const gateKey = eligible
    ? `${backend.session.staffUserId}:${backend.branchId}`
    : null;
  const [retryNonce, setRetryNonce] = useState(0);
  const [snapshot, setSnapshot] = useState<GateSnapshot>({
    key: null,
    status: 'not-required',
    error: null
  });
  const generationRef = useRef(0);

  useEffect(() => {
    const generation = ++generationRef.current;
    if (gateKey === null || backend === null) {
      setSnapshot({ key: null, status: 'not-required', error: null });
      return undefined;
    }

    let disposed = false;
    setSnapshot({ key: gateKey, status: 'checking', error: null });
    const shifts = resolveClient(backend, client);
    void shifts.getCurrentShift(backend.branchId)
      .then((shift) => {
        if (disposed || generationRef.current !== generation) return;
        setSnapshot({
          key: gateKey,
          status: shift === null ? 'required' : 'ready',
          error: null
        });
      })
      .catch((error) => {
        if (disposed || generationRef.current !== generation) return;
        setSnapshot({
          key: gateKey,
          status: 'failed',
          error: projectOperatorError(error, t).detail
        });
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, client, gateKey, retryNonce, t]);

  const retry = useCallback(() => {
    setRetryNonce((value) => value + 1);
  }, []);

  const openShift = useCallback(async (request: OpenShiftRequest) => {
    if (gateKey === null || backend === null) return;

    const generation = ++generationRef.current;
    const shifts = resolveClient(backend, client);
    setSnapshot({ key: gateKey, status: 'opening', error: null });

    try {
      await shifts.openShift(backend.branchId, request);
      if (generationRef.current !== generation) return;
      setSnapshot({ key: gateKey, status: 'ready', error: null });
    } catch (error) {
      try {
        const concurrentShift = await shifts.getCurrentShift(backend.branchId);
        if (generationRef.current !== generation) return;
        if (concurrentShift !== null) {
          setSnapshot({ key: gateKey, status: 'ready', error: null });
          return;
        }
      } catch {
        // Preserve the actionable open error when the reconciliation read also fails.
      }

      if (generationRef.current !== generation) return;
      setSnapshot({
        key: gateKey,
        status: 'failed',
        error: projectOperatorError(error, t).detail
      });
    }
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, client, gateKey, t]);

  const keyMatches = gateKey !== null && snapshot.key === gateKey;
  return {
    status: gateKey === null
      ? 'not-required'
      : keyMatches
        ? snapshot.status
        : 'checking',
    error: keyMatches ? snapshot.error : null,
    retry,
    openShift
  };
}
