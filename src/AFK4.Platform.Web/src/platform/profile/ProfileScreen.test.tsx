import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { PlatformAdminSession } from '@/auth/tokenStore';
import { ProfileScreen } from './ProfileScreen';

const session = {
  platformAdminId: 'pa-1', userName: 'admin@afk4.io', displayName: 'Админ',
  roles: ['platform_admin'], permissions: ['tenants.read', 'billing.invoice.void'],
  accessToken: 'a', accessTokenExpiresAtUtc: '', refreshToken: 'r', refreshTokenExpiresAtUtc: ''
} as PlatformAdminSession;

it('shows identity, roles, permissions, and signs out', () => {
  const onSignOut = vi.fn();
  render(
    <I18nProvider>
      <ProfileScreen session={session} onSignOut={onSignOut} />
    </I18nProvider>
  );
  expect(screen.getByText('Админ')).toBeInTheDocument();
  expect(screen.getByText('admin@afk4.io')).toBeInTheDocument();
  expect(screen.getByText('platform_admin')).toBeInTheDocument();
  expect(screen.getByText('tenants.read')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Выйти' }));
  expect(onSignOut).toHaveBeenCalled();
});
