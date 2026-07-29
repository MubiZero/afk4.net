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

function setup() {
  const updates = {
    listPackages: mock().mockResolvedValue([packageRow]),
    listRollouts: mock().mockResolvedValue([]),
    registerPackage: mock(),
    changePackageState: mock().mockResolvedValue({ ...packageRow, state: 'validated' }),
    createRollout: mock(),
    changeRolloutState: mock()
  };
  render(<I18nProvider><ToastProvider><UpdatesScreen client={updates as never} /></ToastProvider></I18nProvider>);
  return updates;
}

describe('UpdatesScreen', () => {
  it('shows the global package catalog and validates a registered package with a reason', async () => {
    const client = setup();
    await screen.findByText('Organization Admin');
    fireEvent.click(screen.getByRole('button', { name: 'Проверить пакет' }));
    fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'Подпись и хеш проверены.' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить проверку' }));
    await waitFor(() => expect(client.changePackageState).toHaveBeenCalledWith('p1', 'validated', 'Подпись и хеш проверены.'));
  });

  it('keeps rollouts separate from the package catalog', async () => {
    setup();
    await screen.findByText('Organization Admin');
    const tab = screen.getByRole('tab', { name: 'Rollout' });
    fireEvent.mouseDown(tab);
    fireEvent.click(tab);
    expect(await screen.findByText('Rollout ещё не запускались.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Запустить rollout' })).toBeInTheDocument();
  });
});
