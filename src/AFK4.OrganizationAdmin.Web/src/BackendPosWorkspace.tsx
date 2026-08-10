import { useCallback, useEffect, useRef, useState } from 'react';
import { Search, UserRoundPlus, X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type {
  PaymentPartDto,
  PackageOptionDto,
  PlayerSearchResultDto,
  PosProductDto,
  SettlePosSaleRequest,
  ShiftDto
} from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  emptyFeedback,
  formatMinorUnits,
  projectPlayerClient,
  readArray,
  readBoolean,
  readMoney,
  readNumber,
  readString,
  requireBackend,
  type PlayerClientItem,
  workspaceLoadStatusLabel
} from './operatorHelpers';
import { Money } from './operatorPrimitives';
import { PanelModal } from './PanelModal';
import { PaymentDialog, type PaymentBillLine } from './PaymentDialog';
import { PlatformApiError } from './platformApi';
import { useToast } from './operatorToast';
import { matchByBarcode } from './barcodeScanner';
import { useBarcodeScanner } from './useBarcodeScanner';
import { useFeedbackToasts } from './useFeedbackToasts';
import { PackagePurchasePanel } from './PackagePurchasePanel';

type PosCatalogItem = {
  productId?: string;
  name: string;
  priceMinorUnits: number;
  category: string;
  note: string;
  trackStock: boolean;
  stockOnHand: number;
  reorderThreshold: number;
  barcodes: string[];
  source: 'fixture' | 'backend';
};

export function isLowStock(item: Pick<PosCatalogItem, 'source' | 'trackStock' | 'stockOnHand' | 'reorderThreshold'>): boolean {
  return item.source === 'backend'
    && item.trackStock
    && (item.stockOnHand === 0 || (item.reorderThreshold > 0 && item.stockOnHand <= item.reorderThreshold));
}

type PosCartItem = PosCatalogItem & {
  quantity: number;
};

function projectSettlementError(error: unknown, t: ReturnType<typeof useI18n>['t']): string {
  if (!(error instanceof PlatformApiError)) {
    return projectOperatorError(error, t).detail;
  }

  let code = '';
  try {
    const body = JSON.parse(error.body) as { error?: unknown; Error?: unknown };
    const value = body.error ?? body.Error;
    code = typeof value === 'string' ? value : '';
  } catch {
    // A non-JSON error body is deliberately not shown to the operator.
  }

  switch (code) {
    case 'version_conflict':
    case 'idempotency_conflict':
    case 'sale_not_payable':
      return t('op.pos.error.settlementConflict');
    case 'open_shift_required':
      return t('op.pos.error.openShiftFirst');
    case 'invalid_payment_split':
      return t('op.pos.error.invalidPaymentSplit');
    case 'insufficient_funds':
      return t('op.pos.error.insufficientFunds');
    case 'player_required_for_wallet':
      return t('op.pos.error.playerRequiredForWallet');
    case 'out_of_stock':
    case 'product_unavailable':
      return t('op.pos.error.outOfStock');
    default:
      return t('op.pos.error.settlementFailed');
  }
}

// Sentinel for the "All" category — never shown as a backend category name
const CATEGORY_ALL = '__all__';


function makeFixtureProducts(t: ReturnType<typeof useI18n>['t']): PosCatalogItem[] {
  return [
    { name: t('op.pos.fixture.cola'), priceMinorUnits: 1200, category: t('op.pos.fixture.drinks'), note: t('op.pos.fixture.note'), trackStock: false, stockOnHand: 0, reorderThreshold: 0, barcodes: [], source: 'fixture' },
    { name: t('op.pos.fixture.water'), priceMinorUnits: 600, category: t('op.pos.fixture.drinks'), note: t('op.pos.fixture.note'), trackStock: false, stockOnHand: 0, reorderThreshold: 0, barcodes: [], source: 'fixture' },
    { name: t('op.pos.fixture.hotdog'), priceMinorUnits: 2800, category: t('op.pos.fixture.food'), note: t('op.pos.fixture.note'), trackStock: false, stockOnHand: 0, reorderThreshold: 0, barcodes: [], source: 'fixture' },
    { name: t('op.pos.fixture.guestHour'), priceMinorUnits: 2500, category: t('op.pos.fixture.services'), note: t('op.pos.fixture.note'), trackStock: false, stockOnHand: 0, reorderThreshold: 0, barcodes: [], source: 'fixture' }
  ];
}

