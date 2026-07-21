import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { EmptyState } from '../../operatorPrimitives';
import { PaymentMethodsSection } from './payments/PaymentMethodsSection';
import { LoyaltySection } from './payments/LoyaltySection';
import { useLoyaltySettings } from './payments/useLoyaltySettings';
import type { DestinationProps } from './types';

// «Платежи и лояльность»: одна связная страница «деньги ↔ игрок», без табов. Две озаглавленные
// зоны, каждая самодостаточна — глобального save-бара ManagementScreen нет (именно он навязывал
// одну save-модель на весь экран и породил табы). Зоны показываются по правам: нет права — нет
// зоны, без пустых блоков и размытия прав.
export function PaymentsLoyaltyDestination({ backend, session, currencyCode, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const canGateways = hasPermission(session, permissionNames.managePaymentGateways);
  const canLoyalty = hasPermission(session, permissionNames.manageLoyaltySettings);

  const loyalty = useLoyaltySettings(backend, canLoyalty);

  useEffect(() => {
    onDirtyChange?.(loyalty.dirty);
  }, [loyalty.dirty, onDirtyChange]);

  return (
    <ManagementScreen
      title={t('op.management.dest.payments')}
      subtitle={t('op.management.dest.payments.subtitle')}
      contentWidth="wide"
    >
      {canGateways && (
        backend === null ? (
          <div className="management-panel">
            <EmptyState
              title={t('op.management.dest.payment.noBackendTitle')}
              description={t('op.management.dest.payment.noBackendHint')}
            />
          </div>
        ) : (
          <PaymentMethodsSection backend={backend} />
        )
      )}

      {canLoyalty && (
        <LoyaltySection controller={loyalty} currencyCode={currencyCode} hasBackend={backend !== null} />
      )}
    </ManagementScreen>
  );
}
