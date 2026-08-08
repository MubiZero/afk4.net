import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { EmptyState } from '../../operatorPrimitives';
import { useOrganizationFeatures } from '../../useOrganizationFeatures';
import { PaymentMethodsSection } from './payments/PaymentMethodsSection';
import { LoyaltySection } from './payments/LoyaltySection';
import { PaymentsSetupSection } from './payments/PaymentsSetupSection';
import { useLoyaltySettings } from './payments/useLoyaltySettings';
import type { DestinationProps } from './types';

// «Платежи и лояльность» — спокойный setup-экран, куда заходят раз в несколько месяцев. Две ясные
// секции с человеческим лидом: «Как игрок платит вам» (приём) и «Как вы возвращаете» (кэшбэк).
// Приоритет — ясность и воздух, а не плотность рабочих вкладок. Каждая секция самодостаточна
// (своя кнопка сохранения), глобального save-бара нет. Зоны гейтятся по правам.
export function PaymentsLoyaltyDestination({ backend, session, currencyCode, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const canGateways = hasPermission(session, permissionNames.managePaymentGateways);
  const canLoyalty = hasPermission(session, permissionNames.manageLoyaltySettings);

  // Configuring a disabled feature is pointless — hide the whole loyalty zone when the
  // organization's `loyalty` feature is off, not just the settings within it. The payments zone
  // stays: it's unrelated to the loyalty feature flag.
  const features = useOrganizationFeatures(backend);
  const loyaltyFeatureEnabled = features === null || features.includes('loyalty');
  const showLoyalty = canLoyalty && loyaltyFeatureEnabled;

  const loyalty = useLoyaltySettings(backend, showLoyalty);

  useEffect(() => {
    onDirtyChange?.(loyalty.dirty);
  }, [loyalty.dirty, onDirtyChange]);

  return (
    <ManagementScreen
      title={t('op.management.dest.payments')}
      subtitle={t('op.management.dest.payments.subtitle')}
      contentWidth="wide"
    >
      {/* Две половины одного экрана: приём слева, возврат справа. auto-fit сам сводит в одну
          колонку, если видна лишь одна зона (по правам) или окно узкое. */}
      <div className="payset-columns">
        {canGateways && (
          <PaymentsSetupSection
            direction="in"
            title={t('op.payments.zone.income')}
            lead={t('op.payments.zone.income.lead')}
          >
            {backend !== null ? (
              <PaymentMethodsSection backend={backend} />
            ) : (
              <EmptyState
                title={t('op.management.dest.payment.noBackendTitle')}
                description={t('op.management.dest.payment.noBackendHint')}
              />
            )}
          </PaymentsSetupSection>
        )}

        {showLoyalty && (
          <PaymentsSetupSection
            direction="out"
            title={t('op.payments.zone.loyalty')}
            lead={t('op.payments.zone.loyalty.lead')}
          >
            <LoyaltySection controller={loyalty} currencyCode={currencyCode} hasBackend={backend !== null} />
          </PaymentsSetupSection>
        )}
      </div>
    </ManagementScreen>
  );
}
