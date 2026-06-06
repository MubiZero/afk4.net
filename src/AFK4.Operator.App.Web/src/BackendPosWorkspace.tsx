import { useEffect, useState } from 'react';
import { AlertTriangle, ArrowRightLeft, Banknote, CircleDollarSign, ReceiptText, Search, UserRoundPlus, X } from 'lucide-react';
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

const fixturePosProducts: PosCatalogItem[] = [
  { name: 'Кола 0.5', priceMinorUnits: 1200, category: 'Напитки', note: 'локальный пример', stockOnHand: 0, source: 'fixture' },
  { name: 'Вода 0.5', priceMinorUnits: 600, category: 'Напитки', note: 'локальный пример', stockOnHand: 0, source: 'fixture' },
  { name: 'Хот-дог', priceMinorUnits: 2800, category: 'Еда', note: 'локальный пример', stockOnHand: 0, source: 'fixture' },
  { name: 'Гостевой час', priceMinorUnits: 2500, category: 'Услуги', note: 'локальный пример', stockOnHand: 0, source: 'fixture' }
];

function projectPosProduct(product: PosProductDto, currencyCode: string): PosCatalogItem {
  const price = readMoney(product, 'price');
  return {
    productId: readString(product, 'productId') || undefined,
    name: readString(product, 'name', 'Товар'),
    priceMinorUnits: price?.minorUnits ?? 0,
    category: readString(product, 'categoryName', readString(product, 'categoryId', 'Каталог')),
    note: `${readString(product, 'sku', 'SKU')} · ${readNumber(product, 'stockOnHand', 0)} шт.`,
    stockOnHand: readNumber(product, 'stockOnHand', 0),
    source: price?.currencyCode === currencyCode || price !== null ? 'backend' : 'backend'
  };
}

