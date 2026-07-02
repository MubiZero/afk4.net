import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../PanelModal';
import { createAuthenticatedOperatorClients, createIdempotencyKey, requireBackend } from '../operatorHelpers';
import { Money } from '../operatorPrimitives';
import { projectOperatorError } from '../apiErrors';
import type { OperatorBackendContext } from '../operatorTypes';
import type { StockItem } from './stockLevels';

export function WriteOffDialog({
  item,
  backend,
  currencyCode,
  onClose,
  onDone,
}: {
  item: StockItem;
  backend: OperatorBackendContext | null;
  currencyCode: string;
  onClose: () => void;
  onDone: () => void;
}) {
  const { t } = useI18n();
  const [qtyText, setQtyText] = useState('1');
  const [reason, setReason] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    const quantity = Number(qtyText);
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > item.stockOnHand) {
      setError(t('op.stock.writeoff.errorQty'));
      return;
    }
    if (!reason.trim()) {
      setError(t('op.stock.writeoff.errorReason'));
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const nextBackend = requireBackend(backend, t);
      const api = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await api.inventory.createStockMovement(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        productId: item.productId,
        movementType: 'adjustment',
        quantityDelta: -quantity,
        unitCost: { currencyCode, minorUnits: Math.max(item.avgCostMinorUnits, 0) },
        reason: reason.trim(),
        idempotencyKey: createIdempotencyKey('stock-movement-create'),
      });
      onDone();
    } catch (caught) {
      setSubmitting(false);
      setError(projectOperatorError(caught, t).detail);
    }
  };

  return (
    <PanelModal title={t('op.stock.writeoff.title')} subtitle={item.name} tone="warning" onClose={onClose}>
      <div className="recv-field">
        <span>{t('op.stock.writeoff.available', { count: item.stockOnHand })}</span>
      </div>
      <label className="ui-field">
        <span>{t('op.stock.writeoff.qty')}</span>
        <input inputMode="numeric" aria-label={t('op.stock.writeoff.qty')} value={qtyText} disabled={submitting} onChange={(event) => setQtyText(event.currentTarget.value)} />
      </label>
      <label className="ui-field">
        <span>{t('op.stock.writeoff.reason')}</span>
        <input aria-label={t('op.stock.writeoff.reason')} value={reason} disabled={submitting} placeholder={t('op.stock.writeoff.reasonPlaceholder')} onChange={(event) => setReason(event.currentTarget.value)} />
      </label>
      <div className="recv-field">
        <span>{t('op.stock.writeoff.cost')}: <Money minorUnits={Math.max(item.avgCostMinorUnits, 0)} currencyCode={currencyCode} /></span>
      </div>
      {error && <p className="recv-status err" role="alert">{error}</p>}
      <div className="critical-confirmation-actions">
        <button type="button" onClick={onClose} disabled={submitting}>{t('common.cancel')}</button>
        <button type="button" className="ui-btn ui-btn--danger" disabled={submitting} onClick={submit}>
          {submitting ? t('op.stock.writeoff.submitting') : t('op.stock.writeoff.submit')}
        </button>
      </div>
    </PanelModal>
  );
}
