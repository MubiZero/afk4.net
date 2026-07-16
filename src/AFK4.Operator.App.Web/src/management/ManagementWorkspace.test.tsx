import { describe, it, expect } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import { ManagementWorkspace } from './ManagementWorkspace';
import { permissionNames } from '../operatorPermissions';

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1', displayName: 'x' }) as never;

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
});
