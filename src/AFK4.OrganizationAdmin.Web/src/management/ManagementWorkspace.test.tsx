import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import { permissionNames } from '../operatorPermissions';
import { supportPermissions } from '../support/supportWorkspaces';

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
    devices: { listDevices }
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
    expect(screen.getByRole('button', { name: 'Платежи и лояльность' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Клуб' })).toBeNull();
  });

  it('under a support session, hides Тарифы/Товары/Платежи/Новости — no tab that would 403 on save — and keeps Клуб', () => {
    // The exact permission set supportOperatorSession would build for a grant covering all five
    // writable areas: read everywhere, write only where the grant maps to a real server-tagged
    // write endpoint (see support/supportWorkspaces.ts). None of manageTariffs/managePackages/
    // managePosCatalog/manageInventoryStock/managePaymentGateways/manageLoyaltySettings/manageNews
    // ever appear in that set — they don't correspond to any writable area — so those four
    // destinations must never render as clickable tabs.
    const allAreas = ['branch-settings', 'devices', 'staff', 'floor-map', 'branch-profile'];
    wrap(<ManagementWorkspace backend={null} session={session(supportPermissions(allAreas))} currencyCode="TJS" />);

    expect(screen.getByRole('button', { name: 'Клуб' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Залы и ПК' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'Сотрудники и роли' })).toBeTruthy();
    expect(screen.queryByRole('button', { name: 'Тарифы и пакеты' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Товары' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Платежи и лояльность' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Новости' })).toBeNull();
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

    await waitFor(() => expect(getLayoutZones).toHaveBeenCalledTimes(1));
    expect(getLayoutZones).toHaveBeenCalledWith('b1');
    expect(getTariffOptions).toHaveBeenCalledTimes(1);
    expect(getStaffUsers).toHaveBeenCalledTimes(1);
    expect(getCatalog).toHaveBeenCalledTimes(1);

    // Профиль филиала грузит ТОЛЬКО ClubDestination, не общий lift — иначе заход на «Клуб» даёт
    // двойной getBranchProfile. Здесь активны «Тарифы», Клуб не смонтирован → 0 вызовов.
    expect(getBranchProfile).not.toHaveBeenCalled();
    expect(getDiagnostics).not.toHaveBeenCalled();
    expect(getRolloutStatuses).not.toHaveBeenCalled();
  });

  it('does not turn a package load failure into an empty package catalog', async () => {
    getPackageOptions.mockImplementationOnce(async () => { throw new Error('packages offline'); });
    wrap(<ManagementWorkspace backend={backend as never} session={session([permissionNames.manageTariffs])} currencyCode="TJS" />);

    await waitFor(() => expect(getTariffOptions).toHaveBeenCalled());
    expect(screen.getByRole('tab', { name: 'Тарифы' })).toBeTruthy();
    fireEvent.click(screen.getByRole('tab', { name: 'Пакеты' }));
    expect(await screen.findByText('packages offline')).toBeTruthy();
    expect(screen.queryByText('Нет пакетов')).toBeNull();
  });
});
