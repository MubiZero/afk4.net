import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import { permissionNames } from '../../../operatorPermissions';
import type { PackageOptionDto } from '../../../operatorApiClients';

const createPackageDefinition = mock(async () => ({ packageDefinitionId: 'pkg-new' }));
const updatePackageDefinition = mock(async () => ({ packageDefinitionId: 'p1' }));

const actualHelpers = await import('../../../operatorHelpers');
mock.module('../../../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: { createPackageDefinition, updatePackageDefinition }
  })
}));

const { PackagesTab } = await import('./PackagesTab');

afterAll(() => {
  mock.module('../../../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../../../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  createPackageDefinition.mockClear();
  updatePackageDefinition.mockClear();
});

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [permissionNames.managePackages] },
  branchId: 'b1'
};

const packageDefinitionId = '33333333-3333-3333-3333-333333333333';

const packageOptions: PackageOptionDto[] = [{
  packageDefinitionId,
  name: 'Ночной пакет',
  priceMinorUnits: 25000, // 250.00 TJS
  includedSeconds: 300 * 60, // 300 minutes
  bonusSeconds: 30 * 60, // 30 bonus minutes
  expiresAfterDays: 30,
  isActive: true,
  currencyCode: 'TJS'
} as never];

const onReload = mock(async () => {});
const onFeedback = mock(() => {});

describe('PackagesTab', () => {
  it('renders the package list and the price without an amber/warning class — money is neutral', () => {
    const { container } = wrap(
      <PackagesTab packageOptions={packageOptions} currencyCode="TJS" backend={null} canManagePackages={false} onReload={onReload} onFeedback={onFeedback} />
    );
    expect(screen.getByText('Ночной пакет')).toBeTruthy();
    const priceEl = container.querySelector('.ui-money');
    expect(priceEl).toBeTruthy();
    expect(priceEl!.textContent).toContain('250');
    expect(priceEl!.className).toBe('ui-money');
  });

  it('hides the "+ Пакет" primary action and row menu without canManagePackages', () => {
    wrap(<PackagesTab packageOptions={packageOptions} currencyCode="TJS" backend={null} canManagePackages={false} onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.queryByRole('button', { name: '+ Пакет' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Действия' })).toBeNull();
  });

  it('shows a create-first-package empty state when there are no packages', () => {
    wrap(<PackagesTab packageOptions={[]} currencyCode="TJS" backend={null} canManagePackages onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.getByText('Нет пакетов')).toBeTruthy();
  });

  it('clicking a row opens the drawer with the package form', () => {
    wrap(<PackagesTab packageOptions={packageOptions} currencyCode="TJS" backend={null} canManagePackages={false} onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByText('Ночной пакет'));
    expect(screen.getByRole('textbox', { name: 'Название пакета' })).toHaveValue('Ночной пакет');
  });

  it('"+ Пакет" opens the create modal and submits createPackageDefinition', async () => {
    wrap(<PackagesTab packageOptions={packageOptions} currencyCode="TJS" backend={backend as never} canManagePackages onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByRole('button', { name: '+ Пакет' }));
    expect(screen.getByRole('dialog', { name: 'Новый пакет' })).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Создать пакет' }));

    await waitFor(() => expect(createPackageDefinition).toHaveBeenCalledTimes(1));
    expect(createPackageDefinition).toHaveBeenCalledWith('b1', expect.objectContaining({
      organizationId: 'org',
      price: { currencyCode: 'TJS', minorUnits: 25000 },
      includedSeconds: 300 * 60,
      bonusSeconds: 30 * 60,
      expiresAfterDays: 30
    }));
    await waitFor(() => expect(onReload).toHaveBeenCalled());
  });

  it('saves an edit from the drawer form via updatePackageDefinition', async () => {
    wrap(<PackagesTab packageOptions={packageOptions} currencyCode="TJS" backend={backend as never} canManagePackages onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByText('Ночной пакет'));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updatePackageDefinition).toHaveBeenCalledWith('b1', packageDefinitionId, expect.objectContaining({
      organizationId: 'org',
      name: 'Ночной пакет',
      isActive: true
    })));
  });

  it('retiring a package via the row menu asks for confirmation before calling updatePackageDefinition with isActive:false', async () => {
    const { container } = wrap(
      <PackagesTab packageOptions={packageOptions} currencyCode="TJS" backend={backend as never} canManagePackages onReload={onReload} onFeedback={onFeedback} />
    );
    fireEvent.click(within(container).getAllByRole('button', { name: 'Действия' })[0]);
    fireEvent.click(screen.getByRole('menuitem', { name: 'Снять пакет' }));

    expect(updatePackageDefinition).not.toHaveBeenCalled();
    expect(screen.getByRole('alertdialog')).toBeTruthy();

    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Снять пакет' }));
    await waitFor(() => expect(updatePackageDefinition).toHaveBeenCalledWith('b1', packageDefinitionId, expect.objectContaining({ isActive: false })));
  });
});
