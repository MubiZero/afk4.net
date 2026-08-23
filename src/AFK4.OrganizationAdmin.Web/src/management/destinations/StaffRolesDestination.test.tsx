import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import { permissionNames } from '../../operatorPermissions';
import type { StaffUserDto } from '../../operatorApiClients';

const createStaffInvite = mock(async () => ({ staffInviteId: 'inv-1', code: 'ABCD-1234', expiresAtUtc: '2026-01-08T00:00:00Z' }));
const updateStaffUserProfile = mock(async () => ({ staffUserId: 'u1', userName: 'operator1x', displayName: 'Марина Сидорова', roleNames: ['operator'], isActive: true }));
const updateStaffUserRoles = mock(async () => ({ staffUserId: 'u1', userName: 'operator1', displayName: 'Марина Сидорова', roleNames: ['shift_supervisor'], isActive: true }));
const updateStaffUserState = mock(async (_branchId: string, _staffUserId: string, request: Record<string, unknown>) => ({
  staffUserId: 'u1',
  userName: 'operator1',
  displayName: 'Марина Сидорова',
  roleNames: ['operator'],
  isActive: request.isActive
}));
const resetStaffUserPassword = mock(async () => ({ staffUserId: 'u1', userName: 'operator1', displayName: 'Марина Сидорова', roleNames: ['operator'], isActive: true }));

const actualHelpers = await import('../../operatorHelpers');
mock.module('../../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: { createStaffInvite, updateStaffUserProfile, updateStaffUserRoles, updateStaffUserState, resetStaffUserPassword }
  })
}));

const { StaffRolesDestination } = await import('./StaffRolesDestination');

afterAll(() => {
  mock.module('../../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  createStaffInvite.mockClear();
  updateStaffUserProfile.mockClear();
  updateStaffUserRoles.mockClear();
  updateStaffUserState.mockClear();
  resetStaffUserPassword.mockClear();
});

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[], staffUserId?: string, isSupportSession?: boolean) => ({ permissions: perms, organizationId: 'org', displayName: 'x', staffUserId, isSupportSession }) as never;

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [permissionNames.manageBranchStaff, permissionNames.manageRoles] },
  branchId: 'b1'
};

const staffUserId = '11111111-1111-1111-1111-111111111111';

const staffUsers: StaffUserDto[] = [{
  staffUserId,
  userName: 'operator1',
  displayName: 'Марина Сидорова',
  roleNames: ['operator'],
  isActive: true
} as never];

