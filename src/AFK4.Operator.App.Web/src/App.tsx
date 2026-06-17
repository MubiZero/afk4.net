import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import { I18nProvider, useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { refreshOperatorSession } from './authClient';
import { ConnectionResolutionScreen } from './ConnectionResolutionScreen';
import { getOperatorConfig } from './operatorConfig';
import { navSections, type NavSection } from './operatorData';
import { AccountPanel } from './AccountPanel';
import { MapSidePanel } from './MapSidePanel';
import { ContextPanel } from './ContextPanel';
import { CommandPalette } from './CommandPalette';
import { ForgotPassword } from './ForgotPassword';
import { WindowResizeHandles } from './WindowChrome';
import { SignInScreen } from './SignInScreen';
import { BlockedTenantScreen } from './BlockedTenantScreen';
import { ShellHeader } from './ShellHeader';
import { WorkspaceRail } from './WorkspaceRail';
import { WorkspaceRouter } from './WorkspaceRouter';
import { ShellStatusBar } from './ShellStatusBar';
import { useOperatorAuth } from './useOperatorAuth';
import { useShellData } from './useShellData';
import { useOperatorRealtime } from './useOperatorRealtime';
import { useOperatorConnection } from './useOperatorConnection';
import { useFloorMap } from './useFloorMap';
import { ToastProvider, useToast } from './operatorToast';
import { createCommandRegistry, type QuickAction } from './operatorCommands';
import { useHotkeys } from './useHotkeys';
import type {
  WorkspaceId,
  MapFilterId,
  OperatorBackendContext
} from './operatorTypes';
import {
  permissionNames,
  hasPermission,
  canOpenWorkspace,
  firstAllowedWorkspace
} from './operatorPermissions';
import {
  operatorDisplayNameLabel,
  shellShiftLabel,
  shellPosLabel,
  resolveActiveBranchId
} from './operatorHelpers';


export function App() {
  return (
    <I18nProvider>
      <ToastProvider>
        <AppInner />
      </ToastProvider>
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
  const [mapFilter, setMapFilter] = useState<MapFilterId>('all');
  const [workspaceFeedback, setWorkspaceFeedback] = useState<string | null>(null);
  const {
    authStatus,
    authSession,
    authError,
    authView,
    setAuthView,
    setAuthSession,
    setAuthStatus,
    setAuthError,
    handleSignIn,
    handleSignOut
  } = useOperatorAuth(config, {
    onSignedIn: () => setWorkspaceFeedback(null),
    onSignedOut: () => setWorkspaceFeedback(null)
  });
  const [accountPanelOpen, setAccountPanelOpen] = useState(false);
  const [paletteOpen, setPaletteOpen] = useState(false);
  const toast = useToast();
  const commandRegistry = useMemo(() => createCommandRegistry(), []);
  // ⌘K / Ctrl+K открывают палитру даже из поля ввода (allowInInputs). Биндинги мемоизированы —
  // useHotkeys пересоздаёт слушатель только при смене массива.
  const paletteHotkeys = useMemo(
    () => [
      { key: 'k', ctrl: true, allowInInputs: true, onTrigger: () => setPaletteOpen(true) },
      { key: 'k', meta: true, allowInInputs: true, onTrigger: () => setPaletteOpen(true) }
    ],
    []
  );
  useHotkeys(paletteHotkeys);
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
    if (authStatus !== 'signed-in' || authSession === null) {
      return;
    }

    if (!canOpenWorkspace(authSession, workspace)) {
      setWorkspace(firstAllowedWorkspace(authSession));
    }
  }, [authStatus, authSession, workspace]);

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

  const handleQuickAction = (action: QuickAction) => {
    if (!commandRegistry.dispatch(action.id)) {
      toast.info(t('op.command.deferred', { stage: t(action.stageKey) }));
    }
  };

  const handleOpenSeat = (seatId: string) => {
    setSelectedSeatId(seatId);
    setWorkspace('map');
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
  const hasContextContent = workspace === 'map' && selectedSeat !== null;
  const contextCol = hasContextContent ? 'minmax(260px, 292px)' : '0px';
  const actionsEnabled = floorMap.source === 'backend' && floorMap.loadStatus === 'ready';
  const operatorDisplayName = operatorDisplayNameLabel(authSession.displayName, t);

  return (
    <div
      className="operator-shell"
      style={{
        '--shell-tabstrip': showWorkspaceTabs ? '41px' : '0px',
        '--shell-context-col': contextCol
      } as CSSProperties}
    >
      <WindowResizeHandles />
      <ShellHeader
        session={authSession}
        shiftText={shellShiftText}
        onOpenPalette={() => setPaletteOpen(true)}
        onQuickAction={handleQuickAction}
      />

      {accountPanelOpen && backendContext !== null && (
        <AccountPanel
          backend={backendContext}
          displayName={operatorDisplayName}
          onClose={() => setAccountPanelOpen(false)}
        />
      )}

      {paletteOpen && (
        <CommandPalette
          session={authSession}
          onNavigate={(id) => {
            setWorkspace(id);
            setPaletteOpen(false);
          }}
          onClose={() => setPaletteOpen(false)}
        />
      )}

      <WorkspaceRail
        session={authSession}
        activeSectionKey={activeSection.key}
        displayName={operatorDisplayName}
        onNavigateSection={handleSectionNavigation}
        onOpenAccount={() => setAccountPanelOpen(true)}
        onSignOut={handleSignOut}
      />

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

        <WorkspaceRouter
          workspace={workspace}
          session={authSession}
          backend={backendContext}
          currencyCode={config.currencyCode}
          displayedFloorMap={displayedFloorMap}
          actionsEnabled={actionsEnabled}
          selectedSeatId={selectedSeat?.id ?? ''}
          mapFilter={mapFilter}
          offlineActionAudit={offlineActionAudit}
          onSelectSeat={setSelectedSeatId}
          onFilterChange={setMapFilter}
          onPcControlAction={handlePcControlAction}
          onSeatAction={handleSeatAction}
          onNavigate={setWorkspace}
          onOpenSeat={handleOpenSeat}
        />
      </div>

      {workspace === 'map' && selectedSeat !== null && (
        <ContextPanel>
          <MapSidePanel
            seat={selectedSeat}
            seats={displayedFloorMap.seats}
            currencyCode={config.currencyCode}
            backend={backendContext}
            actionsEnabled={actionsEnabled}
            canUsePcControl={canUsePcControl}
            onSeatAction={handleSeatAction}
            onPcControlAction={handlePcControlAction}
          />
        </ContextPanel>
      )}

      <ShellStatusBar
        realtimeState={realtimeState}
        realtimeError={realtimeError}
        dataSource={floorMap.source}
        seats={displayedFloorMap.seats}
        workspaceFeedback={workspaceFeedback}
        posText={shellPosText}
        onSelectAlertSource={(filterId) => {
          setMapFilter(filterId);
          setWorkspace('map');
        }}
      />
    </div>
  );
}
