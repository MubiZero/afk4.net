import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { ReactElement } from 'react';
import type { UpdateRolloutStatusDto } from '../api/clients/updates';
import { OrganizationAdminUpdateCard } from './OrganizationAdminUpdateCard';

const preference = {
  organizationId: 'org-1', branchId: 'branch-1', maintenanceWindowStart: '04:00:00',
  maintenanceWindowEnd: '05:00:00', timeZone: 'Asia/Dushanbe'
};

describe('OrganizationAdminUpdateCard', () => {
  afterEach(cleanup);

  it('shows an explicit empty state', () => {
    renderWithI18n(<OrganizationAdminUpdateCard rollouts={[]} preference={preference} canManagePreference={false} onSavePreference={mock()} onRestart={mock()} />);
    expect(screen.getByTestId('admin-update-empty')).toBeTruthy();
  });

  it('shows installed and offered versions with safe deferred detail', () => {
    renderWithI18n(<OrganizationAdminUpdateCard rollouts={[rollout('deferred', 'Waiting for maintenance window.')]} preference={preference} canManagePreference onSavePreference={mock()} onRestart={mock()} />);
    expect(screen.getByText('1.2.2')).toBeTruthy();
    expect(screen.getByText('1.2.3')).toBeTruthy();
    expect(screen.getByText('Waiting for maintenance window.')).toBeTruthy();
  });

  it('keeps preference controls disabled without branch-settings permission', () => {
    renderWithI18n(<OrganizationAdminUpdateCard rollouts={[rollout('offered')]} preference={preference} canManagePreference={false} onSavePreference={mock()} onRestart={mock()} />);
    expect(screen.getByTestId('maintenance-start')).toBeDisabled();
    expect(screen.getByTestId('maintenance-save')).toBeDisabled();
    expect(screen.queryByTestId('restart-and-update')).toBeNull();
  });

  it('saves a changed maintenance window', () => {
    const onSave = mock();
    renderWithI18n(<OrganizationAdminUpdateCard rollouts={[rollout('offered')]} preference={preference} canManagePreference onSavePreference={onSave} onRestart={mock()} />);
    fireEvent.change(screen.getByTestId('maintenance-start'), { target: { value: '03:30' } });
    fireEvent.click(screen.getByTestId('maintenance-save'));
    expect(onSave).toHaveBeenCalledWith('03:30', '05:00');
  });

  it('binds restart to the offered rollout and package and Later dismisses the action', () => {
    const onRestart = mock();
    renderWithI18n(<OrganizationAdminUpdateCard rollouts={[rollout('deferred')]} preference={preference} canManagePreference onSavePreference={mock()} onRestart={onRestart} />);
    fireEvent.click(screen.getByTestId('restart-and-update'));
    expect(onRestart).toHaveBeenCalledWith('rollout-1', 'package-1');
    fireEvent.click(screen.getByTestId('update-later'));
    expect(screen.queryByTestId('restart-and-update')).toBeNull();
  });

  it('keeps update actions as keyboard-focusable native buttons', () => {
    renderWithI18n(<OrganizationAdminUpdateCard rollouts={[rollout('offered')]} preference={preference} canManagePreference onSavePreference={mock()} onRestart={mock()} />);
    const restart = screen.getByRole('button', { name: 'Перезапустить и обновить' });
    restart.focus();
    expect(document.activeElement).toBe(restart);
    expect(restart.tagName).toBe('BUTTON');
  });

  it('does not expose restart while installation is in progress or failed', () => {
    const { rerender } = renderWithI18n(<OrganizationAdminUpdateCard rollouts={[rollout('installing')]} preference={preference} canManagePreference onSavePreference={mock()} onRestart={mock()} />);
    expect(screen.queryByTestId('restart-and-update')).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><OrganizationAdminUpdateCard rollouts={[rollout('failed', 'Installer returned code 1.')]} preference={preference} canManagePreference onSavePreference={mock()} onRestart={mock()} /></I18nProvider>);
    expect(screen.getByText('Installer returned code 1.')).toBeTruthy();
    expect(screen.queryByTestId('restart-and-update')).toBeNull();
  });

  it('replaces installer internals with a safe failure detail', () => {
    renderWithI18n(<OrganizationAdminUpdateCard rollouts={[rollout('failed', 'Download https://objects.example/admin.msi signature mismatch')]} preference={preference} canManagePreference onSavePreference={mock()} onRestart={mock()} />);
    expect(screen.getByRole('alert').textContent).toContain('Подробности установки скрыты');
    expect(screen.queryByText(/objects\.example/)).toBeNull();
  });
});

function renderWithI18n(element: ReactElement) {
  return render(<I18nProvider initialLocale="ru">{element}</I18nProvider>);
}

function rollout(status: string, message = 'Ready.'): UpdateRolloutStatusDto {
  return {
    updateRolloutId: 'rollout-1', updatePackageId: 'package-1', organizationId: 'org-1', branchId: 'branch-1',
    component: 'organization-admin', version: '1.2.3', channel: 'stable', state: 'active', startsAtUtc: '2026-07-29T00:00:00Z',
    deviceStatuses: [{ deviceId: 'device-1', updateRolloutId: 'rollout-1', updatePackageId: 'package-1', component: 'organization-admin', installedVersion: '1.2.2', targetVersion: '1.2.3', status, message, updatedAtUtc: '2026-07-29T01:00:00Z' }]
  };
}
