import { describe, expect, it, mock } from 'bun:test';
import { useState } from 'react';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import type { AdCampaignDto, AdCreativeDto, AdImpressionRowDto, AdvertiserDto } from '@/api/types';
import type { AdsTab } from '@/routing/platformRoute';
import { AdsScreen, type AdsClient } from './AdsScreen';

const ADVERTISER_ID = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
const CAMPAIGN_ID = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';

function advertiser(overrides: Partial<AdvertiserDto> = {}): AdvertiserDto {
  return { advertiserId: ADVERTISER_ID, name: 'Техномир', contact: 'Фаррух, +992 93 000 00 00', createdAtUtc: '2026-09-01T10:00:00Z', ...overrides };
}

function creative(overrides: Partial<AdCreativeDto> = {}): AdCreativeDto {
  return {
    creativeId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    campaignId: CAMPAIGN_ID,
    title: 'Скидка на ноутбуки',
    body: null,
    imageUrl: null,
    moderation: 'approved',
    rejectedReason: null,
    moderatedAtUtc: null,
    createdAtUtc: '2026-09-01T10:00:00Z',
    ...overrides
  };
}

// Даты с запасом в обе стороны: фаза кампании считается от «сейчас», и тест не должен стареть.
function campaign(overrides: Partial<AdCampaignDto> = {}): AdCampaignDto {
  return {
    campaignId: CAMPAIGN_ID,
    advertiserId: ADVERTISER_ID,
    advertiserName: 'Техномир',
    name: 'Осень в Техномире',
    category: 'electronics',
    startsAtUtc: '2020-01-01T00:00:00Z',
    endsAtUtc: '2099-01-01T00:00:00Z',
    cities: [],
    organizationIds: [],
    state: 'active',
    creatives: [creative(), creative({ creativeId: 'dddddddd-dddd-dddd-dddd-dddddddddddd', moderation: 'pending' })],
    createdAtUtc: '2026-09-01T10:00:00Z',
    updatedAtUtc: '2026-09-01T10:00:00Z',
    ...overrides
  };
}

const draftCampaign = campaign({
  campaignId: 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee',
  name: 'Курсы программирования',
  category: 'education',
  state: 'draft',
  creatives: []
});

function reportRow(overrides: Partial<AdImpressionRowDto> = {}): AdImpressionRowDto {
  return {
    day: '2026-09-24',
    campaignId: CAMPAIGN_ID,
    campaignName: 'Осень в Техномире',
    creativeId: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
    creativeTitle: 'Скидка на ноутбуки',
    organizationId: '11111111-1111-1111-1111-111111111111',
    organizationName: 'Кибер Арена',
    branchId: '22222222-2222-2222-2222-222222222222',
    branchName: 'Центр',
    city: 'Душанбе',
    impressions: 1200,
    shownSeconds: 10800,
    ...overrides
  };
}

function makeClient(overrides: Partial<AdsClient> = {}): AdsClient {
  return {
    listAdvertisers: mock(async () => [advertiser()]),
    createAdvertiser: mock(async () => advertiser()),
    updateAdvertiser: mock(async () => advertiser()),
    listCampaigns: mock(async () => [campaign(), draftCampaign]),
    createCampaign: mock(async () => campaign({ campaignId: 'ffffffff-ffff-ffff-ffff-ffffffffffff' })),
    report: mock(async () => [reportRow(), reportRow({ day: '2026-09-23', impressions: 300, shownSeconds: 2700, branchName: 'Сино' })]),
    ...overrides
  };
}

function renderScreen(client: AdsClient, initialTab: AdsTab = 'campaigns', onOpenCampaign = mock((_id: string) => {})) {
  function Harness() {
    const [tab, setTab] = useState<AdsTab>(initialTab);
    return <AdsScreen client={client} tab={tab} onTabChange={setTab} onOpenCampaign={onOpenCampaign} />;
  }
  render(
    <I18nProvider>
      <ToastProvider>
        <Harness />
      </ToastProvider>
    </I18nProvider>
  );
  return { onOpenCampaign };
}

