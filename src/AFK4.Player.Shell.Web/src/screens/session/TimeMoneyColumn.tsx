import type { PlayerShellStateDto } from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { clubTime } from '../../model/offers';
import type { SessionRole } from '../../model/session';
import { AssistButton } from '../../ui/AssistButton';
import { Countdown } from '../../ui/Countdown';

interface TimeMoneyColumnProps {
  state: PlayerShellStateDto;
  receivedAtMs: number | null;
  role: SessionRole;
  /** Без связи с клубом продлевать и заканчивать нечем — сервер недоступен. */
  offline: boolean;
  onExtend: () => void;
  onEndEarly: () => void;
  onSignIn: () => void;
  onSignOut: () => void;
}

/**
 * Колонка «время и деньги» (кадр 03 концепта, каркас как у Senet): сколько осталось, до скольки и
 * что с этим можно сделать. Денежные кнопки — только у того, чья это сессия.
 */
export function TimeMoneyColumn({ state, receivedAtMs, role, offline, onExtend, onEndEarly, onSignIn, onSignOut }: TimeMoneyColumnProps) {
  const { t, locale } = useI18n();

  return (
    <aside className="time-money" aria-label={t('playerShell.session.remaining')}>
      <div className="time-money__time">
        <span className="time-money__label">{t('playerShell.session.remaining')}</span>
        <Countdown
          className="time-money__countdown"
          untilUtc={state.leaseExpiresAtUtc}
          observedAtUtc={state.observedAtUtc}
          receivedAtMs={receivedAtMs}
        />
        {state.leaseExpiresAtUtc ? (
          <span className="time-money__until">
            {t('playerShell.session.until', { time: clubTime(state.leaseExpiresAtUtc, undefined, locale) })}
          </span>
        ) : null}
      </div>

      {role === 'owner' ? (
        <div className="time-money__actions">
          <button type="button" className="btn btn--primary btn--wide" onClick={onExtend} disabled={offline}>
            {t('playerShell.session.extend')}
          </button>
          <button type="button" className="btn btn--ghost btn--wide" onClick={onEndEarly} disabled={offline}>
            {t('playerShell.session.endEarly')}
          </button>
          {offline ? <p className="time-money__hint" role="status">{t('playerShell.session.graceHint')}</p> : null}
          <button type="button" className="time-money__sign-out" onClick={onSignOut}>
            {t('playerShell.signOut')}
          </button>
        </div>
      ) : role === 'signInToManage' ? (
        <div className="time-money__actions">
          <p className="time-money__hint">{t('playerShell.session.signInToManage')}</p>
          <button type="button" className="btn btn--primary btn--wide" onClick={onSignIn}>
            {t('playerShell.signIn.submit')}
          </button>
        </div>
      ) : (
        <div className="time-money__actions">
          <p className="time-money__hint">{t('playerShell.session.counterHint')}</p>
          <AssistButton />
        </div>
      )}
    </aside>
  );
}
