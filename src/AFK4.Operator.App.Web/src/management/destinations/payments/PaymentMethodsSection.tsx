import { useI18n } from '@afk4/i18n';
import type { OperatorBackendContext } from '../../../operatorTypes';
import { EskhataGatewayForm } from './EskhataGatewayForm';

interface Props {
  backend: OperatorBackendContext;
}

// Содержимое секции «Как игрок платит вам»: Eskhata Merchant — основной способ приёма.
// Обёртку-секцию с заголовком/лидом даёт PaymentsSetupSection.
export function PaymentMethodsSection({ backend }: Props) {
  const { t } = useI18n();
  return (
    <div>
      <div className="payset-subhead">{t('op.payments.primary.subhead')}</div>
      <EskhataGatewayForm backend={backend} />
    </div>
  );
}
