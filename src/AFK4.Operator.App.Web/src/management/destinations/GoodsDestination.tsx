import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { SettingsGoodsSection } from '../../settings/SettingsGoodsSection';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import type { DestinationProps } from './types';

// Товары: тонкая обёртка над SettingsGoodsSection (каталог + штрихкоды) в общем каркасе
// ManagementScreen. Движение склада сюда не переехало — оно уже полноценно живёт в разделе
// «Склад» (приёмка/журнал/инвентаризация), здесь был бы дубль. Раздел сохраняет по-действию,
// поэтому без save-бара — сигнализируем "clean" сразу на маунте.
export function GoodsDestination({
  backend,
  session,
  currencyCode,
  catalog,
  onCatalogChange,
  onReload,
  onFeedback,
  onDirtyChange
}: DestinationProps) {
  const { t } = useI18n();

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  const canManagePosCatalog = backend !== null && hasPermission(session, permissionNames.managePosCatalog);
  const canManageInventoryStock = backend !== null && hasPermission(session, permissionNames.manageInventoryStock);

  return (
    <ManagementScreen
      title={t('op.management.dest.goods')}
      subtitle={t('op.management.dest.goods.subtitle')}
    >
      <div className="management-panel">
        <SettingsGoodsSection
          catalog={catalog ?? []}
          currencyCode={currencyCode}
          backend={backend}
          canManagePosCatalog={canManagePosCatalog}
          canManageInventoryStock={canManageInventoryStock}
          onCatalogChange={onCatalogChange ?? (() => {})}
          onReload={onReload ?? (async () => {})}
          onFeedback={onFeedback ?? (() => {})}
        />
      </div>
    </ManagementScreen>
  );
}
