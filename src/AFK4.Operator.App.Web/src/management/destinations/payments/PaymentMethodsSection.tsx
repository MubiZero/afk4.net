import { useI18n } from '@afk4/i18n';
import { PaymentGatewaysWorkspace } from '../../../PaymentGatewaysWorkspace';
import type { OperatorBackendContext } from '../../../operatorTypes';
import { EskhataGatewayForm } from './EskhataGatewayForm';

interface Props {
  backend: OperatorBackendContext;
}

// Содержимое секции «Как игрок платит вам»: Eskhata Merchant — основной способ (сверху), перевод
// на карту DushanbeCity — дополнительный (ниже). Обёртку-секцию с заголовком/лидом даёт
// PaymentsSetupSection.
export function PaymentMethodsSection({ backend }: Props) {
  const { t } = useI18n();
  return (
    <div>
      <div className="payset-subhead">{t('op.payments.primary.subhead')}</div>
      <EskhataGatewayForm backend={backend} />

      <div className="payset-divider" />

      <div className="payset-subhead">{t('op.payments.dc.subhead')}</div>
      <p className="payset-note">{t('op.payments.dc.note')}</p>
      <PaymentGatewaysWorkspace backend={backend} />
    </div>
  );
}
