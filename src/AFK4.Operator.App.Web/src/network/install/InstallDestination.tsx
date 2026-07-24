import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState } from '../../operatorPrimitives';
import type { OperatorBackendContext } from '../../operatorTypes';

// Плейсхолдер: наполняется в Task 7 (установка нового ПК через Мастер).
export function InstallDestination({ backend: _backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t } = useI18n();
  return (
    <ManagementScreen title={t('op.network.dest.install')} subtitle={t('op.network.dest.install.subtitle')} contentWidth="full">
      <EmptyState title={t('op.network.placeholder')} />
    </ManagementScreen>
  );
}
