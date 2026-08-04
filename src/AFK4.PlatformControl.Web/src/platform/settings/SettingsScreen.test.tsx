import { describe, expect, it } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { SettingsScreen } from './SettingsScreen';

describe('SettingsScreen', () => {
  it('показывает сотрудников платформы списком', async () => {
    const client = {
      listAdmins: async () => [{
        platformAdminUserId: 'me', userName: 'root', displayName: 'Главный',
        role: 'platform_admin', isActive: true, twoFactorEnabled: true,
        lastSignInAtUtc: null, createdAtUtc: '2026-08-01T00:00:00Z'
      }],
      listInvitations: async () => []
    };

    render(
      <I18nProvider><ToastProvider>
        <SettingsScreen client={client as never} session={{ platformAdminId: 'me' } as never} />
      </ToastProvider></I18nProvider>
    );

    expect(await screen.findByText('Главный')).toBeInTheDocument();
  });
});
