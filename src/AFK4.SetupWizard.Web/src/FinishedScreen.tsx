import { useState } from 'react';
import { Button } from '@afk4/ui/react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import type { WizardEnrollResult, WizardRole, WizardSeat, WizardShellOutcome } from './wizardApi';
import { wizardErrorMessage } from './wizardErrors';
import { KioskStatusRow, RebootButton } from './Kiosk';
import { WizardStepLayout } from './WizardStepLayout';

interface FinishedScreenProps {
  result: WizardEnrollResult;
  branchName: string;
  selectedSeat: WizardSeat | null;

  /// Номер этого шага. Считает его App по видимым шагам прогона: здесь была зашита пятёрка, и
  /// прогон, где часть шагов пропущена, заканчивался «шагом 5» из трёх.
  stepNumber: number;

  /// Обращения к хосту — параметрами, как у остальных экранов мастера (см. `installClient` на
  /// экране устройства). Экран не берёт их из модуля намеренно: bun делит подмены модулей между
  /// файлами одного прогона, и частичная подмена в соседнем тесте оставляла этот экран без
  /// импорта — сборка падала на «Export named 'closeWizard' not found».
  provisionShell: (role: WizardRole) => Promise<WizardShellOutcome>;
  /// Перезагрузка — после киоска: автовход срабатывает только при запуске Windows.
  reboot: () => Promise<void>;
  onClose: () => void;
}

// Known update channels → shared i18n labels (reused from the Operator helper catalog).
const CHANNEL_LABEL_KEYS: Record<string, MessageKey> = {
  stable: 'op.helper.update.channel.stable',
  beta: 'op.helper.update.channel.beta',
  internal: 'op.helper.update.channel.internal',
};

export function FinishedScreen({
  result,
  branchName,
  selectedSeat,
  stepNumber,
  provisionShell,
  reboot,
  onClose,
}: FinishedScreenProps) {
  const { t } = useI18n();
  // Киоск живёт здесь, а не в своей строке: от него зависит, какая кнопка на экране главная.
  const [kiosk, setKiosk] = useState(result.shell.kiosk);
  const [rebootFailed, setRebootFailed] = useState(false);
  const isPending = result.enrollmentState.toLowerCase() === 'pending';
  const kioskReady = kiosk?.status === 'ready';
  const roleLabel = result.role === 'gaming_pc'
    ? t('setup.wizard.role.gamingPc.title')
    : t('setup.wizard.role.managerWorkstation.title');
  // Локализуем канал обновлений; неизвестный slug показываем как есть, а не прячем за generic.
  const channelLabel = CHANNEL_LABEL_KEYS[result.updateChannel.toLowerCase()];
  const channelText = channelLabel ? t(channelLabel) : result.updateChannel;

  // Что осталось сделать руками — одним списком по порядку. Раньше это были две плашки с разными
  // голосами: зелёная звала «Перезагрузить сейчас», жёлтая ниже — сначала подтвердить ПК в Панели,
  // и перезагрузка до подтверждения ничего бы не дала.
  const nextSteps: string[] = [];
  if (isPending) nextSteps.push(t('setup.wizard.finished.next.confirm'));
  if (kioskReady) nextSteps.push(t('setup.wizard.finished.next.reboot'));
  else if (isPending) nextSteps.push(t('setup.wizard.finished.next.restart'));

  return (
    <WizardStepLayout
      stepNumber={stepNumber}
      title={isPending ? t('setup.wizard.finished.pending.title') : t('setup.wizard.finished.ok.title')}
      subtitle={isPending ? t('setup.wizard.finished.pending.body') : t('setup.wizard.finished.ok.body')}
      // Главная одна. Киоск без перезагрузки не заработает, поэтому главная — перезагрузка, а
      // закрыть мастер можно и без неё, тихой ссылкой.
      skip={kioskReady ? { label: t('setup.wizard.finished.closeWithoutReboot'), onClick: onClose } : null}
      primary={kioskReady ? (
        <RebootButton reboot={reboot} onFailed={() => setRebootFailed(true)} />
      ) : (
        <Button variant="primary" onClick={onClose}>{t('setup.wizard.finished.close')}</Button>
      )}
    >
      <dl className="wizard-summary">
        <div>
          <dt>{t('setup.wizard.finished.summary.branch')}</dt>
          <dd>{branchName}</dd>
        </div>
        <div>
          <dt>{t('setup.wizard.finished.summary.role')}</dt>
          <dd>{roleLabel}</dd>
        </div>
        {/* Место — зал и имя места на карте («Общий зал · ПК-5»): по одному залу ПК в нём не найти. */}
        {selectedSeat && (
          <div>
            <dt>{t('setup.wizard.finished.summary.seat')}</dt>
            <dd>{selectedSeat.zoneName} · {selectedSeat.pcName}</dd>
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
        <ShellStatusRow initial={result.shell} role={result.role} provisionShell={provisionShell} />
      )}

      {kiosk?.status === 'failed' && (
        <KioskStatusRow outcome={kiosk} role={result.role} provisionShell={provisionShell} onOutcome={setKiosk} />
      )}

      {nextSteps.length > 0 && (
        <div className="wizard-next" role="status">
          <strong>{t('setup.wizard.finished.next.title')}</strong>
          <ol>
            {nextSteps.map((step) => <li key={step}>{step}</li>)}
          </ol>
        </div>
      )}

      {rebootFailed ? <p className="ui-alert" role="alert">{t('setup.wizard.kiosk.rebootFailed')}</p> : null}
    </WizardStepLayout>
  );
}

