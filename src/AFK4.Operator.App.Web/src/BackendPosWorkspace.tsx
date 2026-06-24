import { useEffect, useState } from 'react';
import { AlertTriangle, ArrowRightLeft, Banknote, CircleDollarSign, ReceiptText, Search, UserRoundPlus, X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import type {
  PlayerSearchResultDto,
  PosProductDto,
  PosSaleDto,
  ReceiptDto,
  ReportResultDto,
  ShiftDto
} from './operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from './operatorTypes';
import { hasPermission, permissionNames } from './operatorPermissions';
import {
  buildPosReceiptText,
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  downloadTextFile,
  emptyFeedback,
  escapeHtml,
  formatMinorUnits,
  formatMoney,
  formatTime,
  posSaleLineSummary,
  posSaleStateLabel,
  posReceiptTypeLabel,
  projectPlayerClient,
  readArray,
  readMoney,
  readNumber,
  readRecord,
  readString,
  requireBackend,
  safeReceiptFileName,
  shiftStateLabel,
  triggerFeedback,
  type PlayerClientItem,
  workspaceLoadStatusLabel
} from './operatorHelpers';
import { CriticalActionConfirmation, FeedbackNotice, StateFlag } from './operatorPrimitives';

type PosCatalogItem = {
  productId?: string;
  name: string;
  priceMinorUnits: number;
  category: string;
  note: string;
  stockOnHand: number;
  source: 'fixture' | 'backend';
};

type PosCartItem = PosCatalogItem & {
  quantity: number;
};

// Sentinel for the "All" category — never shown as a backend category name
const CATEGORY_ALL = '__all__';

type PaymentMethodKey = 'cash' | 'card' | 'deposit';

function makeFixtureProducts(t: ReturnType<typeof useI18n>['t']): PosCatalogItem[] {
  return [
    { name: t('op.pos.fixture.cola'), priceMinorUnits: 1200, category: t('op.pos.fixture.drinks'), note: t('op.pos.fixture.note'), stockOnHand: 0, source: 'fixture' },
    { name: t('op.pos.fixture.water'), priceMinorUnits: 600, category: t('op.pos.fixture.drinks'), note: t('op.pos.fixture.note'), stockOnHand: 0, source: 'fixture' },
    { name: t('op.pos.fixture.hotdog'), priceMinorUnits: 2800, category: t('op.pos.fixture.food'), note: t('op.pos.fixture.note'), stockOnHand: 0, source: 'fixture' },
    { name: t('op.pos.fixture.guestHour'), priceMinorUnits: 2500, category: t('op.pos.fixture.services'), note: t('op.pos.fixture.note'), stockOnHand: 0, source: 'fixture' }
  ];
}

function projectPosProduct(product: PosProductDto, t: ReturnType<typeof useI18n>['t']): PosCatalogItem {
  const price = readMoney(product, 'price');
  const sku = readString(product, 'sku', 'SKU');
  const stockOnHand = readNumber(product, 'stockOnHand', 0);
  return {
    productId: readString(product, 'productId') || undefined,
    name: readString(product, 'name', t('op.pos.catalog.productFallback')),
    priceMinorUnits: price?.minorUnits ?? 0,
    category: readString(product, 'categoryName', readString(product, 'categoryId', t('op.pos.catalog.categoryFallback'))),
    note: t('op.pos.catalog.note', { sku, count: stockOnHand }),
    stockOnHand,
    // projectPosProduct обрабатывает только реальные бэкенд-продукты (фикстуры идут через
    // makeFixtureProducts с source:'fixture'), поэтому источник всегда 'backend'.
    source: 'backend'
  };
}

export function BackendPosWorkspace({ currencyCode, backend, embedded = false }: { currencyCode: string; backend: OperatorBackendContext | null; embedded?: boolean }) {
  const { t } = useI18n();

  const [activeCategory, setActiveCategory] = useState(CATEGORY_ALL);
  const [productSearch, setProductSearch] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<PaymentMethodKey>('cash');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null);
  const [catalog, setCatalog] = useState<PosCatalogItem[]>(() => backend === null ? makeFixtureProducts(t) : []);
  const [salesReport, setSalesReport] = useState<ReportResultDto | null>(null);
  const [lastSale, setLastSale] = useState<PosSaleDto | null>(null);
  const [selectedSaleDetail, setSelectedSaleDetail] = useState<PosSaleDto | null>(null);
  const [selectedReceiptDetail, setSelectedReceiptDetail] = useState<ReceiptDto | null>(null);
  const [selectedRefundSaleId, setSelectedRefundSaleId] = useState('');
  const [playerSearch, setPlayerSearch] = useState('');
  const [posPlayers, setPosPlayers] = useState<PlayerClientItem[]>([]);
  const [selectedPlayerId, setSelectedPlayerId] = useState('');
  const [playerLoadStatus, setPlayerLoadStatus] = useState<LoadStatus>('fixture');
  const [newPlayerName, setNewPlayerName] = useState('');
  const [newPlayerPhone, setNewPlayerPhone] = useState('');
  const [stockWriteOffProductId, setStockWriteOffProductId] = useState('');
  const [stockWriteOffQuantity, setStockWriteOffQuantity] = useState('1');
  const [stockWriteOffReason, setStockWriteOffReason] = useState(() => t('op.pos.defaultWriteOffReason'));
  const [criticalAction, setCriticalAction] = useState<'refund-sale' | 'void-draft' | null>(null);
  const [refundReason, setRefundReason] = useState(() => t('op.pos.defaultRefundReason'));
  const [voidReason, setVoidReason] = useState(() => t('op.pos.defaultVoidReason'));
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

  const loadBackendPos = async (nextBackend = backend) => {
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
      const [nextCatalog, nextShift, nextSalesReport] = await Promise.all([
        clients.pos.getCatalog(nextBackend.branchId),
        clients.shifts.getCurrentShift(nextBackend.branchId),
        clients.shifts.getSalesReport(nextBackend.branchId, { limit: 8 })
      ]);

      const products = Array.isArray(nextCatalog)
        ? nextCatalog.map((product) => projectPosProduct(product, t))
        : [];

      const backendProducts = products.filter((product) => product.source === 'backend' && product.productId);
      setCatalog(products);
      setStockWriteOffProductId((current) => backendProducts.some((product) => product.productId === current)
        ? current
        : backendProducts[0]?.productId ?? '');
      setCurrentShift(nextShift);
      setSalesReport(nextSalesReport);
      const nextSalesRows = readArray(nextSalesReport, 'rows');
      setSelectedRefundSaleId((current) => nextSalesRows.some((row) => readString(row, 'posSaleId') === current)
        ? current
        : readString(nextSalesRows.find((row) => readString(row, 'state').toLowerCase() === 'paid'), 'posSaleId'));
      setCartItems((items) => {
        const productById = new Map(backendProducts.map((product) => [product.productId, product]));
        const validBackendItems = items
          .filter((item) => item.source === 'backend' && item.productId && productById.has(item.productId))
          .map((item) => ({
            ...productById.get(item.productId!)!,
            quantity: item.quantity
          }));
        if (validBackendItems.length > 0) {
          return validBackendItems;
        }

        return backendProducts[0] ? [{ ...backendProducts[0], quantity: 1 }] : [];
      });
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
        setSelectedPlayerId((current) => current && projected.some((player) => player.playerAccountId === current)
          ? current
          : projected[0]?.playerAccountId ?? '');
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
  const categories = [CATEGORY_ALL, ...Array.from(new Set(catalog.map((product) => product.category))).slice(0, 5)];
  const visibleProducts = catalog.filter((product) => {
    const categoryMatches = activeCategory === CATEGORY_ALL || product.category === activeCategory;
    const searchMatches = `${product.name} ${product.category} ${product.note}`.toLowerCase().includes(productSearch.trim().toLowerCase());
    return categoryMatches && searchMatches;
  });
  const selectedPosPlayer = posPlayers.find((player) => player.playerAccountId === selectedPlayerId) ?? null;
  const selectedPosPlayerId = selectedPosPlayer?.playerAccountId ?? null;
  const playerSearchQuery = playerSearch.trim();
  const paymentMethodName = paymentMethod === 'card' ? 'card_manual' : 'cash';
  const newPlayerDisplayName = newPlayerName.trim() || playerSearchQuery;
  const cartTotalMinorUnits = cartItems.reduce((sum, item) => sum + item.priceMinorUnits * item.quantity, 0);
  const acceptedCashMinorUnits = paymentMethod === 'cash'
    ? Math.ceil(cartTotalMinorUnits / 1000) * 1000
    : cartTotalMinorUnits;
  const changeMinorUnits = acceptedCashMinorUnits - cartTotalMinorUnits;
  const salesRows = readArray(salesReport, 'rows');
  const grossSales = readMoney(salesReport, 'grossSalesTotal');
  const refundsTotal = readMoney(salesReport, 'refundsTotal');
  const lowStockCount = catalog.filter((product) => product.source === 'backend' && product.stockOnHand <= 2).length;
  const shiftId = readString(currentShift, 'shiftId');
  const shiftState = readString(currentShift, 'state', currentShift === null ? 'нет смены' : 'unknown');
  const reportRefundableSales = salesRows.filter((row) => readString(row, 'state').toLowerCase() === 'paid');
  const selectedRefundableSale = reportRefundableSales.find((row) => readString(row, 'posSaleId') === selectedRefundSaleId)
    ?? reportRefundableSales[0]
    ?? salesRows[0]
    ?? lastSale;
  const selectedRefundableSaleId = readString(selectedRefundableSale, 'posSaleId');
  const canRefundSelectedSale = backend !== null
    && selectedRefundableSaleId.length > 0
    && hasPermission(backend.session, permissionNames.refundPosSale);
  const canViewSaleDetails = backend !== null && hasPermission(backend.session, permissionNames.viewReceipt);
  const canVoidDraftCart = backend !== null
    && shiftId.length > 0
    && cartItems.length > 0
    && cartItems.every((item) => Boolean(item.productId) && item.source === 'backend')
    && hasPermission(backend.session, permissionNames.createPosSale)
    && hasPermission(backend.session, permissionNames.voidPosSale);
  const canAcceptPayment = backend !== null
    && shiftId.length > 0
    && cartItems.length > 0
    && cartItems.every((item) => Boolean(item.productId) && item.source === 'backend')
    && hasPermission(backend.session, permissionNames.createPosSale)
    && hasPermission(backend.session, permissionNames.payPosSale);
  const canCreatePosPlayer = backend !== null
    && hasPermission(backend.session, permissionNames.createPlayerAccount)
    && newPlayerDisplayName.length > 0;
  const canTopUpPosWallet = backend !== null
    && selectedPosPlayerId !== null
    && cartTotalMinorUnits > 0
    && hasPermission(backend.session, permissionNames.topUpWallet);
  const backendCatalogProducts = catalog.filter((product) => product.source === 'backend' && product.productId);
  const selectedStockProduct = backendCatalogProducts.find((product) => product.productId === stockWriteOffProductId)
    ?? backendCatalogProducts[0]
    ?? null;
  const canWriteOffStock = backend !== null
    && selectedStockProduct !== null
    && hasPermission(backend.session, permissionNames.manageInventoryStock);

  const addProduct = (product: PosCatalogItem) => {
    setCartItems((items) => {
      const existing = items.find((item) => item.name === product.name);
      if (existing) {
        return items.map((item) => item.name === product.name ? { ...item, quantity: item.quantity + 1 } : item);
      }

      return [...items, { ...product, quantity: 1 }];
    });
    triggerFeedback(setFeedback, t('op.pos.cart.productAdded', { name: product.name }), 'confirmed');
  };

  const createPosPlayer = async () => {
    setFeedback({ label: t('op.pos.feedback.newCard'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
        throw new Error(t('op.pos.error.noPermissionNewCard'));
      }

      const displayName = newPlayerDisplayName;
      if (!displayName) {
        throw new Error(t('op.pos.error.enterClientName'));
      }

      const player = await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).players.createPlayer(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        displayName,
        phoneNumber: newPlayerPhone.trim() || null,
        idempotencyKey: createIdempotencyKey('player-create')
      });
      const projected = projectPlayerClient(player, t);
      setPosPlayers((players) => [
        projected,
        ...players.filter((candidate) => candidate.playerAccountId !== projected.playerAccountId)
      ]);
      setSelectedPlayerId(projected.playerAccountId ?? '');
      setNewPlayerName('');
      setNewPlayerPhone('');
      setFeedback({ label: t('op.pos.feedback.newCard'), state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: t('op.pos.feedback.newCard'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  const writeOffStock = async () => {
    setFeedback({ label: t('op.pos.feedback.stockWriteOff'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.manageInventoryStock)) {
        throw new Error(t('op.pos.error.noPermissionStock'));
      }

      const productId = selectedStockProduct?.productId;
      const quantity = Number(stockWriteOffQuantity);
      const reason = stockWriteOffReason.trim();
      if (!productId || !Number.isInteger(quantity) || quantity <= 0 || !reason) {
        throw new Error(t('op.pos.error.invalidStockInput'));
      }

      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).inventory.createStockMovement(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        productId,
        movementType: 'adjustment',
        quantityDelta: -quantity,
        unitCost: { currencyCode, minorUnits: 0 },
        reason,
        idempotencyKey: createIdempotencyKey('stock-write-off')
      });

      setFeedback({ label: t('op.pos.feedback.stockWriteOff'), state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: t('op.pos.feedback.stockWriteOff'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  const topUpSelectedPosPlayer = async () => {
    setFeedback({ label: t('op.pos.feedback.topUp'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.topUpWallet)) {
        throw new Error(t('op.pos.error.noPermissionTopUp'));
      }

      if (!selectedPosPlayerId) {
        throw new Error(t('op.pos.error.selectClientForTopUp'));
      }

      if (cartTotalMinorUnits <= 0) {
        throw new Error(t('op.pos.error.addAmountForTopUp'));
      }

      const wallet = await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).players.topUpWallet(selectedPosPlayerId, {
        organizationId: nextBackend.session.organizationId,
        amount: { currencyCode, minorUnits: cartTotalMinorUnits },
        reason: 'operator POS wallet top-up',
        idempotencyKey: createIdempotencyKey('wallet-top-up')
      });
      const walletBalance = readMoney(wallet, 'walletBalance')?.minorUnits;
      if (walletBalance !== undefined) {
        setPosPlayers((players) => players.map((player) => player.playerAccountId === selectedPosPlayerId
          ? { ...player, balanceMinorUnits: walletBalance }
          : player));
      }
      setFeedback({
        label: t('op.pos.feedback.topUp'),
        state: 'confirmed',
        detail: formatMinorUnits(cartTotalMinorUnits, currencyCode)
      });
    } catch (error) {
      setFeedback({
        label: t('op.pos.feedback.topUp'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  const acceptPayment = async () => {
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
        idempotencyKey: createIdempotencyKey('pos-sale'),
        playerAccountId: selectedPosPlayerId
      });
      const saleId = readString(sale, 'posSaleId');
      if (!saleId) {
        throw new Error(t('op.pos.error.receiptNotConfirmed'));
      }

      const paidSale = await clients.pos.paySaleManual(saleId, {
        organizationId: nextBackend.session.organizationId,
        paymentMethod: paymentMethodName,
        amount: {
          currencyCode,
          minorUnits: cartTotalMinorUnits
        },
        note: 'operator POS checkout',
        idempotencyKey: createIdempotencyKey('pos-payment')
      });

      setLastSale(paidSale);
      setCartItems([]);
      setFeedback({ label: t('op.pos.feedback.payment'), state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: t('op.pos.feedback.payment'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  const refundLatestSale = async () => {
    setCriticalAction(null);
    setFeedback({ label: t('op.pos.feedback.refund'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.refundPosSale)) {
        throw new Error(t('op.pos.error.noPermissionRefund'));
      }

      if (!selectedRefundableSaleId) {
        throw new Error(t('op.pos.error.selectReceiptForRefund'));
      }

      const reason = refundReason.trim();
      if (!reason) {
        throw new Error(t('op.pos.error.enterRefundReason'));
      }

      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).pos.refundSale(selectedRefundableSaleId, {
        organizationId: nextBackend.session.organizationId,
        reason,
        idempotencyKey: createIdempotencyKey('pos-refund')
      });
      setFeedback({ label: t('op.pos.feedback.refund'), state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: t('op.pos.feedback.refund'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  const loadSaleDetail = async (saleId: string) => {
    setSelectedReceiptDetail(null);
    setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.viewReceipt)) {
        throw new Error(t('op.pos.error.noPermissionViewReceipts'));
      }

      if (!saleId) {
        throw new Error(t('op.pos.error.selectReceiptFromList'));
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const sale = await clients.pos.getSale(saleId);
      const latestReceipt = readRecord(sale, 'latestReceipt');
      const receiptId = readString(latestReceipt, 'receiptId');
      const receipt = receiptId ? await clients.pos.getReceipt(receiptId) : null;
      setSelectedSaleDetail(sale);
      setSelectedReceiptDetail(receipt);
      setFeedback({ label: t('op.pos.feedback.receiptDetails'), state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: t('op.pos.feedback.receiptDetails'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  const selectedReceiptRecord = selectedReceiptDetail ?? readRecord(selectedSaleDetail, 'latestReceipt');

  const printSelectedReceipt = () => {
    setFeedback({ label: t('op.pos.feedback.print'), state: 'pending' });
    try {
      if (selectedSaleDetail === null) {
        throw new Error(t('op.pos.error.openReceiptFirst'));
      }

      const receiptText = buildPosReceiptText(selectedSaleDetail, selectedReceiptRecord, currencyCode, t);
      const printWindow = window.open('', '_blank', 'width=360,height=640');
      if (printWindow === null) {
        throw new Error(t('op.pos.error.printWindowFailed'));
      }

      printWindow.document.write(`<pre style="font: 13px/1.45 monospace; white-space: pre-wrap;">${escapeHtml(receiptText)}</pre>`);
      printWindow.document.close();
      printWindow.focus();
      printWindow.print();
      setFeedback({ label: t('op.pos.feedback.print'), state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: t('op.pos.feedback.print'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  const exportSelectedReceipt = () => {
    setFeedback({ label: t('op.pos.feedback.export'), state: 'pending' });
    try {
      if (selectedSaleDetail === null) {
        throw new Error(t('op.pos.error.openReceiptFirstExport'));
      }

      const receiptText = buildPosReceiptText(selectedSaleDetail, selectedReceiptRecord, currencyCode, t);
      const receiptNumber = readString(selectedReceiptRecord, 'receiptNumber', 'receipt');
      downloadTextFile(`${safeReceiptFileName(receiptNumber)}.txt`, receiptText);
      setFeedback({ label: t('op.pos.feedback.export'), state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: t('op.pos.feedback.export'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  const voidDraftCart = async () => {
    setCriticalAction(null);
    setFeedback({ label: t('op.pos.feedback.void'), state: 'pending' });
    try {
      const nextBackend = requireBackend(backend, t);
      if (!hasPermission(nextBackend.session, permissionNames.createPosSale) || !hasPermission(nextBackend.session, permissionNames.voidPosSale)) {
        throw new Error(t('op.pos.error.noPermissionVoid'));
      }

      if (!shiftId) {
        throw new Error(t('op.pos.error.shiftRequiredForVoid'));
      }

      if (cartItems.length === 0 || cartItems.some((item) => !item.productId || item.source !== 'backend')) {
        throw new Error(t('op.pos.error.catalogNotLoaded'));
      }

      const reason = voidReason.trim();
      if (!reason) {
        throw new Error(t('op.pos.error.enterVoidReason'));
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const draft = await clients.pos.createSale(nextBackend.branchId, {
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
        idempotencyKey: createIdempotencyKey('pos-sale-draft'),
        playerAccountId: selectedPosPlayerId
      });
      const saleId = readString(draft, 'posSaleId');
      if (!saleId) {
        throw new Error(t('op.pos.error.draftNotConfirmed'));
      }

      const voidedSale = await clients.pos.voidSale(saleId, {
        organizationId: nextBackend.session.organizationId,
        reason,
        idempotencyKey: createIdempotencyKey('pos-void')
      });

      setLastSale(voidedSale);
      setCartItems([]);
      setFeedback({ label: t('op.pos.feedback.void'), state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: t('op.pos.feedback.void'),
        state: 'failed',
        detail: projectOperatorError(error, t).detail
      });
    }
  };

  const paymentMethods: { key: PaymentMethodKey; label: string; disabled: boolean; title?: string }[] = [
    { key: 'cash', label: t('op.pos.payment.methodCash'), disabled: false },
    { key: 'card', label: t('op.pos.payment.methodCard'), disabled: false },
    {
      key: 'deposit',
      label: t('op.pos.payment.methodDeposit'),
      disabled: true,
      title: t('op.pos.payment.depositDisabledTitle')
    }
  ];

  type QuickAction = {
    id: 'topUp' | 'refund' | 'void' | 'stockWriteOff' | 'newClient' | 'cashIn';
    label: string;
    detail: string;
    Icon: React.ComponentType<{ size: number }>;
    disabled: boolean;
  };

  const quickActions: QuickAction[] = [
    {
      id: 'topUp',
      label: t('op.pos.quick.topUpLabel'),
      detail: selectedPosPlayer
        ? t('op.pos.quick.topUpCartDetail', { amount: formatMinorUnits(cartTotalMinorUnits, currencyCode) })
        : t('op.pos.quick.topUpSelectClient'),
      Icon: CircleDollarSign,
      disabled: !canTopUpPosWallet || feedback.state === 'pending'
    },
    {
      id: 'refund',
      label: t('op.pos.quick.refundLabel'),
      detail: t('op.pos.quick.refundDetail'),
      Icon: ReceiptText,
      disabled: !canRefundSelectedSale || feedback.state === 'pending'
    },
    {
      id: 'void',
      label: t('op.pos.quick.voidLabel'),
      detail: t('op.pos.quick.voidDetail'),
      Icon: X,
      disabled: !canVoidDraftCart || feedback.state === 'pending'
    },
    {
      id: 'stockWriteOff',
      label: t('op.pos.quick.stockWriteOffLabel'),
      detail: selectedStockProduct?.name ?? t('op.pos.quick.stockWriteOffSelectProduct'),
      Icon: AlertTriangle,
      disabled: !canWriteOffStock || feedback.state === 'pending'
    },
    {
      id: 'newClient',
      label: t('op.pos.quick.newClientLabel'),
      detail: newPlayerDisplayName || t('op.pos.quick.newClientFillName'),
      Icon: UserRoundPlus,
      disabled: !canCreatePosPlayer || feedback.state === 'pending'
    },
    {
      id: 'cashIn',
      label: t('op.pos.quick.cashInLabel'),
      detail: t('op.pos.quick.cashInDetail'),
      Icon: Banknote,
      disabled: false
    }
  ];

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

      <section className="state-strip pos-state-strip" aria-label={t('op.pos.strip.salesSummaryLabel')}>
        <StateFlag label={t('op.pos.strip.sales')} value={`${salesRows.length} · ${formatMoney(grossSales, currencyCode)}`} />
        <StateFlag label={t('op.pos.strip.refunds')} value={formatMoney(refundsTotal, currencyCode)} critical={(refundsTotal?.minorUnits ?? 0) > 0} />
        <StateFlag label={t('op.pos.strip.products')} value={t('op.pos.strip.positions', { count: catalog.length })} />
        <StateFlag label={t('op.pos.strip.stock')} value={t('op.pos.strip.stockLow', { count: lowStockCount })} critical={lowStockCount > 0} />
        <StateFlag label={t('op.pos.strip.shift')} value={shiftStateLabel(shiftState, t)} critical={!shiftId} />
      </section>

      <section className="pos-layout">
        <section className="pos-panel pos-catalog-panel">
          <header className="pos-panel-title">
            <span>{t('op.pos.catalog.title')}</span>
            <strong>{t('op.pos.catalog.subtitle')}</strong>
          </header>
          <label className="pos-search">
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
                className={activeCategory === category ? 'active' : undefined}
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
                <button key={`${product.productId ?? product.name}-${product.name}`} type="button" className="pos-product-card" onClick={() => addProduct(product)}>
                  <strong>{product.name}</strong>
                  <span>{product.category}</span>
                  <b>{formatMinorUnits(product.priceMinorUnits, currencyCode)}</b>
                  <em>{product.note}</em>
                </button>
              ))
            )}
          </div>
        </section>

        <section className="pos-panel pos-cart-panel">
          <header className="pos-panel-title">
            <span>{t('op.pos.cart.title')}</span>
            <strong>{shiftId ? t('op.pos.cart.shiftOpen') : t('op.pos.cart.shiftClosed')}</strong>
          </header>
          <div className="pos-cart-client">
            <UserRoundPlus size={17} />
            <div>
              <span>{t('op.pos.cart.clientLabel')}</span>
              <strong>{selectedPosPlayer ? selectedPosPlayer.name : t('op.pos.cart.clientGuest')}</strong>
              <em>{selectedPosPlayer
                ? `${selectedPosPlayer.phoneNumber || t('op.pos.cart.clientNoPhone')} · ${formatMinorUnits(selectedPosPlayer.balanceMinorUnits, currencyCode)}`
                : t('op.pos.cart.clientGuestSale')}</em>
            </div>
            <button
              type="button"
              onClick={() => {
                setPlayerSearch('');
                setSelectedPlayerId('');
                setPosPlayers([]);
              }}
            >
              {t('op.pos.cart.guestBtn')}
            </button>
          </div>
          <label className="pos-search pos-client-search">
            <Search size={14} />
            <input
              aria-label={t('op.pos.cart.clientSearchLabel')}
              value={playerSearch}
              disabled={backend !== null && !hasPermission(backend.session, permissionNames.viewPlayers)}
              placeholder={t('op.pos.cart.clientSearchPlaceholder')}
              onChange={(event) => setPlayerSearch(event.currentTarget.value)}
            />
          </label>
          {playerSearchQuery.length > 1 && (
            <div className="pos-client-candidates" aria-label={t('op.pos.cart.clientsLabel')}>
              {posPlayers.map((player) => (
                <button
                  key={player.playerAccountId ?? player.name}
                  type="button"
                  className={player.playerAccountId === selectedPlayerId ? 'active' : undefined}
                  disabled={!player.playerAccountId || feedback.state === 'pending'}
                  onClick={() => setSelectedPlayerId(player.playerAccountId ?? '')}
                >
                  <strong>{player.name}</strong>
                  <span>{formatMinorUnits(player.balanceMinorUnits, currencyCode)} · {t('op.pos.cart.clientDebt', { amount: formatMinorUnits(player.debtMinorUnits, currencyCode) })}</span>
                </button>
              ))}
              {playerLoadStatus === 'loading' && <p>{t('op.pos.cart.clientSearching')}</p>}
              {playerLoadStatus !== 'loading' && posPlayers.length === 0 && <p>{t('op.pos.cart.clientNotFound')}</p>}
            </div>
          )}
          <div className="pos-new-client-form">
            <label>
              <span>{t('op.pos.cart.newCardLabel')}</span>
              <input
                aria-label={t('op.pos.cart.newCardNameLabel')}
                value={newPlayerName}
                disabled={backend !== null && !hasPermission(backend.session, permissionNames.createPlayerAccount)}
                placeholder={playerSearchQuery || t('op.pos.cart.newCardNamePlaceholder')}
                onChange={(event) => setNewPlayerName(event.currentTarget.value)}
              />
            </label>
            <label>
              <span>{t('op.pos.cart.newCardPhoneLabel')}</span>
              <input
                aria-label={t('op.pos.cart.newCardPhoneAriaLabel')}
                value={newPlayerPhone}
                disabled={backend !== null && !hasPermission(backend.session, permissionNames.createPlayerAccount)}
                placeholder="+992..."
                onChange={(event) => setNewPlayerPhone(event.currentTarget.value)}
              />
            </label>
            <button type="button" disabled={!canCreatePosPlayer || feedback.state === 'pending'} onClick={createPosPlayer}>
              <UserRoundPlus size={14} />
              {t('op.pos.cart.createBtn')}
            </button>
          </div>
          <div className="pos-cart-list">
            {cartItems.length === 0 ? (
              <article className="pos-cart-row empty">
                <div>
                  <strong>{t('op.pos.cart.emptyTitle')}</strong>
                  <span>{t('op.pos.cart.emptyHint')}</span>
                </div>
                <b>{formatMinorUnits(0, currencyCode)}</b>
              </article>
            ) : (
              cartItems.map((item) => (
                <article key={`${item.productId ?? item.name}-${item.name}`} className="pos-cart-row interactive-row">
                  <div>
                    <strong>{item.name}</strong>
                    <span>{t('op.pos.cart.itemQty', { count: item.quantity })}</span>
                  </div>
                  <b>{formatMinorUnits(item.priceMinorUnits * item.quantity, currencyCode)}</b>
                </article>
              ))
            )}
          </div>
          <div className="pos-total-card">
            <span>{t('op.pos.cart.total')}</span>
            <strong>{formatMinorUnits(cartTotalMinorUnits, currencyCode)}</strong>
            <em>{lastSale ? t('op.pos.cart.lastSaleAccepted') : t('op.pos.cart.pendingReceipt')}</em>
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="pos-panel pos-payment-panel">
          <header className="pos-panel-title">
            <span>{t('op.pos.payment.title')}</span>
            <strong>{t('op.pos.payment.subtitle')}</strong>
          </header>
          <div className="pos-payment-methods">
            {paymentMethods.map(({ key, label, disabled, title }) => (
              <button
                key={key}
                type="button"
                className={paymentMethod === key ? 'active' : undefined}
                disabled={disabled || feedback.state === 'pending'}
                title={title}
                onClick={() => setPaymentMethod(key)}
              >
                {key === 'cash' && <Banknote size={15} />}
                {key === 'card' && <CircleDollarSign size={15} />}
                {key === 'deposit' && <ReceiptText size={15} />}
                {label}
              </button>
            ))}
          </div>
          <div className="pos-payment-summary">
            <div><span>{t('op.pos.payment.accepted')}</span><strong>{formatMinorUnits(acceptedCashMinorUnits, currencyCode)}</strong></div>
            <div><span>{t('op.pos.payment.change')}</span><strong>{formatMinorUnits(changeMinorUnits, currencyCode)}</strong></div>
            <div><span>{t('op.pos.payment.shiftLabel')}</span><strong>{shiftId ? t('op.pos.payment.shiftOpen') : t('op.pos.payment.shiftNone')}</strong></div>
          </div>
          <button type="button" className="pos-primary-action" disabled={!canAcceptPayment || feedback.state === 'pending'} onClick={acceptPayment}>{t('op.pos.payment.acceptBtn')}</button>
          <button type="button" className="pos-secondary-action" onClick={() => setCartItems([])}>{t('op.pos.payment.clearCartBtn')}</button>
        </section>

        <section className="pos-panel pos-receipts-panel">
          <header className="pos-panel-title">
            <span>{t('op.pos.receipts.title')}</span>
            <strong>{t('op.pos.receipts.subtitle')}</strong>
          </header>
          <div className="pos-receipt-list">
            {salesRows.slice(0, 4).map((row) => (
              <button
                key={readString(row, 'posSaleId')}
                type="button"
                className={`pos-receipt-row ${readString(row, 'posSaleId') === selectedRefundableSaleId ? 'selected' : ''}`}
                disabled={!canViewSaleDetails || feedback.state === 'pending'}
                onClick={() => {
                  const saleId = readString(row, 'posSaleId');
                  setSelectedRefundSaleId(saleId);
                  void loadSaleDetail(saleId);
                }}
              >
                <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                <strong>{posSaleStateLabel(readString(row, 'state', 'sale'), t)}</strong>
                <em>{posSaleLineSummary(row, t)}</em>
                <b>{formatMoney(readMoney(row, 'total'), currencyCode)}</b>
              </button>
            ))}
            {salesRows.length === 0 && (
              <article className="pos-receipt-row">
                <span>—</span>
                <strong>{t('op.pos.receipts.emptyLabel')}</strong>
                <em>{t('op.pos.receipts.emptyPlatform')}</em>
                <b>{formatMoney(null, currencyCode)}</b>
              </article>
            )}
          </div>
          {selectedSaleDetail !== null && (
            <div className="pos-sale-detail">
              <div>
                <span>{t('op.pos.receipts.detailsTitle')}</span>
                <strong>{posSaleStateLabel(readString(selectedSaleDetail, 'state', 'sale'), t)}</strong>
                <b>{formatMoney(readMoney(selectedSaleDetail, 'total'), currencyCode)}</b>
              </div>
              {readArray(selectedSaleDetail, 'lines').slice(0, 3).map((line) => (
                <p key={`${readString(line, 'productId')}-${readNumber(line, 'quantity', 0)}`}>
                  {readString(line, 'productName', t('op.pos.receipts.productFallback'))} · {readNumber(line, 'quantity', 0)} × {formatMoney(readMoney(line, 'unitPrice'), currencyCode)}
                </p>
              ))}
              {selectedReceiptDetail !== null && (
                <div className="pos-receipt-detail">
                  <span>{t('op.pos.receipts.platformReceipt')}</span>
                  <strong>{readString(selectedReceiptDetail, 'receiptNumber', t('op.pos.receipts.receiptFallback'))}</strong>
                  <b>{formatMoney(readMoney(selectedReceiptDetail, 'total'), currencyCode)}</b>
                  <p>{posReceiptTypeLabel(readString(selectedReceiptDetail, 'receiptType', 'sale'), t)}</p>
                </div>
              )}
              <div className="pos-receipt-actions">
                <button type="button" disabled={feedback.state === 'pending'} onClick={printSelectedReceipt}>
                  <ReceiptText size={13} />
                  {t('op.pos.receipts.printBtn')}
                </button>
                <button type="button" disabled={feedback.state === 'pending'} onClick={exportSelectedReceipt}>
                  <ArrowRightLeft size={13} />
                  {t('op.pos.receipts.exportBtn')}
                </button>
              </div>
            </div>
          )}
        </section>

        <section className="pos-panel pos-quick-panel">
          <header className="pos-panel-title">
            <span>{t('op.pos.quick.title')}</span>
            <strong>{t('op.pos.quick.subtitle')}</strong>
          </header>
          <div className="pos-stock-form">
            <label>
              <span>{t('op.pos.quick.writeOffLabel')}</span>
              <select
                aria-label={t('op.pos.quick.writeOffProductLabel')}
                value={selectedStockProduct?.productId ?? ''}
                disabled={backendCatalogProducts.length === 0 || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffProductId(event.currentTarget.value)}
              >
                {backendCatalogProducts.length === 0 && <option value="">{t('op.pos.quick.writeOffNoProduct')}</option>}
                {backendCatalogProducts.map((product) => (
                  <option key={product.productId} value={product.productId}>
                    {t('op.pos.quick.writeOffProductItem', { name: product.name, count: product.stockOnHand })}
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>{t('op.pos.quick.writeOffQtyLabel')}</span>
              <input
                aria-label={t('op.pos.quick.writeOffQtyAriaLabel')}
                inputMode="numeric"
                value={stockWriteOffQuantity}
                disabled={!canWriteOffStock || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffQuantity(event.currentTarget.value)}
              />
            </label>
            <label className="pos-stock-reason">
              <span>{t('op.pos.quick.writeOffReasonLabel')}</span>
              <input
                aria-label={t('op.pos.quick.writeOffReasonAriaLabel')}
                value={stockWriteOffReason}
                disabled={!canWriteOffStock || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffReason(event.currentTarget.value)}
              />
            </label>
            <button type="button" disabled={!canWriteOffStock || feedback.state === 'pending'} onClick={writeOffStock}>
              <AlertTriangle size={14} />
              {t('op.pos.quick.writeOffBtn')}
            </button>
          </div>
          <div className="pos-quick-grid">
            {quickActions.map(({ id, label, detail, Icon, disabled }) => (
              <button
                key={id}
                type="button"
                className="pos-quick-card"
                disabled={disabled}
                onClick={() => {
                  if (id === 'topUp') {
                    void topUpSelectedPosPlayer();
                  } else if (id === 'refund') {
                    setFeedback(emptyFeedback);
                    setCriticalAction('refund-sale');
                  } else if (id === 'void') {
                    setFeedback(emptyFeedback);
                    setCriticalAction('void-draft');
                  } else if (id === 'stockWriteOff') {
                    void writeOffStock();
                  } else if (id === 'newClient') {
                    void createPosPlayer();
                  } else {
                    triggerFeedback(setFeedback, label);
                  }
                }}
              >
                <Icon size={17} />
                <strong>{label}</strong>
                <span>{detail}</span>
              </button>
            ))}
          </div>
          {criticalAction === 'refund-sale' && (
            <CriticalActionConfirmation
              title={t('op.pos.quick.refundConfirmTitle')}
              detail={t('op.pos.quick.refundConfirmDetail', { amount: formatMoney(readMoney(selectedRefundableSale, 'total'), currencyCode) })}
              impact={t('op.pos.quick.refundConfirmImpact')}
              confirmLabel={t('op.pos.quick.refundConfirmBtn')}
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void refundLatestSale()}
            >
              <label className="critical-confirmation-field">
                <span>{t('op.pos.quick.refundReasonLabel')}</span>
                <input
                  value={refundReason}
                  disabled={feedback.state === 'pending'}
                  onChange={(event) => setRefundReason(event.currentTarget.value)}
                />
              </label>
            </CriticalActionConfirmation>
          )}
          {criticalAction === 'void-draft' && (
            <CriticalActionConfirmation
              title={t('op.pos.quick.voidConfirmTitle')}
              detail={t('op.pos.quick.voidConfirmDetail', { count: cartItems.length, amount: formatMinorUnits(cartTotalMinorUnits, currencyCode) })}
              impact={t('op.pos.quick.voidConfirmImpact')}
              confirmLabel={t('op.pos.quick.voidConfirmBtn')}
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void voidDraftCart()}
            >
              <label className="critical-confirmation-field">
                <span>{t('op.pos.quick.voidReasonLabel')}</span>
                <input
                  value={voidReason}
                  disabled={feedback.state === 'pending'}
                  onChange={(event) => setVoidReason(event.currentTarget.value)}
                />
              </label>
            </CriticalActionConfirmation>
          )}
        </section>
      </section>
    </Root>
  );
}
