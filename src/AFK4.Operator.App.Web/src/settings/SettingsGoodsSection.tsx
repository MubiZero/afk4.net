import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from '../apiErrors';
import type { PosProductDto } from '../operatorApiClients';
import type { Feedback, OperatorBackendContext } from '../operatorTypes';
import { hasPermission, permissionNames } from '../operatorPermissions';
import { ProductBarcodesSection } from './ProductBarcodesSection';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  formatMoney,
  formatMoneyInputMinorUnits,
  parseMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits,
  readBoolean,
  readMoney,
  readNumber,
  readString,
  requireBackend,
  triggerFeedback
} from '../operatorHelpers';

// Раздел «Товары и склад»: форма создания/редактирования товаров + форма записи движений склада.
// Родитель отдаёт серверный catalog + сеттер onCatalogChange + currencyCode + onFeedback + onReload.
export function SettingsGoodsSection({
  catalog,
  currencyCode,
  backend,
  canManagePosCatalog,
  canManageInventoryStock,
  onCatalogChange,
  onReload,
  onFeedback
}: {
  catalog: PosProductDto[];
  currencyCode: string;
  backend: OperatorBackendContext | null;
  canManagePosCatalog: boolean;
  canManageInventoryStock: boolean;
  onCatalogChange: (catalog: PosProductDto[]) => void;
  onReload: (nextBackend: OperatorBackendContext) => Promise<void>;
  onFeedback: (feedback: Feedback) => void;
}) {
  const { t } = useI18n();

  const createProductActionKey = t('op.settings.action.createProduct');
  const updateProductActionKey = t('op.settings.action.updateProduct');
  const delistProductActionKey = t('op.settings.action.delistProduct');
  const recordMovementActionKey = t('op.settings.action.recordMovement');

  const [productCategoryName, setProductCategoryName] = useState(() => t('op.settings.prefill.categoryNameIndexed', { n: 1 }));
  const [productName, setProductName] = useState(() => t('op.settings.prefill.productNameIndexed', { n: 1 }));
  const [productSku, setProductSku] = useState('SKU-001');
  const [productPrice, setProductPrice] = useState('12.00');
  const [productTrackStock, setProductTrackStock] = useState(true);
  const [productAllowNegativeStock, setProductAllowNegativeStock] = useState(false);
  const [productAvailableInShell, setProductAvailableInShell] = useState(false);
  const [productReorderThreshold, setProductReorderThreshold] = useState('0');
  const [selectedProductId, setSelectedProductId] = useState('');
  const [stockProductId, setStockProductId] = useState('');
  const [stockMovementType, setStockMovementType] = useState('purchase');
  const [stockQuantityDelta, setStockQuantityDelta] = useState('10');
  const [stockUnitCost, setStockUnitCost] = useState('0.00');
  const [stockReason, setStockReason] = useState(() => t('op.settings.prefill.stockReason'));

  const trackedCatalog = catalog.filter((product) => readBoolean(product, 'trackStock'));

  // Засев выбора из загруженных данных: сохранить текущий, если он ещё есть
  useEffect(() => {
    setSelectedProductId((current) => catalog.some((product) => readString(product, 'productId') === current) ? current : '');
    setStockProductId((current) => catalog.some((product) => readString(product, 'productId') === current && readBoolean(product, 'trackStock'))
      ? current
      : readString(catalog.find((product) => readBoolean(product, 'trackStock')), 'productId'));
  }, [catalog]);

  const selectCatalogProduct = (product: PosProductDto) => {
    const productId = readString(product, 'productId');
    const price = readMoney(product, 'price');
    setSelectedProductId(productId);
    setProductName(readString(product, 'name', productName));
    setProductSku(readString(product, 'sku', productSku));
    setProductPrice(price ? formatMoneyInputMinorUnits(price.minorUnits) : productPrice);
    setProductTrackStock(readBoolean(product, 'trackStock', true));
    setProductAllowNegativeStock(readBoolean(product, 'allowNegativeStock'));
    setProductAvailableInShell(readBoolean(product, 'availableInShell'));
    setProductReorderThreshold(String(readNumber(product, 'reorderThreshold', 0)));
    triggerFeedback(onFeedback, readString(product, 'name', t('op.settings.pos.productFallback')), 'confirmed');
  };

  const runAction = async (label: string) => {
    onFeedback({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      if (label === createProductActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error(t('op.settings.pos.error.noPerm'));
        }

        const categoryName = productCategoryName.trim();
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!categoryName || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error(t('op.settings.pos.error.fillCreate'));
        }

        const category = await apiClients.settings.createProductCategory(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          name: categoryName,
          idempotencyKey: createIdempotencyKey('pos-category-create')
        });
        const categoryId = readString(category, 'categoryId');
        if (!categoryId) {
          throw new Error(t('op.settings.pos.error.categoryNotConfirmed'));
        }

        const product = await apiClients.settings.createProduct(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          categoryId,
          name: nextProductName,
          sku,
          price: { currencyCode, minorUnits: priceMinorUnits },
          trackStock: productTrackStock,
          allowNegativeStock: productAllowNegativeStock,
          availableInShell: productAvailableInShell,
          reorderThreshold: Number(productReorderThreshold) || 0,
          idempotencyKey: createIdempotencyKey('pos-product-create')
        });
        onCatalogChange([...catalog, product]);
        const nextIndex = catalog.length + 2;
        setProductCategoryName(t('op.settings.prefill.categoryNameIndexed', { n: nextIndex })); // editable prefill
        setProductName(t('op.settings.prefill.productNameIndexed', { n: nextIndex })); // editable prefill
        setProductSku(`SKU-${String(nextIndex).padStart(3, '0')}`);
        setProductPrice('12.00');
        setProductTrackStock(true);
        setProductAllowNegativeStock(false);
        setProductAvailableInShell(false);
        setSelectedProductId(readString(product, 'productId'));
      } else if (label === updateProductActionKey || label === delistProductActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
          throw new Error(t('op.settings.pos.error.noPerm'));
        }

        const selectedProduct = catalog.find((product) => readString(product, 'productId') === selectedProductId);
        const nextProductName = productName.trim();
        const sku = productSku.trim();
        const priceMinorUnits = parseMoneyInputMinorUnits(productPrice);
        if (!selectedProduct || !nextProductName || !sku || priceMinorUnits === null) {
          throw new Error(t('op.settings.pos.error.fillUpdate'));
        }

        await apiClients.settings.updateProduct(nextBackend.branchId, readString(selectedProduct, 'productId'), {
          organizationId: nextBackend.session.organizationId,
          categoryId: readString(selectedProduct, 'categoryId'),
          name: nextProductName,
          sku,
          price: { currencyCode, minorUnits: priceMinorUnits },
          trackStock: productTrackStock,
          allowNegativeStock: productAllowNegativeStock,
          availableInShell: productAvailableInShell,
          reorderThreshold: Number(productReorderThreshold) || 0,
          isActive: label !== delistProductActionKey
        });
        if (label === delistProductActionKey) {
          setSelectedProductId('');
        }
        await onReload(nextBackend);
      } else if (label === recordMovementActionKey) {
        if (!hasPermission(nextBackend.session, permissionNames.manageInventoryStock)) {
          throw new Error(t('op.settings.stock.error.noPerm'));
        }

        const selectedProduct = trackedCatalog.find((product) => readString(product, 'productId') === stockProductId);
        const quantityDelta = Number(stockQuantityDelta);
        const unitCostMinorUnits = parseNonNegativeMoneyInputMinorUnits(stockUnitCost);
        const reason = stockReason.trim();
        if (!selectedProduct || !Number.isInteger(quantityDelta) || quantityDelta === 0 || unitCostMinorUnits === null || !reason) {
          throw new Error(t('op.settings.stock.error.fillFields'));
        }

        await apiClients.inventory.createStockMovement(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          productId: readString(selectedProduct, 'productId'),
          movementType: stockMovementType,
          quantityDelta,
          unitCost: { currencyCode, minorUnits: unitCostMinorUnits },
          reason,
          idempotencyKey: createIdempotencyKey('stock-movement-create')
        });
        await onReload(nextBackend);
      } else {
        throw new Error(t('op.settings.generic.error.notConnected'));
      }

      onFeedback({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  return (
    <>
      <div className="settings-section-title">
        <span>{t('op.settings.pos.title')}</span>
        <div className="settings-section-actions">
          <button type="button" disabled={!canManagePosCatalog} onClick={() => runAction(createProductActionKey)}>{createProductActionKey}</button>
          <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runAction(updateProductActionKey)}>{updateProductActionKey}</button>
          <button type="button" disabled={!canManagePosCatalog || !selectedProductId} onClick={() => runAction(delistProductActionKey)}>{delistProductActionKey}</button>
        </div>
      </div>
      <div className="settings-config-grid">
        {catalog.slice(0, 8).map((product) => (
          <button key={readString(product, 'productId')} type="button" className={readString(product, 'productId') === selectedProductId ? 'active' : undefined} onClick={() => selectCatalogProduct(product)}>
            <strong>{readString(product, 'name', t('op.settings.pos.productFallback'))}</strong>
            <span>{formatMoney(readMoney(product, 'price'), currencyCode)} · {t('op.settings.pos.stockOnHand', { count: readNumber(product, 'stockOnHand', 0) })}</span>
          </button>
        ))}
      </div>
      <div className="settings-form-grid settings-pos-form">
        <label>{t('op.settings.pos.category')}<input value={productCategoryName} disabled={!canManagePosCatalog} onChange={(event) => setProductCategoryName(event.currentTarget.value)} /></label>
        <label>{t('op.settings.pos.productName')}<input value={productName} disabled={!canManagePosCatalog} onChange={(event) => setProductName(event.currentTarget.value)} /></label>
        <label>{t('op.settings.pos.sku')}<input value={productSku} disabled={!canManagePosCatalog} onChange={(event) => setProductSku(event.currentTarget.value)} /></label>
        <label>{t('op.settings.pos.price')}<input inputMode="decimal" value={productPrice} disabled={!canManagePosCatalog} onChange={(event) => setProductPrice(event.currentTarget.value)} /></label>
        <label>{t('op.settings.pos.trackStock')}
          <select value={productTrackStock ? 'yes' : 'no'} disabled={!canManagePosCatalog} onChange={(event) => setProductTrackStock(event.currentTarget.value === 'yes')}>
            <option value="yes">{t('op.settings.pos.yes')}</option>
            <option value="no">{t('op.settings.pos.no')}</option>
          </select>
        </label>
        <label>{t('op.settings.pos.allowNegative')}
          <select value={productAllowNegativeStock ? 'yes' : 'no'} disabled={!canManagePosCatalog} onChange={(event) => setProductAllowNegativeStock(event.currentTarget.value === 'yes')}>
            <option value="no">{t('op.settings.pos.no')}</option>
            <option value="yes">{t('op.settings.pos.yes')}</option>
          </select>
        </label>
        <label>{t('op.settings.pos.availableInShell')}
          <input type="checkbox" checked={productAvailableInShell} disabled={!canManagePosCatalog} onChange={(event) => setProductAvailableInShell(event.currentTarget.checked)} />
        </label>
        <label>{t('op.settings.pos.reorderThreshold')}
          <input inputMode="numeric" value={productReorderThreshold} disabled={!canManagePosCatalog} onChange={(event) => setProductReorderThreshold(event.currentTarget.value)} />
          <span className="settings-field-hint">{t('op.settings.pos.reorderThresholdHint')}</span>
        </label>
      </div>
      {selectedProductId ? (
        <ProductBarcodesSection
          productId={selectedProductId}
          backend={backend}
          organizationId={backend?.session.organizationId ?? ''}
          canManage={canManageInventoryStock}
        />
      ) : canManagePosCatalog ? (
        <p className="settings-barcodes-save-hint">{t('op.barcode.saveFirst')}</p>
      ) : null}
      <div className="settings-section-title">
        <span>{t('op.settings.stock.title')}</span>
        <button type="button" disabled={!canManageInventoryStock || trackedCatalog.length === 0} onClick={() => runAction(recordMovementActionKey)}>{t('op.settings.stock.recordBtn')}</button>
      </div>
      <div className="settings-form-grid settings-stock-form">
        <label>{t('op.settings.stock.product')}
          <select value={stockProductId} disabled={!canManageInventoryStock || trackedCatalog.length === 0} onChange={(event) => setStockProductId(event.currentTarget.value)}>
            {trackedCatalog.length === 0 && <option value="">{t('op.settings.stock.noTrackedProducts')}</option>}
            {trackedCatalog.map((product) => (
              <option key={readString(product, 'productId')} value={readString(product, 'productId')}>{readString(product, 'name', t('op.settings.pos.productFallback'))} · {t('op.settings.pos.stockOnHand', { count: readNumber(product, 'stockOnHand', 0) })}</option>
            ))}
          </select>
        </label>
        <label>{t('op.settings.stock.type')}
          <select value={stockMovementType} disabled={!canManageInventoryStock} onChange={(event) => setStockMovementType(event.currentTarget.value)}>
            <option value="purchase">{t('op.settings.stock.typePurchase')}</option>
            <option value="adjustment">{t('op.settings.stock.typeAdjustment')}</option>
          </select>
        </label>
        <label>{t('op.settings.stock.quantity')}<input inputMode="numeric" value={stockQuantityDelta} disabled={!canManageInventoryStock} onChange={(event) => setStockQuantityDelta(event.currentTarget.value)} /></label>
        <label>{t('op.settings.stock.unitCost')}<input inputMode="decimal" value={stockUnitCost} disabled={!canManageInventoryStock} onChange={(event) => setStockUnitCost(event.currentTarget.value)} /></label>
        <label>{t('op.settings.stock.reason')}<input value={stockReason} disabled={!canManageInventoryStock} onChange={(event) => setStockReason(event.currentTarget.value)} /></label>
      </div>
    </>
  );
}
