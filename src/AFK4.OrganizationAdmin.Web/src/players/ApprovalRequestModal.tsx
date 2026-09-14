import { useI18n } from '@afk4/i18n';
import { SendHorizonal } from 'lucide-react';
import { PanelModal } from '../PanelModal';

/**
 * Развилка на отказе по порогу: операция не проведена, но её можно отправить старшему.
 *
 * Половина механизма антифрода была мертва — очередь одобрений умела принять и отклонить
 * заявку, а подать её было неоткуда. Сервер при превышении порога отвечал «подайте через
 * /money-actions», то есть советовал экран, которого не существовало: сотрудник упирался в
 * отказ и шёл искать старшего ногами.
 *
 * Диалог появляется ровно на 409 с `requiresApproval` и ни на каком другом отказе: превышение
 * жёсткого лимита (422) отправлять некуда, и предлагать это было бы враньём.
 */
export function ApprovalRequestModal({
  amountLabel,
  onClose,
  onConfirm,
  busy,
}: {
  /// Сумма в том виде, в каком её уже видит оператор на экране.
  amountLabel: string;
  onClose: () => void;
  onConfirm: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();

  return (
    <PanelModal
      title={t('op.players.approval.title')}
      subtitle={t('op.players.approval.subtitle')}
      onClose={onClose}
      tone="warning"
    >
      <div className="clients-confirm">
        <p className="clients-confirm-amount">{amountLabel}</p>
        <p className="clients-confirm-impact">{t('op.players.approval.impact')}</p>
        <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={onConfirm}>
          <SendHorizonal size={15} aria-hidden="true" />
          {t('op.players.approval.confirm')}
        </button>
      </div>
    </PanelModal>
  );
}
