import { useEffect, useMemo, useState } from 'react';
import { postHostRequest } from './hostBridge';
import { isOperatorTenantBlocked } from './ConnectionResolutionScreen';
import {
  BridgeOperatorConnectionStorage,
  ConnectionResolver,
  LocalStorageOperatorConnectionStorage,
  type OperatorConnectionStorage,
  type ResolvedOperatorConnection,
  type ResolveOperatorConnectionResponse
} from './connectionResolver';
import type { OperatorConfig } from './operatorTypes';

function createOperatorConnectionStorage(): OperatorConnectionStorage {
  if (typeof window !== 'undefined' && window.chrome?.webview) {
    return new BridgeOperatorConnectionStorage(postHostRequest);
  }
  return new LocalStorageOperatorConnectionStorage();
}

export interface OperatorConnection {
  config: OperatorConfig;
  connectionResolver: ConnectionResolver;
  needsConnectionResolution: boolean;
  isConnectionLoading: boolean;
  blockedResolution: ResolveOperatorConnectionResponse | null;
  handleConnectionResolved: (resolution: ResolveOperatorConnectionResponse) => Promise<void>;
  handleChangeConnection: () => Promise<void>;
}

// Resolves the operator's org/branch binding from local (or host-bridge) storage and layers it over the
// base config. A blocked/suspended tenant is surfaced separately so the shell can show the blocked screen.
export function useOperatorConnection(baseConfig: OperatorConfig): OperatorConnection {
  const connectionStorage = useMemo(() => createOperatorConnectionStorage(), []);
  const [resolvedConnection, setResolvedConnection] = useState<ResolvedOperatorConnection | null>(
    () => connectionStorage.loadSync()
  );
  const [isConnectionLoading, setIsConnectionLoading] = useState<boolean>(
    () => connectionStorage.loadSync() === null
  );
  const [blockedResolution, setBlockedResolution] = useState<ResolveOperatorConnectionResponse | null>(null);
  const config = useMemo<OperatorConfig>(() => {
    if (resolvedConnection === null) {
      return baseConfig;
    }
    return {
      ...baseConfig,
      organizationId: baseConfig.organizationId ?? resolvedConnection.organizationId,
      branchId: baseConfig.branchId ?? resolvedConnection.branchId
    };
  }, [baseConfig, resolvedConnection]);
  const connectionResolver = useMemo(
    () => new ConnectionResolver({ baseUrl: baseConfig.platformBaseUrl }),
    [baseConfig.platformBaseUrl]
  );
  // Plain-browser production builds never carry org/branch in config by design (see
  // browserConfigFromEnv in operatorConfig.ts) — the two-step sign-in flow (login → server picks
  // the club, 409 ChooseClubError → pick club) resolves the org itself. Connection-resolution
  // (device-to-branch pairing) is a WPF/kiosk concern only, so the browser runtime skips this gate
  // entirely rather than getting stuck on a pairing screen it has no use for.
  const needsConnectionResolution =
    config.runtime !== 'browser' && (!config.organizationId || !config.branchId);

  useEffect(() => {
    let disposed = false;

    connectionStorage.load()
      .then((connection) => {
        if (disposed) {
          return;
        }
        if (connection !== null) {
          setResolvedConnection(connection);
        }
        setIsConnectionLoading(false);
      })
      .catch(() => {
        if (disposed) {
          return;
        }
        setIsConnectionLoading(false);
      });

    return () => {
      disposed = true;
    };
  }, [connectionStorage]);

  const handleConnectionResolved = async (resolution: ResolveOperatorConnectionResponse) => {
    if (isOperatorTenantBlocked(resolution)) {
      await connectionStorage.clear();
      setResolvedConnection(null);
      setBlockedResolution(resolution);
      return;
    }

    const stored = await connectionStorage.save(resolution);
    setResolvedConnection(stored);
    setBlockedResolution(null);
  };

  const handleChangeConnection = async () => {
    await connectionStorage.clear();
    setResolvedConnection(null);
    setBlockedResolution(null);
  };

  return {
    config,
    connectionResolver,
    needsConnectionResolution,
    isConnectionLoading,
    blockedResolution,
    handleConnectionResolved,
    handleChangeConnection
  };
}
