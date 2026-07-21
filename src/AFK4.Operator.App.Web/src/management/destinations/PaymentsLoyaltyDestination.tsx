import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { EmptyState } from '../../operatorPrimitives';
import { GatewaysTab } from './payments/GatewaysTab';
import { LoyaltyTab } from './payments/LoyaltyTab';
import { useLoyaltySettings } from './payments/useLoyaltySettings';
import type { DestinationProps } from './types';

type PaymentsTab = 'gateways' | 'loyalty';

// «Платежи и лояльность»: один раздел, две вкладки с разными моделями. «Платёжные шлюзы» —
// мгновенные действия (dcgate-карты + реквизиты Eskhata, свои кнопки), без save-бара, широкая.
// «Лояльность» — форма с save-баром (узкая). Save-бар ManagementScreen получает save только на
// вкладке лояльности → конфликт моделей снят. Вкладки гейтятся по правам: у роли с одним правом
// видна только своя вкладка (без пустой второй, без размытия прав).
export function PaymentsLoyaltyDestination({ backend, session, currencyCode, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();
  const canGateways = hasPermission(session, permissionNames.managePaymentGateways);
  const canLoyalty = hasPermission(session, permissionNames.manageLoyaltySettings);
  const [activeTab, setActiveTab] = useState<PaymentsTab>(canGateways ? 'gateways' : 'loyalty');

  const loyalty = useLoyaltySettings(backend, canLoyalty);

  useEffect(() => {
    onDirtyChange?.(loyalty.dirty);
  }, [loyalty.dirty, onDirtyChange]);

  const showTabs = canGateways && canLoyalty;
  const onLoyaltyTab = activeTab === 'loyalty' && canLoyalty;

  return (
    <ManagementScreen
      title={t('op.management.dest.payments')}
      subtitle={t('op.management.dest.payments.subtitle')}
      contentWidth={onLoyaltyTab ? 'form' : 'wide'}
      save={onLoyaltyTab
        ? { state: loyalty.saveState, onSave: () => void loyalty.save(), disabled: backend === null }
        : undefined}
    >
      {showTabs && (
        <div className="mgmt-tabs" role="tablist" aria-label={t('op.management.dest.payments')}>
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'gateways'}
            className={`mgmt-tab${activeTab === 'gateways' ? ' is-active' : ''}`}
            onClick={() => setActiveTab('gateways')}
          >
            {t('op.payments.tab.gateways')}
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === 'loyalty'}
            className={`mgmt-tab${activeTab === 'loyalty' ? ' is-active' : ''}`}
            onClick={() => setActiveTab('loyalty')}
          >
            {t('op.payments.tab.loyalty')}
          </button>
        </div>
      )}

      {onLoyaltyTab ? (
        <LoyaltyTab controller={loyalty} currencyCode={currencyCode} hasBackend={backend !== null} />
      ) : backend === null ? (
        <div className="management-panel">
          <EmptyState
            title={t('op.management.dest.payment.noBackendTitle')}
            description={t('op.management.dest.payment.noBackendHint')}
          />
        </div>
      ) : (
        <GatewaysTab backend={backend} />
      )}
    </ManagementScreen>
  );
}
