import {
  AlertTriangle,
  CircleDollarSign,
  LockKeyhole,
  Maximize2,
  Minus,
  MonitorCheck,
  Search,
  Wifi,
  X
} from 'lucide-react';
import { useEffect, useMemo, useRef, useState, type FormEvent, type MouseEvent } from 'react';
import { I18nProvider, useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { loadOperatorSession, refreshOperatorSession, signInOperator, signOutOperator, type OperatorAuthSession, type OperatorSignInRequest } from './authClient';
import {
  applyDeviceStatusToSeats,
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
import { postHostRequest, postHostWindowCommand, postHostWindowResize, type HostWindowResizeEdge } from './hostBridge';
import { ConnectionResolutionScreen, isOperatorTenantBlocked } from './ConnectionResolutionScreen';
import {
  BridgeOperatorConnectionStorage,
  ConnectionResolver,
  LocalStorageOperatorConnectionStorage,
  OperatorTenantStatus,
  type OperatorConnectionStorage,
  type ResolvedOperatorConnection,
  type ResolveOperatorConnectionResponse
} from './connectionResolver';
import {
  type OperatorDashboardSummaryDto,
  type ShiftDto
} from './operatorApiClients';
import { getOperatorConfig } from './operatorConfig';
import {
  createOperatorRealtimeClient,
  type OperatorRealtimeConnectionState
} from './operatorRealtime';
import { navItems, seats, type SeatSummary } from './operatorData';
import { PaymentGatewaysWorkspace } from './PaymentGatewaysWorkspace';
import { AccountPanel } from './AccountPanel';
import { BackendBookingWorkspace } from './BackendBookingWorkspace';
import { DashboardWorkspace } from './DashboardWorkspace';
import { MapWorkspace } from './MapWorkspace';
import { MapSidePanel } from './MapSidePanel';
import { SummarySidePanel } from './SummarySidePanel';
import { BackendPosWorkspace } from './BackendPosWorkspace';
import { BackendPlayersWorkspace } from './BackendPlayersWorkspace';
import { BackendPaymentsWorkspace } from './BackendPaymentsWorkspace';
import { ReviewWorkspace } from './ReviewWorkspace';
import { BackendLogsWorkspace } from './BackendLogsWorkspace';
import { BackendSettingsWorkspace } from './BackendSettingsWorkspace';
import { ForgotPassword } from './ForgotPassword';
import type {
  WorkspaceId,
  AuthStatus,
  LoadStatus,
  OperatorConfig,
  OperatorBackendContext,
  SeatActionResult,
  SeatActionRequest,
  PcControlActionId,
  PcControlActionResult
} from './operatorTypes';
import {
  workspaceIds,
  permissionNames,
  hasPermission,
  canOpenWorkspace,
  firstAllowedWorkspace
} from './operatorPermissions';
import {
  defaultSessionDurationMinutes,
  shellOperationalRefreshMs,
  toDateInputValue,
  countByTone,
  countProblems,
  isPendingSeatCommand,
  operatorDisplayNameLabel,
  dataSourceLabel,
  shellShiftLabel,
  shellPosLabel,
  shellModeLabel,
  describeTechModeResult,
  projectAuthHostError,
  realtimeLabel,
  resolveActiveBranchId,
  matchesRealtimeScope,
  matchesCommandResultScope,
  findSeatForDeviceStatus,
  shouldReloadFloorMapAfterDeviceStatus,
  createAuthenticatedOperatorClients,
  isUnauthorizedPlatformError,
  isUnauthorizedAuthError,
  clearStoredOperatorSession,
  loadBackendFloorMapState,
  createIdempotencyKey,
  isGuid,
  dashboardRangeQuery,
  requireBackend,
  describeSeatActionResult,
  describeDispatchedDeviceCommand
} from './operatorHelpers';


function handleWindowDragStart(event: MouseEvent<HTMLElement>) {
  if (event.button !== 0) {
    return;
  }

  const target = event.target as HTMLElement;
  if (event.detail > 1 || target.closest('button, input, select, textarea, .command-search, .window-resize-handle')) {
    return;
  }

  postHostWindowCommand('drag');
}

function handleWindowTitleDoubleClick(event: MouseEvent<HTMLElement>) {
  const target = event.target as HTMLElement;
  if (target.closest('button, input, select, textarea, .command-search, .window-resize-handle')) {
    return;
  }

  postHostWindowCommand('maximize');
}

function WindowControls() {
  const { t } = useI18n();
  return (
    <div className="window-controls" aria-label={t('op.shell.window')}>
      <button type="button" title={t('op.shell.minimize')} aria-label={t('op.shell.minimize')} onClick={() => postHostWindowCommand('minimize')}>
        <Minus size={15} />
      </button>
      <button type="button" title={t('op.shell.maximize')} aria-label={t('op.shell.maximize')} onClick={() => postHostWindowCommand('maximize')}>
        <Maximize2 size={13} />
      </button>
      <button type="button" title={t('op.shell.close')} aria-label={t('op.shell.close')} onClick={() => postHostWindowCommand('close')}>
        <X size={15} />
      </button>
    </div>
  );
}

function WindowResizeHandles() {
  const edges: HostWindowResizeEdge[] = ['top', 'right', 'bottom', 'left', 'top-left', 'top-right', 'bottom-left', 'bottom-right'];

  return (
    <div className="window-resize-handles" aria-hidden="true">
      {edges.map((edge) => (
        <div
          key={edge}
          className={`window-resize-handle ${edge}`}
          onMouseDown={(event) => {
            if (event.button !== 0) {
              return;
            }

            event.preventDefault();
            event.stopPropagation();
            postHostWindowResize(edge);
          }}
        />
      ))}
    </div>
  );
}

function SignInScreen({
  config,
  authStatus,
  hostError,
  onSignIn,
  onForgotPassword
}: {
  config: ReturnType<typeof getOperatorConfig>;
  authStatus: AuthStatus;
  hostError: string | null;
  onSignIn: (request: OperatorSignInRequest) => Promise<void>;
  onForgotPassword: () => void;
}) {
  const { t } = useI18n();
  const [userName, setUserName] = useState('');
  const [password, setPassword] = useState('');
  const [isBusy, setIsBusy] = useState(false);
  const [error, setError] = useState<string | null>(hostError);
  const isChecking = authStatus === 'checking';

  useEffect(() => {
    setError(hostError);
  }, [hostError]);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setError(null);

    const organizationId = config.organizationId?.trim() ?? '';
    if (!isGuid(organizationId)) {
      setError(t('op.auth.connectionMissing'));
      return;
    }

    if (!userName.trim()) {
      setError(t('auth.error.required'));
      return;
    }

    if (!password) {
      setError(t('auth.error.required'));
      return;
    }

    setIsBusy(true);
    try {
      await onSignIn({
        organizationId: organizationId.trim(),
        userName: userName.trim(),
        password
      });
      setPassword('');
    } catch (nextError) {
      setError(projectAuthHostError(nextError, config, t));
    } finally {
      setIsBusy(false);
    }
  };

  return (
    <div className="operator-shell auth-shell">
      <WindowResizeHandles />
      <header className="top-command auth-top-command" onMouseDown={handleWindowDragStart} onDoubleClick={handleWindowTitleDoubleClick}>
        <div className="brand-block">
          <img className="brand-logo" src="/afk4-logo-horizontal.svg" alt="AFK4.NET" />
          <span>{t('op.auth.operator')}</span>
        </div>
        <div className="top-status">
          <span><Wifi size={14} />{isChecking ? t('op.shell.checkingAuth') : t('op.shell.secureAuth')}</span>
          <span>{config.platformBaseUrl}</span>
          <span>{shellModeLabel(config.shellMode, t)}</span>
        </div>
        <WindowControls />
      </header>

      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>{t('op.shell.appName')}</span>
            <h1>{t('op.shell.signInTitle')}</h1>
            <p>{t('op.shell.storageNote')}</p>
          </header>

          <form className="auth-form" onSubmit={submit}>
            <label>
              {t('auth.field.login')}
              <input
                value={userName}
                onChange={(event) => setUserName(event.currentTarget.value)}
                autoComplete="username"
                autoFocus
              />
            </label>
            <label>
              {t('auth.field.password')}
              <input
                type="password"
                value={password}
                onChange={(event) => setPassword(event.currentTarget.value)}
                autoComplete="current-password"
              />
            </label>

            <button type="submit" className="primary-wide" disabled={isBusy || isChecking}>
              {isBusy ? t('auth.action.signingIn') : t('auth.action.signIn')}
            </button>
          </form>

          <button type="button" className="auth-link" onClick={onForgotPassword}>
            {t('auth.forgot.link')}
          </button>

          {error && (
            <div className="auth-error" role="alert">
              <AlertTriangle size={16} />
              <span>{error}</span>
            </div>
          )}
        </section>

        <aside className="auth-context-panel">
          <section>
            <span>{t('op.shell.platform')}</span>
            <strong>{config.platformBaseUrl}</strong>
          </section>
          <section>
            <span>{t('op.shell.currency')}</span>
            <strong>{config.currencyCode}</strong>
          </section>
          <section>
            <span>{t('op.shell.storage')}</span>
            <strong>{t('op.shell.secureStorage')}</strong>
          </section>
        </aside>
      </main>
    </div>
  );
}

function BlockedTenantScreen({
  resolution,
  onChangeConnection
}: {
  resolution: ResolveOperatorConnectionResponse;
  onChangeConnection: () => void;
}) {
  const { t } = useI18n();
  const isDeletionPending = resolution.organizationStatus === OperatorTenantStatus.DeletionPending;
  const headline = isDeletionPending ? t('op.shell.club.deletionPending') : t('op.shell.club.suspended');
  const reason = resolution.organizationStatusReason?.trim();
  return (
    <div className="operator-shell auth-shell">
      <WindowResizeHandles />
      <header
        className="top-command auth-top-command"
        onMouseDown={handleWindowDragStart}
        onDoubleClick={handleWindowTitleDoubleClick}
      >
        <div className="brand-block">
          <img className="brand-logo" src="/afk4-logo-horizontal.svg" alt="AFK4.NET" />
          <span>{t('op.auth.operator')}</span>
        </div>
        <WindowControls />
      </header>

      <main className="auth-workspace">
        <section className="auth-panel">
          <header>
            <span>{resolution.organizationName}</span>
            <h1>{headline}</h1>
            <p>
              {reason !== undefined && reason.length > 0
                ? reason
                : t('op.shell.club.contactOwner')}
            </p>
          </header>

          <div className="auth-error" role="alert">
            <AlertTriangle size={16} />
            <span>
              {isDeletionPending
                ? t('op.shell.club.deletionMsg')
                : t('op.shell.club.suspendedMsg')}
            </span>
          </div>

          <button type="button" className="primary-wide" onClick={onChangeConnection}>
            {t('op.shell.club.changeConnection')}
          </button>
        </section>
      </main>
    </div>
  );
}

function createOperatorConnectionStorage(): OperatorConnectionStorage {
  if (typeof window !== 'undefined' && window.chrome?.webview) {
    return new BridgeOperatorConnectionStorage(postHostRequest);
  }
  return new LocalStorageOperatorConnectionStorage();
}

export function App() {
  return (
    <I18nProvider>
      <AppInner />
    </I18nProvider>
  );
}

function AppInner() {
  const { t } = useI18n();
  const baseConfig = getOperatorConfig();
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
  const needsConnectionResolution = !config.organizationId || !config.branchId;
  const [workspace, setWorkspace] = useState<WorkspaceId>('map');
  const [selectedSeatId, setSelectedSeatId] = useState(seats[0].id);
  const [authStatus, setAuthStatus] = useState<AuthStatus>('checking');
  const [authSession, setAuthSession] = useState<OperatorAuthSession | null>(null);
  const [authError, setAuthError] = useState<string | null>(null);
  const [authView, setAuthView] = useState<'signIn' | 'forgot'>('signIn');
  const [workspaceFeedback, setWorkspaceFeedback] = useState<string | null>(null);
  const [floorMap, setFloorMap] = useState<OperatorFloorMapState>(() => createFixtureFloorMapState());
  const floorMapRef = useRef(floorMap);
  const [remainingNowMs, setRemainingNowMs] = useState(() => Date.now());
  const [realtimeState, setRealtimeState] = useState<OperatorRealtimeConnectionState>('disconnected');
  const [realtimeError, setRealtimeError] = useState<string | null>(null);
  const [shellCurrentShift, setShellCurrentShift] = useState<ShiftDto | null>(null);
  const [shellDashboardSummary, setShellDashboardSummary] = useState<OperatorDashboardSummaryDto | null>(null);
  const [shellLoadStatus, setShellLoadStatus] = useState<LoadStatus>('loading');
  const [shellLoadError, setShellLoadError] = useState<string | null>(null);
  const [offlineActionAudit, setOfflineActionAudit] = useState<string[]>([]);
  const [accountPanelOpen, setAccountPanelOpen] = useState(false);
  const displayedFloorMap = useMemo(
    () => refreshFloorMapRemaining(floorMap, t, remainingNowMs),
    [floorMap, remainingNowMs, t]
  );
  const selectedSeat = displayedFloorMap.seats.find((seat) => seat.id === selectedSeatId) ?? displayedFloorMap.seats[0] ?? null;
  const activeBranchId = authSession === null ? null : resolveActiveBranchId(authSession, config.branchId);
  const backendContext: OperatorBackendContext | null = authSession !== null && activeBranchId !== null
    ? { config, session: authSession, branchId: activeBranchId }
    : null;
  const canUsePcControl = (hasPermission(authSession, permissionNames.viewDiagnostics)
    && hasPermission(authSession, permissionNames.viewDeviceDetail))
    || hasPermission(authSession, permissionNames.dispatchDeviceCommand);
  const shellShiftText = shellShiftLabel(shellCurrentShift, shellDashboardSummary, shellLoadStatus, shellLoadError, t);
  const shellPosText = shellPosLabel(shellDashboardSummary, shellLoadStatus, t);

  useEffect(() => {
    floorMapRef.current = floorMap;
  }, [floorMap]);

  useEffect(() => {
    const intervalId = window.setInterval(() => setRemainingNowMs(Date.now()), 1000);
    return () => window.clearInterval(intervalId);
  }, []);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      setShellCurrentShift(null);
      setShellDashboardSummary(null);
      setShellLoadStatus('loading');
      setShellLoadError(null);
      return undefined;
    }

    const branchId = resolveActiveBranchId(authSession, config.branchId);
    if (!branchId) {
      setShellCurrentShift(null);
      setShellDashboardSummary(null);
      setShellLoadStatus('failed');
      setShellLoadError(t('op.dashboard.noBranch'));
      return undefined;
    }

    const canLoadShift = hasPermission(authSession, permissionNames.viewShift);
    const canLoadSummary = hasPermission(authSession, permissionNames.viewReports);
    if (!canLoadShift && !canLoadSummary) {
      setShellCurrentShift(null);
      setShellDashboardSummary(null);
      setShellLoadStatus('failed');
      setShellLoadError(null);
      return undefined;
    }

    let disposed = false;
    const clients = createAuthenticatedOperatorClients(config, authSession);

    const loadShellStatus = async () => {
      setShellLoadStatus((current) => current === 'backend' ? current : 'loading');
      setShellLoadError(null);

      let nextShift: ShiftDto | null = null;
      let nextSummary: OperatorDashboardSummaryDto | null = null;
      const errors: string[] = [];
      const today = toDateInputValue(new Date());

      await Promise.all([
        canLoadShift
          ? clients.shifts.getCurrentShift(branchId)
            .then((shift) => {
              nextShift = shift;
            })
            .catch((error) => {
              errors.push(projectOperatorError(error).detail);
            })
          : Promise.resolve(),
        canLoadSummary
          ? clients.dashboard.getSummary(branchId, dashboardRangeQuery(today, today))
            .then((summary) => {
              nextSummary = summary;
            })
            .catch((error) => {
              errors.push(projectOperatorError(error).detail);
            })
          : Promise.resolve()
      ]);

      if (disposed) {
        return;
      }

      setShellCurrentShift(nextShift);
      setShellDashboardSummary(nextSummary);
      setShellLoadStatus(errors.length > 0 && nextShift === null && nextSummary === null ? 'failed' : 'backend');
      setShellLoadError(errors[0] ?? null);
    };

    void loadShellStatus();
    const intervalId = window.setInterval(() => void loadShellStatus(), shellOperationalRefreshMs);

    return () => {
      disposed = true;
      window.clearInterval(intervalId);
    };
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

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

  useEffect(() => {
    let disposed = false;

    loadOperatorSession()
      .then(async (session) => {
        if (session === null) {
          return null;
        }

        try {
          return await refreshOperatorSession();
        } catch (error) {
          if (isUnauthorizedAuthError(error)) {
            await clearStoredOperatorSession();
            return null;
          }

          throw error;
        }
      })
      .then((session) => {
        if (disposed) {
          return;
        }

        setAuthSession(session);
        setAuthStatus(session ? 'signed-in' : 'signed-out');
        setAuthError(null);
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setAuthSession(null);
        setAuthStatus('signed-out');
        setAuthError(projectAuthHostError(error, config, t));
      });

    return () => {
      disposed = true;
    };
  }, []);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      return;
    }

    if (!canOpenWorkspace(authSession, workspace)) {
      setWorkspace(firstAllowedWorkspace(authSession));
    }
  }, [authStatus, authSession, workspace]);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      setFloorMap(createFixtureFloorMapState());
      setSelectedSeatId(seats[0].id);
      setRealtimeState('disconnected');
      setRealtimeError(null);
      return undefined;
    }

    const branchId = resolveActiveBranchId(authSession, config.branchId);
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
        setSelectedSeatId(nextState.seats[0]?.id ?? seats[0].id);
        void drainQueuedActions(nextState.seats);
      } catch (error) {
        if (isUnauthorizedPlatformError(error)) {
          try {
            const nextSession = await refreshOperatorSession();
            const nextBranchId = resolveActiveBranchId(nextSession, config.branchId);
            if (!nextBranchId) {
              throw new Error(t('op.dashboard.noBranch'));
            }

            const nextState = await loadBackendFloorMapState(config, nextSession, nextBranchId, t);
            if (disposed) {
              return;
            }

            setAuthSession(nextSession);
            setFloorMap(nextState);
            setSelectedSeatId(nextState.seats[0]?.id ?? seats[0].id);
            return;
          } catch (refreshError) {
            await clearStoredOperatorSession();
            if (disposed) {
              return;
            }

            setAuthSession(null);
            setAuthStatus('signed-out');
            setAuthError(projectAuthHostError(refreshError, config, t));
            setFloorMap(createFixtureFloorMapState());
            setSelectedSeatId(seats[0].id);
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
          setSelectedSeatId((current) => current ?? degraded.seats[0]?.id ?? seats[0].id);
          return;
        }

        setFloorMap((current) => ({
          ...current,
          branchId,
          loadStatus: 'failed',
          error: projectOperatorError(error).detail
        }));
      }
    })();

    return () => {
      disposed = true;
    };
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
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
              setRealtimeError(projectOperatorError(error).detail);
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
        setRealtimeError(projectOperatorError(error).detail);
      });

    return () => {
      disposed = true;
      if (reloadTimeoutId !== null) {
        window.clearTimeout(reloadTimeoutId);
      }
      void realtimeClient.stop();
    };
  }, [authStatus, authSession, config.branchId, config.platformBaseUrl]);

  const handleSignIn = async (request: OperatorSignInRequest) => {
    const session = await signInOperator(request);
    setAuthSession(session);
    setAuthStatus('signed-in');
    setAuthError(null);
    setWorkspaceFeedback(null);
  };

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

  const handleSignOut = async () => {
    try {
      await signOutOperator();
      setAuthError(null);
      setWorkspaceFeedback(null);
    } catch (error) {
      setAuthError(projectAuthHostError(error, config, t));
    } finally {
      setAuthSession(null);
      setAuthStatus('signed-out');
    }
  };

  const handleWorkspaceNavigation = async (
    workspaceId: WorkspaceId,
    label: string,
    isAllowed: boolean
  ) => {
    if (isAllowed) {
      setWorkspaceFeedback(null);
      setWorkspace(workspaceId);
      return;
    }

    try {
      const refreshedSession = await refreshOperatorSession();
      setAuthSession(refreshedSession);
      if (canOpenWorkspace(refreshedSession, workspaceId)) {
        setWorkspaceFeedback(null);
        setWorkspace(workspaceId);
        return;
      }

      setWorkspaceFeedback(t('op.shell.err.noPermNav', { label }));
    } catch (error) {
      setWorkspaceFeedback(t('op.shell.err.navRefreshFailed', { label, detail: projectOperatorError(error).detail }));
    }
  };

  const handleSeatAction = async (request: SeatActionRequest): Promise<SeatActionResult> => {
    const session = authSession;
    if (session === null) {
      throw new Error(t('op.shell.err.notSignedIn'));
    }

    const branchId = resolveActiveBranchId(session, config.branchId);
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
        durationMinutes: isOpenTab ? null : defaultSessionDurationMinutes,
        tariffRuleVersionId: billing.tariffRuleVersionId,
        idempotencyKey: createIdempotencyKey('session-start'),
        playerAccountId: billing.playerAccountId ?? null,
        billingMode: billing.mode === 'guest' ? '' : billing.mode,
        tariffVersionId: billing.tariffVersionId ?? null,
        playerPackageId: billing.playerPackageId ?? null
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

  if (blockedResolution !== null) {
    return (
      <BlockedTenantScreen
        resolution={blockedResolution}
        onChangeConnection={handleChangeConnection}
      />
    );
  }

  if (authStatus === 'signed-out' && needsConnectionResolution && !isConnectionLoading) {
    return (
      <ConnectionResolutionScreen
        resolver={connectionResolver}
        onResolved={handleConnectionResolved}
      />
    );
  }

  if (authStatus !== 'signed-in' || authSession === null) {
    if (authView === 'forgot') {
      return <ForgotPassword onBackToSignIn={() => setAuthView('signIn')} />;
    }
    return (
      <SignInScreen
        config={config}
        authStatus={authStatus}
        hostError={authError}
        onSignIn={handleSignIn}
        onForgotPassword={() => setAuthView('forgot')}
      />
    );
  }

  return (
    <div className="operator-shell">
      <WindowResizeHandles />
      <header className="top-command" onMouseDown={handleWindowDragStart} onDoubleClick={handleWindowTitleDoubleClick}>
        <div className="brand-block">
          <img className="brand-logo" src="/afk4-logo-horizontal.svg" alt="AFK4.NET" />
          <span>{t('op.auth.operator')}</span>
        </div>
        <label className="command-search">
          <Search size={16} />
          <input placeholder={t('op.shell.searchPlaceholder')} aria-label={t('op.shell.searchLabel')} />
        </label>
        <div className="top-status">
          <span>{shellShiftText}</span>
          <button type="button" className="top-account" aria-label={t('op.shell.myAccount')} onClick={() => setAccountPanelOpen(true)}>
            {operatorDisplayNameLabel(authSession.displayName, t)} · {shellModeLabel(config.shellMode, t)}
          </button>
        </div>
        <button type="button" className="sign-out-button" onClick={handleSignOut}>{t('shell.signOut')}</button>
        <WindowControls />
      </header>

      {accountPanelOpen && backendContext !== null && (
        <AccountPanel
          backend={backendContext}
          displayName={operatorDisplayNameLabel(authSession.displayName, t)}
          onClose={() => setAccountPanelOpen(false)}
        />
      )}

      <nav className="workspace-rail" aria-label={t('op.shell.workspaces')}>
        {navItems.map((item, index) => {
          const Icon = item.icon;
          const id = workspaceIds[index];
          const isAllowed = canOpenWorkspace(authSession, id);
          const label = t(item.labelKey);
          return (
            <button
              key={id}
              type="button"
              className={[workspace === id ? 'active' : '', !isAllowed ? 'locked' : ''].filter(Boolean).join(' ')}
              aria-disabled={!isAllowed}
              title={label}
              onClick={() => void handleWorkspaceNavigation(id, label, isAllowed)}
            >
              <Icon size={22} />
              <span>{label}</span>
            </button>
          );
        })}
      </nav>

      {workspace === 'map' && (
        <MapWorkspace
          floorMap={displayedFloorMap}
          canUsePcControl={canUsePcControl}
          selectedSeatId={selectedSeat?.id ?? ''}
          offlineActionAudit={offlineActionAudit}
          onSelectSeat={setSelectedSeatId}
          onPcControlAction={handlePcControlAction}
        />
      )}
      {workspace === 'dashboard' && (
        <DashboardWorkspace
          currencyCode={config.currencyCode}
          backend={backendContext}
          onNavigate={setWorkspace}
          onOpenSeat={(seatId) => {
            setSelectedSeatId(seatId);
            setWorkspace('map');
          }}
        />
      )}
      {workspace === 'booking' && (
        <BackendBookingWorkspace
          floorMap={displayedFloorMap}
          backend={backendContext}
          onOpenSeat={(seatId) => {
            setSelectedSeatId(seatId);
            setWorkspace('map');
          }}
        />
      )}
      {workspace === 'pos' && <BackendPosWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'players' && <BackendPlayersWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'payments' && <BackendPaymentsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'payment_cards' && backendContext !== null && (
        <PaymentGatewaysWorkspace backend={backendContext} />
      )}
      {workspace === 'logs' && <BackendLogsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'settings' && <BackendSettingsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'review' && <ReviewWorkspace currencyCode={config.currencyCode} backend={backendContext} />}

      {workspace === 'map' && selectedSeat !== null && (
        <MapSidePanel
          seat={selectedSeat}
          seats={displayedFloorMap.seats}
          currencyCode={config.currencyCode}
          backend={backendContext}
          actionsEnabled={floorMap.source === 'backend' && floorMap.loadStatus === 'ready'}
          onSeatAction={handleSeatAction}
        />
      )}
      {workspace !== 'map' && workspace !== 'dashboard' && workspace !== 'booking' && workspace !== 'pos' && workspace !== 'players' && workspace !== 'payments' && workspace !== 'logs' && workspace !== 'settings' && workspace !== 'review'
        && <SummarySidePanel workspace={workspace} currencyCode={config.currencyCode} />}

      <footer className="signals-strip">
        <span><Wifi size={14} />{realtimeLabel(realtimeState, realtimeError, t)} · {dataSourceLabel(floorMap.source, t)}</span>
        <span><MonitorCheck size={14} />{t('op.shell.signals', { offline: countByTone(displayedFloorMap.seats, 'offline'), problems: countProblems(displayedFloorMap.seats) })}</span>
        <span><CircleDollarSign size={14} />{shellPosText}</span>
        {workspaceFeedback && (
          <span className="rail-feedback"><LockKeyhole size={14} />{workspaceFeedback}</span>
        )}
      </footer>
    </div>
  );
}
