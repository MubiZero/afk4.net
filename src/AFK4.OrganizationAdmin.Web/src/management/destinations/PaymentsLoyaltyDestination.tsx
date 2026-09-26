import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { useOrganizationFeatures } from '../../useOrganizationFeatures';
import { PaymentMethodsSection } from './payments/PaymentMethodsSection';
import { LoyaltySection } from './payments/LoyaltySection';
import { ReferralSection } from './payments/ReferralSection';
import { BirthdayGiftSection } from './payments/BirthdayGiftSection';
import { TipsSection } from './payments/TipsSection';
import { PaymentsSetupSection } from './payments/PaymentsSetupSection';
import { useLoyaltySettings } from './payments/useLoyaltySettings';
import { useReferralSettings } from './payments/useReferralSettings';
import { useBirthdayGiftSettings } from './payments/useBirthdayGiftSettings';
import type { DestinationProps } from './types';

// «Платежи и лояльность» — спокойный setup-экран, куда заходят раз в несколько месяцев. Две ясные
// секции с человеческим лидом: «Как игрок платит вам» (приём) и «Как вы возвращаете» (кэшбэк).
// Приоритет — ясность и воздух, а не плотность рабочих вкладок. Каждая секция самодостаточна
// (своя кнопка сохранения), глобального save-бара нет. Зоны гейтятся по правам.
export function PaymentsLoyaltyDestination({ backend, session, currencyCode, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const canGateways = hasPermission(session, permissionNames.managePaymentGateways);
  const canLoyalty = hasPermission(session, permissionNames.manageLoyaltySettings);
  const canTips = hasPermission(session, permissionNames.manageTips);

  // Configuring a disabled feature is pointless — hide the whole loyalty zone when the
  // organization's `loyalty` feature is off, not just the settings within it. The payments zone
  // stays: it's unrelated to the loyalty feature flag.
  const features = useOrganizationFeatures(backend);
  const loyaltyFeatureEnabled = features === null || features.includes('loyalty');
  const showLoyalty = canLoyalty && loyaltyFeatureEnabled;

  const loyalty = useLoyaltySettings(backend, showLoyalty);
  // Приглашение — та же программа лояльности и то же право: гейтится вместе с кэшбэком.
  const referral = useReferralSettings(backend, showLoyalty);
  // Подарок на день рождения — туда же и под тем же правом.
  const birthdayGift = useBirthdayGiftSettings(backend, showLoyalty);

  useEffect(() => {
    onDirtyChange?.(loyalty.dirty || referral.dirty || birthdayGift.dirty);
  }, [loyalty.dirty, referral.dirty, birthdayGift.dirty, onDirtyChange]);

  return (
    <ManagementScreen
      title={t('op.management.dest.payments')}
      subtitle={t('op.management.dest.payments.subtitle')}
      contentWidth="wide"
    >
      {/* Две половины одного экрана: приём слева, возврат справа. auto-fit сам сводит в одну
          колонку, если видна лишь одна зона (по правам) или окно узкое. */}
      <div className="payset-columns">
        {/* Без backend здесь не бывает связи с сервером — бывает, что нет активного филиала,
            и это называет оболочка своим экраном (NoActiveBranchScreen) до Управления. Своя
            заглушка тут писала «Нет подключения к серверу», то есть неправду. */}
        {canGateways && backend !== null && (
          <PaymentsSetupSection
            direction="in"
            title={t('op.payments.zone.income')}
            lead={t('op.payments.zone.income.lead')}
          >
            <PaymentMethodsSection backend={backend} />
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

        {showLoyalty && (
          <PaymentsSetupSection
            direction="out"
            title={t('op.payments.zone.referral')}
            lead={t('op.payments.zone.referral.lead')}
          >
            <ReferralSection controller={referral} currencyCode={currencyCode} hasBackend={backend !== null} />
          </PaymentsSetupSection>
        )}

        {showLoyalty && (
          <PaymentsSetupSection
            direction="out"
            title={t('op.payments.zone.birthdayGift')}
            lead={t('op.payments.zone.birthdayGift.lead')}
          >
            <BirthdayGiftSection controller={birthdayGift} currencyCode={currencyCode} hasBackend={backend !== null} />
          </PaymentsSetupSection>
        )}

        {/* Чаевые — не программа лояльности и не её фича: у них своё право и своя секция. */}
        {canTips && backend !== null && (
          <PaymentsSetupSection
            direction="out"
            title={t('op.payments.zone.tips')}
            lead={t('op.payments.zone.tips.lead')}
          >
            <TipsSection backend={backend} />
          </PaymentsSetupSection>
        )}
      </div>
    </ManagementScreen>
  );
}
