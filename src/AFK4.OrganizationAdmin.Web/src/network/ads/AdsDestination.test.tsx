import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { ClubAdDto, ClubAdsDto } from '@afk4/contracts';
import { AdsDestination } from './AdsDestination';

afterEach(() => cleanup());

const running: ClubAdDto = {
  creativeId: 'c1', advertiser: 'Сомон Телеком', category: 'telecom',
  title: 'Интернети бемаҳдуд', body: 'Барои бозигарон', titleRu: 'Безлимит', bodyRu: 'Для игроков', imageUrl: null,
  startsAtUtc: '2026-09-16T00:00:00Z', endsAtUtc: '2026-10-31T00:00:00Z', running: true,
  impressions: 1240, shownSeconds: 11160, lastShownDay: '2026-09-26',
  seller: { legalName: 'ООО «Сомон Телеком»', taxId: '123456789', address: 'Душанбе' },
  requiresCertification: true, offerUntilUtc: '2026-10-31T00:00:00Z'
};
const ended: ClubAdDto = {
  ...running, creativeId: 'c2', advertiser: 'Техномир', category: 'electronics', title: 'Тахфиф', body: null, titleRu: null, bodyRu: null,
  running: false, impressions: 0, shownSeconds: 0, lastShownDay: null, seller: null, requiresCertification: false, offerUntilUtc: null
};

function show(data: ClubAdsDto, afterReport?: ClubAdsDto) {
  const client = { listPlatformAds: mock(async () => data), reportPlatformAd: mock(async () => afterReport ?? data) };
  render(<I18nProvider initialLocale="ru"><AdsDestination backend={null} client={client} /></I18nProvider>);
  return client;
}

describe('Сеть → Реклама', () => {
  // Клуб — тоже распространитель: видит, что идёт на его ПК, тем же видом, что игрок.
  it('показывает идущую рекламу: таджикский первым, показы на ПК клуба и пометки закона', async () => {
    show({ adsEnabled: true, from: '2026-08-28', to: '2026-09-26', ads: [running, ended] });

    const card = (await screen.findByText('Интернети бемаҳдуд')).closest('li')!;
    expect(within(card).getByText('Реклама · Сомон Телеком')).toBeInTheDocument();
    expect(within(card).getByText('Идёт на ваших ПК')).toBeInTheDocument();
    const tajik = within(card).getByText('Интернети бемаҳдуд');
    const russian = within(card).getByText('Безлимит — Для игроков');
    expect(tajik.compareDocumentPosition(russian) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(within(card).getByText(/^1\s?240 показов · 186 мин на экране$/)).toBeInTheDocument();
    expect(within(card).getByText(/^Продавец: ООО «Сомон Телеком» · ИНН 123456789 · Душанбе · Подлежит обязательной сертификации · Предложение действует до/))
      .toBeInTheDocument();
    expect(screen.getByText(/распространитель рекламы/)).toBeInTheDocument();

    const old = screen.getByText('Тахфиф').closest('li')!;
    expect(within(old).getByText('Больше не показывается')).toBeInTheDocument();
    expect(within(old).getByText('Ещё не показывалась')).toBeInTheDocument();
  });

  it('показывает ответ AFK4 на закрытую жалобу', async () => {
    show({ adsEnabled: true, from: '2026-08-28', to: '2026-09-26', ads: [{ ...running, complaintAnswer: 'Проверили — реклама допустима' }] });

    expect(await screen.findByText('Ответ AFK4 на вашу жалобу: Проверили — реклама допустима')).toBeInTheDocument();
  });

  it('на тарифе без рекламы говорит, почему её нет', async () => {
    show({ adsEnabled: false, from: '2026-08-28', to: '2026-09-26', ads: [] });

    expect(await screen.findByText('На вашем тарифе рекламы платформы нет')).toBeInTheDocument();
    expect(screen.getByText('Реклама идёт только на бесплатном тарифе.')).toBeInTheDocument();
  });

  it('на бесплатном тарифе без кампаний — спокойное «пока нет»', async () => {
    show({ adsEnabled: true, from: '2026-08-28', to: '2026-09-26', ads: [] });

    expect(await screen.findByText('Сейчас на ваших ПК рекламы нет')).toBeInTheDocument();
  });

  // Снять рекламу клуб не может — жалуется платформе; «Отправлено» — только после ответа сервера.
  it('жалоба: причина обязательна, после отправки карточка говорит, что AFK4 проверит', async () => {
    const client = show(
      { adsEnabled: true, from: '2026-08-28', to: '2026-09-26', ads: [running] },
      { adsEnabled: true, from: '2026-08-28', to: '2026-09-26', ads: [{ ...running, complaintOpen: true }] }
    );

    const card = (await screen.findByText('Интернети бемаҳдуд')).closest('li')!;
    fireEvent.click(within(card).getByRole('button', { name: 'Пожаловаться' }));
    const send = screen.getByRole('button', { name: 'Отправить жалобу' });
    expect(send).toBeDisabled();
    fireEvent.click(screen.getByLabelText('Не подходит детям и подросткам'));
    fireEvent.change(screen.getByLabelText('Подробности (необязательно)'), { target: { value: 'Ночью у детей' } });
    fireEvent.click(send);

    await waitFor(() => expect(client.reportPlatformAd).toHaveBeenCalledWith('c1', 'minors', 'Ночью у детей'));
    expect(await screen.findByText('Жалоба отправлена. AFK4 проверит рекламу и ответит.')).toBeInTheDocument();
    const after = screen.getByText('Интернети бемаҳдуд').closest('li')!;
    expect(within(after).getByText('Жалоба отправлена — AFK4 проверит')).toBeInTheDocument();
    expect(within(after).queryByRole('button', { name: 'Пожаловаться' })).toBeNull();
  });
});