function rowOf(name: string): HTMLElement {
  const row = screen.getAllByRole('row').find(candidate => candidate.querySelector('td')?.textContent === name);
  if (row === undefined) throw new Error(`no row for ${name}`);
  return row;
}

describe('AdsScreen — кампании', () => {
  it('показывает, крутится ли кампания и сколько креативов одобрено', async () => {
    const { onOpenCampaign } = renderScreen(makeClient());
    await screen.findByText('Осень в Техномире');

    const running = rowOf('Осень в Техномире');
    expect(within(running).getByText('Техномир')).toBeInTheDocument();
    expect(within(running).getByText('Электроника')).toBeInTheDocument();
    expect(within(running).getByText('Идёт')).toBeInTheDocument();
    expect(running).toHaveTextContent('Одобрено 1 из 2 · 1 ждёт проверки');

    const draft = rowOf('Курсы программирования');
    expect(within(draft).getByText('Обучение')).toBeInTheDocument();
    expect(within(draft).getByText('Черновик')).toBeInTheDocument();
    expect(within(draft).getByText('Креативов нет')).toBeInTheDocument();

    await userEvent.click(within(draft).getByRole('button', { name: 'Открыть' }));
    expect(onOpenCampaign).toHaveBeenCalledWith('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee');
  });

  // Правка креатива снимает одобрение — кампания «запущена», а на ПК её нет.
  it('запущенную кампанию без одобренного креатива называет «нечего показывать»', async () => {
    renderScreen(makeClient({ listCampaigns: mock(async () => [campaign({ creatives: [creative({ moderation: 'pending' })] })]) }));
    await screen.findByText('Осень в Техномире');
    expect(within(rowOf('Осень в Техномире')).getByText('Нечего показывать')).toBeInTheDocument();
  });

  it('без рекламодателей ведёт сначала завести рекламодателя', async () => {
    const client = makeClient({ listCampaigns: mock(async () => []), listAdvertisers: mock(async () => []) });
    renderScreen(client);

    expect(await screen.findByText(/сначала заведите рекламодателя/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Новая кампания' })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'К рекламодателям' }));

    expect(await screen.findByRole('button', { name: 'Новый рекламодатель' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Рекламодатели' })).toHaveAttribute('aria-selected', 'true');
  });

  it('заводит кампанию и открывает её страницу — следующий шаг там', async () => {
    const client = makeClient();
    const { onOpenCampaign } = renderScreen(client);
    await screen.findByText('Осень в Техномире');

    await userEvent.click(screen.getByRole('button', { name: 'Новая кампания' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая кампания' });
    // Рекламодатель один — он уже выбран.
    expect(within(dialog).getByLabelText('Рекламодатель')).toHaveValue(ADVERTISER_ID);
    await userEvent.type(within(dialog).getByLabelText(/^Название кампании/), '  Зима ');
    await userEvent.selectOptions(within(dialog).getByLabelText(/^Категория/), 'telecom');
    await userEvent.type(within(dialog).getByLabelText(/^Города/), 'Душанбе, Худжанд');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.createCampaign).toHaveBeenCalledTimes(1));
    const request = (client.createCampaign as ReturnType<typeof mock>).mock.calls[0][0] as Parameters<AdsClient['createCampaign']>[0];
    expect(request).toMatchObject({
      advertiserId: ADVERTISER_ID,
      name: 'Зима',
      category: 'telecom',
      cities: ['Душанбе', 'Худжанд'],
      organizationIds: []
    });
    expect(new Date(request.endsAtUtc).getTime() - new Date(request.startsAtUtc).getTime()).toBe(30 * 24 * 60 * 60 * 1000);
    await waitFor(() => expect(onOpenCampaign).toHaveBeenCalledWith('ffffffff-ffff-ffff-ffff-ffffffffffff'));
  });

  it('не отправляет кампанию, которая кончается раньше, чем начинается, и говорит это у поля', async () => {
    const client = makeClient();
    renderScreen(client);
    await screen.findByText('Осень в Техномире');

    await userEvent.click(screen.getByRole('button', { name: 'Новая кампания' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая кампания' });
    await userEvent.type(within(dialog).getByLabelText(/^Название кампании/), 'Зима');
    const ends = within(dialog).getByLabelText('Конец');
    fireEvent.change(ends, { target: { value: '2020-01-01T10:00' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(client.createCampaign).not.toHaveBeenCalled();
    expect(within(dialog).getByText('Конец — позже начала.')).toBeInTheDocument();
    expect(ends).toHaveAttribute('aria-invalid', 'true');
    expect(ends).toHaveFocus();
  });

  it('опечатку в id клуба называет до отправки', async () => {
    const client = makeClient();
    renderScreen(client);
    await screen.findByText('Осень в Техномире');

    await userEvent.click(screen.getByRole('button', { name: 'Новая кампания' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая кампания' });
    await userEvent.type(within(dialog).getByLabelText(/^Название кампании/), 'Зима');
    await userEvent.type(within(dialog).getByLabelText(/^Только эти клубы/), 'кибер-арена');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    expect(client.createCampaign).not.toHaveBeenCalled();
    expect(within(dialog).getByText(/только id организаций вида 8-4-4-4-12/)).toBeInTheDocument();
  });

  it('отказ сервера показывает своей фразой и не закрывает форму', async () => {
    const client = makeClient({
      createCampaign: mock(async () => {
        throw new PlatformApiError(400, 'Advertiser was not found.', 'ad_invalid');
      })
    });
    renderScreen(client);
    await screen.findByText('Осень в Техномире');

    await userEvent.click(screen.getByRole('button', { name: 'Новая кампания' }));
    const dialog = screen.getByRole('dialog', { name: 'Новая кампания' });
    await userEvent.type(within(dialog).getByLabelText(/^Название кампании/), 'Зима');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    const alert = await within(dialog).findByRole('alert');
    expect(alert).toHaveTextContent('Сервер не принял данные');
    expect(alert).not.toHaveTextContent('Advertiser was not found');
    expect(within(dialog).getByLabelText(/^Название кампании/)).toHaveValue('Зима');
  });

  it('при сбое загрузки даёт повторить', async () => {
    let calls = 0;
    const client = makeClient({
      listCampaigns: mock(async () => {
        calls += 1;
        if (calls === 1) throw new PlatformApiError(500, 'boom', null);
        return [campaign()];
      })
    });
    renderScreen(client);

    expect(await screen.findByText('Не удалось загрузить кампании')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Повторить' }));
    expect(await screen.findByText('Осень в Техномире')).toBeInTheDocument();
  });
});

describe('AdsScreen — рекламодатели', () => {
  it('показывает контакт как внутренний и заводит рекламодателя', async () => {
    const client = makeClient();
    renderScreen(client, 'advertisers');
    await screen.findByText('Фаррух, +992 93 000 00 00');
    expect(screen.getByRole('columnheader', { name: 'Контакт (для платформы)' })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Новый рекламодатель' }));
    const dialog = screen.getByRole('dialog', { name: 'Новый рекламодатель' });
    expect(within(dialog).getByText(/Клубы и игроки этого не видят/)).toBeInTheDocument();
    await userEvent.type(within(dialog).getByLabelText(/^Название/), ' Сомон Телеком ');
    await userEvent.type(within(dialog).getByLabelText(/^Контакт — только для платформы/), 'Отдел рекламы, ads@somon.tj');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.createAdvertiser).toHaveBeenCalledWith({ name: 'Сомон Телеком', contact: 'Отдел рекламы, ads@somon.tj' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(client.listAdvertisers).toHaveBeenCalledTimes(2);
  });

  it('правка сохраняет рекламодателя по id', async () => {
    const client = makeClient();
    renderScreen(client, 'advertisers');
    await screen.findByText('Фаррух, +992 93 000 00 00');

    await userEvent.click(within(rowOf('Техномир')).getByRole('button', { name: 'Изменить' }));
    const dialog = screen.getByRole('dialog', { name: 'Изменить рекламодателя' });
    const contact = within(dialog).getByLabelText(/^Контакт — только для платформы/);
    await userEvent.clear(contact);
    await userEvent.click(within(dialog).getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateAdvertiser).toHaveBeenCalledWith(ADVERTISER_ID, { name: 'Техномир', contact: null }));
  });
});

describe('AdsScreen — отчёт', () => {
  it('показывает строки за последние 30 дней и итог', async () => {
    const client = makeClient();
    renderScreen(client, 'report');

    await screen.findByText('Сино');
    const [query] = (client.report as ReturnType<typeof mock>).mock.calls[0] as [Parameters<AdsClient['report']>[0]];
    expect(query.campaignId).toBeNull();
    expect((Date.parse(query.to) - Date.parse(query.from)) / (24 * 60 * 60 * 1000)).toBe(29);

    const first = screen.getAllByRole('row').find(row => row.textContent?.includes('Центр'));
    expect(first).toHaveTextContent('2026-09-24');
    expect(first).toHaveTextContent('Кибер Арена');
    expect(first).toHaveTextContent('Душанбе');
    const total = screen.getByText('Итого').closest('tr');
    expect(total).not.toBeNull();
    // 1 200 + 300 показов, 10 800 + 2 700 секунд; разделитель разрядов — узкий пробел локали.
    expect(total!.textContent?.replace(/\s/g, '')).toBe('Итого150013500');
  });

  it('фильтр по кампании уходит на сервер, а пустой ответ предлагает сбросить фильтр', async () => {
    const client = makeClient({
      report: mock(async (query: Parameters<AdsClient['report']>[0]) => (query.campaignId === null ? [reportRow()] : []))
    });
    renderScreen(client, 'report');
    await screen.findByText('Центр');
    await screen.findByRole('option', { name: 'Курсы программирования' });

    await userEvent.selectOptions(screen.getByLabelText('Кампания'), 'Курсы программирования');
    await userEvent.click(screen.getByRole('button', { name: 'Показать' }));

    expect(await screen.findByText('У этой кампании за эти дни показов нет.')).toBeInTheDocument();
    const lastCall = (client.report as ReturnType<typeof mock>).mock.calls.at(-1) as [Parameters<AdsClient['report']>[0]];
    expect(lastCall[0].campaignId).toBe('eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee');

    await userEvent.click(screen.getByRole('button', { name: 'Сбросить фильтр' }));
    expect(await screen.findByText('Центр')).toBeInTheDocument();
    expect(screen.getByLabelText('Кампания')).toHaveValue('');
  });

  it('период длиннее 92 дней не отправляет и говорит почему', async () => {
    const client = makeClient();
    renderScreen(client, 'report');
    await screen.findByText('Центр');

    fireEvent.change(screen.getByLabelText('Первый день'), { target: { value: '2026-01-01' } });
    fireEvent.change(screen.getByLabelText('Последний день'), { target: { value: '2026-09-01' } });
    await userEvent.click(screen.getByRole('button', { name: 'Показать' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Период — не длиннее 92 дней.');
    expect(client.report).toHaveBeenCalledTimes(1);
  });

  it('без показов говорит, когда ждать свежие', async () => {
    renderScreen(makeClient({ report: mock(async () => []) }), 'report');
    expect(await screen.findByText(/^За эти дни показов нет\. ПК присылают показы раз в час/)).toBeInTheDocument();
  });
});
