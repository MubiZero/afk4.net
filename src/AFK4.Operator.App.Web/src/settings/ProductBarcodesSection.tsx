import { useEffect, useState, useCallback } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from '../operatorHelpers';
import { useToast } from '../operatorToast';
import type { ProductBarcodeDto } from '../api/clients/settings';
import type { OperatorBackendContext } from '../operatorTypes';

// Секция «Штрих-коды» в карточке товара.
// Монтируется только при наличии productId (сохранённый товар).
export function ProductBarcodesSection({
  productId,
  backend,
  organizationId,
  canManage,
}: {
  productId: string;
  backend: OperatorBackendContext | null;
  organizationId: string;
  canManage: boolean;
}) {
  const { t } = useI18n();
  const toast = useToast();
  const [barcodes, setBarcodes] = useState<ProductBarcodeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [manualCode, setManualCode] = useState('');
  const [error, setError] = useState<string | null>(null);

  const fetchBarcodes = useCallback(async () => {
    if (!backend || !productId) return;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    const result = await clients.settings.getProductBarcodes(backend.branchId, productId);
    setBarcodes(result);
    setLoading(false);
  }, [backend, productId]);

  useEffect(() => {
    setLoading(true);
    void fetchBarcodes();
  }, [fetchBarcodes]);

  const addCode = async (code: string) => {
    const trimmed = code.trim();
    if (!trimmed || !backend) return;
    if (barcodes.some((b) => b.code === trimmed)) {
      setError(t('op.barcode.duplicate'));
      return;
    }
    setError(null);
    const isPrimary = barcodes.length === 0;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    await clients.settings.addProductBarcode(backend.branchId, productId, {
      organizationId,
      code: trimmed,
      isPrimary,
    });
    setManualCode('');
    toast.success(t('op.barcode.added'));
    await fetchBarcodes();
  };

  const removeBarcode = async (barcodeId: string) => {
    if (!backend) return;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    await clients.settings.deleteProductBarcode(backend.branchId, productId, barcodeId);
    await fetchBarcodes();
  };

  if (loading) return null;

  return (
    <div className="settings-barcodes-section">
      <div className="settings-section-subtitle">{t('op.barcode.section.title')}</div>
      {barcodes.length > 0 && (
        <div className="barcode-chips">
          {barcodes.map((b) => (
            <span key={b.barcodeId} className={`barcode-chip${b.isPrimary ? ' is-primary' : ''}`}>
              <span className="barcode-code">{b.code}</span>
              {b.isPrimary && <span className="primary-label">{t('op.barcode.primary')}</span>}
              {canManage && (
                <button
                  type="button"
                  className="barcode-remove-btn"
                  aria-label={t('op.barcode.remove')}
                  onClick={() => void removeBarcode(b.barcodeId)}
                >
                  ×
                </button>
              )}
            </span>
          ))}
        </div>
      )}
      {error && <p className="settings-barcodes-error">{error}</p>}
      {canManage && (
        <div className="barcode-add-row">
          <input
            type="text"
            placeholder={t('op.barcode.manualPlaceholder')}
            value={manualCode}
            onChange={(e) => setManualCode(e.currentTarget.value)}
            onKeyDown={(e) => { if (e.key === 'Enter') { void addCode(manualCode); } }}
          />
          <button type="button" className="ui-btn" onClick={() => void addCode(manualCode)}>
            {t('op.barcode.add')}
          </button>
        </div>
      )}
    </div>
  );
}
