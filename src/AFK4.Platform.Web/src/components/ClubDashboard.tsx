import { useEffect, useMemo, useState, type FormEvent } from 'react';
import type { ClubApiClient } from '../api/clubApi';
import { PlatformApiError } from '../api/platformApi';
import type {
  BranchProfile,
  BranchSettings,
  DeviceInventoryItem,
  FloorMap,
  FloorMapBulkSeatRequest,
  FloorMapBulkZoneRequest,
  OperatorDashboardSummary,
  OwnerCodeIssued,
  OwnerCodeSummary,
  SeatStatus,
  StaffUser
} from '../api/types';
import type { ClubRoute } from '../App';
import type { StaffSession } from '../auth/staffTokenStore';
import { EmptyState, ErrorBanner, Field, Loading, StatusBadge, formatDate } from './ui';

const Permissions = {
  manageOwnerCode: 'identity.owner_code.manage',
  viewFloorMap: 'floor_map.view',
  manageLayout: 'layout.manage',
  viewDeviceDetail: 'devices.detail.view',
  assignDeviceSeat: 'devices.seat_assignment.assign',
  revokeDeviceCredential: 'devices.credentials.revoke',
  manageBranchStaff: 'identity.branch_staff.manage',
  manageBranchSettings: 'branches.settings.manage'
} as const;

const StaffRoles = [
  'branch_manager',
  'cashier_operator',
  'technician',
  'shift_supervisor',
  'accountant_auditor',
  'owner'
] as const;

export interface ClubDashboardProps {
  client: ClubApiClient;
  route: ClubRoute;
  session: StaffSession;
  onNavigate: (route: ClubRoute, path: string) => void;
  onSignOut: () => void;
}

interface BranchSummary {
  profile: BranchProfile;
  dashboard: OperatorDashboardSummary | null;
  devices: DeviceInventoryItem[];
  pendingDevices: DeviceInventoryItem[];
  error: string | null;
}

