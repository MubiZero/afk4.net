import { describe, expect, it, mock } from 'bun:test';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import {
  PlatformMessageBanner,
  PlatformMessagesButton,
  PlatformMessagesHistory,
  usePlatformMessages,
  type PlatformMessagesClient
} from './PlatformMessageBanner';
import type { PlatformMessageDto } from './operatorApiClients';

function message(overrides: Partial<PlatformMessageDto> = {}): PlatformMessageDto {
  return {
    announcementId: 'a1',
    title: 'Плановые работы',
    body: 'В субботу с 2:00 до 4:00',
    severity: 'warning',
    showFromUtc: '2026-08-10T00:00:00Z',
    showUntilUtc: '2026-08-20T00:00:00Z',
    publishedAtUtc: '2026-08-10T00:00:00Z',
    isRead: false,
    ...overrides
  };
}

function Harness({ client, history = false }: { client: PlatformMessagesClient; history?: boolean }) {
  const state = usePlatformMessages(null, client);
  return (
    <>
      <PlatformMessageBanner state={state} onOpenHistory={() => {}} />
      <PlatformMessagesButton state={state} onOpen={() => {}} />
      {history ? <PlatformMessagesHistory state={state} onClose={() => {}} /> : null}
    </>
  );
}

function renderHarness(client: PlatformMessagesClient, history = false) {
  return render(
    <I18nProvider>
      <Harness client={client} history={history} />
    </I18nProvider>
  );
}

describe('сообщения платформы в Операторе', () => {
  it('показывает непрочитанное сообщение полосой', async () => {
    renderHarness({ list: mock(async () => [message()]), markRead: mock(async () => {}) });

    expect(await screen.findByText('Плановые работы')).toBeInTheDocument();
    expect(screen.getByText('В субботу с 2:00 до 4:00')).toBeInTheDocument();
  });

  it('показывает только одно сообщение — первое из пришедших', async () => {
    renderHarness({
      list: mock(async () => [
        message({ announcementId: 'a1', title: 'Авария', severity: 'critical' }),
        message({ announcementId: 'a2', title: 'Мелочь', severity: 'info' })
      ]),
      markRead: mock(async () => {})
    });

    // Порядок важности задаёт сервер; полоса не спорит с ним и не показывает вторую строку.
    expect(await screen.findByText('Авария')).toBeInTheDocument();
    expect(screen.queryByText('Мелочь')).not.toBeInTheDocument();
  });

  it('«Прочитано» гасит полосу и открывает следующее сообщение', async () => {
    const client = {
      list: mock(async () => [
        message({ announcementId: 'a1', title: 'Первое' }),
        message({ announcementId: 'a2', title: 'Второе' })
      ]),
      markRead: mock(async () => {})
    };
    renderHarness(client);
    await screen.findByText('Первое');

    fireEvent.click(screen.getByRole('button', { name: 'Прочитано' }));

    await waitFor(() => expect(client.markRead).toHaveBeenCalledWith('a1'));
    expect(await screen.findByText('Второе')).toBeInTheDocument();
  });

  it('при пустом списке полосы и кнопки нет', async () => {
    const { container } = renderHarness({ list: mock(async () => []), markRead: mock(async () => {}) });

    await waitFor(() => expect(container.querySelector('.platform-message')).toBeNull());
    expect(screen.queryByRole('button', { name: 'Сообщения платформы' })).not.toBeInTheDocument();
  });

  it('кривой ответ не роняет шелл, а просто прячет полосу', async () => {
    // Некорректная форма 200-ответа обязана вести себя как отказ: иначе `undefined` доезжает до
    // `.filter` и весь Оператор уходит в белый экран (ErrorBoundary в шелле нет).
    const client = {
      list: mock(async () => (undefined as unknown as PlatformMessageDto[])),
      markRead: mock(async () => {})
    };
    const { container } = renderHarness(client);

    await waitFor(() => expect(container.querySelector('.platform-message')).toBeNull());
  });

  it('история показывает и прочитанные, и непрочитанные', async () => {
    renderHarness({
      list: mock(async () => [
        message({ announcementId: 'a1', title: 'Свежее', isRead: false }),
        message({ announcementId: 'a2', title: 'Старое', isRead: true })
      ]),
      markRead: mock(async () => {})
    }, true);

    expect(await screen.findByText('Старое')).toBeInTheDocument();
    // У прочитанного вместо кнопки — пометка: нажимать больше нечего.
    expect(screen.getByText('Прочитано', { selector: '.platform-message-badge' })).toBeInTheDocument();
  });

  it('счётчик в шапке считает непрочитанные', async () => {
    renderHarness({
      list: mock(async () => [
        message({ announcementId: 'a1', isRead: false }),
        message({ announcementId: 'a2', isRead: false }),
        message({ announcementId: 'a3', isRead: true })
      ]),
      markRead: mock(async () => {})
    });

    const button = await screen.findByRole('button', { name: 'Сообщения платформы' });
    expect(button).toHaveTextContent('2');
  });
});
