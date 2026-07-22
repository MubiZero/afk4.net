import { useI18n } from '@afk4/i18n';
import type { OperatorBackendContext } from '../../../operatorTypes';
import { EskhataGatewayForm } from './EskhataGatewayForm';
import { DcTransferForm } from './DcTransferForm';

interface Props {
  backend: OperatorBackendContext;
}

// Содержимое секции «Как игрок платит вам»: Eskhata Merchant — основной способ приёма,
// DushanbeCity (ручной перевод по карте) — второй способ. Обёртку-секцию с заголовком/лидом
// даёт PaymentsSetupSection.
export function PaymentMethodsSection({ backend }: Props) {
  const { t } = useI18n();
  return (
    <div>
      <div className="payset-subhead">{t('op.payments.primary.subhead')}</div>
      <EskhataGatewayForm backend={backend} />

      <div className="payset-divider" />
      <div className="payset-subhead">{t('op.dc.subhead')}</div>
      <p className="payset-note">{t('op.dc.note')}</p>
      <DcTransferForm backend={backend} />
    </div>
  );
}
