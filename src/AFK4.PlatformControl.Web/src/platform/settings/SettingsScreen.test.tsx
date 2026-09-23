import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
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
    const twoFactorClient = { reset: async () => {} };
    const rolesClient = {
      listRoles: async () => [],
      listPermissions: async () => [],
      createRole: async () => { throw new Error('not used'); },
      updateRole: async () => { throw new Error('not used'); },
      deleteRole: async () => {}
    };

    render(
      <I18nProvider><ToastProvider>
        <SettingsScreen client={client as never} twoFactorClient={twoFactorClient} rolesClient={rolesClient as never} session={{ platformAdminId: 'me' } as never} />
      </ToastProvider></I18nProvider>
    );

    expect(await screen.findByText('Главный')).toBeInTheDocument();
  });

  // Отключить коллеге доступ ко всей платформе — не меньший вес, чем приостановить клуб или
  // отозвать приглашение: те давно спрашивают подтверждение, а это срабатывало одним кликом.
  it('отключение администратора спрашивает подтверждение', async () => {
    const updateAdmin = mock().mockResolvedValue({});
    renderSettings({ updateAdmin });
    await screen.findByText('Второй');

    await userEvent.click(screen.getAllByRole('button', { name: 'Отключить' }).find(button => !button.hasAttribute('disabled'))!);

    expect(updateAdmin).not.toHaveBeenCalled();
    expect(await screen.findByText('Отключить доступ к платформе?')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));

    await waitFor(() => expect(updateAdmin).toHaveBeenCalledTimes(1));
    expect((updateAdmin.mock.calls[0] as unknown[])[1]).toEqual({ isActive: false });
  });

  it('отказ от подтверждения ничего не меняет', async () => {
    const updateAdmin = mock().mockResolvedValue({});
    renderSettings({ updateAdmin });
    await screen.findByText('Второй');

    await userEvent.click(screen.getAllByRole('button', { name: 'Отключить' }).find(button => !button.hasAttribute('disabled'))!);
    await userEvent.click(screen.getByRole('button', { name: 'Отмена' }));

    expect(updateAdmin).not.toHaveBeenCalled();
  });

  // Смена роли меняет набор прав сразу — человек должен видеть, какую роль он выдаёт.
  it('смена роли спрашивает подтверждение и называет новую роль', async () => {
    const updateAdmin = mock().mockResolvedValue({});
    renderSettings({ updateAdmin });
    await screen.findByText('Второй');

    await userEvent.click(screen.getAllByRole('button', { name: 'Сделать поддержкой' }).find(button => !button.hasAttribute('disabled'))!);

    expect(await screen.findByText(/Второй получит роль/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));

    await waitFor(() => expect(updateAdmin).toHaveBeenCalledTimes(1));
    expect((updateAdmin.mock.calls[0] as unknown[])[1]).toEqual({ role: 'platform_support' });
  });

  // Список сотрудников и приглашения — разные запросы. Отказ второго не должен прятать первый:
  // людей платформы видно и ими можно управлять, даже когда приглашения не пришли.
  it('отказ приглашений не прячет сотрудников и повторяет только приглашения', async () => {
    const listAdmins = mock(async () => ADMINS);
    const listInvitations = mock()
      .mockRejectedValueOnce(new PlatformApiError(500, 'boom'))
      .mockResolvedValue([]);
    renderSettings({ listAdmins, listInvitations });

    expect(await screen.findByText('Второй')).toBeInTheDocument();
    expect(screen.getByText('Не удалось загрузить приглашения')).toBeInTheDocument();
    expect(screen.getByText('Сервер платформы вернул ошибку. Повторите позже.')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));

    await waitFor(() => expect(screen.queryByText('Не удалось загрузить приглашения')).not.toBeInTheDocument());
    expect(listInvitations).toHaveBeenCalledTimes(2);
    expect(listAdmins).toHaveBeenCalledTimes(1);
    expect(screen.getByText('Второй')).toBeInTheDocument();
  });

  it('отказ приглашений по правам называет причину без кнопки повтора', async () => {
    renderSettings({ listInvitations: mock().mockRejectedValue(new PlatformApiError(403, 'Forbidden')) });

    expect(await screen.findByText('Второй')).toBeInTheDocument();
    expect(await screen.findByText('Недостаточно прав для этого действия.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Повторить' })).not.toBeInTheDocument();
  });
});

const ADMINS = [
  {
    platformAdminUserId: 'me', userName: 'root', displayName: 'Главный',
    role: 'platform_admin', isActive: true, twoFactorEnabled: true,
    lastSignInAtUtc: null, createdAtUtc: '2026-08-01T00:00:00Z'
  },
  {
    platformAdminUserId: 'other', userName: 'second', displayName: 'Второй',
    role: 'platform_admin', isActive: true, twoFactorEnabled: false,
    lastSignInAtUtc: null, createdAtUtc: '2026-08-01T00:00:00Z'
  }
];

function renderSettings(overrides: {
  updateAdmin?: ReturnType<typeof mock>;
  listAdmins?: () => Promise<unknown>;
  listInvitations?: () => Promise<unknown>;
} = {}) {
  const client = {
    listAdmins: overrides.listAdmins ?? (async () => ADMINS),
    listInvitations: overrides.listInvitations ?? (async () => []),
    updateAdmin: overrides.updateAdmin ?? mock().mockResolvedValue({})
  };
  const rolesClient = {
    listRoles: async () => [],
    listPermissions: async () => [],
    createRole: async () => { throw new Error('not used'); },
    updateRole: async () => { throw new Error('not used'); },
    deleteRole: async () => {}
  };

  render(
    <I18nProvider><ToastProvider>
      <SettingsScreen
        client={client as never}
        twoFactorClient={{ reset: async () => {} }}
        rolesClient={rolesClient as never}
        session={{ platformAdminId: 'me' } as never}
      />
    </ToastProvider></I18nProvider>
  );
}
