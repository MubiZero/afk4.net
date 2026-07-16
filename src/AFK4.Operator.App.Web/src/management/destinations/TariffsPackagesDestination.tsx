import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { SettingsTariffsSection } from '../../settings/SettingsTariffsSection';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import type { DestinationProps } from './types';

// Тарифы и пакеты: тонкая обёртка над SettingsTariffsSection (тариф-версии + пакеты времени) в
// общем каркасе ManagementScreen. Раздел сохраняет по-действию (создать/обновить/списать
// тариф или пакет), поэтому без save-бара — сигнализируем "clean" сразу на маунте.
export function TariffsPackagesDestination({
  backend,
  session,
  currencyCode,
  tariffs,
  packageOptions,
  onReload,
  onFeedback,
  onDirtyChange
}: DestinationProps) {
  const { t } = useI18n();

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  const canManageTariffs = backend !== null && hasPermission(session, permissionNames.manageTariffs);
  const canManagePackages = backend !== null && hasPermission(session, permissionNames.managePackages);

  return (
    <ManagementScreen
      title={t('op.management.dest.tariffs')}
      subtitle={t('op.management.dest.tariffs.subtitle')}
    >
      <div className="management-panel">
        <SettingsTariffsSection
          tariffs={tariffs ?? []}
          packageOptions={packageOptions ?? []}
          currencyCode={currencyCode}
          backend={backend}
          canManageTariffs={canManageTariffs}
          canManagePackages={canManagePackages}
          onReload={onReload ?? (async () => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      </div>
    </ManagementScreen>
  );
}
