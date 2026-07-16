import { useEffect } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../ManagementScreen';
import { PaymentGatewaysWorkspace } from '../../PaymentGatewaysWorkspace';
import { EmptyState } from '../../operatorPrimitives';
import type { DestinationProps } from './types';

// Оплата: тонкая обёртка над PaymentGatewaysWorkspace (провижининг карт + Telegram-привязка) в
// общем каркасе ManagementScreen. Разделу не нужны settings-domain данные ManagementWorkspace
// (zones/staff/catalog/...) — он строит собственный клиент из backend, поэтому save-бара нет,
// сигнализируем "clean" сразу на маунте. PaymentGatewaysWorkspace требует не-null backend для
// своего клиента (нет фикстур/офлайн-режима), так что при backend === null показываем честный
// empty-state вместо попытки построить клиент без sessions/branchId.
export function PaymentDestination({ backend, onDirtyChange }: DestinationProps) {
  const { t } = useI18n();

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  return (
    <ManagementScreen
      title={t('op.management.dest.payment')}
      subtitle={t('op.management.dest.payment.subtitle')}
      contentWidth="wide"
    >
      <div className="management-panel">
        {backend === null ? (
          <EmptyState
            title={t('op.management.dest.payment.noBackendTitle')}
            description={t('op.management.dest.payment.noBackendHint')}
          />
        ) : (
          <PaymentGatewaysWorkspace backend={backend} />
        )}
      </div>
    </ManagementScreen>
  );
}
