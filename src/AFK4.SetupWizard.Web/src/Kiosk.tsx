import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { WizardKioskOutcome, WizardKioskStatus, WizardRole, WizardShellOutcome } from './wizardApi';
import { wizardErrorMessage } from './wizardErrors';

/**
 * Строка киоска на экране «Готово» (спека оболочки, §6.1). В отличие от установки приложения,
 * успех здесь показывается: без перезагрузки киоск не заработает, и человек должен это прочитать.
 */
export function KioskStatusRow({ initial, role, provisionShell, reboot }: {
  initial: WizardKioskOutcome;
  role: WizardRole;
  provisionShell: (role: WizardRole) => Promise<WizardShellOutcome>;
  reboot: () => Promise<void>;
}) {
  const { t } = useI18n();
  const [outcome, setOutcome] = useState(initial);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);

  if (outcome.status === 'ready') {
    return (
      <div className="wizard-shell-status is-ok" role="status">
        <span>{t('setup.wizard.kiosk.ready')}</span>
        {failure === null ? null : <span className="wizard-shell-status-detail">{failure}</span>}
        <RebootButton reboot={reboot} onFailed={setFailure} />
      </div>
    );
  }

  return (
    <div className="wizard-shell-status is-error" role="alert">
      {/* Системная причина (код Windows) нужна тому, кто будет разбираться, — она в подсказке и в журнале. */}
      <span title={outcome.message ?? undefined}>{t('setup.wizard.kiosk.failed')}</span>
      {failure === null ? null : <span className="wizard-shell-status-detail">{failure}</span>}
      <button
        type="button"
        className="ui-btn"
        disabled={busy}
        onClick={async () => {
          setBusy(true);
          setFailure(null);
          try {
            const next = await provisionShell(role);
            if (next.kiosk) setOutcome(next.kiosk);
          } catch (error) {
            setFailure(wizardErrorMessage(error, t, 'setup.wizard.finished.shell.retryFailed'));
          } finally {
            setBusy(false);
          }
        }}
      >
        {busy ? t('setup.wizard.kiosk.working') : t('setup.wizard.finished.shell.retry')}
      </button>
    </div>
  );
}

/**
 * «Снять киоск» под формой входа — только там, где киоск стоит. Мастер и так работает от
 * администратора ПК: это и есть допуск к откату.
 */
export function KioskRemoval({ loadStatus, remove, reboot }: {
  loadStatus: () => Promise<WizardKioskStatus>;
  remove: () => Promise<WizardKioskStatus>;
  reboot: () => Promise<void>;
}) {
  const { t } = useI18n();
  const [stage, setStage] = useState<'hidden' | 'offer' | 'confirm' | 'removing' | 'removed'>('hidden');
  const [failure, setFailure] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    loadStatus()
      .then((status) => { if (alive && status.installed) setStage('offer'); })
      // Не узнали — не предлагаем: ссылка на откат там, где откатывать нечего, только путает.
      .catch(() => undefined);
    return () => { alive = false; };
  }, [loadStatus]);

  if (stage === 'hidden') return null;

  if (stage === 'offer') {
    return (
      <button type="button" className="wizard-link-inline wizard-mode-switch" onClick={() => setStage('confirm')}>
        {t('setup.wizard.kiosk.remove.link')}
      </button>
    );
  }

  if (stage === 'removed') {
    return (
      <div className="wizard-shell-status is-ok" role="status">
        <span>{t('setup.wizard.kiosk.remove.done')}</span>
        {failure === null ? null : <span className="wizard-shell-status-detail">{failure}</span>}
        <RebootButton reboot={reboot} onFailed={setFailure} />
      </div>
    );
  }

  return (
    <div className="wizard-shell-status is-error" role="group" aria-label={t('setup.wizard.kiosk.remove.confirmTitle')}>
      <strong>{t('setup.wizard.kiosk.remove.confirmTitle')}</strong>
      <span>{t('setup.wizard.kiosk.remove.confirmBody')}</span>
      {failure === null ? null : <span className="wizard-shell-status-detail" role="alert">{failure}</span>}
      <div className="wizard-actions">
        <button type="button" className="ui-btn" disabled={stage === 'removing'} onClick={() => { setFailure(null); setStage('offer'); }}>
          {t('setup.wizard.common.back')}
        </button>
        <button
          type="button"
          className="ui-btn ui-btn--primary"
          disabled={stage === 'removing'}
          onClick={async () => {
            setStage('removing');
            setFailure(null);
            try {
              const status = await remove();
              setStage(status.installed ? 'confirm' : 'removed');
            } catch (error) {
              setFailure(wizardErrorMessage(error, t, 'setup.wizard.kiosk.remove.failed'));
              setStage('confirm');
            }
          }}
        >
          {stage === 'removing' ? t('setup.wizard.kiosk.working') : t('setup.wizard.kiosk.remove.confirm')}
        </button>
      </div>
    </div>
  );
}

function RebootButton({ reboot, onFailed }: { reboot: () => Promise<void>; onFailed: (message: string) => void }) {
  const { t } = useI18n();
  const [busy, setBusy] = useState(false);
  return (
    <button
      type="button"
      className="ui-btn ui-btn--primary"
      disabled={busy}
      onClick={async () => {
        setBusy(true);
        try {
          await reboot();
        } catch {
          onFailed(t('setup.wizard.kiosk.rebootFailed'));
          setBusy(false);
        }
      }}
    >
      {busy ? t('setup.wizard.kiosk.rebooting') : t('setup.wizard.kiosk.reboot')}
    </button>
  );
}
