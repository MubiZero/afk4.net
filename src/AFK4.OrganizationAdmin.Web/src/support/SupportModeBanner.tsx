import { useI18n } from '@afk4/i18n';
import { formatSupportCountdown, useSupportSessionCountdown } from './useSupportSessionCountdown';
import type { SupportSession } from './supportSession';

// Non-dismissible strip pinned above the shell for the whole duration of a support session — the
// person using the club's screen must never lose track of the fact that platform support, not
// club staff, currently holds it. Club, reason and a live countdown to the grant's end, plus the
// one way out (`onExit`, wired up in App.tsx to revoke the grant server-side and drop back to the
// sign-in screen). The countdown also self-terminates the session at zero — nobody should have to
// notice the timer ran out on their own before the tab acts on it.
export function SupportModeBanner({ session, onExit }: {
  session: SupportSession;
  onExit: () => void;
}) {
  const { t } = useI18n();
  const remainingMs = useSupportSessionCountdown(session.expiresAtUtc, onExit);

  return (
    <div className="support-mode-banner" role="banner">
      <span className="support-mode-banner-badge">{t('op.support.banner.title')}</span>
      <span className="support-mode-banner-club">{session.organizationName}</span>
      <span className="support-mode-banner-reason">{session.reason}</span>
      <span className="support-mode-banner-time">
        {t('op.support.banner.timeLeft', { time: formatSupportCountdown(remainingMs) })}
      </span>
      <span className="support-mode-banner-audit">{t('op.support.banner.audit')}</span>
      <button type="button" className="support-mode-banner-exit" onClick={onExit}>
        {t('op.support.banner.exit')}
      </button>
    </div>
  );
}
