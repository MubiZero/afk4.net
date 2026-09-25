import { describe, expect, it, mock } from 'bun:test';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import type { AdCampaignDto, AdCreativeDto, ModerateAdCreativeRequest, UpsertAdCreativeRequest } from '@/api/types';
import { AdCampaignPage, type AdCampaignClient } from './AdCampaignPage';

const CAMPAIGN_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
const ADVERTISER_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const PENDING_ID = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
const REJECTED_ID = 'dddddddd-dddd-dddd-dddd-dddddddddddd';

function creative(overrides: Partial<AdCreativeDto> = {}): AdCreativeDto {
  return {
    creativeId: PENDING_ID,
    campaignId: CAMPAIGN_ID,
    title: 'Скидка на ноутбуки',
    body: 'До конца месяца — минус 15%',
    imageUrl: 'https://cdn.example.com/laptops.jpg',
    moderation: 'pending',
    rejectedReason: null,
    moderatedAtUtc: null,
    createdAtUtc: '2026-09-01T10:00:00Z',
    ...overrides
  };
}

const rejected = creative({
  creativeId: REJECTED_ID,
  title: 'Пивной фестиваль',
  body: null,
  imageUrl: null,
  moderation: 'rejected',
  rejectedReason: 'Алкоголь рекламировать нельзя'
});

function campaign(overrides: Partial<AdCampaignDto> = {}): AdCampaignDto {
  return {
    campaignId: CAMPAIGN_ID,
    advertiserId: ADVERTISER_ID,
    advertiserName: 'Техномир',
    name: 'Осень в Техномире',
    category: 'electronics',
    startsAtUtc: '2020-01-01T00:00:00Z',
    endsAtUtc: '2099-01-01T00:00:00Z',
    cities: ['Душанбе', 'Худжанд'],
    organizationIds: ['11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222'],
    state: 'draft',
    creatives: [creative(), rejected],
    createdAtUtc: '2026-09-01T10:00:00Z',
    updatedAtUtc: '2026-09-01T10:00:00Z',
    ...overrides
  };
}

