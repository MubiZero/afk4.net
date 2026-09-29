import { useI18n } from '@afk4/i18n';
import { PhoneVerificationCard, type PhoneVerificationBackend } from './PhoneVerificationCard';
import { initialsOf } from './RailAccount';
import { CloseButton } from '@afk4/ui/react';

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
        <CloseButton className="account-panel-close" label={t('account.phone.close')} onClick={onClose} />
        <header className="account-panel-identity">
          <span className="account-panel-avatar" aria-hidden="true">{initialsOf(displayName)}</span>
          <div className="account-panel-identity-text">
            <strong id="account-panel-title">{displayName}</strong>
            <span>{t('op.auth.operator')}</span>
          </div>
        </header>
        <PhoneVerificationCard backend={backend} />
      </div>
    </div>
  );
}
