import { useCallback, useEffect, useMemo, useState, type CSSProperties } from 'react';
import { I18nProvider, useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { refreshOperatorSession } from './authClient';
import { ConnectionResolutionScreen } from './ConnectionResolutionScreen';
import { getOperatorConfig } from './operatorConfig';
import { navSections, type NavSection } from './operatorData';
import { useActiveBranch } from './branches/useActiveBranch';
import { BranchSwitcher } from './branches/BranchSwitcher';
import { useBranchDirectory } from './branches/useBranchDirectory';
import { AccountPanel } from './AccountPanel';
import { MapSidePanel } from './MapSidePanel';
import { ContextPanel } from './ContextPanel';
import { CommandPalette } from './CommandPalette';
import { ForgotPassword } from './ForgotPassword';
import { WindowResizeHandles } from './WindowChrome';
import { SignInScreen } from './SignInScreen';
import { BlockedOrganizationScreen } from './BlockedOrganizationScreen';
import { isSupportSessionExpired, readSupportSession } from './support/supportSession';
import { SupportHomeScreen } from './support/SupportHomeScreen';
import { PostAuthShiftGate } from './PostAuthShiftGate';
import { ShellHeader } from './ShellHeader';
import { WorkspaceRail } from './WorkspaceRail';
import { WorkspaceRouter } from './WorkspaceRouter';
import { ShellStatusBar } from './ShellStatusBar';
import { useOperatorAuth } from './useOperatorAuth';
import { useShellData } from './useShellData';
import { useOperatorRealtime } from './useOperatorRealtime';
import { usePlayersPreload } from './usePlayersPreload';
import { useOperatorConnection } from './useOperatorConnection';
import { usePostAuthShiftGate } from './usePostAuthShiftGate';
import { useFloorMap } from './useFloorMap';
import { ToastProvider } from './operatorToast';
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
  shellShiftBadge,
  resolveActiveBranchId,
  createAuthenticatedOperatorClients
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
    chooseClub,
    handleSignIn,
    handleChooseClub,
    cancelChooseClub,
    handleSignOut
  } = useOperatorAuth(config, {
    onSignedIn: () => setWorkspaceFeedback(null),
    onSignedOut: () => setWorkspaceFeedback(null)
  });
  const [accountPanelOpen, setAccountPanelOpen] = useState(false);
  const [paletteOpen, setPaletteOpen] = useState(false);
  // Токен «открыть запуск сессии» — растёт по клику на «+» свободной плитки; боковая панель
  // реагирует на изменение и открывает старт-диалог для выбранного места.
  const [startSeatToken, setStartSeatToken] = useState(0);
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
  // Реактивный выбор филиала (мульти-филиальность): свитчер в шапке меняет `chosenBranchId`
  // (персистится в localStorage), который перебивает и session.activeBranchId, и config.branchId.
  const { activeBranchId: chosenBranchId, select: selectBranch } = useActiveBranch(authSession?.branchIds ?? []);
  const activeBranchId = authSession === null
    ? null
    : resolveActiveBranchId(authSession, config.branchId, chosenBranchId ?? undefined);
  const backendContext: OperatorBackendContext | null = useMemo(
    () => authSession !== null && activeBranchId !== null
      ? { config, session: authSession, branchId: activeBranchId }
      : null,
    [config, authSession, activeBranchId]
  );
  // Имена филиалов для свитчера — подгружаются по branchIds сессии, независимо от того, сколько
  // их (сам свитчер решает, показываться ли, по количеству). Фолбэк на branchId внутри хука —
  // сбой одного профиля не валит весь список.
  const getBranchProfileName = useCallback(async (branchId: string) => {
    if (authSession === null) {
      throw new Error('not signed in');
    }
    const profile = await createAuthenticatedOperatorClients(config, authSession).settings.getBranchProfile(branchId);
    return { name: typeof profile.name === 'string' ? profile.name : '' };
  }, [config, authSession]);
  const branchDirectory = useBranchDirectory(getBranchProfileName, authSession?.branchIds ?? []);
  const shiftGate = usePostAuthShiftGate({
    authStatus,
    backend: backendContext,
    t
  });
  const workspaceAllowed = shiftGate.status === 'ready'
    || shiftGate.status === 'not-required';
  const operationalAuthStatus = workspaceAllowed ? authStatus : 'checking';
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
    authStatus: operationalAuthStatus,
    authSession,
    activeBranchId,
    backendContext,
    setAuthSession,
    setAuthStatus,
    setAuthError
  });
  const { realtimeState, realtimeError } = useOperatorRealtime({
    authStatus: operationalAuthStatus,
    authSession,
    config,
    activeBranchId,
    t,
    floorMapRef,
    setFloorMap,
    setSelectedSeatId,
    onShellReconcile: () => setShellReconcileSignal((signal) => signal + 1)
  });
  const { shellCurrentShift, shellDashboardSummary, shellLoadStatus, shellLoadError } = useShellData(
    operationalAuthStatus,
    authSession,
    config,
    t,
    shellReconcileSignal,
    activeBranchId
  );
  // Прогрев кэша клиентов при входе → первый заход в «Клиенты» мгновенный (как заранее загруженный зал).
  usePlayersPreload(operationalAuthStatus, authSession, config, t, activeBranchId);
  const canUsePcControl = (hasPermission(authSession, permissionNames.viewDiagnostics)
    && hasPermission(authSession, permissionNames.viewDeviceDetail))
    || hasPermission(authSession, permissionNames.dispatchDeviceCommand);
  const shellShift = shellShiftBadge(shellCurrentShift, shellDashboardSummary, shellLoadStatus, shellLoadError, t);

  useEffect(() => {
    if (authStatus !== 'signed-in' || authSession === null) {
      return;
    }

    if (!canOpenWorkspace(authSession, workspace)) {
      setWorkspace(firstAllowedWorkspace(authSession));
    }
    // activeBranchId: после смены филиала доступные разделы могут отличаться — увести с
    // недоступного воркспейса, а не оставить на разделе, к которому больше нет доступа.
  }, [authStatus, authSession, workspace, activeBranchId]);

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

  const handleOpenSeat = (seatId: string) => {
    setSelectedSeatId(seatId);
    setWorkspace('map');
  };

  // Клик по «+» свободной плитки: выбрать место, остаться на карте и открыть запуск сессии.
  const handleStartSeat = (seatId: string) => {
    setSelectedSeatId(seatId);
    setWorkspace('map');
    setStartSeatToken((value) => value + 1);
  };

  // Support mode (platform staff impersonating this organization via a time-boxed grant, see
  // support/supportSession.ts) is a self-contained app state, independent of the staff sign-in
  // flow above — checked first and short-circuits everything else, staff auth included. Scoping
  // which sections/branches/actions it can reach is deliberately not done here (follow-up task);
  // this only needs to land the person somewhere real instead of stuck on the sign-in form.
  const supportSession = readSupportSession();
  if (supportSession !== null && !isSupportSessionExpired(supportSession)) {
    return <SupportHomeScreen session={supportSession} />;
  }

  if (blockedResolution !== null) {
    return (
      <BlockedOrganizationScreen
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
        chooseClub={chooseClub}
        onSignIn={handleSignIn}
        onChooseClub={handleChooseClub}
        onCancelChooseClub={cancelChooseClub}
        onForgotPassword={() => setAuthView('forgot')}
      />
    );
  }

  if (!workspaceAllowed) {
    return (
      <PostAuthShiftGate
        controller={shiftGate}
        organizationId={authSession.organizationId}
        currencyCode={config.currencyCode}
        onSignOut={handleSignOut}
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
        onOpenPalette={() => setPaletteOpen(true)}
        branchSwitcher={authSession.branchIds.length > 1 ? (
          <BranchSwitcher
            branches={authSession.branchIds.map((branchId) => ({
              branchId,
              name: branchDirectory[branchId]?.name ?? ''
            }))}
            activeBranchId={activeBranchId}
            onSelect={selectBranch}
          />
        ) : undefined}
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
        shift={shellShift}
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
          onStartSeat={handleStartSeat}
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
            startRequestToken={startSeatToken}
            onSeatAction={handleSeatAction}
            onPcControlAction={handlePcControlAction}
          />
        </ContextPanel>
      )}

      <ShellStatusBar
        operatorName={authSession.displayName}
        roleNames={authSession.roleNames ?? []}
        clubName={displayedFloorMap.source === 'backend' ? displayedFloorMap.branchName : ''}
        realtimeState={realtimeState}
        realtimeError={realtimeError}
        dataSource={floorMap.source}
        appVersion={config.appVersion ?? ''}
        workspaceFeedback={workspaceFeedback}
      />
    </div>
  );
}
