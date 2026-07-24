import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState } from '../../operatorPrimitives';
import type { OperatorBackendContext } from '../../operatorTypes';

// Плейсхолдер: наполняется в Task 8 (org-audit — журнал действий по сети).
export function JournalDestination({ backend: _backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t } = useI18n();
  return (
    <ManagementScreen title={t('op.network.dest.journal')} subtitle={t('op.network.dest.journal.subtitle')} contentWidth="full">
      <EmptyState title={t('op.network.placeholder')} />
    </ManagementScreen>
  );
}
