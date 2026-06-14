import {
  CircleDollarSign,
  LockKeyhole,
  MonitorCheck,
  Search,
  Wifi
} from 'lucide-react';
import { useEffect, useState, type CSSProperties } from 'react';
import { I18nProvider, useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { loadOperatorSession, refreshOperatorSession, signInOperator, signOutOperator, type OperatorAuthSession, type OperatorSignInRequest } from './authClient';
import { ConnectionResolutionScreen } from './ConnectionResolutionScreen';
import { getOperatorConfig } from './operatorConfig';
import { navSections, type NavSection } from './operatorData';
import { PaymentGatewaysWorkspace } from './PaymentGatewaysWorkspace';
import { AccountPanel } from './AccountPanel';
import { BackendBookingWorkspace } from './BackendBookingWorkspace';
import { DashboardWorkspace } from './DashboardWorkspace';
import { MapWorkspace } from './MapWorkspace';
import { MapSidePanel } from './MapSidePanel';
import { SummarySidePanel } from './SummarySidePanel';
import { BackendPosWorkspace } from './BackendPosWorkspace';
import { ShopOrdersWorkspace } from './ShopOrdersWorkspace';
import { LoyaltySettingsWorkspace } from './LoyaltySettingsWorkspace';
import { NewsWorkspace } from './NewsWorkspace';
import { ShiftsWorkspace } from './ShiftsWorkspace';
import { BackendPlayersWorkspace } from './BackendPlayersWorkspace';
import { BackendPaymentsWorkspace } from './BackendPaymentsWorkspace';
import { ReviewWorkspace } from './ReviewWorkspace';
import { BackendLogsWorkspace } from './BackendLogsWorkspace';
import { BackendSettingsWorkspace } from './BackendSettingsWorkspace';
import { ForgotPassword } from './ForgotPassword';
import { WindowControls, WindowResizeHandles, handleWindowDragStart, handleWindowTitleDoubleClick } from './WindowChrome';
import { TitlebarControls } from './TitlebarControls';
import { BrandLogo } from './BrandLogo';
import { SignInScreen } from './SignInScreen';
import { BlockedTenantScreen } from './BlockedTenantScreen';
import { WorkspaceErrorBoundary } from './WorkspaceErrorBoundary';
import { useShellData } from './useShellData';
import { useOperatorRealtime } from './useOperatorRealtime';
import { useOperatorConnection } from './useOperatorConnection';
import { useFloorMap } from './useFloorMap';
import type {
  WorkspaceId,
  AuthStatus,
  OperatorBackendContext
} from './operatorTypes';
import {
  permissionNames,
  hasPermission,
  canOpenWorkspace,
  firstAllowedWorkspace
} from './operatorPermissions';
import {
  countByTone,
  countProblems,
  operatorDisplayNameLabel,
  dataSourceLabel,
  shellShiftLabel,
  shellPosLabel,
  shellModeLabel,
  projectAuthHostError,
  realtimeLabel,
  resolveActiveBranchId,
  isUnauthorizedAuthError,
  clearStoredOperatorSession
} from './operatorHelpers';


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
  const {
    config,
    connectionResolver,
    needsConnectionResolution,
    isConnectionLoading,
    blockedResolution,
    handleConnectionResolved,
    handleChangeConnection
  } = useOperatorConnection(baseConfig);
  const [workspace, setWorkspace] = useState<WorkspaceId>('map');
  const [authStatus, setAuthStatus] = useState<AuthStatus>('checking');
  const [authSession, setAuthSession] = useState<OperatorAuthSession | null>(null);
  const [authError, setAuthError] = useState<string | null>(null);
  const [authView, setAuthView] = useState<'signIn' | 'forgot'>('signIn');
  const [workspaceFeedback, setWorkspaceFeedback] = useState<string | null>(null);
  const [accountPanelOpen, setAccountPanelOpen] = useState(false);
  // Bumped by the realtime hook to make the shell KPIs reconcile event-driven instead of polled.
  const [shellReconcileSignal, setShellReconcileSignal] = useState(0);
  const activeBranchId = authSession === null ? null : resolveActiveBranchId(authSession, config.branchId);
  const backendContext: OperatorBackendContext | null = authSession !== null && activeBranchId !== null
    ? { config, session: authSession, branchId: activeBranchId }
    : null;
  const {
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
  } = useFloorMap({
    config,
    t,
    authStatus,
    authSession,
    backendContext,
    setAuthSession,
    setAuthStatus,
    setAuthError
  });
  const { realtimeState, realtimeError } = useOperatorRealtime({
    authStatus,
    authSession,
    config,
    t,
    floorMapRef,
    setFloorMap,
    setSelectedSeatId,
    onShellReconcile: () => setShellReconcileSignal((signal) => signal + 1)
  });
  const { shellCurrentShift, shellDashboardSummary, shellLoadStatus, shellLoadError } = useShellData(
    authStatus,
    authSession,
    config,
    t,
    shellReconcileSignal
  );
  const canUsePcControl = (hasPermission(authSession, permissionNames.viewDiagnostics)
    && hasPermission(authSession, permissionNames.viewDeviceDetail))
    || hasPermission(authSession, permissionNames.dispatchDeviceCommand);
  const shellShiftText = shellShiftLabel(shellCurrentShift, shellDashboardSummary, shellLoadStatus, shellLoadError, t);
  const shellPosText = shellPosLabel(shellDashboardSummary, shellLoadStatus, t);

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

  const handleSignIn = async (request: OperatorSignInRequest) => {
    const session = await signInOperator(request);
    setAuthSession(session);
    setAuthStatus('signed-in');
    setAuthError(null);
    setWorkspaceFeedback(null);
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
      setWorkspaceFeedback(t('op.shell.err.navRefreshFailed', { label, detail: projectOperatorError(error, t).detail }));
    }
  };

  // Клик по кнопке раздела в рельсе. Если мы уже внутри раздела — ничего не делаем (вкладки сами
  // переключают экраны). Иначе открываем первую доступную вкладку; если прав нет ни на одну —
  // прогоняем через handleWorkspaceNavigation, чтобы сработал refresh-прав + понятный feedback.
  const handleSectionNavigation = (section: NavSection) => {
    if (section.items.some((item) => item.id === workspace)) {
      return;
    }
    const allowedItem = section.items.find((item) => canOpenWorkspace(authSession, item.id));
    const target = allowedItem ?? section.items[0];
    void handleWorkspaceNavigation(target.id, t(target.labelKey), allowedItem != null);
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

  const activeSection = navSections.find((section) => section.items.some((item) => item.id === workspace)) ?? navSections[0];
  const activeVisibleItems = activeSection.items.filter((item) => canOpenWorkspace(authSession, item.id));
  const showWorkspaceTabs = activeVisibleItems.length > 1;

  return (
    <div className="operator-shell" style={{ '--shell-tabstrip': showWorkspaceTabs ? '41px' : '0px' } as CSSProperties}>
      <WindowResizeHandles />
      <header className="top-command" onMouseDown={handleWindowDragStart} onDoubleClick={handleWindowTitleDoubleClick}>
        <div className="brand-block">
          <BrandLogo className="brand-logo" />
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
        <TitlebarControls />
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
        {navSections.map((section) => {
          const Icon = section.icon;
          const isAllowed = section.items.some((item) => canOpenWorkspace(authSession, item.id));
          const label = t(section.labelKey);
          return (
            <button
              key={section.key}
              type="button"
              className={[section.key === activeSection.key ? 'active' : '', !isAllowed ? 'locked' : ''].filter(Boolean).join(' ')}
              aria-disabled={!isAllowed}
              title={label}
              onClick={() => handleSectionNavigation(section)}
            >
              <Icon size={20} />
              <span>{label}</span>
            </button>
          );
        })}
      </nav>

      <div className="workspace-content">
        {showWorkspaceTabs && (
          <div className="workspace-tabs" role="tablist" aria-label={t(activeSection.labelKey)}>
            {activeVisibleItems.map((item) => {
              const tabLabel = t(item.labelKey);
              return (
                <button
                  key={item.id}
                  type="button"
                  role="tab"
                  aria-selected={workspace === item.id}
                  className={workspace === item.id ? 'active' : undefined}
                  title={tabLabel}
                  onClick={() => setWorkspace(item.id)}
                >
                  {tabLabel}
                </button>
              );
            })}
          </div>
        )}

      <WorkspaceErrorBoundary key={workspace} message={t('op.shell.workspaceError')}>
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
      {workspace === 'shop_orders' && <ShopOrdersWorkspace backend={backendContext} />}
      {workspace === 'players' && <BackendPlayersWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'payments' && <BackendPaymentsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'payment_cards' && backendContext !== null && (
        <PaymentGatewaysWorkspace backend={backendContext} />
      )}
      {workspace === 'logs' && <BackendLogsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'settings' && <BackendSettingsWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'review' && <ReviewWorkspace currencyCode={config.currencyCode} backend={backendContext} />}
      {workspace === 'loyalty' && backendContext !== null && (
        <LoyaltySettingsWorkspace backend={backendContext} />
      )}
      {workspace === 'news' && backendContext !== null && (
        <NewsWorkspace backend={backendContext} />
      )}
      {workspace === 'shifts' && backendContext !== null && (
        <ShiftsWorkspace backend={backendContext} branchId={backendContext.branchId} />
      )}
      </WorkspaceErrorBoundary>
      </div>

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
      {workspace !== 'map' && workspace !== 'dashboard' && workspace !== 'booking' && workspace !== 'pos' && workspace !== 'shop_orders' && workspace !== 'players' && workspace !== 'payments' && workspace !== 'logs' && workspace !== 'settings' && workspace !== 'review' && workspace !== 'loyalty' && workspace !== 'news' && workspace !== 'shifts'
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
