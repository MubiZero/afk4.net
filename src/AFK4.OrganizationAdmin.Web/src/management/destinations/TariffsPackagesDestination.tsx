import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Tabs } from '@afk4/ui/react';
import { ManagementScreen } from '../ManagementScreen';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import { projectOperatorError } from '../../apiErrors';
import { LoadFailureState } from '../../operatorPrimitives';
import { managementScreenState, type DestinationProps } from './types';
import { TariffsTab, TariffsTabSkeleton } from './tariffs/TariffsTab';
import { PackagesTab, PackagesTabSkeleton } from './tariffs/PackagesTab';
import { ViewOnlyNotice } from '../ViewOnlyNotice';
import { DeferredSkeleton, SkeletonTabs } from '../../LoadingSkeleton';

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
  packageState,
  onReload,
  onFeedback,
  onDirtyChange,
  loadStatus,
  failure,
  onRetry,
  onRetryPackages
}: DestinationProps) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<TariffsPackagesTab>('tariffs');

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  const canManageTariffs = backend !== null && hasPermission(session, permissionNames.manageTariffs);
  const canManagePackages = backend !== null && hasPermission(session, permissionNames.managePackages);
  // Раздел открыт по любому из двух прав; вкладка без своего права — только для просмотра.
  const tabViewOnly = backend === null
    ? null
    : activeTab === 'tariffs'
      ? (canManageTariffs ? null : t('op.management.viewOnly.tariffs'))
      : (canManagePackages ? null : t('op.management.viewOnly.packages'));

  return (
    <ManagementScreen
      title={t('op.management.dest.tariffs')}
      contentWidth="full"
      state={managementScreenState(loadStatus)}
      skeleton={
        <>
          <SkeletonTabs count={2} />
          <ViewOnlyNotice reason={tabViewOnly} />
          {activeTab === 'tariffs'
            ? <TariffsTabSkeleton canManageTariffs={canManageTariffs} />
            : <PackagesTabSkeleton canManagePackages={canManagePackages} />}
        </>
      }
      failure={failure}
      onRetry={onRetry}
    >
      <Tabs
        label={t('op.management.dest.tariffs')}
        value={activeTab}
        onChange={setActiveTab}
        items={[
          { value: 'tariffs', label: t('op.management.tariffs.tab.tariffs') },
          { value: 'packages', label: t('op.management.tariffs.tab.packages') }
        ]}
      />

      <ViewOnlyNotice reason={tabViewOnly} />
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
        packageState?.status === 'failed' ? (
          <LoadFailureState
            title={t('op.management.state.errorTitle')}
            failure={packageState.failure ?? projectOperatorError(undefined, t)}
            onRetry={() => onRetryPackages?.()}
          />
        ) : packageState?.status === 'loading' && packageState.data.length === 0 ? (
          <DeferredSkeleton><PackagesTabSkeleton canManagePackages={canManagePackages} /></DeferredSkeleton>
        ) : (
          <PackagesTab
            packageOptions={packageState?.data ?? packageOptions ?? []}
            currencyCode={currencyCode}
            backend={backend}
            canManagePackages={canManagePackages}
            onReload={onReload ?? (async () => {})}
            onFeedback={onFeedback ?? (() => {})}
          />
        )
      )}
    </ManagementScreen>
  );
}
