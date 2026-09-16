import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { PlatformApiError } from '@/api/platformTransport';
import { AdminInviteDialog } from './AdminInviteDialog';

afterEach(cleanup);

function setup(invite = mock().mockResolvedValue({ code: 'INV-123', expiresAtUtc: '2026-09-20T00:00:00Z' })) {
  const client = { invite };
  const onCreated = mock();
  const onOpenChange = mock();
  render(
    <I18nProvider>
      <AdminInviteDialog open client={client as never} onOpenChange={onOpenChange} onCreated={onCreated} />
    </I18nProvider>
  );
  return { client, onCreated, onOpenChange };
}

async function createInvite(client: { invite: ReturnType<typeof mock> }) {
  fireEvent.click(screen.getByRole('button', { name: 'Создать приглашение' }));
  await waitFor(() => expect(client.invite).toHaveBeenCalled());
}

describe('AdminInviteDialog', () => {
  // Поддержка — вариант по умолчанию: полный доступ выдают осознанно, а не промахом мимо селекта.
  it('по умолчанию приглашает поддержку на 72 часа', async () => {
    const { client } = setup();

    await createInvite(client);

    expect(client.invite.mock.calls[0]).toEqual(['platform_support', 72]);
  });

  it('отправляет выбранную роль и срок действия', async () => {
    const { client } = setup();

    fireEvent.change(screen.getByLabelText('Роль'), { target: { value: 'platform_admin' } });
    fireEvent.change(screen.getByLabelText('Срок действия, часов'), { target: { value: '24' } });
    await createInvite(client);

    expect(client.invite.mock.calls[0]).toEqual(['platform_admin', 24]);
  });

  // Приглашение на ноль часов просрочено в момент выдачи.
  it('не даёт выставить срок меньше часа', async () => {
    const { client } = setup();

    fireEvent.change(screen.getByLabelText('Срок действия, часов'), { target: { value: '0' } });
    await createInvite(client);

    expect(client.invite.mock.calls[0][1]).toBe(1);
  });

  // Код сервер отдаёт ровно один раз: если этот шаг пропустить, приглашение выдано и потеряно.
  it('показывает ссылку и код после создания', async () => {
    const { client } = setup();

    await createInvite(client);

    await screen.findByText('Код показан только сейчас. Сохраните его — второй раз получить его будет неоткуда.');
    expect(screen.getByLabelText('Ссылка для активации')).toHaveValue(
      `${window.location.origin}/account-activation?kind=platform-admin&code=INV-123`
    );
    expect(screen.getByLabelText('Код приглашения')).toHaveValue('INV-123');
  });

  it('обновляет список только после того, как код показали', async () => {
    const { client, onCreated, onOpenChange } = setup();

    await createInvite(client);
    expect(onCreated).not.toHaveBeenCalled();

    fireEvent.click(await screen.findByRole('button', { name: 'Готово' }));

    expect(onCreated).toHaveBeenCalledTimes(1);
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('не закрывает форму, если сервер отказал', async () => {
    const { client, onOpenChange } = setup(mock().mockRejectedValue(new PlatformApiError(403, 'Forbidden.')));

    await createInvite(client);

    await screen.findByText('Недостаточно прав для этого действия.');
    expect(onOpenChange).not.toHaveBeenCalled();
  });
});
