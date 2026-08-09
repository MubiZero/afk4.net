import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import type { PlatformRole } from '@/api/types';
import { RolesSection } from './RolesSection';
import type { RolesApi } from '@/api/platformClients/roles';

type RolesClient = Pick<RolesApi, 'listRoles' | 'listPermissions' | 'createRole' | 'updateRole' | 'deleteRole'>;

const PERMISSIONS = [
  'platform.organizations.view',
  'platform.billing.view',
  'platform.admins.manage'
];

function role(overrides: Partial<PlatformRole> = {}): PlatformRole {
  return {
    roleName: 'platform_support',
    displayName: 'Поддержка',
    description: 'Наблюдение',
    isBuiltIn: true,
    grantsAllPermissions: false,
    permissions: ['platform.organizations.view'],
    adminCount: 2,
    ...overrides
  };
}

function makeClient(overrides: Partial<RolesClient> = {}): RolesClient {
  return {
    listRoles: mock(async () => [role()]),
    listPermissions: mock(async () => PERMISSIONS),
    createRole: mock(async () => role({ roleName: 'clerk', isBuiltIn: false })),
    updateRole: mock(async () => role()),
    deleteRole: mock(async () => undefined),
    ...overrides
  };
}

function renderSection(client: RolesClient) {
  return render(
    <I18nProvider>
      <ToastProvider>
        <RolesSection client={client} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('RolesSection', () => {
  it('показывает роли, их пометки и сколько человек их носят', async () => {
    renderSection(makeClient());

    expect(await screen.findByText('Поддержка')).toBeInTheDocument();
    expect(screen.getByText('platform_support')).toBeInTheDocument();
    expect(screen.getByText('Встроенная')).toBeInTheDocument();
    expect(screen.getByText('2 администратора')).toBeInTheDocument();
  });

  it('не показывает удаление у встроенной роли', async () => {
    renderSection(makeClient());
    await screen.findByText('Поддержка');

    // Рычаг, который заведомо ответит отказом, хуже отсутствующего.
    expect(screen.queryByRole('button', { name: 'Удалить' })).not.toBeInTheDocument();
  });

  it('показывает удаление у роли, заведённой в панели', async () => {
    const client = makeClient({
      listRoles: mock(async () => [role({ roleName: 'clerk', displayName: 'Клерк', isBuiltIn: false, adminCount: 0 })])
    });
    renderSection(client);
    await screen.findByText('Клерк');

    await userEvent.click(screen.getByRole('button', { name: 'Удалить' }));

    await waitFor(() => expect(client.deleteRole).toHaveBeenCalledWith('clerk'));
  });

  it('создаёт роль с выбранными правами', async () => {
    const client = makeClient();
    renderSection(client);
    await screen.findByText('Поддержка');

    await userEvent.click(screen.getByRole('button', { name: 'Новая роль' }));
    await userEvent.type(screen.getByLabelText('Короткое имя'), 'clerk');
    await userEvent.type(screen.getByLabelText('Название'), 'Клерк');
    await userEvent.click(screen.getByLabelText('platform.billing.view'));
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.createRole).toHaveBeenCalledWith('clerk', {
      displayName: 'Клерк',
      description: '',
      permissions: ['platform.billing.view']
    }));
  });

  it('не даёт сохранить роль без единого права', async () => {
    const client = makeClient();
    renderSection(client);
    await screen.findByText('Поддержка');

    await userEvent.click(screen.getByRole('button', { name: 'Новая роль' }));
    await userEvent.type(screen.getByLabelText('Короткое имя'), 'empty');
    await userEvent.type(screen.getByLabelText('Название'), 'Пустая');

    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
    expect(client.createRole).not.toHaveBeenCalled();
  });

  it('объясняет отказ, когда права нет у самого администратора', async () => {
    const client = makeClient({
      createRole: mock(async () => {
        throw new PlatformApiError(403, 'permission_not_held_by_actor', 'permission_not_held_by_actor', null, '');
      })
    });
    renderSection(client);
    await screen.findByText('Поддержка');

    await userEvent.click(screen.getByRole('button', { name: 'Новая роль' }));
    await userEvent.type(screen.getByLabelText('Короткое имя'), 'wide');
    await userEvent.type(screen.getByLabelText('Название'), 'Широкая');
    await userEvent.click(screen.getByLabelText('platform.admins.manage'));
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    // Конкретная причина, пришедшая с сервера, обязана доехать до человека.
    expect(await screen.findByText(/нет у вас самих/)).toBeInTheDocument();
  });

  it('объясняет отказ при попытке снять ключ от платформы у своей роли', async () => {
    const client = makeClient({
      updateRole: mock(async () => {
        throw new PlatformApiError(409, 'self_lockout', 'self_lockout', null, '');
      })
    });
    renderSection(client);
    await screen.findByText('Поддержка');

    await userEvent.click(screen.getByRole('button', { name: 'Изменить' }));
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(await screen.findByText(/потеряете доступ к платформе/)).toBeInTheDocument();
  });
});
