import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import type { StaffUserDto } from '../../operatorApiClients';
import { StaffRolesDestination } from './StaffRolesDestination';

afterEach(() => cleanup());

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1', displayName: 'x' }) as never;

const staffUsers: StaffUserDto[] = [{
  staffUserId: 'u1',
  userName: 'operator1',
  displayName: 'Марина Сидорова',
  roleNames: ['cashier_operator'],
  isActive: true
} as never];

describe('StaffRolesDestination', () => {
  it('renders the ManagementScreen title and subtitle', () => {
    wrap(
      <StaffRolesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        staffUsers={[]}
      />
    );

    expect(screen.getByRole('heading', { name: 'Сотрудники и роли' })).toBeTruthy();
    expect(screen.getByText('Сотрудники, роли и доступ')).toBeTruthy();
  });

  it('renders a staff row for each provided staff user', () => {
    wrap(
      <StaffRolesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        staffUsers={staffUsers}
      />
    );

    expect(screen.getByText('Марина Сидорова')).toBeTruthy();
  });

  it('calls onDirtyChange(false) on mount since the section saves per-action', () => {
    const onDirtyChange = mock(() => {});
    wrap(
      <StaffRolesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        staffUsers={[]}
        onDirtyChange={onDirtyChange}
      />
    );
    expect(onDirtyChange).toHaveBeenCalledWith(false);
  });

  it('shows a loading skeleton instead of staff rows while loadStatus is loading', () => {
    const { container } = wrap(
      <StaffRolesDestination
        backend={null}
        session={session([])}
        currencyCode="TJS"
        staffUsers={staffUsers}
        loadStatus="loading"
      />
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
});