function makeClient(initial: AdCampaignDto = campaign(), overrides: Partial<AdCampaignClient> = {}): AdCampaignClient {
  return {
    listCampaigns: mock(async () => [initial]),
    listAdvertisers: mock(async () => [{ advertiserId: ADVERTISER_ID, name: 'Техномир', contact: '', createdAtUtc: '2026-09-01T10:00:00Z' }]),
    updateCampaign: mock(async () => initial),
    setCampaignState: mock(async (_id: string, state: AdCampaignDto['state']) => ({ ...initial, state })),
    createCreative: mock(async (_id: string, request: UpsertAdCreativeRequest) =>
      creative({ creativeId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', ...request })),
    updateCreative: mock(async (_campaignId: string, creativeId: string, request: UpsertAdCreativeRequest) =>
      creative({ creativeId, ...request, moderation: 'pending' })),
    moderateCreative: mock(async (_campaignId: string, creativeId: string, request: ModerateAdCreativeRequest) => {
      const source = initial.creatives.find(item => item.creativeId === creativeId) ?? creative();
      return { ...source, moderation: request.approve ? 'approved' : 'rejected', rejectedReason: request.reason } as AdCreativeDto;
    }),
    ...overrides
  };
}

function renderPage(client: AdCampaignClient, onBack = mock(() => {})) {
  render(
    <I18nProvider>
      <ToastProvider>
        <AdCampaignPage client={client} campaignId={CAMPAIGN_ID} onBack={onBack} />
      </ToastProvider>
    </I18nProvider>
  );
  return { onBack };
}

function creativeRow(title: string): HTMLElement {
  const row = screen.getAllByRole('row').find(candidate => candidate.querySelector('td strong')?.textContent === title);
  if (row === undefined) throw new Error(`no row for ${title}`);
  return row;
}

describe('AdCampaignPage', () => {
  it('показывает кампанию, нацеливание и креативы с решением модератора', async () => {
    renderPage(makeClient());

    expect(await screen.findByRole('heading', { name: 'Осень в Техномире' })).toBeInTheDocument();
    expect(screen.getByText('Душанбе, Худжанд')).toBeInTheDocument();
    expect(screen.getByText('2 клуба')).toBeInTheDocument();
    expect(screen.getByText(/Любая правка возвращает креатив на модерацию/)).toBeInTheDocument();

    const pending = creativeRow('Скидка на ноутбуки');
    expect(within(pending).getByText('Ждёт проверки')).toBeInTheDocument();
    expect(pending.querySelector('img')).toHaveAttribute('src', 'https://cdn.example.com/laptops.jpg');
    const declined = creativeRow('Пивной фестиваль');
    expect(within(declined).getByText('Отклонён')).toBeInTheDocument();
    expect(within(declined).getByText('Причина: Алкоголь рекламировать нельзя')).toBeInTheDocument();
    // Отклонённый второй раз не отклоняют — его правят или одобряют.
    expect(within(declined).queryByRole('button', { name: 'Отклонить' })).not.toBeInTheDocument();
  });

  it('без одобренного креатива «Запустить» погашена и говорит почему', async () => {
    const client = makeClient();
    renderPage(client);

    const start = await screen.findByRole('button', { name: 'Запустить' });
    expect(start).toBeDisabled();
    expect(start).toHaveAccessibleDescription('Запустить можно, когда одобрен хотя бы один креатив.');
    // У черновика паузы нет: ставить на паузу то, что не шло, бессмысленно.
    expect(screen.queryByRole('button', { name: 'Пауза' })).not.toBeInTheDocument();
  });

  it('одобрение требует отметки «проверено» и отправляет её', async () => {
    const client = makeClient();
    renderPage(client);
    await screen.findByText('Скидка на ноутбуки');

    await userEvent.click(within(creativeRow('Скидка на ноутбуки')).getByRole('button', { name: 'Одобрить' }));
    const dialog = screen.getByRole('dialog', { name: 'Одобрить креатив' });
    // Модератор видит креатив так, как его увидит игрок.
    expect(within(dialog).getByText('Реклама · Техномир')).toBeInTheDocument();
    const confirm = within(dialog).getByRole('button', { name: 'Одобрить' });
    expect(confirm).toBeDisabled();

    await userEvent.click(within(dialog).getByRole('checkbox', { name: 'Проверено: не другой клуб, не алкоголь, не табак, не ставки' }));
    expect(confirm).toBeEnabled();
    await userEvent.click(confirm);

    await waitFor(() => expect(client.moderateCreative).toHaveBeenCalledWith(CAMPAIGN_ID, PENDING_ID, {
      approve: true,
      reason: null,
      confirmedAllowed: true
    }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(within(creativeRow('Скидка на ноутбуки')).getByText('Одобрен')).toBeInTheDocument();
    // С одобренным креативом кампанию можно запускать.
    expect(screen.getByRole('button', { name: 'Запустить' })).toBeEnabled();
  });

  it('отказ требует причину и отправляет её', async () => {
    const client = makeClient();
    renderPage(client);
    await screen.findByText('Скидка на ноутбуки');

    await userEvent.click(within(creativeRow('Скидка на ноутбуки')).getByRole('button', { name: 'Отклонить' }));
    const dialog = screen.getByRole('dialog', { name: 'Отклонить креатив' });
    const confirm = within(dialog).getByRole('button', { name: 'Отклонить' });
    expect(confirm).toBeDisabled();

    await userEvent.type(within(dialog).getByLabelText(/^Причина/), '  На картинке логотип другого клуба ');
    await userEvent.click(confirm);

    await waitFor(() => expect(client.moderateCreative).toHaveBeenCalledWith(CAMPAIGN_ID, PENDING_ID, {
      approve: false,
      reason: 'На картинке логотип другого клуба',
      confirmedAllowed: false
    }));
    expect(await screen.findByText('Причина: На картинке логотип другого клуба')).toBeInTheDocument();
  });

  it('запускает кампанию с одобренным креативом', async () => {
    const client = makeClient(campaign({ creatives: [creative({ moderation: 'approved' })] }));
    renderPage(client);

    await userEvent.click(await screen.findByRole('button', { name: 'Запустить' }));

    await waitFor(() => expect(client.setCampaignState).toHaveBeenCalledWith(CAMPAIGN_ID, 'active'));
    expect(await screen.findByText('Кампания запущена')).toBeInTheDocument();
    expect(screen.getByText('Идёт')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Пауза' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'В черновик' })).toBeInTheDocument();
  });

  // На экране одобренный креатив, а на сервере его одобрение уже сняли — например, правкой в
  // другой вкладке. Отказ называется словами, а креативы перечитываются.
  it('отказ «нет одобренного креатива» объясняет словами и перечитывает кампанию', async () => {
    const client = makeClient(campaign({ creatives: [creative({ moderation: 'approved' })] }), {
      setCampaignState: mock(async () => {
        throw new PlatformApiError(409, 'The campaign has no approved creative.', 'ad_campaign_without_approved_creative');
      })
    });
    renderPage(client);

    await userEvent.click(await screen.findByRole('button', { name: 'Запустить' }));

    expect(await screen.findByText('Запустить нельзя: у кампании нет одобренного креатива. Одобрите хотя бы один.')).toBeInTheDocument();
    expect(screen.queryByText(/no approved creative/)).not.toBeInTheDocument();
    await waitFor(() => expect(client.listCampaigns).toHaveBeenCalledTimes(2));
  });

  it('правка креатива предупреждает о повторной модерации и возвращает его в «ждёт проверки»', async () => {
    const client = makeClient(campaign({ state: 'active', creatives: [creative({ moderation: 'approved' })] }));
    renderPage(client);
    await screen.findByText('Скидка на ноутбуки');

    await userEvent.click(within(creativeRow('Скидка на ноутбуки')).getByRole('button', { name: 'Изменить' }));
    const dialog = screen.getByRole('dialog', { name: 'Изменить креатив' });
    expect(dialog).toHaveTextContent('После сохранения креатив снова ждёт модерации');
    const title = within(dialog).getByLabelText(/^Заголовок/);
    await userEvent.clear(title);
    await userEvent.type(title, 'Скидка на мониторы');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateCreative).toHaveBeenCalledWith(CAMPAIGN_ID, PENDING_ID, {
      title: 'Скидка на мониторы',
      body: 'До конца месяца — минус 15%',
      imageUrl: 'https://cdn.example.com/laptops.jpg'
    }));
    expect(await screen.findByText('Скидка на мониторы')).toBeInTheDocument();
    expect(within(creativeRow('Скидка на мониторы')).getByText('Ждёт проверки')).toBeInTheDocument();
    // Запущенная кампания осталась без одобренного креатива — на ПК её больше не видно.
    expect(screen.getByText('Нечего показывать')).toBeInTheDocument();
  });

  it('новый креатив: картинка только https, предпросмотр — та самая картинка', async () => {
    const client = makeClient();
    renderPage(client);
    await screen.findByText('Скидка на ноутбуки');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить креатив' }));
    const dialog = screen.getByRole('dialog', { name: 'Новый креатив' });
    await userEvent.type(within(dialog).getByLabelText(/^Заголовок/), 'Интернет 100 Мбит');
    const image = within(dialog).getByLabelText(/^Картинка/);
    await userEvent.type(image, 'http://cdn.example.com/net.jpg');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(client.createCreative).not.toHaveBeenCalled();
    expect(within(dialog).getByText('Картинка — ссылка, которая начинается с https://.')).toBeInTheDocument();

    await userEvent.clear(image);
    await userEvent.type(image, 'https://cdn.example.com/net.jpg');
    expect(within(dialog).getByAltText('Предпросмотр картинки')).toHaveAttribute('src', 'https://cdn.example.com/net.jpg');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.createCreative).toHaveBeenCalledWith(CAMPAIGN_ID, {
      title: 'Интернет 100 Мбит',
      body: null,
      imageUrl: 'https://cdn.example.com/net.jpg'
    }));
    expect(await screen.findByText('Интернет 100 Мбит')).toBeInTheDocument();
  });

  it('старая ссылка на кампанию, которой нет, ведёт обратно к списку', async () => {
    const client = makeClient();
    client.listCampaigns = mock(async () => []);
    const { onBack } = renderPage(client);

    expect(await screen.findByText('Такой кампании нет. Возможно, ссылка старая.')).toBeInTheDocument();
    await userEvent.click(screen.getAllByRole('button', { name: 'Все кампании' }).at(-1)!);
    expect(onBack).toHaveBeenCalled();
  });
});
