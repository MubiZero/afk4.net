import { useI18n } from '@afk4/i18n';
import { PaymentGatewaysWorkspace } from '../../../PaymentGatewaysWorkspace';
import type { OperatorBackendContext } from '../../../operatorTypes';
import { EskhataGatewayForm } from './EskhataGatewayForm';

interface Props {
  backend: OperatorBackendContext;
}

// Зона «Как игрок платит вам»: настраиваемые онлайн-способы приёма денег — dcgate (приём по
// картам + привязка Telegram) и реквизиты Eskhata. Каждый блок работает моделью мгновенных
// действий (свои кнопки), общего save-бара на экране нет.
export function PaymentMethodsSection({ backend }: Props) {
  const { t } = useI18n();
  return (
    <section className="management-panel payment-methods">
      <h3 className="payment-zone-title">{t('op.payments.zone.income')}</h3>
      <PaymentGatewaysWorkspace backend={backend} />
      <EskhataGatewayForm backend={backend} />
    </section>
  );
}
