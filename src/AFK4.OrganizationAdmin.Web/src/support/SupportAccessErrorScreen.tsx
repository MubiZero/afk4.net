import { AlertTriangle } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { AuthFrame } from '../AuthFrame';

// Shown instead of the app shell when a /support-access link fails to redeem (ticket already
// used, expired — it only lives 60 seconds — or unknown). There is nothing else to fall back to
// yet: no staff session, no support session, so a blank screen would otherwise be the result.
export function SupportAccessErrorScreen() {
  const { t } = useI18n();

  return (
    <AuthFrame>
      <section className="auth-panel">
        <header className="auth-panel-head">
          <img className="auth-brand-mark" src="/favicon.svg" alt="" aria-hidden />
          <h1>{t('support.access.error.title')}</h1>
        </header>

        <div className="auth-error" role="alert">
          <AlertTriangle size={16} aria-hidden />
          <span>{t('support.access.error.body')}</span>
        </div>
      </section>
    </AuthFrame>
  );
}
