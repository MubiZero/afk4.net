import { useI18n } from '@afk4/i18n';
import { Power, PowerOff } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Подтверждение деактивации/реактивации клиента. Деактивация — destructive (tone=danger);
// реактивация мягче (tone=warning). Реальный toggle IsActive держит оркестратор.
export function ActiveStateConfirmModal({
  mode,
  onClose,
  onConfirm,
  busy,
}: {
  mode: 'deactivate' | 'reactivate';
  onClose: () => void;
  onConfirm: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const isDeactivate = mode === 'deactivate';

  return (
    <PanelModal
      title={isDeactivate ? t('op.players.deactivate.title') : t('op.players.reactivate.title')}
      subtitle={isDeactivate ? t('op.players.deactivate.subtitle') : t('op.players.reactivate.subtitle')}
      onClose={onClose}
      tone={isDeactivate ? 'danger' : 'warning'}
    >
      <div className="clients-confirm">
        <p className="clients-confirm-impact">
          {isDeactivate ? t('op.players.deactivate.impact') : t('op.players.reactivate.impact')}
        </p>
        <button
          type="button"
          className={isDeactivate ? 'clients-danger-action' : 'clients-primary-action'}
          disabled={busy}
          onClick={onConfirm}
        >
          {isDeactivate ? <PowerOff size={15} aria-hidden="true" /> : <Power size={15} aria-hidden="true" />}
          {isDeactivate ? t('op.players.deactivate.confirm') : t('op.players.reactivate.confirm')}
        </button>
      </div>
    </PanelModal>
  );
}
