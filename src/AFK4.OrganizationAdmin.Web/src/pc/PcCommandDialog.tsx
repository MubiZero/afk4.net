import { useId, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../PanelModal';
import { PC_MESSAGE_MAX_LENGTH } from './pcCommandOptions';
import { DANGEROUS_PC_COMMANDS, PC_BULK_CONFIRM, type PcCommandOrLock } from './pcCommandCopy';
import type { BulkPlan } from './pcBulk';

/**
 * «Точно?» для команды одному или нескольким ПК: кому она уйдёт, кому нет и почему. Сообщению —
 * поле текста. Меню места исполняет пункт сразу, поэтому всё, что спрашивает, приходит сюда.
 */
export function PcCommandDialog({
  plan,
  onCancel,
  onConfirm
}: {
  plan: BulkPlan;
  onCancel: () => void;
  onConfirm: (text?: string) => void;
}) {
  const { t } = useI18n();
  const messageId = useId();
  const [text, setText] = useState('');
  const copy = PC_BULK_CONFIRM[plan.command];
  const single = plan.targets.length + plan.skipped.length === 1;
  const who = single
    ? (plan.targets[0] ?? plan.skipped[0].seat).name
    : t('op.pc.bulk.seats', { count: plan.targets.length });
  const isMessage = plan.command === 'message';
  const ready = plan.targets.length > 0 && (!isMessage || (text.trim().length > 0 && text.length <= PC_MESSAGE_MAX_LENGTH));
  const danger = DANGEROUS_PC_COMMANDS.has(plan.command as PcCommandOrLock);

  return (
    <PanelModal title={t(copy.title, { seat: who })} onClose={onCancel} tone={danger ? 'danger' : 'warning'}>
      <p className="pc-dialog-impact">{t(copy.impact)}</p>

      {!single && plan.targets.length > 0 && (
        <section className="pc-dialog-list" aria-label={t('op.pc.bulk.targets')}>
          <span className="pc-dialog-list-title">{t('op.pc.bulk.targets')}</span>
          <p>{plan.targets.map((seat) => seat.name).join(', ')}</p>
        </section>
      )}

      {plan.skipped.length > 0 && (
        <section className="pc-dialog-list pc-dialog-list--skipped" aria-label={t('op.pc.bulk.skipped')}>
          <span className="pc-dialog-list-title">{t('op.pc.bulk.skipped')}</span>
          <ul>
            {plan.skipped.map(({ seat, reason }) => (
              <li key={seat.id}><strong>{seat.name}</strong> — {t(reason)}</li>
            ))}
          </ul>
        </section>
      )}

      {plan.targets.length === 0 && <p className="ui-blocked-reason" role="status">{t('op.pc.bulk.nothing')}</p>}

      {isMessage && plan.targets.length > 0 && (
        <label className="pc-message-field" htmlFor={messageId}>
          <span>{t('op.pc.confirm.messageLabel')}</span>
          <textarea
            id={messageId}
            value={text}
            rows={3}
            maxLength={PC_MESSAGE_MAX_LENGTH}
            onChange={(event) => setText(event.currentTarget.value)}
          />
          <small>{t('op.pc.confirm.messageCount', { count: text.length, max: PC_MESSAGE_MAX_LENGTH })}</small>
        </label>
      )}

      <div className="pc-dialog-actions">
        <button type="button" className="ui-btn" onClick={onCancel}>{t('common.cancel')}</button>
        <button
          type="button"
          className={`ui-btn ${danger ? 'ui-btn--danger' : 'ui-btn--primary'}`}
          disabled={!ready}
          onClick={() => onConfirm(isMessage ? text.trim() : undefined)}
        >
          {t(copy.confirm)}
        </button>
      </div>
    </PanelModal>
  );
}
