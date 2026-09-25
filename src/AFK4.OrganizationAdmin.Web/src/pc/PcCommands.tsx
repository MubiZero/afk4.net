import { useId, useState, type ReactNode } from 'react';
import { Check, Loader2, LogOut, MessageSquare, Power, RotateCw, Sunrise, Wrench } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { projectOperatorFacingError } from '../operatorHelpers';
import type { SeatSummary } from '../operatorData';
import type { PcControlActionId, PcControlActionOptions, PcControlActionResult } from '../operatorTypes';
import { CriticalActionConfirmation } from '../operatorPrimitives';
import { PC_MESSAGE_MAX_LENGTH, pcCommandsFor, type PcCommandAccess, type PcCommandId } from './pcCommandOptions';

const LABELS: Record<PcCommandId, MessageKey> = {
  reboot: 'op.pc.reboot',
  shutdown: 'op.pc.shutdown',
  wake: 'op.pc.wake',
  'maintenance-on': 'op.pc.maintenanceOn',
  'maintenance-off': 'op.pc.maintenanceOff',
  'sign-out': 'op.pc.signOut',
  message: 'op.pc.message'
};

const ICONS: Record<PcCommandId, ReactNode> = {
  reboot: <RotateCw size={14} aria-hidden="true" />,
  shutdown: <Power size={14} aria-hidden="true" />,
  wake: <Sunrise size={14} aria-hidden="true" />,
  'maintenance-on': <Wrench size={14} aria-hidden="true" />,
  'maintenance-off': <Wrench size={14} aria-hidden="true" />,
  'sign-out': <LogOut size={14} aria-hidden="true" />,
  message: <MessageSquare size={14} aria-hidden="true" />
};

type ConfirmCopy = { title: MessageKey; impact: MessageKey; confirm: MessageKey };

const CONFIRM: Partial<Record<PcCommandId, ConfirmCopy>> = {
  reboot: { title: 'op.pc.confirm.rebootTitle', impact: 'op.pc.confirm.rebootImpact', confirm: 'op.pc.reboot' },
  shutdown: { title: 'op.pc.confirm.shutdownTitle', impact: 'op.pc.confirm.shutdownImpact', confirm: 'op.pc.shutdown' },
  'maintenance-on': { title: 'op.pc.confirm.maintenanceTitle', impact: 'op.pc.confirm.maintenanceImpact', confirm: 'op.pc.maintenanceOn' },
  'sign-out': { title: 'op.pc.confirm.signOutTitle', impact: 'op.pc.confirm.signOutImpact', confirm: 'op.pc.confirm.signOutButton' },
  message: { title: 'op.pc.confirm.messageTitle', impact: 'op.pc.confirm.messageImpact', confirm: 'op.pc.confirm.messageSend' }
};

type Outcome = { id: PcCommandId; state: 'pending' | 'confirmed' | 'failed'; detail?: string };

/**
 * Перезагрузка, выключение, пробуждение, обслуживание, сообщение и выход игрока (спека оболочки,
 * §5.8). Опасное спрашивает «точно?» на месте, а не всплывающим окном: так же, как «Завершить»
 * в этой карточке. Почему кнопка закрыта, написано под ней — до нажатия, а не отказом после.
 */
export function PcCommands({
  seat,
  access,
  onPcControlAction
}: {
  seat: SeatSummary;
  access: PcCommandAccess;
  onPcControlAction: (seat: SeatSummary, action: PcControlActionId, options?: PcControlActionOptions) => Promise<PcControlActionResult>;
}) {
  const { t } = useI18n();
  const messageId = useId();
  const reasonId = useId();
  const [asking, setAsking] = useState<PcCommandId | null>(null);
  const [text, setText] = useState('');
  const [outcome, setOutcome] = useState<Outcome | null>(null);
  const options = pcCommandsFor(seat, access);
  if (options.length === 0) return null;

  const busy = outcome?.state === 'pending';
  const reasons = [...new Set(options.map((option) => option.blockedReason).filter((reason): reason is MessageKey => reason !== null))];

  const send = async (id: PcCommandId) => {
    setAsking(null);
    setOutcome({ id, state: 'pending' });
    try {
      const result = await onPcControlAction(seat, id, id === 'message' ? { text: text.trim() } : undefined);
      setOutcome({ id, state: 'confirmed', detail: result.detail });
      if (id === 'message') setText('');
    } catch (error) {
      setOutcome({ id, state: 'failed', detail: projectOperatorFacingError(error, t) });
    }
  };

  const glyph = (id: PcCommandId) => {
    if (outcome?.id !== id) return ICONS[id];
    if (outcome.state === 'pending') return <Loader2 size={14} className="ui-spinner" aria-hidden="true" />;
    if (outcome.state === 'confirmed') return <Check size={14} aria-hidden="true" />;
    return ICONS[id];
  };

  const copy = asking ? CONFIRM[asking] : undefined;
  const messageReady = text.trim().length > 0 && text.length <= PC_MESSAGE_MAX_LENGTH;

  return (
    <div className="pc-commands">
      <div className="pc-control-actions panel-pc-actions">
        {options.map((option) => (
          <button
            key={option.id}
            type="button"
            disabled={busy || option.blockedReason !== null}
            aria-describedby={option.blockedReason ? reasonId : undefined}
            onClick={() => (option.confirm ? setAsking(option.id) : void send(option.id))}
          >
            {glyph(option.id)}
            <span>{t(LABELS[option.id])}</span>
          </button>
        ))}
      </div>

      {reasons.length > 0 && (
        <div id={reasonId}>
          {reasons.map((reason) => (
            <p key={reason} className="ui-blocked-reason" role="status">{t(reason)}</p>
          ))}
        </div>
      )}

      {asking && copy && (
        <CriticalActionConfirmation
          title={t(copy.title, { seat: seat.name })}
          detail={seat.deviceName ?? seat.name}
          impact={t(copy.impact)}
          confirmLabel={t(copy.confirm)}
          tone={asking === 'reboot' || asking === 'shutdown' ? 'danger' : 'warning'}
          disabled={asking === 'message' && !messageReady}
          onCancel={() => setAsking(null)}
          onConfirm={() => void send(asking)}
        >
          {asking === 'message' && (
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
        </CriticalActionConfirmation>
      )}

      {outcome?.state === 'confirmed' && outcome.detail && (
        <p className="pc-control-result" role="status">{outcome.detail}</p>
      )}
      {outcome?.state === 'failed' && outcome.detail && (
        <p className="pc-control-result failed" role="alert">{outcome.detail}</p>
      )}
    </div>
  );
}
