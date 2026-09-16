import { useState } from 'react';
import { CheckCircle2 } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { closeWizard, provisionShell, type WizardEnrollResult, type WizardRole, type WizardSeat, type WizardShellOutcome } from './wizardApi';
import { wizardErrorMessage } from './wizardErrors';

interface FinishedScreenProps {
  result: WizardEnrollResult;
  branchName: string;
  selectedSeat: WizardSeat | null;

  /// Номер этого шага. Считает его App по видимым шагам прогона: здесь была зашита пятёрка, и
  /// прогон, где часть шагов пропущена, заканчивался «шагом 5» из трёх.
  stepNumber: number;
}

// Known update channels → shared i18n labels (reused from the Operator helper catalog).
const CHANNEL_LABEL_KEYS: Record<string, MessageKey> = {
  stable: 'op.helper.update.channel.stable',
  beta: 'op.helper.update.channel.beta',
  internal: 'op.helper.update.channel.internal',
};

export function FinishedScreen({ result, branchName, selectedSeat, stepNumber }: FinishedScreenProps) {
  const { t } = useI18n();
  const isPending = result.enrollmentState.toLowerCase() === 'pending';
  const roleLabel = result.role === 'gaming_pc'
    ? t('setup.wizard.role.gamingPc.title')
    : t('setup.wizard.role.managerWorkstation.title');
  // Локализуем канал обновлений; неизвестный slug показываем как есть, а не прячем за generic.
  const channelLabel = CHANNEL_LABEL_KEYS[result.updateChannel.toLowerCase()];
  const channelText = channelLabel ? t(channelLabel) : result.updateChannel;

  return (
    <section className="wizard-screen">
      <div className="wizard-finished">
        <div className="wizard-finished-hero">
          {/* Бейдж только для успеха — зелёная галка совпадает со смыслом «готово».
              Для pending кругляш с часами противоречил надписи внизу, поэтому его нет. */}
          {!isPending && (
            <span className="wizard-finished-badge" aria-hidden>
              <CheckCircle2 size={32} />
            </span>
          )}
          <span className="wizard-eyebrow">
            {t('setup.wizard.common.step')} {stepNumber} · {t('setup.wizard.finished.done')}
          </span>
          <h1>
            {isPending
              ? t('setup.wizard.finished.pending.title')
              : t('setup.wizard.finished.ok.title')}
          </h1>
          <p>
            {isPending
              ? t('setup.wizard.finished.pending.body')
              : t('setup.wizard.finished.ok.body')}
          </p>
        </div>

        <dl className="wizard-finished-summary">
          <div>
            <dt>{t('setup.wizard.finished.summary.branch')}</dt>
            <dd>{branchName}</dd>
          </div>
          <div>
            <dt>{t('setup.wizard.finished.summary.role')}</dt>
            <dd>{roleLabel}</dd>
          </div>
          {selectedSeat && (
            <div>
              <dt>{t('setup.wizard.finished.summary.seat')}</dt>
              <dd>{selectedSeat.zoneName}</dd>
            </div>
          )}
          <div>
            <dt>{t('setup.wizard.finished.summary.name')}</dt>
            <dd>{result.displayName}</dd>
          </div>
          <div>
            <dt>{t('setup.wizard.finished.summary.channel')}</dt>
            <dd>{channelText}</dd>
          </div>
        </dl>

        {result.shell.status !== 'skipped' && (
          <ShellStatusRow initial={result.shell} role={result.role} />
        )}

        {isPending && (
          <div className="wizard-pending-note" role="status">
            {t('setup.wizard.finished.pendingNote')}
          </div>
        )}

        <div className="wizard-actions is-end">
          <button type="button" className="ui-btn ui-btn--primary wizard-finished-close" onClick={closeWizard}>
            <span>{t('setup.wizard.finished.close')}</span>
          </button>
        </div>
      </div>
    </section>
  );
}

function ShellStatusRow({ initial, role }: { initial: WizardShellOutcome; role: WizardRole }) {
  const { t } = useI18n();
  const [outcome, setOutcome] = useState(initial);
  const [busy, setBusy] = useState(false);
  // Сорвавшийся повтор. Без него кнопка молча возвращалась в исходное состояние, и человек у ПК
  // видел ровно то же, что до нажатия, — будто она не работает.
  const [retryFailure, setRetryFailure] = useState<string | null>(null);

  // На игровом ПК ставится оболочка игрока, на рабочем месте управляющего — панель. Одна строка
  // на обе роли обещала управляющему «оболочку игрока», которой у него не будет.
  const appName = role === 'gaming_pc'
    ? t('op.helper.update.component.playerShell')
    : t('op.helper.update.component.organizationAdmin');

  // Успех (или уже было установлено) не показываем — зелёная плашка только шумит.
  // Показываем строку лишь когда установка оболочки сорвалась: это actionable (есть «Повторить»).
  if (outcome.status === 'installed' || outcome.status === 'already_present' || outcome.status === 'skipped') {
    return null;
  }

  return (
    <div className="wizard-shell-status is-error" role="alert">
      {/* Код установщика нужен тому, кто будет разбираться, но человеку у ПК он ничего не
          говорит: «(msiexec 1603)» рядом с русской фразой читается как часть поломки. Он
          остаётся в подсказке и в журнале, а на экране — только то, что делать дальше. */}
      <span title={outcome.exitCode !== null ? `msiexec ${outcome.exitCode}` : undefined}>
        {t('setup.wizard.finished.shell.failed', { app: appName })}
      </span>
      {retryFailure === null ? null : <span className="wizard-shell-status-detail">{retryFailure}</span>}
      <button
        type="button"
        className="ui-btn"
        disabled={busy}
        onClick={async () => {
          setBusy(true);
          setRetryFailure(null);
          try {
            setOutcome(await provisionShell(role));
          } catch (error) {
            setRetryFailure(wizardErrorMessage(error, t, 'setup.wizard.finished.shell.retryFailed'));
          } finally {
            setBusy(false);
          }
        }}
      >
        {busy
          ? t('setup.wizard.finished.shell.installing', { app: appName })
          : t('setup.wizard.finished.shell.retry')}
      </button>
    </div>
  );
}