function ShellStatusRow({ initial, role, provisionShell }: {
  initial: WizardShellOutcome;
  role: WizardRole;
  provisionShell: (role: WizardRole) => Promise<WizardShellOutcome>;
}) {
  const { t } = useI18n();
  const [outcome, setOutcome] = useState(initial);
  const [busy, setBusy] = useState(false);
  // Сорвавшийся повтор. Без него кнопка молча возвращалась в исходное состояние, и человек у ПК
  // видел ровно то же, что до нажатия, — будто она не работает.
  const [retryFailure, setRetryFailure] = useState<string | null>(null);

  // На игровом ПК ставится оболочка игрока, на рабочем месте управляющего — панель. Одна строка
  // на обе роли обещала управляющему «оболочку игрока», которой у него не будет. Имена свои, а не
  // из списка компонентов обновлений: падеж там именительный, а здесь нужен винительный — у
  // «оболочки игрока» они разные.
  const appName = role === 'gaming_pc'
    ? t('setup.wizard.finished.app.playerShell')
    : t('setup.wizard.finished.app.organizationAdmin');

  // Успех (или уже было установлено) не показываем — зелёная плашка только шумит.
  // Показываем строку лишь когда что-то сорвалось: это actionable (есть «Повторить»).
  if (outcome.status === 'installed' || outcome.status === 'already_present' || outcome.status === 'skipped') {
    return null;
  }

  // Не запустившаяся служба — не то же, что не установившееся приложение: приложение как раз
  // встало. Одна фраза на два случая отправляла разбираться не туда.
  const failureText = outcome.status === 'agent_start_failed'
    ? t('setup.wizard.finished.agent.failed')
    : t('setup.wizard.finished.shell.failed', { app: appName });

  return (
    <div className="wizard-shell-status is-error" role="alert">
      {/* Код установщика нужен тому, кто будет разбираться, но человеку у ПК он ничего не
          говорит: «(msiexec 1603)» рядом с русской фразой читается как часть поломки. Он
          остаётся в подсказке и в журнале, а на экране — только то, что делать дальше. */}
      <span title={outcome.exitCode !== null ? `msiexec ${outcome.exitCode}` : undefined}>
        {failureText}
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
