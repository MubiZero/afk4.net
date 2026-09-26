import { useEffect, useRef, useState } from 'react';
import type { MoneyDto, ShowcaseCardDto } from '@afk4/contracts';
import { ShowcaseCardKindNames } from '@afk4/contracts';
import { createCatalogTranslator, useI18n, type MessageKey } from '@afk4/i18n';
import { playerShellCatalog } from '@afk4/i18n/catalogs/player-shell';
import { formatMoney } from '@afk4/money';
import { INTL_LOCALES } from '../../model/offers';

/** Сколько карточка стоит на экране. Дольше — витрина кажется застывшей, короче — мельтешит. */
export const SHOWCASE_SLIDE_MS = 9000;

// Растворение уходящей карточки; после него её слой снимается, чтобы не держать лишнюю картинку.
const FADE_MS = 700;

const KIND_LABEL: Partial<Record<string, MessageKey>> = {
  [ShowcaseCardKindNames.News]: 'playerShell.showcase.kind.news',
  [ShowcaseCardKindNames.Tariff]: 'playerShell.showcase.kind.tariff',
  [ShowcaseCardKindNames.Product]: 'playerShell.showcase.kind.product',
  [ShowcaseCardKindNames.BarHit]: 'playerShell.showcase.kind.barHit',
  [ShowcaseCardKindNames.Tournament]: 'playerShell.showcase.kind.tournament',
  [ShowcaseCardKindNames.Ad]: 'playerShell.showcase.kind.ad'
};

/**
 * Витрина свободного ПК (спека, §5.7 и §8): карточки клуба сменяют друг друга растворением —
 * только прозрачность, ничего в кадре не пересчитывается. Под открытым окном входа карусель
 * стоит: человек читает окно, а не следит за сменой картинок. Следующая картинка декодируется
 * заранее, чтобы смена не мигала пустым кадром.
 */
