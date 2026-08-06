import { useEffect, useState } from 'react';
import { ShieldCheck } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { AuthFrame } from '../AuthFrame';
import { clearSupportSession, type SupportSession } from './supportSession';

// Landing screen for platform support impersonating an organization (see supportSession.ts). This
// is the first real, working destination past ticket redemption — proof that the grant-header
// transport actually gets exercised end to end, not just unit-tested in isolation. Scoping section
// access, branch selection and per-area write gating to `writableAreas` is deliberately out of
// scope here (tracked as follow-up work): the grant is already enforced server-side per endpoint
// regardless of what this screen shows.
export function SupportHomeScreen({ session }: { session: SupportSession }) {
  const { t } = useI18n();
  const remainingMs = useCountdown(session.expiresAtUtc);

  useEffect(() => {
    if (remainingMs !== null && remainingMs <= 0) {
      // The grant is server-authoritative and already stops working the moment it expires — this
      // just stops the tab from sitting on a screen that quietly can't do anything anymore.
      clearSupportSession();
      window.location.reload();
    }
  }, [remainingMs]);

  return (
    <AuthFrame>
      <section className="auth-panel">
        <header>
          <ShieldCheck size={20} aria-hidden />
          <span>{session.organizationName}</span>
          <h1>{t('support.session.title')}</h1>
          <p>{t('support.session.reason', { reason: session.reason })}</p>
        </header>

        <p>
          {session.writableAreas.length > 0
            ? t('support.session.writableAreas', { areas: session.writableAreas.join(', ') })
            : t('support.session.writableAreasEmpty')}
        </p>

        <p className="auth-hint" role="status">
          {remainingMs !== null
            ? t('support.session.expiresIn', { time: formatRemaining(remainingMs) })
            : ''}
        </p>
      </section>
    </AuthFrame>
  );
}

// Ticks once a second so the visible countdown stays accurate; NaN/garbage expiry is treated as
// already-expired (fail-safe), matching isAccessTokenExpired's convention for the staff session.
function useCountdown(expiresAtUtc: string): number | null {
  const expiresAt = Date.parse(expiresAtUtc);
  const [remainingMs, setRemainingMs] = useState<number>(() =>
    Number.isNaN(expiresAt) ? 0 : Math.max(0, expiresAt - Date.now())
  );

  useEffect(() => {
    if (Number.isNaN(expiresAt)) {
      setRemainingMs(0);
      return;
    }

    const tick = () => setRemainingMs(Math.max(0, expiresAt - Date.now()));
    tick();
    const timer = window.setInterval(tick, 1000);
    return () => window.clearInterval(timer);
  }, [expiresAt]);

  return remainingMs;
}

function formatRemaining(remainingMs: number): string {
  const totalSeconds = Math.ceil(remainingMs / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${String(seconds).padStart(2, '0')}`;
}
