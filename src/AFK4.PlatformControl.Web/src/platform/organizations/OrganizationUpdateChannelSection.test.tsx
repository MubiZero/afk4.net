import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import { OrganizationUpdateChannelSection } from './OrganizationUpdateChannelSection';

afterEach(cleanup);

const organization = {
  organizationId: 'org-1',
  updateChannel: 'stable',
  pinnedClientVersion: null
} as never;

function setup(updateUpdateChannel = mock().mockResolvedValue(organization)) {
  const client = { updateUpdateChannel };
  render(
    <I18nProvider><ToastProvider>
      <OrganizationUpdateChannelSection client={client as never} organization={organization} onUpdated={() => {}} />
    </ToastProvider></I18nProvider>
  );
  return client;
}

describe('OrganizationUpdateChannelSection', () => {
  // Форма предлагала «canary» — канал, которого сервер не знает: закрепить его за клубом было
  // нельзя, отказ приходил каждый раз, а «internal», который сервер принимает, не предлагался.
  it('предлагает ровно те каналы, которые принимает сервер', () => {
    setup();

    const options = [...screen.getByRole('combobox').querySelectorAll('option')].map(option => option.value);

    expect(options).toEqual(['stable', 'beta', 'internal']);
  });

  it('сохраняет выбранный канал и закреплённую версию', async () => {
    const client = setup();

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'internal' } });
    fireEvent.change(screen.getByLabelText('Закреплённая версия'), { target: { value: '1.4.0' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateUpdateChannel).toHaveBeenCalledTimes(1));
    expect(client.updateUpdateChannel.mock.calls[0]).toEqual([
      'org-1',
      { channel: 'internal', pinnedClientVersion: '1.4.0' }
    ]);
  });

  // Пустое поле — это «снять закрепление», а не «закрепить версию с пустым именем».
  it('пустая версия снимает закрепление', async () => {
    const client = setup();

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'beta' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateUpdateChannel).toHaveBeenCalledTimes(1));
    expect(client.updateUpdateChannel.mock.calls[0][1].pinnedClientVersion).toBeNull();
  });

  it('не даёт сохранить, пока ничего не изменили', () => {
    setup();

    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
  });

  // Отказ обязан быть виден: молчаливая форма выглядит как «сохранилось», а канал остаётся прежним.
  it('показывает отказ сервера', async () => {
    const client = setup(mock().mockRejectedValue(new PlatformApiError(403, 'Forbidden.')));

    fireEvent.change(screen.getByRole('combobox'), { target: { value: 'beta' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateUpdateChannel).toHaveBeenCalled());
    await screen.findByText('Недостаточно прав для этого действия.');
  });
});
