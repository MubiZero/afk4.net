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

const ALL_CHECKS = ['not_club_or_betting', 'no_banned_goods', 'minors', 'truthful', 'ethical', 'tajik_on_image'];

// Таджикский — государственный язык: он в title/body, русский — вторая строка.
function creative(overrides: Partial<AdCreativeDto> = {}): AdCreativeDto {
  return {
    creativeId: PENDING_ID,
    campaignId: CAMPAIGN_ID,
    title: 'Тахфиф ба ноутбукҳо',
    body: 'То охири моҳ — 15% арзонтар',
    imageUrl: 'https://cdn.example.com/laptops.jpg',
    moderation: 'pending',
    rejectedReason: null,
    moderatedAtUtc: null,
    createdAtUtc: '2026-09-01T10:00:00Z',
    titleRu: 'Скидка на ноутбуки',
    bodyRu: 'До конца месяца — минус 15%',
    wordingFlags: [],
    archivedAtUtc: null,
    ...overrides
  };
}

const rejected = creative({
  creativeId: REJECTED_ID,
  title: 'Фестивали пиво',
  body: null,
  imageUrl: null,
  titleRu: null,
  bodyRu: null,
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
    compliance: { permitNumber: null, distanceSelling: false, requiresCertification: false, containsOffer: false },
    ...overrides
  };
}

const advertiser = {
  advertiserId: ADVERTISER_ID,
  name: 'Техномир',
  contact: '',
  createdAtUtc: '2026-09-01T10:00:00Z',
  legalName: 'ООО «Техномир»',
  taxId: '123456789',
  address: 'Душанбе, пр. Рудаки, 1'
};

