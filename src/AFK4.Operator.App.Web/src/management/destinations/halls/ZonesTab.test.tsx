import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
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

const onReload = mock(async () => {});
const onFeedback = mock(() => {});

describe('ZonesTab', () => {
  it('renders the zone list with seat count and sort order', () => {
    wrap(<ZonesTab zones={zones} backend={null} canManageLayout={false} onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.getByText('Зал VIP')).toBeTruthy();
    expect(screen.getByText('1')).toBeTruthy(); // seat count
    expect(screen.getByText('10')).toBeTruthy(); // sort order
  });

  it('opens the drawer with the zone seats when a zone row is clicked', () => {
    wrap(<ZonesTab zones={zones} backend={null} canManageLayout={false} onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByText('Зал VIP'));
    expect(screen.getByText('PC-01')).toBeTruthy();
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
    wrap(<ZonesTab zones={zones} backend={backend as never} canManageLayout onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByRole('button', { name: 'Действия' }));
    fireEvent.click(screen.getByRole('menuitem', { name: 'Удалить зал' }));

    expect(deleteZone).not.toHaveBeenCalled();
    expect(screen.getByRole('alertdialog')).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить удаление зала' }));
    await waitFor(() => expect(deleteZone).toHaveBeenCalledWith('b1', 'z1', 'org'));
  });
});
