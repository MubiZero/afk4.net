import { useI18n } from '@afk4/i18n';
import { PhoneVerificationCard, type PhoneVerificationBackend } from './PhoneVerificationCard';

interface Props {
  backend: PhoneVerificationBackend;
  displayName: string;
  onClose: () => void;
}

export function AccountPanel({ backend, displayName, onClose }: Props) {
  const { t } = useI18n();
  return (
    <div className="account-panel-overlay" onClick={onClose}>
      <div
        className="account-panel"
        role="dialog"
        aria-modal="true"
        aria-labelledby="account-panel-title"
        onClick={(e) => e.stopPropagation()}
      >
        <header className="account-panel-head">
          <strong id="account-panel-title">{displayName}</strong>
          <button type="button" className="account-panel-close" aria-label={t('account.phone.close')} onClick={onClose}>×</button>
        </header>
        <PhoneVerificationCard backend={backend} />
      </div>
    </div>
  );
}
