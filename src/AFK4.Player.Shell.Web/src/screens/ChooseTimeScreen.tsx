import { useMemo, useRef, useState } from 'react';
import {
  ShellBridgeRequestTypeNames,
  type MoneyDto,
  type PlayerShellStateDto,
  type PlayerStartOffersDto,
  type ShellAuthStateDto
} from '@afk4/contracts';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { formatMoney } from '@afk4/money';
import { apiBaseUrl, postJson } from '../api/playerApi';
import { useStartOffers } from '../api/useStartOffers';
import { requestHost } from '../host/shellHost';
import {
  INTL_LOCALES,
  clubTime,
  durationKey,
  packageMinuteOptions,
  startErrorKey,
  startRequest,
  type OfferChoice
} from '../model/offers';
import { SeatBadge } from '../ui/SeatBadge';

/**
 * Вошедший на свободном ПК выбирает время (спека, §3, кадр 02). Суммы считает сервер тем же
 * расчётом, что и списание: экран не должен пообещать одну цифру, а касса списать другую. Деньги
 * не празднуются раньше ответа — пока ждём, кнопка говорит «Запускаем…».
 */
export function ChooseTimeScreen({ state, auth }: { state: PlayerShellStateDto; auth: ShellAuthStateDto }) {
  const { t, locale } = useI18n();
  const baseUrl = apiBaseUrl(state);
  const { offers, failed, loading, reload } = useStartOffers(baseUrl);
  const [choice, setChoice] = useState<OfferChoice | null>(null);
  const [starting, setStarting] = useState(false);
  const [error, setError] = useState<MessageKey | null>(null);
  const [leaving, setLeaving] = useState(false);
  // Один ключ на один выбор: повтор после обрыва получит ту же сессию, а не вторую.
  const idempotencyKey = useRef<string | null>(null);

  const money = (value: MoneyDto) => formatMoney(value.minorUnits, value.currencyCode, INTL_LOCALES[locale]);
  const duration = (minutes: number) => {
    const { key, values } = durationKey(minutes);
    return t(key, values);
  };

  const choose = (next: OfferChoice) => {
    setChoice(next);
    setError(null);
    idempotencyKey.current = crypto.randomUUID();
  };

  const start = async () => {
    if (!choice || !baseUrl || starting) return;
    setStarting(true);
    setError(null);
    try {
      await postJson(baseUrl, '/api/me/sessions/start', startRequest(choice, idempotencyKey.current ?? crypto.randomUUID()));
      // Сессия началась — экран сменит состояние от агента, когда ПК откроется.
    } catch (reason) {
      const { key, reload: shouldReload } = startErrorKey(reason);
      setError(key);
      setStarting(false);
      if (shouldReload) {
        setChoice(null);
        reload();
      }
    }
  };

  const signOut = async () => {
    setLeaving(true);
    // Выход подтверждает событие auth.changed; не ответил хост — сервер всё равно погасит вход.
    await requestHost(ShellBridgeRequestTypeNames.AuthSignOut).catch(() => setLeaving(false));
  };

  return (
    <main className="choose-time">
      <header className="choose-time__top">
        <SeatBadge seatLabel={state.seatLabel} zoneName={state.zoneName} free />
        {offers ? (
          <span className="choose-time__balance">
            {t('playerShell.chooseTime.balance', { amount: money(offers.balance) })}
          </span>
        ) : null}
        <button type="button" className="btn btn--ghost" onClick={signOut} disabled={leaving || starting}>
          {t('playerShell.signOut')}
        </button>
      </header>

      <div className="choose-time__heading">
        <p className="choose-time__greeting">{t('playerShell.chooseTime.greeting', { name: auth.displayName ?? '' })}</p>
        <h1 className="choose-time__title">{t('playerShell.chooseTime.title')}</h1>
      </div>

      <section className="offers" aria-busy={loading}>
        {offers ? (
          <OfferList offers={offers} choice={choice} disabled={starting} onChoose={choose} money={money} duration={duration} />
        ) : failed ? (
          <div className="offers__failed" role="alert">
            <p>{t('playerShell.chooseTime.loadFailed')}</p>
            <button type="button" className="btn btn--ghost" onClick={reload}>{t('playerShell.chooseTime.retry')}</button>
          </div>
        ) : (
          <OfferSkeleton />
        )}
      </section>

      <footer className="choose-time__action">
        {error ? <p className="choose-time__error" role="alert">{t(error)}</p> : null}
        <button
          type="button"
          className="btn btn--primary choose-time__start"
          disabled={!choice || starting}
          onClick={start}
        >
          {starting
            ? t('playerShell.chooseTime.starting')
            : !choice
              ? t('playerShell.chooseTime.pick')
              : choice.kind === 'tariff'
                ? t('playerShell.chooseTime.start', { amount: money(choice.option.amount) })
                : t('playerShell.chooseTime.startPackage')}
        </button>
      </footer>
    </main>
  );
}