describe('StaffRolesDestination', () => {
  it('renders the ManagementScreen title and subtitle', () => {
    wrap(<StaffRolesDestination backend={null} session={session([])} currencyCode="TJS" staffUsers={[]} />);
    expect(screen.getByRole('heading', { name: 'Сотрудники и роли' })).toBeTruthy();
    expect(screen.getByText('Сотрудники, роли и доступ')).toBeTruthy();
  });

  it('renders a staff row with login, role and status columns', () => {
    wrap(<StaffRolesDestination backend={null} session={session([])} currencyCode="TJS" staffUsers={staffUsers} />);
    expect(screen.getByText('Марина Сидорова')).toBeTruthy();
    expect(screen.getByText('operator1')).toBeTruthy();
    expect(screen.getByText('Оператор')).toBeTruthy();
    expect(screen.getByText('активен')).toBeTruthy();
  });

  it('calls onDirtyChange(false) on mount since the section saves per-action', () => {
    const onDirtyChange = mock(() => {});
    wrap(<StaffRolesDestination backend={null} session={session([])} currencyCode="TJS" staffUsers={[]} onDirtyChange={onDirtyChange} />);
    expect(onDirtyChange).toHaveBeenCalledWith(false);
  });

  it('shows a loading skeleton instead of staff rows while loadStatus is loading', () => {
    const { container } = wrap(
      <StaffRolesDestination backend={null} session={session([])} currencyCode="TJS" staffUsers={staffUsers} loadStatus="loading" />
    );
    expect(container.querySelector('.management-skeleton')).toBeTruthy();
    expect(screen.queryByText('Марина Сидорова')).toBeNull();
  });

  it('shows the concrete error detail and retries via onRetry when loadStatus is failed', () => {
    const onRetry = mock(() => {});
    wrap(
      <StaffRolesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        staffUsers={staffUsers}
        loadStatus="failed"
        errorDetail="boom"
        onRetry={onRetry}
      />
    );
    expect(screen.getByText('boom')).toBeTruthy();
    fireEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('shows a create-first-staff empty state when there are no staff members', () => {
    wrap(<StaffRolesDestination backend={null} session={session([])} currencyCode="TJS" staffUsers={[]} />);
    expect(screen.getByText('Нет сотрудников')).toBeTruthy();
  });

  it('hides the "+ Сотрудник" primary action and row menu without canManageBranchStaff', () => {
    wrap(<StaffRolesDestination backend={null} session={session([])} currencyCode="TJS" staffUsers={staffUsers} />);
    expect(screen.queryByRole('button', { name: '+ Сотрудник' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Действия' })).toBeNull();
  });

  it('hides the "+ Сотрудник" button during a support session, even with canManageBranchStaff', () => {
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff], undefined, true)}
        currencyCode="TJS"
        staffUsers={staffUsers}
      />
    );
    expect(screen.queryByRole('button', { name: '+ Сотрудник' })).toBeNull();
  });

  it('shows the "+ Сотрудник" button for a regular staff member with canManageBranchStaff', () => {
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff], undefined, false)}
        currencyCode="TJS"
        staffUsers={staffUsers}
      />
    );
    expect(screen.getByRole('button', { name: '+ Сотрудник' })).toBeTruthy();
  });

  it('clicking a row opens the drawer with profile, role and access sections', () => {
    wrap(<StaffRolesDestination backend={null} session={session([])} currencyCode="TJS" staffUsers={staffUsers} />);
    fireEvent.click(screen.getByText('Марина Сидорова'));
    const drawer = screen.getByRole('complementary');
    expect(within(drawer).getByText('Профиль')).toBeTruthy();
    expect(within(drawer).getByText('Роль')).toBeTruthy();
    expect(within(drawer).getByText('Доступ')).toBeTruthy();
    expect(screen.getByRole('textbox', { name: 'Логин профиля' })).toHaveValue('operator1');
  });

  it('"+ Сотрудник" sends every selected role and shows the invite code', async () => {
    const onFeedback = mock(() => {});
    wrap(<StaffRolesDestination backend={backend as never} session={session([permissionNames.manageBranchStaff])} currencyCode="TJS" staffUsers={staffUsers} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByRole('button', { name: '+ Сотрудник' }));
    expect(screen.getByRole('dialog', { name: 'Пригласить сотрудника' })).toBeTruthy();

    fireEvent.change(screen.getByRole('textbox', { name: 'Логин для входа' }), { target: { value: 'operator2' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Имя в смене' }), { target: { value: 'Новый сотрудник' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Телефон для приглашения' }), { target: { value: '+992937380070' } });
    fireEvent.change(screen.getByRole('textbox', { name: 'Email (необязательно)' }), { target: { value: 'new@club.tj' } });
    fireEvent.click(screen.getByRole('checkbox', { name: 'Оператор' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Управляющий' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Техник' }));
    fireEvent.click(screen.getByRole('button', { name: 'Пригласить сотрудника' }));

    await waitFor(() => expect(createStaffInvite).toHaveBeenCalledTimes(1));
    expect(createStaffInvite).toHaveBeenCalledWith('b1', expect.objectContaining({
      organizationId: 'org',
      userName: 'operator2',
      displayName: 'Новый сотрудник',
      phoneNumber: '+992937380070',
      email: 'new@club.tj',
      roleNames: ['branch_manager', 'technician']
    }));
    expect(screen.getByDisplayValue('ABCD-1234')).toBeTruthy();
  });

  it('saves a profile edit from the drawer via updateStaffUserProfile and merges the result', async () => {
    const onStaffUsersChange = mock(() => {});
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff])}
        currencyCode="TJS"
        staffUsers={staffUsers}
        onStaffUsersChange={onStaffUsersChange}
      />
    );
    fireEvent.click(screen.getByText('Марина Сидорова'));
    fireEvent.change(screen.getByRole('textbox', { name: 'Логин профиля' }), { target: { value: 'operator1x' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateStaffUserProfile).toHaveBeenCalledWith('b1', staffUserId, expect.objectContaining({
      organizationId: 'org',
      userName: 'operator1x'
    })));
    await waitFor(() => expect(onStaffUsersChange).toHaveBeenCalled());
  });

  it('changes the complete role set via updateStaffUserRoles', async () => {
    const multiRoleStaffUsers: StaffUserDto[] = [{ ...staffUsers[0], roleNames: ['operator', 'branch_manager'] } as never];
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff, permissionNames.manageRoles])}
        currencyCode="TJS"
        staffUsers={multiRoleStaffUsers}
      />
    );
    fireEvent.click(screen.getByText('Марина Сидорова'));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Оператор' }));
    fireEvent.click(screen.getByRole('checkbox', { name: 'Техник' }));
    fireEvent.click(screen.getByRole('button', { name: 'Обновить роль' }));

    await waitFor(() => expect(updateStaffUserRoles).toHaveBeenCalledWith('b1', staffUserId, {
      organizationId: 'org',
      roleNames: ['branch_manager', 'technician']
    }));
  });

  it('disables the role section without canManageRoles', () => {
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff])}
        currencyCode="TJS"
        staffUsers={staffUsers}
      />
    );
    fireEvent.click(screen.getByText('Марина Сидорова'));
    expect(screen.getByRole('checkbox', { name: 'Оператор' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Обновить роль' })).toBeDisabled();
  });

  it('does not offer access deactivation for the authenticated staff user', () => {
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff], staffUserId)}
        currencyCode="TJS"
        staffUsers={staffUsers}
      />
    );

    fireEvent.click(screen.getByText('Марина Сидорова'));

    expect(screen.queryByRole('button', { name: 'Отключить сотрудника' })).toBeNull();
  });

  it('disabling access asks for confirmation before calling updateStaffUserState with isActive:false', async () => {
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff])}
        currencyCode="TJS"
        staffUsers={staffUsers}
      />
    );
    fireEvent.click(screen.getByText('Марина Сидорова'));
    fireEvent.click(screen.getByRole('button', { name: 'Отключить сотрудника' }));

    expect(updateStaffUserState).not.toHaveBeenCalled();
    expect(screen.getByRole('alertdialog')).toBeTruthy();

    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Отключить сотрудника' }));
    await waitFor(() => expect(updateStaffUserState).toHaveBeenCalledWith('b1', staffUserId, expect.objectContaining({ isActive: false })));
  });

  it('re-enabling access calls updateStaffUserState with isActive:true directly, without a confirm dialog', async () => {
    const disabledStaffUsers: StaffUserDto[] = [{ ...staffUsers[0], isActive: false } as never];
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff])}
        currencyCode="TJS"
        staffUsers={disabledStaffUsers}
      />
    );
    fireEvent.click(screen.getByText('Марина Сидорова'));
    fireEvent.click(screen.getByRole('button', { name: 'Включить сотрудника' }));

    expect(screen.queryByRole('alertdialog')).toBeNull();
    await waitFor(() => expect(updateStaffUserState).toHaveBeenCalledWith('b1', staffUserId, expect.objectContaining({ isActive: true })));
  });

  it('rejects a reset password shorter than 8 characters without opening a confirm dialog', () => {
    const onFeedback = mock(() => {});
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff])}
        currencyCode="TJS"
        staffUsers={staffUsers}
        onFeedback={onFeedback}
      />
    );
    fireEvent.click(screen.getByText('Марина Сидорова'));
    fireEvent.change(screen.getByLabelText('Новый пароль для входа'), { target: { value: 'short' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сбросить пароль' }));

    expect(screen.queryByRole('alertdialog')).toBeNull();
    expect(resetStaffUserPassword).not.toHaveBeenCalled();
    expect(onFeedback).toHaveBeenCalledWith(expect.objectContaining({ state: 'failed' }));
  });

  it('resetting a password of valid length asks for confirmation before calling resetStaffUserPassword', async () => {
    wrap(
      <StaffRolesDestination
        backend={backend as never}
        session={session([permissionNames.manageBranchStaff])}
        currencyCode="TJS"
        staffUsers={staffUsers}
      />
    );
    fireEvent.click(screen.getByText('Марина Сидорова'));
    fireEvent.change(screen.getByLabelText('Новый пароль для входа'), { target: { value: 'longenoughpwd' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сбросить пароль' }));

    expect(resetStaffUserPassword).not.toHaveBeenCalled();
    expect(screen.getByRole('alertdialog')).toBeTruthy();

    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Сбросить пароль' }));
    await waitFor(() => expect(resetStaffUserPassword).toHaveBeenCalledWith('b1', staffUserId, {
      organizationId: 'org',
      newPassword: 'longenoughpwd'
    }));
  });
});