export function ShowcaseCarousel({
  cards,
  paused,
  slideMs = SHOWCASE_SLIDE_MS,
  onShown
}: {
  cards: readonly ShowcaseCardDto[];
  paused: boolean;
  slideMs?: number;
  // Карточка ушла с экрана (или экран сменился) — сколько она простояла. Счёт ведёт агент.
  onShown?: (card: ShowcaseCardDto, shownMs: number) => void;
}) {
  const [currentId, setCurrentId] = useState<string | null>(cards[0]?.cardId ?? null);
  const [leavingId, setLeavingId] = useState<string | null>(null);

  // Список обновился (агент докачал витрину): показ продолжается с той же карточки, если она
  // осталась, иначе — с первой.
  const index = Math.max(0, cards.findIndex((card) => card.cardId === currentId));
  const current = cards[index] ?? null;

  // Таймер читает список и текущую карточку из ref: иначе он перезапускался бы с каждым
  // пульсом состояния, и карточка не доживала бы до смены.
  const latest = useRef({ cards, currentId: current?.cardId ?? null });
  latest.current = { cards, currentId: current?.cardId ?? null };

  useEffect(() => {
    if (paused || cards.length < 2) return undefined;
    const timer = setInterval(() => {
      const { cards: list, currentId: shownId } = latest.current;
      if (list.length < 2) return;
      const at = Math.max(0, list.findIndex((card) => card.cardId === shownId));
      setLeavingId(shownId);
      setCurrentId(list[(at + 1) % list.length].cardId);
    }, slideMs);
    return () => clearInterval(timer);
  }, [paused, cards.length, slideMs]);

  useEffect(() => {
    if (leavingId === null) return undefined;
    const timer = setTimeout(() => setLeavingId(null), FADE_MS);
    return () => clearTimeout(timer);
  }, [leavingId]);

  // Сколько карточка простояла — от её появления до ухода или до смены экрана.
  const onShownRef = useRef(onShown);
  onShownRef.current = onShown;
  useEffect(() => {
    if (current === null) return undefined;
    const card = current;
    const shownSince = performance.now();
    return () => onShownRef.current?.(card, Math.round(performance.now() - shownSince));
    // Только смена карточки: новый объект той же карточки из следующего пульса — не новый показ.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [current?.cardId]);

  const nextImage = cards.length > 1 ? cards[(index + 1) % cards.length]?.imageUrl : null;
  useEffect(() => {
    if (!nextImage) return;
    const image = new Image();
    image.src = nextImage;
    image.decode?.().catch(() => undefined);
  }, [nextImage]);

  if (current === null) return null;
  const leaving = leavingId !== null && leavingId !== current.cardId ? cards.find((card) => card.cardId === leavingId) : undefined;

  return (
    <div className="showcase" aria-live="off">
      {leaving ? <ShowcaseSlide key={leaving.cardId} card={leaving} state="leaving" /> : null}
      <ShowcaseSlide key={current.cardId} card={current} state="current" />
    </div>
  );
}

function ShowcaseSlide({ card, state }: { card: ShowcaseCardDto; state: 'current' | 'leaving' }) {
  const { t, locale } = useI18n();
  const intlLocale = INTL_LOCALES[locale];
  const money = (value: MoneyDto) => formatMoney(value.minorUnits, value.currencyCode, intlLocale);
  const kindKey = KIND_LABEL[card.kind];
  const kind = kindKey ? t(kindKey) : null;
  const title = card.kind === ShowcaseCardKindNames.Packages && !card.title ? t('playerShell.showcase.packagesTitle') : card.title;

  return (
    <article className={`showcase-slide showcase-slide--${state}${card.imageUrl ? ' showcase-slide--image' : ''}`} aria-hidden={state === 'leaving' || undefined}>
      {card.imageUrl ? <img className="showcase-slide__image" src={card.imageUrl} alt="" decoding="async" /> : null}
      <div className="showcase-slide__text">
        {kind ? <p className="showcase-slide__kind">{kindLine(kind, card)}</p> : null}
        <h2 className="showcase-slide__title">{title}</h2>
        {card.body ? <p className="showcase-slide__body">{card.body}</p> : null}
        {card.secondaryTitle || card.secondaryBody ? (
          <p className="showcase-slide__secondary" lang="ru">
            {[card.secondaryTitle, card.secondaryBody].filter(Boolean).join(' — ')}
          </p>
        ) : null}
        {card.packages && card.packages.length > 0 ? (
          <ul className="showcase-slide__packages">
            {card.packages.map((line) => (
              <li key={`${line.name}-${line.minutes}`}>
                <span className="showcase-slide__package-name">{line.name}</span>
                <span className="showcase-slide__package-time">{duration(line.minutes, t)}</span>
                <span className="showcase-slide__package-price">{money(line.price)}</span>
              </li>
            ))}
          </ul>
        ) : null}
        <SlideFacts card={card} money={money} intlLocale={intlLocale} />
        {card.kind === ShowcaseCardKindNames.Ad ? <AdDisclosures card={card} /> : null}
      </div>
    </article>
  );
}

// Пометки закона на государственном языке: карточка их пишет по-таджикски всегда, а если экран
// на другом языке — ещё и на нём строкой ниже (закон о рекламе, ст. 5, 14(1), 26).
const TAJIK = createCatalogTranslator(playerShellCatalog, 'tg');

function AdDisclosures({ card }: { card: ShowcaseCardDto }) {
  const { t, locale } = useI18n();
  const lines = (translate: typeof t) => {
    const parts: string[] = [];
    if (card.seller) parts.push(translate('playerShell.showcase.ad.seller', { ...card.seller }));
    if (card.requiresCertification) parts.push(translate('playerShell.showcase.ad.certification'));
    if (card.offerUntilUtc) parts.push(translate('playerShell.showcase.ad.offerUntil', { date: numericDay(card.offerUntilUtc) }));
    return parts.join(' · ');
  };

  const tajik = lines(TAJIK as typeof t);
  if (!tajik) return null;
  const local = locale === 'tg' ? '' : lines(t);
  return (
    <div className="showcase-slide__legal">
      <p lang="tg">{tajik}</p>
      {local ? <p>{local}</p> : null}
    </div>
  );
}

// «31.10.2026» — одинаково на всех языках: таджикских названий месяцев в Intl браузера нет
// (tg-TJ сводится к en-US), а по-английски срок в таджикской строке выглядел бы ошибкой.
function numericDay(iso: string): string {
  const date = new Date(iso);
  const pad = (value: number) => String(value).padStart(2, '0');
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()}`;
}

// «Турнир · Dota 2», «Реклама · Сомон Телеком»: у рекламы рядом с меткой всегда рекламодатель (PRD).
function kindLine(kind: string, card: ShowcaseCardDto): string {
  const detail = card.kind === ShowcaseCardKindNames.Ad ? card.advertiser : card.subtitle;
  return detail ? `${kind} · ${detail}` : kind;
}

function SlideFacts({ card, money, intlLocale }: { card: ShowcaseCardDto; money: (value: MoneyDto) => string; intlLocale: string }) {
  const { t } = useI18n();
  const facts: string[] = [];
  if (card.kind === ShowcaseCardKindNames.Tariff && card.price) {
    facts.push(t('playerShell.showcase.perHour', { price: money(card.price) }));
    if (card.timeWindow) facts.push(card.timeWindow);
  } else if (card.kind === ShowcaseCardKindNames.Tournament) {
    if (card.startsAtUtc) facts.push(formatStart(card.startsAtUtc, intlLocale));
    facts.push(card.price ? t('playerShell.showcase.entryFee', { price: money(card.price) }) : t('playerShell.showcase.entryFree'));
  } else if (card.price) {
    facts.push(money(card.price));
  }

  if (facts.length === 0) return null;
  return <p className="showcase-slide__facts">{facts.join(' · ')}</p>;
}

function duration(minutes: number, t: ReturnType<typeof useI18n>['t']): string {
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  if (hours === 0) return t('playerShell.showcase.durationMinutes', { minutes: rest });
  return rest === 0
    ? t('playerShell.showcase.durationHours', { hours })
    : t('playerShell.showcase.durationHoursMinutes', { hours, minutes: rest });
}

// Время турнира — по часам ПК: ПК стоит в клубе, и его часы и есть время клуба.
function formatStart(iso: string, intlLocale: string): string {
  try {
    return new Intl.DateTimeFormat(intlLocale, { weekday: 'short', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' })
      .format(new Date(iso));
  } catch {
    return new Date(iso).toLocaleString();
  }
}
