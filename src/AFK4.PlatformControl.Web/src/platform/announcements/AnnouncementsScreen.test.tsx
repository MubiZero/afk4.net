import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import type { PlatformAnnouncement } from '@/api/types';
import type { AnnouncementsApi } from '@/api/platformClients/announcements';
import { AnnouncementsScreen } from './AnnouncementsScreen';

type Client = Pick<AnnouncementsApi,
  'listAnnouncements' | 'createAnnouncement' | 'updateAnnouncement' | 'publishAnnouncement' | 'withdrawAnnouncement'>;

function announcement(overrides: Partial<PlatformAnnouncement> = {}): PlatformAnnouncement {
  return {
    announcementId: '11111111-1111-1111-1111-111111111111',
    title: 'Плановые работы',
    body: 'В субботу с 2:00 до 4:00',
    severity: 'warning',
    showFromUtc: '2026-08-10T00:00:00Z',
    showUntilUtc: '2026-08-20T00:00:00Z',
    audienceKind: 'all',
    audiencePlanCodes: [],
    audienceOrganizationIds: [],
    status: 'draft',
    publishedAtUtc: null,
    emailDispatched: false,
    readCount: 0,
    ...overrides
  };
}

function makeClient(overrides: Partial<Client> = {}): Client {
  return {
    listAnnouncements: mock(async () => [announcement()]),
    createAnnouncement: mock(async () => announcement()),
    updateAnnouncement: mock(async () => announcement()),
    publishAnnouncement: mock(async () => announcement({ status: 'published' })),
    withdrawAnnouncement: mock(async () => announcement({ status: 'withdrawn' })),
    ...overrides
  };
}

function renderScreen(client: Client) {
  return render(
    <I18nProvider>
      <ToastProvider>
        <AnnouncementsScreen client={client} />
      </ToastProvider>
    </I18nProvider>
  );
}

describe('AnnouncementsScreen', () => {
  it('показывает сообщение с важностью и состоянием', async () => {
    renderScreen(makeClient());

    expect(await screen.findByText('Плановые работы')).toBeInTheDocument();
    expect(screen.getByText('Предупреждение')).toBeInTheDocument();
    expect(screen.getByText('Черновик')).toBeInTheDocument();
  });

  it('публикует только после подтверждения и предупреждает про письмо', async () => {
    const client = makeClient();
    renderScreen(client);
    await screen.findByText('Плановые работы');

    await userEvent.click(screen.getByRole('button', { name: 'Опубликовать' }));
    expect(client.publishAnnouncement).not.toHaveBeenCalled();
    // Публикация видна снаружи: владельцы получат письмо, и человек должен знать об этом до нажатия.
    expect(await screen.findByText(/владельцы получат письмо/)).toBeInTheDocument();

    const dialogConfirm = screen.getAllByRole('button', { name: 'Опубликовать' }).at(-1)!;
    await userEvent.click(dialogConfirm);

    await waitFor(() => expect(client.publishAnnouncement).toHaveBeenCalledWith('11111111-1111-1111-1111-111111111111'));
  });

  it('для сообщения «к сведению» не обещает письма', async () => {
    const client = makeClient({ listAnnouncements: mock(async () => [announcement({ severity: 'info' })]) });
    renderScreen(client);
    await screen.findByText('Плановые работы');

    await userEvent.click(screen.getByRole('button', { name: 'Опубликовать' }));

    expect(await screen.findByText(/Письмо не уходит/)).toBeInTheDocument();
  });

  it('у опубликованного сообщения важность и получатели не редактируются', async () => {
    const client = makeClient({
      listAnnouncements: mock(async () => [announcement({ status: 'published', emailDispatched: true })])
    });
    renderScreen(client);
    await screen.findByText('Плановые работы');

    await userEvent.click(screen.getByRole('button', { name: 'Изменить' }));

    // Рычаг, который заведомо ответит отказом, хуже отсутствующего: сервер такую правку отклонит.
    expect(screen.getByLabelText('Важность')).toBeDisabled();
    expect(screen.getByLabelText('Кому показать')).toBeDisabled();
    expect(screen.getByText(/письма ушли по прежнему списку/)).toBeInTheDocument();
  });

  it('снятое сообщение не предлагает править', async () => {
    const client = makeClient({ listAnnouncements: mock(async () => [announcement({ status: 'withdrawn' })]) });
    renderScreen(client);
    await screen.findByText('Плановые работы');

    expect(screen.queryByRole('button', { name: 'Изменить' })).not.toBeInTheDocument();
  });

  it('объясняет отказ сервера его собственными словами', async () => {
    const client = makeClient({
      listAnnouncements: mock(async () => [announcement({ status: 'published' })]),
      updateAnnouncement: mock(async () => {
        throw new PlatformApiError(409, 'published_audience_locked', 'published_audience_locked', null, '');
      })
    });
    renderScreen(client);
    await screen.findByText('Плановые работы');

    await userEvent.click(screen.getByRole('button', { name: 'Изменить' }));
    await userEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    // Именно в тосте: подсказка в форме говорит о том же, но она была на экране и до отказа.
    const toast = await screen.findByRole('status');
    expect(toast).toHaveTextContent('важность и получателей менять нельзя');
  });
});
