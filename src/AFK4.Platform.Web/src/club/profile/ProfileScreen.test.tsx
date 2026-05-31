import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import type { StaffSession } from '@/auth/staffTokenStore';
import { ProfileScreen } from './ProfileScreen';

const session = {
  staffUserId: 'u1', organizationId: 'org-1', displayName: 'Иван', branchIds: ['b1'],
  permissions: ['players.view', 'billing.refund'],
  accessToken: 'a', accessTokenExpiresAtUtc: '', refreshToken: 'r', refreshTokenExpiresAtUtc: ''
} as StaffSession;

it('shows identity, permissions, and signs out', () => {
  const onSignOut = mock();
  render(
    <I18nProvider>
      <ProfileScreen session={session} branches={[{ branchId: 'b1', name: 'Центр' }]} roleLabel="Владелец" onSignOut={onSignOut} />
    </I18nProvider>
  );
  expect(screen.getByText('Иван')).toBeInTheDocument();
  expect(screen.getByText('Владелец')).toBeInTheDocument();
  expect(screen.getByText('Центр')).toBeInTheDocument();
  expect(screen.getByText('players.view')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Выйти' }));
  expect(onSignOut).toHaveBeenCalled();
});
