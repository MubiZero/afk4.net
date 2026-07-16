import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import { permissionNames } from '../operatorPermissions';

// Task 2.1: ManagementWorkspace now loads the settings-domain data (zones/staff/catalog/
// tariffs/packages/device lists) that the halls/tariffs/staff/goods destinations need, via
// createAuthenticatedOperatorClients — so this file mocks that module the same way
// settingsSectionsSmoke.test.tsx does for BackendSettingsWorkspace. mock.module leaks
// process-wide, hence the real-module snapshot + afterAll restore.
const getBranchProfile = mock(async () => ({}));
const getLayoutZones = mock(async () => []);
const getTariffOptions = mock(async () => []);
const getStaffUsers = mock(async () => []);
const getPackageOptions = mock(async () => []);
const getCatalog = mock(async () => []);
const getDiagnostics = mock(async () => ({}));
const getRolloutStatuses = mock(async () => []);
const listDevices = mock(async () => []);
const listBranchDeviceCommands = mock(async () => []);

const actualHelpers = await import('../operatorHelpers');
mock.module('../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: {
      getBranchProfile,
      getLayoutZones,
      getTariffOptions,
      getStaffUsers,
      getPackageOptions
    },
    pos: { getCatalog },
    diagnostics: { getDiagnostics },
    updates: { getRolloutStatuses },
    devices: { listDevices, listBranchDeviceCommands }
  })
}));

const { ManagementWorkspace } = await import('./ManagementWorkspace');

afterAll(() => {
  mock.module('../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  getBranchProfile.mockClear();
  getLayoutZones.mockClear();
  getTariffOptions.mockClear();
  getStaffUsers.mockClear();
  getPackageOptions.mockClear();
  getCatalog.mockClear();
  getDiagnostics.mockClear();
  getRolloutStatuses.mockClear();
  listDevices.mockClear();
  listBranchDeviceCommands.mockClear();
});

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1', displayName: 'x' }) as never;

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [] },
  branchId: 'b1'
};

describe('ManagementWorkspace', () => {
  it('renders only the destinations the session may see', () => {
    wrap(<ManagementWorkspace backend={null} session={session([permissionNames.manageNews, permissionNames.manageLoyaltySettings])} currencyCode="TJS" />);
    expect(screen.getByRole('button', { name: 'Новости' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Лояльность' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Клуб' })).toBeNull();
  });

  it('shows a no-access message when nothing is permitted', () => {
    wrap(<ManagementWorkspace backend={null} session={session([])} currencyCode="TJS" />);
    expect(screen.getByText('Нет доступных разделов')).toBeTruthy(); // op.management.noAccess ru value
  });

  it('switches the active destination on nav click', () => {
    wrap(<ManagementWorkspace backend={null} session={session([permissionNames.manageBranchSettings, permissionNames.manageNews])} currencyCode="TJS" />);
    fireEvent.click(screen.getByRole('button', { name: 'Новости' }));
    // News screen head renders its subtitle
    expect(screen.getByRole('heading', { name: 'Новости' })).toBeTruthy();
  });

  it('loads settings-domain slices once on mount with a backend, and skips integrations', async () => {
    // Permission picked deliberately: any of club/loyalty/news would additionally trigger that
    // destination's own independent load (e.g. ClubDestination also calls getBranchProfile),
    // double-counting calls that belong to it, not to ManagementWorkspace's own load. `tariffs`
    // still renders the slice-1 stub (no client calls of its own), so the count stays exact.
    wrap(
      <ManagementWorkspace
        backend={backend as never}
        session={session([permissionNames.manageTariffs])}
        currencyCode="TJS"
      />
    );

    await waitFor(() => expect(getBranchProfile).toHaveBeenCalledTimes(1));
    expect(getBranchProfile).toHaveBeenCalledWith('b1');
    expect(getLayoutZones).toHaveBeenCalledTimes(1);
    expect(getTariffOptions).toHaveBeenCalledTimes(1);
    expect(getStaffUsers).toHaveBeenCalledTimes(1);
    expect(getCatalog).toHaveBeenCalledTimes(1);

    expect(getDiagnostics).not.toHaveBeenCalled();
    expect(getRolloutStatuses).not.toHaveBeenCalled();
  });
});
