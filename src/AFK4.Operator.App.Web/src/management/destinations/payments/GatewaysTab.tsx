import { PaymentGatewaysWorkspace } from '../../../PaymentGatewaysWorkspace';
import type { OperatorBackendContext } from '../../../operatorTypes';
import { EskhataGatewayForm } from './EskhataGatewayForm';

interface Props {
  backend: OperatorBackendContext;
}

// Вкладка «Платёжные шлюзы»: сверху существующий dcgate (карты + Telegram), ниже — реквизиты
// Eskhata. Модель мгновенных действий (у каждого блока свои кнопки), общего save-бара нет.
export function GatewaysTab({ backend }: Props) {
  return (
    <div className="management-panel payment-gateways">
      <PaymentGatewaysWorkspace backend={backend} />
      <EskhataGatewayForm backend={backend} />
    </div>
  );
}