function projectPosProduct(product: PosProductDto, t: ReturnType<typeof useI18n>['t']): PosCatalogItem {
  const price = readMoney(product, 'price');
  const sku = readString(product, 'sku', 'SKU');
  const stockOnHand = readNumber(product, 'stockOnHand', 0);
  const reorderThreshold = readNumber(product, 'reorderThreshold', 0);
  return {
    productId: readString(product, 'productId') || undefined,
    name: readString(product, 'name', t('op.pos.catalog.productFallback')),
    priceMinorUnits: price?.minorUnits ?? 0,
    category: readString(product, 'categoryName', readString(product, 'categoryId', t('op.pos.catalog.categoryFallback'))),
    note: t('op.pos.catalog.note', { sku, count: stockOnHand }),
    trackStock: readBoolean(product, 'trackStock'),
    stockOnHand,
    reorderThreshold,
    barcodes: readArray<string>(product, 'barcodes'),
    // projectPosProduct обрабатывает только реальные бэкенд-продукты (фикстуры идут через
    // makeFixtureProducts с source:'fixture'), поэтому источник всегда 'backend'.
    source: 'backend'
  };
}

export function BackendPosWorkspace({ currencyCode, backend, embedded = false }: { currencyCode: string; backend: OperatorBackendContext | null; embedded?: boolean }) {
  const { t } = useI18n();
  const toast = useToast();

  const [activeCategory, setActiveCategory] = useState(CATEGORY_ALL);
  const [productSearch, setProductSearch] = useState('');
  const [payOpen, setPayOpen] = useState(false);
  const [paymentCloseLocked, setPaymentCloseLocked] = useState(false);
  const paymentAttemptRef = useRef<{
    createSaleKey: string;
    settlementKey: string;
    saleId: string | null;
    settlementRequest: SettlePosSaleRequest | null;
  } | null>(null);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null);
  const [catalog, setCatalog] = useState<PosCatalogItem[]>(() => backend === null ? makeFixtureProducts(t) : []);
  const [playerSearch, setPlayerSearch] = useState('');
  const [posPlayers, setPosPlayers] = useState<PlayerClientItem[]>([]);
  const [selectedPlayerId, setSelectedPlayerId] = useState('');
  const [playerLoadStatus, setPlayerLoadStatus] = useState<LoadStatus>('fixture');
  const [clientPickerOpen, setClientPickerOpen] = useState(false);
  const [packageOptions, setPackageOptions] = useState<PackageOptionDto[]>([]);
  const [packageOptionsLoading, setPackageOptionsLoading] = useState(false);
  const [packageOptionsError, setPackageOptionsError] = useState<string | null>(null);
  const [cartItems, setCartItems] = useState<PosCartItem[]>(() => {
    if (backend === null) {
      const fixtures = makeFixtureProducts(t);
      return [
        { ...fixtures[0], quantity: 1 },
        { ...fixtures[3], quantity: 1 }
      ];
    }

    return [];
  });

  const loadBackendPos = async (
    nextBackend = backend,
    { preserveCartSnapshot = false }: { preserveCartSnapshot?: boolean } = {}
  ) => {
    if (nextBackend === null) {
      const fixtures = makeFixtureProducts(t);
      setLoadStatus('fixture');
      setCatalog(fixtures);
      setCartItems((items) => items.length > 0 ? items : [
        { ...fixtures[0], quantity: 1 },
        { ...fixtures[3], quantity: 1 }
      ]);
      return;
    }

    setLoadStatus('loading');
    try {
      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const [nextCatalog, nextShift] = await Promise.all([
        clients.pos.getCatalog(nextBackend.branchId),
        clients.shifts.getCurrentShift(nextBackend.branchId)
      ]);

      const products = Array.isArray(nextCatalog)
        ? nextCatalog.map((product) => projectPosProduct(product, t))
        : [];

      const backendProducts = products.filter((product) => product.source === 'backend' && product.productId);
      setCatalog(products);
      setCurrentShift(nextShift);
      if (!preserveCartSnapshot) {
        // Пересобираем чек из свежего каталога: позиции, пропавшие из каталога, отваливаются.
        // Пустой результат так и остаётся пустым — класть сюда «первый попавшийся товар» нельзя,
        // кассир получит пробитую позицию, которую не выбирал.
        setCartItems((items) => {
          const productById = new Map(backendProducts.map((product) => [product.productId, product]));
          return items
            .filter((item) => item.source === 'backend' && item.productId && productById.has(item.productId))
            .map((item) => ({
              ...productById.get(item.productId!)!,
              quantity: item.quantity
            }));
        });
      }
      setLoadStatus('backend');
    } catch (error) {
      setLoadStatus('failed');
      setFeedback({
        label: t('op.pos.feedback.pos'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  useEffect(() => {
    void loadBackendPos();
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, currencyCode]);

  useEffect(() => {
    let disposed = false;
    const query = playerSearch.trim();

    if (backend === null || query.length < 2 || !hasPermission(backend.session, permissionNames.viewPlayers)) {
      setPosPlayers([]);
      setSelectedPlayerId('');
      setPlayerLoadStatus(backend === null ? 'fixture' : 'backend');
      return undefined;
    }

    setPlayerLoadStatus('loading');
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    clients.players.searchPlayers(backend.branchId, query, 8)
      .then((players: PlayerSearchResultDto[]) => {
        if (disposed) {
          return;
        }

        const projected = Array.isArray(players) ? players.map((p) => projectPlayerClient(p, t)) : [];
        setPosPlayers(projected);
        // Клиент выбирается явным кликом по кандидату — не авто-выбираем первого,
        // иначе свёрнутый пикер закрывается раньше, чем оператор успел выбрать.
        setSelectedPlayerId((current) => projected.some((player) => player.playerAccountId === current) ? current : '');
        setPlayerLoadStatus('backend');
      })
      .catch((error) => {
        if (disposed) {
          return;
        }

        setPosPlayers([]);
        setSelectedPlayerId('');
        setPlayerLoadStatus('failed');
        setFeedback({ label: t('op.pos.feedback.client'), state: 'failed', detail: projectOperatorError(error, t).detail });
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, playerSearch]);

  const categoryAll = t('op.pos.catalog.categoryAll');
  // Все категории каталога — без потолка: строка переносит чипы на новую линию
  // (flex-wrap), а отрезать категории молча значило бы спрятать часть фильтра.
  const categories = [CATEGORY_ALL, ...Array.from(new Set(catalog.map((product) => product.category)))];
  const visibleProducts = catalog.filter((product) => {
    const categoryMatches = activeCategory === CATEGORY_ALL || product.category === activeCategory;
    const searchMatches = `${product.name} ${product.category} ${product.note}`.toLowerCase().includes(productSearch.trim().toLowerCase());
    return categoryMatches && searchMatches;
  });
  const selectedPosPlayer = posPlayers.find((player) => player.playerAccountId === selectedPlayerId) ?? null;
  const selectedPosPlayerId = selectedPosPlayer?.playerAccountId ?? null;
  const playerSearchQuery = playerSearch.trim();
  const cartTotalMinorUnits = cartItems.reduce((sum, item) => sum + item.priceMinorUnits * item.quantity, 0);
  const lowStockCount = catalog.filter(isLowStock).length;
  const shiftId = readString(currentShift, 'shiftId');

  useEffect(() => {
    let disposed = false;
    if (backend === null || selectedPosPlayer === null || !hasPermission(backend.session, permissionNames.purchasePackage)) {
      setPackageOptions([]);
      setPackageOptionsError(null);
      setPackageOptionsLoading(false);
      return undefined;
    }

    setPackageOptionsLoading(true);
    setPackageOptionsError(null);
    createAuthenticatedOperatorClients(backend.config, backend.session).settings.getPackageOptions(backend.branchId)
      .then((options) => {
        if (!disposed) setPackageOptions(Array.isArray(options) ? options : []);
      })
      .catch((error) => {
        if (!disposed) {
          setPackageOptions([]);
          setPackageOptionsError(projectOperatorError(error, t).detail);
        }
      })
      .finally(() => {
        if (!disposed) setPackageOptionsLoading(false);
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, selectedPosPlayerId]);

  const refreshPurchasedPackage = async () => {
    if (backend === null || selectedPosPlayerId === null) return;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    const [wallet] = await Promise.all([
      clients.players.getWalletSummary(selectedPosPlayerId),
      clients.players.getPlayerPackages(selectedPosPlayerId)
    ]);
    const walletBalance = readMoney(wallet, 'walletBalance');
    const debtBalance = readMoney(wallet, 'debtBalance');
    setPosPlayers((players) => players.map((player) => player.playerAccountId === selectedPosPlayerId
      ? {
          ...player,
          balanceMinorUnits: walletBalance?.minorUnits ?? player.balanceMinorUnits,
          debtMinorUnits: debtBalance?.minorUnits ?? player.debtMinorUnits
        }
      : player));
  };
  const canAcceptPayment = backend !== null
    && shiftId.length > 0
    && cartItems.length > 0
    && cartItems.every((item) => Boolean(item.productId) && item.source === 'backend')
    && hasPermission(backend.session, permissionNames.createPosSale)
    && hasPermission(backend.session, permissionNames.payPosSale);

  const addProduct = useCallback((product: PosCatalogItem) => {
    setCartItems((items) => {
      // Дедуп по productId — позволяет иметь в чеке два разных товара с одинаковым именем.
      // (Фикстуры без productId дедупятся по имени как запасной вариант.)
      const key = product.productId;
      const existing = key
        ? items.find((item) => item.productId === key)
        : items.find((item) => item.name === product.name);
      if (existing) {
        return items.map((item) => {
          const match = key ? item.productId === key : item.name === product.name;
          return match ? { ...item, quantity: item.quantity + 1 } : item;
        });
      }

      return [...items, { ...product, quantity: 1 }];
    });
  }, []);

  const onScan = useCallback((code: string) => {
    const found = matchByBarcode(catalog, code);
    if (found) {
      addProduct(found);
      toast.success(t('op.pos.scan.added', { name: found.name }));
    } else {
      toast.info(t('op.pos.scan.unknown'));
    }
  }, [catalog, addProduct, toast, t]);

  useBarcodeScanner(!payOpen, onScan);




  const acceptPayment = async (payments: PaymentPartDto[]) => {
    setPaymentCloseLocked(true);
    setFeedback({ label: t('op.pos.feedback.payment'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.createPosSale) || !hasPermission(nextBackend.session, permissionNames.payPosSale)) {
        throw new Error(t('op.pos.error.noPermissionPayment'));
      }

      if (!shiftId) {
        throw new Error(t('op.pos.error.openShiftFirst'));
      }

      if (cartItems.length === 0 || cartItems.some((item) => !item.productId || item.source !== 'backend')) {
        throw new Error(t('op.pos.error.catalogNotLoaded'));
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const attempt = paymentAttemptRef.current ?? {
        createSaleKey: createIdempotencyKey('pos-sale'),
        settlementKey: createIdempotencyKey('pos-payment'),
        saleId: null,
        settlementRequest: null
      };
      paymentAttemptRef.current = attempt;
      if (attempt.saleId === null) {
        const sale = await clients.pos.createSale(nextBackend.branchId, {
          organizationId: nextBackend.session.organizationId,
          shiftId,
          lines: cartItems.map((item) => ({
            productId: item.productId!,
            quantity: item.quantity,
            unitPrice: {
              currencyCode,
              minorUnits: item.priceMinorUnits
            }
          })),
          idempotencyKey: attempt.createSaleKey,
          playerAccountId: selectedPosPlayerId
        });
        attempt.saleId = readString(sale, 'posSaleId');
        if (!attempt.saleId) {
          throw new Error(t('op.pos.error.receiptNotConfirmed'));
        }
      }

      const settlementRequest = attempt.settlementRequest ?? {
        organizationId: nextBackend.session.organizationId,
        payments: payments.map((part) => ({
          paymentMethod: part.paymentMethod,
          amount: {
            currencyCode: part.amount.currencyCode,
            minorUnits: part.amount.minorUnits
          }
        })),
        note: 'operator POS checkout',
        idempotencyKey: attempt.settlementKey
      };
      attempt.settlementRequest = settlementRequest;
      try {
        await clients.pos.settleSale(attempt.saleId, settlementRequest);
      } catch (error) {
        if (error instanceof PlatformApiError) {
          throw error;
        }

        // A transport failure is ambiguous: the first request may have committed.
        // Replay exactly once with the same sale, payload and idempotency key.
        await clients.pos.settleSale(attempt.saleId, settlementRequest);
      }

      paymentAttemptRef.current = null;
      setPaymentCloseLocked(false);
      setPayOpen(false);
      setFeedback({ label: t('op.pos.feedback.payment'), state: 'confirmed' });
      await loadBackendPos(nextBackend);
      setCartItems([]);
    } catch (error) {
      const attempt = paymentAttemptRef.current;
      let settlementOutcomeResolved = error instanceof PlatformApiError && error.status !== 409;
      if (error instanceof PlatformApiError && error.status === 409 && attempt?.saleId && backend !== null) {
        try {
          const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
          const authoritativeSale = await clients.pos.getSale(attempt.saleId);
          const authoritativeState = readString(authoritativeSale, 'state');
          if (authoritativeState === 'paid') {
            paymentAttemptRef.current = null;
            setPaymentCloseLocked(false);
            setPayOpen(false);
            setFeedback({ label: t('op.pos.feedback.payment'), state: 'confirmed' });
            await loadBackendPos(backend);
            setCartItems([]);
            return;
          }

          if (authoritativeState === 'voided' || authoritativeState === 'refunded') {
            paymentAttemptRef.current = null;
            setPaymentCloseLocked(false);
            setPayOpen(false);
            setFeedback({
              label: t('op.pos.feedback.payment'),
              state: 'failed',
              detail: t('op.pos.error.saleTerminal')
            });
            return;
          }

          if (authoritativeState === 'draft' || authoritativeState === 'pending_payment') {
            await loadBackendPos(backend, { preserveCartSnapshot: true });
            settlementOutcomeResolved = true;
          }
        } catch {
          // Reconciliation is best-effort, but the original settlement failure
          // remains actionable and the unresolved attempt must stay intact.
        }
      }
      if (attempt?.saleId && settlementOutcomeResolved) {
        // The next explicit click may carry corrected payment parts. Reuse the
        // authoritative sale, but use a new key for that new payload gesture.
        attempt.settlementKey = createIdempotencyKey('pos-payment');
        attempt.settlementRequest = null;
      }
      if (settlementOutcomeResolved) {
        setPaymentCloseLocked(false);
      }
      setFeedback({
        label: t('op.pos.feedback.payment'),
        state: 'failed',
        detail: projectSettlementError(error, t)
      });
    }
  };







  // Позиции чека для окна оплаты: товар (× кол-во, если больше одного) → сумма строки.
  const billLines: PaymentBillLine[] = cartItems.map((item) => ({
    label: item.quantity > 1 ? `${item.name} ×${item.quantity}` : item.name,
    amountMinorUnits: item.priceMinorUnits * item.quantity
  }));

  const Root = embedded ? 'section' : 'main';
  return (
    <Root className={embedded ? 'pos-screen pos-embed' : 'workspace-screen pos-screen'}>
      {!embedded && (
        <section className="screen-head pos-head">
          <div>
            <span>{t('op.pos.title')}</span>
            <h1>{t('op.pos.heading')}</h1>
          </div>
          <div className="screen-actions">
            <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, t('op.pos.platformConnected'), t)}</span>
          </div>
        </section>
      )}

      <section className="pos-layout">
        <section className="pos-panel pos-catalog-panel">
          <header className="pos-panel-title pos-panel-title--metrics">
            <div className="pos-panel-headings">
              <span>{t('op.pos.catalog.title')}</span>
              <strong>{t('op.pos.catalog.subtitle')}</strong>
            </div>
            <div className="pos-panel-metrics">
              {lowStockCount > 0 ? (
                <span className="warn">{t('op.pos.strip.stock')} <b>{t('op.pos.strip.stockLow', { count: lowStockCount })}</b></span>
              ) : (
                <span>{t('op.pos.strip.stock')} {t('op.pos.strip.stockOk')}</span>
              )}
              <span className="ui-scanner-badge" aria-label={t('op.pos.scan.active')}>
                <span className="ui-scanner-pulse" aria-hidden="true" />
                {t('op.pos.scan.active')}
              </span>
            </div>
          </header>
          <label className="ui-search-field pos-catalog-search">
            <Search size={14} />
            <input
              placeholder={t('op.pos.catalog.searchPlaceholder')}
              value={productSearch}
              onChange={(event) => setProductSearch(event.currentTarget.value)}
            />
          </label>
          <div className="pos-category-row" aria-label={t('op.pos.catalog.categoryLabel')}>
            {categories.map((category) => (
              <button
                key={category}
                type="button"
                className={`ui-chip ui-chip--filter${activeCategory === category ? ' is-active' : ''}`}
                onClick={() => setActiveCategory(category)}
              >
                {category === CATEGORY_ALL ? categoryAll : category}
              </button>
            ))}
          </div>
          <div className="pos-catalog-grid">
            {visibleProducts.length === 0 ? (
              <div className="pos-empty-state">
                <strong>{t('op.pos.catalog.emptyTitle')}</strong>
                <span>{loadStatus === 'backend' ? t('op.pos.catalog.emptyBackend') : t('op.pos.catalog.emptyLoad')}</span>
              </div>
            ) : (
              visibleProducts.map((product) => (
                <button key={`${product.productId ?? product.name}-${product.name}`} type="button" className="ui-card ui-card--interactive pos-product-card" onClick={() => addProduct(product)}>
                  <strong>{product.name}</strong>
                  <span>{product.category}</span>
                  <b><span>{t('op.pos.catalog.priceLabel')}</span> <Money minorUnits={product.priceMinorUnits} currencyCode={currencyCode} /></b>
                  <em>{product.note}</em>
                </button>
              ))
            )}
          </div>
        </section>

        <section className="pos-panel pos-sale-panel">
          <header className="pos-panel-title">
            <span>{t('op.pos.cart.title')}</span>
            <strong>{shiftId ? t('op.pos.cart.shiftOpen') : t('op.pos.cart.shiftClosed')}</strong>
          </header>
          {/* КТО — клиент одной строкой; поиск разворачивается по «Выбрать» */}
          {selectedPosPlayer ? (
            <div className="pos-client-row">
              <UserRoundPlus size={17} />
              <div>
                <strong>{selectedPosPlayer.name}</strong>
                <em>{selectedPosPlayer.phoneNumber || t('op.pos.cart.clientNoPhone')} · <span>{t('op.pos.cart.balanceLabel')}</span> <Money minorUnits={selectedPosPlayer.balanceMinorUnits} currencyCode={currencyCode} /></em>
              </div>
              <button
                type="button"
                className="pos-client-reset"
                aria-label={t('op.pos.cart.clientResetLabel')}
                onClick={() => {
                  setPlayerSearch('');
                  setSelectedPlayerId('');
                  setPosPlayers([]);
                  setClientPickerOpen(false);
                }}
              >
                <X size={15} />
              </button>
            </div>
          ) : clientPickerOpen ? (
            <div className="pos-client-pick">
              <label className="ui-search-field pos-client-search">
                <Search size={14} />
                <input
                  aria-label={t('op.pos.cart.clientSearchLabel')}
                  autoFocus
                  value={playerSearch}
                  disabled={backend !== null && !hasPermission(backend.session, permissionNames.viewPlayers)}
                  placeholder={t('op.pos.cart.clientSearchPlaceholder')}
                  onChange={(event) => setPlayerSearch(event.currentTarget.value)}
                />
                <button
                  type="button"
                  className="pos-client-reset"
                  aria-label={t('op.pos.cart.clientResetLabel')}
                  onClick={() => {
                    setPlayerSearch('');
                    setPosPlayers([]);
                    setClientPickerOpen(false);
                  }}
                >
                  <X size={15} />
                </button>
              </label>
              {playerSearchQuery.length > 1 && (
                <div className="pos-client-candidates" aria-label={t('op.pos.cart.clientsLabel')}>
                  {posPlayers.map((player) => (
                    <button
                      key={player.playerAccountId ?? player.name}
                      type="button"
                      className={player.playerAccountId === selectedPlayerId ? 'active' : undefined}
                      disabled={!player.playerAccountId || feedback.state === 'pending'}
                      onClick={() => {
                        setSelectedPlayerId(player.playerAccountId ?? '');
                        setClientPickerOpen(false);
                      }}
                    >
                      <strong>{player.name}</strong>
                      <span><Money minorUnits={player.balanceMinorUnits} currencyCode={currencyCode} /> · {t('op.pos.cart.clientDebt', { amount: formatMinorUnits(player.debtMinorUnits, currencyCode) })}</span>
                    </button>
                  ))}
                  {playerLoadStatus === 'loading' && <p>{t('op.pos.cart.clientSearching')}</p>}
                  {playerLoadStatus !== 'loading' && posPlayers.length === 0 && <p>{t('op.pos.cart.clientNotFound')}</p>}
                </div>
              )}
            </div>
          ) : (
            <div className="pos-client-row">
              <UserRoundPlus size={17} />
              <div>
                <strong>{t('op.pos.cart.clientGuest')}</strong>
                <em>{t('op.pos.cart.clientGuestSale')}</em>
              </div>
              <button
                type="button"
                className="pos-client-select"
                disabled={backend !== null && !hasPermission(backend.session, permissionNames.viewPlayers)}
                onClick={() => setClientPickerOpen(true)}
              >
                {t('op.pos.cart.selectClientBtn')}
              </button>
            </div>
          )}

          {backend !== null && selectedPosPlayer?.playerAccountId && hasPermission(backend.session, permissionNames.purchasePackage) && (
            <>
              {packageOptionsLoading && <p>{workspaceLoadStatusLabel('loading', '', t)}</p>}
              {packageOptionsError && <p role="alert">{packageOptionsError}</p>}
              {!packageOptionsLoading && !packageOptionsError && (
                <PackagePurchasePanel
                  backend={backend}
                  player={selectedPosPlayer as PlayerClientItem & { playerAccountId: string }}
                  options={packageOptions}
                  shiftOpen={shiftId.length > 0}
                  onPurchased={refreshPurchasedPackage}
                />
              )}
            </>
          )}

          {/* ЧТО — лента товаров */}
          <div className={cartItems.length === 0 ? 'pos-cart-list is-empty' : 'pos-cart-list'}>
            {cartItems.length === 0 ? (
              <article className="pos-cart-row empty">
                <div>
                  <strong>{t('op.pos.cart.emptyTitle')}</strong>
                  <span>{t('op.pos.cart.emptyHint')}</span>
                </div>
              </article>
            ) : (
              cartItems.map((item) => (
                <article key={`${item.productId ?? item.name}-${item.name}`} className="pos-cart-row interactive-row">
                  <div>
                    <strong>{item.name}</strong>
                    <span>{t('op.pos.cart.itemQty', { count: item.quantity })}</span>
                  </div>
                  <b><Money minorUnits={item.priceMinorUnits * item.quantity} currencyCode={currencyCode} /></b>
                </article>
              ))
            )}
          </div>

          {/* ИТОГ + ОПЛАТА — единый подвал расчёта */}
          <div className="pos-tender">
            <div className="pos-tender-total">
              <span>{t('op.pos.cart.total')}</span>
              <strong><Money minorUnits={cartTotalMinorUnits} currencyCode={currencyCode} /></strong>
            </div>
            <button type="button" className="ui-btn ui-btn--primary ui-btn--lg pos-primary-action" disabled={!canAcceptPayment || feedback.state === 'pending'} onClick={() => {
              paymentAttemptRef.current = {
                createSaleKey: createIdempotencyKey('pos-sale'),
                settlementKey: createIdempotencyKey('pos-payment'),
                saleId: null,
                settlementRequest: null
              };
              setPayOpen(true);
            }}>{t('op.pos.payment.acceptBtn')}</button>
            <button type="button" className="ui-btn pos-secondary-action" onClick={() => setCartItems([])}>{t('op.pos.payment.clearCartBtn')}</button>
          </div>
        </section>
      </section>

      {payOpen && (
        <PanelModal
          title={t('op.pos.checkout.title')}
          subtitle={selectedPosPlayer ? selectedPosPlayer.name : t('op.pos.cart.clientGuest')}
          closeDisabled={feedback.state === 'pending' || paymentCloseLocked}
          onClose={() => {
            if (feedback.state === 'pending' || paymentCloseLocked) {
              return;
            }
            paymentAttemptRef.current = null;
            setPayOpen(false);
          }}
        >
          <PaymentDialog
            lines={billLines}
            dueLabel={t('op.pos.cart.total')}
            grandTotalMinorUnits={cartTotalMinorUnits}
            currencyCode={currencyCode}
            walletBalanceMinorUnits={selectedPosPlayer?.balanceMinorUnits ?? null}
            allowSplit={selectedPosPlayer !== null}
            disabled={feedback.state === 'pending'}
            draftDisabled={paymentCloseLocked}
            cancelDisabled={paymentCloseLocked}
            confirmVariant="accent"
            onCancel={() => {
              if (feedback.state === 'pending' || paymentCloseLocked) {
                return;
              }
              paymentAttemptRef.current = null;
              setPayOpen(false);
            }}
            onConfirm={acceptPayment}
          />
        </PanelModal>
      )}
    </Root>
  );
}