interface OfferListProps {
  offers: PlayerStartOffersDto;
  choice: OfferChoice | null;
  disabled: boolean;
  onChoose: (choice: OfferChoice) => void;
  money: (value: MoneyDto) => string;
  duration: (minutes: number) => string;
}

function OfferList({ offers, choice, disabled, onChoose, money, duration }: OfferListProps) {
  const { t, locale } = useI18n();
  const now = useMemo(() => Date.now(), [offers]);

  if (offers.tariffs.length === 0 && offers.packages.length === 0) {
    return <p className="offers__empty">{t('playerShell.chooseTime.empty')}</p>;
  }

  return (
    <>
      {offers.tariffs.map((tariff) => (
        <div key={tariff.tariffVersionId} className="offer-group">
          <div className="offer-group__head">
            <h2 className="offer-group__name">{tariff.name}</h2>
            <span className="offer-group__price">{t('playerShell.chooseTime.perHour', { amount: money(tariff.pricePerHour) })}</span>
          </div>
          {tariff.appliesNow ? (
            <div className="offer-group__options" role="group" aria-label={tariff.name}>
              {tariff.options.map((option) => {
                const chosen = choice?.kind === 'tariff'
                  && choice.tariff.tariffVersionId === tariff.tariffVersionId
                  && choice.option.minutes === option.minutes;
                const until = t('playerShell.chooseTime.until', { time: clubTime(option.endsAtUtc, offers.timeZone, locale) });
                const price = option.affordable
                  ? money(option.amount)
                  : t('playerShell.chooseTime.short', {
                      amount: money({ ...option.balanceAfter, minorUnits: -option.balanceAfter.minorUnits })
                    });
                return (
                  <button
                    key={option.minutes}
                    type="button"
                    className="offer-tile"
                    // Три строки плитки — одним именем через запятую, иначе диктор прочтёт «2 чдо 21:40».
                    aria-label={[duration(option.minutes), until, price].join(', ')}
                    aria-pressed={chosen}
                    disabled={disabled || !option.affordable}
                    onClick={() => onChoose({ kind: 'tariff', tariff, option })}
                  >
                    <span className="offer-tile__duration">{duration(option.minutes)}</span>
                    <span className="offer-tile__until">{until}</span>
                    <span className="offer-tile__amount mono">{price}</span>
                  </button>
                );
              })}
            </div>
          ) : tariff.startsAtUtc ? (
            <p className="offer-group__later">
              {t('playerShell.chooseTime.startsAt', { time: clubTime(tariff.startsAtUtc, offers.timeZone, locale) })}
            </p>
          ) : null}
        </div>
      ))}

      {offers.packages.length > 0 ? (
        <div className="offer-group">
          <div className="offer-group__head">
            <h2 className="offer-group__name">{t('playerShell.chooseTime.packages')}</h2>
          </div>
          {offers.packages.map((offer) => (
            <div key={offer.playerPackageId} className="offer-package">
              <p className="offer-package__name">
                {offer.name}
                <span className="offer-package__left">
                  {t('playerShell.chooseTime.packageLeft', { duration: duration(offer.remainingMinutes) })}
                </span>
              </p>
              <div className="offer-group__options" role="group" aria-label={offer.name}>
                {packageMinuteOptions(offer.remainingMinutes).map((minutes) => {
                  const chosen = choice?.kind === 'package'
                    && choice.offer.playerPackageId === offer.playerPackageId
                    && choice.minutes === minutes;
                  const all = minutes === offer.remainingMinutes;
                  const until = t('playerShell.chooseTime.until', {
                    time: clubTime(new Date(now + minutes * 60_000).toISOString(), offers.timeZone, locale)
                  });
                  return (
                    <button
                      key={minutes}
                      type="button"
                      className="offer-tile"
                      aria-label={[duration(minutes), until, ...(all ? [t('playerShell.chooseTime.packageAll')] : [])].join(', ')}
                      aria-pressed={chosen}
                      disabled={disabled}
                      onClick={() => onChoose({ kind: 'package', offer, minutes })}
                    >
                      <span className="offer-tile__duration">{duration(minutes)}</span>
                      <span className="offer-tile__until">{until}</span>
                      {all ? <span className="offer-tile__amount">{t('playerShell.chooseTime.packageAll')}</span> : null}
                    </button>
                  );
                })}
              </div>
            </div>
          ))}
        </div>
      ) : null}
    </>
  );
}

/** Та же геометрия, что у настоящего списка: экран не прыгает, когда приходят цены. */
function OfferSkeleton() {
  return (
    <div className="offer-group offer-group--skeleton" aria-hidden="true">
      <div className="offer-group__head">
        <span className="skeleton skeleton--title" />
      </div>
      <div className="offer-group__options">
        {[0, 1, 2, 3].map((index) => (
          <span key={index} className="offer-tile skeleton" />
        ))}
      </div>
    </div>
  );
}