function makeClient(initial: AdCampaignDto = campaign(), overrides: Partial<AdCampaignClient> = {}): AdCampaignClient {
  const source = (creativeId: string) => initial.creatives.find(item => item.creativeId === creativeId) ?? creative();
  return {
    listCampaigns: mock(async () => [initial]),
    listAdvertisers: mock(async () => [advertiser]),
    updateCampaign: mock(async () => initial),
    setCampaignState: mock(async (_id: string, state: AdCampaignDto['state']) => ({ ...initial, state })),
    createCreative: mock(async (_id: string, request: UpsertAdCreativeRequest) =>
      creative({ creativeId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee', ...request })),
    updateCreative: mock(async (_campaignId: string, creativeId: string, request: UpsertAdCreativeRequest) =>
      creative({ creativeId, ...request, moderation: 'pending', rejectedReason: null })),
    moderateCreative: mock(async (_campaignId: string, creativeId: string, request: ModerateAdCreativeRequest) =>
      ({ ...source(creativeId), moderation: request.approve ? 'approved' : 'rejected', rejectedReason: request.reason }) as AdCreativeDto),
    archiveCreative: mock(async (_campaignId: string, creativeId: string) =>
      ({ ...source(creativeId), archivedAtUtc: '2026-09-26T10:00:00Z' })),
    uploadImage: mock(async () => ({ url: 'https://media.test/platform/ad-creative/banner.png' })),
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

/** Таджикский стоит раньше русского и сам не в русском блоке; русский — в мелком блоке `lang="ru"`. */
function expectTajikFirst(container: HTMLElement, tajik: string, russian: string) {
  const tajikNode = within(container).getByText(tajik);
  const russianNode = within(container).getByText(russian);
  expect(tajikNode.closest('[lang="tg"]')).not.toBeNull();
  expect(russianNode.closest('.pc-ad-ru')).toHaveAttribute('lang', 'ru');
  expect(tajikNode.compareDocumentPosition(russianNode) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
}

async function openApproval(client: AdCampaignClient) {
  renderPage(client);
  await screen.findByText('Тахфиф ба ноутбукҳо');
  await userEvent.click(within(creativeRow('Тахфиф ба ноутбукҳо')).getByRole('button', { name: 'Одобрить' }));
  return screen.getByRole('dialog', { name: 'Одобрить креатив' });
}

async function tickAll(dialog: HTMLElement) {
  for (const checkbox of within(dialog).getAllByRole('checkbox')) await userEvent.click(checkbox);
}

describe('AdCampaignPage', () => {
  it('показывает кампанию, нацеливание и креативы — таджикский первым, русский ниже', async () => {
    renderPage(makeClient());

    expect(await screen.findByRole('heading', { name: 'Осень в Техномире' })).toBeInTheDocument();
    expect(screen.getByText('Душанбе, Худжанд')).toBeInTheDocument();
    expect(screen.getByText('2 клуба')).toBeInTheDocument();
    expect(screen.getByText(/одобренный не правится/)).toBeInTheDocument();

    const pending = creativeRow('Тахфиф ба ноутбукҳо');
    expect(within(pending).getByText('Ждёт проверки')).toBeInTheDocument();
    expect(pending.querySelector('img')).toHaveAttribute('src', 'https://cdn.example.com/laptops.jpg');
    expectTajikFirst(pending, 'Тахфиф ба ноутбукҳо', 'Скидка на ноутбуки');
    expectTajikFirst(pending, 'То охири моҳ — 15% арзонтар', 'До конца месяца — минус 15%');
    const declined = creativeRow('Фестивали пиво');
    expect(within(declined).getByText('Отклонён')).toBeInTheDocument();
    expect(within(declined).getByText('Причина: Алкоголь рекламировать нельзя')).toBeInTheDocument();
    // Без русского текста русского блока нет вовсе.
    expect(declined.querySelector('.pc-ad-ru')).toBeNull();
    // Отклонённый второй раз не отклоняют — его правят или одобряют.
    expect(within(declined).queryByRole('button', { name: 'Отклонить' })).not.toBeInTheDocument();
    expect(within(declined).getByRole('button', { name: 'Изменить' })).toBeEnabled();
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

  it('сводка говорит, что карточка допишет по закону', async () => {
    renderPage(makeClient(campaign({
      category: 'health_beauty',
      compliance: { permitNumber: '№ 123/45', distanceSelling: true, requiresCertification: true, containsOffer: true }
    })));
    await screen.findByRole('heading', { name: 'Осень в Техномире' });

    expect(screen.getByText('Здоровье и красота')).toBeInTheDocument();
    expect(screen.getByText('Разрешение Минздрава').closest('.pc-passport-row')).toHaveTextContent('№ 123/45');
    const notices = screen.getByText('На карточке по закону').closest('.pc-passport-row') as HTMLElement;
    // Продавца карточка напечатает из реквизитов рекламодателя — они здесь те же.
    expect(await within(notices).findByText('Продавец: ООО «Техномир», ИНН 123456789, Душанбе, пр. Рудаки, 1')).toBeInTheDocument();
    expect(within(notices).getByText('Пометка «подлежит обязательной сертификации»')).toBeInTheDocument();
    expect(within(notices).getByText(/^Срок предложения: до \S/)).toBeInTheDocument();
  });

  it('без отметок закона карточка несёт только метку и рекламодателя', async () => {
    renderPage(makeClient());
    await screen.findByRole('heading', { name: 'Осень в Техномире' });

    expect(screen.getByText('На карточке по закону').closest('.pc-passport-row')).toHaveTextContent('Только метка «Реклама» и рекламодатель');
    expect(screen.queryByText('Разрешение Минздрава')).not.toBeInTheDocument();
  });

  it('одобрение требует все шесть отметок и отправляет их', async () => {
    const client = makeClient();
    const dialog = await openApproval(client);
    // Модератор видит креатив так, как его увидит игрок: таджикский первым.
    expect(within(dialog).getByText('Реклама · Техномир')).toBeInTheDocument();
    expectTajikFirst(within(dialog).getByRole('figure'), 'Тахфиф ба ноутбукҳо', 'Скидка на ноутбуки');

    const confirm = within(dialog).getByRole('button', { name: 'Одобрить' });
    const checkboxes = within(dialog).getAllByRole('checkbox');
    expect(checkboxes).toHaveLength(6);
    expect(within(dialog).getByRole('checkbox', { name: 'Не другой клуб, не ставки и не казино' })).toBeInTheDocument();
    expect(within(dialog).getByRole('checkbox', { name: /^Нет запрещённого товара \(ст\. 17\)/ })).toBeInTheDocument();
    expect(within(dialog).getByRole('checkbox', { name: /^Несовершеннолетние \(ст\. 21\)/ })).toBeInTheDocument();
    expect(within(dialog).getByRole('checkbox', { name: /^Достоверно \(ст\. 7\)/ })).toBeInTheDocument();
    expect(within(dialog).getByRole('checkbox', { name: /^Этично \(ст\. 6, 8, 9, 10\)/ })).toBeInTheDocument();
    expect(within(dialog).getByRole('checkbox', { name: /^Текст на картинке — на таджикском/ })).toBeInTheDocument();
    expect(confirm).toBeDisabled();

    // Пять из шести — всё ещё нельзя: сервер не одобрит без каждой строки.
    for (const checkbox of checkboxes.slice(0, 5)) await userEvent.click(checkbox);
    expect(confirm).toBeDisabled();
    await userEvent.click(checkboxes[5]);
    expect(confirm).toBeEnabled();
    // Снятая отметка снова гасит кнопку.
    await userEvent.click(checkboxes[2]);
    expect(confirm).toBeDisabled();
    await userEvent.click(checkboxes[2]);
    await userEvent.click(confirm);

    await waitFor(() => expect(client.moderateCreative).toHaveBeenCalledWith(CAMPAIGN_ID, PENDING_ID, {
      approve: true,
      reason: null,
      confirmed: ALL_CHECKS
    }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(within(creativeRow('Тахфиф ба ноутбукҳо')).getByText('Одобрен')).toBeInTheDocument();
    // С одобренным креативом кампанию можно запускать.
    expect(screen.getByRole('button', { name: 'Запустить' })).toBeEnabled();
  });

  it('у «Финансов» — седьмая отметка, и без неё не одобрить (ст. 18)', async () => {
    const client = makeClient(campaign({ category: 'finance' }));
    const dialog = await openApproval(client);

    expect(within(dialog).getAllByRole('checkbox')).toHaveLength(7);
    expect(within(dialog).getByRole('checkbox', { name: /^Финансы \(ст\. 18\)/ })).toBeInTheDocument();
    await tickAll(dialog);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Одобрить' }));

    await waitFor(() => expect(client.moderateCreative).toHaveBeenCalledWith(CAMPAIGN_ID, PENDING_ID, {
      approve: true,
      reason: null,
      confirmed: [...ALL_CHECKS, 'finance_terms']
    }));
  });

  it('слова вроде «лучший» подсвечивает над отметками', async () => {
    const client = makeClient(campaign({ creatives: [creative({ wordingFlags: ['лучш', 'беҳтарин'] })] }));
    const dialog = await openApproval(client);

    const warning = within(dialog).getByText(/^В тексте есть «лучш», «беҳтарин»\./);
    expect(warning).toHaveTextContent('только с подтверждающим документом (ст. 7)');
    const firstCheck = within(dialog).getAllByRole('checkbox')[0];
    expect(warning.compareDocumentPosition(firstCheck) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('без подозрительных слов предупреждения нет', async () => {
    const dialog = await openApproval(makeClient());
    expect(within(dialog).queryByText(/^В тексте есть/)).not.toBeInTheDocument();
  });

  it('картинку не удалось сохранить — говорит, что проверить, и не закрывает окно', async () => {
    const client = makeClient(campaign(), {
      moderateCreative: mock(async () => {
        throw new PlatformApiError(409, 'The image could not be downloaded for storage.', 'ad_image_unavailable');
      })
    });
    const dialog = await openApproval(client);
    await tickAll(dialog);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Одобрить' }));

    const alert = await within(dialog).findByRole('alert');
    expect(alert).toHaveTextContent('Картинку не удалось скачать для хранения — проверьте адрес. Подойдёт PNG, JPEG или WebP до 2 МБ.');
    expect(alert).not.toHaveTextContent('could not be downloaded');
    expect(within(creativeRow('Тахфиф ба ноутбукҳо')).getByText('Ждёт проверки')).toBeInTheDocument();
  });

  it('отказ требует причину и отправляет её без отметок', async () => {
    const client = makeClient();
    renderPage(client);
    await screen.findByText('Тахфиф ба ноутбукҳо');

    await userEvent.click(within(creativeRow('Тахфиф ба ноутбукҳо')).getByRole('button', { name: 'Отклонить' }));
    const dialog = screen.getByRole('dialog', { name: 'Отклонить креатив' });
    const confirm = within(dialog).getByRole('button', { name: 'Отклонить' });
    expect(confirm).toBeDisabled();
    expect(within(dialog).queryByRole('checkbox')).not.toBeInTheDocument();

    await userEvent.type(within(dialog).getByLabelText(/^Причина/), '  На картинке логотип другого клуба ');
    await userEvent.click(confirm);

    await waitFor(() => expect(client.moderateCreative).toHaveBeenCalledWith(CAMPAIGN_ID, PENDING_ID, {
      approve: false,
      reason: 'На картинке логотип другого клуба',
      confirmed: []
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

  // На экране одобренный креатив, а на сервере его уже сняли с показа — например, в другой
  // вкладке. Отказ называется словами, а креативы перечитываются.
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

  // Одобренный креатив остаётся таким, каким его видели игроки: его не правят, а снимают.
  it('одобренный креатив не правится и объясняет почему; «Снять с показа» снимает его', async () => {
    const client = makeClient(campaign({ state: 'active', creatives: [creative({ moderation: 'approved' })] }));
    renderPage(client);
    await screen.findByText('Тахфиф ба ноутбукҳо');

    const row = creativeRow('Тахфиф ба ноутбукҳо');
    const edit = within(row).getByRole('button', { name: 'Изменить' });
    expect(edit).toBeDisabled();
    expect(edit).toHaveAccessibleDescription(
      'Одобренный креатив не меняется: игроки видели именно его, и отчёт показов считает его. Нужна правка — добавьте новый креатив.');
    // Отклонить задним числом — тоже нельзя: после отказа креатив снова правился бы.
    expect(within(row).queryByRole('button', { name: 'Отклонить' })).not.toBeInTheDocument();
    expect(within(row).queryByRole('button', { name: 'Одобрить' })).not.toBeInTheDocument();

    await userEvent.click(within(row).getByRole('button', { name: 'Снять с показа' }));
    const dialog = screen.getByRole('dialog', { name: 'Снять креатив с показа?' });
    expect(dialog).toHaveTextContent('Вернуть его на показ будет нельзя');
    // Последний одобренный у запущенной кампании — после снятия её не станет на ПК.
    expect(dialog).toHaveTextContent('Это последний одобренный креатив кампании');
    expect(client.archiveCreative).not.toHaveBeenCalled();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Снять с показа' }));

    await waitFor(() => expect(client.archiveCreative).toHaveBeenCalledWith(CAMPAIGN_ID, PENDING_ID));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const archived = creativeRow('Тахфиф ба ноутбукҳо');
    expect(within(archived).getByText('Снят с показа')).toBeInTheDocument();
    expect(within(archived).queryAllByRole('button')).toHaveLength(0);
    expect(screen.getByText('Нечего показывать')).toBeInTheDocument();
    // Одобренных на показе больше нет — и объяснять погашенную «Изменить» больше некому.
    expect(screen.queryByText(/Одобренный креатив не меняется/)).not.toBeInTheDocument();
  });

  it('снятый с показа креатив — с пометкой и без действий', async () => {
    renderPage(makeClient(campaign({ creatives: [creative({ moderation: 'approved', archivedAtUtc: '2026-09-20T00:00:00Z' }), rejected] })));
    await screen.findByText('Тахфиф ба ноутбукҳо');

    const archived = creativeRow('Тахфиф ба ноутбукҳо');
    expect(within(archived).getByText('Снят с показа')).toBeInTheDocument();
    expect(within(archived).queryByText('Одобрен')).not.toBeInTheDocument();
    expect(within(archived).queryAllByRole('button')).toHaveLength(0);
  });

  it('правка отклонённого креатива: таджикский и русский отдельно, креатив снова ждёт проверки', async () => {
    const client = makeClient();
    renderPage(client);
    await screen.findByText('Фестивали пиво');

    await userEvent.click(within(creativeRow('Фестивали пиво')).getByRole('button', { name: 'Изменить' }));
    const dialog = screen.getByRole('dialog', { name: 'Изменить креатив' });
    expect(dialog).toHaveTextContent('После сохранения креатив снова ждёт модерации');
    expect(dialog).toHaveTextContent('На карточке первым идёт таджикский');
    const title = within(dialog).getByLabelText(/^Заголовок на таджикском \(обязательно\)/);
    await userEvent.clear(title);
    await userEvent.type(title, 'Шарбати табиӣ');
    await userEvent.type(within(dialog).getByLabelText(/^Заголовок по-русски/), 'Натуральный сок');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateCreative).toHaveBeenCalledWith(CAMPAIGN_ID, REJECTED_ID, {
      title: 'Шарбати табиӣ',
      body: null,
      imageUrl: null,
      titleRu: 'Натуральный сок',
      bodyRu: null
    }));
    expect(await screen.findByText('Шарбати табиӣ')).toBeInTheDocument();
    expect(within(creativeRow('Шарбати табиӣ')).getByText('Ждёт проверки')).toBeInTheDocument();
  });

  // Пока здесь была открыта правка, креатив одобрили в другой вкладке: сервер правку не примет.
  it('отказ «креатив уже одобрен» объясняет словами и перечитывает кампанию', async () => {
    const client = makeClient(campaign(), {
      updateCreative: mock(async () => {
        throw new PlatformApiError(409, 'An approved creative cannot be edited; add a new one.', 'ad_creative_locked');
      })
    });
    renderPage(client);
    await screen.findByText('Тахфиф ба ноутбукҳо');

    await userEvent.click(within(creativeRow('Тахфиф ба ноутбукҳо')).getByRole('button', { name: 'Изменить' }));
    const dialog = screen.getByRole('dialog', { name: 'Изменить креатив' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    const alert = await within(dialog).findByRole('alert');
    expect(alert).toHaveTextContent('Креатив уже одобрен и не меняется: игроки видели именно его. Добавьте новый креатив.');
    expect(alert).not.toHaveTextContent('cannot be edited');
    await waitFor(() => expect(client.listCampaigns).toHaveBeenCalledTimes(2));
  });

  it('новый креатив: заголовок на таджикском обязателен, картинка только https', async () => {
    const client = makeClient();
    renderPage(client);
    await screen.findByText('Тахфиф ба ноутбукҳо');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить креатив' }));
    const dialog = screen.getByRole('dialog', { name: 'Новый креатив' });
    // Только русский — мало: реклама идёт на государственном языке.
    await userEvent.type(within(dialog).getByLabelText(/^Заголовок по-русски/), 'Интернет 100 Мбит');
    const image = within(dialog).getByLabelText(/^Картинка/);
    await userEvent.type(image, 'http://cdn.example.com/net.jpg');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(client.createCreative).not.toHaveBeenCalled();
    expect(within(dialog).getByText('Укажите заголовок на таджикском — реклама идёт на государственном языке.')).toBeInTheDocument();
    expect(within(dialog).getByLabelText(/^Заголовок на таджикском/)).toHaveFocus();
    expect(within(dialog).getByText('Картинка — ссылка, которая начинается с https://.')).toBeInTheDocument();

    await userEvent.type(within(dialog).getByLabelText(/^Заголовок на таджикском/), 'Интернет 100 Мбит/с');
    await userEvent.clear(image);
    await userEvent.type(image, 'https://cdn.example.com/net.jpg');
    expect(within(dialog).getByAltText('Предпросмотр картинки')).toHaveAttribute('src', 'https://cdn.example.com/net.jpg');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.createCreative).toHaveBeenCalledWith(CAMPAIGN_ID, {
      title: 'Интернет 100 Мбит/с',
      body: null,
      imageUrl: 'https://cdn.example.com/net.jpg',
      titleRu: 'Интернет 100 Мбит',
      bodyRu: null
    }));
    expect(await screen.findByText('Интернет 100 Мбит/с')).toBeInTheDocument();
  });

  it('картинку креатива можно загрузить файлом — адрес встаёт в поле сам', async () => {
    const client = makeClient();
    renderPage(client);
    await screen.findByText('Тахфиф ба ноутбукҳо');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить креатив' }));
    const dialog = screen.getByRole('dialog', { name: 'Новый креатив' });
    const file = new File([new Uint8Array([0x89, 0x50, 0x4e, 0x47])], 'banner.png', { type: 'image/png' });
    await userEvent.upload(dialog.querySelector('input[type=file]') as HTMLInputElement, file);

    await waitFor(() => expect(client.uploadImage).toHaveBeenCalledWith(file));
    expect(within(dialog).getByLabelText(/^Картинка/)).toHaveValue('https://media.test/platform/ad-creative/banner.png');
  });

  it('слишком большой файл — говорит, что сделать, и поле не трогает', async () => {
    const client = makeClient(campaign(), {
      uploadImage: mock(async () => { throw new PlatformApiError(400, 'media_too_large', 'media_too_large'); })
    });
    renderPage(client);
    await screen.findByText('Тахфиф ба ноутбукҳо');

    await userEvent.click(screen.getByRole('button', { name: 'Добавить креатив' }));
    const dialog = screen.getByRole('dialog', { name: 'Новый креатив' });
    await userEvent.upload(dialog.querySelector('input[type=file]') as HTMLInputElement, new File(['x'], 'big.png', { type: 'image/png' }));

    expect(await within(dialog).findByRole('alert')).toHaveTextContent('Файл слишком большой');
    expect(within(dialog).getByLabelText(/^Картинка/)).toHaveValue('');
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
