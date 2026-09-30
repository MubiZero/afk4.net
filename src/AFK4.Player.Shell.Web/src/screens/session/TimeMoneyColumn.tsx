import type { MoneyDto, PlayerShellStateDto, ShopOrderDto } from '@afk4/contracts';
import { formatMoney } from '@afk4/money';
import { useI18n } from '@afk4/i18n';
import { ORDER_STATUS_KEYS } from '../../model/bar';
import { INTL_LOCALES, clubTime } from '../../model/offers';
import type { SessionRole } from '../../model/session';
import { AppWindow, LogOut } from 'lucide-react';
import { AssistButton } from '../../ui/AssistButton';
import { Countdown, Elapsed } from '../../ui/Countdown';
import { sessionUntilUtc } from '../../model/sessionTime';

interface TimeMoneyColumnProps {
  state: PlayerShellStateDto;
  receivedAtMs: number | null;
  role: SessionRole;
  /** На ПК кто-то вошёл: выйти ему можно при любой сессии, даже чужой. */
  signedIn: boolean;
  /** Баланс владельца; null — не узнали или сессия не его: строки нет. */
  balance?: MoneyDto | null;
  /** Заказ бара в работе — статус виден, на какой бы вкладке ни был человек. */
  activeOrder?: ShopOrderDto | null;
  onOpenBar?: () => void;
  /** Без связи с клубом продлевать и заканчивать нечем — сервер недоступен. */
  offline: boolean;
  onExtend: () => void;
  onEndEarly: () => void;
  onSignIn: () => void;
  onSignOut: () => void;
  /** «Мои приложения»: что игрок запустил из библиотеки — вернуться в окно или закрыть. */
  onOpenApps?: () => void;
  /** Сколько запущенных из библиотеки ещё работает — число на кнопке. */
  runningApps?: number;
}

/**
 * Колонка «время и деньги» (кадр 03 концепта, каркас как у Senet): сколько осталось, до скольки и
 * что с этим можно сделать. Денежные кнопки — только у того, чья это сессия.
 */
export function TimeMoneyColumn({
  state,
  receivedAtMs,
  role,
  signedIn,
  balance = null,
  activeOrder = null,
  onOpenBar = () => {},
  offline,
  onExtend,
  onEndEarly,
  onSignIn,
  onSignOut,
  onOpenApps,
  runningApps = 0
}: TimeMoneyColumnProps) {
  const { t, locale } = useI18n();
  const until = sessionUntilUtc(state);

  return (
    <aside className="time-money" aria-label={t('playerShell.session.remaining')}>
      <div className="time-money__time">
        {until ? (
          <>
            <span className="time-money__label">{t('playerShell.session.remaining')}</span>
            <Countdown
              className="time-money__countdown"
              untilUtc={until}
              observedAtUtc={state.observedAtUtc}
              receivedAtMs={receivedAtMs}
            />
            <span className="time-money__until">
              {t('playerShell.session.until', { time: clubTime(until, undefined, locale) })}
            </span>
          </>
        ) : (
          // Открытый счёт: конца нет, честно показать можно только, сколько уже идёт.
          <>
            <span className="time-money__label">{t('playerShell.session.elapsed')}</span>
            <Elapsed
              className="time-money__countdown"
              sinceUtc={state.sessionStartedAtUtc}
              observedAtUtc={state.observedAtUtc}
              receivedAtMs={receivedAtMs}
            />
          </>
        )}
      </div>

      {/* Сколько денег осталось — рядом с тем, сколько времени: решать, продлевать ли, по одному
          взгляду на колонку. */}
      {balance ? (
        <p className="time-money__balance">
          <span className="time-money__label">{t('playerShell.session.balance')}</span>
          <span className="time-money__balance-amount">{formatMoney(balance.minorUnits, balance.currencyCode, INTL_LOCALES[locale])}</span>
        </p>
      ) : null}

      {activeOrder ? (
        <button type="button" className="time-money__order" onClick={onOpenBar} aria-live="polite">
          <span className="time-money__label">{t('playerShell.bar.orderTitle')}</span>
          <span className="time-money__order-status">{t(ORDER_STATUS_KEYS[activeOrder.status] ?? 'playerShell.bar.status.placed')}</span>
        </button>
      ) : null}

      {onOpenApps ? (
        // Кнопка — выше денежных действий и у любой роли: вернуться в игру или закрыть зависшую
        // нужно и гостю стойки. Диспетчера задач Windows в сессии нет, это его замена.
        <button type="button" className="btn btn--ghost btn--wide time-money__apps" onClick={onOpenApps}>
          <AppWindow aria-hidden="true" />
          {t('playerShell.apps.open')}
          {runningApps > 0 ? <span className="time-money__apps-count" aria-hidden="true">{runningApps}</span> : null}
        </button>
      ) : null}

      {role === 'owner' ? (
        <div className="time-money__actions">
          <button type="button" className="btn btn--primary btn--wide" onClick={onExtend} disabled={offline}>
            {t('playerShell.session.extend')}
          </button>
          <button type="button" className="btn btn--ghost btn--wide" onClick={onEndEarly} disabled={offline}>
            {t('playerShell.session.endEarly')}
          </button>
          {offline ? <p className="time-money__hint" role="status">{t('playerShell.session.graceHint')}</p> : null}
          {/* Позвать администратора нужно и тому, кто играет со своего счёта: мышь сломалась,
              игра не запускается. Раньше кнопка была только у сессии со стойки. */}
          <AssistButton wide />
          <SignOutButton onSignOut={onSignOut} />
        </div>
      ) : role === 'signInToManage' ? (
        <div className="time-money__actions">
          <p className="time-money__hint">{t('playerShell.session.signInToManage')}</p>
          <button type="button" className="btn btn--primary btn--wide" onClick={onSignIn}>
            {t('playerShell.signIn.submit')}
          </button>
          <AssistButton wide />
        </div>
      ) : role === 'otherPlayer' ? (
        // Стойка посадила сюда чужую сессию, пока этот человек был вошедшим: «Войти» ему не
        // поможет — окно входа для вошедшего не откроется. Нужен выход.
        <div className="time-money__actions">
          <p className="time-money__hint">{t('playerShell.session.otherPlayer')}</p>
          <button type="button" className="btn btn--primary btn--wide" onClick={onSignOut}>
            {t('playerShell.signOut')}
          </button>
          <AssistButton wide />
        </div>
      ) : (
        <div className="time-money__actions">
          {role === 'counter' ? <p className="time-money__hint">{t('playerShell.session.counterHint')}</p> : null}
          <AssistButton wide />
          {signedIn ? <SignOutButton onSignOut={onSignOut} /> : null}
        </div>
      )}
    </aside>
  );
}

/**
 * «Выйти» — пунктом колонки той же ширины, что остальные, и последним: раньше это была
 * подчёркнутая ссылка под «Позвать администратора», и её путали с отменой «Встать раньше».
 */
function SignOutButton({ onSignOut }: { onSignOut: () => void }) {
  const { t } = useI18n();
  return (
    <button type="button" className="btn btn--ghost btn--wide time-money__leave" onClick={onSignOut}>
      <LogOut aria-hidden="true" />
      {t('playerShell.signOut')}
    </button>
  );
}