export function BackendPosWorkspace({ currencyCode, backend }: { currencyCode: string; backend: OperatorBackendContext | null }) {
  const [activeCategory, setActiveCategory] = useState('Все');
  const [productSearch, setProductSearch] = useState('');
  const [paymentMethod, setPaymentMethod] = useState('Наличные');
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [currentShift, setCurrentShift] = useState<ShiftDto | null>(null);
  const [catalog, setCatalog] = useState<PosCatalogItem[]>(() => backend === null ? fixturePosProducts : []);
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
  const [stockWriteOffReason, setStockWriteOffReason] = useState('операторское списание');
  const [criticalAction, setCriticalAction] = useState<'refund-sale' | 'void-draft' | null>(null);
  const [refundReason, setRefundReason] = useState('Возврат по запросу клиента');
  const [voidReason, setVoidReason] = useState('Ошибка в черновике чека');
  const [cartItems, setCartItems] = useState<PosCartItem[]>(() => backend === null
    ? [
        { ...fixturePosProducts[0], quantity: 1 },
        { ...fixturePosProducts[3], quantity: 1 }
      ]
    : []);

  const loadBackendPos = async (nextBackend = backend) => {
    if (nextBackend === null) {
      setLoadStatus('fixture');
      setCatalog(fixturePosProducts);
      setCartItems((items) => items.length > 0 ? items : [
        { ...fixturePosProducts[0], quantity: 1 },
        { ...fixturePosProducts[3], quantity: 1 }
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
        ? nextCatalog.map((product) => projectPosProduct(product, currencyCode))
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
        label: 'Касса',
        state: 'failed',
        detail: projectOperatorError(error).detail
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

        const projected = Array.isArray(players) ? players.map(projectPlayerClient) : [];
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
        setFeedback({ label: 'Клиент', state: 'failed', detail: projectOperatorError(error).detail });
      });

    return () => {
      disposed = true;
    };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, playerSearch]);

  const categories = ['Все', ...Array.from(new Set(catalog.map((product) => product.category))).slice(0, 5)];
  const visibleProducts = catalog.filter((product) => {
    const categoryMatches = activeCategory === 'Все' || product.category === activeCategory;
    const searchMatches = `${product.name} ${product.category} ${product.note}`.toLowerCase().includes(productSearch.trim().toLowerCase());
    return categoryMatches && searchMatches;
  });
  const selectedPosPlayer = posPlayers.find((player) => player.playerAccountId === selectedPlayerId) ?? null;
  const selectedPosPlayerId = selectedPosPlayer?.playerAccountId ?? null;
  const playerSearchQuery = playerSearch.trim();
  const paymentMethodName = paymentMethod === 'Карта' ? 'card_manual' : 'cash';
  const newPlayerDisplayName = newPlayerName.trim() || playerSearchQuery;
  const cartTotalMinorUnits = cartItems.reduce((sum, item) => sum + item.priceMinorUnits * item.quantity, 0);
  const acceptedCashMinorUnits = paymentMethod === 'Наличные'
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
    triggerFeedback(setFeedback, `${product.name} добавлен`, 'confirmed');
  };

  const createPosPlayer = async () => {
    setFeedback({ label: 'Новая карта', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
        throw new Error('Нет прав на создание карты клиента.');
      }

      const displayName = newPlayerDisplayName;
      if (!displayName) {
        throw new Error('Введите имя клиента для новой карты.');
      }

      const player = await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).players.createPlayer(nextBackend.branchId, {
        organizationId: nextBackend.session.organizationId,
        displayName,
        phoneNumber: newPlayerPhone.trim() || null,
        idempotencyKey: createIdempotencyKey('player-create')
      });
      const projected = projectPlayerClient(player);
      setPosPlayers((players) => [
        projected,
        ...players.filter((candidate) => candidate.playerAccountId !== projected.playerAccountId)
      ]);
      setSelectedPlayerId(projected.playerAccountId ?? '');
      setNewPlayerName('');
      setNewPlayerPhone('');
      setFeedback({ label: 'Новая карта', state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: 'Новая карта',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const writeOffStock = async () => {
    setFeedback({ label: 'Списание склада', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.manageInventoryStock)) {
        throw new Error('Нет прав на управление остатками.');
      }

      const productId = selectedStockProduct?.productId;
      const quantity = Number(stockWriteOffQuantity);
      const reason = stockWriteOffReason.trim();
      if (!productId || !Number.isInteger(quantity) || quantity <= 0 || !reason) {
        throw new Error('Выберите товар платформы, целое количество больше нуля и причину списания.');
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

      setFeedback({ label: 'Списание склада', state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: 'Списание склада',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const topUpSelectedPosPlayer = async () => {
    setFeedback({ label: 'Пополнить депозит', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.topUpWallet)) {
        throw new Error('Нет прав на пополнение депозита.');
      }

      if (!selectedPosPlayerId) {
        throw new Error('Выберите клиента платформы для пополнения депозита.');
      }

      if (cartTotalMinorUnits <= 0) {
        throw new Error('Добавьте сумму в корзину перед пополнением депозита.');
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
        label: 'Пополнить депозит',
        state: 'confirmed',
        detail: formatMinorUnits(cartTotalMinorUnits, currencyCode)
      });
    } catch (error) {
      setFeedback({
        label: 'Пополнить депозит',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const acceptPayment = async () => {
    setFeedback({ label: 'Оплата', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.createPosSale) || !hasPermission(nextBackend.session, permissionNames.payPosSale)) {
        throw new Error('Нет прав на создание или оплату чека.');
      }

      if (!shiftId) {
        throw new Error('Откройте смену перед оплатой.');
      }

      if (cartItems.length === 0 || cartItems.some((item) => !item.productId || item.source !== 'backend')) {
        throw new Error('Каталог товаров не загружен для текущей корзины.');
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
        throw new Error('Платформа не подтвердила номер чека. Повторите операцию или обратитесь в поддержку.');
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
      setFeedback({ label: 'Оплата', state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: 'Оплата',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const refundLatestSale = async () => {
    setCriticalAction(null);
    setFeedback({ label: 'Возврат по чеку', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.refundPosSale)) {
        throw new Error('Нет прав на возврат по чеку.');
      }

      if (!selectedRefundableSaleId) {
        throw new Error('Выберите чек для возврата.');
      }

      const reason = refundReason.trim();
      if (!reason) {
        throw new Error('Введите причину возврата.');
      }

      await createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session).pos.refundSale(selectedRefundableSaleId, {
        organizationId: nextBackend.session.organizationId,
        reason,
        idempotencyKey: createIdempotencyKey('pos-refund')
      });
      setFeedback({ label: 'Возврат по чеку', state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: 'Возврат по чеку',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const loadSaleDetail = async (saleId: string) => {
    setSelectedReceiptDetail(null);
    setFeedback({ label: 'Детали чека', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.viewReceipt)) {
        throw new Error('Нет прав на просмотр чеков.');
      }

      if (!saleId) {
        throw new Error('Выберите чек из списка.');
      }

      const clients = createAuthenticatedOperatorClients(nextBackend.config, nextBackend.session);
      const sale = await clients.pos.getSale(saleId);
      const latestReceipt = readRecord(sale, 'latestReceipt');
      const receiptId = readString(latestReceipt, 'receiptId');
      const receipt = receiptId ? await clients.pos.getReceipt(receiptId) : null;
      setSelectedSaleDetail(sale);
      setSelectedReceiptDetail(receipt);
      setFeedback({ label: 'Детали чека', state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: 'Детали чека',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const selectedReceiptRecord = selectedReceiptDetail ?? readRecord(selectedSaleDetail, 'latestReceipt');

  const printSelectedReceipt = () => {
    setFeedback({ label: 'Печать чека', state: 'pending' });
    try {
      if (selectedSaleDetail === null) {
        throw new Error('Откройте чек платформы перед печатью.');
      }

      const receiptText = buildPosReceiptText(selectedSaleDetail, selectedReceiptRecord, currencyCode);
      const printWindow = window.open('', '_blank', 'width=360,height=640');
      if (printWindow === null) {
        throw new Error('Не удалось открыть окно печати чека.');
      }

      printWindow.document.write(`<pre style="font: 13px/1.45 monospace; white-space: pre-wrap;">${escapeHtml(receiptText)}</pre>`);
      printWindow.document.close();
      printWindow.focus();
      printWindow.print();
      setFeedback({ label: 'Печать чека', state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: 'Печать чека',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const exportSelectedReceipt = () => {
    setFeedback({ label: 'Экспорт чека', state: 'pending' });
    try {
      if (selectedSaleDetail === null) {
        throw new Error('Откройте чек платформы перед экспортом.');
      }

      const receiptText = buildPosReceiptText(selectedSaleDetail, selectedReceiptRecord, currencyCode);
      const receiptNumber = readString(selectedReceiptRecord, 'receiptNumber', 'receipt');
      downloadTextFile(`${safeReceiptFileName(receiptNumber)}.txt`, receiptText);
      setFeedback({ label: 'Экспорт чека', state: 'confirmed' });
    } catch (error) {
      setFeedback({
        label: 'Экспорт чека',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  const voidDraftCart = async () => {
    setCriticalAction(null);
    setFeedback({ label: 'Аннулировать черновик', state: 'pending' });
    try {
      const nextBackend = requireBackend(backend);
      if (!hasPermission(nextBackend.session, permissionNames.createPosSale) || !hasPermission(nextBackend.session, permissionNames.voidPosSale)) {
        throw new Error('Нет прав на создание или аннулирование чека.');
      }

      if (!shiftId) {
        throw new Error('Открытая смена обязательна для аннулирования черновика.');
      }

      if (cartItems.length === 0 || cartItems.some((item) => !item.productId || item.source !== 'backend')) {
        throw new Error('Каталог товаров не загружен для текущей корзины.');
      }

      const reason = voidReason.trim();
      if (!reason) {
        throw new Error('Введите причину аннулирования.');
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
        throw new Error('Платформа не подтвердила черновик чека. Повторите операцию или обратитесь в поддержку.');
      }

      const voidedSale = await clients.pos.voidSale(saleId, {
        organizationId: nextBackend.session.organizationId,
        reason,
        idempotencyKey: createIdempotencyKey('pos-void')
      });

      setLastSale(voidedSale);
      setCartItems([]);
      setFeedback({ label: 'Аннулировать черновик', state: 'confirmed' });
      await loadBackendPos(nextBackend);
    } catch (error) {
      setFeedback({
        label: 'Аннулировать черновик',
        state: 'failed',
        detail: projectOperatorError(error).detail
      });
    }
  };

  return (
    <main className="workspace-screen pos-screen">
      <section className="screen-head pos-head">
        <div>
          <span>Продажи</span>
          <h1>Продажи · чек и кассовые операции</h1>
        </div>
        <div className="screen-actions">
          <span className={`map-load-state ${loadStatus === 'backend' ? 'ready' : loadStatus}`}>{workspaceLoadStatusLabel(loadStatus, 'Платформа подключена')}</span>
        </div>
      </section>

      <section className="state-strip pos-state-strip" aria-label="Сводка продаж">
        <StateFlag label="Продажи" value={`${salesRows.length} · ${grossSales ? formatMinorUnits(grossSales.minorUnits, grossSales.currencyCode) : `0 ${currencyCode}`}`} />
        <StateFlag label="Возвраты" value={refundsTotal ? formatMinorUnits(refundsTotal.minorUnits, refundsTotal.currencyCode) : `0 ${currencyCode}`} critical={(refundsTotal?.minorUnits ?? 0) > 0} />
        <StateFlag label="Товары" value={`${catalog.length} поз.`} />
        <StateFlag label="Склад" value={`${lowStockCount} низко`} critical={lowStockCount > 0} />
        <StateFlag label="Смена" value={shiftStateLabel(shiftState)} critical={!shiftId} />
      </section>

      <section className="pos-layout">
        <section className="pos-panel pos-catalog-panel">
          <header className="pos-panel-title">
            <span>Каталог</span>
            <strong>активные товары, остатки и поиск</strong>
          </header>
          <label className="pos-search">
            <Search size={14} />
            <input
              placeholder="Товар, услуга, SKU"
              value={productSearch}
              onChange={(event) => setProductSearch(event.currentTarget.value)}
            />
          </label>
          <div className="pos-category-row" aria-label="Категории товаров">
            {categories.map((category) => (
              <button
                key={category}
                type="button"
                className={activeCategory === category ? 'active' : undefined}
                onClick={() => setActiveCategory(category)}
              >
                {category}
              </button>
            ))}
          </div>
          <div className="pos-catalog-grid">
            {visibleProducts.length === 0 ? (
              <div className="pos-empty-state">
                <strong>Каталог пуст</strong>
                <span>{loadStatus === 'backend' ? 'Активных товаров для этого филиала нет.' : 'Загрузите каталог.'}</span>
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
            <span>Корзина</span>
            <strong>{shiftId ? 'смена открыта' : 'откройте смену'}</strong>
          </header>
          <div className="pos-cart-client">
            <UserRoundPlus size={17} />
            <div>
              <span>Клиент</span>
              <strong>{selectedPosPlayer ? selectedPosPlayer.name : 'Гость · без карты'}</strong>
              <em>{selectedPosPlayer
                ? `${selectedPosPlayer.phoneNumber || 'без телефона'} · ${formatMinorUnits(selectedPosPlayer.balanceMinorUnits, currencyCode)}`
                : 'продажа без карты клиента'}</em>
            </div>
            <button
              type="button"
              onClick={() => {
                setPlayerSearch('');
                setSelectedPlayerId('');
                setPosPlayers([]);
              }}
            >
              Гость
            </button>
          </div>
          <label className="pos-search pos-client-search">
            <Search size={14} />
            <input
              aria-label="Клиент"
              value={playerSearch}
              disabled={backend !== null && !hasPermission(backend.session, permissionNames.viewPlayers)}
              placeholder="имя или телефон клиента"
              onChange={(event) => setPlayerSearch(event.currentTarget.value)}
            />
          </label>
          {playerSearchQuery.length > 1 && (
            <div className="pos-client-candidates" aria-label="Клиенты продажи">
              {posPlayers.map((player) => (
                <button
                  key={player.playerAccountId ?? player.name}
                  type="button"
                  className={player.playerAccountId === selectedPlayerId ? 'active' : undefined}
                  disabled={!player.playerAccountId || feedback.state === 'pending'}
                  onClick={() => setSelectedPlayerId(player.playerAccountId ?? '')}
                >
                  <strong>{player.name}</strong>
                  <span>{formatMinorUnits(player.balanceMinorUnits, currencyCode)} · долг {formatMinorUnits(player.debtMinorUnits, currencyCode)}</span>
                </button>
              ))}
              {playerLoadStatus === 'loading' && <p>Поиск клиента</p>}
              {playerLoadStatus !== 'loading' && posPlayers.length === 0 && <p>Клиент не найден</p>}
            </div>
          )}
          <div className="pos-new-client-form">
            <label>
              <span>Новая карта</span>
              <input
                aria-label="Имя клиента"
                value={newPlayerName}
                disabled={backend !== null && !hasPermission(backend.session, permissionNames.createPlayerAccount)}
                placeholder={playerSearchQuery || 'имя клиента'}
                onChange={(event) => setNewPlayerName(event.currentTarget.value)}
              />
            </label>
            <label>
              <span>Телефон</span>
              <input
                aria-label="Телефон клиента"
                value={newPlayerPhone}
                disabled={backend !== null && !hasPermission(backend.session, permissionNames.createPlayerAccount)}
                placeholder="+992..."
                onChange={(event) => setNewPlayerPhone(event.currentTarget.value)}
              />
            </label>
            <button type="button" disabled={!canCreatePosPlayer || feedback.state === 'pending'} onClick={createPosPlayer}>
              <UserRoundPlus size={14} />
              Создать
            </button>
          </div>
          <div className="pos-cart-list">
            {cartItems.length === 0 ? (
              <article className="pos-cart-row empty">
                <div>
                  <strong>Корзина пуста</strong>
                  <span>Добавьте товар из каталога.</span>
                </div>
                <b>{formatMinorUnits(0, currencyCode)}</b>
              </article>
            ) : (
              cartItems.map((item) => (
                <article key={`${item.productId ?? item.name}-${item.name}`} className="pos-cart-row interactive-row">
                  <div>
                    <strong>{item.name}</strong>
                    <span>{item.quantity} шт.</span>
                  </div>
                  <b>{formatMinorUnits(item.priceMinorUnits * item.quantity, currencyCode)}</b>
                </article>
              ))
            )}
          </div>
          <div className="pos-total-card">
            <span>Итого к оплате</span>
            <strong>{formatMinorUnits(cartTotalMinorUnits, currencyCode)}</strong>
            <em>{lastSale ? 'последний чек принят' : 'чек создаётся после подтверждения платформы'}</em>
          </div>
          <FeedbackNotice feedback={feedback} />
        </section>

        <section className="pos-panel pos-payment-panel">
          <header className="pos-panel-title">
            <span>Оплата</span>
            <strong>чек и подтверждение оплаты</strong>
          </header>
          <div className="pos-payment-methods">
            {['Наличные', 'Карта', 'Депозит'].map((method) => (
              <button
                key={method}
                type="button"
                className={paymentMethod === method ? 'active' : undefined}
                disabled={method === 'Депозит' || feedback.state === 'pending'}
                title={method === 'Депозит' ? 'Оплата с депозита будет включена после подключения депозитного платежа.' : undefined}
                onClick={() => setPaymentMethod(method)}
              >
                {method === 'Наличные' && <Banknote size={15} />}
                {method === 'Карта' && <CircleDollarSign size={15} />}
                {method === 'Депозит' && <ReceiptText size={15} />}
                {method}
              </button>
            ))}
          </div>
          <div className="pos-payment-summary">
            <div><span>Принято</span><strong>{formatMinorUnits(acceptedCashMinorUnits, currencyCode)}</strong></div>
            <div><span>Сдача</span><strong>{formatMinorUnits(changeMinorUnits, currencyCode)}</strong></div>
            <div><span>Смена</span><strong>{shiftId ? 'Открыта' : 'Нет'}</strong></div>
          </div>
          <button type="button" className="pos-primary-action" disabled={!canAcceptPayment || feedback.state === 'pending'} onClick={acceptPayment}>Принять оплату</button>
          <button type="button" className="pos-secondary-action" onClick={() => setCartItems([])}>Очистить корзину</button>
        </section>

        <section className="pos-panel pos-receipts-panel">
          <header className="pos-panel-title">
            <span>Последние чеки</span>
            <strong>чеки за смену</strong>
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
                <strong>{posSaleStateLabel(readString(row, 'state', 'sale'))}</strong>
                <em>{posSaleLineSummary(row)}</em>
                <b>{formatMoney(readMoney(row, 'total'), currencyCode)}</b>
              </button>
            ))}
            {salesRows.length === 0 && (
              <article className="pos-receipt-row">
                <span>—</span>
                <strong>Чеков нет</strong>
                <em>платформа</em>
                <b>0 {currencyCode}</b>
              </article>
            )}
          </div>
          {selectedSaleDetail !== null && (
            <div className="pos-sale-detail">
              <div>
                <span>Детали чека</span>
                <strong>{posSaleStateLabel(readString(selectedSaleDetail, 'state', 'sale'))}</strong>
                <b>{formatMoney(readMoney(selectedSaleDetail, 'total'), currencyCode)}</b>
              </div>
              {readArray(selectedSaleDetail, 'lines').slice(0, 3).map((line) => (
                <p key={`${readString(line, 'productId')}-${readNumber(line, 'quantity', 0)}`}>
                  {readString(line, 'productName', 'Товар')} · {readNumber(line, 'quantity', 0)} × {formatMoney(readMoney(line, 'unitPrice'), currencyCode)}
                </p>
              ))}
              {selectedReceiptDetail !== null && (
                <div className="pos-receipt-detail">
                  <span>Чек платформы</span>
                  <strong>{readString(selectedReceiptDetail, 'receiptNumber', 'чек')}</strong>
                  <b>{formatMoney(readMoney(selectedReceiptDetail, 'total'), currencyCode)}</b>
                  <p>{posReceiptTypeLabel(readString(selectedReceiptDetail, 'receiptType', 'sale'))}</p>
                </div>
              )}
              <div className="pos-receipt-actions">
                <button type="button" disabled={feedback.state === 'pending'} onClick={printSelectedReceipt}>
                  <ReceiptText size={13} />
                  Печать
                </button>
                <button type="button" disabled={feedback.state === 'pending'} onClick={exportSelectedReceipt}>
                  <ArrowRightLeft size={13} />
                  Экспорт
                </button>
              </div>
            </div>
          )}
        </section>

        <section className="pos-panel pos-quick-panel">
          <header className="pos-panel-title">
            <span>Быстрые операции</span>
            <strong>действия ждут подтверждения платформы</strong>
          </header>
          <div className="pos-stock-form">
            <label>
              <span>Списание</span>
              <select
                aria-label="Товар для списания"
                value={selectedStockProduct?.productId ?? ''}
                disabled={backendCatalogProducts.length === 0 || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffProductId(event.currentTarget.value)}
              >
                {backendCatalogProducts.length === 0 && <option value="">Нет товара платформы</option>}
                {backendCatalogProducts.map((product) => (
                  <option key={product.productId} value={product.productId}>
                    {product.name} · {product.stockOnHand} шт.
                  </option>
                ))}
              </select>
            </label>
            <label>
              <span>Кол-во</span>
              <input
                aria-label="Количество списания"
                inputMode="numeric"
                value={stockWriteOffQuantity}
                disabled={!canWriteOffStock || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffQuantity(event.currentTarget.value)}
              />
            </label>
            <label className="pos-stock-reason">
              <span>Причина</span>
              <input
                aria-label="Причина списания"
                value={stockWriteOffReason}
                disabled={!canWriteOffStock || feedback.state === 'pending'}
                onChange={(event) => setStockWriteOffReason(event.currentTarget.value)}
              />
            </label>
            <button type="button" disabled={!canWriteOffStock || feedback.state === 'pending'} onClick={writeOffStock}>
              <AlertTriangle size={14} />
              Списать
            </button>
          </div>
          <div className="pos-quick-grid">
            {[
              ['Пополнить депозит', selectedPosPlayer ? `корзина ${formatMinorUnits(cartTotalMinorUnits, currencyCode)}` : 'выберите клиента', CircleDollarSign],
              ['Возврат по чеку', 'требует выбранный чек', ReceiptText],
              ['Аннулировать черновик', 'создать и аннулировать чек', X],
              ['Списать склад', selectedStockProduct?.name ?? 'выберите товар', AlertTriangle],
              ['Новый клиент', newPlayerDisplayName || 'заполните имя', UserRoundPlus],
              ['Внести наличные', 'экран платежей', Banknote]
            ].map(([label, detail, Icon]) => (
              <button
                key={label as string}
                type="button"
                className="pos-quick-card"
                disabled={((label as string) === 'Пополнить депозит' && (!canTopUpPosWallet || feedback.state === 'pending'))
                  || ((label as string) === 'Возврат по чеку' && (!canRefundSelectedSale || feedback.state === 'pending'))
                  || ((label as string) === 'Аннулировать черновик' && (!canVoidDraftCart || feedback.state === 'pending'))
                  || ((label as string) === 'Списать склад' && (!canWriteOffStock || feedback.state === 'pending'))
                  || ((label as string) === 'Новый клиент' && (!canCreatePosPlayer || feedback.state === 'pending'))}
                onClick={() => {
                  if ((label as string) === 'Пополнить депозит') {
                    void topUpSelectedPosPlayer();
                  } else if ((label as string) === 'Возврат по чеку') {
                    setFeedback(emptyFeedback);
                    setCriticalAction('refund-sale');
                  } else if ((label as string) === 'Аннулировать черновик') {
                    setFeedback(emptyFeedback);
                    setCriticalAction('void-draft');
                  } else if ((label as string) === 'Списать склад') {
                    void writeOffStock();
                  } else if ((label as string) === 'Новый клиент') {
                    void createPosPlayer();
                  } else {
                    triggerFeedback(setFeedback, label as string);
                  }
                }}
              >
                <Icon size={17} />
                <strong>{label as string}</strong>
                <span>{detail as string}</span>
              </button>
            ))}
          </div>
          {criticalAction === 'refund-sale' && (
            <CriticalActionConfirmation
              title="Подтвердите возврат"
              detail={`Выбранный чек · ${formatMoney(readMoney(selectedRefundableSale, 'total'), currencyCode)}`}
              impact="Платформа создаст возврат по выбранному чеку и запишет причину в аудит."
              confirmLabel="Подтвердить возврат"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void refundLatestSale()}
            >
              <label className="critical-confirmation-field">
                <span>Причина возврата</span>
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
              title="Подтвердите аннулирование"
              detail={`Корзина · ${cartItems.length} поз. · ${formatMinorUnits(cartTotalMinorUnits, currencyCode)}`}
              impact="Черновик чека будет создан и аннулирован после подтверждения платформы."
              confirmLabel="Подтвердить аннулирование"
              disabled={feedback.state === 'pending'}
              onCancel={() => setCriticalAction(null)}
              onConfirm={() => void voidDraftCart()}
            >
              <label className="critical-confirmation-field">
                <span>Причина аннулирования</span>
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
    </main>
  );
}
