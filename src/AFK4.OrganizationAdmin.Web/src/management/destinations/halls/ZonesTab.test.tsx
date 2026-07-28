import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import { permissionNames } from '../../../operatorPermissions';
import type { ZoneDto } from '../../../operatorApiClients';

const createZone = mock(async (_branchId: string, request: Record<string, unknown>) => ({ zoneId: 'z-new', ...request }));
const updateZone = mock(async () => ({ zoneId: 'z1' }));
const deleteZone = mock(async () => undefined);
const createSeat = mock(async () => ({ seatId: 's-new' }));
const updateSeat = mock(async () => ({ seatId: 's1' }));
const deleteSeat = mock(async () => undefined);

const actualHelpers = await import('../../../operatorHelpers');
mock.module('../../../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: { createZone, updateZone, deleteZone, createSeat, updateSeat, deleteSeat }
  })
}));

const { ZonesTab } = await import('./ZonesTab');

afterAll(() => {
  mock.module('../../../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../../../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  createZone.mockClear();
  updateZone.mockClear();
  deleteZone.mockClear();
  createSeat.mockClear();
  updateSeat.mockClear();
  deleteSeat.mockClear();
});

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [permissionNames.manageLayout] },
  branchId: 'b1'
};

const zones: ZoneDto[] = [{
  zoneId: 'z1',
  name: 'Зал VIP',
  sortOrder: 10,
  seats: [{ seatId: 's1', name: 'PC-01', sortOrder: 10 }]
} as never];

const twoZones: ZoneDto[] = [
  ...zones,
  { zoneId: 'z2', name: 'Общий зал', sortOrder: 20, seats: [] } as never
];

const onReload = mock(async () => {});
const onFeedback = mock(() => {});

// The zones list and the (now permanent) right-hand seat table both render an .mgmt-table — with
// auto-select active, the selected zone's name/⋯-menu appear in BOTH the list row and the drawer
// head at once, so tests that target the LIST specifically must scope to it, not `screen`.
const zonesTableOf = (container: HTMLElement) => container.querySelector('.mgmt-master-detail > .mgmt-table') as HTMLElement;

describe('ZonesTab', () => {
  it('renders the zone list with a seat-count badge', () => {
    const { container } = wrap(<ZonesTab zones={zones} backend={null} canManageLayout={false} onReload={onReload} onFeedback={onFeedback} />);
    const list = within(zonesTableOf(container));
    expect(list.getByText('Зал VIP')).toBeTruthy();
    expect(list.getByText('1 ПК')).toBeTruthy(); // seat-count badge
  });

  it('auto-selects the first zone on mount so the right panel shows its seats without a click', () => {
    wrap(<ZonesTab zones={zones} backend={null} canManageLayout={false} onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.getByText('PC-01')).toBeTruthy();
  });

  it('shows a create-first-zone empty state in the right panel when there are no zones', () => {
    wrap(<ZonesTab zones={[]} backend={null} canManageLayout onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.getByText('Создайте первый зал')).toBeTruthy();
  });

  it('switches the right panel to another zone on click', () => {
    wrap(<ZonesTab zones={twoZones} backend={null} canManageLayout={false} onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.getByText('PC-01')).toBeTruthy(); // first zone auto-selected
    fireEvent.click(screen.getByText('Общий зал'));
    expect(screen.queryByText('PC-01')).toBeNull();
    expect(screen.getByText('В этом зале нет ПК')).toBeTruthy();
  });

  it('hides the "+ Зал" primary action and row menu without canManageLayout', () => {
    wrap(<ZonesTab zones={zones} backend={null} canManageLayout={false} onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.queryByRole('button', { name: '+ Зал' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Действия' })).toBeNull();
  });

  it('"+ Зал" opens the create-zone modal', () => {
    wrap(<ZonesTab zones={zones} backend={null} canManageLayout onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByRole('button', { name: '+ Зал' }));
    expect(screen.getByRole('dialog', { name: 'Новый зал' })).toBeTruthy();
  });

  it('submits the create-zone form and calls settings.createZone with the org/branch payload', async () => {
    wrap(<ZonesTab zones={zones} backend={backend as never} canManageLayout onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByRole('button', { name: '+ Зал' }));
    fireEvent.click(screen.getByRole('button', { name: 'Создать зал' }));

    await waitFor(() => expect(createZone).toHaveBeenCalledTimes(1));
    expect(createZone).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org' }));
    await waitFor(() => expect(onReload).toHaveBeenCalled());
  });

  it('deleting a zone via the row menu asks for confirmation before calling settings.deleteZone', async () => {
    const { container } = wrap(<ZonesTab zones={zones} backend={backend as never} canManageLayout onReload={onReload} onFeedback={onFeedback} />);
    // The zone is auto-selected, so its permanent detail panel ALSO shows a "Действия" ⋯-menu —
    // scope to the list row's menu specifically, not the drawer head's.
    fireEvent.click(within(zonesTableOf(container)).getByRole('button', { name: 'Действия' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Удалить зал' }));

    expect(deleteZone).not.toHaveBeenCalled();
    expect(screen.getByRole('alertdialog')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить удаление зала' }));
    await waitFor(() => expect(deleteZone).toHaveBeenCalledWith('b1', 'z1', 'org'));
  });

  it('the right-side zone detail panel has no close button — it is a permanent panel, not a closable drawer', () => {
    wrap(<ZonesTab zones={zones} backend={null} canManageLayout onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.queryByRole('button', { name: 'Закрыть' })).toBeNull();
  });
});
