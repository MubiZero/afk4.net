import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Archive, Package, Pencil } from 'lucide-react';
import { ManagementScreen } from '../ManagementScreen';
import { MgmtTable } from '../kit/MgmtTable';
import { MgmtDrawer } from '../kit/MgmtDrawer';
import type { RowAction } from '../kit/types';
import { PanelModal } from '../../PanelModal';
import { CriticalActionConfirmation, Money } from '../../operatorPrimitives';
import { ProductBarcodesSection } from '../../settings/ProductBarcodesSection';
import { projectOperatorError } from '../../apiErrors';
import { hasPermission, permissionNames } from '../../operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  formatMoneyInputMinorUnits,
  isGuid,
  parseNonNegativeMoneyInputMinorUnits,
  readBoolean,
  readMoney,
  readNumber,
  readString,
  requireBackend
} from '../../operatorHelpers';
import { managementScreenState, type DestinationProps } from './types';

type Product = Record<string, unknown>;

interface DelistAction {
  productId: string;
  name: string;
}

// Товары: список+drawer CRUD по эталону «Залы и ПК»/«Тарифы»/«Сотрудники» (см.
// task-C3-goods-brief.md). Одна сущность — товары; категория задаётся при создании товара и
// дальше не меняется (как в оригинале). createProduct/updateProduct/delistProduct портированы
// 1:1 из settings/SettingsGoodsSection.tsx.runAction — те же клиентские вызовы, идемпотентные
// ключи и двойной permission-гейт (canManagePosCatalog проп + серверный hasPermission на каждый
// вызов). Штрихкоды остаются отдельным самодостаточным виджетом (ProductBarcodesSection, не
// трогаем), встроенным в drawer; их гейтит canManageInventoryStock, а не canManagePosCatalog.
// Усиление против оригинала: «Снять с продажи» — через CriticalActionConfirmation (в старой
// форме было одним кликом). Cap каталога `slice(0, 8)` из оригинала снят — показываем весь
// список (0..сотни, дизайн под масштаб).
//
// Категория при создании: PosProductDto, реально возвращаемый API, несёт только categoryId
// (Guid) — сервер не отдаёт человекочитаемое имя категории, и GET-эндпоинта списка категорий не
// существует. Поэтому выбор «существующая категория» из читаемых имён невозможен без выдумывания
// источника — модалка создания оставлена как в оригинале: имя новой категории текстом →
// createProductCategory → createProduct.
export function GoodsDestination({
  backend,
  session,
  currencyCode,
  catalog,
  onCatalogChange,
  onReload,
  onFeedback,
  onDirtyChange,
  loadStatus,
  errorDetail,
  onRetry
}: DestinationProps) {
  const { t } = useI18n();
  const catalogRows = catalog ?? [];

  const [selectedProductId, setSelectedProductId] = useState<string | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [delistAction, setDelistAction] = useState<DelistAction | null>(null);
  const [categoryName, setCategoryName] = useState('');
  const [name, setName] = useState('');
  const [sku, setSku] = useState('');
  const [price, setPrice] = useState('0.00');
  const [trackStock, setTrackStock] = useState(true);
  const [allowNegativeStock, setAllowNegativeStock] = useState(false);
  const [availableInShell, setAvailableInShell] = useState(false);
  const [reorderThreshold, setReorderThreshold] = useState('0');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    onDirtyChange?.(false);
  }, [onDirtyChange]);

  // Если выбранный товар пропал из выборки (снят/reload) — закрыть drawer, а не показывать
  // устаревшую запись.
  useEffect(() => {
    setSelectedProductId((current) => (current && catalogRows.some((product) => readString(product, 'productId') === current) ? current : null));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [catalogRows]);

  const selectedProduct = catalogRows.find((product) => readString(product, 'productId') === selectedProductId) ?? null;
  const selectedProductIsActive = readBoolean(selectedProduct, 'isActive', true);

  // Засев формы drawer'а из выбранного товара.
  useEffect(() => {
    if (!selectedProduct) return;
    setName(readString(selectedProduct, 'name'));
    setSku(readString(selectedProduct, 'sku'));
    setPrice(formatMoneyInputMinorUnits(readMoney(selectedProduct, 'price')?.minorUnits ?? 0));
    setTrackStock(readBoolean(selectedProduct, 'trackStock', true));
    setAllowNegativeStock(readBoolean(selectedProduct, 'allowNegativeStock'));
    setAvailableInShell(readBoolean(selectedProduct, 'availableInShell'));
    setReorderThreshold(String(readNumber(selectedProduct, 'reorderThreshold', 0)));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedProductId]);

  const canManagePosCatalog = backend !== null && hasPermission(session, permissionNames.managePosCatalog);
  const canManageInventoryStock = backend !== null && hasPermission(session, permissionNames.manageInventoryStock);

  const openCreate = () => {
    setCategoryName('');
    setName('');
    setSku('');
    setPrice('0.00');
    setTrackStock(true);
    setAllowNegativeStock(false);
    setAvailableInShell(false);
    setReorderThreshold('0');
    setCreateOpen(true);
  };

  const submitCreate = async () => {
    const label = t('op.settings.action.createProduct');
    setBusy(true);
    onFeedback?.({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
        throw new Error(t('op.settings.pos.error.noPerm'));
      }

      const trimmedCategoryName = categoryName.trim();
      const trimmedName = name.trim();
      const trimmedSku = sku.trim();
      const priceMinorUnits = parseNonNegativeMoneyInputMinorUnits(price);
      if (!trimmedCategoryName || !trimmedName || !trimmedSku || priceMinorUnits === null) {
        throw new Error(t('op.settings.pos.error.fillCreate'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const category = await apiClients.settings.createProductCategory(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        name: trimmedCategoryName,
        idempotencyKey: createIdempotencyKey('pos-category-create')
      });
      const categoryId = readString(category, 'categoryId');
      if (!categoryId) {
        throw new Error(t('op.settings.pos.error.categoryNotConfirmed'));
      }

      const product = await apiClients.settings.createProduct(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        categoryId,
        name: trimmedName,
        sku: trimmedSku,
        price: { currencyCode, minorUnits: priceMinorUnits },
        trackStock,
        allowNegativeStock,
        availableInShell,
        reorderThreshold: Number(reorderThreshold) || 0,
        idempotencyKey: createIdempotencyKey('pos-product-create')
      });
      onCatalogChange?.([...catalogRows, product]);
      setSelectedProductId(readString(product, 'productId'));
      setCreateOpen(false);
      onFeedback?.({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitEdit = async () => {
    if (!selectedProduct) return;
    const label = t('op.settings.action.updateProduct');
    setBusy(true);
    onFeedback?.({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
        throw new Error(t('op.settings.pos.error.noPerm'));
      }

      const productId = readString(selectedProduct, 'productId');
      const trimmedName = name.trim();
      const trimmedSku = sku.trim();
      const priceMinorUnits = parseNonNegativeMoneyInputMinorUnits(price);
      if (!isGuid(productId) || !trimmedName || !trimmedSku || priceMinorUnits === null) {
        throw new Error(t('op.settings.pos.error.fillUpdate'));
      }

      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.settings.updateProduct(nextBackend.branchId, productId, {
        organizationId: nextBackend.session.organizationId,
        categoryId: readString(selectedProduct, 'categoryId'),
        name: trimmedName,
        sku: trimmedSku,
        price: { currencyCode, minorUnits: priceMinorUnits },
        trackStock,
        allowNegativeStock,
        availableInShell,
        reorderThreshold: Number(reorderThreshold) || 0,
        // Сохраняем текущее состояние «в продаже/снят» — редактирование карточки не должно
        // втихую возвращать в продажу товар, снятый через отдельное действие «Снять с продажи».
        isActive: readBoolean(selectedProduct, 'isActive', true)
      });
      await onReload?.(nextBackend);
      onFeedback?.({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const confirmDelist = async () => {
    if (!delistAction) return;
    const label = t('op.settings.action.delistProduct');
    const { productId } = delistAction;
    setDelistAction(null);
    onFeedback?.({ label, state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.managePosCatalog)) {
        throw new Error(t('op.settings.pos.error.noPerm'));
      }

      const product = catalogRows.find((item) => readString(item, 'productId') === productId);
      if (!product) {
        throw new Error(t('op.settings.pos.error.fillUpdate'));
      }

      const productPrice = readMoney(product, 'price');
      const apiClients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      await apiClients.settings.updateProduct(nextBackend.branchId, productId, {
        organizationId: nextBackend.session.organizationId,
        categoryId: readString(product, 'categoryId'),
        name: readString(product, 'name'),
        sku: readString(product, 'sku'),
        price: { currencyCode: productPrice?.currencyCode ?? currencyCode, minorUnits: productPrice?.minorUnits ?? 0 },
        trackStock: readBoolean(product, 'trackStock', true),
        allowNegativeStock: readBoolean(product, 'allowNegativeStock'),
        availableInShell: readBoolean(product, 'availableInShell'),
        reorderThreshold: readNumber(product, 'reorderThreshold', 0),
        isActive: false
      });
      setSelectedProductId((current) => (current === productId ? null : current));
      await onReload?.(nextBackend);
      onFeedback?.({ label, state: 'confirmed' });
    } catch (error) {
      onFeedback?.({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    }
  };

  const rowActions = canManagePosCatalog
    ? (product: Product): RowAction[] => [
      {
        id: 'edit',
        label: t('op.settings.action.updateProduct'),
        icon: <Pencil size={14} aria-hidden="true" />,
        onSelect: () => setSelectedProductId(readString(product, 'productId'))
      },
      {
        id: 'delist',
        label: t('op.settings.action.delistProduct'),
        icon: <Archive size={14} aria-hidden="true" />,
        danger: true,
        disabled: !readBoolean(product, 'isActive', true),
        onSelect: () => setDelistAction({ productId: readString(product, 'productId'), name: readString(product, 'name', t('op.settings.pos.productFallback')) })
      }
    ]
    : undefined;

  const drawerActions: RowAction[] = selectedProduct && canManagePosCatalog && selectedProductIsActive
    ? [
      {
        id: 'delist',
        label: t('op.settings.action.delistProduct'),
        icon: <Archive size={14} aria-hidden="true" />,
        danger: true,
        onSelect: () => setDelistAction({ productId: readString(selectedProduct, 'productId'), name: readString(selectedProduct, 'name', t('op.settings.pos.productFallback')) })
      }
    ]
    : [];

  return (
    <ManagementScreen
      title={t('op.management.dest.goods')}
      subtitle={t('op.management.dest.goods.subtitle')}
      contentWidth="full"
      state={managementScreenState(loadStatus)}
      errorDetail={errorDetail}
      onRetry={onRetry}
    >
      <div className="mgmt-master-detail">
        <MgmtTable<Product>
          columns={[
            { key: 'name', header: t('op.management.goods.col.name'), render: (product) => readString(product, 'name', t('op.settings.pos.productFallback')) },
            { key: 'sku', header: t('op.management.goods.col.sku'), render: (product) => readString(product, 'sku', '—') },
            {
              key: 'price',
              header: t('op.management.goods.col.price'),
              align: 'end',
              render: (product) => {
                const money = readMoney(product, 'price');
                return <Money minorUnits={money?.minorUnits ?? 0} currencyCode={money?.currencyCode ?? currencyCode} />;
              }
            },
            { key: 'category', header: t('op.management.goods.col.category'), render: (product) => readString(product, 'categoryName', '—') },
            {
              key: 'stock',
              header: t('op.management.goods.col.stock'),
              align: 'end',
              render: (product) => (readBoolean(product, 'trackStock', true) ? String(readNumber(product, 'stockOnHand', 0)) : '—')
            },
            {
              key: 'status',
              header: t('op.management.goods.col.status'),
              render: (product) => (readBoolean(product, 'isActive', true) ? t('op.management.goods.statusActive') : t('op.management.goods.statusDelisted'))
            }
          ]}
          rows={catalogRows}
          rowKey={(product) => readString(product, 'productId')}
          gridTemplate="1.3fr 0.9fr 0.8fr 1fr 0.7fr 0.9fr"
          selectedKey={selectedProductId}
          onSelectRow={(product) => setSelectedProductId(readString(product, 'productId'))}
          rowActions={rowActions}
          toolbar={{
            title: t('op.settings.pos.title'),
            primary: canManagePosCatalog ? { label: t('op.management.goods.addProductCta'), onClick: openCreate } : undefined
          }}
          empty={{
            icon: <Package size={22} aria-hidden="true" />,
            title: t('op.management.goods.productsEmpty.title'),
            description: t('op.management.goods.productsEmpty.description'),
            action: canManagePosCatalog ? { label: t('op.management.goods.addProductCta'), onClick: openCreate } : undefined
          }}
        />

        {selectedProduct && (
          <MgmtDrawer
            title={readString(selectedProduct, 'name', t('op.settings.pos.productFallback'))}
            subtitle={selectedProductIsActive ? t('op.management.goods.statusActive') : t('op.management.goods.statusDelisted')}
            actions={drawerActions}
            onClose={() => setSelectedProductId(null)}
            footer={
              canManagePosCatalog ? (
                <div className="mgmt-form-actions">
                  <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => void submitEdit()}>
                    {t('common.save')}
                  </button>
                </div>
              ) : undefined
            }
          >
            <div className="mgmt-drawer-section">
              <div className="mgmt-section-title"><span>{t('op.management.goods.productSection.title')}</span></div>
              <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitEdit(); }}>
                <div className="mgmt-form-grid">
                  <label>{t('op.settings.pos.productName')}
                    <input value={name} disabled={!canManagePosCatalog || busy} onChange={(event) => setName(event.currentTarget.value)} />
                  </label>
                  <label>{t('op.settings.pos.sku')}
                    <input value={sku} disabled={!canManagePosCatalog || busy} onChange={(event) => setSku(event.currentTarget.value)} />
                  </label>
                  <label>{t('op.settings.pos.price')}
                    <input inputMode="decimal" value={price} disabled={!canManagePosCatalog || busy} onChange={(event) => setPrice(event.currentTarget.value)} />
                  </label>
                  <label>{t('op.settings.pos.trackStock')}
                    <select value={trackStock ? 'yes' : 'no'} disabled={!canManagePosCatalog || busy} onChange={(event) => setTrackStock(event.currentTarget.value === 'yes')}>
                      <option value="yes">{t('op.settings.pos.yes')}</option>
                      <option value="no">{t('op.settings.pos.no')}</option>
                    </select>
                  </label>
                  <label>{t('op.settings.pos.allowNegative')}
                    <select value={allowNegativeStock ? 'yes' : 'no'} disabled={!canManagePosCatalog || busy} onChange={(event) => setAllowNegativeStock(event.currentTarget.value === 'yes')}>
                      <option value="no">{t('op.settings.pos.no')}</option>
                      <option value="yes">{t('op.settings.pos.yes')}</option>
                    </select>
                  </label>
                  <label>{t('op.settings.pos.availableInShell')}
                    <input type="checkbox" checked={availableInShell} disabled={!canManagePosCatalog || busy} onChange={(event) => setAvailableInShell(event.currentTarget.checked)} />
                  </label>
                  <label>{t('op.settings.pos.reorderThreshold')}
                    <input inputMode="numeric" value={reorderThreshold} disabled={!canManagePosCatalog || busy} onChange={(event) => setReorderThreshold(event.currentTarget.value)} />
                    <span className="settings-field-hint">{t('op.settings.pos.reorderThresholdHint')}</span>
                  </label>
                </div>
              </form>
            </div>

            <div className="mgmt-drawer-section">
              <ProductBarcodesSection
                productId={readString(selectedProduct, 'productId')}
                backend={backend}
                organizationId={backend?.session.organizationId ?? ''}
                canManage={canManageInventoryStock}
              />
            </div>
          </MgmtDrawer>
        )}
      </div>

      {createOpen && (
        <PanelModal title={t('op.management.goods.productModal.createTitle')} onClose={() => setCreateOpen(false)} closeDisabled={busy}>
          <form className="mgmt-form" onSubmit={(event) => { event.preventDefault(); void submitCreate(); }}>
            <div className="mgmt-form-grid">
              <label>{t('op.settings.pos.category')}
                <input value={categoryName} disabled={busy} onChange={(event) => setCategoryName(event.currentTarget.value)} autoFocus />
              </label>
              <label>{t('op.settings.pos.productName')}
                <input value={name} disabled={busy} onChange={(event) => setName(event.currentTarget.value)} />
              </label>
              <label>{t('op.settings.pos.sku')}
                <input value={sku} disabled={busy} onChange={(event) => setSku(event.currentTarget.value)} />
              </label>
              <label>{t('op.settings.pos.price')}
                <input inputMode="decimal" value={price} disabled={busy} onChange={(event) => setPrice(event.currentTarget.value)} />
              </label>
              <label>{t('op.settings.pos.trackStock')}
                <select value={trackStock ? 'yes' : 'no'} disabled={busy} onChange={(event) => setTrackStock(event.currentTarget.value === 'yes')}>
                  <option value="yes">{t('op.settings.pos.yes')}</option>
                  <option value="no">{t('op.settings.pos.no')}</option>
                </select>
              </label>
              <label>{t('op.settings.pos.allowNegative')}
                <select value={allowNegativeStock ? 'yes' : 'no'} disabled={busy} onChange={(event) => setAllowNegativeStock(event.currentTarget.value === 'yes')}>
                  <option value="no">{t('op.settings.pos.no')}</option>
                  <option value="yes">{t('op.settings.pos.yes')}</option>
                </select>
              </label>
              <label>{t('op.settings.pos.availableInShell')}
                <input type="checkbox" checked={availableInShell} disabled={busy} onChange={(event) => setAvailableInShell(event.currentTarget.checked)} />
              </label>
              <label>{t('op.settings.pos.reorderThreshold')}
                <input inputMode="numeric" value={reorderThreshold} disabled={busy} onChange={(event) => setReorderThreshold(event.currentTarget.value)} />
                <span className="settings-field-hint">{t('op.settings.pos.reorderThresholdHint')}</span>
              </label>
            </div>
            <div className="mgmt-form-actions">
              <button type="button" className="ui-btn" onClick={() => setCreateOpen(false)} disabled={busy}>{t('common.cancel')}</button>
              <button type="submit" className="ui-btn ui-btn--primary" disabled={busy}>{t('op.settings.action.createProduct')}</button>
            </div>
          </form>
        </PanelModal>
      )}

      {delistAction && (
        <CriticalActionConfirmation
          title={t('op.management.goods.confirmDelist.title')}
          detail={delistAction.name}
          impact={t('op.management.goods.confirmDelist.impact')}
          confirmLabel={t('op.settings.action.delistProduct')}
          onCancel={() => setDelistAction(null)}
          onConfirm={() => void confirmDelist()}
        />
      )}
    </ManagementScreen>
  );
}
