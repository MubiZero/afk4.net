import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { UpdatesScreen } from './UpdatesScreen';

const packageRow = {
  updatePackageId: 'p1', component: 'organization_admin', version: '1.4.0', channel: 'stable',
  artifactUri: 'https://updates.afk4.net/admin.msi', sha256: 'a'.repeat(64), signature: 'sig',
  signatureAlgorithm: 'ecdsa-p256-sha256-ieee-p1363', sizeBytes: 1024, state: 'registered',
  releaseNotes: 'Safe release.', createdByPlatformAdminUserId: 'a1', createdAtUtc: '2026-07-29T10:00:00Z',
  validatedByPlatformAdminUserId: null, validatedAtUtc: null, retiredAtUtc: null
};

function setup(state: string = 'registered') {
  const updates = {
    listPackages: mock().mockResolvedValue([{ ...packageRow, state }]),
    listRollouts: mock().mockResolvedValue([]),
    registerPackage: mock(),
    changePackageState: mock().mockResolvedValue({ ...packageRow, state: 'validated' }),
    createRollout: mock().mockResolvedValue({})
  };
  const organizations = {
    listOrganizations: mock().mockResolvedValue([
      { organizationId: 'org-1' },
      { organizationId: 'org-2' }
    ])
  };
  render(
    <I18nProvider><ToastProvider>
      <UpdatesScreen client={updates as never} organizationsClient={organizations as never} />
    </ToastProvider></I18nProvider>
  );
  return { updates, organizations };
}

describe('UpdatesScreen', () => {
  it('shows the global package catalog and validates a registered package with a reason', async () => {
    const { updates } = setup();
    await screen.findByText('Organization Admin');
    fireEvent.click(screen.getByRole('button', { name: 'Проверить пакет' }));
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'Подпись и хеш проверены.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить проверку' }));
    await waitFor(() => expect(updates.changePackageState).toHaveBeenCalledWith('p1', 'validated', 'Подпись и хеш проверены.'));
  });

  // Публикация обязана дойти до КАЖДОГО клиента: если этот тест ослабить до «вызвали
  // createRollout», обратно вернётся поэтапная выкатка на часть парка, а половина клубов
  // молча останется на старой версии.
  it('publishes a validated package to every organization at once', async () => {
    const { updates } = setup('validated');
    await screen.findByText('Organization Admin');
    fireEvent.click(screen.getByRole('button', { name: 'Опубликовать' }));
    fireEvent.click(screen.getAllByRole('button', { name: 'Опубликовать' })[1]);
    await waitFor(() => expect(updates.createRollout).toHaveBeenCalled());
    const request = updates.createRollout.mock.calls[0][0];
    expect(request.targetKind).toBe('organization');
    expect(request.organizationIds).toEqual(['org-1', 'org-2']);
    expect(request.batchPercent).toBe(100);
  });

  it('offers no staged rollout controls', async () => {
    setup('validated');
    await screen.findByText('Organization Admin');
    expect(screen.queryByRole('button', { name: 'Запустить rollout' })).toBeNull();
    expect(screen.queryByLabelText('Размер партии, %')).toBeNull();
  });
});
