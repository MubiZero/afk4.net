import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import type { ShowcaseCardDto } from '@afk4/contracts';
import { ShellI18nProvider } from '../../i18n/ShellI18nProvider';
import { ShowcaseCarousel } from './ShowcaseCarousel';

const news: ShowcaseCardDto = { cardId: 'news:1', kind: 'news', title: 'Ночь CS2', body: 'В пятницу с 22:00' };
const tariff: ShowcaseCardDto = {
  cardId: 'tariff:1', kind: 'tariff', title: 'Ночной', price: { currencyCode: 'TJS', minorUnits: 600 }, timeWindow: '22:00–06:00'
};
const packages: ShowcaseCardDto = {
  cardId: 'packages:1', kind: 'packages', title: '',
  packages: [{ name: '3 часа', price: { currencyCode: 'TJS', minorUnits: 2500 }, minutes: 180 }]
};
const tournament: ShowcaseCardDto = { cardId: 'tournament:1', kind: 'tournament', title: 'Кубок зала', subtitle: 'Dota 2', startsAtUtc: '2026-09-27T14:00:00Z' };

function show(cards: ShowcaseCardDto[], paused = false, slideMs = 40) {
  return render(<ShellI18nProvider><ShowcaseCarousel cards={cards} paused={paused} slideMs={slideMs} /></ShellI18nProvider>);
}

describe('витрина свободного ПК', () => {
  afterEach(() => cleanup());

  it('сменяет карточки по кругу', async () => {
    show([news, tariff]);

    expect(screen.getByRole('heading', { name: 'Ночь CS2' })).toBeTruthy();
    await waitFor(() => expect(screen.getByRole('heading', { name: 'Ночной' })).toBeTruthy());
    // Цена часа и часы тарифа — одной строкой фактов.
    expect(screen.getByText(/в час · 22:00–06:00$/)).toBeTruthy();
  });

  it('под открытым окном входа стоит на месте', async () => {
    show([news, tariff], true);

    await new Promise((resolve) => setTimeout(resolve, 150));
    expect(screen.getByRole('heading', { name: 'Ночь CS2' })).toBeTruthy();
    expect(screen.queryByRole('heading', { name: 'Ночной' })).toBeNull();
  });

  it('пишет заголовок пакетов сама и говорит, что участие бесплатное', () => {
    show([packages]);
    expect(screen.getByRole('heading', { name: 'Пакеты времени' })).toBeTruthy();
    expect(screen.getByText('3 ч')).toBeTruthy();
    cleanup();

    show([tournament]);
    expect(screen.getByText('Турнир · Dota 2')).toBeTruthy();
    expect(screen.getByText(/Участие бесплатно$/)).toBeTruthy();
  });

  it('обновлённый список не сбрасывает показ с текущей карточки', async () => {
    const { rerender } = show([news, tariff], false, 60_000);

    rerender(<ShellI18nProvider><ShowcaseCarousel cards={[tariff, news]} paused={false} slideMs={60_000} /></ShellI18nProvider>);

    expect(screen.getByRole('heading', { name: 'Ночь CS2' })).toBeTruthy();
  });
});

describe('реклама в витрине', () => {
  afterEach(() => cleanup());

  it('подписана «Реклама» с рекламодателем и докладывает, сколько простояла', async () => {
    const ad: ShowcaseCardDto = { cardId: 'ad:1', kind: 'ad', title: 'Безлимит на месяц', advertiser: 'Сомон Телеком' };
    const shown: Array<[string, number]> = [];
    render(
      <ShellI18nProvider>
        <ShowcaseCarousel cards={[ad, news]} paused={false} slideMs={60} onShown={(card, ms) => shown.push([card.cardId, ms])} />
      </ShellI18nProvider>
    );

    expect(screen.getByText('Реклама · Сомон Телеком')).toBeTruthy();
    await waitFor(() => expect(shown.map(([id]) => id)).toContain('ad:1'));
    expect(shown[0][1]).toBeGreaterThanOrEqual(40);
  });

  // Закон о рекламе: таджикский первым, пометки — по-таджикски всегда и на языке экрана строкой ниже.
  it('ставит таджикский первым и пишет пометки закона на государственном языке', () => {
    const ad: ShowcaseCardDto = {
      cardId: 'ad:2', kind: 'ad', title: 'Бемаҳдуд барои як моҳ', advertiser: 'Сомон Телеком', secondaryTitle: 'Безлимит на месяц',
      seller: { legalName: 'ООО «Сомон Телеком»', taxId: '123456789', address: 'Душанбе' },
      requiresCertification: true, offerUntilUtc: '2026-10-31T00:00:00Z'
    };
    render(<ShellI18nProvider initialLocale="ru"><ShowcaseCarousel cards={[ad]} paused slideMs={60} /></ShellI18nProvider>);

    expect(screen.getByRole('heading', { name: 'Бемаҳдуд барои як моҳ' })).toBeTruthy();
    expect(screen.getByText('Безлимит на месяц')).toBeTruthy();
    expect(screen.getByText(/^Фурӯшанда: ООО «Сомон Телеком» · РМА 123456789 · Душанбе · Бояд ҳатман сертификатсия карда шавад · Пешниҳод то/)).toBeTruthy();
    expect(screen.getByText(/^Продавец: ООО «Сомон Телеком» · ИНН 123456789 · Душанбе · Подлежит обязательной сертификации · Предложение действует до/)).toBeTruthy();
  });

  it('на таджикском экране пометки не повторяются', () => {
    const ad: ShowcaseCardDto = { cardId: 'ad:3', kind: 'ad', title: 'Бемаҳдуд', advertiser: 'Сомон', requiresCertification: true };
    render(<ShellI18nProvider initialLocale="tg"><ShowcaseCarousel cards={[ad]} paused slideMs={60} /></ShellI18nProvider>);

    expect(screen.getAllByText('Бояд ҳатман сертификатсия карда шавад')).toHaveLength(1);
  });
});