export function ClubDashboard({ client, route, session, onNavigate, onSignOut }: ClubDashboardProps) {
  const [branches, setBranches] = useState<BranchSummary[]>([]);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      if (session.branchIds.length === 0) {
        setBranches([]);
        return;
      }

      try {
        const loaded = await Promise.all(session.branchIds.map(branchId => loadBranchSummary(client, branchId)));
        if (!cancelled) {
          setBranches(loaded);
        }
      } catch {
        if (!cancelled) {
          setBranches([]);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [client, session.branchIds]);

  const selectedBranch = useMemo(() => {
    if (!('branchId' in route) || route.branchId === undefined) {
      return branches[0] ?? null;
    }
    return branches.find(branch => sameId(branch.profile.branchId, route.branchId)) ?? null;
  }, [branches, route]);

  const installPath = '/club/install';
  const firstBranchPath = selectedBranch === null
    ? '/club/branches'
    : `/club/branches/${encodeURIComponent(selectedBranch.profile.branchId)}`;

  return (
    <>
      <header className="app-header club-header" data-club-chrome>
        <div className="app-title">AFK4 Club</div>
        <nav className="club-nav" aria-label="Club navigation">
          <button type="button" className="link" onClick={() => onNavigate({ kind: 'clubInstall' }, installPath)}>
            Install
          </button>
          <button type="button" className="link" onClick={() => onNavigate({ kind: 'clubDashboard' }, '/club')}>
            Overview
          </button>
          <button type="button" className="link" onClick={() => onNavigate({ kind: 'clubBranches' }, '/club/branches')}>
            Branches
          </button>
          {selectedBranch !== null && (
            <>
              <button
                type="button"
                className="link"
                onClick={() => onNavigate(
                  { kind: 'clubBranchDetail', branchId: selectedBranch.profile.branchId },
                  firstBranchPath
                )}
              >
                Branch detail
              </button>
              <button
                type="button"
                className="link"
                onClick={() => onNavigate(
                  { kind: 'clubBranchDevices', branchId: selectedBranch.profile.branchId },
                  `${firstBranchPath}/devices`
                )}
              >
                Devices
              </button>
              <button
                type="button"
                className="link"
                onClick={() => onNavigate(
                  { kind: 'clubBranchPendingDevices', branchId: selectedBranch.profile.branchId },
                  `${firstBranchPath}/devices/pending`
                )}
              >
                Pending
              </button>
              <button
                type="button"
                className="link"
                onClick={() => onNavigate(
                  { kind: 'clubBranchFloorMap', branchId: selectedBranch.profile.branchId },
                  `${firstBranchPath}/floor-map`
                )}
              >
                Floor map
              </button>
              <button
                type="button"
                className="link"
                onClick={() => onNavigate(
                  { kind: 'clubBranchOperators', branchId: selectedBranch.profile.branchId },
                  `${firstBranchPath}/operators`
                )}
              >
                Operators
              </button>
            </>
          )}
        </nav>
        <div className="app-session">
          <span className="muted">{session.displayName}</span>
          <button type="button" onClick={onSignOut}>Sign out</button>
        </div>
      </header>
      <main>
        <LegacyClubScreen client={client} route={route} session={session} onNavigate={onNavigate} />
      </main>
    </>
  );
}

export interface LegacyClubScreenProps {
  client: ClubApiClient;
  route: ClubRoute;
  session: StaffSession;
  onNavigate: (route: ClubRoute, path: string) => void;
}

/**
 * Renders the per-route body for the legacy club screens WITHOUT the legacy
 * top chrome/nav. Reused both by {@link ClubDashboard} (which adds its own
 * header) and by the new AppShell-based layout in App.tsx, which supplies its
 * own chrome. This is the "reparented" body of the old monolithic dashboard.
 */
export function LegacyClubScreen({ client, route, session, onNavigate }: LegacyClubScreenProps) {
  const [branches, setBranches] = useState<BranchSummary[]>([]);
  const [branchLoadState, setBranchLoadState] = useState<'loading' | 'ready' | 'failed'>('loading');
  const [branchError, setBranchError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      if (session.branchIds.length === 0) {
        setBranches([]);
        setBranchLoadState('ready');
        setBranchError(null);
        return;
      }

      setBranchLoadState('loading');
      setBranchError(null);
      try {
        const loaded = await Promise.all(session.branchIds.map(branchId => loadBranchSummary(client, branchId)));
        if (!cancelled) {
          setBranches(loaded);
          setBranchLoadState('ready');
        }
      } catch (cause) {
        if (!cancelled) {
          setBranchLoadState('failed');
          setBranchError(projectError(cause));
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [client, session.branchIds]);

  const installPath = '/club/install';

  return (
    <>
      {branchLoadState === 'loading' && route.kind !== 'clubInstall' && (
        <div className="page">
          <Loading label="Loading club branches..." />
        </div>
      )}
      {branchLoadState === 'failed' && (
        <div className="page">
          <ErrorBanner message={branchError} onDismiss={() => setBranchError(null)} />
        </div>
      )}
      {route.kind === 'clubInstall' && (
        <InstallScreen client={client} session={session} branches={branches} />
      )}
      {route.kind === 'clubDashboard' && branchLoadState === 'ready' && (
        <DashboardHome
          branches={branches}
          onOpenInstall={() => onNavigate({ kind: 'clubInstall' }, installPath)}
          onOpenBranch={branchId => onNavigate(
            { kind: 'clubBranchDetail', branchId },
            `/club/branches/${encodeURIComponent(branchId)}`
          )}
        />
      )}
      {route.kind === 'clubBranches' && branchLoadState === 'ready' && (
        <BranchesScreen
          branches={branches}
          onOpenBranch={branchId => onNavigate(
            { kind: 'clubBranchDetail', branchId },
            `/club/branches/${encodeURIComponent(branchId)}`
          )}
        />
      )}
      {route.kind === 'clubBranchDetail' && (
        <BranchDetailScreen
          client={client}
          session={session}
          branchId={route.branchId}
        />
      )}
      {route.kind === 'clubBranchFloorMap' && (
        <FloorMapScreen
          client={client}
          session={session}
          branchId={route.branchId}
        />
      )}
      {route.kind === 'clubBranchDevices' && (
        <DevicesScreen
          client={client}
          session={session}
          branchId={route.branchId}
          pendingOnly={false}
        />
      )}
      {route.kind === 'clubBranchPendingDevices' && (
        <DevicesScreen
          client={client}
          session={session}
          branchId={route.branchId}
          pendingOnly
        />
      )}
      {route.kind === 'clubBranchOperators' && (
        <OperatorsScreen
          client={client}
          session={session}
          branchId={route.branchId}
        />
      )}
    </>
  );
}

async function loadBranchSummary(client: ClubApiClient, branchId: string): Promise<BranchSummary> {
  const profile = await client.getBranchProfile(branchId);
  const [dashboard, devices, pendingDevices] = await Promise.all([
    client.getDashboardSummary(branchId).catch(() => null),
    client.listDevices(branchId).catch(() => []),
    client.listPendingDevices(branchId).catch(() => [])
  ]);
  return {
    profile,
    dashboard,
    devices,
    pendingDevices,
    error: dashboard === null ? 'Dashboard metrics unavailable.' : null
  };
}

function DashboardHome({
  branches,
  onOpenInstall,
  onOpenBranch
}: {
  branches: BranchSummary[];
  onOpenInstall: () => void;
  onOpenBranch: (branchId: string) => void;
}) {
  const totalDevices = branches.reduce((sum, branch) => sum + branch.devices.length, 0);
  const totalActiveSessions = branches.reduce(
    (sum, branch) => sum + (branch.dashboard?.utilization.activeSessions ?? 0),
    0
  );
  const totalAlerts = branches.reduce(
    (sum, branch) => sum + (branch.dashboard?.alertPressure.totalAlerts ?? branch.pendingDevices.length),
    0
  );

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>Club overview</h1>
          <p className="muted">Branch status for owner onboarding and PC setup.</p>
        </div>
        <button type="button" className="primary" onClick={onOpenInstall}>
          Open install
        </button>
      </div>
      <section className="section kpi-grid" aria-label="Club KPIs">
        <KpiCard label="Devices" value={totalDevices.toString()} />
        <KpiCard label="Active sessions" value={totalActiveSessions.toString()} />
        <KpiCard label="Alerts today" value={totalAlerts.toString()} />
      </section>
      <section className="section">
        <div className="section-header">
          <h2>Branches</h2>
        </div>
        {branches.length === 0 ? (
          <EmptyState>No branches are assigned to this staff account.</EmptyState>
        ) : (
          <ul className="cards branch-card-grid">
            {branches.map(branch => (
              <li className="card branch-card" key={branch.profile.branchId}>
                <div className="card-header">
                  <span>{branch.profile.name}</span>
                  <span className="muted">{branch.profile.city}</span>
                </div>
                <div className="club-mini-kpis">
                  <span>{branch.devices.length} devices</span>
                  <span>{branch.dashboard?.utilization.activeSessions ?? 0} active</span>
                  <span>{branch.dashboard?.alertPressure.totalAlerts ?? branch.pendingDevices.length} alerts</span>
                </div>
                {branch.error !== null && <p className="muted">{branch.error}</p>}
                <button type="button" onClick={() => onOpenBranch(branch.profile.branchId)}>
                  Open branch
                </button>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function BranchesScreen({
  branches,
  onOpenBranch
}: {
  branches: BranchSummary[];
  onOpenBranch: (branchId: string) => void;
}) {
  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>Branches</h1>
          <p className="muted">Owner-visible branches from the signed-in staff session.</p>
        </div>
      </div>
      <section className="section">
        {branches.length === 0 ? (
          <EmptyState>No branches are assigned to this staff account.</EmptyState>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Name</th>
                <th>City</th>
                <th>Devices</th>
                <th>Active sessions</th>
                <th>Pending</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {branches.map(branch => (
                <tr key={branch.profile.branchId}>
                  <td>{branch.profile.name}</td>
                  <td>{branch.profile.city}</td>
                  <td>{branch.devices.length}</td>
                  <td>{branch.dashboard?.utilization.activeSessions ?? 0}</td>
                  <td>{branch.pendingDevices.length}</td>
                  <td>
                    <button type="button" onClick={() => onOpenBranch(branch.profile.branchId)}>
                      Open
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

function InstallScreen({
  client,
  session,
  branches
}: {
  client: ClubApiClient;
  session: StaffSession;
  branches: BranchSummary[];
}) {
  const [summary, setSummary] = useState<OwnerCodeSummary | null>(null);
  const [issued, setIssued] = useState<OwnerCodeIssued | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'saving'>('loading');
  const [reason, setReason] = useState('dashboard rotation');
  const [error, setError] = useState<string | null>(null);
  const canManage = hasPermission(session, Permissions.manageOwnerCode);
  const setupMsiUrl = getSetupMsiUrl();

  useEffect(() => {
    let cancelled = false;
    async function load() {
      if (!canManage) {
        setState('ready');
        return;
      }
      setState('loading');
      setError(null);
      try {
        const current = await client.getOwnerCode();
        if (!cancelled) {
          setSummary(current);
          setIssued(null);
          setState('ready');
        }
      } catch (cause) {
        if (!cancelled) {
          setError(projectError(cause));
          setState('ready');
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [client, canManage]);

  async function generate() {
    setState('saving');
    setError(null);
    try {
      const next = await client.generateOwnerCode();
      setIssued(next);
      setSummary({
        codeSuffix: next.codeSuffix,
        expiresAtUtc: next.expiresAtUtc,
        lastUsedAtUtc: null,
        failedAttemptCount: 0
      });
    } catch (cause) {
      setError(projectError(cause));
    } finally {
      setState('ready');
    }
  }

  async function rotate() {
    setState('saving');
    setError(null);
    try {
      const next = await client.rotateOwnerCode(reason.trim() || 'dashboard rotation');
      setIssued(next);
      setSummary({
        codeSuffix: next.codeSuffix,
        expiresAtUtc: next.expiresAtUtc,
        lastUsedAtUtc: null,
        failedAttemptCount: 0
      });
    } catch (cause) {
      setError(projectError(cause));
    } finally {
      setState('ready');
    }
  }

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>Install AFK4 on PCs</h1>
          <p className="muted">Use the owner code in the Windows setup wizard.</p>
        </div>
        <a className="button-link primary" href={setupMsiUrl} download>
          Download AFK4 Setup MSI
        </a>
      </div>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      <section className="section">
        <div className="section-header">
          <h2>Owner code</h2>
          {state === 'loading' && <Loading label="Loading owner code..." />}
        </div>
        {!canManage ? (
          <EmptyState>Your staff account cannot generate owner codes.</EmptyState>
        ) : (
          <div className="owner-code-panel">
            <div className="owner-code-value" aria-label="Owner code">
              {issued?.ownerCode ?? (summary === null ? 'No active code' : `**** ${summary.codeSuffix}`)}
            </div>
            <dl className="kv">
              <dt>Valid until</dt>
              <dd>{summary === null ? 'Not generated' : formatDate(summary.expiresAtUtc)}</dd>
              <dt>Last used</dt>
              <dd>{summary?.lastUsedAtUtc === undefined ? 'Not generated' : formatDate(summary.lastUsedAtUtc)}</dd>
              <dt>Failed attempts</dt>
              <dd>{summary?.failedAttemptCount ?? 0}</dd>
            </dl>
            <div className="actions">
              <button type="button" className="primary" disabled={state !== 'ready'} onClick={() => void generate()}>
                Generate code
              </button>
              <Field label="Rotation reason" htmlFor="owner-code-rotation-reason">
                <input
                  id="owner-code-rotation-reason"
                  value={reason}
                  onChange={event => setReason(event.target.value)}
                  disabled={state === 'saving'}
                />
              </Field>
              <button type="button" disabled={state !== 'ready' || summary === null} onClick={() => void rotate()}>
                Rotate
              </button>
            </div>
          </div>
        )}
      </section>
      <section className="section">
        <div className="section-header">
          <h2>Setup wizard path</h2>
        </div>
        <ol className="setup-steps">
          <li>Run the MSI on the Windows 10/11 PC.</li>
          <li>Enter the 8-digit owner code.</li>
          <li>Pick the branch and the target seat from the floor map.</li>
          <li>Choose Gaming PC or Manager workstation and finish enrollment.</li>
        </ol>
        <pre className="command-block">msiexec /i AFK4-Agent.msi</pre>
      </section>
      <section className="section">
        <div className="section-header">
          <h2>Branches available to the wizard</h2>
        </div>
        {branches.length === 0 ? (
          <EmptyState>No branches are assigned to this owner account.</EmptyState>
        ) : (
          <ul className="cards">
            {branches.map(branch => (
              <li className="card" key={branch.profile.branchId}>
                <div className="card-header">
                  <span>{branch.profile.name}</span>
                  <span className="muted">{branch.devices.length} PCs</span>
                </div>
                <span className="muted">{branch.profile.city}</span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function BranchDetailScreen({
  client,
  session,
  branchId
}: {
  client: ClubApiClient;
  session: StaffSession;
  branchId: string;
}) {
  const [profile, setProfile] = useState<BranchProfile | null>(null);
  const [settings, setSettings] = useState<BranchSettings | null>(null);
  const [name, setName] = useState('');
  const [city, setCity] = useState('');
  const [manualApproval, setManualApproval] = useState(false);
  const [state, setState] = useState<'loading' | 'ready' | 'saving'>('loading');
  const [error, setError] = useState<string | null>(null);
  const canManageLayout = hasPermission(session, Permissions.manageLayout);
  const canManageSettings = hasPermission(session, Permissions.manageBranchSettings);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      setState('loading');
      setError(null);
      try {
        const [nextProfile, nextSettings] = await Promise.all([
          client.getBranchProfile(branchId),
          canManageSettings ? client.getBranchSettings(branchId).catch(() => null) : Promise.resolve(null)
        ]);
        if (!cancelled) {
          setProfile(nextProfile);
          setSettings(nextSettings);
          setName(nextProfile.name);
          setCity(nextProfile.city);
          setManualApproval(nextSettings?.requireManualDeviceApproval ?? false);
          setState('ready');
        }
      } catch (cause) {
        if (!cancelled) {
          setError(projectError(cause));
          setState('ready');
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [branchId, canManageSettings, client]);

  async function saveProfile(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (profile === null) {
      return;
    }
    setState('saving');
    setError(null);
    try {
      const updated = await client.updateBranchProfile(branchId, {
        organizationId: session.organizationId,
        name: name.trim(),
        city: city.trim()
      });
      setProfile(updated);
      setName(updated.name);
      setCity(updated.city);
    } catch (cause) {
      setError(projectError(cause));
    } finally {
      setState('ready');
    }
  }

  async function saveSettings() {
    setState('saving');
    setError(null);
    try {
      const updated = await client.updateBranchSettings(branchId, {
        organizationId: session.organizationId,
        requireManualDeviceApproval: manualApproval
      });
      setSettings(updated);
      setManualApproval(updated.requireManualDeviceApproval);
    } catch (cause) {
      setError(projectError(cause));
    } finally {
      setState('ready');
    }
  }

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>Branch detail</h1>
          <p className="muted">{branchId}</p>
        </div>
      </div>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      {state === 'loading' && <Loading label="Loading branch..." />}
      {profile !== null && (
        <section className="section">
          <div className="section-header">
            <h2>Profile</h2>
          </div>
          <form className="form form-grid" onSubmit={saveProfile}>
            <Field label="Branch name" htmlFor="branch-name">
              <input
                id="branch-name"
                value={name}
                onChange={event => setName(event.target.value)}
                disabled={!canManageLayout || state === 'saving'}
                required
              />
            </Field>
            <Field label="City" htmlFor="branch-city">
              <input
                id="branch-city"
                value={city}
                onChange={event => setCity(event.target.value)}
                disabled={!canManageLayout || state === 'saving'}
                required
              />
            </Field>
            <div className="actions">
              <button type="submit" className="primary" disabled={!canManageLayout || state === 'saving'}>
                Save profile
              </button>
            </div>
          </form>
        </section>
      )}
      <section className="section">
        <div className="section-header">
          <h2>Device approval</h2>
        </div>
        {!canManageSettings ? (
          <EmptyState>Your staff account cannot change branch settings.</EmptyState>
        ) : (
          <div className="setting-row">
            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={manualApproval}
                onChange={event => setManualApproval(event.target.checked)}
                disabled={state === 'saving' || settings === null}
              />
              Require manual approval for newly enrolled PCs
            </label>
            <button type="button" onClick={() => void saveSettings()} disabled={state === 'saving' || settings === null}>
              Save setting
            </button>
          </div>
        )}
      </section>
    </div>
  );
}

interface EditorZone {
  clientId: string;
  zoneId: string | null;
  name: string;
  sortOrder: number;
  seats: EditorSeat[];
}

interface EditorSeat {
  clientId: string;
  seatId: string | null;
  name: string;
  sortOrder: number;
}

function FloorMapScreen({
  client,
  session,
  branchId
}: {
  client: ClubApiClient;
  session: StaffSession;
  branchId: string;
}) {
  const [floorMap, setFloorMap] = useState<FloorMap | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [zones, setZones] = useState<EditorZone[]>([]);
  const [state, setState] = useState<'loading' | 'ready' | 'saving'>('loading');
  const [error, setError] = useState<string | null>(null);
  const canEdit = hasPermission(session, Permissions.manageLayout);

  async function load() {
    setState('loading');
    setError(null);
    try {
      const result = await client.getFloorMap(branchId);
      setFloorMap(result.floorMap);
      setEtag(result.etag);
      setZones(toEditorZones(result.floorMap));
    } catch (cause) {
      setError(projectError(cause));
    } finally {
      setState('ready');
    }
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId, client]);

  function updateZoneName(clientId: string, value: string) {
    setZones(current => current.map(zone => zone.clientId === clientId ? { ...zone, name: value } : zone));
  }

  function updateSeatName(zoneClientId: string, seatClientId: string, value: string) {
    setZones(current => current.map(zone => zone.clientId === zoneClientId
      ? {
          ...zone,
          seats: zone.seats.map(seat => seat.clientId === seatClientId ? { ...seat, name: value } : seat)
        }
      : zone));
  }

  function addZone() {
    setZones(current => [
      ...current,
      {
        clientId: makeClientId('zone'),
        zoneId: null,
        name: `Zone ${current.length + 1}`,
        sortOrder: current.length + 1,
        seats: []
      }
    ]);
  }

  function addSeat(zoneClientId: string) {
    setZones(current => current.map(zone => {
      if (zone.clientId !== zoneClientId) {
        return zone;
      }
      return {
        ...zone,
        seats: [
          ...zone.seats,
          {
            clientId: makeClientId('seat'),
            seatId: null,
            name: `PC-${zone.seats.length + 1}`,
            sortOrder: zone.seats.length + 1
          }
        ]
      };
    }));
  }

  function removeSeat(zoneClientId: string, seatClientId: string) {
    setZones(current => current.map(zone => zone.clientId === zoneClientId
      ? { ...zone, seats: zone.seats.filter(seat => seat.clientId !== seatClientId) }
      : zone));
  }

  async function save() {
    setState('saving');
    setError(null);
    try {
      const compacted = compactZones(zones);
      const request = {
        organizationId: session.organizationId,
        zones: compacted.map<FloorMapBulkZoneRequest>((zone, zoneIndex) => ({
          zoneId: zone.zoneId,
          clientId: zone.clientId,
          name: zone.name.trim(),
          sortOrder: zoneIndex + 1
        })),
        seats: compacted.flatMap<FloorMapBulkSeatRequest>((zone) => zone.seats.map((seat, seatIndex) => ({
          seatId: seat.seatId,
          clientId: seat.clientId,
          zoneClientId: zone.clientId,
          name: seat.name.trim(),
          sortOrder: seatIndex + 1
        })))
      };
      const response = await client.updateFloorMap(branchId, request, etag);
      setEtag(response.eTag ?? null);
      await load();
    } catch (cause) {
      if (cause instanceof PlatformApiError && cause.status === 412) {
        setError('Floor map changed in another browser session. Reload before saving again.');
      } else {
        setError(projectError(cause));
      }
      setState('ready');
    }
  }

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>Floor map</h1>
          <p className="muted">{floorMap?.branchName ?? branchId}</p>
        </div>
        <div className="actions">
          <button type="button" onClick={() => void load()} disabled={state === 'loading' || state === 'saving'}>
            Reload
          </button>
          <button type="button" className="primary" onClick={() => void save()} disabled={!canEdit || state !== 'ready'}>
            Save map
          </button>
        </div>
      </div>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      {state === 'loading' && <Loading label="Loading floor map..." />}
      <section className="section floor-map-editor">
        {zones.length === 0 ? (
          <EmptyState>No zones or seats yet.</EmptyState>
        ) : (
          zones.map(zone => (
            <div className="floor-zone" key={zone.clientId}>
              <Field label="Zone name" htmlFor={`zone-${zone.clientId}`}>
                <input
                  id={`zone-${zone.clientId}`}
                  value={zone.name}
                  onChange={event => updateZoneName(zone.clientId, event.target.value)}
                  disabled={!canEdit || state === 'saving'}
                />
              </Field>
              <div className="floor-seat-grid">
                {zone.seats.map(seat => (
                  <div className="floor-seat-edit" key={seat.clientId}>
                    <input
                      aria-label={`Seat name ${seat.name}`}
                      value={seat.name}
                      onChange={event => updateSeatName(zone.clientId, seat.clientId, event.target.value)}
                      disabled={!canEdit || state === 'saving'}
                    />
                    <button
                      type="button"
                      onClick={() => removeSeat(zone.clientId, seat.clientId)}
                      disabled={!canEdit || state === 'saving'}
                    >
                      Remove
                    </button>
                  </div>
                ))}
                <button type="button" onClick={() => addSeat(zone.clientId)} disabled={!canEdit || state === 'saving'}>
                  Add seat
                </button>
              </div>
            </div>
          ))
        )}
        <button type="button" onClick={addZone} disabled={!canEdit || state === 'saving'}>
          Add zone
        </button>
      </section>
    </div>
  );
}

function DevicesScreen({
  client,
  session,
  branchId,
  pendingOnly
}: {
  client: ClubApiClient;
  session: StaffSession;
  branchId: string;
  pendingOnly: boolean;
}) {
  const [devices, setDevices] = useState<DeviceInventoryItem[]>([]);
  const [floorMap, setFloorMap] = useState<FloorMap | null>(null);
  const [state, setState] = useState<'loading' | 'ready' | 'saving'>('loading');
  const [error, setError] = useState<string | null>(null);
  const canAssign = hasPermission(session, Permissions.assignDeviceSeat);
  const canRemove = hasPermission(session, Permissions.revokeDeviceCredential);

  async function load() {
    setState('loading');
    setError(null);
    try {
      const [nextDevices, map] = await Promise.all([
        pendingOnly ? client.listPendingDevices(branchId) : client.listDevices(branchId),
        client.getFloorMap(branchId).then(result => result.floorMap).catch(() => null)
      ]);
      setDevices(nextDevices);
      setFloorMap(map);
    } catch (cause) {
      setError(projectError(cause));
    } finally {
      setState('ready');
    }
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId, client, pendingOnly]);

  async function runAction(action: () => Promise<unknown>) {
    setState('saving');
    setError(null);
    try {
      await action();
      await load();
    } catch (cause) {
      setError(projectError(cause));
      setState('ready');
    }
  }

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>{pendingOnly ? 'Pending PCs' : 'Devices'}</h1>
          <p className="muted">{branchId}</p>
        </div>
        <button type="button" onClick={() => void load()} disabled={state === 'loading' || state === 'saving'}>
          Refresh
        </button>
      </div>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      {state === 'loading' && <Loading label="Loading devices..." />}
      <section className="section">
        {devices.length === 0 ? (
          <EmptyState>{pendingOnly ? 'No PCs are waiting for approval.' : 'No devices are enrolled in this branch.'}</EmptyState>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>PC</th>
                <th>Seat</th>
                <th>State</th>
                <th>Heartbeat</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {devices.map(device => (
                <DeviceRow
                  key={device.deviceId}
                  client={client}
                  device={device}
                  seats={floorMap?.seats ?? []}
                  organizationId={session.organizationId}
                  canAssign={canAssign}
                  canRemove={canRemove}
                  disabled={state === 'saving'}
                  onAction={runAction}
                />
              ))}
            </tbody>
          </table>
        )}
      </section>
    </div>
  );
}

function DeviceRow({
  client,
  device,
  seats,
  organizationId,
  canAssign,
  canRemove,
  disabled,
  onAction
}: {
  client: ClubApiClient;
  device: DeviceInventoryItem;
  seats: SeatStatus[];
  organizationId: string;
  canAssign: boolean;
  canRemove: boolean;
  disabled: boolean;
  onAction: (action: () => Promise<unknown>) => Promise<void>;
}) {
  const [displayName, setDisplayName] = useState(device.displayName || device.machineName);
  const [seatId, setSeatId] = useState(device.seatId ?? '');
  const [reason, setReason] = useState('dashboard device admin');
  const isPending = device.enrollmentState === 'pending';

  useEffect(() => {
    setDisplayName(device.displayName || device.machineName);
    setSeatId(device.seatId ?? '');
  }, [device]);

  return (
    <tr>
      <td>
        <strong>{device.displayName || device.machineName}</strong>
        <div className="muted">{device.machineName}</div>
      </td>
      <td>{device.seatName ?? 'Unassigned'}</td>
      <td>
        <StatusBadge status={device.enrollmentState} />{' '}
        <span className={device.isOnline ? 'status-online' : 'status-offline'}>
          {device.isOnline ? 'online' : 'offline'}
        </span>
      </td>
      <td>{formatDate(device.lastHeartbeatAtUtc)}</td>
      <td>
        <div className="device-actions">
          <input
            aria-label={`Display name for ${device.machineName}`}
            value={displayName}
            onChange={event => setDisplayName(event.target.value)}
            disabled={!canAssign || disabled}
          />
          <button
            type="button"
            disabled={!canAssign || disabled}
            onClick={() => onAction(() => client.renameDevice(device.deviceId, organizationId, displayName))}
          >
            Rename
          </button>
          <select
            aria-label={`Seat for ${device.machineName}`}
            value={seatId}
            onChange={event => setSeatId(event.target.value)}
            disabled={!canAssign || disabled || seats.length === 0}
          >
            <option value="">Select seat</option>
            {seats.map(seat => (
              <option key={seat.seatId} value={seat.seatId}>
                {seat.zoneName} / {seat.seatName}
              </option>
            ))}
          </select>
          <button
            type="button"
            disabled={!canAssign || disabled || seatId.length === 0}
            onClick={() => onAction(() => client.moveDeviceSeat(device.deviceId, organizationId, seatId))}
          >
            Move
          </button>
          <input
            aria-label={`Reason for ${device.machineName}`}
            value={reason}
            onChange={event => setReason(event.target.value)}
            disabled={disabled}
          />
          {isPending && (
            <>
              <button
                type="button"
                disabled={!canAssign || disabled}
                onClick={() => onAction(() => client.approveDevice(device.deviceId, organizationId, reason))}
              >
                Approve
              </button>
              <button
                type="button"
                disabled={!canAssign || disabled}
                onClick={() => onAction(() => client.rejectDevice(device.deviceId, organizationId, reason))}
              >
                Reject
              </button>
            </>
          )}
          <button
            type="button"
            disabled={!canRemove || disabled}
            onClick={() => onAction(() => client.removeDevice(device.deviceId, organizationId, reason))}
          >
            Remove
          </button>
        </div>
      </td>
    </tr>
  );
}

function OperatorsScreen({
  client,
  session,
  branchId
}: {
  client: ClubApiClient;
  session: StaffSession;
  branchId: string;
}) {
  const [staff, setStaff] = useState<StaffUser[]>([]);
  const [state, setState] = useState<'loading' | 'ready' | 'saving'>('loading');
  const [error, setError] = useState<string | null>(null);
  const [userName, setUserName] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [roleName, setRoleName] = useState<(typeof StaffRoles)[number]>('branch_manager');
  const canManage = hasPermission(session, Permissions.manageBranchStaff);

  async function load() {
    if (!canManage) {
      setState('ready');
      return;
    }
    setState('loading');
    setError(null);
    try {
      setStaff(await client.listStaff(branchId));
    } catch (cause) {
      setError(projectError(cause));
    } finally {
      setState('ready');
    }
  }

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [branchId, client, canManage]);

  async function createStaff(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setState('saving');
    setError(null);
    try {
      const created = await client.createStaff(branchId, {
        organizationId: session.organizationId,
        userName: userName.trim(),
        displayName: displayName.trim(),
        password,
        roleNames: [roleName]
      });
      setStaff(current => [...current, created].sort((left, right) => left.displayName.localeCompare(right.displayName)));
      setUserName('');
      setDisplayName('');
      setPassword('');
    } catch (cause) {
      setError(projectError(cause));
    } finally {
      setState('ready');
    }
  }

  return (
    <div className="page">
      <div className="page-header">
        <div>
          <h1>Operators</h1>
          <p className="muted">{branchId}</p>
        </div>
      </div>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      {!canManage ? (
        <section className="section">
          <EmptyState>Your staff account cannot manage operators.</EmptyState>
        </section>
      ) : (
        <>
          <section className="section">
            <div className="section-header">
              <h2>Create operator</h2>
            </div>
            <form className="form form-grid" onSubmit={createStaff}>
              <Field label="User name" htmlFor="operator-user-name">
                <input
                  id="operator-user-name"
                  value={userName}
                  onChange={event => setUserName(event.target.value)}
                  disabled={state === 'saving'}
                  required
                />
              </Field>
              <Field label="Display name" htmlFor="operator-display-name">
                <input
                  id="operator-display-name"
                  value={displayName}
                  onChange={event => setDisplayName(event.target.value)}
                  disabled={state === 'saving'}
                  required
                />
              </Field>
              <Field label="Temporary password" htmlFor="operator-password">
                <input
                  id="operator-password"
                  type="password"
                  value={password}
                  onChange={event => setPassword(event.target.value)}
                  disabled={state === 'saving'}
                  required
                />
              </Field>
              <Field label="Role" htmlFor="operator-role">
                <select
                  id="operator-role"
                  value={roleName}
                  onChange={event => setRoleName(event.target.value as (typeof StaffRoles)[number])}
                  disabled={state === 'saving'}
                >
                  {StaffRoles.map(role => <option key={role} value={role}>{role}</option>)}
                </select>
              </Field>
              <div className="actions">
                <button type="submit" className="primary" disabled={state === 'saving'}>
                  Create
                </button>
              </div>
            </form>
          </section>
          <section className="section">
            <div className="section-header">
              <h2>Staff users</h2>
              {state === 'loading' && <Loading label="Loading operators..." />}
            </div>
            {staff.length === 0 ? (
              <EmptyState>No operators are assigned to this branch.</EmptyState>
            ) : (
              <table className="table">
                <thead>
                  <tr>
                    <th>User</th>
                    <th>Roles</th>
                    <th>Status</th>
                    <th>Created</th>
                  </tr>
                </thead>
                <tbody>
                  {staff.map(user => (
                    <tr key={user.staffUserId}>
                      <td>
                        <strong>{user.displayName}</strong>
                        <div className="muted">{user.userName}</div>
                      </td>
                      <td>{user.roleNames.join(', ')}</td>
                      <td>{user.isActive ? 'active' : 'inactive'}</td>
                      <td>{formatDate(user.createdAtUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function KpiCard({ label, value }: { label: string; value: string }) {
  return (
    <div className="kpi-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function toEditorZones(floorMap: FloorMap): EditorZone[] {
  const zonesById = new Map<string, EditorZone>();
  for (const zone of floorMap.zones ?? []) {
    zonesById.set(zone.zoneId, {
      clientId: zone.zoneId,
      zoneId: zone.zoneId,
      name: zone.name,
      sortOrder: zone.sortOrder,
      seats: []
    });
  }
  for (const seat of floorMap.seats) {
    let zone = zonesById.get(seat.zoneId);
    if (zone === undefined) {
      zone = {
        clientId: seat.zoneId,
        zoneId: seat.zoneId,
        name: seat.zoneName,
        sortOrder: zonesById.size + 1,
        seats: []
      };
      zonesById.set(seat.zoneId, zone);
    }
    zone.seats.push({
      clientId: seat.seatId,
      seatId: seat.seatId,
      name: seat.seatName,
      sortOrder: seat.sortOrder
    });
  }
  return Array.from(zonesById.values())
    .sort((left, right) => left.sortOrder - right.sortOrder)
    .map(zone => ({
      ...zone,
      seats: [...zone.seats].sort((left, right) => left.sortOrder - right.sortOrder)
    }));
}

function compactZones(zones: EditorZone[]): EditorZone[] {
  return zones
    .filter(zone => zone.name.trim().length > 0)
    .map((zone, zoneIndex) => ({
      ...zone,
      sortOrder: zoneIndex + 1,
      seats: zone.seats
        .filter(seat => seat.name.trim().length > 0)
        .map((seat, seatIndex) => ({ ...seat, sortOrder: seatIndex + 1 }))
    }));
}

function hasPermission(session: StaffSession, permission: string): boolean {
  return session.permissions.some(candidate => candidate.localeCompare(permission, undefined, { sensitivity: 'accent' }) === 0);
}

function sameId(left: string, right: string): boolean {
  return left.localeCompare(right, undefined, { sensitivity: 'accent' }) === 0;
}

function makeClientId(prefix: string): string {
  return `${prefix}-${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function getSetupMsiUrl(): string {
  const configured = import.meta.env.VITE_SETUP_MSI_URL;
  return typeof configured === 'string' && configured.trim().length > 0
    ? configured.trim()
    : '/downloads/AFK4-Agent.msi';
}

function projectError(cause: unknown): string {
  if (cause instanceof PlatformApiError) {
    return cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return 'The request failed.';
}
