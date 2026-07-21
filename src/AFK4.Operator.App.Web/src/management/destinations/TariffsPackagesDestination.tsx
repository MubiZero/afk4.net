import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { managementScreenState, type DestinationProps } from './types';
import { TariffsTab } from './tariffs/TariffsTab';
import { PackagesTab } from './tariffs/PackagesTab';

type TariffsPackagesTab = 'tariffs' | 'packages';

// Тарифы и пакеты: список+drawer CRUD по эталону «Залы и ПК» (см. task-C1-tariffs-brief.md).
// Домен A (версии тарифов, TariffsTab) и домен B (пакеты времени, PackagesTab) живут во
// внутренних вкладках раздела. Каждая операция коммитит сама — общего save-бара нет, поэтому
// dirty-guard не нужен, сигнализируем "clean" сразу на маунте.
export function TariffsPackagesDestination({
  backend,
  session,
  currencyCode,
  tariffs,
  packageOptions,
  onReload,
  onFeedback,
  onDirtyChange,
  loadStatus,
  errorDetail,
  onRetry
}: DestinationProps) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<TariffsPackagesTab>('tariffs');

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  const canManageTariffs = backend !== null && hasPermission(session, permissionNames.manageTariffs);
  const canManagePackages = backend !== null && hasPermission(session, permissionNames.managePackages);

  return (
    <ManagementScreen
      title={t('op.management.dest.tariffs')}
      subtitle={t('op.management.dest.tariffs.subtitle')}
      contentWidth="full"
      state={managementScreenState(loadStatus)}
      errorDetail={errorDetail}
      onRetry={onRetry}
    >
      <div className="mgmt-tabs" role="tablist" aria-label={t('op.management.dest.tariffs')}>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'tariffs'}
          className={`mgmt-tab${activeTab === 'tariffs' ? ' is-active' : ''}`}
          onClick={() => setActiveTab('tariffs')}
        >
          {t('op.management.tariffs.tab.tariffs')}
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={activeTab === 'packages'}
          className={`mgmt-tab${activeTab === 'packages' ? ' is-active' : ''}`}
          onClick={() => setActiveTab('packages')}
        >
          {t('op.management.tariffs.tab.packages')}
        </button>
      </div>

      {activeTab === 'tariffs' ? (
        <TariffsTab
          tariffs={tariffs ?? []}
          currencyCode={currencyCode}
          backend={backend}
          canManageTariffs={canManageTariffs}
          onReload={onReload ?? (async () => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      ) : (
        <PackagesTab
          packageOptions={packageOptions ?? []}
          currencyCode={currencyCode}
          backend={backend}
          canManagePackages={canManagePackages}
          onReload={onReload ?? (async () => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      )}
    </ManagementScreen>
  );
}
